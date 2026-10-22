import json

import face_reasons


def square(x, y, size=10, plot="1"):
    vertices = [[x, y], [x + size, y], [x + size, y + size], [x, y + size]]
    return {"kind": "plot_shape", "plot": plot, "area": size * size, "vertices": vertices}


def other(x, y, reason, size=10, labels=None):
    vertices = [[x, y], [x + size, y], [x + size, y + size], [x, y + size]]
    face = {"kind": "other_face", "vertices": vertices, "area": size * size, "reason": reason}
    if labels:
        face["labels"] = labels
    return face


def text(value, x, y, layer="MP_PLOT_TEXT"):
    return {"layer": layer, "text": value, "x": x, "y": y}


def test_r33_label_text_gives_its_number_and_type_words_and_other_text_gives_none():
    assert face_reasons.label_of("ROW - External Road\n3146") == (3146, "ROW - External Road")
    assert face_reasons.label_of("283") == (283, "")
    assert face_reasons.label_of("Local Mosque") is None


def test_r33_other_faces_are_grouped_by_reason_with_counts():
    faces = [
        other(100, 0, "many_labels", labels=["162", "161", "163"]),
        other(200, 0, "many_labels", labels=["151", "152", "153", "482"]),
        other(300, 0, "many_labels", labels=["424", "355"]),
        other(400, 0, "no_label", size=3),
        other(500, 0, "no_label", size=20),
        other(600, 0, "no_label", size=50),
    ]
    report = face_reasons.reasons({"plots": [square(0, 0)]}, {"faces": faces}, [], ["MP_PLOT_TEXT"])
    groups = {name: item["count"] for name, item in report["faces"]["groups"].items()}
    assert groups == {"many_labels_consecutive_run": 1, "many_labels_run_plus_outlier": 1, "many_labels_not_consecutive": 1, "no_label_sliver": 1, "no_label_small": 1, "no_label_large": 1}
    assert report["faces"]["total"] == 6


def test_r33_labels_outside_every_face_are_grouped_with_distance_and_nothing_inside_is_listed():
    plots = {"plots": [square(0, 0, plot="1"), square(100, 0, plot="2")]}
    faces = {"faces": [other(200, 0, "no_label")]}
    texts = [
        text("1", 5, 5),
        text("ROW - Internal Road\n3140", 50, 5),
        text("Buffer Area\n3151", 300, 5),
        text("77", 215, 5),
        text("999", 150, 5, layer="OTHER"),
        text("not a plot", 150, 5),
        text("78", 115, 5),
        text("79", 800, 5),
    ]
    report = face_reasons.reasons(plots, faces, texts, ["MP_PLOT_TEXT"])
    assert report["labelsRead"] == 6
    groups = {name: item["count"] for name, item in report["labels"]["groups"].items()}
    assert groups == {"road_or_buffer_label": 2, "near_unlabelled_face": 1, "near_labelled_face": 1, "far_from_every_face": 1}
    near = next(item for item in report["labels"]["list"] if item["plot"] == 79)
    assert near["group"] == "far_from_every_face" and near["nearest"] == "no_label"
    assert all("point" not in item for item in report["labels"]["list"])


def test_r33_label_just_outside_an_unlabelled_face_is_named_for_it():
    faces = {"faces": [other(200, 0, "no_label")]}
    report = face_reasons.reasons({"plots": [square(0, 0)]}, faces, [text("77", 205, 5), text("78", 205, 12)], ["MP_PLOT_TEXT"])
    labels = {item["plot"]: item for item in report["labels"]["list"]}
    assert 77 not in labels
    assert labels[78]["group"] == "near_unlabelled_face" and labels[78]["distance"] == 2
    assert report["faces"]["list"][0]["strayLabelsWithin30m"] == [78]


def test_r33_main_writes_the_report_without_coordinates(tmp_path):
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"plots": [square(0, 0)]}), encoding="utf-8")
    (cad / "other_faces.json").write_text(json.dumps({"faces": [other(100, 0, "no_label")]}), encoding="utf-8")
    (cad / "texts.json").write_text(json.dumps({"texts": [text("Buffer Area\n3151", 500, 5)]}), encoding="utf-8")
    layers = tmp_path / "layers.json"
    layers.write_text(json.dumps({"plotLabelLayers": ["MP_PLOT_TEXT"]}), encoding="utf-8")
    assert face_reasons.main([str(tmp_path / "out"), "--cad", str(cad), "--layers", str(layers)]) == 0
    written = (tmp_path / "out" / "other_reasons.json").read_text(encoding="utf-8")
    assert json.loads(written)["labels"]["total"] == 1
    assert "500" not in written
