"""R515, R516. Under a mode of floors.rampJoint.modes and with an applied override list of no entry a back ramp corner two or more plots share holds one
level and the door level of each such plot is the road at front plus floors.rampJoint.slopePercent over the ramp depth, with any entry the build is the one of no joint,
on made up ramps and the made up row of test_level_floor_steps.py built in code (R10), which R515_SharedBackCornerTests.cs holds the same."""
import copy

import pytest

import check_rows
import plan_from_book
import project_profile
from test_level_floor_steps import made_build
from test_r407_design_basis import basis_settings, rev6_build

NEAR = 0.005
LEFT = [(0.0, 0.0), (12.0, 0.0), (12.0, 5.52), (0.0, 5.52)]
RIGHT = [(12.0, 0.0), (24.0, 0.0), (24.0, 5.52), (12.0, 5.52)]
FAR = [(100.0, 0.0), (112.0, 0.0), (112.0, 5.52), (100.0, 5.52)]
LOAD = project_profile.load_settings


def joined(*args, **kwargs):
    """The project settings with the joint on under the past job of the made up row, which config keeps off (R515)."""
    settings = LOAD(*args, **kwargs)
    settings.values["floors"]["rampJoint"].update({"join": True, "joinBack": True, "modes": list(settings.get("designBasis.modes")), "slopePercent": 0.5})
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
    rule = plan_from_book.joint_rule(settings, {"mode": "rev6"}, {})
    assert rule == {"near": NEAR, "back": True, "slope": 0.5}
    # The past job proof breaks with the joint on, so config keeps the past job off (R515)
    past = next(mode for mode in settings.get("designBasis.modes") if mode != "rev6")
    assert block["modes"] == ["rev6"] and plan_from_book.joint_rule(settings, {"mode": past}, {}) is None
    assert plan_from_book.joint_rule(settings, None, {}) is None
    off = copy.deepcopy(settings)
    off.values["floors"]["rampJoint"]["join"] = False
    assert plan_from_book.joint_rule(off, {"mode": "rev6"}, {}) is None
    for name, bad, line in (("modes", "rev6", "floors.rampJoint.modes"), ("modes", ["rev5"], "floors.rampJoint.modes"), ("joinBack", 1, "floors.rampJoint.joinBack"),
                            ("slopePercent", -1, "floors.rampJoint.slopePercent"), ("slopePercent", "0.5", "floors.rampJoint.slopePercent"), ("slopePercent", True, "floors.rampJoint.slopePercent")):
        refused = copy.deepcopy(settings)
        refused.values["floors"]["rampJoint"][name] = bad
        with pytest.raises(project_profile.ProfileError, match=line):
            plan_from_book.joint_rule(refused, {"mode": "rev6"}, {})


def test_r515_shared_backs_names_the_plots_of_a_shared_back_corner():
    joint = {"near": NEAR, "back": True, "slope": 0.5}
    front = {"1": {"unit": "A", "front": [1.0, 1.1], "corners": LEFT}, "2": {"unit": "A", "front": [1.1, 1.2], "corners": RIGHT}, "9": {"unit": "A", "front": [1.0, 1.1], "corners": FAR},
             "8": {"unit": None, "front": None, "corners": None}}
    assert plan_from_book.shared_backs(front, joint) == {"1", "2"}
    # A ramp that shares only its front corner: the back pair apart
    skew = [(12.0, 0.0), (24.0, 0.0), (24.0, 5.52), (12.5, 5.52)]
    assert plan_from_book.shared_backs({"1": front["1"], "2": dict(front["2"], corners=skew)}, joint) == set()
    assert plan_from_book.shared_backs(front, None) == set()
    assert plan_from_book.shared_backs(front, dict(joint, back=False)) == set()


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


def test_r515_under_rev6_a_two_percent_unit_in_a_row_takes_the_slope_of_the_joint_and_its_door_level_type_and_ffl_follow(monkeypatch):
    door = {"source": "roadPlusSlope", "slopePercent": {"default": 0.5, "VD2": 2.0}}
    built = rev6_build(monkeypatch, basis_settings(mode="rev6", join=True, door=door))
    one = built["levels"]["1"]
    # 1, a VD2, shares its back corner with 2, so it takes 0.5 percent and not the 2 percent of a VD2, by the slope of the joint in config
    assert one["slopePercent"] == pytest.approx(0.5, abs=1e-12) and built["floors"]["1"]["rampType"] == "Ramps 0.5%"
    assert built["plan"]["1"]["floors"]["ramp"]["type"]["value"] == "Ramps 0.5%"
    levels = row_levels(built, ("1", "2"))
    assert levels["1"][2] == levels["2"][3]
    # With the joint off the same unit holds its 2 percent and its back corners stand apart
    apart = rev6_build(monkeypatch, basis_settings(mode="rev6", door=door))
    assert apart["levels"]["1"]["slopePercent"] == pytest.approx(2.0, abs=1e-12)
    assert apart["levels"]["1"]["ffl"] > built["levels"]["1"]["ffl"]
    far = row_levels(apart, ("1", "2"))
    assert abs(far["1"][2] - far["2"][3]) > 0.05


def test_r515_r517_r518_the_joint_with_the_pull_holds_one_level_at_each_pulled_corner_and_the_rows_pass(monkeypatch):
    door = {"source": "roadPlusSlope", "slopePercent": {"default": 0.5, "VD2": 2.0}}
    settings = basis_settings(mode="rev6", join=True, door=door)
    built = rev6_build(monkeypatch, settings, pull=0.002)
    assert built["trace"]["rampJoint"] == settings.get("floors.rampJoint.nearM") and built["trace"]["pull"] == 0.002
    plan = {number: built["plan"][number]["floors"]["ramp"]["corners"]["value"] for number in ("1", "2", "3")}
    # The corners stand pulled, 4 mm apart across each shared edge, and hold one level
    assert (plan["1"][1][0], plan["2"][0][0]) == (11.998, 12.002) and (plan["2"][1][0], plan["3"][0][0]) == (23.998, 24.002)
    assert plan["1"][1][2] == plan["2"][0][2] and plan["2"][1][2] == plan["3"][0][2]
    assert plan["1"][2][2] == plan["2"][3][2] and plan["2"][2][2] == plan["3"][3][2]
    limits = check_rows.joint_limits(settings)
    rows = check_rows.joint_rows(["1", "2", "3"], check_rows.ramp_corners({"plots": built["plots"]}), {}, settings.get("checkRows"), limits)
    # 1 shares two corners with 2, 2 two with 1 and two with 3, and every row reads a gap of 0 and passes
    assert [len(rows[number]) for number in ("1", "2", "3")] == [2, 4, 4]
    for made in rows.values():
        for row in made:
            assert row["toolValue"] == 0.0 and ", Pass," in row["why"]


def test_r516_an_entry_of_the_list_turns_the_joint_off_for_the_build():
    settings = project_profile.load_settings()
    assert plan_from_book.joint_rule(settings, {"mode": "rev6"}, {"2": {"entryM": {"gapToRuleM": 0.2}}}) is None
    # A list that holds no entry of the job loads as no plot, the new project of R487, and keeps the joint
    assert plan_from_book.joint_rule(settings, {"mode": "rev6"}, {}) is not None


def test_r516_with_an_entry_a_rev6_build_is_the_one_of_no_joint_and_the_overrides_apply(monkeypatch):
    door = {"source": "roadPlusSlope", "slopePercent": {"default": 0.5, "VD2": 2.0}}
    overrides = {"2": {"rampFrontM": {"gapToRuleM": 0.05}, "entryM": {"gapToRuleM": 0.2}}}
    built = rev6_build(monkeypatch, basis_settings(mode="rev6", join=True, door=door), overrides=overrides)
    off = rev6_build(monkeypatch, basis_settings(mode="rev6", join=False, door=door), overrides=overrides)
    assert built["trace"]["rampJoint"] is None and built["trace"]["jointBack"] == set()
    assert row_levels(built, ("1", "2", "3")) == row_levels(off, ("1", "2", "3"))
    assert built["plan"]["2"]["levels"]["entry"]["value"] == off["plan"]["2"]["levels"]["entry"]["value"]
    assert built["levels"]["1"]["slopePercent"] == pytest.approx(2.0, abs=1e-12)
    made = built["overrides"]["2"]
    assert "entryM" in made["applied"] and "rampFrontM" in made["applied"] and made["notApplied"] == {}
