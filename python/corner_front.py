"""R224 to R226. Which road edge the ramp of a plot with more than one road edge takes, tested on the past keys.

R224: a road edge of a baseplan face is an edge whose midpoint lies within siteBuild.corner.roadEdgeM of a curb line. The
edge the past ramp takes is the face edge that holds the most of the ramp outline within siteBuild.corner.onEdgeM. A plot
with two or more road edges is a corner plot here, and the count is checked against Corner and No. of streets of the Rev 9
sales sheet.
R225: each candidate rule picks one road edge of a corner plot: nearestCurb (the R122 front of today), orientation (the
edge whose outward normal falls in the Orientation quadrant of the sales sheet, EN 0 to 90, SE 90 to 180, WS 180 to 270,
NW 270 to 360 degrees from north), orientationOrCurb (orientation, and the R122 front where the sales sheet has no row),
longest and shortest road edge, and neighbourFront (the edge facing the way the R122 front of the nearest plot of its
cluster faces). A rule is kept only when it never picks another edge than the past ramp on a key plot outside the cut
plots. A plot it cannot say on is listed and gets no front. The breaks of every rule are listed.
R226: the kept rule, if any, is run on the plots given, such as the Z4 plots whose past ramp sits on another edge.
"""
import argparse
import csv
import json
import math
import sys
from collections import Counter
from pathlib import Path

import openpyxl
from shapely.geometry import LineString, Point, Polygon
from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import entry_levels
import link_summary
import plan_compare
import plan_from_past
import plot_relations
import project_profile
import prototype_offsets
import rev9_workbooks
import yard_rule

QUADRANTS = {"EN": (0.0, 90.0), "SE": (90.0, 180.0), "WS": (180.0, 270.0), "NW": (270.0, 360.0)}
RULES = ("orientationOrCurb", "nearestCurb", "orientation", "longest", "shortest", "neighbourFront")


def bearing(vector):
    """Degrees clockwise from north of a plan vector."""
    return math.degrees(math.atan2(vector[0], vector[1])) % 360.0


def outward(polygon, first, second):
    """The unit normal of an edge pointing out of the face."""
    length = math.dist(first, second)
    normal = (-(second[1] - first[1]) / length, (second[0] - first[0]) / length)
    middle = ((first[0] + second[0]) / 2, (first[1] + second[1]) / 2)
    probe = Point(middle[0] + normal[0] * 0.01, middle[1] + normal[1] * 0.01)
    return (-normal[0], -normal[1]) if polygon.contains(probe) else normal


def road_edges(polygon, tree, roads, limit):
    """R224. The edges of the face whose midpoint lies within limit of a curb line, with length, distance and outward normal."""
    found = []
    for first, second in entry_levels.ring_edges(polygon):
        middle = Point((first[0] + second[0]) / 2, (first[1] + second[1]) / 2)
        distance = float(tree.query_nearest(middle, return_distance=True)[1][0])
        if distance <= limit:
            found.append({"first": first, "second": second, "length": math.dist(first, second), "distance": distance, "out": outward(polygon, first, second)})
    return found


def ramp_edge(polygon, ramp, near):
    """R224. The index of the face edge that holds the most of the ramp outline within near, and that length."""
    best, held = None, 0.0
    boundary = ramp.boundary
    for index, (first, second) in enumerate(entry_levels.ring_edges(polygon)):
        length = boundary.intersection(LineString([first, second]).buffer(near)).length
        if length > held:
            best, held = index, length
    return best, held


def edge_index(polygon, edge):
    for index, (first, second) in enumerate(entry_levels.ring_edges(polygon)):
        if first == edge["first"] and second == edge["second"]:
            return index
    return None


def frame_for(polygon, edge, distance=None):
    """R226. The frame of R122 built on a given edge: n into the plot, r the right hand seen from the road, left and right ends."""
    first, second = edge["first"], edge["second"]
    normal = (-edge["out"][0], -edge["out"][1])
    right = (normal[1], -normal[0])
    left_end, right_end = (first, second) if first[0] * right[0] + first[1] * right[1] < second[0] * right[0] + second[1] * right[1] else (second, first)
    return {"n": normal, "r": right, "left": left_end, "right": right_end, "length": edge["length"], "distance": edge["distance"] if distance is None else distance, "tied": False}


def corner_frame(polygon, tree, roads, limits, block, sales_row):
    """R226. The front frame of a plot: on a plot with two or more road edges the edge the orientation rule picks, and none
    when its sales row has no Orientation, else the R122 front. Returns the frame and the rule that gave it, or why none."""
    frame = cad_links.front_frame(polygon, tree, roads, limits) if polygon is not None else None
    edges = road_edges(polygon, tree, roads, block["roadEdgeM"]) if polygon is not None else []
    if len(edges) < 2:
        return frame, "nearestCurb"
    chosen = pick("orientation", edges, frame, sales_row, None)
    if chosen is None:
        return None, "a corner plot with no Orientation in the sales sheet"
    return (frame if same_edge(frame, chosen) else frame_for(polygon, chosen)), "orientation"


def same_edge(frame, edge):
    """Whether the R122 front frame stands on this edge."""
    return frame is not None and {tuple(frame["left"]), tuple(frame["right"])} == {tuple(edge["first"]), tuple(edge["second"])}


def pick(rule, edges, frame, sales, neighbour):
    """R225. The road edge a rule picks, or None when it cannot say."""
    if rule == "nearestCurb":
        return next((edge for edge in edges if same_edge(frame, edge)), None)
    if rule == "orientation":
        quadrant = QUADRANTS.get((sales or {}).get("Orientation") or "")
        if quadrant is None:
            return None
        centre = (quadrant[0] + quadrant[1]) / 2.0
        inside = [edge for edge in edges if quadrant[0] <= bearing(edge["out"]) < quadrant[1]]
        pool = inside or edges
        return min(pool, key=lambda edge: cad_links.angle_gap(bearing(edge["out"]), centre))
    if rule == "orientationOrCurb":
        return pick("orientation", edges, frame, sales, neighbour) or pick("nearestCurb", edges, frame, sales, neighbour)
    if rule == "longest":
        return max(edges, key=lambda edge: edge["length"])
    if rule == "shortest":
        return min(edges, key=lambda edge: edge["length"])
    if rule == "neighbourFront":
        if neighbour is None:
            return None
        facing = bearing((-neighbour["n"][0], -neighbour["n"][1]))
        return min(edges, key=lambda edge: cad_links.angle_gap(bearing(edge["out"]), facing))
    raise ValueError("no rule " + rule)


def sales_rows(path, sheet, plot_column):
    """The sales rows by plot number, the first row the header. R350: through rev9_workbooks.sheet_values, so a value past the stated
    dimension of the sheet is refused."""
    rows = rev9_workbooks.sheet_values(path, sheet)
    header = [str(cell).strip() if cell is not None else None for cell in rows[0]]
    found = {}
    for row in rows[1:]:
        item = dict(zip(header, row))
        number = plan_from_past.plot_number(item.get(plot_column))
        if number is not None:
            found[number] = item
    return found


def ramp_shapes(plan):
    """The ramp outline of each plot of a plan as a polygon."""
    found = {}
    for entry in plan["plots"]:
        ramp = (entry.get("floors") or {}).get("ramp")
        if ramp:
            found[entry["plot"]] = plan_compare.floor_shape(ramp)
    return found


def neighbours(clusters, frames, faces):
    """The R122 frame of the nearest other plot of the cluster of each plot."""
    found = {}
    for cluster in clusters:
        plots = [plot for plot in cluster["plots"] if plot in faces]
        for plot in plots:
            others = [other for other in plots if other != plot and frames.get(other) is not None]
            if others:
                near = min(others, key=lambda other: faces[other].centroid.distance(faces[plot].centroid))
                found[plot] = frames[near]
    return found


def test_rules(keys, faces, tree, roads, limits, block, sales, near_frames, apart):
    """R224, R225. Per key the corner plots, the check against the sales sheet, and per rule holds and breaks outside apart."""
    report = {}
    for name, ramps in keys.items():
        corners, check, rules = [], Counter(), {rule: {"holds": 0, "breaks": [], "cannot": 0, "apart": 0} for rule in RULES}
        by_plot = {}
        for plot, ramp in sorted(ramps.items(), key=lambda item: link_summary.sort_key(item[0])):
            face = faces.get(plot)
            if face is None or ramp is None:
                continue
            edges = road_edges(face, tree, roads, block["roadEdgeM"])
            row = sales.get(plot) or {}
            streets = row.get("No. of streets")
            check[("many" if len(edges) > 1 else "one" if edges else "none") + " road edges, sales streets " + str(streets) + ", corner " + str(row.get("Corner"))] += 1
            if len(edges) < 2:
                continue
            past, held = ramp_edge(face, ramp, block["onEdgeM"])
            corners.append(plot)
            frame = cad_links.front_frame(face, tree, roads, limits)
            by_plot[plot] = {"pastEdge": past, "roadEdges": [edge_index(face, edge) for edge in edges]}
            for rule in RULES:
                chosen = pick(rule, edges, frame, row, near_frames.get(plot))
                by_plot[plot][rule] = edge_index(face, chosen) if chosen is not None else None
                table = rules[rule]
                if plot in apart:
                    table["apart"] += 1
                elif chosen is None:
                    table["cannot"] += 1
                elif edge_index(face, chosen) == past:
                    table["holds"] += 1
                else:
                    table["breaks"].append({"plot": plot, "roadEdges": len(edges), "orientation": row.get("Orientation"),
                                            "frontAspects": row.get("Front Aspects"), "sideAspects": row.get("Side Aspects"), "rearAspect": row.get("Rear Aspect")})
        report[name] = {"plots": len(ramps), "cornerPlots": len(corners), "againstSales": dict(check.most_common()),
                        "rules": {rule: {"holds": table["holds"], "breaks": len(table["breaks"]), "cannot": table["cannot"], "apart": table["apart"],
                                         "breakList": table["breaks"]} for rule, table in rules.items()},
                        "byPlot": by_plot}
    return report


def kept_rule(report):
    """R225. The first rule of RULES that never picks another edge than the past ramp on a corner plot of any key outside apart.
    A plot it cannot say on is no break, the rule gives that plot no front and it is listed, so nothing is guessed."""
    for rule in RULES:
        if all(key["rules"][rule]["breaks"] == 0 and key["rules"][rule]["holds"] > 0 for key in report.values()):
            return rule
    return None


def main(argv=None):
    parser = argparse.ArgumentParser(description="The ramp front of plots with more than one road edge, tested on the past keys (R224 to R226).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--key", action="append", required=True, help="name=plan.json of a past key, more than one may be given")
    parser.add_argument("--labels", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--roads", required=True)
    parser.add_argument("--analysis", required=True)
    parser.add_argument("--sales", required=True, help="the Rev 9 workbook of the snapshot that holds the sales sheet")
    parser.add_argument("--check-plots", default="", help="R226. plots to run the kept rule on, by comma, with the key whose edge they are compared to, name:plots")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        block = settings.get("siteBuild")["corner"]
        sales_settings = settings.get("dataBook.locationPlan.sales")
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        numbers, _ = plan_from_past.location_numbers(args.labels)
        by_label, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
        faces = {numbers[label]: shape for label, shape in by_label.items() if label in numbers}
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
        tree = STRtree(roads)
        limits = cad_links.front_limits(profile, settings.get("analysis"))
        clusters = [{**cluster, "plots": [numbers[label] for label in cluster["plots"] if label in numbers]} for cluster in json.loads(Path(args.analysis).read_text(encoding="utf-8"))["clusters"]]
        frames = {plot: cad_links.front_frame(face, tree, roads, limits) for plot, face in faces.items()}
        near_frames = neighbours(clusters, frames, faces)
        sales = sales_rows(args.sales, sales_settings["sheet"], sales_settings["columns"]["plot"])
        apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
        keys = {}
        for item in args.key:
            name, _, path = item.partition("=")
            keys[name] = ramp_shapes(link_summary.load(path))
        report = test_rules(keys, faces, tree, roads, limits, block, sales, near_frames, apart)
        rule = kept_rule(report)
        result = {"keys": report, "keptRule": rule, "check": []}
        if args.check_plots:
            name, _, plots = args.check_plots.partition(":")
            for plot in plots.split(","):
                face, ramp = faces.get(plot), keys[name].get(plot)
                edges = road_edges(face, tree, roads, block["roadEdgeM"]) if face is not None else []
                past = ramp_edge(face, ramp, block["onEdgeM"])[0] if face is not None and ramp is not None else None
                row = {"plot": plot, "roadEdges": len(edges), "pastEdge": past}
                for test in RULES:
                    chosen = pick(test, edges, frames.get(plot), sales.get(plot), near_frames.get(plot)) if len(edges) > 1 else None
                    row[test] = None if chosen is None else edge_index(face, chosen) == past
                result["check"].append(row)
        common.write_json(out, result)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("corner_front: " + str(error), file=sys.stderr)
        return 2
    for name, key in report.items():
        print("%s: plots %d, corner plots %d" % (name, key["plots"], key["cornerPlots"]))
        print("  against the sales sheet %s" % key["againstSales"])
        for test, row in key["rules"].items():
            print("  %s holds %d, breaks %d, cannot say %d, apart %d" % (test, row["holds"], row["breaks"], row["cannot"], row["apart"]))
    print("kept rule %s" % rule)
    for row in result["check"]:
        print("check %s" % row)
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
