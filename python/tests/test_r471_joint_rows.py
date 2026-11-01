"""R471. The check row of a ramp joint, check_rows.joint_rows of python/check_rows.py, on ramp corners made up in code (R10). R471_JointRowTests.cs
of the C# holds the same corners and the same rows, so the two sides give one check table."""
import copy

import pytest

import check_rows
import project_profile

SETTINGS = project_profile.load_settings()
BLOCK = SETTINGS.get("checkRows")
LIMITS = check_rows.joint_limits(SETTINGS)
PAST_WHY = BLOCK["fields"]["rampJoint"]["why"]


def ramp(*corners):
    return {"floors": {"ramp": {"corners": {"value": [list(corner) for corner in corners]}}}}


def plan_of(plots):
    return {"plots": [dict(ramp(*corners), plot=number) for number, corners in plots.items()]}


# Plot 1 and 2 share their front corners 1 and 0, the back corners of the two lie 50 mm apart, plot 3 stands alone
SHARED = {
    "1": [(0.0, 0.0, 10.0), (10.0, 0.0, 10.2), (10.0, 6.0, 10.5), (0.0, 6.0, 10.3)],
    "2": [(10.0, 0.0, 10.2), (20.0, 0.0, 10.4), (20.0, 6.0, 10.7), (10.05, 6.0, 10.6)],
    "3": [(50.0, 0.0, 11.0), (60.0, 0.0, 11.0), (60.0, 6.0, 11.3), (50.0, 6.0, 11.3)],
}


def row(corner, others, gap, verdict, use="Rule", limit=0.001):
    return {"check": "Floor", "field": "rampJoint", "from": "joint", "use": use, "rule": "R471", "toolValue": gap,
            "past": {"writes": None, "value": None, "why": PAST_WHY},
            "why": "corner %d shared with plots %s, gap %.3f m, %s, passes at most %.3f m" % (corner, others, gap, verdict, limit)}


def rows_of(plots, numbers=None, skip=None, limits=LIMITS):
    return check_rows.joint_rows(numbers or sorted(plots), check_rows.ramp_corners(plan_of(plots)), skip or {}, BLOCK, limits)


def test_r471_limits_read_from_config():
    assert LIMITS == (SETTINGS.get("floors.rampJoint.nearM"), SETTINGS.get("floors.rampJoint.passGapM"))
    for broken in ({}, {"nearM": 0, "passGapM": 0.001}, {"nearM": 0.01, "passGapM": -1}, {"nearM": "0.01", "passGapM": 0.001}):
        settings = copy.deepcopy(SETTINGS.values)
        settings["floors"]["rampJoint"] = broken
        with pytest.raises(project_profile.ProfileError, match="R471"):
            check_rows.joint_limits(settings)


def test_r471_joint_at_zero_gap_passes():
    found = rows_of(SHARED)
    assert found == {"1": [row(2, "2", 0.0, "Pass")], "2": [row(1, "1", 0.0, "Pass")], "3": []}
    assert isinstance(found["1"][0]["toolValue"], float)


def test_r471_joint_at_two_mm_fails():
    plots = copy.deepcopy(SHARED)
    plots["2"][0] = (10.0, 0.0, 10.202)
    found = rows_of(plots)
    assert found["1"] == [row(2, "2", 0.002, "Fail")]
    assert found["2"] == [row(1, "1", 0.002, "Fail")]
    assert rows_of(plots, limits=(LIMITS[0], 0.002))["1"] == [row(2, "2", 0.002, "Pass", limit=0.002)]


def test_r471_corner_eleven_mm_away_has_no_row():
    plots = copy.deepcopy(SHARED)
    plots["2"][0] = (10.011, 0.0, 10.2)
    assert rows_of(plots) == {"1": [], "2": [], "3": []}
    assert rows_of(plots, limits=(0.02, LIMITS[1]))["2"] == [row(1, "1", 0.0, "Pass")]


def test_r471_chain_of_three_plots_is_one_joint():
    # 1 and 3 lie 12 mm apart, each 6 mm from 2, so the chain through 2 joins all three
    plots = {
        "1": [(0.0, 0.0, 5.0), (10.0, 0.0, 5.001), (10.0, 6.0, 5.3), (0.0, 6.0, 5.3)],
        "2": [(10.006, 0.0, 5.0), (20.0, 0.0, 5.1), (20.0, 6.0, 5.4), (10.05, 6.0, 5.4)],
        "3": [(10.012, 0.0, 5.0005), (10.012, -6.0, 5.2), (30.0, -6.0, 5.2), (30.0, 0.0, 5.0)],
    }
    found = rows_of(plots)
    assert found["1"] == [row(2, "2, 3", 0.001, "Pass")]
    assert found["2"] == [row(1, "1, 3", 0.001, "Pass")]
    assert found["3"] == [row(1, "1, 2", 0.001, "Pass")]


def test_r471_skip_plot_reads_skip_and_scope_holds_only_its_plots():
    found = rows_of(SHARED, numbers=["2", "3"], skip={"2": "a made up reason"})
    assert found == {"2": [row(1, "1", 0.0, "Pass", use="Skip")], "3": []}


def test_r471_joint_rows_sort_after_the_floor_rows_of_a_plot():
    joints = rows_of(SHARED)
    infos = {number: {"unit": "VD2", "nbh": "N1", "skip": None, "applied": [], "notApplied": {}, "lacks": ["floors.yard"]} for number in SHARED}
    made = check_rows.plot_rows(sorted(SHARED), infos, [], [], {}, BLOCK, joints)
    assert [(item["check"], item["field"], item["from"]) for item in made["1"]] == [("Floor", "floors.yard", "missing"), ("Floor", "rampJoint", "joint")]
    assert check_rows.FROM_ORDER[-1] == "joint"


def test_r471_checks_adds_the_joint_rows_of_the_plan():
    plan = plan_of(SHARED)
    infos, rows_by_plot, _, counted = check_rows.checks(["1", "2", "3"], {}, plan, {"skip": [{"plot": "1", "reason": "a made up reason"}]}, ["parsons"], [], SETTINGS)
    assert rows_by_plot["1"] == [row(2, "2", 0.0, "Pass", use="Skip")]
    assert rows_by_plot["2"] == [row(1, "1", 0.0, "Pass")]
    assert counted["Floor"] == 2
