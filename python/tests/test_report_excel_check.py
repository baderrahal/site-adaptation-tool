"""R365. The second route of the report Excel, python/report_excel_check.py, on small workbooks written here with openpyxl in the layout the
worker writes, from a made up plan with its status, walls and missing list, override list, step checkRows and plots.csv, the same as
ReportExcelTests of C# (R10). The counts it works out are those the C# test asserts, and each planted fault is caught by its own check."""
import calendar
import hashlib
import json
import time
from datetime import timedelta, timezone

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
    # Review 1 of PR 96: a height the heightM override touched still reads keyRules.entry, and counts under no keyRules entry
    "levels.height from estimate by heightM": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "section 6"},
                                                           {"entry": KEY + "entry.rule", "status": "MEASURED", "share": 0.088, "strength": "weak"}], "overrides": ["heightM"]},
    "walls.base from keyRules": {"entries": [{"entry": KEY + "wallBase.rule", "status": "MEASURED", "share": 0.98, "strength": "strong"}], "strength": "strong"},
    "walls.top from keyRules by heightM": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "section 6"}], "overrides": ["heightM"]},
    # R552: a value the new sheets read and no rule counts, so the rows of Assumed and Rules used stay those of the C# test
    "schedule": {"entries": []},
}
TYPES = {"generic": "Generic 150mm", "yard": "yard", "ramp": "Ramps 2%"}


def read(value):
    return valued(value, "plan", "DOCUMENT", "schedule")


def mm(value):
    """A level as a plan writes it, to the mm, so openpyxl writes the test book back with the same double."""
    return round(value, 3)


def floor(kind, base):
    item = {"type": valued(TYPES[kind], "computed", "UNKNOWN", "floors type from computed"), "level": read("Level " + kind), "levelElevation": read(base), "z": read(mm(base + 0.25)),
            "heightOffset": read(0.25)}
    if kind == "ramp":
        item["slopePercent"] = read(2.0)
        item["corners"] = read([[1.0, 2.0, mm(base + 0.1)], [3.0, 2.0, mm(base + 0.2)], [3.0, 4.0, mm(base + 0.3)], [1.0, 4.0, mm(base + 0.4)]])
    return item


def plot(number, unit, mirrored, moved, floors, overrides=None):
    base = float(number) / 10.0
    item = {"plot": number, "unit": valued(unit, "locationPlan", "DOCUMENT", "unit from locationPlan"),
            "mirrored": valued(mirrored, "computed", "MEASURED", "mirrored from computed hand villa"),
            "levels": {"entry": valued(10.5, "override", "RECOMMENDED", "levels.entry from override by entryM") if moved
                       else valued(10.25, "estimate", "MEASURED", "levels.entry from estimate")},
            "floors": {kind: floor(kind, base) for kind in floors},
            "levelRule": {"steps": 3}, "prototypeFile": read("C:\\protos\\%s_v2.1.rvt" % unit), "rotationDeg": read(90.5)}
    item["levels"].update(ffl=read(mm(base + 1.5)), ssl=read(mm(base + 1.45)), steps=read(2))
    if overrides is not None:
        item["overrides"] = overrides
        item["levels"]["height"] = valued(0.2, "estimate", "RECOMMENDED", "levels.height from estimate by heightM")
    return item


PLAN = {"format": "site-adaptation-plan", "run": {"runId": "b7"}, "statusFrom": DETAILS,
        "summary": {"skipped": [], "rules": {"levelProfile": "parsons2024Rev6", "overrideSource": ["parsons"], "overrideModel": []}},
        "plots": [plot("101", "VD4", False, True, ["generic", "ramp"], {"applied": ["entryM", "parameter CASE", "rampFrontM", "parameter RRE_ZZ_SP_RVT-Name"], "notApplied": {"rampFrontFallM": "no direction"}}),
                  plot("102", "DP1", True, False, ["generic", "yard"]), plot("201", "VD1", False, False, ["generic"]), plot("999", "VD1", False, False, ["generic"])],
        "walls": [{"plot": "101", "edge": 1, "plots": valued(["101", "102"], "locationPlan", "DOCUMENT", "unit from locationPlan"),
                   "base": valued(10.1, "keyRules", "MEASURED", "walls.base from keyRules"), "top": valued(10.4, "keyRules", "RECOMMENDED", "walls.top from keyRules by heightM"),
                   "ends": read([[0.0, 0.0], [3.0, 4.0]]), "basePlot": "102", "topPlot": "101"}],
        "missing": [{"plot": "102", "written": True, "missing": [{"key": "floors.ramp", "why": "no ramp type"}]},
                    {"plot": "103", "written": False, "missing": [{"key": "unit", "why": "no unit code"}],
                     "overrides": {"applied": [], "notApplied": {"mirrored": "plot not written"}}}],
        "conflicts": []}


def entry(number, field, value, reason, source="parsons"):
    return {"plot": number, "field": field, "value": value, "source": source, "model": "model_%s.rvt" % source, "reason": reason}


OVERRIDES = {"overrides": [entry("101", "entryM", {"gapToRuleM": 0.01}, "breaks roadLevel"), entry("101", "heightM", {"gapToRuleM": 0.5}, "native", "native"),
                           entry("101", "parameter CASE", "D", "breaks typologyMirrored"), entry("101", "rampFrontFallM", 0.02, "breaks roadAtEachCorner"),
                           entry("101", "rampFrontM", {"gapToRuleM": 0.003}, "breaks roadLevel"),
                           entry("101", "parameter RRE_ZZ_SP_RVT-Name", "101 | VD4 | 1", "breaks plotCodeStepsAndFacade"),
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
                                                            "wallTop": {"rule": "higherFflPlusRev6", "share": 0.35, "strong": False},
                                                            "ssl": {"rule": "fflPlusGroundSsl", "notMeasured": "no key"}}}}}
# The step summary of the build, 103 with no unit, 150 of plots.csv in 012 outside the scope as a key cover holds part of an NBH
SUMMARY = {"plots": {number: {"written": {"unit": unit, "written": unit is not None}} for number, unit in
                     (("101", "VD4"), ("102", "DP1"), ("103", None), ("201", "VD1"), ("999", "VD1"))}}
# R381: the steps of the order of Bader's changes of 28 Oct 2026, the link parameters under 7 Place villas, as config/project.json holds them
STEP_OF_FIELD = {"rampType": 5, "rampFrontM": 5, "genericFloor": 5, "rampOutline": 5, "yardOutline": 5, "rampFrontFallM": 5, "entryM": 6, "heightM": 6, "steps": 6,
                 "parameter RRE_ZZ_SP_Prototype-FFL": 7, "parameter RRE_ZZ_SP_RVT-Name": 7}
CONFIG = {"profile": "rules/r.json", "addin": {"report": {"stepOfField": STEP_OF_FIELD, "defaultStep": 7, "wallDecisions": ["wallBase", "wallTop", "wallLine", "frontOpen"],
                                                          "oldPlotColumn": "baseplanLabel"}},
          "valueStatus": {"assumed": ["RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"], "worstFirst": ["UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak"]},
          }
# R553: an old number in digits a number, 102 with a leading zero stays text, 201 with none is empty
PLOTS = "locationPlanNumber,typologyRev9,zone,nbhId,baseplanLabel\n101,VD4,Zone 2,012,5101\n102,DP1,Zone 2,012,0102\n103,,Zone 2,012,5103\n150,,Zone 2,012,5150\n201,VD1,Zone 3,013,\n"
FILL = PatternFill(fill_type="solid", fgColor="FFDDEBF7")
THIN = Side(style="thin")


ACCEPTED_ROW = {"text": "Highlighted floors overlap.", "plot": "VD4-101", "firstType": "Generic 150mm", "secondType": "yard", "firstId": "a", "secondId": "b"}


def accepted_file(nbh="012"):
    return {"format": "site-adaptation-accepted-warnings", "runId": "5_1", "neighbourhood": nbh, "count": 1, "accepted": [ACCEPTED_ROW]}


def build(tmp_path, rules=RULES, summary=SUMMARY, revit=None, accepted=None, facts=None, plan=PLAN):
    paths = {}
    for name, content in (("plan", plan), ("overrides", OVERRIDES), ("checkRows", CHECK_ROWS), ("summary", summary), ("rules", rules), ("config", CONFIG)):
        paths[name] = tmp_path / (name + ".json")
        paths[name].write_text(json.dumps(content), encoding="utf-8")
    paths["plots"] = tmp_path / "plots.csv"
    paths["plots"].write_text(PLOTS, encoding="utf-8")
    if revit is not None:
        paths["revit"] = tmp_path / "revit_counts.json"
        paths["revit"].write_text(json.dumps(revit), encoding="utf-8")
    if accepted is not None:
        paths["accepted"] = tmp_path / "floors_012_accepted.json"
        paths["accepted"].write_text(json.dumps(accepted), encoding="utf-8")
    if facts is not None:
        paths["facts"] = tmp_path / "report_facts_r1_012.json"
        paths["facts"].write_text(json.dumps(facts), encoding="utf-8")
    mock = check.mock_labels()
    inputs = check.load_inputs(paths["plan"], paths["overrides"], paths["checkRows"], paths["summary"], paths["plots"], paths["rules"], paths["config"], paths.get("revit"), paths.get("accepted"), paths.get("facts"))
    return paths, mock, check.expected(inputs, "parsons2024Rev6", mock)


@pytest.fixture
def made(tmp_path):
    return build(tmp_path)


def plain(value):
    if not isinstance(value, tuple):
        return value
    return {"json": lambda: json.dumps(value[1], separators=(",", ":")), "near": lambda: value[1], "blank": lambda: None}[value[0]]()


def write_list(sheet, header, lines):
    sheet.append(header)
    for line in lines:
        sheet.append([plain(value) for value in line])
    sheet.freeze_panes = "A2"
    sheet.auto_filter.ref = "A1:%s%d" % (openpyxl.utils.get_column_letter(len(header)), len(lines) + 1)


def write_summary(summary, want, mock):
    summary.append([want["topLine"]])
    summary["A1"].font = Font(bold=True)
    summary.merge_cells("A1:F1")
    summary.append([])
    summary.append(mock["stepColumns"])
    for line in want["steps"]:
        summary.append(line)
    counts_at = 5 + len(want["steps"])
    rules_at = counts_at + 3 + len(want["counts"])
    for start, name, header, lines in ((counts_at, mock["countsBox"], mock["countColumns"], want["counts"]), (rules_at, mock["rulesBox"], list(check.RULE_COLUMNS), want["box"])):
        summary.cell(row=start, column=1, value=name).font = Font(bold=True)
        for offset, values in enumerate([header] + lines):
            for column, value in enumerate(values, 1):
                target = summary.cell(row=start + 1 + offset, column=column, value=value)
                target.border = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
                if start == rules_at and column == 3 and offset > 0 and check.is_number(value):
                    target.number_format = check.SHARE_FORMAT


def local_date(stamp):
    """The local day of a UTC stamp by the time module, another route than the datetime of the check."""
    return time.strftime("%Y-%m-%d", time.localtime(calendar.timegm(time.strptime(stamp, "%Y-%m-%dT%H:%M:%SZ"))))


def write_book(path, want, mock, settings):
    """A workbook in the layout of the worker, filled from want."""
    book = openpyxl.Workbook()
    book.remove(book.active)
    headers = check.sheet_headers(mock, want)
    for name in mock["sheets"]:
        sheet = book.create_sheet(name)
        if name == "Summary":
            write_summary(sheet, want, mock)
        elif name == "Cover":
            sheet.append(list(check.COVER_COLUMNS))
            for item, value in want["cover"]:
                sheet.append([item, local_date(settings["Run time"]) if item == check.COVER_DATE else value])
        elif name == "Settings":
            write_list(sheet, ["Setting", "Value"], [[item, settings.get(item, want["settings"].get(item))] for item in check.SETTINGS])
        else:
            write_list(sheet, headers[name], want["sheets"][name])
    for column, name in enumerate(mock["placed"], 1):
        if name in mock["editable"]:
            book["Placed"].cell(row=1, column=column).fill = FILL
    book.save(path)


SETTINGS = {"Run id": "rx1", "Run time": "2026-10-29T08:00:00Z"}


def test_r365_the_plan_side_works_out_the_counts_the_c_sharp_test_asserts(made):
    paths, mock, want = made
    book = want["books"]["012"]
    assert sorted(want["books"]) == ["012", "013"]
    assert want["gaps"]["plots with no nbhId"] == 1
    assert book["file"] == "012_Report.xlsx"
    assert book["topLine"] == "not set, NBH 012, 3 plots, parsons2024Rev6, run time not set"
    # R381 moved this from the buttons 1 to 7 of the mock of 25 Oct to the steps 1 to 8 of the new order: rampFrontM and rampFrontFallM under 5,
    # entryM under 6, CASE and RVT-Name under 7 Place villas, 2 Read data over the faces of the scope, 150 left out as the scope is no whole
    # project, and the weak wallTop the Issues of 8 Walls, each line with the words of no manual rule block
    none = "no manual rule block"
    assert book["steps"] == [["1 Set up site"] + ["not run yet"] * 4 + [none], ["2 Read data", 2, 1, 0, 0, none], ["3 Review data"] + ["not run yet"] * 4 + [none],
                             ["4 Check", 3, 0, 6, 6, none], ["5 Floors and ramps", 4, 4, 1, 1, none], ["6 Levels", 2, 1, 1, 0, none],
                             ["7 Place villas", 2, 1, 2, 1, none], ["8 Walls", 1, 0, 1, 1, none]]
    assert book["sheets"]["Issues"][0] == ["4 Check", 101, "Level", "entryM", "override", "Past", None, none]
    assert book["sheets"]["Issues"][-1] == ["8 Walls", None, "Weak rule", "wallTop", KEY + "wallTop.rule", None, "higherFflPlusRev6 share 0.35, weak", none]
    assert len(book["sheets"]["Issues"]) == 7
    assert book["counts"] == [["Links", "not placed yet", 2], ["Floors", "not placed yet", 4], ["Levels", "not placed yet", "no run of button 6 yet"]]
    assert book["box"] == [["hand villa", "blockMirrorRoadSide", 0.96, "strong", "2 plots"], ["entry", "roadLevel", 0.088, "weak", "1 plots"],
                           ["wallBase", "yardLevelOfTheHigherPlot", 0.98, "strong", "1 plots"], ["wallTop", "higherFflPlusRev6", 0.35, "weak", "0 plots"], ["ssl", "fflPlusGroundSsl", "not measured", "not measured", "0 plots"]]
    assert book["sheets"]["Assumed"][2] == ["101", "floors.generic.type", "Generic 150mm", "UNKNOWN", "config/project.json floors.types"]
    assert book["sheets"]["Assumed"][1] == ["101", "levels.height", 0.2, "RECOMMENDED", "Q053"]
    assert book["sheets"]["Assumed"][4] == ["102", "levels.entry", 10.25, "MEASURED weak", KEY + "entry.rule share 0.088"]
    assert len(book["sheets"]["Assumed"]) == 7
    assert book["sheets"]["Rules used"][0] == ["Q053", "RECOMMENDED", "Q053", 1, 3]
    assert book["sheets"]["Rules used"][2] == [KEY + "entry.rule", "MEASURED weak", KEY + "entry.rule share 0.088", 2, 2]
    assert book["sheets"]["Rules used"][-1] == ["the location plan", "DOCUMENT", "the location plan", 2, 3]
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
    # The Mirror cell of each of the 9 Placed rows of the two workbooks, the flag of the 3 links and empty on the 5 floors and the wall
    assert result["match Placed Mirror"] == 9


@pytest.mark.parametrize("fault, caught", [
    (lambda book: book["Summary"].cell(row=6, column=5, value=5), "Summary row 6 Issues"),
    (lambda book: book["Summary"].cell(row=21, column=5, value="3 plots"), "Rules applied row 21 Plots"),
    (lambda book: book["Summary"].cell(row=7, column=6, value="M01"), "Summary row 7 Manual rule"),
    (lambda book: book["Issues"].cell(row=8, column=8, value="M11"), "Issues row 8 Manual rule"),
    (lambda book: setattr(book["Summary"].cell(row=14, column=1), "border", Border()), "Summary box borders"),
    (lambda book: setattr(book["Placed"].cell(row=1, column=5), "fill", FILL), "fills on"),
    (lambda book: book["Placed"].cell(row=3, column=4, value=False), "Placed row 3 Mirror"),
    (lambda book: book["Placed"].insert_rows(3), "Placed max_row"),
    (lambda book: book["Assumed"].cell(row=2, column=5, value="Q053 share 1"), "Assumed row 2 Basis"),
    (lambda book: book["Settings"].cell(row=9, column=2, value="0" * 64), "Settings Plan SHA-256"),
    (lambda book: book.move_sheet("Assumed", offset=-1), "sheets"),
    # R552
    (lambda book: book["Plot schedule"].cell(row=2, column=4, value=99.0), "Plot schedule row 2 FFL (m)"),
    (lambda book: book["Plot schedule"].cell(row=2, column=14, value=99.0), "Plot schedule row 2 Ramp corner 1 (m)"),
    (lambda book: book["Plot schedule"].cell(row=2, column=11, value="VD4_v2.1.rvt"), "Plot schedule row 2 Prototype"),
    (lambda book: book["Plot schedule"].cell(row=2, column=4, value="11.6"), "Plot schedule row 2 FFL (m)"),
    (lambda book: book["Floors"].cell(row=2, column=6, value=99.0), "Floors row 2 Z (m)"),
    (lambda book: book["Walls"].cell(row=2, column=6, value=6.0), "Walls row 2 Length (m)"),
    (lambda book: book["Cover"].cell(row=6, column=2, value="2020-01-01"), "Cover row 6 Value"),
    (lambda book: book["Cover"].cell(row=9, column=2, value="other.xlsx"), "Cover row 9 Value"),
    # Q307
    (lambda book: book["Placed"].delete_rows(7), "Placed max_row"),
    (lambda book: book["Placed"].cell(row=7, column=2, value="wall"), "Placed row 7 Kind"),
    # R553
    (lambda book: book["Issues"].cell(row=2, column=2, value="101"), "Issues row 2 Plot"),
    (lambda book: book["Overrides"].cell(row=2, column=1, value="101"), "Overrides row 2 Plot"),
    (lambda book: book["Plot schedule"].cell(row=2, column=2, value="101"), "Plot schedule row 2 Plot No."),
    (lambda book: book["Plot schedule"].cell(row=2, column=10, value="5101"), "Plot schedule row 2 Old Plot No."),
    (lambda book: book["Placed"].cell(row=2, column=1, value=101), "Placed row 2 Plot"),
    (lambda book: setattr(book["Plot schedule"], "freeze_panes", None), "Plot schedule freeze"),
    (lambda book: setattr(book["Settings"], "freeze_panes", None), "Settings freeze"),
    (lambda book: setattr(book["Walls"].auto_filter, "ref", None), "Walls filter"),
    (lambda book: setattr(book["Floors"].auto_filter, "ref", "A1:B2"), "Floors filter"),
    (lambda book: setattr(book["Cover"], "freeze_panes", "A2"), "Cover frozen"),
    # R555: no changed rule, so no sixth column
    (lambda book: book["Rules used"].cell(row=1, column=6, value="Changed"), "Rules used max_column"),
    (lambda book: setattr(book["Rules used"], "title", "Rules"), "sheets"),
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
    # R551: Bader's change of 3 Nov 2026 over the order of the two mock files
    assert mock["sheets"] == ["Cover", "Plot schedule", "Floors", "Walls", "Issues", "Overrides", "Rules used", "Summary", "Placed", "Skipped", "Assumed", "Accepted warnings", "Settings"]
    assert mock["accepted"] == ["Plot", "First floor type", "Second floor type", "Warning"]
    assert mock["placed"] == ["Plot", "Kind", "Type", "Mirror", "Element id"]
    assert mock["editable"] == ["Type", "Mirror"]
    # R381: the steps 1 to 8 of the order of docs/design/13-workflow-changes.md, 9 Report the step of the report
    assert mock["steps"] == ["1 Set up site", "2 Read data", "3 Review data", "4 Check", "5 Floors and ramps", "6 Levels", "7 Place villas", "8 Walls"]
    assert mock["reportStep"] == "9 Report"
    assert mock["stepColumns"][-1] == "Manual rule"


def test_r365_a_placed_sheet_short_of_one_floor_row_gives_the_mismatch_as_an_issue_row(made):
    paths, mock, want = made
    written = [plot for plot in PLAN["plots"] if plot["plot"] in ("101", "102")]
    placed = [row for row in want["books"]["012"]["sheets"]["Placed"] if row[1] != "boundaryWall"][:-1]
    rows, issues = check.counts_box(placed, written, mock, None, "M15")
    assert rows[1] == ["Floors", "not placed yet", 3]
    assert issues == [["9 Report", None, "Counts", "Floors", None, None, "the plan holds 4, Revit not placed yet, Excel 3 (R365)", "M15"]]


def test_r381_read_data_counts_the_faces_of_plots_csv_in_the_scope_with_a_unit_as_done(tmp_path):
    paths, mock, want = build(tmp_path, summary=dict(SUMMARY, scope="project"))
    assert want["books"]["012"]["steps"][1] == ["2 Read data", 2, 2, 0, 0, "no manual rule block"]


MANUAL = {"manual": {"name": "made up"},
          "rules": [{"id": "M01", "text": "made up", "group": "Plots and clusters", "manualRef": "2.1", "checkedIn": ["4 Check", "7 Place villas"]},
                    {"id": "M04", "text": "made up", "group": "Levels and steps", "manualRef": "2.2.A", "checkedIn": ["6 Levels", "8 Walls"]},
                    {"id": "M02", "text": "made up", "group": "Plots and clusters", "manualRef": "2.1.E", "checkedIn": ["4 Check"]},
                    {"id": "M15", "text": "made up", "group": "Structure", "manualRef": "3.2.A", "checkedIn": ["9 Report"]}]}


def test_r381_every_summary_and_issues_line_names_the_manual_rule_ids_whose_steps_take_its_step(tmp_path):
    paths, mock, want = build(tmp_path, rules=dict(RULES, manualRules=MANUAL))
    book = want["books"]["012"]
    assert [line[-1] for line in book["steps"]] == ["no manual rule", "no manual rule", "no manual rule", "M01, M02", "no manual rule", "M04", "M01", "M04"]
    assert book["sheets"]["Issues"][0][-1] == "M01, M02" and book["sheets"]["Issues"][-1][-1] == "M04"
    with pytest.raises(ValueError, match="no list rules each with a text id"):
        build(tmp_path, rules=dict(RULES, manualRules={"rules": [{"id": "M01", "text": "made up"}]}))


def test_r381_the_revit_counts_fill_the_revit_column_and_a_mismatch_of_any_two_is_an_issue(tmp_path):
    floors = lambda generic, yard, ramp: {"generic": generic, "yard": yard, "ramp": ramp}
    revit = {"format": "siteAdaptationRevitCounts", "formatVersion": 1, "levelRun": "r6",
             "plots": {"101": {"links": 1, "floors": floors(1, 0, 1), "level": True}, "102": {"links": 1, "floors": floors(1, 0, 0), "level": False},
                       "201": {"links": 1, "floors": floors(1, 0, 0), "level": True}}}
    paths, mock, want = build(tmp_path, revit=revit)
    book = want["books"]["012"]
    assert book["counts"] == [["Links", 2, 2], ["Floors", 3, 4], ["Levels", 1, "no run of button 6 yet"]]
    assert book["sheets"]["Issues"][-2:] == [["9 Report", None, "Counts", "Floors", None, None, "the plan holds 4, Revit 3, Excel 4 (R381)", "no manual rule block"],
                                             ["9 Report", None, "Counts", "Levels", None, None, "the plan holds 2, Revit 1, Excel no run of button 6 yet (R381)", "no manual rule block"]]
    assert want["books"]["013"]["counts"] == [["Links", 1, 1], ["Floors", 1, 1], ["Levels", 1, "no run of button 6 yet"]]
    with pytest.raises(ValueError, match="no file of the counts"):
        build(tmp_path, revit={"format": "another"})


def test_r483_the_placed_sheet_is_held_against_the_ids_of_the_counts_file(tmp_path):
    floors = {"generic": 1, "yard": 0, "ramp": 2}
    revit = {"format": "siteAdaptationRevitCounts", "formatVersion": 1, "levelRun": None,
             "plots": {"101": {"links": 1, "floors": floors, "level": False, "ids": {"link": [5001], "generic": [5002], "ramp": [5004, 5003]}}}}
    paths, mock, want = build(tmp_path, revit=revit)
    placed = {(row[0], row[1]): row[4] for row in want["books"]["012"]["sheets"]["Placed"]}
    assert placed[("101", "link")] == 5001
    assert placed[("101", "generic")] == 5002
    assert placed[("101", "ramp")] == "5003, 5004"
    assert placed[("102", "link")] is None
    assert check.element_id(None, "101", "link") is None


def test_r459_the_sheet_holds_the_rows_of_the_file_of_its_nbh_and_a_workbook_that_differs_is_caught(tmp_path):
    paths, mock, want = build(tmp_path, accepted=accepted_file())
    assert want["books"]["012"]["sheets"][check.ACCEPTED_SHEET] == [["VD4-101", "Generic 150mm", "yard", "Highlighted floors overlap."]]
    assert want["books"]["013"]["sheets"][check.ACCEPTED_SHEET] == []
    path = tmp_path / "012_Report.xlsx"
    write_book(path, want["books"]["012"], mock, SETTINGS)
    result, bad = check.check_xlsx(path, want["books"]["012"], mock, "rx1")
    assert not bad, bad
    assert result["match Accepted warnings rows"] == 1

    # A sheet that lost its row, as a report that took no file would
    book = openpyxl.load_workbook(path)
    book[check.ACCEPTED_SHEET].delete_rows(2)
    book.save(path)
    result, bad = check.check_xlsx(path, want["books"]["012"], mock, "rx1")
    assert any("Accepted warnings" in item for item in bad), bad


def test_r459_no_file_expects_an_empty_sheet(made):
    paths, mock, want = made
    assert all(book["sheets"][check.ACCEPTED_SHEET] == [] for book in want["books"].values())


def test_r459_a_file_with_a_wrong_count_is_refused(tmp_path):
    with pytest.raises(ValueError, match="R459"):
        build(tmp_path, accepted=dict(accepted_file(), count=2))


def test_r365_settings_with_no_block_addin_report_are_refused(made, tmp_path):
    paths, mock, want = made
    bare = dict(CONFIG)
    bare.pop("addin")
    paths["config"].write_text(json.dumps(bare), encoding="utf-8")
    inputs = check.load_inputs(paths["plan"], paths["overrides"], paths["checkRows"], paths["summary"], paths["plots"], paths["rules"], paths["config"])
    with pytest.raises(ValueError, match="no block addin.report"):
        check.expected(inputs, "parsons2024Rev6", mock)


FIELDS = {"project": "00101", "originator": "PSN", "volume": "<nbh>", "level": "XXX", "type": "SCH", "role": "TU", "number": "090000", "revision": "P01", "status": "S0", "projectName": "Made up"}
FACTS = {"model": "site_012.rvt", "toolVersion": "1.0, build abc", "runSeconds": 75.0, "fields": FIELDS,
         "steps": {"1": {"time": "2026-11-03T08:00:00Z", "issues": 4}, "3": {"time": "2026-11-03T08:05:00Z", "issues": 0}, "5": {"time": "2026-11-03T08:10:00Z", "issues": 1407}}}


def level_excel(tmp_path, numbers=(101, "102", 103, "other", None, "201")):
    """A level Excel in the layout of the worker: a top line, then the header and a key in column A. The row after the empty cell and the Plot No of the
    other sheet are bait for a read that goes too far."""
    path = tmp_path / "012_Levels_parsons2024Rev6.xlsx"
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "Levels"
    sheet.append(["NBH 012, made up"])
    sheet.append(["Plot No", "Unit"])
    for number in numbers:
        sheet.append([number, "VD4"])
    book.create_sheet("Overrides").append(["Plot No", "Field"])
    book["Overrides"].append(["201", "entryM"])
    book.save(path)
    return str(path)


def round_trip(tmp_path, want, mock, nbh="012"):
    path = tmp_path / want["books"][nbh]["file"]
    write_book(path, want["books"][nbh], mock, SETTINGS)
    return path


def test_r554_the_facts_fill_the_top_line_the_steps_of_a_record_and_levels_of_the_box(tmp_path):
    paths, mock, want = build(tmp_path, facts=dict(FACTS, levelExcel=level_excel(tmp_path)))
    book = want["books"]["012"]
    none = "no manual rule block"
    # 75 s is 1.25 min, half away from zero as the format of the worker, which %.1f would write 1.2
    assert book["topLine"] == "site_012.rvt, NBH 012, 3 plots, parsons2024Rev6, run time 1.3 min"
    assert book["steps"][0] == ["1 Set up site", "2026-11-03T08:00:00Z", None, None, 4, none]
    assert book["steps"][2] == ["3 Review data", "2026-11-03T08:05:00Z", None, None, 0, none]
    assert book["steps"][4] == ["5 Floors and ramps", 4, 4, 1, 1407, none]
    assert book["steps"][3][4] == 6 and book["steps"][6][4] == 1
    assert book["counts"][2] == ["Levels", "not placed yet", 2]
    result, bad = check.check_xlsx(round_trip(tmp_path, want, mock), book, mock, "rx1")
    assert not bad, bad


def test_r554_facts_with_no_model_no_time_and_no_record_keep_the_words_of_not_set(tmp_path):
    paths, mock, want = build(tmp_path, facts={"model": None, "toolVersion": "1.0", "runSeconds": None, "steps": {}})
    book = want["books"]["012"]
    assert book["topLine"] == "not set, NBH 012, 3 plots, parsons2024Rev6, run time not set"
    assert book["steps"][0][1:5] == ["not run yet"] * 4
    assert book["counts"][2][2] == "no run of button 6 yet"
    assert book["file"] == "012_Report.xlsx"


@pytest.mark.parametrize("seconds, minutes", [(0.0, "0.0 min"), (75.0, "1.3 min"), (9.0, "0.2 min"), (600.0, "10.0 min"), (3599.9, "60.0 min")])
def test_r554_the_run_time_rounds_as_the_format_of_the_worker(seconds, minutes):
    assert check.run_time(seconds) == minutes


@pytest.mark.parametrize("fault, caught", [
    (lambda book: book["Summary"].cell(row=1, column=1, value="not set, NBH 012, 3 plots, parsons2024Rev6, run time not set"), "Summary row 1"),
    (lambda book: book["Summary"].cell(row=4, column=2, value="not run yet"), "Summary row 4 Done"),
    (lambda book: book["Summary"].cell(row=8, column=5, value=112), "Summary row 8 Issues"),
    (lambda book: book["Summary"].cell(row=17, column=3, value="no run of button 6 yet"), "Counts row 17 Excel"),
    (lambda book: book["Cover"].cell(row=8, column=2, value="not set"), "Cover row 8 Value"),
])
def test_r554_a_fault_against_the_facts_is_caught(tmp_path, fault, caught):
    paths, mock, want = build(tmp_path, facts=dict(FACTS, levelExcel=level_excel(tmp_path)))
    path = round_trip(tmp_path, want, mock)
    opened = openpyxl.load_workbook(path)
    fault(opened)
    opened.save(path)
    result, bad = check.check_xlsx(path, want["books"]["012"], mock, "rx1")
    assert any(caught in item for item in bad), bad


def test_r550_the_file_name_comes_from_the_fields_and_each_nbh_takes_its_own(tmp_path):
    paths, mock, want = build(tmp_path, facts=FACTS)
    assert want["books"]["012"]["file"] == "00101-PSN-012-XXX-SCH-TU-090000.xlsx"
    assert want["books"]["013"]["file"] == "00101-PSN-013-XXX-SCH-TU-090000.xlsx"
    folder = tmp_path / "out"
    folder.mkdir()
    write_book(folder / want["books"]["012"]["file"], want["books"]["012"], mock, SETTINGS)
    write_book(folder / "012_Report.xlsx", want["books"]["013"], mock, SETTINGS)
    result, bad = check.check_folder(folder, want, mock, "rx1")
    assert any(item.startswith("workbooks") for item in bad), bad
    with pytest.raises(ValueError, match="R550"):
        build(tmp_path, facts=dict(FACTS, fields={"project": "00101"}))


def test_r554_levels_counts_the_written_plots_of_the_book_in_the_sheet_levels(tmp_path):
    path = level_excel(tmp_path)
    assert check.level_plots(path) == ["101", "102", "103", "other"]
    books = build(tmp_path, facts=dict(FACTS, levelExcel=path))[2]["books"]
    # 103 is in the book but not written and 201 lies past the empty cell, so 101 and 102 of 012 and none of 013
    assert books["012"]["counts"][2] == ["Levels", "not placed yet", 2]
    assert books["013"]["counts"][2] == ["Levels", "not placed yet", 0]


def test_r554_with_no_level_excel_the_levels_cell_says_no_run_and_old_facts_are_not_read(tmp_path):
    assert build(tmp_path, facts=FACTS)[2]["books"]["012"]["counts"][2] == ["Levels", "not placed yet", "no run of button 6 yet"]
    old = dict(FACTS, levelPlots=["101", "102"])
    assert build(tmp_path, facts=old)[2]["books"]["012"]["counts"][2] == ["Levels", "not placed yet", "no run of button 6 yet"]


def test_r554_a_level_excel_with_no_sheet_levels_or_no_header_is_refused_by_name(tmp_path):
    path = tmp_path / "bad.xlsx"
    book = openpyxl.Workbook()
    book.active.title = "Other"
    book.save(path)
    with pytest.raises(ValueError, match="bad.xlsx.*Levels.*R554"):
        check.level_plots(path)
    book.active.title = "Levels"
    book.active.append(["Plot", "Unit"])
    book.save(path)
    with pytest.raises(ValueError, match="bad.xlsx.*Plot No.*R554"):
        check.level_plots(path)


def test_r551_the_cover_date_is_the_local_day_of_the_utc_run_time(tmp_path, monkeypatch):
    stamp = "2026-11-03T22:30:00Z"
    east, west = timezone(timedelta(hours=3)), timezone(timedelta(hours=-5))
    assert check.local_day(stamp, east) == "2026-11-04"
    assert check.local_day("2026-11-03T01:00:00Z", west) == "2026-11-02"
    assert check.local_day(stamp) == local_date(stamp)
    assert check.local_day("no time") == ""
    # The zone is fixed here so a machine on UTC still sees the two days apart
    real = check.local_day
    monkeypatch.setattr(check, "local_day", lambda text: real(text, east))
    paths, mock, want = build(tmp_path)
    book = want["books"]["012"]
    path = tmp_path / book["file"]
    for day, holds in (("2026-11-04", True), ("2026-11-03", False)):
        write_book(path, book, mock, dict(SETTINGS, **{"Run time": stamp}))
        opened = openpyxl.load_workbook(path)
        opened["Cover"]["B6"] = day
        opened.save(path)
        result, bad = check.check_xlsx(path, book, mock, "rx1")
        assert (not any("Cover row 6 Value" in item for item in bad)) is holds, bad


def test_r551_cover_holds_the_fields_and_the_day_of_the_run(tmp_path):
    paths, mock, want = build(tmp_path, facts=FACTS)
    book = want["books"]["012"]
    assert book["cover"] == [["Project", "Made up"], ["NBH", "012"], ["Revision", "P01"], ["Status code", "S0"], ["Date", None], ["Tool version", "1.0, build abc"],
                             ["Model name", "site_012.rvt"], ["File name", "00101-PSN-012-XXX-SCH-TU-090000.xlsx"]]
    bare = build(tmp_path)[2]["books"]["012"]
    assert [row[1] for row in bare["cover"]] == ["not set", "012", "not set", "not set", None, "not set", "not set", "012_Report.xlsx"]
    opened = openpyxl.load_workbook(round_trip(tmp_path, want, mock))
    assert opened["Cover"]["B6"].value == local_date(SETTINGS["Run time"])


def test_r552_plot_schedule_has_every_column_from_the_plan(made):
    paths, mock, want = made
    lines = want["books"]["012"]["sheets"]["Plot schedule"]
    assert [line[1] for line in lines] == [101, 102, 103]
    assert lines[0] == ["101-VD4", 101, "VD4", 11.6, 11.55, 10.5, 0.2, 3, 2, 5101, "VD4_v2.1", False, 90.5, 10.2, 10.3, 10.4, 10.5, None, None, None, None, None]
    # The leading zero of 0102 stays text, 102 has no ramp and no height
    assert lines[1][:2] == ["102-DP1", 102] and lines[1][6] is None and lines[1][9] == "0102" and lines[1][13:17] == [None] * 4
    assert want["books"]["013"]["sheets"]["Plot schedule"][0][9] == ("blank",)
    # The plot the plan could not write keeps its row after the written ones, its number as text, its old number and nothing else
    assert lines[2] == ["103", 103] + [None] * 7 + [5103] + [None] * 12


def test_r552_floors_and_walls_read_the_plan_and_the_length_from_the_ends(made):
    paths, mock, want = made
    book = want["books"]["012"]
    assert book["sheets"]["Floors"][0] == [101, "generic", "Generic 150mm", "Level generic", 10.1, 10.35, 0.25, None, None]
    assert book["sheets"]["Floors"][1] == [101, "ramp", "Ramps 2%", "Level ramp", 10.1, 10.35, 0.25, 2.0, None]
    assert len(book["sheets"]["Floors"]) == 4
    assert book["sheets"]["Walls"] == [[101, 1, "102", 10.1, 10.4, ("near", 5.0), None]]


COUNTED = {"format": "siteAdaptationRevitCounts", "formatVersion": 1, "levelRun": None,
           "plots": {"101": {"links": 1, "floors": {"generic": 1, "yard": 0, "ramp": 1}, "level": True, "ids": {"link": [5001], "generic": [5002], "ramp": [5004, 5003]}},
                     "102": {"links": 1, "floors": {"generic": 1, "yard": 1, "ramp": 0}, "level": True, "ids": {"link": [6001]}}},
           # The first piece of 101 on edge 1 is one id, the second two, the wall of 102 none, the wall of 201 falls in the other book
           "walls": {"101|101+102|e1|p1": [7001], "101|101+102|e1|p2": [7003, 7002], "201|201|e2|p1": [8001]}}
PIECE = dict(PLAN["walls"][0], ends=read([[0.0, 0.0], [1.0, 1.0]]))
WALLED = dict(PLAN, walls=[PLAN["walls"][0], dict(PIECE, plot="102", edge=1, plots=read(["102", "101"])), PIECE,
                           dict(PIECE, plot="201", edge=2, plots=read(["201"]))])


def test_q307_placed_gains_one_row_per_wall_with_its_ids_after_the_plot_rows_and_plot_stays_text(tmp_path):
    paths, mock, want = build(tmp_path, revit=COUNTED, plan=WALLED)
    book = want["books"]["012"]
    placed = book["sheets"]["Placed"]
    assert placed[-3:] == [["101", "boundaryWall", None, None, 7001], ["102", "boundaryWall", None, None, None], ["101", "boundaryWall", None, None, "7002, 7003"]]
    assert all(row[1] != "boundaryWall" for row in placed[:-3])
    assert want["books"]["013"]["sheets"]["Placed"][-1] == ["201", "boundaryWall", None, None, 8001]
    # One wall key on two kinds of row: the walls repeat plot and kind, the check of one row per plot and kind passes them over
    result, bad = check.check_xlsx(round_trip(tmp_path, want, mock), book, mock, "rx1")
    assert not bad, bad


def test_q307_the_walls_row_of_the_box_stands_only_when_the_counts_hold_walls_and_a_mismatch_is_an_issue(tmp_path):
    paths, mock, want = build(tmp_path, revit=COUNTED, plan=WALLED)
    book = want["books"]["012"]
    # The plan holds 3 walls in the book, the model 3 ids on 2 walls of the plots of the book, Excel 3 rows
    assert book["counts"][-1] == ["Walls", 3, 3]
    assert not [row for row in book["sheets"]["Issues"] if row[3] == "Walls"]
    fewer = dict(COUNTED, walls={"101|101+102|e1|p1": [7001]})
    book = build(tmp_path, revit=fewer, plan=WALLED)[2]["books"]["012"]
    assert book["counts"][-1] == ["Walls", 1, 3]
    assert ["9 Report", None, "Counts", "Walls", None, None, "the plan holds 3, Revit 1, Excel 3 (R381)", "no manual rule block"] in book["sheets"]["Issues"]
    bare = {key: value for key, value in COUNTED.items() if key != "walls"}
    book = build(tmp_path, revit=bare, plan=WALLED)[2]["books"]["012"]
    assert [row[0] for row in book["counts"]] == ["Links", "Floors", "Levels"]
    assert book["sheets"]["Placed"][-1] == ["101", "boundaryWall", None, None, None]
    with pytest.raises(ValueError, match="Q307"):
        build(tmp_path, revit=dict(COUNTED, walls=["7001"]), plan=WALLED)


def test_q307_the_piece_counts_one_plot_and_stem_over_all_the_plan():
    parts = check.wall_parts(WALLED["walls"])
    assert parts == {0: "101+102|e1|p1", 1: "102+101|e1|p1", 2: "101+102|e1|p2", 3: "201|e2|p1"}


def test_r552_the_ids_of_the_model_fill_the_id_columns_a_single_id_a_number(tmp_path):
    paths, mock, want = build(tmp_path, revit=COUNTED, plan=WALLED)
    book = want["books"]["012"]
    first, second, unwritten = book["sheets"]["Plot schedule"]
    assert first[17:] == [5001, 5002, None, "5003, 5004", "7001, 7002, 7003"]
    # 102 holds the two pieces of 101 on their shared edge, by their ids under the owner 101
    assert second[17:] == [6001, None, None, None, "7001, 7002, 7003"]
    assert unwritten[17:] == [None] * 5
    assert want["books"]["013"]["sheets"]["Plot schedule"][0][-1] == 8001
    assert [row[-1] for row in book["sheets"]["Walls"]] == [7001, None, "7002, 7003"]
    assert [row[-1] for row in book["sheets"]["Floors"]][:2] == [5002, "5003, 5004"]


def test_r552_wall_ids_read_all_walls_of_the_plan_in_plan_order_also_those_of_another_book(tmp_path):
    shared = dict(PIECE, plot="201", edge=3, plots=read(["201", " 102", "103"]))
    counts = dict(COUNTED, walls=dict(COUNTED["walls"], **{"201|201+ 102+103|e3|p1": [8002]}))
    paths, mock, want = build(tmp_path, revit=counts, plan=dict(WALLED, walls=WALLED["walls"] + [shared]))
    first, second, unwritten = want["books"]["012"]["sheets"]["Plot schedule"]
    assert first[-1] == "7001, 7002, 7003"
    assert second[-1] == "7001, 7002, 7003, 8002"
    assert unwritten[-1] is None
    assert want["books"]["013"]["sheets"]["Plot schedule"][0][-1] == "8001, 8002"
    # The sheet Walls keeps the walls tagged under the plots of the book, only the schedule reads across
    assert len(want["books"]["012"]["sheets"]["Walls"]) == 3
    alone = dict(COUNTED, walls={"201|201+ 102+103|e3|p1": [8002]})
    lines = build(tmp_path, revit=alone, plan=dict(PLAN, walls=[shared]))[2]["books"]["012"]["sheets"]["Plot schedule"]
    assert lines[1][-1] == 8002 and lines[0][-1] is None


@pytest.mark.parametrize("fault, caught", [
    # R552: the id of a wall shared with 101 left off the row of 102
    (lambda book: book["Plot schedule"].cell(row=3, column=22, value="7001, 7002"), "Plot schedule row 3 Wall IDs"),
    (lambda book: setattr(book["Plot schedule"].cell(row=3, column=22), "value", None), "Plot schedule row 3 Wall IDs"),
    # R552: the row of the plot the plan could not write
    (lambda book: book["Plot schedule"].delete_rows(4), "Plot schedule max_row"),
    (lambda book: book["Plot schedule"].cell(row=4, column=1, value="103-"), "Plot schedule row 4 Plot & Unit"),
    (lambda book: book["Plot schedule"].cell(row=4, column=2, value="103"), "Plot schedule row 4 Plot No."),
    (lambda book: book["Plot schedule"].cell(row=4, column=3, value="DP1"), "Plot schedule row 4 Unit"),
    (lambda book: setattr(book["Plot schedule"].cell(row=4, column=10), "value", None), "Plot schedule row 4 Old Plot No."),
])
def test_r552_a_fault_of_the_schedule_is_caught(tmp_path, fault, caught):
    paths, mock, want = build(tmp_path, revit=COUNTED, plan=WALLED)
    path = round_trip(tmp_path, want, mock)
    assert not check.check_xlsx(path, want["books"]["012"], mock, "rx1")[1]
    opened = openpyxl.load_workbook(path)
    fault(opened)
    opened.save(path)
    result, bad = check.check_xlsx(path, want["books"]["012"], mock, "rx1")
    assert any(caught in item for item in bad), bad


def test_r553_a_plot_with_a_letter_or_a_leading_zero_stays_text():
    assert [check.plot_cell(value) for value in ("101", "0101", "12A", "101|102", "", None)] == [101, "0101", "12A", "101|102", "", None]
    assert check.as_numbers([["101", "0102", "103"]], ("Plot", "Other plot", "Old Plot No.")) == [[101, "0102", 103]]


CHANGED_RULES = [{"id": "M04", "name": "Step height", "manual": 0.15, "project": 0.17, "unit": "m"}, {"id": "L2", "name": "Round up", "manual": "yes", "project": 1.0, "unit": None}]


def test_r555_a_changed_rule_gives_the_column_changed_and_its_row_and_none_keeps_five_columns(tmp_path):
    paths, mock, want = build(tmp_path, facts=dict(FACTS, changedRules=CHANGED_RULES))
    book = want["books"]["012"]
    lines = book["sheets"]["Rules used"]
    assert check.sheet_headers(mock, book)["Rules used"] == ["Rule", "Status", "Basis", "Plots", "Values", "Changed"]
    assert lines[-2:] == [["M04 Step height", None, None, None, "manual 0.15, project 0.17 m", "Changed"], ["L2 Round up", None, None, None, "manual yes, project 1", "Changed"]]
    assert all(len(line) == 6 and line[5] is None for line in lines[:-2])
    path = round_trip(tmp_path, want, mock)
    result, bad = check.check_xlsx(path, book, mock, "rx1")
    assert not bad, bad
    assert result["match Rules used Changed"] == len(lines)
    opened = openpyxl.load_workbook(path)
    opened["Rules used"].cell(row=len(lines) + 1, column=5, value="manual yes, project 2")
    opened.save(path)
    assert any("Rules used row %d Values" % (len(lines) + 1) in item for item in check.check_xlsx(path, book, mock, "rx1")[1])
    for empty in ({}, {"changedRules": []}):
        book = build(tmp_path, facts=dict(FACTS, **empty))[2]["books"]["012"]
        assert not book["changed"] and len(check.sheet_headers(mock, book)["Rules used"]) == 5 and all(len(line) == 5 for line in book["sheets"]["Rules used"])
