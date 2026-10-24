import pytest
from shapely.geometry import LineString, Polygon

import c3d_connect
import data_book
import wiring
import wiring_check


def square(x, y, w=10, d=20):
    return Polygon([(x, y), (x + w, y), (x + w, y + d), (x, y + d)])


def faces_row(n=3):
    return wiring.Faces({str(k + 1): square(10 * k, 0) for k in range(n)})


# R174 the methods

def test_r174_point_inside_nearest_and_key_near():
    faces = faces_row()
    assert wiring.point_inside(faces, [(5, 5), (50, 5)]) == [["1"], []]
    assert wiring.nearest(faces, [(5, 21), (5, 30)], 2, 0.01) == [["1"], []]
    assert wiring.nearest(faces, [(10, 21)], 2, 0.01) == [["1", "2"]]
    keys = {"1": {"DP2"}, "2": {"VL4"}, "3": {"VL4"}}
    assert wiring.key_near(faces, [(9, 5), (9, 5)], ["VL4", "TH1"], keys, 5) == [["2"], []]


def test_r174_widened_join_works_per_plot_and_reaches_both_plots_of_a_pair():
    faces = faces_row()
    # The first item sits on the wall of plots 1 and 2, the second inside plot 3 and 0.3 m from plot 2
    found = wiring.point_widened(faces, [(10.0, 5.0), (20.3, 5)], 0.5)
    assert sorted(found[0]) == ["1", "2"] or sorted(found[0]) == ["1"] or sorted(found[0]) == ["2"]
    held = wiring.by_plot(found)
    assert set(held) == {"1", "2", "3"} and held["3"] == [1]
    inside_only = wiring.point_widened(faces, [(5, 5), (10.3, 5)], 0.5)
    assert inside_only == [["1"], ["2"]]


def test_r174_best_share_and_every_share():
    faces = faces_row()
    pair = square(5, 1, 10, 18)
    mostly = square(1, 1, 12, 18)
    assert wiring.every_share(faces, [pair], 0.1) == [["1", "2"]]
    assert wiring.best_share(faces, [pair], 0.1, 0.02) == [["1", "2"]]
    assert wiring.best_share(faces, [mostly], 0.1, 0.02) == [["1"]]
    assert wiring.best_share(faces, [square(100, 0)], 0.1, 0.02) == [[]]


def test_r174_schedules_with_and_without_the_pdf_location():
    faces = wiring.Faces({"1": square(0, 0), "2": square(10, 0)})
    schedules = {("IMC", "11"): {}, ("IMC", "12"): {}, ("IMC", "13"): {}}
    pdf = {"11": {"IMC-a.pdf": [(5, 5)]}, "12": {"IMC-a.pdf": [(5, 5)]}, "13": {"IMC-a.pdf": [(500, 5)]}}
    keys, methods, edges, failed = wiring.schedule_methods(schedules, faces, set(), pdf, lambda sheet: "IMC", {"IMC": 10}, 1, 1.0)
    assert methods["labelShift"] == [["1"], ["2"], []]
    assert methods["labelShiftPdf"] == [["1"], ["1"], []]
    assert edges == [("IMC", "12", 11)] and failed == [("IMC", "13", "no shift fits")]


# R175 the score and the pick

def test_r175_score_counts_each_plot_once():
    found = {"1": [7], "2": [8, 9], "3": [5], "5": [1]}
    truth = {"1": 7, "2": 8, "3": 4, "4": 2, "5": None}
    assert wiring.score(found, truth, ["1", "2", "3", "4", "5"]) == {"keyPlots": 5, "wired": 4, "agree": 1, "ambiguous": 1, "wrong": 1, "unwired": 1, "noKey": 1}
    assert wiring.coverage(found, ["1", "2", "6"]) == {"one": 1, "ambiguous": 1, "unwired": 1}


def test_r175_pick_by_agree_then_owner_then_the_current_method():
    key = {"keyPlots": 3, "wired": 3, "agree": 3, "ambiguous": 0, "wrong": 0, "unwired": 0, "noKey": 0}
    scores = {"a": {"key": key, "phase1": {"one": 3, "wrongOwner": 2}}, "b": {"key": key, "phase1": {"one": 3}}, "c": {"key": key, "phase1": {"one": 3}}}
    assert wiring.pick(scores, "c") == "c" and wiring.pick(scores, "a") in ("b", "c")
    better = dict(key, agree=2, wrong=1)
    assert wiring.pick({"x": {"key": better, "phase1": {}}, "y": {"key": key, "phase1": {}}}, "x") == "y"
    none = dict(key, agree=0, noKey=3)
    assert wiring.pick({"p": {"key": none, "phase1": {"one": 5, "ambiguous": 1}}, "q": {"key": none, "phase1": {"one": 5}}}) == "q"


def test_r175_answer_key_from_the_past_generic_floor():
    faces = faces_row()
    past = {"11": {"generic": square(1, 1, 8, 15), "ramp": None}, "12": {"generic": square(15, 1, 10, 15), "ramp": None}}
    labels = wiring.truth_faces(faces, past, 0.5)
    assert labels == {"11": "1", "12": "2"}
    blocks = [square(0.5, 0.5, 9, 19), square(100, 0)]
    assert wiring.truth_blocks(past, {"11": "1", "12": None}, blocks, 0.5) == {"1": 0}
    curbs = c3d_connect.LineSet([LineString([(-5, -4), (40, -4)])])
    corners, middle = wiring.ramp_front([[2, 0, 1.0], [8, 0, 1.1], [8, 5, 1.2], [2, 5, 1.3]], curbs)
    assert sorted(c[:2] for c in corners) == [[2, 0], [8, 0]] and middle == (5, 0)


def test_r175_level_methods(monkeypatch):
    class Flat:
        count = 1

        def __init__(self, z, covers):
            self.z_value, self.covers = z, covers

        def z_at(self, x, y):
            return self.z_value if self.covers else None

    surfaces = {"a": Flat(1.0, False), "b": Flat(2.0, True), "c": Flat(2.003, True)}
    monkeypatch.setattr(c3d_connect, "snap_z", lambda surface, x, y, tolerance: surface.z_value if surface.covers or surface.z_value == 1.0 else None)
    assert wiring.level_at(surfaces, ["a", "b", "c"], 0, 0, "snapOrder", 0.005, 1, 0.005) == [1.0]
    assert wiring.level_at(surfaces, ["a", "b", "c"], 0, 0, "plainOrder", 0.005, 1, 0.005) == [2.0]
    assert wiring.level_at(surfaces, ["b", "c"], 0, 0, "snapAll", 0.005, 1, 0.005) == [2.0]
    assert wiring.level_at(surfaces, ["a", "b"], 0, 0, "snapAll", 0.005, 1, 0.005) == [1.0, 2.0]
    with pytest.raises(ValueError):
        wiring.level_at(surfaces, ["a"], 0, 0, "other", 0.005, 1, 0.005)


# R176 the weak spots and drawing to drawing

def test_r176_owner_test_and_wrong_owner():
    faces = faces_row()
    shapes = [square(9.5, 1, 9, 18)]
    owns = wiring.owner_test(faces, shapes, 0.1)
    assert owns("2", 0) and not owns("1", 0)
    assert wiring.coverage({"1": [0], "2": [0]}, ["1", "2"], owns) == {"one": 2, "wrongOwner": 1}


def test_r176_rescue_of_many_labels_and_labels_outside():
    faces = wiring.Faces({"1": square(0, 0)})
    plots = [{"kind": "other_face", "reason": "many_labels", "labels": ["5", "6", "7"], "vertices": [[100, 0], [140, 0], [140, 20], [100, 20]]},
             {"kind": "plot_open", "vertices": [[200, 0], [210, 0], [210, 20], [200, 20]]}]
    blocks = [{"centre": (105, 10)}, {"centre": (115, 10)}]
    labels = {"5": (104, 10, "DP4"), "6": (116, 10, "DP4"), "7": (139, 10, "OS"), "8": (212, 10, "DP4"), "9": (500, 500, "ROW"), "1": (5, 5, "VL4")}
    found = wiring.rescue(plots, faces, blocks, labels, 5)
    assert found["labels"] == {"manyLabelRescued": 2, "manyLabelListed": 1, "outsideRescued": 1, "outsideListed": 1, "manyLabelFaces": 1}
    assert found["listed"] == {"manyLabels": ["7"], "outside": ["9"]}
    texts = [{"layer": "L", "text": "WADI PARK\n161", "x": 1, "y": 2}, {"layer": "L", "text": "no number", "x": 0, "y": 0}, {"layer": "M", "text": "3", "x": 0, "y": 0}]
    assert wiring.label_texts(texts, "L") == {"161": (1, 2, "WADI PARK")}


def test_r176_blocks_and_curbs_of_two_drawings():
    first = [{"name": "Villa_VL4", "shape": square(0, 0)}, {"name": "Duplex_DP2", "shape": square(50, 0)}, {"name": "Villa_C10", "shape": square(200, 0)}]
    second = [{"name": "Villa_VL4", "shape": square(0.1, 0)}, {"name": "Duplex_DP4", "shape": square(50, 0)}]
    assert wiring.block_pairs(first, second, 0.5) == {"sameName": 1, "otherName": 1, "firstUnmatched": 1, "secondUnmatched": 0}
    lines = [LineString([(0, 0), (10, 0)])]
    others = c3d_connect.LineSet([LineString([(0, 0.005), (5, 0.005)]), LineString([(5, 2), (10, 2)])])
    found = wiring.line_agreement(lines, others, 1, [0.01, 0.1, 1.0], lambda point: point.x < 9.5)
    assert found == {"within0.01m": 6, "worse": 4}


# R177 the chosen wiring in plots.csv

def test_r177_every_wired_column_has_its_method_column_next_to_it():
    columns = data_book.BASE_COLUMNS
    for column in ("officialNumber", "cutsheet", "footprintBlock", "r06Block", "r06_0000Block", "r06Number", "r06_0000Number", "v9PlotNumber",
                   "setbackFromFrontM", "rowDistanceM", "curbDistanceM"):
        assert columns[columns.index(column) + 1] == column + "Method"
    assert columns[columns.index("roadLevelSurface") + 1] == "roadLevelMethod"


def test_r177_sidewalk_from_the_two_distances_of_the_row():
    row = {"rowDistanceM": 1.0, "curbDistanceM": 5.15, "sidewalkWidthM": None}
    data_book.sidewalk(row)
    assert row["sidewalkWidthM"] == pytest.approx(4.15)
    row = {"rowDistanceM": 6.0, "curbDistanceM": 5.0, "sidewalkWidthM": None}
    data_book.sidewalk(row)
    assert row["sidewalkWidthM"] is None


# R178 second method

def test_r178_grid_share_and_names_over_the_share():
    face = [(0, 0), (10, 0), (10, 20), (0, 20)]
    pair = [(5, 1), (15, 1), (15, 19), (5, 19)]
    assert wiring_check.grid_share(pair, face, 0.2) == pytest.approx(0.5, abs=0.02)
    assert wiring_check.grid_share(pair, face, 0.2, most=100) == pytest.approx(0.5, abs=0.1)
    outlines = [("Duplex_DP2", pair, wiring_check.box(pair)), ("Villa_VL4", [(50, 0), (60, 0), (60, 20)], (50, 0, 60, 20))]
    assert wiring_check.shared_names(face, outlines, 0.1, 0.2, 0.03, 10000) == (["Duplex_DP2"], 0)


def test_r178_front_middle_and_label_shift():
    face = [(0, 0), (10, 0), (10, 20), (0, 20)]
    distance, middle = wiring_check.front_middle(face, [(-5, -4, 15, -4)], 1.0)
    assert middle == (5, 0) and distance == pytest.approx(4)
    rows = [{"neighbourhood": "IMC", "officialNumber": "21", "baseplanLabel": "11"}, {"neighbourhood": "IMD", "officialNumber": "21", "baseplanLabel": "11"}]
    assert wiring_check.check_schedules(rows, {"IMC": 10, "IMD": 11}) == {"labelShift match": 1, "labelShift mismatch": 1}
