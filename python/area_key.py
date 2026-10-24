"""The area key of every plot, Zone and Neighbourhood_ID, Part A of round DATA-5 (R256).

The key comes from the PH_1-DLUS sheet of the workbook dataBook.data5.areaKey.dlus names, by plot number: the location plan
number of the face (R182), or for a face with none the number of its Rev 9 parcel (R244, parcel_matches.csv). A face with
two numbers takes their value when both agree and is listed with two values when they do not. A dash, a zero or an empty
cell is no value. The neighbourhood is written in upper case, so 01a and 01A are one key. The sales sheet of check is read
the same way and each key is marked same, differs or noSalesRow against it. The key never comes from an IM letter, the
table of config/area_codes.json only names the IM code a neighbourhood is known by. plots.csv gets the columns zone, nbhId,
areaKeyMethod and areaKeyCheck, the file before is kept in the area_key folder of data5, and area_key.json holds the counts.
"""
import argparse
import json
import re
import shutil
import sys
from collections import Counter
from pathlib import Path

import c3d_run
import data_book
import data_book_analysis
import rev9_workbooks

COLUMNS = ["zone", "nbhId", "areaKeyMethod", "areaKeyCheck"]
NO_VALUE = {"", "-", "0", "N/A", "NONE"}


def plot_text(value):
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    return str(value).strip() if value is not None else ""


def zone_text(value):
    """Zone 4, 4 and 4.0 are all Zone 4."""
    text = plot_text(value)
    if text.upper() in NO_VALUE:
        return None
    digits = re.findall(r"\d+", text)
    return "Zone %d" % int(digits[0]) if digits else text


def nbh_text(value):
    text = plot_text(value).upper()
    return None if text in NO_VALUE else text


def by_plot(rows):
    """Plot number to (zone, nbh), a row with neither left out, and the numbers with two rows that disagree."""
    found, clash = {}, set()
    for row in rows:
        plot = plot_text(row.get("plot"))
        value = (zone_text(row.get("zone")), nbh_text(row.get("nbh")))
        if not plot.isdigit() or value == (None, None):
            continue
        if plot in found and found[plot] != value:
            clash.add(plot)
        found[plot] = value
    return found, clash


def numbers_of(row, parcel):
    """The plot numbers of a face and where they came from."""
    numbers = [n for n in (row.get("locationPlanNumber") or "").split("|") if n]
    if numbers:
        return numbers, "locationPlanNumber"
    if parcel:
        return [parcel], "rev9Parcel"
    return [], "noNumber"


def key_of(numbers, table):
    """One (zone, nbh) for the numbers, or why there is none: noNumber, noRow or twoValues."""
    if not numbers:
        return None, "noNumber"
    values = {table[n] for n in numbers if n in table}
    if not values:
        return None, "noRow"
    if len(values) > 1:
        return None, "twoValues"
    return values.pop(), None


def check(numbers, key, sales):
    values = {sales[n] for n in numbers if n in sales}
    if not values:
        return "noSalesRow"
    return "same" if values == {key} else "differs"


def area_rows(plots, parcel_of, dlus, sales):
    rows = []
    for row in plots:
        numbers, method = numbers_of(row, parcel_of.get(row["baseplanLabel"]))
        key, why = key_of(numbers, dlus)
        rows.append({"baseplanLabel": row["baseplanLabel"], "clientType": row["clientType"], "numbers": "|".join(numbers),
                     "zone": key[0] if key else None, "nbhId": key[1] if key else None,
                     "areaKeyMethod": method if key else why, "areaKeyCheck": check(numbers, key, sales) if key else None})
    return rows


def summary(rows, unit_types):
    keyed = [r for r in rows if r["nbhId"] or r["zone"]]
    return {"plots": len(rows), "keyed": len(keyed), "byZoneAndNbh": {"%s %s" % k: n for k, n in sorted(Counter((r["zone"], r["nbhId"]) for r in keyed).items(), key=str)},
            "byMethod": dict(Counter(r["areaKeyMethod"] for r in rows)), "check": dict(Counter(r["areaKeyCheck"] for r in keyed)),
            "unitPlots": sum(1 for r in rows if r["clientType"] in unit_types), "unitPlotsKeyed": sum(1 for r in keyed if r["clientType"] in unit_types),
            "noValue": [r["baseplanLabel"] for r in rows if not (r["nbhId"] or r["zone"]) and r["areaKeyMethod"] != "twoValues"],
            "twoValues": [r["baseplanLabel"] for r in rows if r["areaKeyMethod"] == "twoValues"],
            "differsFromSales": [r["baseplanLabel"] for r in keyed if r["areaKeyCheck"] == "differs"]}


def sheet(root, book, entry):
    return rev9_workbooks.sheet_rows(root / "work" / book["rev9"]["workbooks"][entry["workbook"]], entry["sheet"], entry["headerRow"], entry["columns"])


def run(config, write=True):
    book = config["dataBook"]
    settings = book["data5"]["areaKey"]
    root = Path(book["root"])
    out = root / book["data5"]["outFolder"] / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    dlus, dlus_clash = by_plot(sheet(root, book, settings["dlus"]))
    sales, sales_clash = by_plot(sheet(root, book, settings["sales"]))
    plots_path = root / "plots.csv"
    plots = data_book_analysis.read_csv(plots_path)
    matches = data_book_analysis.read_csv(root / book["rev9"]["outFolder"] / "parcel_matches.csv")
    parcel_of = {m["baseplanLabel"]: m["parcel"] for m in matches if m["parcel"]}
    rows = area_rows(plots, parcel_of, dlus, sales)
    report = summary(rows, set(book["analysis"]["unitPlotTypes"]))
    report.update({"dlusPlots": len(dlus), "dlusNumbersWithTwoRows": sorted(dlus_clash), "salesPlots": len(sales), "salesNumbersWithTwoRows": sorted(sales_clash)})
    if write:
        before = out / "plots_before_area_key.csv"
        if not before.is_file():
            shutil.copyfile(plots_path, before)
        columns = list(plots[0]) + [c for c in COLUMNS if c not in plots[0]]
        for plot, found in zip(plots, rows):
            plot.update({c: found[c] for c in COLUMNS})
        data_book.write_plots(plots_path, plots, columns)
        data_book.write_plots(out / "area_key.csv", rows, list(rows[0]))
        (out / "area_key.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report, rows


def main(argv=None):
    parser = argparse.ArgumentParser(description="Zone and Neighbourhood_ID of every plot from the Rev 9 DLUS, the sales sheet as the check (R256)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    report, _ = run(c3d_run.load_config(args.config))
    print(json.dumps({k: v if not isinstance(v, list) or len(v) < 40 else "%d listed" % len(v) for k, v in report.items()}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
