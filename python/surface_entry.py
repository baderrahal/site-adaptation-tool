"""El @ Entry against every road surface version, Part X of round DATA-4 (R248, R249). A test, not a rule.

A surface version is every surface with a left and a right plot front level in plots.csv, the snapped road level of R157 first.
For the 747 schedule plots the prediction is the mean of the two front corner levels on that surface plus the ramp slope by
typology times the ramp depth of the profile (R170), against the schedule El @ Entry, banded per surface and by neighbourhood.
The same differences less their median per surface give the shape fit, which shows a surface that would fit once a constant
datum is taken off. The past key model of round Z4 on one neighbourhood is tested the same way, read two ways: its El @ Entry
parameter on the FFL floor of each plot (a design level, level_cross.key14_plots of R221), and the mean of the two ramp corners
farthest from the curb, the entrance side, less the datum of the key model (z4_compare.key_datum).
"""
import argparse
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_connect
import c3d_run
import data_book
import data_book_analysis
import data_book_retest
import level_cross
import link_summary
import plan_from_past
import project_profile
import wiring
import yard_rule
import z4_compare

number = data_book_analysis.number


def bands(differences, limits):
    found = Counter(data_book_analysis.band(d, limits) for d in differences)
    return {"within5mm": found.get("within5mm", 0), "within30mm": found.get("within30mm", 0), "worse": found.get("worse", 0)}


def summarise(items, limits):
    """Per surface: the bands, no level, the median difference and the shape fit, all plots and by neighbourhood."""
    by = defaultdict(list)
    for item in items:
        by[item["surface"]].append(item)
    result = {}
    for surface, members in by.items():
        diffs = [m["difference"] for m in members if m["difference"] is not None]
        median = statistics.median(diffs) if diffs else None
        groups = defaultdict(list)
        for m in members:
            groups[m["neighbourhood"] or "none"].append(m)
        result[surface] = {"plots": len(members), **bands(diffs, limits), "noLevel": len(members) - len(diffs),
                           "medianDifference": round(median, 4) if median is not None else None,
                           "shapeFit": bands([d - median for d in diffs], limits) if diffs else {},
                           "byNeighbourhood": {g: {**bands([m["difference"] for m in v if m["difference"] is not None], limits),
                                                   "noLevel": sum(1 for m in v if m["difference"] is None)} for g, v in sorted(groups.items())}}
    return result


def ranked(summary):
    """Surfaces best first by within 5 mm, then within 30 mm."""
    return sorted(summary, key=lambda s: (-summary[s]["within5mm"], -summary[s]["within30mm"], s))


def schedule_items(rows, profile, steep, names, limits):
    items = data_book_retest.entry_rows(rows, profile, steep, names, limits)
    return [i for i in items if i["variant"] == data_book_retest.STEEP]


def key_entry(ramp, curbs, datum):
    """The El @ Entry of a key plot: the mean level of the two ramp corners farthest from the curb, less the datum."""
    if not ramp or len(ramp) < 4 or curbs.tree is None:
        return None
    far = sorted(ramp, key=lambda corner: curbs.nearest(corner[0], corner[1])[0])[2:]
    return sum(c[2] for c in far[:2]) / 2 - datum


def one_typology(row):
    value = row.get("typologyRev9") or ""
    return value if value and "|" not in value else None


def ramp_entries(plan, curbs, datum):
    """The El @ Entry of each plot of the key plan from its ramp corners, by plot number."""
    found = {}
    for plot in plan["plots"]:
        ramp = (((plot.get("floors") or {}).get("ramp") or {}).get("corners") or {}).get("value")
        found[plot["plot"]] = key_entry(ramp, curbs, datum)
    return found


def key_items(entries, rows_by_number, profile, steep, names, nbh_of, wanted_nbh, limits):
    """One item per key plot of the wanted neighbourhood and surface with the prediction less the key El @ Entry."""
    items = []
    for number_, entry in sorted(entries.items()):
        if nbh_of.get(number_) != wanted_nbh:
            continue
        row = rows_by_number.get(number_)
        unit = one_typology(row) if row else None
        for name in names:
            item = {"plot": number_, "neighbourhood": wanted_nbh, "unit": unit, "surface": name, "difference": None}
            if row is not None and entry is not None and unit is not None:
                left, right = data_book_retest.corner_levels(row, name)
                if left is not None and right is not None:
                    slope = data_book_retest.slopes(profile, steep, unit)[data_book_retest.STEEP]
                    item["difference"] = round((left + right) / 2 + slope / 100 * data_book_retest.ramp_depth(profile, unit) - entry, 4)
            items.append(item)
    return items


def run(config, settings_path):
    book, connect = config["dataBook"], config["connect"]
    settings = book["surfaceEntry"]
    root = Path(book["root"])
    rows = data_book_analysis.read_csv(root / "plots.csv")
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings(settings_path)))
    steep = set(book["retest"]["steepUnits"])
    limits = book["analysis"]["bandsM"]
    names = data_book_retest.surface_names(rows)
    schedule = summarise(schedule_items(rows, profile, steep, names, limits), limits)
    report = {"surfaces": len(names), "schedule": schedule, "scheduleRanked": ranked(schedule)}

    key = settings["key"]
    plan = json.loads(Path(key["plan"]).read_text(encoding="utf-8"))
    datum, proof = z4_compare.key_datum(link_summary.load(key["model"]), key["fflParameter"])
    rows_by_number = {}
    seen = Counter()
    for row in rows:
        numbers = [n for n in (row["locationPlanNumber"] or "").split("|") if n]
        if len(numbers) == 1:
            seen[numbers[0]] += 1
            rows_by_number[numbers[0]] = row
    rows_by_number = {n: r for n, r in rows_by_number.items() if seen[n] == 1}
    labels = data_book_analysis.read_csv(root / book["locationPlan"]["outFolder"] / "labels.csv")
    nbh_of = {l["number"]: l["nbh"] for l in labels if l.get("nbh")}
    _, road_folder = data_book.folder_of(book, book["join"]["curb"]["drawing"], book["join"]["fallbackOutputs"])
    curbs = c3d_connect.LineSet(data_book.lines_of(road_folder, book["join"]["curb"]["layers"]))
    site = project_profile.load_settings(settings_path)
    faces = yard_rule.face_shapes(link_summary.load(config["connect"]["baseplanPlots"])["plots"], 0)[0]
    numbers, _ = plan_from_past.location_numbers(root / book["locationPlan"]["outFolder"] / "labels.csv")
    floors = plan_from_past.load_floors(project_profile.load_settings(key["settings"]))
    key14, _, _ = level_cross.key14_plots(link_summary.load(key["model"]), floors, faces, numbers, site.get("siteBuild")["key14"])
    readings = {"parameter": {n: data_book_analysis.number(v.get("entry")) for n, v in key14.items()}, "rampCorners": ramp_entries(plan, curbs, datum)}
    report["key"] = {"neighbourhood": key["neighbourhood"], "datumProof": proof, "readings": {}}
    for reading, entries in readings.items():
        items = key_items(entries, rows_by_number, profile, steep, names, nbh_of, key["neighbourhood"], limits)
        key_summary = summarise(items, limits)
        report["key"]["readings"][reading] = {"plots": len({i["plot"] for i in items}), "surfaces": key_summary, "ranked": ranked(key_summary)}
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "surface_entry.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="El @ Entry of the schedule plots and the past key against every road surface version (R248, R249)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config), args.settings)
    top = report["scheduleRanked"][:5]
    print(json.dumps({"schedule": {s: {k: report["schedule"][s][k] for k in ("within5mm", "within30mm", "worse", "noLevel", "medianDifference", "shapeFit")} for s in top},
                      "key": {reading: {s: {k: entry["surfaces"][s][k] for k in ("within5mm", "within30mm", "worse", "noLevel", "medianDifference", "shapeFit")}
                                        for s in entry["ranked"][:3]} for reading, entry in report["key"]["readings"].items()}}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
