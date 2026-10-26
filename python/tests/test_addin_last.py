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
    road = lambda surfaces, point, snap, reach=None: (None, None) if point[0] > 36.0 - 1e-6 else (10.0, "road")
    from test_addin_review import LATER_3A
    plan, rules, _ = key_build(tmp_path, monkeypatch, overrides=overrides, road=road, later=LATER_3A)
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


# R330 the hand edits of Bader, read from the list a run writes over and refused in any other spelling
def test_r330_a_by_hand_entry_of_the_list_a_keep_from_run_writes_over_is_kept_or_the_run_refused(tmp_path):
    import json
    import pytest
    import majority
    import project_profile
    from test_addin_review import entry, keep_block
    committed, older = tmp_path / "committed.json", tmp_path / "older.json"
    hand = entry("10", "mirrored", True, "parsons", "IMC.rvt", "imc", byHand=True)
    committed.write_text(json.dumps({"note": "n", "overrides": [hand]}), encoding="utf-8")
    older.write_text(json.dumps({"note": "n", "overrides": []}), encoding="utf-8")
    run = [entry("10", "mirrored", False, "parsons", "IMC.rvt", "imc")]
    # mirrored is not kept, the hand edit of the committed list stays all the same, one by one (Round ADDIN 4b review)
    listing, _ = majority.final_list(run, keep_block(), dry=False, path=committed, keep=[], replace=[], keep_from=older, joined=None)
    assert listing == [hand]
    # Kept by its field it stays cell for cell, though the --keep-from list never held it
    listing, _ = majority.final_list(run, keep_block(), dry=False, path=committed, keep=["parsons:mirrored"], replace=[], keep_from=older, joined=None)
    assert listing == [hand]
    # The --keep-from list holding the same plot and field otherwise is a conflict, never resolved in silence
    older.write_text(json.dumps({"note": "n", "overrides": [entry("10", "mirrored", False, "parsons", "IMC.rvt", "imc")]}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError) as error:
        majority.final_list(run, keep_block(), dry=False, path=committed, keep=["parsons:mirrored"], replace=[], keep_from=older, joined=None)
    assert "hand edit" in str(error.value)


def test_r330_a_by_hand_mark_of_another_spelling_or_value_is_refused_loudly_and_never_dropped():
    import pytest
    import override_source
    import project_profile
    from test_addin_review import GROUPS, entry
    for extra in ({"byHand": "true"}, {"byHand": 1}, {"byHand": "byhand"}, {"byhand": True}, {"ByHand": True}, {"by_hand": True}, {"BYHAND": "yes"}):
        item = entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc", **extra)
        # Not kept by the run, a mark read as none would drop it in silence
        with pytest.raises(project_profile.ProfileError) as error:
            override_source.merge([item], [], {"native": None})
        assert "byHand" in str(error.value)
        with pytest.raises(project_profile.ProfileError):
            override_source.check_entries([item], GROUPS)
    assert override_source.by_hand(entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc", byHand=True)) is True
    assert override_source.by_hand(entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc")) is False


# R336 the merged list keeps every kept entry of the earlier list cell for cell, and a failed check fails the run
def test_r336_the_merged_list_is_held_against_the_earlier_list_and_a_list_that_lost_every_native_entry_fails():
    import key_rules_check
    table = [{"key": "u12", "plot": "1"}, {"key": "u12", "plot": "2"}, {"key": "imc", "plot": "2300"}]
    native = [{"plot": plot, "field": "entryM", "value": {"gapToRuleM": 0.2}, "source": "native", "key": "u12"} for plot in ("1", "2")]
    hand = {"plot": "2300", "field": "mirrored", "value": True, "source": "parsons", "key": "imc", "byHand": True}
    keep = {"native": None, "parsons": {"mirrored"}}
    found = key_rules_check.merged_check(native + [hand], [], table, keep, set(), earlier=native + [hand])
    assert found["ok"] is True and found["keptMissing"] == 0 and found["byHandLost"] == 0
    found = key_rules_check.merged_check([hand], [], table, keep, set(), earlier=native + [hand])
    assert found["ok"] is False and found["keptMissing"] == 2 and found["keptMissingBySource"] == {"native": 2}
    # A kept entry changed in one cell is no kept entry
    changed = [dict(native[0], value={"gapToRuleM": 0.3}), native[1]]
    found = key_rules_check.merged_check(changed, [], table, {"native": None}, set(), earlier=native)
    assert found["ok"] is False and found["keptMissing"] == 1
    found = key_rules_check.merged_check(native, [], table, keep, set(), earlier=native + [hand])
    assert found["ok"] is False and found["byHandLost"] == 1


# R338 the keyRules block written from a majority report, the zone prefix and the zone taken from another from config
PROFILE_TEXT = '{\n  "levelProfiles": {\n    "p1": {\n      "job": "parsons",\n      "yardBelowFflM": 0.1\n    },\n    "p2": {\n      "job": "native"\n    }\n  }\n}\n'


def made_report():
    def decision(group, word, coded, rule, byKey):
        return {"decision": group + " " + word, "coded": coded, "share": 1.0, "strong": True, "plots": 4, "misses": 0,
                "tests": {coded: {"holds": 4, "breaks": 0, "cannot": 0, "share": 1.0, "byKey": byKey, "byCode": {"DP2": {"holds": 4}}, "rule": rule},
                          "key": {"holds": 4, "share": 1.0, "checkOnly": True, "byKey": byKey}}}
    yard = decision("parsons", "yard", "zone", {"parsons | 2": "0.100", "parsons | 3": "0.100", "parsons | 4": "noYard"}, {"imx": {"holds": 4}})
    native = decision("native", "yard", "all", {"native | all": "0.100"}, {"u1": {"holds": 2, "breaks": 2}})
    native["share"] = 0.5
    hand = decision("parsons", "hand villa", "blockMirrorRoadSide", {"('parsons', 'a.rvt') | no none": True, "('parsons', 'a.rvt') | yes none": False}, {"imx": {"holds": 4}})
    hand["byBuild"] = {"holds": 3, "breaks": 1, "cannot": 0, "share": 0.75}
    return {"decisions": [yard, native, hand]}


def block_config(**change):
    import majority
    import project_profile
    block = dict(majority.load_block(project_profile.load_settings()))
    made = {"levelProfile": "p1", "keyRulesNote": "made up", "nativeRound17": {"yard": 1.0}, "zoneFrom": {"Zone 1": {"from": ["Zone 2", "Zone 3"], "why": "made up why"}}}
    block["keyRulesBlock"] = dict(block["keyRulesBlock"], **dict(made, **change))
    return block


def test_r338_the_key_rules_block_takes_the_zone_prefix_and_the_zone_taken_from_another_from_config_and_a_rerun_gives_it_byte_for_byte(tmp_path):
    import json
    import key_rules_block
    path = tmp_path / "profile.json"
    path.write_bytes(PROFILE_TEXT.encode("utf-8"))
    key_rules_block.write(path, made_report(), block_config(), ["yard", "hand villa"])
    first = path.read_bytes()
    data = json.loads(first)
    found = data["levelProfiles"]["p1"]["keyRules"]
    assert list(found) == ["note", "yard", "hand villa"] and found["note"] == "made up"
    assert found["yard"]["table"] == {"Zone 1": "0.100", "Zone 2": "0.100", "Zone 3": "0.100", "Zone 4": "noYard"}
    assert found["yard"]["notMeasured"] == {"Zone 1": "made up why"} and found["yard"]["native"] == 0.5 and found["yard"]["nativeRound17"] == 1.0
    assert found["hand villa"]["byFile"] == {"a.rvt": {"no none": True, "yes none": False}}
    # The share a build reproduces, by the table of the file it links (R337)
    assert found["hand villa"]["byBuild"] == {"holds": 3, "breaks": 1, "cannot": 0, "share": 0.75}
    # The rest of the profile keeps its bytes, p2 holds no keyRules
    assert data["levelProfiles"]["p2"] == {"job": "native"} and first.decode("utf-8").startswith('{\n  "levelProfiles": {\n    "p1": {\n      "job": "parsons",\n      "yardBelowFflM": 0.1,\n')
    # A rerun of every decision the block holds gives it byte for byte
    key_rules_block.write(path, made_report(), block_config(), None)
    assert path.read_bytes() == first


def test_r338_a_zone_taken_from_zones_that_differ_or_are_missing_is_refused_never_written():
    import pytest
    import key_rules_block
    import project_profile
    report = made_report()
    report["decisions"][0]["tests"]["zone"]["rule"] = {"parsons | 2": "0.100", "parsons | 3": "0.200", "parsons | 4": "noYard"}
    with pytest.raises(project_profile.ProfileError) as error:
        key_rules_block.block_of(report, ["yard"], {}, block_config())
    assert "Zone 1" in str(error.value) and "Zone 3" in str(error.value)
    with pytest.raises(project_profile.ProfileError):
        key_rules_block.block_of(made_report(), ["yard"], {}, block_config(zoneFrom={"Zone 1": {"from": ["Zone 9"], "why": "w"}}))


# R337, R272 a hand learned per file the key links is judged by the table of the file a build links
def hand_items():
    """Villas of a made up group: four link the ARC file a build links, three an STR file a build does not link, two of them the hand
    the STR table learns and the ARC table does not, one a block mirror flag the ARC table never learned."""
    found = []
    for plot, (file, block, mirrored) in enumerate([("arc.rvt", "yes", True)] * 3 + [("arc.rvt", "no", False), ("str.rvt", "yes", False), ("str.rvt", "yes", False),
                                                    ("str.rvt", "maybe", True)]):
        found.append({"plot": str(plot + 1), "key": "imx", "group": "parsons", "code": "VD2", "file": file, "buildFile": "arc.rvt",
                      "factors": {"blockMirror": block}, "row": {"mirrored": mirrored}})
    return found


def test_r337_a_hand_is_coded_by_the_tables_of_the_files_the_keys_link_and_its_misses_are_the_plots_a_build_does_not_reproduce():
    import majority
    by_file = lambda item: (item["group"], item["file"])
    built = lambda item: (item["group"], item.get("buildFile") or item["file"])
    result, misses = majority.decide("parsons hand villa", hand_items(), lambda item: item["row"]["mirrored"], {"all": "all", "blockMirror": "blockMirror"},
                                     by_file, 0.9, judge_scope=built)
    # The coded candidate and its share are measured on the file each key links, as before
    assert result["coded"] == "blockMirror" and result["share"] == 1.0
    # A build links the ARC file, so the two STR hands it gives otherwise and the one it gives none are its misses
    assert result["byBuild"] == {"holds": 4, "breaks": 2, "cannot": 1, "share": round(4 / 7, 4)}
    assert sorted(miss["item"]["plot"] for miss in misses) == ["5", "6", "7"] and result["misses"] == 3
    # With no judge scope the measure is the one of the review
    result, misses = majority.decide("parsons hand villa", hand_items(), lambda item: item["row"]["mirrored"], {"blockMirror": "blockMirror"}, by_file, 0.9)
    assert misses == [] and "byBuild" not in result


def test_r272_a_miss_of_a_plot_and_field_an_open_question_decides_is_listed_apart_and_never_written_as_an_override():
    import majority
    block = {"decidedByQuestion": {"note": "n", "Q017": {"fields": ["mirrored"], "plots": {"imx": ["2"]}, "why": "made up"}}}
    misses = [{"decision": "parsons hand villa", "field": "mirrored", "plot": plot, "key": "imx", "value": True, "rule": False, "state": "breaks", "reason": "r"}
              for plot in ("1", "2")] + [{"decision": "parsons linkOrigin", "field": "insert", "plot": "2", "key": "imx", "value": None, "rule": None,
                                          "state": "cannot", "reason": "r"}]
    kept, decided = majority.decided_by_question(misses, block)
    assert [(miss["plot"], miss["field"]) for miss in kept] == [("1", "mirrored"), ("2", "insert")]
    assert decided == [{"question": "Q017", "key": "imx", "plot": "2", "field": "mirrored", "decision": "parsons hand villa", "value": True, "rule": False}]


# R336 the second method holds a hand by the table of the file a build links, found by its own route
def test_r336_the_own_route_finds_the_file_a_build_links_and_counts_a_plot_a_question_decides_as_a_first_method_miss():
    import key_rules_check
    names = ["RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt", "RRE-WP-NSP-KEO-ARC-VD4-ZZ-MOD-2400.rvt",
             "RRE-WP-NSP-KEO-STR-DP2-ZZ-MOD-2200.rvt", "notes.txt"]
    found = key_rules_check.own_build_files(names, {"prefix": "RRE-WP-NSP-KEO-", "discipline": "ARC", "suffix": "-ZZ-MOD-"},
                                            {"VD1": {"keo_file": "DP1"}, "TH1-IM": {"keo_file": "TH1"}, "VD4": {"keo_file": "DP4"}, "DP2": {"keo_file": "DP2"}})
    assert found == {"DP1": names[0], "TH1": names[1], "VD4": names[2], "VD1": names[0], "TH1-IM": names[1]}
    dry = [{"decision": "parsons hand villa", "key": "imx", "plot": "1"}]
    report = {"decidedByQuestion": [{"question": "Q017", "key": "imx", "plot": "2", "field": "mirrored", "decision": "parsons hand villa"}]}
    assert key_rules_check.first_misses(dry, report) == {"parsons hand villa": {("imx", "1"), ("imx", "2")}}


# R336 the second method holds a hand edit kept one by one, with no entry of the run on its plot and field (Round ADDIN 4b review)
def test_r336_the_merged_list_holds_a_hand_edit_one_by_one_in_place_of_the_entry_of_the_run():
    import key_rules_check
    table = [{"key": "imc", "plot": "2300"}, {"key": "imc", "plot": "2301"}]
    hand = {"plot": "2300", "field": "heightM", "value": {"gapToRuleM": 0.002}, "source": "parsons", "key": "imc", "use": "rule", "byHand": True}
    other = {"plot": "2301", "field": "heightM", "value": {"gapToRuleM": 0.004}, "source": "parsons", "key": "imc"}
    dry = [{"plot": "2300", "field": "heightM", "value": {"gapToRuleM": 0.001}, "source": "parsons", "key": "imc"},
           {"plot": "2301", "field": "heightM", "value": {"gapToRuleM": 0.003}, "source": "parsons", "key": "imc"}]
    found = key_rules_check.merged_check([hand, dry[1]], dry, table, {"native": None}, set(), earlier=[hand, other])
    assert found["ok"] is True and found["runEntriesEqualDry"] is True and found["handEdits"] == 1 and found["byHandLost"] == 0
    # The entry of the run written over the hand edit fails, and so does the earlier entry kept where the run measured it again
    found = key_rules_check.merged_check(dry, dry, table, {"native": None}, set(), earlier=[hand, other])
    assert found["ok"] is False and found["byHandLost"] == 1
    found = key_rules_check.merged_check([hand, other], dry, table, {"native": None}, set(), earlier=[hand, other])
    assert found["ok"] is False and found["runEntriesEqualDry"] is False
