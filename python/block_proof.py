"""Second method for the block to plot match of R69, by plain polygon clipping (R76).

The match in cad_analysis.py uses a geometry library. This file works out the same shares with Sutherland-Hodgman
clipping, a face against the convex outline of the block, and compares the owner of every block and the plots it stands
for. It reads the same two folders and changes nothing. The report holds counts and ids and is kept outside git.
"""
import argparse
import sys
from pathlib import Path

import cad_analysis
import common
import project_profile

# Two faces are the same polygon after a repair only if they do not cross themselves, so a face that does is left out
EPSILON = 1e-12
# Two shares closer than this are a near tie. The outline of a block is rounded to a millimetre, which moves a share by about this much
NEAR_TIE = 1e-4


def signed_area(points):
    return sum(x1 * y2 - x2 * y1 for (x1, y1), (x2, y2) in zip(points, points[1:] + points[:1])) / 2.0


def convex_hull(points):
    """The convex hull, counter clockwise, with collinear points dropped. The outline of a block is a hull rounded to a millimetre,
    which can leave three nearly straight points a hair concave, and the clipping below needs a convex outline."""
    unique = sorted(set(tuple(point[:2]) for point in points))
    if len(unique) < 3:
        return unique

    def half(sequence):
        chain = []
        for point in sequence:
            while len(chain) >= 2 and (chain[-1][0] - chain[-2][0]) * (point[1] - chain[-2][1]) - (chain[-1][1] - chain[-2][1]) * (point[0] - chain[-2][0]) <= 0:
                chain.pop()
            chain.append(point)
        return chain[:-1]

    return half(unique) + half(unique[::-1])


def left_of(point, start, end):
    return (end[0] - start[0]) * (point[1] - start[1]) - (end[1] - start[1]) * (point[0] - start[0]) >= 0


def cut(first, second, start, end):
    """Where the segment first to second crosses the line through start and end."""
    dx, dy = end[0] - start[0], end[1] - start[1]
    ex, ey = second[0] - first[0], second[1] - first[1]
    denominator = dx * ey - dy * ex
    along = ((first[0] - start[0]) * dy - (first[1] - start[1]) * dx) / denominator
    return (first[0] + along * ex, first[1] + along * ey)


def clip_area(subject, outline):
    """The area of the subject polygon inside the convex outline. The outline may run either way round."""
    clip = convex_hull(outline)
    if len(clip) < 3:
        return 0.0
    result = [tuple(point[:2]) for point in subject]
    for start, end in zip(clip, clip[1:] + clip[:1]):
        if not result:
            return 0.0
        source, result = result, []
        previous = source[-1]
        for current in source:
            if left_of(current, start, end):
                if not left_of(previous, start, end):
                    result.append(cut(previous, current, start, end))
                result.append(current)
            elif left_of(previous, start, end):
                result.append(cut(previous, current, start, end))
            previous = current
    return abs(signed_area(result)) if len(result) >= 3 else 0.0


def segments_cross(a, b, c, d):
    def side(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])

    return side(a, b, c) * side(a, b, d) < -EPSILON and side(c, d, a) * side(c, d, b) < -EPSILON


def crosses_itself(points):
    count = len(points)
    edges = [(points[index], points[(index + 1) % count]) for index in range(count)]
    for first in range(count):
        for second in range(first + 2, count):
            if first == 0 and second == count - 1:
                continue
            if segments_cross(*edges[first], *edges[second]):
                return True
    return False


def box_of(points):
    xs, ys = [point[0] for point in points], [point[1] for point in points]
    return min(xs), min(ys), max(xs), max(ys)


def overlap(first, second):
    return first[0] <= second[2] and second[0] <= first[2] and first[1] <= second[3] and second[1] <= first[3]


def claim(faces, villas):
    """Per block, the owner face index and the faces it stands for, worked out by clipping. Faces that cross themselves are left out."""
    rings = [[(vertex[0], vertex[1]) for vertex in face["vertices"]] for face in faces]
    comparable = [not crosses_itself(ring) for ring in rings]
    boxes = [box_of(ring) for ring in rings]
    found = []
    for block in villas:
        outline = convex_hull(block["outline"])
        total = abs(signed_area(outline)) if len(outline) >= 3 else 0.0
        shares = []
        if total > 0:
            block_box = box_of(outline)
            for index, ring in enumerate(rings):
                if comparable[index] and overlap(boxes[index], block_box):
                    share = clip_area(ring, outline) / total
                    if share > 0:
                        shares.append((share, index))
        # A block whose largest share is under the cover share belongs to no plot, as in claim_blocks
        if not shares or not cad_analysis.holds_cover(max(share for share, _ in shares)):
            found.append(None)
            continue
        top = max(share for share, _ in shares)
        owner = sorted((index for share, index in shares if top - share <= cad_analysis.TIE), key=lambda index: cad_analysis.plot_key(faces[index]["plot"]))[0]
        held = {index for share, index in shares if cad_analysis.holds_cover(share)}
        found.append({"owner": owner, "stands": sorted(held | {owner}), "top": top, "shares": {index: share for share, index in shares}})
    return found, comparable


def compare(faces, villas):
    """The owner and the plots of every block by the library and by clipping, and the deciding block of every plot."""
    owned, covered, stands_for, _ = cad_analysis.claim_blocks(faces, villas)
    mine, comparable = claim(faces, villas)
    owner_of = {number: index for index, numbers in enumerate(owned) for number in numbers}
    agree = differ = near = skipped = 0
    different = []
    for number, found in enumerate(mine):
        crossing = any(not comparable[index] for index in stands_for[number])
        if crossing:
            skipped += 1
        elif (found is None) != (not stands_for[number]) or (found is not None and found["stands"] != stands_for[number]):
            differ += 1
            different.append({"block": villas[number].get("name"), "handle": villas[number].get("handle")})
        elif found is not None and found["owner"] != owner_of.get(number):
            # Same plots, another owner. Real when the owner of the library holds a clearly smaller share by clipping, else a near tie
            held = found["shares"].get(owner_of.get(number), 0.0)
            if found["top"] - held <= NEAR_TIE:
                near += 1
            else:
                differ += 1
                different.append({"block": villas[number].get("name"), "handle": villas[number].get("handle")})
        else:
            agree += 1
    # The deciding block of a plot is the block it owns, else the one that covers it, as in give_units
    own_mine = [[] for _ in faces]
    cover_mine = [[] for _ in faces]
    for number, found in enumerate(mine):
        if found is not None:
            own_mine[found["owner"]].append(number)
            for index in found["stands"]:
                if index != found["owner"]:
                    cover_mine[index].append(number)
    plots_agree = plots_differ = 0
    plots_different = []
    for index, face in enumerate(faces):
        if not comparable[index]:
            continue
        theirs, ours = owned[index] or covered[index], own_mine[index] or cover_mine[index]
        if theirs == ours:
            plots_agree += 1
        else:
            plots_differ += 1
            plots_different.append(str(face["plot"]))
    return {
        "blocks": len(villas), "blocksAgree": agree, "blocksNearTie": near, "blocksDiffer": differ, "blocksNotComparable": skipped,
        "plots": len(faces), "plotsAgree": plots_agree, "plotsDiffer": plots_differ,
        "facesCrossingThemselves": sum(1 for flag in comparable if not flag),
        "withABlock": sum(1 for index in range(len(faces)) if comparable[index] and (own_mine[index] or cover_mine[index])),
        "differingBlocks": different[:20], "differingPlots": plots_different[:20],
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check the block to plot match of R69 a second way, by polygon clipping.")
    parser.add_argument("out_dir", help="where block_proof.json is written")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader, run on the baseplan")
    parser.add_argument("--blocks", required=True, help="folder with blocks.json from the CAD reader, run on the block drawing")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        faces = cad_analysis.plot_faces(cad_analysis.read_plots(args.cad)["plots"])
        villas = cad_analysis.villa_blocks(cad_analysis.read_blocks(args.blocks, settings)["blocks"], profile)
        result = compare(faces, villas)
    except project_profile.ProfileError as error:
        print("block_proof: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "block_proof.json", result)
    print("blocks " + str(result["blocks"]) + ": agree " + str(result["blocksAgree"]) + ", owner flips on a near tie " + str(result["blocksNearTie"]) + ", differ " + str(result["blocksDiffer"]) + ", not comparable " + str(result["blocksNotComparable"]))
    print("plots " + str(result["plots"]) + ": agree " + str(result["plotsAgree"]) + ", differ " + str(result["plotsDiffer"]) + ", faces crossing themselves " + str(result["facesCrossingThemselves"]) + ", with a block " + str(result["withABlock"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
