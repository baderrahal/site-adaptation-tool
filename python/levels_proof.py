"""Proof of the R67 level rule on past work. Reads the two schedule workbooks in place and compares.

Consecutive rows with the same FFL are one cluster. The unit and the entry level of each row go through
levels.compute, and its FFL and step count are compared with the sheet. Nothing is copied out of the workbooks,
and the report goes to a folder that git ignores. The rule is never changed to make a row match.
"""
import argparse
import os
import re
import sys
from pathlib import Path

import common
import excel_reader
import full_run
import levels
import project_profile

ROOT_VARIABLE = "SITE_ADAPTATION_INPUT"
DEFAULT_ROOT = r"C:\02-Site Adaptation"
SCHEDULES = ("00101-PSN-IMC-XXX-SCH-TU-090000.xlsx", "00101-PSN-IMD-XXX-SCH-TU-090000.xlsx")
# The sheet writes FFL to the millimetre in most rows, so a computed FFL within half a millimetre is a match
FFL_TOLERANCE = 0.0005
EPSILON = 1e-9
COLUMNS = {"plot": "plot no.", "unit": "unit", "ffl": "ffl", "ssl": "ssl", "entry": "el @ entry", "steps": "steps needed"}


def input_root():
    return Path(os.environ.get(ROOT_VARIABLE) or DEFAULT_ROOT)


def schedule_paths(root):
    folder = Path(root) / full_run.XLSX_FOLDER
    return [folder / name for name in SCHEDULES]


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
            "fflMatch": abs(mine["ffl"] - row["ffl"]) <= FFL_TOLERANCE + EPSILON,
            "sheetSteps": row["steps"], "computedSteps": mine["steps"], "stepsMatch": mine["steps"] == row["steps"],
            "sslMatch": abs(mine["ssl"] - row["ssl"]) <= FFL_TOLERANCE + EPSILON,
            # The count rule on its own, from the sheet's own FFL, so it is told apart from the base heights
            "countRuleAlone": levels.steps_for(row["ffl"] - row["entry"], rule) == row["steps"],
            "clusterSize": mine["size"],
        })
    return found, len(groups)


def build_report(profile, paths):
    rule = levels.count_rule(profile)
    plots, clusters = [], 0
    for path in paths:
        found, count = compare_schedule(profile, read_schedule(path), rule)
        plots += found
        clusters += count
    ffl_bad = [plot for plot in plots if not plot["fflMatch"]]
    steps_bad = [plot for plot in plots if not plot["stepsMatch"]]
    sizes = {}
    for plot in plots:
        sizes[plot["clusterSize"]] = sizes.get(plot["clusterSize"], 0) + 1
    summary = {
        "files": [Path(path).name for path in paths],
        "plots": len(plots),
        "clusters": clusters,
        "fflMatches": len(plots) - len(ffl_bad),
        "stepsMatches": len(plots) - len(steps_bad),
        "bothMatch": sum(1 for plot in plots if plot["fflMatch"] and plot["stepsMatch"]),
        "sslMatches": sum(1 for plot in plots if plot["sslMatch"]),
        "countRuleAloneMatches": sum(1 for plot in plots if plot["countRuleAlone"]),
        # The unit codes of the sheets are the codes of the profile, so a code the profile lacks is reported and never mapped
        "unitsNotInProfile": sorted({str(plot["unit"]) for plot in plots if plot["unit"] not in profile.get("typologies")}),
        "largestDifference": max((abs(plot["difference"]) for plot in plots), default=0),
        "baseHeightSource": profile.get("steps.base_height_source"),
    }
    return {"summary": summary, "fflMismatches": [mismatch(plot) for plot in ffl_bad], "stepsMismatches": [mismatch(plot) for plot in steps_bad]}


def mismatch(plot):
    return {key: plot[key] for key in ("plot", "unit", "sheetRow", "sheetFfl", "computedFfl", "difference", "sheetSteps", "computedSteps", "clusterSize")}


def summary_line(report):
    summary = report["summary"]
    return ("FFL matches " + str(summary["fflMatches"]) + " of " + str(summary["plots"]) + ", steps matches " + str(summary["stepsMatches"]) + " of " + str(summary["plots"])
            + ", both " + str(summary["bothMatch"]) + ", SSL " + str(summary["sslMatches"]) + ", count rule alone " + str(summary["countRuleAloneMatches"]))


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the R67 level rule with the two past schedules, read in place.")
    parser.add_argument("out_dir", help="where levels_proof.json is written, keep it outside git")
    parser.add_argument("--root", default=None, help="input folder, default is " + ROOT_VARIABLE + " or " + DEFAULT_ROOT)
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    args = parser.parse_args(argv)
    paths = schedule_paths(args.root or input_root())
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        absent = [str(path) for path in paths if not path.is_file()]
        if absent:
            raise project_profile.ProfileError("schedule not found: " + ", ".join(absent))
        report = build_report(profile, paths)
    except (project_profile.ProfileError, ValueError, KeyError) as error:
        print("levels_proof: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "levels_proof.json", report, wide_keys=frozenset({"sheetFfl", "computedFfl"}))
    print(summary_line(report))
    print("base height source: " + str(report["summary"]["baseHeightSource"]))
    print("FFL mismatches " + str(len(report["fflMismatches"])) + ", steps mismatches " + str(len(report["stepsMismatches"])) + ", largest FFL difference " + str(report["summary"]["largestDifference"]) + " m")
    for found in report["fflMismatches"]:
        print("FFL plot " + found["plot"] + " " + str(found["unit"]) + " sheet " + str(found["sheetFfl"]) + " computed " + str(found["computedFfl"]) + " (" + str(found["difference"]) + ")")
    for found in report["stepsMismatches"]:
        print("steps plot " + found["plot"] + " " + str(found["unit"]) + " sheet " + str(found["sheetSteps"]) + " computed " + str(found["computedSteps"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
