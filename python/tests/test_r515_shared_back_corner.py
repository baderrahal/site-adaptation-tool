"""R515, R516. Under a mode of floors.rampJoint.modes a back ramp corner two or more plots share holds one level, the door level of each such plot
is the road at front plus floors.rampJoint.slopePercent over the ramp depth, and the entryM and rampFrontM overrides of such a plot are left with why,
on made up ramps and the made up row of test_level_floor_steps.py built in code (R10), which R515_SharedBackCornerTests.cs holds the same."""
import copy

import pytest

import plan_from_book
import project_profile
from test_level_floor_steps import made_build

NEAR = 0.005
LEFT = [(0.0, 0.0), (12.0, 0.0), (12.0, 5.52), (0.0, 5.52)]
RIGHT = [(12.0, 0.0), (24.0, 0.0), (24.0, 5.52), (12.0, 5.52)]
FAR = [(100.0, 0.0), (112.0, 0.0), (112.0, 5.52), (100.0, 5.52)]
LOAD = project_profile.load_settings


def joined(**more):
    """The project settings with the joint on under the past job of the made up row, which config keeps off (R515)."""
    settings = LOAD()
    settings.values["floors"]["rampJoint"].update({"join": True, "joinBack": True, "modes": ["rev6", "roshnPastJob"], "slopePercent": 0.5}, **more)
    return settings


def test_r515_a_back_joint_takes_the_level_of_its_first_corner_and_a_free_corner_stays():
    left = plan_from_book.ramp_heights([10.0, 10.12], 10.1)
    right = plan_from_book.ramp_heights([10.12, 10.24], 10.25)
    found = plan_from_book.joint_levels({"1": (LEFT, left, [10.0, 10.12]), "2": (RIGHT, right, [10.12, 10.24])}, NEAR, True)
    assert "1" not in found and found["2"][3] == left[2] and found["2"][:3] == right[:3]
    # Given the other way round the joint takes the back corner of 2
    found = plan_from_book.joint_levels({"2": (RIGHT, right, [10.12, 10.24]), "1": (LEFT, left, [10.0, 10.12])}, NEAR, True)
    assert "2" not in found and found["1"][2] == right[3] and found["1"][3] == left[3]


def test_r515_with_join_back_false_the_back_pair_joins_nothing_as_r470():
    left = plan_from_book.ramp_heights([10.0, 10.12], 10.1)
    right = plan_from_book.ramp_heights([10.12, 10.24], 10.25)
    assert plan_from_book.joint_levels({"1": (LEFT, left, [10.0, 10.12]), "2": (RIGHT, right, [10.12, 10.24])}, NEAR, False) == {}


def test_r515_corners_are_grouped_on_the_mm_the_plan_writes():
    # 4.6 and 5.4 mm apart on the raw points read 5 mm on the whole mm of the plan: both are one joint, as the check row of R471 reads them, and 5.6 mm reads 6
    for dx, near in ((0.0046, True), (0.0054, True), (0.0056, False)):
        right = [(x + dx, y) for x, y in RIGHT]
        found = plan_from_book.joint_levels({"1": (LEFT, [10.0, 10.12, 10.15, 10.03], [10.0, 10.12]), "2": (right, [10.2, 10.3, 10.4, 10.5], [10.2, 10.3])}, NEAR, True)
        assert ("2" in found) is near


def test_r515_the_rule_of_config_turns_on_under_its_modes_and_refuses_a_bad_block():
    settings = project_profile.load_settings()
    block = settings.values["floors"]["rampJoint"]
    assert block["join"] is True and block["joinBack"] is True and block["nearM"] == NEAR and block["slopePercent"] == 0.5
    rule = plan_from_book.joint_rule(settings, {"mode": "rev6"})
    assert rule == {"near": NEAR, "back": True, "slope": 0.5}
    # The past job proof breaks with the joint on, so config keeps the past job off (R515)
    assert block["modes"] == ["rev6"] and plan_from_book.joint_rule(settings, {"mode": "roshnPastJob"}) is None
    assert plan_from_book.joint_rule(settings, None) is None
    off = copy.deepcopy(settings)
    off.values["floors"]["rampJoint"]["join"] = False
    assert plan_from_book.joint_rule(off, {"mode": "rev6"}) is None
    for name, bad, line in (("modes", "rev6", "floors.rampJoint.modes"), ("modes", ["rev5"], "floors.rampJoint.modes"), ("joinBack", 1, "floors.rampJoint.joinBack"),
                            ("slopePercent", -1, "floors.rampJoint.slopePercent"), ("slopePercent", "0.5", "floors.rampJoint.slopePercent"), ("slopePercent", True, "floors.rampJoint.slopePercent")):
        refused = copy.deepcopy(settings)
        refused.values["floors"]["rampJoint"][name] = bad
        with pytest.raises(project_profile.ProfileError, match=line):
            plan_from_book.joint_rule(refused, {"mode": "rev6"})


def test_r515_shared_ramps_names_the_plots_of_a_shared_front_and_of_a_shared_back():
    front = {"1": {"unit": "A", "front": [1.0, 1.1], "corners": LEFT}, "2": {"unit": "A", "front": [1.1, 1.2], "corners": RIGHT}, "9": {"unit": "A", "front": [1.0, 1.1], "corners": FAR},
             "8": {"unit": None, "front": None, "corners": None}}
    assert plan_from_book.shared_ramps(front, {"near": NEAR, "back": True, "slope": 0.5}) == ({"1", "2"}, {"1", "2"})
    # A ramp that shares only its front corner: the back pair apart
    skew = [(12.0, 0.0), (24.0, 0.0), (24.0, 5.52), (12.5, 5.52)]
    assert plan_from_book.shared_ramps({"1": front["1"], "2": dict(front["2"], corners=skew)}, {"near": NEAR, "back": True, "slope": 0.5}) == ({"1", "2"}, set())
    assert plan_from_book.shared_ramps(front, None) == (set(), set())


def row_levels(built, numbers):
    return {number: [corner[2] for corner in built["plan"][number]["floors"]["ramp"]["corners"]["value"]] for number in numbers}


def test_r515_a_row_under_the_mode_holds_one_level_at_every_shared_corner_and_its_ramps_stay_planes(monkeypatch):
    monkeypatch.setattr(project_profile, "load_settings", joined)
    built = made_build(monkeypatch, {})
    assert built["trace"]["rampJoint"] == NEAR
    levels = row_levels(built, ("1", "2", "3"))
    assert levels["1"][1] == levels["2"][0] and levels["2"][1] == levels["3"][0]
    assert levels["1"][2] == levels["2"][3] and levels["2"][2] == levels["3"][3]
    for z in levels.values():
        # The rise over the depth of 5.52 m at the slope of the joint, the same on both sides of each ramp
        assert z[2] - z[1] == pytest.approx(0.005 * 5.52, abs=2e-3) and z[3] - z[0] == pytest.approx(0.005 * 5.52, abs=2e-3)


def test_r516_an_entry_and_a_front_override_of_a_shared_ramp_are_left_with_why(monkeypatch):
    monkeypatch.setattr(project_profile, "load_settings", joined)
    base = made_build(monkeypatch, {})
    built = made_build(monkeypatch, {"2": {"rampFrontM": {"gapToRuleM": 0.05}, "entryM": {"gapToRuleM": 0.2}}})
    assert row_levels(built, ("1", "2", "3")) == row_levels(base, ("1", "2", "3"))
    assert built["plan"]["2"]["levels"]["entry"]["value"] == base["plan"]["2"]["levels"]["entry"]["value"]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert made["2"]["notApplied"] == {"entryM": plan_from_book.JOINT_LEFT, "rampFrontM": plan_from_book.JOINT_LEFT}
    assert "entryM" not in made["2"]["applied"] and "rampFrontM" not in made["2"]["applied"]
