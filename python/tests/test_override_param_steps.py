"""R360 and R361. The build steps overrides and parameters of the parity harness on the made up row of test_level_floor_steps.py built in code
(R10): what the build did with each override of each plot and the values after them, and the link parameters the plan writes, which the C#
of the worker gives the same (OverrideParameterStepTests.cs holds the same made up data)."""
import argparse
import json
from pathlib import Path

import pytest
from shapely.geometry import LineString, Polygon

import common
import plan_from_book
import plan_from_past
import plan_rules
import project_profile
from test_level_floor_steps import DP2_FILE, TH1_FILE, UNITS, VD2_FILE, YARD, made_build, made_rules

UNWRITTEN = "the plot is not written, it lacks "
# The overrides of the made up row, one of each path a build takes, applied or left
MADE = {"2": {"entryM": {"gapToRuleM": 0.05}, "parameter CASE": "Z", "rotationDeg": {"gapToRuleDeg": -179.999}},
        "3": {"heightM": {"gapToRuleM": 0.1}, "parameter RRE_ZZ_SP_Prototype-FFL": {"gapToRuleM": 0.02}},
        "4": {"mirrored": False, "rampType": "flat", "insert": {"gapToRuleM": None}, "parameter RRE_ZZ_SP_RVT-Name": "a made up name"},
        "5": {"insert": {"gapToRuleM": [0.3, -0.2]}, "steps": "4", "genericFloor": "a made up gap", "rampFrontFallM": {"gapToRuleM": 0.1}},
        "6": {"rotationDeg": {"gapToRuleDeg": 1.5}, "rampFrontM": {"gapToRuleM": -0.05}, "yardOutline": {"gapToRuleM": 0.2}, "rampOutline": {"gapToRuleM": 0.1},
              "foo": 1, "mirrored": "yes"}}


def steps_of(built, step=0.5):
    """The steps overrides and parameters of a made build."""
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], step)
    return made, plan_from_book.parameter_step(built["scope"], built["rows"], built["trace"], built["rules"])


def test_r360_parity_overrides_what_the_build_did_with_each_override_applied_or_left_with_why(monkeypatch):
    made, _ = steps_of(made_build(monkeypatch, MADE))
    assert (made["2"]["applied"], made["2"]["notApplied"]) == (["entryM", "parameter CASE", "rotationDeg"], {})
    assert (made["3"]["applied"], made["3"]["notApplied"]) == (["heightM", "parameter RRE_ZZ_SP_Prototype-FFL"], {})
    assert (made["4"]["applied"], made["4"]["notApplied"]) == (["mirrored", "parameter RRE_ZZ_SP_RVT-Name", "rampType"], {"insert": "the override holds no gap to the rule"})
    assert (made["5"]["applied"], made["5"]["notApplied"]) == (["insert", "steps"], {"genericFloor": "an outline gap, a build places the whole face",
                                                                                     "rampFrontFallM": "a ramp front fall is not applied (Q057)"})
    assert made["6"]["applied"] == ["rampFrontM", "rotationDeg"] and made["6"]["notApplied"] == {
        "foo": "a build of parsons2024Rev6 does not apply the field", "mirrored": "the override holds no mirror flag",
        "rampOutline": "an outline gap, a build places the ramp outline of R127", "yardOutline": "an outline gap, a build places the fitted yard outline"}
    # A plot with no override holds none, and plot 7, outside the build with no row, nothing
    assert (made["1"]["applied"], made["1"]["notApplied"], made["1"]["written"]) == (None, None, True)
    assert made["7"] == dict.fromkeys(plan_from_book.OVERRIDE_FIELDS, None) | {"written": False}


def test_r360_the_values_after_the_overrides_are_those_the_plan_writes_by_their_kind(monkeypatch):
    built = made_build(monkeypatch, MADE)
    made, _ = steps_of(built)
    plan = built["plan"]
    entries = plan_from_book.step_entries(project_profile.load_settings(), "overrides", made, {"rotationDeg": plan_from_past.plan_rotation})
    for number in ("1", "2", "3", "4", "5", "6"):
        written, entry = entries[number]["written"], plan[number]
        assert written["insert"] == entry["insert"]["value"][:2] and written["rotationDeg"] == entry["rotationDeg"]["value"]
        assert written["mirrored"] == entry["mirrored"]["value"]
        assert [written[name] for name in ("entry", "ffl", "ssl", "height", "steps")] == [entry["levels"][name]["value"] for name in ("entry", "ffl", "ssl", "height", "steps")]
        assert written["rampZ"] == [corner[2] for corner in entry["floors"]["ramp"]["corners"]["value"]] and written["rampType"] == entry["floors"]["ramp"]["type"]["value"]
        yard = entry["floors"].get("yard")
        assert written["yardOutline"] == (None if yard is None else [loop["points"] for loop in yard["outline"]["value"]["loops"]])
        assert written["yardZ"] == (None if yard is None else yard["z"]["value"])
    # By hand: 2 El @ Entry moved by 5 cm on the FFL of its piece, 3 its FFL by 10 cm, 4 the link of the other hand, 5 the insert along X and Y
    assert entries["2"]["written"]["entry"] == common.fixed(10.2076 + 0.05, 4) and entries["2"]["written"]["ffl"] == 10.528
    assert entries["2"]["written"]["height"] == common.fixed(10.528 - 10.2576, 4)
    assert (entries["3"]["written"]["ffl"], entries["3"]["written"]["ssl"], entries["3"]["written"]["height"]) == (10.628, 10.553, 0.3004)
    # 4 not mirrored takes the link of 2, the DP2 not mirrored two faces to its left, 24 m along X, and the yard outline of DP2 on it
    two = plan["2"]["insert"]["value"]
    assert made["4"]["mirrored"] is False and entries["4"]["written"]["insert"] == [common.r3(two[0] + 24.0), two[1]] and entries["4"]["written"]["rotationDeg"] == 0.0
    assert entries["4"]["written"]["rampType"] == "Flat Ramps"
    assert entries["4"]["written"]["yardOutline"] == [[[common.r3(two[0] + 24.0 + x), common.r3(two[1] + y)] for x, y in YARD["loops"][0]]]
    rule = plan_from_past.point(built["links"]["5"]["insert"])
    assert made["5"]["insert"] == [rule[0] + 0.3, rule[1] - 0.2] and entries["5"]["written"]["steps"] == 4
    # A gap that turns the link past 0 is written by plan_rotation of the rule plus the gap, the raw value left as it is
    assert made["2"]["rotationDeg"] == 0.0 - 179.999 and entries["2"]["written"]["rotationDeg"] == 180.001
    assert made["6"]["rotationDeg"] == 180.0 + 1.5 and entries["6"]["written"]["rampZ"][:2] == [common.r3(10.6 - 0.05), common.r3(10.72 - 0.05)]
    # The step levels of R359 stay those of the rule, the step overrides holds the moved ones
    assert built["levels"]["3"]["ffl"] != made["3"]["ffl"] and common.fixed(built["levels"]["3"]["ffl"], 3) == 10.528


def test_r360_the_raw_values_are_those_just_before_their_last_rounding(monkeypatch):
    built = made_build(monkeypatch, MADE)
    made, _ = steps_of(built)
    trace = built["trace"]
    # No gap: the raw El @ Entry and the FFL of the piece unrounded, a gap: the value as written by the rule plus the gap
    assert made["5"]["entry"] == trace["entryRaw"]["5"] and made["2"]["entry"] == trace["entries"]["2"]["rule"] + 0.05
    assert made["3"]["ffl"] == trace["levelRows"]["3"]["fflRule"] + 0.1 and made["5"]["ffl"] == built["levels"]["5"]["ffl"]
    assert made["5"]["ssl"] == 10.888 - 0.075 and made["2"]["height"] == 10.528 - common.fixed(10.2076 + 0.05, 4)


def test_r360_the_step_refuses_a_value_the_plan_writes_otherwise(monkeypatch):
    built = made_build(monkeypatch, MADE)
    insert = built["trace"]["built"]["5"]["entry"]["insert"]["value"]
    made = list(insert[:2])
    insert[0] = common.r3(insert[0] + 0.001)
    with pytest.raises(ValueError) as error:
        steps_of(built)
    assert str(error.value) == "the step overrides gives plot 5 the insert %r and the plan writes %r (R360)" % (made, insert[:2])


def row_build(monkeypatch, overrides, faceless=(), dry=(), change=None):
    """The made up row with plot 8, a DP2 with no face, and the plots of dry with no road level at a front corner, built with the steps."""
    import test_level_floor_steps

    class Patch:
        def setattr(self, target, name, value):
            if name == "road_level":
                # No road level past the left corner of a plot of dry up to its right corner, so the plot to its left keeps both of its own
                value = (lambda surfaces, point, snap: (None, None) if any(12.0 * (int(plot) - 1) + 1e-9 < point[0] <= 12.0 * int(plot) + 1e-9 for plot in dry)
                         else (10.0 + point[0] / 100.0, "road"))
            monkeypatch.setattr(target, name, value)
    settings = project_profile.load_settings()
    profile = made_rules()
    if change is not None:
        change(profile.values["levelProfiles"]["parsons2024Rev6"]["keyRules"])
    numbers = [str(index) for index in range(1, 7)] + list(faceless)
    rows = {number: {"locationPlanNumber": number, "typologyRev9": UNITS.get(number, "DP2"), "footprintMirrored": "no", "zone": "Zone 2"} for number in numbers}
    faces = {number: Polygon([(index * 12.0, 0.0), (index * 12.0 + 12.0, 0.0), (index * 12.0 + 12.0, 25.0), (index * 12.0, 25.0)]).buffer(0)
             for index, number in enumerate(numbers) if number not in faceless}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)]), LineString([(-3.0, -10.0), (-3.0, 40.0)])]
    sales = {"1": {"Orientation": "WS"}}
    clusters = [{"code": "Duplex_DP2", "plots": ["2", "3"], "source": "footprint"}]
    Patch().setattr(plan_from_book, "road_level", None)
    args = argparse.Namespace(level_profile=None, overrides=None, override_source=None, override_model=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales, clusters)
    rules["overrides"] = overrides
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
            "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((Path(test_level_floor_steps.__file__).resolve().parents[2] / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    files = {"DP2": DP2_FILE, "VD2": VD2_FILE, "TH1-IM": TH1_FILE}
    trace = {}
    plan = plan_from_book.build(numbers, rows, {}, faces, roads, clusters, profile, settings, {"units": {}}, files, "folder", site, [("road", None)], outlines, "N1",
                                sales, True, rules, trace=trace)
    hands = plan_from_book.hand_step(numbers, rows, rules, trace, files, profile, settings.get("majority"))
    return {"plan": {entry["plot"]: entry for entry in plan["plots"]}, "missing": {entry["plot"]: entry for entry in plan["missing"]}, "summary": plan["summary"],
            "profile": profile, "rules": rules, "trace": trace, "hands": hands, "rows": rows, "scope": numbers, "files": files}


def test_r360_a_plot_the_plan_does_not_write_leaves_its_overrides_with_the_keys_it_lacks(monkeypatch):
    lacks = "levels.ffl, parameters.RRE_ZZ_SP_Number-of-Steps, parameters.RRE_ZZ_SP_Prototype-FFL"
    overrides = {"6": {"entryM": {"gapToRuleM": 0.1}, "heightM": {"gapToRuleM": 0.1}, "steps": "2", "mirrored": False, "insert": {"gapToRuleM": [0.1, 0.0]},
                       "parameter CASE": "H", "parameter RRE_ZZ_SP_Prototype-Foundation-Type": "DP2-T1"},
                 "8": {"mirrored": True, "parameter CASE": "H", "insert": {"gapToRuleM": [0.1, 0.0]}}}
    built = row_build(monkeypatch, overrides, faceless=("8",), dry=("6",))
    made, parameters = steps_of(built)
    six = made["6"]
    assert six["written"] is False and six["applied"] == []
    assert six["notApplied"] == {"entryM": "no road level at a front corner, so no El @ Entry to move", "heightM": "no FFL, no road level at a front corner",
                                 "insert": UNWRITTEN + lacks, "mirrored": UNWRITTEN + lacks, "parameter CASE": UNWRITTEN + lacks,
                                 "parameter RRE_ZZ_SP_Prototype-Foundation-Type": UNWRITTEN + lacks, "steps": "no FFL, no road level at a front corner"}
    # The link of the plot is held with its hand and insert after the overrides, its levels and floors are none
    assert six["mirrored"] is False and six["insert"] is not None and (six["entry"], six["ffl"], six["rampZ"], six["yardOutline"]) == (None, None, None, None)
    eight = made["8"]
    no_face = "transform, levels.ffl, parameters.CASE, parameters.RRE_ZZ_SP_Number-of-Steps, parameters.RRE_ZZ_SP_Prototype-FFL, parameters.RRE_ZZ_SP_Prototype-Foundation-Type"
    assert eight["notApplied"] == {"insert": "the face has no front frame", "mirrored": UNWRITTEN + no_face, "parameter CASE": UNWRITTEN + no_face}
    assert (eight["mirrored"], eight["insert"], eight["rotationDeg"]) == (None, None, None)
    # The build summary says the same by field and why
    assert built["summary"]["rules"]["overridesNotAppliedWhy"]["mirrored"] == {UNWRITTEN + lacks: {"plots": 1, "first": ["6"]}, UNWRITTEN + no_face: {"plots": 1, "first": ["8"]}}
    # R361: the parameters of a plot not written are those the build gave it, mirrored by its override as the link it holds, the lacks by name
    assert parameters["6"]["written"] is False and parameters["6"]["parameters"]["CASE"] == "H" and parameters["6"]["sources"]["CASE"] == "override"
    assert parameters["6"]["lacks"] == ["RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_Prototype-FFL"]
    assert parameters["8"]["lacks"] == ["CASE", "RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_Prototype-FFL", "RRE_ZZ_SP_Prototype-Foundation-Type"]
    assert parameters["8"]["parameters"]["RRE_ZZ_SP_RVT-Name"] == "8 | DP2 | None | XX | XX"


def test_r360_r386_a_skip_plot_and_an_entry_marked_use_rule_are_left_with_why(monkeypatch):
    overrides = plan_rules.Overrides({"1": {"mirrored": False}, "5": {"entryM": {"gapToRuleM": 0.1}}}, {"1": "a made up reason"}, {"5": {"heightM": plan_rules.USE_RULE}})
    built = made_build(monkeypatch, overrides)
    made, parameters = steps_of(built)
    assert made["1"]["skip"] == "a made up reason" and made["1"]["written"] is False and made["1"]["notApplied"] == {"mirrored": UNWRITTEN + "skip"}
    assert (made["1"]["insert"], made["1"]["entry"], made["1"]["mirrored"]) == (None, None, None) and parameters["1"]["parameters"] is None
    assert made["5"]["applied"] == ["entryM"] and made["5"]["notApplied"] == {"heightM": "use rule, the override list marks the entry to follow the rule (R386)"}
    assert made["5"]["ffl"] == built["levels"]["5"]["ffl"] and "1" not in built["plan"]


def test_r361_parity_parameters_the_text_of_each_link_parameter_the_plan_writes_with_its_source(monkeypatch):
    built = made_build(monkeypatch, MADE)
    _, made = steps_of(built)
    for number in ("1", "2", "3", "4", "5", "6"):
        entry = built["plan"][number]
        assert made[number]["parameters"] == {name: item["value"] for name, item in entry["parameters"].items()}
        assert made[number]["sources"] == {name: item["source"] for name, item in entry["parameters"].items()}
        assert made[number]["written"] is True and made[number]["linkName"] == entry["parameters"]["RRE_ZZ_SP_RVT-Name"]["value"]
        assert made[number]["lacks"] == (["RRE_ZZ_SP_Number-of-Steps"] if number == "5" else [])
    # 2 its CASE by the text of its override, 3 its Prototype-FFL from the FFL moved by 10 cm, down to the cm, then its gap of 2 cm
    assert (made["2"]["parameters"]["CASE"], made["2"]["sources"]["CASE"]) == ("Z", "override")
    assert made["3"]["parameters"]["RRE_ZZ_SP_Prototype-FFL"] == "10.640" and made["3"]["sources"]["RRE_ZZ_SP_Prototype-FFL"] == "override"
    # 4 not mirrored by its override takes the CASE of DP2 not mirrored, 5 its steps of the override in Number-of-Steps and the link Name
    assert made["4"]["parameters"]["CASE"] == made["5"]["parameters"]["CASE"] and made["4"]["sources"]["CASE"] == "keyRules"
    assert (made["4"]["parameters"]["RRE_ZZ_SP_RVT-Name"], made["4"]["sources"]["RRE_ZZ_SP_RVT-Name"]) == ("a made up name", "override")
    # keyRules.steps of Number-of-Steps holds no 4, so the plot lacks it and is written all the same, as LINK_KEYS name no parameter
    assert made["5"]["parameters"]["RRE_ZZ_SP_RVT-Name"] == "5 | DP2 | 4 | XX | XX" and "RRE_ZZ_SP_Number-of-Steps" not in made["5"]["parameters"]
    assert made["7"] == dict.fromkeys(plan_from_book.PARAMETER_FIELDS, None) | {"written": False}


def test_r361_r107_q099_a_build_writes_the_link_name_as_the_rvt_name_parameter_after_its_overrides_with_its_source(monkeypatch):
    built = made_build(monkeypatch, MADE)
    _, made = steps_of(built)
    plan = built["plan"]
    # 1 the formula of keyRules, plot, code, steps and the facade, 4 the text of its override of the parameter
    # The status of R401 stands beside value and source
    assert (plan["1"]["linkName"]["value"], plan["1"]["linkName"]["source"]) == ("1 | VD2 | 1 | XX | XX", "keyRules") and made["1"]["linkName"] == "1 | VD2 | 1 | XX | XX"
    assert (plan["4"]["linkName"]["value"], plan["4"]["linkName"]["source"]) == ("a made up name", "override") and made["4"]["linkName"] == "a made up name"
    assert plan["5"]["linkName"]["value"] == "5 | DP2 | 4 | XX | XX"
    for number in ("1", "2", "3", "4", "5", "6"):
        name, parameter = plan[number]["linkName"], plan[number]["parameters"][plan_from_past.RVT_NAME_PARAMETER]
        assert (name["value"], name["source"], name["status"]) == (parameter["value"], parameter["source"], parameter["status"])
    # A plot the plan does not write holds the name of its parameter in the step, and 7 with no row none
    built = row_build(monkeypatch, {}, faceless=("8",))
    _, made = steps_of(built)
    assert made["8"]["written"] is False and made["8"]["linkName"] == "8 | DP2 | None | XX | XX"


def test_r361_r107_q099_a_rule_that_gives_no_rvt_name_leaves_the_link_name_out_and_the_plot_lacks_the_parameter(monkeypatch):
    def no_name(key):
        del key["parameter RRE_ZZ_SP_RVT-Name"]
    built = row_build(monkeypatch, {}, change=no_name)
    _, made = steps_of(built)
    assert "linkName" not in built["plan"]["1"] and made["1"]["linkName"] is None and made["1"]["written"] is True


def test_r360_r361_the_config_names_the_fields_of_the_steps_overrides_and_parameters_by_their_kind():
    steps = project_profile.load_settings().get("addin")["parity"]["steps"]
    assert {name: (field["kind"], field.get("decimals")) for name, field in steps["overrides"].items()} == {
        "unit": ("noGeometry", None), "skip": ("noGeometry", None), "written": ("noGeometry", None), "applied": ("noGeometry", None), "notApplied": ("noGeometry", None),
        "mirrored": ("noGeometry", None), "insert": ("points", 3), "rotationDeg": ("angles", 3), "entry": ("levels", 4), "ffl": ("levels", 3), "ssl": ("levels", 3),
        "height": ("levels", 4), "steps": ("noGeometry", None), "rampZ": ("levels", 3), "rampType": ("noGeometry", None), "yardZ": ("levels", 3),
        "yardOutline": ("points", 3)}
    assert tuple(steps["overrides"]) == plan_from_book.OVERRIDE_FIELDS and tuple(steps["parameters"]) == plan_from_book.PARAMETER_FIELDS
    assert all(field["kind"] == "noGeometry" for field in steps["parameters"].values())
    assert plan_from_book.BUILD_STEPS[-3:-1] == ("overrides", "parameters") and plan_from_book.PARITY_STEPS[-3:-1] == ("overrides", "parameters")
    assert {"overrides", "parameters"} <= set(plan_from_book.PROTOTYPE_STEPS)


def test_r360_r361_a_plot_its_hand_rule_gives_no_mirror_flag_holds_its_levels_and_lacks_the_parameters_of_the_hand(monkeypatch):
    def no_pair_right(key):
        key["hand alone"]["byFile"][DP2_FILE].pop("pair right")
    built = row_build(monkeypatch, {"6": {"heightM": {"gapToRuleM": 0.1}}}, change=no_pair_right)
    made, parameters = steps_of(built)
    six = made["6"]
    lacks = "mirrored, parameters.CASE, parameters.RRE_ZZ_SP_Prototype-Foundation-Type"
    assert built["hands"]["6"]["mirrored"] is None and six["written"] is False and six["notApplied"] == {"heightM": UNWRITTEN + lacks}
    # The FFL moved by its gap all the same, as the level chain of the build runs before the link, and no link with no hand
    assert common.fixed(six["ffl"], 3) == common.fixed(built["trace"]["levelRows"]["6"]["fflRule"] + 0.1, 3) and (six["mirrored"], six["insert"]) == (None, None)
    assert parameters["6"]["lacks"] == ["CASE", "RRE_ZZ_SP_Prototype-Foundation-Type"] and parameters["6"]["written"] is False


def entry(plot, field, value, key="imc", model="a.rvt", **more):
    """An entry of the override list in the form of R330, the job of its key as source."""
    return {"plot": plot, "field": field, "value": value, "source": {"u11": "native"}.get(key, "parsons"), "model": model, "key": key, **more}


def load(tmp_path, data):
    path = tmp_path / "overrides.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    settings = project_profile.load_settings()
    lp = made_rules().get("levelProfiles")["parsons2024Rev6"]
    return path, lambda: plan_rules.load_overrides(path, set(plan_rules_sources(settings)), None, settings.get("majority")["groups"], "parsons2024Rev6", lp)


def plan_rules_sources(settings):
    import override_source
    return override_source.default_sources(settings)


def test_r360_r386_the_override_list_a_build_loads_with_the_lines_of_the_csharp(tmp_path):
    # Of the job of the profile only, an entry marked use rule left with why, the plots of skip by plot
    _, loaded = load(tmp_path, {"overrides": [entry("5", "entryM", {"gapToRuleM": 0.1}), entry("6", "heightM", {"gapToRuleM": 0.2}, use="rule", byHand=True),
                                             entry("5", "mirrored", True, key="u11")], "skip": [{"plot": "1", "reason": "a made up reason", "byHand": True}]})
    found = loaded()
    assert dict(found) == {"5": {"entryM": {"gapToRuleM": 0.1}}} and found.ruled == {"6": {"heightM": plan_rules.USE_RULE}} and found.skip == {"1": "a made up reason"}
    lines = [({"overrides": [entry("2", "entryM", {"gapToRuleM": 0.1}, key="zz"), entry("3", "heightM", {"gapToRuleM": 0.1}, use="rule")]},
              "2 overrides refused: override of plot 2 field entryM names key zz, which no group of majority.groups holds (R330) | override of plot 3 field heightM has "
              "use rule with no \"byHand\": true, a use is a hand edit a majority run must keep (R386)"),
             ({"overrides": [entry("5", "entryM", {"gapToRuleM": 0.1})], "skip": [{"plot": "1", "reason": ""}]},
              "1 skip entries refused: {\"plot\": \"1\", \"reason\": \"\"} is not {\"plot\": text, \"reason\": text, \"byHand\": true} (R386)"),
             ({"overrides": [entry("5", "entryM", {"gapToRuleM": 0.1}), entry("5", "entryM", {"gapToRuleM": 0.2}, key="imd", model="b.rvt")]},
              "1 plots hold two overrides of one field: plot 5 field entryM of imc a.rvt and imd b.rvt. Narrow the list with --override-model or edit it by hand (R280)"),
             ({"overrides": [entry("5", "steps", "2.5")]}, "1 steps overrides hold no whole number: plot 5 value \"2.5\" of a.rvt. Give the step count as a whole number (R280, R340)"),
             ({"overrides": [entry("5", "mirrored", True, key="u11")]}, "the override list holds no override of parsons among the sources parsons (R280, R330)")]
    for data, line in lines:
        path, loaded = load(tmp_path, data)
        with pytest.raises(project_profile.ProfileError) as error:
            loaded()
        # The faults of an entry and of the skip list name the list first, as check_entries and check_skip do
        assert str(error.value) == (str(path) + ": " + line if "refused" in line else line)
    import override_source
    with pytest.raises(project_profile.ProfileError) as error:
        override_source.skip_plots_known({"02": "a made up reason"}, {"2": {}}, "the skip list of the override list: ")
    assert str(error.value) == "the skip list of the override list: 1 skip plots are no plot of plots.csv, a skip names the plot as plots.csv spells it: \"02\" (R386)"
