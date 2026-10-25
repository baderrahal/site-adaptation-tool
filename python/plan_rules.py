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
"""
import json
from collections import defaultdict
from pathlib import Path

import project_profile

FIELDS = ("mirrored", "rampType", "entryM", "heightM", "rampFrontM")
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
    found = defaultdict(dict)
    for item in chosen:
        if item.get("field") in fields:
            found[str(item["plot"])][item["field"]] = item["value"]
    return dict(found)


def gap_of(value):
    """The gap of a level override in metres, None for an override the rule could not give a gap to."""
    return value.get("gapToRuleM") if isinstance(value, dict) else None
