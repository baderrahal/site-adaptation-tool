"""The surfaces of the road surface drawing, read through its LandXML export, matched with the drawing (R155).

The probe plug-in cannot load while SECURELOAD holds it out, so the surface names, counts and levels come from the LandXML
the earlier rounds read, and the drawing side is the count of AECC_TIN_SURFACE in the LISP inventory of R153. Where two
surfaces overlap, the level difference is sampled on a grid. The border of each surface is every visible triangle side that
no other visible triangle shares, the same rule the probe uses in Civil 3D (R152).
"""
import argparse
import json
import statistics
import sys
from pathlib import Path

import numpy as np

import c3d_connect
import c3d_run
import common
import landxml_reader
import surface_lookup


def grid_points(first, second, step):
    """The centres of a square grid inside the overlap of two boxes given as [min x, min y, max x, max y]."""
    low_x, low_y = max(first[0], second[0]), max(first[1], second[1])
    high_x, high_y = min(first[2], second[2]), min(first[3], second[3])
    if high_x <= low_x or high_y <= low_y:
        return []
    xs = np.arange(low_x + step / 2, high_x, step)
    ys = np.arange(low_y + step / 2, high_y, step)
    return [(float(x), float(y)) for x in xs for y in ys]


def difference_stats(differences):
    if not differences:
        return {"count": 0, "medianAbs": None, "worstAbs": None, "medianSigned": None}
    absolute = [abs(value) for value in differences]
    return {"count": len(differences), "medianAbs": statistics.median(absolute), "worstAbs": max(absolute), "medianSigned": statistics.median(differences),
            "within5mm": sum(1 for value in absolute if value <= 0.005), "within30mm": sum(1 for value in absolute if value <= 0.030)}


def overlap(first, second, box_first, box_second, step):
    differences = []
    points = grid_points(box_first, box_second, step)
    for x, y in points:
        a, b = first.z_at(x, y), second.z_at(x, y)
        if a is not None and b is not None:
            differences.append(a - b)
    result = difference_stats(differences)
    result["gridPoints"] = len(points)
    result["stepM"] = step
    return result


def border_edges(faces):
    """The number of triangle sides that only one of the given triangles owns."""
    faces = np.asarray(faces, dtype=np.int64).reshape(-1, 3)
    if len(faces) == 0:
        return 0
    edges = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]])
    edges.sort(axis=1)
    _, counts = np.unique(edges, axis=0, return_counts=True)
    return int((counts == 1).sum())


def border_vertices(faces):
    """The vertex indices on the border of the given triangles."""
    faces = np.asarray(faces, dtype=np.int64).reshape(-1, 3)
    edges = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]])
    edges.sort(axis=1)
    unique, counts = np.unique(edges, axis=0, return_counts=True)
    return np.unique(unique[counts == 1].ravel())


def seam(points, faces, other, tolerance, snap):
    """Where two surfaces abut, the level of the other surface at each border vertex of this one within tolerance."""
    differences = []
    vertices = border_vertices(faces)
    for index in vertices:
        x, y, z = points[index]
        level = snap(other, x, y, tolerance)
        if level is not None:
            differences.append(level - z)
    result = difference_stats(differences)
    result["borderVertices"] = int(len(vertices))
    return result


def box_of(entry):
    box = entry["bbox"]
    return [box["minEasting"], box["minNorthing"], box["maxEasting"], box["maxNorthing"]]


def main(argv=None):
    parser = argparse.ArgumentParser(description="Surfaces of the road surface drawing from its LandXML, matched with the drawing (R155)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    connect = config["connect"]
    landxml = Path(connect["landxmlDir"])
    summary = json.loads((landxml / "landxml_summary.json").read_text(encoding="utf-8"))
    root = Path(config["probeRoot"])
    inventory_path = root / "out" / "inventory" / "inventory.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8")) if inventory_path.exists() else {}
    in_drawing = (inventory.get("roadSurface", {}).get("civil") or {}).get("AECC_TIN_SURFACE")
    surfaces = []
    for entry in summary["surfaces"]:
        faces = np.load(landxml / landxml_reader.folder_name(entry["name"]) / "faces.npy")
        surfaces.append({"name": entry["name"], "points": entry["pointCount"], "triangles": entry["faceCount"], "visibleTriangles": entry["visibleFaceCount"],
                         "zRange": entry["zRange"], "box": box_of(entry), "borderEdges": border_edges(faces)})
    pair = config["surfacePair"]
    named = {entry["name"]: entry for entry in summary["surfaces"]}
    first, second = (surface_lookup.load_surface(landxml, name) for name in pair)
    report = {
        "landxml": summary["file"], "epsg": summary.get("epsgCode"), "unit": summary.get("linearUnit"),
        "surfacesInLandxml": len(surfaces), "surfacesInDrawing": in_drawing,
        "namesMatched": None, "namesNote": "the drawing names need the probe plug-in, SECURELOAD keeps it out",
        "surfaces": surfaces,
        "overlap": {"pair": pair, **overlap(first, second, box_of(named[pair[0]]), box_of(named[pair[1]]), config["overlapStepM"])},
    }

    folder = landxml / landxml_reader.folder_name(pair[1])
    report["seam"] = {"of": pair[1], "against": pair[0], "toleranceM": connect["snapM"],
                      **seam(np.load(folder / "points.npy"), np.load(folder / "faces.npy"), first, connect["snapM"], c3d_connect.snap_z)}
    common.write_json(root / "out" / "surfaces" / "surfaces.json", report)
    print("LandXML %d surfaces, drawing %s AECC_TIN_SURFACE" % (len(surfaces), in_drawing))
    for entry in surfaces:
        print("  %-34s points %7d triangles %7d border edges %6d" % (entry["name"], entry["points"], entry["triangles"], entry["borderEdges"]))
    print("overlap", json.dumps(common.round_tree(report["overlap"])))
    print("seam", json.dumps(common.round_tree(report["seam"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
