"""R271 to R276. The majority rule of Bader: per decision, the logic most past plots follow, its share, and every plot it misses.

R271: each plot of past_all.csv gets its context from the data book: the Rev 9 code, the footprint block mirror flag, its
cluster side and index (R236, R123), its row of side neighbours with the open ends, the sales Orientation and streets, the
side road of a corner plot, the plot depth, the road level at its front corners on every surface, the key and its group.
R272: a candidate logic is a factor or a formula. A factor learns, on one key group and within its scope, the value most
plots take for each value of the factor. A formula gives the value with nothing learned. Each counts holds, breaks and cannot
(no value) by unit code and by key. The strongest is the one with the largest share of holds over every plot, strong when it
reaches majority.strongShare. Its breaks and cannots go to the override list. A level holds within majority.levelHoldMm.
R273 ramp type and slope, R274 hand, R275 levels (El @ Entry against every road surface, FFL less El @ Entry, steps), R276
Generic floor, yard, CASE and the other link parameters. Each is tested on every group of majority.groups apart, so a rule of
one generation is not forced on the other. R326. A row of a key whose kind majority.kindLeaveOut names, SE, is left out of every
decision it lists, the fields an SE model does not hold, so the SE Generic levels of IMA and IMB never count for a level rule.
Overrides hold no real level or coordinate: a level as its gap to the rule in metres.
"""
import argparse
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely import wkt
from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import corner_front
import floor_fit
import footprint_check
import key_levels
import key_rules
import link_summary
import override_source
import past_all
import plan_from_book
import plan_from_cad
import plan_from_past
import plan_rules
import plot_relations
import project_profile
import prototype_offsets
import ramp_levels
import ramp_outline
import yard_rule

CANNOT = "cannot"
ROAD = "roadLevel"
# The first word of each decision run makes, which majority.kindLeaveOut names
DECISIONS = ("rampSlope", "hand", "entry", "rampFront", "rampFrontFall", "height", "steps", "generic", "yard", "parameter", "linkRotation", "frontCorner",
             "linkOrigin", "rampOutline", "yardOutline", "wallBase", "wallTop", "wallLine", "frontOpen", "levelSplit", "baseHeight", "ffl")


def group_of(key, block):
    return next((name for name, keys in block["groups"].items() if key in keys), None)


def kind_of(code, profile, block):
    """villa, duplex or townhouse of a unit code, from the profile, else from the code prefixes the config names."""
    found = ((profile.get("typologies") or {}).get(code) or {}).get("kind")
    if found:
        return found
    return next((kind for prefix, kind in block["kindByPrefix"].items() if code and code.startswith(prefix)), None)


def side_neighbours(numbers, faces, frames, share, depth):
    """R271. Per plot the unit plots that share a side with it, at least share of the depth, put left or right as seen from the road."""
    labels = [number for number in numbers if number in faces]
    tree = STRtree([faces[number] for number in labels])
    found = {}
    for number in labels:
        face, frame = faces[number], frames.get(number)
        sides = {"left": [], "right": [], "framed": frame is not None}
        for index in tree.query(face.buffer(0.05)):
            other = labels[index]
            if other == number or frame is None:
                continue
            if face.boundary.intersection(faces[other].buffer(0.05)).length < share * depth:
                continue
            offset = (faces[other].centroid.x - face.centroid.x) * frame["r"][0] + (faces[other].centroid.y - face.centroid.y) * frame["r"][1]
            sides["left" if offset < 0 else "right"].append(other)
        found[number] = sides
    return found


def row_place(number, neighbours):
    """R271. How many plots stand left of a plot in its row of side neighbours, and the length of the row."""
    seen, at, left = {number}, number, 0
    while True:
        nxt = [plot for plot in neighbours.get(at, {}).get("left", []) if plot not in seen]
        if len(nxt) != 1:
            break
        at = nxt[0]
        seen.add(at)
        left += 1
    at, length = number, left + 1
    while True:
        nxt = [plot for plot in neighbours.get(at, {}).get("right", []) if plot not in seen]
        if len(nxt) != 1:
            break
        at = nxt[0]
        seen.add(at)
        length += 1
    return left, length


def road_side(face, frame, tree, roads, limit):
    """R271. For a plot with two or more road edges where its other road edge lies seen from the front road: left, right or back."""
    if face is None or frame is None:
        return None
    others = [edge for edge in corner_front.road_edges(face, tree, roads, limit) if not corner_front.same_edge(frame, edge)]
    if not others:
        return "none"
    edge = others[0]
    middle = ((edge["first"][0] + edge["second"][0]) / 2 - face.centroid.x, (edge["first"][1] + edge["second"][1]) / 2 - face.centroid.y)
    along = middle[0] * frame["r"][0] + middle[1] * frame["r"][1]
    across = middle[0] * frame["n"][0] + middle[1] * frame["n"][1]
    return "back" if abs(across) > abs(along) else "left" if along < 0 else "right"


def band(value, edges):
    """The label of the first band whose upper edge the value is under, the last label above them all."""
    if value is None:
        return None
    for limit, label in edges:
        if value < limit:
            return label
    return edges[-1][1]


def road_mean(data, column):
    """The mean road level of the two front corners on a surface of plots.csv, or of the snapped road level of R157."""
    left, right = (data.get("roadLevelLeft"), data.get("roadLevelRight")) if column == ROAD else (data.get("level_" + column + "_left"), data.get("level_" + column + "_right"))
    if left in (None, "") or right in (None, ""):
        return None
    return (float(left) + float(right)) / 2.0


def road_corners(data):
    """The snapped road level of R157 at the left and the right front corner, or None."""
    left, right = data.get("roadLevelLeft"), data.get("roadLevelRight")
    return None if left in (None, "") or right in (None, "") else (float(left), float(right))


def clean_entry(row, limit):
    """El @ Entry of a row, None where a key parameter is off its own ramp back by more than limit (R270)."""
    if row["entry"] is None:
        return None
    if row["entrySource"] == "floorParameter" and row["rampBack"] is not None and abs(row["entry"] - row["rampBack"]) > limit:
        return None
    return row["entry"]


def contexts(rows, book, sales, faces, roles, neighbours, side_roads, profile, block):
    """R271. The factors of every plot of every key."""
    found = []
    for row in rows:
        plot = row["plot"]
        data = book.get(plot, {})
        group = group_of(row["key"], block)
        code = row["typologyRev9"] if block["codeFrom"][group] == "rev9" else (row["keyUnit"] or row["typologyRev9"])
        role = roles.get(plot)
        sides = neighbours.get(plot)
        left, length = row_place(plot, neighbours) if sides is not None else (None, None)
        # R333. With no front frame no neighbour can be put left or right, so the open end is unknown, never both
        open_end = None if sides is None or not sides.get("framed", True) else "both" if not sides["left"] and not sides["right"] else "left" if not sides["left"] else "right" if not sides["right"] else "none"
        streets = (sales.get(plot) or {}).get("No. of streets")
        depth = float(data["depthM"]) if data.get("depthM") not in (None, "") else None
        steps = None if row["steps"] in (None, "") else str(row["steps"])
        found.append({
            "row": row, "plot": plot, "key": row["key"], "group": group, "code": code, "rev9": row["typologyRev9"], "kind": kind_of(code, profile, block),
            "modelKind": row.get("kind") or None,
            "file": row["linkFile"], "inCluster": role is not None, "role": role, "neighbours": sides or {"left": [], "right": []},
            "factors": {
                "all": "all", "typology": code, "key": row["key"],
                "blockMirror": {"yes": "yes", "no": "no"}.get(data.get("footprintMirrored")),
                "clusterSide": role["side"] if role else "alone", "clusterIndexParity": ("odd" if role["index"] % 2 else "even") if role else "alone",
                "openEnd": open_end, "rowParity": None if left is None else ("odd" if left % 2 == 0 else "even"),
                "orientation": (sales.get(plot) or {}).get("Orientation") or None, "roadSide": side_roads.get(plot),
                "corner": None if streets is None else ("yes" if plan_from_past.is_number(streets) and float(streets) >= 2 else "no"),
                "depthBand": band(depth, block["depthBands"]), "steps": steps, "typologySteps": None if steps is None else code + " " + steps,
                "mirroredFactor": str(row["mirrored"]), "typologyMirrored": str(code) + " " + str(row["mirrored"]),
                "zone": row.get("zone") or None, "nbh": row.get("nbh") or None,
            },
        })
    for item in found:
        plan_rules.joined_factors(item["factors"])
    return found


def evaluate(items, target, factor, scope, hold):
    """R272. A factor learns the value most plots take per factor value within its scope. A formula is a function of the item."""
    if callable(factor):
        return None, {id(item): judge(factor(item), target(item), hold) for item in items if target(item) is not None}
    learned = defaultdict(Counter)
    for item in items:
        value, aim = item["factors"].get(factor), target(item)
        if value is not None and aim is not None:
            learned[(scope(item), value)][aim] += 1
    rule = {key: counts.most_common(1)[0][0] for key, counts in learned.items()}
    return rule, {id(item): judge(rule.get((scope(item), item["factors"].get(factor))), target(item), hold) for item in items if target(item) is not None}


def judge(guess, aim, hold):
    return (CANNOT if guess is None else "holds" if hold(guess, aim) else "breaks", guess)


def tally(items, outcome, by):
    found = defaultdict(Counter)
    for item in items:
        if id(item) in outcome:
            found[by(item)][outcome[id(item)][0]] += 1
    return {str(key): dict(value) for key, value in sorted(found.items(), key=lambda pair: str(pair[0]))}


def share(counts):
    total = sum(counts.values())
    return round(counts.get("holds", 0) / total, 4) if total else None


def decide(name, items, target, candidates, scope, strong, hold=None, checks=(), extra_by=None, judge_scope=None, bands=None, build_view=None):
    """R272. Every candidate on the items, the strongest by share, whether it is strong, and its misses. A check reads a value of the
    key itself, such as its step count, so it is reported and never coded, a new plan has no such value. extra_by names more
    tallies of the coded candidate, by name, such as the front rule of each plot (R332). R337: with judge_scope the coded candidate,
    learned and chosen within scope, is judged again by judge_scope, the scope of a build, its counts in byBuild and its misses the
    plots a build does not reproduce. R339: with build_view, per formula the value it gives on the levels a build holds, the coded
    formula is judged again on them the same way, and bands, by name the holds of the levels at 1 and 5 mm, count the coded
    candidate on the values of the key at each band beside its share, over all and per key."""
    hold = hold or (lambda a, b: a == b)
    tests, best, rules, outcomes = {}, None, {}, {}
    for label, factor in candidates.items():
        rule, outcome = evaluate(items, target, factor, scope, hold)
        rules[label] = rule
        outcomes[label] = outcome
        counts = Counter(state for state, _ in outcome.values())
        tests[label] = {"holds": counts.get("holds", 0), "breaks": counts.get("breaks", 0), "cannot": counts.get(CANNOT, 0), "share": share(counts),
                        "byCode": tally(items, outcome, lambda item: item["code"]), "byKey": tally(items, outcome, lambda item: item["key"])}
        if rule is not None:
            tests[label]["rule"] = {" | ".join(str(part) for part in key): value for key, value in sorted(rule.items(), key=lambda pair: str(pair[0]))}
        if label in checks:
            tests[label]["checkOnly"] = True
        elif best is None or (tests[label]["share"] or 0) > (tests[best[0]]["share"] or 0):
            best = (label, outcome)
    label, outcome = best
    keyed = outcome
    built = None
    if judge_scope is not None and rules[label] is not None:
        factor = candidates[label]
        outcome = {id(item): judge(rules[label].get((judge_scope(item), item["factors"].get(factor))), target(item), hold) for item in items if id(item) in outcome}
        counts = Counter(state for state, _ in outcome.values())
        built = {"holds": counts.get("holds", 0), "breaks": counts.get("breaks", 0), "cannot": counts.get(CANNOT, 0), "share": share(counts)}
    elif build_view and label in build_view:
        # R339. The coded formula on the levels a build holds, so its misses are the plots a build does not reproduce
        outcome = {id(item): judge(build_view[label](item), target(item), hold) for item in items if id(item) in keyed}
        counts = Counter(state for state, _ in outcome.values())
        built = {"holds": counts.get("holds", 0), "breaks": counts.get("breaks", 0), "cannot": counts.get(CANNOT, 0), "share": share(counts)}
    misses = [{"item": item, "state": outcome[id(item)][0], "rule": outcome[id(item)][1], "value": target(item)}
              for item in items if id(item) in outcome and outcome[id(item)][0] != "holds"]
    result = {"decision": name, "plots": len(outcome), "coded": label, "share": tests[label]["share"], "strong": (tests[label]["share"] or 0) >= strong,
              "misses": len(misses), "tests": tests}
    if built is not None:
        result["byBuild"] = built
    for extra, by in (extra_by or {}).items():
        result["by" + extra[0].upper() + extra[1:]] = tally(items, outcome, by)
    if bands:
        result["bands"] = {}
        for band_name, band_hold in bands.items():
            states = {id(item): judge(keyed[id(item)][1], target(item), band_hold) for item in items if id(item) in keyed}
            counts = Counter(state for state, _ in states.values())
            result["bands"][band_name] = {"holds": counts.get("holds", 0), "share": share(counts),
                                          "byKey": {key: share(found) for key, found in tally(items, states, lambda item: item["key"]).items()}}
    return result, misses


def decided_by_question(misses, block):
    """R272, R337. The misses of a plot and field an open question of majority.decidedByQuestion decides are no override: the question
    gives their value, so they are listed apart with the question and kept out of the list."""
    decided = {}
    for question, rule in (block.get("decidedByQuestion") or {}).items():
        if isinstance(rule, dict):
            for key, plots in rule["plots"].items():
                for plot in plots:
                    for field in rule["fields"]:
                        decided[(key, str(plot), field)] = question
    kept, listed = [], []
    for miss in misses:
        question = decided.get((miss["key"], str(miss["plot"]), miss["field"]))
        if question is None:
            kept.append(miss)
        else:
            listed.append({"question": question, "key": miss["key"], "plot": miss["plot"], "field": miss["field"], "decision": miss["decision"],
                           "value": miss["value"], "rule": miss["rule"]})
    return kept, listed


def pair_kind(plot, book, profile, block):
    """R333. The kind of the unit of a plot for the row pairs. A face with two plan numbers, such as 2007|2006 with the code
    TH3|TH3, is no plot of its own, so it pairs with no unit as it joins no cluster, and has no kind."""
    return kind_of((book.get(plot) or {}).get("typologyRev9"), profile, block) if plot.isdigit() else None


def yard_depth(row):
    """R334. How far the yard lies under the top of the generic floor of the key itself, at 3 decimals, noYard with no yard. A key
    with a schedule takes its FFL from the schedule (R326), so the floor top of the key is held here, and the FFL only where the
    row has no floor top."""
    top = row.get("genericTop") if row.get("genericTop") is not None else row.get("ffl")
    if row.get("yardTop") is None:
        return "noYard"
    return None if top is None else "%.3f" % (top - row["yardTop"])


def kept_for(word, pool, block, decision=None):
    """R326. The items a decision is measured on: a row whose model kind majority.kindLeaveOut leaves out of that decision, by its
    first word or, R335, by the whole name of one parameter decision such as parameter RRE_ZZ_SP_RVT-Name, is not."""
    leave = block.get("kindLeaveOut") or {}
    return [item for item in pool if word not in leave.get(item.get("modelKind") or "", ()) and (decision is None or decision not in leave.get(item.get("modelKind") or "", ()))]


def slope_class(item, block):
    """R273. The ramp slope class of a plot: from the type name where the type says it, else from the measured slope."""
    named = block["rampTypeClass"].get(item["row"]["rampType"] or "")
    return named if named is not None else band(item["row"]["rampSlopePercent"], block["slopeBands"])


def hand_class(item):
    """R274. villa for a detached unit, cluster for a duplex or townhouse unit in a cluster, alone for one in none."""
    if item["kind"] == "villa":
        return "villa"
    return "cluster" if item["inCluster"] else "alone"


def parameter(item, name):
    """R276, R335. A link parameter of the row, else of the key read, which gives only the level parameters past_all.csv does not list."""
    value = json.loads(item["row"].get("parameters") or "{}").get(name)
    if value in (None, ""):
        value = (item.get("readParameters") or {}).get(name)
    return None if value in (None, "") else str(value)


def group_yard_depth(profile, group):
    """R335. The yard depth of the level profile whose job is the group, so a wall base at the yard level follows the profile."""
    return next((found.get("yardBelowFflM") for found in (profile.get("levelProfiles") or {}).values() if isinstance(found, dict) and found.get("job") == group), None)


def base_height(profile, code):
    table = profile.get("steps.base_height")
    return (table.get(code, table["default"])) / 1000.0


def neighbour_votes(pool, target):
    """R273. The class most side neighbours of the same key take, the plot itself left out."""
    found = {(item["key"], item["plot"]): target(item) for item in pool}
    for item in pool:
        sides = item["neighbours"]
        votes = Counter(found.get((item["key"], other)) for other in sides["left"] + sides["right"])
        votes.pop(None, None)
        item["factors"]["neighbour"] = votes.most_common(1)[0][0] if votes else None


def cluster_top(pool, profile, block):
    """R275. The height a plot gets when its cluster shares one FFL at the highest El @ Entry plus the base of that unit."""
    by_cluster = defaultdict(list)
    for item in pool:
        entry = clean_entry(item["row"], block["entryRampM"])
        if item["role"] and entry is not None:
            by_cluster[(item["key"], tuple(item["role"]["plots"]))].append((entry + base_height(profile, item["code"]), item))
    found = {}
    for members in by_cluster.values():
        top = max(value for value, _ in members)
        for _, item in members:
            found[id(item)] = common.fixed(top - clean_entry(item["row"], block["entryRampM"]), 4)
    return found


def levels_rule(pool, clusters, profile, block):
    """R275. The height levels.py gives each plot from the El @ Entry of its key, one FFL per cluster (R67), as the rebuild does."""
    found = {}
    for key in sorted({item["key"] for item in pool}):
        mine = {item["plot"]: item for item in pool if item["key"] == key}
        entries = {plot: {"unit": item["code"], "entry": clean_entry(item["row"], block["entryRampM"])} for plot, item in mine.items()}
        held = [cluster for cluster in clusters if set(cluster["plots"]) <= set(mine)]
        for plot, row in plan_from_cad.plot_levels(profile, held, entries).items():
            if row.get("ffl") is not None and plot in mine:
                found[id(mine[plot])] = common.fixed(row["ffl"] - entries[plot]["entry"], 4)
    return found


def run(items, book, profile, block, surfaces, clusters, frame_units=None, key_plans=None, outlines=None, step=None, faces=None, wall_edge=None, levels=None):
    """R273 to R276. Every decision on every group, with its misses. R332: with frame_units, the plotFrame units of
    config/prototype_offsets.json, the link rotation, the front of the corner plots and the link origin, with the refit per code
    of each group in linkFits of extras. R334: the ramp outline of R127, and with key_plans, outlines (config/floor_outlines.json)
    and the width step the yard outline of R135, with the flat ramps of each group in flatRamps of extras. R335: with key_plans,
    faces and wall_edge the boundary walls and the open front, their misses in wallMisses of extras and never in the override list.
    R339: levels gives per group a build by keyRules builds the level profile, the road a build gives at the front corners of every
    site plot, the roles of the clusters and the row partners, and the levels of that group are measured as the build gives them
    (key_levels.measure), the other groups keep R275."""
    strong, hold_m = block["strongShare"], block["levelHoldMm"] / 1000.0
    by_group = lambda item: item["group"]
    by_file = lambda item: (item["group"], item["file"])
    level_hold = lambda guess, aim: abs(guess - aim) <= hold_m + 1e-9
    riser = profile.get("steps.riser_added") / 1000.0
    results, misses, datums, extras = [], [], {}, {"linkFits": {}, "flatRamps": {}, "yardOutlineGroups": {}, "walls": {}, "wallMisses": {}, "frontCover": {}, "frontWalled": {},
                                                    "wallsNoBaseLevel": {}, "coverage": {}, "decidedByQuestion": [], "levelMisses": {}, "levelNotMeasured": {}, "levelItems": {}, "levelSplitPatterns": {}}

    def record(name, pool, target, candidates, scope, field, hold=None, checks=(), gap=None, extra_by=None, judge_scope=None, bands=None, build_view=None,
               listed=False):
        pool = kept_for(name.split(" ")[1], pool, block, name.split(" ", 1)[1])
        if listed:
            # R339, R341. Every item a level decision judges, so the second method counts an item only one side holds as a mismatch
            extras["levelItems"][name] = sorted({item["key"] + " " + item["plot"] for item in pool if target(item) is not None})
        # R272. Every decision says which keys and how many plots it holds, a decision with no plot is listed with none, never dropped
        by_key = Counter(item["key"] for item in pool if target(item) is not None)
        extras["coverage"][name] = {"plots": sum(by_key.values()), "byKey": dict(sorted(by_key.items())),
                                    "keysWithNoPlot": sorted(set(block["groups"].get(name.split(" ")[0], ())) - set(by_key))}
        if not pool:
            return None
        result, missed = decide(name, pool, target, candidates, scope, strong, hold, checks, extra_by, judge_scope, bands, build_view)
        result["field"] = field
        if bands is not None:
            result["passMm"] = block["levelPassMm"]
        result["byKind"] = dict(sorted(Counter(item.get("modelKind") or "none" for item in pool).items()))
        results.append(result)
        found_misses = []
        for miss in missed:
            item = miss["item"]
            found = {"decision": name, "field": field, "plot": item["plot"], "key": item["key"], "code": item["code"], "value": miss["value"],
                     "rule": miss["rule"], "state": miss["state"], "reason": ("no value of " if miss["state"] == CANNOT else "breaks ") + result["coded"]}
            if judge_scope is not None and item.get("buildFile") and item["buildFile"] != item.get("file"):
                # R337. A build links another file than the key, so its hand is the one the table of that file gives
                found["reason"] += " by the table of %s, the file a build links, the key links %s" % (item["buildFile"], item.get("file"))
            if gap is not None:
                found["overrideValue"] = gap(item, miss["value"], miss["rule"])
            if "wall" in item:
                found["elementId"] = item["wall"].get("elementId")
                found["plots"] = item["wall"].get("plots")
            found_misses.append(found)
        # R272, R337. A plot and field an open question decides is listed apart, never an override
        kept_misses, decided = decided_by_question(found_misses, block)
        if decided:
            result["misses"] -= len(decided)
            result["decidedByQuestion"] = len(decided)
            extras["decidedByQuestion"].extend(decided)
        misses.extend(kept_misses)
        return result

    for group in sorted({item["group"] for item in items if item["group"]}):
        pool = [item for item in items if item["group"] == group]
        kept = lambda word, pool=pool: kept_for(word, pool, block)
        slopes = block["groupSlopes"][group]
        # R339. A group a build by keyRules builds has its levels measured as the build gives them
        lv = (levels or {}).get(group)
        for item in pool:
            entry = clean_entry(item["row"], block["entryRampM"])
            road = road_mean(book.get(item["plot"], {}), ROAD)
            depth = ramp_outline.setback(profile, item["rev9"] or item["code"])
            item["factors"]["measuredBand"] = band(item["row"]["rampSlopePercent"], block["slopeBands"])
            item["factors"]["heightOverRoad"] = None if entry is None or road is None else band((entry - road) / depth * 100.0, block["slopeBands"])
            item["factors"]["heightBand"] = band(item["row"]["height"], block["heightBands"])
            item["depth"] = depth
        neighbour_votes(kept("rampSlope"), lambda item: slope_class(item, block))
        # R273. Ramp type and slope, flat ramps included
        # A group whose ramp types name no slope is measured, so the measured band would only repeat its target
        factors = [name for name in ("all", "typology", "measuredBand", "steps", "heightOverRoad", "corner", "depthBand", "neighbour", "key", "typologyOrEntryRefused")
                   if not (name == "measuredBand" and group in block["measuredSlopeGroups"])]
        # R334. The key, and whether the key left El @ Entry unset, are values of the key itself, so they are checks a new build cannot code
        record(group + " rampSlope", kept("rampSlope"), lambda item: slope_class(item, block), {name: name for name in factors}, by_group, "rampType",
               checks=("key", "typologyOrEntryRefused"))
        # R334. What the flat ramps share, and the ramp outline of R127 against the corners of the key ramp
        extras["flatRamps"][group] = key_rules.flat_ramps(kept("rampSlope"), block["rampTypeClass"][block["flatRampType"]], lambda item: slope_class(item, block))
        ramps = [item for item in kept("rampOutline") if key_rules.ramp_target(item) is not None]
        record(group + " rampOutline", ramps, key_rules.ramp_target, {"outlineR127": key_rules.ramp_rule(profile)}, by_group, "rampOutline",
               key_rules.corner_hold(block["outlineHoldMm"] / 1000.0), gap=key_rules.corner_gap)
        # R274. Hand, per link file, apart for villas, units in a cluster and units alone
        # R333. The clusters of the CAD analysis alone beside the footprint clusters (Q040), the pairs of the rows of units in no cluster, the villas between cluster units.
        # A tie goes to the candidate listed first, so blockMirrorOpenEnd, the rule Round 17 coded, stays ahead of blockMirrorRoadSide
        # R337. Learned and coded per file the key links, judged by the table of the file a build links, so a miss is a plot a build does not reproduce
        built_file = lambda item: (item["group"], item.get("buildFile") or item["file"])
        for unit_class in ("villa", "cluster", "alone"):
            chosen = [item for item in kept("hand") if item["row"]["mirrored"] is not None and hand_class(item) == unit_class]
            record(group + " hand " + unit_class, chosen, lambda item: item["row"]["mirrored"],
                   {name: name for name in ("all", "blockMirror", "clusterSide", "clusterSideAnalysisOnly", "clusterIndexParity", "openEnd", "rowParity", "orientation",
                                            "roadSide", "blockMirrorOpenEnd", "blockMirrorRoadSide", "rowPairSide", "rowPairSideOrBlockMirrorOpenEnd")}, by_file, "mirrored",
                   extra_by={"betweenClusters": lambda item: item.get("betweenClusters")}, judge_scope=built_file)
        if lv is None:
            # R275. El @ Entry: the road level at the front corners of a surface plus the slope of the unit times the ramp depth, as is and less a datum
            entries = [item for item in kept("entry") if clean_entry(item["row"], block["entryRampM"]) is not None]
            target = lambda item: clean_entry(item["row"], block["entryRampM"])
            formulas = {}
            for surface in surfaces:
                def guess(item, surface=surface):
                    road = road_mean(book.get(item["plot"], {}), surface)
                    return None if road is None else road + slopes.get(item["code"], slopes["default"]) / 100.0 * item["depth"]
                gaps = [target(item) - guess(item) for item in entries if guess(item) is not None]
                datum = statistics.median(gaps) if gaps else 0.0
                by_key = defaultdict(list)
                for item in entries:
                    if guess(item) is not None:
                        by_key[item["key"]].append(target(item) - guess(item))
                key_datum = {key: statistics.median(values) for key, values in by_key.items()}
                formulas[surface] = guess
                formulas[surface + " less datum %+.3f" % datum] = lambda item, guess=guess, datum=datum: None if guess(item) is None else guess(item) + datum
                formulas[surface + " less the datum of each key"] = lambda item, guess=guess, key_datum=key_datum: None if guess(item) is None else guess(item) + key_datum[item["key"]]
                if surface in block["datumSurfaces"]:
                    datums.setdefault(group + " entry", {})[surface] = {key: common.fixed(value, 4) for key, value in sorted(key_datum.items())}
            record(group + " entry", entries, target, formulas, by_group, "entryM", level_hold)
            # R275. The ramp front, the mean of its two low corners, against every road surface as it is and less the datum of each key
            fronts = [item for item in kept("rampFront") if item["row"]["rampFront"] is not None]
            front_target = lambda item: item["row"]["rampFront"]
            front_formulas = {}
            for surface in surfaces:
                def road(item, surface=surface):
                    return road_mean(book.get(item["plot"], {}), surface)
                by_key = defaultdict(list)
                for item in fronts:
                    if road(item) is not None:
                        by_key[item["key"]].append(front_target(item) - road(item))
                key_datum = {key: statistics.median(values) for key, values in by_key.items()}
                if surface in block["datumSurfaces"]:
                    datums.setdefault(group + " rampFront", {})[surface] = {key: common.fixed(value, 4) for key, value in sorted(key_datum.items())}
                front_formulas[surface] = road
                front_formulas[surface + " less the datum of each key"] = lambda item, road=road, key_datum=key_datum: None if road(item) is None else road(item) + key_datum[item["key"]]
            record(group + " rampFront", fronts, front_target, front_formulas, by_group, "rampFrontM", level_hold)
        else:
            # R339. El @ Entry, the ramp front, the split, the base, the FFL of each piece, the steps and the Prototype-FFL parameter as a build gives them
            key_levels.measure(record, group, pool, kept, lv, profile, block, book, surfaces, extras, lambda row: clean_entry(row, block["entryRampM"]), parameter,
                               misses, slopes)
        # R275. The fall along the front: both front corners level, or each on the road at its corner
        falls = [item for item in kept("rampFrontFall") if item["row"]["rampFrontFall"] is not None]
        road_fall = lambda item: (None if road_corners(book.get(item["plot"], {})) is None else abs(road_corners(book.get(item["plot"], {}))[0] - road_corners(book.get(item["plot"], {}))[1]))
        record(group + " rampFrontFall", falls, lambda item: item["row"]["rampFrontFall"], {"levelFront": lambda item: 0.0, "roadAtEachCorner": road_fall},
               by_group, "rampFrontFallM", level_hold)
        if lv is None:
            # R275. FFL less El @ Entry and the steps
            heights = [item for item in kept("height") if clean_entry(item["row"], block["entryRampM"]) is not None and item["row"]["ffl"] is not None]
            height = lambda item: common.fixed(item["row"]["ffl"] - clean_entry(item["row"], block["entryRampM"]), 4)
            tops = cluster_top(heights, profile, block)
            from_levels = levels_rule(heights, clusters, profile, block)
            record(group + " height", heights, height, {
                "typology": "typology", "typologySteps": "typologySteps", "key": "key",
                "baseAndRisers": lambda item: None if item["factors"]["steps"] is None else common.fixed(base_height(profile, item["code"]) + riser * (int(item["factors"]["steps"]) - 1), 4),
                "baseOnly": lambda item: base_height(profile, item["code"]),
                "oneFflPerClusterAtTheHighestEntry": lambda item: tops.get(id(item), base_height(profile, item["code"]) if not item["role"] else None),
                "levelsPyFromEntry": lambda item: from_levels.get(id(item)),
            }, by_group, "heightM", lambda guess, aim: abs(guess - aim) <= hold_m + 1e-9, checks=("typologySteps", "baseAndRisers"))
            with_steps = [item for item in kept("steps") if item["factors"]["steps"] is not None]
            record(group + " steps", with_steps, lambda item: item["factors"]["steps"], {
                "typology": "typology", "heightBand": "heightBand",
                "stepsFromHeight": lambda item: None if item["row"]["height"] is None else str(1 + max(0, round((item["row"]["height"] - base_height(profile, item["code"])) / riser))),
            }, by_group, "steps")
        # R276. Generic floor on the whole face, yard, CASE and the other link parameters
        record(group + " generic", kept("generic"), lambda item: item["factors"].get("wholeFace"), {name: name for name in ("all", "typology", "key")}, by_group, "genericFloor",
               checks=("key",))
        for item in pool:
            item["factors"]["yardDepth"] = yard_depth(item["row"])
        # R334. A new build has a zone and a neighbourhood but no key, so a split by area is coded as a setting per area
        record(group + " yard", kept("yard"), lambda item: item["factors"]["yardDepth"], {name: name for name in ("all", "typology", "key", "zone", "nbh")}, by_group, "yard",
               checks=("key",))
        if key_plans and outlines is not None:
            # R334. The yard outline of R135, as fitted on the IMC key and fitted again on every key of the group
            frames = {item["plot"]: item.get("frame") for item in pool}
            rows = key_rules.outline_rows({key: plan for key, plan in key_plans.items() if key in block["groups"][group]}, frames, step)
            if rows:
                refit = floor_fit.fit_groups(list(rows.values()), ("unit", "mirrored", "width"), block["check"]["seeds"][0])
                extras["yardOutlineGroups"][group] = {"fitted": len(outlines["outlines"]["yard"]), "refit": len(refit),
                                                       "refitLoops": [{name: fitted[name] for name in ("unit", "mirrored", "width", "type", "plots", "fromPlot", "loops")} for fitted in refit]}
                candidates = {"fittedOutline": key_rules.outline_rule(key_rules.outline_groups(outlines["outlines"]["yard"]), rows),
                              "fittedOutlineRefit": key_rules.outline_rule(key_rules.outline_groups(refit), rows)}
                yards = [item for item in kept("yardOutline") if (item["key"], item["plot"]) in rows]
                record(group + " yardOutline", yards, key_rules.outline_target(rows), candidates, by_group, "yardOutline",
                       key_rules.shape_hold(block["outlineHoldMm"] / 1000.0), gap=key_rules.shape_gap)
        # R276, R335. Each shared parameter against the factors and the formulas a new build can give it, a level parameter against the levels of the key
        formulas = key_rules.parameter_formulas(block["linkNameFacade"])
        for name in block["parameterTests"]:
            chosen = [item for item in kept("parameter") if parameter(item, name) is not None]
            record(group + " parameter " + name, chosen, lambda item, name=name: parameter(item, name),
                   {**{label: label for label in ("all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps", "clusterSide", "corner")}, **formulas},
                   by_group, "parameter " + name)
        for name in (block["parameterLevels"] if lv is None else []):
            chosen = [item for item in kept("parameter") if key_rules.number(parameter(item, name)) is not None]
            record(group + " parameter " + name, chosen, lambda item, name=name: float(parameter(item, name)),
                   {"genericTop": lambda item: item["row"].get("genericTop"), "ffl": lambda item: item["row"].get("ffl")}, by_group, "parameter " + name,
                   key_rules.level_hold(hold_m), gap=key_rules.level_gap)
        depth = group_yard_depth(profile, group)
        if key_plans and faces is not None and depth is not None:
            # R335. The boundary walls of the key plans of the group: base, top and line, their misses listed in the report only
            walls, counts = key_rules.wall_items(key_plans, block["groups"][group], group, block["wallTypes"])
            extras["walls"][group] = {"byKey": counts, "items": len(walls)}
            # R335. A wall whose base level no floor of its plan names has no base, it is listed and held by no level decision
            extras["wallsNoBaseLevel"][group] = key_rules.no_base_level(walls)
            record(group + " wallBase", walls, key_rules.wall_base, key_rules.wall_base_candidates(depth), by_group, "wallBase", key_rules.level_hold(hold_m),
                   gap=key_rules.level_gap, extra_by={"plots": lambda item: item["factors"]["plots"]})
            record(group + " wallTop", walls, key_rules.wall_top, key_rules.wall_top_candidates(profile), by_group, "wallTop", key_rules.level_hold(hold_m),
                   gap=key_rules.level_gap, extra_by={"plots": lambda item: item["factors"]["plots"]})
            record(group + " wallLine", [item for item in walls if key_rules.wall_line_target(faces)(item) is not None], key_rules.wall_line_target(faces),
                   {"centreOnTheEdge": key_rules.wall_line_rule(block["wallCentreCodes"], profile.metres("plot.boundary_wall_thickness"))}, by_group, "wallLine",
                   key_rules.level_hold(block["outlineHoldMm"] / 1000.0), gap=key_rules.level_gap, extra_by={"locationLine": lambda item: item["factors"]["locationLine"]})
            by_plot = defaultdict(list)
            for item in walls:
                for plot in item["wall"].get("plots") or []:
                    by_plot[(item["key"], plot)].append(item["wall"])
            covers = []
            # R335. A kind of majority.kindLeaveOut that holds no front wall, the SE keys, is left out of the open front
            fronted = {id(item) for item in kept("frontOpen")}
            for item in pool:
                if item["key"] in key_plans and id(item) in fronted:
                    cover = key_rules.front_cover(item.get("frame"), by_plot.get((item["key"], item["plot"]), []), wall_edge)
                    item["factors"]["frontOpen"] = None if cover is None else "open" if cover <= block["frontOpenShare"] + 1e-9 else "walled"
                    if cover is not None:
                        covers.append(round(cover, 2))
            extras["frontCover"][group] = dict(sorted(Counter(covers).items()))
            extras["frontWalled"][group] = sorted((item["key"] + " " + item["plot"] for item in pool if item["factors"].get("frontOpen") == "walled"), key=str)
            record(group + " frontOpen", [item for item in pool if item["factors"].get("frontOpen")], lambda item: item["factors"]["frontOpen"],
                   {"all": "all", "typology": "typology"}, by_group, "frontOpen")
            extras["wallMisses"][group] = [{name: miss.get(name) for name in ("decision", "key", "elementId", "plots", "state", "overrideValue")}
                                           for miss in misses if miss["decision"].split(" ")[1] in ("wallBase", "wallTop", "wallLine")]
        if frame_units is not None:
            # R332. The link rotation by the front frame, apart on the corner plots of the corner rule (Q037), and the link origin by plotFrame
            links = [item for item in kept("linkRotation") if item["row"].get("rotationDeg") is not None and item["row"].get("mirrored") is not None]
            rotation_hold = key_rules.angle_hold(block["rotationHoldDeg"])
            by_rule = {"frontRule": lambda item: item.get("frontRule")}
            record(group + " linkRotation", links, lambda item: item["row"]["rotationDeg"], key_rules.rotation_candidates(), by_group, "rotationDeg", rotation_hold,
                   gap=key_rules.rotation_gap, extra_by=by_rule)
            held = {id(item) for item in links}
            record(group + " frontCorner", [item for item in kept("frontCorner") if id(item) in held and item.get("frontRule") == block["cornerFrontRule"]],
                   lambda item: item["row"]["rotationDeg"], key_rules.rotation_candidates(), by_group, "rotationDeg", rotation_hold)
            origins = [item for item in kept("linkOrigin") if id(item) in held and key_rules.origin_target(item) is not None]
            extras["linkFits"][group] = key_rules.refit(origins)
            record(group + " linkOrigin", origins, key_rules.origin_target, key_rules.origin_candidates(frame_units, extras["linkFits"][group], profile.get("typologies")),
                   by_group, "insert", key_rules.point_hold(block["linkHoldMm"] / 1000.0), gap=key_rules.insert_gap, extra_by=by_rule)
    return results, misses, datums, extras


def check_keys(answer, rows, reads, plans, groups, references, allowed):
    """R272, R331, R335. Every answer key of answerKeys must have rows in past_all.csv, a key read and a key plan, and every group
    read with --key-read its reference key of majority.siteReference, else a key or a decision would be left out in silence. A key
    --allow-missing-key names may be missing, and the coverage of every decision says what it holds."""
    missing = []
    for label, found in (("rows in past_all.csv", rows), ("--key-read", reads), ("--key-plan", plans)):
        gone = sorted(key for key in answer if key not in found and key not in allowed)
        if gone:
            missing.append(label + " of " + ", ".join(gone))
    for group, reference in sorted(references.items()):
        if any(key in reads for key in groups.get(group, ())) and reference not in reads and reference not in allowed:
            missing.append("--key-read of " + reference + ", the reference key of " + group)
    if missing:
        raise project_profile.ProfileError("answer keys missing: " + " | ".join(missing) + ". Give them or name each with --allow-missing-key KEY (R272)")


def level_groups(profile, block):
    """R339. The groups a build by keyRules builds, by the level profile whose job each is, so their levels are measured as that
    build gives them."""
    found = {}
    for name, lp in (profile.get("levelProfiles") or {}).items():
        if isinstance(lp, dict) and lp.get("keyRules") and lp.get("job") in block["groups"]:
            found[lp["job"]] = name
    return found


def level_road_names(groups, profile, settings):
    """R339. The road surfaces the levels of the groups a build by keyRules builds are measured on, those plan_rules.road_names gives the
    level profile of each, the setting a build asks. Groups that ask other surfaces are refused, one run measures on one road."""
    found = {tuple(plan_rules.road_names(settings.get("siteBuild"), plan_rules.level_profile(profile, name))) for name in groups.values()}
    if len(found) > 1:
        raise project_profile.ProfileError("the level profiles of %s ask other road surfaces, %s, a run measures the levels on one road (R339)"
                                           % (", ".join(sorted(groups)), " and ".join(", ".join(names) for names in sorted(found))))
    return list(next(iter(found))) if found else []


def need_roads(groups, landxml, scratch):
    """R339. The levels of a group a build by keyRules builds are held against the road the build gives at the front corners, so
    the run needs the LandXML surfaces the build asks and a scratch folder for their reader, else it is refused."""
    if groups and (not landxml or not scratch):
        raise project_profile.ProfileError("the levels of %s are measured on the road a build gives, give --landxml and --scratch (R339)" % ", ".join(sorted(groups)))


def site_context(rows, book, number_of, faces, roads, shapes, analysis, sales, profile, settings, block, surfaces=(), snap=0.0, extra=None):
    """R271. The context of every row of past_all.csv from the site as a build sees it: the front of every unit plot of plots.csv
    with a face, the clusters of the footprint blocks over every unit plot joined with those of the CAD analysis, their sides by the
    faces of every plot, the side neighbours, the side road, the row pairs and the villas between cluster units. A plot no key holds,
    such as a cut face, keeps its front, so a row of units is walked through it as a build walks it (Round ADDIN 3a review).
    Returns the items, the clusters, the fronts, the plots of the table and the tree of the road edges. R339: with surfaces and snap
    the fronts hold the road level a build gives at their two front corners, and extra, a dict, takes the roles of the clusters and
    the row partner of every unit in no cluster."""
    numbers = sorted({row["plot"] for row in rows}, key=link_summary.sort_key)
    units = [number for number, data in book.items() if data.get("typologyRev9") and number in faces]
    site = sorted(set(numbers) | set(units), key=link_summary.sort_key)
    clusters = plan_from_book.joined_clusters(plan_from_book.footprint_clusters(site, book, faces, shapes), plan_from_book.numbered_clusters(analysis, number_of))
    # A face with two plan numbers, such as 476|477, is no plot of its own and leaves its cluster
    clusters = [cluster for cluster in ({**cluster, "plots": [plot for plot in cluster["plots"] if plot.isdigit()]} for cluster in clusters) if len(cluster["plots"]) > 1]
    corner = settings.get("siteBuild")["corner"]
    front = plan_from_book.fronts(site, book, faces, roads, cad_links.front_limits(profile, settings.get("analysis")), list(surfaces), snap, profile, corner, sales)
    frames = {number: item["frame"] for number, item in front.items()}
    roles = cad_links.cluster_roles(clusters, 0, faces, frames)
    neighbours = side_neighbours(units, faces, frames, block["sideShare"], profile.get("plot.long_side") / 1000.0)
    tree = STRtree(roads)
    side_roads = {number: road_side(faces.get(number), frames.get(number), tree, roads, corner["roadEdgeM"]) for number in numbers}
    items = contexts(rows, book, sales, faces, roles, neighbours, side_roads, profile, block)
    # R333. The sides of the clusters of the CAD analysis alone (Q040), the pairs of the rows of units in no cluster and the villas between cluster units
    analysis_only = [cluster for cluster in ({**cluster, "plots": [plot for plot in cluster["plots"] if plot.isdigit()]} for cluster in plan_from_book.numbered_clusters(analysis, number_of))
                     if len(cluster["plots"]) > 1]
    analysis_roles = cad_links.cluster_roles(analysis_only, 0, faces, frames)
    pairs = key_rules.row_pairs(units, neighbours, lambda plot: pair_kind(plot, book, profile, block), lambda plot: plot in roles, block["pairKinds"])
    for item in items:
        factors = item["factors"]
        factors["clusterSideAnalysisOnly"] = analysis_roles[item["plot"]]["side"] if item["plot"] in analysis_roles else "alone"
        factors["rowPairSide"] = (pairs.get(item["plot"]) or {}).get("side")
        plan_rules.joined_factors(factors)
        item["betweenClusters"] = key_rules.between_clusters(item["neighbours"], lambda plot: plot in roles)
    if extra is not None:
        extra.update({"roles": roles, "partners": plan_rules.row_partners({plot: found.get("side") for plot, found in pairs.items()}, neighbours)})
    return items, clusters, front, numbers, tree


def overrides(misses, block, models):
    """R272. The override list: every miss of the decisions block.overrideDecisions names, with no real level or coordinate,
    sorted by plot. R330: source is the job of the key, its group of majority.groups, and model the past model file of the key."""
    jobs = override_source.job_of_key(block["groups"])
    kept = []
    for miss in misses:
        decision = miss["decision"].split(" ", 1)[1]
        if not any(decision.startswith(name) for name in block["overrideDecisions"]):
            continue
        if miss["key"] not in models:
            # R330. model names the past model file, a key name in its place would pass as a file
            raise project_profile.ProfileError("key %s has misses for the override list and no past model file, give --model %s FILE (R330)" % (miss["key"], miss["key"]))
        value, rule = miss["value"], miss["rule"]
        if miss["field"] in ("heightM", "entryM", "rampFrontM", "rampFrontFallM"):
            value, rule = (None if rule is None else {"gapToRuleM": common.fixed(value - rule, 4)}), None
        if "overrideValue" in miss:
            # R332. A gap the decision gives in its own terms, the link axes or degrees, never a coordinate
            value, rule = miss["overrideValue"], None
        kept.append({"plot": miss["plot"], "field": miss["field"], "value": value, "rule": rule, "source": jobs.get(miss["key"]),
                     "model": models[miss["key"]], "key": miss["key"], "decision": miss["decision"], "reason": miss["reason"]})
    return sorted(kept, key=lambda item: (link_summary.sort_key(item["plot"]), item["key"], item["field"]))


def split_cannot(listing):
    """R272, R330. The entries of a run with a value to write, and the misses with none, a cannot of a rule that gives no value to hold
    a gap to, listed apart with no value and no rule and never written as an override, so no build reads a null as a value."""
    written = [item for item in listing if item["value"] is not None]
    cannot = [{name: value for name, value in item.items() if name not in ("value", "rule")} for item in listing if item["value"] is None]
    return written, cannot


def final_cannot(cannot, block, dry, path, keep, replace, keep_from, joined):
    """R272, R330. The cannot list a run writes beside the override list: a dry run its own, else the cannot entries of the earlier
    list the run keeps, by the same keep as its overrides, on plots the join gives their key, and every cannot of the run it does not
    keep and no hand edit of the earlier lists holds the plot, key and field of, sorted as the list."""
    if dry:
        return cannot
    source = Path(keep_from or path)
    data = override_source.read_list(source) if source.exists() else {"overrides": []}
    earlier = [override_source.migrate_entry(item, block["groups"]) for item in data["overrides"]]
    chosen = override_source.keep_of(block, keep, replace, earlier)
    kept = [item for item in data.get("cannot") or [] if override_source.kept_by(item, chosen)
            and (joined is None or (item.get("key"), str(item["plot"])) in joined)]
    replaced = override_source.read_list(path)["overrides"] if keep_from and Path(path).exists() else []
    hands = {override_source.edit_key(item) for item in earlier + replaced if override_source.hand_edit(item)}
    new = [item for item in cannot if not override_source.kept_by(item, chosen) and override_source.edit_key(item) not in hands]
    return sorted(kept + new, key=lambda item: (link_summary.sort_key(str(item["plot"])), str(item.get("key")), str(item.get("field"))))


def final_list(listing, block, dry, path, keep, replace, keep_from, joined):
    """R330. The override list a run writes. A dry run writes the list of the run alone and never into the repo, where the project
    list would lose every entry the run does not measure. Else the run merges with the earlier list, keep_from or the list at path:
    it keeps the sources and the laterFields of majority.overrideKeep, with --keep added and --replace taken out, and drops a kept
    entry of a plot the join gives no row of its key (joined, the key and plot pairs of past_all.csv)."""
    if dry:
        try:
            plot_relations.refuse_inside_repo(Path(path).resolve().parent)
        except ValueError as error:
            raise project_profile.ProfileError("--dry writes the list of the run alone, " + str(error) + " (R330)") from error
        return listing, None
    source = Path(keep_from or path)
    earlier = [override_source.migrate_entry(item, block["groups"]) for item in override_source.read_list(source)["overrides"]] if source.exists() else []
    if keep_from and not source.exists():
        raise project_profile.ProfileError("--keep-from " + str(source) + " is no file (R330)")
    if keep_from and Path(path).exists() and Path(path).resolve() != source.resolve():
        # R330. The run writes over the list at path, so the hand edits of that list are read too, never lost with it
        replaced = [override_source.migrate_entry(item, block["groups"]) for item in override_source.read_list(path)["overrides"]]
        earlier = override_source.with_hand_edits(earlier, replaced)
    chosen = override_source.keep_of(block, keep, replace, earlier)
    merged, counts = override_source.merge(earlier, listing, chosen, joined)
    counts["keep"] = override_source.describe(chosen)
    counts["earlier"] = str(source) if source.exists() else None
    return merged, counts


def check_measured_rounding(counts, block, profile):
    """R385. A run measures the level overrides on R385 only, so it never writes the overrides of a job whose level profile
    majority.overrideRounding names another rounding, native2022 measured in Round 17 on round(): counts, the merge counts of the run,
    None for a dry run, which writes outside the repo."""
    named = (block.get("overrideRounding") or {}).get("levelProfiles") or {}
    written = set(((counts or {}).get("new") or {}))
    wrong = sorted("%s of %s on %s" % (lp.get("job"), name, named.get(name)) for name, lp in (profile.get("levelProfiles") or {}).items()
                   if isinstance(lp, dict) and lp.get("job") in written and named.get(name) != "R385")
    if wrong:
        raise project_profile.ProfileError("the run writes overrides of %s, measured on R385, where majority.overrideRounding names another rounding: keep them, "
                                           "or name R385 for the profile with --replace (R385)" % ", ".join(wrong))


def final_skip(path, keep_from, plots=None):
    """R386. The skip list a run writes: the skip of the list at path it writes over and of --keep-from, kept whole and never measured,
    sorted by plot, a plot the two lists skip otherwise refused with both named, and with plots, the plot numbers of plots.csv, a plot
    that is none of them refused with its name. None when neither list holds a skip."""
    found, lists = {}, []
    for source in [Path(path)] + ([Path(keep_from)] if keep_from else []):
        if not source.exists():
            continue
        data = override_source.read_list(source)
        if data.get("skip") is None:
            continue
        override_source.check_skip(data["skip"], source, plots)
        lists.append(source)
        for item in data["skip"]:
            other = found.get(item["plot"])
            if other is not None and other != item:
                raise project_profile.ProfileError("plot %s is skipped otherwise in %s: %s against %s. Edit one of the two by hand (R386)"
                                                   % (item["plot"], " and ".join(str(name) for name in lists), json.dumps(other), json.dumps(item)))
            found[item["plot"]] = item
    return None if not lists else [found[plot] for plot in sorted(found, key=link_summary.sort_key)]


def overrides_text(note, listing, cannot=None, skip=None):
    """The text of the override list with one override per line, so Bader can find and edit a plot by hand, with cannot the misses
    with no value after it, one per line, listed and never applied (R272), and with skip the plots the check table skips after that
    (R386), read back as JSON before anything is written."""
    block = lambda items: ",\n".join("    " + json.dumps(item, ensure_ascii=False) for item in items) + ("\n" if items else "")
    text = "{\n  \"note\": " + json.dumps(note, ensure_ascii=False) + ",\n  \"overrides\": [\n" + block(listing) + "  ]"
    text += "" if cannot is None else ",\n  \"cannot\": [\n" + block(cannot) + "  ]"
    text += ("" if skip is None else ",\n  \"skip\": [\n" + block(skip) + "  ]") + "\n}\n"
    json.loads(text)
    return text


def write_overrides(path, note, listing, cannot=None, skip=None):
    """The override list of overrides_text written to path."""
    text = overrides_text(note, listing, cannot, skip)
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(text.encode("utf-8"))


def finish(args, block, book, report, listing, cannot):
    """R272, R386. The end of a run: every check first, the skip list of the earlier lists against the plots of plots.csv, the text of
    the list and the text of the report, which R385 refuses for a value it can not round, and only then majority.json and the list, so
    a refused run leaves both as they were."""
    skip = final_skip(args.overrides, args.keep_from, set(book))
    listed = overrides_text(block["overrideNote"], listing, cannot, skip)
    written = json.dumps(common.round_tree(report), indent=1, ensure_ascii=False)
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(written, encoding="utf-8")
    Path(args.overrides).parent.mkdir(parents=True, exist_ok=True)
    Path(args.overrides).write_bytes(listed.encode("utf-8"))


def load_block(settings):
    block = settings.get("majority")
    needed = ("strongShare", "levelHoldMm", "entryRampM", "groups", "codeFrom", "kindByPrefix", "slopeBands", "rampTypeClass", "groupSlopes", "depthBands",
              "heightBands", "sideShare", "parameterTests", "overrideDecisions", "wholeFaceShare", "overrideNote", "measuredSlopeGroups", "datumSurfaces", "kindLeaveOut",
              "rotationHoldDeg", "linkHoldMm", "cornerFrontRule", "siteReference", "check", "outlineHoldMm", "pairKinds", "flatRampType", "wallTypes", "wallCentreCodes",
              "frontOpenShare", "linkNameFacade", "parameterLevels", "overrideKeep", "linkPlotParameter", "levelPassMm", "levelBandsMm", "rampFrontCorners")
    if not isinstance(block, dict) or any(key not in block for key in needed):
        raise project_profile.ProfileError(settings.path.name + " majority must name " + ", ".join(needed))
    leave = block["kindLeaveOut"]
    named = {"parameter " + name for name in block.get("parameterTests", []) + block.get("parameterLevels", [])}
    if not isinstance(leave, dict) or not all(isinstance(words, list) and all(word in DECISIONS or word in named for word in words) for words in leave.values()):
        raise project_profile.ProfileError(settings.path.name + " majority.kindLeaveOut must map a model kind to decisions of " + ", ".join(DECISIONS))
    # R326. A kind spelled otherwise than answerKeys.kinds would match no row and leave nothing out
    rule = past_all.kind_rule(settings)
    wrong = [kind for kind in leave if past_all.canonical_kind(kind, rule) != kind]
    if wrong:
        raise project_profile.ProfileError(settings.path.name + " majority.kindLeaveOut names " + ", ".join(wrong) + ", which is no kind of answerKeys.kinds "
                                           + ", ".join(rule["kinds"]))
    return block


def read_rows(path, settings, block):
    """R326. The rows of past_all.csv, each kind held against answerKeys.kinds and every row of a group of majority.kindGroups with a kind."""
    return past_all.check_kinds(past_all.read_table(path), past_all.kind_rule(settings), block["groups"])


def main(argv=None):
    parser = argparse.ArgumentParser(description="The majority rule on every past plot, per decision (R271 to R276).")
    parser.add_argument("table", help="past_all.csv")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--plots-csv", required=True)
    parser.add_argument("--baseplan", required=True)
    parser.add_argument("--roads", required=True)
    parser.add_argument("--analysis", required=True)
    parser.add_argument("--footprint-blocks", required=True)
    parser.add_argument("--sales", required=True)
    parser.add_argument("--model", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="the past model file of a key, the override source")
    parser.add_argument("--out", required=True, help="the report, outside the repo")
    parser.add_argument("--overrides", required=True, help="the override list to write")
    parser.add_argument("--keep", nargs="*", default=[], help="R330. SOURCE or SOURCE:FIELD,FIELD of the earlier list to keep as it is, besides majority.overrideKeep")
    parser.add_argument("--replace", nargs="*", default=[], help="R330. SOURCE or SOURCE:FIELD,FIELD of majority.overrideKeep the run writes again")
    parser.add_argument("--dry", action="store_true", help="R330. write the list of the run alone, outside the repo only")
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH), help="R332. prototype_offsets.json with the plotFrame fit")
    parser.add_argument("--key-read", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="R331. the reader output of a key, its site facts")
    parser.add_argument("--key-plan", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="R334. the plan of a key of R325, its yard outlines")
    parser.add_argument("--floors", default=str(floor_fit.DEFAULT_PATH), help="R334. floor_outlines.json with the yard outlines of R135")
    parser.add_argument("--keep-from", help="R330. the earlier override list --keep reads, --overrides when left out")
    parser.add_argument("--allow-missing-key", action="append", default=[], metavar="KEY", help="R272. an answer key the run may lack rows, a read or a plan of")
    parser.add_argument("--landxml", help="R339. the landxml folder of the snapshot, the road surfaces a build asks for the levels of a group it builds by keyRules")
    parser.add_argument("--scratch", help="R339. a folder outside the repo for the LandXML reader")
    args = parser.parse_args(argv)
    try:
        return run_args(args)
    except project_profile.ProfileError as error:
        print("majority: " + str(error), file=sys.stderr)
        return 2


def run_args(args):
    """R271 to R276, R330 to R335. The run of main once its arguments are read, a refusal raised as ProfileError."""
    settings = project_profile.load_settings(args.settings)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = load_block(settings)
    plot_relations.refuse_inside_repo(Path(args.out).resolve().parent)
    rows = read_rows(args.table, settings, block)
    book, number_of = plan_from_book.book_rows(args.plots_csv)
    by_label, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    faces = {number_of[label]: shape for label, shape in by_label.items() if label in number_of}
    sheet = json.loads((project_profile.ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["locationPlan"]["sales"]
    sales = corner_front.sales_rows(args.sales, sheet["sheet"], sheet["columns"]["plot"])
    roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
    analysis = json.loads(Path(args.analysis).read_text(encoding="utf-8"))["clusters"]
    shapes = footprint_check.block_shapes(footprint_check.footprint_blocks(args.footprint_blocks, settings))
    # R339. The groups a build by keyRules builds have their levels held against the road that build gives at the front corners
    built_levels = level_groups(profile, block)
    need_roads(built_levels, getattr(args, "landxml", None), getattr(args, "scratch", None))
    road_surfaces, snap = [], 0.0
    if built_levels:
        plot_relations.refuse_inside_repo(Path(args.scratch).resolve())
        road_surfaces = plan_from_book.load_roads([Path(args.landxml) / (name + ".xml") for name in level_road_names(built_levels, profile, settings)], args.scratch)
        snap = settings.get("siteBuild")["snapM"]
    site_extra = {}
    items, clusters, front, numbers, tree = site_context(rows, book, number_of, faces, roads, shapes, analysis, sales, profile, settings, block, road_surfaces, snap, site_extra)
    levels = {group: {"lp": plan_rules.level_profile(profile, name), "roles": site_extra["roles"], "partners": site_extra["partners"],
                      "site": {plot: {"front": found["front"], "depth": found.get("depth"), "unit": found.get("unit")} for plot, found in front.items()}}
              for group, name in built_levels.items()}
    # R337. The file a build links for the code of each plot of a group a level profile with keyRules builds, as plan_from_book.py finds it
    built_groups = {found.get("job") for found in (profile.get("levelProfiles") or {}).values() if isinstance(found, dict) and found.get("keyRules")}
    if built_groups & {item["group"] for item in items}:
        folder, _ = plan_from_past.prototype_folders(plan_from_past.input_root(settings), plan_from_past.load_job(settings))
        files = plan_from_book.folder_files(folder, profile)
        for item in items:
            if item["group"] in built_groups:
                item["buildFile"] = files.get(item["code"])
    for item in items:
        outline, face = item["row"]["genericOutline"], faces.get(item["plot"])
        if outline and face is not None:
            shape = wkt.loads(outline)
            union = shape.union(face).area
            item["factors"]["wholeFace"] = "wholeFace" if union and shape.intersection(face).area / union >= block["wholeFaceShare"] else "partOfFace"
        else:
            item["factors"]["wholeFace"] = None
    surfaces = [ROAD] + sorted({column[len("level_"):-len("_left")] for column in next(iter(book.values())) if column.startswith("level_") and column.endswith("_left")})
    curbs = {number: cad_links.front_frame(faces.get(number), tree, roads, cad_links.front_limits(profile, settings.get("analysis"))) for number in numbers}
    for item in items:
        found = front.get(item["plot"]) or {}
        item["frame"], item["frontRule"], item["curbFrame"] = found.get("frame"), found.get("frontRule"), curbs.get(item["plot"])
    # R272. A key of answerKeys with no rows, read or plan, or a group read without its reference, is refused unless named
    check_keys(sorted(key["code"].lower() for key in settings.get("answerKeys")["keys"]), {row["key"] for row in rows}, {key for key, _ in args.key_read},
               {key for key, _ in args.key_plan}, block["groups"], block["siteReference"], args.allow_missing_key)
    plans = {key: json.loads(Path(path).read_text(encoding="utf-8")) for key, path in args.key_plan}
    # R335. The level parameters of majority.parameterLevels from each key read, which past_all.csv does not list. Only those, since a
    # read is found by its plot number parameter, so its plot number would hold by construction
    reads = {key: ramp_levels.host_document(link_summary.load(path)) for key, path in args.key_read}
    read_found = key_rules.read_parameters(reads, block["linkPlotParameter"])
    for item in items:
        found = read_found.get((item["key"], item["plot"])) or {}
        item["readParameters"] = {name: found[name] for name in block["parameterLevels"] if name in found}
    # R334. The plots whose El @ Entry parameter the plan of the key refused as a placeholder (R325)
    refused = key_rules.entry_refused(plans)
    for item in items:
        if item["key"] in plans:
            item["factors"]["entryRefused"] = "yes" if (item["key"], item["plot"]) in refused else "no"
            item["factors"]["typologyOrEntryRefused"] = "entryRefused" if (item["key"], item["plot"]) in refused else item["factors"]["typology"]
    results, misses, datums, extras = run(items, book, profile, block, surfaces, clusters, prototype_offsets.plot_frame(prototype_offsets.load(args.offsets))["units"],
                                          plans, floor_fit.load(args.floors), floor_fit.width_step(settings), faces, settings.get("keyPlans.wallEdgeM"), levels)
    # R272. A miss with no value is listed in cannot, never written as an override with a null value
    listing, cannot = split_cannot(overrides(misses, block, dict(args.model)))
    joined = {(row["key"], row["plot"]) for row in rows}
    cannot = final_cannot(cannot, block, args.dry, args.overrides, args.keep, args.replace, args.keep_from, joined)
    # R330. The entries of the earlier list this run does not measure stay cell for cell, on plots the join gives their key
    listing, merged = final_list(listing, block, args.dry, args.overrides, args.keep, args.replace, args.keep_from, joined)
    override_source.check_entries(listing, block["groups"])
    check_measured_rounding(merged, block, profile)
    # R331. The coordinates of each key read against the reference key, gaps and names only
    site = key_rules.group_site_facts(reads, block["groups"], block["siteReference"]) if reads else None
    report = {"plots": len(items), "byGroup": dict(Counter(item["group"] for item in items)), "clusters": len(clusters), "clusterPlots": [cluster["plots"] for cluster in clusters],
              "frontRules": dict(Counter(front[number]["frontRule"] for number in numbers if number in front)), "keyDatumsM": datums, "siteFacts": site, **extras, "decisions": results,
              "overrideCount": dict(Counter(item["decision"] for item in listing)), "overrideMerge": merged, "overrideCounts": override_source.counts(listing),
              "cannotCount": dict(Counter(item["decision"] for item in cannot)), "cannotCounts": override_source.counts(cannot)}
    # R386. The skip list of the check table is kept whole, a run never measures it, and every check is made before anything is written
    finish(args, block, book, report, listing, cannot)
    for result in results:
        print("%s: coded %s, share %s, %s, misses %d of %d" % (result["decision"], result["coded"], result["share"], "strong" if result["strong"] else "WEAK", result["misses"], result["plots"]))
    for name, found in extras["coverage"].items():
        if not found["plots"]:
            print("%s: no plot, keys with none %s" % (name, found["keysWithNoPlot"]))
    print("overrides %d %s" % (len(listing), report["overrideCount"]))
    print("cannot, listed with no value %d %s" % (len(cannot), report["cannotCount"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
