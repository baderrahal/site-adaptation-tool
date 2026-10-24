"""R281. The second method of round 17, with json, csv and math only and none of the helpers of the main method.

Seeds 14 and 7, 20 plots each:
1 past_all.csv against the raw reader output of its key: the link whose shared origin sits at the row's X and Y holds the
  row's file and mirror, and the Generic floor that holds the row's Generic outline centre by a ray cast of its own has the
  row's area by the shoelace formula and its FFL as the top less the survey elevation of the key.
2 the join of R270: each plot of entry_gaps.json and 20 plots both keys hold, the face that holds the centre of each key's
  Generic outline by a ray cast on the raw baseplan vertices, its location plan number, and whether the gap is over the limit.
3 each coded rule against its counts: the holds of the coded candidate equal its plots less its misses, and the misses of a
  decision named in the override list are the overrides of that decision. For 20 plots the Parsons ramp class of the coded
  typology rule and the step count of the height bands are worked out again.
4 the split check of the round 16 file against the counts of round 16.
5 the rebuilt file against the key: the link origin gap, the mirror and the link Z against the key FFL, worked out from the
  raw reader outputs, in the bands of the split check.
"""
import argparse
import csv
import json
import math
import random
import sys
from collections import Counter
from pathlib import Path

SEEDS = (14, 7)
COUNT = 20
EDGE_MM = 1e-6
# The plan outline is rounded to the mm by R2, so its area may differ from the raw outline by a few hundredths of a m2
AREA_M2 = 0.05


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def project(data):
    return next(document for document in data["documents"] if document.get("kind") == "project")


def shift(document):
    survey = document["surveyPoint"]
    return [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(3)]


def shoelace(ring):
    return abs(sum(ring[i][0] * ring[(i + 1) % len(ring)][1] - ring[(i + 1) % len(ring)][0] * ring[i][1] for i in range(len(ring)))) / 2.0


def centre(ring):
    return sum(point[0] for point in ring) / len(ring), sum(point[1] for point in ring) / len(ring)


def inside(point, ring):
    """A ray cast to the right, the point is inside when it crosses an odd number of edges."""
    x, y = point
    found = False
    for i in range(len(ring)):
        (x1, y1), (x2, y2) = ring[i][:2], ring[(i + 1) % len(ring)][:2]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            found = not found
    return found


def wkt_ring(text):
    """The outer ring of a POLYGON or the first part of a MULTIPOLYGON well known text, as points."""
    body = text[text.index("(") :].lstrip("(")
    first = body.split(")")[0]
    return [tuple(float(value) for value in pair.split()) for pair in first.split(",")]


def band(gap_mm, bands):
    if gap_mm is None:
        return "notCompared"
    if gap_mm <= 0.001:
        return "equal"
    for limit in bands[1:]:
        if gap_mm <= limit + EDGE_MM:
            return "within%gmm" % limit
    return "worse"


def read_rows(path):
    with open(path, encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def number(text):
    return None if text in (None, "") else float(text)


def check_table(rows, keys, seed):
    """Part 1."""
    picked = random.Random(seed).sample(rows, COUNT)
    results = []
    for row in picked:
        document = keys[row["key"]]
        offset = shift(document)
        hit = [link for link in document["links"] if abs(link["transform"]["origin"][0] + offset[0] - float(row["x"])) <= 0.0015
               and abs(link["transform"]["origin"][1] + offset[1] - float(row["y"])) <= 0.0015]
        # Two links may share one origin in a key, the row's link must be one of them
        link_ok = any(link["file"] == row["linkFile"] and str(link["transform"]["mirrored"]) == row["mirrored"] for link in hit)
        ring = wkt_ring(row["genericOutline"]) if row["genericOutline"] else None
        floor_ok = None
        if ring:
            point = centre(ring)
            floors = [floor for floor in document["floors"] if floor.get("outline") and floor.get("topElevationRange") and inside(point, floor["outline"][0])
                      and abs(shoelace(floor["outline"][0]) - float(row["genericAreaM2"])) <= AREA_M2]
            datum = float(row["datumM"])
            floor_ok = any(abs(floor["topElevationRange"]["max"] - datum - float(row["ffl"])) <= 0.001 for floor in floors)
        results.append({"key": row["key"], "plot": row["plot"], "link": link_ok, "floor": floor_ok, "match": link_ok and floor_ok is not False})
    return results


def check_join(gaps, rows, faces, numbers, limit, seed):
    """Part 2."""
    by = {}
    for row in rows:
        by.setdefault(row["plot"], {})[row["key"]] = row
    shared = sorted(plot for plot, held in by.items() if "imc" in held and "u14" in held and held["imc"]["entry"] and held["u14"]["entry"])
    listed = {item["plot"] for item in gaps["pairs"][0]["plots"]}
    picked = sorted(set(random.Random(seed).sample(shared, COUNT)) | listed)
    results = []
    for plot in picked:
        held = by[plot]
        found = []
        for name in ("imc", "u14"):
            point = centre(wkt_ring(held[name]["genericOutline"]))
            labels = [label for label, ring in faces.items() if inside(point, ring)]
            found.append(numbers.get(labels[0]) if len(labels) == 1 else None)
        over = abs(float(held["u14"]["entry"]) - float(held["imc"]["entry"])) > limit
        results.append({"plot": plot, "faceNumbers": found, "over": over, "listed": plot in listed, "match": found == [plot, plot] and over == (plot in listed)})
    return results


def check_rules(report, overrides, rows, profile_bands, classes, seed):
    """Part 3."""
    results = []
    counted = Counter(item["decision"] for item in overrides["overrides"])
    for decision in report["decisions"]:
        test = decision["tests"][decision["coded"]]
        holds_ok = test["holds"] == decision["plots"] - decision["misses"]
        listed = counted.get(decision["decision"])
        results.append({"decision": decision["decision"], "holds": holds_ok, "overrides": listed is None or listed == decision["misses"],
                        "match": holds_ok and (listed is None or listed == decision["misses"])})
    rule = next(decision for decision in report["decisions"] if decision["decision"] == "parsons rampSlope")["tests"]["typology"]["rule"]
    imc = [row for row in rows if row["key"] == "imc"]
    picked = random.Random(seed).sample(imc, COUNT)
    for row in picked:
        guess = rule.get("parsons | " + row["typologyRev9"])
        aim = classes.get(row["rampType"])
        steps = next((str(value) for limit, value in profile_bands if number(row["height"]) is not None and number(row["height"]) < limit), None)
        results.append({"plot": row["plot"], "rampClassRuleHolds": guess == aim, "stepsFromHeight": steps == row["steps"], "match": steps == row["steps"] and guess is not None})
    return results


def check_split(split, round16):
    """Part 4. The bands of the split check of the round 16 file against the counts round 16 wrote."""
    position = split["position"]["bands"]
    level = split["level"]["againstKeyFfl"]
    old = round16["counts"]
    return [{"part": "position", "match": position == old["position"], "split": position, "round16": old["position"]},
            {"part": "level", "match": level == old["zAgainstFfl"], "split": level, "round16": old["zAgainstFfl"]}]


def check_rebuilt(new, key, plan, bands, split, seed):
    """Part 5, with the bands of the split check of R261 on the same plots."""
    rows = {row["plot"]: row for row in split["rows"]}
    new_doc, key_doc = project(new), project(key)
    new_off, key_off = shift(new_doc), shift(key_doc)
    plot_parameter = "RRE_ZZ_SP_Plot-Number"

    def by_plot(document):
        found = {}
        for link in document["links"]:
            plot = str((link.get("parameters") or {}).get(plot_parameter) or "")
            if plot:
                found.setdefault(plot, []).append(link)
        return found

    mine, theirs = by_plot(new_doc), by_plot(key_doc)
    key_ffl = {entry["plot"]: entry["levels"]["ffl"]["value"] for entry in plan["plots"]}
    plots = sorted(plot for plot in mine if len(mine[plot]) == 1 and len(theirs.get(plot, [])) == 1)
    results = []
    for plot in random.Random(seed).sample(plots, min(COUNT, len(plots))):
        a, b = mine[plot][0]["transform"], theirs[plot][0]["transform"]
        gap = math.hypot(a["origin"][0] + new_off[0] - b["origin"][0] - key_off[0], a["origin"][1] + new_off[1] - b["origin"][1] - key_off[1]) * 1000.0
        z = abs(a["origin"][2] + new_off[2] - key_ffl[plot]) * 1000.0
        main = rows.get(plot, {})
        same = band(main.get("positionMm"), bands) == band(gap, bands) and band(main.get("levelMm"), bands) == band(z, bands)
        results.append({"plot": plot, "position": band(gap, bands), "level": band(z, bands), "mirroredEqual": a["mirrored"] == b["mirrored"], "sameAsSplit": same})
    return results


def main(argv=None):
    parser = argparse.ArgumentParser(description="The second method of round 17 (R281).")
    parser.add_argument("--table", required=True)
    parser.add_argument("--key", nargs=2, action="append", metavar=("NAME", "PROTOTYPES"), required=True)
    parser.add_argument("--gaps", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--labels", required=True)
    parser.add_argument("--majority", required=True)
    parser.add_argument("--overrides", required=True)
    parser.add_argument("--split-i16", required=True)
    parser.add_argument("--compare-i16", required=True)
    parser.add_argument("--new", required=True, help="the reader output of the rebuilt file")
    parser.add_argument("--key-plan", required=True, help="the plan of the IMC key, its FFL")
    parser.add_argument("--split-new", required=True, help="the split check of the rebuilt file")
    parser.add_argument("--settings", required=True)
    parser.add_argument("--profile", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    rows = read_rows(args.table)
    keys = {name: project(load(path)) for name, path in args.key}
    settings, profile = load(args.settings), load(args.profile)
    bands = settings["placerCheck"]["position"]["bandsMm"]
    step_bands = profile["levelProfiles"]["parsons2024Rev6"]["stepBands"]
    faces = {}
    for entry in load(args.baseplan)["plots"]:
        if entry.get("kind") == "plot_shape" and str(entry.get("plot", "")).isdigit() and len(entry.get("vertices") or []) >= 3:
            faces[str(entry["plot"])] = [tuple(vertex[:2]) for vertex in entry["vertices"]]
    face_rows = {}
    for row in read_rows(args.labels):
        if row.get("face"):
            face_rows.setdefault(row["face"], []).append(row["number"])
    numbers = {face: found[0] for face, found in face_rows.items() if len(found) == 1}
    gaps, report, overrides = load(args.gaps), load(args.majority), load(args.overrides)
    key_imc = next(path for name, path in args.key if name == "imc")
    result = {"seeds": {}}
    for seed in SEEDS:
        part = {
            "table": check_table(rows, keys, seed),
            "join": check_join(gaps, rows, faces, numbers, settings["majority"]["entryGaps"]["entryGapM"], seed),
            "rules": check_rules(report, overrides, rows, step_bands, settings["majority"]["rampTypeClass"], seed),
            "split": check_split(load(args.split_i16), load(args.compare_i16)),
            "rebuilt": check_rebuilt(load(args.new), load(key_imc), load(args.key_plan), bands, load(args.split_new), seed),
        }
        result["seeds"][str(seed)] = part
        print("seed %d: table %d of %d, join %d of %d, rules %d of %d, split %d of %d, rebuilt bands %s, mirror %d of %d" % (
            seed, sum(1 for row in part["table"] if row["match"]), len(part["table"]), sum(1 for row in part["join"] if row["match"]), len(part["join"]),
            sum(1 for row in part["rules"] if row["match"]), len(part["rules"]), sum(1 for row in part["split"] if row["match"]), len(part["split"]),
            dict(Counter(row["position"] for row in part["rebuilt"])), sum(1 for row in part["rebuilt"] if row["mirroredEqual"]), len(part["rebuilt"])))
        print("  rebuilt bands as the split check %d of %d" % (sum(1 for row in part["rebuilt"] if row["sameAsSplit"]), len(part["rebuilt"])))
    with open(args.out, "w", encoding="utf-8") as handle:
        json.dump(result, handle, indent=1)
    return 0


if __name__ == "__main__":
    sys.exit(main())
