import argparse
import json
import sys
from pathlib import Path

from openpyxl import load_workbook
from openpyxl.utils import get_column_letter

from common import write_json

HEADER_MARKER = "Plot No."
# Columns that keep 4 decimals, matched on the header text without trailing stars
WIDE_COLUMNS = {"el @ entry", "height"}
EXTRA_LIMIT = 200
PLOT_WORDS = ("plot no", "plot number", "plot id", "plot #")
AREA_FIT = 0.01
HEADER_SCAN = 30


def find_sheet(workbook, wanted):
    for name in workbook.sheetnames:
        if name.strip().lower() == wanted.strip().lower():
            return workbook[name]
    raise ValueError("Sheet '" + wanted + "' not found. Sheets present: " + ", ".join(workbook.sheetnames))


def unique_headers(first_row):
    headers = []
    seen = {}
    for index, cell in enumerate(first_row, start=1):
        text = "" if cell is None else str(cell).strip()
        if not text:
            text = "col_" + get_column_letter(index)
        seen[text] = seen.get(text, 0) + 1
        headers.append(text if seen[text] == 1 else text + "_" + str(seen[text]))
    return headers


def is_blank(cell):
    return cell is None or str(cell).strip() == ""


def find_header_row(rows):
    marker = HEADER_MARKER.lower()
    for index, row in enumerate(rows):
        if any(not is_blank(cell) and marker in str(cell).lower() for cell in row):
            return index
    return None


def is_total_row(row, plot_column):
    if any(isinstance(cell, str) and cell.strip().lower().startswith("total") for cell in row):
        return True
    # The total row of the real workbook has no label, only a number and an empty plot number cell
    return is_blank(row[plot_column]) if plot_column < len(row) else True


def wide_headers(headers):
    return {header for header in headers if header.strip().rstrip("*").strip().lower() in WIDE_COLUMNS}


def read_sheet(xlsx_path, sheet_name):
    workbook = load_workbook(str(xlsx_path), read_only=True, data_only=True)
    try:
        sheet = find_sheet(workbook, sheet_name)
        rows = [row for row in sheet.iter_rows(values_only=True)]
        sheet_title = sheet.title
    finally:
        workbook.close()
    header_index = find_header_row(rows)
    if header_index is None:
        raise ValueError("No row containing '" + HEADER_MARKER + "' in sheet '" + sheet_title + "'")
    header_row = rows[header_index]
    headers = unique_headers(header_row)
    plot_column = next(index for index, cell in enumerate(header_row) if not is_blank(cell) and HEADER_MARKER.lower() in str(cell).lower())
    records = []
    for offset, row in enumerate(rows[header_index + 1:]):
        if all(is_blank(cell) for cell in row) or is_total_row(row, plot_column):
            break
        record = {"sheetRow": header_index + 2 + offset}
        for header, cell in zip(headers, row):
            record[header] = cell.isoformat() if hasattr(cell, "isoformat") else cell
        records.append(record)
    return sheet_title, header_index + 1, headers, records


def cells_of(row, number):
    cells = {}
    for index, cell in enumerate(row, start=1):
        if not is_blank(cell):
            cells[get_column_letter(index)] = cell.isoformat() if hasattr(cell, "isoformat") else cell
    return {"row": number, "cells": cells} if cells else None


def read_extra(xlsx_path, sheet_name, header_row, row_count):
    """Rows outside the plot table and the formulas of the first plot row, kept because notes and legends hold the rules."""
    workbook = load_workbook(str(xlsx_path), read_only=True, data_only=False)
    try:
        rows = [row for row in find_sheet(workbook, sheet_name).iter_rows(values_only=True)]
    finally:
        workbook.close()
    end = header_row + row_count
    above = [entry for entry in (cells_of(row, number) for number, row in enumerate(rows[:header_row - 1], start=1)) if entry]
    below = [entry for entry in (cells_of(row, number) for number, row in enumerate(rows[end:], start=end + 1)) if entry]
    formulas = {}
    if row_count and header_row < len(rows):
        headers = unique_headers(rows[header_row - 1])
        for header, cell in zip(headers, rows[header_row]):
            if isinstance(cell, str) and cell.startswith("="):
                formulas[header] = cell
    return {"above": above[:EXTRA_LIMIT], "below": below[:EXTRA_LIMIT], "formulas": formulas}


def write_outputs(xlsx_path, out_dir, sheet_name):
    sheet_title, header_row, headers, records = read_sheet(xlsx_path, sheet_name)
    out_dir = Path(out_dir)
    wide = wide_headers(headers)
    write_json(out_dir / "coords.json", {"source": Path(xlsx_path).name, "sheet": sheet_title, "headerRow": header_row, "rows": records}, wide)
    write_json(out_dir / "coords_headers.json", {"sheet": sheet_title, "headerRow": header_row, "headers": headers})
    extra = read_extra(xlsx_path, sheet_name, header_row, len(records))
    write_json(out_dir / "coords_extra.json", dict({"source": Path(xlsx_path).name, "sheet": sheet_title}, **extra))
    return headers, records


def plot_column_of(headers):
    for index, header in enumerate(headers):
        text = str(header).strip().lower()
        if any(word in text for word in PLOT_WORDS):
            return index
    return None


def survey_header_row(rows):
    """The row with Plot No. when there is one, else the row with the most text cells in the first rows. Title rows have few."""
    found = find_header_row(rows)
    if found is not None:
        return found
    best, best_count = None, 2
    for index, row in enumerate(rows[:HEADER_SCAN]):
        count = sum(1 for cell in row if isinstance(cell, str) and cell.strip())
        if count > best_count:
            best, best_count = index, count
    return best


def numbering_counts(plots, areas, cad_areas, shifts):
    """For each reading of the numbers (as found, then minus each shift) how many plots have a CAD plot of the same area."""
    readings = {"asFound": 0}
    readings.update({"minus" + str(shift): 0 for shift in shifts})
    for plot, area in zip(plots, areas):
        if not isinstance(plot, (int, float)) or not isinstance(area, (int, float)) or area <= 0:
            continue
        for name, shift in [("asFound", 0)] + [("minus" + str(value), value) for value in shifts]:
            cad = cad_areas.get(int(plot) - shift)
            if cad and abs(cad - area) / area <= AREA_FIT:
                readings[name] += 1
    return readings


def survey(xlsx_path, cad_areas=None, shifts=()):
    """Sheets, header rows, row counts and the plot number column of a workbook, counts and header texts only."""
    workbook = load_workbook(str(xlsx_path), read_only=True, data_only=True)
    try:
        sheets = [(sheet.title, [row for row in sheet.iter_rows(values_only=True)]) for sheet in workbook.worksheets]
    finally:
        workbook.close()
    report = []
    for title, rows in sheets:
        header_index = survey_header_row(rows)
        entry = {"sheet": title, "rows": len(rows), "headerRow": None if header_index is None else header_index + 1, "dataRows": 0, "plotColumn": None}
        if header_index is not None:
            headers = unique_headers(rows[header_index])
            body = [row for row in rows[header_index + 1:] if any(not is_blank(cell) for cell in row)]
            entry["dataRows"] = len(body)
            entry["headers"] = [header for header in headers if not header.startswith("col_")]
            entry["plotHeaders"] = [header for header in headers if "plot" in header.lower()]
            column = plot_column_of(headers)
            if column is not None:
                entry["plotColumn"] = headers[column]
                plots = [row[column] if column < len(row) else None for row in body]
                entry["plotNumbers"] = sum(1 for plot in plots if isinstance(plot, (int, float)))
                area_column = next((index for index, header in enumerate(headers) if "area" in header.lower()), None)
                if cad_areas and area_column is not None:
                    areas = [row[area_column] if area_column < len(row) else None for row in body]
                    entry["numbering"] = numbering_counts(plots, areas, cad_areas, shifts)
        report.append(entry)
    return {"source": Path(xlsx_path).name, "sheets": report, "sheetCount": len(report), "plotColumnSheets": sum(1 for entry in report if entry["plotColumn"])}


def cad_areas_of(plots_path):
    plots = json.loads(Path(plots_path).read_text(encoding="utf-8"))
    return {int(shape["plot"]): shape["area"] for shape in plots["plots"] if shape["kind"] == "plot_shape" and str(shape.get("plot", "")).isdigit()}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read the coordinates sheet of a workbook.")
    parser.add_argument("xlsx")
    parser.add_argument("out_dir")
    parser.add_argument("--sheet", default="Coordinates")
    parser.add_argument("--survey", action="store_true", help="report sheets, header rows, row counts and the plot number column instead of reading one sheet")
    parser.add_argument("--cad-plots", help="plots.json of the baseplan, for the numbering test of a survey")
    parser.add_argument("--shifts", help="plot_numbers.json, for the numbering test of a survey")
    args = parser.parse_args(argv)
    if not Path(args.xlsx).is_file():
        print("Workbook not found: " + args.xlsx, file=sys.stderr)
        return 2
    if args.survey:
        cad = cad_areas_of(args.cad_plots) if args.cad_plots and Path(args.cad_plots).is_file() else None
        shifts = []
        if args.shifts and Path(args.shifts).is_file():
            shifts = sorted({entry["shift"] for entry in json.loads(Path(args.shifts).read_text(encoding="utf-8"))["neighbourhoods"].values()})
        report = survey(args.xlsx, cad, shifts)
        write_json(Path(args.out_dir) / "survey.json", report)
        print("sheets " + str(report["sheetCount"]) + ", sheets with a plot number column " + str(report["plotColumnSheets"]))
        return 0
    try:
        headers, records = write_outputs(args.xlsx, args.out_dir, args.sheet)
    except ValueError as problem:
        print(str(problem), file=sys.stderr)
        return 3
    print("headers: " + ", ".join(headers))
    print("rows " + str(len(records)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
