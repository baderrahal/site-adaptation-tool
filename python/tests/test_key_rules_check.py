"""Round ADDIN PR 3a: the second method of the rules on the Parsons keys (R336), on made up plots."""
import csv
import json

import openpyxl
import pytest

import key_rules_check

SQUARE = [(0.0, 0.0), (16.0, 0.0), (16.0, 25.0), (0.0, 25.0)]
FRONT_CURB = [((-50.0, -3.0), (50.0, -3.0))]


def test_r336_the_own_front_is_the_edge_within_the_limits_whose_middle_is_nearest_a_curb_with_n_into_the_plot():
    frame, rule = key_rules_check.own_frame(SQUARE, FRONT_CURB, (7.99, 18.75), 8.0, None)
    assert rule == "nearestCurb"
    assert frame["n"] == pytest.approx((0.0, 1.0)) and frame["r"] == pytest.approx((1.0, 0.0))
    assert frame["left"] == (0.0, 0.0) and frame["right"] == (16.0, 0.0)


def test_r336_a_corner_face_takes_the_road_edge_its_orientation_quadrant_bears_to_and_none_with_no_orientation():
    curbs = FRONT_CURB + [((19.0, -50.0), (19.0, 50.0))]
    frame, rule = key_rules_check.own_frame(SQUARE, curbs, (7.99, 18.75), 8.0, "EN")
    # The east edge bears 90 degrees, the south edge 180, EN takes the east edge though the south edge is nearer
    assert rule == "orientation" and frame["n"] == pytest.approx((-1.0, 0.0))
    assert key_rules_check.own_frame(SQUARE, curbs, (7.99, 18.75), 8.0, None) == (None, "noOrientation")


def test_r336_the_own_origin_runs_from_the_anchor_corner_along_the_link_axes_and_turns_with_the_mirror():
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0)}
    assert key_rules_check.own_origin(frame, False, {"anchor": "end", "offset": [-10.0, 1.5]}) == pytest.approx((6.0, 1.5))
    assert key_rules_check.own_origin(frame, True, {"anchor": "end", "offset": [-10.0, 1.5]}) == pytest.approx((10.0, 1.5))


def made_up(tmp_path):
    """Two parsons plots on two square faces with a curb in front, one link where the fit puts it and one 2 m along."""
    past = tmp_path / "past_all.csv"
    fields = ["key", "source", "kind", "plot", "typologyRev9", "x", "y", "rotationDeg", "mirrored"]
    with open(past, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerow({"key": "imx", "source": "parsons", "kind": "TU", "plot": "1", "typologyRev9": "C10", "x": 6.0, "y": 1.5, "rotationDeg": 0.0, "mirrored": "False"})
        writer.writerow({"key": "imx", "source": "parsons", "kind": "TU", "plot": "2", "typologyRev9": "C10", "x": 108.0, "y": 1.5, "rotationDeg": 0.0, "mirrored": "False"})
    plots = tmp_path / "plots.csv"
    plots.write_text("locationPlanNumber,baseplanLabel\n1,11\n2,12\n", encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": "11", "vertices": [list(point) for point in SQUARE]},
                                              {"kind": "plot_shape", "plot": "12", "vertices": [[x + 100.0, y] for x, y in SQUARE]}]}), encoding="utf-8")
    roads = tmp_path / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_text(json.dumps({"pieces": [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-50, -3, 0], [150, -3, 0]]}]}), encoding="utf-8")
    sales = tmp_path / "sales.xlsx"
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "S"
    sheet.append(["Plot No", "Orientation"])
    sheet.append([1, "EN"])
    sheet.append([2, "EN"])
    book.save(sales)
    majority_json = tmp_path / "majority.json"
    decision = {"decision": "parsons linkOrigin", "plots": 2, "coded": "plotFrameRefit", "tests": {"plotFrameRefit": {"holds": 1, "breaks": 1, "cannot": 0, "byKey": {"imx": {"holds": 1, "breaks": 1}}}}}
    majority_json.write_text(json.dumps({"linkFits": {"parsons": {"C10": {"anchor": "end", "offset": [-10.0, 1.5], "plots": 2}}}, "decisions": [decision], "siteFacts": {}}), encoding="utf-8")
    overrides = tmp_path / "overrides.json"
    overrides.write_text(json.dumps({"overrides": [{"plot": "2", "field": "insert", "decision": "parsons linkOrigin"}]}), encoding="utf-8")
    settings = tmp_path / "project.json"
    real = json.loads((key_rules_check.ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    real["majority"]["check"] = {"seeds": [14, 7], "sample": 2}
    settings.write_text(json.dumps(real), encoding="utf-8")
    return ["--settings", str(settings), "--past-all", str(past), "--majority", str(majority_json), "--overrides", str(overrides), "--plots-csv", str(plots),
            "--baseplan", str(baseplan), "--roads", str(roads), "--sales", str(sales), "--out", str(tmp_path / "out" / "check.json")], tmp_path / "out" / "check.json"


def test_r336_the_check_holds_each_sampled_plot_again_and_counts_its_verdict_against_the_first_method(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {"1": "EN", "2": "EN"})
    argv, out = made_up(tmp_path)
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    for seed in ("14", "7", "every"):
        assert found["seeds"][seed]["counts"] == {"linkOrigin match": 2, "linkRotation match": 2}
        assert found["seeds"][seed]["mismatches"] == []
    assert found["totals"]["parsons linkOrigin"]["match"] is True


def test_r336_a_first_method_that_missed_no_plot_where_the_route_finds_a_miss_is_a_mismatch(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {})
    argv, out = made_up(tmp_path)
    (tmp_path / "overrides.json").write_text(json.dumps({"overrides": []}), encoding="utf-8")
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    assert found["seeds"]["14"]["counts"]["linkOrigin mismatch"] == 1
    assert found["seeds"]["14"]["mismatches"][0]["plot"] == "2" and found["seeds"]["14"]["mismatches"][0]["own"] == "misses"


def test_r336_the_check_writes_nothing_inside_the_repo(tmp_path):
    argv, _ = made_up(tmp_path)
    argv[-1] = str(key_rules_check.ROOT / "check.json")
    assert key_rules_check.main(argv) == 2
    assert not (key_rules_check.ROOT / "check.json").exists()


# R336 the second method of the hands, ramps and yards (R333, R334)
def test_r336_two_faces_are_side_neighbours_by_the_length_of_the_edges_they_share_within_5_cm():
    left = [(0.0, 0.0), (10.0, 0.0), (10.0, 25.0), (0.0, 25.0)]
    right = [(10.02, 0.0), (20.0, 0.0), (20.0, 25.0), (10.02, 25.0)]
    far = [(10.2, 0.0), (20.0, 0.0), (20.0, 25.0), (10.2, 25.0)]
    assert key_rules_check.shared_length(left, right, 0.05) == pytest.approx(25.0)
    assert key_rules_check.shared_length(left, far, 0.05) == 0.0


def test_r336_the_own_row_pairs_count_from_the_right_end_and_skip_units_of_a_cluster():
    sides = {"a": {"left": [], "right": ["b"]}, "b": {"left": ["a"], "right": ["c"]}, "c": {"left": ["b"], "right": []}}
    assert key_rules_check.own_pair_side("a", sides, lambda plot: True) == "right"
    assert key_rules_check.own_pair_side("b", sides, lambda plot: True) == "left"
    assert key_rules_check.own_pair_side("b", sides, lambda plot: plot != "c") == "right"
    assert key_rules_check.own_pair_side("a", {"a": {"left": [], "right": []}}, lambda plot: True) is None


def test_r336_a_learned_rule_is_read_by_the_scope_and_the_factor_value_the_first_method_writes():
    rule = {"('parsons', 'A.rvt') | yes none": True}
    assert key_rules_check.learned(rule, ("parsons", "A.rvt"), "yes none") is True
    assert key_rules_check.learned(rule, ("parsons", "B.rvt"), "yes none") is None


def test_r336_the_own_ramp_outline_and_its_largest_corner_gap():
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0)}
    corners = [(0.0, 0.0), (16.0, 0.0), (16.0, 5.52), (0.0, 5.53)]
    assert key_rules_check.own_corner_gap(key_rules_check.own_ramp(frame, 5.52), corners) == pytest.approx(0.01)


def test_r336_the_own_boundary_gap_of_two_outlines_takes_the_vertices_of_each_against_the_edges_of_the_other():
    first = [(0.0, 0.0), (10.0, 0.0), (10.0, 5.0), (0.0, 5.0)]
    second = [(0.0, 0.0), (10.0, 0.0), (10.0, 5.02), (0.0, 5.02)]
    assert key_rules_check.own_boundary_gap([first], [second]) == pytest.approx(0.02)


def test_r336_a_vertex_on_the_line_of_its_neighbours_is_no_corner_so_an_edge_drawn_in_two_pieces_is_one_edge():
    ring = [(0.0, 0.0), (8.0, 0.0005), (16.0, 0.0), (16.0, 25.0), (0.0, 25.0)]
    assert key_rules_check.straight(ring) == [(0.0, 0.0), (16.0, 0.0), (16.0, 25.0), (0.0, 25.0)]
    assert key_rules_check.straight(SQUARE) == SQUARE


def test_r336_the_own_road_side_is_where_the_second_road_edge_lies_seen_from_the_front():
    curbs = FRONT_CURB + [((19.0, -50.0), (19.0, 50.0))]
    frame, _ = key_rules_check.own_frame(SQUARE, FRONT_CURB, (7.99, 18.75), 8.0, None)
    assert key_rules_check.own_road_side(SQUARE, curbs, frame, 8.0) == "right"
    assert key_rules_check.own_road_side(SQUARE, FRONT_CURB, frame, 8.0) == "none"
    assert key_rules_check.own_road_side(SQUARE, FRONT_CURB + [((-50.0, 28.0), (50.0, 28.0))], frame, 8.0) == "back"
    assert key_rules_check.own_road_side(SQUARE, curbs, None, 8.0) is None


# R336 the second method of the walls and parameters (R335)
def check_settings():
    return json.loads((key_rules_check.ROOT / "config" / "project.json").read_text(encoding="utf-8"))


def test_r336_a_parameter_is_held_against_the_formula_or_the_table_the_first_method_coded_and_a_level_within_the_hold():
    settings = check_settings()
    report = {"decisions": [{"decision": "parsons parameter RRE_ZZ_SP_Plot-Number", "coded": "plot", "tests": {"plot": {}}},
                            {"decision": "parsons parameter RRE_ZZ_SP_Prototype", "coded": "typology", "tests": {"typology": {"rule": {"parsons | DP2": "DP2"}}}},
                            {"decision": "parsons parameter RRE_ZZ_SP_RVT-Name", "coded": "plotCodeStepsAndFacade", "tests": {"plotCodeStepsAndFacade": {}}},
                            {"decision": "parsons parameter RRE_ZZ_SP_Prototype-FFL", "coded": "ffl", "tests": {"ffl": {}}}]}
    row = {"key": "imx", "plot": "12", "kind": "TU", "typologyRev9": "DP2", "mirrored": "True", "steps": "2", "ffl": "10.000",
           "parameters": json.dumps({"RRE_ZZ_SP_Plot-Number": "12", "RRE_ZZ_SP_Prototype": "DP2", "RRE_ZZ_SP_RVT-Name": "12 | DP2 | 2 | XX | XX",
                                     "RRE_ZZ_SP_Façade-Color": "XX", "RRE_ZZ_SP_Façade-Design-Option": "XX"})}
    ctx = {"readParameters": {("imx", "12"): {"RRE_ZZ_SP_Prototype-FFL": "10.004"}}}
    assert key_rules_check.parameter_verdicts(row, ctx, settings, report) == {"parameter RRE_ZZ_SP_Plot-Number": True, "parameter RRE_ZZ_SP_Prototype": True,
                                                                               "parameter RRE_ZZ_SP_RVT-Name": True, "parameter RRE_ZZ_SP_Prototype-FFL": True}
    # An SE row leaves the link Name out, which an STR link writes as plot | code
    assert "parameter RRE_ZZ_SP_RVT-Name" not in key_rules_check.parameter_verdicts(dict(row, kind="SE"), ctx, settings, report)


def test_r336_a_boundary_wall_is_held_again_on_its_base_top_and_line():
    settings = check_settings()
    profile = json.loads((key_rules_check.ROOT / settings["profile"]).read_text(encoding="utf-8"))
    plan = {"plots": [{"plot": "1", "levels": {"ffl": {"value": 10.0}}, "floors": {"generic": {"level": {"value": "Level 1"}, "levelElevation": {"value": 0.0}}}},
                      {"plot": "2", "levels": {"ffl": {"value": 10.3}}, "floors": {}}],
            "walls": [{"elementId": 7, "type": "BW", "plots": ["1", "2"], "baseConstraint": "Level 1", "baseOffset": 10.2, "heightM": 1.8, "locationLine": 0,
                       "start": [16.0, 0.0, 0], "end": [16.0, 25.0, 0]}]}
    report = {"decisions": [{"decision": "parsons wallBase", "coded": "yardLevelOfTheHigherPlot"}, {"decision": "parsons wallTop", "coded": "higherFflPlusRev6"},
                            {"decision": "parsons wallLine", "coded": "centreOnTheEdge"}]}
    ctx = {"fullPlans": {"imx": plan}, "faces": {"1": SQUARE}}
    assert key_rules_check.wall_verdicts(ctx, settings, profile, report) == {("imx", 7): {"wallBase": True, "wallTop": True, "wallLine": True}}


def test_r336_the_own_front_cover_counts_the_walls_along_the_front_once():
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0)}
    walls = [{"start": [0.0, 0.1, 0], "end": [2.0, 0.1, 0]}, {"start": [1.0, 0.0, 0], "end": [4.0, 0.0, 0]}, {"start": [0.0, 0.0, 0], "end": [0.0, 25.0, 0]}]
    assert key_rules_check.own_front_cover(frame, walls, 0.3) == pytest.approx(4.0 / 16.0)
