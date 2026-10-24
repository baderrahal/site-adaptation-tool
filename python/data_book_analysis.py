"""The analysis of the data book (R167), written to analysis.md and analysis.json in the data book.

It reads plots.csv, the per drawing tables and the workbook sheets and counts: what each drawing and sheet adds, the
coverage of every plots.csv column per neighbourhood, the conflicts between sources, the unit code source for plots with no
schedule, the entry level source against the past schedules, the IME neighbourhood and the data quality. Counts only, the
data book itself holds the rows.
"""
import argparse
import csv
import json
import re
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

import c3d_run
import link_summary
import project_profile

TOKEN = re.compile(r"[A-Z]{1,3}\d{1,2}(?:-IM)?", re.IGNORECASE)


def read_csv(path):
    with open(path, encoding="utf-8") as handle:
        return list(csv.DictReader(handle))


def number(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def band(difference, bands):
    for limit, name in zip(bands, ("within5mm", "within30mm")):
        if abs(difference) <= limit + 1e-9:
            return name
    return "worse"


def group_of(row):
    return row["neighbourhood"] or "rest"


def coverage(rows, columns):
    groups = defaultdict(list)
    for row in rows:
        groups[group_of(row)].append(row)
    table = {}
    for column in columns:
        table[column] = {group: round(sum(1 for r in members if r[column] not in ("", "no")) / len(members), 3) for group, members in sorted(groups.items())}
    return {"plots": {g: len(m) for g, m in sorted(groups.items())}, "share": table}


def suffix(name):
    """The type a block name ends with, Villa_VL4 gives VL4."""
    return name.split("_")[-1].upper() if name else None


def conflicts(rows, profile_units, analysis):
    unit_types = set(analysis["unitPlotTypes"])
    units = [r for r in rows if r["clientType"] in unit_types]
    result = {}
    v9 = Counter()
    for r in rows:
        if not r["v9PlotNumber"]:
            v9["noV9Number"] += 1
        elif "|" in r["v9PlotNumber"]:
            v9["manyV9Numbers"] += 1
        elif r["v9PlotNumber"] == r["baseplanLabel"]:
            v9["equalsBaseplanLabel"] += 1
        elif r["officialNumber"] and r["v9PlotNumber"] == r["officialNumber"]:
            v9["equalsOfficialNumber"] += 1
        else:
            v9["differs"] += 1
    result["plotNumberV9"] = dict(v9)
    for column in ("r06Number", "r06_0000Number"):
        found = Counter()
        for r in rows:
            value = r[column]
            found["none" if not value else "many" if "|" in value else "equalsBaseplanLabel" if value == r["baseplanLabel"]
                  else "equalsOfficialNumber" if value == r["officialNumber"] else "differs"] += 1
        result["plotNumber_" + column] = dict(found)
    for column in ("footprintBlock", "r06_0000Block", "r06Block"):
        found = Counter()
        for r in units:
            names = [n for n in (r[column] or "").split("|") if n]
            if not names:
                found["noBlock"] += 1
            elif len(names) > 1:
                found["manyBlocks"] += 1
            elif suffix(names[0]) == r["clientType"]:
                found["sameType"] += 1
            else:
                found["otherType " + suffix(names[0])] += 1
        result["typeBaseplanAgainst_" + column] = dict(found)
    widths = defaultdict(Counter)
    for r in units:
        width = number(r["widthM"])
        allowed = analysis["widthsByClientType"].get(r["clientType"])
        if width is None or allowed is None:
            widths[r["clientType"]]["notChecked"] += 1
            continue
        ok = any(abs(width - value) <= analysis["widthToleranceM"] for value in allowed)
        widths[r["clientType"]]["matches" if ok else "differs %.1f" % width] += 1
    result["widthAgainstRev6"] = {k: dict(v.most_common(8)) for k, v in sorted(widths.items())}
    setback = Counter()
    for r in units:
        value = number(r["setbackFromFrontM"])
        wanted = analysis["setbackM"].get(r["clientType"], analysis["setbackM"]["default"])
        if value is None:
            setback["noSetbackLine"] += 1
        else:
            setback["within" if abs(value - wanted) <= analysis["setbackToleranceM"] else "differs"] += 1
    result["setbackAgainstRev6"] = dict(setback)
    result["inManyLabelFace"] = sum(1 for r in rows if r["inManyLabelFace"] == "yes")
    result["th1im"] = {"unitsInRev6Code": "TH1-IM" in profile_units, "plotsNamingIt": sum(1 for r in rows if "TH1-IM" in " ".join(str(v) for v in r.values()).upper())}
    result["unitPlotsInPhase1"] = len(units)
    return result


def unit_source(rows, profile_units, analysis):
    """For unit plots with no schedule unit, which column names a Rev 6 unit code."""
    unit_types = set(analysis["unitPlotTypes"])
    missing = [r for r in rows if r["clientType"] in unit_types and not r["unitRev6"]]
    columns = ["footprintBlock", "r06Block", "r06_0000Block", "v9PlotNumber", "cluster", "clientType"]
    by_column = Counter()
    other_code = Counter()
    for r in missing:
        for column in columns:
            tokens = {t.upper() for t in TOKEN.findall(r[column] or "")}
            codes = tokens & profile_units
            if codes:
                by_column[column] += 1
            if codes - {r["clientType"]}:
                other_code[column] += 1
    # The codes of Rev 6 that are never a client plot type, a source naming one of them gives what the baseplan cannot
    only_codes = profile_units - unit_types
    only = Counter()
    for r in missing:
        for column in columns:
            if {t.upper() for t in TOKEN.findall(r[column] or "")} & only_codes:
                only[column] += 1
    return {"unitPlotsWithoutUnit": len(missing), "plotsWhereColumnNamesAProfileCode": dict(by_column),
            "plotsWhereColumnNamesACodeOtherThanTheClientType": dict(other_code), "codesOnlyInRev6": sorted(only_codes),
            "plotsWhereColumnNamesARev6OnlyCode": dict(only)}


def entry_source(rows, analysis):
    level_columns = sorted({c[len("level_"):-len("_left")] for c in rows[0] if c.startswith("level_") and c.endswith("_left")}) if rows else []
    result = {}
    with_entry = [r for r in rows if number(r["pastEntry"]) is not None]
    for name in level_columns + ["roadLevel"]:
        found = Counter()
        diffs = []
        for r in with_entry:
            left = number(r["roadLevelLeft"] if name == "roadLevel" else r["level_" + name + "_left"])
            right = number(r["roadLevelRight"] if name == "roadLevel" else r["level_" + name + "_right"])
            if left is None or right is None:
                found["noLevel"] += 1
                continue
            difference = (left + right) / 2 - number(r["pastEntry"])
            diffs.append(difference)
            found[band(difference, analysis["bandsM"])] += 1
        result[name] = {"plots": len(with_entry), **dict(found), "medianDifference": round(statistics.median(diffs), 4) if diffs else None}
    return result


def ime_search(root, words):
    """Where any text of the data book, a layer name or a workbook cell names a neighbourhood code."""
    found = defaultdict(Counter)
    for folder in sorted(p for p in root.iterdir() if p.is_dir() and (p / "texts.csv").is_file()):
        for row in read_csv(folder / "texts.csv"):
            for word in words:
                if re.search(r"\b" + word + r"\b", row["text"] or "", re.IGNORECASE):
                    found[word][folder.name + " texts"] += 1
        for row in read_csv(folder / "layers.csv"):
            for word in words:
                if re.search(r"\b" + word + r"\b", row["layer"] or "", re.IGNORECASE):
                    found[word][folder.name + " layers"] += 1
    sheets = root / "master_plan_excel"
    for path in sorted(sheets.glob("*.csv")) if sheets.is_dir() else []:
        text = path.read_text(encoding="utf-8")
        for word in words:
            hits = len(re.findall(r"\b" + word + r"\b", text, re.IGNORECASE))
            if hits:
                found[word]["workbook " + path.name] += hits
    return {word: dict(found[word]) for word in words}


def drawing_tables(root):
    """Rows kept and dropped per table of every drawing folder, with its reader."""
    tables = {}
    for folder in sorted(p for p in root.iterdir() if p.is_dir() and (p / "data.json").is_file()):
        summary = json.loads((folder / "data.json").read_text(encoding="utf-8"))
        rows = {}
        for name in ("layers", "entities", "blocks", "texts", "surfaces", "civil"):
            paths = [folder / (name + ".csv")] if (folder / (name + ".csv")).is_file() else sorted((folder / name).glob("*.csv")) if (folder / name).is_dir() else []
            if paths:
                rows[name] = sum(sum(1 for _ in open(path, encoding="utf-8")) - 1 for path in paths)
        layers = read_csv(folder / "layers.csv") if (folder / "layers.csv").is_file() else []
        mixed = [l["layer"] for l in layers if len((l.get("countsByType") or "").split()) >= 4]
        tables[folder.name] = {"reader": summary.get("reader") or "C3DProbe", "rows": rows, "counts": summary.get("counts"),
                               "layersWithEntities": sum(1 for l in layers if l.get("entitiesInside") not in ("", "0")), "layersWithFourTypesOrMore": mixed[:20]}
    return tables


def quality(rows, plots_json, border_contains):
    open_faces = [e for e in plots_json if e.get("kind") == "plot_open"]
    inside_open = sum(1 for e in open_faces if any(border_contains(s[0], s[1]) for s in e.get("segments") or []))
    return {"duplicateLabel": sum(1 for r in rows if r["duplicateLabel"] == "yes"), "openFacesInPhase1": inside_open,
            "inManyLabelFace": sum(1 for r in rows if r["inManyLabelFace"] == "yes"), "crossesBorder": sum(1 for r in rows if r["crossesBorder"] == "yes"),
            "noFront": sum(1 for r in rows if r["noFront"] == "yes"), "twoNeighbourhoods": sum(1 for r in rows if r["twoNeighbourhoods"] == "yes"),
            "noClientType": sum(1 for r in rows if not r["clientType"])}


def markdown(report):
    lines = ["# Data book analysis", "", "Generated by python/data_book_analysis.py (R167). Counts only, the rows are in the tables of this folder.", ""]
    for key, value in report.items():
        lines.append("## " + key)
        lines.append("")
        lines.append("```")
        lines.append(json.dumps(value, indent=1, ensure_ascii=False))
        lines.append("```")
        lines.append("")
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Analyse the data book (R167)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book = config["dataBook"]
    analysis = book["analysis"]
    root = Path(book["root"])
    rows = read_csv(root / "plots.csv")
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    profile_units = {code.upper() for code in project_profile.load_profile(project_profile.profile_path(settings)).get("typologies")}
    from shapely.geometry import Point
    import data_book
    border = data_book.border_polygon(root / "phase1_border.csv")
    plots_json = link_summary.load(config["connect"]["baseplanPlots"])["plots"]
    columns = [c for c in rows[0] if not c.startswith("level_")] if rows else []
    unit_types = set(analysis["unitPlotTypes"])
    report = {
        "drawings": drawing_tables(root),
        "workbook": read_csv(root / "master_plan_excel" / "sheets.csv") if (root / "master_plan_excel" / "sheets.csv").is_file() else None,
        "plotCount": {"phase1Plots": len(rows), "unitPlots": sum(1 for r in rows if r["clientType"] in unit_types), "unitsRev6": book["join"]["unitsRev6"],
                      "byClientType": dict(Counter(r["clientType"] or "none" for r in rows).most_common())},
        "coverage": coverage(rows, columns),
        "conflicts": conflicts(rows, profile_units, analysis),
        "unitCodeSource": unit_source(rows, profile_units, analysis),
        "entryLevelSource": entry_source(rows, analysis),
        "neighbourhoodWords": ime_search(root, analysis["neighbourhoodWords"]),
        "quality": quality(rows, plots_json, lambda x, y: border.contains(Point(x, y))),
    }
    (root / "analysis.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    (root / "analysis.md").write_text(markdown(report), encoding="utf-8")
    for key in ("plotCount", "conflicts", "unitCodeSource", "entryLevelSource", "neighbourhoodWords", "quality"):
        print(key, json.dumps(report[key], ensure_ascii=False)[:1500])
    return 0


if __name__ == "__main__":
    sys.exit(main())
