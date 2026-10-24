"""Unit codes of the baseplan plots, then clusters of duplex and townhouse plots, then the coded rule checks on them.

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
import link_table
import project_profile
import rules_checks
from rules_checks import UNKNOWN

CLUSTER_KINDS = ("duplex", "townhouse")
SETTINGS = ("toleranceM", "partyWallShare", "endShare", "unitPlotTypes", "noRev6Match")
# A shared segment is longer than this many tolerances, so two plots that only touch at a corner are not adjacent
SEGMENT_FACTOR = 4
# A plot counts as covered by a block when it holds at least this share of the block outline. Only the report uses it
COVER_SHARE = 0.2
# Two shares closer than this are a tie. The two halves of a duplex block always tie
TIE = 1e-6
FRONT_RULES = ("single plot", "neighbour", "road edge", "none", "not a row")
DEFAULT_SHIFTS = Path(__file__).resolve().parent.parent / "config" / "plot_numbers.json"


def plot_key(label):
    text = str(label)
    return (0, int(text), text) if text.isdecimal() else (1, 0, text)


def clean(polygon):
    """A face that crosses itself would stop the union of a row, so it is repaired first."""
    return polygon if polygon.is_valid else polygon.buffer(0)


def kind_of(profile, typology):
    entry = profile.get("typologies").get(typology) if isinstance(typology, str) else None
    return entry.get("kind") if entry else None


def whole_number(value):
    """The whole number a plot number cell holds, else None. A superscript digit or a fraction is not one."""
    if isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value if value >= 0 else None
    if isinstance(value, float):
        return int(value) if value.is_integer() and value >= 0 else None
    text = str(value).strip() if value is not None else ""
    if text.endswith(".0"):
        text = text[:-2]
    if not (text.isascii() and text.isdecimal()):
        return None
    try:
        return int(text)
    except ValueError:
        # Python refuses a text of thousands of digits, and that is not a plot number either
        return None


def has_unit_type(label_type, unit_types):
    """The label type of a face is read as text, so a face with a list or a number there is not a unit plot."""
    return isinstance(label_type, str) and label_type in unit_types


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

    plots are all the baseplan faces with plot, vertices and the unit code of R69 (unit, unitSource), and a block with
    name, mirrored and rotation when one is known. The unit code and never the baseplan label type decides the kind. Two plots are side by side when they share a party wall, which is at least partyWallShare
    of the long side of a plot. Back to back plots share a shorter edge and stay in separate clusters. A row of
    side by side plots is cut into the listed clusters of the profile (R70). roads is a list of road edge lines
    and gives the front of a row that the neighbours leave open (R71).
    """
    tolerance = analysis["toleranceM"]
    long_side = profile.metres("plot.long_side")
    reach = analysis["endShare"] * long_side
    outlines = [clean(Polygon([(vertex[0], vertex[1]) for vertex in plot["vertices"]])) for plot in plots]
    tree = STRtree(outlines)
    road_tree = STRtree(roads) if roads else None
    members = [index for index, plot in enumerate(plots) if kind_of(profile, plot.get("unit")) in CLUSTER_KINDS]
    pairs = adjacent_pairs([outlines[index] for index in members], tolerance)
    walls = {(members[first], members[second]): shared for (first, second), shared in pairs.items() if shared.length >= analysis["partyWallShare"] * long_side}
    links = {index: [] for index in members}
    for first, second in walls:
        links[first].append(second)
        links[second].append(first)
    known = {code: tuple(units) for code, units in profile.get("clusters").items()}

    def label(index):
        return str(plots[index]["plot"])

    used_ids = set()

    def row_id_of(whole):
        """The lowest plot number of the row. Two rows that share it, which a duplicate label makes possible, get a number after it."""
        base = min((label(index) for index in whole), key=plot_key)
        found, number = base, 1
        while found in used_ids:
            number += 1
            found = base + "#" + str(number)
        used_ids.add(found)
        return found

    def cluster_of(indexes, code, sure, row, rule, whole, row_id):
        blocks = [plots[index].get("block") or {} for index in indexes]
        return {
            "code": code,
            "plots": [label(index) for index in indexes],
            "typologies": [plots[index]["unit"] for index in indexes],
            "sources": [plots[index].get("unitSource") for index in indexes],
            "mirrored": [mirrored_of(plots[index].get("block")) for index in indexes],
            "rotation": [block.get("rotation") for block in blocks],
            "block": [block.get("name") for block in blocks],
            "orderSure": sure,
            "row": row,
            "frontRule": rule,
            "rowId": row_id,
            "rowUnits": len(whole),
        }

    clusters = []
    for group in groups_of(members, links):
        chain = chain_of(group, links)
        if chain is None:
            chain = sorted(group, key=lambda index: plot_key(plots[index]["plot"]))
            clusters.append(cluster_of(chain, UNKNOWN, False, False, "not a row", chain, row_id_of(chain)))
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
        units = [plots[index]["unit"] for index in chain]
        flags = [mirrored_of(plots[index].get("block")) for index in chain]
        # Without a known front the direction of the row is a guess, so each piece may also read reversed
        pieces, ways = best_split(units, flags, known, profile, reverse_ok=not sure)
        row_id = row_id_of(chain)
        for start, end, code, flipped in split_pieces(len(chain), pieces):
            indexes = chain[start:end][::-1] if flipped else chain[start:end]
            clusters.append(cluster_of(indexes, code, sure and ways == 1, True, rule, chain, row_id))
    clusters.sort(key=lambda cluster: min(plot_key(label) for label in cluster["plots"]))
    return clusters


def plot_faces(entries):
    """Copies of the faces, so the blocks attached later never reach the caller's data."""
    return [dict(entry) for entry in entries if entry.get("kind") == "plot_shape" and entry.get("plot") not in (None, "") and len(entry.get("vertices") or []) >= 3]


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


def unit_blocks_without_outline(blocks, profile):
    """How many inserts are named like a unit block but have no outline of three points, so villa_blocks leaves them out."""
    codes = set(profile.get("typologies"))
    count = 0
    for block in blocks:
        name = block.get("name") or ""
        code = name.rsplit("_", 1)[-1] if "_" in name else None
        if code in codes and len(block.get("outline") or []) < 3:
            count += 1
    return count


def holds_cover(share):
    """True when a face holds at least COVER_SHARE of a block. A true 0.2 can come out a hair under at coordinates this size, so a tie width of slack is allowed."""
    return share >= COVER_SHARE - TIE


def face_polygons(faces):
    return [clean(Polygon([(vertex[0], vertex[1]) for vertex in face["vertices"]])) for face in faces]


def outline_of(block):
    return clean(Polygon([(point[0], point[1]) for point in block["outline"]]))


def shares_of(outline, polygons, tree):
    """(share, index) of every face that holds part of the outline, a share being its part of the outline area."""
    if outline.is_empty or outline.area <= 0:
        return []
    found = []
    for index in tree.query(outline):
        index = int(index)
        share = outline.intersection(polygons[index]).area / outline.area
        if share > 0:
            found.append((share, index))
    return found


def owner_of(shares, faces):
    """The index of the face that holds the largest share and whether another face is within a millionth of it.
    A tie goes to the lowest plot number."""
    top = max(share for share, _ in shares)
    best = sorted((index for share, index in shares if top - share <= TIE), key=lambda index: plot_key(faces[index]["plot"]))
    return best[0], len(best) > 1


def claim_blocks(faces, villas):
    """R69. The blocks each face owns, the blocks it is only covered by, and the faces each block stands for.

    A block belongs to the face that holds the largest share of its outline, and shares that tie go to the lowest
    plot number. A face that holds at least COVER_SHARE of it is covered by it too. A block stands for its owner and
    for every face it covers, so a duplex block stands for two plots and a townhouse block for four. A block whose
    largest share is under COVER_SHARE only touches the edge of a plot, or is spread too thin over many, so it
    belongs to no plot.
    """
    polygons = face_polygons(faces)
    tree = STRtree(polygons)
    owned = [[] for _ in faces]
    covered = [[] for _ in faces]
    stands_for = []
    stats = {"tied": 0, "noPlot": 0, "covering": Counter()}
    for number, block in enumerate(villas):
        shares = shares_of(outline_of(block), polygons, tree)
        if not shares or not holds_cover(max(share for share, _ in shares)):
            stats["noPlot"] += 1
            stands_for.append([])
            continue
        owner, tied = owner_of(shares, faces)
        stats["tied"] += tied
        held = {index for share, index in shares if holds_cover(share)}
        stats["covering"][len(held)] += 1
        owned[owner].append(number)
        for index in held - {owner}:
            covered[index].append(number)
        stands_for.append(sorted(held | {owner}))
    return owned, covered, stands_for, stats


def give_units(faces, villas, claims, units, unit_types, report):
    """R69. The unit code of each plot, from the block first, then from the Excel unit, else none.

    The block of a plot is the one block it owns. A plot that owns none takes the one block that covers it, and a
    plot whose deciding blocks are two or more takes none and gets an issue. A block that stands for several plots
    gives its code to each of them with mirrored null and a flag, since one flag cannot say which unit is mirrored.
    The Excel unit comes in by baseplan label. The baseplan label type decides nothing here, it only says which plots
    get an issue when no unit code is found, and report false leaves that issue out. Returns the issues and counts.
    """
    owned, covered, stands_for, _ = claims
    issues, counts = [], Counter()
    for index, face in enumerate(faces):
        for key in ("unit", "unitSource", "block", "excelUnit"):
            face.pop(key, None)
        plot = str(face["plot"])
        head = "Plot " + plot + " (" + str(face.get("type")) + ") has "
        deciding = owned[index] or covered[index]
        face["blocks"], face["flags"] = len(deciding), []
        if len(deciding) >= 2:
            counts["two"] += 1
            issues.append(rules_checks.issue("R69", [plot], head + str(len(deciding)) + " blocks: " + ", ".join(str(villas[number].get("name")) for number in deciding)))
        elif deciding:
            counts["own" if owned[index] else "coveredOnly"] += 1
            block = villas[deciding[0]]
            count = len(stands_for[deciding[0]])
            face["unit"], face["unitSource"] = block["code"], "block"
            face["block"] = {"name": block.get("name"), "mirrored": mirrored_of(block) if count == 1 else None, "rotation": block.get("rotationDeg"), "plots": count}
            if count > 1:
                face["flags"].append("block " + str(block.get("name")) + " covers " + str(count) + " plots")
            if owned[index] and covered[index]:
                face["flags"].append("also covered by " + ", ".join(str(villas[number].get("name")) for number in covered[index]))
        found = units.get(plot)
        if found is not None:
            face["excelUnit"] = found
            # A plot with two blocks is a conflict, so the schedule does not settle it
            if "unit" not in face and len(deciding) < 2:
                face["unit"], face["unitSource"] = found, "excel"
        if "unit" not in face and len(deciding) < 2 and report and has_unit_type(face.get("type"), unit_types):
            issues.append(rules_checks.issue("R69", [plot], head + "no unit code, no block and no Excel unit"))
    return issues, counts


def plot_records(faces, unit_types):
    """One record per plot that has a unit code or a block, or whose label type says a unit stands on it."""
    records = []
    for face in faces:
        if "unit" not in face and not face["blocks"] and not has_unit_type(face.get("type"), unit_types):
            continue
        block = face.get("block") or {}
        records.append({"plot": str(face["plot"]), "cadType": face.get("type"), "unit": face.get("unit"), "unitSource": face.get("unitSource"), "excelUnit": face.get("excelUnit"),
                        "block": block.get("name"), "mirrored": block.get("mirrored"), "rotation": block.get("rotation"), "blocks": face["blocks"], "blockPlots": block.get("plots"), "flags": face["flags"]})
    records.sort(key=lambda record: plot_key(record["plot"]))
    return records


def block_summary(records, stats, counts, villas, source, unit_types):
    """Counts over the plot records. blocksCovering counts the blocks by how many plots hold at least COVER_SHARE of them."""
    with_block = [record for record in records if record["block"] is not None]
    return {
        "source": source,
        "villaBlocks": len(villas),
        "plotsOneBlock": sum(1 for record in records if record["blocks"] == 1),
        "plotsCoveredOnly": counts["coveredOnly"],
        "plotsNoBlock": sum(1 for record in records if record["blocks"] == 0),
        "plotsTwoBlocks": sum(1 for record in records if record["blocks"] >= 2),
        "plotsOfSeveralPlotBlocks": sum(1 for record in with_block if record["blockPlots"] > 1),
        "otherPlotsWithBlocks": sum(1 for record in with_block if not has_unit_type(record["cadType"], unit_types)),
        "tiedBlocks": stats["tied"],
        "blocksOnNoPlot": stats["noPlot"],
        "unitBlocksNoOutline": stats["noOutline"],
        "blocksCovering": {str(count): number for count, number in sorted(stats["covering"].items())},
        "mirrored": sum(1 for record in with_block if record["mirrored"] is True),
        "notMirrored": sum(1 for record in with_block if record["mirrored"] is False),
        "mirrorNull": sum(1 for record in with_block if record["mirrored"] is None),
    }


def unit_summary(records, profile, read, dropped):
    """Where the unit codes come from, and the block unit against the Excel unit for the plots that have both."""
    sources = Counter(record["unitSource"] or "none" for record in records)
    both = [record for record in records if record["unitSource"] == "block" and record["excelUnit"]]
    differ = Counter(record["unit"] + ">" + record["excelUnit"] for record in both if record["unit"] != record["excelUnit"])
    return {
        "plots": len(records),
        "bySource": {key: sources[key] for key in ("block", "excel", "none")},
        "byCode": dict(sorted(Counter(record["unit"] for record in records if record["unit"]).items())),
        "byCodeFromBlock": dict(sorted(Counter(record["unit"] for record in records if record["unitSource"] == "block").items())),
        "notInProfile": dropped,
        "plotsWithBoth": len(both),
        "blockUnitAgrees": len(both) - sum(differ.values()),
        "blockUnitDiffers": sum(differ.values()),
        "differing": dict(sorted(differ.items(), key=lambda item: (-item[1], item[0]))),
        "excel": read,
    }


def check_settings(profile, analysis):
    """The two lists in the analysis settings must agree with the profile, so a typo is an error and not a silent miss."""
    biggest = profile.get("cluster_block_max")
    if isinstance(biggest, bool) or not isinstance(biggest, int) or biggest < 1:
        raise project_profile.ProfileError("cluster_block_max must be a whole number of plots, 1 or more")
    for name in ("unitPlotTypes", "noRev6Match"):
        values = analysis.get(name, [])
        if not isinstance(values, list) or any(not isinstance(value, str) for value in values):
            raise project_profile.ProfileError("analysis." + name + " must be a list of codes")
    if analysis.get("noRev6Match"):
        clusters, aliases = profile.get("clusters"), profile.get("cluster_aliases")
        for code in analysis["noRev6Match"]:
            if code not in clusters:
                raise project_profile.ProfileError("analysis.noRev6Match names " + code + ", which is not a cluster of the profile")
            if code in aliases:
                raise project_profile.ProfileError("analysis.noRev6Match names " + code + ", which cluster_aliases gives a Rev 6 cluster")


def analyse(entries, profile, analysis, blocks=None, roads=None, blocks_source=None, excel=None):
    """The clusters, the unit code of each plot and the issues. excel is the result of read_excel_units."""
    check_settings(profile, analysis)
    faces = plot_faces(entries)
    unit_types = set(analysis.get("unitPlotTypes", []))
    villas = villa_blocks(blocks, profile) if blocks is not None else []
    claims = claim_blocks(faces, villas)
    claims[3]["noOutline"] = 0 if blocks is None else unit_blocks_without_outline(blocks, profile)
    # A unit text that is not a code of the profile is left out with an issue and never normalised
    typologies = profile.get("typologies")
    units = {label: unit for label, unit in (excel["units"] if excel else {}).items() if unit in typologies}
    dropped = [(label, unit) for label, unit in (excel["units"] if excel else {}).items() if unit not in typologies]
    # When no source gave any unit every plot lacks one, so one issue says so instead of 2000 repeats of it
    report = bool(villas) or bool(units)
    issues_units, counts = give_units(faces, villas, claims, units, unit_types, report)
    if blocks is None and excel is None:
        issues_units.append(rules_checks.issue("R69", [], "No block drawing and no Excel unit was given, so no plot has a unit code and no cluster can be built"))
    elif blocks is not None and not villas:
        if claims[3]["noOutline"]:
            text = "The block drawing holds no unit block with an outline, " + str(claims[3]["noOutline"]) + " of its " + str(len(blocks)) + " inserts are named after a typology of the profile but have no outline of three points"
        else:
            text = "The block drawing holds no unit block, none of its " + str(len(blocks)) + " inserts is named after a typology of the profile"
        issues_units.append(rules_checks.issue("R69", [], text))
    if excel is not None and not units:
        issues_units.append(rules_checks.issue("R69", [], "The schedule workbooks gave no unit code of the profile, so they give no plot a unit code"))
    if excel:
        issues_units += excel["issues"]
    issues_units += [rules_checks.issue("R69", [label], "Plot " + label + " has the Excel unit " + repr(unit) + ", which is not a unit code of the profile, so it gives no unit code") for label, unit in dropped]
    biggest = profile.get("cluster_block_max")
    labels_of = [str(face["plot"]) for face in faces]
    for number, held in enumerate(claims[2]):
        if len(held) > biggest:
            issues_units.append(rules_checks.issue("R69", [labels_of[index] for index in held], "Block " + str(villas[number].get("name")) + " stands for " + str(len(held)) + " plots, more than the " + str(biggest) + " of the biggest cluster"))
    records = plot_records(faces, unit_types)
    labels = set(labels_of)
    read = None if excel is None else {**excel["stats"], "noFace": sum(1 for label in excel["units"] if label not in labels)}
    # Where the block and the schedule give a plot different units the block wins, and the pair is listed so the conflict is never silent
    conflicts = [{"plot": record["plot"], "block": record["block"], "blockUnit": record["unit"], "excelUnit": record["excelUnit"]}
                 for record in records if record["unitSource"] == "block" and record["excelUnit"] and record["unit"] != record["excelUnit"]]
    clusters = derive_clusters(faces, profile, analysis, roads)
    issues = rules_checks.check_attachment(clusters, profile) + rules_checks.check_known_clusters(clusters) + rules_checks.check_unmatched_codes(clusters, analysis.get("noRev6Match", [])) + issues_units
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
        "blocks": None if blocks is None else block_summary(records, claims[3], counts, villas, blocks_source, unit_types),
        "units": unit_summary(records, profile, read, len(dropped)),
        "issues": len(issues),
        "numbering": "baseplan labels",
    }
    return {"summary": summary, "clusters": clusters, "plots": records, "issues": issues, "conflicts": conflicts}


def read_excel_units(books, shifts):
    """R69. The unit of each plot in the schedule workbooks, keyed by baseplan label (the official number minus the shift of R28).

    books are coords.json files as the Excel reader writes them. A workbook whose neighbourhood has no shift is refused,
    since the shift is never guessed. A row with no numeric plot number or no unit is left out with an issue, and two
    rows that end on one baseplan label are both left out with an issue.
    """
    units, ambiguous, issues = {}, set(), []
    stats = {"rows": 0, "used": 0}
    where = {}
    for book in books:
        source = str(book.get("source"))
        code = link_table.neighbourhood_of(source, shifts)
        if code is None or code not in shifts["shifts"]:
            raise project_profile.ProfileError("no plot number shift for the workbook " + source)
        for row in book.get("rows") or []:
            stats["rows"] += 1
            place = source + " row " + str(row.get("sheetRow"))
            plot = next((row[header] for header in row if header.strip().lower().startswith("plot no")), None)
            unit = next((row[header] for header in row if header.strip().rstrip("*").strip().lower() == "unit"), None)
            key = whole_number(plot)
            if key is None:
                issues.append(rules_checks.issue("R69", [], place + " has no numeric plot number"))
            elif not isinstance(unit, str) or not unit.strip():
                issues.append(rules_checks.issue("R69", [], place + " (plot " + str(key) + ") has no unit"))
            else:
                label = str(key - shifts["shifts"][code])
                if label in where:
                    if label not in ambiguous:
                        stats["used"] -= 1
                    ambiguous.add(label)
                    units.pop(label, None)
                    issues.append(rules_checks.issue("R69", [label], place + " and " + where[label] + " both end on baseplan label " + label + ", so neither gives a unit"))
                else:
                    units[label] = unit.strip()
                    stats["used"] += 1
                    where[label] = place
    stats["ambiguous"] = len(ambiguous)
    return {"units": units, "stats": stats, "issues": issues}


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


def read_coords(paths):
    """The coords.json files of the Excel reader, as given on the command line."""
    books = []
    for path in paths:
        try:
            book = json.loads(Path(path).read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            raise project_profile.ProfileError(str(path) + " cannot be read: " + str(error)) from error
        if not isinstance(book, dict) or not isinstance(book.get("rows"), list):
            raise project_profile.ProfileError(str(path) + " has no rows list")
        books.append(book)
    return books


def read_shifts(path):
    try:
        return link_table.load_shifts(path)
    except (OSError, ValueError, KeyError, TypeError) as error:
        raise project_profile.ProfileError("the plot number shifts cannot be read from " + str(path) + ": " + str(error)) from error


def main(argv=None):
    parser = argparse.ArgumentParser(description="Clusters of duplex and townhouse plots from the baseplan faces, then the coded checks of rules/README.md and docs/rules.md.")
    parser.add_argument("out_dir", help="where analysis.json is written")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader, run on the baseplan")
    parser.add_argument("--blocks", help="folder with blocks.json from the CAD reader, run on the villa block drawing")
    parser.add_argument("--roads", help="folder with pieces.json from the CAD reader, run on the road layout drawing")
    parser.add_argument("--coords", nargs="+", help="coords.json files of the schedule workbooks, their Unit column gives the unit of a plot that has no block")
    parser.add_argument("--shifts", default=str(DEFAULT_SHIFTS), help="plot number shift config, used with --coords")
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
        excel = read_excel_units(read_coords(args.coords), read_shifts(args.shifts)) if args.coords else None
        result = analyse(data["plots"], profile, analysis, None if blocks is None else blocks["blocks"], roads, None if blocks is None else blocks.get("source"), excel)
    except project_profile.ProfileError as error:
        print("cad_analysis: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "analysis.json", {"source": data.get("source"), **result})
    summary = result["summary"]
    print("clusters " + str(summary["clusters"]) + " " + json.dumps(summary["byCode"]) + ", issues " + str(summary["issues"]))
    if summary["blocks"] is not None:
        print("blocks " + json.dumps(summary["blocks"]))
    print("units " + json.dumps({key: value for key, value in summary["units"].items() if key != "differing"}))
    print("fronts " + json.dumps(summary["frontRules"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
