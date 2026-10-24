"""Builds plan.json, the one file the placer reads, from a past job: prototypes.json of the past model and its schedule (R80, R81).

Nothing is guessed. A value the sources do not give is left out and its plot is listed in missing with the reason.
Two sources that give a value differently are listed in conflicts with the value kept and the other one.
"""
import argparse
import math
import os
import re
import sys
from collections import Counter
from pathlib import Path

import common
import excel_reader
import full_run
import link_summary
import plot_relations
import project_profile
import ramp_levels

FORMAT = "site-adaptation-plan"
FORMAT_VERSION = 1
PAST = "pastJob"
COMPUTED = "computed"
ROOT_VARIABLE = "SITE_ADAPTATION_INPUT"
PLOT_PARAMETER = "RRE_ZZ_SP_Plot-Number"
UNIT_PARAMETER = "RRE_ZZ_SP_Prototype"
STEPS_PARAMETER = "RRE_ZZ_SP_Number-of-Steps"
FFL_PARAMETER = "RRE_ZZ_SP_Prototype-FFL"
CASE_PARAMETER = "CASE"
JOB_KEYS = ("neighbourhood", "schedule", "sheet", "prototypes", "prototypeFolder", "output", "parameters")
# The yard floor sits this far below the FFL, in metres (Bader, 24 Oct)
YARD_BELOW_FFL = 0.100
# The link axes are written to 3 decimals, so a turned axis within this of the expected one is taken as exact
AXIS_TOLERANCE = 0.002
LEVEL_CONFLICT = 0.0015
TOP_CONFLICT = 0.005
PROOF_LIMIT_MM = 1.0
EPS = 1e-9
# Without these values the placer cannot place the link, so a plot that lacks one is not written
LINK_KEYS = ("prototypeFile", "insert", "rotationDeg", "mirrored", "levels.ffl")
# A step count is a whole number, alone or before the word step or steps, so 2.5 is not read as 2
STEPS_TEXT = re.compile(r"^\s*(\d+)\s*(steps?)?\s*$", re.IGNORECASE)
LEVEL_COLUMNS = (("ffl", "FFL"), ("ssl", "SSL"), ("entry", "El @ Entry"), ("height", "Height"))
WIDE_LEVELS = ("entry", "height")


def sourced(value, source=PAST):
    return {"value": value, "source": source}


def point(values):
    return [common.r3(value) for value in values]


def load_job(settings):
    """The pastJob block of the project settings, with every key present. A parameter list that names the FFL parameter is refused."""
    job = settings.get("pastJob")
    if not isinstance(job, dict):
        raise project_profile.ProfileError(settings.path.name + " has no pastJob block")
    missing = [key for key in JOB_KEYS if not job.get(key)]
    if missing:
        raise project_profile.ProfileError(settings.path.name + " pastJob has no " + ", ".join(missing))
    if job["schedule"] not in (settings.get("schedules") or []):
        raise project_profile.ProfileError(settings.path.name + " pastJob schedule is not one of schedules")
    if FFL_PARAMETER in job["parameters"]:
        raise project_profile.ProfileError(settings.path.name + " pastJob parameters name " + FFL_PARAMETER + ", which is never overwritten")
    return job


def input_root(settings):
    return Path(os.environ.get(ROOT_VARIABLE) or settings.get("inputRoot"))


def rings_of(shape):
    """The closed rings of a plan shape as [E, N] lists, outer ring then holes, first point not repeated, read even odd."""
    if shape is None:
        return None
    polygons = list(shape.geoms) if hasattr(shape, "geoms") else [shape]
    rings = []
    for polygon in polygons:
        for ring in [polygon.exterior] + list(polygon.interiors):
            rings.append([point(xy) for xy in list(ring.coords)[:-1]])
    return rings or None


def axes_fit(transform):
    """True when the Z axis is up and the Y axis is the X axis turned by plus 90 degrees, or minus 90 when mirrored."""
    turn = math.radians(transform["rotationDeg"])
    sign = -1 if transform["mirrored"] else 1
    expected = {
        "axisX": (math.cos(turn), math.sin(turn), 0.0),
        "axisY": (-sign * math.sin(turn), sign * math.cos(turn), 0.0),
        "axisZ": (0.0, 0.0, 1.0),
    }
    for key, wanted in expected.items():
        found = transform.get(key)
        if not found or len(found) != 3 or max(abs(a - b) for a, b in zip(found, wanted)) > AXIS_TOLERANCE:
            return False
    return True


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def plot_number(value):
    """A plot number as text when it is a whole number, held as a number or as digits, else None, so 101.5 never joins 101."""
    if is_number(value):
        return str(int(value)) if float(value).is_integer() else None
    text = str(value).strip() if value is not None else ""
    return str(int(text)) if text.isascii() and text.isdigit() else None


def shared_origin(link, offset):
    """The link origin in shared coordinates, the transform origin plus the survey point offset of R51. None unless it has three numbers."""
    origin = (link.get("transform") or {}).get("origin")
    if not isinstance(origin, list) or len(origin) != 3 or not all(is_number(value) for value in origin):
        return None
    return [origin[axis] + offset[axis] for axis in range(3)]


def plan_rotation(degrees):
    """R2 rounding can turn 359.9996 into 360, which R80 writes as 0."""
    rounded = common.r3(degrees % 360.0)
    return 0 if rounded >= 360 else rounded


def position_proof(links, offset):
    """R82. The largest gap in mm between the worked out origin and transformShared, over the links that have both."""
    largest, count, over = 0.0, 0, []
    for link in links:
        shared = (link.get("transformShared") or {}).get("origin")
        mine = shared_origin(link, offset)
        if not shared or not mine:
            continue
        count += 1
        gap = max(abs(mine[axis] - shared[axis]) for axis in range(3)) * 1000.0
        largest = max(largest, gap)
        if gap > PROOF_LIMIT_MM + EPS:
            over.append((link, gap))
    return {"links": count, "largestMm": common.r3(largest)}, over


def host_of(data):
    """The project document of prototypes.json. Unlike the ramp code it does not need floors, a plan can lack them."""
    for document in data.get("documents") or []:
        if document.get("kind") == "project":
            return document
    raise ValueError("no project document in the file")


def floor_index(document, offset):
    """The Generic and ramp floors by plot number, the plan shape of every floor and the yard search of R58."""
    floors = document.get("floors") or []
    shapes = {floor["elementId"]: plot_relations.floor_shape(floor, offset) for floor in floors}
    by_plot, bad = {}, []
    for floor in floors:
        kind = link_summary.floor_kind(floor)
        named = link_summary.floor_plot(floor)
        if kind not in ("generic", "ramp") or named is None:
            continue
        plot = plot_number(named)
        if plot is None:
            bad.append({"elementId": floor.get("elementId"), "plotNumber": str(named)})
            continue
        by_plot.setdefault(plot, {}).setdefault(kind, []).append(floor)
    yards, tree = plot_relations.yard_index(floors, shapes)
    return {"byPlot": by_plot, "shapes": shapes, "yards": yards, "tree": tree, "badFloors": bad}


class PlotBuilder:
    """Collects one plot entry with what it lacks and what disagrees."""

    def __init__(self, plot):
        self.plot = plot
        self.entry = {"plot": plot}
        self.missing = []
        self.conflicts = []

    def lack(self, key, why):
        self.missing.append({"key": key, "why": why})

    def clash(self, field, kept, other, other_source):
        self.conflicts.append({"plot": self.plot, "field": field, "kept": kept, "other": other, "otherSource": other_source})

    def has(self, dotted):
        node = self.entry
        for part in dotted.split("."):
            if not isinstance(node, dict) or part not in node:
                return False
            node = node[part]
        return True


def link_values(builder, link, offset, job, folder_files):
    parameters = link.get("parameters") or {}
    for key, name in (("unit", UNIT_PARAMETER), ("case", CASE_PARAMETER)):
        value = parameters.get(name)
        if isinstance(value, str) and value.strip():
            builder.entry[key] = sourced(value.strip())
        else:
            builder.lack(key, "the link has no " + name)
    file_name = link.get("file")
    if not file_name:
        builder.lack("prototypeFile", "the link has no file name")
    elif folder_files is None or file_name.lower() not in folder_files:
        builder.lack("prototypeFile", file_name + " is not in the prototype folder")
    else:
        builder.entry["prototypeFile"] = sourced(file_name)
    transform = link.get("transform")
    origin = shared_origin(link, offset)
    if not transform or origin is None or not is_number(transform.get("rotationDeg")) or not isinstance(transform.get("mirrored"), bool):
        builder.lack("transform", "the link has no transform")
    elif not axes_fit(transform):
        builder.lack("transform", "the link axes are not a turn about Z with or without a mirror")
    else:
        builder.entry["insert"] = sourced(point(origin[:2]) + [None])
        builder.entry["rotationDeg"] = sourced(plan_rotation(transform["rotationDeg"]))
        builder.entry["mirrored"] = sourced(bool(transform["mirrored"]))
    written = {}
    for name in job["parameters"]:
        value = parameters.get(name)
        if isinstance(value, str) and value.strip():
            written[name] = sourced(value)
        else:
            builder.lack("parameters." + name, "the link has no text value for it")
    builder.entry["parameters"] = written
    return origin


def schedule_values(builder, row):
    levels = {}
    for key, label in LEVEL_COLUMNS:
        value = plot_relations.excel_cell(row, label)
        if not is_number(value):
            builder.lack("levels." + key, "the schedule cell " + label + " holds no number")
        else:
            levels[key] = sourced(common.r3(float(value), 4 if key in WIDE_LEVELS else 3))
    steps = STEPS_TEXT.match(str(plot_relations.excel_cell(row, "Steps Needed") or ""))
    if steps is None:
        builder.lack("levels.steps", "the schedule cell Steps Needed holds no whole step count")
    else:
        levels["steps"] = sourced(int(steps.group(1)))
    builder.entry["levels"] = levels


def compare_link_and_schedule(builder, link, row, origin):
    parameters = link.get("parameters") or {}
    unit = builder.entry.get("unit", {}).get("value")
    sheet_unit = plot_relations.excel_cell(row, "Unit")
    if unit and sheet_unit and str(sheet_unit).strip() != unit:
        builder.clash("unit", unit, str(sheet_unit).strip(), "schedule Unit")
    file_unit = link_summary.file_unit(link.get("file"))
    if unit and file_unit and file_unit != unit:
        builder.clash("unit", unit, file_unit, "link file name")
    steps = builder.entry["levels"].get("steps", {}).get("value")
    link_steps = link_summary.leading_integer(parameters.get(STEPS_PARAMETER))
    if steps is not None and link_steps is not None and steps != link_steps:
        builder.clash("levels.steps", steps, link_steps, STEPS_PARAMETER)
    ffl = builder.entry["levels"].get("ffl", {}).get("value")
    if ffl is None:
        return
    if origin is not None and abs(origin[2] - ffl) > LEVEL_CONFLICT + EPS:
        builder.clash("levels.ffl", ffl, common.r3(origin[2]), "past link origin Z")
    link_ffl = link_summary.parse_number(parameters.get(FFL_PARAMETER))
    if link_ffl is not None and abs(link_ffl - ffl) > LEVEL_CONFLICT + EPS:
        builder.clash("levels.ffl", ffl, common.r3(link_ffl), FFL_PARAMETER)


def only_one(builder, key, found):
    if not found:
        builder.lack(key, "no floor names the plot")
        return None
    if len(found) > 1:
        builder.lack(key, str(len(found)) + " floors name the plot: " + " ".join(str(floor["elementId"]) for floor in plot_relations.first_by_id(found)))
        return None
    return found[0]


def flat_floor(builder, key, floor, shape, z, offset):
    """A Generic or yard floor as type, outline and the computed z, with its past top compared to that z."""
    rings = rings_of(shape)
    if rings is None:
        builder.lack(key, "floor " + str(floor["elementId"]) + " has no outline")
        return None
    low, high, _ = plot_relations.top_range(floor, offset)
    if z is not None and low is not None and max(abs(low - z), abs(high - z)) > TOP_CONFLICT + EPS:
        builder.clash(key + ".z", common.r3(z), [common.r3(low), common.r3(high)], "past floor top")
    entry = {"type": sourced(floor.get("typeName")), "outline": sourced(rings)}
    if z is None:
        builder.lack(key + ".z", "no FFL")
    else:
        entry["z"] = sourced(common.r3(z), COMPUTED)
    return entry


def floor_values(builder, index, offset):
    kinds = index["byPlot"].get(builder.plot, {})
    ffl = builder.entry.get("levels", {}).get("ffl", {}).get("value")
    floors = {}
    generic = only_one(builder, "floors.generic", kinds.get("generic", []))
    ramp = only_one(builder, "floors.ramp", kinds.get("ramp", []))
    generic_shape = index["shapes"][generic["elementId"]][0] if generic is not None else None
    ramp_shape = index["shapes"][ramp["elementId"]][0] if ramp is not None else None
    if generic is not None:
        made = flat_floor(builder, "floors.generic", generic, generic_shape, ffl, offset)
        if made is not None:
            floors["generic"] = made
    status, candidates = plot_relations.find_yard(index["yards"], index["tree"], generic_shape, ramp_shape)
    if status == "assigned":
        yard, yard_shape = candidates[0]
        made = flat_floor(builder, "floors.yard", yard, yard_shape, None if ffl is None else ffl - YARD_BELOW_FFL, offset)
        if made is not None:
            floors["yard"] = made
    elif status == "ambiguous":
        builder.lack("floors.yard", "ambiguous, candidates " + " ".join(str(floor["elementId"]) for floor, _ in candidates))
    else:
        builder.lack("floors.yard", "no yard floor touches the Generic floor or the ramp")
    if ramp is not None:
        corners, source = ramp_levels.corner_points(ramp, offset)
        if source != "corners":
            builder.lack("floors.ramp", "floor " + str(ramp["elementId"]) + " has no corner levels")
        elif len(corners) != ramp_levels.CORNERS:
            builder.lack("floors.ramp", "floor " + str(ramp["elementId"]) + " has " + str(len(corners)) + " corners")
        else:
            floors["ramp"] = {"type": sourced(ramp.get("typeName")), "corners": sourced([point(corner) for corner in corners])}
    builder.entry["floors"] = floors
    return status


def build_plot(plot, plot_links, rows, index, offset, job, folder_files):
    builder = PlotBuilder(plot)
    row = rows[0] if len(rows) == 1 else None
    origin = None
    if not plot_links:
        builder.lack("link", "no link names the plot")
    elif len(plot_links) > 1:
        builder.lack("links", str(len(plot_links)) + " links name the plot: " + " ".join(str(link.get("elementId")) for link in plot_links))
    else:
        origin = link_values(builder, plot_links[0], offset, job, folder_files)
    if len(rows) > 1:
        builder.lack("scheduleRow", str(len(rows)) + " schedule rows hold the plot, sheet rows " + " ".join(str(item.get("sheetRow")) for item in rows))
        builder.entry["levels"] = {}
    elif row is None:
        builder.lack("scheduleRow", "the schedule has no row for the plot")
        builder.entry["levels"] = {}
    else:
        schedule_values(builder, row)
        if len(plot_links) == 1:
            compare_link_and_schedule(builder, plot_links[0], row, origin)
    ffl = builder.entry["levels"].get("ffl", {}).get("value")
    if "insert" in builder.entry:
        if ffl is None:
            del builder.entry["insert"]
        else:
            builder.entry["insert"]["value"][2] = ffl
    yard_status = floor_values(builder, index, offset)
    lacking = [key for key in LINK_KEYS if not builder.has(key)]
    return builder, not lacking, yard_status


def schedule_by_plot(rows):
    """Every schedule row by plot number. Two rows for one plot are both kept, so the plot is listed and never picks one."""
    found, bad = {}, []
    for row in rows:
        value = plot_relations.excel_cell(row, "Plot No.")
        plot = plot_number(value)
        if plot is None:
            bad.append({"sheetRow": row.get("sheetRow"), "plotNo": str(value)})
            continue
        found.setdefault(plot, []).append(row)
    return found, bad


def build_plan(data, rows, job, folder_files, prototype_folder):
    """The plan of R80 from the host document of prototypes.json and the schedule rows."""
    document = host_of(data)
    offset = ramp_levels.offset_of(document)
    links = document.get("links") or []
    by_plot, without_plot, bad_links = {}, 0, []
    for link in links:
        value = (link.get("parameters") or {}).get(PLOT_PARAMETER)
        if value is None or not str(value).strip():
            without_plot += 1
            continue
        plot = plot_number(value)
        if plot is None:
            bad_links.append({"elementId": link.get("elementId"), "plotNumber": str(value)})
            continue
        by_plot.setdefault(plot, []).append(link)
    schedule, bad_rows = schedule_by_plot(rows)
    index = floor_index(document, offset)
    plots, missing, conflicts = [], [], []
    missing_keys, floors_found, yard_status = Counter(), Counter(), Counter()
    for plot in sorted(set(by_plot) | set(schedule), key=link_summary.sort_key):
        builder, complete, status = build_plot(plot, by_plot.get(plot, []), schedule.get(plot, []), index, offset, job, folder_files)
        yard_status[status] += 1
        floors_found.update(builder.entry["floors"].keys())
        if complete:
            plots.append(builder.entry)
        if builder.missing:
            missing.append({"plot": plot, "written": complete, "missing": builder.missing})
            missing_keys.update(item["key"] for item in builder.missing)
        conflicts.extend(builder.conflicts)
    proof, over = position_proof(links, offset)
    for link, gap in over:
        conflicts.append({"plot": plot_number((link.get("parameters") or {}).get(PLOT_PARAMETER)) or "link " + str(link.get("elementId")), "field": "insert",
                          "kept": "transform origin plus offset", "other": common.r3(gap), "otherSource": "transformShared, difference in mm"})
    survey = document["surveyPoint"]
    plan = {
        "format": FORMAT,
        "formatVersion": FORMAT_VERSION,
        "neighbourhood": job["neighbourhood"],
        "coordinateSystem": {"name": "shared", "lengthUnit": "m", "angleUnit": "deg"},
        "surveyPoint": {"position": point(survey["position"]), "sharedPosition": point(survey["sharedPosition"])},
        "prototypeFolder": str(prototype_folder),
        "sources": {"prototypes": Path(job["prototypes"]).name, "schedule": job["schedule"], "sheet": job["sheet"]},
        "summary": {
            "plots": len(set(by_plot) | set(schedule)),
            "written": len(plots),
            "notWritten": len(set(by_plot) | set(schedule)) - len(plots),
            "linksWithoutPlot": without_plot,
            "scheduleRows": len(rows),
            "linksWithBadPlotNumber": bad_links,
            "scheduleRowsWithBadPlotNumber": bad_rows,
            "floorsWithBadPlotNumber": index["badFloors"],
            "missingByKey": dict(sorted(missing_keys.items())),
            "floorsFound": {kind: floors_found[kind] for kind in ("generic", "yard", "ramp")},
            "yardStatus": dict(sorted(yard_status.items())),
            "conflictsByField": dict(sorted(Counter(item["field"] + " against " + item["otherSource"] for item in conflicts).items())),
            "positionProof": proof,
        },
        "plots": plots,
        "missing": missing,
        "conflicts": conflicts,
    }
    # Never guessed. A plan without the angle stops the placer under R83
    angle = document.get("angleToTrueNorthDeg")
    if is_number(angle):
        plan["angleToTrueNorthDeg"] = common.r3(angle)
    return plan


def folder_listing(folder):
    folder = Path(folder)
    return {path.name.lower() for path in folder.iterdir() if path.is_file()} if folder.is_dir() else None


def write_plan(path, plan):
    # entry and height hold an object, so round_tree leaves them as they are and they keep the 4 decimals set above
    return common.write_json(path, plan, frozenset(WIDE_LEVELS))


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build plan.json from a past job, prototypes.json of the past model and its schedule.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH), help="project settings, default config/project.json")
    parser.add_argument("--prototypes", help="prototypes.json, default pastJob.prototypes")
    parser.add_argument("--out", help="plan file, default pastJob.output")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    job = load_job(settings)
    prototypes = Path(args.prototypes or job["prototypes"])
    out = Path(args.out or job["output"])
    plot_relations.refuse_inside_repo(out.resolve().parent)
    root = input_root(settings)
    schedule = root / full_run.XLSX_FOLDER / job["schedule"]
    folder = root / job["prototypeFolder"]
    for path in (prototypes, schedule):
        if not path.is_file():
            print("not found: " + str(path), file=sys.stderr)
            return 2
    data = link_summary.load(prototypes)
    rows = excel_reader.read_sheet(schedule, job["sheet"])[3]
    plan = build_plan(data, rows, job, folder_listing(folder), folder)
    plan["sources"]["prototypes"] = prototypes.name
    write_plan(out, plan)
    summary = plan["summary"]
    print("plots written %d of %d, missing a value %d" % (summary["written"], summary["plots"], len(plan["missing"])))
    print("floors found %s" % summary["floorsFound"])
    print("position proof on %d links, largest difference %s mm" % (summary["positionProof"]["links"], summary["positionProof"]["largestMm"]))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
