import pytest

from project_profile import ROOT, ProfileError, load_profile, load_settings, profile_path

PROFILE = profile_path(load_settings())


def test_r62_profile_file_loads_with_cluster_q01_of_four_units_and_front_max_two(tmp_path):
    profile = load_profile(PROFILE)
    assert len(profile.get("clusters.Q01")) == 4
    assert profile.get("slopes_percent.front_max") == 2.0
    assert profile.get("cluster_split_positions") == [2, 3]
    assert profile.get("cluster_split_meaning") == "wall after unit n, counted from 1, 4-unit clusters only"
    assert profile.get("plot.vl4_width") == "derive"
    with pytest.raises(ProfileError, match="slopes_percent.no_such_value"):
        profile.get("slopes_percent.no_such_value")
    # A null is an answer (not supplied yet) and a missing key is an error
    unset = tmp_path / "unset.json"
    unset.write_text('{"value": null}', encoding="utf-8")
    assert load_profile(unset).get("value") is None


def test_r62_a_key_that_appears_twice_in_one_object_raises_and_names_the_key(tmp_path):
    doubled = tmp_path / "doubled.json"
    doubled.write_text('{"plot": {"long_side": 25000, "th1_width": 8000, "long_side": 24000}}', encoding="utf-8")
    with pytest.raises(ProfileError, match="doubled.json has a duplicate key long_side"):
        load_profile(doubled)


def test_r62_a_length_in_mm_reads_as_metres_and_a_profile_without_mm_is_refused(tmp_path):
    profile = load_profile(PROFILE)
    assert profile.metres("plot.long_side") == 25.0
    assert profile.metres("levels.ground_ssl") == -0.075
    assert profile.metres("steps.riser_height") is None
    with pytest.raises(ProfileError, match="plot.vl4_width is not a number"):
        profile.metres("plot.vl4_width")
    with pytest.raises(ProfileError, match="units_mm is not a number"):
        profile.metres("units_mm")
    metres = tmp_path / "metres.json"
    metres.write_text('{"units_mm": false, "plot": {"long_side": 25}}', encoding="utf-8")
    with pytest.raises(ProfileError, match="does not say its lengths are in mm"):
        load_profile(metres).metres("plot.long_side")


def test_r63_settings_without_a_profile_path_are_refused_and_a_relative_path_starts_at_the_repo_root(tmp_path):
    empty = tmp_path / "project.json"
    empty.write_text('{"profile": null}', encoding="utf-8")
    with pytest.raises(ProfileError, match="project.json has no profile path"):
        profile_path(load_settings(empty))
    empty.write_text('{"profile": "rules/x.json"}', encoding="utf-8")
    assert profile_path(load_settings(empty)) == ROOT / "rules" / "x.json"
    assert profile_path(load_settings()).is_file()
