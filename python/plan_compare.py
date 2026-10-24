"""Compares plan.json with the links of a model the placer built, read back by the Revit reader into prototypes.json (R89).

It counts matches and mismatches per field and lists the plots that differ. It changes nothing and sets no rule.
"""
import argparse
import sys
from collections import Counter
from pathlib import Path

import common
import link_summary
import plan_from_past
import plot_relations
import ramp_levels

POSITION_TOLERANCE = 0.001
ANGLE_TOLERANCE = 0.01
REPORT_NAME = "plan_compare.json"
FIELDS = ("link", "file", "position", "rotation", "mirrored", "parameters")


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


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare plan.json with the links of a model read back into prototypes.json.")
    parser.add_argument("plan", help="plan.json the placer read")
    parser.add_argument("prototypes", help="prototypes.json of the placed model. The report is written next to it")
    args = parser.parse_args(argv)
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
