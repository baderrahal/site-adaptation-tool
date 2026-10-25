"""R328. The second method of Round ADDIN Phase 2b: the plans of the answer keys (R325), their join and place check (R326), the area rows
and the spots at the plot entrance (R327), each read again by another route on 20 plots at seed 14 and 20 at seed 7, with json, csv and
plain math, no shapely, no tree and none of the functions that made them.

- plans: insert, rotation, mirror, FFL and El @ Entry of each plan plot worked out again from the link transform, the survey point and the
  generic floor of the read, the plan plot tied to the read by the plot number of the model it keeps (modelPlot)
- join: the generic floor of each plot by the text of its Plot parameter, its shoelace centroid, the faces of plots.json by ray casting and
  the location plan number and area key of the face from plots.csv, not labels.csv
- place: each ramp by its centroid the same way, each link by the house centre of its file learned on the other links of that file only
- spots: the entrance as the middle of the higher of the two long edges of the ramp, its corners put in order by their angle round
  their mean point, the spots within each radius and at those corners by plain distance
- areas: the joined plots of a key counted by the area key plots.csv gives their official number, against the area row, and the rows of
  past_all.csv of a key against the plots of its plan
Written to key_plans_check.json outside the repo, counts, plot numbers and element ids only.
"""
import argparse
import csv
import json
import math
import random
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import key_reads
import plan_from_past
import plot_relations
import project_profile
from common import write_json

OUTPUT = "key_plans_check.json"
SEEDS = (14, 7)
SAMPLE = 20
PLOT = "Plot"
SURVEY_BASE = "Survey Point"
NUMBERS_BAR = "|"
# Plan values are written to the mm and El @ Entry to a tenth of a mm by R2, so these are the rounding of two values
METRES = 0.0005
ENTRY = 0.00005
# A link axis is written to 3 decimals by R2, so a component is off by up to half a thousandth and the angle worked out from it by up to
# about 0.04 degrees, a little more is allowed
AXIS_DEGREES = 0.05
SLACK = 1e-7


def order(text):
    return (0, int(text)) if str(text).isdigit() else (1, str(text))


def sample(items, seed, size=SAMPLE):
    """R328. size of the items at the seed, drawn from them in plot order so the draw does not hang on the order they came in, in plot order."""
    items = sorted(items, key=order)
    picked = items if len(items) <= size else random.Random(seed).sample(items, size)
    return sorted(picked, key=order)


def inside(point, ring):
    """R328. The even odd ray test of a point in a ring of [x, y] points."""
    x, y = point[0], point[1]
    hit = False
    for index, first in enumerate(ring):
        second = ring[index - 1]
        if (first[1] > y) != (second[1] > y):
            cross = (second[0] - first[0]) * (y - first[1]) / (second[1] - first[1]) + first[0]
            if x < cross:
                hit = not hit
    return hit


def centroid(ring):
    """R328. The centroid of a ring by the shoelace sums, the mean of its points when it has no area."""
    area = cx = cy = 0.0
    for index, first in enumerate(ring):
        second = ring[(index + 1) % len(ring)]
        step = first[0] * second[1] - second[0] * first[1]
        area += step
        cx += (first[0] + second[0]) * step
        cy += (first[1] + second[1]) * step
    if abs(area) < 1e-12:
        return sum(point[0] for point in ring) / len(ring), sum(point[1] for point in ring) / len(ring)
    return cx / (3 * area), cy / (3 * area)


def largest_loop(floor):
    """The outline loop of R52 with the largest shoelace area, as [x, y] in shared coordinates, or None."""
    loops = [[point[:2] for point in loop] for loop in floor.get("outline") or [] if len(loop) >= 3]
    return max(loops, key=lambda loop: abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(loop, loop[1:] + loop[:1]))), default=None)


def plot_text(floor):
    """The plot of a floor from the text of its Plot parameter, the digits after the last dash, never the plotNumber field."""
    text = str((floor.get("parameters") or {}).get(PLOT) or "")
    number = text.rpartition("-")[2] if "-" in text else ""
    return str(int(number)) if number.isascii() and number.isdigit() else None


def of_kind(document, floors_config, kind):
    found = defaultdict(list)
    for floor in document.get("floors") or []:
        if floor.get("typeName") in floors_config["types"][kind]:
            plot = plot_text(floor)
            if plot is not None:
                found[plot].append(floor)
    return found


def survey(document):
    point = document["surveyPoint"]
    return [point["sharedPosition"][axis] - point["position"][axis] for axis in range(3)]


def faces_at(point, rings):
    return [label for label, shapes in rings.items() if any(inside(point, ring) for ring in shapes)]


def where(point, rings, rows):
    """R328. The status, face and number of a point by ray casting and plots.csv: noFace, twoFaces, noNumber or joined."""
    if point is None:
        return {"status": "noPlace", "face": None, "number": None}
    hits = faces_at(point, rings)
    if not hits:
        return {"status": "noFace", "face": None, "number": None}
    if len(hits) > 1:
        return {"status": "twoFaces", "face": None, "number": None}
    number = (rows.get(hits[0]) or {}).get("locationPlanNumber") or None
    # plots.csv joins the numbers of a face of several plan labels with a bar, such a face has no one number
    if number is not None and NUMBERS_BAR in number:
        number = None
    return {"status": "joined" if number else "noNumber", "face": hits[0], "number": number}


def status_class(status):
    """A face of no plan label and one of two have no number in plots.csv alike, so both are noNumber here."""
    return "noNumber" if status == "manyNumbers" else status


def mismatch(found, plot, field, first, second):
    if first != second:
        found.append({"plot": plot, "field": field, "first": first, "second": second})


def result(compared, wrong):
    return {"compared": compared, "match": compared - len({item["plot"] for item in wrong}), "mismatch": wrong}


def check_join(document, floors_config, join, rings, rows, plots):
    """R328. Each sampled plot of the join of R326 found again: status, face, number and the area key of plots.csv."""
    generics = of_kind(document, floors_config, "generic")
    wrong = []
    for plot in plots:
        first = join["plots"][plot]
        own = generics.get(plot, [])
        loop = largest_loop(own[0]) if len(own) == 1 else None
        if len(own) != 1:
            second = {"status": "noGenericFloor" if not own else "twoGenericFloors", "face": None, "number": None}
        else:
            second = where(centroid(loop) if loop else None, rings, rows)
        mismatch(wrong, plot, "status", status_class(first["status"]), second["status"])
        if first["status"] == "joined" and second["status"] == "joined":
            row = rows.get(second["face"]) or {}
            for field, value in (("face", second["face"]), ("number", second["number"]), ("areaNbh", row.get("nbhId")), ("areaZone", row.get("zone"))):
                mismatch(wrong, plot, field, first[field], value)
    return result(len(plots), wrong)


def house_centres(document, floors_config, leave_out):
    """R328. The house centre of the link leave_out from the median place of the generic floor centre in the axes of the other links of its
    file whose plot names one generic floor, so the link never teaches its own frame. None when no other link of its file does."""
    offset = survey(document)
    generics = of_kind(document, floors_config, "generic")
    places = []
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if link is leave_out or link.get("file") != leave_out.get("file") or len(generics.get(plot, [])) != 1 or not link.get("transform"):
            continue
        loop = largest_loop(generics[plot][0])
        if loop is None:
            continue
        cx, cy = centroid(loop)
        transform = link["transform"]
        dx, dy = cx - transform["origin"][0] - offset[0], cy - transform["origin"][1] - offset[1]
        places.append((dx * transform["axisX"][0] + dy * transform["axisX"][1], dx * transform["axisY"][0] + dy * transform["axisY"][1]))
    if not places or not leave_out.get("transform"):
        return None
    along, across = statistics.median(place[0] for place in places), statistics.median(place[1] for place in places)
    transform = leave_out["transform"]
    origin, x, y = transform["origin"], transform["axisX"], transform["axisY"]
    return origin[0] + offset[0] + along * x[0] + across * y[0], origin[1] + offset[1] + along * x[1] + across * y[1]


def check_place(document, floors_config, place, rings, rows, plots):
    """R328. The ramps and links of each sampled plot of the place check of R326 found again, verdict and number found."""
    first_of = {(item["kind"], item["elementId"]): item for item in place["mismatches"]}
    elements = [("ramp", floor, plot, centroid(largest_loop(floor)) if largest_loop(floor) else None)
                for plot, floors in of_kind(document, floors_config, "ramp").items() if plot in plots for floor in floors]
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if plot in plots:
            elements.append(("link", link, plot, house_centres(document, floors_config, link)))
    wrong = []
    for kind, element, plot, point in elements:
        found = where(point, rings, rows)
        verdict = "match" if found["status"] == "joined" and found["number"] == plot else ("otherNumber" if found["status"] == "joined" else found["status"])
        first = first_of.get((kind, element.get("elementId")))
        mismatch(wrong, plot, kind + " " + str(element.get("elementId")), status_class(first["verdict"]) if first else "match", verdict)
        if first and verdict == "otherNumber":
            mismatch(wrong, plot, kind + " " + str(element.get("elementId")) + " number", first["found"], found["number"])
    return {"compared": len(elements), "match": len(elements) - len({item["field"].split(" number")[0] for item in wrong}), "mismatch": wrong}


def axis_rotation_holds(rotation, axis):
    """R328. True when the rotation in degrees of the plan is the angle of the link X axis written to 3 decimals, within AXIS_DEGREES."""
    found = math.degrees(math.atan2(axis[1], axis[0]))
    return abs((rotation - found + 180.0) % 360.0 - 180.0) <= AXIS_DEGREES


def entrance_edge(corners):
    """R328. The two corners of the higher long edge of a ramp of four corners: the corners in order of their angle round their mean
    point, the pair of opposite edges with the larger total length, of them the edge with the larger mean level."""
    mx, my = sum(corner[0] for corner in corners) / 4, sum(corner[1] for corner in corners) / 4
    ring = sorted(corners, key=lambda corner: math.atan2(corner[1] - my, corner[0] - mx))
    edges = [(ring[index], ring[(index + 1) % 4]) for index in range(4)]
    length = [math.hypot(a[0] - b[0], a[1] - b[1]) for a, b in edges]
    pair = (edges[0], edges[2]) if length[0] + length[2] >= length[1] + length[3] else (edges[1], edges[3])
    return max(pair, key=lambda edge: edge[0][2] + edge[1][2])


def check_spots(document, floors_config, first, block, plots):
    """R328. The spots of each sampled plot of R327 found again: the entrance as the middle of the higher long edge of the ramp by
    entrance_edge, the spots within each radius and the spot of the ramp nearest each corner of that edge within the corner distance."""
    offset = survey(document)
    ramps = of_kind(document, floors_config, "ramp")
    spots = [(spot.get("elementId"), spot["origin"][0] + offset[0], spot["origin"][1] + offset[1], (spot.get("host") or {}).get("elementId"))
             for spot in document.get("spotElevations") or [] if isinstance(spot.get("value"), (int, float)) and spot.get("elevationBase") == SURVEY_BASE
             and isinstance(spot.get("origin"), list)]
    wrong = []
    for plot in plots:
        ramp = ramps[plot][0]
        corners = entrance_edge([[corner[axis] + offset[axis] for axis in range(3)] for corner in ramp["corners"]])
        mx, my = (corners[0][0] + corners[1][0]) / 2, (corners[0][1] + corners[1][1]) / 2
        for radius in block["radiiM"]:
            near = sorted(spot_id for spot_id, x, y, _ in spots if math.hypot(x - mx, y - my) <= radius + SLACK)
            mismatch(wrong, plot, "near " + str(radius), first["plots"][plot]["near"][str(radius)], near)
        pair = []
        for corner in corners:
            close = sorted((math.hypot(x - corner[0], y - corner[1]), spot_id) for spot_id, x, y, host in spots
                           if host == ramp.get("elementId") and math.hypot(x - corner[0], y - corner[1]) <= block["cornerM"] + SLACK)
            pair.append(close[0][1] if close else None)
        mismatch(wrong, plot, "corners", sorted(first["plots"][plot]["corners"], key=str), sorted(pair, key=str))
    return result(len(plots), wrong)


def recount_spots(document, floors_config, first, block, schedule=None):
    """R328. On every plot R327 compared, the plots with a spot within each radius of the entrance and with both corner spots whose value
    or mean is within 1 mm and within 5 mm of El @ Entry of the key and of the schedule, counted again by entrance_edge and plain
    distance, against the counts of R327. match is True only when every count is equal."""
    offset = survey(document)
    ramps, generics = of_kind(document, floors_config, "ramp"), of_kind(document, floors_config, "generic")
    spots = [(spot.get("elementId"), spot["origin"][0] + offset[0], spot["origin"][1] + offset[1], spot["value"], (spot.get("host") or {}).get("elementId"))
             for spot in document.get("spotElevations") or [] if isinstance(spot.get("value"), (int, float)) and spot.get("elevationBase") == SURVEY_BASE
             and isinstance(spot.get("origin"), list)]
    bands = (("within1mm", 0.001), ("within5mm", 0.005))
    counts = {source: defaultdict(Counter) for source in ("key", "schedule")}
    for plot in first["plots"]:
        ramp = ramps[plot][0]
        edge = entrance_edge([[corner[axis] + offset[axis] for axis in range(3)] for corner in ramp["corners"]])
        mx, my = (edge[0][0] + edge[1][0]) / 2, (edge[0][1] + edge[1][1]) / 2
        pair = []
        for corner in edge:
            close = sorted((math.hypot(x - corner[0], y - corner[1]), value) for _, x, y, value, host in spots
                           if host == ramp.get("elementId") and math.hypot(x - corner[0], y - corner[1]) <= block["cornerM"] + SLACK)
            pair.append(close[0][1] if close else None)
        own = generics.get(plot, [])
        key_value = (own[0].get("parameters") or {}).get(block["entryParameter"]) if len(own) == 1 else None
        for source, level in (("key", key_value), ("schedule", (schedule or {}).get(plot))):
            if not isinstance(level, (int, float)) or isinstance(level, bool):
                continue
            for radius in block["radiiM"]:
                values = [value for _, x, y, value, _ in spots if math.hypot(x - mx, y - my) <= radius + SLACK]
                for band, limit in bands:
                    counts[source]["radius " + str(radius)][band] += any(abs(value - level) <= limit + SLACK for value in values)
            if None not in pair:
                for band, limit in bands:
                    counts[source]["cornerPair"][band] += abs(sum(pair) / 2 - level) <= limit + SLACK
    found = {source: {name: {band: parts[name][band] for band, _ in bands} for name in ["radius " + str(radius) for radius in block["radiiM"]] + ["cornerPair"]}
             for source, parts in counts.items()}
    same = all(found[source][name][band] == first["counts"][source][name][band] for source in found for name in found[source] for band in found[source][name])
    return {"counts": found, "match": same}


def check_plan(plan, document, floors_config, entry_parameter, plots):
    """R328. Insert, rotation, mirror, FFL and El @ Entry of each sampled plan plot worked out again from the read, by the model plot."""
    offset = survey(document)
    links = defaultdict(list)
    for link in document.get("links") or []:
        number = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if number is not None:
            links[number].append(link)
    generics = of_kind(document, floors_config, "generic")
    entries = {entry["plot"]: entry for entry in plan["plots"]}
    wrong = []
    for plot in plots:
        entry = entries[plot]
        model = entry["modelPlot"]["value"]
        if len(links.get(model, [])) != 1 or len(generics.get(model, [])) != 1:
            mismatch(wrong, plot, "read", "one link and one generic floor", str(len(links.get(model, []))) + " links, " + str(len(generics.get(model, []))) + " generic floors")
            continue
        transform, floor = links[model][0]["transform"], generics[model][0]
        x, y = transform["axisX"], transform["axisY"]
        insert = [transform["origin"][axis] + offset[axis] for axis in range(2)]
        rotation = math.degrees(math.atan2(x[1], x[0])) % 360.0
        mirrored = x[0] * y[1] - x[1] * y[0] < 0
        ffl = (floor.get("topElevationRange") or {}).get("max")
        entry_value = (floor.get("parameters") or {}).get(entry_parameter)
        planned = entry["insert"]["value"]
        if max(abs(planned[axis] - insert[axis]) for axis in range(2)) > METRES:
            mismatch(wrong, plot, "insert", "plan", "read")
        if not axis_rotation_holds(entry["rotationDeg"]["value"], x):
            mismatch(wrong, plot, "rotationDeg", entry["rotationDeg"]["value"], round(rotation, 3))
        mismatch(wrong, plot, "mirrored", entry["mirrored"]["value"], mirrored)
        if ffl is None or abs(entry["levels"]["ffl"]["value"] - ffl) > METRES or abs(planned[2] - ffl) > METRES:
            mismatch(wrong, plot, "ffl", "plan", "read")
        first_entry = (entry["levels"].get("entry") or {}).get("value")
        if (first_entry is None) != (not isinstance(entry_value, (int, float))) or (first_entry is not None and abs(first_entry - entry_value) > ENTRY):
            mismatch(wrong, plot, "entry", first_entry, entry_value)
    return result(len(plots), wrong)


def check_areas(join, area_row, numbers):
    """R328. The joined plots of R326 counted by the area key plots.csv gives their official number, not their face label, against the
    area row of the key."""
    counts = Counter("%s %s" % ((numbers.get(row["number"]) or {}).get("nbhId"), (numbers.get(row["number"]) or {}).get("zone"))
                     for row in join["plots"].values() if row["status"] == "joined")
    expected = {"%s %s" % (area["nbh"], area["zone"]): area["plots"] for area in area_row["areas"]}
    return {"plots": sum(counts.values()), "byArea": dict(sorted(counts.items())), "match": dict(counts) == expected}


def check_rows(rows, key, plan):
    """R328. past_all.csv holds one row per plot of the plan of the key, counted by csv."""
    own = [row for row in rows if row["key"] == key]
    return {"rows": len(own), "planPlots": len(plan["plots"]), "match": sorted(row["plot"] for row in own) == sorted(entry["plot"] for entry in plan["plots"])}


def read_faces(path):
    rings = defaultdict(list)
    for entry in json.loads(Path(path).read_text(encoding="utf-8")).get("plots") or []:
        if entry.get("kind") == "plot_shape" and len(entry.get("vertices") or []) >= 3:
            rings[str(entry.get("plot")).strip()].append([vertex[:2] for vertex in entry["vertices"]])
    return dict(rings)


def read_rows(path, column):
    with open(path, encoding="utf-8") as handle:
        return {row[column]: row for row in csv.DictReader(handle) if row.get(column)}


def parse_plan(text):
    code, sep, path = text.partition("=")
    if not sep or not code or not path:
        raise argparse.ArgumentTypeError("give the key and its plan as CODE=PATH")
    return code, path


def main(argv=None):
    parser = argparse.ArgumentParser(description="R328. The second method of the key plans, the join, the place check, the area rows and the entrance spots.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=key_reads.parse_pair, required=True, metavar="CODE=JOB")
    parser.add_argument("--plan", action="append", type=parse_plan, required=True, metavar="CODE=PLAN")
    parser.add_argument("--join", required=True, help="key_join.json of R326")
    parser.add_argument("--spots", required=True, help="entry_spots.json of R327")
    parser.add_argument("--past-all", required=True, help="past_all.csv of R326")
    parser.add_argument("--baseplan", required=True, help="plots.json of the baseplan")
    parser.add_argument("--plots", required=True, help="plots.csv of the data book")
    parser.add_argument("--out", required=True, help="the folder key_plans_check.json is written to, outside the repo")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out.resolve())
    settings = project_profile.load_settings(args.settings)
    entry_parameter = plan_from_past.load_key_plans(settings)["entryParameter"]
    block = json.loads(Path(args.spots).read_text(encoding="utf-8"))
    joined = json.loads(Path(args.join).read_text(encoding="utf-8"))["keys"]
    plans = dict(args.plan)
    rings, by_label, by_number = read_faces(args.baseplan), read_rows(args.plots, "baseplanLabel"), read_rows(args.plots, "locationPlanNumber")
    with open(args.past_all, encoding="utf-8") as handle:
        past = list(csv.DictReader(handle))
    # El @ Entry of the schedule by official number, read here by csv on its own, not by the reader of R326
    schedule = {number: float(row["pastEntry"]) for number, row in read_rows(args.plots, "officialNumber").items() if row.get("pastEntry") and row.get("pastSource")}
    report = {"note": "R328. Each part of Round ADDIN 2b read again by another route on %d plots at seeds %s, counts, plots and ids only." % (SAMPLE, SEEDS), "keys": {}}
    for code, job in args.key:
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        plan = json.loads(Path(plans[code]).read_text(encoding="utf-8"))
        spots = block["keys"][code]
        entry = {"job": job, "areas": check_areas(joined[code]["join"], joined[code]["areaRow"], by_number), "pastAll": check_rows(past, code.lower(), plan),
                 "spotCounts": recount_spots(document, floors, spots, block["settings"], schedule)}
        for seed in SEEDS:
            compared = [plot for plot in spots["plots"] if spots["plots"][plot]]
            entry[str(seed)] = {
                "plans": check_plan(plan, document, floors, entry_parameter, sample([item["plot"] for item in plan["plots"]], seed)),
                "join": check_join(document, floors, joined[code]["join"], rings, by_label, sample(joined[code]["join"]["plots"], seed)),
                "place": check_place(document, floors, joined[code]["place"], rings, by_label, sample(joined[code]["join"]["plots"], seed)),
                "spots": check_spots(document, floors, spots, block["settings"], sample(compared, seed))}
        report["keys"][code] = entry
        print("%s: areas %s, past_all %s, spot counts equal %s, %s" % (code, entry["areas"], entry["pastAll"], entry["spotCounts"]["match"], ", ".join("seed %d %s" % (seed, {part: (found["match"], found["compared"]) for part, found in entry[str(seed)].items()})
                                                                 for seed in SEEDS)))
    out.mkdir(parents=True, exist_ok=True)
    write_json(out / OUTPUT, report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
