"""README.md of the data book: one line per file and per column, what it means and where it comes from (R166)."""
import argparse
import csv
import json
import sys
from pathlib import Path

import c3d_run

FILES = {
    "phase1_border.csv": "The Phase 1 border (R163): its vertices in order with the area, perimeter, drawing, layer and handle on every row.",
    "plots.csv": "One row per baseplan plot whose face centroid lies inside Phase 1, every source joined (R166).",
    "analysis.md": "The analysis of the data book (R167), counts only. analysis.json holds the same as data.",
    "analysis.json": "The analysis of R167 as JSON.",
    "README.md": "This file.",
    "retest.md": "The round DATA-2 tests (R170 to R172), counts only. retest.json holds the same as data.",
    "retest.json": "The tests of R170 to R172 as JSON: El @ Entry from the road plus the ramp, the units per face, the unit gap and the width keys.",
    "retest_entry.csv": "One row per schedule plot, surface and slope variant: El @ Entry from the front corners plus slope times ramp depth, less the schedule value (R170).",
    "wiring.md": "The wiring methods tried per source with their scores against the answer key and the method chosen (R174 to R177), counts only.",
    "wiring.json": "The same as wiring.md as data, with the lists of plots left unwired or ambiguous.",
    "locationPlan.md": "The Zone 4 location plan read and tied to the plots (R179 to R183), counts only.",
    "locationPlan.json": "The same as locationPlan.md as data, with the lists of plots with no sales row, shift breaks and unit differences.",
    "labels.csv": "One row per label of the location plan: number, old code, new code, Rev 9 typology, NBH, zone, page point, site point and face (R180 to R182).",
    "lines.csv": "Every text line of the location plan with its kind and page point (R179).",
    "fit.json": "The affine map from location plan page points to site metres (R181).",
    "units.csv": "One row per baseplan face inside Phase 1 with the footprint unit blocks that cover it, one unit each (R171).",
    "layers.csv": "Every layer of the drawing with the entities inside Phase 1 by type. Colour, on, frozen, locked and linetype are filled by the Civil 3D read only.",
    "entities.csv": "Every entity inside Phase 1, blocks exploded with the parent kept. A line is kept where any part is inside and cut at the border, one row per part. A table over the size limit is split by layer into the folder entities.",
    "blocks.csv": "Every block insert whose insert point lies inside Phase 1, attributes as attr_ columns.",
    "texts.csv": "Every text, mtext and attribute whose insert point lies inside Phase 1.",
    "surfaces.csv": "The surfaces of the drawing with the points and triangles inside Phase 1. reader LandXML means the statistics come from the LandXML export.",
    "civil.csv": "Civil 3D objects inside Phase 1 in long form: kind, name, layer, parent, property, value. Written by the Civil 3D read only.",
    "data.json": "What wrote the tables of the folder (reader C3DProbe for the full Civil 3D read, CadReader for the stand-in) and the rows kept and dropped per table.",
    "sheets.csv": "The sheet list of the master plan workbook: rows, columns, header row, header and the plot or location columns.",
    "rev9_workbooks.md": "The two Rev 9 workbooks of round DATA-3 (R185 to R188): inventory, diff, setbacks, widths and the code table, counts only.",
    "rev9_workbooks.json": "The same as rev9_workbooks.md as data.",
    "setbacks.csv": "One row per DLUS plot joined to plots.csv: the four setbacks, the drawn front and the Rev 6 front (R186).",
    "widths.csv": "One row per unit plot with a sales row: Land Width and Land Length against the CAD width and depth, Rev 6 and the code width (R187).",
    "rev9_drawing.md": "The Rev 9 drawing and its PDF against the baseplan (R189 to R191), counts only.",
    "rev9_drawing.json": "The same as rev9_drawing.md as data.",
    "rev9_faces.csv": "One row per unit plot: the Rev 9 face match, number, code and DWG unit text against the baseplan and the location plan (R191).",
    "rev9_labels.csv": "One row per label of the Rev 9 PDF plot: number, code, page point, site point and the baseplan face that holds it (R190).",
    "rev9_face_outlines.json": "The outline of the Rev 9 face matched to each unit plot, site metres, for the second method (R191, R198).",
    "cross_sections.json": "Each curb of the Typical Cross Sections drawing: section, road type, up-stand, sidewalk width and cross fall (R192).",
    "entry_terms.json": "El @ Entry from the road at the curb, the up-stand, the sidewalk and the ramp, with each term left out (R193), counts only.",
    "entry_terms.csv": "One row per schedule plot and variant of R193 with the predicted less the schedule El @ Entry and its band.",
    "stray_units.json": "The footprint unit blocks on no baseplan plot and what the location plan and Rev 9 have there (R194), counts only.",
    "stray_units.csv": "One row per footprint unit block on no baseplan plot (R194).",
    "location_dwg.json": "The location plan DWG of 01_Output against the Rev.09 location plan PDF, with the dates of every export (R196).",
}

COLUMNS = {
    "handle": "The handle of the entity in its drawing, inside a block the handle in the block definition.",
    "type": "The entity type (DXF name for the Civil 3D read, the CadReader kind for the stand-in).",
    "layer": "The layer.",
    "path": "The handles of the block inserts the entity sits in, outermost first, empty in model space.",
    "parentBlock": "The name of the block the entity sits in.",
    "part": "The part number of a line cut at the border, 0 for a whole line.",
    "x1": "Start or position, easting in metres, UTM 37N.", "y1": "Start or position, northing.", "z1": "Start or position, elevation.",
    "x2": "End easting.", "y2": "End northing.", "z2": "End elevation.",
    "cx": "Centre easting (arc, circle, or the outline centre of a block).", "cy": "Centre northing.", "radius": "Radius of an arc or circle.",
    "length": "Length of the part inside Phase 1.", "area": "Area of a closed shape that is not cut, or of a hatch.",
    "closed": "yes when the line is closed.", "zMin": "Lowest elevation of the part.", "zMax": "Highest elevation of the part.",
    "cut": "yes when the line was cut at the Phase 1 border.", "reader": "C3DProbe (full Civil 3D read), CadReader (stand-in) or LandXML.",
    "name": "Block or surface name, the effective name of a dynamic or anonymous block.", "definition": "The block definition name as stored.",
    "x": "Insert point easting.", "y": "Insert point northing.", "z": "Insert point elevation.", "rotationDeg": "Rotation in degrees, counter clockwise from east.",
    "scaleX": "Scale X.", "scaleY": "Scale Y.", "scaleZ": "Scale Z.", "mirrored": "yes when the insert is mirrored.", "xref": "yes for an xref insert, not opened here.",
    "text": "The plain text.", "height": "Text height in metres.",
    "colour": "Layer colour.", "on": "yes when the layer is on.", "frozen": "yes when frozen.", "locked": "yes when locked.", "linetype": "Layer linetype.",
    "entitiesInside": "Entities of the layer inside Phase 1.", "countsByType": "Entities inside Phase 1 by type, TYPE=count.",
    "officialNumber": "Baseplan label plus the shift of R28 of the neighbourhood whose schedule holds the plot, empty where none does.",
    "neighbourhood": "IMC or IMD when its past schedule holds the plot, empty otherwise.", "baseplanLabel": "The plot label of the baseplan face.",
    "clientType": "The client plot type of the baseplan label.", "centroidX": "Face centroid easting.", "centroidY": "Face centroid northing.",
    "faceAreaM2": "Face area in square metres.", "cutsheet": "The cutsheet frame number the centroid lies in.",
    "frontLengthM": "Length of the front edge (R122).", "widthM": "Plot width, the front edge length.", "depthM": "Depth of the face along the inward normal of the front.",
    "frontBearingDeg": "Bearing of the front edge from left to right seen from the road, clockwise from grid north.", "facingDeg": "Bearing the plot faces, towards the road.",
    "unitRev6": "The Rev 6 unit code from the schedule.", "unitRev6Source": "Where unitRev6 comes from.", "scheduleFile": "The schedule workbook.", "scheduleRow": "The sheet row.",
    "footprintBlock": "The footprint block on the face, by its outline centre (Building Footprint).", "footprintRotationDeg": "Its rotation.", "footprintMirrored": "Its mirror flag.",
    "footprintShared": "yes when the block also covers another face by the share of its wiring, a pair or row block.", "footprintSource": "The reader the block came from.",
    "cluster": "The cluster of analysis.json (R64) with its code.", "clusterRow": "The row id of the cluster.",
    "r06Block": "Block names on the face in R06.", "r06_0000Block": "Block names on the face in R06_0000.", "masterBlocksSource": "The readers of the master plan blocks.",
    "r06Number": "Whole number texts on the face in R06.", "r06_0000Number": "Whole number texts on the face in R06_0000.",
    "v9PlotNumber": "Plot number texts of V9 on the face.", "v9Source": "The reader of the V9 texts.",
    "setbackFromFrontM": "Distance from the middle of the front to the setback line the face owns by its wiring method.", "rowDistanceM": "Distance from the middle of the front edge of its wiring method to the ROW line.",
    "curbDistanceM": "Distance from the middle of the front edge of its wiring method to the curb (RD-CURB-LINE).", "sidewalkWidthM": "Curb distance less ROW distance where the ROW lies nearer.",
    "roadLevelLeft": "Road level at the left front corner, snapped within 5 mm as in R157.", "roadLevelRight": "The same at the right corner.", "roadLevelSurface": "The surface that gave it.",
    "pastFfl": "FFL of the past job or schedule.", "pastEntry": "El @ Entry of the past job or schedule.", "pastSteps": "Steps.", "pastSource": "past job plan or schedule.",
    "inCutList": "yes for the plots of the R120 cut list.", "duplicateLabel": "yes when two faces carry the label.", "inManyLabelFace": "yes when the label also sits in a face with many labels.",
    "crossesBorder": "yes when the face is not wholly inside Phase 1.", "noFront": "yes when no edge qualifies as a front (R122).", "twoNeighbourhoods": "yes when two schedules claim the label.",
    "kind": "Civil object kind.", "parent": "The object it belongs to.", "property": "Property name.", "value": "Property value.",
    "description": "Surface description.", "style": "Surface style.", "points": "Points of the surface.", "triangles": "Triangles of the surface.",
    "pointsInside": "Points inside Phase 1.", "trianglesInside": "Triangles whose centroid lies inside Phase 1.", "zMinInside": "Lowest level inside.", "zMaxInside": "Highest level inside.",
    "boundaries": "Surface boundaries.", "definitions": "Definition parts with counts.", "operations": "Surface operations with counts.", "isReference": "yes for a data shortcut.",
    "inExistingLandxml": "yes when the surface is in the LandXML of the earlier rounds and was left out of the export, no when exportKnownSurfaces exported every surface (R169).", "exportedLandxml": "The LandXML file this round wrote for it.",
    "vertex": "Vertex order.", "perimeter": "Perimeter in metres.", "drawing": "Source drawing.",
    "sheet": "Sheet name.", "file": "CSV file of the sheet.", "rows": "Rows of the sheet.", "columns": "Columns of the sheet.", "headerRow": "Row of the header, 1 based.",
    "plot": "The official plot number.", "unit": "The Rev 6 unit code from the schedule.", "surface": "roadLevel for the snapped road level, else the surface of the level columns.",
    "variant": "slope0.5 and slope2 for one front slope on every plot, byTypology for the steep slope on the steepUnits only.", "slopePercent": "The front slope in percent.",
    "rampDepthM": "The Rev 6 ramp depth of the unit, ramp.front_setback of the profile.", "difference": "Predicted El @ Entry less the schedule value, in metres.",
    "band": "within5mm, within30mm, worse or noLevel.", "units": "Rev 6 units on the face.", "blocks": "The footprint unit blocks that cover the face.",
    "locationPlanNumber": "The plot number of the location plan label on the face.", "locationPlanCode": "Its old client code.",
    "locationPlanNewCode": "Its new code by the old to new table.", "typologyRev9": "Typology_Rev9 of the Rev 9 sales row of that number.",
    "size": "Font size of the text line in points.", "number": "Plot number of the location plan.", "code": "Old client code of the location plan.", "newCode": "New code by the table.", "rev9": "Typology_Rev9.",
    "nbh": "NBH of the sales sheet.", "zone": "Construction zone of the sales sheet.", "face": "Baseplan face label.", "offset": "Number less face label.",
    "group": "IMC or IMD when its schedule holds the number, else rest.", "pageX": "Page point x.", "pageY": "Page point y.", "east": "Site easting.", "north": "Site northing.",
    "header": "The header cells.", "plotOrLocationColumns": "Columns naming a plot number or a location.", "cutToPhase1": "Whether the rows were cut to Phase 1.",
}


def describe(column):
    if column.startswith("level_"):
        side = "left" if column.endswith("_left") else "right"
        return "Level of surface " + column[len("level_"):column.rfind("_")] + " at the " + side + " front corner, where the corner lies on it."
    if column.startswith("attr_"):
        return "Block attribute " + column[len("attr_"):] + "."
    if column.endswith("Method") and column != "Method":
        return "The wiring method that filled " + column[:-len("Method")] + ", chosen by score in wiring.md (R177)."
    return COLUMNS.get(column, "")


def rows_of(path):
    with open(path, encoding="utf-8") as handle:
        return sum(1 for _ in handle) - 1


def write(root):
    lines = ["# Data book, Phase 1", "", "Read only from working copies, only data inside the Phase 1 border. Metres, UTM 37N. Written by the readers of rounds DATA, DATA-2 and DATA-3 (R160 to R198).", "",
             "## Files", ""]
    seen_columns = set()
    for path in sorted(p for p in root.rglob("*") if p.is_file() and "work" not in p.relative_to(root).parts and "logs" not in p.relative_to(root).parts):
        relative = path.relative_to(root).as_posix()
        parts = path.relative_to(root).parts
        meaning = FILES.get(path.name) or ("Sheet of the master plan workbook, cell for cell, column letters as header." if path.parent.name == "master_plan_excel" else
                                           "PDF reader output of a coordinate PDF copy, read for the location check of the schedules (R174)." if parts[0] == "pdf" else
                                           "CadReader output of the R06 copy, read for its block outlines (R174)." if parts[:2] == ("wiring", "cad") else
                                           "CadReader output of a copy read whole, not cut to the border (R189, R192, R196)." if len(parts) > 2 and parts[1] == "cad" else
                                           "plots.csv or a report as it was before Part W or Part L rebuilt it, kept for the before and after counts." if "_before_" in path.name else
                                           "Part of a table split by layer." if path.parent.name in ("entities", "blocks", "texts") else
                                           "LandXML of a surface the full Civil 3D read exported, triangles inside Phase 1 (R162, R169)." if path.suffix == ".xml" else "")
        count = (" (%d rows)" % rows_of(path)) if path.suffix == ".csv" else ""
        lines.append("- " + relative + count + ": " + meaning)
        if path.suffix == ".csv" and path.parent.name not in ("entities", "master_plan_excel"):
            with open(path, encoding="utf-8") as handle:
                seen_columns.update(next(csv.reader(handle), []))
    lines += ["", "## Columns", ""]
    for column in sorted(seen_columns):
        lines.append("- " + column + ": " + describe(column))
    (root / "README.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Write README.md of the data book (R166)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    root = Path(c3d_run.load_config(args.config)["dataBook"]["root"])
    print("README lines", write(root))
    return 0


if __name__ == "__main__":
    sys.exit(main())
