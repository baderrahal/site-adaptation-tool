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
    assert refused(real.values, "native2022") == ("the build steps clusters of the parity harness are those of a build by keyRules, the level profile native2022 holds "
                                                  "none, the C# builds by keyRules alone (R354)")
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
