import json

import second_source


def square(x, y, size=10, plot="1"):
    vertices = [[x, y], [x + size, y], [x + size, y + size], [x, y + size]]
    return {"kind": "plot_shape", "plot": plot, "area": size * size, "center": [x + size / 2, y + size / 2], "vertices": vertices}


def closed(layer, x, y, size=10):
    return {"kind": "polyline", "layer": layer, "closed": True, "pts": [[x, y, 0], [x + size, y, 0], [x + size, y + size, 0], [x, y + size, 0]]}


def line(layer, a, b):
    return {"kind": "line", "layer": layer, "pts": [list(a), list(b)]}


LAYERS = ["Plots", "Sub"]


def test_r31_closed_polylines_and_loose_lines_become_faces_and_equal_faces_count_once():
    pieces = [closed("Plots", 0, 0), closed("Sub", 0, 0), closed("Other", 50, 50)]
    for a, b in [((20, 0), (30, 0)), ((30, 0), (30, 10)), ((30, 10), (20, 10)), ((20, 10), (20, 0))]:
        pieces.append(line("Plots", a, b))
    faces, per_layer, repeated = second_source.second_faces(pieces, LAYERS)
    assert len(faces) == 2
    assert repeated == 1
    assert per_layer["Plots"] == {"closed": 1, "polygonized": 1}
    assert per_layer["Sub"] == {"closed": 1, "polygonized": 0}


def test_r31_faces_are_matched_by_location_and_area_and_the_rest_is_counted():
    plots = {"plots": [square(0, 0, plot="1"), square(20, 0, plot="2"), square(40, 0, plot="3")]}
    other = {"faces": [{"vertices": [[100, 100], [110, 100], [110, 110], [100, 110]]}]}
    base = second_source.base_faces(plots, other)
    pieces = [closed("Plots", 0, 0), closed("Plots", 20, 0, size=10.5), closed("Plots", 200, 200)]
    faces, _, _ = second_source.second_faces(pieces, LAYERS)
    counts, details = second_source.compare(base, faces, 0.5, 1.0)
    assert counts["matched"] == 2
    assert counts["onlyBase"] == 2
    assert counts["onlySecond"] == 1
    assert counts["areaDiffOver"] == 1
    assert details["areaDiff"][0]["plot"] == "2"
    assert sorted(item["plot"] for item in details["onlyBase"] if item["plot"]) == ["3"]


def test_r31_second_face_that_holds_several_baseplan_faces_is_counted_apart():
    plots = {"plots": [square(0, 0, plot="1"), square(10, 0, plot="2")]}
    base = second_source.base_faces(plots, {"faces": []})
    pieces = [{"kind": "polyline", "layer": "Plots", "closed": True, "pts": [[0, 0], [20, 0], [20, 10], [0, 10]]}]
    faces, _, _ = second_source.second_faces(pieces, LAYERS)
    counts, _ = second_source.compare(base, faces, 0.6, 1.0)
    assert counts["matched"] == 0 and counts["onlySecond"] == 1 and counts["onlySecondHoldsSeveral"] == 1


def test_r31_main_writes_counts_and_changes_nothing(tmp_path):
    cad, second = tmp_path / "cad", tmp_path / "second"
    cad.mkdir()
    second.mkdir()
    (cad / "plots.json").write_text(json.dumps({"plots": [square(0, 0)]}), encoding="utf-8")
    (cad / "other_faces.json").write_text(json.dumps({"faces": []}), encoding="utf-8")
    (second / "pieces.json").write_text(json.dumps({"pieces": [closed("Plots", 0, 0)]}), encoding="utf-8")
    config = tmp_path / "config.json"
    config.write_text(json.dumps({"file": "x.dwg", "layers": ["Plots"], "minOverlap": 0.5, "areaPercent": 1.0}), encoding="utf-8")
    before = (cad / "plots.json").read_text(encoding="utf-8")
    assert second_source.main([str(tmp_path / "out"), "--cad", str(cad), "--second", str(second), "--config", str(config)]) == 0
    counts = json.loads((tmp_path / "out" / "second_source.json").read_text(encoding="utf-8"))["counts"]
    assert counts["matched"] == 1 and counts["onlyBase"] == 0 and counts["onlySecond"] == 0
    assert (cad / "plots.json").read_text(encoding="utf-8") == before
