"""The second check of a scenario run, by a route that shares no code with the reader: the workbooks of the folder read straight with openpyxl.

Usage: second_check.py S1 [S2 ...]. For each scenario it takes the plots.json of the run and, in every sheet of every workbook of the folder, the
column whose cells match the most plot numbers. The villa type codes are the part of each prototype file name between the first two dashes
after the discipline, found here by a split of the name and not by the pattern of the settings. A cell that holds one of those codes as a
whole word is the sheet's villa type of the plot. Counts only are printed and written to second_check.json beside the run, no value of a
file and no file name. The workbooks are read in place, read only, and their SHA-256 is held before and after.
"""
import json
import re
import sys
from pathlib import Path

import scenario_tools as tools
from openpyxl import load_workbook


def codes_of(folder):
    """The code of each prototype file: the token after the discipline token ARC, split on dashes."""
    found = set()
    for path in Path(folder).rglob("*.rvt"):
        parts = path.stem.split("-")
        if "ARC" in parts and parts.index("ARC") + 1 < len(parts):
            found.add(parts[parts.index("ARC") + 1])
    return found


def sheet_rows(path):
    book = load_workbook(path, read_only=True, data_only=True)
    try:
        for sheet in book.worksheets:
            yield sheet.title, [list(row) for row in sheet.iter_rows(values_only=True)]
    finally:
        book.close()


def text_of(cell):
    if cell is None:
        return ""
    if isinstance(cell, float) and cell.is_integer():
        return str(int(cell))
    return str(cell).strip()


def whole_word_code(text, codes):
    """The one code a cell holds as a whole word, else None."""
    words = set(re.split(r"[^A-Za-z0-9]+", text))
    hits = words & codes
    return next(iter(hits)) if len(hits) == 1 else None


def check_sheet(rows, plots, codes):
    """Best key column of a sheet and, for the best type column, the plots that agree, differ and have no code in the sheet."""
    best = None
    width = max((len(row) for row in rows), default=0)
    for key in range(width):
        keyed = {}
        for row in rows:
            if key < len(row) and text_of(row[key]) in plots:
                keyed.setdefault(text_of(row[key]), row)
        if keyed and (best is None or len(keyed) > len(best[1])):
            best = (key, keyed)
    if best is None:
        return None
    key, keyed = best
    result = {"keyed": len(keyed), "agree": 0, "differ": 0, "none": 0, "column": None}
    for column in range(width):
        if column == key:
            continue
        agree = differ = none = 0
        for number, row in keyed.items():
            code = whole_word_code(text_of(row[column]), codes) if column < len(row) else None
            mine = plots[number]
            if code is None:
                none += 1
            elif mine == code:
                agree += 1
            else:
                differ += 1
        if agree + differ > result["agree"] + result["differ"]:
            result.update({"agree": agree, "differ": differ, "none": none, "column": column})
    return result


def run_one(name, root):
    folder = root / name
    out = root / "runs" / name
    plots = {row["number"]: row["villaType"] for row in json.loads((out / "plots.json").read_text(encoding="utf-8"))["plots"]}
    codes = codes_of(folder)
    before = {path: tools.sha256(path) for path in sorted(folder.rglob("*.xlsx"))}
    sheets = []
    for path in before:
        for title, rows in sheet_rows(path):
            result = check_sheet(rows, plots, codes)
            if result and result["keyed"] > 0:
                sheets.append(result)
    unchanged = all(tools.sha256(path) == digest for path, digest in before.items())
    best = max(sheets, key=lambda item: item["keyed"], default=None)
    kinds = {}
    for code in plots.values():
        kinds[code] = kinds.get(code, 0) + 1
    report = {
        "scenario": name, "plotsRead": len(plots), "prototypeCodes": len(codes), "workbooks": len(before), "workbooksUnchanged": unchanged,
        "sheetsJoined": len(sheets), "bestSheet": best, "readTypeCounts": dict(sorted(kinds.items(), key=lambda item: str(item[0]))),
    }
    (out / "second_check.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    print("%s: %d plots read, %d workbooks (unchanged: %s), %d sheets join the plots" % (name, len(plots), len(before), unchanged, len(sheets)))
    if best:
        print("  best sheet holds %d of the read plots, villa type agrees on %d, differs on %d, no code on %d" % (best["keyed"], best["agree"], best["differ"], best["none"]))
    return report


def main(argv=None):
    names = list(argv if argv is not None else sys.argv[1:]) or sorted(tools.SCENARIOS)
    root = tools.work_root() / "scenarios"
    for name in names:
        run_one(name, root)
    return 0


if __name__ == "__main__":
    sys.exit(main())
