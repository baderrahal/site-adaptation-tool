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


def made_split(levels, roads=None):
    """split_test on a made up cluster of four DP2 units with El @ Entry levels in row order and the road at front of each, none when left out."""
    profile = made_rules()
    key = profile.get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    lp = plan_rules.level_profile(profile, "parsons2024Rev6")
    entries = {plot: {"unit": "DP2", "entry": level, "rule": level} for plot, level in zip(CLUSTER, levels)}
    roles = {plot: {"plots": list(CLUSTER)} for plot in CLUSTER}
    return plan_rules.split_test(entries, dict(zip(CLUSTER, roads or [])), roles, SPLIT, key, profile, lp)


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
    # Q303 took the slope out of the test, so this case holds steps alone: plot 2 lies 0.51 m under plot 4 and plot 4 0.51 m under plot 5,
    # so neither wall leaves two parts that fit
    pieces, marks = made_split([LOW, LOW, HIGH, LOW + 1.02])
    assert pieces == {plot: CLUSTER for plot in CLUSTER} and marks == {plot: "noFit" for plot in CLUSTER}


def test_r414_the_slope_is_taken_on_the_unrounded_el_at_entry_and_a_slope_of_exactly_front_min_fits():
    road, depth = 10.12344, 5.52
    raw = road + 0.5 / 100.0 * depth
    front = [road, road]
    unrounded = design_basis.slope_of(raw, front, depth)
    rounded_first = design_basis.slope_of(common.fixed(raw, plan_rules.ENTRY_DECIMALS), front, depth)
    # Rounding El @ Entry to 4 decimals first turns 0.5 into 0.499, outside the manual, the unrounded one is 0.5 as the plan writes it
    assert common.fixed(unrounded, 3) == 0.5 and common.fixed(rounded_first, 3) == 0.499
    # Q303. The slope is no longer in the split test, the report by outside_manual holds it as before
    limits = {"front_min": 0.5, "front_max": 2.0}
    assert design_basis.outside_manual(unrounded, limits) is False and design_basis.outside_manual(rounded_first, limits) is True


def test_r414_a_cluster_of_another_size_or_with_a_plot_with_no_el_at_entry_is_not_tested():
    profile = made_rules()
    key = profile.get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    lp = plan_rules.level_profile(profile, "parsons2024Rev6")
    entries = {plot: {"unit": "DP2", "entry": LOW, "rule": LOW} for plot in CLUSTER[:3]}
    three = {plot: {"plots": CLUSTER[:3]} for plot in CLUSTER[:3]}
    roads = {plot: 10.0 for plot in CLUSTER}
    assert plan_rules.split_test(entries, roads, three, SPLIT, key, profile, lp) == ({}, {})
    four = {plot: {"plots": CLUSTER} for plot in CLUSTER[:3]}
    assert plan_rules.split_test(entries, roads, four, SPLIT, key, profile, lp) == ({}, {})


def test_r414_q304_a_row_rising_to_its_last_unit_counts_the_walls_from_there_and_the_lone_unit_stands_at_the_low_end():
    # After unit 3 from the high road end is after unit 1 in row order, named from the first unit as split_pattern names it
    pieces, marks = made_split([LOW, MID, MID, HIGH], roads=[10.0, 10.1, 10.2, 10.3])
    assert pieces == {"2": ["2"], "3": ["3", "4", "5"], "4": ["3", "4", "5"], "5": ["3", "4", "5"]} and marks == {plot: "walls 1" for plot in CLUSTER}
    assert marks["2"] == plan_rules.split_pattern([LOW, HIGH, HIGH, HIGH])


def test_r414_q304_a_row_falling_from_its_first_unit_counts_the_walls_from_there_and_the_lone_unit_stands_at_the_low_end():
    pieces, marks = made_split([HIGH, MID, MID, LOW], roads=[10.3, 10.2, 10.1, 10.0])
    assert pieces == {"2": ["2", "3", "4"], "3": ["2", "3", "4"], "4": ["2", "3", "4"], "5": ["5"]} and marks == {plot: "walls 3" for plot in CLUSTER}
    assert marks["2"] == plan_rules.split_pattern([HIGH, HIGH, HIGH, LOW])


def test_r414_q304_the_second_wall_counts_from_the_high_road_end_too_and_the_middle_is_the_middle_either_way():
    # Rising to the last unit: after unit 3 from there leaves plot 3 0.51 m under plot 5, after unit 2 from there is the middle
    pieces, marks = made_split([LOW, LOW, HIGH, HIGH], roads=[10.0, 10.1, 10.2, 10.3])
    assert pieces == {"2": ["2", "3"], "3": ["2", "3"], "4": ["4", "5"], "5": ["4", "5"]} and marks == {plot: "middle" for plot in CLUSTER}


def test_r414_q304_a_tie_or_no_road_counts_the_walls_from_the_first_unit():
    three = {"2": ["2", "3", "4"], "3": ["2", "3", "4"], "4": ["2", "3", "4"], "5": ["5"]}
    levels = [LOW, MID, MID, HIGH]
    assert made_split(levels, roads=[10.0, 10.1, 10.2, 10.0]) == (three, {plot: "walls 3" for plot in CLUSTER})
    # A rise under the 4 decimals the plan writes the road at is a tie
    assert made_split(levels, roads=[10.0, 10.1, 10.2, 10.00001]) == (three, {plot: "walls 3" for plot in CLUSTER})
    assert made_split(levels) == (three, {plot: "walls 3" for plot in CLUSTER})
    assert made_split(levels, roads=[None, 10.1, 10.2, 10.3]) == (three, {plot: "walls 3" for plot in CLUSTER})
    assert made_split(levels, roads=[10.0, 10.1, 10.2, None]) == (three, {plot: "walls 3" for plot in CLUSTER})


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
    # Q304 changes this case: the road of the made up row rises to plot 5, so the first wall cuts after plot 2 and not after plot 4
    assert [levels[plot]["splitTest"] for plot in CLUSTER] == ["walls 1"] * 4
    assert levels["2"]["piece"] == ["2"] and levels["5"]["piece"] == ["3", "4", "5"]
    assert levels["1"]["splitTest"] is None and levels["6"]["splitTest"] is None and levels["7"]["splitTest"] is None
    # Each part its own FFL by piece_top: plot 2 at 10.2076 plus 0.2, plot 5 at 10.65 plus 0.2
    assert plan["2"]["levels"]["ffl"]["value"] == common.fixed(10.2076 + 0.2, 3) and plan["5"]["levels"]["ffl"]["value"] == common.fixed(10.65 + 0.2, 3)
    # The slope of plot 2 is front_min as the plan writes it
    assert common.fixed(levels["2"]["slopePercent"], 3) == 0.5


def test_r414_q303_a_slope_outside_the_limits_no_longer_stops_a_fit_and_stays_reported_for_each_unit_of_the_cluster(monkeypatch, tmp_path):
    # Roads of the row 10.18, 10.30, 10.42, 10.54 over a ramp of 5.52 m: 3.08 percent on plots 2 to 4 and 3.623 percent on plot 5, all over
    # front_max. Plot 2 lies 0.59 m under the FFL of the cluster, 3 steps, so the cluster stays whole
    path = civil_csv(tmp_path, [("1", "10.1"), ("2", "10.35"), ("3", "10.47"), ("4", "10.59"), ("5", "10.74"), ("6", "10.7")])
    settings = basis_settings(mode="rev6", door={"source": "civilFile"}, civil={"path": str(path)})
    settings.values["designBasis"]["split"]["test"] = True
    built = rev6_build(monkeypatch, settings, clusters=FOUR)
    levels = built["levels"]
    assert [levels[plot]["splitTest"] for plot in CLUSTER] == ["none"] * 4 and levels["2"]["piece"] == CLUSTER
    assert [levels[plot]["steps"] for plot in CLUSTER] == [3, 3, 2, 1]
    listed = {item["plot"]: item["slopePercent"] for item in built["summary"]["slopeOutsideManual"]}
    assert set(CLUSTER) <= set(listed)
    assert all(listed[plot] == common.fixed(levels[plot]["slopePercent"], design_basis.SLOPE_DECIMALS) for plot in CLUSTER)
    assert built["summaryStep"][1]["summary"]["slopeOutsideManual"] == built["summary"]["slopeOutsideManual"]


def test_r414_q303_the_steps_stop_a_fit_where_every_slope_is_inside_the_limits(monkeypatch, tmp_path):
    # split_settings holds every slope of the cluster inside the limits and plot 2 at 4 steps when whole, so the steps alone split it
    built = rev6_build(monkeypatch, split_settings(tmp_path), clusters=FOUR)
    assert built["levels"]["2"]["splitTest"] == "walls 1"
    assert not {item["plot"] for item in built["summary"]["slopeOutsideManual"]} & set(CLUSTER)
    # At 1 percent each plot 2 lies 0.56 m under the FFL of the cluster, 3 steps, so the same row stays whole
    fit = [("1", "10.1"), ("2", "10.2352"), ("3", "10.3552"), ("4", "10.4752"), ("5", "10.5952"), ("6", "10.7")]
    settings = basis_settings(mode="rev6", door={"source": "civilFile"}, civil={"path": str(civil_csv(tmp_path, fit))})
    settings.values["designBasis"]["split"]["test"] = True
    built = rev6_build(monkeypatch, settings, clusters=FOUR)
    assert built["levels"]["2"]["splitTest"] == "none"
    assert not {item["plot"] for item in built["summary"]["slopeOutsideManual"]} & set(CLUSTER)


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
