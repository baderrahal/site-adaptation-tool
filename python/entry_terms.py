"""El @ Entry built from its terms, Part E of round DATA-3 (R193). A test, not a rule.

For each of the 747 schedule plots the prediction is the road level at the curb point of the front edge (R94, snapOrder of
R157), plus the curb up-stand, plus the sidewalk cross fall times the sidewalk width of plots.csv, plus the ramp slope by
typology times the ramp depth of the profile. The up-stand and the cross fall are the modal values python/cross_sections.py
measured, never typed in here. Each term is also left out one at a time, and the R170 variant on the front corners is run
beside them as the baseline. The bands are counted per variant, by Rev 6 unit and by neighbourhood.
"""
import argparse
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_connect
import c3d_run
import cross_sections
import data_book
import data_book_analysis
import data_book_retest
import entry_levels
import link_summary
import project_profile
import surface_lookup
import wiring
import yard_rule

TERMS = ("upstand", "sidewalk", "ramp")


def measured(cross, settings):
    """The modal up-stand and sidewalk cross fall of the curbs with a sidewalk of at least minSidewalkM behind them."""
    rows = [r for r in cross["rows"] if r["sidewalkWidthM"] is not None and r["sidewalkWidthM"] >= settings["minSidewalkM"]]
    upstand = Counter(r["upstandM"] for r in rows).most_common(1)
    fall = Counter(r["sidewalkCrossFallPercent"] for r in rows if r["crossFallSource"] == "text").most_common(1)
    return {"curbs": len(rows), "upstandM": upstand[0][0] if upstand else None, "crossFallPercent": fall[0][0] if fall else None}


def predict(road, upstand, fall_percent, sidewalk, slope_percent, depth, leave_out=None):
    """The predicted El @ Entry with one term left out, or None when a term it needs has no value."""
    if road is None:
        return None
    total = road
    if leave_out != "upstand":
        total += upstand
    if leave_out != "sidewalk":
        if sidewalk is None:
            return None
        total += fall_percent / 100 * sidewalk
    if leave_out != "ramp":
        total += slope_percent / 100 * depth
    return total


def variants(row, road, values, profile, steep, bands):
    """One item per variant for one plot: full, each term left out and the corner baseline of R170."""
    entry = data_book_analysis.number(row["pastEntry"])
    unit = row["unitRev6"]
    depth = data_book_retest.ramp_depth(profile, unit)
    slope = data_book_retest.slopes(profile, steep, unit)["byTypology"]
    sidewalk = data_book_analysis.number(row["sidewalkWidthM"])
    left, right = data_book_analysis.number(row["roadLevelLeft"]), data_book_analysis.number(row["roadLevelRight"])
    found = {"full": predict(road, values["upstandM"], values["crossFallPercent"], sidewalk, slope, depth)}
    for term in TERMS:
        found["no" + term.capitalize()] = predict(road, values["upstandM"], values["crossFallPercent"], sidewalk, slope, depth, term)
    found["cornerByTypology"] = (left + right) / 2 + slope / 100 * depth if left is not None and right is not None else None
    items = []
    for name, value in found.items():
        difference = None if value is None else value - entry
        items.append({"plot": row["officialNumber"], "neighbourhood": row["neighbourhood"], "unit": unit, "variant": name,
                      "difference": None if difference is None else round(difference, 4),
                      "band": "noLevel" if difference is None else data_book_analysis.band(difference, bands)})
    return items


def summary(items):
    def count(members):
        diffs = [m["difference"] for m in members if m["difference"] is not None]
        found = Counter(m["band"] for m in members)
        return {"within5mm": found.get("within5mm", 0), "within30mm": found.get("within30mm", 0), "worse": found.get("worse", 0),
                "noLevel": found.get("noLevel", 0), "medianDifference": round(statistics.median(diffs), 4) if diffs else None}
    by = defaultdict(list)
    for item in items:
        by[item["variant"]].append(item)
    result = {}
    for name, members in by.items():
        units, groups = defaultdict(list), defaultdict(list)
        for m in members:
            units[m["unit"]].append(m)
            groups[m["neighbourhood"] or "none"].append(m)
        result[name] = {"all": count(members), "byUnit": {k: count(v) for k, v in sorted(units.items())},
                        "byNeighbourhood": {k: count(v) for k, v in sorted(groups.items())}}
    return result


def run(config, settings):
    book, connect = config["dataBook"], config["connect"]
    entry = book["entryTerms"]
    root = Path(book["root"])
    rows = [r for r in data_book_analysis.read_csv(root / "plots.csv")
            if data_book_analysis.number(r["pastEntry"]) is not None and (r["unitRev6Source"] or "").startswith("schedule")]
    cross = json.loads((root / book["crossSections"]["outFolder"] / "cross_sections.json").read_text(encoding="utf-8"))
    values = measured(cross, entry)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    plots = link_summary.load(connect["baseplanPlots"])["plots"]
    faces = yard_rule.face_shapes(plots, 0)[0]
    _, road_folder = data_book.folder_of(book, book["join"]["curb"]["drawing"], book["join"]["fallbackOutputs"])
    curbs = c3d_connect.LineSet(data_book.lines_of(road_folder, book["join"]["curb"]["layers"]))
    surfaces = {name: surface_lookup.load_surface(connect["landxmlDir"], name) for name in connect["surfaces"]}
    order = [name for name in book["roadSurfaceOrder"] if name in surfaces]
    items, curb_found, plot_line = [], Counter(), []
    for row in rows:
        face = faces.get(row["baseplanLabel"])
        front = entry_levels.front_edge(face, curbs.tree, curbs.lines) if face is not None else None
        road = None
        if front is None:
            curb_found["noFront"] += 1
        else:
            levels = wiring.level_at(surfaces, order, front["curb"][0], front["curb"][1], "snapOrder", connect["snapM"], 0, 0)
            road = levels[0] if levels else None
            curb_found["curbLevel" if road is not None else "noCurbLevel"] += 1
        items.extend(variants(row, road, values, profile, book["retest"]["steepUnits"], book["analysis"]["bandsM"]))
        left, right = data_book_analysis.number(row["roadLevelLeft"]), data_book_analysis.number(row["roadLevelRight"])
        walk = predict(road, values["upstandM"], values["crossFallPercent"], data_book_analysis.number(row["sidewalkWidthM"]), 0, 0, "ramp")
        if walk is not None and left is not None and right is not None:
            plot_line.append(walk - (left + right) / 2)
    sidewalks = [data_book_analysis.number(r["sidewalkWidthM"]) for r in rows if data_book_analysis.number(r["sidewalkWidthM"]) is not None]
    report = {"plots": len(rows), "measured": values, "curbPoints": dict(curb_found), "variants": summary(items),
              "sidewalkWidthM": {"plots": len(sidewalks), "median": round(statistics.median(sidewalks), 3) if sidewalks else None},
              "curbTermsAgainstPlotLine": {"plots": len(plot_line), "medianM": round(statistics.median(plot_line), 4) if plot_line else None,
                                           "bands": dict(Counter(data_book_analysis.band(d, book["analysis"]["bandsM"]) for d in plot_line))},
              "data2ByTypology": entry["data2ByTypology"],
              "profileValues": cross_sections.profile_values(profile.values, book["crossSections"]["profileWords"])}
    out = root / entry["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "entry_terms.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    data_book_retest.write_rows(out / "entry_terms.csv", items, list(items[0]) if items else ["plot"])
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="El @ Entry from the road at the curb, the up-stand, the sidewalk and the ramp (R193)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config), project_profile.load_settings(args.settings))
    print(json.dumps({"measured": report["measured"], "curbPoints": report["curbPoints"],
                      "variants": {k: v["all"] for k, v in report["variants"].items()}}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
