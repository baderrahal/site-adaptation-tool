"""The Generic and yard outlines and the ramp type and front levels measured on the past job, for the floors of the plan from CAD (R135, R136).

The Generic and yard outlines of each past plot are taken into the axes of its link (R121), so a mirrored unit lands on
the same shape as one drawn as is. They are grouped by unit, mirror flag and plot width, fitted on one half of each group
and tested on the other with the split of R98. The ramps give the slope along their depth per type, which becomes the slope
bands that pick a ramp type, and the level of their front corners against the road at the curb point. The values go to
config/floor_outlines.json, written by this script and never by hand. Lengths are in metres, slopes in percent.
"""
import argparse
import json
import math
import random
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import shapely
from shapely.geometry import Point, Polygon
from shapely.ops import unary_union
from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import link_summary
import link_table
import plan_from_past
import project_profile
import prototype_offsets
import ramp_levels
import road_lookup
import yard_rule

DEFAULT_PATH = Path(__file__).resolve().parent.parent / "config" / "floor_outlines.json"
SEED = 13
MIN_PLOTS = 4
OUTLINE_BANDS = (("within1mm", 0.001), ("within10mm", 0.010))
LEVEL_BANDS = (("within5mm", 0.005), ("within30mm", 0.030))
# The outline gap is the Hausdorff distance of the boundaries with every edge cut into pieces of this length, in metres
DENSIFY = 0.05
EPSILON = 1e-9
KINDS = ("generic", "yard")
NOTE = ("Measured from the {job} past job, not a rule of the client, written by python/floor_fit.py --write and never by hand (R135, R136). "
        "outlines: per floor kind one group per unit, mirror flag and plot width, the width being the front edge of the baseplan face (R122) rounded to cadLinks.floorWidthStepM. "
        "loops are in the axes of the link, x along its X axis and y along its Y axis from its origin, so the outline is placed by the insert, rotation and mirror flag of the link. "
        "type is the floor type most of the group carry. The loops are the outline of the plot of the fit half nearest all the others (the medoid), fitError and testError count the plots "
        "of each half by the Hausdorff distance of the boundaries, a group with tooFew true was fitted on all its plots and not tested. "
        "rampTypes: the slope of each past ramp along its depth, back corners over front corners, per type, and the bands that pick a type, cut halfway between the medians. "
        "A type whose median lies in the band of another is listed in notSeparable. rampFront: the median of the front corner level minus the road level at the curb point nearest the corner (R91, R94). "
        "Lengths are in metres, slopes in percent.")


def band_of(value, bands):
    for label, limit in bands:
        if value <= limit + EPSILON:
            return label
    return "worse"


def link_axes(rotation, mirrored):
    """The link X and Y axes in plan, Y turned by plus 90 degrees from X, by minus 90 when mirrored (R80)."""
    turn = math.radians(rotation)
    axis_x = (math.cos(turn), math.sin(turn))
    return axis_x, ((axis_x[1], -axis_x[0]) if mirrored else (-axis_x[1], axis_x[0]))


def to_link(points, origin, rotation, mirrored):
    axis_x, axis_y = link_axes(rotation, mirrored)
    return [((p[0] - origin[0]) * axis_x[0] + (p[1] - origin[1]) * axis_x[1], (p[0] - origin[0]) * axis_y[0] + (p[1] - origin[1]) * axis_y[1]) for p in points]


def to_world(points, origin, rotation, mirrored):
    axis_x, axis_y = link_axes(rotation, mirrored)
    return [(origin[0] + p[0] * axis_x[0] + p[1] * axis_y[0], origin[1] + p[0] * axis_x[1] + p[1] * axis_y[1]) for p in points]


def shape_of(loops):
    return unary_union([Polygon(loop) for loop in loops if len(loop) >= 3]).buffer(0)


def outline_gap(first, second):
    """The Hausdorff distance between the boundaries of two shapes, in metres."""
    return float(shapely.hausdorff_distance(first.boundary, second.boundary, densify=DENSIFY))


def width_key(width, step):
    return round(round(width / step) * step, 3)


def medoid(rows):
    """The row whose shape lies nearest every other shape of the rows, the largest gap being the least, a tie to the lower plot."""
    return min(rows, key=lambda row: (max(outline_gap(row["shape"], other["shape"]) for other in rows), int(row["plot"])))


def split_rows(rows, seed):
    ordered = sorted(rows, key=lambda row: int(row["plot"]))
    random.Random(seed).shuffle(ordered)
    cut = (len(ordered) + 1) // 2
    return ordered[:cut], ordered[cut:]


def band_table(gaps):
    table = Counter(band_of(gap, OUTLINE_BANDS) for gap in gaps)
    return {"within1mm": table["within1mm"], "within10mm": table["within1mm"] + table["within10mm"], "worse": table["worse"], "n": len(gaps)}


def fit_groups(rows, keys, seed):
    """R135. The groups of rows by keys, each fitted on its fit half and tested on its test half, or fitted on all when too few."""
    groups = defaultdict(list)
    for row in rows:
        groups[tuple(row[key] for key in keys)].append(row)
    fitted = []
    for key, members in sorted(groups.items(), key=lambda item: str(item[0])):
        entry = dict(zip(keys, key))
        entry.update({"plots": len(members), "tooFew": len(members) < MIN_PLOTS, "type": Counter(row["type"] for row in members).most_common(1)[0][0]})
        if entry["tooFew"]:
            best = medoid(members)
        else:
            fit, test = split_rows(members, seed)
            best = medoid(fit)
            entry["splitSeed"] = seed
            entry["fitError"] = band_table([outline_gap(best["shape"], row["shape"]) for row in fit])
            entry["testError"] = band_table([outline_gap(best["shape"], row["shape"]) for row in test])
        entry["fromPlot"] = best["plot"]
        entry["loops"] = [[[common.r3(x), common.r3(y)] for x, y in loop] for loop in best["loops"]]
        fitted.append(entry)
    return fitted


def plot_gaps(rows, groups, keys):
    """R135. The gap of every plot to the fitted outline of its group, for the second method of R139."""
    fitted = {tuple(group[key] for key in keys): shape_of(group["loops"]) for group in groups}
    return [{"plot": row["plot"], "gap": outline_gap(fitted[tuple(row[key] for key in keys)], row["shape"])} for row in rows]


def totals(groups):
    total = Counter()
    for group in groups:
        if group["tooFew"]:
            total["tooFewPlots"] += group["plots"]
            continue
        for half in ("fitError", "testError"):
            for label in ("within1mm", "within10mm", "worse", "n"):
                total[half + " " + label] += group[half][label]
    return dict(sorted(total.items()))


def outline_rows(plan, kind, faces, frames, apart, step):
    """The past outlines of one floor kind in link axes, with unit, mirror flag, plot width and type, the plots of apart left out."""
    rows = []
    for entry in plan["plots"]:
        plot = entry["plot"]
        floor = (entry.get("floors") or {}).get(kind)
        frame = frames.get(plot)
        if plot in apart or floor is None or frame is None:
            continue
        insert, rotation, mirrored = entry["insert"]["value"], entry["rotationDeg"]["value"], entry["mirrored"]["value"]
        loops = [to_link(loop["points"], insert, rotation, mirrored) for loop in floor["outline"]["value"]["loops"]]
        rows.append({"plot": plot, "unit": entry["unit"]["value"], "mirrored": mirrored, "width": width_key(frame["length"], step),
                     "type": floor["type"]["value"], "loops": loops, "shape": shape_of(loops)})
    return rows


def face_test(plan, faces, apart):
    """R135. Every past Generic floor against its whole baseplan face, the plots of apart left out."""
    gaps, worse = [], []
    for entry in plan["plots"]:
        floor = (entry.get("floors") or {}).get("generic")
        if entry["plot"] in apart or floor is None or entry["plot"] not in faces:
            continue
        gap = outline_gap(shape_of([loop["points"] for loop in floor["outline"]["value"]["loops"]]), faces[entry["plot"]])
        gaps.append(gap)
        if band_of(gap, OUTLINE_BANDS) == "worse":
            worse.append({"plot": entry["plot"], "gap": gap})
    return {"bands": band_table(gaps), "worse": worse}


def front_and_back(corners, frame):
    """The two front corners, nearer the front edge, and the two back corners of a ramp, by their depth along the inward normal."""
    depth = lambda corner: (corner[0] - frame["left"][0]) * frame["n"][0] + (corner[1] - frame["left"][1]) * frame["n"][1]
    ordered = sorted(corners, key=depth)
    return ordered[:2], ordered[2:], depth


def along_slope(front, back, depth):
    """R136. The slope along the depth in percent, the mean back level over the mean front level across the mean depth."""
    run = statistics.mean(depth(corner) for corner in back) - statistics.mean(depth(corner) for corner in front)
    rise = statistics.mean(corner[2] for corner in back) - statistics.mean(corner[2] for corner in front)
    return rise / run * 100.0 if run > 0 else None


def type_bands(slopes):
    """R136. Per type the ramps and the median, least and largest slope, and the bands cut halfway between the medians in rising
    order. A type whose median lies within the band of another, two medians within 0.1 percent, is not separable and the type
    with more ramps keeps the band."""
    measured = {name: {"ramps": len(values), "median": statistics.median(values), "least": min(values), "largest": max(values)} for name, values in sorted(slopes.items()) if values}
    ordered = sorted(measured, key=lambda name: (measured[name]["median"], -measured[name]["ramps"]))
    kept, apart = [], []
    for name in ordered:
        if kept and abs(measured[name]["median"] - measured[kept[-1]]["median"]) < 0.1:
            if measured[name]["ramps"] > measured[kept[-1]]["ramps"]:
                apart.append(kept.pop())
                kept.append(name)
            else:
                apart.append(name)
            continue
        kept.append(name)
    bands = []
    for index, name in enumerate(kept):
        upper = (measured[name]["median"] + measured[kept[index + 1]]["median"]) / 2.0 if index + 1 < len(kept) else None
        bands.append({"type": name, "belowPercent": None if upper is None else common.r3(upper)})
    return {"measured": measured, "bands": bands, "notSeparable": apart}


def type_of(slope, bands):
    """R136. The ramp type of a slope from the bands, the first whose upper limit lies above it."""
    for band in bands:
        if band["belowPercent"] is None or slope < band["belowPercent"]:
            return band["type"]
    return None


def curb_level(point, roads, road_tree, lookup):
    """The road level at the point of the nearest road edge line nearest to a point, with that curb point (R94)."""
    line = roads[int(road_tree.query_nearest(Point(point))[0])]
    curb = line.interpolate(line.project(Point(point)))
    return lookup.level_at(curb.x, curb.y)[0], (curb.x, curb.y)


def measure_ramps(plan, frames, roads, lookup, apart):
    """R136. The slope of every past ramp per type and the front corner level against the road at its curb point."""
    road_tree = STRtree(roads)
    slopes, offsets, rows = defaultdict(list), [], []
    for entry in plan["plots"]:
        ramp, frame = (entry.get("floors") or {}).get("ramp"), frames.get(entry["plot"])
        if entry["plot"] in apart or ramp is None or frame is None:
            continue
        front, back, depth = front_and_back(ramp["corners"]["value"], frame)
        slope = along_slope(front, back, depth)
        row = {"plot": entry["plot"], "type": ramp["type"]["value"], "slope": slope, "front": []}
        if slope is not None:
            slopes[row["type"]].append(slope)
        for corner in front:
            road, _ = curb_level(corner[:2], roads, road_tree, lookup)
            if road is not None:
                offsets.append(corner[2] - road)
                row["front"].append(corner[2] - road)
        rows.append(row)
    result = type_bands(slopes)
    offset = statistics.median(offsets) if offsets else None
    picks = Counter((row["type"], type_of(row["slope"], result["bands"])) for row in rows if row["slope"] is not None)
    result["picks"] = {past + " as " + str(picked): count for (past, picked), count in sorted(picks.items())}
    front = {"offsetM": offset, "corners": len(offsets), "within5mm": sum(1 for value in offsets if abs(value - offset) <= 0.005 + EPSILON),
             "within30mm": sum(1 for value in offsets if abs(value - offset) <= 0.030 + EPSILON)} if offsets else {"offsetM": None, "corners": 0}
    return result, front, rows


def load(path=DEFAULT_PATH):
    """The floor outlines file. A missing file or a missing section raises ProfileError."""
    path = Path(path)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise project_profile.ProfileError(path.name + " cannot be read: " + str(error)) from error
    for section in ("outlines", "rampTypes", "rampFront"):
        if not isinstance(data, dict) or section not in data:
            raise project_profile.ProfileError(path.name + " has no section " + section)
    return data


def outline_for(data, kind, unit, mirrored, width):
    """R137. The fitted group of a floor kind for a unit, mirror flag and plot width, or None."""
    for group in data["outlines"].get(kind) or []:
        if group["unit"] == unit and group["mirrored"] == mirrored and abs(group["width"] - width) < EPSILON:
            return group
    return None


def width_step(settings):
    block = cad_links.cad_links_settings(settings)
    step = block.get("floorWidthStepM")
    if not plan_from_past.is_number(step) or step <= 0:
        raise project_profile.ProfileError(settings.path.name + " cadLinks.floorWidthStepM must be a length above 0")
    return step


def main(argv=None):
    parser = argparse.ArgumentParser(description="Measure the floor outlines, the ramp type bands and the ramp front level on the past job (R135, R136).")
    parser.add_argument("out_dir", help="where floor_fit.json is written, keep it outside git")
    parser.add_argument("--roads", required=True)
    parser.add_argument("--landxml-dir", required=True)
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--surfaces", default=str(ramp_levels.DEFAULT_CONFIG))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    parser.add_argument("--out", default=str(DEFAULT_PATH))
    parser.add_argument("--write", action="store_true", help="write config/floor_outlines.json")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        analysis = settings.get("analysis")
        job = plan_from_past.load_job(settings)
        step = width_step(settings)
        shift = link_table.load_shifts(args.shifts)["shifts"][job["neighbourhood"]]
        faces, _ = yard_rule.face_shapes(link_summary.load(Path(args.baseplan or project_profile.ROOT / job["baseplanPlots"]))["plots"], shift)
        plan = link_summary.load(job["output"])
        roads = cad_analysis.read_road_edges(args.roads, analysis["roadEdgeLayers"])
        road_tree = STRtree(roads)
        limits = cad_links.front_limits(profile, analysis)
        frames = {entry["plot"]: cad_links.front_frame(faces.get(entry["plot"]), road_tree, roads, limits) for entry in plan["plots"]}
        apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
        report, outlines = {"apart": sorted(apart, key=int), "widthStepM": step}, {}
        for kind in KINDS:
            rows = outline_rows(plan, kind, faces, frames, apart, step)
            outlines[kind] = fit_groups(rows, ("unit", "mirrored", "width"), SEED)
            report[kind] = {"byUnitMirrorWidth": totals(outlines[kind]), "byUnitMirror": totals(fit_groups(rows, ("unit", "mirrored"), SEED)), "groups": len(outlines[kind]),
                            "plotGaps": plot_gaps(rows, outlines[kind], ("unit", "mirrored", "width"))}
        report["genericAgainstFace"] = face_test(plan, faces, apart)
        lookup = road_lookup.load(ramp_levels.load_config(args.surfaces), args.landxml_dir)
        ramp_types, front, ramp_rows = measure_ramps(plan, frames, roads, lookup, apart)
        report["rampTypes"], report["rampFront"], report["ramps"] = ramp_types, front, ramp_rows
        common.write_json(Path(args.out_dir) / "floor_fit.json", report)
        if args.write:
            common.write_json(args.out, {"note": NOTE.format(job=job["neighbourhood"]), "outlines": outlines, "rampTypes": ramp_types, "rampFront": front})
        for kind in KINDS:
            print("%s by unit, mirror and width %s" % (kind, report[kind]["byUnitMirrorWidth"]))
            print("%s by unit and mirror %s" % (kind, report[kind]["byUnitMirror"]))
        print("generic against its face %s" % report["genericAgainstFace"]["bands"])
        print("ramp bands %s, picks %s" % (ramp_types["bands"], ramp_types["picks"]))
        print("ramp front %s" % front)
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("floor_fit: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
