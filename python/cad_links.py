"""The link of each plot from CAD, measured against the links of the past job (R120 to R123).

The baseplan face of a plot, the road edges of the road layout drawing and the clusters of the schedule units (R90) give
a frame for every plot: the front edge, the inward normal and the left and right as seen from the road. The past links are
read in that frame. R120 lists the plots whose Generic floor does not follow the baseplan cut, R121 tests whether the link
origin sits on a corner and measures it from the front corners, R122 tests the rotation against the front edge and R123
counts the mirror flag against the side of the unit in its cluster and the CASE letter against rotation and mirror.
The measured frame goes to config/prototype_offsets.json under plotFrame. Lengths are in metres and angles in degrees.
"""
import argparse
import math
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Point
from shapely.ops import unary_union
from shapely.strtree import STRtree

import cad_analysis
import common
import entry_levels
import levels_proof
import link_summary
import link_table
import plan_from_past
import plot_relations
import project_profile
import prototype_offsets
import ramp_levels
import yard_rule

BANDS = (("within1mm", 0.001), ("within10mm", 0.010))
EPSILON = 1e-9
# Shares below this are slivers of a neighbouring face drawn a hair over the line, not a part of the plot
SLIVER = 1e-4
ROTATION_TOLERANCE = 0.1
ANCHORS = ("start", "end")
SIDES = ("left", "right")
# Faces of one cluster are drawn a hair apart, so their union is grown and shrunk by this to close the seams, in metres
UNION_SLACK = 0.001


def band_of(distance):
    for label, limit in BANDS:
        if distance <= limit + EPSILON:
            return label
    return "worse"


def angle_of(vector):
    return math.degrees(math.atan2(vector[1], vector[0])) % 360.0


def angle_gap(first, second):
    gap = abs(first - second) % 360.0
    return min(gap, 360.0 - gap)


def front_limits(profile, analysis):
    """R122. The lengths an edge may have to be a front: from the narrowest plot width of the profile less analysis.toleranceM
    up to below analysis.partyWallShare of plot.long_side, so a side edge of a corner plot and a chamfer are never the front."""
    widths = [entry.get("plot_width_m") for entry in profile.get("typologies").values()]
    widths = [value for value in widths if isinstance(value, (int, float)) and not isinstance(value, bool)]
    if not widths:
        raise project_profile.ProfileError(profile.path.name + " typologies hold no plot_width_m")
    return min(widths) - analysis["toleranceM"], analysis["partyWallShare"] * profile.metres("plot.long_side")


def front_frame(polygon, road_tree, roads, limits):
    """R122. The front edge of a plot face and the frame it gives, or None.

    The front edge is the edge whose length lies inside limits and whose midpoint is nearest to a road edge line. n is the
    unit normal of that edge pointing into the plot, r is the right hand of someone on the road facing the plot, which is n
    turned by minus 90 degrees. left and right are the two ends of the front edge as seen from the road. tied says another
    candidate edge is within FRONT_TIE of the nearest one.
    """
    if roads is None or len(roads) == 0 or polygon is None or polygon.geom_type != "Polygon":
        return None
    low, high = limits
    candidates = []
    for first, second in entry_levels.ring_edges(polygon):
        length = math.dist(first, second)
        if length < low or length >= high:
            continue
        middle = Point((first[0] + second[0]) / 2, (first[1] + second[1]) / 2)
        distance = float(road_tree.query_nearest(middle, return_distance=True)[1][0])
        candidates.append((distance, first, second))
    if not candidates:
        return None
    candidates.sort(key=lambda item: item[0])
    distance, first, second = candidates[0]
    length = math.dist(first, second)
    along = ((second[0] - first[0]) / length, (second[1] - first[1]) / length)
    normal = (-along[1], along[0])
    centre = polygon.centroid
    if (centre.x - first[0]) * normal[0] + (centre.y - first[1]) * normal[1] < 0:
        normal = (-normal[0], -normal[1])
    right = (normal[1], -normal[0])
    left_end, right_end = (first, second) if first[0] * right[0] + first[1] * right[1] < second[0] * right[0] + second[1] * right[1] else (second, first)
    tied = len(candidates) > 1 and candidates[1][0] - distance <= entry_levels.FRONT_TIE
    return {"n": normal, "r": right, "left": left_end, "right": right_end, "length": length, "distance": distance, "tied": tied}


def link_axes(frame, mirrored):
    """R122. The link axes the front edge gives: Y is the inward normal, X is Y turned by minus 90 degrees, by plus 90 when mirrored."""
    axis_x = (-frame["r"][0], -frame["r"][1]) if mirrored else frame["r"]
    return axis_x, frame["n"]


def rotation_of(frame, mirrored):
    return angle_of(link_axes(frame, mirrored)[0])


def anchor_corner(frame, mirrored, anchor):
    """R121. The front corner the link X axis starts from (start) or runs to (end)."""
    start, end = (frame["right"], frame["left"]) if mirrored else (frame["left"], frame["right"])
    return start if anchor == "start" else end


def in_axes(point, corner, axis_x, axis_y):
    dx, dy = point[0] - corner[0], point[1] - corner[1]
    return (dx * axis_x[0] + dy * axis_x[1], dx * axis_y[0] + dy * axis_y[1])


def from_axes(corner, axis_x, axis_y, offset):
    return (corner[0] + offset[0] * axis_x[0] + offset[1] * axis_y[0], corner[1] + offset[0] * axis_x[1] + offset[1] * axis_y[1])


def corner_name(corner, centre, frame):
    """front or back and left or right of a corner, seen from the road."""
    dx, dy = corner[0] - centre[0], corner[1] - centre[1]
    depth = dx * frame["n"][0] + dy * frame["n"][1]
    side = dx * frame["r"][0] + dy * frame["r"][1]
    return ("front" if depth < 0 else "back") + " " + ("right" if side > 0 else "left")


def nearest_corner(point, polygon, frame):
    """R121. The distance from a point to the nearest corner of the outer ring of a shape and the name of that corner."""
    ring = list(polygon.exterior.coords)[:-1]
    distance, corner = min((math.dist(point, vertex), vertex) for vertex in ring)
    centre = polygon.centroid
    return distance, corner_name(corner, (centre.x, centre.y), frame) if frame else None


def outer(shape):
    """The largest piece of a shape, so a union of faces that touch only at a corner still has one outline."""
    if shape.geom_type == "Polygon":
        return shape
    return max(shape.geoms, key=lambda piece: piece.area)


def cut_list(generics, faces, least):
    """R120. The share of each Generic floor on each baseplan face and the plots whose best share is under least.

    generics maps an official plot number to the plan shape of its Generic floor, faces maps an official number to its face.
    The faces are named by official number and their baseplan label is the number less the shift.
    """
    numbers = sorted(faces, key=int)
    tree = STRtree([faces[number] for number in numbers]) if numbers else None
    rows, listed = {}, []
    for plot in sorted(generics, key=int):
        shape = generics[plot]
        shares = []
        if tree is not None and shape is not None and shape.area > 0:
            for index in tree.query(shape):
                share = faces[numbers[int(index)]].intersection(shape).area / shape.area
                if share > SLIVER:
                    shares.append((share, numbers[int(index)]))
        shares.sort(key=lambda item: (-item[0], int(item[1])))
        best = shares[0] if shares else (0.0, None)
        rows[plot] = {"best": best[0], "bestFace": best[1], "own": dict((number, share) for share, number in shares).get(plot, 0.0), "shares": shares}
        if best[0] < least - EPSILON:
            listed.append(plot)
    return rows, listed


def read_past(document, offset, floors_config):
    """The past links by official plot number with origin, rotation, mirror, unit and CASE, and the Generic floor shapes."""
    links, twice = {}, set()
    for link in document.get("links") or []:
        parameters = link.get("parameters") or {}
        plot = plan_from_past.plot_number(parameters.get(plan_from_past.PLOT_PARAMETER))
        origin = plan_from_past.shared_origin(link, offset)
        transform = link.get("transform") or {}
        if plot is None or origin is None or not plan_from_past.is_number(transform.get("rotationDeg")) or not isinstance(transform.get("mirrored"), bool):
            continue
        if plot in links:
            twice.add(plot)
        links[plot] = {"origin": origin[:2], "rotation": transform["rotationDeg"] % 360.0, "mirrored": transform["mirrored"],
                       "unit": str(parameters.get(plan_from_past.UNIT_PARAMETER) or "").strip() or None, "case": str(parameters.get(plan_from_past.CASE_PARAMETER) or "").strip() or None}
    for plot in twice:
        del links[plot]
    generics = defaultdict(list)
    for floor in document.get("floors") or []:
        if plan_from_past.kind_of(floor, floors_config) != "generic":
            continue
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        shape = plot_relations.floor_shape(floor, offset)[0]
        if plot is not None and shape is not None:
            generics[plot].append(shape)
    return links, {plot: shapes[0] for plot, shapes in generics.items() if len(shapes) == 1}, sorted(twice, key=int)


def cluster_roles(clusters, shift, faces, frames):
    """R123. The side and role of every plot of a cluster of two or more, by official plot number.

    The plots of a cluster are put left to right as seen from the road, by their face centroids along the right hand r of the
    first plot of the cluster that has a frame. The plots of the left half are left and of the right half right, a middle plot
    of an odd cluster is middle. role is pair for a cluster of two, end for the ends of a longer one and middle for the others.
    """
    roles = {}
    for cluster in clusters:
        plots = [str(int(label) + shift) for label in cluster["plots"]]
        if len(plots) < 2 or any(plot not in faces for plot in plots):
            continue
        frame = next((frames[plot] for plot in plots if frames.get(plot)), None)
        if frame is None:
            continue
        ordered = sorted(plots, key=lambda plot: faces[plot].centroid.x * frame["r"][0] + faces[plot].centroid.y * frame["r"][1])
        count = len(ordered)
        for index, plot in enumerate(ordered):
            side = "left" if index < (count - 1) / 2 else "right" if index > (count - 1) / 2 else "middle"
            role = "pair" if count == 2 else "end" if index in (0, count - 1) else "middle"
            roles[plot] = {"cluster": cluster.get("code"), "units": count, "index": index + 1, "side": side, "role": role, "plots": ordered}
    return roles


def drawn_sides(links, roles, apart):
    """R123. The side each typology is drawn for, measured: the side where its unit is not mirrored in every cluster.

    A typology whose plots outside apart are not mirrored on one side only and mirrored on the other side only is drawn for
    that side. Any other typology has None and its plots that break it are listed.
    """
    seen = defaultdict(Counter)
    for plot, role in roles.items():
        link = links.get(plot)
        if link is None or plot in apart or role["side"] == "middle" or not link["unit"]:
            continue
        seen[link["unit"]][(role["side"], link["mirrored"])] += 1
    sides = {}
    for unit, counts in sorted(seen.items()):
        found = None
        for side in SIDES:
            other = SIDES[1 - SIDES.index(side)]
            if counts[(side, True)] == 0 and counts[(other, False)] == 0:
                found = side
        sides[unit] = {"drawnSide": found, "counts": {side + (" mirrored" if mirrored else " not mirrored"): count for (side, mirrored), count in sorted(counts.items())}}
    return sides


def mirror_of(unit, role, sides):
    """R123. The mirror flag a unit of a cluster gets: mirrored when it sits on the other side than its typology is drawn for.
    None when the plot is in no cluster, in the middle of an odd one, or its typology has no drawn side."""
    drawn = (sides.get(unit) or {}).get("drawnSide")
    if role is None or drawn is None or role["side"] not in SIDES:
        return None
    return role["side"] != drawn


def lead_counts(links, roles, apart):
    """R123. The lead of the Rev 6 manual on every cluster: seen from the road the left unit is the prototype as drawn and the
    right unit is mirrored. Counted per cluster code and role, the plots of apart on their own."""
    counts, breaks = defaultdict(Counter), defaultdict(list)
    for plot in sorted(roles, key=int):
        role, link = roles[plot], links.get(plot)
        if link is None or role["side"] == "middle":
            continue
        key = str(role["cluster"]) + " " + role["role"]
        if plot in apart:
            counts[key]["apart"] += 1
            continue
        holds = link["mirrored"] == (role["side"] == "right")
        counts[key]["holds" if holds else "breaks"] += 1
        if not holds:
            breaks[key].append(plot)
    return {key: dict(value) for key, value in sorted(counts.items())}, {key: value for key, value in sorted(breaks.items())}


def case_table(links):
    """R123. Each CASE letter with the rotation and mirror pairs it is on, and whether it maps to one pair.

    facing is the direction of the link Y axis, the X axis turned by plus 90 degrees, by minus 90 when mirrored.
    """
    letters, facings = defaultdict(Counter), defaultdict(Counter)
    for link in links.values():
        if not link["case"]:
            continue
        rotation = round(link["rotation"], 1) % 360.0
        letters[link["case"]][(rotation, link["mirrored"])] += 1
        facing = round((link["rotation"] + (-90.0 if link["mirrored"] else 90.0)) % 360.0, 1) % 360.0
        facings[link["case"]][facing] += 1
    table = {}
    for letter in sorted(letters):
        table[letter] = {"pairs": [{"rotationDeg": rotation, "mirrored": mirrored, "links": count} for (rotation, mirrored), count in sorted(letters[letter].items())],
                         "facingDeg": dict(sorted(facings[letter].items())), "onePair": len(letters[letter]) == 1, "oneFacing": len(facings[letter]) == 1}
    return table


def fit_origins(links, frames, apart):
    """R121. The link origin of each typology in link axes from each of the two front corners, fitted over the plots outside apart.

    The anchor of a typology is the corner whose offsets lie nearer their median, the most plots within 10 mm and then within
    1 mm, a tie to start. The offset is the median of each axis. Every plot is then judged against the fit of its typology.
    """
    rows = defaultdict(lambda: {anchor: [] for anchor in ANCHORS})
    for plot, link in links.items():
        frame = frames.get(plot)
        if frame is None or plot in apart or not link["unit"]:
            continue
        axis_x, axis_y = link_axes(frame, link["mirrored"])
        for anchor in ANCHORS:
            rows[link["unit"]][anchor].append(in_axes(link["origin"], anchor_corner(frame, link["mirrored"], anchor), axis_x, axis_y))
    fits = {}
    for unit, by_anchor in sorted(rows.items()):
        best = None
        for anchor in ANCHORS:
            offsets = by_anchor[anchor]
            middle = (statistics.median(item[0] for item in offsets), statistics.median(item[1] for item in offsets))
            bands = Counter(band_of(math.dist(item, middle)) for item in offsets)
            score = (bands["within1mm"] + bands["within10mm"], bands["within1mm"])
            if best is None or score > best[0]:
                best = (score, anchor, middle)
        fits[unit] = {"anchor": best[1], "offset": [best[2][0], best[2][1]]}
    return fits


def predicted_origin(frame, mirrored, fit):
    axis_x, axis_y = link_axes(frame, mirrored)
    return from_axes(anchor_corner(frame, mirrored, fit["anchor"]), axis_x, axis_y, fit["offset"])


def measure(links, generics, faces, roads, clusters, shift, limits, least, labels, neighbours=()):
    """R120 to R123 on the past links. Returns the report and the plotFrame section of the offsets file.

    labels maps an official plot number to the baseplan label and type of its face, for the cut list. neighbours are the
    plots of cadLinks.cutNeighbours, listed apart with the cut list.
    """
    road_tree = STRtree(roads) if roads else None
    frames = {plot: front_frame(faces.get(plot), road_tree, roads, limits) for plot in links}
    old_front = {plot: entry_levels.front_edge(faces[plot], road_tree, roads) if plot in faces else None for plot in links}
    shares, listed = cut_list(generics, faces, least)
    apart = set(listed) | set(neighbours)
    roles = cluster_roles(clusters, shift, faces, frames)
    sides = drawn_sides(links, roles, apart)
    fits = fit_origins(links, frames, apart)
    rows, tables = [], defaultdict(lambda: defaultdict(Counter))
    for plot in sorted(links, key=int):
        link, frame, role = links[plot], frames.get(plot), roles.get(plot)
        group = "apart" if plot in apart else "outside"
        row = {"plot": plot, "unit": link["unit"], "mirrored": link["mirrored"], "case": link["case"], "cutListed": plot in apart,
               "cutShare": shares[plot]["best"] if plot in shares else None, "frame": frame is not None, "tied": bool(frame and frame["tied"])}
        key = str(link["unit"]) + (" mirrored" if link["mirrored"] else " not mirrored")
        if plot in faces:
            distance, name = nearest_corner(link["origin"], faces[plot], frame)
            row["faceCorner"] = distance
            row["faceCornerName"] = name
            tables["faceCorner " + group][key][band_of(distance)] += 1
        if role is not None:
            outline = outer(unary_union([faces[item] for item in role["plots"]]).buffer(UNION_SLACK).buffer(-UNION_SLACK))
            distance, name = nearest_corner(link["origin"], outline, frame)
            row.update({"cluster": role["cluster"], "side": role["side"], "role": role["role"], "clusterCorner": distance, "clusterCornerName": name})
            tables["clusterCorner " + group][key][band_of(distance)] += 1
            if band_of(distance) != "worse" and name:
                tables["clusterCornerName " + group][key][name] += 1
        if frame is not None:
            row["rotationGap"] = angle_gap(link["rotation"], rotation_of(frame, link["mirrored"]))
            fit = fits.get(link["unit"])
            if fit is not None:
                row["originGap"] = math.dist(link["origin"], predicted_origin(frame, link["mirrored"], fit))
                tables["origin " + group][str(link["unit"])][band_of(row["originGap"])] += 1
        old = old_front[plot]
        if old is not None:
            normal = old_normal(old, faces[plot])
            row["rotationGapR94Front"] = angle_gap(link["rotation"], angle_of((-normal[1], normal[0]) if link["mirrored"] else (normal[1], -normal[0])))
        predicted = mirror_of(link["unit"], role, sides)
        row["mirrorRule"] = None if predicted is None else predicted == link["mirrored"]
        rows.append(row)
    lead, lead_breaks = lead_counts(links, roles, apart)
    report = {
        "plots": len(links),
        "cutList": cut_report(listed, shares, labels),
        "corners": {name: {key: dict(value) for key, value in sorted(table.items())} for name, table in sorted(tables.items()) if not name.startswith("origin")},
        "origin": {"fits": fits, "bands": {name: {key: dict(value) for key, value in sorted(table.items())} for name, table in sorted(tables.items()) if name.startswith("origin")},
                   "worse": [{"plot": row["plot"], "unit": row["unit"], "gap": row["originGap"]} for row in rows if not row["cutListed"] and row.get("originGap") is not None and band_of(row["originGap"]) == "worse"]},
        "rotation": rotation_report(rows),
        "mirror": {"lead": lead, "leadBreaks": lead_breaks, "drawnSides": sides, "rule": mirror_report(rows), "villas": villa_table(links, faces, frames, roles, limits)},
        "case": case_table(links),
        "rows": rows,
    }
    section = {"front": "the face edge from " + str(common.r3(limits[0])) + " m up to below " + str(common.r3(limits[1])) + " m long whose midpoint is nearest to a road edge line",
               "linkAxes": "Y is the inward normal of the front edge, X is Y turned by minus 90 degrees, plus 90 when mirrored",
               "units": {unit: {**fit, "drawnSide": (sides.get(unit) or {}).get("drawnSide")} for unit, fit in fits.items()},
               "measuredOn": len([row for row in rows if not row["cutListed"]]), "apart": sorted(apart, key=int)}
    return report, section


def old_normal(front, polygon):
    """The inward normal of a front edge of R94, for the rotation test against the old front."""
    first, second = front["edge"]
    length = math.dist(first, second)
    normal = (-(second[1] - first[1]) / length, (second[0] - first[0]) / length)
    centre = polygon.centroid
    if (centre.x - first[0]) * normal[0] + (centre.y - first[1]) * normal[1] < 0:
        normal = (-normal[0], -normal[1])
    return normal


def cut_report(listed, shares, labels):
    found = []
    for plot in listed:
        faces = [{"face": number, "label": (labels.get(number) or {}).get("label"), "type": (labels.get(number) or {}).get("type"), "share": share} for share, number in shares[plot]["shares"]]
        found.append({"plot": plot, "bestShare": shares[plot]["best"], "faces": faces})
    return {"limit": "best share under the cut limit", "plots": len(shares), "listed": len(found), "entries": found}


def rotation_report(rows):
    counts = Counter()
    breaks = []
    for row in rows:
        group = "apart" if row["cutListed"] else "outside"
        if "rotationGap" not in row:
            counts[group + " noFrame"] += 1
            continue
        holds = row["rotationGap"] <= ROTATION_TOLERANCE + EPSILON
        counts[group + (" holds" if holds else " breaks")] += 1
        if not holds:
            breaks.append({"plot": row["plot"], "unit": row["unit"], "gap": row["rotationGap"], "tied": row["tied"]})
        if "rotationGapR94Front" in row:
            counts[group + " R94 front " + ("holds" if row["rotationGapR94Front"] <= ROTATION_TOLERANCE + EPSILON else "breaks")] += 1
    return {"counts": dict(sorted(counts.items())), "breaks": breaks, "tiedFronts": sum(1 for row in rows if row["tied"])}


def mirror_report(rows):
    counts = Counter()
    breaks = []
    for row in rows:
        group = "apart" if row["cutListed"] else "outside"
        counts[group + (" no rule" if row["mirrorRule"] is None else " holds" if row["mirrorRule"] else " breaks")] += 1
        if row["mirrorRule"] is False:
            breaks.append(row["plot"])
    return {"counts": dict(sorted(counts.items())), "breaks": breaks}


def villa_table(links, faces, frames, roles, limits):
    """R123. The mirror flags of the plots in no cluster by the sides their side by side neighbours sit on, seen from the road.

    A neighbour shares at least the upper front limit (partyWallShare of plot.long_side) of boundary with the plot. Counted only, no rule.
    """
    numbers = sorted(faces, key=int)
    tree = STRtree([faces[number] for number in numbers]) if numbers else None
    table = defaultdict(Counter)
    for plot in sorted(links, key=int):
        frame = frames.get(plot)
        if plot in roles or frame is None or plot not in faces:
            continue
        sides = []
        for index in tree.query(faces[plot]):
            other = numbers[int(index)]
            if other == plot or faces[plot].boundary.intersection(faces[other].boundary).length < limits[1]:
                continue
            centre = faces[other].centroid
            dx, dy = centre.x - faces[plot].centroid.x, centre.y - faces[plot].centroid.y
            sides.append("right" if dx * frame["r"][0] + dy * frame["r"][1] > 0 else "left")
        key = str(links[plot]["unit"]) + " neighbours " + (" and ".join(sorted(sides)) if sides else "none")
        table[key]["mirrored" if links[plot]["mirrored"] else "not mirrored"] += 1
    return {key: dict(value) for key, value in sorted(table.items())}


def face_labels(plots, shift):
    """The baseplan label and type of each face by official plot number."""
    found = {}
    for entry in plots:
        label = yard_rule.whole_label(entry.get("plot"))
        if entry.get("kind") == "plot_shape" and label is not None:
            found[str(label + shift)] = {"label": str(entry.get("plot")), "type": entry.get("type")}
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="Measure the past links against the baseplan faces, the road edges and the clusters (R120 to R123).")
    parser.add_argument("out_dir", help="where cad_links.json is written, keep it outside git")
    parser.add_argument("--roads", required=True, help="folder with pieces.json of the road layout drawing")
    parser.add_argument("--analysis", required=True, help="analysis.json of cad_analysis.py with the schedule units")
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--prototypes", help="prototypes.json of the past job, default pastJob.prototypes")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--write-offsets", action="store_true", help="write the plotFrame section of the offsets file")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        analysis = settings.get("analysis")
        job = plan_from_past.load_job(settings)
        floors_config = plan_from_past.load_floors(settings)
        block = cad_links_settings(settings)
        least = block["cutShareMin"]
        shift = link_table.load_shifts(args.shifts)["shifts"][job["neighbourhood"]]
        baseplan = Path(args.baseplan or project_profile.ROOT / job["baseplanPlots"])
        plots = link_summary.load(baseplan)["plots"]
        faces, _ = yard_rule.face_shapes(plots, shift)
        document = ramp_levels.host_document(link_summary.load(args.prototypes or job["prototypes"]))
        links, generics, twice = read_past(document, ramp_levels.offset_of(document), floors_config)
        roads = cad_analysis.read_road_edges(args.roads, analysis["roadEdgeLayers"])
        clusters = levels_proof.read_clusters(args.analysis)
        report, section = measure(links, generics, faces, roads, clusters, shift, front_limits(profile, analysis), least, face_labels(plots, shift), block.get("cutNeighbours", []))
        report["linksOnTwoPlots"] = twice
        common.write_json(Path(args.out_dir) / "cad_links.json", report)
        if args.write_offsets:
            prototype_offsets.write_section(args.offsets, "plotFrame", section, prototype_offsets.note_for(job["neighbourhood"]))
        print("plots %d, cut listed %d" % (report["plots"], report["cutList"]["listed"]))
        print("rotation %s" % report["rotation"]["counts"])
        print("mirror rule %s" % report["mirror"]["rule"]["counts"])
        print("origin %s" % report["origin"]["bands"])
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("cad_links: " + str(error), file=sys.stderr)
        return 2
    return 0


def cad_links_settings(settings):
    """The cadLinks block of the project settings. cutShareMin is the share of its Generic floor a plot face must hold (R120)."""
    block = settings.values.get("cadLinks")
    if not isinstance(block, dict):
        raise project_profile.ProfileError(settings.path.name + " has no cadLinks block")
    least = block.get("cutShareMin")
    if not plan_from_past.is_number(least) or not 0 < least <= 1:
        raise project_profile.ProfileError(settings.path.name + " cadLinks.cutShareMin must be a share above 0 up to 1")
    neighbours = block.get("cutNeighbours", [])
    if not isinstance(neighbours, list) or not all(isinstance(plot, str) and plot.isdigit() for plot in neighbours):
        raise project_profile.ProfileError(settings.path.name + " cadLinks.cutNeighbours must be a list of plot numbers as text")
    return block


if __name__ == "__main__":
    sys.exit(main())
