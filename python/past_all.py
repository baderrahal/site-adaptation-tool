"""R267 to R269. Every past site model in one place: what each holds, when it was made, and one table of every plot.

inventory (R268): per model of the rvt_info.py output, its format, worksharing, the times its storages were written and the
prototype files it links, each with the folder and the date the folder name carries, such as 20221206 - Links.
coverage (R267): the plots of each answer key plan by neighbourhood and zone of the location plan, with the key units and the
Typology_Rev9 codes, the plots two keys share, and per neighbourhood the unit plots of the location plan no key holds.
table (R269): past_all.csv, one row per plot per key. Levels are taken off the datum of the key, its survey elevation for a
Native key, as in R215. A key of the Native dialect is first mapped to official numbers by R206 and R264, so its floor
parameters can be read per plot. The parameters written are those pastAll in config/project.json names.
R326. Each row names the job of its key, the group of majority.groups that holds it, in the column source, and the kind of an answer
key, TU or SE of answerKeys, in the column kind. The rows of one job of an older table can be kept cell for cell, an answer key of Round
ADDIN is read by the floor types of its code (R321) and joins its plots by area share (R325), El @ Entry of a schedule goes only to the
key of its own job, and heightSource says what the height was worked out from.
Nothing is written inside the repo.
"""
import argparse
import copy
import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely import wkt
from shapely.geometry import Point

import common
import key_cover
import link_summary
import placer_check
import plan_from_past
import plot_relations
import project_profile
import ramp_levels
import yard_rule

DATE_IN_PATH = re.compile(r"(?<!\d)(20\d{2})(\d{2})(\d{2})(?!\d)")
COLUMNS = ["key", "source", "kind", "plot", "nbh", "zone", "typologyRev9", "planCode", "keyUnit", "linkFile", "x", "y", "rotationDeg", "mirrored", "case",
           "footprintResidualM", "genericType", "genericAreaM2", "genericTop", "yardType", "yardAreaM2", "yardTop", "rampType", "rampAreaM2", "rampCorners", "rampSlopePercent",
           "rampCrossPercent", "rampFront", "rampBack", "rampFrontFall", "ffl", "entry", "entrySource", "height", "heightSource", "steps", "datumM", "linkName",
           "genericOutline", "rampOutline", "parameters"]
# R326. The FFL of a plan of the names dialect is the top of the generic floor of the key, else it is the schedule of the past job
FFL_FROM_FLOOR = "floorTop"
FFL_FROM_SCHEDULE = "schedule"
# R326. Where a key has a schedule row, its FFL, El @ Entry and height are that pair, and each key value off it by more than the band is listed
CONFLICT_COLUMNS = ["key", "plot", "field", "keyValue", "scheduleValue", "gapM", "kept"]


def path_date(path):
    """The first date of eight digits in a folder name of the path, as yyyy-mm-dd, or None."""
    for part in re.split(r"[\\/]", str(path))[:-1]:
        match = DATE_IN_PATH.search(part)
        if match:
            return "%s-%s-%s" % match.groups()
    return None


def inventory(rows):
    """R268. Per model: format, worksharing, storage times and the linked prototype files with folder and folder date."""
    found = []
    for row in rows:
        info = row.get("info") or {}
        links = []
        for link in row.get("revitLinks") or []:
            path = link.get("absolutePath") or link.get("path") or ""
            links.append({"file": link.get("file"), "folder": str(path).replace("/", "\\").rpartition("\\")[0].rpartition("\\")[2], "folderDate": path_date(path)})
        found.append({"file": row["file"], "format": info.get("formatYear"), "workshared": info.get("workshared"), "storageTimes": row.get("storageTimes"),
                      "links": links, "linkFolderDates": dict(Counter(link["folderDate"] or "none" for link in links))})
    return found


def coverage(plans, labels):
    """R267. Per key its plots by neighbourhood, per neighbourhood the union of the keys against the location plan unit plots,
    and the plots each pair of keys shares."""
    by_key, held = {}, defaultdict(set)
    for name, plan in plans.items():
        by_key[name] = key_cover.cover(plan, labels)
        for entry in plan["plots"]:
            held[entry["plot"]].add(name)
    units = defaultdict(set)
    for number, label in labels.items():
        if label.get("nbh") and label.get("kind") == "clientCode":
            units[label["nbh"]].add(number)
    union = {}
    for nbh, numbers in sorted(units.items()):
        inside = {number for number in numbers if number in held}
        union[nbh] = {"zone": next((labels[n].get("zone") for n in numbers), None), "unitPlots": len(numbers), "heldByAKey": len(inside),
                      "heldByNoKey": len(numbers) - len(inside), "byKey": dict(Counter(key for n in inside for key in held[n])),
                      "noKeyCodes": dict(Counter(labels[n].get("rev9") or "none" for n in numbers - inside).most_common())}
    pairs = Counter("+".join(sorted(keys)) for keys in held.values() if len(keys) > 1)
    outside = Counter(key for number, keys in held.items() for key in keys if not (labels.get(number) or {}).get("nbh"))
    total = sum(len(numbers) for numbers in units.values())
    return {"byKey": by_key, "byNeighbourhood": union, "sharedByKeys": dict(pairs), "keyPlotsWithNoNeighbourhood": dict(outside),
            "phase": {"unitPlots": total, "heldByAKey": sum(row["heldByAKey"] for row in union.values()),
                      "neighbourhoodsWithNoKeyPlot": [nbh for nbh, row in union.items() if not row["heldByAKey"]]}}


def floor_parameters(data, dialect, floors, faces, numbers, names, share_min=None):
    """The parameters of pastAll.floorParameters on the Generic floor of each plot, by official number, and the datum of the key. With
    share_min the generic floors join their faces by area share as the plan of an answer key does (R325)."""
    data = copy.deepcopy(data)
    key_floors = placer_check.dialect_floors(floors, dialect)
    if dialect["plot"] == "name":
        data, _, _ = plan_from_past.names_dialect(data, key_floors, faces, numbers, share_min=share_min)
    document = plan_from_past.host_of(data)
    datum = ramp_levels.offset_of(document)[2] if dialect["datum"] == "surveyElevation" else 0.0
    found = {}
    for floor in document.get("floors") or []:
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if plot is not None and plan_from_past.kind_of(floor, key_floors) == "generic":
            found[plot] = {name: value for name, value in (floor.get("parameters") or {}).items() if name in names and value not in (None, "")}
    return found, datum


def number(value):
    return float(value) if plan_from_past.is_number(value) else None


def ramp_ends(corners, face):
    """The mean level of the front pair of corners, at the road, and of the back pair, at the entrance, and the fall along the
    front. The front is the long edge whose middle lies nearer the boundary of the plot face, so a ramp that falls toward the
    house still has its front at the road. None when the ramp has no four corners or there is no face."""
    geometry = plot_relations.ramp_geometry(corners) if len(corners) == 4 else None
    if geometry is None or face is None:
        return None
    edges = [geometry["edges"][0], geometry["edges"][2]]
    near = [face.boundary.distance(Point((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)) for a, b in edges]
    front, back = (edges[0], edges[1]) if near[0] <= near[1] else (edges[1], edges[0])
    return (front[0][2] + front[1][2]) / 2, (back[0][2] + back[1][2]) / 2, abs(front[0][2] - front[1][2])


def floor_cells(floor, datum, face=None):
    if not floor:
        return {}
    shape = plot_relations.even_odd([plan_from_past.loop_points(loop) for loop in floor["outline"]["value"]["loops"]])
    cells = {"type": floor["type"]["value"], "area": None if shape is None else round(shape.area, 3), "outline": None if shape is None else shape.wkt}
    if "z" in floor:
        cells["top"] = common.r3(floor["z"]["value"] - datum)
    corners = (floor.get("corners") or {}).get("value")
    if corners:
        lowered = [[c[0], c[1], round(c[2] - datum, 4)] for c in corners]
        cells["corners"] = json.dumps(lowered)
        geometry = plot_relations.ramp_geometry(lowered) if len(lowered) == 4 else None
        # The short edges run from the road to the house, so their slope is the ramp slope and the long edges give the cross fall
        cells["slope"] = None if geometry is None or geometry["slope_short"] is None else round(geometry["slope_short"], 3)
        cells["cross"] = None if geometry is None else round(geometry["slope"], 3)
        ends = ramp_ends(lowered, face)
        if ends is not None:
            cells["front"], cells["back"], cells["frontFall"] = round(ends[0], 4), round(ends[1], 4), round(ends[2], 4)
    return cells


def refused_entry(plan, plot):
    """R325. True when the plan lists El @ Entry of the plot as refused, a key value off keyPlans.entryHeightM."""
    return any(item["key"] == "levels.entry" and str(item.get("why", "")).startswith(plan_from_past.REFUSED)
               for row in plan.get("missing") or [] if row.get("plot") == plot for item in row.get("missing") or [])


def conflict(name, plot, field, key_value, schedule_value, band):
    """R326. The row of the conflicts list when a key value is off the schedule value by more than band, else None. The schedule is kept."""
    if key_value is None or abs(schedule_value - key_value) <= band + 1e-9:
        return None
    return {"key": name, "plot": plot, "field": field, "keyValue": key_value, "scheduleValue": schedule_value, "gapM": round(schedule_value - key_value, 4),
            "kept": FFL_FROM_SCHEDULE}


def table_rows(name, plan, data, dialect, floors, faces, numbers, labels, settings_block, footprint=None, schedule=None, code=None, share_min=None,
               conflicts=None, level_band=None):
    """R269. One row per plot of the key. R326. schedule is the FFL and El @ Entry of the schedules of plots.csv by official number, each with
    its source, and a row takes them first only when pastAll.scheduleKeys gives that source the code of this key, so a Native key or another
    key on the same plots never does. Such a row takes FFL, El @ Entry and the height as one pair of the schedule, never the key floor top
    with the schedule El @ Entry, and each key value, the floor top or the El @ Entry of the plan, that is off the schedule by more than
    level_band goes to conflicts with both values. A schedule row with El @ Entry and no FFL is refused. The El @ Entry a plan of an answer key
    holds (R325) is the parameter of its generic floor, so its source is floorParameter, and a value that plan refused is never taken again
    from the floor or the ramp, its source is refused. heightSource is where the FFL came from less the source of El @ Entry."""
    params, datum = floor_parameters(data, dialect, floors, faces, numbers, settings_block["floorParameters"], share_min)
    entry_parameter = settings_block["entryParameter"]
    own = {}
    if code and schedule:
        import key_join
        own = key_join.schedule_pairs(schedule, code, settings_block.get("scheduleKeys") or {})
    if own and (conflicts is None or level_band is None):
        raise ValueError("a schedule pair needs the conflicts list and the level band, so a key value off it is never left unlisted")
    answer = (plan.get("sources") or {}).get("answerKey")
    ffl_source = FFL_FROM_FLOOR if "namesDialect" in (plan.get("summary") or {}) else FFL_FROM_SCHEDULE
    rows = []
    for entry in sorted(plan["plots"], key=lambda item: link_summary.sort_key(item["plot"])):
        plot = entry["plot"]
        label = labels.get(plot) or {}
        value = lambda key: (entry.get(key) or {}).get("value")
        levels = entry.get("levels") or {}
        ffl = (levels.get("ffl") or {}).get("value")
        generic = floor_cells((entry.get("floors") or {}).get("generic"), datum)
        face = wkt.loads(generic["outline"]) if generic.get("outline") else None
        ramp = floor_cells((entry.get("floors") or {}).get("ramp"), datum, face)
        yard = floor_cells((entry.get("floors") or {}).get("yard"), datum)
        floor_params = params.get(plot, {})
        # The schedule of the job of the key gives El @ Entry, else the plan, else the FFL floor of a Native key, else the top of its ramp
        entry_level = (levels.get("entry") or {}).get("value")
        source = None if entry_level is None else "floorParameter" if answer else "schedule"
        height_from = ffl_source
        if plot in own:
            pair = own[plot]
            if pair["ffl"] is None:
                raise project_profile.ProfileError("the schedule row of plot " + plot + " of the key " + name + " has El @ Entry and no FFL, a pair is taken whole")
            for field, key_value, schedule_value in (("ffl", ffl, pair["ffl"]), ("entry", entry_level, pair["entry"])):
                found = conflict(name, plot, field, key_value, schedule_value, level_band)
                if found:
                    conflicts.append(found)
            ffl, entry_level, source, height_from = pair["ffl"], pair["entry"], "schedule", FFL_FROM_SCHEDULE
        elif entry_level is None and answer:
            source = "refused" if refused_entry(plan, plot) else None
        elif entry_level is None and number(floor_params.get(entry_parameter)) is not None:
            entry_level, source = number(floor_params[entry_parameter]), "floorParameter"
        elif entry_level is None and ramp.get("back") is not None:
            entry_level, source = ramp["back"], "rampBack"
        ffl_design = None if ffl is None else ffl - datum
        insert = value("insert") or [None, None]
        link_parameters = {key: item["value"] for key, item in (entry.get("parameters") or {}).items() if key in settings_block["linkParameters"]}
        link_parameters.update({"floor " + key: item for key, item in floor_params.items()})
        rows.append({
            "key": name, "plot": plot, "nbh": label.get("nbh"), "zone": label.get("zone"), "typologyRev9": label.get("rev9"), "planCode": label.get("code"),
            "keyUnit": value("unit"), "linkFile": value("prototypeFile"), "x": insert[0], "y": insert[1], "rotationDeg": value("rotationDeg"),
            "mirrored": value("mirrored"), "case": value("case"), "footprintResidualM": (footprint or {}).get(plot),
            "genericType": generic.get("type"), "genericAreaM2": generic.get("area"), "genericTop": generic.get("top"),
            "yardType": yard.get("type"), "yardAreaM2": yard.get("area"), "yardTop": yard.get("top"), "rampType": ramp.get("type"), "rampAreaM2": ramp.get("area"), "rampCorners": ramp.get("corners"), "rampSlopePercent": ramp.get("slope"),
            "rampCrossPercent": ramp.get("cross"), "rampFront": ramp.get("front"), "rampBack": ramp.get("back"), "rampFrontFall": ramp.get("frontFall"), "ffl": None if ffl_design is None else common.r3(ffl_design),
            "entry": None if entry_level is None else round(entry_level, 4), "entrySource": source,
            "height": None if entry_level is None or ffl_design is None else round(ffl_design - entry_level, 4),
            "heightSource": None if entry_level is None or ffl_design is None else height_from + " less " + source,
            "steps": (levels.get("steps") or {}).get("value"), "datumM": common.r3(datum), "linkName": value("linkName"),
            "genericOutline": generic.get("outline"), "rampOutline": ramp.get("outline"), "parameters": json.dumps(link_parameters, ensure_ascii=False, sort_keys=True),
        })
    return rows


def source_of(key, groups):
    """R326. The job of a key, the group of majority.groups that holds it. A key of no group or of two is refused."""
    found = [group for group, keys in groups.items() if key in keys]
    if len(found) != 1:
        raise project_profile.ProfileError("majority.groups must hold the key " + str(key) + " in one group, it is in " + str(len(found)))
    return found[0]


def key_kind(settings, code):
    """R326. The kind of an answer key, TU or SE of answerKeys, and none for a key of no code."""
    if not code:
        return ""
    kinds = [key.get("kind") for key in settings.get("answerKeys.keys") if key.get("code") == code]
    if len(kinds) != 1 or not kinds[0]:
        raise project_profile.ProfileError("answerKeys has no one key " + str(code) + " with a kind")
    return kinds[0]


def kept_rows(path, group, groups):
    """R326. The rows of an older past_all.csv whose key is in that group of majority.groups, every cell as the file holds it, with the
    group as their source."""
    if group not in groups:
        raise project_profile.ProfileError("majority.groups has no group " + str(group) + " to keep the rows of")
    with open(path, encoding="utf-8") as handle:
        return [dict(row, source=group) for row in csv.DictReader(handle) if row["key"] in groups[group]]


def write_csv(path, rows):
    path = Path(path)
    plot_relations.refuse_inside_repo(path.resolve().parent)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=COLUMNS)
        writer.writeheader()
        writer.writerows(rows)


def write_conflicts(table, conflicts):
    """R326. The conflicts list beside the table, <table>_conflicts.csv, one row per key value off its schedule with both values."""
    path = Path(table).with_name(Path(table).stem + "_conflicts.csv")
    plot_relations.refuse_inside_repo(path.resolve().parent)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=CONFLICT_COLUMNS)
        writer.writeheader()
        writer.writerows(conflicts)
    return path


def level_band(settings):
    """R326. majority.levelHoldMm of the project settings in metres, the band a key value may stand off its schedule before it is listed."""
    hold = settings.get("majority.levelHoldMm")
    if not plan_from_past.is_number(hold) or hold <= 0:
        raise project_profile.ProfileError(settings.path.name + " majority.levelHoldMm must be millimetres above 0")
    return hold / 1000.0


def read_table(path):
    """past_all.csv as rows with the numbers back as floats, None for an empty cell."""
    found = []
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            for key in ("x", "y", "rotationDeg", "genericAreaM2", "genericTop", "yardAreaM2", "yardTop", "rampAreaM2", "rampSlopePercent", "rampCrossPercent", "rampFront", "rampBack", "rampFrontFall", "ffl", "entry", "height", "footprintResidualM", "datumM"):
                row[key] = float(row[key]) if row.get(key) not in (None, "") else None
            row["mirrored"] = {"True": True, "False": False}.get(row.get("mirrored"))
            found.append(row)
    return found


def load_block(settings):
    block = settings.get("pastAll")
    if not isinstance(block, dict) or not all(isinstance(block.get(key), list) for key in ("linkParameters", "floorParameters")) or not block.get("entryParameter"):
        raise project_profile.ProfileError(settings.path.name + " pastAll must name linkParameters, floorParameters and entryParameter")
    keys = block.get("scheduleKeys", {})
    codes = [key.get("code") for key in (settings.values.get("answerKeys") or {}).get("keys") or []]
    if not isinstance(keys, dict) or not all(isinstance(text, str) and code in codes for text, code in keys.items()):
        raise project_profile.ProfileError(settings.path.name + " pastAll.scheduleKeys must map a pastSource of plots.csv to an answer key code")
    return block


def main(argv=None):
    parser = argparse.ArgumentParser(description="Inventory, coverage and one table of every past plot (R267 to R269).")
    parser.add_argument("what", choices=("inventory", "coverage", "table"))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--rvt-info", help="inventory: the rvt_info.py output")
    parser.add_argument("--key", nargs=4, action="append", metavar=("NAME", "DIALECT", "PLAN", "PROTOTYPES"), default=[],
                        help="coverage and table: one answer key, its placerCheck dialect, its plan and its reader output")
    parser.add_argument("--labels", help="location_plan/labels.csv of the snapshot")
    parser.add_argument("--baseplan", help="plots.json of the baseplan, for a key of plot by name")
    parser.add_argument("--footprint", nargs=2, action="append", metavar=("NAME", "FOOTPRINT_JSON"), default=[],
                        help="table: footprint_check.py output whose test set is that key, for its residual per plot")
    parser.add_argument("--key-code", nargs=2, action="append", metavar=("NAME", "CODE"), default=[],
                        help="table: the key NAME is the answer key CODE, read by its floor types (R321) and only from its work copy (R322)")
    parser.add_argument("--keep-from", nargs=2, metavar=("PAST_ALL_CSV", "GROUP"), help="table: keep the rows of that job of an older table cell for cell (R326)")
    parser.add_argument("--schedule-entry", metavar="PLOTS_CSV", help="table: plots.csv of the data book, El @ Entry of the schedule by official number (R326)")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    out = Path(args.out)
    if args.keep_from and out.resolve() == Path(args.keep_from[0]).resolve():
        parser.error("--out is the table --keep-from keeps rows of, it is never written over")
    settings = project_profile.load_settings(args.settings)
    plot_relations.refuse_inside_repo(out.resolve().parent)
    if args.what == "inventory":
        found = inventory(link_summary.load(args.rvt_info)["files"])
        common.write_json(out, {"models": found})
        for row in found:
            print("%s format %s, written %s, last saved %s, link folder dates %s" % (row["file"], row["format"], (row["storageTimes"] or {}).get("firstWritten"),
                                                                                 (row["storageTimes"] or {}).get("lastSaved"), row["linkFolderDates"]))
        return 0
    labels = key_cover.labels_by_number(args.labels)
    if args.what == "coverage":
        result = coverage({name: link_summary.load(plan) for name, _, plan, _ in args.key}, labels)
        common.write_json(out, result)
        for nbh, row in result["byNeighbourhood"].items():
            print("NBH %s zone %s: unit plots %d, held by a key %d, by none %d, by key %s" % (nbh, row["zone"], row["unitPlots"], row["heldByAKey"], row["heldByNoKey"], row["byKey"]))
        print("shared by keys %s" % result["sharedByKeys"])
        print("phase %s" % result["phase"])
        return 0
    block = load_block(settings)
    split = placer_check.load_split(settings)
    floors = plan_from_past.load_floors(settings)
    numbers, _ = plan_from_past.location_numbers(args.labels)
    faces, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    footprints = {}
    for name, path in args.footprint:
        judged = link_summary.load(path)["testJudged"]
        footprints[name] = {row["plot"]: row["residualM"] for row in judged["rows"]}
    groups = settings.get("majority.groups")
    codes = dict(args.key_code)
    rows = kept_rows(args.keep_from[0], args.keep_from[1], groups) if args.keep_from else []
    kept = {row["key"] for row in rows}
    schedule, conflicts = None, []
    band = level_band(settings)
    if args.schedule_entry:
        import key_join
        schedule = key_join.read_plots_csv(args.schedule_entry)[1]
    for name, dialect, plan, prototypes in args.key:
        if name in kept:
            raise project_profile.ProfileError("the key " + name + " is both kept from " + args.keep_from[0] + " and given, give it once")
        data = link_summary.load(prototypes)
        key_floors, share_min = floors, None
        if name in codes:
            key_floors = plan_from_past.load_floors(settings, codes[name])
            plan_from_past.check_key_file(settings, codes[name], plan_from_past.host_of(data).get("file"), plan_from_past.read_job(prototypes))
            share_min = plan_from_past.load_key_plans(settings)["cutShareMin"]
        source, kind = source_of(name, groups), key_kind(settings, codes.get(name))
        rows += [dict(row, source=source, kind=kind) for row in table_rows(name, link_summary.load(plan), data, split["dialects"][dialect], key_floors, faces, numbers,
                                                                          labels, block, footprints.get(name), schedule, codes.get(name), share_min,
                                                                          conflicts, band)]
    write_csv(out, rows)
    if schedule is not None:
        listed = write_conflicts(out, conflicts)
        print("conflicts with the schedule over %g m %d, by key and field %s, %s" % (band, len(conflicts), dict(Counter((item["key"], item["field"]) for item in conflicts)), listed))
    print("rows %d, by key %s" % (len(rows), dict(Counter(row["key"] for row in rows))))
    print("by source %s" % dict(Counter(row["source"] for row in rows)))
    print("by source and kind %s" % dict(Counter((row["source"], row.get("kind") or "none") for row in rows)))
    print("El @ Entry source %s" % dict(Counter((row["key"], row["entrySource"]) for row in rows)))
    print("height source %s" % dict(Counter((row["key"], row.get("heightSource")) for row in rows)))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
