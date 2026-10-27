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
R337: a level profile with keyRules, parsons2024Rev6, builds the hands, link origin, ramp class, yard per zone, yard outline and
link parameters by the rules measured on its answer keys and places their overrides, the Round 17 tables only for native2022.
"""
import argparse
import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Polygon
from shapely.strtree import STRtree

import c3d_connect
import cad_analysis
import cad_units
import cad_links
import corner_front
import common
import data_book_retest
import face_rings
import floor_fit
import footprint_check
import key_rules
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
import parity
import ramp_outline
import surface_lookup
import yard_rule

ESTIMATE = "estimate"
LOCATION = "locationPlan"
OVERRIDE = "override"
KEY_RULES = "keyRules"
# R345. The steps of the parity harness this builder writes with --steps, the build steps of R354 to R357 after the faces
PARITY_STEPS = ("faces", "clusters", "fronts", "hands", "linkFrame")
BUILD_STEPS = ("clusters", "fronts", "hands", "linkFrame")
FOOTPRINT, ANALYSIS = "footprint", "analysis"
# R337. The source of a parameter that reads the steps, the stepBands of the level profile when keyRules codes no steps
STEP_BANDS = "levelProfileStepBands"
sourced = plan_from_cad.sourced


def book_rows(plots_csv):
    """The rows of plots.csv by location plan number, and the number of each baseplan label. utf-8-sig drops the byte order mark a
    file saved by Excel starts with, as the C# reader does (R345)."""
    with open(plots_csv, encoding="utf-8-sig") as handle:
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


def site_clusters(units, rows, faces, shapes, analysis, number_of):
    """R236, R354. The clusters a build by keyRules takes, the site as the keys were measured on (R271): the footprint clusters over every
    unit plot, then each cluster of the analysis less the plots a footprint cluster holds, a face of two plan numbers such as 476|477 left
    out of its cluster as majority.py leaves it, each of two plots or more with its source, footprint or analysis."""
    footprint = footprint_clusters(units, rows, faces, shapes) if shapes is not None else []
    joined = joined_clusters(footprint, numbered_clusters(analysis, number_of)) if analysis is not None else footprint
    found = []
    for index, cluster in enumerate(joined):
        plots = [plot for plot in cluster["plots"] if plot.isdigit()]
        if len(plots) > 1:
            found.append({**cluster, "plots": plots, "source": FOOTPRINT if index < len(footprint) else ANALYSIS})
    return found


def cluster_step(numbers, clusters):
    """R354. The step clusters of the parity harness: every cluster of the build that holds a plot of the scope, whole, by its plots in
    the order of their numbers joined by a bar, with its plots as the build takes them, its code and its source."""
    wanted, found = set(numbers), {}
    for cluster in clusters:
        if wanted & set(cluster["plots"]):
            key = "|".join(sorted(cluster["plots"], key=link_summary.sort_key))
            found[key if key not in found else key + "#" + str(sum(1 for name in found if name.split("#")[0] == key) + 1)] = {
                "plots": list(cluster["plots"]), "code": cluster.get("code"), "source": cluster["source"]}
    return found


def build_step_rules(profile, name):
    """R354. keyRules of the level profile of the build steps, held by plan_rules.key_rules_of, and refused when the profile holds none:
    the C# builds by keyRules alone, so the build steps of the parity harness are those of a build by keyRules (BuildRules.KeyRules)."""
    key = plan_rules.key_rules_of(plan_rules.level_profile(profile, name))
    if key is None:
        raise project_profile.ProfileError("the build steps %s of the parity harness are those of a build by keyRules, the level profile %s holds none, "
                                           "the C# builds by keyRules alone (R354)" % (", ".join(BUILD_STEPS), plan_rules.profile_name(profile, name)))
    return key


def front_step(numbers, front, surfaces, snap):
    """R355. The step fronts of the parity harness: per plot of the scope the front the build takes (fronts), the rule that gave it, the
    frame of R122 or of the edge of R226 with its ends left and right seen from the road, its inward normal and right hand, length,
    distance to the nearest curb and tie, the ramp outline of R127 on it with its depth, the road level of R344 at the two front corners
    with the surface that gave each, as the build keeps them, and for a unit plot with none the entry of noRoadLevel with why."""
    found = {}
    for number in numbers:
        item = front[number]
        frame = item["frame"]
        levels = item["front"] or [None, None]
        names = item["surfaces"] or [None, None]
        found[number] = {
            "frontRule": item["frontRule"], "unit": item["unit"], "left": None if frame is None else list(frame["left"]), "right": None if frame is None else list(frame["right"]),
            "normal": None if frame is None else list(frame["n"]), "rightHand": None if frame is None else list(frame["r"]),
            "length": None if frame is None else frame["length"], "distance": None if frame is None else frame["distance"], "tied": None if frame is None else frame["tied"],
            "depth": item.get("depth"), "corners": None if item["corners"] is None else [list(corner) for corner in item["corners"]],
            "roadLevelLeft": levels[0], "roadLevelRight": levels[1], "surfaceLeft": names[0], "surfaceRight": names[1],
            "noRoadLevel": no_road_level(number, item, surfaces, snap) if item["unit"] and item["front"] is None else None}
    return found


def hand_step(numbers, rows, rules, trace, files, profile, block):
    """R357. The step hands of the parity harness: per plot of the scope what the hand rule of keyRules reads and gives, as the build
    takes it: the kind and the pair kind of its unit (majority.kind_of, pair_kind), its unit class, its cluster, side, role, index and
    count (cad_links.cluster_roles), its side neighbours left and right in the order of their numbers (majority.side_neighbours), the open
    end, the side road, its row pair (key_rules.row_pairs), the block mirror flag, the factors joined of two (plan_rules.joined_factors),
    the rule of hand villa, cluster or alone, the value of its factor, the file the plot links and the mirror flag the table of that file
    gives (plan_rules.key_hand), before an override of the list. A hand that differs from the one the build took is refused."""
    import majority
    key, roles = rules["keyRules"], trace["roles"]
    found = {}
    for number in numbers:
        row = rows.get(number) or {}
        unit = row.get("typologyRev9") or None
        role = roles.get(number)
        sides = rules["neighbours"].get(number)
        pair = rules["rowPairs"].get(number)
        factors = plan_rules.joined_factors({"all": "all", "blockMirror": {"yes": "yes", "no": "no"}.get(row.get("footprintMirrored")),
                                             "clusterSide": role["side"] if role is not None else "alone", "openEnd": rules["openEnds"].get(number),
                                             "roadSide": rules["roadSides"].get(number), "rowPairSide": rules["pairs"].get(number)})
        unit_class = plan_rules.unit_class(rules["kinds"].get(number), role)
        hand, rule = plan_rules.key_hand(key, unit_class, files.get(unit), factors) if unit else (None, None)
        built = trace["hands"].get(number)
        if built is not None and (built["mirrored"], built["rule"]) != (hand, rule):
            raise ValueError("the step hands gives plot %s the hand %r by %s and the build took %r by %s (R357)" % (number, hand, rule, built["mirrored"], built["rule"]))
        found[number] = {
            "unit": unit, "kind": rules["kinds"].get(number), "pairKind": majority.pair_kind(number, rows, profile, block), "unitClass": unit_class,
            "cluster": None if role is None else list(role["plots"]), "clusterCode": None if role is None else role["cluster"],
            "clusterSide": None if role is None else role["side"], "clusterRole": None if role is None else role["role"],
            "clusterIndex": None if role is None else role["index"], "clusterUnits": None if role is None else role["units"],
            "left": None if sides is None else sorted(sides["left"], key=link_summary.sort_key), "right": None if sides is None else sorted(sides["right"], key=link_summary.sort_key),
            "openEnd": rules["openEnds"].get(number), "roadSide": rules["roadSides"].get(number),
            "rowPair": None if pair is None else {"left": pair["left"], "right": pair["right"], "length": pair["length"], "side": pair["side"]},
            "blockMirror": factors["blockMirror"], "blockMirrorRoadSide": factors["blockMirrorRoadSide"], "blockMirrorOpenEnd": factors["blockMirrorOpenEnd"],
            "rowPairSideOrBlockMirrorOpenEnd": factors["rowPairSideOrBlockMirrorOpenEnd"], "handRule": rule, "factor": factors.get(rule) if rule else None,
            "linkFile": files.get(unit) if unit else None, "mirrored": hand}
    return found


def link_step(numbers, rows, trace, hands, key, files, profile):
    """R356. The step linkFrame of the parity harness: per plot of the scope the link plan_from_cad.link_values gives on the front the
    build takes (fronts) with the hand by the rule of the step hands, before an insert or rotation override of the list moves it (R360):
    the prototype file of its code, the unit whose fit of keyRules.linkOrigin it takes, its own or that of its keo_file, the anchor and
    offset of that fit, the mirror flag, the insert in plan unrounded and the rotation of the link X axis, written as the plan writes them,
    and what the link lacks with why, as the plan lists it."""
    section = plan_rules.key_frame_section(key)
    found = {}
    for number in numbers:
        unit = (rows.get(number) or {}).get("typologyRev9") or None
        item = trace["front"].get(number) or {"face": None, "frame": None}
        builder = plan_from_past.PlotBuilder(number)
        fit_unit = fit = None
        mirrored = hands[number]["mirrored"]
        if unit:
            keo = ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file")
            fit_unit = unit if unit in section["units"] else keo
            fit = section["units"].get(fit_unit)
            plan_from_cad.link_values(builder, unit, item["face"], item["frame"], trace["roles"].get(number), section, files, mirrored, fit_unit)
        placed = builder.has("insert")
        found[number] = {
            "prototypeFile": (builder.entry.get("prototypeFile") or {}).get("value"), "fitUnit": fit_unit, "anchor": None if fit is None else fit["anchor"],
            "offset": None if fit is None else list(fit["offset"]), "mirrored": mirrored if unit else None,
            "insert": list(cad_links.predicted_origin(item["frame"], mirrored, fit)) if placed else None,
            "rotationDeg": cad_links.rotation_of(item["frame"], mirrored) if placed else None, "lacks": builder.missing}
    return found


def one_ring_faces(plots):
    """R354. A build takes the face of a label as one ring, whose edges it walks in the order shapely gives them (face_ring), so a label of
    two rings or more, which face_shapes joins by a union whose ring order only shapely knows, is refused here and in the C# alike. A ring
    is one the build takes: a whole label, three vertices or more and an area."""
    count = Counter()
    for entry in plots or []:
        label = yard_rule.whole_label(entry.get("plot")) if entry.get("kind") == "plot_shape" else None
        vertices = entry.get("vertices") or []
        if label is not None and len(vertices) >= 3 and not Polygon([(vertex[0], vertex[1]) for vertex in vertices]).buffer(0).is_empty:
            count[str(label)] += 1
    many = sorted((label for label, held in count.items() if held > 1), key=link_summary.sort_key)
    if many:
        raise project_profile.ProfileError("%d baseplan labels hold two rings or more: %s, a build takes a face of one ring, whose edges it walks in the order shapely "
                                           "gives them, so plan_from_book.py and the C# refuse such a label (R354)" % (len(many), ", ".join(many)))


def face_ring(shape):
    """R354. The outer ring of a face as the build walks it, the vertices shapely gives with the closing one left out: for a face of
    Polygon(vertices).buffer(0) the first vertex and then the others in reverse when the ring was drawn anticlockwise (BuildFace.Of)."""
    return [(point[0], point[1]) for point in list(shape.exterior.coords)[:-1]]


def simple_blocks(blocks):
    """R354. The footprint blocks of a build, a block whose outline of three points or more is not a simple ring refused with its name,
    as plot_table.simple_blocks refuses it for the plot table (R352): block_shapes mends it by buffer(0) and the C# would not."""
    for block in blocks:
        points = face_rings.clean([point[:2] for point in block.get("outline") or []])
        if len(points) >= 3 and not face_rings.simple(points):
            raise ValueError("the footprint block %s at %s is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, "
                             "so the build refuses it (R354)" % (block.get("name"), block.get("insertPoint")))
    return blocks


def analysis_clusters(path, settings, baseplan, blocks=None):
    """R354. The clusters of the analysis.json a build joins (R236), held to what it claims to be: the analysis of the baseplan drawing
    drawings.baseplan names, by the name in its source, a clusters list, and the reads it was made from (hold_analysis)."""
    path = Path(path)
    data = json.loads(path.read_text(encoding="utf-8"))
    drawing = settings.get("drawings")["baseplan"]
    if data.get("source") != drawing:
        raise project_profile.ProfileError("%s is of %s, the analysis a build joins is of the baseplan drawings.baseplan names, %s (R354)" % (path.name, data.get("source"), drawing))
    if not isinstance(data.get("clusters"), list):
        raise project_profile.ProfileError("%s holds no clusters list, the clusters of the analysis a build joins (R354)" % path.name)
    hold_analysis(data, path.name, baseplan, blocks)
    return data["clusters"]


# R354, R2. Half a unit of the 3 decimals cad_analysis writes the rotation of a block at
ANALYSIS_TURN = 0.0005


def hold_analysis(data, name, baseplan, blocks=None):
    """R354, Q089. The analysis records the name of its drawing and no hash or date of it, so a build holds it to the reads it was made
    from by what it records of them, as SiteClusters.HoldAnalysis does: summary.plotFaces is the count of the plot faces of the baseplan read
    (cad_analysis.plot_faces), each plot of its plots list lies on the face of its label in that read with its type, cadType, and each plot
    it gives a block name lies on a block of that name in the footprint read, the one that shares the most of its face, at its rotation within
    ANALYSIS_TURN by the turn. An analysis of a later drawing whose labels moved puts a label on another face, type or block."""
    summary, records = data.get("summary"), data.get("plots")
    counted = summary.get("plotFaces") if isinstance(summary, dict) else None
    if not isinstance(records, list) or not isinstance(counted, int) or isinstance(counted, bool):
        raise project_profile.ProfileError("%s holds no plots list or no summary.plotFaces, which hold the analysis a build joins to the reads it was "
                                           "made from (R354)" % name)
    faces, count = {}, 0
    for entry in baseplan or []:
        if entry.get("kind") == "plot_shape" and entry.get("plot") not in (None, "") and len(entry.get("vertices") or []) >= 3:
            faces[str(entry["plot"])] = entry
            count += 1
    if count != counted:
        raise project_profile.ProfileError("%s counts %d plot faces and the baseplan read holds %d, the analysis a build joins is of the reads it was "
                                           "made from (R354)" % (name, counted, count))
    labels = [record.get("plot") if isinstance(record, dict) else None for record in records]
    off = [label for label, record in zip(labels, records)
           if not isinstance(label, str) or label not in faces or faces[label].get("type") != record.get("cadType")]
    if off:
        raise project_profile.ProfileError("%d plots of %s lie on no face of their label or on one of another type in the baseplan read: %s, the analysis "
                                           "a build joins is of the reads it was made from (R354)" % (len(off), name, ", ".join(str(label) for label in off[:10])))
    if blocks is None:
        return
    shapes = defaultdict(list)
    for block in blocks:
        if len(block.get("outline") or []) >= 3:
            shapes[block.get("name")].append((Polygon([(point[0], point[1]) for point in block["outline"]]).buffer(0), block))
    for label, record in zip(labels, records):
        if not isinstance(record.get("block"), str) or not record["block"]:
            continue
        face = Polygon([(vertex[0], vertex[1]) for vertex in faces[label]["vertices"]]).buffer(0)
        best, held = None, 0.0
        for shape, block in shapes.get(record["block"], []):
            if shape.intersects(face):
                share = shape.intersection(face).area
                if share > held:
                    best, held = block, share
        turn = record.get("rotation")
        if best is None or (isinstance(turn, (int, float)) and not isinstance(turn, bool) and not (
                isinstance(best.get("rotationDeg"), (int, float)) and abs((best["rotationDeg"] - turn + 180.0) % 360.0 - 180.0) <= ANALYSIS_TURN)):
            off.append(label)
    if off:
        raise project_profile.ProfileError("%d plots of %s lie on no block of their name or on one of another rotation in the footprint read: %s, the "
                                           "analysis a build joins is of the reads it was made from (R354)" % (len(off), name, ", ".join(off[:10])))


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
    """R209, R344. The level of the first surface that covers the point, snapped to its edge within snap (R157), and its name, by the
    one rule of c3d_connect.road_level the data book takes as well. A corner none covers gets (None, None), never a road sideways."""
    return c3d_connect.road_level(surfaces, point, snap)


def no_road_level(number, item, surfaces, snap):
    """R344. Why a plot has no road level at a front corner, for the list of the summary: the corners with none, the road surfaces
    tried in their order and the reason, the other sources of the Roads folder given once in roadFolderNotTaken of the summary."""
    if item["corners"] is None:
        return {"plot": number, "corners": [], "tried": [], "reason": "no front frame, so no front corners to take a road level at"}
    missing = [side for side, name in zip(("left", "right"), item["surfaces"]) if name is None]
    return {"plot": number, "corners": missing, "tried": [name for name, _ in surfaces],
            "reason": "no road surface covers the corner within snapM %s m, so no road level is given (R344)" % snap}


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


def moved_link(builder, frame, overrides, number, outcomes):
    """R337. An insert override moves the link origin by its gap to the rule in the link axes, X along the front and Y into the plot,
    a rotation override turns the link by its gap in degrees. An override with no gap, or a plot with no link, is left with why."""
    for field, word in (("insert", "gapToRuleM"), ("rotationDeg", "gapToRuleDeg")):
        if field not in overrides:
            continue
        gap = overrides[field].get(word) if isinstance(overrides[field], dict) else None
        if gap is None or frame is None or not builder.has(field) or not builder.has("mirrored"):
            outcomes.left(number, field, NO_GAP if gap is None else "the face has no front frame" if frame is None else "the plot has no link")
            continue
        if field == "insert":
            axis_x, axis_y = cad_links.link_axes(frame, builder.entry["mirrored"]["value"])
            point = builder.entry["insert"]["value"]
            moved = [common.r3(point[0] + gap[0] * axis_x[0] + gap[1] * axis_y[0]), common.r3(point[1] + gap[0] * axis_x[1] + gap[1] * axis_y[1])]
            builder.entry["insert"] = sourced(moved + list(point[2:]), OVERRIDE)
        else:
            builder.entry["rotationDeg"] = sourced(plan_from_past.plan_rotation(builder.entry["rotationDeg"]["value"] + gap), OVERRIDE)
        outcomes.applied(number, field)


# R337. Why a build leaves an override of a field it never places
NO_GAP = "the override holds no gap to the rule"
LEFT = {"rampOutline": "an outline gap, a build places the ramp outline of R127", "yardOutline": "an outline gap, a build places the fitted yard outline",
        "genericFloor": "an outline gap, a build places the whole face", "rampFrontFallM": "a ramp front fall is not applied (Q057)"}


class Outcomes:
    """R337, R280. What a build did with each override of each plot of the build: applied, or left with why, so the plan says it plot
    by plot and the summary counts as applied only what a written plot took. An override of a plot of a cluster outside the build
    that moved the El @ Entry or the FFL of its cluster is counted apart. An entry the override list marks use rule is left with why (R386)."""

    def __init__(self, overrides, numbers, later, name):
        self.overrides, self.numbers, self.later, self.name = overrides, set(numbers), set(later), name
        self.ruled = getattr(overrides, "ruled", {})
        self.done, self.why, self.outside = defaultdict(set), defaultdict(dict), Counter()

    def applied(self, number, field):
        if number in self.numbers:
            self.done[number].add(field)
        else:
            self.outside[field] += 1

    def left(self, number, field, why):
        if number in self.numbers:
            self.why[number].setdefault(field, why)

    def plot(self, number, written, lacks):
        """The applied and the left overrides of a plot, None for a plot with none."""
        ruled = self.ruled.get(number) or {}
        fields = set(self.overrides.get(number) or {}) | set(ruled)
        if not fields:
            return None
        applied, left = [], {}
        unwritten = "the plot is not written, it lacks " + ", ".join(lacks)
        for field in sorted(fields):
            if field in ruled:
                left[field] = ruled[field]
            elif field in self.why[number]:
                left[field] = self.why[number][field]
            elif field in self.done[number]:
                if written:
                    applied.append(field)
                else:
                    left[field] = unwritten
            elif field in LEFT:
                left[field] = LEFT[field]
            elif field in self.later:
                left[field] = "left to 3c, majority.overrideKeep.laterFields"
            elif not written:
                left[field] = unwritten
            else:
                left[field] = "a build of %s does not apply the field" % self.name
        return {"applied": applied, "notApplied": left}


def not_applied_why(records):
    """R337. The overrides a build left, by field and why, with the plots of each, so the summary says why as the plan says it."""
    found = defaultdict(lambda: defaultdict(list))
    for number, record in records.items():
        for field, why in (record or {}).get("notApplied", {}).items():
            found[field][why].append(number)
    return {field: {why: {"plots": len(plots), "first": sorted(plots, key=link_summary.sort_key)[:10]} for why, plots in sorted(whys.items())}
            for field, whys in sorted(found.items())}


def key_levels(entries, key, roles, partners, rows, overrides, outcomes, numbers, front, profile, lp):
    """R340. FFL, SSL, height and steps by the level rules of keyRules, as R339 measured them: one FFL per piece at its highest El @ Entry
    plus the base of that unit to the mm, then the heightM gap moves the FFL onto the key with its SSL, height and steps, and a steps
    override sets the steps. An override of a plot with no FFL is left with why."""
    units = {number: (rows.get(number) or {}).get("typologyRev9") for number in front}
    level_rows = plan_rules.key_level_rows(entries, plan_rules.key_pieces(key, list(entries), roles, partners, units), profile, lp, key)
    ground = profile.metres("levels.ground_ssl")
    for number, row in level_rows.items():
        # R340. An override moves the levels of its own plot only, so those of a plot outside the build move nothing written
        mine = overrides.get(number, {}) if number in set(numbers) else {}
        # R340. The FFL the rule alone gives, before the heightM gap, which the plan writes in levelRule beside the levels
        row["fflRule"] = row["ffl"]
        gap = plan_rules.gap_of(mine.get("heightM"))
        if gap is not None:
            row["ffl"] = common.fixed(row["ffl"] + gap, plan_rules.FFL_DECIMALS)
            row["ssl"] = common.fixed(row["ffl"] + ground, plan_rules.FFL_DECIMALS)
            row["height"] = common.fixed(row["ffl"] - row["entry"], plan_rules.ENTRY_DECIMALS)
            row["steps"] = plan_rules.steps_by_rule(key["steps"]["rule"], row["height"], entries[number]["unit"], profile, lp, key.get("baseHeight"))
            outcomes.applied(number, "heightM")
        elif "heightM" in mine:
            outcomes.left(number, "heightM", NO_GAP)
        count = plan_rules.whole_number(mine.get("steps"))
        if count is not None:
            row["steps"] = count
            outcomes.applied(number, "steps")
        elif "steps" in mine:
            outcomes.left(number, "steps", "the override holds no step count")
    for number in numbers:
        for field in ("heightM", "steps"):
            if field in overrides.get(number, {}) and number not in level_rows:
                outcomes.left(number, field, "no FFL, " + ("no road level at a front corner" if front[number]["front"] is None else "the rule gives none"))
    return level_rows


def rule_levels(row, entry, item, key, unit, profile, rules):
    """R340. The levels the rule alone gives a plot, before any override of the list moves them: El @ Entry, the FFL, the height, the
    steps, the level parameters of keyRules and the two front corners of the ramp at the 3 decimals the plan writes them, their mean
    left to the proof, which a plan written to 3 decimals would round off its half mm, so the proof tells a level that holds by the rule
    from one an override puts on the key."""
    height = common.fixed(row["fflRule"] - entry["rule"], plan_rules.ENTRY_DECIMALS)
    found = {"entry": entry["rule"], "ffl": row["fflRule"], "height": height,
             "steps": plan_rules.steps_by_rule(key["steps"]["rule"], height, unit, profile, rules["levelProfile"], key.get("baseHeight"))}
    for word, rule in key.items():
        if word.startswith(plan_rules.PARAMETER) and word not in rules["laterParameters"] and isinstance(rule, dict) and rule.get("rule") in plan_rules.LEVEL_RULES:
            found[word] = plan_rules.prototype_ffl(rule["rule"], row["fflRule"])
    if item["front"] is not None:
        found["rampFrontCorners"] = [common.r3(z) for z in item["front"]]
    return found


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


def build(numbers, rows, number_of, faces, roads, clusters, profile, settings, frame_section, files, folder, site, surfaces, outlines, nbh, sales=None, numbered=False, rules=None,
          trace=None):
    """R208. The plan of R80 for the plots numbers, every value from CAD, the data book and the new site file. R278 to R280: with
    rules the level profile, the hand rules and the overrides of the majority rule give the levels, the ramp type and the hand.
    main always gives rules (R277), rules None is the estimate of R209 that round Z4 built with. R345: trace, a dict, takes the fronts
    and the snap the build took, for the build steps of the parity harness."""
    block = settings.get("siteBuild")
    floors_config = plan_from_past.load_floors(settings)
    steep = set(block["steepUnits"])
    snap = plan_rules.road_snap(block)
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    # R337. A profile with keyRules builds the link origin by the fits per code measured on its keys, never the IMC fit of config
    key = None if rules is None else rules.get("keyRules")
    # R340. keyRules with the level rules gives El @ Entry, the FFL per piece to the mm, the steps and the Prototype-FFL parameter
    by_levels = plan_rules.key_levels_of(key)
    stepped = set() if key is None or by_levels else plan_rules.step_parameters(key, rules["laterParameters"])
    partners = (rules.get("partners") or {}) if by_levels else {}
    if key is not None:
        frame_section = plan_rules.key_frame_section(key)
        missing_zones = defaultdict(list)
        for number in numbers:
            row = rows.get(number) or {}
            if row.get("typologyRev9") and (row.get("zone") or "") not in key["yard"]["table"]:
                missing_zones[row.get("zone") or "no zone"].append(number)
        if missing_zones:
            raise project_profile.ProfileError("keyRules.yard holds no yard for %s, an area missing from the table is refused and never given another value (R337)"
                                               % ", ".join("%s (%d plots, %s)" % (zone, len(plots), " ".join(plots[:5])) for zone, plots in sorted(missing_zones.items())))
    mine = [cluster for cluster in (clusters if numbered else numbered_clusters(clusters, number_of)) if set(cluster["plots"]) & set(numbers)]
    # R340. The other unit of a row pair shares the FFL, so its El @ Entry is worked out as well
    members = sorted({plot for cluster in mine for plot in cluster["plots"]} | set(numbers) | {partners[number] for number in numbers if number in partners},
                     key=link_summary.sort_key)
    front = fronts(members, rows, faces, roads, limits, surfaces, snap, profile, block.get("corner") if sales is not None else None, sales)
    if trace is not None:
        trace.update(front=front, snap=snap)
    frames = {number: item["frame"] for number, item in front.items()}
    roles = cad_links.cluster_roles(mine, 0, {number: item["face"] for number, item in front.items() if item["face"] is not None}, frames)
    if trace is not None:
        trace.update(roles=roles, hands={})
    entries, slopes = {}, {}
    overrides = {} if rules is None else rules["overrides"]
    outcomes = Outcomes(overrides, numbers, [] if rules is None else rules.get("laterFields") or [], "the estimate of R209" if rules is None else rules["name"])
    in_build = set(numbers)
    # R386. The plots of the skip list of the override list are left out and listed in missing as skip, each a plot of plots.csv
    skip = getattr(overrides, "skip", {})
    override_source.skip_plots_known(skip, rows, "the skip list of the override list: ")
    for number, item in front.items():
        if item["front"] is not None:
            rule = None
            if rules is None:
                value, slope = entry_estimate(item["front"], item["unit"], profile, steep, item["depth"])
            else:
                value, slope = plan_rules.entry_of(item["front"], item["unit"], rules["levelProfile"], item["depth"])
                if by_levels:
                    # R340. At the 4 decimals of R2, as R339 measured it, so a gap moves it onto the key exactly
                    value = rule = common.fixed(value, plan_rules.ENTRY_DECIMALS)
                gap = plan_rules.gap_of(overrides.get(number, {}).get("entryM"))
                # R340. With the level rules a gap moves the El @ Entry of its own plot only, so one of a plot outside the build moves nothing
                if gap is not None and not (by_levels and number not in in_build):
                    value = common.fixed(value + gap, plan_rules.ENTRY_DECIMALS) if by_levels else value + gap
                    outcomes.applied(number, "entryM")
                elif gap is None and "entryM" in overrides.get(number, {}):
                    outcomes.left(number, "entryM", NO_GAP)
            entries[number] = {"unit": item["unit"], "entry": value, **({"rule": rule} if by_levels else {})}
            slopes[number] = slope
        elif "entryM" in overrides.get(number, {}):
            outcomes.left(number, "entryM", "no road level at a front corner, so no El @ Entry to move")
    if rules is None:
        level_rows = plan_from_cad.plot_levels(profile, mine, entries)
    elif by_levels:
        level_rows = key_levels(entries, key, roles, partners, rows, overrides, outcomes, numbers, front, profile, rules["levelProfile"])
    else:
        # R385. The levels of R278 and their overrides by the rounding the overrides of the profile were measured on, Round 17 for native2022
        fix = rules["rounding"]
        level_rows = plan_rules.heights(entries, mine, rules["levelProfile"], profile, fix)
        ground = profile.metres("levels.ground_ssl")
        for number, row in level_rows.items():
            gap = plan_rules.gap_of(overrides.get(number, {}).get("heightM"))
            if gap is not None:
                row["ffl"] = fix(row["ffl"] + gap, plan_rules.FFL_DECIMALS)
                row["ssl"] = fix(row["ffl"] + ground, plan_rules.FFL_DECIMALS)
                row["height"] = fix(row["ffl"] - row["entry"], plan_rules.ENTRY_DECIMALS)
                row["steps"] = plan_rules.steps_of(row["height"], rules["levelProfile"])
                outcomes.applied(number, "heightM")
            elif "heightM" in overrides.get(number, {}):
                outcomes.left(number, "heightM", NO_GAP)
        for number in numbers:
            if "heightM" in overrides.get(number, {}) and number not in level_rows and not by_levels:
                outcomes.left(number, "heightM", "no FFL to move, " + ("no road level at a front corner" if front[number]["front"] is None else "the height rule gives none"))
    inputs = {"outlines": outlines, "step": floor_fit.width_step(settings), "levels": site["levels"], "floors": floors_config, "bands": outlines["rampTypes"]["bands"]}
    plots, missing, lacking, surface_count, no_level, book_check, no_road = [], [], Counter(), Counter(), [], Counter(), []
    not_measured, not_measured_plots, records, hand_rules = {}, Counter(), {}, Counter()
    for number in numbers:
        item = front[number]
        builder = plan_from_past.PlotBuilder(number)
        unit = item["unit"]
        if number in skip:
            builder.lack("skip", "the skip list of the override list: %s (R386)" % skip[number])
            records[number] = outcomes.plot(number, False, ["skip"])
            missing.append({"plot": number, "written": False, "missing": builder.missing, **({"overrides": records[number]} if records[number] else {})})
            lacking.update(entry["key"] for entry in builder.missing)
            continue
        if unit is None:
            # R230. A location plan number with no plots.csv row or no Typology_Rev9, such as a label sharing its face, is listed
            builder.lack("unit", "plots.csv holds no row or no Typology_Rev9 for the plot")
            records[number] = outcomes.plot(number, False, ["unit"])
            missing.append({"plot": number, "written": False, "missing": builder.missing, **({"overrides": records[number]} if records[number] else {})})
            lacking.update(entry["key"] for entry in builder.missing)
            continue
        builder.entry["unit"] = sourced(unit, LOCATION)
        hand, fit_unit, override = None, None, None
        mine_overrides = {} if rules is None else rules["overrides"].get(number, {})
        if rules is not None:
            keo = ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file")
            fit_unit = unit if unit in frame_section["units"] else keo
            block_mirror = {"yes": "yes", "no": "no"}.get(rows.get(number, {}).get("footprintMirrored"))
            if key is not None:
                # R337. The factor the coded hand rule of the keys names, looked up in the table of the file the plot links
                factors = plan_rules.joined_factors({"all": "all", "blockMirror": block_mirror, "clusterSide": roles[number]["side"] if number in roles else "alone",
                                                     "openEnd": rules["openEnds"].get(number), "roadSide": rules["roadSides"].get(number),
                                                     "rowPairSide": rules["pairs"].get(number)})
                hand, rule = plan_rules.key_hand(key, plan_rules.unit_class(rules["kinds"].get(number), roles.get(number)), files.get(unit), factors)
                if trace is not None:
                    # R357. The hand by the rule, before an override of the list sets it
                    trace["hands"][number] = {"mirrored": hand, "rule": rule}
            else:
                sides = {code: {"drawnSide": value.get("drawnSide")} for code, value in frame_section["units"].items()}
                kind = ((profile.get("typologies") or {}).get(unit) or {}).get("kind")
                hand, rule = plan_rules.hand_of(unit, kind, roles.get(number), block_mirror, rules["openEnds"].get(number), rules["hands"],
                                                cad_links.mirror_of(fit_unit or unit, roles.get(number), sides), keo)
            override = mine_overrides.get("mirrored")
            if isinstance(override, bool):
                hand, rule = override, "override"
                outcomes.applied(number, "mirrored")
            elif "mirrored" in mine_overrides:
                outcomes.left(number, "mirrored", "the override holds no mirror flag")
        plan_from_cad.link_values(builder, unit, item["face"], item["frame"], roles.get(number), frame_section, files, hand, fit_unit)
        if key is not None:
            if isinstance(override, bool) and builder.has("mirrored"):
                builder.entry["mirrored"]["source"] = OVERRIDE
            moved_link(builder, item["frame"], mine_overrides, number, outcomes)
        level_values(builder, level_rows.get(number))
        steps = builder.entry["levels"].get("steps", {}).get("value")
        if key is not None:
            # R337. The link parameters by the parameter rules of the keys, those left to a later phase not written, then their overrides
            mirrored = builder.entry["mirrored"]["value"] if builder.has("mirrored") else None
            values, lacking_parameters = plan_rules.key_parameters(key, number, unit, mirrored, steps, builder.entry["levels"].get("ffl", {}).get("value"),
                                                                   rules["facade"], rules["laterParameters"])
            builder.entry["parameters"] = {name: sourced(value, STEP_BANDS if name in stepped else KEY_RULES) for name, value in values.items()}
            for name in lacking_parameters:
                builder.lack("parameters." + name, "the rule of keyRules gives no value for " + unit + (" mirrored" if mirrored else ""))
            if by_levels and level_rows.get(number) is not None:
                builder.entry["levelRule"] = rule_levels(level_rows[number], entries[number], item, key, unit, profile, rules)
            for field, value in mine_overrides.items():
                if field.startswith(plan_rules.PARAMETER) and field not in rules["laterParameters"]:
                    name = field[len(plan_rules.PARAMETER):]
                    if isinstance(value, dict):
                        # R340. A level parameter holds its gap to the rule, the text of the rule moved by it with 3 decimals
                        gap, current = plan_rules.gap_of(value), key_rules.number((builder.entry["parameters"].get(name) or {}).get("value"))
                        if gap is None or current is None:
                            outcomes.left(number, field, NO_GAP if gap is None else "the rule gives the parameter no value to move")
                            continue
                        builder.entry["parameters"][name] = sourced("%.3f" % common.fixed(current + gap, plan_rules.FFL_DECIMALS), OVERRIDE)
                    else:
                        builder.entry["parameters"][name] = sourced(str(value), OVERRIDE)
                    outcomes.applied(number, field)
                elif field.startswith(plan_rules.PARAMETER):
                    outcomes.left(number, field, "left to 3c, majority.overrideKeep.laterFields, the build writes no such parameter")
        else:
            builder.entry["parameters"] = {plan_from_past.PLOT_PARAMETER: sourced(number, LOCATION), plan_from_past.UNIT_PARAMETER: sourced(unit, LOCATION)}
            if steps is not None:
                builder.entry["parameters"][plan_from_past.STEPS_PARAMETER] = sourced(str(steps), ESTIMATE)
        surface_count.update(name or "none" for name in item["surfaces"])
        if item["front"] is None:
            no_level.append(number)
            no_road.append(no_road_level(number, item, surfaces, snap))
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
            yard_unit, yard_inputs = unit, inputs
            if key is not None:
                # R337. The yard per zone of the keys and the outline fitted on them, the outline of the unit the link holds, TH1-IM the TH1 one
                below, why = plan_rules.key_yard(key, rows[number]["zone"])
                if why:
                    not_measured[rows[number]["zone"]] = why
                    not_measured_plots[rows[number]["zone"]] += 1
                yard_inputs = dict(inputs, outlines=plan_rules.key_outlines(key, outlines))
                if not any(group["unit"] == unit for group in yard_inputs["outlines"]["outlines"].get("yard") or []):
                    yard_unit = ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file") or unit
            else:
                below = floors_config["yardBelowFflM"] if rules is None else rules["levelProfile"].get("yardBelowFflM")
            made = None if below is None else plan_from_cad.flat_floor(builder, "yard", yard_unit, item["frame"], yard_inputs, ffl - below)
            if made:
                made["z"]["source"] = made["heightOffset"]["source"] = ESTIMATE
                floors["yard"] = made
            ramp_front, kind, ramp_class, moved, named = item["front"], None, "rule", [], None
            if rules is not None:
                gap = plan_rules.gap_of(mine_overrides.get("rampFrontM"))
                if gap is not None:
                    ramp_front = [z + gap for z in item["front"]]
                    moved.append("rampFrontM")
                elif "rampFrontM" in mine_overrides:
                    outcomes.left(number, "rampFrontM", NO_GAP)
                named = mine_overrides.get("rampType")
                if named is not None:
                    moved.append("rampType")
                # R337. The ramp class the keys give the Rev 9 code, the slope of the level profile only for the Round 17 build
                ramp_class = named or (plan_rules.key_ramp_class(key, unit) if key is not None else plan_rules.slope_class(slopes[number]))
                kind = rules["levelProfile"]["rampTypes"].get(ramp_class) if ramp_class is not None else None
            if key is not None and kind is None:
                builder.lack("floors.ramp", "keyRules.rampSlope and rampTypes give no ramp type for " + unit)
                made = None
            else:
                made = ramp_floor(builder, item["frame"], item["corners"], ramp_front, builder.entry["levels"]["entry"]["value"], slopes[number], inputs, kind)
            for field in moved:
                if made:
                    outcomes.applied(number, field)
                else:
                    outcomes.left(number, field, "no ramp floor, " + next((found["why"] for found in builder.missing if found["key"] == "floors.ramp"), "none made"))
            if made:
                floors["ramp"] = made
        builder.entry["floors"] = floors
        builder.entry["roadSurfaces"] = item["surfaces"]
        # R337. What the build did with each override of the plot, in the plan entry or the missing entry of a plot not written
        records[number] = outcomes.plot(number, complete, [found["key"] for found in builder.missing])
        if complete:
            if records[number]:
                builder.entry["overrides"] = records[number]
            if rules is not None:
                hand_rules[str(rule)] += 1
            plots.append(builder.entry)
        if builder.missing:
            missing.append({"plot": number, "written": complete, "missing": builder.missing, **({"overrides": records[number]} if records[number] and not complete else {})})
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
            "skipped": sorted((number for number in numbers if number in skip), key=link_summary.sort_key),
            "clusters": len(mine), "clusterPlots": len(members), "inACluster": sum(1 for number in numbers if number in roles),
            "roadSurfacesOfFrontCorners": dict(surface_count.most_common()), "plotsWithNoRoadLevel": no_level,
            "noRoadLevel": no_road, "roadFolderNotTaken": (block.get("roadFolder") or {}).get("notTaken") or {},
            "frontRule": dict(Counter(front[number]["frontRule"] for number in numbers).most_common()),
            "frontLevelAgainstPlotsCsv": dict(book_check),
            "floorsFound": {kind: sum(1 for entry in plots if kind in entry["floors"]) for kind in plan_from_past.FLOOR_KINDS},
            "floorTypes": dict(sorted(Counter(kind + ": " + floor["type"]["value"] for entry in plots for kind, floor in entry["floors"].items()).items())),
            "rules": None if rules is None else {"levelProfile": rules["name"], "overrideSource": rules["sources"], "overrideModel": rules.get("models", []),
                                                "applied": dict(sorted(Counter(field for found in records.values() if found for field in found["applied"]).items())),
                                                "appliedOutsideBuild": dict(sorted(outcomes.outside.items())), "handRules": dict(sorted(hand_rules.items())),
                                                "keyRules": key is not None, "laterFields": rules.get("laterFields") or [],
                                                "overridesNotApplied": dict(sorted(Counter(field for found in records.values() if found for field in found["notApplied"]).items())),
                                                "overridesNotAppliedWhy": not_applied_why(records),
                                                "yardNotMeasured": {zone: {"plots": not_measured_plots[zone], "why": why} for zone, why in sorted(not_measured.items())},
                                                "steps": None if key is None else (
                                                    {"from": "keyRules.steps %s of %s" % (key["steps"]["rule"], rules["name"]),
                                                     "parameters": sorted(plan_rules.step_parameters(key, rules["laterParameters"]))} if by_levels
                                                    else {"from": "stepBands of the level profile " + rules["name"], "parameters": sorted(stepped)})},
        },
        "plots": plots,
        "missing": missing,
        "conflicts": [],
    }


def nbh_plots(labels_path, nbh):
    """Every unit plot of a neighbourhood on the location plan, by its number, in order. R345: nbh None gives every unit plot of the
    location plan, the whole project of the scope of the add-in (docs/addin-design.md section 3)."""
    with open(labels_path, encoding="utf-8-sig") as handle:
        rows = [row for row in csv.DictReader(handle) if (nbh is None or row.get("nbh") == nbh) and row.get("kind") == "clientCode"]
    return sorted({row["number"] for row in rows}, key=link_summary.sort_key)


def face_step(numbers, faces):
    """R345. The step faces of the parity harness: per plot of the build its baseplan face as the build takes it, by
    yard_rule.face_shapes and the number plots.csv gives its label, with its area, centroid and bounds unrounded, or no face with
    every other field null, and the values as the plan writes them (step_entry)."""
    found = {}
    for number in numbers:
        shape = faces.get(number)
        if shape is None:
            found[number] = step_entry({"face": False, "area": None, "centroid": None, "boundsMin": None, "boundsMax": None})
            continue
        low_x, low_y, high_x, high_y = shape.bounds
        found[number] = step_entry({"face": True, "area": shape.area, "centroid": [shape.centroid.x, shape.centroid.y], "boundsMin": [low_x, low_y],
                                    "boundsMax": [high_x, high_y]})
    return found


def step_entry(raw):
    """R345. A plot of a step file: its raw values and the values as the plan writes them, through common.round_tree as
    common.write_json writes plan.json, so the parity compares what Python writes and not a value recomputed from raw."""
    return {"raw": raw, "written": json.loads(json.dumps(common.round_tree(raw)))}


def area_tolerance(settings):
    """R345. The areas tolerance of addin.parity, the most the union of the rings of a label may fall short of their sum."""
    kinds = ((settings.get("addin") or {}).get("parity") or {}).get("kinds") or {}
    if not isinstance(kinds.get("areas"), (int, float)):
        raise project_profile.ProfileError("config holds no addin.parity.kinds.areas, the tolerance of the baseplan faces (R345)")
    return kinds["areas"]


def step_scope(cover):
    """R345. The scope of a step file as the worker names it: project for all, nbh=<NBH>, or cover=<file> for a cover file."""
    return "project" if cover == "all" else cover if cover.startswith("nbh=") else "cover=" + Path(cover).name


def step_inputs(args):
    """R345, R347. The inputs a step of this run was made from, by the roles of the worker, each with its SHA-256."""
    roles = [("config", args.settings), ("baseplanPlots", args.baseplan), ("plotsCsv", args.plots_csv), ("labels", args.labels),
             ("cover", None if args.cover == "all" or args.cover.startswith("nbh=") else args.cover)]
    return [parity.input_entry(role, path) for role, path in roles if path]


def build_inputs(args, settings):
    """R345, R354 to R357. The inputs the build steps were made from, those of the faces and every other file the steps read, by role."""
    roles = [("rules", project_profile.profile_path(settings)), ("footprintBlocks", args.footprint_blocks), ("analysis", args.analysis), ("sales", args.sales),
             ("roadPieces", Path(args.roads) / "pieces.json" if args.roads else None)]
    roles += [("roadSurface:" + name, Path(args.landxml) / (name + ".xml")) for name in getattr(args, "road_names", None) or []]
    return step_inputs(args) + [parity.input_entry(role, path) for role, path in roles if path]


def step_entries(settings, step, made, written=None):
    """R345, R354 to R357. The items of a build step by the fields of the step in addin.parity, a field of written written by its own
    function of the raw value where the plan writes it so, such as the rotation by plan_rotation, which writes 0 for one that rounds to 360."""
    fields = ((settings.get("addin") or {}).get("parity") or {}).get("steps", {}).get(step) or {}
    plots = {key: parity.fields_entry(raw, fields) for key, raw in made.items()}
    for entry in plots.values():
        for name, function in (written or {}).items():
            if entry["raw"].get(name) is not None:
                entry["written"][name] = function(entry["raw"][name])
    return plots


def write_build_step(args, settings, step, made, written=None):
    """R345, R354 to R357. step_<step>_python.json of a build step (step_entries)."""
    plots = step_entries(settings, step, made, written)
    parity.write_step(args.steps, step, "python", step_scope(args.cover), plots, settings.values, build_inputs(args, settings))
    print("step %s: %d items (R345)" % (step, len(plots)))


def site_report(path):
    """The survey point, angle and level elevations the newSite job read back from the new file."""
    report = json.loads(Path(path).read_text(encoding="utf-8"))
    return {"surveyPoint": report["surveyPoint"], "angleToTrueNorthDeg": report["angleToTrueNorthDeg"],
            "levels": {level["name"]: level["elevationM"] for level in report["levels"]}}


def folder_files(folder, profile):
    """The prototype file of each unit with its name as the folder spells it."""
    names = sorted(path.name for path in Path(folder).iterdir() if path.is_file())
    return plan_from_cad.prototype_files(names, profile)[0]


def rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales, clusters=()):
    """R277 to R280. The level profile, named or the default of R277, the hand rules, the open end of every plot and the overrides
    of the source models. R337: for a profile with keyRules also the side road, the pair of its row of units in no cluster (the
    clusters given, over every unit plot) and the kind of every plot, as majority.py gives them on the keys, the fronts of every
    unit plot of plots.csv found so a row is walked past the plots of the build."""
    import majority
    name = plan_rules.profile_name(profile, args.level_profile)
    lp = plan_rules.level_profile(profile, name)
    key = plan_rules.key_rules_of(lp)
    block = settings.get("majority")
    corner = settings.get("siteBuild")["corner"]
    if key is not None and key["linkRotation"]["rule"] == "frontRule" and sales is None:
        raise project_profile.ProfileError("keyRules.linkRotation of %s takes the front of R226, give --sales for the Orientation of a corner plot (R337)" % name)
    units = [number for number, row in rows.items() if row.get("typologyRev9") and number in faces]
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    front = fronts(sorted(set(numbers) | set(units)) if key is not None else numbers, rows, faces, roads, limits, [], 0.0, profile, corner, sales)
    frames = {number: item["frame"] for number, item in front.items()}
    neighbours = majority.side_neighbours(units, faces, frames, block["sideShare"], profile.get("plot.long_side") / 1000.0)
    open_ends = {}
    for number in numbers:
        sides = neighbours.get(number)
        if sides is not None and frames.get(number) is not None:
            open_ends[number] = "both" if not sides["left"] and not sides["right"] else "left" if not sides["left"] else "right" if not sides["right"] else "none"
    road_sides, pairs, kinds, partners = {}, {}, {}, {}
    if key is not None:
        tree = STRtree(roads) if len(roads) else None
        road_sides = {number: majority.road_side(faces.get(number), frames.get(number), tree, roads, corner["roadEdgeM"]) for number in numbers} if tree is not None else {}
        roles = cad_links.cluster_roles([cluster for cluster in clusters if len(cluster["plots"]) > 1], 0, {number: faces[number] for number in units}, frames)
        found = key_rules.row_pairs(units, neighbours, lambda plot: majority.pair_kind(plot, rows, profile, block), lambda plot: plot in roles, block["pairKinds"])
        pairs = {number: (found.get(number) or {}).get("side") for number in numbers}
        # R340. The other unit of the row pair of every unit in no cluster, which shares its FFL by keyRules.ffl
        partners = plan_rules.row_partners({plot: item.get("side") for plot, item in found.items()}, neighbours)
        kinds = {number: majority.kind_of((rows.get(number) or {}).get("typologyRev9"), profile, block) for number in numbers}
    if args.override_fields:
        known = plan_rules.FIELDS + (plan_rules.KEY_FIELDS if key is not None else ())
        wrong = [field for field in args.override_fields if field not in known and not (key is not None and field.startswith(plan_rules.PARAMETER))]
        if wrong:
            raise project_profile.ProfileError("--override-fields %s: a build of %s applies only %s%s (R280, R337)" % (
                ", ".join(wrong), name, ", ".join(known), " and parameter <name>" if key is not None else ""))
    # R337. A build by keyRules loads every field and counts the ones it can not place, the Round 17 build its five fields
    fields = tuple(args.override_fields) if args.override_fields else plan_rules.FIELDS if key is None else None
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
    later = set((block.get("overrideKeep") or {}).get("laterFields", {}).get(lp.get("job")) or ())
    return {"name": name, "levelProfile": lp, "rounding": plan_rules.rounding_of(settings, name, key), "hands": profile.get("hands") or {}, "openEnds": open_ends, "overrides": overrides,
            "sources": sources, "models": list(models), "keyRules": key, "roadSides": road_sides, "pairs": pairs, "kinds": kinds, "partners": partners,
            "neighbours": neighbours, "rowPairs": found if key is not None else {},
            "laterFields": [field for field in ((block.get("overrideKeep") or {}).get("laterFields", {}).get(lp.get("job")) or [])] if key is not None else [],
            "laterParameters": {field for field in later if field.startswith(plan_rules.PARAMETER)}, "facade": list(block.get("linkNameFacade") or [])}


def main(argv=None):
    parser = argparse.ArgumentParser(description="plan.json from CAD and the data book only (R208 to R210).")
    parser.add_argument("cover", help="cover.json of key_cover.py, its neighbourhood and plots are built, or nbh=<NBH> or all with --labels")
    parser.add_argument("--labels", help="location_plan/labels.csv of the snapshot, for nbh=<NBH>")
    parser.add_argument("--sales", help="the Rev 9 workbook of the snapshot, with it the corner rule of R226 sets the front")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--plots-csv", required=True, help="plots.csv of the snapshot")
    parser.add_argument("--landxml", required=True, help="the landxml folder of the snapshot")
    parser.add_argument("--site", required=True, help="testhost_<code>_site.json of the newSite job")
    parser.add_argument("--baseplan", help="R388. plots.json of the baseplan reader, the one cadReads of config names when left out, else a copy of its bytes")
    parser.add_argument("--roads", help="R388. folder with pieces.json of the road layout reader, the one cadReads names when left out, else a copy of its pieces.json")
    parser.add_argument("--analysis", help="analysis.json of cad_analysis.py, its clusters are used when no footprint blocks are given")
    parser.add_argument("--footprint-blocks", help="R236. blocks.json of the footprint drawing, the plots that share a block are the clusters, R388 the one cadReads names or a copy of its bytes")
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--floors", default=str(floor_fit.DEFAULT_PATH))
    parser.add_argument("--scratch", required=True, help="a folder outside the repo for the LandXML reader")
    parser.add_argument("--level-profile", help="R277, R278. a level profile of the rules profile, levelProfiles.default when left out")
    parser.add_argument("--overrides", help="R280. the override list in config/overrides")
    parser.add_argument("--override-source", nargs="*", help="R330. the jobs whose overrides apply, majority.overrideDefaultSources (parsons) when left out")
    parser.add_argument("--override-model", nargs="*", default=[], help="R280, R330. only the overrides of these past model files")
    parser.add_argument("--override-fields", nargs="*", choices=plan_rules.FIELDS, help="R280. only these fields of the overrides, all when left out")
    parser.add_argument("--out", required=True)
    parser.add_argument("--steps", help="R345. a folder outside the repo for the output of each step per plot, step_<step>.json")
    parser.add_argument("--steps-only", action="store_true", help="R345. write the steps that need no build and stop before it")
    args = parser.parse_args(argv)
    try:
        # R345. The step files of this run go first, so a run that fails leaves none and the parity finds them missing
        if args.steps:
            for name in PARITY_STEPS:
                parity.clear_step(args.steps, name, "python")
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        block = settings.get("siteBuild")
        if args.cover.startswith("nbh=") or args.cover == "all":
            if not args.labels:
                raise project_profile.ProfileError("nbh=<NBH> and all need --labels")
            nbh = None if args.cover == "all" else args.cover.partition("=")[2]
            cover = {"neighbourhood": nbh or "all", "plots": nbh_plots(args.labels, nbh)}
        else:
            cover = json.loads(Path(args.cover).read_text(encoding="utf-8"))
        sales = None
        if args.sales:
            sheet = settings.get("dataBook.locationPlan.sales")
            sales = corner_front.sales_rows(args.sales, sheet["sheet"], sheet["columns"]["plot"])
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        rows, number_of = book_rows(args.plots_csv)
        # R388. Each CadReader output the build reads is the one config names, of a drawing in Meters, held right before it is read
        args.baseplan = str(cad_units.read_input(settings, "baseplan", args.baseplan))
        baseplan = link_summary.load(args.baseplan)["plots"]
        # R345. A ring shapely would mend and the C# would not is refused, so the build and its C# take the same faces
        refused = face_rings.refusals(baseplan, area_tolerance(settings))
        if refused:
            raise project_profile.ProfileError("%d baseplan faces refused: %s" % (len(refused), ", ".join(refused)))
        by_label, _ = yard_rule.face_shapes(baseplan, 0)
        faces = {number_of[label]: shape for label, shape in by_label.items() if label in number_of}
        if args.steps_only and not args.steps:
            raise project_profile.ProfileError("--steps-only needs --steps, the folder of the step files (R345)")
        if args.steps:
            parity.write_step(args.steps, "faces", "python", step_scope(args.cover), face_step(cover["plots"], faces), settings.values, step_inputs(args))
        if args.steps_only:
            print("steps faces of %d plots written to %s, no plan built (R345)" % (len(cover["plots"]), args.steps))
            return 0
        # R354. A build walks the ring of a face in the order shapely gives it, which the C# gives only for a face of one ring
        one_ring_faces(baseplan)
        args.roads = str(cad_units.read_input(settings, "roads", args.roads))
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
        if not roads:
            # R355. The front of a plot is the edge nearest a curb, and corner_front.road_edges asks the nearest of no line
            raise project_profile.ProfileError("the road layout read holds no line of the curb layers analysis.roadEdgeLayers names, the front of a plot is the edge "
                                               "nearest a curb (R355)")
        if args.footprint_blocks:
            args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
        blocks = simple_blocks(footprint_check.footprint_blocks(args.footprint_blocks, settings)) if args.footprint_blocks else None
        shapes = footprint_check.block_shapes(blocks) if blocks is not None else None
        # R354. The analysis held to the baseplan read and the footprint read it was made from, not by the name of its drawing alone
        analysis = analysis_clusters(args.analysis, settings, baseplan, blocks) if args.analysis else None
        if shapes is not None:
            clusters = footprint_clusters(cover["plots"], rows, faces, shapes)
            if analysis is not None:
                clusters = joined_clusters(clusters, numbered_clusters(analysis, number_of))
        elif analysis is not None:
            clusters = analysis
        else:
            raise project_profile.ProfileError("give --analysis or --footprint-blocks for the clusters")
        job = plan_from_past.load_job(settings)
        folder, _ = plan_from_past.prototype_folders(plan_from_past.input_root(settings), job)
        # R337. A build by keyRules walks the rows of units past its own plots, so the clusters of every unit plot tell which units pair
        if args.steps:
            build_step_rules(profile, args.level_profile)
        everything, by_keys = [], plan_rules.key_rules_of(plan_rules.level_profile(profile, plan_rules.profile_name(profile, args.level_profile))) is not None
        if by_keys:
            units = [number for number, row in rows.items() if row.get("typologyRev9") and number in faces]
            # A face with two plan numbers, such as 476|477, is no plot of its own and leaves its cluster, as majority.py takes them
            everything = site_clusters(units, rows, faces, shapes, analysis, number_of)
        if args.steps:
            write_build_step(args, settings, "clusters", cluster_step(cover["plots"], everything))
        # R277. Every build follows a level profile, the named one or levelProfiles.default
        rules = rule_inputs(args, settings, profile, cover["plots"], rows, faces, roads, sales, everything)
        # R339, R340. The road surfaces of the one setting majority.py measures the levels on
        args.road_names = plan_rules.road_names(block, rules["levelProfile"])
        surfaces = load_roads([Path(args.landxml) / (name + ".xml") for name in args.road_names], args.scratch)
        # R337. A build by keyRules takes the clusters of every unit plot, the site as the keys were measured on (R271)
        trace = {}
        files = folder_files(folder, profile)
        plan = build(cover["plots"], rows, number_of, faces, roads, everything if by_keys else clusters, profile, settings,
                     prototype_offsets.plot_frame(prototype_offsets.load(args.offsets)), files, folder,
                     site_report(args.site), surfaces, floor_fit.load(args.floors), cover["neighbourhood"], sales, by_keys or bool(args.footprint_blocks), rules, trace=trace)
        if args.steps:
            write_build_step(args, settings, "fronts", front_step(cover["plots"], trace["front"], surfaces, trace["snap"]))
            hands = hand_step(cover["plots"], rows, rules, trace, files, profile, settings.get("majority"))
            write_build_step(args, settings, "hands", hands)
            write_build_step(args, settings, "linkFrame", link_step(cover["plots"], rows, trace, hands, rules["keyRules"], files, profile),
                             {"rotationDeg": plan_from_past.plan_rotation})
        plan_from_past.write_plan(out, plan)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("plan_from_book: " + str(error), file=sys.stderr)
        return 2
    for line in summary_lines(plan["summary"], rules["name"], bool(args.level_profile)):
        print(line)
    print(out)
    return 0


def summary_lines(summary, name, named):
    """R337. The lines main prints: the profile, what the build applied of the overrides and what it left with why, the fields the
    override list leaves to 3c apart from what the build applied of them, the steps, the yard and the plots."""
    rules = summary.get("rules") or {}
    applied, left = rules.get("applied") or {}, rules.get("overridesNotApplied") or {}
    lines = ["level profile %s (R277), %s" % (name, "named" if named else "the default of the rules profile")]
    if rules.get("keyRules"):
        lines.append("built by keyRules of %s (R337)" % name)
    lines.append("overrides applied on written plots %s, of plots outside the build that moved their cluster %s (R280, R337)"
                 % (applied or "none", rules.get("appliedOutsideBuild") or "none"))
    for field, whys in (rules.get("overridesNotAppliedWhy") or {}).items():
        for why, found in whys.items():
            lines.append("override %s left on %d plots, %s, such as %s" % (field, found["plots"], why, " ".join(found["first"][:5])))
    later = rules.get("laterFields") or []
    if later:
        lines.append("fields the override list keeps for 3c (majority.overrideKeep.laterFields): %s, of them applied %s, left %s" % (
            ", ".join(later), ", ".join("%s %d" % (field, applied[field]) for field in later if applied.get(field)) or "none",
            ", ".join("%s %d" % (field, left[field]) for field in later if left.get(field)) or "none"))
    if rules.get("steps"):
        lines.append("steps and %s by %s%s" % (", ".join(rules["steps"]["parameters"]), rules["steps"]["from"],
                                                " (R340)" if rules["steps"]["from"].startswith("keyRules") else ", keyRules codes no steps (R337)"))
    for zone, found in (rules.get("yardNotMeasured") or {}).items():
        lines.append("yard of %s on %d plots not measured on a key: %s (R337)" % (zone, found["plots"], found["why"]))
    lines.append("plots written %d of %d, missing %s" % (summary["written"], summary["plots"], summary["missingByKey"]))
    if summary.get("skipped"):
        lines.append("plots the skip list of the override list leaves out %d: %s (R386)" % (len(summary["skipped"]), " ".join(summary["skipped"][:10])))
    lines.append("in a cluster %d, clusters %d with %d plots" % (summary["inACluster"], summary["clusters"], summary["clusterPlots"]))
    lines.append("road surfaces of the front corners %s, plots with no road level %s" % (summary["roadSurfacesOfFrontCorners"], summary["plotsWithNoRoadLevel"]))
    for item in summary.get("noRoadLevel") or []:
        lines.append("plot %s: no road level at the front corners %s, tried %s, %s" % (item["plot"], ", ".join(item["corners"]) or "none",
                                                                                     ", ".join(item["tried"]) or "nothing", item["reason"]))
    if summary.get("noRoadLevel"):
        lines.append("the other sources of the Roads folder give no road level there (siteBuild.roadFolder, R344): %s"
                     % " | ".join("%s: %s" % (name, why) for name, why in (summary.get("roadFolderNotTaken") or {}).items()))
    lines.append("front level against plots.csv %s, front rule %s" % (summary["frontLevelAgainstPlotsCsv"], summary["frontRule"]))
    lines.append("floors %s, types %s" % (summary["floorsFound"], summary["floorTypes"]))
    return lines


if __name__ == "__main__":
    sys.exit(main())
