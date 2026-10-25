import copy
import math

import pytest
from shapely.geometry import box

import plan_from_past

FLOORS = {"types": {"generic": ["FFL"], "yard": [], "ramp": ["Ramps"]}, "plotParameter": "Plot"}
D4 = "RRE-WP-NSP-KEO-ARC-D4-ZZ-MOD-2400.rvt"
T3 = "RRE-WP-NSP-KEO-ARC-T3-ZZ-MOD-3300.rvt"


def native_link(name, file, origin, rotation=0.0, mirrored=False):
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    return {"file": file, "parameters": {"Name": name},
            "transform": {"origin": list(origin), "rotationDeg": rotation, "mirrored": mirrored,
                          "axisX": [math.cos(turn), math.sin(turn), 0.0], "axisY": [-sign * math.sin(turn), sign * math.cos(turn), 0.0], "axisZ": [0, 0, 1]}}


def ffl(value, x0):
    return {"typeName": "FFL", "parameters": {"Plot": value}, "outline": [[[x0, 0], [x0 + 10, 0], [x0 + 10, 25], [x0, 25]]], "topElevationRange": {"min": 7.0, "max": 7.0}}


def native_model(links, floors):
    return {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "links": links, "floors": floors}]}


FACES = {"11": box(0, 0, 10, 25), "12": box(10, 0, 20, 25), "13": box(20, 0, 30, 25)}
NUMBERS = {"11": "501", "12": "502", "13": "503"}


def test_r264_a_link_whose_name_is_a_level_takes_the_plot_its_file_house_centre_stands_on():
    # The D4 house centre sits 5 m along X and 12.5 m along Y from its origin, learned on the link named by its plot
    links = [native_link("DP4-1", D4, (0, 0, 0)), native_link("EL@-Entry +164.98", D4, (10, 0, 0))]
    data = native_model(links, [ffl("DP4-1", 0), ffl("DP4-2", 10)])
    data, rows, report = plan_from_past.names_dialect(data, FLOORS, FACES, NUMBERS)
    plots = [link["parameters"].get(plan_from_past.PLOT_PARAMETER) for link in plan_from_past.host_of(data)["links"]]
    assert plots == ["501", "502"]
    assert report["link mapped"] == 1 and report["link mapped by frame"] == 1


def test_r264_the_digits_of_a_wrong_name_are_never_trusted():
    # The Name says plot 2 but the house stands on plot 3
    links = [native_link("DP4-1", D4, (0, 0, 0)), native_link("TH3-2", D4, (20, 0, 0))]
    data = native_model(links, [ffl("DP4-1", 0), ffl("DP4-2", 10), ffl("DP4-3", 20)])
    data, _, report = plan_from_past.names_dialect(data, FLOORS, FACES, NUMBERS)
    assert plan_from_past.host_of(data)["links"][1]["parameters"][plan_from_past.PLOT_PARAMETER] == "503"


def test_r264_a_mirrored_link_is_read_in_its_own_axes():
    # Mirrored, its Y axis points down, so the house centre 12.5 m along its Y lies below the origin
    links = [native_link("DP4-1", D4, (0, 0, 0)), native_link("EL@-Entry +1", D4, (10, 25, 0), mirrored=True)]
    data = native_model(links, [ffl("DP4-1", 0), ffl("DP4-2", 10)])
    data, _, _ = plan_from_past.names_dialect(data, FLOORS, FACES, NUMBERS)
    assert plan_from_past.host_of(data)["links"][1]["parameters"][plan_from_past.PLOT_PARAMETER] == "502"


def test_r264_a_file_with_no_named_link_takes_its_house_centre_from_another_model_of_the_job():
    other = native_model([native_link("TH3-9", T3, (0, 0, 0))], [ffl("TH3-9", 0)])
    frames = plan_from_past.model_frames(other, FLOORS)
    assert frames[T3] == pytest.approx((5.0, 12.5))
    data = native_model([native_link("EL@-Entry +2", T3, (10, 0, 0))], [ffl("TH3-2", 10)])
    unknown, _, report = plan_from_past.names_dialect(copy.deepcopy(data), FLOORS, FACES, NUMBERS)
    assert report["link with no mapped name"] == 1
    data, _, report = plan_from_past.names_dialect(data, FLOORS, FACES, NUMBERS, frames)
    assert plan_from_past.host_of(data)["links"][0]["parameters"][plan_from_past.PLOT_PARAMETER] == "502"


def test_r264_two_links_on_one_plot_are_both_refused():
    links = [native_link("DP4-1", D4, (0, 0, 0)), native_link("DP4-2", D4, (10, 0, 0)), native_link("EL@-Entry +3", D4, (10, 0, 0))]
    data = native_model(links, [ffl("DP4-1", 0), ffl("DP4-2", 10)])
    data, _, report = plan_from_past.names_dialect(data, FLOORS, FACES, NUMBERS)
    assert report["link refused, its plot claimed by two links"] == 2
    assert plan_from_past.PLOT_PARAMETER not in plan_from_past.host_of(data)["links"][1]["parameters"]


# R265 to R280 below. Dummy data built in code only (R10)

import json as _json

import entry_gaps
import majority
import past_all
import plan_rules
import project_profile

PROFILE = project_profile.ProjectProfile("dummy.json", {
    "units_mm": True,
    "typologies": {"VD2": {"kind": "villa", "keo_file": "DP2"}, "DP2": {"kind": "duplex", "keo_file": "DP2"}, "VD1": {"kind": "villa", "keo_file": "DP1"}},
    "steps": {"base_height": {"default": 100, "DP2": 200}, "riser_added": 170},
    "levels": {"ground_ssl": -75},
    "plot": {"long_side": 25000},
    "levelProfiles": {"note": "dummy", "rev6": {"frontRoad": {"surface": "roadLevel", "offsetM": 0.0}, "rampSlopePercent": {"default": 0.5, "VL4": 2.0},
                                                 "rampTypes": {"0.5": "Ramps 0.5%", "2": "Ramps 2%", "flat": "Flat Ramps"},
                                                 "height": "oneFflPerClusterAtTheHighestEntry", "stepBands": [[0.205, 1], [0.38, 2], [99.0, 3]], "yardBelowFflM": 0.1}},
})


def test_r268_the_date_of_a_link_folder_is_read_from_its_path_and_a_file_name_date_is_not():
    assert past_all.path_date("P:\\Site\\1. Villas Prototypes\\20221206 - Links\\RRE-D2.rvt") == "2022-12-06"
    assert past_all.path_date("P:\\Site\\Prototypes\\RRE-20221206.rvt") is None
    rows = past_all.inventory([{"file": "a.rvt", "info": {"formatYear": 2021, "workshared": True}, "storageTimes": {"firstWritten": "2022-11-10 06:40"},
                                "revitLinks": [{"file": "D2.rvt", "absolutePath": "P:\\x\\20221206 - Links\\D2.rvt"}, {"file": "C1.rvt", "path": "C1.rvt"}]}])
    assert rows[0]["linkFolderDates"] == {"2022-12-06": 1, "none": 1}


def test_r267_coverage_counts_each_neighbourhood_against_the_location_plan_and_the_plots_two_keys_share():
    labels = {str(n): {"nbh": "A" if n < 4 else "B", "zone": "1", "kind": "clientCode", "rev9": "DP2"} for n in range(1, 7)}
    plans = {"k1": {"plots": [{"plot": "1"}, {"plot": "2"}]}, "k2": {"plots": [{"plot": "2"}, {"plot": "4"}]}}
    found = past_all.coverage(plans, labels)
    assert found["byNeighbourhood"]["A"]["heldByAKey"] == 2 and found["byNeighbourhood"]["A"]["heldByNoKey"] == 1
    assert found["byNeighbourhood"]["B"]["heldByNoKey"] == 2
    assert found["sharedByKeys"] == {"k1+k2": 1}
    assert found["phase"] == {"unitPlots": 6, "heldByAKey": 3, "neighbourhoodsWithNoKeyPlot": []}


def test_r269_the_ramp_front_is_the_edge_on_the_face_boundary_also_when_the_ramp_falls_to_the_house():
    face = box(0, 0, 10, 25)
    # The front edge at y 0 sits lower than the back at y 5.52, then a ramp that falls toward the house
    rising = [[0, 0, 10.0], [10, 0, 10.02], [10, 5.52, 10.1], [0, 5.52, 10.1]]
    falling = [[0, 0, 10.2], [10, 0, 10.2], [10, 5.52, 10.1], [0, 5.52, 10.1]]
    assert past_all.ramp_ends(rising, face)[:2] == pytest.approx((10.01, 10.1))
    front, back, fall = past_all.ramp_ends(falling, face)
    assert front == pytest.approx(10.2) and back == pytest.approx(10.1) and fall == pytest.approx(0.0)


def test_r269_a_native_key_row_takes_its_levels_off_the_datum_and_el_at_entry_from_its_floor_parameter(tmp_path):
    datum = 100.0
    plan = {"plots": [{"plot": "501", "unit": {"value": "DP4"}, "prototypeFile": {"value": D4}, "insert": {"value": [1.0, 2.0]}, "rotationDeg": {"value": 90.0},
                       "mirrored": {"value": True}, "case": {"value": "a"}, "linkName": {"value": "DP4-1"}, "levels": {"ffl": {"value": datum + 7.3}},
                       "floors": {"generic": {"type": {"value": "FFL"}, "outline": {"value": {"loops": [{"points": [[0, 0], [10, 0], [10, 25], [0, 25]], "arcs": []}]}},
                                              "z": {"value": datum + 7.3}}}}]}
    data = native_model([native_link("DP4-1", D4, (0, 0, 0))], [dict(ffl("DP4-1", 0), parameters={"Plot": "DP4-1", "ELl@Entry": 7.1})])
    data["documents"][0]["surveyPoint"]["sharedPosition"] = [0, 0, datum]
    dialect = {"plot": "name", "datum": "surveyElevation", "floorTypes": {"generic": ["FFL"], "ramp": ["Ramps"]}}
    floors = dict(FLOORS, levels={}, yardBelowFflM=0.1, yardShareTie=0.01, yardMinShare=0.5)
    block = {"linkParameters": ["CASE"], "floorParameters": ["ELl@Entry"], "entryParameter": "ELl@Entry"}
    rows = past_all.table_rows("u14", plan, data, dialect, floors, FACES, NUMBERS, {"501": {"nbh": "012", "zone": "2", "rev9": "DP4"}}, block)
    assert rows[0]["ffl"] == pytest.approx(7.3) and rows[0]["entry"] == pytest.approx(7.1) and rows[0]["height"] == pytest.approx(0.2)
    assert rows[0]["entrySource"] == "floorParameter" and rows[0]["datumM"] == datum and rows[0]["nbh"] == "012"
    past_all.write_csv(tmp_path / "past_all.csv", rows)
    back = past_all.read_table(tmp_path / "past_all.csv")
    assert back[0]["mirrored"] is True and back[0]["ffl"] == pytest.approx(7.3)


def gap_row(key, plot, entry, back, outline):
    return {"key": key, "plot": plot, "entry": entry, "rampBack": back, "genericOutline": outline, "typologyRev9": "TH3"}


def test_r270_a_key_parameter_off_its_own_ramp_is_a_parameter_fault_and_a_floor_on_another_face_a_wrong_join():
    faces = {"11": box(0, 0, 10, 25), "12": box(10, 0, 20, 25)}
    numbers = {"11": "501", "12": "502"}
    block = {"entryGapM": 0.5, "entryRampM": 0.3, "sameFaceShare": 0.9}
    rows = [gap_row("imc", "501", 10.0, 10.0, box(0, 0, 10, 25).wkt), gap_row("u14", "501", 11.2, 10.15, box(0, 0, 10, 25).wkt),
            gap_row("imc", "502", 10.0, 10.0, box(10, 0, 20, 25).wkt), gap_row("u14", "502", 9.0, 9.0, box(0, 0, 10, 25).wkt)]
    result = entry_gaps.check_pair(rows, "imc", "u14", faces, numbers, {}, block)
    verdicts = {found["plot"]: found["verdict"] for found in result["plots"]}
    assert verdicts == {"501": "keyParameterFault", "502": "wrongJoin"}


def test_r270_two_keys_on_the_same_face_with_their_ramps_apart_differ_for_real():
    faces, numbers = {"11": box(0, 0, 10, 25)}, {"11": "501"}
    rows = [gap_row("imc", "501", 10.0, 10.0, box(0, 0, 10, 25).wkt), gap_row("u14", "501", 9.3, 9.3, box(0, 0, 10, 25).wkt)]
    result = entry_gaps.check_pair(rows, "imc", "u14", faces, numbers, {}, {"entryGapM": 0.5, "entryRampM": 0.3, "sameFaceShare": 0.9})
    assert result["plots"][0]["verdict"] == "realLevelDifference"


def item(plot, key, value, factors, code="VD2", group="g"):
    return {"plot": plot, "key": key, "group": group, "code": code, "factors": dict(factors, all="all"), "value": value}


def test_r272_a_factor_learns_the_value_most_plots_take_per_factor_value_and_counts_holds_breaks_and_cannot():
    items = [item(str(n), "k", n % 2 == 0, {"blockMirror": "yes" if n % 2 == 0 else "no"}) for n in range(10)]
    items[3]["value"] = True
    items.append(item("99", "k", False, {"blockMirror": None}))
    result, misses = majority.decide("g hand", items, lambda it: it["value"], {"all": "all", "blockMirror": "blockMirror"}, lambda it: it["group"], 0.9)
    assert result["coded"] == "blockMirror"
    assert result["tests"]["blockMirror"]["holds"] == 9 and result["tests"]["blockMirror"]["breaks"] == 1 and result["tests"]["blockMirror"]["cannot"] == 1
    assert result["share"] == pytest.approx(round(9 / 11, 4)) and result["strong"] is False
    assert sorted(miss["item"]["plot"] for miss in misses) == ["3", "99"]


def test_r272_a_check_candidate_that_reads_a_value_of_the_key_is_reported_and_never_coded():
    items = [item(str(n), "k", 1.0, {}) for n in range(4)]
    result, _ = majority.decide("g height", items, lambda it: it["value"], {"all": "all", "keySteps": lambda it: 1.0}, lambda it: it["group"], 0.9,
                                checks=("keySteps",))
    assert result["coded"] == "all" and result["tests"]["keySteps"]["checkOnly"] is True


def test_r272_a_formula_is_judged_within_the_level_hold_and_a_level_miss_goes_to_the_overrides_as_its_gap():
    items = [item("1", "imc", 10.000, {}), item("2", "imc", 10.020, {})]
    hold = lambda guess, aim: abs(guess - aim) <= 0.005
    result, _ = majority.decide("parsons entry", items, lambda it: it["value"], {"road": lambda it: 10.0}, lambda it: it["group"], 0.9, hold)
    assert result["tests"]["road"]["holds"] == 1
    listed = majority.overrides([{"decision": "parsons entry", "field": "entryM", "plot": "2", "key": "imc", "code": "VD2", "value": 10.02, "rule": 10.0,
                                  "state": "breaks", "reason": "breaks road"}], {"overrideDecisions": ["entry"]}, {"imc": "IMC.rvt"})
    assert listed[0]["value"] == {"gapToRuleM": 0.02} and listed[0]["source"] == "IMC.rvt" and listed[0]["rule"] is None


def test_r272_the_override_list_is_written_one_override_per_line(tmp_path):
    path = tmp_path / "overrides.json"
    majority.write_overrides(path, "note", [{"plot": "1", "field": "mirrored", "value": True}, {"plot": "2", "field": "rampType", "value": "flat"}])
    lines = path.read_text(encoding="utf-8").splitlines()
    assert _json.loads(path.read_text(encoding="utf-8"))["overrides"][1]["value"] == "flat"
    assert sum(1 for line in lines if line.strip().startswith('{"plot"')) == 2


def test_r271_side_neighbours_lie_left_or_right_seen_from_the_road_and_the_row_place_counts_from_the_left_end():
    faces = {str(n): box(n * 10, 0, n * 10 + 10, 25) for n in range(3)}
    frames = {plot: {"r": (1.0, 0.0), "n": (0.0, 1.0)} for plot in faces}
    found = majority.side_neighbours(list(faces), faces, frames, 0.5, 25.0)
    assert found["1"] == {"left": ["0"], "right": ["2"]}
    assert majority.row_place("2", found) == (2, 3)
    assert majority.band(0.3, [[0.205, "1"], [0.38, "2"], [99.0, "3"]]) == "2"


def test_r273_the_slope_class_comes_from_the_type_name_and_else_from_the_measured_slope():
    block = {"rampTypeClass": {"Ramps 2%": "2"}, "slopeBands": [[0.25, "flat"], [1.0, "0.5"], [99.0, "2"]]}
    assert majority.slope_class({"row": {"rampType": "Ramps 2%", "rampSlopePercent": 0.4}}, block) == "2"
    assert majority.slope_class({"row": {"rampType": "Ramps", "rampSlopePercent": 0.4}}, block) == "0.5"
    assert majority.slope_class({"row": {"rampType": "Ramps", "rampSlopePercent": 0.1}}, block) == "flat"


def test_r274_a_villa_is_a_villa_and_a_duplex_is_a_cluster_unit_or_a_unit_alone():
    assert majority.hand_class({"kind": "villa", "inCluster": True}) == "villa"
    assert majority.hand_class({"kind": "duplex", "inCluster": True}) == "cluster"
    assert majority.hand_class({"kind": "duplex", "inCluster": False}) == "alone"


def test_r275_one_ffl_per_cluster_sits_at_the_highest_el_at_entry_plus_the_base_of_that_unit():
    pool = [{"key": "k", "code": "DP2", "row": {"entry": 10.0, "entrySource": "schedule", "rampBack": None}, "role": {"plots": ["1", "2"]}},
            {"key": "k", "code": "VD1", "row": {"entry": 10.15, "entrySource": "schedule", "rampBack": None}, "role": {"plots": ["1", "2"]}}]
    tops = majority.cluster_top(pool, PROFILE, {"entryRampM": 0.3})
    assert sorted(tops.values()) == pytest.approx([0.1, 0.25])


def test_r277_a_profile_with_no_default_refuses_a_build_that_names_none_and_an_unknown_name():
    with pytest.raises(project_profile.ProfileError):
        plan_rules.level_profile(PROFILE, None)
    with pytest.raises(project_profile.ProfileError):
        plan_rules.level_profile(PROFILE, "note")
    assert plan_rules.level_profile(PROFILE, "rev6")["height"] == "oneFflPerClusterAtTheHighestEntry"


def test_r277_the_real_rules_profile_holds_both_generations_and_each_is_complete():
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    names = set(real.get("levelProfiles")) - set(plan_rules.NOT_PROFILES)
    assert {"parsons2024Rev6", "native2022"} <= names
    for name in names:
        assert plan_rules.level_profile(real, name)["rampTypes"]


def test_r278_el_at_entry_is_the_front_road_plus_the_offset_plus_the_slope_times_the_depth_and_the_cluster_shares_one_ffl():
    lp = plan_rules.level_profile(PROFILE, "rev6")
    entry, slope = plan_rules.entry_of([10.0, 10.02], "DP2", lp, 5.52)
    assert slope == 0.5 and entry == pytest.approx(10.01 + 0.0276)
    rows = plan_rules.heights({"1": {"unit": "DP2", "entry": 10.0}, "2": {"unit": "VD1", "entry": 10.15}, "3": {"unit": "VD1", "entry": 9.0}},
                              [{"plots": ["1", "2"]}], lp, PROFILE)
    assert rows["1"]["ffl"] == rows["2"]["ffl"] == pytest.approx(10.25)
    assert rows["1"]["steps"] == 2 and rows["2"]["steps"] == 1 and rows["1"]["ssl"] == pytest.approx(10.175)
    assert rows["3"]["ffl"] == pytest.approx(9.1) and rows["3"]["steps"] == 1


def test_r279_a_villa_takes_its_hand_from_the_block_mirror_and_open_end_then_the_block_mirror_and_a_cluster_unit_keeps_r123():
    hands = {"villa": {"byCode": {"VD2": {"blockMirrorOpenEnd": {"no right": False}, "blockMirror": {"no": True, "yes": False}}, "DP1": {"blockMirror": {"no": True}}}},
             "alone": {"byCode": {"DP2": {"blockMirror": {"yes": False}}}}}
    assert plan_rules.hand_of("VD2", "villa", None, "no", "right", hands, None) == (False, "blockMirrorOpenEnd")
    assert plan_rules.hand_of("VD2", "villa", None, "no", "left", hands, None) == (True, "blockMirror")
    assert plan_rules.hand_of("VD1", "villa", None, "no", "left", hands, None, "DP1") == (True, "blockMirror")
    assert plan_rules.hand_of("DP2", "duplex", {"side": "left"}, "yes", "left", hands, True) == (True, "clusterSide")
    assert plan_rules.hand_of("DP2", "duplex", None, "yes", "left", hands, None) == (False, "blockMirror")
    assert plan_rules.hand_of("DP2", "duplex", None, None, None, hands, None) == (None, None)


def test_r280_only_the_overrides_of_the_source_model_apply_and_a_level_override_is_its_gap(tmp_path):
    path = tmp_path / "overrides.json"
    path.write_text(_json.dumps({"note": "n", "overrides": [
        {"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.02}, "source": "IMC.rvt"},
        {"plot": "1", "field": "mirrored", "value": True, "source": "NATIVE.rvt"},
        {"plot": "2", "field": "rampType", "value": "flat", "source": "IMC.rvt"},
        {"plot": "2", "field": "parameter CASE", "value": "F", "source": "IMC.rvt"}]}), encoding="utf-8")
    found = plan_rules.load_overrides(path, {"IMC.rvt"})
    assert found == {"1": {"entryM": {"gapToRuleM": 0.02}}, "2": {"rampType": "flat"}}
    assert plan_rules.gap_of(found["1"]["entryM"]) == 0.02 and plan_rules.gap_of(None) is None
    assert plan_rules.slope_class(0.5) == "0.5" and plan_rules.slope_class(0) == "flat"


def test_r276_a_parameter_is_read_from_the_row_and_an_empty_value_is_none():
    row = {"row": {"parameters": _json.dumps({"CASE": "F", "RRE_ZZ_SP_Number-of-Steps": "2", "RRE_ZZ_SP_RVT-Name": ""})}}
    assert majority.parameter(row, "CASE") == "F"
    assert majority.parameter(row, "RRE_ZZ_SP_Number-of-Steps") == "2"
    assert majority.parameter(row, "RRE_ZZ_SP_RVT-Name") is None and majority.parameter(row, "missing") is None
    assert majority.base_height(PROFILE, "DP2") == pytest.approx(0.2) and majority.base_height(PROFILE, "VL4") == pytest.approx(0.1)


def test_r281_the_second_method_has_its_own_ray_cast_shoelace_outline_text_and_bands():
    import r17_check
    ring = [(0, 0), (10, 0), (10, 25), (0, 25)]
    assert r17_check.inside((5, 5), ring) and not r17_check.inside((11, 5), ring)
    assert r17_check.shoelace(ring) == pytest.approx(250.0)
    assert r17_check.wkt_ring(box(0, 0, 10, 25).wkt)[:2] == [(10.0, 0.0), (10.0, 25.0)]
    assert r17_check.band(0.0005, [0, 1, 10, 30]) == "equal" and r17_check.band(5.0, [0, 1, 10, 30]) == "within10mm" and r17_check.band(31.0, [0, 1, 10, 30]) == "worse"
    assert r17_check.centre(ring) == (5.0, 12.5)


def test_r280_a_build_may_take_only_some_fields_of_the_overrides(tmp_path):
    path = tmp_path / "overrides.json"
    path.write_text(_json.dumps({"note": "n", "overrides": [{"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.02}, "source": "NATIVE.rvt"},
                                                             {"plot": "1", "field": "mirrored", "value": True, "source": "NATIVE.rvt"}]}), encoding="utf-8")
    assert plan_rules.load_overrides(path, {"NATIVE.rvt"}, ("entryM", "heightM")) == {"1": {"entryM": {"gapToRuleM": 0.02}}}


def test_r282_a_key_hand_is_turned_where_its_old_file_is_drawn_the_other_hand_and_another_unit_is_counted_apart():
    import hand_compare
    old_files = {"V2": {"same": ["C10"], "opposite": ["VD2"]}, "D2": {"same": ["DP2"], "opposite": ["DT3"]}}
    assert hand_compare.old_code("RRE-WP-NSP-KEO-ARC-V2-ZZ-MOD-1200.rvt") == "V2" and hand_compare.old_code("RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt") is None
    assert hand_compare.as_built(True, "V2", "VD2", old_files) is False
    assert hand_compare.as_built(True, "D2", "DP2", old_files) is True
    assert hand_compare.as_built(True, "V2", "DP2", old_files) is None
    assert hand_compare.as_built(True, "D2", "DT3", old_files) is False
    new = {"1": [{"parameters": {"RRE_ZZ_SP_Prototype": "VD2"}, "transform": {"mirrored": False}}],
           "2": [{"parameters": {"RRE_ZZ_SP_Prototype": "DP2"}, "transform": {"mirrored": False}}],
           "3": [{"parameters": {"RRE_ZZ_SP_Prototype": "DP2"}, "transform": {"mirrored": False}}]}
    rows = [{"plot": "1", "mirrored": True, "linkFile": "RRE-WP-NSP-KEO-ARC-V2-ZZ-MOD-1200.rvt"},
            {"plot": "2", "mirrored": True, "linkFile": "RRE-WP-NSP-KEO-ARC-D2-ZZ-MOD-2200.rvt"},
            {"plot": "3", "mirrored": True, "linkFile": "RRE-WP-NSP-KEO-ARC-V4-ZZ-MOD-1400.rvt"}]
    result = hand_compare.compare(new, rows, old_files)
    assert result["counts"] == {"equal": 1, "differs": 1, "anotherUnit": 1}
    assert result["differs"] == [{"plot": "2", "code": "DP2", "oldFile": "D2"}]
