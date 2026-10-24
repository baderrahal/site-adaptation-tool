"""Checks one test host run of one prototype against the answer key, the reader output of the past model (R105).

It joins the links of prototypes_original_imc.json and testhost_<code>_prototypes.json by the official plot number,
for the plots the plan gives that prototype file, and counts what matches. With --floors it checks the floors of a floor
scenario the same way (R119). With --split it checks a whole built file against an answer key in three checks, plan
position, level and the footprint as an edge test, and reads a key of the IMC or the Native dialect (R261 to R263).
It changes nothing and fixes nothing.
"""
import argparse
import copy
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
import yard_rule

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
EQUAL_MM = 0.001


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


# R119. The floor check

TOP_MM = 1.0
AREA_M2 = 0.01
OUTLINE_MM = 1.0
SLOPE_POINTS = 0.01
FLOOR_KINDS = plan_from_past.FLOOR_KINDS


def kind_of_floor(floor, config):
    return plan_from_past.kind_of(floor, config)


def answer_floors(document, config, faces):
    """The floors of the answer key by plot and kind. Generic and ramp go by their plot number, a yard by the yard rule of R111,
    because the past model left the plot parameter off its yards."""
    offset = ramp_levels.offset_of(document)
    found, yards = {}, []
    floors = document.get("floors") or []
    for floor in floors:
        kind = kind_of_floor(floor, config)
        if kind == "yard":
            yards.append((floor["elementId"], plot_relations.floor_shape(floor, offset)[0]))
        elif kind is not None:
            plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
            if plot is not None:
                found.setdefault((plot, kind), []).append(floor)
    by_id = {floor["elementId"]: floor for floor in floors}
    for plot, ids in yard_rule.by_plot(yard_rule.assign(yards, faces, config["yardShareTie"], config["yardMinShare"])).items():
        found[(plot, "yard")] = [by_id[element_id] for element_id in ids]
    return found


def plot_of_value(value):
    """The plot number of a unit-plot value such as VD2-2441, or None."""
    text = str(value).strip() if value not in (None, "") else ""
    unit, dash, plot = text.rpartition("-")
    return plan_from_past.plot_number(plot) if dash and unit else None


def test_floors(document, config):
    """The floors of the test model by plot and kind, all three by the plot parameter, and how many of the kinds have none."""
    found, unnumbered = {}, 0
    for floor in document.get("floors") or []:
        kind = kind_of_floor(floor, config)
        if kind is None:
            continue
        plot = plot_of_value((floor.get("parameters") or {}).get(config["plotParameter"]))
        if plot is None:
            unnumbered += 1
            continue
        found.setdefault((plot, kind), []).append(floor)
    return found, unnumbered


def floor_total(document, config):
    return sum(1 for floor in document.get("floors") or [] if kind_of_floor(floor, config) is not None)


loop_points = plan_from_past.loop_points


def plan_shape(plan_floor):
    return plot_relations.even_odd([loop_points(loop) for loop in plan_floor["outline"]["value"]["loops"]])


def outline_gap_mm(a, b):
    """The largest distance between the two outlines in plan, in mm, the Hausdorff distance of their boundaries."""
    return a.boundary.hausdorff_distance(b.boundary) * 1000.0


def shared_corners(floor, offset):
    corners, source = ramp_levels.corner_points(floor, offset)
    return corners if source == "corners" else []


def corner_gap_mm(mine, theirs):
    """The largest z difference in mm between each corner and the corner of the other set that sits nearest in plan, or None."""
    if len(mine) != len(theirs) or not mine:
        return None
    return max(abs(min(theirs, key=lambda other: math.dist(corner[:2], other[:2]))[2] - corner[2]) for corner in mine) * 1000.0


def slope_of(corners):
    geometry = plot_relations.ramp_geometry(corners) if len(corners) == 4 else None
    return None if geometry is None else geometry["slope"]


def within(value, limit):
    return value is not None and value <= limit + EPS


def compare_floor(kind, old, new, plan_floor, old_offset, new_offset, config):
    """One floor of the test against the answer key and the plan. A field that differs from the answer key while the test
    holds the plan value is a past data difference, the plan rule differs from the past model, and not a placer fault.
    Only the top can be past data, it is the one value the plan works out by a rule (the yard depth). Everything else the plan
    copies from the past model, so it must equal both. Equal means within the limit of the answer key and of the plan."""
    checks = {}

    def mark(name, against_key, against_plan, past=False):
        checks[name] = "equal" if against_key and against_plan else "pastData" if past and against_plan else "differs"

    plan_type = plan_floor["type"]["value"]
    mark("type", old.get("typeName") == new.get("typeName"), new.get("typeName") == plan_type)
    mark("level", old.get("level") == new.get("level"), new.get("level") == plan_floor["level"]["value"])
    row = {}
    if kind == "ramp":
        old_corners, new_corners = shared_corners(old, old_offset), shared_corners(new, new_offset)
        planned = plan_floor["corners"]["value"]
        to_key, to_plan = corner_gap_mm(new_corners, old_corners), corner_gap_mm(new_corners, planned)
        row["cornerZMm"] = None if to_key is None else common.r3(to_key)
        mark("cornersZ", within(to_key, TOP_MM), within(to_plan, TOP_MM))
        old_slope, new_slope = slope_of(old_corners), slope_of(new_corners)
        slope_gap = None if old_slope is None or new_slope is None else abs(old_slope - new_slope)
        plan_slope = slope_of(planned)
        mark("slope", within(slope_gap, SLOPE_POINTS), new_slope is not None and plan_slope is not None and abs(new_slope - plan_slope) <= SLOPE_POINTS + EPS)
        # The highest point read from the top faces must be the highest corner, so a corner list that agrees with itself cannot hide a wrong slab
        new_high = plot_relations.top_range(new, new_offset)[1]
        row["topMaxMm"] = None if new_high is None or not new_corners else common.r3(abs(new_high - max(corner[2] for corner in new_corners)) * 1000.0)
        mark("topMax", within(row["topMaxMm"], TOP_MM), within(row["topMaxMm"], TOP_MM))
        row["cornersSource"] = new.get("cornersSource")
        checks["cornersSource"] = "equal" if new.get("cornersSource") == old.get("cornersSource") == "shapeEditor" else "differs"
    else:
        low, high, _ = plot_relations.top_range(new, new_offset)
        old_low, old_high, _ = plot_relations.top_range(old, old_offset)
        z = plan_floor["z"]["value"]
        to_key = None if low is None or old_low is None else max(abs(low - old_low), abs(high - old_high)) * 1000.0
        to_plan = None if low is None else max(abs(low - z), abs(high - z)) * 1000.0
        row["topMm"] = None if to_key is None else common.r3(to_key)
        mark("top", within(to_key, TOP_MM), within(to_plan, TOP_MM), past=True)
    old_shape, new_shape, planned_shape = plot_relations.floor_shape(old, old_offset)[0], plot_relations.floor_shape(new, new_offset)[0], plan_shape(plan_floor)
    if old_shape is None or new_shape is None:
        checks["area"] = checks["outline"] = "differs"
    else:
        area_gap, gap = abs(old_shape.area - new_shape.area), outline_gap_mm(old_shape, new_shape)
        plan_area_gap = abs(planned_shape.area - new_shape.area) if planned_shape is not None else None
        plan_gap = outline_gap_mm(planned_shape, new_shape) if planned_shape is not None else None
        row["areaGapM2"], row["outlineGapMm"] = round(area_gap, 4), common.r3(gap)
        mark("area", within(area_gap, AREA_M2), within(plan_area_gap, AREA_M2))
        mark("outline", within(gap, OUTLINE_MM), within(plan_gap, OUTLINE_MM))
    value = (new.get("parameters") or {}).get(config["plotParameter"])
    checks["plotValue"] = "equal" if value == plan_floor["plotValue"]["value"] else "differs"
    row["checks"] = checks
    return row


def expected_floors(plan, plots):
    """The floors the plan holds, by plot and kind, for the plots given or every plot."""
    wanted = None if plots is None else set(plots)
    return {(entry["plot"], kind): floor for entry in plan["plots"] if wanted is None or entry["plot"] in wanted
            for kind, floor in (entry.get("floors") or {}).items()}


def floor_report_summary(report):
    """The floor part of a placer_report of R116, or None."""
    floors = (report or {}).get("floors")
    if floors is None:
        return None
    return {
        "mode": floors.get("mode"), "stopped": bool(floors.get("stopped")), "rolledBack": bool(floors.get("rolledBack")), "failure": floors.get("failure"),
        "counts": floors.get("counts"), "floorsBefore": floors.get("floorsBefore"), "floorsAfter": floors.get("floorsAfter"),
        "placedPlots": sorted({item["plot"] for item in floors.get("placed") or []}, key=link_summary.sort_key),
        "skippedReasons": dict(Counter(item.get("reason", "").split(", a ")[0].split(" has ")[0] for item in floors.get("skipped") or [])),
        "skipped": [{"plot": item.get("plot"), "kind": item.get("kind"), "reason": item.get("reason")} for item in (floors.get("skipped") or [])[:WORST]],
        "issues": dict(Counter(issue.split(": ", 1)[-1] for issue in floors.get("issues") or [])),
    }


def compare_floors(plan, original, test, config, faces, plots=None, report=None):
    """R119. The floors of one scenario against the answer key, per kind, with the plan as the judge of a difference."""
    old_doc, new_doc = plan_from_past.host_of(original), plan_from_past.host_of(test)
    old_offset, new_offset = ramp_levels.offset_of(old_doc), ramp_levels.offset_of(new_doc)
    key = answer_floors(old_doc, config, faces)
    mine, unnumbered = test_floors(new_doc, config)
    expected = expected_floors(plan, plots)
    wanted = None if plots is None else set(plots)
    in_scope = (lambda pair: wanted is None or pair[0] in wanted)
    per_kind = {}
    checks = {}
    rows, gaps, tops = [], [], []
    ramp_types, arc_plots = {}, []
    for kind in FLOOR_KINDS:
        key_pairs = {pair for pair in key if pair[1] == kind and in_scope(pair)}
        test_pairs = {pair for pair in mine if pair[1] == kind and in_scope(pair)}
        plan_pairs = {pair for pair in expected if pair[1] == kind}
        per_kind[kind] = {
            "answerKey": len(key_pairs), "test": len(test_pairs), "plan": len(plan_pairs), "inBoth": len(key_pairs & test_pairs),
            "onlyAnswerKey": sorted((pair[0] for pair in key_pairs - test_pairs), key=link_summary.sort_key),
            "onlyTest": sorted((pair[0] for pair in test_pairs - key_pairs), key=link_summary.sort_key),
            "planNotInTest": sorted((pair[0] for pair in plan_pairs - test_pairs), key=link_summary.sort_key),
            "testNotInPlan": sorted((pair[0] for pair in test_pairs - plan_pairs), key=link_summary.sort_key),
            "twiceInTest": sorted((pair[0] for pair in test_pairs if len(mine[pair]) > 1), key=link_summary.sort_key),
            "notCompared": sorted((pair[0] for pair in test_pairs & plan_pairs if pair not in key or len(key[pair]) != 1 or len(mine[pair]) != 1), key=link_summary.sort_key),
        }
        for pair in sorted(key_pairs & test_pairs & plan_pairs, key=lambda item: link_summary.sort_key(item[0])):
            if len(key[pair]) != 1 or len(mine[pair]) != 1:
                continue
            row = compare_floor(kind, key[pair][0], mine[pair][0], expected[pair], old_offset, new_offset, config)
            row.update({"plot": pair[0], "kind": kind})
            for name, state in row["checks"].items():
                checks.setdefault(kind + "." + name, Counter())[state] += 1
            if "outlineGapMm" in row:
                gaps.append((row["outlineGapMm"], pair[0], kind))
            if row.get("topMm") is not None:
                tops.append((row["topMm"], pair[0], kind))
            if kind == "ramp":
                type_row = ramp_types.setdefault(expected[pair]["type"]["value"], Counter())
                type_row["plots"] += 1
                type_row["allEqual"] += all(state == "equal" for state in row["checks"].values())
                type_row["cornersZEqual"] += row["checks"]["cornersZ"] == "equal"
                type_row["slopeEqual"] += row["checks"]["slope"] == "equal"
            if any(loop.get("arcs") for loop in expected[pair]["outline"]["value"]["loops"]):
                arc_plots.append({"plot": pair[0], "kind": kind, "allEqual": all(state == "equal" for state in row["checks"].values())})
            rows.append(row)
    gaps.sort(reverse=True)
    tops.sort(reverse=True)
    placer = sum(state["differs"] for state in checks.values())
    past = sum(state["pastData"] for state in checks.values())
    result = {
        "plotFilter": None if plots is None else sorted(plots, key=link_summary.sort_key),
        "floors": per_kind,
        "checks": {name: dict(state) for name, state in sorted(checks.items())},
        "differsByPlacer": placer,
        "pastDataDifferences": past,
        "pastDataByCheck": {name: state["pastData"] for name, state in sorted(checks.items()) if state["pastData"]},
        "worstOutline": [{"plot": plot, "kind": kind, "mm": mm} for mm, plot, kind in gaps[:WORST]],
        "worstTop": [{"plot": plot, "kind": kind, "mm": mm} for mm, plot, kind in tops[:WORST]],
        "rampTypes": {name: dict(state) for name, state in sorted(ramp_types.items())},
        "arcPlots": arc_plots,
        "unnumberedTestFloors": unnumbered,
        "model": {"answerKeyFloors": floor_total(old_doc, config), "testFloors": floor_total(new_doc, config), "planFloors": len(expected)},
        "placerReport": floor_report_summary(report),
        "differing": [row for row in rows if any(state == "differs" for state in row["checks"].values())][:50],
    }
    return result


def floors_pass(result, expect_empty=False, expect_end=None, placed_plots=None, second=None):
    """R119. A full scenario passes when every planned floor is in the test once and differs from the answer key only by
    past data, nothing else of the kinds is in the test, and the step neither stopped nor rolled back. An empty scenario
    passes when the test model holds no floor of the kinds and the report shows the end it wanted and no change."""
    report = result["placerReport"]
    if report is None:
        return False
    no_change = report["floorsBefore"] == report["floorsAfter"]
    if expect_empty:
        ended = "stop" if report["stopped"] else "rollBack" if report["rolledBack"] else report["mode"]
        return result["model"]["testFloors"] == 0 and no_change and ended == expect_end
    if report["stopped"] or report["rolledBack"]:
        return False
    for state in result["floors"].values():
        if state["planNotInTest"] or state["testNotInPlan"] or state["twiceInTest"] or state["notCompared"] or state["onlyTest"]:
            return False
    # A filter must not hide floors on other plots, the whole model holds the planned floors and no other
    if result["model"]["testFloors"] != result["model"]["planFloors"]:
        return False
    if result["differsByPlacer"] or result["unnumberedTestFloors"]:
        return False
    if placed_plots is not None and report["placedPlots"] != sorted(placed_plots, key=link_summary.sort_key):
        return False
    if second is not None and not second:
        return False
    return True


def second_run(report):
    """R114 on the second run of S2: nothing placed, every floor skipped as placed already, and no change in the model."""
    floors = (report or {}).get("floors")
    if floors is None:
        return None
    skipped = floors.get("skipped") or []
    # A floor the plan does not hold is skipped on every run, such as the Generic floor of a plot the past model left without one
    reasons = [str(item.get("reason", "")) for item in skipped]
    as_placed = sum(1 for reason in reasons if reason.startswith("already placed"))
    not_planned = sum(1 for reason in reasons if reason.startswith("the plan has no "))
    return {
        "placed": len(floors.get("placed") or []), "skipped": len(skipped), "skippedAsPlaced": as_placed, "skippedNotInPlan": not_planned,
        "floorsBefore": floors.get("floorsBefore"), "floorsAfter": floors.get("floorsAfter"),
        "pass": bool(not floors.get("stopped") and not floors.get("rolledBack") and not floors.get("placed")
                     and floors.get("floorsBefore") == floors.get("floorsAfter") and as_placed > 0 and as_placed + not_planned == len(skipped)),
    }


def floors_by_hand(original, test, config, faces, plan, seed, count=SECOND_METHOD_PLOTS):
    """R119 second method. Random plots, area by the shoelace formula on the raw outline points against the answer key,
    and the highest point of the raw floor against the plan (z, or the highest corner of a ramp), with no geometry library,
    for the Generic floor and the ramp of each."""
    expected = expected_floors(plan, None)
    old_doc, new_doc = plan_from_past.host_of(original), plan_from_past.host_of(test)
    mine = test_floors(new_doc, config)[0]
    key = answer_floors(old_doc, config, faces)
    plots = sorted({plot for plot, kind in mine if kind in ("generic", "ramp") and (plot, kind) in key}, key=link_summary.sort_key)
    rows = []
    for plot in random.Random(seed).sample(plots, min(count, len(plots))):
        for kind in ("generic", "ramp"):
            if (plot, kind) not in mine or (plot, kind) not in key:
                continue
            old, new = key[(plot, kind)][0], mine[(plot, kind)][0]

            def area(floor):
                # The pieces of a floor add up, a Generic floor and a ramp have one loop each
                total = 0.0
                for ring in floor.get("outline") or []:
                    total += abs(sum(ring[index][0] * ring[(index + 1) % len(ring)][1] - ring[(index + 1) % len(ring)][0] * ring[index][1] for index in range(len(ring)))) / 2
                return total

            gap = abs(area(old) - area(new))
            planned = expected.get((plot, kind))
            if planned is None:
                continue
            high = planned["z"]["value"] if kind == "generic" else max(corner[2] for corner in planned["corners"]["value"])
            read = (new.get("topElevationRange") or {}).get("max")
            if read is None:
                rows.append({"plot": plot, "kind": kind, "areaGapM2": round(gap, 4), "topMaxMm": None, "match": False})
                continue
            top = abs(high - read) * 1000.0
            rows.append({"plot": plot, "kind": kind, "areaGapM2": round(gap, 4), "topMaxMm": common.r3(top), "match": gap <= AREA_M2 + EPS and top <= TOP_MM + EPS})
    return {"seed": seed, "floors": rows, "match": sum(1 for row in rows if row["match"]), "of": len(rows)}


def floors_main(args, job, folder, settings):
    config = plan_from_past.load_floors(settings)
    test = Path(args.test or folder / ("testhost_" + args.code + "_prototypes.json"))
    report_path = Path(args.report or folder / ("testhost_" + args.code + "_placer_report.json"))
    out = test.parent / ("testhost_" + args.code + "_check.json")
    plot_relations.refuse_inside_repo(out.parent)
    plan = link_summary.load(args.plan)
    baseplan = Path(args.baseplan or project_profile.ROOT / job["baseplanPlots"])
    faces, _ = plan_from_past.baseplan_faces(baseplan, args.shifts, job["neighbourhood"])
    plots = None
    if args.plots:
        plots = [plot.strip() for plot in args.plots.split(",") if plot.strip()]
    if args.prototype_code:
        plots = [entry["plot"] for entry in plan["plots"] if "-" + args.prototype_code + "-ZZ-MOD-" in entry["prototypeFile"]["value"]]
    original, data = link_summary.load(args.floors_original or job["prototypes"]), link_summary.load(test)
    report = link_summary.load(report_path) if report_path.exists() else None
    result = compare_floors(plan, original, data, config, faces, plots, report)
    second_path = report_path.with_name(report_path.stem + "2.json")
    result["secondRun"] = second_run(link_summary.load(second_path)) if second_path.exists() else None
    if args.prototype_code and report is not None and report.get("links") is not None:
        files = sorted({entry["prototypeFile"]["value"] for entry in plan["plots"] if "-" + args.prototype_code + "-ZZ-MOD-" in entry["prototypeFile"]["value"]})
        links = compare(plan, link_summary.load(args.original), data, files[0], job["parameters"], report["links"])
        result["links"] = {key: links[key] for key in ("prototypeFile", "plots", "position", "rotationEqual", "mirroredEqual", "nameEqual", "zEqualFfl", "compared", "parameters", "pass")}
    seed = args.seed if args.seed is not None else random.SystemRandom().randrange(1, 10 ** 6)
    result["secondMethod"] = floors_by_hand(original, data, config, faces, plan, seed) if result["model"]["testFloors"] else {"seed": seed, "floors": [], "match": 0, "of": 0}
    # none asks that the real run placed nothing at all
    placed = None if not args.placed_plots else [] if args.placed_plots == "none" else [plot.strip() for plot in args.placed_plots.split(",")]
    second = result["secondRun"]["pass"] if args.second_run and result["secondRun"] else (False if args.second_run else None)
    result["pass"] = floors_pass(result, args.expect_empty is not None, args.expect_empty, placed, second)
    if "links" in result:
        result["pass"] = result["pass"] and result["links"]["pass"]
    result["pass"] = result["pass"] and (result["secondMethod"]["match"] == result["secondMethod"]["of"])
    common.write_json(out, result)
    print("%s floors, filter %s" % (args.code, "none" if plots is None else str(len(plots)) + " plots"))
    for kind, state in result["floors"].items():
        print("  %s: answer key %d, test %d, plan %d, in both %d, only answer key %s, only test %s, plan not in test %s, twice %s" % (
            kind, state["answerKey"], state["test"], state["plan"], state["inBoth"], state["onlyAnswerKey"][:WORST], state["onlyTest"][:WORST], state["planNotInTest"][:WORST], state["twiceInTest"][:WORST]))
    for name, state in result["checks"].items():
        print("  %s %s" % (name, state))
    print("differs by placer %d, past data %d %s" % (result["differsByPlacer"], result["pastDataDifferences"], result["pastDataByCheck"]))
    print("worst outline %s" % result["worstOutline"])
    print("ramp types %s, arc plots %d" % (result["rampTypes"], len(result["arcPlots"])))
    print("model floors: answer key %d, test %d, unnumbered in test %d" % (result["model"]["answerKeyFloors"], result["model"]["testFloors"], result["unnumberedTestFloors"]))
    summary = result["placerReport"]
    print("placer report %s" % ({key: summary[key] for key in ("mode", "stopped", "rolledBack", "failure", "counts", "floorsBefore", "floorsAfter", "skippedReasons")} if summary else "missing"))
    if result["secondRun"]:
        print("second run %s" % result["secondRun"])
    if "links" in result:
        print("links %s" % {key: result["links"][key] for key in ("plots", "position", "rotationEqual", "mirroredEqual", "nameEqual", "pass")})
    method = result["secondMethod"]
    print("second method seed %d, %d of %d match" % (method["seed"], method["match"], method["of"]))
    print("PASS" if result["pass"] else "FAIL")
    print(out)
    return 0 if result["pass"] else 1


# R261 to R263. The split check: plan position and level apart, the footprint as an edge test, and the Native dialect

def load_split(settings):
    """R261. The placerCheck block: bands and pass limit of the position and of the level check, and the key dialects."""
    block = settings.get("placerCheck")
    name = settings.path.name
    if not isinstance(block, dict):
        raise project_profile.ProfileError(name + " has no placerCheck block")
    for check in ("position", "level"):
        part = block.get(check)
        if not isinstance(part, dict) or not isinstance(part.get("bandsMm"), list) or not part["bandsMm"] or part["bandsMm"][0] != 0:
            raise project_profile.ProfileError(name + " placerCheck." + check + ".bandsMm must be a list of mm that starts with 0")
        if not plan_from_past.is_number(part.get("passMm")) or part["passMm"] < 0:
            raise project_profile.ProfileError(name + " placerCheck." + check + ".passMm must be mm of 0 or more")
    dialects = block.get("dialects")
    if not isinstance(dialects, dict) or not dialects:
        raise project_profile.ProfileError(name + " placerCheck.dialects must name each key dialect")
    for key, dialect in dialects.items():
        if dialect.get("plot") not in ("parameter", "name") or dialect.get("datum") not in ("none", "surveyElevation"):
            raise project_profile.ProfileError(name + " placerCheck.dialects." + key + " needs plot parameter or name and datum none or surveyElevation")
    return block


def dialect_floors(floors, dialect):
    """The floors block a key is read with: the project floor types, or the types the dialect names, with no yard when it has none."""
    if not dialect.get("floorTypes"):
        return floors
    types = {kind: list(dialect["floorTypes"].get(kind) or []) for kind in FLOOR_KINDS}
    return dict(floors, types=types)


def key_views(data, dialect, floors, faces=None, numbers=None):
    """R261, R263. Each plot of the answer key: link file, origin in shared coordinates less the datum, plan rotation, mirror
    and FFL, the Generic floor top less the datum. A Native key holds its plot in the link Name and its floors by type FFL
    and Ramps, so its links and floors get the official number by the face of their FFL floor (R206) first."""
    data = copy.deepcopy(data)
    mapping = None
    if dialect["plot"] == "name":
        data, _, mapping = plan_from_past.names_dialect(data, floors, faces, numbers)
    document = plan_from_past.host_of(data)
    offset = ramp_levels.offset_of(document)
    datum = offset[2] if dialect["datum"] == "surveyElevation" else 0.0
    views, twice = {}, Counter()
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if plot is None:
            continue
        twice[plot] += 1
        views[plot] = view_of(link, offset, datum)
    for floor in document.get("floors") or []:
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if plot in views and plan_from_past.kind_of(floor, floors) == "generic":
            views[plot]["ffl"] = plot_relations.top_range(floor, offset)[1] - datum
    for plot, count in twice.items():
        if count > 1:
            views[plot]["twice"] = True
    return views, {"datumM": common.r3(datum), "names": mapping}


def view_of(link, offset, datum=0.0):
    transform = link.get("transform") or {}
    origin = plan_from_past.shared_origin(link, offset)
    if origin is not None:
        origin = [origin[0], origin[1], origin[2] - datum]
    rotation = transform.get("rotationDeg")
    return {"file": link.get("file"), "origin": origin, "rotation": None if rotation is None else plan_from_past.plan_rotation(rotation),
            "mirrored": transform.get("mirrored"), "ffl": None}


def test_views(data):
    """The links of a file this tool built, by their plot number parameter, origin in shared coordinates."""
    document = plan_from_past.host_of(data)
    offset = ramp_levels.offset_of(document)
    views = {}
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if plot is not None:
            views.setdefault(plot, []).append(view_of(link, offset))
    return views


def band_of(gap_mm, bands):
    """equal at 0, then within<n>mm for the first band that holds the gap, else worse."""
    if gap_mm is None:
        return "notCompared"
    # Shared coordinates near 2.4e6 m carry float noise, so equal is held to a thousandth of a mm as in R215
    if gap_mm <= EQUAL_MM:
        return "equal"
    for limit in bands[1:]:
        if gap_mm <= limit + 1e-6:
            return "within%gmm" % limit
    return "worse"


def split_compare(plan, key, test, block):
    """R261. Plan position (X and Y against the key link) and level (link Z against the key FFL and against the plan FFL) as two
    checks with their own bands and pass rule. Rotation and mirror are counted with the position, and a link of another file
    than the key is counted apart, its origin sits elsewhere in the house."""
    expected = {entry["plot"]: entry for entry in plan["plots"] if (entry.get("prototypeFile") or {}).get("value")}
    position, level, to_plan = Counter(), Counter(), Counter()
    orientation = Counter()
    worst_position, worst_level, rows = [], [], []
    plots = Counter()
    for plot in sorted(expected, key=link_summary.sort_key):
        mine = test.get(plot, [])
        if not mine:
            plots["missing"] += 1
            continue
        if len(mine) > 1:
            plots["placedTwice"] += 1
            continue
        plots["placed"] += 1
        new, old = mine[0], key.get(plot)
        planned_ffl = ((expected[plot].get("levels") or {}).get("ffl") or {}).get("value")
        z_plan = None if new["origin"] is None or planned_ffl is None else abs(new["origin"][2] - planned_ffl) * 1000.0
        to_plan[band_of(z_plan, block["level"]["bandsMm"])] += 1
        if old is None or old.get("twice"):
            plots["noSingleKeyLink"] += 1
            continue
        plots["compared"] += 1
        same = (old["file"] or "").lower() == (new["file"] or "").lower()
        orientation["fileEqual" if same else "fileDiffers"] += 1
        gap = None if new["origin"] is None or old["origin"] is None else math.dist(new["origin"][:2], old["origin"][:2]) * 1000.0
        name = band_of(gap, block["position"]["bandsMm"])
        position[name if same else "otherFile " + name] += 1
        if gap is not None:
            worst_position.append((gap, plot))
        turn = None if new["rotation"] is None or old["rotation"] is None else angle_gap(new["rotation"], old["rotation"])
        orientation["rotationEqual" if turn is not None and turn <= ANGLE_TOLERANCE + EPS else "rotationDiffers"] += 1
        orientation["mirroredEqual" if old["mirrored"] is not None and old["mirrored"] == new["mirrored"] else "mirroredDiffers"] += 1
        z_key = None if new["origin"] is None or old["ffl"] is None else abs(new["origin"][2] - old["ffl"]) * 1000.0
        level[band_of(z_key, block["level"]["bandsMm"])] += 1
        if z_key is not None:
            worst_level.append((z_key, plot))
        rows.append({"plot": plot, "positionMm": None if gap is None else common.r3(gap), "levelMm": None if z_key is None else common.r3(z_key), "sameFile": same})
    n = len(expected)
    worst_position.sort(reverse=True)
    worst_level.sort(reverse=True)
    position_ok = sum(count for label, count in position.items() if not label.startswith("otherFile") and within_pass(label, block["position"]["passMm"]))
    level_ok = sum(count for label, count in level.items() if within_pass(label, block["level"]["passMm"]))
    plan_ok = sum(count for label, count in to_plan.items() if within_pass(label, block["level"]["passMm"]))
    placed_ok = plots["placed"] == n and not plots["missing"] and not plots["placedTwice"]
    return {
        "plots": dict(plots, expected=n),
        "position": {"bands": dict(position), "passMm": block["position"]["passMm"], "withinPass": position_ok,
                     "rotationAndMirror": dict(orientation), "worst": [{"plot": plot, "mm": common.r3(gap)} for gap, plot in worst_position[:WORST]],
                     "pass": bool(placed_ok and position_ok == n and orientation["rotationEqual"] == n and orientation["mirroredEqual"] == n)},
        "level": {"againstKeyFfl": dict(level), "againstPlanFfl": dict(to_plan), "passMm": block["level"]["passMm"], "withinPassOfKey": level_ok,
                  "withinPassOfPlan": plan_ok, "worst": [{"plot": plot, "mm": common.r3(gap)} for gap, plot in worst_level[:WORST]],
                  "pass": bool(placed_ok and level_ok == n)},
        "rows": rows,
    }


def within_pass(label, limit):
    if label == "equal":
        return True
    if label.startswith("within") and label.endswith("mm"):
        return float(label[len("within"):-2]) <= limit + EPS
    return False


def footprint_part(report):
    """R262. The footprint test of R227 to R229 as the third check. It sees a link on the wrong edge or far off its block, and a
    mirrored house only where the unit is not near symmetric across its front, so it is an edge test and never a hand test."""
    judged = report["testJudged"]
    return {"edgeOnly": True, "note": "the footprint test cannot see a wrong hand on a near symmetric unit, it checks the edge and the place",
            "measured": report["test"]["measured"], "notMeasured": report["test"]["notMeasured"], "pass": judged["fail"] == 0 and report["test"]["measured"] > 0,
            "passed": judged["pass"], "failed": judged["fail"], "residualBands": judged["residualBands"], "worst": judged["worst10"][:WORST]}


def split_main(args, settings):
    block = load_split(settings)
    floors = plan_from_past.load_floors(settings)
    if args.dialect not in block["dialects"]:
        print("placerCheck.dialects has no " + args.dialect, file=sys.stderr)
        return 2
    dialect = block["dialects"][args.dialect]
    key_floors = dialect_floors(floors, dialect)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out.resolve().parent)
    faces = numbers = None
    if dialect["plot"] == "name":
        if not args.labels or not args.baseplan:
            print("a key of plot by name needs --labels and --baseplan", file=sys.stderr)
            return 2
        numbers, _ = plan_from_past.location_numbers(args.labels)
        faces, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    plan = link_summary.load(args.plan)
    key, key_info = key_views(link_summary.load(args.original), dialect, key_floors, faces, numbers)
    result = split_compare(plan, key, test_views(link_summary.load(args.test)), block)
    result["dialect"] = args.dialect
    result["keyDatumM"] = key_info["datumM"]
    result["keyNames"] = key_info["names"]
    if args.footprint_reads:
        import footprint_check
        result["footprint"] = footprint_part(footprint_check.load_and_run(settings, args.footprint_reads, args.footprint_key, args.footprint_key_plan, args.test, args.plan,
                                                                          args.plots_csv, args.blocks, args.baseplan, args.labels))
    common.write_json(out, result)
    print("%s, %s key, datum %s m" % (args.code, args.dialect, result["keyDatumM"]))
    print("plots %s" % result["plots"])
    for name in ("position", "level"):
        part = result[name]
        print("%s %s, %s" % (name, "PASS" if part["pass"] else "FAIL", {key: value for key, value in part.items() if key not in ("pass", "worst")}))
        print("  worst %s" % part["worst"])
    if "footprint" in result:
        part = result["footprint"]
        print("footprint, edge only, %s: passed %d, failed %d, %s" % ("PASS" if part["pass"] else "FAIL", part["passed"], part["failed"], part["residualBands"]))
    print(out)
    return 0


def main(argv=None):
    settings = project_profile.load_settings()
    if argv is None:
        argv = sys.argv[1:]
    if "--split" in argv:
        parser = argparse.ArgumentParser(description="The split check of a built file against an answer key (R261 to R263).")
        parser.add_argument("code", help="a label of the run, such as I16")
        parser.add_argument("--split", action="store_true")
        parser.add_argument("--plan", required=True, help="the plan the file was built from")
        parser.add_argument("--original", required=True, help="the reader output of the answer key")
        parser.add_argument("--test", required=True, help="the reader output of the built file")
        parser.add_argument("--dialect", required=True, help="a key dialect of placerCheck.dialects")
        parser.add_argument("--labels", help="labels.csv of the location plan, for a key of plot by name")
        parser.add_argument("--baseplan", help="plots.json of the baseplan")
        parser.add_argument("--footprint-reads", nargs="*", help="R262. testhost_P<code>_prototypes.json of each prototype")
        parser.add_argument("--footprint-key", help="the reader output of a key whose links are right and whose prototypes are read")
        parser.add_argument("--footprint-key-plan", help="the plan of that key")
        parser.add_argument("--plots-csv")
        parser.add_argument("--blocks")
        parser.add_argument("--out", required=True)
        return split_main(parser.parse_args(argv), settings)
    job = plan_from_past.load_job(settings)
    folder = Path(job["output"]).parent
    parser = argparse.ArgumentParser(description="Check one test host run of one prototype against the answer key.")
    parser.add_argument("code", help="prototype code, such as C10")
    parser.add_argument("--plan", default=job["output"])
    parser.add_argument("--original", default=str(folder / ORIGINAL_NAME))
    parser.add_argument("--test", help="testhost_<code>_prototypes.json")
    parser.add_argument("--report", help="testhost_<code>_placer_report.json")
    parser.add_argument("--seed", type=int, default=None, help="seed of the second method, a new one is drawn and printed when left out")
    floors = parser.add_argument_group("floors (R119), code is a scenario such as S1")
    floors.add_argument("--floors", action="store_true", help="check the floors of a floor scenario")
    floors.add_argument("--floors-original", help="the answer key, default pastJob.prototypes")
    floors.add_argument("--baseplan", help="plots.json of the baseplan for the yard rule, default pastJob.baseplanPlots")
    floors.add_argument("--shifts", default=str(project_profile.ROOT / "config" / "plot_numbers.json"))
    floors.add_argument("--plots", help="only these plots, by comma")
    floors.add_argument("--prototype-code", help="only the plots of this prototype, and its links too when the report has them")
    floors.add_argument("--expect-empty", choices=("stop", "rollBack", "dryRun"), help="the test model must hold no floor and the step must end this way")
    floors.add_argument("--placed-plots", help="the plots the real run must have placed, by comma, and no other, or none")
    floors.add_argument("--second-run", action="store_true", help="the second real run must skip every floor as placed already")
    args = parser.parse_args(argv)
    if args.floors:
        return floors_main(args, job, folder, settings)
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
