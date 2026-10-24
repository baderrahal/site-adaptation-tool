"""Proof of the R67 level rule on past work. Reads the two schedule workbooks in place and compares.

Consecutive rows with the same FFL are one cluster. The unit and the entry level of each row go through
levels.compute, and its FFL and step count are compared with the sheet. Nothing is copied out of the workbooks,
and the report goes to a folder that git ignores. The rule is never changed to make a row match.

Grouping by the sheet FFL cannot fail on which plots belong together. With --analysis the same rule is fed with
the clusters of analysis.json instead, the way the pipeline will use it, and that run can fail on membership.
"""
import argparse
import json
import os
import re
import sys
from pathlib import Path

import common
import excel_reader
import full_run
import levels
import link_table
import project_profile

ROOT_VARIABLE = "SITE_ADAPTATION_INPUT"
# The sheet writes FFL to the millimetre in most rows, so a computed FFL within half a millimetre is a match
FFL_TOLERANCE = 0.0005
EPSILON = 1e-9
COLUMNS = {"plot": "plot no.", "unit": "unit", "ffl": "ffl", "ssl": "ssl", "entry": "el @ entry", "steps": "steps needed"}
DEFAULT_SHIFTS = Path(__file__).resolve().parent.parent / "config" / "plot_numbers.json"


def input_root(settings):
    """The input folder, from the environment variable when it is set, else from inputRoot in the project settings."""
    return Path(os.environ.get(ROOT_VARIABLE) or settings.get("inputRoot"))


def schedule_paths(root, settings):
    folder = Path(root) / full_run.XLSX_FOLDER
    return [folder / name for name in settings.get("schedules")]


def number_of(text):
    found = re.match(r"\s*(\d+)", str(text))
    return int(found.group(1)) if found else None


def read_schedule(path, sheet=full_run.DEFAULT_SHEET):
    """The plot rows of one schedule as dicts with plot, unit, ffl, ssl, entry and steps, and the sheet row number."""
    _, _, headers, records = excel_reader.read_sheet(path, sheet)
    names = {header.strip().rstrip("*").strip().lower(): header for header in headers}
    missing = [wanted for wanted in COLUMNS.values() if wanted not in names]
    if missing:
        raise project_profile.ProfileError(Path(path).name + " has no column " + ", ".join(missing))
    rows = []
    for record in records:
        for key in ("ffl", "ssl", "entry"):
            value = record[names[COLUMNS[key]]]
            if isinstance(value, bool) or not isinstance(value, (int, float)):
                raise project_profile.ProfileError(Path(path).name + " row " + str(record["sheetRow"]) + " has no number in column " + COLUMNS[key])
        rows.append({"sheetRow": record["sheetRow"], "plot": str(record[names[COLUMNS["plot"]]]), "unit": record[names[COLUMNS["unit"]]],
                     "ffl": record[names[COLUMNS["ffl"]]], "ssl": record[names[COLUMNS["ssl"]]], "entry": record[names[COLUMNS["entry"]]],
                     "steps": number_of(record[names[COLUMNS["steps"]]])})
    return rows


def clusters_by_ffl(rows):
    """Consecutive rows with the same FFL, to the fourth decimal, are one cluster."""
    groups = []
    for row in rows:
        if groups and round(groups[-1][-1]["ffl"], 4) == round(row["ffl"], 4):
            groups[-1].append(row)
        else:
            groups.append([row])
    return groups


def fflmatch(computed, sheet):
    return abs(computed - sheet) <= FFL_TOLERANCE + EPSILON


def compare_schedule(profile, rows, rule):
    groups = clusters_by_ffl(rows)
    clusters = [{"plots": [row["plot"] for row in group]} for group in groups]
    entries = {row["plot"]: {"unit": row["unit"], "entry": row["entry"]} for row in rows}
    computed = {row["plot"]: row for row in levels.compute(profile, clusters, entries)}
    found = []
    for row in rows:
        mine = computed[row["plot"]]
        found.append({
            "plot": row["plot"], "unit": row["unit"], "sheetRow": row["sheetRow"],
            "sheetFfl": row["ffl"], "computedFfl": mine["ffl"], "difference": common.r3(mine["ffl"] - row["ffl"]),
            "fflMatch": fflmatch(mine["ffl"], row["ffl"]),
            "sheetSteps": row["steps"], "computedSteps": mine["steps"], "stepsMatch": mine["steps"] == row["steps"],
            "sslMatch": fflmatch(mine["ssl"], row["ssl"]),
            # The count rule on its own, from the sheet's own FFL, so it is told apart from the base heights
            "countRuleAlone": levels.steps_for(row["ffl"] - row["entry"], rule) == row["steps"],
            "clusterSize": mine["size"],
        })
    return found, len(groups)


def shift_of(path, shifts):
    """The plot number shift of R28 for the neighbourhood named in the file name of a schedule."""
    code = link_table.neighbourhood_of(Path(path).name, shifts)
    if code is None or code not in shifts["shifts"]:
        raise project_profile.ProfileError("no plot number shift for the schedule " + Path(path).name)
    return shifts["shifts"][code]


def cad_cluster_report(profile, books, clusters):
    """The level rule fed with the clusters of analysis.json.

    books are (shift, rows) of each schedule. A sheet plot enters under its baseplan label, the official number minus
    the shift of its neighbourhood. A plot that is in no cluster is a cluster of one. Clusters that are only partly in
    the sheet are counted apart. For clusters wholly in the sheet it counts, by size, how many hold more than one FFL.
    """
    entries, sheet = {}, {}
    for shift, rows in books:
        for row in rows:
            if not row["plot"].isdecimal():
                raise project_profile.ProfileError("sheet plot " + row["plot"] + " is not a number")
            label = str(int(row["plot"]) - shift)
            if label in entries:
                raise project_profile.ProfileError("two sheet plots end on the baseplan label " + label)
            entries[label] = {"unit": row["unit"], "entry": row["entry"]}
            sheet[label] = row
    computed = {row["plot"]: row for row in levels.compute(profile, clusters, entries) if row["plot"] in entries}
    wholly, partly, held = {}, 0, set()
    for cluster in clusters:
        plots = [str(plot) for plot in cluster["plots"]]
        present = [plot for plot in plots if plot in entries]
        held.update(present)
        if present and len(present) < len(plots):
            partly += 1
        if present and len(present) == len(plots):
            counts = wholly.setdefault(str(len(plots)), {"clusters": 0, "withMoreThanOneSheetFfl": 0})
            counts["clusters"] += 1
            counts["withMoreThanOneSheetFfl"] += len({round(sheet[plot]["ffl"], 4) for plot in plots}) > 1
    return {
        "plots": len(entries),
        "inAnalysisClusters": len(held),
        "clustersPartlyInTheSheet": partly,
        "fflMatches": sum(1 for label, row in computed.items() if fflmatch(row["ffl"], sheet[label]["ffl"])),
        "stepsMatches": sum(1 for label, row in computed.items() if row["steps"] == sheet[label]["steps"]),
        "clustersWhollyInTheSheet": dict(sorted(wholly.items(), key=lambda item: int(item[0]))),
    }


def build_report(profile, paths, clusters=None, shifts=None):
    rule = levels.count_rule(profile)
    plots, cluster_count, books = [], 0, []
    for path in paths:
        rows = read_schedule(path)
        found, count = compare_schedule(profile, rows, rule)
        plots += found
        cluster_count += count
        if clusters is not None:
            books.append((shift_of(path, shifts), rows))
    ffl_bad = [plot for plot in plots if not plot["fflMatch"]]
    steps_bad = [plot for plot in plots if not plot["stepsMatch"]]
    summary = {
        "files": [Path(path).name for path in paths],
        "plots": len(plots),
        "clusters": cluster_count,
        "fflMatches": len(plots) - len(ffl_bad),
        "stepsMatches": len(plots) - len(steps_bad),
        "bothMatch": sum(1 for plot in plots if plot["fflMatch"] and plot["stepsMatch"]),
        "sslMatches": sum(1 for plot in plots if plot["sslMatch"]),
        "countRuleAloneMatches": sum(1 for plot in plots if plot["countRuleAlone"]),
        # The unit codes of the sheets are the codes of the profile, so a code the profile lacks is reported and never mapped
        "unitsNotInProfile": sorted({str(plot["unit"]) for plot in plots if plot["unit"] not in profile.get("typologies")}),
        "largestDifference": max((abs(plot["difference"]) for plot in plots), default=0),
        "baseHeightSource": profile.get("steps.base_height_source"),
        "cadClusters": None if clusters is None else cad_cluster_report(profile, books, clusters),
    }
    return {"summary": summary, "fflMismatches": [mismatch(plot) for plot in ffl_bad], "stepsMismatches": [mismatch(plot) for plot in steps_bad]}


def mismatch(plot):
    return {key: plot[key] for key in ("plot", "unit", "sheetRow", "sheetFfl", "computedFfl", "difference", "sheetSteps", "computedSteps", "clusterSize")}


def summary_line(report):
    summary = report["summary"]
    return ("FFL matches " + str(summary["fflMatches"]) + " of " + str(summary["plots"]) + ", steps matches " + str(summary["stepsMatches"]) + " of " + str(summary["plots"])
            + ", both " + str(summary["bothMatch"]) + ", SSL " + str(summary["sslMatches"]) + ", count rule alone " + str(summary["countRuleAloneMatches"]))


def cad_line(report):
    found = report["summary"]["cadClusters"]
    if found is None:
        return None
    split = ", ".join(size + " units " + str(counts["withMoreThanOneSheetFfl"]) + " of " + str(counts["clusters"]) for size, counts in found["clustersWhollyInTheSheet"].items())
    return ("CAD clusters: FFL matches " + str(found["fflMatches"]) + " of " + str(found["plots"]) + ", steps matches " + str(found["stepsMatches"]) + " of " + str(found["plots"])
            + ", clusters with more than one sheet FFL: " + split)


def read_clusters(path):
    try:
        data = json.loads(Path(path).read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise project_profile.ProfileError("analysis.json cannot be read: " + str(error)) from error
    if not isinstance(data, dict) or not isinstance(data.get("clusters"), list):
        raise project_profile.ProfileError("analysis.json has no clusters list")
    return data["clusters"]


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the R67 level rule with the two past schedules, read in place.")
    parser.add_argument("out_dir", help="where levels_proof.json is written, keep it outside git")
    parser.add_argument("--root", default=None, help="input folder, default is " + ROOT_VARIABLE + " or inputRoot in the project settings")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    parser.add_argument("--analysis", help="analysis.json of the cluster step, adds the run on the CAD clusters")
    parser.add_argument("--shifts", default=str(DEFAULT_SHIFTS), help="plot number shift config, used with --analysis")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        paths = schedule_paths(args.root or input_root(settings), settings)
        absent = [str(path) for path in paths if not path.is_file()]
        if absent:
            raise project_profile.ProfileError("schedule not found: " + ", ".join(absent))
        clusters = read_clusters(args.analysis) if args.analysis else None
        shifts = link_table.load_shifts(args.shifts) if args.analysis else None
        report = build_report(profile, paths, clusters, shifts)
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("levels_proof: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "levels_proof.json", report, wide_keys=frozenset({"sheetFfl", "computedFfl"}))
    print(summary_line(report))
    print("base height source: " + str(report["summary"]["baseHeightSource"]))
    print("FFL mismatches " + str(len(report["fflMismatches"])) + ", steps mismatches " + str(len(report["stepsMismatches"])) + ", largest FFL difference " + str(report["summary"]["largestDifference"]) + " m")
    if cad_line(report):
        print(cad_line(report))
    for found in report["fflMismatches"]:
        print("FFL plot " + found["plot"] + " " + str(found["unit"]) + " sheet " + str(found["sheetFfl"]) + " computed " + str(found["computedFfl"]) + " (" + str(found["difference"]) + ")")
    for found in report["stepsMismatches"]:
        print("steps plot " + found["plot"] + " " + str(found["unit"]) + " sheet " + str(found["sheetSteps"]) + " computed " + str(found["computedSteps"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
