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


# R340 the proof needs every input of every key it covers and counts a level it could not hold
def test_r340_the_proof_refuses_a_key_it_covers_with_no_plan_key_plan_or_key_read():
    import pytest
    import build_proof
    import project_profile
    for plans, key_plans, reads, flag in (({"imx": 1}, {"imx": 1, "imy": 1}, {"imx": 1, "imy": 1}, "--plan of imy"),
                                          ({"imx": 1, "imy": 1}, {"imx": 1}, {"imx": 1, "imy": 1}, "--key-plan of imy"),
                                          ({"imx": 1, "imy": 1}, {"imx": 1, "imy": 1}, {"imx": 1}, "--key-read of imy")):
        with pytest.raises(project_profile.ProfileError) as error:
            build_proof.check_inputs(["imx", "imy"], plans, key_plans, reads)
        assert flag in str(error.value) and "R340" in str(error.value)
    build_proof.check_inputs(["imx", "imy"], {"imx": 1, "imy": 1}, {"imx": 1, "imy": 1}, {"imx": 1, "imy": 1})


def test_r340_a_level_the_proof_can_not_read_is_not_checked_and_fails_the_proof_one_the_key_holds_none_of_is_counted_apart():
    import build_proof
    from test_addin_levels import level_context, proof_plot, proof_row
    plan = {"plots": [proof_plot("1", 10.0, 10.1, 1, "10.100"), proof_plot("2", 10.0, 10.1, 1, "10.100"), proof_plot("3", 10.0, 10.1, 1, "10.100")], "missing": []}
    rows = [proof_row("1", 10.0, 10.1, 1, 9.9), proof_row("2", 10.0, 10.1, 1, 9.9), proof_row("3", 10.0, 10.1, 1, 9.9)]
    rows[2]["entry"] = None
    # The read of the key holds no link of plot 2, the key holds no El @ Entry of plot 3
    reads = {("imx", "1"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}, ("imx", "3"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}}
    verdicts = build_proof.compare("imx", plan, rows, {"plots": []}, level_context(), reads)
    found = {(item["plot"], item["field"]): item for item in verdicts}
    assert found[("2", "parameter RRE_ZZ_SP_Prototype-FFL")]["state"] == "notChecked"
    assert found[("3", "entry")]["state"] == "keyHoldsNone" and found[("3", "height")]["state"] == "keyHoldsNone"
    counted = build_proof.tally(verdicts)
    assert counted["notCheckedByKey"] == {"imx": 1} and counted["levelHolds"]["imx"]["entry"]["keyHoldsNone"] == 1
    assert build_proof.exit_code(counted) == 1
    assert build_proof.exit_code(build_proof.tally([item for item in verdicts if item["state"] != "notChecked"])) == 0


# R340 the holds by the rule alone, by an override, and the misses, per key and level
def test_r340_the_proof_counts_per_key_and_level_the_holds_by_the_rule_alone_by_an_override_and_the_misses():
    import build_proof
    from test_addin_levels import level_context, proof_plot, proof_row
    plots = [proof_plot("1", 10.0, 10.1, 1, "10.100"), proof_plot("2", 10.0, 10.1, 1, "10.100"), proof_plot("3", 10.002, 10.1, 1, "10.100")]
    for found, rule in zip(plots, (10.0, 9.998, 10.002)):
        found["levelRule"] = {"entry": rule, "ffl": 10.1, "height": round(10.1 - rule, 4), "steps": 1, "rampFrontCorners": [9.9, 9.9],
                              "parameter RRE_ZZ_SP_Prototype-FFL": "10.100"}
    plots[1]["overrides"] = {"applied": ["entryM"], "notApplied": {}}
    rows = [proof_row(plot, 10.0, 10.1, 1, 9.9) for plot in ("1", "2", "3")]
    reads = {("imx", plot): {"RRE_ZZ_SP_Prototype-FFL": "10.100"} for plot in ("1", "2", "3")}
    counted = build_proof.tally(build_proof.compare("imx", {"plots": plots, "missing": []}, rows, {"plots": []}, level_context(), reads))
    entry = counted["levelHolds"]["imx"]["entry"]
    assert (entry["byRule"], entry["byOverride"], entry["misses"]) == (1, 1, 1)
    assert counted["levelHolds"]["imx"]["ffl"]["byRule"] == 3


# R340 the ramp front per corner as well as its mean, a corner off by the fall of Q057 never unexplained
def test_r340_the_ramp_front_is_held_per_corner_as_well_as_by_its_mean_and_a_corner_off_by_the_fall_is_left_to_its_question():
    import json
    import build_proof
    from test_addin_levels import level_context, proof_plot, proof_row
    plots = [proof_plot("1", 10.0, 10.1, 1, "10.100", front=(9.9, 9.9)), proof_plot("2", 10.0, 10.1, 1, "10.100", front=(9.9, 9.9)),
             proof_plot("3", 10.0, 10.1, 1, "10.100", front=(9.9, 9.901))]
    rows = [proof_row(plot, 10.0, 10.1, 1, 9.9) for plot in ("1", "2", "3")]
    # The key of plot 2 falls 10 mm along its front, its mean on the road the build gives, plot 3 lies half a mm off at the mean
    rows[1]["rampCorners"] = json.dumps([[13.0, 0.0, 9.895], [17.0, 0.0, 9.905], [17.0, 3.0, 10.0], [13.0, 3.0, 10.0]])
    reads = {("imx", plot): {"RRE_ZZ_SP_Prototype-FFL": "10.100"} for plot in ("1", "2", "3")}
    verdicts = build_proof.compare("imx", {"plots": plots, "missing": []}, rows, {"plots": []}, level_context(), reads)
    found = {(item["plot"], item["field"]): item for item in verdicts}
    assert found[("1", "rampFrontCorner")]["state"] == "holds" and found[("2", "rampFront")]["state"] == "holds"
    assert found[("2", "rampFrontCorner")]["state"] == "question" and "Q057" in found[("2", "rampFrontCorner")]["why"]
    assert found[("2", "rampFrontCorner")]["gapMm"] == 5.0
    counted = build_proof.tally(verdicts)
    assert counted["unexplainedLevelsByKey"]["imx"]["pass"] == 0
    # A level that holds at the pass limit itself is counted apart, plot 3 half a mm off at its mean and 1 mm at a corner
    assert counted["levelHolds"]["imx"]["rampFront"]["atTheLimit"] == 1 and counted["levelHolds"]["imx"]["rampFrontCorner"]["misses"] == 2


def test_r340_a_build_writes_the_levels_the_rule_alone_gives_beside_the_levels_its_overrides_moved(tmp_path, monkeypatch):
    from test_addin_review import entry
    full = [entry("4", "entryM", {"gapToRuleM": 0.0013}, "parsons", "IMC.rvt", "imc"), entry("4", "heightM", {"gapToRuleM": 0.001}, "parsons", "IMC.rvt", "imc"),
            entry("4", "rampFrontM", {"gapToRuleM": 0.002}, "parsons", "IMC.rvt", "imc")]
    found, _ = level_build(tmp_path, monkeypatch, overrides=full)
    rule = found["4"]["levelRule"]
    assert rule["entry"] == 10.0626 and rule["ffl"] == 10.263 and rule["height"] == 0.2004 and rule["steps"] == 1
    assert rule["parameter RRE_ZZ_SP_Prototype-FFL"] == "10.260" and level_of(found["4"], "ffl") == 10.264
    corners = found["4"]["floors"]["ramp"]["corners"]["value"]
    assert [round(z + 0.002, 3) for z in rule["rampFrontCorners"]] == [corners[0][2], corners[1][2]] and "rampFront" not in rule


# R341 a level item one side holds and the other does not is a mismatch
def test_r341_a_level_item_only_one_method_holds_is_a_mismatch_never_left_out():
    import key_rules_check
    _, _, _, extras = level_run()
    # The first method lists every item each level decision judged
    assert extras["levelItems"]["parsons levelSplit"] == ["imc 1"] and "imc 7" in extras["levelItems"]["parsons ffl"]
    own = {("imc", "1"): {"baseHeight": True, "ffl": True}, ("imc", "2"): {"ffl": False}, ("imc", "3"): {"baseHeight": True}}
    first = {"parsons baseHeight": ["imc 1", "imc 4"], "parsons ffl": ["imc 1", "imc 2"]}
    found = key_rules_check.one_sided(own, first, "parsons")
    assert found == [{"key": "imc", "plot": "3", "decision": "baseHeight", "own": "holds", "first": "no item"},
                     {"key": "imc", "plot": "4", "decision": "baseHeight", "own": "no item", "first": "an item"}]


# R338, R339 the split per unit sequence with its count, strong or weak by majority.strongShare
def test_r338_key_rules_holds_per_unit_sequence_the_clusters_of_each_split_and_whether_the_coded_split_is_strong():
    import key_rules_block
    import majority
    from test_addin_levels import SETTINGS, level_report
    _, _, _, extras = level_run()
    assert extras["levelSplitPatterns"]["parsons"] == {"DP1 TH1 TH1 DP1": {"middle": 1}}
    report = level_report()
    report["levelSplitPatterns"] = {"parsons": {"DP1 TH1 TH1 DP1": {"middle": 17, "none": 7, "walls 1": 6, "walls 3": 7}, "DP3 TH3 TH3 DP3": {"none": 19, "middle": 1}}}
    found = key_rules_block.block_of(report, ["levelSplit"], {}, majority.load_block(SETTINGS))["levelSplit"]
    assert found["bySequence"]["DP1 TH1 TH1 DP1"] == {"coded": "middle", "clusters": 37, "holds": 17, "share": 0.4595, "strong": False, "tie": False,
                                                      "patterns": {"middle": 17, "none": 7, "walls 1": 6, "walls 3": 7}}
    assert found["bySequence"]["DP3 TH3 TH3 DP3"]["strong"] is True and found["bySequence"]["DP3 TH3 TH3 DP3"]["share"] == 0.95


# R339, R340 a build and the measure of its levels ask the road surfaces of one setting
def test_r339_the_measure_and_a_build_take_the_road_surfaces_from_one_setting():
    import json
    import key_rules_check
    import majority
    import plan_rules
    import project_profile
    from test_addin_levels import LP, PROFILE, SETTINGS
    block = SETTINGS.get("siteBuild")
    assert plan_rules.road_names(block, LP) == list(block["roadSurfaces"])
    named = dict(LP, frontRoad=dict(LP["frontRoad"], surface="Made Up Surface"))
    assert plan_rules.road_names(block, named) == ["Made Up Surface"]
    values = json.loads(json.dumps(PROFILE.values))
    values["levelProfiles"]["parsons2024Rev6"]["frontRoad"]["surface"] = "Made Up Surface"
    profile = project_profile.ProjectProfile(PROFILE.path, values)
    assert majority.level_road_names({"parsons": "parsons2024Rev6"}, profile, SETTINGS) == ["Made Up Surface"]
    assert majority.level_road_names({"parsons": "parsons2024Rev6"}, PROFILE, SETTINGS) == list(block["roadSurfaces"])
    # The second method by its own route
    assert key_rules_check.own_road_names(SETTINGS.values, named) == ["Made Up Surface"]
    assert key_rules_check.own_road_names(SETTINGS.values, LP) == list(block["roadSurfaces"])


# R340 one FFL per piece needs the split and the base the keys were measured on
def test_r340_one_ffl_per_piece_with_no_split_or_no_base_height_in_key_rules_is_refused():
    import pytest
    import plan_rules
    import project_profile
    from test_addin_review import made_key_rules
    for word in ("levelSplit", "baseHeight"):
        rules = dict(made_key_rules(), **{name: value for name, value in LEVEL_RULES.items() if name != word})
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.check_key_rules(rules)
        assert word in str(error.value) and "R340" in str(error.value)
    whole = dict(made_key_rules(), **{name: value for name, value in LEVEL_RULES.items() if name != "levelSplit"})
    whole["ffl"] = {"rule": "oneFflPerClusterAtTheHighestEntry"}
    plan_rules.check_key_rules(whole)


# R280, R340 a steps override is a whole number
def test_r280_a_steps_override_that_is_no_whole_number_is_refused(tmp_path):
    import json
    import pytest
    import plan_rules
    import project_profile
    from test_addin_review import entry
    groups = {"native": ["u11"], "parsons": ["imc"]}
    path = tmp_path / "list.json"
    for bad in ("2.6", 2.6, "two"):
        path.write_text(json.dumps({"note": "n", "overrides": [entry("2", "steps", bad, "parsons", "IMC.rvt", "imc")]}), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.load_overrides(path, {"parsons"}, None, groups)
        assert "whole number" in str(error.value) and "plot 2" in str(error.value)
    path.write_text(json.dumps({"note": "n", "overrides": [entry("2", "steps", "2", "parsons", "IMC.rvt", "imc")]}), encoding="utf-8")
    assert plan_rules.load_overrides(path, {"parsons"}, None, groups) == {"2": {"steps": "2"}}


# R340 the print names the steps rule of keyRules
def test_r340_a_build_by_the_level_rules_prints_the_steps_of_key_rules_and_no_later_phase(tmp_path, monkeypatch):
    import plan_from_book
    found, plan = level_build(tmp_path, monkeypatch)
    lines = "\n".join(plan_from_book.summary_lines(plan["summary"], "parsons2024Rev6", False))
    assert "by keyRules.steps stepsFromHeight of parsons2024Rev6" in lines and "until 3c" not in lines


def test_r340_a_ramp_front_the_rule_alone_gives_holds_by_the_rule_at_the_pass_limit_too():
    import build_proof
    from test_addin_levels import level_context, proof_plot, proof_row
    # The mean of two corners written to the mm lies on a half mm, which a plan written to 3 decimals would round away, so the plan holds
    # the rule corners and the proof takes the rule front from them the same way as the built one
    plot = proof_plot("1", 10.0, 10.1, 1, "10.100", front=(9.9, 9.901))
    plot["levelRule"] = {"entry": 10.0, "ffl": 10.1, "height": 0.1, "steps": 1, "rampFrontCorners": [9.9, 9.901], "parameter RRE_ZZ_SP_Prototype-FFL": "10.100"}
    verdicts = build_proof.compare("imx", {"plots": [plot], "missing": []}, [proof_row("1", 10.0, 10.1, 1, 9.9)], {"plots": []}, level_context(),
                                   {("imx", "1"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}})
    front = build_proof.tally(verdicts)["levelHolds"]["imx"]["rampFront"]
    assert (front["byRule"], front["byNeither"], front["atTheLimit"]) == (1, 0, 1)
