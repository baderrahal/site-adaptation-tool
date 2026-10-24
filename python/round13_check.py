"""Second methods for the road level lookup (R91) and the block offsets (R97), each worked out another way.

road: random points go through a plain search over every triangle of every surface in the order, with no grid. offsets:
random plots of block_offsets.json are worked out again from the raw prototypes.json and blocks.json with plain trigonometry
and no matrix helper. Both draw their sample with a seed, which is in the report. The reports hold counts and distances only.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import numpy as np

import cad_analysis
import common
import landxml_reader
import plan_from_past
import project_profile
import ramp_levels
import road_lookup

SEED = 13
SAMPLE = 10
# The grid lookup and the plain search must agree to a micrometre, they use the same plane
LEVEL_TOLERANCE = 1e-6
# The report rounds each axis of an offset to the millimetre, which can move the distance by 0.71 mm
OFFSET_TOLERANCE = 0.0008
ANGLE_TOLERANCE = 0.05
EPS = 1e-9


def plain_z(xyz, faces, easting, northing):
    """z of the first triangle, in face order, that holds the point, by barycentric coordinates over all triangles. None outside."""
    a, b, c = xyz[faces[:, 0]], xyz[faces[:, 1]], xyz[faces[:, 2]]
    det = (b[:, 1] - c[:, 1]) * (a[:, 0] - c[:, 0]) + (c[:, 0] - b[:, 0]) * (a[:, 1] - c[:, 1])
    ok = det != 0
    with np.errstate(divide="ignore", invalid="ignore"):
        l0 = ((b[:, 1] - c[:, 1]) * (easting - c[:, 0]) + (c[:, 0] - b[:, 0]) * (northing - c[:, 1])) / det
        l1 = ((c[:, 1] - a[:, 1]) * (easting - c[:, 0]) + (a[:, 0] - c[:, 0]) * (northing - c[:, 1])) / det
    l2 = 1.0 - l0 - l1
    hit = np.flatnonzero(ok & (l0 >= -EPS) & (l1 >= -EPS) & (l2 >= -EPS))
    if len(hit) == 0:
        return None
    first = hit[0]
    return float(l0[first] * a[first, 2] + l1[first] * b[first, 2] + l2[first] * c[first, 2])


def plain_level(surfaces, easting, northing):
    """(level, name) from the first surface in the order whose triangles hold the point, else (None, None)."""
    for name, xyz, faces in surfaces:
        z = plain_z(xyz, faces, easting, northing)
        if z is not None:
            return z, name
    return None, None


def check_road(lookup, landxml_dir, points, seed=SEED, sample=SAMPLE):
    """Compares the lookup with the plain search on a random sample of points. Returns counts and the largest level difference."""
    chosen = random.Random(seed).sample(list(points), min(sample, len(points)))
    surfaces = []
    for name in lookup.names:
        folder = Path(landxml_dir) / landxml_reader.folder_name(name)
        surfaces.append((name, np.load(folder / "points.npy").reshape(-1, 3).astype(np.float64), np.load(folder / "faces.npy").reshape(-1, 3).astype(np.int64)))
    same, largest, differing = 0, 0.0, []
    covered = 0
    for easting, northing in chosen:
        mine = lookup.level_at(easting, northing)
        plain = plain_level(surfaces, easting, northing)
        covered += mine[0] is not None
        if mine[1] == plain[1] and ((mine[0] is None) == (plain[0] is None)) and (mine[0] is None or abs(mine[0] - plain[0]) <= LEVEL_TOLERANCE):
            same += 1
            largest = max(largest, 0.0 if mine[0] is None else abs(mine[0] - plain[0]))
        else:
            differing.append({"easting": easting, "northing": northing, "lookup": mine, "plain": plain})
    return {"seed": seed, "points": len(chosen), "covered": covered, "agree": same, "differ": len(differing), "largestLevelDifference": largest, "differing": differing}


def hand_offset(block, link_origin, link_transform):
    """The local offset and the relation of a link in the axes of a block, by plain trigonometry.

    local x is the world offset along the turned X axis of the block divided by its X scale, local y the same along the turned Y axis.
    The relation is the angle of the link X axis in those axes and whether the link is mirrored against the block.
    """
    turn = math.radians(block.get("rotationDeg") or 0.0)
    sx, sy = block["scale"][0], block["scale"][1]
    dx, dy = link_origin[0] - block["insertPoint"][0], link_origin[1] - block["insertPoint"][1]
    local = ((dx * math.cos(turn) + dy * math.sin(turn)) / sx, (-dx * math.sin(turn) + dy * math.cos(turn)) / sy)
    ax, ay = link_transform["axisX"][0], link_transform["axisX"][1]
    bx, by = link_transform["axisY"][0], link_transform["axisY"][1]
    ux, uy = (ax * math.cos(turn) + ay * math.sin(turn)) / sx, (-ax * math.sin(turn) + ay * math.cos(turn)) / sy
    angle = math.degrees(math.atan2(uy, ux)) % 360.0
    link_det = ax * by - ay * bx
    mirrored = link_det * (1 if sx * sy > 0 else -1) < 0
    return local, angle, mirrored


def check_offsets(rows, villas, links, seed=SEED, sample=SAMPLE):
    """Compares random rows of block_offsets.json with the hand worked offset. links maps a plot to (origin, transform) from the raw file."""
    chosen = random.Random(seed).sample(rows, min(sample, len(rows)))
    agree, worst, worst_angle, differing = 0, 0.0, 0.0, []
    for row in chosen:
        origin, transform = links[row["plot"]]
        local, angle, mirrored = hand_offset(villas[row["blockNumber"]], origin, transform)
        gap = math.hypot(local[0] - row["offset"][0], local[1] - row["offset"][1])
        turn_gap = abs(angle - row["rotation"]) % 360.0
        turn_gap = min(turn_gap, 360.0 - turn_gap)
        worst, worst_angle = max(worst, gap), max(worst_angle, turn_gap)
        if gap <= OFFSET_TOLERANCE and turn_gap <= ANGLE_TOLERANCE and mirrored == row["mirrored"]:
            agree += 1
        else:
            differing.append({"plot": row["plot"], "gap": gap, "turnGap": turn_gap})
    return {"seed": seed, "plots": len(chosen), "agree": agree, "differ": len(differing), "largestOffsetGap": worst, "largestTurnGap": worst_angle, "differing": differing}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check the road lookup and the block offsets a second way on a random sample.")
    parser.add_argument("command", choices=("road", "offsets"))
    parser.add_argument("out_dir")
    parser.add_argument("--landxml-dir", help="road: output folder of landxml_reader.py")
    parser.add_argument("--points", help="road: entry_proof.json, the curb points of its workbooks are drawn from, or a json list of [easting, northing]")
    parser.add_argument("--surfaces", default=str(ramp_levels.DEFAULT_CONFIG))
    parser.add_argument("--offsets-report", help="offsets: block_offsets.json of block_offsets.py")
    parser.add_argument("--blocks", help="offsets: folder with blocks.json of the footprint drawing")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--seed", type=int, default=SEED)
    args = parser.parse_args(argv)
    try:
        if args.command == "road":
            config = ramp_levels.load_config(args.surfaces)
            lookup = road_lookup.load(config, args.landxml_dir)
            found = json.loads(Path(args.points).read_text(encoding="utf-8"))
            if isinstance(found, dict):
                found = [point for book in found["workbooks"].values() for point in book["curbPoints"]]
            points = [tuple(point) for point in found]
            result = check_road(lookup, args.landxml_dir, points, args.seed)
        else:
            settings = project_profile.load_settings(args.project)
            profile = project_profile.load_profile(project_profile.profile_path(settings))
            job = plan_from_past.load_job(settings)
            data = json.loads(Path(job["prototypes"]).read_text(encoding="utf-8"))
            document = ramp_levels.host_document(data)
            shift = ramp_levels.offset_of(document)
            raw = {}
            for link in document.get("links") or []:
                plot = plan_from_past.plot_number((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number"))
                origin = (link.get("transform") or {}).get("origin")
                if plot and origin:
                    raw[plot] = ([origin[axis] + shift[axis] for axis in range(3)], link["transform"])
            villas = cad_analysis.villa_blocks(cad_analysis.read_blocks(args.blocks)["blocks"], profile)
            rows = json.loads(Path(args.offsets_report).read_text(encoding="utf-8"))["rows"]
            result = check_offsets(rows, villas, raw, args.seed)
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("round13_check: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / ("check_" + args.command + ".json"), result)
    print(args.command + ": seed " + str(result["seed"]) + ", " + str(result.get("points", result.get("plots"))) + " drawn, agree " + str(result["agree"]) + ", differ " + str(result["differ"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
