"""R470. A front ramp corner two or more plots share holds one level, the road level of the first corner of the joint in plot order, taken
before any rampFrontM gap, and the back pair keeps El @ Entry, on made up ramps and the made up row of test_level_floor_steps.py built in code
(R10), which R470_RampJointTests.cs holds the same."""
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


def test_r470_an_override_on_either_side_leaves_the_front_joint_at_the_road_of_the_first_plot():
    # The left ramp's front moved up 5 cm by a rampFrontM override, the right one's down 3 cm, the road at x 12 sampled 10.12 by the left
    # plot and 10.125 by the right, so the joint takes 10.12 of the first plot given and neither gap moves it
    left_road, right_road = [10.0, 10.12], [10.125, 10.24]
    left = plan_from_book.ramp_heights([z + 0.05 for z in left_road], 10.1)
    right = plan_from_book.ramp_heights([z - 0.03 for z in right_road], 10.2)
    joined = plan_from_book.joint_levels({"1": (LEFT, left, left_road), "2": (RIGHT, right, right_road)}, NEAR)
    assert joined["1"] == [left[0], 10.12, left[2], left[3]] and joined["2"] == [10.12, right[1], right[2], right[3]]
    # Given the other way round the joint takes the road of 2
    joined = plan_from_book.joint_levels({"2": (RIGHT, right, right_road), "1": (LEFT, left, left_road)}, NEAR)
    assert joined["1"][1] == joined["2"][0] == 10.125


def test_r470_back_corners_of_different_el_at_entry_are_never_joined():
    left = plan_from_book.ramp_heights([10.0, 10.12], 10.10)
    right = plan_from_book.ramp_heights([10.12, 10.24], 10.25)
    # The back right corner of 1 and the back left of 2 lie at (12, 5.52), 10.16 and 10.19 by El @ Entry, and keep them (R358), the front
    # corner at x 12 is the road sampled once, 10.12 on both, so nothing changes
    assert (left[2], right[3]) == (pytest.approx(10.16, abs=1e-12), pytest.approx(10.19, abs=1e-12))
    assert plan_from_book.joint_levels({"1": (LEFT, left, [10.0, 10.12]), "2": (RIGHT, right, [10.12, 10.24])}, NEAR) == {}
    # A back corner on the front corner of another ramp joins nothing either
    behind = [(12.0, 5.52), (24.0, 5.52), (24.0, 11.04), (12.0, 11.04)]
    assert plan_from_book.joint_levels({"1": (LEFT, left, [10.0, 10.12]), "3": (behind, [10.5, 10.6, 10.7, 10.8], [10.5, 10.6])}, NEAR) == {}


def test_r470_a_front_corner_11_mm_away_is_no_joint_and_one_9_mm_away_is():
    left, right = [10.05, 10.17, 10.16, 10.04], [10.07, 10.21, 10.25, 10.11]
    ramps = lambda dx: {"1": (LEFT, left, [10.0, 10.12]), "2": (moved(RIGHT, dx), right, [10.1, 10.24])}
    assert plan_from_book.joint_levels(ramps(NEAR * 1.1), NEAR) == {}
    joined = plan_from_book.joint_levels(ramps(NEAR * 0.9), NEAR)
    assert joined["1"][1] == joined["2"][0] == 10.12 and joined["1"][2] == 10.16 and joined["2"][3] == 10.11


def test_r470_three_front_corners_at_one_point_and_a_chain_take_the_road_of_the_first_plot():
    a = [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0)]
    b = [(0.0, 0.0), (-1.0, 0.0), (-1.0, 1.0), (0.0, 1.0)]
    c = [(0.0, 0.0), (0.0, -1.0), (1.0, -1.0), (1.0, -2.0)]
    joined = plan_from_book.joint_levels({"a": (a, [1.5, 5.0, 5.0, 2.0], [1.0, 5.0]), "b": (b, [2.0, 5.0, 5.0, 2.5], [2.0, 5.0]),
                                          "c": (c, [4.0, 9.0, 9.0, 9.0], [4.0, 9.0])}, NEAR)
    assert joined == {"a": [1.0, 5.0, 5.0, 2.0], "b": [1.0, 5.0, 5.0, 2.5], "c": [1.0, 9.0, 9.0, 9.0]}
    # A chain, each front corner within 6 mm of the next and 12 mm from the far one, is one joint all the same
    chain = {"a": ([(0.0, 0.0), (5.0, 0.0)], [1.5, 7.0], [1.0, 7.0]), "b": ([(NEAR * 0.6, 0.0), (-5.0, 0.0)], [2.0, 8.0], [2.0, 8.0]),
             "c": ([(NEAR * 1.2, 0.0), (0.0, 5.0)], [6.0, 9.0], [6.0, 9.0])}
    assert plan_from_book.joint_levels(chain, NEAR) == {"a": [1.0, 7.0], "b": [1.0, 8.0], "c": [1.0, 9.0]}


def test_r470_a_plot_with_no_joint_or_a_joint_with_no_override_is_left_out():
    left, far = [10.0, 10.12, 10.16, 10.04], [11.0, 11.1, 11.2, 11.3]
    ramps = {"1": (LEFT, left, [10.0, 10.12]), "2": (RIGHT, [10.17, 10.29, 10.28, 10.17], [10.12, 10.24]), "9": (moved(LEFT, 500.0), far, far[:2])}
    before = copy.deepcopy(ramps)
    joined = plan_from_book.joint_levels(ramps, NEAR)
    assert joined == {"2": [10.12, 10.29, 10.28, 10.17]} and ramps == before
    # A joint whose front corners lie at the road, as the ramp rules gave them with no gap, changes nothing
    assert plan_from_book.joint_levels({"1": (LEFT, left, [10.0, 10.12]), "2": (RIGHT, [10.12, 10.24, 10.28, 10.16], [10.12, 10.24])}, NEAR) == {}


def test_r470_near_is_read_from_config_floors_ramp_joint_and_a_bad_one_refused():
    settings = project_profile.load_settings()
    assert plan_from_book.joint_near(settings) == settings.get("floors.rampJoint.nearM") == NEAR
    wider = copy.deepcopy(settings)
    wider.values["floors"]["rampJoint"]["nearM"] = 0.02
    assert plan_from_book.joint_near(wider) == 0.02
    # At 2 cm the front corner 11 mm away is a joint
    assert plan_from_book.joint_levels({"1": (LEFT, [10.0, 10.12, 10.16, 10.04], [10.0, 10.12]), "2": (moved(RIGHT, NEAR * 1.1), [10.10, 10.24, 10.28, 10.14], [10.10, 10.24])},
                                       plan_from_book.joint_near(wider))["2"][0] == 10.12
    for bad in (0, -0.01, "0.01", True, None):
        wider.values["floors"]["rampJoint"]["nearM"] = bad
        with pytest.raises(project_profile.ProfileError, match="floors.rampJoint.nearM"):
            plan_from_book.joint_near(wider)


def test_r470_a_build_and_its_step_overrides_keep_the_front_joints_at_the_road_a_ramp_front_override_moved(monkeypatch):
    built = made_build(monkeypatch, {"2": {"rampFrontM": {"gapToRuleM": 0.05}}})
    assert built["trace"]["rampJoint"] == NEAR
    plan = built["plan"]
    ramp = {number: plan[number]["floors"]["ramp"] for number in ("1", "2", "3", "4")}
    corners = {number: ramp[number]["corners"]["value"] for number in ramp}
    # Both front corners of 2 are shared, at x 12 with 1 and at x 24 with 3, so they stay at the road 10.12 and 10.24 and its 5 cm gap moves
    # neither, its back pair at El @ Entry as the ramp rules gave it
    assert corners["1"][1] == corners["2"][0] == [12.0, 0, 10.12] and corners["2"][1] == corners["3"][0] == [24.0, 0, 10.24]
    assert [corner[2] for corner in corners["2"][2:]] == [10.268, 10.148]
    # The height offset of each ramp is its lowest corner after the joint over the ramp level, 0 in the made up site
    assert (ramp["1"]["heightOffset"]["value"], ramp["2"]["heightOffset"]["value"]) == (10.0, 10.12)
    # A ramp none of whose corners moved is the one the ramp rules wrote, and sources and status stay those of the ramp rules
    assert corners["4"] == [[36.0, 0, 10.36], [48.0, 0, 10.48], [48.0, 5.52, 10.508], [36.0, 5.52, 10.388]]
    assert ramp["2"]["corners"]["source"] == ramp["4"]["corners"]["source"] == plan_from_book.ESTIMATE
    assert "statusFrom" in ramp["2"]["corners"] and "status" in ramp["2"]["heightOffset"]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert made["2"]["rampZ"][:2] == [made["1"]["rampZ"][1], made["3"]["rampZ"][0]] == [built["trace"]["front"]["2"]["front"][0], built["trace"]["front"]["2"]["front"][1]]
    assert [common.r3(z) for z in made["2"]["rampZ"]] == [corner[2] for corner in corners["2"]]
