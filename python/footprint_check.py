"""R227 to R229. Placed links checked against the Building Footprint blocks of CAD, with no old prototype needed in the key.

R227: the house outline of a prototype is the union of its floors on its ground levels, the levels within
siteBuild.footprint.groundSpanM of its lowest floor level, read once per prototype file by a read job of the test host. It is
put into the host by the link transform the reader gives (origin and axes), so a mirrored link is mirrored with it.
R228: the footprint of a plot is the outline of its Building Footprint block clipped to its baseplan face, as one duplex or
townhouse block covers two or four plots, and the block is the one of the name the data book wired to the plot (everyShare)
that shares the most of the face. Per link: overlap share (area of the intersection over the area of the union), centre
offset whole and in the plot frame (along the front normal and along the front, from the ramp front of the plan of the
link), turn of the long axis (modulo 180) and mirror by shape: the house flipped about its own centre across the plot front
fits the footprint better by more than siteBuild.footprint.mirrorGain of overlap.
R229: the first set of links, a key whose links are right, gives per unit the median overlap share and the median centre
offset. Share and offset do not change with the hand of a link, the frame offset does, a left and a right unit of a pair sit
on the other side of their footprint. The residual of a link is its centre offset less the key median of its unit, banded
within 10 mm, 100 mm and 1 m. A link passes when its residual and its share residual are at most the largest of the key plus
siteBuild.footprint.slack, and the mirror test does not flag it.
"""
import argparse
import csv
import math
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely import affinity
from shapely.geometry import Polygon
from shapely.ops import unary_union

import common
import link_summary
import plan_from_past
import plot_relations
import project_profile
import ramp_levels
import yard_rule

OFFSET_BANDS = ((0.010, "within10mm"), (0.100, "within100mm"), (1.0, "within1m"))
SHARE_BANDS = ((0.95, "atLeast0.95"), (0.90, "atLeast0.90"), (0.80, "atLeast0.80"))


def house_outline(document, span):
    """R227. The union of the floors on the ground levels of a prototype, in its own internal coordinates, the largest part."""
    floors = [floor for floor in document.get("floors") or [] if plan_from_past.is_number(floor.get("levelElevation"))]
    if not floors:
        return None
    lowest = min(floor["levelElevation"] for floor in floors)
    offset = ramp_levels.offset_of(document)
    parts = []
    for floor in floors:
        if floor["levelElevation"] > lowest + span:
            continue
        shape, _ = plot_relations.floor_shape(floor, offset)
        if shape is not None and not shape.is_empty:
            parts.append(affinity.translate(shape.buffer(0), -offset[0], -offset[1]))
    if not parts:
        return None
    union = unary_union(parts).buffer(0.05).buffer(-0.05)
    return max(union.geoms, key=lambda part: part.area) if union.geom_type == "MultiPolygon" else union


def place(outline, link, offset):
    """R227. The outline in shared host coordinates by the link origin and axes, x along axisX and y along axisY."""
    transform = link["transform"]
    ox, oy = transform["origin"][0] + offset[0], transform["origin"][1] + offset[1]
    ax, ay = transform["axisX"], transform["axisY"]
    return affinity.affine_transform(outline, [ax[0], ay[0], ax[1], ay[1], ox, oy])


def long_axis_deg(shape):
    """The bearing of the long side of the smallest rotated rectangle, modulo 180."""
    ring = list(shape.minimum_rotated_rectangle.exterior.coords)
    sides = [(ring[k], ring[k + 1]) for k in range(2)]
    a, b = max(sides, key=lambda side: math.dist(side[0], side[1]))
    return math.degrees(math.atan2(b[1] - a[1], b[0] - a[0])) % 180.0


def iou(first, second):
    union = first.union(second).area
    return first.intersection(second).area / union if union > 0 else 0.0


def flipped(shape, front_normal):
    """The shape mirrored about the line through its centre along the plot front normal, the effect of the other mirror flag."""
    centre = shape.centroid
    angle = math.degrees(math.atan2(front_normal[1], front_normal[0]))
    turned = affinity.rotate(shape, -angle, origin=centre)
    return affinity.rotate(affinity.scale(turned, xfact=1.0, yfact=-1.0, origin=centre), angle, origin=centre)


def measure(house, footprint, front_normal, gain):
    """R228. Overlap share, centre offset whole and in the plot frame, long axis turn and the mirror test of one link."""
    share = iou(house, footprint)
    dx, dy = house.centroid.x - footprint.centroid.x, house.centroid.y - footprint.centroid.y
    frame = None
    if front_normal is not None:
        frame = [round(dx * front_normal[0] + dy * front_normal[1], 4), round(dx * front_normal[1] - dy * front_normal[0], 4)]
    turn = abs(long_axis_deg(house) - long_axis_deg(footprint)) % 180.0
    other = iou(flipped(house, front_normal), footprint) if front_normal is not None else share
    return {"share": round(share, 4), "offsetM": round(math.hypot(dx, dy), 4), "frameOffsetM": frame, "turnDeg": round(min(turn, 180.0 - turn), 3),
            "flippedShare": round(other, 4), "mirrorFlag": other > share + gain}


def band(value, bands, above):
    for limit, label in bands:
        if (value >= limit) if above else (value <= limit + 1e-9):
            return label
    return "worse"


def footprints(plots_csv, faces):
    """R228. The block name the data book wired to each plot and its face, by location plan number."""
    found = {}
    with open(plots_csv, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            number = row.get("locationPlanNumber")
            face = faces.get(number)
            name = row.get("footprintBlock") or ""
            if number and face is not None and name:
                found[number] = {"block": name, "face": face}
    return found


def footprint_blocks(path, settings=None):
    """R300. The blocks of the blocks.json of the footprint drawing, refused when its source or its folder names a drawing kept
    for reference only, by the settings the caller loaded."""
    return project_profile.read_villa_blocks(path, settings)["blocks"]


def block_shapes(blocks):
    """The outline of each block by block name."""
    index = defaultdict(list)
    for block in blocks:
        outline = block.get("outline")
        if outline and len(outline) >= 3:
            index[block.get("name")].append(Polygon([(point[0], point[1]) for point in outline]).buffer(0))
    return index


def plot_footprint(entry, block_index):
    """R228. The block of the plot's name that shares the most of the face, clipped to the face."""
    face = entry["face"]
    best, held = None, 0.0
    for shape in block_index.get(entry["block"], []):
        if shape.intersects(face):
            share = shape.intersection(face).area
            if share > held:
                best, held = shape, share
    return best.intersection(face) if best is not None else None


def front_normals(plan, faces):
    """The unit vector from the middle of the two lowest ramp corners of each plot to its face centre, the plot front normal."""
    normals = {}
    for entry in plan["plots"]:
        ramp = (entry.get("floors") or {}).get("ramp")
        face = faces.get(entry["plot"])
        if not ramp or not ramp.get("corners") or face is None:
            continue
        corners = sorted(ramp["corners"]["value"], key=lambda corner: corner[2])
        front = ((corners[0][0] + corners[1][0]) / 2, (corners[0][1] + corners[1][1]) / 2)
        vector = (face.centroid.x - front[0], face.centroid.y - front[1])
        length = math.hypot(*vector)
        if length > 0:
            normals[entry["plot"]] = (vector[0] / length, vector[1] / length)
    return normals


def check_links(document, plots, outlines, rows, block_index, normals, gain):
    """R228. Every link with a plot number in plots, measured against its footprint."""
    offset = ramp_levels.offset_of(document)
    results = {}
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if plot is None or plot not in plots:
            continue
        outline = outlines.get((link.get("file") or "").lower())
        entry = rows.get(plot)
        if outline is None or entry is None or not link.get("transform"):
            results[plot] = {"why": "no house outline" if outline is None else "no footprint block" if entry is None else "no transform"}
            continue
        footprint = plot_footprint(entry, block_index)
        if footprint is None or footprint.is_empty:
            results[plot] = {"why": "no block of its name on the face"}
            continue
        result = measure(place(outline, link, offset), footprint, normals.get(plot), gain)
        result["file"] = link.get("file")
        result["mirrored"] = bool(link["transform"].get("mirrored"))
        results[plot] = result
    return results


def group_of(plot, item, units):
    return units.get(plot) or "none"


def learn(results, units):
    """R229. Per unit of the key: the median share and centre offset and the largest residual of each."""
    groups = defaultdict(list)
    for plot, item in results.items():
        if "share" in item:
            groups[group_of(plot, item, units)].append(item)
    learned = {}
    for key, items in sorted(groups.items()):
        share = statistics.median(item["share"] for item in items)
        offset = statistics.median(item["offsetM"] for item in items)
        learned[key] = {"n": len(items), "medianShare": round(share, 4), "medianOffsetM": round(offset, 4),
                        "shareResidualMax": round(max(abs(item["share"] - share) for item in items), 4),
                        "residualMaxM": round(max(abs(item["offsetM"] - offset) for item in items), 4),
                        "mirrorFlags": sum(1 for item in items if item["mirrorFlag"])}
    return learned


def judge(results, units, learned, slack):
    """R229. Each measured link against the key group of its unit and mirror flag, residual bands, and the worst 10 with the reason."""
    rows = []
    for plot, item in results.items():
        if "share" not in item:
            continue
        group = group_of(plot, item, units)
        base = learned.get(group)
        reasons, residual = [], None
        if base is None:
            reasons.append("the key holds no link of " + group)
        else:
            residual = abs(item["offsetM"] - base["medianOffsetM"])
            if abs(item["share"] - base["medianShare"]) > base["shareResidualMax"] + slack:
                reasons.append("overlap %.3f against the key median %.3f" % (item["share"], base["medianShare"]))
            if residual > base["residualMaxM"] + slack:
                reasons.append("centre offset %.3f m from the key median, the key at most %.3f m" % (residual, base["residualMaxM"]))
        if item["mirrorFlag"]:
            reasons.append("the flipped house fits better, %.3f against %.3f" % (item["flippedShare"], item["share"]))
        rows.append({"plot": plot, "group": group, "share": item["share"], "residualM": round(residual, 4) if residual is not None else None,
                     "pass": not reasons, "reasons": reasons})
    rows.sort(key=lambda row: (row["pass"], -(row["residualM"] if row["residualM"] is not None else 1e9)))
    return {"pass": sum(1 for row in rows if row["pass"]), "fail": sum(1 for row in rows if not row["pass"]),
            "residualBands": dict(Counter(band(row["residualM"], OFFSET_BANDS, False) if row["residualM"] is not None else "noGroup" for row in rows)),
            "shareBands": dict(Counter(band(row["share"], SHARE_BANDS, True) for row in rows)), "worst10": rows[:10],
            "rows": [{"plot": row["plot"], "residualM": row["residualM"], "pass": row["pass"]} for row in rows]}


def summary(results):
    return {"links": len(results), "measured": sum(1 for item in results.values() if "share" in item),
            "notMeasured": dict(Counter(item["why"] for item in results.values() if "why" in item)),
            "mirrorFlags": sum(1 for item in results.values() if item.get("mirrorFlag")),
            "turnMaxDeg": max((item["turnDeg"] for item in results.values() if "turnDeg" in item), default=None)}


def run(outlines, sets, rows, index, faces, block):
    """R228, R229. Measure the key and the test sets, learn from the key and judge both."""
    measured = {}
    for name, (document, plan) in sets.items():
        units = {entry["plot"]: entry["unit"]["value"] for entry in plan["plots"]}
        measured[name] = (check_links(document, set(units), outlines, rows, index, front_normals(plan, faces), block["mirrorGain"]), units)
    learned = learn(*measured["key"])
    report = {"learned": learned}
    for name, (results, units) in measured.items():
        report[name] = summary(results)
        report[name + "Judged"] = judge(results, units, learned, block["slack"])
        report[name + "ByPlot"] = results
    return report


def load_and_run(settings, reads, key, key_plan, test, test_plan, plots_csv, blocks, baseplan, labels):
    """R228, R229 from the file paths, also for the third check of placer_check.py (R262). R300: the blocks first, by the
    settings given, so a reference only drawing is refused before any other file is read."""
    footprint = footprint_blocks(blocks, settings)
    block = settings.get("siteBuild")["footprint"]
    outlines = {}
    for path in reads:
        document = link_summary.load(path)["documents"][0]
        outlines[Path(document["file"]).name.split("_TEST-")[0].lower() + ".rvt"] = house_outline(document, block["groundSpanM"])
    numbers, _ = plan_from_past.location_numbers(labels)
    by_label, _ = yard_rule.face_shapes(link_summary.load(baseplan)["plots"], 0)
    faces = {numbers[label]: shape for label, shape in by_label.items() if label in numbers}
    sets = {"key": (plan_from_past.host_of(link_summary.load(key)), link_summary.load(key_plan)),
            "test": (plan_from_past.host_of(link_summary.load(test)), link_summary.load(test_plan))}
    report = run(outlines, sets, footprints(plots_csv, faces), block_shapes(footprint), faces, block)
    report["prototypeOutlinesM2"] = {name: round(shape.area, 2) if shape is not None else None for name, shape in outlines.items()}
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Placed links against the CAD footprint blocks (R227 to R229).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--prototype-reads", nargs="+", required=True, help="testhost_P<code>_prototypes.json of each prototype")
    parser.add_argument("--key", required=True, help="the reader output of the key whose links are right")
    parser.add_argument("--key-plan", required=True, help="the plan of that key, for its plots, units and fronts")
    parser.add_argument("--test", required=True, help="the reader output of the links to judge")
    parser.add_argument("--test-plan", required=True, help="the plan of those links")
    parser.add_argument("--plots-csv", required=True)
    parser.add_argument("--blocks", required=True, help="blocks.json of the footprint drawing in the snapshot")
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--labels", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        report = load_and_run(project_profile.load_settings(args.settings), args.prototype_reads, args.key, args.key_plan, args.test, args.test_plan,
                              args.plots_csv, args.blocks, args.baseplan, args.labels)
        common.write_json(out, report)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("footprint_check: " + str(error), file=sys.stderr)
        return 2
    print("prototype house outlines m2 %s" % report["prototypeOutlinesM2"])
    for group, row in report["learned"].items():
        print("key %s %s" % (group, row))
    for name in ("key", "test"):
        part, judged = report[name], report[name + "Judged"]
        print("%s: links %d, measured %d, not %s, mirror flags %d, turn max %s deg" % (name, part["links"], part["measured"], part["notMeasured"], part["mirrorFlags"], part["turnMaxDeg"]))
        print("  pass %d, fail %d, residual %s, share %s" % (judged["pass"], judged["fail"], judged["residualBands"], judged["shareBands"]))
    for row in report["testJudged"]["worst10"]:
        print("  worst %s %s share %s residual %s %s" % (row["plot"], row["group"], row["share"], row["residualM"], " | ".join(row["reasons"])))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
