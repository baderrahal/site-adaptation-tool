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
    # On the El @ Entry of the key every plot holds. A build takes the FFL on the El @ Entry of the rule, 0.2 and 0.3 mm off the key on
    # plots 1 and 2, whose FFL rounds to the next mm, and 2.4 mm under the key on plot 3, which sets the FFL of 3 and 4, so those four
    # are its misses and the gap of each moves its own FFL onto the key (Round ADDIN 3c review)
    assert ffl["coded"] == "oneFflPerPieceAtTheHighestEntry" and ffl["share"] == 1.0
    assert ffl["tests"]["oneFflPerClusterAtTheHighestEntry"]["holds"] == 4
    assert ffl["byBuild"] == {"holds": 3, "breaks": 4, "cannot": 0, "share": round(3 / 7, 4)}
    found = sorted((miss["plot"], miss["value"], miss["rule"]) for miss in misses if miss["field"] == "heightM")
    assert found == [("1", 10.127, 10.128), ("2", 10.127, 10.128), ("3", 10.23, 10.228), ("4", 10.23, 10.228)]
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


# R340 a build by the level rules of keyRules
LEVEL_RULES = {"entry": {"rule": "roadLevel"}, "rampFront": {"rule": "roadLevel"}, "levelSplit": {"rule": "units", "table": {"DP1 TH1 TH1 DP1": "middle"}},
               "baseHeight": {"rule": "profile"}, "ffl": {"rule": "oneFflPerPieceAtTheHighestEntry"}, "steps": {"rule": "stepsFromHeight"},
               "parameter RRE_ZZ_SP_Prototype-FFL": {"rule": "fflDownToTheCm"}}


def level_build(tmp_path, monkeypatch, overrides=None, later=None, rules=None, skip=None, rounding=None):
    """A build by keyRules with the level rules on the made up row of test_addin_review, a villa and three DP2 units in no cluster, the
    road rising 0.01 m every 12 m along the row, with the fields majority.overrideKeep leaves to a later phase as later, with skip
    the skip list of the override list (R386) and with rounding the one config names for parsons2024Rev6 (R385)."""
    import argparse
    from shapely.geometry import LineString, box
    import plan_from_book
    import plan_from_past
    from test_addin_review import ROOT, made_key_rules, DP2, VD2
    values = json.loads(json.dumps(SETTINGS.values))
    values["majority"]["overrideKeep"]["laterFields"] = later if later is not None else {}
    if rounding is not None:
        values["majority"]["overrideRounding"]["levelProfiles"]["parsons2024Rev6"] = rounding
    settings = project_profile.ProjectProfile(SETTINGS.path, values)
    profile_values = json.loads(json.dumps(PROFILE.values))
    profile_values["levelProfiles"]["parsons2024Rev6"]["keyRules"] = dict(made_key_rules(), **(LEVEL_RULES if rules is None else rules))
    profile = project_profile.ProjectProfile(PROFILE.path, profile_values)
    numbers = ["1", "2", "3", "4"]
    units = {"1": "VD2", "2": "DP2", "3": "DP2", "4": "DP2"}
    rows = {number: {"locationPlanNumber": number, "typologyRev9": units[number], "footprintMirrored": "no", "zone": "Zone 2", "roadLevelLeft": "", "roadLevelRight": ""}
            for number in numbers}
    faces = {number: box(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0) for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)])]
    monkeypatch.setattr(plan_from_book, "road_level", lambda surfaces, point, snap: (10.0 + 0.01 * point[0] / 12.0, "road"))
    path = None
    if overrides is not None:
        path = tmp_path / "overrides.json"
        path.write_text(json.dumps({"note": "n", "overrides": overrides, **({} if skip is None else {"skip": skip})}), encoding="utf-8")
    args = argparse.Namespace(level_profile=None, overrides=None if path is None else str(path), override_source=None, override_model=[], override_fields=None)
    found = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, roads, {}, [])
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
            "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((ROOT / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    plan = plan_from_book.build(numbers, rows, {}, faces, roads, [], profile, settings, {"units": {}}, {"DP2": DP2, "VD2": VD2}, "folder", site, [("road", None)],
                                outlines, "N1", {}, True, found)
    return {item["plot"]: item for item in plan["plots"]}, plan


def level_of(entry, name):
    return entry["levels"][name]["value"]


def test_r340_a_build_by_the_level_rules_gives_one_ffl_per_piece_to_the_mm_the_steps_of_the_base_and_risers_and_the_prototype_ffl(tmp_path, monkeypatch):
    found, plan = level_build(tmp_path, monkeypatch)
    # The row pairs from the right end: 3 and 4 share one FFL, 2 has no pair, the villa 1 stands alone
    assert level_of(found["3"], "entry") == 10.0526 and level_of(found["4"], "entry") == 10.0626
    assert level_of(found["3"], "ffl") == level_of(found["4"], "ffl") == 10.263 and level_of(found["2"], "ffl") == round(10.0426 + 0.2, 3)
    assert level_of(found["3"], "height") == pytest.approx(0.2104) and level_of(found["3"], "steps") == 1 and level_of(found["3"], "ssl") == pytest.approx(10.188)
    parameters = found["3"]["parameters"]
    assert parameters["RRE_ZZ_SP_Prototype-FFL"]["value"] == "10.260" and parameters["RRE_ZZ_SP_Number-of-Steps"]["source"] == "keyRules"
    assert plan["summary"]["rules"]["steps"]["from"] == "keyRules.steps stepsFromHeight of parsons2024Rev6"


def test_r340_the_level_overrides_move_el_at_entry_and_the_ffl_onto_the_key_and_set_the_steps_and_the_prototype_ffl(tmp_path, monkeypatch):
    from test_addin_review import entry
    overrides = [entry("4", "entryM", {"gapToRuleM": 0.0013}, "parsons", "IMC.rvt", "imc"), entry("3", "heightM", {"gapToRuleM": -0.001}, "parsons", "IMC.rvt", "imc"),
                 entry("2", "steps", "2", "parsons", "IMC.rvt", "imc"), entry("2", "parameter RRE_ZZ_SP_Prototype-FFL", {"gapToRuleM": 0.01}, "parsons", "IMC.rvt", "imc")]
    found, plan = level_build(tmp_path, monkeypatch, overrides=overrides)
    # El @ Entry moved by its gap at 4 decimals, the FFL of the pair stays on the El @ Entry of the rule and the FFL of 3 alone is moved
    # by its own gap, an override moves the levels of its own plot only (Round ADDIN 3c review)
    assert level_of(found["4"], "entry") == 10.0639 and level_of(found["4"], "ffl") == 10.263 and level_of(found["3"], "ffl") == 10.262
    assert level_of(found["4"], "height") == pytest.approx(10.263 - 10.0639)
    assert level_of(found["3"], "height") == pytest.approx(10.262 - 10.0526) and level_of(found["3"], "ssl") == pytest.approx(10.187)
    assert level_of(found["2"], "steps") == 2 and found["2"]["parameters"]["RRE_ZZ_SP_Number-of-Steps"]["value"] == "2"
    assert found["2"]["parameters"]["RRE_ZZ_SP_Prototype-FFL"]["value"] == "10.250" and found["2"]["parameters"]["RRE_ZZ_SP_Prototype-FFL"]["source"] == "override"
    assert plan["summary"]["rules"]["applied"] == {"entryM": 1, "heightM": 1, "parameter RRE_ZZ_SP_Prototype-FFL": 1, "steps": 1}


def test_r340_a_level_rule_a_build_can_not_give_is_refused_and_a_profile_with_no_level_rules_builds_by_r278(tmp_path, monkeypatch):
    from test_addin_review import made_key_rules
    for word, bad in (("ffl", {"rule": "medianPerUnit"}), ("steps", {"rule": "typology", "table": {}}), ("entry", {"rule": "Top Surface-02"}),
                      ("levelSplit", {"rule": "units"}), ("parameter RRE_ZZ_SP_Prototype-FFL", {"rule": "genericTop"})):
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.check_key_rules(dict(made_key_rules(), **dict(LEVEL_RULES, **{word: bad})))
        assert word in str(error.value)
    found, plan = level_build(tmp_path, monkeypatch, rules={}, later={"parsons": ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]})
    # No level rule in keyRules: the levels of R278, the steps of the step bands and no Prototype-FFL parameter
    assert level_of(found["3"], "ffl") == round(10.0526 + 0.2, 3) and "RRE_ZZ_SP_Prototype-FFL" not in found["3"]["parameters"]
    assert found["3"]["parameters"]["RRE_ZZ_SP_Number-of-Steps"]["source"] == "levelProfileStepBands"


# R340 the proof holds the levels
def proof_row(plot, entry_value, ffl, steps, front=None):
    from test_build_proof import key_row
    row = key_row(plot)
    row.update(entry=entry_value, entrySource="schedule", ffl=ffl, height=round(ffl - entry_value, 4), steps=str(steps), rampFront=front, rampBack=None,
               genericTop=ffl, yardTop=round(ffl - 0.1, 3))
    return row


def proof_plot(plot, entry_value, ffl, steps, prototype=None, front=(9.9, 9.9)):
    from test_build_proof import built
    found = built(plot, steps=steps)
    found["levels"] = {"entry": {"value": entry_value}, "ffl": {"value": ffl}, "height": {"value": round(ffl - entry_value, 4)}, "steps": {"value": steps},
                       "ssl": {"value": round(ffl - 0.075, 3)}}
    shift = 12.0 * (int(plot) - 1)
    found["floors"]["ramp"]["corners"]["value"] = [[1.0 + shift, 0.0, front[0]], [5.0 + shift, 0.0, front[1]], [5.0 + shift, 3.0, entry_value],
                                                [1.0 + shift, 3.0, entry_value]]
    found["floors"]["yard"]["z"]["value"] = round(ffl - 0.1, 3)
    if prototype is not None:
        found["parameters"]["RRE_ZZ_SP_Prototype-FFL"] = {"value": prototype}
    return found


def level_context(overrides=None):
    import build_proof
    block = dict(majority.load_block(SETTINGS), levelPassMm=0.5, levelBandsMm=[1, 5])
    rules = dict(LEVEL_RULES, **{"parameter RRE_ZZ_SP_Number-of-Steps": {"rule": "steps", "table": {"1": "1", "2": "2"}}})
    return build_proof.proof_context(block, {"prefix": "RRE-WP-NSP-KEO-", "discipline": "ARC"}, rules, overrides or {}, [])


def test_r340_the_proof_holds_every_level_at_the_pass_limit_and_counts_what_is_unexplained_at_1_and_5_mm():
    import build_proof
    plan = {"plots": [proof_plot("1", 10.0, 10.1, 1, "10.100"), proof_plot("2", 10.0003, 10.101, 1, "10.100"), proof_plot("3", 10.0, 10.103, 1, "10.110")], "missing": []}
    rows = [proof_row("1", 10.0, 10.1, 1, 9.9), proof_row("2", 10.0, 10.1, 1, 9.9), proof_row("3", 10.0, 10.1, 2, 9.9)]
    reads = {("imx", "1"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}, ("imx", "2"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}, ("imx", "3"): {"RRE_ZZ_SP_Prototype-FFL": "10.100"}}
    verdicts = build_proof.compare("imx", plan, rows, {"plots": []}, level_context(), reads)
    found = {(item["plot"], item["field"]): item for item in verdicts}
    assert {field for plot, field in found if plot == "1"} >= {"entry", "ffl", "height", "steps", "rampFront", "parameter RRE_ZZ_SP_Prototype-FFL"}
    assert all(item["state"] == "holds" for (plot, _), item in found.items() if plot == "1")
    # El @ Entry 0.3 mm off holds, the FFL 1 mm off does not, nor its height 0.7 mm off, the FFL 3 mm off holds at 5 mm only
    assert found[("2", "entry")]["state"] == "holds" and found[("2", "ffl")]["state"] == "unexplained" and found[("2", "ffl")]["gapMm"] == pytest.approx(1.0)
    assert found[("3", "steps")]["state"] == "unexplained" and found[("3", "parameter RRE_ZZ_SP_Prototype-FFL")]["state"] == "unexplained"
    counted = build_proof.tally(verdicts)
    assert counted["unexplainedLevelsByKey"] == {"imx": {"pass": 6, "1mm": 4, "5mm": 2}}


def test_r340_an_override_the_build_could_not_place_explains_a_level_and_a_plot_with_no_road_level_is_explained_by_its_input():
    import build_proof
    overrides = {("imx", "2", "heightM"): {"value": {"gapToRuleM": -0.001}}, ("imx", "3", "entryM"): {"value": None}}
    two = proof_plot("2", 10.0, 10.101, 1)
    two["overrides"] = {"applied": [], "notApplied": {"heightM": "made up"}}
    plan = {"plots": [two], "missing": [{"plot": "3", "written": False, "missing": [{"key": "levels.ffl", "why": "levels.py gives no FFL"}]},
                                        {"plot": "4", "written": False, "missing": [{"key": "levels.ffl", "why": "levels.py gives no FFL"}]}],
            "summary": {"plotsWithNoRoadLevel": ["3", "4"]}}
    rows = [proof_row("2", 10.0, 10.1, 1), proof_row("3", 10.0, 10.1, 1), proof_row("4", 10.0, 10.1, 1)]
    found = {(item["plot"], item["field"]): item["state"] for item in build_proof.compare("imx", plan, rows, {"plots": []}, level_context(overrides), {})}
    assert found[("2", "ffl")] == "override" and found[("3", "written")] == "input" and found[("4", "written")] == "input"


# R341 the second method of the levels
LANDXML = """<?xml version="1.0"?>
<LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2"><Surfaces><Surface name="Made Up"><Definition surfType="TIN"><Pnts>
<P id="1">0.0 0.0 0.0</P>
<P id="2">0.0 10.0 10.0</P>
<P id="3">10.0 10.0 10.0</P>
<P id="4">10.0 0.0 0.0</P>
<P id="5">0.0 20.0 20.0</P>
<P id="6">10.0 20.0 20.0</P>
</Pnts><Faces>
<F>1 2 3</F>
<F>1 3 4</F>
<F i="1">2 5 6</F>
</Faces></Definition></Surface></Surfaces></LandXML>
"""


def test_r341_the_own_tin_reads_the_landxml_by_its_text_gives_the_level_inside_and_on_the_edge_within_the_snap_and_skips_a_hidden_face(tmp_path):
    import key_rules_check
    path = tmp_path / "made_up.xml"
    path.write_text(LANDXML, encoding="utf-8")
    tin = key_rules_check.OwnTin(path, None, 0.005)
    # The text holds northing then easting, the level rises 1 m per m to the east
    assert tin.level(2.0, 3.0, 0.005) == pytest.approx(2.0) and tin.level(10.003, 5.0, 0.005) == pytest.approx(10.0)
    assert tin.level(10.01, 5.0, 0.005) is None and tin.level(15.0, 5.0, 0.005) is None and len(tin.triangles) == 2
    # A box leaves out the triangles it does not meet
    assert len(key_rules_check.OwnTin(path, (100.0, 100.0, 200.0, 200.0), 0.005).triangles) == 0
    assert key_rules_check.own_road([("a", key_rules_check.OwnTin(path, (100.0, 100.0, 200.0, 200.0), 0.005)), ("b", tin)], (4.0, 1.0), 0.005) == pytest.approx(4.0)


def own_rows():
    """The made up plots of level_run as past_all.csv rows, typed as the csv gives them."""
    found = []
    for item in level_run_items():
        row = item["row"]
        found.append({"key": "imc", "plot": item["plot"], "source": "parsons", "kind": "TU", "typologyRev9": item["code"], "entry": str(row["entry"]), "entrySource": "schedule",
                      "rampBack": "", "ffl": str(row["ffl"]), "steps": row["steps"], "rampFront": "", "parameters": "{}"})
    return found


def level_run_items():
    roles = {plot: {"plots": ROW} for plot in ROW}
    return [level_item("1", 10.0274, 10.127, "1", roles["1"], "10.120"), level_item("2", 10.0273, 10.127, "1", roles["2"], "10.120"),
            level_item("3", 10.1300, 10.230, "1", roles["3"], "10.230"), level_item("4", 9.9000, 10.230, "2", roles["4"], "10.230"),
            level_item("5", 10.1104, 10.210, "1", None, "10.210"), level_item("6", 10.0276, 10.228, "1", None, "10.220"),
            level_item("7", 9.9876, 10.228, "1", None, "10.220")]


def test_r341_the_own_levels_hold_el_at_entry_the_split_the_ffl_the_steps_and_the_prototype_ffl_on_what_a_build_holds_as_the_first_method():
    import key_rules_check
    results, misses, _, extras = level_run()
    report = {"decisions": results, "levelMisses": extras["levelMisses"]}
    fronts = {"1": 10.0, "2": 10.0, "3": 10.1, "4": 9.8724, "5": 10.0, "6": 10.0, "7": 9.96}
    road = {plot: {"entry": round(level + (2.0 if UNITS[plot] == "VL4" else 0.5) / 100.0 * 5.52, 4), "front": level} for plot, level in fronts.items()}
    shapes = {"order": {plot: ROW for plot in ROW}, "partners": {"6": "7", "7": "6"}}
    reads = {("imc", item["plot"]): item["readParameters"] for item in level_run_items()}
    found = key_rules_check.level_verdicts(own_rows(), road, shapes, UNITS, report, PROFILE.values, LP, block_of(), reads)
    # The own verdicts are those of the first method: El @ Entry of 3 misses, the FFL of 1 to 4 on the El @ Entry of the rule, the rest holds
    first = {(miss["plot"], miss["decision"].split(" ", 1)[1]) for miss in misses}
    for (key, plot), verdicts in found.items():
        for decision, holds in verdicts.items():
            assert holds == ((plot, decision) not in first), (plot, decision)
    assert found[("imc", "3")]["entry"] is False and found[("imc", "1")]["ffl"] is False and found[("imc", "4")]["ffl"] is False
    assert found[("imc", "6")]["ffl"] is True
    assert found[("imc", "1")]["levelSplit"] is True and "levelSplit" not in found[("imc", "2")]
    assert found[("imc", "7")]["steps"] is True and found[("imc", "6")]["parameter RRE_ZZ_SP_Prototype-FFL"] is True
    # A plot with no road level has no El @ Entry, the miss the first method lists as a cannot
    road["5"] = {"entry": None, "front": None}
    assert key_rules_check.level_verdicts(own_rows(), road, shapes, UNITS, report, PROFILE.values, LP, block_of(), reads)[("imc", "5")]["entry"] is False


def test_r341_the_steps_of_the_own_route_count_half_a_riser_as_no_step_and_its_own_ffl_rounds_to_the_mm_first():
    import key_rules_check
    assert key_rules_check.own_steps("stepsFromHeight", 0.185, 0.1, 0.17, []) == 1 and key_rules_check.own_steps("stepsFromHeight", 0.1851, 0.1, 0.17, []) == 2
    assert key_rules_check.own_steps("stepBands", 0.2404, 0.2, 0.17, [[0.205, 1], [0.38, 2], [99.0, 3]]) == 2
    assert key_rules_check.own_ffl([(10.0274, 0.1), (10.0273, 0.1), (None, 0.2)]) == 10.127 and key_rules_check.own_ffl([(None, 0.1)]) is None
    assert key_rules_check.own_down_to_the_cm(173.329) == 173.32


# R339, R340 the committed rules profile and override list
def test_r339_r340_the_rules_profile_holds_the_level_rules_of_the_keys_and_a_build_takes_them():
    rules = plan_rules.key_rules_of(plan_rules.level_profile(PROFILE, "parsons2024Rev6"))
    assert plan_rules.key_levels_of(rules)
    assert rules["entry"]["rule"] == "roadLevel" and rules["ffl"]["rule"] == "oneFflPerPieceAtTheHighestEntry" and rules["steps"]["rule"] == "stepsFromHeight"
    assert rules["parameter RRE_ZZ_SP_Prototype-FFL"]["rule"] == "fflDownToTheCm" and rules["levelSplit"]["table"]["DP3 TH3 TH3 DP3"] == "none"
    for word in ("entry", "rampFront", "ffl", "baseHeight", "parameter RRE_ZZ_SP_Prototype-FFL"):
        assert rules[word]["passMm"] == 0.5 and set(rules[word]["bands"]) == {"1mm", "5mm"}
    assert rules["ssl"]["rule"] == "fflPlusGroundSsl" and "no key holds SSL" in rules["ssl"]["notMeasured"]


def test_r339_the_override_list_holds_the_level_misses_of_the_keys_as_gaps_and_never_a_level():
    from collections import Counter
    import override_source
    data = json.loads(override_source.list_path(SETTINGS).read_text(encoding="utf-8"))
    parsons = [item for item in data["overrides"] if item["source"] == "parsons"]
    fields = Counter(item["field"] for item in parsons)
    assert all(fields[field] > 0 for field in ("entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL", "rampFrontM"))
    for item in parsons:
        if item["field"] in ("entryM", "heightM", "rampFrontM", "parameter RRE_ZZ_SP_Prototype-FFL") and item["value"] is not None:
            # A gap to the rule in metres, a site level lies over 100 m
            assert set(item["value"]) == {"gapToRuleM"} and abs(item["value"]["gapToRuleM"]) < 50.0
        if item["field"] == "steps":
            assert item["value"] in ("1", "2", "3")
