import json

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


MANUAL_TYPOLOGIES = {
    "C10": ("villa", "detached", "C10", 16), "VL4": ("villa", "detached", "VL4", 12), "VL6": ("villa", "detached", "VL3", 16),
    "VL3": ("villa", "detached", "VL3", 12), "VD1": ("villa", "detached", "DP1", 10), "VD2": ("villa", "detached", "DP2", 12),
    "VD4": ("villa", "detached", "DP4", 12), "DP1": ("duplex", "one_side", "DP1", 10), "DP2": ("duplex", "one_side", "DP2", 12),
    "DP3": ("duplex", "one_side", "DP3", 10), "DP4": ("duplex", "one_side", "DP4", 12), "DT1": ("duplex", "one_side", "TH1", 10),
    "DT3": ("duplex", "one_side", "TH3", 10), "TH1": ("townhouse", "both_sides", "TH1", 8), "TH3": ("townhouse", "both_sides", "TH3", 8),
    "TH1-IM": ("townhouse", "both_sides", "TH1", 8),
}
MANUAL_CLUSTERS = {
    "M01": ["DP1", "DP1"], "M02": ["DP2", "DP2"], "M03": ["DP3", "DP3"], "M04": ["DP4", "DP4"], "M05": ["DT1", "DT1"], "M06": ["DT3", "DT3"],
    "M07": ["DP1", "TH1", "TH1", "DP1"], "M08": ["DP1", "TH3", "TH3", "DP1"], "M09": ["DP2", "TH1", "TH1", "DP2"],
    "M10": ["DP2", "TH3", "TH3", "DP2"], "M11": ["DP3", "TH3", "TH3", "DP3"],
}


def test_r62_the_source_and_the_typologies_are_the_manual_rev_6_set_with_the_keo_file_and_the_reference_plot_width():
    profile = load_profile(PROFILE)
    assert profile.get("source") == "01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06, June 2024, with Rev 01 Dec 2022 for foundations"
    typologies = profile.get("typologies")
    assert list(typologies) == list(MANUAL_TYPOLOGIES)
    for code, (kind, attachment, keo_file, width) in MANUAL_TYPOLOGIES.items():
        entry = typologies[code]
        assert (entry["kind"], entry["attachment"], entry["keo_file"], entry["plot_width_m"]) == (kind, attachment, keo_file, width)
        assert set(entry) <= {"kind", "attachment", "keo_file", "plot_width_m", "note"}
    notes = {code: entry["note"] for code, entry in typologies.items() if "note" in entry}
    assert notes == {"DP2": "count table says 10", "DP4": "count table says 10", "TH1-IM": "18 units in the count, not explained"}
    # Every unit code of the two past schedules is a code of the profile, so no mapping sits between them
    assert {"DT3", "DT1", "DP4", "VD4", "DP2", "DP1", "VD2", "C10", "TH1", "TH3", "VL4", "VL6", "DP3", "VD1"} <= set(typologies)


def test_r62_the_clusters_are_the_eleven_of_the_manual_and_the_old_codes_stay_as_aliases():
    profile = load_profile(PROFILE)
    clusters = profile.get("clusters")
    assert {code: clusters[code] for code in MANUAL_CLUSTERS} == MANUAL_CLUSTERS
    assert list(clusters)[:11] == list(MANUAL_CLUSTERS)
    aliases = profile.get("cluster_aliases")
    assert aliases == {"D02": "M02", "D04": "M04", "Q01": "M09"}
    # Q02 is an old client code with no Rev 6 cluster, so it stays in clusters and has no alias
    assert sorted(clusters) == sorted(list(MANUAL_CLUSTERS) + list(aliases) + ["Q02"])
    # The old codes keep their own units, so profile.get("clusters.Q01") still answers
    assert all(clusters[old] == clusters[new] for old, new in aliases.items())
    # Q02 has DP4 ends and M10 has DP2 ends, and Rev 6 has no DP4 TH3 TH3 DP4 cluster
    assert clusters["Q02"] == ["DP4", "TH3", "TH3", "DP4"] and clusters["M10"] == ["DP2", "TH3", "TH3", "DP2"]
    assert all(units != clusters["Q02"] for code, units in clusters.items() if code != "Q02")
    assert (profile.get("cluster_max_units"), profile.get("cluster_block_max"), profile.get("cluster_six_unit_joint_after")) == (4, 4, 4)
    assert profile.get("cluster_split_positions") == [2, 3]
    typologies = set(profile.get("typologies"))
    assert all(unit in typologies for units in clusters.values() for unit in units)


def test_r62_the_rev_6_block_holds_the_new_values_and_no_code_reads_it_yet():
    profile = load_profile(PROFILE)
    assert profile.get("rev6") == {
        "back_slope_to_front_min_percent": 0.3, "back_slope_to_front_max_percent": 0.5, "th_back_slope_percent": 2.0,
        "boundary_wall_top_above_ffl": 1700, "boundary_wall_continuity_limit": 500, "garden_wall_top_above_ffl": 1700,
        "utility_cabinet_raise": 100, "front_setback": 5520, "front_setback_c10": 5500, "one_step_only_typologies": ["C10", "VL4", "VL6"]}
    assert profile.metres("rev6.front_setback") == 5.52
    python_files = [path for path in (ROOT / "python").glob("*.py")]
    assert [path.name for path in python_files if "rev6" in path.read_text(encoding="utf-8")] == []


def test_r62_the_profile_steps_keep_riser_height_null_and_hold_the_legend_the_count_rule_and_the_base_heights():
    profile = load_profile(PROFILE)
    assert profile.get("steps.riser_height") is None and profile.get("steps.riser_max") == 180 and profile.get("steps.riser_min") == 100
    assert profile.get("steps.legend") == [[0.10, 1], [0.20, 1], [0.27, 2], [0.37, 2], [0.44, 3], [0.54, 3]]
    assert profile.get("steps.riser_added") == 170
    assert profile.get("steps.count_rule") == "under 0.205 is 1, 0.205 to under 0.38 is 2, 0.38 and over is 3"
    assert profile.get("steps.base_height") == {"default": 100, "DP2": 200, "DP3": 200, "DP4": 200, "VD2": 200, "VD4": 200}
    assert profile.get("steps.base_height_source") == ("past job schedules, 747 plots. Rev 6 Appendix A groups DP2, DP3, DP4, VD2, VD4 on one sheet, "
                                                       "which supports it. Not confirmed by the architect")
    assert profile.metres("steps.base_height.DP4") == 0.2 and profile.metres("steps.riser_added") == 0.17


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


def test_r63_the_project_config_names_a_drawing_for_each_role_and_skips_only_the_identical_copy():
    settings = load_settings()
    drawings = settings.get("drawings")
    assert set(drawings) == {"baseplan", "masterplan", "previousMasterplan", "villaBlocks", "roadLayout", "xrefRoles", "skip"}
    assert drawings["masterplan"].lower().endswith(".dwg") and drawings["masterplan"] != drawings["previousMasterplan"]
    assert drawings["previousMasterplan"] == "221229_MP-SK-01-03-001_R06.dwg"
    # The unit blocks come from the building footprint drawing. Not the R06 copies, whose inserts are one client plot type only
    assert drawings["villaBlocks"].endswith(" Building Footprint.dwg") and not drawings["villaBlocks"].startswith("CEC")
    assert drawings["villaBlocks"] not in (drawings["masterplan"], drawings["previousMasterplan"], drawings["baseplan"])
    assert drawings["roadLayout"].endswith("Road Layout.dwg")
    assert list(drawings["xrefRoles"].values()) == ["roadLayout"]
    assert [entry["file"] for entry in drawings["skip"]] == ["221229_MP-SK-01-03-001_R06_0001.dwg"]
    assert settings.get("analysis.roadEdgeLayers") == ["RD-CURB-LINE"]
    # The second source check takes its drawing from the master plan role, so its config names only the plot layers
    second = json.loads((ROOT / "config" / "second_source.json").read_text(encoding="utf-8"))
    assert "file" not in second and len(second["layers"]) == 3


def test_r63_the_current_master_plan_and_unit_block_file_names_are_in_the_config_only_and_in_no_python_file():
    for role in ("masterplan", "villaBlocks"):
        name = load_settings().get("drawings." + role)
        stem = name.rsplit(".", 1)[0]
        offenders = [str(path.relative_to(ROOT)) for path in (ROOT / "python").rglob("*.py") if name in path.read_text(encoding="utf-8") or stem in path.read_text(encoding="utf-8")]
        assert offenders == [], role
        assert name in (ROOT / "config" / "project.json").read_text(encoding="utf-8")


def test_r63_settings_without_a_profile_path_are_refused_and_a_relative_path_starts_at_the_repo_root(tmp_path):
    empty = tmp_path / "project.json"
    empty.write_text('{"profile": null}', encoding="utf-8")
    with pytest.raises(ProfileError, match="project.json has no profile path"):
        profile_path(load_settings(empty))
    empty.write_text('{"profile": "rules/x.json"}', encoding="utf-8")
    assert profile_path(load_settings(empty)) == ROOT / "rules" / "x.json"
    assert profile_path(load_settings()).is_file()
