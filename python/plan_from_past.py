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
from pathlib import Path, PureWindowsPath

from shapely.geometry import Polygon

import common
import excel_reader
import full_run
import link_summary
import plot_relations
import link_table
import project_profile
import ramp_levels
import yard_rule

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
NAME_PARAMETER = "Name"
RVT_NAME_PARAMETER = "RRE_ZZ_SP_RVT-Name"
JOB_KEYS = ("neighbourhood", "schedule", "sheet", "prototypes", "prototypeFolder", "output", "parameters")
FLOOR_KINDS = ("generic", "yard", "ramp")
FLOOR_KEYS = ("types", "levels", "plotParameter", "yardBelowFflM", "yardShareTie", "yardMinShare")
# A ramp corner sits on an outline vertex when it lies this close in plan, in metres. Both are rounded to the mm by R2
CORNER_ON_VERTEX = 0.002
# An arc end must lie this close to its circle, in metres
ARC_FIT = 0.001
# A top face loop is the sketch loop again when the area they do not share is under this share of the sketch loop.
# It leaves room for an arc the top face cuts into chords
SAME_LOOP_SHARE = 0.01
# An arc is compared with other shapes as chords that stand off it by this much at most, in metres
ARC_SAG = 0.0001
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


def load_floors(settings):
    """R112. The floors block of the project settings: the type names and the level of each floor kind, the Plot parameter,
    the yard depth below the FFL and the tie limit of R111. Anything missing or of the wrong kind is refused, no name is guessed."""
    floors = settings.values.get("floors")
    name = settings.path.name
    if not isinstance(floors, dict):
        raise project_profile.ProfileError(name + " has no floors block")
    missing = [key for key in FLOOR_KEYS if key not in floors]
    if missing:
        raise project_profile.ProfileError(name + " floors has no " + ", ".join(missing))
    types, levels = floors["types"], floors["levels"]
    if not isinstance(types, dict) or not isinstance(levels, dict) or set(types) != set(FLOOR_KINDS) or set(levels) != set(FLOOR_KINDS):
        raise project_profile.ProfileError(name + " floors.types and floors.levels must each name " + ", ".join(FLOOR_KINDS))
    seen = set()
    for kind in FLOOR_KINDS:
        names = types[kind]
        if not isinstance(names, list) or not names or not all(isinstance(item, str) and item.strip() == item and item for item in names):
            raise project_profile.ProfileError(name + " floors.types." + kind + " must be a list of type names")
        if seen & set(names):
            raise project_profile.ProfileError(name + " floors.types names " + ", ".join(sorted(seen & set(names))) + " under two kinds")
        seen |= set(names)
        if not isinstance(levels[kind], str) or not levels[kind].strip():
            raise project_profile.ProfileError(name + " floors.levels." + kind + " must be a level name")
    if not isinstance(floors["plotParameter"], str) or not floors["plotParameter"].strip():
        raise project_profile.ProfileError(name + " floors.plotParameter must be a parameter name")
    below, tie = floors["yardBelowFflM"], floors["yardShareTie"]
    if not is_number(below) or not 0 <= below < 1:
        raise project_profile.ProfileError(name + " floors.yardBelowFflM must be metres from 0 to below 1")
    if not is_number(tie) or not 0 <= tie < 1:
        raise project_profile.ProfileError(name + " floors.yardShareTie must be a share from 0 to below 1")
    if not is_number(floors["yardMinShare"]) or not 0 <= floors["yardMinShare"] < 1:
        raise project_profile.ProfileError(name + " floors.yardMinShare must be a share from 0 to below 1")
    return floors


def kind_of(floor, floors):
    """The floor kind of a floor by its type name, exact, from floors.types, else None."""
    name = floor.get("typeName")
    for kind in FLOOR_KINDS:
        if name in floors["types"][kind]:
            return kind
    return None


def input_root(settings):
    return Path(os.environ.get(ROOT_VARIABLE) or settings.get("inputRoot"))


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


def floor_index(document, offset, floors_config, faces):
    """The Generic and ramp floors by plot number and the yards by the plot of R111. faces are the baseplan plot faces by official number."""
    floors = document.get("floors") or []
    by_plot, bad, yards = {}, [], []
    for floor in floors:
        kind = kind_of(floor, floors_config)
        if kind == "yard":
            yards.append((floor["elementId"], plot_relations.floor_shape(floor, offset)[0]))
            continue
        named = link_summary.floor_plot(floor)
        if kind is None or named is None:
            continue
        plot = plot_number(named)
        if plot is None:
            bad.append({"elementId": floor.get("elementId"), "plotNumber": str(named)})
            continue
        by_plot.setdefault(plot, {}).setdefault(kind, []).append(floor)
    results = yard_rule.assign(yards, faces, floors_config["yardShareTie"], floors_config["yardMinShare"])
    owned = yard_rule.by_plot(results)
    by_id = {floor["elementId"]: floor for floor in floors}
    for plot, ids in owned.items():
        by_plot.setdefault(plot, {})["yard"] = [by_id[element_id] for element_id in ids]
    return {"byPlot": by_plot, "badFloors": bad, "yards": results, "config": floors_config, "faces": set(faces)}


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
    # The instance Name, kept as the link holds it. RRE_ZZ_SP_RVT-Name is built from the same values, a difference is a conflict
    link_name = parameters.get(NAME_PARAMETER)
    if isinstance(link_name, str) and link_name.strip():
        builder.entry["linkName"] = sourced(link_name)
        rvt_name = parameters.get(RVT_NAME_PARAMETER)
        if isinstance(rvt_name, str) and rvt_name.strip() and rvt_name != link_name:
            builder.clash("linkName", link_name, rvt_name, RVT_NAME_PARAMETER)
    else:
        builder.lack("linkName", "the link has no instance Name")
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


def outer_loop(floor, offset):
    """R112. The outer loop of the floor sketch in shared plan coordinates with its arcs, and None, or None and why.

    The reader gives the sketch vertices and, for each arc, the edge from vertex edge to the next one with centre, radius
    and direction (R34). An arc whose ends do not lie on its circle is refused, so the placer never builds a wrong curve.
    """
    sketch = link_summary.sketch_points(floor)
    if len(sketch) < 3:
        return None, "has no sketch outline"
    raw = [(vertex[0] + offset[0], vertex[1] + offset[1]) for vertex in sketch]
    arcs, edges = [], set()
    for arc in floor.get("arcs") or []:
        edge, center, radius = arc.get("edge"), arc.get("center"), arc.get("radius")
        if not isinstance(edge, int) or isinstance(edge, bool) or not 0 <= edge < len(raw) or edge in edges:
            return None, "has an arc on an edge that is not in its outline"
        if not isinstance(center, list) or len(center) < 2 or not all(is_number(value) for value in center[:2]) or not is_number(radius) or radius <= 0:
            return None, "has an arc with no centre or radius"
        if not isinstance(arc.get("ccw"), bool):
            return None, "has an arc with no direction"
        middle = (center[0] + offset[0], center[1] + offset[1])
        for end in (raw[edge], raw[(edge + 1) % len(raw)]):
            if abs(math.dist(end, middle) - radius) > ARC_FIT + EPS:
                return None, "has an arc on edge " + str(edge) + " whose ends are not on its circle"
        edges.add(edge)
        arcs.append({"edge": edge, "center": point(middle), "radius": common.r3(radius), "ccw": arc["ccw"]})
    return {"points": [point(xy) for xy in raw], "arcs": sorted(arcs, key=lambda item: item["edge"])}, None


def loop_points(loop):
    """The points of a plan loop with each arc cut into chords that stand off it by ARC_SAG at most, for comparing shapes only."""
    points, arcs = loop["points"], {arc["edge"]: arc for arc in loop.get("arcs") or []}
    found = []
    for edge, start in enumerate(points):
        found.append(start)
        arc = arcs.get(edge)
        if arc is None:
            continue
        end = points[(edge + 1) % len(points)]
        cx, cy = arc["center"]
        first = math.atan2(start[1] - cy, start[0] - cx)
        last = math.atan2(end[1] - cy, end[0] - cx)
        sweep = (last - first) % (2 * math.pi) if arc["ccw"] else -((first - last) % (2 * math.pi))
        radius = arc["radius"]
        step = 2 * math.acos(max(-1.0, 1 - ARC_SAG / radius)) if radius > ARC_SAG else math.pi
        steps = max(1, int(math.ceil(abs(sweep) / step)))
        for step in range(1, steps):
            angle = first + sweep * step / steps
            found.append([cx + arc["radius"] * math.cos(angle), cy + arc["radius"] * math.sin(angle)])
    return found


def floor_loops(floor, offset):
    """R112. Every loop of the floor in shared plan coordinates, the outer sketch loop with its arcs first, and None, or None and why.

    The reader keeps only the outer loop of the sketch, so a second piece or a hole comes from the top face outline of R52,
    which holds straight edges only. A top face loop that is the sketch loop again is left out.
    """
    first, why = outer_loop(floor, offset)
    if first is None:
        return None, why
    loops = [first]
    sketch = Polygon(loop_points(first)).buffer(0)
    for ring in floor.get("outline") or []:
        if len(ring) < 3:
            continue
        shape = Polygon([(vertex[0], vertex[1]) for vertex in ring]).buffer(0)
        if sketch.area > 0 and shape.symmetric_difference(sketch).area <= SAME_LOOP_SHARE * sketch.area:
            continue
        loops.append({"points": [point(vertex[:2]) for vertex in ring], "arcs": []})
    return loops, None


def corners_on_outline(corners, points):
    """True when the outline has four vertices and each corner sits on its own vertex in plan."""
    if len(points) != len(corners):
        return False
    used = set()
    for corner in corners:
        near = [index for index, vertex in enumerate(points) if index not in used and math.dist(corner[:2], vertex) <= CORNER_ON_VERTEX + EPS]
        if not near:
            return False
        used.add(near[0])
    return True


def floor_entry(builder, kind, floor, offset, config, unit):
    """R112. One floor of a plot with all the placer needs, or None when a part is missing and the plot says why."""
    key = "floors." + kind
    label = "floor " + str(floor["elementId"]) + " "
    level = floor.get("level")
    if level != config["levels"][kind]:
        builder.lack(key, label + "is on level " + str(level) + ", floors.levels." + kind + " is " + config["levels"][kind])
        return None
    if not is_number(floor.get("levelElevation")):
        builder.lack(key, label + "has no level elevation")
        return None
    loops, why = floor_loops(floor, offset)
    if loops is None:
        builder.lack(key, label + why)
        return None
    if not unit:
        builder.lack(key, "the plot has no unit for the " + config["plotParameter"] + " value")
        return None
    plot_value = unit + "-" + builder.plot
    past_value = (floor.get("parameters") or {}).get(config["plotParameter"])
    if past_value not in (None, "") and str(past_value) != plot_value:
        builder.clash(key + ".plotValue", plot_value, str(past_value), "past floor " + config["plotParameter"])
    entry = {
        "type": sourced(floor.get("typeName")),
        "level": sourced(level),
        "levelElevation": sourced(common.r3(floor["levelElevation"])),
        "outline": sourced({"loops": loops}),
        "plotValue": sourced(plot_value, COMPUTED),
    }
    if kind == "ramp":
        corners, source = ramp_levels.corner_points(floor, offset)
        if source != "corners":
            builder.lack(key, label + "has no corner levels")
            return None
        if len(corners) != ramp_levels.CORNERS:
            builder.lack(key, label + "has " + str(len(corners)) + " corners")
            return None
        if len(loops) != 1 or not corners_on_outline(corners, loops[0]["points"]):
            builder.lack(key, label + "has corners that are not the vertices of its outline")
            return None
        entry["corners"] = sourced([point(corner) for corner in corners])
        lowest = min(corner[2] for corner in corners)
        entry["heightOffset"] = sourced(common.r3(lowest - floor["levelElevation"]), COMPUTED)
    return entry


def flat_top(builder, kind, entry, floor, z, offset):
    """The top z of a Generic or yard floor and its height offset from the level, with the past top compared to that z."""
    key = "floors." + kind
    if z is None:
        builder.lack(key + ".z", "no FFL")
        return None
    low, high, _ = plot_relations.top_range(floor, offset)
    if low is not None and max(abs(low - z), abs(high - z)) > TOP_CONFLICT + EPS:
        builder.clash(key + ".z", common.r3(z), [common.r3(low), common.r3(high)], "past floor top")
    entry["z"] = sourced(common.r3(z), COMPUTED)
    entry["heightOffset"] = sourced(common.r3(z - entry["levelElevation"]["value"]), COMPUTED)
    return entry


def yard_why(index, plot):
    """Why a plot has no yard under R111."""
    tied = yard_rule.tied_with(index["yards"], plot, index["config"]["yardShareTie"])
    if tied:
        return "tie, yards " + " ".join(str(element_id) for element_id in tied) + " hold shares within " + str(index["config"]["yardShareTie"]) + " of another plot"
    low = yard_rule.low_with(index["yards"], plot)
    if low:
        return "lowShare, yards " + " ".join(str(element_id) for element_id in low) + " hold less than " + str(index["config"]["yardMinShare"]) + " of their area on the face of this plot"
    if plot not in index["faces"]:
        return "no baseplan face carries the label of this plot"
    return "no yard has its largest share on the baseplan face of this plot"


def floor_values(builder, index, offset):
    kinds = index["byPlot"].get(builder.plot, {})
    ffl = builder.entry.get("levels", {}).get("ffl", {}).get("value")
    unit = builder.entry.get("unit", {}).get("value")
    config = index["config"]
    below = config["yardBelowFflM"]
    floors = {}
    generic = only_one(builder, "floors.generic", kinds.get("generic", []))
    if generic is not None:
        made = floor_entry(builder, "generic", generic, offset, config, unit)
        if made is not None and flat_top(builder, "generic", made, generic, ffl, offset) is not None:
            floors["generic"] = made
    yards = kinds.get("yard", [])
    if not yards:
        status = "none"
        builder.lack("floors.yard", yard_why(index, builder.plot))
    elif len(yards) > 1:
        status = "several"
        builder.lack("floors.yard", str(len(yards)) + " yards have their largest share on this plot: " + " ".join(str(floor["elementId"]) for floor in plot_relations.first_by_id(yards)))
    else:
        status = "assigned"
        made = floor_entry(builder, "yard", yards[0], offset, config, unit)
        if made is not None and flat_top(builder, "yard", made, yards[0], None if ffl is None else ffl - below, offset) is not None:
            floors["yard"] = made
    ramp = only_one(builder, "floors.ramp", kinds.get("ramp", []))
    if ramp is not None:
        made = floor_entry(builder, "ramp", ramp, offset, config, unit)
        if made is not None:
            floors["ramp"] = made
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


def yard_summary(results, plots):
    """R111 in counts: the yards by status, and the yards that go to no plot of the plan, a tie, none or a plot outside it."""
    status = Counter(result["status"] for result in results.values())
    left = []
    for element_id in sorted(results):
        result = results[element_id]
        if result["status"] != "assigned" or result["plot"] not in plots:
            left.append({"elementId": element_id, "status": result["status"] if result["status"] != "assigned" else "plotNotInPlan",
                         "plot": result["plot"], "candidates": result["candidates"][:3]})
    return {"yards": len(results), "byStatus": dict(sorted(status.items())), "withNoPlanPlot": len(left), "left": left}


def build_plan(data, rows, job, folder_files, prototype_folder, floors_config, faces):
    """The plan of R80 from the host document of prototypes.json and the schedule rows.

    floors_config is the floors block of the settings (R112) and faces the baseplan plot faces by official plot number (R111).
    """
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
    index = floor_index(document, offset, floors_config, faces)
    plots, missing, conflicts = [], [], []
    missing_keys, floors_found, yard_status, floor_types = Counter(), Counter(), Counter(), Counter()
    for plot in sorted(set(by_plot) | set(schedule), key=link_summary.sort_key):
        builder, complete, status = build_plot(plot, by_plot.get(plot, []), schedule.get(plot, []), index, offset, job, folder_files)
        yard_status[status] += 1
        floors_found.update(builder.entry["floors"].keys())
        if complete:
            floor_types.update(kind + ": " + floor["type"]["value"] for kind, floor in builder.entry["floors"].items())
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
        # R112. The placer reads every floor type, level and parameter name from here, none sits in its code
        "floorSettings": {"types": floors_config["types"], "levels": floors_config["levels"], "plotParameter": floors_config["plotParameter"]},
        "sources": {"prototypes": PureWindowsPath(job["prototypes"]).name, "schedule": job["schedule"], "sheet": job["sheet"]},
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
            "floorsFound": {kind: floors_found[kind] for kind in FLOOR_KINDS},
            "floorTypes": dict(sorted(floor_types.items())),
            "yardStatus": dict(sorted(yard_status.items())),
            "yardRule": yard_summary(index["yards"], set(by_plot) | set(schedule)),
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


def prototype_folders(root, job):
    """R109. The folder the placer loads from and the past folder it came from. The upgraded Revit 2024 copies win when the config names them."""
    source = root / job["prototypeFolder"]
    upgraded = job.get("upgradedPrototypeFolder")
    return (root / upgraded if upgraded else source), source


def baseplan_faces(path, shifts_path, neighbourhood):
    """R111. The plot faces of the baseplan by official plot number, with the shift of the neighbourhood, which is never guessed."""
    shifts = link_table.load_shifts(shifts_path)["shifts"]
    if neighbourhood not in shifts:
        raise project_profile.ProfileError("config/plot_numbers.json has no shift for " + str(neighbourhood))
    data = link_summary.load(path)
    if not isinstance(data, dict) or not isinstance(data.get("plots"), list):
        raise project_profile.ProfileError(str(path) + " has no plots list")
    faces, bad = yard_rule.face_shapes(data["plots"], shifts[neighbourhood])
    return faces, bad


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build plan.json from a past job, prototypes.json of the past model and its schedule.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH), help="project settings, default config/project.json")
    parser.add_argument("--prototypes", help="prototypes.json, default pastJob.prototypes")
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--shifts", default=str(project_profile.ROOT / "config" / "plot_numbers.json"))
    parser.add_argument("--out", help="plan file, default pastJob.output")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    job = load_job(settings)
    floors = load_floors(settings)
    prototypes = Path(args.prototypes or job["prototypes"])
    baseplan = Path(args.baseplan or (project_profile.ROOT / job["baseplanPlots"] if job.get("baseplanPlots") else ""))
    out = Path(args.out or job["output"])
    plot_relations.refuse_inside_repo(out.resolve().parent)
    root = input_root(settings)
    schedule = root / full_run.XLSX_FOLDER / job["schedule"]
    folder, source_folder = prototype_folders(root, job)
    for path in (prototypes, schedule, baseplan):
        if not path.is_file():
            print("not found: " + str(path), file=sys.stderr)
            return 2
    faces, bad_labels = baseplan_faces(baseplan, args.shifts, job["neighbourhood"])
    data = link_summary.load(prototypes)
    rows = excel_reader.read_sheet(schedule, job["sheet"])[3]
    plan = build_plan(data, rows, job, folder_listing(folder), folder, floors, faces)
    plan["sources"]["prototypes"] = prototypes.name
    plan["sources"]["prototypeSourceFolder"] = str(source_folder)
    plan["sources"]["baseplanPlots"] = baseplan.name
    plan["summary"]["yardRule"]["facesWithBadLabel"] = len(bad_labels)
    write_plan(out, plan)
    summary = plan["summary"]
    print("plots written %d of %d, missing a value %d" % (summary["written"], summary["plots"], len(plan["missing"])))
    print("floors found %s" % summary["floorsFound"])
    print("floor types %s" % summary["floorTypes"])
    rule = summary["yardRule"]
    print("yard rule: yards %d, %s, with no plan plot %d, plot yard status %s" % (rule["yards"], rule["byStatus"], rule["withNoPlanPlot"], summary["yardStatus"]))
    print("position proof on %d links, largest difference %s mm" % (summary["positionProof"]["links"], summary["positionProof"]["largestMm"]))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
