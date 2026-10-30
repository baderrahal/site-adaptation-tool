"""R402. The second route of Update from report, python/update_check.py, on a made up plan whose level Excel and report Excel are written
here with openpyxl in the layout the worker writes, then edited cell by cell, the same plan, rules, block manualRules and override list as
R402_UpdateFromReportTests of C# (R10), so both routes give the same changes, checks, tiles, chips and entries."""
import copy
import json

import openpyxl
import pytest

import majority
import update_check as check

SHA = "aaaa000000000000000000000000000000000000000000000000000000000001"
PROFILE = "parsons2024Rev6"


def plot(number, unit, road, entry, ffl, height, steps, mirrored, **floors):
    levels = {"ffl": ffl, "ssl": check.fixed(ffl - 0.075, 3), "entry": entry, "height": height, "steps": steps, "roadAtFrontM": road}
    return {"plot": number, "unit": {"value": unit}, "mirrored": {"value": mirrored}, "levels": {key: {"value": value} for key, value in levels.items()},
            "levelRule": {"entry": entry, "ffl": ffl, "height": height, "steps": steps}, "floors": {kind: {"type": {"value": name}} for kind, name in floors.items()}}


PLAN = {"summary": {"rules": {"levelProfile": PROFILE}},
        "plots": [plot("101", "VD4", 9.98, 10.0, 10.2, 0.2, 1, False, generic="Generic 300"),
                  plot("102", "DP1", 10.18, 10.2, 10.3, 0.1, 1, True, generic="Generic 300", ramp="Ramps 0.5%"),
                  plot("103", "DP1", 10.03, 10.05, 10.3, 0.25, 2, False, generic="Generic 300")],
        "missing": [{"plot": "104", "written": False, "missing": [{"key": "levels.ffl", "why": "no road level"}]}]}

RULES = {"typologies": {"VD4": {}, "DP1": {}, "C10": {}},
         "steps": {"front_min": 1, "front_max": 3, "riser_max": 180, "riser_min": 100, "riser_added": 170,
                   "legend": [[0.1, 1], [0.2, 1], [0.27, 2], [0.37, 2], [0.44, 3], [0.54, 3]], "base_height": {"default": 100, "VD4": 200}, "one_step_only": ["C10"]},
         "levelProfiles": {PROFILE: {"job": "parsons", "stepBands": [[0.205, 1], [0.38, 2], [99.0, 3]],
                                     "keyRules": {"steps": {"rule": "stepsFromHeight"}, "baseHeight": {"rule": "profile"}}}},
         "manualRules": [{"id": "M08", "text": "made up", "updateChecks": ["stepsAtMost", "stepHeights"]}, {"id": "M09", "text": "made up", "updateChecks": ["riser"]}]}


def entry(number, field, value, source, key):
    return {"plot": number, "field": field, "value": value, "rule": None, "source": source, "model": "m_%s.rvt" % key, "key": key, "decision": source + " " + field, "reason": "made up"}


OVERRIDES = {"note": "made up",
             "overrides": [entry("101", "heightM", {"gapToRuleM": 0.01}, "parsons", "img"), entry("102", "entryM", {"gapToRuleM": 0.02}, "native", "u12"),
                           entry("102", "rampFrontM", {"gapToRuleM": -0.002}, "parsons", "imc"), entry("103", "parameter CASE", "D", "parsons", "imc")],
             "cannot": [{"plot": "9", "field": "rampFrontFallM", "source": "native", "model": "m_u13.rvt", "key": "u13", "decision": "d", "reason": "r"}]}


def settings(sheet, editable):
    for row in (("Setting", "Value"), ("Run id", "x1"), ("Editable columns", editable), ("Plan build id", check.NO_BUILD), ("Plan SHA-256", SHA)):
        sheet.append(row)


def write_level(path, plan=PLAN):
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "Levels"
    sheet.append(("NBH 012, made up top line",))
    sheet.append(check.LEVEL_COLUMNS)
    cluster = {"101": "none", "102": "102-103", "103": "102-103"}
    for item in plan["plots"]:
        lv = {key: value["value"] for key, value in item["levels"].items()}
        sheet.append((item["plot"], item["unit"]["value"], cluster[item["plot"]], lv["roadAtFrontM"], lv["entry"], lv["ffl"], lv["ssl"], lv["height"], lv["steps"], "Rule"))
    sheet.append(("104", None, "none", None, None, None, None, None, None, "Not written: levels.ffl (no road level)"))
    settings(book.create_sheet("Settings"), "El @ Entry, FFL, Steps")
    book.save(path)


def write_report(path):
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "Placed"
    sheet.append(check.PLACED_COLUMNS)
    for item in PLAN["plots"]:
        sheet.append((item["plot"], "link", item["unit"]["value"], item["mirrored"]["value"], None))
        for kind, floor in item["floors"].items():
            sheet.append((item["plot"], kind, floor["type"]["value"], None, None))
    settings(book.create_sheet("Settings"), "Type, Mirror")
    book.save(path)


def edit(path, sheet, reference, number, value):
    book = openpyxl.load_workbook(path)
    assert book[sheet]["A" + reference.lstrip("ABCDEFGHIJ")].value == number
    book[sheet][reference] = value
    book.save(path)


@pytest.fixture
def books(tmp_path):
    level, report = tmp_path / "012_Levels_parsons2024Rev6.xlsx", tmp_path / "012_Report.xlsx"
    write_level(level)
    write_report(report)
    for reference, number, value in (("F3", "101", 10.25), ("E4", "102", 10.15), ("I5", "103", 3), ("G3", "101", 10.5), ("F6", "104", 11.0)):
        edit(level, "Levels", reference, number, value)
    for reference, number, value in (("C2", "101", "C10"), ("D4", "102", False), ("C5", "102", "Generic 200"), ("C7", "103", "ZZ9")):
        edit(report, "Placed", reference, number, value)
    return [check.read_book(level), check.read_book(report)]


def test_r402_an_edited_copy_lists_each_change_its_rule_check_the_tiles_and_the_chips(books):
    found = check.result(books, PLAN, SHA, RULES, PROFILE, None, "u1")
    assert found["refusal"] is None
    assert [(item["plot"], item["field"], item["inModel"], item["inExcel"], item["ruleCheck"], item["ticked"]) for item in found["changes"]] == [
        ("101", "FFL", 10.2, 10.25, "stepHeights, M08", False),
        ("102", "El @ Entry", 10.2, 10.15, "follows the rules", True),
        ("103", "Steps", 2, 3, "stepsRule, riser, M09", False),
        ("101", "Villa type", "VD4", "C10", "follows the rules", True),
        ("102", "Mirror", True, False, "follows the rules", True),
        ("103", "Villa type", "DP1", "ZZ9", "villaType", True)]
    assert [(item["plot"], item["column"]) for item in found["ignored"]] == [("101", "SSL"), ("104", "FFL"), ("102", "Type")]
    assert found["tiles"] == {"Changes found": 6, "Follow the rules": 3, "Break a manual rule": 2, "Not editable ignored": 3}
    assert found["chips"] == {"All": 6, "FFL": 1, "El @ Entry": 1, "Steps": 1, "Villa type": 2, "Mirror": 1, "Breaks a rule": 3}
    assert found["lacks"] == []


def test_r402_apply_saves_each_ticked_change_as_an_override_that_gives_back_its_cell(books):
    found = check.result(books, PLAN, SHA, RULES, PROFILE, OVERRIDES, "u1", tick="all")
    assert found["plots"] == ["101", "102", "103"] and found["notApplied"] == []
    rules = {item["plot"]: item["levelRule"] for item in PLAN["plots"]}
    for item in found["changes"]:
        made = next(entry for entry in found["entries"] if entry["plot"] == item["plot"] and entry["field"] == check.FIELDS[item["field"]])
        back = {"El @ Entry": lambda: check.fixed(rules[item["plot"]]["entry"] + made["value"]["gapToRuleM"], 4),
                "FFL": lambda: check.fixed(rules[item["plot"]]["ffl"] + made["value"]["gapToRuleM"], 3)}.get(item["field"], lambda: made["value"])()
        assert back == item["inExcel"] and made["byHand"] is True and made["reason"] == "Update from report u1" and made["source"] == "parsons"
    listing = json.loads(found["listText"])
    assert [(item["plot"], item["field"], item["source"], item["key"]) for item in listing["overrides"]] == [
        ("101", "heightM", "parsons", "img"), ("101", "unit", "parsons", "img"), ("102", "entryM", "native", "u12"), ("102", "rampFrontM", "parsons", "imc"),
        ("102", "entryM", "parsons", "imc"), ("102", "mirrored", "parsons", "imc"), ("103", "parameter CASE", "parsons", "imc"), ("103", "steps", "parsons", "imc"),
        ("103", "unit", "parsons", "imc")]
    assert listing["overrides"][0]["value"] == {"gapToRuleM": 0.05} and listing["overrides"][4]["value"] == {"gapToRuleM": -0.05}
    assert listing["cannot"] == OVERRIDES["cannot"] and found["replaced"] == [OVERRIDES["overrides"][0]]
    # The list text is the form majority.py writes, so a majority run reads it as its own
    assert found["listText"] == majority.overrides_text(listing["note"], listing["overrides"], listing["cannot"])


def test_r402_the_default_ticks_leave_the_changes_that_break_a_manual_rule(books):
    found = check.result(books, PLAN, SHA, RULES, PROFILE, OVERRIDES, "u2")
    assert [(item["plot"], item["field"]) for item in found["entries"]] == [("102", "entryM"), ("101", "unit"), ("102", "mirrored"), ("103", "unit")]
    assert found["plots"] == ["102", "101", "103"] and found["replaced"] == []


def test_r402_an_excel_of_another_build_is_refused_with_one_line(books):
    other = dict(PLAN, run={"runId": "b2"})
    assert check.result(books, other, SHA, RULES, PROFILE, OVERRIDES, "u3")["refusal"] == (
        "012_Levels_parsons2024Rev6.xlsx is of another build, its Plan build id is none, the plan holds no run stamp, the plan of the model b2 (R402)")
    found = check.result(books, PLAN, "bbbb", RULES, PROFILE, OVERRIDES, "u3")
    assert found["refusal"] == "012_Levels_parsons2024Rev6.xlsx is of another build, its Plan SHA-256 is %s, the plan of the model bbbb (R402)" % SHA
    assert found["changes"] == [] and "entries" not in found


def test_r402_with_no_manual_rules_block_nothing_is_unticked_and_the_lack_is_named(books):
    rules = {key: value for key, value in RULES.items() if key != "manualRules"}
    found = check.result(books, PLAN, SHA, rules, PROFILE, None, "u4")
    assert all(item["ticked"] for item in found["changes"]) and found["tiles"]["Break a manual rule"] == 0
    assert found["lacks"] == ["the rules profile holds no block manualRules, so no change breaks a manual rule (R402)"]


def test_r402_after_a_level_edit_a_plot_with_a_steps_override_keeps_its_override_count_in_the_checks(tmp_path):
    """Breaker 5f: the build keeps the count of a steps override when it gives the levels again, so 103 at 0.30 m with 2 steps by the legend
    keeps 2 after an El @ Entry edit to 0.36 m, where the steps rule gives 3."""
    plan = copy.deepcopy(PLAN)
    item = plan["plots"][2]
    item["levels"]["entry"]["value"], item["levels"]["height"]["value"], item["levelRule"]["entry"] = 10.0, 0.3, 10.0
    item["overrides"] = {"applied": ["steps"], "notApplied": {}}
    level = tmp_path / "012_Levels_parsons2024Rev6.xlsx"
    write_level(level, plan)
    edit(level, "Levels", "E5", "103", 9.94)
    found = check.result([check.read_book(level)], plan, SHA, RULES, PROFILE, None, "u6")
    assert [(change["plot"], change["field"], change["breaks"], change["ticked"]) for change in found["changes"]] == [("103", "El @ Entry", [], True)]
    del item["overrides"]
    found = check.result([check.read_book(level)], plan, SHA, RULES, PROFILE, None, "u6")
    assert found["changes"][0]["breaks"] == ["stepHeights"]


def test_r402_a_change_of_a_plot_with_no_override_entry_of_its_job_is_not_savable_unticked_and_never_applied(books):
    """Breaker 5f: Apply takes the key and model of an entry of the job, so the changes of a plot with none are marked at once (Q169)."""
    overrides = copy.deepcopy(OVERRIDES)
    del overrides["overrides"][2]
    found = check.result(books, PLAN, SHA, RULES, PROFILE, overrides, "u7")
    why = "the plot has no override entry of the job parsons, so Apply can not save it (Q169)"
    assert [(item["plot"], item["field"], item["invalid"], item["ticked"]) for item in found["changes"]] == [
        ("101", "FFL", None, False), ("102", "El @ Entry", why, False), ("103", "Steps", None, False), ("101", "Villa type", None, True),
        ("102", "Mirror", why, False), ("103", "Villa type", None, True)]
    assert [(item["plot"], item["field"]) for item in found["entries"]] == [("101", "unit"), ("103", "unit")]
    assert found["notApplied"] == [] and found["plots"] == ["101", "103"]


def test_r402_the_compare_counts_each_cell_and_names_a_planted_mismatch(books):
    found = check.result(books, PLAN, SHA, RULES, PROFILE, OVERRIDES, "u5", tick="all")
    sharp = json.loads(json.dumps(found))
    assert check.compare(found, sharp)["mismatches"] == 0
    planted = copy.deepcopy(sharp)
    planted["entries"][0]["value"] = {"gapToRuleM": 0.06}
    planted["changes"][2]["ticked"] = True
    tally = check.compare(found, planted)
    assert tally["mismatches"] == 2 and tally["conflicts"][0] == "changes 2 ticked plot 103 row 5"


def test_r402_the_counts_go_outside_the_repo():
    with pytest.raises(check.Refused, match="inside the repo"):
        check.outside_repo(check.REPO / "sample_output")
