import pytest
from shapely.geometry import LineString, Polygon
from shapely.strtree import STRtree

import entry_levels
import road_lookup
import surface_lookup


def ramp(low_ends=(1.00, 1.00), high_ends=(1.04, 1.06), length=10.0, depth=5.0):
    """A ramp with its long edges along x, the low one at y=0 and the high one at y=depth."""
    return [(0.0, 0.0, low_ends[0]), (length, 0.0, low_ends[1]), (length, depth, high_ends[1]), (0.0, depth, high_ends[0])]


def test_r94_the_entry_point_is_the_midpoint_of_the_higher_long_edge_and_the_level_is_the_mean_of_its_two_corners():
    found = entry_levels.ramp_entry(ramp())
    assert found["point"] == pytest.approx((5.0, 5.0)) and found["level"] == pytest.approx(1.05) and found["flat"] is False
    # The same ramp turned over, so the high edge is the one at y=0
    turned = [(x, y, z) for x, y, z in [(0.0, 5.0, 1.00), (10.0, 5.0, 1.00), (10.0, 0.0, 1.04), (0.0, 0.0, 1.06)]]
    again = entry_levels.ramp_entry(turned)
    assert again["point"] == pytest.approx((5.0, 0.0)) and again["level"] == pytest.approx(1.05)


def test_r94_a_ramp_that_rises_along_its_short_edges_still_takes_the_long_edge():
    corners = [(0.0, 0.0, 1.0), (10.0, 0.0, 3.0), (10.0, 5.0, 3.0), (0.0, 5.0, 1.0)]
    found = entry_levels.high_long_edge(corners)
    assert found["rise"] == pytest.approx(0.0) and found["flat"] is True
    # The long edges are the ones along x, whose means are 2.0 and 2.0, so the tie goes to the first
    assert found["mid"][2] == pytest.approx(2.0)


def test_r94_a_ramp_with_level_long_edges_is_flat_and_says_so_and_a_ramp_without_four_corners_has_no_entry():
    flat = entry_levels.ramp_entry(ramp(low_ends=(1.0, 1.0), high_ends=(1.0, 1.0)))
    assert flat["flat"] is True and flat["level"] == pytest.approx(1.0)
    assert entry_levels.ramp_entry(ramp()[:3]) is None and entry_levels.high_long_edge([]) is None
    assert entry_levels.ramp_candidates(ramp()[:2]) == {}


def test_r93_the_candidates_are_the_corners_the_edge_midpoints_and_the_ends_of_the_higher_long_edge():
    found = entry_levels.ramp_candidates(ramp())
    assert found["corner highest"][2] == pytest.approx(1.06) and found["corner lowest"][2] == pytest.approx(1.0)
    assert found["corner mean"][2] == pytest.approx(1.025)
    assert found["higher long edge, midpoint"][2] == pytest.approx(1.05) and found["lower long edge, midpoint"][2] == pytest.approx(1.0)
    assert found["higher long edge, lower end"][2] == pytest.approx(1.04) and found["higher long edge, upper end"][2] == pytest.approx(1.06)
    assert found["edge midpoint highest of four"][2] == pytest.approx(1.05)
    assert "corner nearest the link" not in found


def test_r93_with_the_link_origin_the_candidates_also_hold_the_corner_and_edge_nearest_and_farthest_from_it():
    found = entry_levels.ramp_candidates(ramp(), origin=(0.0, 0.0))
    assert found["corner nearest the link"][:2] == pytest.approx((0.0, 0.0)) and found["corner farthest from the link"][:2] == pytest.approx((10.0, 5.0))
    assert found["edge midpoint nearest the link"][:2] == pytest.approx((0.0, 2.5)) and found["edge midpoint farthest from the link"][:2] == pytest.approx((10.0, 2.5))


def test_r93_the_spread_counts_the_bands_as_they_are_and_after_the_median_is_taken_off():
    values = [0.100, 0.102, 0.108, 0.140, 0.100]
    found = entry_levels.spread(values)
    assert found["n"] == 5 and found["median"] == 0.102 and found["maxAbs"] == 0.14
    assert (found["within5mm"], found["within10mm"], found["within30mm"]) == (0, 0, 0)
    assert (found["within5mmAfterMedian"], found["within10mmAfterMedian"], found["within30mmAfterMedian"]) == (3, 4, 4)
    assert entry_levels.spread([]) == {"n": 0}


def test_r95_the_bands_are_cumulative_a_difference_of_exactly_5_mm_is_inside_and_worse_is_the_rest():
    errors = [0.0, 0.005, -0.005, 0.0051, 0.010, -0.030, 0.0301, 0.5]
    assert entry_levels.band_counts(errors) == {"within5mm": 3, "within10mm": 5, "within30mm": 6, "worse": 2}
    assert entry_levels.band_counts([]) == {"within5mm": 0, "within10mm": 0, "within30mm": 0, "worse": 0}


def square_plot():
    return Polygon([(0, 0), (10, 0), (10, 25), (0, 25)])


def roads_tree(*lines):
    roads = [LineString(line) for line in lines]
    return roads, STRtree(roads)


def test_r94_the_front_edge_is_the_edge_nearest_a_road_line_with_the_curb_point_on_that_line():
    roads, tree = roads_tree([(-5, -4), (15, -4)])
    front = entry_levels.front_edge(square_plot(), tree, roads)
    assert front["mid"] == pytest.approx((5.0, 0.0)) and front["curb"] == pytest.approx((5.0, -4.0))
    assert front["distance"] == pytest.approx(4.0) and front["tied"] is False


def test_r94_a_corner_plot_whose_two_edges_face_roads_is_a_tie_and_an_edge_shorter_than_a_metre_is_no_front():
    roads, tree = roads_tree([(-5, -4), (15, -4)], [(-4, -5), (-4, 30)])
    assert entry_levels.front_edge(square_plot(), tree, roads)["tied"] is True
    chamfer = Polygon([(0.0, 0.5), (0.5, 0.0), (10, 0), (10, 25), (0, 25)])
    roads, tree = roads_tree([(-3, -3), (0.25, 0.25)])
    only = entry_levels.front_edge(chamfer, tree, roads)
    assert only["mid"] == pytest.approx((5.25, 0.0)) and only["tied"] is False
    assert entry_levels.front_edge(square_plot(), None, None) is None and entry_levels.front_edge(square_plot(), None, []) is None


def flat_surface(z, x0=-100, y0=-100, x1=100, y1=100):
    return surface_lookup.Surface([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [(0, 1, 2), (0, 2, 3)])


def test_r94_a_plot_with_no_ramp_gets_the_road_level_at_the_curb_point_plus_the_measured_offset_and_the_surface_that_answered():
    roads, tree = roads_tree([(-5, -4), (15, -4)])
    front = entry_levels.front_edge(square_plot(), tree, roads)
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0))])
    found = entry_levels.road_entry(lookup, front, 0.14)
    assert found["level"] == pytest.approx(3.14) and found["road"] == pytest.approx(3.0) and found["surface"] == "Road" and found["point"] == pytest.approx((5.0, -4.0))


def test_r94_a_curb_point_no_surface_covers_gives_no_level_and_is_never_filled_in():
    roads, tree = roads_tree([(-5, -4), (15, -4)])
    front = entry_levels.front_edge(square_plot(), tree, roads)
    assert entry_levels.road_entry(road_lookup.RoadLevels([("Road", flat_surface(3.0, 50, 50, 60, 60))]), front, 0.14) is None
    assert entry_levels.road_entry(road_lookup.RoadLevels([]), None, 0.14) is None


def test_r94_the_offset_is_the_median_of_entry_minus_road_and_none_without_a_difference():
    assert entry_levels.entry_offset([0.10, 0.20, 0.14]) == pytest.approx(0.14)
    assert entry_levels.entry_offset([0.10, 0.20]) == pytest.approx(0.15)
    assert entry_levels.entry_offset([]) is None
    assert entry_levels.MEASURED_NOTE == "measured from past work"


def test_r94_a_face_that_is_not_one_polygon_has_no_front_edge_and_the_p95_is_the_nearest_rank_of_20_values():
    from shapely.geometry import MultiPolygon
    roads, tree = roads_tree([(-5, -4), (15, -4)])
    parts = MultiPolygon([square_plot(), Polygon([(30, 0), (40, 0), (40, 25)])])
    assert entry_levels.front_edge(parts, tree, roads) is None
    assert entry_levels.spread(list(range(1, 21)))["p95"] == 19 and entry_levels.spread([5])["p95"] == 5
