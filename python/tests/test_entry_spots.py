"""Round ADDIN Phase 2b: the spot elevations at the plot entrance against El @ Entry (R327).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import json

import pytest

import entry_spots
import project_profile

FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A"]}, "plotParameter": "Plot"}
BLOCK = {"radiiM": [0.5, 6.0], "cornerM": 0.05, "entryParameter": "El at entry"}
# The survey point is 1000 m east of the internal origin, so a spot is moved by it as a floor outline is not
OFFSET = [1000.0, 0.0, 0.0]


def ramp(plot, x0, element_id, front=6.0, back=6.1):
    """A ramp 10 m along the road and 5 m deep, its front at y 0 and its higher long edge, the entrance, at y 5. Corners are internal."""
    return {"typeName": "Ramp A", "elementId": element_id, "parameters": {"Plot": "DP4-" + plot},
            "corners": [[x0, 0, front], [x0 + 10, 0, front], [x0 + 10, 5, back], [x0, 5, back]]}


def generic(plot, x0, entry):
    return {"typeName": "Slab G", "elementId": 900 + x0, "parameters": {"Plot": "DP4-" + plot, "El at entry": entry}}


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
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK, schedule={"501": 6.104, "777": 1.0})
    schedule = found["counts"]["schedule"]
    assert schedule["plots"] == 1 and schedule["radius 6.0"]["within1mm"] == 0 and schedule["radius 6.0"]["within5mm"] == 1
    assert found["counts"]["key"]["plots"] == 2 and found["counts"]["key"]["radius 6.0"]["withSpot"] == 1


def test_r327_a_spot_with_no_value_of_another_base_or_on_another_host_is_left_out_where_the_rule_says():
    floors = [ramp("501", 0, 1), generic("501", 0, 6.1)]
    spots = [spot(11, 0, 5, None, host=1), spot(12, 10, 5, 6.1, host=1, base="Relative"), spot(13, 10, 5, 6.1, host=2)]
    found = entry_spots.key_entry(document(floors, spots), FLOORS, BLOCK)
    # The spot on another host counts by its place within a radius, never as a corner spot of this ramp
    assert found["plots"]["501"]["near"]["6.0"] == [13] and found["plots"]["501"]["corners"] == [None, None]
    assert found["spotsLeftOut"] == {"noValue": 1, "otherBase": 1}
    assert found["counts"]["key"]["cornerPair"]["plots"] == 0


def test_r327_a_plot_with_no_ramp_two_ramps_or_a_ramp_of_no_four_corners_or_no_el_at_entry_is_listed_by_why():
    bad = ramp("503", 40, 4)
    bad["corners"] = bad["corners"][:3]
    floors = [generic("501", 0, 6.1), ramp("502", 20, 2), ramp("502", 20, 3), generic("502", 20, 6.1), bad, generic("503", 40, 6.1),
              ramp("504", 60, 5), generic("504", 60, "none")]
    found = entry_spots.key_entry(document(floors, []), FLOORS, BLOCK)
    assert found["notCompared"] == {"noRamp": ["501"], "twoRamps": ["502"], "noEntrance": ["503"]}
    assert found["noEntry"] == {"key": ["504"], "schedule": ["504"]}


@pytest.mark.parametrize("block", [
    {"radiiM": [], "cornerM": 0.05}, {"radiiM": [6.0, 0.5], "cornerM": 0.05}, {"radiiM": [0, 6.0], "cornerM": 0.05},
    {"radiiM": ["6"], "cornerM": 0.05}, {"radiiM": [6.0], "cornerM": 0}, {"radiiM": [6.0]},
])
def test_r327_the_radii_must_rise_above_0_and_the_corner_distance_be_above_0(tmp_path, block):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"entrySpots": block, "pastAll": {"entryParameter": "x"}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="entrySpots"):
        entry_spots.load_block(project_profile.load_settings(path))


def test_r327_the_real_settings_name_the_radii_the_corner_distance_and_the_entry_parameter():
    settings = project_profile.load_settings()
    block = entry_spots.load_block(settings)
    assert block["radiiM"] == sorted(block["radiiM"]) and block["cornerM"] > 0
    assert block["entryParameter"] == settings.get("pastAll.entryParameter")
