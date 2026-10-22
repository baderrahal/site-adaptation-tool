import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

from common import write_json

FFL_TOLERANCE = 0.0015
NOT_LOADED = "Link is not loaded"
NONE_KEY = "(none)"
FLAT_NUMBER = re.compile(r"^\s*([-+]?\d+(?:[.,]\d+)?)\s*m?\s*$", re.IGNORECASE)


def drop_instances(node):
    # An old file repeats the contents of every loaded link, so only the count is kept to save memory
    if isinstance(node.get("instances"), list):
        node["instanceCount"] = len(node.pop("instances"))
    return node


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle, object_hook=drop_instances)


def links_of(data):
    links = []
    for document in data.get("documents") or []:
        links.extend(document.get("links") or [])
    return links


def text_key(value):
    if value is None or str(value).strip() == "":
        return NONE_KEY
    return str(value).strip()


def number_key(value):
    if value is None:
        return NONE_KEY
    rounded = round(float(value), 3)
    return str(int(rounded)) if rounded == int(rounded) else str(rounded)


def sort_key(text):
    return (0, float(text), text) if re.fullmatch(r"-?\d+(\.\d+)?", text) else (1, 0, text)


def ordered(counter):
    return {key: counter[key] for key in sorted(counter, key=sort_key)}


def parse_number(value):
    # Only a plain number counts. Text such as 12.5 m is read, anything with other words is left unread
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    found = FLAT_NUMBER.match(str(value))
    return float(found.group(1).replace(",", ".")) if found else None


def has_field(links, field):
    return sum(1 for link in links if link.get(field) is not None)


def origin_z(link):
    origin = (link.get("transform") or {}).get("origin")
    return origin[2] if isinstance(origin, list) and len(origin) > 2 else None


def mirrored(link):
    return bool((link.get("transform") or {}).get("mirrored"))


def rotation(link):
    return (link.get("transform") or {}).get("rotationDeg")


def summarize(data):
    links = links_of(data)
    missing = []

    def need(part, field):
        if not links:
            missing.append({"part": part, "reason": "the file has no links"})
            return False
        if has_field(links, field) == 0:
            missing.append({"part": part, "reason": "no link has the field " + field + ", the file has the old shape"})
            return False
        return True

    summary = {
        "documents": len(data.get("documents") or []),
        "links": len(links),
        "notLoaded": sum(1 for link in links if NOT_LOADED in (link.get("errors") or [])),
        "perFile": ordered(Counter(text_key(link.get("file")) for link in links)),
        "mirroredTotal": sum(1 for link in links if mirrored(link)),
        "rotations": ordered(Counter(number_key(rotation(link)) for link in links)),
    }

    summary["perPrototype"] = None
    summary["mirroredByPrototype"] = None
    if need("perPrototype and mirroredByPrototype", "prototype"):
        counts = Counter(text_key(link.get("prototype")) for link in links)
        summary["perPrototype"] = ordered(counts)
        flipped = Counter(text_key(link.get("prototype")) for link in links if mirrored(link))
        summary["mirroredByPrototype"] = {key: {"links": counts[key], "mirrored": flipped[key]} for key in sorted(counts, key=sort_key)}

    summary["caseVsMirrored"] = None
    summary["caseVsRotation"] = None
    if need("caseVsMirrored and caseVsRotation", "case"):
        cases = sorted({text_key(link.get("case")) for link in links}, key=sort_key)
        summary["caseVsMirrored"] = {
            case: {
                "mirrored": sum(1 for link in links if text_key(link.get("case")) == case and mirrored(link)),
                "notMirrored": sum(1 for link in links if text_key(link.get("case")) == case and not mirrored(link)),
            }
            for case in cases
        }
        summary["caseVsRotation"] = {
            case: ordered(Counter(number_key(rotation(link)) for link in links if text_key(link.get("case")) == case))
            for case in cases
        }

    summary["fflEqualsOriginZ"] = None
    if need("fflEqualsOriginZ", "ffl"):
        match = differ = unreadable = 0
        for link in links:
            ffl = parse_number(link.get("ffl"))
            z = origin_z(link)
            if ffl is None or z is None:
                unreadable += 1
            elif abs(ffl - z) <= FFL_TOLERANCE + 1e-9:
                match += 1
            else:
                differ += 1
        summary["fflEqualsOriginZ"] = {"toleranceMm": 1.5, "unitAssumed": "m", "match": match, "differ": differ, "unreadable": unreadable}

    summary["repeatedPlots"] = None
    summary["plotNameMismatch"] = None
    if need("repeatedPlots and plotNameMismatch", "plotNumber"):
        plots = Counter(text_key(link.get("plotNumber")) for link in links if link.get("plotNumber") is not None)
        summary["repeatedPlots"] = [{"plot": plot, "links": plots[plot]} for plot in sorted(plots, key=sort_key) if plots[plot] > 1]
        summary["plotNameMismatch"] = sum(
            1
            for link in links
            if link.get("plotNumber") is not None
            and (link.get("nameParts") or {}).get("plot") is not None
            and text_key(link["plotNumber"]) != text_key(link["nameParts"]["plot"])
        )

    summary["notComputable"] = missing
    return summary


def render(summary):
    lines = ["documents " + str(summary["documents"]), "links " + str(summary["links"]), "not loaded " + str(summary["notLoaded"])]
    lines.append("links per file (" + str(len(summary["perFile"])) + " files)")
    lines.extend("  " + key + ": " + str(count) for key, count in summary["perFile"].items())
    if summary["perPrototype"] is not None:
        lines.append("links per prototype parameter")
        lines.extend("  " + key + ": " + str(count) for key, count in summary["perPrototype"].items())
        lines.append("mirrored per prototype")
        lines.extend("  " + key + ": " + str(item["mirrored"]) + " of " + str(item["links"]) for key, item in summary["mirroredByPrototype"].items())
    lines.append("mirrored links " + str(summary["mirroredTotal"]))
    lines.append("rotation values")
    lines.extend("  " + key + ": " + str(count) for key, count in summary["rotations"].items())
    if summary["caseVsMirrored"] is not None:
        lines.append("CASE against mirrored")
        lines.extend("  " + key + ": " + str(item["mirrored"]) + " mirrored, " + str(item["notMirrored"]) + " not" for key, item in summary["caseVsMirrored"].items())
        lines.append("CASE against rotation")
        for case, counts in summary["caseVsRotation"].items():
            lines.append("  " + case + ": " + ", ".join(key + " x" + str(count) for key, count in counts.items()))
    if summary["fflEqualsOriginZ"] is not None:
        item = summary["fflEqualsOriginZ"]
        lines.append("Prototype-FFL equal to link origin Z within 1.5 mm, read as metres")
        lines.append("  match " + str(item["match"]) + ", differ " + str(item["differ"]) + ", unreadable " + str(item["unreadable"]))
    if summary["repeatedPlots"] is not None:
        lines.append("plot numbers on more than one link: " + str(len(summary["repeatedPlots"])))
        lines.extend("  " + item["plot"] + ": " + str(item["links"]) + " links" for item in summary["repeatedPlots"])
        lines.append("plot parameter differs from the plot in the name: " + str(summary["plotNameMismatch"]))
    for item in summary["notComputable"]:
        lines.append("NOT COMPUTED " + item["part"] + ": " + item["reason"])
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Count the Revit links in prototypes.json by file, prototype, mirror, rotation and CASE.")
    parser.add_argument("prototypes", help="prototypes.json from the Revit reader")
    parser.add_argument("out_dir", nargs="?", default=".", help="where link_summary.json is written")
    args = parser.parse_args(argv)
    summary = summarize(load(args.prototypes))
    write_json(Path(args.out_dir) / "link_summary.json", summary)
    print(render(summary))
    return 0


if __name__ == "__main__":
    sys.exit(main())
