"""The Zone 4 location plan of Phase 1 (R179 to R183), Part L of round DATA-2.

The plan is one vector PDF sheet that labels every Phase 1 plot with its old client code and its plot number. This reads
every character, joins them into text lines of its own, classes each line by the patterns of dataBook.locationPlan, pairs a
code line with the number line just under it, fits the page to the site on the IMC plots by an affine least squares fit,
puts every number on its baseplan face, gives the shift from baseplan label to plot number per neighbourhood, compares the
code through the old to new code table with Typology_Rev9 of the Rev 9 sales sheet and reads the zone outlines. Rows go to
the data book, counts to locationPlan.json and locationPlan.md there.
"""
import argparse
import csv
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import openpyxl
import pdfplumber
from shapely.geometry import LineString, Point, Polygon
from shapely.ops import polygonize, unary_union
from shapely.strtree import STRtree

import c3d_run
import common
import data_book
import data_book_analysis
import link_summary
import yard_rule


def text_lines(chars, size_band, gap, line_tolerance):
    """Characters joined into text lines: same top within line_tolerance and each character starting within gap of the end of
    the one before. Each line keeps its text, box and centre in page points."""
    picked = sorted((c for c in chars if size_band[0] <= c["size"] <= size_band[1]), key=lambda c: (c["top"], c["x0"]))
    rows = []
    for c in picked:
        if rows and abs(c["top"] - rows[-1][0]["top"]) <= line_tolerance:
            rows[-1].append(c)
        else:
            rows.append([c])
    found = []
    for row in rows:
        row.sort(key=lambda c: c["x0"])
        group = [row[0]]
        for c in row[1:]:
            if c["x0"] - group[-1]["x1"] <= gap:
                group.append(c)
            else:
                found.append(_line(group))
                group = [c]
        found.append(_line(group))
    return [line for line in found if line["text"]]


def _line(group):
    x0, x1 = min(c["x0"] for c in group), max(c["x1"] for c in group)
    top, bottom = min(c["top"] for c in group), max(c["bottom"] for c in group)
    return {"text": "".join(c["text"] for c in group).strip(), "x0": x0, "x1": x1, "top": top, "bottom": bottom,
            "x": (x0 + x1) / 2, "y": (top + bottom) / 2, "size": group[0]["size"]}


def classify(text, patterns):
    """The kind of a text line, the first pattern of the config it matches, else other."""
    for kind, pattern in patterns.items():
        if re.fullmatch(pattern, text):
            return kind
    return "other"


def pair_labels(codes, numbers, dy_band, dx_max):
    """Each code line with the number line just under it, nearest first and one to one."""
    candidates = []
    for i, code in enumerate(codes):
        for k, number in enumerate(numbers):
            dy = number["top"] - code["top"]
            dx = abs(number["x"] - code["x"])
            if dy_band[0] <= dy <= dy_band[1] and dx <= dx_max:
                candidates.append((dx + abs(dy - (dy_band[0] + dy_band[1]) / 2), i, k))
    candidates.sort()
    used_codes, used_numbers, pairs = set(), set(), []
    for _, i, k in candidates:
        if i in used_codes or k in used_numbers:
            continue
        used_codes.add(i)
        used_numbers.add(k)
        pairs.append((i, k))
    return pairs, [i for i in range(len(codes)) if i not in used_codes], [k for k in range(len(numbers)) if k not in used_numbers]


def fit_affine(page, site):
    """The affine map from page points to site metres by least squares, as a 3 by 2 matrix."""
    a = np.hstack([np.asarray(page, dtype=float), np.ones((len(page), 1))])
    matrix, *_ = np.linalg.lstsq(a, np.asarray(site, dtype=float), rcond=None)
    return matrix


def apply(matrix, points):
    a = np.hstack([np.asarray(points, dtype=float).reshape(-1, 2), np.ones((len(points), 1))])
    return a @ matrix


def robust_fit(page, site, drop_factor, rounds, floor=0.0):
    """Fit, drop the points whose residual is over drop_factor times the median, never under floor, fit again, rounds times."""
    keep = list(range(len(page)))
    for _ in range(rounds):
        matrix = fit_affine([page[i] for i in keep], [site[i] for i in keep])
        residual = np.hypot(*(apply(matrix, page) - np.asarray(site)).T)
        limit = max(drop_factor * float(np.median(residual[keep])), floor)
        keep = [i for i in range(len(page)) if residual[i] <= limit]
    matrix = fit_affine([page[i] for i in keep], [site[i] for i in keep])
    residual = np.hypot(*(apply(matrix, page) - np.asarray(site)).T)
    return matrix, residual, keep


def stats(values):
    values = sorted(float(v) for v in values)
    if not values:
        return {}
    return {"n": len(values), "median": round(statistics.median(values), 3), "p95": round(values[int(0.95 * (len(values) - 1))], 3), "max": round(values[-1], 3)}


def new_code(code, table, form_of, pattern):
    """The new code of an old client code: an exact row of the table, else the row of its base code, picked by the form the
    suffix names when the base has more than one row. None when no row fits."""
    table = [row for row in table if row["new"]]
    exact = [row for row in table if row["old"] == code]
    if len({row["new"] for row in exact}) == 1:
        return exact[0]["new"]
    found = re.fullmatch(pattern, code)
    if not found:
        return None
    base, suffix = found.group("base"), found.group("suffix")
    rows = [row for row in table if row["old"] == base] or [row for row in table if row["old"] and row["old"].split(" ")[0] == base]
    news = {row["new"] for row in rows}
    if not rows and base in {row["new"] for row in table}:
        # A base with no row of its own that is a new code already, DP2 (BB) is a DP2
        return base
    if len(news) == 1:
        return news.pop()
    form = form_of.get(suffix or "")
    picked = {row["new"] for row in rows if row["form"] == form}
    return picked.pop() if len(picked) == 1 else None


def control_check(matrix, word, small, stated, search):
    """The small shape nearest the control point text, within search points, that the fit puts nearest the stated point, and
    the distance in metres. The text itself sits beside the mark."""
    best = None
    for x, y in small:
        if abs(x - word["x0"]) <= search and abs(y - word["top"]) <= search:
            at = apply(matrix, [(x, y)])[0]
            distance = float(np.hypot(at[0] - stated[0], at[1] - stated[1]))
            if best is None or distance < best:
                best = distance
    return round(best, 3) if best is not None else None


def control_texts(words):
    """R181, R387. Each control point text the sheet prints, E = <east> N = <north>: its E word and the stated site point."""
    flat = [w["text"] for w in words]
    found = []
    for k, word in enumerate(words):
        if word["text"] == "E" and k + 5 < len(words) and flat[k + 1] == "=" and flat[k + 3] == "N" and flat[k + 4] == "=":
            try:
                east, north = float(flat[k + 2]), float(flat[k + 5])
            except ValueError:
                continue
            found.append((word, (east, north)))
    return found


def is_mark(shape, mark):
    """R387. A curve of the mark shape of controlMark: its width and height within tolerancePt of widthPt and heightPt and its points."""
    return (abs(shape["x1"] - shape["x0"] - mark["widthPt"]) <= mark["tolerancePt"] and abs(shape["bottom"] - shape["top"] - mark["heightPt"]) <= mark["tolerancePt"]
            and len(shape.get("pts") or []) == mark["points"])


def control_marks(curves, texts, mark, search):
    """R387. The mark of each control point text: the one curve of the mark shape within search points of the start of its E = text,
    with the stated point. A text with no such curve or two is refused, a mark is never taken by its nearness to the text."""
    found = []
    for word, stated in texts:
        near = [((shape["x0"] + shape["x1"]) / 2, (shape["top"] + shape["bottom"]) / 2) for shape in curves if is_mark(shape, mark)]
        near = [point for point in near if abs(point[0] - word["x0"]) <= search and abs(point[1] - word["top"]) <= search]
        if len(near) != 1:
            raise ValueError("the control point text E = %s N = %s has %d curves of the mark shape within %s pt, one is needed (R387)" % (stated[0], stated[1], len(near), search))
        found.append((near[0], stated))
    return found


def similarity_fit(page, site):
    """R387. The similarity from page points to site metres by least squares, a scale, a turn, a mirror or none and a shift, no shear, as
    a 3 by 2 matrix like fit_affine. Of the two, mirrored and not, the one whose largest residual is smaller. It needs two points, and
    three marks almost on one line give it where they give no affine map."""
    if len(page) < 2:
        raise ValueError("the similarity of the control marks needs two marks, it has %d (R387)" % len(page))
    fits = []
    for mirrored in (True, False):
        rows, rhs = [], []
        for (x, y), (east, north) in zip(page, site):
            rows += [[x, y, 1.0, 0.0], [-y, x, 0.0, 1.0]] if mirrored else [[x, -y, 1.0, 0.0], [y, x, 0.0, 1.0]]
            rhs += [east, north]
        (a, b, e, f), *_ = np.linalg.lstsq(np.asarray(rows, dtype=float), np.asarray(rhs, dtype=float), rcond=None)
        matrix = np.array([[a, b], [b, -a], [e, f]]) if mirrored else np.array([[a, b], [-b, a], [e, f]])
        fits.append((float(np.hypot(*(apply(matrix, page) - np.asarray(site, dtype=float)).T).max()), mirrored, matrix))
    return min(fits, key=lambda item: (item[0], not item[1]))[2]


def face_of_points(points, faces):
    """R182, R387. The baseplan face that holds each site point, None where no face or more than one holds it."""
    names = sorted(faces, key=int)
    tree = STRtree([faces[name] for name in names])
    found = []
    for east, north in points:
        hits = [names[int(k)] for k in tree.query(Point(east, north), predicate="within")]
        found.append(hits[0] if len(hits) == 1 else None)
    return found


def control_refine(matrix, labels, faces, drop_factor, rounds, floor):
    """R387. The start refined by the fit of R181 on the faces that hold one label each under it: each such label to the centroid of
    its face, the points over drop_factor times the median residual dropped. Gives the refined matrix and the faces taken."""
    points = [(label["x"], label["y"]) for label in labels]
    homes = face_of_points(apply(matrix, points), faces) if points else []
    held = Counter(home for home in homes if home is not None)
    taken = sorted(((home, index) for index, home in enumerate(homes) if home is not None and held[home] == 1), key=lambda pair: int(pair[0]))
    if len(taken) < 3:
        raise ValueError("the control point fit puts a single label on %d faces, the refine needs three (R387)" % len(taken))
    page = [points[index] for _, index in taken]
    site = [(faces[home].centroid.x, faces[home].centroid.y) for home, _ in taken]
    refined, residual, keep = robust_fit(page, site, drop_factor, rounds, floor)
    return refined, {"faces": [home for home, _ in taken], "kept": len(keep), "residualKeptM": stats(residual[keep]), "residualAllM": stats(residual)}


def numbered_otherwise(rows, control_faces, schedule_site, control_site, faces):
    """R387. Every face the control point fit numbers otherwise than the schedule fit, listed apart: rows are the labels as the schedule
    fit numbered them, number and face, control_faces the face of each label by the control fit, both site points in the same order.
    Each listed face says why: every label of it the two fits put apart, its face by each, how far its point lies from the edge of the
    face it is in and how far apart the two fits put it. It numbers nothing."""
    by_schedule, by_control, moved = defaultdict(list), defaultdict(list), defaultdict(list)
    for row, face, at_schedule, at_control in zip(rows, control_faces, schedule_site, control_site):
        if row["face"] is not None:
            by_schedule[row["face"]].append(row["number"])
        if face is not None:
            by_control[face].append(row["number"])
        if row["face"] != face:
            edge_face, point = (row["face"], at_schedule) if row["face"] is not None else (face, at_control)
            edge = common.fixed(float(faces[edge_face].exterior.distance(Point(*point))), 3) if edge_face is not None else None
            gap = common.fixed(float(np.hypot(at_schedule[0] - at_control[0], at_schedule[1] - at_control[1])), 3)
            why = {"number": row["number"], "scheduleFace": row["face"], "controlFace": face, "edgeM": edge, "gapM": gap}
            why["text"] = "label %s on face %s by the schedule fit and on face %s by the control fit, %s m from the edge of face %s, the fits %s m apart at it" % (
                row["number"], row["face"] or "none", face or "none", edge, edge_face or "none", gap)
            for home in {row["face"], face} - {None}:
                moved[home].append(why)
    found = []
    for face in sorted(set(by_schedule) | set(by_control), key=int):
        mine, theirs = sorted(by_schedule.get(face, []), key=int), sorted(by_control.get(face, []), key=int)
        if mine != theirs:
            found.append({"face": face, "schedule": mine, "control": theirs, "why": sorted(moved[face], key=lambda item: int(item["number"]))})
    return found


def control_fit_check(labels, rows, words, curves, faces, plan, schedule_site):
    """R387. The control point fit as a check of the schedule fit of R181, which stays the numbering: the marks are the curves of the
    mark shape of controlMark, the fit starts from their similarity and is refined on the faces that hold one label each, then every
    face it numbers otherwise than the schedule fit is listed apart with why. It writes no number into labels.csv or any row."""
    texts = control_texts(words)
    marks = control_marks(curves, texts, plan["controlMark"], plan["controlSearchPt"])
    start = similarity_fit([mark for mark, _ in marks], [stated for _, stated in marks])
    refined, refine = control_refine(start, labels, faces, plan["fitDropFactor"], plan["fitRounds"], plan["fitFloorM"])
    points = [(label["x"], label["y"]) for label in labels]
    control_site = apply(refined, points) if points else []
    control_faces = face_of_points(control_site, faces)
    listed = numbered_otherwise(rows, control_faces, schedule_site, control_site, faces)
    unit_numbers = {row["number"] for row in rows if row["kind"] == "clientCode"}
    at = lambda matrix: [common.fixed(float(np.hypot(*(apply(matrix, [mark])[0] - np.asarray(stated)))), 3) for mark, stated in marks]
    apart = np.hypot(*(np.asarray(control_site) - np.asarray(schedule_site)).T) if points else []
    return {"controlTexts": len(texts), "marks": len(marks), "markGapStartM": at(start), "markGapRefinedM": at(refined),
            "refineFaces": len(refine["faces"]), "refineKept": refine["kept"], "refineResidualKeptM": refine["residualKeptM"],
            "againstScheduleM": stats(apart), "labelsOnAFace": {"schedule": sum(1 for row in rows if row["face"] is not None),
                                                                 "control": sum(1 for face in control_faces if face is not None)},
            "facesNumberedOtherwise": len(listed), "unitFacesNumberedOtherwise": sum(1 for item in listed if set(item["schedule"] + item["control"]) & unit_numbers),
            "list": listed}


def read_table(path, sheet, header_row, columns):
    workbook = openpyxl.load_workbook(path, read_only=True, data_only=True)
    rows = list(workbook[sheet].iter_rows(values_only=True))
    header = [str(c).strip() if c is not None else None for c in rows[header_row - 1]]
    index = {name: header.index(columns[name]) for name in columns}
    found = []
    for row in rows[header_row:]:
        item = {name: (str(row[k]).strip() if k < len(row) and row[k] is not None else None) for name, k in index.items()}
        if any(item.values()):
            found.append(item)
    return found


def zone_outlines(lines, colour, width):
    """Closed outlines from the page lines of one stroke colour and width, polygonized."""
    segments = [LineString(l["pts"]) for l in lines
                if str(l.get("stroking_color")) == colour and round(l.get("linewidth", 0), 2) == width and len(l.get("pts") or []) >= 2]
    if not segments:
        return []
    return [p for p in polygonize(unary_union(segments)) if p.area > 0]


def page_cells(label):
    """R180, R385. The page point of a label as labels.csv writes it, in points to 3 decimals."""
    return {"pageX": common.fixed(label["x"], 3), "pageY": common.fixed(label["y"], 3)}


def site_cells(east, north):
    """R181, R385. The site point of a label as labels.csv writes it, in metres to 3 decimals, read by data_book.py to wire the labels
    to the faces."""
    return {"east": common.fixed(float(east), 3), "north": common.fixed(float(north), 3)}


def run(config, settings, check=None):
    book = config["dataBook"]
    plan = book["locationPlan"]
    root = Path(book["root"])
    work = root / "work"
    with pdfplumber.open(work / plan["pdf"]) as pdf:
        page = pdf.pages[0]
        chars = page.chars
        page_lines = [dict(l, pts=[(p[0], p[1]) for p in l.get("pts") or []]) for l in page.lines]
        words = page.extract_words(extra_attrs=["size"])
        small = [((s["x0"] + s["x1"]) / 2, (s["top"] + s["bottom"]) / 2) for s in page.curves + page.rects + page.lines
                 if s["x1"] - s["x0"] <= plan["controlShapeMaxPt"] and s["bottom"] - s["top"] <= plan["controlShapeMaxPt"]]
        curves = [{key: s[key] for key in ("x0", "x1", "top", "bottom", "pts")} for s in page.curves] if check == "control" else []
        meta = {"pages": len(pdf.pages), "width": float(page.width), "height": float(page.height), "rotate": page.page_obj.attrs.get("Rotate"),
                "words": len(words), "chars": len(chars), "images": len(page.images)}
    lines = text_lines(chars, plan["labelSize"], plan["joinGapPt"], plan["lineTolerancePt"])
    for line in lines:
        line["kind"] = classify(line["text"], plan["patterns"])
    kinds = Counter(line["kind"] for line in lines)
    codes = [line for line in lines if line["kind"] in plan["codeKinds"]]
    numbers = [line for line in lines if line["kind"] == "number"]
    pairs, lone_codes, lone_numbers = pair_labels(codes, numbers, plan["pairDyPt"], plan["pairDxPt"])
    labels = [{"code": codes[i]["text"], "kind": codes[i]["kind"], "number": numbers[k]["text"],
               "x": (codes[i]["x"] + numbers[k]["x"]) / 2, "y": (codes[i]["top"] + numbers[k]["bottom"]) / 2} for i, k in pairs]
    report = {"sheet": meta, "lines": dict(kinds), "pairs": {"pairs": len(pairs), "codesWithoutNumber": len(lone_codes), "numbersWithoutCode": len(lone_numbers),
                                                              "pairsByKind": dict(Counter(l["kind"] for l in labels)),
                                                              "duplicateNumbers": sum(1 for n, c in Counter(l["number"] for l in labels).items() if c > 1)}}

    # The fit on the IMC plots, the official number less the shift gives the baseplan face
    connect = config["connect"]
    shifts = {name: entry["shift"] for name, entry in connect["neighbourhoods"].items() if entry}
    schedules = data_book.schedule_rows(settings, connect)
    official_of = defaultdict(set)
    for name, official in schedules:
        official_of[name].add(official)
    plots = link_summary.load(connect["baseplanPlots"])["plots"]
    faces = yard_rule.face_shapes(plots, 0)[0]
    by_number = defaultdict(list)
    for label in labels:
        by_number[label["number"]].append(label)

    def known(name):
        page_points, site_points, numbers_used = [], [], []
        for official in sorted(official_of[name], key=int):
            found = by_number.get(official) or []
            face = faces.get(str(int(official) - shifts[name]))
            if len(found) == 1 and face is not None:
                page_points.append((found[0]["x"], found[0]["y"]))
                site_points.append((face.centroid.x, face.centroid.y))
                numbers_used.append(official)
        return page_points, site_points, numbers_used

    fit_name = plan["fitNeighbourhood"]
    page_points, site_points, used = known(fit_name)
    matrix, residual, keep = robust_fit(page_points, site_points, plan["fitDropFactor"], plan["fitRounds"], plan["fitFloorM"])
    fit = {"neighbourhood": fit_name, "points": len(page_points), "kept": len(keep), "residualKeptM": stats(residual[keep]), "residualAllM": stats(residual)}
    scale = float(np.sqrt(abs(np.linalg.det(matrix[:2, :2]))))
    fit["metresPerPoint"] = round(scale, 4)
    checks = {}
    for name in shifts:
        if name == fit_name:
            continue
        p, s, _ = known(name)
        if p:
            checks[name] = stats(np.hypot(*(apply(matrix, p) - np.asarray(s)).T))
    fit["checkOn"] = checks
    flat = [w["text"] for w in words]
    fit["controlPoints"] = [control_check(matrix, word, small, stated, plan["controlSearchPt"]) for word, stated in control_texts(words)]
    report["fit"] = fit

    # Every number on its baseplan face
    labels_site = apply(matrix, [(l["x"], l["y"]) for l in labels]) if labels else []
    face_labels = sorted(faces, key=int)
    tree = STRtree([faces[k] for k in face_labels])
    home = {}
    for name in shifts:
        for official in official_of[name]:
            home[official] = name
    shift_by = defaultdict(Counter)
    rows = []
    for label, (east, north) in zip(labels, labels_site):
        hits = [face_labels[int(k)] for k in tree.query(Point(east, north), predicate="within")]
        face = hits[0] if len(hits) == 1 else None
        group = home.get(label["number"], "rest")
        offset = int(label["number"]) - int(face) if face is not None else None
        shift_by[group]["noFace" if face is None else offset] += 1
        rows.append(dict({"number": label["number"], "code": label["code"], "kind": label["kind"], "face": face, "offset": offset, "group": group},
                         **page_cells(label)))
    report["numberToFace"] = {g: dict(c.most_common(12)) for g, c in sorted(shift_by.items())}
    if check == "control":
        # R387. A check of the numbering above, never a numbering of its own
        report["controlCheck"] = control_fit_check(labels, rows, words, curves, faces, plan, labels_site)
    for name, shift in shifts.items():
        found = shift_by.get(name, Counter())
        report["numberToFace"][name + "Check"] = {"shift": shift, "match": found.get(shift, 0), "other": sum(n for k, n in found.items() if k != shift)}

    # Codes against the Rev 9 sales sheet through the old to new table
    # R350. Every workbook setting names a key of dataBook.rev9.workbooks
    sales_book = work / book["rev9"]["workbooks"][plan["sales"]["workbook"]]
    sales = read_table(sales_book, plan["sales"]["sheet"], plan["sales"]["headerRow"], plan["sales"]["columns"])
    table = read_table(sales_book, plan["codeTable"]["sheet"], plan["codeTable"]["headerRow"], plan["codeTable"]["columns"])
    sale_of = {}
    for row in sales:
        if row["plot"]:
            sale_of.setdefault(str(int(float(row["plot"]))), row)
    compare = Counter()
    pairs_seen = Counter()
    for row in rows:
        sale = sale_of.get(row["number"])
        new = new_code(row["code"], table, plan["formOfSuffix"], plan["patterns"]["clientCode"])
        row.update({"newCode": new, "rev9": sale["typology"] if sale else None, "nbh": sale["nbh"] if sale else None, "zone": sale["zone"] if sale else None})
        if row["kind"] != "clientCode":
            continue
        if sale is None:
            compare["noSalesRow"] += 1
        elif new is None:
            compare["noNewCode"] += 1
        elif new == sale["typology"]:
            compare["same"] += 1
        else:
            compare["differs"] += 1
            pairs_seen["%s>%s against %s" % (row["code"], new, sale["typology"])] += 1
    dlus = read_table(work / book["rev9"]["workbooks"][plan["dlus"]["workbook"]], plan["dlus"]["sheet"], plan["dlus"]["headerRow"], plan["dlus"]["columns"])
    level4 = Counter(row["level4"] for row in dlus if row["level4"])
    plan_new = Counter(row["newCode"] or "no new code" for row in rows if row["kind"] == "clientCode")
    numbers_paired = [int(row["number"]) for row in rows if row["kind"] == "clientCode"]
    report["codes"] = {"compare": dict(compare), "differences": dict(pairs_seen.most_common()), "salesRows": sum(1 for r in sales if r["plot"]),
                       "noSalesRowByCode": dict(Counter(row["code"] for row in rows if row["kind"] == "clientCode" and row["rev9"] is None).most_common()),
                       "planNewCodeAgainstLevel4": {code: {"plan": n, "level4": level4.get(code, 0)} for code, n in sorted(plan_new.items())},
                       "level4UnitTotal": sum(level4.get(code, 0) for code in plan_new), "planUnitTotal": sum(plan_new.values()),
                       "oldCodes": dict(Counter(row["code"] for row in rows if row["kind"] == "clientCode").most_common())}
    report["numbers"] = {"unitMin": min(numbers_paired), "unitMax": max(numbers_paired), "allMin": min(int(r["number"]) for r in rows),
                         "allMax": max(int(r["number"]) for r in rows)} if numbers_paired else {}
    report["shiftByNbh"] = {nbh or "noSalesRow": dict(c.most_common(6)) for nbh, c in sorted(
        ((nbh, Counter(r["offset"] if r["offset"] is not None else "noFace" for r in rows if r["kind"] == "clientCode" and r["nbh"] == nbh))
         for nbh in {r["nbh"] for r in rows}), key=lambda item: str(item[0]))}

    # The lists behind the counts, by plot number, for the data book only
    modal = {}
    for nbh in {r["nbh"] for r in rows if r["nbh"]}:
        offsets = Counter(r["offset"] for r in rows if r["kind"] == "clientCode" and r["nbh"] == nbh and r["offset"] is not None)
        modal[nbh] = offsets.most_common(1)[0][0] if offsets else None
    schedule_unit = {official: row.get("unit") for (name, official), row in schedules.items()}
    report["lists"] = {
        "noSalesRow": sorted((r["number"] for r in rows if r["kind"] == "clientCode" and r["rev9"] is None), key=int),
        "codeDiffers": sorted(("%s %s>%s against %s" % (r["number"], r["code"], r["newCode"], r["rev9"]) for r in rows
                               if r["kind"] == "clientCode" and r["rev9"] and r["newCode"] != r["rev9"])),
        "shiftBreaks": sorted(("%s %s offset %s, the NBH mostly %s" % (r["nbh"], r["number"], r["offset"], modal.get(r["nbh"])) for r in rows
                               if r["kind"] == "clientCode" and r["nbh"] and r["offset"] != modal.get(r["nbh"]))),
        "scheduleUnitAgainstRev9": sorted(("%s schedule %s Rev 9 %s" % (o, u, (sale_of.get(o) or {}).get("typology")) for o, u in schedule_unit.items()
                                           if (sale_of.get(o) or {}).get("typology") and u != sale_of[o]["typology"]), key=lambda t: int(t.split()[0])),
    }
    report["counts"] = {k: len(v) for k, v in report["lists"].items()}
    report["modalShiftByNbh"] = modal

    # Zones: the hatch of the plan, and the outlines of the key plan
    hatch = plan["zoneHatch"]
    hatch_lines = [LineString(l["pts"]) for l in page_lines if str(l.get("stroking_color")) == hatch["colour"] and round(l.get("linewidth", 0), 2) == hatch["width"]
                   and len(l["pts"]) >= 2]
    hatch_tree = STRtree(hatch_lines) if hatch_lines else None
    under = Counter()
    for label, row in zip(labels, rows):
        if row["kind"] != "clientCode":
            continue
        hit = hatch_tree is not None and len(hatch_tree.query(Point(label["x"], label["y"]).buffer(hatch["bufferPt"]))) > 0
        under["%s|%s|%s" % (row["zone"] or "noSalesRow", row["nbh"] or "noSalesRow", "hatched" if hit else "not hatched")] += 1
    report["hatch"] = dict(sorted(under.items()))
    zones = {}
    for name, style in plan["zoneStyles"].items():
        outlines = zone_outlines(page_lines, style["colour"], style["width"])
        zones[name] = {"outlines": len(outlines), "areasPt2": sorted(round(p.area) for p in outlines)[-6:]}
        if outlines:
            members = defaultdict(Counter)
            for label, row in zip(labels, rows):
                for k, outline in enumerate(sorted(outlines, key=lambda p: -p.area)):
                    if outline.contains(Point(label["x"], label["y"])):
                        members["outline%d" % k][(row["group"], row["nbh"], row["zone"])] += 1
            zones[name]["members"] = {k: {"%s|%s|%s" % key: n for key, n in v.most_common(8)} for k, v in members.items()}
    report["zones"] = zones
    report["salesNbhZone"] = {"%s|%s" % key: n for key, n in sorted(Counter((r["nbh"], r["zone"]) for r in sales).items(), key=str)}
    report["nbhOfSchedulePlots"] = {name: dict(Counter((sale_of.get(o) or {}).get("nbh") for o in official_of[name]).most_common()) for name in shifts}
    words_upper = " ".join(flat).upper()
    report["searchWords"] = {word: words_upper.count(word) for word in plan["searchWords"]}
    out = root / plan["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    with open(out / "labels.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]) + ["east", "north"] if rows else ["number"], lineterminator="\n")
        writer.writeheader()
        for row, (east, north) in zip(rows, labels_site):
            writer.writerow(dict(row, **site_cells(east, north)))
    with open(out / "lines.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["text", "kind", "x", "y", "size"], lineterminator="\n", extrasaction="ignore")
        writer.writeheader()
        writer.writerows(lines)
    (out / "fit.json").write_text(json.dumps({"matrix": matrix.tolist()}, indent=1), encoding="utf-8")
    return report


def markdown(report):
    lines = ["# Location plan", "", "Generated by python/location_plan.py (R179 to R183). Counts only.", ""]
    for key, value in report.items():
        lines += ["## " + key, "", "```", json.dumps(value, indent=1, ensure_ascii=False), "```", ""]
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read the Zone 4 location plan and tie it to the plots (R179 to R183)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--check", choices=("control",), help="R387. control: the control point fit as a check of the schedule fit, it numbers nothing")
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    import project_profile
    report = run(config, project_profile.load_settings(project_profile.SETTINGS_PATH), args.check)
    root = Path(config["dataBook"]["root"])
    (root / "locationPlan.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    (root / "locationPlan.md").write_text(markdown(report), encoding="utf-8")
    print(json.dumps(report, indent=1, ensure_ascii=False)[:6000])
    return 0


if __name__ == "__main__":
    sys.exit(main())
