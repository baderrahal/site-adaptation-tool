"""Second method for the plan from CAD, the ramp outline test and the plan against plan compare (R130).

levels works out the FFL of drawn plots again from El @ Entry of the past plan, the clusters of analysis.json and the base
heights of the raw profile, with no call to levels.py. ramps works out the ramp outline of drawn ramps again from the raw
baseplan, road edges and profile, with a plain search over every road segment, pairs its corners with the past corners by
trying all 24 orders, and asks the road surface by a plain search over every triangle at each corner and curb point.
compare counts the fields of the plan against plan report again by hand. Each must agree with the report it checks.
"""
import argparse
import itertools
import json
import math
import random
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import cad_links_check
import round13_check

SEED = 14
SAMPLE = 20
FFL_SLACK = 0.0006
HALF_SLACK = 1e-6
CORNER_SLACK = 0.001
LENGTH_BANDS = (("equal", 0.0000005), ("within1mm", 0.001), ("within10mm", 0.010))


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def drawn(items, seed, sample):
    ordered = sorted(items, key=lambda plot: int(plot))
    random.Random(seed).shuffle(ordered)
    return ordered[:sample]


def check_levels(cad_plan, past_plan, analysis, profile, shift, seed, sample):
    """The FFL and SSL of drawn plots of the CAD plan, worked out by hand, against the plan."""
    entries = {entry["plot"]: entry["levels"]["entry"]["value"] for entry in past_plan["plots"] if "entry" in entry["levels"]}
    units = {entry["plot"]: entry["unit"]["value"] for entry in past_plan["plots"]}
    units.update({entry["plot"]: entry["unit"]["value"] for entry in cad_plan["plots"]})
    groups = {}
    for cluster in analysis["clusters"]:
        members = [str(int(label) + shift) for label in cluster["plots"]]
        for member in members:
            groups[member] = members
    table = profile["steps"]["base_height"]
    base = lambda unit: (table[unit] if unit in table else table["default"]) / 1000.0
    ground = profile["levels"]["ground_ssl"] / 1000.0
    written = {entry["plot"]: entry for entry in cad_plan["plots"]}
    results = []
    for plot in drawn(written, seed, sample):
        members = [member for member in groups.get(plot, [plot]) if member in entries]
        if not members:
            results.append({"plot": plot, "skipped": "no entry level"})
            continue
        exact = max(entries[member] for member in members) + max(base(units.get(members[0])), base(units.get(members[-1])))
        ffl = round(exact, 3)
        levels = written[plot]["levels"]
        differs, half = [], False
        # El @ Entry of the past plan holds 4 decimals (R2). An FFL on a half millimetre is decided by the decimals the plan does not hold, so either millimetre agrees
        if abs(abs(exact * 1000.0 - math.floor(exact * 1000.0)) - 0.5) < HALF_SLACK and min(abs(levels["ffl"]["value"] - (exact - 0.0005)), abs(levels["ffl"]["value"] - (exact + 0.0005))) < HALF_SLACK:
            ffl, half = levels["ffl"]["value"], True
        if abs(ffl - levels["ffl"]["value"]) > FFL_SLACK:
            differs.append("ffl " + str(ffl) + " against " + str(levels["ffl"]["value"]))
        if abs(ffl + ground - levels["ssl"]["value"]) > FFL_SLACK:
            differs.append("ssl differs")
        if abs(ffl - written[plot]["insert"]["value"][2]) > FFL_SLACK:
            differs.append("insert z is not the FFL")
        results.append({"plot": plot, "differs": differs, "halfMillimetre": half})
    return results


def best_pairing(planned, corners):
    """The largest corner gap of the pairing of planned and past corners whose largest gap is the least, over all 24 orders."""
    return min(max(math.dist(point, corners[index]) for point, index in zip(planned, order)) for order in itertools.permutations(range(len(corners))))


def nearest_on_roads(point, segments):
    best = None
    for a, b in segments:
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = dx * dx + dy * dy
        t = 0.0 if length == 0 else max(0.0, min(1.0, ((point[0] - a[0]) * dx + (point[1] - a[1]) * dy) / length))
        foot = (a[0] + t * dx, a[1] + t * dy)
        distance = math.dist(point, foot)
        if best is None or distance < best[0]:
            best = (distance, foot)
    return best[1]


def check_ramps(report, prototypes, plots, pieces, profile, settings, ramp_types, surface, shift, seed, sample):
    """The ramp outline and the road cover of drawn ramps, worked out by hand, against ramp_outline.json."""
    document = next(item for item in prototypes["documents"] if item.get("kind") == "project")
    survey = document["surveyPoint"]
    offset = [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(2)]
    ramps = {}
    for floor in document.get("floors") or []:
        plot_value = str((floor.get("parameters") or {}).get("Plot") or "")
        if floor.get("typeName") in ramp_types and floor.get("corners") and "-" in plot_value:
            ramps[str(int(plot_value.rpartition("-")[2]))] = [(point[0] + offset[0], point[1] + offset[1]) for point in floor["corners"]]
    faces = {}
    for entry in plots:
        label = str(entry.get("plot") or "")
        if entry.get("kind") == "plot_shape" and label.isdigit():
            faces.setdefault(str(int(label) + shift), []).append(cad_links_check.counter_clockwise([(vertex[0], vertex[1]) for vertex in entry["vertices"]]))
    widths = [item["plot_width_m"] for item in profile["typologies"].values() if isinstance(item.get("plot_width_m"), (int, float))]
    low = min(widths) - settings["analysis"]["toleranceM"]
    high = settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0
    segments = cad_links_check.road_segments(pieces, settings["analysis"]["roadEdgeLayers"])
    setbacks = profile["ramp"]["front_setback"]
    xyz, triangles = surface
    rows = {row["plot"]: row for row in report["rows"]}
    results = []
    for plot in drawn([plot for plot in rows if plot in ramps], seed, sample):
        row, differs = rows[plot], []
        own = faces.get(plot) or []
        if len(own) != 1 or "corner" not in row:
            results.append({"plot": plot, "skipped": "no single face or no outline in the report"})
            continue
        found = cad_links_check.front(own[0], segments, low, high)
        depth = (setbacks[row["unit"]] if row["unit"] in setbacks else setbacks["default"]) / 1000.0
        normal, _, left_end, right_end = found
        planned = [left_end, right_end, (right_end[0] + depth * normal[0], right_end[1] + depth * normal[1]), (left_end[0] + depth * normal[0], left_end[1] + depth * normal[1])]
        gap = best_pairing(planned, ramps[plot])
        if abs(gap - row["corner"]) > CORNER_SLACK:
            differs.append("corner " + str(round(gap, 4)) + " against " + str(row["corner"]))
        here = sum(1 for corner in ramps[plot] if round13_check.plain_z(xyz, triangles, corner[0], corner[1]) is not None)
        there = sum(1 for corner in ramps[plot] if round13_check.plain_z(xyz, triangles, *nearest_on_roads(corner, segments)) is not None)
        if row.get("cover") != {"atCorner": here, "atCurb": there}:
            differs.append("cover " + str(here) + " and " + str(there) + " against " + str(row.get("cover")))
        results.append({"plot": plot, "differs": differs})
    return results


def length_band(gap):
    for label, limit in LENGTH_BANDS:
        if gap <= limit:
            return label
    return "worse"


def check_compare(report, cad_plan, past_plan, apart):
    """The counts of insertPlan, ffl, rotation and mirrored for the plots outside apart, by hand, against the report."""
    past = {entry["plot"]: entry for entry in past_plan["plots"]}
    counts = {"insertPlan": Counter(), "ffl": Counter(), "rotation": Counter(), "mirrored": Counter()}
    for entry in cad_plan["plots"]:
        other = past.get(entry["plot"])
        if other is None or entry["plot"] in apart:
            continue
        a, b = entry["insert"]["value"], other["insert"]["value"]
        counts["insertPlan"][length_band(math.hypot(a[0] - b[0], a[1] - b[1]))] += 1
        counts["ffl"][length_band(abs(entry["levels"]["ffl"]["value"] - other["levels"]["ffl"]["value"]))] += 1
        turn = abs(entry["rotationDeg"]["value"] - other["rotationDeg"]["value"]) % 360.0
        counts["rotation"]["equal" if min(turn, 360.0 - turn) <= 0.01 else "worse"] += 1
        counts["mirrored"]["equal" if entry["mirrored"]["value"] == other["mirrored"]["value"] else "worse"] += 1
    found = {name: dict(table) for name, table in counts.items()}
    return [{"field": name, "differs": [] if found[name] == report["counts"]["outside"].get(name) else [str(found[name]) + " against " + str(report["counts"]["outside"].get(name))]} for name in found]


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check the plan from CAD, the ramp outline and the plan compare a second way (R130).")
    parser.add_argument("part", choices=("levels", "ramps", "compare"))
    parser.add_argument("out_dir")
    parser.add_argument("--cad-plan")
    parser.add_argument("--past-plan")
    parser.add_argument("--analysis")
    parser.add_argument("--profile")
    parser.add_argument("--settings")
    parser.add_argument("--shift", type=int)
    parser.add_argument("--ramp-report")
    parser.add_argument("--prototypes")
    parser.add_argument("--baseplan")
    parser.add_argument("--roads", help="pieces.json of the road layout drawing")
    parser.add_argument("--surface", help="folder of the road surface with points.npy and faces.npy")
    parser.add_argument("--surfaces-config", help="config/road_surfaces.json for the ramp types")
    parser.add_argument("--compare-report")
    parser.add_argument("--offsets", help="config/prototype_offsets.json for the plots listed apart")
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--sample", type=int, default=SAMPLE)
    args = parser.parse_args(argv)
    if args.part == "levels":
        results = check_levels(read(args.cad_plan), read(args.past_plan), read(args.analysis), read(args.profile), args.shift, args.seed, args.sample)
    elif args.part == "ramps":
        folder = Path(args.surface)
        surface = (np.load(folder / "points.npy"), np.load(folder / "faces.npy"))
        results = check_ramps(read(args.ramp_report), read(args.prototypes), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.profile), read(args.settings),
                              set(read(args.surfaces_config)["rampTypes"]), surface, args.shift, args.seed, args.sample)
    else:
        apart = set(read(args.offsets)["plotFrame"].get("apart") or [])
        results = check_compare(read(args.compare_report), read(args.cad_plan), read(args.past_plan), apart)
    checked = [item for item in results if "differs" in item]
    agree = [item for item in checked if not item["differs"]]
    out = {"part": args.part, "seed": args.seed, "drawn": len(results), "checked": len(checked), "agree": len(agree),
           "skipped": [item for item in results if "skipped" in item], "disagree": [item for item in checked if item["differs"]],
           "halfMillimetre": [item["plot"] for item in checked if item.get("halfMillimetre")]}
    path = Path(args.out_dir) / ("cad_plan_check_" + args.part + ".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1), encoding="utf-8")
    print("%s seed %d, drawn %d, checked %d, agree %d, on a half millimetre %d" % (args.part, args.seed, len(results), len(checked), len(agree), len(out["halfMillimetre"])))
    return 0 if len(agree) == len(checked) else 1


if __name__ == "__main__":
    sys.exit(main())
