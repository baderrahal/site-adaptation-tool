"""Second method for the villa hand, the CASE and the split tests (R134).

It reads the raw files with json and math only, no geometry library and none of the helpers of cad_rules.py. villas works
out the side neighbours of drawn villas from the shared boundary length of the raw faces, edge by edge, and test b from
the raw mirror flag. case counts the links each hand value explains again over the raw answer key. split works out the
walls of drawn clusters by hand from the schedule rows and the raw profile. Each must agree with cad_rules.json.
"""
import argparse
import itertools
import json
import math
import random
import sys
from pathlib import Path

import cad_links_check
import levels_proof

SEED = 14
SAMPLE = 20
# Two edges lie on one line when both ends of one are this near the other line, in metres
ON_LINE = 0.01
EPS = 1e-6


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def shared_length(first, second):
    """The length of boundary two rings share, the overlap of every pair of edges that lie on one line."""
    total = 0.0
    for i in range(len(first)):
        a, b = first[i], first[(i + 1) % len(first)]
        length = math.dist(a, b)
        if length == 0:
            continue
        ux, uy = (b[0] - a[0]) / length, (b[1] - a[1]) / length
        for j in range(len(second)):
            c, d = second[j], second[(j + 1) % len(second)]
            if max(abs((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) for p in (c, d)) > ON_LINE:
                continue
            s, t = sorted(((c[0] - a[0]) * ux + (c[1] - a[1]) * uy, (d[0] - a[0]) * ux + (d[1] - a[1]) * uy))
            total += max(0.0, min(length, t) - max(0.0, s))
    return total


def drawn(items, seed, sample):
    ordered = list(items)
    random.Random(seed).shuffle(ordered)
    return ordered[:sample]


def check_villas(report, prototypes, plots, pieces, analysis, profile, settings, shift, seed, sample):
    document = next(item for item in prototypes["documents"] if item.get("kind") == "project")
    mirrored = {}
    for link in document.get("links") or []:
        number = str((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number") or "").strip()
        if number.isdigit() and link.get("transform"):
            mirrored[str(int(number))] = bool(link["transform"]["mirrored"])
    faces, types = {}, {}
    for entry in plots:
        label = str(entry.get("plot") or "")
        if entry.get("kind") == "plot_shape" and label.isdigit():
            number = str(int(label) + shift)
            faces.setdefault(number, []).append(cad_links_check.counter_clockwise([(vertex[0], vertex[1]) for vertex in entry["vertices"]]))
            types[number] = entry.get("type")
    units = {str(int(record["plot"]) + shift): record["unit"] for record in analysis["plots"] if record.get("unit")}
    carriers = set(units) | {number for number, kind in types.items() if kind in settings["analysis"]["unitPlotTypes"]}
    widths = [item["plot_width_m"] for item in profile["typologies"].values() if isinstance(item.get("plot_width_m"), (int, float))]
    low = min(widths) - settings["analysis"]["toleranceM"]
    wall = settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0
    segments = cad_links_check.road_segments(pieces, settings["analysis"]["roadEdgeLayers"])
    villas = report["villas"]
    flipped = set(villas["reversed"])
    apart = set(report["apart"])
    results = []
    for plot in drawn(sorted(villas["sides"], key=int), seed, sample):
        if len(faces.get(plot) or []) != 1:
            results.append({"plot": plot, "skipped": "no single face"})
            continue
        face = faces[plot][0]
        normal, right, _, _ = cad_links_check.front(face, segments, low, wall)
        centre = cad_links_check.centroid(face)
        sides = {"left": [], "right": []}
        for other, rings in faces.items():
            if other == plot or other not in carriers or len(rings) != 1:
                continue
            ring = rings[0]
            if max(math.dist(centre, point) for point in ring) > 60:
                continue
            if shared_length(face, ring) + EPS < wall:
                continue
            middle = cad_links_check.centroid(ring)
            sides["right" if (middle[0] - centre[0]) * right[0] + (middle[1] - centre[1]) * right[1] > 0 else "left"].append(other)
        sides = {side: sorted(items, key=int) for side, items in sides.items()}
        differs = []
        if sides != villas["sides"][plot]:
            differs.append("sides " + str(sides) + " against " + str(villas["sides"][plot]))
        held = [side for side in ("left", "right") if sides[side]]
        if len(held) == 1 and plot not in apart and plot in mirrored:
            holds = (mirrored[plot] != (units.get(plot) in flipped)) == (held[0] == "left")
            if holds != (plot not in villas["b"]["breaks"]):
                differs.append("test b differs")
        results.append({"plot": plot, "differs": differs})
    return results



def check_case(report, prototypes):
    """The links the hand explains, counted again over the raw links, against the report."""
    document = next(item for item in prototypes["documents"] if item.get("kind") == "project")
    flipped = set(report["case"]["flipped"])
    apart = set(report["apart"])
    rows = []
    for link in document.get("links") or []:
        parameters = link.get("parameters") or {}
        number = str(parameters.get("RRE_ZZ_SP_Plot-Number") or "").strip()
        case = str(parameters.get("CASE") or "").strip()
        if not number.isdigit() or not case or not link.get("transform"):
            continue
        mirror = bool(link["transform"]["mirrored"])
        facing = round((link["transform"]["rotationDeg"] + (-90.0 if mirror else 90.0)) % 360.0, 1) % 360.0
        rows.append((str(int(number)), case, facing, mirror != (str(parameters.get("RRE_ZZ_SP_Prototype")).strip() in flipped)))
    pairs = {}
    for _, case, facing, _ in rows:
        pairs.setdefault(facing, set()).add(case)
    first = {case: min(cases) for cases in pairs.values() for case in cases}
    votes = {}
    for _, case, _, hand in rows:
        votes.setdefault((first[case], hand), {}).setdefault(case, 0)
        votes[(first[case], hand)][case] += 1
    best = {key: max(sorted(table), key=lambda letter: table[letter]) for key, table in votes.items()}
    breaks = sorted((number for number, case, _, hand in rows if best[(first[case], hand)] != case and number not in apart), key=int)
    explained = sum(1 for number, case, _, hand in rows if best[(first[case], hand)] == case)
    test = report["case"]["tests"]["hand"]
    differs = []
    if explained != test["explained"]:
        differs.append("explained " + str(explained) + " against " + str(test["explained"]))
    if breaks != test["breaksOutside"]:
        differs.append("breaks " + str(breaks) + " against " + str(test["breaksOutside"]))
    return [{"test": "hand", "differs": differs}]


def check_split(report, books, profile, seed, sample):
    """The rule walls and the sheet walls of drawn clusters, by hand, against the report."""
    table = profile["steps"]["base_height"]
    base = lambda unit: (table[unit] if unit in table else table["default"]) / 1000.0
    heights = [entry[0] for entry in profile["steps"]["legend"]]
    low, high = heights[0], heights[-1]
    allowed = set(profile["cluster_split_positions"])
    slack = 0.0005 + 1e-9
    results = []
    for cluster in drawn(report["split"]["clusters"], seed, sample):
        rows = [books[cluster["neighbourhood"]][plot] for plot in cluster["plots"]]
        level = lambda piece: round(max(row["entry"] for row in piece) + max(base(piece[0]["unit"]), base(piece[-1]["unit"])), 3)
        sheet = [index + 1 for index in range(len(rows) - 1) if abs(rows[index + 1]["ffl"] - rows[index]["ffl"]) > 0.0005]
        whole = level(rows)
        if all(whole - row["entry"] <= high + slack for row in rows):
            rule = []
        else:
            count = len(rows)
            walls = [wall for wall in range(1, count) if count == profile["cluster_max_units"] and ({wall, count - wall} & allowed)]
            rule = None
            for size in range(1, len(walls) + 1):
                for chosen in itertools.combinations(walls, size):
                    cuts = [0, *chosen, count]
                    pieces = [rows[start:end] for start, end in zip(cuts, cuts[1:])]
                    if all(low - slack <= level(piece) - row["entry"] <= high + slack for piece in pieces for row in piece):
                        rule = list(chosen)
                        break
                if rule is not None:
                    break
        differs = []
        if sheet != cluster["sheetWalls"]:
            differs.append("sheet walls differ")
        if rule != cluster["ruleWalls"]:
            differs.append("rule walls " + str(rule) + " against " + str(cluster["ruleWalls"]))
        results.append({"plots": cluster["plots"], "differs": differs})
    return results


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check cad_rules.json a second way (R134).")
    parser.add_argument("part", choices=("villas", "case", "split"))
    parser.add_argument("out_dir")
    parser.add_argument("--report", required=True)
    parser.add_argument("--prototypes")
    parser.add_argument("--baseplan")
    parser.add_argument("--roads", help="pieces.json of the road layout drawing")
    parser.add_argument("--analysis")
    parser.add_argument("--profile", required=True)
    parser.add_argument("--settings")
    parser.add_argument("--shift", type=int)
    parser.add_argument("--schedule", action="append", default=[], help="neighbourhood=path of a schedule workbook, once per schedule")
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--sample", type=int, default=SAMPLE)
    args = parser.parse_args(argv)
    report, profile = read(args.report), read(args.profile)
    if args.part == "villas":
        results = check_villas(report, read(args.prototypes), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.analysis), profile, read(args.settings), args.shift, args.seed, args.sample)
    elif args.part == "case":
        results = check_case(report, read(args.prototypes))
    else:
        books = {}
        for item in args.schedule:
            hood, _, path = item.partition("=")
            books[hood] = {row["plot"]: row for row in levels_proof.read_schedule(path)}
        results = check_split(report, books, profile, args.seed, args.sample)
    checked = [item for item in results if "differs" in item]
    agree = [item for item in checked if not item["differs"]]
    out = {"part": args.part, "seed": args.seed, "drawn": len(results), "checked": len(checked), "agree": len(agree),
           "skipped": [item for item in results if "skipped" in item], "disagree": [item for item in checked if item["differs"]]}
    path = Path(args.out_dir) / ("cad_rules_check_" + args.part + ".json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1), encoding="utf-8")
    print("%s seed %d, drawn %d, checked %d, agree %d" % (args.part, args.seed, len(results), len(checked), len(agree)))
    return 0 if len(agree) == len(checked) else 1


if __name__ == "__main__":
    sys.exit(main())
