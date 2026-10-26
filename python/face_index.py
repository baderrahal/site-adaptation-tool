"""R351, R352. The baseplan faces of a site point by one rule in Python and C# (FaceIndex and LocationPlan.Holds of Common), and a grid by
bounding box that finds them.

The rule: a site point is on a face when it lies inside one of the rings of the face by the crossings of a ray to the east, or on the
outline of two of its rings or more, such as on the edge two rings of one label share, which the union of its rings holds within it. A
point on the outline of one ring alone is on no face. The face of a point is the one face that holds it, None when no face or two do.
Both languages take the same steps on plain doubles, so they give the same face, where Python took the union of shapely before and C#
the rings one by one, and a label on the edge two rings of its face share went on that face in Python and on none in C#.

The grid: the faces by the cells of their bounding boxes, the cell the median of the larger side of the boxes and at least MIN_CELL_M,
so a point is held against the faces whose box holds it, never against every face. A face whose box covers more than
MAX_CELLS_PER_FACE cells, such as a ring with a far vertex, is kept apart in wide and held against every point, so the grid holds at
most MAX_CELLS_PER_FACE cells per face whatever the faces are. The grid only leaves out faces whose box does not hold the point, so it
gives what a scan of every face gives.
"""
import math

import yard_rule

# The least side of a cell, so faces of no extent never give a cell of none. The cell only sets how many faces a point is held against
MIN_CELL_M = 1.0
# The most cells the box of a face is put in, a face over it scanned for every point instead. It bounds the memory of the grid and never
# its result
MAX_CELLS_PER_FACE = 256


def on_segment(ax, ay, bx, by, x, y):
    """The point lies on the segment from a to b, the cross product exactly 0 and the point within the box of the segment."""
    cross = (bx - ax) * (y - ay) - (by - ay) * (x - ax)
    return cross == 0 and min(ax, bx) <= x <= max(ax, bx) and min(ay, by) <= y <= max(ay, by)


def inside(ring, x, y):
    """A point strictly inside a ring, by the crossings of a ray to the east, False on its outline, as LocationPlan.Inside."""
    found = False
    before = len(ring) - 1
    for index in range(len(ring)):
        xi, yi = ring[index][0], ring[index][1]
        xj, yj = ring[before][0], ring[before][1]
        if on_segment(xi, yi, xj, yj, x, y):
            return False
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            found = not found
        before = index
    return found


def on_outline(ring, x, y):
    """The point lies on an edge of the ring."""
    return any(on_segment(ring[index][0], ring[index][1], ring[index - 1][0], ring[index - 1][1], x, y) for index in range(len(ring)))


def holds(rings, x, y):
    """R351. The face of these rings holds the point: inside one ring, or on the outline of two rings or more."""
    x, y = float(x), float(y)
    if any(inside(ring, x, y) for ring in rings):
        return True
    return sum(1 for ring in rings if on_outline(ring, x, y)) >= 2


def ring_area(ring):
    """The area of a ring by the shoelace about its first vertex, as FaceStep.Ring of C# takes it."""
    origin_x, origin_y = ring[0][0], ring[0][1]
    twice = 0.0
    for index in range(len(ring)):
        ax, ay = ring[index][0] - origin_x, ring[index][1] - origin_y
        bx, by = ring[(index + 1) % len(ring)][0] - origin_x, ring[(index + 1) % len(ring)][1] - origin_y
        twice += ax * by - bx * ay
    return abs(twice) / 2


def rings_of(plots):
    """The rings of every face of the plot_shape entries of a baseplan read by label, as LocationPlan.Faces of C# takes them: a whole
    number label, three vertices or more and an area."""
    found = {}
    for entry in plots or []:
        if entry.get("kind") != "plot_shape":
            continue
        label = yard_rule.whole_label(entry.get("plot"))
        ring = [(float(vertex[0]), float(vertex[1])) for vertex in entry.get("vertices") or []]
        if label is None or len(ring) < 3 or ring_area(ring) == 0:
            continue
        found.setdefault(str(label), []).append(ring)
    return found


class FaceIndex:
    """The faces of a point by the rule of holds, the faces found by a grid of the cells of their bounding boxes."""

    def __init__(self, rings):
        self.labels = sorted(rings, key=int)
        self.rings = [rings[label] for label in self.labels]
        self.boxes = [(min(p[0] for r in found for p in r), min(p[1] for r in found for p in r), max(p[0] for r in found for p in r),
                       max(p[1] for r in found for p in r)) for found in self.rings]
        self.cells = {}
        self.wide = []
        self.wide_items = []
        if not self.boxes:
            self.cell_count = 0
            return
        self.origin = (min(box[0] for box in self.boxes), min(box[1] for box in self.boxes))
        sides = sorted(max(box[2] - box[0], box[3] - box[1]) for box in self.boxes)
        middle = len(sides) // 2
        median = sides[middle] if len(sides) % 2 else (sides[middle - 1] + sides[middle]) / 2
        self.cell = max(median, MIN_CELL_M)
        for item, box in enumerate(self.boxes):
            columns, rows = self.column(box[2]) - self.column(box[0]) + 1, self.row(box[3]) - self.row(box[1]) + 1
            if float(columns) * float(rows) > MAX_CELLS_PER_FACE:
                self.wide_items.append(item)
                self.wide.append(self.labels[item])
                continue
            for column in range(self.column(box[0]), self.column(box[2]) + 1):
                for row in range(self.row(box[1]), self.row(box[3]) + 1):
                    self.cells.setdefault((column, row), []).append(item)
        self.cell_count = len(self.cells)

    def column(self, x):
        return math.floor((x - self.origin[0]) / self.cell)

    def row(self, y):
        return math.floor((y - self.origin[1]) / self.cell)

    def holding(self, x, y):
        """The labels of every face that holds the point, in the order of their numbers."""
        if not self.boxes:
            return []
        x, y = float(x), float(y)
        found = []
        for item in sorted(self.cells.get((self.column(x), self.row(y)), []) + self.wide_items):
            box = self.boxes[item]
            if box[0] <= x <= box[2] and box[1] <= y <= box[3] and holds(self.rings[item], x, y):
                found.append(self.labels[item])
        return found

    def face_of(self, x, y):
        """The one face that holds the point, None when no face or two hold it."""
        found = self.holding(x, y)
        return found[0] if len(found) == 1 else None


class PlanFaces(dict):
    """The faces of a baseplan read by label, each the union of its rings by shapely as yard_rule.face_shapes gives it for its centroid,
    area and outline, with the rings of each label and their FaceIndex for the faces of a point."""

    def __init__(self, faces, rings):
        super().__init__(faces)
        self.rings = rings
        self.index = FaceIndex(rings)


def plan_faces(plots):
    """R351. The faces of the plots of a baseplan read with the rings of each label and their index."""
    return PlanFaces(yard_rule.face_shapes(plots, 0)[0], rings_of(plots))


def index_of(faces):
    """The FaceIndex of faces: the one of PlanFaces, else one of the outer rings of shapely faces such as the made up faces of a test."""
    index = getattr(faces, "index", None)
    if index is not None:
        return index
    rings = {}
    for label, shape in faces.items():
        parts = getattr(shape, "geoms", [shape])
        rings[label] = [[(float(x), float(y)) for x, y in list(part.exterior.coords)[:-1]] for part in parts]
    return FaceIndex(rings)
