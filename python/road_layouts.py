"""The facts of the two road layouts for Bader to ask which is current, Part C of round DATA-5 (R255).

For each road layout of dataBook.data5.roadLayouts: the created and saved times of its header (CadReader --header-only,
R241), every text of its DXF that matches a revision pattern, in model space, in a paper space layout or as an attribute
of an insert, and its curb. The curbs are compared as R177 does, a point every curbStepM along each curb inside Phase 1
and its distance to the other curb, and every point further than farM is kept. Those points are grouped into stretches
along their curb and counted by a 3 by 3 grid over Phase 1, and a plan image of both curbs with the far stretches drawn
over them is written as an SVG in the data book. The tool picks no layout. Rows go to road_layouts.json and the image to
curb_difference.svg in the road_layouts folder of data5.
"""
import argparse
import json
import re
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

import c3d_connect
import c3d_run
import data_book
import dxf_count

REPO = Path(__file__).resolve().parent.parent
GRID = (("SW", "S", "SE"), ("W", "middle", "E"), ("NW", "N", "NE"))


def header(reader, copy):
    """Created and saved times and the units of a DWG header, or the error."""
    with tempfile.TemporaryDirectory() as folder:
        done = subprocess.run([str(reader), str(copy), folder, "--header-only"], capture_output=True, text=True, timeout=600)
        if done.returncode != 0:
            return {"error": done.stderr.strip() or "exit %d" % done.returncode}
        found = json.loads((Path(folder) / "header.json").read_text(encoding="utf-8"))
    return {k: found.get(k) for k in ("created", "updated", "units", "modelSpaceEntities")}


def revision_texts(parsed, patterns):
    """Texts and attributes that match a pattern, with the space they sit in: model, the active layout or a layout block."""
    rules = [re.compile(p, re.IGNORECASE) for p in patterns]
    found = []

    def take(entity, space):
        texts = [("", entity.get("text") or "")] if entity["type"] in dxf_count.TEXT_TYPES | {"ATTDEF"} else []
        texts += [(a["tag"], a["text"]) for a in entity.get("attributes") or []]
        for tag, text in texts:
            if text and any(rule.search(text) or rule.search(tag) for rule in rules):
                found.append({"space": space, "type": entity["type"], "layer": entity["layer"], "tag": tag, "text": text[:200]})

    for entity in parsed["entities"]:
        take(entity, "paper" if entity["paper"] else "model")
    for name, block in parsed["blocks"].items():
        if name.lower().startswith("*paper_space"):
            for entity in block["entities"]:
                take(entity, name)
    return found


def layout_content(parsed):
    """What a title block could sit in: the entities of every paper space layout and the texts of model space."""
    layouts = {n: len(b["entities"]) for n, b in parsed["blocks"].items() if n.lower().startswith("*paper_space")}
    return {"layouts": len(layouts), "paperEntities": sum(layouts.values()) + sum(1 for e in parsed["entities"] if e["paper"]),
            "modelTexts": sum(1 for e in parsed["entities"] if not e["paper"] and e["type"] in ("TEXT", "MTEXT"))}


def far_points(lines, others, step, far, inside):
    """Points every step along each line inside the border and further than far from the other set, as (line, at, x, y)."""
    found, total = [], 0
    for index, line in enumerate(lines):
        n = max(1, int(line.length // step))
        for k in range(n + 1):
            at = min(k * step, line.length)
            point = line.interpolate(at)
            if not inside(point):
                continue
            total += 1
            distance, _ = others.nearest(point.x, point.y)
            if distance is None or distance > far:
                found.append((index, at, point.x, point.y))
    return found, total


def stretches(points, step):
    """Far points in runs along one line, a gap of more than one step starting a new run. The length of each run."""
    runs = []
    for index, at, _, _ in sorted(points):
        if runs and runs[-1]["line"] == index and at - runs[-1]["end"] <= step * 1.5:
            runs[-1]["end"] = at
        else:
            runs.append({"line": index, "start": at, "end": at})
    return sorted((round(r["end"] - r["start"] + step, 1) for r in runs), reverse=True)


def grid_cell(x, y, bounds):
    west, south, east, north = bounds
    column = min(2, max(0, int(3 * (x - west) / (east - west))))
    row = min(2, max(0, int(3 * (y - south) / (north - south))))
    return GRID[row][column]


def svg(border, lines_a, lines_b, far_a, far_b, size):
    """Plan image: the border, both curbs thin, the far points of each curb in its colour. Site metres scaled into size."""
    west, south, east, north = border.bounds
    scale = size / max(east - west, north - south)

    def xy(x, y):
        return "%.1f,%.1f" % ((x - west) * scale, (north - y) * scale)

    def path(line):
        return " ".join(xy(x, y) for x, y in list(line.coords)[:: max(1, len(line.coords) // 200)] + [line.coords[-1]])
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" viewBox="0 0 %d %d">' % (size, size, size, size),
             '<rect width="100%" height="100%" fill="white"/>',
             '<polygon points="%s" fill="none" stroke="black" stroke-width="2"/>' % " ".join(xy(x, y) for x, y in border.exterior.coords)]
    for lines, colour in ((lines_a, "#9bb7d4"), (lines_b, "#d9b38c")):
        parts += ['<polyline points="%s" fill="none" stroke="%s" stroke-width="0.6"/>' % (path(line), colour) for line in lines]
    for points, colour in ((far_a, "#1f4e9c"), (far_b, "#b3541e")):
        parts += ['<circle cx="%.1f" cy="%.1f" r="1.4" fill="%s"/>' % ((x - west) * scale, (north - y) * scale, colour) for _, _, x, y in points]
    parts.append('<text x="10" y="20" font-size="14">blue: Roads curb with no Base Plans curb within the far distance. brown: the other way</text>')
    parts.append("</svg>")
    return "\n".join(parts)


def run(config, reader_header=None):
    book = config["dataBook"]
    settings = book["data5"]["roadLayouts"]
    root = Path(book["root"])
    out = root / book["data5"]["outFolder"] / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    border = data_book.border_polygon(root / "phase1_border.csv")
    inside = border.contains
    reader_header = reader_header or (lambda copy: header(REPO / book["cadReader"], copy))
    report, curbs = {"layouts": {}}, {}
    for name, entry in settings["layouts"].items():
        copy = root / "work" / book["drawings"][entry["role"]]
        stem = copy.stem
        dxf = root / book["data5"]["outFolder"] / book["data5"]["counts"]["dxfFolder"] / (stem + ".dxf")
        parsed = dxf_count.parse(dxf) if dxf.is_file() else None
        texts = revision_texts(parsed, settings["revisionPatterns"]) if parsed else None
        lines = data_book.lines_of(root / book["data5"]["outFolder"] / book["data5"]["xrefs"]["outFolder"] / stem, entry["curbLayers"])
        curbs[name] = lines
        report["layouts"][name] = {"drawing": book["drawings"][entry["role"]], "header": reader_header(copy),
                                   "revisionTexts": texts, "content": layout_content(parsed) if parsed else None, "curbLines": len(lines), "curbLengthM": round(sum(l.length for l in lines if inside(l.representative_point())), 1)}
    first, second = list(settings["layouts"])
    step, far = settings["curbStepM"], settings["farM"]
    far_a, total_a = far_points(curbs[first], c3d_connect.LineSet(curbs[second]), step, far, inside)
    far_b, total_b = far_points(curbs[second], c3d_connect.LineSet(curbs[first]), step, far, inside)
    report["curbs"] = {}
    for name, points, total in ((first, far_a, total_a), (second, far_b, total_b)):
        runs = stretches(points, step)
        report["curbs"][name] = {"points": total, "far": len(points), "farShare": round(len(points) / total, 3) if total else None,
                                 "stretches": len(runs), "longestM": runs[:10], "stretchesOver50m": sum(1 for r in runs if r > 50),
                                 "byGrid": dict(Counter(grid_cell(x, y, border.bounds) for _, _, x, y in points).most_common())}
    (out / "curb_difference.svg").write_text(svg(border, curbs[first], curbs[second], far_a, far_b, settings["imagePx"]), encoding="utf-8")
    (out / "road_layouts.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Facts of the two road layouts: dates, revision texts and where the curbs differ (R255)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    for name, entry in report["layouts"].items():
        print(name, json.dumps(entry["header"]), "revision texts", None if entry["revisionTexts"] is None else len(entry["revisionTexts"]),
              "curb lines", entry["curbLines"], "length", entry["curbLengthM"])
    print(json.dumps(report["curbs"], indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
