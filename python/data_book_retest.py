"""The round DATA-2 tests on the data book (R170 to R172), written to retest.json and retest.md in the data book.

entry (R170): El @ Entry sits at the back edge of the ramp (Round 13), so each schedule plot gets the mean level of its two
front corners plus slope times ramp depth, at the lowest and the highest front slope of the profile and by typology, on the
snapped road level and on every surface level column of plots.csv. units (R171): the Rev 6 units on each baseplan face, from
the outlines of the footprint unit blocks, against the Rev 6 count and the Phase1 sheet. width (R172): what the width keys of
the profile measure, with the faces that hold one unit. Counts only here, the rows go to retest_entry.csv and units.csv.
"""
import argparse
import csv
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Point, Polygon
from shapely.strtree import STRtree

import c3d_run
import data_book
import data_book_analysis
import link_summary
import project_profile
import yard_rule

ROAD = "roadLevel"
STEEP = "byTypology"


def number(text):
    return data_book_analysis.number(text)


def ramp_depth(profile, unit):
    """The Rev 6 ramp depth of a unit in metres, from ramp.front_setback of the profile."""
    setbacks = profile.get("ramp.front_setback")
    return profile.metres("ramp.front_setback." + (unit if unit in setbacks else "default"))


def slopes(profile, steep_units, unit):
    """The slopes in percent each variant tests on a unit, named slope<percent> and byTypology."""
    low, high = profile.get("slopes_percent.front_min"), profile.get("slopes_percent.front_max")
    return {"slope%g" % low: low, "slope%g" % high: high, STEEP: high if unit in steep_units else low}


def surface_names(rows):
    """The snapped road level first, then every surface with a left and a right level column in plots.csv."""
    if not rows:
        return [ROAD]
    return [ROAD] + sorted(c[len("level_"):-len("_left")] for c in rows[0] if c.startswith("level_") and c.endswith("_left"))


def corner_levels(row, name):
    if name == ROAD:
        return number(row["roadLevelLeft"]), number(row["roadLevelRight"])
    return number(row.get("level_" + name + "_left")), number(row.get("level_" + name + "_right"))


def entry_rows(rows, profile, steep_units, names, bands):
    """One row per schedule plot, surface and variant with the predicted El @ Entry less the schedule value and its band."""
    found = []
    for row in rows:
        entry = number(row["pastEntry"])
        if entry is None or not (row["unitRev6Source"] or "").startswith("schedule"):
            continue
        unit = row["unitRev6"]
        depth = ramp_depth(profile, unit)
        for name in names:
            left, right = corner_levels(row, name)
            for variant, slope in slopes(profile, steep_units, unit).items():
                item = {"plot": row["officialNumber"], "neighbourhood": row["neighbourhood"], "unit": unit, "surface": name, "variant": variant,
                        "slopePercent": slope, "rampDepthM": depth, "difference": None, "band": "noLevel"}
                if left is not None and right is not None:
                    difference = (left + right) / 2 + slope / 100 * depth - entry
                    item.update({"difference": round(difference, 4), "band": data_book_analysis.band(difference, bands)})
                found.append(item)
    return found


def entry_summary(items):
    groups = defaultdict(list)
    for item in items:
        groups[(item["surface"], item["variant"])].append(item)
    result = defaultdict(dict)
    for (surface, variant), members in groups.items():
        diffs = [m["difference"] for m in members if m["difference"] is not None]
        result[surface][variant] = {"plots": len(members), **dict(Counter(m["band"] for m in members)),
                                    "medianDifference": round(statistics.median(diffs), 4) if diffs else None}
    return dict(result)


def unit_blocks(blocks, kinds, inside):
    """The unit blocks whose outline centre lies inside Phase 1. A unit block is named <kind>_<type>, Villa_VL4."""
    found = []
    for block in blocks:
        name = block["name"].split("$")[-1]
        outline = [p[:2] for p in block.get("outline") or []]
        if name.split("_")[0] not in kinds or len(outline) < 3:
            continue
        shape = Polygon(outline).buffer(0)
        if inside(shape.centroid):
            found.append({"name": name, "shape": shape})
    return found


def units_on_faces(faces, blocks, share_min):
    """Each face a unit block covers by at least share_min of the block area holds one unit of it."""
    labels = sorted(faces)
    tree = STRtree([faces[label] for label in labels])
    per_face = defaultdict(list)
    per_block = []
    for block in blocks:
        shape = block["shape"]
        hits = sorted(labels[k] for k in tree.query(shape) if faces[labels[k]].intersection(shape).area >= share_min * shape.area)
        per_block.append((block["name"], hits))
        for label in hits:
            per_face[label].append(block["name"])
    return per_face, per_block


def unit_count(per_face, per_block, types, unit_types):
    """Units by client type: one per face a block covers, and for a block on no face the types a block of its name covers most often."""
    by_name = defaultdict(lambda: {"blocks": 0, "onFaces": 0, "noFace": 0, "facesPerBlock": Counter(), "typesUnder": Counter()})
    for name, hits in per_block:
        entry = by_name[name]
        entry["blocks"] += 1
        entry["onFaces" if hits else "noFace"] += 1
        entry["facesPerBlock"][len(hits)] += 1
        entry["typesUnder"].update(types.get(label) or "none" for label in hits)
    on_face = Counter(types.get(label) or "none" for label, names in per_face.items() for _ in names)
    no_face = Counter()
    for name, entry in by_name.items():
        # The usual cover of a block of this name, a townhouse block covers two TH and two DP faces
        usual = Counter()
        cover = Counter(tuple(sorted(types.get(label) or "none" for label in hits)) for n, hits in per_block if n == name and hits)
        if cover:
            usual.update(cover.most_common(1)[0][0])
        entry["usualCover"] = dict(usual)
        for kind, count in usual.items():
            no_face[kind] += count * entry["noFace"]
    faces_by_units = defaultdict(Counter)
    for label, kind in types.items():
        if kind in unit_types:
            faces_by_units[kind][len(per_face.get(label, []))] += 1
    total = Counter(on_face) + no_face
    return {"blocksByName": {n: {k: (dict(v) if isinstance(v, Counter) else v) for k, v in e.items()} for n, e in sorted(by_name.items())},
            "facesByUnitCount": {k: dict(sorted(v.items())) for k, v in sorted(faces_by_units.items())},
            "unitsOnFaces": dict(on_face), "unitsOnNoFace": dict(no_face), "unitsByClientType": dict(total), "unitTotal": sum(total.values())}


def phase1_sheet(path, sheet):
    """The plot count of each typology row of the Phase1 sheet, with the client types its label names in brackets."""
    groups, total = {}, None
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            label = (row.get(sheet["typologyColumn"]) or "").strip()
            if label in sheet["labels"]:
                inner = re.search(r"\(([^)]*)\)", label)
                groups[label] = {"types": [t.strip() for t in inner.group(1).split(",")] if inner else [], "count": int(float(row[sheet["countColumn"]]))}
            if (row.get(sheet["totalColumn"]) or "").strip() == sheet["totalLabel"]:
                total = int(float(row[sheet["countColumn"]]))
    return groups, total


def kind_of(profile, code):
    typologies = profile.get("typologies")
    if code == "C10":
        return "C10"
    kinds = sorted({typologies[part]["kind"] for part in code.split("/") if part in typologies})
    return " or ".join(kinds) if kinds else "unknown"


def unit_gap(units, sheet_groups, sheet_total, manual, profile):
    """The footprint units against the Phase1 sheet by its rows, and against Rev 6 by kind, C10 apart as the sheet keeps it."""
    by_type = units["unitsByClientType"]
    sheet = {}
    for label, group in sheet_groups.items():
        found = sum(by_type.get(t, 0) for t in group["types"])
        sheet[label] = {"footprintUnits": found, "sheet": group["count"], "gap": found - group["count"]}
    manual_kind = Counter()
    for code, count in manual["byCode"].items():
        manual_kind[kind_of(profile, code)] += count
    foot_kind = Counter()
    for label, group in sheet_groups.items():
        kinds = {kind_of(profile, t) for t in group["types"]} - {"unknown"}
        foot_kind[" or ".join(sorted(kinds))] += sheet[label]["footprintUnits"]
    kinds = sorted(set(manual_kind) | set(foot_kind))
    return {"total": {"footprintUnits": units["unitTotal"], "unitsRev6": sum(manual["byCode"].values()), "phase1Sheet": sheet_total,
                      "gapToUnitsRev6": units["unitTotal"] - sum(manual["byCode"].values()), "gapToSheet": units["unitTotal"] - sheet_total if sheet_total else None},
            "bySheetRow": sheet,
            "byKind": {k: {"footprintUnits": foot_kind.get(k, 0), "unitsRev6": manual_kind.get(k, 0), "gap": foot_kind.get(k, 0) - manual_kind.get(k, 0)} for k in kinds}}


def schedule_cross(rows):
    """Client plot type against the Rev 6 unit code of the 747 schedule plots, the only plots that have both."""
    return {"%s>%s" % key: n for key, n in sorted(Counter((r["clientType"], r["unitRev6"]) for r in rows if r["unitRev6"]).items())}


def width_meaning(profile, faces_by_units):
    typologies = profile.get("typologies")
    keys = sorted({key for entry in typologies.values() for key in entry if "width" in key})
    plot_keys = sorted("plot." + key for key in profile.get("plot") if "width" in key)
    counts = Counter()
    for found in faces_by_units.values():
        for units, n in found.items():
            counts["one" if units == 1 else "none" if units == 0 else "more"] += n
    return {"typologyWidthKeys": keys, "plotWidthKeys": plot_keys, "unitFacesByUnits": dict(counts)}


def write_rows(path, items, columns):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, lineterminator="\n")
        writer.writeheader()
        writer.writerows(items)


def markdown(report):
    lines = ["# Data book retest", "", "Generated by python/data_book_retest.py (R170 to R172). Counts only, the rows are in retest_entry.csv and units.csv.", ""]
    for key, value in report.items():
        lines += ["## " + key, "", "```", json.dumps(value, indent=1, ensure_ascii=False), "```", ""]
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Round DATA-2 tests on the data book (R170 to R172)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book = config["dataBook"]
    retest = book["retest"]
    root = Path(book["root"])
    rows = data_book_analysis.read_csv(root / "plots.csv")
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    items = entry_rows(rows, profile, set(retest["steepUnits"]), surface_names(rows), book["analysis"]["bandsM"])
    write_rows(root / "retest_entry.csv", items, list(items[0]) if items else ["plot"])
    border = data_book.border_polygon(root / "phase1_border.csv")
    plots = link_summary.load(config["connect"]["baseplanPlots"])["plots"]
    phase1 = {r["baseplanLabel"]: r for r in rows}
    faces = {label: face for label, face in yard_rule.face_shapes(plots, 0)[0].items() if label in phase1}
    types = {label: phase1[label]["clientType"] for label in faces}
    _, earlier = data_book.folder_of(book, book["join"]["footprint"]["drawing"], book["join"]["fallbackOutputs"])
    blocks = unit_blocks(json.loads((earlier / "blocks.json").read_text(encoding="utf-8"))["blocks"], set(retest["unitBlockKinds"]), border.contains)
    per_face, per_block = units_on_faces(faces, blocks, retest["unitShareMin"])
    units = unit_count(per_face, per_block, types, set(book["analysis"]["unitPlotTypes"]))
    write_rows(root / "units.csv", [{"baseplanLabel": label, "clientType": types[label], "units": len(per_face.get(label, [])), "blocks": "|".join(per_face.get(label, []))}
                                    for label in sorted(faces, key=int)], ["baseplanLabel", "clientType", "units", "blocks"])
    sheet_groups, sheet_total = phase1_sheet(root / "master_plan_excel" / retest["phase1Sheet"]["file"], retest["phase1Sheet"])
    report = {
        "entry": entry_summary(items),
        "units": units,
        "unitGap": unit_gap(units, sheet_groups, sheet_total, retest["unitsRev6ByCode"], profile),
        "scheduleClientAgainstRev6": schedule_cross(rows),
        "widthMeaning": width_meaning(profile, units["facesByUnitCount"]),
    }
    (root / "retest.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    (root / "retest.md").write_text(markdown(report), encoding="utf-8")
    for key in ("unitGap", "widthMeaning"):
        print(key, json.dumps(report[key], ensure_ascii=False))
    for surface, variants in report["entry"].items():
        print("entry", surface, json.dumps(variants))
    return 0


if __name__ == "__main__":
    sys.exit(main())
