"""The per drawing tables of the data book from CadReader, where the full Civil 3D read has not run (R164).

CadReader reads a working copy with an empty layer list into a temporary folder, which is removed once read. Every piece is
judged against the Phase 1 border as in R162: a line, polyline, arc, circle or spline is kept where any part is inside and cut
at the border, a block, text, hatch or point is kept when its point is inside. Only rows inside are written, the rows
outside are counted. Every table carries the column reader with CadReader and data.json says so, so a table of the full
read (reader C3DProbe) is never mistaken for these. Civil 3D objects are not seen by this reader.
"""
import argparse
import csv
import json
import math
import shutil
import subprocess
import sys
import tempfile
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import LineString, MultiLineString, Point, Polygon
from shapely.prepared import prep

import c3d_run
import data_book

REPO = Path(__file__).resolve().parent.parent
READER = "CadReader"
ARC_STEP_DEG = 5.0

ENTITY_COLUMNS = ["handle", "type", "layer", "path", "parentBlock", "part", "x1", "y1", "z1", "x2", "y2", "z2", "cx", "cy", "radius",
                  "length", "area", "closed", "zMin", "zMax", "cut", "reader"]
TEXT_COLUMNS = ["handle", "type", "layer", "path", "parentBlock", "text", "x", "y", "z", "height", "rotationDeg", "reader"]
BLOCK_COLUMNS = ["handle", "name", "definition", "layer", "path", "parentBlock", "x", "y", "z", "rotationDeg", "scaleX", "scaleY", "scaleZ", "mirrored", "xref", "cx", "cy", "reader"]
LAYER_COLUMNS = ["layer", "colour", "on", "frozen", "locked", "linetype", "entitiesInside", "countsByType", "reader"]


def arc_points(centre, radius, start, end, ccw=True):
    a0 = math.atan2(start[1] - centre[1], start[0] - centre[0])
    a1 = math.atan2(end[1] - centre[1], end[0] - centre[0])
    sweep = ((a1 - a0) if ccw else (a0 - a1)) % (2 * math.pi) or 2 * math.pi
    steps = max(2, int(math.ceil(math.degrees(sweep) / ARC_STEP_DEG)) + 1)
    z = centre[2] if len(centre) > 2 else 0.0
    sign = 1 if ccw else -1
    return [(centre[0] + radius * math.cos(a0 + sign * sweep * k / (steps - 1)), centre[1] + radius * math.sin(a0 + sign * sweep * k / (steps - 1)), z) for k in range(steps)]


def piece_line(piece):
    """The points of a piece as a line, the closed flag and the centre and radius of an arc or circle."""
    kind = piece.get("kind")
    if kind in ("line", "polyline", "spline"):
        points = [tuple(p) + (0.0,) * (3 - len(p)) for p in piece.get("pts") or []]
        closed = bool(piece.get("closed"))
        if closed and len(points) > 2:
            points.append(points[0])
        return points, closed, None
    if kind == "arc":
        return arc_points(piece["center"], piece["radius"], piece["start"], piece["end"], piece.get("ccw", True)), False, (piece["center"], piece["radius"])
    if kind == "circle":
        c, r = piece["center"], piece["radius"]
        start = (c[0] + r, c[1])
        return arc_points(c, r, start, start), True, (c, r)
    return None, False, None


def parts_of(points, border, prepared):
    """The parts of a line inside the border and whether it was cut. Z is read from the nearest vertex of the line."""
    line = LineString([p[:2] for p in points])
    if prepared.contains(line):
        return [points], False
    if not prepared.intersects(line):
        return [], False
    inside = line.intersection(border)
    pieces = list(inside.geoms) if hasattr(inside, "geoms") else [inside]
    found = []
    for piece in pieces:
        if piece.geom_type != "LineString" or piece.length == 0:
            continue
        coords = list(piece.coords)
        found.append([(x, y, nearest_z(points, x, y)) for x, y in coords])
    return found, bool(found)


def nearest_z(points, x, y):
    best = min(range(len(points)), key=lambda k: (points[k][0] - x) ** 2 + (points[k][1] - y) ** 2)
    return points[best][2]


def length_of(points):
    return sum(math.dist(a[:2], b[:2]) for a, b in zip(points, points[1:]))


def shoelace(points):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(points, points[1:]))) / 2


def tables(folder, border, out):
    """Writes layers, entities, blocks and texts of one CadReader output folder, inside the border only."""
    prepared = prep(border)
    counts = defaultdict(Counter)
    by_layer = defaultdict(Counter)
    out.mkdir(parents=True, exist_ok=True)
    pieces = json.loads((folder / "pieces.json").read_text(encoding="utf-8"))["pieces"]
    attributes = {p.get("handle"): p.get("attributes") or {} for p in pieces if p.get("kind") == "insert"}
    with open(out / "entities.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(ENTITY_COLUMNS)
        for piece in pieces:
            kind = piece.get("kind")
            if kind == "insert":
                continue
            layer = piece.get("layer") or ""
            base = [piece.get("handle"), kind.upper(), layer, piece.get("path"), None]
            points, closed, circle = piece_line(piece)
            if points and len(points) > 1:
                parts, cut = parts_of(points, border, prepared)
                if not parts:
                    counts["entities"]["outside"] += 1
                    continue
                area = shoelace(points) if closed and not cut else None
                for index, part in enumerate(parts):
                    writer.writerow([data_book.cell(v) for v in base + [index, *part[0], *part[-1], circle[0][0] if circle else None, circle[0][1] if circle else None,
                                                                         circle[1] if circle else None, length_of(part), area, closed, min(p[2] for p in part), max(p[2] for p in part), cut, READER]])
                    counts["entities"]["keptCut" if cut else "kept"] += 1
                by_layer[layer][kind.upper()] += 1
                continue
            at = point_of(piece)
            if at is None:
                counts["entities"]["noPosition"] += 1
                continue
            if not prepared.contains(Point(at[:2])):
                counts["entities"]["outside"] += 1
                continue
            area = hatch_area(piece) if kind == "hatch" else None
            z = at[2] if len(at) > 2 else 0.0
            writer.writerow([data_book.cell(v) for v in base + [0, at[0], at[1], z, None, None, None, None, None, None, None, area, None, z, z, False, READER]])
            counts["entities"]["kept"] += 1
            by_layer[layer][kind.upper()] += 1
    blocks = json.loads((folder / "blocks.json").read_text(encoding="utf-8"))["blocks"]
    tags = sorted({tag for b in blocks for tag in attributes.get(b.get("handle"), {})})
    with open(out / "blocks.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(BLOCK_COLUMNS + ["attr_" + tag for tag in tags])
        for block in blocks:
            x, y = block["insertPoint"][0], block["insertPoint"][1]
            if not prepared.contains(Point(x, y)):
                counts["blocks"]["outside"] += 1
                continue
            scale = (block.get("scale") or [None, None, None]) + [None] * 3
            outline = block.get("outline") or []
            centre = Polygon([p[:2] for p in outline]).centroid if len(outline) >= 3 else None
            found = attributes.get(block.get("handle"), {})
            writer.writerow([data_book.cell(v) for v in [block.get("handle"), block["name"].split("$")[-1], block["name"], block.get("layer"), block.get("path"), None, x, y,
                                                         block["insertPoint"][2] if len(block["insertPoint"]) > 2 else 0.0, block.get("rotationDeg"), scale[0], scale[1], scale[2],
                                                         block.get("mirrored"), False, centre.x if centre and not centre.is_empty else None,
                                                         centre.y if centre and not centre.is_empty else None, READER] + [found.get(tag) for tag in tags]])
            counts["blocks"]["kept"] += 1
            by_layer[block.get("layer") or ""]["INSERT"] += 1
    texts = json.loads((folder / "texts.json").read_text(encoding="utf-8"))["texts"]
    with open(out / "texts.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(TEXT_COLUMNS)
        for text in texts:
            if not prepared.contains(Point(text["x"], text["y"])):
                counts["texts"]["outside"] += 1
                continue
            writer.writerow([data_book.cell(v) for v in [text.get("handle"), (text.get("kind") or "text").upper(), text.get("layer"), text.get("path"), None,
                                                         (text.get("text") or "").replace("\n", " "), text["x"], text["y"], text.get("z", 0.0), text.get("height"), None, READER]])
            counts["texts"]["kept"] += 1
    layers = json.loads((folder / "layers.json").read_text(encoding="utf-8"))
    with open(out / "layers.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(LAYER_COLUMNS)
        names = [entry["name"] for entry in layers["layers"]] + sorted(set(by_layer) - {entry["name"] for entry in layers["layers"]})
        for name in names:
            found = by_layer.get(name, Counter())
            writer.writerow([data_book.cell(v) for v in [name, None, None, None, None, None, sum(found.values()), " ".join("%s=%d" % kv for kv in sorted(found.items())), READER]])
    return {table: dict(values) for table, values in counts.items()}, layers.get("units"), [x.get("name") for x in layers.get("xrefs", [])]


def point_of(piece):
    if piece.get("kind") == "hatch":
        loops = piece.get("loops") or []
        if not loops or len(loops[0]) < 3:
            return None
        centre = Polygon([p[:2] for p in loops[0]]).centroid
        return (centre.x, centre.y, 0.0)
    for key in ("center", "insertPoint"):
        if piece.get(key):
            return tuple(piece[key])
    points = piece.get("pts") or []
    return tuple(points[0]) if points else None


def hatch_area(piece):
    loops = piece.get("loops") or []
    return sum(Polygon([p[:2] for p in loop]).area for loop in loops if len(loop) >= 3) if loops else None


def run_drawing(cad_reader, drawing, border, out):
    folder = Path(tempfile.mkdtemp(prefix="book_cad_"))
    try:
        empty = folder / "layers.json"
        empty.write_text(json.dumps({"keep": [], "drop": []}), encoding="utf-8")
        result = folder / "out"
        done = subprocess.run([str(cad_reader), str(drawing), str(result), "--layers", str(empty)], capture_output=True, text=True, timeout=1800)
        if done.returncode != 0:
            return {"error": done.stderr.strip() or "exit %d" % done.returncode}
        counts, units, xrefs = tables(result, border, out)
        return {"counts": counts, "units": units, "xrefs": xrefs}
    finally:
        shutil.rmtree(folder, ignore_errors=True)


def landxml_surfaces(connect, border, out, known):
    """surfaces.csv of the road surface drawing from its LandXML export, the points and triangles inside Phase 1 only."""
    import numpy as np
    from shapely import contains_xy
    import landxml_reader
    folder = Path(connect["landxmlDir"])
    columns = ["name", "description", "type", "style", "layer", "points", "triangles", "pointsInside", "trianglesInside", "zMinInside", "zMaxInside",
               "boundaries", "definitions", "operations", "isReference", "inExistingLandxml", "exportedLandxml", "reader"]
    summary = json.loads((folder / "landxml_summary.json").read_text(encoding="utf-8"))
    with open(out / "surfaces.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(columns)
        for entry in summary["surfaces"]:
            if entry["name"] not in known:
                continue
            sub = folder / landxml_reader.folder_name(entry["name"])
            xyz, faces = np.load(sub / "points.npy"), np.load(sub / "faces.npy")
            inside_points = contains_xy(border, xyz[:, 0], xyz[:, 1])
            centres = xyz[faces].mean(axis=1)
            inside_faces = contains_xy(border, centres[:, 0], centres[:, 1])
            used = np.unique(faces[inside_faces].ravel())
            z = xyz[used, 2] if len(used) else np.array([])
            writer.writerow([data_book.cell(v) for v in [entry["name"], None, "TinSurface", None, None, entry["pointCount"], entry["faceCount"], int(inside_points.sum()),
                                                         int(inside_faces.sum()), float(z.min()) if len(z) else None, float(z.max()) if len(z) else None,
                                                         None, None, None, None, True, None, "LandXML"]])


def main(argv=None):
    parser = argparse.ArgumentParser(description="Per drawing tables of the data book from CadReader where the full read has not run (R164)")
    parser.add_argument("roles", nargs="*")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    book = c3d_run.load_config(args.config)["dataBook"]
    root = Path(book["root"])
    border = data_book.border_polygon(root / "phase1_border.csv")
    for role in args.roles or list(book["drawings"]):
        drawing = root / "work" / book["drawings"][role]
        out = root / drawing.stem
        summary_path = out / "data.json"
        if summary_path.is_file() and json.loads(summary_path.read_text(encoding="utf-8")).get("reader") != READER:
            print(role, "has a full Civil 3D read, left as it is")
            continue
        summary = {"drawing": drawing.name, "reader": READER, **run_drawing(REPO / book["cadReader"], drawing, border, out)}
        summary["done"] = "error" not in summary
        known = book.get("knownSurfaces", {}).get(role)
        if known and summary["done"]:
            landxml_surfaces(c3d_run.load_config(args.config)["connect"], border, out, set(known))
        out.mkdir(parents=True, exist_ok=True)
        summary_path.write_text(json.dumps(summary, indent=1), encoding="utf-8")
        print(role, json.dumps(summary.get("counts") or summary.get("error")))
    return 0


if __name__ == "__main__":
    sys.exit(main())
