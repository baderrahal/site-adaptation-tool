import argparse
import json
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import surface_lookup
import surface_roles
from common import write_json

DEFAULT_CONFIG = Path(__file__).resolve().parent.parent / "config" / "road_surfaces.json"
CORNERS = 4
CHECK_TOLERANCE = 0.02
# Edges of the bands of the corner level minus the road level, in metres. The last band is everything above
DIFF_EDGES = [0.01, 0.03, 0.1, 0.3]
DIFF_LABELS = ["upTo10mm", "upTo30mm", "upTo100mm", "upTo300mm", "more"]
EPS = 1e-9
SPOT_RADIUS = 0.05


def load_config(path):
    config = json.loads(Path(path).read_text(encoding="utf-8"))
    roles = {name: entry["role"] for name, entry in config["surfaces"].items()}
    roads = [name for name, role in roles.items() if role == "road"]
    if len(roads) > 1:
        raise ValueError("more than one surface has the role road: " + ", ".join(roads))
    config["roles"] = roles
    config["road"] = roads[0] if roads else None
    # The road is a design surface too, so the tables over the design surfaces keep their meaning
    config["design"] = [name for name, role in roles.items() if role in ("design", "road")]
    config["ground"] = [name for name, role in roles.items() if role == "ground"]
    return config


def host_document(data):
    for document in data["documents"]:
        if document.get("kind") == "project" and document.get("floors"):
            return document
    raise ValueError("no project document with floors in the file")


def offset_of(document):
    """Shared coordinates equal internal ones plus the survey point shared position minus its internal position."""
    position, shared = document["surveyPoint"]["position"], document["surveyPoint"]["sharedPosition"]
    return [shared[axis] - position[axis] for axis in range(3)]


def split_plot(text):
    """The Plot parameter holds the unit, a dash and the plot number. A text with no dash has no unit."""
    if not text:
        return None, None
    unit, dash, plot = str(text).rpartition("-")
    return (unit or None), plot


def corner_points(floor, offset):
    """Corners as (easting, northing, level) in shared coordinates and where they came from.

    The corners key holds the real level of each corner. Only a floor without it falls back to the sketch outline,
    whose z is a sketch plane, so the level is then the top elevation.
    """
    if floor.get("corners"):
        return [(point[0] + offset[0], point[1] + offset[1], point[2] + offset[2]) for point in floor["corners"]], "corners"
    if floor.get("sketchBoundary"):
        level = floor["topElevation"] + offset[2]
        return [(point[0] + offset[0], point[1] + offset[1], level) for point in floor["sketchBoundary"]], "sketchBoundary"
    return [], None


def ramp_rows(document, types):
    """One row per ramp floor with its corners in shared coordinates and its top elevation in shared elevation.

    Returns the rows and a count of the ramp floors left out, with no corners at all or not four.
    """
    offset = offset_of(document)
    rows, left_out = [], Counter()
    for floor in document["floors"]:
        if floor["typeName"] not in types:
            continue
        points, source = corner_points(floor, offset)
        if not points:
            left_out["noCorners"] += 1
            continue
        if len(points) != CORNERS:
            left_out["notFourCorners"] += 1
            continue
        unit, plot = split_plot((floor.get("parameters") or {}).get("Plot"))
        rows.append({"plot": plot, "unit": unit, "elementId": floor["elementId"], "type": floor["typeName"], "topElevation": floor["topElevation"] + offset[2], "source": source,
                     "corners": [(point[0], point[1]) for point in points], "levels": [point[2] for point in points]})
    return rows, left_out


def band_of(distance, bands):
    if distance is None:
        return "more"
    for edge in bands:
        if distance <= edge:
            return "upTo" + str(edge)
    return "more"


def pairs_kind(covered):
    """Two covered corners of a four corner ramp are neighbouring when they share an edge and opposite when they do not."""
    first, second = covered
    return "neighbouring" if (second - first) % CORNERS in (1, CORNERS - 1) else "opposite"


def surface_pass(landxml_dir, name, rows, limit):
    """z at every corner and the distance to the nearest visible triangle for the corners the surface does not cover."""
    surface = surface_lookup.load_surface(landxml_dir, name)
    levels, distances = [], []
    for row in rows:
        row_levels = [surface.z_at(easting, northing) for easting, northing in row["corners"]]
        levels.append(row_levels)
        distances.append([None if z is not None else surface.distance_to(easting, northing, limit) for z, (easting, northing) in zip(row_levels, row["corners"])])
    return levels, distances


def count_by_corners(per_ramp_covered):
    histogram = Counter(len(covered) for covered in per_ramp_covered)
    return {str(count): histogram.get(count, 0) for count in range(CORNERS + 1)}


def two_corner_kinds(per_ramp_covered):
    kinds = Counter(pairs_kind(covered) for covered in per_ramp_covered if len(covered) == 2)
    return {"neighbouring": kinds.get("neighbouring", 0), "opposite": kinds.get("opposite", 0)}


def diff_band(value):
    size = abs(value)
    for edge, label in zip(DIFF_EDGES, DIFF_LABELS):
        if size <= edge + EPS:
            return label
    return DIFF_LABELS[-1]


def road_differences(rows, road_levels):
    """Corner level minus road level per ramp and corner, None where the road does not cover the corner."""
    return [[None if road_levels[index][corner] is None else row["levels"][corner] - road_levels[index][corner] for corner in range(CORNERS)] for index, row in enumerate(rows)]


def difference_bands(differences):
    found = [value for row in differences for value in row if value is not None]
    counts = Counter(diff_band(value) for value in found)
    exclusive = {label: counts.get(label, 0) for label in DIFF_LABELS}
    within = {label: sum(exclusive[other] for other in DIFF_LABELS[:number + 1]) for number, label in enumerate(DIFF_LABELS[:-1])}
    return {"covered": len(found), "bands": exclusive, "cumulative": within}


def neighbouring_pairs(rows, differences, covered_by):
    """For ramps with exactly 2 covered neighbouring corners, how many have both, one or none within 30 mm of the road, by floor type."""
    result = {}
    for index, row in enumerate(rows):
        covered = covered_by[index]
        if len(covered) != 2 or pairs_kind(covered) != "neighbouring":
            continue
        close = sum(1 for corner in covered if abs(differences[index][corner]) <= surface_roles.NEAR + EPS)
        for key in (row["type"], "all types"):
            result.setdefault(key, Counter())[("none", "one", "both")[close]] += 1
    return {key: {label: counts.get(label, 0) for label in ("both", "one", "none")} for key, counts in sorted(result.items())}


def spot_check(document, rows, offset):
    """Second method for the corner levels. Spot elevations whose origin lies within 5 cm of a ramp corner, in plan, and the spot z minus the corner z."""
    spots = [spot for spot in document.get("spotElevations") or [] if len(spot.get("origin") or []) > 2]
    corners = [(point, level) for row in rows for point, level in zip(row["corners"], row["levels"])]
    result = {"spots": len(spots), "corners": len(corners), "spotsNearCorner": 0, "cornersWithSpot": 0}
    if not spots or not corners:
        return result
    spot_xy = np.array([[spot["origin"][0] + offset[0], spot["origin"][1] + offset[1]] for spot in spots])
    spot_z = np.array([spot["origin"][2] + offset[2] for spot in spots])
    corner_xy = np.array([point for point, level in corners])
    corner_z = np.array([level for point, level in corners])
    distance = np.hypot(spot_xy[:, None, 0] - corner_xy[None, :, 0], spot_xy[:, None, 1] - corner_xy[None, :, 1])
    near_spot = distance.min(axis=1) <= SPOT_RADIUS + EPS
    has_spot = distance.min(axis=0) <= SPOT_RADIUS + EPS
    result["spotsNearCorner"] = int(near_spot.sum())
    result["cornersWithSpot"] = int(has_spot.sum())
    if has_spot.any():
        gaps = spot_z[distance.argmin(axis=0)[has_spot]] - corner_z[has_spot]
        result["spotMinusCorner"] = {"median": float(np.median(gaps)), "min": float(gaps.min()), "max": float(gaps.max()), "withinOneMm": int((np.abs(gaps) <= 0.001 + EPS).sum())}
    return result


def role_report(landxml_dir, config, rows, got):
    """Measures every surface of the LandXML file at the ramp corners and proposes a role for each. The config is only read."""
    summary_path = Path(landxml_dir) / "landxml_summary.json"
    listed = json.loads(summary_path.read_text(encoding="utf-8"))["surfaces"] if summary_path.is_file() else [{"name": name} for name in config["design"] + config["ground"]]
    corner_levels = [level for row in rows for level in row["levels"]]
    stats = {}
    for entry in listed:
        name = entry["name"]
        levels = got[name][0] if name in got else surface_pass(landxml_dir, name, rows, max(config["bands"]))[0]
        total = entry.get("faceCount")
        share = entry["invisibleFaceCount"] / total if total else None
        stats[name] = surface_roles.measure(corner_levels, [level for row_levels in levels for level in row_levels], share)
    return surface_roles.report(stats, config["roles"])


def run(prototypes, landxml_dir, config, out_dir, check_plot=None):
    data = json.loads(Path(prototypes).read_text(encoding="utf-8"))
    document = host_document(data)
    rows, left_out = ramp_rows(document, set(config["rampTypes"]))
    bands = config["bands"]
    design, ground, road = config["design"], config["ground"], config["road"]
    names = design + ground
    got = {name: surface_pass(landxml_dir, name, rows, max(bands)) for name in names}
    road_levels = got[road][0] if road else [[None] * CORNERS for _ in rows]
    differences = road_differences(rows, road_levels)

    records, covered_by = [], []
    for index, row in enumerate(rows):
        corners, covered = [], []
        for corner in range(CORNERS):
            levels = {name: got[name][0][index][corner] for name in design if got[name][0][index][corner] is not None}
            under = {name: got[name][0][index][corner] for name in ground if got[name][0][index][corner] is not None}
            corners.append({"corner": corner, "cornerLevel": row["levels"][corner], "roadLevel": road_levels[index][corner], "difference": differences[index][corner], "levels": levels, "ground": under})
            if levels:
                covered.append(corner)
        covered_by.append(covered)
        records.append({"plot": row["plot"], "unit": row["unit"], "elementId": row["elementId"], "type": row["type"], "topElevation": row["topElevation"], "corners": corners})
    write_json(Path(out_dir) / "ramp_road_levels.json", {"ramps": records})

    summary = {"rampCount": len(rows), "designSurfaces": design, "groundSurfaces": ground, "roadSurface": road}
    summary["cornerSource"] = {"corners": sum(1 for row in rows if row["source"] == "corners"), "sketchBoundary": sum(1 for row in rows if row["source"] == "sketchBoundary"),
                               "noCorners": left_out.get("noCorners", 0), "notFourCorners": left_out.get("notFourCorners", 0)}
    summary["coveredCorners"] = count_by_corners(covered_by)
    summary["twoCorners"] = two_corner_kinds(covered_by)
    summary["perSurface"] = {}
    for name in names:
        per_ramp = [[corner for corner in range(CORNERS) if got[name][0][index][corner] is not None] for index in range(len(rows))]
        entry = {"coveredCorners": count_by_corners(per_ramp), "cornersCovered": sum(len(item) for item in per_ramp), "twoCorners": two_corner_kinds(per_ramp)}
        gaps = [rows[index]["topElevation"] - got[name][0][index][corner] for index in range(len(rows)) for corner in range(CORNERS) if got[name][0][index][corner] is not None]
        if gaps:
            entry["topMinusSurface"] = {"min": float(min(gaps)), "max": float(max(gaps)), "above": int(sum(1 for gap in gaps if gap > 0)), "count": len(gaps)}
        summary["perSurface"][name] = entry
    summary["gapBands"] = gap_bands(rows, got, design, covered_by, bands)
    if road:
        road_covered = [[corner for corner in range(CORNERS) if road_levels[index][corner] is not None] for index in range(len(rows))]
        summary["roadDifference"] = difference_bands(differences)
        summary["roadCoveredCorners"] = count_by_corners(road_covered)
        summary["roadNeighbouringPairs"] = neighbouring_pairs(rows, differences, road_covered)
    summary["spots"] = spot_check(document, rows, offset_of(document))
    roles = role_report(landxml_dir, config, rows, got)
    write_json(Path(out_dir) / "role_proposal.json", roles)
    summary["roleWarnings"] = roles["warnings"]
    write_json(Path(out_dir) / "road_levels_summary.json", summary)
    if check_plot is not None:
        write_json(Path(out_dir) / "plot_check.json", plot_check(document, rows, records, check_plot, set(config["rampTypes"])))
    return summary


def gap_bands(rows, got, design, covered_by, bands):
    """For corners no design surface covers, and for ramps with no covered corner, how far each design surface is, in bands."""
    labels = ["upTo" + str(edge) for edge in bands] + ["more"]
    result = {"corners": {}, "ramps": {}}
    for name in design:
        corner_bands, ramp_bands = Counter(), Counter()
        for index, row in enumerate(rows):
            if len(covered_by[index]) == CORNERS:
                continue
            nearest = []
            for corner in range(CORNERS):
                if corner in covered_by[index]:
                    continue
                distance = got[name][1][index][corner]
                corner_bands[band_of(distance, bands)] += 1
                nearest.append(distance)
            if not covered_by[index]:
                known = [distance for distance in nearest if distance is not None]
                ramp_bands[band_of(min(known) if known else None, bands)] += 1
        result["corners"][name] = {label: corner_bands.get(label, 0) for label in labels}
        result["ramps"][name] = {label: ramp_bands.get(label, 0) for label in labels}
    return result


def other_levels(document, plot, types):
    """Levels the Revit file holds for a plot besides the ramp floors, the link Prototype-FFL and the other floors of the plot."""
    offset = offset_of(document)[2]
    levels = {}
    for link in document.get("links", []):
        if str(link.get("plotNumber")) == plot and str(link.get("ffl", "")).replace(".", "", 1).isdigit():
            levels["linkFfl"] = float(link["ffl"])
    for floor in document["floors"]:
        unit, number = split_plot(floor["parameters"].get("Plot"))
        if number == plot and floor["typeName"] not in types:
            levels[floor["typeName"]] = floor["topElevation"] + offset
    return levels


def plot_check(document, rows, records, plot, types):
    """Road level of every corner of the ramps of one plot against the levels Revit holds. The street corner is not known, so every corner is listed."""
    found = []
    others = other_levels(document, str(plot), types)
    for row, record in zip(rows, records):
        if row["plot"] != str(plot):
            continue
        corners = []
        for corner in record["corners"]:
            diffs = {name: z - row["topElevation"] for name, z in corner["levels"].items()}
            against = {name + " minus " + label: z - level for name, z in corner["levels"].items() for label, level in others.items()}
            near = [key for key, diff in {**{name + " minus top": diff for name, diff in diffs.items()}, **against}.items() if abs(diff) <= CHECK_TOLERANCE]
            corners.append({"corner": corner["corner"], "levels": corner["levels"], "minusTop": diffs, "minusOther": against, "withinTolerance": near})
        found.append({"plot": row["plot"], "unit": row["unit"], "type": row["type"], "topElevation": row["topElevation"], "otherLevels": others, "corners": corners})
    return {"plot": str(plot), "tolerance": CHECK_TOLERANCE, "ramps": found}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Look up the road level at every corner of the ramp floors of prototypes.json.")
    parser.add_argument("out_dir")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG))
    parser.add_argument("--prototypes")
    parser.add_argument("--landxml-dir", required=True, help="output folder of landxml_reader.py")
    parser.add_argument("--check-plot")
    args = parser.parse_args(argv)
    config = load_config(args.config)
    prototypes = args.prototypes or str(Path(config["folder"]) / config["prototypes"])
    summary = run(prototypes, args.landxml_dir, config, args.out_dir, args.check_plot)
    print("ramps " + str(summary["rampCount"]) + ", covered corners " + json.dumps(summary["coveredCorners"]))
    for line in summary["roleWarnings"]:
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
