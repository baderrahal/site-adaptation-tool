"""Ties the road drawings to the plots of the past job (R156 to R158).

R156 takes the front edge of each plot face from the curb lines (R122) and measures from its middle to the curb, to every
other line set of c3d_probe.json and to the edge of every LandXML surface, which gives the sidewalk width and whether a
surface reaches the plot line. R157 reads a level at the two front corners of each past ramp from every source, each surface
where the corner is, each surface at the nearest curb point, the nearest level text and the nearest 3D line, and bands it
against the ramp corner and against El @ Entry. R158 counts the plot fronts each source covers per neighbourhood and lists
the footprint blocks that sit on the plots of the cut check. Lengths and levels are in metres.
"""
import argparse
import json
import math
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
from shapely.geometry import LineString, Point, Polygon
from shapely.ops import unary_union
from shapely.strtree import STRtree

import c3d_drawings
import c3d_run
import cad_analysis
import cad_links
import common
import full_run
import levels_proof
import link_summary
import plan_from_past
import project_profile
import ramp_levels
import surface_lookup
import yard_rule

EPSILON = 1e-9
FRONT_CORNERS = 2


def band_names(bands):
    return ["within%dmm" % round(limit * 1000) for limit in bands] + ["worse"]


def band_of(difference, bands):
    for name, limit in zip(band_names(bands), bands):
        if abs(difference) <= limit + EPSILON:
            return name
    return "worse"


class LineSet:
    """Lines with a tree, for the nearest distance and the nearest point."""

    def __init__(self, lines):
        self.lines = [line for line in lines if line.length > 0]
        self.tree = STRtree(self.lines) if self.lines else None

    def nearest(self, x, y):
        if self.tree is None:
            return None, None
        point = Point(x, y)
        line = self.lines[int(self.tree.query_nearest(point)[0])]
        foot = line.interpolate(line.project(point))
        return point.distance(line), (foot.x, foot.y)


class PointLevels:
    """Level texts as points with a value. The nearest one within the radius gives the level."""

    def __init__(self, rows):
        self.xy = np.array([(row[0], row[1]) for row in rows], dtype=float).reshape(-1, 2)
        self.z = np.array([row[2] for row in rows], dtype=float)

    def z_at(self, x, y, radius):
        if len(self.z) == 0:
            return None
        distance = np.hypot(self.xy[:, 0] - x, self.xy[:, 1] - y)
        best = int(distance.argmin())
        return float(self.z[best]) if distance[best] <= radius else None


class Segments3d:
    """3D line segments. The nearest one within the radius gives the level at the foot of the point on it."""

    def __init__(self, segments):
        array = np.array(segments, dtype=float).reshape(-1, 6)
        self.a, self.b = array[:, 0:3], array[:, 3:6]

    def __len__(self):
        return len(self.a)

    def z_at(self, x, y, radius):
        if len(self.a) == 0:
            return None
        d = self.b[:, :2] - self.a[:, :2]
        length = (d * d).sum(axis=1)
        t = np.where(length > 0, ((x - self.a[:, 0]) * d[:, 0] + (y - self.a[:, 1]) * d[:, 1]) / np.where(length > 0, length, 1), 0)
        t = np.clip(t, 0, 1)
        fx, fy = self.a[:, 0] + t * d[:, 0], self.a[:, 1] + t * d[:, 1]
        distance = np.hypot(fx - x, fy - y)
        best = int(distance.argmin())
        if distance[best] > radius:
            return None
        return float(self.a[best, 2] + t[best] * (self.b[best, 2] - self.a[best, 2]))


def snap_z(surface, x, y, tolerance):
    """The level where the point is, or at the nearest point of the surface edge within tolerance, else None (R157).

    A plot corner that lies on the line a surface stops at falls outside its last triangle by a fraction of a millimetre,
    so the level of the nearest triangle side stands in for it.
    """
    z = surface.z_at(x, y)
    if z is not None or surface.count == 0:
        return z
    px, py = x - surface.origin[0], y - surface.origin[1]
    i0, i1 = int(np.floor((px - tolerance) / surface.cell)), int(np.floor((px + tolerance) / surface.cell))
    j0, j1 = max(int(np.floor((py - tolerance) / surface.cell)), 0), min(int(np.floor((py + tolerance) / surface.cell)), surface.ny - 1)
    parts = []
    for i in range(max(i0, 0), min(i1, surface.nx - 1) + 1):
        lo = np.searchsorted(surface.keys, i * surface.ny + j0, "left")
        hi = np.searchsorted(surface.keys, i * surface.ny + j1, "right")
        parts.append(surface.tris[lo:hi])
    if not parts or j0 > j1:
        return None
    near = np.unique(np.concatenate(parts))
    if len(near) == 0:
        return None
    best = None
    for a, b in ((0, 1), (1, 2), (2, 0)):
        ax, ay, az = surface.x[near, a], surface.y[near, a], surface.z[near, a]
        bx, by, bz = surface.x[near, b], surface.y[near, b], surface.z[near, b]
        dx, dy = bx - ax, by - ay
        length = dx * dx + dy * dy
        t = np.clip(np.where(length > 0, ((px - ax) * dx + (py - ay) * dy) / np.where(length > 0, length, 1), 0), 0, 1)
        distance = np.hypot(px - (ax + t * dx), py - (ay + t * dy))
        k = int(distance.argmin())
        if best is None or distance[k] < best[0]:
            best = (float(distance[k]), float(az[k] + t[k] * (bz[k] - az[k])))
    return best[1] if best[0] <= tolerance else None


def text_value(text, survey):
    """The first number of a level text that falls inside levelRange with levelDecimals, or None."""
    low, high = survey["levelRange"]
    for found in c3d_drawings.NUMBER.findall(text or ""):
        if len(found.split(".")[1]) >= survey["levelDecimals"] and low <= float(found) <= high:
            return float(found)
    return None


def read_levels(cad_root, drawings, survey, box):
    """Level texts with a value and 3D segments with a real Z, from every drawing, inside the box."""
    texts, segments, by_drawing = [], [], Counter()
    for role, relative in drawings.items():
        folder = Path(cad_root) / Path(relative).name
        for text in json.loads((folder / "texts.json").read_text(encoding="utf-8")).get("texts", []):
            value = text_value(text.get("text"), survey) if c3d_drawings.is_level_text(text.get("text"), survey) else None
            if value is not None and c3d_drawings.inside((text["x"], text["y"]), box):
                texts.append((text["x"], text["y"], value))
                by_drawing[role + ":text"] += 1
        for piece in json.loads((folder / "pieces.json").read_text(encoding="utf-8")).get("pieces", []):
            if c3d_drawings.real_z(piece) is None:
                continue
            points = c3d_drawings.piece_points(piece)
            for first, second in zip(points, points[1:]):
                if len(first) > 2 and len(second) > 2 and c3d_drawings.inside(first, box):
                    segments.append((*first[:3], *second[:3]))
                    by_drawing[role + ":line3d"] += 1
    return PointLevels(texts), Segments3d(segments), dict(by_drawing)


def line_set(cad_root, drawings, entry):
    return LineSet(cad_analysis.read_road_edges(Path(cad_root) / Path(drawings[entry["drawing"]]).name, entry["layers"]))


def front_corners(ramp, frame):
    """The two corners of a ramp nearest to the plot front edge, ordered as the left and the right seen from the road."""
    edge = (frame["left"], frame["right"])
    ranked = sorted(range(len(ramp["corners"])), key=lambda index: Point(ramp["corners"][index]).distance(_segment(edge)))
    chosen = ranked[:FRONT_CORNERS]
    r = frame["r"]
    chosen.sort(key=lambda index: ramp["corners"][index][0] * r[0] + ramp["corners"][index][1] * r[1])
    return [(ramp["corners"][index], ramp["levels"][index]) for index in chosen]


def _segment(edge):
    return LineString(edge)


def make_sources(surfaces, curbs, texts, segments, settings, ground=frozenset()):
    """Every level source as a name and a function of x and y that gives a level or None."""
    sources = {}
    for name, surface in surfaces.items():
        sources["surface:" + name] = surface.z_at
        sources["snap:" + name] = lambda x, y, surface=surface: snap_z(surface, x, y, settings["snapM"])

        def at_curb(x, y, surface=surface):
            _, foot = curbs.nearest(x, y)
            return surface.z_at(*foot) if foot else None

        sources["curbPoint:" + name] = at_curb
    designed = [sources["snap:" + name] for name in surfaces if name not in ground]

    def any_design(x, y):
        for source in designed:
            z = source(x, y)
            if z is not None:
                return z
        return None

    # Only for the coverage of a zone, where surfaces overlap the first in config order answers
    sources["snap:anyDesign"] = any_design
    sources["levelText"] = lambda x, y: texts.z_at(x, y, settings["textRadiusM"])
    sources["line3d"] = lambda x, y: segments.z_at(x, y, settings["lineRadiusM"])
    return sources


def median(values):
    return statistics.median(values) if values else None


def level_table(rows, names, bands):
    """Per source: corners compared with the past ramp corner and the mean of the two front corners against El @ Entry."""
    table = {}
    for name in names:
        corner = [diff for row in rows for diff in row["corners"].get(name, []) if diff is not None]
        entry = [row["entry"][name] for row in rows if row["entry"].get(name) is not None]
        table[name] = {
            "corners": len(corner),
            "cornerBands": dict(Counter(band_of(d, bands) for d in corner)),
            "cornerMedianAbs": median([abs(d) for d in corner]),
            "plotsWithEntry": len(entry),
            "entryBands": dict(Counter(band_of(d, bands) for d in entry)),
            "entryMedian": median(entry),
        }
    return table


def ranking(table, bands):
    first, second = band_names(bands)[0], band_names(bands)[1]
    order = sorted(table, key=lambda name: (-table[name]["cornerBands"].get(first, 0), -table[name]["cornerBands"].get(second, 0), -table[name]["corners"]))
    return [{"source": name, first: table[name]["cornerBands"].get(first, 0), second: table[name]["cornerBands"].get(second, 0), "corners": table[name]["corners"]} for name in order]


def measure_plots(plan_rows, faces, curbs, line_sets, surfaces, ramps_by_plot, sources, limits, settings, ground=frozenset()):
    bands = settings["bandsM"]
    rows, no_front = [], []
    for plan_row in plan_rows:
        plot = plan_row["plot"]
        face = faces.get(plot)
        frame = cad_links.front_frame(face, curbs.tree, curbs.lines, limits) if face is not None else None
        if frame is None:
            no_front.append(plot)
            continue
        mid = ((frame["left"][0] + frame["right"][0]) / 2, (frame["left"][1] + frame["right"][1]) / 2)
        row = {"plot": plot, "unit": plan_row["unit"], "curb": frame["distance"], "lines": {}, "surfaceEdge": {}, "frontCovered": {}}
        for name, lines in line_sets.items():
            row["lines"][name] = lines.nearest(*mid)[0]
        for name, surface in surfaces.items():
            row["surfaceEdge"][name] = 0.0 if surface.z_at(*mid) is not None else surface.distance_to(mid[0], mid[1], settings["searchM"])
            row["frontCovered"][name] = sum(surface.z_at(*corner) is not None for corner in (frame["left"], frame["right"]))
        row["corners"], row["entry"], row["rampCovered"] = {}, {}, None
        ramps = ramps_by_plot.get(plot) or []
        if ramps:
            ramp = min(ramps, key=lambda item: Point(item["corners"][0]).distance(_segment((frame["left"], frame["right"]))))
            corners = front_corners(ramp, frame)
            row["rampFront"] = [[corner[0], corner[1], level] for corner, level in corners]
            # The ground surface covers every plot, so as in round 13 a ramp counts as covered only by a design or road surface
            designed = [surface for name, surface in surfaces.items() if name not in ground]
            row["rampCovered"] = sum(any(surface.z_at(*corner) is not None for surface in designed) for corner in ramp["corners"])
            entry = plan_row["entry"]
            for name, source in sources.items():
                values = [source(*corner) for corner, _ in corners]
                row["corners"][name] = [None if value is None else value - level for value, (_, level) in zip(values, corners)]
                if entry is not None and all(value is not None for value in values):
                    row["entry"][name] = sum(values) / len(values) - entry
        rows.append(row)
    return rows, no_front


def reach_summary(rows, line_sets, surfaces, back_of_sidewalk):
    curb = [row["curb"] for row in rows]
    summary = {"plots": len(rows), "curbMedian": median(curb), "lines": {}, "surfaces": {}}
    for name in line_sets:
        values = [row["lines"][name] for row in rows if row["lines"][name] is not None]
        summary["lines"][name] = {"median": median(values), "within0p5m": sum(1 for v in values if v <= 0.5)}
    widths = [row["curb"] - row["lines"][back_of_sidewalk] for row in rows if row["lines"].get(back_of_sidewalk) is not None and row["lines"][back_of_sidewalk] < row["curb"]]
    summary["sidewalk"] = {"from": back_of_sidewalk, "plots": len(widths), "medianWidth": median(widths)}
    for name in surfaces:
        edges = [row["surfaceEdge"][name] for row in rows]
        found = [value for value in edges if value is not None]
        # Where the middle of the front is not covered, how far the surface edge lies against the curb
        apart = [(row["surfaceEdge"][name], row["curb"]) for row in rows if row["surfaceEdge"][name]]
        summary["surfaces"][name] = {
            "bothFrontCornersCovered": sum(1 for row in rows if row["frontCovered"][name] == 2),
            "midpointCovered": sum(1 for value in edges if value == 0),
            "edgeFound": len(found),
            "notCoveredEdgeMedian": median([edge for edge, _ in apart]),
            "edgeWithin0p5mOfCurb": sum(1 for edge, curb in apart if abs(edge - curb) <= 0.5),
            "edgeInsideSidewalk": sum(1 for edge, curb in apart if edge < curb - 0.5),
            "edgeBeyondCurb": sum(1 for edge, curb in apart if edge > curb + 0.5),
        }
    return summary


def cut_check(plots, cut_labels, blocks):
    """The footprint blocks whose outline centre lies on the faces of the cut labels, by label."""
    faces, _ = yard_rule.face_shapes(plots, 0)
    found = {label: faces.get(label) for label in cut_labels}
    region = unary_union([face for face in found.values() if face is not None])
    hits = []
    for block in blocks:
        outline = block.get("outline") or []
        if len(outline) < 3:
            continue
        shape = Polygon([(p[0], p[1]) for p in outline]).buffer(0)
        if shape.is_empty or not region.buffer(0.5).contains(shape.centroid):
            continue
        on = [label for label, face in found.items() if face is not None and face.contains(shape.centroid)]
        hits.append({"block": block.get("name"), "mirrored": block.get("mirrored"), "areaM2": shape.area, "onLabels": on,
                     "shareOfRegion": shape.intersection(region).area / region.area if region.area else None})
    return {"labels": {label: {"type": cut_labels[label], "found": face is not None, "areaM2": face.area if face is not None else None} for label, face in found.items()}, "blocks": hits}


def coverage(groups, faces_by_shift, curbs, sources, limits):
    """The share of plot fronts whose two front corners each source gives a level for, per group of plots."""
    result = {}
    for group, (shift, plots) in groups.items():
        faces = faces_by_shift[shift]
        counts, fronts = Counter(), 0
        for plot in plots:
            face = faces.get(plot)
            frame = cad_links.front_frame(face, curbs.tree, curbs.lines, limits) if face is not None else None
            if frame is None:
                counts["noFront"] += 1
                continue
            fronts += 1
            for name, source in sources.items():
                if all(source(*corner) is not None for corner in (frame["left"], frame["right"])):
                    counts[name] += 1
        result[group] = {"plots": len(plots), "fronts": fronts, "noFront": counts.pop("noFront", 0),
                         "share": {name: (counts[name] / fronts if fronts else None) for name in sources}}
    return result


def load_neighbourhood_plots(settings, connect):
    """The official plot numbers of each neighbourhood schedule on this machine, None where there is none."""
    root = levels_proof.input_root(settings)
    found = {}
    for name, entry in connect["neighbourhoods"].items():
        if not entry:
            found[name] = None
            continue
        rows = levels_proof.read_schedule(Path(root) / full_run.XLSX_FOLDER / entry["schedule"])
        found[name] = (entry["shift"], sorted({str(levels_proof.number_of(row["plot"])) for row in rows if levels_proof.number_of(row["plot"]) is not None}))
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="Tie the road drawings to the plots of the past job (R156 to R158)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    connect = config["connect"]
    root = Path(config["probeRoot"])
    cad_root = root / "out" / "cad"
    settings = project_profile.load_settings(args.settings)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    plots = link_summary.load(connect["baseplanPlots"])["plots"]
    box = json.loads((cad_root / Path(config["drawings"]["baseplan"]).name / "layers.json").read_text(encoding="utf-8"))["extents"]["core"]
    curbs = line_set(cad_root, config["drawings"], connect["curb"])
    line_sets = {name: line_set(cad_root, config["drawings"], entry) for name, entry in connect["lines"].items()}
    surfaces = {name: surface_lookup.load_surface(connect["landxmlDir"], name) for name in connect["surfaces"]}
    texts, segments, level_counts = read_levels(cad_root, config["drawings"], config["survey"], box)
    plan = json.loads(Path(connect["plan"]).read_text(encoding="utf-8"))
    plan_rows = [{"plot": row["plot"], "unit": (row.get("unit") or {}).get("value"),
                  "entry": ((row.get("levels") or {}).get("entry") or {}).get("value")} for row in plan["plots"]]
    imc_shift = connect["neighbourhoods"]["IMC"]["shift"]
    faces, _ = yard_rule.face_shapes(plots, imc_shift)
    document = ramp_levels.host_document(link_summary.load(connect["prototypes"]))
    ramps, left_out = ramp_levels.ramp_rows(document, set(connect["rampTypes"]))
    ramps_by_plot = defaultdict(list)
    for ramp in ramps:
        ramps_by_plot[plan_from_past.plot_number(ramp["plot"])].append(ramp)
    roles = json.loads(Path(project_profile.ROOT / "config" / "road_surfaces.json").read_text(encoding="utf-8"))["surfaces"]
    ground = frozenset(name for name, entry in roles.items() if entry.get("role") == "ground")
    sources = make_sources(surfaces, curbs, texts, segments, connect, ground)
    rows, no_front = measure_plots(plan_rows, faces, curbs, line_sets, surfaces, ramps_by_plot, sources, limits, connect, ground)
    bands = connect["bandsM"]
    table = level_table(rows, list(sources), bands)
    uncovered = [row for row in rows if row["rampCovered"] == 0]
    uncovered_sources = {name: sum(1 for row in uncovered if any(d is not None for d in row["corners"].get(name, []))) for name in sources}
    neighbourhoods = load_neighbourhood_plots(settings, connect)
    used_labels = set()
    groups = {}
    for name, found in neighbourhoods.items():
        if found is None:
            continue
        shift, numbers = found
        groups[name] = (shift, numbers)
        used_labels |= {str(int(number) - shift) for number in numbers}
    unit_types = set(connect["unitPlotTypes"])
    others = sorted({str(yard_rule.whole_label(entry.get("plot"))) for entry in plots if entry.get("kind") == "plot_shape" and entry.get("type") in unit_types
                     and yard_rule.whole_label(entry.get("plot")) is not None} - used_labels)
    groups["otherUnitPlots"] = (0, others)
    faces_by_shift = {shift: yard_rule.face_shapes(plots, shift)[0] for shift, _ in groups.values()}
    cover = coverage(groups, faces_by_shift, curbs, sources, limits)
    # R300. The cut is checked against the footprint blocks, never those of a drawing kept for reference only
    blocks = project_profile.read_villa_blocks(cad_root / Path(config["drawings"][connect["cutDrawing"]]).name / "blocks.json", settings)["blocks"]
    cut = cut_check(plots, connect["cutLabels"], blocks)
    cut["pastPlan"] = {row["plot"]: row["unit"] for row in plan_rows if row["plot"] in connect["cutPlots"]}
    report = {
        "plots": len(plan_rows), "measured": len(rows), "noFront": no_front,
        "ramps": len(ramps), "rampsLeftOut": dict(left_out), "levelSources": level_counts,
        "reach": reach_summary(rows, line_sets, surfaces, connect["backOfSidewalk"]),
        "levels": table, "ranking": ranking(table, bands),
        "uncoveredRamps": {"count": len(uncovered), "ground": sorted(ground), "coveredBy": uncovered_sources},
        "coverage": cover, "neighbourhoodsWithoutSchedule": [name for name, found in neighbourhoods.items() if found is None],
        "cut": cut, "rows": rows,
    }
    out = root / "out" / "connect"
    common.write_json(out / "connect.json", report)
    print("plots %d measured %d no front %d ramps %d" % (len(plan_rows), len(rows), len(no_front), len(ramps)))
    print("reach", json.dumps(common.round_tree(report["reach"]))[:1500])
    for item in report["ranking"][:8]:
        print("rank", item, "entry", common.round_tree({k: table[item["source"]][k] for k in ("plotsWithEntry", "entryBands", "entryMedian", "cornerMedianAbs")}))
    print("uncovered", report["uncoveredRamps"])
    print("coverage", json.dumps(common.round_tree(cover)))
    print("cut", json.dumps(common.round_tree(cut))[:1500])
    return 0


if __name__ == "__main__":
    sys.exit(main())
