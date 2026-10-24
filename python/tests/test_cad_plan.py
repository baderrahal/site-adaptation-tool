import copy

import numpy as np
import pytest
from shapely.geometry import LineString, Polygon

import cad_plan_check
import plan_compare
import plan_from_cad
import project_profile
import ramp_outline
import rules_checks

PROFILE_PATH = project_profile.profile_path(project_profile.load_settings())
ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "roadEdgeLayers": ["RD-CURB-LINE"]}
FLOORS = {"types": {"generic": ["Generic 150mm"], "yard": ["yard"], "ramp": ["Ramps"]}, "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"}, "plotParameter": "Plot"}
JOB = {"neighbourhood": "XX", "schedule": "s.xlsx", "sheet": "S", "prototypes": "C:\\x\\past.json"}
DOCUMENT = {"surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "angleToTrueNorthDeg": 0.0}


def profile():
    return project_profile.load_profile(PROFILE_PATH)


def small_profile(tmp_path, positions=(2, 3)):
    return project_profile.ProjectProfile(tmp_path / "p.json", {"cluster_max_units": 4, "cluster_split_positions": list(positions)})


def box(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def cluster(*plots, code="M07"):
    return {"code": code, "plots": list(plots)}


def test_r125_a_four_unit_cluster_splits_at_any_listed_wall_counted_from_either_end(tmp_path):
    four = cluster("1", "2", "3", "4")
    for levels, walls, case in (([1.0, 1.2, 1.2, 1.2], [1], "end"), ([1.0, 1.0, 1.2, 1.2], [2], "middle"), ([1.0, 1.0, 1.0, 1.2], [3], "end")):
        issues, records = rules_checks.check_one_ffl([four], dict(zip(four["plots"], levels)), small_profile(tmp_path))
        assert issues == [] and records[0]["status"] == "split allowed"
        assert [wall["after"] for wall in records[0]["walls"]] == walls and records[0]["walls"][0] == {"after": walls[0], "case": case, "doubleWall": True}
    # Under half a millimetre is one level
    issues, records = rules_checks.check_one_ffl([four], dict(zip(four["plots"], [1.0, 1.0004, 1.0, 1.0])), small_profile(tmp_path))
    assert issues == [] and records[0]["status"] == "one FFL"


def test_r125_a_pair_never_splits_and_a_wall_off_the_list_is_an_issue(tmp_path):
    issues, records = rules_checks.check_one_ffl([cluster("1", "2", code="D04")], {"1": 1.0, "2": 1.1}, small_profile(tmp_path))
    assert records[0]["status"] == "split not allowed" and issues[0]["rule"] == "R125" and "a cluster of 2 never splits" in issues[0]["text"]
    # With the middle wall only listed, the wall after unit 1 and after unit 3 are both refused
    only_middle = small_profile(tmp_path, positions=(2,))
    for levels in ([1.0, 1.2, 1.2, 1.2], [1.0, 1.0, 1.0, 1.2]):
        issues, _ = rules_checks.check_one_ffl([cluster("1", "2", "3", "4")], dict(zip("1234", levels)), only_middle)
        assert len(issues) == 1 and "not a split position" in issues[0]["text"]
    _, records = rules_checks.check_one_ffl([cluster("1", "2")], {"1": 1.0}, small_profile(tmp_path))
    assert records[0]["status"] == "no FFL"


def row(plot, unit, entry, ffl=None, sheet_row=1):
    return {"Plot No.": plot, "Unit": unit, "El @ Entry": entry, "FFL": ffl, "sheetRow": sheet_row}


def test_r126_schedule_entries_take_the_unit_and_entry_and_list_a_row_that_lacks_one_or_a_plot_held_twice():
    entries, lacking, bad = plan_from_cad.schedule_entries([row(1, "DP1", 10.0, 10.2), row(2, "DP1", None), row(3, " ", 11.0), row(4, "VL4", 9.0, sheet_row=4), row(4, "VL4", 9.0, sheet_row=5), row("x", "DP1", 1.0)])
    assert entries == {"1": {"unit": "DP1", "entry": 10.0, "sheetFfl": 10.2}}
    assert sorted(item["plot"] for item in lacking) == ["2", "3", "4"] and bad[0]["plotNo"] == "x"


def test_r126_levels_come_from_levels_py_on_official_clusters_and_a_one_step_typology_with_two_steps_is_flagged():
    clusters = plan_from_cad.official_clusters([cluster("1", "2", code="M01")], 10)
    assert clusters[0]["plots"] == ["11", "12"]
    rows = plan_from_cad.plot_levels(profile(), clusters, {"11": {"unit": "DP1", "entry": 10.0}, "12": {"unit": "DP1", "entry": 10.05}, "13": {"unit": "VL4", "entry": 9.0}})
    assert rows["11"]["ffl"] == rows["12"]["ffl"] == pytest.approx(10.15)
    assert rows["13"]["ffl"] == pytest.approx(9.1) and rows["13"]["flags"] == []
    flagged = plan_from_cad.one_step_flags([{"unit": "VL4", "steps": 2, "flags": []}, {"unit": "DP1", "steps": 2, "flags": []}, {"unit": "C10", "steps": None, "flags": []}], profile())
    assert [item["flags"] for item in flagged] == [["more than 1 step"], [], []]


def test_r127_the_setback_comes_from_the_profile_with_its_default_and_the_outline_runs_from_the_front_edge():
    assert ramp_outline.setback(profile(), "C10") == pytest.approx(5.5) and ramp_outline.setback(profile(), "DP1") == pytest.approx(5.52)
    assert ramp_outline.setback(profile(), None) == pytest.approx(5.52)
    frame = {"left": (0.0, 0.0), "right": (10.0, 0.0), "n": (0.0, 1.0)}
    planned = ramp_outline.outline(frame, 5.0)
    assert planned == [(0.0, 0.0), (10.0, 0.0), (10.0, 5.0), (0.0, 5.0)]
    same = ramp_outline.compare_outline(planned, [(10.0, 5.0), (0.0, 0.0), (0.0, 5.0), (10.0, 0.0)])
    assert same["corner"] == pytest.approx(0) and same["depthGap"] == pytest.approx(0) and same["pastDepth"] == pytest.approx(5.0)
    deeper = ramp_outline.compare_outline(planned, [(0.0, 0.0), (10.0, 0.0), (10.0, 5.05), (0.0, 5.05)])
    assert deeper["front"] == pytest.approx(0) and deeper["back"] == pytest.approx(0.05) and ramp_outline.band_of(deeper["depthGap"]) == "within100mm"
    assert [ramp_outline.band_of(value) for value in (0.01, 0.1, 0.2)] == ["within10mm", "within100mm", "worse"]


class RoadBelow:
    """A road surface that covers every point south of a line, as the road surface stops at the curb."""

    def __init__(self, edge):
        self.edge = edge

    def z_at(self, easting, northing):
        return 1.0 if northing <= self.edge + 1e-9 else None


def test_r127_a_ramp_on_the_sidewalk_is_covered_at_its_curb_point_and_not_at_its_corners():
    ramps = [{"plot": "11", "corners": [(0, 0), (10, 0), (10, 5), (0, 5)]}]
    roads = [LineString([(-100, -3), (100, -3)])]
    counts, per_plot = ramp_outline.curb_cover(ramps, roads, RoadBelow(-3))
    assert counts["coveredAtCorner"] == 0 and counts["coveredAtCurb"] == 4 and counts["rampsNoneAtCornerSomeAtCurb"] == 1
    assert per_plot["11"] == {"atCorner": 0, "atCurb": 4}


def test_r127_test_ramps_bands_each_ramp_and_keeps_the_listed_plots_apart():
    faces = {"11": Polygon(box(0, 0, 10, 25)), "12": Polygon(box(10, 0, 20, 25))}
    roads = [LineString([(-100, -5), (100, -5)])]
    ramps = [{"plot": "11", "unit": "DP1", "corners": [(0, 0), (10, 0), (10, 5.52), (0, 5.52)]},
             {"plot": "12", "unit": "DP1", "corners": [(10, 0), (20, 0), (20, 5.0), (10, 5.0)]}]
    rows, counts = ramp_outline.test_ramps(ramps, faces, roads, profile(), ANALYSIS, {"12"})
    assert counts["outside"]["corners"] == {"within10mm": 1} and counts["apart"]["corners"] == {"worse": 1}
    assert rows[0]["setback"] == pytest.approx(5.52) and rows[1]["cutListed"] is True


def pair_inputs():
    faces = {"11": Polygon(box(0, 0, 12, 25)), "12": Polygon(box(12, 0, 22, 25)), "13": Polygon(box(30, 0, 42, 25)), "14": Polygon(box(50, 0, 60, 25))}
    roads = [LineString([(-100, -5), (100, -5)])]
    clusters = [cluster("11", "12", code="M01")]
    entries = {"11": {"unit": "DP1", "entry": 10.0}, "12": {"unit": "DP1", "entry": 10.05}, "13": {"unit": "VL4", "entry": 9.0}, "14": {"unit": "VD1", "entry": 9.5}}
    frame = {"units": {"DP1": {"anchor": "end", "offset": [-10.0, 0.5], "drawnSide": "left"}, "VL4": {"anchor": "end", "offset": [-10.0, 0.5], "drawnSide": None}}}
    listing = {"rre-arc-dp1-zz.rvt", "rre-arc-vl4-zz.rvt"}
    return faces, roads, clusters, entries, frame, listing


def test_r128_the_plan_from_cad_writes_the_cluster_plots_with_rotation_mirror_insert_and_levels_and_lists_the_villas():
    faces, roads, clusters, entries, frame, listing = pair_inputs()
    plan = plan_from_cad.build_plan(entries, faces, roads, clusters, profile(), ANALYSIS, frame, listing, DOCUMENT, JOB, "C:\\folder", FLOORS)
    written = {entry["plot"]: entry for entry in plan["plots"]}
    assert sorted(written) == ["11", "12"] and plan["summary"]["written"] == 2 and plan["summary"]["missingByKey"] == {"mirrored": 2}
    left, right = written["11"], written["12"]
    assert left["mirrored"] == {"value": False, "source": "computed"} and right["mirrored"]["value"] is True
    assert left["rotationDeg"]["value"] == 0 and right["rotationDeg"]["value"] == 180
    assert left["insert"]["value"] == pytest.approx([2.0, 0.5, 10.15]) and right["insert"]["value"] == pytest.approx([22.0, 0.5, 10.15])
    assert left["insert"]["value"][2] == left["levels"]["ffl"]["value"] and left["levels"]["entry"]["source"] == "schedule"
    assert left["unit"] == {"value": "DP1", "source": "schedule"} and left["prototypeFile"]["value"] == "rre-arc-dp1-zz.rvt" and left["floors"] == {}
    assert set(left["parameters"]) == {"RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "RRE_ZZ_SP_Number-of-Steps"} and "case" not in left and "linkName" not in left
    missing = {item["plot"]: [lack["key"] for lack in item["missing"]] for item in plan["missing"]}
    # VD1 takes the file of its keo_file DP1, it still has no mirror flag in no cluster
    assert missing == {"13": ["mirrored"], "14": ["mirrored"]}
    assert plan["summary"]["check3"]["byStatus"] == {"one FFL": 1} and plan["format"] == "site-adaptation-plan"


def test_r128_a_unit_with_two_files_or_no_file_gets_none_and_the_keo_file_stands_in_for_a_unit_without_one():
    files, twice = plan_from_cad.prototype_files({"a-ARC-DP1-1.rvt", "b-ARC-DP1-2.rvt", "a-ARC-VL4-1.rvt", "readme.txt"}, profile())
    assert twice == ["DP1"] and "DP1" not in files and "VD1" not in files and files["VL4"] == "a-ARC-VL4-1.rvt"
    files, _ = plan_from_cad.prototype_files({"a-ARC-DP1-1.rvt"}, profile())
    assert files["VD1"] == "a-ARC-DP1-1.rvt"


def plan_entry(plot, insert, rotation=0.0, mirrored=False, ffl=10.0, steps=1, unit="DP1"):
    return {"plot": plot, "unit": {"value": unit}, "prototypeFile": {"value": "F.rvt"}, "insert": {"value": list(insert)}, "rotationDeg": {"value": rotation},
            "mirrored": {"value": mirrored}, "levels": {"ffl": {"value": ffl}, "ssl": {"value": ffl - 0.075}, "entry": {"value": ffl - 0.2}, "height": {"value": 0.2}, "steps": {"value": steps}}}


def test_r129_two_plans_are_compared_field_by_field_in_bands_with_the_listed_plots_apart():
    mine = {"plots": [plan_entry("1", (0, 0, 10.0)), plan_entry("2", (0.0005, 0, 10.004), rotation=90.0), plan_entry("3", (0, 0.5, 10.0), mirrored=True), plan_entry("4", (0, 0, 10.0))]}
    theirs = {"plots": [plan_entry("1", (0, 0, 10.0)), plan_entry("2", (0, 0, 10.0)), plan_entry("3", (0, 0, 10.0)), plan_entry("5", (0, 0, 10.0))]}
    report = plan_compare.compare_plans(mine, theirs, {"3"})
    outside = report["counts"]["outside"]
    assert outside["insertPlan"] == {"equal": 1, "within1mm": 1} and outside["insertZ"] == {"equal": 1, "within10mm": 1}
    assert outside["rotation"] == {"equal": 1, "worse": 1} and outside["mirrored"] == {"equal": 2}
    assert report["counts"]["apart"]["insertPlan"] == {"worse": 1} and report["counts"]["apart"]["mirrored"] == {"worse": 1}
    assert report["onlyInPlan"] == ["4"] and report["onlyInOther"] == ["5"] and report["worse"] == [{"plot": "2", "field": "rotation", "gap": pytest.approx(90.0)}]
    lacking = plan_entry("6", (0, 0, 1.0))
    del lacking["levels"]["ssl"]
    assert plan_compare.plan_gaps(lacking, plan_entry("6", (0, 0, 1.0)))["ssl"] is None


def test_r129_the_compare_refuses_a_call_with_no_model_and_no_plan(tmp_path):
    with pytest.raises(SystemExit):
        plan_compare.main([str(tmp_path / "plan.json")])


def test_r130_the_levels_check_works_the_ffl_out_by_hand_and_lets_a_half_millimetre_go_either_way(tmp_path):
    raw = {"steps": {"base_height": {"default": 100, "DP1": 200}}, "levels": {"ground_ssl": -75}}
    past = {"plots": [plan_entry("11", (0, 0, 0)), plan_entry("12", (0, 0, 0))]}
    past["plots"][0]["levels"]["entry"]["value"] = 10.0
    past["plots"][1]["levels"]["entry"]["value"] = 10.0505
    analysis = {"clusters": [cluster("1", "2")]}
    cad = {"plots": [plan_entry("11", (0, 0, 10.25)), plan_entry("12", (0, 0, 10.25))]}
    for item in cad["plots"]:
        item["levels"]["ffl"]["value"], item["levels"]["ssl"]["value"] = 10.25, 10.175
    results = cad_plan_check.check_levels(cad, past, analysis, raw, 10, 1, 5)
    assert all(item["differs"] == [] and item["halfMillimetre"] for item in results)
    cad["plots"][0]["levels"]["ffl"]["value"] = 10.252
    found = {item["plot"]: item["differs"] for item in cad_plan_check.check_levels(cad, past, analysis, raw, 10, 1, 5)}
    assert found["11"] and found["12"] == []


def test_r130_the_compare_check_counts_again_and_catches_a_planted_count():
    mine = {"plots": [plan_entry("1", (0, 0, 10.0)), plan_entry("2", (0.0005, 0, 10.0))]}
    theirs = {"plots": [plan_entry("1", (0, 0, 10.0)), plan_entry("2", (0, 0, 10.0))]}
    report = plan_compare.compare_plans(mine, theirs, set())
    assert all(item["differs"] == [] for item in cad_plan_check.check_compare(report, mine, theirs, set()))
    planted = copy.deepcopy(report)
    planted["counts"]["outside"]["insertPlan"] = {"equal": 2}
    assert [item["field"] for item in cad_plan_check.check_compare(planted, mine, theirs, set()) if item["differs"]] == ["insertPlan"]


def test_r130_the_ramps_check_pairs_the_corners_in_all_orders_and_asks_every_triangle():
    faces = {"11": Polygon(box(0, 0, 10, 25))}
    roads = [LineString([(-100, -5), (100, -5)])]
    ramp = {"plot": "11", "unit": "DP1", "corners": [(0, 0), (10, 0), (10, 5.52), (0, 5.52)]}
    rows, _ = ramp_outline.test_ramps([ramp], faces, roads, profile(), ANALYSIS, set())
    # Two triangles of a road that covers the band from y -10 to y -4, so the curb at y -5 is on it and the corners are not
    xyz = np.array([[-100.0, -10.0, 1.0], [100.0, -10.0, 1.0], [100.0, -4.0, 1.0], [-100.0, -4.0, 1.0]])
    triangles = np.array([[0, 1, 2], [0, 2, 3]])
    rows[0]["cover"] = {"atCorner": 0, "atCurb": 4}
    document = {"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
                "floors": [{"typeName": "Ramps", "parameters": {"Plot": "DP1-11"}, "corners": [[x, y, 1.0] for x, y in ramp["corners"]]}]}
    plots = [{"kind": "plot_shape", "plot": "1", "vertices": [list(point) for point in box(0, 0, 10, 25)]}]
    pieces = [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-100, -5], [100, -5]]}]
    raw = {"typologies": {"DP1": {"plot_width_m": 10}}, "plot": {"long_side": 25000}, "ramp": {"front_setback": {"default": 5520, "C10": 5500}}}
    settings = {"analysis": ANALYSIS}
    results = cad_plan_check.check_ramps({"rows": rows}, {"documents": [document]}, plots, pieces, raw, settings, {"Ramps"}, (xyz, triangles), 10, 1, 5)
    assert results == [{"plot": "11", "differs": []}]
    rows[0]["cover"] = {"atCorner": 4, "atCurb": 4}
    rows[0]["corner"] = 0.5
    differs = cad_plan_check.check_ramps({"rows": rows}, {"documents": [document]}, plots, pieces, raw, settings, {"Ramps"}, (xyz, triangles), 10, 1, 5)[0]["differs"]
    assert any(text.startswith("corner") for text in differs) and any(text.startswith("cover") for text in differs)
    assert cad_plan_check.best_pairing([(0, 0), (1, 0)], [(1, 0), (0, 0)]) == pytest.approx(0.0)
