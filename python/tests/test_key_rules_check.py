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


def made_up(tmp_path, decisions=None, later=None):
    """Four parsons plots with a curb in front: three 16 m and 18 m faces whose link stands 10 m before the end corner, so the own
    fit takes the end anchor, and one 16 m face whose link stands 2 m off it, the one miss of the dry list."""
    past = tmp_path / "past_all.csv"
    fields = ["key", "source", "kind", "plot", "typologyRev9", "x", "y", "rotationDeg", "mirrored", "linkFile"]
    faces = {"1": (0.0, 16.0), "2": (100.0, 16.0), "3": (200.0, 18.0), "4": (300.0, 16.0)}
    xs = {"1": 6.0, "2": 106.0, "3": 208.0, "4": 308.0}
    with open(past, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for plot in faces:
            writer.writerow({"key": "imx", "source": "parsons", "kind": "TU", "plot": plot, "typologyRev9": "C10", "x": xs[plot], "y": 1.5, "rotationDeg": 0.0,
                             "mirrored": "False", "linkFile": "a.rvt"})
    plots = tmp_path / "plots.csv"
    plots.write_text("locationPlanNumber,baseplanLabel\n" + "".join("%s,1%s\n" % (plot, plot) for plot in faces), encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": "1" + plot, "vertices": [[x, 0.0], [x + width, 0.0], [x + width, 25.0], [x, 25.0]]}
                                              for plot, (x, width) in faces.items()]}), encoding="utf-8")
    roads = tmp_path / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_text(json.dumps({"pieces": [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-50, -3, 0], [400, -3, 0]]}]}), encoding="utf-8")
    sales = tmp_path / "sales.xlsx"
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "S"
    sheet.append(["Plot No", "Orientation"])
    for plot in faces:
        sheet.append([int(plot), "EN"])
    book.save(sales)
    (tmp_path / "blocks.json").write_text(json.dumps({"source": "made up", "blocks": []}), encoding="utf-8")
    majority_json = tmp_path / "majority.json"
    decision = {"decision": "parsons linkOrigin", "plots": 4, "coded": "plotFrameRefit", "field": "insert",
                "tests": {"plotFrameRefit": {"holds": 3, "breaks": 1, "cannot": 0, "byKey": {"imx": {"holds": 3, "breaks": 1}}}}}
    majority_json.write_text(json.dumps({"decisions": decisions if decisions is not None else [decision], "siteFacts": {}}), encoding="utf-8")
    overrides = tmp_path / "overrides.json"
    overrides.write_text(json.dumps({"overrides": [{"plot": "4", "key": "imx", "field": "insert", "decision": "parsons linkOrigin"}]}), encoding="utf-8")
    settings = tmp_path / "project.json"
    real = json.loads((key_rules_check.ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    real["majority"]["check"] = {"seeds": [14, 7], "sample": 2, "group": "parsons"}
    if later is not None:
        real["majority"]["overrideKeep"]["laterFields"] = later
    # The folder a build links its prototype files from, made up and empty here (R337)
    real["inputRoot"] = str(tmp_path / "input")
    (tmp_path / "input" / real["pastJob"]["upgradedPrototypeFolder"]).mkdir(parents=True)
    settings.write_text(json.dumps(real), encoding="utf-8")
    return ["--settings", str(settings), "--past-all", str(past), "--majority", str(majority_json), "--overrides", str(overrides), "--plots-csv", str(plots),
            "--baseplan", str(baseplan), "--roads", str(roads), "--sales", str(sales),
            "--footprint-blocks", str(tmp_path / "blocks.json"), "--out", str(tmp_path / "out" / "check.json")], tmp_path / "out" / "check.json"


def test_r336_the_check_holds_each_sampled_plot_again_and_counts_its_verdict_against_the_first_method(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {plot: {"Orientation": "EN"} for plot in "1234"})
    argv, out = made_up(tmp_path)
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    for seed in ("14", "7"):
        assert found["seeds"][seed]["counts"] == {"linkOrigin match": 2} and found["seeds"][seed]["mismatches"] == []
    assert found["seeds"]["every"]["counts"] == {"linkOrigin match": 4}
    assert found["totals"]["parsons linkOrigin"]["match"] is True and found["cannotCheck"] == {}


def test_r336_a_first_method_that_missed_no_plot_where_the_route_finds_a_miss_is_a_mismatch(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {})
    argv, out = made_up(tmp_path)
    (tmp_path / "overrides.json").write_text(json.dumps({"overrides": []}), encoding="utf-8")
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    assert found["seeds"]["every"]["counts"]["linkOrigin mismatch"] == 1
    assert found["seeds"]["every"]["mismatches"][0]["plot"] == "4" and found["seeds"]["every"]["mismatches"][0]["own"] == "misses"


def test_r336_the_check_writes_nothing_inside_the_repo(tmp_path):
    argv, _ = made_up(tmp_path)
    argv[-1] = str(key_rules_check.ROOT / "check.json")
    assert key_rules_check.main(argv) == 2
    assert not (key_rules_check.ROOT / "check.json").exists()


# R336 any coded candidate, a decision the route can not check is a failure (Round ADDIN 3a review)
def test_r336_any_coded_candidate_is_held_by_its_own_value_and_a_candidate_with_no_route_gives_none():
    hand = {"coded": "orientation", "tests": {"orientation": {"rule": {"('parsons', 'a.rvt') | EN": True}}}}
    same = lambda guess, aim: guess == aim
    assert key_rules_check.evaluate(hand, {"orientation": "EN"}, {}, True, ("parsons", "a.rvt"), same) is True
    assert key_rules_check.evaluate(hand, {"orientation": "EN"}, {}, False, ("parsons", "a.rvt"), same) is False
    # A factor value the table never learned is a miss, as the first method lists a cannot
    assert key_rules_check.evaluate(hand, {"orientation": "WS"}, {}, True, ("parsons", "a.rvt"), same) is False
    rotation = {"coded": "nearestCurb", "tests": {"nearestCurb": {}}}
    assert key_rules_check.evaluate(rotation, {}, {"nearestCurb": lambda: 5.0}, 5.0, "parsons", same) is True
    assert key_rules_check.evaluate({"coded": "magic", "tests": {"magic": {}}}, {"all": "all"}, {}, True, "parsons", same) is None


def test_r336_a_decision_whose_coded_candidate_has_no_route_fails_the_run_and_a_level_left_to_3c_is_listed(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {})
    origin = {"decision": "parsons linkOrigin", "plots": 4, "coded": "magicFit", "field": "insert",
              "tests": {"magicFit": {"holds": 3, "breaks": 1, "cannot": 0, "byKey": {"imx": {"holds": 3, "breaks": 1}}}}}
    entry = {"decision": "parsons entry", "plots": 4, "coded": "roadLevel", "field": "entryM",
             "tests": {"roadLevel": {"holds": 4, "breaks": 0, "cannot": 0, "byKey": {"imx": {"holds": 4}}}}}
    # The levels left to Phase 3c in the config of Round ADDIN 3a, made up since 3c measured them (R339)
    argv, out = made_up(tmp_path, [origin, entry], later={"parsons": ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]})
    assert key_rules_check.main(argv) == 1
    found = json.loads(out.read_text(encoding="utf-8"))
    assert found["cannotCheck"] == {"linkOrigin": 4} and found["leftForLater"] == ["entry"]


# R336 the fits are made again by the own route, never taken from majority.json
def test_r336_the_link_fit_per_code_is_made_again_with_the_anchor_most_links_hold():
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0)}
    wide = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (18.0, 0.0)}
    links = [("C10", frame, False, (6.0, 1.5)), ("C10", frame, False, (6.0, 1.5)), ("C10", wide, False, (8.0, 1.5)), ("DP2", frame, True, (10.0, 0.2))]
    fits = key_rules_check.own_fits(links)
    assert fits["C10"] == {"anchor": "end", "offset": [-10.0, 1.5]}
    # One link holds both anchors, a tie goes to start
    assert fits["DP2"] == {"anchor": "start", "offset": [6.0, 0.2]}


def test_r336_the_yard_outline_of_a_group_is_made_again_from_the_plot_of_its_fit_half_nearest_the_others():
    square = lambda size: [[(0.0, 0.0), (size, 0.0), (size, 3.0), (0.0, 3.0)]]
    rows = [{"plot": str(plot), "unit": "DP2", "mirrored": False, "width": 12.0, "loops": square(size)} for plot, size in ((1, 5.0), (2, 5.01), (3, 5.0), (4, 9.0), (5, 5.02))]
    fits = key_rules_check.own_refit(rows, 14)
    loops = fits[("DP2", False, 12.0)]
    assert key_rules_check.own_boundary_gap(loops, square(5.0)) <= 0.021
    few = key_rules_check.own_refit(rows[:3], 14)
    assert few[("DP2", False, 12.0)] == [[(0.0, 0.0), (5.0, 0.0), (5.0, 3.0), (0.0, 3.0)]]


def test_r354_the_check_makes_the_clusters_again_from_the_footprint_blocks_alone_and_refuses_an_analysis(tmp_path, capsys):
    faces = {str(plot): [(plot * 10.0, 0.0), (plot * 10.0 + 10.0, 0.0), (plot * 10.0 + 10.0, 25.0), (plot * 10.0, 25.0)] for plot in range(1, 6)}
    book = {plot: {"baseplanLabel": "L" + plot, "footprintBlock": "Duplex" if plot in ("1", "2", "3", "4") else ""} for plot in faces}
    blocks = [{"name": "Duplex", "outline": [[10.0, 5.0], [30.0, 5.0], [30.0, 20.0], [10.0, 20.0]]}, {"name": "Duplex", "outline": [[60.0, 5.0], [70.0, 5.0], [70.0, 20.0]]}]
    # 3 and 4 name a block that lies on neither, and since Q089 no analysis makes them a cluster
    assert key_rules_check.own_clusters(sorted(faces), book, faces, blocks) == [["1", "2"]]
    argv, _ = made_up(tmp_path)
    assert key_rules_check.main(argv + ["--analysis", str(tmp_path / "analysis.json")]) == 2
    assert capsys.readouterr().err.strip() == ("key_rules_check: the analysis is no input of the majority rule since Q089, it measures on the footprint "
                                               "clusters of R236 alone (R354)")


def test_r336_the_area_two_rings_share_is_exact_for_a_ring_with_a_reflex_corner():
    ell = [(0.0, 0.0), (20.0, 0.0), (20.0, 10.0), (10.0, 10.0), (10.0, 20.0), (0.0, 20.0)]
    square = [(5.0, 5.0), (15.0, 5.0), (15.0, 15.0), (5.0, 15.0)]
    assert key_rules_check.overlap_area(square, ell) == pytest.approx(75.0)
    assert key_rules_check.overlap_area(ell, square) == pytest.approx(75.0)
    assert key_rules_check.whole_face_share("POLYGON ((0 0, 20 0, 20 10, 10 10, 10 20, 0 20, 0 0))", ell) == pytest.approx(1.0)
    assert key_rules_check.whole_face_share("POLYGON ((5 5, 15 5, 15 15, 5 15, 5 5))", ell) == pytest.approx(75.0 / 325.0)


# R330 the merged list, every entry on a plot the join gives its key
def test_r336_the_merged_list_is_held_on_the_join_and_its_run_entries_against_the_dry_list():
    table = [{"key": "imc", "plot": "2300"}, {"key": "u12", "plot": "1"}]
    run = {"plot": "2300", "field": "mirrored", "value": True, "source": "parsons", "key": "imc"}
    kept = {"plot": "2312", "field": "entryM", "value": {"gapToRuleM": 0.1}, "source": "parsons", "key": "imc"}
    native = {"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.2}, "source": "native", "key": "u12"}
    keep = {"native": None, "parsons": {"entryM"}}
    found = key_rules_check.merged_check([native, run, kept], [run, dict(kept, value={"gapToRuleM": 0.9})], table, keep, {"entryM"})
    assert found["offJoin"] == [{"key": "imc", "plot": "2312", "field": "entryM", "source": "parsons"}]
    assert found["runEntriesEqualDry"] is True and found["laterFieldsInRun"] == ["entryM"]
    found = key_rules_check.merged_check([native, kept], [run], table, keep, {"entryM"})
    assert found["runEntriesEqualDry"] is False and found["onlyDry"] == 1


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


def test_r336_main_holds_the_merged_list_on_the_join_and_names_an_entry_off_it(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {})
    argv, out = made_up(tmp_path, later={"parsons": ["entryM", "heightM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]})
    merged = tmp_path / "merged.json"
    merged.write_text(json.dumps({"overrides": [{"plot": "4", "key": "imx", "field": "insert", "value": None, "source": "parsons", "decision": "parsons linkOrigin"},
                                                {"plot": "9", "key": "imx", "field": "entryM", "value": None, "source": "parsons", "decision": "parsons entry"}]}), encoding="utf-8")
    (tmp_path / "overrides.json").write_text(json.dumps({"overrides": [{"plot": "4", "key": "imx", "field": "insert", "value": None, "source": "parsons",
                                                                         "decision": "parsons linkOrigin"}]}), encoding="utf-8")
    earlier = tmp_path / "earlier.json"
    earlier.write_text(json.dumps({"overrides": []}), encoding="utf-8")
    # The merged list is held against the list before the run, a merged check with no earlier list is refused
    assert key_rules_check.main(argv + ["--merged", str(merged)]) == 2
    # An entry off the join fails the check and the run (Round ADDIN 3a, last pass)
    assert key_rules_check.main(argv + ["--merged", str(merged), "--earlier", str(earlier)]) == 1
    found = json.loads(out.read_text(encoding="utf-8"))["merged"]
    assert found["offJoin"] == [{"key": "imx", "plot": "9", "field": "entryM", "source": "parsons"}] and found["runEntriesEqualDry"] is True
    assert found["ok"] is False
