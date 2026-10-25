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
from collections import defaultdict
from pathlib import Path

import project_profile

FIELDS = ("mirrored", "rampType", "entryM", "heightM", "rampFrontM")
# R337. The override fields a build by keyRules applies besides FIELDS, the link parameters by their field "parameter <name>"
KEY_FIELDS = ("insert", "rotationDeg")
PARAMETER = "parameter "
# R337. The candidates of majority.py a build can give: the hand factors, the factors of a parameter table, its formulas and levels
HAND_FACTORS = ("all", "blockMirror", "clusterSide", "openEnd", "roadSide", "blockMirrorOpenEnd", "blockMirrorRoadSide", "rowPairSide",
                "rowPairSideOrBlockMirrorOpenEnd")
TABLE_FACTORS = ("all", "typology", "mirroredFactor", "typologyMirrored", "steps", "typologySteps")
FORMULAS = ("plot", "rev9Code", "plotAndCode", "plotCodeStepsAndFacade")
LEVEL_RULES = ("ffl",)
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
        height = round(level - entry, 4)
        rows[plot] = {"plot": plot, "ffl": round(level, 3), "ssl": round(level + ground, 3), "entry": round(entry, 4), "height": height,
                      "steps": steps_of(height, lp), "flags": []}
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
                "ffl": lambda: None if ffl is None else "%.3f" % ffl}
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


def load_overrides(path, sources, fields=FIELDS, groups=None, name=None, lp=None, models=None):
    """R280, R330. The overrides of the list whose source, the job of their key, is one of sources and whose field is one of
    fields, by plot and field, with models only those of the past model files named. Every entry must hold the job of its key as
    source and its model apart (override_source.check_entries), groups from majority.groups of config/project.json when not
    given. A source or a model named with no override in the list is refused, never applied as none. With lp the job of each
    source must be the job of the level profile first (check_override_jobs). A mirror of a past model that links other
    prototype files than the plan is no mirror of the plan, so a build may leave it out by fields."""
    import override_source
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    entries = data.get("overrides") or []
    if groups is None:
        groups = project_profile.load_settings().get("majority.groups")
    try:
        override_source.check_entries(entries, groups)
    except project_profile.ProfileError as error:
        raise project_profile.ProfileError(str(path) + ": " + str(error)) from error
    if lp is not None:
        check_override_jobs(name, lp, sources)
    chosen = [item for item in entries if item["source"] in sources and (models is None or item["model"] in models)]
    missing = sorted(set(sources) - {item["source"] for item in chosen}) + sorted(set(models or ()) - {item["model"] for item in chosen})
    if missing:
        raise project_profile.ProfileError("the override list holds no override of %s among the sources %s (R280, R330)" % (", ".join(missing), ", ".join(sorted(sources))))
    found, origin, twice = defaultdict(dict), {}, []
    for item in chosen:
        if fields is None or item.get("field") in fields:
            pair = (str(item["plot"]), item["field"])
            # R280. Two entries of one plot and field, of two keys or one, are refused, the last one is never taken in silence
            if pair in origin:
                twice.append("plot %s field %s of %s and %s" % (pair[0], pair[1], origin[pair], item.get("key") + " " + item.get("model")))
                continue
            origin[pair] = item.get("key") + " " + item.get("model")
            found[pair[0]][pair[1]] = item["value"]
    if twice:
        raise project_profile.ProfileError("%d plots hold two overrides of one field: %s. Narrow the list with --override-model or edit it by hand (R280)"
                                           % (len(twice), " | ".join(twice[:10])))
    return dict(found)


def gap_of(value):
    """The gap of a level override in metres, None for an override the rule could not give a gap to."""
    return value.get("gapToRuleM") if isinstance(value, dict) else None
