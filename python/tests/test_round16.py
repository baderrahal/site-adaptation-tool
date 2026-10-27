"""Round 16 on small made up inputs: the levels of two past jobs, the corner rule, the footprint test, the plan of a whole
neighbourhood and the run per prototype code. Every name and number here is invented (R10)."""
import json
import math

import pytest
from shapely.geometry import LineString, Polygon, box
from shapely.strtree import STRtree

import corner_front
import footprint_check
import level_cross
import place_codes
import plan_from_book
import site_spec

BANDS = [0, 1, 10, 30]
SURVEY = {"position": [0, 0, 0], "sharedPosition": [1000.0, 2000.0, 0]}


# R221 to R223 two past jobs

def test_r221_the_gaps_of_two_keys_are_banded_per_field_with_their_sign_and_the_corners_paired_in_plan():
    key14 = {"1": {"ffl": 10.2, "entry": 9.9, "genericTop": 10.2, "corners": [[0, 0, 9.85], [5, 0, 9.85]], "unit": "D4"},
             "2": {"ffl": 10.0, "entry": 9.8, "genericTop": 10.0, "corners": [[9, 0, 9.8], [14, 0, 9.8]], "unit": "D2"},
             "3": {"ffl": 11.0}}
    imc = {"1": {"ffl": 10.0, "entry": 9.9, "genericTop": 10.0, "corners": [[0, 0.001, 9.8], [5, 0, 9.8]], "unit": "DT3"},
           "2": {"ffl": 10.005, "entry": 9.8, "genericTop": 10.005, "corners": [[9, 0, 9.8], [14, 0, 9.8]], "unit": "DP4"}}

    report = level_cross.cross(key14, imc, BANDS)

    assert report["plots"] == 2
    assert report["bands"]["ffl"] == {"worse": 1, "within10mm": 1} and report["bands"]["entry"] == {"equal": 2}
    assert report["signedMm"]["ffl"]["max"] == 200.0 and report["signedMm"]["ffl"]["min"] == -5.0
    assert report["rampCornerBands"] == {"worse": 1, "equal": 1} and report["rampCornerPlanGapMm"]["max"] == 1.0


def test_r223_the_gap_is_also_given_per_unit_of_the_imc_plan():
    key14 = {"1": {"ffl": 10.3}, "2": {"ffl": 10.0}, "3": {"ffl": 10.1}}
    imc = {"1": {"ffl": 10.0, "unit": "DT3"}, "2": {"ffl": 10.0, "unit": "DP4"}, "3": {"ffl": 10.0, "unit": "DT3"}}

    units = level_cross.cross(key14, imc, BANDS)["byUnitMedianMm"]["ffl"]

    assert units == {"DP4": 0.0, "DT3": 200.0}


class Plane:
    """A surface at a fixed level over a box, with no triangle so a snap never looks past its edge."""
    count = 0

    def __init__(self, level, area):
        self.level, self.area = level, area

    def z_at(self, x, y):
        return self.level if self.area.contains(LineString([(x, y), (x, y)]).centroid) else None


def test_r222_each_surface_is_scored_by_the_fronts_it_covers_and_how_near_it_lies():
    fronts = level_cross.fronts_of({"1": {"corners": [[1, 1, 10.0], [2, 1, 10.0], [2, 5, 10.2], [1, 5, 10.2]]}, "2": {}}, ["1", "2"])
    surfaces = [("low", Plane(9.0, box(0, 0, 10, 10))), ("near", Plane(9.995, box(0, 0, 10, 10))), ("half", Plane(10.0, box(0, 0, 1.5, 10)))]

    rows = level_cross.surface_fit(fronts, surfaces, 0.005, BANDS)

    assert list(fronts) == ["1"] and [corner[2] for corner in fronts["1"]] == [10.0, 10.0]
    assert [row["surface"] for row in rows] == ["near", "low", "half"]
    assert rows[0]["medianMm"] == 5.0 and rows[0]["bands"] == {"within10mm": 2}
    assert rows[2]["covered"] == 1 and rows[2]["of"] == 2


# R224 to R226 the corner rule

FACE = Polygon([(0, 0), (20, 0), (20, 30), (0, 30)])
ROADS = [LineString([(-50, -4), (50, -4)]), LineString([(24, -50), (24, 80)])]
TREE = STRtree(ROADS)


def test_r224_road_edges_lie_near_a_curb_and_the_past_ramp_sits_on_the_edge_that_holds_most_of_it():
    edges = corner_front.road_edges(FACE, TREE, ROADS, 8.0)
    ramp = box(5, 0, 15, 5.52)

    assert sorted(round(corner_front.bearing(edge["out"])) for edge in edges) == [90, 180]
    index, held = corner_front.ramp_edge(FACE, ramp, 0.3)
    assert held > 10 and corner_front.edge_index(FACE, next(edge for edge in edges if round(corner_front.bearing(edge["out"])) == 180)) == index


def test_r225_orientation_picks_the_edge_facing_its_quadrant_and_says_nothing_without_one():
    edges = corner_front.road_edges(FACE, TREE, ROADS, 8.0)

    assert round(corner_front.bearing(corner_front.pick("orientation", edges, None, {"Orientation": "WS"}, None)["out"])) == 180
    assert round(corner_front.bearing(corner_front.pick("orientation", edges, None, {"Orientation": "EN"}, None)["out"])) == 90
    assert corner_front.pick("orientation", edges, None, {}, None) is None
    assert corner_front.pick("longest", edges, None, None, None)["length"] == 30
    assert corner_front.pick("shortest", edges, None, None, None)["length"] == 20


def test_r225_a_rule_is_kept_only_with_no_break_in_any_key():
    good = {"breaks": 0, "holds": 5, "cannot": 1}
    report = {"a": {"rules": {rule: {"breaks": 2, "holds": 3, "cannot": 0} for rule in corner_front.RULES}},
              "b": {"rules": {rule: {"breaks": 0, "holds": 3, "cannot": 0} for rule in corner_front.RULES}}}
    report["a"]["rules"]["orientation"] = good

    assert corner_front.kept_rule(report) == "orientation"
    report["b"]["rules"]["orientation"] = {"breaks": 1, "holds": 3, "cannot": 0}
    assert corner_front.kept_rule(report) is None


def test_r226_a_corner_plot_takes_the_front_of_its_orientation_and_none_without_one():
    limits = (8.0, 40.0)

    east, rule = corner_front.corner_frame(FACE, TREE, ROADS, limits, {"roadEdgeM": 8.0}, {"Orientation": "EN"})
    south, _ = corner_front.corner_frame(FACE, TREE, ROADS, limits, {"roadEdgeM": 8.0}, {"Orientation": "WS"})
    none, why = corner_front.corner_frame(FACE, TREE, ROADS, limits, {"roadEdgeM": 8.0}, {})

    assert rule == "orientation" and east["n"] == pytest.approx((-1.0, 0.0)) and east["length"] == 30
    assert south["n"] == pytest.approx((0.0, 1.0))
    assert none is None and "no Orientation" in why
    single = Polygon([(0, 0), (20, 0), (20, 30), (0, 30)])
    frame, rule = corner_front.corner_frame(single, STRtree(ROADS[:1]), ROADS[:1], limits, {"roadEdgeM": 8.0}, {"Orientation": "EN"})
    assert rule == "nearestCurb" and frame["n"] == pytest.approx((0.0, 1.0))


# R227 to R229 the footprint test

def floor(level, ring):
    return {"levelElevation": level, "outline": [[[x + 1000.0, y + 2000.0, level] for x, y in ring]]}


def test_r227_the_house_is_the_union_of_the_ground_floors_in_the_prototype_coordinates():
    document = {"surveyPoint": SURVEY, "floors": [floor(-0.075, [(0, 0), (10, 0), (10, 8), (0, 8)]), floor(0.0, [(10, 0), (12, 0), (12, 8), (10, 8)]),
                                                  floor(3.2, [(0, 0), (30, 0), (30, 30), (0, 30)])]}

    house = footprint_check.house_outline(document, 0.5)

    assert house.area == pytest.approx(96.0, abs=0.01) and house.bounds == pytest.approx((0, 0, 12, 8), abs=0.01)


def test_r227_the_house_is_put_in_place_by_the_link_origin_and_axes_with_a_mirror():
    house = box(0, 0, 4, 2)
    link = {"transform": {"origin": [10.0, 0.0, 0.0], "axisX": [0, 1, 0], "axisY": [1, 0, 0]}}

    placed = footprint_check.place(house, link, [100.0, 0.0, 0.0])

    assert placed.bounds == pytest.approx((110, 0, 112, 4))


def test_r228_overlap_offset_turn_and_the_mirror_test_of_a_link():
    footprint = box(0, 0, 10, 10)
    l_shape = Polygon([(0, 0), (6, 0), (6, 4), (2, 4), (2, 10), (0, 10)])
    right = footprint_check.measure(l_shape, footprint.intersection(Polygon([(0, 0), (6, 0), (6, 4), (2, 4), (2, 10), (0, 10)])), (0.0, 1.0), 0.05)
    wrong = footprint_check.measure(footprint_check.flipped(l_shape, (0.0, 1.0)), l_shape, (0.0, 1.0), 0.05)

    assert right["share"] == 1.0 and right["offsetM"] == 0 and right["mirrorFlag"] is False
    assert wrong["mirrorFlag"] is True and wrong["share"] < wrong["flippedShare"]


def test_r228_the_footprint_is_the_block_of_the_wired_name_that_shares_most_of_the_face_clipped_to_it():
    face = box(0, 0, 10, 20)
    index = {"Duplex_X": [box(-10, 0, 10, 20), box(8, 0, 30, 20)]}

    clipped = footprint_check.plot_footprint({"block": "Duplex_X", "face": face}, index)

    assert clipped.area == pytest.approx(200.0)
    assert footprint_check.plot_footprint({"block": "Other", "face": face}, index) is None


def test_r229_a_link_passes_inside_the_key_band_of_its_unit_and_fails_outside_it():
    key = {"1": {"share": 0.57, "offsetM": 5.19, "mirrorFlag": False, "flippedShare": 0.5}, "2": {"share": 0.571, "offsetM": 5.195, "mirrorFlag": False, "flippedShare": 0.5}}
    units = {"1": "DT3", "2": "DT3", "3": "DT3", "4": "DT3", "5": "VL9"}
    learned = footprint_check.learn(key, units)
    test = {"3": {"share": 0.5705, "offsetM": 5.2, "mirrorFlag": False, "flippedShare": 0.5}, "4": {"share": 0.77, "offsetM": 2.2, "mirrorFlag": False, "flippedShare": 0.5},
            "5": {"share": 0.6, "offsetM": 1.0, "mirrorFlag": False, "flippedShare": 0.5}}

    judged = footprint_check.judge(test, units, learned, 0.05)

    assert learned["DT3"]["residualMaxM"] == pytest.approx(0.0025) and learned["DT3"]["n"] == 2
    assert judged["pass"] == 1 and judged["fail"] == 2
    assert judged["worst10"][0]["plot"] == "5" and "holds no link" in judged["worst10"][0]["reasons"][0]
    assert judged["residualBands"] == {"within10mm": 1, "worse": 1, "noGroup": 1}


# R230 the plan of a whole neighbourhood

def test_r230_a_neighbourhood_gives_its_unit_plots_and_a_plot_with_no_row_only_leaves_the_box(tmp_path):
    labels = tmp_path / "labels.csv"
    labels.write_text("number,kind,nbh\n12,clientCode,N1\n9,clientCode,N1\n12,clientCode,N1\n5,landUse,N1\n7,clientCode,N2\n", encoding="utf-8")
    plots = tmp_path / "plots.csv"
    plots.write_text("locationPlanNumber,centroidX,centroidY\n9,1.0,2.0\n", encoding="utf-8")

    assert plan_from_book.nbh_plots(str(labels), "N1") == ["9", "12"]
    assert site_spec.plot_centroids(str(plots), ["9", "12"]) == ([[1.0, 2.0]], ["12"])


# R231 one run per code

def test_r231_the_codes_follow_the_order_with_their_plot_counts():
    plan = {"plots": [{"prototypeFile": {"value": "A-KEO-ARC-DT3-ZZ-MOD-1.rvt"}}, {"prototypeFile": {"value": "a-keo-arc-dt3-zz-mod-1.rvt"}},
                      {"prototypeFile": {"value": "A-KEO-ARC-C10-ZZ-MOD-1.rvt"}}]}

    assert place_codes.plan_codes(plan, ["C10", "TH1", "DT3"]) == [("C10", 1), ("DT3", 2)]


def test_r231_a_code_waits_for_memory_to_settle_and_the_run_stops_with_the_codes_left(tmp_path):
    block = {"folder": str(tmp_path), "minFreeGB": 2.0, "placeSettleMinutes": 2}
    (tmp_path / "logs").mkdir()
    memory = iter([1.5, 2.5, 3.0, 1.0, 1.0, 1.0, 1.0])
    rows, slept = [], []

    runs, left = place_codes.run([("C10", 3), ("DT3", 5), ("TH1", 2)], tmp_path / "plan.json", block, 161, rows.append,
                                 free=lambda: next(memory), launch=lambda job, code, spec: 0, sleep=slept.append)

    assert [row["code"] for row in runs] == ["C10"] and runs[0]["jobCode"] == "S161" and runs[0]["freeGBBefore"] == 2.5
    assert left == ["DT3", "TH1"] and rows[-1]["event"] == "stopped"
    assert json.loads((tmp_path / "logs" / "spec_S161_links.json").read_text(encoding="utf-8"))["prototypeCode"] == "C10"


def test_r231_a_failed_run_stops_the_next_codes():
    block = {"folder": "unused", "minFreeGB": 2.0, "placeSettleMinutes": 0}

    with pytest.raises(OSError):
        place_codes.run([("C10", 1)], "plan.json", {**block, "folder": "Z:/no/such"}, 161, list().append, free=lambda: 5.0, launch=lambda *a: 0)
    assert math.isclose(place_codes.settle(lambda: 1.0, 2.0, 0, sleep=lambda s: None) or 0.0, 0.0)


# R235 second method

def test_r235_the_ray_cast_and_the_grid_give_share_and_offset_of_two_areas():
    import z16_check

    square = [(0, 0), (10, 0), (10, 10), (0, 10)]
    half = [(0, 0), (5, 0), (5, 10), (0, 10)]
    share, offset = z16_check.grid_share({"rings": [half], "test": lambda p: z16_check.inside(p, half)},
                                         {"rings": [square], "test": lambda p: z16_check.inside(p, square)}, 0.5)

    assert z16_check.inside((1, 1), square) and not z16_check.inside((11, 1), square)
    assert share == pytest.approx(0.5) and offset == pytest.approx(2.5)
    assert z16_check.segment_distance((5, 3), (0, 0), (10, 0)) == 3 and z16_check.segment_distance((-3, 4), (0, 0), (10, 0)) == 5


def test_r235_the_level_part_takes_the_floor_top_less_the_raw_datum_against_the_report():
    import z16_check

    key14 = {"7": {"floors": {"generic": {"z": {"value": 110.2}}}}}
    imc = {"7": {"levels": {"ffl": {"value": 10.0}}}}

    rows = z16_check.level_part(["7"], key14, imc, 100.0, {"7": {"fflGapMm": 200.0}})
    off = z16_check.level_part(["7"], key14, imc, 100.0, {"7": {"fflGapMm": 205.0}})

    assert rows[0]["agree"] is True and rows[0]["gapMm"] == pytest.approx(200.0) and off[0]["agree"] is False
    assert z16_check.band(0.0, [0, 1, 10, 30]) == "equal" and z16_check.band(0.02, [0, 1, 10, 30]) == "within30mm"


# R236 clusters from the footprint blocks

def test_r236_plots_sharing_one_footprint_block_are_a_cluster_and_a_lone_plot_is_none():
    rows = {"1": {"footprintBlock": "Row_T"}, "2": {"footprintBlock": "Row_T"}, "3": {"footprintBlock": "Row_T"}, "4": {"footprintBlock": "Villa"}, "5": {}}
    faces = {"1": box(0, 0, 10, 20), "2": box(10, 0, 20, 20), "3": box(30, 0, 40, 20), "4": box(50, 0, 60, 20), "5": box(70, 0, 80, 20)}
    index = {"Row_T": [box(0, 0, 20, 18), box(30, 0, 40, 18)], "Villa": [box(50, 0, 60, 18)]}

    clusters = plan_from_book.footprint_clusters(["1", "2", "3", "4", "5"], rows, faces, index)

    assert clusters == [{"code": "Row_T", "plots": ["1", "2"]}]
