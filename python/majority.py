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
one generation is not forced on the other.
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
import footprint_check
import link_summary
import past_all
import plan_from_book
import plan_from_cad
import plan_from_past
import plot_relations
import project_profile
import ramp_outline
import yard_rule

CANNOT = "cannot"
ROAD = "roadLevel"


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
        sides = {"left": [], "right": []}
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
        open_end = None if sides is None else "both" if not sides["left"] and not sides["right"] else "left" if not sides["left"] else "right" if not sides["right"] else "none"
        streets = (sales.get(plot) or {}).get("No. of streets")
        depth = float(data["depthM"]) if data.get("depthM") not in (None, "") else None
        steps = None if row["steps"] in (None, "") else str(row["steps"])
        found.append({
            "row": row, "plot": plot, "key": row["key"], "group": group, "code": code, "rev9": row["typologyRev9"], "kind": kind_of(code, profile, block),
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
            },
        })
    for item in found:
        factors = item["factors"]
        block_mirror = factors["blockMirror"]
        factors["blockMirrorRoadSide"] = None if block_mirror is None or factors["roadSide"] is None else block_mirror + " " + factors["roadSide"]
        factors["blockMirrorOpenEnd"] = None if block_mirror is None or factors["openEnd"] is None else block_mirror + " " + factors["openEnd"]
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


def decide(name, items, target, candidates, scope, strong, hold=None, checks=()):
    """R272. Every candidate on the items, the strongest by share, whether it is strong, and its misses. A check reads a value of the
    key itself, such as its step count, so it is reported and never coded, a new plan has no such value."""
    hold = hold or (lambda a, b: a == b)
    tests, best = {}, None
    for label, factor in candidates.items():
        rule, outcome = evaluate(items, target, factor, scope, hold)
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
    misses = [{"item": item, "state": outcome[id(item)][0], "rule": outcome[id(item)][1], "value": target(item)}
              for item in items if id(item) in outcome and outcome[id(item)][0] != "holds"]
    result = {"decision": name, "plots": len(outcome), "coded": label, "share": tests[label]["share"], "strong": (tests[label]["share"] or 0) >= strong,
              "misses": len(misses), "tests": tests}
    return result, misses


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
    value = json.loads(item["row"]["parameters"] or "{}").get(name)
    return None if value in (None, "") else str(value)


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
            found[id(item)] = round(top - clean_entry(item["row"], block["entryRampM"]), 4)
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
                found[id(mine[plot])] = round(row["ffl"] - entries[plot]["entry"], 4)
    return found


def run(items, book, profile, block, surfaces, clusters):
    """R273 to R276. Every decision on every group, with its misses."""
    strong, hold_m = block["strongShare"], block["levelHoldMm"] / 1000.0
    by_group = lambda item: item["group"]
    by_file = lambda item: (item["group"], item["file"])
    level_hold = lambda guess, aim: abs(guess - aim) <= hold_m + 1e-9
    riser = profile.get("steps.riser_added") / 1000.0
    results, misses, datums = [], [], {}

    def record(name, pool, target, candidates, scope, field, hold=None, checks=()):
        if not pool:
            return
        result, missed = decide(name, pool, target, candidates, scope, strong, hold, checks)
        results.append(result)
        for miss in missed:
            item = miss["item"]
            misses.append({"decision": name, "field": field, "plot": item["plot"], "key": item["key"], "code": item["code"], "value": miss["value"],
                           "rule": miss["rule"], "state": miss["state"], "reason": ("no value of " if miss["state"] == CANNOT else "breaks ") + result["coded"]})

    for group in sorted({item["group"] for item in items if item["group"]}):
        pool = [item for item in items if item["group"] == group]
        slopes = block["groupSlopes"][group]
        for item in pool:
            entry = clean_entry(item["row"], block["entryRampM"])
            road = road_mean(book.get(item["plot"], {}), ROAD)
            depth = ramp_outline.setback(profile, item["rev9"] or item["code"])
            item["factors"]["measuredBand"] = band(item["row"]["rampSlopePercent"], block["slopeBands"])
            item["factors"]["heightOverRoad"] = None if entry is None or road is None else band((entry - road) / depth * 100.0, block["slopeBands"])
            item["factors"]["heightBand"] = band(item["row"]["height"], block["heightBands"])
            item["depth"] = depth
        neighbour_votes(pool, lambda item: slope_class(item, block))
        # R273. Ramp type and slope, flat ramps included
        # A group whose ramp types name no slope is measured, so the measured band would only repeat its target
        factors = [name for name in ("all", "typology", "measuredBand", "steps", "heightOverRoad", "corner", "depthBand", "neighbour", "key")
                   if not (name == "measuredBand" and group in block["measuredSlopeGroups"])]
        record(group + " rampSlope", pool, lambda item: slope_class(item, block), {name: name for name in factors}, by_group, "rampType")
        # R274. Hand, per link file, apart for villas, units in a cluster and units alone
        for unit_class in ("villa", "cluster", "alone"):
            chosen = [item for item in pool if item["row"]["mirrored"] is not None and hand_class(item) == unit_class]
            record(group + " hand " + unit_class, chosen, lambda item: item["row"]["mirrored"],
                   {name: name for name in ("all", "blockMirror", "clusterSide", "clusterIndexParity", "openEnd", "rowParity", "orientation", "roadSide",
                                            "blockMirrorRoadSide", "blockMirrorOpenEnd")}, by_file, "mirrored")
        # R275. El @ Entry: the road level at the front corners of a surface plus the slope of the unit times the ramp depth, as is and less a datum
        entries = [item for item in pool if clean_entry(item["row"], block["entryRampM"]) is not None]
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
                datums.setdefault(group + " entry", {})[surface] = {key: round(value, 4) for key, value in sorted(key_datum.items())}
        record(group + " entry", entries, target, formulas, by_group, "entryM", level_hold)
        # R275. The ramp front, the mean of its two low corners, against every road surface as it is and less the datum of each key
        fronts = [item for item in pool if item["row"]["rampFront"] is not None]
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
                datums.setdefault(group + " rampFront", {})[surface] = {key: round(value, 4) for key, value in sorted(key_datum.items())}
            front_formulas[surface] = road
            front_formulas[surface + " less the datum of each key"] = lambda item, road=road, key_datum=key_datum: None if road(item) is None else road(item) + key_datum[item["key"]]
        record(group + " rampFront", fronts, front_target, front_formulas, by_group, "rampFrontM", level_hold)
        # R275. The fall along the front: both front corners level, or each on the road at its corner
        falls = [item for item in pool if item["row"]["rampFrontFall"] is not None]
        road_fall = lambda item: (None if road_corners(book.get(item["plot"], {})) is None else abs(road_corners(book.get(item["plot"], {}))[0] - road_corners(book.get(item["plot"], {}))[1]))
        record(group + " rampFrontFall", falls, lambda item: item["row"]["rampFrontFall"], {"levelFront": lambda item: 0.0, "roadAtEachCorner": road_fall},
               by_group, "rampFrontFallM", level_hold)
        # R275. FFL less El @ Entry and the steps
        heights = [item for item in entries if item["row"]["ffl"] is not None]
        height = lambda item: round(item["row"]["ffl"] - clean_entry(item["row"], block["entryRampM"]), 4)
        tops = cluster_top(heights, profile, block)
        from_levels = levels_rule(heights, clusters, profile, block)
        record(group + " height", heights, height, {
            "typology": "typology", "typologySteps": "typologySteps", "key": "key",
            "baseAndRisers": lambda item: None if item["factors"]["steps"] is None else round(base_height(profile, item["code"]) + riser * (int(item["factors"]["steps"]) - 1), 4),
            "baseOnly": lambda item: base_height(profile, item["code"]),
            "oneFflPerClusterAtTheHighestEntry": lambda item: tops.get(id(item), base_height(profile, item["code"]) if not item["role"] else None),
            "levelsPyFromEntry": lambda item: from_levels.get(id(item)),
        }, by_group, "heightM", lambda guess, aim: abs(guess - aim) <= hold_m + 1e-9, checks=("typologySteps", "baseAndRisers"))
        with_steps = [item for item in pool if item["factors"]["steps"] is not None]
        record(group + " steps", with_steps, lambda item: item["factors"]["steps"], {
            "typology": "typology", "heightBand": "heightBand",
            "stepsFromHeight": lambda item: None if item["row"]["height"] is None else str(1 + max(0, round((item["row"]["height"] - base_height(profile, item["code"])) / riser))),
        }, by_group, "steps")
        # R276. Generic floor on the whole face, yard, CASE and the other link parameters
        record(group + " generic", pool, lambda item: item["factors"].get("wholeFace"), {name: name for name in ("all", "typology", "key")}, by_group, "genericFloor")
        for item in pool:
            top, ffl = item["row"]["yardTop"], item["row"]["ffl"]
            item["factors"]["yardDepth"] = "noYard" if top is None else None if ffl is None else "%.3f" % (ffl - top)
        record(group + " yard", pool, lambda item: item["factors"]["yardDepth"], {name: name for name in ("all", "typology", "key")}, by_group, "yard")
        for name in block["parameterTests"]:
            chosen = [item for item in pool if parameter(item, name) is not None]
            record(group + " parameter " + name, chosen, lambda item, name=name: parameter(item, name),
                   {label: label for label in ("all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps", "clusterSide", "corner")}, by_group, "parameter " + name)
    return results, misses, datums


def overrides(misses, block, models):
    """R272. The override list: every miss of the decisions block.overrideDecisions names, with no real level or coordinate,
    the source model of the key, sorted by plot."""
    kept = []
    for miss in misses:
        decision = miss["decision"].split(" ", 1)[1]
        if not any(decision.startswith(name) for name in block["overrideDecisions"]):
            continue
        value, rule = miss["value"], miss["rule"]
        if miss["field"] in ("heightM", "entryM", "rampFrontM", "rampFrontFallM"):
            value, rule = (None if rule is None else {"gapToRuleM": round(value - rule, 4)}), None
        kept.append({"plot": miss["plot"], "field": miss["field"], "value": value, "rule": rule, "source": models.get(miss["key"], miss["key"]),
                     "key": miss["key"], "decision": miss["decision"], "reason": miss["reason"]})
    return sorted(kept, key=lambda item: (link_summary.sort_key(item["plot"]), item["key"], item["field"]))


def write_overrides(path, note, listing):
    """The override list with one override per line, so Bader can find and edit a plot by hand."""
    lines = ",\n".join("    " + json.dumps(item, ensure_ascii=False) for item in listing)
    text = "{\n  \"note\": " + json.dumps(note, ensure_ascii=False) + ",\n  \"overrides\": [\n" + lines + ("\n" if listing else "") + "  ]\n}\n"
    json.loads(text)
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(text.encode("utf-8"))


def load_block(settings):
    block = settings.get("majority")
    needed = ("strongShare", "levelHoldMm", "entryRampM", "groups", "codeFrom", "kindByPrefix", "slopeBands", "rampTypeClass", "groupSlopes", "depthBands",
              "heightBands", "sideShare", "parameterTests", "overrideDecisions", "wholeFaceShare", "overrideNote", "measuredSlopeGroups", "datumSurfaces")
    if not isinstance(block, dict) or any(key not in block for key in needed):
        raise project_profile.ProfileError(settings.path.name + " majority must name " + ", ".join(needed))
    return block


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
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = load_block(settings)
    plot_relations.refuse_inside_repo(Path(args.out).resolve().parent)
    rows = past_all.read_table(args.table)
    book, number_of = plan_from_book.book_rows(args.plots_csv)
    by_label, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    faces = {number_of[label]: shape for label, shape in by_label.items() if label in number_of}
    sheet = json.loads((project_profile.ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["locationPlan"]["sales"]
    sales = corner_front.sales_rows(args.sales, sheet["sheet"], sheet["columns"]["plot"])
    roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
    numbers = sorted({row["plot"] for row in rows}, key=link_summary.sort_key)
    clusters = plan_from_book.footprint_clusters(numbers, book, faces, footprint_check.block_shapes(link_summary.load(args.footprint_blocks)["blocks"]))
    clusters = plan_from_book.joined_clusters(clusters, plan_from_book.numbered_clusters(json.loads(Path(args.analysis).read_text(encoding="utf-8"))["clusters"], number_of))
    # A face with two plan numbers, such as 476|477, is no plot of its own and leaves its cluster
    clusters = [cluster for cluster in ({**cluster, "plots": [plot for plot in cluster["plots"] if plot.isdigit()]} for cluster in clusters) if len(cluster["plots"]) > 1]
    corner = settings.get("siteBuild")["corner"]
    front = plan_from_book.fronts(numbers, book, faces, roads, cad_links.front_limits(profile, settings.get("analysis")), [], 0.0, profile, corner, sales)
    frames = {number: item["frame"] for number, item in front.items()}
    roles = cad_links.cluster_roles(clusters, 0, {number: faces[number] for number in numbers if number in faces}, frames)
    units = [number for number, data in book.items() if data.get("typologyRev9") and number in faces]
    neighbours = side_neighbours(units, faces, frames, block["sideShare"], profile.get("plot.long_side") / 1000.0)
    tree = STRtree(roads)
    side_roads = {number: road_side(faces.get(number), frames.get(number), tree, roads, corner["roadEdgeM"]) for number in numbers}
    items = contexts(rows, book, sales, faces, roles, neighbours, side_roads, profile, block)
    for item in items:
        outline, face = item["row"]["genericOutline"], faces.get(item["plot"])
        if outline and face is not None:
            shape = wkt.loads(outline)
            union = shape.union(face).area
            item["factors"]["wholeFace"] = "wholeFace" if union and shape.intersection(face).area / union >= block["wholeFaceShare"] else "partOfFace"
        else:
            item["factors"]["wholeFace"] = None
    surfaces = [ROAD] + sorted({column[len("level_"):-len("_left")] for column in next(iter(book.values())) if column.startswith("level_") and column.endswith("_left")})
    results, misses, datums = run(items, book, profile, block, surfaces, clusters)
    listing = overrides(misses, block, dict(args.model))
    report = {"plots": len(items), "byGroup": dict(Counter(item["group"] for item in items)), "clusters": len(clusters),
              "frontRules": dict(Counter(item["frontRule"] for item in front.values())), "keyDatumsM": datums, "decisions": results,
              "overrideCount": dict(Counter(item["decision"] for item in listing))}
    common.write_json(args.out, report)
    write_overrides(args.overrides, block["overrideNote"], listing)
    for result in results:
        print("%s: coded %s, share %s, %s, misses %d of %d" % (result["decision"], result["coded"], result["share"], "strong" if result["strong"] else "WEAK", result["misses"], result["plots"]))
    print("overrides %d %s" % (len(listing), report["overrideCount"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
