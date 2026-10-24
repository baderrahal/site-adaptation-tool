"""R217. The second method of the round compare, with json and math only and none of the compare code.

For seeds 14 and 7 it draws up to 20 plots of cover.json and works out, on its own:
- the CAD plan against the past plan: the FFL gap, with the key datum from the raw survey point of the key model, and the
  Generic floor outline gap as the largest distance from a vertex of one outline to the nearest vertex of the other
- the placed links against the past links of the raw reader outputs: the plan distance of the link origins in shared
  coordinates and the turn of the link X axes, each link found by its plot number parameter or by its instance Name
Each value is put in the bands of siteBuild.bandsMm and compared with the band z4_compare wrote for that plot and field.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import project_profile

FIELDS = ("zAgainstFfl", "generic outline")
# A band edge is held to a millionth of a mm, the float noise of shared coordinates near 2.4e6 m
EDGE_MM = 1e-6


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def project(data):
    return next(document for document in data["documents"] if document.get("kind") == "project")


def survey_offset(document):
    survey = document["surveyPoint"]
    return [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(3)]


def bands_of(gap_m, bands_mm):
    if gap_m <= 1e-6:
        return "equal"
    for limit in bands_mm[1:]:
        if gap_m * 1000.0 <= limit + EDGE_MM:
            return "within%gmm" % limit
    return "worse"


def vertex_gap(first, second):
    """The largest distance from a vertex of either outline to the nearest vertex of the other, in metres."""
    a = [p for loop in first for p in loop["points"]]
    b = [p for loop in second for p in loop["points"]]
    one = max(min(math.hypot(p[0] - q[0], p[1] - q[1]) for q in b) for p in a)
    two = max(min(math.hypot(p[0] - q[0], p[1] - q[1]) for q in a) for p in b)
    return max(one, two)


def plan_checks(plots, cad, key, datum, report, bands):
    """The FFL and Generic outline bands of the plots by this method against the bands of the compare report."""
    rows = []
    by_plot = {row["plot"]: row for row in report["byPlot"]}
    for plot in plots:
        mine, theirs, done = cad.get(plot), key.get(plot), by_plot.get(plot)
        if mine is None or theirs is None or done is None:
            continue
        ffl = abs(mine["levels"]["ffl"]["value"] - (theirs["levels"]["ffl"]["value"] - datum))
        outline = vertex_gap(mine["floors"]["generic"]["outline"]["value"]["loops"], theirs["floors"]["generic"]["outline"]["value"]["loops"])
        for field, gap in (("zAgainstFfl", ffl), ("generic outline", outline)):
            other = done["gapsMm"].get(field)
            rows.append({"plot": plot, "field": field, "mineMm": round(gap * 1000.0, 3), "compareMm": other,
                         "band": bands_of(gap, bands), "compareBand": done["bands"].get(field), "agree": bands_of(gap, bands) == done["bands"].get(field)})
    return rows


def link_index(document, by_name):
    """The raw links of a reader output by plot number parameter, or by instance Name when by_name is given."""
    found = {}
    for link in document.get("links") or []:
        parameters = link.get("parameters") or {}
        key = parameters.get("Name") if by_name else parameters.get("RRE_ZZ_SP_Plot-Number")
        if key:
            found.setdefault(str(key), []).append(link)
    return found


def link_checks(plots, new_doc, key_doc, key_plan, report, bands):
    """The link origin distance and X axis turn of the placed and the past links, from the raw reader outputs."""
    rows = []
    by_plot = {row["plot"]: row for row in report["byPlot"]}
    new_links, key_links = link_index(new_doc, False), link_index(key_doc, True)
    new_offset, key_offset = survey_offset(new_doc), survey_offset(key_doc)
    for plot in plots:
        done, entry = by_plot.get(plot), key_plan.get(plot)
        name = (entry or {}).get("linkName", {}).get("value")
        mine, theirs = new_links.get(plot) or [], key_links.get(name) or []
        if done is None or len(mine) != 1 or len(theirs) != 1:
            continue
        a, b = mine[0]["transform"], theirs[0]["transform"]
        pa = [a["origin"][axis] + new_offset[axis] for axis in range(2)]
        pb = [b["origin"][axis] + key_offset[axis] for axis in range(2)]
        distance = math.hypot(pa[0] - pb[0], pa[1] - pb[1])
        turn = abs(math.degrees(math.atan2(a["axisX"][1], a["axisX"][0]) - math.atan2(b["axisX"][1], b["axisX"][0]))) % 360.0
        turn = min(turn, 360.0 - turn)
        other = done["gapsMm"].get("position")
        rows.append({"plot": plot, "positionMm": round(distance * 1000.0, 3), "compareMm": other, "turnDeg": round(turn, 3), "compareTurnDeg": done["rotationGapDeg"],
                     "band": bands_of(distance, bands), "compareBand": done["bands"].get("position"),
                     "agree": bands_of(distance, bands) == done["bands"].get("position") and abs(turn - done["rotationGapDeg"]) <= 0.01})
    return rows


def main(argv=None):
    parser = argparse.ArgumentParser(description="The second method of the round compare (R217).")
    parser.add_argument("cover")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--cad", required=True, help="plan_z4_cad.json")
    parser.add_argument("--key", required=True, help="plan_z4_past.json")
    parser.add_argument("--key-model", required=True, help="prototypes_z4_past.json")
    parser.add_argument("--new-model", required=True, help="the reader output of the new file")
    parser.add_argument("--plan-report", required=True, help="the z4_compare report of the CAD plan")
    parser.add_argument("--model-report", required=True, help="the z4_compare report of the new file")
    parser.add_argument("--seeds", default="14,7")
    parser.add_argument("--size", type=int, default=20)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    bands = project_profile.load_settings(args.settings).get("siteBuild")["bandsMm"]
    plots = load(args.cover)["plots"]
    cad = {entry["plot"]: entry for entry in load(args.cad)["plots"]}
    key = {entry["plot"]: entry for entry in load(args.key)["plots"]}
    key_doc, new_doc = project(load(args.key_model)), project(load(args.new_model))
    datum = survey_offset(key_doc)[2]
    plan_report, model_report = load(args.plan_report), load(args.model_report)
    result = {"keyDatumM": round(datum, 3), "seeds": {}}
    for seed in [int(text) for text in args.seeds.split(",")]:
        drawn = sorted(random.Random(seed).sample(plots, min(args.size, len(plots))), key=int)
        planned = plan_checks(drawn, cad, key, datum, plan_report, bands)
        linked = link_checks(drawn, new_doc, key_doc, key, model_report, bands)
        result["seeds"][str(seed)] = {"plots": drawn, "plan": {"agree": sum(row["agree"] for row in planned), "of": len(planned), "rows": planned},
                                      "links": {"agree": sum(row["agree"] for row in linked), "of": len(linked), "rows": linked}}
        print("seed %d: %d plots drawn, CAD plan against past plan %d of %d agree, placed links against past links %d of %d agree" % (
            seed, len(drawn), result["seeds"][str(seed)]["plan"]["agree"], len(planned), result["seeds"][str(seed)]["links"]["agree"], len(linked)))
    out = Path(args.out)
    if project_profile.ROOT in out.resolve().parents:
        print("z4_check: the report goes outside the repo", file=sys.stderr)
        return 2
    out.write_text(json.dumps(result, indent=1), encoding="utf-8")
    print("key datum %s m" % result["keyDatumM"])
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
