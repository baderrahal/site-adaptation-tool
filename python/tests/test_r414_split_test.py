"""R414. The split of a cluster by slope and steps, manual Rev 06 2.2.B with 2.3.A and 2.4.A, on made up clusters of four DP2 units and on the
made up row of test_level_floor_steps.py with a cluster of plots 2 to 5, built in code (R10). R414_SplitTestTests.cs holds the same made up data."""
import pytest

import common
import design_basis
import plan_from_book
import plan_rules
import project_profile
from test_level_floor_steps import made_rules
from test_r407_design_basis import PAST_JOB, basis_settings, civil_csv, rev6_build

CLUSTER = ["2", "3", "4", "5"]
SPLIT = {"test": True, "units": 4, "walls": [3, 2]}
# The made up row with plots 2 to 5 one cluster of four DP2 units in row order
FOUR = [{"code": "Townhouse_made_up", "plots": CLUSTER, "source": "footprint"}]
# By hand on DP2, base 0.2 m and risers of 0.17 m: 0.51 m under the top of a piece is 4 steps, 0.255 m is 2 steps, steps.front_max is 3
LOW, MID, HIGH = 10.0, 10.255, 10.51


def made_split(levels, slopes=None):
    """split_test on a made up cluster of four DP2 units with El @ Entry levels in row order and a slope of 1 percent each, or slopes."""
    profile = made_rules()
    key = profile.get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    lp = plan_rules.level_profile(profile, "parsons2024Rev6")
    entries = {plot: {"unit": "DP2", "entry": level, "rule": level} for plot, level in zip(CLUSTER, levels)}
    roles = {plot: {"plots": list(CLUSTER)} for plot in CLUSTER}
    return plan_rules.split_test(entries, dict(zip(CLUSTER, slopes or [1.0] * 4)), roles, SPLIT, key, profile, lp)


def test_r414_a_cluster_whose_units_all_fit_stays_whole():
    pieces, marks = made_split([LOW, LOW, MID, MID])
    assert pieces == {plot: CLUSTER for plot in CLUSTER} and marks == {plot: "none" for plot in CLUSTER}


def test_r414_a_split_after_unit_3_is_tried_first_and_kept_when_both_parts_fit():
    # Whole, plot 2 lies 0.51 m under the top, 4 steps. After unit 3 and after unit 2 both fit, so the first of walls, after 3, is kept
    pieces, marks = made_split([LOW, LOW, MID, HIGH])
    assert pieces == {"2": ["2", "3", "4"], "3": ["2", "3", "4"], "4": ["2", "3", "4"], "5": ["5"]}
    assert marks == {plot: "walls 3" for plot in CLUSTER}


def test_r414_a_split_after_unit_2_when_after_unit_3_does_not_fit_is_the_middle():
    # After unit 3 plot 2 still lies 0.51 m under plot 4
    pieces, marks = made_split([LOW, LOW, HIGH, HIGH])
    assert pieces == {"2": ["2", "3"], "3": ["2", "3"], "4": ["4", "5"], "5": ["4", "5"]} and marks == {plot: "middle" for plot in CLUSTER}


def test_r414_a_cluster_no_split_fits_stays_whole_as_no_fit():
    # A slope outside front_min to front_max fits in no part, so no split helps and the cluster stays whole
    pieces, marks = made_split([LOW, LOW, MID, HIGH], slopes=[1.0, 1.0, 1.0, 2.5])
    assert pieces == {plot: CLUSTER for plot in CLUSTER} and marks == {plot: "noFit" for plot in CLUSTER}
    # Steps alone: plot 2 lies 0.51 m under plot 4 and plot 4 0.51 m under plot 5, so neither wall leaves two parts that fit
    assert made_split([LOW, LOW, HIGH, LOW + 1.02])[1]["2"] == "noFit"


def test_r414_the_slope_is_taken_on_the_unrounded_el_at_entry_and_a_slope_of_exactly_front_min_fits():
    road, depth = 10.12344, 5.52
    raw = road + 0.5 / 100.0 * depth
    front = [road, road]
    unrounded = design_basis.slope_of(raw, front, depth)
    rounded_first = design_basis.slope_of(common.fixed(raw, plan_rules.ENTRY_DECIMALS), front, depth)
    # Rounding El @ Entry to 4 decimals first turns 0.5 into 0.499, outside the manual, the unrounded one is 0.5 as the plan writes it
    assert common.fixed(unrounded, 3) == 0.5 and common.fixed(rounded_first, 3) == 0.499
    assert made_split([LOW] * 4, slopes=[unrounded] * 4)[1]["2"] == "none"
    assert made_split([LOW] * 4, slopes=[rounded_first] * 4)[1]["2"] == "noFit"
    assert made_split([LOW] * 4, slopes=[0.5, 0.5, 2.0, 2.0])[1]["2"] == "none"


def test_r414_a_cluster_of_another_size_or_with_a_plot_with_no_el_at_entry_is_not_tested():
    profile = made_rules()
    key = profile.get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    lp = plan_rules.level_profile(profile, "parsons2024Rev6")
    entries = {plot: {"unit": "DP2", "entry": LOW, "rule": LOW} for plot in CLUSTER[:3]}
    three = {plot: {"plots": CLUSTER[:3]} for plot in CLUSTER[:3]}
    slopes = {plot: 1.0 for plot in CLUSTER}
    assert plan_rules.split_test(entries, slopes, three, SPLIT, key, profile, lp) == ({}, {})
    four = {plot: {"plots": CLUSTER} for plot in CLUSTER[:3]}
    assert plan_rules.split_test(entries, slopes, four, SPLIT, key, profile, lp) == ({}, {})


def split_settings(tmp_path, mode="rev6", test=True):
    """config/project.json under mode with El @ Entry from a made up civil file and designBasis.split.test as test."""
    # Plot 2 at 0.5 percent over its road of 10.18 m, plot 5 near 2 percent over 10.54 m, 0.4424 m apart: 4 steps for plot 2 when whole
    path = civil_csv(tmp_path, [("1", "10.1"), ("2", "10.2076"), ("3", "10.3276"), ("4", "10.4476"), ("5", "10.65"), ("6", "10.7")])
    settings = basis_settings(mode=mode, door={"source": "civilFile"}, civil={"path": str(path)})
    settings.values["designBasis"]["split"]["test"] = test
    return settings


def test_r414_the_build_splits_the_cluster_and_the_levels_step_marks_each_plot(monkeypatch, tmp_path):
    built = rev6_build(monkeypatch, split_settings(tmp_path), clusters=FOUR)
    levels, plan = built["levels"], built["plan"]
    assert [levels[plot]["splitTest"] for plot in CLUSTER] == ["walls 3"] * 4
    assert levels["2"]["piece"] == ["2", "3", "4"] and levels["5"]["piece"] == ["5"]
    assert levels["1"]["splitTest"] is None and levels["6"]["splitTest"] is None and levels["7"]["splitTest"] is None
    # Each part its own FFL by piece_top: plot 4 at 10.4476 plus 0.2, plot 5 at 10.65 plus 0.2
    assert plan["2"]["levels"]["ffl"]["value"] == common.fixed(10.4476 + 0.2, 3) and plan["5"]["levels"]["ffl"]["value"] == common.fixed(10.65 + 0.2, 3)
    # The slope of plot 2 is front_min as the plan writes it
    assert common.fixed(levels["2"]["slopePercent"], 3) == 0.5


def test_r414_the_switch_off_gives_the_pieces_of_key_pieces_and_a_null_split_test(monkeypatch, tmp_path):
    off = rev6_build(monkeypatch, split_settings(tmp_path, test=False), clusters=FOUR)
    assert all(off["levels"][plot]["splitTest"] is None for plot in off["levels"])
    assert off["levels"]["2"]["piece"] == CLUSTER and off["plan"]["2"]["levels"]["ffl"]["value"] == common.fixed(10.65 + 0.2, 3)
    # Plot 2 holds 4 steps on the whole cluster, which the switch off leaves as it is
    assert off["levels"]["2"]["steps"] == 4


def test_r414_the_past_job_gives_a_null_split_test_and_the_plan_of_the_switch_off(monkeypatch, tmp_path):
    past = rev6_build(monkeypatch, split_settings(tmp_path, mode=PAST_JOB), clusters=FOUR)
    plain = rev6_build(monkeypatch, split_settings(tmp_path, mode=PAST_JOB, test=False), clusters=FOUR)
    assert all(past["levels"][plot]["splitTest"] is None for plot in past["levels"])
    assert past["levels"] == plain["levels"] and past["plots"] == plain["plots"]
    assert design_basis.basis_of(split_settings(tmp_path, mode=PAST_JOB)) == {"mode": PAST_JOB}


def test_r414_a_test_that_is_no_bool_or_walls_outside_the_cluster_are_refused(tmp_path):
    settings = split_settings(tmp_path)
    settings.values["designBasis"]["split"]["test"] = "yes"
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(settings)
    assert str(error.value) == "designBasis.split.test must be true or false, it holds 'yes' (R414)"
    for units, walls in ((4, [4]), (4, []), (True, [1]), (1, [1]), (4, "3")):
        settings = split_settings(tmp_path)
        settings.values["designBasis"]["split"].update(units=units, walls=walls)
        with pytest.raises(project_profile.ProfileError) as error:
            design_basis.basis_of(settings)
        assert str(error.value) == WALLS_LINE
    assert design_basis.basis_of(split_settings(tmp_path))["split"] == SPLIT
    assert design_basis.splits(design_basis.basis_of(split_settings(tmp_path, test=False))) is False


WALLS_LINE = ("designBasis.split.units must be a whole number of 2 or more and designBasis.split.walls a list of the walls it tries in order, each a "
              "wall after a unit inside the cluster (R414)")
