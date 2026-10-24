"""Checks one test host run of one prototype against the answer key, the reader output of the past model (R105).

It joins the links of prototypes_original_imc.json and testhost_<code>_prototypes.json by the official plot number,
for the plots the plan gives that prototype file, and counts what matches. It changes nothing and fixes nothing.
"""
import argparse
import math
import random
import sys
from collections import Counter
from pathlib import Path

import common
import link_summary
import plan_from_past
import plot_relations
import project_profile
import ramp_levels

POSITION_MM = 1.0
LOOSE_MM = 10.0
ANGLE_TOLERANCE = 0.01
Z_MM = 1.0
WORST = 5
SECOND_METHOD_PLOTS = 3
ORIGINAL_NAME = "prototypes_original_imc.json"
FFL_PARAMETER = plan_from_past.FFL_PARAMETER
NAME_PARAMETER = plan_from_past.NAME_PARAMETER
EPS = 1e-9


def angle_gap(a, b):
    gap = abs(a - b) % 360.0
    return min(gap, 360.0 - gap)


def links_by_plot(document):
    """Every link with a whole plot number, by plot number. A link may share its number, the caller counts that."""
    found = {}
    for link in document.get("links") or []:
        number = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if number is not None:
            found.setdefault(number, []).append(link)
    return found


def same_file(link, name):
    return (link.get("file") or "").lower() == name.lower()


def distance_mm(a, b):
    return math.sqrt(sum((a[axis] - b[axis]) ** 2 for axis in range(3))) * 1000.0


def text(value):
    return None if value is None else str(value)


def plan_plots(plan, prototype):
    return {entry["plot"]: entry for entry in plan["plots"] if (entry.get("prototypeFile") or {}).get("value", "").lower() == prototype.lower()}


def compare(plan, original, test, prototype, parameters, report=None):
    """The counts of R105 for the plots of one prototype file."""
    expected = plan_plots(plan, prototype)
    old_doc = plan_from_past.host_of(original)
    new_doc = plan_from_past.host_of(test)
    old_offset = ramp_levels.offset_of(old_doc)
    new_offset = ramp_levels.offset_of(new_doc)
    old_links = links_by_plot(old_doc)
    new_links = links_by_plot(new_doc)

    placed, missing, doubled = [], [], []
    for plot in sorted(expected, key=link_summary.sort_key):
        mine = [link for link in new_links.get(plot, []) if same_file(link, prototype)]
        if not mine:
            missing.append(plot)
        elif len(mine) > 1:
            doubled.append(plot)
        else:
            placed.append(plot)
    extra = sorted({plot for plot, links in new_links.items() if plot not in expected and any(same_file(link, prototype) for link in links)}, key=link_summary.sort_key)
    unnumbered = sum(1 for link in new_doc.get("links") or [] if same_file(link, prototype) and plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER)) is None)

    counts = Counter()
    gaps, rows, no_key = [], [], []
    parameter_equal = {name: 0 for name in parameters}
    parameter_differ = {name: [] for name in parameters}
    ffl = Counter()
    for plot in placed:
        new = next(link for link in new_links[plot] if same_file(link, prototype))
        old_all = old_links.get(plot, [])
        if len(old_all) != 1:
            no_key.append(plot)
            continue
        old = old_all[0]
        row = {"plot": plot}
        old_origin = plan_from_past.shared_origin(old, old_offset)
        new_origin = plan_from_past.shared_origin(new, new_offset)
        if old_origin is None or new_origin is None:
            counts["positionUnread"] += 1
        else:
            gap = distance_mm(old_origin, new_origin)
            gaps.append((gap, plot))
            row["positionMm"] = common.r3(gap)
            counts["within1mm" if gap <= POSITION_MM + EPS else "within10mm" if gap <= LOOSE_MM + EPS else "worse"] += 1
        old_t, new_t = old.get("transform") or {}, new.get("transform") or {}
        if old_t.get("rotationDeg") is not None and new_t.get("rotationDeg") is not None and angle_gap(old_t["rotationDeg"], new_t["rotationDeg"]) <= ANGLE_TOLERANCE + EPS:
            counts["rotationEqual"] += 1
        else:
            row["rotation"] = "differs"
        if old_t.get("mirrored") is not None and old_t.get("mirrored") == new_t.get("mirrored"):
            counts["mirroredEqual"] += 1
        else:
            row["mirrored"] = "differs"
        counts["fileEqual" if (old.get("file") or "").lower() == (new.get("file") or "").lower() else "fileDiffers"] += 1
        counts["rawNameEqual" if old.get("rawName") == new.get("rawName") else "rawNameDiffers"] += 1
        ffl_value = ((expected[plot].get("levels") or {}).get("ffl") or {}).get("value")
        if new_origin is not None and ffl_value is not None and abs(new_origin[2] - ffl_value) * 1000.0 <= Z_MM + EPS:
            counts["zEqualFfl"] += 1
        else:
            row["z"] = "differs from the plan FFL"
        old_p, new_p = old.get("parameters") or {}, new.get("parameters") or {}
        # R107. The instance Name the placer writes, a real check like the parameters
        if old_p.get(NAME_PARAMETER) and text(old_p.get(NAME_PARAMETER)) == text(new_p.get(NAME_PARAMETER)):
            counts["nameEqual"] += 1
        else:
            row["name"] = "differs"
        for name in parameters:
            if text(old_p.get(name)) == text(new_p.get(name)):
                parameter_equal[name] += 1
            else:
                parameter_differ[name].append(plot)
        if FFL_PARAMETER not in new_p or new_p.get(FFL_PARAMETER) in (None, ""):
            ffl["emptyInTest"] += 1
        elif text(old_p.get(FFL_PARAMETER)) == text(new_p.get(FFL_PARAMETER)):
            ffl["equal"] += 1
        else:
            ffl["differs"] += 1
        rows.append(row)

    compared = len(placed) - len(no_key)
    gaps.sort(reverse=True)
    result = {
        "prototypeFile": prototype,
        "plots": {
            "expected": len(expected), "placed": len(placed), "missing": len(missing), "extra": len(extra),
            "placedTwice": len(doubled), "unnumberedLinksOfThisFile": unnumbered, "noSingleOriginal": len(no_key),
        },
        "missingPlots": missing,
        "extraPlots": extra,
        "position": {"within1mm": counts["within1mm"], "within10mm": counts["within10mm"], "worse": counts["worse"], "unread": counts["positionUnread"],
                     "worst": [{"plot": plot, "mm": common.r3(gap)} for gap, plot in gaps[:WORST]]},
        "rotationEqual": counts["rotationEqual"],
        "mirroredEqual": counts["mirroredEqual"],
        "fileEqual": counts["fileEqual"],
        "nameEqual": counts["nameEqual"],
        "rawNameEqual": counts["rawNameEqual"],
        "zEqualFfl": counts["zEqualFfl"],
        "compared": compared,
        "parameters": {name: {"equal": parameter_equal[name], "differ": len(parameter_differ[name]), "plots": parameter_differ[name][:WORST]} for name in parameters},
        "prototypeFfl": {"equal": ffl["equal"], "differs": ffl["differs"], "emptyInTest": ffl["emptyInTest"]},
        "readerFixes": reader_fixes(new_doc),
        "placerReport": report_summary(report),
        "differing": [row for row in rows if differs(row)],
    }
    result["pass"] = passes(result, parameters)
    return result


def differs(row):
    """A row lists a field only when it differs, the position is listed always and differs over 1 mm."""
    return any(key not in ("plot", "positionMm") for key in row) or row.get("positionMm", 0) > POSITION_MM + EPS


def passes(result, parameters):
    """R105 and R107. Every plot placed once, position within 1 mm, rotation, mirror, Z, the instance Name and every written parameter equal, and a placer run that neither stopped nor rolled back."""
    plots = result["plots"]
    n = plots["expected"]
    ok = plots["placed"] == n and plots["missing"] == 0 and plots["extra"] == 0 and plots["placedTwice"] == 0 and plots["noSingleOriginal"] == 0
    ok = ok and result["position"]["within1mm"] == n and result["rotationEqual"] == n and result["mirroredEqual"] == n and result["zEqualFfl"] == n
    ok = ok and result["nameEqual"] == n
    ok = ok and all(result["parameters"][name]["equal"] == n for name in parameters)
    report = result["placerReport"]
    return bool(ok and report is not None and not report["stopped"] and not report["rolledBack"])


def reader_fixes(document):
    """The two reader fixes of R54 and R55 on the test model: loaded links with a box, spots with a host."""
    links = document.get("links") or []
    loaded = [link for link in links if link.get("loaded") is True]
    spots = document.get("spotElevations") or []
    return {
        "links": len(links),
        "loaded": len(loaded),
        "loadedWithBox": sum(1 for link in loaded if link.get("boundingBoxCorners")),
        "boxSource": dict(Counter(str(link.get("boxSource")) for link in loaded)),
        "spots": len(spots),
        "spotsWithHost": sum(1 for spot in spots if spot.get("hostSource")),
        "hostSource": dict(Counter(str(spot.get("hostSource")) for spot in spots)),
    }


def report_summary(report):
    if report is None:
        return None
    return {
        "mode": report.get("mode"),
        "stopped": bool(report.get("stopped")),
        "rolledBack": bool(report.get("rolledBack")),
        "failure": report.get("failure"),
        "counts": report.get("counts"),
        "skipped": [{"plot": item.get("plot"), "reason": item.get("reason")} for item in report.get("skipped") or []],
        "issues": dict(Counter(issue.split(": ", 1)[-1] for issue in report.get("issues") or [])),
    }


def by_hand(link, survey):
    """The second method. Origin, rotation and mirror worked out from the raw transform axes, with no helper of the main method."""
    transform = link["transform"]
    shift = [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(3)]
    origin = [transform["origin"][axis] + shift[axis] for axis in range(3)]
    x, y = transform["axisX"], transform["axisY"]
    rotation = math.degrees(math.atan2(x[1], x[0])) % 360.0
    # The Z of X cross Y is down for a mirrored link
    mirrored = x[0] * y[1] - x[1] * y[0] < 0
    return origin, rotation, mirrored


def second_method(plan, original, test, prototype, seed):
    """R105. SECOND_METHOD_PLOTS random placed plots, both raw files, recomputed by hand."""
    old_doc, new_doc = plan_from_past.host_of(original), plan_from_past.host_of(test)
    old_links, new_links = links_by_plot(old_doc), links_by_plot(new_doc)
    plots = sorted((plot for plot in plan_plots(plan, prototype)
                    if len(old_links.get(plot, [])) == 1 and len([link for link in new_links.get(plot, []) if same_file(link, prototype)]) == 1),
                   key=link_summary.sort_key)
    picked = random.Random(seed).sample(plots, min(SECOND_METHOD_PLOTS, len(plots)))
    rows = []
    for plot in picked:
        old_origin, old_rotation, old_mirror = by_hand(old_links[plot][0], old_doc["surveyPoint"])
        new = next(link for link in new_links[plot] if same_file(link, prototype))
        new_origin, new_rotation, new_mirror = by_hand(new, new_doc["surveyPoint"])
        gap = distance_mm(old_origin, new_origin)
        turn = angle_gap(old_rotation, new_rotation)
        rows.append({
            "plot": plot, "positionMm": common.r3(gap), "rotationGapDeg": common.r3(turn), "mirroredEqual": old_mirror == new_mirror,
            "match": gap <= POSITION_MM + EPS and turn <= ANGLE_TOLERANCE + EPS and old_mirror == new_mirror,
        })
    return {"seed": seed, "plots": rows, "match": sum(1 for row in rows if row["match"]), "of": len(rows)}


def main(argv=None):
    settings = project_profile.load_settings()
    job = plan_from_past.load_job(settings)
    folder = Path(job["output"]).parent
    parser = argparse.ArgumentParser(description="Check one test host run of one prototype against the answer key.")
    parser.add_argument("code", help="prototype code, such as C10")
    parser.add_argument("--plan", default=job["output"])
    parser.add_argument("--original", default=str(folder / ORIGINAL_NAME))
    parser.add_argument("--test", help="testhost_<code>_prototypes.json")
    parser.add_argument("--report", help="testhost_<code>_placer_report.json")
    parser.add_argument("--seed", type=int, default=None, help="seed of the second method, a new one is drawn and printed when left out")
    args = parser.parse_args(argv)
    test = Path(args.test or folder / ("testhost_" + args.code + "_prototypes.json"))
    report_path = Path(args.report or folder / ("testhost_" + args.code + "_placer_report.json"))
    out = test.parent / ("testhost_" + args.code + "_check.json")
    plot_relations.refuse_inside_repo(out.parent)

    plan = link_summary.load(args.plan)
    files = sorted({entry["prototypeFile"]["value"] for entry in plan["plots"] if "-" + args.code + "-ZZ-MOD-" in entry["prototypeFile"]["value"]})
    if len(files) != 1:
        print("the plan names %d prototype files for %s, one is needed" % (len(files), args.code))
        return 2
    seed = args.seed if args.seed is not None else random.SystemRandom().randrange(1, 10 ** 6)
    original, data = link_summary.load(args.original), link_summary.load(test)
    report = link_summary.load(report_path) if report_path.exists() else None
    result = compare(plan, original, data, files[0], job["parameters"], report)
    result["secondMethod"] = second_method(plan, original, data, files[0], seed)
    common.write_json(out, result)

    plots, position = result["plots"], result["position"]
    print("%s %s" % (args.code, files[0]))
    print("plots expected %d, placed %d, missing %d, extra %d, placed twice %d" % (plots["expected"], plots["placed"], plots["missing"], plots["extra"], plots["placedTwice"]))
    print("position within 1 mm %d, within 10 mm %d, worse %d, worst %s" % (position["within1mm"], position["within10mm"], position["worse"], position["worst"]))
    print("rotation equal %d, mirrored equal %d, file equal %d, link name equal %d, full raw name equal %d, Z equal FFL %d, of %d compared" % (
        result["rotationEqual"], result["mirroredEqual"], result["fileEqual"], result["nameEqual"], result["rawNameEqual"], result["zEqualFfl"], result["compared"]))
    print("parameters equal " + ", ".join("%s %d" % (name, value["equal"]) for name, value in result["parameters"].items()))
    print("Prototype-FFL apart %s" % result["prototypeFfl"])
    print("reader fixes %s" % result["readerFixes"])
    print("placer report %s" % ({key: result["placerReport"][key] for key in ("stopped", "rolledBack", "counts", "skipped", "issues")} if report else "missing"))
    second = result["secondMethod"]
    print("second method seed %d, %d of %d match %s" % (second["seed"], second["match"], second["of"], second["plots"]))
    print("PASS" if result["pass"] else "FAIL")
    print(out)
    return 0 if result["pass"] else 1


if __name__ == "__main__":
    sys.exit(main())
