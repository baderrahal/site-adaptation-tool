"""The footprint unit blocks on no baseplan plot, Part U of round DATA-3 (R194).

R171 found unit blocks of the footprint drawing whose outline no labelled baseplan face holds by unitShareMin. For each such
block this says how many units it stands for (the usual cover of a block of its name), what baseplan shape holds its centre
(a face with many labels, a face with no label or nothing), how far the nearest labelled face and the nearest unit face lie,
the Zone and NBH of the plot number the block holds (or of the nearest location plan label) by the DLUS sheet of
dataBook.strayUnits.nbhLookup, which has a row for every plot number where the sales sheet has not, and whether a label of the location plan or of the Rev 9 plot lies inside
the block, which means that plan has a plot there. Rows go to the data book, counts to stray_units.json there.
"""
import argparse
import csv
import json
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import Point, Polygon
from shapely.strtree import STRtree

import c3d_run
import data_book
import data_book_analysis
import data_book_retest
import link_summary
import location_plan
import rev9_workbooks
import yard_rule


def other_shapes(plots):
    """The baseplan shapes that are no labelled face: faces with many labels, faces with no label and other faces."""
    found = []
    for entry in plots:
        vertices = entry.get("vertices") or []
        kind = entry.get("reason") if entry.get("kind") == "plot_shape" and entry.get("reason") else entry.get("kind") if entry.get("kind") == "other_face" else None
        if kind is None or len(vertices) < 3:
            continue
        shape = Polygon([(v[0], v[1]) for v in vertices]).buffer(0)
        if not shape.is_empty:
            found.append((kind, shape))
    return found


def label_points(rows, east, north):
    points = []
    for row in rows:
        x, y = data_book_analysis.number(row.get(east)), data_book_analysis.number(row.get(north))
        if x is not None and y is not None:
            points.append((Point(x, y), row))
    return points


def inside(shape, points, tree, buffer):
    if tree is None:
        return []
    return [points[int(k)][1] for k in tree.query(shape.buffer(buffer), predicate="contains")]


def nearest(shape, points, tree):
    if tree is None or not points:
        return None, None
    k = int(tree.query_nearest(shape.centroid)[0])
    return points[k][1], round(points[k][0].distance(shape.centroid), 2)


def describe(blocks, per_block, usual, faces, unit_faces, others, location, rev9, buffer, nbh_of):
    """One row per block on no labelled face."""
    face_labels = sorted(faces)
    face_tree = STRtree([faces[k] for k in face_labels])
    unit_labels = sorted(unit_faces)
    unit_tree = STRtree([unit_faces[k] for k in unit_labels]) if unit_labels else None
    other_tree = STRtree([s for _, s in others]) if others else None
    location_tree = STRtree([p for p, _ in location]) if location else None
    rev9_tree = STRtree([p for p, _ in rev9]) if rev9 else None
    rows = []
    for block, (name, hits) in zip(blocks, per_block):
        if hits:
            continue
        shape = block["shape"]
        centre = shape.centroid
        holder = [others[int(k)][0] for k in other_tree.query(centre, predicate="within")] if other_tree is not None else []
        k = int(face_tree.query_nearest(shape)[0])
        u = int(unit_tree.query_nearest(shape)[0]) if unit_tree is not None else None
        best = max(((faces[label].intersection(shape).area / shape.area, label) for label in (face_labels[int(i)] for i in face_tree.query(shape))), default=(0.0, None))
        plan_in = inside(shape, location, location_tree, buffer)
        rev9_in = inside(shape, rev9, rev9_tree, buffer)
        near, near_m = nearest(shape, location, location_tree)
        number = plan_in[0]["number"] if plan_in else near["number"] if near else None
        zone, nbh = nbh_of.get(number, (None, None))
        rows.append({"block": name, "units": usual.get(name, 0), "holder": holder[0] if holder else "none",
                     "nearestFaceM": round(faces[face_labels[k]].distance(shape), 2), "nearestUnitFaceM": round(unit_faces[unit_labels[u]].distance(shape), 2) if u is not None else None,
                     "bestShare": round(best[0], 3), "locationLabelsInside": len(plan_in), "locationNumbers": "|".join(r["number"] for r in plan_in),
                     "locationCodes": "|".join(r["code"] for r in plan_in), "rev9LabelsInside": len(rev9_in), "rev9Numbers": "|".join(r["number"] for r in rev9_in),
                     "rev9Codes": "|".join(r["code"] for r in rev9_in), "nearestLabelM": near_m, "nbh": nbh, "zone": zone,
                     "salesNbh": near.get("nbh") if near else None})
    return rows


def summary(rows):
    def total(items):
        return {"blocks": len(items), "units": sum(r["units"] for r in items)}
    return {**total(rows),
            "byBlock": dict(Counter(r["block"] for r in rows).most_common()),
            "byHolder": {h: total([r for r in rows if r["holder"] == h]) for h in sorted({r["holder"] for r in rows})},
            "byZoneNbh": {"%s %s" % (r["zone"], r["nbh"]): total([x for x in rows if (x["zone"], x["nbh"]) == (r["zone"], r["nbh"])]) for r in rows},
            "nearestFaceM": location_plan.stats([r["nearestFaceM"] for r in rows]),
            "nearestUnitFaceM": location_plan.stats([r["nearestUnitFaceM"] for r in rows if r["nearestUnitFaceM"] is not None]),
            "withSalesRow": total([r for r in rows if r["salesNbh"]]),
            "withLocationPlot": total([r for r in rows if r["locationLabelsInside"]]),
            "withRev9Plot": total([r for r in rows if r["rev9LabelsInside"]]),
            "withNeither": total([r for r in rows if not r["locationLabelsInside"] and not r["rev9LabelsInside"]]),
            "locationCodes": dict(Counter(c for r in rows for c in r["locationCodes"].split("|") if c).most_common()),
            "rev9Codes": dict(Counter(c for r in rows for c in r["rev9Codes"].split("|") if c).most_common())}


def run(config):
    book = config["dataBook"]
    retest = book["retest"]
    settings = book["strayUnits"]
    root = Path(book["root"])
    rows = data_book_analysis.read_csv(root / "plots.csv")
    phase1 = {r["baseplanLabel"]: r for r in rows}
    border = data_book.border_polygon(root / "phase1_border.csv")
    plots = link_summary.load(config["connect"]["baseplanPlots"])["plots"]
    faces = {label: face for label, face in yard_rule.face_shapes(plots, 0)[0].items() if label in phase1}
    unit_types = set(book["analysis"]["unitPlotTypes"])
    unit_faces = {label: face for label, face in faces.items() if phase1[label]["clientType"] in unit_types}
    types = {label: phase1[label]["clientType"] for label in faces}
    _, earlier = data_book.folder_of(book, book["join"]["footprint"]["drawing"], book["join"]["fallbackOutputs"])
    blocks = data_book_retest.unit_blocks(json.loads((earlier / "blocks.json").read_text(encoding="utf-8"))["blocks"], set(retest["unitBlockKinds"]), border.contains)
    per_face, per_block = data_book_retest.units_on_faces(faces, blocks, retest["unitShareMin"])
    units = data_book_retest.unit_count(per_face, per_block, types, unit_types)
    usual = {name: sum(entry["usualCover"].get(t, 0) for t in unit_types) for name, entry in units["blocksByName"].items()}
    location = label_points(data_book_analysis.read_csv(root / book["locationPlan"]["outFolder"] / "labels.csv"), "east", "north")
    rev9 = label_points(data_book_analysis.read_csv(root / book["rev9"]["outFolder"] / "rev9_labels.csv"), "east", "north")
    lookup = settings["nbhLookup"]
    dlus = rev9_workbooks.sheet_rows(root / "work" / book["rev9"]["workbooks"][lookup["workbook"]], lookup["sheet"], lookup["headerRow"], lookup["columns"])
    nbh_of = {str(int(r["plot"])) if isinstance(r["plot"], (int, float)) else str(r["plot"]): (r["zone"], r["nbh"]) for r in dlus}
    found = describe(blocks, per_block, usual, faces, unit_faces, other_shapes(plots), location, rev9, settings["labelBufferM"], nbh_of)
    report = {"r171UnitsOnNoFace": units["unitsOnNoFace"], **summary(found)}
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    with open(out / "stray_units.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(found[0]) if found else ["block"], lineterminator="\n")
        writer.writeheader()
        writer.writerows(found)
    (out / "stray_units.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report, found


def main(argv=None):
    parser = argparse.ArgumentParser(description="The footprint unit blocks on no baseplan plot and what the plans have there (R194)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report, _ = run(c3d_run.load_config(args.config))
    print(json.dumps(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
