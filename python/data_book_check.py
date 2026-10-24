"""Checks the data book a second way (R168), with its own code and no geometry library.

border: a ray cast of its own on seeded samples of every point table (texts, blocks, point entities), which must all lie
inside, and a full recount of inside and outside for the small drawings named in --recount, from a fresh CadReader run.
counts: the rows of every table against the counts its data.json holds. plots: seeded rows of plots.csv worked out again
from plots.json, the schedules, blocks.csv and texts.csv by plain point in polygon tests. Matches and mismatches are counted.
"""
import argparse
import csv
import json
import random
import shutil
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

import c3d_run
import full_run
import levels_proof
import link_summary
import project_profile

REPO = Path(__file__).resolve().parent.parent
SAMPLE = 200
AREA_SLACK = 0.01
SHARED_M = 0.5


def ring_of(path):
    with open(path, encoding="utf-8") as handle:
        return [(float(r["x"]), float(r["y"])) for r in csv.DictReader(handle)]


def inside(ring, x, y):
    hit = False
    for i in range(len(ring)):
        (x1, y1), (x2, y2) = ring[i - 1], ring[i]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            hit = not hit
    return hit


def edge_distance(ring, x, y):
    best = float("inf")
    for i in range(len(ring)):
        (ax, ay), (bx, by) = ring[i - 1], ring[i]
        dx, dy = bx - ax, by - ay
        length = dx * dx + dy * dy
        t = min(max(((x - ax) * dx + (y - ay) * dy) / length, 0.0), 1.0) if length else 0.0
        best = min(best, ((x - ax - t * dx) ** 2 + (y - ay - t * dy) ** 2) ** 0.5)
    return best


def area(ring):
    return abs(sum(ring[i - 1][0] * ring[i][1] - ring[i][0] * ring[i - 1][1] for i in range(len(ring)))) / 2


def centroid(ring):
    a = sum(ring[i - 1][0] * ring[i][1] - ring[i][0] * ring[i - 1][1] for i in range(len(ring))) / 2
    cx = sum((ring[i - 1][0] + ring[i][0]) * (ring[i - 1][0] * ring[i][1] - ring[i][0] * ring[i - 1][1]) for i in range(len(ring))) / (6 * a)
    cy = sum((ring[i - 1][1] + ring[i][1]) * (ring[i - 1][0] * ring[i][1] - ring[i][0] * ring[i - 1][1]) for i in range(len(ring))) / (6 * a)
    return cx, cy


def table_rows(folder, name):
    """The rows of a table, read from its split files when it was split by layer."""
    whole = folder / (name + ".csv")
    paths = [whole] if whole.is_file() else sorted((folder / name).glob("*.csv")) if (folder / name).is_dir() else []
    rows = []
    for path in paths:
        with open(path, encoding="utf-8") as handle:
            rows += list(csv.DictReader(handle))
    return rows


def check_points(ring, rows, xkey, ykey, seed):
    picked = random.Random(seed).sample(rows, min(SAMPLE, len(rows)))
    found = Counter("inside" if inside(ring, float(r[xkey]), float(r[ykey])) else "outside" for r in picked if r[xkey] and r[ykey])
    return dict(found)


def check_counts(folder):
    summary = json.loads((folder / "data.json").read_text(encoding="utf-8"))
    counts = summary.get("counts") or {}
    result = {}
    for table in ("entities", "blocks", "texts"):
        expected = sum(v for k, v in (counts.get(table) or {}).items() if k.startswith("kept"))
        result[table] = "match" if len(table_rows(folder, table)) == expected else "mismatch %d against %d" % (len(table_rows(folder, table)), expected)
    return result


def recount(cad_reader, drawing, ring):
    """Inside and outside of every text and block of a fresh CadReader run, by the ray cast of this file."""
    folder = Path(tempfile.mkdtemp(prefix="book_check_"))
    try:
        empty = folder / "layers.json"
        empty.write_text(json.dumps({"keep": [], "drop": []}), encoding="utf-8")
        subprocess.run([str(cad_reader), str(drawing), str(folder / "out"), "--layers", str(empty)], capture_output=True, timeout=1800, check=True)
        texts = json.loads((folder / "out" / "texts.json").read_text(encoding="utf-8"))["texts"]
        blocks = json.loads((folder / "out" / "blocks.json").read_text(encoding="utf-8"))["blocks"]
        return {"texts": dict(Counter("kept" if inside(ring, t["x"], t["y"]) else "outside" for t in texts)),
                "blocks": dict(Counter("kept" if inside(ring, b["insertPoint"][0], b["insertPoint"][1]) else "outside" for b in blocks))}
    finally:
        shutil.rmtree(folder, ignore_errors=True)


def faces_by_label(plots):
    found = {}
    for entry in plots:
        label = str(entry.get("plot") or "").strip()
        if entry.get("kind") == "plot_shape" and label.isdigit() and len(entry.get("vertices") or []) >= 3:
            found.setdefault(label, []).append([tuple(v[:2]) for v in entry["vertices"]])
    return found


def check_plots(rows, faces, ring, schedules, blocks, v9, seed, size):
    picked = random.Random(seed).sample(rows, min(size, len(rows)))
    counts = Counter()
    for row in picked:
        rings = faces.get(row["baseplanLabel"]) or []
        if len(rings) != 1:
            counts["skippedNotOneFace"] += 1
            continue
        face = rings[0]
        cx, cy = centroid(face)
        counts["insideMatch" if inside(ring, cx, cy) else "insideMismatch"] += 1
        counts["areaMatch" if abs(area(face) - float(row["faceAreaM2"])) <= AREA_SLACK else "areaMismatch"] += 1
        homes = [name for name, (shift, numbers) in schedules.items() if str(int(row["baseplanLabel"]) + shift) in numbers]
        mine = homes[0] if len(homes) == 1 else ""
        counts["neighbourhoodMatch" if mine == row["neighbourhood"] else "neighbourhoodMismatch"] += 1
        on = sorted(b["name"] for b in blocks if inside(face, b["x"], b["y"]))
        if not on:
            on = sorted(b["name"] for b in blocks if edge_distance(face, b["x"], b["y"]) <= SHARED_M and not inside(face, b["x"], b["y"]))
        counts["blockMatch" if "|".join(on) == "|".join(sorted((row["footprintBlock"] or "").split("|"))) or (not on and not row["footprintBlock"]) else "blockMismatch"] += 1
        numbers = sorted({t for t, x, y in v9 if inside(face, x, y)})
        counts["v9Match" if numbers == sorted(filter(None, (row["v9PlotNumber"] or "").split("|"))) else "v9Mismatch"] += 1
    return dict(counts)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for the data book (R168)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--size", type=int, default=20)
    parser.add_argument("--recount", nargs="*", default=["cutsheet", "phaseBoundaries", "baseRoadLayout"])
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book = config["dataBook"]
    root = Path(book["root"])
    ring = ring_of(root / "phase1_border.csv")
    result = {"border": {}, "counts": {}, "recount": {}, "plots": {}}
    for role, relative in book["drawings"].items():
        folder = root / Path(relative).stem
        if not (folder / "data.json").is_file():
            continue
        result["counts"][role] = check_counts(folder)
        texts, blocks = table_rows(folder, "texts"), table_rows(folder, "blocks")
        result["border"][role] = {"texts": check_points(ring, texts, "x", "y", 14), "blocks": check_points(ring, blocks, "x", "y", 14)}
    for role in args.recount:
        drawing = root / "work" / book["drawings"][role]
        mine = recount(REPO / book["cadReader"], drawing, ring)
        stored = json.loads((root / drawing.stem / "data.json").read_text(encoding="utf-8")).get("counts") or {}
        result["recount"][role] = {table: {"mine": mine[table], "stored": stored.get(table, {}),
                                           "match": mine[table].get("kept", 0) == stored.get(table, {}).get("kept", 0) and mine[table].get("outside", 0) == stored.get(table, {}).get("outside", 0)}
                                   for table in ("texts", "blocks")}
    with open(root / "plots.csv", encoding="utf-8") as handle:
        rows = list(csv.DictReader(handle))
    faces = faces_by_label(link_summary.load(config["connect"]["baseplanPlots"])["plots"])
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    inputs = Path(levels_proof.input_root(settings)) / full_run.XLSX_FOLDER
    schedules = {name: (entry["shift"], {str(levels_proof.number_of(r["plot"])) for r in levels_proof.read_schedule(inputs / entry["schedule"])})
                 for name, entry in config["connect"]["neighbourhoods"].items() if entry}
    foot = root / Path(book["drawings"][book["join"]["footprint"]["drawing"]]).stem
    blocks = [{"name": r["name"], "x": float(r["cx"] or r["x"]), "y": float(r["cy"] or r["y"])} for r in table_rows(foot, "blocks")]
    v9_folder = root / Path(book["drawings"][book["join"]["v9"]["drawing"]]).stem
    import fnmatch
    v9 = [(r["text"].strip(), float(r["x"]), float(r["y"])) for r in table_rows(v9_folder, "texts") if fnmatch.fnmatchcase(r["layer"], book["join"]["v9"]["plotNumberLayer"])]
    for seed in (14, 7):
        result["plots"]["seed%d" % seed] = check_plots(rows, faces, ring, schedules, blocks, v9, seed, args.size)
    (root / "logs").mkdir(parents=True, exist_ok=True)
    (root / "logs" / "check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    for key, value in result.items():
        print(key, json.dumps(value)[:1500])
    return 0


if __name__ == "__main__":
    sys.exit(main())
