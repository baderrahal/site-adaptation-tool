"""Round ADDIN PR 3a: the source of every override (R330) and the rules on the Parsons keys (R331 to R336)."""
import json
from pathlib import Path

import pytest

import key_rules
import majority
import override_source
import plan_rules
import project_profile
from test_round_addin import run_book

ROOT = Path(__file__).resolve().parents[2]
GROUPS = {"native": ["u11", "u12"], "parsons": ["imc", "imd"]}


def real_groups():
    return project_profile.load_settings().get("majority.groups")


def write_list(path, entries):
    path.write_text(json.dumps({"note": "n", "overrides": entries}), encoding="utf-8")
    return path


def entry(plot, field, value, source, model, key):
    return {"plot": plot, "field": field, "value": value, "rule": None, "source": source, "model": model, "key": key, "decision": "d", "reason": "r"}


# R330 the source of every override
def test_r330_every_override_majority_writes_has_the_job_of_its_key_as_source_and_its_model_file_apart():
    misses = [{"decision": "parsons hand villa", "field": "mirrored", "plot": "2", "key": "imc", "code": "VD2", "value": True, "rule": False,
               "state": "breaks", "reason": "breaks blockMirror"},
              {"decision": "native rampSlope", "field": "rampType", "plot": "1", "key": "u12", "code": "DP2", "value": "flat", "rule": "2",
               "state": "breaks", "reason": "breaks all"}]
    listed = majority.overrides(misses, {"overrideDecisions": ["hand", "rampSlope"], "groups": GROUPS}, {"imc": "IMC.rvt"})
    by_plot = {item["plot"]: item for item in listed}
    assert by_plot["2"]["source"] == "parsons" and by_plot["2"]["model"] == "IMC.rvt"
    # A key with no model given keeps its key as the model, as R272 did
    assert by_plot["1"]["source"] == "native" and by_plot["1"]["model"] == "u12"
    assert list(by_plot["2"]) == ["plot", "field", "value", "rule", "source", "model", "key", "decision", "reason"]


def test_r330_an_entry_of_before_the_source_field_takes_the_job_of_its_key_and_its_model_file_moves_to_model():
    old = {"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.02}, "rule": None, "source": "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt", "key": "u12",
           "decision": "native entry", "reason": "r"}
    new = override_source.migrate_entry(old, GROUPS)
    assert new["source"] == "native" and new["model"] == "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"
    assert list(new) == ["plot", "field", "value", "rule", "source", "model", "key", "decision", "reason"]
    # An entry already in the new form passes as it is
    assert override_source.migrate_entry(new, GROUPS) == new


@pytest.mark.parametrize("bad", [
    {"plot": "1", "field": "f", "value": 1, "source": "a.rvt", "key": "zz"},
    {"plot": "1", "field": "f", "value": 1, "source": "native", "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "source": "other", "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "source": "a.rvt", "model": "b.rvt", "key": "imc"},
])
def test_r330_an_entry_whose_key_has_no_job_or_whose_source_is_not_the_job_of_its_key_is_refused(bad):
    with pytest.raises(project_profile.ProfileError):
        override_source.migrate_entry(bad, GROUPS)
    with pytest.raises(project_profile.ProfileError):
        override_source.check_entries([bad], GROUPS)


def test_r330_the_loader_refuses_a_list_with_an_entry_of_no_source(tmp_path):
    path = write_list(tmp_path / "o.json", [entry("1", "rampType", "flat", "parsons", "IMC.rvt", "imc"),
                                            {"plot": "2", "field": "rampType", "value": "flat", "source": "IMC.rvt", "key": "imc"}])
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.load_overrides(path, {"parsons"})
    assert "R330" in str(error.value) and "2" in str(error.value)


def test_r330_the_loader_takes_the_entries_of_the_sources_named_and_narrows_to_the_models_named(tmp_path):
    path = write_list(tmp_path / "o.json", [entry("1", "rampType", "flat", "parsons", "IMC.rvt", "imc"),
                                            entry("2", "mirrored", True, "parsons", "IMD.rvt", "imd"),
                                            entry("3", "entryM", {"gapToRuleM": 0.02}, "native", "U12.rvt", "u12")])
    assert plan_rules.load_overrides(path, {"parsons"}) == {"1": {"rampType": "flat"}, "2": {"mirrored": True}}
    assert plan_rules.load_overrides(path, {"parsons"}, models={"IMD.rvt"}) == {"2": {"mirrored": True}}
    assert plan_rules.load_overrides(path, {"native"}) == {"3": {"entryM": {"gapToRuleM": 0.02}}}
    # A model with no override of the sources named is refused, never applied as none
    with pytest.raises(project_profile.ProfileError):
        plan_rules.load_overrides(path, {"parsons"}, models={"U12.rvt"})


def new_list(tmp_path):
    """One override of each job in the form of R330, with the keys and models the real list uses."""
    return write_list(tmp_path / "overrides.json", [
        entry("1", "entryM", {"gapToRuleM": 0.02}, "native", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt", "u12"),
        entry("2", "rampType", "flat", "parsons", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt", "imc")])


def test_r330_a_build_with_overrides_and_no_source_named_applies_only_the_parsons_overrides_by_default(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path)])
    assert code == 0, capsys.readouterr().err
    assert seen["rules"]["overrides"] == {"2": {"rampType": "flat"}}
    assert seen["rules"]["sources"] == ["parsons"]


def test_r330_native_overrides_are_never_applied_by_default_a_native2022_build_must_name_their_source(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path)])
    err = capsys.readouterr().err
    assert code == 2 and "rules" not in seen
    assert "--override-source native" in err and "R330" in err
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path), "--override-source", "native"])
    assert code == 0 and seen["rules"]["overrides"] == {"1": {"entryM": {"gapToRuleM": 0.02}}}
    # Native overrides on the default parsons build are refused, gaps to the rule of another job
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path), "--override-source", "native"])
    assert code == 2 and "rules" not in seen


def test_r330_a_model_named_narrows_the_overrides_of_its_source_and_needs_the_list(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path), "--override-source", "native",
                                                  "--override-model", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"])
    assert code == 0 and seen["rules"]["overrides"] == {"1": {"entryM": {"gapToRuleM": 0.02}}}
    code, seen = run_book(tmp_path, monkeypatch, ["--override-model", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"])
    assert code == 2 and "--overrides" in capsys.readouterr().err


def test_r330_the_default_sources_of_the_config_are_jobs_of_majority_groups():
    settings = project_profile.load_settings()
    assert override_source.default_sources(settings) == ["parsons"]
    for wrong in ([], ["other"], "parsons", ["parsons", "parsons"]):
        values = dict(settings.values, majority=dict(settings.values["majority"], overrideDefaultSources=wrong))
        with pytest.raises(project_profile.ProfileError):
            override_source.default_sources(project_profile.ProjectProfile(settings.path, values))


def test_r330_majority_keeps_the_entries_of_a_kept_source_or_field_cell_for_cell_and_drops_the_run_entries_of_them():
    earlier = [entry("1", "entryM", {"gapToRuleM": 0.02}, "native", "U12.rvt", "u12"),
               entry("2", "entryM", {"gapToRuleM": 0.03}, "parsons", "IMC.rvt", "imc"),
               entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc")]
    run = [entry("1", "entryM", {"gapToRuleM": 0.5}, "native", "U12.rvt", "u12"),
           entry("2", "entryM", {"gapToRuleM": 0.9}, "parsons", "IMC.rvt", "imc"),
           entry("3", "mirrored", False, "parsons", "IMD.rvt", "imd")]
    keep = override_source.parse_keep(["native", "parsons:entryM,heightM"], GROUPS)
    merged, counts = override_source.merge(earlier, run, keep)
    assert merged[0] == earlier[0] and earlier[1] in merged and earlier[2] not in merged
    assert run[2] in merged and run[0] not in merged and run[1] not in merged
    assert counts == {"kept": {"native": 1, "parsons": 1}, "new": {"parsons": 1}, "droppedFromRun": {"native": 1, "parsons": 1}}
    for wrong in (["other"], ["parsons:"], ["parsons:entryM,"], ["native", "native"]):
        with pytest.raises(project_profile.ProfileError):
            override_source.parse_keep(wrong, GROUPS)


def test_r330_the_real_override_list_has_the_job_of_its_key_as_source_on_every_entry():
    data = json.loads(override_source.list_path(project_profile.load_settings()).read_text(encoding="utf-8"))
    entries = data["overrides"]
    override_source.check_entries(entries, real_groups())
    counts = override_source.counts(entries)
    assert set(counts["bySource"]) == {"parsons", "native"}
    assert sum(counts["bySource"].values()) == len(entries)


# R331 the coordinates of the keys, one set for button 1
def read_doc(dx=0.0, angle=0.0, level2=4.0, with_site=True):
    doc = {"file": "X", "surveyPoint": {"position": [1.0 + dx, 2.0, 3.0], "sharedPosition": [100.0 + dx, 200.0, 3.0]},
           "basePoint": {"position": [5.0, 6.0, 7.0], "sharedPosition": [500.0, 600.0, 7.0]}, "angleToTrueNorthDeg": angle}
    if with_site:
        doc["site"] = {"activeSite": "Internal", "sites": ["Internal"], "placeName": "P", "projectPosition": {"eastWest": 10.0 + dx, "northSouth": 20.0, "elevation": 0.0, "angleDeg": angle},
                       "levels": [{"name": "Level 1", "elementId": 1, "elevation": 0.0, "projectElevation": 0.0, "elevationBase": "Survey Point"},
                                  {"name": "Level 2", "elementId": 2, "elevation": level2, "projectElevation": level2, "elevationBase": "Survey Point"}],
                       "units": {"system": "METRIC", "length": "meters", "lengthSystem": "METRIC", "lengthAccuracy": 0.01}}
    return doc


def test_r331_keys_that_hold_one_survey_point_base_point_position_angle_and_levels_are_one_set():
    facts = key_rules.site_facts({"imc": read_doc(), "imd": read_doc()}, "imc")
    assert facts["oneSet"] is True and facts["keys"]["imd"]["differ"] == {}
    assert facts["shared"] == {"angleToTrueNorthDeg": 0.0, "activeSite": "Internal", "sites": ["Internal"], "levels": ["Level 1", "Level 2"],
                               "levelStepsM": {"Level 2": 4.0}, "units": {"system": "METRIC", "length": "meters", "lengthSystem": "METRIC"}}


def test_r331_a_key_off_by_two_mm_or_another_level_is_named_with_its_gap_and_the_set_is_not_one():
    facts = key_rules.site_facts({"imc": read_doc(), "imd": read_doc(dx=0.002), "ime": read_doc(level2=4.5)}, "imc")
    assert facts["oneSet"] is False
    assert facts["keys"]["imd"]["differ"]["surveyPoint.sharedPosition"] == pytest.approx(2.0)
    assert facts["keys"]["imd"]["differ"]["projectPosition"] == pytest.approx(2.0)
    assert facts["keys"]["ime"]["differ"]["levels.Level 2"] == pytest.approx(500.0)


def test_r331_each_group_is_held_against_its_own_reference_and_the_references_against_each_other():
    found = key_rules.group_site_facts({"imc": read_doc(), "imd": read_doc(), "u12": read_doc(dx=5.0, with_site=False), "u14": read_doc(dx=5.0, with_site=False)},
                                       GROUPS | {"native": ["u12", "u14"]}, {"parsons": "imc", "native": "u14"})
    assert found["parsons"]["oneSet"] is True and found["native"]["oneSet"] is True
    assert found["acrossGroups"]["oneSet"] is False and found["acrossGroups"]["keys"]["u14"]["differ"]["surveyPoint.position"] == pytest.approx(5000.0)


def test_r331_a_read_of_before_the_site_facts_is_held_on_its_points_and_angle_only_and_no_coordinate_is_written():
    facts = key_rules.site_facts({"imc": read_doc(), "u12": read_doc(with_site=False)}, "imc")
    assert facts["keys"]["u12"]["compared"] == ["surveyPoint.position", "surveyPoint.sharedPosition", "basePoint.position", "basePoint.sharedPosition", "angleToTrueNorthDeg"]
    assert facts["keys"]["u12"]["differ"] == {}
    text = json.dumps(facts)
    for value in ("100.0", "200.0", "500.0", "600.0"):
        assert value not in text


# R332 the link origin and rotation by the front frame
FRAME = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0), "length": 16.0}


def link_item(plot, x, y, rotation, mirrored, code="C10", frame=FRAME, curb=FRAME, rule="nearestCurb", group="parsons", key="imc"):
    return {"plot": plot, "key": key, "group": group, "code": code, "factors": {"all": "all"}, "frame": frame, "curbFrame": curb, "frontRule": rule,
            "row": {"x": x, "y": y, "rotationDeg": rotation, "mirrored": mirrored}}


def test_r332_the_link_rotation_is_held_against_the_front_frame_and_a_corner_plot_against_its_orientation_frame():
    side = {"n": (1.0, 0.0), "r": (0.0, -1.0), "left": (0.0, 25.0), "right": (0.0, 0.0), "length": 25.0}
    items = [link_item("1", 0, 0, 0.0, False), link_item("2", 0, 0, 0.05, False), link_item("3", 0, 0, 270.0, False, frame=side, rule="orientation"),
             link_item("4", 0, 0, 0.0, False, frame=side, rule="orientation")]
    hold = key_rules.angle_hold(0.1)
    result, misses = majority.decide("parsons linkRotation", items, lambda it: it["row"]["rotationDeg"], key_rules.rotation_candidates(), lambda it: it["group"], 0.9, hold)
    assert result["coded"] == "frontRule" and result["tests"]["frontRule"]["holds"] == 3 and result["tests"]["nearestCurb"]["holds"] == 3
    assert [miss["item"]["plot"] for miss in misses] == ["4"]
    assert key_rules.rotation_gap(misses[0]["item"], misses[0]["value"], misses[0]["rule"]) == {"gapToRuleDeg": 90.0}


def test_r332_the_origin_refit_takes_per_code_the_anchor_most_plots_of_the_group_hold_and_its_median_offset():
    wide = dict(FRAME, right=(18.0, 0.0), length=18.0)
    # The house of the code sits 10 m before the end corner, so a fit from the start corner holds on the 16 m plots only
    items = [link_item("1", 6.0, 1.59, 0.0, False), link_item("2", 6.0, 1.59, 0.0, False), link_item("3", 8.0, 1.59, 0.0, False, frame=wide)]
    fits = key_rules.refit(items)
    assert fits["C10"] == {"anchor": "end", "offset": [-10.0, 1.59], "plots": 3}
    as_fitted = {"C10": {"anchor": "start", "offset": [6.0, 1.59]}}
    result, misses = majority.decide("parsons linkOrigin", items, key_rules.origin_target, key_rules.origin_candidates(as_fitted, fits, {}),
                                     lambda it: it["group"], 0.9, key_rules.point_hold(0.010))
    assert result["coded"] == "plotFrameRefit" and result["tests"]["plotFrame"]["holds"] == 2 and result["tests"]["plotFrameRefit"]["holds"] == 3
    assert misses == []


def test_r332_an_origin_miss_goes_to_the_overrides_as_its_gap_in_the_link_axes_and_never_a_coordinate():
    item = link_item("1", 8.0, 1.6, 0.0, True)
    gap = key_rules.insert_gap(item, (8.0, 1.6), (6.0, 1.59))
    # Mirrored, the link X axis runs against r, so 2 m along r is -2 m along X
    assert gap == {"gapToRuleM": [-2.0, 0.01]}
    assert key_rules.insert_gap(item, (8.0, 1.6), None) is None
    listed = majority.overrides([{"decision": "parsons linkOrigin", "field": "insert", "plot": "1", "key": "imc", "code": "C10", "value": (8.0, 1.6),
                                  "rule": (6.0, 1.59), "state": "breaks", "reason": "breaks plotFrameRefit", "overrideValue": gap}],
                                {"overrideDecisions": ["linkOrigin"], "groups": GROUPS}, {})
    assert listed[0]["value"] == {"gapToRuleM": [-2.0, 0.01]} and listed[0]["rule"] is None


def test_r332_a_majority_run_holds_the_link_decisions_of_a_group_and_lists_each_miss_with_its_gap():
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = majority.load_block(settings)
    items = [link_item(str(plot), 6.0, 1.59, 0.0, False) for plot in range(1, 10)] + [link_item("10", 7.0, 1.59, 0.0, False), link_item("11", 6.0, 1.59, 90.0, False)]
    for item in items:
        item.update(kind="villa", inCluster=False, role=None, neighbours={"left": [], "right": []}, modelKind="TU", rev9="C10", file="a.rvt")
        item["factors"].update(typology="C10", steps=None)
        item["row"].update(rampType=None, rampSlopePercent=None, entry=None, entrySource=None, rampBack=None, rampFront=None, rampFrontFall=None, ffl=None,
                           height=None, yardTop=None, genericTop=None, parameters="{}", steps=None)
    results, misses, _, fits = majority.run(items, {}, profile, block, [majority.ROAD], [], {"C10": {"anchor": "start", "offset": [6.0, 1.59]}})
    names = {result["decision"]: result for result in results}
    assert names["parsons linkRotation"]["misses"] == 1 and names["parsons linkOrigin"]["misses"] == 1
    assert fits["parsons"]["C10"]["plots"] == 11
    by_field = {miss["field"]: miss for miss in misses}
    assert by_field["rotationDeg"]["overrideValue"] == {"gapToRuleDeg": 90.0} and by_field["insert"]["overrideValue"] == {"gapToRuleM": [1.0, 0.0]}


def test_r331_r332_the_rules_profile_holds_the_site_and_link_rules_of_the_keys_with_no_coordinate():
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    rules = real.get("levelProfiles.parsons2024Rev6.keyRules")
    assert rules["site"]["oneSet"] is True and rules["site"]["angleToTrueNorthDeg"] == 0 and rules["site"]["levels"] == ["Level 1", "Level 2"]
    for word in ("linkRotation", "frontCorner", "linkOrigin"):
        assert 0 < rules[word]["share"] <= 1 and set(rules[word]["byKey"]) == {"ima", "imb", "imc", "imd", "ime", "imf", "img"}
    for code, fit in rules["linkOrigin"]["byCode"].items():
        assert fit["anchor"] in ("start", "end") and len(fit["offset"]) == 2 and all(abs(value) < 30 for value in fit["offset"]), code
