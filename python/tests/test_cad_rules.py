import pytest
from shapely.geometry import LineString, Polygon
from shapely.strtree import STRtree

import cad_links
import cad_rules
import cad_rules_check
import project_profile

ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "roadEdgeLayers": ["RD-CURB-LINE"], "unitPlotTypes": ["VL4", "DP2"]}
RAW_PROFILE = {"typologies": {"VD2": {"plot_width_m": 12}}, "plot": {"long_side": 25000}, "cluster_max_units": 4, "cluster_split_positions": [2, 3],
               "steps": {"base_height": {"default": 100, "DP1": 100, "TH1": 100}, "legend": [[0.1, 1], [0.54, 3]]}}


def box(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def profile():
    return project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


def row_of_plots():
    """Plots 11 to 14 side by side north of a road, 15 stands alone. 14 carries no unit, an open space."""
    faces = {str(10 + index): Polygon(box(12 * (index - 1), 0, 12 * index, 25)) for index in range(1, 5)}
    faces["15"] = Polygon(box(100, 0, 112, 25))
    roads = [LineString([(-100, -5), (200, -5)])]
    frames = {plot: cad_links.front_frame(face, STRtree(roads), roads, (7.99, 18.75)) for plot, face in faces.items()}
    return faces, roads, frames


def test_r131_side_neighbours_carry_a_unit_and_a_row_joins_villas_only():
    faces, _, frames = row_of_plots()
    carriers = {"11", "12", "13", "15"}
    sides = cad_rules.side_neighbours(["11", "12", "13", "15"], faces, frames, carriers, 18.75)
    assert sides["11"] == {"left": [], "right": ["12"]} and sides["12"] == {"left": ["11"], "right": ["13"]}
    assert sides["13"] == {"left": ["12"], "right": []} and sides["15"] == {"left": [], "right": []}
    assert cad_rules.villa_rows({plot: sides[plot] for plot in ("11", "12", "15")}) == [["11", "12"], ["15"]]
    assert cad_rules.one_side(sides["11"]) == "right" and cad_rules.one_side(sides["12"]) is None and cad_rules.one_side(sides["15"]) is None
    plots = [{"kind": "plot_shape", "plot": "4", "type": "OS"}, {"kind": "plot_shape", "plot": "5", "type": "VL4"}]
    assert cad_rules.unit_plots(plots, 10, {"11": "VD2"}, {"VL4"}) == {"11", "15"}


def link(mirrored, case=None, rotation=0.0, unit="VD2"):
    return {"mirrored": mirrored, "case": case, "rotation": rotation, "unit": unit}


def test_r131_a_typology_drawn_the_other_hand_is_measured_and_the_tests_count_each_villa():
    sides = {"1": {"left": [], "right": ["2"]}, "2": {"left": ["1"], "right": ["3"]}, "3": {"left": ["2"], "right": []},
             "4": {"left": ["9"], "right": []}, "5": {"left": [], "right": ["9"]}, "6": {"left": ["9"], "right": ["8"]}}
    units = {"1": "VD2", "2": "VD2", "3": "VD2", "4": "VL6", "5": "VL6", "6": "VD2"}
    # VD2 is mirrored with the neighbour on the left, VL6 the other way round
    links = {"1": link(False), "2": link(False), "3": link(True), "4": link(False, unit="VL6"), "5": link(True, unit="VL6"), "6": link(True)}
    flipped, counts = cad_rules.reversed_villas(links, units, sides, set())
    assert flipped == ["VL6"] and counts == {"VD2": {"same": 2}, "VL6": {"other": 2}}
    assert cad_rules.hand("VL6", False, flipped) is True and cad_rules.hand("VD2", False, flipped) is False
    report = cad_rules.villa_tests(links, units, sides, set(), ["VL6"])
    # Row 1 2 3 has two hands, its ends vote both ways, so 2 is undecided
    assert report["a"]["rows"] == {"breaks": 1, "single": 3} and report["a"]["breaks"] == [["1", "2", "3"]]
    assert report["b"]["counts"] == {"holds": 4} and report["c"] == {"VL6": {"holds": 2}} and report["d"]["VL6"] == {"breaks": 2}
    assert report["full"]["counts"] == {"holds": 4, "undecided": 2} and report["full"]["undecided"] == ["2", "6"] and report["holdsOnEveryVilla"] is False
    # A villa of the cut plots counts nowhere
    assert cad_rules.villa_tests(links, units, sides, {"3"}, [])["a"]["rows"] == {"holds": 1, "single": 3}


def test_r132_each_feature_votes_per_facing_pair_and_the_breaking_links_are_listed():
    links = {"1": link(False, "B"), "2": link(True, "A", rotation=180.0), "3": link(False, "B"), "4": link(True, "A", rotation=180.0), "5": link(False, "A")}
    assert cad_rules.facing_pairs(links) == {"A": "A", "B": "A"}
    features = {"mirrored": lambda plot, item: item["mirrored"], "nothing": lambda plot, item: "XX"}
    report = cad_rules.case_tests(links, features, {"4"})
    assert report["mirrored"]["explained"] == 4 and report["mirrored"]["breaksOutside"] == ["5"] and report["mirrored"]["holdsOnAll"] is False
    assert report["mirrored"]["outside"] == 4 and report["mirrored"]["explainedOutside"] == 3
    assert report["nothing"]["values"] == 1 and report["nothing"]["explained"] == 3


def rows(entries, units=("DP1", "TH1", "TH1", "DP1"), ffls=None):
    return [{"plot": str(index), "unit": unit, "entry": entry, "ffl": (ffls or [0] * len(entries))[index]} for index, (entry, unit) in enumerate(zip(entries, units))]


def test_r133_one_ffl_unless_a_height_would_pass_the_legend_then_the_fewest_allowed_walls():
    assert cad_rules.split_rule(profile(), rows([10.0, 10.1, 10.2, 10.3])) == ([], 0)
    # The highest entry is 0.5 above the lowest, one FFL gives a 0.6 m height, a split after unit 2 brings all inside
    walls, ties = cad_rules.split_rule(profile(), rows([10.0, 10.05, 10.5, 10.45]))
    assert walls == [2] and ties >= 1
    # A pair never splits, so no split brings it inside
    assert cad_rules.split_rule(profile(), rows([10.0, 10.7], units=("DP1", "DP1"))) == (None, 0)
    assert cad_rules.walls_of([1.0, 1.0, 1.2, 1.2]) == [2] and cad_rules.walls_of([1.0, 1.0004]) == []


def test_r133_the_split_test_compares_the_rule_with_the_schedule_walls_per_neighbourhood():
    clusters = [{"code": "M07", "plots": ["1", "2", "3", "4"]}, {"code": "M01", "plots": ["5", "6"]}]
    records = [{"plot": str(10 + index), "unit": unit, "entry": entry, "ffl": ffl} for index, unit, entry, ffl in
               ((1, "DP1", 10.0, 10.15), (2, "TH1", 10.05, 10.15), (3, "TH1", 10.1, 10.3), (4, "DP1", 10.2, 10.3), (5, "DP1", 9.0, 9.1), (6, "DP1", 9.0, 9.1))]
    report = cad_rules.split_tests(profile(), clusters, {"XX": (10, records)})
    assert report["counts"] == {"XX sheet one FFL same": 1, "XX sheet split differs": 1} and report["reproducesEverySplit"] is False
    assert report["missed"][0]["sheetWalls"] == [2] and report["missed"][0]["ruleWalls"] == []
    assert report["piecesAtSheetWalls"] == {"equal": 4}


def test_r120_the_cut_neighbours_are_read_from_the_settings_and_counted_apart(tmp_path):
    good = project_profile.ProjectProfile(tmp_path / "s.json", {"cadLinks": {"cutShareMin": 0.9, "cutNeighbours": ["2374"]}})
    assert cad_links.cad_links_settings(good)["cutNeighbours"] == ["2374"]
    for bad in ([2374], "2374", ["x"]):
        with pytest.raises(project_profile.ProfileError, match="cutNeighbours"):
            cad_links.cad_links_settings(project_profile.ProjectProfile(tmp_path / "s.json", {"cadLinks": {"cutShareMin": 0.9, "cutNeighbours": bad}}))


def test_r134_shared_boundary_by_hand_and_the_split_by_hand_agree_with_the_report():
    assert cad_rules_check.shared_length(box(0, 0, 10, 25), box(10, 5, 20, 30)) == pytest.approx(20.0)
    assert cad_rules_check.shared_length(box(0, 0, 10, 25), box(11, 0, 20, 25)) == pytest.approx(0.0)
    books = {"XX": {"1": {"plot": "1", "unit": "DP1", "entry": 10.0, "ffl": 10.1}, "2": {"plot": "2", "unit": "DP1", "entry": 10.0, "ffl": 10.2}}}
    report = {"split": {"clusters": [{"neighbourhood": "XX", "plots": ["1", "2"], "sheetWalls": [1], "ruleWalls": []}]}}
    assert cad_rules_check.check_split(report, books, RAW_PROFILE, 1, 5) == [{"plots": ["1", "2"], "differs": []}]
    report["split"]["clusters"][0]["ruleWalls"] = [1]
    assert cad_rules_check.check_split(report, books, RAW_PROFILE, 1, 5)[0]["differs"]


def test_r134_the_case_count_by_hand_catches_a_changed_count():
    links = []
    for number, case, rotation, mirrored in (("1", "B", 0.0, False), ("2", "A", 180.0, True), ("3", "A", 0.0, False)):
        links.append({"parameters": {"RRE_ZZ_SP_Plot-Number": number, "CASE": case, "RRE_ZZ_SP_Prototype": "VD2"}, "transform": {"rotationDeg": rotation, "mirrored": mirrored}})
    prototypes = {"documents": [{"kind": "project", "links": links}]}
    # Links 1 and 3 tie in one vote, the lower letter A wins, so 1 breaks
    report = {"apart": [], "case": {"flipped": [], "tests": {"hand": {"explained": 2, "breaksOutside": ["1"]}}}}
    assert cad_rules_check.check_case(report, prototypes) == [{"test": "hand", "differs": []}]
    report["case"]["tests"]["hand"]["explained"] = 3
    assert cad_rules_check.check_case(report, prototypes)[0]["differs"]


def test_r134_the_villa_sides_by_hand_agree_with_the_report_and_catch_a_changed_side():
    plots = [{"kind": "plot_shape", "plot": str(index), "type": "VL4", "vertices": [list(point) for point in box(12 * (index - 1), 0, 12 * index, 25)]} for index in (1, 2)]
    pieces = [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-100, -5], [200, -5]]}]
    analysis = {"plots": [{"plot": "1", "unit": "VD2"}, {"plot": "2", "unit": "VD2"}]}
    prototypes = {"documents": [{"kind": "project", "links": [
        {"parameters": {"RRE_ZZ_SP_Plot-Number": "11"}, "transform": {"mirrored": False}},
        {"parameters": {"RRE_ZZ_SP_Plot-Number": "12"}, "transform": {"mirrored": True}}]}]}
    report = {"apart": [], "villas": {"reversed": [], "b": {"breaks": []}, "sides": {"11": {"left": [], "right": ["12"]}, "12": {"left": ["11"], "right": []}}}}
    results = cad_rules_check.check_villas(report, prototypes, plots, pieces, analysis, RAW_PROFILE, {"analysis": ANALYSIS}, 10, 1, 5)
    assert all(item["differs"] == [] for item in results) and len(results) == 2
    report["villas"]["sides"]["11"] = {"left": ["12"], "right": []}
    report["villas"]["b"]["breaks"] = ["12"]
    found = {item["plot"]: item["differs"] for item in cad_rules_check.check_villas(report, prototypes, plots, pieces, analysis, RAW_PROFILE, {"analysis": ANALYSIS}, 10, 1, 5)}
    assert found["11"] and found["12"] == ["test b differs"]
