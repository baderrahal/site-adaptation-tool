"""Round ADDIN PR 3a, last pass on PR 47: the proof of a build by the rules of the keys (R337), the plan plan_from_book.py builds
for the plots of each key held field by field against that key. Every name and number here is made up (R10)."""
import json

import pytest

import build_proof
import majority
import project_profile

ARC = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt"
STR = "RRE-WP-NSP-KEO-STR-DP2-ZZ-MOD-2200.rvt"
FACE = [[0.0, 0.0], [12.0, 0.0], [12.0, 25.0], [0.0, 25.0]]
YARD = [[2.0, 18.0], [10.0, 18.0], [10.0, 24.0], [2.0, 24.0]]
RAMP = [[1.0, 0.0, 9.9], [5.0, 0.0, 9.9], [5.0, 3.0, 10.0], [1.0, 3.0, 10.0]]


def made_key_rules():
    return {"parameter RRE_ZZ_SP_Plot-Number": {"rule": "plot"}, "parameter RRE_ZZ_SP_Number-of-Steps": {"rule": "steps", "table": {"1": "1", "2": "2"}},
            "parameter RRE_ZZ_SP_Façade-Color": {"rule": "all", "table": {"all": "XX"}},
            "parameter RRE_ZZ_SP_Façade-Design-Option": {"rule": "all", "table": {"all": "XX"}},
            "parameter RRE_ZZ_SP_RVT-Name": {"rule": "plotCodeStepsAndFacade"}}


def built(plot, mirrored=False, steps=1, file=ARC, name=None, overrides=None):
    """A plot entry of a plan as plan_from_book.py writes it, at x 12 times its number."""
    shift = 12.0 * (int(plot) - 1)
    move = lambda points: [[point[0] + shift] + list(point[1:]) for point in points]
    entry = {"plot": plot, "unit": {"value": "DP2"}, "prototypeFile": {"value": file}, "insert": {"value": [6.0 + shift, 1.0, 10.0]},
             "rotationDeg": {"value": 0.0}, "mirrored": {"value": mirrored}, "levels": {"ffl": {"value": 10.0}, "steps": {"value": steps}},
             "parameters": {"RRE_ZZ_SP_Plot-Number": {"value": plot}, "RRE_ZZ_SP_Number-of-Steps": {"value": str(steps)},
                            "RRE_ZZ_SP_RVT-Name": {"value": name or "%s | DP2 | %d | XX | XX" % (plot, steps)}},
             "floors": {"generic": {"outline": {"value": {"loops": [{"points": move(FACE)}]}}, "z": {"value": 10.0}},
                        "yard": {"type": {"value": "yard"}, "outline": {"value": {"loops": [{"points": move(YARD)}]}}, "z": {"value": 9.9}},
                        "ramp": {"type": {"value": "Ramps 0.5%"}, "corners": {"value": move(RAMP)}}}}
    if overrides is not None:
        entry["overrides"] = overrides
    return entry


def key_row(plot, key="imx", kind="TU", mirrored=False, steps=1, file=ARC, name=None):
    """The past_all.csv row of the key on the same plot, typed as past_all.read_table gives it."""
    shift = 12.0 * (int(plot) - 1)
    ring = ", ".join("%g %g" % (x + shift, y) for x, y in FACE + FACE[:1])
    return {"key": key, "source": "parsons", "kind": kind, "plot": plot, "typologyRev9": "DP2", "linkFile": file, "x": 6.0 + shift, "y": 1.0, "rotationDeg": 0.0,
            "mirrored": mirrored, "rampType": "Ramps 0.5%", "rampSlopePercent": 0.5, "rampCorners": json.dumps([[x + shift, y, z] for x, y, z in RAMP]),
            "genericOutline": "POLYGON ((%s))" % ring, "genericTop": 10.0, "yardTop": 9.9, "steps": str(steps),
            "parameters": json.dumps({"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Number-of-Steps": str(steps),
                                      "RRE_ZZ_SP_RVT-Name": name or "%s | DP2 | %d | XX | XX" % (plot, steps)})}


def key_entry(plot, mirrored=False):
    """The key plan entry of R325 with its yard in plan, for the yard outline in link axes."""
    shift = 12.0 * (int(plot) - 1)
    return {"plot": plot, "insert": {"value": [6.0 + shift, 1.0, 10.0]}, "rotationDeg": {"value": 0.0}, "mirrored": {"value": mirrored},
            "floors": {"yard": {"outline": {"value": {"loops": [{"points": [[x + shift, y] for x, y in YARD]}]}}}}}


def context(overrides=(), questions=None):
    block = majority.load_block(project_profile.load_settings())
    block = dict(block, kindDiscipline={"SE": "STR"}, decidedByQuestion=questions or {})
    return build_proof.proof_context(block, {"prefix": "RRE-WP-NSP-KEO-", "discipline": "ARC"}, made_key_rules(), overrides,
                                     ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"])


def states(verdicts):
    return {(item["plot"], item["field"]): item["state"] for item in verdicts}


# R337 the plots of each key, built as any neighbourhood is built
def test_r337_a_cover_per_key_holds_the_plots_of_that_key_in_plot_order_with_its_neighbourhood():
    rows = [{"key": "imx", "source": "parsons", "plot": "12", "nbh": "N1"}, {"key": "imx", "source": "parsons", "plot": "3", "nbh": "N1"},
            {"key": "imy", "source": "parsons", "plot": "7", "nbh": "N2"}, {"key": "u1", "source": "native", "plot": "7", "nbh": "N2"}]
    assert build_proof.covers(rows, "parsons") == {"imx": {"neighbourhood": "N1", "plots": ["3", "12"]}, "imy": {"neighbourhood": "N2", "plots": ["7"]}}
    with pytest.raises(project_profile.ProfileError):
        build_proof.covers(rows + [{"key": "imx", "source": "parsons", "plot": "4", "nbh": "N3"}], "parsons")


# R337 every field of a built plot against the key, holds or explained or unexplained
def test_r337_the_proof_holds_every_field_of_a_built_plot_against_the_key_and_counts_a_difference_with_no_reason_unexplained():
    plan = {"plots": [built("1"), built("2", mirrored=True)], "missing": []}
    rows = [key_row("1"), key_row("2")]
    found = states(build_proof.compare("imx", plan, rows, {"plots": [key_entry("1"), key_entry("2")]}, context()))
    assert {field for plot, field in found if plot == "1"} >= {"prototypeFile", "mirrored", "rotationDeg", "insert", "rampType", "rampOutline", "genericFloor", "yard",
                                                              "yardOutline", "parameter RRE_ZZ_SP_Plot-Number", "parameter RRE_ZZ_SP_RVT-Name"}
    # The made up key holds no level and the proof was given no key read, so its levels are counted as such, never as holding (R340)
    assert all(state == "holds" for (plot, _), state in found.items() if plot == "1" and state not in ("keyHoldsNone", "notChecked"))
    assert found[("1", "ffl")] == "keyHoldsNone" and found[("1", "parameter RRE_ZZ_SP_Prototype-FFL")] == "notChecked"
    # Plot 2 is built the other hand with no override, so its hand is unexplained, its other fields held apart
    assert found[("2", "mirrored")] == "unexplained" and found[("2", "insert")] == "holds"


def test_r337_an_override_the_build_did_not_place_explains_its_field_and_one_it_placed_that_still_differs_does_not():
    overrides = {("imx", "1", "mirrored"): {"value": False}, ("imx", "2", "mirrored"): {"value": True}}
    plan = {"plots": [built("1", mirrored=True, overrides={"applied": [], "notApplied": {"mirrored": "made up"}}),
                      built("2", mirrored=True, overrides={"applied": ["mirrored"], "notApplied": {}})], "missing": []}
    found = states(build_proof.compare("imx", plan, [key_row("1"), key_row("2", mirrored=False)], {"plots": []}, context(overrides)))
    assert found[("1", "mirrored")] == "override" and found[("2", "mirrored")] == "unexplained"


def test_r337_a_key_of_a_kind_that_links_another_discipline_and_a_plot_an_open_question_decides_are_explained_with_their_reason():
    questions = {"Q017": {"fields": ["prototypeFile", "mirrored"], "plots": {"imx": ["2"]}, "why": "made up"}}
    plan = {"plots": [built("1"), built("2", mirrored=True)], "missing": []}
    rows = [key_row("1", kind="SE", file=STR), key_row("2", file="RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt")]
    verdicts = build_proof.compare("imx", plan, rows, {"plots": []}, context(questions=questions))
    found = states(verdicts)
    assert found[("1", "prototypeFile")] == "kind" and found[("2", "prototypeFile")] == "question" and found[("2", "mirrored")] == "question"
    assert next(item for item in verdicts if item["plot"] == "2" and item["field"] == "mirrored")["why"].startswith("Q017")
    # The SE kind leaves the yard and the RVT-Name out, majority.kindLeaveOut
    assert ("1", "yard") not in found and ("1", "parameter RRE_ZZ_SP_RVT-Name") not in found


def test_r337_a_parameter_that_differs_only_by_the_steps_of_the_level_profile_is_left_to_3c_and_one_that_differs_otherwise_is_not():
    plan = {"plots": [built("1", steps=2), built("2", steps=2, name="2 | DP4 | 2 | XX | XX")], "missing": []}
    found = states(build_proof.compare("imx", plan, [key_row("1", steps=1), key_row("2", steps=1)], {"plots": []}, context()))
    assert found[("1", "parameter RRE_ZZ_SP_Number-of-Steps")] == "laterPhase" and found[("1", "parameter RRE_ZZ_SP_RVT-Name")] == "laterPhase"
    assert found[("2", "parameter RRE_ZZ_SP_RVT-Name")] == "unexplained"


def test_r337_a_plot_the_build_does_not_write_is_explained_only_by_its_levels_or_a_link_whose_overrides_have_no_gap():
    overrides = {("imx", "3", "insert"): {"value": None}, ("imx", "3", "rotationDeg"): {"value": None}}
    plan = {"plots": [], "missing": [
        {"plot": "1", "written": False, "missing": [{"key": "levels.ffl", "why": "levels.py gives no FFL"}, {"key": "parameters.RRE_ZZ_SP_Number-of-Steps", "why": "w"}]},
        {"plot": "2", "written": False, "missing": [{"key": "mirrored", "why": "no rule gives the mirror flag"}]},
        {"plot": "3", "written": False, "missing": [{"key": "transform", "why": "the face has no front edge"}, {"key": "levels.ffl", "why": "w"}]}]}
    rows = [key_row("1"), key_row("2"), key_row("3"), key_row("4")]
    found = states(build_proof.compare("imx", plan, rows, {"plots": []}, context(overrides)))
    assert found[("1", "written")] == "laterPhase" and found[("2", "written")] == "unexplained" and found[("3", "written")] == "override"
    # A key plot the build neither writes nor lists is unexplained
    assert found[("4", "written")] == "unexplained"


def test_r337_the_tally_counts_each_state_per_key_and_field_and_lists_every_unexplained_difference():
    verdicts = [{"key": "imx", "plot": "1", "field": "mirrored", "state": "holds", "why": ""},
                {"key": "imx", "plot": "2", "field": "mirrored", "state": "unexplained", "why": "built True, key False"},
                {"key": "imy", "plot": "5", "field": "insert", "state": "override", "why": "w"}]
    found = build_proof.tally(verdicts)
    assert found["byKey"]["imx"]["mirrored"] == {"holds": 1, "unexplained": 1}
    assert found["unexplainedByKey"] == {"imx": 1, "imy": 0}
    assert found["unexplained"] == [verdicts[1]]


def test_r337_the_fields_that_follow_from_a_hand_a_question_decides_or_from_a_plot_with_no_link_take_its_state():
    questions = {"Q017": {"fields": ["prototypeFile", "mirrored"], "plots": {"imx": ["2"]}, "why": "made up"}}
    rules = dict(made_key_rules(), **{"parameter CASE": {"rule": "typologyMirrored", "table": {"DP2 False": "F", "DP2 True": "E"}}})
    turned = built("2", mirrored=True)
    turned["rotationDeg"]["value"] = 180.0
    turned["parameters"]["CASE"] = {"value": "E"}
    row = key_row("2", file="RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt")
    row["parameters"] = json.dumps(dict(json.loads(row["parameters"]), CASE="F"))
    ctx = context(questions=questions)
    ctx["keyRules"] = rules
    ctx["handed"] = build_proof.hand_parameters(rules, ctx["later"])
    found = states(build_proof.compare("imx", {"plots": [turned], "missing": []}, [row], {"plots": [key_entry("2")]}, ctx))
    assert found[("2", "mirrored")] == "question" and found[("2", "rotationDeg")] == "question" and found[("2", "parameter CASE")] == "question"
    # The made up yard is the same shape in the link axes of either hand, so it holds
    assert found[("2", "yardOutline")] == "holds"
    # A plot with no link lacks the parameters of its hand as well, they follow the link and its overrides
    overrides = {("imx", "3", "insert"): {"value": None}, ("imx", "3", "rotationDeg"): {"value": None}}
    plan = {"plots": [], "missing": [{"plot": "3", "written": False, "missing": [{"key": "transform", "why": "the face has no front edge"},
                                                                                 {"key": "parameters.CASE", "why": "the rule of keyRules gives no value for DP2"}]}]}
    ctx = context(overrides)
    ctx["handed"] = build_proof.hand_parameters(rules, ctx["later"])
    assert states(build_proof.compare("imx", plan, [key_row("3")], {"plots": []}, ctx))[("3", "written")] == "override"
