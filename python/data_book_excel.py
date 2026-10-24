"""Every sheet of the master plan workbook as CSV, and a sheet list (R165).

A sheet is written cell for cell with the column letters as the header row, since the header of these schedules sits a few
rows down. The sheet list gives the rows, the columns, the header row (the first row with at least three text cells) and
whether a column names a plot number or a location. Rows are cut to Phase 1 only where such a column exists, every other
sheet is a summary and is kept whole.
"""
import argparse
import csv
import json
import re
import sys
from pathlib import Path

import openpyxl
from openpyxl.utils import get_column_letter

import c3d_run

PLOT_COLUMN = re.compile(r"plot\s*(no|number|id|#)|easting|northing|\bx\b|\by\b", re.IGNORECASE)


def header_row(rows):
    for index, row in enumerate(rows):
        if sum(1 for cell in row if isinstance(cell, str) and cell.strip()) >= 3:
            return index
    return None


def plot_columns(header):
    return [str(cell) for cell in header or [] if isinstance(cell, str) and PLOT_COLUMN.search(cell)]


def clean(cell):
    if cell is None:
        return ""
    if isinstance(cell, float):
        return repr(round(cell, 6))
    return str(cell).replace("\r", " ").replace("\n", " ")


def write_sheets(path, out):
    out.mkdir(parents=True, exist_ok=True)
    book = openpyxl.load_workbook(path, read_only=True, data_only=True)
    listing = []
    for sheet in book.worksheets:
        rows = [list(row) for row in sheet.iter_rows(values_only=True)]
        width = max((len(row) for row in rows), default=0)
        index = header_row(rows)
        header = rows[index] if index is not None else None
        named = plot_columns(header)
        target = out / (re.sub(r"[^A-Za-z0-9_-]+", "_", sheet.title.strip()) + ".csv")
        with target.open("w", encoding="utf-8", newline="") as handle:
            writer = csv.writer(handle, lineterminator="\n")
            writer.writerow([get_column_letter(k + 1) for k in range(width)])
            for row in rows:
                writer.writerow([clean(cell) for cell in row] + [""] * (width - len(row)))
        listing.append({"sheet": sheet.title, "file": target.name, "rows": len(rows), "columns": width,
                        "headerRow": None if index is None else index + 1,
                        "header": " | ".join(clean(cell) for cell in header if cell is not None) if header else "",
                        "plotOrLocationColumns": " | ".join(named), "cutToPhase1": "no, no plot number or location column" if not named else "see plotOrLocationColumns"})
    with (out / "sheets.csv").open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(listing[0]), lineterminator="\n")
        writer.writeheader()
        writer.writerows(listing)
    return listing


def main(argv=None):
    parser = argparse.ArgumentParser(description="Every sheet of the master plan workbook as CSV (R165)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    book = c3d_run.load_config(args.config)["dataBook"]
    root = Path(book["root"])
    listing = write_sheets(root / "work" / book["excel"], root / "master_plan_excel")
    for entry in listing:
        print(json.dumps({k: entry[k] for k in ("sheet", "rows", "columns", "headerRow", "plotOrLocationColumns")}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
