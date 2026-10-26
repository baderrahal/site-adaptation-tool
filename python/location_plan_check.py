"""Checks the location plan a second way (R184), with plain math and separate code.

The page is fitted to the site again on the IMD plots, not the IMC plots of R181, by normal equations solved here. For 20
code labels at seed 14 and 20 at seed 7: the raw characters near the label must spell the code with the number under it,
the number goes through the second fit and a ray cast to its baseplan face, and the Rev 9 typology of that number, read
from the sales sheet here, must be a new code the old to new table gives for the base of the old code. Each is compared
with labels.csv and counted.
"""
import argparse
import json
import random
import re
import sys
from collections import Counter
from pathlib import Path

import openpyxl
import pdfplumber

import c3d_run
import data_book
import data_book_analysis
import data_book_check
import link_summary
import project_profile


def solve(a, b):
    """Gauss elimination for a small square system."""
    n = len(a)
    m = [list(map(float, row)) + [float(v)] for row, v in zip(a, b)]
    for col in range(n):
        pivot = max(range(col, n), key=lambda r: abs(m[r][col]))
        m[col], m[pivot] = m[pivot], m[col]
        for r in range(n):
            if r != col and m[col][col]:
                f = m[r][col] / m[col][col]
                m[r] = [x - f * y for x, y in zip(m[r], m[col])]
    return [m[r][n] / m[r][r] for r in range(n)]


def fit(page, site):
    """The affine map page to site, one least squares fit for east and one for north."""
    rows = [(x, y, 1.0) for x, y in page]
    ata = [[sum(r[i] * r[j] for r in rows) for j in range(3)] for i in range(3)]
    east = solve(ata, [sum(r[i] * s[0] for r, s in zip(rows, site)) for i in range(3)])
    north = solve(ata, [sum(r[i] * s[1] for r, s in zip(rows, site)) for i in range(3)])
    return east, north


def to_site(transform, x, y):
    east, north = transform
    return east[0] * x + east[1] * y + east[2], north[0] * x + north[1] * y + north[2]


def spelled(chars, x, y, code, number, window):
    """True when the characters within window of the label point hold the code on one line and the number on the next."""
    near = sorted((c for c in chars if abs(c["x0"] - x) <= window and abs(c["top"] - y) <= window), key=lambda c: (round(c["top"], 1), c["x0"]))
    lines = {}
    for c in near:
        lines.setdefault(round(c["top"], 1), []).append(c["text"])
    texts = ["".join(t).replace(" ", "") for t in lines.values()]
    want_code, want_number = code.replace(" ", ""), number
    return any(want_code in t for t in texts) and any(re.search(r"(^|\D)" + want_number + r"($|\D)", t) for t in texts)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Second method for the location plan (R184)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    book, connect = config["dataBook"], config["connect"]
    plan = book["locationPlan"]
    check = plan["check"]
    root = Path(book["root"])
    labels = data_book_analysis.read_csv(root / plan["outFolder"] / "labels.csv")
    faces = data_book_check.faces_by_label(link_summary.load(connect["baseplanPlots"])["plots"])
    settings = project_profile.load_settings(project_profile.SETTINGS_PATH)
    schedules = data_book.schedule_rows(settings, connect)
    shift = connect["neighbourhoods"][check["fitNeighbourhood"]]["shift"]
    by_number = {}
    for row in labels:
        by_number.setdefault(row["number"], []).append(row)
    page, site = [], []
    for name, official in schedules:
        if name != check["fitNeighbourhood"]:
            continue
        rings = faces.get(str(int(official) - shift)) or []
        found = by_number.get(official) or []
        if len(rings) == 1 and len(found) == 1:
            page.append((float(found[0]["pageX"]), float(found[0]["pageY"])))
            site.append(data_book_check.centroid(rings[0]))
    transform = fit(page, site)
    with pdfplumber.open(root / "work" / plan["pdf"]) as pdf:
        chars = [c for c in pdf.pages[0].chars if plan["labelSize"][0] <= c["size"] <= plan["labelSize"][1]]
    workbook = openpyxl.load_workbook(root / "work" / book["rev9"]["workbooks"][plan["sales"]["workbook"]], read_only=True, data_only=True)
    sheet = list(workbook[plan["sales"]["sheet"]].iter_rows(values_only=True))
    header = [str(c).strip() if c else "" for c in sheet[0]]
    plot_col, type_col = header.index(plan["sales"]["columns"]["plot"]), header.index(plan["sales"]["columns"]["typology"])
    rev9 = {str(int(r[plot_col])): r[type_col] for r in sheet[1:] if isinstance(r[plot_col], (int, float))}
    table = list(workbook[plan["codeTable"]["sheet"]].iter_rows(values_only=True))
    head = [str(c).strip() if c else "" for c in table[plan["codeTable"]["headerRow"] - 1]]
    old_col, new_col = head.index(plan["codeTable"]["columns"]["old"]), head.index(plan["codeTable"]["columns"]["new"])
    news_of = {}
    for r in table[plan["codeTable"]["headerRow"]:]:
        if r[old_col] and r[new_col]:
            news_of.setdefault(re.split(r"[ (-]", str(r[old_col]).strip())[0], set()).add(str(r[new_col]).strip())
    every_new = {n for v in news_of.values() for n in v}
    codes = sorted((r for r in labels if r["kind"] == "clientCode"), key=lambda r: int(r["number"]))
    result = {"fit": {"neighbourhood": check["fitNeighbourhood"], "points": len(page)}}
    for seed in check["seeds"]:
        counts = Counter()
        for row in random.Random(seed).sample(codes, min(check["size"], len(codes))):
            counts["spelled " + ("match" if spelled(chars, float(row["pageX"]), float(row["pageY"]), row["code"], row["number"], check["windowPt"]) else "mismatch")] += 1
            x, y = to_site(transform, float(row["pageX"]), float(row["pageY"]))
            mine = [label for label, rings in faces.items() if len(rings) == 1 and data_book_check.inside(rings[0], x, y)]
            counts["face " + ("match" if (mine[0] if len(mine) == 1 else "") == (row["face"] or "") else "mismatch")] += 1
            base = re.split(r"[ (-]", row["code"])[0]
            allowed = news_of.get(base) or ({base} if base in every_new else set())
            typology = rev9.get(row["number"])
            if typology is None:
                counts["code noSalesRow"] += 1
            else:
                counts["code " + ("match" if typology in allowed and (row["rev9"] or None) == typology else "mismatch")] += 1
        result["seed%d" % seed] = dict(sorted(counts.items()))
    (root / "logs" / "location_plan_check.json").write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
