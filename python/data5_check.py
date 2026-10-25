"""Second method for round DATA-5 (R260).

Each check works by other means than the code it checks, 20 items at each seed of dataBook.data5Check where items are
sampled. The area key: the PH_1-DLUS sheet is read again from the xlsx XML with zipfile (data3_check), not openpyxl, and
the zone and neighbourhood of each sampled plot are compared with plots.csv. The parcels: the parcel that holds the face
centroid is found again on the raw civil.csv lines by a ray cast (data4_check) and held against parcel_matches.csv. The
third count: the DXF of each road drawing is scanned line by line for the entity records of its ENTITIES section and its
BLOCK records, against the counts of the DXF parser. The IFC: each line that starts a record is matched by one regular
expression and the types counted, against ifc.json. The IM file names: the source root is listed with pathlib and each
name searched for -IMx- by plain string search, against im_codes.json. The curb: for 20 points per seed along the Roads
curb the distance to the Base Plans curb is worked out by brute force over every segment with numpy, against the far
test of road_layouts.py.
"""
import argparse
import json
import random
import re
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import c3d_connect
import c3d_run
import data3_check
import data4_check
import data_book
import data_book_analysis
import data_book_check
import dxf_count
import link_summary


def zone_of(value):
    digits = re.findall(r"\d+", data3_check.number_key(value) or "")
    return "Zone %d" % int(digits[0]) if digits else None


def check_area(sample, dlus):
    counts = Counter()
    for row in sample:
        numbers = [n for n in (row.get("locationPlanNumber") or "").split("|") if n]
        found = {(zone_of(d["zone"]), (data3_check.number_key(d["nbh"]) or "").upper()) for n in numbers for d in dlus.get(n, [])}
        found = {f for f in found if f[1] not in ("", "-", "0")}
        mine = (row.get("zone") or None, row.get("nbhId") or None)
        if not numbers:
            counts["noPlanNumber " + ("keyed" if mine[1] else "noKey")] += 1
        elif not found:
            counts["noDlusRow " + ("match" if not mine[1] else "mismatch")] += 1
        else:
            counts["match" if found == {mine} else "mismatch"] += 1
    return dict(counts)


def scan_dxf(path):
    """Entity records of the ENTITIES section by type, sub records left out, and the number of BLOCK records."""
    found, blocks = Counter(), 0
    section, previous = None, None
    with open(path, encoding="utf-8", errors="replace") as handle:
        lines = iter(handle)
        for code in lines:
            value = next(lines, "").strip()
            code = code.strip()
            if code == "2" and previous == "SECTION":
                section = value
            if code == "0":
                previous = value
                if value == "ENDSEC":
                    section = None
                if value == "BLOCK" and section == "BLOCKS":
                    blocks += 1
                if section == "ENTITIES" and value not in ("ENDSEC", "SECTION") and value not in dxf_count.SUB_TYPES:
                    found[value] += 1
    return found, blocks


def check_dxf(path):
    scanned, blocks = scan_dxf(path)
    parsed = dxf_count.parse(path)
    mine = Counter(e["type"] for e in parsed["entities"])
    kinds = set(scanned) | set(mine)
    return {"types": len(kinds), "typesEqual": sum(1 for k in kinds if scanned[k] == mine[k]), "differ": {k: [scanned[k], mine[k]] for k in kinds if scanned[k] != mine[k]},
            "blockRecords": blocks, "blocksParsed": len(parsed["blocks"])}


def ifc_part(settings, root, data5):
    """R260. The IFC types counted again against ifc.json, or skipped with the reason when the IFC input is retired (Q047)."""
    if settings["ifc"].get("retired"):
        return {"skipped": settings["ifc"]["retired"]}
    ifc = json.loads((data5 / settings["ifc"]["outFolder"] / "ifc.json").read_text(encoding="utf-8"))
    return check_ifc(root / "work" / settings["ifc"]["copy"], ifc)


def check_ifc(path, report):
    found = Counter()
    rule = re.compile(r"^#\d+\s*=\s*([A-Z0-9_]+)\(")
    with open(path, encoding="utf-8", errors="replace") as handle:
        for line in handle:
            match = rule.match(line)
            if match:
                found[match.group(1)] += 1
    kinds = set(found) | set(report["types"])
    return {"types": len(kinds), "typesEqual": sum(1 for k in kinds if found[k] == report["types"].get(k, 0)),
            "differ": {k: [found[k], report["types"].get(k, 0)] for k in kinds if found[k] != report["types"].get(k, 0)}}


def check_names(root, report):
    found = Counter()
    for path in Path(root).rglob("*"):
        if path.is_file():
            for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
                code = "IM" + letter
                if "-" + code + "-" in path.name or "_" + code + "_" in path.name or " " + code + " " in path.name:
                    found[code] += 1
    codes = set(found) | {c for c, v in report["codes"].items() if v["fileNames"]}
    return {code: [found[code], report["codes"].get(code, {}).get("fileNames", 0)] for code in sorted(codes)}


def segment_distance(points, starts, ends):
    """Least distance from each point to any segment, by brute force."""
    best = np.full(len(points), np.inf)
    direction = ends - starts
    length2 = (direction ** 2).sum(axis=1)
    length2[length2 == 0] = 1e-12
    for k, p in enumerate(points):
        t = np.clip(((p - starts) * direction).sum(axis=1) / length2, 0, 1)
        foot = starts + direction * t[:, None]
        best[k] = np.sqrt(((foot - p) ** 2).sum(axis=1)).min()
    return best


def check_curb(sample_points, lines_b, far):
    starts = np.array([c for line in lines_b for c in list(line.coords)[:-1]], dtype=float)[:, :2]
    ends = np.array([c for line in lines_b for c in list(line.coords)[1:]], dtype=float)[:, :2]
    brute = segment_distance(np.array(sample_points, dtype=float), starts, ends)
    tree = c3d_connect.LineSet(lines_b)
    counts = Counter()
    for (x, y), distance in zip(sample_points, brute):
        theirs, _ = tree.nearest(x, y)
        counts["match" if (distance > far) == (theirs > far) else "mismatch"] += 1
        counts["far" if distance > far else "near"] += 1
    return dict(counts)


def curb_points(lines, inside, count, rng):
    found = []
    while len(found) < count:
        line = lines[rng.randrange(len(lines))]
        point = line.interpolate(rng.uniform(0, line.length))
        if inside(point):
            found.append((point.x, point.y))
    return found


def run(config):
    book, connect = config["dataBook"], config["connect"]
    settings = book["data5"]
    check = book["data5Check"]
    root = Path(book["root"])
    data5 = root / settings["outFolder"]
    plots = data_book_analysis.read_csv(root / "plots.csv")
    unit_types = set(book["analysis"]["unitPlotTypes"])
    units = sorted((r for r in plots if r["clientType"] in unit_types), key=lambda r: int(r["baseplanLabel"]))
    area = settings["areaKey"]["dlus"]
    dlus = {}
    for row in data3_check.by_header(data3_check.xlsx_rows(root / "work" / book["rev9"]["workbooks"][area["workbook"]], area["sheet"]), area["headerRow"], area["columns"]):
        dlus.setdefault(data3_check.number_key(row["plot"]), []).append(row)
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])
    parcels = data4_check.raw_parcels(root / Path(book["drawings"][book["rev9"]["drawing"]["role"]]).stem / "civil.csv")
    by_label = {m["baseplanLabel"]: {"number": m["parcel"], "faceClass": m["faceClass"]} for m in data_book_analysis.read_csv(root / book["rev9"]["outFolder"] / "parcel_matches.csv") if m["parcel"]}
    border = data_book.border_polygon(root / "phase1_border.csv")
    layouts = settings["roadLayouts"]["layouts"]
    names = list(layouts)
    lines = {n: data_book.lines_of(data5 / settings["xrefs"]["outFolder"] / Path(book["drawings"][layouts[n]["role"]]).stem, layouts[n]["curbLayers"]) for n in names}
    result = {"seeds": {}, "dxf": {}, "ifc": None, "names": None}
    for seed in check["seeds"]:
        pick = lambda items: random.Random(seed).sample(items, min(check["size"], len(items)))
        result["seeds"]["seed%d" % seed] = {
            "area": check_area(pick(plots), dlus),
            "parcels": data4_check.check_parcels(pick(units), faces, parcels, by_label, {**book["rev9"]["parcels"], **book["data4Check"]}),
            "curb": check_curb(curb_points(lines[names[0]], border.contains, check["size"], random.Random(seed)), lines[names[1]], settings["roadLayouts"]["farM"])}
    for role in settings["counts"]["roles"]:
        path = data5 / settings["counts"]["dxfFolder"] / (Path(book["drawings"][role]).stem + ".dxf")
        result["dxf"][role] = check_dxf(path) if path.is_file() else "no DXF"
    result["ifc"] = ifc_part(settings, root, data5)
    result["names"] = check_names(config["sourceRoot"], json.loads((data5 / settings["areaKey"]["outFolder"] / "im_codes.json").read_text(encoding="utf-8")))
    (root / "logs" / "data5_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for round DATA-5 (R260)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    print(json.dumps(run(c3d_run.load_config(args.config)), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
