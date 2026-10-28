"""R401, the plan part. The status of each sourced value of a plot of the plan, one of the six of rule_status.py, taken once per build from
the block status of the rules profile through valueStatus of config/project.json. The map names per field and source one block entry or a
list of them, by the code that computes the value, or the status itself for an override or a client input. A value of several entries takes
the worst status among them in the order of valueStatus.worstFirst, else the status of the first, and lists each (Q107). An override of the
list marks every field it moves, overrideSets, with the status of the source override, and the value names every applied override whose set
holds its field, the overrides that touched its inputs, in the order the build applied them (Q112, Q113). An entry may name the unit code of
the plot, prototypeFile the keo_file of its typology (Q110).
A pair the map does not hold, or an entry the block lacks, is UNKNOWN with why, never a guess. A status changes no value: a value holds its
status and the key of its detail, and the detail is written once in the table statusFrom at the top of the plan. ValueStatus.cs of the C#
gives the same."""
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
# The why of a value whose field and source valueStatus maps to no entry, NoStatus of ValueStatus.cs
NO_STATUS = "valueStatus holds no status for %s from %s (R401)"
SETS_LINE = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold %s (R401)"
# The labels a status of a value takes, the six of rule_status.py with MEASURED by its strength, which worstFirst names
LABELS = ("UNKNOWN", "CHOSEN by Code", "CHOSEN by Bader", "RECOMMENDED", "MEASURED weak", "MEASURED strong", "MEASURED", "DOCUMENT")
READS_LINE = ("valueStatus.overrideReads maps an override of overrideSets to the fields of its set the code still computes from other entries, "
              "each to the list of them, the settings hold %s (R401)")
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


def decided_by(found):
    """Review 1 of PR 78. majority.decidedByQuestion of the settings by (plot, field), each to the list of (question, key) that list the plot
    under the field, as build_proof.py reads it. The note and any item that is no object are left out."""
    decided = {}
    for question, rule in (found or {}).items():
        if not isinstance(rule, dict):
            continue
        for key, plots in (rule.get("plots") or {}).items():
            for plot in plots or ():
                for field in rule.get("fields") or ():
                    decided.setdefault((str(plot), field), []).append((question, key))
    return decided


class Table:
    """The map of valueStatus and the block status, read once per build for the level profile the build follows, and the details the build
    used by their key, the table statusFrom of the plan."""

    def __init__(self, fields, block, profile_file, level_profile, decided=None):
        fields = fields or {}
        self.sources, self.fields = fields.get("sources") or {}, fields.get("fields") or {}
        self.sets = override_sets(fields.get("overrideSets")) if fields else {}
        self.order = worst_first(fields.get("worstFirst")) if fields else []
        self.reads = override_reads(fields.get("overrideReads"), self.sets) if fields else {}
        self.block = block or {}
        self.files = {"profile": profile_file, "config": SETTINGS_FILE}
        self.level_profile = level_profile
        self.decided = decided_by(decided)
        self.used = {}

    def refs(self, field, source):
        for name in (field, ANY_PARAMETER) if field.startswith(PARAMETERS) else (field,):
            found = (self.fields.get(name) or {}).get(source)
            if found is not None:
                return found if isinstance(found, list) else [found]
        found = self.sources.get(source)
        return None if found is None else found if isinstance(found, list) else [found]

    def of(self, field, source, hand=None, applied=(), unit=None, plot=None):
        """The status of a value of field from source, its detail and the key of the detail. Every applied override whose set holds the field
        touched an input of the value, so the value takes the status of the source override, as its source stays, with the entries the code
        still reads for it by overrideReads of the override applied last that touched it, none when that one sets the field outright, as the
        code computes the field last after it (R1 of the review of PR 85), and names each override once in the order the build applied them
        (Q112, the fallback of its answer, as one more build per override added more than 20 percent to a build of the 7 keys). It does so
        also where the value equals its value with no override, as a value takes the worst status of its inputs (Q113). unit is the unit code
        of the plot, which an entry names as {unit}, the keo_file of its typology for prototypeFile (Q110). A field an open question of majority.decidedByQuestion
        decides on the plot adds the block entry of that question and key, so it takes the worst of its list and that question (review 1 of
        PR 78)."""
        moved = [] if source == OVERRIDE else list(dict.fromkeys(name for name in applied or () if field in self.sets.get(name, ())))
        if moved:
            source = OVERRIDE
        refs = self.refs(field, source)
        if moved and refs is not None:
            # R1 of the review of PR 85. The code computes the field last after the override applied last that touched it, so what that
            # override reads for it, none when it sets the field outright, is what the value still reads
            refs = list(refs) + [ref for ref in (self.reads.get(moved[-1]) or {}).get(field) or () if ref not in refs]
        if refs is None:
            entries = [{"status": "UNKNOWN", "why": NO_STATUS % (field, source)}]
        else:
            entries = [self.resolve(field, source, ref, hand, unit) for ref in refs]
        pairs = self.decided.get((str(plot), field), []) if plot is not None else []
        entries += [self.resolve(field, source, {"config": "majority.decidedByQuestion.%s.plots.%s" % pair}, hand, unit) for pair in pairs]
        # Q107. The worst status of the list by worstFirst, else the first entry, the first of a tie
        chosen = worst_entries(entries, self.order)[0]
        detail = {"entries": entries}
        if chosen["status"] == MEASURED and chosen.get("strength"):
            detail["strength"] = chosen["strength"]
        if moved:
            detail["overrides"] = moved
        key = "%s from %s" % (field, source)
        if any("{hand}" in ref.get(kind, "") for ref in refs or () for kind in ("profile", "config")):
            key += " hand %s" % hand
        # Q110. The detail of an entry named by the unit differs per unit, so the key names it
        if any("{unit}" in ref.get(kind, "") for ref in refs or () for kind in ("profile", "config")):
            key += " unit %s" % unit
        if pairs:
            key += " question " + " ".join("%s %s" % pair for pair in pairs)
        if moved:
            key += " by " + " ".join(moved)
        return chosen["status"], detail, key

    def resolve(self, field, source, ref, hand, unit=None):
        if "status" in ref:
            return dict(ref)
        kind = "profile" if "profile" in ref else "config" if "config" in ref else None
        if kind is None:
            return {"status": "UNKNOWN", "why": "valueStatus names no block entry and no status for %s from %s (R401)" % (field, source)}
        names = {"levelProfile": self.level_profile, "parameter": field[len(PARAMETERS):] if field.startswith(PARAMETERS) else None, "hand": hand,
                 "unit": unit}
        lacking = [word for word in PLACEHOLDER.findall(ref[kind]) if names.get(word) is None]
        if lacking:
            return {"entry": ref[kind], "status": "UNKNOWN", "why": "the plot holds no %s (R401)" % lacking[0]}
        path = "%s %s" % (self.files[kind], PLACEHOLDER.sub(lambda found: names[found.group(1)], ref[kind]))
        entry = (self.block.get(self.files[kind]) or {}).get(path.split(" ", 1)[1])
        if not isinstance(entry, dict) or "status" not in entry:
            return {"entry": path, "status": "UNKNOWN", "why": "the block status holds no entry (R401)"}
        return {"entry": path, "status": entry["status"], **{key: value for key, value in entry.items() if key != "status"}}


def entry_label(entry):
    return status_label(entry["status"], entry)


def status_label(status, detail):
    """The label of a status, MEASURED with the strength its detail or entry holds, ValueStatus.Label of the C#."""
    return status + (" " + detail["strength"] if status == MEASURED and detail.get("strength") else "")


def worst_entries(entries, order):
    """Q107, R363. The entries of a value that hold its worst status by order, worstFirst, in list order, a tie giving each, else the first entry
    alone. Table.of takes the first of them, the chip Assumed of Q108 counts the value under each (review 1 of PR 81)."""
    ranked = [order.index(entry_label(entry)) if entry_label(entry) in order else None for entry in entries]
    held = [rank for rank in ranked if rank is not None]
    if not held:
        return entries[:1]
    return [entry for entry, rank in zip(entries, ranked) if rank == min(held)]


def table_of(settings, profile, level_profile):
    """The table of a build: valueStatus of the settings, the block status of the rules profile and the file name it is kept under."""
    return Table(settings.get("valueStatus"), profile.get("status"), settings.get("profile"), level_profile, (settings.get("majority") or {}).get("decidedByQuestion"))


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
    plot took and applied the overrides of the list the build applied on it, in the order it applied them."""
    found = fields_of(entry)
    unit, plot = (found.get("unit") or {}).get("value"), entry.get("plot")
    for field, item in found.items():
        item["status"], detail, item["statusFrom"] = table.of(field, item["source"], hand, applied, unit, plot)
        table.used[item["statusFrom"]] = detail


def label(item, details):
    """The status a count names, MEASURED with the strength of its detail."""
    return status_label(item["status"], details.get(item["statusFrom"]) or {})


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
