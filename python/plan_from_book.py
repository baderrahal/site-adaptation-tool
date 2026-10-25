"""R208 to R210. plan.json of R80 for the plots of one neighbourhood and one unit code, from CAD and the data book only.

No value comes from a past model. Per plot:
- plot: the official number of the location plan (R182), unit: its Typology_Rev9 code (R183), both from the snapshot plots.csv
- prototypeFile: the file of the unit in the upgraded prototype folder
- insert, rotationDeg, mirrored: the front frame of its baseplan face (R122), the mirror by the drawn side of the unit in its
  cluster (R123) and the fitted origin offset of plotFrame (R121). A unit with no drawn side, such as a villa, gets none and is listed
- levels: by the level profile of R277 and R278, the one named with --level-profile or else levelProfiles.default of the rules
  profile, parsons2024Rev6. Round Z4 built with the estimate of R209 instead, the snapped road level at the two front corners
  plus the slope of the unit times the ramp depth (R170 by typology), FFL, SSL and steps from levels.py on it, marked estimate
- floors: the Generic floor is the whole baseplan face at the FFL, the yard the fitted outline of R135 at the FFL less
  floors.yardBelowFflM, the ramp the Rev 6 outline of R127 with its front corners on the snapped road and its back corners
  parallel to them with their mean at El @ Entry, of the ramp type of its slope band (R210)
The survey point, the angle and the level elevations are those the newSite job read back from the new file (R212).
With the Rev 9 sales sheet the front of a plot with two or more road edges is the edge of the orientation rule (R226), and
such a plot with no Orientation gets no front. The plots are those of cover.json, or every unit plot of a neighbourhood.
With the footprint blocks the clusters are the plots that share one Building Footprint block, a duplex pair or a townhouse
row, and a plot no such block joins to another keeps its cluster of the CAD analysis, less the plots a block took (R236).
"""
import argparse
import csv
import json
import sys
from collections import Counter
from pathlib import Path

from shapely.strtree import STRtree

import c3d_connect
import cad_analysis
import cad_links
import corner_front
import common
import data_book_retest
import floor_fit
import footprint_check
import landxml_reader
import levels
import link_summary
import plan_from_cad
import plan_from_past
import plan_rules
import plot_relations
import project_profile
import prototype_offsets
import override_source
import ramp_outline
import surface_lookup
import yard_rule

ESTIMATE = "estimate"
LOCATION = "locationPlan"
sourced = plan_from_cad.sourced


def book_rows(plots_csv):
    """The rows of plots.csv by location plan number, and the number of each baseplan label."""
    with open(plots_csv, encoding="utf-8") as handle:
        rows = [row for row in csv.DictReader(handle) if row.get("locationPlanNumber")]
    return {row["locationPlanNumber"]: row for row in rows}, {row["baseplanLabel"]: row["locationPlanNumber"] for row in rows if row.get("baseplanLabel")}


def footprint_clusters(numbers, rows, faces, block_index):
    """R236. The plots of the numbers that share one Building Footprint block of the name the data book wired to them, the block
    that shares the most of each face, two or more to a cluster, in the shape of the clusters of the CAD analysis."""
    groups = {}
    for number in numbers:
        row, face = rows.get(number), faces.get(number)
        if row is None or face is None or not row.get("footprintBlock"):
            continue
        best, held = None, 0.0
        for index, shape in enumerate(block_index.get(row["footprintBlock"], [])):
            if shape.intersects(face):
                share = shape.intersection(face).area
                if share > held:
                    best, held = (row["footprintBlock"], index), share
        if best is not None:
            groups.setdefault(best, []).append(number)
    return [{"code": name, "plots": sorted(plots, key=link_summary.sort_key)} for (name, _), plots in sorted(groups.items()) if len(plots) > 1]


def joined_clusters(footprint, analysis):
    """R236. The footprint clusters, then each analysis cluster less the plots a footprint cluster holds, when two or more stay."""
    taken = {plot for cluster in footprint for plot in cluster["plots"]}
    rest = [{**cluster, "plots": [plot for plot in cluster["plots"] if plot not in taken]} for cluster in analysis]
    return footprint + [cluster for cluster in rest if len(cluster["plots"]) > 1]


def numbered_clusters(clusters, number_of):
    """The CAD clusters with their plots as location plan numbers. A plot with no number leaves the cluster."""
    found = []
    for cluster in clusters:
        plots = [number_of[label] for label in cluster["plots"] if label in number_of]
        if plots:
            found.append({**cluster, "plots": plots})
    return found


def load_roads(files, scratch):
    """The road surfaces of the snapshot in the order given, each read by the LandXML reader into scratch."""
    surfaces = []
    for path in files:
        path = Path(path)
        out = Path(scratch) / path.stem
        summary = landxml_reader.read_landxml(path, out)
        entry = summary["surfaces"][0]
        surfaces.append((path.stem.rpartition("__")[2] or path.stem, surface_lookup.Surface.from_dir(out / entry["folder"])))
    return surfaces


def road_level(surfaces, point, snap):
    """R209. The level of the first surface that covers the point, snapped to its edge within snap (R157), and its name."""
    for name, surface in surfaces:
        z = c3d_connect.snap_z(surface, point[0], point[1], snap)
        if z is not None:
            return z, name
    return None, None


def entry_estimate(front, unit, profile, steep, depth):
    """R209. El @ Entry: the mean road level of the front corners plus the slope of the unit along the ramp depth, and the slope."""
    slope = data_book_retest.slopes(profile, steep, unit)[data_book_retest.STEEP]
    return (front[0] + front[1]) / 2.0 + slope / 100.0 * depth, slope


def face_loops(face):
    """The whole baseplan face as plan loops, the outer ring and any hole, the first point not repeated."""
    rings = [face.exterior] + list(face.interiors)
    return [[plan_from_past.point(point[:2]) for point in list(ring.coords)[:-1]] for ring in rings]


def generic_floor(builder, face, inputs, z):
    """R208. The Generic floor is the whole baseplan face at the FFL (Bader, DATA-2 7)."""
    level = inputs["floors"]["levels"]["generic"]
    elevation = inputs["levels"].get(level)
    if elevation is None:
        builder.lack("floors.generic", "the new site file has no level " + level)
        return None
    return {"type": sourced(inputs["floors"]["types"]["generic"][0]), "level": sourced(level), "levelElevation": sourced(common.r3(elevation)),
            "outline": sourced({"loops": [{"points": loop, "arcs": []} for loop in face_loops(face)]}),
            "plotValue": sourced(builder.entry["unit"]["value"] + "-" + builder.plot), "z": sourced(common.r3(z), ESTIMATE),
            "heightOffset": sourced(common.r3(z - elevation), ESTIMATE)}


def ramp_floor(builder, frame, corners, front, entry, slope, inputs, kind=None):
    """R210. The ramp of the Rev 6 outline, the front corners at the road, the back pair parallel with its mean at El @ Entry.
    R278: the ramp type the level profile names, else the type of its slope band."""
    middle = (front[0] + front[1]) / 2.0
    heights = list(front) + [entry + front[1] - middle, entry + front[0] - middle]
    kind = kind or floor_fit.type_of(slope, inputs["bands"])
    level = inputs["floors"]["levels"]["ramp"]
    elevation = inputs["levels"].get(level)
    if kind is None or elevation is None:
        builder.lack("floors.ramp", "no ramp type for a slope of " + str(slope) + " percent" if kind is None else "the new site file has no level " + level)
        return None
    points = [(corner[0], corner[1], z) for corner, z in zip(corners, heights)]
    return {"type": sourced(kind), "level": sourced(level), "levelElevation": sourced(common.r3(elevation)),
            "outline": sourced(plan_from_cad.loops_of([corners])), "plotValue": sourced(builder.entry["unit"]["value"] + "-" + builder.plot),
            "corners": sourced([plan_from_past.point(point) for point in points], ESTIMATE),
            "heightOffset": sourced(common.r3(min(heights) - elevation), ESTIMATE), "slopePercent": sourced(common.r3(slope), ESTIMATE)}


def level_values(builder, row):
    """R209. The levels of levels.py from the estimated El @ Entry, every one marked estimate. The link Z is the FFL."""
    if row is None or row["ffl"] is None:
        builder.lack("levels.ffl", "levels.py gives no FFL" + ("" if row is None else ", " + ", ".join(row["flags"])))
        builder.entry["levels"] = {}
        return
    builder.entry["levels"] = {key: sourced(row[key], ESTIMATE) for key in ("ffl", "ssl", "entry", "height", "steps")}
    if row["flags"]:
        builder.entry["levelFlags"] = list(row["flags"])
    if "insert" in builder.entry:
        builder.entry["insert"]["value"][2] = row["ffl"]


def fronts(numbers, rows, faces, roads, limits, surfaces, snap, profile, corner=None, sales=None):
    """R209, R226. The frame, ramp outline and road level of each plot's front corners, with the surface that gave each level.
    With corner settings and sales rows the frame comes from the corner rule, else from the R122 front."""
    tree = STRtree(roads)
    found = {}
    for number in numbers:
        face = faces.get(number)
        rule = "nearestCurb"
        if face is None:
            frame = None
        elif corner is not None and sales is not None:
            frame, rule = corner_front.corner_frame(face, tree, roads, limits, corner, sales.get(number))
        else:
            frame = cad_links.front_frame(face, tree, roads, limits)
        item = {"face": face, "frame": frame, "corners": None, "front": None, "surfaces": [], "unit": rows.get(number, {}).get("typologyRev9") or None, "frontRule": rule}
        if frame is not None and item["unit"]:
            depth = ramp_outline.setback(profile, item["unit"])
            item["depth"] = depth
            item["corners"] = ramp_outline.outline(frame, depth)
            levels_found = [road_level(surfaces, corner, snap) for corner in item["corners"][:2]]
            item["surfaces"] = [name for _, name in levels_found]
            if all(z is not None for z, _ in levels_found):
                item["front"] = [z for z, _ in levels_found]
        found[number] = item
    return found


def build(numbers, rows, number_of, faces, roads, clusters, profile, settings, frame_section, files, folder, site, surfaces, outlines, nbh, sales=None, numbered=False, rules=None):
    """R208. The plan of R80 for the plots numbers, every value from CAD, the data book and the new site file. R278 to R280: with
    rules the level profile, the hand rules and the overrides of the majority rule give the levels, the ramp type and the hand.
    main always gives rules (R277), rules None is the estimate of R209 that round Z4 built with."""
    block = settings.get("siteBuild")
    floors_config = plan_from_past.load_floors(settings)
    steep = set(block["steepUnits"])
    snap = block["snapM"]
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    mine = [cluster for cluster in (clusters if numbered else numbered_clusters(clusters, number_of)) if set(cluster["plots"]) & set(numbers)]
    members = sorted({plot for cluster in mine for plot in cluster["plots"]} | set(numbers), key=link_summary.sort_key)
    front = fronts(members, rows, faces, roads, limits, surfaces, snap, profile, block.get("corner") if sales is not None else None, sales)
    frames = {number: item["frame"] for number, item in front.items()}
    roles = cad_links.cluster_roles(mine, 0, {number: item["face"] for number, item in front.items() if item["face"] is not None}, frames)
    entries, slopes, applied = {}, {}, Counter()
    for number, item in front.items():
        if item["front"] is not None:
            if rules is None:
                value, slope = entry_estimate(item["front"], item["unit"], profile, steep, item["depth"])
            else:
                value, slope = plan_rules.entry_of(item["front"], item["unit"], rules["levelProfile"], item["depth"])
                gap = plan_rules.gap_of(rules["overrides"].get(number, {}).get("entryM"))
                if gap is not None:
                    value += gap
                    applied["entryM"] += 1
            entries[number] = {"unit": item["unit"], "entry": value}
            slopes[number] = slope
    if rules is None:
        level_rows = plan_from_cad.plot_levels(profile, mine, entries)
    else:
        level_rows = plan_rules.heights(entries, mine, rules["levelProfile"], profile)
        for number, row in level_rows.items():
            gap = plan_rules.gap_of(rules["overrides"].get(number, {}).get("heightM"))
            if gap is not None:
                row["ffl"] = round(row["ffl"] + gap, 3)
                row["ssl"] = round(row["ssl"] + gap, 3)
                row["height"] = round(row["height"] + gap, 4)
                row["steps"] = plan_rules.steps_of(row["height"], rules["levelProfile"])
                applied["heightM"] += 1
    inputs = {"outlines": outlines, "step": floor_fit.width_step(settings), "levels": site["levels"], "floors": floors_config, "bands": outlines["rampTypes"]["bands"]}
    plots, missing, lacking, surface_count, no_level, book_check = [], [], Counter(), Counter(), [], Counter()
    for number in numbers:
        item = front[number]
        builder = plan_from_past.PlotBuilder(number)
        unit = item["unit"]
        if unit is None:
            # R230. A location plan number with no plots.csv row or no Typology_Rev9, such as a label sharing its face, is listed
            builder.lack("unit", "plots.csv holds no row or no Typology_Rev9 for the plot")
            missing.append({"plot": number, "written": False, "missing": builder.missing})
            lacking.update(entry["key"] for entry in builder.missing)
            continue
        builder.entry["unit"] = sourced(unit, LOCATION)
        hand, fit_unit = None, None
        if rules is not None:
            keo = ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file")
            fit_unit = unit if unit in frame_section["units"] else keo
            sides = {code: {"drawnSide": value.get("drawnSide")} for code, value in frame_section["units"].items()}
            kind = ((profile.get("typologies") or {}).get(unit) or {}).get("kind")
            block_mirror = {"yes": "yes", "no": "no"}.get(rows.get(number, {}).get("footprintMirrored"))
            hand, rule = plan_rules.hand_of(unit, kind, roles.get(number), block_mirror, rules["openEnds"].get(number), rules["hands"],
                                            cad_links.mirror_of(fit_unit or unit, roles.get(number), sides), keo)
            override = rules["overrides"].get(number, {}).get("mirrored")
            if isinstance(override, bool):
                hand, rule = override, "override"
                applied["mirrored"] += 1
            applied["hand " + str(rule)] += 1
        plan_from_cad.link_values(builder, unit, item["face"], item["frame"], roles.get(number), frame_section, files, hand, fit_unit)
        level_values(builder, level_rows.get(number))
        steps = builder.entry["levels"].get("steps", {}).get("value")
        builder.entry["parameters"] = {plan_from_past.PLOT_PARAMETER: sourced(number, LOCATION), plan_from_past.UNIT_PARAMETER: sourced(unit, LOCATION)}
        if steps is not None:
            builder.entry["parameters"][plan_from_past.STEPS_PARAMETER] = sourced(str(steps), ESTIMATE)
        surface_count.update(name or "none" for name in item["surfaces"])
        if item["front"] is None:
            no_level.append(number)
        else:
            for z, column in zip(item["front"], ("roadLevelLeft", "roadLevelRight")):
                other = rows.get(number, {}).get(column)
                book_check["within 5 mm" if other and abs(float(other) - z) <= 0.005 else "other or empty"] += 1
        complete = all(builder.has(key) for key in plan_from_past.LINK_KEYS)
        floors = {}
        if complete:
            ffl = builder.entry["levels"]["ffl"]["value"]
            made = generic_floor(builder, item["face"], inputs, ffl)
            if made:
                floors["generic"] = made
            below = floors_config["yardBelowFflM"] if rules is None else rules["levelProfile"].get("yardBelowFflM")
            made = None if below is None else plan_from_cad.flat_floor(builder, "yard", unit, item["frame"], inputs, ffl - below)
            if made:
                made["z"]["source"] = made["heightOffset"]["source"] = ESTIMATE
                floors["yard"] = made
            ramp_front, kind = item["front"], None
            if rules is not None:
                gap = plan_rules.gap_of(rules["overrides"].get(number, {}).get("rampFrontM"))
                if gap is not None:
                    ramp_front = [z + gap for z in item["front"]]
                    applied["rampFrontM"] += 1
                named = rules["overrides"].get(number, {}).get("rampType")
                if named is not None:
                    applied["rampType"] += 1
                kind = rules["levelProfile"]["rampTypes"].get(named or plan_rules.slope_class(slopes[number]))
            made = ramp_floor(builder, item["frame"], item["corners"], ramp_front, builder.entry["levels"]["entry"]["value"], slopes[number], inputs, kind)
            if made:
                floors["ramp"] = made
        builder.entry["floors"] = floors
        builder.entry["roadSurfaces"] = item["surfaces"]
        if complete:
            plots.append(builder.entry)
        if builder.missing:
            missing.append({"plot": number, "written": complete, "missing": builder.missing})
            lacking.update(entry["key"] for entry in builder.missing)
    survey = site["surveyPoint"]
    return {
        "format": plan_from_past.FORMAT,
        "formatVersion": plan_from_past.FORMAT_VERSION,
        "neighbourhood": nbh,
        "coordinateSystem": {"name": "shared", "lengthUnit": "m", "angleUnit": "deg"},
        "surveyPoint": {"position": plan_from_past.point(survey["position"]), "sharedPosition": plan_from_past.point(survey["sharedPosition"])},
        "angleToTrueNorthDeg": common.r3(site["angleToTrueNorthDeg"]),
        "prototypeFolder": str(folder),
        "floorSettings": {"types": floors_config["types"], "levels": floors_config["levels"], "plotParameter": floors_config["plotParameter"]},
        "sources": {"plots": "plots.csv of the data book snapshot", "survey": "the new site file", "pastModel": "none"},
        "summary": {
            "plots": len(numbers), "written": len(plots), "notWritten": len(numbers) - len(plots), "missingByKey": dict(sorted(lacking.items())),
            "clusters": len(mine), "clusterPlots": len(members), "inACluster": sum(1 for number in numbers if number in roles),
            "roadSurfacesOfFrontCorners": dict(surface_count.most_common()), "plotsWithNoRoadLevel": no_level,
            "frontRule": dict(Counter(front[number]["frontRule"] for number in numbers).most_common()),
            "frontLevelAgainstPlotsCsv": dict(book_check),
            "floorsFound": {kind: sum(1 for entry in plots if kind in entry["floors"]) for kind in plan_from_past.FLOOR_KINDS},
            "floorTypes": dict(sorted(Counter(kind + ": " + floor["type"]["value"] for entry in plots for kind, floor in entry["floors"].items()).items())),
            "rules": None if rules is None else {"levelProfile": rules["name"], "overrideSource": rules["sources"], "overrideModel": rules.get("models", []),
                                                "applied": dict(sorted(applied.items()))},
        },
        "plots": plots,
        "missing": missing,
        "conflicts": [],
    }


def nbh_plots(labels_path, nbh):
    """Every unit plot of a neighbourhood on the location plan, by its number, in order."""
    with open(labels_path, encoding="utf-8") as handle:
        rows = [row for row in csv.DictReader(handle) if row.get("nbh") == nbh and row.get("kind") == "clientCode"]
    return sorted({row["number"] for row in rows}, key=link_summary.sort_key)


def site_report(path):
    """The survey point, angle and level elevations the newSite job read back from the new file."""
    report = json.loads(Path(path).read_text(encoding="utf-8"))
    return {"surveyPoint": report["surveyPoint"], "angleToTrueNorthDeg": report["angleToTrueNorthDeg"],
            "levels": {level["name"]: level["elevationM"] for level in report["levels"]}}


def folder_files(folder, profile):
    """The prototype file of each unit with its name as the folder spells it."""
    names = sorted(path.name for path in Path(folder).iterdir() if path.is_file())
    return plan_from_cad.prototype_files(names, profile)[0]


def rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales):
    """R277 to R280. The level profile, named or the default of R277, the hand rules, the open end of every plot and the overrides
    of the source models."""
    import majority
    name = plan_rules.profile_name(profile, args.level_profile)
    lp = plan_rules.level_profile(profile, name)
    block = settings.get("majority")
    corner = settings.get("siteBuild")["corner"]
    front = fronts(numbers, rows, faces, roads, cad_links.front_limits(profile, settings.get("analysis")), [], 0.0, profile, corner, sales)
    frames = {number: item["frame"] for number, item in front.items()}
    units = [number for number, row in rows.items() if row.get("typologyRev9") and number in faces]
    neighbours = majority.side_neighbours(units, faces, frames, block["sideShare"], profile.get("plot.long_side") / 1000.0)
    open_ends = {}
    for number in numbers:
        sides = neighbours.get(number)
        if sides is not None and frames.get(number) is not None:
            open_ends[number] = "both" if not sides["left"] and not sides["right"] else "left" if not sides["left"] else "right" if not sides["right"] else "none"
    fields = tuple(args.override_fields) if args.override_fields else plan_rules.FIELDS
    # R280. An empty list name or a source with no list would build with no override as if none was asked for
    if args.overrides is not None and not args.overrides.strip():
        raise project_profile.ProfileError("--overrides names no file, give the override list in config/overrides (R280)")
    named_sources, models = getattr(args, "override_source", None), getattr(args, "override_model", None) or []
    if (named_sources or models) and args.overrides is None:
        raise project_profile.ProfileError("--override-source and --override-model need --overrides, the override list their overrides come from (R280)")
    overrides, sources = {}, []
    if args.overrides:
        if named_sources is not None and not named_sources:
            raise project_profile.ProfileError("--override-source names no source, name parsons or native or leave it out for the default of R330")
        # R330. With no source named a build applies majority.overrideDefaultSources, parsons, so native overrides never apply by default
        sources = list(named_sources) if named_sources else override_source.default_sources(settings)
        # R280. Only the overrides of a source of the job of the level profile
        overrides = plan_rules.load_overrides(args.overrides, set(sources), fields, block["groups"], name, lp, set(models) or None)
    return {"name": name, "levelProfile": lp, "hands": profile.get("hands") or {}, "openEnds": open_ends, "overrides": overrides,
            "sources": sources, "models": list(models)}


def main(argv=None):
    parser = argparse.ArgumentParser(description="plan.json from CAD and the data book only (R208 to R210).")
    parser.add_argument("cover", help="cover.json of key_cover.py, its neighbourhood and plots are built, or nbh=<NBH> with --labels")
    parser.add_argument("--labels", help="location_plan/labels.csv of the snapshot, for nbh=<NBH>")
    parser.add_argument("--sales", help="the Rev 9 workbook of the snapshot, with it the corner rule of R226 sets the front")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--plots-csv", required=True, help="plots.csv of the snapshot")
    parser.add_argument("--landxml", required=True, help="the landxml folder of the snapshot")
    parser.add_argument("--site", required=True, help="testhost_<code>_site.json of the newSite job")
    parser.add_argument("--baseplan", required=True, help="plots.json of the baseplan reader")
    parser.add_argument("--roads", required=True, help="folder with pieces.json of the road layout reader")
    parser.add_argument("--analysis", help="analysis.json of cad_analysis.py, its clusters are used when no footprint blocks are given")
    parser.add_argument("--footprint-blocks", help="R236. blocks.json of the footprint drawing, the plots that share a block are the clusters")
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--floors", default=str(floor_fit.DEFAULT_PATH))
    parser.add_argument("--scratch", required=True, help="a folder outside the repo for the LandXML reader")
    parser.add_argument("--level-profile", help="R277, R278. a level profile of the rules profile, levelProfiles.default when left out")
    parser.add_argument("--overrides", help="R280. the override list in config/overrides")
    parser.add_argument("--override-source", nargs="*", help="R330. the jobs whose overrides apply, majority.overrideDefaultSources (parsons) when left out")
    parser.add_argument("--override-model", nargs="*", default=[], help="R280, R330. only the overrides of these past model files")
    parser.add_argument("--override-fields", nargs="*", choices=plan_rules.FIELDS, help="R280. only these fields of the overrides, all when left out")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        block = settings.get("siteBuild")
        if args.cover.startswith("nbh="):
            if not args.labels:
                raise project_profile.ProfileError("nbh=<NBH> needs --labels")
            nbh = args.cover.partition("=")[2]
            cover = {"neighbourhood": nbh, "plots": nbh_plots(args.labels, nbh)}
        else:
            cover = json.loads(Path(args.cover).read_text(encoding="utf-8"))
        sales = None
        if args.sales:
            sheet = json.loads((project_profile.ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["locationPlan"]["sales"]
            sales = corner_front.sales_rows(args.sales, sheet["sheet"], sheet["columns"]["plot"])
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        rows, number_of = book_rows(args.plots_csv)
        by_label, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
        faces = {number_of[label]: shape for label, shape in by_label.items() if label in number_of}
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
        if args.footprint_blocks:
            clusters = footprint_clusters(cover["plots"], rows, faces, footprint_check.block_shapes(footprint_check.footprint_blocks(args.footprint_blocks, settings)))
            if args.analysis:
                clusters = joined_clusters(clusters, numbered_clusters(json.loads(Path(args.analysis).read_text(encoding="utf-8"))["clusters"], number_of))
        elif args.analysis:
            clusters = json.loads(Path(args.analysis).read_text(encoding="utf-8"))["clusters"]
        else:
            raise project_profile.ProfileError("give --analysis or --footprint-blocks for the clusters")
        job = plan_from_past.load_job(settings)
        folder, _ = plan_from_past.prototype_folders(plan_from_past.input_root(settings), job)
        # R277. Every build follows a level profile, the named one or levelProfiles.default
        rules = rule_inputs(args, settings, profile, cover["plots"], rows, faces, roads, sales)
        road = rules["levelProfile"]["frontRoad"]["surface"]
        names = block["roadSurfaces"] if road == "roadLevel" else [road]
        surfaces = load_roads([Path(args.landxml) / (name + ".xml") for name in names], args.scratch)
        plan = build(cover["plots"], rows, number_of, faces, roads, clusters, profile, settings,
                     prototype_offsets.plot_frame(prototype_offsets.load(args.offsets)), folder_files(folder, profile), folder,
                     site_report(args.site), surfaces, floor_fit.load(args.floors), cover["neighbourhood"], sales, bool(args.footprint_blocks), rules)
        plan_from_past.write_plan(out, plan)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("plan_from_book: " + str(error), file=sys.stderr)
        return 2
    summary = plan["summary"]
    print("level profile %s (R277), %s" % (rules["name"], "named" if args.level_profile else "the default of the rules profile"))
    print("plots written %d of %d, missing %s" % (summary["written"], summary["plots"], summary["missingByKey"]))
    print("in a cluster %d, clusters %d with %d plots" % (summary["inACluster"], summary["clusters"], summary["clusterPlots"]))
    print("road surfaces of the front corners %s, plots with no road level %s" % (summary["roadSurfacesOfFrontCorners"], summary["plotsWithNoRoadLevel"]))
    print("front level against plots.csv %s, front rule %s" % (summary["frontLevelAgainstPlotsCsv"], summary["frontRule"]))
    print("floors %s, types %s" % (summary["floorsFound"], summary["floorTypes"]))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
