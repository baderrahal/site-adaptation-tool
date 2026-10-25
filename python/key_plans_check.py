"""R328. The second method of Round ADDIN Phase 2b: the plans of the answer keys (R325), their join and place check (R326), the area rows,
past_all.csv and the spots at the plot entrance (R327), each read again by another route on 20 plots at seed 14 and 20 at seed 7, with
json, csv and plain math, no shapely, no tree and none of the functions that made them.

- sample: the plots are drawn from every plot the read names, by a link plot number or the Plot value of a generic floor or a ramp, never
  from the lists of the first method, so a plot it left out can be drawn
- join: the generic floor of each plot by the text of its Plot value, the share of its area on each face of plots.json by a grid of points
  over it and the even odd ray test, the face that holds cadLinks.cutShareMin of it, and the location plan number and area key of that face
  from plots.csv, not labels.csv. Two key plots on one number are twoPlots
- links: each link is tied to a plot by the face its house centre stands on, the house centre learned per file on the other links of that
  file by the plot their Name gives, never by the plot number parameter the first method maps a link by
- plans: insert, rotation, mirror, FFL, El @ Entry with the band of keyPlans.entryHeightM, the height, the steps, modelPlot and the walls
  of each plot, worked out again from the read
- place: each generic floor and ramp by its grid share, each link by its house centre
- spots: the spots of the floors of the plot only, the entrance as the middle of the higher of the two long edges of the ramp, the
  schedule by the official number of the second join and only for the key of its job
- past_all.csv: source, kind, El @ Entry, its source and the height source of each sampled plot, the rows of a key against its plan, and
  the counts of the join, the area rows, the plan and past_all.csv of each key against each other
Written to key_plans_check.json outside the repo, counts, plot numbers and element ids only.
"""
import argparse
import csv
import json
import math
import random
import re
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
SURVEY_BASE = "Survey Point"
NUMBERS_BAR = "|"
LINK_PLOT = "RRE_ZZ_SP_Plot-Number"
LINK_STEPS = "RRE_ZZ_SP_Number-of-Steps"
# Points per side of the grid laid over a floor. A share is off by about one column of the grid, 0.02, where the measured shares are 1 or
# at most 0.84 against the limit 0.9
GRID = 50
SHARE_SLACK = 0.03
# Plan values are written to the mm and El @ Entry to a tenth of a mm by R2, so these are the rounding of two values
METRES = 0.0005
ENTRY = 0.00005
# A link axis is written to 3 decimals by R2, so a component is off by up to half a thousandth and the angle worked out from it by up to
# about 0.04 degrees, a little more is allowed
AXIS_DEGREES = 0.05
SLACK = 1e-7
LEADING_DIGITS = re.compile(r"^\s*(\d+)")


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


def box(rings):
    xs = [point[0] for ring in rings for point in ring]
    ys = [point[1] for ring in rings for point in ring]
    return min(xs), min(ys), max(xs), max(ys)


def in_box(point, bounds):
    return bounds[0] <= point[0] <= bounds[2] and bounds[1] <= point[1] <= bounds[3]


def overlap(first, second):
    return first[0] <= second[2] and second[0] <= first[2] and first[1] <= second[3] and second[1] <= first[3]


def largest_loop(floor):
    """The outline loop of R52 with the largest shoelace area, as [x, y] in shared coordinates, or None."""
    loops = [[point[:2] for point in loop] for loop in floor.get("outline") or [] if len(loop) >= 3]
    return max(loops, key=lambda loop: abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(loop, loop[1:] + loop[:1]))), default=None)


def plot_text(floor, parameter):
    """The plot of a floor from the text of its plot parameter, floors.plotParameter, the digits after the last dash, never the plotNumber field."""
    text = str((floor.get("parameters") or {}).get(parameter) or "")
    number = text.rpartition("-")[2] if "-" in text else ""
    return str(int(number)) if number.isascii() and number.isdigit() else None


def link_plot(link):
    number = str((link.get("parameters") or {}).get(LINK_PLOT) or "").strip()
    return str(int(number)) if number.isascii() and number.isdigit() else None


def name_plot(link):
    """R328. The plot a link Name gives, its first part when the Name has bars, else its digits after the last dash."""
    name = str((link.get("parameters") or {}).get("Name") or "")
    for part in (name.split("|")[0].strip(), name.rpartition("-")[2].strip()):
        if part.isascii() and part.isdigit():
            return str(int(part))
    return None


def of_kind(document, floors_config, kind):
    found = defaultdict(list)
    for floor in document.get("floors") or []:
        if floor.get("typeName") in floors_config["types"][kind]:
            plot = plot_text(floor, floors_config["plotParameter"])
            if plot is not None:
                found[plot].append(floor)
    return found


def population(document, floors_config):
    """R328. Every plot the read names: the plot numbers of its links and the Plot values of its generic floors and ramps."""
    plots = {plot for plot in (link_plot(link) for link in document.get("links") or []) if plot is not None}
    for kind in ("generic", "ramp"):
        plots |= set(of_kind(document, floors_config, kind))
    return sorted(plots, key=order)


def survey(document):
    point = document["surveyPoint"]
    return [point["sharedPosition"][axis] - point["position"][axis] for axis in range(3)]


def face_index(rings):
    """R328. The rings of each face of plots.json with the box around them, so a point is only tested on the faces whose box holds it."""
    return {"rings": rings, "boxes": {label: box(shapes) for label, shapes in rings.items()}}


def faces_at(point, faces):
    return [label for label, bounds in faces["boxes"].items() if in_box(point, bounds) and any(inside(point, ring) for ring in faces["rings"][label])]


def number_of(face, rows):
    number = (rows.get(face) or {}).get("locationPlanNumber") or None
    # plots.csv joins the numbers of a face of several plan labels with a bar, such a face has no one number
    return None if number is None or NUMBERS_BAR in number else number


def where(point, faces, rows):
    """R328. The status, face and number of a point by ray casting and plots.csv: noPlace, noFace, twoFaces, noNumber or joined."""
    if point is None:
        return {"status": "noPlace", "face": None, "number": None}
    hits = faces_at(point, faces)
    if not hits:
        return {"status": "noFace", "face": None, "number": None}
    if len(hits) > 1:
        return {"status": "twoFaces", "face": None, "number": None}
    number = number_of(hits[0], rows)
    return {"status": "joined" if number else "noNumber", "face": hits[0], "number": number}


def grid_shares(ring, faces):
    """R328. The share of a floor ring on each face by a grid of GRID by GRID points over its box: the points inside the ring that each face
    holds over all points inside the ring, the largest first."""
    bounds = box([ring])
    width, height = bounds[2] - bounds[0], bounds[3] - bounds[1]
    points = [(bounds[0] + (column + 0.5) * width / GRID, bounds[1] + (row + 0.5) * height / GRID) for column in range(GRID) for row in range(GRID)]
    points = [point for point in points if inside(point, ring)]
    if not points:
        return []
    found = []
    for label, face_box in faces["boxes"].items():
        if not overlap(face_box, bounds):
            continue
        count = sum(1 for point in points if in_box(point, face_box) and any(inside(point, shape) for shape in faces["rings"][label]))
        if count:
            found.append((label, count / len(points)))
    return sorted(found, key=lambda item: (-item[1], order(item[0])))


def floor_status(ring, faces, rows, share_min):
    """R328. The face that holds at least share_min of the floor by the grid, its number and share, or noFace, twoFaces or cut with the shares."""
    shares = grid_shares(ring, faces)
    held = [label for label, share in shares if share >= share_min]
    if len(held) == 1:
        number = number_of(held[0], rows)
        return {"status": "joined" if number else "noNumber", "face": held[0], "number": number, "share": round(dict(shares)[held[0]], 3)}
    return {"status": "twoFaces" if held else "cut" if shares else "noFace", "face": None, "number": None,
            "shares": [(label, round(share, 3)) for label, share in shares]}


def status_class(status):
    """A face of no plan label and one of two have no number in plots.csv alike, so both are noNumber here."""
    return "noNumber" if status == "manyNumbers" else status


def second_join(document, floors_config, faces, rows, share_min):
    """R328. Each plot a link names joined again by the grid share of its one generic floor, and two plots on one number marked twoPlots."""
    generics = of_kind(document, floors_config, "generic")
    found = {}
    for plot in sorted({plot for plot in (link_plot(link) for link in document.get("links") or []) if plot is not None}, key=order):
        own = generics.get(plot, [])
        loop = largest_loop(own[0]) if len(own) == 1 else None
        if len(own) != 1:
            found[plot] = {"status": "noGenericFloor" if not own else "twoGenericFloors", "face": None, "number": None}
        else:
            found[plot] = floor_status(loop, faces, rows, share_min) if loop else {"status": "noShape", "face": None, "number": None}
    numbers = Counter(row["number"] for row in found.values() if row["status"] == "joined")
    for row in found.values():
        if row["status"] == "joined" and numbers[row["number"]] > 1:
            row["status"] = "twoPlots"
    return found


def mismatch(found, plot, field, first, second):
    if first != second:
        found.append({"plot": plot, "field": field, "first": first, "second": second})


def result(compared, wrong):
    return {"compared": compared, "match": compared - len({item["plot"] for item in wrong}), "mismatch": wrong}


def top_share(row):
    if row.get("share") is not None:
        return row["share"]
    shares = row.get("shares") or []
    return None if not shares else (shares[0]["share"] if isinstance(shares[0], dict) else shares[0][1])


def check_join(first, second, plots, rows):
    """R328. Each sampled plot of the join of R326 found again: in both or neither, status, face, number, share and the area key of plots.csv."""
    wrong = []
    for plot in plots:
        one, two = first["plots"].get(plot), second.get(plot)
        if one is None or two is None:
            mismatch(wrong, plot, "presence", one is not None, two is not None)
            continue
        mismatch(wrong, plot, "status", status_class(one["status"]), two["status"])
        if one["status"] in ("joined", "twoPlots") and two["status"] in ("joined", "twoPlots"):
            mismatch(wrong, plot, "face", one["face"], two["face"])
            mismatch(wrong, plot, "number", one["number"], two["number"])
        share, again = top_share(one), top_share(two)
        if share is not None and again is not None and abs(share - again) > SHARE_SLACK:
            mismatch(wrong, plot, "share", share, again)
        if one["status"] == "joined" and two["status"] == "joined":
            row = rows.get(two["face"]) or {}
            mismatch(wrong, plot, "areaNbh", one.get("areaNbh"), row.get("nbhId"))
            mismatch(wrong, plot, "areaZone", one.get("areaZone"), row.get("zone"))
    return result(len(plots), wrong)


def link_places(document, floors_config, faces, rows):
    """R328. Where each link of the read stands, in the order of the links: the face that holds its house centre, learned per file as the
    median place of the generic floor centroid in the axes of the other links of that file, each by the plot its Name gives."""
    offset = survey(document)
    generics = of_kind(document, floors_config, "generic")
    links = document.get("links") or []
    places = defaultdict(list)
    for index, link in enumerate(links):
        plot, transform = name_plot(link), link.get("transform")
        loop = largest_loop(generics[plot][0]) if plot is not None and len(generics.get(plot, [])) == 1 else None
        if loop is None or not transform:
            continue
        cx, cy = centroid(loop)
        dx, dy = cx - transform["origin"][0] - offset[0], cy - transform["origin"][1] - offset[1]
        places[link.get("file")].append((index, dx * transform["axisX"][0] + dy * transform["axisX"][1], dx * transform["axisY"][0] + dy * transform["axisY"][1]))
    found = []
    for index, link in enumerate(links):
        others = [(along, across) for at, along, across in places.get(link.get("file"), []) if at != index]
        transform = link.get("transform")
        if not others or not transform:
            found.append(where(None, faces, rows))
            continue
        along, across = statistics.median(item[0] for item in others), statistics.median(item[1] for item in others)
        origin, x, y = transform["origin"], transform["axisX"], transform["axisY"]
        found.append(where((origin[0] + offset[0] + along * x[0] + across * y[0], origin[1] + offset[1] + along * x[1] + across * y[1]), faces, rows))
    return found


def check_place(document, floors_config, place, faces, rows, links_at, share_min, plots):
    """R328. The generic floors, ramps and links of each sampled plot of the place check of R326 found again, verdict and number found."""
    first_of = {(item["kind"], str(item["elementId"])): item for item in place["mismatches"]}
    elements = []
    for kind in ("generic", "ramp"):
        for plot, floors in of_kind(document, floors_config, kind).items():
            if plot in plots:
                for floor in floors:
                    loop = largest_loop(floor)
                    elements.append((kind, floor, plot, floor_status(loop, faces, rows, share_min) if loop else where(None, faces, rows)))
    for link, found in zip(document.get("links") or [], links_at):
        if link_plot(link) in plots:
            elements.append(("link", link, link_plot(link), found))
    wrong = []
    for kind, element, plot, found in elements:
        verdict = "match" if found["status"] == "joined" and found["number"] == plot else ("otherNumber" if found["status"] == "joined" else found["status"])
        first = first_of.get((kind, str(element.get("elementId"))))
        name = kind + " " + str(element.get("elementId"))
        mismatch(wrong, plot, name, status_class(first["verdict"]) if first else "match", verdict)
        if first and verdict == "otherNumber":
            mismatch(wrong, plot, name + " number", first["found"], found["number"])
    return {"compared": len(elements), "match": len(elements) - len({item["field"].split(" number")[0] for item in wrong}), "mismatch": wrong}


def axis_rotation_holds(rotation, axis):
    """R328. True when the rotation in degrees of the plan is the angle of the link X axis written to 3 decimals, within AXIS_DEGREES."""
    found = math.degrees(math.atan2(axis[1], axis[0]))
    return abs((rotation - found + 180.0) % 360.0 - 180.0) <= AXIS_DEGREES


def segment_distance(point, start, end):
    dx, dy = end[0] - start[0], end[1] - start[1]
    length = dx * dx + dy * dy
    along = 0.0 if length == 0 else max(0.0, min(1.0, ((point[0] - start[0]) * dx + (point[1] - start[1]) * dy) / length))
    return math.hypot(point[0] - start[0] - along * dx, point[1] - start[1] - along * dy)


def walls_of(document, ring, edge):
    """R328. The ids of the walls whose location line has its middle inside the ring or within edge of one of its edges, by plain math."""
    offset = survey(document)
    found = set()
    for wall in document.get("walls") or []:
        location = wall.get("location") or {}
        if location.get("kind") != "line" or not isinstance(location.get("start"), list) or not isinstance(location.get("end"), list):
            continue
        middle = ((location["start"][0] + location["end"][0]) / 2 + offset[0], (location["start"][1] + location["end"][1]) / 2 + offset[1])
        if inside(middle, ring) or min(segment_distance(middle, ring[index - 1], ring[index]) for index in range(len(ring))) <= edge + SLACK:
            found.add(str(wall.get("elementId")))
    return found


def refused(plan, plot):
    return any(item["key"] == "levels.entry" and str(item.get("why", "")).startswith(plan_from_past.REFUSED)
               for row in plan.get("missing") or [] if row.get("plot") == plot for item in row.get("missing") or [])


def check_plan(plan, document, floors_config, key, second, links_at, plots):
    """R328. Each sampled plot of the read in the plan again: a plot the second join does not join holds no plan entry, a joined one holds
    the entry of its number or is listed missing, and that entry holds the model plot, the insert, rotation and mirror of the link that
    stands on its face, the steps of that link, the FFL of its generic floor, El @ Entry of its parameter when the height it gives is in
    keyPlans.entryHeightM and none else, the height and the walls of its floor."""
    offset = survey(document)
    links = document.get("links") or []
    generics = of_kind(document, floors_config, "generic")
    entries = {entry["plot"]: entry for entry in plan["plots"]}
    listed = {row["plot"] for row in plan.get("missing") or [] if not row.get("written")}
    band = key["entryHeightM"]
    wrong = []
    for plot in plots:
        found = second.get(plot) or {"status": "noKeyPlot"}
        if found["status"] != "joined":
            held = [entry["plot"] for entry in plan["plots"] if (entry.get("modelPlot") or {}).get("value") == plot]
            if held:
                mismatch(wrong, plot, "presence", "in the plan as " + " ".join(held), found["status"])
            continue
        number = found["number"]
        entry = entries.get(number)
        if entry is None:
            mismatch(wrong, plot, "presence", "listed missing" if number in listed else "absent", "listed missing")
            continue
        mismatch(wrong, plot, "modelPlot", (entry.get("modelPlot") or {}).get("value"), plot)
        tied = [link for link, at in zip(links, links_at) if at["status"] == "joined" and at["number"] == number]
        floor = generics[plot][0]
        if len(tied) != 1:
            mismatch(wrong, plot, "link", "one link on the face", str(len(tied)) + " links")
        else:
            transform, parameters = tied[0]["transform"], tied[0].get("parameters") or {}
            planned = entry["insert"]["value"]
            if max(abs(planned[axis] - transform["origin"][axis] - offset[axis]) for axis in range(2)) > METRES:
                mismatch(wrong, plot, "insert", "plan", "read")
            x, y = transform["axisX"], transform["axisY"]
            if not axis_rotation_holds(entry["rotationDeg"]["value"], x):
                mismatch(wrong, plot, "rotationDeg", entry["rotationDeg"]["value"], round(math.degrees(math.atan2(x[1], x[0])) % 360.0, 3))
            mismatch(wrong, plot, "mirrored", entry["mirrored"]["value"], x[0] * y[1] - x[1] * y[0] < 0)
            steps = LEADING_DIGITS.match(str(parameters.get(LINK_STEPS) or ""))
            mismatch(wrong, plot, "steps", (entry["levels"].get("steps") or {}).get("value"), int(steps.group(1)) if steps else None)
        top = floor.get("topElevationRange") or {}
        ffl = top.get("max") if top.get("min") is not None and abs(top["max"] - top["min"]) <= 1e-9 else None
        levels = entry["levels"]
        if ffl is None or abs(levels["ffl"]["value"] - ffl) > METRES or abs(entry["insert"]["value"][2] - ffl) > METRES:
            mismatch(wrong, plot, "ffl", "plan", "read")
        value = (floor.get("parameters") or {}).get(key["entryParameter"])
        usable = isinstance(value, (int, float)) and not isinstance(value, bool) and ffl is not None and band["min"] - SLACK <= ffl - value <= band["max"] + SLACK
        held = levels.get("entry") or {}
        if usable != ("value" in held) or (usable and (abs(held["value"] - value) > ENTRY or held.get("source") != plan_from_past.KEY_PARAMETER)):
            mismatch(wrong, plot, "entry", held.get("value"), value if usable else None)
        if not usable and isinstance(value, (int, float)) and not refused(plan, number):
            mismatch(wrong, plot, "entryRefused", "not listed", "refused")
        height = (levels.get("height") or {}).get("value")
        if usable != (height is not None) or (usable and abs(height - (ffl - value)) > METRES + ENTRY):
            mismatch(wrong, plot, "height", height, round(ffl - value, 4) if usable else None)
        loop = largest_loop(floor)
        first_walls = {str(wall["elementId"]) for wall in plan.get("walls") or [] if number in wall.get("plots") or []}
        mismatch(wrong, plot, "walls", sorted(first_walls), sorted(walls_of(document, loop, key["wallEdgeM"])) if loop else [])
    return result(len(plots), wrong)


def cell(row, column):
    return float(row[column]) if row.get(column) not in (None, "") else None


def check_sources(rows, code, plan, by_number, config, numbers):
    """R328. The row of past_all.csv of each sampled plan plot by its official number: source, the group of majority.groups, kind, the kind
    of answerKeys, entrySource and El @ Entry, the schedule of the job of the key by plots.csv and pastAll.scheduleKeys first, else the El @
    Entry of the plan, else refused when the plan refused it, FFL and the height, the pair of that schedule or the floor top of the plan less
    El @ Entry, and heightSource, schedule less schedule for a pair, else the floor top less the source of El @ Entry."""
    key = code.lower()
    own = {row["plot"]: row for row in rows if row["key"] == key}
    entries = {entry["plot"]: entry for entry in plan["plots"]}
    group = next((name for name, keys in config["groups"].items() if key in keys), None)
    wrong = []
    for number in numbers:
        row, entry = own.get(number), entries.get(number)
        if row is None or entry is None:
            mismatch(wrong, number, "row", entry is not None, row is not None)
            continue
        mismatch(wrong, number, "source", row.get("source"), group)
        mismatch(wrong, number, "kind", row.get("kind"), config["kinds"].get(code))
        schedule = by_number.get(number) or {}
        held = (entry["levels"].get("entry") or {}).get("value")
        ffl, top = (entry["levels"].get("ffl") or {}).get("value"), "floorTop"
        if schedule.get("pastEntry") and config["scheduleKeys"].get(schedule.get("pastSource")) == code:
            source, value = "schedule", float(schedule["pastEntry"])
            ffl, top = (float(schedule["pastFfl"]) if schedule.get("pastFfl") else None), "schedule"
        elif held is not None:
            source, value = "floorParameter", held
        else:
            source, value = ("refused" if refused(plan, number) else ""), None
        mismatch(wrong, number, "entrySource", row.get("entrySource") or "", source)
        written = cell(row, "entry")
        if (written is None) != (value is None) or (value is not None and abs(written - value) > ENTRY):
            mismatch(wrong, number, "entry", written, value)
        if ffl is None or cell(row, "ffl") is None or abs(cell(row, "ffl") - ffl) > METRES:
            mismatch(wrong, number, "ffl", cell(row, "ffl"), ffl)
        height = None if value is None or ffl is None else ffl - value
        if (cell(row, "height") is None) != (height is None) or (height is not None and abs(cell(row, "height") - height) > METRES + ENTRY):
            mismatch(wrong, number, "height", cell(row, "height"), None if height is None else round(height, 4))
        expected = top + " less " + source if value is not None and ffl is not None else ""
        mismatch(wrong, number, "heightSource", row.get("heightSource") or "", expected)
    return result(len(numbers), wrong)


def check_counts(join_entry, plan, rows, key):
    """R328. The plots the join of a key joins, the plots its area row counts, the plots of its plan and its rows of past_all.csv, which must
    be as many, and the official numbers only some of them hold."""
    joined = {row["number"] for row in join_entry["join"]["plots"].values() if row["status"] == "joined"}
    planned = {entry["plot"] for entry in plan["plots"]}
    past = {row["plot"] for row in rows if row["key"] == key}
    counts = {"joined": len(joined), "areaRows": sum(area["plots"] for area in join_entry["areaRow"]["areas"]), "planPlots": len(planned), "pastAllRows": len(past)}
    counts["agree"] = len(set(counts.values())) == 1
    counts.update(onlyJoined=sorted(joined - planned, key=order), onlyPlan=sorted(planned - joined, key=order), onlyPastAll=sorted(past - planned, key=order))
    return counts


def entrance_edge(corners):
    """R328. The two corners of the higher long edge of a ramp of four corners: the corners in order of their angle round their mean
    point, the pair of opposite edges with the larger total length, of them the edge with the larger mean level."""
    mx, my = sum(corner[0] for corner in corners) / 4, sum(corner[1] for corner in corners) / 4
    ring = sorted(corners, key=lambda corner: math.atan2(corner[1] - my, corner[0] - mx))
    edges = [(ring[index], ring[(index + 1) % 4]) for index in range(4)]
    length = [math.hypot(a[0] - b[0], a[1] - b[1]) for a, b in edges]
    pair = (edges[0], edges[2]) if length[0] + length[2] >= length[1] + length[3] else (edges[1], edges[3])
    return max(pair, key=lambda edge: edge[0][2] + edge[1][2])


def own_spots(document, floors_config):
    """R328. The spots whose value reads and whose base is the survey point, in shared plan coordinates, by the plot of the Plot value of
    their host floor, as (id, x, y, value, host)."""
    offset = survey(document)
    host_plot = {str(floor.get("elementId")): plot_text(floor, floors_config["plotParameter"]) for floor in document.get("floors") or []}
    found = defaultdict(list)
    for spot in document.get("spotElevations") or []:
        if not isinstance(spot.get("value"), (int, float)) or spot.get("elevationBase") != SURVEY_BASE or not isinstance(spot.get("origin"), list):
            continue
        host = str((spot.get("host") or {}).get("elementId"))
        plot = host_plot.get(host)
        if plot is not None:
            found[plot].append((spot.get("elementId"), spot["origin"][0] + offset[0], spot["origin"][1] + offset[1], spot["value"], host))
    return found


def ramp_edge(ramp, offset):
    corners = ramp.get("corners") or []
    if len(corners) != 4:
        return None
    return entrance_edge([[corner[axis] + offset[axis] for axis in range(3)] for corner in corners])


def corner_spots(edge, spots, ramp, limit):
    pair = []
    for corner in edge:
        close = sorted((math.hypot(x - corner[0], y - corner[1]), spot_id, value) for spot_id, x, y, value, host in spots
                       if host == str(ramp.get("elementId")) and math.hypot(x - corner[0], y - corner[1]) <= limit + SLACK)
        pair.append(close[0][1:] if close else None)
    return pair


def check_spots(document, floors_config, first, settings, plots):
    """R328. The spots of each sampled plot of R327 found again with the settings of its key: compared when it has one ramp of four
    corners, the spots of its own floors within each radius of the middle of the entrance edge and the spot of the ramp nearest each corner
    of that edge within the corner distance."""
    offset = survey(document)
    ramps, spots = of_kind(document, floors_config, "ramp"), own_spots(document, floors_config)
    not_compared = {plot for items in first["notCompared"].values() for plot in items}
    wrong = []
    for plot in plots:
        own = ramps.get(plot, [])
        edge = ramp_edge(own[0], offset) if len(own) == 1 else None
        if plot not in first["plots"]:
            if edge is not None and plot not in not_compared:
                mismatch(wrong, plot, "compared", False, True)
            continue
        if edge is None:
            mismatch(wrong, plot, "compared", True, False)
            continue
        mx, my = (edge[0][0] + edge[1][0]) / 2, (edge[0][1] + edge[1][1]) / 2
        for radius in settings["radiiM"]:
            near = sorted(spot_id for spot_id, x, y, _, _ in spots.get(plot, []) if math.hypot(x - mx, y - my) <= radius + SLACK)
            mismatch(wrong, plot, "near " + str(radius), first["plots"][plot]["near"][str(radius)], near)
        pair = [None if item is None else item[0] for item in corner_spots(edge, spots.get(plot, []), own[0], settings["cornerM"])]
        mismatch(wrong, plot, "corners", sorted(first["plots"][plot]["corners"], key=str), sorted(pair, key=str))
    return result(len(plots), wrong)


def band_value(floor, settings):
    """R328. The El @ Entry parameter of a generic floor, None when it is no number or when the flat floor top stands outside the band of
    the settings over it."""
    value = (floor.get("parameters") or {}).get(settings["entryParameter"])
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return None
    top, band = floor.get("topElevationRange") or {}, settings.get("entryHeightM")
    if band and top.get("min") is not None and abs(top["max"] - top["min"]) <= 1e-9 and not band["min"] - SLACK <= top["max"] - value <= band["max"] + SLACK:
        return None
    return value


def recount_spots(document, floors_config, first, settings, schedule, second):
    """R328. On every plot R327 compared, the plots with a spot of their own floors within each radius of the entrance and with both corner
    spots whose value or mean is within each band of El @ Entry of the key and of the schedule, the schedule by the official number of the
    second join, counted again against the counts of R327. match is True only when every count is equal."""
    offset = survey(document)
    ramps, generics, spots = of_kind(document, floors_config, "ramp"), of_kind(document, floors_config, "generic"), own_spots(document, floors_config)
    bands = [("within%gmm" % round(band * 1000.0, 6), band) for band in settings["bandsM"]]
    counts = {source: defaultdict(Counter) for source in ("key", "schedule")}
    for plot in first["plots"]:
        ramp = ramps[plot][0]
        edge = ramp_edge(ramp, offset)
        mx, my = (edge[0][0] + edge[1][0]) / 2, (edge[0][1] + edge[1][1]) / 2
        pair = corner_spots(edge, spots.get(plot, []), ramp, settings["cornerM"])
        own = generics.get(plot, [])
        key_value = band_value(own[0], settings) if len(own) == 1 else None
        joined = second.get(plot) or {}
        official = joined.get("number") if joined.get("status") == "joined" else None
        for source, level in (("key", key_value), ("schedule", (schedule or {}).get(official) if official else None)):
            if not isinstance(level, (int, float)) or isinstance(level, bool):
                continue
            for radius in settings["radiiM"]:
                values = [value for _, x, y, value, _ in spots.get(plot, []) if math.hypot(x - mx, y - my) <= radius + SLACK]
                for band, limit in bands:
                    counts[source]["radius " + str(radius)][band] += any(abs(value - level) <= limit + SLACK for value in values)
            if None not in pair:
                for band, limit in bands:
                    counts[source]["cornerPair"][band] += abs(sum(item[1] for item in pair) / 2 - level) <= limit + SLACK
    names = ["radius " + str(radius) for radius in settings["radiiM"]] + ["cornerPair"]
    found = {source: {name: {band: parts[name][band] for band, _ in bands} for name in names} for source, parts in counts.items()}
    same = all(found[source][name][band] == first["counts"][source][name][band] for source in found for name in found[source] for band in found[source][name])
    return {"counts": found, "match": same}


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


def config_values(settings):
    """R328. What the checks of past_all.csv take from the project settings, read as values: the groups, the kind of each answer key and
    the answer key of each schedule source."""
    return {"groups": settings.get("majority.groups"), "kinds": {key["code"]: key.get("kind") for key in settings.get("answerKeys.keys")},
            "scheduleKeys": settings.get("pastAll.scheduleKeys")}


def main(argv=None):
    parser = argparse.ArgumentParser(description="R328. The second method of the key plans, the join, the place check, past_all.csv and the entrance spots.")
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
    key = plan_from_past.load_key_plans(settings)
    config = config_values(settings)
    spots_file = json.loads(Path(args.spots).read_text(encoding="utf-8"))["keys"]
    joined = json.loads(Path(args.join).read_text(encoding="utf-8"))["keys"]
    plans = dict(args.plan)
    faces = face_index(read_faces(args.baseplan))
    by_label, by_number = read_rows(args.plots, "baseplanLabel"), read_rows(args.plots, "locationPlanNumber")
    by_official = read_rows(args.plots, "officialNumber")
    with open(args.past_all, encoding="utf-8") as handle:
        past = list(csv.DictReader(handle))
    report = {"note": "R328. Each part of Round ADDIN 2b read again by another route on %d plots at seeds %s drawn from every plot of the read, "
                      "counts, plots and ids only." % (SAMPLE, SEEDS), "keys": {}}
    for code, job in args.key:
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        plan = json.loads(Path(plans[code]).read_text(encoding="utf-8"))
        spots = spots_file[code]
        share_min = joined[code]["settings"]["cutShareMin"]
        second = second_join(document, floors, faces, by_label, share_min)
        links_at = link_places(document, floors, faces, by_label)
        # El @ Entry of the schedule of the job of this key by official number, read here by csv on its own, not by the reader of R326
        schedule = {number: float(row["pastEntry"]) for number, row in by_official.items()
                    if row.get("pastEntry") and config["scheduleKeys"].get(row.get("pastSource")) == code}
        entry = {"job": job, "areas": check_areas(joined[code]["join"], joined[code]["areaRow"], by_number), "pastAll": check_rows(past, code.lower(), plan),
                 "counts": check_counts(joined[code], plan, past, code.lower()),
                 "secondJoin": dict(Counter(row["status"] for row in second.values())),
                 # The second join runs on every plot a link names, so the join is held on all of them beside the samples
                 "joinAll": check_join(joined[code]["join"], second, sorted(second, key=order), by_label),
                 "spotCounts": recount_spots(document, floors, spots, spots["settings"], schedule, second)}
        everything = population(document, floors)
        for seed in SEEDS:
            plots = sample(everything, seed)
            numbers = [second[plot]["number"] for plot in plots if (second.get(plot) or {}).get("status") == "joined" and second[plot]["number"] in
                       {item["plot"] for item in plan["plots"]}]
            entry[str(seed)] = {
                "plans": check_plan(plan, document, floors, key, second, links_at, plots),
                "join": check_join(joined[code]["join"], second, plots, by_label),
                "place": check_place(document, floors, joined[code]["place"], faces, by_label, links_at, share_min, plots),
                "spots": check_spots(document, floors, spots, spots["settings"], plots),
                "pastAllRows": check_sources(past, code, plan, by_official, config, numbers)}
        report["keys"][code] = entry
        print("%s: counts %s, second join %s, join of every plot %s, areas %s, past_all %s, spot counts equal %s, %s" % (
            code, {name: value for name, value in entry["counts"].items() if not isinstance(value, list)}, entry["secondJoin"],
            (entry["joinAll"]["match"], entry["joinAll"]["compared"], entry["joinAll"]["mismatch"]), entry["areas"]["match"],
            entry["pastAll"]["match"], entry["spotCounts"]["match"],
            ", ".join("seed %d %s" % (seed, {part: (found["match"], found["compared"]) for part, found in entry[str(seed)].items()}) for seed in SEEDS)))
    out.mkdir(parents=True, exist_ok=True)
    write_json(out / OUTPUT, report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
