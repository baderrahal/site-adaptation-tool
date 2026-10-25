"""Round ADDIN Phase 2b: the spot elevations at the plot entrance against El @ Entry (R327).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import json

import pytest

import entry_spots
import project_profile

FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A"]}, "plotParameter": "Plot"}
BLOCK = {"radiiM": [0.5, 6.0], "cornerM": 0.05, "bandsM": [0.001, 0.005], "entryParameter": "El at entry", "entryHeightM": {"min": 0.0, "max": 1.5},
         "flatTopM": 0.0005}
# The survey point is 1000 m east of the internal origin, so a spot is moved by it as a floor outline is not
OFFSET = [1000.0, 0.0, 0.0]


def ramp(plot, x0, element_id, front=6.0, back=6.1):
    """A ramp 10 m along the road and 5 m deep, its front at y 0 and its higher long edge, the entrance, at y 5. Corners are internal."""
    return {"typeName": "Ramp A", "elementId": element_id, "parameters": {"Plot": "DP4-" + plot},
            "corners": [[x0, 0, front], [x0 + 10, 0, front], [x0 + 10, 5, back], [x0, 5, back]]}


def generic(plot, x0, entry):
    """A generic floor with a flat top 0.15 m over its El @ Entry, as the keys hold them, or at 7.0 when the value is no number."""
    top = entry + 0.15 if isinstance(entry, (int, float)) else 7.0
    return {"typeName": "Slab G", "elementId": 900 + x0, "parameters": {"Plot": "DP4-" + plot, "El at entry": entry}, "topElevationRange": {"min": top, "max": top}}


def spot(element_id, x, y, value, host=None, base="Survey Point"):
    return {"elementId": element_id, "origin": [x, y, 0.0], "value": value, "elevationBase": base,
            "host": None if host is None else {"elementId": host, "category": "Floors", "typeName": "Ramp A"}}


def document(floors, spots):
    return {"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": OFFSET}, "floors": floors, "spotElevations": spots, "links": []}


def test_r327_the_spots_at_the_entrance_corners_equal_el_at_entry_and_one_at_the_road_does_not():
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1)]
    spots = [spot(11, 0, 5, 6.1, host=1), spot(12, 10, 5, 6.1004, host=1), spot(13, 0, 0, 6.0, host=1)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK)
    plot = found["plots"]["501"]
    # The two entrance corners lie 5 m from the middle of the entrance edge, the road corner 7.07 m
    assert plot["near"] == {"0.5": [], "6.0": [11, 12]} and plot["corners"] == [11, 12]
    key = found["counts"]["key"]
    assert key["plots"] == 1
    assert key["radius 0.5"] == {"withSpot": 0, "within1mm": 0, "within5mm": 0, "share1mm": 0.0, "share5mm": 0.0, "spots": 0, "spotsWithin1mm": 0, "spotsWithin5mm": 0}
    assert key["radius 6.0"] == {"withSpot": 1, "within1mm": 1, "within5mm": 1, "share1mm": 1.0, "share5mm": 1.0, "spots": 2, "spotsWithin1mm": 2, "spotsWithin5mm": 2}
    assert key["cornerPair"] == {"plots": 1, "within1mm": 1, "within5mm": 1, "share1mm": 1.0, "share5mm": 1.0}


def test_r327_el_at_entry_of_the_schedule_is_compared_apart_by_plot_number():
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1), ramp("502", 20, 2), generic("502", 20, 6.1)]
    spots = [spot(11, 0, 5, 6.1, host=1), spot(12, 10, 5, 6.1, host=1)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK, schedule={"501": 6.104, "777": 1.0}, numbers={"501": "501", "502": "502"})
    schedule = found["counts"]["schedule"]
    assert schedule["plots"] == 1 and schedule["radius 6.0"]["within1mm"] == 0 and schedule["radius 6.0"]["within5mm"] == 1
    assert found["counts"]["key"]["plots"] == 2 and found["counts"]["key"]["radius 6.0"]["withSpot"] == 1


def test_r327_a_spot_with_no_value_of_another_base_or_on_another_host_is_left_out_where_the_rule_says():
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1)]
    spots = [spot(11, 0, 5, None, host=1), spot(12, 10, 5, 6.1, host=1, base="Relative"), spot(13, 10, 5, 6.1, host=2)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK)
    # A spot on a host that is no floor of a plot counts for no plot, neither within a radius nor as a corner spot
    assert found["plots"]["501"]["near"]["6.0"] == [] and found["plots"]["501"]["corners"] == [None, None]
    assert found["spotsLeftOut"] == {"noValue": 1, "otherBase": 1} and found["spotsOnNoPlotFloor"] == 1
    assert found["counts"]["key"]["cornerPair"]["plots"] == 0


def test_r327_a_spot_on_a_floor_of_another_plot_never_counts_for_this_plot_and_one_on_its_generic_floor_does():
    # The ramp of 502 lies next to the ramp of 501, its spots sit within 6 m of the entrance of 501
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1), ramp("502", 10, 2), generic("502", 10, 6.1)]
    spots = [spot(11, 0, 5, 6.1, host=1), spot(21, 10.2, 5, 6.1, host=2), spot(22, 12, 7, 6.1, host=910)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK)
    assert found["plots"]["501"]["near"]["6.0"] == [11] and found["plots"]["502"]["near"]["6.0"] == [21, 22]
    assert found["counts"]["key"]["radius 6.0"]["spots"] == 3


def test_r327_a_key_value_whose_floor_top_is_outside_the_height_band_is_no_el_at_entry_and_is_listed():
    # 150.0 is the placeholder of R325, the flat floor top at 7.0 stands 143 m under it
    floors = [ramp("501", 0, 1), dict(generic("501", 0, 150.0), topElevationRange={"min": 7.0, "max": 7.0}), ramp("502", 20, 2),
              dict(generic("502", 20, 6.1), topElevationRange={"min": 6.25, "max": 6.25})]
    spots = [spot(11, 0, 5, 6.1, host=1), spot(21, 20, 5, 6.1, host=2)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK)
    assert found["counts"]["key"]["plots"] == 1 and found["refusedEntry"] == ["501"] and found["noEntry"]["key"] == ["501"]


def test_r327_one_flat_top_tolerance_of_key_plans_decides_and_a_sloped_generic_floor_gives_no_el_at_entry_as_the_plan_refuses_it():
    block = dict(BLOCK, flatTopM=0.008)
    nearly = dict(generic("501", 0, 6.8), topElevationRange={"min": 7.0, "max": 7.005})
    sloped = dict(generic("502", 20, 6.8), topElevationRange={"min": 7.0, "max": 7.05})
    # The top of 502 falls 50 mm, so it has no flat top to give a height, the plan of R325 refuses its value and so must the spots
    assert entry_spots.key_level(sloped, block, OFFSET) == (None, True)
    assert entry_spots.key_level(nearly, block, OFFSET) == (6.8, False)
    # The top of 501 falls 5 mm, flat within keyPlans.flatTopM, so a value 2.0 m under it is off the height band
    assert entry_spots.key_level(dict(nearly, parameters={"Plot": "DP4-501", "El at entry": 5.0}), block, OFFSET) == (None, True)
    assert entry_spots.key_level(dict(nearly, parameters={"Plot": "DP4-501", "El at entry": 5.0}), dict(BLOCK, flatTopM=0.001), OFFSET) == (None, True)


def test_r327_el_at_entry_of_the_schedule_is_found_by_the_official_number_the_join_gives_the_plot():
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1)]
    spots = [spot(11, 0, 5, 6.1, host=1), spot(12, 10, 5, 6.1, host=1)]
    # The model plot 501 is the official plot 1501, the schedule holds 1501 and another 501 of no matter
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK, schedule={"1501": 6.1, "501": 1.0}, numbers={"501": "1501"})
    assert found["counts"]["schedule"]["plots"] == 1 and found["counts"]["schedule"]["radius 6.0"]["within1mm"] == 1
    # A plot the join gives no number has no schedule value
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK, schedule={"501": 6.1}, numbers={})
    assert found["counts"]["schedule"]["plots"] == 0 and found["noEntry"]["schedule"] == ["501"]


def test_r327_a_plot_with_no_ramp_two_ramps_or_a_ramp_of_no_four_corners_or_no_el_at_entry_is_listed_by_why():
    bad = ramp("503", 40, 4)
    bad["corners"] = bad["corners"][:3]
    floors = [generic("501", 0, 6.1), ramp("502", 20, 2), ramp("502", 20, 3), generic("502", 20, 6.1), bad, generic("503", 40, 6.1),
              ramp("504", 60, 5), generic("504", 60, "none")]
    found = entry_spots.key_entry(document(floors, []), FLOORS, BLOCK)
    assert found["notCompared"] == {"noRamp": ["501"], "twoRamps": ["502"], "noEntrance": ["503"]}
    assert found["noEntry"] == {"key": ["504"], "schedule": ["504"]}


@pytest.mark.parametrize("block", [
    {"radiiM": [], "cornerM": 0.05, "bandsM": [0.001]}, {"radiiM": [6.0, 0.5], "cornerM": 0.05, "bandsM": [0.001]},
    {"radiiM": [0, 6.0], "cornerM": 0.05, "bandsM": [0.001]}, {"radiiM": ["6"], "cornerM": 0.05, "bandsM": [0.001]},
    {"radiiM": [6.0], "cornerM": 0, "bandsM": [0.001]}, {"radiiM": [6.0], "bandsM": [0.001]}, {"radiiM": [6.0], "cornerM": 0.05},
    {"radiiM": [6.0], "cornerM": 0.05, "bandsM": [0.005, 0.001]}, {"radiiM": [6.0], "cornerM": 0.05, "bandsM": [0]},
])
def test_r327_the_radii_must_rise_above_0_and_the_corner_distance_be_above_0(tmp_path, block):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"entrySpots": block, "pastAll": {"entryParameter": "x"}, "keyPlans": {"entryHeightM": {"min": 0.0, "max": 1.5}}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="entrySpots"):
        entry_spots.load_block(project_profile.load_settings(path))


def test_r327_the_real_settings_name_the_radii_the_corner_distance_and_the_entry_parameter():
    settings = project_profile.load_settings()
    block = entry_spots.load_block(settings)
    assert block["radiiM"] == sorted(block["radiiM"]) and block["cornerM"] > 0 and block["bandsM"] == [0.001, 0.005]
    assert block["entryHeightM"] == settings.get("keyPlans.entryHeightM") and block["flatTopM"] == settings.get("keyPlans.flatTopM")
    assert block["entryParameter"] == settings.get("pastAll.entryParameter")
    assert entry_spots.band_names(block["bandsM"]) == ["1mm", "5mm"]


FULL_FLOORS = dict(FLOORS, levels={"generic": "L1", "yard": "L2", "ramp": "L1"}, yardBelowFflM=0.1, yardShareTie=0.01, yardMinShare=0.5)


def spot_settings(tmp_path, name, radii):
    path = tmp_path / name
    path.write_text(json.dumps({"entrySpots": {"radiiM": radii, "cornerM": 0.05, "bandsM": [0.001, 0.005]}, "pastAll": {"entryParameter": "El at entry"},
                                "keyPlans": {"entryHeightM": {"min": 0.0, "max": 1.5}, "flatTopM": 0.0005},
                                "floors": FULL_FLOORS, "answerKeys": {"keys": [{"code": "IMX", "copy": "X-IMX.rvt"}, {"code": "IMY", "copy": "X-IMY.rvt"}]}}),
                    encoding="utf-8")
    return path


def test_r327_each_key_keeps_the_settings_it_was_run_with_and_a_run_at_other_settings_must_rerun_every_key_of_the_file(tmp_path):
    reads = tmp_path / "reads"
    for code, job in (("IMX", "KX"), ("IMY", "KY")):
        (reads / job).mkdir(parents=True)
        doc = dict(document([ramp("501", 0, 1), generic("501", 0, 6.1)], [spot(11, 0, 5, 6.1, host=1)]), file="X-" + code + "_TEST-" + job)
        (reads / job / ("testhost_" + job + "_prototypes.json")).write_text(json.dumps({"documents": [doc]}), encoding="utf-8")
    out = tmp_path / "spots"
    run = lambda name, radii, *keys: entry_spots.main(["--settings", str(spot_settings(tmp_path, name, radii)), "--reads", str(reads)]
                                                       + [part for key in keys for part in ("--key", key)] + ["--out", str(out)])
    assert run("a.json", [0.5, 6.0], "IMX=KX") == 0
    assert run("a.json", [0.5, 6.0], "IMY=KY") == 0
    report = json.loads((out / entry_spots.OUTPUT).read_text(encoding="utf-8"))
    assert "settings" not in report
    assert report["keys"]["IMX"]["settings"]["radiiM"] == [0.5, 6.0] and report["keys"]["IMY"]["settings"]["radiiM"] == [0.5, 6.0]
    assert set(report["keys"]["IMX"]["plots"]["501"]["near"]) == {"0.5", "6.0"}
    # IMY alone at other radii would leave the file with keys of two settings, it is refused and the file stays as it was
    before = (out / entry_spots.OUTPUT).read_bytes()
    with pytest.raises(SystemExit):
        run("b.json", [0.5, 4.0], "IMY=KY")
    assert (out / entry_spots.OUTPUT).read_bytes() == before
    assert run("b.json", [0.5, 4.0], "IMX=KX", "IMY=KY") == 0
    report = json.loads((out / entry_spots.OUTPUT).read_text(encoding="utf-8"))
    assert {code: entry["settings"]["radiiM"] for code, entry in report["keys"].items()} == {"IMX": [0.5, 4.0], "IMY": [0.5, 4.0]}


def test_r327_a_run_with_the_schedule_needs_the_join_to_find_the_official_number(tmp_path):
    with pytest.raises(SystemExit):
        entry_spots.main(["--settings", str(spot_settings(tmp_path, "a.json", [6.0])), "--reads", str(tmp_path), "--key", "IMX=KX",
                          "--plots", str(tmp_path / "plots.csv"), "--out", str(tmp_path / "spots")])
