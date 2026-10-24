"""R221 to R223. The levels of two past jobs on the same plots, and the road surface that fits the ramp fronts of one of them.

R221: the 1.4 key holds the plots of the IMC key too. On the plots both hold, by official number, its FFL, El @ Entry,
ramp corners and Generic top are compared with the IMC plan and its schedule. The 1.4 levels are taken down by the survey
elevation of the 1.4 model (R215), and the El @ Entry and FFL parameters of its floors, named by siteBuild.key14, are read
as they are, they hold the design level. The gaps are counted in the bands of siteBuild.bandsMm per field and per unit.
R222: the front corners of the 1.4 ramps, the two lowest, on the plots of a neighbourhood, are compared with every LandXML
surface of the snapshot, snapped as R157, and each surface is scored by how many fronts it covers and how near it lies.
R223: each gap is also taken per unit of the IMC plan, as the median of the signed gaps, so a gap that only some units show stands out. No level rule is coded from any of it, the round log recommends.
"""
import argparse
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_connect
import common
import landxml_reader
import link_summary
import plan_from_past
import plot_relations
import project_profile
import ramp_levels
import surface_lookup
import yard_rule
import z4_compare


def key14_plots(data, floors_config, faces, numbers, names):
    """The FFL, El @ Entry, Generic top and ramp corners of each plot of the 1.4 key, by official number, in design levels."""
    data, _, report = plan_from_past.names_dialect(data, floors_config, faces, numbers)
    document = plan_from_past.host_of(data)
    offset = ramp_levels.offset_of(document)
    datum = offset[2]
    found = defaultdict(dict)
    for floor in document.get("floors") or []:
        kind = plan_from_past.kind_of(floor, floors_config)
        plot = floor.get("plotNumber") if floor.get("modelPlot") else None
        if kind is None or plot is None:
            continue
        parameters = floor.get("parameters") or {}
        item = found[plot]
        if kind == "generic":
            low, high, _ = plot_relations.top_range(floor, offset)
            item["genericTop"] = high - datum if high is not None else None
            item["ffl"] = parameters.get(names["fflParameter"])
            item["entry"] = parameters.get(names["entryParameter"])
            item["unit"] = floor["modelPlot"].rpartition("-")[0]
        elif kind == "ramp" and floor.get("corners"):
            item["corners"] = [[c[0] + offset[0], c[1] + offset[1], c[2]] for c in floor["corners"]]
    return dict(found), datum, report


def imc_plots(plan):
    """The levels and floors of the IMC plan by plot, with the schedule values the plan took (source pastJob)."""
    found = {}
    for entry in plan["plots"]:
        levels = entry.get("levels") or {}
        floors = entry.get("floors") or {}
        found[entry["plot"]] = {"ffl": levels.get("ffl", {}).get("value"), "entry": levels.get("entry", {}).get("value"),
                                "genericTop": floors.get("generic", {}).get("z", {}).get("value"),
                                "corners": floors.get("ramp", {}).get("corners", {}).get("value"), "unit": entry.get("unit", {}).get("value")}
    return found


def corner_pairs(mine, theirs):
    """Each corner of mine with the nearest corner of theirs in plan: the plan distance and the level gap."""
    pairs = []
    for corner in mine:
        other = min(theirs, key=lambda item: (item[0] - corner[0]) ** 2 + (item[1] - corner[1]) ** 2)
        pairs.append((((other[0] - corner[0]) ** 2 + (other[1] - corner[1]) ** 2) ** 0.5, corner[2] - other[2]))
    return pairs


def stats(values):
    if not values:
        return None
    ordered = sorted(values)
    return {"n": len(values), "median": common.r3(statistics.median(values)), "min": common.r3(ordered[0]), "max": common.r3(ordered[-1])}


def cross(key14, imc, bands):
    """R221. Per field the bands of the gap (1.4 less IMC) over the plots both hold, the signed gaps and the gap per unit."""
    fields = ("ffl", "entry", "genericTop")
    table, signed, by_unit, corners, xy = {}, defaultdict(list), defaultdict(lambda: defaultdict(list)), Counter(), []
    by_plot = {}
    common_plots = sorted(set(key14) & set(imc), key=link_summary.sort_key)
    for plot in common_plots:
        a, b = key14[plot], imc[plot]
        for field in fields:
            if plan_from_past.is_number(a.get(field)) and plan_from_past.is_number(b.get(field)):
                gap = a[field] - b[field]
                by_plot.setdefault(plot, {})[field + "GapMm"] = common.r3(gap * 1000.0)
                table.setdefault(field, Counter())[z4_compare.band(abs(gap), bands)] += 1
                signed[field].append(gap * 1000.0)
                by_unit[field][b.get("unit") or "none"].append(gap * 1000.0)
        if a.get("corners") and b.get("corners"):
            pairs = corner_pairs(a["corners"], b["corners"])
            xy.append(max(distance for distance, _ in pairs) * 1000.0)
            worst = max(abs(gap) for _, gap in pairs)
            corners[z4_compare.band(worst, bands)] += 1
            signed["rampCorners"].extend(gap * 1000.0 for _, gap in pairs)
    return {"plots": len(common_plots), "bands": {field: dict(counts) for field, counts in table.items()}, "rampCornerBands": dict(corners),
            "signedMm": {field: stats(values) for field, values in signed.items()}, "rampCornerPlanGapMm": stats(xy),
            "byUnitMedianMm": {field: {unit: common.r3(statistics.median(values)) for unit, values in sorted(units.items())} for field, units in by_unit.items()},
            "byPlot": by_plot}


def fronts_of(key14, plots):
    """The two lowest corners of each 1.4 ramp of the plots, in shared plan and design level."""
    found = {}
    for plot in plots:
        corners = key14.get(plot, {}).get("corners")
        if corners:
            found[plot] = sorted(corners, key=lambda corner: corner[2])[:2]
    return found


def surface_fit(fronts, surfaces, snap, bands):
    """R222. Every surface against every front corner: covered, signed gap median, and the bands of the gap."""
    rows = []
    for name, surface in surfaces:
        gaps = []
        for corners in fronts.values():
            for corner in corners:
                z = c3d_connect.snap_z(surface, corner[0], corner[1], snap)
                if z is not None:
                    gaps.append(corner[2] - z)
        count = Counter(z4_compare.band(abs(gap), bands) for gap in gaps)
        rows.append({"surface": name, "covered": len(gaps), "of": sum(len(c) for c in fronts.values()),
                     "medianMm": common.r3(statistics.median(gaps) * 1000.0) if gaps else None, "bands": dict(count)})
    rows.sort(key=lambda row: (-row["covered"], abs(row["medianMm"]) if row["medianMm"] is not None else 1e9))
    return rows


def load_surfaces(folder, scratch):
    surfaces = []
    for path in sorted(Path(folder).glob("*.xml")):
        out = Path(scratch) / path.stem
        summary = landxml_reader.read_landxml(path, out)
        for entry in summary["surfaces"]:
            surfaces.append((path.stem, surface_lookup.Surface.from_dir(out / entry["folder"])))
    return surfaces


def main(argv=None):
    parser = argparse.ArgumentParser(description="The levels of two past jobs on the same plots and the road that fits the ramp fronts (R221 to R223).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--key14", required=True, help="the reader output of the 1.4 key")
    parser.add_argument("--key14-settings", required=True, help="the settings its floor types are named in")
    parser.add_argument("--imc-plan", required=True, help="plan_imc_past.json")
    parser.add_argument("--labels", required=True, help="location_plan/labels.csv of the snapshot")
    parser.add_argument("--baseplan", required=True, help="plots.json of the baseplan in the snapshot")
    parser.add_argument("--landxml", required=True, help="the landxml folder of the snapshot")
    parser.add_argument("--nbh", required=True, help="the neighbourhood whose 1.4 ramp fronts are fitted to the surfaces")
    parser.add_argument("--scratch", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        block = settings.get("siteBuild")
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        numbers, _ = plan_from_past.location_numbers(args.labels)
        faces, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
        floors14 = plan_from_past.load_floors(project_profile.load_settings(args.key14_settings))
        key14, datum, dialect = key14_plots(link_summary.load(args.key14), floors14, faces, numbers, block["key14"])
        imc = imc_plots(link_summary.load(args.imc_plan))
        report = {"key14Plots": len(key14), "datumFound": datum is not None, "dialect": dialect, "cross": cross(key14, imc, block["bandsMm"])}
        import csv
        with open(args.labels, encoding="utf-8") as handle:
            nbh_plots = {row["number"] for row in csv.DictReader(handle) if row.get("nbh") == args.nbh}
        fronts = fronts_of(key14, sorted(nbh_plots & set(key14), key=link_summary.sort_key))
        report["fronts"] = {"nbh": args.nbh, "ramps": len(fronts)}
        report["surfaces"] = surface_fit(fronts, load_surfaces(args.landxml, args.scratch), block["snapM"], block["bandsMm"])
        common.write_json(out, report)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("level_cross: " + str(error), file=sys.stderr)
        return 2
    cross_report = report["cross"]
    print("plots in both keys %d" % cross_report["plots"])
    for field, counts in cross_report["bands"].items():
        print("%s %s signed %s" % (field, counts, cross_report["signedMm"][field]))
    print("ramp corners %s signed %s, plan gap of the corners %s" % (cross_report["rampCornerBands"], cross_report["signedMm"].get("rampCorners"), cross_report["rampCornerPlanGapMm"]))
    for field, units in cross_report["byUnitMedianMm"].items():
        print("%s by unit, median mm %s" % (field, units))
    print("ramp fronts of %s: %d" % (args.nbh, report["fronts"]["ramps"]))
    for row in report["surfaces"]:
        print("surface %s covers %d of %d, median %s mm, %s" % (row["surface"], row["covered"], row["of"], row["medianMm"], row["bands"]))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
