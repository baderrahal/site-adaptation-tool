"""R480. The side edges of a written unit keyRules.wallSideOpen holds leave a span open by the hand of the plot, on faces made up in code (R10). R480_C10SideTests.cs
of the C# holds the same faces and the same values, so the two sides give one wall list."""
import pytest

import plan_walls
import project_profile

SIDES = {"cos": 0.5, "byUnit": {"U1": {"left": (1.8, 7.21), "right": (5.5, 22.025)}}}
RULES = {"higherBase": True, "higherTop": True, "depth": 0.1, "rise": 1.7, "near": 0.3, "sides": SIDES}
# Seen from its front on y = 0 the plot looks north, so the left side is the west edge and the right side the east edge
RING = [(0.0, 0.0), (16.0, 0.0), (16.0, 25.0), (0.0, 25.0)]
FRAME = ((0.0, 0.0), (16.0, 0.0))


def built(rings, frames, kin, mirrored, rules=RULES, written=None):
    written = set(frames) if written is None else written
    ffls = {plot: 100.0 for plot in written}
    return plan_walls.walls(sorted(rings), written, rings, frames, ffls, rules, frames, kin, mirrored)


def ends(walls, edge, plot="1"):
    return [[tuple(round(value, 6) for value in point) for point in wall["ends"]] for wall in walls if wall["plot"] == plot and wall["edge"] == edge]


def shared(walls, plots):
    return [[tuple(round(value, 6) for value in point) for point in wall["ends"]] for wall in walls if wall["plots"] == plots]


def alone(mirrored=False, ring=RING, frame=FRAME, unit="U1"):
    return built({"1": ring}, {"1": frame}, {"1": (None, unit)}, {"1": mirrored})


def test_r480_a_plot_not_mirrored_leaves_the_left_span_on_its_left_and_the_right_span_on_its_right():
    found = alone()
    assert ends(found, 3) == [[(0.0, 25.0), (0.0, 7.21)], [(0.0, 1.8), (0.0, 0.0)]]
    assert ends(found, 1) == [[(16.0, 0.0), (16.0, 5.5)], [(16.0, 22.025), (16.0, 25.0)]]


def test_r480_a_mirrored_plot_swaps_the_two_spans():
    found = alone(True)
    assert ends(found, 3) == [[(0.0, 25.0), (0.0, 22.025)], [(0.0, 5.5), (0.0, 0.0)]]
    assert ends(found, 1) == [[(16.0, 0.0), (16.0, 1.8)], [(16.0, 7.21), (16.0, 25.0)]]


def test_r480_the_span_is_measured_from_the_end_nearer_the_middle_of_the_front_whichever_way_the_front_runs():
    turned = [(16.0, 25.0), (0.0, 25.0), (0.0, 0.0), (16.0, 0.0)]
    # The front on y = 25 looks south, so the left side is the east edge, the first edge of this ring runs along the front
    found = alone(False, turned, ((16.0, 25.0), (0.0, 25.0)))
    assert ends(found, 3) == [[(16.0, 0.0), (16.0, 17.79)], [(16.0, 23.2), (16.0, 25.0)]]
    assert ends(found, 1) == [[(0.0, 25.0), (0.0, 19.5)], [(0.0, 2.975), (0.0, 0.0)]]


def test_r480_the_back_edge_stays_whole_and_an_edge_of_28_m_keeps_the_span_from_the_front_end():
    long = [(0.0, 0.0), (16.0, 0.0), (16.0, 28.0), (0.0, 28.0)]
    found = alone(False, long)
    assert ends(found, 2) == [[(16.0, 28.0), (0.0, 28.0)]]
    assert ends(found, 3) == [[(0.0, 28.0), (0.0, 7.21)], [(0.0, 1.8), (0.0, 0.0)]]
    assert ends(found, 1) == [[(16.0, 0.0), (16.0, 5.5)], [(16.0, 22.025), (16.0, 28.0)]]


def test_r480_an_edge_shorter_than_the_end_of_its_span_stays_whole():
    short = [(0.0, 0.0), (16.0, 0.0), (16.0, 20.0), (0.0, 20.0)]
    found = alone(False, short)
    assert ends(found, 1) == [[(16.0, 0.0), (16.0, 20.0)]]
    assert ends(found, 3) == [[(0.0, 20.0), (0.0, 7.21)], [(0.0, 1.8), (0.0, 0.0)]]


def test_r480_a_unit_the_table_lacks_a_plot_with_no_hand_and_a_build_with_no_table_are_walled_whole():
    whole = {1: [[(16.0, 0.0), (16.0, 25.0)]], 3: [[(0.0, 25.0), (0.0, 0.0)]]}
    other = alone(unit="U2")
    assert {edge: ends(other, edge) for edge in whole} == whole
    no_hand = built({"1": RING}, {"1": FRAME}, {"1": (None, "U1")}, {})
    assert {edge: ends(no_hand, edge) for edge in whole} == whole
    no_table = built({"1": RING}, {"1": FRAME}, {"1": (None, "U1")}, {"1": False}, dict(RULES, sides=None))
    assert {edge: ends(no_table, edge) for edge in whole} == whole
    # A build that gives no hands at all, as the callers before R480
    assert plan_walls.walls(["1"], {"1"}, {"1": RING}, {"1": FRAME}, {"1": 100.0}, RULES) == no_table


# Plot 0 of another unit lies east of the plot and files the shared edge, as the first of the two in the order, its own edge from y = -3 to y = 30
NEXT = [(16.0, -3.0), (30.0, -3.0), (30.0, 30.0), (16.0, 30.0)]
PAIR = {"1": RING, "0": NEXT}
PAIR_FRAMES = {"1": FRAME, "0": ((16.0, -3.0), (30.0, -3.0))}


def test_r480_a_side_edge_another_plot_files_leaves_the_span_of_the_unit_from_the_front_of_the_unit():
    found = built(PAIR, PAIR_FRAMES, {"1": (None, "U1"), "0": (None, "U2")}, {"1": False, "0": False})
    assert shared(found, ["0", "1"]) == [[(16.0, 25.0), (16.0, 22.025)], [(16.0, 5.5), (16.0, 0.0)]]
    # The rest of the long edge of 0 past the unit has no span
    assert ends(found, 3, "0")[0] == [(16.0, 30.0), (16.0, 25.0)]
    assert ends(found, 3, "0")[-1] == [(16.0, 0.0), (16.0, -3.0)]


def test_r480_an_edge_the_written_unit_does_not_write_is_left_by_its_neighbour_whole_when_the_unit_is_not_written():
    found = built(PAIR, {"0": PAIR_FRAMES["0"]}, {"1": (None, "U1"), "0": (None, "U2")}, {"0": False}, written={"0"})
    assert shared(found, ["0", "1"]) == [[(16.0, 25.0), (16.0, 0.0)]]


def test_r480_an_edge_of_a_pair_the_wallopen_table_holds_keeps_that_rule():
    kin = {"1": (0, "U1"), "0": (0, "U2")}
    rules = dict(RULES, open={"U1|U2": (10.0, 12.0)})
    found = built(PAIR, PAIR_FRAMES, kin, {"1": False, "0": False}, rules)
    # The span of Q115 runs from the front end of the edge of the plot that files it, y = -3 on this edge, and its parts run on from there
    assert shared(found, ["0", "1"]) == [[(16.0, 25.0), (16.0, 9.0)], [(16.0, 7.0), (16.0, 0.0)]]


def test_r480_two_units_on_one_edge_leave_open_both_their_spans_as_one():
    west = [(-16.0, 0.0), (0.0, 0.0), (0.0, 25.0), (-16.0, 25.0)]
    rings = {"1": RING, "2": west}
    frames = {"1": FRAME, "2": ((-16.0, 0.0), (0.0, 0.0))}
    # 1 not mirrored takes the left span on the edge and 2 not mirrored the right span
    found = built(rings, frames, {"1": (None, "U1"), "2": (None, "U1")}, {"1": False, "2": False})
    assert shared(found, ["1", "2"]) == [[(0.0, 25.0), (0.0, 22.025)], [(0.0, 1.8), (0.0, 0.0)]]


# The made up row of test_override_param_steps.py, whose plot 1 is the one VD2, its face x 0 to 12 and y 0 to 25, its front of R355 on y = 0 along the road. The table takes the
# VD2 so plot 1 alone holds the spans. R480_C10SideTests.cs holds the same row and the same values
ROW_SIDES = {"rule": "sideSpanByHand", "sideCosMax": 0.5, "byUnit": {"VD2": {"left": [1.8, 7.21], "right": [5.5, 22.025]}}}


def row(monkeypatch, overrides):
    from test_override_param_steps import row_build
    return row_build(monkeypatch, overrides, change=lambda key: key.update(wallSideOpen=ROW_SIDES))


def along(built, x):
    """The y of the two ends of each wall of plot 1 on the line x of the row, lowest first."""
    return sorted(sorted(round(end[1], 6) for end in item["ends"]["value"]) for item in built["trace"]["walls"] if item["plot"] == "1" and all(end[0] == x for end in item["ends"]["value"]))


def test_r480_a_mirrored_override_swaps_the_spans_through_the_build(monkeypatch):
    by_rule = row(monkeypatch, {})["plan"]["1"]["mirrored"]["value"]
    plain, turned = row(monkeypatch, {"1": {"mirrored": False}}), row(monkeypatch, {"1": {"mirrored": True}})
    assert (plain["plan"]["1"]["mirrored"]["value"], turned["plan"]["1"]["mirrored"]["value"]) == (False, True)
    # One of the two moves the flag off its rule, so the span follows the flag after the overrides and not the hand of the rule
    assert "mirrored" in (plain if by_rule else turned)["plan"]["1"]["overrides"]["applied"]
    # Seen from y = 0 into the plot the left side is x = 0
    assert (along(plain, 0.0), along(plain, 12.0)) == ([[0.0, 1.8], [7.21, 25.0]], [[0.0, 5.5], [22.025, 25.0]])
    assert (along(turned, 0.0), along(turned, 12.0)) == ([[0.0, 5.5], [22.025, 25.0]], [[0.0, 1.8], [7.21, 25.0]])


def test_r480_a_plot_a_rotation_override_turned_takes_its_spans_from_the_edge_it_opens_and_not_from_the_front_of_r355(monkeypatch):
    built = row(monkeypatch, {"1": {"mirrored": False, "rotationDeg": {"gapToRuleDeg": -179.999}}})
    assert "rotationDeg" in built["plan"]["1"]["overrides"]["applied"]
    walls = [item for item in built["trace"]["walls"] if item["plot"] == "1"]
    # The turn opens the back edge on y = 25 and walls the front of R355 on y = 0
    assert not any(all(end[1] == 25.0 for end in item["ends"]["value"]) for item in walls)
    assert any(all(end[1] == 0.0 for end in item["ends"]["value"]) for item in walls)
    # Seen from y = 25 into the plot the left side is x = 12, each span measured down from y = 25
    assert along(built, 12.0) == [[0.0, 17.79], [23.2, 25.0]]
    assert along(built, 0.0) == [[0.0, 2.975], [19.5, 25.0]]


def test_r480_keyrules_wallsideopen_is_read_at_the_strong_share_and_a_table_that_is_no_span_table_is_refused():
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    level = profile.get("levelProfiles.parsons2024Rev6")
    key = level["keyRules"]
    rules = plan_walls.wall_rules(key, level, "parsons2024Rev6", profile, 0.3)
    assert rules["sides"] == {"cos": 0.5, "byUnit": {"C10": {"left": (1.8, 7.21), "right": (5.5, 22.025)}}}
    assert key["wallSideOpen"]["strong"] is True and key["wallSideOpen"]["share"] >= 0.9
    span = key["wallSideOpen"]["byUnit"]["C10"]
    for bad in ({"rule": "other", "sideCosMax": 0.5, "byUnit": {"C10": span}}, {"rule": "sideSpanByHand", "byUnit": {"C10": span}},
                {"rule": "sideSpanByHand", "sideCosMax": 0.5, "byUnit": {"C10": {"left": [7.0, 2.0], "right": [5.5, 22.0]}}},
                {"rule": "sideSpanByHand", "sideCosMax": 0.5, "byUnit": {"C10": {"left": [1.8, 7.2]}}}, {"rule": "sideSpanByHand", "sideCosMax": 0.5}):
        with pytest.raises(project_profile.ProfileError) as error:
            plan_walls.wall_rules(dict(key, wallSideOpen=bad), level, "parsons2024Rev6", profile, 0.3)
        assert str(error.value) == ("keyRules.wallSideOpen of parsons2024Rev6 holds no span table a build leaves a side edge open by, it takes sideSpanByHand with sideCosMax "
                                    "and for each unit a left and a right span of two numbers, the first under the second (R480)")
    bare = {name: rule for name, rule in key.items() if name != "wallSideOpen"}
    assert plan_walls.wall_rules(bare, level, "parsons2024Rev6", profile, 0.3)["sides"] is None
