import json

import pytest

import levels
import project_profile

REAL = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


def dummy_profile(tmp_path, **steps):
    values = {
        "units_mm": True,
        "levels": {"ground_ssl": -75},
        "steps": {
            "front_min": 1,
            "front_max": 3,
            "count_rule": "under 0.205 is 1, 0.205 to under 0.38 is 2, 0.38 and over is 3",
            "legend": [[0.10, 1], [0.54, 3]],
            "base_height": {"default": 100, "DP2": 200},
            "base_height_source": "past job schedules, not confirmed by the architect",
            **steps,
        },
    }
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(values), encoding="utf-8")
    return project_profile.load_profile(path)


def entries_of(*pairs):
    return {plot: {"unit": unit, "entry": entry} for plot, unit, entry in pairs}


def by_plot(rows):
    return {row["plot"]: row for row in rows}


def test_r67_the_count_rule_is_read_from_the_profile_text_and_a_height_on_a_limit_counts_with_the_band_that_starts_there():
    rule = levels.count_rule(REAL)
    assert rule == ([(0.205, 1), (0.38, 2)], 3)
    assert [levels.steps_for(height, rule) for height in (0.0, 0.2049, 0.205, 0.3799, 0.38, 0.9)] == [1, 1, 2, 2, 3, 3]


def test_r67_every_pair_of_the_profile_legend_gives_its_step_count():
    rule = levels.count_rule(REAL)
    legend = REAL.get("steps.legend")
    assert len(legend) == 6
    assert [(height, steps) for height, steps in legend if levels.steps_for(height, rule) != steps] == []


@pytest.mark.parametrize("text", ["under 0.2 is 1", "under 0.38 is 1, 0.205 to under 0.38 is 2, 0.38 and over is 3", "under 0.4 is 1, 0.4 to under 0.2 is 2, 0.2 and over is 3", None, 7])
def test_r67_a_count_rule_that_does_not_have_the_agreed_form_is_refused(tmp_path, text):
    profile = dummy_profile(tmp_path, count_rule=text)
    with pytest.raises(project_profile.ProfileError, match="steps.count_rule"):
        levels.count_rule(profile)


def test_r67_the_base_height_of_a_unit_is_its_own_or_the_default_in_metres(tmp_path):
    profile = dummy_profile(tmp_path)
    assert levels.base_height(profile, "DP2") == 0.2
    assert levels.base_height(profile, "VL4") == 0.1
    assert levels.base_height(profile, "default") == 0.1
    assert levels.base_height(profile, None) == 0.1
    assert [levels.base_height(REAL, unit) for unit in ("VD2", "VD4", "DP2", "DP3", "DP4")] == [0.2] * 5
    assert [levels.base_height(REAL, unit) for unit in ("DP1", "DT1", "TH1", "VL4")] == [0.1] * 4


def test_r67_the_ffl_of_a_cluster_is_its_highest_entry_plus_the_base_height_of_its_end_units(tmp_path):
    profile = dummy_profile(tmp_path)
    entries = entries_of(("1", "DP2", 10.0), ("2", "TH1", 10.3), ("3", "TH1", 10.1), ("4", "DP2", 10.05))
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3", "4"], "code": "Q01"}], entries))
    assert {row["ffl"] for row in rows.values()} == {10.5}
    assert [rows[plot]["height"] for plot in "1234"] == [0.5, 0.2, 0.4, 0.45]
    assert [rows[plot]["steps"] for plot in "1234"] == [3, 1, 3, 3]
    assert {row["baseHeight"] for row in rows.values()} == {0.2}
    assert {row["cluster"] for row in rows.values()} == {"Q01"} and {row["size"] for row in rows.values()} == {4}
    # The middle units never decide the base height, only the two ends do
    middle = entries_of(("1", "TH1", 10.0), ("2", "DP2", 10.3), ("3", "DP2", 10.1), ("4", "TH1", 10.05))
    flat = levels.compute(profile, [{"plots": ["1", "2", "3", "4"]}], middle)
    assert {row["baseHeight"] for row in flat} == {0.1} and {row["ffl"] for row in flat} == {10.4}


def test_r67_end_units_with_different_base_heights_take_the_larger_one_and_say_so(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "DP2", 5.0), ("2", "TH1", 5.1)))
    assert {row["baseHeight"] for row in rows} == {0.2} and {row["ffl"] for row in rows} == {5.3}
    assert all(row["flags"] == ["end units have different base heights"] for row in rows)
    same = levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "DP2", 5.0), ("2", "DP2", 5.1)))
    assert all(row["flags"] == [] for row in same)


def test_r67_a_plot_in_no_cluster_is_a_cluster_of_one_and_the_ssl_is_the_ffl_minus_0_075(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [], entries_of(("7", "DP2", 20.1234567), ("8", "VL4", 20.0))))
    assert rows["7"]["size"] == 1 and rows["7"]["cluster"] is None
    assert (rows["7"]["entry"], rows["7"]["ffl"], rows["7"]["ssl"], rows["7"]["height"], rows["7"]["steps"]) == (20.1235, 20.323, 20.248, 0.1995, 1)
    assert (rows["8"]["ffl"], rows["8"]["ssl"], rows["8"]["height"], rows["8"]["steps"]) == (20.1, 20.025, 0.1, 1)


def test_r67_plot_numbers_may_be_numbers_and_the_analysis_cluster_shape_is_read_as_it_is(tmp_path):
    profile = dummy_profile(tmp_path)
    analysis = {"clusters": [{"code": "D02", "plots": ["11", "12"], "typologies": ["DP2", "DP2"], "mirrored": [None, None], "orderSure": True, "row": True}]}
    rows = levels.compute(profile, analysis["clusters"], entries_of((11, "DP2", 1.0), (12, "DP2", 1.1)))
    assert [(row["plot"], row["cluster"], row["ffl"]) for row in rows] == [("11", "D02", 1.3), ("12", "D02", 1.3)]


def test_r67_a_plot_without_an_entry_level_is_flagged_and_the_others_of_its_cluster_are_still_worked_out(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("1", "VL4", 3.0), ("3", "VL4", 3.2))))
    assert rows["2"]["flags"] == ["no entry level"] and rows["2"]["ffl"] is None and rows["2"]["steps"] is None
    assert rows["1"]["ffl"] == rows["3"]["ffl"] == 3.3
    nothing = levels.compute(profile, [{"plots": ["9"]}], {})
    assert nothing[0]["flags"] == ["no entry level"] and nothing[0]["notes"] == [levels.PAST_WORK_NOTE]


def test_r67_a_plot_in_two_clusters_is_refused(tmp_path):
    profile = dummy_profile(tmp_path)
    with pytest.raises(ValueError, match="plot 2 is in two clusters"):
        levels.compute(profile, [{"plots": ["1", "2"]}, {"plots": ["2", "3"]}], {})


def test_r67_steps_under_the_least_or_over_the_most_are_flagged_from_the_profile_limits(tmp_path):
    profile = dummy_profile(tmp_path, count_rule="under 0.2 is 0, 0.2 to under 0.3 is 2, 0.3 and over is 5")
    rows = by_plot(levels.compute(profile, [], entries_of(("1", "VL4", 1.0), ("2", "VL4", 1.0))))
    assert rows["1"]["steps"] == 0 and rows["1"]["flags"] == ["steps under 1"]
    profile = dummy_profile(tmp_path, count_rule="under 0.12 is 1, 0.12 to under 0.14 is 2, 0.14 and over is 5")
    rows = levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "VL4", 1.0), ("2", "VL4", 1.05)))
    assert [(row["steps"], row["flags"]) for row in rows] == [(5, ["steps over 3"]), (1, [])]
    # The profile count rule gives 1 to 3 steps for every height, so on the real profile no row is flagged
    real = levels.compute(REAL, [], entries_of(("1", "VL4", 1.0), ("2", "DP2", 1.0), ("3", "VL4", 1.0)))
    assert all(row["flags"] == [] for row in real)


def test_r67_every_row_carries_the_past_work_note_until_the_base_height_source_no_longer_says_not_confirmed(tmp_path):
    entries = entries_of(("1", "VL4", 3.0), ("2", "DP2", 3.0))
    rows = levels.compute(dummy_profile(tmp_path), [{"plots": ["1"]}], entries)
    assert [row["notes"] for row in rows] == [["base height from past work"]] * 2
    confirmed = dummy_profile(tmp_path, base_height_source="architect drawing 123, confirmed on site")
    assert [row["notes"] for row in levels.compute(confirmed, [], entries)] == [[], []]
    assert [row["notes"] for row in levels.compute(REAL, [], entries)] == [["base height from past work"]] * 2


def test_r67_the_real_profile_gives_the_base_heights_the_ffl_and_the_steps_of_a_made_up_cluster():
    entries = entries_of(("1", "DP4", 100.0), ("2", "TH3", 100.15), ("3", "TH3", 100.3), ("4", "DP4", 100.38))
    rows = by_plot(levels.compute(REAL, [{"plots": ["1", "2", "3", "4"], "code": "Q02"}], entries))
    assert {row["ffl"] for row in rows.values()} == {100.58} and {row["ssl"] for row in rows.values()} == {100.505}
    assert [rows[plot]["steps"] for plot in "1234"] == [3, 3, 2, 1]


def test_r67_a_height_that_float_noise_puts_a_hair_under_a_limit_still_takes_the_band_that_starts_there(tmp_path):
    profile = dummy_profile(tmp_path)
    # 150.38 minus 150.0 is 0.37999999999999545 in binary floats and 100.205 minus 100.0 is 0.20500000000000540 off the limit the other way
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "DP2", 150.0), ("2", "DP2", 150.18))))
    assert rows["1"]["height"] == 0.38 and rows["1"]["steps"] == 3
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "DP2", 100.0), ("2", "DP2", 100.005))))
    assert rows["1"]["height"] == 0.205 and rows["1"]["steps"] == 2
    rule = levels.count_rule(profile)
    assert [levels.steps_for(0.37999999999999545, rule), levels.steps_for(0.20499999999999, rule), levels.steps_for(0.2049, rule)] == [3, 2, 1]


def test_r67_the_closest_real_style_height_to_a_limit_is_not_moved_by_the_rounding_before_the_band(tmp_path):
    rule = levels.count_rule(dummy_profile(tmp_path))
    assert levels.steps_for(0.205 - 2.7e-5, rule) == 1 and levels.steps_for(0.205 + 2.7e-5, rule) == 2
    assert levels.steps_for(0.38 - 5.6e-5, rule) == 2 and levels.steps_for(0.38 + 5.6e-5, rule) == 3


@pytest.mark.parametrize("text", [
    "under 0.2.5 is 1, 0.2.5 to under 0.38 is 2, 0.38 and over is 3",
    "under 0.2 is 1, 0.2 to under 0.4 is 2, 0.5 and over is 3",
    "under 0.3 is 1, 0.3 to under 0.3 is 2, 0.3 and over is 3",
    "under 0.2 is 3, 0.2 to under 0.3 is 2, 0.3 and over is 1",
    "under 0.2 is 1, 0.2 to under 0.3 is 3, 0.3 and over is 2",
])
def test_r67_a_count_rule_with_a_bad_number_a_broken_limit_or_falling_steps_is_refused_in_one_error(tmp_path, text):
    with pytest.raises(project_profile.ProfileError, match="steps.count_rule"):
        levels.count_rule(dummy_profile(tmp_path, count_rule=text))


def test_r67_steps_equal_to_the_profile_limits_are_not_flagged_and_the_values_keep_four_decimals(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("1", "VL4", 10.0), ("2", "VL4", 10.4567), ("3", "VL4", 10.3))))
    assert [rows[plot]["steps"] for plot in "123"] == [3, 1, 2]
    assert [[flag for flag in rows[plot]["flags"] if flag.startswith("steps")] for plot in "123"] == [[], [], []]
    # The FFL is 10.557 after the rounding to the millimetre, so the height of the first plot is 0.557
    assert (rows["1"]["height"], rows["2"]["height"], rows["2"]["entry"], rows["1"]["ffl"], rows["1"]["baseHeight"]) == (0.557, 0.1003, 10.4567, 10.557, 0.1)


def test_r67_a_cluster_whose_end_unit_has_no_entry_level_says_so_on_every_row_of_it(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("2", "VL4", 3.0), ("3", "VL4", 3.1))))
    assert rows["1"]["flags"] == ["no entry level"]
    assert rows["2"]["flags"] == ["end unit has no entry level"] and rows["3"]["flags"] == ["end unit has no entry level"]
    complete = levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("1", "DP2", 3.0), ("2", "DP2", 3.0), ("3", "VL4", 3.1)))
    assert all("end unit has no entry level" not in row["flags"] for row in complete)


def test_r67_a_base_height_with_three_decimals_is_written_with_three_decimals(tmp_path):
    profile = dummy_profile(tmp_path, base_height={"default": 125})
    rows = levels.compute(profile, [], entries_of(("1", "VL4", 10.0)))
    assert (rows[0]["baseHeight"], rows[0]["ffl"], rows[0]["height"]) == (0.125, 10.125, 0.125)


def test_r67_the_ffl_is_rounded_to_the_millimetre_before_the_height_is_taken(tmp_path):
    profile = dummy_profile(tmp_path)
    # 100.0049 plus 0.2 is 100.2049, which would give a height of 0.2049 and one step. The sheet writes 100.205, a height of 0.205 and two steps
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "DP2", 100.0), ("2", "DP2", 100.0049))))
    assert (rows["1"]["ffl"], rows["1"]["height"], rows["1"]["steps"]) == (100.205, 0.205, 2)
    assert (rows["2"]["ffl"], rows["2"]["height"], rows["2"]["ssl"]) == (100.205, 0.2001, 100.13)


def test_r67_the_height_span_is_the_lowest_and_highest_height_of_the_profile_legend():
    assert levels.height_span(REAL) == (0.10, 0.54)


def test_r67_a_height_outside_the_legend_span_is_flagged_and_one_on_the_edge_is_not(tmp_path):
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3", "4"]}], entries_of(("1", "VL4", 10.0), ("2", "VL4", 9.56), ("3", "VL4", 9.559), ("4", "VL4", 9.9))))
    assert {plot: rows[plot]["flags"] for plot in "1234"} == {"1": [], "2": [], "3": ["height over 0.54 m"], "4": []}
    low = by_plot(levels.compute(dummy_profile(tmp_path, base_height={"default": 90}), [], entries_of(("1", "VL4", 10.0))))
    assert low["1"]["flags"] == ["height under 0.1 m"] and low["1"]["height"] == 0.09


def test_r67_the_height_is_compared_with_the_span_to_the_millimetre_so_the_rounding_of_the_ffl_flags_nothing(tmp_path):
    profile = dummy_profile(tmp_path)
    # 10.0004 plus 0.1 is 10.1004, which the FFL takes to 10.1, so the top plot has the height 0.0996 and that is 0.1 to the millimetre
    top = by_plot(levels.compute(profile, [], entries_of(("1", "VL4", 10.0004))))
    assert (top["1"]["ffl"], top["1"]["height"], top["1"]["flags"]) == (10.1, 0.0996, [])
    # 0.5404 is 0.540 to the millimetre and is not over the span, 0.5406 is 0.541 and is
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("1", "VL4", 10.0), ("2", "VL4", 9.5596), ("3", "VL4", 9.5594))))
    assert [rows[plot]["height"] for plot in "123"] == [0.1, 0.5404, 0.5406]
    assert [rows[plot]["flags"] for plot in "123"] == [[], [], ["height over 0.54 m"]]
    low = by_plot(levels.compute(dummy_profile(tmp_path, base_height={"default": 99.6}), [], entries_of(("1", "VL4", 10.0))))
    assert (low["1"]["ffl"], low["1"]["height"], low["1"]["flags"]) == (10.1, 0.1, [])
    # A base of 99.4 gives the FFL 10.099 and the height 0.099, which is under the span
    lower = by_plot(levels.compute(dummy_profile(tmp_path, base_height={"default": 99.4}), [], entries_of(("1", "VL4", 10.0))))
    assert (lower["1"]["height"], lower["1"]["flags"]) == (0.099, ["height under 0.1 m"])


def test_r67_the_height_flag_reads_its_span_from_the_profile(tmp_path):
    profile = dummy_profile(tmp_path, legend=[[0.15, 1], [0.3, 2]])
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "VL4", 10.0), ("2", "VL4", 9.6))))
    assert rows["1"]["flags"] == ["height under 0.15 m"] and rows["2"]["flags"] == ["height over 0.3 m"]
    single = by_plot(levels.compute(profile, [], entries_of(("3", "DP2", 10.0))))
    assert single["3"]["flags"] == []


@pytest.mark.parametrize("legend", [None, [], "0.1", [[0.5, 1], [0.1, 2]], [[True, 1], [0.5, 2]], [["0.1", 1]], [0.1, 0.5], [[0.1]]])
def test_r67_a_legend_that_is_not_a_list_of_height_and_steps_pairs_with_rising_heights_is_refused_in_one_error(tmp_path, legend):
    profile = dummy_profile(tmp_path, legend=legend)
    with pytest.raises(project_profile.ProfileError, match="steps.legend is not a list of"):
        levels.compute(profile, [], entries_of(("1", "VL4", 10.0)))


def test_r67_a_height_exactly_half_a_millimetre_off_the_span_is_inside_it_whatever_the_float_noise(tmp_path):
    profile = dummy_profile(tmp_path)
    # 119.3075 plus 0.1 is the tie 119.4075, R385 takes the FFL to 119.408 and the height is 0.1005, half a millimetre over 0.1 (Round ADDIN 4b)
    top = by_plot(levels.compute(profile, [], entries_of(("1", "VL4", 119.3075))))
    assert (top["1"]["ffl"], top["1"]["height"], top["1"]["flags"]) == (119.408, 0.1005, [])
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2", "3"]}], entries_of(("1", "VL4", 10.0), ("2", "VL4", 9.5595), ("3", "VL4", 9.5594))))
    assert [rows[plot]["height"] for plot in "123"] == [0.1, 0.5405, 0.5406]
    assert [rows[plot]["flags"] for plot in "123"] == [[], [], ["height over 0.54 m"]]
