"""R415 and R416, round 4j-2. The irregular plot by the tolerance of the rules profile and the boundary wall of an irregular plot with its outer
face on the plot line under the Rev 6 mode (Manual Rev 06 2.8.A), on faces made up in code (R10). R415_IrregularWallTests.cs of the C# holds
the same faces and values."""
import copy
import math

import pytest

import common
import plan_walls
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build

RECTANGLE = [(0.0, 0.0), (12.0, 0.0), (12.0, 25.0), (0.0, 25.0)]
# The back edge 1 degree off, the corners 89 and 91 degrees
OFF_ONE = [(0.0, 0.0), (12.0, 0.0), (12.0, 25.0), (0.0, 25.0 + 12.0 * math.tan(math.radians(1.0)))]
OFF_THREE = [(0.0, 0.0), (12.0, 0.0), (12.0, 25.0), (0.0, 25.0 + 12.0 * math.tan(math.radians(3.0)))]
# A corner plot cut by a chamfer of 3 m at 45 degrees
CHAMFER = [(0.0, 0.0), (12.0, 0.0), (12.0, 22.0), (9.0, 25.0), (0.0, 25.0)]
# A plot notched 4 m by 5 m at its back right corner
NOTCH = [(0.0, 0.0), (12.0, 0.0), (12.0, 20.0), (8.0, 20.0), (8.0, 25.0), (0.0, 25.0)]
# A rectangle with a vertex on its side that runs on within half a degree and a repeated vertex, both merged
BENT = [(0.0, 0.0), (6.0, 0.02), (12.0, 0.0), (12.0, 25.0), (12.0, 25.0), (0.0, 25.0)]


def profile():
    return project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


def test_r415_a_rectangle_and_one_off_by_1_degree_are_regular_a_chamfer_and_a_notch_irregular():
    assert [plan_walls.irregular(ring, 2.0) for ring in (RECTANGLE, OFF_ONE, BENT, CHAMFER, NOTCH, OFF_THREE)] == [False, False, False, True, True, True]
    assert [len(plan_walls.corners(ring, 2.0)) for ring in (RECTANGLE, OFF_ONE, BENT, CHAMFER, NOTCH)] == [4, 4, 4, 5, 6]
    # The ring drawn the other way round gives the same
    assert [plan_walls.irregular(list(reversed(ring)), 2.0) for ring in (RECTANGLE, OFF_ONE, CHAMFER, NOTCH)] == [False, False, True, True]


def test_r415_the_tolerance_is_read_from_the_rules_profile_and_a_value_that_is_no_angle_refused():
    real = profile()
    assert real.get("rev6.irregular_tolerance_deg") == 2.0
    lines = plan_walls.line_rules(real)
    assert lines == {"tolerance": 2.0, "half": 0.1}
    assert plan_walls.irregular(OFF_THREE, lines["tolerance"]) is True
    values = copy.deepcopy(real.values)
    values["rev6"]["irregular_tolerance_deg"] = 4.0
    wider = plan_walls.line_rules(project_profile.ProjectProfile(real.path, values))
    assert wider["tolerance"] == 4.0 and plan_walls.irregular(OFF_THREE, wider["tolerance"]) is False
    for wrong in ("2", 0, 45, None):
        values["rev6"]["irregular_tolerance_deg"] = wrong
        with pytest.raises(project_profile.ProfileError) as error:
            plan_walls.line_rules(project_profile.ProjectProfile(real.path, values))
        assert str(error.value) == plan_walls.TOLERANCE_LINE


def test_r415_the_plots_of_the_scope_with_a_face_each_regular_or_irregular():
    rings = {"1": RECTANGLE, "2": CHAMFER, "9": NOTCH}
    assert plan_walls.irregular_of(["1", "2", "3"], rings, 2.0) == {"1": False, "2": True}


def wall(plot, edge, plots, ends):
    return {"plot": plot, "edge": edge, "plots": plots, "ends": ends}


def test_r416_the_wall_of_an_irregular_plot_runs_half_the_thickness_inside_it_a_regular_plot_keeps_the_centre_on_the_edge():
    # 1 the chamfered plot, 2 a rectangle beside it on x 12 to 24, its ring clockwise
    rings = {"1": CHAMFER, "2": [(12.0, 0.0), (12.0, 25.0), (24.0, 25.0), (24.0, 0.0)]}
    walls = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 22.0]]), wall("1", 2, ["1"], [[12.0, 22.0], [9.0, 25.0]]), wall("2", 2, ["2"], [[24.0, 25.0], [24.0, 0.0]])]
    plan_walls.inside(walls, rings, {"1", "2"}, {"1": True, "2": False}, 0.1)
    assert [item["lineInsideM"] for item in walls] == [0.1, 0.1, 0.0]
    assert walls[0]["ends"] == [[11.9, 0.0], [11.9, 22.0]]
    step = 0.1 / math.sqrt(2.0)
    assert [[common.r3(value, 6) for value in end] for end in walls[1]["ends"]] == [[common.r3(12.0 - step, 6), common.r3(22.0 - step, 6)], [common.r3(9.0 - step, 6), common.r3(25.0 - step, 6)]]
    assert walls[2]["ends"] == [[24.0, 25.0], [24.0, 0.0]]


def test_r416_a_shared_edge_takes_the_inside_of_the_one_irregular_plot_and_of_the_plot_it_is_filed_under_when_both_are():
    rings = {"1": RECTANGLE, "2": [(12.0, 0.0), (24.0, 0.0), (24.0, 20.0), (20.0, 20.0), (20.0, 25.0), (12.0, 25.0)]}
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    # 1 regular, 2 irregular: the wall filed under 1 moves into 2
    plan_walls.inside(shared, rings, {"1", "2"}, {"1": False, "2": True}, 0.1)
    assert (shared[0]["ends"], shared[0]["lineInsideM"]) == ([[12.1, 0.0], [12.1, 25.0]], 0.1)
    # Both irregular: written once as today, inside 1 it is filed under
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    plan_walls.inside(shared, rings, {"1", "2"}, {"1": True, "2": True}, 0.1)
    assert (shared[0]["ends"], shared[0]["lineInsideM"]) == ([[11.9, 0.0], [11.9, 25.0]], 0.1)
    # An irregular neighbour the build does not write pulls no wall
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    plan_walls.inside(shared, rings, {"1"}, {"1": False, "2": True}, 0.1)
    assert (shared[0]["ends"], shared[0]["lineInsideM"]) == ([[12.0, 0.0], [12.0, 25.0]], 0.0)


def irregular_four(monkeypatch):
    """Plot 4 of the made up row, its face x 36 to 48, taken as irregular, the others regular by their rectangles."""
    real = plan_walls.irregular
    monkeypatch.setattr(plan_walls, "irregular", lambda ring, tolerance: min(point[0] for point in ring) == 36.0 or real(ring, tolerance))


def test_r416_the_rev6_build_moves_the_walls_of_an_irregular_plot_inside_it_and_the_step_walls_gives_irregular_and_line_inside(monkeypatch):
    irregular_four(monkeypatch)
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    built = rev6_build(monkeypatch, basis_settings(mode="rev6"))
    before = {(item["plot"], item["edge"]): item["ends"]["value"] for item in past["trace"]["walls"]}
    after = {(item["plot"], item["edge"]): item["ends"]["value"] for item in built["trace"]["walls"]}
    assert list(before) == list(after)
    moved = {key: (before[key], after[key]) for key in before if before[key] != after[key]}
    # The wall 3 files on the edge it shares with 4 moves into 4, the walls of 4 into 4, the rest stays on the edge
    assert sorted(moved) == [("3", 2), ("4", 1), ("4", 2)]
    assert [[common.r3(value) for value in end] for end in after[("3", 2)]] == [[common.r3(value + 0.1 if index == 0 else value) for index, value in enumerate(end)] for end in before[("3", 2)]]
    assert [[common.r3(value) for value in end] for end in after[("4", 2)]] == [[common.r3(value - 0.1 if index == 0 else value) for index, value in enumerate(end)] for end in before[("4", 2)]]
    made, _ = plan_walls.step(built["scope"], built["rows"], built["trace"])
    assert {number: made[number]["irregular"] for number in built["scope"]} == {"1": False, "2": False, "3": False, "4": True, "5": False, "6": False, "7": None}
    assert {number: made[number]["lineInsideM"] for number in ("2", "3", "4", "7")} == {"2": [0.0, 0.0], "3": [0.0, 0.1], "4": [0.1, 0.1], "7": []}
    assert all(tuple(made[number]) == plan_walls.STEP_FIELDS for number in made)


def test_r416_under_the_past_job_the_walls_stay_and_both_new_fields_are_null(monkeypatch):
    irregular_four(monkeypatch)
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    made, _ = plan_walls.step(past["scope"], past["rows"], past["trace"])
    assert all(made[number]["irregular"] is None and made[number]["lineInsideM"] is None for number in made)
    assert past["trace"]["irregular"] is None and past["trace"]["wallLines"] is None
    # The plan writes no field of R416 on a wall
    assert all(set(item) == {"plot", "edge", "plots", "ends", "base", "top", "basePlot", "topPlot"} for item in past["trace"]["walls"])


def test_r416_the_parity_fields_of_the_step_walls_hold_irregular_and_line_inside():
    fields = project_profile.load_settings().get("addin")["parity"]["steps"]["walls"]
    assert tuple(fields) == plan_walls.STEP_FIELDS
    assert (fields["irregular"], fields["lineInsideM"]) == ({"kind": "noGeometry"}, {"kind": "levels", "decimals": 3})
