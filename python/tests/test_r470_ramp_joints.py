"""R470. A ramp corner two or more plots share holds one level, the mean of the levels the ramp rules give it, on made up ramps and the made
up row of test_level_floor_steps.py built in code (R10), which R470_RampJointTests.cs holds the same."""
import copy

import pytest

import common
import plan_from_book
import project_profile
from test_level_floor_steps import made_build

NEAR = 0.01
LEFT = [(0.0, 0.0), (12.0, 0.0), (12.0, 5.52), (0.0, 5.52)]
RIGHT = [(12.0, 0.0), (24.0, 0.0), (24.0, 5.52), (12.0, 5.52)]


def moved(corners, dx):
    return [(x + dx, y) for x, y in corners]


def test_r470_two_ramps_side_by_side_with_front_gaps_meet_at_one_level():
    # The left ramp's front moved up 5 cm by a rampFrontM override, the right one's down 3 cm, so the shared front corner at x 12 differs by 8 cm
    left = plan_from_book.ramp_heights([10.0 + 0.05, 10.12 + 0.05], 10.1)
    right = plan_from_book.ramp_heights([10.12 - 0.03, 10.24 - 0.03], 10.2)
    joined = plan_from_book.joint_levels({"1": (LEFT, left), "2": (RIGHT, right)}, NEAR)
    assert joined["1"][1] == joined["2"][0] == (left[1] + right[0]) / 2.0
    assert joined["1"][1] == pytest.approx(10.13, abs=1e-12)
    # The corners of no joint keep the levels of the ramp rules
    assert (joined["1"][0], joined["2"][1]) == (left[0], right[1])


def test_r470_back_corners_with_different_el_at_entry_meet_at_their_mean():
    left = plan_from_book.ramp_heights([10.0, 10.12], 10.10)
    right = plan_from_book.ramp_heights([10.12, 10.24], 10.25)
    joined = plan_from_book.joint_levels({"1": (LEFT, left), "2": (RIGHT, right)}, NEAR)
    # The back right corner of 1 and the back left of 2 lie at (12, 5.52), 10.16 and 10.19 by El @ Entry, so both take 10.175
    assert joined["1"][2] == joined["2"][3] == (left[2] + right[3]) / 2.0
    assert joined["1"][2] == pytest.approx(10.175, abs=1e-12)
    # The front corner at x 12 is the road sampled once, 10.12 on both, and stays as it was
    assert joined["1"][1] == joined["2"][0] == 10.12


def test_r470_a_corner_11_mm_away_is_no_joint_and_one_9_mm_away_is():
    left = [10.0, 10.12, 10.16, 10.04]
    right = [10.10, 10.24, 10.28, 10.14]
    assert plan_from_book.joint_levels({"1": (LEFT, left), "2": (moved(RIGHT, 0.011), right)}, NEAR) == {}
    joined = plan_from_book.joint_levels({"1": (LEFT, left), "2": (moved(RIGHT, NEAR * 0.9), right)}, NEAR)
    assert joined["1"][1] == joined["2"][0] == (10.12 + 10.10) / 2.0 and joined["1"][2] == joined["2"][3] == (10.16 + 10.14) / 2.0


def test_r470_three_ramps_at_one_point_and_a_chain_take_one_mean():
    a, b, c = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)], [(0.0, 0.0), (-1.0, 0.0), (-1.0, 1.0), (0.0, 1.0)], [(0.0, 0.0), (0.0, -1.0), (1.0, -1.0), (1.0, -2.0)]
    joined = plan_from_book.joint_levels({"a": (a, [1.0, 5.0, 5.0, 2.0]), "b": (b, [2.0, 5.0, 5.0, 2.0]), "c": (c, [4.0, 9.0, 9.0, 9.0])}, NEAR)
    assert joined["a"][0] == joined["b"][0] == joined["c"][0] == (1.0 + 2.0 + 4.0) / 3.0
    # A chain, each corner within 6 mm of the next and 12 mm from the far one, is one joint all the same
    chain = {"a": ([(0.0, 0.0)], [1.0]), "b": ([(0.006, 0.0)], [2.0]), "c": ([(0.012, 0.0)], [6.0])}
    assert plan_from_book.joint_levels(chain, NEAR) == {"a": [3.0], "b": [3.0], "c": [3.0]}


def test_r470_a_plot_with_no_joint_is_left_out_and_the_order_of_the_sum_is_the_order_given():
    left, far = [10.0, 10.12, 10.16, 10.04], [11.0, 11.1, 11.2, 11.3]
    ramps = {"1": (LEFT, left), "2": (RIGHT, [10.13, 10.24, 10.28, 10.17]), "9": (moved(LEFT, 500.0), far)}
    before = copy.deepcopy(ramps)
    joined = plan_from_book.joint_levels(ramps, NEAR)
    assert set(joined) == {"1", "2"} and ramps == before
    # A joint whose levels already agree changes nothing, so the plan of its plots stays as the ramp rules wrote it
    assert plan_from_book.joint_levels({"1": (LEFT, left), "2": (RIGHT, [10.12, 10.24, 10.28, 10.16])}, NEAR) == {}
    # Three levels summed left to right one by one, as the C# sums them, never by the compensated sum of Python 3.12
    levels = [0.1, 0.2, 0.3]
    total = 0.0
    for z in levels:
        total += z
    tri = {str(index): ([(0.0, 0.0)], [z]) for index, z in enumerate(levels)}
    assert plan_from_book.joint_levels(tri, NEAR)["0"] == [total / 3.0]


def test_r470_near_is_read_from_config_floors_ramp_joint_and_a_bad_one_refused():
    settings = project_profile.load_settings()
    assert plan_from_book.joint_near(settings) == settings.get("floors.rampJoint.nearM") == NEAR
    wider = copy.deepcopy(settings)
    wider.values["floors"]["rampJoint"]["nearM"] = 0.02
    assert plan_from_book.joint_near(wider) == 0.02
    # At 2 cm the corner 11 mm away is a joint
    assert plan_from_book.joint_levels({"1": (LEFT, [10.0, 10.12, 10.16, 10.04]), "2": (moved(RIGHT, 0.011), [10.10, 10.24, 10.28, 10.14])},
                                       plan_from_book.joint_near(wider))["1"][1] == (10.12 + 10.10) / 2.0
    for bad in (0, -0.01, "0.01", True, None):
        wider.values["floors"]["rampJoint"]["nearM"] = bad
        with pytest.raises(project_profile.ProfileError, match="floors.rampJoint.nearM"):
            plan_from_book.joint_near(wider)


def test_r470_a_build_and_its_step_overrides_join_the_front_corners_a_ramp_front_override_moved(monkeypatch):
    built = made_build(monkeypatch, {"2": {"rampFrontM": {"gapToRuleM": 0.05}}})
    assert built["trace"]["rampJoint"] == NEAR
    plan = built["plan"]
    ramp = {number: plan[number]["floors"]["ramp"] for number in ("1", "2", "3", "4")}
    corners = {number: ramp[number]["corners"]["value"] for number in ramp}
    # The road at x 12 is 10.12 and plot 2 lifts its front 5 cm, so both plots write the mean 10.145 there, and 10.265 at x 24
    assert corners["1"][1] == corners["2"][0] == [12.0, 0, common.r3((10.12 + 10.17) / 2.0)] == [12.0, 0, 10.145]
    assert corners["2"][1][2] == corners["3"][0][2] == 10.265
    # The height offset of each ramp is its lowest corner after the joint over the ramp level, 0 in the made up site
    assert (ramp["1"]["heightOffset"]["value"], ramp["2"]["heightOffset"]["value"]) == (10.0, 10.145)
    # A ramp none of whose corners moved is the one the ramp rules wrote, and sources and status stay those of the ramp rules
    assert corners["4"] == [[36.0, 0, 10.36], [48.0, 0, 10.48], [48.0, 5.52, 10.508], [36.0, 5.52, 10.388]]
    assert ramp["1"]["corners"]["source"] == ramp["4"]["corners"]["source"] == plan_from_book.ESTIMATE
    assert "statusFrom" in ramp["1"]["corners"] and "status" in ramp["1"]["heightOffset"]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert made["1"]["rampZ"][1] == made["2"]["rampZ"][0] == pytest.approx(10.145, abs=1e-12)
    assert [common.r3(z) for z in made["2"]["rampZ"]] == [corner[2] for corner in corners["2"]]
