"""R345. A baseplan face the build refuses in Python and in C# alike: a ring that is not simple, two of its edges meeting other than
two neighbours at their shared vertex, such as a bow tie, a fold back or a ring that touches itself, and a label whose rings overlap,
such as an outline drawn twice. yard_rule.face_shapes mends both by buffer(0) and a union, buffer(0) of a bow tie keeping one lobe,
while the face step of C# sums the ring areas, so a face of either kind would differ on the two sides of the parity. FaceRings of
Common holds the same checks, step for step, so both sides refuse the same faces. Plain floats, no shapely, about the first vertex.
"""
import yard_rule


def clean(ring):
    """The vertices of a ring as (x, y) with a vertex equal to the one before it dropped, the closing one among them."""
    found = []
    for vertex in ring:
        point = (float(vertex[0]), float(vertex[1]))
        if not found or point != found[-1]:
            found.append(point)
    if len(found) > 1 and found[0] == found[-1]:
        found.pop()
    return found


def orient(p, q, r):
    return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])


def within(p, q, r):
    """q, on the line of p and r, lies on the segment from p to r."""
    return min(p[0], r[0]) <= q[0] <= max(p[0], r[0]) and min(p[1], r[1]) <= q[1] <= max(p[1], r[1])


def crossing(d1, d2, d3, d4):
    return ((d1 > 0 and d2 < 0) or (d1 < 0 and d2 > 0)) and ((d3 > 0 and d4 < 0) or (d3 < 0 and d4 > 0))


def meet(p1, p2, q1, q2):
    """The closed segments p1 p2 and q1 q2 share a point."""
    d1, d2, d3, d4 = orient(q1, q2, p1), orient(q1, q2, p2), orient(p1, p2, q1), orient(p1, p2, q2)
    if crossing(d1, d2, d3, d4):
        return True
    return (d1 == 0 and within(q1, p1, q2)) or (d2 == 0 and within(q1, p2, q2)) or (d3 == 0 and within(p1, q1, p2)) or (d4 == 0 and within(p1, q2, p2))


def folds(before, at, after):
    """Two neighbour edges that go back along one line from their shared vertex."""
    return orient(before, at, after) == 0 and (before[0] - at[0]) * (after[0] - at[0]) + (before[1] - at[1]) * (after[1] - at[1]) > 0


def relative(ring, origin):
    return [(x - origin[0], y - origin[1]) for x, y in ring]


def simple(ring):
    """A cleaned ring of three vertices or more is simple when no two of its edges meet but neighbours at their shared vertex and no
    two neighbours fold back along one line. Taken about its first vertex, as the area is."""
    points = relative(ring, ring[0])
    count = len(points)
    for one in range(count):
        a, b = points[one], points[(one + 1) % count]
        for other in range(one + 1, count):
            c, d = points[other], points[(other + 1) % count]
            if other == one + 1:
                if folds(a, b, d):
                    return False
            elif one == 0 and other == count - 1:
                if folds(c, a, b):
                    return False
            elif meet(a, b, c, d):
                return False
    return True


def ring_area(ring):
    """The area of a cleaned simple ring by the shoelace about its first vertex, as FaceStep.Ring of C# takes it."""
    points = relative(ring, ring[0])
    twice = 0.0
    for index in range(len(points)):
        a, b = points[index], points[(index + 1) % len(points)]
        twice += a[0] * b[1] - b[0] * a[1]
    return abs(twice) / 2


def crossing_x(p1, p2, q1, q2):
    """The x where two segments cross at a point inside both, None when they do not."""
    d1, d2, d3, d4 = orient(q1, q2, p1), orient(q1, q2, p2), orient(p1, p2, q1), orient(p1, p2, q2)
    if crossing(d1, d2, d3, d4):
        return p1[0] + d1 / (d1 - d2) * (p2[0] - p1[0])
    return None


def union_area(rings):
    """The area the union of cleaned simple rings covers, by vertical slabs about the first vertex of the first ring: between two
    neighbouring x of the vertices and of the crossings of edges of two rings no edge begins, ends or crosses another, so the length
    the union covers across a slab is linear in x and its value at the middle times the width is the area of the slab."""
    origin = rings[0][0]
    edges = []
    for ring in rings:
        points = relative(ring, origin)
        edges.append([(points[index], points[(index + 1) % len(points)]) for index in range(len(points))])
    xs = {a[0] for ring_edges in edges for a, _ in ring_edges}
    for one in range(len(edges)):
        for other in range(one + 1, len(edges)):
            for p1, p2 in edges[one]:
                for q1, q2 in edges[other]:
                    x = crossing_x(p1, p2, q1, q2)
                    if x is not None:
                        xs.add(x)
    xs = sorted(xs)
    area = 0.0
    for left, right in zip(xs, xs[1:]):
        middle = (left + right) / 2
        spans = []
        for ring_edges in edges:
            ys = sorted(a[1] + (b[1] - a[1]) * (middle - a[0]) / (b[0] - a[0]) for a, b in ring_edges if min(a[0], b[0]) < middle < max(a[0], b[0]))
            spans.extend(zip(ys[0::2], ys[1::2]))
        spans.sort()
        covered, end = 0.0, None
        for low, high in spans:
            if end is None or low > end:
                covered += high - low
                end = high
            elif high > end:
                covered += high - end
                end = high
        area += (right - left) * covered
    return area


def refusals(plots, tolerance_m2):
    """Why the build refuses the faces of the plot_shape entries of plots.json, one line per label, [] when it takes every one. The
    entries yard_rule.face_shapes takes: a whole number label and three vertices or more. A ring of under three distinct vertices has
    no area and is left as both sides leave it. A label whose union falls short of the sum of its ring areas by more than tolerance_m2,
    the areas tolerance of addin.parity, has rings that overlap."""
    found, rings = [], {}
    for entry in plots or []:
        if entry.get("kind") != "plot_shape":
            continue
        label = yard_rule.whole_label(entry.get("plot"))
        vertices = entry.get("vertices") or []
        if label is None or len(vertices) < 3:
            continue
        ring = clean(vertices)
        if len(ring) < 3:
            continue
        if not simple(ring):
            found.append("the baseplan face of label %d is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, so the build refuses it (R345)" % label)
            continue
        rings.setdefault(label, []).append(ring)
    for label in sorted(rings):
        if len(rings[label]) < 2:
            continue
        shortfall = sum(ring_area(ring) for ring in rings[label]) - union_area(rings[label])
        if shortfall > tolerance_m2:
            found.append("the baseplan faces of label %d overlap by %.3f m2, shapely joins them by a union and the C# would sum them, so the build refuses them (R345)" % (label, shortfall))
    return found
