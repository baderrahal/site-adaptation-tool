"""Wiring between the drawings (R174 to R178), Part W of round DATA-2.

Each source of the data book is wired to the baseplan plots by several methods: point inside, a widened point, the best
area share or every share over a minimum, nearest within a distance, a type or label match with location, and for the
schedules the label plus shift with and without the location check of the PDF points (R24, R28). Every method is scored
against the answer key, the 747 schedule plots and the past job plan (plan_imc_past.json): wired, agree, ambiguous, wrong
and unwired. It also measures the weak spots of the fixed join of R166 and checks the drawings against each other. Counts
go to wiring.json and wiring.md in the data book, every method, distance and share comes from dataBook.wiring.
"""
import argparse
import fnmatch
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Point, Polygon
from shapely.strtree import STRtree

import c3d_connect
import c3d_run
import cad_links
import data_book
import data_book_analysis
import entry_levels
import link_summary
import project_profile
import prototype_offsets
import surface_lookup
import yard_rule

NO_KEY = "noKey"


class Faces:
    """The baseplan faces by label with a tree, for the point, distance and share questions of every method."""

    def __init__(self, faces):
        self.labels = sorted(faces, key=int)
        self.shapes = [faces[label] for label in self.labels]
        self.by_label = dict(faces)
        self.tree = STRtree(self.shapes) if self.shapes else None

    def holding(self, x, y):
        if self.tree is None:
            return []
        return [self.labels[k] for k in self.tree.query(Point(x, y), predicate="within")]

    def within(self, x, y, distance):
        """The faces within distance of the point, nearest first, as (distance, label)."""
        if self.tree is None:
            return []
        point = Point(x, y)
        found = [(self.shapes[k].distance(point), self.labels[k]) for k in self.tree.query(point.buffer(distance))]
        return sorted(item for item in found if item[0] <= distance)

    def shares(self, shape):
        """The share of shape that lies in each face it touches, largest first, as (share, label)."""
        if self.tree is None or shape.area <= 0:
            return []
        found = [(self.shapes[k].intersection(shape).area / shape.area, self.labels[k]) for k in self.tree.query(shape)]
        return sorted((item for item in found if item[0] > 0), key=lambda item: (-item[0], int(item[1])))


# The matchers. Each gives, for every item, the labels of the plots it wires to

def point_inside(faces, points):
    return [faces.holding(x, y) for x, y in points]


def point_widened(faces, points, widen):
    """The fixed join of the footprint blocks in R166, per plot: the items inside the face, and when there are none every item
    within widen of it. A pair block whose centre sits on the shared wall reaches both plots this way."""
    inside = point_inside(faces, points)
    held = by_plot(inside)
    tree = STRtree([Point(x, y) for x, y in points]) if points else None
    found = [list(labels) for labels in inside]
    for label, face in zip(faces.labels, faces.shapes):
        if held.get(label) or tree is None:
            continue
        for k in tree.query(face.buffer(widen), predicate="contains"):
            found[int(k)].append(label)
    return found


def nearest(faces, points, max_distance, tie):
    found = []
    for x, y in points:
        near = faces.within(x, y, max_distance)
        found.append([label for distance, label in near if distance <= near[0][0] + tie] if near else [])
    return found


def best_share(faces, shapes, min_share, tie):
    """The one face that holds the largest share of the shape, with any face within tie of it, when the share reaches min_share."""
    found = []
    for shape in shapes:
        shares = faces.shares(shape)
        found.append([label for share, label in shares if share >= shares[0][0] - tie] if shares and shares[0][0] >= min_share else [])
    return found


def every_share(faces, shapes, min_share):
    """Every face that holds at least min_share of the shape, so a pair or a row block wires to each of its plots (R171)."""
    return [[label for share, label in faces.shares(shape) if share >= min_share] for shape in shapes]


def key_near(faces, points, keys, face_keys, max_distance):
    """The nearest face within max_distance whose key (type or label) equals the key of the item."""
    found = []
    for (x, y), key in zip(points, keys):
        near = [(distance, label) for distance, label in faces.within(x, y, max_distance) if key is not None and key in face_keys.get(label, ())]
        found.append([label for distance, label in near if distance <= near[0][0]] if near else [])
    return found


def by_plot(wired):
    """Item lists per plot from the label lists per item."""
    found = defaultdict(list)
    for item, labels in enumerate(wired):
        for label in labels:
            found[label].append(item)
    return found


def score(found, truth, key_plots, same=None):
    """wired, agree, ambiguous, wrong and unwired over the key plots. found holds a list per plot, truth a value per plot,
    None where the key has no value for that plot, which counts the plot under noKey once wired."""
    same = same or (lambda value, wanted: value == wanted)
    counts = Counter({"keyPlots": len(key_plots)})
    for plot in key_plots:
        items = found.get(plot) or []
        if not items:
            counts["unwired"] += 1
            continue
        counts["wired"] += 1
        if len(items) > 1:
            counts["ambiguous"] += 1
        elif truth.get(plot) is None:
            counts[NO_KEY] += 1
        else:
            counts["agree" if same(items[0], truth[plot]) else "wrong"] += 1
    return {k: counts.get(k, 0) for k in ("keyPlots", "wired", "agree", "ambiguous", "wrong", "unwired", NO_KEY)}


def coverage(found, plots, owns=None):
    """one, ambiguous and unwired over the Phase 1 plots, and wrongOwner where owns says the item does not sit on the plot."""
    counts = Counter()
    for plot in plots:
        items = found.get(plot) or []
        counts["unwired" if not items else "one" if len(items) == 1 else "ambiguous"] += 1
        if owns is not None:
            counts["wrongOwner"] += sum(1 for item in items if not owns(plot, item))
    return dict(counts)


def owner_test(faces, shapes, min_share):
    """True when the face of the plot holds at least min_share of the shape of the item."""
    def owns(plot, item):
        shape = shapes[item]
        return shape.area > 0 and faces.by_label[plot].intersection(shape).area / shape.area >= min_share
    return owns


def pick(scores, current=None):
    """The best method of a source: most agree, then fewest wrong, fewest ambiguous and most wired. With no key, most plots with
    one hit and fewest ambiguous. On a tie the current method of R166 stays."""
    def rank(name):
        s = scores[name]["key"]
        cover = scores[name].get("phase1", {})
        keep = 1 if name == current else 0
        if s["agree"] + s["wrong"]:
            return (1, s["agree"], -s["wrong"] - cover.get("wrongOwner", 0), -s["ambiguous"], s["wired"], -cover.get("ambiguous", 0), keep)
        return (0, cover.get("one", 0), -cover.get("ambiguous", 0), 0, 0, 0, keep)
    return max(sorted(scores), key=rank)


# The answer key

def past_plots(plan):
    """The past plots with their generic outline, ramp corners and link insert."""
    found = {}
    for plot in plan["plots"]:
        floors = plot.get("floors") or {}
        generic = ((floors.get("generic") or {}).get("outline") or {}).get("value")
        ramp = (floors.get("ramp") or {}).get("corners", {}).get("value") if floors.get("ramp") else None
        if not generic:
            continue
        loops = generic["loops"]
        found[plot["plot"]] = {"generic": Polygon(loops[0]["points"]).buffer(0), "ramp": ramp, "unit": (plot.get("unit") or {}).get("value")}
    return found


def truth_faces(faces, past, min_share):
    """The face of each past plot, the one that holds the largest share of its generic floor, as label per official number."""
    found = {}
    for official, entry in past.items():
        shares = faces.shares(entry["generic"])
        found[official] = shares[0][1] if shares and shares[0][0] >= min_share else None
    return found


def truth_blocks(past, labels_of, shapes, min_share):
    """For each face label of a past plot, the block that covers the largest share of its generic floor, or None."""
    tree = STRtree(shapes) if shapes else None
    found = {}
    for official, label in labels_of.items():
        if label is None or tree is None:
            continue
        generic = past[official]["generic"]
        best = max(((shapes[k].intersection(generic).area / generic.area, int(k)) for k in tree.query(generic)), default=(0, None))
        found[label] = best[1] if best[0] >= min_share else None
    return found


def ramp_front(ramp, lines):
    """The two corners of a past ramp nearest the curb, and their middle."""
    if not ramp or len(ramp) < 2 or lines.tree is None:
        return None
    ranked = sorted(ramp, key=lambda corner: lines.nearest(corner[0], corner[1])[0])[:2]
    return ranked, ((ranked[0][0] + ranked[1][0]) / 2, (ranked[0][1] + ranked[1][1]) / 2)


# Front distances and levels

def front_middles(faces, labels, curbs, limits):
    """The middle of the front edge of each plot by three methods: the R122 frame, any edge nearest the curb (R94) and none."""
    frame_mid, edge_mid, tied = {}, {}, set()
    for label in labels:
        face = faces.by_label[label]
        frame = cad_links.front_frame(face, curbs.tree, curbs.lines, limits)
        if frame is not None:
            frame_mid[label] = ((frame["left"][0] + frame["right"][0]) / 2, (frame["left"][1] + frame["right"][1]) / 2)
            if frame["tied"]:
                tied.add(label)
        edge = entry_levels.front_edge(face, curbs.tree, curbs.lines) if curbs.tree is not None else None
        if edge is not None:
            edge_mid[label] = edge["mid"]
    return frame_mid, edge_mid, tied


def level_at(surfaces, order, x, y, method, snap, vertex_m, tolerance):
    """The road level at a point by a method, as a list of one value, several for an ambiguous answer, or empty."""
    if method == "snapOrder":
        for name in order:
            z = c3d_connect.snap_z(surfaces[name], x, y, snap)
            if z is not None:
                return [z]
        return []
    if method == "plainOrder":
        for name in order:
            z = surfaces[name].z_at(x, y)
            if z is not None:
                return [z]
        return []
    if method == "snapAll":
        values = [z for z in (c3d_connect.snap_z(surfaces[name], x, y, snap) for name in order) if z is not None]
        if not values:
            return []
        return [values[0]] if max(values) - min(values) <= tolerance else values
    if method == "nearestVertex":
        best = None
        for name in order:
            surface = surfaces[name]
            if surface.count == 0:
                continue
            d = (surface.x - x) ** 2 + (surface.y - y) ** 2
            k = d.argmin()
            if d.flat[k] <= vertex_m ** 2 and (best is None or d.flat[k] < best[0]):
                best = (d.flat[k], float(surface.z.flat[k]))
        return [best[1]] if best else []
    raise ValueError("no level method " + method)


# Drawing to drawing

def block_pairs(first, second, min_overlap):
    """Blocks of two drawings matched by outline overlap over union, best first and one to one."""
    tree = STRtree([b["shape"] for b in second]) if second else None
    pairs = []
    for i, block in enumerate(first):
        for k in (tree.query(block["shape"]) if tree is not None else []):
            other = second[int(k)]["shape"]
            union = block["shape"].union(other).area
            if union > 0:
                ratio = block["shape"].intersection(other).area / union
                if ratio >= min_overlap:
                    pairs.append((ratio, i, int(k)))
    pairs.sort(reverse=True)
    used_a, used_b, counts = set(), set(), Counter()
    for ratio, i, k in pairs:
        if i in used_a or k in used_b:
            continue
        used_a.add(i)
        used_b.add(k)
        counts["sameName" if first[i]["name"] == second[k]["name"] else "otherName"] += 1
    counts["firstUnmatched"] = len(first) - len(used_a)
    counts["secondUnmatched"] = len(second) - len(used_b)
    return dict(counts)


def line_agreement(lines, others, step, bands, inside):
    """Points every step along each line inside Phase 1 and their distance to the other line set, banded."""
    counts = Counter()
    for line in lines:
        n = max(1, int(line.length // step))
        for k in range(n + 1):
            p = line.interpolate(min(k * step, line.length))
            if not inside(p):
                continue
            distance, _ = others.nearest(p.x, p.y)
            if distance is None:
                counts["noOther"] += 1
                continue
            counts[next((name for name, limit in zip(("within%gm" % b for b in bands), bands) if distance <= limit), "worse")] += 1
    return dict(counts)


# Loading

def unit_shapes(blocks, kinds, inside=None):
    found = []
    for block in blocks:
        name = block["name"].split("$")[-1]
        outline = [p[:2] for p in block.get("outline") or []]
        if (kinds is not None and name.split("_")[0] not in kinds) or len(outline) < 3:
            continue
        shape = Polygon(outline).buffer(0)
        if inside is None or inside(shape.centroid):
            found.append({"name": name, "shape": shape, "insert": tuple(block["insertPoint"][:2]), "centre": (shape.centroid.x, shape.centroid.y),
                          "rotation": block.get("rotationDeg"), "mirrored": block.get("mirrored")})
    return found


def setback_shapes(pieces, layer):
    found = []
    for piece in pieces:
        if piece.get("kind") == "polyline" and piece.get("layer") == layer and piece.get("closed") and len(piece.get("pts") or []) >= 3:
            shape = Polygon([p[:2] for p in piece["pts"]]).buffer(0)
            if shape.area > 0:
                found.append(shape)
    return found


def pdf_points(folder):
    """The PDF points of every official plot, with the sheet file name, from pdf_rows.json of the PDF reader."""
    found = defaultdict(lambda: defaultdict(list))
    for path in sorted(Path(folder).glob("*/pdf_rows.json")):
        rows = json.loads(path.read_text(encoding="utf-8"))
        for row in rows["rows"]:
            found[str(row["plot"])][rows["source"]].append((row["e"], row["n"]))
    return found


def schedule_methods(schedules, faces, many, pdf, neighbourhood_code, shifts, search, tolerance):
    """labelShift and labelShiftPdf for every schedule plot, with the edge plots whose shift found differs from the config."""
    keys = sorted(schedules)
    shift_only, with_pdf, edges, failed = [], [], [], []
    for name, official in keys:
        shift = shifts[name]
        label = str(int(official) - shift)
        shift_only.append([label] if label in faces.by_label else [])
        points = [p for sheet, pts in pdf.get(official, {}).items() if neighbourhood_code(sheet) == name for p in pts]
        if label in many and label not in faces.by_label:
            with_pdf.append([])
            continue

        def fits(candidate):
            face = faces.by_label.get(candidate)
            return face is not None and points and all(face.distance(Point(x, y)) <= tolerance for x, y in points)

        if not points:
            with_pdf.append([])
            failed.append((name, official, "no PDF points"))
            continue
        if fits(label):
            with_pdf.append([label])
            continue
        other = next((str(int(official) - s) for d in range(1, search + 1) for s in (shift - d, shift + d) if fits(str(int(official) - s))), None)
        if other is not None:
            with_pdf.append([other])
            edges.append((name, official, int(official) - int(other)))
        else:
            with_pdf.append([])
            failed.append((name, official, "no shift fits"))
    return keys, {"labelShift": shift_only, "labelShiftPdf": with_pdf}, edges, failed


def frames_of(folder, layer):
    return [Polygon(line.coords).buffer(0) for line in data_book.lines_matching(folder, layer) if len(line.coords) > 3]


def evaluate(config, settings):
    book, connect = config["dataBook"], config["connect"]
    wiring = book["wiring"]
    join = book["join"]
    roots = join["fallbackOutputs"]
    root = Path(book["root"])
    border = data_book.border_polygon(root / "phase1_border.csv")
    inside = border.contains
    rows = data_book_analysis.read_csv(root / "plots.csv")
    phase1 = [r["baseplanLabel"] for r in rows]
    types = {r["baseplanLabel"]: r["clientType"] for r in rows}
    officials = {r["baseplanLabel"]: r["officialNumber"] for r in rows if r["officialNumber"]}
    plots = link_summary.load(connect["baseplanPlots"])["plots"]
    faces = Faces(yard_rule.face_shapes(plots, 0)[0])
    many = {str(label) for e in plots if e.get("reason") == "many_labels" for label in e.get("labels") or []}
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    plan = json.loads(Path(connect["plan"]).read_text(encoding="utf-8"))
    past = past_plots(plan)
    shifts = {name: entry["shift"] for name, entry in connect["neighbourhoods"].items() if entry}
    imc = plan.get("neighbourhood")
    truth_face = truth_faces(faces, past, wiring["answerShareMin"])
    label_of_past = {official: label for official, label in truth_face.items() if label is not None}
    key_labels = sorted({label for label in label_of_past.values() if label in set(phase1)}, key=int)
    report = {"sources": {}, "weakSpots": {}, "drawingToDrawing": {}, "chosen": {}, "inputs": {}, "lists": {}}

    # Schedules
    schedules = data_book.schedule_rows(settings, connect)
    pdf = pdf_points(root / wiring["pdfFolder"])
    code_of = lambda sheet: next((name for name in shifts if name in sheet.upper()), None)
    cut = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
    keys, methods, edges, failed = schedule_methods(schedules, faces, many, pdf, code_of, shifts, wiring["shiftSearch"], wiring["pdfToleranceM"])
    found = {}
    for method, wired in methods.items():
        per_key = {"%s %s" % key: labels for key, labels in zip(keys, wired)}
        truth = {"%s %s" % key: (truth_face.get(key[1]) if key[0] == imc else None) for key in keys}
        found[method] = {"key": score(per_key, truth, list(per_key)), "phase1": coverage(by_plot(wired), phase1)}
    report["sources"]["schedules"] = {"methods": found, "edgePlots": len(edges), "edgeOffsets": dict(Counter(e[2] for e in edges)),
                                      "locationFailed": dict(Counter(f[2] for f in failed)), "plots": len(keys),
                                      "locationFailedIn": dict(Counter(name for name, _, _ in failed)),
                                      "locationFailedCutPlots": sum(1 for name, official, _ in failed if official in cut)}
    report["lists"] = {"scheduleEdgePlots": ["%s %s %+d" % e for e in edges], "scheduleLocationFailed": ["%s %s %s" % f for f in failed]}

    # The location plan of Part L, each label at the site point of its fit (R183)
    plan_labels = root / book["locationPlan"]["outFolder"] / "labels.csv"
    if plan_labels.is_file():
        items = [r for r in data_book_analysis.read_csv(plan_labels) if r["east"] and r["north"]]
        points = [(float(r["east"]), float(r["north"])) for r in items]
        truth = {"%s %s" % key: None for key in keys}
        by_label_truth = {}
        for (name, official) in keys:
            label = str(int(official) - shifts[name])
            by_label_truth[label] = official
        wired = {"pointInside": point_inside(faces, points), "nearest": nearest(faces, points, wiring["nearestM"], wiring["tieM"])}
        found = {}
        for method, labels_of_item in wired.items():
            per_face = {label: [items[k]["number"] for k in ks] for label, ks in by_plot(labels_of_item).items()}
            found[method] = {"key": score(per_face, by_label_truth, sorted(by_label_truth, key=int)), "phase1": coverage(per_face, phase1)}
        report["sources"]["locationPlan"] = {"labels": len(items), "methods": found, "note": "key: the 747 schedule plots, label plus shift"}
    else:
        report["sources"]["locationPlan"] = {"missing": "run python/location_plan.py first"}

    # Unit blocks of three drawings
    kinds = set(wiring["unitBlockKinds"])
    block_sources = {}
    for source, (role, override) in {"footprintBlocks": (join["footprint"]["drawing"], None), "r06Blocks": ("masterR06", wiring.get("r06Outlines")),
                                     "r06_0000Blocks": ("masterR06_0000", None)}.items():
        _, earlier = data_book.folder_of(book, role, roots)
        folder = Path(override) if override else earlier
        if folder is None or not (folder / "blocks.json").is_file():
            report["sources"][source] = {"missing": "no block outlines"}
            continue
        blocks = unit_shapes(json.loads((folder / "blocks.json").read_text(encoding="utf-8"))["blocks"], kinds, inside)
        block_sources[source] = blocks
        centres = [b["centre"] for b in blocks]
        shapes = [b["shape"] for b in blocks]
        face_types = {label: {types.get(label)} for label in faces.labels}
        wired = {"centreInside": point_inside(faces, centres), "centreWidened": point_widened(faces, centres, wiring["widenM"]),
                 "insertInside": point_inside(faces, [b["insert"] for b in blocks]), "bestShare": best_share(faces, shapes, wiring["minShare"], wiring["shareTie"]),
                 "everyShare": every_share(faces, shapes, wiring["minShare"]), "nearest": nearest(faces, centres, wiring["nearestM"], wiring["tieM"]),
                 "typeNear": key_near(faces, centres, [b["name"].split("_")[-1] for b in blocks], face_types, wiring["labelNearM"])}
        truth = truth_blocks(past, label_of_past, shapes, wiring["answerShareMin"])
        owns = owner_test(faces, shapes, wiring["minShare"])
        found = {m: {"key": score(by_plot(w), truth, key_labels), "phase1": coverage(by_plot(w), phase1, owns)} for m, w in wired.items()}
        report["sources"][source] = {"blocks": len(blocks), "methods": found}
        if source == "footprintBlocks":
            owner = best_share(faces, shapes, wiring["minShare"], 0)
            widened = by_plot(wired["centreWidened"])
            several = [label for label in phase1 if len(widened.get(label) or []) > 1]
            not_owner = sum(1 for label in several if label not in owner[widened[label][0]])
            stolen = [label for label in phase1 for item in widened.get(label) or [] if not faces.by_label[label].contains(Point(blocks[item]["centre"]))
                      and not owns(label, item)]
            report["weakSpots"]["footprintSeveralBlocks"] = {"plots": len(several), "firstIsNotTheOwner": not_owner}
            report["weakSpots"]["widenedTakesNeighbour"] = {"plots": len(set(stolen)), "widenedHits": sum(1 for label in phase1 for item in widened.get(label) or []
                                                                                                          if not faces.by_label[label].contains(Point(blocks[item]["centre"])))}

    # Numbers of R06 and V9
    for source, role, pattern in (("r06Numbers", "masterR06", None), ("v9Numbers", join["v9"]["drawing"], join["v9"]["plotNumberLayer"])):
        data, earlier = data_book.folder_of(book, role, roots)
        texts = [t for t in data_book.texts_of(data, earlier)[0] if (t.get("text") or "").strip().isdigit() and (pattern is None or fnmatch.fnmatchcase(t.get("layer") or "", pattern))]
        texts = [t for t in texts if inside(Point(t["x"], t["y"]))]
        if not texts:
            report["sources"][source] = {"texts": 0, "methods": {}}
            continue
        points = [(t["x"], t["y"]) for t in texts]
        names = {label: {label, officials.get(label)} for label in faces.labels}
        wired = {"pointInside": point_inside(faces, points), "nearest": nearest(faces, points, wiring["nearestM"], wiring["tieM"]),
                 "labelNear": key_near(faces, points, [t["text"].strip() for t in texts], names, wiring["labelNearM"])}
        found = {m: {"key": score(by_plot(w), {}, key_labels), "phase1": coverage(by_plot(w), phase1)} for m, w in wired.items()}
        report["sources"][source] = {"texts": len(texts), "methods": found, "note": "no answer key, the numbering is its own" if source == "v9Numbers" else ""}

    # Curb and ROW
    _, road_folder = data_book.folder_of(book, join["curb"]["drawing"], roots)
    curbs = c3d_connect.LineSet(data_book.lines_of(road_folder, join["curb"]["layers"]))
    rows_of_way = c3d_connect.LineSet(data_book.lines_of(road_folder, join["row"]["layers"]))
    frame_mid, edge_mid, tied = front_middles(faces, phase1, curbs, limits)
    fronts = {official: ramp_front(entry["ramp"], curbs) for official, entry in past.items()}
    for source, lines in (("curb", curbs), ("row", rows_of_way)):
        truth = {}
        for official, label in label_of_past.items():
            front = fronts.get(official)
            if front is not None and label in key_labels:
                truth[label] = lines.nearest(*front[1])[0]
        values = {"frontFrame": {l: [lines.nearest(*m)[0]] * (2 if l in tied else 1) for l, m in frame_mid.items()},
                  "anyEdge": {l: [lines.nearest(*m)[0]] for l, m in edge_mid.items()},
                  "faceNearest": {l: [float(lines.tree.query_nearest(faces.by_label[l], return_distance=True)[1][0])] for l in phase1} if lines.tree is not None else {}}
        same = lambda value, wanted: abs(value - wanted) <= wiring["frontToleranceM"]
        report["sources"][source] = {"methods": {m: {"key": score(v, truth, key_labels, same), "phase1": coverage(v, phase1)} for m, v in values.items()}}

    # Setback line
    _, foot_folder = data_book.folder_of(book, join["footprint"]["drawing"], roots)
    setbacks = setback_shapes(json.loads((foot_folder / "pieces.json").read_text(encoding="utf-8"))["pieces"], join["footprint"]["setbackLayer"])
    tree = STRtree(setbacks)
    centres = [(s.centroid.x, s.centroid.y) for s in setbacks]
    intersects = {}
    for label in phase1:
        hits = [int(k) for k in tree.query(faces.by_label[label], predicate="intersects")]
        middle = frame_mid.get(label)
        if hits and middle is not None:
            intersects[label] = [min(hits, key=lambda k: setbacks[k].exterior.distance(Point(middle)))]
        elif hits:
            intersects[label] = hits[:1]
    owner = best_share(faces, setbacks, wiring["ownerShareMin"], 0)
    truth = {}
    for official, label in label_of_past.items():
        if label in key_labels:
            c = past[official]["generic"].centroid
            holders = [int(k) for k in tree.query(c, predicate="within")]
            truth[label] = holders[0] if len(holders) == 1 else None
    wired = {"intersects": intersects, "bestShare": by_plot(best_share(faces, setbacks, wiring["ownerShareMin"], wiring["shareTie"])),
             "centroidInside": by_plot(point_inside(faces, centres)), "nearest": by_plot(nearest(faces, centres, wiring["nearestM"], wiring["tieM"]))}
    owns = owner_test(faces, setbacks, wiring["ownerShareMin"])
    report["sources"]["setback"] = {"lines": len(setbacks), "methods": {m: {"key": score(w, truth, key_labels), "phase1": coverage(w, phase1, owns)} for m, w in wired.items()}}
    report["weakSpots"]["setbackOfNeighbour"] = {"plots": sum(1 for label, items in intersects.items() if owner[items[0]] and label not in owner[items[0]]),
                                                 "plotsTouchingSeveral": sum(1 for label in phase1 if len(tree.query(faces.by_label[label], predicate="intersects")) > 1)}

    # Cutsheet frames, no key
    _, cut_folder = data_book.folder_of(book, join["cutsheet"]["drawing"], roots)
    frames = frames_of(cut_folder, join["cutsheet"]["frameLayer"])
    frame_tree = STRtree(frames)
    centre_in, share_in, nearest_frame = {}, {}, {}
    for label in phase1:
        face = faces.by_label[label]
        centre_in[label] = [int(k) for k in frame_tree.query(face.centroid, predicate="within")]
        shares = sorted(((frames[int(k)].intersection(face).area / face.area, int(k)) for k in frame_tree.query(face)), reverse=True)
        share_in[label] = [shares[0][1]] if shares and shares[0][0] >= wiring["minShare"] else []
        k = frame_tree.query_nearest(face.centroid)
        nearest_frame[label] = [int(k[0])] if len(k) else []
    report["sources"]["cutsheet"] = {"frames": len(frames), "methods": {m: {"key": score(w, {}, key_labels), "phase1": coverage(w, phase1)}
                                                                        for m, w in (("centroidIn", centre_in), ("bestShare", share_in), ("nearestFrame", nearest_frame))},
                                     "note": "no answer key for the cutsheet frame"}

    # Road surfaces at the past ramp front corners
    surfaces = {name: surface_lookup.load_surface(connect["landxmlDir"], name) for name in connect["surfaces"]}
    order = [name for name in book["roadSurfaceOrder"] if name in surfaces]
    corner_key = {}
    for official, label in label_of_past.items():
        front = fronts.get(official)
        if front is not None and label in key_labels:
            for side, corner in enumerate(front[0]):
                corner_key["%s %d" % (label, side)] = corner
    found = {}
    for method in wiring["levelMethods"]:
        values = {key: level_at(surfaces, order, corner[0], corner[1], method, connect["snapM"], wiring["nearestVertexM"], wiring["levelToleranceM"])
                  for key, corner in corner_key.items()}
        truth = {key: corner[2] for key, corner in corner_key.items()}
        found[method] = {"key": score(values, truth, sorted(corner_key), lambda v, w: abs(v - w) <= wiring["levelToleranceM"])}
    report["sources"]["roadSurfaces"] = {"corners": len(corner_key), "methods": found}

    # Many labels and labels outside any face
    base_texts = json.loads((Path(connect["baseplanPlots"]).parent / "texts.json").read_text(encoding="utf-8"))["texts"]
    labels = {n: v for n, v in label_texts(base_texts, wiring["labelLayer"]).items() if inside(Point(v[0], v[1]))}
    rescued = rescue([e for e in plots if e.get("kind") != "other_face" or inside(Polygon([v[:2] for v in e["vertices"]]).buffer(0).centroid)],
                     faces, block_sources.get("footprintBlocks") or [], labels, wiring["labelNearM"])
    report["weakSpots"]["rescue"] = rescued["labels"]
    report["lists"].update({"manyLabelListed": rescued["listed"]["manyLabels"], "outsideListed": rescued["listed"]["outside"]})

    # Drawing to drawing
    d2d = report["drawingToDrawing"]
    if "footprintBlocks" in block_sources and "r06Blocks" in block_sources:
        d2d["footprintAgainstR06"] = block_pairs(block_sources["footprintBlocks"], block_sources["r06Blocks"], wiring["blockOverlapMin"])
    if "r06Blocks" in block_sources and "r06_0000Blocks" in block_sources:
        d2d["r06AgainstR06_0000"] = block_pairs(block_sources["r06Blocks"], block_sources["r06_0000Blocks"], wiring["blockOverlapMin"])
    _, base_folder = data_book.folder_of(book, wiring["baseCurb"]["drawing"], roots)
    base_curbs = c3d_connect.LineSet(data_book.lines_of(base_folder, wiring["baseCurb"]["layers"]))
    d2d["curbRoadsAgainstBasePlans"] = line_agreement(curbs.lines, base_curbs, wiring["curbStepM"], wiring["curbBandsM"], inside)
    d2d["curbBasePlansAgainstRoads"] = line_agreement(base_curbs.lines, curbs, wiring["curbStepM"], wiring["curbBandsM"], inside)

    for source, entry in report["sources"].items():
        if entry.get("methods"):
            report["chosen"][source] = pick(entry["methods"], wiring["current"].get(source))
    report["inputs"] = {"keyPlots": len(key_labels), "pastPlots": len(past), "pastWithoutFace": sum(1 for v in truth_face.values() if v is None),
                        "tables": "C3DProbe for Road Layout, Road Surface and Rough Grading, CadReader for the rest, round 12 CadReader output for the geometry"}
    return report


def label_texts(texts, layer):
    """The plot label texts of the baseplan: number from the last line, type from the lines before it."""
    found = {}
    for text in texts:
        if text.get("layer") != layer:
            continue
        lines = [line.strip() for line in str(text.get("text") or "").splitlines() if line.strip()]
        if lines and lines[-1].isdigit():
            found.setdefault(lines[-1], (text["x"], text["y"], " ".join(lines[:-1])))
    return found


def rescue(plots, faces, blocks, labels, max_distance):
    """A label in a face with many labels is rescued when exactly one footprint unit block of that face lies nearer to it than
    to any other label of the face. A label outside every face is rescued by the nearest face with no label of its own within
    max_distance. The rest are listed by number."""
    counts, listed = Counter(), {"manyLabels": [], "outside": []}
    many_faces = [e for e in plots if e.get("reason") == "many_labels"]
    for entry in many_faces:
        face = Polygon([v[:2] for v in entry.get("vertices") or []]).buffer(0)
        numbers = [str(label) for label in entry.get("labels") or []]
        points = {n: labels[n][:2] for n in numbers if n in labels}
        claimed = Counter()
        for block in (b for b in blocks if face.contains(Point(b["centre"]))):
            if points:
                claimed[min(points, key=lambda n: math.dist(points[n], block["centre"]))] += 1
        for number in numbers:
            ok = claimed[number] == 1
            counts["manyLabelRescued" if ok else "manyLabelListed"] += 1
            if not ok:
                listed["manyLabels"].append(number)
    in_many = {str(label) for e in many_faces for label in e.get("labels") or []}
    open_faces = [Polygon([v[:2] for v in e.get("vertices") or []]).buffer(0) for e in plots
                  if (e.get("reason") == "no_label" or e.get("kind") == "plot_open") and len(e.get("vertices") or []) >= 3]
    for number, (x, y, _) in sorted(labels.items(), key=lambda item: int(item[0])):
        if number in faces.by_label or number in in_many:
            continue
        near = [shape for shape in open_faces if shape.distance(Point(x, y)) <= max_distance]
        counts["outsideRescued" if len(near) == 1 else "outsideListed"] += 1
        if len(near) != 1:
            listed["outside"].append(number)
    counts["manyLabelFaces"] = len(many_faces)
    return {"labels": dict(counts), "listed": listed}


def markdown(report):
    lines = ["# Wiring between the drawings", "", "Generated by python/wiring.py (R174 to R178). Counts only.", "", "## Chosen method per source", ""]
    lines += ["| Source | Methods tried | Chosen | Score of the chosen (wired, agree, ambiguous, wrong, unwired) |", "|---|---|---|---|"]
    for source, entry in report["sources"].items():
        methods = entry.get("methods") or {}
        chosen = report["chosen"].get(source)
        s = methods.get(chosen, {}).get("key", {}) if chosen else {}
        lines.append("| %s | %s | %s | %s |" % (source, ", ".join(methods) or "none", chosen or "none",
                                                ", ".join(str(s.get(k, "")) for k in ("wired", "agree", "ambiguous", "wrong", "unwired")) if s else ""))
    for key in ("sources", "weakSpots", "drawingToDrawing", "inputs", "lists"):
        lines += ["", "## " + key, "", "```", json.dumps(report[key], indent=1, ensure_ascii=False), "```"]
    return "\n".join(lines) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description="Score the ways each source wires to the baseplan plots (R174 to R178)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    report = evaluate(config, settings)
    root = Path(config["dataBook"]["root"])
    (root / "wiring.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    (root / "wiring.md").write_text(markdown(report), encoding="utf-8")
    configured = config["dataBook"]["wiring"].get("chosen") or {}
    differ = sorted(source for source, method in report["chosen"].items() if configured.get(source) != method)
    print("dataBook.wiring.chosen differs from the score on: " + ", ".join(differ) if differ else "dataBook.wiring.chosen equals the score")
    print(markdown(report).split("## sources")[0])
    print(json.dumps({k: report[k] for k in ("weakSpots", "drawingToDrawing", "inputs")}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
