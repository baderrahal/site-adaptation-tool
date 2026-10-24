"""Second method for the floors of the plan from CAD and their compare (R139).

It reads the raw files with json, math and numpy only, and none of the helpers of floor_fit.py or plan_from_cad.py.
fit works out the gap of drawn past plots to the fitted outline of their group by a plain Hausdorff distance. outlines places the fitted loops of config/floor_outlines.json again by hand for drawn plots, with the group picked by a
plain search for the front edge. ramps works out the ramp corners of drawn plots again: the front edge by a plain search
over every road segment, the curb point by a plain search, the road level by a plain search over every triangle of the
road surfaces in their file order, the road first. compare counts the type, top and corner bands of the floor compare again.
Each must agree with the plan or the report it checks.
"""
import argparse
import json
import math
import random
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import cad_links_check
import cad_plan_check
import round13_check

SEED = 14
SAMPLE = 20
POINT_SLACK = 0.0015
LEVEL_SLACK = 0.0015
# The fit report rounds to the millimetre and pieces the edges as the report does, the stored loops are rounded to the millimetre too
FIT_SLACK = 0.002
DENSIFY = 0.05


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def drawn(items, seed, sample):
    ordered = sorted(items, key=int)
    random.Random(seed).shuffle(ordered)
    return ordered[:sample]


def world(points, insert, rotation, mirrored):
    turn = math.radians(rotation)
    ax, ay = math.cos(turn), math.sin(turn)
    bx, by = (ay, -ax) if mirrored else (-ay, ax)
    return [(insert[0] + x * ax + y * bx, insert[1] + x * ay + y * by) for x, y in points]


def raw_inputs(plots, pieces, profile, settings, shift):
    faces = {}
    for entry in plots:
        label = str(entry.get("plot") or "")
        if entry.get("kind") == "plot_shape" and label.isdigit():
            faces.setdefault(str(int(label) + shift), []).append(cad_links_check.counter_clockwise([(vertex[0], vertex[1]) for vertex in entry["vertices"]]))
    widths = [item["plot_width_m"] for item in profile["typologies"].values() if isinstance(item.get("plot_width_m"), (int, float))]
    low = min(widths) - settings["analysis"]["toleranceM"]
    high = settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0
    return faces, low, high, cad_links_check.road_segments(pieces, settings["analysis"]["roadEdgeLayers"])


def front_of(ring, segments, low, high):
    """The front edge with its ends, inward normal and length, by the plain search."""
    normal, right, left_end, right_end = cad_links_check.front(ring, segments, low, high)
    return {"n": normal, "left": left_end, "right": right_end, "length": math.dist(left_end, right_end)}


def check_outlines(plan, outlines, plots, pieces, profile, settings, shift, seed, sample):
    faces, low, high, segments = raw_inputs(plots, pieces, profile, settings, shift)
    step = settings["cadLinks"]["floorWidthStepM"]
    results = []
    for plot in drawn([entry["plot"] for entry in plan["plots"] if entry["floors"]], seed, sample):
        entry = next(item for item in plan["plots"] if item["plot"] == plot)
        if len(faces.get(plot) or []) != 1:
            results.append({"plot": plot, "skipped": "no single face"})
            continue
        frame = front_of(faces[plot][0], segments, low, high)
        width = round(round(frame["length"] / step) * step, 3)
        unit, mirrored = entry["unit"]["value"], entry["mirrored"]["value"]
        differs = []
        for kind in ("generic", "yard"):
            groups = [group for group in outlines["outlines"][kind] if group["unit"] == unit and group["mirrored"] == mirrored and abs(group["width"] - width) < 1e-9]
            floor = entry["floors"].get(kind)
            if not groups or floor is None:
                if bool(groups) != (floor is not None):
                    differs.append(kind + " present in one only")
                continue
            mine = [world(loop, entry["insert"]["value"], entry["rotationDeg"]["value"], mirrored) for loop in groups[0]["loops"]]
            theirs = [loop["points"] for loop in floor["outline"]["value"]["loops"]]
            gap = max(math.dist(a, b) for loop_a, loop_b in zip(mine, theirs) for a, b in zip(loop_a, loop_b)) if len(mine) == len(theirs) else None
            if gap is None or gap > POINT_SLACK:
                differs.append(kind + " outline " + str(gap))
        results.append({"plot": plot, "differs": differs})
    return results


def check_ramps(plan, outlines, plots, pieces, profile, settings, surfaces, shift, seed, sample):
    faces, low, high, segments = raw_inputs(plots, pieces, profile, settings, shift)
    setbacks = profile["ramp"]["front_setback"]
    offset = outlines["rampFront"]["offsetM"]
    results = []
    for plot in drawn([entry["plot"] for entry in plan["plots"] if "ramp" in entry["floors"]], seed, sample):
        entry = next(item for item in plan["plots"] if item["plot"] == plot)
        if len(faces.get(plot) or []) != 1:
            results.append({"plot": plot, "skipped": "no single face"})
            continue
        frame = front_of(faces[plot][0], segments, low, high)
        unit = entry["unit"]["value"]
        depth = (setbacks[unit] if unit in setbacks else setbacks["default"]) / 1000.0
        front = []
        for corner in (frame["left"], frame["right"]):
            z, _ = round13_check.plain_level(surfaces, *cad_plan_check.nearest_on_roads(corner, segments))
            front.append(None if z is None else z + offset)
        differs = []
        if None in front:
            differs.append("no road level at a front corner")
        else:
            entry_level = entry["levels"]["entry"]["value"]
            middle = (front[0] + front[1]) / 2.0
            n = frame["n"]
            mine = [(frame["left"][0], frame["left"][1], front[0]), (frame["right"][0], frame["right"][1], front[1]),
                    (frame["right"][0] + depth * n[0], frame["right"][1] + depth * n[1], entry_level + front[1] - middle),
                    (frame["left"][0] + depth * n[0], frame["left"][1] + depth * n[1], entry_level + front[0] - middle)]
            theirs = entry["floors"]["ramp"]["corners"]["value"]
            for a, b in zip(mine, theirs):
                if math.dist(a[:2], b[:2]) > POINT_SLACK or abs(a[2] - b[2]) > LEVEL_SLACK:
                    differs.append("corner " + str([round(value, 3) for value in a]) + " against " + str(b))
        results.append({"plot": plot, "differs": differs})
    return results


def link_points(points, insert, rotation, mirrored):
    turn = math.radians(rotation)
    ax, ay = math.cos(turn), math.sin(turn)
    bx, by = (ay, -ax) if mirrored else (-ay, ax)
    return [((x - insert[0]) * ax + (y - insert[1]) * ay, (x - insert[0]) * bx + (y - insert[1]) * by) for x, y in points]


def edges(loops):
    return [(loop[i], loop[(i + 1) % len(loop)]) for loop in loops for i in range(len(loop))]


def directed(first, second):
    """The largest distance from a point of the boundary of first, every edge cut into pieces of DENSIFY at most, to the boundary of second."""
    targets = edges(second)
    largest = 0.0
    for a, b in edges(first):
        pieces = max(1, math.ceil(math.dist(a, b) / DENSIFY))
        for step in range(pieces):
            point = (a[0] + (b[0] - a[0]) * step / pieces, a[1] + (b[1] - a[1]) * step / pieces)
            largest = max(largest, min(cad_links_check.segment_distance(point, c, d) for c, d in targets))
    return largest


def check_fit(fit_report, past, outlines, plots, pieces, profile, settings, shift, seed, sample):
    """The gap of drawn past plots to the fitted outline of their group, worked out by hand, against floor_fit.json."""
    faces, low, high, segments = raw_inputs(plots, pieces, profile, settings, shift)
    step = settings["cadLinks"]["floorWidthStepM"]
    entries = {entry["plot"]: entry for entry in past["plots"]}
    results = []
    for kind in ("generic", "yard"):
        gaps = {row["plot"]: row["gap"] for row in fit_report[kind]["plotGaps"]}
        for plot in drawn(gaps, seed, sample):
            entry = entries[plot]
            frame = front_of(faces[plot][0], segments, low, high)
            width = round(round(frame["length"] / step) * step, 3)
            unit, mirrored = entry["unit"]["value"], entry["mirrored"]["value"]
            group = next(group for group in outlines["outlines"][kind] if group["unit"] == unit and group["mirrored"] == mirrored and abs(group["width"] - width) < 1e-9)
            mine = [link_points(loop["points"], entry["insert"]["value"], entry["rotationDeg"]["value"], mirrored) for loop in entry["floors"][kind]["outline"]["value"]["loops"]]
            gap = max(directed(mine, group["loops"]), directed(group["loops"], mine))
            differs = [] if abs(gap - gaps[plot]) <= FIT_SLACK else [kind + " gap " + str(round(gap, 4)) + " against " + str(gaps[plot])]
            results.append({"plot": plot, "kind": kind, "differs": differs})
    return results


def check_compare(report, plan, other, apart):
    """The type, top and corner bands of the floors outside apart, counted by hand, against the report."""
    theirs = {entry["plot"]: entry for entry in other["plots"]}
    counts = {}
    for entry in plan["plots"]:
        match = theirs.get(entry["plot"])
        if match is None or entry["plot"] in apart:
            continue
        for kind in ("generic", "yard", "ramp"):
            mine, past = entry["floors"].get(kind), match["floors"].get(kind)
            if mine is None or past is None:
                continue
            table = counts.setdefault(kind, {"type": Counter(), "top": Counter(), "corners": Counter()})
            table["type"]["equal" if mine["type"]["value"] == past["type"]["value"] else "worse"] += 1
            if "z" in mine and "z" in past:
                table["top"]["within5mm" if abs(mine["z"]["value"] - past["z"]["value"]) <= 0.005 + 1e-9 else "worse"] += 1
            if "corners" in mine and "corners" in past:
                gap = max(abs(a[2] - min(past["corners"]["value"], key=lambda b: math.hypot(a[0] - b[0], a[1] - b[1]))[2]) for a in mine["corners"]["value"])
                table["corners"]["within5mm" if gap <= 0.005 + 1e-9 else "within30mm" if gap <= 0.030 + 1e-9 else "worse"] += 1
    results = []
    for kind, fields in counts.items():
        for field, table in fields.items():
            if not table:
                continue
            want = report["floors"]["outside"].get(kind, {}).get(field)
            results.append({"field": kind + " " + field, "differs": [] if dict(table) == want else [str(dict(table)) + " against " + str(want)]})
    return results


def road_surfaces(config, landxml_dir):
    """The surfaces that may answer a road level, the road first and then the design surfaces in file order, never the ground."""
    names = [name for name, entry in config["surfaces"].items() if entry["role"] == "road"] + [name for name, entry in config["surfaces"].items() if entry["role"] == "design"]
    found = []
    for name in names:
        folder = Path(landxml_dir) / name
        found.append((name, np.load(folder / "points.npy"), np.load(folder / "faces.npy")))
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check the floors of the plan from CAD and their compare a second way (R139).")
    parser.add_argument("part", choices=("fit", "outlines", "ramps", "compare"))
    parser.add_argument("out_dir")
    parser.add_argument("--plan", required=True, help="plan_imc_cad.json")
    parser.add_argument("--outlines", help="config/floor_outlines.json")
    parser.add_argument("--baseplan")
    parser.add_argument("--roads", help="pieces.json of the road layout drawing")
    parser.add_argument("--profile")
    parser.add_argument("--settings")
    parser.add_argument("--shift", type=int)
    parser.add_argument("--landxml-dir")
    parser.add_argument("--surfaces-config")
    parser.add_argument("--past-plan")
    parser.add_argument("--compare-report")
    parser.add_argument("--offsets")
    parser.add_argument("--fit-report", help="floor_fit.json")
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--sample", type=int, default=SAMPLE)
    args = parser.parse_args(argv)
    plan = read(args.plan)
    if args.part == "fit":
        results = check_fit(read(args.fit_report), read(args.past_plan), read(args.outlines), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.profile), read(args.settings),
                            args.shift, args.seed, args.sample)
    elif args.part == "outlines":
        results = check_outlines(plan, read(args.outlines), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.profile), read(args.settings), args.shift, args.seed, args.sample)
    elif args.part == "ramps":
        surfaces = road_surfaces(read(args.surfaces_config), args.landxml_dir)
        results = check_ramps(plan, read(args.outlines), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.profile), read(args.settings), surfaces, args.shift, args.seed, args.sample)
    else:
        results = check_compare(read(args.compare_report), plan, read(args.past_plan), set(read(args.offsets)["plotFrame"].get("apart") or []))
    checked = [item for item in results if "differs" in item]
    agree = [item for item in checked if not item["differs"]]
    out = {"part": args.part, "seed": args.seed, "drawn": len(results), "checked": len(checked), "agree": len(agree),
           "skipped": [item for item in results if "skipped" in item], "disagree": [item for item in checked if item["differs"]]}
    path = Path(args.out_dir) / ("cad_floor_check_" + args.part + ".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1), encoding="utf-8")
    print("%s seed %d, drawn %d, checked %d, agree %d" % (args.part, args.seed, len(results), len(checked), len(agree)))
    return 0 if len(agree) == len(checked) else 1


if __name__ == "__main__":
    sys.exit(main())
