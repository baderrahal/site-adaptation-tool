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
from common import write_json

OUTPUT = "key_reads.json"
NO_KIND = "none"
IN_LINK = "in a link"
NO_HOST = "no host"
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


def host_kind(spot):
    """The category and type of the host of a spot, in a link when its reference points into one, no host when none was found."""
    host = spot.get("host")
    if not host:
        return NO_HOST
    if host.get("linkedElementId") is not None:
        return IN_LINK
    return str(host.get("category")) + ": " + str(host.get("typeName"))


def spot_counts(spots):
    if spots is None:
        return None
    return {"count": len(spots), "withValue": sum(1 for spot in spots if spot.get("value") is not None),
            "byHost": by_name(host_kind(spot) for spot in spots)}


def level_counts(site):
    levels = (site or {}).get("levels")
    if levels is None:
        return None
    return {"count": len(levels), "names": [level.get("name") for level in levels]}


def counts(document, settings_floors):
    """R322. The counts of one project document of the reader, read by the floors block of its key."""
    return {"links": link_counts(document.get("links")), "floors": floor_counts(document.get("floors"), settings_floors),
            "walls": wall_counts(document.get("walls")), "spots": spot_counts(document.get("spotElevations")),
            "levels": level_counts(document.get("site"))}


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
    folder = Path(reads) / job
    prefix = "testhost_" + job + "_"
    floors = plan_from_past.load_floors(settings, code)
    document = project_document(load(folder / (prefix + "prototypes.json")))
    plan_from_past.check_key_file(settings, code, document.get("file"))
    return {"job": job, "file": (folder / (prefix + "prototypes.json")).name, "fileMib": round((folder / (prefix + "prototypes.json")).stat().st_size / 1048576.0, 2),
            "errors": len(document.get("errors") or []), "counts": counts(document, floors),
            "window": window_facts(load(folder / (prefix + "run.json")), load(folder / (prefix + "log.json")))}


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
        lines.append("%s (%s): links %s, floors %s, walls %s, spots %s, levels %s, window %s min, peak private %s MiB" % (
            code, entry["job"], links and links["instances"], floors and floors["byKind"], walls and walls["count"],
            spots and spots["count"], levels and levels["count"], window["minutes"], window["peakPrivateMib"]))
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
    entries = {code: key_entry(settings, args.reads, code, job) for code, job in args.key}
    report = {"note": "R322. Counts per answer key read, no coordinate or level. Floors by kind follow floors.types and floors.keyTypes (R321). "
                      "A run writes the keys it is given and keeps the other keys of the file.",
              "keys": merged(path, entries)}
    write_json(path, report)
    print(render(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
