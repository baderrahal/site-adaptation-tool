"""What the IFC in Downloads holds, Part I of round DATA-5 (R258).

The IFC is read only from its copy under dataBook.root/work, as STEP text with no IFC library: the header records
(FILE_DESCRIPTION, FILE_NAME, FILE_SCHEMA), the names of IfcProject and IfcSite, the projected CRS and map conversion if
any, a count of every entity type, the names and property names of the products, and where its points lie. A point is
taken from every IfcCartesianPoint with three values, and the share of them inside the Phase 1 border says whether the
site lies in Phase 1. A product or property whose name matches a level word of dataBook.data5.ifc.levelWords is listed, so
plot entrance spots or road levels would show. Nothing is wired from it. Counts go to ifc.json in the ifc folder of data5,
no coordinate is written.
"""
import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

import numpy as np
import shapely

import c3d_run
import data_book
import data_book_analysis

# The STEP record end, kept out of the source text by the writing rules of the repo
END = chr(59)
RECORD = re.compile(r"#(\d+)\s*=\s*([A-Z0-9_]+)\s*\((.*)\)\s*$", re.S)
TEXT = re.compile(r"'((?:[^']|'')*)'")


def records(text):
    """The DATA records of a STEP file as (id, type, arguments), a record ending at the semicolon outside a string."""
    body = text.split("DATA" + END, 1)[1] if "DATA" + END in text else ""
    found, current, quoted = [], [], False
    for char in body:
        if char == "'":
            quoted = not quoted
        if char == END and not quoted:
            match = RECORD.match("".join(current).strip())
            if match:
                found.append((int(match.group(1)), match.group(2), match.group(3)))
            current = []
            continue
        current.append(char)
    return found


def strings(arguments):
    return [s.replace("''", "'") for s in TEXT.findall(arguments)]


def header(text):
    """The three header records, each as its strings in order."""
    head = text.split("ENDSEC" + END, 1)[0]
    found = {}
    for name in ("FILE_DESCRIPTION", "FILE_NAME", "FILE_SCHEMA"):
        match = re.search(name + r"\s*\((.*?)\)" + END, head, re.S)
        found[name] = strings(match.group(1)) if match else None
    names = found.get("FILE_NAME") or []
    return {"description": found["FILE_DESCRIPTION"], "fileName": names, "schema": found["FILE_SCHEMA"],
            "timestamp": names[0] if names and re.match(r"\d{4}-", names[0]) else (names[1] if len(names) > 1 else None)}


def split_arguments(arguments):
    """The top level arguments of a record, commas inside a string or a bracket kept."""
    found, current, depth, quoted = [], [], 0, False
    for char in arguments:
        if char == "'":
            quoted = not quoted
        elif not quoted and char == "(":
            depth += 1
        elif not quoted and char == ")":
            depth -= 1
        if char == "," and depth == 0 and not quoted:
            found.append("".join(current).strip())
            current = []
            continue
        current.append(char)
    found.append("".join(current).strip())
    return found


def value(argument):
    """A string argument without its quotes, None for $ or an unset string."""
    if argument.startswith("'") and argument.endswith("'"):
        return argument[1:-1].replace("''", "'") or None
    return None


def named(rows, kind, index=2):
    """The Name of each record of one type, the third argument of a rooted entity, None when it is not set."""
    return [value(split_arguments(arguments)[index]) if len(split_arguments(arguments)) > index else None
            for _, typ, arguments in rows if typ == kind]


def points(rows):
    found = []
    for _, typ, arguments in rows:
        if typ == "IFCCARTESIANPOINT":
            values = [float(v) for v in re.findall(r"-?\d+\.?\d*(?:E[-+]?\d+)?", arguments)]
            if len(values) == 3:
                found.append(values)
    return np.asarray(found, dtype=float).reshape(-1, 3)


def level_names(rows, words):
    """Product names and property names that hold a level word, by type and name."""
    rule = re.compile("|".join(words), re.IGNORECASE)
    found = Counter()
    for _, typ, arguments in rows:
        if typ in ("IFCPROPERTYSINGLEVALUE", "IFCPROPERTYBOUNDEDVALUE", "IFCPROPERTYSET", "IFCBUILDINGELEMENTPROXY", "IFCALIGNMENT", "IFCSITE",
                   "IFCANNOTATION", "IFCGEOGRAPHICELEMENT", "IFCCIVILELEMENT"):
            parts = split_arguments(arguments)
            name = value(parts[0] if typ.startswith("IFCPROPERTY") else (parts[2] if len(parts) > 2 else "")) or ""
            if name and rule.search(name):
                found["%s %s" % (typ, name[:60])] += 1
    return dict(found.most_common())


def run(config):
    book = config["dataBook"]
    settings = book["data5"]["ifc"]
    root = Path(book["root"])
    copy = root / "work" / settings["copy"]
    text = copy.read_text(encoding="utf-8", errors="replace")
    rows = records(text)
    border = data_book.border_polygon(root / "phase1_border.csv")
    found = points(rows)
    inside = shapely.contains_xy(border, found[:, 0], found[:, 1]) if len(found) else np.zeros(0, dtype=bool)
    proxies = Counter(named(rows, "IFCBUILDINGELEMENTPROXY"))
    properties = Counter(strings(a)[0] for _, t, a in rows if t == "IFCPROPERTYSINGLEVALUE" and strings(a))
    labels = Counter()
    for _, t, a in rows:
        parts = split_arguments(a) if t == "IFCPROPERTYSINGLEVALUE" else []
        if len(parts) > 2 and value(parts[0]) in settings["labelProperties"]:
            labels["%s %s" % (value(parts[0]), ", ".join(strings(parts[2])))] += 1
    plots = data_book_analysis.read_csv(root / "plots.csv")
    road = [float(r[c]) for r in plots for c in ("roadLevelLeft", "roadLevelRight") if r.get(c)]
    z_overlap = bool(road) and bool(len(found)) and float(found[:, 2].max()) >= min(road) and float(found[:, 2].min()) <= max(road)
    report = {"copy": settings["copy"], "header": header(text), "records": len(rows), "types": dict(Counter(t for _, t, _ in rows).most_common()),
              "project": named(rows, "IFCPROJECT"), "site": named(rows, "IFCSITE"),
              "crs": [strings(a) for _, t, a in rows if t == "IFCPROJECTEDCRS"], "mapConversions": sum(1 for _, t, _ in rows if t == "IFCMAPCONVERSION"),
              "alignments": Counter(named(rows, "IFCALIGNMENT")).most_common(10), "proxyNames": proxies.most_common(15), "proxyNameCount": len(proxies),
              "propertyNames": properties.most_common(40), "propertyValues": dict(labels.most_common()), "levelNames": level_names(rows, settings["levelWords"]),
              "points": {"count": int(len(found)), "insidePhase1": int(inside.sum()), "zMeetsPhase1RoadLevels": z_overlap,
                         "largestXYOrder": int(np.floor(np.log10(np.abs(found[:, :2]).max()))) if len(found) and np.abs(found[:, :2]).max() > 0 else None,
                         "boxInsideBorderBox": bool(len(found)) and bool(shapely.box(*border.bounds).intersects(shapely.box(found[:, 0].min(), found[:, 1].min(), found[:, 0].max(), found[:, 1].max())))}}
    out = root / book["data5"]["outFolder"] / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "ifc.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Header, names, types and place of the IFC copy (R258)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({k: v for k, v in report.items() if k != "types"}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
