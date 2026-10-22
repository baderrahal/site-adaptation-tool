import json

import numpy as np
import pytest

import full_run
import landxml_reader
import ramp_levels
import road_check
from landxml_fixture import prototypes_dict, write_config, write_landxml, write_prototypes


@pytest.fixture
def ramps(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    landxml_reader.read_landxml(xml, tmp_path / "landxml", set(config["surfaces"]))
    prototypes = write_prototypes(tmp_path / "p.json")
    summary = ramp_levels.run(prototypes, tmp_path / "landxml", config, tmp_path / "levels", check_plot="77")
    records = json.loads((tmp_path / "levels" / "ramp_road_levels.json").read_text(encoding="utf-8"))["ramps"]
    return tmp_path, summary, {record["plot"]: record for record in records}


def test_r44_corners_are_shifted_by_the_survey_point_and_each_corner_has_its_own_level(ramps):
    _, _, records = ramps
    corners = records["77"]["corners"]
    assert corners[0]["levels"]["Road"] == pytest.approx(20)
    assert corners[1]["levels"]["Road"] == pytest.approx(23)
    assert corners[2]["levels"] == {} and corners[3]["levels"] == {}


def test_r44_file_holds_plot_type_top_elevation_and_the_ground_level(ramps):
    _, _, records = ramps
    assert len(records) == 5
    first = records["77"]
    assert (first["plot"], first["unit"], first["type"], first["topElevation"]) == ("77", "DT1", "Ramps 2%", 101.0)
    assert first["corners"][0]["ground"]["Ground"] == pytest.approx(5)
    assert [corner["corner"] for corner in first["corners"]] == [0, 1, 2, 3]


def test_r44_only_the_ramp_types_of_the_config_are_tested(ramps):
    _, summary, _ = ramps
    assert summary["rampCount"] == 5


def test_r44_counts_of_ramps_by_covered_corners_and_neighbouring_or_opposite(ramps):
    _, summary, _ = ramps
    assert summary["coveredCorners"] == {"0": 2, "1": 0, "2": 2, "3": 0, "4": 1}
    assert summary["twoCorners"] == {"neighbouring": 1, "opposite": 1}
    assert summary["perSurface"]["Ground"]["coveredCorners"]["4"] == 5


def test_r44_neighbouring_and_opposite_corners_and_plot_split():
    assert ramp_levels.pairs_kind([0, 1]) == "neighbouring"
    assert ramp_levels.pairs_kind([3, 0]) == "neighbouring"
    assert ramp_levels.pairs_kind([1, 3]) == "opposite"
    assert ramp_levels.split_plot("DT1-2441") == ("DT1", "2441")
    assert ramp_levels.split_plot("2441") == (None, "2441")
    assert ramp_levels.split_plot(None) == (None, None)


def test_r44_plot_check_lists_every_corner_against_the_levels_revit_holds(ramps):
    tmp_path, _, _ = ramps
    check = json.loads((tmp_path / "levels" / "plot_check.json").read_text(encoding="utf-8"))
    ramp = check["ramps"][0]
    assert ramp["otherLevels"] == {"linkFfl": 105.5, "Generic 150mm": 106.0}
    assert ramp["corners"][0]["minusTop"]["Road"] == pytest.approx(-81)
    assert ramp["corners"][2]["minusTop"] == {}


def test_r45_corners_with_no_cover_fall_in_distance_bands(ramps):
    _, summary, _ = ramps
    corners = summary["gapBands"]["corners"]["Road"]
    assert corners == {"upTo1": 4, "upTo5": 2, "upTo20": 1, "more": 5}


def test_r45_ramps_with_no_covered_corner_fall_in_bands_by_their_nearest_corner(ramps):
    _, summary, _ = ramps
    assert summary["gapBands"]["ramps"]["Road"] == {"upTo1": 1, "upTo5": 0, "upTo20": 0, "more": 1}


def test_r45_band_edges_are_inclusive():
    assert ramp_levels.band_of(1.0, [1, 5, 20]) == "upTo1"
    assert ramp_levels.band_of(1.01, [1, 5, 20]) == "upTo5"
    assert ramp_levels.band_of(20.0, [1, 5, 20]) == "upTo20"
    assert ramp_levels.band_of(None, [1, 5, 20]) == "more"


def test_r46_second_check_agrees_with_the_reader_the_lookup_and_the_ramp_file(ramps):
    tmp_path, _, _ = ramps
    config = ramp_levels.load_config(tmp_path / "config.json")
    result = road_check.check(tmp_path / "road.xml", tmp_path / "landxml", tmp_path / "levels" / "ramp_road_levels.json", tmp_path / "p.json", config, 3)
    assert result["readerCounts"]["match"] == 3 and result["readerCounts"]["mismatch"] == []
    assert result["rawRead"]["pointsDifferent"] == 0 and result["rawRead"]["facesDifferent"] == 0
    assert result["shapelyLookup"]["onlyOne"] == 0 and result["shapelyLookup"]["largestDifference"] < 1e-9
    assert result["rampFile"]["mismatch"] == [] and result["rampFile"]["ramps"] == 5


def test_r46_plane_fit_gives_the_plane_value_and_none_for_a_flat_sliver():
    triangle = np.array([[0.0, 0.0, 1.0], [10.0, 0.0, 11.0], [0.0, 10.0, 21.0]])
    assert road_check.plane_z(triangle, 2.0, 3.0) == pytest.approx(1 + 2 + 6)
    assert road_check.plane_z(np.array([[0.0, 0.0, 1.0], [1.0, 1.0, 1.0], [2.0, 2.0, 1.0]]), 1.0, 1.0) is None


def test_r46_stages_have_their_own_600_second_limit_and_road_levels_waits_for_the_reader(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    prototypes = write_prototypes(tmp_path / "p.json")
    config = write_config(tmp_path / "config.json")
    stages = {stage["name"]: stage for stage in full_run.road_stages(tmp_path / "out", config, xml, prototypes)}
    assert stages["landxml"]["timeout"] == 600 and stages["road_levels"]["needs"] == "landxml"
    assert "landxml_reader.py" in stages["landxml"]["args"][1]


def test_r46_missing_inputs_skip_the_stages_and_say_why(tmp_path):
    config = write_config(tmp_path / "config.json")
    stages = {stage["name"]: stage for stage in full_run.road_stages(tmp_path / "out", config, tmp_path / "none.xml", tmp_path / "p.json")}
    assert "args" not in stages["landxml"] and "none.xml" in stages["landxml"]["why"]
    gone = {stage["name"]: stage for stage in full_run.road_stages(tmp_path / "out", tmp_path / "no.json")}
    assert "args" not in gone["landxml"] and "args" not in gone["road_levels"]


def test_r46_a_broken_landxml_is_recorded_with_its_exact_text_and_the_run_goes_on(tmp_path):
    broken = tmp_path / "broken.xml"
    broken.write_text("<LandXML><Surfaces><Surface name='a'><P id='1'>", encoding="utf-8")
    config = write_config(tmp_path / "config.json")
    prototypes = write_prototypes(tmp_path / "p.json")
    out = tmp_path / "out"
    code = full_run.main([str(tmp_path / "root"), str(out), "--kinds", "pdf", "--roads", str(config), "--landxml", str(broken), "--prototypes", str(prototypes)])
    assert code == 0
    stages = {stage["stage"]: stage for stage in json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))["stages"]}
    assert stages["landxml"]["status"] == "failed" and "ExpatError" in stages["landxml"]["error"]
    run = json.loads((out / "landxml" / "run.json").read_text(encoding="utf-8"))
    assert run["timeoutSeconds"] == 600 and "ExpatError" in run["stderr"]
    assert stages["road_levels"]["status"] == "skipped"
    assert stages["other_faces"]["status"] == "skipped"


def test_r46_a_good_landxml_runs_both_stages_through_the_full_run(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    config = write_config(tmp_path / "config.json")
    prototypes = write_prototypes(tmp_path / "p.json")
    out = tmp_path / "out"
    full_run.main([str(tmp_path / "root"), str(out), "--kinds", "pdf", "--roads", str(config), "--landxml", str(xml), "--prototypes", str(prototypes)])
    stages = {stage["stage"]: stage for stage in json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))["stages"]}
    assert stages["landxml"]["status"] == "ok" and stages["road_levels"]["status"] == "ok"
    assert (out / "road_levels" / "ramp_road_levels.json").is_file()
    assert (out / "landxml" / "Road" / "points.npy").is_file()


def run_with(tmp_path, data):
    """Runs the ramp test on the fixture surfaces with a changed prototypes dict. Returns the summary and the records by plot."""
    xml = write_landxml(tmp_path / "road.xml")
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    landxml_reader.read_landxml(xml, tmp_path / "landxml", set(config["surfaces"]))
    summary = ramp_levels.run(write_prototypes(tmp_path / "p.json", data), tmp_path / "landxml", config, tmp_path / "levels")
    records = json.loads((tmp_path / "levels" / "ramp_road_levels.json").read_text(encoding="utf-8"))["ramps"]
    return summary, {record["plot"]: record for record in records}


def ramp_floor(data, plot):
    return next(item for item in data["documents"][0]["floors"] if item["parameters"]["Plot"] == "DT1-" + plot and item["typeName"] == "Ramps 2%")


def test_r47_corner_level_comes_from_the_corners_key_and_never_from_the_sketch_boundary(ramps):
    _, summary, records = ramps
    levels = [corner["cornerLevel"] for corner in records["77"]["corners"]]
    assert levels == [20.0, 23.02, 101.0, 101.0]
    assert 150 not in [corner["cornerLevel"] for record in records.values() for corner in record["corners"]]
    assert summary["cornerSource"] == {"corners": 5, "sketchBoundary": 0, "noCorners": 0, "notFourCorners": 0}


def test_r47_survey_offset_is_shared_minus_position_on_every_axis(tmp_path):
    data = prototypes_dict()
    data["documents"][0]["surveyPoint"] = {"position": [10, 20, 3], "sharedPosition": [1010, 5020, 10]}
    _, records = run_with(tmp_path, data)
    corner = records["77"]["corners"][0]
    assert ramp_levels.offset_of(data["documents"][0]) == [1000, 5000, 7]
    assert corner["roadLevel"] == pytest.approx(20)
    assert corner["cornerLevel"] == pytest.approx(27.0)
    assert corner["difference"] == pytest.approx(7.0)
    assert records["77"]["topElevation"] == pytest.approx(108.0)


def test_r47_floor_without_corners_uses_the_sketch_outline_with_the_top_elevation_and_is_counted(tmp_path):
    data = prototypes_dict()
    del ramp_floor(data, "79")["corners"]
    summary, records = run_with(tmp_path, data)
    assert [corner["cornerLevel"] for corner in records["79"]["corners"]] == [103.0] * 4
    assert summary["cornerSource"] == {"corners": 4, "sketchBoundary": 1, "noCorners": 0, "notFourCorners": 0}


def test_r47_old_boundary_key_is_not_read_and_does_not_crash(tmp_path):
    data = prototypes_dict()
    old = ramp_floor(data, "79")
    old["boundary"] = [[x, y, 0] for x, y, z in old.pop("corners")]
    del old["sketchBoundary"]
    five = ramp_floor(data, "80")
    five["corners"].append([100, 102, 104.0])
    summary, records = run_with(tmp_path, data)
    assert summary["rampCount"] == 3 and "79" not in records and "80" not in records
    assert summary["cornerSource"]["noCorners"] == 1 and summary["cornerSource"]["notFourCorners"] == 1


def test_r47_record_holds_corner_level_road_level_and_difference(ramps):
    _, _, records = ramps
    first, second = records["78"]["corners"][0], records["78"]["corners"][1]
    assert (first["cornerLevel"], first["roadLevel"], first["difference"]) == (21.5, 21.0, 0.5)
    assert (second["cornerLevel"], second["roadLevel"], second["difference"]) == (102.0, None, None)


def test_r47_differences_fall_in_bands_of_absolute_size(ramps):
    _, summary, _ = ramps
    assert summary["roadSurface"] == "Road"
    assert summary["roadDifference"] == {
        "covered": 8,
        "bands": {"upTo10mm": 4, "upTo30mm": 2, "upTo100mm": 0, "upTo300mm": 1, "more": 1},
        "cumulative": {"upTo10mm": 4, "upTo30mm": 6, "upTo100mm": 6, "upTo300mm": 7},
    }


def test_r47_band_edges_are_inclusive_and_the_sign_does_not_matter():
    assert ramp_levels.diff_band(0.01) == "upTo10mm" and ramp_levels.diff_band(0.0101) == "upTo30mm"
    assert ramp_levels.diff_band(-0.03) == "upTo30mm" and ramp_levels.diff_band(-0.05) == "upTo100mm"
    assert ramp_levels.diff_band(0.3) == "upTo300mm" and ramp_levels.diff_band(0.31) == "more"


def test_r47_neighbouring_pairs_count_both_one_none_within_30mm_by_floor_type():
    rows = [{"type": "A"}, {"type": "A"}, {"type": "B"}, {"type": "B"}]
    differences = [[0.0, 0.01, None, None], [None, 0.5, 0.5, None], [0.0, 0.5, None, None], [0.0, None, 0.0, None]]
    covered = [[0, 1], [1, 2], [0, 1], [0, 2]]
    counted = ramp_levels.neighbouring_pairs(rows, differences, covered)
    assert counted["A"] == {"both": 1, "one": 0, "none": 1}
    assert counted["B"] == {"both": 0, "one": 1, "none": 0}
    assert counted["all types"] == {"both": 1, "one": 1, "none": 1}


def test_r47_fixture_pairs_are_counted_in_the_summary(ramps):
    _, summary, _ = ramps
    assert summary["roadNeighbouringPairs"] == {"Ramps 2%": {"both": 1, "one": 0, "none": 0}, "all types": {"both": 1, "one": 0, "none": 0}}
    assert summary["roadCoveredCorners"] == {"0": 2, "1": 0, "2": 2, "3": 0, "4": 1}


def test_r47_spots_within_5_cm_of_a_corner_are_counted_with_the_spot_minus_corner_spread(ramps):
    _, summary, _ = ramps
    spots = summary["spots"]
    assert (spots["spots"], spots["corners"], spots["spotsNearCorner"], spots["cornersWithSpot"]) == (5, 20, 3, 3)
    assert spots["spotMinusCorner"]["median"] == pytest.approx(0.001)
    assert (spots["spotMinusCorner"]["min"], spots["spotMinusCorner"]["max"], spots["spotMinusCorner"]["withinOneMm"]) == (pytest.approx(0), pytest.approx(0.01), 2)


def test_r47_a_file_with_no_spot_elevations_gives_zero_counts(tmp_path):
    data = prototypes_dict()
    del data["documents"][0]["spotElevations"]
    summary, _ = run_with(tmp_path, data)
    assert summary["spots"] == {"spots": 0, "corners": 20, "spotsNearCorner": 0, "cornersWithSpot": 0}
