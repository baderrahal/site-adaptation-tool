"""Second method for round DATA-3 (R198).

Each check works by other means than the code it checks, on 20 plots drawn at each seed of dataBook.data3Check:
the DLUS setback join and the land width compare read the workbooks as xlsx XML with zipfile, not openpyxl, and join to
plots.csv by a parse of their own. The Rev 9 face match counts the overlap over union on a point grid with a ray cast of its
own, not shapely, and puts the Rev 9 label on the face the same way. The best El @ Entry variant takes the road level by brute
force on the surface of each plot, through the R173 check, and compares the band with entry_terms.csv.
"""
import argparse
import json
import random
import re
import sys
import zipfile
from collections import Counter
from pathlib import Path
from xml.etree import ElementTree

import numpy as np

import c3d_check
import c3d_run
import data_book
import data_book_analysis
import data_book_check
import data_book_retest
import data_book_retest_check
import landxml_reader
import link_summary
import project_profile

MAIN = "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}"
REL = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}"
PACKAGE = "{http://schemas.openxmlformats.org/package/2006/relationships}"


def column_index(reference):
    letters = re.match(r"[A-Z]+", reference).group(0)
    index = 0
    for letter in letters:
        index = index * 26 + ord(letter) - 64
    return index - 1


def xlsx_rows(path, sheet):
    """The rows of one sheet as lists by column, read from the xlsx XML: shared strings, inline strings and numbers."""
    with zipfile.ZipFile(path) as book:
        names = set(book.namelist())
        strings = []
        if "xl/sharedStrings.xml" in names:
            for item in ElementTree.fromstring(book.read("xl/sharedStrings.xml")).iter(MAIN + "si"):
                strings.append("".join(t.text or "" for t in item.iter(MAIN + "t")))
        workbook = ElementTree.fromstring(book.read("xl/workbook.xml"))
        rid = next(s.get(REL + "id") for s in workbook.iter(MAIN + "sheet") if s.get("name") == sheet)
        rels = ElementTree.fromstring(book.read("xl/_rels/workbook.xml.rels"))
        target = next(r.get("Target") for r in rels.iter(PACKAGE + "Relationship") if r.get("Id") == rid)
        target = target.lstrip("/")
        target = target if target.startswith("xl/") else "xl/" + target
        rows = {}
        with book.open(target) as handle:
            for _, element in ElementTree.iterparse(handle):
                if element.tag != MAIN + "row":
                    continue
                values = {}
                for cell in element.iter(MAIN + "c"):
                    kind, raw = cell.get("t"), cell.find(MAIN + "v")
                    if kind == "s" and raw is not None:
                        value = strings[int(raw.text)]
                    elif kind == "inlineStr":
                        value = "".join(t.text or "" for t in cell.iter(MAIN + "t"))
                    elif raw is not None and kind in (None, "n"):
                        value = float(raw.text)
                    else:
                        value = raw.text if raw is not None else None
                    values[column_index(cell.get("r"))] = value
                rows[int(element.get("r"))] = values
                element.clear()
    return rows


def by_header(rows, header_row, wanted):
    """Every row under header_row as a dict of the wanted header names."""
    header = {str(v).strip(): k for k, v in rows.get(header_row, {}).items() if v is not None}
    at = {name: header[column] for name, column in wanted.items()}
    return [{name: row.get(k) for name, k in at.items()} for number, row in sorted(rows.items()) if number > header_row]


def number_key(value):
    if isinstance(value, float) and value.is_integer():
        return str(int(value))
    return str(value).strip() if value is not None else None


def to_float(value):
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def one_number(text):
    parts = [p for p in (text or "").split("|") if p]
    return parts[0] if len(parts) == 1 else None


def check_setbacks(sample, dlus, plots_by_number, settings, tolerance):
    counts = Counter()
    for row in sample:
        item = dlus.get(row["plot"])
        plot = plots_by_number.get(row["plot"])
        front = to_float(item["front"]) if item else None
        mine = to_float(row["front"])
        counts["front " + ("match" if front is not None and mine is not None and abs(front - mine) <= 1e-6 else "mismatch")] += 1
        drawn = to_float(plot["setbackFromFrontM"]) if plot else None
        counts["drawn " + ("match" if drawn == to_float(row["drawnFront"]) else "mismatch")] += 1
        cls = "none" if drawn is None else next(("%.2f" % line for line in settings["setbackLinesM"] if abs(drawn - line) <= tolerance), "other")
        counts["drawnClass " + ("match" if cls == row["drawnClass"] else "mismatch")] += 1
    return dict(counts)


def check_widths(sample, sales, plots_by_number, tolerance):
    counts = Counter()
    for row in sample:
        item = sales.get(row["plot"])
        plot = plots_by_number.get(row["plot"])
        width, length = (to_float(item["landWidth"]), to_float(item["landLength"])) if item else (None, None)
        counts["landWidth " + ("match" if width == to_float(row["landWidth"]) else "mismatch")] += 1
        counts["landLength " + ("match" if length == to_float(row["landLength"]) else "mismatch")] += 1
        counts["typology " + ("match" if item and item["typology"] == row["typologyRev9"] else "mismatch")] += 1
        cad = to_float(plot["widthM"]) if plot else None
        mine = "noValue" if width is None or cad is None else "equal" if abs(width - cad) <= tolerance else "differs"
        counts["widthAgainstCad " + ("match" if mine == row["widthAgainstCad"] else "mismatch")] += 1
    return dict(counts)


def grid_iou(ring, other, step):
    """Overlap over union of two rings from the points of a grid over both boxes, with the ray cast of data_book_check."""
    xs, ys = [p[0] for p in ring + other], [p[1] for p in ring + other]
    either = both = 0
    y = min(ys) + step / 2
    while y < max(ys):
        x = min(xs) + step / 2
        while x < max(xs):
            a, b = data_book_check.inside(ring, x, y), data_book_check.inside(other, x, y)
            either += a or b
            both += a and b
            x += step
        y += step
    return both / either if either else 0.0


def check_faces(sample, faces, outlines, labels, settings):
    counts = Counter()
    for row in sample:
        rings = faces.get(row["baseplanLabel"]) or []
        outline = outlines.get(row["baseplanLabel"])
        if len(rings) != 1 or outline is None:
            counts["noRing"] += 1
            continue
        ring = [tuple(p) for p in rings[0]]
        iou = grid_iou(ring, [tuple(p) for p in outline], settings["gridM"])
        theirs = to_float(row["iou"])
        counts["iou " + ("match" if theirs is not None and abs(iou - theirs) <= settings["iouSlack"] else "mismatch")] += 1
        mine = "same" if iou >= settings["sameShareMin"] else "shapeDiffers" if iou >= settings["matchShareMin"] else "noFace"
        near = theirs is not None and abs(theirs - settings["sameShareMin"]) <= settings["iouSlack"]
        counts["class " + ("match" if mine == row["faceClass"] else "nearLimit" if near else "mismatch")] += 1
        held = [l["number"] for l in labels if data_book_check.inside(ring, float(l["east"]), float(l["north"]))]
        number = held[0] if len(held) == 1 else None
        counts["number " + ("match" if number == (row["rev9Number"] or None) else "mismatch")] += 1
    return dict(counts)


def run(config, settings_path):
    book, connect = config["dataBook"], config["connect"]
    check = book["data3Check"]
    rev9 = book["rev9"]
    root = Path(book["root"])
    work = root / "work"
    plots = data_book_analysis.read_csv(root / "plots.csv")
    seen = Counter(one_number(r["locationPlanNumber"]) for r in plots)
    plots_by_number = {one_number(r["locationPlanNumber"]): r for r in plots if one_number(r["locationPlanNumber"]) and seen[one_number(r["locationPlanNumber"])] == 1}
    dlus_settings, sales_settings = rev9["dlus"], rev9["sales"]
    dlus = {number_key(r["plot"]): r for r in by_header(xlsx_rows(work / rev9["workbooks"][dlus_settings["workbook"]], dlus_settings["sheet"]),
                                                        dlus_settings["headerRow"], {"plot": dlus_settings["columns"]["plot"], "front": dlus_settings["columns"]["front"]})}
    sales = {number_key(r["plot"]): r for r in by_header(xlsx_rows(work / rev9["workbooks"][sales_settings["workbook"]], sales_settings["sheet"]),
                                                         sales_settings["headerRow"], {k: sales_settings["columns"][k] for k in ("plot", "typology", "landWidth", "landLength")})}
    out = root / rev9["outFolder"]
    setback_rows = [r for r in data_book_analysis.read_csv(out / "setbacks.csv") if r["unitPlot"] == "True"]
    width_rows = data_book_analysis.read_csv(out / "widths.csv")
    face_rows = [r for r in data_book_analysis.read_csv(out / "rev9_faces.csv") if r["faceClass"] != "noFace"]
    outlines = json.loads((out / "rev9_face_outlines.json").read_text(encoding="utf-8"))
    labels = data_book_analysis.read_csv(out / "rev9_labels.csv")
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])

    settings = project_profile.load_settings(settings_path)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    _, earlier = data_book.folder_of(book, book["join"]["curb"]["drawing"], book["join"]["fallbackOutputs"])
    curbs = c3d_check.segments_of(json.loads((earlier / "pieces.json").read_text(encoding="utf-8"))["pieces"], book["join"]["curb"]["layers"])
    entry_items = data_book_analysis.read_csv(root / book["entryTerms"]["outFolder"] / "entry_terms.csv")
    best = max({i["variant"] for i in entry_items}, key=lambda v: sum(1 for i in entry_items if i["variant"] == v and i["band"] == "within5mm"))
    entry_by_key = {(i["plot"], data_book_retest.STEEP): i["band"] for i in entry_items if i["variant"] == best}
    scheduled = sorted((r for r in plots if (r["unitRev6Source"] or "").startswith("schedule") and r["pastEntry"]), key=lambda r: int(r["baseplanLabel"]))
    surfaces = {}
    for name in {r["roadLevelSurface"] for r in scheduled if r["roadLevelSurface"]}:
        folder = Path(connect["landxmlDir"]) / landxml_reader.folder_name(name)
        surfaces[name] = (np.load(folder / "points.npy"), np.load(folder / "faces.npy"))

    tolerance = book["analysis"]["setbackToleranceM"]
    result = {"bestEntryVariant": best, "seeds": {}}
    for seed in check["seeds"]:
        size = check["size"]
        pick = lambda items: random.Random(seed).sample(items, min(size, len(items)))
        entry = data_book_retest_check.check_entry(pick(scheduled), faces, curbs, surfaces, entry_by_key, profile, book["retest"],
                                                   book["analysis"]["bandsM"], connect["snapM"])
        result["seeds"]["seed%d" % seed] = {
            "setbacks": check_setbacks(pick(setback_rows), dlus, plots_by_number, rev9, tolerance),
            "widths": check_widths(pick(width_rows), sales, plots_by_number, book["analysis"]["widthToleranceM"]),
            "faces": check_faces(pick(face_rows), faces, outlines, labels, {**rev9["drawing"], **check}),
            "entry": {k: v for k, v in entry.items() if k.startswith(data_book_retest.STEEP)}}
    (root / "logs" / "data3_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for round DATA-3 (R198)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    print(json.dumps(run(c3d_run.load_config(args.config), args.settings), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
