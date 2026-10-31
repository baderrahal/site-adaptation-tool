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
The clusters are the plots that share one Building Footprint block, a duplex pair or a townhouse row (R236), and since Q089
no cluster of a CAD analysis is joined to them (R354).
R337: a level profile with keyRules, parsons2024Rev6, builds the hands, link origin, ramp class, yard per zone, yard outline and
link parameters by the rules measured on its answer keys and places their overrides, the Round 17 tables only for native2022.
"""
import argparse
import csv
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Polygon
from shapely.strtree import STRtree

import c3d_connect
import cad_analysis
import check_rows
import cad_units
import cad_links
import corner_front
import common
import data_book_retest
import cluster_proposals
import design_basis
import face_rings
import floor_fit
import footprint_check
import input_guard
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
import plan_walls
import ramp_outline
import surface_lookup
import value_status
import yard_rule

ESTIMATE = "estimate"
LOCATION = "locationPlan"
OVERRIDE = "override"
KEY_RULES = "keyRules"
# R345. The steps of the parity harness this builder writes with --steps, the build steps of R354 to R363 and R401 after the faces
PARITY_STEPS = ("faces", "clusters", "fronts", "hands", "linkFrame", "levels", "floors", "walls", "overrides", "parameters", "status", "checkRows", "summary")
BUILD_STEPS = ("clusters", "fronts", "hands", "linkFrame", "levels", "floors", "walls", "overrides", "parameters", "status", "checkRows", "summary")
FOOTPRINT = "footprint"
# R354, Q089. The one line a build and its C# refuse an analysis with, as a build takes the footprint clusters alone
ANALYSIS_REFUSED = "the analysis is no input of a build since Q089, a build takes the footprint clusters of R236 alone (R354)"
# R408. A build under rev6 gives the levels of its El @ Entry by the level rules of keyRules
REV6_NO_LEVELS = "designBasis.mode rev6 takes the levels of El @ Entry by the level rules of keyRules, the level profile %s codes no ffl (R408)"
# R337. The source of a parameter that reads the steps, the stepBands of the level profile when keyRules codes no steps
STEP_BANDS = "levelProfileStepBands"
sourced = plan_from_cad.sourced


def csv_rows(path, columns=()):
    """R345. The rows of a CSV file of the data book as CsvRows.Read of the C# reads them: utf-8-sig drops the byte order mark a file saved
    by Excel starts with, newline="" keeps a line end inside a quoted field as it is, and strict refuses an unclosed quote, a text after a
    closing quote and a field past the limit of csv with the line of csv, where Python took the rest of the file into one field and kept
    the rows before it (G2 of the fourth review of PR 55). With columns named, a header that lacks one and a file of no row are refused,
    where an empty labels.csv gave a build of no plot and a plots.csv of two byte order marks no row (G1 of the fourth review of PR 55)."""
    path = Path(path)
    try:
        with open(path, encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle, strict=True)
            rows = list(reader)
            header = reader.fieldnames or []
    except csv.Error as error:
        raise project_profile.ProfileError("%s is no CSV as Python reads it: %s (R345)" % (path.name, error)) from error
    if columns:
        missing = [column for column in columns if column not in header]
        if missing:
            raise project_profile.ProfileError("%s has no column %s in its header (R345)" % (path.name, ", ".join(missing)))
        if not rows:
            raise project_profile.ProfileError("%s holds no row under its header (R345)" % path.name)
    return rows


# R345, G1. The columns of plots.csv and labels.csv a build reads, CsvRows.PlotsColumns and LabelsColumns of the C#
PLOTS_COLUMNS = ("locationPlanNumber", "baseplanLabel")
LABELS_COLUMNS = ("kind", "number")


def book_rows(plots_csv):
    """The rows of plots.csv by location plan number, and the number of each baseplan label (csv_rows, R345)."""
    rows = [row for row in csv_rows(plots_csv, PLOTS_COLUMNS) if row.get("locationPlanNumber")]
    return {row["locationPlanNumber"]: row for row in rows}, {row["baseplanLabel"]: row["locationPlanNumber"] for row in rows if row.get("baseplanLabel")}


def footprint_clusters(numbers, rows, faces, block_index):
    """R236. The plots of the numbers that share one Building Footprint block of the name the data book wired to them, the block
    that shares the most of each face, two or more to a cluster, each with the block name as its code."""
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


def numbered_clusters(clusters, number_of):
    """R208. Clusters given by baseplan label with their plots as location plan numbers. A plot with no number leaves the cluster."""
    found = []
    for cluster in clusters:
        plots = [number_of[label] for label in cluster["plots"] if label in number_of]
        if plots:
            found.append({**cluster, "plots": plots})
    return found


def site_clusters(units, rows, faces, shapes):
    """R236, R354. The clusters a build by keyRules takes, the site as the keys were measured on (R271): the footprint clusters over every
    unit plot alone since Q089, a face of two plan numbers such as 476|477 left out of its cluster as majority.py leaves it, each of two
    plots or more with its source, footprint."""
    found = []
    for cluster in footprint_clusters(units, rows, faces, shapes):
        plots = [plot for plot in cluster["plots"] if plot.isdigit()]
        if len(plots) > 1:
            found.append({**cluster, "plots": plots, "source": FOOTPRINT})
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
    the table of hand villa, cluster or alone it takes (plan_rules.hand_table, Q098) and its rule, the value of its factor, the file the plot links and the mirror flag the table of that file
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
        table = plan_rules.hand_table(key, unit_class, files.get(unit)) if unit else None
        hand, rule = plan_rules.key_hand(key, table, files.get(unit), factors) if unit else (None, None)
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
            "rowPairSideOrBlockMirrorOpenEnd": factors["rowPairSideOrBlockMirrorOpenEnd"], "handTable": table, "handRule": rule, "factor": factors.get(rule) if rule else None,
            "linkFile": files.get(unit) if unit else None, "mirrored": hand}
        # R411. The proposal the build made under rev6, null on a build of the past job or with designBasis.clusters.propose false
        proposal = (trace.get("proposals") or {}).get(number) or {}
        found[number].update(proposedCluster=proposal.get("cluster"), proposedMirror=proposal.get("mirrored"), proposalReason=proposal.get("reason"))
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


# R359, R358. The lines the steps levels and floors and their C# refuse a level profile with, as the C# builds by keyRules alone
LEVELS_NO_FFL = ("the steps levels and floors of the parity harness are a build by the level rules of keyRules, keyRules of %s codes no ffl, the C# builds the "
                 "levels by keyRules alone (R359)")
YARD_GROUPS = ("the step floors of the parity harness takes the yard outlines of keyRules.yardOutline groups, keyRules of %s codes fittedOutline, whose outlines "
               "lie in config/floor_outlines.json, which the C# does not read (R358)")


def level_step(numbers, rows, trace, rules, profile):
    """R359. The step levels of the parity harness: per plot of the scope the levels the level rules of keyRules give it, as the build
    takes them before an override of the list moves them, which step 12 applies (R360), the values plan_rules.key_level_rows and
    rule_levels give: the unit, the other unit of its row pair (plan_rules.row_partners), the piece that shares its FFL and the road at
    front, held in the step alone, and El @ Entry from the one unrounded mean of the road levels at its two front corners, the FFL of the
    piece unrounded, its SSL and height from the FFL as written, the steps of that height at 4 decimals and the level parameters, each raw
    the value just before its last rounding, null where the plot has none."""
    key, lp = rules["keyRules"], rules["levelProfile"]
    if not plan_rules.key_levels_of(key):
        raise project_profile.ProfileError(LEVELS_NO_FFL % rules["name"])
    ground = profile.metres("levels.ground_ssl")
    base_rule = key.get("baseHeight")
    rev6 = design_basis.is_rev6(rules.get("basis"))
    entries, level_rows, front = trace.get("entries") or {}, trace.get("levelRows") or {}, trace.get("front") or {}
    found = {}
    for number in numbers:
        item, entry, row = front.get(number) or {}, entries.get(number), level_rows.get(number)
        made = {"unit": (rows.get(number) or {}).get("typologyRev9") or None, "partner": rules["partners"].get(number), "piece": None, "roadAtFront": None,
                "entry": None, "ffl": None, "ssl": None, "height": None, "steps": None, "parameters": None, "doorSource": None, "slopePercent": None,
                "splitTest": None}
        if item.get("front") is not None:
            made["roadAtFront"] = plan_rules.road_at_front(item["front"])
        if rev6 and made["unit"] is not None:
            # R408. The source of El @ Entry under rev6, the input, and the slope it leaves, the result
            made["doorSource"] = rules["basis"]["door"]["source"]
        if entry is not None:
            made["entry"] = trace["entryRaw"][number]
            if rev6:
                made["slopePercent"] = trace["slopes"][number]
        if row is not None:
            top = plan_rules.piece_top([(plan_rules.rule_entry(entries.get(other)), (entries.get(other) or {}).get("unit")) for other in row["piece"]], profile, base_rule)
            if common.fixed(top, plan_rules.FFL_DECIMALS) != row["fflRule"]:
                raise ValueError("the step levels gives plot %s the FFL %r and the build took %r (R359)" % (number, common.fixed(top, plan_rules.FFL_DECIMALS), row["fflRule"]))
            ffl, rule = row["fflRule"], entry["rule"]
            made.update(piece=list(row["piece"]), ffl=top, ssl=ffl + ground, height=ffl - rule,
                        steps=plan_rules.steps_by_rule(key["steps"]["rule"], common.fixed(ffl - rule, plan_rules.ENTRY_DECIMALS), entry["unit"], profile, lp, base_rule),
                        parameters=plan_rules.level_parameters(key, ffl, rules["laterParameters"]))
            # R414. The result of the split test of its cluster, the mark of a double wall, null where no test ran
            made["splitTest"] = row.get("splitTest")
        found[number] = made
    return found


FLOOR_FIELDS = ("unit", "generic", "rampOutline", "rampZ", "rampClass", "rampType", "yardDepth", "yardUnit", "yardWidth", "yardType", "yardOutline", "lacks",
                "yardGrade")
# R408. The lack of a ramp whose slope no band of the ramp types holds, under rev6
NO_BAND = "no ramp type for a slope of %r percent in the bands of rampTypes (R408)"


def floor_step(numbers, rows, trace, links, rules, profile, step):
    """R358. The step floors of the parity harness: per unit plot of the scope the floors the rules of keyRules give it, as the build takes
    them before an override of the list moves them (R360): the Generic floor on the whole face as shapely gives its rings, the ramp of R127
    on the front of R355 with the Z of its four corners by the chain of ramp_heights, the front pair at the road and the back pair from El @
    Entry by the rule as written at 4 decimals, its class of keyRules.rampSlope and the ramp type rampTypes names for it, and the yard of
    keyRules.yard of its zone on the outline keyRules.yardOutline fits its unit, mirror flag and plot width, placed by the link of R356 as the
    plan writes it, the width a lookup by floor_fit.width_key, with what the floors lack and why."""
    key, lp = rules["keyRules"], rules["levelProfile"]
    # The back corners take El @ Entry of the level rules, which the C# gives by keyRules alone
    if not plan_rules.key_levels_of(key):
        raise project_profile.ProfileError(LEVELS_NO_FFL % rules["name"])
    if key["yardOutline"]["rule"] != "fittedOutlineRefit":
        raise project_profile.ProfileError(YARD_GROUPS % rules["name"])
    outlines = plan_rules.key_outlines(key, {"outlines": {}})
    rev6 = design_basis.is_rev6(rules.get("basis"))
    entries, front = trace.get("entries") or {}, trace.get("front") or {}
    found = {}
    for number in numbers:
        row = rows.get(number) or {}
        unit = row.get("typologyRev9") or None
        made = dict.fromkeys(FLOOR_FIELDS)
        made.update(unit=unit, lacks=[])
        found[number] = made
        if unit is None:
            continue
        item, entry, link = front.get(number) or {}, entries.get(number), links.get(number) or {}
        builder = plan_from_past.PlotBuilder(number)
        face = item.get("face")
        if face is not None:
            made["generic"] = [[list(point[:2]) for point in list(ring.coords)[:-1]] for ring in [face.exterior] + list(face.interiors)]
        if item.get("corners") is not None:
            made["rampOutline"] = [list(corner) for corner in item["corners"]]
        if item.get("front") is not None and entry is not None:
            made["rampZ"] = ramp_heights(item["front"], plan_rules.rule_entry(entry))
        if rev6:
            # R410. The grade of the back yard by the manual and no yard floor. R408: the ramp type of the band of the slope, the result
            made["yardGrade"] = design_basis.yard_grade(unit, profile)
            slope = trace["slopes"].get(number) if entry is not None else None
            made["rampType"] = None if slope is None else floor_fit.type_of(slope, trace["bands"])
            if slope is not None and made["rampType"] is None:
                builder.lack("floors.ramp", NO_BAND % slope)
            made["lacks"] = builder.missing
            continue
        below, _ = plan_rules.key_yard(key, row.get("zone") or "")
        made["yardDepth"] = below
        if below is not None and link.get("insert") is not None:
            mirrored = link["mirrored"]
            yard_unit = unit if any(group["unit"] == unit for group in outlines["outlines"].get("yard") or []) else (
                ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file") or unit)
            width = floor_fit.width_key(item["frame"]["length"], step)
            group = floor_fit.outline_for(outlines, "yard", yard_unit, mirrored, width)
            made.update(yardUnit=yard_unit, yardWidth=width)
            if group is None:
                builder.lack("floors.yard", "no fitted outline for " + yard_unit + (" mirrored" if mirrored else " not mirrored") + " on a plot " + str(width) + " m wide")
            else:
                insert, rotation = plan_from_past.point(link["insert"]), plan_from_past.plan_rotation(link["rotationDeg"])
                made.update(yardType=group["type"], yardOutline=[[list(point) for point in floor_fit.to_world(loop, insert, rotation, mirrored)] for loop in group["loops"]])
        ramp_class = plan_rules.key_ramp_class(key, unit)
        made.update(rampClass=ramp_class, rampType=lp["rampTypes"].get(ramp_class) if ramp_class is not None else None)
        if made["rampType"] is None:
            builder.lack("floors.ramp", "keyRules.rampSlope and rampTypes give no ramp type for " + unit)
        made["lacks"] = builder.missing
    return found


OVERRIDE_FIELDS = ("unit", "skip", "written", "applied", "notApplied", "mirrored", "insert", "rotationDeg", "roadAtFront", "entry", "ffl", "ssl", "height", "steps", "rampZ",
                   "rampType", "rampOutline", "yardZ", "yardOutline", "slopePercent")


def held_plan(number, name, made, written):
    """R360. A value of the step overrides as the plan writes it, refused when the plan writes another."""
    if made != written:
        raise ValueError("the step overrides gives plot %s the %s %r and the plan writes %r (R360)" % (number, name, made, written))


def override_step(numbers, rows, trace, hands, files, rules, profile, step):
    """R360. The step overrides of the parity harness: per plot of the scope what the build did with each override of the list, the Outcomes of
    the build, applied or left with why, whether the plan writes the plot and the skip reason of R386, and the values after the overrides as
    the build places them: the mirror flag, the link of R356 on that hand with the insert moved by its gap in the link axes and the rotation
    turned by its gap, El @ Entry with its entryM gap, the FFL with its heightM gap, SSL, the height and the steps or its override from them,
    and for a plot the plan writes the ramp corners of Step 8 with the rampFrontM gap, the ramp type of a rampType override and the yard of
    R358 on the link after the overrides at the FFL less its depth, each raw the value just before its last rounding. Each value the plan
    writes is held to it, a level of a plot not written to the level rows of the build."""
    key, lp = rules["keyRules"], rules["levelProfile"]
    if not plan_rules.key_levels_of(key):
        raise project_profile.ProfileError(LEVELS_NO_FFL % rules["name"])
    if key["yardOutline"]["rule"] != "fittedOutlineRefit":
        raise project_profile.ProfileError(YARD_GROUPS % rules["name"])
    overrides = rules["overrides"]
    skip = getattr(overrides, "skip", {})
    section = plan_rules.key_frame_section(key)
    outlines = plan_rules.key_outlines(key, {"outlines": {}})
    ground = profile.metres("levels.ground_ssl")
    base_rule = key.get("baseHeight")
    rev6 = design_basis.is_rev6(rules.get("basis"))
    entries, level_rows, front = trace.get("entries") or {}, trace.get("levelRows") or {}, trace.get("front") or {}
    built, records = trace.get("built") or {}, trace.get("records") or {}
    found = {}
    for number in numbers:
        row = rows.get(number) or {}
        unit = row.get("typologyRev9") or None
        record, plot = records.get(number), built.get(number) or {"entry": {}, "written": False}
        made = dict.fromkeys(OVERRIDE_FIELDS)
        made.update(unit=unit, skip=skip.get(number), written=plot["written"], applied=None if record is None else list(record["applied"]),
                    notApplied=None if record is None else dict(record["notApplied"]))
        found[number] = made
        if unit is None or number in skip:
            continue
        mine = overrides.get(number) or {}
        item, entry, level = front[number], entries.get(number), level_rows.get(number)
        mirrored = mine["mirrored"] if isinstance(mine.get("mirrored"), bool) else hands[number]["mirrored"]
        keo = ((profile.get("typologies") or {}).get(unit) or {}).get("keo_file")
        fit_unit = unit if unit in section["units"] else keo
        builder = plan_from_past.PlotBuilder(number)
        plan_from_cad.link_values(builder, unit, item["face"], item["frame"], trace["roles"].get(number), section, files, mirrored, fit_unit)
        turned = None
        if builder.has("insert"):
            frame = item["frame"]
            insert = list(cad_links.predicted_origin(frame, mirrored, section["units"].get(fit_unit or unit)))
            rotation = cad_links.rotation_of(frame, mirrored)
            gap = mine["insert"].get("gapToRuleM") if isinstance(mine.get("insert"), dict) else None
            if gap is not None:
                # moved_link: the insert as the rule writes it, moved by the gap along the link X and Y axes of the hand
                axis_x, axis_y = cad_links.link_axes(frame, mirrored)
                point = plan_from_past.point(insert)
                insert = [point[0] + gap[0] * axis_x[0] + gap[1] * axis_y[0], point[1] + gap[0] * axis_x[1] + gap[1] * axis_y[1]]
            gap = mine["rotationDeg"].get("gapToRuleDeg") if isinstance(mine.get("rotationDeg"), dict) else None
            if gap is not None:
                rotation = plan_from_past.plan_rotation(rotation) + gap
                turned = open_ramp(item["face"], frame, plan_from_past.plan_rotation(rotation), mirrored, item.get("depth"), trace["surfaces"], trace["snap"])
            made.update(mirrored=mirrored, insert=insert, rotationDeg=rotation)
        if entry is not None:
            # R364. The unrounded mean of the two front corners, which no override moves
            made["roadAtFront"] = plan_rules.road_at_front(item["front"])
            gap = plan_rules.gap_of(mine.get("entryM"))
            made["entry"] = trace["entryRaw"][number] if gap is None else entry["rule"] + gap
            if rev6:
                # R408. The slope of El @ Entry after its override, the result the plan writes
                made["slopePercent"] = trace["slopes"][number] if gap is None else design_basis.slope_of(made["entry"], item["front"], item["depth"])
        if level is not None:
            gap = plan_rules.gap_of(mine.get("heightM"))
            if gap is None:
                pieces = [(plan_rules.rule_entry(entries.get(other)), (entries.get(other) or {}).get("unit")) for other in level["piece"]]
                ffl_raw = plan_rules.piece_top(pieces, profile, base_rule)
            else:
                ffl_raw = level["fflRule"] + gap
            ffl = common.fixed(ffl_raw, plan_rules.FFL_DECIMALS)
            height = ffl - entry["entry"]
            count = plan_rules.whole_number(mine.get("steps"))
            made.update(ffl=ffl_raw, ssl=ffl + ground, height=height, steps=count if count is not None else plan_rules.steps_by_rule(
                key["steps"]["rule"], common.fixed(height, plan_rules.ENTRY_DECIMALS), entry["unit"], profile, lp, base_rule))
            for name, decimals in (("entry", plan_rules.ENTRY_DECIMALS), ("ffl", plan_rules.FFL_DECIMALS), ("ssl", plan_rules.FFL_DECIMALS), ("height", plan_rules.ENTRY_DECIMALS)):
                if common.fixed(made[name], decimals) != level[name]:
                    raise ValueError("the step overrides gives plot %s the %s %r and the build took %r (R360)" % (number, name, common.fixed(made[name], decimals), level[name]))
            if made["steps"] != level["steps"]:
                raise ValueError("the step overrides gives plot %s the steps %r and the build took %r (R360)" % (number, made["steps"], level["steps"]))
        if not plot["written"]:
            continue
        opened = turned is not None and turned["front"] is not None
        ramp_front, ramp_corners = (turned["front"], turned["corners"]) if opened else (item["front"], item["corners"])
        gap = None if opened else plan_rules.gap_of(mine.get("rampFrontM"))
        ramp_front = ramp_front if gap is None else [z + gap for z in ramp_front]
        if rev6 and (gap is not None or opened):
            # R408. The slope of the front corners after the rampFrontM override or of the open edge the ramp moved to (Q116), which the plan writes
            made["slopePercent"] = design_basis.slope_of(made["entry"], ramp_front, item["depth"])
        # R408. Under rev6 a ramp with no rampType override takes the type of the band of its slope after the overrides, never keyRules.rampSlope
        ramp_class = mine.get("rampType") or (None if rev6 else plan_rules.key_ramp_class(key, unit))
        kind = lp["rampTypes"].get(ramp_class) if ramp_class is not None else floor_fit.type_of(made["slopePercent"], trace["bands"]) if rev6 else None
        if kind is not None:
            made.update(rampZ=ramp_heights(ramp_front, entry["entry"]), rampType=kind, rampOutline=[list(corner) for corner in ramp_corners])
        # R410. No yard floor under rev6
        below = None if rev6 else plan_rules.key_yard(key, row.get("zone") or "")[0]
        if below is not None:
            yard_unit = unit if any(group["unit"] == unit for group in outlines["outlines"].get("yard") or []) else keo or unit
            group = floor_fit.outline_for(outlines, "yard", yard_unit, mirrored, floor_fit.width_key(item["frame"]["length"], step))
            if group is not None:
                origin, turn = plan_from_past.point(made["insert"]), plan_from_past.plan_rotation(made["rotationDeg"])
                made.update(yardZ=ffl - below, yardOutline=[[list(point) for point in floor_fit.to_world(loop, origin, turn, mirrored)] for loop in group["loops"]])
        written = plot["entry"]
        held_plan(number, "mirror flag", made["mirrored"], written["mirrored"]["value"])
        held_plan(number, "insert", plan_from_past.point(made["insert"]), written["insert"]["value"][:2])
        held_plan(number, "rotation", plan_from_past.plan_rotation(made["rotationDeg"]), written["rotationDeg"]["value"])
        for name, decimals in (("entry", plan_rules.ENTRY_DECIMALS), ("ffl", plan_rules.FFL_DECIMALS), ("ssl", plan_rules.FFL_DECIMALS), ("height", plan_rules.ENTRY_DECIMALS)):
            held_plan(number, name, common.fixed(made[name], decimals), written["levels"][name]["value"])
        held_plan(number, "steps", made["steps"], written["levels"]["steps"]["value"])
        held_plan(number, "road at front", common.fixed(made["roadAtFront"], plan_rules.ENTRY_DECIMALS), written["levels"]["roadAtFrontM"]["value"])
        ramp, yard = written["floors"].get("ramp"), written["floors"].get("yard")
        held_plan(number, "ramp corner Z", None if made["rampZ"] is None else [common.r3(z) for z in made["rampZ"]],
                  None if ramp is None else [corner[2] for corner in ramp["corners"]["value"]])
        held_plan(number, "ramp type", made["rampType"], None if ramp is None else ramp["type"]["value"])
        if rev6:
            held_plan(number, "ramp slope", None if ramp is None else common.r3(made["slopePercent"]), None if ramp is None else ramp["slopePercent"]["value"])
        held_plan(number, "yard outline", None if made["yardOutline"] is None else [plan_from_past.point(point) for loop in made["yardOutline"] for point in loop],
                  None if yard is None else [point for loop in yard["outline"]["value"]["loops"] for point in loop["points"]])
        held_plan(number, "yard Z", None if made["yardZ"] is None else common.r3(made["yardZ"]), None if yard is None else yard["z"]["value"])
    return found


PARAMETER_FIELDS = ("unit", "written", "parameters", "sources", "lacks", "linkName")


def parameter_step(numbers, rows, trace, rules):
    """R361. The step parameters of the parity harness: per plot of the scope the link parameters the build gives it, as the plan writes them
    for a plot it writes and as the missing list holds it for one it does not, the text of each by plan_rules.key_parameters on the mirror flag,
    the steps and the FFL after the overrides, then the parameter overrides of the list, the source of each, keyRules or override, the
    parameters the rules give no value with the lack of the plan, and the link Name of R107, the text of RRE_ZZ_SP_RVT-Name (Q099)."""
    skip = getattr(rules["overrides"], "skip", {})
    built = trace.get("built") or {}
    found = {}
    for number in numbers:
        unit = (rows.get(number) or {}).get("typologyRev9") or None
        plot = built.get(number) or {"entry": {}, "written": False, "missing": []}
        made = dict.fromkeys(PARAMETER_FIELDS)
        made.update(unit=unit, written=plot["written"])
        found[number] = made
        if unit is None or number in skip:
            continue
        parameters = plot["entry"].get("parameters") or {}
        made.update(parameters={name: item["value"] for name, item in parameters.items()}, sources={name: item["source"] for name, item in parameters.items()},
                    lacks=[name[len("parameters."):] for name in plot["missing"] if name.startswith("parameters.")],
                    linkName=(plot["entry"].get("linkName") or {}).get("value"))
    return found


STATUS_FIELDS = ("unit", "written", "fields")


def status_step(numbers, rows, trace):
    """R401. The step status of the parity harness: per plot of the scope each sourced value of the plot the plan writes by its field, with its
    source, its status and the key of the detail that gives it, and no field for a plot the plan does not write, and the table statusFrom of
    the details by key, as the plan writes them once (Q107)."""
    built, details = trace.get("built") or {}, trace.get("statusFrom") or {}
    found, used = {}, set()
    for number in numbers:
        plot = built.get(number) or {"entry": {}, "written": False}
        fields = {field: {"source": item["source"], "status": item["status"], "statusFrom": item["statusFrom"]}
                  for field, item in value_status.fields_of(plot["entry"]).items()} if plot["written"] else None
        used.update(item["statusFrom"] for item in (fields or {}).values())
        found[number] = {"unit": (rows.get(number) or {}).get("typologyRev9") or None, "written": plot["written"], "fields": fields}
    return found, {"statusFrom": {key: details[key] for key in sorted(used)}}


SUMMARY_FIELDS = ("unit", "written")
# R365. The entries of the table summary of step 15, the top line of the level Excel, the box Rules applied of the report and the status of
# R401, then the conflicts of the plan (Q116), each from the summary block or under its rules
SUMMARY_TABLE = ("frontRule", "roadSurfacesOfFrontCorners", "floorsFound", "floorTypes", "handRules", "overridesNotApplied", "steps", "statusByField",
                 "assumedByField", "statusPlots", "conflicts")
SUMMARY_TOP = ("frontRule", "roadSurfacesOfFrontCorners", "floorsFound", "floorTypes")
# R408. The lists of a build under rev6 in the summary block, null in the table of a build of the past job, whose plan holds neither
SUMMARY_REV6 = ("noDoorLevel", "slopeOutsideManual")


def summary_step(numbers, rows, plan):
    """R365. The step summary of the parity harness: per plot of the scope its unit and whether the plan writes it, and the table summary
    with the counts and names of step 15 as the plan writes them, one source, which parity.py compares entry by entry exact."""
    written = {entry["plot"] for entry in plan["plots"]}
    found = {number: {"unit": (rows.get(number) or {}).get("typologyRev9") or None, "written": number in written} for number in numbers}
    summary, rules = plan["summary"], plan["summary"].get("rules") or {}
    table = {name: summary[name] if name in SUMMARY_TOP else plan["conflicts"] if name == "conflicts" else rules.get(name) for name in SUMMARY_TABLE}
    table.update({name: summary.get(name) for name in SUMMARY_REV6})
    return found, {"summary": table}


def status_line(found):
    """R401. The line of summary_lines: the plots that hold a value of each status, those with an assumed value and the fields of those."""
    plots = found.get("statusPlots") or {}
    counts = ", ".join("%s %d" % (name, count) for name, count in plots.items() if name != value_status.ASSUMED)
    return "plots by the status of their values (R401): %s, an assumed value %d, in the fields %s" % (
        counts or "none", plots.get(value_status.ASSUMED, 0), " ".join(found.get("assumedByField") or {}) or "none")


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


def ramp_heights(front, entry):
    """R210, R358. The Z of the four ramp corners, raw: the front pair the road levels given, the back pair El @ Entry plus its front corner
    less the mean of the two, so it runs parallel to the front with its mean at El @ Entry, the El @ Entry the plan writes."""
    middle = (front[0] + front[1]) / 2.0
    return list(front) + [entry + front[1] - middle, entry + front[0] - middle]


def ramp_floor(builder, frame, corners, front, entry, slope, inputs, kind=None):
    """R210. The ramp of the Rev 6 outline, the front corners at the road, the back pair parallel with its mean at El @ Entry.
    R278: the ramp type the level profile names, else the type of its slope band."""
    heights = ramp_heights(front, entry)
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
# Q116. key_levels measures rampFront against the road of the front of R355, which the open edge of a turned plot does not hold
OPEN_EDGE_GAP = "measured on the front of R355, the ramp moved to the open edge (Q116)"
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
        # Q107. The overrides of each plot in the order the build applied them, so a status names the one that moved a value
        self.order = defaultdict(list)

    def applied(self, number, field):
        if number in self.numbers:
            self.done[number].add(field)
            if field not in self.order[number]:
                self.order[number].append(field)
        else:
            self.outside[field] += 1

    def in_order(self, number, applied):
        """Q107. The applied overrides of a plot, those Outcomes.plot gives, in the order the build applied them."""
        return [field for field in self.order[number] if field in set(applied)]

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


def built_entry(trace, builder, written):
    """R360, R361. The entry the build made for a plot, whether the plan writes it and the keys it lacks, kept in the trace for the steps."""
    if trace is not None:
        trace["built"][builder.plot] = {"entry": builder.entry, "written": written, "missing": [found["key"] for found in builder.missing]}


def not_applied_why(records):
    """R337. The overrides a build left, by field and why, with the plots of each, so the summary says why as the plan says it."""
    found = defaultdict(lambda: defaultdict(list))
    for number, record in records.items():
        for field, why in (record or {}).get("notApplied", {}).items():
            found[field][why].append(number)
    return {field: {why: {"plots": len(plots), "first": sorted(plots, key=link_summary.sort_key)[:10]} for why, plots in sorted(whys.items())}
            for field, whys in sorted(found.items())}


def key_levels(entries, key, roles, partners, rows, overrides, outcomes, numbers, front, profile, lp, basis=None, slopes=None):
    """R340. FFL, SSL, height and steps by the level rules of keyRules, as R339 measured them: one FFL per piece at its highest El @ Entry
    plus the base of that unit to the mm, then the heightM gap moves the FFL onto the key with its SSL, height and steps, and a steps
    override sets the steps. An override of a plot with no FFL is left with why. R414: under rev6 with designBasis.split.test the pieces
    of the split test replace those of its clusters, each row of them marked with splitTest."""
    units = {number: (rows.get(number) or {}).get("typologyRev9") for number in front}
    pieces = plan_rules.key_pieces(key, list(entries), roles, partners, units)
    marks = {}
    if design_basis.splits(basis):
        found, marks = plan_rules.split_test(entries, slopes, roles, basis["split"], key, profile, lp)
        pieces.update(found)
    level_rows = plan_rules.key_level_rows(entries, pieces, profile, lp, key)
    for number, mark in marks.items():
        if number in level_rows:
            level_rows[number]["splitTest"] = mark
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
    found.update(plan_rules.level_parameters(key, row["fflRule"], rules["laterParameters"]))
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
    # R407. The design basis of the build, read once by rule_inputs, rev6 the manual, the past job the build of before round 4i
    basis = None if rules is None else rules.get("basis")
    rev6 = design_basis.is_rev6(basis)
    if rev6 and not by_levels:
        raise project_profile.ProfileError(REV6_NO_LEVELS % rules["name"])
    if key is not None:
        frame_section = plan_rules.key_frame_section(key)
        missing_zones = defaultdict(list)
        for number in numbers:
            row = rows.get(number) or {}
            # R410. Under rev6 no yard floor is placed, so keyRules.yard is not read
            if not rev6 and row.get("typologyRev9") and (row.get("zone") or "") not in key["yard"]["table"]:
                missing_zones[row.get("zone") or "no zone"].append(number)
        if missing_zones:
            raise project_profile.ProfileError("keyRules.yard holds no yard for %s, an area missing from the table is refused and never given another value (R337)"
                                               % ", ".join("%s (%d plots, %s)" % (zone, len(plots), " ".join(plots[:5])) for zone, plots in sorted(missing_zones.items())))
    # R362. The wall rules of the keys, a rule no build places walls by refused before any plot is built
    wall_rules = plan_walls.wall_rules(key, rules["levelProfile"], rules["name"], profile, settings.get("keyPlans.wallEdgeM")) if key is not None else None
    mine = [cluster for cluster in (clusters if numbered else numbered_clusters(clusters, number_of)) if set(cluster["plots"]) & set(numbers)]
    # R340. The other unit of a row pair shares the FFL, so its El @ Entry is worked out as well
    members = sorted({plot for cluster in mine for plot in cluster["plots"]} | set(numbers) | {partners[number] for number in numbers if number in partners},
                     key=link_summary.sort_key)
    front = fronts(members, rows, faces, roads, limits, surfaces, snap, profile, block.get("corner") if sales is not None else None, sales)
    if trace is not None:
        trace.update(front=front, snap=snap, surfaces=surfaces)
    frames = {number: item["frame"] for number, item in front.items()}
    roles = cad_links.cluster_roles(mine, 0, {number: item["face"] for number, item in front.items() if item["face"] is not None}, frames)
    # R411. Under rev6 with designBasis.clusters.propose a cluster and a mirror flag proposed per unit plot by the manual, which set no value
    proposals = cluster_proposals.proposals(numbers, rows, rules["neighbours"], profile, key, files) if design_basis.proposes(basis) else {}
    if trace is not None:
        trace.update(roles=roles, hands={}, proposals=proposals)
    # R408. slopes the slope by the rule for the step levels, finals and ends the slope and the raw El @ Entry after the overrides for the plan
    entries, slopes, finals, ends, no_door = {}, {}, {}, {}, []
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
                end, final = value, slope
            else:
                if rev6:
                    # R408. El @ Entry the input, from the civil file or the road at front plus the slope of the unit, the slope the result
                    value, why = design_basis.door_entry(basis, number, item["unit"], item["front"], item["depth"])
                    if value is None:
                        if number in in_build:
                            no_door.append({"plot": number, "reason": why})
                        if "entryM" in overrides.get(number, {}):
                            outcomes.left(number, "entryM", why)
                        continue
                    slope = design_basis.slope_of(value, item["front"], item["depth"])
                else:
                    value, slope = plan_rules.entry_of(item["front"], item["unit"], rules["levelProfile"], item["depth"])
                if trace is not None:
                    # R359. El @ Entry unrounded, the value the step levels holds its rounding to 4 decimals against
                    trace.setdefault("entryRaw", {})[number] = value
                end, final = value, slope
                if by_levels:
                    # R340. At the 4 decimals of R2, as R339 measured it, so a gap moves it onto the key exactly
                    value = rule = common.fixed(value, plan_rules.ENTRY_DECIMALS)
                gap = plan_rules.gap_of(overrides.get(number, {}).get("entryM"))
                # R340. With the level rules a gap moves the El @ Entry of its own plot only, so one of a plot outside the build moves nothing
                if gap is not None and not (by_levels and number not in in_build):
                    shifted = value + gap
                    value = common.fixed(shifted, plan_rules.ENTRY_DECIMALS) if by_levels else shifted
                    if rev6:
                        # R408. The slope the plan writes is that of the El @ Entry the override moved, raw as the step overrides holds it
                        end, final = shifted, design_basis.slope_of(shifted, item["front"], item["depth"])
                    outcomes.applied(number, "entryM")
                elif gap is None and "entryM" in overrides.get(number, {}):
                    outcomes.left(number, "entryM", NO_GAP)
            entries[number] = {"unit": item["unit"], "entry": value, **({"rule": rule} if by_levels else {})}
            slopes[number], finals[number], ends[number] = slope, final, end
        elif "entryM" in overrides.get(number, {}):
            outcomes.left(number, "entryM", "no road level at a front corner, so no El @ Entry to move")
    if rules is None:
        level_rows = plan_from_cad.plot_levels(profile, mine, entries)
    elif by_levels:
        level_rows = key_levels(entries, key, roles, partners, rows, overrides, outcomes, numbers, front, profile, rules["levelProfile"], basis, slopes)
        if trace is not None:
            # R359. The El @ Entry and the levels of each plot with the FFL by the rule kept apart, for the step levels
            trace.update(entries=entries, levelRows=level_rows)
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
    if trace is not None:
        # R408. The slope of each El @ Entry and the bands of the ramp types, for the steps floors and overrides under rev6
        trace.update(slopes=slopes, bands=inputs["bands"])
    plots, missing, lacking, surface_count, no_level, book_check, no_road = [], [], Counter(), Counter(), [], Counter(), []
    not_measured, not_measured_plots, records, hand_rules, applied_of = {}, Counter(), {}, Counter(), {}
    # Q116. The plots whose ramp moved to the open edge a rotation override turned them to face
    opened_ramps = set()
    # R401. The status of each sourced value from the block status of the rules profile, the map and the block read once per build
    statuses = value_status.table_of(settings, profile, None if rules is None else rules["name"])
    if trace is not None:
        trace["statusFrom"] = statuses.used
    if trace is not None:
        # R360, R361. What the build did with each override of each plot and the entry it built, written or not, for the steps overrides and parameters
        trace.update(records=records, built={})
    for number in numbers:
        item = front[number]
        builder = plan_from_past.PlotBuilder(number)
        unit = item["unit"]
        if number in skip:
            builder.lack("skip", "the skip list of the override list: %s (R386)" % skip[number])
            records[number] = outcomes.plot(number, False, ["skip"])
            built_entry(trace, builder, False)
            missing.append({"plot": number, "written": False, "missing": builder.missing, **({"overrides": records[number]} if records[number] else {})})
            lacking.update(entry["key"] for entry in builder.missing)
            continue
        if unit is None:
            # R230. A location plan number with no plots.csv row or no Typology_Rev9, such as a label sharing its face, is listed
            builder.lack("unit", "plots.csv holds no row or no Typology_Rev9 for the plot")
            records[number] = outcomes.plot(number, False, ["unit"])
            built_entry(trace, builder, False)
            missing.append({"plot": number, "written": False, "missing": builder.missing, **({"overrides": records[number]} if records[number] else {})})
            lacking.update(entry["key"] for entry in builder.missing)
            continue
        builder.entry["unit"] = sourced(unit, LOCATION)
        hand, fit_unit, override, table = None, None, None, None
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
                # Q098. A cluster unit whose file holds no table of hand cluster takes that of hand alone
                table = plan_rules.hand_table(key, plan_rules.unit_class(rules["kinds"].get(number), roles.get(number)), files.get(unit))
                hand, rule = plan_rules.key_hand(key, table, files.get(unit), factors)
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
        if by_levels and item["front"] is not None and builder.entry["levels"]:
            # R359, R364. The unrounded mean entry_of takes El @ Entry from, rounded once, never the 3 decimal corners of levelRule
            builder.entry["levels"]["roadAtFrontM"] = sourced(common.fixed(plan_rules.road_at_front(item["front"]), plan_rules.ENTRY_DECIMALS), ESTIMATE)
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
            # R107, Q099. The instance Name the placer writes is the text of RRE_ZZ_SP_RVT-Name after its overrides, with its source
            link_name = builder.entry["parameters"].get(plan_from_past.RVT_NAME_PARAMETER)
            if link_name is not None:
                builder.entry["linkName"] = dict(link_name)
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
            if rev6:
                # R410. The back yard is the owner's, graded by the manual, so no yard floor is placed (Q225)
                below = None
            elif key is not None:
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
            ramp_frame, ramp_corners, ramp_front, kind, ramp_class, moved, named = item["frame"], item["corners"], item["front"], None, "rule", [], None
            if "rotationDeg" in outcomes.done[number]:
                turned = open_ramp(item["face"], item["frame"], builder.entry["rotationDeg"]["value"], builder.entry["mirrored"]["value"], item.get("depth"), surfaces, snap)
                # Q116. With no road level at an open corner the ramp stays on the front and the plot stays in the conflicts
                if turned is not None and turned["front"] is not None:
                    ramp_frame, ramp_corners, ramp_front = turned["frame"], turned["corners"], turned["front"]
                    opened_ramps.add(number)
                    if rev6 and number in ends:
                        # R408. The slope the plan writes is that of the front of the open edge the ramp moved to
                        finals[number] = design_basis.slope_of(ends[number], ramp_front, item["depth"])
            if rules is not None:
                gap = plan_rules.gap_of(mine_overrides.get("rampFrontM"))
                if "rampFrontM" in mine_overrides and number in opened_ramps:
                    outcomes.left(number, "rampFrontM", OPEN_EDGE_GAP)
                elif gap is not None:
                    ramp_front = [z + gap for z in ramp_front]
                    moved.append("rampFrontM")
                    if rev6:
                        # R408. The slope the plan writes is that of the front corners the override moved
                        finals[number] = design_basis.slope_of(ends[number], ramp_front, item["depth"])
                elif "rampFrontM" in mine_overrides:
                    outcomes.left(number, "rampFrontM", NO_GAP)
                named = mine_overrides.get("rampType")
                if named is not None:
                    moved.append("rampType")
                if rev6 and not named:
                    # R408. The ramp type of the band of the slope after the overrides, the result, never keyRules.rampSlope
                    ramp_class, kind = None, floor_fit.type_of(finals[number], inputs["bands"])
                else:
                    # R337. The ramp class the keys give the Rev 9 code, the slope of the level profile only for the Round 17 build
                    ramp_class = named or (plan_rules.key_ramp_class(key, unit) if key is not None else plan_rules.slope_class(slopes[number]))
                    kind = rules["levelProfile"]["rampTypes"].get(ramp_class) if ramp_class is not None else None
            if key is not None and kind is None:
                builder.lack("floors.ramp", NO_BAND % finals[number] if rev6 and not named else "keyRules.rampSlope and rampTypes give no ramp type for " + unit)
                made = None
            else:
                made = ramp_floor(builder, ramp_frame, ramp_corners, ramp_front, builder.entry["levels"]["entry"]["value"], finals[number], inputs, kind)
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
        built_entry(trace, builder, complete)
        if complete:
            if records[number]:
                builder.entry["overrides"] = records[number]
            if number in proposals:
                builder.entry["proposal"] = dict(proposals[number])
            applied_of[number] = outcomes.in_order(number, records[number]["applied"]) if records[number] else ()
            value_status.mark(builder.entry, statuses, table, applied_of[number], number in opened_ramps)
            if rules is not None:
                hand_rules[str(rule)] += 1
            plots.append(builder.entry)
        if builder.missing:
            missing.append({"plot": number, "written": complete, "missing": builder.missing, **({"overrides": records[number]} if records[number] and not complete else {})})
            lacking.update(entry["key"] for entry in builder.missing)
    walls = []
    if wall_rules is not None:
        # R362. The boundary walls of the written plots on the faces of the whole site, on the FFL after the overrides at the decimals of the plan
        written = {entry["plot"]: entry for entry in plots}
        rings = {plot: list(shape.exterior.coords)[:-1] for plot, shape in faces.items() if shape is not None and shape.geom_type == "Polygon"}
        ffls = {plot: common.fixed(entry["levels"]["ffl"]["value"], plan_rules.FFL_DECIMALS) for plot, entry in written.items()
                if (entry.get("levels") or {}).get("ffl", {}).get("value") is not None}
        # Breaker F2 of PR 79. The open edge after the overrides, the front of R355 or the edge a rotation override turned the link to face
        faced = {plot: (frames[plot]["left"], frames[plot]["right"]) for plot in written if frames.get(plot) and plot in rings}
        opened = {plot: plan_walls.open_edge(rings[plot], faced[plot], written[plot]["rotationDeg"]["value"], written[plot]["mirrored"]["value"],
                                             "rotationDeg" in (applied_of.get(plot) or ())) for plot in faced}
        # Q120. The fronts before a turn, so a wall names the rotation of a plot only where the turn decides whether it stands
        # Q115. The footprint cluster and the unit of each plot, for the span keyRules.wallOpen leaves open between two units of one cluster
        kin = {plot: (None, (rows.get(plot) or {}).get("typologyRev9") or None) for plot in rings}
        for index, cluster in enumerate(clusters if numbered else numbered_clusters(clusters, number_of)):
            for plot in cluster["plots"]:
                kin[plot] = (index, (rows.get(plot) or {}).get("typologyRev9") or None)
        for wall in plan_walls.walls(numbers, set(ffls), rings, opened, ffls, wall_rules, faced, kin):
            item = plan_walls.entry(wall)
            plan_walls.mark(item, wall, statuses, applied_of)
            walls.append(item)
    if trace is not None:
        trace["walls"] = walls
    conflicts = [] if key is None else turned_ramps(plots, frames, faces, applied_of, opened_ramps)
    if trace is not None:
        trace["conflicts"] = conflicts
    survey = site["surveyPoint"]
    plan = {
        "format": plan_from_past.FORMAT,
        "formatVersion": plan_from_past.FORMAT_VERSION,
        "neighbourhood": nbh,
        "coordinateSystem": {"name": "shared", "lengthUnit": "m", "angleUnit": "deg"},
        "surveyPoint": {"position": plan_from_past.point(survey["position"]), "sharedPosition": plan_from_past.point(survey["sharedPosition"])},
        "angleToTrueNorthDeg": common.r3(site["angleToTrueNorthDeg"]),
        "prototypeFolder": str(folder),
        "floorSettings": {"types": floors_config["types"], "levels": floors_config["levels"], "plotParameter": floors_config["plotParameter"]},
        "sources": {"plots": "plots.csv of the data book snapshot", "survey": "the new site file", "pastModel": "none"},
        # R401. The detail of the status of the values, written once by its key, which each value holds in statusFrom
        "statusFrom": dict(sorted(statuses.used.items())),
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
            # R362. The boundary walls by the number of plots they stand on and by field and status, none with no wall rules of the keys
            "walls": None if wall_rules is None else plan_walls.summary(walls, statuses.used),
            "rules": None if rules is None else {"levelProfile": rules["name"], "overrideSource": rules["sources"], "overrideModel": rules.get("models", []),
                                                "applied": dict(sorted(Counter(field for found in records.values() if found for field in found["applied"]).items())),
                                                **value_status.summary(plots, (settings.get("valueStatus") or {}).get("assumed"), statuses.used),
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
        # R362. The boundary walls, each value with its source and status
        "walls": walls,
        "missing": missing,
        # Q116. The plots a rotationDeg override turned to face another edge, whose ramp stays on the front of R355
        "conflicts": conflicts,
    }
    if rev6:
        # R408. The plots of the scope with no El @ Entry and why, and those whose slope after the overrides lies outside slopes_percent front_min to front_max
        limits = profile.get("slopes_percent")
        plan["summary"].update(noDoorLevel=no_door, slopeOutsideManual=[{"plot": number, "slopePercent": common.fixed(finals[number], design_basis.SLOPE_DECIMALS)}
                                                                        for number in numbers if number in entries and design_basis.outside_manual(finals[number], limits)])
    if proposals:
        plan["summary"]["proposals"] = cluster_proposals.counts(proposals)
    return plan


# Q116. The conflict of a plot a rotationDeg override turned, PlanSummary of the C# writes the same
TURNED_KEPT = "the ramp on the front of R355"
TURNED_OTHER = "the open edge the rotationDeg override turned the link to face, which the walls leave open (R362)"


def turned_ramps(plots, frames, faces, applied_of, opened=()):
    """Q116. Each written plot with a ramp whose rotationDeg override turned the edge its walls leave open off the edge the rule leaves open,
    both by plan_walls.open_edge, so the ramp on the front of R355 meets a wall, but a plot whose ramp the build moved to the open edge
    (opened, open_ramp), which leaves only a plot with no road level at a corner of its open edge."""
    found = []
    for entry in plots:
        plot = entry["plot"]
        face, frame = faces.get(plot), frames.get(plot)
        if plot in opened or "rotationDeg" not in (applied_of.get(plot) or ()) or "ramp" not in entry["floors"] or not frame or face is None or face.geom_type != "Polygon":
            continue
        ring, front, mirrored = list(face.exterior.coords)[:-1], (frame["left"], frame["right"]), entry["mirrored"]["value"]
        rule = plan_walls.open_edge(ring, front, plan_from_past.plan_rotation(cad_links.rotation_of(frame, mirrored)), mirrored, True)
        if plan_walls.open_edge(ring, front, entry["rotationDeg"]["value"], mirrored, True) != rule:
            found.append({"plot": plot, "field": "floors.ramp", "kept": TURNED_KEPT, "other": TURNED_OTHER, "otherSource": "override rotationDeg", "question": "Q116"})
    return found


def open_ramp(face, frame, rotation, mirrored, depth, surfaces, snap):
    """Q116 (Bader, 2026-10-28), R355, R362. The ramp of a plot a rotation override turned to leave open another edge than the rule
    leaves open, both by plan_walls.open_edge as turned_ramps compares them: the frame of corner_front.frame_for on the edge the walls
    leave open, the ramp outline of R127 on it with the front setback depth and the road level of R344 at its two front corners as fronts
    takes them, front None when a corner has none. None when the turn keeps the open edge of the rule or the plot has no polygon face,
    front frame or depth."""
    if face is None or face.geom_type != "Polygon" or frame is None or depth is None:
        return None
    ring, front = list(face.exterior.coords)[:-1], (frame["left"], frame["right"])
    rule = plan_walls.open_edge(ring, front, plan_from_past.plan_rotation(cad_links.rotation_of(frame, mirrored)), mirrored, True)
    first, second = plan_walls.open_edge(ring, front, rotation, mirrored, True)
    if (first, second) == tuple(rule):
        return None
    # The open edge is picked by the turn, not by a curb, so its frame holds no curb distance
    edge = {"first": first, "second": second, "length": math.dist(first, second), "distance": None, "out": corner_front.outward(face, first, second)}
    made = corner_front.frame_for(face, edge)
    corners = ramp_outline.outline(made, depth)
    levels_found = [road_level(surfaces, corner, snap) for corner in corners[:2]]
    return {"frame": made, "corners": corners, "surfaces": [name for _, name in levels_found],
            "front": [z for z, _ in levels_found] if all(z is not None for z, _ in levels_found) else None}


def nbh_plots(labels_path, nbh):
    """Every unit plot of a neighbourhood on the location plan, by its number, in order. R345: nbh None gives every unit plot of the
    location plan, the whole project of the scope of the add-in (docs/addin-design.md section 3). A client code row with no number and a
    scope of no plot are refused, as PlotScope.Numbers of the C# refuses them (H1 of the fifth review of PR 55)."""
    rows = csv_rows(labels_path, LABELS_COLUMNS + (("nbh",) if nbh is not None else ()))
    for index, row in enumerate(rows, 1):
        if row.get("kind") == "clientCode" and not row.get("number"):
            raise project_profile.ProfileError("a row of kind clientCode of the labels holds no number, row %d of the rows read (R345)" % index)
    found = sorted({row["number"] for row in rows if (nbh is None or row.get("nbh") == nbh) and row.get("kind") == "clientCode"}, key=link_summary.sort_key)
    return scope_plots(found, "project" if nbh is None else "nbh=" + nbh)


def scope_plots(plots, scope):
    """R345. The plots of a scope, refused when it numbers none, so a build or a read of no plot never reads as a run done, the line of
    PlotScope.Numbers of the C# (H1 of the fifth review of PR 55)."""
    if not plots:
        raise project_profile.ProfileError("the scope %s numbers no plot, a build or a read of no plot is refused (R345)" % scope)
    return plots


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


def step_scope(cover):
    """R345. The scope of a step file as the worker names it: project for all, nbh=<NBH>, or cover=<file> for a cover file."""
    return "project" if cover == "all" else cover if cover.startswith("nbh=") else "cover=" + Path(cover).name


def step_inputs(args):
    """R345, R347. The inputs a step of this run was made from, by the roles of the worker, each with its SHA-256."""
    roles = [("config", args.settings), ("baseplanPlots", args.baseplan), ("plotsCsv", args.plots_csv), ("labels", args.labels),
             ("cover", None if args.cover == "all" or args.cover.startswith("nbh=") else args.cover)]
    return [parity.input_entry(role, path) for role, path in roles if path]


# R408. The worker input roles of the civil file and of the floor file of the bands of the ramp types, BuildSteps.CivilRole and BandsRole of the C#
CIVIL_ROLE = "civilFile"
BANDS_ROLE = "floorOutlines"
# R357. The build steps that take the prototype files, each file of the folder an input of its own, prototype:<its name>
PROTOTYPE_STEPS = parity.LISTED_STEPS
PROTOTYPE_ROLE = "prototype:"


def build_inputs(args, settings, step):
    """R345, R354 to R357. The inputs a build step was made from, those of the faces and every other file the steps read, by role, and for
    the steps of PROTOTYPE_STEPS every file of the prototype folder as folder_files lists it, so parity sees the listing each side took."""
    roles = [("rules", project_profile.profile_path(settings)), ("footprintBlocks", args.footprint_blocks), ("sales", args.sales),
             ("roadPieces", Path(args.roads) / "pieces.json" if args.roads else None), ("overrides", getattr(args, "overrides", None))]
    roles += [("roadSurface:" + name, Path(args.landxml) / (name + ".xml")) for name in getattr(args, "road_names", None) or []]
    # R408. Under rev6 the civil file of the door levels and the floor file whose bands give the ramp type of a slope
    roles += [(CIVIL_ROLE, getattr(args, "civil_file", None)), (BANDS_ROLE, args.floors if getattr(args, "design_basis", None) == design_basis.REV6 else None)]
    found = step_inputs(args) + [parity.input_entry(role, path) for role, path in roles if path]
    if step in PROTOTYPE_STEPS and getattr(args, "prototypes", None):
        # A prototype file is a whole Revit model, so each is hashed once a run
        listed = getattr(args, "prototype_entries", None)
        if listed is None:
            listed = args.prototype_entries = [parity.input_entry(PROTOTYPE_ROLE + name, Path(args.prototypes) / name) for name in folder_listing(args.prototypes)]
        found += listed
    return found


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


def write_build_step(args, settings, step, made, written=None, tables=None):
    """R345, R354 to R357. step_<step>_python.json of a build step (step_entries), with the tables of the step, R401."""
    plots = step_entries(settings, step, made, written)
    # R407. The design basis of the build in the run block of each build step
    parity.write_step(args.steps, step, "python", step_scope(args.cover), plots, settings.values, build_inputs(args, settings, step), tables,
                      {"designBasis": getattr(args, "design_basis", None)})
    print("step %s: %d items (R345)" % (step, len(plots)))


def site_report(path):
    """The survey point, angle and level elevations the newSite job read back from the new file."""
    report = json.loads(Path(path).read_text(encoding="utf-8"))
    return {"surveyPoint": report["surveyPoint"], "angleToTrueNorthDeg": report["angleToTrueNorthDeg"],
            "levels": {level["name"]: level["elevationM"] for level in report["levels"]}}


def folder_listing(folder):
    """R357. The names of the files of the prototype folder in the order of sorted, refused when the folder is not on disk, the listing the C#
    takes of the same folder (BuildSteps)."""
    folder = Path(folder)
    if not folder.is_dir():
        raise project_profile.ProfileError("the prototype folder %s of pastJob is not on disk (R357)" % folder)
    return sorted(path.name for path in folder.iterdir() if path.is_file())


def folder_files(folder, profile):
    """The prototype file of each unit with its name as the folder spells it."""
    return plan_from_cad.prototype_files(folder_listing(folder), profile)[0]


def hand_settings(block):
    """R357. The settings of the majority rule a build by keyRules reads for the hands, refused when one is missing or of another kind and
    never read as empty, with the lines of Hands.Majority of the C# (B3 of the review of PR 55)."""
    def number(value):
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    if not isinstance(block, dict):
        raise project_profile.ProfileError("config holds no majority block, the settings of the majority rule a build by keyRules reads (R357)")
    if not isinstance(block.get("pairKinds"), list) or not all(isinstance(kind, str) for kind in block["pairKinds"]):
        raise project_profile.ProfileError("config holds no majority.pairKinds list of texts, the unit kinds that pair in a row of units (R357)")
    if not isinstance(block.get("kindByPrefix"), dict) or not all(isinstance(kind, str) for kind in block["kindByPrefix"].values()):
        raise project_profile.ProfileError("config holds no majority.kindByPrefix object of texts, the kind of a unit code the rules profile gives none (R357)")
    if not number(block.get("sideShare")):
        raise project_profile.ProfileError("config holds no majority.sideShare number, the share of the depth a side neighbour shares (R357)")
    return block


# R355, F3 of the third review of PR 55. The settings of config a build by keyRules reads as numbers, BuildRules.NumberSettings of the C#
NUMBER_SETTINGS = ("siteBuild.corner.roadEdgeM", "analysis.toleranceM", "analysis.partyWallShare")


def number_settings(settings):
    """R355. Each setting of NUMBER_SETTINGS a number that is no bool, else refused with one line, where a text stopped Python with TypeError
    and the C# took it as a number."""
    for dotted in NUMBER_SETTINGS:
        value = settings.get(dotted)
        if not isinstance(value, (int, float)) or isinstance(value, bool):
            raise project_profile.ProfileError("config %s holds %s, a build takes it as a number (R355)" % (dotted, plan_rules.shape_name(value)))


def alone_table_units(units, rows, roles, key, files, kind_of):
    """Q105. The unit plots in a cluster that take the table of hand alone (plan_rules.hand_table, Q098), whose row pair of R333 is walked as
    if no cluster held them, for their hand alone. files gives the file each code links, none given leaves the set empty."""
    found = set()
    for plot in units:
        code = (rows.get(plot) or {}).get("typologyRev9")
        name = plan_rules.unit_class(kind_of(code), roles.get(plot))
        if name == "cluster" and plan_rules.hand_table(key, name, files.get(code)) == "alone":
            found.add(plot)
    return found


def rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales, clusters=(), files=None, basis=None):
    """R277 to R280. The level profile, named or the default of R277, the hand rules, the open end of every plot and the overrides
    of the source models. R337: for a profile with keyRules also the side road, the pair of its row of units in no cluster (the
    clusters given, over every unit plot) and the kind of every plot, as majority.py gives them on the keys, the fronts of every
    unit plot of plots.csv found so a row is walked past the plots of the build. Q105: with files, the file of each code, a cluster unit that
    takes the table of hand alone gets the row pair of the walk as if no cluster held it, for its hand alone. R407: the design basis the run
    read once, read here when none is given."""
    import majority
    name = plan_rules.profile_name(profile, args.level_profile)
    lp = plan_rules.level_profile(profile, name)
    key = plan_rules.key_rules_of(lp)
    block = settings.get("majority") if key is None else hand_settings(settings.get("majority"))
    if key is not None:
        number_settings(settings)
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

        def pair_kind(plot):
            return majority.pair_kind(plot, rows, profile, block)
        found = key_rules.row_pairs(units, neighbours, pair_kind, lambda plot: plot in roles, block["pairKinds"])
        # R340. The other unit of the row pair of every unit in no cluster, which shares its FFL by keyRules.ffl
        partners = plan_rules.row_partners({plot: item.get("side") for plot, item in found.items()}, neighbours)
        free = alone_table_units(units, rows, roles, key, files or {}, lambda code: majority.kind_of(code, profile, block))
        if free:
            # Q105. The row pair of such a unit as if no cluster held it, for its hand alone, so the partners above stay those of the plain walk
            walked = key_rules.row_pairs(units, neighbours, pair_kind, lambda plot: plot in roles and plot not in free, block["pairKinds"])
            found = dict(found, **{plot: walked[plot] for plot in free if plot in walked})
        pairs = {number: (found.get(number) or {}).get("side") for number in numbers}
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
            "basis": design_basis.basis_of(settings) if basis is None else basis,
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
    parser.add_argument("--analysis", help="R354. refused since Q089, a build takes the footprint clusters alone")
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
    # D19. Every file the build opens under an input root is held from its first open to the end, recorded beside the plan
    with input_guard.Guard(input_guard.roots(args.settings), record=Path(args.out).with_name(Path(args.out).stem + "_inputs.json")):
        return run_build(args)


def run_build(args):
    done = False
    try:
        # R345. The step files of this run go first, so a run that fails leaves none and the parity finds them missing
        if args.steps:
            for name in PARITY_STEPS:
                parity.clear_step(args.steps, name, "python")
        if args.analysis:
            raise project_profile.ProfileError(ANALYSIS_REFUSED)
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        block = settings.get("siteBuild")
        # R407. The design basis read once for the whole run, a mode outside designBasis.modes refused before any step
        basis = design_basis.basis_of(settings)
        args.design_basis = basis["mode"]
        args.civil_file = (basis.get("door") or {}).get("path") if (basis.get("door") or {}).get("source") == design_basis.CIVIL_FILE else None
        if args.cover.startswith("nbh=") or args.cover == "all":
            if not args.labels:
                raise project_profile.ProfileError("nbh=<NBH> and all need --labels")
            nbh = None if args.cover == "all" else args.cover.partition("=")[2]
            cover = {"neighbourhood": nbh or "all", "plots": nbh_plots(args.labels, nbh)}
        else:
            cover = json.loads(Path(args.cover).read_text(encoding="utf-8"))
            scope_plots(cover["plots"], step_scope(args.cover))
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
        refused = face_rings.refusals(baseplan, parity.area_tolerance(settings))
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
            done = True
            return 0
        # R354. A build walks the ring of a face in the order shapely gives it, which the C# gives only for a face of one ring
        one_ring_faces(baseplan)
        args.roads = str(cad_units.read_input(settings, "roads", args.roads))
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
        if not roads:
            # R355. The front of a plot is the edge nearest a curb, and corner_front.road_edges asks the nearest of no line
            raise project_profile.ProfileError("the road layout read holds no line of the curb layers analysis.roadEdgeLayers names, the front of a plot is the edge "
                                               "nearest a curb (R355)")
        if not args.footprint_blocks:
            raise project_profile.ProfileError("give --footprint-blocks for the clusters, a build takes the footprint clusters of R236 alone (R354)")
        args.footprint_blocks = str(cad_units.read_input(settings, "footprintBlocks", args.footprint_blocks))
        shapes = footprint_check.block_shapes(simple_blocks(footprint_check.footprint_blocks(args.footprint_blocks, settings)))
        clusters = footprint_clusters(cover["plots"], rows, faces, shapes)
        job = plan_from_past.load_job(settings)
        folder, _ = plan_from_past.prototype_folders(plan_from_past.input_root(settings), job)
        args.prototypes = str(folder)
        # R337. A build by keyRules walks the rows of units past its own plots, so the clusters of every unit plot tell which units pair
        if args.steps:
            build_step_rules(profile, args.level_profile)
        everything, by_keys = [], plan_rules.key_rules_of(plan_rules.level_profile(profile, plan_rules.profile_name(profile, args.level_profile))) is not None
        if by_keys:
            units = [number for number, row in rows.items() if row.get("typologyRev9") and number in faces]
            # A face with two plan numbers, such as 476|477, is no plot of its own and leaves its cluster, as majority.py takes them
            everything = site_clusters(units, rows, faces, shapes)
        if args.steps:
            write_build_step(args, settings, "clusters", cluster_step(cover["plots"], everything))
        # R277. Every build follows a level profile, the named one or levelProfiles.default
        # Q105. The file of each code, which tells the cluster units of the table of hand alone whose row pair the hand walks
        files = folder_files(folder, profile)
        rules = rule_inputs(args, settings, profile, cover["plots"], rows, faces, roads, sales, everything, files=files, basis=basis)
        # R339, R340. The road surfaces of the one setting majority.py measures the levels on, R409 the one surface of rev6
        args.road_names = plan_rules.road_names(block, rules["levelProfile"], basis)
        if design_basis.is_rev6(basis):
            design_basis.check_surfaces(basis, args.landxml)
        surfaces = load_roads([Path(args.landxml) / (name + ".xml") for name in args.road_names], args.scratch)
        # R337. A build by keyRules takes the clusters of every unit plot, the site as the keys were measured on (R271)
        trace = {}
        plan = build(cover["plots"], rows, number_of, faces, roads, everything if by_keys else clusters, profile, settings,
                     prototype_offsets.plot_frame(prototype_offsets.load(args.offsets)), files, folder,
                     site_report(args.site), surfaces, floor_fit.load(args.floors), cover["neighbourhood"], sales, True, rules, trace=trace)
        if args.steps:
            write_build_step(args, settings, "fronts", front_step(cover["plots"], trace["front"], surfaces, trace["snap"]))
            hands = hand_step(cover["plots"], rows, rules, trace, files, profile, settings.get("majority"))
            write_build_step(args, settings, "hands", hands)
            links = link_step(cover["plots"], rows, trace, hands, rules["keyRules"], files, profile)
            write_build_step(args, settings, "linkFrame", links, {"rotationDeg": plan_from_past.plan_rotation})
            write_build_step(args, settings, "levels", level_step(cover["plots"], rows, trace, rules, profile))
            write_build_step(args, settings, "floors", floor_step(cover["plots"], rows, trace, links, rules, profile, floor_fit.width_step(settings)))
            walls, tables = plan_walls.step(cover["plots"], rows, trace)
            write_build_step(args, settings, "walls", walls, tables=tables)
            write_build_step(args, settings, "overrides", override_step(cover["plots"], rows, trace, hands, files, rules, profile, floor_fit.width_step(settings)),
                             {"rotationDeg": plan_from_past.plan_rotation})
            write_build_step(args, settings, "parameters", parameter_step(cover["plots"], rows, trace, rules))
            status, tables = status_step(cover["plots"], rows, trace)
            write_build_step(args, settings, "status", status, tables=tables)
            # R363. The check rows of each plot from the plan and the override list the build took, with the chip Assumed of Q108
            listed = json.loads(Path(args.overrides).read_text(encoding="utf-8")) if args.overrides else {}
            made, tables = check_rows.step(cover["plots"], rows, plan, listed, rules["sources"], rules.get("models"), settings)
            write_build_step(args, settings, check_rows.STEP, made, tables=tables)
            made, tables = summary_step(cover["plots"], rows, plan)
            write_build_step(args, settings, "summary", made, tables=tables)
        plan_from_past.write_plan(out, plan)
        done = True
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("plan_from_book: " + str(error), file=sys.stderr)
        return 2
    finally:
        # R345, R347. A run that fails, by a refusal or by any error, leaves no step file of its own, as the worker writes its outputs all or
        # none, so a step written before a later step or the build failed never reads as a run done (B5 of the review of PR 55)
        if args.steps and not done:
            for name in PARITY_STEPS:
                try:
                    parity.clear_step(args.steps, name, "python")
                except (ValueError, OSError):
                    # A folder refused at the start, inside the repo, holds no step file of this run to clear
                    pass
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
    if "statusPlots" in rules:
        lines.append(status_line(rules))
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
    if summary.get("walls"):
        lines.append("boundary walls %d, by the plots they stand on %s, by field and status %s (R362)" % (
            summary["walls"]["walls"], summary["walls"]["byPlots"], summary["walls"]["statusByField"]))
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
    if summary.get("proposals"):
        lines.append("cluster proposals by Rev 06 (R411): proposed %d, none %d" % (summary["proposals"]["proposed"], summary["proposals"]["none"]))
    return lines


if __name__ == "__main__":
    sys.exit(main())
