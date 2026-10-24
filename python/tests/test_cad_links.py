import copy
import math

import pytest
from shapely.geometry import LineString, Polygon
from shapely.strtree import STRtree

import cad_links
import cad_links_check
import project_profile
import prototype_offsets

ROAD_LAYER = "RD-CURB-LINE"
FLOORS = {"types": {"generic": ["Generic 150mm"], "yard": ["yard"], "ramp": ["Ramps"]}}
ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "roadEdgeLayers": [ROAD_LAYER]}
PROFILE = {"units_mm": True, "typologies": {"DP1": {"kind": "duplex", "plot_width_m": 10}, "TH1": {"kind": "townhouse", "plot_width_m": 8}, "VL4": {"kind": "villa", "plot_width_m": 12}},
           "plot": {"long_side": 25000}}


def box(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def profile(tmp_path):
    return project_profile.ProjectProfile(tmp_path / "p.json", PROFILE)


def frame_of(polygon, roads, limits=(7.99, 18.75)):
    return cad_links.front_frame(Polygon(polygon), STRtree(roads), roads, limits)


def test_r122_the_front_limits_come_from_the_narrowest_plot_width_less_the_tolerance_and_the_party_wall_share(tmp_path):
    low, high = cad_links.front_limits(profile(tmp_path), ANALYSIS)
    assert low == pytest.approx(7.99) and high == pytest.approx(18.75)
    with pytest.raises(project_profile.ProfileError, match="plot_width_m"):
        cad_links.front_limits(project_profile.ProjectProfile(tmp_path / "p.json", {**PROFILE, "typologies": {"DP1": {}}}), ANALYSIS)


def test_r122_the_front_is_the_short_edge_nearest_the_road_and_the_link_y_axis_points_into_the_plot():
    roads = [LineString([(-100, -5), (100, -5)])]
    frame = frame_of(box(0, 0, 10, 25), roads)
    assert frame["n"] == pytest.approx((0, 1)) and frame["r"] == pytest.approx((1, 0))
    assert frame["left"] == (0, 0) and frame["right"] == (10, 0) and frame["tied"] is False
    assert cad_links.rotation_of(frame, False) == pytest.approx(0.0)
    assert cad_links.rotation_of(frame, True) == pytest.approx(180.0)
    # Mirrored or not the link Y axis is the inward normal, only X turns
    assert cad_links.link_axes(frame, True)[1] == frame["n"]
    # A plot north of the road seen from the road has its left at the west, a plot south of it at the east
    south = frame_of(box(0, -35, 10, -10), roads)
    assert south["n"] == pytest.approx((0, -1)) and south["left"] == (10, -10)


def test_r122_a_long_side_or_a_chamfer_nearer_the_road_is_never_the_front():
    # A corner plot with a side road one metre from its 25 m side and five metres from its front
    roads = [LineString([(-100, -5), (100, -5)]), LineString([(13, -100), (13, 100)])]
    frame = frame_of(box(0, 0, 12, 25), roads)
    assert frame["n"] == pytest.approx((0, 1))
    # A 4 m chamfer at the front corner lies nearer the road than the front itself, the front of R94 takes the chamfer
    chamfered = [(0, 0), (9, 0), (12, 3), (12, 25), (0, 25)]
    roads = [LineString([(-100, -3), (100, -3)]), LineString([(11.5, 1.0), (11.6, 1.0)])]
    frame = frame_of(chamfered, roads)
    assert frame["n"] == pytest.approx((0, 1)) and frame["length"] == pytest.approx(9.0)
    import entry_levels
    assert math.dist(*entry_levels.front_edge(Polygon(chamfered), STRtree(roads), roads)["edge"]) == pytest.approx(math.sqrt(18))
    assert cad_links.front_frame(Polygon(box(0, 0, 10, 25)), None, [], (7.99, 18.75)) is None
    assert frame_of(box(0, 0, 30, 30), [LineString([(-100, -5), (100, -5)])]) is None


def test_r120_a_floor_half_on_two_faces_is_listed_with_both_faces_and_a_sliver_is_left_out():
    faces = {"11": Polygon(box(0, 0, 10, 25)), "12": Polygon(box(10, 0, 20, 25)), "13": Polygon(box(20, 0, 30, 25))}
    generics = {"11": Polygon(box(5, 0, 15, 25)), "12": Polygon(box(10, 0, 20, 25)), "13": Polygon(box(20, 0, 30.0000001, 25))}
    rows, listed = cad_links.cut_list(generics, faces, 0.9)
    assert listed == ["11"]
    assert rows["11"]["best"] == pytest.approx(0.5) and [number for _, number in rows["11"]["shares"]] == ["11", "12"]
    assert rows["12"]["best"] == pytest.approx(1.0) and len(rows["12"]["shares"]) == 1
    assert [number for _, number in rows["13"]["shares"]] == ["13"]
    report = cad_links.cut_report(listed, rows, {"11": {"label": "1", "type": "VL4"}})
    assert report["listed"] == 1 and report["entries"][0]["faces"][0] == {"face": "11", "label": "1", "type": "VL4", "share": pytest.approx(0.5)}


def test_r120_the_cut_share_comes_from_the_settings_and_a_bad_one_is_refused(tmp_path):
    settings = project_profile.ProjectProfile(tmp_path / "s.json", {"cadLinks": {"cutShareMin": 0.9}})
    assert cad_links.cad_links_settings(settings)["cutShareMin"] == 0.9
    for bad in ({}, {"cadLinks": {"cutShareMin": 0}}, {"cadLinks": {"cutShareMin": True}}, {"cadLinks": {"cutShareMin": 1.5}}, {"cadLinks": 3}):
        with pytest.raises(project_profile.ProfileError, match="cadLinks"):
            cad_links.cad_links_settings(project_profile.ProjectProfile(tmp_path / "s.json", bad))


def pair_job():
    """A 12 m and a 10 m duplex plot side by side north of a road, the left as drawn and the right mirrored, origin 10 m back along X from the end corner and 0.5 m in.
    The plots differ in width so only the end corner gives one offset."""
    roads = [LineString([(-100, -5), (100, -5)])]
    faces = {"11": Polygon(box(0, 0, 12, 25)), "12": Polygon(box(12, 0, 22, 25))}
    links = {"11": {"origin": (2.0, 0.5), "rotation": 0.0, "mirrored": False, "unit": "DP1", "case": "A"},
             "12": {"origin": (22.0, 0.5), "rotation": 180.0, "mirrored": True, "unit": "DP1", "case": "B"}}
    generics = {"11": Polygon(box(0, 0, 12, 20)), "12": Polygon(box(12, 0, 22, 20))}
    clusters = [{"code": "M01", "plots": ["2", "1"]}]
    return roads, faces, links, generics, clusters


def test_r121_the_origin_is_fitted_from_the_front_corner_that_lies_nearer_and_a_corner_is_named_from_the_road():
    roads, faces, links, _, _ = pair_job()
    frames = {plot: frame_of(list(faces[plot].exterior.coords)[:-1], roads) for plot in faces}
    fits = cad_links.fit_origins(links, frames, set())
    assert fits["DP1"]["anchor"] == "end" and fits["DP1"]["offset"] == pytest.approx([-10.0, 0.5])
    for plot in links:
        assert cad_links.predicted_origin(frames[plot], links[plot]["mirrored"], fits["DP1"]) == pytest.approx(links[plot]["origin"])
    distance, name = cad_links.nearest_corner((0.0, 0.5), faces["11"], frames["11"])
    assert distance == pytest.approx(0.5) and name == "front left"
    assert cad_links.nearest_corner((22.0, 25.0), faces["12"], frames["12"])[1] == "back right"
    # A plot on the cut list is left out of the fit
    assert cad_links.fit_origins(links, frames, {"11", "12"}) == {}
    assert [cad_links.band_of(value) for value in (0.001, 0.0100000001, 0.02)] == ["within1mm", "within10mm", "worse"]


def test_r123_a_cluster_is_put_left_to_right_from_the_road_whatever_order_the_analysis_lists():
    roads, faces, links, _, clusters = pair_job()
    frames = {plot: frame_of(list(faces[plot].exterior.coords)[:-1], roads) for plot in faces}
    roles = cad_links.cluster_roles(clusters, 10, faces, frames)
    assert roles["11"]["side"] == "left" and roles["12"]["side"] == "right" and roles["11"]["role"] == "pair" and roles["11"]["plots"] == ["11", "12"]
    four = {str(10 + index): Polygon(box(8 * index, 0, 8 * index + 8, 25)) for index in range(1, 5)}
    four_frames = {plot: frame_of(list(four[plot].exterior.coords)[:-1], roads) for plot in four}
    roles = cad_links.cluster_roles([{"code": "M07", "plots": ["1", "2", "3", "4"]}], 10, four, four_frames)
    assert [roles[plot]["role"] for plot in ("11", "12", "13", "14")] == ["end", "middle", "middle", "end"]
    assert [roles[plot]["side"] for plot in ("11", "12", "13", "14")] == ["left", "left", "right", "right"]
    three = {str(10 + index): Polygon(box(8 * index, 0, 8 * index + 8, 25)) for index in range(1, 4)}
    roles = cad_links.cluster_roles([{"code": "X", "plots": ["1", "2", "3"]}], 10, three, {plot: frame_of(list(three[plot].exterior.coords)[:-1], roads) for plot in three})
    assert roles["12"]["side"] == "middle"


def test_r123_the_drawn_side_holds_only_when_every_plot_agrees_and_the_mirror_follows_it():
    roles = {"1": {"cluster": "M01", "side": "left", "role": "pair"}, "2": {"cluster": "M01", "side": "right", "role": "pair"},
             "3": {"cluster": "M05", "side": "left", "role": "pair"}, "4": {"cluster": "M05", "side": "right", "role": "pair"},
             "5": {"cluster": "M05", "side": "left", "role": "pair"}, "6": {"cluster": "M05", "side": "right", "role": "pair"}}
    link = lambda unit, mirrored: {"unit": unit, "mirrored": mirrored, "rotation": 0.0, "case": None}
    links = {"1": link("DP1", False), "2": link("DP1", True), "3": link("DT1", True), "4": link("DT1", False), "5": link("TH1", True), "6": link("TH1", True)}
    sides = cad_links.drawn_sides(links, roles, set())
    assert sides["DP1"]["drawnSide"] == "left" and sides["DT1"]["drawnSide"] == "right" and sides["TH1"]["drawnSide"] is None
    assert cad_links.mirror_of("DP1", roles["2"], sides) is True and cad_links.mirror_of("DT1", roles["4"], sides) is False
    assert cad_links.mirror_of("TH1", roles["5"], sides) is None and cad_links.mirror_of("DP1", None, sides) is None
    assert cad_links.mirror_of("DP1", {"side": "middle"}, sides) is None
    # A plot on the cut list never decides a drawn side
    assert cad_links.drawn_sides({**links, "6": link("TH1", False)}, roles, set())["TH1"]["drawnSide"] == "right"
    assert cad_links.drawn_sides(links, roles, {"5", "6"}).get("TH1") is None
    lead, breaks = cad_links.lead_counts(links, roles, {"6"})
    assert lead["M01 pair"] == {"holds": 2} and lead["M05 pair"] == {"breaks": 3, "apart": 1} and breaks["M05 pair"] == ["3", "4", "5"]


def test_r123_a_case_letter_on_two_rotation_and_mirror_pairs_with_one_facing_is_said_so():
    links = {"1": {"case": "A", "rotation": 30.1, "mirrored": True}, "2": {"case": "A", "rotation": 210.1, "mirrored": False},
             "3": {"case": "C", "rotation": 120.1, "mirrored": False}, "4": {"case": None, "rotation": 0.0, "mirrored": False}}
    table = cad_links.case_table(links)
    assert table["A"]["onePair"] is False and table["A"]["oneFacing"] is True and table["A"]["facingDeg"] == {300.1: 2}
    assert table["C"]["onePair"] is True and table["C"]["facingDeg"] == {210.1: 1} and None not in table


def raw_job():
    """The pair job as the raw files hold it, for measure through read_past and for the second method."""
    roads, faces, links, generics, clusters = pair_job()
    document = {"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "links": [], "floors": []}
    for plot, link in links.items():
        document["links"].append({"transform": {"origin": [link["origin"][0], link["origin"][1], 0.0], "rotationDeg": link["rotation"], "mirrored": link["mirrored"]},
                                  "parameters": {"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Prototype": link["unit"], "CASE": link["case"]}})
        document["floors"].append({"typeName": "Generic 150mm", "plotNumber": plot, "outline": [[[x, y, 0.0] for x, y in list(generics[plot].exterior.coords)[:-1]]]})
    plots = [{"kind": "plot_shape", "plot": str(int(plot) - 10), "type": "DP2", "vertices": [list(point) for point in list(faces[plot].exterior.coords)[:-1]]} for plot in faces]
    pieces = [{"kind": "line", "layer": "X$0$" + ROAD_LAYER, "pts": [[-100, -5], [100, -5]]}]
    return document, plots, pieces, {"clusters": clusters}


def measured(tmp_path):
    document, plots, pieces, analysis = raw_job()
    import yard_rule
    faces, _ = yard_rule.face_shapes(plots, 10)
    links, generics, twice = cad_links.read_past(document, [0, 0, 0], FLOORS)
    roads = [LineString(pieces[0]["pts"])]
    report, section = cad_links.measure(links, generics, faces, roads, analysis["clusters"], 10, (7.99, 18.75), 0.9, cad_links.face_labels(plots, 10))
    return report, section, document, plots, pieces, analysis, twice


def test_r123_measure_on_a_pair_gives_the_rotation_the_mirror_rule_and_the_frame_section(tmp_path):
    report, section, _, _, _, _, twice = measured(tmp_path)
    assert twice == [] and report["plots"] == 2 and report["cutList"]["listed"] == 0
    assert report["rotation"]["counts"] == {"outside holds": 2, "outside R94 front holds": 2}
    assert report["mirror"]["rule"]["counts"] == {"outside holds": 2} and report["mirror"]["drawnSides"]["DP1"]["drawnSide"] == "left"
    assert section["units"]["DP1"] == {"anchor": "end", "offset": pytest.approx([-10.0, 0.5]), "drawnSide": "left"} and section["apart"] == []
    assert report["origin"]["bands"]["origin outside"]["DP1"] == {"within1mm": 2} and report["origin"]["worse"] == []


def test_r124_the_second_method_agrees_on_every_drawn_plot_and_catches_a_planted_change(tmp_path):
    report, _, document, plots, pieces, analysis, _ = measured(tmp_path)
    settings = {"floors": FLOORS, "analysis": ANALYSIS}
    raw = {"documents": [document]}
    results = cad_links_check.check(report["rows"], report, raw, plots, pieces, analysis, PROFILE, settings, 10, 1, 5)
    assert len(results) == 2 and all(item["differs"] == [] and item["notChecked"] == [] for item in results)
    planted = copy.deepcopy(report)
    planted["rows"][0]["rotationGap"] = 90.0
    planted["rows"][1]["cutShare"] = 0.5
    planted["rows"][1]["side"] = "left"
    found = {item["plot"]: item["differs"] for item in cad_links_check.check(planted["rows"], planted, raw, plots, pieces, analysis, PROFILE, settings, 10, 1, 5)}
    assert any(text.startswith("rotationGap") for text in found["11"])
    assert any(text.startswith("cutShare") for text in found["12"]) and any(text.startswith("side") for text in found["12"])


def test_r124_plain_clipping_and_the_shoelace_area_agree_with_a_hand_count():
    square = cad_links_check.counter_clockwise(box(0, 0, 10, 10))
    part = cad_links_check.clip(cad_links_check.counter_clockwise(box(5, 5, 15, 15)), square)
    assert cad_links_check.shoelace(part) == pytest.approx(25.0)
    assert cad_links_check.clip(cad_links_check.counter_clockwise(box(20, 20, 30, 30)), square) == []
    assert cad_links_check.centroid(square) == pytest.approx((5.0, 5.0))
    # A vertex a hair off a straight edge is no bend, a real notch is
    assert cad_links_check.convex([(0, 0), (5, 0.0001), (10, 0), (10, 10), (0, 10)]) is True
    assert cad_links_check.convex([(0, 0), (10, 0), (10, 10), (5, 5), (0, 10)]) is False
    assert cad_links_check.segment_distance((5, 3), (0, 0), (10, 0)) == pytest.approx(3.0)
    assert cad_links_check.segment_distance((13, 4), (0, 0), (10, 0)) == pytest.approx(5.0)


def test_r124_a_share_that_plain_clipping_cannot_take_is_listed_as_not_checked_and_never_agrees(tmp_path):
    report, _, document, plots, pieces, analysis, _ = measured(tmp_path)
    notch = [[0, 0], [10, 0], [10, 25], [5, 20], [0, 25]]
    plots[0]["vertices"] = notch
    for floor in document["floors"]:
        if floor["plotNumber"] == "11":
            floor["outline"] = [[[x, y, 0.0] for x, y in [(1, 1), (9, 1), (9, 20), (5, 15), (1, 20)]]]
    results = cad_links_check.check(report["rows"], report, {"documents": [document]}, plots, pieces, analysis, PROFILE, {"floors": FLOORS, "analysis": ANALYSIS}, 10, 1, 5)
    first = next(item for item in results if item["plot"] == "11")
    assert first["notChecked"] and first["notChecked"][0].startswith("cutShare")


def test_r100_the_plot_frame_section_is_read_and_a_bad_unit_is_refused():
    frame = prototype_offsets.plot_frame(prototype_offsets.load())
    assert frame["units"] and all(entry["anchor"] in ("start", "end") for entry in frame["units"].values())
    for bad in ({}, {"plotFrame": {"units": {"DP1": {"anchor": "side", "offset": [0, 0], "drawnSide": None}}}},
                {"plotFrame": {"units": {"DP1": {"anchor": "end", "offset": [0], "drawnSide": None}}}},
                {"plotFrame": {"units": {"DP1": {"anchor": "end", "offset": [0, True], "drawnSide": None}}}},
                {"plotFrame": {"units": {"DP1": {"anchor": "end", "offset": [0, 0], "drawnSide": "up"}}}}):
        with pytest.raises(project_profile.ProfileError, match="plotFrame"):
            prototype_offsets.plot_frame(bad)
