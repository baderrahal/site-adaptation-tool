"""El @ Entry of a plot, measured on the past job and coded as one rule (R93, R94, R95).

El @ Entry is the level at the top of the ramp in front of the entrance. The rule is measured from past work:
the entry point of a ramp is the midpoint of its higher long edge. A plot with no ramp gets the road level at the
curb point nearest to its front edge plus a measured offset. Lengths and levels are in metres.
"""
import math
import statistics

from shapely.geometry import Point

import plot_relations

# The tolerances the entry level is judged by, in metres. A level within one of them is counted in its band
BANDS = (0.005, 0.010, 0.030)
BAND_LABELS = ("within5mm", "within10mm", "within30mm")
# Slack so a difference of exactly 5 mm is inside the band despite float noise
EPSILON = 1e-9
MIN_EDGE = 1.0
# Two edges whose midpoints are this near the road are both candidates for the front, a corner plot
FRONT_TIE = 1.0
FLAT_RISE = 1e-6
MEASURED_NOTE = "measured from past work"


def spread(values):
    """Count, median, 5th and 95th percentile and the largest absolute value of a list of differences, and how many lie in each band.

    The bands are counted on the differences as they are and again after the median is taken off, which tells a
    fixed offset from a scatter.
    """
    if not values:
        return {"n": 0}
    ordered = sorted(values)
    count = len(ordered)
    median = statistics.median(ordered)
    result = {"n": count, "median": round(median, 4), "p5": round(ordered[int(0.05 * count)], 4), "p95": round(ordered[min(int(0.95 * count), count - 1)], 4), "maxAbs": round(max(abs(value) for value in ordered), 4)}
    for label, limit in zip(BAND_LABELS, BANDS):
        result[label] = sum(1 for value in ordered if abs(value) <= limit + EPSILON)
        result[label + "AfterMedian"] = sum(1 for value in ordered if abs(value - median) <= limit + EPSILON)
    return result


def band_counts(errors):
    """Plots by size of an error: within 5 mm, 10 mm, 30 mm and worse. The bands are cumulative, worse is the rest."""
    counts = {label: sum(1 for value in errors if abs(value) <= limit + EPSILON) for label, limit in zip(BAND_LABELS, BANDS)}
    counts["worse"] = sum(1 for value in errors if abs(value) > BANDS[-1] + EPSILON)
    return counts


def midpoint(first, second):
    return ((first[0] + second[0]) / 2, (first[1] + second[1]) / 2, (first[2] + second[2]) / 2)


def high_long_edge(corners):
    """R94. The higher long edge of a ramp given as four (x, y, z) corners: its two corners, its midpoint and the other long edge.

    The long edges are the pair of R59. The higher one has the larger mean level. A ramp whose long edges are level
    with each other has no higher edge, so the first long edge is taken and flat says so. None for anything but four corners.
    """
    geometry = plot_relations.ramp_geometry(corners)
    if geometry is None:
        return None
    ring = geometry["corners"]
    first, second = (ring[0], ring[1]), (ring[3], ring[2])
    rise = (first[0][2] + first[1][2]) / 2 - (second[0][2] + second[1][2]) / 2
    high, low = (first, second) if rise >= 0 else (second, first)
    return {"high": high, "low": low, "mid": midpoint(*high), "rise": abs(rise), "flat": abs(rise) <= FLAT_RISE, "depth": geometry["depth"], "ring": ring}


def ramp_entry(corners):
    """R94. The entry point (x, y) and the entry level of a ramp, the midpoint of its higher long edge. None without four corners."""
    edge = high_long_edge(corners)
    if edge is None:
        return None
    return {"point": (edge["mid"][0], edge["mid"][1]), "level": edge["mid"][2], "flat": edge["flat"]}


def ramp_candidates(corners, origin=None):
    """R93. Every candidate point of a ramp for the entry level as name to (x, y, z).

    The corner levels, the edge midpoints, the higher long edge at the middle and at its ends. With the link origin
    also the corner and edge midpoint nearest and farthest from it. The levels are those of the ramp itself.
    """
    edge = high_long_edge(corners)
    if edge is None:
        return {}
    ring = edge["ring"]
    around = [(ring[index], ring[(index + 1) % 4]) for index in range(4)]
    mids = [midpoint(*pair) for pair in around]
    found = {
        "corner highest": max(ring, key=lambda point: point[2]),
        "corner lowest": min(ring, key=lambda point: point[2]),
        "corner mean": tuple(sum(point[axis] for point in ring) / 4 for axis in range(3)),
        "edge midpoint highest of four": max(mids, key=lambda point: point[2]),
        "edge midpoint lowest of four": min(mids, key=lambda point: point[2]),
        "higher long edge, midpoint": edge["mid"],
        "higher long edge, first end": edge["high"][0],
        "higher long edge, second end": edge["high"][1],
        "higher long edge, lower end": min(edge["high"], key=lambda point: point[2]),
        "higher long edge, upper end": max(edge["high"], key=lambda point: point[2]),
        "lower long edge, midpoint": midpoint(*edge["low"]),
    }
    if origin is not None:
        near = lambda point: math.hypot(point[0] - origin[0], point[1] - origin[1])
        found["corner nearest the link"] = min(ring, key=near)
        found["corner farthest from the link"] = max(ring, key=near)
        found["edge midpoint nearest the link"] = min(mids, key=near)
        found["edge midpoint farthest from the link"] = max(mids, key=near)
    return found


def ring_edges(polygon):
    """The edges of the outer ring of a plot face as ((x, y), (x, y)), the first point is not repeated."""
    ring = list(polygon.exterior.coords)[:-1]
    return [(ring[index], ring[(index + 1) % len(ring)]) for index in range(len(ring))]


def front_edge(polygon, road_tree, roads):
    """R94. The edge of a plot face whose midpoint is nearest to a road edge line, with its curb point.

    Edges shorter than MIN_EDGE are left out, a chamfer is no front. tied is true when another edge is within
    FRONT_TIE of the nearest one, which is a corner plot. The curb point is the point of the nearest road edge line
    that is nearest to the midpoint of the front edge. None when the face has no edge or there is no road line.
    """
    if roads is None or len(roads) == 0 or polygon.geom_type != "Polygon":
        return None
    candidates = []
    for first, second in ring_edges(polygon):
        length = math.hypot(second[0] - first[0], second[1] - first[1])
        if length < MIN_EDGE:
            continue
        centre = Point((first[0] + second[0]) / 2, (first[1] + second[1]) / 2)
        nearest, distance = road_tree.query_nearest(centre, return_distance=True)
        candidates.append((float(distance[0]), first, second, centre, int(nearest[0])))
    if not candidates:
        return None
    candidates.sort(key=lambda item: item[0])
    distance, first, second, centre, line = candidates[0]
    curb = roads[line].interpolate(roads[line].project(centre))
    tied = len(candidates) > 1 and candidates[1][0] - distance <= FRONT_TIE
    return {"edge": (first, second), "mid": (centre.x, centre.y), "curb": (curb.x, curb.y), "distance": distance, "tied": tied}


def road_entry(lookup, front, offset):
    """R94. The entry level of a plot with no ramp: the road level at the curb point of its front edge plus the measured offset.

    Returns the level, the surface that answered and the curb point, or None when no surface covers the curb point. A level is never filled in.
    """
    if front is None:
        return None
    z, name = lookup.level_at(front["curb"][0], front["curb"][1])
    if z is None:
        return None
    return {"level": z + offset, "road": z, "surface": name, "point": front["curb"]}


def entry_offset(differences):
    """R94. The median of entry level minus road level, which is the offset of the rule. None without a difference."""
    return statistics.median(differences) if differences else None
