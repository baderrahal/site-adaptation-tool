"""A third count of the road drawings from a DXF that Civil 3D wrote of the copy, Part C of round DATA-5 (R253, R254).

The DXF is read as plain group code and value pairs, with no CAD library, so the count shares nothing with CadReader
(ACadSharp) or the probe (the Civil 3D API). Every entity of model space is counted by type, and every insert is expanded
through its block, nested blocks too, with the insert point, scale, rotation and a flipped extrusion applied, so each leaf
entity gets its place on the site. An entity is inside Phase 1 when any of its points is, a text or insert by its insert
point, the rule of R162 without the cut into parts. Civil 3D objects (AECC types), proxies (ACAD_PROXY_ENTITY) and the
attributes of inserts are counted apart. The rows of both readers are counted by type in the same way: one row per
entity, the parts of a cut line taken once, and LWPOLYLINE and POLYLINE as one type, since CadReader writes both as
POLYLINE. Counts go to counts.json in the counts folder of data5, no coordinate is written.
"""
import argparse
import csv
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import shapely

import c3d_run
import data_book

TEXT_TYPES = {"TEXT", "MTEXT", "ATTRIB"}
SUB_TYPES = {"ATTRIB", "VERTEX", "SEQEND"}
OCS_TYPES = {"LWPOLYLINE", "ARC", "CIRCLE", "INSERT", "TEXT", "MTEXT", "HATCH", "SOLID", "TRACE", "ATTRIB", "POLYLINE", "ELLIPSE"}
SAME_TYPE = {"LWPOLYLINE": "POLYLINE"}


def pairs(path):
    """Group code and value pairs of an ASCII DXF, values stripped."""
    with open(path, encoding="utf-8", errors="replace") as handle:
        while True:
            code = handle.readline()
            if not code:
                return
            value = handle.readline()
            yield int(code), value.strip()


def new_entity(kind):
    return {"type": kind, "layer": "", "paper": False, "points": [], "name": None, "scale": [1.0, 1.0], "rotation": 0.0,
            "flip": False, "columns": 1, "rows": 1, "attribs": 0, "text": "", "attributes": []}


def parse(path):
    """CLASSES, BLOCKS and model space ENTITIES of a DXF. A block is its base point and its entities, an entity its type,
    layer, paper space flag, points and for an insert the block name, scale, rotation and array counts."""
    classes, blocks, entities = {}, {}, []
    section, current, block, target, parent, last_x = None, None, None, None, None, None
    klass, head = None, False
    for code, value in pairs(path):
        if code == 0:
            head = False
            if value == "SECTION":
                section, current = None, None
                continue
            if value == "ENDSEC":
                section, current, target = None, None, None
                continue
            if value == "EOF":
                break
            if section == "CLASSES":
                if value == "CLASS":
                    klass = {}
                    classes[len(classes)] = klass
                continue
            if section == "BLOCKS":
                if value == "BLOCK":
                    block = {"name": None, "base": (0.0, 0.0), "flags": 0, "entities": []}
                    current, target, head = None, None, True
                    continue
                if value == "ENDBLK":
                    if block and block["name"]:
                        blocks[block["name"]] = block
                    block, current, target = None, None, None
                    continue
                target = block["entities"] if block else None
            elif section == "ENTITIES":
                target = entities
            else:
                current = None
                continue
            if value in SUB_TYPES and parent is not None:
                current = parent if value == "VERTEX" else None
                if value == "ATTRIB" and parent["type"] == "INSERT":
                    parent["attribs"] += 1
                    current = {"type": "ATTRIB", "tag": "", "text": ""}
                    parent["attributes"].append(current)
                if value == "SEQEND":
                    parent = None
                continue
            current = new_entity(value)
            parent = current if value in ("INSERT", "POLYLINE") else None
            if target is not None:
                target.append(current)
            continue
        if code == 2 and section is None and current is None and block is None:
            section = value
            continue
        if section == "CLASSES" and klass is not None:
            key = {1: "dxfName", 2: "cppName", 90: "proxyFlags", 91: "instances", 280: "wasProxy", 281: "isEntity"}.get(code)
            if key:
                klass[key] = int(value) if code in (90, 91, 280, 281) else value
            continue
        if head:
            # Only the BLOCK record itself names the block, an attribute skipped inside it carries a tag in code 2 too
            if code == 2:
                block["name"] = value
            elif code == 70:
                block["flags"] = int(value)
            elif code == 10:
                block["base"] = (float(value), block["base"][1])
            elif code == 20:
                block["base"] = (block["base"][0], float(value))
            continue
        if current is None:
            continue
        if current["type"] == "ATTRIB" and "tag" in current:
            if code == 1:
                current["text"] = value
            elif code == 2:
                current["tag"] = value
            continue
        if code in (1, 3) and current["type"] in TEXT_TYPES | {"ATTDEF"}:
            current["text"] += value
            continue
        if code == 8 and not current["layer"]:
            current["layer"] = value
        elif code == 67:
            current["paper"] = value == "1"
        elif code == 2 and current["type"] == "INSERT":
            current["name"] = value
        elif code in (10, 11) and current["type"] != "DIMENSION" or code == 10:
            last_x = float(value)
        elif code in (20, 21) and current["type"] != "DIMENSION" or code == 20:
            if last_x is not None:
                current["points"].append((last_x, float(value)))
                last_x = None
        elif code == 41 and current["type"] == "INSERT":
            current["scale"][0] = float(value)
        elif code == 42 and current["type"] == "INSERT":
            current["scale"][1] = float(value)
        elif code == 50 and current["type"] == "INSERT":
            current["rotation"] = float(value)
        elif code == 70 and current["type"] == "INSERT":
            current["columns"] = max(1, int(value))
        elif code == 71 and current["type"] == "INSERT":
            current["rows"] = max(1, int(value))
        elif code == 230 and current["type"] in OCS_TYPES:
            current["flip"] = float(value) < 0
    return {"classes": list(classes.values()), "blocks": blocks, "entities": entities}


def own_points(entity):
    """The points of an entity in its block frame, the x of a flipped extrusion turned as the arbitrary axis rule does."""
    points = entity["points"][:1] if entity["type"] in ("INSERT", "TEXT", "MTEXT") else entity["points"]
    return [(-x, y) for x, y in points] if entity["flip"] else list(points)


def placed(points, insert, base):
    """Points of a block entity on the site through one insert."""
    if len(points) == 0:
        return np.zeros((0, 2))
    at = own_points(insert)[0] if insert["points"] else (0.0, 0.0)
    turn = math.radians(insert["rotation"]) * (-1 if insert["flip"] else 1)
    local = (np.asarray(points, dtype=float) - np.asarray(base)) * np.asarray(insert["scale"])
    if insert["flip"]:
        # The extrusion 0,0,-1 mirrors the whole insert in x, the block points as well as the insert point
        local = local * np.array([-1.0, 1.0])
    cos, sin = math.cos(turn), math.sin(turn)
    return np.column_stack([local[:, 0] * cos - local[:, 1] * sin + at[0], local[:, 0] * sin + local[:, 1] * cos + at[1]])


def leaves(name, blocks, memo, depth=0):
    """Every leaf entity of a block in the frame of that block: type, nested depth and points. Inserts of an array are
    expanded once, the array counts kept as a weight."""
    if name in memo:
        return memo[name]
    memo[name] = []
    block = blocks.get(name)
    if block is None or depth > 32:
        return memo[name]
    found = []
    for entity in block["entities"]:
        if entity["type"] == "INSERT":
            child = blocks.get(entity["name"])
            found.append(("INSERT", 1, np.asarray(own_points(entity) or [(0.0, 0.0)], dtype=float), 1, entity["attribs"]))
            if child is None:
                continue
            for kind, nested, points, weight, attribs in leaves(entity["name"], blocks, memo, depth + 1):
                found.append((kind, nested + 1, placed(points, entity, child["base"]), weight * entity["columns"] * entity["rows"], attribs))
        else:
            found.append((entity["type"], 0, np.asarray(own_points(entity), dtype=float).reshape(-1, 2), 1, 0))
    memo[name] = found
    return found


def inside_any(points, border):
    return bool(len(points)) and bool(shapely.contains_xy(border, points[:, 0], points[:, 1]).any())


def count(parsed, border):
    """Model space by type, all and inside, and the exploded leaves inside by type and depth, with inserts and attributes."""
    blocks, memo = parsed["blocks"], {}
    model = [e for e in parsed["entities"] if not e["paper"]]
    top_all, top_inside = Counter(), Counter()
    exploded, nested, inserts, attributes = Counter(), Counter(), Counter(), Counter()
    xrefs, missing = Counter(), Counter()
    for entity in model:
        kind = entity["type"]
        points = np.asarray(own_points(entity), dtype=float).reshape(-1, 2)
        top_all[kind] += 1
        inside = inside_any(points, border)
        if inside:
            top_inside[kind] += 1
        if kind != "INSERT":
            if inside:
                exploded[kind] += 1
            continue
        if inside:
            inserts["top"] += 1
            attributes["top"] += entity["attribs"]
        block = blocks.get(entity["name"])
        if block is None:
            missing[entity["name"]] += 1
            continue
        if block["flags"] & 4:
            xrefs[entity["name"]] += 1
            continue
        for leaf, depth, local, weight, attribs in leaves(entity["name"], blocks, memo):
            points = placed(local, entity, block["base"])
            if not inside_any(points, border):
                continue
            times = weight * entity["columns"] * entity["rows"]
            if leaf == "INSERT":
                inserts["nested"] += times
                attributes["nested"] += attribs * times
                continue
            exploded[leaf] += times
            nested[leaf] += times
    return {"modelSpace": dict(top_all), "modelSpaceInside": dict(top_inside), "explodedInside": dict(exploded),
            "nestedInside": dict(nested), "insertsInside": dict(inserts), "attributesInside": dict(attributes),
            "xrefInserts": dict(xrefs), "missingBlocks": dict(missing),
            "paperSpace": dict(Counter(e["type"] for e in parsed["entities"] if e["paper"])),
            "civilTypes": {k: v for k, v in top_all.items() if k.startswith("AECC")}, "proxies": top_all.get("ACAD_PROXY_ENTITY", 0),
            "classes": [c for c in parsed["classes"] if c.get("instances")]}


def table_rows(folder, name):
    path = Path(folder) / (name + ".csv")
    files = [path] if path.is_file() else sorted((Path(folder) / name).glob("*.csv"))
    rows = []
    for file in files:
        with open(file, encoding="utf-8") as handle:
            rows.extend(csv.DictReader(handle))
    return rows


def reader_counts(folder):
    """Rows of a data book table folder by type, one per entity: a cut line counts once, its parts apart."""
    csv.field_size_limit(1 << 30)
    entities = table_rows(folder, "entities")
    parts = sum(1 for r in entities if r.get("part") not in (None, "", "0"))
    by_type = Counter(SAME_TYPE.get(r["type"], r["type"]) for r in entities if r.get("part") in (None, "", "0"))
    blocks = table_rows(folder, "blocks")
    texts = table_rows(folder, "texts")
    return {"entityRows": len(entities), "partRows": parts, "entities": dict(by_type), "blocks": len(blocks),
            "nestedBlocks": sum(1 for b in blocks if b.get("parentBlock")), "texts": dict(Counter(t["type"] for t in texts)),
            "textRows": len(texts)}


def top_of(path):
    return ([p for p in (path or "").split("/") if p] or [""])[0]


def gap_by_insert(probe_folder, cad_folder, kinds):
    """Rows of the kinds the probe holds over CadReader, by the block of the top insert they come from. An insert whose
    point lies outside Phase 1 is in no blocks table and is named by its handle."""
    def per(folder):
        found = Counter()
        for row in table_rows(folder, "entities"):
            if row.get("part") in (None, "", "0") and SAME_TYPE.get(row["type"], row["type"]) in kinds:
                found[top_of(row.get("path"))] += 1
        return found
    probe, cad = per(probe_folder), per(cad_folder)
    names = {b["handle"]: b["name"] for b in table_rows(probe_folder, "blocks") if not b.get("parentBlock")}
    gaps, inserts = Counter(), Counter()
    for handle in set(probe) | set(cad):
        if probe[handle] != cad[handle]:
            key = "model space" if not handle else names.get(handle, "insert outside Phase 1 " + handle)
            gaps[key] += probe[handle] - cad[handle]
            inserts[key] += 1
    return [{"block": k, "rows": v, "inserts": inserts[k]} for k, v in gaps.most_common()]


def compare(dxf, probe, cad):
    """Per type the DXF exploded count against each reader, the differences only."""
    third = Counter()
    for kind, n in dxf["explodedInside"].items():
        third[SAME_TYPE.get(kind, kind)] += n
    kinds = sorted(set(third) | set(probe["entities"]) | set(cad["entities"]))
    rows = []
    for kind in kinds:
        row = {"type": kind, "dxf": third.get(kind, 0), "probe": probe["entities"].get(kind, 0), "cadReader": cad["entities"].get(kind, 0)}
        if len({row["dxf"], row["probe"], row["cadReader"]}) > 1:
            rows.append(row)
    return rows


def run(config):
    book = config["dataBook"]
    settings = book["data5"]["counts"]
    root = Path(book["root"])
    out = root / book["data5"]["outFolder"] / settings["outFolder"]
    border = data_book.border_polygon(root / "phase1_border.csv")
    shapely.prepare(border)
    report = {}
    for role in settings["roles"]:
        stem = Path(book["drawings"][role]).stem
        dxf_path = root / book["data5"]["outFolder"] / settings["dxfFolder"] / (stem + ".dxf")
        if not dxf_path.is_file():
            report[role] = {"error": "no DXF of the copy"}
            continue
        dxf = count(parse(dxf_path), border)
        probe = reader_counts(root / stem)
        cad = reader_counts(out / "cadreader" / stem)
        report[role] = {"dxf": dxf, "probe": probe, "cadReader": cad, "byType": compare(dxf, probe, cad),
                        "cadReaderGapByInsert": gap_by_insert(root / stem, out / "cadreader" / stem, set(settings["geometryTypes"]))}
    out.mkdir(parents=True, exist_ok=True)
    (out / "counts.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="A third count of the road drawings from a DXF of the copy (R253, R254)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    for role, found in report.items():
        print("==", role)
        if "error" in found:
            print(found["error"])
            continue
        for key in ("modelSpace", "insertsInside", "attributesInside", "xrefInserts", "missingBlocks", "civilTypes", "proxies"):
            print(key, json.dumps(found["dxf"][key]))
        print("probe", json.dumps({k: found["probe"][k] for k in ("entityRows", "partRows", "blocks", "nestedBlocks", "texts")}))
        print("cadReader", json.dumps({k: found["cadReader"][k] for k in ("entityRows", "partRows", "blocks", "nestedBlocks", "texts")}))
        for row in found["byType"]:
            print("  ", json.dumps(row))
        print("cadReader gap by insert", json.dumps(found["cadReaderGapByInsert"][:8]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
