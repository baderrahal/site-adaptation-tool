"""Round ADDIN PR 3a, review of PR 47: the override list a run writes (R330), the build by the rules of the keys (R337), the
duplicates of a build (R280), the coverage of every decision (R272), the walls with no base level (R335) and the Round 17 check
(R281). Every name and number here is made up (R10)."""
import json
from pathlib import Path

import pytest

import majority
import override_source
import plan_rules
import project_profile

ROOT = Path(__file__).resolve().parents[2]
GROUPS = {"native": ["u11", "u12"], "parsons": ["imc", "imd"]}


def entry(plot, field, value, source, model, key, **extra):
    return {"plot": plot, "field": field, "value": value, "rule": None, "source": source, "model": model, "key": key, "decision": "d", "reason": "r", **extra}


def keep_block(sources=("native",), later=None):
    return {"groups": GROUPS, "overrideKeep": {"note": "n", "sources": list(sources), "laterPhase": "3c",
                                               "laterFields": later if later is not None else {"parsons": ["entryM", "heightM"]}}}


# R330 a key with no model file
def test_r330_a_key_with_no_model_file_is_refused_never_written_with_its_key_name_as_model():
    misses = [{"decision": "parsons hand villa", "field": "mirrored", "plot": "2", "key": "imc", "code": "VD2", "value": True, "rule": False,
               "state": "breaks", "reason": "breaks blockMirror"}]
    with pytest.raises(project_profile.ProfileError) as error:
        majority.overrides(misses, {"overrideDecisions": ["hand"], "groups": GROUPS}, {})
    assert "imc" in str(error.value) and "--model" in str(error.value)
    listed = majority.overrides(misses, {"overrideDecisions": ["hand"], "groups": GROUPS}, {"imc": "IMC.rvt"})
    assert listed[0]["model"] == "IMC.rvt"


# R330 --keep names only what the earlier list holds
def test_r330_keep_with_a_field_or_a_source_no_entry_of_the_earlier_list_has_is_refused():
    earlier = [entry("2", "entryM", {"gapToRuleM": 0.02}, "parsons", "IMC.rvt", "imc")]
    assert override_source.parse_keep(["parsons:entryM"], GROUPS, earlier) == {"parsons": {"entryM"}}
    for wrong in (["parsons:steps"], ["parsons:entryM,steps"], ["native"]):
        with pytest.raises(project_profile.ProfileError) as error:
            override_source.parse_keep(wrong, GROUPS, earlier)
        assert "no entry" in str(error.value)


# R330 the plot of a kept entry must be one the join gives its key
def test_r330_a_kept_entry_on_a_plot_the_join_gives_no_row_of_its_key_is_dropped_and_counted():
    earlier = [entry("2312", "entryM", {"gapToRuleM": 0.02}, "parsons", "IMC.rvt", "imc"), entry("2300", "entryM", {"gapToRuleM": 0.01}, "parsons", "IMC.rvt", "imc")]
    keep = {"parsons": {"entryM"}}
    merged, counts = override_source.merge(earlier, [], keep, joined={("imc", "2300")})
    assert merged == [earlier[1]]
    assert counts["droppedNoJoin"] == {"parsons": 1} and counts["kept"] == {"parsons": 1}


# R330 the fields left to a later phase and the sources kept come from config, so the plain command gives the committed list
def test_r330_the_config_keeps_every_native_entry_and_since_3c_no_level_field_so_a_plain_run_writes_the_levels():
    keep = override_source.keep_of(keep_block(), [], [], [])
    assert keep == {"native": None, "parsons": {"entryM", "heightM"}}
    # Round ADDIN 3c measured the levels (R339), so the config leaves no field to a later phase
    real = override_source.keep_of(majority.load_block(project_profile.load_settings()), [], [], [])
    assert real == {"native": None}
    earlier = [entry("1", "entryM", {"gapToRuleM": 0.5}, "native", "U12.rvt", "u12"), entry("2", "heightM", {"gapToRuleM": 0.03}, "parsons", "IMC.rvt", "imc"),
               entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc")]
    run = [entry("1", "entryM", {"gapToRuleM": 0.9}, "native", "U12.rvt", "u12"), entry("2", "heightM", {"gapToRuleM": 0.9}, "parsons", "IMC.rvt", "imc"),
           entry("3", "mirrored", False, "parsons", "IMD.rvt", "imd")]
    merged, _ = override_source.merge(earlier, run, keep)
    assert merged == [earlier[0], earlier[1], run[2]]


@pytest.mark.parametrize("bad", [{"sources": ["other"]}, {"laterFields": {"other": ["entryM"]}}, {"laterFields": {"parsons": []}}, {"laterFields": {"parsons": [""]}},
                                 {"laterPhase": ""}, {"sources": "native"}])
def test_r330_an_override_keep_block_that_names_no_job_or_an_empty_field_is_refused(bad):
    block = keep_block()
    block["overrideKeep"] = dict(block["overrideKeep"], **bad)
    with pytest.raises(project_profile.ProfileError):
        override_source.keep_of(block, [], [], [])


# R330 native entries stay unless a run names them, an entry Bader edited by hand is never dropped
def test_r330_native_entries_stay_unless_replace_names_them_and_replace_of_what_is_not_kept_is_refused():
    earlier = [entry("1", "entryM", {"gapToRuleM": 0.5}, "native", "U12.rvt", "u12")]
    run = [entry("1", "entryM", {"gapToRuleM": 0.9}, "native", "U12.rvt", "u12")]
    keep = override_source.keep_of(keep_block(), [], ["native"], earlier)
    assert "native" not in keep
    merged, counts = override_source.merge(earlier, run, keep)
    assert merged == run and counts["new"] == {"native": 1}
    assert override_source.keep_of(keep_block(), [], ["parsons:entryM"], earlier) == {"native": None, "parsons": {"heightM"}}
    for wrong in (["parsons:mirrored"], ["native:entryM"], ["other"]):
        with pytest.raises(project_profile.ProfileError):
            override_source.keep_of(keep_block(), [], wrong, earlier)


def test_r330_an_entry_marked_by_hand_is_never_dropped_and_a_run_that_would_drop_one_refuses():
    hand = entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc", byHand=True)
    run = [entry("2", "mirrored", False, "parsons", "IMC.rvt", "imc")]
    with pytest.raises(project_profile.ProfileError) as error:
        override_source.merge([hand], run, {"native": None})
    assert "byHand" in str(error.value) and "2" in str(error.value)
    # Kept by its field it stays, cell for cell
    merged, _ = override_source.merge([hand], run, {"parsons": {"mirrored"}})
    assert merged == [hand]
    # Off the join it would be dropped, so the run refuses
    with pytest.raises(project_profile.ProfileError):
        override_source.merge([hand], [], {"parsons": {"mirrored"}}, joined={("imc", "3")})


@pytest.mark.parametrize("mark", [False, "yes", 1, None])
def test_r330_by_hand_is_true_or_left_out(mark):
    with pytest.raises(project_profile.ProfileError):
        override_source.check_entries([entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc", byHand=mark)], GROUPS)
    assert override_source.check_entries([entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc", byHand=True)], GROUPS)


def test_r330_a_dry_run_writes_the_list_of_the_run_alone_and_never_into_the_repo(tmp_path):
    run = [entry("1", "entryM", {"gapToRuleM": 0.9}, "native", "U12.rvt", "u12")]
    block = keep_block()
    listing, merged = majority.final_list(run, block, dry=True, path=tmp_path / "dry.json", keep=[], replace=[], keep_from=None, joined=None)
    assert listing == run and merged is None
    with pytest.raises(project_profile.ProfileError):
        majority.final_list(run, block, dry=True, path=ROOT / "config" / "overrides" / "dry.json", keep=[], replace=[], keep_from=None, joined=None)


def test_r330_a_run_merges_with_the_earlier_list_by_the_config_keep_and_drops_what_the_join_does_not_give(tmp_path):
    path = tmp_path / "list.json"
    earlier = [entry("1", "entryM", {"gapToRuleM": 0.5}, "native", "U12.rvt", "u12"), entry("2312", "entryM", {"gapToRuleM": 0.02}, "parsons", "IMC.rvt", "imc"),
               entry("2300", "mirrored", True, "parsons", "IMC.rvt", "imc")]
    path.write_text(json.dumps({"note": "n", "overrides": earlier}), encoding="utf-8")
    run = [entry("2300", "mirrored", False, "parsons", "IMC.rvt", "imc")]
    listing, merged = majority.final_list(run, keep_block(), dry=False, path=path, keep=[], replace=[], keep_from=None,
                                          joined={("u12", "1"), ("imc", "2300")})
    assert listing == [earlier[0], run[0]]
    assert merged["droppedNoJoin"] == {"parsons": 1} and merged["kept"] == {"native": 1}


# R272 the coverage of every decision and the keys of answerKeys a run must have
FRAME = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0), "length": 16.0}


def link_items(keys=("imc",)):
    items = []
    for number, key in enumerate(keys * 5):
        item = {"plot": str(number + 1), "key": key, "group": "parsons", "code": "C10", "factors": {"all": "all", "typology": "C10", "steps": None}, "frame": FRAME,
                "curbFrame": FRAME, "frontRule": "nearestCurb", "kind": "villa", "inCluster": False, "role": None, "neighbours": {"left": [], "right": []},
                "modelKind": "TU", "rev9": "C10", "file": "a.rvt",
                "row": {"x": 6.0, "y": 1.59, "rotationDeg": 0.0, "mirrored": False, "rampType": None, "rampSlopePercent": None, "entry": None, "entrySource": None,
                        "rampBack": None, "rampFront": None, "rampFrontFall": None, "ffl": None, "height": None, "yardTop": None, "genericTop": None,
                        "parameters": "{}", "steps": None}}
        items.append(item)
    return items


def test_r272_every_decision_reports_its_coverage_by_key_and_a_decision_with_no_plot_is_listed_with_none():
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = majority.load_block(settings)
    _, _, _, extras = majority.run(link_items(("imc", "imd")), {}, profile, block, [majority.ROAD], [], {"C10": {"anchor": "start", "offset": [6.0, 1.59]}})
    coverage = extras["coverage"]
    assert coverage["parsons linkOrigin"]["plots"] == 10 and coverage["parsons linkOrigin"]["byKey"] == {"imc": 5, "imd": 5}
    assert "imc" not in coverage["parsons linkOrigin"]["keysWithNoPlot"] and "ima" in coverage["parsons linkOrigin"]["keysWithNoPlot"]
    # No item has a ramp, so the ramp outline covers no plot and says so, never dropped in silence
    assert coverage["parsons rampOutline"] == {"plots": 0, "byKey": {}, "keysWithNoPlot": sorted(block["groups"]["parsons"])}


def test_r272_a_key_of_answer_keys_missing_from_the_rows_the_reads_or_the_plans_is_refused_unless_a_flag_names_it():
    answer = ["ima", "imc"]
    groups = {"parsons": ["imc", "ima"], "native": ["u14"]}
    references = {"parsons": "imc", "native": "u14"}
    majority.check_keys(answer, {"ima", "imc", "u14"}, {"ima", "imc", "u14"}, {"ima", "imc"}, groups, references, [])
    for rows, reads, plans in (({"imc", "u14"}, {"ima", "imc"}, {"ima", "imc"}), ({"ima", "imc"}, {"imc"}, {"ima", "imc"}), ({"ima", "imc"}, {"ima", "imc"}, {"imc"})):
        with pytest.raises(project_profile.ProfileError) as error:
            majority.check_keys(answer, rows, reads, plans, groups, references, [])
        assert "ima" in str(error.value) and "--allow-missing-key" in str(error.value)
        majority.check_keys(answer, rows, reads, plans, groups, references, ["ima"])
    # A group read without its reference key is refused as well
    with pytest.raises(project_profile.ProfileError):
        majority.check_keys(answer, {"ima", "imc"}, {"ima", "imc", "u12"}, {"ima", "imc"}, groups | {"native": ["u12", "u14"]}, references, [])


def test_r331_a_group_whose_reference_read_is_missing_is_listed_never_left_out_in_silence():
    import key_rules
    doc = {"surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "angleToTrueNorthDeg": 0.0}
    found = key_rules.group_site_facts({"imd": doc}, {"parsons": ["imc", "imd"]}, {"parsons": "imc"})
    assert found["parsons"] == {"reference": "imc", "missingReference": True, "keys": ["imd"]}


# R335 a wall whose base level no floor names, the plan of a key that is missing
def test_r335_a_wall_whose_base_level_no_floor_of_its_plan_names_has_no_base_and_is_listed():
    import key_rules
    plan = {"plots": [{"plot": "1", "unit": {"value": "DP2"}, "levels": {"ffl": {"value": 10.0}},
                       "floors": {"generic": {"level": {"value": "Level 1"}, "levelElevation": {"value": 0.0}}}}],
            "walls": [{"elementId": 1, "type": "BW", "plots": ["1"], "baseConstraint": "Level 1", "baseOffset": 9.9, "heightM": 1.8, "locationLine": 0},
                      {"elementId": 2, "type": "BW", "plots": ["1"], "baseConstraint": "Level 9", "baseOffset": 9.9, "heightM": 1.8, "locationLine": 0}]}
    items, counts = key_rules.wall_items({"imc": plan}, ["imc", "imd"], "parsons", {"boundary": "^BW$"})
    by_id = {item["wall"]["elementId"]: item for item in items}
    assert key_rules.wall_base(by_id[1]) == pytest.approx(9.9) and by_id[2]["baseLevel"] is None
    assert key_rules.wall_base(by_id[2]) is None and key_rules.wall_top(by_id[2]) is None
    assert key_rules.no_base_level(items) == [{"key": "imc", "elementId": 2, "baseConstraint": "Level 9"}]
    # The key with no plan is counted as none, never as a key with no wall
    assert counts["imd"] is None


def test_r335_the_link_plot_parameter_and_the_kinds_with_no_front_wall_come_from_config():
    import key_rules
    block = majority.load_block(project_profile.load_settings())
    assert block["linkPlotParameter"] == "RRE_ZZ_SP_Plot-Number" and "frontOpen" in block["kindLeaveOut"]["SE"]
    read = {"links": [{"parameters": {"PLOT": "12", "X": "1"}}, {"parameters": {"RRE_ZZ_SP_Plot-Number": "13"}}]}
    assert key_rules.read_parameters({"imc": read}, "PLOT") == {("imc", "12"): {"PLOT": "12", "X": "1"}}


# R280 two overrides of one plot and field are refused, never the last one taken
def test_r280_two_overrides_of_one_plot_and_field_from_two_keys_or_one_are_refused_and_a_model_named_narrows_them(tmp_path):
    path = tmp_path / "o.json"
    entries = [entry("5", "mirrored", True, "parsons", "IMC.rvt", "imc"), entry("5", "mirrored", False, "parsons", "IMD.rvt", "imd"),
               entry("6", "mirrored", True, "parsons", "IMD.rvt", "imd")]
    path.write_text(json.dumps({"note": "n", "overrides": entries}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.load_overrides(path, {"parsons"}, groups=GROUPS)
    text = str(error.value)
    assert "5" in text and "imc" in text and "imd" in text and "--override-model" in text
    assert plan_rules.load_overrides(path, {"parsons"}, groups=GROUPS, models={"IMD.rvt"}) == {"5": {"mirrored": False}, "6": {"mirrored": True}}
    path.write_text(json.dumps({"note": "n", "overrides": [entries[0], dict(entries[0], value=False)]}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError):
        plan_rules.load_overrides(path, {"parsons"}, groups=GROUPS)


# R281 a decision with misses and no entry in the list is no match
def test_r281_a_decision_with_misses_and_no_entry_in_the_override_list_is_not_a_match():
    import r17_check
    report = {"decisions": [{"decision": "parsons hand villa", "plots": 10, "misses": 3, "coded": "c", "tests": {"c": {"holds": 7}}},
                            {"decision": "parsons rampSlope", "plots": 10, "misses": 0, "coded": "c", "tests": {"c": {"holds": 10}}},
                            {"decision": "parsons frontCorner", "plots": 10, "misses": 2, "coded": "c", "tests": {"c": {"holds": 8}}}]}
    found = {row["decision"]: row for row in r17_check.decision_counts(report, {"overrides": []}, ["hand", "rampSlope"])}
    assert found["parsons hand villa"]["match"] is False and found["parsons hand villa"]["overrides"] is False
    assert found["parsons rampSlope"]["match"] is True
    # A decision that writes no override holds when the list has none of it
    assert found["parsons frontCorner"]["match"] is True
    listed = {"overrides": [{"decision": "parsons hand villa"}] * 3 + [{"decision": "parsons frontCorner"}]}
    found = {row["decision"]: row for row in r17_check.decision_counts(report, listed, ["hand", "rampSlope"])}
    assert found["parsons hand villa"]["match"] is True and found["parsons frontCorner"]["match"] is False


# R337 a build by the rules of the keys
DP2 = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt"
VD2 = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt"
# The levels left to Phase 3c in the config of Round ADDIN 3a, a made up config since 3c measured them (R339)
LATER_3A = {"parsons": ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]}


def made_key_rules():
    """keyRules as Round ADDIN 3a writes them, made up, for a row of a VD2 villa and three DP2 units in no cluster."""
    yard = [[[1.0, 6.0], [9.0, 6.0], [9.0, 9.0], [1.0, 9.0]]]
    return {
        "linkRotation": {"rule": "frontRule"}, "frontCorner": {"rule": "frontRule"},
        "linkOrigin": {"rule": "plotFrameRefit", "byCode": {"DP2": {"anchor": "end", "offset": [-9.5, 0.2]}, "VD2": {"anchor": "end", "offset": [-10.0, 0.2]}}},
        "hand villa": {"rule": "blockMirrorRoadSide", "byFile": {VD2: {"no none": True, "yes none": False}}},
        "hand cluster": {"rule": "clusterSide", "byFile": {DP2: {"left": False, "right": True}}},
        "hand alone": {"rule": "rowPairSideOrBlockMirrorOpenEnd", "byFile": {DP2: {"pair left": False, "pair right": True}}},
        "rampSlope": {"rule": "typology", "table": {"DP2": "0.5", "VD2": "0.5"}},
        "rampOutline": {"rule": "outlineR127"}, "generic": {"rule": "all", "table": {"all": "wholeFace"}},
        "yard": {"rule": "zone", "table": {"Zone 1": "0.100", "Zone 2": "0.100", "Zone 4": "noYard"},
                 "notMeasured": {"Zone 1": "taken from the Zone 2 and 3 value pending Q066"}},
        "yardOutline": {"rule": "fittedOutlineRefit", "groups": [{"unit": unit, "mirrored": mirrored, "width": 12.0, "type": "yard", "loops": yard}
                                                                  for unit in ("DP2", "VD2") for mirrored in (True, False)]},
        "parameter RRE_ZZ_SP_Plot-Number": {"rule": "plot"},
        "parameter RRE_ZZ_SP_Prototype": {"rule": "typology", "table": {"DP2": "DP2", "VD2": "VD2"}},
        "parameter RRE_ZZ_SP_Number-of-Steps": {"rule": "steps", "table": {"1": "1", "2": "2", "3": "3"}},
        "parameter RRE_ZZ_SP_Façade-Color": {"rule": "all", "table": {"all": "XX"}},
        "parameter RRE_ZZ_SP_Façade-Design-Option": {"rule": "all", "table": {"all": "XX"}},
        "parameter CASE": {"rule": "typologyMirrored", "table": {"DP2 False": "F", "DP2 True": "E", "VD2 False": "F", "VD2 True": "E"}},
        "parameter RRE_ZZ_SP_RVT-Name": {"rule": "plotCodeStepsAndFacade"},
        "parameter RRE_ZZ_SP_Prototype-FFL": {"rule": "ffl"},
    }


def key_build(tmp_path, monkeypatch, zones=("Zone 2", "Zone 2", "Zone 2", "Zone 4"), overrides=None, road=None, later=None):
    """plan_from_book.rule_inputs and build on a row of four 12 m plots with one road in front, the road level stubbed, by road when given,
    with later as the fields majority.overrideKeep leaves to a later phase when given."""
    import argparse
    from shapely.geometry import LineString, box
    import plan_from_book
    import plan_from_past
    settings = project_profile.load_settings()
    if later is not None:
        values = json.loads(json.dumps(settings.values))
        values["majority"]["overrideKeep"]["laterFields"] = later
        settings = project_profile.ProjectProfile(settings.path, values)
    real = project_profile.load_profile(project_profile.profile_path(settings))
    values = json.loads(json.dumps(real.values))
    values["levelProfiles"]["parsons2024Rev6"]["keyRules"] = made_key_rules()
    profile = project_profile.ProjectProfile(real.path, values)
    numbers = ["1", "2", "3", "4"]
    units = {"1": "VD2", "2": "DP2", "3": "DP2", "4": "DP2"}
    rows = {number: {"locationPlanNumber": number, "typologyRev9": units[number], "footprintMirrored": "no", "zone": zone, "roadLevelLeft": "", "roadLevelRight": ""}
            for number, zone in zip(numbers, zones)}
    faces = {number: box(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0) for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)])]
    monkeypatch.setattr(plan_from_book, "road_level", road or (lambda surfaces, point, snap: (10.0, "road")))
    path = None
    if overrides is not None:
        path = tmp_path / "overrides.json"
        path.write_text(json.dumps({"note": "n", "overrides": overrides}), encoding="utf-8")
    args = argparse.Namespace(level_profile=None, overrides=None if path is None else str(path), override_source=None, override_model=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, roads, {}, [])
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
            "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((ROOT / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    plan = plan_from_book.build(numbers, rows, {}, faces, roads, [], profile, settings, {"units": {}}, {"DP2": DP2, "VD2": VD2}, "folder", site, [("road", None)],
                                outlines, "N1", {}, True, rules)
    return plan, rules, profile


def by_plot(plan):
    return {item["plot"]: item for item in plan["plots"]}


def test_r337_a_build_of_a_profile_with_key_rules_takes_the_hands_origin_ramp_yard_and_parameters_from_them(tmp_path, monkeypatch):
    import cad_links
    plan, rules, profile = key_build(tmp_path, monkeypatch, later=LATER_3A)
    found = by_plot(plan)
    # The villa by the block mirror flag and its side road, the three DP2 units in no cluster by the pairs of their row from its right end
    assert {plot: found[plot]["mirrored"]["value"] for plot in found} == {"1": True, "2": True, "3": False, "4": True}
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (24.0, 0.0), "right": (36.0, 0.0)}
    expected = cad_links.predicted_origin(frame, False, {"anchor": "end", "offset": [-9.5, 0.2]})
    assert found["3"]["insert"]["value"][:2] == pytest.approx(list(expected), abs=0.001)
    assert found["2"]["floors"]["ramp"]["type"]["value"] == "Ramps 0.5%"
    # The yard per zone: none in Zone 4
    assert "yard" in found["3"]["floors"] and "yard" not in found["4"]["floors"]
    assert found["3"]["floors"]["yard"]["z"]["value"] == pytest.approx(found["3"]["levels"]["ffl"]["value"] - 0.1, abs=0.0005)
    parameters = {name: value["value"] for name, value in found["1"]["parameters"].items()}
    steps = str(found["1"]["levels"]["steps"]["value"])
    assert parameters == {"RRE_ZZ_SP_Plot-Number": "1", "RRE_ZZ_SP_Prototype": "VD2", "RRE_ZZ_SP_Number-of-Steps": steps, "RRE_ZZ_SP_Façade-Color": "XX",
                          "RRE_ZZ_SP_Façade-Design-Option": "XX", "CASE": "E", "RRE_ZZ_SP_RVT-Name": "1 | VD2 | " + steps + " | XX | XX"}
    summary = plan["summary"]["rules"]
    assert summary["keyRules"] is True and summary["laterFields"] == ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]


def test_r337_the_overrides_of_a_key_rules_build_set_the_hand_and_parameters_and_move_the_origin_and_rotation_by_their_gaps(tmp_path, monkeypatch):
    import cad_links
    overrides = [entry("3", "mirrored", True, "parsons", "IMC.rvt", "imc"), entry("4", "insert", {"gapToRuleM": [0.5, -0.2]}, "parsons", "IMC.rvt", "imc"),
                 entry("2", "rotationDeg", {"gapToRuleDeg": 90.0}, "parsons", "IMC.rvt", "imc"), entry("2", "parameter CASE", "Z", "parsons", "IMC.rvt", "imc"),
                 entry("1", "yardOutline", {"gapToRuleM": 0.3}, "parsons", "IMC.rvt", "imc")]
    plain, _, _ = key_build(tmp_path, monkeypatch)
    plan, _, _ = key_build(tmp_path, monkeypatch, overrides=overrides)
    before, found = by_plot(plain), by_plot(plan)
    assert found["3"]["mirrored"]["value"] is True and found["3"]["mirrored"]["source"] == "override"
    assert found["2"]["parameters"]["CASE"]["value"] == "Z"
    assert found["2"]["rotationDeg"]["value"] == pytest.approx((before["2"]["rotationDeg"]["value"] + 90.0) % 360.0, abs=0.001)
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (36.0, 0.0), "right": (48.0, 0.0)}
    axis_x, axis_y = cad_links.link_axes(frame, True)
    moved = [before["4"]["insert"]["value"][index] + 0.5 * axis_x[index] - 0.2 * axis_y[index] for index in (0, 1)]
    assert found["4"]["insert"]["value"][:2] == pytest.approx(moved, abs=0.0011)
    # An outline gap can not be placed, it is counted, never applied in silence
    assert plan["summary"]["rules"]["overridesNotApplied"] == {"yardOutline": 1}


def test_r337_a_zone_the_yard_table_does_not_hold_is_refused_and_a_zone_taken_from_another_is_said_on_every_build(tmp_path, monkeypatch):
    with pytest.raises(project_profile.ProfileError) as error:
        key_build(tmp_path, monkeypatch, zones=("Zone 2", "Zone 3", "Zone 2", ""))
    assert "Zone 3" in str(error.value) and "yard" in str(error.value)
    plan, _, _ = key_build(tmp_path, monkeypatch, zones=("Zone 1", "Zone 1", "Zone 2", "Zone 2"))
    assert plan["summary"]["rules"]["yardNotMeasured"] == {"Zone 1": {"plots": 2, "why": "taken from the Zone 2 and 3 value pending Q066"}}


def test_r337_a_profile_with_no_key_rules_builds_by_the_round_17_tables_and_a_key_rule_a_build_cannot_give_is_refused():
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    assert plan_rules.key_rules_of(plan_rules.level_profile(real, "native2022")) is None
    rules = made_key_rules()
    assert plan_rules.key_hand(rules, "alone", DP2, {"rowPairSideOrBlockMirrorOpenEnd": "pair right"}) == (True, "rowPairSideOrBlockMirrorOpenEnd")
    assert plan_rules.key_hand(rules, "alone", "other.rvt", {"rowPairSideOrBlockMirrorOpenEnd": "pair right"}) == (None, "rowPairSideOrBlockMirrorOpenEnd")
    for word, bad in (("hand villa", {"rule": "clusterIndexParity", "byFile": {}}), ("rampOutline", {"rule": "other"}), ("yard", {"rule": "typology", "table": {}}),
                      ("linkOrigin", {"rule": "plotFrame"}), ("parameter CASE", {"rule": "corner", "table": {}})):
        with pytest.raises(project_profile.ProfileError):
            plan_rules.check_key_rules(dict(rules, **{word: bad}))


# R271 the context of the site as a build sees it
def test_r271_a_plot_no_key_holds_keeps_its_front_so_the_row_of_units_is_walked_through_it_as_a_build_walks_it():
    from shapely.geometry import LineString, box
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = majority.load_block(settings)
    numbers = ["1", "2", "3", "4"]
    book = {number: {"locationPlanNumber": number, "typologyRev9": "DP2", "footprintMirrored": "no", "baseplanLabel": "L" + number} for number in numbers}
    faces = {number: box(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0) for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)])]
    # Plot 3, a cut face, is no row of past_all.csv
    rows = [{"plot": plot, "key": "imc", "typologyRev9": "DP2", "keyUnit": "DP2", "linkFile": "a.rvt", "steps": "1", "mirrored": True} for plot in ("1", "2", "4")]
    items, clusters, front, table, _ = majority.site_context(rows, book, {"L" + number: number for number in numbers}, faces, roads, {}, [], {}, profile, settings, block)
    assert table == ["1", "2", "4"] and front["3"]["frame"] is not None and clusters == []
    assert {item["plot"]: item["factors"]["rowPairSide"] for item in items} == {"1": "left", "2": "right", "4": "right"}
