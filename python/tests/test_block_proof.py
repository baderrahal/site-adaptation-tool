import json
from pathlib import Path

import pytest

import block_proof
import cad_analysis
import project_profile

PROFILE_VALUES = {"units_mm": True, "typologies": {"DP2": {"kind": "duplex"}, "TH1": {"kind": "townhouse"}, "VL3": {"kind": "villa"}}}


def face(label, x, y, width=10, depth=25):
    corners = [(x, y), (x + width, y), (x + width, y + depth), (x, y + depth)]
    return {"kind": "plot_shape", "plot": label, "type": "VL3", "vertices": [list(corner) for corner in corners]}


def villa(name, x0, y0, x1, y1, handle="A1"):
    return {"name": name, "handle": handle, "code": name.rsplit("_", 1)[-1], "outline": [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]}


def square(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def test_r76_the_clipped_area_of_a_polygon_inside_a_convex_outline_is_the_area_of_the_overlap():
    outline = square(0, 0, 10, 10)
    assert block_proof.clip_area(square(2, 2, 6, 5), outline) == pytest.approx(12.0)
    assert block_proof.clip_area(square(5, 5, 15, 15), outline) == pytest.approx(25.0)
    assert block_proof.clip_area(square(-5, -5, 20, 20), outline) == pytest.approx(100.0)
    assert block_proof.clip_area(square(20, 20, 30, 30), outline) == 0.0
    assert block_proof.clip_area(square(10, 0, 20, 10), outline) == pytest.approx(0.0)


def test_r76_an_outline_that_runs_clockwise_gives_the_same_area_and_a_triangle_outline_clips_too():
    assert block_proof.clip_area(square(5, 5, 15, 15), list(reversed(square(0, 0, 10, 10)))) == pytest.approx(25.0)
    assert block_proof.clip_area(square(0, 0, 10, 10), [(0, 0), (10, 0), (0, 10)]) == pytest.approx(50.0)


def test_r76_a_concave_face_is_clipped_to_the_area_that_lies_inside_the_outline():
    ell = [(0, 0), (10, 0), (10, 4), (4, 4), (4, 10), (0, 10)]
    assert abs(block_proof.signed_area(ell)) == pytest.approx(64.0)
    assert block_proof.clip_area(ell, square(0, 0, 10, 10)) == pytest.approx(64.0)
    assert block_proof.clip_area(ell, square(5, 5, 10, 10)) == pytest.approx(0.0)
    # The lower bar holds 4 by 2 of it and the left bar 2 by 2, so 12
    assert block_proof.clip_area(ell, square(2, 2, 6, 6)) == pytest.approx(12.0)


def test_r76_the_hull_drops_collinear_points_and_repairs_a_hair_of_concavity_before_the_clipping():
    assert block_proof.convex_hull([(0, 0), (5, 0), (10, 0), (10, 10), (0, 10)]) == [(0, 0), (10, 0), (10, 10), (0, 10)]
    assert block_proof.convex_hull(list(reversed(square(0, 0, 4, 4)))) == square(0, 0, 4, 4)
    assert block_proof.convex_hull([(0, 0), (1, 1)]) == [(0, 0), (1, 1)]
    # A rounded hull with the middle point of the bottom edge a thousandth inside is the plain rectangle once repaired
    dented = [(0, 0), (5, 0.001), (10, 0), (10, 10), (0, 10)]
    assert block_proof.clip_area(square(0, 0, 10, 10), dented) == pytest.approx(100.0)
    assert block_proof.clip_area(square(0, 0, 10, 10), [(0, 0), (10, 0)]) == 0.0


def test_r76_a_face_that_crosses_itself_is_told_apart_from_a_plain_one():
    assert block_proof.crosses_itself([(0, 0), (10, 10), (10, 0), (0, 10)])
    assert not block_proof.crosses_itself(square(0, 0, 10, 10))
    assert not block_proof.crosses_itself([(0, 0), (10, 0), (10, 4), (4, 4), (4, 10), (0, 10)])


def test_r76_the_owner_and_the_plots_of_a_block_agree_with_the_library_on_ties_covers_and_a_block_on_no_plot():
    faces = [face("9", 10, 0), face("4", 0, 0), face("5", 20, 0)]
    villas = [villa("Duplex_DP2", 5, 5, 15, 20), villa("Villa_VL3", 21, 1, 29, 24), villa("Villa_VL3", 500, 500, 510, 510), villa("Villa_VL3", 8, 1, 18, 24)]
    result = block_proof.compare(faces, villas)
    assert (result["blocks"], result["blocksAgree"], result["blocksDiffer"], result["blocksNotComparable"]) == (4, 4, 0, 0)
    assert (result["plots"], result["plotsAgree"], result["plotsDiffer"], result["withABlock"]) == (3, 3, 0, 3)
    assert result["differingBlocks"] == [] and result["differingPlots"] == []


def test_r76_a_plot_holding_exactly_a_fifth_of_a_block_is_covered_by_both_methods():
    faces = [face("1", 0, 0, width=2, depth=10), face("2", 2, 0, width=8, depth=10)]
    villas = [villa("Villa_VL3", 0, 0, 10, 10)]
    assert block_proof.claim(faces, villas)[0][0]["stands"] == [0, 1]
    result = block_proof.compare(faces, villas)
    assert (result["blocksAgree"], result["plotsAgree"]) == (1, 2)


def test_r76_a_disagreement_is_counted_with_the_block_and_the_plot_and_not_hidden(monkeypatch):
    faces = [face("1", 0, 0), face("2", 10, 0)]
    villas = [villa("Villa_VL3", 1, 1, 9, 24, handle="H7")]
    real = cad_analysis.claim_blocks

    def swapped(found_faces, found_villas):
        owned, covered, stands_for, stats = real(found_faces, found_villas)
        return [owned[1], owned[0]], covered, [[1]], stats

    monkeypatch.setattr(cad_analysis, "claim_blocks", swapped)
    result = block_proof.compare(faces, villas)
    assert (result["blocksAgree"], result["blocksDiffer"], result["plotsAgree"], result["plotsDiffer"]) == (0, 1, 0, 2)
    assert result["differingBlocks"] == [{"block": "Villa_VL3", "handle": "H7"}] and result["differingPlots"] == ["1", "2"]


def test_r76_an_owner_that_differs_between_faces_with_shares_within_a_ten_thousandth_is_a_near_tie_and_a_larger_gap_is_a_difference(monkeypatch):
    faces = [face("4", 0, 0), face("9", 10, 0)]
    real = cad_analysis.claim_blocks

    def other_owner(found_faces, found_villas):
        owned, covered, stands_for, stats = real(found_faces, found_villas)
        return [[], [0]], covered, stands_for, stats

    monkeypatch.setattr(cad_analysis, "claim_blocks", other_owner)
    near = block_proof.compare(faces, [villa("Duplex_DP2", 4.9999, 5, 14.9999, 20)])
    assert (near["blocksAgree"], near["blocksNearTie"], near["blocksDiffer"]) == (0, 1, 0)
    assert near["differingBlocks"] == []
    far = block_proof.compare(faces, [villa("Duplex_DP2", 3, 5, 13, 20, handle="H1")])
    assert (far["blocksAgree"], far["blocksNearTie"], far["blocksDiffer"]) == (0, 0, 1)
    assert far["differingBlocks"] == [{"block": "Duplex_DP2", "handle": "H1"}]


def test_r76_a_face_that_crosses_itself_and_the_blocks_on_it_are_left_out_and_counted():
    bowtie = {"kind": "plot_shape", "plot": "1", "type": "VL3", "vertices": [[0, 0], [10, 10], [10, 0], [0, 10]]}
    faces = [bowtie, face("2", 20, 0)]
    result = block_proof.compare(faces, [villa("Villa_VL3", 1, 1, 9, 9), villa("Villa_VL3", 21, 1, 29, 24)])
    assert (result["facesCrossingThemselves"], result["blocksNotComparable"], result["blocksAgree"], result["plotsAgree"], result["plots"]) == (1, 1, 1, 1, 2)


def test_r76_a_block_with_a_line_for_an_outline_has_no_plot_in_either_method():
    faces = [face("1", 0, 0)]
    flat = villa("Villa_VL3", 1, 1, 9, 1)
    result = block_proof.compare(faces, [flat])
    assert (result["blocksAgree"], result["blocksDiffer"], result["withABlock"]) == (1, 0, 0)


def test_r76_the_second_method_does_not_use_the_geometry_library():
    source = Path(block_proof.__file__).read_text(encoding="utf-8")
    assert "import shapely" not in source and "from shapely" not in source and "shapely" not in source.lower().replace("geometry library", "")


def write_inputs(tmp_path, faces, blocks):
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"source": "base.dwg", "plots": faces}), encoding="utf-8")
    drawing = tmp_path / "blocks"
    drawing.mkdir()
    (drawing / "blocks.json").write_text(json.dumps({"source": "footprint.dwg", "blocks": blocks}), encoding="utf-8")
    profile = tmp_path / "profile.json"
    profile.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile)}), encoding="utf-8")
    return ["--cad", str(cad), "--blocks", str(drawing), "--project", str(settings)]


def test_r76_main_writes_block_proof_json_and_prints_two_lines(tmp_path, capsys):
    blocks = [{"name": "Villa_VL3", "outline": [[1, 1], [9, 1], [9, 24], [1, 24]]}, {"name": "Parking", "outline": [[1, 1], [2, 1], [2, 2]]}]
    args = write_inputs(tmp_path, [face("1", 0, 0)], blocks)
    assert block_proof.main([str(tmp_path / "out")] + args) == 0
    lines = capsys.readouterr().out.strip().splitlines()
    assert lines == ["blocks 1: agree 1, owner flips on a near tie 0, differ 0, not comparable 0", "plots 1: agree 1, differ 0, faces crossing themselves 0, with a block 1"]
    result = json.loads((tmp_path / "out" / "block_proof.json").read_text(encoding="utf-8"))
    assert result["blocks"] == 1 and "vertices" not in json.dumps(result)


def test_r76_main_refuses_a_missing_folder_with_one_line(tmp_path, capsys):
    args = write_inputs(tmp_path, [face("1", 0, 0)], [])
    args[3] = str(tmp_path / "nowhere")
    assert block_proof.main([str(tmp_path / "out")] + args) == 2
    error = capsys.readouterr().err
    assert error.startswith("block_proof: ") and error.count("\n") == 1


def test_r76_a_block_whose_largest_share_is_under_the_cover_share_belongs_to_no_plot_in_both_methods():
    faces = [face("1", 0, 0)]
    result = block_proof.compare(faces, [villa("Villa_VL3", -9.9, 5, 0.1, 20)])
    assert (result["blocksAgree"], result["blocksDiffer"], result["blocksNearTie"], result["withABlock"]) == (1, 0, 0, 0)
