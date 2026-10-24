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
    "layers.csv": "Every layer of the drawing with the entities inside Phase 1 by type. Colour, on, frozen, locked and linetype are filled by the Civil 3D read only.",
    "entities.csv": "Every entity inside Phase 1, blocks exploded with the parent kept. A line is kept where any part is inside and cut at the border, one row per part. A table over the size limit is split by layer into the folder entities.",
    "blocks.csv": "Every block insert whose insert point lies inside Phase 1, attributes as attr_ columns.",
    "texts.csv": "Every text, mtext and attribute whose insert point lies inside Phase 1.",
    "surfaces.csv": "The surfaces of the drawing with the points and triangles inside Phase 1. reader LandXML means the statistics come from the LandXML export.",
    "civil.csv": "Civil 3D objects inside Phase 1 in long form: kind, name, layer, parent, property, value. Written by the Civil 3D read only.",
    "data.json": "What wrote the tables of the folder (reader C3DProbe for the full Civil 3D read, CadReader for the stand-in) and the rows kept and dropped per table.",
    "sheets.csv": "The sheet list of the master plan workbook: rows, columns, header row, header and the plot or location columns.",
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
    "rev6Unit": "The Rev 6 unit code from the schedule.", "rev6UnitSource": "Where rev6Unit comes from.", "scheduleFile": "The schedule workbook.", "scheduleRow": "The sheet row.",
    "footprintBlock": "The footprint block on the face, by its outline centre (Building Footprint).", "footprintRotationDeg": "Its rotation.", "footprintMirrored": "Its mirror flag.",
    "footprintShared": "yes when the block centre lies on a shared wall within 0.5 m of the face, a pair or row block.", "footprintSource": "The reader the block came from.",
    "cluster": "The cluster of analysis.json (R64) with its code.", "clusterRow": "The row id of the cluster.",
    "r06Block": "Block names on the face in R06.", "r06_0000Block": "Block names on the face in R06_0000.", "masterBlocksSource": "The readers of the master plan blocks.",
    "r06Number": "Whole number texts on the face in R06.", "r06_0000Number": "Whole number texts on the face in R06_0000.",
    "v9PlotNumber": "Plot number texts of V9 on the face.", "v9Source": "The reader of the V9 texts.",
    "setbackFromFrontM": "Distance from the middle of the front to the nearest setback line on the face.", "rowDistanceM": "Distance from the middle of the front to the ROW line.",
    "curbDistanceM": "Distance from the middle of the front to the curb (RD-CURB-LINE).", "sidewalkWidthM": "Curb distance less ROW distance where the ROW lies nearer.",
    "roadLevelLeft": "Road level at the left front corner, snapped within 5 mm as in R157.", "roadLevelRight": "The same at the right corner.", "roadLevelSurface": "The surface that gave it.",
    "pastFfl": "FFL of the past job or schedule.", "pastEntry": "El @ Entry of the past job or schedule.", "pastSteps": "Steps.", "pastSource": "past job plan or schedule.",
    "inCutList": "yes for the plots of the R120 cut list.", "duplicateLabel": "yes when two faces carry the label.", "inManyLabelFace": "yes when the label also sits in a face with many labels.",
    "crossesBorder": "yes when the face is not wholly inside Phase 1.", "noFront": "yes when no edge qualifies as a front (R122).", "twoNeighbourhoods": "yes when two schedules claim the label.",
    "kind": "Civil object kind.", "parent": "The object it belongs to.", "property": "Property name.", "value": "Property value.",
    "description": "Surface description.", "style": "Surface style.", "points": "Points of the surface.", "triangles": "Triangles of the surface.",
    "pointsInside": "Points inside Phase 1.", "trianglesInside": "Triangles whose centroid lies inside Phase 1.", "zMinInside": "Lowest level inside.", "zMaxInside": "Highest level inside.",
    "boundaries": "Surface boundaries.", "definitions": "Definition parts with counts.", "operations": "Surface operations with counts.", "isReference": "yes for a data shortcut.",
    "inExistingLandxml": "yes when the surface is in the LandXML of the earlier rounds.", "exportedLandxml": "The LandXML file this round wrote for it.",
    "vertex": "Vertex order.", "perimeter": "Perimeter in metres.", "drawing": "Source drawing.",
    "sheet": "Sheet name.", "file": "CSV file of the sheet.", "rows": "Rows of the sheet.", "columns": "Columns of the sheet.", "headerRow": "Row of the header, 1 based.",
    "header": "The header cells.", "plotOrLocationColumns": "Columns naming a plot number or a location.", "cutToPhase1": "Whether the rows were cut to Phase 1.",
}


def describe(column):
    if column.startswith("level_"):
        side = "left" if column.endswith("_left") else "right"
        return "Level of surface " + column[len("level_"):column.rfind("_")] + " at the " + side + " front corner, where the corner lies on it."
    if column.startswith("attr_"):
        return "Block attribute " + column[len("attr_"):] + "."
    return COLUMNS.get(column, "")


def rows_of(path):
    with open(path, encoding="utf-8") as handle:
        return sum(1 for _ in handle) - 1


def write(root):
    lines = ["# Data book, Phase 1", "", "Read only from working copies, only data inside the Phase 1 border. Metres, UTM 37N. Written by the readers of round DATA (R160 to R168).", "",
             "## Files", ""]
    seen_columns = set()
    for path in sorted(p for p in root.rglob("*") if p.is_file() and "work" not in p.relative_to(root).parts and "logs" not in p.relative_to(root).parts):
        relative = path.relative_to(root).as_posix()
        meaning = FILES.get(path.name) or ("Sheet of the master plan workbook, cell for cell, column letters as header." if path.parent.name == "master_plan_excel" else
                                           "Part of a table split by layer." if path.parent.name in ("entities", "blocks", "texts") else
                                           "LandXML of a surface not in the earlier LandXML, triangles inside Phase 1." if path.suffix == ".xml" else "")
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
