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
    assert (rows["7"]["entry"], rows["7"]["ffl"], rows["7"]["ssl"], rows["7"]["height"], rows["7"]["steps"]) == (20.1235, 20.323, 20.248, 0.2, 1)
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
