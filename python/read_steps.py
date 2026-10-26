"""R348 to R350. The Python side of the read steps of the parity harness (R345): what the Python readers the worker read of button 2
replaces give, per item of the data book, written to step_<step>_python.json for python/parity.py.

- labels (R348): per baseplan label of the plots.json of the baseplan read, every plot shape of that label as read, its rings, label
  types and areas, and whether yard_rule.whole_label takes it
- blocks (R348): per block insert of the footprint blocks.json, as project_profile.read_villa_blocks gives it (R300)
- curbs, rows and setbacks (R348): the lines of the curb layers of analysis.roadEdgeLayers and of the ROW layers of dataBook.join.row of the
  road layout read, and of the setback layer of dataBook.join.footprint of the footprint read, by cad_analysis.road_edge_pieces, the
  lines the build and the data book take (read_road_edges), each by its place in the read with its handle and layer
- surfaces and roadLevels (R349): per road surface of siteBuild.roadSurfaces the summary of landxml_reader.read_landxml, and per vertex
  of the baseplan face of every client code plot of the location plan the road level of plan_from_book.road_level, the first surface
  of the order that covers it snapped within siteBuild.snapM, with the surface that gave it, and none where no road surface covers it
  (R344), the snap held against siteBuild.snapMaxM by plan_rules.road_snap
- areaKey, dlusSetbacks and sales (R350): per plot the zone and neighbourhood of the DLUS and sales sheets of dataBook.data5.areaKey
  (area_key.by_plot), the setback sheet of dataBook.rev9.dlus as rev9_workbooks.sheet_rows gives it, and the sales row of
  dataBook.locationPlan.sales as corner_front.sales_rows gives it with the plain values of dataBook.rev9.sales, each workbook by its
  key of dataBook.rev9.workbooks, a table of no row and a value past the stated dimension of a sheet refused
- locationLabels, locationFit, controlCheck and controlFaces (R351): the location plan as location_plan.py numbers it, per label of the
  page its number, code and kind, the baseplan face of its point by the schedule fit of R181 with its offset and group, its page and site
  point and the new code, Typology_Rev9, NBH and zone of R182, per neighbourhood schedule the numbers the fit took and kept with the
  residuals, the control point fit of R387 as its check and every face it numbers otherwise with why
- plotTable (R352): per baseplan face inside the Phase 1 border the plot table of python/plot_table.py on the reads and the location plan
  of the same run, the faces of the baseplan read, the footprint blocks of the footprint read and the area key sheets

Every CadReader output is read through the units check of R388, a LandXML in meter only. A value of no geometry is written as read, a
value of geometry as common.round_tree writes it. Step files go outside any git work tree, real values stay out of git.
"""
import argparse
import datetime
import json
import sys
from collections import Counter
from pathlib import Path

import statistics
from collections import defaultdict

import numpy

import area_key
import cad_analysis
import cad_units
import common
import corner_front
import data_book
import face_index
import face_rings as ring_rules
import footprint_check
import full_run
import landxml_reader
import levels_proof
import link_summary
import location_plan
import parity
import plot_table
import plan_from_book
import plan_from_past
import plan_rules
import project_profile
import rev9_workbooks
import yard_rule

LOCATION_STEPS = ("locationLabels", "locationFit", "controlCheck", "controlFaces")
# R387. The fields of the step controlCheck but failed, each null when the check failed
CHECK_FIELDS = ("controlTexts", "marks", "markGapStartM", "markGapRefinedM", "refineFaces", "refineKept", "refineResidualKeptM", "againstScheduleM",
                "labelsOnAFaceSchedule", "labelsOnAFaceControl", "facesNumberedOtherwise", "unitFacesNumberedOtherwise")
TABLE_STEPS = ("plotTable",)
STEPS = ("labels", "blocks", "curbs", "rows", "setbacks", "surfaces", "roadLevels", "areaKey", "dlusSetbacks", "sales")
SCOPE = "project"
NO_GEOMETRY = "noGeometry"


def jsonable(value):
    """A cell as a step file holds it: a date or time as its ISO text, as the C# reader writes it, every other value as read. R350: a
    duration, which openpyxl gives as a timedelta, is refused as the C# refuses it."""
    if isinstance(value, datetime.timedelta):
        raise ValueError("a cell holds a duration, %s, which openpyxl gives as a timedelta, a duration is not read (R350)" % value)
    if isinstance(value, (datetime.datetime, datetime.date, datetime.time)):
        return value.isoformat()
    if isinstance(value, dict):
        return {("null" if key is None else key): jsonable(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [jsonable(item) for item in value]
    return value


def at_decimals(value, decimals):
    """R345, R351. A value of geometry by R385 at the decimals of its field, a list item by item, as common.round_tree writes it at 3 and
    the C# by Rounding.Fixed at the decimals of the field."""
    if isinstance(value, (list, tuple)):
        return [at_decimals(item, decimals) for item in value]
    return common.r3(value, decimals)


def entry(raw, fields):
    """A plot of a step file: a field of no geometry written as read, a field of geometry by R385 at the decimals of its field."""
    raw = json.loads(json.dumps(jsonable(raw)))
    written = {name: (value if (fields.get(name) or {}).get("kind") == NO_GEOMETRY else at_decimals(value, (fields.get(name) or {}).get("decimals", common.DECIMALS)))
               for name, value in raw.items()}
    return {"raw": raw, "written": json.loads(json.dumps(written))}


def label_step(plots):
    """R348. Per label of the plot shapes, as plots.json spells it, the rings, types and areas of its shapes in the order read."""
    found = {}
    for item in plots:
        if item.get("kind") != "plot_shape":
            continue
        label = "" if item.get("plot") is None else str(item.get("plot"))
        group = found.setdefault(label, {"rings": [], "types": [], "areas": [], "whole": yard_rule.whole_label(label) is not None})
        group["rings"].append([[vertex[0], vertex[1]] for vertex in item.get("vertices") or []])
        group["types"].append(item.get("type"))
        group["areas"].append(item.get("area"))
    return found


def by_handle(items):
    """The items by their handle, a handle met again named handle#n by its count, in the order read."""
    found, counts = {}, Counter()
    for handle, item in items:
        handle = "" if handle is None else str(handle)
        counts[handle] += 1
        found[handle if counts[handle] == 1 else "%s#%d" % (handle, counts[handle])] = item
    return found


def block_step(blocks):
    """R348. Per block insert of blocks.json its name, layer, mirror flag and scale, its insert point, rotation and outline."""
    return by_handle((block.get("handle"), {"name": block.get("name"), "layer": block.get("layer"), "mirrored": block.get("mirrored"), "scale": block.get("scale"),
                                            "insertPoint": block.get("insertPoint"), "rotationDeg": block.get("rotationDeg"), "outline": block.get("outline")})
                     for block in blocks)


def edge_step(pieces, layers):
    """R348. Per piece of the edge layers, by its place in the read, its handle, layer, kind and closed flag and the points of its line."""
    return {str(index): {"handle": piece.get("handle"), "layer": piece.get("layer"), "kind": piece.get("kind"), "closed": piece.get("closed"),
                         "points": [[x, y] for x, y in points]}
            for index, (piece, points) in enumerate(cad_analysis.road_edge_pieces(pieces, layers))}


def surface_step(reports, folders):
    """R349. Per surface of the LandXML reads in the order of the files, its file, unit and counts from the landxml_summary.json of each
    read, and its bounds and level range from the points.npy of the read as summary_of takes them, unrounded, since write_json writes the
    summary at the decimals of R2."""
    found = {}
    for report, folder in zip(reports, folders):
        for item in report["surfaces"]:
            if item["name"] in found:
                raise ValueError("two road surfaces are named %s, a surface is known by its name" % item["name"])
            xyz = numpy.load(Path(folder) / item["folder"] / "points.npy")
            held = len(xyz) > 0
            found[item["name"]] = {"file": report["file"], "linearUnit": report["linearUnit"], "epsgCode": report["epsgCode"], "pointCount": item["pointCount"],
                                   "faceCount": item["faceCount"], "visibleFaceCount": item["visibleFaceCount"], "invisibleFaceCount": item["invisibleFaceCount"],
                                   "badFaceCount": item["badFaceCount"], "badPointCount": item["badPointCount"], "declaredZRange": item["declaredZRange"],
                                   "bboxMin": [float(xyz[:, 0].min()), float(xyz[:, 1].min())] if held else None,
                                   "bboxMax": [float(xyz[:, 0].max()), float(xyz[:, 1].max())] if held else None,
                                   "zMin": float(xyz[:, 2].min()) if held else None, "zMax": float(xyz[:, 2].max()) if held else None}
    return found


def face_rings(plots, number_of):
    """The rings of the baseplan face of each plot number, the plot shapes of three vertices or more of its whole label, the label to number
    of plots.csv taken as plan_from_book.py takes the faces, a later label of one number over an earlier one."""
    rings = {}
    for item in plots:
        label = yard_rule.whole_label(item.get("plot")) if item.get("kind") == "plot_shape" else None
        vertices = item.get("vertices") or []
        if label is not None and len(vertices) >= 3:
            rings.setdefault(str(label), []).append(vertices)
    return {number_of[label]: found for label, found in rings.items() if label in number_of}


def road_level_step(numbers, faces, surfaces, snap):
    """R349. Per vertex of the face of each plot, <plot>/<k> in the order of its rings, the road level and the surface that gave it."""
    found = {}
    for number in numbers:
        index = 0
        for ring in faces.get(number) or []:
            for vertex in ring:
                level, name = plan_from_book.road_level(surfaces, (vertex[0], vertex[1]), snap)
                found["%s/%d" % (number, index)] = {"point": [vertex[0], vertex[1]], "level": level, "surface": name}
                index += 1
    return found


def held(count, entry_value, what):
    """R350. A table of no row is refused, so a step is never written empty: a sheet whose rows went missing gave 0 items on both sides
    and the parity held (the review of PR 53)."""
    if not count:
        raise ValueError("the sheet %s of the workbook %s holds no row under its header for %s, a table of no row is refused (R350)" % (
            entry_value["sheet"], entry_value["workbook"], what))


def area_key_step(dlus_rows, sales_rows):
    """R350. Per plot of the DLUS or the sales sheet, the zone and the neighbourhood of each by area_key.by_plot and two rows that differ."""
    dlus, dlus_two = area_key.by_plot(dlus_rows)
    sales, sales_two = area_key.by_plot(sales_rows)
    if not dlus or not sales:
        raise ValueError("the %s sheet of the area key holds no row under its header, a table of no row is refused (R350)" % ("DLUS" if not dlus else "sales"))
    found = {}
    for plot in list(dlus) + [plot for plot in sales if plot not in dlus]:
        own, other = dlus.get(plot), sales.get(plot)
        found[plot] = {"inDlus": own is not None, "zone": own[0] if own else None, "nbhId": own[1] if own else None, "twoRows": plot in dlus_two,
                       "inSales": other is not None, "salesZone": other[0] if other else None, "salesNbhId": other[1] if other else None,
                       "salesTwoRows": plot in sales_two}
    return found


def setback_key(value):
    """The plot of a DLUS row as rev9_workbooks.setbacks joins it."""
    return str(int(value)) if isinstance(value, (int, float)) else str(value)


def dlus_setback_step(rows):
    """R350. Per row of the DLUS setback sheet, by its plot, its plot cell and every other column of the config as sheet_rows gives it."""
    return by_handle((setback_key(item["plot"]), {**{"plotCell": item["plot"]}, **{name: value for name, value in item.items() if name != "plot"}}) for item in rows)


def sales_step(rows, plain_rows):
    """R350. Per plot of the sales sheet, its row as corner_front.sales_rows gives it, every cell by its header, and the plain values of the
    columns of dataBook.rev9.sales as sheet_rows gives them, the last row of the plot."""
    plain = {}
    for item in plain_rows:
        number = plan_from_past.plot_number(item.get("plot"))
        if number is not None:
            plain[number] = item
    return {number: {"row": row, "plain": plain.get(number)} for number, row in rows.items()}


def stat_values(values):
    """R351. The median, the 95th percentile and the largest of residuals as location_plan.stats takes them, unrounded, None for none."""
    values = sorted(float(value) for value in values)
    if not values:
        return None
    return [float(statistics.median(values)), values[int(0.95 * (len(values) - 1))], values[-1]]


def location_steps(settings, pdf, plots, schedule_paths, sales):
    """R351. The steps of the location plan as location_plan.py numbers it: the page read by pdfplumber, the labels paired, the page fitted
    to the site on the schedule of fitNeighbourhood (R181), every label on its face and its codes joined (R182), and the control point fit
    of R387 as its check, numbering nothing. The faces of the baseplan are refused as the build refuses them (R345)."""
    plan = settings.get("dataBook.locationPlan")
    connect = settings.get("connect")
    refused = ring_rules.refusals(plots, plan_from_book.area_tolerance(settings.values))
    if refused:
        raise ValueError("%d baseplan faces refused: %s" % (len(refused), ", ".join(refused)))
    page = location_plan.read_page(pdf, plan, "control")
    _, labels, _ = location_plan.label_pairs(page["chars"], plan)
    shifts = {name: entry["shift"] for name, entry in connect["neighbourhoods"].items() if entry}
    official_of = defaultdict(set)
    for name, official in data_book.schedule_rows(settings, connect, schedule_paths):
        official_of[name].add(official)
    faces = face_index.plan_faces(plots)
    matrix, _, fit = location_plan.schedule_fit(plan, labels, faces, official_of, shifts)
    rows, site, _ = location_plan.number_rows(labels, matrix, faces, official_of, shifts)
    location_plan.join_codes(rows, location_plan.read_table(sales, plan["sales"]["sheet"], plan["sales"]["headerRow"], plan["sales"]["columns"]),
                             location_plan.read_table(sales, plan["codeTable"]["sheet"], plan["codeTable"]["headerRow"], plan["codeTable"]["columns"]), plan)
    parts = location_plan.control_fit_parts(labels, rows, page["words"], page["curves"], faces, plan, site)
    made = {"locationLabels": by_handle((row["number"], {
        "number": row["number"], "code": row["code"], "kind": row["kind"], "face": row["face"], "offset": row["offset"], "group": row["group"],
        "page": [label["x"], label["y"]], "site": [float(point[0]), float(point[1])], "newCode": row["newCode"], "rev9": row["rev9"], "nbh": row["nbh"],
        "zone": row["zone"]}) for row, label, point in zip(rows, labels, site))}
    keep = fit["keep"]
    made["locationFit"] = {plan["fitNeighbourhood"]: {
        "role": "fit", "points": len(fit["used"]), "used": fit["used"], "kept": fit["kept"], "residualKeptM": stat_values(fit["residual"][keep]),
        "residualAllM": stat_values(fit["residual"]), "metresPerPoint": float(numpy.sqrt(abs(numpy.linalg.det(matrix[:2, :2]))))}}
    for name, (numbers, residual) in fit["others"].items():
        made["locationFit"][name] = {"role": "check", "points": len(numbers), "used": numbers, "kept": None, "residualKeptM": None,
                                     "residualAllM": stat_values(residual), "metresPerPoint": None}
    if parts["failed"]:
        # R387. A check that cannot be made is an issue of the read with why, every other field null, and the numbering goes on
        made["controlCheck"] = {"control": dict(dict.fromkeys(CHECK_FIELDS), failed=parts["failed"])}
        made["controlFaces"] = {}
        labels_written = [dict(row, east=common.fixed(float(point[0]), 3), north=common.fixed(float(point[1]), 3)) for row, point in zip(rows, site)]
        return made, {"labels": labels_written, "shifts": shifts}
    refine = parts["refine"]
    made["controlCheck"] = {"control": {
        "failed": None, "controlTexts": len(parts["texts"]), "marks": len(parts["marks"]), "markGapStartM": location_plan.mark_gaps(parts["start"], parts["marks"]),
        "markGapRefinedM": location_plan.mark_gaps(parts["refined"], parts["marks"]), "refineFaces": len(refine["faces"]), "refineKept": refine["kept"],
        "refineResidualKeptM": stat_values(refine["residual"][refine["keep"]]), "againstScheduleM": stat_values(parts["apart"]),
        "labelsOnAFaceSchedule": sum(1 for row in rows if row["face"] is not None),
        "labelsOnAFaceControl": sum(1 for face in parts["controlFaces"] if face is not None),
        "facesNumberedOtherwise": len(parts["listed"]), "unitFacesNumberedOtherwise": parts["unitFaces"]}}
    made["controlFaces"] = {item["face"]: {"schedule": item["schedule"], "control": item["control"], "why": item["why"]} for item in parts["listed"]}
    # R352. The rows of the location plan with their site points as labels.csv writes them, for the plot table
    labels_written = [dict(row, east=common.fixed(float(point[0]), 3), north=common.fixed(float(point[1]), 3)) for row, point in zip(rows, site)]
    return made, {"labels": labels_written, "shifts": shifts}


def workbook(args, settings, entry_name):
    """The workbook of a sheet setting, the file of the role addin.workbookRoles gives its workbook name, a key of dataBook.rev9.workbooks."""
    if entry_name not in ((settings.get("dataBook") or {}).get("rev9") or {}).get("workbooks", {}):
        raise project_profile.ProfileError("the workbook %s of a sheet setting is no key of dataBook.rev9.workbooks, every workbook setting names one (R350)" % entry_name)
    roles = (settings.get("addin") or {}).get("workbookRoles") or {}
    role = roles.get(entry_name)
    if role not in ("dlus", "sales"):
        raise project_profile.ProfileError("addin.workbookRoles names no role dlus or sales for the workbook %s (R350)" % entry_name)
    path = getattr(args, role)
    if not path:
        raise project_profile.ProfileError("the workbook %s is the input %s, give --%s (R350)" % (entry_name, role, role))
    return path


def sheet(args, settings, entry_value):
    return rev9_workbooks.sheet_rows(workbook(args, settings.values, entry_value["workbook"]), entry_value["sheet"], entry_value["headerRow"], entry_value["columns"])


def main(argv=None):
    parser = argparse.ArgumentParser(description="The Python side of the read steps of the parity harness (R348 to R350).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--steps", required=True, help="a folder outside any git work tree for step_<step>_python.json")
    parser.add_argument("--only", nargs="*", choices=STEPS + LOCATION_STEPS + TABLE_STEPS,
                        help="these steps only, every step when left out, those of the location plan when --plot-numbers is given and the plot table with --border")
    parser.add_argument("--baseplan", help="R388. plots.json of the baseplan read, the one cadReads of config names when left out")
    parser.add_argument("--roads", help="R388. the folder of the road layout read, the one cadReads names when left out")
    parser.add_argument("--footprint-blocks", help="R388. blocks.json of the footprint read, the one cadReads names when left out")
    parser.add_argument("--landxml", help="the folder of the LandXML files of siteBuild.roadSurfaces")
    parser.add_argument("--scratch", help="a folder outside the repo for the LandXML reader")
    parser.add_argument("--plots-csv", help="plots.csv of the data book")
    parser.add_argument("--labels", help="location_plan/labels.csv of the data book")
    parser.add_argument("--dlus", help="the DLUS workbook, the role of addin.workbookRoles")
    parser.add_argument("--sales", help="the sales workbook, the role of addin.workbookRoles")
    parser.add_argument("--plot-numbers", help="R351. the location plan PDF, the input plotNumbers")
    parser.add_argument("--border", help="R352. the Phase 1 border of the data book, phase1_border.csv, the input border")
    parser.add_argument("--schedule", action="append", default=[], metavar="NBH=PATH",
                        help="R351. the schedule workbook of a neighbourhood of connect.neighbourhoods, the input schedule:<NBH>, its file in the References folder of inputRoot when left out")
    args = parser.parse_args(argv)
    steps = args.only or (STEPS + (LOCATION_STEPS if args.plot_numbers else ()) + (TABLE_STEPS if args.plot_numbers and args.border else ()))
    try:
        for step in steps:
            parity.clear_step(args.steps, step, "python")
        # R352. A plot table with no border or no DLUS workbook is refused before any read, never after the location plan is read
        if "plotTable" in steps and (not args.border or not args.dlus):
            raise project_profile.ProfileError("the step plotTable needs --border and --dlus (R352)")
        settings = project_profile.load_settings(args.settings)
        values = settings.values
        if "plotTable" in steps:
            plot_table.overlap_decimals(values)
        rules = parity.tolerances(values)
        inputs = [parity.input_entry("config", args.settings)]
        made = {}
        plots = None
        if {"labels", "roadLevels"} & set(steps) or set(LOCATION_STEPS) & set(steps):
            args.baseplan = str(cad_units.read_input(settings, "baseplan", args.baseplan))
            inputs.append(parity.input_entry("baseplanPlots", args.baseplan))
            plots = link_summary.load(args.baseplan)["plots"]
        if "labels" in steps:
            made["labels"] = label_step(plots)
        if "blocks" in steps or "setbacks" in steps:
            args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
            inputs.append(parity.input_entry("footprintBlocks", args.footprint_blocks))
        if "blocks" in steps:
            made["blocks"] = block_step(footprint_check.footprint_blocks(args.footprint_blocks, settings))
        if "setbacks" in steps:
            pieces = Path(args.footprint_blocks).parent / "pieces.json"
            cad_units.check(pieces)
            inputs.append(parity.input_entry("footprintPieces", pieces))
            made["setbacks"] = edge_step(link_summary.load(pieces)["pieces"], [settings.get("dataBook.join.footprint.setbackLayer")])
        if "curbs" in steps or "rows" in steps:
            road = cad_units.read_input(settings, "roads", args.roads) / "pieces.json"
            inputs.append(parity.input_entry("roadPieces", road))
            pieces = link_summary.load(road)["pieces"]
            if "curbs" in steps:
                made["curbs"] = edge_step(pieces, settings.get("analysis.roadEdgeLayers"))
            if "rows" in steps:
                made["rows"] = edge_step(pieces, settings.get("dataBook.join.row.layers"))
        if {"surfaces", "roadLevels"} & set(steps):
            if not args.landxml or not args.scratch:
                raise project_profile.ProfileError("the steps surfaces and roadLevels need --landxml and --scratch")
            # R344. The snap of the road level held against snapMaxM before any surface is read
            snap = plan_rules.road_snap(settings.get("siteBuild"))
            profile = project_profile.load_profile(project_profile.profile_path(settings))
            names = plan_rules.road_names(settings.get("siteBuild"), plan_rules.level_profile(profile, plan_rules.profile_name(profile, None)))
            files = [Path(args.landxml) / (name + ".xml") for name in names]
            inputs.extend(parity.input_entry("roadSurface:" + path.stem, path) for path in files)
            surfaces = plan_from_book.load_roads(files, args.scratch)
            reports = [json.loads((Path(args.scratch) / path.stem / landxml_reader.SUMMARY).read_text(encoding="utf-8")) for path in files]
            if "surfaces" in steps:
                made["surfaces"] = surface_step(reports, [Path(args.scratch) / path.stem for path in files])
            if "roadLevels" in steps:
                if not args.plots_csv or not args.labels:
                    raise project_profile.ProfileError("the step roadLevels needs --plots-csv and --labels")
                inputs.extend([parity.input_entry("plotsCsv", args.plots_csv), parity.input_entry("labels", args.labels)])
                _, number_of = plan_from_book.book_rows(args.plots_csv)
                made["roadLevels"] = road_level_step(plan_from_book.nbh_plots(args.labels, None), face_rings(plots, number_of), surfaces, snap)
        if {"areaKey", "dlusSetbacks", "sales"} & set(steps):
            inputs.extend(parity.input_entry(role, getattr(args, role)) for role in ("dlus", "sales") if getattr(args, role))
            book = settings.get("dataBook")
            if "areaKey" in steps:
                made["areaKey"] = area_key_step(sheet(args, settings, book["data5"]["areaKey"]["dlus"]), sheet(args, settings, book["data5"]["areaKey"]["sales"]))
            if "dlusSetbacks" in steps:
                rows = sheet(args, settings, book["rev9"]["dlus"])
                held(len(rows), book["rev9"]["dlus"], "the DLUS setbacks")
                made["dlusSetbacks"] = dlus_setback_step(rows)
            if "sales" in steps:
                # R350. The sales rows come from the workbook their own setting names, a key of dataBook.rev9.workbooks as every workbook
                # setting, where they took the workbook of rev9.sales before (the review of PR 53)
                found = book["locationPlan"]["sales"]
                rows = corner_front.sales_rows(workbook(args, values, found["workbook"]), found["sheet"], found["columns"]["plot"])
                held(len(rows), found, "the sales rows")
                plain_rows = sheet(args, settings, book["rev9"]["sales"])
                held(len(plain_rows), book["rev9"]["sales"], "the sales values")
                made["sales"] = sales_step(rows, plain_rows)
        if (set(LOCATION_STEPS) | set(TABLE_STEPS)) & set(steps):
            if not args.plot_numbers or not args.sales:
                raise project_profile.ProfileError("the steps of the location plan need --plot-numbers and --sales (R351)")
            # R351. Every schedule of connect.neighbourhoods is an input of the stamp, by its file in the References folder of inputRoot
            # when the run names none
            paths = {name: str(Path(levels_proof.input_root(settings)) / full_run.XLSX_FOLDER / entry["schedule"])
                     for name, entry in settings.get("connect.neighbourhoods").items() if entry}
            paths.update(dict(item.split("=", 1) for item in args.schedule))
            inputs.append(parity.input_entry("plotNumbers", args.plot_numbers))
            inputs.extend(parity.input_entry("schedule:" + name, path) for name, path in sorted(paths.items()))
            if not any(item["role"] == "sales" for item in inputs):
                inputs.append(parity.input_entry("sales", args.sales))
            found, numbering = location_steps(settings, args.plot_numbers, plots, paths, workbook(args, values, settings.get("dataBook.locationPlan.sales.workbook")))
            made.update(found)
            if "plotTable" in steps:
                inputs.append(parity.input_entry("border", args.border))
                if not any(item["role"] == "dlus" for item in inputs):
                    inputs.append(parity.input_entry("dlus", args.dlus))
                args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
                if not any(item["role"] == "footprintBlocks" for item in inputs):
                    inputs.append(parity.input_entry("footprintBlocks", args.footprint_blocks))
                book = settings.get("dataBook")
                dlus, _ = area_key.by_plot(sheet(args, settings, book["data5"]["areaKey"]["dlus"]))
                sales_key, _ = area_key.by_plot(sheet(args, settings, book["data5"]["areaKey"]["sales"]))
                table = plot_table.table(plots, plot_table.border_points(args.border), data_book.schedule_rows(settings, settings.get("connect"), paths),
                                         numbering["shifts"], numbering["labels"], link_summary.load(args.footprint_blocks)["blocks"],
                                         settings.get("dataBook.wiring.minShare"), dlus, sales_key, plot_table.overlap_decimals(values))
                made["plotTable"] = {row["baseplanLabel"]: row for row in table}
        for step in steps:
            fields = (values["addin"]["parity"]["steps"]).get(step) or {}
            if step not in rules:
                raise project_profile.ProfileError("addin.parity.steps names no step %s (R345)" % step)
            plots_of_step = {key: entry(raw, fields) for key, raw in made[step].items()}
            parity.write_step(args.steps, step, "python", SCOPE, plots_of_step, values, inputs)
            print("step %s: %d items" % (step, len(plots_of_step)))
    except (project_profile.ProfileError, cad_units.UnitError, landxml_reader.UnitError, KeyError, OSError, ValueError) as error:
        print("read_steps: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
