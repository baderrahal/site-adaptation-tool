"""R278 to R280. The coded rules of the majority rule and the override list, as a plan from CAD applies them.

R278: a level profile of levelProfiles in the rules profile is named, never taken by default. El @ Entry is the road level of
its frontRoad surface at the two front corners plus offsetM, plus the ramp slope of the unit times the ramp depth. The height
rule oneFflPerClusterAtTheHighestEntry gives a cluster one FFL, the highest El @ Entry of its plots plus the base of that unit,
and a plot alone its El @ Entry plus its base, medianPerUnit gives the median height of the unit. Steps follow stepBands on
the height, SSL is the FFL plus levels.ground_ssl. The ramp type is the type rampTypes names for the slope class.
R279: the hand of a villa and of a duplex unit in no cluster is the hands table of the profile: the mirror flag of the plot's
Building Footprint block with its open end, else the block mirror flag alone. A unit of a cluster keeps R123.
R280: the override list of config/overrides is applied after the rules, only the overrides whose source is
the past model the plan follows. A level override moves the value by its gap to the rule, a mirror or ramp class replaces it.
"""
import json
from collections import defaultdict
from pathlib import Path

import project_profile

FIELDS = ("mirrored", "rampType", "entryM", "heightM", "rampFrontM")


def level_profile(profile, name):
    """R278. The named level profile. There is no default, a missing or unknown name is refused."""
    profiles = profile.get("levelProfiles") or {}
    if not name:
        raise project_profile.ProfileError("name a level profile, one of " + ", ".join(sorted(key for key in profiles if key != "note")))
    if name not in profiles or name == "note":
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


def load_overrides(path, sources, fields=FIELDS):
    """R280. The overrides of the list whose source is one of sources and whose field is one of fields, by plot and field. A
    mirror of a past model that links other prototype files than the plan is no mirror of the plan, so a build may leave it out."""
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    found = defaultdict(dict)
    for item in data.get("overrides") or []:
        if item.get("source") in sources and item.get("field") in fields:
            found[str(item["plot"])][item["field"]] = item["value"]
    return dict(found)


def gap_of(value):
    """The gap of a level override in metres, None for an override the rule could not give a gap to."""
    return value.get("gapToRuleM") if isinstance(value, dict) else None
