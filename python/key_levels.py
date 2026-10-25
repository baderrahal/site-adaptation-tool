"""R339. The levels of the answer keys of a group a build by keyRules builds, measured as that build gives them, to the mm.

El @ Entry is held against the road a build gives at the front corners of R226, the road surfaces of siteBuild.roadSurfaces asked
as plan_from_book.fronts asks them, plus the slope of the unit times the ramp depth (R278), at the 4 decimals of R2, within
majority.levelPassMm, with its share at each band of majority.levelBandsMm beside. A build takes the road of frontRoad of its level
profile and no other, so the surfaces of plots.csv and the datums are reported as checks, never coded. The ramp front the same way.

What follows is chained on the levels a build holds: a plot whose El @ Entry holds keeps the value of the rule, a miss takes the
value of the key its gap gives, a plot with no road level has none. The split of a cluster of three or more units is learned per
cluster by its units in row order (Q012), the base of the unit that sets the FFL of its piece per typology or by steps.base_height,
and the FFL of each piece, its highest El @ Entry plus the base of that unit to the mm (R67), is chosen on the El @ Entry of the key
and judged on the El @ Entry a build holds (byBuild), so its misses, the overrides, are the plots a build does not reproduce. The
steps and the Prototype-FFL parameter are chosen on the levels of the key and judged on the FFL and the height a build holds. SSL is
in no key, majority.levelNotMeasured says so.
"""
import statistics
from collections import defaultdict

import plan_rules

EPSILON = 1e-9
ROAD = "roadLevel"


def holds_within(mm):
    return lambda guess, aim: abs(guess - aim) <= mm / 1000.0 + EPSILON


def bands_of(block):
    """R339. The hold of each band of majority.levelBandsMm, by its name in mm."""
    return {"%gmm" % mm: holds_within(mm) for mm in block["levelBandsMm"]}


def road_mean(data, surface):
    """The mean road level of the two front corners on a surface of plots.csv, None when a corner has none."""
    left, right = (data.get("roadLevelLeft"), data.get("roadLevelRight")) if surface == ROAD else (data.get("level_" + surface + "_left"), data.get("level_" + surface + "_right"))
    if left in (None, "") or right in (None, ""):
        return None
    return (float(left) + float(right)) / 2.0


def front_mean(found):
    """The mean of the road levels a build gives at the two front corners of a plot, None when a corner has none."""
    front = (found or {}).get("front")
    return None if not front or any(level is None for level in front) else (front[0] + front[1]) / 2.0


def number(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def table_of(result, label):
    """The table a factor learned, by its value, the scope of the group left out."""
    rule = ((result or {}).get("tests") or {}).get(label, {}).get("rule") or {}
    return {key.split(" | ", 1)[1]: value for key, value in rule.items()}


def missed(misses, name):
    """The key and plot of every miss of a decision, for a decision that writes no override (levelSplit, baseHeight)."""
    return sorted(miss["key"] + " " + miss["plot"] for miss in misses if miss["decision"] == name)


def measure(record, group, pool, kept, lv, profile, block, book, surfaces, extras, clean, parameter, misses, slopes):
    """R339. Every level decision of a group a build by keyRules builds, recorded with record of majority.run."""
    hold = holds_within(block["levelPassMm"])
    bands = bands_of(block)
    lp, site = lv["lp"], lv["site"]
    by_group = lambda item: item["group"]
    units = {plot: found.get("unit") for plot, found in site.items()}
    rule_entry = {plot: plan_rules.key_entry(found.get("front"), found.get("unit"), lp, found.get("depth")) for plot, found in site.items()}
    by_pair = {(item["key"], item["plot"]): item for item in pool}

    # El @ Entry against the road a build gives, every other surface and datum a check
    entries = [item for item in kept("entry") if clean(item["row"]) is not None]
    target = lambda item: clean(item["row"])
    formulas, checks = {ROAD: lambda item: rule_entry.get(item["plot"])}, []
    for surface in surfaces:
        if surface == ROAD:
            guess = formulas[ROAD]
        else:
            def guess(item, surface=surface):
                road = road_mean(book.get(item["plot"], {}), surface)
                return None if road is None else road + slopes.get(item["code"], slopes["default"]) / 100.0 * item["depth"]
            formulas[surface] = guess
            checks.append(surface)
        by_key = defaultdict(list)
        for item in entries:
            if guess(item) is not None:
                by_key[item["key"]].append(target(item) - guess(item))
        key_datum = {key: statistics.median(values) for key, values in by_key.items()}
        label = surface + " less the datum of each key"
        formulas[label] = lambda item, guess=guess, key_datum=key_datum: None if guess(item) is None else guess(item) + key_datum[item["key"]]
        checks.append(label)
    record(group + " entry", entries, target, formulas, by_group, "entryM", hold, checks=tuple(checks), bands=bands)
    key_entry = {(item["key"], item["plot"]): clean(item["row"]) for item in entries}

    def built_entry(key, plot):
        """El @ Entry a build holds: the rule where it holds, the key where its gap moves it, none with no road level."""
        rule = rule_entry.get(plot)
        aim = key_entry.get((key, plot))
        if rule is None:
            return None
        return rule if aim is None or hold(rule, aim) else aim

    def key_view(key, plot):
        aim = key_entry.get((key, plot))
        return aim if aim is not None else rule_entry.get(plot)

    # The ramp front against the road a build gives, every other surface a check
    fronts = [item for item in kept("rampFront") if item["row"]["rampFront"] is not None]
    front_formulas, front_checks = {ROAD: lambda item: front_mean(site.get(item["plot"]))}, []
    for surface in surfaces:
        if surface != ROAD:
            front_formulas[surface] = lambda item, surface=surface: road_mean(book.get(item["plot"], {}), surface)
            front_checks.append(surface)
    record(group + " rampFront", fronts, lambda item: item["row"]["rampFront"], front_formulas, by_group, "rampFrontM", hold, checks=tuple(front_checks), bands=bands)

    # The split of a cluster of three or more units, learned per cluster by its units in row order (Q012)
    key_ffl = {(item["key"], item["plot"]): item["row"]["ffl"] for item in kept("levelSplit") if item["row"]["ffl"] is not None}
    clusters = {}
    for item in kept("levelSplit"):
        role = lv["roles"].get(item["plot"])
        if role is not None and len(role["plots"]) >= 3:
            clusters.setdefault((item["key"], tuple(role["plots"])), item)
    split_items = []
    for (key, plots), first in sorted(clusters.items()):
        levels = [key_ffl.get((key, plot)) for plot in plots]
        if any(level is None for level in levels):
            continue
        name = " ".join(str(units.get(plot)) for plot in plots)
        split_items.append({"plot": plots[0], "key": key, "group": group, "code": name, "modelKind": first.get("modelKind"), "file": first.get("file"),
                            "factors": {"all": "all", "units": name}, "pattern": plan_rules.split_pattern(levels), "plots": list(plots)})
    split = record(group + " levelSplit", split_items, lambda item: item["pattern"], {"none": lambda item: plan_rules.NO_SPLIT, "units": "units"}, by_group, "levelSplit")
    table = table_of(split, "units") if split is not None and split["coded"] == "units" else {}
    extras["levelMisses"][group + " levelSplit"] = missed(misses, group + " levelSplit")
    plots = sorted(set(site) | set(lv["roles"]) | set(lv["partners"]))
    pieces = plan_rules.level_pieces(plots, lv["roles"], lv["partners"], units, table)
    whole = plan_rules.level_pieces(plots, lv["roles"], {}, units, {})

    # The base of the unit that sets the FFL of its piece, on the pieces whose plots the key holds at one FFL
    tops, seen = [], set()
    for item in kept("baseHeight"):
        piece = pieces.get(item["plot"]) or [item["plot"]]
        if (item["key"], tuple(piece)) in seen:
            continue
        seen.add((item["key"], tuple(piece)))
        held = [by_pair.get((item["key"], plot)) for plot in piece]
        if any(other is None or key_entry.get((item["key"], other["plot"])) is None or other["row"]["ffl"] is None for other in held):
            continue
        if len({round(other["row"]["ffl"], 3) for other in held}) != 1:
            continue
        top = max(held, key=lambda other: key_entry[(item["key"], other["plot"])] + plan_rules.base_of(profile, other["code"]))
        tops.append({"plot": top["plot"], "key": top["key"], "group": group, "code": top["code"], "modelKind": top.get("modelKind"), "file": top.get("file"),
                     "factors": {"all": "all", "typology": top["code"]}, "base": round(top["row"]["ffl"] - key_entry[(top["key"], top["plot"])], 4)})
    base = record(group + " baseHeight", tops, lambda item: item["base"], {"profile": lambda item: plan_rules.base_of(profile, item["code"]), "typology": "typology"},
                  by_group, "baseHeight", hold, bands=bands)
    base_rule = {"rule": "typology", "table": table_of(base, "typology")} if base is not None and base["coded"] == "typology" else None
    extras["levelMisses"][group + " baseHeight"] = missed(misses, group + " baseHeight")

    # The FFL of each piece, chosen on the El @ Entry of the key and judged on the El @ Entry a build holds
    def ffl_by(piece_map, entry_of):
        return lambda item: plan_rules.piece_ffl([(entry_of(item["key"], other), units.get(other)) for other in piece_map.get(item["plot"], [item["plot"]])], profile, base_rule)
    shapes = {"oneFflPerClusterAtTheHighestEntry": whole, "oneFflPerPieceAtTheHighestEntry": pieces, "ownEntryPlusBase": {}}
    ffls = [item for item in kept("ffl") if item["row"]["ffl"] is not None]
    built_view = {name: ffl_by(shape, built_entry) for name, shape in shapes.items()}
    ffl = record(group + " ffl", ffls, lambda item: item["row"]["ffl"], {name: ffl_by(shape, key_view) for name, shape in shapes.items()}, by_group, "heightM", hold,
                 bands=bands, build_view=built_view)
    coded = built_view[ffl["coded"]] if ffl is not None else (lambda item: None)

    def built_ffl(item):
        """The FFL a build holds: the rule where it holds, the key where its gap moves it, none where the rule gives none."""
        guess = coded(item)
        aim = item["row"]["ffl"]
        return None if guess is None else guess if aim is None or hold(guess, aim) else aim

    def key_height(item):
        entry = clean(item["row"])
        return None if entry is None or item["row"]["ffl"] is None else round(item["row"]["ffl"] - entry, plan_rules.ENTRY_DECIMALS)

    def built_height(item):
        level, entry = built_ffl(item), built_entry(item["key"], item["plot"])
        return None if level is None or entry is None else round(level - entry, plan_rules.ENTRY_DECIMALS)

    def steps_by(rule, height_of):
        def steps(item):
            height = height_of(item)
            return None if height is None else str(plan_rules.steps_by_rule(rule, height, item["code"], profile, lp, base_rule))
        return steps
    with_steps = [item for item in kept("steps") if item["factors"]["steps"] is not None]
    record(group + " steps", with_steps, lambda item: str(int(float(item["factors"]["steps"]))),
           {"stepsFromHeight": steps_by("stepsFromHeight", key_height), "stepBands": steps_by("stepBands", key_height), "typology": "typology"}, by_group, "steps",
           build_view={"stepsFromHeight": steps_by("stepsFromHeight", built_height), "stepBands": steps_by("stepBands", built_height)})

    # The level parameters of majority.parameterLevels against the FFL of the key, judged on the FFL a build holds
    down = lambda level: None if level is None else float(plan_rules.prototype_ffl("fflDownToTheCm", level))
    for name in block["parameterLevels"]:
        chosen = [item for item in kept("parameter") if number(parameter(item, name)) is not None]
        record(group + " parameter " + name, chosen, lambda item, name=name: float(parameter(item, name)),
               {"ffl": lambda item: item["row"].get("ffl"), "genericTop": lambda item: item["row"].get("genericTop"), "fflDownToTheCm": lambda item: down(item["row"].get("ffl"))},
               by_group, "parameter " + name, hold, gap=lambda item, value, rule: None if rule is None or value is None else {"gapToRuleM": round(value - rule, 4)},
               bands=bands, build_view={"ffl": built_ffl, "fflDownToTheCm": lambda item: down(built_ffl(item))})
    extras["levelNotMeasured"].update(block.get("levelNotMeasured") or {})
