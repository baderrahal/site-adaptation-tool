"""Checks the counts of the C3D round a second way, on seeded samples and on all items (R159).

levels: the level of the road surface at each ramp front corner by brute force over every visible triangle and every
triangle side, with no grid, against the difference c3d_connect.py wrote. curb: the distance from the middle of the ramp
front to the curb and to the right of way, by brute force over the segments of pieces.json with no geometry library,
against the curb distance of the plot front. inventory: the model space count per class of the LISP inventory against the
ACadSharp count of the same drawing. Matches and mismatches are counted, nothing is fixed.
"""
import argparse
import json
import math
import random
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import c3d_run
import common
import landxml_reader

LEVEL_SLACK = 0.0011
CURB_SLACK = 0.05
ROW_SLACK = 0.01
# ACadSharp names some classes another way than the DXF names of entget
CLASS_NAMES = {"LWPOLYLINE": "LWPOLYLINE", "LINE": "LINE", "ARC": "ARC", "CIRCLE": "CIRCLE", "INSERT": "INSERT", "TEXT": "TEXT", "MTEXT": "MTEXT", "HATCH": "HATCH"}


def brute_level(points, faces, x, y, tolerance):
    """The level at a point from every triangle at once, or from the nearest side within tolerance, or None."""
    tri = points[faces]
    near = (np.abs(tri[:, :, 0] - x).min(axis=1) < 10) & (np.abs(tri[:, :, 1] - y).min(axis=1) < 10)
    tri = tri[near]
    if len(tri) == 0:
        return None
    (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = (tri[:, k, :].T for k in range(3))
    det = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
    with np.errstate(divide="ignore", invalid="ignore"):
        a = ((y1 - y2) * (x - x2) + (x2 - x1) * (y - y2)) / det
        b = ((y2 - y0) * (x - x2) + (x0 - x2) * (y - y2)) / det
    c = 1 - a - b
    inside = np.flatnonzero((det != 0) & (a >= -1e-9) & (b >= -1e-9) & (c >= -1e-9))
    if len(inside):
        k = inside[0]
        return float(a[k] * z0[k] + b[k] * z1[k] + c[k] * z2[k])
    best = (math.inf, None)
    for (ax, ay, az), (bx, by, bz) in (((x0, y0, z0), (x1, y1, z1)), ((x1, y1, z1), (x2, y2, z2)), ((x2, y2, z2), (x0, y0, z0))):
        for k in range(len(ax)):
            dx, dy = bx[k] - ax[k], by[k] - ay[k]
            length = dx * dx + dy * dy
            t = min(max(((x - ax[k]) * dx + (y - ay[k]) * dy) / length, 0.0), 1.0) if length > 0 else 0.0
            distance = math.hypot(x - (ax[k] + t * dx), y - (ay[k] + t * dy))
            if distance < best[0]:
                best = (distance, az[k] + t * (bz[k] - az[k]))
    return float(best[1]) if best[0] <= tolerance else None


def segments_of(pieces, layers):
    """Every straight segment of the lines and polylines on the layers, a layer inside a bound xref included."""
    wanted = [layer.lower() for layer in layers]
    found = []
    for piece in pieces:
        layer = str(piece.get("layer") or "").lower()
        if piece.get("kind") not in ("line", "polyline") or not any(layer == name or layer.endswith("$" + name) for name in wanted):
            continue
        points = [point[:2] for point in piece.get("pts") or []]
        if piece.get("closed") and len(points) > 2:
            points.append(points[0])
        found += [(a[0], a[1], b[0], b[1]) for a, b in zip(points, points[1:])]
    return found


def nearest_segment(segments, x, y):
    best = math.inf
    for ax, ay, bx, by in segments:
        dx, dy = bx - ax, by - ay
        length = dx * dx + dy * dy
        t = min(max(((x - ax) * dx + (y - ay) * dy) / length, 0.0), 1.0) if length > 0 else 0.0
        best = min(best, math.hypot(x - (ax + t * dx), y - (ay + t * dy)))
    return best


def sample(items, seed, size):
    items = sorted(items, key=lambda item: item["plot"])
    return random.Random(seed).sample(items, min(size, len(items))) if seed is not None else items


def check_levels(rows, points, faces, source, tolerance):
    counts = Counter()
    for row in rows:
        stored = row["corners"].get(source) or []
        for (x, y, level), diff in zip(row.get("rampFront") or [], stored):
            mine = brute_level(points, faces, x, y, tolerance)
            if mine is None and diff is None:
                counts["bothNone"] += 1
            elif mine is None or diff is None:
                counts["mismatch"] += 1
            else:
                counts["match" if abs((mine - level) - diff) <= LEVEL_SLACK else "mismatch"] += 1
    return dict(counts)


def check_curb(rows, curbs, rows_of_way):
    counts = Counter()
    for row in rows:
        front = row.get("rampFront")
        if not front:
            counts["noRamp"] += 1
            continue
        x, y = (front[0][0] + front[1][0]) / 2, (front[0][1] + front[1][1]) / 2
        counts["curbMatch" if abs(nearest_segment(curbs, x, y) - row["curb"]) <= CURB_SLACK else "curbMismatch"] += 1
        counts["rowOnFront" if nearest_segment(rows_of_way, x, y) <= ROW_SLACK else "rowOffFront"] += 1
    return dict(counts)


def check_inventory(tsv_lines, layers_json):
    """Model space count per class, LISP against ACadSharp, for the classes both name the same way."""
    lisp = Counter()
    for line in tsv_lines:
        cells = line.split("\t")
        if len(cells) >= 3 and cells[2] == "Model":
            lisp[cells[0]] += 1
    cad = Counter()
    for layer in layers_json.get("layers", []):
        for kind, count in (layer.get("byType") or {}).items():
            cad[kind] += count
    result = {"match": 0, "mismatch": []}
    for name in CLASS_NAMES:
        if lisp[name] == cad[CLASS_NAMES[name]]:
            result["match"] += 1
        else:
            result["mismatch"].append({"class": name, "lisp": lisp[name], "acadsharp": cad[CLASS_NAMES[name]]})
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for the C3D round (R159)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--size", type=int, default=20)
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    connect = config["connect"]
    root = Path(config["probeRoot"])
    report = json.loads((root / "out" / "connect" / "connect.json").read_text(encoding="utf-8"))
    road = config["checkSurface"]
    folder = Path(connect["landxmlDir"]) / landxml_reader.folder_name(road)
    points, faces = np.load(folder / "points.npy"), np.load(folder / "faces.npy")
    cad = root / "out" / "cad"
    curb_entry, row_entry = connect["curb"], connect["lines"][connect["backOfSidewalk"]]
    curbs = segments_of(json.loads((cad / Path(config["drawings"][curb_entry["drawing"]]).name / "pieces.json").read_text(encoding="utf-8"))["pieces"], curb_entry["layers"])
    rows_of_way = segments_of(json.loads((cad / Path(config["drawings"][row_entry["drawing"]]).name / "pieces.json").read_text(encoding="utf-8"))["pieces"], row_entry["layers"])
    result = {"levels": {}, "curb": {}, "inventory": {}}
    for seed in (14, 7, None):
        label = "all" if seed is None else "seed%d" % seed
        rows = sample(report["rows"], seed, args.size)
        result["levels"][label] = check_levels(rows, points, faces, "snap:" + road, connect["snapM"])
        result["curb"][label] = check_curb(rows, curbs, rows_of_way)
        print(label, "levels", result["levels"][label], "curb", result["curb"][label])
    for role, relative in config["drawings"].items():
        tsv = root / "out" / "inventory" / (role + ".tsv")
        layers = cad / Path(relative).name / "layers.json"
        if tsv.exists() and layers.exists():
            result["inventory"][role] = check_inventory(tsv.read_text(encoding="utf-8", errors="replace").splitlines(), json.loads(layers.read_text(encoding="utf-8")))
            print(role, "inventory", result["inventory"][role])
        else:
            result["inventory"][role] = "no inventory file"
    common.write_json(root / "out" / "check" / "c3d_check.json", result)
    return 0


if __name__ == "__main__":
    sys.exit(main())
