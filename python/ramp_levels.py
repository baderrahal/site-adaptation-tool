import argparse
import json
import sys
from collections import Counter
from pathlib import Path

import numpy as np

import surface_lookup
from common import write_json

DEFAULT_CONFIG = Path(__file__).resolve().parent.parent / "config" / "road_surfaces.json"
CORNERS = 4
CHECK_TOLERANCE = 0.02


def load_config(path):
    config = json.loads(Path(path).read_text(encoding="utf-8"))
    roles = {name: entry["role"] for name, entry in config["surfaces"].items()}
    config["design"] = [name for name, role in roles.items() if role == "design"]
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


def ramp_rows(document, types):
    """One row per ramp floor with its corners in shared coordinates and its top elevation in shared elevation."""
    offset = offset_of(document)
    rows = []
    for floor in document["floors"]:
        if floor["typeName"] not in types:
            continue
        unit, plot = split_plot(floor["parameters"].get("Plot"))
        corners = [(point[0] + offset[0], point[1] + offset[1]) for point in floor["boundary"]]
        rows.append({"plot": plot, "unit": unit, "elementId": floor["elementId"], "type": floor["typeName"], "topElevation": floor["topElevation"] + offset[2], "corners": corners})
    return rows


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


def run(prototypes, landxml_dir, config, out_dir, check_plot=None):
    data = json.loads(Path(prototypes).read_text(encoding="utf-8"))
    document = host_document(data)
    rows = ramp_rows(document, set(config["rampTypes"]))
    bands = config["bands"]
    design, ground = config["design"], config["ground"]
    names = design + ground
    got = {name: surface_pass(landxml_dir, name, rows, max(bands)) for name in names}

    records, covered_by = [], []
    for index, row in enumerate(rows):
        corners, covered = [], []
        for corner in range(CORNERS):
            levels = {name: got[name][0][index][corner] for name in design if got[name][0][index][corner] is not None}
            under = {name: got[name][0][index][corner] for name in ground if got[name][0][index][corner] is not None}
            corners.append({"corner": corner, "levels": levels, "ground": under})
            if levels:
                covered.append(corner)
        covered_by.append(covered)
        records.append({"plot": row["plot"], "unit": row["unit"], "elementId": row["elementId"], "type": row["type"], "topElevation": row["topElevation"], "corners": corners})
    write_json(Path(out_dir) / "ramp_road_levels.json", {"ramps": records})

    summary = {"rampCount": len(rows), "designSurfaces": design, "groundSurfaces": ground}
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
    return 0


if __name__ == "__main__":
    sys.exit(main())
