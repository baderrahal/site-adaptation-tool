"""R354 to R357. The build steps of the parity harness on made up faces, blocks, curbs and clusters built in code (R10): the clusters of a
build, the fronts, the hands and the link frame of plan_from_book.py, which the C# of the worker gives the same (BuildStepTests.cs holds
the same made up data)."""
import json

import pytest
from shapely.geometry import Polygon, box

import parity
import plan_from_book
import project_profile


def ring(x0, y0, x1, y1):
    return [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]


def plot_shape(label, vertices):
    return {"kind": "plot_shape", "plot": label, "vertices": vertices}


# A row of six 12 m faces along x, labels 101 to 106 numbered 1 to 6, and faces 7 and 8 apart
ROW = {str(index + 1): ring(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0) for index in range(6)}
APART = {"7": ring(100.0, 0.0, 112.0, 25.0), "8": ring(112.0, 0.0, 124.0, 25.0)}
FACES = {number: Polygon(vertices).buffer(0) for number, vertices in {**ROW, **APART}.items()}
ROWS = {number: {"locationPlanNumber": number, "baseplanLabel": str(100 + int(number)), "typologyRev9": "DP2", "footprintBlock": block}
        for number, block in (("1", "Duplex_DP2"), ("2", "Duplex_DP2"), ("3", "Duplex_DP2"), ("4", "Duplex_DP2"), ("5", "Villa_VD2"), ("6", "Duplex_DP2"),
                              ("7", "Pair_N"), ("8", "Pair_N"))}
NUMBER_OF = {row["baseplanLabel"]: number for number, row in ROWS.items()}
# Two duplex blocks of one name, a villa block on 5 alone, and two blocks of one name on 7 that tie at 150 m2, the second also on 8
BLOCKS = [{"name": "Duplex_DP2", "outline": ring(2.0, 5.0, 22.0, 20.0)}, {"name": "Duplex_DP2", "outline": ring(26.0, 5.0, 46.0, 20.0)},
          {"name": "Villa_VD2", "outline": ring(50.0, 5.0, 58.0, 20.0)}, {"name": "Pair_N", "outline": ring(100.0, 0.0, 106.0, 25.0)},
          {"name": "Pair_N", "outline": ring(106.0, 0.0, 118.0, 25.0)},
          # A block of the name of 6 that touches it along an edge only, area 0, so it joins 6 to nothing
          {"name": "Duplex_DP2", "outline": ring(72.0, 5.0, 80.0, 20.0)}]
# The analysis by baseplan labels: 104 to 106, 104 taken by a block, so 5 and 6 stay a cluster of the analysis
DRAWINGS = project_profile.load_settings().get("drawings")
ANALYSIS = {"source": DRAWINGS["baseplan"], "clusters": [{"code": "M02", "plots": ["104", "105", "106"]}]}


def made_clusters(analysis=ANALYSIS, rows=ROWS, number_of=NUMBER_OF):
    import footprint_check
    shapes = footprint_check.block_shapes(BLOCKS)
    units = [number for number, row in rows.items() if row.get("typologyRev9") and number in FACES]
    return plan_from_book.site_clusters(units, rows, FACES, shapes, analysis["clusters"], number_of)


def test_r354_the_clusters_of_a_build_are_the_footprint_clusters_then_the_rest_of_the_analysis_each_with_its_source():
    clusters = made_clusters()
    assert [(cluster["code"], cluster["plots"], cluster["source"]) for cluster in clusters] == [
        ("Duplex_DP2", ["1", "2"], "footprint"), ("Duplex_DP2", ["3", "4"], "footprint"), ("M02", ["5", "6"], "analysis")]
    step = plan_from_book.cluster_step([str(number) for number in range(1, 9)], clusters)
    assert step == {"1|2": {"plots": ["1", "2"], "code": "Duplex_DP2", "source": "footprint"}, "3|4": {"plots": ["3", "4"], "code": "Duplex_DP2", "source": "footprint"},
                    "5|6": {"plots": ["5", "6"], "code": "M02", "source": "analysis"}}
    # A scope holds the clusters that hold one of its plots, the whole cluster
    assert list(plan_from_book.cluster_step(["2"], clusters)) == ["1|2"]
    assert plan_from_book.cluster_step(["7", "8"], clusters) == {}


def test_r354_two_blocks_of_one_name_that_tie_on_a_face_give_it_the_first_and_a_block_on_its_edge_gives_none():
    clusters = made_clusters()
    # 7 holds 150 m2 of each block of Pair_N, the first in the read takes it, so 7 and 8 lie on two blocks and form no cluster
    assert not any(set(cluster["plots"]) & {"7", "8"} for cluster in clusters)
    # The block that touches 6 along an edge shares no area with it
    assert all(cluster["source"] == "analysis" for cluster in clusters if "6" in cluster["plots"])


def test_r354_a_plot_of_two_plan_numbers_or_a_cluster_left_with_one_plot_is_no_cluster():
    rows = dict(ROWS, **{"7|8": dict(ROWS["7"], locationPlanNumber="7|8")})
    number_of = dict(NUMBER_OF, **{"107": "7|8"})
    analysis = {"clusters": [{"code": "M04", "plots": ["107", "108"]}, {"code": "M02", "plots": ["101", "105"]}]}
    clusters = made_clusters(analysis, rows, number_of)
    assert [cluster["plots"] for cluster in clusters] == [["1", "2"], ["3", "4"]]


def test_r354_a_label_of_two_rings_is_refused_by_the_build_steps():
    plots = [plot_shape("101", ROW["1"]), plot_shape("102", ROW["2"]), plot_shape("0102", ring(24.0, 0.0, 30.0, 10.0)), plot_shape("x", ROW["3"])]
    with pytest.raises(project_profile.ProfileError) as error:
        plan_from_book.one_ring_faces(plots)
    assert str(error.value) == ("1 baseplan labels hold two rings or more: 102, a build takes a face of one ring, whose edges it walks in the order "
                                "shapely gives them, so plan_from_book.py and the C# refuse such a label (R354)")
    plan_from_book.one_ring_faces([plot_shape("101", ROW["1"]), plot_shape("102", ring(0.0, 0.0, 1.0, 0.0))])


def test_r354_a_footprint_block_that_is_not_a_simple_ring_is_refused_by_the_build():
    bow_tie = {"name": "Duplex_DP2", "insertPoint": [1.0, 2.0, 0.0], "outline": [[0.0, 0.0], [10.0, 10.0], [10.0, 0.0], [0.0, 10.0]]}
    with pytest.raises(ValueError) as error:
        plan_from_book.simple_blocks(BLOCKS + [bow_tie])
    assert str(error.value) == ("the footprint block Duplex_DP2 at [1.0, 2.0, 0.0] is not a simple ring, two of its edges meet, shapely would mend it by "
                                "buffer(0) and the C# would not, so the build refuses it (R354)")
    assert plan_from_book.simple_blocks(BLOCKS) == BLOCKS


def test_r354_an_analysis_of_another_drawing_or_with_no_clusters_is_refused(tmp_path):
    settings = project_profile.load_settings()
    path = tmp_path / "analysis.json"
    path.write_text(json.dumps(ANALYSIS), encoding="utf-8")
    assert plan_from_book.analysis_clusters(path, settings) == ANALYSIS["clusters"]
    for content, why in (({"source": DRAWINGS["villaBlocks"], "clusters": []}, "is of " + DRAWINGS["villaBlocks"]),
                         ({"clusters": []}, "is of None"), ({"source": DRAWINGS["baseplan"]}, "holds no clusters list"),
                         ({"source": DRAWINGS["baseplan"].swapcase(), "clusters": []}, "is of " + DRAWINGS["baseplan"].swapcase())):
        path.write_text(json.dumps(content), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.analysis_clusters(path, settings)
        assert why in str(error.value) and "(R354)" in str(error.value)


def test_r354_the_config_names_the_fields_of_the_step_clusters_all_exact():
    block = project_profile.load_settings().get("addin")["parity"]["steps"]
    assert block["clusters"] == {"plots": {"kind": "noGeometry"}, "code": {"kind": "noGeometry"}, "source": {"kind": "noGeometry"}}
    assert parity.tolerances(project_profile.load_settings().values)["clusters"]["plots"] == ("noGeometry", None, None)


def test_r354_the_ring_of_a_face_is_the_one_shapely_gives_by_buffer_zero():
    """The C# takes the ring of a face by this rule (BuildFace.Of), measured on the 2,962 faces of the baseplan (round_addin logs
    p4e_buffer_order.txt): a repeated vertex and the closing one dropped, an anticlockwise ring turned clockwise from its first vertex, a
    clockwise ring as it is, a collinear vertex kept."""
    def order(vertices):
        return [list(point) for point in list(Polygon(vertices).buffer(0).exterior.coords)[:-1]]
    square = ring(0.0, 0.0, 10.0, 10.0)
    assert order(square) == [[0.0, 0.0], [0.0, 10.0], [10.0, 10.0], [10.0, 0.0]]
    assert order(square[::-1]) == square[::-1]
    assert order([[0, 0], [10, 0], [10, 0], [10, 10], [0, 10], [0, 0]]) == [[0.0, 0.0], [0.0, 10.0], [10.0, 10.0], [10.0, 0.0]]
    assert order([[0, 0], [5, 0], [10, 0], [10, 10], [0, 10]]) == [[0.0, 0.0], [0.0, 10.0], [10.0, 10.0], [10.0, 0.0], [5.0, 0.0]]
    assert [list(point) for point in plan_from_book.face_ring(Polygon(square).buffer(0))] == order(square)


def test_r354_a_level_profile_with_no_key_rules_or_a_key_rule_a_build_cannot_give_is_refused_with_the_lines_of_csharp():
    import copy
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    assert plan_from_book.build_step_rules(real, None)["linkRotation"]["rule"] == "frontRule"

    def refused(values, name):
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.build_step_rules(project_profile.ProjectProfile(real.path, values), name)
        return str(error.value)
    assert refused(real.values, "native2022") == ("the build steps %s of the parity harness are those of a build by keyRules, the level profile native2022 holds "
                                                  "none, the C# builds by keyRules alone (R354)" % ", ".join(plan_from_book.BUILD_STEPS))
    assert refused(real.values, "other") == "the rules profile has no level profile other"
    for word, path, value, line in (
            ("hand villa", ("hand villa", "rule"), "clusterIndexParity", "keyRules hand villa codes clusterIndexParity, a build gives only all, blockMirror, clusterSide, "
             "openEnd, roadSide, blockMirrorOpenEnd, blockMirrorRoadSide, rowPairSide, rowPairSideOrBlockMirrorOpenEnd with byFile (R337)"),
            ("linkRotation", ("linkRotation", "rule"), "nearestCurb", "keyRules linkRotation codes nearestCurb, a build gives only frontRule (R337)"),
            ("rampSlope", ("rampSlope", "table", "C10"), "0.5", "the slope of El @ Entry and the ramp class of keyRules differ for C10 (rampSlopePercent 2.0, "
             "keyRules.rampSlope 0.5), give both one slope (R337)")):
        values = copy.deepcopy(real.values)
        place = values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
        for key in path[:-1]:
            place = place[key]
        place[path[-1]] = value
        assert refused(values, None) == line, word


# R355 the fronts: a 12 m face 3 m off a road along y = -3, a corner face with a second road along x = 45, and a face with no unit
FRONT_FACES = {"11": ring(0.0, 0.0, 12.0, 25.0), "12": ring(30.0, 0.0, 42.0, 25.0), "13": ring(60.0, 0.0, 72.0, 25.0)}
FRONT_ROWS = {"11": {"typologyRev9": "DP2"}, "12": {"typologyRev9": "C10"}, "13": {"typologyRev9": ""}}


def made_fronts(orientation, numbers=("11", "12", "13", "14")):
    from shapely.geometry import LineString
    import cad_links
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    faces = {number: Polygon(vertices).buffer(0) for number, vertices in FRONT_FACES.items()}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)]), LineString([(45.0, -10.0), (45.0, 40.0)])]
    sales = {"12": {"Orientation": orientation}} if orientation is not None else {"12": {"Orientation": None}}
    limits = cad_links.front_limits(profile, settings.get("analysis"))
    return plan_from_book.fronts(list(numbers), FRONT_ROWS, faces, roads, limits, [("Top Surface-02", None)], 0.005, profile, settings.get("siteBuild")["corner"], sales)


def stub_levels(monkeypatch):
    # A level at a corner west of x = 40 only, so the corner face 12 has its right corner on no surface
    monkeypatch.setattr(plan_from_book, "road_level", lambda surfaces, point, snap: (10.0 + point[0] / 100.0, "Top Surface-02") if point[0] < 40.0 else (None, None))


def test_r355_the_front_of_a_plot_is_its_edge_nearest_a_curb_within_the_limits_with_its_frame_ramp_corners_and_road_levels(monkeypatch):
    stub_levels(monkeypatch)
    front = made_fronts("WS")
    step = plan_from_book.front_step(["11", "12", "13", "14"], front, [("Top Surface-02", None)], 0.005)
    assert step["11"] == {"frontRule": "nearestCurb", "unit": "DP2", "left": [0.0, 0.0], "right": [12.0, 0.0], "normal": [0.0, 1.0], "rightHand": [1.0, -0.0], "length": 12.0,
                          "distance": 3.0, "tied": False, "depth": 5.52, "corners": [[0.0, 0.0], [12.0, 0.0], [12.0, 5.52], [0.0, 5.52]], "roadLevelLeft": 10.0,
                          "roadLevelRight": 10.12, "surfaceLeft": "Top Surface-02", "surfaceRight": "Top Surface-02", "noRoadLevel": None}
    # 13 has a frame and no unit, so no ramp and no road level, 14 has no face
    assert step["13"]["left"] == [60.0, 0.0] and step["13"]["corners"] is None and step["13"]["noRoadLevel"] is None and step["13"]["unit"] is None
    assert step["14"] == {"frontRule": "nearestCurb", "unit": None, "left": None, "right": None, "normal": None, "rightHand": None, "length": None, "distance": None,
                          "tied": None, "depth": None, "corners": None, "roadLevelLeft": None, "roadLevelRight": None, "surfaceLeft": None, "surfaceRight": None,
                          "noRoadLevel": None}


def test_r355_a_corner_plot_takes_the_road_edge_its_orientation_names_its_r122_frame_on_that_edge_and_no_front_with_no_orientation(monkeypatch):
    stub_levels(monkeypatch)
    # WS takes the edge facing south, the R122 front, as the rule orientation
    front = made_fronts("WS")
    assert front["12"]["frontRule"] == "orientation" and front["12"]["frame"]["left"] == (30.0, 0.0) and front["12"]["frame"]["right"] == (42.0, 0.0)
    step = plan_from_book.front_step(["12"], front, [("Top Surface-02", None)], 0.005)["12"]
    assert step["depth"] == 5.5 and step["roadLevelLeft"] is None and step["surfaceLeft"] == "Top Surface-02" and step["surfaceRight"] is None
    assert step["noRoadLevel"] == {"plot": "12", "corners": ["right"], "tried": ["Top Surface-02"],
                                   "reason": "no road surface covers the corner within snapM 0.005 m, so no road level is given (R344)"}
    # SE takes the edge facing east, a frame of its own on it, its ends left and right seen from the road
    front = made_fronts("SE")
    frame = front["12"]["frame"]
    assert front["12"]["frontRule"] == "orientation" and frame["left"] == (42.0, 0.0) and frame["right"] == (42.0, 25.0)
    assert frame["n"] == (-1.0, -0.0) and frame["r"] == (-0.0, 1.0) and frame["length"] == 25.0 and frame["distance"] == 3.0 and frame["tied"] is False
    # No Orientation in the sales sheet: no front, and a unit plot with no front frame has no front corners for a road level
    front = made_fronts(None)
    assert front["12"]["frame"] is None and front["12"]["frontRule"] == "a corner plot with no Orientation in the sales sheet"
    assert plan_from_book.front_step(["12"], front, [("Top Surface-02", None)], 0.005)["12"]["noRoadLevel"] == {
        "plot": "12", "corners": [], "tried": [], "reason": "no front frame, so no front corners to take a road level at"}


def test_r355_the_config_names_the_fields_of_the_step_fronts_by_their_kind():
    fields = project_profile.load_settings().get("addin")["parity"]["steps"]["fronts"]
    assert {name: (field["kind"], field.get("decimals")) for name, field in fields.items()} == {
        "frontRule": ("noGeometry", None), "unit": ("noGeometry", None), "left": ("points", 3), "right": ("points", 3), "normal": ("points", 6), "rightHand": ("points", 6),
        "length": ("points", 3), "distance": ("points", 3), "tied": ("noGeometry", None), "depth": ("points", 3), "corners": ("points", 3),
        "roadLevelLeft": ("levels", 4), "roadLevelRight": ("levels", 4), "surfaceLeft": ("noGeometry", None), "surfaceRight": ("noGeometry", None),
        "noRoadLevel": ("noGeometry", None)}


# R357 the hands: a row of six 12 m faces along a road at y = -3, plot 1 a VD2 villa with a side road at x = -3, plots 2 and 3 a DP2 pair of
# the footprint, plots 4 to 6 DP2 units in no cluster, the tables of keyRules made up for one file each
DP2_FILE = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt"
VD2_FILE = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt"
HAND_UNITS = {"1": "VD2", "2": "DP2", "3": "DP2", "4": "DP2", "5": "DP2", "6": "DP2"}


def hand_rules():
    """keyRules as the rules profile holds them, the hand tables made up for the made up row."""
    import copy
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    values = copy.deepcopy(real.values)
    key = values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
    key["hand villa"] = {"rule": "blockMirrorRoadSide", "byFile": {VD2_FILE: {"no left": True, "no none": False}}}
    key["hand cluster"] = {"rule": "clusterSide", "byFile": {DP2_FILE: {"left": False, "right": True}}}
    key["hand alone"] = {"rule": "rowPairSideOrBlockMirrorOpenEnd", "byFile": {DP2_FILE: {"pair left": False, "pair right": True}}}
    return project_profile.ProjectProfile(real.path, values)


def hand_build(monkeypatch):
    """plan_from_book.rule_inputs and build with a trace on the made up row, the road level stubbed, as key_build of the R337 tests."""
    import argparse
    from shapely.geometry import LineString, box
    import plan_from_past
    from pathlib import Path
    settings = project_profile.load_settings()
    profile = hand_rules()
    numbers = [str(index) for index in range(1, 7)]
    rows = {number: {"locationPlanNumber": number, "typologyRev9": HAND_UNITS[number], "footprintMirrored": "no", "zone": "Zone 2"} for number in numbers}
    faces = {number: box(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0).buffer(0) for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)]), LineString([(-3.0, -10.0), (-3.0, 40.0)])]
    sales = {"1": {"Orientation": "WS"}}
    clusters = [{"code": "Duplex_DP2", "plots": ["2", "3"], "source": "footprint"}]
    monkeypatch.setattr(plan_from_book, "road_level", lambda surfaces, point, snap: (10.0, "road"))
    args = argparse.Namespace(level_profile=None, overrides=None, override_source=None, override_model=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales, clusters)
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
            "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((Path(__file__).resolve().parents[2] / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    files = {"DP2": DP2_FILE, "VD2": VD2_FILE}
    trace = {}
    plan_from_book.build(numbers, rows, {}, faces, roads, clusters, profile, settings, {"units": {}}, files, "folder", site, [("road", None)], outlines, "N1", sales,
                         True, rules, trace=trace)
    return plan_from_book.hand_step(numbers + ["7"], rows, rules, trace, files, profile, settings.get("majority")), trace


def test_r357_the_hand_of_a_plot_is_the_table_of_its_file_read_by_the_factor_of_its_unit_class(monkeypatch):
    step, trace = hand_build(monkeypatch)
    assert step["1"] == {"unit": "VD2", "kind": "villa", "pairKind": "villa", "unitClass": "villa", "cluster": None, "clusterCode": None, "clusterSide": None,
                         "clusterRole": None, "clusterIndex": None, "clusterUnits": None, "left": [], "right": ["2"], "openEnd": "left", "roadSide": "left", "rowPair": None,
                         "blockMirror": "no", "blockMirrorRoadSide": "no left", "blockMirrorOpenEnd": "no left", "rowPairSideOrBlockMirrorOpenEnd": "alone no left",
                         "handRule": "blockMirrorRoadSide", "factor": "no left", "linkFile": VD2_FILE, "mirrored": True}
    assert step["2"]["unitClass"] == "cluster" and step["2"]["cluster"] == ["2", "3"] and step["2"]["clusterSide"] == "left" and step["2"]["clusterRole"] == "pair"
    assert (step["2"]["clusterIndex"], step["2"]["clusterUnits"], step["2"]["mirrored"]) == (1, 2, False)
    assert (step["3"]["clusterSide"], step["3"]["clusterIndex"], step["3"]["mirrored"], step["3"]["factor"]) == ("right", 2, True, "right")
    # The units in no cluster pair along their row from its right end: 6 and 5 a pair, 4 alone on the left, the side of 4 its pair side right
    assert {number: step[number]["rowPair"] for number in ("4", "5", "6")} == {"4": {"left": 0, "right": 2, "length": 3, "side": "right"},
                                                                                "5": {"left": 1, "right": 1, "length": 3, "side": "left"},
                                                                                "6": {"left": 2, "right": 0, "length": 3, "side": "right"}}
    assert [step[number]["mirrored"] for number in ("4", "5", "6")] == [True, False, True]
    assert step["6"]["openEnd"] == "right" and step["4"]["roadSide"] == "none" and step["4"]["left"] == ["3"] and step["4"]["right"] == ["5"]
    # Plot 7 holds no row and no face: no unit, no hand, every field of what it lacks null
    assert step["7"]["unit"] is None and step["7"]["mirrored"] is None and step["7"]["handRule"] is None and step["7"]["unitClass"] == "alone"
    assert trace["hands"]["4"] == {"mirrored": True, "rule": "rowPairSideOrBlockMirrorOpenEnd"}


def test_r357_a_hand_of_the_step_that_differs_from_the_hand_the_build_took_is_refused(monkeypatch):
    import argparse
    step, trace = hand_build(monkeypatch)
    trace["hands"]["4"] = {"mirrored": False, "rule": "rowPairSideOrBlockMirrorOpenEnd"}
    rules = {"keyRules": hand_rules().get("levelProfiles")["parsons2024Rev6"]["keyRules"], "neighbours": {}, "rowPairs": {}, "openEnds": {}, "roadSides": {},
             "pairs": {"4": "right"}, "kinds": {"4": "duplex"}}
    with pytest.raises(ValueError) as error:
        plan_from_book.hand_step(["4"], {"4": {"typologyRev9": "DP2"}}, rules, trace, {"DP2": DP2_FILE}, hand_rules(), project_profile.load_settings().get("majority"))
    assert str(error.value) == "the step hands gives plot 4 the hand True by rowPairSideOrBlockMirrorOpenEnd and the build took False by rowPairSideOrBlockMirrorOpenEnd (R357)"


def test_r357_the_config_names_the_fields_of_the_step_hands_all_exact():
    fields = project_profile.load_settings().get("addin")["parity"]["steps"]["hands"]
    assert list(fields) == ["unit", "kind", "pairKind", "unitClass", "cluster", "clusterCode", "clusterSide", "clusterRole", "clusterIndex", "clusterUnits", "left", "right",
                            "openEnd", "roadSide", "rowPair", "blockMirror", "blockMirrorRoadSide", "blockMirrorOpenEnd", "rowPairSideOrBlockMirrorOpenEnd", "handRule",
                            "factor", "linkFile", "mirrored"]
    assert all(field == {"kind": "noGeometry"} for field in fields.values())


def test_r357_a_side_neighbour_shares_side_share_of_the_depth_by_its_band_and_takes_its_side_by_the_centroids_along_the_front():
    import majority
    faces = {"1": box(0, 0, 12, 25).buffer(0), "2": box(12, 0, 24, 12.45).buffer(0), "3": box(-12, 0, 0, 12.39).buffer(0)}
    # The band lengths shapely gives, which BuildStepTests.cs holds the C# to
    assert faces["1"].boundary.intersection(faces["2"].buffer(0.05)).length == 12.55
    assert faces["1"].boundary.intersection(box(12, 0, 24, 25).buffer(0).buffer(0.05)).length == 25.1
    assert faces["1"].boundary.intersection(box(0, 25, 12, 37.4).buffer(0).buffer(0.05)).length == 12.100000000000001
    assert box(0, 0, 12, 40).buffer(0).boundary.intersection(box(12.04, 30, 14, 31).buffer(0).buffer(0.05)).length == 1.059800209322752
    frames = {"1": {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (12.0, 0.0)}}
    found = majority.side_neighbours(["1", "2", "3"], faces, frames, 0.5, 25.0)
    assert found["1"] == {"left": [], "right": ["2"], "framed": True} and found["2"] == {"left": [], "right": [], "framed": False}


def test_r357_the_prototype_file_of_a_code_is_the_one_of_the_folder_else_that_of_its_keo_file_and_a_code_of_two_files_gets_none():
    import plan_from_cad
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    files, twice = plan_from_cad.prototype_files(["RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", "RRE-WP-NSP-KEO-arc-dp2-ZZ-MOD-2200.rvt", "notes.txt",
                                                   "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt", "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3101.rvt"], profile)
    assert files["DP1"] == "RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt" and files["DP2"] == "RRE-WP-NSP-KEO-arc-dp2-ZZ-MOD-2200.rvt"
    assert files["VD1"] == files["DP1"] and "TH1" not in files and "DT1" not in files and twice == ["TH1"]


def test_r356_the_link_of_a_plot_is_the_fit_of_its_code_from_the_anchor_corner_of_its_front_in_the_link_axes_of_its_hand(monkeypatch):
    hands, trace = hand_build(monkeypatch)
    key = hand_rules().get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    rows = {number: {"typologyRev9": HAND_UNITS[number]} for number in HAND_UNITS}
    step = plan_from_book.link_step([str(index) for index in range(1, 8)], rows, trace, hands, key, {"DP2": DP2_FILE, "VD2": VD2_FILE}, hand_rules())
    fit = key["linkOrigin"]["byCode"]["DP2"]
    # 4 mirrored: the link X axis runs west, the end anchor the left end of the front seen from the road, (36, 0)
    assert step["4"]["prototypeFile"] == DP2_FILE and step["4"]["fitUnit"] == "DP2" and step["4"]["anchor"] == fit["anchor"] and step["4"]["offset"] == fit["offset"]
    assert step["4"]["mirrored"] is True and step["4"]["insert"] == [36.0 - fit["offset"][0], fit["offset"][1]] and step["4"]["rotationDeg"] == 180.0
    # 5 not mirrored: the X axis runs east, the end anchor the right end, (60, 0), the rotation the 0.0 Python % gives minus zero
    assert step["5"]["mirrored"] is False and step["5"]["insert"] == [60.0 + fit["offset"][0], fit["offset"][1]] and step["5"]["rotationDeg"] == 0.0
    assert step["4"]["lacks"] == [] and step["7"] == {"prototypeFile": None, "fitUnit": None, "anchor": None, "offset": None, "mirrored": None, "insert": None,
                                                      "rotationDeg": None, "lacks": []}


def test_r356_a_plot_with_no_hand_no_fit_no_face_or_no_file_lacks_its_link_with_why(monkeypatch):
    hands, trace = hand_build(monkeypatch)
    key = hand_rules().get("levelProfiles")["parsons2024Rev6"]["keyRules"]
    rows = {"4": {"typologyRev9": "DP2"}, "5": {"typologyRev9": "XX9"}, "6": {"typologyRev9": "DP2"}}
    hands = dict(hands, **{"4": dict(hands["4"], mirrored=None)})
    trace["front"]["6"] = dict(trace["front"]["6"], face=None)
    step = plan_from_book.link_step(["4", "5", "6"], rows, trace, hands, key, {"VD2": VD2_FILE}, hand_rules())
    assert step["4"]["lacks"] == [{"key": "prototypeFile", "why": "no single prototype file in the folder carries the unit DP2"},
                                  {"key": "mirrored", "why": "no rule gives the mirror flag of DP2 in no cluster"}]
    assert step["5"]["lacks"][-1] == {"key": "insert", "why": "plotFrame holds no anchor and offset for XX9"} and step["5"]["fitUnit"] is None
    assert step["6"]["lacks"][-1] == {"key": "transform", "why": "no baseplan face carries the label of the plot"} and step["6"]["insert"] is None


def test_r356_the_rotation_of_a_link_is_written_as_the_plan_writes_it_and_one_that_rounds_to_360_as_0():
    import plan_from_past
    settings = project_profile.load_settings()
    made = {"1": {"rotationDeg": 359.9996, "insert": [1.00049, 2.0]}, "2": {"rotationDeg": None, "insert": None}}
    entries = plan_from_book.step_entries(settings, "linkFrame", made, {"rotationDeg": plan_from_past.plan_rotation})
    assert entries["1"]["written"]["rotationDeg"] == 0 and entries["1"]["raw"]["rotationDeg"] == 359.9996 and entries["1"]["written"]["insert"] == [1.0, 2.0]
    assert entries["2"]["written"]["rotationDeg"] is None


def test_r356_the_config_names_the_fields_of_the_step_link_frame_by_their_kind():
    fields = project_profile.load_settings().get("addin")["parity"]["steps"]["linkFrame"]
    assert {name: (field["kind"], field.get("decimals")) for name, field in fields.items()} == {
        "prototypeFile": ("noGeometry", None), "fitUnit": ("noGeometry", None), "anchor": ("noGeometry", None), "offset": ("points", 3), "mirrored": ("noGeometry", None),
        "insert": ("points", 3), "rotationDeg": ("angles", 3), "lacks": ("noGeometry", None)}
