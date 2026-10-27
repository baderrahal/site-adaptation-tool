"""R401, the plan part. The status of each sourced value of a plot of the plan, one of the six of rule_status.py, taken once per build from
the block status of the rules profile through valueStatus of config/project.json, which names per field and source the block entry that
gives the status, or the status itself for an override or a client input. A pair the map does not hold, or an entry the block lacks, is
UNKNOWN with why, never a guess. A status changes no value: it is written beside value and source, with statusFrom, the detail that gives
it. ValueStatus.cs of the C# gives the same."""
import re
from collections import Counter, defaultdict

import link_summary

# rule_status.SETTINGS_FILE, the key of the block entries of the settings
SETTINGS_FILE = "config/project.json"
MEASURED = "MEASURED"
OVERRIDE = "override"
PARAMETERS = "parameters."
ANY_PARAMETER = "parameters.*"
PLACEHOLDER = re.compile(r"\{(\w+)\}")
ASSUMED = "assumed"


class Table:
    """The map of valueStatus and the block status, read once per build, for the level profile the build follows."""

    def __init__(self, fields, block, profile_file, level_profile):
        fields = fields or {}
        self.sources, self.fields = fields.get("sources") or {}, fields.get("fields") or {}
        self.sets = fields.get("overrideSets") or {}
        self.block = block or {}
        self.files = {"profile": profile_file, "config": SETTINGS_FILE}
        self.level_profile = level_profile

    def of(self, field, source, hand=None, applied=()):
        """The status of a value of field from source and its detail. An applied override of overrideSets that sets the field gives it the
        status of the source override, as its source stays."""
        moved = next((name for name in applied or () if self.sets.get(name) == field), None)
        if moved is not None:
            source = OVERRIDE
        ref = None
        for name in (field, ANY_PARAMETER) if field.startswith(PARAMETERS) else (field,):
            ref = (self.fields.get(name) or {}).get(source)
            if ref is not None:
                break
        if ref is None:
            ref = self.sources.get(source)
        status, detail = self.resolve(field, source, ref, hand)
        if moved is not None:
            detail["override"] = moved
        return status, detail

    def resolve(self, field, source, ref, hand):
        if ref is None:
            return "UNKNOWN", {"why": "valueStatus holds no status for %s from %s (R401)" % (field, source)}
        if "status" in ref:
            return ref["status"], {key: value for key, value in ref.items() if key != "status"}
        kind = "profile" if "profile" in ref else "config" if "config" in ref else None
        if kind is None:
            return "UNKNOWN", {"why": "valueStatus names no block entry and no status for %s from %s (R401)" % (field, source)}
        names = {"levelProfile": self.level_profile, "parameter": field[len(PARAMETERS):] if field.startswith(PARAMETERS) else None, "hand": hand}
        lacking = [word for word in PLACEHOLDER.findall(ref[kind]) if names.get(word) is None]
        if lacking:
            return "UNKNOWN", {"why": "the plot holds no %s for %s (R401)" % (lacking[0], ref[kind])}
        path = PLACEHOLDER.sub(lambda found: names[found.group(1)], ref[kind])
        entry = (self.block.get(self.files[kind]) or {}).get(path)
        if not isinstance(entry, dict) or "status" not in entry:
            return "UNKNOWN", {"why": "the block status holds no entry %s %s (R401)" % (self.files[kind], path)}
        return entry["status"], {"entry": "%s %s" % (self.files[kind], path), **{key: value for key, value in entry.items() if key != "status"}}


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
    """Writes status and statusFrom beside each sourced value of the entry, hand the hand table the plot took and applied the overrides of
    the list the build applied on it."""
    for field, item in fields_of(entry).items():
        item["status"], item["statusFrom"] = table.of(field, item["source"], hand, applied)


def label(item):
    """The status a count names, MEASURED with its strength."""
    strength = (item.get("statusFrom") or {}).get("strength")
    return item["status"] + (" " + strength if item["status"] == MEASURED and strength else "")


def summary(plots, assumed):
    """R401. The plots by field and status, the assumed plots by field and the plots that hold a value of each status, for step 15."""
    by_field, listed, holding = defaultdict(Counter), defaultdict(list), defaultdict(set)
    assumed = set(assumed or ())
    for entry in plots:
        for field, item in fields_of(entry).items():
            name = label(item)
            by_field[field][name] += 1
            holding[name].add(entry["plot"])
            if name in assumed:
                listed[field].append(entry["plot"])
                holding[ASSUMED].add(entry["plot"])
    return {"statusByField": {field: dict(sorted(counts.items())) for field, counts in sorted(by_field.items())},
            "assumedByField": {field: sorted(plots, key=link_summary.sort_key) for field, plots in sorted(listed.items())},
            "statusPlots": {name: len(found) for name, found in sorted(holding.items(), key=lambda item: (item[0] == ASSUMED, item[0]))}}
