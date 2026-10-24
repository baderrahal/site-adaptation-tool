"""R215 and R216. The plots of the round against the answer key, per field in the bands of siteBuild.bandsMm.

The plots compared are the plots of cover.json. Mine is either a plan (R216, the CAD plan before Revit, so a plan fault and
a Revit fault are told apart) or the reader output of the new file (R215, what Revit built). Both and the answer key plan
are brought to one shape: link file, insert, rotation, mirror, FFL, the link parameters and the Generic, yard and ramp
floors with type, outline, top and corners. A gap is equal at 0, then within each band in mm, else worse. Each plot gets its
reasons, in this order: cut plot, mirror rule, estimate FFL, road level, other, and the worst plots are listed with them.

A past model may hold its levels above the design datum: the 1.4 key puts its survey point far below its Level 1 and its
FFL floors carry the design FFL as a parameter, the height above Level 1. With --key-model the datum is that model's own
survey elevation, it is proved on the FFL parameter of its floors and taken off every key level before the compare.
"""
import argparse
import json
import math
import sys
from collections import Counter
from pathlib import Path

import common
import floor_fit
import link_summary
import plan_compare
import plan_from_past
import plot_relations
import project_profile
import prototype_offsets
import ramp_levels

EQUAL_M = 1e-6
# Shared coordinates near 2.4e6 m carry float noise of about 5e-7 mm, so a band edge is held to a millionth of a mm
EDGE_MM = 1e-6
ROTATION_EQUAL_DEG = 0.01
# A ramp outline this far off has its front on another edge than the key, a plot is deeper than this
EDGE_M = 1.0


def band(gap_m, bands_mm):
    """equal, within<n>mm for the first band that holds the gap, else worse. bands_mm starts with 0 for equal."""
    if gap_m is None:
        return "missing"
    if gap_m <= EQUAL_M:
        return "equal"
    for limit in bands_mm[1:]:
        if gap_m * 1000.0 <= limit + EDGE_MM:
            return "within%gmm" % limit
    return "worse"


def key_datum(data, parameter):
    """The shared elevation of the internal origin of a key model, and on how many of its floors the parameter holds the top less it."""
    document = plan_from_past.host_of(data)
    datum = ramp_levels.offset_of(document)[2]
    proof = Counter()
    for floor in document.get("floors") or []:
        value = (floor.get("parameters") or {}).get(parameter)
        top = (floor.get("topElevationRange") or {}).get("max")
        if plan_from_past.is_number(value) and plan_from_past.is_number(top):
            proof["equal within 1 mm" if abs(top - datum - value) <= 0.001 else "differs"] += 1
    return datum, dict(proof)


def lower(view, datum):
    """The view with every level less the datum."""
    if view["insert"] and view["insert"][2] is not None:
        view["insert"] = view["insert"][:2] + [view["insert"][2] - datum]
    if view["ffl"] is not None:
        view["ffl"] -= datum
    for floor in view["floors"].values():
        if floor["z"] is not None:
            floor["z"] -= datum
        if floor["corners"]:
            floor["corners"] = [[c[0], c[1], c[2] - datum] for c in floor["corners"]]
    return view


def plan_view(entry):
    """One plot of a plan in the shape of the compare."""
    floors = {}
    for kind, floor in (entry.get("floors") or {}).items():
        corners = floor.get("corners", {}).get("value")
        floors[kind] = {"type": floor["type"]["value"], "shape": plan_compare.floor_shape(floor),
                        "z": floor["z"]["value"] if "z" in floor else (min(c[2] for c in corners) if corners else None), "corners": corners}
    insert = entry.get("insert", {}).get("value")
    return {"file": (entry.get("prototypeFile") or {}).get("value"), "insert": insert, "rotation": (entry.get("rotationDeg") or {}).get("value"),
            "mirrored": (entry.get("mirrored") or {}).get("value"), "ffl": (entry.get("levels") or {}).get("ffl", {}).get("value"),
            "parameters": {name: value["value"] for name, value in (entry.get("parameters") or {}).items()}, "floors": floors}


def model_views(data, floors_config, names):
    """R215. The plots of a model read back by the reader, by the plot number parameter of each link and the Plot value of each floor."""
    document = plan_from_past.host_of(data)
    offset = ramp_levels.offset_of(document)
    views, twice = {}, Counter()
    for link in document.get("links") or []:
        parameters = link.get("parameters") or {}
        plot = plan_from_past.plot_number(parameters.get(plan_from_past.PLOT_PARAMETER))
        if plot is None:
            continue
        twice[plot] += 1
        transform = link.get("transform") or {}
        origin = plan_from_past.shared_origin(link, offset)
        views[plot] = {"file": link.get("file"), "insert": origin, "rotation": plan_from_past.plan_rotation(transform["rotationDeg"]) if transform.get("rotationDeg") is not None else None,
                       "mirrored": transform.get("mirrored"), "ffl": None, "parameters": {name: parameters.get(name) for name in names if parameters.get(name) is not None}, "floors": {}}
    for floor in document.get("floors") or []:
        kind = plan_from_past.kind_of(floor, floors_config)
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if kind is None or plot is None or plot not in views:
            continue
        shape, _ = plot_relations.floor_shape(floor, offset)
        low, high, _ = plot_relations.top_range(floor, offset)
        corners = [[c[0] + offset[0], c[1] + offset[1], c[2] + offset[2]] for c in floor.get("corners") or []] or None
        views[plot]["floors"][kind] = {"type": floor.get("typeName"), "shape": shape, "z": low if kind == "ramp" else high, "corners": corners}
        twice[plot + " " + kind] += 1
    for plot, view in views.items():
        generic = view["floors"].get("generic")
        view["ffl"] = generic["z"] if generic else (view["insert"][2] if view["insert"] else None)
    return views, sorted(key for key, count in twice.items() if count > 1)


def front_gap(mine, theirs, frame_normal=None):
    """The largest level gap of the two lowest corners in plan order, the front pair of a ramp, each to its nearest other corner."""
    if not mine or not theirs:
        return None
    front = sorted(mine, key=lambda corner: corner[2])[:2]
    return max(abs(c[2] - min(theirs, key=lambda o: math.dist(c[:2], o[:2]))[2]) for c in front)


def compare_plot(mine, key, names, bands):
    """R215. The band of every field of one plot, with its gap, and the reasons of the plot."""
    rows = {}
    rows["file"] = ("equal" if (mine["file"] or "").lower() == (key["file"] or "").lower() else "differs", None)
    position = math.dist(mine["insert"][:2], key["insert"][:2]) if mine["insert"] and key["insert"] else None
    rows["position"] = (band(position, bands), position)
    gap = plan_compare.angle_gap(mine["rotation"], key["rotation"]) if mine["rotation"] is not None and key["rotation"] is not None else None
    rows["rotation"] = ("missing" if gap is None else "equal" if gap <= ROTATION_EQUAL_DEG else "worse", gap)
    rows["mirrored"] = ("missing" if None in (mine["mirrored"], key["mirrored"]) else "equal" if mine["mirrored"] == key["mirrored"] else "differs", None)
    z = abs(mine["insert"][2] - key["ffl"]) if mine["insert"] and mine["insert"][2] is not None and key["ffl"] is not None else None
    rows["zAgainstFfl"] = (band(z, bands), z)
    for name in names:
        a, b = mine["parameters"].get(name), key["parameters"].get(name)
        rows["parameter " + name] = ("missing" if a is None or b is None else "equal" if str(a) == str(b) else "differs", None)
    for kind in plan_from_past.FLOOR_KINDS:
        a, b = mine["floors"].get(kind), key["floors"].get(kind)
        if a is None or b is None:
            rows[kind + " presence"] = ("both missing" if a is None and b is None else "only in mine" if b is None else "only in key", None)
            continue
        rows[kind + " type"] = ("equal" if a["type"] == b["type"] else "differs", None)
        outline = floor_fit.outline_gap(a["shape"], b["shape"]) if a["shape"] is not None and b["shape"] is not None else None
        rows[kind + " outline"] = (band(outline, bands), outline)
        top = abs(a["z"] - b["z"]) if a["z"] is not None and b["z"] is not None else None
        rows[kind + " top"] = (band(top, bands), top)
        if a["corners"] and b["corners"]:
            corner = plan_compare.corner_gap(a["corners"], b["corners"])
            rows[kind + " corners"] = (band(corner, bands), corner)
            front = front_gap(a["corners"], b["corners"])
            rows[kind + " front corners"] = (band(front, bands), front)
    return rows


def reasons_of(plot, rows, apart, limit_m):
    """R215. Why a plot is off, in the order of the rule. other names the field when no earlier reason holds."""
    found = []
    outline = rows.get("generic outline", (None, None))[1]
    if plot in apart or (outline is not None and outline > limit_m):
        found.append("cut plot")
    # The hand of a link of another prototype file is drawn its own way, so only the same file says the mirror rule is off
    if rows["mirrored"][0] == "differs" and rows["file"][0] == "equal":
        found.append("mirror rule")
    if rows["zAgainstFfl"][1] is not None and rows["zAgainstFfl"][1] > limit_m:
        found.append("estimate FFL")
    front = rows.get("ramp front corners", (None, None))[1]
    if front is not None and front > limit_m:
        found.append("road level")
    ramp = rows.get("ramp outline", (None, None))[1]
    if ramp is not None and ramp > EDGE_M:
        found.append("other: the ramp of the key is on another edge of the plot")
    if rows["file"][0] == "differs":
        found.append("other: the key links another prototype file")
    if rows["rotation"][0] == "worse":
        found.append("other: rotation off by %s deg" % common.r3(rows["rotation"][1]))
    return found


def run(mine, key, plots, names, bands, apart):
    """R215, R216. Counts per field and band over the plots, and every plot with its largest gap and reasons."""
    table, per_plot, absent = {}, [], []
    limit = bands[-1] / 1000.0
    for plot in plots:
        if plot not in mine or plot not in key:
            absent.append({"plot": plot, "inMine": plot in mine, "inKey": plot in key})
            continue
        rows = compare_plot(mine[plot], key[plot], names, bands)
        for field, (result, _) in rows.items():
            table.setdefault(field, Counter())[result] += 1
        gaps = {field: gap for field, (_, gap) in rows.items() if gap is not None and field != "rotation"}
        # A link of another prototype file has its own origin, so its position says nothing about this plan and does not rank the plot
        ranked = {field: gap for field, gap in gaps.items() if not (field == "position" and rows["file"][0] == "differs")}
        worst = max(ranked, key=ranked.get) if ranked else None
        per_plot.append({"plot": plot, "worstField": worst, "worstMm": common.r3(gaps[worst] * 1000.0) if worst else None,
                         "gapsMm": {field: common.r3(gap * 1000.0) for field, gap in gaps.items()},
                         "bands": {field: result for field, (result, _) in rows.items()},
                         "rotationGapDeg": common.r3(rows["rotation"][1]) if rows["rotation"][1] is not None else None,
                         "reasons": reasons_of(plot, rows, apart, limit)})
    per_plot.sort(key=lambda row: -(row["worstMm"] or 0))
    return {"plots": len(plots), "compared": len(per_plot), "absent": absent, "bandsMm": bands,
            "counts": {field: dict(counts) for field, counts in table.items()},
            "reasonCounts": dict(Counter(reason.split(":")[0] for row in per_plot for reason in row["reasons"]).most_common()),
            "worst10": per_plot[:10], "byPlot": per_plot}


def main(argv=None):
    parser = argparse.ArgumentParser(description="The plots of the round against the answer key in bands (R215, R216).")
    parser.add_argument("cover", help="cover.json of key_cover.py")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--key", required=True, help="the answer key plan, plan_z4_past.json")
    parser.add_argument("--key-settings", required=True, help="the settings the answer key plan was built with, for its floor types")
    parser.add_argument("--key-model", help="the reader output of the key model, its survey elevation is taken off the key levels")
    parser.add_argument("--key-ffl-parameter", default="FFL", help="the floor parameter of the key model that holds the design FFL, the proof of the datum")
    parser.add_argument("--plan", help="mine is a plan, such as plan_z4_cad.json (R216)")
    parser.add_argument("--model", help="mine is the reader output of the new file (R215)")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    if bool(args.plan) == bool(args.model):
        parser.error("give --plan or --model")
    try:
        settings = project_profile.load_settings(args.settings)
        names = plan_from_past.load_job(settings)["parameters"]
        bands = settings.get("siteBuild")["bandsMm"]
        apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or [])
        cover = json.loads(Path(args.cover).read_text(encoding="utf-8"))
        key = {entry["plot"]: plan_view(entry) for entry in link_summary.load(args.key)["plots"]}
        datum, proof = (0.0, {})
        if args.key_model:
            datum, proof = key_datum(link_summary.load(args.key_model), args.key_ffl_parameter)
            key = {plot: lower(view, datum) for plot, view in key.items()}
        doubled = []
        if args.plan:
            mine = {entry["plot"]: plan_view(entry) for entry in link_summary.load(args.plan)["plots"]}
        else:
            mine, doubled = model_views(link_summary.load(args.model), plan_from_past.load_floors(settings), names)
        report = run(mine, key, cover["plots"], names, bands, apart)
        report["placedTwice"] = doubled
        report["keyDatum"] = {"m": common.r3(datum), "proof": proof}
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        common.write_json(out, report)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("z4_compare: " + str(error), file=sys.stderr)
        return 2
    print("plots %d, compared %d, absent %d, placed twice %d, key datum %s m %s" % (report["plots"], report["compared"], len(report["absent"]), len(doubled), report["keyDatum"]["m"], report["keyDatum"]["proof"]))
    for field, counts in report["counts"].items():
        print("%s %s" % (field, counts))
    print("reasons %s" % report["reasonCounts"])
    for row in report["worst10"]:
        print("worst %s %s %s mm, rotation %s deg, %s" % (row["plot"], row["worstField"], row["worstMm"], row["rotationGapDeg"], " | ".join(row["reasons"])))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
