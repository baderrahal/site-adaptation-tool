"""R469. Round 6B-DATA. The plan of a zone by the tool against the plan of its answer key (R325), per plot, with no Revit.

Compared on each plot both plans hold: villa type, mirror, rotation, FFL, El @ Entry, steps, the ramp corners by level and in plan, and the
walls in both directions, the ends of each wall of one plan against the nearest wall line of the other among the walls of that plot, since a
key can hold one wall in pieces. A gap is a match, within its limit or over it. A field the key does not hold, or one majority.kindLeaveOut
leaves out for the kind of the key, has no reference and is no gap. A value only the key holds is toolNone. In one direction of the walls, a plan
with no wall on the plot has nothing to test, no reference, and a wall with no wall of the other plan on the plot is over the limit, no gap.
Both plans are written to 3 decimals, so a level gap is whole millimetres and the match of 0.5 mm means equal to the mm.

The tool plan comes in two builds. The override list was made from these keys, so the build with it agrees partly by construction, and the
build without it is the test of the rules. Written outside any git work tree: the report and the 10 random plots with their values. The
Markdown holds counts, gaps and plot numbers, no level and no coordinate, for steps/investigations/6b-data.md. Changes nothing, sets no rule.

With --rev6-plan the first build is a plan of rev6 with no override list, the mode a new project runs, beside the past job with none. Its plan comes
from a config copy outside the repo, --settings stays the config of the repo for the limits, and a plan with no summary.noDoorLevel is refused.
"""
import argparse
import csv
import math
import re
import sys
from pathlib import Path

import common
import design_basis
import key_plans_check
import link_summary
import plan_compare
import plot_relations
import project_profile

WORST = 10
RANDOM = 10
EPS = 1e-9
MATCH, WITHIN, OVER, NO_REFERENCE, TOOL_NONE = "match", "withinLimit", "overLimit", "noReference", "toolNone"
STATUSES = (MATCH, WITHIN, OVER, NO_REFERENCE, TOOL_NONE)
BUILDS = ("with", "without")
REV6_BUILDS = ("rev6", "without")
TITLES = {"with": "with overrides", "without": "without overrides", "rev6": "rev6, no override list"}
READS = {"villaType": lambda entry: entry["unit"]["value"], "mirror": lambda entry: entry["mirrored"]["value"],
         "rotation": lambda entry: entry["rotationDeg"]["value"], "ffl": lambda entry: entry["levels"]["ffl"]["value"],
         "entry": lambda entry: entry["levels"]["entry"]["value"], "steps": lambda entry: entry["levels"]["steps"]["value"],
         "corners": lambda entry: entry["floors"]["ramp"]["corners"]["value"]}
SAME_FIELDS = ("villaType", "mirror")
NUMBER_FIELDS = ("rotation", "ffl", "entry", "steps")
CORNER_FIELDS = ("rampCornersLevel", "rampCornersPlan")
WALL_FIELDS = ("wallsToolOnKey", "wallsKeyOnTool")
FIELDS = SAME_FIELDS + NUMBER_FIELDS + CORNER_FIELDS + WALL_FIELDS
LEAVE_OUT = {"ffl": "ffl", "entry": "entry", "steps": "steps", "rampCornersLevel": "rampFront", "rampCornersPlan": "rampFront"}
SHOWN = {"rotation": (1.0, "deg", 2), "steps": (1.0, "", 0)}
METRES_SHOWN = (1000.0, "mm", 1)
THICKNESS = re.compile(r"(?<![0-9])([0-9]{3})(?![0-9])")


def limits_of(settings, chosen, boundary_m=None):
    """R469. The match and the limit of each number field: the levels by majority, the ramp corners by R138, the rotation match by R129, the wall
    limit by keyPlans. The three that no rule holds come from the command line, so the report names them as chosen."""
    majority = settings["majority"]
    level = {"match": majority["levelPassMm"] / 1000.0, "within": majority["levelHoldMm"] / 1000.0}
    corners, outline = dict(plan_compare.CORNER_BANDS), dict(plan_compare.OUTLINE_BANDS)
    return {"rotation": {"match": plan_compare.ANGLE_TOLERANCE, "within": chosen["rotationWithinDeg"]}, "ffl": level, "entry": level,
            "steps": {"match": 0, "within": chosen["stepsWithin"]},
            "rampCornersLevel": {"match": corners["within5mm"], "within": corners["within30mm"]},
            "rampCornersPlan": {"match": outline["within1mm"], "within": outline["within10mm"]},
            "wallLine": {"centreCodes": majority.get("wallCentreCodes") or [], "boundaryM": boundary_m or 0.0},
            **{field: {"match": chosen["wallMatchM"], "within": settings["keyPlans"]["wallEdgeM"]} for field in WALL_FIELDS}}


def status_of(limit, gap):
    if gap <= limit["match"] + EPS:
        return MATCH
    return WITHIN if gap <= limit["within"] + EPS else OVER


def plan_corner_gap(mine, theirs):
    """The largest distance in plan of a corner to the nearest corner of the other floor, both ways."""
    one_way = lambda first, second: max(min(math.dist(corner[:2], other[:2]) for other in second) for corner in first)
    return max(one_way(mine, theirs), one_way(theirs, mine))


def number_gap(field, mine, theirs):
    return plan_compare.angle_gap(mine, theirs) if field == "rotation" else abs(mine - theirs)


def tool_lines(plan):
    found = {}
    for wall in plan.get("walls") or []:
        for plot in wall["plots"]["value"]:
            found.setdefault(plot, []).append([point[:2] for point in wall["ends"]["value"]])
    return found


def half_of(wall, walls):
    """R478. How far the location line of a key wall lies from its centre: none on a centre code or with no code, else half its thickness, the
    number in its type name in mm, such as Upsand - 330, else the boundary wall of the profile (R335)."""
    if wall.get("locationLine") is None or wall["locationLine"] in walls["centreCodes"]:
        return 0.0
    found = THICKNESS.search(str(wall.get("type") or ""))
    return int(found.group(1)) / 2000.0 if found else walls["boundaryM"] / 2.0


def key_lines(plan, walls):
    found = {}
    for wall in plan.get("walls") or []:
        if wall.get("start") is None or wall.get("end") is None:
            continue
        for plot in wall["plots"]:
            found.setdefault(plot, []).append([wall["start"][:2], wall["end"][:2], half_of(wall, walls)])
    return found


def centre_gap(point, line, half):
    """R478. The distance of a point to the centre of a wall drawn half off its line, square to the line and past its end apart, so a point past the
    end is not read nearer by half. The read of a key holds no side of the wall, so this takes the side nearer the point, the best case, and a centre
    on the far side would read up to twice half less than it is (Q365 keeps that open)."""
    (ax, ay), (bx, by) = line[0], line[1]
    dx, dy = bx - ax, by - ay
    length = math.hypot(dx, dy)
    if length == 0.0:
        return abs(math.hypot(point[0] - ax, point[1] - ay) - half)
    along = (point[0] - ax) * dx / length + (point[1] - ay) * dy / length
    square = abs((point[0] - ax) * dy - (point[1] - ay) * dx) / length
    return math.hypot(max(0.0, -along, along - length), abs(square - half))


def wall_gap(mine, theirs):
    """The largest distance of an end of a wall of mine to the nearest wall line of theirs, by the wall centres (R478): a key line on a face stands
    half a thickness from its centre, and a tool line is its centre. None when theirs holds no wall for walls of mine."""
    if not theirs:
        return None
    half = lambda line: line[2] if len(line) > 2 else 0.0
    return max(min(centre_gap(point, line, half(line) + half(wall)) for line in theirs) for wall in mine for point in wall[:2])


def field_row(field, mine, theirs, left_out, limits):
    """One field of one plot: its status and its gap, None for a field of no gap."""
    if LEAVE_OUT.get(field) in left_out:
        return {"status": NO_REFERENCE, "gap": None}
    if theirs is None:
        return {"status": NO_REFERENCE, "gap": None}
    if mine is None:
        return {"status": TOOL_NONE, "gap": None}
    if field in SAME_FIELDS:
        return {"status": MATCH if mine == theirs else OVER, "gap": None}
    if field == "rampCornersLevel":
        gap = plan_compare.corner_gap(mine, theirs)
    elif field == "rampCornersPlan":
        gap = plan_corner_gap(mine, theirs)
    else:
        gap = number_gap(field, mine, theirs)
    return {"status": status_of(limits[field], gap), "gap": gap}


def same_file(entry, other):
    read = lambda item: (item.get("prototypeFile") or {}).get("value") if isinstance(item.get("prototypeFile"), dict) else item.get("prototypeFile")
    return read(entry) is not None and read(entry) == read(other)


def plot_rows(entry, other, left_out, limits, tool_walls, key_walls):
    read = lambda name, item: plan_compare.value_of(item, READS[name])
    rows = {field: field_row(field, read(field, entry), read(field, other), left_out, limits) for field in SAME_FIELDS + NUMBER_FIELDS}
    for field in CORNER_FIELDS:
        rows[field] = field_row(field, read("corners", entry), read("corners", other), left_out, limits)
    # R479. TH1-IM and TH1 are one villa type where both plans give one prototype file, the 6 of 18 plots whose file differs stay over
    if rows["villaType"]["status"] == OVER and same_file(entry, other):
        rows["villaType"] = {"status": MATCH, "gap": None, "sameFile": True}
    plot = entry["plot"]
    for field, mine, theirs in (("wallsToolOnKey", tool_walls.get(plot), key_walls.get(plot)), ("wallsKeyOnTool", key_walls.get(plot), tool_walls.get(plot))):
        if not mine:
            rows[field] = {"status": NO_REFERENCE, "gap": None}
            continue
        gap = wall_gap(mine, theirs)
        rows[field] = {"status": OVER if gap is None else status_of(limits[field], gap), "gap": gap}
    return rows


def worst_of(per_plot, field):
    """The plots off the match, a plot of no gap first, then the largest gap, then plot order."""
    found = [{"plot": plot, **rows[field]} for plot, rows in per_plot.items() if rows[field]["status"] in (WITHIN, OVER)]
    found.sort(key=lambda row: (row["gap"] is not None, -(row["gap"] or 0.0), link_summary.sort_key(row["plot"])))
    return found[:WORST]


def compare_zone(tool, key, kind, limits, left_out):
    """R469. Every plot both plans hold, field by field, with the counts and the worst plots of each field. kind only names the key in the report."""
    theirs = {entry["plot"]: entry for entry in key["plots"]}
    mine = {entry["plot"]: entry for entry in tool["plots"]}
    tool_walls, key_walls = tool_lines(tool), key_lines(key, limits["wallLine"])
    shared = sorted(set(mine) & set(theirs), key=link_summary.sort_key)
    per_plot = {plot: plot_rows(mine[plot], theirs[plot], left_out, limits, tool_walls, key_walls) for plot in shared}
    counts = {field: {} for field in FIELDS}
    for rows in per_plot.values():
        for field in FIELDS:
            counts[field][rows[field]["status"]] = counts[field].get(rows[field]["status"], 0) + 1
    return {"kind": kind, "compared": len(shared), "onlyInTool": sorted(set(mine) - set(theirs), key=link_summary.sort_key),
            "onlyInKey": sorted(set(theirs) - set(mine), key=link_summary.sort_key), "counts": counts,
            "worst": {field: worst_of(per_plot, field) for field in FIELDS}, "perPlot": per_plot}


def random_plots(plots, seed):
    return key_plans_check.sample(plots, seed, RANDOM)


def check_mode(settings):
    if design_basis.mode_of(settings) == design_basis.REV6:
        raise ValueError("designBasis.mode is %s, round 6B-DATA compares the past job mode only (Q224)" % design_basis.REV6)


def check_rev6(plan):
    """R408. Only a build under rev6 writes summary.noDoorLevel, a plan names its mode nowhere else."""
    if "noDoorLevel" not in (plan.get("summary") or {}):
        raise ValueError("--rev6-plan holds no summary.noDoorLevel, so it is no build under rev6 (R408)")


def kind_of(settings, code):
    for key in settings["answerKeys"]["keys"]:
        if key["code"] == code:
            return key["kind"]
    raise ValueError("no answer key %s in answerKeys" % code)


def shown_gap(field, row):
    """A gap as the Markdown writes it: millimetres, degrees or steps, never a level."""
    if row["gap"] is None:
        return {OVER: "differs" if field in SAME_FIELDS else "no wall", NO_REFERENCE: "no ref", TOOL_NONE: "tool none", MATCH: "same"}[row["status"]]
    scale, unit, decimals = SHOWN.get(field, METRES_SHOWN)
    return ("%." + str(decimals) + "f%s") % (row["gap"] * scale, " " + unit if unit else "")


def value_cell(entry, name):
    return "" if entry is None else str(plan_compare.value_of(entry, READS[name]))


def title_of(build):
    return TITLES[build][0].upper() + TITLES[build][1:]


def side_by_side(rows_of, plans, plots):
    """The 10 random plots with the value of each field in each build and the key, and the gaps of the fields that are no one value."""
    rows = []
    for plot in plots:
        row = {"plot": plot}
        for name in SAME_FIELDS + NUMBER_FIELDS:
            for build in rows_of:
                row["%s_%s" % (name, build)] = value_cell(plans[build].get(plot), name)
            row[name + "_key"] = value_cell(plans["key"].get(plot), name)
        for name in CORNER_FIELDS + WALL_FIELDS:
            for build in rows_of:
                row["%s_%s_gap" % (name, build)] = rows_of[build][plot][name]["gap"]
                row["%s_%s_status" % (name, build)] = rows_of[build][plot][name]["status"]
        rows.append(row)
    return rows


def count_cell(counts):
    return " ".join(str(counts.get(status, 0)) for status in STATUSES)


def markdown(code, report):
    """The section of one zone: counts, worst plots and random plots, with gaps and plot numbers only."""
    builds = report["builds"]
    names = list(builds)
    first, second = builds[names[0]], builds[names[1]]
    lines = ["### %s (%s)" % (code, report["kind"]), ""]
    lines += ["%s: plots compared %d, only in the tool plan %d, only in the key %d." % (title_of(build), builds[build]["compared"], len(builds[build]["onlyInTool"]), len(builds[build]["onlyInKey"])) for build in names]
    lines += ["", "Counts are match, within limit, over limit, no reference, tool none.", "",
              "| Field | %s | %s |" % (title_of(names[0]), title_of(names[1])), "|---|---|---|"]
    lines += ["| %s | %s | %s |" % (field, count_cell(first["counts"][field]), count_cell(second["counts"][field])) for field in FIELDS]
    for build in names:
        lines += ["", "Worst 10 per field, %s (plot, gap):" % TITLES[build], ""]
        for field in FIELDS:
            worst = builds[build]["worst"][field]
            lines.append("- %s: %s" % (field, ", ".join("%s %s" % (row["plot"], shown_gap(field, row)) for row in worst) if worst else "none"))
    lines += ["", "Random plots at seed %d, the gap %s then %s. The values are in side_by_side_%s.csv outside git." % (report["seed"], TITLES[names[0]], TITLES[names[1]], code.lower()), "",
              "| Plot | " + " | ".join(FIELDS) + " |", "|---" * (len(FIELDS) + 1) + "|"]
    for plot in report["randomPlots"]:
        cells = ["%s / %s" % (shown_gap(field, first["perPlot"][plot][field]), shown_gap(field, second["perPlot"][plot][field])) for field in FIELDS]
        lines.append("| %s | %s |" % (plot, " | ".join(cells)))
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the tool plans of one zone with the plan of its answer key.")
    parser.add_argument("code", help="the code of the key in answerKeys")
    first = parser.add_mutually_exclusive_group(required=True)
    first.add_argument("--with-overrides", help="plan.json of plan_from_book.py with the override list")
    first.add_argument("--rev6-plan", help="plan.json of plan_from_book.py under rev6 with no override list")
    parser.add_argument("--without-overrides", required=True, help="plan.json of plan_from_book.py with no override list")
    parser.add_argument("--key-plan", required=True, help="the plan of plan_from_past.py --answer-key --key")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--rotation-within-deg", type=float, required=True, help="chosen, no rule holds this limit")
    parser.add_argument("--steps-within", type=int, required=True, help="chosen, no rule holds this limit")
    parser.add_argument("--wall-match-m", type=float, required=True, help="chosen, no rule holds this match")
    parser.add_argument("--seed", type=int, required=True)
    parser.add_argument("--out", required=True, help="a folder outside any git work tree")
    args = parser.parse_args(argv)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    plot_relations.refuse_inside_repo(out)
    settings = link_summary.load(args.settings)
    check_mode(settings)
    kind = kind_of(settings, args.code)
    chosen = {"rotationWithinDeg": args.rotation_within_deg, "stepsWithin": args.steps_within, "wallMatchM": args.wall_match_m}
    # R478. A config with no profile, as a made up one of the tests, holds no boundary wall to take half of
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings(args.settings))) if settings.get("profile") else None
    limits = limits_of(settings, chosen, profile.metres("plot.boundary_wall_thickness") if profile else None)
    left_out = settings["majority"].get("kindLeaveOut", {}).get(kind, [])
    key = link_summary.load(args.key_plan)
    names = REV6_BUILDS if args.rev6_plan else BUILDS
    tools = {names[0]: link_summary.load(args.rev6_plan or args.with_overrides), "without": link_summary.load(args.without_overrides)}
    if args.rev6_plan:
        check_rev6(tools[names[0]])
    builds = {build: compare_zone(tools[build], key, kind, limits, left_out) for build in names}
    # A build can write fewer plots than the other, a random plot is shown in both
    plots = random_plots(list(set(builds[names[0]]["perPlot"]) & set(builds["without"]["perPlot"])), args.seed)
    report = {"code": args.code, "kind": kind, "seed": args.seed, "limits": limits, "chosen": chosen, "randomPlots": plots, "builds": builds}
    plans = {build: {entry["plot"]: entry for entry in tools[build]["plots"]} for build in names}
    plans["key"] = {entry["plot"]: entry for entry in key["plots"]}
    name = args.code.lower()
    # A limit of 0.5 mm and the gaps at it must stay exact, write_json rounds to 3 decimals
    common.write_json(out / ("zone_compare_%s.json" % name), report, kept_keys={"limits", "chosen", "gap"})
    rows = side_by_side({build: builds[build]["perPlot"] for build in names}, plans, plots)
    with (out / ("side_by_side_%s.csv" % name)).open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]) if rows else ["plot"])
        writer.writeheader()
        writer.writerows(rows)
    (out / ("zone_%s.md" % name)).write_text(markdown(args.code, report), encoding="utf-8")
    print("%s compared %d, only in the tool plan %d, only in the key %d" % (args.code, builds[names[0]]["compared"], len(builds[names[0]]["onlyInTool"]), len(builds[names[0]]["onlyInKey"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
