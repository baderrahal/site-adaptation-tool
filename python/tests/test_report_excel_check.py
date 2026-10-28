"""R365. The second route of the report Excel, python/report_excel_check.py, on small workbooks written here with openpyxl in the layout the
worker writes, from a made up plan with its status, walls and missing list, override list, step checkRows and plots.csv, the same as
ReportExcelTests of C# (R10). The counts it works out are those the C# test asserts, and each planted fault is caught by its own check."""
import hashlib
import json

import openpyxl
import pytest
from openpyxl.styles import Border, Font, PatternFill, Side

import report_excel_check as check

KEY = "rules/r.json levelProfiles.parsons2024Rev6.keyRules."


def valued(value, source, status, came):
    return {"value": value, "source": source, "status": status, "statusFrom": came}


DETAILS = {
    "unit from locationPlan": {"entries": [{"status": "DOCUMENT", "document": "the location plan"}]},
    "mirrored from computed hand villa": {"entries": [{"entry": KEY + "hand villa.rule", "status": "MEASURED", "share": 0.96, "strength": "strong"}], "strength": "strong"},
    "levels.entry from estimate": {"entries": [{"entry": KEY + "entry.rule", "status": "MEASURED", "share": 0.088, "strength": "weak"}], "strength": "weak"},
    "levels.entry from override by entryM": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "section 6"}], "overrides": ["entryM"]},
    # A strong entry before the UNKNOWN one, so the basis is that of the worst entry by worstFirst and never the first entry
    "floors type from computed": {"entries": [{"entry": KEY + "generic.rule", "status": "MEASURED", "share": 0.99, "strength": "strong"},
                                              {"entry": "config/project.json floors.types", "status": "UNKNOWN", "why": "no entry"}]},
    "walls.base from keyRules": {"entries": [{"entry": KEY + "wallBase.rule", "status": "MEASURED", "share": 0.98, "strength": "strong"}], "strength": "strong"},
    "walls.top from keyRules by heightM": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "section 6"}], "overrides": ["heightM"]},
}
TYPES = {"generic": "Generic 150mm", "yard": "yard", "ramp": "Ramps 2%"}


def plot(number, unit, mirrored, moved, floors, overrides=None):
    item = {"plot": number, "unit": valued(unit, "locationPlan", "DOCUMENT", "unit from locationPlan"),
            "mirrored": valued(mirrored, "computed", "MEASURED", "mirrored from computed hand villa"),
            "levels": {"entry": valued(10.5, "override", "RECOMMENDED", "levels.entry from override by entryM") if moved
                       else valued(10.25, "estimate", "MEASURED", "levels.entry from estimate")},
            "floors": {kind: {"type": valued(TYPES[kind], "computed", "UNKNOWN", "floors type from computed")} for kind in floors}}
    if overrides is not None:
        item["overrides"] = overrides
    return item


PLAN = {"format": "site-adaptation-plan", "run": {"runId": "b7"}, "statusFrom": DETAILS,
        "summary": {"skipped": [], "rules": {"levelProfile": "parsons2024Rev6", "overrideSource": ["parsons"], "overrideModel": []}},
        "plots": [plot("101", "VD4", False, True, ["generic", "ramp"], {"applied": ["entryM", "parameter CASE"], "notApplied": {"rampFrontFallM": "no direction"}}),
                  plot("102", "DP1", True, False, ["generic", "yard"]), plot("201", "VD1", False, False, ["generic"]), plot("999", "VD1", False, False, ["generic"])],
        "walls": [{"plot": "101", "edge": 1, "plots": valued(["101", "102"], "locationPlan", "DOCUMENT", "unit from locationPlan"),
                   "base": valued(10.1, "keyRules", "MEASURED", "walls.base from keyRules"), "top": valued(10.4, "keyRules", "RECOMMENDED", "walls.top from keyRules by heightM"),
                   "basePlot": "102", "topPlot": "101"}],
        "missing": [{"plot": "102", "written": True, "missing": [{"key": "floors.ramp", "why": "no ramp type"}]},
                    {"plot": "103", "written": False, "missing": [{"key": "unit", "why": "no unit code"}],
                     "overrides": {"applied": [], "notApplied": {"mirrored": "plot not written"}}}],
        "conflicts": []}


def entry(number, field, value, reason, source="parsons"):
    return {"plot": number, "field": field, "value": value, "source": source, "model": "model_%s.rvt" % source, "reason": reason}


OVERRIDES = {"overrides": [entry("101", "entryM", {"gapToRuleM": 0.01}, "breaks roadLevel"), entry("101", "heightM", {"gapToRuleM": 0.5}, "native", "native"),
                           entry("101", "parameter CASE", "D", "breaks typologyMirrored"), entry("101", "rampFrontFallM", 0.02, "breaks roadAtEachCorner"),
                           entry("103", "mirrored", True, "breaks blockMirrorRoadSide")]}


def row(check_type, field, came, use, why=None):
    return {"check": check_type, "field": field, "from": came, "use": use, "rule": None, "toolValue": None, "past": None, "why": why}


CHECK_ROWS = {"plots": {number: {"written": {"rows": rows}} for number, rows in (
    ("101", [row("Level", "entryM", "override", "Past"), row("Level", "rampFrontFallM", "override", "Rule", "no direction"), row("Parameter", "parameter CASE", "override", "Past")]),
    ("102", [row("Floor", "floors.ramp", "missing", "Rule")]),
    ("103", [row("Hand", "mirrored", "override", "Rule", "plot not written"), row("Unit code", "unit", "missing", "Rule")]), ("201", []))}}
RULES = {"levelProfiles": {"parsons2024Rev6": {"keyRules": {"note": "made up", "hand villa": {"rule": "blockMirrorRoadSide", "share": 0.96, "strong": True},
                                                            "entry": {"rule": "roadLevel", "share": 0.088, "strong": False},
                                                            "wallBase": {"rule": "yardLevelOfTheHigherPlot", "share": 0.98, "strong": True},
                                                            "ssl": {"rule": "fflPlusGroundSsl", "notMeasured": "no key"}}}}}
CONFIG = {"profile": "rules/r.json", "majority": {"parameterLevels": ["RRE_ZZ_SP_Prototype-FFL"]},
          "valueStatus": {"assumed": ["RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"], "worstFirst": ["UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak"]},
          "checkRows": {"fields": {"entryM": {"check": "Level"}, "rampFrontFallM": {"check": "Level"}, "mirrored": {"check": "Hand"}, "rampType": {"check": "Floor"},
                                   "parameter RRE_ZZ_SP_Prototype-FFL": {"check": "Parameter"}, "parameter *": {"check": "Parameter"}}}}
PLOTS = "locationPlanNumber,typologyRev9,zone,nbhId\n101,VD4,Zone 2,012\n102,DP1,Zone 2,012\n103,,Zone 2,012\n150,,Zone 2,012\n201,VD1,Zone 3,013\n"
FILL = PatternFill(fill_type="solid", fgColor="FFDDEBF7")
THIN = Side(style="thin")


@pytest.fixture
def made(tmp_path):
    paths = {}
    for name, content in (("plan", PLAN), ("overrides", OVERRIDES), ("checkRows", CHECK_ROWS), ("rules", RULES), ("config", CONFIG)):
        paths[name] = tmp_path / (name + ".json")
        paths[name].write_text(json.dumps(content), encoding="utf-8")
    paths["plots"] = tmp_path / "plots.csv"
    paths["plots"].write_text(PLOTS, encoding="utf-8")
    mock = check.mock_labels()
    inputs = check.load_inputs(paths["plan"], paths["overrides"], paths["checkRows"], paths["plots"], paths["rules"], paths["config"])
    return paths, mock, check.expected(inputs, "parsons2024Rev6", mock)


def plain(value):
    return json.dumps(value[1], separators=(",", ":")) if isinstance(value, tuple) else value


def write_book(path, want, mock, settings):
    """A workbook in the layout of the worker, filled from want."""
    book = openpyxl.Workbook()
    summary = book.active
    summary.title = "Summary"
    summary.append([want["topLine"]])
    summary["A1"].font = Font(bold=True)
    summary.merge_cells("A1:E1")
    summary.append([])
    summary.append(mock["stepColumns"])
    for line in want["steps"]:
        summary.append(line)
    for start, name, header, lines in ((12, mock["countsBox"], mock["countColumns"], want["counts"]), (18, mock["rulesBox"], list(check.RULE_COLUMNS), want["box"])):
        summary.cell(row=start, column=1, value=name).font = Font(bold=True)
        for offset, values in enumerate([header] + lines):
            for column, value in enumerate(values, 1):
                target = summary.cell(row=start + 1 + offset, column=column, value=value)
                target.border = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
                if start == 18 and column == 3 and offset > 0 and check.is_number(value):
                    target.number_format = check.SHARE_FORMAT
    headers = {"Placed": mock["placed"], "Skipped": mock["skipped"], "Overrides": list(check.OVERRIDE_COLUMNS), "Assumed": mock["assumed"],
               "Issues": list(check.ISSUE_COLUMNS), "Rules": list(check.RULE_SHEET_COLUMNS)}
    for name in mock["sheets"][1:-1]:
        sheet = book.create_sheet(name)
        sheet.append(headers[name])
        for line in want["sheets"][name]:
            sheet.append([plain(value) for value in line])
    for column, name in enumerate(mock["placed"], 1):
        if name in mock["editable"]:
            book["Placed"].cell(row=1, column=column).fill = FILL
    sheet = book.create_sheet("Settings")
    sheet.append(["Setting", "Value"])
    for name in check.SETTINGS:
        sheet.append([name, settings.get(name, want["settings"].get(name))])
    book.save(path)


SETTINGS = {"Run id": "rx1", "Run time": "2026-10-29T08:00:00Z"}


def test_r365_the_plan_side_works_out_the_counts_the_c_sharp_test_asserts(made):
    paths, mock, want = made
    book = want["books"]["012"]
    assert sorted(want["books"]) == ["012", "013"]
    assert want["gaps"]["plots with no nbhId"] == 1
    assert book["topLine"] == "[file name].rvt, NBH 012, 3 plots, parsons2024Rev6, run time [n] min"
    assert book["steps"] == [["1 Set up site"] + ["not run yet"] * 4, ["2 Read data", 3, 2, 0, 0], ["3 Check", 3, 0, 4, 6], ["4 Place villas", 2, 1, 1, 1],
                             ["5 Floors and ramps", 4, 4, 0, 0], ["6 Levels", 2, 1, 1, 1], ["7 Walls", 1, 0, 1, 0]]
    assert book["counts"] == [["Links", "not placed yet", 2], ["Floors", "not placed yet", 4], ["Levels", "not placed yet", "no run of button 6 yet"]]
    assert book["box"] == [["hand villa", "blockMirrorRoadSide", 0.96, "strong", "2 plots"], ["entry", "roadLevel", 0.088, "weak", "1 plots"],
                           ["wallBase", "yardLevelOfTheHigherPlot", 0.98, "strong", "1 plots"], ["ssl", "fflPlusGroundSsl", "not measured", "not measured", "0 plots"]]
    assert book["sheets"]["Assumed"][1] == ["101", "floors.generic.type", "Generic 150mm", "UNKNOWN", "config/project.json floors.types"]
    assert book["sheets"]["Assumed"][3] == ["102", "levels.entry", 10.25, "MEASURED weak", KEY + "entry.rule share 0.088"]
    assert len(book["sheets"]["Assumed"]) == 6
    assert book["sheets"]["Rules"][0] == ["Q053", "RECOMMENDED", "Q053", 1, 2]
    assert book["sheets"]["Rules"][-1] == ["the location plan", "DOCUMENT", "the location plan", 2, 3]
    assert book["settings"]["Plan SHA-256"] == hashlib.sha256(paths["plan"].read_bytes()).hexdigest()
    assert book["settings"]["Editable columns"] == "Type, Mirror"


def test_r365_a_workbook_in_the_layout_of_the_worker_holds_with_no_mismatch(made, tmp_path):
    paths, mock, want = made
    folder = tmp_path / "out"
    folder.mkdir()
    for book in want["books"].values():
        write_book(folder / book["file"], book, mock, SETTINGS)
    result, bad = check.check_folder(folder, want, mock, "rx1")
    assert not bad
    assert sum(count for key, count in result.items() if key.startswith("mismatch")) == 0
    # The Mirror cell of each of the 8 Placed rows of the two workbooks, the flag of the 3 links and empty on the 5 floors
    assert result["match Placed Mirror"] == 8


@pytest.mark.parametrize("fault, caught", [
    (lambda book: book["Summary"].cell(row=6, column=5, value=5), "Summary row 6 Issues"),
    (lambda book: book["Summary"].cell(row=20, column=5, value="3 plots"), "Rules applied row 20 Plots"),
    (lambda book: setattr(book["Summary"].cell(row=14, column=1), "border", Border()), "Summary box borders"),
    (lambda book: setattr(book["Placed"].cell(row=1, column=5), "fill", FILL), "fills on"),
    (lambda book: book["Placed"].cell(row=3, column=4, value=False), "Placed row 3 Mirror"),
    (lambda book: book["Placed"].insert_rows(3), "Placed max_row"),
    (lambda book: book["Assumed"].cell(row=2, column=5, value="Q053 share 1"), "Assumed row 2 Basis"),
    (lambda book: book["Settings"].cell(row=9, column=2, value="0" * 64), "Settings Plan SHA-256"),
    (lambda book: book.move_sheet("Assumed", offset=-1), "sheets"),
])
def test_r365_each_planted_fault_is_caught_by_its_check(made, tmp_path, fault, caught):
    paths, mock, want = made
    book = want["books"]["012"]
    path = tmp_path / book["file"]
    write_book(path, book, mock, SETTINGS)
    opened = openpyxl.load_workbook(path)
    fault(opened)
    opened.save(path)
    result, bad = check.check_xlsx(path, book, mock, "rx1")
    assert any(caught in item for item in bad), bad


def test_r365_the_labels_come_from_the_mock_files_and_their_additions_of_7_7(made):
    paths, mock, want = made
    assert mock["sheets"] == ["Summary", "Placed", "Skipped", "Overrides", "Assumed", "Issues", "Rules", "Settings"]
    assert mock["placed"] == ["Plot", "Kind", "Type", "Mirror", "Element id"]
    assert mock["editable"] == ["Type", "Mirror"]
    assert mock["steps"][-1] == "7 Walls" and len(mock["steps"]) == 7
