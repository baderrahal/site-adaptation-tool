"""The second check of a scenario run, by a route that shares no code with the reader: the workbooks of the folder read straight with openpyxl.

Usage: second_check.py [--confirmed] S1 [S2 ...], --confirmed for the runs on a confirmed profile. For each scenario it finds in the workbooks the sheet that lists the plots, without looking at the plots the
run read: a column of whole numbers for the plot, a column whose cells hold a prototype code as a whole word for the villa type, and a column of
a few repeated words for the neighbourhood. It counts the plots of the sheet for each neighbourhood and each villa type, then counts what the
run read the same way, and writes the gap. A plot read that the sheet does not list is an over read, a plot of the sheet the run did not read
is an under read, and both show. The villa type codes are the part of each prototype file name after the architecture token, found here by a
split of the name and not by the pattern of the settings. Counts only are printed and written to second_check.json beside the run, no value of
a file and no file name. The workbooks are read in place, read only, and their SHA-256 is held before and after.
"""
import collections
import json
import re
import sys
import warnings
from pathlib import Path

import scenario_tools as tools
from openpyxl import load_workbook

warnings.simplefilter("ignore")
MIN_ROWS = 100
MAX_NEIGHBOURHOODS = 300


def codes_of(folder):
    """The code of each prototype file: the token after the discipline token ARC, split on dashes."""
    found = set()
    for path in Path(folder).rglob("*.rvt"):
        parts = path.stem.split("-")
        if "ARC" in parts and parts.index("ARC") + 1 < len(parts):
            found.add(parts[parts.index("ARC") + 1])
    return found


def text_of(cell):
    if cell is None:
        return ""
    if isinstance(cell, float) and cell.is_integer():
        return str(int(cell))
    return str(cell).strip()


def digits_of(text):
    """The run of digits of a plot number, so a number in another style is counted as the same plot."""
    found = re.findall(r"\d+", text)
    return found[-1] if found else None


def code_of(text, codes):
    hits = set(re.split(r"[^A-Za-z0-9]+", text)) & codes
    return next(iter(hits)) if len(hits) == 1 else None


def sheets_of(folder):
    for path in sorted(Path(folder).rglob("*.xlsx")):
        book = load_workbook(path, read_only=True, data_only=True)
        try:
            for sheet in book.worksheets:
                yield [list(row) for row in sheet.iter_rows(values_only=True)]
        finally:
            book.close()


def header_of(rows):
    """The texts of the first row with three cells or more, lower case, and the rows under it."""
    for at, row in enumerate(rows):
        if sum(1 for cell in row if text_of(cell)) >= 3:
            return [text_of(cell).lower() for cell in row], rows[at + 1:]
    return [], []


def plot_list(rows, codes):
    """The best listing of plots in a sheet: key column, type column and neighbourhood column, with the plots of every row, none left out.
    The key is a column whose header says plot, the neighbourhood a column whose header says neighbourhood or nbh, found from the header
    alone and not from the plots the run read."""
    header, rows = header_of(rows)
    width = len(header)
    best = None
    for key in range(width):
        if "plot" not in header[key]:
            continue
        keyed = {}
        for row in rows:
            digits = digits_of(text_of(row[key])) if key < len(row) else None
            if digits is not None and digits not in keyed:
                keyed[digits] = row
        if len(keyed) < MIN_ROWS:
            continue
        for typed in range(width):
            if typed == key:
                continue
            given = {number: code_of(text_of(row[typed]), codes) for number, row in keyed.items() if typed < len(row)}
            given = {number: code for number, code in given.items() if code}
            if len(given) < MIN_ROWS or (best is not None and len(given) <= len(best["codes"])):
                continue
            area = None
            for column in range(width):
                if column in (key, typed) or not re.search(r"neighbou?rhood|nbh", header[column]):
                    continue
                values = {number: text_of(row[column]) for number, row in keyed.items() if column < len(row) and text_of(row[column])}
                if 2 <= len(set(values.values())) <= MAX_NEIGHBOURHOODS and (area is None or len(values) > len(area)):
                    area = values
            best = {"keys": set(keyed), "codes": given, "area": area or {}}
    return best


def run_one(name, root, confirmed=False):
    # A confirmed run reads the same folder as its first run, so it is held against the same workbooks
    folder = root / name.replace("_confirmed", "")
    out = root / "runs" / (name + tools.CONFIRMED_RUN if confirmed else name)
    read = {}
    for row in json.loads((out / "plots.json").read_text(encoding="utf-8"))["plots"]:
        digits = digits_of(row["number"]) if row["number"] is not None else None
        if digits is not None:
            read[digits] = row["villaType"]
    codes = codes_of(folder)
    before = {path: tools.sha256(path) for path in sorted(folder.rglob("*.xlsx"))}
    listing = None
    for rows in sheets_of(folder):
        found = plot_list(rows, codes)
        if found and (listing is None or len(found["codes"]) > len(listing["codes"])):
            listing = found
    unchanged = all(tools.sha256(path) == digest for path, digest in before.items())
    report = {"scenario": name, "plotsRead": len(read), "workbooks": len(before), "workbooksUnchanged": unchanged, "sheetFound": listing is not None}
    if listing:
        sheet_keys = listing["keys"]
        only_sheet = len(sheet_keys - set(read))
        only_read = len(set(read) - sheet_keys)
        by_code_sheet = collections.Counter(listing["codes"].values())
        by_code_read = collections.Counter(code for code in read.values() if code)
        by_area_sheet = collections.Counter(listing["area"].get(number) for number in sheet_keys if listing["area"].get(number))
        by_area_read = collections.Counter(listing["area"].get(number) for number in read if listing["area"].get(number))
        report.update({
            "sheetPlots": len(sheet_keys), "underRead": only_sheet, "overRead": only_read,
            "villaTypesInSheet": len(by_code_sheet), "villaTypeCountGap": sum(abs(by_code_sheet[c] - by_code_read[c]) for c in set(by_code_sheet) | set(by_code_read)),
            "neighbourhoodsInSheet": len(by_area_sheet), "neighbourhoodCountGap": sum(abs(by_area_sheet[a] - by_area_read[a]) for a in set(by_area_sheet) | set(by_area_read)),
            "plotsInSheetWithATypeCode": len(listing["codes"]), "plotsReadWithAType": sum(1 for code in read.values() if code),
        })
        # The villa types of each neighbourhood, the plots of the sheet against what the run read for those same plots
        area_type_sheet = collections.Counter((listing["area"][number], code) for number, code in listing["codes"].items() if number in listing["area"])
        area_type_read = collections.Counter((listing["area"][number], read[number]) for number in listing["codes"] if number in listing["area"] and read.get(number))
        report.update({
            "neighbourhoodTypePairs": len(area_type_sheet),
            "neighbourhoodTypeGap": sum(abs(area_type_sheet[key] - area_type_read[key]) for key in set(area_type_sheet) | set(area_type_read)),
            "plotsCompared": sum(area_type_sheet.values()),
        })
        # The area of each plot in the plot table of the run, against the sheet: the same plots together in one area, whatever each side calls it
        table = out / "read_plotTable.json"
        rows = json.loads(table.read_text(encoding="utf-8"))["plots"] if table.is_file() else []
        read_area = {digits_of(row["officialNumber"]): row.get("nbhId") for row in rows}
        pairs = collections.Counter((listing["area"][number], read_area[number]) for number in listing["area"] if read_area.get(number))
        best_of = {}
        for (sheet_area, read_name), count in pairs.items():
            best_of[sheet_area] = max(best_of.get(sheet_area, 0), count)
        report.update({"areasRead": len({value for value in read_area.values() if value}), "plotsInTheirAreasMostCommonReadArea": sum(best_of.values()), "plotsWithBothAreas": sum(pairs.values())})
    (out / "second_check.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    print("%s: %d plots read, %d workbooks (unchanged: %s)" % (name, len(read), len(before), unchanged))
    if listing:
        print("  sheet lists %d plots in %d neighbourhoods and %d villa types, %d under read, %d over read" % (
            report["sheetPlots"], report["neighbourhoodsInSheet"], report["villaTypesInSheet"], report["underRead"], report["overRead"]))
        print("  count gap over neighbourhoods %d plots, over villa types %d plots" % (report["neighbourhoodCountGap"], report["villaTypeCountGap"]))
        print("  villa types per neighbourhood: %d pairs over %d plots, gap %d plots" % (report["neighbourhoodTypePairs"], report["plotsCompared"], report["neighbourhoodTypeGap"]))
        print("  areas: %d on the sheet, %d read, %d of %d plots lie in the read area most of their sheet area share" % (report["neighbourhoodsInSheet"], report["areasRead"], report["plotsInTheirAreasMostCommonReadArea"], report["plotsWithBothAreas"]))
    else:
        print("  no sheet lists the plots by whole numbers with a villa type code")
    return report


def main(argv=None):
    words = list(argv if argv is not None else sys.argv[1:])
    confirmed = "--confirmed" in words
    names = [word for word in words if word != "--confirmed"] or sorted(tools.SCENARIOS)
    root = tools.work_root() / "scenarios"
    for name in names:
        run_one(name, root, confirmed)
    return 0


if __name__ == "__main__":
    sys.exit(main())
