"""Compares plan.json with the links of a model the placer built, read back by the Revit reader into prototypes.json (R89),
or with another plan field by field with its floors per kind (R129, R138).

It counts matches and mismatches per field and lists the plots that differ. It changes nothing and sets no rule.
"""
import argparse
import math
import sys
from collections import Counter
from pathlib import Path

import common
import floor_fit
import link_summary
import plan_from_past
import plot_relations
import prototype_offsets
import ramp_levels

POSITION_TOLERANCE = 0.001
ANGLE_TOLERANCE = 0.01
REPORT_NAME = "plan_compare.json"
FIELDS = ("link", "file", "position", "rotation", "mirrored", "linkName", "parameters")


def angle_gap(a, b):
    gap = abs(a - b) % 360.0
    return min(gap, 360.0 - gap)


def compare_plot(entry, links, offset):
    """The fields of one plot that differ, each with a short reason. An empty dict is a full match."""
    if len(links) != 1:
        return {"link": str(len(links)) + " links hold the plot number"}
    link = links[0]
    found = {}
    if (link.get("file") or "").lower() != entry["prototypeFile"]["value"].lower():
        found["file"] = "file differs"
    origin = plan_from_past.shared_origin(link, offset)
    if origin is None:
        found["position"] = "no transform"
    else:
        gap = max(abs(origin[axis] - entry["insert"]["value"][axis]) for axis in range(3))
        if gap > POSITION_TOLERANCE:
            found["position"] = "%s mm off" % common.r3(gap * 1000.0)
    transform = link.get("transform") or {}
    if transform.get("rotationDeg") is None or angle_gap(transform["rotationDeg"], entry["rotationDeg"]["value"]) > ANGLE_TOLERANCE:
        found["rotation"] = "rotation differs"
    if transform.get("mirrored") != entry["mirrored"]["value"]:
        found["mirrored"] = "mirror flag differs"
    parameters = link.get("parameters") or {}
    if "linkName" in entry and parameters.get(plan_from_past.NAME_PARAMETER) != entry["linkName"]["value"]:
        found["linkName"] = "link name differs"
    wrong = sorted(name for name, value in (entry.get("parameters") or {}).items() if parameters.get(name) != value["value"])
    if wrong:
        found["parameters"] = "differ: " + ", ".join(wrong)
    return found


def compare(plan, data):
    document = plan_from_past.host_of(data)
    offset = ramp_levels.offset_of(document)
    links, bad = {}, []
    for link in document.get("links") or []:
        plot = (link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER)
        if plot is not None and str(plot).strip():
            key = plan_from_past.plot_number(plot)
            if key is None:
                bad.append({"elementId": link.get("elementId"), "plotNumber": str(plot)})
            else:
                links.setdefault(key, []).append(link)
    matches, mismatches, differing = Counter(), Counter(), []
    planned = set()
    for entry in plan["plots"]:
        planned.add(entry["plot"])
        found = compare_plot(entry, links.get(entry["plot"], []), offset)
        for field in FIELDS:
            if field in found:
                mismatches[field] += 1
            elif "link" not in found:
                matches[field] += 1
        if found:
            differing.append({"plot": entry["plot"], "differs": found})
    return {
        "plots": len(plan["plots"]),
        "fullMatch": len(plan["plots"]) - len(differing),
        "matches": {field: matches[field] for field in FIELDS},
        "mismatches": {field: mismatches[field] for field in FIELDS},
        "linksNotInPlan": sorted((plot for plot in links if plot not in planned), key=link_summary.sort_key),
        "linksWithBadPlotNumber": bad,
        "differing": differing,
    }


# R129. The bands of a length, in metres, and the plan fields compared with them
LENGTH_BANDS = (("equal", 0.0000005), ("within1mm", 0.001), ("within10mm", 0.010))
PLAN_REPORT_NAME = "plan_against_plan.json"
LENGTH_FIELDS = (("insertPlan", lambda entry: entry["insert"]["value"][:2]), ("insertZ", lambda entry: entry["insert"]["value"][2]),
                 ("ffl", lambda entry: entry["levels"]["ffl"]["value"]), ("ssl", lambda entry: entry["levels"]["ssl"]["value"]),
                 ("entry", lambda entry: entry["levels"]["entry"]["value"]), ("height", lambda entry: entry["levels"]["height"]["value"]))
SAME_FIELDS = (("unit", lambda entry: entry["unit"]["value"]), ("prototypeFile", lambda entry: entry["prototypeFile"]["value"].lower()),
               ("mirrored", lambda entry: entry["mirrored"]["value"]), ("steps", lambda entry: entry["levels"]["steps"]["value"]))


def length_band(gap):
    for label, limit in LENGTH_BANDS:
        if gap <= limit:
            return label
    return "worse"


def value_of(entry, read):
    try:
        return read(entry)
    except (KeyError, TypeError, IndexError):
        return None


def plan_gaps(entry, other):
    """R129. The band of every field of one plot in two plans, None where either plan lacks it."""
    found = {}
    for name, read in LENGTH_FIELDS:
        mine, theirs = value_of(entry, read), value_of(other, read)
        if mine is None or theirs is None:
            found[name] = None
        else:
            gap = math.dist(mine, theirs) if isinstance(mine, list) else abs(mine - theirs)
            found[name] = (length_band(gap), gap)
    rotation = value_of(entry, lambda item: item["rotationDeg"]["value"]), value_of(other, lambda item: item["rotationDeg"]["value"])
    found["rotation"] = None if None in rotation else (("equal" if angle_gap(*rotation) <= ANGLE_TOLERANCE else "worse"), angle_gap(*rotation))
    for name, read in SAME_FIELDS:
        mine, theirs = value_of(entry, read), value_of(other, read)
        found[name] = None if mine is None or theirs is None else (("equal" if mine == theirs else "worse"), None)
    return found


# R138. The bands of a floor outline gap and of a level, in metres
OUTLINE_BANDS = (("within1mm", 0.001), ("within10mm", 0.010))
TOP_BANDS = (("within5mm", 0.005),)
CORNER_BANDS = (("within5mm", 0.005), ("within30mm", 0.030))


def band(gap, bands):
    for label, limit in bands:
        if gap <= limit + 1e-9:
            return label
    return "worse"


def floor_shape(floor):
    return floor_fit.shape_of([loop["points"] for loop in floor["outline"]["value"]["loops"]])


def corner_gap(mine, theirs):
    """The largest level gap of the corners, each paired with the nearest corner of the other floor in plan."""
    return max(abs(corner[2] - min(theirs, key=lambda other: math.dist(corner[:2], other[:2]))[2]) for corner in mine)


def floor_gaps(entry, other):
    """R138. Per floor kind whether both plans hold it, and the band of its type, outline, top and ramp corners."""
    found = {}
    for kind in plan_from_past.FLOOR_KINDS:
        mine, theirs = (entry.get("floors") or {}).get(kind), (other.get("floors") or {}).get(kind)
        if mine is None or theirs is None:
            found[kind] = {"presence": "both missing" if mine is None and theirs is None else "only in plan" if theirs is None else "only in other"}
            continue
        result = {"presence": "both", "type": "equal" if mine["type"]["value"] == theirs["type"]["value"] else "worse",
                  "outline": band(floor_fit.outline_gap(floor_shape(mine), floor_shape(theirs)), OUTLINE_BANDS)}
        if "z" in mine and "z" in theirs:
            result["top"] = band(abs(mine["z"]["value"] - theirs["z"]["value"]), TOP_BANDS)
        if "corners" in mine and "corners" in theirs:
            result["corners"] = band(corner_gap(mine["corners"]["value"], theirs["corners"]["value"]), CORNER_BANDS)
        found[kind] = result
    return found


def compare_plans(plan, other, apart):
    """R129. plan against other field by field, per field the plots equal, within 1 mm, within 10 mm and worse, a rotation
    equal within 0.01 degrees and the other fields equal or not. The plots of apart are counted on their own, the plots of
    one plan only are listed."""
    theirs = {entry["plot"]: entry for entry in other["plots"]}
    counts = {"outside": {}, "apart": {}}
    floors = {"outside": {}, "apart": {}}
    worse, missing_in_other = [], []
    for entry in plan["plots"]:
        match = theirs.get(entry["plot"])
        if match is None:
            missing_in_other.append(entry["plot"])
            continue
        group = "apart" if entry["plot"] in apart else "outside"
        for name, result in plan_gaps(entry, match).items():
            table = counts[group].setdefault(name, Counter())
            table["missing" if result is None else result[0]] += 1
            if result is not None and result[0] == "worse" and group == "outside":
                worse.append({"plot": entry["plot"], "field": name, "gap": result[1]})
        for kind, result in floor_gaps(entry, match).items():
            for field, value in result.items():
                floors[group].setdefault(kind, {}).setdefault(field, Counter())[value] += 1
    mine = {entry["plot"] for entry in plan["plots"]}
    return {
        "plots": len(plan["plots"]), "otherPlots": len(other["plots"]), "compared": len(plan["plots"]) - len(missing_in_other),
        "apart": sorted(apart, key=link_summary.sort_key),
        "counts": {group: {name: dict(table) for name, table in sorted(fields.items())} for group, fields in counts.items()},
        "floors": {group: {kind: {field: dict(table) for field, table in sorted(fields.items())} for kind, fields in sorted(kinds.items())} for group, kinds in floors.items()},
        "onlyInPlan": missing_in_other, "onlyInOther": sorted((plot for plot in theirs if plot not in mine), key=link_summary.sort_key),
        "worse": worse,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare plan.json with the links of a model read back into prototypes.json, or with another plan.")
    parser.add_argument("plan", help="plan.json the placer read")
    parser.add_argument("prototypes", nargs="?", help="prototypes.json of the placed model. The report is written next to it")
    parser.add_argument("--against-plan", help="another plan.json, the plan is compared with it field by field (R129) and the report written next to the plan")
    args = parser.parse_args(argv)
    if args.against_plan:
        out = Path(args.plan).resolve().parent / PLAN_REPORT_NAME
        plot_relations.refuse_inside_repo(out.parent)
        apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
        report = compare_plans(link_summary.load(args.plan), link_summary.load(args.against_plan), apart)
        common.write_json(out, report)
        print("plots %d, compared %d, only in the other plan %d" % (report["plots"], report["compared"], len(report["onlyInOther"])))
        for name, table in report["counts"]["outside"].items():
            print("%s %s" % (name, table))
        for kind, fields in report["floors"]["outside"].items():
            print("floor %s %s" % (kind, fields))
        print(out)
        return 0
    if not args.prototypes:
        parser.error("give prototypes.json or --against-plan")
    out = Path(args.prototypes).resolve().parent / REPORT_NAME
    plot_relations.refuse_inside_repo(out.parent)
    report = compare(link_summary.load(args.plan), link_summary.load(args.prototypes))
    common.write_json(out, report)
    print("plots %d, full match %d" % (report["plots"], report["fullMatch"]))
    print("mismatches %s" % report["mismatches"])
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
