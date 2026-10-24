import math

import numpy as np
import pytest

import block_offsets
import landxml_reader
import road_lookup
import round13_check
import surface_lookup

SQUARE = np.array([(0.0, 0.0, 1.0), (10.0, 0.0, 1.0), (10.0, 10.0, 3.0), (0.0, 10.0, 3.0)])
FACES = np.array([(0, 1, 2), (0, 2, 3)])


def write_surface(root, name, xyz, faces):
    folder = root / landxml_reader.folder_name(name)
    folder.mkdir(parents=True)
    np.save(folder / "points.npy", xyz)
    np.save(folder / "faces.npy", faces)


def test_r101_the_plain_search_gives_the_z_of_the_triangle_that_holds_the_point_and_none_outside():
    assert round13_check.plain_z(SQUARE, FACES, 5.0, 5.0) == pytest.approx(2.0)
    assert round13_check.plain_z(SQUARE, FACES, 0.0, 0.0) == pytest.approx(1.0)
    assert round13_check.plain_z(SQUARE, FACES, 11.0, 5.0) is None
    degenerate = np.array([(0, 1, 2)])
    assert round13_check.plain_z(np.array([(0.0, 0.0, 0.0), (1.0, 1.0, 0.0), (2.0, 2.0, 0.0)]), degenerate, 1.0, 1.0) is None


def test_r101_the_plain_search_asks_the_surfaces_in_order_like_the_lookup():
    other = np.array([(5.0, 5.0, 9.0), (15.0, 5.0, 9.0), (15.0, 15.0, 9.0), (5.0, 15.0, 9.0)])
    surfaces = [("Road", SQUARE, FACES), ("Design", other, FACES)]
    assert round13_check.plain_level(surfaces, 7.0, 7.0)[1] == "Road"
    assert round13_check.plain_level(surfaces, 12.0, 12.0) == (pytest.approx(9.0), "Design")
    assert round13_check.plain_level(surfaces, 50.0, 50.0) == (None, None)


def test_r101_the_road_check_agrees_with_the_lookup_on_a_random_sample_and_reports_the_seed(tmp_path):
    other = np.array([(5.0, 5.0, 9.0), (15.0, 5.0, 9.0), (15.0, 15.0, 9.0), (5.0, 15.0, 9.0)])
    write_surface(tmp_path, "Road", SQUARE, FACES)
    write_surface(tmp_path, "Design", other, FACES)
    lookup = road_lookup.RoadLevels([("Road", surface_lookup.Surface(SQUARE, FACES)), ("Design", surface_lookup.Surface(other, FACES))])
    points = [(float(x), float(y)) for x in range(-2, 18, 3) for y in range(-2, 18, 3)]
    result = round13_check.check_road(lookup, tmp_path, points, seed=13, sample=10)
    assert result["seed"] == 13 and result["points"] == 10 and result["agree"] == 10 and result["differ"] == 0
    assert result["covered"] <= 10 and result["largestLevelDifference"] <= round13_check.LEVEL_TOLERANCE
    assert round13_check.check_road(lookup, tmp_path, points, seed=13, sample=10)["agree"] == 10


def test_r101_the_road_check_reports_a_point_where_the_lookup_and_the_plain_search_differ(tmp_path):
    write_surface(tmp_path, "Road", SQUARE, FACES)
    shifted = SQUARE + np.array([100.0, 0.0, 0.0])
    lookup = road_lookup.RoadLevels([("Road", surface_lookup.Surface(shifted, FACES))])
    result = round13_check.check_road(lookup, tmp_path, [(5.0, 5.0), (105.0, 5.0)], seed=1, sample=2)
    assert result["differ"] == 2 and len(result["differing"]) == 2 and result["agree"] == 0


def test_r101_the_hand_offset_equals_the_matrix_offset_for_a_turned_and_flipped_block_and_a_mirrored_link():
    flipped = {"insertPoint": [100.0, 50.0, 0.0], "scale": [-1, 1, 1], "rotationDeg": 30.0}
    transform = {"axisX": [math.cos(math.radians(120)), math.sin(math.radians(120)), 0], "axisY": [math.sin(math.radians(120)), -math.cos(math.radians(120)), 0]}
    origin = (103.2, 57.9)
    local, angle, mirrored = round13_check.hand_offset(flipped, origin, transform)
    matrix = block_offsets.block_matrix(flipped)
    back = block_offsets.invert(matrix)
    assert local == pytest.approx(block_offsets.to_local(flipped, back, origin))
    degrees, flag, _ = block_offsets.relation(block_offsets.multiply(back, block_offsets.link_matrix(transform)))
    assert angle == pytest.approx(degrees) and mirrored == flag


def test_r101_the_offset_check_compares_rows_with_the_hand_math_and_names_a_plot_that_differs():
    villas = [{"insertPoint": [0.0, 0.0, 0.0], "scale": [1, 1, 1], "rotationDeg": 0.0}]
    transform = {"axisX": [1, 0, 0], "axisY": [0, 1, 0]}
    links = {"11": ([2.0, 3.0, 0.0], transform), "12": ([9.0, 9.0, 0.0], transform)}
    rows = [{"plot": "11", "blockNumber": 0, "offset": (2.0, 3.0), "rotation": 0.0, "mirrored": False},
            {"plot": "12", "blockNumber": 0, "offset": (2.0, 3.0), "rotation": 0.0, "mirrored": False}]
    result = round13_check.check_offsets(rows, villas, links, seed=13, sample=2)
    assert result["seed"] == 13 and result["plots"] == 2 and result["agree"] == 1 and result["differ"] == 1
    assert [item["plot"] for item in result["differing"]] == ["12"] and result["largestOffsetGap"] > 5


def test_r101_the_command_stops_with_one_line_on_a_missing_input(tmp_path, capsys):
    assert round13_check.main(["road", str(tmp_path / "out"), "--landxml-dir", str(tmp_path), "--points", str(tmp_path / "none.json")]) == 2
    assert capsys.readouterr().err.startswith("round13_check: ")
