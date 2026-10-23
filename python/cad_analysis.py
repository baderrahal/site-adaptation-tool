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

from shapely.geometry import LineString, Point, Polygon
from shapely.ops import unary_union
from shapely.strtree import STRtree

import common
import project_profile
import rules_checks
from rules_checks import UNKNOWN

CLUSTER_KINDS = ("duplex", "townhouse")
# A shared segment is longer than this many tolerances, so two plots that only touch at a corner are not adjacent
SEGMENT_FACTOR = 4


def plot_key(label):
    text = str(label)
    return (0, int(text), text) if text.isdigit() else (1, 0, text)


def kind_of(profile, typology):
    entry = profile.get("typologies").get(typology)
    return entry.get("kind") if entry else None


def mirrored_from_scale(block):
    """A block is mirrored when exactly one of its X and Y scales is negative. No block or no scale gives None."""
    scale = (block or {}).get("scale")
    if not scale or len(scale) < 2:
        return None
    return scale[0] * scale[1] < 0


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


def groups_of(count, links):
    seen, groups = set(), []
    for start in range(count):
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


def front_of(centre, direction, road, half, margin):
    """The unit vector from the cluster towards the road and whether one end is clearly nearer, or (None, False)."""
    if direction is None or road is None:
        return None, False
    ahead = road.distance(Point(centre.x + direction[0] * half, centre.y + direction[1] * half))
    behind = road.distance(Point(centre.x - direction[0] * half, centre.y - direction[1] * half))
    front = direction if ahead <= behind else (-direction[0], -direction[1])
    return front, abs(ahead - behind) > margin


def left_to_right(chain, centres, front):
    """Facing the road from the plots, the left side is a quarter turn anticlockwise from the front."""
    right = (front[1], -front[0])
    first, last = centres[chain[0]], centres[chain[-1]]
    start = first.x * right[0] + first.y * right[1]
    end = last.x * right[0] + last.y * right[1]
    return chain if start <= end else chain[::-1]


def derive_clusters(plots, roads, profile, analysis):
    """Clusters of side by side duplex and townhouse plots.

    plots are baseplan faces with plot, type and vertices, and a block with a scale when one is known.
    roads is a list of road centreline vertex lists, or None. Two plots are side by side when they share
    a party wall, which is at least partyWallShare of the long side of a plot. Back to back plots share a
    shorter edge and stay in separate clusters.
    """
    tolerance = analysis["toleranceM"]
    long_side = profile.metres("plot.long_side")
    members = [plot for plot in plots if kind_of(profile, plot.get("type")) in CLUSTER_KINDS]
    outlines = [Polygon([(vertex[0], vertex[1]) for vertex in plot["vertices"]]) for plot in members]
    walls = {pair: shared for pair, shared in adjacent_pairs(outlines, tolerance).items() if shared.length >= analysis["partyWallShare"] * long_side}
    links = {node: [] for node in range(len(members))}
    for first, second in walls:
        links[first].append(second)
        links[second].append(first)
    road = unary_union([LineString(line) for line in roads]) if roads else None
    centres = [shape.centroid for shape in outlines]
    known = {code: tuple(units) for code, units in profile.get("clusters").items()}
    clusters = []
    for group in groups_of(len(members), links):
        chain = chain_of(group, links)
        row = chain is not None
        sure = row
        by_number = sorted(group, key=lambda node: plot_key(members[node]["plot"]))
        if not row:
            chain = by_number
        elif len(chain) > 1:
            shared = max((shared for (first, _), shared in walls.items() if first in group), key=lambda shared: shared.length)
            centre = unary_union([outlines[node] for node in chain]).centroid
            front, sure = front_of(centre, direction_of(shared), road, long_side / 2, analysis["frontMarginM"])
            chain = by_number if front is None else left_to_right(chain, centres, front)
        typologies = [members[node]["type"] for node in chain]
        code = next((name for name, units in known.items() if units == tuple(typologies)), UNKNOWN) if row else UNKNOWN
        clusters.append({
            "code": code,
            "plots": [str(members[node]["plot"]) for node in chain],
            "typologies": typologies,
            "mirrored": [mirrored_from_scale(members[node].get("block")) for node in chain],
            "orderSure": sure,
        })
    clusters.sort(key=lambda cluster: min(plot_key(label) for label in cluster["plots"]))
    return clusters


def plot_faces(entries):
    return [entry for entry in entries if entry.get("kind") == "plot_shape" and entry.get("vertices")]


def road_lines(entries, layers):
    wanted = set(layers)
    return [[(vertex[0], vertex[1]) for vertex in entry["vertices"]] for entry in entries if entry.get("kind") == "polyline" and entry.get("layer") in wanted and len(entry.get("vertices") or []) >= 2]


def analyse(entries, profile, analysis):
    faces = plot_faces(entries)
    clusters = derive_clusters(faces, road_lines(entries, analysis["roadLayers"]), profile, analysis)
    issues = rules_checks.check_attachment(clusters, profile) + rules_checks.check_known_clusters(clusters)
    summary = {
        "plotFaces": len(faces),
        "clusters": len(clusters),
        "clusterPlots": sum(len(cluster["plots"]) for cluster in clusters),
        "byCode": dict(sorted(Counter(cluster["code"] for cluster in clusters).items())),
        "orderNotSure": sum(1 for cluster in clusters if not cluster["orderSure"]),
        "unitsWithMirrorFlag": sum(1 for cluster in clusters for flag in cluster["mirrored"] if flag is not None),
        "issues": len(issues),
        "numbering": "baseplan labels",
    }
    return {"summary": summary, "clusters": clusters, "issues": issues}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Clusters of duplex and townhouse plots from the baseplan faces, then checks 1 and 2 of rules/README.md.")
    parser.add_argument("out_dir", help="where analysis.json is written")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader, run on the baseplan")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        plots_file = Path(args.cad) / "plots.json"
        if not plots_file.is_file():
            raise project_profile.ProfileError("no plots.json in " + str(args.cad))
        data = json.loads(plots_file.read_text(encoding="utf-8"))
        result = analyse(data["plots"], profile, settings.get("analysis"))
    except project_profile.ProfileError as error:
        print("cad_analysis: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "analysis.json", {"source": data.get("source"), **result})
    summary = result["summary"]
    print("clusters " + str(summary["clusters"]) + " " + json.dumps(summary["byCode"]) + ", issues " + str(summary["issues"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
