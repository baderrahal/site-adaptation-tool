"""Where the Revit link and the entrance of a unit sit in its footprint block, measured on the past job (R97 to R100).

A footprint block covers 1, 2 or 4 plots. For each plot of the past job with a block and a villa link, the offset from the
insert point of the block to the link origin is taken in the axes of the block itself, and so is the full transform of the
link against the block. Plots are grouped by block name and by the position of the unit inside the block, counted along
the X axis of the block. Each group is split in half with a fixed seed, one offset and one relation are fitted on the first
half and tested on the second. The fitted values go to config/prototype_offsets.json. Everything is in metres and degrees.
"""
import argparse
import json
import math
import random
import statistics
import sys
from collections import Counter
from pathlib import Path

import cad_analysis
import common
import entry_levels
import levels_proof
import link_table
import plan_from_past
import project_profile
import prototype_offsets
import ramp_levels

SEED = 13
# A group with fewer plots than this has no half to fit and no half to test, so it is reported and not tested
MIN_PLOTS = 4
# Bands of an error in metres. The offset of a unit is judged to the millimetre, the entrance point to the centimetre and the decimetre
OFFSET_BANDS = (("within1mm", 0.001), ("within10mm", 0.010))
ENTRANCE_BANDS = (("within10mm", 0.010), ("within100mm", 0.100))
EPSILON = 1e-9
ROTATION_DECIMALS = 1
# A scale that is not 1 or -1 makes the relation of the link a scaling too, which the fit does not model
SCALE_TOLERANCE = 1e-6
BLOCK_SIZES = (1, 2, 4)
MEASURED = "measured from the IMC past job"


def block_matrix(block):
    """The 2x2 matrix that takes a point of the block definition to the world, rotation times scale, or None without a usable scale.

    World = insert point + matrix times local. A negative scale flips the block, so the local X axis of the block runs
    the other way in the world, which is why the unit position is counted in local axes.
    """
    scale = block.get("scale") or [1, 1, 1]
    sx, sy = scale[0], scale[1]
    if not sx or not sy:
        return None
    turn = math.radians(block.get("rotationDeg") or 0.0)
    cosine, sine = math.cos(turn), math.sin(turn)
    return ((cosine * sx, -sine * sy), (sine * sx, cosine * sy))


def invert(matrix):
    (a, b), (c, d) = matrix
    det = a * d - b * c
    return ((d / det, -b / det), (-c / det, a / det))


def apply(matrix, vector):
    return (matrix[0][0] * vector[0] + matrix[0][1] * vector[1], matrix[1][0] * vector[0] + matrix[1][1] * vector[1])


def multiply(left, right):
    return tuple(tuple(sum(left[row][k] * right[k][col] for k in range(2)) for col in range(2)) for row in range(2))


def to_local(block, matrix_inverse, point):
    """A world point in the axes of the block, in metres from its insert point."""
    insert = block["insertPoint"]
    return apply(matrix_inverse, (point[0] - insert[0], point[1] - insert[1]))


def to_world(block, matrix, local):
    insert = block["insertPoint"]
    moved = apply(matrix, local)
    return (insert[0] + moved[0], insert[1] + moved[1])


def link_matrix(transform):
    """The plan part of a link transform as columns X axis and Y axis, or None without both axes."""
    axis_x, axis_y = transform.get("axisX"), transform.get("axisY")
    if not axis_x or not axis_y:
        return None
    return ((axis_x[0], axis_y[0]), (axis_x[1], axis_y[1]))


def relation(matrix):
    """The relation of a link to its block as (rotation in degrees from 0 up to 360, mirrored, scaled).

    mirrored is a negative determinant. scaled is true when the matrix is not a rotation or a reflection, a unit scale off by
    more than SCALE_TOLERANCE.
    """
    (a, b), (c, d) = matrix
    det = a * d - b * c
    scaled = abs(abs(det) - 1.0) > SCALE_TOLERANCE or abs(math.hypot(a, c) - 1.0) > SCALE_TOLERANCE
    degrees = math.degrees(math.atan2(c, a)) % 360.0
    degrees = 0.0 if round(degrees, ROTATION_DECIMALS) >= 360.0 else degrees
    return degrees, det < 0, scaled


def relation_key(degrees, mirrored):
    return (round(degrees, ROTATION_DECIMALS), bool(mirrored))


def angle_gap(first, second):
    gap = abs(first - second) % 360.0
    return min(gap, 360.0 - gap)


def median_point(points):
    return (statistics.median(point[0] for point in points), statistics.median(point[1] for point in points))


def deciding_blocks(faces, villas):
    """The one block of each face and the faces each block stands for, as in R69. A face with no block or two gets None."""
    owned, covered, stands_for, _ = cad_analysis.claim_blocks(faces, villas)
    chosen = []
    for index in range(len(faces)):
        deciding = owned[index] or covered[index]
        chosen.append(deciding[0] if len(deciding) == 1 else None)
    return chosen, stands_for


def positions(faces, villas, stands_for):
    """The position of every face inside its block, 1 up, counted along the X axis of the block, by (block number, face index)."""
    found = {}
    polygons = cad_analysis.face_polygons(faces)
    for number, held in enumerate(stands_for):
        block = villas[number]
        matrix = block_matrix(block)
        if matrix is None or len(held) not in BLOCK_SIZES:
            continue
        back = invert(matrix)
        local = []
        for index in held:
            local.append((index, to_local(block, back, polygons[index].centroid.coords[0])[0]))
        local.sort(key=lambda item: item[1])
        for rank, (index, _) in enumerate(local, start=1):
            found[(number, index)] = rank
    return found


def measure_plots(faces, villas, links, entries, shifts):
    """R97. One row per plot with a block and a villa link: its group, offset, relation and entrance point in the axes of the block.

    links maps an official plot number to its link (origin x, y and transform), entries maps it to the entry point (x, y) of its
    ramp or is missing it. shifts maps an official number to its baseplan label offset. A plot is left out and counted when it
    has no face, no block, two blocks, a block of another size than 1, 2 or 4 plots, or a link without both axes.
    """
    by_label = {}
    for index, face in enumerate(faces):
        by_label.setdefault(str(face["plot"]), []).append(index)
    chosen, stands_for = deciding_blocks(faces, villas)
    place = positions(faces, villas, stands_for)
    rows, left_out = [], Counter()
    for plot, link in sorted(links.items(), key=lambda item: int(item[0])):
        label = str(int(plot) - shifts)
        found = by_label.get(label, [])
        if len(found) != 1:
            left_out["noFace"] += 1
            continue
        number = chosen[found[0]]
        if number is None:
            left_out["noBlockOrTwo"] += 1
            continue
        block = villas[number]
        matrix = block_matrix(block)
        rank = place.get((number, found[0]))
        link_axes = link_matrix(link["transform"])
        if matrix is None or rank is None:
            left_out["blockSizeOrScale"] += 1
            continue
        if link_axes is None:
            left_out["linkWithoutAxes"] += 1
            continue
        back = invert(matrix)
        local = to_local(block, back, link["origin"])
        degrees, mirrored, scaled = relation(multiply(back, link_axes))
        row = {"plot": plot, "block": cad_analysis.base_name(block.get("name")), "position": rank, "size": len(stands_for[number]), "blockNumber": number, "offset": local,
               "rotation": degrees, "mirrored": mirrored, "scaled": scaled, "blockMirrored": cad_analysis.mirrored_of(block), "entrance": None}
        point = entries.get(plot)
        if point is not None:
            row["entrance"] = to_local(block, back, point)
        rows.append(row)
    return rows, dict(left_out)


def fit_group(rows):
    """The fitted values of rows of one group: median offset, the most common relation and median entrance point.

    The relation is the most common (rotation, mirrored) pair, a tie goes to the smaller pair. share is how many rows have it.
    """
    keys = Counter(relation_key(row["rotation"], row["mirrored"]) for row in rows)
    top = max(keys.values())
    key = sorted(item for item, count in keys.items() if count == top)[0]
    entrance = [row["entrance"] for row in rows if row["entrance"] is not None]
    return {
        "offset": median_point([row["offset"] for row in rows]),
        "rotation": key[0], "mirrored": key[1], "share": top, "relations": len(keys),
        "entrance": median_point(entrance) if entrance else None, "entrances": len(entrance),
    }


def predicted_gap(fit, row, villas, key):
    """Distance in metres between the offset (key offset) or entrance point the fit predicts and the measured one, in the world."""
    block = villas[row["blockNumber"]]
    matrix = block_matrix(block)
    guess = to_world(block, matrix, fit[key])
    actual = to_world(block, matrix, row[key])
    return math.hypot(guess[0] - actual[0], guess[1] - actual[1])


def band_table(errors, bands):
    table = {label: sum(1 for value in errors if value <= limit + EPSILON) for label, limit in bands}
    table["worse"] = sum(1 for value in errors if value > bands[-1][1] + EPSILON)
    table["n"] = len(errors)
    return table


def split_group(rows, seed):
    """The rows of a group in a fixed random order, cut in a fit half and a test half. The fit half is the larger when the count is odd."""
    ordered = sorted(rows, key=lambda row: int(row["plot"]))
    random.Random(seed).shuffle(ordered)
    cut = (len(ordered) + 1) // 2
    return ordered[:cut], ordered[cut:]


def variants_of(rows):
    """The relations a group holds, each with its plots, offset and entrance point. A mirrored unit has its origin on the other side of its plot."""
    by_relation = {}
    for row in rows:
        by_relation.setdefault(relation_key(row["rotation"], row["mirrored"]), []).append(row)
    found = []
    for key, members in sorted(by_relation.items(), key=lambda item: (-len(item[1]), item[0])):
        fit = fit_group(members)
        found.append({"rotationDeg": key[0], "mirrored": key[1], "plots": len(members), "offset": list(fit["offset"]), "entrance": None if fit["entrance"] is None else list(fit["entrance"])})
    return found


def evaluate(rows, villas, seed):
    """R98. Per group of (block, position): plots, the fit on the first half, its error on the first and on the second half.

    A group with fewer plots than MIN_PLOTS is reported with tooFew and no test. The relation is tested too: a test plot
    counts as right when its rotation is within 0.1 degrees of the fitted one and its mirror flag is the same.
    """
    groups = {}
    for row in rows:
        groups.setdefault((row["block"], row["position"]), []).append(row)
    report = []
    for (block, position), members in sorted(groups.items()):
        whole = fit_group(members)
        entry = {"block": block, "position": position, "plots": len(members), "tooFew": len(members) < MIN_PLOTS, "scaled": sum(1 for row in members if row["scaled"]),
                 "blockMirrored": dict(sorted(Counter(str(row["blockMirrored"]) for row in members).items())), "all": whole, "relations": {str(key): count for key, count in Counter(relation_key(row["rotation"], row["mirrored"]) for row in members).items()}}
        if not entry["tooFew"]:
            fit_rows, test_rows = split_group(members, seed)
            half = fit_group(fit_rows)
            entry["fit"] = {"plots": len(fit_rows), "offset": half["offset"], "rotation": half["rotation"], "mirrored": half["mirrored"],
                            "offsetError": band_table([predicted_gap(half, row, villas, "offset") for row in fit_rows], OFFSET_BANDS)}
            entry["test"] = {"plots": len(test_rows), "offsetError": band_table([predicted_gap(half, row, villas, "offset") for row in test_rows], OFFSET_BANDS),
                             "relationRight": sum(1 for row in test_rows if angle_gap(row["rotation"], half["rotation"]) <= 0.1 + EPSILON and row["mirrored"] == half["mirrored"])}
            with_entrance = lambda part: [row for row in part if row["entrance"] is not None]
            if half["entrance"] is not None and with_entrance(test_rows):
                entry["entrance"] = {"fitPlots": len(with_entrance(fit_rows)), "testPlots": len(with_entrance(test_rows)),
                                     "fitError": band_table([predicted_gap(half, row, villas, "entrance") for row in with_entrance(fit_rows)], ENTRANCE_BANDS),
                                     "testError": band_table([predicted_gap(half, row, villas, "entrance") for row in with_entrance(test_rows)], ENTRANCE_BANDS)}
        entry["variants"] = variants_of(members)
        report.append(entry)
    return report


def summarise(report):
    total = Counter()
    for entry in report:
        total["groups"] += 1
        total["tooFew"] += entry["tooFew"]
        total["plots"] += entry["plots"]
        if "test" in entry:
            total["testPlots"] += entry["test"]["plots"]
            for label in ("within1mm", "within10mm", "worse"):
                total["offset" + label] += entry["test"]["offsetError"][label]
            total["relationRight"] += entry["test"]["relationRight"]
        if "entrance" in entry:
            total["entranceTestPlots"] += entry["entrance"]["testPlots"]
            for label in ("within10mm", "within100mm", "worse"):
                total["entrance" + label] += entry["entrance"]["testError"][label]
    return dict(total)


def groups_for_config(report, seed):
    """The groups as config/prototype_offsets.json holds them, the fit over all the plots of a group and the errors of the split."""
    groups = []
    for entry in report:
        whole = entry["all"]
        group = {"block": entry["block"], "position": entry["position"], "plots": entry["plots"], "tooFew": entry["tooFew"],
                 "offset": list(whole["offset"]), "relative": {"rotationDeg": whole["rotation"], "mirrored": whole["mirrored"], "plotsWithIt": whole["share"], "differentRelations": whole["relations"]},
                 "entrance": None if whole["entrance"] is None else list(whole["entrance"]), "entrancePlots": whole["entrances"], "variants": entry["variants"]}
        if "test" in entry:
            group["splitSeed"] = seed
            group["fitError"] = entry["fit"]["offsetError"]
            group["testError"] = entry["test"]["offsetError"]
            group["relationRightOnTest"] = entry["test"]["relationRight"]
            if "entrance" in entry:
                group["entranceTestError"] = entry["entrance"]["testError"]
        groups.append(group)
    return groups


def read_links(document, offset):
    """The villa links of the past job by official plot number, with the origin in shared coordinates and the transform."""
    found = {}
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number"))
        origin = plan_from_past.shared_origin(link, offset)
        if plot and origin and link.get("transform"):
            found.setdefault(plot, []).append({"origin": origin, "transform": link["transform"]})
    return {plot: items[0] for plot, items in found.items() if len(items) == 1}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Measure where the link and the entrance of a unit sit in its footprint block on the past job.")
    parser.add_argument("out_dir", help="where block_offsets.json is written, keep it outside git")
    parser.add_argument("--cad", required=True, help="folder with plots.json of the baseplan")
    parser.add_argument("--blocks", required=True, help="folder with blocks.json of the footprint drawing")
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--surfaces", default=str(ramp_levels.DEFAULT_CONFIG))
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--write-offsets", action="store_true", help="write the groups section of the offsets file")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        config = ramp_levels.load_config(args.surfaces)
        job = plan_from_past.load_job(settings)
        data = json.loads(Path(job["prototypes"]).read_text(encoding="utf-8"))
        document = ramp_levels.host_document(data)
        faces = cad_analysis.plot_faces(cad_analysis.read_plots(args.cad)["plots"])
        villas = cad_analysis.villa_blocks(cad_analysis.read_blocks(args.blocks)["blocks"], profile)
        shifts = link_table.load_shifts(args.shifts)["shifts"][job["neighbourhood"]]
        links = read_links(document, ramp_levels.offset_of(document))
        ramps, _ = ramp_levels.ramp_rows(document, set(config["rampTypes"]))
        entries = {}
        for ramp in ramps:
            entry = entry_levels.ramp_entry([(point[0], point[1], level) for point, level in zip(ramp["corners"], ramp["levels"])])
            if entry is not None and ramp["plot"] is not None:
                entries[plan_from_past.plot_number(ramp["plot"])] = entry["point"]
        rows, left_out = measure_plots(faces, villas, {plot: {"origin": link["origin"], "transform": link["transform"]} for plot, link in links.items()}, entries, shifts)
        report = evaluate(rows, villas, args.seed)
        summary = summarise(report)
        common.write_json(Path(args.out_dir) / "block_offsets.json", {"seed": args.seed, "plotsWithLink": len(links), "measured": len(rows), "leftOut": left_out, "summary": summary, "groups": report, "rows": rows})
        if args.write_offsets:
            prototype_offsets.write_section(args.offsets, "groups", groups_for_config(report, args.seed), prototype_offsets.NOTE)
        print("links " + str(len(links)) + ", measured " + str(len(rows)) + ", left out " + json.dumps(left_out) + ", " + json.dumps(summary))
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("block_offsets: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
