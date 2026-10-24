import json
from collections import Counter

import openpyxl

import rev9_workbooks as rw

ANALYSIS = {"setbackM": {"default": 5.52, "C10": 5.5}, "widthsByClientType": {"VL3": [12, 16], "C10": [16]}}
UNITS = {"C10", "VL3", "TH1"}


def workbook(path, sheets, hidden=()):
    book = openpyxl.Workbook()
    book.remove(book.active)
    for title, rows in sheets.items():
        sheet = book.create_sheet(title)
        for row in rows:
            sheet.append(row)
        if title in hidden:
            sheet.sheet_state = "hidden"
    book.save(path)
    return path


def plot(number, kind, width="16", depth="25", setback="5.0"):
    return {"locationPlanNumber": number, "clientType": kind, "neighbourhood": "", "widthM": width, "depthM": depth, "setbackFromFrontM": setback}


# R185 inventory and diff
def test_r185_inventory_lists_every_sheet_with_state_and_rows(tmp_path):
    path = workbook(tmp_path / "a.xlsx", {"A": [["x"], [1], [None], [2]], "B": [[None], ["y"]]}, hidden=("B",))
    found = rw.inventory(path)
    assert [(s["sheet"], s["state"], s["rowsWithValues"], s["firstRow"]) for s in found["sheets"]] == [("A", "visible", 3, 1), ("B", "hidden", 1, 2)]


def test_r185_diff_counts_cells_and_plots_only_in_one_and_changed_columns(tmp_path):
    a = workbook(tmp_path / "a.xlsx", {"S": [["PLOT_NO", "PID", "NBH"], [1, None, "01A"], [2, None, "02"], [84, None, "02"]], "Only A": [[1]]})
    b = workbook(tmp_path / "b.xlsx", {"S": [["PLOT_NO", "PID", "NBH"], [1, 11, "01a"], [2, 12, "02"]], "Only B": [[1]]})
    found = rw.diff_books(a, b, ["PLOT_NO"], 3)
    assert found["onlyA"] == ["Only A"] and found["onlyB"] == ["Only B"]
    sheet = found["sheets"]["S"]
    assert sheet["onlyA"] == ["84"] and sheet["onlyB"] == [] and sheet["plotsChanged"] == 2
    assert sheet["columnsChanged"] == {"PID": 2, "NBH": 1} and sheet["cellsDiffer"] == 5


def test_r185_plain_rounds_numbers_and_strips_text():
    assert rw.plain(" a ") == "a" and rw.plain("  ") is None and rw.plain(1.00000001) == 1.0


# R186 setbacks
def test_r186_front_joined_by_number_against_the_drawn_line_and_rev6():
    dlus = [{"plot": 10, "typology": "C10", "front": 5.5, "right": "0 / 4.5", "left": "0 / 4.5", "rear": 3},
            {"plot": 11, "typology": "TH1 (QP)", "front": 7, "right": 0, "left": 0, "rear": 7},
            {"plot": 12, "typology": "C10", "front": 5.5, "right": 0, "left": 0, "rear": 3}]
    plots = [plot("10", "C10"), plot("11", "TH1", setback="3.0"), plot("12|13", "C10")]
    summary, rows = rw.setbacks(dlus, plots, [5.0, 3.0], ANALYSIS["setbackM"], 0.05, UNITS)
    assert summary["joined"] == 2 and summary["byTypology"]["C10"]["notInPlots"] == 1
    assert summary["unitFrontAgainstRev6"] == {"equal": 1, "differs": 1}
    assert summary["unitFrontAgainstDrawn"] == {"differs": 2}
    assert summary["unitDrawnClass"] == {"5.00": 1, "3.00": 1}
    assert summary["valuesByTypology"]["C10"]["right 0 / 4.5"] == 1


def test_r186_a_number_on_two_faces_joins_to_none():
    found, many = rw.plots_by_number([plot("5", "C10"), plot("5", "VL3")])
    assert found == {} and many == ["5"]


# R187 widths
def test_r187_land_width_against_cad_rev6_and_code_width():
    sales = [{"plot": 1, "typology": "C10", "typologyOld": "C10", "landWidth": 17.5, "landLength": 25},
             {"plot": 2, "typology": "VL4", "typologyOld": "VL3", "landWidth": 13.5, "landLength": 25},
             {"plot": 3, "typology": "VL3", "typologyOld": "VL3", "landWidth": 12, "landLength": 25}]
    plots = [plot("1", "C10", "17.5"), plot("2", "VL3", "13.5"), plot("3", "VL3", "12.02")]
    summary, rows = rw.widths(sales, plots, ANALYSIS["widthsByClientType"], {"C10": 16, "VL4": 12, "VL3": 12}, set(), 0.1, UNITS)
    by = {r["plot"]: r for r in rows}
    assert by["1"]["cadAgainstRev6"] == "wider" and by["2"]["cadAgainstRev6"] == "between" and by["3"]["cadAgainstRev6"] == "matches"
    assert by["1"]["widthAgainstCad"] == "equal" and by["1"]["landAgainstCode"] == "differs"
    again = rw.wide_again(rows, ["C10", "VL3"])
    assert again["C10"]["plots"] == 1 and again["C10"]["landWidthEqualsCad"] == 1 and again["C10"]["typologyRev9"] == {"C10": 1}
    assert again["VL3"]["typologyRev9"] == {"VL4": 1} and again["VL3"]["against"] == {"between": 1}


# R188 code table
def test_r188_code_table_keeps_both_code_sets_and_empty_forms(tmp_path):
    path = workbook(tmp_path / "c.xlsx", {"Codes": [[None, None, None, "Plot size (m)"], ["Typology Form", "Old", "New", "Width (m)"],
                                                    [None, "VL3-0", "VL6", 16], ["Villa", "DP4", "VD4", 12], ["Duplex", "DP4", "DP4", 12], [None, None, None, None]]})
    rows = rw.code_table(path, "Codes", 2, {"form": "Typology Form", "old": "Old", "new": "New", "widthM": "Width (m)"})
    assert [(r["form"], r["old"], r["new"]) for r in rows] == [(None, "VL3-0", "VL6"), ("Villa", "DP4", "VD4"), ("Duplex", "DP4", "DP4")]
    block = rw.profile_codes(rows, "c.xlsx")
    assert block["rows"][0]["old"] == "VL3-0" and block["rows"][0]["new"] == "VL6"
    profile = [{"form": r["form"], "old": r["old"], "new": r["new"], "width_m": r["widthM"]} for r in rows]
    assert rw.profile_against_sheet(profile, rows)["equal"] == 3
    profile[0]["new"] = "VL3"
    assert rw.profile_against_sheet(profile, rows)["equal"] == 2


def test_r188_checks_against_the_location_plan_by_base_and_typology_rev9():
    table = [{"old": "DP4", "new": "VD4"}, {"old": "DP4", "new": "DP4"}, {"old": "VL3-0", "new": "VL6"}]
    labels = [{"kind": "clientCode", "code": "DP4 (SA)", "newCode": "VD4"}, {"kind": "clientCode", "code": "VL3-0", "newCode": "VL6"},
              {"kind": "clientCode", "code": "XX9", "newCode": "XX9"}, {"kind": "openSpace", "code": "OS", "newCode": ""}]
    pattern = r"(?P<base>[A-Z]{1,3}\d{1,2}(?:-0|-IM)?)(?:\s?\(|-)?(?P<suffix>BB|SA|QP)?\)?"
    found = rw.code_checks(table, labels, Counter({"VD4": 3, "VL6": 1, "TH9": 1}), pattern)
    assert found["oldWithTwoNew"] == ["DP4"] and found["locationCodesInTable"] == ["VL3-0"]
    assert found["locationCodesByBase"] == {"DP4 (SA)": 1} and found["locationCodesNotInTable"] == {"XX9": 1}
    assert found["locationPairsInTable"] == 2 and found["typologyRev9NotNew"] == {"TH9": 1}


def test_r188_the_profile_holds_the_table_with_both_codes():
    profile = json.loads((rw.REPO / rw.config_profile()).read_text(encoding="utf-8"))
    rows = profile["typology_codes"]["rows"]
    assert len(rows) == 18 and {"old", "new", "form", "width_m"} <= set(rows[0])
    assert ("VL3-0", "VL6") in {(r["old"], r["new"]) for r in rows} and ("DP4", "VD4") in {(r["old"], r["new"]) for r in rows}
