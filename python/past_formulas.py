"""Typed values and formulas of the past schedules, Part X of round DATA-4 (R247).

Each schedule copy is opened as its raw xlsx XML with zipfile. The table named by dataBook.pastFormulas.table gives the
header row and the data rows. For every row with a plot number and each column of columns, the cell is a formula (its own
or a shared formula of a master cell), a typed value or empty. A formula is put in a pattern by writing each A1 reference
relative to its cell (R[dr]C[dc]) and each structured reference as written, and its references are named by the table
header of their column. The calculated column formulas of the table, the external links and the note texts of the sheet
are listed too. Counts go to past_formulas.json in the data book.
"""
import argparse
import json
import re
import sys
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from xml.etree import ElementTree

import c3d_run
import data3_check

MAIN = data3_check.MAIN
A1 = re.compile(r"(?<![A-Za-z_\[])(\$?)([A-Z]{1,3})(\$?)(\d+)(?![\w(\]])")


def column_letters(index):
    letters = ""
    index += 1
    while index:
        index, rest = divmod(index - 1, 26)
        letters = chr(65 + rest) + letters
    return letters


def cell_index(reference):
    found = re.fullmatch(r"([A-Z]+)(\d+)", reference)
    return data3_check.column_index(reference), int(found.group(2))


def pattern(formula, at):
    """The formula with every A1 reference written relative to the cell at, as R[dr]C[dc], a fixed one as R<r>C<c>."""
    column, row = cell_index(at)

    def relative(match):
        fixed_column, letters, fixed_row, number = match.groups()
        target = data3_check.column_index(letters + "1")
        c = "C%d" % (target + 1) if fixed_column else "C[%d]" % (target - column)
        r = "R%s" % number if fixed_row else "R[%d]" % (int(number) - row)
        return r + c
    return A1.sub(relative, formula)


def references(formula, at, header_of):
    """The table headers of the columns an A1 reference points to, and the structured references by name."""
    found = []
    for match in A1.finditer(formula):
        target = data3_check.column_index(match.group(2) + "1")
        found.append("%s row %+d" % (header_of.get(target, column_letters(target)), int(match.group(4)) - cell_index(at)[1]))
    found += ["[%s]" % name for name in re.findall(r"\[\[#This Row\],\[([^\]]+)\]\]|\[@\[?([^\]]+)\]?\]", formula) for name in name if name]
    return found


def shared_text(master_formula, master_at, at):
    """A shared formula at a cell: the master formula moved by the offset from the master cell."""
    dc = cell_index(at)[0] - cell_index(master_at)[0]
    dr = cell_index(at)[1] - cell_index(master_at)[1]

    def move(match):
        fixed_column, letters, fixed_row, number = match.groups()
        column = data3_check.column_index(letters + "1") + (0 if fixed_column else dc)
        row = int(number) + (0 if fixed_row else dr)
        return fixed_column + column_letters(column) + fixed_row + str(row)
    return A1.sub(move, master_formula)


def sheet_cells(book, target):
    """Every cell of a sheet with its value, its type and its formula, shared formulas resolved from their master."""
    cells, masters, pending = {}, {}, []
    with book.open(target) as handle:
        for _, element in ElementTree.iterparse(handle):
            if element.tag != MAIN + "c":
                continue
            at = element.get("r")
            formula = element.find(MAIN + "f")
            value = element.find(MAIN + "v")
            item = {"type": element.get("t"), "value": value.text if value is not None else None, "formula": None, "shared": False}
            if formula is not None:
                if formula.get("t") == "shared":
                    item["shared"] = True
                    if formula.text:
                        masters[formula.get("si")] = (formula.text, at)
                        item["formula"] = formula.text
                    else:
                        pending.append((at, formula.get("si")))
                else:
                    item["formula"] = formula.text or ""
            cells[at] = item
            element.clear()
    for at, si in pending:
        if si in masters:
            cells[at]["formula"] = shared_text(masters[si][0], masters[si][1], at)
    return cells


def read_book(path, settings):
    """The table rows of one schedule: for each wanted column the kind of every cell, its patterns and references."""
    with zipfile.ZipFile(path) as book:
        names = set(book.namelist())
        strings = []
        if "xl/sharedStrings.xml" in names:
            for item in ElementTree.fromstring(book.read("xl/sharedStrings.xml")).iter(MAIN + "si"):
                strings.append("".join(t.text or "" for t in item.iter(MAIN + "t")))
        tables = [n for n in names if n.startswith("xl/tables/")]
        table = None
        for name in tables:
            root = ElementTree.fromstring(book.read(name))
            if root.get("displayName") == settings["table"]:
                table = root
        if table is None:
            raise ValueError("%s has no table %s" % (path.name, settings["table"]))
        calculated = {c.get("name"): (c.findtext(MAIN + "calculatedColumnFormula") or None) for c in table.iter(MAIN + "tableColumn")}
        externals = sorted(n for n in names if n.startswith("xl/externalLinks/"))
        first, last = table.get("ref").split(":")
        cells = sheet_cells(book, "xl/worksheets/sheet1.xml")

    def text(item):
        if item is None:
            return None
        if item["type"] == "s" and item["value"] is not None:
            return strings[int(item["value"])]
        return item["value"]
    c0, r0 = cell_index(first)
    c1, r1 = cell_index(last)
    header = {c: (text(cells.get(column_letters(c) + str(r0))) or "").strip() for c in range(c0, c1 + 1)}
    column_of = {name: c for c, name in header.items()}
    plot_column = column_of[settings["plotColumn"]]
    result = {"file": path.name, "table": table.get("ref"), "rows": 0, "externalLinks": externals,
              "calculatedColumns": {k: v for k, v in calculated.items() if v}, "notes": [], "columns": {}}
    for note in settings["noteCells"]:
        found = text(cells.get(note))
        if found:
            result["notes"].append(found.strip())
    per = {name: {"formula": 0, "typed": 0, "empty": 0, "patterns": Counter(), "references": Counter()} for name in settings["columns"]}
    for r in range(r0 + 1, r1 + 1):
        if text(cells.get(column_letters(plot_column) + str(r))) in (None, ""):
            continue
        result["rows"] += 1
        for name in settings["columns"]:
            at = column_letters(column_of[name]) + str(r)
            item = cells.get(at)
            entry = per[name]
            if item is None or (item["formula"] is None and item["value"] is None):
                entry["empty"] += 1
            elif item["formula"] is not None:
                entry["formula"] += 1
                entry["patterns"][pattern(item["formula"], at)] += 1
                entry["references"].update(references(item["formula"], at, header))
            else:
                entry["typed"] += 1
    for name, entry in per.items():
        result["columns"][name] = {"formula": entry["formula"], "typed": entry["typed"], "empty": entry["empty"],
                                   "patterns": dict(entry["patterns"].most_common()), "references": dict(entry["references"].most_common()),
                                   "tableFormula": calculated.get(name)}
    return result


def run(config):
    book = config["dataBook"]
    settings = book["pastFormulas"]
    root = Path(book["root"])
    report = {"books": [read_book(root / "work" / rel, settings) for rel in settings["workbooks"]]}
    totals = defaultdict(Counter)
    for entry in report["books"]:
        for name, column in entry["columns"].items():
            totals[name].update({k: column[k] for k in ("formula", "typed", "empty")})
    report["rows"] = sum(b["rows"] for b in report["books"])
    report["totals"] = {k: dict(v) for k, v in totals.items()}
    out = root / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "past_formulas.json").write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Typed values and formulas of El @ Entry, FFL and Height in the past schedules (R247)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config))
    print(json.dumps({"rows": report["rows"], "totals": report["totals"],
                      "patterns": {b["file"]: {n: c["patterns"] for n, c in b["columns"].items()} for b in report["books"]},
                      "externalLinks": {b["file"]: b["externalLinks"] for b in report["books"]}}, indent=1, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
