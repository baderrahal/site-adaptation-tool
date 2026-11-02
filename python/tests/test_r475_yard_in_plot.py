"""R475. The yard of a plot cut to its face and by its ramp on the grid of 1 mm by the one clipper, in one fixed order. Every outline made up (R10).
R475_YardInPlotTests.cs holds the same cases and the same expected values."""
from shapely.geometry import MultiPolygon, Polygon

import plan_from_book
import plan_from_past
import test_level_floor_steps as floor_steps
from test_level_floor_steps import made_build

# A plot 20 m wide and 30 m deep and its ramp on the front, drawn as the build draws them, the face not from its smallest corner
FACE = [[20.0, 0.0], [20.0, 30.0], [0.0, 30.0], [0.0, 0.0]]
RAMP = [[5.0, 0.0], [11.0, 0.0], [11.0, 6.0], [5.0, 6.0]]


def test_r475_a_yard_past_the_face_is_cut_to_it():
    assert plan_from_book.yard_in_plot([[[2.0, 10.0], [25.0, 10.0], [25.0, 25.0], [2.0, 25.0]]], FACE, RAMP) == [[[2.0, 10.0], [20.0, 10.0], [20.0, 25.0], [2.0, 25.0]]]


def test_r475_a_yard_on_the_ramp_is_cut_by_it():
    cut = plan_from_book.yard_in_plot([[[2.0, 4.0], [18.0, 4.0], [18.0, 25.0], [2.0, 25.0]]], FACE, RAMP)
    assert cut == [[[2.0, 4.0], [5.0, 4.0], [5.0, 6.0], [11.0, 6.0], [11.0, 4.0], [18.0, 4.0], [18.0, 25.0], [2.0, 25.0]]]


def test_r475_a_concave_yard_keeps_its_notch():
    cut = plan_from_book.yard_in_plot([[[10.0, 10.0], [25.0, 10.0], [25.0, 14.0], [14.0, 14.0], [14.0, 20.0], [10.0, 20.0]]], FACE, RAMP)
    assert cut == [[[10.0, 10.0], [20.0, 10.0], [20.0, 14.0], [14.0, 14.0], [14.0, 20.0], [10.0, 20.0]]]


def test_r475_a_yard_with_a_hole_is_even_odd_and_the_outer_ring_comes_first():
    loops = [[[8.0, 15.0], [12.0, 15.0], [12.0, 20.0], [8.0, 20.0]], [[1.0, 8.0], [25.0, 8.0], [25.0, 28.0], [1.0, 28.0]]]
    # The hole winds the other way, from its smallest corner, so its second vertex lies above
    assert plan_from_book.yard_in_plot(loops, FACE, RAMP) == [[[1.0, 8.0], [20.0, 8.0], [20.0, 28.0], [1.0, 28.0]], [[8.0, 15.0], [8.0, 20.0], [12.0, 20.0], [12.0, 15.0]]]


def test_r475_a_yard_of_two_loops_keeps_both_rings_sorted_by_first_vertex():
    loops = [[[15.0, 10.0], [25.0, 10.0], [25.0, 12.0], [15.0, 12.0]], [[2.0, 10.0], [6.0, 10.0], [6.0, 12.0], [2.0, 12.0]]]
    assert plan_from_book.yard_in_plot(loops, FACE, RAMP) == [[[2.0, 10.0], [6.0, 10.0], [6.0, 12.0], [2.0, 12.0]], [[15.0, 10.0], [20.0, 10.0], [20.0, 12.0], [15.0, 12.0]]]


def test_r475_a_yard_inside_is_returned_as_it_was_given_byte_for_byte():
    # Off the grid, wound clockwise, a collinear vertex and not from its smallest corner: nothing of it changes
    loops = [[[10.00012, 10.00049], [10.00012, 20.1], [15.0, 20.1], [18.1234, 20.1], [18.1234, 10.00049]]]
    given = repr(loops)
    assert plan_from_book.yard_in_plot(loops, FACE, RAMP) is loops and repr(loops) == given


def test_r475_a_yard_that_only_touches_the_face_and_the_ramp_is_returned_as_it_was_given():
    loops = [[[0.0, 6.0], [20.0, 6.0], [20.0, 30.0], [0.0, 30.0]]]
    assert plan_from_book.yard_in_plot(loops, FACE, RAMP) is loops


def test_r475_a_yard_the_cut_leaves_empty_gives_no_loops():
    assert plan_from_book.yard_in_plot([[[30.0, 10.0], [40.0, 10.0], [40.0, 20.0], [30.0, 20.0]]], FACE, RAMP) == []
    assert plan_from_book.yard_in_plot([[[6.0, 1.0], [10.0, 1.0], [10.0, 5.0], [6.0, 5.0]]], FACE, RAMP) == []


def test_r475_a_plot_with_no_face_or_no_ramp_clips_by_what_it_has():
    loops = [[[2.0, 4.0], [25.0, 4.0], [25.0, 25.0], [2.0, 25.0]]]
    assert plan_from_book.yard_in_plot(loops, FACE, None) == [[[2.0, 4.0], [20.0, 4.0], [20.0, 25.0], [2.0, 25.0]]]
    assert plan_from_book.yard_in_plot(loops, None, RAMP) == [[[2.0, 4.0], [5.0, 4.0], [5.0, 6.0], [11.0, 6.0], [11.0, 4.0], [25.0, 4.0], [25.0, 25.0], [2.0, 25.0]]]
    assert plan_from_book.yard_in_plot(loops, None, None) is loops


def test_r475_the_shared_case_gives_the_same_rings_in_the_same_order_in_both_languages():
    # Drawn clockwise from the middle of an edge on the mm grid and off it. The ramp cuts it in two, the face takes its right side, each part holds a hole or not
    loops = [[[3.0004, 3.5], [3.0004, 26.0], [25.0, 26.0], [25.0, 3.5]], [[14.0, 12.0], [14.0, 18.0], [17.0, 18.0], [17.0, 12.0]], [[0.5, 28.0], [4.0, 29.5], [4.0, 28.0]]]
    ramp = [[-1.0, 8.0], [30.0, 8.0], [30.0, 9.0], [-1.0, 9.0]]
    assert plan_from_book.yard_in_plot(loops, FACE, ramp) == SHARED


# The rings of the shared case, the same list in R475_YardInPlotTests.cs: outer rings anticlockwise, then holes clockwise, each from its smallest (x, y), sorted by it
SHARED = [[[0.5, 28.0], [4.0, 28.0], [4.0, 29.5]],
          [[3.0, 3.5], [20.0, 3.5], [20.0, 8.0], [3.0, 8.0]],
          [[3.0, 9.0], [20.0, 9.0], [20.0, 26.0], [3.0, 26.0]],
          [[14.0, 12.0], [14.0, 18.0], [17.0, 18.0], [17.0, 12.0]]]


def test_r475_a_second_way_shapely_finds_the_same_area_and_no_area_past_the_face_or_on_the_ramp():
    loops = [[[3.0, 3.5], [3.0, 26.0], [25.0, 26.0], [25.0, 3.5]], [[14.0, 12.0], [14.0, 18.0], [17.0, 18.0], [17.0, 12.0]]]
    cut = plan_from_book.yard_in_plot(loops, FACE, RAMP)
    shape, face, ramp = even_odd(cut), Polygon(FACE), Polygon(RAMP)
    expected = even_odd(loops).intersection(face).difference(ramp)
    assert abs(shape.area - expected.area) < 1e-9 and shape.difference(face).area < 1e-9 and shape.intersection(ramp).area < 1e-9


def even_odd(loops):
    """A shape of rings by even-odd fill, each ring turning over what lies under it."""
    shape = Polygon()
    for ring in loops:
        shape = shape.symmetric_difference(Polygon(ring))
    return shape if isinstance(shape, (Polygon, MultiPolygon)) else shape.buffer(0)


# The made up row of test_level_floor_steps.py, a plot 12 m wide with its ramp 5.52 m deep. Plot 5, x 48 to 60, takes its link at the corner of its face, in the link frame the
# yard runs past the plot on its right and over the ramp
PAST = {"unit": "DP2", "mirrored": False, "width": 12.0, "type": "Yard made up", "loops": [[[1.0, 2.0], [15.0, 2.0], [15.0, 10.0], [1.0, 10.0]]]}
# All of it on the plot of its right, past the face
OUTSIDE = {"unit": "DP2", "mirrored": False, "width": 12.0, "type": "Yard made up", "loops": [[[20.0, 8.0], [22.0, 8.0], [22.0, 10.0], [20.0, 10.0]]]}
GONE = "the cut of R475 leaves the fitted yard outline empty"


def cut_of(built, number, written):
    """The rings the plan writes for a plot, each a list of points, and the hand made cut of the face and ramp by shapely as a second way."""
    entry = built["plan"][number]["floors"]
    rings = [loop["points"] for loop in entry["yard"]["outline"]["value"]["loops"]]
    assert written == rings
    face, ramp = Polygon(entry["generic"]["outline"]["value"]["loops"][0]["points"]), Polygon(entry["ramp"]["outline"]["value"]["loops"][0]["points"])
    assert Polygon(rings[0]).difference(face).area < 1e-9 and Polygon(rings[0]).intersection(ramp).area < 1e-9
    return rings


def test_r475_the_steps_floors_and_overrides_and_the_build_write_the_one_cut_yard(monkeypatch):
    monkeypatch.setattr(floor_steps, "YARD", PAST)
    built = made_build(monkeypatch, {})
    insert = plan_from_past.point(built["links"]["5"]["insert"])
    rings = cut_of(built, "5", built["floors"]["5"]["yardOutline"])
    # By hand: x cut at the face edge 60, y from the back of the ramp 5.52 where the link puts the yard below it
    assert rings == [[[insert[0] + 1.0, max(insert[1] + 2.0, 5.52)], [60.0, max(insert[1] + 2.0, 5.52)], [60.0, insert[1] + 10.0], [insert[0] + 1.0, insert[1] + 10.0]]]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert [[plan_from_past.point(point) for point in loop] for loop in made["5"]["yardOutline"]] == rings


def test_r475_a_ramp_on_the_open_edge_of_q116_cuts_the_yard_where_it_ends(monkeypatch):
    monkeypatch.setattr(floor_steps, "YARD", dict(PAST, loops=[[[10.0, -8.0], [14.0, -8.0], [14.0, -2.0], [10.0, -2.0]]]))
    built = made_build(monkeypatch, {"5": {"rotationDeg": {"gapToRuleDeg": 90.0}}})
    entry = built["plan"]["5"]["floors"]
    # By hand: turned 90 degrees the yard lies x 52.005 to 58.005 and y 10.155 to 14.155, far behind the ramp of the front, y 0 to 5.52. The ramp on the
    # open edge covers x 54.48 to 60, so the cut stops the yard at 54.48
    assert "rotationDeg" in built["plan"]["5"]["overrides"]["applied"]
    assert [loop["points"] for loop in entry["yard"]["outline"]["value"]["loops"]] == [[[52.005, 10.155], [54.48, 10.155], [54.48, 14.155], [52.005, 14.155]]]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert [plan_from_past.point(point) for point in made["5"]["rampOutline"]] == entry["ramp"]["outline"]["value"]["loops"][0]["points"]
    assert [[plan_from_past.point(point) for point in loop] for loop in made["5"]["yardOutline"]] == [loop["points"] for loop in entry["yard"]["outline"]["value"]["loops"]]


def test_r475_a_yard_the_cut_leaves_empty_is_the_no_yard_path_with_its_line_before_the_ramp(monkeypatch):
    monkeypatch.setattr(floor_steps, "YARD", OUTSIDE)
    built = made_build(monkeypatch, {})
    five = built["floors"]["5"]
    assert (five["yardOutline"], five["yardType"], five["yardUnit"], five["yardWidth"]) == (None, None, "DP2", 12.0)
    assert five["lacks"] == [{"key": "floors.yard", "why": GONE}]
    assert "yard" not in built["plan"]["5"]["floors"] and {"key": "floors.yard", "why": GONE} in built["missing"]["5"]["missing"]
    made = plan_from_book.override_step(built["scope"], built["rows"], built["trace"], built["hands"], built["files"], built["rules"], built["profile"], 0.5)
    assert (made["5"]["yardOutline"], made["5"]["yardZ"]) == (None, None)
