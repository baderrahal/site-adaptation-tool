"""R344, Q073: a front corner takes a road level only from a road surface that covers it within snapM (R157). The Roads folder, read in
full, holds no other road level at a corner no road surface covers, so such a plot gets no level and is listed with the sources tried
and why each failed, never the road of the nearest surface sideways and never a fixed rise. The build, the data book and the own route
of the second method decide a corner by the one rule and the one snap. Every surface, face and road here is made up (R10)."""
import inspect
import math

import pytest

import c3d_connect
import data_book
import key_rules_check
import majority
import plan_from_book
import plan_rules
import project_profile
import surface_lookup

SETTINGS = project_profile.load_settings()


def square(x0, size=10.0, slope=0.01):
    """A made up road square from x0 along x, its level rising slope per metre along x."""
    xyz = [(x0, 0.0, x0 * slope), (x0 + size, 0.0, (x0 + size) * slope), (x0 + size, size, (x0 + size) * slope), (x0, size, x0 * slope)]
    return surface_lookup.Surface(xyz, [(0, 1, 2), (0, 2, 3)])


def made_up_landxml(path, x0=0.0):
    """The square of square() as LandXML text, northing then easting as the export writes it."""
    points = [(x0, 0.0, x0 * 0.01), (x0 + 10.0, 0.0, (x0 + 10.0) * 0.01), (x0 + 10.0, 10.0, (x0 + 10.0) * 0.01), (x0, 10.0, x0 * 0.01)]
    text = "".join('<P id="%d">%r %r %r</P>\n' % (index + 1, y, x, z) for index, (x, y, z) in enumerate(points))
    path.write_text('<?xml version="1.0"?>\n<LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2"><Surfaces><Surface name="Made Up">'
                    '<Definition surfType="TIN"><Pnts>\n' + text + "</Pnts><Faces>\n<F>1 2 3</F>\n<F>1 3 4</F>\n</Faces></Definition></Surface></Surfaces></LandXML>\n",
                    encoding="utf-8")
    return path


def test_r344_a_corner_no_road_surface_covers_gets_no_level_never_the_nearest_road_sideways(tmp_path):
    roads = [("A", square(0.0))]
    # 2 m off the edge toward the plot, and beside the end of the road, no level at all
    assert plan_from_book.road_level(roads, (12.0, 5.0), 0.005) == (None, None)
    assert plan_from_book.road_level(roads, (5.0, 14.0), 0.005) == (None, None)
    # On the line the surface stops at, within snapM, the level of the surface (R157)
    z, name = plan_from_book.road_level(roads, (10.002, 5.0), 0.005)
    assert z == pytest.approx(0.1) and name == "A"
    with pytest.raises(TypeError):
        plan_from_book.road_level(roads, (12.0, 5.0), 0.005, {"reachM": 6.05, "crossFallPercent": 2.0})
    # No reach and no cross fall in config or code, in the build, the measure or the second method
    assert "roadReach" not in SETTINGS.get("siteBuild")
    for module, name in ((plan_rules, "road_reach"), (majority, "level_road_reach"), (key_rules_check, "own_reach"), (c3d_connect, "edge_nearest"),
                         (plan_from_book, "REACH_MARK")):
        assert not hasattr(module, name), name
    tin = key_rules_check.OwnTin(made_up_landxml(tmp_path / "a.xml"), None, 0.005)
    assert key_rules_check.own_road([("a", tin)], (12.0, 5.0), 0.005) is None
    assert key_rules_check.own_road([("a", tin)], (10.002, 5.0), 0.005) == pytest.approx(0.1)
    with pytest.raises(TypeError):
        key_rules_check.own_road([("a", tin)], (12.0, 5.0), 0.005, {"reachM": 6.05, "crossFallPercent": 2.0})


def test_r344_a_plot_with_no_road_level_is_listed_with_the_sources_tried_and_why_each_failed(tmp_path, monkeypatch):
    from test_addin_review import LATER_3A, key_build
    # Past 36 m no road surface covers a corner, so plot 4 has none at its right front corner, its left one at 36 m is covered
    road = lambda surfaces, point, snap: (None, None) if point[0] > 36.0 + 1e-6 else (10.0, "road")
    plan, _, _ = key_build(tmp_path, monkeypatch, road=road, later=LATER_3A)
    summary = plan["summary"]
    assert summary["plotsWithNoRoadLevel"] == ["4"]
    listed = summary["noRoadLevel"]
    assert [item["plot"] for item in listed] == ["4"]
    four = listed[0]
    assert four["corners"] == ["right"] and four["tried"] == ["road"]
    assert "no road surface covers" in four["reason"] and "0.005" in four["reason"]
    # The other sources of the Roads folder and why none gives a road level, from config, the same for every listed plot
    folder = SETTINGS.get("siteBuild")["roadFolder"]
    assert summary["roadFolderNotTaken"] == folder["notTaken"] and folder["note"]
    assert {"alignments", "roadsSubgrade", "groundAndGrading", "crossSections"} <= set(folder["notTaken"])
    lines = "\n".join(plan_from_book.summary_lines(summary, "parsons2024Rev6", False))
    assert "plot 4: no road level at the front corners right, tried road" in lines and "groundAndGrading" in lines


def test_r344_the_snap_the_road_level_reads_is_refused_unless_a_finite_number_in_its_range():
    block = SETTINGS.get("siteBuild")
    assert plan_rules.road_snap(block) == block["snapM"] and 0.0 <= block["snapM"] <= block["snapMaxM"]
    for bad in (float("nan"), float("inf"), -0.001, True, "0.005", None, block["snapMaxM"] + 0.001):
        with pytest.raises(project_profile.ProfileError, match="R344"):
            plan_rules.road_snap(dict(block, snapM=bad))
    for bad in (float("nan"), -1.0, False, None):
        with pytest.raises(project_profile.ProfileError, match="R344"):
            plan_rules.road_snap(dict(block, snapMaxM=bad))
    # The limit itself is taken, the own route of the second method refuses the same values by its own reading
    assert plan_rules.road_snap(dict(block, snapM=block["snapMaxM"])) == block["snapMaxM"]
    own = key_rules_check.read_json(project_profile.SETTINGS_PATH)
    assert key_rules_check.own_snap(own) == block["snapM"]
    for bad in (float("nan"), float("inf"), -0.001, True, block["snapMaxM"] + 0.001):
        changed = dict(own, siteBuild=dict(own["siteBuild"], snapM=bad))
        with pytest.raises(ValueError, match="R344"):
            key_rules_check.own_snap(changed)
    assert key_rules_check.own_snap(dict(own, siteBuild=dict(own["siteBuild"], snapM=block["snapMaxM"]))) == block["snapMaxM"]


def test_r344_a_corner_exactly_at_the_snap_is_decided_the_same_by_the_build_the_data_book_and_the_own_route(tmp_path):
    surface = square(0.0)
    tin = key_rules_check.OwnTin(made_up_landxml(tmp_path / "a.xml"), None, 0.25)
    # 0.25 and 0.5 are exact in binary, so the corner lies exactly at the snap, the other just past it
    at, past = (10.25, 5.0), (10.25 + 1e-9, 5.0)
    assert plan_from_book.road_level([("A", surface)], at, 0.25) == (pytest.approx(0.1), "A")
    assert plan_from_book.road_level([("A", surface)], past, 0.25) == (None, None)
    assert key_rules_check.own_road([("a", tin)], at, 0.25) == pytest.approx(0.1)
    assert key_rules_check.own_road([("a", tin)], past, 0.25) is None
    book = data_book.front_road_levels({"A": surface}, ["A"], {"left": at, "right": past}, 0.25)
    assert book == {"Left": (pytest.approx(0.1), "A"), "Right": (None, None)}


def test_r344_the_data_book_gives_the_road_level_of_the_build_by_the_same_surfaces_order_and_snap():
    surfaces = {"A": square(0.0), "B": square(20.0)}
    order = ["A", "B"]
    frame = {"left": (10.002, 5.0), "right": (25.0, 5.0)}
    book = data_book.front_road_levels(surfaces, order, frame, 0.005)
    for side, point in (("Left", frame["left"]), ("Right", frame["right"])):
        assert book[side] == plan_from_book.road_level([(name, surfaces[name]) for name in order], point, 0.005)
    # A corner the surfaces do not cover is blank in both
    assert data_book.front_road_levels(surfaces, order, {"left": (15.0, 5.0), "right": (15.0, 6.0)}, 0.005) == {"Left": (None, None), "Right": (None, None)}
    # The data book takes siteBuild.roadSurfaces in their order and the snap of the build, never a setting of its own
    block = SETTINGS.get("siteBuild")
    short = [name.split("__", 1)[1] for name in block["roadSurfaces"]]
    every = {name: None for name in short + ["EXISTING_11.5 Sqkm"]}
    assert data_book.road_rule(SETTINGS, every) == (short, plan_rules.road_snap(block))
    assert data_book.road_rule(SETTINGS, {name: None for name in short[1:]}) == (short[1:], block["snapM"])


def test_r341_the_own_tin_keeps_the_triangles_within_the_snap_of_config_around_the_corners(tmp_path):
    margin = inspect.signature(key_rules_check.OwnTin.__init__).parameters["margin"]
    assert margin.default is inspect.Parameter.empty
    path = made_up_landxml(tmp_path / "a.xml")
    # A box 0.004 m east of the square keeps it with a margin of 0.005 and drops it with 0.003
    box = (10.004, 4.0, 11.0, 6.0)
    assert len(key_rules_check.OwnTin(path, box, 0.005).triangles) == 2
    assert key_rules_check.OwnTin(path, box, 0.003).triangles == []
    assert not math.isnan(SETTINGS.get("siteBuild")["snapM"])
