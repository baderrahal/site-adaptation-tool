"""R401, the plan part. The status of each sourced value of a plot of the plan, one of the six of rule_status.py, taken once per build from
the block status of the rules profile through valueStatus of config/project.json. The map names per field and source one block entry or a
list of them, by the code that computes the value, or the status itself for an override or a client input. A value of several entries takes
the worst status among them in the order of valueStatus.worstFirst, else the status of the first, and lists each (Q107). An override of the
list marks every field it moves, overrideSets, with the status of the source override, named by the override applied last whose set holds
the field, the one that moved the value (Q107). A pair the map does not hold, or an entry the block lacks, is UNKNOWN with why, never
a guess. A status changes no value: a value holds its status and the key of its detail, and the detail is written once in the table
statusFrom at the top of the plan. ValueStatus.cs of the C# gives the same."""
import json
import re
from collections import Counter, defaultdict

import common
import link_summary
import project_profile

# rule_status.SETTINGS_FILE, the key of the block entries of the settings
SETTINGS_FILE = "config/project.json"
MEASURED = "MEASURED"
OVERRIDE = "override"
PARAMETERS = "parameters."
ANY_PARAMETER = "parameters.*"
PLACEHOLDER = re.compile(r"\{(\w+)\}")
ASSUMED = "assumed"
SETS_LINE = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold %s (R401)"
# The labels a status of a value takes, the six of rule_status.py with MEASURED by its strength, which worstFirst names
LABELS = ("UNKNOWN", "CHOSEN by Code", "CHOSEN by Bader", "RECOMMENDED", "MEASURED weak", "MEASURED strong", "MEASURED", "DOCUMENT")
# R3 of the review of PR 75. The key of the step overrides (R360) a field of the plan is written as, whose decimals of addin.parity the
# value is held at when it is held against the value with no override, ValueStatus.OverrideKeys of the C#. A parameter and linkName are texts
STEP_KEYS = {"mirrored": "mirrored", "insert": "insert", "rotationDeg": "rotationDeg", "levels.entry": "entry", "levels.ffl": "ffl", "levels.ssl": "ssl",
             "levels.height": "height", "levels.steps": "steps", "floors.ramp.corners": "rampZ", "floors.ramp.type": "rampType", "floors.yard.z": "yardZ",
             "floors.yard.outline": "yardOutline"}
READS_LINE = ("valueStatus.overrideReads maps an override of overrideSets to the fields of its set the code still computes from other entries, "
              "each to the list of them, the settings hold %s (R401)")
MOVED_LINE = "valueStatus.movedBy maps a field to the field whose value tells whether an override moved it, the settings hold %s (R401)"
ORDER_LINE = ("valueStatus.worstFirst lists the statuses a value of several block entries takes first, the worst first, each of %s or %s, the settings "
              "hold %%s (R401)") % (", ".join(LABELS[:-1]), LABELS[-1])


def override_sets(sets):
    """overrideSets, each override to a list of field names, refused with one line when it is missing or of another shape."""
    if not isinstance(sets, dict) or not sets:
        raise project_profile.ProfileError(SETS_LINE % "none")
    for name, fields in sets.items():
        if not isinstance(fields, list) or not fields or not all(isinstance(field, str) for field in fields):
            # json.dumps, so the line is the one of the C#
            raise project_profile.ProfileError(SETS_LINE % ("%s: %s" % (name, json.dumps(fields))))
    return sets


def worst_first(order):
    """Q107. worstFirst, a list of the labels of LABELS, refused with one line when it is missing, empty or holds another."""
    if not isinstance(order, list) or not order or not all(isinstance(name, str) and name in LABELS for name in order):
        # json.dumps, so the line is the one of the C#
        raise project_profile.ProfileError(ORDER_LINE % ("none" if order is None else json.dumps(order)))
    return order


def override_reads(reads, sets):
    """Q107, R2 of the review of PR 75. overrideReads, per override of sets the fields of its set the code still computes from other entries,
    each to the list of them, refused with one line when it is missing or of another shape."""
    if not isinstance(reads, dict):
        raise project_profile.ProfileError(READS_LINE % "none")
    for name, fields in reads.items():
        if name not in sets or not isinstance(fields, dict) or not fields or not all(
                field in sets[name] and isinstance(refs, list) and refs and all(isinstance(ref, dict) for ref in refs) for field, refs in fields.items()):
            # json.dumps, so the line is the one of the C#
            raise project_profile.ProfileError(READS_LINE % ("%s: %s" % (name, json.dumps(fields))))
    return reads


def moved_by(found):
    """Q107, R3 of the review of PR 75. movedBy, a field to the field whose value tells whether an override moved it, refused with one line
    when it is missing or of another shape."""
    if not isinstance(found, dict) or not all(isinstance(value, str) for value in found.values()):
        raise project_profile.ProfileError(MOVED_LINE % ("none" if found is None else json.dumps(found)))
    return found


class Table:
    """The map of valueStatus and the block status, read once per build for the level profile the build follows, and the details the build
    used by their key, the table statusFrom of the plan."""

    def __init__(self, fields, block, profile_file, level_profile, decimals=None):
        fields = fields or {}
        self.sources, self.fields = fields.get("sources") or {}, fields.get("fields") or {}
        self.sets = override_sets(fields.get("overrideSets")) if fields else {}
        self.order = worst_first(fields.get("worstFirst")) if fields else []
        self.reads = override_reads(fields.get("overrideReads"), self.sets) if fields else {}
        self.moved_by = moved_by(fields.get("movedBy")) if fields else {}
        self.decimals = decimals or {}
        self.block = block or {}
        self.files = {"profile": profile_file, "config": SETTINGS_FILE}
        self.level_profile = level_profile
        self.used = {}

    def refs(self, field, source):
        for name in (field, ANY_PARAMETER) if field.startswith(PARAMETERS) else (field,):
            found = (self.fields.get(name) or {}).get(source)
            if found is not None:
                return found if isinstance(found, list) else [found]
        found = self.sources.get(source)
        return None if found is None else found if isinstance(found, list) else [found]

    def of(self, field, source, hand=None, applied=(), unmoved=False):
        """The status of a value of field from source, its detail and the key of the detail. An applied override whose set holds the field
        gives a value the status of the source override, as its source stays, applied in the order the build applied them so the last whose
        set holds the field names the override that moved the value, with the entries the code still reads for it by overrideReads, and
        unmoved, the value equal to the one with no override, leaves the value its own list (Q107)."""
        moved = None if source == OVERRIDE or unmoved else next((name for name in reversed(list(applied or ())) if field in self.sets.get(name, ())), None)
        if moved is not None:
            source = OVERRIDE
        refs = self.refs(field, source)
        if moved is not None and refs is not None:
            refs = refs + list((self.reads.get(moved) or {}).get(field) or ())
        if refs is None:
            entries = [{"status": "UNKNOWN", "why": "valueStatus holds no status for %s from %s (R401)" % (field, source)}]
        else:
            entries = [self.resolve(field, source, ref, hand) for ref in refs]
        # Q107. The worst status of the list by worstFirst, else the first entry
        ranked = [(self.order.index(entry_label(entry)), index) for index, entry in enumerate(entries) if entry_label(entry) in self.order]
        chosen = entries[min(ranked)[1]] if ranked else entries[0]
        detail = {"entries": entries}
        if chosen["status"] == MEASURED and chosen.get("strength"):
            detail["strength"] = chosen["strength"]
        if moved is not None:
            detail["override"] = moved
        key = "%s from %s" % (field, source)
        if any("{hand}" in ref.get(kind, "") for ref in refs or () for kind in ("profile", "config")):
            key += " hand %s" % hand
        if moved is not None:
            key += " by " + moved
        return chosen["status"], detail, key

    def resolve(self, field, source, ref, hand):
        if "status" in ref:
            return dict(ref)
        kind = "profile" if "profile" in ref else "config" if "config" in ref else None
        if kind is None:
            return {"status": "UNKNOWN", "why": "valueStatus names no block entry and no status for %s from %s (R401)" % (field, source)}
        names = {"levelProfile": self.level_profile, "parameter": field[len(PARAMETERS):] if field.startswith(PARAMETERS) else None, "hand": hand}
        lacking = [word for word in PLACEHOLDER.findall(ref[kind]) if names.get(word) is None]
        if lacking:
            return {"entry": ref[kind], "status": "UNKNOWN", "why": "the plot holds no %s (R401)" % lacking[0]}
        path = "%s %s" % (self.files[kind], PLACEHOLDER.sub(lambda found: names[found.group(1)], ref[kind]))
        entry = (self.block.get(self.files[kind]) or {}).get(path.split(" ", 1)[1])
        if not isinstance(entry, dict) or "status" not in entry:
            return {"entry": path, "status": "UNKNOWN", "why": "the block status holds no entry (R401)"}
        return {"entry": path, "status": entry["status"], **{key: value for key, value in entry.items() if key != "status"}}


def entry_label(entry):
    return entry["status"] + (" " + entry["strength"] if entry["status"] == MEASURED and entry.get("strength") else "")


def table_of(settings, profile, level_profile):
    """The table of a build: valueStatus of the settings, the block status of the rules profile and the file name it is kept under."""
    steps = (((settings.get("addin") or {}).get("parity") or {}).get("steps") or {}).get("overrides") or {}
    return Table(settings.get("valueStatus"), profile.get("status"), settings.get("profile"), level_profile,
                 {key: field["decimals"] for key, field in steps.items() if isinstance(field, dict) and field.get("decimals") is not None})


def is_sourced(value):
    return isinstance(value, dict) and "value" in value and "source" in value


def fields_of(entry):
    """Every sourced value of a plot entry by its field, the dotted path of its keys, in the order of the entry."""
    found = {}

    def walk(value, path):
        if is_sourced(value):
            found[".".join(path)] = value
        elif isinstance(value, dict):
            for key, item in value.items():
                walk(item, path + [key])
    walk(entry, [])
    return found


def written(value, decimals):
    """A value at the decimals of the plan, each number rounded by R385, a list or an object item by item."""
    if decimals is None or isinstance(value, (bool, str)) or value is None:
        return value
    if isinstance(value, dict):
        return {key: written(item, decimals) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [written(item, decimals) for item in value]
    return common.fixed(value, decimals) if isinstance(value, float) else value


def unmoved(field, entry_fields, rule_fields, table):
    """Q107, R3 of the review of PR 75. True when the value movedBy names for field, else that of field, is the same at the decimals the
    plan writes it in the entry and in the entry the build gives the plot with no override, false for a field of no step key, parameter or
    linkName, as ValueStatus.Unmoved of the C#."""
    if rule_fields is None:
        return False
    # movedBy may add lowest: the lowest Z of the points of that field, which the heightOffset of a ramp is taken from
    name, _, how = table.moved_by.get(field, field).partition(" ")
    if name not in STEP_KEYS and name != "linkName" and not name.startswith(PARAMETERS):
        return False
    mine, rule = entry_fields.get(name), rule_fields.get(name)
    if mine is None or rule is None:
        return False
    decimals = table.decimals.get(STEP_KEYS.get(name))
    mine, rule = written(mine["value"], decimals), written(rule["value"], decimals)
    if how == "lowest":
        return min(point[-1] for point in mine) == min(point[-1] for point in rule)
    return mine == rule


def mark(entry, table, hand=None, applied=(), rule=None):
    """Writes status and statusFrom, the key of its detail in table.used, beside each sourced value of the entry, hand the hand table the
    plot took, applied the overrides of the list the build applied on it and rule the entry the build gives the plot with no override."""
    found = fields_of(entry)
    rule_fields = None if rule is None else fields_of(rule)
    for field, item in found.items():
        item["status"], detail, item["statusFrom"] = table.of(field, item["source"], hand, applied, bool(applied) and unmoved(field, found, rule_fields, table))
        table.used[item["statusFrom"]] = detail


def label(item, details):
    """The status a count names, MEASURED with the strength of its detail."""
    strength = (details.get(item["statusFrom"]) or {}).get("strength")
    return item["status"] + (" " + strength if item["status"] == MEASURED and strength else "")


def summary(plots, assumed, details):
    """R401. The plots by field and status, the assumed plots by field and the plots that hold a value of each status, for step 15."""
    by_field, listed, holding = defaultdict(Counter), defaultdict(list), defaultdict(set)
    assumed = set(assumed or ())
    for entry in plots:
        for field, item in fields_of(entry).items():
            name = label(item, details)
            by_field[field][name] += 1
            holding[name].add(entry["plot"])
            if name in assumed:
                listed[field].append(entry["plot"])
                holding[ASSUMED].add(entry["plot"])
    return {"statusByField": {field: dict(sorted(counts.items())) for field, counts in sorted(by_field.items())},
            "assumedByField": {field: sorted(plots, key=link_summary.sort_key) for field, plots in sorted(listed.items())},
            "statusPlots": {name: len(found) for name, found in sorted(holding.items(), key=lambda item: (item[0] == ASSUMED, item[0]))}}
