"""R363. The check rows of button 3 and the chip Assumed of Q108, python/check_rows.py, on plots, entries and statuses made up in code (R10).
CheckRowTests.cs of the C# holds the same made up data and the same rows, so the two sides give one check table."""
import json

import pytest

import check_rows
import plan_from_book
import project_profile

BLOCK = project_profile.load_settings().get("checkRows")
GAP = "a build places the outline of its rule and leaves these (LEFT of plan_from_book.py)"
# Plot 1 follows every rule, 2 is written with overrides, a question and a yard it lacks, 3 is not written, 4 has no unit, 5 is on the skip list
PLOTS = {
    "1": {"unit": "VD2", "nbh": "N1", "skip": None, "applied": [], "notApplied": {}, "lacks": []},
    "2": {"unit": "DP2", "nbh": "N1", "skip": None, "applied": ["entryM", "parameter CASE"],
          "notApplied": {"rampFrontFallM": "a ramp front fall is not applied (Q057)"}, "lacks": ["floors.yard"]},
    "3": {"unit": "DP2", "nbh": "N1", "skip": None, "applied": [], "notApplied": {"mirrored": "the plot is not written, it lacks levels.ffl"},
          "lacks": ["levels.ffl", "parameters.RRE_ZZ_SP_Prototype-FFL"]},
    "4": {"unit": None, "nbh": None, "skip": None, "applied": [], "notApplied": {}, "lacks": ["unit"]},
    "5": {"unit": "VD4", "nbh": "N2", "skip": "a made up reason", "applied": [], "notApplied": {"heightM": "the plot is not written, it lacks skip"},
          "lacks": ["skip"]},
}
LIST = {
    "overrides": [
        {"plot": "2", "field": "entryM", "value": {"gapToRuleM": 0.0077}, "rule": None, "decision": "parsons entry", "source": "parsons", "key": "imc", "model": "a.rvt"},
        {"plot": "2", "field": "entryM", "value": {"gapToRuleM": 0.5}, "rule": None, "decision": "native entry", "source": "native", "key": "u1", "model": "b.rvt"},
        {"plot": "2", "field": "parameter CASE", "value": "F", "rule": "D", "decision": "parsons parameter CASE", "source": "parsons", "key": "imc", "model": "a.rvt"},
        {"plot": "2", "field": "rampFrontFallM", "value": {"gapToRuleM": 0.1}, "rule": None, "decision": "parsons rampFrontFall", "source": "parsons", "key": "imc",
         "model": "a.rvt"},
        {"plot": "3", "field": "mirrored", "value": True, "rule": False, "decision": "parsons hand villa", "source": "parsons", "key": "imc", "model": "a.rvt"},
        {"plot": "5", "field": "heightM", "value": {"gapToRuleM": 0.001}, "rule": None, "decision": "parsons ffl", "source": "parsons", "key": "imc", "model": "a.rvt"}],
    "cannot": [
        {"plot": "3", "field": "rampFrontM", "decision": "parsons rampFront", "reason": "a made up reason of the key", "source": "parsons", "key": "imc", "model": "a.rvt"},
        {"plot": "9", "field": "entryM", "decision": "parsons entry", "reason": "a plot outside the scope", "source": "parsons", "key": "imc", "model": "a.rvt"},
        {"plot": "2", "field": "heightM", "decision": "native ffl", "reason": "another job", "source": "native", "key": "u1", "model": "b.rvt"}]}
DECIDED = {"note": "a made up note", "Q017": {"fields": ["prototypeFile", "mirrored"], "plots": {"imc": ["2", "9"]}, "why": "a made up why"}}
NUMBERS = ["1", "2", "3", "4", "5"]


def row(check, field, came, use, rule=None, tool=None, writes=None, value=None, not_offered=None, why=None):
    return {"check": check, "field": field, "from": came, "use": use, "rule": rule, "toolValue": tool, "past": {"writes": writes, "value": value, "why": not_offered},
            "why": why}


def made_rows():
    overrides, cannot = check_rows.list_entries(LIST, ["parsons"])
    return check_rows.plot_rows(NUMBERS, PLOTS, overrides, cannot, DECIDED, BLOCK)


def test_r363_parity_check_rows_one_row_per_plot_and_check_with_its_type_field_and_use():
    found = made_rows()
    assert found["1"] == []
    assert found["2"] == [
        row("Floor", "floors.yard", "missing", "Rule", not_offered=GAP),
        row("Hand", "mirrored", "question", "Rule", rule="Q017", writes="trueOrFalse", why="a made up why"),
        row("Unit code", "prototypeFile", "question", "Rule", rule="Q017", why="a made up why",
            not_offered="no field of the list gives a prototype file, the code is Typology_Rev9 (Q017) and never a key's (Q056)"),
        row("Level", "entryM", "override", "Past", rule="parsons entry", writes="gapToRuleM", value={"gapToRuleM": 0.0077}),
        row("Level", "rampFrontFallM", "override", "Rule", rule="parsons rampFrontFall", value={"gapToRuleM": 0.1}, not_offered="a build never applies a fall (Q057)",
            why="a ramp front fall is not applied (Q057)"),
        row("Parameter", "parameter CASE", "override", "Past", rule="parsons parameter CASE", tool="D", writes="text", value="F")]
    assert found["3"] == [
        row("Hand", "mirrored", "override", "Rule", rule="parsons hand villa", tool=False, writes="trueOrFalse", value=True, why="the plot is not written, it lacks levels.ffl"),
        row("Level", "levels.ffl", "missing", "Rule", not_offered="the rule gives no level to hold a gap to"),
        row("Level", "rampFrontM", "cannot", "Rule", rule="parsons rampFront", not_offered=BLOCK["cannotWhy"], why="a made up reason of the key"),
        row("Parameter", "parameters.RRE_ZZ_SP_Prototype-FFL", "missing", "Rule", not_offered="the rule gives no value to hold a gap to")]
    assert found["4"] == [row("Unit code", "unit", "missing", "Rule", not_offered=BLOCK["missing"]["unit"]["why"])]
    # A plot on the skip list opens every row on Skip
    assert found["5"] == [
        row("Level", "heightM", "override", "Skip", rule="parsons ffl", writes="gapToRuleM", value={"gapToRuleM": 0.001}, why="the plot is not written, it lacks skip"),
        row("Skip", "skip", "missing", "Skip", not_offered=BLOCK["missing"]["skip"]["why"])]


def test_r363_past_writes_in_the_form_of_the_list_by_check_and_field_as_the_design_gives_it():
    # docs/design/03b-buttons-2-to-8.md 3 Check, the table of what Past writes by check and field
    forms = {name: (item["check"], item["past"]) for name, item in BLOCK["fields"].items()}
    assert forms == {
        "entryM": ("Level", "gapToRuleM"), "heightM": ("Level", "gapToRuleM"), "rampFrontM": ("Level", "gapToRuleM"), "steps": ("Level", "wholeNumber"),
        "rampFrontFallM": ("Level", None), "insert": ("Position", "gapToRuleM"), "rotationDeg": ("Position", "gapToRuleDeg"), "mirrored": ("Hand", "trueOrFalse"),
        "rampType": ("Floor", "slopeClass"), "genericFloor": ("Floor", None), "yardOutline": ("Floor", None), "rampOutline": ("Floor", None),
        "parameter RRE_ZZ_SP_Prototype-FFL": ("Parameter", "gapToRuleM"), "parameter *": ("Parameter", "text"), "prototypeFile": ("Unit code", None)}
    assert all(item.get("why") for item in list(BLOCK["fields"].values()) + list(BLOCK["missing"].values()) if item["past"] is None)
    # The check types of the mock in its order (docs/addin-mock.md item 4)
    assert BLOCK["types"] == ["Plot cut", "Floor", "Hand", "Unit code", "Position", "Level"]
    assert check_rows.entry_of(BLOCK, "fields", "parameter RRE_ZZ_SP_RVT-Name")["past"] == "text"
    assert check_rows.entry_of(BLOCK, "missing", "levels.entry")["check"] == "Level"
    with pytest.raises(project_profile.ProfileError) as error:
        check_rows.entry_of(BLOCK, "fields", "foo")
    assert str(error.value) == "checkRows.fields names no check for foo (R363)"


def test_r363_the_chips_count_the_rows_by_type_in_the_order_of_the_mock_and_the_plots_that_follow_every_rule():
    found = made_rows()
    chips = check_rows.chips(NUMBERS, found, {"a | UNKNOWN": {}}, BLOCK)
    assert list(chips) == ["All", "Plot cut", "Floor", "Hand", "Unit code", "Position", "Level", "Parameter", "Skip", "Assumed", "followEveryRule"]
    assert chips == {"All": 13, "Plot cut": 0, "Floor": 1, "Hand": 2, "Unit code": 2, "Position": 0, "Level": 5, "Parameter": 2, "Skip": 1, "Assumed": 1,
                     "followEveryRule": 1}


# The details of the statuses of the plan, as the table statusFrom holds them (R401)
FFL = "rules/r.json levelProfiles.p.keyRules.ffl.rule"
ELEVATION = "the elevation of the level of the floor in the new site file"
DETAILS = {
    "levels.ffl from estimate": {"entries": [{"entry": FFL, "status": "MEASURED", "share": 0.7465, "strength": "weak"}], "strength": "weak"},
    "levels.ffl from override by heightM": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "a made up place"}], "override": "heightM"},
    "floors.generic.heightOffset from estimate": {"entries": [{"entry": "rules/r.json levelProfiles.p.keyRules.generic.rule", "status": "MEASURED", "share": 0.99,
                                                               "strength": "strong"}, {"entry": FFL, "status": "MEASURED", "share": 0.7465, "strength": "weak"},
                                                              {"status": "UNKNOWN", "why": ELEVATION}]},
    "prototypeFile from computed question Q017 imc": {"entries": [{"status": "DOCUMENT", "document": "the prototype folder"},
                                                                  {"entry": "config/project.json majority.decidedByQuestion.Q017.plots.imc", "status": "RECOMMENDED",
                                                                   "question": "Q017"}]},
    "unit from locationPlan": {"entries": [{"status": "DOCUMENT", "document": "the location plan"}]},
    "floors.generic.outline from computed": {"entries": [{"entry": "rules/r.json levelProfiles.p.keyRules.generic.rule", "status": "MEASURED", "share": 0.99,
                                                          "strength": "strong"}], "strength": "strong"},
    "walls.top from keyRules": {"entries": [{"entry": "rules/r.json levelProfiles.p.keyRules.wallTop.rule", "status": "MEASURED", "share": 0.95, "strength": "strong"},
                                            {"entry": FFL, "status": "MEASURED", "share": 0.7465, "strength": "weak"}], "strength": "weak"},
    "parameters.CASE from keyRules": {"entries": [{"entry": "rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule", "status": "CHOSEN by Code"}]},
    "mirrored from override": {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "a made up place"}]}}
VALUES = [("levels.ffl", ["1"], "MEASURED", "levels.ffl from estimate", "estimate"), ("levels.ffl", ["2"], "MEASURED", "levels.ffl from estimate", "estimate"),
          ("levels.ffl", ["3"], "RECOMMENDED", "levels.ffl from override by heightM", "estimate"),
          ("floors.generic.heightOffset", ["1"], "UNKNOWN", "floors.generic.heightOffset from estimate", "estimate"),
          ("prototypeFile", ["2"], "RECOMMENDED", "prototypeFile from computed question Q017 imc", "computed"),
          ("unit", ["1"], "DOCUMENT", "unit from locationPlan", "locationPlan"), ("floors.generic.outline", ["1"], "MEASURED", "floors.generic.outline from computed", "computed"),
          ("walls.top", ["2", "1"], "MEASURED", "walls.top from keyRules", "keyRules"),
          ("parameters.CASE", ["10"], "CHOSEN by Code", "parameters.CASE from keyRules", "keyRules"),
          ("parameters.CASE", ["9"], "CHOSEN by Code", "parameters.CASE from keyRules", "keyRules"),
          # A value of the source override, whose detail names no override as no override moved it, has its own row under its check type
          ("mirrored", ["4"], "RECOMMENDED", "mirrored from override", "override")]
ASSUMED = ["RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"]
WORST = ["UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak"]


def test_r363_q108_the_assumed_chip_groups_by_rule_one_row_per_rule_and_status_with_its_plots():
    found = check_rows.assumed_rows(VALUES, DETAILS, ASSUMED, WORST)
    assert found == {
        FFL + " | MEASURED weak": {"rule": FFL, "status": "MEASURED weak", "plotCount": 2, "plots": ["1", "2"], "values": 3, "fields": ["levels.ffl", "walls.top"]},
        ELEVATION + " | UNKNOWN": {"rule": ELEVATION, "status": "UNKNOWN", "plotCount": 1, "plots": ["1"], "values": 1, "fields": ["floors.generic.heightOffset"]},
        "config/project.json majority.decidedByQuestion.Q017.plots.imc | RECOMMENDED": {
            "rule": "config/project.json majority.decidedByQuestion.Q017.plots.imc", "status": "RECOMMENDED", "plotCount": 1, "plots": ["2"], "values": 1,
            "fields": ["prototypeFile"]},
        "rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule | CHOSEN by Code": {
            "rule": "rules/r.json levelProfiles.p.keyRules.parameters.CASE.rule", "status": "CHOSEN by Code", "plotCount": 2, "plots": ["9", "10"], "values": 2,
            "fields": ["parameters.CASE"]}}
    # DOCUMENT, strong MEASURED and a value an override sets are not shown, the last has its own row under its check type
    shown = sum(item["values"] for item in found.values())
    assert shown == len(VALUES) - 4


def sourced(source, status, key, value=1):
    return {"value": value, "source": source, "status": status, "statusFrom": key}


def test_r363_q108_the_values_of_a_plan_each_value_of_a_written_plot_and_of_a_wall_on_its_written_plots():
    plan = {"plots": [{"plot": "1", "unit": sourced("locationPlan", "DOCUMENT", "u"), "levels": {"ffl": sourced("estimate", "MEASURED", "f")}},
                      {"plot": "9", "unit": sourced("locationPlan", "DOCUMENT", "u")}],
            "walls": [{"plot": "1", "plots": sourced("locationPlan", "DOCUMENT", "p", ["1", "3"]), "ends": sourced("keyRules", "UNKNOWN", "e"),
                       "base": sourced("keyRules", "MEASURED", "b"), "top": sourced("keyRules", "MEASURED", "t")},
                      {"plot": "3", "plots": sourced("locationPlan", "DOCUMENT", "p", ["3"]), "ends": sourced("keyRules", "UNKNOWN", "e"),
                       "base": sourced("keyRules", "MEASURED", "b"), "top": sourced("keyRules", "MEASURED", "t")}]}
    # Plot 9 is out of the scope and 3 is in it with no plan entry, so the first wall stands on plot 1 alone and the second on no written plot
    assert check_rows.plan_values(plan, ["1", "3"]) == [
        ("unit", ["1"], "DOCUMENT", "u", "locationPlan"), ("levels.ffl", ["1"], "MEASURED", "f", "estimate"), ("walls.plots", ["1"], "DOCUMENT", "p", "locationPlan"),
        ("walls.ends", ["1"], "UNKNOWN", "e", "keyRules"), ("walls.base", ["1"], "MEASURED", "b", "keyRules"), ("walls.top", ["1"], "MEASURED", "t", "keyRules")]


def test_r363_q108_the_rule_of_a_value_is_its_worst_entry_by_worst_first_else_its_first():
    assert check_rows.rule_of(DETAILS["floors.generic.heightOffset from estimate"], WORST) == ELEVATION
    assert check_rows.rule_of(DETAILS["walls.top from keyRules"], WORST) == FFL
    assert check_rows.rule_of({"entries": [{"status": "DOCUMENT", "document": "a document"}, {"status": "DOCUMENT", "document": "another"}]}, WORST) == "a document"
    assert check_rows.rule_of({"entries": [{"status": "RECOMMENDED", "question": "Q053"}]}, WORST) == "Q053"


def test_r363_the_plots_of_a_plan_give_the_applied_and_left_overrides_and_the_keys_each_lacks():
    plan = {"plots": [{"plot": "2", "overrides": {"applied": ["entryM"], "notApplied": {"rampFrontFallM": "a ramp front fall is not applied (Q057)"}}},
                      {"plot": "1"}],
            "missing": [{"plot": "2", "written": True, "missing": [{"key": "floors.yard", "why": "a made up why"}]},
                        {"plot": "3", "written": False, "missing": [{"key": "levels.ffl", "why": "a"}, {"key": "levels.ffl", "why": "b"}],
                         "overrides": {"applied": [], "notApplied": {"mirrored": "the plot is not written, it lacks levels.ffl"}}},
                        {"plot": "5", "written": False, "missing": [{"key": "skip", "why": "c"}]}]}
    rows = {"1": {"typologyRev9": "VD2", "nbhId": "N1"}, "2": {"typologyRev9": "DP2", "nbhId": "N1"}, "3": {"typologyRev9": "DP2", "nbhId": "N1"}, "5": {"typologyRev9": "VD4"}}
    found = check_rows.plot_infos(["1", "2", "3", "4", "5"], plan, rows, {"5": "a made up reason"})
    assert found == {
        "1": {"unit": "VD2", "nbh": "N1", "skip": None, "applied": [], "notApplied": {}, "lacks": []},
        "2": {"unit": "DP2", "nbh": "N1", "skip": None, "applied": ["entryM"], "notApplied": {"rampFrontFallM": "a ramp front fall is not applied (Q057)"},
              "lacks": ["floors.yard"]},
        "3": {"unit": "DP2", "nbh": "N1", "skip": None, "applied": [], "notApplied": {"mirrored": "the plot is not written, it lacks levels.ffl"}, "lacks": ["levels.ffl"]},
        "4": {"unit": None, "nbh": None, "skip": None, "applied": [], "notApplied": {}, "lacks": []},
        "5": {"unit": "VD4", "nbh": None, "skip": "a made up reason", "applied": [], "notApplied": {}, "lacks": ["skip"]}}


def test_r363_the_config_names_the_fields_of_the_step_check_rows_and_the_build_steps_hold_it():
    settings = project_profile.load_settings()
    fields = settings.get("addin.parity.steps.checkRows")
    assert {name: item["kind"] for name, item in fields.items()} == {"unit": "noGeometry", "nbh": "noGeometry", "rows": "noGeometry"}
    assert plan_from_book.BUILD_STEPS[-1] == "checkRows" and plan_from_book.PARITY_STEPS[-1] == "checkRows"
    assert json.dumps(BLOCK).count(chr(59)) == 0


# The made up row of test_override_param_steps.row_build, plot 6 with no road level at a front corner and 8 a DP2 with no face
ROW_OVERRIDES = {"2": {"heightM": {"gapToRuleM": 0.1}, "rampOutline": {"gapToRuleM": 0.1}}, "6": {"entryM": {"gapToRuleM": 0.1}, "mirrored": False},
                 "8": {"mirrored": True}}
NO_FACE = ["transform", "levels.ffl", "parameters.CASE", "parameters.RRE_ZZ_SP_Number-of-Steps", "parameters.RRE_ZZ_SP_Prototype-FFL",
           "parameters.RRE_ZZ_SP_Prototype-Foundation-Type"]


def test_r363_parity_check_rows_of_a_made_up_build_the_lacks_of_written_and_unwritten_plots(monkeypatch):
    import plan_rules
    from test_override_param_steps import row_build
    built = row_build(monkeypatch, plan_rules.Overrides(ROW_OVERRIDES), faceless=("8",), dry=("6",))
    plan = {"plots": list(built["plan"].values()), "missing": list(built["missing"].values())}
    infos = check_rows.plot_infos(built["scope"], plan, built["rows"], {})
    assert {number: info["lacks"] for number, info in infos.items()} == {
        "1": ["floors.yard"], "2": [], "3": ["floors.yard"], "4": ["floors.yard"], "5": [], "6": ["levels.ffl", "parameters.RRE_ZZ_SP_Number-of-Steps",
                                                                                                "parameters.RRE_ZZ_SP_Prototype-FFL"], "8": NO_FACE}
    found = check_rows.plot_rows(built["scope"], infos, [], [], None, BLOCK)
    assert {number: [(item["check"], item["field"], item["from"], item["use"]) for item in made] for number, made in found.items()} == {
        "1": [("Floor", "floors.yard", "missing", "Rule")],
        "2": [("Floor", "rampOutline", "override", "Rule"), ("Level", "heightM", "override", "Past")],
        "3": [("Floor", "floors.yard", "missing", "Rule")], "4": [("Floor", "floors.yard", "missing", "Rule")], "5": [],
        "6": [("Hand", "mirrored", "override", "Rule"), ("Level", "entryM", "override", "Rule"), ("Level", "levels.ffl", "missing", "Rule"),
              ("Parameter", "parameters.RRE_ZZ_SP_Number-of-Steps", "missing", "Rule"), ("Parameter", "parameters.RRE_ZZ_SP_Prototype-FFL", "missing", "Rule")],
        "8": [("Hand", "mirrored", "override", "Rule"), ("Position", "transform", "missing", "Rule"), ("Level", "levels.ffl", "missing", "Rule"),
              ("Parameter", "parameters.CASE", "missing", "Rule"), ("Parameter", "parameters.RRE_ZZ_SP_Number-of-Steps", "missing", "Rule"),
              ("Parameter", "parameters.RRE_ZZ_SP_Prototype-FFL", "missing", "Rule"), ("Parameter", "parameters.RRE_ZZ_SP_Prototype-Foundation-Type", "missing", "Rule")]}


def test_r363_the_step_check_rows_of_a_build_with_the_tables_assumed_and_chips(monkeypatch):
    # R363_the_worker_build_writes_the_step_check_rows_with_the_tables_assumed_and_chips of CheckRowTests.cs on the same made up row and list
    import plan_rules
    from test_level_floor_steps import made_build
    built = made_build(monkeypatch, plan_rules.Overrides({"3": {"heightM": {"gapToRuleM": 0.1}}}))
    plan = {"plots": built["plots"], "missing": list(built["missing"].values()), "walls": built["trace"]["walls"], "statusFrom": built["trace"]["statusFrom"]}
    listed = {"overrides": [{"plot": "3", "field": "heightM", "value": {"gapToRuleM": 0.1}, "rule": None, "decision": "parsons ffl", "source": "parsons", "model": "a.rvt",
                             "key": "imc"}]}
    numbers = ["1", "2", "3", "4", "5", "6"]
    made, tables = check_rows.step(numbers, built["rows"], plan, listed, ["parsons"], [], project_profile.load_settings())
    # Plot 3 lacks a yard outline as in the made up build of LevelFloorStepTests.cs
    assert [(item["check"], item["field"], item["from"], item["use"], item["rule"]) for item in made["3"]["rows"]] == [
        ("Floor", "floors.yard", "missing", "Rule", None), ("Level", "heightM", "override", "Past", "parsons ffl")]
    assert made["3"]["rows"][1]["past"]["value"] == {"gapToRuleM": 0.1} and list(made) == numbers
    assert tables["chips"]["All"] == sum(len(item["rows"]) for item in made.values())
    assert tables["assumed"] and all(item["status"] not in ("DOCUMENT", "MEASURED strong") for item in tables["assumed"].values())
