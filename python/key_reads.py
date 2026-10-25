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
    if links is None:
        return None
    return {"instances": len(links), "withPlotNumber": sum(1 for link in links if link.get("plotNumber") is not None),
            "files": len({link.get("file") for link in links}), "loaded": sum(1 for link in links if link.get("loaded") is True)}


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


def key_entry(settings, reads, code, job):
    folder = Path(reads) / job
    prefix = "testhost_" + job + "_"
    floors = plan_from_past.load_floors(settings, code)
    document = project_document(load(folder / (prefix + "prototypes.json")))
    return {"job": job, "file": (folder / (prefix + "prototypes.json")).name, "fileMb": round((folder / (prefix + "prototypes.json")).stat().st_size / 1048576.0, 2),
            "errors": len(document.get("errors") or []), "counts": counts(document, floors),
            "window": window_facts(load(folder / (prefix + "run.json")), load(folder / (prefix + "log.json")))}


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
    plot_relations.refuse_inside_repo(Path(args.reads))
    settings = project_profile.load_settings(args.settings)
    report = {"note": "R322. Counts per answer key read, no coordinate or level. Floors by kind follow floors.types and floors.keyTypes (R321).",
              "keys": {code: key_entry(settings, args.reads, code, job) for code, job in args.key}}
    write_json(Path(args.reads) / OUTPUT, report)
    print(render(report))
    return 0


if __name__ == "__main__":
    sys.exit(main())
