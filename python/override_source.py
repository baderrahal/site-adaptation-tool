"""R330. The source of every override of config/overrides: the job of its key, parsons or native, with its past model apart.

An override is a gap to the rule of its own job (R280), so each entry names that job in source, the group of majority.groups in
config/project.json that holds its key, and the past model file it came from in model. Before Round ADDIN 3a source held the
model file, migrate gives such an entry the job of its key and moves the file to model. A build applies by default only the
sources majority.overrideDefaultSources names, parsons, so the native overrides stay in the list and are never applied unless
a build names their source (plan_rules.load_overrides). merge keeps the entries of the earlier list a run does not measure.

python python/override_source.py migrate <list> rewrites a list of before the source field in place, refused when any entry
is not whole, python python/override_source.py count <list> prints the counts by source, field and key.
"""
import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

import link_summary
import project_profile

ORDER = ("plot", "field", "value", "rule", "source", "model", "key", "decision", "reason")
BY_HAND = "byHand"


def job_of_key(groups):
    return {key: job for job, keys in groups.items() for key in keys}


def migrate_entry(item, groups):
    """R330. The entry with source the job of its key and model its past model file. An entry of before the field held the
    model file in source. A key of no group, or a source that names a job other than the group of its key, is refused."""
    jobs = job_of_key(groups)
    job = jobs.get(item.get("key"))
    if job is None:
        raise project_profile.ProfileError("override of plot %s field %s names key %s, which no group of majority.groups holds (R330)"
                                           % (item.get("plot"), item.get("field"), item.get("key")))
    source = item.get("source")
    if "model" in item:
        if source != job:
            raise project_profile.ProfileError("override of plot %s field %s has source %s, the job of its key %s is %s (R330)"
                                               % (item.get("plot"), item.get("field"), source, item.get("key"), job))
        model = item["model"]
    else:
        if source is None or source in groups:
            raise project_profile.ProfileError("override of plot %s field %s names no past model, an entry with a job as source needs model (R330)"
                                               % (item.get("plot"), item.get("field")))
        model = source
    moved = dict(item, source=job, model=model)
    return {**{name: moved[name] for name in ORDER if name in moved}, **{name: value for name, value in moved.items() if name not in ORDER}}


def check_entries(entries, groups):
    """R330. Every entry must name the job of its key as source and its past model in model, and byHand, the mark of an entry
    Bader edited by hand, is true or left out. One error lists every fault."""
    faults = []
    for item in entries:
        try:
            if migrate_entry(item, groups) != item or "model" not in item:
                raise project_profile.ProfileError("override of plot %s field %s is not in the form of R330, run override_source.py migrate"
                                                   % (item.get("plot"), item.get("field")))
            by_hand(item)
        except project_profile.ProfileError as error:
            faults.append(str(error))
    if faults:
        raise project_profile.ProfileError("%d overrides refused: %s" % (len(faults), " | ".join(faults[:5])) + (" and more" if len(faults) > 5 else ""))
    return entries


def by_hand(item):
    """R330. True for an entry Bader marked as edited by hand, "byHand": true at the end of its line, False for one with no mark. A
    byHand of any other value, such as "true" or 1, and the mark spelled otherwise, such as byhand or by_hand, are refused loudly,
    never read as no mark, so a run never drops a hand edit in silence."""
    for name in item:
        if name != BY_HAND and re.sub("[^a-z]", "", str(name).lower()) == BY_HAND.lower():
            raise project_profile.ProfileError("override of plot %s field %s marks a hand edit as %s, spell it \"byHand\": true (R330)"
                                               % (item.get("plot"), item.get("field"), json.dumps(name)))
    if BY_HAND not in item:
        return False
    if item[BY_HAND] is not True:
        raise project_profile.ProfileError("override of plot %s field %s has byHand %s, mark an entry edited by hand with \"byHand\": true or leave it out (R330)"
                                           % (item.get("plot"), item.get("field"), json.dumps(item[BY_HAND])))
    return True


def with_hand_edits(earlier, replaced):
    """R330. The earlier list a run keeps from, --keep-from, with every entry marked byHand of the list the run writes over that
    it lacks, so a hand edit of the committed list is kept or the run refused, never lost. The same plot, key and field held
    otherwise by the two lists is a conflict, refused with both named."""
    index = {(str(item["plot"]), item.get("key"), item.get("field")): item for item in earlier}
    added, clash = [], []
    for item in replaced:
        if not by_hand(item):
            continue
        found = index.get((str(item["plot"]), item.get("key"), item.get("field")))
        if found is None:
            added.append(item)
        elif found != item:
            clash.append("plot %s field %s key %s" % (item["plot"], item.get("field"), item.get("key")))
    if clash:
        raise project_profile.ProfileError("the list the run writes over holds %d hand edits that the --keep-from list holds otherwise: %s. Edit one of the two by hand (R330)"
                                           % (len(clash), " | ".join(clash[:10])))
    return earlier + added


def default_sources(settings):
    """R330. The sources a build applies when it names none, majority.overrideDefaultSources, jobs of majority.groups, each once."""
    block = settings.get("majority")
    found = block.get("overrideDefaultSources")
    if not isinstance(found, list) or not found or len(set(found)) != len(found) or any(name not in block["groups"] for name in found):
        raise project_profile.ProfileError(settings.path.name + " majority.overrideDefaultSources must list jobs of majority.groups, each once (R330)")
    return list(found)


def parse_keep(values, groups, earlier=None, flag="--keep"):
    """R330. What a run keeps of the earlier list: SOURCE keeps every entry of that job, SOURCE:FIELD,FIELD only those fields.
    With earlier, a source or a field no entry of that source in it has is refused, so a misspelt field never keeps nothing."""
    keep = {}
    for value in values:
        source, colon, fields = value.partition(":")
        names = fields.split(",") if colon else None
        if source not in groups or source in keep or (names is not None and (not names or any(not name.strip() for name in names))):
            raise project_profile.ProfileError(flag + " " + value + " must be a job of majority.groups once, with :field,field or none (R330)")
        if earlier is not None:
            held = {item.get("field") for item in earlier if item.get("source") == source}
            missing = sorted(set(names or ()) - held) if held else [source]
            if missing:
                raise project_profile.ProfileError("%s %s: the earlier list has no entry of %s %s, nothing to keep (R330)" % (flag, value, source, ", ".join(missing)))
        keep[source] = None if names is None else set(names)
    return keep


def keep_block(block):
    """R330. majority.overrideKeep of the config: sources, the jobs whose every entry a run keeps (native, measured in Round 17),
    and laterFields, per job the fields a later phase (laterPhase) measures, whose entries a run keeps and never writes."""
    found = block.get("overrideKeep")
    groups = block["groups"]
    if (not isinstance(found, dict) or not isinstance(found.get("sources"), list) or any(source not in groups for source in found["sources"])
            or not isinstance(found.get("laterFields"), dict) or not isinstance(found.get("laterPhase"), str) or not found["laterPhase"].strip()
            or any(source not in groups or not isinstance(fields, list) or not fields or any(not isinstance(name, str) or not name.strip() for name in fields)
                   for source, fields in found["laterFields"].items())):
        raise project_profile.ProfileError("majority.overrideKeep must hold sources, jobs of majority.groups, laterFields, per job a list of fields, "
                                           "and laterPhase, the phase that measures them (R330)")
    return found


def keep_of(block, extra, replace, earlier):
    """R330. What a run keeps of the earlier list: every entry of the sources of majority.overrideKeep and the laterFields of
    each job, then the --keep values added and the --replace values taken out. --replace may name only what is kept, a whole
    source or fields of a job kept by field."""
    found = keep_block(block)
    keep = {source: None for source in found["sources"]}
    for source, fields in found["laterFields"].items():
        if source not in keep:
            keep[source] = set(fields)
        elif keep[source] is not None:
            keep[source] |= set(fields)
    for source, fields in parse_keep(extra, block["groups"], earlier).items():
        keep[source] = None if fields is None or keep.get(source, set()) is None else set(keep.get(source) or ()) | fields
    for source, fields in parse_keep(replace, block["groups"], flag="--replace").items():
        if source not in keep or (fields is not None and (keep[source] is None or not fields <= keep[source])):
            raise project_profile.ProfileError("--replace %s%s names what the run does not keep, it keeps %s (R330)"
                                               % (source, "" if fields is None else ":" + ",".join(sorted(fields)), describe(keep)))
        if fields is None:
            del keep[source]
        else:
            keep[source] -= fields
            if not keep[source]:
                del keep[source]
    return keep


def describe(keep):
    return ", ".join(source if fields is None else source + ":" + ",".join(sorted(fields)) for source, fields in sorted(keep.items())) or "nothing"


def kept_by(item, keep):
    fields = keep.get(item["source"], False)
    return fields is None or (fields is not False and item["field"] in fields)


def merge(earlier, run, keep, joined=None):
    """R330. The entries of earlier that keep names, cell for cell, and every entry of the run that keep does not name, sorted
    by plot, key and field as R272 sorts the list, so Bader finds a plot in one place. With joined, the key and plot pairs of
    past_all.csv, a kept entry of a plot the join gives no row of its key is dropped, no key overrides a plot it does not hold.
    An entry marked byHand is never dropped: a run that would drop one, as replaced or off the join, is refused."""
    kept, off_join, lost = [], [], []
    for item in earlier:
        if not kept_by(item, keep):
            if by_hand(item):
                lost.append(item)
            continue
        if joined is not None and (item.get("key"), str(item["plot"])) not in joined:
            (lost if by_hand(item) else off_join).append(item)
            continue
        kept.append(item)
    if lost:
        raise project_profile.ProfileError("the run would drop %d entries marked byHand: %s. Keep their field with --keep or take the byHand mark off by hand (R330)"
                                           % (len(lost), " | ".join("plot %s field %s key %s" % (item["plot"], item["field"], item.get("key")) for item in lost[:10])))
    new = [item for item in run if not kept_by(item, keep)]
    dropped = [item for item in run if kept_by(item, keep)]
    tally = lambda items: dict(sorted(Counter(item["source"] for item in items).items()))
    merged = sorted(kept + new, key=lambda item: (link_summary.sort_key(str(item["plot"])), str(item.get("key")), str(item.get("field"))))
    return merged, {"kept": tally(kept), "new": tally(new), "droppedFromRun": tally(dropped), "droppedNoJoin": tally(off_join),
                    "droppedNoJoinEntries": [{name: item.get(name) for name in ("plot", "field", "key", "source")} for item in off_join]}


def counts(entries):
    """R330. The counts of a list by source, by source and field, and by key."""
    return {"total": len(entries), "bySource": dict(sorted(Counter(item.get("source") for item in entries).items())),
            "bySourceField": dict(sorted(Counter(str(item.get("source")) + " " + str(item.get("field")) for item in entries).items())),
            "byKey": dict(sorted(Counter(str(item.get("key")) for item in entries).items()))}


def list_path(settings):
    """R330. The override list of the project, config/overrides with the file name of the rules profile."""
    return project_profile.ROOT / "config" / "overrides" / Path(settings.get("profile")).name


def read_list(path):
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    if not isinstance(data, dict) or not isinstance(data.get("overrides"), list):
        raise project_profile.ProfileError(str(path) + " holds no override list")
    return data


def main(argv=None):
    parser = argparse.ArgumentParser(description="The source field of the override list (R330).")
    parser.add_argument("command", choices=("migrate", "count"))
    parser.add_argument("list", help="the override list, config/overrides/<profile>.json")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        groups = settings.get("majority.groups")
        data = read_list(args.list)
        if args.command == "migrate":
            before = counts(data["overrides"])
            moved = [migrate_entry(item, groups) for item in data["overrides"]]
            check_entries(moved, groups)
            import majority
            majority.write_overrides(args.list, data.get("note", ""), moved)
            print(json.dumps({"before": before, "after": counts(moved)}, indent=1))
        else:
            print(json.dumps(counts(data["overrides"]), indent=1))
    except (project_profile.ProfileError, OSError, ValueError) as error:
        print("override_source: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
