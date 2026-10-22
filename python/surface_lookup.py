from pathlib import Path

import numpy as np

import landxml_reader

# Barycentric tolerance. A point on an edge or a vertex counts as inside
EPS = 1e-9
# The grid may hold this many triangle entries per triangle before the cells are made larger
MAX_ENTRIES = 12
MIN_CELL = 0.5


class Surface:
    """Visible triangles of one TIN with a grid index. Easting is x and northing is y."""

    def __init__(self, xyz, faces):
        xyz = np.asarray(xyz, dtype=np.float64).reshape(-1, 3)
        faces = np.asarray(faces, dtype=np.int64).reshape(-1, 3)
        self.point_count = len(xyz)
        self.origin = (float(xyz[:, 0].min()), float(xyz[:, 1].min())) if len(xyz) else (0.0, 0.0)
        corners = xyz[faces] - np.array([self.origin[0], self.origin[1], 0.0])
        x, y, z = corners[:, :, 0], corners[:, :, 1], corners[:, :, 2]
        det = (y[:, 1] - y[:, 2]) * (x[:, 0] - x[:, 2]) + (x[:, 2] - x[:, 1]) * (y[:, 0] - y[:, 2])
        keep = np.isfinite(det) & (det != 0)
        self.degenerate = int(len(det) - keep.sum())
        self.x, self.y, self.z, self.det = x[keep], y[keep], z[keep], det[keep]
        self.count = int(keep.sum())
        self.build_grid()

    @classmethod
    def from_dir(cls, folder):
        folder = Path(folder)
        return cls(np.load(folder / "points.npy"), np.load(folder / "faces.npy"))

    def build_grid(self):
        if self.count == 0:
            self.cell, self.nx, self.ny = 1.0, 0, 0
            self.keys = np.zeros(0, dtype=np.int64)
            self.tris = np.zeros(0, dtype=np.int64)
            return
        lo_x, hi_x = self.x.min(axis=1), self.x.max(axis=1)
        lo_y, hi_y = self.y.min(axis=1), self.y.max(axis=1)
        cell = max(float(np.median(np.maximum(hi_x - lo_x, hi_y - lo_y))), MIN_CELL)
        while True:
            i0, i1 = np.floor(lo_x / cell).astype(np.int64), np.floor(hi_x / cell).astype(np.int64)
            j0, j1 = np.floor(lo_y / cell).astype(np.int64), np.floor(hi_y / cell).astype(np.int64)
            span_j = j1 - j0 + 1
            cells = (i1 - i0 + 1) * span_j
            if cells.sum() <= MAX_ENTRIES * self.count:
                break
            cell *= 1.5
        self.cell = cell
        self.nx = int(i1.max()) + 1
        self.ny = int(j1.max()) + 1
        tri = np.repeat(np.arange(self.count, dtype=np.int64), cells)
        start = np.cumsum(cells) - cells
        inside = np.arange(len(tri), dtype=np.int64) - np.repeat(start, cells)
        width = np.repeat(span_j, cells)
        keys = (np.repeat(i0, cells) + inside // width) * self.ny + np.repeat(j0, cells) + inside % width
        order = np.argsort(keys, kind="stable")
        self.keys, self.tris = keys[order], tri[order]

    def candidates(self, px, py):
        i, j = int(np.floor(px / self.cell)), int(np.floor(py / self.cell))
        if i < 0 or j < 0 or i >= self.nx or j >= self.ny:
            return self.tris[:0]
        key = i * self.ny + j
        return self.tris[np.searchsorted(self.keys, key, "left"):np.searchsorted(self.keys, key, "right")]

    def z_at(self, easting, northing):
        """Interpolated z from the visible triangle that holds the point, or None. The triangle with the lowest index wins on a shared edge."""
        if self.count == 0:
            return None
        px, py = easting - self.origin[0], northing - self.origin[1]
        near = self.candidates(px, py)
        if len(near) == 0:
            return None
        x, y, z, det = self.x[near], self.y[near], self.z[near], self.det[near]
        l0 = ((y[:, 1] - y[:, 2]) * (px - x[:, 2]) + (x[:, 2] - x[:, 1]) * (py - y[:, 2])) / det
        l1 = ((y[:, 2] - y[:, 0]) * (px - x[:, 2]) + (x[:, 0] - x[:, 2]) * (py - y[:, 2])) / det
        l2 = 1.0 - l0 - l1
        hit = np.flatnonzero((l0 >= -EPS) & (l1 >= -EPS) & (l2 >= -EPS))
        if len(hit) == 0:
            return None
        first = hit[0]
        return float(l0[first] * z[first, 0] + l1[first] * z[first, 1] + l2[first] * z[first, 2])

    def distance_to(self, easting, northing, limit):
        """Distance to the nearest visible triangle, or None when none lies within limit."""
        if self.count == 0:
            return None
        px, py = easting - self.origin[0], northing - self.origin[1]
        i0, i1 = int(np.floor((px - limit) / self.cell)), int(np.floor((px + limit) / self.cell))
        j0, j1 = max(int(np.floor((py - limit) / self.cell)), 0), min(int(np.floor((py + limit) / self.cell)), self.ny - 1)
        if j0 > j1:
            return None
        parts = []
        for i in range(max(i0, 0), min(i1, self.nx - 1) + 1):
            lo = np.searchsorted(self.keys, i * self.ny + j0, "left")
            hi = np.searchsorted(self.keys, i * self.ny + j1, "right")
            parts.append(self.tris[lo:hi])
        if not parts:
            return None
        near = np.unique(np.concatenate(parts))
        if len(near) == 0:
            return None
        best = float(triangle_distance(px, py, self.x[near], self.y[near], self.det[near]).min())
        return best if best <= limit else None


def segment_distance(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    length = dx * dx + dy * dy
    t = np.where(length > 0, ((px - ax) * dx + (py - ay) * dy) / np.where(length > 0, length, 1.0), 0.0)
    t = np.clip(t, 0.0, 1.0)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))


def triangle_distance(px, py, x, y, det):
    """Distance from a point to each triangle, zero when the point is inside."""
    l0 = ((y[:, 1] - y[:, 2]) * (px - x[:, 2]) + (x[:, 2] - x[:, 1]) * (py - y[:, 2])) / det
    l1 = ((y[:, 2] - y[:, 0]) * (px - x[:, 2]) + (x[:, 0] - x[:, 2]) * (py - y[:, 2])) / det
    inside = (l0 >= -EPS) & (l1 >= -EPS) & (1.0 - l0 - l1 >= -EPS)
    edges = np.minimum.reduce([segment_distance(px, py, x[:, a], y[:, a], x[:, b], y[:, b]) for a, b in ((0, 1), (1, 2), (2, 0))])
    return np.where(inside, 0.0, edges)


def load_surface(landxml_dir, name):
    return Surface.from_dir(Path(landxml_dir) / landxml_reader.folder_name(name))


def lookup_z(surface, easting, northing):
    """Interpolated z of a surface at a point from the visible triangle that holds it, None outside every visible triangle."""
    return surface.z_at(easting, northing)


def lookup_many(surface, points):
    """z for a list of (easting, northing) points, None where a point is outside."""
    return [surface.z_at(easting, northing) for easting, northing in points]
