"""Round ADDIN PR 3c: the levels of the Parsons keys to the mm. R339 measures them as a build gives them, R340 builds and proves
them, R341 checks them a second way. Every name and number here is made up (R10)."""
import json

import pytest

import majority
import plan_rules
import project_profile

SETTINGS = project_profile.load_settings()
PROFILE = project_profile.load_profile(project_profile.profile_path(SETTINGS))
LP = plan_rules.level_profile(PROFILE, "parsons2024Rev6")
UNITS = {"1": "DP1", "2": "TH1", "3": "TH1", "4": "DP1", "5": "VL4", "6": "DP2", "7": "DP2"}
ROW = ["1", "2", "3", "4"]


def block_of(**change):
    return dict(majority.load_block(SETTINGS), levelPassMm=0.5, levelBandsMm=[1, 5], **change)


# R339 the plots that share one FFL
def test_r339_a_cluster_splits_by_its_units_in_row_order_and_a_unit_in_no_cluster_shares_its_ffl_with_its_row_pair():
    roles = {plot: {"plots": ROW} for plot in ROW}
    partners = {"6": "7", "7": "6"}
    found = plan_rules.level_pieces(list(UNITS), roles, partners, UNITS, {"DP1 TH1 TH1 DP1": "middle"})
    assert found["1"] == ["1", "2"] and found["4"] == ["3", "4"] and found["6"] == ["6", "7"] and found["5"] == ["5"]
    # A table that does not name the units keeps the cluster whole, as R67 does
    assert plan_rules.level_pieces(ROW, roles, {}, UNITS, {})["2"] == ROW
    assert plan_rules.level_pieces(ROW, roles, {}, UNITS, {"DP1 TH1 TH1 DP1": "walls 1"})["2"] == ["2", "3", "4"]
    assert plan_rules.split_walls("middle", 4) == [2] and plan_rules.split_walls("none", 4) == [] and plan_rules.split_walls("walls 1,3", 4) == [1, 3]


def test_r339_the_ffl_of_a_piece_is_its_highest_el_at_entry_plus_the_base_of_that_unit_to_the_mm_and_the_height_comes_from_that_ffl():
    entries = {"1": {"unit": "DP1", "entry": 10.0274}, "2": {"unit": "TH1", "entry": 10.0273}, "6": {"unit": "DP2", "entry": 10.0276}, "7": {"unit": "DP2", "entry": 9.9876}}
    rows = plan_rules.key_level_rows(entries, {"1": ["1", "2"], "2": ["1", "2"], "6": ["6", "7"], "7": ["6", "7"]}, PROFILE, LP, {"steps": {"rule": "stepsFromHeight"}})
    # Rounded to the mm first (R67), so the height is the FFL of the sheet less El @ Entry, never from a hidden fourth decimal
    assert rows["1"]["ffl"] == 10.127 and rows["1"]["height"] == pytest.approx(0.0996) and rows["2"]["height"] == pytest.approx(0.0997)
    assert rows["6"]["ffl"] == 10.228 and rows["7"]["height"] == pytest.approx(0.2404) and rows["7"]["ssl"] == pytest.approx(10.153)
    # The steps of the base and the risers: a DP2 0.04 over its base of 0.20 m has one step where the step bands give two
    assert rows["7"]["steps"] == 1 and plan_rules.steps_of(0.2404, LP) == 2


def test_r339_half_a_riser_over_the_base_adds_no_step_and_the_prototype_ffl_is_the_ffl_down_to_the_cm():
    assert plan_rules.steps_from_height(0.185, 0.1, 0.17) == 1 and plan_rules.steps_from_height(0.1851, 0.1, 0.17) == 2
    assert plan_rules.steps_from_height(0.05, 0.1, 0.17) == 1 and plan_rules.steps_from_height(0.44, 0.1, 0.17) == 3
    assert plan_rules.prototype_ffl("fflDownToTheCm", 173.329) == "173.320" and plan_rules.prototype_ffl("fflDownToTheCm", 10.3) == "10.300"
    assert plan_rules.prototype_ffl("ffl", 173.329) == "173.329"
    assert plan_rules.key_entry([10.0, 10.0], "DP1", LP, 5.52) == 10.0276 and plan_rules.key_entry(None, "DP1", LP, 5.52) is None


# R339 a decision of the levels of a group a build by keyRules builds
def level_item(plot, entry, ffl, steps, role=None, prototype=None):
    code = UNITS[plot]
    kind = "villa" if code == "VL4" else "townhouse" if code.startswith("TH") else "duplex"
    return {"plot": plot, "key": "imc", "group": "parsons", "code": code, "rev9": code, "kind": kind, "inCluster": role is not None, "role": role,
            "neighbours": {"left": [], "right": []}, "modelKind": "TU", "file": "a.rvt", "factors": {"all": "all", "typology": code, "steps": steps},
            "readParameters": {} if prototype is None else {"RRE_ZZ_SP_Prototype-FFL": prototype},
            "row": {"rampType": None, "rampSlopePercent": None, "entry": entry, "entrySource": "schedule", "rampBack": None, "rampFront": None, "rampFrontFall": None,
                    "ffl": ffl, "height": None if ffl is None else round(ffl - entry, 4), "yardTop": None, "genericTop": ffl, "parameters": "{}", "steps": steps,
                    "mirrored": None}}


def level_run(block=None):
    roles = {plot: {"plots": ROW, "side": "left" if plot in ("1", "2") else "right", "index": ROW.index(plot) + 1} for plot in ROW}
    fronts = {"1": 10.0, "2": 10.0, "3": 10.1, "4": 9.8724, "5": 10.0, "6": 10.0, "7": 9.96}
    site = {plot: {"front": [level, level], "depth": 5.52, "unit": UNITS[plot]} for plot, level in fronts.items()}
    items = [level_item("1", 10.0274, 10.127, "1", roles["1"], "10.120"), level_item("2", 10.0273, 10.127, "1", roles["2"], "10.120"),
             level_item("3", 10.1300, 10.230, "1", roles["3"], "10.230"), level_item("4", 9.9000, 10.230, "2", roles["4"], "10.230"),
             level_item("5", 10.1104, 10.210, "1", None, "10.210"), level_item("6", 10.0276, 10.228, "1", None, "10.220"),
             level_item("7", 9.9876, 10.228, "1", None, "10.220")]
    levels = {"parsons": {"lp": LP, "site": site, "roles": roles, "partners": {"6": "7", "7": "6"}}}
    return majority.run(items, {}, PROFILE, block or block_of(), [majority.ROAD], [], levels=levels)


def test_r339_el_at_entry_is_held_at_the_pass_limit_against_the_road_the_build_gives_with_its_share_at_1_and_5_mm():
    results, misses, _, extras = level_run()
    names = {result["decision"]: result for result in results}
    entry = names["parsons entry"]
    assert entry["coded"] == "roadLevel" and entry["misses"] == 1 and entry["passMm"] == 0.5
    assert entry["bands"]["1mm"]["share"] == round(6 / 7, 4) and entry["bands"]["5mm"]["share"] == 1.0 and entry["bands"]["5mm"]["byKey"] == {"imc": 1.0}
    found = [miss for miss in misses if miss["field"] == "entryM"]
    assert [(miss["plot"], miss["value"], miss["rule"]) for miss in found] == [("3", 10.13, 10.1276)]
    listed = majority.overrides(found, block_of(), {"imc": "IMC.rvt"})
    assert listed[0]["value"] == {"gapToRuleM": 0.0024}


def test_r339_the_split_is_learned_per_cluster_by_its_units_and_the_ffl_is_judged_on_the_el_at_entry_a_build_holds():
    results, misses, _, extras = level_run()
    names = {result["decision"]: result for result in results}
    split = names["parsons levelSplit"]
    assert split["coded"] == "units" and split["plots"] == 1 and split["tests"]["units"]["rule"] == {"parsons | DP1 TH1 TH1 DP1": "middle"}
    ffl = names["parsons ffl"]
    # On the El @ Entry of the key every plot holds, the build holds plots 1 and 2 at the road it gives, 0.2 and 0.3 mm off the key, and
    # its FFL rounds to the next mm, so those two are its misses and their gaps move the FFL of the build onto the key
    assert ffl["coded"] == "oneFflPerPieceAtTheHighestEntry" and ffl["share"] == 1.0
    assert ffl["tests"]["oneFflPerClusterAtTheHighestEntry"]["holds"] == 4
    assert ffl["byBuild"] == {"holds": 5, "breaks": 2, "cannot": 0, "share": round(5 / 7, 4)}
    found = sorted((miss["plot"], miss["value"], miss["rule"]) for miss in misses if miss["field"] == "heightM")
    assert found == [("1", 10.127, 10.128), ("2", 10.127, 10.128)]
    assert majority.overrides([miss for miss in misses if miss["field"] == "heightM"], block_of(), {"imc": "IMC.rvt"})[0]["value"] == {"gapToRuleM": -0.001}
    assert extras["levelMisses"]["parsons levelSplit"] == []


def test_r339_steps_and_the_prototype_ffl_are_judged_on_the_levels_a_build_holds_and_the_other_surfaces_are_checks():
    results, misses, _, extras = level_run()
    names = {result["decision"]: result for result in results}
    steps = names["parsons steps"]
    assert steps["coded"] == "stepsFromHeight" and steps["tests"]["stepBands"]["holds"] == 6 and steps["misses"] == 0
    prototype = names["parsons parameter RRE_ZZ_SP_Prototype-FFL"]
    assert prototype["coded"] == "fflDownToTheCm" and prototype["misses"] == 0 and prototype["tests"]["ffl"]["holds"] == 3
    # A build takes the road of frontRoad of its level profile, so a candidate of another surface or of a datum is reported, never coded
    entry = names["parsons entry"]
    assert all(test.get("checkOnly") for label, test in entry["tests"].items() if label != "roadLevel")
    # SSL is in no key
    assert "ssl" in extras["levelNotMeasured"]


def test_r339_a_run_of_a_group_a_build_by_key_rules_builds_needs_the_road_surfaces_the_build_asks():
    assert majority.level_groups(PROFILE, majority.load_block(SETTINGS)) == {"parsons": "parsons2024Rev6"}
    with pytest.raises(project_profile.ProfileError) as error:
        majority.need_roads({"parsons": "parsons2024Rev6"}, None, None)
    assert "--landxml" in str(error.value) and "R339" in str(error.value)
    majority.need_roads({}, None, None)
    majority.need_roads({"parsons": "parsons2024Rev6"}, "landxml", "scratch")


def test_r339_the_config_holds_the_pass_limit_and_the_bands_of_a_level():
    block = majority.load_block(SETTINGS)
    assert block["levelPassMm"] == 0.5 and block["levelBandsMm"] == [1, 5]
    assert {"ffl", "levelSplit", "baseHeight"} <= set(block["kindLeaveOut"]["SE"]) and "ffl" in block["overrideDecisions"]


# R339, R338 the keyRules block of the levels
def level_report():
    def decision(group, word, coded, rule=None, bands=None, built=None, share=1.0):
        found = {"decision": group + " " + word, "coded": coded, "share": share, "strong": share >= 0.9, "plots": 4, "misses": 0,
                 "tests": {coded: {"holds": 4, "breaks": 0, "cannot": 0, "share": share, "byKey": {"imx": {"holds": 4}}, "byCode": {"DP1": {"holds": 4}}}}}
        if rule is not None:
            found["tests"][coded]["rule"] = rule
        if bands is not None:
            found.update(bands=bands, passMm=0.5)
        if built is not None:
            found["byBuild"] = built
        return found
    bands = {"1mm": {"holds": 4, "share": 1.0, "byKey": {"imx": 1.0}}, "5mm": {"holds": 4, "share": 1.0, "byKey": {"imx": 1.0}}}
    return {"decisions": [decision("parsons", "entry", "roadLevel", bands=bands, share=0.25), decision("native", "entry", "Total less the datum of each key", share=0.2),
                          decision("parsons", "levelSplit", "units", rule={"parsons | DP1 TH1 TH1 DP1": "middle", "parsons | DP3 TH3 TH3 DP3": "none"}),
                          decision("parsons", "ffl", "oneFflPerPieceAtTheHighestEntry", bands=bands, built={"holds": 3, "breaks": 1, "cannot": 0, "share": 0.75}),
                          decision("native", "height", "medianPerUnit", share=0.35),
                          decision("parsons", "steps", "stepsFromHeight", built={"holds": 4, "breaks": 0, "cannot": 0, "share": 1.0})]}


def test_r339_the_key_rules_block_holds_each_level_rule_with_its_pass_limit_its_shares_at_1_and_5_mm_and_the_native_share_beside_it():
    import key_rules_block
    block = majority.load_block(SETTINGS)
    found = key_rules_block.block_of(level_report(), ["entry", "levelSplit", "ffl", "steps", "ssl"], {}, block)
    assert found["entry"]["rule"] == "roadLevel" and found["entry"]["passMm"] == 0.5 and found["entry"]["bands"]["5mm"]["share"] == 1.0
    assert found["entry"]["native"] == 0.2 and found["entry"]["strong"] is False
    assert found["levelSplit"]["table"] == {"DP1 TH1 TH1 DP1": "middle", "DP3 TH3 TH3 DP3": "none"}
    # The FFL beside the FFL less El @ Entry of the Native keys, the decision Round 17 measured them by
    assert found["ffl"]["byBuild"]["share"] == 0.75 and found["ffl"]["native"] == 0.35
    assert found["steps"]["rule"] == "stepsFromHeight"
    # SSL is in no key, a build gives the FFL plus levels.ground_ssl and says why
    assert found["ssl"]["rule"] == "fflPlusGroundSsl" and "no key holds SSL" in found["ssl"]["notMeasured"]
