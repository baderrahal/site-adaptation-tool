"""R415 and R416, round 4j-2. The irregular plot by the tolerance of the rules profile and the boundary wall of an irregular plot with its outer
face on the plot line under the Rev 6 mode (Manual Rev 06 2.8.A), on faces made up in code (R10). R415_IrregularWallTests.cs of the C# holds
the same faces and values."""
import copy
import json
import math

import pytest

import common
import plan_walls
import project_profile
import value_status
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


# keyPlans.wallEdgeM of a made up build, within which two ends of walls of two faces are one point
NEAR = 0.05


def rounded(item):
    return [[common.r3(value, 6) for value in end] for end in item["ends"]]


def test_r416_the_wall_of_an_irregular_plot_runs_half_the_thickness_inside_it_a_regular_plot_keeps_the_centre_on_the_edge():
    # 1 the chamfered plot, 2 a rectangle beside it on x 12 to 24, its ring clockwise
    rings = {"1": CHAMFER, "2": [(12.0, 0.0), (12.0, 25.0), (24.0, 25.0), (24.0, 0.0)]}
    walls = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 22.0]]), wall("1", 2, ["1"], [[12.0, 22.0], [9.0, 25.0]]), wall("2", 2, ["2"], [[24.0, 25.0], [24.0, 0.0]])]
    plan_walls.inside(walls, rings, {"1", "2"}, {"1": True, "2": False}, 0.1, 2.0, NEAR)
    assert [item["lineInsideM"] for item in walls] == [0.1, 0.1, 0.0]
    # The two moved walls meet at the vertex 12, 22 again, at the mitre of their lines. The end at 9, 25 has no wall on the next edge, it stays square
    step = 0.1 / math.sqrt(2.0)
    mitre = 34.0 - 2.0 * step - 11.9
    assert rounded(walls[0]) == [[11.9, 0.0], [11.9, common.r3(mitre, 6)]]
    assert rounded(walls[1]) == [[11.9, common.r3(mitre, 6)], [common.r3(9.0 - step, 6), common.r3(25.0 - step, 6)]]
    assert walls[2]["ends"] == [[24.0, 25.0], [24.0, 0.0]]


def test_r416_a_shared_edge_takes_the_inside_of_the_one_irregular_plot_and_of_the_plot_it_is_filed_under_when_both_are():
    rings = {"1": RECTANGLE, "2": [(12.0, 0.0), (24.0, 0.0), (24.0, 20.0), (20.0, 20.0), (20.0, 25.0), (12.0, 25.0)]}
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    # 1 regular, 2 irregular: the wall filed under 1 moves into 2
    plan_walls.inside(shared, rings, {"1", "2"}, {"1": False, "2": True}, 0.1, 2.0, NEAR)
    assert (shared[0]["ends"], shared[0]["lineInsideM"]) == ([[12.1, 0.0], [12.1, 25.0]], 0.1)
    # Both irregular: written once as today, inside 1 it is filed under
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    plan_walls.inside(shared, rings, {"1", "2"}, {"1": True, "2": True}, 0.1, 2.0, NEAR)
    assert (shared[0]["ends"], shared[0]["lineInsideM"]) == ([[11.9, 0.0], [11.9, 25.0]], 0.1)
    # An irregular neighbour the build does not write pulls no wall
    shared = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 25.0]])]
    plan_walls.inside(shared, rings, {"1"}, {"1": False, "2": True}, 0.1, 2.0, NEAR)
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
    # The back corners of 4 close at the mitre of the moved lines, the front ends next to the open front stay square
    points = {key: sorted((common.r3(end[0]), common.r3(end[1])) for end in after[key]) for key in moved}
    assert points == {("3", 2): [(36.1, 0.0), (36.1, 24.9)], ("4", 1): [(36.1, 24.9), (47.9, 24.9)], ("4", 2): [(47.9, 0.0), (47.9, 24.9)]}
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
    # The plan writes no field of R416 on a wall, each end keeps the source keyRules, and no plot holds a split test of R414
    assert all(set(item) == {"plot", "edge", "plots", "ends", "base", "top", "basePlot", "topPlot"} for item in past["trace"]["walls"])
    assert {item["ends"]["source"] for item in past["trace"]["walls"]} == {"keyRules"}
    assert all(past["levels"][plot]["splitTest"] is None for plot in past["levels"]) and len(past["levels"]) == len(past["scope"])
    assert all(made[number]["irregular"] is None for number in past["scope"]) and len(made) == len(past["scope"])


def test_r416_the_parity_fields_of_the_step_walls_hold_irregular_and_line_inside():
    fields = project_profile.load_settings().get("addin")["parity"]["steps"]["walls"]
    assert tuple(fields) == plan_walls.STEP_FIELDS
    assert (fields["irregular"], fields["lineInsideM"]) == ({"kind": "noGeometry"}, {"kind": "levels", "decimals": 3})


def test_r416_an_l_shaped_clockwise_plot_with_a_wall_on_each_edge_closes_at_every_corner_reflex_and_convex():
    # The L of 10 by 10 less its 4 by 6 back right part, drawn clockwise, its reflex corner at 6, 4
    ring = [(0.0, 0.0), (0.0, 10.0), (6.0, 10.0), (6.0, 4.0), (10.0, 4.0), (10.0, 0.0)]
    walls = [wall("1", index, ["1"], [list(ring[index]), list(ring[(index + 1) % len(ring)])]) for index in range(len(ring))]
    plan_walls.inside(walls, {"1": ring}, {"1"}, {"1": True}, 0.1, 2.0, NEAR)
    inner = [[0.1, 0.1], [0.1, 9.9], [5.9, 9.9], [5.9, 3.9], [9.9, 3.9], [9.9, 0.1]]
    assert [rounded(item) for item in walls] == [[inner[index], inner[(index + 1) % len(inner)]] for index in range(len(inner))]
    # Each corner, the reflex one too, holds one end of two walls, so no square is left unbuilt and none built twice
    assert all(walls[index]["ends"][1] == walls[(index + 1) % len(walls)]["ends"][0] for index in range(len(walls)))
    assert [item["lineInsideM"] for item in walls] == [0.1] * 6


def test_r416_a_chamfered_plot_closes_at_its_chamfer_and_keeps_square_ends_next_to_its_open_front():
    # CHAMFER anticlockwise, its front edge 0 open, a wall on each other edge
    walls = [wall("1", index, ["1"], [list(CHAMFER[index]), list(CHAMFER[(index + 1) % len(CHAMFER)])]) for index in range(1, len(CHAMFER))]
    plan_walls.inside(walls, {"1": CHAMFER}, {"1"}, {"1": True}, 0.1, 2.0, NEAR)
    # The chamfer line x + y = 34 moved 0.1 inside is x + y = 34 less 0.1 times the root of 2
    line = 34.0 - 0.1 * math.sqrt(2.0)
    assert [rounded(item) for item in walls] == [[[11.9, 0.0], [11.9, common.r3(line - 11.9, 6)]], [[11.9, common.r3(line - 11.9, 6)], [common.r3(line - 24.9, 6), 24.9]],
                                                 [[common.r3(line - 24.9, 6), 24.9], [0.1, 24.9]], [[0.1, 24.9], [0.1, 0.0]]]


def test_r416_an_end_mid_edge_a_collinear_vertex_and_a_wall_that_meets_mid_edge_keep_square_ends():
    # 1 a 12 by 25 plot with a vertex on its back edge that runs on, its side x 12 parted at y 10 where 2 and 3 beside it meet. All three taken
    # irregular, the wall of 2 and 3 filed under 2 meets the side of 1 mid edge
    rings = {"1": [(0.0, 0.0), (12.0, 0.0), (12.0, 25.0), (6.0, 25.0), (0.0, 25.0)], "2": [(12.0, 0.0), (24.0, 0.0), (24.0, 10.0), (12.0, 10.0)],
             "3": [(12.0, 10.0), (24.0, 10.0), (24.0, 25.0), (12.0, 25.0)]}
    walls = [wall("1", 1, ["1", "2"], [[12.0, 0.0], [12.0, 10.0]]), wall("1", 1, ["1", "3"], [[12.0, 10.0], [12.0, 25.0]]), wall("1", 2, ["1"], [[12.0, 25.0], [6.0, 25.0]]),
             wall("1", 3, ["1"], [[6.0, 25.0], [0.0, 25.0]]), wall("1", 4, ["1"], [[0.0, 25.0], [0.0, 0.0]]), wall("2", 2, ["2", "3"], [[24.0, 10.0], [12.0, 10.0]])]
    plan_walls.inside(walls, rings, {"1", "2", "3"}, {"1": True, "2": True, "3": True}, 0.1, 2.0, NEAR)
    assert [rounded(item) for item in walls] == [[[11.9, 0.0], [11.9, 10.0]], [[11.9, 10.0], [11.9, 24.9]], [[11.9, 24.9], [6.0, 24.9]], [[6.0, 24.9], [0.1, 24.9]],
                                                 [[0.1, 24.9], [0.1, 0.0]], [[24.0, 9.9], [12.0, 9.9]]]


def status_wall(line):
    found = {"plot": "1", "edge": 1, "plots": ["1"], "ends": [[11.9, 0.0], [11.9, 25.0]], "base": 9.5, "top": 12.4, "basePlot": "1", "topPlot": "1"}
    if line is not None:
        found["lineInsideM"] = line
    return found


def test_r416_a_moved_wall_names_r416_and_the_manual_in_its_status_an_unmoved_wall_keeps_the_status_of_today():
    table = value_status.table_of(project_profile.load_settings(), profile(), "parsons2024Rev6")
    marked = {}
    for name, line in (("past", None), ("regular", 0.0), ("moved", 0.1)):
        made = status_wall(line)
        item = plan_walls.entry(made)
        plan_walls.mark(item, made, table, {})
        marked[name] = item
    assert marked["past"] == marked["regular"]
    assert (marked["regular"]["ends"]["source"], marked["regular"]["ends"]["statusFrom"]) == ("keyRules", "walls.ends from keyRules")
    assert (marked["moved"]["ends"]["source"], marked["moved"]["ends"]["status"], marked["moved"]["ends"]["statusFrom"]) == ("manualInside", "UNKNOWN", "walls.ends from manualInside")
    assert all(marked["moved"][field] == marked["regular"][field] for field in ("plots", "base", "top"))
    entries = table.used["walls.ends from manualInside"]["entries"]
    assert "R416" in entries[0]["document"] and "Rev 06" in entries[0]["document"] and entries[0]["pageOrSheet"] == "2.8.A"
    named = [entry.get("entry", "").split(" ", 1)[-1] for entry in entries]
    assert "rev6.irregular_tolerance_deg" in named and "plot.boundary_wall_thickness" in named
    assert not any("wallLine" in name for name in named)
    assert any("wallLine" in entry.get("entry", "") for entry in table.used["walls.ends from keyRules"]["entries"])


def test_r416_the_rev6_build_gives_the_moved_ends_the_status_of_r416_and_the_other_walls_those_of_the_past_job(monkeypatch):
    irregular_four(monkeypatch)
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    built = rev6_build(monkeypatch, basis_settings(mode="rev6"))
    moved = [(item["plot"], item["edge"]) for item in built["trace"]["walls"] if item["ends"]["source"] == "manualInside"]
    assert moved == [("3", 2), ("4", 1), ("4", 2)]
    # Byte for byte the wall of the past job on every wall that did not move
    assert all(json.dumps(after, sort_keys=True) == json.dumps(before, sort_keys=True) for before, after in zip(past["trace"]["walls"], built["trace"]["walls"])
               if (after["plot"], after["edge"]) not in moved)
    _, tables = plan_walls.step(built["scope"], built["rows"], built["trace"])
    assert "walls.ends from manualInside" in tables["statusFrom"] and "walls.ends from keyRules" in tables["statusFrom"]
