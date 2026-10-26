"""The two Rev 9 DLUS workbooks, Part R of round DATA-3 (R185 to R188).

Both workbooks are read from their copies under dataBook.root/work, row by row and values only, so memory stays small. Each
sheet is listed with its state and size, the sheets of both are diffed cell by cell and by plot where a sheet has a plot
column, the setbacks of the DLUS sheet and the land width and length of the sales sheet are joined to plots.csv by the
official plot number, and the old to new code table is checked against the location plan and Typology_Rev9. Rows go to the
data book, counts to rev9_workbooks.json and rev9_workbooks.md there.
"""
import argparse
import csv
import json
import re
import sys
from collections import Counter, defaultdict
from itertools import zip_longest
from pathlib import Path

import openpyxl

import c3d_run
import data_book_analysis

number = data_book_analysis.number
REPO = Path(__file__).resolve().parent.parent


def config_profile():
    return json.loads((REPO / "config" / "project.json").read_text(encoding="utf-8"))["profile"]


def open_book(path):
    import warnings
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")
        return openpyxl.load_workbook(path, read_only=True, data_only=True)


def sheet_values(path, sheet):
    """R350. The rows of a sheet as openpyxl gives them read only with values only, refused when a cell with a value lies past the
    stated dimension of the sheet, below its last row or right of its last column, which openpyxl leaves out with no word, so a sheet
    whose dimension is out of date gave no row (the review of PR 53). A row of no value past it is no refusal."""
    book = open_book(path)
    try:
        worksheet = book[sheet]
        rows = list(worksheet.iter_rows(values_only=True))
        last_row, last_column = worksheet.max_row, worksheet.max_column
        if last_row is not None or last_column is not None:
            stated = worksheet.calculate_dimension() if last_row is not None and last_column is not None else "of no size"
            worksheet.reset_dimensions()
            for index, row in enumerate(worksheet.iter_rows(values_only=True), 1):
                for column, value in enumerate(row, 1):
                    if value is not None and ((last_row is not None and index > last_row) or (last_column is not None and column > last_column)):
                        raise ValueError("the sheet %s holds a value in %s%d past its stated dimension %s, which openpyxl and the worker leave out, so the "
                                         "dimension is out of date: save the workbook again in Excel (R350)" % (sheet, openpyxl.utils.get_column_letter(column), index, stated))
    finally:
        book.close()
    return rows


def plain(value):
    """A cell as it is compared: text stripped, a number rounded to 6 decimals, an empty text as no value."""
    if isinstance(value, str):
        value = value.strip()
        return value or None
    if isinstance(value, float):
        return round(value, 6)
    return value


def inventory(path):
    """Properties of the workbook and per sheet its state, size, rows with a value and the first row with a value."""
    book = open_book(path)
    props = book.properties
    sheets = []
    for sheet in book.worksheets:
        filled, first = 0, None
        for index, row in enumerate(sheet.iter_rows(values_only=True), 1):
            if any(plain(v) is not None for v in row):
                filled += 1
                if first is None:
                    first = index
        sheets.append({"sheet": sheet.title, "state": sheet.sheet_state, "maxRow": sheet.max_row, "maxColumn": sheet.max_column,
                       "rowsWithValues": filled, "firstRow": first})
    book.close()
    return {"file": Path(path).name, "created": str(props.created), "modified": str(props.modified), "lastModifiedBy": props.lastModifiedBy,
            "sheets": sheets}


def key_column(rows, names, search_rows):
    """The header row and column of the first plot key of names found in the first search_rows rows."""
    wanted = [n.lower() for n in names]
    for index, row in enumerate(rows[:search_rows]):
        texts = [str(v).strip().lower() if v is not None else "" for v in row]
        for name in wanted:
            if name in texts:
                return index, texts.index(name)
    return None, None


def diff_sheet(rows_a, rows_b, names, search_rows):
    """Cell by cell by position, and by plot when both sheets have the same plot key: plots only in one, and per column the plots
    whose value differs."""
    cells = rows_differ = 0
    for row_a, row_b in zip_longest(rows_a, rows_b, fillvalue=()):
        found = sum(1 for a, b in zip_longest(row_a, row_b) if plain(a) != plain(b))
        cells += found
        rows_differ += found > 0
    result = {"rowsA": len(rows_a), "rowsB": len(rows_b), "rowsDiffer": rows_differ, "cellsDiffer": cells}
    head_a, col_a = key_column(rows_a, names, search_rows)
    head_b, col_b = key_column(rows_b, names, search_rows)
    if head_a is None or head_b is None:
        return result
    header_a = [str(v).strip() if v is not None else "" for v in rows_a[head_a]]
    header_b = [str(v).strip() if v is not None else "" for v in rows_b[head_b]]

    def keyed(rows, head, col, header):
        found = {}
        for row in rows[head + 1:]:
            key = plain(row[col]) if col < len(row) else None
            if key is None:
                continue
            found.setdefault(str(key), {header[k]: plain(v) for k, v in enumerate(row) if k < len(header) and header[k]})
        return found
    a, b = keyed(rows_a, head_a, col_a, header_a), keyed(rows_b, head_b, col_b, header_b)
    changed = Counter()
    plots_changed = 0
    for key in set(a) & set(b):
        columns = [c for c in set(a[key]) & set(b[key]) if a[key][c] != b[key][c]]
        changed.update(columns)
        plots_changed += bool(columns)
    result.update({"key": header_a[col_a], "plotsA": len(a), "plotsB": len(b), "onlyA": sorted(set(a) - set(b), key=_order),
                   "onlyB": sorted(set(b) - set(a), key=_order), "plotsChanged": plots_changed, "columnsChanged": dict(changed.most_common()),
                   "columnsOnlyA": sorted(set(header_a) - set(header_b) - {""}), "columnsOnlyB": sorted(set(header_b) - set(header_a) - {""})})
    return result


def _order(key):
    return (0, int(key)) if key.isdigit() else (1, key)


def diff_books(path_a, path_b, names, search_rows):
    """The sheets only in one workbook and the diff of each sheet both hold."""
    book_a, book_b = open_book(path_a), open_book(path_b)
    titles_a, titles_b = book_a.sheetnames, book_b.sheetnames
    sheets = {}
    for title in [t for t in titles_a if t in titles_b]:
        rows_a = [tuple(r) for r in book_a[title].iter_rows(values_only=True)]
        rows_b = [tuple(r) for r in book_b[title].iter_rows(values_only=True)]
        sheets[title] = diff_sheet(rows_a, rows_b, names, search_rows)
    book_a.close()
    book_b.close()
    return {"onlyA": [t for t in titles_a if t not in titles_b], "onlyB": [t for t in titles_b if t not in titles_a], "sheets": sheets}


def sheet_rows(path, sheet, header_row, columns):
    """Rows of one sheet as dicts of the config column names, from the row under header_row, stopping at no plot value."""
    found = []
    header = None
    for index, row in enumerate(sheet_values(path, sheet), 1):
        if index < header_row:
            continue
        if index == header_row:
            header = [str(v).strip() if v is not None else "" for v in row]
            missing = [c for c in columns.values() if c not in header]
            if missing:
                raise ValueError("%s has no column %s" % (sheet, ", ".join(missing)))
            at = {name: header.index(column) for name, column in columns.items()}
            continue
        values = {name: plain(row[k]) if k < len(row) else None for name, k in at.items()}
        if values.get("plot") is None:
            continue
        found.append(values)
    return found


def plots_by_number(plots):
    """plots.csv rows by official number: the location plan number of the face, a face with one number only."""
    found, many = {}, Counter()
    for row in plots:
        numbers = [n for n in (row.get("locationPlanNumber") or "").split("|") if n]
        if len(numbers) != 1:
            continue
        if numbers[0] in found:
            many[numbers[0]] += 1
        found[numbers[0]] = row
    for key in many:
        found.pop(key, None)
    return found, sorted(many, key=_order)


def line_class(value, lines, tolerance):
    if value is None:
        return "none"
    for line in lines:
        if abs(value - line) <= tolerance:
            return "%.2f" % line
    return "other"


def setbacks(dlus, plots, lines, manual, tolerance, unit_types):
    """The DLUS setbacks joined to plots.csv by plot number. Front against the drawn line and against Rev 6, counted by typology,
    and the four values as they are written, counted by typology."""
    by_number, many = plots_by_number(plots)
    rows, counts = [], defaultdict(Counter)
    values = defaultdict(Counter)
    joined = 0
    for item in dlus:
        key = str(int(item["plot"])) if isinstance(item["plot"], (int, float)) else str(item["plot"])
        row = by_number.get(key)
        typology = item.get("typology") or "none"
        for side in ("front", "right", "left", "rear"):
            values[typology][side + " " + _text(item.get(side))] += 1
        if row is None:
            counts[typology]["notInPlots"] += 1
            continue
        joined += 1
        front = number(item.get("front"))
        drawn = number(row["setbackFromFrontM"])
        wanted = manual.get(row["clientType"], manual["default"])
        drawn_class = line_class(drawn, lines, tolerance)
        counts[typology]["plots"] += 1
        counts[typology]["drawn " + drawn_class] += 1
        if front is None:
            counts[typology]["noDlusFront"] += 1
        else:
            counts[typology]["front %.2f" % front] += 1
            if drawn is not None:
                counts[typology]["frontEqualsDrawn" if abs(front - drawn) <= tolerance else "frontDiffersDrawn"] += 1
            counts[typology]["frontEqualsRev6" if abs(front - wanted) <= tolerance else "frontDiffersRev6"] += 1
        rows.append({"plot": key, "typology": item.get("typology"), "clientType": row["clientType"], "neighbourhood": row["neighbourhood"],
                     "front": front, "right": number(item.get("right")), "left": number(item.get("left")), "rear": number(item.get("rear")),
                     "drawnFront": drawn, "drawnClass": drawn_class, "manualFront": wanted, "unitPlot": row["clientType"] in unit_types})
    unit_rows = [r for r in rows if r["unitPlot"]]
    summary = {"dlusRows": len(dlus), "joined": joined, "numbersOnManyFaces": len(many),
               "unitPlots": len(unit_rows),
               "unitFront": dict(Counter("%.2f" % r["front"] if r["front"] is not None else "none" for r in unit_rows).most_common()),
               "unitFrontAgainstDrawn": dict(Counter(_against(r["front"], r["drawnFront"], tolerance) for r in unit_rows)),
               "unitFrontAgainstRev6": dict(Counter(_against(r["front"], r["manualFront"], tolerance) for r in unit_rows)),
               "unitDrawnClass": dict(Counter(r["drawnClass"] for r in unit_rows)),
               "byTypology": {k: dict(v) for k, v in sorted(counts.items())},
               "valuesByTypology": {k: dict(v.most_common(12)) for k, v in sorted(values.items())}}
    return summary, rows


def _text(value):
    found = number(value)
    return "none" if value is None else "%.2f" % found if found is not None else str(value)


def _against(value, other, tolerance):
    if value is None or other is None:
        return "noValue"
    return "equal" if abs(value - other) <= tolerance else "differs"


def against_widths(width, allowed, tolerance):
    """matches one of the Rev 6 widths, wider than the widest, narrower than the narrowest, or between two of them."""
    if not allowed or width is None:
        return "noRev6"
    if any(abs(width - v) <= tolerance for v in allowed):
        return "matches"
    return "wider" if width > max(allowed) else "narrower" if width < min(allowed) else "between"


def widths(sales, plots, manual_widths, code_widths, from_cad, tolerance, unit_types):
    """Land Width and Land Length of the sales sheet against the CAD width and depth of plots.csv, against the Rev 6 widths of
    the client type and against the width of the Typology_Rev9 code in the code table."""
    by_number, _ = plots_by_number(plots)
    rows = []
    for item in sales:
        key = str(int(item["plot"])) if isinstance(item["plot"], (int, float)) else str(item["plot"])
        row = by_number.get(key)
        if row is None or row["clientType"] not in unit_types:
            continue
        width, length = number(item.get("landWidth")), number(item.get("landLength"))
        cad_width, cad_depth = number(row["widthM"]), number(row["depthM"])
        allowed = manual_widths.get(row["clientType"])
        code_width = code_widths.get(item.get("typology"))
        rows.append({"plot": key, "clientType": row["clientType"], "typologyRev9": item.get("typology"), "typologyRev81": item.get("typologyOld"),
                     "neighbourhood": row["neighbourhood"], "landWidth": width, "landLength": length, "cadWidth": cad_width, "cadDepth": cad_depth,
                     "widthAgainstCad": _against(width, cad_width, tolerance), "lengthAgainstCad": _against(length, cad_depth, tolerance),
                     "cadAgainstRev6": "fromCad" if row["clientType"] in from_cad else against_widths(cad_width, allowed, tolerance),
                     "landAgainstRev6": against_widths(width, allowed, tolerance),
                     "landAgainstCode": _against(width, code_width, tolerance), "codeWidth": code_width})
    by_type = defaultdict(Counter)
    for r in rows:
        for column in ("widthAgainstCad", "lengthAgainstCad", "cadAgainstRev6", "landAgainstRev6", "landAgainstCode"):
            by_type[r["clientType"]][column + " " + r[column]] += 1
    return {"unitPlotsWithSalesRow": len(rows), "byClientType": {k: dict(sorted(v.items())) for k, v in sorted(by_type.items())}}, rows


def wide_again(rows, types):
    """The plots whose CAD width is over Rev 6, by client type: what the sales sheet and Typology_Rev9 say of them."""
    found = {}
    for kind in types:
        wide = [r for r in rows if r["clientType"] == kind and r["cadAgainstRev6"] not in ("matches", "fromCad", "noRev6")]
        found[kind] = {"plots": len(wide), "against": dict(Counter(r["cadAgainstRev6"] for r in wide)),
                       "landWidthEqualsCad": sum(1 for r in wide if r["widthAgainstCad"] == "equal"),
                       "landWidth": dict(Counter("%.2f" % r["landWidth"] if r["landWidth"] is not None else "none" for r in wide).most_common()),
                       "cadWidth": dict(Counter("%.1f" % r["cadWidth"] for r in wide).most_common(6)),
                       "typologyRev9": dict(Counter(r["typologyRev9"] or "none" for r in wide).most_common()),
                       "codeWidthOfTypologyRev9": dict(Counter("%g" % r["codeWidth"] if r["codeWidth"] is not None else "none" for r in wide).most_common()),
                       "landEqualsCodeWidth": sum(1 for r in wide if r["landAgainstCode"] == "equal")}
    return found


def code_table(path, sheet, header_row, columns):
    """The old to new code table as it is written: every row with an old or a new code, the form left empty where the sheet has none."""
    book = open_book(path)
    rows, at = [], None
    for index, row in enumerate(book[sheet].iter_rows(values_only=True), 1):
        if index == header_row:
            header = [str(v).strip() if v is not None else "" for v in row]
            at = {name: header.index(column) for name, column in columns.items()}
            continue
        if at is None:
            continue
        values = {name: plain(row[k]) if k < len(row) else None for name, k in at.items()}
        if values.get("old") is None and values.get("new") is None:
            continue
        rows.append(values)
    book.close()
    return rows


def profile_codes(rows, source):
    """The block of the rules profile: the rows of the table with both code sets side by side, never renamed (R188)."""
    keep = ("form", "old", "new", "widthM", "depthM", "plotAreaM2", "gfaM2", "buaM2")
    return {"source": source, "rows": [{k: row.get(k) for k in keep} for row in rows]}


PROFILE_KEYS = {"form": "form", "old": "old", "new": "new", "widthM": "width_m", "depthM": "depth_m", "plotAreaM2": "plot_area_m2", "gfaM2": "gfa_m2",
                "buaM2": "bua_m2"}


def profile_against_sheet(profile_rows, sheet_rows):
    """Rows of typology_codes in the rules profile that equal the sheet row at the same place, value by value (R188)."""
    equal = sum(1 for a, b in zip(profile_rows, sheet_rows) if all(a.get(p) == b.get(s) for s, p in PROFILE_KEYS.items()))
    return {"profileRows": len(profile_rows), "sheetRows": len(sheet_rows), "equal": equal}


def code_checks(table, labels, sales_typologies, code_pattern):
    """The table against the codes the location plan prints and the Typology_Rev9 values of the sales sheet. A printed code that
    is no old code of the table is looked up by its base, the code without the suffix of the location plan pattern."""
    olds = {r["old"] for r in table if r["old"]}
    news = {r["new"] for r in table if r["new"]}
    printed = Counter(l["code"] for l in labels if l.get("kind") == "clientCode")
    pairs = {(r["old"], r["new"]) for r in table if r["old"] and r["new"]}

    def base(code):
        found = re.fullmatch(code_pattern, code or "")
        return found.group("base") if found else code
    mapped = Counter((l["code"] if l["code"] in olds else base(l["code"]), l["newCode"]) for l in labels if l.get("kind") == "clientCode")
    return {"tableRows": len(table), "oldCodes": sorted(olds), "newCodes": sorted(news),
            "oldWithTwoNew": sorted(o for o in olds if len({r["new"] for r in table if r["old"] == o and r["new"]}) > 1),
            "oldWithNoNew": sorted(r["old"] for r in table if r["old"] and not r["new"]),
            "locationCodes": len(printed), "locationCodesInTable": sorted(c for c in printed if c in olds),
            "locationCodesByBase": dict((c, n) for c, n in printed.most_common() if c not in olds and base(c) in olds),
            "locationCodesNotInTable": dict((c, n) for c, n in printed.most_common() if c not in olds and base(c) not in olds),
            "locationLabels": sum(printed.values()),
            "locationPairsInTable": sum(n for (o, n2), n in mapped.items() if (o, n2) in pairs),
            "locationPairsNotInTable": dict(("%s>%s" % k, n) for k, n in mapped.most_common() if k not in pairs),
            "typologyRev9": dict(sales_typologies.most_common()),
            "typologyRev9NotNew": dict((c, n) for c, n in sales_typologies.most_common() if c not in news)}


def write_rows(path, rows):
    if not rows:
        return
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, list(rows[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def markdown(report):
    lines = ["# Rev 9 workbooks, round DATA-3 Part R", "", "Counts only. Rows are setbacks.csv and widths.csv next to this file.", ""]
    for name, inv in report["inventory"].items():
        lines.append("- %s: %s, modified %s by %s, %d sheets" % (name, inv["file"], inv["modified"], inv["lastModifiedBy"], len(inv["sheets"])))
    lines.append("")
    lines.append("```")
    lines.append(json.dumps({k: v for k, v in report.items() if k != "inventory"}, indent=1, default=str)[:60000])
    lines.append("```")
    return "\n".join(lines) + "\n"


def run(config):
    book = config["dataBook"]
    settings = book["rev9"]
    location = book["locationPlan"]
    root = Path(book["root"])
    work = root / "work"
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    paths = {name: work / rel for name, rel in settings["workbooks"].items()}
    report = {"inventory": {name: inventory(path) for name, path in paths.items()}}
    first, second = settings["diffOrder"]
    report["diff"] = {"a": first, "b": second, **diff_books(paths[first], paths[second], settings["keyNames"], settings["keySearchRows"])}
    plots = data_book_analysis.read_csv(root / "plots.csv")
    analysis = book["analysis"]
    dlus = settings["dlus"]
    dlus_rows = sheet_rows(paths[dlus["workbook"]], dlus["sheet"], dlus["headerRow"], dlus["columns"])
    report["setbacks"], setback_rows = setbacks(dlus_rows, plots, settings["setbackLinesM"], analysis["setbackM"], analysis["setbackToleranceM"],
                                                set(analysis["unitPlotTypes"]))
    table_settings = settings["codeTable"]
    table = code_table(paths[table_settings["workbook"]], table_settings["sheet"], table_settings["headerRow"], table_settings["columns"])
    code_widths = {}
    for row in table:
        if row["new"] and number(row.get("widthM")) is not None:
            code_widths.setdefault(row["new"], number(row["widthM"]))
    sales = settings["sales"]
    sales_rows = sheet_rows(paths[sales["workbook"]], sales["sheet"], sales["headerRow"], sales["columns"])
    report["widths"], width_rows = widths(sales_rows, plots, analysis["widthsByClientType"], code_widths, set(analysis.get("widthFromCad") or []),
                                          analysis["widthToleranceM"], set(analysis["unitPlotTypes"]))
    report["wideAgain"] = wide_again(width_rows, settings["wideTypes"])
    labels = data_book_analysis.read_csv(root / location["outFolder"] / "labels.csv")
    report["codes"] = code_checks(table, labels, Counter(r["typology"] for r in sales_rows if r.get("typology")), location["patterns"]["clientCode"])
    profile = json.loads((REPO / config_profile()).read_text(encoding="utf-8"))
    report["profileAgainstSheet"] = profile_against_sheet((profile.get("typology_codes") or {}).get("rows") or [], table)
    report["profileCodes"] = profile_codes(table, "%s, sheet %s" % (Path(settings["workbooks"][table_settings["workbook"]]).name, table_settings["sheet"]))
    write_rows(out / "setbacks.csv", setback_rows)
    write_rows(out / "widths.csv", width_rows)
    (out / "rev9_workbooks.json").write_text(json.dumps(report, indent=1, default=str), encoding="utf-8")
    (out / "rev9_workbooks.md").write_text(markdown(report), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The two Rev 9 DLUS workbooks: inventory, diff, setbacks, widths and the code table (R185 to R188)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"diff": {k: v for k, v in report["diff"].items() if k != "sheets"}, "setbacks": {k: report["setbacks"][k] for k in ("dlusRows", "joined", "unitPlots")},
                      "widths": report["widths"]["unitPlotsWithSalesRow"]}, default=str))
    return 0


if __name__ == "__main__":
    sys.exit(main())
