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
  of the order that covers it snapped within siteBuild.snapM, with the surface that gave it
- areaKey, dlusSetbacks and sales (R350): per plot the zone and neighbourhood of the DLUS and sales sheets of dataBook.data5.areaKey
  (area_key.by_plot), the setback sheet of dataBook.rev9.dlus as rev9_workbooks.sheet_rows gives it, and the sales row of
  dataBook.locationPlan.sales as corner_front.sales_rows gives it with the plain values of dataBook.rev9.sales

Every CadReader output is read through the units check of R388, a LandXML in meter only. A value of no geometry is written as read, a
value of geometry as common.round_tree writes it. Step files go outside any git work tree, real values stay out of git.
"""
import argparse
import datetime
import json
import sys
from collections import Counter
from pathlib import Path

import numpy

import area_key
import cad_analysis
import cad_units
import common
import corner_front
import footprint_check
import landxml_reader
import link_summary
import parity
import plan_from_book
import plan_from_past
import plan_rules
import project_profile
import rev9_workbooks
import yard_rule

STEPS = ("labels", "blocks", "curbs", "rows", "setbacks", "surfaces", "roadLevels", "areaKey", "dlusSetbacks", "sales")
SCOPE = "project"
NO_GEOMETRY = "noGeometry"


def jsonable(value):
    """A cell as a step file holds it: a date or time as its ISO text, as the C# reader writes it, every other value as read."""
    if isinstance(value, (datetime.datetime, datetime.date, datetime.time)):
        return value.isoformat()
    if isinstance(value, dict):
        return {("null" if key is None else key): jsonable(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [jsonable(item) for item in value]
    return value


def entry(raw, fields):
    """A plot of a step file: a field of no geometry written as read, a field of geometry as common.round_tree writes it."""
    raw = json.loads(json.dumps(jsonable(raw)))
    written = {name: (value if (fields.get(name) or {}).get("kind") == NO_GEOMETRY else common.round_tree(value)) for name, value in raw.items()}
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


def area_key_step(dlus_rows, sales_rows):
    """R350. Per plot of the DLUS or the sales sheet, the zone and the neighbourhood of each by area_key.by_plot and two rows that differ."""
    dlus, dlus_two = area_key.by_plot(dlus_rows)
    sales, sales_two = area_key.by_plot(sales_rows)
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


def workbook(args, settings, entry_name):
    """The workbook of a sheet setting, the file of the role addin.workbookRoles gives its workbook name."""
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
    parser.add_argument("--only", nargs="*", choices=STEPS, help="these steps only, every step when left out")
    parser.add_argument("--baseplan", help="R388. plots.json of the baseplan read, the one cadReads of config names when left out")
    parser.add_argument("--roads", help="R388. the folder of the road layout read, the one cadReads names when left out")
    parser.add_argument("--footprint-blocks", help="R388. blocks.json of the footprint read, the one cadReads names when left out")
    parser.add_argument("--landxml", help="the folder of the LandXML files of siteBuild.roadSurfaces")
    parser.add_argument("--scratch", help="a folder outside the repo for the LandXML reader")
    parser.add_argument("--plots-csv", help="plots.csv of the data book")
    parser.add_argument("--labels", help="location_plan/labels.csv of the data book")
    parser.add_argument("--dlus", help="the DLUS workbook, the role of addin.workbookRoles")
    parser.add_argument("--sales", help="the sales workbook, the role of addin.workbookRoles")
    args = parser.parse_args(argv)
    steps = args.only or STEPS
    try:
        for step in steps:
            parity.clear_step(args.steps, step, "python")
        settings = project_profile.load_settings(args.settings)
        values = settings.values
        rules = parity.tolerances(values)
        inputs = [parity.input_entry("config", args.settings)]
        made = {}
        plots = None
        if {"labels", "roadLevels"} & set(steps):
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
                made["roadLevels"] = road_level_step(plan_from_book.nbh_plots(args.labels, None), face_rings(plots, number_of), surfaces, settings.get("siteBuild.snapM"))
        if {"areaKey", "dlusSetbacks", "sales"} & set(steps):
            inputs.extend(parity.input_entry(role, getattr(args, role)) for role in ("dlus", "sales") if getattr(args, role))
            book = settings.get("dataBook")
            if "areaKey" in steps:
                made["areaKey"] = area_key_step(sheet(args, settings, book["data5"]["areaKey"]["dlus"]), sheet(args, settings, book["data5"]["areaKey"]["sales"]))
            if "dlusSetbacks" in steps:
                made["dlusSetbacks"] = dlus_setback_step(sheet(args, settings, book["rev9"]["dlus"]))
            if "sales" in steps:
                found = book["locationPlan"]["sales"]
                rows = corner_front.sales_rows(workbook(args, values, book["rev9"]["sales"]["workbook"]), found["sheet"], found["columns"]["plot"])
                made["sales"] = sales_step(rows, sheet(args, settings, book["rev9"]["sales"]))
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
