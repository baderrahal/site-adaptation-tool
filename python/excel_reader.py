import argparse
import sys
from pathlib import Path

from openpyxl import load_workbook
from openpyxl.utils import get_column_letter

from common import write_json


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


def read_sheet(xlsx_path, sheet_name):
    workbook = load_workbook(str(xlsx_path), read_only=True, data_only=True)
    try:
        sheet = find_sheet(workbook, sheet_name)
        rows = [row for row in sheet.iter_rows(values_only=True) if any(cell is not None and str(cell).strip() != "" for cell in row)]
        sheet_title = sheet.title
    finally:
        workbook.close()
    if not rows:
        return sheet_title, [], []
    headers = unique_headers(rows[0])
    records = []
    for row in rows[1:]:
        record = {}
        for header, cell in zip(headers, row):
            record[header] = cell.isoformat() if hasattr(cell, "isoformat") else cell
        records.append(record)
    return sheet_title, headers, records


def write_outputs(xlsx_path, out_dir, sheet_name):
    sheet_title, headers, records = read_sheet(xlsx_path, sheet_name)
    out_dir = Path(out_dir)
    write_json(out_dir / "coords.json", {"source": Path(xlsx_path).name, "sheet": sheet_title, "rows": records})
    write_json(out_dir / "coords_headers.json", {"sheet": sheet_title, "headers": headers})
    return headers, records


def main(argv=None):
    parser = argparse.ArgumentParser(description="Read the coordinates sheet of a workbook.")
    parser.add_argument("xlsx")
    parser.add_argument("out_dir")
    parser.add_argument("--sheet", default="Coordinates")
    args = parser.parse_args(argv)
    if not Path(args.xlsx).is_file():
        print("Workbook not found: " + args.xlsx, file=sys.stderr)
        return 2
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
