"""R344, Q073: a front corner the road surfaces of the build do not cover takes the road at the nearest point of those surfaces within
siteBuild.roadReach.reachM, plus the sidewalk cross fall of the Typical Cross Sections drawing (R192) times the distance, after the
surfaces in their order. Every surface, face and road here is made up (R10)."""
import copy

import pytest
from shapely.geometry import LineString, box

import c3d_connect
import cad_links
import key_rules_check
import plan_from_book
import plan_rules
import project_profile
import surface_lookup

SETTINGS = project_profile.load_settings()
PROFILE = project_profile.load_profile(project_profile.profile_path(SETTINGS))
REACH = {"reachM": 6.05, "crossFallPercent": 2.0}


def square(x0, size=10.0, slope=0.01):
    """A made up road square from x0 along x, its level rising slope per metre along x."""
    xyz = [(x0, 0.0, x0 * slope), (x0 + size, 0.0, (x0 + size) * slope), (x0 + size, size, (x0 + size) * slope), (x0, size, x0 * slope)]
    return surface_lookup.Surface(xyz, [(0, 1, 2), (0, 2, 3)])


def test_r344_a_corner_no_road_surface_covers_takes_the_nearest_road_within_reach_plus_the_cross_fall():
    roads = [("A", square(0.0))]
    # 2 m east of the edge at x 10, whose level is 0.1, the sidewalk rising 2 percent toward the plot
    z, name = plan_from_book.road_level(roads, (12.0, 5.0), 0.005, REACH)
    assert z == pytest.approx(0.1 + 0.02 * 2.0) and name == "A" + plan_from_book.REACH_MARK
    assert c3d_connect.edge_nearest(roads[0][1], 12.0, 5.0, 6.05) == pytest.approx((2.0, 0.1))
    # Past the reach no level, and with no reach the build keeps R157 alone
    assert plan_from_book.road_level(roads, (16.1, 5.0), 0.005, REACH) == (None, None)
    assert plan_from_book.road_level(roads, (12.0, 5.0), 0.005) == (None, None)
    assert c3d_connect.edge_nearest(roads[0][1], 16.1, 5.0, 6.05) is None
    # The snap of R157 is unchanged by the new edge search
    assert c3d_connect.snap_z(roads[0][1], 10.002, 5.0, 0.005) == pytest.approx(0.1) and c3d_connect.snap_z(roads[0][1], 10.02, 5.0, 0.005) is None


def test_r344_the_surfaces_answer_first_in_their_order_and_the_reach_comes_after_them():
    roads = [("A", square(0.0)), ("B", square(20.0))]
    # Within the snap of A the level of A, never the reach
    assert plan_from_book.road_level(roads, (10.002, 5.0), 0.005, REACH) == (pytest.approx(0.1), "A")
    assert plan_from_book.road_level(roads, (25.0, 5.0), 0.005, REACH) == (pytest.approx(0.25), "B")
    # Off both, the nearest point of either, B 4.5 m off before A 5.5 m off
    z, name = plan_from_book.road_level(roads, (15.5, 5.0), 0.005, REACH)
    assert name == "B" + plan_from_book.REACH_MARK and z == pytest.approx(0.2 + 0.02 * 4.5)
    # A tie goes to the surface first in siteBuild.roadSurfaces
    z, name = plan_from_book.road_level(roads, (15.0, 5.0), 0.005, REACH)
    assert name == "A" + plan_from_book.REACH_MARK and z == pytest.approx(0.1 + 0.02 * 5.0)


def test_r344_config_names_the_reach_and_the_cross_fall_and_only_a_build_on_the_road_level_takes_them():
    block = SETTINGS.get("siteBuild")
    reach = block["roadReach"]
    assert isinstance(reach["reachM"], float) and reach["reachM"] > 0 and isinstance(reach["crossFallPercent"], float) and reach["note"]
    parsons = plan_rules.level_profile(PROFILE, "parsons2024Rev6")
    native = plan_rules.level_profile(PROFILE, "native2022")
    assert plan_rules.road_reach(block, parsons) == {"reachM": reach["reachM"], "crossFallPercent": reach["crossFallPercent"]}
    # A build on one named surface, the subgrade of native2022, takes no reach
    assert plan_rules.road_reach(block, native) is None
    for bad in ({"crossFallPercent": 2.0}, {"reachM": 0.0, "crossFallPercent": 2.0}, {"reachM": 6.0, "crossFallPercent": "2"}, {"reachM": True, "crossFallPercent": 2.0}):
        broken = dict(copy.deepcopy(block), roadReach=bad)
        with pytest.raises(project_profile.ProfileError, match="R344"):
            plan_rules.road_reach(broken, parsons)
    assert plan_rules.road_reach({k: v for k, v in block.items() if k != "roadReach"}, parsons) is None


def test_r344_the_fronts_hold_the_reach_level_and_its_surface_at_a_corner_off_the_road():
    # A plot of 12 m by 20 m whose front edge at y 0 faces a road edge 6 m off, the road surface stopping 3 m short of the front
    faces = {"1": box(0.0, 0.0, 12.0, 20.0)}
    roads_cad = [LineString([(-50.0, -6.0), (60.0, -6.0)])]
    flat = surface_lookup.Surface([(-50.0, -20.0, 10.0), (60.0, -20.0, 10.0), (60.0, -3.0, 10.0), (-50.0, -3.0, 10.0)], [(0, 1, 2), (0, 2, 3)])
    limits = cad_links.front_limits(PROFILE, SETTINGS.get("analysis"))
    rows = {"1": {"typologyRev9": "DP1"}}
    found = plan_from_book.fronts(["1"], rows, faces, roads_cad, limits, [("Road", flat)], 0.005, PROFILE, reach=REACH)["1"]
    assert found["front"] == [pytest.approx(10.06), pytest.approx(10.06)] and found["surfaces"] == ["Road" + plan_from_book.REACH_MARK] * 2
    # With no reach the plot has no road level, as before
    assert plan_from_book.fronts(["1"], rows, faces, roads_cad, limits, [("Road", flat)], 0.005, PROFILE)["1"]["front"] is None


def test_r344_the_second_method_gives_the_reach_by_its_own_route(tmp_path):
    path = tmp_path / "made_up.xml"
    path.write_text("""<?xml version="1.0"?>
<LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2"><Surfaces><Surface name="Made Up"><Definition surfType="TIN"><Pnts>
<P id="1">0.0 0.0 0.0</P>
<P id="2">10.0 0.0 0.0</P>
<P id="3">10.0 10.0 0.1</P>
<P id="4">0.0 10.0 0.1</P>
</Pnts><Faces>
<F>1 2 3</F>
<F>1 3 4</F>
</Faces></Definition></Surface></Surfaces></LandXML>
""", encoding="utf-8")
    tin = key_rules_check.OwnTin(path)
    # The text holds northing then easting, the level rises 0.01 per m to the east
    assert tin.nearest_side(12.0, 5.0, 6.05) == pytest.approx((2.0, 0.1)) and tin.nearest_side(16.1, 5.0, 6.05) is None
    assert key_rules_check.own_road([("a", tin)], (12.0, 5.0), 0.005, REACH) == pytest.approx(0.14)
    assert key_rules_check.own_road([("a", tin)], (12.0, 5.0), 0.005) is None
    assert key_rules_check.own_road([("a", tin)], (5.0, 5.0), 0.005, REACH) == pytest.approx(0.05)
    # The own route reads the config as plain JSON, as its main does
    own = key_rules_check.read_json(project_profile.SETTINGS_PATH)
    assert key_rules_check.own_reach(own, plan_rules.level_profile(PROFILE, "parsons2024Rev6")) == plan_rules.road_reach(
        SETTINGS.get("siteBuild"), plan_rules.level_profile(PROFILE, "parsons2024Rev6"))
    assert key_rules_check.own_reach(own, plan_rules.level_profile(PROFILE, "native2022")) is None
