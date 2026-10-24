"""plots.csv of the data book, one row per baseplan plot inside the Phase 1 border with every source joined (R166).

A plot is inside when the centroid of its baseplan face is. The official number is the label plus the shift of R28 where a
neighbourhood schedule holds the plot. Blocks and texts come from the data book tables of the full Civil 3D read where a
drawing has them, else from the earlier CadReader outputs named in dataBook.join.fallbackOutputs, and the column source
says which. Line geometry (curb, ROW, setback, cutsheet frames) comes from the CadReader pieces, since the data book entity
table holds the ends of each line only. Levels are the LandXML surfaces of the earlier rounds and every surface the data
book exported, read at the two front corners, the road level snapped as in R157.
"""
import argparse
import csv
import fnmatch
import json
import math
import shutil
import sys
import tempfile
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import LineString, Point, Polygon
from shapely.strtree import STRtree

import c3d_connect
import c3d_run
import cad_analysis
import cad_links
import entry_levels
import full_run
import landxml_reader
import levels_proof
import link_summary
import project_profile
import prototype_offsets
import surface_lookup
import wiring
import yard_rule

SHARED_M = 0.5

BASE_COLUMNS = [
    "officialNumber", "officialNumberMethod", "neighbourhood", "baseplanLabel", "clientType", "centroidX", "centroidY", "faceAreaM2", "cutsheet", "cutsheetMethod",
    "frontLengthM", "widthM", "depthM", "frontBearingDeg", "facingDeg", "unitRev6", "unitRev6Source", "scheduleFile", "scheduleRow",
    "footprintBlock", "footprintBlockMethod", "footprintRotationDeg", "footprintMirrored", "footprintShared", "footprintSource", "cluster", "clusterRow",
    "r06Block", "r06BlockMethod", "r06_0000Block", "r06_0000BlockMethod", "masterBlocksSource", "r06Number", "r06NumberMethod", "r06_0000Number", "r06_0000NumberMethod",
    "v9PlotNumber", "v9PlotNumberMethod", "v9Source",
    "setbackFromFrontM", "setbackFromFrontMMethod", "rowDistanceM", "rowDistanceMMethod", "curbDistanceM", "curbDistanceMMethod", "sidewalkWidthM",
    "roadLevelLeft", "roadLevelRight", "roadLevelSurface", "roadLevelMethod",
    "pastFfl", "pastEntry", "pastSteps", "pastSource",
    "inCutList", "duplicateLabel", "inManyLabelFace", "crossesBorder", "noFront", "twoNeighbourhoods",
]


def border_polygon(path):
    points = []
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            points.append((float(row["x"]), float(row["y"])))
    return Polygon(points)


def folder_of(book, role, fallback_roots):
    """The data book table folder of a drawing when the full read wrote it, and the first earlier CadReader output folder."""
    name = Path(book["drawings"][role]).name
    data = Path(book["root"]) / Path(name).stem
    data = data if (data / "data.json").is_file() else None
    earlier = next((Path(root) / name for root in fallback_roots if (Path(root) / name / "pieces.json").is_file()), None)
    return data, earlier


def reader_of(data):
    """The reader that wrote a data book table folder, C3DProbe for the full Civil 3D read or CadReader for the stand-in."""
    return "data book " + str(json.loads((data / "data.json").read_text(encoding="utf-8")).get("reader") or "C3DProbe")


def read_csv(path):
    with open(path, encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def blocks_of(data, earlier):
    """Blocks with name, x, y, rotation and mirror, and where they came from."""
    if data is not None and (data / "blocks.csv").is_file():
        rows = read_csv(data / "blocks.csv")
        # The outline centre places a unit block on its plot, the insert point of a unit block sits on a corner
        return [{"name": r["name"], "x": float(r.get("cx") or r["x"]), "y": float(r.get("cy") or r["y"]), "rotation": float(r["rotationDeg"]) if r["rotationDeg"] else None,
                 "mirrored": r["mirrored"] == "yes"} for r in rows], reader_of(data)
    if earlier is not None and (earlier / "blocks.json").is_file():
        rows = json.loads((earlier / "blocks.json").read_text(encoding="utf-8"))["blocks"]
        found = []
        for r in rows:
            outline = r.get("outline") or []
            centre = Polygon([p[:2] for p in outline]).centroid if len(outline) >= 3 else None
            x, y = (centre.x, centre.y) if centre is not None and not centre.is_empty else r["insertPoint"][:2]
            found.append({"name": r["name"].split("$")[-1], "x": x, "y": y, "rotation": r.get("rotationDeg"), "mirrored": r.get("mirrored")})
        return found, "earlier CadReader"
    return [], None


def texts_of(data, earlier):
    if data is not None and (data / "texts.csv").is_file():
        return [{"text": r["text"], "x": float(r["x"]), "y": float(r["y"]), "layer": r["layer"]} for r in read_csv(data / "texts.csv")], reader_of(data)
    if earlier is not None and (earlier / "texts.json").is_file():
        return json.loads((earlier / "texts.json").read_text(encoding="utf-8"))["texts"], "earlier CadReader"
    return [], None


def lines_of(earlier, layers):
    if earlier is None:
        return []
    return cad_analysis.read_road_edges(earlier, layers)


def lines_matching(earlier, pattern):
    """Lines on the layers that match a pattern, closed polylines closed."""
    if earlier is None:
        return []
    found = []
    for piece in json.loads((earlier / "pieces.json").read_text(encoding="utf-8"))["pieces"]:
        if piece.get("kind") not in ("polyline", "line") or not fnmatch.fnmatchcase((piece.get("layer") or "").upper(), pattern.upper()):
            continue
        points = [tuple(p[:2]) for p in piece.get("pts") or []]
        if piece.get("closed") and len(points) > 2:
            points.append(points[0])
        if len(points) > 1:
            found.append(LineString(points))
    return found


def in_face(items, face, tree):
    """The items whose point lies in the face, by an STRtree of points."""
    if tree is None:
        return []
    return [items[int(k)] for k in tree.query(face, predicate="contains")]


def bearing(vector):
    """Clockwise from grid north, in degrees."""
    return math.degrees(math.atan2(vector[0], vector[1])) % 360.0


def depth_of(face, frame):
    n = frame["n"]
    values = [(x - frame["left"][0]) * n[0] + (y - frame["left"][1]) * n[1] for x, y in face.exterior.coords]
    return max(values) - min(values)


def load_surfaces(connect, book, scratch):
    """The LandXML surfaces of the earlier rounds and every surface the data book exported, by a column name."""
    surfaces = {name: surface_lookup.load_surface(connect["landxmlDir"], name) for name in connect["surfaces"]}
    folder = Path(book["root"]) / "landxml"
    for path in sorted(folder.glob("*.xml")) if folder.is_dir() else []:
        out = Path(scratch) / path.stem
        summary = landxml_reader.read_landxml(path, out)
        for entry in summary["surfaces"]:
            surfaces[path.stem] = surface_lookup.Surface.from_dir(out / entry["folder"])
    return surfaces


def schedule_rows(settings, connect):
    root = Path(levels_proof.input_root(settings)) / full_run.XLSX_FOLDER
    found = {}
    for name, entry in connect["neighbourhoods"].items():
        if not entry:
            continue
        for row in levels_proof.read_schedule(root / entry["schedule"]):
            number = levels_proof.number_of(row["plot"])
            if number is not None:
                found[(name, str(number))] = dict(row, file=entry["schedule"], shift=entry["shift"])
    return found


def build(config, settings, scratch):
    book, connect, join = config["dataBook"], config["connect"], config["dataBook"]["join"]
    roots = join["fallbackOutputs"]
    border = border_polygon(Path(book["root"]) / "phase1_border.csv")
    plots = link_summary.load(connect["baseplanPlots"])["plots"]
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    _, road_folder = folder_of(book, join["curb"]["drawing"], roots)
    curbs = c3d_connect.LineSet(lines_of(road_folder, join["curb"]["layers"]))
    rows_of_way = c3d_connect.LineSet(lines_of(road_folder, join["row"]["layers"]))
    _, foot_folder = folder_of(book, join["footprint"]["drawing"], roots)
    setbacks = lines_of(foot_folder, [join["footprint"]["setbackLayer"]])
    setback_tree = STRtree(setbacks) if setbacks else None
    foot_data, _ = folder_of(book, join["footprint"]["drawing"], roots)
    foot_blocks, foot_source = blocks_of(foot_data, foot_folder)
    _, cut_folder = folder_of(book, join["cutsheet"]["drawing"], roots)
    frames = [Polygon(line.coords) for line in lines_matching(cut_folder, join["cutsheet"]["frameLayer"]) if len(line.coords) > 3]
    cut_texts = [t for t in (texts_of(None, cut_folder)[0]) if t.get("layer") == join["cutsheet"]["numberLayer"]]
    v9_data, v9_folder = folder_of(book, join["v9"]["drawing"], roots)
    v9_texts, v9_source = texts_of(v9_data, v9_folder)
    v9_texts = [t for t in v9_texts if fnmatch.fnmatchcase(t.get("layer") or "", join["v9"]["plotNumberLayer"])]
    master, master_numbers = {}, {}
    for role in join["masterBlocks"]:
        data, earlier = folder_of(book, role, roots)
        master[role] = blocks_of(data, earlier)
        master_numbers[role] = [t for t in texts_of(data, earlier)[0] if (t.get("text") or "").strip().isdigit()]
    trees = {}

    def tree_of(key, items):
        if key not in trees:
            trees[key] = STRtree([Point(i["x"], i["y"]) for i in items]) if items else None
        return trees[key]

    surfaces = load_surfaces(connect, book, scratch)
    road_order = [name for name in book["roadSurfaceOrder"] if name in surfaces]
    schedules = schedule_rows(settings, connect)
    shifts = {name: entry["shift"] for name, entry in connect["neighbourhoods"].items() if entry}
    plan = json.loads(Path(connect["plan"]).read_text(encoding="utf-8"))
    past = {p["plot"]: p.get("levels") or {} for p in plan["plots"]}
    apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
    analysis = json.loads((Path(roots[1]) / "analysis" / "analysis.json").read_text(encoding="utf-8"))
    cluster_of = {}
    for index, cluster in enumerate(analysis["clusters"]):
        for plot in cluster["plots"]:
            cluster_of[str(plot)] = (cluster["code"] + "-" + str(index), cluster.get("rowId"))
    labels = Counter(str(yard_rule.whole_label(e.get("plot"))) for e in plots if e.get("kind") == "plot_shape" and yard_rule.whole_label(e.get("plot")) is not None)
    many = {str(label) for e in plots if e.get("reason") == "many_labels" for label in e.get("labels") or []}
    faces, _ = yard_rule.face_shapes(plots, 0)
    types = {}
    for entry in plots:
        if entry.get("kind") == "plot_shape" and yard_rule.whole_label(entry.get("plot")) is not None:
            types[str(yard_rule.whole_label(entry.get("plot")))] = entry.get("type")
    chosen = book["wiring"]["chosen"]
    all_faces = wiring.Faces(faces)
    supported = {"footprintBlocks": ("centreWidened", "everyShare"), "r06Blocks": ("centreInside", "everyShare"), "r06_0000Blocks": ("centreInside",),
                 "setback": ("intersects", "bestShare"), "curb": ("frontFrame", "anyEdge"), "row": ("frontFrame", "anyEdge"),
                 "schedules": ("labelShift",), "v9Numbers": ("pointInside",), "r06Numbers": ("pointInside",), "cutsheet": ("centroidIn",), "roadSurfaces": ("snapOrder",)}
    for source, methods in supported.items():
        if chosen.get(source) not in methods:
            raise ValueError("data_book.py cannot wire %s with %s, it knows %s" % (source, chosen.get(source), ", ".join(methods)))
    shared_blocks = {}

    def share_wired(source, folder):
        """The blocks of every face over minShare, largest share first, and the faces of each block (R177)."""
        shapes = wiring.unit_shapes(json.loads((Path(folder) / "blocks.json").read_text(encoding="utf-8"))["blocks"], None)
        per_block = wiring.every_share(all_faces, [b["shape"] for b in shapes], book["wiring"]["minShare"])
        found = defaultdict(list)
        for item, hits in enumerate(per_block):
            for hit in hits:
                found[hit].append(item)
        for hit, items in found.items():
            items.sort(key=lambda item: -faces[hit].intersection(shapes[item]["shape"]).area)
        shared_blocks[source] = (shapes, found, per_block)

    if chosen["footprintBlocks"] == "everyShare":
        share_wired("footprintBlocks", folder_of(book, join["footprint"]["drawing"], roots)[1])
    if chosen["r06Blocks"] == "everyShare":
        share_wired("r06Blocks", book["wiring"].get("r06Outlines") or folder_of(book, "masterR06", roots)[1])
    owned_setbacks = {}
    if chosen["setback"] == "bestShare":
        polygons = wiring.setback_shapes(json.loads((foot_folder / "pieces.json").read_text(encoding="utf-8"))["pieces"], join["footprint"]["setbackLayer"])
        for item, owners in enumerate(wiring.best_share(all_faces, polygons, book["wiring"]["ownerShareMin"], 0)):
            for owner in owners:
                owned_setbacks.setdefault(owner, []).append(polygons[item])
    surface_columns = []
    for name in surfaces:
        surface_columns += ["level_" + name + "_left", "level_" + name + "_right"]
    rows, dropped = [], 0
    for label, face in sorted(faces.items(), key=lambda item: int(item[0])):
        centre = face.centroid
        if not border.contains(centre):
            dropped += 1
            continue
        homes = [name for name, shift in shifts.items() if (name, str(int(label) + shift)) in schedules]
        home = homes[0] if len(homes) == 1 else None
        official = str(int(label) + shifts[home]) if home else None
        schedule = schedules.get((home, official)) if home else None
        row = {key: None for key in BASE_COLUMNS + surface_columns}
        row.update({"officialNumber": official, "neighbourhood": home, "baseplanLabel": label, "clientType": types.get(label),
                    "centroidX": centre.x, "centroidY": centre.y, "faceAreaM2": face.area,
                    "duplicateLabel": labels[label] > 1, "inManyLabelFace": label in many, "crossesBorder": not border.contains(face),
                    "twoNeighbourhoods": len(homes) > 1, "inCutList": official in apart if official else False})
        sheets = [t["text"] for frame in frames if frame.contains(centre) for t in cut_texts if frame.contains(Point(t["x"], t["y"]))]
        row["cutsheet"] = "|".join(sorted(set(sheets))) or None
        if schedule:
            row.update({"unitRev6": schedule["unit"], "unitRev6Source": "schedule " + home, "scheduleFile": schedule["file"], "scheduleRow": schedule["sheetRow"],
                        "pastFfl": schedule["ffl"], "pastEntry": schedule["entry"], "pastSteps": schedule["steps"], "pastSource": "schedule " + home})
        if official and official in past and home == plan.get("neighbourhood"):
            levels = past[official]
            row.update({"pastFfl": (levels.get("ffl") or {}).get("value"), "pastEntry": (levels.get("entry") or {}).get("value"),
                        "pastSteps": (levels.get("steps") or {}).get("value"), "pastSource": "past job plan"})
        if chosen["footprintBlocks"] == "everyShare":
            shapes, found, per_block = shared_blocks["footprintBlocks"]
            on = [shapes[item] for item in found.get(label, [])]
            if on:
                first = found[label][0]
                row.update({"footprintBlock": "|".join(b["name"] for b in on), "footprintRotationDeg": on[0]["rotation"], "footprintMirrored": on[0]["mirrored"],
                            "footprintShared": len(per_block[first]) > 1})
            row["footprintSource"] = "earlier CadReader outlines"
        else:
            on = in_face(foot_blocks, face, tree_of("foot", foot_blocks))
            shared = False
            if not on:
                # A block of a pair or a row of townhouses has its centre on a shared wall, so it is looked for within SHARED_M
                on = in_face(foot_blocks, face.buffer(SHARED_M), tree_of("foot", foot_blocks))
                shared = bool(on)
            if on:
                row.update({"footprintBlock": "|".join(b["name"] for b in on), "footprintRotationDeg": on[0]["rotation"], "footprintMirrored": on[0]["mirrored"],
                            "footprintShared": shared})
            row["footprintSource"] = foot_source
        row["footprintBlockMethod"] = chosen["footprintBlocks"]
        for role, column, source_name in zip(join["masterBlocks"], ("r06Block", "r06_0000Block"), ("r06Blocks", "r06_0000Blocks")):
            if chosen[source_name] == "everyShare":
                shapes, found, _ = shared_blocks[source_name]
                row[column] = "|".join(sorted({shapes[item]["name"] for item in found.get(label, [])})) or None
            else:
                items, source = master[role]
                hits = in_face(items, face, tree_of(role, items))
                row[column] = "|".join(sorted({b["name"] for b in hits})) or None
            row[column + "Method"] = chosen[source_name]
        row["masterBlocksSource"] = " ".join("%s:%s" % (role, master[role][1]) for role in join["masterBlocks"])
        for role, column in zip(join["masterBlocks"], ("r06Number", "r06_0000Number")):
            items = master_numbers[role]
            row[column] = "|".join(sorted({t["text"].strip() for t in in_face(items, face, tree_of(role + "#", items))})) or None
        row["r06NumberMethod"] = row["r06_0000NumberMethod"] = chosen["r06Numbers"]
        numbers = [t["text"].strip() for t in in_face(v9_texts, face, tree_of("v9", v9_texts))]
        row["v9PlotNumber"] = "|".join(numbers) or None
        row["v9PlotNumberMethod"] = chosen["v9Numbers"]
        row["officialNumberMethod"] = chosen["schedules"] if official else None
        row["cutsheetMethod"] = chosen["cutsheet"]
        if "anyEdge" in (chosen["curb"], chosen["row"]):
            edge = entry_levels.front_edge(face, curbs.tree, curbs.lines) if curbs.tree is not None else None
            if edge is not None:
                if chosen["curb"] == "anyEdge":
                    row["curbDistanceM"], row["curbDistanceMMethod"] = curbs.nearest(*edge["mid"])[0], "anyEdge"
                if chosen["row"] == "anyEdge":
                    row["rowDistanceM"], row["rowDistanceMMethod"] = rows_of_way.nearest(*edge["mid"])[0], "anyEdge"
        row["v9Source"] = v9_source
        cluster = cluster_of.get(label)
        if cluster:
            row["cluster"], row["clusterRow"] = cluster
        frame = cad_links.front_frame(face, curbs.tree, curbs.lines, limits)
        if frame is None:
            row["noFront"] = True
            sidewalk(row)
            rows.append(row)
            continue
        row["noFront"] = False
        middle = ((frame["left"][0] + frame["right"][0]) / 2, (frame["left"][1] + frame["right"][1]) / 2)
        along = (frame["right"][0] - frame["left"][0], frame["right"][1] - frame["left"][1])
        row.update({"frontLengthM": frame["length"], "widthM": frame["length"], "depthM": depth_of(face, frame),
                    "frontBearingDeg": bearing(along), "facingDeg": bearing((-frame["n"][0], -frame["n"][1]))})
        if chosen["curb"] == "frontFrame":
            row["curbDistanceM"], row["curbDistanceMMethod"] = frame["distance"], "frontFrame"
        if chosen["row"] == "frontFrame":
            row["rowDistanceM"], row["rowDistanceMMethod"] = rows_of_way.nearest(*middle)[0], "frontFrame"
        if chosen["setback"] == "bestShare":
            owned = owned_setbacks.get(label)
            if owned:
                row["setbackFromFrontM"] = min(polygon.exterior.distance(Point(middle)) for polygon in owned)
                row["setbackFromFrontMMethod"] = "bestShare"
        elif setback_tree is not None:
            near = [setbacks[int(k)] for k in setback_tree.query(face, predicate="intersects")]
            if near:
                row["setbackFromFrontM"] = min(line.distance(Point(middle)) for line in near)
                row["setbackFromFrontMMethod"] = "intersects"
        for side, corner in (("Left", frame["left"]), ("Right", frame["right"])):
            for name in road_order:
                level = c3d_connect.snap_z(surfaces[name], corner[0], corner[1], connect["snapM"])
                if level is not None:
                    row["roadLevel" + side] = level
                    row["roadLevelSurface"] = name
                    row["roadLevelMethod"] = chosen["roadSurfaces"]
                    break
        sidewalk(row)
        for name, surface in surfaces.items():
            row["level_" + name + "_left"] = surface.z_at(*frame["left"])
            row["level_" + name + "_right"] = surface.z_at(*frame["right"])
        rows.append(row)
    return rows, dropped, BASE_COLUMNS + surface_columns


def cell(value):
    if value is None:
        return ""
    if isinstance(value, bool):
        return "yes" if value else "no"
    if isinstance(value, float):
        return repr(round(value, 4))
    return str(value)


def sidewalk(row):
    """Curb distance less ROW distance where the ROW lies nearer, both by the method of the row."""
    if row["rowDistanceM"] is not None and row["curbDistanceM"] is not None and row["rowDistanceM"] < row["curbDistanceM"]:
        row["sidewalkWidthM"] = row["curbDistanceM"] - row["rowDistanceM"]


def write_plots(path, rows, columns):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(columns)
        for row in rows:
            writer.writerow([cell(row[column]) for column in columns])


def split_large(folder, max_mb):
    """A table over max_mb is split by layer into <table>/<layer>.csv, and a layer still over it into numbered parts (R164).
    The whole table is removed once split. Returns the tables split."""
    split = []
    for path in sorted(Path(folder).glob("*.csv")):
        if path.stat().st_size <= max_mb * (1 << 20):
            continue
        rows = read_csv(path)
        if not rows or "layer" not in rows[0]:
            continue
        columns = list(rows[0])
        by_layer = defaultdict(list)
        for row in rows:
            by_layer[row["layer"]].append(row)
        target = path.with_suffix("")
        target.mkdir(exist_ok=True)
        for layer, members in by_layer.items():
            name = "".join(ch if ch.isalnum() or ch in "-_ ." else "_" for ch in layer) or "_"
            chunk = max(1, int(len(members) * max_mb * (1 << 20) / max(path.stat().st_size * len(members) / len(rows), 1)))
            for index in range(0, len(members), chunk):
                part = target / (name + ("" if len(members) <= chunk else "_%03d" % (index // chunk + 1)) + ".csv")
                with open(part, "w", encoding="utf-8", newline="") as handle:
                    writer = csv.DictWriter(handle, fieldnames=columns, lineterminator="\n")
                    writer.writeheader()
                    writer.writerows(members[index:index + chunk])
        path.unlink()
        split.append(path.name)
    return split


def main(argv=None):
    parser = argparse.ArgumentParser(description="plots.csv of the data book, every source joined per plot (R166)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    scratch = tempfile.mkdtemp(prefix="data_book_")
    try:
        rows, dropped, columns = build(config, settings, scratch)
    finally:
        shutil.rmtree(scratch, ignore_errors=True)
    root = Path(config["dataBook"]["root"])
    write_plots(root / "plots.csv", rows, columns)
    for role, relative in config["dataBook"]["drawings"].items():
        done = split_large(root / Path(relative).stem, config["dataBook"]["maxFileMB"])
        if done:
            print(role, "split by layer:", ", ".join(done))
    by = Counter(row["neighbourhood"] or "rest" for row in rows)
    print("plots.csv kept %d, dropped as outside %d, by neighbourhood %s, columns %d" % (len(rows), dropped, dict(by), len(columns)))
    (root / "logs").mkdir(parents=True, exist_ok=True)
    (root / "logs" / "plots.json").write_text(json.dumps({"kept": len(rows), "droppedOutside": dropped, "byNeighbourhood": dict(by), "columns": columns}, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
