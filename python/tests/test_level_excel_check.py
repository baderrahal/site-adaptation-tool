"""R364. The second route of the level Excel, python/level_excel_check.py, on a small workbook written here with openpyxl in the layout the
worker writes, from a made up plan, override list, step clusters and plots.csv (R10). Each planted fault is caught."""
import hashlib
import json

import openpyxl
import pytest
from openpyxl.styles import Font, PatternFill

import level_excel_check as check


def levels(road, entry, ffl, ssl, height, steps):
    return {key: {"value": value} for key, value in (("ffl", ffl), ("ssl", ssl), ("entry", entry), ("height", height), ("steps", steps), ("roadAtFrontM", road))}


def plot(number, unit, lv, *applied):
    item = {"plot": number, "unit": {"value": unit}, "levels": lv}
    if applied:
        item["overrides"] = {"applied": list(applied), "notApplied": {}}
    return item


def entry(number, field, value, reason, source="parsons"):
    return {"plot": number, "field": field, "value": value, "source": source, "model": "model_%s.rvt" % source, "key": "k1", "reason": reason}


PLAN = {"neighbourhood": "012", "summary": {"roadSurfacesOfFrontCorners": {"Road A": 6}, "rules": {"levelProfile": "parsons2024Rev6", "overrideSource": ["parsons"], "overrideModel": []}},
        "plots": [plot("101", "VD4", levels(9.9012, 9.9234, 10.123, 10.048, 0.1996, 1), "heightM", "parameter CASE"),
                  plot("102", "DP1", levels(10.3801, 10.4, 10.5, 10.425, 0.1, 1), "entryM", "rampFrontM"),
                  plot("103", "DP1", levels(10.2857, 10.3115, 10.5, 10.425, 0.1885, 2))],
        "missing": [{"plot": "104", "written": False, "missing": [{"key": "levels.ffl", "why": "no road level"}]}]}
OVERRIDES = {"overrides": [entry("101", "heightM", {"gapToRuleM": 0.0006}, "breaks oneFflPerPieceAtTheHighestEntry"),
                           entry("101", "heightM", {"gapToRuleM": 0.5}, "native", "native"), entry("101", "parameter CASE", "D", "no level field"),
                           entry("102", "entryM", {"gapToRuleM": 0.0123}, "breaks roadLevel"), entry("102", "rampFrontM", {"gapToRuleM": -0.002}, "breaks roadLevel")]}
CLUSTERS = {"plots": {key: {"written": {"plots": key.split("|")}} for key in ("102|103", "104|105")}}
RULES = {"units_mm": True, "levels": {"ground_ssl": -75}, "levelProfiles": {"parsons2024Rev6": {"frontRoad": {"surface": "roadLevel"},
         "keyRules": {"ffl": {"rule": "oneFflPerPieceAtTheHighestEntry"}, "levelSplit": {"rule": "units", "table": {"DP1 DP1": "middle"}}}}}}
CONFIG = {"siteBuild": {"roadSurfaces": ["Road A", "Road B"]}, "placerCheck": {"level": {"passMm": 1.0}}}
FILL = PatternFill(fill_type="solid", fgColor="FFDDEBF7")


@pytest.fixture
def made(tmp_path):
    paths = {}
    for name, content in (("plan", PLAN), ("overrides", OVERRIDES), ("clusters", CLUSTERS), ("rules", RULES), ("config", CONFIG)):
        paths[name] = tmp_path / (name + ".json")
        paths[name].write_text(json.dumps(content), encoding="utf-8")
    paths["plots"] = tmp_path / "plots.csv"
    paths["plots"].write_text("locationPlanNumber,zone\n101,Zone 2\n102,Zone 2\n103,Zone 2\n104,Zone 2\n", encoding="utf-8")
    mock = check.mock_labels()
    want = check.expected(check.load_inputs(paths["plan"], paths["overrides"], paths["clusters"], paths["plots"], paths["rules"], paths["config"]), "parsons2024Rev6", mock)
    want["plan"] = PLAN
    return paths, mock, want


def write_book(path, want, mock, change=None):
    """The workbook as the worker lays it out, change applied to it before it is saved."""
    book = openpyxl.Workbook()
    levels = book.active
    levels.title = mock["sheets"][0]
    levels.append([want["topLine"] + want["formulas"]])
    levels["A1"].font = Font(bold=True)
    levels.merge_cells("A1:J1")
    levels.append(mock["columns"])
    for column, name in enumerate(mock["columns"], 1):
        if name in check.EDITABLE:
            levels.cell(row=2, column=column).fill = FILL
    for row in want["levels"]:
        levels.append(row)
        for column, name in enumerate(mock["columns"], 1):
            if name in check.PLAN_KEYS:
                levels.cell(row=levels.max_row, column=column).number_format = check.number_format(mock["decimals"].get(name, 0))
    for name, header, rows in ((mock["sheets"][1], check.CLUSTER_COLUMNS, want["clusters"]), (mock["sheets"][2], check.OVERRIDE_COLUMNS, want["used"])):
        sheet = book.create_sheet(name)
        sheet.append(list(header))
        for row in rows:
            sheet.append(row)
    settings = book.create_sheet(mock["sheets"][3])
    settings.append(["Setting", "Value"])
    values = dict(want["settings"], **{"Run id": "r1", "Run time": "2026-10-28T10:00:00Z", "Plan build id": "r1"})
    for name in check.SETTINGS:
        settings.append([name, values[name]])
    if change:
        change(book)
    book.save(path)


def test_r364_the_mock_labels_read_from_both_mock_files_agree_on_the_ten_columns_the_four_sheets_and_the_decimals():
    mock = check.mock_labels()
    assert mock["columns"] == ["Plot No", "Unit", "Cluster", "Road at front", "El @ Entry", "FFL", "SSL", "Height", "Steps", "Source"]
    assert mock["sheets"] == ["Levels", "Clusters", "Overrides used", "Settings"]
    assert mock["file"] == "<NBH>_Levels_<profile>.xlsx"
    assert mock["decimals"] == {"Road at front": 4, "El @ Entry": 4, "Height": 4, "FFL": 3, "SSL": 3}
    assert mock["formulas"].startswith("El @ Entry = the road at the two front corners") and "SSL = FFL - 0.075 m" in mock["formulas"]


def test_r364_the_cells_are_worked_out_again_from_the_plan_the_list_and_the_clusters(made):
    _, _, want = made
    assert want["levels"][0] == ["101", "VD4", "none", 9.9012, 9.9234, 10.123, 10.048, 0.1996, 1, "Override heightM (breaks oneFflPerPieceAtTheHighestEntry)"]
    assert want["levels"][1][-1] == "Override entryM (breaks roadLevel), rampFrontM (breaks roadLevel)"
    assert want["levels"][2][-1] == "Rule" and want["levels"][3] == ["104", None, "104-105", None, None, None, None, None, None, "Not written: levels.ffl (no road level)"]
    assert want["clusters"] == [["102-103", "102-103", "DP1 DP1", "middle", 10.5]]
    assert [row[:3] for row in want["used"]] == [["101", "heightM", 0.0006], ["102", "entryM", 0.0123], ["102", "rampFrontM", -0.002]]
    assert want["topLine"] == "NBH 012, Zone 2, road surface Road A on 6 of 6 front corners, formulas: "
    assert want["settings"]["Plan SHA-256"] == hashlib.sha256(made[0]["plan"].read_bytes()).hexdigest()
    assert want["gaps"]["level overrides with one entries"] == 3


def test_r364_a_workbook_as_the_worker_lays_it_out_holds_every_check(made, tmp_path):
    _, mock, want = made
    path = tmp_path / "012_Levels_parsons2024Rev6.xlsx"
    write_book(path, want, mock)
    result, bad = check.check_xlsx(path, want, mock)
    assert bad == [] and sum(count for key, count in result.items() if key.startswith("match ")) > 80


def plant(name):
    def setter(cell, value):
        return lambda book: setattr(cell(book), "value", value)
    return {
        "label": setter(lambda book: book["Levels"]["D2"], "Road level"),
        "cell": setter(lambda book: book["Levels"]["F3"], 10.124),
        "source": setter(lambda book: book["Levels"]["J5"], "Override heightM"),
        "key": setter(lambda book: book["Levels"]["A4"], "101"),
        "fill": lambda book: setattr(book["Levels"]["H2"], "fill", FILL),
        "unfill": lambda book: setattr(book["Levels"]["I2"], "fill", PatternFill()),
        "merge": lambda book: book["Levels"].merge_cells("A3:B3"),
        "blank": lambda book: book["Levels"].insert_rows(4),
        "format": lambda book: setattr(book["Levels"]["F3"], "number_format", "0.0000"),
        "sha": setter(lambda book: book["Settings"]["B11"], "0" * 64),
        "editable": setter(lambda book: book["Settings"]["B9"], "FFL, Steps"),
        "order": lambda book: book.move_sheet("Settings", offset=-1),
        "cluster": setter(lambda book: book["Clusters"]["D2"], "none"),
    }[name]


@pytest.mark.parametrize("fault", ["label", "cell", "source", "key", "fill", "unfill", "merge", "blank", "format", "sha", "editable", "order", "cluster"])
def test_r364_each_planted_fault_is_a_mismatch(made, tmp_path, fault):
    _, mock, want = made
    path = tmp_path / "planted.xlsx"
    write_book(path, want, mock, plant(fault))
    result, bad = check.check_xlsx(path, want, mock)
    assert bad, fault


def test_r364_the_plan_side_counts_a_level_override_the_list_lacks(made, tmp_path):
    paths, mock, _ = made
    lacking = dict(OVERRIDES, overrides=OVERRIDES["overrides"][:4])
    paths["overrides"].write_text(json.dumps(lacking), encoding="utf-8")
    want = check.expected(check.load_inputs(paths["plan"], paths["overrides"], paths["clusters"], paths["plots"], paths["rules"], paths["config"]), "parsons2024Rev6", mock)
    assert want["gaps"]["level overrides with no entries"] == 1
    assert want["levels"][1][-1] == "Override entryM (breaks roadLevel), rampFrontM (no entry in the override list)"
