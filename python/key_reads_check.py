"""R323. The second method of the key reads of Round ADDIN Phase 2, json and math only.

Three routes other than the reader sections the counts of R322 come from:
- the categories of R311 in the same read, the elements per category counted by Revit apart from the sections, for the links,
  walls, floors (by the Category parameter of each floor) and spot elevations
- the bytes of the key copy, the Revit links its TransmissionData names by rvt_info.py (R203), against the link files of the read
- a second read of the same key copy, the Phase 1 read of another window and an older reader, by element id: 20 links at a seed with
  their file, origin, rotation, mirror, plot number, prototype and CASE, and the floors and walls of each type as id sets
The value of each spot elevation (R320) is held against its origin and the survey point offset of R51, a second source of the same
elevation. Written to key_reads_check.json in the reads folder, counts, ids and gaps only.
"""
import argparse
import json
import random
import sys
from collections import Counter
from pathlib import Path

import key_reads
import plan_from_past
import plot_relations
import project_profile
import rvt_info
from common import write_json

OUTPUT = "key_reads_check.json"
SEEDS = (14, 7)
SAMPLE = 20
# Revit names of the categories R311 counts the sections by, and of the spot elevation base that is the survey point
CATEGORY_OF = {"links": "RVT Links", "walls": "Walls", "floors": "Floors", "spots": "Spot Elevations"}
SECTION_OF = {"links": "links", "walls": "walls", "floors": "floors", "spots": "spotElevations"}
CATEGORY_PARAMETER = "Category"
SURVEY_BASE = "Survey Point"
# Origins are written to the mm by R2, so a gap up to this, in metres, is the rounding of two values
ORIGIN_TOLERANCE = 0.0005
VALUE_TOLERANCE = 0.0010001
LINK_FIELDS = ("file", "plotNumber", "prototype", "case")


def category_counts(document):
    counts = Counter()
    for item in ((document.get("site") or {}).get("categories") or []):
        counts[item.get("name")] += item.get("count") or 0
    return counts


def by_categories(document):
    """R323. Each section against the elements Revit counts in its category. A floor whose Category parameter names another
    category, as the structural slabs Revit files under Structural Foundations, is counted apart beside that category."""
    found = category_counts(document)
    result = {}
    floors = document.get("floors") or []
    of_floor = Counter((floor.get("parameters") or {}).get(CATEGORY_PARAMETER) for floor in floors)
    for key, category in CATEGORY_OF.items():
        read = of_floor[category] if key == "floors" else len(document.get(SECTION_OF[key]) or [])
        result[key] = {"read": read, "category": found[category], "equal": read == found[category]}
    result["floorsInOtherCategories"] = {str(name): {"read": count, "category": found[name]} for name, count in sorted(of_floor.items(), key=lambda item: str(item[0]))
                                        if name != CATEGORY_OF["floors"]}
    return result


def by_bytes(document, references):
    """R323. The link files of the read against the Revit links the TransmissionData of the copy names, by file name in any case."""
    read = {str(link.get("file")).lower(): str(link.get("file")) for link in document.get("links") or []}
    stored = {str(item.get("file")).lower(): str(item.get("file")) for item in references}
    return {"read": len(read), "bytes": len(stored), "both": len(set(read) & set(stored)),
            "onlyRead": sorted(read[name] for name in set(read) - set(stored)), "onlyBytes": sorted(stored[name] for name in set(stored) - set(read))}


def pick(ids, seed, size):
    """The same ids for the same seed and list, at most size of them."""
    ordered = sorted(ids)
    return sorted(random.Random(seed).sample(ordered, min(size, len(ordered))))


def link_gaps(new, old):
    """The fields of one link that differ between two reads, an origin within the rounding of R2 being equal."""
    gaps = [field for field in LINK_FIELDS if new.get(field) != old.get(field)]
    one, two = new.get("transform") or {}, old.get("transform") or {}
    if one.get("mirrored") != two.get("mirrored"):
        gaps.append("mirrored")
    if one.get("rotationDeg") is None or two.get("rotationDeg") is None or abs(one["rotationDeg"] - two["rotationDeg"]) > ORIGIN_TOLERANCE:
        gaps.append("rotationDeg")
    first, second = one.get("origin"), two.get("origin")
    if not first or not second or max(abs(a - b) for a, b in zip(first, second)) > ORIGIN_TOLERANCE:
        gaps.append("origin")
    return gaps


def links_by_id(new, old, seed, size=SAMPLE):
    """R323. The link ids of both reads, and size links picked at the seed among the ids both hold, held field by field."""
    one = {link.get("elementId"): link for link in new.get("links") or []}
    two = {link.get("elementId"): link for link in old.get("links") or []}
    ids = pick(set(one) & set(two), seed, size)
    mismatches = []
    for element in ids:
        gaps = link_gaps(one[element], two[element])
        if gaps:
            mismatches.append({"elementId": element, "fields": gaps})
    return {"seed": seed, "ids": ids, "idsEqual": set(one) == set(two), "onlyNew": sorted(set(one) - set(two)), "onlyOld": sorted(set(two) - set(one)),
            "match": len(ids) - len(mismatches), "mismatch": len(mismatches), "mismatches": mismatches}


def id_sets(rows):
    sets = {}
    for row in rows or []:
        sets.setdefault(str(row.get("typeName")), set()).add(row.get("elementId"))
    return sets


def ids_per_type(new, old):
    """R323. Per floor type and per wall type, whether both reads hold the same element ids."""
    result = {}
    for section in ("floors", "walls"):
        one, two = id_sets(new.get(section)), id_sets(old.get(section))
        types = sorted(set(one) | set(two))
        differ = [name for name in types if one.get(name) != two.get(name)]
        result[section] = {"types": len(types), "equal": len(types) - len(differ), "differ": differ}
    return result


def spot_values(document, seed, size=SAMPLE):
    """R323, R320. The value of every spot whose base is the survey point against its origin z plus the survey point offset of R51,
    in mm, and size spots at the seed among them. A spot of another base is counted by that base and not compared."""
    survey = document.get("surveyPoint") or {}
    offset = survey["sharedPosition"][2] - survey["position"][2]
    spots = [spot for spot in document.get("spotElevations") or [] if spot.get("value") is not None]
    other = Counter(str(spot.get("elevationBase")) for spot in spots if spot.get("elevationBase") != SURVEY_BASE)
    gaps = {spot.get("elementId"): abs(spot["value"] - (spot["origin"][2] + offset)) for spot in spots if spot.get("elevationBase") == SURVEY_BASE}
    ids = pick(gaps, seed, size)
    off = [element for element in ids if gaps[element] > VALUE_TOLERANCE]
    return {"withValue": len(spots), "otherBase": dict(sorted(other.items())), "compared": len(gaps),
            "withinMm": sum(1 for gap in gaps.values() if gap <= VALUE_TOLERANCE), "worstMm": round(max(gaps.values(), default=0.0) * 1000, 1),
            "sample": {"seed": seed, "ids": ids, "match": len(ids) - len(off), "mismatch": len(off), "mismatches": off}}


def check_key(document, old_document, references):
    result = {"categories": by_categories(document), "bytes": by_bytes(document, references), "spotValues": {}, "links": {}}
    if old_document is not None:
        result["idsPerType"] = ids_per_type(document, old_document)
    for seed in SEEDS:
        result["spotValues"][str(seed)] = spot_values(document, seed)
        if old_document is not None:
            result["links"][str(seed)] = links_by_id(document, old_document, seed)
    return result


def parse_triple(text):
    parts = text.split("=")
    if len(parts) != 3 or not all(parts):
        raise argparse.ArgumentTypeError("give CODE=JOB=PHASE1_READ, as IMC=RC=C:\\...\\testhost_KC_prototypes.json")
    return parts


def main(argv=None):
    parser = argparse.ArgumentParser(description="R323. Check the key reads a second way: categories, the bytes of the copy, a second read by id.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=parse_triple, required=True, metavar="CODE=JOB=PHASE1_READ")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    plot_relations.refuse_inside_repo(Path(args.reads))
    settings = project_profile.load_settings(args.settings)
    block = settings.values["answerKeys"]
    folder = Path(settings.values["inputRoot"]) / block["folder"]
    copies = {key["code"]: key["copy"] for key in block["keys"]}
    report = {"note": "R323. The key reads held against the categories of R311, the bytes of each key copy and a second read by id, seeds " + ", ".join(str(seed) for seed in SEEDS), "keys": {}}
    for code, job, old in args.key:
        if code not in copies:
            raise project_profile.ProfileError("answerKeys has no key " + code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"))
        old_document = key_reads.project_document(key_reads.load(old)) if old != "none" else None
        if old_document is not None:
            plan_from_past.check_key_file(settings, code, old_document.get("file"))
        references = rvt_info.read(folder / copies[code])["revitLinks"]
        report["keys"][code] = dict(check_key(document, old_document, references), job=job, secondRead=Path(old).name)
    write_json(Path(args.reads) / OUTPUT, report)
    for code, entry in report["keys"].items():
        categories = entry["categories"]
        links = entry["links"]
        print("%s: categories equal %s, bytes both %d of read %d and bytes %d, links by id %s, spot values within 1 mm %d of %d" % (
            code, {key: categories[key]["equal"] for key in CATEGORY_OF}, entry["bytes"]["both"], entry["bytes"]["read"], entry["bytes"]["bytes"],
            {seed: (item["match"], item["mismatch"]) for seed, item in links.items()}, entry["spotValues"]["14"]["withinMm"], entry["spotValues"]["14"]["compared"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
