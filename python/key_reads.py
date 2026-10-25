"""R322. The counts of each answer key read in Round ADDIN Phase 2, from the reader output of the read job and its run files.

Per key: the link instances, those with a plot number, the link files and the loaded links, the floors by type and by the kind
the key reads them as (R321), the walls by type, the spot elevations by the kind of their host and those whose value reads (R320),
the levels, and the window time and peak memory of the read. A section the read lacks is null, never zero. Written to
key_reads.json in the reads folder, outside the repo, counts and names only, no coordinate or level.
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

import plan_from_past
import plot_relations
import project_profile
from common import round_tree, write_json

OUTPUT = "key_reads.json"
NO_KIND = "none"
IN_LINK = "in a link"
NO_HOST = "no host"
SECTIONS = ("links", "floors", "walls", "spotElevations")
SECTION_NAMES = {"links": "links", "floors": "floors", "walls": "walls", "spotElevations": "spots"}
WINDOW_STEPS = (("openSeconds", "openModel"), ("readSeconds", "read"))
MEMORY_KEYS = ("peakPrivateMib", "peakWorkingSetMib", "throughExit", "minFreeGib", "samplesUnderFloor", "notResponding")


def by_name(values):
    """A count per name, sorted by name, so two reads give the same order."""
    return dict(sorted(Counter(values).items()))


def link_counts(links):
    """R322. A link with no file names no file, and two names that differ only in case are one file, as Windows opens them."""
    if links is None:
        return None
    files = {str(link["file"]).lower() for link in links if link.get("file") is not None}
    return {"instances": len(links), "withPlotNumber": sum(1 for link in links if link.get("plotNumber") is not None),
            "files": len(files), "loaded": sum(1 for link in links if link.get("loaded") is True)}


def floor_counts(floors, settings_floors):
    """R321. Each floor by its type and by the kind the key reads it as, none when its type is of no kind."""
    if floors is None:
        return None
    kinds = Counter(plan_from_past.kind_of(floor, settings_floors) or NO_KIND for floor in floors)
    return {"count": len(floors), "byType": by_name(str(floor.get("typeName")) for floor in floors),
            "byKind": {kind: kinds[kind] for kind in plan_from_past.FLOOR_KINDS + (NO_KIND,)}}


def wall_counts(walls):
    if walls is None:
        return None
    return {"count": len(walls), "byType": by_name(str(wall.get("typeName")) for wall in walls)}


def link_files(links):
    """The file of each link instance of the read by its element id."""
    return {link.get("elementId"): link.get("file") for link in links or [] if link.get("elementId") is not None}


def host_kind(spot, files=None):
    """R322. The kind of the host of a spot and the link file when it is in a link: no host when none was found, in a link when
    its reference points into a link or a link instance of the read holds it, as the SE spots of the dependents route of R55,
    else the category and type of the host."""
    host = spot.get("host")
    if not host:
        return NO_HOST, None
    files = files or {}
    if host.get("linkedElementId") is not None or host.get("elementId") in files:
        return IN_LINK, files.get(host.get("elementId"))
    return str(host.get("category")) + ": " + str(host.get("typeName")), None


def spot_counts(spots, links=None):
    if spots is None:
        return None
    files = link_files(links)
    kinds = [host_kind(spot, files) for spot in spots]
    return {"count": len(spots), "withValue": sum(1 for spot in spots if spot.get("value") is not None),
            "byHost": by_name(kind for kind, _ in kinds),
            "inLinkByFile": by_name(str(file) for kind, file in kinds if kind == IN_LINK)}


def level_counts(site):
    levels = (site or {}).get("levels")
    if levels is None:
        return None
    return {"count": len(levels), "names": [level.get("name") for level in levels]}


def counts(document, settings_floors):
    """R322. The counts of one project document of the reader, read by the floors block of its key."""
    return {"links": link_counts(document.get("links")), "floors": floor_counts(document.get("floors"), settings_floors),
            "walls": wall_counts(document.get("walls")), "spots": spot_counts(document.get("spotElevations"), document.get("links")),
            "levels": level_counts(document.get("site"))}


def shared_rows(documents):
    """R322. Per key and per other key the rows of each section both reads hold, the same element id with the same whole row, as
    the 733 Generic floors of IMA and IMB. Element ids of two models meet by chance, so an id alone is no shared row."""
    result = {code: {} for code in documents}
    for code, document in documents.items():
        for other, second in documents.items():
            if other == code:
                continue
            for section in SECTIONS:
                rows = {row.get("elementId"): row for row in second.get(section) or [] if row.get("elementId") is not None}
                same = sum(1 for row in document.get(section) or [] if row.get("elementId") is not None and rows.get(row["elementId"]) == row)
                if same:
                    result[code].setdefault(other, {})[SECTION_NAMES[section]] = same
    return result


def read_of(reads, job):
    """The project document of the read of a job in the reads folder, None when its file is gone."""
    path = Path(reads) / job / ("testhost_" + job + "_prototypes.json")
    return project_document(load(path)) if path.is_file() else None


def with_shares(keys, documents, reads):
    """R322. sharedWith for every key of the file, from the reads of this run and the reads of the keys an earlier run wrote. A key
    whose read is gone keeps its counts with sharedWith null and is named in the list of keys whose shares were not checked."""
    found = dict(documents)
    for code, entry in keys.items():
        if code not in found:
            found[code] = read_of(reads, entry.get("job"))
    missing = [code for code in keys if found[code] is None]
    shares = shared_rows({code: document for code, document in found.items() if document is not None})
    for code, entry in keys.items():
        entry["sharedWith"] = None if code in missing else shares[code]
    return missing


def window_facts(run, log):
    """R322. The window time, the open and read seconds, the peaks of R310 and the result of the read job."""
    seconds = {step.get("name"): step.get("seconds") for step in log.get("steps") or []}
    memory = run.get("memory") or {}
    facts = {"minutes": run.get("minutes")}
    facts.update({key: seconds.get(step) for key, step in WINDOW_STEPS})
    facts.update({key: memory.get(key) for key in MEMORY_KEYS})
    facts.update({"exitCode": run.get("exitCode"), "logResult": log.get("result")})
    return facts


def load(path):
    with open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


def project_document(data):
    documents = [document for document in data.get("documents") or [] if document.get("kind") == "project"]
    if len(documents) != 1:
        raise ValueError("the read holds " + str(len(documents)) + " project documents, one is needed")
    return documents[0]


def refuse_repeats(parser, pairs):
    """R322, R323. A key or a job given twice is refused, so one read is never filed under two keys or two reads under one."""
    for place, what in ((0, "key"), (1, "job")):
        seen = Counter(pair[place] for pair in pairs)
        twice = sorted(name for name, count in seen.items() if count > 1)
        if twice:
            parser.error("the " + what + " " + ", ".join(twice) + " is given twice, give each once")


def key_entry(settings, reads, code, job):
    """R322. The entry of one key and the project document of its read."""
    folder = Path(reads) / job
    prefix = "testhost_" + job + "_"
    floors = plan_from_past.load_floors(settings, code)
    document = project_document(load(folder / (prefix + "prototypes.json")))
    plan_from_past.check_key_file(settings, code, document.get("file"), job)
    entry = {"job": job, "file": (folder / (prefix + "prototypes.json")).name, "fileMib": round((folder / (prefix + "prototypes.json")).stat().st_size / 1048576.0, 2),
             "errors": len(document.get("errors") or []), "counts": counts(document, floors),
             "window": window_facts(load(folder / (prefix + "run.json")), load(folder / (prefix + "log.json")))}
    return entry, document


def earlier_keys(path):
    """R322, R323. The entries of the report a run before wrote, by key, so a run of one key leaves the others in the file. A file
    that is no such report is refused and never written over."""
    path = Path(path)
    if not path.exists():
        return {}
    try:
        data = load(path)
    except ValueError as error:
        raise ValueError(path.name + " is no JSON, it is left as it is: " + str(error))
    if not isinstance(data, dict) or not isinstance(data.get("keys"), dict):
        raise ValueError(path.name + " holds no keys of a report, it is left as it is")
    return data["keys"]


def merged(path, entries):
    """The keys of the earlier report in their order with the entries of this run in place of their own, new keys at the end."""
    keys = dict(earlier_keys(path))
    keys.update(entries)
    return keys


def merged_settings(path, entries, settings, wide_keys=frozenset()):
    """R326, R327. merged, when every key of the earlier report this run leaves as it is holds the settings of this run, as the report writes
    them with wide_keys at 4 decimals (R2). Else it is refused with the keys that differ, so one file never holds keys measured at two
    settings: rerun every key or write to another folder."""
    written = round_tree(settings, wide_keys)
    differ = sorted(code for code, entry in earlier_keys(path).items() if code not in entries and entry.get("settings") != written)
    if differ:
        raise ValueError(Path(path).name + " holds " + ", ".join(differ) + " run with other settings than " + str(settings)
                         + ", give every key of the file to run them again or write to another folder")
    return merged(path, entries)


def parse_pair(text):
    code, sep, job = text.partition("=")
    if not sep or not code or not job:
        raise argparse.ArgumentTypeError("give the key and its read job as CODE=JOB, as IMC=RC")
    return code, job


def render(report):
    lines = []
    for code, entry in report["keys"].items():
        found, window = entry["counts"], entry["window"]
        links, floors, walls, spots, levels = found["links"], found["floors"], found["walls"], found["spots"], found["levels"]
        lines.append("%s (%s): links %s, floors %s, walls %s, spots %s, levels %s, window %s min, peak private %s MiB, shared with %s" % (
            code, entry["job"], links and links["instances"], floors and floors["byKind"], walls and walls["count"],
            spots and spots["count"], levels and levels["count"], window["minutes"], window["peakPrivateMib"], entry.get("sharedWith")))
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="R322. Count what each answer key read holds and how its window went.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder, one folder per read job with its testhost files")
    parser.add_argument("--key", action="append", type=parse_pair, required=True, metavar="CODE=JOB")
    args = parser.parse_args(argv)
    refuse_repeats(parser, args.key)
    plot_relations.refuse_inside_repo(Path(args.reads))
    settings = project_profile.load_settings(args.settings)
    path = Path(args.reads) / OUTPUT
    earlier_keys(path)
    entries, documents = {}, {}
    for code, job in args.key:
        entries[code], documents[code] = key_entry(settings, args.reads, code, job)
    keys = merged(path, entries)
    missing = with_shares(keys, documents, args.reads)
    report = {"note": "R322. Counts per answer key read, no coordinate or level. Floors by kind follow floors.types and floors.keyTypes (R321). "
                      "A run writes the keys it is given and keeps the other keys of the file. sharedWith gives per other key the rows of each "
                      "section both reads hold with the same element id and the same whole row, count them once in a total over keys.",
              "sharedNotChecked": missing, "keys": keys}
    write_json(path, report)
    print(render(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
