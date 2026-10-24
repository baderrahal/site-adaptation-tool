"""What each drawing of the Roads and Base Plans folders holds, from the ACadSharp output of its working copy (R154).

CadReader runs on each copy with an empty layer list, so no layer is dropped. This survey reads its output and gives per
drawing the units, the xrefs, the layers with their counts, the block names, the texts that look like a level, the 3D lines
with a real Z and the layers that may hold a curb, an edge of road, a back of sidewalk, a right of way, a building line or a
setback. The Civil 3D objects ACadSharp cannot see come from the LISP inventory of R153.
"""
import argparse
import fnmatch
import json
import math
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_run
import common

NUMBER = re.compile(r"(?<![\d.])[+]?(\d{2,3}\.\d+)(?![\d.])")
WORD = re.compile(r"[A-Za-z]+")
EXAMPLES = 5


def matches(name, patterns):
    upper = name.upper()
    return any(fnmatch.fnmatchcase(upper, pattern.upper()) for pattern in patterns)


def is_level_text(text, survey):
    """A text that names a level word, or holds a number inside levelRange with at least levelDecimals decimals."""
    if not text:
        return False
    words = {word.upper() for word in WORD.findall(text)}
    if words & {word.upper() for word in survey["levelWords"]}:
        return True
    low, high = survey["levelRange"]
    for found in NUMBER.findall(text):
        decimals = len(found.split(".")[1])
        if decimals >= survey["levelDecimals"] and low <= float(found) <= high:
            return True
    return False


def piece_points(piece):
    return piece.get("pts") or piece.get("vertices") or []


def piece_length(piece):
    kind = piece.get("kind")
    if kind == "arc" and piece.get("radius"):
        centre, start, end = piece["center"], piece["start"], piece["end"]
        a0 = math.atan2(start[1] - centre[1], start[0] - centre[0])
        a1 = math.atan2(end[1] - centre[1], end[0] - centre[0])
        sweep = (a1 - a0) if piece.get("ccw", True) else (a0 - a1)
        return piece["radius"] * (sweep % (2 * math.pi))
    if kind in ("line", "polyline"):
        points = piece_points(piece)
        total = sum(math.dist(a[:2], b[:2]) for a, b in zip(points, points[1:]))
        if piece.get("closed") and len(points) > 2:
            total += math.dist(points[-1][:2], points[0][:2])
        return total
    return 0.0


def real_z(piece):
    """The Z range of a line or polyline that has a vertex off zero, else None."""
    if piece.get("kind") not in ("line", "polyline"):
        return None
    zs = [point[2] for point in piece_points(piece) if len(point) > 2]
    if not zs or all(abs(z) < 1e-9 for z in zs):
        return None
    return min(zs), max(zs)


def inside(point, box):
    return box[0] <= point[0] <= box[2] and box[1] <= point[1] <= box[3]


def overlap_share(box, reference):
    """The share of the reference box that the box covers, 0 when they miss."""
    width = min(box[2], reference[2]) - max(box[0], reference[0])
    height = min(box[3], reference[3]) - max(box[1], reference[1])
    area = (reference[2] - reference[0]) * (reference[3] - reference[1])
    return max(width, 0) * max(height, 0) / area if area > 0 else 0.0


def survey_drawing(folder, survey, reference_box=None):
    folder = Path(folder)
    layers = json.loads((folder / "layers.json").read_text(encoding="utf-8"))
    texts = json.loads((folder / "texts.json").read_text(encoding="utf-8")).get("texts", [])
    pieces = json.loads((folder / "pieces.json").read_text(encoding="utf-8")).get("pieces", [])
    blocks = json.loads((folder / "blocks.json").read_text(encoding="utf-8")).get("blocks", [])
    used = {entry["name"]: {"entities": entry["entities"], "byType": entry["byType"]} for entry in layers["layers"] if entry["entities"]}
    level_layers, examples = Counter(), []
    for text in texts:
        if is_level_text(text.get("text"), survey):
            level_layers[text.get("layer")] += 1
            if len(examples) < EXAMPLES:
                examples.append({"layer": text.get("layer"), "text": text.get("text")})
    three_d, z_by_layer = Counter(), {}
    role_pieces = {role: Counter() for role in survey["lineRoles"]}
    role_length = {role: defaultdict(float) for role in survey["lineRoles"]}
    box = (layers.get("extents") or {}).get("core")
    in_box = Counter()
    for piece in pieces:
        layer = piece.get("layer") or ""
        z = real_z(piece)
        if z is not None:
            three_d[layer] += 1
            known = z_by_layer.get(layer)
            z_by_layer[layer] = [min(z[0], known[0]), max(z[1], known[1])] if known else list(z)
        for role, patterns in survey["lineRoles"].items():
            if piece.get("kind") in ("line", "polyline", "arc") and matches(layer, patterns):
                role_pieces[role][layer] += 1
                role_length[role][layer] += piece_length(piece)
                if reference_box:
                    points = piece_points(piece) or [piece.get("start") or [0, 0]]
                    in_box[role] += inside(points[0], reference_box)
    roles = {}
    for role in survey["lineRoles"]:
        if role_pieces[role]:
            roles[role] = {
                "layers": {layer: {"pieces": count, "lengthM": role_length[role][layer]} for layer, count in role_pieces[role].most_common()},
                "pieces": sum(role_pieces[role].values()),
                "insideBaseplan": in_box[role] if reference_box else None,
            }
    names = Counter(block.get("name") for block in blocks)
    return {
        "units": layers.get("units"),
        "xrefs": [{"name": x["name"], "found": x.get("found")} for x in layers.get("xrefs", [])],
        "core": box,
        "baseplanOverlap": overlap_share(box, reference_box) if box and reference_box else None,
        "layersUsed": len(used),
        "layers": used,
        "blocks": {"inserts": len(blocks), "names": len(names), "top": dict(names.most_common(20)), "mirrored": sum(1 for b in blocks if b.get("mirrored"))},
        "texts": len(texts),
        "levelTexts": {"count": sum(level_layers.values()), "byLayer": dict(level_layers.most_common()), "examples": examples},
        "lines3d": {"count": sum(three_d.values()), "byLayer": dict(three_d.most_common()), "zRange": z_by_layer},
        "roles": roles,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Survey the ACadSharp output of every working copy (R154)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    root = Path(config["probeRoot"])
    cad = root / "out" / "cad"
    survey = config["survey"]
    base = json.loads((cad / Path(config["drawings"]["baseplan"]).name / "layers.json").read_text(encoding="utf-8"))
    reference = base["extents"]["core"]
    inventory_path = root / "out" / "inventory" / "inventory.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8")) if inventory_path.exists() else {}
    report = {}
    for role, relative in config["drawings"].items():
        name = Path(relative).name
        entry = survey_drawing(cad / name, survey, reference)
        entry["drawing"] = relative
        entry["civilObjects"] = inventory.get(role, {}).get("civil")
        report[role] = entry
        print("%-18s units %s, layers %d, level texts %d, 3D lines %d, roles %s, civil %s" % (
            role, entry["units"], entry["layersUsed"], entry["levelTexts"]["count"], entry["lines3d"]["count"],
            {k: v["pieces"] for k, v in entry["roles"].items()}, entry["civilObjects"]))
    common.write_json(root / "out" / "drawings" / "drawings.json", report, wide_keys=frozenset({"zRange"}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
