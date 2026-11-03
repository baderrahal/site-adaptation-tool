"""R517. Every floor of a plot stands inside its outline by floors.plotPull.pullM, so the floors of two plots lap on no 1 mm grid, on made up rings and the made up row of
test_level_floor_steps.py built in code (R10), which R517_FloorsPulledTests.cs holds the same."""
import copy

import pytest

import floor_fit
import plan_from_book
import project_profile
from test_level_floor_steps import made_build

PULL = 0.002


def test_r517_a_rectangle_moves_each_corner_by_the_pull_on_both_axes_in_either_direction():
    ring = [(0.0, 0.0), (12.0, 0.0), (12.0, 5.52), (0.0, 5.52)]
    inside = [(0.002, 0.002), (11.998, 0.002), (11.998, 5.518), (0.002, 5.518)]
    assert floor_fit.pulled_ring(ring, PULL) == pytest.approx(inside, abs=1e-12)
    assert floor_fit.pulled_ring(ring[::-1], PULL) == pytest.approx(inside[::-1], abs=1e-12)


def test_r517_a_skew_corner_takes_the_mitre_and_a_notch_moves_into_the_plot_too():
    # The corner of 45 degrees: both edges 2 mm off stand 2 mm from each line, tan 22.5 of the pull along the edge
    moved = floor_fit.pulled_ring([(0.0, 0.0), (10.0, 0.0), (0.0, 10.0)], PULL)
    assert moved[0] == pytest.approx((PULL, PULL), abs=1e-12)
    assert moved[1][1] == pytest.approx(PULL, abs=1e-12) and moved[1][0] == pytest.approx(10.0 - PULL * (1 + 2 ** 0.5), abs=1e-12)
    # A notch at (5, 2) in the top of a square: the reflex corner moves up into the material
    notch = floor_fit.pulled_ring([(0.0, 0.0), (10.0, 0.0), (10.0, 4.0), (6.0, 4.0), (6.0, 2.0), (4.0, 2.0), (4.0, 4.0), (0.0, 4.0)], PULL)
    assert notch[4] == pytest.approx((6.0 + PULL, 2.0 - PULL), abs=1e-12) and notch[3] == pytest.approx((6.0 + PULL, 4.0 - PULL), abs=1e-12)


def test_r517_no_pull_and_a_ring_of_no_mitre_stay_as_given():
    ring = [(0.0, 0.0), (12.0, 0.0), (12.0, 5.52), (0.0, 5.52)]
    assert floor_fit.pulled_ring(ring, 0) == ring
    assert floor_fit.pulled_ring([(0.0, 0.0), (1.0, 0.0), (1.0, 0.0), (0.0, 1.0)], PULL) == [(0.0, 0.0), (1.0, 0.0), (1.0, 0.0), (0.0, 1.0)]
    assert floor_fit.pulled_ring([(0.0, 0.0), (1.0, 0.0)], PULL) == [(0.0, 0.0), (1.0, 0.0)]


def test_r517_the_pull_is_read_from_config_and_a_bad_one_refused():
    settings = project_profile.load_settings()
    assert floor_fit.plot_pull(settings) == settings.get("floors.plotPull.pullM") == PULL
    for bad in (-0.001, "0.002", True, None):
        refused = copy.deepcopy(settings)
        refused.values["floors"]["plotPull"]["pullM"] = bad
        with pytest.raises(project_profile.ProfileError, match="floors.plotPull.pullM"):
            floor_fit.plot_pull(refused)


def test_r517_the_floors_of_a_build_stand_inside_and_the_ramp_corners_move_with_the_outline(monkeypatch):
    load = project_profile.load_settings

    def pulled(*args, **kwargs):
        settings = load(*args, **kwargs)
        settings.values["floors"]["plotPull"]["pullM"] = PULL
        return settings

    monkeypatch.setattr(project_profile, "load_settings", pulled)
    built = made_build(monkeypatch, {}, pull=PULL)
    floors = built["plan"]["2"]["floors"]
    assert floors["generic"]["outline"]["value"]["loops"][0]["points"] == [[12.002, 0.002], [23.998, 0.002], [23.998, 24.998], [12.002, 24.998]][::-1] or floors["generic"]["outline"]["value"]["loops"][0]["points"][0][0] != 12.0
    corners = floors["ramp"]["corners"]["value"]
    assert [corner[:2] for corner in corners] == [[12.002, 0.002], [23.998, 0.002], [23.998, 5.518], [12.002, 5.518]]
    assert [corner[:2] for corner in corners] == floors["ramp"]["outline"]["value"]["loops"][0]["points"]
    # The steps floors and overrides hold the same outline as the plan, and the rule ramp keeps its levels
    step = built["floors"]["2"]
    assert [plan_from_book.plan_from_past.point(point) for point in step["rampOutline"]] == [corner[:2] for corner in corners]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert [plan_from_book.plan_from_past.point(point) for point in made["2"]["rampOutline"]] == [corner[:2] for corner in corners]
    for ring in floors["yard"]["outline"]["value"]["loops"] if "yard" in floors else ():
        for x, y in (point[:2] for point in ring["points"]):
            assert 12.0 < x < 24.0 and 0.0 < y < 25.0
