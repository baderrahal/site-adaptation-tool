"""Round ADDIN PR 3a, last pass on PR 47: the steps of the level profile (R337), the overrides a build applies and leaves plot by
plot (R337, R280), the rules a build carries out (R337), the hand edits of Bader (R330, R336) and the keyRules block (R338).
Every name and number here is made up (R10)."""
import plan_rules
from test_addin_review import by_plot, key_build, made_key_rules


# R337 the steps come from the step bands of the level profile until 3c codes them, and say so
def test_r337_the_steps_parameters_are_labelled_by_the_step_bands_of_the_level_profile_until_3c_codes_the_steps(tmp_path, monkeypatch):
    plan, rules, _ = key_build(tmp_path, monkeypatch)
    found = by_plot(plan)["1"]["parameters"]
    assert found["RRE_ZZ_SP_Number-of-Steps"]["source"] == "levelProfileStepBands"
    assert found["RRE_ZZ_SP_RVT-Name"]["source"] == "levelProfileStepBands"
    assert found["CASE"]["source"] == "keyRules" and found["RRE_ZZ_SP_Plot-Number"]["source"] == "keyRules"
    assert plan_rules.step_parameters(made_key_rules(), set()) == {"RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name"}
    assert plan_rules.step_parameters(made_key_rules(), {"parameter RRE_ZZ_SP_RVT-Name"}) == {"RRE_ZZ_SP_Number-of-Steps"}
    steps = plan["summary"]["rules"]["steps"]
    assert steps == {"from": "stepBands of the level profile parsons2024Rev6", "parameters": ["RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name"]}


# R337, R280 what a build applies and leaves of the overrides, plot by plot, and only what it applied counted as applied
def test_r337_a_build_says_plot_by_plot_which_overrides_it_applied_and_which_it_left_and_why(tmp_path, monkeypatch):
    import plan_from_book
    from test_addin_review import entry
    gap = lambda value: {"gapToRuleM": value}
    overrides = [entry("2", field, value, "parsons", "IMC.rvt", "imc") for field, value in (
        ("entryM", gap(0.02)), ("heightM", gap(0.03)), ("rampFrontM", gap(0.01)), ("rampType", "2"), ("mirrored", False), ("rampFrontFallM", gap(0.04)),
        ("yardOutline", gap(0.3)), ("parameter RRE_ZZ_SP_Prototype-FFL", "10.000"))]
    overrides += [entry("4", field, value, "parsons", "IMC.rvt", "imc") for field, value in (
        ("entryM", gap(0.05)), ("heightM", gap(0.03)), ("mirrored", False), ("rampFrontM", gap(0.01)), ("rampType", "2"))]
    # Plot 4, from 36 m on, has no road level, so no El @ Entry, no FFL and it is not written
    road = lambda surfaces, point, snap: (None, None) if point[0] > 36.0 - 1e-6 else (10.0, "road")
    plan, rules, _ = key_build(tmp_path, monkeypatch, overrides=overrides, road=road)
    written = by_plot(plan)
    assert "4" not in written
    two = written["2"]["overrides"]
    assert two["applied"] == ["entryM", "heightM", "mirrored", "rampFrontM", "rampType"]
    assert set(two["notApplied"]) == {"rampFrontFallM", "yardOutline", "parameter RRE_ZZ_SP_Prototype-FFL"}
    assert "3c" in two["notApplied"]["parameter RRE_ZZ_SP_Prototype-FFL"] and "Q057" in two["notApplied"]["rampFrontFallM"]
    four = next(item for item in plan["missing"] if item["plot"] == "4")
    assert four["written"] is False and four["overrides"]["applied"] == []
    assert "no road level" in four["overrides"]["notApplied"]["entryM"] and "not written" in four["overrides"]["notApplied"]["mirrored"]
    assert set(four["overrides"]["notApplied"]) == {"entryM", "heightM", "mirrored", "rampFrontM", "rampType"}
    summary = plan["summary"]["rules"]
    # Only what a written plot took is applied, plot 4 is counted as left
    assert summary["applied"] == {"entryM": 1, "heightM": 1, "mirrored": 1, "rampFrontM": 1, "rampType": 1}
    assert summary["overridesNotApplied"] == {"entryM": 1, "heightM": 1, "mirrored": 1, "parameter RRE_ZZ_SP_Prototype-FFL": 1, "rampFrontFallM": 1,
                                              "rampFrontM": 1, "rampType": 1, "yardOutline": 1}
    # The fields left to 3c in the list are said apart from what the build applied of them
    lines = "\n".join(plan_from_book.summary_lines(plan["summary"], rules["name"], False))
    assert "applied entryM 1, heightM 1" in lines and "left to phase" not in lines
    assert "parameter RRE_ZZ_SP_Prototype-FFL 1" in lines


# R337 a coded rule the build does not carry out is refused, and El @ Entry and the ramp class take one slope
def test_r337_a_link_rotation_by_the_edge_nearest_a_curb_is_refused_since_a_build_takes_the_corner_front_of_r226():
    import pytest
    import project_profile
    rules = made_key_rules()
    plan_rules.check_key_rules(rules)
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.check_key_rules(dict(rules, linkRotation={"rule": "nearestCurb"}))
    assert "linkRotation" in str(error.value) and "frontRule" in str(error.value)


def test_r337_the_slope_of_el_at_entry_and_the_ramp_class_of_key_rules_must_agree_for_every_code_else_the_build_is_refused():
    import pytest
    import project_profile
    lp = {"rampSlopePercent": {"default": 0.5, "VD2": 2.0}, "keyRules": made_key_rules()}
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.key_rules_of(lp)
    assert "VD2" in str(error.value) and "rampSlopePercent" in str(error.value)
    lp["rampSlopePercent"] = {"default": 0.5}
    assert plan_rules.key_rules_of(lp) is lp["keyRules"]
    # The committed profile agrees
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    assert plan_rules.key_rules_of(plan_rules.level_profile(real, "parsons2024Rev6")) is not None
