"""R336. Second method of Round ADDIN 3a: the rules measured on the answer keys of majority.check.group read again by another route.

json, csv, openpyxl for the sales sheet and plain math only, no shapely and none of the functions that made the rules. For each
seed of check.seeds, check.sample plots are drawn in plot order from every plot of the group in past_all.csv with a link, never
from the misses of the first method, and every such plot is held as well. Each plot is held again and its verdict, holds or
misses, compared with the verdict of the first method, which is a miss when the override list of the run names the key and plot
under that decision, else holds.

Every decision of the group in majority.json is held by the candidate the first method coded, whichever it is: a factor by its
own value of that factor looked up in the table the first method learned, a formula by its own sums. The fits are made again
here, never taken from majority.json: the link origin per code (R121), the yard outlines per unit, mirror flag and width (R135)
and the clusters from the footprint blocks and the CAD analysis (R236). A decision whose coded candidate has no own route, or a
decision with no route at all, is a failure, listed in cannotCheck and the run ends with 1, unless its field is one of
majority.overrideKeep.laterFields, left to a later phase, listed in leftForLater.

- coordinates (R331): each key read by json, its points, angle, project position and levels held against the reference key
- link rotation and the corner front (R332): the front edge of the face by its own walk, frontRule by the Orientation of a corner
  plot read here, nearestCurb the edge nearest a curb, the rotation with the mirror flag of the key within rotationHoldDeg
- link origin (R332): the origin the own fit per code, or the plotFrame of config/prototype_offsets.json, gives on that edge
- ramp type, outline, front and fall (R273, R275, R334), generic floor on the whole face by the exact area the two outlines share,
  yard depth and outline (R334) against the own refit or config/floor_outlines.json
- hands (R333): side neighbours by the edges two faces share, the open end, the row, the road side, the clusters made here and
  the row pairs, looked up in the table the first method learned for the file of the link
- parameters (R335) by their formula or table, a level parameter within levelHoldMm, the walls (R335) and the open front
- the merged override list (R330), every entry on a plot past_all.csv gives its key, and the run entries of the fields it does not
  keep equal to the dry list
- R341, the levels of R339 with --landxml: the road surfaces read again from the LandXML text, the road level at the own front corners
  by an own grid of the triangles and the edge within snapM, El @ Entry of the rule, the El @ Entry a build holds, the split of the own
  clusters in own row order by the table the first method learned, the own row pairs, one FFL per piece to the mm, the base of the
  plot that sets it, the steps and the Prototype-FFL parameter, each held within majority.levelPassMm
"""
import argparse
import csv
import json
import math
import os
import random
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parent.parent
POINT_FIELDS = (("surveyPoint", "position"), ("surveyPoint", "sharedPosition"), ("basePoint", "position"), ("basePoint", "sharedPosition"))
OWN_FIELDS = {name + "." + field for name, field in POINT_FIELDS} | {"angleToTrueNorthDeg", "projectPosition"}
QUADRANTS = {"EN": (0.0, 90.0), "SE": (90.0, 180.0), "WS": (180.0, 270.0), "NW": (270.0, 360.0)}
# R121: the anchors a link origin is fitted from, a tie going to the first, and the bands the fit counts
ANCHORS = ("start", "end")
FIT_BANDS = (0.001, 0.010)
# R135: a group of fewer plots is fitted on all of them and not split
MIN_PLOTS = 4
ROAD = "roadLevel"
# R341. A LandXML surface read again by a plain text scan, another route than the expat reader of R40: the points, northing easting
# elevation, the faces and the faces marked hidden, and the size of the cells of the own grid of the triangles
POINT = re.compile(rb'<P\s+id="(\d+)"[^>]*>([^<]*)</P>')
FACE = re.compile(rb'<F(\s[^>]*)?>([^<]*)</F>')
HIDDEN = re.compile(rb'\bi="1"')
CELL_M = 5.0
# R341. The level decisions of R339 the own route holds
LEVEL_WORDS = ("entry", "rampFront", "levelSplit", "baseHeight", "ffl", "steps")


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def ring_area(ring):
    """The signed area, the sums taken from the first vertex, since products of world coordinates lose the decimals."""
    x0, y0 = ring[0][0], ring[0][1]
    local = [(point[0] - x0, point[1] - y0) for point in ring]
    return sum(local[index][0] * local[(index + 1) % len(local)][1] - local[(index + 1) % len(local)][0] * local[index][1] for index in range(len(local))) / 2.0


def shoelace_centre(ring):
    """The centre of area, the sums taken from the first vertex as ring_area takes them."""
    x0, y0 = ring[0][0], ring[0][1]
    area, cx, cy = 0.0, 0.0, 0.0
    for index in range(len(ring)):
        x1, y1 = ring[index][0] - x0, ring[index][1] - y0
        x2, y2 = ring[(index + 1) % len(ring)][0] - x0, ring[(index + 1) % len(ring)][1] - y0
        cross = x1 * y2 - x2 * y1
        area += cross
        cx += (x1 + x2) * cross
        cy += (y1 + y2) * cross
    area /= 2.0
    return (x0 + cx / (6.0 * area), y0 + cy / (6.0 * area)) if area else (sum(x for x, _ in ring) / len(ring), sum(y for _, y in ring) / len(ring))


def inside(point, ring):
    """Even odd ray test."""
    x, y = point
    hit = False
    for index, (x1, y1) in enumerate(ring):
        x2, y2 = ring[(index + 1) % len(ring)]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


def counter_clockwise(ring):
    points = [(point[0], point[1]) for point in ring]
    if len(points) > 1 and points[0] == points[-1]:
        points = points[:-1]
    return points if ring_area(points) >= 0 else points[::-1]


def triangles(ring):
    """The ring cut into triangles by its ears, counter clockwise, for the exact overlap of R336."""
    points = counter_clockwise(ring)
    found = []
    while len(points) > 3:
        cut = None
        for index in range(len(points)):
            a, b, c = points[index - 1], points[index], points[(index + 1) % len(points)]
            cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            if cross <= 1e-12:
                continue
            if any(point_in_triangle(other, a, b, c) for other in points if other not in (a, b, c)):
                continue
            cut = index
            found.append((a, b, c))
            break
        if cut is None:
            # Left with collinear points only, which hold no area
            return found
        del points[cut]
    if len(points) == 3 and ring_area(points) > 0:
        found.append(tuple(points))
    return found


def point_in_triangle(point, a, b, c):
    def side(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    return side(a, b, point) >= 0 and side(b, c, point) >= 0 and side(c, a, point) >= 0


def clip(subject, triangle):
    """The subject ring clipped by one counter clockwise triangle, edge by edge (Sutherland and Hodgman)."""
    output = list(subject)
    for index in range(3):
        a, b = triangle[index], triangle[(index + 1) % 3]
        source, output = output, []
        if not source:
            break
        for position, current in enumerate(source):
            before = source[position - 1]
            inside_now = (b[0] - a[0]) * (current[1] - a[1]) - (b[1] - a[1]) * (current[0] - a[0]) >= 0
            inside_before = (b[0] - a[0]) * (before[1] - a[1]) - (b[1] - a[1]) * (before[0] - a[0]) >= 0
            if inside_now != inside_before:
                dx, dy = current[0] - before[0], current[1] - before[1]
                denominator = (b[0] - a[0]) * dy - (b[1] - a[1]) * dx
                t = ((b[0] - a[0]) * (a[1] - before[1]) - (b[1] - a[1]) * (a[0] - before[0])) / denominator if denominator else 0.0
                output.append((before[0] + t * dx, before[1] + t * dy))
            if inside_now:
                output.append(current)
    return output


def overlap_area(subject, ring):
    """The area two simple rings share, exact: the subject clipped by each triangle of the other."""
    shape = counter_clockwise(subject)
    return sum(ring_area(found) for found in (clip(shape, triangle) for triangle in triangles(ring)) if len(found) >= 3)


def wkt_rings(text):
    """The rings of a POLYGON or MULTIPOLYGON well known text, each part as its outer ring and its holes."""
    parts, depth, current, ring = [], 0, [], []
    for token in re.findall(r"\(|\)|[^(),]+|,", text[text.index("("):]):
        if token == "(":
            depth += 1
            ring = []
        elif token == ")":
            if ring:
                current.append(ring)
                ring = []
            depth -= 1
            if depth == (0 if text.lstrip().upper().startswith("POLYGON") else 1) and current:
                parts.append(current)
                current = []
        elif token != ",":
            values = token.split()
            if len(values) >= 2:
                ring.append((float(values[0]), float(values[1])))
    return parts


def whole_face_share(outline, face):
    """The area a generic outline and its face share over the area of their union, exact by clipping."""
    parts = wkt_rings(outline)
    area = sum(abs(ring_area(part[0])) - sum(abs(ring_area(hole)) for hole in part[1:]) for part in parts)
    shared = sum(overlap_area(part[0], face) - sum(overlap_area(hole, face) for hole in part[1:]) for part in parts)
    union = area + abs(ring_area(face)) - shared
    return shared / union if union else None


def segment_distance(point, first, second):
    dx, dy = second[0] - first[0], second[1] - first[1]
    length = dx * dx + dy * dy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((point[0] - first[0]) * dx + (point[1] - first[1]) * dy) / length))
    return math.hypot(point[0] - first[0] - t * dx, point[1] - first[1] - t * dy)


def curb_distance(point, curbs):
    return min(segment_distance(point, first, second) for first, second in curbs)


def angle_of(vector):
    return math.degrees(math.atan2(vector[1], vector[0])) % 360.0


def bearing(vector):
    return math.degrees(math.atan2(vector[0], vector[1])) % 360.0


def turn_gap(first, second):
    gap = abs(first - second) % 360.0
    return min(gap, 360.0 - gap)


def band(value, edges):
    """The label of the first band whose upper edge the value is under, the last label above them all."""
    if value is None:
        return None
    for limit, label in edges:
        if value < limit:
            return label
    return edges[-1][1]


def edge_frame(ring, first, second, centre):
    """The frame on an edge: n the unit normal into the plot, r the right hand seen from the road, the ends left and right."""
    length = math.dist(first, second)
    along = ((second[0] - first[0]) / length, (second[1] - first[1]) / length)
    normal = (-along[1], along[0])
    if (centre[0] - first[0]) * normal[0] + (centre[1] - first[1]) * normal[1] < 0:
        normal = (-normal[0], -normal[1])
    right = (normal[1], -normal[0])
    left_end, right_end = (first, second) if first[0] * right[0] + first[1] * right[1] < second[0] * right[0] + second[1] * right[1] else (second, first)
    return {"n": normal, "r": right, "left": left_end, "right": right_end}


def curb_frame(ring, curbs, limits):
    """The front of a face by the edge within limits whose middle is nearest a curb, None with none."""
    centre = shoelace_centre(ring)
    candidates = sorted(((curb_distance(((first[0] + second[0]) / 2.0, (first[1] + second[1]) / 2.0), curbs), first, second) for first, second in edges_of(ring)
                         if limits[0] <= math.dist(first, second) < limits[1]), key=lambda item: item[0])
    return None if not candidates else edge_frame(ring, candidates[0][1], candidates[0][2], centre)


def own_frame(ring, curbs, limits, corner_m, orientation):
    """The front of a face: the edge within limits whose middle is nearest a curb, and on a face with two or more edges whose
    middle lies within corner_m of a curb the one whose outward normal bears into the quadrant of its Orientation, nearest the
    middle of it. None when such a corner face has no Orientation. Returns the frame and the rule."""
    centre = shoelace_centre(ring)
    roads = [(first, second) for first, second in edges_of(ring) if curb_distance(((first[0] + second[0]) / 2.0, (first[1] + second[1]) / 2.0), curbs) <= corner_m]
    if len(roads) < 2:
        return curb_frame(ring, curbs, limits), "nearestCurb"
    quadrant = QUADRANTS.get(orientation or "")
    if quadrant is None:
        return None, "noOrientation"
    scored = []
    for first, second in roads:
        length = math.dist(first, second)
        normal = (-(second[1] - first[1]) / length, (second[0] - first[0]) / length)
        middle = ((first[0] + second[0]) / 2.0, (first[1] + second[1]) / 2.0)
        if inside((middle[0] + normal[0] * 0.01, middle[1] + normal[1] * 0.01), ring):
            normal = (-normal[0], -normal[1])
        scored.append((bearing(normal), first, second))
    centre_bearing = sum(quadrant) / 2.0
    pool = [item for item in scored if quadrant[0] <= item[0] < quadrant[1]] or scored
    _, first, second = min(pool, key=lambda item: turn_gap(item[0], centre_bearing))
    return edge_frame(ring, first, second, centre), "orientation"


def own_road_side(ring, curbs, frame, corner_m):
    """Where the first road edge other than the front lies, seen from the road: back when it lies more across the plot than
    along it, else left or right, none when there is no other road edge, None with no front."""
    if frame is None:
        return None
    centre = shoelace_centre(ring)
    front = {tuple(frame["left"]), tuple(frame["right"])}
    for first, second in edges_of(ring):
        middle = ((first[0] + second[0]) / 2.0, (first[1] + second[1]) / 2.0)
        if {tuple(first), tuple(second)} == front or curb_distance(middle, curbs) > corner_m:
            continue
        along = (middle[0] - centre[0]) * frame["r"][0] + (middle[1] - centre[1]) * frame["r"][1]
        across = (middle[0] - centre[0]) * frame["n"][0] + (middle[1] - centre[1]) * frame["n"][1]
        return "back" if abs(across) > abs(along) else "left" if along < 0 else "right"
    return "none"


def link_axes(frame, mirrored):
    axis_x = (-frame["r"][0], -frame["r"][1]) if mirrored else frame["r"]
    return axis_x, frame["n"]


def anchor_point(frame, mirrored, anchor):
    start, end = (frame["right"], frame["left"]) if mirrored else (frame["left"], frame["right"])
    return start if anchor == "start" else end


def own_origin(frame, mirrored, fit):
    axis_x, axis_y = link_axes(frame, mirrored)
    corner = anchor_point(frame, mirrored, fit["anchor"])
    return (corner[0] + fit["offset"][0] * axis_x[0] + fit["offset"][1] * axis_y[0], corner[1] + fit["offset"][0] * axis_x[1] + fit["offset"][1] * axis_y[1])


def own_fits(links):
    """R121 by its own sums: per code the anchor corner whose offsets in link axes lie nearer their median, the most within 10 mm
    and then within 1 mm, a tie to start, and the median offset to the mm. links are (code, frame, mirrored, origin)."""
    rows = defaultdict(lambda: {anchor: [] for anchor in ANCHORS})
    for code, frame, mirrored, origin in links:
        axis_x, axis_y = link_axes(frame, mirrored)
        for anchor in ANCHORS:
            corner = anchor_point(frame, mirrored, anchor)
            dx, dy = origin[0] - corner[0], origin[1] - corner[1]
            rows[code][anchor].append((dx * axis_x[0] + dy * axis_x[1], dx * axis_y[0] + dy * axis_y[1]))
    fits = {}
    for code, by_anchor in rows.items():
        best = None
        for anchor in ANCHORS:
            offsets = by_anchor[anchor]
            middle = (statistics.median(item[0] for item in offsets), statistics.median(item[1] for item in offsets))
            gaps = [math.dist(item, middle) for item in offsets]
            score = (sum(1 for gap in gaps if gap <= FIT_BANDS[1] + 1e-9), sum(1 for gap in gaps if gap <= FIT_BANDS[0] + 1e-9))
            if best is None or score > best[0]:
                best = (score, anchor, middle)
        fits[code] = {"anchor": best[1], "offset": [round(best[2][0], 3), round(best[2][1], 3)]}
    return fits


def curb_segments(pieces, layers):
    wanted = [name.lower() for name in layers]
    found = []
    for piece in pieces:
        layer = str(piece.get("layer") or "").lower()
        if piece.get("kind") not in ("polyline", "line") or not any(layer == name or layer.endswith("$" + name) for name in wanted):
            continue
        points = [(point[0], point[1]) for point in piece.get("pts") or [] if len(point) >= 2]
        if piece.get("closed") is True and len(points) >= 3:
            points.append(points[0])
        found.extend(zip(points, points[1:]))
    return found


def sales_orientation(path, sheet, plot_column):
    """The Orientation and the No. of streets of each plot of the sales sheet, the header the first row that names the plot column."""
    book = openpyxl.load_workbook(path, read_only=True, data_only=True)
    found, header = {}, None
    for row in book[sheet].iter_rows(values_only=True):
        cells = [None if cell is None else str(cell).strip() for cell in row]
        if header is None:
            if plot_column in cells:
                header = cells
            continue
        item = dict(zip(header, cells))
        number = item.get(plot_column)
        if number and number.replace(".0", "").isdigit():
            found[number.replace(".0", "")] = {"Orientation": item.get("Orientation"), "No. of streets": item.get("No. of streets")}
    return found


def site_check(reads, groups, references, first):
    """R336. The coordinates again: per group each read against the reference key of its group, the gap of each point in mm."""
    found = {}
    for group, reference in references.items():
        mine = {key: read_json(path) for key, path in reads.items() if key in groups.get(group, ())}
        if reference not in mine:
            if mine:
                found.setdefault(group, {})["missingReference"] = {"differ": [], "match": bool((first.get(group) or {}).get("missingReference"))}
            continue
        base = mine[reference]["documents"][0]
        for key, data in mine.items():
            document = data["documents"][0]
            differ = set()
            for name, field in POINT_FIELDS:
                gap = math.sqrt(sum((a - b) ** 2 for a, b in zip(document[name][field], base[name][field]))) * 1000.0
                if gap > 0.5:
                    differ.add(name + "." + field)
            if turn_gap(document["angleToTrueNorthDeg"], base["angleToTrueNorthDeg"]) > 1e-6:
                differ.add("angleToTrueNorthDeg")
            site, base_site = document.get("site"), base.get("site")
            if site and base_site:
                levels = {level["name"]: level["elevation"] for level in site["levels"]}
                for level in base_site["levels"]:
                    if level["name"] not in levels or abs(levels[level["name"]] - level["elevation"]) * 1000.0 > 0.5:
                        differ.add("levels." + level["name"])
                position = site["projectPosition"]
                other = base_site["projectPosition"]
                if math.sqrt(sum((position[axis] - other[axis]) ** 2 for axis in ("eastWest", "northSouth", "elevation"))) * 1000.0 > 0.5:
                    differ.add("projectPosition")
            # The first method also holds the names and units, this route the points, the angle, the position and the levels only
            theirs = {name for name in ((first.get(group) or {}).get("keys", {}).get(key, {}).get("differ") or {})
                      if name in OWN_FIELDS or name.startswith("levels.")}
            found.setdefault(group, {})[key] = {"differ": sorted(differ), "match": sorted(differ) == sorted(theirs)}
    return found


def straight(ring, tolerance=0.001):
    """The ring with every vertex that lies within tolerance of the line through its neighbours left out, so an edge drawn in two
    pieces along one line is one edge, as the face of the first method holds it."""
    points = list(ring)
    changed = True
    while changed and len(points) > 3:
        changed = False
        for index in range(len(points)):
            before, here, after = points[index - 1], points[index], points[(index + 1) % len(points)]
            length = math.dist(before, after)
            if length and abs((after[0] - before[0]) * (here[1] - before[1]) - (after[1] - before[1]) * (here[0] - before[0])) / length <= tolerance \
                    and min(before[0], after[0]) - tolerance <= here[0] <= max(before[0], after[0]) + tolerance \
                    and min(before[1], after[1]) - tolerance <= here[1] <= max(before[1], after[1]) + tolerance:
                del points[index]
                changed = True
                break
    return points


def edges_of(ring):
    return [(ring[index], ring[(index + 1) % len(ring)]) for index in range(len(ring))]


def shared_length(first, second, near):
    """The length of the edges of first that an edge of second lies along, both its ends within near of the line of the edge."""
    total = 0.0
    for a1, a2 in edges_of(first):
        length = math.dist(a1, a2)
        if length == 0:
            continue
        ux, uy = (a2[0] - a1[0]) / length, (a2[1] - a1[1]) / length
        for b1, b2 in edges_of(second):
            if abs((b1[0] - a1[0]) * uy - (b1[1] - a1[1]) * ux) > near or abs((b2[0] - a1[0]) * uy - (b2[1] - a1[1]) * ux) > near:
                continue
            t1, t2 = (b1[0] - a1[0]) * ux + (b1[1] - a1[1]) * uy, (b2[0] - a1[0]) * ux + (b2[1] - a1[1]) * uy
            low, high = max(0.0, min(t1, t2)), min(length, max(t1, t2))
            if high > low:
                total += high - low
    return total


def own_pair_side(plot, sides, counted):
    """The side of a unit in the pair of its row of counted units, the row walked while each step has one, paired from its right end."""
    def walk(side):
        seen, at, steps = {plot}, plot, 0
        while True:
            beside = [other for other in sides.get(at, {}).get(side, []) if other not in seen and counted(other)]
            if len(beside) != 1:
                return steps
            at = beside[0]
            seen.add(at)
            steps += 1
    left, right = walk("left"), walk("right")
    return None if left + right + 1 < 2 else ("right" if right % 2 == 0 else "left")


def learned(rule, scope, value):
    """The value a learned table of majority.json gives a factor value in its scope, the key written as the first method writes it."""
    return rule.get(str(scope) + " | " + str(value))


def own_ramp(frame, depth):
    left, right, normal = frame["left"], frame["right"], frame["n"]
    return [left, right, (right[0] + depth * normal[0], right[1] + depth * normal[1]), (left[0] + depth * normal[0], left[1] + depth * normal[1])]


def own_corner_gap(planned, corners):
    """The largest distance from a planned corner to its own nearest corner of the key, each key corner used once."""
    free, gaps = list(corners), []
    for point in planned:
        best = min(free, key=lambda corner: math.dist(point, corner))
        gaps.append(math.dist(point, best))
        free.remove(best)
    return max(gaps)


def own_boundary_gap(first, second):
    """The largest distance of a vertex of either set of loops to the nearest edge of the other."""
    def one_way(points, loops):
        segments = [edge for loop in loops for edge in edges_of(loop)]
        return max(min(segment_distance(point, a, b) for a, b in segments) for loop in points for point in loop)
    return max(one_way(first, second), one_way(second, first))


def to_link(points, origin, rotation, mirrored):
    turn = math.radians(rotation)
    axis_x = (math.cos(turn), math.sin(turn))
    axis_y = (axis_x[1], -axis_x[0]) if mirrored else (-axis_x[1], axis_x[0])
    return [((x - origin[0]) * axis_x[0] + (y - origin[1]) * axis_x[1], (x - origin[0]) * axis_y[0] + (y - origin[1]) * axis_y[1]) for x, y in points]


def own_refit(rows, seed):
    """R135 by its own sums: per unit, mirror flag and plot width the yard outline of the plot of the fit half whose largest gap to
    the others of that half is least, a tie to the lower plot, the half split by the seed, a group under MIN_PLOTS fitted on all.
    rows are dicts with plot, unit, mirrored, width and loops in link axes."""
    groups = defaultdict(list)
    for row in rows:
        groups[(row["unit"], row["mirrored"], row["width"])].append(row)
    fits = {}
    for key, members in groups.items():
        pool = members
        if len(members) >= MIN_PLOTS:
            ordered = sorted(members, key=lambda row: int(row["plot"]))
            random.Random(seed).shuffle(ordered)
            pool = ordered[:(len(ordered) + 1) // 2]
        best = min(pool, key=lambda row: (max(own_boundary_gap(row["loops"], other["loops"]) for other in pool), int(row["plot"])))
        fits[key] = [[(round(x, 3), round(y, 3)) for x, y in loop] for loop in best["loops"]]
    return fits


def own_clusters(numbers, book, faces, blocks, analysis):
    """R236 by its own route: per plot the insert of its footprintBlock name whose outline shares the most area with its face,
    exact by clipping, the plots that share one insert a cluster of two or more in plot order, then each cluster of the CAD
    analysis by the location plan numbers of its labels less the plots those took, and a face with two plan numbers left out."""
    by_name = defaultdict(list)
    for block in blocks:
        outline = block.get("outline")
        if outline and len(outline) >= 3:
            by_name[block.get("name")].append([(point[0], point[1]) for point in outline])
    groups = defaultdict(list)
    for number in numbers:
        row, face = book.get(number), faces.get(number)
        if row is None or face is None or not row.get("footprintBlock"):
            continue
        best, held = None, 0.0
        for index, outline in enumerate(by_name.get(row["footprintBlock"], [])):
            share = overlap_area(outline, face)
            if share > held + 1e-9:
                best, held = index, share
        if best is not None:
            groups[(row["footprintBlock"], best)].append(number)
    order = lambda plot: (0, int(plot)) if plot.isdigit() else (1, plot)
    footprint = [sorted(plots, key=order) for _, plots in sorted(groups.items()) if len(plots) > 1]
    number_of = {row["baseplanLabel"]: number for number, row in book.items() if row.get("baseplanLabel")}
    taken = {plot for cluster in footprint for plot in cluster}
    rest = []
    for cluster in analysis:
        plots = [number_of[label] for label in cluster["plots"] if label in number_of and number_of[label] not in taken]
        if len(plots) > 1:
            rest.append(plots)
    return [plots for plots in ([plot for plot in cluster if plot.isdigit()] for cluster in footprint + rest) if len(plots) > 1]


def analysis_only(book, analysis):
    """The clusters of the CAD analysis alone by location plan number, for clusterSideAnalysisOnly (Q040)."""
    number_of = {row["baseplanLabel"]: number for number, row in book.items() if row.get("baseplanLabel")}
    found = []
    for cluster in analysis:
        plots = [number_of[label] for label in cluster["plots"] if label in number_of]
        plots = [plot for plot in plots if plot.isdigit()]
        if len(plots) > 1:
            found.append(plots)
    return found


def parameter_verdicts(row, ctx, settings, report, factors=None):
    """R336. Each parameter decision of the group held again on the row: the value the plot holds, from the parameters of
    past_all.csv or the key read, against the factor table or the formula the first method coded, with factors the own values
    of the factors, else those the row gives itself."""
    block = settings["majority"]
    group = block["check"]["group"]
    decisions = {item["decision"]: item for item in report["decisions"]}
    values = json.loads(row.get("parameters") or "{}")
    read = ctx["readParameters"].get((row["key"], row["plot"])) or {}
    leave = set(block["kindLeaveOut"].get(row.get("kind") or "", []))
    factors = dict(factors) if factors is not None else row_factors(row, block)
    facade = [str(values.get(name) or "") for name in block["linkNameFacade"]]
    formulas = {"plot": lambda: row["plot"], "rev9Code": lambda: row["typologyRev9"], "plotAndCode": lambda: row["plot"] + " | " + row["typologyRev9"],
                "plotCodeStepsAndFacade": lambda: " | ".join([row["plot"], row["typologyRev9"], row.get("steps") or "None"] + facade)}
    found, unknown = {}, []
    for name in block["parameterTests"]:
        decision = "parameter " + name
        coded = decisions.get(group + " " + decision)
        value = values.get(name)
        if coded is None or value in (None, "") or "parameter" in leave or decision in leave:
            continue
        verdict = evaluate(coded, factors, formulas, str(value), group, lambda guess, aim: str(guess) == aim)
        if verdict is None:
            unknown.append(decision)
        else:
            found[decision] = verdict
    for name in block["parameterLevels"]:
        decision = "parameter " + name
        coded = decisions.get(group + " " + decision)
        # R341. A level parameter of R339 is held by the own level route
        if coded is not None and "passMm" in coded:
            continue
        try:
            value = float(values.get(name) or read.get(name))
        except (TypeError, ValueError):
            continue
        if coded is None or "parameter" in leave or decision in leave:
            continue
        levels = {"ffl": lambda: number(row.get("ffl")), "genericTop": lambda: number(row.get("genericTop"))}
        verdict = evaluate(coded, {}, levels, value, group, lambda guess, aim: abs(guess - aim) * 1000.0 <= block["levelHoldMm"] + 1e-6)
        if verdict is None:
            unknown.append(decision)
        else:
            found[decision] = verdict
    if unknown:
        found["cannotCheck"] = unknown
    return found


def number(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def evaluate(decision, factors, formulas, target, scope, hold):
    """R336. The own verdict of one plot under the candidate the first method coded, True for holds: a formula by its own sums, a
    factor by its own value in the table the first method learned, a value neither gives being a miss, as the first method lists a
    cannot. None when the coded candidate is one this route can not give, a failure of the check."""
    coded = decision["coded"]
    if coded in formulas:
        guess = formulas[coded]()
    elif coded in factors and "rule" in decision["tests"].get(coded, {}):
        value = factors[coded]
        guess = None if value is None else learned(decision["tests"][coded]["rule"], scope, value)
    else:
        return None
    return guess is not None and target is not None and bool(hold(guess, target))


def row_factors(row, block):
    """The factors a row of past_all.csv gives itself, with no geometry: the code, the steps and the mirror flag."""
    code = row["typologyRev9"] if block["codeFrom"].get(row.get("source") or block["check"]["group"]) == "rev9" else (row.get("keyUnit") or row["typologyRev9"])
    steps = row.get("steps") or None
    return {"all": "all", "typology": code, "key": row["key"], "zone": row.get("zone") or None, "nbh": row.get("nbh") or None, "steps": steps,
            "typologySteps": None if steps is None else code + " " + steps, "mirroredFactor": row["mirrored"], "typologyMirrored": code + " " + row["mirrored"]}


def wall_verdicts(ctx, settings, profile, report):
    """R336. Each boundary wall of the key plans of the group held again: its base, top and line by its own sums under the
    candidate the first method coded, keyed by key and wall id. A wall whose base constraint no floor of its plan names has no
    base and no top, and is listed in ctx noBaseLevel when given."""
    block = settings["majority"]
    group = block["check"]["group"]
    coded = {item["decision"].split(" ", 1)[1]: item for item in report["decisions"] if item["decision"].startswith(group + " wall")}
    depth = next((found.get("yardBelowFflM") for found in profile["levelProfiles"].values() if isinstance(found, dict) and found.get("job") == group), None)
    rise = profile["rev6"]["boundary_wall_top_above_ffl"] / 1000.0
    half = profile["plot"]["boundary_wall_thickness"] / 2000.0
    level_hold = lambda guess, aim: abs(guess - aim) * 1000.0 <= block["levelHoldMm"] + 1e-6
    found = {}
    for key, plan in ctx["fullPlans"].items():
        plots = {entry["plot"]: entry for entry in plan["plots"]}
        levels = {floor["level"]["value"]: floor["levelElevation"]["value"] for entry in plan["plots"] for floor in (entry.get("floors") or {}).values()
                  if "level" in floor and "levelElevation" in floor}
        for wall in plan.get("walls") or []:
            if not re.search(block["wallTypes"]["boundary"], wall.get("type") or ""):
                continue
            ffls = [plots[plot]["levels"]["ffl"]["value"] for plot in wall.get("plots") or [] if plot in plots and (plots[plot].get("levels") or {}).get("ffl")]
            if not ffls:
                continue
            verdict = {}
            level = levels.get(wall["baseConstraint"])
            if level is None:
                ctx.setdefault("noBaseLevel", []).append({"key": key, "elementId": wall["elementId"], "baseConstraint": wall["baseConstraint"]})
            else:
                base = wall["baseOffset"] + level
                bases = {"yardLevelOfTheLowerPlot": lambda: None if depth is None else min(ffls) - depth,
                         "yardLevelOfTheHigherPlot": lambda: None if depth is None else max(ffls) - depth}
                tops = {"higherFflPlusRev6": lambda: max(ffls) + rise, "lowerFflPlusRev6": lambda: min(ffls) + rise}
                for word, formulas, target in (("wallBase", bases, base), ("wallTop", tops, base + wall["heightM"])):
                    if word in coded:
                        verdict[word] = evaluate(coded[word], {}, formulas, target, group, level_hold)
            rings = [ctx["faces"][plot] for plot in wall.get("plots") or [] if plot in ctx["faces"]]
            if "wallLine" in coded and rings:
                middle = ((wall["start"][0] + wall["end"][0]) / 2.0, (wall["start"][1] + wall["end"][1]) / 2.0)
                distance = min(segment_distance(middle, a, b) for ring in rings for a, b in edges_of(ring))
                lines = {"centreOnTheEdge": lambda: 0.0 if wall.get("locationLine") in block["wallCentreCodes"] else half}
                verdict["wallLine"] = evaluate(coded["wallLine"], {}, lines, distance, group, lambda guess, aim: abs(guess - aim) <= block["outlineHoldMm"] / 1000.0 + 1e-9)
            found[(key, wall["elementId"])] = verdict
    return found


def own_front_cover(frame, walls, near):
    """The share of the front a wall within 5 degrees of it and with both ends within near of its line runs along."""
    left, right = frame["left"], frame["right"]
    length = math.dist(left, right)
    along = ((right[0] - left[0]) / length, (right[1] - left[1]) / length)
    spans = []
    for wall in walls:
        first, second = wall["start"][:2], wall["end"][:2]
        size = math.dist(first, second)
        if size == 0 or abs((second[0] - first[0]) * along[1] - (second[1] - first[1]) * along[0]) / size > math.sin(math.radians(5)):
            continue
        if max(abs((point[0] - left[0]) * along[1] - (point[1] - left[1]) * along[0]) for point in (first, second)) > near:
            continue
        ends = sorted((point[0] - left[0]) * along[0] + (point[1] - left[1]) * along[1] for point in (first, second))
        spans.append((max(0.0, ends[0]), min(length, ends[1])))
    covered, reach = 0.0, 0.0
    for low, high in sorted(span for span in spans if span[1] > span[0]):
        covered += max(0.0, high - max(low, reach))
        reach = max(reach, high)
    return covered / length


def plot_context(args, settings, profile, report, table):
    """Everything the own routes read, once: faces, curbs, Orientation, unit plots, side neighbours, the clusters made here, the
    fits made here and the key plans."""
    block = settings["majority"]
    group = block["check"]["group"]
    with open(args.plots_csv, encoding="utf-8") as handle:
        book = {row["locationPlanNumber"]: row for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
    rings = {}
    for entry in read_json(args.baseplan)["plots"]:
        if entry.get("kind") == "plot_shape" and len(entry.get("vertices") or []) >= 3:
            rings.setdefault(str(entry["plot"]), []).append([(vertex[0], vertex[1]) for vertex in entry["vertices"]])
    faces = {number: straight(rings[row["baseplanLabel"]][0]) for number, row in book.items() if len(rings.get(row.get("baseplanLabel") or "", [])) == 1}
    curbs = curb_segments(read_json(Path(args.roads) / "pieces.json")["pieces"], settings["analysis"]["roadEdgeLayers"])
    sheet = read_json(ROOT / "python" / "c3d_probe.json")["dataBook"]["locationPlan"]["sales"]
    sales = sales_orientation(args.sales, sheet["sheet"], sheet["columns"]["plot"])
    widths = [entry["plot_width_m"] for entry in profile["typologies"].values() if isinstance(entry.get("plot_width_m"), (int, float))]
    limits = (min(widths) - settings["analysis"]["toleranceM"], settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0)
    corner_m = settings["siteBuild"]["corner"]["roadEdgeM"]
    frames, curb_frames = {}, {}

    def frame_of(plot):
        if plot not in frames:
            frames[plot] = (None, "noFace") if plot not in faces else own_frame(faces[plot], curbs, limits, corner_m, (sales.get(plot) or {}).get("Orientation"))
        return frames[plot]

    def curb_frame_of(plot):
        if plot not in curb_frames:
            curb_frames[plot] = None if plot not in faces else curb_frame(faces[plot], curbs, limits)
        return curb_frames[plot]
    units = [number for number, row in book.items() if row.get("typologyRev9") and number in faces]
    boxes = {number: (min(x for x, _ in faces[number]), min(y for _, y in faces[number]), max(x for x, _ in faces[number]), max(y for _, y in faces[number]))
             for number in units}
    least = block["sideShare"] * profile["plot"]["long_side"] / 1000.0
    sides = {}

    def sides_of(plot):
        """The unit plots that share least of their edges with the plot, left or right of it along its own front."""
        if plot not in sides:
            found = {"left": [], "right": []}
            frame = frame_of(plot)[0]
            if frame is not None and plot in boxes:
                x1, y1, x2, y2 = boxes[plot]
                centre = shoelace_centre(faces[plot])
                for other in units:
                    a1, b1, a2, b2 = boxes[other]
                    if other == plot or a1 > x2 + 0.1 or a2 < x1 - 0.1 or b1 > y2 + 0.1 or b2 < y1 - 0.1:
                        continue
                    if shared_length(faces[plot], faces[other], 0.05) < least:
                        continue
                    there = shoelace_centre(faces[other])
                    along = (there[0] - centre[0]) * frame["r"][0] + (there[1] - centre[1]) * frame["r"][1]
                    found["left" if along < 0 else "right"].append(other)
            sides[plot] = found
        return sides[plot]
    # R236. The clusters made again from the footprint blocks and the analysis, never taken from majority.json
    analysis = read_json(args.analysis)["clusters"]
    site = sorted({row["plot"] for row in table} | set(units))
    # R300. The villa blocks pass the guard of the reference only drawings, an input check and no function that made the rules
    import project_profile
    blocks = project_profile.read_villa_blocks(args.footprint_blocks, project_profile.load_settings(args.settings))["blocks"]
    clusters = own_clusters(site, book, faces, blocks, analysis)
    # A cluster gives its plots a side when every plot has a face and one has a front, the site as a build sees it (R123, R271)
    member = {plot: cluster for cluster in clusters if all(other in faces for other in cluster) and any(frame_of(other)[0] is not None for other in cluster) for plot in cluster}
    analysis_member = {plot: cluster for cluster in analysis_only(book, analysis)
                       if all(other in faces for other in cluster) and any(frame_of(other)[0] is not None for other in cluster) for plot in cluster}

    def kind(plot):
        code = (book.get(plot) or {}).get("typologyRev9") or ""
        found = (profile["typologies"].get(code) or {}).get("kind")
        return found or next((value for prefix, value in block["kindByPrefix"].items() if code.startswith(prefix)), None)
    full = {key: read_json(path) for key, path in args.key_plan}
    plans = {key: {entry["plot"]: entry for entry in plan["plots"]} for key, plan in full.items()}
    read_parameters = {}
    for key, path in args.key_read:
        for link in read_json(path)["documents"][0].get("links") or []:
            plot = str((link.get("parameters") or {}).get(block["linkPlotParameter"]) or "").strip()
            if plot:
                read_parameters[(key, plot)] = None if (key, plot) in read_parameters else link["parameters"]
    step = settings["cadLinks"]["floorWidthStepM"]
    ctx = {"book": book, "faces": faces, "frame": frame_of, "curbFrame": curb_frame_of, "sides": sides_of, "member": member, "analysisMember": analysis_member,
           "clusters": clusters, "kind": kind, "plans": plans, "step": step, "curbs": curbs, "cornerM": corner_m, "fullPlans": full, "sales": sales,
           "readParameters": {pair: found for pair, found in read_parameters.items() if found is not None}}
    # R121, R332. The link origin fitted again per code on every link of the group with a front, by the own frames
    links = [(own_code(row, block), frame_of(row["plot"])[0], row["mirrored"] == "True", (float(row["x"]), float(row["y"]))) for row in table
             if row["source"] == group and row["x"] and row["mirrored"] in ("True", "False") and row["rotationDeg"] and frame_of(row["plot"])[0] is not None]
    ctx["fits"] = own_fits(links)
    ctx["plotFrame"] = read_json(ROOT / "config" / "prototype_offsets.json")["plotFrame"]["units"]
    # R135, R334. The yard outlines fitted again per unit, mirror flag and width on the key plans of the group, in link axes
    rows = []
    for key, plan in full.items():
        if key not in block["groups"][group]:
            continue
        for entry in plan["plots"]:
            floor, frame = (entry.get("floors") or {}).get("yard"), frame_of(entry["plot"])[0]
            if floor is None or frame is None:
                continue
            loops = [to_link(loop["points"], entry["insert"]["value"], entry["rotationDeg"]["value"], entry["mirrored"]["value"]) for loop in floor["outline"]["value"]["loops"]]
            rows.append({"plot": entry["plot"], "key": key, "unit": entry["unit"]["value"], "mirrored": entry["mirrored"]["value"], "loops": loops,
                         "width": round(round(math.dist(frame["left"], frame["right"]) / step) * step, 3)})
    ctx["yardRows"] = {(row["key"], row["plot"]): row for row in rows}
    ctx["refit"] = own_refit(rows, block["check"]["seeds"][0])
    ctx["fitted"] = {(item["unit"], item["mirrored"], item["width"]): item["loops"] for item in read_json(ROOT / "config" / "floor_outlines.json")["outlines"]["yard"]}
    ctx["rows"] = {(row["key"], row["plot"]): row for row in table}
    # R337. A group a level profile with keyRules builds links the file of the code in the build folder, its hands are held by that table
    built = any(isinstance(found, dict) and found.get("keyRules") and found.get("job") == group for found in (profile.get("levelProfiles") or {}).values())
    ctx["buildFiles"] = None
    if built:
        folder = Path(os.environ.get("SITE_ADAPTATION_INPUT") or settings["inputRoot"]) / settings["pastJob"]["upgradedPrototypeFolder"]
        ctx["buildFiles"] = own_build_files([path.name for path in folder.iterdir() if path.is_file()], settings["prototypeNames"], profile["typologies"])
    return ctx


def own_code(row, block):
    return row["typologyRev9"] if block["codeFrom"].get(row["source"]) == "rev9" else (row.get("keyUnit") or row["typologyRev9"])


def own_build_files(names, prototype_names, typologies):
    """R336, R337. The file a build links per code, by its own route: a file name of the folder that starts with the prefix and the
    discipline of prototypeNames gives the code up to the suffix, a code two names give has none, and a code with no file of its own
    takes the file of its keo_file."""
    head = prototype_names["prefix"] + prototype_names["discipline"] + "-"
    found, twice = {}, set()
    for name in sorted(names):
        if not name.startswith(head) or prototype_names["suffix"] not in name[len(head):]:
            continue
        code = name[len(head):].split(prototype_names["suffix"])[0]
        if code in found:
            twice.add(code)
        found[code] = name
    files = {code: name for code, name in found.items() if code not in twice}
    for code, typology in typologies.items():
        if code not in files and code not in twice and (typology or {}).get("keo_file") in files:
            files[code] = files[typology["keo_file"]]
    return files


def dry_entries(data):
    """R341. Every entry of a dry list, its overrides and the misses with no value its cannot list holds (R272)."""
    return list(data["overrides"]) + list(data.get("cannot") or [])


def cannot_check(merged, dry, keep):
    """R272, R330. The cannot list a run merged against the cannot list of its dry run, as JSON text, per job the entries of what it
    does not keep equal."""
    kept = lambda item: item["source"] in keep and (keep[item["source"]] is None or item["field"] in keep[item["source"]])
    text = lambda items: Counter(json.dumps(item, sort_keys=True, ensure_ascii=False) for item in items if not kept(item))
    return {"cannot": len(merged), "cannotEqualDry": text(merged) == text(dry), "cannotBySource": dict(sorted(Counter(item["source"] for item in merged).items()))}


def first_misses(dry, report):
    """R336. The plots the first method misses per decision: every entry of the dry list, and every plot and field an open question
    decides (R272, R337), which majority.py lists apart and writes no override of."""
    misses = defaultdict(set)
    for item in dry:
        misses[item.get("decision")].add((item.get("key"), str(item["plot"])))
    for item in report.get("decidedByQuestion") or []:
        misses[item["decision"]].add((item["key"], str(item["plot"])))
    return dict(misses)


def cluster_role(plot, cluster, ctx):
    """The side and index of a plot in its cluster, the plots put left to right along the own front of the first plot with one."""
    frame = next((ctx["frame"](other)[0] for other in cluster if ctx["frame"](other)[0] is not None), None)
    if frame is None or any(other not in ctx["faces"] for other in cluster):
        return None
    along = {other: shoelace_centre(ctx["faces"][other])[0] * frame["r"][0] + shoelace_centre(ctx["faces"][other])[1] * frame["r"][1] for other in cluster}
    ordered = sorted(cluster, key=lambda other: along[other])
    index, count = ordered.index(plot), len(ordered)
    return {"side": "left" if index < (count - 1) / 2 else "right" if index > (count - 1) / 2 else "middle", "index": index + 1}


def cluster_side(plot, cluster, ctx):
    role = cluster_role(plot, cluster, ctx)
    return None if role is None else role["side"]


def row_sides(plot, ctx, counted):
    """The side neighbours of every counted unit of the row of a plot, walked both ways."""
    found, todo = {}, [plot]
    while todo:
        at = todo.pop()
        if at in found:
            continue
        found[at] = ctx["sides"](at)
        todo.extend(other for side in ("left", "right") for other in found[at][side] if counted(other) and other not in found)
    return found


def entry_of(row, block):
    """El @ Entry of a row, None where a key parameter is off its own ramp back by more than entryRampM (R270)."""
    entry, back = number(row.get("entry")), number(row.get("rampBack"))
    if entry is None:
        return None
    if row.get("entrySource") == "floorParameter" and back is not None and abs(entry - back) > block["entryRampM"]:
        return None
    return entry


def road_of(data, surface):
    left, right = (data.get("roadLevelLeft"), data.get("roadLevelRight")) if surface == ROAD else (data.get("level_" + surface + "_left"), data.get("level_" + surface + "_right"))
    if left in (None, "") or right in (None, ""):
        return None
    return (float(left) + float(right)) / 2.0


def own_class(row, block):
    """R273. The ramp slope class of the key ramp: from its type name, else the band of its slope."""
    named = block["rampTypeClass"].get(row.get("rampType") or "")
    return named if named is not None else band(number(row.get("rampSlopePercent")), block["slopeBands"])


def own_factors(row, ctx, block, profile):
    """Every factor of R271 this route gives a plot again by its own sums."""
    factors = row_factors(row, block)
    plot, data = row["plot"], ctx["book"].get(row["plot"]) or {}
    frame = ctx["frame"](plot)[0]
    here = ctx["sides"](plot)
    mirror_block = {"yes": "yes", "no": "no"}.get(data.get("footprintMirrored"))
    open_end = None if frame is None else "both" if not here["left"] and not here["right"] else "left" if not here["left"] else "right" if not here["right"] else "none"
    road = own_road_side(ctx["faces"][plot], ctx["curbs"], frame, ctx["cornerM"]) if plot in ctx["faces"] else None
    unit = plot in ctx["faces"] and bool(data.get("typologyRev9"))
    left, at, seen = 0, plot, {plot}
    while unit:
        beside = [other for other in ctx["sides"](at)["left"] if other not in seen]
        if len(beside) != 1:
            break
        at = beside[0]
        seen.add(at)
        left += 1
    role = cluster_role(plot, ctx["member"][plot], ctx) if plot in ctx["member"] else None
    analysis_role = cluster_role(plot, ctx["analysisMember"][plot], ctx) if plot in ctx["analysisMember"] else None
    counted = lambda other: ctx["kind"](other) in block["pairKinds"] and other not in ctx["member"] and other.isdigit()
    pair = own_pair_side(plot, row_sides(plot, ctx, counted), counted) if counted(plot) else None
    sale = ctx["sales"].get(plot) or {}
    streets = sale.get("No. of streets")
    depth = number(data.get("depthM"))
    entry, road_mean = entry_of(row, block), road_of(data, ROAD)
    setbacks = profile["ramp"]["front_setback"]
    ramp_depth = setbacks.get(row["typologyRev9"], setbacks["default"]) / 1000.0
    factors.update({
        "blockMirror": mirror_block, "openEnd": open_end, "roadSide": road, "rowParity": None if not unit else ("odd" if left % 2 == 0 else "even"),
        "clusterSide": "alone" if plot not in ctx["member"] else (role or {}).get("side"),
        "clusterSideAnalysisOnly": "alone" if plot not in ctx["analysisMember"] else (analysis_role or {}).get("side"),
        "clusterIndexParity": "alone" if plot not in ctx["member"] else None if role is None else ("odd" if role["index"] % 2 else "even"),
        "orientation": sale.get("Orientation") or None, "corner": None if streets is None else ("yes" if (number(streets) or 0) >= 2 else "no"),
        "rowPairSide": pair, "depthBand": band(depth, block["depthBands"]), "measuredBand": band(number(row.get("rampSlopePercent")), block["slopeBands"]),
        "heightOverRoad": None if entry is None or road_mean is None else band((entry - road_mean) / ramp_depth * 100.0, block["slopeBands"]),
        "heightBand": band(number(row.get("height")), block["heightBands"]),
    })
    factors["blockMirrorOpenEnd"] = None if mirror_block is None or open_end is None else mirror_block + " " + open_end
    factors["blockMirrorRoadSide"] = None if mirror_block is None or road is None else mirror_block + " " + road
    factors["rowPairSideOrBlockMirrorOpenEnd"] = "pair " + pair if pair else None if factors["blockMirrorOpenEnd"] is None else "alone " + factors["blockMirrorOpenEnd"]
    # R273. The class most side neighbours of the same key take, of the rows the ramp decision holds
    votes = Counter()
    for other in here["left"] + here["right"]:
        neighbour = ctx["rows"].get((row["key"], other))
        if neighbour is not None and "rampSlope" not in block["kindLeaveOut"].get(neighbour.get("kind") or "", ()):
            found = own_class(neighbour, block)
            if found is not None:
                votes[found] += 1
    factors["neighbour"] = votes.most_common(1)[0][0] if votes else None
    return factors


def key_datum(rows, ctx, surface, target):
    """R275. The median gap of the key value over the road of a surface, per key, over the rows that hold both."""
    gaps = defaultdict(list)
    for row in rows:
        road, value = road_of(ctx["book"].get(row["plot"]) or {}, surface), target(row)
        if road is not None and value is not None:
            gaps[row["key"]].append(value - road)
    return {key: statistics.median(values) for key, values in gaps.items()}


def held(row, ctx, settings, profile, report, datums):
    """R336. The own verdict, True for holds, of every decision of 3a that holds this plot, the decisions whose coded candidate
    this route can not give, and the rule of its front."""
    block = settings["majority"]
    group = block["check"]["group"]
    decisions = {item["decision"]: item for item in report["decisions"]}
    leave = set(block["kindLeaveOut"].get(row.get("kind") or "", []))
    plot, mirrored = row["plot"], row["mirrored"] == "True"
    frame, rule = ctx["frame"](plot)
    factors = own_factors(row, ctx, block, profile)
    data = ctx["book"].get(plot) or {}
    code = own_code(row, block)
    typologies = profile["typologies"]
    found, unknown = {}, []

    def judge(word, formulas, target, hold, scope=group, plain=None):
        decision = decisions.get(group + " " + word)
        if decision is None or word.split(" ")[0] in leave or word in leave or target is None:
            return
        verdict = evaluate(decision, plain if plain is not None else factors, formulas, target, scope, hold)
        if verdict is None:
            unknown.append(word)
        else:
            found[word] = verdict

    rotation_hold = lambda guess, aim: turn_gap(angle_of(guess), aim) <= block["rotationHoldDeg"] + 1e-9
    axes = lambda chosen: None if chosen is None else link_axes(chosen, mirrored)[0]
    rotations = {"frontRule": lambda: axes(frame), "nearestCurb": lambda: axes(ctx["curbFrame"](plot))}
    judge("linkRotation", rotations, float(row["rotationDeg"]), rotation_hold, plain={})
    if rule == block["cornerFrontRule"]:
        judge("frontCorner", rotations, float(row["rotationDeg"]), rotation_hold, plain={})

    def origin_by(fits):
        fit = fits.get(code) or fits.get((typologies.get(code) or {}).get("keo_file"))
        return None if fit is None or frame is None else own_origin(frame, mirrored, fit)
    judge("linkOrigin", {"plotFrameRefit": lambda: origin_by(ctx["fits"]), "plotFrame": lambda: origin_by(ctx["plotFrame"])}, (float(row["x"]), float(row["y"])),
          lambda guess, aim: math.dist(guess, aim) * 1000.0 <= block["linkHoldMm"] + 1e-6, plain={})
    if row.get("rampType") or row.get("rampSlopePercent"):
        judge("rampSlope", {}, own_class(row, block), lambda guess, aim: guess == aim)
    if row.get("rampCorners"):
        corners = [(corner[0], corner[1]) for corner in json.loads(row["rampCorners"])]
        if len(corners) == 4:
            setbacks = profile["ramp"]["front_setback"]
            depth = setbacks.get(row["typologyRev9"], setbacks["default"]) / 1000.0
            judge("rampOutline", {"outlineR127": lambda: None if frame is None else own_ramp(frame, depth)}, corners,
                  lambda guess, aim: own_corner_gap(guess, aim) <= block["outlineHoldMm"] / 1000.0 + 1e-9, plain={})
    level_hold = lambda guess, aim: abs(guess - aim) * 1000.0 <= block["levelHoldMm"] + 1e-6
    front = number(row.get("rampFront"))
    # R341. A ramp front of R339 is held by the own level route, against the road a build gives
    if front is not None and "passMm" not in (decisions.get(group + " rampFront") or {}):
        surfaces = [ROAD] + sorted({column[len("level_"):-len("_left")] for column in data if column.startswith("level_") and column.endswith("_left")})
        formulas = {}
        for surface in surfaces:
            formulas[surface] = lambda surface=surface: road_of(data, surface)
            formulas[surface + " less the datum of each key"] = (lambda surface=surface: None if road_of(data, surface) is None or row["key"] not in datums.get(surface, {})
                                                                 else road_of(data, surface) + datums[surface][row["key"]])
        judge("rampFront", formulas, front, level_hold, plain={})
    fall = number(row.get("rampFrontFall"))
    if fall is not None:
        left, right = number(data.get("roadLevelLeft")), number(data.get("roadLevelRight"))
        judge("rampFrontFall", {"levelFront": lambda: 0.0, "roadAtEachCorner": lambda: None if left is None or right is None else abs(left - right)}, fall, level_hold, plain={})
    if row.get("genericOutline") and plot in ctx["faces"]:
        share = whole_face_share(row["genericOutline"], ctx["faces"][plot])
        if share is not None:
            judge("generic", {}, "wholeFace" if share >= block["wholeFaceShare"] else "partOfFace", lambda guess, aim: guess == aim)
    top = number(row.get("genericTop")) if row.get("genericTop") else number(row.get("ffl"))
    yard_depth = "noYard" if not row.get("yardTop") else None if top is None else "%.3f" % (top - float(row["yardTop"]))
    judge("yard", {}, yard_depth, lambda guess, aim: guess == aim)
    yard_row = ctx["yardRows"].get((row["key"], plot))
    if yard_row is not None:
        outlines = {"fittedOutlineRefit": lambda: ctx["refit"].get((yard_row["unit"], yard_row["mirrored"], yard_row["width"])),
                    "fittedOutline": lambda: ctx["fitted"].get((yard_row["unit"], yard_row["mirrored"], yard_row["width"]))}
        judge("yardOutline", outlines, yard_row["loops"], lambda guess, aim: own_boundary_gap(guess, aim) <= block["outlineHoldMm"] / 1000.0 + 1e-9, plain={})
    kind = ctx["kind"](plot)
    unit_class = "villa" if kind == "villa" else "cluster" if plot in ctx["member"] else "alone"
    # R337. The table of the file a build links, which the first method judges the coded hand by
    link_file = row["linkFile"] if ctx.get("buildFiles") is None else (ctx["buildFiles"].get(own_code(row, block)) or row["linkFile"])
    judge("hand " + unit_class, {}, mirrored, lambda guess, aim: guess == aim, scope=(group, link_file))
    verdicts = parameter_verdicts(row, ctx, settings, report, factors)
    unknown.extend(verdicts.pop("cannotCheck", []))
    found.update(verdicts)
    # R341. The levels of R339 by the own level route, made once for every plot
    found.update((ctx.get("levels") or {}).get((row["key"], plot), {}))
    walls = [wall for wall in (ctx["fullPlans"].get(row["key"]) or {}).get("walls") or []
             if plot in (wall.get("plots") or []) and re.search(block["wallTypes"]["boundary"], wall.get("type") or "")]
    if frame is not None and row["key"] in ctx["fullPlans"]:
        cover = own_front_cover(frame, walls, settings["keyPlans"]["wallEdgeM"])
        judge("frontOpen", {}, "open" if cover <= block["frontOpenShare"] + 1e-9 else "walled", lambda guess, aim: guess == aim)
    return found, unknown, rule


class OwnTin:
    """R341. The visible triangles of one LandXML surface, each as three (easting, northing, z), those whose box meets box when given,
    in a grid of CELL_M cells, read by a plain text scan of the file."""

    def __init__(self, path, box=None, margin=10.0):
        text = Path(path).read_bytes()
        points = {}
        for found in POINT.finditer(text):
            parts = found.group(2).split()
            if len(parts) == 3:
                points[int(found.group(1))] = (float(parts[1]), float(parts[0]), float(parts[2]))
        self.triangles, self.cells = [], defaultdict(list)
        for found in FACE.finditer(text):
            if found.group(1) and HIDDEN.search(found.group(1)):
                continue
            ids = found.group(2).split()
            if len(ids) != 3 or any(int(name) not in points for name in ids):
                continue
            corners = [points[int(name)] for name in ids]
            low_x, high_x = min(corner[0] for corner in corners), max(corner[0] for corner in corners)
            low_y, high_y = min(corner[1] for corner in corners), max(corner[1] for corner in corners)
            if box is not None and (high_x < box[0] - margin or low_x > box[2] + margin or high_y < box[1] - margin or low_y > box[3] + margin):
                continue
            index = len(self.triangles)
            self.triangles.append(corners)
            for i in range(math.floor(low_x / CELL_M), math.floor(high_x / CELL_M) + 1):
                for j in range(math.floor(low_y / CELL_M), math.floor(high_y / CELL_M) + 1):
                    self.cells[(i, j)].append(index)

    def inside(self, x, y):
        """The level of the triangle that holds the point, a point on an edge or a corner inside, None when none holds it."""
        for index in self.cells.get((math.floor(x / CELL_M), math.floor(y / CELL_M)), ()):
            (x0, y0, z0), (x1, y1, z1), (x2, y2, z2) = self.triangles[index]
            det = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
            if det == 0:
                continue
            first = ((y1 - y2) * (x - x2) + (x2 - x1) * (y - y2)) / det
            second = ((y2 - y0) * (x - x2) + (x0 - x2) * (y - y2)) / det
            third = 1.0 - first - second
            if first >= -1e-9 and second >= -1e-9 and third >= -1e-9:
                return first * z0 + second * z1 + third * z2
        return None

    def level(self, x, y, snap):
        """The level where the point is, else at the nearest point of a triangle side within snap, as R157 snaps to a surface edge."""
        found = self.inside(x, y)
        if found is not None:
            return found
        best = None
        for i in range(math.floor((x - snap) / CELL_M), math.floor((x + snap) / CELL_M) + 1):
            for j in range(math.floor((y - snap) / CELL_M), math.floor((y + snap) / CELL_M) + 1):
                for index in self.cells.get((i, j), ()):
                    corners = self.triangles[index]
                    for a, b in ((0, 1), (1, 2), (2, 0)):
                        (ax, ay, az), (bx, by, bz) = corners[a], corners[b]
                        dx, dy = bx - ax, by - ay
                        length = dx * dx + dy * dy
                        part = 0.0 if length == 0 else min(1.0, max(0.0, ((x - ax) * dx + (y - ay) * dy) / length))
                        distance = math.hypot(x - (ax + part * dx), y - (ay + part * dy))
                        if best is None or distance < best[0]:
                            best = (distance, az + part * (bz - az))
        return best[1] if best is not None and best[0] <= snap else None


def own_road(tins, point, snap):
    """R341. The level of the first surface, in the order of siteBuild.roadSurfaces, that covers the point within snap."""
    for _, tin in tins:
        found = tin.level(point[0], point[1], snap)
        if found is not None:
            return found
    return None


def own_ffl(members):
    """R341. One FFL of a piece: its highest El @ Entry plus the base of that unit, to the mm. members are (El @ Entry, base)."""
    known = [entry + base for entry, base in members if entry is not None and base is not None]
    return None if not known else round(max(known), 3)


def own_steps(rule, height, base, riser, bands):
    """R341. The steps of a height: the base and the risers, half a riser over the base no step, or the step bands of the profile."""
    height = round(height, 6)
    if rule == "stepsFromHeight":
        return 1 + max(0, math.ceil(round((height - base) / riser, 6) - 0.5))
    if rule == "stepBands":
        return next((steps for limit, steps in bands if height < limit), None)
    return None


def own_down_to_the_cm(level):
    """R341. The FFL down to the cm, the Prototype-FFL parameter."""
    return (int(round(level * 1000)) // 10) / 100.0


def own_walls(pattern, count):
    """R341. The walls a split pattern names in a cluster of count units."""
    if pattern in (None, "none"):
        return []
    if pattern == "middle":
        return [count // 2] if count % 2 == 0 else []
    return sorted({int(part) for part in str(pattern)[len("walls "):].split(",") if 0 < int(part) < count})


def level_verdicts(rows, road, shapes, units, report, profile, lp, block, reads):
    """R341. The own verdict, True for holds, of every level decision of R339 per key and plot, under the candidate the first method
    coded, on the levels a build holds: road gives per plot the own El @ Entry of the rule and the own road of the front, shapes the
    own row order of each cluster and the own row partners, units the code of each plot, reads the level parameters of the key reads."""
    group = block["check"]["group"]
    decisions = {item["decision"].split(" ", 1)[1]: item for item in report["decisions"] if item["decision"].startswith(group + " ")}
    limit = block["levelPassMm"]
    within = lambda guess, aim: guess is not None and aim is not None and abs(guess - aim) * 1000.0 <= limit + 1e-6
    table_of = lambda word, label: {key.split(" | ", 1)[1]: value for key, value in ((decisions.get(word) or {}).get("tests", {}).get(label, {}).get("rule") or {}).items()}
    profile_base = lambda code: None if code is None else profile["steps"]["base_height"].get(code, profile["steps"]["base_height"]["default"]) / 1000.0
    learned_base = table_of("baseHeight", "typology") if (decisions.get("baseHeight") or {}).get("coded") == "typology" else {}
    base = lambda code: float(learned_base[code]) if code in learned_base else profile_base(code)
    split = table_of("levelSplit", "units") if (decisions.get("levelSplit") or {}).get("coded") == "units" else {}
    ffl_rule = (decisions.get("ffl") or {}).get("coded")
    riser = profile["steps"]["riser_added"] / 1000.0
    mine = {(row["key"], row["plot"]): row for row in rows if row.get("source") == group}
    out = lambda row, word: word in set(block["kindLeaveOut"].get(row.get("kind") or "", []))
    key_entry = {pair: entry_of(row, block) for pair, row in mine.items() if not out(row, "entry")}
    order, partners = shapes["order"], shapes["partners"]

    def built_entry(key, plot):
        rule = (road.get(plot) or {}).get("entry")
        aim = key_entry.get((key, plot))
        return None if rule is None else rule if aim is None or within(rule, aim) else aim

    def piece(plot, rule):
        ordered = order.get(plot)
        if rule == "ownEntryPlusBase":
            return [plot]
        if ordered:
            if rule == "oneFflPerClusterAtTheHighestEntry":
                return list(ordered)
            start = 0
            for wall in own_walls(split.get(" ".join(str(units.get(other)) for other in ordered)), len(ordered)) + [len(ordered)]:
                if plot in ordered[start:wall]:
                    return list(ordered[start:wall])
                start = wall
        other = partners.get(plot)
        return sorted([plot, other], key=lambda number: (len(number), number)) if other and partners.get(other) == plot else [plot]

    def rule_ffl(plot):
        """The FFL of the piece on the El @ Entry of the rule of each plot, which an entryM override never moves."""
        return own_ffl([((road.get(other) or {}).get("entry"), base(units.get(other))) for other in piece(plot, ffl_rule)])

    def built_ffl(key, plot, row):
        guess = rule_ffl(plot)
        aim = number(row.get("ffl"))
        return None if guess is None else guess if aim is None or within(guess, aim) else aim
    found = defaultdict(dict)
    for (key, plot), row in mine.items():
        verdict = found[(key, plot)]
        if "entry" in decisions and key_entry.get((key, plot)) is not None:
            verdict["entry"] = within((road.get(plot) or {}).get("entry"), key_entry[(key, plot)])
        if "rampFront" in decisions and number(row.get("rampFront")) is not None and not out(row, "rampFront"):
            verdict["rampFront"] = within((road.get(plot) or {}).get("front"), number(row["rampFront"]))
        aim = number(row.get("ffl"))
        if "ffl" in decisions and aim is not None and not out(row, "ffl"):
            verdict["ffl"] = within(rule_ffl(plot), aim)
        level, entry = built_ffl(key, plot, row), built_entry(key, plot)
        if "steps" in decisions and row.get("steps") not in (None, "") and not out(row, "steps"):
            coded = decisions["steps"]["coded"]
            if coded == "typology":
                guess = learned(decisions["steps"]["tests"]["typology"]["rule"], group, units.get(plot))
            else:
                guess = None if level is None or entry is None else own_steps(coded, round(level - entry, 4), base(units.get(plot)), riser, lp.get("stepBands") or [])
            verdict["steps"] = guess is not None and str(guess) == str(int(float(row["steps"])))
        for name in block["parameterLevels"]:
            word, value = "parameter " + name, number((reads.get((key, plot)) or {}).get(name))
            if word not in decisions or value is None or out(row, "parameter") or out(row, word):
                continue
            coded = decisions[word]["coded"]
            guess = (own_down_to_the_cm(level) if coded == "fflDownToTheCm" and level is not None else level if coded == "ffl" else
                     number(row.get("genericTop")) if coded == "genericTop" else None)
            verdict[word] = within(guess, value)
    # The split of each own cluster of three or more units the key holds at every FFL, held on its first plot in row order
    if "levelSplit" in decisions:
        for key in sorted({key for key, _ in mine}):
            for ordered in sorted({tuple(found_order) for plot, found_order in order.items() if len(found_order) >= 3}):
                rows_of = [mine.get((key, plot)) for plot in ordered]
                if any(row is None or out(row, "levelSplit") or number(row.get("ffl")) is None for row in rows_of):
                    continue
                levels = [round(number(row["ffl"]), 3) for row in rows_of]
                walls = [index + 1 for index in range(len(levels) - 1) if abs(levels[index] - levels[index + 1]) > 0.0005]
                pattern = "none" if not walls else "middle" if len(levels) % 2 == 0 and walls == [len(levels) // 2] else "walls " + ",".join(str(wall) for wall in walls)
                coded = decisions["levelSplit"]["coded"]
                guess = "none" if coded == "none" else split.get(" ".join(str(units.get(plot)) for plot in ordered))
                found[(key, ordered[0])]["levelSplit"] = guess == pattern
    # The base of the plot that sets the FFL of each piece the key holds at one FFL, held on that plot
    if "baseHeight" in decisions:
        seen = set()
        for (key, plot), row in mine.items():
            members = piece(plot, "oneFflPerPieceAtTheHighestEntry")
            if (key, tuple(members)) in seen or out(row, "baseHeight"):
                continue
            seen.add((key, tuple(members)))
            rows_of = [mine.get((key, other)) for other in members]
            if any(other is None or key_entry.get((key, other["plot"])) is None or number(other.get("ffl")) is None for other in rows_of):
                continue
            if len({round(number(other["ffl"]), 3) for other in rows_of}) != 1:
                continue
            top = max(rows_of, key=lambda other: key_entry[(key, other["plot"])] + profile_base(units.get(other["plot"])))
            coded = decisions["baseHeight"]["coded"]
            guess = profile_base(units.get(top["plot"])) if coded == "profile" else learned_base.get(units.get(top["plot"]))
            found[(key, top["plot"])]["baseHeight"] = within(None if guess is None else float(guess), round(number(top["ffl"]) - key_entry[(key, top["plot"])], 4))
    return {pair: verdict for pair, verdict in found.items() if verdict}


def level_inputs(ctx, settings, profile, rows, folder):
    """R341. What level_verdicts reads, by the own routes: the own road levels at the two own front corners of every plot the levels
    need, on the surfaces of siteBuild.roadSurfaces read from folder in a box around them, El @ Entry of the rule of the level profile
    of the group, the own row order of each own cluster and the own row partners of the units in no cluster."""
    block = settings["majority"]
    group = block["check"]["group"]
    lp = next(found for found in profile["levelProfiles"].values() if isinstance(found, dict) and found.get("keyRules") and found.get("job") == group)
    units = {plot: row.get("typologyRev9") or None for plot, row in ctx["book"].items()}
    # The own row order of each own cluster, along the own front of its first plot with one
    order = {}
    for cluster in ctx["clusters"]:
        if not all(plot in ctx["faces"] for plot in cluster):
            continue
        frame = next((ctx["frame"](plot)[0] for plot in cluster if ctx["frame"](plot)[0] is not None), None)
        if frame is None:
            continue
        ordered = tuple(sorted(cluster, key=lambda plot: shoelace_centre(ctx["faces"][plot])[0] * frame["r"][0] + shoelace_centre(ctx["faces"][plot])[1] * frame["r"][1]))
        for plot in cluster:
            order[plot] = ordered
    counted = lambda plot: ctx["kind"](plot) in block["pairKinds"] and plot not in ctx["member"] and plot.isdigit()
    sides = {}
    plots = sorted({row["plot"] for row in rows if row.get("source") == group})
    for plot in [plot for plot in plots if counted(plot)] + [other for plot in plots if counted(plot) for side in ("left", "right") for other in ctx["sides"](plot)[side] if counted(other)]:
        if plot not in sides:
            sides[plot] = own_pair_side(plot, row_sides(plot, ctx, counted), counted)
    partners = {}
    for plot, side in sides.items():
        if side in ("left", "right"):
            other_side = "right" if side == "left" else "left"
            beside = [other for other in ctx["sides"](plot)[other_side] if counted(other) and sides.get(other, own_pair_side(other, row_sides(other, ctx, counted), counted)) == other_side]
            if len(beside) == 1:
                partners[plot] = beside[0]
    needed = set(plots) | {plot for ordered in order.values() for plot in ordered} | set(partners.values())
    corners = {}
    for plot in needed:
        frame = ctx["frame"](plot)[0]
        if frame is not None and units.get(plot):
            corners[plot] = (frame["left"], frame["right"])
    points = [point for pair in corners.values() for point in pair]
    box = (min(x for x, _ in points), min(y for _, y in points), max(x for x, _ in points), max(y for _, y in points)) if points else None
    tins = [(name, OwnTin(Path(folder) / (name + ".xml"), box)) for name in settings["siteBuild"]["roadSurfaces"]]
    snap = settings["siteBuild"]["snapM"]
    setbacks = profile["ramp"]["front_setback"]
    road = {}
    for plot, pair in corners.items():
        levels = [own_road(tins, point, snap) for point in pair]
        if any(level is None for level in levels):
            road[plot] = {"entry": None, "front": None}
            continue
        code = units[plot]
        front = (levels[0] + levels[1]) / 2.0
        slope = lp["rampSlopePercent"].get(code, lp["rampSlopePercent"]["default"])
        road[plot] = {"front": front, "entry": round(front + lp["frontRoad"]["offsetM"] + slope / 100.0 * setbacks.get(code, setbacks["default"]) / 1000.0, 4)}
    return road, {"order": order, "partners": partners}, units, lp


def merged_check(merged, dry, table, keep, later, earlier=None):
    """R330. The list a run merged: every entry on a plot past_all.csv gives its key, and per job the entries of the fields it does
    not keep equal to the entries of the dry list of that run, as JSON text, so a kept entry of a cut plot is found. With earlier,
    the list before the run, every entry it keeps on the join, native whole, is in the merged list cell for cell, and every entry
    marked "byHand": true is. ok when all hold, else the run fails."""
    joined = {(row["key"], row["plot"]) for row in table}
    on_join = lambda item: (item.get("key"), str(item["plot"])) in joined
    off = [{name: item.get(name) for name in ("key", "plot", "field", "source")} for item in merged if not on_join(item)]
    kept = lambda item: item["source"] in keep and (keep[item["source"]] is None or item["field"] in keep[item["source"]])
    text = lambda items: Counter(json.dumps(item, sort_keys=True, ensure_ascii=False) for item in items)
    mine, theirs = text(item for item in merged if not kept(item)), text(item for item in dry if not kept(item))
    found = {"entries": len(merged), "offJoin": off, "runEntriesEqualDry": mine == theirs, "onlyMerged": sum((mine - theirs).values()), "onlyDry": sum((theirs - mine).values()),
             "bySource": dict(sorted(Counter(item["source"] for item in merged).items())), "byHand": sum(1 for item in merged if item.get("byHand") is True),
             "laterFieldsInRun": sorted({item["field"] for item in dry if item["field"] in later})}
    ok = not off and found["runEntriesEqualDry"]
    if earlier is not None:
        held = text(merged)
        wanted = [item for item in earlier if kept(item) and on_join(item)]
        missing = text(wanted) - held
        hands = text(item for item in earlier if item.get("byHand") is True) - held
        by_text = {json.dumps(item, sort_keys=True, ensure_ascii=False): item for item in earlier}
        found.update({"earlierKept": len(wanted), "keptMissing": sum(missing.values()),
                      "keptMissingBySource": dict(sorted(Counter(by_text[entry]["source"] for entry in missing.elements()).items())),
                      "keptMissingFirst": [{name: by_text[entry].get(name) for name in ("key", "plot", "field", "source")} for entry in list(missing)[:10]],
                      "byHandLost": sum(hands.values())})
        ok = ok and not missing and not hands
    found["ok"] = ok
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method of Round ADDIN 3a (R336).")
    parser.add_argument("--settings", default=str(ROOT / "config" / "project.json"))
    parser.add_argument("--past-all", required=True)
    parser.add_argument("--majority", required=True, help="majority.json of the run")
    parser.add_argument("--overrides", required=True, help="the override list the dry run wrote, every miss of the run")
    parser.add_argument("--merged", help="the override list the write run merged, config/overrides, held against the dry one and the join")
    parser.add_argument("--earlier", help="R330. the override list before the write run, its kept and byHand entries held in --merged cell for cell")
    parser.add_argument("--plots-csv", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--roads", required=True, help="folder with pieces.json")
    parser.add_argument("--sales", required=True)
    parser.add_argument("--analysis", required=True, help="analysis.json of the CAD analysis, its clusters")
    parser.add_argument("--footprint-blocks", required=True, help="blocks.json of the Building Footprint drawing")
    parser.add_argument("--key-read", nargs=2, action="append", metavar=("KEY", "FILE"), default=[])
    parser.add_argument("--key-plan", nargs=2, action="append", metavar=("KEY", "FILE"), default=[])
    parser.add_argument("--out", required=True)
    parser.add_argument("--landxml", help="R341. the landxml folder of the snapshot, the road surfaces the levels of R339 are held on")
    args = parser.parse_args(argv)
    if Path(args.out).resolve().is_relative_to(ROOT):
        print("key_rules_check: write outside the repo", file=sys.stderr)
        return 2
    if args.merged and not args.earlier:
        print("key_rules_check: --merged is held against the list before the run, give --earlier", file=sys.stderr)
        return 2
    settings = read_json(args.settings)
    block = settings["majority"]
    group = block["check"]["group"]
    profile = read_json(ROOT / settings["profile"])
    check = block["check"]
    report = read_json(args.majority)
    dry_list = read_json(args.overrides)
    dry = dry_list["overrides"]
    misses = defaultdict(set, first_misses(dry_entries(dry_list), report))
    with open(args.past_all, encoding="utf-8") as handle:
        table = list(csv.DictReader(handle))
    rows = [row for row in table if row["source"] == group and row["x"] and row["rotationDeg"] and row["mirrored"] in ("True", "False")]
    ctx = plot_context(args, settings, profile, report, table)
    # R341. The levels of R339 are held on the road surfaces a build asks, read here again from the LandXML text
    if any("passMm" in decision for decision in report["decisions"] if decision["decision"].startswith(group + " ")):
        if not args.landxml:
            print("key_rules_check: the report holds the levels of R339, give --landxml to hold them on the road a build asks (R341)", file=sys.stderr)
            return 2
        reads = ctx["readParameters"]
        road, shapes, units, lp = level_inputs(ctx, settings, profile, table, args.landxml)
        ctx["levels"] = level_verdicts(table, road, shapes, units, report, profile, lp, block, reads)
        ctx["levelRoads"] = {"plots": len(road), "noRoad": sum(1 for value in road.values() if value["entry"] is None)}
    # R275. The datum of each key per surface, over the rows the ramp front decision holds
    fronts = [row for row in table if row["source"] == group and number(row.get("rampFront")) is not None and "rampFront" not in block["kindLeaveOut"].get(row.get("kind") or "", ())]
    surfaces = {ROAD} | {column[len("level_"):-len("_left")] for column in next(iter(ctx["book"].values())) if column.startswith("level_") and column.endswith("_left")}
    datums = {surface: key_datum(fronts, ctx, surface, lambda row: number(row.get("rampFront"))) for surface in surfaces}
    plots = sorted(rows, key=lambda row: (row["key"], int(row["plot"])))
    samples = {str(seed): sorted(random.Random(seed).sample(plots, check["sample"]), key=lambda row: (row["key"], int(row["plot"]))) for seed in check["seeds"]}
    # Every plot as well as the samples, so a rare miss no sample draws is still held
    samples["every"] = plots
    # frontCorner writes no override, its plots are held against the entries of the link rotation, the same rule on the corner plots
    coded = {item["decision"]: item["coded"] for item in report["decisions"]}
    if coded.get(group + " frontCorner") == coded.get(group + " linkRotation"):
        misses[group + " frontCorner"] = misses[group + " linkRotation"]
    for item in (report.get("frontWalled") or {}).get(group) or []:
        key, plot = item.split(" ")
        misses[group + " frontOpen"].add((key, plot))
    # R341. The level decisions that write no override, the split and the base, list their misses in levelMisses
    for decision, found in (report.get("levelMisses") or {}).items():
        misses[decision] |= {tuple(item.split(" ")) for item in found}
    verdicts, seeds, cannot = {}, {}, Counter()
    for seed, sample in samples.items():
        tally, mismatches = Counter(), []
        for row in sample:
            if (row["key"], row["plot"]) not in verdicts:
                verdicts[(row["key"], row["plot"])] = held(row, ctx, settings, profile, report, datums)
            found, unknown, rule = verdicts[(row["key"], row["plot"])]
            if seed == "every":
                cannot.update(unknown)
            for decision, holds in sorted(found.items()):
                own = "holds" if holds else "misses"
                theirs = "misses" if (row["key"], row["plot"]) in misses.get(group + " " + decision, set()) else "holds"
                tally[decision + (" match" if own == theirs else " mismatch")] += 1
                if own != theirs:
                    mismatches.append({"key": row["key"], "plot": row["plot"], "decision": decision, "own": own, "first": theirs, "rule": rule})
        seeds[seed] = {"plots": len(sample) if seed == "every" else [row["key"] + " " + row["plot"] for row in sample], "counts": dict(sorted(tally.items())),
                       "mismatches": mismatches}
    # R335. The walls are drawn and held apart from the plots, a wall has an id and no plot of its own
    walls = wall_verdicts(ctx, settings, profile, report)
    wall_misses = {(item["key"], item["elementId"], item["decision"].split(" ", 1)[1]) for item in (report.get("wallMisses") or {}).get(group) or []}
    ordered = sorted(walls, key=lambda pair: (pair[0], pair[1]))
    wall_samples = {str(seed): sorted(random.Random(seed).sample(ordered, min(check["sample"], len(ordered)))) for seed in check["seeds"]}
    wall_samples["every"] = ordered
    for seed, sample in wall_samples.items():
        tally, mismatches = Counter(), []
        for pair in sample:
            for decision, holds in sorted(walls[pair].items()):
                if holds is None:
                    if seed == "every":
                        cannot[decision] += 1
                    continue
                own = "holds" if holds else "misses"
                theirs = "misses" if (pair[0], pair[1], decision) in wall_misses else "holds"
                tally[decision + (" match" if own == theirs else " mismatch")] += 1
                if own != theirs:
                    mismatches.append({"key": pair[0], "wall": pair[1], "decision": decision, "own": own, "first": theirs})
        seeds["walls " + seed] = {"walls": len(sample), "counts": dict(sorted(tally.items())), "mismatches": mismatches}
    no_base = {(item["key"], item["elementId"]) for item in ctx.get("noBaseLevel") or []}
    first_no_base = {(item["key"], item["elementId"]) for item in (report.get("wallsNoBaseLevel") or {}).get(group) or []}
    site = site_check(dict(args.key_read), block["groups"], block["siteReference"], report.get("siteFacts") or {})
    totals = {}
    for decision in report["decisions"]:
        if decision["decision"].startswith(group + " "):
            found = decision["tests"][decision["coded"]]
            by_key = sum(sum(counts.values()) for counts in found["byKey"].values())
            totals[decision["decision"]] = {"plots": decision["plots"], "byKeySum": by_key,
                                            "match": by_key == decision["plots"] == found["holds"] + found["breaks"] + found["cannot"]}
    # A decision of the group no route held is a failure, unless its field is left to a later phase
    later = set((block.get("overrideKeep") or {}).get("laterFields", {}).get(group) or ())
    routed = {decision for seed in seeds.values() for name in seed["counts"] for decision in [name.rsplit(" ", 1)[0]]}
    left_for_later, unchecked = [], []
    for decision in report["decisions"]:
        name = decision["decision"]
        if not name.startswith(group + " ") or name.split(" ", 1)[1] in routed or name.split(" ", 1)[1] in cannot:
            continue
        (left_for_later if decision.get("field") in later else unchecked).append(name.split(" ", 1)[1])
    failures = sorted(set(cannot) | set(unchecked))
    out = {"group": group, "seeds": seeds, "site": site, "totals": totals, "cannotCheck": {name: cannot.get(name, 0) for name in failures},
           "leftForLater": sorted(left_for_later), "wallsNoBaseLevel": {"own": len(no_base), "first": len(first_no_base), "match": no_base == first_no_base},
           "fits": {"linkOrigin": len(ctx["fits"]), "yardOutline": len(ctx["refit"]), "clusters": len(ctx["clusters"])}, "levelRoads": ctx.get("levelRoads")}
    if args.merged:
        keep = {source: None for source in block["overrideKeep"]["sources"]}
        for source, fields in block["overrideKeep"]["laterFields"].items():
            if keep.get(source, set()) is not None:
                keep[source] = set(keep.get(source) or ()) | set(fields)
        merged_list = read_json(args.merged)
        out["merged"] = merged_check(merged_list["overrides"], dry, table, keep, later, read_json(args.earlier)["overrides"])
        # R272. The misses with no value of the run are in the cannot list, those of the overrides with no value counted by source
        out["merged"].update(cannot_check(merged_list.get("cannot") or [], dry_list.get("cannot") or [], keep))
        out["merged"]["noValueBySource"] = dict(sorted(Counter(item["source"] for item in merged_list["overrides"] if item.get("value") is None).items()))
        out["merged"]["ok"] = out["merged"]["ok"] and out["merged"]["cannotEqualDry"]
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(out, indent=1, ensure_ascii=False), encoding="utf-8")
    for seed, found in seeds.items():
        print("seed %s: %s, mismatches %d" % (seed, found["counts"], len(found["mismatches"])))
    print("site: " + json.dumps({name: {key: entry["match"] for key, entry in keys.items()} for name, keys in site.items()}))
    print("totals: %d of %d decisions count every plot once" % (sum(1 for value in totals.values() if value["match"]), len(totals)))
    print("fitted here: %s, walls with no base level %s" % (out["fits"], out["wallsNoBaseLevel"]))
    print("left for a later phase: %s" % out["leftForLater"])
    if "merged" in out:
        print("merged list: %s" % ({name: value for name, value in out["merged"].items() if name != "offJoin"} | {"offJoin": len(out["merged"]["offJoin"])}))
    if failures:
        print("cannot check, a failure: %s" % out["cannotCheck"], file=sys.stderr)
        return 1
    if "merged" in out and not out["merged"]["ok"]:
        print("the merged list fails its check, a failure: off the join %d, run entries equal the dry list %s, kept entries missing %s, byHand entries lost %s, "
              "cannot equal the dry list %s" % (len(out["merged"]["offJoin"]), out["merged"]["runEntriesEqualDry"], out["merged"].get("keptMissing"),
                                                out["merged"].get("byHandLost"), out["merged"].get("cannotEqualDry")), file=sys.stderr)
        return 1
    return 0



if __name__ == "__main__":
    sys.exit(main())
