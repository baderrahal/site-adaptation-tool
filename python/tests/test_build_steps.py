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
    path.write_text(json.dumps(HELD), encoding="utf-8")
    assert plan_from_book.analysis_clusters(path, settings, READ) == ANALYSIS["clusters"]
    for content, why in (({"source": DRAWINGS["villaBlocks"], "clusters": []}, "is of " + DRAWINGS["villaBlocks"]),
                         ({"clusters": []}, "is of None"), ({"source": DRAWINGS["baseplan"]}, "holds no clusters list"),
                         ({"source": DRAWINGS["baseplan"].swapcase(), "clusters": []}, "is of " + DRAWINGS["baseplan"].swapcase())):
        path.write_text(json.dumps(content), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.analysis_clusters(path, settings, READ)
        assert why in str(error.value) and "(R354)" in str(error.value)


# B1 of the review of PR 55. The baseplan read of the made up faces with a type, the footprint blocks with a rotation, and an analysis made
# from them, which records no hash or date of its drawings, only their name (Q089)
READ = [dict(plot_shape(str(100 + int(number)), vertices), type="DP2") for number, vertices in {**ROW, **APART}.items()]
TURNED = [dict(block, rotationDeg=turn) for block, turn in zip(BLOCKS, (0.0, 90.0, 0.0, 180.0, 180.0, 0.0))]
HELD = dict(ANALYSIS, summary={"plotFaces": 8}, plots=[{"plot": "101", "cadType": "DP2", "block": "Duplex_DP2", "rotation": 0.0},
                                                       {"plot": "103", "cadType": "DP2", "block": "Duplex_DP2", "rotation": 90.0},
                                                       {"plot": "105", "cadType": "DP2", "block": "Villa_VD2", "rotation": 359.9996},
                                                       {"plot": "106", "cadType": "DP2", "block": None, "rotation": None}])


def held(**changes):
    plots = [dict(record, **changes.pop(record["plot"], {})) for record in HELD["plots"]]
    return dict(HELD, plots=plots + changes.pop("more", []), **changes)


def test_r354_an_analysis_is_held_to_the_reads_it_was_made_from_by_its_face_count_the_type_of_each_label_and_the_block_under_it(tmp_path):
    settings = project_profile.load_settings()
    path = tmp_path / "analysis.json"
    path.write_text(json.dumps(HELD), encoding="utf-8")
    assert plan_from_book.analysis_clusters(path, settings, READ, TURNED) == ANALYSIS["clusters"]
    # Within half a unit of the 3 decimals the rotation is written at, by the turn
    path.write_text(json.dumps(held(**{"101": {"rotation": 0.0004}})), encoding="utf-8")
    assert plan_from_book.analysis_clusters(path, settings, READ, TURNED) == ANALYSIS["clusters"]
    cases = (
        (held(summary={}), "analysis.json holds no plots list or no summary.plotFaces, which hold the analysis a build joins to the reads it was made "
                           "from (R354)"),
        ({k: v for k, v in HELD.items() if k != "plots"}, "analysis.json holds no plots list or no summary.plotFaces"),
        (held(summary={"plotFaces": 9}), "analysis.json counts 9 plot faces and the baseplan read holds 8, the analysis a build joins is of the reads it "
                                         "was made from (R354)"),
        (held(**{"103": {"cadType": "VL4"}}, more=[{"plot": "999", "cadType": "DP2"}]),
         "2 plots of analysis.json lie on no face of their label or on one of another type in the baseplan read: 103, 999, the analysis a build "
         "joins is of the reads it was made from (R354)"),
        # Plot 101 of a later drawing whose labels moved: its face lies on the first duplex block, of rotation 0, and the analysis gives 90
        (held(**{"101": {"rotation": 90.0}}), "1 plots of analysis.json lie on no block of their name or on one of another rotation in the footprint "
                                              "read: 101, the analysis a build joins is of the reads it was made from (R354)"),
        (held(**{"101": {"rotation": 0.0006}, "105": {"block": "Duplex_DP2"}}), "2 plots of analysis.json lie on no block of their name or on one of "
                                                                                "another rotation in the footprint read: 101, 105"))
    for content, why in cases:
        path.write_text(json.dumps(content), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.analysis_clusters(path, settings, READ, TURNED)
        assert why in str(error.value)
    # With no footprint read the blocks are not held, the count and the labels are
    path.write_text(json.dumps(held(**{"101": {"rotation": 90.0}})), encoding="utf-8")
    assert plan_from_book.analysis_clusters(path, settings, READ) == ANALYSIS["clusters"]


# B4 of the review of PR 55, the text of R354_every_number_of_a_json_input_is_the_double_or_integer_python_reads_from_its_text of the C#
JSON_TEXT = ('{"a": 1.2345678901234567e-15, "b": 3e-29, "c": -0.0, "d": 1e30, "e": 8e28, "f": [0.1, 2264.253152, 1E+400, -1e-400, '
             '4.9406564584124654e-324, 2.4703282292062328e-324, 1.7976931348623158e+308], "g": 12345678901234567890123, "h": -0, '
             '"i": 7.9228162514264337593543950336e28, "j": "x\\u00e9\\ud83d\\ude00", "a": 1.2345678901234567e-15}')


def test_r354_each_json_input_is_read_as_its_python_reader_reads_it_a_duplicate_key_a_byte_order_mark_and_long_integers(tmp_path):
    """P2 of the review of PR 55: what the C# test of the same name holds PyMath.ReadJson to, as the Python readers give it. Config, the
    rules profile and the footprint blocks through project_profile.read_object, the other reads through json.loads."""
    path = tmp_path / "project.json"
    path.write_text('{"a": {"b": 1, "b": 2}}', encoding="utf-8")
    with pytest.raises(project_profile.ProfileError) as error:
        project_profile.read_object(path)
    assert str(error.value) == "project.json has a duplicate key b"
    assert json.loads(path.read_text(encoding="utf-8"))["a"]["b"] == 2
    path.write_bytes(b"\xef\xbb\xbf" + b'{"a": 1}')
    assert project_profile.read_object(path) == {"a": 1}
    with pytest.raises(ValueError, match="Unexpected UTF-8 BOM"):
        json.loads(path.read_text(encoding="utf-8"))
    wide = json.loads("[" + "7" * 4300 + ", -" + "7" * 4300 + "]")
    assert len(str(wide[0])) == 4300 and isinstance(wide[1], int)
    with pytest.raises(ValueError, match="Exceeds the limit \\(4300 digits\\) for integer string conversion: value has 4301 digits"):
        json.loads("[-" + "7" * 4301 + "]")
    assert float(2 ** 70 + 2 ** 17 + 1) == 1180591620717411565568.0 and float(-(2 ** 70 + 2 ** 17 + 1)) == -1180591620717411565568.0
    with pytest.raises(OverflowError, match="int too large to convert to float"):
        float(10 ** 400)


def test_r354_every_number_of_a_json_input_is_the_double_or_integer_python_reads_from_its_text():
    """The bits the C# test holds PyMath.ReadJson to, as json.loads gives them here, and the texts it refuses as json.loads refuses them."""
    import struct
    read = json.loads(JSON_TEXT)
    def bits(value):
        return struct.pack(">d", value).hex()
    assert list(read) == list("abcdefghij")
    assert [bits(read[name]) for name in "abcde"] == ["3cd63d70819bfe6b", "3a0303c73efd0676", "8000000000000000", "46293e5939a08cea", "45f027e72f1f1281"]
    assert [bits(value) for value in read["f"]] == ["3fb999999999999a", "40a1b0819d2391d5", "7ff0000000000000", "8000000000000000", "0000000000000001",
                                                    "0000000000000001", "7fefffffffffffff"]
    assert read["g"] == 12345678901234567890123 and read["h"] == 0 and isinstance(read["h"], int) and bits(read["i"]) == "45f0000000000000"
    # The surrogate pair of the text is one character to Python, two UTF-16 units of the same character to C#
    assert read["j"] == "x\u00e9\U0001f600"
    for text in ('{"a": 1,}', '{"a": 01}', '// a note\n{}', "{'a': 1}", "[1] 2", '{"a": "x\ty"}', "", '{"a": .5}', '{"a": 1.}', '{"a": +1}'):
        with pytest.raises(ValueError):
            json.loads(text)


def test_r345_a_run_that_fails_after_the_step_clusters_leaves_no_step_file(tmp_path, monkeypatch, capsys):
    """B5 of the review of PR 55: the faces and clusters steps were written before the build, which can still fail, so a failed run left
    step files that read as a run done. A run that fails leaves none, as the worker writes its outputs all or none (R347)."""
    import test_round_addin

    def failing(*args, **named):
        raise ValueError("a made up failure of the build after the step clusters")

    steps = tmp_path / "steps"
    # run_book stubs the readers and the build, the build here fails once the steps faces and clusters are made, the files the step
    # inputs hash made up here
    (tmp_path / "r").mkdir()
    for name in ("p.csv", "b.json", "s.xlsx", "r/pieces.json"):
        (tmp_path / name).write_text("made up", encoding="utf-8")
    code, _ = test_round_addin.run_book(tmp_path, monkeypatch, ["--steps", str(steps), "--plots-csv", str(tmp_path / "p.csv"), "--baseplan", str(tmp_path / "b.json"),
                                                                "--sales", str(tmp_path / "s.xlsx"), "--roads", str(tmp_path / "r")], build=failing)
    assert code == 2 and "a made up failure of the build after the step clusters" in capsys.readouterr().err
    assert sorted(path.name for path in steps.iterdir()) == []


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


# P3 of the review of PR 55: a keyRules value of the wrong shape, which need() of check_key_rules let through as a list, the C# then reading
# it as empty or as no hand, refused with one line in both languages (R354_a_key_rule_of_the_wrong_shape_is_refused_with_the_line_of_python)
SHAPES = (
    (("rampSlope", "table"), ["C10"], "keyRules rampSlope holds table as a list, a build takes it as an object (R337)"),
    (("linkOrigin", "byCode"), [], "keyRules linkOrigin holds byCode as a list, a build takes it as an object (R337)"),
    (("yardOutline", "groups"), {}, "keyRules yardOutline holds groups as an object, a build takes it as a list (R337)"),
    (("parameter CASE", "table"), "x", "keyRules parameter CASE holds table as a text, a build takes it as an object (R337)"),
    (("hand villa", "byFile"), {"made-up.rvt": [True]}, "keyRules hand villa holds byFile made-up.rvt as a list, a build takes it as an object of factor values (R337)"),
    (("hand cluster", "byFile"), {"made-up.rvt": None}, "keyRules hand cluster holds byFile made-up.rvt as null, a build takes it as an object of factor values (R337)"))


def test_r354_a_key_rule_of_the_wrong_shape_is_refused_with_the_line_of_csharp():
    import copy
    import plan_rules
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    key = real.values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
    plan_rules.check_key_rules(key)
    for (word, field), value, line in SHAPES:
        changed = copy.deepcopy(key)
        changed[word][field] = value
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.check_key_rules(changed)
        assert str(error.value) == line, word


# F4 of the third review of PR 55: the lines of a key rule of another kind than a build reads, the same in both languages, Python str()
# of a list in the C# (SiteClusters.PyText) and no AttributeError of a parameter or level word given as a text
PARAMETER_RULES = ("plot, rev9Code, plotAndCode, plotCodeStepsAndFacade, ffl, fflDownToTheCm, all, typology, mirroredFactor, typologyMirrored, steps, "
                   "typologySteps")
KIND_LINES = (
    (("linkRotation", "rule"), ["frontRule"], "keyRules linkRotation codes ['frontRule'], a build gives only frontRule (R337)"),
    (("linkRotation", "rule"), {"a": "it's"}, "keyRules linkRotation codes {'a': \"it's\"}, a build gives only frontRule (R337)"),
    (("rampSlope", "table", "C10"), ["0.5"], "the slope of El @ Entry and the ramp class of keyRules differ for C10 (rampSlopePercent 2.0, keyRules.rampSlope "
                                             "['0.5']), give both one slope (R337)"),
    (("parameter CASE",), "x", "keyRules parameter CASE codes None, a build gives only " + PARAMETER_RULES + " (R337)"),
    (("entry",), "x", "keyRules entry codes None, a build gives only roadLevel (R337)"))


def test_r337_a_key_rule_of_another_kind_is_refused_with_one_line_the_same_in_csharp():
    import copy
    import plan_rules
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    for path, value, line in KIND_LINES:
        values = copy.deepcopy(real.values)
        place = values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
        for key in path[:-1]:
            place = place[key]
        place[path[-1]] = value
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.key_rules_of(values["levelProfiles"]["parsons2024Rev6"])
        assert str(error.value) == line, path


# F1 of the third review of PR 55: each fit of keyRules.linkOrigin.byCode in the shape a build reads it, every code of the table in the
# scope or not, refused with one line in both languages (R356_a_link_fit_of_the_wrong_shape_or_kind_is_refused_with_the_line_of_python)
FIT_LINES = (
    ("DP2", {"offset": [1.0, 2.0]}, "keyRules linkOrigin byCode DP2 holds anchor None, a build takes start or end (R337)"),
    ("DP2", [], "keyRules linkOrigin holds byCode DP2 as a list, a build takes it as an object of anchor and offset (R337)"),
    ("DP2", None, "keyRules linkOrigin holds byCode DP2 as null, a build takes it as an object of anchor and offset (R337)"),
    ("ZZ9", {"anchor": "middle", "offset": [1.0, 2.0]}, "keyRules linkOrigin byCode ZZ9 holds anchor middle, a build takes start or end (R337)"),
    ("DP2", {"anchor": "start", "offset": ["1.0", 2.0]}, "keyRules linkOrigin byCode DP2 holds offset ['1.0', 2.0], a build takes a list of two numbers (R337)"),
    ("DP2", {"anchor": "start", "offset": [1.0]}, "keyRules linkOrigin byCode DP2 holds offset [1.0], a build takes a list of two numbers (R337)"),
    ("DP2", {"anchor": "end", "offset": [True, 1]}, "keyRules linkOrigin byCode DP2 holds offset [True, 1], a build takes a list of two numbers (R337)"))


def test_r356_a_link_fit_of_the_wrong_shape_or_kind_is_refused_with_the_line_of_csharp():
    import copy
    import plan_rules
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    key = real.values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
    assert len(plan_rules.key_frame_section(key)["units"]) == len(key["linkOrigin"]["byCode"])
    for code, fit, line in FIT_LINES:
        changed = copy.deepcopy(key)
        changed["linkOrigin"]["byCode"][code] = fit
        for check in (plan_rules.check_key_rules, plan_rules.key_frame_section):
            with pytest.raises(project_profile.ProfileError) as error:
                check(changed)
            assert str(error.value) == line, (code, fit)


# F2 of the third review of PR 55: a hand of a byFile table that is no true or false, taken as truthy on both sides, so the text false
# mirrored the link, refused with one line (R357_a_hand_of_a_byfile_table_that_is_no_true_or_false_is_refused_with_the_line_of_python)
LEAF_LINES = (("false", "a text"), (1, "a number"), (None, "null"), ([True], "a list"))


def test_r357_a_hand_of_a_byfile_table_that_is_no_true_or_false_is_refused_with_the_line_of_csharp():
    import copy
    import plan_rules
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    key = real.values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
    name = sorted(key["hand villa"]["byFile"])[0]
    factor = sorted(key["hand villa"]["byFile"][name])[0]
    for value, kind in LEAF_LINES:
        changed = copy.deepcopy(key)
        changed["hand villa"]["byFile"][name][factor] = value
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.check_key_rules(changed)
        assert str(error.value) == "keyRules hand villa holds byFile %s %s as %s, a build takes true or false (R337)" % (name, factor, kind)


# F3 of the third review of PR 55: a setting a build reads as a number given as a text, true or false or null, which the C# took as a
# number and Python stopped on with TypeError, refused with one line (R355_a_setting_due_as_a_number_is_refused_with_the_line_of_python)
NUMBER_LINES = (
    (("siteBuild", "corner", "roadEdgeM"), "1.0", "config siteBuild.corner.roadEdgeM holds a text, a build takes it as a number (R355)"),
    (("analysis", "toleranceM"), True, "config analysis.toleranceM holds true or false, a build takes it as a number (R355)"),
    (("analysis", "partyWallShare"), None, "config analysis.partyWallShare holds null, a build takes it as a number (R355)"))


def test_r355_a_setting_due_as_a_number_is_refused_with_the_line_of_csharp(tmp_path):
    import copy
    import plan_rules
    real = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    plan_from_book.number_settings(project_profile.load_settings())
    for path, value, line in NUMBER_LINES:
        changed = copy.deepcopy(real)
        place = changed
        for key in path[:-1]:
            place = place[key]
        place[path[-1]] = value
        (tmp_path / "project.json").write_text(json.dumps(changed), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.number_settings(project_profile.load_settings(tmp_path / "project.json"))
        assert str(error.value) == line, path
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    for slope, kind in (("2.0", "a text"), (False, "true or false")):
        lp = copy.deepcopy(profile.values["levelProfiles"]["parsons2024Rev6"])
        lp["rampSlopePercent"]["C10"] = slope
        with pytest.raises(project_profile.ProfileError) as error:
            plan_rules.check_ramp_slopes(lp["keyRules"], lp)
        assert str(error.value) == "rampSlopePercent C10 of the level profile holds %s, a build takes it as a number (R337)" % kind


def test_r345_a_csv_of_the_data_book_with_a_byte_that_is_no_utf8_is_refused_as_the_csharp_refuses_it(tmp_path):
    """F5 of the third review of PR 55: book_rows and nbh_plots read plots.csv and labels.csv as utf-8-sig, so a byte that is no UTF-8 such as
    0xE9 is refused, where CsvRows of the C# took it as U+FFFD (R345_a_csv_of_the_data_book_with_a_byte_that_is_no_utf8_is_refused)."""
    path = tmp_path / "plots.csv"
    path.write_bytes(b"\xef\xbb\xbflocationPlanNumber,baseplanLabel,typologyRev9\n1,101,DP\xe9\n")
    with pytest.raises(UnicodeDecodeError):
        plan_from_book.book_rows(path)
    labels = tmp_path / "labels.csv"
    labels.write_bytes(b"kind,number,nbh\nclientCode,1,0\xe912\n")
    with pytest.raises(UnicodeDecodeError):
        plan_from_book.nbh_plots(labels, None)
    path.write_bytes(b"\xef\xbb\xbflocationPlanNumber,baseplanLabel,typologyRev9\n1,101,DP2\n")
    assert plan_from_book.book_rows(path)[0]["1"]["typologyRev9"] == "DP2"


def test_r337_shape_name_names_each_kind_of_a_json_value_as_the_csharp_names_it():
    """F6 of the third review of PR 55: the words of plan_rules.shape_name, which BuildRules.ShapeName gives the same
    (R337_shape_name_names_each_kind_of_a_json_value_as_python_names_it)."""
    import plan_rules
    assert [plan_rules.shape_name(value) for value in ({}, [], "x", True, False, 1, 1.5, None)] == [
        "an object", "a list", "a text", "true or false", "true or false", "a number", "a number", "null"]
    assert plan_rules.shape_name(dict()) == plan_rules.shape_name(plan_rules.KEY_SHAPES["table"]())


# G3 of the fourth review of PR 55: the areas tolerance of addin.parity held as AddinConfig.ParityTolerance holds it, with its lines
# (R345_the_areas_tolerance_is_held_as_python_holds_it)
AREA_LINES = (
    (True, "addin.parity.kinds.areas is true, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
    (float("nan"), "addin.parity.kinds.areas is NaN, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
    (float("inf"), "addin.parity.kinds.areas is Infinity, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
    (-1, "addin.parity.kinds.areas is -1, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
    ("x", "addin.parity.kinds.areas is \"x\", a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)"),
    (None, "addin.parity.kinds.areas is null, the tolerance of the baseplan faces (R345)"))


def test_r345_the_areas_tolerance_is_held_as_the_csharp_holds_it(tmp_path):
    real = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    assert plan_from_book.area_tolerance(project_profile.load_settings()) == real["addin"]["parity"]["kinds"]["areas"]
    path = tmp_path / "project.json"
    for value, line in AREA_LINES:
        real["addin"]["parity"]["kinds"]["areas"] = value
        path.write_text(json.dumps(real), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.area_tolerance(project_profile.load_settings(path))
        assert str(error.value) == line, value
    del real["addin"]["parity"]["kinds"]["areas"]
    path.write_text(json.dumps(real), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError) as error:
        plan_from_book.area_tolerance(project_profile.load_settings(path))
    assert str(error.value) == "addin.parity.kinds names no kind areas (R345)"


# G2 of the fourth review of PR 55: the CSV files of the data book read the same on both sides, by csv of Python with newline="" and
# strict, a line end in a quoted field kept as it is, an unclosed quote, a field past the limit of csv and a text after a closing quote
# refused with the line of csv (R345_a_csv_of_the_data_book_is_read_as_python_reads_it_with_newline_and_strict)
CSV_HEAD = "locationPlanNumber,baseplanLabel,typologyRev9"
CSV_KEPT = ((CSV_HEAD + '\r\n1,101,"D\r\nP"\r\n2,102,DP2\r\n', "D\r\nP"), (CSV_HEAD + '\n1,101,"D\rP"\n2,102,DP2\n', "D\rP"))
CSV_REFUSED = ((CSV_HEAD + '\n1,101,"DP2\n2,102,DP2\n3,103,DP2\n', "unexpected end of data"),
               (CSV_HEAD + "\n1,101," + "D" * 131073 + "\n", "field larger than field limit (131072)"),
               (CSV_HEAD + '\n1,101,"DP"2\n', "',' expected after '\"'"))


def test_r345_a_csv_of_the_data_book_is_read_as_the_csharp_reads_it_with_newline_and_strict(tmp_path):
    path = tmp_path / "plots.csv"
    for text, value in CSV_KEPT:
        path.write_bytes(text.encode("utf-8"))
        rows, _ = plan_from_book.book_rows(path)
        assert rows["1"]["typologyRev9"] == value and rows["2"]["typologyRev9"] == "DP2"
    path.write_bytes((CSV_HEAD + "\n1,101," + "D" * 131072 + "\n").encode("utf-8"))
    assert len(plan_from_book.book_rows(path)[0]["1"]["typologyRev9"]) == 131072
    for text, why in CSV_REFUSED:
        path.write_bytes(text.encode("utf-8"))
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.book_rows(path)
        assert str(error.value) == "plots.csv is no CSV as Python reads it: %s (R345)" % why


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


# The line both languages refuse a road layout read with no curb line with, R355_the_worker_build_writes_the_step_fronts of the C#
NO_CURB = "the road layout read holds no line of the curb layers analysis.roadEdgeLayers names, the front of a plot is the edge nearest a curb (R355)"


def test_r355_a_road_layout_read_with_no_curb_line_is_refused_with_the_line_of_csharp(tmp_path, monkeypatch, capsys):
    """C3 of the review of PR 55: Python had no test of this refusal, which the C# test holds by the same line."""
    import test_round_addin
    code, _ = test_round_addin.run_book(tmp_path, monkeypatch, [], edges=lambda folder, layers: [])
    assert code == 2
    assert capsys.readouterr().err == "plan_from_book: " + NO_CURB + chr(10)


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


MAJORITY_LINES = (
    ("pairKinds", "config holds no majority.pairKinds list of texts, the unit kinds that pair in a row of units (R357)"),
    ("kindByPrefix", "config holds no majority.kindByPrefix object of texts, the kind of a unit code the rules profile gives none (R357)"),
    ("sideShare", "config holds no majority.sideShare number, the share of the depth a side neighbour shares (R357)"))


def test_r357_a_missing_majority_list_or_link_fit_is_refused_with_the_line_of_csharp_and_never_read_as_empty(monkeypatch, tmp_path):
    """B3 of the review of PR 55: the C# read a missing majority.pairKinds, kindByPrefix or keyRules.linkOrigin.byCode as empty where
    Python raised KeyError, so both refuse it with one line."""
    import argparse
    import plan_rules
    from shapely.geometry import LineString, box
    block = project_profile.load_settings().get("majority")
    assert plan_from_book.hand_settings(block) is block
    for drop, line in MAJORITY_LINES:
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.hand_settings({key: value for key, value in block.items() if key != drop})
        assert str(error.value) == line
    for wrong, line in ((dict(block, pairKinds=[1]), MAJORITY_LINES[0][1]), (dict(block, kindByPrefix={"DP": 2}), MAJORITY_LINES[1][1]),
                        (dict(block, sideShare=True), MAJORITY_LINES[2][1]),
                        (None, "config holds no majority block, the settings of the majority rule a build by keyRules reads (R357)")):
        with pytest.raises(project_profile.ProfileError) as error:
            plan_from_book.hand_settings(wrong)
        assert str(error.value) == line
    # Through rule_inputs of a build by keyRules, on a config with no pairKinds
    real = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    del real["majority"]["pairKinds"]
    path = tmp_path / "project.json"
    path.write_text(json.dumps(real), encoding="utf-8")
    numbers = [str(index) for index in range(1, 7)]
    rows = {number: {"locationPlanNumber": number, "typologyRev9": HAND_UNITS[number], "footprintMirrored": "no", "zone": "Zone 2"} for number in numbers}
    faces = {number: box(index * 12.0, 0.0, index * 12.0 + 12.0, 25.0).buffer(0) for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)])]
    args = argparse.Namespace(level_profile=None, overrides=None, override_source=None, override_model=[], override_fields=None)
    with pytest.raises(project_profile.ProfileError) as error:
        plan_from_book.rule_inputs(args, project_profile.load_settings(path), hand_rules(), numbers, rows, faces, roads, {"1": {"Orientation": "WS"}}, [])
    assert str(error.value) == MAJORITY_LINES[0][1]
    key = json.loads(json.dumps(plan_rules.key_rules_of(plan_rules.level_profile(hand_rules(), None))))
    del key["linkOrigin"]["byCode"]
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.key_frame_section(key)
    assert str(error.value) == "keyRules linkOrigin codes plotFrameRefit, a build gives only plotFrameRefit with byCode (R337)"


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


def test_r357_the_steps_that_take_the_prototype_files_list_every_file_of_the_folder_in_their_inputs_and_a_folder_not_on_disk_is_refused(tmp_path):
    """B2 of the review of PR 55: the C# took the files the request named, Python the whole folder, so both take the whole folder as
    plan_from_book.folder_files lists it, recorded in the inputs of the steps hands and linkFrame, and parity refuses another listing."""
    import argparse
    folder = tmp_path / "prototypes"
    (folder / "sub").mkdir(parents=True)
    for name in ("RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", "RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2101.rvt", "notes.txt"):
        (folder / name).write_text("a made up file " + name, encoding="utf-8")
    labels = tmp_path / "labels.csv"
    labels.write_text("kind,number\n", encoding="utf-8")
    args = argparse.Namespace(settings=str(project_profile.SETTINGS_PATH), baseplan=None, plots_csv=str(labels), labels=str(labels), cover="all",
                              footprint_blocks=None, analysis=None, sales=None, roads=None, landxml=None, road_names=[], prototypes=str(folder))
    settings = project_profile.load_settings()
    for step in ("hands", "linkFrame"):
        listed = [item for item in plan_from_book.build_inputs(args, settings, step) if item["role"].startswith("prototype:")]
        assert [item["role"] for item in listed] == ["prototype:RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt", "prototype:RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2101.rvt",
                                                     "prototype:notes.txt"]
        assert listed[0]["sha256"] == parity.sha256(folder / "RRE-WP-NSP-KEO-ARC-DP1-ZZ-MOD-2100.rvt")
    for step in ("clusters", "fronts"):
        assert not [item for item in plan_from_book.build_inputs(args, settings, step) if item["role"].startswith("prototype:")]
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    # Two files of one code in the folder give it none, whatever a request names
    assert "DP1" not in plan_from_book.folder_files(folder, profile)
    with pytest.raises(project_profile.ProfileError) as error:
        plan_from_book.folder_files(tmp_path / "none", profile)
    assert str(error.value) == "the prototype folder %s of pastJob is not on disk (R357)" % (tmp_path / "none")
    python = {"step": "hands", "run": {"inputs": [{"role": "prototype:a.rvt", "sha256": "1"}, {"role": "prototype:b.rvt", "sha256": "2"}, {"role": "labels", "sha256": "3"}]}}
    csharp = {"step": "hands", "run": {"inputs": [{"role": "prototype:a.rvt", "sha256": "1"}, {"role": "labels", "sha256": "3"}, {"role": "prototype:c.rvt", "sha256": "4"}]}}
    for step in ("hands", "linkFrame"):
        with pytest.raises(ValueError) as error:
            parity.listed_inputs(dict(python, step=step), dict(csharp, step=step))
        assert str(error.value) == "the two step files list other prototype files, only python prototype:b.rvt, only csharp prototype:c.rvt (R357)"
    parity.listed_inputs(python, python)
    parity.listed_inputs({"step": "hands", "run": {"inputs": []}}, {"step": "hands", "run": {"inputs": [{"role": "labels"}]}})
    # A step that takes no prototype file, as the C# stamps every step of a request with the inputs of the request, the prototype files
    # of its steps hands and linkFrame among them, which the Python steps clusters and fronts do not name (the parity of the fix pass)
    for step in ("clusters", "fronts", "faces"):
        parity.listed_inputs({"step": step, "run": {"inputs": []}}, dict(csharp, step=step))


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
