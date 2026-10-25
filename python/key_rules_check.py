"""R336. Second method of Round ADDIN 3a: the rules measured on the Parsons keys read again by another route.

json, csv, openpyxl for the sales sheet and plain math only, no shapely and none of the functions that made the rules. For each
seed of check.seeds, check.sample plots are drawn in plot order from every Parsons plot of past_all.csv with a link, never from
the misses of the first method, and every such plot is held as well. Each plot is held again and its verdict, holds or misses, compared with the verdict of the
first method, which is a miss when the override list of the run names the plot under that decision, else holds.

- coordinates (R331): each key read by json, its points, angle, project position and levels held against the reference key of
  its group with its own sums, and the keys that differ compared with siteFacts of majority.json
- link rotation (R332): the front edge of the baseplan face by its own walk of the edges, a corner plot by the Orientation of
  the sales sheet read here, the rotation the edge gives with the mirror flag of the key, held within rotationHoldDeg
- link origin (R332): the origin the refit of linkFits in majority.json gives on that edge, held within linkHoldMm
- ramp type (R334): the class of the ramp type of the key, else the band of its slope, against the class the coded table of
  majority.json gives its code
- ramp outline (R334): the outline of R127 on the own front edge with the setback of the profile, each corner against its own
  nearest corner of the key ramp within outlineHoldMm
- yard (R334): the generic floor top less the yard top of past_all.csv against the coded table of majority.json by zone, and
  the yard outline of the key plan in link axes by its own sums against the refit outline of its unit, mirror and width,
  the largest distance of a vertex of either to an edge of the other within outlineHoldMm
- hands (R333): side neighbours by the length of the edges two faces of plots.json share within 5 cm, left or right along the
  own front, the open end, the side of a second road edge, the cluster side in the clusters majority.json lists and the pair of
  the row of units in no cluster counted from its right end, each looked up in the table the first method learned for the file
  of the link
- parameters (R335): the value of each shared parameter, from past_all.csv or the key read by json, against the factor table
  or the formula the first method coded, a level parameter against the level of the plot within levelHoldMm
- walls (R335): each boundary wall of the TU key plans, its base and top against the FFL of its plots and its line by the own
  distance of its middle to the face edges, check.sample walls at each seed and every wall, against wallMisses of the report,
  and the front each plot leaves open by its own walk of the walls along the own front, against frontWalled
"""
import argparse
import csv
import json
import math
import random
import re
import sys
from collections import Counter
from pathlib import Path

import openpyxl

ROOT = Path(__file__).resolve().parent.parent
POINT_FIELDS = (("surveyPoint", "position"), ("surveyPoint", "sharedPosition"), ("basePoint", "position"), ("basePoint", "sharedPosition"))
OWN_FIELDS = {name + "." + field for name, field in POINT_FIELDS} | {"angleToTrueNorthDeg", "projectPosition"}
QUADRANTS = {"EN": (0.0, 90.0), "SE": (90.0, 180.0), "WS": (180.0, 270.0), "NW": (270.0, 360.0)}


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def shoelace_centre(ring):
    area, cx, cy = 0.0, 0.0, 0.0
    for index, (x1, y1) in enumerate(ring):
        x2, y2 = ring[(index + 1) % len(ring)]
        cross = x1 * y2 - x2 * y1
        area += cross
        cx += (x1 + x2) * cross
        cy += (y1 + y2) * cross
    area /= 2.0
    return (cx / (6.0 * area), cy / (6.0 * area)) if area else (sum(x for x, _ in ring) / len(ring), sum(y for _, y in ring) / len(ring))


def inside(point, ring):
    """Even odd ray test."""
    x, y = point
    hit = False
    for index, (x1, y1) in enumerate(ring):
        x2, y2 = ring[(index + 1) % len(ring)]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


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


def own_frame(ring, curbs, limits, corner_m, orientation):
    """The front of a face: the edge within limits whose middle is nearest a curb, and on a face with two or more edges whose
    middle lies within corner_m of a curb the one whose outward normal bears into the quadrant of its Orientation, nearest the
    middle of it. None when such a corner face has no Orientation. Returns the frame and the rule."""
    centre = shoelace_centre(ring)
    edges = [(ring[index], ring[(index + 1) % len(ring)]) for index in range(len(ring))]
    near = []
    for first, second in edges:
        middle = ((first[0] + second[0]) / 2.0, (first[1] + second[1]) / 2.0)
        near.append((curb_distance(middle, curbs), first, second))
    candidates = sorted((item for item in near if limits[0] <= math.dist(item[1], item[2]) < limits[1]), key=lambda item: item[0])
    curb_edge = candidates[0] if candidates else None
    roads = [item for item in near if item[0] <= corner_m]
    if len(roads) < 2:
        return (None if curb_edge is None else edge_frame(ring, curb_edge[1], curb_edge[2], centre)), "nearestCurb"
    quadrant = QUADRANTS.get(orientation or "")
    if quadrant is None:
        return None, "noOrientation"
    scored = []
    for _, first, second in roads:
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


def own_origin(frame, mirrored, fit):
    axis_x, axis_y = link_axes(frame, mirrored)
    start, end = (frame["right"], frame["left"]) if mirrored else (frame["left"], frame["right"])
    corner = start if fit["anchor"] == "start" else end
    return (corner[0] + fit["offset"][0] * axis_x[0] + fit["offset"][1] * axis_y[0], corner[1] + fit["offset"][0] * axis_x[1] + fit["offset"][1] * axis_y[1])


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
    """The Orientation of each plot of the sales sheet, the header the first row that names the plot column."""
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
            found[number.replace(".0", "")] = item.get("Orientation")
    return found


def site_check(reads, groups, references, first):
    """R336. The coordinates again: per group each read against the reference key of its group, the gap of each point in mm."""
    found = {}
    for group, reference in references.items():
        mine = {key: read_json(path) for key, path in reads.items() if key in groups.get(group, ())}
        if reference not in mine:
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
            if length and abs((after[0] - before[0]) * (here[1] - before[1]) - (after[1] - before[1]) * (here[0] - before[0])) / length <= tolerance                     and min(before[0], after[0]) - tolerance <= here[0] <= max(before[0], after[0]) + tolerance                     and min(before[1], after[1]) - tolerance <= here[1] <= max(before[1], after[1]) + tolerance:
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


def first_misses(overrides, decision):
    return {item["plot"] for item in overrides if item.get("decision") == decision}


def parameter_verdicts(row, ctx, settings, report):
    """R336. Each parameter decision of 3a held again on the row: the value the plot holds, from the parameters of past_all.csv or
    the key read, against the factor table or the formula the first method coded."""
    block = settings["majority"]
    decisions = {item["decision"]: item for item in report["decisions"]}
    values = json.loads(row.get("parameters") or "{}")
    read = ctx["readParameters"].get((row["key"], row["plot"])) or {}
    leave = set(block["kindLeaveOut"].get(row.get("kind") or "", []))
    facade = [str(values.get(name) or "") for name in block["linkNameFacade"]]
    own = {"all": "all", "typology": row["typologyRev9"], "typologyMirrored": row["typologyRev9"] + " " + row["mirrored"], "steps": row.get("steps") or None,
           "typologySteps": None if not row.get("steps") else row["typologyRev9"] + " " + row["steps"], "mirroredFactor": row["mirrored"]}
    formulas = {"plot": row["plot"], "rev9Code": row["typologyRev9"], "plotAndCode": row["plot"] + " | " + row["typologyRev9"],
                "plotCodeStepsAndFacade": " | ".join([row["plot"], row["typologyRev9"], row.get("steps") or "None"] + facade)}
    found = {}
    for name in block["parameterTests"]:
        decision = "parameter " + name
        coded = decisions.get("parsons " + decision)
        value = values.get(name)
        if coded is None or value in (None, "") or "parameter" in leave or decision in leave:
            continue
        if coded["coded"] in formulas:
            found[decision] = formulas[coded["coded"]] == str(value)
        elif coded["coded"] in own:
            found[decision] = learned(coded["tests"][coded["coded"]]["rule"], "parsons", own[coded["coded"]]) == str(value)
    for name in block["parameterLevels"]:
        decision = "parameter " + name
        coded = decisions.get("parsons " + decision)
        try:
            value = float(values.get(name) or read.get(name))
        except (TypeError, ValueError):
            continue
        if coded is None or "parameter" in leave or decision in leave:
            continue
        level = row.get(coded["coded"])
        found[decision] = bool(level) and abs(value - float(level)) * 1000.0 <= block["levelHoldMm"] + 1e-6
    return found


def wall_verdicts(ctx, settings, profile, report):
    """R336. Each boundary wall of the TU key plans held again: its base, top and line, by its own sums, keyed by key and wall id."""
    block = settings["majority"]
    coded = {item["decision"].split(" ", 1)[1]: item["coded"] for item in report["decisions"] if item["decision"].startswith("parsons wall")}
    depth = next((found.get("yardBelowFflM") for found in profile["levelProfiles"].values() if isinstance(found, dict) and found.get("job") == "parsons"), None)
    rise = profile["rev6"]["boundary_wall_top_above_ffl"] / 1000.0
    half = profile["plot"]["boundary_wall_thickness"] / 2000.0
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
            base = wall["baseOffset"] + levels.get(wall["baseConstraint"], 0.0)
            verdict = {}
            if coded.get("wallBase") == "yardLevelOfTheHigherPlot" and depth is not None:
                verdict["wallBase"] = abs(base - (max(ffls) - depth)) * 1000.0 <= block["levelHoldMm"] + 1e-6
            if coded.get("wallTop") == "higherFflPlusRev6":
                verdict["wallTop"] = abs(base + wall["heightM"] - (max(ffls) + rise)) * 1000.0 <= block["levelHoldMm"] + 1e-6
            rings = [ctx["faces"][plot] for plot in wall.get("plots") or [] if plot in ctx["faces"]]
            if coded.get("wallLine") == "centreOnTheEdge" and rings:
                middle = ((wall["start"][0] + wall["end"][0]) / 2.0, (wall["start"][1] + wall["end"][1]) / 2.0)
                distance = min(segment_distance(middle, a, b) for ring in rings for a, b in edges_of(ring))
                want = 0.0 if wall.get("locationLine") in block["wallCentreCodes"] else half
                verdict["wallLine"] = abs(distance - want) <= block["outlineHoldMm"] / 1000.0 + 1e-9
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


def plot_context(args, settings, profile, report):
    """Everything the own routes read, once: faces, curbs, Orientation, unit plots, side neighbours, clusters and key plans."""
    block = settings["majority"]
    with open(args.plots_csv, encoding="utf-8") as handle:
        book = {row["locationPlanNumber"]: row for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
    rings = {}
    for entry in read_json(args.baseplan)["plots"]:
        if entry.get("kind") == "plot_shape" and len(entry.get("vertices") or []) >= 3:
            rings.setdefault(str(entry["plot"]), []).append([(vertex[0], vertex[1]) for vertex in entry["vertices"]])
    faces = {number: straight(rings[row["baseplanLabel"]][0]) for number, row in book.items() if len(rings.get(row.get("baseplanLabel") or "", [])) == 1}
    curbs = curb_segments(read_json(Path(args.roads) / "pieces.json")["pieces"], settings["analysis"]["roadEdgeLayers"])
    sheet = read_json(ROOT / "python" / "c3d_probe.json")["dataBook"]["locationPlan"]["sales"]
    orientation = sales_orientation(args.sales, sheet["sheet"], sheet["columns"]["plot"])
    widths = [entry["plot_width_m"] for entry in profile["typologies"].values() if isinstance(entry.get("plot_width_m"), (int, float))]
    limits = (min(widths) - settings["analysis"]["toleranceM"], settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0)
    corner_m = settings["siteBuild"]["corner"]["roadEdgeM"]
    frames = {}

    def frame_of(plot):
        if plot not in frames:
            frames[plot] = (None, "noFace") if plot not in faces else own_frame(faces[plot], curbs, limits, corner_m, orientation.get(plot))
        return frames[plot]
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
    # A cluster gives its plots a side only when every plot is a plot of past_all.csv with a face and one has a front, as R271 gives them (R123)
    member = {plot: cluster for cluster in report.get("clusterPlots") or []
              if all(other in faces and other in args.table_plots for other in cluster) and any(frame_of(other)[0] is not None for other in cluster) for plot in cluster}
    kind = lambda plot: (profile["typologies"].get((book.get(plot) or {}).get("typologyRev9") or "") or {}).get("kind")
    full = {key: read_json(path) for key, path in args.key_plan}
    plans = {key: {entry["plot"]: entry for entry in plan["plots"]} for key, plan in full.items()}
    read_parameters = {}
    for key, path in args.key_read:
        for link in read_json(path)["documents"][0].get("links") or []:
            number = str((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number") or "").strip()
            if number:
                read_parameters[(key, number)] = None if (key, number) in read_parameters else link["parameters"]
    groups = ((report.get("yardOutlineGroups") or {}).get("parsons") or {}).get("refitLoops") or []
    refit = {(group["unit"], group["mirrored"], group["width"]): group["loops"] for group in groups}
    return {"book": book, "faces": faces, "frame": frame_of, "sides": sides_of, "member": member, "kind": kind, "plans": plans, "refit": refit,
            "step": settings["cadLinks"]["floorWidthStepM"], "curbs": curbs, "cornerM": corner_m, "fullPlans": full,
            "readParameters": {pair: found for pair, found in read_parameters.items() if found is not None}}


def cluster_side(plot, cluster, ctx):
    """The side of a plot in its cluster, the plots put left to right along the own front of the first plot of the cluster with one."""
    frame = next((ctx["frame"](other)[0] for other in cluster if ctx["frame"](other)[0] is not None), None)
    if frame is None or any(other not in ctx["faces"] for other in cluster):
        return None
    along = {other: shoelace_centre(ctx["faces"][other])[0] * frame["r"][0] + shoelace_centre(ctx["faces"][other])[1] * frame["r"][1] for other in cluster}
    ordered = sorted(cluster, key=lambda other: along[other])
    index, count = ordered.index(plot), len(ordered)
    return "left" if index < (count - 1) / 2 else "right" if index > (count - 1) / 2 else "middle"


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


def held(row, ctx, settings, profile, report):
    """R336. The own verdict, True for holds, of every decision of 3a that holds this plot, and the rule of its front."""
    block = settings["majority"]
    decisions = {item["decision"]: item for item in report["decisions"]}
    plot, mirrored = row["plot"], row["mirrored"] == "True"
    frame, rule = ctx["frame"](plot)
    found = {}
    found["linkRotation"] = frame is not None and turn_gap(angle_of(link_axes(frame, mirrored)[0]), float(row["rotationDeg"])) <= block["rotationHoldDeg"] + 1e-9
    fits = report["linkFits"]["parsons"]
    fit = fits.get(row["typologyRev9"]) or fits.get((profile["typologies"].get(row["typologyRev9"]) or {}).get("keo_file"))
    found["linkOrigin"] = (frame is not None and fit is not None
                           and math.dist(own_origin(frame, mirrored, fit), (float(row["x"]), float(row["y"]))) * 1000.0 <= block["linkHoldMm"] + 1e-6)
    tu = row.get("kind") != "SE"
    coded = decisions.get("parsons rampSlope")
    if tu and coded is not None and coded["coded"] == "typology":
        own_class = block["rampTypeClass"].get(row.get("rampType") or "")
        if own_class is None and row.get("rampSlopePercent"):
            slope = float(row["rampSlopePercent"])
            own_class = next((label for limit, label in block["slopeBands"] if slope < limit), block["slopeBands"][-1][1])
        if own_class is not None:
            found["rampSlope"] = learned(coded["tests"]["typology"]["rule"], "parsons", row["typologyRev9"]) == own_class
    if tu and row.get("rampCorners"):
        corners = [(corner[0], corner[1]) for corner in json.loads(row["rampCorners"])]
        if len(corners) == 4:
            setbacks = profile["ramp"]["front_setback"]
            depth = setbacks.get(row["typologyRev9"], setbacks["default"]) / 1000.0
            found["rampOutline"] = frame is not None and own_corner_gap(own_ramp(frame, depth), corners) <= block["outlineHoldMm"] / 1000.0 + 1e-9
    coded = decisions.get("parsons yard")
    if tu and coded is not None and coded["coded"] == "zone":
        top = row.get("genericTop") or row.get("ffl")
        depth = "noYard" if not row.get("yardTop") else None if not top else "%.3f" % (float(top) - float(row["yardTop"]))
        if depth is not None:
            found["yard"] = learned(coded["tests"]["zone"]["rule"], "parsons", row.get("zone")) == depth
    entry = (ctx["plans"].get(row["key"]) or {}).get(plot)
    yard = ((entry or {}).get("floors") or {}).get("yard")
    if tu and yard and frame is not None and ctx["refit"]:
        width = round(round(math.dist(frame["left"], frame["right"]) / ctx["step"]) * ctx["step"], 3)
        loops = [to_link(loop["points"], entry["insert"]["value"], entry["rotationDeg"]["value"], entry["mirrored"]["value"]) for loop in yard["outline"]["value"]["loops"]]
        group = ctx["refit"].get((entry["unit"]["value"], entry["mirrored"]["value"], width))
        found["yardOutline"] = group is not None and own_boundary_gap(group, loops) <= block["outlineHoldMm"] / 1000.0 + 1e-9
    kind = (profile["typologies"].get(row["typologyRev9"]) or {}).get("kind")
    unit_class = "villa" if kind == "villa" else "cluster" if plot in ctx["member"] else "alone"
    coded = decisions.get("parsons hand " + unit_class)
    if coded is not None:
        here = ctx["sides"](plot)
        mirror_block = {"yes": "yes", "no": "no"}.get((ctx["book"].get(plot) or {}).get("footprintMirrored"))
        open_end = None if frame is None else "both" if not here["left"] and not here["right"] else "left" if not here["left"] else "right" if not here["right"] else "none"
        value = None
        if coded["coded"] == "blockMirrorOpenEnd":
            value = None if mirror_block is None or open_end is None else mirror_block + " " + open_end
        elif coded["coded"] == "blockMirrorRoadSide":
            road = own_road_side(ctx["faces"][plot], ctx["curbs"], frame, ctx["cornerM"]) if plot in ctx["faces"] else None
            value = None if mirror_block is None or road is None else mirror_block + " " + road
        elif coded["coded"] == "clusterSide":
            value = cluster_side(plot, ctx["member"][plot], ctx)
        elif coded["coded"] == "rowPairSideOrBlockMirrorOpenEnd":
            counted = lambda other: ctx["kind"](other) in block["pairKinds"] and other not in ctx["member"]
            side = own_pair_side(plot, row_sides(plot, ctx, counted), counted)
            value = "pair " + side if side else None if mirror_block is None or open_end is None else "alone " + mirror_block + " " + open_end
        found["hand " + unit_class] = value is not None and learned(coded["tests"][coded["coded"]]["rule"], ("parsons", row["linkFile"]), value) == mirrored
    found.update(parameter_verdicts(row, ctx, settings, report))
    walls = [wall for wall in (ctx["fullPlans"].get(row["key"]) or {}).get("walls") or []
             if plot in (wall.get("plots") or []) and re.search(block["wallTypes"]["boundary"], wall.get("type") or "")]
    if tu and frame is not None and row["key"] in ctx["fullPlans"] and "parsons frontOpen" in decisions:
        found["frontOpen"] = own_front_cover(frame, walls, settings["keyPlans"]["wallEdgeM"]) <= block["frontOpenShare"] + 1e-9
    return found, rule


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method of Round ADDIN 3a (R336).")
    parser.add_argument("--settings", default=str(ROOT / "config" / "project.json"))
    parser.add_argument("--past-all", required=True)
    parser.add_argument("--majority", required=True, help="majority.json of the run")
    parser.add_argument("--overrides", required=True, help="the override list the run wrote")
    parser.add_argument("--plots-csv", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--roads", required=True, help="folder with pieces.json")
    parser.add_argument("--sales", required=True)
    parser.add_argument("--key-read", nargs=2, action="append", metavar=("KEY", "FILE"), default=[])
    parser.add_argument("--key-plan", nargs=2, action="append", metavar=("KEY", "FILE"), default=[])
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    if Path(args.out).resolve().is_relative_to(ROOT):
        print("key_rules_check: write outside the repo", file=sys.stderr)
        return 2
    settings = read_json(args.settings)
    block = settings["majority"]
    profile = read_json(ROOT / settings["profile"])
    check = block["check"]
    report = read_json(args.majority)
    misses = {}
    for item in read_json(args.overrides)["overrides"]:
        misses.setdefault(item.get("decision"), set()).add(item["plot"])
    with open(args.past_all, encoding="utf-8") as handle:
        table = list(csv.DictReader(handle))
    rows = [row for row in table if row["source"] == "parsons" and row["x"] and row["rotationDeg"] and row["mirrored"] in ("True", "False")]
    args.table_plots = {row["plot"] for row in table}
    ctx = plot_context(args, settings, profile, report)
    plots = sorted(rows, key=lambda row: (row["key"], int(row["plot"])))
    samples = {str(seed): sorted(random.Random(seed).sample(plots, check["sample"]), key=lambda row: (row["key"], int(row["plot"]))) for seed in check["seeds"]}
    # Every plot as well as the samples, so a rare miss no sample draws is still held
    samples["every"] = plots
    walled = {tuple(item.split(" ")) for item in (report.get("frontWalled") or {}).get("parsons") or []}
    for key, plot in walled:
        misses.setdefault("parsons frontOpen", set()).add(plot)
    verdicts, seeds = {}, {}
    for seed, sample in samples.items():
        tally, mismatches = Counter(), []
        for row in sample:
            if (row["key"], row["plot"]) not in verdicts:
                verdicts[(row["key"], row["plot"])] = held(row, ctx, settings, profile, report)
            found, rule = verdicts[(row["key"], row["plot"])]
            for decision, holds in sorted(found.items()):
                own = "holds" if holds else "misses"
                theirs = "misses" if row["plot"] in misses.get("parsons " + decision, set()) else "holds"
                tally[decision + (" match" if own == theirs else " mismatch")] += 1
                if own != theirs:
                    mismatches.append({"key": row["key"], "plot": row["plot"], "decision": decision, "own": own, "first": theirs, "rule": rule})
        seeds[seed] = {"plots": len(sample) if seed == "every" else [row["key"] + " " + row["plot"] for row in sample], "counts": dict(sorted(tally.items())),
                       "mismatches": mismatches}
    # R335. The walls are drawn and held apart from the plots, a wall has an id and no plot of its own
    walls = wall_verdicts(ctx, settings, profile, report)
    wall_misses = {(item["key"], item["elementId"], item["decision"].split(" ", 1)[1]) for item in (report.get("wallMisses") or {}).get("parsons") or []}
    ordered = sorted(walls, key=lambda pair: (pair[0], pair[1]))
    wall_samples = {str(seed): sorted(random.Random(seed).sample(ordered, min(check["sample"], len(ordered)))) for seed in check["seeds"]}
    wall_samples["every"] = ordered
    for seed, sample in wall_samples.items():
        tally, mismatches = Counter(), []
        for pair in sample:
            for decision, holds in sorted(walls[pair].items()):
                own = "holds" if holds else "misses"
                theirs = "misses" if (pair[0], pair[1], decision) in wall_misses else "holds"
                tally[decision + (" match" if own == theirs else " mismatch")] += 1
                if own != theirs:
                    mismatches.append({"key": pair[0], "wall": pair[1], "decision": decision, "own": own, "first": theirs})
        seeds["walls " + seed] = {"walls": len(sample), "counts": dict(sorted(tally.items())), "mismatches": mismatches}
    site = site_check(dict(args.key_read), block["groups"], block["siteReference"], report.get("siteFacts") or {})
    totals = {}
    for decision in report["decisions"]:
        if decision["decision"].startswith("parsons "):
            coded = decision["tests"][decision["coded"]]
            by_key = sum(sum(counts.values()) for counts in coded["byKey"].values())
            totals[decision["decision"]] = {"plots": decision["plots"], "byKeySum": by_key,
                                            "match": by_key == decision["plots"] == coded["holds"] + coded["breaks"] + coded["cannot"]}
    out = {"seeds": seeds, "site": site, "totals": totals}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(out, indent=1), encoding="utf-8")
    for seed, found in seeds.items():
        print("seed %s: %s, mismatches %d" % (seed, found["counts"], len(found["mismatches"])))
    print("site: " + json.dumps({group: {key: entry["match"] for key, entry in keys.items()} for group, keys in site.items()}))
    print("totals: %d of %d decisions count every plot once" % (sum(1 for value in totals.values() if value["match"]), len(totals)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
