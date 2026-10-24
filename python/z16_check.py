"""R235. The second method of round 16, with json, csv and math only and none of the code it checks.

For seeds 14 and 7 it draws up to 20 items of each part and works them out on its own:
- footprint: the share and centre offset of a placed link and its footprint block by a point grid of gridM, a point in the
  house when it lies in any ground floor of the prototype read and in the largest set of such points that touch, in the
  footprint when it lies in the block and the face, each by its own ray cast, against footprint_check.py within shareSlack
  and offsetSlackM
- corner rule: whether the orientation quadrant of the sales row picks the face edge nearest the centre of the past ramp
  corners, a ramp being shallower than half a plot is wide, among the face edges whose middle lies within roadEdgeM of a
  curb, against corner_front.py
- levels: the FFL gap of the 1.4 key and the IMC key from the generic floor tops of the two plans and the raw survey point
  of the 1.4 model, against level_cross.py within 1 mm
- new IMC file: the plan distance of the link origins and the turn of the X axes of the raw reader outputs of the new file
  and of the IMC key, joined by the plot number parameter, against the bands z4_compare.py wrote
"""
import argparse
import csv
import json
import math
import random
import sys
from pathlib import Path

import openpyxl

CHECKS = {"gridM": 0.2, "shareSlack": 0.01, "offsetSlackM": 0.05, "levelSlackMm": 1.0}
QUADRANTS = {"EN": (0.0, 90.0), "SE": (90.0, 180.0), "WS": (180.0, 270.0), "NW": (270.0, 360.0)}


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def project(data):
    return next(document for document in data["documents"] if document.get("kind") == "project")


def offset_of(document):
    survey = document["surveyPoint"]
    return [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(3)]


def inside(point, ring):
    """Ray cast, the point in the closed ring of (x, y) points."""
    x, y = point
    hit = False
    for k in range(len(ring)):
        x1, y1 = ring[k][0], ring[k][1]
        x2, y2 = ring[(k + 1) % len(ring)][0], ring[(k + 1) % len(ring)][1]
        if (y1 > y) != (y2 > y) and x < x1 + (y - y1) * (x2 - x1) / (y2 - y1):
            hit = not hit
    return hit


def bounds(rings):
    xs = [p[0] for ring in rings for p in ring]
    ys = [p[1] for ring in rings for p in ring]
    return min(xs), min(ys), max(xs), max(ys)


def largest_part(cells):
    """The largest set of grid cells that touch side by side, by a walk from each cell not yet seen."""
    seen, best = set(), set()
    for start in cells:
        if start in seen:
            continue
        part, todo = set(), [start]
        seen.add(start)
        while todo:
            i, j = todo.pop()
            part.add((i, j))
            for other in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
                if other in cells and other not in seen:
                    seen.add(other)
                    todo.append(other)
        if len(part) > len(best):
            best = part
    return best


def grid_share(house, footprint, step):
    """Share and centre offset of two areas given as tests on a point, over a grid of the box of both, the house its largest part."""
    rings = house["rings"] + footprint["rings"]
    x0, y0, x1, y1 = bounds(rings)
    nx, ny = int((x1 - x0) / step) + 1, int((y1 - y0) / step) + 1
    point = lambda i, j: (x0 + (i + 0.5) * step, y0 + (j + 0.5) * step)
    in_house = largest_part({(i, j) for i in range(nx) for j in range(ny) if house["test"](point(i, j))})
    in_foot = {(i, j) for i in range(nx) for j in range(ny) if footprint["test"](point(i, j))}
    if not in_house or not in_foot:
        return None, None
    hx = sum(point(i, j)[0] for i, j in in_house) / len(in_house)
    hy = sum(point(i, j)[1] for i, j in in_house) / len(in_house)
    fx = sum(point(i, j)[0] for i, j in in_foot) / len(in_foot)
    fy = sum(point(i, j)[1] for i, j in in_foot) / len(in_foot)
    return len(in_house & in_foot) / len(in_house | in_foot), math.hypot(hx - fx, hy - fy)


def ground_rings(read):
    """The ground floor rings of a prototype read, in its internal coordinates."""
    document = read["documents"][0]
    offset = offset_of(document)
    floors = [floor for floor in document["floors"] if floor.get("levelElevation") is not None and floor.get("outline")]
    lowest = min(floor["levelElevation"] for floor in floors)
    return [[(p[0] - offset[0], p[1] - offset[1]) for p in floor["outline"][0]] for floor in floors if floor["levelElevation"] <= lowest + 0.5]


def footprint_part(items, links, reads, faces, blocks, rows, report, step):
    """Share and offset of sampled links against the footprint report."""
    out = []
    for plot in items:
        link = links.get(plot)
        row = rows.get(plot)
        done = report.get(plot) or {}
        if link is None or row is None or "share" not in done:
            continue
        rings = reads.get((link["file"] or "").lower())
        if rings is None:
            continue
        t, offset = link["transform"], link["offset"]
        placed = [[(t["origin"][0] + offset[0] + t["axisX"][0] * x + t["axisY"][0] * y, t["origin"][1] + offset[1] + t["axisX"][1] * x + t["axisY"][1] * y) for x, y in ring] for ring in rings]
        face = faces[row["baseplanLabel"]]
        candidates = [ring for ring in blocks.get(row["footprintBlock"], []) if any(inside(p, face) for p in ring) or any(inside(p, ring) for p in face)]
        if not candidates:
            continue
        block = max(candidates, key=lambda ring: sum(1 for p in face if inside(p, ring)) + sum(1 for p in ring if inside(p, face)))
        house = {"rings": placed, "test": lambda p, rings=placed: any(inside(p, ring) for ring in rings)}
        foot = {"rings": [face], "test": lambda p, face=face, block=block: inside(p, face) and inside(p, block)}
        share, distance = grid_share(house, foot, step)
        if share is None:
            continue
        agree = abs(share - done["share"]) <= CHECKS["shareSlack"] and abs(distance - done["offsetM"]) <= CHECKS["offsetSlackM"]
        out.append({"plot": plot, "share": round(share, 4), "reportShare": done["share"], "offsetM": round(distance, 3), "reportOffsetM": done["offsetM"], "agree": agree})
    return out


def segment_distance(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = dx * dx + dy * dy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / length))
    return math.hypot(p[0] - a[0] - t * dx, p[1] - a[1] - t * dy)


def corner_part(items, faces_by_number, curbs, sales, ramps, report, limit):
    """Whether orientation picks the past ramp edge, by own geometry, against corner_front.py."""
    out = []
    for plot in items:
        face, ramp, done = faces_by_number.get(plot), ramps.get(plot), report.get(plot)
        if face is None or ramp is None or done is None:
            continue
        edges = [(face[k], face[(k + 1) % len(face)]) for k in range(len(face))]
        near = []
        for a, b in edges:
            middle = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            if min(segment_distance(middle, c, d) for c, d in curbs if abs(c[0] - middle[0]) < 200 and abs(c[1] - middle[1]) < 200) <= limit:
                near.append((a, b))
        middle = (sum(corner[0] for corner in ramp) / len(ramp), sum(corner[1] for corner in ramp) / len(ramp))
        past = min(edges, key=lambda edge: segment_distance(middle, edge[0], edge[1]))
        quadrant = QUADRANTS.get((sales.get(plot) or {}).get("Orientation") or "")
        mine = None
        if quadrant is not None and len(near) > 1:
            centre_x = sum(p[0] for p in face) / len(face)
            centre_y = sum(p[1] for p in face) / len(face)

            def facing(edge):
                middle = ((edge[0][0] + edge[1][0]) / 2, (edge[0][1] + edge[1][1]) / 2)
                return math.degrees(math.atan2(middle[0] - centre_x, middle[1] - centre_y)) % 360.0

            middle_q = (quadrant[0] + quadrant[1]) / 2
            pool = [edge for edge in near if quadrant[0] <= facing(edge) < quadrant[1]] or near
            chosen = min(pool, key=lambda edge: min(abs(facing(edge) - middle_q), 360 - abs(facing(edge) - middle_q)))
            mine = chosen == past
        theirs = None if done.get("orientation") is None else done["orientation"] == done["pastEdge"]
        out.append({"plot": plot, "mine": mine, "report": theirs, "agree": mine == theirs})
    return out


def level_part(items, key14_plan, imc_plan, datum, report):
    out = []
    for plot in items:
        a, b, done = key14_plan.get(plot), imc_plan.get(plot), report.get(plot)
        if a is None or b is None or done is None or "fflGapMm" not in done:
            continue
        top = a["floors"]["generic"]["z"]["value"] - datum
        gap = (top - b["levels"]["ffl"]["value"]) * 1000.0
        out.append({"plot": plot, "gapMm": round(gap, 3), "reportMm": done["fflGapMm"], "agree": abs(gap - done["fflGapMm"]) <= CHECKS["levelSlackMm"]})
    return out


def band(gap_m, bands):
    if gap_m <= 1e-6:
        return "equal"
    for limit in bands[1:]:
        if gap_m * 1000.0 <= limit + 1e-6:
            return "within%gmm" % limit
    return "worse"


def link_part(items, new_links, key_links, report, bands):
    out = []
    for plot in items:
        a, b, done = new_links.get(plot), key_links.get(plot), report.get(plot)
        if a is None or b is None or done is None:
            continue
        pa = [a["transform"]["origin"][k] + a["offset"][k] for k in range(2)]
        pb = [b["transform"]["origin"][k] + b["offset"][k] for k in range(2)]
        distance = math.hypot(pa[0] - pb[0], pa[1] - pb[1])
        turn = abs(math.degrees(math.atan2(a["transform"]["axisX"][1], a["transform"]["axisX"][0]) - math.atan2(b["transform"]["axisX"][1], b["transform"]["axisX"][0]))) % 360.0
        turn = min(turn, 360.0 - turn)
        agree = band(distance, bands) == done["bands"].get("position") and abs(turn - (done["rotationGapDeg"] or 0.0)) <= 0.01
        out.append({"plot": plot, "positionMm": round(distance * 1000.0, 3), "band": band(distance, bands), "reportBand": done["bands"].get("position"), "agree": agree})
    return out


def links_by_plot(data):
    document = project(data)
    offset = offset_of(document)
    found = {}
    for link in document.get("links") or []:
        plot = (link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number")
        if plot and link.get("transform"):
            found[str(plot).strip()] = {"file": link.get("file"), "transform": link["transform"], "offset": offset}
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="The second method of round 16 (R235).")
    parser.add_argument("--snapshot", required=True)
    parser.add_argument("--reads", required=True, help="the folder of the prototype reads")
    parser.add_argument("--footprint", required=True, help="footprint_z4.json")
    parser.add_argument("--z4-new", required=True, help="the reader output of the Z4 new file")
    parser.add_argument("--corner", required=True, help="corner_front.json")
    parser.add_argument("--key14-plan", required=True)
    parser.add_argument("--key14-model", required=True)
    parser.add_argument("--imc-plan", required=True)
    parser.add_argument("--imc-model", required=True)
    parser.add_argument("--levels", required=True, help="level_cross.json")
    parser.add_argument("--new-model", required=True, help="the reader output of the new IMC file")
    parser.add_argument("--compare", required=True, help="the z4_compare report of the new IMC file against the IMC key")
    parser.add_argument("--road-edge-m", type=float, required=True)
    parser.add_argument("--seeds", default="14,7")
    parser.add_argument("--size", type=int, default=20)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    snapshot = Path(args.snapshot)
    with open(snapshot / "plots.csv", encoding="utf-8") as handle:
        rows = {row["locationPlanNumber"]: row for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
    faces = {str(plot["plot"]): [(p[0], p[1]) for p in plot["vertices"]] for plot in load(snapshot / "cad" / "baseplan" / "plots.json")["plots"] if plot.get("kind") == "plot_shape" and plot.get("vertices")}
    faces_by_number = {number: faces[row["baseplanLabel"]] for number, row in rows.items() if row.get("baseplanLabel") in faces}
    blocks = {}
    for block in load(snapshot / "cad" / "footprint" / "blocks.json")["blocks"]:
        if block.get("outline"):
            blocks.setdefault(block["name"], []).append([(p[0], p[1]) for p in block["outline"]])
    reads = {}
    for path in Path(args.reads).glob("*.json"):
        read = load(path)
        reads[Path(read["documents"][0]["file"]).name.split("_TEST-")[0].lower() + ".rvt"] = ground_rings(read)
    curbs = []
    for piece in load(snapshot / "cad" / "roads" / "pieces.json")["pieces"]:
        layer = str(piece.get("layer") or "").lower()
        if piece.get("kind") in ("polyline", "line") and (layer == "rd-curb-line" or layer.endswith("$rd-curb-line")):
            points = piece["pts"] if isinstance(piece["pts"], list) else json.loads(piece["pts"])
            curbs.extend(((points[k][0], points[k][1]), (points[k + 1][0], points[k + 1][1])) for k in range(len(points) - 1))
    workbook = openpyxl.load_workbook(next((snapshot / "rev9").glob("*Accomodation_Schedule.xlsx")), read_only=True, data_only=True)
    sheet = list(workbook["JN Sales Data_Base_Rev09"].iter_rows(values_only=True))
    header = [str(cell).strip() if cell is not None else None for cell in sheet[0]]
    sales = {}
    for line in sheet[1:]:
        item = dict(zip(header, line))
        if isinstance(item.get("Plot No"), (int, float)):
            sales[str(int(item["Plot No"]))] = item
    footprint = load(args.footprint)
    z4_links = links_by_plot(load(args.z4_new))
    corner = load(args.corner)
    key14_plan = {entry["plot"]: entry for entry in load(args.key14_plan)["plots"]}
    imc_plan = {entry["plot"]: entry for entry in load(args.imc_plan)["plots"]}
    datum = offset_of(project(load(args.key14_model)))[2]
    levels = load(args.levels)["cross"]["byPlot"]
    compare = {row["plot"]: row for row in load(args.compare)["byPlot"]}
    new_links, key_links = links_by_plot(load(args.new_model)), links_by_plot(load(args.imc_model))
    ramps14 = {plot: entry["floors"]["ramp"]["corners"]["value"] for plot, entry in key14_plan.items() if entry.get("floors", {}).get("ramp", {}).get("corners")}
    ramps_imc = {plot: entry["floors"]["ramp"]["corners"]["value"] for plot, entry in imc_plan.items() if entry.get("floors", {}).get("ramp", {}).get("corners")}
    bands = [0, 1, 10, 30]
    result = {"seeds": {}}
    for seed in [int(text) for text in args.seeds.split(",")]:
        draw = random.Random(seed)

        def sample(pool):
            pool = sorted(pool, key=lambda item: int(item))
            return sorted(draw.sample(pool, min(args.size, len(pool))), key=int)

        parts = {
            "footprint": footprint_part(sample(footprint["testByPlot"]), z4_links, reads, faces, blocks, rows, footprint["testByPlot"], CHECKS["gridM"]),
            "cornerKey14": corner_part(sample(corner["keys"]["key14"]["byPlot"]), faces_by_number, curbs, sales, ramps14, corner["keys"]["key14"]["byPlot"], args.road_edge_m),
            "cornerImc": corner_part(sample(corner["keys"]["imc"]["byPlot"]), faces_by_number, curbs, sales, ramps_imc, corner["keys"]["imc"]["byPlot"], args.road_edge_m),
            "levels": level_part(sample(levels), key14_plan, imc_plan, datum, levels),
            "newImc": link_part(sample(compare), new_links, key_links, compare, bands),
        }
        result["seeds"][str(seed)] = {name: {"agree": sum(1 for row in rows_ if row["agree"]), "of": len(rows_), "rows": rows_} for name, rows_ in parts.items()}
        print("seed %d: %s" % (seed, ", ".join("%s %d of %d" % (name, part["agree"], part["of"]) for name, part in result["seeds"][str(seed)].items())))
    Path(args.out).write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(args.out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
