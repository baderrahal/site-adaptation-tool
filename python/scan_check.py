"""R420, R427, round 4j-1. The second checks of the scanner, docs/design/17-scanner.md: scan.json of the worker against scan_python.json
field by field, the face count of each candidate of each scan_cad file against a polygonize of its lines and arcs, the plots and villa
types against a plot table of button 2 and on the plots of each answer key, and the inputs before and after the run (D19).
"""

import argparse
import json
import math
import sys
from collections import defaultdict
from pathlib import Path

from shapely import STRtree, snap
from shapely.geometry import LineString
from shapely.ops import polygonize, unary_union

import input_guard
import key_join
import key_reads
import project_profile
import scan
from common import write_json

OUTPUT = "scan_check.json"
LISTS = ("inputs", "rows", "conflicts", "checks", "plots")
# The fields that make an item of each list one item, so the two files are held item by item whatever one of them leaves out
KEYS = {"inputs": ("role",), "rows": ("kind", "source"), "conflicts": ("plot", "field", "sourceA", "sourceB", "valueA", "valueB"),
        "checks": ("what", "source", "plot", "value"), "plots": ("plot",)}
SHOWN = 20
# The node tolerance and the least face area of CadReader/PlotShapes.cs (Tolerance, MinArea), so the polygonize joins the ends and the
# T junctions the trace joins. With no snap it counted 1145 faces where the trace counts 2992 on the ROSHN baseplan (round 4j-1)
NODE_M = 0.01
MIN_AREA = 0.001


def item_key(name, item):
    return tuple(str(item.get(field)) for field in KEYS[name])


def parity(worker, python):
    """R427. The two scan results item by item and field by field per list. An item only one file holds counts as one differ."""
    report = {}
    for name in LISTS:
        ours = {item_key(name, item): item for item in worker.get(name) or []}
        theirs = {item_key(name, item): item for item in python.get(name) or []}
        equal, differ, shown = 0, 0, []
        for key in sorted(set(ours) | set(theirs)):
            if key not in ours or key not in theirs:
                differ += 1
                if len(shown) < SHOWN:
                    shown.append({"item": " | ".join(key), "field": "", "scan": "present" if key in ours else "missing", "scanPython": "present" if key in theirs else "missing"})
                continue
            for field in sorted(set(ours[key]) | set(theirs[key])):
                if ours[key].get(field) == theirs[key].get(field):
                    equal += 1
                else:
                    differ += 1
                    if len(shown) < SHOWN:
                        shown.append({"item": " | ".join(key), "field": field, "scan": ours[key].get(field), "scanPython": theirs[key].get(field)})
        order = [item_key(name, item) for item in worker.get(name) or []] == [item_key(name, item) for item in python.get(name) or []]
        report[name] = {"equal": equal, "differ": differ, "sameOrder": order, "differs": shown}
    return report


def layer_items(node, layer, field):
    """The lines or arcs of a layer, held as an object by layer or as a list of entries that name their layer."""
    if isinstance(node, dict):
        return node.get(layer) or []
    return [item for entry in node or [] if entry.get("layer") == layer for item in entry.get(field) or []]


def chord(arc):
    cx, cy, radius, start, end = arc[:5]
    return ((cx + radius * math.cos(math.radians(start)), cy + radius * math.sin(math.radians(start))),
            (cx + radius * math.cos(math.radians(end)), cy + radius * math.sin(math.radians(end))))


def polygon_count(cad, layers):
    """R420. The faces a polygonize of the lines and arcs of the layers makes, each arc taken as its chord, the segments snapped to
    the node tolerance of the trace and noded first, faces under its least area left out."""
    pieces = []
    for layer in layers:
        for line in layer_items(cad.get("lines"), layer, "lines"):
            pieces.append(((line[0], line[1]), (line[2], line[3])))
        for arc in layer_items(cad.get("arcs"), layer, "arcs"):
            pieces.append(chord(arc))
    shapes = [LineString(piece) for piece in pieces if piece[0] != piece[1]]
    if not shapes:
        return 0
    # A line snapped to itself keeps every vertex, so each is snapped to the lines near it, an end onto a corner or into a T junction
    tree = STRtree(shapes)
    snapped = []
    for index, shape in enumerate(shapes):
        near = [shapes[other] for other in tree.query(shape, predicate="dwithin", distance=NODE_M) if other != index]
        snapped.append(snap(shape, unary_union(near), NODE_M) if near else shape)
    return sum(1 for face in polygonize(unary_union(snapped)) if face.area >= MIN_AREA)


def face_counts(cads):
    """R420. The face count of each candidate against the polygonize count of the same segments."""
    found, equal, differ = [], 0, 0
    for cad in cads:
        for candidate in cad.get("candidates") or []:
            faces, counted = len(candidate.get("faces") or []), polygon_count(cad, candidate["layers"])
            equal, differ = equal + (faces == counted), differ + (faces != counted)
            found.append({"source": cad["source"] + "|" + scan.LAYER_JOIN.join(sorted(candidate["layers"])), "faces": faces, "polygonize": counted})
    return {"equal": equal, "differ": differ, "candidates": found}


def proposed_of(result, kind):
    found = [item["source"] for item in result.get("rows") or [] if item["kind"] == kind and item["proposedUse"] == scan.USE[kind]]
    return found[0] if len(found) == 1 else None


def table_rows(table):
    """The rows of a plot table by baseplan label, the labels two rows hold left out and counted."""
    by_label = defaultdict(list)
    for item in table.get("plots") or []:
        if item.get("baseplanLabel") is not None:
            by_label[str(item["baseplanLabel"])].append(item)
    return {label: found[0] for label, found in by_label.items() if len(found) == 1}, sorted(label for label, found in by_label.items() if len(found) > 1)


def type_counts(plots, rows, numbers, block_source, codes, split):
    """The villa type of the proposed source against typologyRev9 and the code of the villa block source against the code footprintBlock
    holds, on the plots of numbers both hold."""
    counts = {"plots": 0, "typeEqual": 0, "typeDiffer": 0, "typeScanNone": 0, "typeTableNone": 0, "blockEqual": 0, "blockDiffer": 0, "blockNone": 0}
    shown = []
    for number in sorted(numbers):
        if number not in plots or number not in rows:
            continue
        counts["plots"] += 1
        ours, theirs = plots[number]["proposed"], rows[number].get("typologyRev9")
        if ours is None:
            counts["typeScanNone"] += 1
        elif theirs is None:
            counts["typeTableNone"] += 1
        elif ours == theirs:
            counts["typeEqual"] += 1
        else:
            counts["typeDiffer"] += 1
            if len(shown) < SHOWN:
                shown.append({"plot": number, "scan": ours, "table": theirs})
        block = plots[number]["villaType"].get(block_source) if block_source else None
        footprint = scan.value_codes(rows[number].get("footprintBlock"), codes, split)
        if block is None or len(footprint) != 1:
            counts["blockNone"] += 1
        elif block == footprint[0]:
            counts["blockEqual"] += 1
        else:
            counts["blockDiffer"] += 1
    return dict(counts, differs=shown)


def table_check(result, table, codes, split, keys):
    """The plots and villa types of the scan against a plot table of button 2, and on the plots each answer key names."""
    proposed = proposed_of(result, "villaType")
    blocks = proposed_of(result, "villaBlock")
    block_source = None if blocks is None else blocks.split("|", 1)[0] + "|block|" + blocks.split("|", 1)[1]
    plots = {item["plot"]: dict(item, proposed=(item["villaType"] or {}).get(proposed) if proposed else None) for item in result.get("plots") or []}
    rows, twice = table_rows(table)
    report = {"plotsBoth": len(set(plots) & set(rows)), "onlyScan": len(set(plots) - set(rows)), "onlyTable": len(set(rows) - set(plots)),
              "tableLabelsTwice": len(twice), "villaTypeSource": proposed, "villaBlockSource": block_source,
              "all": type_counts(plots, rows, set(plots) | set(rows), block_source, codes, split)}
    if keys is None:
        report["keys"] = "UNKNOWN, no key reads given"
    else:
        report["keys"] = {code: dict(type_counts(plots, rows, numbers, block_source, codes, split), keyPlots=len(numbers),
                                     inScan=len(numbers & set(plots))) for code, numbers in keys.items()}
    return report


def key_plot_numbers(reads, pairs):
    """The plot numbers the links of the read of each answer key name, as key_join takes them. They are the numbers of the past model,
    which key_join holds apart from the baseplan number."""
    return {code: set(key_join.key_plots(key_reads.project_document(key_reads.load(Path(reads) / job / ("testhost_" + job + "_prototypes.json")))))
            for code, job in pairs}


def input_check(records):
    """D19. Each input of each guard record unchanged in the run and unchanged now against its record before the run."""
    found, unchanged, changed = [], 0, 0
    for record in records:
        for item in scan.load_json(record).get("inputs") or []:
            now = input_guard.mark(item["path"]) if Path(item["path"]).is_file() else {"size": None, "writeTimeUtc": None, "sha256": None}
            differs = ", ".join(part for part in (input_guard.change(item["before"], item["after"]), input_guard.change(item["before"], now)) if part)
            if differs or not item.get("unchanged", False):
                changed += 1
                found.append({"name": item["name"], "change": differs or "marked changed"})
            else:
                unchanged += 1
    return {"unchanged": unchanged, "changed": changed, "changes": found}


def main(argv=None):
    parser = argparse.ArgumentParser(description="R420, R427. The second checks of a scan run, written to scan_check.json.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--scan", required=True, help="scan.json of the worker")
    parser.add_argument("--scan-python", required=True, help="scan_python.json of python/scan.py")
    parser.add_argument("--cad-read", action="append", default=[], help="a scan_cad_<n>.json of the run")
    parser.add_argument("--plot-table", help="read_plotTable.json of a button 2 read")
    parser.add_argument("--reads", help="the reads folder of the answer keys")
    parser.add_argument("--key", action="append", type=key_reads.parse_pair, default=[], metavar="CODE=JOB")
    parser.add_argument("--prototypes", help="the prototype folder, for the codes of the footprint blocks")
    parser.add_argument("--record", action="append", default=[], help="a guard record of the inputs of the run")
    parser.add_argument("--out", required=True, help="the folder scan_check.json is written to")
    args = parser.parse_args(argv)
    worker, python = scan.load_json(args.scan), scan.load_json(args.scan_python)
    report = {"parity": parity(worker, python), "faces": face_counts([scan.load_json(path) for path in args.cad_read]), "inputs": input_check(args.record)}
    if args.plot_table:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        names = sorted(path.name for path in Path(args.prototypes).iterdir() if path.is_file()) if args.prototypes else []
        codes = scan.known_codes(profile, settings.get("prototypeNames"), names)
        keys = key_plot_numbers(args.reads, args.key) if args.reads and args.key else None
        report["plotTable"] = table_check(python, scan.load_json(args.plot_table), codes, profile.get("scan.tokenSplit"), keys)
    write_json(Path(args.out) / OUTPUT, report)
    for name, counts in report["parity"].items():
        print("parity %s: equal %d, differ %d, same order %s" % (name, counts["equal"], counts["differ"], counts["sameOrder"]))
    print("faces: equal %d, differ %d" % (report["faces"]["equal"], report["faces"]["differ"]))
    print("inputs: unchanged %d, changed %d" % (report["inputs"]["unchanged"], report["inputs"]["changed"]))
    if "plotTable" in report:
        table = report["plotTable"]
        print("plot table: both %d, only scan %d, only table %d, villa type equal %d, differ %d" % (
            table["plotsBoth"], table["onlyScan"], table["onlyTable"], table["all"]["typeEqual"], table["all"]["typeDiffer"]))
    failed = any(counts["differ"] or not counts["sameOrder"] for counts in report["parity"].values()) or report["inputs"]["changed"]
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
