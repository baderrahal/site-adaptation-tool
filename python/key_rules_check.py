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
"""
import argparse
import csv
import json
import math
import random
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


def first_misses(overrides, decision):
    return {item["plot"] for item in overrides if item.get("decision") == decision}


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
    overrides = read_json(args.overrides)["overrides"]
    with open(args.past_all, encoding="utf-8") as handle:
        rows = [row for row in csv.DictReader(handle) if row["source"] == "parsons" and row["x"] and row["rotationDeg"] and row["mirrored"] in ("True", "False")]
    with open(args.plots_csv, encoding="utf-8") as handle:
        label_of = {row["locationPlanNumber"]: row["baseplanLabel"] for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
    rings = {}
    for entry in read_json(args.baseplan)["plots"]:
        if entry.get("kind") == "plot_shape" and len(entry.get("vertices") or []) >= 3:
            rings.setdefault(str(entry["plot"]), []).append([(vertex[0], vertex[1]) for vertex in entry["vertices"]])
    curbs = curb_segments(read_json(Path(args.roads) / "pieces.json")["pieces"], settings["analysis"]["roadEdgeLayers"])
    sheet = read_json(ROOT / "python" / "c3d_probe.json")["dataBook"]["locationPlan"]["sales"]
    orientation = sales_orientation(args.sales, sheet["sheet"], sheet["columns"]["plot"])
    widths = [entry["plot_width_m"] for entry in profile["typologies"].values() if isinstance(entry.get("plot_width_m"), (int, float))]
    limits = (min(widths) - settings["analysis"]["toleranceM"], settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0)
    corner_m = settings["siteBuild"]["corner"]["roadEdgeM"]
    fits = report["linkFits"]["parsons"]
    rotation_misses = first_misses(overrides, "parsons linkRotation")
    origin_misses = first_misses(overrides, "parsons linkOrigin")
    plots = sorted(rows, key=lambda row: (row["key"], int(row["plot"])))
    samples = {str(seed): sorted(random.Random(seed).sample(plots, check["sample"]), key=lambda row: (row["key"], int(row["plot"]))) for seed in check["seeds"]}
    # Every plot as well as the samples, so a rare miss no sample draws is still held
    samples["every"] = plots
    seeds = {}
    for seed, sample in samples.items():
        tally, mismatches = Counter(), []
        for row in sample:
            label = label_of.get(row["plot"])
            faces = rings.get(label or "")
            frame, rule = (None, "noFace") if not faces or len(faces) != 1 else own_frame(faces[0], curbs, limits, corner_m, orientation.get(row["plot"]))
            mirrored = row["mirrored"] == "True"
            rotation = float(row["rotationDeg"])
            if frame is None:
                own = "misses"
            else:
                own = "holds" if turn_gap(angle_of(link_axes(frame, mirrored)[0]), rotation) <= block["rotationHoldDeg"] + 1e-9 else "misses"
            theirs = "misses" if row["plot"] in rotation_misses else "holds"
            tally["rotation " + ("match" if own == theirs else "mismatch")] += 1
            if own != theirs:
                mismatches.append({"key": row["key"], "plot": row["plot"], "decision": "linkRotation", "own": own, "first": theirs, "rule": rule})
            code = row["typologyRev9"]
            fit = fits.get(code) or fits.get((profile["typologies"].get(code) or {}).get("keo_file"))
            if frame is None or fit is None:
                own = "misses"
                gap = None
            else:
                predicted = own_origin(frame, mirrored, fit)
                gap = math.dist(predicted, (float(row["x"]), float(row["y"]))) * 1000.0
                own = "holds" if gap <= block["linkHoldMm"] + 1e-6 else "misses"
            theirs = "misses" if row["plot"] in origin_misses else "holds"
            tally["origin " + ("match" if own == theirs else "mismatch")] += 1
            if own != theirs:
                mismatches.append({"key": row["key"], "plot": row["plot"], "decision": "linkOrigin", "own": own, "first": theirs, "rule": rule,
                                   "gapMm": None if gap is None else round(gap, 1)})
        seeds[seed] = {"plots": len(sample) if seed == "every" else [row["key"] + " " + row["plot"] for row in sample], "counts": dict(sorted(tally.items())),
                       "mismatches": mismatches}
    groups = block["groups"]
    site = site_check(dict(args.key_read), groups, block["siteReference"], report.get("siteFacts") or {})
    totals = {}
    for decision in report["decisions"]:
        if decision["decision"].startswith("parsons "):
            coded = decision["tests"][decision["coded"]]
            by_key = sum(sum(counts.values()) for counts in coded["byKey"].values())
            totals[decision["decision"]] = {"plots": decision["plots"], "byKeySum": by_key, "match": by_key == decision["plots"] == coded["holds"] + coded["breaks"] + coded["cannot"]}
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
