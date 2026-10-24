"""How the ramp, the Generic floor, the yard floor and the villa link of each plot relate, measured from prototypes.json.

It measures and counts. It sets no rule, so nothing here says which pattern is right.
"""
import argparse
import csv
import json
import math
import re
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import LineString, MultiPoint, Point, Polygon
from shapely.strtree import STRtree

import excel_reader
import link_summary
import ramp_levels

ROOT = Path(__file__).resolve().parent.parent
CSV_NAME = "plot_relations.csv"
SUMMARY_NAME = "plot_relations_summary.md"
EXCEL_SHEET = "MP_Phase 1_Zone21"
# Two edges are shared when they lie within this distance of each other, in metres
EDGE_TOLERANCE = 0.005
# A level equals another level when it is within this distance, in metres
LEVEL_TOLERANCE = 0.005
# Gaps narrower than twice this are measured, in metres
GAP_CLOSE = 0.05
# The slope from the corners equals the slope in the type name within this many percentage points
SLOPE_TOLERANCE = 0.1
# An edge counts as partly shared when more than this length is shared, and fully shared when all but this length is
MIN_SHARED = 2 * EDGE_TOLERANCE
RING = 4
EPS = 1e-9
AREA_EPS = 1e-6
MAX_LISTED = 40
SLOPE_IN_NAME = re.compile(r"(\d+(?:[.,]\d+)?)\s*%")

COLUMNS = [
    "plot", "unit", "link_id", "link_status", "link_x", "link_y", "link_z", "link_rotation", "link_mirrored", "case",
    "excel_ffl", "excel_ssl", "excel_el_entry",
    "generic_id", "generic_top_min", "generic_top_max", "generic_top_source",
    "generic_min_vs_ffl", "generic_max_vs_ffl", "generic_min_vs_ssl", "generic_max_vs_ssl", "generic_min_vs_entry", "generic_max_vs_entry",
    "generic_top_is_ffl", "generic_top_is_ssl", "generic_top_is_entry",
    "yard_status", "yard_id", "yard_candidates", "yard_candidate_overlaps", "yard_top_min", "yard_top_max", "yard_top_source",
    "yard_min_vs_ffl", "yard_max_vs_ffl", "yard_min_vs_ssl", "yard_max_vs_ssl", "yard_min_vs_entry", "yard_max_vs_entry", "yard_top_is_entry",
    "ramp_id", "ramp_type", "ramp_z1", "ramp_z2", "ramp_z3", "ramp_z4", "ramp_depth", "ramp_long_length",
    "ramp_slope_corners", "ramp_slope_type", "ramp_slope_diff", "ramp_slope_short", "ramp_slope_short_diff",
    "ramp_e0_shared", "ramp_e1_shared", "ramp_e2_shared", "ramp_e3_shared", "ramp_generic_roles", "ramp_yard_roles",
    "generic_ramp_shared_len", "generic_yard_shared_len", "ramp_yard_shared_len", "generic_yard_edge",
    "overlap_generic_ramp", "overlap_generic_yard", "overlap_ramp_yard", "gap_generic_ramp", "gap_generic_yard", "gap_ramp_yard",
    "link_box_vs_generic", "link_box_inside_share", "link_box_holds_origin",
    "generic_level", "generic_offset", "generic_level_elevation",
    "yard_level", "yard_offset", "yard_level_elevation",
    "ramp_level", "ramp_offset", "ramp_level_elevation",
    "flags",
]


def header_key(text):
    return re.sub(r"[\s*]+", " ", str(text)).strip().lower()


def excel_cell(row, label):
    wanted = header_key(label)
    for key, value in row.items():
        if header_key(key) == wanted:
            return value
    return None


def excel_by_plot(rows):
    found = {}
    for row in rows:
        plot = excel_cell(row, "Plot No.")
        if plot is not None:
            found.setdefault(link_summary.plot_key(plot), row)
    return found


def load_excel(path, sheet=EXCEL_SHEET):
    """Rows of the schedule keyed by header. A coords.json of excel_reader.py is read as it is and a workbook is read on the given sheet."""
    path = Path(path)
    if path.suffix.lower() == ".json":
        return json.loads(path.read_text(encoding="utf-8")).get("rows") or []
    return excel_reader.read_sheet(path, sheet)[3]


def shared_offset(document):
    try:
        return ramp_levels.offset_of(document), True
    except (KeyError, TypeError):
        return [0.0, 0.0, 0.0], False


def loop_polygon(points):
    if len(points) < 3:
        return None
    polygon = Polygon([(point[0], point[1]) for point in points])
    if not polygon.is_valid:
        polygon = polygon.buffer(0)
    return None if polygon.is_empty or polygon.area == 0 else polygon


def even_odd(loops):
    """The area the loops enclose when a loop inside another loop is a hole."""
    polygons = [polygon for polygon in (loop_polygon(loop) for loop in loops) if polygon is not None]
    if not polygons:
        return None
    polygons.sort(key=lambda polygon: -polygon.area)
    result = polygons[0]
    for polygon in polygons[1:]:
        result = result.symmetric_difference(polygon)
    return result


def floor_shape(floor, offset):
    """The floor in plan in shared coordinates, from the outline loops and else from the sketch outline."""
    loops = floor.get("outline")
    if loops:
        return even_odd(loops), "outline"
    sketch = link_summary.sketch_points(floor)
    if sketch:
        return even_odd([[(point[0] + offset[0], point[1] + offset[1]) for point in sketch]]), "sketchBoundary"
    return None, None


def top_range(floor, offset):
    top = floor.get("topElevationRange")
    if top and top.get("min") is not None and top.get("max") is not None:
        return top["min"], top["max"], "topFace"
    if floor.get("topElevation") is not None:
        level = floor["topElevation"] + offset[2]
        return level, level, "topElevation"
    return None, None, None


def difference(value, reference):
    return None if value is None or reference is None else round(value - reference, 4)


def equals(low, high, reference):
    """Both ends of the top are the reference within the level tolerance."""
    if low is None or high is None or reference is None:
        return None
    return abs(low - reference) <= LEVEL_TOLERANCE and abs(high - reference) <= LEVEL_TOLERANCE


def ring_of(points):
    cx = sum(point[0] for point in points) / len(points)
    cy = sum(point[1] for point in points) / len(points)
    return sorted(points, key=lambda point: math.atan2(point[1] - cy, point[0] - cx))


def plan_length(a, b):
    return math.hypot(b[0] - a[0], b[1] - a[1])


def type_slope(name):
    """The percent in the type name, for example 0.5 for Ramps 0.5%. A name with no percent has none."""
    found = SLOPE_IN_NAME.search(str(name or ""))
    return float(found.group(1).replace(",", ".")) if found else None


def ramp_geometry(points):
    """Depth, slope and edges of a ramp with four corners given as (x, y, z), or None for any other count.

    The corners go round in plan and the ring starts so that edges 0 and 2 are the two long edges and 1 and 3 the short ones.
    The long pair is the opposite pair with the larger total length. The depth is the area divided by the mean long edge,
    which is the distance between the long edges when they are parallel. The slope is the sum of the rises along both long edges
    over the sum of their lengths, in percent. The slope along the short edges is worked out the same way, it is the slope across the depth.
    """
    if len(points) != RING:
        return None
    corners = ring_of(points)
    lengths = [plan_length(corners[index], corners[(index + 1) % RING]) for index in range(RING)]
    if lengths[1] + lengths[3] > lengths[0] + lengths[2] + EPS:
        corners, lengths = corners[1:] + corners[:1], lengths[1:] + lengths[:1]
    long_total = lengths[0] + lengths[2]
    if long_total <= EPS:
        return None
    edges = [(corners[index], corners[(index + 1) % RING]) for index in range(RING)]
    area = abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in edges)) / 2
    rises = [abs(edges[index][1][2] - edges[index][0][2]) for index in (0, 2)]
    short_rises = [abs(edges[index][1][2] - edges[index][0][2]) for index in (1, 3)]
    short_total = lengths[1] + lengths[3]
    return {
        "corners": corners,
        "edges": edges,
        "lengths": lengths,
        "roles": ["long", "short", "long", "short"],
        "depth": area / (long_total / 2),
        "slope": (rises[0] + rises[1]) / long_total * 100,
        "slope_short": (short_rises[0] + short_rises[1]) / short_total * 100 if short_total > EPS else None,
    }


def shared_length(edge, shape):
    """The length of the edge that lies within the edge tolerance of the outline of the shape."""
    if shape is None:
        return 0.0
    line = LineString([(point[0], point[1]) for point in edge])
    if line.length == 0:
        return 0.0
    return line.intersection(shape.boundary.buffer(EDGE_TOLERANCE)).length


def share_state(length, edge_length):
    if length >= edge_length - MIN_SHARED:
        return "full"
    if length > MIN_SHARED:
        return "partial"
    return "none"


def edge_label(states):
    full = [name for name, state in states.items() if state == "full"]
    partial = ["part " + name for name, state in states.items() if state == "partial"]
    return "+".join(full + partial) or "none"


def boundary_shared_length(a, b):
    if a is None or b is None:
        return None
    return a.boundary.intersection(b.boundary.buffer(EDGE_TOLERANCE)).length


def overlap_area(a, b):
    if a is None or b is None:
        return None
    return clean_area(a.intersection(b).area)


def gap_area(a, b):
    """The area the two floors leave between them where they are closer than twice the closing distance."""
    if a is None or b is None:
        return None
    both = a.union(b)
    closed = both.buffer(GAP_CLOSE, join_style=2).buffer(-GAP_CLOSE, join_style=2)
    return clean_area(closed.area - both.area)


def clean_area(value):
    return 0.0 if value < AREA_EPS else round(value, 4)


def link_origin(link, offset):
    shared = (link.get("transformShared") or {}).get("origin")
    if shared:
        return shared
    origin = (link.get("transform") or {}).get("origin")
    return None if not origin else [origin[axis] + offset[axis] for axis in range(3)]


def link_box_state(link, shape, origin):
    """Where the plan footprint of the link box lies against the Generic floor, as a state and the share of the footprint inside."""
    if link is None:
        return "no link", None, None
    if link.get("loaded") is False:
        return "not loaded", None, None
    corners = link.get("boundingBoxCorners")
    if not corners:
        return "no box", None, None
    if shape is None:
        return "no generic", None, None
    hull = MultiPoint([(point[0], point[1]) for point in corners]).convex_hull
    holds = None if origin is None else hull.buffer(EDGE_TOLERANCE).contains(Point(origin[0], origin[1]))
    inside = hull.intersection(shape).area
    share = inside / hull.area if hull.area > 0 else None
    if shape.buffer(EDGE_TOLERANCE).contains(hull):
        return "inside", share, holds
    if inside > AREA_EPS:
        return "partly inside", share, holds
    return "outside", share, holds


def candidate_yards(yards, tree, targets):
    """The yard floors whose outline touches or overlaps any of the target outlines."""
    found = {}
    for target in targets:
        if target is None:
            continue
        for index in tree.query(target.buffer(EDGE_TOLERANCE)):
            floor, shape = yards[int(index)]
            if shape.distance(target) <= EDGE_TOLERANCE:
                found[floor["elementId"]] = (floor, shape)
    return [found[key] for key in sorted(found)]


def yard_index(floors, shapes):
    """The yard floors that have a plan shape, with a search tree over them, which is None when there is no yard."""
    yards = []
    for floor in floors:
        if link_summary.floor_kind(floor) == "yard" and shapes[floor["elementId"]][0] is not None:
            yards.append((floor, shapes[floor["elementId"]][0]))
    return yards, (STRtree([shape for _, shape in yards]) if yards else None)


def find_yard(yards, tree, generic_shape, ramp_shape):
    """The R58 yard of a plot as a status, assigned, ambiguous or none, and the candidates in element id order."""
    candidates = candidate_yards(yards, tree, [generic_shape, ramp_shape]) if tree is not None else []
    return ("none" if not candidates else "assigned" if len(candidates) == 1 else "ambiguous"), candidates


def plot_of(floor):
    plot = link_summary.floor_plot(floor)
    return None if plot is None else link_summary.plot_key(plot)


def first_by_id(floors):
    return sorted(floors, key=lambda floor: floor.get("elementId") or 0)


def level_columns(row, prefix, floor):
    if floor is None:
        return
    row[prefix + "_level"] = floor.get("level")
    row[prefix + "_offset"] = floor.get("heightOffset")
    row[prefix + "_level_elevation"] = floor.get("levelElevation")


def top_columns(row, prefix, floor, offset, excel):
    low, high, source = top_range(floor, offset)
    row[prefix + "_top_min"], row[prefix + "_top_max"], row[prefix + "_top_source"] = low, high, source
    for name, reference in (("ffl", excel["ffl"]), ("ssl", excel["ssl"]), ("entry", excel["entry"])):
        row[prefix + "_min_vs_" + name] = difference(low, reference)
        row[prefix + "_max_vs_" + name] = difference(high, reference)
    row[prefix + "_top_is_entry"] = equals(low, high, excel["entry"])
    return low, high


def build_rows(data, excel_rows=None):
    """One row per plot and the notes about the input. Plots are those with a villa link or a Generic, yard or ramp floor that names them."""
    document = ramp_levels.host_document(data)
    offset, offset_found = shared_offset(document)
    floors = document["floors"]
    shapes = {}
    for floor in floors:
        shapes[floor["elementId"]] = floor_shape(floor, offset)

    by_plot = {}
    for floor in floors:
        kind = link_summary.floor_kind(floor)
        plot = plot_of(floor)
        if kind not in (None, "yard") and plot is not None:
            by_plot.setdefault(plot, {}).setdefault(kind, []).append(floor)
    yards, tree = yard_index(floors, shapes)

    links = {}
    links_without_plot = 0
    for link in document.get("links") or []:
        if link.get("plotNumber") is None:
            links_without_plot += 1
            continue
        links.setdefault(link_summary.plot_key(link["plotNumber"]), []).append(link)
    excel = excel_by_plot(excel_rows or [])

    plots = sorted(set(by_plot) | set(links), key=link_summary.sort_key)
    plot_set = set(plots)
    rows = [plot_row(plot, by_plot.get(plot, {}), links.get(plot, []), excel.get(plot), shapes, yards, tree, offset) for plot in plots]
    notes = {
        "offsetFound": offset_found,
        "floors": len(floors),
        "yardFloors": len(yards),
        "links": len(document.get("links") or []),
        "linksWithoutPlot": links_without_plot,
        "excelRows": len(excel),
        "excelPlotsWithNoRow": sum(1 for plot in excel if plot not in plot_set),
    }
    return rows, notes


def plot_row(plot, kinds, plot_links, excel_row, shapes, yards, tree, offset):
    row = dict.fromkeys(COLUMNS)
    flags = []
    row["plot"] = plot
    link = plot_links[0] if plot_links else None
    generic = next(iter(first_by_id(kinds.get("generic", []))), None)
    ramp = next(iter(first_by_id(kinds.get("ramp", []))), None)
    for name, found in (("generic", kinds.get("generic", [])), ("ramp", kinds.get("ramp", []))):
        if len(found) > 1:
            flags.append("several_" + name)
    if link is None:
        flags.append("no_link")
    if len(plot_links) > 1:
        flags.append("several_links")

    ffl = ssl = entry = None
    if excel_row is not None:
        ffl = link_summary.parse_number(excel_cell(excel_row, "FFL"))
        ssl = link_summary.parse_number(excel_cell(excel_row, "SSL"))
        entry = link_summary.parse_number(excel_cell(excel_row, "El @ Entry"))
        row["excel_ffl"], row["excel_ssl"], row["excel_el_entry"] = ffl, ssl, entry
    else:
        flags.append("no_excel_row")
    reference = {"ffl": ffl, "ssl": ssl, "entry": entry}

    unit = next((floor.get("unit") for floor in (generic, ramp) if floor is not None and floor.get("unit")), None)
    if link is not None:
        origin = link_origin(link, offset)
        row["link_id"] = link.get("elementId")
        row["link_status"] = link.get("status")
        if origin:
            row["link_x"], row["link_y"], row["link_z"] = origin
        transform = link.get("transform") or {}
        row["link_rotation"] = transform.get("rotationDeg")
        row["link_mirrored"] = transform.get("mirrored")
        row["case"] = link.get("case")
        unit = unit or link.get("prototype")
    else:
        origin = None
    row["unit"] = unit or (excel_cell(excel_row, "Unit") if excel_row is not None else None)

    generic_shape = shapes[generic["elementId"]][0] if generic is not None else None
    ramp_shape = shapes[ramp["elementId"]][0] if ramp is not None else None
    for name, floor in (("generic", generic), ("ramp", ramp)):
        if floor is None:
            flags.append("no_" + name)
        elif shapes[floor["elementId"]][1] == "sketchBoundary":
            flags.append(name + "_outline_from_sketch")
    if generic is not None:
        row["generic_id"] = generic["elementId"]
        low, high = top_columns(row, "generic", generic, offset, reference)
        row["generic_top_is_ffl"] = equals(low, high, ffl)
        row["generic_top_is_ssl"] = equals(low, high, ssl)
        level_columns(row, "generic", generic)

    row["yard_status"], candidates = find_yard(yards, tree, generic_shape, ramp_shape)
    row["yard_candidates"] = " ".join(str(floor["elementId"]) for floor, _ in candidates)
    row["yard_candidate_overlaps"] = " ".join(
        "%s:%g/%g" % (floor["elementId"], overlap_area(generic_shape, shape) or 0, overlap_area(ramp_shape, shape) or 0) for floor, shape in candidates)
    yard = None
    yard_shape = None
    if len(candidates) == 1:
        yard = candidates[0][0]
        yard_shape = candidates[0][1]
        row["yard_id"] = yard["elementId"]
        top_columns(row, "yard", yard, offset, reference)
        level_columns(row, "yard", yard)
    elif candidates:
        flags.append("yard_ambiguous")
        yard_shape = candidates[0][1]
        for _, shape in candidates[1:]:
            yard_shape = yard_shape.union(shape)
    else:
        flags.append("no_yard")

    if ramp is not None:
        ramp_geometry_of(row, ramp, offset, generic_shape, yard_shape, flags)
        level_columns(row, "ramp", ramp)

    row["generic_ramp_shared_len"] = rounded(boundary_shared_length(generic_shape, ramp_shape))
    row["generic_yard_shared_len"] = rounded(boundary_shared_length(generic_shape, yard_shape))
    row["ramp_yard_shared_len"] = rounded(boundary_shared_length(ramp_shape, yard_shape))
    if yard_shape is None or generic_shape is None:
        row["generic_yard_edge"] = "no yard" if yard_shape is None else "no generic"
    else:
        row["generic_yard_edge"] = "shared" if row["generic_yard_shared_len"] > MIN_SHARED else "none"

    row["overlap_generic_ramp"] = overlap_area(generic_shape, ramp_shape)
    row["overlap_generic_yard"] = overlap_area(generic_shape, yard_shape)
    row["overlap_ramp_yard"] = overlap_area(ramp_shape, yard_shape)
    row["gap_generic_ramp"] = gap_area(generic_shape, ramp_shape)
    row["gap_generic_yard"] = gap_area(generic_shape, yard_shape)
    row["gap_ramp_yard"] = gap_area(ramp_shape, yard_shape)

    state, share, holds = link_box_state(link, generic_shape, origin)
    row["link_box_vs_generic"] = state
    row["link_box_inside_share"] = rounded(share)
    row["link_box_holds_origin"] = holds
    row["flags"] = " ".join(flags)
    return row


def rounded(value):
    return None if value is None else round(value, 4)


def ramp_geometry_of(row, ramp, offset, generic_shape, yard_shape, flags):
    row["ramp_id"] = ramp["elementId"]
    row["ramp_type"] = ramp.get("typeName")
    points, _ = ramp_levels.corner_points(ramp, offset)
    geometry = ramp_geometry(points)
    if geometry is None:
        flags.append("ramp_not_four_corners")
        return
    for index, level in enumerate(corner[2] for corner in geometry["corners"]):
        row["ramp_z" + str(index + 1)] = level
    row["ramp_depth"] = rounded(geometry["depth"])
    row["ramp_long_length"] = rounded((geometry["lengths"][0] + geometry["lengths"][2]) / 2)
    row["ramp_slope_corners"] = rounded(geometry["slope"])
    named = type_slope(ramp.get("typeName"))
    row["ramp_slope_type"] = named
    row["ramp_slope_diff"] = None if named is None else rounded(geometry["slope"] - named)
    row["ramp_slope_short"] = rounded(geometry["slope_short"])
    row["ramp_slope_short_diff"] = None if named is None or geometry["slope_short"] is None else rounded(geometry["slope_short"] - named)
    roles = {"generic": [], "yard": []}
    for index, edge in enumerate(geometry["edges"]):
        states = {"generic": share_state(shared_length(edge, generic_shape), geometry["lengths"][index])}
        if yard_shape is not None:
            states["yard"] = share_state(shared_length(edge, yard_shape), geometry["lengths"][index])
        row["ramp_e" + str(index) + "_shared"] = edge_label(states)
        for name, state in states.items():
            if state == "full":
                roles[name].append(geometry["roles"][index])
    row["ramp_generic_roles"] = "+".join(sorted(roles["generic"])) or "none"
    row["ramp_yard_roles"] = ("+".join(sorted(roles["yard"])) or "none") if yard_shape is not None else "no yard"


def pattern_of(row):
    """The few measured facts a plot is grouped by. A fact the plot cannot give is n/a."""
    def yes_no(value):
        return "n/a" if value is None else "yes" if value else "no"

    ramp_generic = row["ramp_generic_roles"]
    ramp_yard = row["ramp_yard_roles"]
    return (
        ("generic top equals El @ Entry", yes_no(row["generic_top_is_entry"])),
        ("ramp shares an edge with Generic", "n/a" if ramp_generic is None else "no" if ramp_generic == "none" else "yes"),
        ("ramp shares an edge with yard", "n/a" if ramp_yard in (None, "no yard") else "no" if ramp_yard == "none" else "yes"),
        ("Generic shares an edge with yard", "n/a" if row["generic_yard_edge"] in (None, "no yard", "no generic") else "yes" if row["generic_yard_edge"] == "shared" else "no"),
        ("yard", row["yard_status"]),
        ("link box against Generic", row["link_box_vs_generic"]),
    )


def summarize(rows, notes):
    patterns = [pattern_of(row) for row in rows]
    per_fact = {}
    for pattern in patterns:
        for fact, value in pattern:
            per_fact.setdefault(fact, Counter())[value] += 1
    common, common_count = (Counter(patterns).most_common(1)[0] if patterns else ((), 0))
    breaking = []
    for row, pattern in zip(rows, patterns):
        if pattern != common:
            differing = ["%s: %s" % (fact, value) for (fact, value), (_, usual) in zip(pattern, common) if value != usual]
            breaking.append((row["plot"], differing))

    def count(test):
        return sum(1 for row in rows if test(row))

    slope_rows = [row for row in rows if row["ramp_slope_diff"] is not None]
    short_rows = [row for row in rows if row["ramp_slope_short_diff"] is not None]
    summary = {
        "plots": len(rows),
        "notes": notes,
        "perFact": per_fact,
        "common": common,
        "commonCount": common_count,
        "breaking": breaking,
        "genericTopEquals": {name: (count(lambda row: row["generic_top_is_" + name] is True), count(lambda row: row["generic_top_is_" + name] is not None)) for name in ("ffl", "ssl", "entry")},
        "yardTopEqualsEntry": (count(lambda row: row["yard_top_is_entry"] is True), count(lambda row: row["yard_top_is_entry"] is not None)),
        "rampGenericRoles": Counter(row["ramp_generic_roles"] for row in rows if row["ramp_generic_roles"] is not None),
        "rampYardRoles": Counter(row["ramp_yard_roles"] for row in rows if row["ramp_yard_roles"] is not None),
        "slopeMatches": (sum(1 for row in slope_rows if abs(row["ramp_slope_diff"]) <= SLOPE_TOLERANCE), len(slope_rows)),
        "slopeShortMatches": (sum(1 for row in short_rows if abs(row["ramp_slope_short_diff"]) <= SLOPE_TOLERANCE), len(short_rows)),
        "yardCandidates": Counter(len((row["yard_candidates"] or "").split()) for row in rows),
        "rampDepths": Counter(round(row["ramp_depth"], 2) for row in rows if row["ramp_depth"] is not None),
        "overlaps": {name: (count(lambda row: (row["overlap_" + name] or 0) > 0), max([row["overlap_" + name] or 0 for row in rows] or [0])) for name in ("generic_ramp", "generic_yard", "ramp_yard")},
        "gaps": {name: (count(lambda row: (row["gap_" + name] or 0) > 0), max([row["gap_" + name] or 0 for row in rows] or [0])) for name in ("generic_ramp", "generic_yard", "ramp_yard")},
        "linkBoxHoldsOrigin": (count(lambda row: row["link_box_holds_origin"] is True), count(lambda row: row["link_box_holds_origin"] is not None)),
        "levels": {kind: Counter((row[kind + "_level"], row[kind + "_offset"]) for row in rows if row[kind + "_level"] is not None or row[kind + "_offset"] is not None) for kind in ("generic", "yard", "ramp")},
        "flags": Counter(flag for row in rows for flag in (row["flags"] or "").split()),
    }
    return summary


def listed(items):
    shown = ", ".join(str(item) for item in items[:MAX_LISTED])
    return shown + (" and " + str(len(items) - MAX_LISTED) + " more" if len(items) > MAX_LISTED else "")


def render_summary(summary, names):
    lines = ["# Plot relations", ""]
    lines.append("Measured from " + names["prototypes"] + (" and " + names["excel"] if names.get("excel") else " with no schedule") + ". Counts only, no rule is set by this report.")
    notes = summary["notes"]
    lines += ["", "## Input", ""]
    lines.append("- %d plots, %d floors (%d yard floors), %d links (%d with no plot number), %d schedule rows" % (summary["plots"], notes["floors"], notes["yardFloors"], notes["links"], notes["linksWithoutPlot"], notes["excelRows"]))
    lines.append("- Survey point offset " + ("read from the file" if notes["offsetFound"] else "not in the file, so 0 was used"))
    lines.append("- Schedule rows with no plot in the model: %d" % notes["excelPlotsWithNoRow"])
    lines.append("- Tolerances: shared edge %g m, equal level %g m, gaps narrower than %g m, slope match %g percentage points" % (EDGE_TOLERANCE, LEVEL_TOLERANCE, 2 * GAP_CLOSE, SLOPE_TOLERANCE))
    lines += ["", "## Counts per pattern", ""]
    lines.append("- yard floors that touch or overlap the Generic floor or the ramp, per plot: " + ", ".join("%d x%d" % (key, value) for key, value in sorted(summary["yardCandidates"].items())))
    for fact, counter in summary["perFact"].items():
        lines.append("- " + fact + ": " + ", ".join("%s %d" % (value, counter[value]) for value in sorted(counter, key=str)))
    lines += ["", "## Levels", ""]
    for name, label in (("entry", "El @ Entry"), ("ffl", "FFL"), ("ssl", "SSL")):
        hit, known = summary["genericTopEquals"][name]
        lines.append("- Generic top equals %s within %g m: %d of %d plots with both values" % (label, LEVEL_TOLERANCE, hit, known))
    hit, known = summary["yardTopEqualsEntry"]
    lines.append("- Yard top equals El @ Entry within %g m: %d of %d plots with both values" % (LEVEL_TOLERANCE, hit, known))
    lines += ["", "## Ramp", ""]
    lines.append("- Ramp edges shared with Generic, by the roles of the shared edges: " + ", ".join("%s %d" % (key, value) for key, value in sorted(summary["rampGenericRoles"].items())))
    lines.append("- Ramp edges shared with yard: " + ", ".join("%s %d" % (key, value) for key, value in sorted(summary["rampYardRoles"].items())))
    hit, known = summary["slopeMatches"]
    lines.append("- Slope along the long edges equals the slope in the type name within %g points: %d of %d ramps with a percent in the name" % (SLOPE_TOLERANCE, hit, known))
    hit, known = summary["slopeShortMatches"]
    lines.append("- Slope along the short edges, across the depth, equals the slope in the type name within %g points: %d of %d ramps with a percent in the name" % (SLOPE_TOLERANCE, hit, known))
    lines.append("- Ramp depth in metres, most common: " + ", ".join("%g x%d" % (depth, value) for depth, value in summary["rampDepths"].most_common(5)))
    lines += ["", "## Gaps and overlaps", ""]
    for name in ("generic_ramp", "generic_yard", "ramp_yard"):
        over, biggest_over = summary["overlaps"][name]
        gap, biggest_gap = summary["gaps"][name]
        lines.append("- %s: overlap on %d plots (largest %g m2), gap on %d plots (largest %g m2)" % (name.replace("_", " and "), over, biggest_over, gap, biggest_gap))
    hit, known = summary["linkBoxHoldsOrigin"]
    lines += ["", "## Link box", "", "- The footprint of the link box holds the link origin on %d of %d loaded links with a box. A low number means the box is not in the space this report assumes." % (hit, known)]
    lines += ["", "## Floor levels used", ""]
    for kind, counter in summary["levels"].items():
        common_levels = sorted(counter.items(), key=lambda item: (-item[1], str(item[0])))[:6]
        lines.append("- " + kind + ": " + (", ".join("%s offset %s x%d" % (level, offset, value) for (level, offset), value in common_levels) or "none"))
    if summary["flags"]:
        lines += ["", "## Flags", ""]
        lines += ["- %s: %d" % (flag, value) for flag, value in sorted(summary["flags"].items())]
    lines += ["", "## Common pattern", ""]
    if summary["common"]:
        lines.append("Most plots (%d of %d) show:" % (summary["commonCount"], summary["plots"]))
        lines += ["- %s: %s" % (fact, value) for fact, value in summary["common"]]
    lines += ["", "## Plots that break the common pattern", ""]
    if not summary["breaking"]:
        lines.append("None.")
    for plot, differing in summary["breaking"][:MAX_LISTED * 5]:
        lines.append("- plot %s: %s" % (plot, ", ".join(differing)))
    if len(summary["breaking"]) > MAX_LISTED * 5:
        lines.append("- and %d more, they are in the CSV" % (len(summary["breaking"]) - MAX_LISTED * 5))
    return "\n".join(lines) + "\n"


def cell(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "yes" if value else "no"
    if isinstance(value, float):
        return repr(round(value, 4)) if abs(value) < 1e15 else str(value)
    return str(value)


def write_outputs(rows, summary, prototypes_path, excel_path):
    out_dir = Path(prototypes_path).resolve().parent
    refuse_inside_repo(out_dir)
    with open(out_dir / CSV_NAME, "w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(COLUMNS)
        for row in rows:
            writer.writerow([cell(row[column]) for column in COLUMNS])
    names = {"prototypes": Path(prototypes_path).name, "excel": Path(excel_path).name if excel_path else None}
    (out_dir / SUMMARY_NAME).write_text(render_summary(summary, names), encoding="utf-8")
    return out_dir / CSV_NAME, out_dir / SUMMARY_NAME


def refuse_inside_repo(folder):
    folder = Path(folder).resolve()
    if folder == ROOT or ROOT in folder.parents:
        raise ValueError("the report is written next to the input file and never inside the repo, but " + str(folder) + " is inside it")


def main(argv=None):
    parser = argparse.ArgumentParser(description="Measure how the ramp, Generic floor, yard floor and villa link of each plot relate.")
    parser.add_argument("prototypes", help="prototypes.json from the Revit reader. The report files are written next to it")
    parser.add_argument("--excel", help="the schedule workbook, or the coords.json of excel_reader.py")
    parser.add_argument("--sheet", default=EXCEL_SHEET, help="sheet of the workbook, default " + EXCEL_SHEET)
    args = parser.parse_args(argv)
    refuse_inside_repo(Path(args.prototypes).resolve().parent)
    data = json.loads(Path(args.prototypes).read_text(encoding="utf-8"))
    excel_rows = load_excel(args.excel, args.sheet) if args.excel else []
    rows, notes = build_rows(data, excel_rows)
    summary = summarize(rows, notes)
    csv_path, summary_path = write_outputs(rows, summary, args.prototypes, args.excel)
    print("%d plots" % len(rows))
    print(csv_path)
    print(summary_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
