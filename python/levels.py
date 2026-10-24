"""FFL, SSL and step count of the plots of a cluster, from the entry level of each plot (R67).

Every value comes from the project profile. Levels are in metres, the profile base heights are in mm and become
metres through profile.metres. Nothing here reads a drawing or a workbook.
"""
import re

import common
import project_profile

PAST_WORK_NOTE = "base height from past work"
# The note stays while the profile says its base heights are not confirmed. A source text without these words ends it
UNCONFIRMED = "not confirmed"
RULE_FORM = re.compile(r"^under ([0-9.]+) is (\d+), ([0-9.]+) to under ([0-9.]+) is (\d+), ([0-9.]+) and over is (\d+)$")
ENTRY_DECIMALS = 4
HEIGHT_DECIMALS = 4


def count_rule(profile):
    """The step count rule of steps.count_rule as ([(upper limit, steps), ...], steps at and over the last limit)."""
    text = profile.get("steps.count_rule")
    found = RULE_FORM.match(text.strip()) if isinstance(text, str) else None
    if not found:
        raise project_profile.ProfileError(profile.path.name + " steps.count_rule is not of the form: under A is N, A to under B is M, B and over is K")
    first, low, second_start, second, middle, top_start, high = found.groups()
    if float(first) != float(second_start) or float(second) != float(top_start) or float(first) >= float(second):
        raise project_profile.ProfileError(profile.path.name + " steps.count_rule limits do not follow each other")
    return [(float(first), int(low)), (float(second), int(middle))], int(high)


def steps_for(height, rule):
    """The step count for a height in metres. A height equal to a limit counts with the band that starts there."""
    limits, top = rule
    for limit, steps in limits:
        if height < limit:
            return steps
    return top


def base_height(profile, unit):
    """The base height of a unit typology in metres, the default when the profile lists none for it."""
    table = profile.get("steps.base_height")
    key = unit if isinstance(unit, str) and unit in table else "default"
    return profile.metres("steps.base_height." + key)


def group_plots(clusters, entries):
    """The groups to work out: every cluster as listed and then each plot with an entry level that is in none."""
    groups, seen = [], set()
    for cluster in clusters:
        plots = [str(plot) for plot in cluster["plots"]]
        for plot in plots:
            if plot in seen:
                raise ValueError("plot " + plot + " is in two clusters")
            seen.add(plot)
        groups.append((cluster.get("code"), plots))
    for plot in entries:
        if plot not in seen:
            seen.add(plot)
            groups.append((None, [plot]))
    return groups


def compute(profile, clusters, entries):
    """One row per plot with FFL, SSL, height and steps.

    clusters are dicts with plots in row order, as analysis.json writes them. entries maps a plot to its unit
    typology and its entry level in metres: {"1001": {"unit": "DT1", "entry": 171.512}}. A plot with an entry
    level that is in no cluster is a cluster of one. The base height of a cluster is the base height of its end
    units, the larger one when the two ends differ. The FFL of a cluster is its highest entry level plus the base
    height. The height of a plot is the FFL minus its entry level, the SSL is the FFL plus levels.ground_ssl.
    """
    rule = count_rule(profile)
    ground = profile.metres("levels.ground_ssl")
    least, most = profile.get("steps.front_min"), profile.get("steps.front_max")
    unconfirmed = UNCONFIRMED in str(profile.get("steps.base_height_source")).lower()
    entries = {str(plot): found for plot, found in entries.items()}
    rows = []
    for code, plots in group_plots(clusters, entries):
        known = [plot for plot in plots if plot in entries]
        ends = [entries[known[0]]["unit"], entries[known[-1]]["unit"]] if known else []
        bases = [base_height(profile, unit) for unit in ends]
        base = max(bases) if bases else None
        top = max(entries[plot]["entry"] for plot in known) if known else None
        ffl = None if top is None else top + base
        for plot in plots:
            row = {"plot": plot, "unit": entries[plot]["unit"] if plot in entries else None, "cluster": code, "size": len(plots),
                   "entry": None, "baseHeight": None, "ffl": None, "ssl": None, "height": None, "steps": None, "flags": [], "notes": []}
            if unconfirmed:
                row["notes"].append(PAST_WORK_NOTE)
            if plot not in entries:
                row["flags"].append("no entry level")
            else:
                height = ffl - entries[plot]["entry"]
                row.update({"entry": common.r3(entries[plot]["entry"], ENTRY_DECIMALS), "baseHeight": common.r3(base), "ffl": common.r3(ffl),
                            "ssl": common.r3(ffl + ground), "height": common.r3(height, HEIGHT_DECIMALS), "steps": steps_for(height, rule)})
                if row["steps"] < least:
                    row["flags"].append("steps under " + str(least))
                if row["steps"] > most:
                    row["flags"].append("steps over " + str(most))
                if len(set(bases)) > 1:
                    row["flags"].append("end units have different base heights")
            rows.append(row)
    return rows
