"""Checks the chosen wiring of plots.csv a second way (R178), with plain math and no geometry library.

For 20 unit plots at seed 14 and 20 at seed 7 the share of every footprint and R06 block outline in the face is estimated on
a point grid with a ray cast of this file, the setback line that owns the face is found the same way, the curb and ROW are
measured from the middle of the edge of at least minEdgeM nearest a curb segment, the cutsheet number comes from the frame
that holds the face centroid and the V9 numbers from a ray cast. 20 schedule plots per seed check the label plus shift. Each
is compared with plots.csv and counted as a match or a mismatch.
"""
import argparse
import fnmatch
import json
import random
import sys
from collections import Counter
from pathlib import Path

import c3d_check
import c3d_run
import data_book
import data_book_analysis
import data_book_check
import link_summary

EDGE_SLACK = 1e-9


def inside(ring, x, y):
    return data_book_check.inside(ring, x, y)


def box(ring):
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    return min(xs), min(ys), max(xs), max(ys)


def boxes_meet(a, b, pad=0.0):
    return a[0] - pad <= b[2] and b[0] - pad <= a[2] and a[1] - pad <= b[3] and b[1] - pad <= a[3]


def grid_share(ring, face, step, most=None):
    """The share of the ring that lies in the face, from the points of a grid of step over the ring box. A large ring gets a
    wider step so its grid holds no more than most points."""
    x0, y0, x1, y1 = box(ring)
    if most:
        step = max(step, ((x1 - x0) * (y1 - y0) / most) ** 0.5)
    total = hit = 0
    y = y0 + step / 2
    while y < y1:
        x = x0 + step / 2
        while x < x1:
            if inside(ring, x, y):
                total += 1
                hit += inside(face, x, y)
            x += step
        y += step
    return hit / total if total else 0.0


def shared_names(face, outlines, min_share, step, slack, most):
    """The block names whose share in the face reaches min_share, and the count of blocks within slack of it."""
    names, close = [], 0
    fb = box(face)
    for name, ring, rb in outlines:
        if not boxes_meet(fb, rb):
            continue
        share = grid_share(ring, face, step, most)
        if abs(share - min_share) <= slack:
            close += 1
        if share >= min_share:
            names.append(name)
    return sorted(names), close


def front_middle(ring, curbs, min_edge):
    best = None
    for k in range(len(ring)):
        a, b = ring[k - 1], ring[k]
        if ((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2) ** 0.5 < min_edge:
            continue
        middle = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        distance = c3d_check.nearest_segment(curbs, *middle)
        if best is None or distance < best[0] - EDGE_SLACK:
            best = (distance, middle)
    return best


def outlines_of(path):
    found = []
    for block in json.loads(Path(path).read_text(encoding="utf-8"))["blocks"]:
        ring = [tuple(p[:2]) for p in block.get("outline") or []]
        if len(ring) >= 3:
            found.append((block["name"].split("$")[-1], ring, box(ring)))
    return found


def setback_rings(pieces, layer):
    return [[tuple(p[:2]) for p in piece["pts"]] for piece in pieces
            if piece.get("kind") == "polyline" and piece.get("layer") == layer and piece.get("closed") and len(piece.get("pts") or []) >= 3]


def ring_distance(ring, x, y):
    return data_book_check.edge_distance(ring, x, y)


def check_plot(row, face, data, settings):
    counts = Counter()
    step, slack, most = settings["check"]["gridM"], settings["check"]["shareSlack"], settings["check"]["maxGridPoints"]
    for column, outlines in (("footprintBlock", data["footprint"]), ("r06Block", data["r06"])):
        names, close = shared_names(face, outlines, settings["minShare"], step, slack, most)
        theirs = sorted(filter(None, (row[column] or "").split("|")))
        if column == "r06Block":
            names = sorted(set(names))
        ok = names == theirs
        counts[column + (" match" if ok else " mismatch near the share" if close else " mismatch")] += 1
    owned = [ring for ring in data["setbacks"] if boxes_meet(box(face), box(ring)) and grid_share(ring, face, step, most) >= settings["ownerShareMin"]]
    theirs = data_book_analysis.number(row["setbackFromFrontM"])
    counts["setbackOwner " + ("match" if bool(owned) == (theirs is not None) or (owned and row["noFront"] == "yes") else "mismatch")] += 1
    front = front_middle(face, data["curbs"], settings["check"]["minEdgeM"])
    for column, segments in (("curbDistanceM", data["curbs"]), ("rowDistanceM", data["rows"])):
        theirs = data_book_analysis.number(row[column])
        mine = c3d_check.nearest_segment(segments, *front[1]) if front else None
        ok = (mine is None and theirs is None) or (mine is not None and theirs is not None and abs(mine - theirs) <= settings["check"]["distanceSlackM"])
        counts[column + (" match" if ok else " mismatch")] += 1
    cx, cy = data_book_check.centroid(face)
    frames = [frame for frame in data["frames"] if inside(frame, cx, cy)]
    numbers = sorted({t for t, x, y in data["sheetNumbers"] for frame in frames if inside(frame, x, y)})
    counts["cutsheet " + ("match" if "|".join(numbers) == (row["cutsheet"] or "") else "mismatch")] += 1
    v9 = sorted(t for t, x, y in data["v9"] if inside(face, x, y))
    counts["v9 " + ("match" if v9 == sorted(filter(None, (row["v9PlotNumber"] or "").split("|"))) else "mismatch")] += 1
    return counts


def check_schedules(rows, shifts):
    counts = Counter()
    for row in rows:
        shift = shifts.get(row["neighbourhood"])
        counts["labelShift " + ("match" if shift is not None and str(int(row["officialNumber"]) - shift) == row["baseplanLabel"] else "mismatch")] += 1
    return counts


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for the chosen wiring (R178)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book, connect = config["dataBook"], config["connect"]
    settings, join = book["wiring"], book["join"]
    roots = join["fallbackOutputs"]
    root = Path(book["root"])
    rows = data_book_analysis.read_csv(root / "plots.csv")
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])

    def pieces(role):
        return json.loads((data_book.folder_of(book, role, roots)[1] / "pieces.json").read_text(encoding="utf-8"))["pieces"]

    _, cut_folder = data_book.folder_of(book, join["cutsheet"]["drawing"], roots)
    cut_pieces = json.loads((cut_folder / "pieces.json").read_text(encoding="utf-8"))["pieces"]
    cut_texts = json.loads((cut_folder / "texts.json").read_text(encoding="utf-8"))["texts"]
    v9_data, v9_earlier = data_book.folder_of(book, join["v9"]["drawing"], roots)
    data = {
        "footprint": outlines_of(data_book.folder_of(book, join["footprint"]["drawing"], roots)[1] / "blocks.json"),
        "r06": outlines_of(Path(settings["r06Outlines"]) / "blocks.json"),
        "setbacks": setback_rings(pieces(join["footprint"]["drawing"]), join["footprint"]["setbackLayer"]),
        "curbs": c3d_check.segments_of(pieces(join["curb"]["drawing"]), join["curb"]["layers"]),
        "rows": c3d_check.segments_of(pieces(join["row"]["drawing"]), join["row"]["layers"]),
        "frames": [[tuple(p[:2]) for p in piece["pts"]] for piece in cut_pieces if piece.get("kind") == "polyline"
                   and fnmatch.fnmatchcase((piece.get("layer") or "").upper(), join["cutsheet"]["frameLayer"].upper()) and len(piece.get("pts") or []) > 3],
        "sheetNumbers": [(t["text"], t["x"], t["y"]) for t in cut_texts if t.get("layer") == join["cutsheet"]["numberLayer"]],
        "v9": [(t["text"].strip(), t["x"], t["y"]) for t in data_book.texts_of(v9_data, v9_earlier)[0]
               if fnmatch.fnmatchcase(t.get("layer") or "", join["v9"]["plotNumberLayer"]) and (t.get("text") or "").strip()],
    }
    units = set(book["analysis"]["unitPlotTypes"])
    unit_rows = sorted((r for r in rows if r["clientType"] in units and len(faces.get(r["baseplanLabel"]) or []) == 1), key=lambda r: int(r["baseplanLabel"]))
    scheduled = sorted((r for r in rows if r["officialNumber"]), key=lambda r: int(r["baseplanLabel"]))
    shifts = {name: entry["shift"] for name, entry in connect["neighbourhoods"].items() if entry}
    result = {}
    for seed in settings["check"]["seeds"]:
        counts = Counter()
        for row in random.Random(seed).sample(unit_rows, min(settings["check"]["size"], len(unit_rows))):
            counts.update(check_plot(row, faces[row["baseplanLabel"]][0], data, settings))
        counts.update(check_schedules(random.Random(seed).sample(scheduled, min(settings["check"]["size"], len(scheduled))), shifts))
        result["seed%d" % seed] = dict(sorted(counts.items()))
    (root / "logs" / "wiring_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
