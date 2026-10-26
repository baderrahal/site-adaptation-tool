"""R278 to R280. The coded rules of the majority rule and the override list, as a plan from CAD applies them.

R277: a build that names no level profile takes levelProfiles.default of the rules profile, parsons2024Rev6 (Bader, Q050).
R278: El @ Entry is the road level of
its frontRoad surface at the two front corners plus offsetM, plus the ramp slope of the unit times the ramp depth. The height
rule oneFflPerClusterAtTheHighestEntry gives a cluster one FFL, the highest El @ Entry of its plots plus the base of that unit,
and a plot alone its El @ Entry plus its base, medianPerUnit gives the median height of the unit. Steps follow stepBands on
the height, SSL is the FFL plus levels.ground_ssl. The ramp type is the type rampTypes names for the slope class.
R279: the hand of a villa and of a duplex unit in no cluster is the hands table of the profile: the mirror flag of the plot's
Building Footprint block with its open end, else the block mirror flag alone. A unit of a cluster keeps R123.
R280: the override list of config/overrides is applied after the rules, only the overrides whose source is the job of the
level profile, parsons by default (R330), narrowed to the past models named. A level override moves the value by its gap to
the rule, a mirror or ramp class replaces it.
R337: a level profile with keyRules, the rules Round ADDIN 3a measured on its answer keys, builds the hands, the link origin, the
ramp class, the yard per zone, the yard outline and the link parameters by them, the tables of R279 only a profile with none.
"""
import json
import math
from collections import defaultdict
from pathlib import Path

import common
import project_profile

FIELDS = ("mirrored", "rampType", "entryM", "heightM", "rampFrontM")
# R337. The override fields a build by keyRules applies besides FIELDS, the link parameters by their field "parameter <name>"
KEY_FIELDS = ("insert", "rotationDeg", "steps")
PARAMETER = "parameter "
# R337. The candidates of majority.py a build can give: the hand factors, the factors of a parameter table, its formulas and levels
HAND_FACTORS = ("all", "blockMirror", "clusterSide", "openEnd", "roadSide", "blockMirrorOpenEnd", "blockMirrorRoadSide", "rowPairSide",
                "rowPairSideOrBlockMirrorOpenEnd")
TABLE_FACTORS = ("all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps")
FORMULAS = ("plot", "rev9Code", "plotAndCode", "plotCodeStepsAndFacade")
LEVEL_RULES = ("ffl", "fflDownToTheCm")
# R340. The level rules of keyRules a build carries out, optional, a profile with none builds the levels of R278
LEVEL_WORDS = {"entry": ("roadLevel",), "rampFront": ("roadLevel",), "levelSplit": ("none", "units"), "baseHeight": ("profile", "typology"),
               "ffl": ("oneFflPerPieceAtTheHighestEntry", "oneFflPerClusterAtTheHighestEntry", "ownEntryPlusBase"), "steps": ("stepsFromHeight", "stepBands")}
# R340. The level rules a build needs once keyRules holds the FFL, the base the keys were measured on among them, and one FFL per piece
# its split, so a build never gives whole clusters or another base than the measure of the gaps
LEVEL_NEEDED = ("entry", "steps", "baseHeight")
PIECE_NEEDED = {"oneFflPerPieceAtTheHighestEntry": ("levelSplit",)}
ROAD_LEVEL = "roadLevel"
# R337. The parameter rules that read the steps, which the stepBands of the level profile give until 3c codes the steps
STEP_RULES = ("steps", "typologySteps", "plotCodeStepsAndFacade")
NO_YARD = "noYard"
# Keys of levelProfiles that hold no profile
NOT_PROFILES = ("note", "default")


def profile_name(profile, name):
    """R277. The level profile a build follows: the one named, else levelProfiles.default. A default that is missing or names
    no profile is refused, so no build runs on a profile nobody chose."""
    profiles = profile.get("levelProfiles") or {}
    names = sorted(key for key in profiles if key not in NOT_PROFILES)
    if name:
        return name
    chosen = profiles.get("default")
    if not chosen or chosen in NOT_PROFILES or not isinstance(profiles.get(chosen), dict):
        raise project_profile.ProfileError("levelProfiles.default of the rules profile names no level profile, name one of " + ", ".join(names))
    return chosen


def level_profile(profile, name=None):
    """R277, R278. The named level profile, or the default of R277 when none is named. An unknown name is refused."""
    profiles = profile.get("levelProfiles") or {}
    name = profile_name(profile, name)
    if name not in profiles or name in NOT_PROFILES:
        raise project_profile.ProfileError("the rules profile has no level profile " + name)
    found = profiles[name]
    for key in ("frontRoad", "rampSlopePercent", "rampTypes", "height"):
        if key not in found:
            raise project_profile.ProfileError("level profile " + name + " has no " + key)
    return found


def slope_of(code, lp):
    table = lp["rampSlopePercent"]
    return table.get(code, table["default"])


def slope_class(slope):
    """The class a slope in percent names, flat, 0.5 or 2, as rampTypes keys it."""
    return "flat" if slope == 0 else "%g" % slope


def entry_of(front, code, lp, depth):
    """R278. El @ Entry from the road levels of the two front corners, the offset of the profile and the slope of the unit."""
    slope = slope_of(code, lp)
    return (front[0] + front[1]) / 2.0 + lp["frontRoad"]["offsetM"] + slope / 100.0 * depth, slope


def base_of(profile, code):
    table = profile.get("steps.base_height")
    return table.get(code, table["default"]) / 1000.0


def steps_of(height, lp):
    for limit, steps in lp.get("stepBands") or []:
        if height < limit:
            return steps
    return None


def heights(entries, clusters, lp, profile):
    """R278. FFL, SSL, height and steps per plot from the El @ Entry of each plot and the clusters, by the height rule of the profile."""
    ground = profile.metres("levels.ground_ssl")
    ffl = {}
    if lp["height"] == "oneFflPerClusterAtTheHighestEntry":
        taken = set()
        for cluster in clusters:
            members = [plot for plot in cluster["plots"] if plot in entries]
            if not members:
                continue
            top = max(entries[plot]["entry"] + base_of(profile, entries[plot]["unit"]) for plot in members)
            for plot in members:
                ffl[plot] = top
                taken.add(plot)
        for plot, item in entries.items():
            if plot not in taken:
                ffl[plot] = item["entry"] + base_of(profile, item["unit"])
    elif lp["height"] == "medianPerUnit":
        for plot, item in entries.items():
            median = (lp.get("medianHeightM") or {}).get(item["unit"])
            if median is not None:
                ffl[plot] = item["entry"] + median
    else:
        raise project_profile.ProfileError("no height rule " + str(lp["height"]))
    rows = {}
    for plot, level in ffl.items():
        entry = entries[plot]["entry"]
        height = common.fixed(level - entry, ENTRY_DECIMALS)
        rows[plot] = {"plot": plot, "ffl": common.fixed(level, FFL_DECIMALS), "ssl": common.fixed(level + ground, FFL_DECIMALS), "entry": common.fixed(entry, ENTRY_DECIMALS),
                      "height": height, "steps": steps_of(height, lp), "flags": []}
    return rows


def hand_of(code, kind, role, block_mirror, open_end, hands, drawn, file_code=None):
    """R279. The mirror flag of a plot and the rule that gave it, or None. drawn is the R123 mirror of a unit of a cluster. The
    tables are learned per prototype file, so a code that links the file of another code, VD1 the DP1 file, reads that code."""
    if kind != "villa" and role is not None:
        return drawn, "clusterSide"
    tables = (hands.get("villa" if kind == "villa" else "alone") or {}).get("byCode") or {}
    table = tables.get(code) or tables.get(file_code) or {}
    if block_mirror is not None and open_end is not None:
        value = (table.get("blockMirrorOpenEnd") or {}).get(block_mirror + " " + open_end)
        if value is not None:
            return value, "blockMirrorOpenEnd"
    value = (table.get("blockMirror") or {}).get(block_mirror) if block_mirror is not None else None
    return (value, "blockMirror") if value is not None else (None, None)


def key_rules_of(lp):
    """R337. keyRules of a level profile, the rules measured on its answer keys, each held to what a build can give, or None for a
    profile with none, native2022, which builds by the Round 17 tables of R279."""
    found = lp.get("keyRules")
    if found is None:
        return None
    check_key_rules(found)
    check_ramp_slopes(found, lp)
    return found


def check_ramp_slopes(rules, lp):
    """R337. El @ Entry takes the slope of a code from rampSlopePercent of the level profile and the ramp from the class of
    keyRules.rampSlope, so for every code the class holds the two must name one slope class, else the build is refused with the codes
    named, never built on two slopes."""
    wrong = sorted("%s (rampSlopePercent %s, keyRules.rampSlope %s)" % (code, slope_of(code, lp), found) for code, found in rules["rampSlope"]["table"].items()
                   if slope_class(slope_of(code, lp)) != found)
    if wrong:
        raise project_profile.ProfileError("the slope of El @ Entry and the ramp class of keyRules differ for " + ", ".join(wrong)
                                           + ", give both one slope (R337)")


def check_key_rules(rules):
    """R337. Every rule of keyRules a build takes must be a candidate it can give, with the table it reads, else the build is refused
    with the rule named, never built by another rule in silence."""
    def need(word, allowed, *fields):
        entry = rules.get(word)
        if not isinstance(entry, dict) or entry.get("rule") not in allowed or any(not isinstance(entry.get(field), (dict, list)) for field in fields):
            raise project_profile.ProfileError("keyRules %s codes %s, a build gives only %s%s (R337)" % (
                word, (entry or {}).get("rule") if isinstance(entry, dict) else None, ", ".join(allowed), "" if not fields else " with " + " and ".join(fields)))
        return entry
    # A build takes the front of R226 with --sales and no other, so the edge nearest a curb is a candidate it does not carry out
    need("linkRotation", ("frontRule",))
    need("linkOrigin", ("plotFrameRefit",), "byCode")
    for unit_class in ("villa", "cluster", "alone"):
        need("hand " + unit_class, HAND_FACTORS, "byFile")
    need("rampSlope", ("typology",), "table")
    need("rampOutline", ("outlineR127",))
    if need("generic", ("all",), "table")["table"] != {"all": "wholeFace"}:
        raise project_profile.ProfileError("keyRules generic gives no whole face, a build gives only the whole face (R337)")
    need("yard", ("zone",), "table")
    if need("yardOutline", ("fittedOutlineRefit", "fittedOutline"))["rule"] == "fittedOutlineRefit":
        need("yardOutline", ("fittedOutlineRefit",), "groups")
    for word, entry in rules.items():
        if word.startswith(PARAMETER):
            need(word, FORMULAS + LEVEL_RULES + TABLE_FACTORS, *(("table",) if (entry or {}).get("rule") in TABLE_FACTORS else ()))
    # R340. The level rules are optional, a rule a build does not carry out refused, and the FFL needs El @ Entry and the steps
    for word, allowed in LEVEL_WORDS.items():
        if word in rules:
            need(word, allowed, *(("table",) if (rules[word] or {}).get("rule") in ("units", "typology") else ()))
    if "ffl" in rules:
        needed = LEVEL_NEEDED + PIECE_NEEDED.get(rules["ffl"]["rule"], ())
        for word in needed:
            if word not in rules:
                raise project_profile.ProfileError("keyRules codes ffl %s and no %s, a build of the levels by keyRules needs %s (R340)"
                                                   % (rules["ffl"]["rule"], word, ", ".join(needed)))


def road_names(site_build, lp):
    """R339, R340. The road surfaces a build asks for El @ Entry and the ramp front, the one setting plan_from_book.py and majority.py
    take them from: siteBuild.roadSurfaces in their order for frontRoad roadLevel, else the one surface frontRoad names."""
    surface = lp["frontRoad"]["surface"]
    return list(site_build["roadSurfaces"]) if surface == ROAD_LEVEL else [surface]


def key_levels_of(rules):
    """R340. True when keyRules holds the level rules a build carries out, the FFL among them, else the levels are those of R278."""
    return rules is not None and "ffl" in rules


def key_pieces(rules, plots, roles, partners, units):
    """R340. The plots that share one FFL by keyRules.ffl: the pieces of the coded split and the row pairs, the clusters whole, or
    each plot alone."""
    rule = rules["ffl"]["rule"]
    if rule == "ownEntryPlusBase":
        return {plot: [plot] for plot in plots}
    split = rules.get("levelSplit") or {}
    table = (split.get("table") or {}) if split.get("rule") == "units" else {}
    if rule == "oneFflPerClusterAtTheHighestEntry":
        return level_pieces(plots, roles, {}, units, {})
    return level_pieces(plots, roles, partners, units, table)


def unit_class(kind, role):
    """R274. villa for a detached unit, cluster for a duplex or townhouse unit in a cluster, alone for one in none."""
    return "villa" if kind == "villa" else "cluster" if role is not None else "alone"


def joined_factors(factors):
    """R271, R333. The factors made of two others, blockMirrorRoadSide, blockMirrorOpenEnd and rowPairSideOrBlockMirrorOpenEnd, the one
    place majority.py and a build make them."""
    block_mirror = factors.get("blockMirror")
    factors["blockMirrorRoadSide"] = None if block_mirror is None or factors.get("roadSide") is None else block_mirror + " " + factors["roadSide"]
    factors["blockMirrorOpenEnd"] = None if block_mirror is None or factors.get("openEnd") is None else block_mirror + " " + factors["openEnd"]
    side = factors.get("rowPairSide")
    factors["rowPairSideOrBlockMirrorOpenEnd"] = "pair " + side if side else None if factors["blockMirrorOpenEnd"] is None else "alone " + factors["blockMirrorOpenEnd"]
    return factors


def key_hand(rules, unit_class_name, link_file, factors):
    """R337. The mirror flag of keyRules for a plot: the factor its coded hand rule names, looked up in the table learned for the file
    the plot links (R333), and the rule, or None when the factor or the table gives none."""
    entry = rules["hand " + unit_class_name]
    value = factors.get(entry["rule"])
    if value is None:
        return None, entry["rule"]
    return (entry["byFile"].get(link_file) or {}).get(value), entry["rule"]


def key_frame_section(rules):
    """R337. The plotFrame fits per code of keyRules.linkOrigin, fitted again on the keys, in the form of the plotFrame section."""
    return {"units": {code: {"anchor": fit["anchor"], "offset": list(fit["offset"])} for code, fit in rules["linkOrigin"]["byCode"].items()}}


def key_ramp_class(rules, code):
    """R337. The ramp slope class keyRules.rampSlope gives a Rev 9 code, None for a code its table does not hold."""
    return rules["rampSlope"]["table"].get(code)


def key_yard(rules, zone):
    """R337. The depth of the yard under the FFL keyRules.yard gives a zone, None for no yard, and why the zone was not measured.
    A zone the table does not hold raises KeyError, the build refuses it and never gives it another value."""
    value = rules["yard"]["table"][zone]
    return (None if value == NO_YARD else float(value)), (rules["yard"].get("notMeasured") or {}).get(zone)


def key_outlines(rules, outlines):
    """R337. The yard outlines a build places by keyRules.yardOutline: the groups fitted again on the keys, or those of
    config/floor_outlines.json fitted on IMC for the rule fittedOutline, with the generic outlines of that file."""
    if rules["yardOutline"]["rule"] == "fittedOutline":
        return outlines
    return dict(outlines, outlines=dict(outlines.get("outlines") or {}, yard=rules["yardOutline"]["groups"]))


def key_parameters(rules, plot, code, mirrored, steps, ffl, facade, later):
    """R337. The link parameters of a plot by the parameter rules of keyRules, a parameter whose decision is in later, left to a
    later phase, not written. The tables first, so the link Name takes the facade parameters they give. A rule that gives no value
    leaves its parameter out and names it in the second list."""
    step = None if steps is None else str(steps)
    factor = {"all": "all", "typology": code, "mirroredFactor": str(mirrored), "typologyMirrored": code + " " + str(mirrored), "steps": step,
              "typologySteps": None if step is None else code + " " + step}
    words = [word for word in rules if word.startswith(PARAMETER) and word not in later]
    values = {}
    for word in words:
        entry = rules[word]
        if entry["rule"] in TABLE_FACTORS and factor[entry["rule"]] is not None:
            found = entry["table"].get(factor[entry["rule"]])
            if found is not None:
                values[word[len(PARAMETER):]] = str(found)
    formulas = {"plot": lambda: plot, "rev9Code": lambda: code, "plotAndCode": lambda: plot + " | " + code,
                "plotCodeStepsAndFacade": lambda: " | ".join([plot, code, str(step)] + [values.get(name) or "" for name in facade]),
                "ffl": lambda: None if ffl is None else "%.3f" % ffl, "fflDownToTheCm": lambda: prototype_ffl("fflDownToTheCm", ffl)}
    for word in words:
        entry = rules[word]
        if entry["rule"] in formulas:
            found = formulas[entry["rule"]]()
            if found is not None:
                values[word[len(PARAMETER):]] = found
    return values, sorted(word[len(PARAMETER):] for word in words if word[len(PARAMETER):] not in values)


def step_parameters(rules, later):
    """R337. The link parameters whose rule of keyRules reads the steps, those of later left out. The steps are a level, the
    stepBands of the level profile of Round 17 give them until 3c codes them, so a build labels these parameters by that source."""
    return {word[len(PARAMETER):] for word, entry in rules.items()
            if word.startswith(PARAMETER) and word not in later and isinstance(entry, dict) and entry.get("rule") in STEP_RULES}


def check_override_jobs(name, lp, sources):
    """R280, R330. The overrides of a source are gaps to the rule of that job, so they apply only to a build whose level profile
    names that job. A source of another job is refused with both named and the source the build may name."""
    job = lp.get("job")
    if not job:
        raise project_profile.ProfileError("the level profile %s names no job, its overrides cannot be matched" % name)
    wrong = sorted(source for source in sources if source != job)
    if wrong:
        raise project_profile.ProfileError("the overrides of source %s are gaps to the rule of that job, the level profile %s is of the %s job: name --override-source %s "
                                           "on a build of %s, native overrides are never applied by default (R280, R330)" % (", ".join(wrong), name, job, job, name))


# R386. Why a build leaves an entry the check table marked "use": "rule"
USE_RULE = "use rule, the override list marks the entry to follow the rule (R386)"


class Overrides(dict):
    """R280, R386. The overrides a build applies by plot and field, with skip, the plots of the skip list by plot with their reason, and
    ruled, the entries marked use rule by plot and field with why, which a build leaves."""

    def __init__(self, found, skip=None, ruled=None):
        super().__init__(found)
        self.skip, self.ruled = dict(skip or {}), {plot: dict(fields) for plot, fields in (ruled or {}).items()}


def load_overrides(path, sources, fields=FIELDS, groups=None, name=None, lp=None, models=None):
    """R280, R330. The overrides of the list whose source, the job of their key, is one of sources and whose field is one of
    fields, by plot and field, with models only those of the past model files named. Every entry must hold the job of its key as
    source and its model apart (override_source.check_entries), groups from majority.groups of config/project.json when not
    given. A source or a model named with no override in the list is refused, never applied as none. With lp the job of each
    source must be the job of the level profile first (check_override_jobs). A mirror of a past model that links other
    prototype files than the plan is no mirror of the plan, so a build may leave it out by fields. R386: an entry marked "use": "rule"
    is left with why in ruled and the plots of the skip list come back in skip, so the build leaves them out and lists them."""
    import override_source
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    entries = data.get("overrides") or []
    if groups is None:
        groups = project_profile.load_settings().get("majority.groups")
    try:
        override_source.check_entries(entries, groups)
    except project_profile.ProfileError as error:
        raise project_profile.ProfileError(str(path) + ": " + str(error)) from error
    skip = override_source.check_skip(data.get("skip"), path) or {}
    if lp is not None:
        check_override_jobs(name, lp, sources)
    chosen = [item for item in entries if item["source"] in sources and (models is None or item["model"] in models)]
    missing = sorted(set(sources) - {item["source"] for item in chosen}) + sorted(set(models or ()) - {item["model"] for item in chosen})
    if missing:
        raise project_profile.ProfileError("the override list holds no override of %s among the sources %s (R280, R330)" % (", ".join(missing), ", ".join(sorted(sources))))
    # R280. An override with no value is no value to apply, a build refuses it, never writes it as the text None or counts it applied
    loaded = [item for item in chosen if fields is None or item.get("field") in fields]
    empty = ["plot %s field %s of %s" % (item["plot"], item.get("field"), item.get("model")) for item in loaded if item.get("value") is None]
    if empty:
        raise project_profile.ProfileError("%d overrides of the build hold no value: %s. A miss with no value belongs in the cannot list of R272, take it out "
                                           "of overrides (R280)" % (len(empty), " | ".join(empty[:10])))
    # R280, R340. A step count is a whole number, 2.6 is never cut to 2 in silence
    broken = ["plot %s value %s of %s" % (item["plot"], json.dumps(item["value"]), item.get("model")) for item in loaded
              if item.get("field") == "steps" and whole_number(item["value"]) is None]
    if broken:
        raise project_profile.ProfileError("%d steps overrides hold no whole number: %s. Give the step count as a whole number (R280, R340)"
                                           % (len(broken), " | ".join(broken[:10])))
    found, origin, twice, ruled = defaultdict(dict), {}, [], defaultdict(dict)
    for item in chosen:
        if fields is None or item.get("field") in fields:
            pair = (str(item["plot"]), item["field"])
            # R280. Two entries of one plot and field, of two keys or one, are refused, the last one is never taken in silence
            if pair in origin:
                twice.append("plot %s field %s of %s and %s" % (pair[0], pair[1], origin[pair], item.get("key") + " " + item.get("model")))
                continue
            origin[pair] = item.get("key") + " " + item.get("model")
            if override_source.use_of(item) == "rule":
                ruled[pair[0]][pair[1]] = USE_RULE
            else:
                found[pair[0]][pair[1]] = item["value"]
    if twice:
        raise project_profile.ProfileError("%d plots hold two overrides of one field: %s. Narrow the list with --override-model or edit it by hand (R280)"
                                           % (len(twice), " | ".join(twice[:10])))
    return Overrides(found, skip, ruled)


def whole_number(value):
    """The whole number a step count holds, as a number or its text, None for any other value such as 2.6 or two."""
    try:
        found = float(value)
    except (TypeError, ValueError):
        return None
    return int(found) if found.is_integer() else None


def gap_of(value):
    """The gap of a level override in metres, None for an override the rule could not give a gap to."""
    return value.get("gapToRuleM") if isinstance(value, dict) else None


# R339, R340. The levels as keyRules gives them, the one place majority.py measures them and a build carries them out
ENTRY_DECIMALS = 4
FFL_DECIMALS = 3
# A height is rounded to a micrometre before its steps are taken, so float noise never moves it across a band (levels.BAND_DECIMALS)
BAND_DECIMALS = 6
NO_SPLIT = "none"
MIDDLE = "middle"
WALLS = "walls "


def key_entry(front, code, lp, depth):
    """R339. El @ Entry a build gives, the road of the two front corners plus the offset and the slope of the unit times the ramp
    depth (R278), at the 4 decimals of R2, None with no road level at a front corner."""
    if not front or depth is None or code is None or any(level is None for level in front):
        return None
    return common.fixed(entry_of(front, code, lp, depth)[0], ENTRY_DECIMALS)


def split_walls(pattern, count):
    """R339. The walls a split pattern of keyRules.levelSplit names in a cluster of count units, a wall n after unit n."""
    if pattern in (None, NO_SPLIT):
        return []
    if pattern == MIDDLE:
        return [count // 2] if count % 2 == 0 else []
    if not str(pattern).startswith(WALLS):
        raise project_profile.ProfileError("keyRules.levelSplit names the pattern %s, a build gives none, middle or walls n,m (R339)" % pattern)
    return sorted({int(part) for part in str(pattern)[len(WALLS):].split(",") if 0 < int(part) < count})


def split_pattern(levels):
    """R339. The pattern of the FFLs of a cluster in row order: none, middle or the walls where the FFL changes."""
    walls = [index + 1 for index in range(len(levels) - 1) if abs(levels[index] - levels[index + 1]) > 0.0005]
    if not walls:
        return NO_SPLIT
    return MIDDLE if len(levels) % 2 == 0 and walls == [len(levels) // 2] else WALLS + ",".join(str(wall) for wall in walls)


def row_partners(sides, neighbours):
    """R339, R340. The other unit of the row pair of each unit in no cluster (R333): a right unit pairs with the one unit beside it
    on the left that is a left unit, and the other way round, none when no such single neighbour stands there."""
    found = {}
    for plot, side in sides.items():
        if side not in ("left", "right"):
            continue
        other_side = "right" if side == "left" else "left"
        beside = [other for other in (neighbours.get(plot) or {}).get(other_side, []) if sides.get(other) == other_side]
        if len(beside) == 1:
            found[plot] = beside[0]
    return found


def level_pieces(plots, roles, partners, units, split):
    """R339, R340. The plots that share the FFL of each plot: its cluster of R236, its plots in row order as R123 puts them, cut at
    the walls split, the table of keyRules.levelSplit, names for the units of the cluster, a unit in no cluster with the unit of its
    row pair (R333, partners), else the plot alone. A cluster whose units the table does not name stays whole, as R67 keeps it."""
    found = {}
    for plot in plots:
        role = roles.get(plot)
        if role is not None:
            ordered = list(role["plots"])
            walls = split_walls((split or {}).get(" ".join(str(units.get(other)) for other in ordered)), len(ordered))
            start = 0
            for wall in walls + [len(ordered)]:
                if plot in ordered[start:wall]:
                    found[plot] = ordered[start:wall]
                    break
                start = wall
        elif partners.get(plot) and partners.get(partners[plot]) == plot:
            found[plot] = sorted([plot, partners[plot]], key=lambda number: (len(number), number))
        else:
            found[plot] = [plot]
    return found


def base_by_rule(profile, code, rule=None):
    """R339. The base height of a unit: the table keyRules.baseHeight learned per typology when it codes one, else steps.base_height."""
    table = (rule or {}).get("table") if (rule or {}).get("rule") == "typology" else None
    if table and code in table:
        return float(table[code])
    return base_of(profile, code)


def piece_ffl(members, profile, base_rule=None):
    """R339, R67. The FFL of a piece: its highest El @ Entry plus the base of that unit, rounded to the mm first. members is a list of
    (El @ Entry, unit), None when no member has one."""
    known = [entry + base_by_rule(profile, unit, base_rule) for entry, unit in members if entry is not None and unit is not None]
    return None if not known else common.fixed(max(known), FFL_DECIMALS)


def steps_from_height(height, base, riser):
    """R339. The steps of the base and the risers: one step for the base, then one per riser over it, a part of a riser over half
    counting as one and half a riser not, as the keys count it: the 9 key plots whose height lies half a riser over their base, such
    as 0.185 m over 0.10 m, have 1 step (Round ADDIN 3c)."""
    return 1 + max(0, int(math.ceil(common.fixed((common.fixed(height, BAND_DECIMALS) - base) / riser, BAND_DECIMALS) - 0.5)))


def steps_by_rule(rule, height, code, profile, lp, base_rule=None):
    """R339, R340. The steps keyRules.steps codes: stepsFromHeight on the base of the unit and the risers, stepBands of the level profile."""
    if height is None:
        return None
    if rule == "stepsFromHeight":
        return steps_from_height(height, base_by_rule(profile, code, base_rule), profile.get("steps.riser_added") / 1000.0)
    if rule == "stepBands":
        return steps_of(common.fixed(height, BAND_DECIMALS), lp)
    raise project_profile.ProfileError("keyRules.steps codes %s, a build gives stepsFromHeight or stepBands (R339)" % rule)


def prototype_ffl(rule, ffl):
    """R339. The text of the RRE_ZZ_SP_Prototype-FFL parameter: the FFL down to the cm, or the FFL, written with 3 decimals."""
    if ffl is None:
        return None
    millimetres = int(common.fixed(ffl * 1000, 0))
    if rule == "fflDownToTheCm":
        return "%.3f" % ((millimetres // 10) / 100.0)
    if rule == "ffl":
        return "%.3f" % (millimetres / 1000.0)
    raise project_profile.ProfileError("keyRules parameter RRE_ZZ_SP_Prototype-FFL codes %s, a build gives fflDownToTheCm or ffl (R339)" % rule)


def rule_entry(item):
    """R340. El @ Entry the rule gives a plot, before the entryM gap of its own override, the entry itself for a plot with none."""
    return None if item is None else item.get("rule", item.get("entry"))


def key_level_rows(entries, pieces, profile, lp, key):
    """R339, R340. FFL, SSL, height and steps per plot by keyRules: one FFL per piece at its highest El @ Entry plus the base of that unit
    to the mm, the height that FFL less El @ Entry, SSL the FFL plus levels.ground_ssl and the steps of keyRules.steps. The FFL is taken
    on the El @ Entry the rule gives each plot of the piece (rule_entry), never moved by an entryM override, so an override moves the
    levels of its own plot only and deleting one line of the list leaves every other plot as it was (Round ADDIN 3c review)."""
    ground = profile.metres("levels.ground_ssl")
    base_rule = key.get("baseHeight")
    rows = {}
    for plot, item in entries.items():
        piece = pieces.get(plot) or [plot]
        ffl = piece_ffl([(rule_entry(entries.get(other)), (entries.get(other) or {}).get("unit")) for other in piece], profile, base_rule)
        if ffl is None:
            continue
        height = common.fixed(ffl - item["entry"], ENTRY_DECIMALS)
        rows[plot] = {"plot": plot, "ffl": ffl, "ssl": common.fixed(ffl + ground, FFL_DECIMALS), "entry": item["entry"], "height": height,
                      "steps": steps_by_rule(key["steps"]["rule"], height, item["unit"], profile, lp, base_rule), "flags": [], "piece": piece}
    return rows
