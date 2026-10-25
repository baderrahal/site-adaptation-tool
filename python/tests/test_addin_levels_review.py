"""Round ADDIN PR 3c, review of PR 48: each level of a plot stands alone (R339 to R341), a miss with no value is listed and never
written as an override (R272, R280, R330), the proof needs every input and says what holds by the rule alone (R340), the second
method counts an item one side holds as a mismatch (R341) and the split per unit sequence (R338, R339). Every name and number here
is made up (R10)."""
from test_addin_levels import LEVEL_RULES, level_build, level_of, level_run


# R340 an override moves the level of its own plot only
def test_r340_an_override_moves_the_level_of_its_own_plot_only_so_deleting_one_el_at_entry_line_of_a_piece_keeps_every_other_level(tmp_path, monkeypatch):
    from test_addin_review import entry
    # Plots 3 and 4 are a row pair and share one FFL, 4 sets it. The key holds El @ Entry of 4 at 1.3 mm over the rule and the FFL of
    # the pair at 1 mm over the rule, so the list holds the entryM of 4 and the heightM of 3 and of 4
    full = [entry("4", "entryM", {"gapToRuleM": 0.0013}, "parsons", "IMC.rvt", "imc"), entry("3", "heightM", {"gapToRuleM": 0.001}, "parsons", "IMC.rvt", "imc"),
            entry("4", "heightM", {"gapToRuleM": 0.001}, "parsons", "IMC.rvt", "imc")]
    found, _ = level_build(tmp_path, monkeypatch, overrides=full)
    assert level_of(found["3"], "ffl") == level_of(found["4"], "ffl") == 10.264 and level_of(found["4"], "entry") == 10.0639
    # The entryM line of 4 deleted by hand: El @ Entry of 4 goes back to the rule, every FFL stays on its key
    cut, _ = level_build(tmp_path, monkeypatch, overrides=full[1:])
    assert level_of(cut["3"], "ffl") == level_of(cut["4"], "ffl") == 10.264 and level_of(cut["4"], "entry") == 10.0626
    assert level_of(cut["3"], "entry") == level_of(found["3"], "entry") == 10.0526


# R339 the FFL is judged on the El @ Entry of the rule, so a plot whose FFL another plot's El @ Entry sets gets its own override
def test_r339_the_ffl_is_judged_on_the_el_at_entry_of_the_rule_so_each_plot_of_a_piece_off_its_key_gets_its_own_height_override():
    results, misses, _, _ = level_run()
    ffl = next(result for result in results if result["decision"] == "parsons ffl")
    # Plot 3 sets the FFL of the piece 3 and 4 by its El @ Entry 2.4 mm over the rule, so on the El @ Entry of the rule both are 2 mm under
    # the key, and plots 1 and 2 round to the next mm, four misses with a gap of their own
    found = sorted((miss["plot"], round(miss["value"] - miss["rule"], 4)) for miss in misses if miss["field"] == "heightM")
    assert found == [("1", -0.001), ("2", -0.001), ("3", 0.002), ("4", 0.002)]
    assert ffl["byBuild"] == {"holds": 3, "breaks": 4, "cannot": 0, "share": round(3 / 7, 4)}


# R272, R330 a miss with no value is listed in cannot, never written as an override with a null value
def test_r272_a_miss_with_no_value_is_listed_in_cannot_and_never_written_as_an_override(tmp_path):
    import json
    import majority
    from test_addin_review import entry, keep_block
    block = {"overrideDecisions": ["parameter", "entry"], "groups": {"native": ["u11"], "parsons": ["imc"]}}
    misses = [{"decision": "parsons parameter RRE_ZZ_SP_Prototype-FFL", "field": "parameter RRE_ZZ_SP_Prototype-FFL", "plot": "2", "key": "imc", "code": "DP1",
               "value": 10.12, "rule": None, "state": "cannot", "reason": "no value of fflDownToTheCm", "overrideValue": None},
              {"decision": "parsons entry", "field": "entryM", "plot": "3", "key": "imc", "code": "DP1", "value": 10.13, "rule": 10.1276, "state": "breaks",
               "reason": "breaks roadLevel"}]
    written, cannot = majority.split_cannot(majority.overrides(misses, block, {"imc": "IMC.rvt"}))
    assert [(item["plot"], item["field"]) for item in written] == [("3", "entryM")] and all(item["value"] is not None for item in written)
    assert cannot == [{"plot": "2", "field": "parameter RRE_ZZ_SP_Prototype-FFL", "source": "parsons", "model": "IMC.rvt", "key": "imc",
                       "decision": "parsons parameter RRE_ZZ_SP_Prototype-FFL", "reason": "no value of fflDownToTheCm"}]
    # The cannot list of a run replaces the one of the earlier list for what the run writes, the cannot entries of a kept source stay
    path = tmp_path / "list.json"
    kept = {"plot": "1", "field": "entryM", "source": "native", "model": "U11.rvt", "key": "u11", "decision": "native entry", "reason": "r"}
    stale = dict(cannot[0], plot="9")
    path.write_text(json.dumps({"note": "n", "overrides": [entry("1", "mirrored", True, "native", "U11.rvt", "u11")], "cannot": [kept, stale]}), encoding="utf-8")
    keep = dict(keep_block(), groups=block["groups"])
    assert majority.final_cannot(cannot, keep, False, path, [], [], None, None) == [kept, cannot[0]]
    assert majority.final_cannot(cannot, keep, True, tmp_path / "dry.json", [], [], None, None) == cannot
    majority.write_overrides(path, "n", written, cannot)
    assert json.loads(path.read_text(encoding="utf-8"))["cannot"] == cannot


# R280 a build refuses an override with no value of any field
def test_r280_a_build_refuses_an_override_with_no_value_of_any_field_never_writes_it_as_the_text_none(tmp_path):
    import json
    import pytest
    import plan_rules
    import project_profile
    from test_addin_review import entry
    groups = {"native": ["u11"], "parsons": ["imc"]}
    for field in ("parameter RRE_ZZ_SP_Prototype-FFL", "entryM", "insert"):
        path = tmp_path / "list.json"
        path.write_text(json.dumps({"note": "n", "overrides": [entry("2", field, None, "parsons", "IMC.rvt", "imc"),
                                                               entry("3", "mirrored", True, "parsons", "IMC.rvt", "imc")]}), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.load_overrides(path, {"parsons"}, None, groups)
        assert "no value" in str(error.value) and "plot 2 field " + field in str(error.value) and "R280" in str(error.value)
    # A field the build does not load is not refused
    assert plan_rules.load_overrides(path, {"parsons"}, ("mirrored",), groups) == {"3": {"mirrored": True}}


# R340 a plot with no link is explained by its insert and rotation listed in cannot
def test_r340_the_proof_reads_the_cannot_list_so_a_plot_with_no_link_is_explained_by_its_listed_insert_and_rotation(tmp_path):
    import json
    import build_proof
    from test_build_proof import context, key_row, states
    from test_addin_review import entry
    path = tmp_path / "list.json"
    listed = [{"plot": "3", "field": field, "source": "parsons", "model": "IMX.rvt", "key": "imx", "decision": "d", "reason": "r"} for field in ("insert", "rotationDeg")]
    path.write_text(json.dumps({"note": "n", "overrides": [entry("2", "mirrored", True, "parsons", "IMX.rvt", "imx")], "cannot": listed}), encoding="utf-8")
    overrides = build_proof.overrides_of(path, {"parsons"})
    assert set(overrides) == {("imx", "2", "mirrored"), ("imx", "3", "insert"), ("imx", "3", "rotationDeg")}
    plan = {"plots": [], "missing": [{"plot": "3", "written": False, "missing": [{"key": "transform", "why": "the face has no front edge"}]}]}
    assert states(build_proof.compare("imx", plan, [key_row("3")], {"plots": []}, context(overrides)))[("3", "written")] == "override"


# R341 the second method takes the cannot list of the dry run as misses of the first method
def test_r341_the_first_misses_hold_the_cannot_list_of_the_dry_run():
    import key_rules_check
    dry = {"overrides": [{"decision": "parsons entry", "key": "imx", "plot": "1"}], "cannot": [{"decision": "parsons entry", "key": "imx", "plot": "5"}]}
    assert key_rules_check.first_misses(key_rules_check.dry_entries(dry), {}) == {"parsons entry": {("imx", "1"), ("imx", "5")}}
