"""Clusters of duplex and townhouse plots, read from the baseplan faces, then the coded rule checks on them.

Plot numbers here are the baseplan labels, so they sit below the official numbers by the shift of R28.
Lengths are in metres. Nothing here moves or changes a drawing.
"""
import argparse
import json
import math
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import LineString, MultiPoint, Polygon
from shapely.ops import unary_union
from shapely.strtree import STRtree

import common
import project_profile
import rules_checks
from rules_checks import UNKNOWN

CLUSTER_KINDS = ("duplex", "townhouse")
SETTINGS = ("toleranceM", "partyWallShare", "endShare")
# A shared segment is longer than this many tolerances, so two plots that only touch at a corner are not adjacent
SEGMENT_FACTOR = 4
# A plot counts as covered by a block when it holds at least this share of the block outline. Only the report uses it
COVER_SHARE = 0.2
# Two shares closer than this are a tie. The two halves of a duplex block always tie
TIE = 1e-6
FRONT_RULES = ("single plot", "neighbour", "road edge", "none", "not a row")


def plot_key(label):
    text = str(label)
    return (0, int(text), text) if text.isdecimal() else (1, 0, text)


def kind_of(profile, typology):
    entry = profile.get("typologies").get(typology) if isinstance(typology, str) else None
    return entry.get("kind") if entry else None


def mirrored_of(block):
    """True when the block is flipped, false when not, None when the plot has no block or the block says nothing.

    The reader writes mirrored, which is scale X times scale Y below zero taken before rounding. A block that only
    has a scale reads the same way. A block inside a flipped insert is flipped once more.
    """
    if not block:
        return None
    flag = block.get("mirrored")
    if flag is None:
        scale = block.get("scale")
        if not scale or len(scale) < 2:
            return None
        flag = scale[0] * scale[1] < 0
    return bool(flag) != bool(block.get("chainMirrored"))


def adjacent_pairs(outlines, tolerance):
    """Every pair of plots whose boundaries share a segment within the tolerance, with the shared geometry."""
    tree = STRtree(outlines)
    pairs = {}
    for first, shape in enumerate(outlines):
        near = shape.boundary.buffer(tolerance)
        for second in tree.query(near):
            second = int(second)
            if second <= first:
                continue
            shared = near.intersection(outlines[second].boundary)
            if shared.length > SEGMENT_FACTOR * tolerance:
                pairs[(first, second)] = shared
    return pairs


def groups_of(nodes, links):
    seen, groups = set(), []
    for start in nodes:
        if start in seen:
            continue
        seen.add(start)
        stack, group = [start], []
        while stack:
            node = stack.pop()
            group.append(node)
            for other in links[node]:
                if other not in seen:
                    seen.add(other)
                    stack.append(other)
        groups.append(group)
    return groups


def chain_of(group, links):
    """The units of a group from one end to the other, or None when the group is not one straight row."""
    if len(group) == 1:
        return list(group)
    ends = sorted(node for node in group if len(links[node]) == 1)
    if len(ends) != 2 or any(len(links[node]) > 2 for node in group):
        return None
    chain, before = [ends[0]], None
    while len(chain) < len(group):
        step = [node for node in links[chain[-1]] if node != before]
        before = chain[-1]
        chain.append(step[0])
    return chain


def direction_of(shared):
    parts = getattr(shared, "geoms", [shared])
    lines = [part for part in parts if part.geom_type == "LineString" and part.length > 0]
    if not lines:
        return None
    longest = max(lines, key=lambda line: line.length)
    (x1, y1), (x2, y2) = longest.coords[0][:2], longest.coords[-1][:2]
    length = math.hypot(x2 - x1, y2 - y1)
    return None if length == 0 else ((x2 - x1) / length, (y2 - y1) / length)


def end_neighbours(union, group, direction, tree, outlines, tolerance, reach):
    """Boundary length a row shares with plots outside it, at its end along the party walls and at the other end.

    A shared edge counts for an end when its middle lies more than reach from the centre of the row along the
    party walls. The side walls to the next plot in the row lie near the centre and do not count.
    """
    centre = union.centroid
    near = union.boundary.buffer(tolerance)
    ends = [0.0, 0.0]
    for other in tree.query(near):
        other = int(other)
        if other in group:
            continue
        shared = near.intersection(outlines[other].boundary)
        if shared.length <= SEGMENT_FACTOR * tolerance:
            continue
        point = shared.centroid
        offset = (point.x - centre.x) * direction[0] + (point.y - centre.y) * direction[1]
        if abs(offset) > reach:
            ends[0 if offset > 0 else 1] += shared.length
    return ends


def front_of(ends, direction):
    """The unit vector from the row towards the road, or None. Plots back onto other plots and front onto a road,
    so the front is away from the one end that touches other plots. Both ends or neither gives None."""
    plus, minus = ends[0] > 0, ends[1] > 0
    if plus == minus:
        return None
    return (-direction[0], -direction[1]) if plus else direction


def front_by_road(union, direction, road_tree, reach, tolerance):
    """The unit vector from the row towards the nearer road edge, or None (R71).

    The two ends of the row are the corners of its hull that lie more than reach from the centre along the party
    walls. The end with the nearer road edge is the front. Two ends as near as the tolerance give None.
    """
    centre = union.centroid
    hull = union.convex_hull
    if hull.geom_type != "Polygon":
        return None
    ends = ([], [])
    for x, y in hull.exterior.coords:
        offset = (x - centre.x) * direction[0] + (y - centre.y) * direction[1]
        if abs(offset) > reach:
            ends[0 if offset > 0 else 1].append((x, y))
    if not ends[0] or not ends[1]:
        return None
    near = []
    for corners in ends:
        _, distances = road_tree.query_nearest(MultiPoint(corners).convex_hull, return_distance=True)
        near.append(float(min(distances)))
    if abs(near[0] - near[1]) <= tolerance:
        return None
    return direction if near[0] < near[1] else (-direction[0], -direction[1])


def left_to_right(chain, outlines, front):
    """Facing the road from the plots, the left side is a quarter turn anticlockwise from the front."""
    right = (front[1], -front[0])
    first, last = outlines[chain[0]].centroid, outlines[chain[-1]].centroid
    start = first.x * right[0] + first.y * right[1]
    end = last.x * right[0] + last.y * right[1]
    return chain if start <= end else chain[::-1]


def listed_code(units, known, reverse_ok):
    """The code of the listed cluster these units are and whether they read against the listed order, or (None, False)."""
    for code, listed in known.items():
        if units == listed:
            return code, False
    if reverse_ok:
        for code, listed in known.items():
            if units[::-1] == listed:
                return code, True
    return None, False


def pair_conflict(units, flags, profile):
    """R70. A duplex pair is one original and one mirror, so two known flags that are equal cannot be a pair."""
    return len(units) == 2 and units[0] == units[1] and kind_of(profile, units[0]) == "duplex" and None not in flags and flags[0] == flags[1]


def best_split(units, flags, known, profile, reverse_ok):
    """Cut a row into listed clusters (R70).

    Returns the pieces as (start, end, code, flipped) and how many different cuts cover as many units as the best
    one does, 1 or 2 for two or more. The cut that is taken covers as many units as it can, then uses as few
    clusters as it can, and where those tie it starts from the first unit with the longest listed cluster. A unit
    that no listed cluster covers is left out of the pieces. A pair of equal duplex units whose known mirror flags
    are equal is not a listed pair. Where more than one cut covers the most units, only a mirror flag could have
    told them apart, so the caller does not call the order sure.
    """
    count = len(units)
    sizes = sorted({len(listed) for listed in known.values()}, reverse=True)
    options = [[] for _ in range(count)]
    for start in range(count):
        for size in sizes:
            end = start + size
            if end > count:
                continue
            stretch = tuple(units[start:end])
            code, flipped = listed_code(stretch, known, reverse_ok)
            if code is not None and not pair_conflict(stretch, flags[start:end], profile):
                options[start].append((end, code, flipped))
    best = [None] * (count + 1)
    best[count] = ((0, 0), 1, None)
    for start in range(count - 1, -1, -1):
        candidates = []
        for end, code, flipped in options[start]:
            (covered, listed), ways, _ = best[end]
            candidates.append(((covered + end - start, listed - 1), ways, (start, end, code, flipped)))
        (covered, listed), ways, _ = best[start + 1]
        candidates.append(((covered, listed), ways, None))
        top = max(candidate[0] for candidate in candidates)
        most = max(candidate[0][0] for candidate in candidates)
        total = sum(candidate[1] for candidate in candidates if candidate[0][0] == most)
        pick = next(candidate for candidate in candidates if candidate[0] == top)
        best[start] = (top, min(total, 2), pick[2])
    pieces, position = [], 0
    while position < count:
        choice = best[position][2]
        if choice is None:
            position += 1
        else:
            pieces.append(choice)
            position = choice[1]
    return pieces, best[0][1]


def split_pieces(count, pieces):
    """The whole row as (start, end, code, flipped) with each run of left over units as one unknown piece."""
    whole, position = [], 0
    for start, end, code, flipped in pieces + [(count, count, None, False)]:
        if start > position:
            whole.append((position, start, UNKNOWN, False))
        if end > start:
            whole.append((start, end, code, flipped))
        position = end
    return whole


def derive_clusters(plots, profile, analysis, roads=None):
    """Clusters of side by side duplex and townhouse plots.

    plots are all the baseplan faces with plot, type and vertices, and a block with name, mirrored and rotation
    when one is known. Two plots are side by side when they share a party wall, which is at least partyWallShare
    of the long side of a plot. Back to back plots share a shorter edge and stay in separate clusters. A row of
    side by side plots is cut into the listed clusters of the profile (R70). roads is a list of road edge lines
    and gives the front of a row that the neighbours leave open (R71).
    """
    tolerance = analysis["toleranceM"]
    long_side = profile.metres("plot.long_side")
    reach = analysis["endShare"] * long_side
    outlines = [Polygon([(vertex[0], vertex[1]) for vertex in plot["vertices"]]) for plot in plots]
    tree = STRtree(outlines)
    road_tree = STRtree(roads) if roads else None
    members = [index for index, plot in enumerate(plots) if kind_of(profile, plot.get("type")) in CLUSTER_KINDS]
    pairs = adjacent_pairs([outlines[index] for index in members], tolerance)
    walls = {(members[first], members[second]): shared for (first, second), shared in pairs.items() if shared.length >= analysis["partyWallShare"] * long_side}
    links = {index: [] for index in members}
    for first, second in walls:
        links[first].append(second)
        links[second].append(first)
    known = {code: tuple(units) for code, units in profile.get("clusters").items()}

    def label(index):
        return str(plots[index]["plot"])

    def cluster_of(indexes, code, sure, row, rule, whole):
        blocks = [plots[index].get("block") or {} for index in indexes]
        return {
            "code": code,
            "plots": [label(index) for index in indexes],
            "typologies": [plots[index]["type"] for index in indexes],
            "mirrored": [mirrored_of(plots[index].get("block")) for index in indexes],
            "rotation": [block.get("rotation") for block in blocks],
            "block": [block.get("name") for block in blocks],
            "orderSure": sure,
            "row": row,
            "frontRule": rule,
            "rowId": min((label(index) for index in whole), key=plot_key),
            "rowUnits": len(whole),
        }

    clusters = []
    for group in groups_of(members, links):
        chain = chain_of(group, links)
        if chain is None:
            chain = sorted(group, key=lambda index: plot_key(plots[index]["plot"]))
            clusters.append(cluster_of(chain, UNKNOWN, False, False, "not a row", chain))
            continue
        sure, rule = False, "none"
        if len(chain) == 1:
            sure, rule = True, "single plot"
        else:
            shared = max((shared for (first, _), shared in walls.items() if first in group), key=lambda shared: shared.length)
            direction = direction_of(shared)
            union = unary_union([outlines[index] for index in chain])
            front = None
            if direction is not None:
                front = front_of(end_neighbours(union, set(group), direction, tree, outlines, tolerance, reach), direction)
                rule = "neighbour"
                if front is None and road_tree is not None:
                    front = front_by_road(union, direction, road_tree, reach, tolerance)
                    rule = "road edge"
            if front is not None:
                sure = True
                chain = left_to_right(chain, outlines, front)
            else:
                rule = "none"
                if plot_key(plots[chain[0]]["plot"]) > plot_key(plots[chain[-1]]["plot"]):
                    chain = chain[::-1]
        units = [plots[index]["type"] for index in chain]
        flags = [mirrored_of(plots[index].get("block")) for index in chain]
        # Without a known front the direction of the row is a guess, so each piece may also read reversed
        pieces, ways = best_split(units, flags, known, profile, reverse_ok=not sure)
        for start, end, code, flipped in split_pieces(len(chain), pieces):
            indexes = chain[start:end][::-1] if flipped else chain[start:end]
            clusters.append(cluster_of(indexes, code, sure and ways == 1, True, rule, chain))
    clusters.sort(key=lambda cluster: min(plot_key(label) for label in cluster["plots"]))
    return clusters


def plot_faces(entries):
    """Copies of the faces, so the blocks attached later never reach the caller's data."""
    return [dict(entry) for entry in entries if entry.get("kind") == "plot_shape" and entry.get("plot") not in (None, "") and len(entry.get("vertices") or []) >= 3]


def clean(polygon):
    return polygon if polygon.is_valid else polygon.buffer(0)


def villa_blocks(blocks, profile):
    """The inserts that stand for a unit. A block name ends in an underscore and the code of a typology of the profile."""
    codes = set(profile.get("typologies"))
    found = []
    for block in blocks:
        name = block.get("name") or ""
        code = name.rsplit("_", 1)[-1] if "_" in name else None
        if code in codes and len(block.get("outline") or []) >= 3:
            found.append({**block, "code": code})
    return found


def match_blocks(faces, blocks):
    """R69. A block belongs to the plot that holds the largest share of its outline area.

    Shares that tie go to the lowest plot number. Returns the blocks of each face by index and the counts.
    """
    polygons = [clean(Polygon([(vertex[0], vertex[1]) for vertex in face["vertices"]])) for face in faces]
    tree = STRtree(polygons)
    owned = [[] for _ in faces]
    stats = {"tied": 0, "noPlot": 0, "covering": Counter()}
    for block in blocks:
        outline = clean(Polygon([(point[0], point[1]) for point in block["outline"]]))
        shares = []
        if not outline.is_empty and outline.area > 0:
            for index in tree.query(outline):
                index = int(index)
                share = outline.intersection(polygons[index]).area / outline.area
                if share > 0:
                    shares.append((share, index))
        if not shares:
            stats["noPlot"] += 1
            continue
        top = max(share for share, _ in shares)
        best = sorted((index for share, index in shares if top - share <= TIE), key=lambda index: plot_key(faces[index]["plot"]))
        if len(best) > 1:
            stats["tied"] += 1
        owned[best[0]].append(block)
        stats["covering"][sum(1 for share, _ in shares if share >= COVER_SHARE)] += 1
    return owned, stats


def attach_blocks(faces, owned):
    """Puts the one block of a plot on the face. A plot with no block or with two keeps none, the issue says why."""
    for face, found in zip(faces, owned):
        face.pop("block", None)
        if len(found) == 1:
            block = found[0]
            face["block"] = {"name": block.get("name"), "mirrored": mirrored_of(block), "rotation": block.get("rotationDeg")}


def block_issues(faces, owned, profile):
    """R69. A plot of a listed typology with no block, or any plot with two blocks, is an issue."""
    typologies = profile.get("typologies")
    issues = []
    for face, found in zip(faces, owned):
        typology = face.get("type")
        text = "Plot " + str(face["plot"]) + " (" + str(typology) + ") has "
        if len(found) >= 2:
            issues.append(rules_checks.issue("R69", [str(face["plot"])], text + str(len(found)) + " blocks: " + ", ".join(str(block.get("name")) for block in found)))
        elif not found and isinstance(typology, str) and typology in typologies:
            issues.append(rules_checks.issue("R69", [str(face["plot"])], text + "no block"))
    return issues


def plot_records(faces, owned, profile):
    """One record per plot of a listed typology with its block name, mirror flag and rotation (null without one block)."""
    typologies = profile.get("typologies")
    records = []
    for face, found in zip(faces, owned):
        typology = face.get("type")
        if not isinstance(typology, str) or typology not in typologies:
            continue
        block = face.get("block") or {}
        records.append({"plot": str(face["plot"]), "type": typology, "block": block.get("name"), "mirrored": block.get("mirrored"), "rotation": block.get("rotation"), "blocks": len(found)})
    records.sort(key=lambda record: plot_key(record["plot"]))
    return records


def block_summary(faces, owned, stats, records, villas, source):
    agree = sum(1 for face, found in zip(faces, owned) if len(found) == 1 and found[0].get("code") == face.get("type"))
    single = sum(1 for found in owned if len(found) == 1)
    return {
        "source": source,
        "villaBlocks": len(villas),
        "plotsOneBlock": single,
        "plotsNoBlock": sum(1 for record in records if record["blocks"] == 0),
        "plotsTwoBlocks": sum(1 for found in owned if len(found) >= 2),
        "tiedBlocks": stats["tied"],
        "blocksOnNoPlot": stats["noPlot"],
        "blocksCovering": {str(count): number for count, number in sorted(stats["covering"].items())},
        "mirrored": sum(1 for record in records if record["mirrored"] is True),
        "notMirrored": sum(1 for record in records if record["mirrored"] is False),
        "typeAgrees": agree,
        "typeDiffers": single - agree,
    }


def analyse(entries, profile, analysis, blocks=None, roads=None, blocks_source=None):
    faces = plot_faces(entries)
    issues_blocks, records, block_facts = [], [], None
    if blocks is not None:
        villas = villa_blocks(blocks, profile)
        owned, stats = match_blocks(faces, villas)
        attach_blocks(faces, owned)
        issues_blocks = block_issues(faces, owned, profile)
        records = plot_records(faces, owned, profile)
        block_facts = block_summary(faces, owned, stats, records, villas, blocks_source)
    clusters = derive_clusters(faces, profile, analysis, roads)
    issues = rules_checks.check_attachment(clusters, profile) + rules_checks.check_known_clusters(clusters) + issues_blocks
    row_rules = {cluster["rowId"]: cluster["frontRule"] for cluster in clusters}
    rules = Counter(row_rules.values())
    summary = {
        "plotFaces": len(faces),
        "clusters": len(clusters),
        "clusterPlots": sum(len(cluster["plots"]) for cluster in clusters),
        "byCode": dict(sorted(Counter(cluster["code"] for cluster in clusters).items())),
        "notRows": sum(1 for cluster in clusters if not cluster["row"]),
        "orderNotSure": sum(1 for cluster in clusters if not cluster["orderSure"]),
        "unitsWithMirrorFlag": sum(1 for cluster in clusters for flag in cluster["mirrored"] if flag is not None),
        "rows": len(row_rules),
        "cutFromLongerRow": sum(1 for cluster in clusters if len(cluster["plots"]) < cluster["rowUnits"]),
        "frontRules": {rule: rules[rule] for rule in FRONT_RULES if rules[rule]},
        "roadEdges": None if roads is None else len(roads),
        "blocks": block_facts,
        "issues": len(issues),
        "numbering": "baseplan labels",
    }
    return {"summary": summary, "clusters": clusters, "plots": records, "issues": issues}


def read_json_file(folder, name):
    path = Path(folder) / name
    if not path.is_file():
        raise project_profile.ProfileError("no " + name + " in " + str(folder))
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as error:
        raise project_profile.ProfileError(name + " is not valid JSON: " + str(error)) from error


def read_plots(folder):
    data = read_json_file(folder, "plots.json")
    if not isinstance(data, dict) or not isinstance(data.get("plots"), list):
        raise project_profile.ProfileError("plots.json has no plots list")
    return data


def read_blocks(folder):
    data = read_json_file(folder, "blocks.json")
    if not isinstance(data, dict) or not isinstance(data.get("blocks"), list):
        raise project_profile.ProfileError("blocks.json has no blocks list")
    return data


def read_road_edges(folder, layers):
    """The lines of the road edge layers of the road layout drawing. A layer name also matches inside a bound xref,
    where the CAD reader names it <xref>$<n>$<layer>. A closed polyline is closed. Bulges are left out."""
    data = read_json_file(folder, "pieces.json")
    if not isinstance(data, dict) or not isinstance(data.get("pieces"), list):
        raise project_profile.ProfileError("pieces.json has no pieces list")
    if not isinstance(layers, list) or not layers:
        raise project_profile.ProfileError("analysis.roadEdgeLayers names no layer")
    wanted = [str(name).lower() for name in layers]
    lines = []
    for piece in data["pieces"]:
        layer = str(piece.get("layer") or "").lower()
        if piece.get("kind") not in ("polyline", "line") or not any(layer == name or layer.endswith("$" + name) for name in wanted):
            continue
        points = [(point[0], point[1]) for point in piece.get("pts") or [] if len(point) >= 2]
        if piece.get("closed") and len(points) >= 3:
            points.append(points[0])
        if len(points) >= 2:
            lines.append(LineString(points))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(description="Clusters of duplex and townhouse plots from the baseplan faces, then the coded checks of rules/README.md and docs/rules.md.")
    parser.add_argument("out_dir", help="where analysis.json is written")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader, run on the baseplan")
    parser.add_argument("--blocks", help="folder with blocks.json from the CAD reader, run on the villa block drawing")
    parser.add_argument("--roads", help="folder with pieces.json from the CAD reader, run on the road layout drawing")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        analysis = {key: settings.get("analysis." + key) for key in SETTINGS}
        data = read_plots(args.cad)
        blocks = read_blocks(args.blocks) if args.blocks else None
        roads = read_road_edges(args.roads, settings.get("analysis.roadEdgeLayers")) if args.roads else None
        result = analyse(data["plots"], profile, analysis, None if blocks is None else blocks["blocks"], roads, None if blocks is None else blocks.get("source"))
    except project_profile.ProfileError as error:
        print("cad_analysis: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "analysis.json", {"source": data.get("source"), **result})
    summary = result["summary"]
    print("clusters " + str(summary["clusters"]) + " " + json.dumps(summary["byCode"]) + ", issues " + str(summary["issues"]))
    if summary["blocks"] is not None:
        print("blocks " + json.dumps(summary["blocks"]))
    print("fronts " + json.dumps(summary["frontRules"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
