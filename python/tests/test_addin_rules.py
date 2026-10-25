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
    listed = majority.overrides(misses, {"overrideDecisions": ["hand", "rampSlope"], "groups": GROUPS}, {"imc": "IMC.rvt", "u12": "U12.rvt"})
    by_plot = {item["plot"]: item for item in listed}
    assert by_plot["2"]["source"] == "parsons" and by_plot["2"]["model"] == "IMC.rvt"
    assert by_plot["1"]["source"] == "native" and by_plot["1"]["model"] == "U12.rvt"
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
    assert {name: counts[name] for name in ("kept", "new", "droppedFromRun", "droppedNoJoin")} == {"kept": {"native": 1, "parsons": 1}, "new": {"parsons": 1},
                                                                                       "droppedFromRun": {"native": 1, "parsons": 1}, "droppedNoJoin": {}}
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
                                {"overrideDecisions": ["linkOrigin"], "groups": GROUPS}, {"imc": "IMC.rvt"})
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
    results, misses, _, extras = majority.run(items, {}, profile, block, [majority.ROAD], [], {"C10": {"anchor": "start", "offset": [6.0, 1.59]}})
    fits = extras["linkFits"]
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


# R333 the hands of the keys
ROW = {"a": {"left": [], "right": ["b"]}, "b": {"left": ["a"], "right": ["c"]}, "c": {"left": ["b"], "right": []},
       "d": {"left": [], "right": ["e"]}, "e": {"left": ["d"], "right": ["v"]}, "v": {"left": ["e"], "right": ["k"]}, "k": {"left": ["v"], "right": []},
       "f": {"left": [], "right": []}}
KINDS = {"a": "duplex", "b": "duplex", "c": "townhouse", "d": "duplex", "e": "duplex", "v": "villa", "k": "duplex", "f": "duplex"}


def test_r333_a_row_of_duplex_units_in_no_cluster_pairs_from_its_right_end_and_a_unit_alone_in_its_row_has_no_side():
    found = key_rules.row_pairs(list(ROW), ROW, KINDS.get, lambda plot: plot == "k", ("duplex", "townhouse"))
    assert {plot: found[plot]["side"] for plot in ("a", "b", "c", "d", "e", "f")} == {"a": "right", "b": "left", "c": "right", "d": "left", "e": "right", "f": None}
    # The odd row a b c pairs b with c from its right end, so a is the right unit of a pair it does not have, and length tells it
    assert found["a"]["length"] == 3 and found["a"]["right"] == 2
    assert "v" not in found and "k" not in found


def test_r333_a_villa_between_cluster_units_counts_the_sides_that_hold_one():
    in_cluster = lambda plot: plot in ("e", "k")
    assert key_rules.between_clusters(ROW["v"], in_cluster) == "both"
    assert key_rules.between_clusters(ROW["d"], in_cluster) == "one"
    assert key_rules.between_clusters(ROW["f"], in_cluster) == "none"


def test_r333_the_hand_of_a_unit_alone_is_learned_per_file_by_the_side_of_its_row_pair():
    items = [{"plot": str(n), "key": "imx", "group": "parsons", "code": "DP2", "file": "DP2.rvt", "row": {"mirrored": n % 2 == 1},
              "factors": {"all": "all", "rowPairSide": "left" if n % 2 == 0 else "right"}} for n in range(8)]
    result, misses = majority.decide("parsons hand alone", items, lambda it: it["row"]["mirrored"], {"all": "all", "rowPairSide": "rowPairSide"},
                                     lambda it: (it["group"], it["file"]), 0.9)
    assert result["coded"] == "rowPairSide" and result["share"] == 1.0 and misses == []


# R334 the ramps and floors of the keys
def test_r334_the_ramp_outline_of_r127_holds_when_each_corner_lies_within_the_hold_and_a_miss_is_its_largest_corner_gap():
    profile = project_profile.ProjectProfile("p.json", {"units_mm": True, "ramp": {"front_setback": {"default": 5520}}})
    item = {"code": "DP2", "rev9": "DP2", "frame": FRAME, "row": {"rampCorners": json.dumps([[0, 0, 1], [16, 0, 1], [16, 5.52, 1.1], [0, 5.52, 1.1]])}}
    rule = key_rules.ramp_rule(profile)(item)
    target = key_rules.ramp_target(item)
    assert key_rules.corner_hold(0.010)(rule, target)
    moved = [(x, y + 0.02 if y else y) for x, y in target]
    assert not key_rules.corner_hold(0.010)(rule, moved)
    assert key_rules.corner_gap(item, moved, rule) == {"gapToRuleM": 0.02}
    assert key_rules.ramp_target({"row": {"rampCorners": ""}}) is None


def test_r334_a_yard_outline_holds_by_the_distance_of_its_boundary_to_the_fitted_shape():
    from shapely.geometry import box
    hold = key_rules.shape_hold(0.010)
    assert hold(box(0, 0, 10, 5), box(0, 0, 10, 5.005))
    assert not hold(box(0, 0, 10, 5), box(0, 0, 10, 5.05))
    assert key_rules.shape_gap({}, box(0, 0, 10, 5.05), box(0, 0, 10, 5)) == {"gapToRuleM": 0.05}


def test_r334_the_yard_depth_is_the_key_generic_top_less_the_yard_top_and_a_zone_with_no_yard_is_learned_by_zone():
    rows = [{"genericTop": 10.0, "ffl": 10.0, "yardTop": 9.9}, {"genericTop": 10.153, "ffl": 10.0, "yardTop": 10.053}, {"genericTop": 10.0, "ffl": 10.0, "yardTop": None}]
    assert [majority.yard_depth(row) for row in rows] == ["0.100", "0.100", "noYard"]
    assert majority.yard_depth({"genericTop": None, "ffl": 10.0, "yardTop": 9.9}) == "0.100"
    assert majority.yard_depth({"genericTop": None, "ffl": None, "yardTop": 9.9}) is None


def test_r334_the_flat_ramps_are_counted_by_what_they_might_share():
    items = [{"plot": str(n), "key": "imx", "code": "DP2" if n else "VL4", "row": {"rampType": "Flat Ramps" if n < 2 else "Ramps 0.5%"},
              "factors": {"measuredBand": "0.5", "steps": "1", "corner": "no"}} for n in range(4)]
    found = key_rules.flat_ramps(items, "flat", lambda item: {"Flat Ramps": "flat"}.get(item["row"]["rampType"], "0.5"))
    assert found["plots"] == 2 and found["byCode"] == {"DP2": 1, "VL4": 1} and found["byMeasuredBand"] == {"0.5": 2} and found["plotsByKey"] == {"imx": ["0", "1"]}


def test_r334_the_plots_whose_el_at_entry_parameter_the_key_plan_refused_are_found_by_key_and_plot():
    plans = {"imx": {"missing": [{"plot": "1", "missing": [{"key": "levels.entry", "why": "refused, the ELl@Entry value puts the floor top 9 m over it"}]},
                                 {"plot": "2", "missing": [{"key": "levels.ssl", "why": "no SSL"}]}]}, "imy": {"missing": []}}
    assert key_rules.entry_refused(plans) == {("imx", "1")}


def test_r333_a_plot_with_no_front_frame_has_no_open_end_since_no_neighbour_can_be_put_left_or_right():
    from shapely.geometry import box
    faces = {str(n): box(n * 10, 0, n * 10 + 10, 25) for n in range(3)}
    frames = {"0": {"r": (1.0, 0.0), "n": (0.0, 1.0)}, "2": {"r": (1.0, 0.0), "n": (0.0, 1.0)}}
    found = majority.side_neighbours(list(faces), faces, frames, 0.5, 25.0)
    assert found["1"] == {"left": [], "right": [], "framed": False}
    block = majority.load_block(project_profile.load_settings())
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    rows = [{"plot": plot, "key": "imc", "typologyRev9": "C10", "keyUnit": "C10", "linkFile": "a.rvt", "steps": "1", "mirrored": True} for plot in ("0", "1")]
    items = majority.contexts(rows, {}, {}, faces, {}, found, {}, profile, block)
    assert [item["factors"]["openEnd"] for item in items] == ["left", None]


def test_r333_a_face_of_two_plan_numbers_has_no_kind_so_it_pairs_with_no_unit():
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = majority.load_block(settings)
    book = {"2005": {"typologyRev9": "DP2"}, "2007|2006": {"typologyRev9": "TH3|TH3"}}
    assert majority.pair_kind("2005", book, profile, block) == "duplex"
    assert majority.pair_kind("2007|2006", book, profile, block) is None


def test_r333_r334_the_rules_profile_holds_the_hand_ramp_and_floor_rules_of_the_keys_with_the_yard_per_zone():
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    rules = real.get("levelProfiles.parsons2024Rev6.keyRules")
    for word in ("hand villa", "hand cluster", "hand alone"):
        assert rules[word]["strong"] is True and all(isinstance(value, bool) for table in rules[word]["byFile"].values() for value in table.values())
    assert rules["hand alone"]["rule"] == "rowPairSideOrBlockMirrorOpenEnd"
    assert rules["yard"]["rule"] == "zone" and rules["yard"]["default"] == "0.100" and "noYard" in rules["yard"]["table"].values()
    assert rules["rampSlope"]["flatRamps"]["plots"] == rules["rampSlope"]["flatRamps"]["withElAtEntryRefused"]
    for word in ("rampOutline", "yardOutline", "rampFront", "rampFrontFall", "generic"):
        assert 0 < rules[word]["share"] <= 1


# R335 the walls and the shared parameters of the keys
WALL_TYPES = {"boundary": "^BW$", "garden": "Garden"}


def wall(element, plots, base, height, line=0, start=(0.0, 0.0), end=(0.0, 25.0), kind="BW"):
    return {"elementId": element, "type": kind, "lengthM": 25.0, "heightM": height, "start": [start[0], start[1], 0], "end": [end[0], end[1], 0], "plots": plots,
            "baseConstraint": "Level 1", "baseOffset": base, "topConstraint": "Unconnected", "topOffset": 0, "locationLine": line}


def plan_of(walls):
    return {"plots": [{"plot": "1", "unit": {"value": "DP2"}, "levels": {"ffl": {"value": 10.0}}}, {"plot": "2", "unit": {"value": "DP2"}, "levels": {"ffl": {"value": 10.3}}}],
            "walls": walls}


def test_r335_each_boundary_wall_of_a_key_plan_is_an_item_with_the_ffl_of_its_plots_and_a_garden_wall_is_counted_apart():
    plans = {"imc": plan_of([wall(1, ["1", "2"], 9.9, 1.8), wall(2, ["1"], 9.9, 1.8, kind="Generic Wall-150mm_ Garden"), wall(3, [], 9.9, 1.8)])}
    items, counts = key_rules.wall_items(plans, ["imc"], "parsons", WALL_TYPES)
    assert [item["wall"]["elementId"] for item in items] == [1]
    assert items[0]["ffls"] == [10.0, 10.3] and items[0]["plot"] == "1" and items[0]["code"] == "DP2"
    assert counts == {"imc": {"boundary on 2 plots": 1, "garden on 1 plot": 1, "boundary on 0 plots": 1}}
    assert key_rules.wall_base(items[0]) == pytest.approx(9.9) and key_rules.wall_top(items[0]) == pytest.approx(11.7)


def test_r335_the_wall_base_sits_at_the_yard_level_of_the_lower_plot_and_the_top_at_ffl_plus_the_rev6_height():
    profile = project_profile.ProjectProfile("p.json", {"units_mm": True, "rev6": {"boundary_wall_top_above_ffl": 1700}})
    item = {"ffls": [10.0, 10.3], "wall": wall(1, ["1", "2"], 9.9, 2.1)}
    base = key_rules.wall_base_candidates(0.1)
    assert base["yardLevelOfTheLowerPlot"](item) == pytest.approx(9.9) and base["yardLevelOfTheHigherPlot"](item) == pytest.approx(10.2)
    top = key_rules.wall_top_candidates(profile)
    assert top["higherFflPlusRev6"](item) == pytest.approx(12.0) and top["lowerFflPlusRev6"](item) == pytest.approx(11.7)
    assert key_rules.wall_top(item) == pytest.approx(12.0)


def test_r335_the_wall_line_is_held_by_the_distance_of_its_middle_to_the_plot_edge_against_the_centre_on_the_edge():
    from shapely.geometry import box
    faces = {"1": box(0, 0, 10, 25)}
    centre = {"wall": wall(1, ["1"], 9.9, 1.8, line=0, start=(0.0, 0.0), end=(0.0, 25.0))}
    face = {"wall": wall(2, ["1"], 9.9, 1.8, line=5, start=(0.1, 0.0), end=(0.1, 25.0))}
    assert key_rules.wall_line_target(faces)(centre) == pytest.approx(0.0) and key_rules.wall_line_target(faces)(face) == pytest.approx(0.1)
    rule = key_rules.wall_line_rule([0, 1], 0.2)
    assert rule(centre) == 0.0 and rule(face) == pytest.approx(0.1)


def test_r335_the_front_cover_is_the_share_of_the_front_edge_a_boundary_wall_runs_along():
    walls = [wall(1, ["1"], 9.9, 1.8, start=(0.0, 0.0), end=(1.0, 0.0)), wall(2, ["1"], 9.9, 1.8, start=(0.0, 0.0), end=(0.0, 25.0))]
    assert key_rules.front_cover(FRAME, walls, 0.3) == pytest.approx(1.0 / 16.0)
    assert key_rules.front_cover(None, walls, 0.3) is None


def test_r335_the_parameters_of_a_read_are_found_by_the_plot_number_parameter_and_the_name_is_made_of_its_parts():
    read = {"links": [{"parameters": {"RRE_ZZ_SP_Plot-Number": "12", "RRE_ZZ_SP_Prototype-FFL": "175.400"}}, {"parameters": {"RRE_ZZ_SP_Plot-Number": ""}}]}
    assert key_rules.read_parameters({"imc": read}) == {("imc", "12"): {"RRE_ZZ_SP_Plot-Number": "12", "RRE_ZZ_SP_Prototype-FFL": "175.400"}}
    item = {"plot": "12", "rev9": "DP2", "factors": {"steps": "2"}, "row": {"parameters": json.dumps({"RRE_ZZ_SP_Façade-Color": "XX", "RRE_ZZ_SP_Façade-Design-Option": "YY"})}}
    formulas = key_rules.parameter_formulas(["RRE_ZZ_SP_Façade-Color", "RRE_ZZ_SP_Façade-Design-Option"])
    assert formulas["plot"](item) == "12" and formulas["rev9Code"](item) == "DP2" and formulas["plotAndCode"](item) == "12 | DP2"
    assert formulas["plotCodeStepsAndFacade"](item) == "12 | DP2 | 2 | XX | YY"


def test_r335_a_level_parameter_that_a_link_holds_as_text_reads_as_a_number():
    assert key_rules.number("175.400") == pytest.approx(175.4) and key_rules.number("XX") is None and key_rules.number(None) is None


def test_r335_a_kind_may_leave_one_parameter_decision_by_its_whole_name_and_no_other():
    block = {"kindLeaveOut": {"SE": ["entry", "parameter RRE_ZZ_SP_RVT-Name"]}}
    pool = [{"modelKind": "SE"}, {"modelKind": "TU"}]
    assert majority.kept_for("parameter", pool, block, "parameter RRE_ZZ_SP_RVT-Name") == [{"modelKind": "TU"}]
    assert majority.kept_for("parameter", pool, block, "parameter CASE") == pool
    assert majority.kept_for("entry", pool, block, "entry") == [{"modelKind": "TU"}]


def test_r335_the_rules_profile_holds_the_wall_and_parameter_rules_of_the_keys():
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    rules = real.get("levelProfiles.parsons2024Rev6.keyRules")
    assert rules["wallBase"]["rule"] == "yardLevelOfTheHigherPlot" and rules["wallLine"]["rule"] == "centreOnTheEdge" and rules["frontOpen"]["rule"] == "all"
    assert rules["wallTop"]["strong"] is False
    assert rules["parameter RRE_ZZ_SP_Plot-Number"]["rule"] == "plot" and rules["parameter RRE_ZZ_SP_RVT-Name"]["rule"] == "plotCodeStepsAndFacade"
    assert rules["parameter RRE_ZZ_SP_RVT-Name"]["byKind"] == {"TU": 1791}
    for word, found in rules.items():
        if word.startswith("parameter "):
            assert 0 <= found["share"] <= 1 and set(found["byKey"]) <= {"ima", "imb", "imc", "imd", "ime", "imf", "img"}
