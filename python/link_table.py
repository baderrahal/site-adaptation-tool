import argparse
import json
import math
import statistics
import sys
from pathlib import Path

from common import r3, write_json

EDGE_TOLERANCE = 1.0
ARC_STEP = math.pi / 36
EXCEL_FIELDS = {
    "unit": "unit",
    "ffl": "ffl",
    "ssl": "ssl",
    "el @ entry": "entry",
    "height": "height",
    "site steps needed": "siteSteps",
    "steps needed": "steps",
}
MISSING_KINDS = ("no_pdf", "no_excel", "pdf_no_cad", "excel_no_cad", "pdf_no_excel", "excel_no_pdf")


def plot_key(value):
    text = str(value).strip()
    if text.endswith(".0"):
        text = text[:-2]
    return int(text) if text.isdigit() else text


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def ring(shape):
    """Outline of a CAD face as points, arcs sampled."""
    points = []
    vertices = shape["vertices"]
    arcs = {arc["segment"]: arc for arc in shape.get("arcs", [])}
    for index, start in enumerate(vertices):
        end = vertices[(index + 1) % len(vertices)]
        points.append((start[0], start[1]))
        arc = arcs.get(index)
        if arc is None:
            continue
        cx, cy = arc["center"]
        first = math.atan2(start[1] - cy, start[0] - cx)
        last = math.atan2(end[1] - cy, end[0] - cx)
        sweep = (last - first) if arc["ccw"] else (first - last)
        while sweep <= 0:
            sweep += 2 * math.pi
        sweep = sweep if arc["ccw"] else -sweep
        steps = max(2, math.ceil(abs(sweep) / ARC_STEP))
        for step in range(1, steps):
            angle = first + sweep * step / steps
            points.append((cx + arc["radius"] * math.cos(angle), cy + arc["radius"] * math.sin(angle)))
    return points


def contains(outline, x, y):
    inside = False
    previous = outline[-1]
    for point in outline:
        if (point[1] > y) != (previous[1] > y) and x < (previous[0] - point[0]) * (y - point[1]) / (previous[1] - point[1]) + point[0]:
            inside = not inside
        previous = point
    return inside


class PlotIndex:
    """Finds the CAD plot that holds a point, using a coarse grid over the plot boxes."""

    def __init__(self, shapes, cell=50.0):
        self.cell = cell
        self.items = []
        self.grid = {}
        for shape in shapes:
            outline = ring(shape)
            box = box_of(outline)
            number = len(self.items)
            self.items.append((box, outline, plot_key(shape["plot"])))
            for gx in range(int(box[0] // cell), int(box[2] // cell) + 1):
                for gy in range(int(box[1] // cell), int(box[3] // cell) + 1):
                    self.grid.setdefault((gx, gy), []).append(number)

    def at(self, x, y):
        for number in self.grid.get((int(x // self.cell), int(y // self.cell)), []):
            box, outline, key = self.items[number]
            if box[0] <= x <= box[2] and box[1] <= y <= box[3] and contains(outline, x, y):
                return key
        return None


def distance_to_outline(outline, x, y):
    best = math.inf
    for index, (ax, ay) in enumerate(outline):
        bx, by = outline[(index + 1) % len(outline)]
        dx, dy = bx - ax, by - ay
        length = dx * dx + dy * dy
        along = 0 if length == 0 else max(0, min(1, ((x - ax) * dx + (y - ay) * dy) / length))
        best = min(best, math.hypot(x - (ax + along * dx), y - (ay + along * dy)))
    return best


def box_of(points):
    xs = [point[0] for point in points]
    ys = [point[1] for point in points]
    return [min(xs), min(ys), max(xs), max(ys)]


def overlap(first, second):
    return first[0] <= second[2] and second[0] <= first[2] and first[1] <= second[3] and second[1] <= first[3]


def excel_rows(coords):
    """Plot rows of one coords.json keyed by plot number, with the fields the placement work needs."""
    found = {}
    for row in coords["rows"]:
        fields = {"row": row.get("sheetRow"), "book": coords["source"]}
        for header, value in row.items():
            name = EXCEL_FIELDS.get(header.strip().rstrip("*").strip().lower())
            if name:
                fields[name] = value
        plot = next((row[header] for header in row if header.strip().lower().startswith("plot no")), None)
        if plot is None or str(plot).strip() == "":
            continue
        found.setdefault(plot_key(plot), []).append(fields)
    return found


def pdf_rows(rows_files):
    grouped = {}
    odd = []
    for rows in rows_files:
        for row in rows["rows"]:
            grouped.setdefault(row["plot"], []).append({"sheet": rows["source"], "block": row["block"], "row": row["row"], "code": row["code"], "type": row["type"], "n": row["n"], "e": row["e"]})
        for entry in rows.get("odd", []):
            odd.append(dict(entry, sheet=rows["source"]))
    return grouped, odd


def cad_plots(plots):
    found = {}
    for shape in plots["plots"]:
        if shape["kind"] == "plot_shape" and shape.get("plot") is not None:
            found.setdefault(plot_key(shape["plot"]), []).append(shape)
    return found


def check_location(shape, rows, tolerance, index):
    """The PDF points are unit corners, so they must lie inside the plot of the same number or within the tolerance of its edge."""
    outline = ring(shape)
    east = [row["e"] for row in rows]
    north = [row["n"] for row in rows]
    centre = shape["center"]
    outside = [0 if contains(outline, row["e"], row["n"]) else distance_to_outline(outline, row["e"], row["n"]) for row in rows]
    holders = [index.at(row["e"], row["n"]) for row in rows]
    holder = max(set(holders), key=holders.count)
    own = plot_key(shape["plot"])
    return {
        "centre": r3(math.hypot(centre[0] - sum(east) / len(east), centre[1] - sum(north) / len(north))),
        "outsideMax": r3(max(outside)),
        "inside": sum(1 for value in outside if value == 0),
        "ok": max(outside) <= tolerance,
        "holder": holder,
        "offset": (holder - own) if isinstance(holder, int) and isinstance(own, int) else None,
    }


def build(plots, rows_files, coords_files, tolerance=EDGE_TOLERANCE):
    cad = cad_plots(plots)
    index = PlotIndex([shapes[0] for shapes in cad.values() if len(shapes) == 1])
    pdf, odd = pdf_rows(rows_files)
    excel = {}
    for coords in coords_files:
        for key, rows in excel_rows(coords).items():
            excel.setdefault(key, []).extend(rows)

    conflicts = []
    links = []
    missing = {kind: [] for kind in MISSING_KINDS}
    for key in sorted(set(cad) | set(pdf) | set(excel), key=lambda value: (isinstance(value, str), value)):
        shapes = cad.get(key, [])
        rows = pdf.get(key, [])
        sheets = excel.get(key, [])
        if len(shapes) > 1:
            conflicts.append({"kind": "duplicate_cad", "plot": key, "count": len(shapes)})
        if len(sheets) > 1:
            conflicts.append({"kind": "duplicate_excel", "plot": key, "count": len(sheets), "rows": [sheet["row"] for sheet in sheets], "books": sorted({sheet["book"] for sheet in sheets})})
        if shapes and not rows:
            missing["no_pdf"].append(key)
        if shapes and not sheets:
            missing["no_excel"].append(key)
        if rows and not shapes:
            missing["pdf_no_cad"].append(key)
        if sheets and not shapes:
            missing["excel_no_cad"].append(key)
        if rows and not sheets:
            missing["pdf_no_excel"].append(key)
        if sheets and not rows:
            missing["excel_no_pdf"].append(key)

        shape = shapes[0] if len(shapes) == 1 else None
        types = {}
        if shape is not None and shape.get("type"):
            types["cad"] = shape["type"].strip().upper()
        if rows:
            pdf_types = sorted({row["type"].strip().upper() for row in rows})
            types["pdf"] = pdf_types[0] if len(pdf_types) == 1 else pdf_types
            if len(pdf_types) > 1:
                conflicts.append({"kind": "pdf_type_mixed", "plot": key, "types": pdf_types})
        if len(sheets) == 1 and sheets[0].get("unit"):
            types["excel"] = str(sheets[0]["unit"]).strip().upper()
        if "pdf" in types and "excel" in types and types["pdf"] != types["excel"]:
            conflicts.append({"kind": "type_mismatch", "plot": key, "types": {"pdf": types["pdf"], "excel": types["excel"]}})

        seen = {}
        for row in rows:
            seen.setdefault(row["code"], []).append(row)
        for code, same in seen.items():
            if len(same) > 1:
                same_place = len({(row["n"], row["e"]) for row in same}) == 1
                conflicts.append({"kind": "pdf_duplicate_point", "plot": key, "code": code, "count": len(same), "samePlace": same_place, "sheets": sorted({row["sheet"] for row in same})})

        location = None
        if shape is not None and rows:
            location = check_location(shape, rows, tolerance, index)
            if not location["ok"]:
                conflicts.append({"kind": "location", "plot": key, "centre": location["centre"], "outsideMax": location["outsideMax"], "points": len(rows), "holder": location["holder"]})

        link = {"plot": key, "type": types.get("cad")}
        if shape is not None:
            link["cad"] = {"center": shape["center"], "area": shape["area"]}
        if rows:
            link["pdf"] = rows
        if len(sheets) == 1:
            link["excel"] = sheets[0]
        if location is not None:
            link["location"] = location
        links.append(link)

    for entry in odd:
        conflicts.append({"kind": "pdf_odd_value", "plot": entry["plot"], "sheet": entry["sheet"], "row": entry["row"], "why": entry["why"]})
    for kind, numbers in missing.items():
        if numbers:
            conflicts.append({"kind": kind, "count": len(numbers), "plots": numbers})

    counts = {}
    for conflict in conflicts:
        counts[conflict["kind"]] = counts.get(conflict["kind"], 0) + (conflict["count"] if conflict["kind"] in MISSING_KINDS else 1)
    both = [link for link in links if "cad" in link and "pdf" in link and "excel" in link]
    located = [link["location"] for link in links if "location" in link]
    offsets = {}
    for item in located:
        label = "none" if item["holder"] is None or item["offset"] is None else str(item["offset"])
        offsets[label] = offsets.get(label, 0) + 1
    summary = {
        "plots": len(links),
        "cad": len(cad),
        "pdf": len(pdf),
        "excel": len(excel),
        "all3": len(both),
        "pdfMultiSheet": sum(1 for rows in pdf.values() if len({row["sheet"] for row in rows}) > 1),
        "located": len(located),
        "locationOk": sum(1 for item in located if item["ok"]),
        "locationBad": sum(1 for item in located if not item["ok"]),
        "centreMedian": r3(statistics.median(item["centre"] for item in located)) if located else None,
        "outsideMaxMedian": r3(statistics.median(item["outsideMax"] for item in located)) if located else None,
        "tolerance": tolerance,
        "holderOffsets": dict(sorted(offsets.items(), key=lambda pair: -pair[1])),
        "conflicts": counts,
    }
    return summary, links, conflicts


def extents(plots, rows_files, layer_files):
    """Boxes of every drawing, in drawing units for CAD and N E for the PDF sheets, with an overlap yes or no for each pair."""
    boxes = []
    shapes = [shape for shape in plots["plots"] if shape["kind"] == "plot_shape"]
    if shapes:
        boxes.append({"name": "cad plots", "kind": "cad", "box": box_of([vertex for shape in shapes for vertex in shape["vertices"]])})
    for layers in layer_files:
        entry = layers.get("extents") or {}
        if entry.get("core"):
            boxes.append({"name": layers["source"], "kind": "cad", "box": entry["core"], "all": entry.get("kept")})
    for rows in rows_files:
        if rows["rows"]:
            boxes.append({"name": rows["source"], "kind": "pdf", "box": box_of([(row["e"], row["n"]) for row in rows["rows"]])})
    pairs = []
    for index, first in enumerate(boxes):
        for second in boxes[index + 1:]:
            if first["kind"] == second["kind"] == "pdf":
                continue
            pairs.append({"a": first["name"], "b": second["name"], "overlap": overlap(first["box"], second["box"])})
    reference = next((box for box in boxes if box["name"] == "cad plots"), None)
    pdf_inside = [overlap(box["box"], reference["box"]) for box in boxes if box["kind"] == "pdf"] if reference else []
    return {
        "boxes": [dict(box, box=[r3(value) for value in box["box"]]) for box in boxes],
        "pairs": pairs,
        "pdfOverlapCadPlots": "yes" if pdf_inside and all(pdf_inside) else "no",
        "allPairsOverlap": "yes" if pairs and all(pair["overlap"] for pair in pairs) else "no",
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Join CAD plots, PDF rows and Excel rows by plot number and check them by location.")
    parser.add_argument("out_dir")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader")
    parser.add_argument("--rows", nargs="+", required=True, help="pdf_rows.json files")
    parser.add_argument("--coords", nargs="+", required=True, help="coords.json files")
    parser.add_argument("--layers", nargs="*", default=[], help="layers.json files of other drawings, for the extents check")
    parser.add_argument("--tolerance", type=float, default=EDGE_TOLERANCE)
    args = parser.parse_args(argv)
    plots = load(Path(args.cad) / "plots.json")
    rows_files = [load(path) for path in args.rows]
    summary, links, conflicts = build(plots, rows_files, [load(path) for path in args.coords], args.tolerance)
    space = extents(plots, rows_files, [load(path) for path in args.layers])
    out = Path(args.out_dir)
    write_json(out / "links.json", {"summary": summary, "links": links}, {"entry", "height"})
    write_json(out / "conflicts.json", {"summary": summary["conflicts"], "conflicts": conflicts})
    write_json(out / "extents.json", space)
    print("plots " + str(summary["plots"]) + ", all three " + str(summary["all3"]) + ", location ok " + str(summary["locationOk"]) + ", bad " + str(summary["locationBad"]) + ", conflicts " + str(sum(summary["conflicts"].values())))
    print("pdf sheets overlap CAD plots: " + space["pdfOverlapCadPlots"] + ", every pair overlaps: " + space["allPairsOverlap"])
    return 0


if __name__ == "__main__":
    sys.exit(main())
