import copy

import numpy as np
import pytest
from shapely.geometry import LineString, Polygon

import cad_floor_check
import floor_fit
import plan_compare
import plan_from_cad
import project_profile

ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "roadEdgeLayers": ["RD-CURB-LINE"]}
FLOORS = {"types": {"generic": ["Generic 150mm"], "yard": ["yard"], "ramp": ["Ramps 0.5%", "Ramps 2%"]}, "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1}
JOB = {"neighbourhood": "XX", "schedule": "s.xlsx", "sheet": "S", "prototypes": "C:\\x\\past.json"}
DOCUMENT = {"surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "angleToTrueNorthDeg": 0.0,
            "floors": [{"level": "L1", "levelElevation": 0.0}, {"level": "L2", "levelElevation": 4.0}]}
RAW_PROFILE = {"typologies": {"DP1": {"plot_width_m": 10}}, "plot": {"long_side": 25000}, "ramp": {"front_setback": {"default": 5000}}}
SETTINGS = {"analysis": ANALYSIS, "cadLinks": {"floorWidthStepM": 0.5}}


def box(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def profile():
    return project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


class FlatRoad:
    """A road at level 9.9 everywhere south of y 0."""

    def level_at(self, easting, northing):
        return (9.9, "road") if northing <= 0 else (None, None)


def test_r135_a_point_goes_to_the_link_axes_and_back_and_a_mirrored_link_flips_y():
    points = [(3.0, 4.0), (10.0, -2.0)]
    for rotation, mirrored in ((0.0, False), (30.0, True), (210.0, False)):
        local = floor_fit.to_link(points, (100.0, 50.0), rotation, mirrored)
        assert [value for point in floor_fit.to_world(local, (100.0, 50.0), rotation, mirrored) for value in point] == pytest.approx([value for point in points for value in point])
    assert list(floor_fit.to_link([(0.0, 1.0)], (0.0, 0.0), 0.0, False)[0]) == pytest.approx([0.0, 1.0])
    assert list(floor_fit.to_link([(0.0, 1.0)], (0.0, 0.0), 0.0, True)[0]) == pytest.approx([0.0, -1.0])
    assert floor_fit.width_key(12.24, 0.5) == 12.0 and floor_fit.width_key(12.26, 0.5) == 12.5


def rows_of(shapes, unit="DP1", mirrored=False, width=10.0):
    return [{"plot": str(10 + index), "unit": unit, "mirrored": mirrored, "width": width, "type": "Generic 150mm", "loops": [shape], "shape": Polygon(shape)}
            for index, shape in enumerate(shapes)]


def test_r135_a_group_is_fitted_on_one_half_and_tested_on_the_other_and_a_small_group_is_not_tested():
    rows = rows_of([box(0, 0, 10, 25)] * 5 + [box(0, 0, 10.05, 25)])
    fitted = floor_fit.fit_groups(rows, ("unit", "mirrored", "width"), 13)
    assert len(fitted) == 1 and fitted[0]["tooFew"] is False and fitted[0]["splitSeed"] == 13 and fitted[0]["type"] == "Generic 150mm"
    errors = fitted[0]["fitError"]["n"] + fitted[0]["testError"]["n"]
    assert errors == 6 and fitted[0]["fitError"]["n"] == 3
    assert fitted[0]["fitError"]["worse"] + fitted[0]["testError"]["worse"] == 1 and fitted[0]["loops"] == [[[0, 0], [10, 0], [10, 25], [0, 25]]]
    small = floor_fit.fit_groups(rows_of([box(0, 0, 10, 25)] * 3), ("unit", "mirrored"), 13)
    assert small[0]["tooFew"] is True and "testError" not in small[0]
    assert floor_fit.totals(fitted + small)["tooFewPlots"] == 3
    gaps = floor_fit.plot_gaps(rows, fitted, ("unit", "mirrored", "width"))
    assert sorted(round(row["gap"], 3) for row in gaps) == [0, 0, 0, 0, 0, 0.05]
    assert floor_fit.outline_for({"outlines": {"generic": fitted}}, "generic", "DP1", False, 10.0) is fitted[0]
    assert floor_fit.outline_for({"outlines": {"generic": fitted}}, "generic", "DP1", True, 10.0) is None


def test_r135_the_generic_floor_is_tested_against_its_whole_face_and_the_width_step_is_read_from_the_settings(tmp_path):
    plan = {"plots": [{"plot": "11", "floors": {"generic": {"outline": {"value": {"loops": [{"points": box(0, 0, 10, 25)}]}}}}},
                      {"plot": "12", "floors": {"generic": {"outline": {"value": {"loops": [{"points": box(10, 0, 20, 24)}]}}}}}]}
    faces = {"11": Polygon(box(0, 0, 10, 25)), "12": Polygon(box(10, 0, 20, 25))}
    result = floor_fit.face_test(plan, faces, set())
    assert result["bands"] == {"within1mm": 1, "within10mm": 1, "worse": 1, "n": 2} and result["worse"][0]["plot"] == "12"
    good = project_profile.ProjectProfile(tmp_path / "s.json", {"cadLinks": {"cutShareMin": 0.9, "floorWidthStepM": 0.5}})
    assert floor_fit.width_step(good) == 0.5
    with pytest.raises(project_profile.ProfileError, match="floorWidthStepM"):
        floor_fit.width_step(project_profile.ProjectProfile(tmp_path / "s.json", {"cadLinks": {"cutShareMin": 0.9, "floorWidthStepM": 0}}))


def test_r136_the_slope_along_the_depth_and_the_type_bands_cut_halfway_with_a_type_that_is_not_separable():
    frame = {"left": (0.0, 0.0), "n": (0.0, 1.0)}
    front, back, depth = floor_fit.front_and_back([(10, 5, 10.1), (0, 0, 10.0), (0, 5, 10.1), (10, 0, 10.0)], frame)
    assert sorted(corner[1] for corner in front) == [0, 0] and floor_fit.along_slope(front, back, depth) == pytest.approx(2.0)
    result = floor_fit.type_bands({"Flat": [0.5, 0.51], "Half": [0.5, 0.5, 0.5], "Two": [2.0, 2.0]})
    assert result["bands"] == [{"type": "Half", "belowPercent": 1.25}, {"type": "Two", "belowPercent": None}] and result["notSeparable"] == ["Flat"]
    assert floor_fit.type_of(0.6, result["bands"]) == "Half" and floor_fit.type_of(1.3, result["bands"]) == "Two"


def test_r136_the_front_corner_level_is_measured_against_the_road_at_its_curb_point():
    plan = {"plots": [{"plot": "11", "floors": {"ramp": {"type": {"value": "Ramps 0.5%"}, "corners": {"value": [[0, 0, 10.0], [10, 0, 10.0], [10, 5, 10.025], [0, 5, 10.025]]}}}}]}
    frames = {"11": {"left": (0.0, 0.0), "n": (0.0, 1.0)}}
    roads = [LineString([(-100, -3), (100, -3)])]
    result, front, rows = floor_fit.measure_ramps(plan, frames, roads, FlatRoad(), set())
    assert front["offsetM"] == pytest.approx(0.1) and front["corners"] == 2 and front["within5mm"] == 2
    assert rows[0]["slope"] == pytest.approx(0.5) and result["picks"] == {"Ramps 0.5% as Ramps 0.5%": 1}


def floor_inputs():
    roads = [LineString([(-100, -5), (100, -5)])]
    outlines = {"outlines": {"generic": [{"unit": "DP1", "mirrored": False, "width": 12.0, "type": "Generic 150mm", "loops": [[[-2, -0.5], [10, -0.5], [10, 24.5], [-2, 24.5]]]},
                                          {"unit": "DP1", "mirrored": True, "width": 10.0, "type": "Generic 150mm", "loops": [[[0, -0.5], [10, -0.5], [10, 24.5], [0, 24.5]]]}],
                             "yard": [{"unit": "DP1", "mirrored": False, "width": 12.0, "type": "yard", "loops": [[[0, 20], [10, 20], [10, 24.5], [0, 24.5]]]}]},
                "rampTypes": {"bands": [{"type": "Ramps 0.5%", "belowPercent": 1.25}, {"type": "Ramps 2%", "belowPercent": None}]}, "rampFront": {"offsetM": 0.1}}
    from shapely.strtree import STRtree
    return {"outlines": outlines, "step": 0.5, "levels": plan_from_cad.model_levels(DOCUMENT), "floors": FLOORS, "profile": profile(), "roads": roads,
            "roadTree": STRtree(roads), "lookup": FlatRoad(), "frontOffset": 0.1, "bands": outlines["rampTypes"]["bands"]}


def pair_plan(inputs):
    faces = {"11": Polygon(box(0, 0, 12, 25)), "12": Polygon(box(12, 0, 22, 25))}
    clusters = [{"code": "M01", "plots": ["11", "12"]}]
    entries = {"11": {"unit": "DP1", "entry": 10.0}, "12": {"unit": "DP1", "entry": 10.05}}
    frame = {"units": {"DP1": {"anchor": "end", "offset": [-10.0, 0.5], "drawnSide": "left"}}}
    return plan_from_cad.build_plan(entries, faces, inputs["roads"], clusters, profile(), ANALYSIS, frame, {"rre-arc-dp1-zz.rvt"}, DOCUMENT, JOB, "C:\\f", FLOORS, inputs)


def test_r137_the_plan_from_cad_gives_each_written_plot_its_floors_from_the_fits_the_ramp_outline_and_the_road():
    plan = pair_plan(floor_inputs())
    left, right = (entry for entry in plan["plots"])
    generic = left["floors"]["generic"]
    # The fitted loop sits 0.5 behind the link origin, and the origin of the left plot is 0.5 in from the front line
    assert generic["outline"]["value"]["loops"][0]["points"] == [[0, 0], [12, 0], [12, 25], [0, 25]]
    assert generic["z"]["value"] == pytest.approx(10.15) and generic["heightOffset"]["value"] == pytest.approx(10.15) and generic["type"]["value"] == "Generic 150mm"
    assert left["floors"]["yard"]["z"]["value"] == pytest.approx(10.05) and left["floors"]["yard"]["heightOffset"]["value"] == pytest.approx(6.05)
    ramp = left["floors"]["ramp"]
    # Front corners at the road plus 0.1, the back pair parallel with its mean at El @ Entry, 5.52 deep
    assert [corner[2] for corner in ramp["corners"]["value"]] == pytest.approx([10.0, 10.0, 10.0, 10.0])
    assert ramp["corners"]["value"][2][:2] == pytest.approx([12.0, 5.52]) and ramp["type"]["value"] == "Ramps 0.5%" and ramp["plotValue"]["value"] == "DP1-11"
    # A mirrored link turns the loop the other way round, the points are the same
    assert sorted(map(tuple, right["floors"]["generic"]["outline"]["value"]["loops"][0]["points"])) == [(12, 0), (12, 25), (22, 0), (22, 25)]
    missing = {item["plot"]: [lack["key"] for lack in item["missing"]] for item in plan["missing"]}
    assert missing == {"12": ["floors.yard"]} and plan["summary"]["floorsFound"] == {"generic": 2, "yard": 1, "ramp": 2}
    assert right["floors"]["ramp"]["slopePercent"]["value"] == pytest.approx((10.05 - 10.0) / 5.52 * 100, abs=0.001)


def test_r137_a_ramp_with_no_road_at_a_curb_point_and_a_level_with_two_elevations_are_refused():
    inputs = floor_inputs()

    class NoRoad:
        def level_at(self, easting, northing):
            return None, None

    inputs["lookup"] = NoRoad()
    plan = pair_plan(inputs)
    assert all("ramp" not in entry["floors"] for entry in plan["plots"]) and plan["summary"]["missingByKey"]["floors.ramp"] == 2
    with pytest.raises(project_profile.ProfileError, match="two elevations"):
        plan_from_cad.model_levels({"floors": [{"level": "L1", "levelElevation": 0.0}, {"level": "L1", "levelElevation": 1.0}]})
    assert pair_plan(dict(floor_inputs()))["plots"][0]["floors"] and not plan_from_cad.build_plan({}, {}, [], [], profile(), ANALYSIS, {"units": {}}, set(), DOCUMENT, JOB, "f", FLOORS)["plots"]


def test_r138_the_floors_of_two_plans_are_compared_per_kind_in_bands():
    plan = pair_plan(floor_inputs())
    other = copy.deepcopy(plan)
    other["plots"][0]["floors"]["generic"]["z"]["value"] += 0.01
    other["plots"][0]["floors"]["ramp"]["corners"]["value"][2][2] += 0.02
    other["plots"][0]["floors"]["ramp"]["type"]["value"] = "Ramps 2%"
    for point in other["plots"][1]["floors"]["generic"]["outline"]["value"]["loops"][0]["points"]:
        point[0] += 0.005
    report = plan_compare.compare_plans(plan, other, set())
    floors = report["floors"]["outside"]
    assert floors["generic"]["top"] == {"within5mm": 1, "worse": 1} and floors["generic"]["outline"] == {"within1mm": 1, "within10mm": 1}
    assert floors["ramp"]["corners"] == {"within5mm": 1, "within30mm": 1} and floors["ramp"]["type"] == {"equal": 1, "worse": 1}
    assert floors["yard"]["presence"] == {"both": 1, "both missing": 1}


def test_r139_the_second_method_places_outlines_and_ramp_corners_by_hand_and_counts_the_compare_again():
    inputs = floor_inputs()
    plan = pair_plan(inputs)
    plots = [{"kind": "plot_shape", "plot": "1", "vertices": [list(point) for point in box(0, 0, 12, 25)]}, {"kind": "plot_shape", "plot": "2", "vertices": [list(point) for point in box(12, 0, 22, 25)]}]
    pieces = [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-100, -5], [100, -5]]}]
    raw = {"typologies": {"DP1": {"plot_width_m": 10}}, "plot": {"long_side": 25000}, "ramp": {"front_setback": {"default": 5520}}}
    results = cad_floor_check.check_outlines(plan, inputs["outlines"], plots, pieces, raw, SETTINGS, 10, 1, 5)
    assert [item["differs"] for item in results] == [[], []]
    surface = ("road", np.array([[-100.0, -50.0, 9.9], [100.0, -50.0, 9.9], [100.0, 0.0, 9.9], [-100.0, 0.0, 9.9]]), np.array([[0, 1, 2], [0, 2, 3]]))
    assert [item["differs"] for item in cad_floor_check.check_ramps(plan, inputs["outlines"], plots, pieces, raw, SETTINGS, [surface], 10, 1, 5)] == [[], []]
    report = plan_compare.compare_plans(plan, plan, set())
    assert all(item["differs"] == [] for item in cad_floor_check.check_compare(report, plan, plan, set()))
    planted = copy.deepcopy(report)
    planted["floors"]["outside"]["ramp"]["type"] = {"equal": 1}
    assert any(item["differs"] for item in cad_floor_check.check_compare(planted, plan, plan, set()))
    moved = copy.deepcopy(plan)
    moved["plots"][0]["floors"]["ramp"]["corners"]["value"][0][2] += 0.01
    assert cad_floor_check.check_ramps(moved, inputs["outlines"], plots, pieces, raw, SETTINGS, [surface], 10, 1, 5)[0]["differs"] or \
        cad_floor_check.check_ramps(moved, inputs["outlines"], plots, pieces, raw, SETTINGS, [surface], 10, 1, 5)[1]["differs"]


def test_r139_the_plain_hausdorff_distance_matches_a_hand_count():
    assert cad_floor_check.directed([box(0, 0, 10, 10)], [box(0, 0, 10, 10)]) == pytest.approx(0.0)
    assert cad_floor_check.directed([box(0, 0, 10, 10)], [box(0, 0, 10, 12)]) == pytest.approx(2.0)
    assert cad_floor_check.directed([box(0, 0, 10, 12)], [box(0, 0, 10, 10)]) == pytest.approx(2.0)
    assert list(cad_floor_check.link_points([(1.0, 2.0)], (0.0, 0.0), 90.0, False)[0]) == pytest.approx([2.0, -1.0])
