import json

import numpy as np
import pytest

import landxml_reader
import ramp_levels
import surface_lookup
from landxml_fixture import road_z, write_config, write_landxml


@pytest.fixture
def read(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    report = landxml_reader.read_landxml(xml, tmp_path / "out", set(config["surfaces"]))
    return tmp_path / "out", report


def entry(report, name):
    return next(item for item in report["surfaces"] if item["name"] == name)


def test_r40_point_order_is_northing_then_easting_then_elevation(read):
    out, report = read
    points = np.load(out / "Road" / "points.npy")
    assert points[0].tolist() == [1000, 5000, 10]
    assert entry(report, "Road")["bbox"] == {"minEasting": 1000, "minNorthing": 5000, "maxEasting": 1020, "maxNorthing": 5010}


def test_r40_summary_holds_counts_box_and_z_range_and_arrays_sit_in_one_folder_per_surface(read):
    out, report = read
    road = entry(report, "Road")
    assert (road["pointCount"], road["faceCount"], road["zRange"]) == (6, 5, {"min": 10, "max": 40})
    assert road["declaredZRange"] == {"zMin": "1", "zMax": "40"}
    assert report["surfaceCount"] == 3 and report["epsgCode"] == "32637" and report["linearUnit"] == "meter"
    for name in ("Road", "Ground", "Spare"):
        assert sorted(path.name for path in (out / name).iterdir()) == ["faces.npy", "points.npy", "summary.json"]
    assert json.loads((out / "Road" / "summary.json").read_text(encoding="utf-8"))["name"] == "Road"


def test_r40_file_is_streamed_in_chunks_and_a_tiny_chunk_gives_the_same_arrays(tmp_path, monkeypatch):
    xml = write_landxml(tmp_path / "road.xml")
    landxml_reader.read_landxml(xml, tmp_path / "whole")
    monkeypatch.setattr(landxml_reader, "CHUNK", 7)
    landxml_reader.read_landxml(xml, tmp_path / "small")
    for name in ("points.npy", "faces.npy"):
        assert np.array_equal(np.load(tmp_path / "whole" / "Road" / name), np.load(tmp_path / "small" / "Road" / name))


def test_r40_face_with_an_unknown_point_id_is_counted_and_dropped(read):
    _, report = read
    road = entry(report, "Road")
    assert road["badFaceCount"] == 1 and road["visibleFaceCount"] == 3


def test_r40_folder_names_never_hold_characters_windows_refuses():
    assert landxml_reader.folder_name("a:b/c*d?") == "a_b_c_d_"
    assert landxml_reader.folder_name("EXISTING_11.5 Sqkm") == "EXISTING_11.5 Sqkm"


def test_r41_invisible_face_is_skipped_and_counted(read):
    out, report = read
    road = entry(report, "Road")
    assert road["invisibleFaceCount"] == 1
    assert road["faceCount"] == road["visibleFaceCount"] + road["invisibleFaceCount"] + road["badFaceCount"]
    assert len(np.load(out / "Road" / "faces.npy")) == 3


def test_r41_a_point_inside_an_invisible_face_has_no_level(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    assert surface_lookup.lookup_z(surface, 1017, 5002) is None
    assert surface_lookup.lookup_z(surface, 1012, 5008) == pytest.approx(road_z(1012, 5008))


def test_r42_lookup_interpolates_inside_a_visible_triangle(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    for easting, northing in [(1003, 5004), (1008, 5002), (1002, 5008), (1000.5, 5009.5)]:
        assert surface_lookup.lookup_z(surface, easting, northing) == pytest.approx(road_z(easting, northing))


def test_r42_a_point_outside_gives_none(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    for easting, northing in [(999.9, 5005), (1005, 4999.9), (1005, 5010.1), (5000, 1000), (-1e9, 1e9)]:
        assert surface_lookup.lookup_z(surface, easting, northing) is None


def test_r42_a_point_on_a_shared_edge_gives_one_value(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    on_edge = surface_lookup.lookup_z(surface, 1010, 5005)
    assert isinstance(on_edge, float) and on_edge == pytest.approx(25)
    assert surface_lookup.lookup_z(surface, 1005, 5005) == pytest.approx(20)
    assert surface_lookup.lookup_z(surface, 1010, 5010) == pytest.approx(30)


def test_r42_grid_lookup_agrees_with_the_plane_on_a_mesh_with_thousands_of_points():
    size = 40
    grid = np.array([[i * 3.0, j * 3.0, 0.5 * i * 3.0 - 0.25 * j * 3.0] for i in range(size + 1) for j in range(size + 1)])
    faces = []
    for i in range(size):
        for j in range(size):
            a, b, c, d = i * (size + 1) + j, (i + 1) * (size + 1) + j, (i + 1) * (size + 1) + j + 1, i * (size + 1) + j + 1
            faces += [(a, b, c), (a, c, d)]
    surface = surface_lookup.Surface(grid, np.array(faces))
    rng = np.random.default_rng(1)
    for easting, northing in rng.uniform(0, size * 3.0, (300, 2)):
        assert surface_lookup.lookup_z(surface, easting, northing) == pytest.approx(0.5 * easting - 0.25 * northing)
    assert surface_lookup.lookup_z(surface, size * 3.0 + 0.1, 5) is None


def test_r42_lookup_many_returns_one_answer_per_point(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    answers = surface_lookup.lookup_many(surface, [(1003, 5004), (0, 0), (1008, 5002)])
    assert answers[1] is None and len(answers) == 3 and answers[0] == pytest.approx(17)


def test_r42_distance_to_the_nearest_visible_triangle(read):
    out, _ = read
    surface = surface_lookup.load_surface(out, "Road")
    assert surface.distance_to(997, 5005, 20) == pytest.approx(3)
    assert surface.distance_to(997, 5005, 2) is None
    assert surface.distance_to(1017, 5002, 20) == pytest.approx(5 / 2 ** 0.5)
    assert surface.distance_to(2000, 9000, 20) is None


def test_r43_a_surface_that_is_not_in_the_config_is_reported_and_a_missing_one_too(tmp_path):
    xml = write_landxml(tmp_path / "road.xml")
    report = landxml_reader.read_landxml(xml, tmp_path / "out", {"Road", "Ground", "Gone"})
    assert report["notInConfig"] == ["Spare"]
    assert report["missingFromFile"] == ["Gone"]


def test_r43_roles_come_from_the_config_and_an_unlisted_surface_is_never_looked_up(tmp_path):
    config = ramp_levels.load_config(write_config(tmp_path / "config.json"))
    assert config["design"] == ["Road"] and config["ground"] == ["Ground"]
    assert "Spare" not in config["design"] + config["ground"]


def test_r43_real_config_names_one_ground_surface_six_design_surfaces_and_the_open_question():
    config = ramp_levels.load_config(ramp_levels.DEFAULT_CONFIG)
    assert config["ground"] == ["EXISTING_11.5 Sqkm"]
    assert len(config["design"]) == 6
    assert "has not confirmed which design surface is the finished road" in config["note"]
    assert "Top Surface-02" in config["note"]
