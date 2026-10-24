import argparse
import json
import sys
from pathlib import Path

from shapely.geometry import LineString, Polygon
from shapely.ops import polygonize, unary_union
from shapely.strtree import STRtree

import link_table
from common import write_json

DEFAULT_CONFIG = Path(__file__).resolve().parent.parent / "config" / "second_source.json"
SEVERAL = 2
INSIDE = 0.9


def clean(polygon):
    return polygon if polygon.is_valid else polygon.buffer(0)


def base_faces(plots, other):
    """Every closed face of the baseplan as (plot label or None, polygon), the faces with one label and the other faces."""
    faces = []
    for shape in plots["plots"]:
        if shape["kind"] == "plot_shape":
            faces.append((shape.get("plot"), clean(Polygon(link_table.ring(shape)))))
    for face in other.get("faces", []):
        faces.append((None, clean(Polygon(link_table.ring(face)))))
    return faces


def second_faces(pieces, layers):
    """Closed faces of the plot layers of the second drawing. Closed polylines are faces as they are and the loose lines are noded and polygonized. Equal faces from different layers count once."""
    wanted = {name.lower() for name in layers}
    per_layer = {name: {"closed": 0, "polygonized": 0} for name in layers}
    names = {name.lower(): name for name in layers}
    found = []
    segments = {name: [] for name in layers}
    for piece in pieces:
        layer = piece["layer"].lower()
        if layer not in wanted:
            continue
        points = [(point[0], point[1]) for point in piece.get("pts", [])]
        if piece["kind"] == "polyline" and piece.get("closed") and len(points) >= 3:
            found.append(clean(Polygon(points)))
            per_layer[names[layer]]["closed"] += 1
        elif piece["kind"] in ("line", "polyline") and len(points) >= 2:
            segments[names[layer]].extend(LineString(pair) for pair in zip(points, points[1:]))
    for name, lines in segments.items():
        if lines:
            made = list(polygonize(unary_union(lines)))
            per_layer[name]["polygonized"] = len(made)
            found.extend(made)
    unique, seen = [], set()
    for polygon in found:
        if polygon.is_empty or polygon.area <= 0:
            continue
        centre = polygon.representative_point()
        key = (round(polygon.area, 1), round(centre.x, 1), round(centre.y, 1))
        if key not in seen:
            seen.add(key)
            unique.append(polygon)
    return unique, per_layer, len(found) - len(unique)


def compare(base, second, min_overlap=0.5, area_percent=1.0):
    """Matches faces one to one by overlap, best overlap first. Counts only, nothing is changed."""
    tree = STRtree([polygon for _, polygon in base])
    pairs = []
    for j, face in enumerate(second):
        for i in tree.query(face, predicate="intersects"):
            shared = base[i][1].intersection(face).area
            union = base[i][1].area + face.area - shared
            if union > 0 and shared / union >= min_overlap:
                pairs.append((shared / union, int(i), j))
    pairs.sort(key=lambda item: -item[0])
    taken_base, taken_second, matched = set(), set(), []
    for overlap, i, j in pairs:
        if i not in taken_base and j not in taken_second:
            taken_base.add(i)
            taken_second.add(j)
            matched.append((i, j))
    differ = []
    for i, j in matched:
        base_area, second_area = base[i][1].area, second[j].area
        percent = abs(second_area - base_area) / base_area * 100
        if percent > area_percent:
            differ.append({"plot": base[i][0], "baseArea": round(base_area, 3), "secondArea": round(second_area, 3), "percent": round(percent, 3)})
    only_base = [i for i in range(len(base)) if i not in taken_base]
    only_second = [j for j in range(len(second)) if j not in taken_second]
    base_polygons = [polygon for _, polygon in base]
    holds_several = []
    for j in only_second:
        inside = [i for i in tree.query(second[j], predicate="intersects") if base_polygons[i].intersection(second[j]).area >= INSIDE * base_polygons[i].area]
        if len(inside) >= SEVERAL:
            holds_several.append(j)
    counts = {
        "baseFaces": len(base),
        "secondFaces": len(second),
        "matched": len(matched),
        "onlyBase": len(only_base),
        "onlySecond": len(only_second),
        "areaDiffOver": len(differ),
        "areaPercent": area_percent,
        "minOverlap": min_overlap,
        "onlySecondHoldsSeveral": len(holds_several),
    }
    details = {
        "onlyBase": [{"plot": base[i][0], "area": round(base[i][1].area, 3)} for i in only_base],
        "onlySecondAreas": [round(second[j].area, 3) for j in only_second],
        "areaDiff": differ,
    }
    return counts, details


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the plot faces of the baseplan with the plot faces of a second drawing by location and area.")
    parser.add_argument("out_dir")
    parser.add_argument("--cad", required=True, help="folder of the baseplan with plots.json and other_faces.json")
    parser.add_argument("--second", required=True, help="folder of the second drawing with pieces.json")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG))
    args = parser.parse_args(argv)
    config = json.loads(Path(args.config).read_text(encoding="utf-8"))
    load = link_table.load
    plots = load(Path(args.cad) / "plots.json")
    other = load(Path(args.cad) / "other_faces.json")
    second = load(Path(args.second) / "pieces.json")
    faces, per_layer, repeated = second_faces(second["pieces"], config["layers"])
    counts, details = compare(base_faces(plots, other), faces, config["minOverlap"], config["areaPercent"])
    counts["secondLayers"] = per_layer
    counts["secondRepeated"] = repeated
    # The name comes from the file the reader read, so the report never names a drawing it did not use
    counts["secondFile"] = second.get("source")
    write_json(Path(args.out_dir) / "second_source.json", {"counts": counts, "details": details})
    print("matched " + str(counts["matched"]) + ", only baseplan " + str(counts["onlyBase"]) + ", only second " + str(counts["onlySecond"]) + ", area differs " + str(counts["areaDiffOver"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
