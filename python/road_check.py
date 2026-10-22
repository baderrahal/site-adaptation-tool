import argparse
import json
import mmap
import random
import re
import sys
from pathlib import Path

import numpy as np
import shapely

import landxml_reader
import surface_lookup
import ramp_levels
from common import write_json

SAMPLE_POINTS = 200
SAMPLE_RAMPS = 6
STORED_TOLERANCE = 0.0006
SURFACE = re.compile(rb'<Surface name="([^"]*)"')


def raw_counts(xml_path):
    """Second method for the reader. Counts the tags of every surface straight from the bytes and returns the first points of a few ids."""
    counts = {}
    with open(xml_path, "rb") as handle, mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as data:
        marks = [(match.group(1).decode("utf-8"), match.start()) for match in SURFACE.finditer(data)]
        for number, (name, start) in enumerate(marks):
            end = marks[number + 1][1] if number + 1 < len(marks) else len(data)
            body = data[start:end]
            counts[name] = {"points": body.count(b"<P id="), "faces": body.count(b"<F "), "invisible": body.count(b'<F i="1"'), "start": start}
    return counts


POINT = re.compile(rb'<P id="(\d+)">([^<]*)</P>')
FACE = re.compile(rb"<F ([^>]*)>(\d+) (\d+) (\d+)</F>")


def raw_compare(xml_path, start, end, xyz, faces):
    """Reads one surface again with regular expressions and compares every point and every visible face with the arrays of the reader."""
    with open(xml_path, "rb") as handle, mmap.mmap(handle.fileno(), 0, access=mmap.ACCESS_READ) as data:
        body = data[start:end]
    points = POINT.findall(body)
    ids = np.array([int(item[0]) for item in points], dtype=np.int64)
    values = np.array([[float(part) for part in item[1].split()] for item in points]).reshape(-1, 3)
    own = xyz[:, [1, 0, 2]]
    point_bad = int((~np.isclose(values, own, rtol=0, atol=1e-6).all(axis=1)).sum()) if len(values) == len(own) else max(len(values), len(own))
    rows = [item for item in FACE.findall(body) if b'i="1"' not in item[0]]
    order = np.argsort(ids)
    triples = np.array([[int(item[1]), int(item[2]), int(item[3])] for item in rows], dtype=np.int64).reshape(-1, 3)
    sorted_ids = ids[order]
    where = np.minimum(np.searchsorted(sorted_ids, triples), len(ids) - 1)
    known = (sorted_ids[where] == triples).all(axis=1)
    mapped = order[where[known]]
    face_bad = int((mapped != faces).any(axis=1).sum()) if len(mapped) == len(faces) else max(len(mapped), len(faces))
    return point_bad, face_bad


def plane_z(triangle, px, py):
    """z at a point from the plane through three vertices, found by solving the plane equation, None for a flat sliver."""
    matrix = np.column_stack([triangle[:, 0] - px, triangle[:, 1] - py, np.ones(3)])
    try:
        coefficients = np.linalg.solve(matrix, triangle[:, 2])
    except np.linalg.LinAlgError:
        return None
    return float(coefficients[2])


class ShapelySurface:
    """Visible triangles as shapely polygons with a tree, the way a different library would answer the same question."""

    def __init__(self, folder):
        xyz, faces = np.load(Path(folder) / "points.npy"), np.load(Path(folder) / "faces.npy")
        self.triangles = xyz[faces]
        self.tree = shapely.STRtree(shapely.polygons(self.triangles[:, :, :2]))

    def z_at_many(self, points):
        """z per point, None outside, and the largest spread of z between triangles that share the point."""
        hits = self.tree.query(shapely.points(np.array(points)), predicate="intersects")
        found = [[] for _ in points]
        for point_index, tri_index in zip(hits[0], hits[1]):
            z = plane_z(self.triangles[tri_index], points[point_index][0], points[point_index][1])
            if z is not None:
                found[point_index].append(z)
        return [(sum(item) / len(item) if item else None) for item in found], max([max(item) - min(item) for item in found if item] or [0.0])


def corner_points(document, types):
    """Corners in shared coordinates, written again here and not taken from ramp_levels."""
    base = document["surveyPoint"]
    shift = [base["sharedPosition"][axis] - base["position"][axis] for axis in range(3)]
    ramps = []
    for floor in document["floors"]:
        if floor["typeName"] in types:
            ramps.append({"plot": str(floor["parameters"]["Plot"]).split("-")[-1], "type": floor["typeName"], "top": floor["topElevation"] + shift[2], "corners": [(p[0] + shift[0], p[1] + shift[1]) for p in floor["boundary"]]})
    return ramps


def compare(first, second):
    """Returns the number of points where both give a value, where both give none, where only one gives a value, and the largest difference."""
    both, none, split, worst = 0, 0, 0, 0.0
    for left, right in zip(first, second):
        if left is None and right is None:
            none += 1
        elif left is None or right is None:
            split += 1
        else:
            both += 1
            worst = max(worst, abs(left - right))
    return both, none, split, worst


def check(xml_path, landxml_dir, levels_path, prototypes, config, seed):
    rng = random.Random(seed)
    summary = json.loads((Path(landxml_dir) / "landxml_summary.json").read_text(encoding="utf-8"))
    result = {"seed": seed}

    raw = raw_counts(xml_path)
    reader = []
    for entry in summary["surfaces"]:
        counts = raw.get(entry["name"])
        same = counts is not None and counts["points"] == entry["pointCount"] + entry["badPointCount"] and counts["faces"] == entry["faceCount"] and counts["invisible"] == entry["invisibleFaceCount"]
        reader.append({"surface": entry["name"], "countsMatch": same})
    result["readerCounts"] = {"surfaces": len(reader), "match": sum(1 for item in reader if item["countsMatch"]), "mismatch": [item["surface"] for item in reader if not item["countsMatch"]]}

    ends = sorted(item["start"] for item in raw.values()) + [Path(xml_path).stat().st_size]
    points_bad, faces_bad, points_seen, faces_seen = 0, 0, 0, 0
    for entry in summary["surfaces"]:
        start = raw[entry["name"]]["start"]
        folder = Path(landxml_dir) / entry["folder"]
        xyz, faces = np.load(folder / "points.npy"), np.load(folder / "faces.npy")
        point_bad, face_bad = raw_compare(xml_path, start, ends[ends.index(start) + 1], xyz, faces)
        points_bad, faces_bad, points_seen, faces_seen = points_bad + point_bad, faces_bad + face_bad, points_seen + len(xyz), faces_seen + len(faces)
    result["rawRead"] = {"points": points_seen, "pointsDifferent": points_bad, "visibleFaces": faces_seen, "facesDifferent": faces_bad}

    document = ramp_levels.host_document(json.loads(Path(prototypes).read_text(encoding="utf-8")))
    ramps = corner_points(document, set(config["rampTypes"]))
    stored = {}
    for record in json.loads(Path(levels_path).read_text(encoding="utf-8"))["ramps"]:
        stored[(record["plot"], record["type"])] = record
    all_corners = [(ramp["plot"], ramp["type"], index, corner) for ramp in ramps for index, corner in enumerate(ramp["corners"])]
    sample = rng.sample(all_corners, min(SAMPLE_POINTS, len(all_corners)))
    picked = rng.sample(ramps, min(SAMPLE_RAMPS, len(ramps)))
    result["sampledRamps"] = [ramp["plot"] for ramp in picked]
    points = [item[3] for item in sample] + [corner for ramp in picked for corner in ramp["corners"]]

    names = config["design"] + config["ground"]
    tally = {"points": len(sample), "surfaces": len(names), "bothValue": 0, "bothNone": 0, "onlyOne": 0, "largestDifference": 0.0, "sharedPointSpread": 0.0}
    rampChecks = {"ramps": len(picked), "cornersCompared": 0, "mismatch": [], "largestDifference": 0.0}
    for name in names:
        surface = ShapelySurface(Path(landxml_dir) / landxml_reader.folder_name(name))
        values, spread = surface.z_at_many(points)
        tally["sharedPointSpread"] = max(tally["sharedPointSpread"], spread)
        mine = surface_lookup.load_surface(landxml_dir, name)
        own = [mine.z_at(easting, northing) for easting, northing in points[:len(sample)]]
        both, none, split, worst = compare(values[:len(sample)], own)
        tally["bothValue"] += both
        tally["bothNone"] += none
        tally["onlyOne"] += split
        tally["largestDifference"] = max(tally["largestDifference"], worst)
        offset = len(sample)
        for ramp in picked:
            record = stored[(ramp["plot"], ramp["type"])]
            for index in range(len(ramp["corners"])):
                key = "levels" if name in config["design"] else "ground"
                kept = record["corners"][index][key].get(name)
                fresh = values[offset + index]
                rampChecks["cornersCompared"] += 1
                if (kept is None) != (fresh is None) or (kept is not None and abs(kept - fresh) > STORED_TOLERANCE):
                    rampChecks["mismatch"].append(ramp["plot"] + " corner " + str(index) + " " + name)
                elif kept is not None:
                    rampChecks["largestDifference"] = max(rampChecks["largestDifference"], abs(kept - fresh))
            offset += len(ramp["corners"])
    for ramp in picked:
        record = stored[(ramp["plot"], ramp["type"])]
        if abs(record["topElevation"] - ramp["top"]) > STORED_TOLERANCE:
            rampChecks["mismatch"].append(ramp["plot"] + " top elevation")
    result["shapelyLookup"] = tally
    result["rampFile"] = rampChecks
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second check of the LandXML reader, the lookup and the ramp file, by other methods.")
    parser.add_argument("out_dir")
    parser.add_argument("--xml", required=True)
    parser.add_argument("--landxml-dir", required=True)
    parser.add_argument("--levels", required=True, help="ramp_road_levels.json")
    parser.add_argument("--prototypes", required=True)
    parser.add_argument("--config", default=str(ramp_levels.DEFAULT_CONFIG))
    parser.add_argument("--seed", type=int)
    args = parser.parse_args(argv)
    seed = args.seed if args.seed is not None else random.SystemRandom().randrange(1 << 30)
    result = check(args.xml, args.landxml_dir, args.levels, args.prototypes, ramp_levels.load_config(args.config), seed)
    write_json(Path(args.out_dir) / "road_check.json", result)
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
