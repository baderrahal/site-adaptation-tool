"""R345. A baseplan face the build refuses in Python and in C# alike: a ring that is not simple and a label whose rings overlap.
shapely mends both by buffer(0) and a union where the C# of the face step sums ring areas, so neither side takes such a face."""
import json

import pytest

import face_rings
import plan_from_book

SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]]


def shape(label, vertices, kind="plot_shape"):
    return {"kind": kind, "plot": label, "vertices": vertices}


def test_r345_a_simple_ring_holds_and_a_bow_tie_a_fold_or_a_touch_is_not_simple():
    assert face_rings.simple(face_rings.clean(SQUARE))
    # Site scale, a closing vertex and a vertex given twice are no problem
    east, north = 512345.678, 2400123.456
    site = [[east + x, north + y] for x, y in SQUARE]
    assert face_rings.simple(face_rings.clean(site + [site[0]]))
    assert face_rings.clean([[0, 0], [0, 0], [1, 0], [1, 1], [0, 0]]) == [(0.0, 0.0), (1.0, 0.0), (1.0, 1.0)]
    assert not face_rings.simple(face_rings.clean([[0, 0], [10, 10], [10, 0], [0, 10]]))
    # A spike folds back along one line, a ring that touches itself at a vertex meets itself
    assert not face_rings.simple(face_rings.clean([[0, 0], [10, 0], [10, 10], [10, 5], [0, 10]]))
    assert not face_rings.simple(face_rings.clean([[0, 0], [10, 0], [5, 5], [10, 10], [0, 10], [5, 5]]))


def test_r345_the_union_of_rings_by_slabs_is_their_sum_when_they_lie_apart_or_share_an_edge():
    apart = [face_rings.clean(SQUARE), face_rings.clean([[20, 0], [30, 0], [30, 10], [20, 10]])]
    assert face_rings.union_area(apart) == pytest.approx(200.0, abs=1e-9)
    shared = [face_rings.clean(SQUARE), face_rings.clean([[10, 0], [20, 0], [20, 10], [10, 10]])]
    assert face_rings.union_area(shared) == pytest.approx(200.0, abs=1e-9)
    twice = [face_rings.clean(SQUARE), face_rings.clean(SQUARE)]
    assert face_rings.union_area(twice) == pytest.approx(100.0, abs=1e-9)
    # Two squares turned against each other, the union an octagram of 100 + 100 - the octagon they share
    turned = [face_rings.clean([[-5, -5], [5, -5], [5, 5], [-5, 5]]), face_rings.clean([[0, -7.0710678118654755], [7.0710678118654755, 0], [0, 7.0710678118654755], [-7.0710678118654755, 0]])]
    octagon = 8 * 5 ** 2 * (2 ** 0.5 - 1)
    assert face_rings.union_area(turned) == pytest.approx(200.0 - octagon, abs=1e-9)


def test_r345_the_refusals_name_each_label_and_why():
    plots = [shape("1", SQUARE), shape("2", [[0, 0], [10, 10], [10, 0], [0, 10]]), shape("3", SQUARE), shape("03", SQUARE),
             shape("4", SQUARE), shape("4", [[10, 0], [20, 0], [20, 10], [10, 10]]), shape("5a", [[0, 0], [10, 10], [10, 0], [0, 10]]),
             shape("6", [[0, 0], [1, 0]]), shape("7", [[0, 0], [10, 10], [10, 0], [0, 10]], kind="polyline")]
    # The same lines as FaceRings.Refusals of C#, in the same order: the rings that are not simple, then the labels that overlap
    assert face_rings.refusals(plots, 0.000001) == [
        "the baseplan face of label 2 is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# would not, so the build refuses it (R345)",
        "the baseplan faces of label 3 overlap by 100.000 m2, shapely joins them by a union and the C# would sum them, so the build refuses them (R345)"]


def run(tmp_path, plots):
    (tmp_path / "plots.json").write_text(json.dumps({"plots": plots}), encoding="utf-8")
    (tmp_path / "layers.json").write_text(json.dumps({"units": "Meters"}), encoding="utf-8")
    (tmp_path / "plots.csv").write_text("baseplanLabel,locationPlanNumber\n1,101\n2,102\n", encoding="utf-8")
    (tmp_path / "labels.csv").write_text("number,code,kind,nbh\n101,DP1,clientCode,012\n102,DP1,clientCode,012\n", encoding="utf-8")
    from test_input_units import baseplan_settings
    # R388. The build reads the baseplan config names, so the made up config names the made up read
    return plan_from_book.main(["all", "--labels", str(tmp_path / "labels.csv"), "--plots-csv", str(tmp_path / "plots.csv"), "--baseplan", str(tmp_path / "plots.json"),
                                "--landxml", "x", "--site", "s.json", "--roads", "r", "--scratch", str(tmp_path), "--out", str(tmp_path / "plan.json"),
                                "--steps", str(tmp_path / "steps"), "--steps-only", "--settings", str(baseplan_settings(tmp_path))])


def test_r345_the_build_refuses_a_bow_tie_and_an_outline_drawn_twice(tmp_path, capsys):
    assert run(tmp_path, [shape("1", SQUARE), shape("2", [[20, 0], [30, 10], [30, 0], [20, 10]])]) == 2
    assert "label 2 is not a simple ring" in capsys.readouterr().err
    assert run(tmp_path, [shape("1", SQUARE), shape("1", SQUARE)]) == 2
    assert "label 1 overlap by 100.000 m2" in capsys.readouterr().err
    assert not (tmp_path / "steps").exists()
    # Two rings of one label that only share an edge are one face, as the union gives it
    assert run(tmp_path, [shape("1", SQUARE), shape("1", [[10, 0], [20, 0], [20, 10], [10, 10]])]) == 0
    step = json.loads((tmp_path / "steps" / "step_faces_python.json").read_text(encoding="utf-8"))
    assert step["plots"]["101"]["raw"]["area"] == pytest.approx(200.0, abs=1e-9)
