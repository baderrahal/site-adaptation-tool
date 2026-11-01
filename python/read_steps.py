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

With --picks, picks.json of button 2 (R473) as ReadPicks of the C# takes it: a villa block pick limits the blocks of the footprint read to
its insert layer, a villa type pick of the sales sheet sets the typology column of the location plan to its header, and each villa type
conflict pick sets typologyRev9 of its plot in the plot table, each pick held to the file its input names, else the default with a line
why. The plot outline and plot number picks are taken by the CAD read that gives plots.json, so with such a pick --baseplan names the
read_baseplan.json of the worker read.

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
import openpyxl

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
import input_guard
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
PICKS_FORMAT = "site-adaptation-picks"
PICK_KINDS = ("plotOutline", "plotNumber", "villaBlock", "villaType")
PICK_LABELS = {"plotOutline": "plot outline", "plotNumber": "plot number", "villaBlock": "villa block", "villaType": "villa type"}
LAYER_JOIN = " + "
SHIFT_MARK = "|shift "


def load_picks(path):
    """R473. picks.json of button 2, refused when it is no file of the format of ReadPicks."""
    picks = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(picks, dict) or picks.get("format") != PICKS_FORMAT:
        raise ValueError("the input picks is no picks.json of the format %s (R473)" % PICKS_FORMAT)
    return picks


def sheet_of(rest):
    """R473. The sheet and the header of a villa type source "<sheet>|<header>|shift <n>" after its file, taken from the end since a sheet
    may hold a bar, None for a page of a PDF or any other shape."""
    shift = rest.rfind(SHIFT_MARK)
    if shift < 0:
        return None
    place = rest[:shift]
    column = place.rfind("|")
    if place.startswith("page ") or column < 0:
        return None
    return place[:column], place[column + 1:]


def other_file(what, file, given, name):
    if given is not None and file == given:
        return None
    return "the %s is of the file %s, the %s of the read is %s, the read keeps the default of config (R473)" % (what, file, name, given or "not given")


def pick_refusal(kind, source, files):
    """R473. ReadPicks.Refusal: why the read can not take this source of the kind with these files, None when it can."""
    what = PICK_LABELS.get(kind, kind) + " pick " + source
    if "|" not in source:
        return "the %s names no file, the read keeps the default of config (R473)" % what
    file, rest = source.split("|", 1)
    if kind in ("plotOutline", "plotNumber"):
        return other_file(what, file, files.get("baseplan"), "baseplan")
    if kind == "villaBlock":
        # Breaker 1 of FIX-1NOV-A2: one villa type per insert layer, a source of one layer would drop the other types (Q248, Q346)
        return other_file(what, file, files.get("villaBlocks"), "villa blocks") or (
            "the %s names one insert layer, the villa blocks of the drawing lie on one layer per villa type, so the read keeps every insert (R473)" % what)
    if kind != "villaType":
        return "the pick %s is of no kind the read takes (R473)" % source
    if rest.startswith("block|") or rest.startswith("text|"):
        return "the %s is of blocks or texts of a drawing, the read takes the villa type from the sales sheet only and keeps the column of config (R473)" % what
    sheet = sheet_of(rest)
    if sheet is None:
        return "the %s is of a PDF page, the read takes the villa type from the sales sheet only and keeps the column of config (R473)" % what
    other = other_file(what, file, files.get("sales"), "sales workbook")
    if other:
        return other
    if sheet[0] != files.get("salesSheet"):
        return "the %s is of the sheet %s, the read takes the sales rows of the sheet %s of config and keeps its column (R473)" % (what, sheet[0], files.get("salesSheet"))
    return None


def pick_of(picks, kind, files):
    """R473. ReadPicks.Take of one kind: the part of its source the read takes, None for the default of config, and the line why a pick or
    proposal is not taken, None when it is."""
    found = ((picks or {}).get("kinds") or {}).get(kind) or {}
    source, origin = found.get("source"), found.get("from")
    if not source or origin not in ("pick", "proposal"):
        return None, None
    refused = pick_refusal(kind, source, files)
    if refused:
        return None, refused
    rest = source.split("|", 1)[1]
    if kind == "plotOutline":
        return rest.split(LAYER_JOIN), None
    if kind == "plotNumber":
        return [rest], None
    if kind == "villaBlock":
        return rest, None
    return sheet_of(rest)[1], None


def blocks_on_layer(blocks, layer):
    """R473. The blocks of the footprint read on the insert layer of a villa block pick, every block when there is none."""
    return blocks if layer is None else [block for block in blocks if block.get("layer") == layer]


def sales_header(path, sheet, header_row):
    """R473. The cells of the header row of the sales sheet as openpyxl reads them."""
    workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    try:
        rows = list(workbook[sheet].iter_rows(min_row=header_row, max_row=header_row, values_only=True))
    finally:
        workbook.close()
    return list(rows[0]) if rows else []


def typology_columns(columns, header_cells, header_row, header, lines):
    """R473. ReadPicks.TypologyColumns: the columns of the sales rows with the typology column set to the header of the pick when the
    header row holds it as location_plan.read_table reads a header, else the columns of config and a line why."""
    if header is None:
        return dict(columns)
    if not any(cell is not None and str(cell).strip() == header for cell in header_cells):
        lines.append("the villa type pick names the column %s, which the header row %d of the sales sheet does not hold, the read keeps the column of config (R473)"
                     % (header, header_row))
        return dict(columns)
    return dict(columns, typology=header)


def apply_conflicts(table, conflicts, lines):
    """R473. ReadPicks.ApplyConflicts: each villa type conflict pick sets typologyRev9 of its plot in the plot table by baseplan label, a plot
    of no row or a second pick of one plot with another value sets nothing and says why. Gives the count set."""
    done = {}
    for conflict in conflicts or []:
        if conflict.get("field") != "villa type":
            continue
        plot, value = conflict.get("plot"), conflict.get("value")
        if plot not in table:
            lines.append("the conflict pick of plot %s names no plot of the plot table, nothing is set (R473)" % plot)
            continue
        if plot in done:
            if done[plot] != value:
                lines.append("two conflict picks give plot %s the villa types %s and %s, the first is kept (R473)" % (plot, done[plot], value))
            continue
        table[plot]["typologyRev9"] = value
        done[plot] = value
    return len(done)


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


def location_steps(settings, pdf, plots, schedule_paths, sales, typology=None, lines=None):
    """R351. The steps of the location plan as location_plan.py numbers it: the page read by pdfplumber, the labels paired, the page fitted
    to the site on the schedule of fitNeighbourhood (R181), every label on its face and its codes joined (R182), and the control point fit
    of R387 as its check, numbering nothing. The faces of the baseplan are refused as the build refuses them (R345). typology is the header
    of a villa type pick of the sales sheet (R473), lines takes the line why it is not taken."""
    plan = settings.get("dataBook.locationPlan")
    connect = settings.get("connect")
    refused = ring_rules.refusals(plots, parity.area_tolerance(settings.values))
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
    columns = plan["sales"]["columns"]
    if typology is not None:
        columns = typology_columns(columns, sales_header(sales, plan["sales"]["sheet"], plan["sales"]["headerRow"]), plan["sales"]["headerRow"], typology,
                                   lines if lines is not None else [])
    location_plan.join_codes(rows, location_plan.read_table(sales, plan["sales"]["sheet"], plan["sales"]["headerRow"], columns),
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
    parser.add_argument("--picks", help="R473. picks.json of button 2, the input picks, the read takes config alone when left out")
    args = parser.parse_args(argv)
    # D19. Every file the read steps open under an input root is held from its first open to the end, recorded in the steps folder
    with input_guard.Guard(input_guard.roots(args.settings), record=Path(args.steps) / "inputs_guard.json"):
        return run_steps(args)


def run_steps(args):
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
        picks = load_picks(args.picks) if args.picks else None
        lines = list((picks or {}).get("lines") or [])
        if picks:
            inputs.append(parity.input_entry("picks", args.picks))
        made = {}
        plots = None
        if {"labels", "roadLevels"} & set(steps) or set(LOCATION_STEPS) & set(steps):
            args.baseplan = str(cad_units.read_input(settings, "baseplan", args.baseplan))
            inputs.append(parity.input_entry("baseplanPlots", args.baseplan))
            baseplan_read = link_summary.load(args.baseplan)
            plots = baseplan_read["plots"]
            for kind in ("plotOutline", "plotNumber"):
                taken, line = pick_of(picks, kind, {"baseplan": baseplan_read.get("source")})
                lines.extend([line] if line else [])
                if taken:
                    print("the %s pick %s is taken by the read that gave %s (R473)" % (PICK_LABELS[kind], LAYER_JOIN.join(taken), Path(args.baseplan).name))
        if "labels" in steps:
            made["labels"] = label_step(plots)
        if "blocks" in steps or "setbacks" in steps:
            args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
            inputs.append(parity.input_entry("footprintBlocks", args.footprint_blocks))
        block_layer = None
        if picks and ("blocks" in steps or "plotTable" in steps):
            args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
            block_layer, line = pick_of(picks, "villaBlock", {"villaBlocks": link_summary.load(args.footprint_blocks).get("source")})
            lines.extend([line] if line else [])
        if "blocks" in steps:
            made["blocks"] = block_step(blocks_on_layer(footprint_check.footprint_blocks(args.footprint_blocks, settings), block_layer))
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
            sales_path = workbook(args, values, settings.get("dataBook.locationPlan.sales.workbook"))
            typology, line = pick_of(picks, "villaType", {"sales": Path(sales_path).name, "salesSheet": settings.get("dataBook.locationPlan.sales.sheet")})
            lines.extend([line] if line else [])
            found, numbering = location_steps(settings, args.plot_numbers, plots, paths, sales_path, typology, lines)
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
                                         numbering["shifts"], numbering["labels"], blocks_on_layer(link_summary.load(args.footprint_blocks)["blocks"], block_layer),
                                         settings.get("dataBook.wiring.minShare"), dlus, sales_key, plot_table.overlap_decimals(values))
                made["plotTable"] = {row["baseplanLabel"]: row for row in table}
                apply_conflicts(made["plotTable"], (picks or {}).get("conflicts"), lines)
        for line in lines:
            print(line)
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
