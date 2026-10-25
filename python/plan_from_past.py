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
# R110, R204. The job code of a test host window, the name it gives its read output and the mark before the job in a work copy
JOB_CODE = re.compile(r"[A-Z][A-Z0-9]{1,3}")
READ_FILE = re.compile(r"testhost_(?P<job>[A-Z][A-Z0-9]{1,3})_prototypes\.json")
WORK_MARK = "_TEST-"
RVT = ".rvt"
LEVEL_COLUMNS = (("ffl", "FFL"), ("ssl", "SSL"), ("entry", "El @ Entry"), ("height", "Height"))
WIDE_LEVELS = ("entry", "height")
# R325. A row of an answer key marks that its height is worked out from its FFL and El @ Entry, a schedule row never holds it
HEIGHT_FROM_LEVELS = "heightFromLevels"
MODEL_PLOT = "modelPlot"
# R325. El @ Entry of a key plan comes from the parameter of its generic floor, a plan of the past job keeps pastJob for its schedule
KEY_PARAMETER = "keyParameter"
ENTRY_SOURCE = "entrySource"
# R325. A row whose El @ Entry was refused keeps why, and the plan lists it under levels.entry with this start
ENTRY_REFUSED = "entryRefused"
REFUSED = "refused, "
# R120. A share of a floor on a face under this is a sliver and is left out
SLIVER_SHARE = 0.0001


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


def check_types(name, label, types):
    """R112, R321. Each kind names a list of type names, none blank or padded, and no name sits under two kinds."""
    seen = set()
    for kind in FLOOR_KINDS:
        names = types[kind]
        if not isinstance(names, list) or not names or not all(isinstance(item, str) and item.strip() == item and item for item in names):
            raise project_profile.ProfileError(name + " " + label + "." + kind + " must be a list of type names")
        if seen & set(names):
            raise project_profile.ProfileError(name + " " + label + " names " + ", ".join(sorted(seen & set(names))) + " under two kinds")
        seen |= set(names)


def key_codes(settings):
    """R321. The codes of the answer keys of answerKeys.keys in the project settings."""
    keys = (settings.values.get("answerKeys") or {}).get("keys") or []
    return [key.get("code") for key in keys if isinstance(key, dict)]


def read_job(path):
    """R321. The job of a key read by the name the test host gives its output, testhost_<job>_prototypes.json, None for another name."""
    match = READ_FILE.fullmatch(PureWindowsPath(str(path)).name)
    return match.group("job") if match else None


def check_key_file(settings, code, name, job):
    """R321. A read is of the answer key code only when its model is the work copy of the Revit 2024 copy of that key the job of the
    read opened: the name the read gives, with or without .rvt, is the copy name of the key in answerKeys without .rvt, then _TEST-
    and the job, in any case as Windows matches names (R204). The job is a job code of the test host (R110). So a floor scenario file
    the placer changed and its master (R118), the source the copy was made from, the copy itself, a work copy of another job or of
    another key are refused and never read by the types of the key."""
    keys = [key for key in (settings.values.get("answerKeys") or {}).get("keys") or [] if isinstance(key, dict) and key.get("code") == code]
    if len(keys) != 1 or not isinstance(keys[0].get("copy"), str):
        raise project_profile.ProfileError(settings.path.name + " answerKeys has no one key " + str(code) + " with a copy name to tie a read to")
    if not isinstance(name, str) or not name.strip():
        raise project_profile.ProfileError("the read names no model file, so it can not be tied to the key " + str(code))
    if not isinstance(job, str) or not JOB_CODE.fullmatch(job):
        raise project_profile.ProfileError("the read of the key " + str(code) + " has no job code of the test host, a job is a capital and 1 to 3 "
                                           "capitals or digits, given: " + str(job))
    stem = PureWindowsPath(keys[0]["copy"]).stem
    given = name[:-len(RVT)] if name.lower().endswith(RVT) else name
    if given.casefold() != (stem + WORK_MARK + job).casefold():
        raise project_profile.ProfileError("the model file " + name + " is not the work copy " + stem + WORK_MARK + job + " of the copy " + keys[0]["copy"]
                                           + " of the key " + str(code) + " under the job " + job)


def key_types(settings, floors):
    """R321. floors.keyTypes gives an answer key the type names of a kind its model names other than floors.types, as FFL for the
    Generic floors of IME and IMF. Each key must be an answer key, each kind one of the floor kinds, and the types of each key
    with floors.types for the kinds it leaves out must pass the checks of R112. Returns the types of every key so named."""
    name = settings.path.name
    given = floors.get("keyTypes", {})
    if not isinstance(given, dict):
        raise project_profile.ProfileError(name + " floors.keyTypes must map an answer key code to its floor types")
    codes = key_codes(settings)
    merged = {}
    for code, own in given.items():
        if code not in codes:
            raise project_profile.ProfileError(name + " floors.keyTypes names " + str(code) + ", which is no code of answerKeys.keys")
        if not isinstance(own, dict) or not own or not set(own) <= set(FLOOR_KINDS):
            raise project_profile.ProfileError(name + " floors.keyTypes." + code + " must name some of " + ", ".join(FLOOR_KINDS))
        types = {kind: own.get(kind, floors["types"][kind]) for kind in FLOOR_KINDS}
        check_types(name, "floors.keyTypes." + code, types)
        merged[code] = types
    return merged


def load_floors(settings, key=None):
    """R112. The floors block of the project settings: the type names and the level of each floor kind, the Plot parameter,
    the yard depth below the FFL and the tie limit of R111. Anything missing or of the wrong kind is refused, no name is guessed.
    R321. With the code of an answer key the block holds the types that key reads its floors by, floors.keyTypes of that key for the
    kinds it names and floors.types for the rest. The block of the settings is never changed."""
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
    check_types(name, "floors.types", types)
    for kind in FLOOR_KINDS:
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
    by_key = key_types(settings, floors)
    if key is None:
        return floors
    if key not in key_codes(settings):
        raise project_profile.ProfileError(name + " answerKeys.keys has no key " + str(key) + ", its floors are not read by floors.types")
    return dict(floors, types=by_key.get(key, types))


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


def floor_index(document, offset, floors_config, faces, key=None):
    """The Generic and ramp floors by plot number and the yards by the plot of R111. faces are the baseplan plot faces by official number.
    R321. A floor of no kind that names a plot is kept by plot, so a plot it stands on says its type is not one the model is read by."""
    floors = document.get("floors") or []
    by_plot, bad, yards, no_kind = {}, [], [], {}
    for floor in floors:
        kind = kind_of(floor, floors_config)
        if kind == "yard":
            yards.append((floor["elementId"], plot_relations.floor_shape(floor, offset)[0]))
            continue
        named = link_summary.floor_plot(floor)
        if kind is None:
            plot = None if named is None else plot_number(named)
            if plot is not None:
                no_kind.setdefault(plot, []).append(floor)
            continue
        if named is None:
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
    types_of = "floors.types" if key is None else "floors.keyTypes of " + key + " or floors.types"
    return {"byPlot": by_plot, "badFloors": bad, "yards": results, "config": floors_config, "faces": set(faces), "noKind": no_kind, "typesOf": types_of}


def no_kind_why(index, plot):
    """R321. Why no floor of a kind names a plot that floors of no kind name: their type is not in the types the model is read by."""
    floors = index["noKind"].get(plot)
    if not floors:
        return None
    types = sorted({str(floor.get("typeName")) for floor in floors})
    ids = " ".join(str(floor.get("elementId")) for floor in plot_relations.first_by_id(floors))
    return "no floor of this kind names the plot, floors " + ids + " of type " + ", ".join(types) + " do and their type is not in " + index["typesOf"]


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
        if key == "height" and row.get(HEIGHT_FROM_LEVELS):
            # R325. A key gives FFL and El @ Entry and no height, so the height is worked out from them
            if "ffl" in levels and "entry" in levels:
                levels[key] = sourced(common.r3(levels["ffl"]["value"] - levels["entry"]["value"], 4), COMPUTED)
            else:
                builder.lack("levels.height", "no El @ Entry to work the height out from")
        elif key == "entry" and row.get(ENTRY_REFUSED):
            builder.lack("levels.entry", REFUSED + row[ENTRY_REFUSED])
        elif not is_number(value):
            builder.lack("levels." + key, "the schedule cell " + label + " holds no number")
        else:
            levels[key] = sourced(common.r3(float(value), 4 if key in WIDE_LEVELS else 3), row.get(ENTRY_SOURCE, PAST) if key == "entry" else PAST)
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


def only_one(builder, key, found, otherwise=None):
    if not found:
        builder.lack(key, otherwise or "no floor names the plot")
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
    generic = only_one(builder, "floors.generic", kinds.get("generic", []), no_kind_why(index, builder.plot))
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
    ramp = only_one(builder, "floors.ramp", kinds.get("ramp", []), no_kind_why(index, builder.plot))
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
        if row.get(MODEL_PLOT) is not None:
            # R325. The plot number of the past model itself, beside the official number the plot is filed by
            builder.entry[MODEL_PLOT] = sourced(str(row[MODEL_PLOT]))
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


def build_plan(data, rows, job, folder_files, prototype_folder, floors_config, faces, key=None):
    """The plan of R80 from the host document of prototypes.json and the schedule rows.

    floors_config is the floors block of the settings (R112) and faces the baseplan plot faces by official plot number (R111).
    key is the answer key the model is read as (R321), None when it is read by floors.types.
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
    index = floor_index(document, offset, floors_config, faces, key)
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


def location_numbers(labels_path):
    """R206. The location plan number of each baseplan face that holds exactly one plan label (R182), by face label."""
    import csv
    with open(labels_path, encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    found = {}
    for row in rows:
        if row.get("face"):
            found.setdefault(row["face"], []).append(row)
    return {face: items[0]["number"] for face, items in found.items() if len(items) == 1}, {face: len(items) for face, items in found.items() if len(items) > 1}


def face_shares(shape, labels, tree, faces):
    """R120, R325. The share of the area of a floor on each baseplan face it overlaps, the face area under the floor over the floor area,
    as (label, share) with the largest first. A share under SLIVER_SHARE is a sliver and is left out."""
    if shape is None or shape.area <= 0:
        return []
    found = []
    for index in tree.query(shape, predicate="intersects"):
        label = labels[int(index)]
        share = faces[label].intersection(shape).area / shape.area
        if share >= SLIVER_SHARE:
            found.append((label, share))
    return sorted(found, key=lambda item: (-item[1], link_summary.sort_key(item[0])))


def share_face(shares, share_min):
    """R325. The one face that holds at least share_min of a floor, else None and why: noFace with no share, twoFaces when two faces
    hold it (faces that overlap), cut when no face holds share_min, as R120 lists a plot that does not follow the baseplan cut."""
    held = [label for label, share in shares if share >= share_min - EPS]
    if len(held) == 1:
        return held[0], None
    return None, "twoFaces" if held else "cut" if shares else "noFace"


def share_rows(shares, numbers):
    """R325. The shares of a floor as the cut list writes them, each face with its location plan number."""
    return [{"face": label, "number": numbers.get(label), "share": round(share, 3)} for label, share in shares]


def names_dialect(data, floors_config, faces, numbers, frames=None, entry_parameter=None, share_min=None, entry_heights=None):
    """R206. A past model that keeps no plot parameter on its links and has no schedule here: each link instance Name and each floor
    Plot value is <unit>-<number> in the model's own numbering. The official number of a plot is the location plan number of the
    baseplan face its generic floor stands on, so the number is taken by position and never by a shift. The links and floors get that
    official number, and one row per plot with the unit and the generic floor top as FFL stands in for the schedule the past job lacks.
    faces are the baseplan faces by label with shift 0, numbers the face to location plan number of location_numbers.
    R264. frames gives the house centre per link file learned on other models of the same job, used for a file this model
    maps no link of by its Name.
    R325. A link of a Parsons key holds its plot number in RRE_ZZ_SP_Plot-Number and a Name with bars, so it maps first by the one
    floor Plot value whose number after the last dash is that plot number, then by its Name, then by its frame. It keeps its own unit,
    and a link no route maps or that is refused loses its plot number, so it never joins a plot by a number nothing placed. With
    entry_parameter the row of a plot also holds El @ Entry from that parameter of its generic floor and the steps of its link.
    With share_min a generic floor joins the one face that holds at least that share of its area (R120), never the face a point inside it
    falls on, and a floor no face holds so is listed in cutPlots with its shares and names no plot. With entry_heights, a min and max in
    metres, an El @ Entry that puts the floor top outside that band over it is refused with why and never used."""
    from shapely.geometry import Point
    from shapely.strtree import STRtree

    document = host_of(data)
    offset = ramp_levels.offset_of(document)
    labels = sorted(faces, key=link_summary.sort_key)
    tree = STRtree([faces[label] for label in labels])
    parameter = floors_config["plotParameter"]
    official, report, rows, shapes, refused, cut = {}, Counter(), {}, {}, set(), []
    for floor in document.get("floors") or []:
        if kind_of(floor, floors_config) != "generic":
            continue
        value = str((floor.get("parameters") or {}).get(parameter) or "")
        shape, _ = plot_relations.floor_shape(floor, offset)
        if "-" not in value or shape is None:
            report["generic floor with no plot value"] += 1
            continue
        if share_min is None:
            hits = [labels[index] for index in tree.query(Point(shape.representative_point()), predicate="within")]
            number = numbers.get(hits[0]) if len(hits) == 1 else None
        else:
            shares = face_shares(shape, labels, tree, faces)
            face, why = share_face(shares, share_min)
            if face is None:
                report["generic floor cut by the baseplan" if why == "cut" else "generic floor on " + why] += 1
                cut.append({"modelPlot": value, "why": why, "shares": share_rows(shares, numbers)})
                continue
            number = numbers.get(face)
        if number is None:
            report["generic floor on no face with one plan label"] += 1
            continue
        if value in official or number in rows or value in refused or number in refused:
            # R325. Both floors leave, and every value and number tied to them, so no link maps to a plot whose row is gone
            report["plot value or official number given twice"] += 1
            gone = {value, number}
            for other in [other for other, found in official.items() if other == value or found == number]:
                gone |= {other, official.pop(other)}
                shapes.pop(other, None)
            for item in gone:
                rows.pop(item, None)
            refused |= gone
            continue
        low, high, _ = plot_relations.top_range(floor, offset)
        official[value] = number
        shapes[value] = shape
        ffl = high if low is not None and abs(high - low) <= EPS else None
        rows[number] = {"Plot No.": int(number), "Unit": value.rpartition("-")[0], "FFL": ffl, "modelPlot": value.rpartition("-")[2], "sheetRow": None}
        entry = (floor.get("parameters") or {}).get(entry_parameter) if entry_parameter else None
        if entry_parameter:
            rows[number][ENTRY_SOURCE] = KEY_PARAMETER
        if is_number(entry) and entry_heights is not None and (ffl is None or not entry_heights["min"] - EPS <= ffl - entry <= entry_heights["max"] + EPS):
            # R325. A placeholder such as 150.0 puts El @ Entry metres off the floor top, so it is listed and never used
            rows[number][ENTRY_REFUSED] = ("the " + entry_parameter + " value has no flat floor top to give a height" if ffl is None else
                                           "the " + entry_parameter + " value puts the floor top %.3f m over it, outside keyPlans.entryHeightM %g to %g m"
                                           % (ffl - entry, entry_heights["min"], entry_heights["max"]))
            report["El @ Entry refused, its height is outside keyPlans.entryHeightM"] += 1
        elif is_number(entry):
            rows[number]["El @ Entry"] = entry
    links = document.get("links") or []
    by_number = number_routes(official)
    learned = dict(frames or {}, **house_offsets(links, official, shapes, offset, by_number))
    values = {id(link): name_fallback(link, official, shapes, learned, offset, by_number) for link in links}
    claimed = Counter(value for value, _ in values.values() if value is not None)
    for link in links:
        parameters = link.setdefault("parameters", {})
        value, method = values[id(link)]
        if value is None or claimed[value] > 1:
            report["link with no mapped name" if value is None else "link refused, its plot claimed by two links"] += 1
            if parameters.pop(PLOT_PARAMETER, None) is not None:
                report["link plot number dropped"] += 1
            continue
        parameters[PLOT_PARAMETER] = official[value]
        own = parameters.get(UNIT_PARAMETER)
        if not (isinstance(own, str) and own.strip()):
            parameters[UNIT_PARAMETER] = value.rpartition("-")[0]
        if entry_parameter:
            rows[official[value]]["Steps Needed"] = parameters.get(STEPS_PARAMETER)
            rows[official[value]][HEIGHT_FROM_LEVELS] = True
        report["link mapped" if method == "name" else "link mapped by " + method] += 1
    for floor in document.get("floors") or []:
        value = str((floor.get("parameters") or {}).get(parameter) or "")
        if value in official:
            floor["modelPlot"] = value
            floor["plotNumber"] = official[value]
            floor["parameters"][parameter] = value.rpartition("-")[0] + "-" + official[value]
            report["floor mapped"] += 1
        elif "-" in value:
            # R325. Its plot has no official number, so it keeps its value apart and names no plot by the number of the model
            floor["modelPlot"] = value
            floor["plotNumber"] = None
            floor["parameters"][parameter] = None
            report["floor with no official number"] += 1
    report["plots"] = len(rows)
    found = dict(sorted(report.items()))
    if share_min is not None:
        found["cutPlots"] = cut
    return data, [rows[number] for number in sorted(rows, key=link_summary.sort_key)], found


def in_link_axes(link, point, offset):
    """A plan point in the axes of a link, from its origin along its X and Y axes, so a mirrored link is read mirrored."""
    transform = link["transform"]
    origin, x, y = transform["origin"], transform["axisX"], transform["axisY"]
    dx, dy = point[0] - origin[0] - offset[0], point[1] - origin[1] - offset[1]
    return dx * x[0] + dy * x[1], dx * y[0] + dy * y[1]


def number_routes(official):
    """R325. The floor Plot value of each plot number, the part after the last dash as a whole number as direct_value takes a link plot
    number, so VD2-0481 ends in 481, where exactly one value of the model ends in it."""
    values = {}
    for value in official:
        number = plot_number(value.rpartition("-")[2])
        if number is not None:
            values.setdefault(number, []).append(value)
    return {number: found[0] for number, found in values.items() if len(found) == 1}


def direct_value(link, official, by_number=None):
    """R325, R206. The floor Plot value a link names itself, and how: by its plot number parameter through by_number, else by its Name
    when that is a floor Plot value. None and None when it names none, so its frame decides (R264)."""
    parameters = link.get("parameters") or {}
    number = plot_number(parameters.get(PLOT_PARAMETER))
    if number is not None and number in (by_number or {}):
        return by_number[number], "parameter"
    value = str(parameters.get(NAME_PARAMETER) or "")
    return (value, "name") if value in official else (None, None)


def house_offsets(links, official, shapes, offset, by_number=None):
    """R264. Per link file, the median place of the plot face centre in the link axes, from the links whose Name is a floor Plot value,
    and with by_number from the links whose plot number parameter names one (R325)."""
    import statistics
    found = {}
    for link in links:
        value, _ = direct_value(link, official, by_number)
        if value is not None and link.get("transform") and link.get("file"):
            centre = shapes[value].centroid
            found.setdefault(link["file"], []).append(in_link_axes(link, (centre.x, centre.y), offset))
    return {name: (statistics.median(a for a, _ in items), statistics.median(b for _, b in items)) for name, items in found.items()}


def model_frames(data, floors_config):
    """R264. The house centre per link file of one model by its own Plot values, for another model of the same job."""
    document = host_of(data)
    offset = ramp_levels.offset_of(document)
    shapes = {}
    for floor in document.get("floors") or []:
        value = str((floor.get("parameters") or {}).get(floors_config["plotParameter"]) or "")
        shape = plot_relations.floor_shape(floor, offset)[0] if kind_of(floor, floors_config) == "generic" else None
        if "-" in value and shape is not None:
            shapes[value] = shape
    return house_offsets(document.get("links") or [], shapes, shapes, offset)


def house_centre(link, learned, offset):
    """R264. The house centre of a link in shared plan coordinates from the place learned for its file in its own axes, or None."""
    from shapely.geometry import Point
    place = learned.get(link.get("file"))
    if place is None or not link.get("transform"):
        return None
    transform = link["transform"]
    origin, x, y = transform["origin"], transform["axisX"], transform["axisY"]
    return Point(origin[0] + offset[0] + place[0] * x[0] + place[1] * y[0], origin[1] + offset[1] + place[0] * x[1] + place[1] * y[1])


def name_fallback(link, official, shapes, learned, offset, by_number=None):
    """R264. The plot value a link stands on, and how it was found: its plot number parameter or its Name when that names a floor Plot
    value (R325), else the face that holds the house centre of its file in its axes. A past model may write a level such as
    EL@-Entry +164.98 or a wrong plot into the instance Name of a link, so the digits of a Name that is not a Plot value are never trusted."""
    value, method = direct_value(link, official, by_number)
    if value is not None:
        return value, method
    centre = house_centre(link, learned, offset)
    if centre is None:
        return None, None
    hits = [other for other, shape in shapes.items() if shape.contains(centre)]
    return (hits[0], "frame") if len(hits) == 1 else (None, None)


WALL_FIELDS = (("baseConstraint", "Base Constraint"), ("baseOffset", "Base Offset"), ("topConstraint", "Top Constraint"),
               ("topOffset", "Top Offset"), ("locationLine", "Location Line"))


def load_key_plans(settings):
    """R325. keyPlans.wallEdgeM of the project settings, how near a wall middle must lie to the edge of a generic floor to go to its plot,
    in metres above 0 and under 1, pastAll.entryParameter, the generic floor parameter that holds El @ Entry in a key, keyPlans.entryHeightM,
    the min and max in metres of the floor top over El @ Entry a key value must give to be used, and cadLinks.cutShareMin (R120), the share of
    its area a generic floor must hold on one face to join it."""
    import cad_links
    name = settings.path.name
    block = settings.values.get("keyPlans")
    edge = block.get("wallEdgeM") if isinstance(block, dict) else None
    if not is_number(edge) or not 0 < edge < 1:
        raise project_profile.ProfileError(name + " keyPlans.wallEdgeM must be metres above 0 and under 1")
    entry = (settings.values.get("pastAll") or {}).get("entryParameter")
    if not isinstance(entry, str) or not entry.strip():
        raise project_profile.ProfileError(name + " pastAll.entryParameter must name the El @ Entry parameter of a generic floor")
    return {"wallEdgeM": edge, "entryParameter": entry, "entryHeightM": entry_height_band(settings), "cutShareMin": cad_links.cad_links_settings(settings)["cutShareMin"]}


def entry_height_band(settings):
    """R325, R327. keyPlans.entryHeightM, the min and the max in metres the flat floor top must stand over a key El @ Entry value for it to be used."""
    band = (settings.values.get("keyPlans") or {}).get("entryHeightM")
    if not isinstance(band, dict) or set(band) != {"min", "max"} or not all(is_number(band[key]) for key in band) or not band["min"] < band["max"]:
        raise project_profile.ProfileError(settings.path.name + " keyPlans.entryHeightM must hold a min under a max in metres")
    return dict(band)


def key_walls(data, floors_config, edge_m):
    """R325. Every wall of a key for the sheets of Part 2: type, length, height, its base and top constraint and offset and location
    line as the read holds them, the ends of its location line in shared coordinates, and the plots whose generic floor holds the middle
    of that line or has an edge within edge_m of it, so a party wall goes to both plots. A wall whose location is no line has no ends
    and no plot. The generic floors are taken by the official number names_dialect gave them."""
    from shapely.geometry import Point
    document = host_of(data)
    offset = ramp_levels.offset_of(document)
    shapes = {}
    for floor in document.get("floors") or []:
        plot = plot_number(floor.get("plotNumber"))
        shape = plot_relations.floor_shape(floor, offset)[0] if plot is not None and kind_of(floor, floors_config) == "generic" else None
        if shape is not None:
            shapes[plot] = shape
    found, counts, not_line = [], Counter(), 0
    for wall in document.get("walls") or []:
        location = wall.get("location") or {}
        parameters = wall.get("parameters") or {}
        ends, plots = [None, None], []
        if location.get("kind") == "line" and all(isinstance(location.get(end), list) and len(location[end]) == 3 for end in ("start", "end")):
            ends = [point([location[end][axis] + offset[axis] for axis in range(3)]) for end in ("start", "end")]
            middle = Point((ends[0][0] + ends[1][0]) / 2, (ends[0][1] + ends[1][1]) / 2)
            plots = sorted((plot for plot, shape in shapes.items() if shape.contains(middle) or shape.boundary.distance(middle) <= edge_m + EPS),
                           key=link_summary.sort_key)
        else:
            not_line += 1
        entry = {"elementId": wall.get("elementId"), "type": wall.get("typeName"), "lengthM": common.r3(wall["length"]) if is_number(wall.get("length")) else None,
                 "heightM": common.r3(wall["height"]) if is_number(wall.get("height")) else None, "start": ends[0], "end": ends[1], "plots": plots}
        entry.update({key: parameters.get(label) for key, label in WALL_FIELDS})
        counts[str(len(plots))] += 1
        found.append(entry)
    return found, {"walls": len(found), "withNoPlot": counts["0"], "byPlotCount": dict(sorted(counts.items())), "notALine": not_line}


def answer_key_listing(listing, data):
    """R206. The folder listing with the link files of the model added, and those of them the folder lacks, by count.
    A past model may link prototypes that are not on this machine, its plan still says which file each plot had."""
    files = Counter(link.get("file") for link in host_of(data).get("links") or [] if link.get("file"))
    known = set(listing or set())
    lacking = {name: count for name, count in sorted(files.items()) if name.lower() not in known}
    return known | {name.lower() for name in files}, lacking


def mark_answer_key(plan, lacking):
    """R206. Such a plan is for comparing only, the placer must never be given it."""
    plan["answerKeyOnly"] = True
    plan["summary"]["linkFilesNotInPrototypeFolder"] = lacking


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


def key_plan_refusal(settings, out, job, key, answer_key):
    """R325. Why a plan may not be written to out, or None. A plan of an answer key, with --answer-key or --key, never goes over
    pastJob.output, the plan of the past job it is compared with. With --key the file name holds the code of that key and of no other
    key, so plan_imc_key_past.json is never the plan of IMD. A file that holds the plan of a key is only written again by a run of that key."""
    if (answer_key or key) and out.resolve() == Path(job["output"]).resolve():
        return "the plan of an answer key is written under its own name, never over pastJob.output " + str(job["output"])
    codes = [code for code in key_codes(settings) if isinstance(code, str)]
    words = set(re.split(r"[^0-9a-z]+", out.stem.lower()))
    if key and (key.lower() not in words or [code for code in codes if code != key and code.lower() in words]):
        return "the file name " + out.name + " must name the key " + key + " and no other answer key"
    if out.is_file():
        try:
            held = (link_summary.load(out).get("sources") or {}).get("answerKey")
        except (ValueError, AttributeError):
            held = None
        if held and held != key:
            return out.name + " holds the plan of the key " + str(held) + ", only a run with --key " + str(held) + " writes it again"
    return None


def main(argv=None):
    parser = argparse.ArgumentParser(description="Build plan.json from a past job, prototypes.json of the past model and its schedule.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH), help="project settings, default config/project.json")
    parser.add_argument("--prototypes", help="prototypes.json, default pastJob.prototypes")
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--shifts", default=str(project_profile.ROOT / "config" / "plot_numbers.json"))
    parser.add_argument("--out", help="plan file, default pastJob.output")
    parser.add_argument("--answer-key", action="store_true",
                        help="R206. A plan to compare with only: link files not in the prototype folder are kept and counted, the plan is marked answerKeyOnly")
    parser.add_argument("--names", metavar="LABELS_CSV",
                        help="R206. The past model keeps <unit>-<number> in link names and floor Plot values and has no schedule here. "
                             "The official number comes from the location plan labels.csv of the data book by the face of the generic floor")
    parser.add_argument("--frames-from", nargs="*", metavar="PROTOTYPES_JSON",
                        help="R264. Reader outputs of other models of the same job, for the house centre of a link file this model names no link of")
    parser.add_argument("--key", metavar="CODE",
                        help="R321. The answer key the past model is, its floors are read by floors.keyTypes of that key and floors.types")
    args = parser.parse_args(argv)
    if args.names and not args.answer_key:
        parser.error("--names goes with --answer-key, such a plan is never placed")
    settings = project_profile.load_settings(args.settings)
    job = load_job(settings)
    floors = load_floors(settings, args.key)
    prototypes = Path(args.prototypes or job["prototypes"])
    baseplan = Path(args.baseplan or (project_profile.ROOT / job["baseplanPlots"] if job.get("baseplanPlots") else ""))
    out = Path(args.out or job["output"])
    plot_relations.refuse_inside_repo(out.resolve().parent)
    why = key_plan_refusal(settings, out, job, args.key, args.answer_key)
    if why:
        parser.error(why)
    root = input_root(settings)
    schedule = root / full_run.XLSX_FOLDER / job["schedule"]
    folder, source_folder = prototype_folders(root, job)
    for path in (prototypes, baseplan, Path(args.names) if args.names else schedule):
        if not path.is_file():
            print("not found: " + str(path), file=sys.stderr)
            return 2
    data = link_summary.load(prototypes)
    key_plans = None
    if args.key:
        check_key_file(settings, args.key, host_of(data).get("file"), read_job(prototypes))
        key_plans = load_key_plans(settings)
    dialect = None
    if args.names:
        numbers, many = location_numbers(args.names)
        by_label, bad_labels = yard_rule.face_shapes(link_summary.load(baseplan)["plots"], 0)
        frames = {}
        for other in args.frames_from or []:
            for name, centre in model_frames(link_summary.load(other), floors).items():
                frames.setdefault(name, centre)
        data, rows, dialect = names_dialect(data, floors, by_label, numbers, frames, key_plans and key_plans["entryParameter"],
                                            key_plans and key_plans["cutShareMin"], key_plans and key_plans["entryHeightM"])
        faces = {numbers[label]: shape for label, shape in by_label.items() if label in numbers}
    else:
        faces, bad_labels = baseplan_faces(baseplan, args.shifts, job["neighbourhood"])
        rows = excel_reader.read_sheet(schedule, job["sheet"])[3]
    listing = folder_listing(folder)
    if args.answer_key:
        listing, not_in_folder = answer_key_listing(listing, data)
    plan = build_plan(data, rows, job, listing, folder, floors, faces, args.key)
    if args.answer_key:
        mark_answer_key(plan, not_in_folder)
    plan["sources"]["prototypes"] = prototypes.name
    plan["sources"]["prototypeSourceFolder"] = str(source_folder)
    plan["sources"]["baseplanPlots"] = baseplan.name
    plan["summary"]["yardRule"]["facesWithBadLabel"] = len(bad_labels)
    if args.key:
        # R325. A plan of an answer key is named by the key, the areas of its plots come from the join of R326
        plan["neighbourhood"] = args.key
        plan["sources"]["answerKey"] = args.key
        plan["walls"], plan["summary"]["walls"] = key_walls(data, floors, key_plans["wallEdgeM"])
        print("walls %s" % plan["summary"]["walls"])
    if dialect is not None:
        plan["sources"]["schedule"] = "none, the generic floor tops of the model stand in for FFL"
        plan["sources"]["locationLabels"] = Path(args.names).name
        plan["summary"]["namesDialect"] = dialect
        print("names dialect %s" % {key: value for key, value in dialect.items() if key != "cutPlots"})
        if "cutPlots" in dialect:
            print("cut plots %d %s" % (len(dialect["cutPlots"]), dict(Counter(item["why"] for item in dialect["cutPlots"]))))
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
