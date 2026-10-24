"""The Phase 1 border of the data book (R163).

CadReader reads the working copy of the boundary drawing into a temporary folder, which is deleted once read, since only
data inside Phase 1 is kept. The border is the closed polyline on dataBook.border.layer that holds the text
dataBook.border.label. It must hold the face centroid of every plot of the past schedules. When the layer and label do not
name one polyline, the one polyline that holds every schedule plot is taken. When none or two do, the border is refused with
each polyline's area and the schedule plots it holds.
"""
import argparse
import json
import math
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from shapely.geometry import Point, Polygon

import c3d_run
import full_run
import levels_proof
import link_summary
import project_profile
import yard_rule

REPO = Path(__file__).resolve().parent.parent


class BorderError(ValueError):
    pass


def rings(pieces):
    """Every polyline with three vertices or more as a ring, closed where its ends meet or it is flagged closed."""
    found = []
    for piece in pieces:
        points = [tuple(p[:2]) for p in piece.get("pts") or []]
        if piece.get("kind") != "polyline" or len(points) < 3:
            continue
        if points[0] == points[-1]:
            points = points[:-1]
        if len(points) < 3:
            continue
        found.append({"layer": piece.get("layer"), "handle": piece.get("handle"), "points": points, "closedFlag": bool(piece.get("closed")),
                      "endGap": math.dist(piece["pts"][0][:2], piece["pts"][-1][:2])})
    return found


def pick(candidates, texts, layer, label, plot_points):
    """The border ring and how it was chosen, or BorderError with the table of every ring."""
    table = []
    for ring in candidates:
        polygon = Polygon(ring["points"])
        labels = [t.get("text") for t in texts if polygon.contains(Point(t["x"], t["y"]))]
        holds = sum(1 for point in plot_points if polygon.contains(Point(point)))
        table.append({**ring, "area": polygon.area, "labels": labels, "holds": holds, "valid": polygon.is_valid})
    named = [row for row in table if row["layer"] == layer and label in row["labels"]]
    if len(named) == 1 and named[0]["holds"] == len(plot_points):
        return named[0], "layer and label", table
    holding = [row for row in table if row["holds"] == len(plot_points)]
    if len(holding) == 1:
        return holding[0], "the one ring that holds every schedule plot", table
    lines = ["%s %s area %.0f holds %d of %d" % (row["layer"], row["handle"], row["area"], row["holds"], len(plot_points)) for row in table]
    raise BorderError("no single Phase 1 border: " + ", ".join(lines))


def schedule_points(plots, books):
    """The face centroid of every plot of the past schedules, by the shift of its neighbourhood."""
    points, missing = [], []
    for path, shift in books:
        faces, _ = yard_rule.face_shapes(plots, shift)
        for row in levels_proof.read_schedule(path):
            number = str(levels_proof.number_of(row["plot"]))
            if number in faces:
                centre = faces[number].centroid
                points.append((centre.x, centre.y))
            else:
                missing.append(number)
    return points, missing


def write_border(path, ring, drawing):
    polygon = Polygon(ring["points"])
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = ["x,y,vertex,area,perimeter,drawing,layer,handle"]
    for index, (x, y) in enumerate(ring["points"]):
        lines.append("%.4f,%.4f,%d,%.3f,%.3f,%s,%s,%s" % (x, y, index, polygon.area, polygon.exterior.length, drawing, ring["layer"], ring["handle"]))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return polygon


def read_drawing(cad_reader, drawing):
    """pieces and texts of one drawing, read into a temporary folder that is removed before returning."""
    folder = Path(tempfile.mkdtemp(prefix="border_"))
    try:
        empty = folder / "layers.json"
        empty.write_text(json.dumps({"keep": [], "drop": []}), encoding="utf-8")
        out = folder / "out"
        done = subprocess.run([str(cad_reader), str(drawing), str(out), "--layers", str(empty)], capture_output=True, text=True, timeout=600)
        if done.returncode != 0:
            raise BorderError("CadReader failed on " + drawing.name + ": " + done.stderr.strip())
        pieces = json.loads((out / "pieces.json").read_text(encoding="utf-8"))["pieces"]
        texts = json.loads((out / "texts.json").read_text(encoding="utf-8"))["texts"]
        return pieces, texts
    finally:
        shutil.rmtree(folder, ignore_errors=True)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Take the Phase 1 border for the data book (R163)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book = config["dataBook"]
    root = Path(book["root"])
    drawing = root / "work" / book["drawings"][book["border"]["drawing"]]
    pieces, texts = read_drawing(REPO / book["cadReader"], drawing)
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    inputs = Path(levels_proof.input_root(settings)) / full_run.XLSX_FOLDER
    shifts = config["connect"]["neighbourhoods"]
    books = [(inputs / entry["schedule"], entry["shift"]) for entry in shifts.values() if entry]
    plots = link_summary.load(config["connect"]["baseplanPlots"])["plots"]
    points, missing = schedule_points(plots, books)
    ring, how, table = pick(rings(pieces), texts, book["border"]["layer"], book["border"]["label"], points)
    polygon = write_border(root / "phase1_border.csv", ring, drawing.name)
    report = {"drawing": drawing.name, "chosen": {"layer": ring["layer"], "handle": ring["handle"], "how": how, "vertices": len(ring["points"]),
                                                "area": polygon.area, "perimeter": polygon.exterior.length, "endGap": ring["endGap"], "closedFlag": ring["closedFlag"]},
              "schedulePlots": len(points), "scheduleWithoutFace": missing, "held": ring["holds"],
              "rings": [{k: v for k, v in row.items() if k != "points"} for row in table]}
    (root / "logs").mkdir(parents=True, exist_ok=True)
    (root / "logs" / "border.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    print("border %s %s by %s, area %.0f, holds %d of %d" % (ring["layer"], ring["handle"], how, polygon.area, ring["holds"], len(points)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
