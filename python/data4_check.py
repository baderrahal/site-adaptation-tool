"""Second method for round DATA-4 (R250).

Each check works by other means than the code it checks. The parcels: for 20 unit plots at each seed of dataBook.data4Check
the raw civil.csv lines are read again with plain string splits, the parcel whose outline holds the face centroid by the ray
cast of data_book_check and is the smallest such parcel by its shoelace area is found (road and overlay parcels hold the
centroid too), and its number and its overlap over union with the face on a point grid are compared with
parcels.csv and the location plan number. The VL6 hand: every gate of each prototype is taken from the reader output of the
read step (the family instance rows, not the rooms step) and its share across the plot is worked out with plain arithmetic
against villa_hand.json. The best surface version: for 20 schedule plots per seed the road level of R173 is taken by brute
force on the surface of the plot and the band compared with surface_entry.csv.
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
import data3_check
import data_book
import data_book_analysis
import data_book_check
import data_book_retest
import data_book_retest_check
import landxml_reader
import link_summary
import project_profile


def raw_parcels(path):
    """Number and outline of every parcel in civil.csv, from the raw lines with plain splits."""
    found, current = [], None
    with open(path, encoding="utf-8") as handle:
        next(handle)
        for line in handle:
            if not line.startswith("parcel,"):
                continue
            head, _, value = line.rstrip("\n").rpartition(",")
            prop = head.rsplit(",", 1)[1]
            if current is None or prop in current:
                current = {}
                found.append(current)
            current[prop] = value
    parcels = []
    for p in found:
        if p.get("Number", "").isdigit() and p.get("outline"):
            ring = [tuple(float(v) for v in pair.split(" ")) for pair in p["outline"].split("|")]
            parcels.append((p["Number"], ring))
    return parcels


def box(ring):
    xs, ys = [p[0] for p in ring], [p[1] for p in ring]
    return min(xs), min(ys), max(xs), max(ys)


def shoelace(ring):
    return abs(sum(ring[k - 1][0] * ring[k][1] - ring[k][0] * ring[k - 1][1] for k in range(len(ring)))) / 2


def centroid(ring):
    area = cx = cy = 0.0
    for k in range(len(ring)):
        (x0, y0), (x1, y1) = ring[k - 1], ring[k]
        cross = x0 * y1 - x1 * y0
        area += cross
        cx += (x0 + x1) * cross
        cy += (y0 + y1) * cross
    return (cx / (3 * area), cy / (3 * area)) if area else ring[0]


def check_parcels(sample, faces, parcels, by_label, settings):
    counts = Counter()
    boxes = [(box(ring), number, ring) for number, ring in parcels]
    for row in sample:
        rings = faces.get(row["baseplanLabel"]) or []
        if len(rings) != 1:
            counts["noRing"] += 1
            continue
        face = [tuple(p) for p in rings[0]]
        x, y = centroid(face)
        holding = sorted(((shoelace(ring), number, ring) for (x0, y0, x1, y1), number, ring in boxes
                          if x0 <= x <= x1 and y0 <= y <= y1 and data_book_check.inside(ring, x, y)), key=lambda item: item[0])
        theirs = by_label.get(row["baseplanLabel"]) or {}
        if not holding:
            counts["parcel " + ("match" if not theirs.get("number") or theirs.get("faceClass") == "noFace" else "mismatch")] += 1
            continue
        _, number, ring = holding[0]
        counts["parcel " + ("match" if number == theirs.get("number") else "mismatch")] += 1
        plan = [n for n in (row["locationPlanNumber"] or "").split("|") if n]
        counts["planNumber " + ("same" if plan == [number] else "differs")] += 1
        iou = data3_check.grid_iou(face, ring, settings["gridM"])
        mine = "same" if iou >= settings["sameShareMin"] else "shapeDiffers" if iou >= settings["matchShareMin"] else "noFace"
        counts["class " + ("match" if mine == theirs.get("faceClass") else "mismatch")] += 1
    return dict(counts)


def check_hand(config, hand):
    """The gate share of each prototype from the reader rows, against villa_hand.json."""
    settings = config["dataBook"]["villaHand"]
    counts, gates = Counter(), {}
    for unit, path in settings["readerFiles"].items():
        document = json.loads(Path(path).read_text(encoding="utf-8"))["documents"][0]
        frame = hand["measures"][unit]["frame"]
        start = -frame["offset"][0] if frame["anchor"] == "start" else -frame["offset"][0] - frame["widthM"]
        rows = [r for r in document["prototypes"] if r.get("placement") == "placed" and settings["gateFamily"].lower() in (r.get("familyName") or "").lower()]
        shares = sorted({round((r["insertionPoint"][0] - start) / frame["widthM"], 3) for r in rows if r.get("insertionPoint")})
        theirs = sorted({g["share"] for g in hand["measures"][unit]["gates"]})
        gates[unit] = {"readerGates": len(rows), "shares": shares, "roomsStep": theirs}
        counts["gateShare " + ("match" if shares == theirs else "mismatch")] += 1
    sides = {u: {"left" if s < 0.5 else "right" for s in g["shares"]} for u, g in gates.items()}
    first, second = settings["compare"]
    mine = "other hand" if len(sides[first]) == 1 and len(sides[second]) == 1 and sides[first] != sides[second] else "same hand" if sides[first] == sides[second] else "open"
    counts["verdict " + ("match" if mine == hand["verdict"] else "mismatch")] += 1
    return {"counts": dict(counts), "gates": gates}


def run(config, settings_path):
    book, connect = config["dataBook"], config["connect"]
    check = book["data4Check"]
    root = Path(book["root"])
    rev9 = book["rev9"]
    plots = data_book_analysis.read_csv(root / "plots.csv")
    unit_types = set(book["analysis"]["unitPlotTypes"])
    units = sorted((r for r in plots if r["clientType"] in unit_types), key=lambda r: int(r["baseplanLabel"]))
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])
    tables = root / Path(book["drawings"][rev9["drawing"]["role"]]).stem
    parcels = raw_parcels(tables / "civil.csv")
    by_label = {r["baseplanLabel"]: {"number": r["number"], "faceClass": r["faceClass"]} for r in data_book_analysis.read_csv(root / rev9["outFolder"] / "parcels.csv") if r["baseplanLabel"]}
    hand = json.loads((root / book["villaHand"]["outFolder"] / "villa_hand.json").read_text(encoding="utf-8"))
    entry_items = data_book_analysis.read_csv(root / book["surfaceEntry"]["outFolder"] / "surface_entry.csv")
    entry_by_key = {(i["plot"], data_book_retest.STEEP): i["band"] for i in entry_items if i["surface"] == data_book_retest.ROAD}
    scheduled = sorted((r for r in plots if (r["unitRev6Source"] or "").startswith("schedule") and r["pastEntry"]), key=lambda r: int(r["baseplanLabel"]))
    settings = project_profile.load_settings(settings_path)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    _, earlier = data_book.folder_of(book, book["join"]["curb"]["drawing"], book["join"]["fallbackOutputs"])
    curbs = c3d_check.segments_of(json.loads((earlier / "pieces.json").read_text(encoding="utf-8"))["pieces"], book["join"]["curb"]["layers"])
    surfaces = {}
    for name in {r["roadLevelSurface"] for r in scheduled if r["roadLevelSurface"]}:
        folder = Path(connect["landxmlDir"]) / landxml_reader.folder_name(name)
        surfaces[name] = (np.load(folder / "points.npy"), np.load(folder / "faces.npy"))
    result = {"hand": check_hand(config, hand), "seeds": {}}
    for seed in check["seeds"]:
        pick = lambda items: random.Random(seed).sample(items, min(check["size"], len(items)))
        entry = data_book_retest_check.check_entry(pick(scheduled), faces, curbs, surfaces, entry_by_key, profile, book["retest"],
                                                   book["analysis"]["bandsM"], connect["snapM"])
        result["seeds"]["seed%d" % seed] = {"parcels": check_parcels(pick(units), faces, parcels, by_label, {**rev9["parcels"], **check}),
                                            "entry": {k: v for k, v in entry.items() if k.startswith(data_book_retest.STEEP)}}
    (root / "logs" / "data4_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for round DATA-4 (R250)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    print(json.dumps(run(c3d_run.load_config(args.config), args.settings), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
