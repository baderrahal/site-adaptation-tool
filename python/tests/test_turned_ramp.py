"""Q116, R355, R362. The ramp of a plot a rotationDeg override turned goes on the open edge the walls take, on made up faces built in code
(R10), which TurnedRampTests.cs holds the same."""
import copy

import pytest
from shapely.geometry import Polygon

import plan_from_book
from test_level_floor_steps import made_build
from test_override_param_steps import MADE, steps_of

FACE = Polygon([(12.0, 0.0), (24.0, 0.0), (24.0, 25.0), (12.0, 25.0)])
FRAME = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (12.0, 0.0), "right": (24.0, 0.0), "length": 12.0, "distance": 3.0, "tied": False}


def road(surfaces, point, snap):
    return (10.0 + point[0] / 100.0, "road") if point[1] < 20.0 else (None, None)


def everywhere(surfaces, point, snap):
    return 10.0 + point[0] / 100.0, "road"


def test_q116_r355_open_ramp_builds_the_frame_ramp_and_road_levels_on_the_open_edge_a_turn_faces(monkeypatch):
    monkeypatch.setattr(plan_from_book, "road_level", everywhere)
    turned = plan_from_book.open_ramp(FACE, FRAME, 180.001, False, 5.52, [("road", None)], 0.5)
    frame = turned["frame"]
    # The back edge seen from outside: n into the plot, its left end the one at x 24, as frame_for gives any edge
    assert (frame["left"], frame["right"], frame["n"], frame["length"]) == ((24.0, 25.0), (12.0, 25.0), (0.0, -1.0), 12.0)
    assert [tuple(corner) for corner in turned["corners"]] == pytest.approx([(24.0, 25.0), (12.0, 25.0), (12.0, 19.48), (24.0, 19.48)])
    assert turned["front"] == pytest.approx([10.24, 10.12]) and turned["surfaces"] == ["road", "road"]
    # The rule rotation, or a small turn, keeps the front of R355 open, so the ramp stays where it is
    assert plan_from_book.open_ramp(FACE, FRAME, 0.0, False, 5.52, [], 0.5) is None
    assert plan_from_book.open_ramp(FACE, FRAME, 1.5, False, 5.52, [], 0.5) is None
    # Mirrored the rule rotation is 180, a turn to 0 faces the back
    assert plan_from_book.open_ramp(FACE, FRAME, 180.0, True, 5.52, [], 0.5) is None
    assert plan_from_book.open_ramp(FACE, FRAME, 0.0, True, 5.52, [("road", None)], 0.5)["frame"]["left"] == (24.0, 25.0)
    # No frame, depth or polygon face gives nothing
    assert plan_from_book.open_ramp(None, FRAME, 180.0, False, 5.52, [], 0.5) is None
    assert plan_from_book.open_ramp(FACE, None, 180.0, False, 5.52, [], 0.5) is None
    assert plan_from_book.open_ramp(FACE, FRAME, 180.0, False, None, [], 0.5) is None


def test_q116_r355_open_ramp_with_no_road_level_at_an_open_corner_gives_no_front(monkeypatch):
    monkeypatch.setattr(plan_from_book, "road_level", road)
    turned = plan_from_book.open_ramp(FACE, FRAME, 180.0, False, 5.52, [("road", None)], 0.5)
    assert turned["front"] is None and turned["surfaces"] == [None, None] and turned["corners"][0] == (24.0, 25.0)


def test_q116_r362_a_turned_plot_takes_its_ramp_on_the_open_edge_and_leaves_the_conflicts(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE))
    plan = built["plan"]["2"]
    corners = plan["floors"]["ramp"]["corners"]["value"]
    # 2 turned by about 180 degrees: its ramp lies on the back edge its walls leave open, 5.52 m deep for DP2
    assert [corner[:2] for corner in corners] == [[24.0, 25.0], [12.0, 25.0], [12.0, 19.48], [24.0, 19.48]]
    # The front pair at the road of the back corners, the back pair parallel with its mean at El @ Entry as the plan computes it
    entry = plan["levels"]["entry"]["value"]
    assert [corner[2] for corner in corners[:2]] == [10.24, 10.12] and (corners[2][2] + corners[3][2]) / 2.0 == pytest.approx(entry, abs=0.001)
    assert corners[3][2] - corners[2][2] == pytest.approx(0.12, abs=0.001)
    assert built["conflicts"] == [] and built["trace"]["conflicts"] == []
    # 6 turned by 1.5 degrees keeps its ramp on the front, and the override step holds each outline after the overrides
    made, _ = steps_of(built)
    assert [[round(value, 3) for value in corner] for corner in made["2"]["rampOutline"]] == [corner[:2] for corner in corners]
    assert made["6"]["rampOutline"] == built["floors"]["6"]["rampOutline"]
    assert made["7"]["rampOutline"] is None


def test_q116_r362_a_turned_plot_with_no_road_level_on_its_open_edge_keeps_its_ramp_on_the_front_and_stays_a_conflict(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE), back_dry=True)
    corners = built["plan"]["2"]["floors"]["ramp"]["corners"]["value"]
    assert [corner[:2] for corner in corners] == [[12.0, 0.0], [24.0, 0.0], [24.0, 5.52], [12.0, 5.52]]
    assert [item["plot"] for item in built["conflicts"]] == ["2"]
    made, _ = steps_of(built)
    assert made["2"]["rampOutline"] == built["floors"]["2"]["rampOutline"]


def test_q116_r401_the_ramp_a_turn_moved_carries_the_status_of_the_override_and_a_ramp_it_left_does_not(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE))
    details = built["trace"]["statusFrom"]
    outline = {plot: built["plan"][plot]["floors"]["ramp"]["outline"] for plot in ("2", "6")}
    # 2 moved by its turn names rotationDeg, 6 turned by 1.5 degrees kept its ramp on the front and names no override for its outline
    assert "rotationDeg" in outline["2"]["statusFrom"] and "rotationDeg" not in outline["6"]["statusFrom"]
    assert all("rotationDeg" in built["plan"]["2"]["floors"]["ramp"][field]["statusFrom"] for field in ("corners", "heightOffset"))
    assert any(entry.get("question") == "Q053" for entry in details[outline["2"]["statusFrom"]]["entries"])
    # With no road level on the open edge the ramp stays and names no turn
    dry = made_build(monkeypatch, copy.deepcopy(MADE), back_dry=True)
    assert all("rotationDeg" not in dry["plan"]["2"]["floors"]["ramp"][field]["statusFrom"] for field in ("outline", "corners", "heightOffset"))


def test_q116_r362_a_turned_plot_leaves_its_ramp_front_gap_measured_on_the_front_of_r355(monkeypatch):
    # The rampFrontM gap holds the road of the front of R355 less the key ramp front, so on the open edge it would count the road gap twice
    overrides = {"2": dict(copy.deepcopy(MADE["2"]), rampFrontM={"gapToRuleM": 0.05})}
    built = made_build(monkeypatch, overrides)
    plan = built["plan"]["2"]
    corners = plan["floors"]["ramp"]["corners"]["value"]
    assert [corner[2] for corner in corners[:2]] == [10.24, 10.12]
    why = "measured on the front of R355, the ramp moved to the open edge (Q116)"
    made, _ = steps_of(built)
    assert "rampFrontM" not in made["2"]["applied"] and made["2"]["notApplied"]["rampFrontM"] == why
    assert [round(z, 3) for z in made["2"]["rampZ"][:2]] == [10.24, 10.12]
    assert all("rampFrontM" not in plan["floors"]["ramp"][field]["statusFrom"] for field in ("corners", "heightOffset"))
