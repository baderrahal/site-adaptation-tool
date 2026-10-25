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
def test_r330_the_config_keeps_every_native_entry_and_the_level_fields_left_to_3c_so_a_plain_run_keeps_them():
    keep = override_source.keep_of(keep_block(), [], [], [])
    assert keep == {"native": None, "parsons": {"entryM", "heightM"}}
    real = override_source.keep_of(majority.load_block(project_profile.load_settings()), [], [], [])
    assert real == {"native": None, "parsons": {"entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"}}
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
