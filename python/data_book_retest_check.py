"""Checks round DATA-2 a second way (R173), with plain math and no geometry library.

For 20 plots at seed 14 and 20 at seed 7: the front edge is taken again as the face edge of at least minEdgeM whose middle
lies nearest a curb segment, the width is its length, and the setback is the distance from its middle to the nearest edge of
a setback line within the face box, classed at the lines of setbackLinesM. El @ Entry is worked out again from the brute force
level of the road surface at the ends of that edge (c3d_check.brute_level) for every variant of R170. Each class and band is
compared with the main output. The surfaces the full window exported are compared with the existing LandXML of the same name
on every plot where both give a level.
"""
import argparse
import json
import random
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import c3d_check
import c3d_run
import data_book
import data_book_analysis
import data_book_check
import data_book_retest
import landxml_reader
import link_summary
import project_profile

EXPORT_SLACK = 0.001


def front_edge(ring, curbs, min_edge):
    """The edge of at least min_edge whose middle lies nearest a curb segment, as its two ends."""
    best = None
    for k in range(len(ring)):
        a, b = ring[k - 1], ring[k]
        length = ((b[0] - a[0]) ** 2 + (b[1] - a[1]) ** 2) ** 0.5
        if length < min_edge:
            continue
        distance = c3d_check.nearest_segment(curbs, (a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        if best is None or distance < best[0]:
            best = (distance, a, b, length)
    return best


def boxed(segments, ring):
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    return [s for s in segments if min(s[0], s[2]) <= max(xs) and max(s[0], s[2]) >= min(xs) and min(s[1], s[3]) <= max(ys) and max(s[1], s[3]) >= min(ys)]


def setback_class(value, lines, tolerance):
    if value is None:
        return "none"
    for line in lines:
        if abs(value - line) <= tolerance:
            return "%.2f" % line
    return "other"


def width_class(kind, width, analysis):
    if kind in (analysis.get("widthFromCad") or []):
        return "fromCad"
    allowed = analysis["widthsByClientType"].get(kind)
    if width is None or allowed is None:
        return "notChecked"
    return "matches" if any(abs(width - value) <= analysis["widthToleranceM"] for value in allowed) else "differs"


def check_fronts(rows, faces, curbs, setbacks, analysis, retest):
    counts = Counter()
    lines, tolerance = retest["setbackLinesM"], analysis["setbackToleranceM"]
    for row in rows:
        rings = faces.get(row["baseplanLabel"]) or []
        if len(rings) != 1:
            counts["skippedNotOneFace"] += 1
            continue
        edge = front_edge(rings[0], curbs, retest["check"]["minEdgeM"])
        if edge is None:
            counts["noEdge"] += 1
            continue
        _, a, b, length = edge
        width = data_book_analysis.number(row["widthM"])
        counts["widthValueMatch" if width is not None and abs(width - length) <= retest["check"]["lengthSlackM"] else "widthValueMismatch"] += 1
        counts["widthClassMatch" if width_class(row["clientType"], length, analysis) == width_class(row["clientType"], width, analysis) else "widthClassMismatch"] += 1
        near = boxed(setbacks, rings[0])
        mine = c3d_check.nearest_segment(near, (a[0] + b[0]) / 2, (a[1] + b[1]) / 2) if near else None
        theirs = data_book_analysis.number(row["setbackFromFrontM"])
        mine_class, their_class = setback_class(mine, lines, tolerance), setback_class(theirs, lines, tolerance)
        counts["setbackClassMatch" if mine_class == their_class else "setbackClassMismatch"] += 1
        counts["setback " + their_class] += 1
    return dict(counts)


def check_entry(rows, faces, curbs, surfaces, entry_by_key, profile, retest, bands, snap):
    counts = Counter()
    steep = set(retest["steepUnits"])
    for row in rows:
        rings = faces.get(row["baseplanLabel"]) or []
        edge = front_edge(rings[0], curbs, retest["check"]["minEdgeM"]) if len(rings) == 1 else None
        name = row["roadLevelSurface"]
        levels = [c3d_check.brute_level(*surfaces[name], p[0], p[1], snap) for p in edge[1:3]] if edge and name in surfaces else [None, None]
        entry = data_book_analysis.number(row["pastEntry"])
        unit = row["unitRev6"]
        for variant, slope in data_book_retest.slopes(profile, steep, unit).items():
            if None in levels:
                mine = "noLevel"
            else:
                mine = data_book_analysis.band(sum(levels) / 2 + slope / 100 * data_book_retest.ramp_depth(profile, unit) - entry, bands)
            theirs = entry_by_key.get((row["officialNumber"], variant))
            counts[variant + (" match" if mine == theirs else " mismatch")] += 1
            counts[variant + " " + str(theirs)] += 1
    return dict(counts)


def check_export(rows, pairs):
    """Each exported surface against the existing LandXML surface of the same name, on every plot where both give a level."""
    result = {}
    for old, new in pairs:
        found = Counter()
        for row in rows:
            for side in ("left", "right"):
                a = data_book_analysis.number(row.get("level_%s_%s" % (old, side)))
                b = data_book_analysis.number(row.get("level_%s_%s" % (new, side)))
                if a is None or b is None:
                    found["onlyOne" if (a is None) != (b is None) else "neither"] += 1
                else:
                    found["equal" if abs(a - b) <= EXPORT_SLACK else "differ"] += 1
        result[old] = dict(found)
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for round DATA-2 (R173)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book, connect = config["dataBook"], config["connect"]
    retest, analysis = book["retest"], book["analysis"]
    root = Path(book["root"])
    rows = data_book_analysis.read_csv(root / "plots.csv")
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])
    roots = book["join"]["fallbackOutputs"]
    pieces = {}
    for key in ("curb", "footprint"):
        _, earlier = data_book.folder_of(book, book["join"][key]["drawing"], roots)
        pieces[key] = json.loads((earlier / "pieces.json").read_text(encoding="utf-8"))["pieces"]
    curbs = c3d_check.segments_of(pieces["curb"], book["join"]["curb"]["layers"])
    setbacks = c3d_check.segments_of(pieces["footprint"], [book["join"]["footprint"]["setbackLayer"]])
    entry_by_key = {(r["plot"], r["variant"]): r["band"] for r in data_book_analysis.read_csv(root / "retest_entry.csv") if r["surface"] == data_book_retest.ROAD}
    surfaces = {}
    for name in {r["roadLevelSurface"] for r in rows if r["roadLevelSurface"]}:
        folder = Path(connect["landxmlDir"]) / landxml_reader.folder_name(name)
        surfaces[name] = (np.load(folder / "points.npy"), np.load(folder / "faces.npy"))
    unit_types = set(analysis["unitPlotTypes"])
    fronted = sorted((r for r in rows if r["clientType"] in unit_types and r["noFront"] == "no"), key=lambda r: int(r["baseplanLabel"]))
    scheduled = sorted((r for r in rows if (r["unitRev6Source"] or "").startswith("schedule") and r["pastEntry"]), key=lambda r: int(r["baseplanLabel"]))
    result = {"fronts": {}, "entry": {}}
    for seed in retest["check"]["seeds"]:
        size = retest["check"]["size"]
        result["fronts"]["seed%d" % seed] = check_fronts(random.Random(seed).sample(fronted, min(size, len(fronted))), faces, curbs, setbacks, analysis, retest)
        # The 3.00 m line holds few plots, so each line gets its own sample as well
        for line in retest["setbackLinesM"]:
            at = [r for r in fronted if setback_class(data_book_analysis.number(r["setbackFromFrontM"]), [line], analysis["setbackToleranceM"]) != "other"
                  and r["setbackFromFrontM"]]
            result["fronts"]["seed%d at %.2f" % (seed, line)] = check_fronts(random.Random(seed).sample(at, min(size, len(at))), faces, curbs, setbacks, analysis, retest)
        result["entry"]["seed%d" % seed] = check_entry(random.Random(seed).sample(scheduled, min(size, len(scheduled))), faces, curbs, surfaces, entry_by_key,
                                                       profile, retest, analysis["bandsM"], connect["snapM"])
    exported = [c[len("level_"):-len("_left")] for c in rows[0] if c.startswith("level_") and c.endswith("_left") and "__" in c]
    pairs = [(name.split("__", 1)[1], name) for name in exported if "level_%s_left" % name.split("__", 1)[1] in rows[0]]
    result["export"] = check_export(rows, pairs)
    (root / "logs" / "retest_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
