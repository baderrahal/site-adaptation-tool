"""R270. The plots whose El @ Entry differs between two past keys by more than majority.entryGapM, each with its join checked.

Per plot and pair of keys: the location plan number of the baseplan face that holds the Generic floor of each key, the overlap
of the two Generic floors, the schedule row of the plot where a key has one, the El @ Entry of each key against the back of its
own ramp, and the gap of the nearest plots both keys hold. The verdict is wrongJoin when a Generic floor stands on the face of
another number or the two floors do not overlap, keyParameterFault when one key's El @ Entry parameter is off its own ramp back
by more than entryRampM while the ramp backs of the two keys sit within entryGapM, else realLevelDifference.
Counts and plot numbers only are printed, the report goes outside the repo.
"""
import argparse
import math
import sys
from collections import Counter, defaultdict

from shapely import wkt
from shapely.strtree import STRtree

import common
import excel_reader
import link_summary
import past_all
import plan_from_past
import plot_relations
import project_profile
import yard_rule

NEIGHBOURS = 4


def face_number(shape, tree, labels, numbers):
    """The location plan number of the one face that holds the representative point of the shape, or None."""
    hits = [labels[index] for index in tree.query(shape.representative_point(), predicate="within")]
    return numbers.get(hits[0]) if len(hits) == 1 else None


def schedule_entries(path, sheet):
    """El @ Entry of each plot of a past schedule, by plot number, from the column whose header starts with El @ Entry."""
    rows = excel_reader.read_sheet(path, sheet)[3]
    found = {}
    for row in rows:
        column = next((key for key in row if str(key).startswith("El @ Entry")), None)
        if row.get("Plot No.") is not None and column and plan_from_past.is_number(row[column]):
            found[str(row["Plot No."])] = float(row[column])
    return found


def overlap(a, b):
    if a is None or b is None:
        return None
    union = a.union(b).area
    return a.intersection(b).area / union if union > 0 else None


def check_pair(rows, first, second, faces, numbers, schedules, block):
    """R270. Every plot both keys hold with El @ Entry, the gaps over the limit with their checks and verdict."""
    by = defaultdict(dict)
    for row in rows:
        by[row["plot"]][row["key"]] = row
    shared = {plot: keys for plot, keys in by.items() if first in keys and second in keys and keys[first]["entry"] is not None and keys[second]["entry"] is not None}
    labels = sorted(faces, key=link_summary.sort_key)
    tree = STRtree([faces[label] for label in labels])
    centres = {}
    for plot, keys in shared.items():
        outline = keys[first]["genericOutline"] or keys[second]["genericOutline"]
        if outline:
            point = wkt.loads(outline).representative_point()
            centres[plot] = (point.x, point.y)
    gaps = {plot: keys[second]["entry"] - keys[first]["entry"] for plot, keys in shared.items()}
    found = []
    for plot in sorted(shared, key=link_summary.sort_key):
        if abs(gaps[plot]) <= block["entryGapM"]:
            continue
        keys = shared[plot]
        checks = {}
        shapes = {}
        for name in (first, second):
            row = keys[name]
            shapes[name] = wkt.loads(row["genericOutline"]) if row["genericOutline"] else None
            checks[name + " faceNumber"] = None if shapes[name] is None else face_number(shapes[name], tree, labels, numbers)
            own = None if row["rampBack"] is None else row["entry"] - row["rampBack"]
            checks[name + " entryLessOwnRampTop"] = None if own is None else round(own, 4)
            if name in schedules:
                checks[name + " scheduleEntry"] = schedules[name].get(plot)
        checks["genericOverlap"] = None if overlap(shapes[first], shapes[second]) is None else round(overlap(shapes[first], shapes[second]), 4)
        tops = [keys[name]["rampBack"] for name in (first, second)]
        checks["rampBackGap"] = None if None in tops else round(tops[1] - tops[0], 4)
        near = sorted((math.dist(centres[plot], centres[other]), other) for other in centres if other != plot) if plot in centres else []
        checks["neighbourGaps"] = [{"plot": other, "gapM": round(gaps[other], 4)} for _, other in near[:NEIGHBOURS]]
        found.append({"plot": plot, "typologyRev9": keys[first]["typologyRev9"], "gapM": round(gaps[plot], 4), "checks": checks,
                      "verdict": verdict(plot, checks, first, second, block)})
    return {"pair": first + "+" + second, "shared": len(shared), "overLimit": len(found), "verdicts": dict(Counter(item["verdict"] for item in found)), "plots": found}


def verdict(plot, checks, first, second, block):
    numbers = [checks[name + " faceNumber"] for name in (first, second)]
    if any(number != plot for number in numbers) or checks["genericOverlap"] is None or checks["genericOverlap"] < block["sameFaceShare"]:
        return "wrongJoin"
    faults = [name for name in (first, second) if checks[name + " entryLessOwnRampTop"] is not None and abs(checks[name + " entryLessOwnRampTop"]) > block["entryRampM"]]
    if faults and checks["rampBackGap"] is not None and abs(checks["rampBackGap"]) <= block["entryGapM"]:
        return "keyParameterFault"
    return "realLevelDifference"


def load_block(settings):
    block = (settings.get("majority") or {}).get("entryGaps")
    if not isinstance(block, dict) or not all(plan_from_past.is_number(block.get(key)) for key in ("entryGapM", "entryRampM", "sameFaceShare")):
        raise project_profile.ProfileError(settings.path.name + " majority.entryGaps must give entryGapM, entryRampM and sameFaceShare")
    return block


def main(argv=None):
    parser = argparse.ArgumentParser(description="El @ Entry gaps between two past keys, with their join checked (R270).")
    parser.add_argument("table", help="past_all.csv")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--labels", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--schedule", nargs=2, action="append", metavar=("KEY", "XLSX"), default=[], help="the schedule of a key, read on its pastJob sheet")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    block = load_block(settings)
    sheet = plan_from_past.load_job(settings)["sheet"]
    plot_relations.refuse_inside_repo(__import__("pathlib").Path(args.out).resolve().parent)
    rows = past_all.read_table(args.table)
    numbers, _ = plan_from_past.location_numbers(args.labels)
    faces, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    schedules = {name: schedule_entries(path, sheet) for name, path in args.schedule}
    held = defaultdict(set)
    for row in rows:
        if row["entry"] is not None:
            held[row["plot"]].add(row["key"])
    keys = sorted({row["key"] for row in rows})
    pairs = [(a, b) for index, a in enumerate(keys) for b in keys[index + 1:] if any(a in found and b in found for found in held.values())]
    result = {"limitM": block["entryGapM"], "pairs": [check_pair(rows, a, b, faces, numbers, schedules, block) for a, b in pairs]}
    common.write_json(args.out, result)
    for pair in result["pairs"]:
        print("%s: shared %d, over %.2f m %d, %s" % (pair["pair"], pair["shared"], block["entryGapM"], pair["overLimit"], pair["verdicts"]))
        for item in pair["plots"]:
            print("  %s %s gap %.3f m %s" % (item["plot"], item["typologyRev9"], item["gapM"], item["verdict"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
