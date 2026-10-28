"""R364. The second route of the level Excel the worker command levelExcel writes in C# (CadReader LevelWorkbook): the xlsx read with
openpyxl, every label and its order held against the words of docs/addin-mock.md item 5 and docs/design/07-mock.md 7.5, read from those
files and never from the C#, and every cell held against the plan, its override list, the step clusters and plots.csv, worked out again
here with json and csv only.

- labels: the sheets, the ten columns, the top line with its formulas as the filled example of 7.5 words them, the number format of each
  column by the decimals 7.5 gives
- Levels: Plot No the key in column A, one row per written plot in the order of the plan then one per plot not written, no blank row and
  no merged range under the top line, each value equal to the plan value, the cluster of the step clusters, the source Rule or Override
  with the reason of the one entry of the list of the sources and models of the plan
- Clusters, Overrides used: each row worked out again from the plan, the step clusters and the list
- Settings: the run id, the build id of the plan, the SHA-256 of the plan file bytes by hashlib, the SHA-256 of the rules and the list,
  and the editable columns, which are the only headers with a fill (Bader, 2026-10-28)

With no --xlsx the plan side runs alone on real plans: what the Excel of a plan must hold, with the gaps a C# run would meet. Counts, plot
numbers and row numbers only, no level, to --out outside the repo.
"""
import argparse
import csv
import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path

import project_profile

ROOT = Path(__file__).resolve().parent.parent
LEVEL_FIELDS = ("entryM", "heightM", "steps", "rampFrontM")
# Bader, 2026-10-28: the columns the team may edit for the button Update from report of Phase 5f
EDITABLE = ("El @ Entry", "FFL", "Steps")
SETTINGS = ("Run id", "Profile", "Rules SHA-256", "Override list SHA-256", "Road surface order", "Pass limit", "Run time", "Editable columns",
            "Plan build id", "Plan SHA-256")
CLUSTER_COLUMNS = ("Cluster", "Plots", "Unit sequence", "Split", "FFL")
OVERRIDE_COLUMNS = ("Plot No", "Field", "Gap", "Reason", "Source", "Model")
PLAN_KEYS = {"Road at front": "roadAtFrontM", "El @ Entry": "entry", "FFL": "ffl", "SSL": "ssl", "Height": "height", "Steps": "steps"}
PIECE_RULE = "oneFflPerPieceAtTheHighestEntry"


def section(text, start, end):
    """The lines of a Markdown file from the heading start up to the heading end."""
    lines = text.splitlines()
    first = next(index for index, line in enumerate(lines) if line.startswith(start))
    last = next((index for index in range(first + 1, len(lines)) if lines[index].startswith(end)), len(lines))
    return "\n".join(lines[first:last])


def listed(text):
    """The names of a list of the mock, a part in brackets dropped, split on its commas."""
    return [part.strip() for part in re.sub(r"\s*\([^)]*\)", "", text).split(",") if part.strip()]


def mock_labels(root=ROOT):
    """The labels of the level Excel by the words of the two mock files, each read on its own and held against the other."""
    item = next(line for line in (root / "docs" / "addin-mock.md").read_text(encoding="utf-8").splitlines() if line.startswith("5. Level Excel"))
    part = section((root / "docs" / "design" / "07-mock.md").read_text(encoding="utf-8"), "### 7.5", "### 7.6")
    found = {
        "file": re.search(r"file (\S+\.xlsx)", item).group(1),
        "columns": listed(re.search(r"Columns in this order: (.*?)\. Sheets:", item).group(1)),
        "sheets": listed(re.search(r"Sheets: (.*?)\.\s*$", item).group(1)),
        "topLine": re.search(r"A top line with (.*?)\.", item).group(1),
    }
    table = next(line for line in part.splitlines() if line.startswith("| Plot No"))
    design = {
        "file": re.search(r"file (\S+\.xlsx)", part).group(1),
        "columns": listed(re.search(r"^Columns in this order: (.*?)\.$", part, re.M).group(1)),
        "table": [cell.strip() for cell in table.strip("|").split("|")],
        "sheets": listed(re.search(r"^Sheets: (.*?)\.$", part, re.M).group(1)),
        "topLine": re.search(r"^A top line with (.*?)\.$", part, re.M).group(1),
    }
    disagree = [key for key in ("file", "columns", "sheets", "topLine") if found[key] != design[key]]
    if design["table"] != found["columns"]:
        disagree.append("table")
    if disagree:
        raise ValueError("docs/addin-mock.md item 5 and 7.5 of docs/design/07-mock.md differ on " + ", ".join(disagree))
    decimals = {}
    match = re.search(r"(Road at front[^.]*?) with (\d) decimals as R2 keeps them, (\w+ and \w+) with (\d)", part)
    for names, count in ((match.group(1), match.group(2)), (match.group(3), match.group(4))):
        for name in re.split(r", | and ", names):
            decimals[name.strip()] = int(count)
    filled = re.search(r'Filled: "(NBH .*?)"', part).group(1)
    found.update(decimals=decimals, formulas=filled.split("formulas: ", 1)[1])
    return found


def number_format(decimals):
    return "0" if decimals == 0 else "0." + "0" * decimals


def plan_build_id(plan, run_id):
    """The run id of the run stamp a C# build writes into its plan, else the run id of the Excel, as the Settings sheet writes it."""
    stamp = plan.get("run")
    return stamp["runId"] if isinstance(stamp, dict) and isinstance(stamp.get("runId"), str) else run_id


def load_inputs(plan_path, overrides_path, clusters_path, plots_csv, rules_path, config_path):
    read = lambda path: json.loads(Path(path).read_text(encoding="utf-8"))
    with open(plots_csv, encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    return {"plan": read(plan_path), "planBytes": Path(plan_path).read_bytes(), "overrides": read(overrides_path), "clusters": read(clusters_path),
            "zones": {row.get("locationPlanNumber"): row.get("zone") for row in rows if row.get("locationPlanNumber")},
            "rules": read(rules_path), "config": read(config_path),
            "sha": {name: hashlib.sha256(Path(path).read_bytes()).hexdigest() for name, path in (("rules", rules_path), ("overrides", overrides_path))}}


def expected(inputs, profile_name, mock):
    """The cells the level Excel of the plan must hold, worked out here from the inputs, with the gaps found on the way."""
    plan, gaps = inputs["plan"], Counter()
    rules = (plan.get("summary") or {}).get("rules") or {}
    sources, models = set(rules.get("overrideSource") or []), set(rules.get("overrideModel") or [])
    entries = {}
    for item in inputs["overrides"].get("overrides") or []:
        if item.get("field") in LEVEL_FIELDS and item.get("source") in sources and (not models or item.get("model") in models):
            entries.setdefault((str(item["plot"]), item["field"]), []).append(item)
    cluster_of = {}
    for item in (inputs["clusters"].get("plots") or {}).values():
        plots = (item.get("written") or item.get("raw") or {}).get("plots") or []
        if len(plots) >= 2:
            for plot in plots:
                cluster_of[plot] = plots
    written = plan.get("plots") or []
    not_written = [item for item in plan.get("missing") or [] if item.get("written") is False]
    levels, used = [], []
    for plot in written:
        number = plot["plot"]
        applied = [field for field in (plot.get("overrides") or {}).get("applied") or [] if field in LEVEL_FIELDS]
        parts = []
        for field in applied:
            found = entries.get((number, field)) or []
            gaps["level overrides applied"] += 1
            gaps["level overrides with %s entries" % ("no" if not found else "one" if len(found) == 1 else "several")] += 1
            if not found:
                parts.append("%s (no entry in the override list)" % field)
                continue
            entry = found[0]
            parts.append("%s (%s)" % (field, entry.get("reason") or ""))
            value = entry.get("value")
            gap = value.get("gapToRuleM") if isinstance(value, dict) else value
            used.append([number, field, gap if isinstance(gap, (int, float)) and not isinstance(gap, bool) else json.dumps(value), entry.get("reason"), entry.get("source"), entry.get("model")])
        row = [number, (plot.get("unit") or {}).get("value"), "-".join(cluster_of[number]) if number in cluster_of else "none"]
        for column in ("Road at front", "El @ Entry", "FFL", "SSL", "Height", "Steps"):
            value = ((plot.get("levels") or {}).get(PLAN_KEYS[column]) or {}).get("value")
            if value is None:
                gaps["written plots with no " + PLAN_KEYS[column]] += 1
            elif column != "Steps" and round(value, mock["decimals"][column]) != value:
                gaps["values with more decimals than 7.5 gives, " + column] += 1
            row.append(value)
        row.append("Override " + ", ".join(parts) if parts else "Rule")
        levels.append(row)
    for item in not_written:
        reasons = ", ".join("%s (%s)" % (found["key"], found["why"]) for found in item.get("missing") or [])
        levels.append([item["plot"], None, "-".join(cluster_of[item["plot"]]) if item["plot"] in cluster_of else "none", None, None, None, None, None, None, "Not written: " + reasons])
    numbers = [row[0] for row in levels]
    zones = sorted({inputs["zones"].get(number) for number in numbers if inputs["zones"].get(number)})
    gaps["plots with no zone in plots.csv"] = sum(1 for number in numbers if not inputs["zones"].get(number))
    profile = inputs["rules"]["levelProfiles"][profile_name]
    key = profile.get("keyRules") or {}
    rule = (key.get("ffl") or {}).get("rule") or profile.get("height")
    by_number = {plot["plot"]: plot for plot in written}
    split = key.get("levelSplit") or {}
    table = split.get("table") or {} if rule == PIECE_RULE and split.get("rule") == "units" else {}
    pieces, seen = [], set()
    for plot in written:
        cluster = cluster_of.get(plot["plot"])
        if not cluster or tuple(cluster) in seen:
            continue
        seen.add(tuple(cluster))
        sequence = " ".join((by_number[member].get("unit") or {}).get("value") if member in by_number else "not written" for member in cluster)
        run, top = [], None
        for member in [member for member in cluster if member in by_number]:
            ffl = by_number[member]["levels"]["ffl"]["value"]
            if run and ffl != top:
                pieces.append(["-".join(cluster), "-".join(run), sequence, table.get(sequence, "none"), top])
                run = []
            run.append(member)
            top = ffl
        pieces.append(["-".join(cluster), "-".join(run), sequence, table.get(sequence, "none"), top])
    surfaces = (plan.get("summary") or {}).get("roadSurfacesOfFrontCorners") or {}
    ground = inputs["rules"]["levels"]["ground_ssl"] / 1000.0
    top_line = "NBH %s, %s, road surface %s of %d front corners, formulas: " % (
        plan["neighbourhood"], " and ".join(zones) or "no zone", ", ".join("%s on %d" % item for item in surfaces.items()), sum(surfaces.values()))
    formulas = mock["formulas"].replace("FFL - 0.075 m", "FFL %s %s m" % ("-" if ground <= 0 else "+", ("%.6f" % abs(ground)).rstrip("0").rstrip(".")))
    front = profile.get("frontRoad") or {}
    order = inputs["config"]["siteBuild"]["roadSurfaces"] if front.get("surface") == "roadLevel" else [front.get("surface")]
    passed = ((inputs["config"].get("placerCheck") or {}).get("level") or {}).get("passMm")
    pass_text = "none" if passed is None else ("%.6f" % passed).rstrip("0").rstrip(".") + " mm"
    return {"levels": levels, "clusters": pieces, "used": used, "topLine": top_line, "formulas": formulas if rule == PIECE_RULE else None,
            "written": len(written), "notWritten": len(not_written), "gaps": gaps, "rule": rule,
            "settings": {"Profile": profile_name, "Rules SHA-256": inputs["sha"]["rules"], "Override list SHA-256": inputs["sha"]["overrides"],
                         "Road surface order": ", ".join(order), "Pass limit": pass_text, "Editable columns": ", ".join(EDITABLE),
                         "Plan SHA-256": hashlib.sha256(inputs["planBytes"]).hexdigest()}}


def row_values(sheet, row, width):
    return [sheet.cell(row=row, column=column).value for column in range(1, width + 1)]


def same(found, wanted):
    """A cell against its value: a number as the same double, a whole step count read back as an int or a float."""
    if isinstance(wanted, (int, float)) and not isinstance(wanted, bool) and isinstance(found, (int, float)) and not isinstance(found, bool):
        return float(found) == float(wanted)
    return found == wanted


def check_xlsx(path, want, mock):
    """Every label, format, fill and cell of the written workbook against the mock and the plan, by sheet: matches and mismatches."""
    import openpyxl
    book = openpyxl.load_workbook(path)
    result, bad = Counter(), []

    def hold(what, ok, where):
        result[("match " if ok else "mismatch ") + what] += 1
        if not ok:
            bad.append(where)

    hold("sheets", book.sheetnames == mock["sheets"], "sheets %s" % book.sheetnames)
    levels = book[mock["sheets"][0]]
    width = len(mock["columns"])
    top = levels.cell(row=1, column=1).value or ""
    hold("top line prefix", top.startswith(want["topLine"]), "Levels row 1 prefix")
    if want["formulas"] is not None:
        hold("top line formulas", top == want["topLine"] + want["formulas"], "Levels row 1 formulas")
    for word in re.split(r", | and ", mock["topLine"]):
        word = re.sub(r"^the | used$", "", word)
        hold("top line names " + word, word.lower() in top.lower(), "Levels row 1 lacks " + word)
    hold("top line bold", bool(levels.cell(row=1, column=1).font.bold), "Levels A1 not bold")
    merged = [str(item) for item in levels.merged_cells.ranges]
    hold("merged only the top line", merged == ["A1:%s1" % openpyxl.utils.get_column_letter(width)], "Levels merged %s" % merged)
    hold("columns", row_values(levels, 2, width) == mock["columns"], "Levels row 2")
    fills = [levels.cell(row=2, column=column).value for column in range(1, width + 1) if levels.cell(row=2, column=column).fill.fill_type == "solid"]
    hold("editable fill", fills == [name for name in mock["columns"] if name in EDITABLE], "fills on %s" % fills)
    colours = {levels.cell(row=2, column=column).fill.fgColor.rgb for column in range(1, width + 1) if levels.cell(row=2, column=column).fill.fill_type == "solid"}
    hold("one fill colour", len(colours) == 1, "fill colours %s" % sorted(colours))
    others = [(name, cell.coordinate) for name in book.sheetnames for row in book[name].iter_rows() for cell in row
              if cell.fill.fill_type == "solid" and not (name == mock["sheets"][0] and cell.row == 2)]
    hold("no other fill", not others, "other fills %s" % others[:5])
    hold("Levels rows", levels.max_row == 2 + len(want["levels"]), "Levels max_row %d against %d" % (levels.max_row, 2 + len(want["levels"])))
    keys = [levels.cell(row=row, column=1).value for row in range(3, levels.max_row + 1)]
    hold("Plot No key unique", len(keys) == len(set(keys)) and all(isinstance(key, str) and key for key in keys), "Plot No keys not unique or blank")
    formats = {name: number_format(mock["decimals"][name]) if name in mock["decimals"] else "0" for name in PLAN_KEYS}
    for index, wanted in enumerate(want["levels"]):
        row = index + 3
        found = row_values(levels, row, width)
        for column, (cell, value) in enumerate(zip(found, wanted)):
            hold("Levels " + mock["columns"][column], same(cell, value), "Levels row %d %s" % (row, mock["columns"][column]))
            name = mock["columns"][column]
            if name in formats and value is not None:
                hold("format " + name, levels.cell(row=row, column=column + 1).number_format == formats[name], "Levels row %d format %s" % (row, name))
    for name, header, rows in ((mock["sheets"][1], CLUSTER_COLUMNS, want["clusters"]), (mock["sheets"][2], OVERRIDE_COLUMNS, want["used"])):
        sheet = book[name]
        hold(name + " header", row_values(sheet, 1, len(header)) == list(header), name + " row 1")
        hold(name + " rows", sheet.max_row == 1 + len(rows), "%s max_row %d against %d" % (name, sheet.max_row, 1 + len(rows)))
        for index, wanted in enumerate(rows):
            found = row_values(sheet, index + 2, len(header))
            hold(name + " row", all(same(cell, value) for cell, value in zip(found, wanted)), "%s row %d" % (name, index + 2))
    settings = book[mock["sheets"][3]]
    hold("Settings header", row_values(settings, 1, 2) == ["Setting", "Value"], "Settings row 1")
    names = [settings.cell(row=row, column=1).value for row in range(2, settings.max_row + 1)]
    hold("Settings names", names == list(SETTINGS), "Settings names %s" % names)
    values = {settings.cell(row=row, column=1).value: settings.cell(row=row, column=2).value for row in range(2, settings.max_row + 1)}
    for name, value in want["settings"].items():
        hold("Settings " + name, values.get(name) == value, "Settings " + name)
    hold("Settings Plan build id", values.get("Plan build id") == plan_build_id(want["plan"], values.get("Run id")), "Settings Plan build id")
    hold("Settings sha256 lowercase hex", bool(re.fullmatch(r"[0-9a-f]{64}", values.get("Plan SHA-256") or "")), "Settings Plan SHA-256 form")
    hold("Settings run time", bool(re.fullmatch(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ", values.get("Run time") or "")), "Settings Run time form")
    hold("Settings editable against the fills", values.get("Editable columns") == ", ".join(fills), "Settings Editable columns against the fills")
    return result, bad


def run(args):
    mock = mock_labels()
    inputs = load_inputs(args.plan, args.overrides, args.clusters, args.plots_csv, args.rules, args.config)
    want = expected(inputs, args.profile, mock)
    want["plan"] = inputs["plan"]
    report = {"plan": Path(args.plan).name, "file": mock["file"].replace("<NBH>", inputs["plan"]["neighbourhood"]).replace("<profile>", args.profile),
              "rows": {"written": want["written"], "notWritten": want["notWritten"], "clusterPieces": len(want["clusters"]), "overridesUsed": len(want["used"])},
              "heightRule": want["rule"], "gaps": dict(sorted(want["gaps"].items())),
              "sources": dict(Counter(row[-1].split(" ", 1)[0] if row[-1] != "Rule" else "Rule" for row in want["levels"]))}
    if args.xlsx:
        result, bad = check_xlsx(args.xlsx, want, mock)
        report["xlsx"] = {"matches": sum(count for key, count in result.items() if key.startswith("match ")),
                          "mismatches": sum(count for key, count in result.items() if key.startswith("mismatch ")),
                          "byCheck": dict(sorted(result.items())), "first": bad[:20]}
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--plan", required=True)
    parser.add_argument("--overrides", required=True)
    parser.add_argument("--clusters", required=True, help="the step clusters file of the build, step_clusters_python.json or its C# twin")
    parser.add_argument("--plots-csv", required=True)
    parser.add_argument("--rules", help="the rules profile, by default the one config names")
    parser.add_argument("--config", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", default="parsons2024Rev6")
    parser.add_argument("--xlsx", help="the level Excel the worker wrote of this plan, left out to check the plan side alone")
    parser.add_argument("--out", required=True, help="the report, outside the repo")
    args = parser.parse_args(argv)
    if args.rules is None:
        args.rules = str(project_profile.profile_path(project_profile.load_settings(Path(args.config))))
    report = run(args)
    Path(args.out).write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({key: report[key] for key in ("plan", "rows", "gaps")}))
    if "xlsx" in report:
        print("xlsx matches %d mismatches %d" % (report["xlsx"]["matches"], report["xlsx"]["mismatches"]))
    return 1 if report.get("xlsx", {}).get("mismatches") else 0


if __name__ == "__main__":
    sys.exit(main())
