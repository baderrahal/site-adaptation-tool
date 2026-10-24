import pytest
from shapely.geometry import box

import yard_rule


def face(label, x0, x1, kind="plot_shape"):
    return {"kind": kind, "plot": label, "vertices": [[x0, 0], [x1, 0], [x1, 10], [x0, 10]]}


def test_r111_faces_are_keyed_by_official_number_label_plus_shift():
    faces, bad = yard_rule.face_shapes([face("91", 0, 10), face("92", 10, 20), face("x7", 20, 30), face("93", 30, 40, kind="other_face")], 10)
    assert sorted(faces) == ["101", "102"]
    assert bad == ["x7"]


def test_r111_two_faces_with_one_label_are_joined_so_their_shares_add_up():
    faces, _ = yard_rule.face_shapes([face("1", 0, 10), face("1", 10, 20)], 0)
    assert faces["1"].area == pytest.approx(200)


def test_r111_a_shift_that_is_not_a_whole_number_is_refused():
    for shift in (None, 1.5, True):
        with pytest.raises(ValueError):
            yard_rule.face_shapes([], shift)


def test_r111_the_largest_share_wins_and_the_candidates_are_listed_largest_first():
    faces = {"1": box(0, 0, 10, 10), "2": box(10, 0, 20, 10)}
    results = yard_rule.assign([(7, box(4, 0, 14, 10))], faces, 0.01)
    assert results[7] == {"status": "assigned", "plot": "1", "share": 0.6, "candidates": [["1", 0.6], ["2", 0.4]]}
    assert yard_rule.by_plot(results) == {"1": [7]}


def test_r111_shares_within_the_tie_limit_are_a_tie_and_no_plot_is_chosen():
    faces = {"1": box(0, 0, 10, 10), "2": box(10, 0, 20, 10)}
    # 50.5 against 49.5 of 100 is 0.01 apart, which is a tie
    results = yard_rule.assign([(7, box(4.95, 0, 14.95, 10))], faces, 0.01)
    assert results[7]["status"] == "tie" and results[7]["plot"] is None
    assert yard_rule.by_plot(results) == {}
    assert yard_rule.tied_with(results, "1", 0.01) == [7] and yard_rule.tied_with(results, "2", 0.01) == [7]
    # Just over the limit the larger share decides
    assert yard_rule.assign([(7, box(4.9, 0, 14.9, 10))], faces, 0.01)[7]["plot"] == "1"


def test_r111_a_yard_on_no_face_is_none():
    results = yard_rule.assign([(7, box(50, 50, 60, 60)), (8, None)], {"1": box(0, 0, 10, 10)}, 0.01)
    assert results[7]["status"] == "none" and results[8]["status"] == "none"


def test_r111_a_tie_limit_that_is_not_a_share_is_refused():
    for tie in (-0.1, 1, "0.01"):
        with pytest.raises(ValueError):
            yard_rule.assign([], {}, tie)
