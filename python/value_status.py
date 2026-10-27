"""R401, the plan part. The status of each sourced value of a plot of the plan, one of the six of rule_status.py, taken once per build from
the block status of the rules profile through valueStatus of config/project.json. The map names per field and source one block entry or a
list of them, by the code that computes the value, or the status itself for an override or a client input. A value of several entries takes
the status of the first assumed one among them, else of the first, and lists each. An override of the list marks every field it moves,
overrideSets, with the status of the source override. A pair the map does not hold, or an entry the block lacks, is UNKNOWN with why, never
a guess. A status changes no value: a value holds its status and the key of its detail, and the detail is written once in the table
statusFrom at the top of the plan. ValueStatus.cs of the C# gives the same."""
import json
import re
from collections import Counter, defaultdict

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


def override_sets(sets):
    """overrideSets, each override to a list of field names, refused with one line when it is missing or of another shape."""
    if not isinstance(sets, dict) or not sets:
        raise project_profile.ProfileError(SETS_LINE % "none")
    for name, fields in sets.items():
        if not isinstance(fields, list) or not fields or not all(isinstance(field, str) for field in fields):
            # json.dumps, so the line is the one of the C#
            raise project_profile.ProfileError(SETS_LINE % ("%s: %s" % (name, json.dumps(fields))))
    return sets


class Table:
    """The map of valueStatus and the block status, read once per build for the level profile the build follows, and the details the build
    used by their key, the table statusFrom of the plan."""

    def __init__(self, fields, block, profile_file, level_profile):
        fields = fields or {}
        self.sources, self.fields = fields.get("sources") or {}, fields.get("fields") or {}
        self.sets = override_sets(fields.get("overrideSets")) if fields else {}
        self.assumed = set(fields.get("assumed") or ())
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

    def of(self, field, source, hand=None, applied=()):
        """The status of a value of field from source, its detail and the key of the detail. An applied override whose set holds the field
        gives a value the status of the source override, as its source stays."""
        moved = None if source == OVERRIDE else next((name for name in applied or () if field in self.sets.get(name, ())), None)
        if moved is not None:
            source = OVERRIDE
        refs = self.refs(field, source)
        if refs is None:
            entries = [{"status": "UNKNOWN", "why": "valueStatus holds no status for %s from %s (R401)" % (field, source)}]
        else:
            entries = [self.resolve(field, source, ref, hand) for ref in refs]
        chosen = next((entry for entry in entries if entry_label(entry) in self.assumed), entries[0])
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
    return Table(settings.get("valueStatus"), profile.get("status"), settings.get("profile"), level_profile)


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


def mark(entry, table, hand=None, applied=()):
    """Writes status and statusFrom, the key of its detail in table.used, beside each sourced value of the entry, hand the hand table the
    plot took and applied the overrides of the list the build applied on it."""
    for field, item in fields_of(entry).items():
        item["status"], detail, item["statusFrom"] = table.of(field, item["source"], hand, applied)
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
