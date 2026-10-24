"""The Master Plan Rev 9 drawing and its PDF, Part R of round DATA-3 (R189 to R191).

The DWG copy is read by CadReader into dataBook.rev9.drawing.cadOut, exploded with no layer filter, and its tables inside the
Phase 1 border are written by data_book_cad.py like every other drawing. The plot numbers of the DWG sit in Civil 3D parcels,
which CadReader does not see, so its class list is counted, and the numbers and codes come from the PDF plot of the same
drawing. Its labels are read like the location plan, fitted to the site on the faces whose location plan number is known,
wired to the baseplan faces by the methods of Part W and scored against the 747 schedule plots. The plot lines the DWG holds
outside the parcels are polygonized and counted, and the filled land use polygons of the PDF, taken to the site by the same
fit, are the Rev 9 plot faces matched with the baseplan faces by overlap over union. Rows go to the data book, counts to
rev9_drawing.json and rev9_drawing.md there.
"""
import argparse
import csv
import fnmatch
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import pdfplumber
from shapely.geometry import LineString, Point
from shapely.ops import polygonize, unary_union
from shapely.strtree import STRtree

import c3d_run
import data_book
import data_book_analysis
import data_book_cad
import link_summary
import location_plan
import wiring
import yard_rule


def layer_match(name, patterns):
    return any(fnmatch.fnmatch(name.lower(), p.lower()) for p in patterns)


def plot_faces(pieces, layers, border, min_area):
    """The faces the plot lines close, kept when their inner point lies inside the border and they reach min_area."""
    lines = []
    for piece in pieces:
        if piece.get("kind") == "insert" or not layer_match(piece.get("layer") or "", layers):
            continue
        points, _, _ = data_book_cad.piece_line(piece)
        if points and len(points) > 1:
            lines.append(LineString([p[:2] for p in points]))
    faces = [f for f in polygonize(unary_union(lines)) if f.area >= min_area and border.contains(f.representative_point())] if lines else []
    return faces, len(lines)


def pdf_faces(objects, matrix, style, border, min_area):
    """The faces the plot strokes of the PDF close, taken to the site by the fit. The plot strokes are the unfilled lines and curves
    of one colour and width, the style the parcels are plotted in."""
    lines = []
    for kind in ("line", "curve"):
        for item in objects.get(kind, []):
            if item.get("fill") or str(item.get("stroking_color")) != style["colour"] or abs((item.get("linewidth") or 0) - style["width"]) > 1e-6:
                continue
            if len(item.get("pts") or []) > 1:
                lines.append(LineString(location_plan.apply(matrix, item["pts"])))
    faces = [f for f in polygonize(unary_union(lines)) if f.area >= min_area and border.contains(f.representative_point())] if lines else []
    return faces, len(lines)


def refine(faces, base, matches, min_iou, settings):
    """A second affine fit from the centroid of each matched PDF face to the centroid of its baseplan face. The first fit is on
    label points, which sit a fixed small distance off the line work."""
    pairs = [(faces[m["face"]].centroid, base[label].centroid) for label, m in matches.items() if m["face"] is not None and m["iou"] >= min_iou]
    if len(pairs) < 3:
        return None, {"points": len(pairs)}
    correction, residual, keep = location_plan.robust_fit([(a.x, a.y) for a, _ in pairs], [(b.x, b.y) for _, b in pairs],
                                                          settings["fitDropFactor"], settings["fitRounds"], settings["fitFloorM"])
    sources = np.asarray([(a.x, a.y) for a, _ in pairs])
    move = np.median(location_plan.apply(correction, sources) - sources, axis=0)
    return correction, {"points": len(pairs), "kept": len(keep), "residualKeptM": location_plan.stats(residual[keep]),
                        "medianMoveM": [round(float(v), 3) for v in move]}


def moved(faces, correction):
    from shapely.geometry import Polygon
    return [Polygon(location_plan.apply(correction, list(f.exterior.coords))) for f in faces]


def face_match(base, other, same_share, match_share):
    """Each baseplan face with the face of the other drawing of the largest overlap over union: same, shapeDiffers or noFace."""
    labels = list(base)
    tree = STRtree(other) if other else None
    found = {}
    for label in labels:
        face = base[label]
        best, best_iou = None, 0.0
        if tree is not None:
            for k in tree.query(face):
                shape = other[int(k)]
                inter = face.intersection(shape).area
                if inter <= 0:
                    continue
                iou = inter / face.union(shape).area
                if iou > best_iou:
                    best, best_iou = int(k), iou
        kind = "same" if best_iou >= same_share else "shapeDiffers" if best_iou >= match_share else "noFace"
        found[label] = {"face": best, "iou": round(best_iou, 4), "class": kind,
                        "areaDifferenceM2": round(other[best].area - face.area, 3) if best is not None else None}
    return found


def classes(layers_json):
    """The DWG classes with instances, the Civil 3D ones apart (R189)."""
    entries = layers_json.get("classes") or []
    civil = {c["dxfName"]: c["instanceCount"] for c in entries if c["dxfName"].startswith("AECC") and c["instanceCount"]}
    shapes = {c["dxfName"]: c["instanceCount"] for c in entries if c["isAnEntity"] and c["instanceCount"]}
    return {"classes": len(entries), "civilClasses": sum(1 for c in entries if c["dxfName"].startswith("AECC")),
            "civilObjects": sum(civil.values()), "entityClassesWithInstances": shapes}


def survey(tables, settings, words, code_pattern):
    """Layers, texts and lines of the tables inside Phase 1: plot number texts, unit code texts, setback lines and labels that
    name a zone or neighbourhood."""
    texts = data_book_analysis.read_csv(tables / "texts.csv")
    layers = data_book_analysis.read_csv(tables / "layers.csv")
    used = [l for l in layers if int(l["entitiesInside"] or 0) > 0]
    numbers = [t for t in texts if re.fullmatch(r"\d{1,4}", (t["text"] or "").strip())]
    on_layer = [t for t in texts if layer_match(t["layer"], settings["unitCodeLayers"])]
    clusters = [t for t in on_layer if re.fullmatch(settings["clusterCodePattern"], (t["text"] or "").strip())]
    codes = [t for t in on_layer if t not in clusters and re.fullmatch(code_pattern, (t["text"] or "").strip())]
    copies = [t for t in texts if layer_match(t["layer"], settings["unitCodeCopyLayers"])]
    setback_layers = [l["layer"] for l in used if layer_match(l["layer"], settings["setbackLayers"])]
    boundary_layers = [l["layer"] for l in used if layer_match(l["layer"], settings["boundaryLayers"])]
    hits = {}
    for word in words:
        pattern = re.compile(r"\b" + re.escape(word) + r"\b", re.I)
        hits[word] = {"texts": sum(1 for t in texts if pattern.search(t["text"] or "")), "layers": sum(1 for l in layers if pattern.search(l["layer"]))}
    return {"layersInside": len(used), "layers": len(layers), "texts": len(texts),
            "textsByLayer": dict(Counter(t["layer"] for t in texts).most_common(15)),
            "numberTexts": len(numbers), "numberTextsByLayer": dict(Counter(t["layer"] for t in numbers).most_common(5)),
            "unitCodeTexts": len(codes), "unitCodes": dict(Counter(t["text"].strip() for t in codes).most_common()),
            "clusterCodeTexts": len(clusters), "clusterCodes": dict(Counter(t["text"].strip() for t in clusters).most_common()),
            "unitCodeCopies": len(copies), "setbackLayers": setback_layers, "boundaryLayers": boundary_layers, "words": hits}, codes


def pdf_labels(path, settings, patterns):
    """The plot labels of the PDF plot: text lines in the label band classed and paired code over number, and the title lines."""
    with pdfplumber.open(path) as pdf:
        page = pdf.pages[0]
        meta = {"pages": len(pdf.pages), "width": page.width, "height": page.height, "rotation": page.rotation,
                "title": (pdf.metadata or {}).get("Title"), "creator": (pdf.metadata or {}).get("Creator"), "created": (pdf.metadata or {}).get("CreationDate")}
        chars = page.chars
        lines = location_plan.text_lines(chars, settings["labelSize"], settings["joinGapPt"], settings["lineTolerancePt"])
        big = location_plan.text_lines(chars, settings["titleSize"], settings["titleGapPt"], settings["lineTolerancePt"])
    for line in lines:
        line["kind"] = location_plan.classify(line["text"], patterns)
    codes = [l for l in lines if l["kind"] in settings["codeKinds"]]
    numbers = [l for l in lines if l["kind"] == "number"]
    pairs, lone_codes, lone_numbers = location_plan.pair_labels(codes, numbers, settings["pairDyPt"], settings["pairDxPt"])
    labels = [{"code": codes[i]["text"], "kind": codes[i]["kind"], "number": numbers[k]["text"],
               "x": (codes[i]["x"] + numbers[k]["x"]) / 2, "y": (codes[i]["top"] + numbers[k]["bottom"]) / 2} for i, k in pairs]
    meta["titleLines"] = [l["text"] for l in big]
    counts = {"lines": dict(Counter(l["kind"] for l in lines)), "pairs": len(pairs), "codesWithoutNumber": len(lone_codes),
              "numbersWithoutCode": len(lone_numbers), "duplicateNumbers": sum(1 for n, c in Counter(l["number"] for l in labels).items() if c > 1),
              "codes": dict(Counter(l["code"] for l in labels if l["kind"] == "clientCode").most_common())}
    return labels, meta, counts


def site_points(plots, faces):
    """The centroid of the baseplan face of each location plan number held by one face only."""
    found, seen = {}, Counter()
    for row in plots:
        numbers = [n for n in (row["locationPlanNumber"] or "").split("|") if n]
        if len(numbers) == 1 and row["baseplanLabel"] in faces:
            seen[numbers[0]] += 1
            found[numbers[0]] = faces[row["baseplanLabel"]].centroid
    return {k: v for k, v in found.items() if seen[k] == 1}


def fit_page(labels, known, settings):
    by_number = defaultdict(list)
    for label in labels:
        by_number[label["number"]].append(label)
    page, site = [], []
    for number, point in known.items():
        found = by_number.get(number) or []
        if len(found) == 1:
            page.append((found[0]["x"], found[0]["y"]))
            site.append((point.x, point.y))
    matrix, residual, keep = location_plan.robust_fit(page, site, settings["fitDropFactor"], settings["fitRounds"], settings["fitFloorM"])
    return matrix, {"points": len(page), "kept": len(keep), "residualKeptM": location_plan.stats(residual[keep]), "residualAllM": location_plan.stats(residual),
                    "metresPerPoint": round(float(np.sqrt(abs(np.linalg.det(matrix[:2, :2])))), 4)}


def base_of(code, pattern):
    found = re.fullmatch(pattern, (code or "").strip())
    return found.group("base") if found else (code or "").strip() or None


def run(config):
    book = config["dataBook"]
    settings = book["rev9"]["drawing"]
    location = book["locationPlan"]
    root = Path(book["root"])
    out = root / book["rev9"]["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    border = data_book.border_polygon(root / "phase1_border.csv")
    cad = root / settings["cadOut"]
    tables = root / (Path(book["drawings"][settings["role"]]).stem)
    pattern = location["patterns"]["clientCode"]
    report = {"classes": classes(json.loads((cad / "layers.json").read_text(encoding="utf-8")))}
    report["survey"], code_texts = survey(tables, settings, location["searchWords"] + settings["extraWords"], pattern)

    plots = data_book_analysis.read_csv(root / "plots.csv")
    row_of = {r["baseplanLabel"]: r for r in plots}
    base = yard_rule.face_shapes(link_summary.load(config["connect"]["baseplanPlots"])["plots"], 0)[0]
    base = {k: v for k, v in base.items() if k in row_of and border.contains(v.representative_point())}
    unit_types = set(book["analysis"]["unitPlotTypes"])

    pieces = json.loads((cad / "pieces.json").read_text(encoding="utf-8"))["pieces"]
    dwg_faces, plot_lines = plot_faces(pieces, settings["plotLayers"], border, settings["minFaceAreaM2"])
    dwg_all, _ = plot_faces(pieces, settings["plotLayers"], border.buffer(1e7), settings["minFaceAreaM2"])
    del pieces

    labels, meta, counts = pdf_labels(root / "work" / settings["pdf"], settings, location["patterns"])
    report["pdf"] = {"sheet": meta, **counts}
    matrix, fit = fit_page(labels, site_points(plots, base), settings)
    report["pdf"]["fit"] = fit
    with pdfplumber.open(root / "work" / settings["pdf"]) as pdf:
        rev9_faces, strokes = pdf_faces(pdf.pages[0].objects, matrix, settings["plotStroke"], border, settings["minFaceAreaM2"])
    first = face_match(base, rev9_faces, settings["sameShareMin"], settings["matchShareMin"])
    correction, refit = refine(rev9_faces, base, first, settings["refineIouMin"], settings)
    if correction is not None:
        rev9_faces = moved(rev9_faces, correction)
    matches = face_match(base, rev9_faces, settings["sameShareMin"], settings["matchShareMin"])
    for label, m in matches.items():
        m["hausdorffM"] = round(rev9_faces[m["face"]].hausdorff_distance(base[label]), 3) if m["face"] is not None else None
    unit_labels = [k for k in base if row_of[k]["clientType"] in unit_types]
    report["faces"] = {"dwgPlotLines": plot_lines, "dwgFacesInsidePhase1": len(dwg_faces), "dwgFacesAnywhere": len(dwg_all),
                       "pdfPlotStrokes": strokes, "pdfFaces": len(rev9_faces), "refit": refit, "baseplanFaces": len(base),
                       "unitFirstMatch": dict(Counter(first[k]["class"] for k in unit_labels)),
                       "unitHausdorffM": location_plan.stats([matches[k]["hausdorffM"] for k in unit_labels if matches[k]["face"] is not None]),
                       "all": dict(Counter(m["class"] for m in matches.values())),
                       "unitPlots": dict(Counter(matches[k]["class"] for k in unit_labels)),
                       "unitIou": location_plan.stats([matches[k]["iou"] for k in unit_labels if matches[k]["class"] != "noFace"]),
                       "unitAreaDifferenceM2": location_plan.stats([abs(matches[k]["areaDifferenceM2"]) for k in unit_labels if matches[k]["class"] != "noFace"])}
    points = [tuple(p) for p in location_plan.apply(matrix, [(l["x"], l["y"]) for l in labels])] if labels else []

    faces = wiring.Faces(base)
    official = {k: r["officialNumber"] for k, r in row_of.items() if r["officialNumber"]}
    key_plots = sorted(official, key=int)
    methods = {"pointInside": wiring.point_inside(faces, points), "nearest": wiring.nearest(faces, points, settings["nearestM"], settings["tieM"])}
    numbers = [l["number"] for l in labels]
    scores = {}
    for name, found in methods.items():
        per_plot = {plot: [numbers[i] for i in items] for plot, items in wiring.by_plot(found).items()}
        scores[name] = {"key": wiring.score(per_plot, official, key_plots), "phase1": wiring.coverage(per_plot, list(base))}
    chosen = wiring.pick(scores, settings["current"])
    report["wiring"] = {"labels": scores, "chosen": chosen}

    code_points = [(float(t["x"]), float(t["y"])) for t in code_texts]
    code_values = [t["text"].strip() for t in code_texts]
    truth_codes = {k: r["locationPlanCode"] for k, r in row_of.items() if r["locationPlanCode"] and "|" not in r["locationPlanCode"]}
    code_scores = {}
    for name, found in {"pointInside": wiring.point_inside(faces, code_points),
                        "nearest": wiring.nearest(faces, code_points, settings["nearestM"], settings["tieM"])}.items():
        per_plot = {plot: [code_values[i] for i in items] for plot, items in wiring.by_plot(found).items()}
        code_scores[name] = {"key": wiring.score(per_plot, truth_codes, sorted(truth_codes, key=int), lambda a, b: base_of(a, pattern) == base_of(b, pattern)),
                             "phase1": wiring.coverage(per_plot, list(base))}
    report["wiring"]["unitCodeTexts"] = code_scores
    report["wiring"]["unitCodeChosen"] = wiring.pick(code_scores, settings["current"])

    per_plot = {plot: [i for i in items] for plot, items in wiring.by_plot(methods[chosen]).items()}
    code_per_plot = {plot: [code_values[i] for i in items] for plot, items in
                     wiring.by_plot(wiring.point_inside(faces, code_points) if report["wiring"]["unitCodeChosen"] == "pointInside"
                                    else wiring.nearest(faces, code_points, settings["nearestM"], settings["tieM"])).items()}
    rows, number_check, code_check, old_check = [], Counter(), Counter(), Counter()
    for label in unit_labels:
        row = row_of[label]
        hits = per_plot.get(label) or []
        number = labels[hits[0]]["number"] if len(hits) == 1 else None
        code = labels[hits[0]]["code"] if len(hits) == 1 else None
        plan_numbers = [n for n in (row["locationPlanNumber"] or "").split("|") if n]
        number_class = "noLabel" if not hits else "manyLabels" if len(hits) > 1 else "noPlanNumber" if len(plan_numbers) != 1 \
            else "same" if number == plan_numbers[0] else "differs"
        code_class = "noLabel" if code is None else "noTypologyRev9" if not row["typologyRev9"] or "|" in row["typologyRev9"] \
            else "same" if code == row["typologyRev9"] else "differs"
        texts = code_per_plot.get(label) or []
        old_class = "noText" if not texts else "manyTexts" if len(set(texts)) > 1 else "noPlanCode" if label not in truth_codes \
            else "same" if base_of(texts[0], pattern) == base_of(truth_codes[label], pattern) else "differs"
        number_check[number_class] += 1
        code_check[code_class] += 1
        old_check[old_class] += 1
        rows.append({"baseplanLabel": label, "clientType": row["clientType"], "neighbourhood": row["neighbourhood"], "faceClass": matches[label]["class"],
                     "iou": matches[label]["iou"], "areaDifferenceM2": matches[label]["areaDifferenceM2"], "hausdorffM": matches[label]["hausdorffM"], "rev9Number": number,
                     "locationPlanNumber": row["locationPlanNumber"], "numberClass": number_class, "rev9Code": code, "typologyRev9": row["typologyRev9"],
                     "codeClass": code_class, "rev9UnitText": "|".join(sorted(set(texts))), "locationPlanCode": row["locationPlanCode"], "unitTextClass": old_class})
    report["compare"] = {"unitPlots": len(rows), "number": dict(number_check), "code": dict(code_check), "unitText": dict(old_check),
                         "codeDiffers": dict(Counter("%s>%s" % (r["typologyRev9"], r["rev9Code"]) for r in rows if r["codeClass"] == "differs").most_common()),
                         "anyDifference": sum(1 for r in rows if r["faceClass"] != "same" or r["numberClass"] == "differs" or r["codeClass"] == "differs"
                                              or r["unitTextClass"] == "differs")}
    with open(out / "rev9_faces.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(rows[0]) if rows else ["baseplanLabel"], lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    outlines = {label: [[round(x, 3), round(y, 3)] for x, y in rev9_faces[matches[label]["face"]].exterior.coords]
                for label in unit_labels if matches[label]["face"] is not None}
    (out / "rev9_face_outlines.json").write_text(json.dumps(outlines), encoding="utf-8")
    with open(out / "rev9_labels.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["number", "code", "kind", "pageX", "pageY", "east", "north", "face"])
        for label, (east, north), held in zip(labels, points, methods["pointInside"]):
            writer.writerow([label["number"], label["code"], label["kind"], round(label["x"], 3), round(label["y"], 3), round(east, 3), round(north, 3), "|".join(held)])
    (out / "rev9_drawing.json").write_text(json.dumps(report, indent=1, default=str), encoding="utf-8")
    (out / "rev9_drawing.md").write_text("# Rev 9 drawing, round DATA-3 Part R\n\nCounts only.\n\n```\n" + json.dumps(report, indent=1, default=str) + "\n```\n", encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The Rev 9 master plan drawing and its PDF against the baseplan (R189 to R191)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"faces": report["faces"]["unitPlots"], "fit": report["pdf"]["fit"]["residualKeptM"], "wiring": report["wiring"]["chosen"],
                      "compare": report["compare"]}, default=str))
    return 0


if __name__ == "__main__":
    sys.exit(main())
