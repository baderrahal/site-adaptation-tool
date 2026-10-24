import copy

import placer_check as pc
import yard_rule

SURVEY = {"position": [0.0, 0.0, 0.0], "sharedPosition": [100.0, 200.0, 0.0]}
# Made up floor type and level names, the real ones live in config/project.json only
CONFIG = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]}, "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}


def rect(x0, y0, x1, y1, z):
    return [[100 + x0, 200 + y0, z], [100 + x1, 200 + y0, z], [100 + x1, 200 + y1, z], [100 + x0, 200 + y1, z]]


def floor(element_id, type_name, level, loop, plot=None, plot_value=None, corners=None):
    zs = [point[2] for point in loop] if corners is None else [corner[2] for corner in corners]
    entry = {"elementId": element_id, "typeName": type_name, "level": level, "outline": [loop],
             "topElevationRange": {"min": min(zs), "max": max(zs)}, "plotNumber": plot, "parameters": {}}
    if plot_value:
        entry["parameters"]["Plot"] = plot_value
    if corners is not None:
        entry["corners"] = [[corner[0] - 100, corner[1] - 200, corner[2]] for corner in corners]
        entry["cornersSource"] = "shapeEditor"
    return entry


def ramp_corners(x0):
    return [[100 + x0 + 10, 200, 9.8], [100 + x0 + 12, 200, 9.81], [100 + x0 + 12, 210, 9.82], [100 + x0 + 10, 210, 9.83]]


def plot_floors(plot, x0, first, yard_top=9.9, with_yard_plot=True):
    return [
        floor(first, "Slab G", "L1", rect(x0, 0, x0 + 10, 10, 10.0), plot, "VD2-" + plot),
        floor(first + 1, "Yard Y", "L2", rect(x0 - 5, 0, x0, 10, yard_top), None, "VD2-" + plot if with_yard_plot else None),
        floor(first + 2, "Ramp B", "L1", [corner[:2] + [corner[2]] for corner in ramp_corners(x0)], plot, "VD2-" + plot, corners=ramp_corners(x0)),
    ]


def plan_floor(type_name, level, loop, z, plot, corners=None):
    entry = {"type": {"value": type_name}, "level": {"value": level}, "outline": {"value": {"loops": [{"points": [point[:2] for point in loop], "arcs": []}]}},
             "plotValue": {"value": "VD2-" + plot}}
    if corners is None:
        entry["z"] = {"value": z}
    else:
        entry["corners"] = {"value": corners}
    return entry


def plan_plot(plot, x0, yard_z=9.9):
    return {"plot": plot, "floors": {
        "generic": plan_floor("Slab G", "L1", rect(x0, 0, x0 + 10, 10, 0), 10.0, plot),
        "yard": plan_floor("Yard Y", "L2", rect(x0 - 5, 0, x0, 10, 0), yard_z, plot),
        "ramp": plan_floor("Ramp B", "L1", ramp_corners(x0), None, plot, corners=ramp_corners(x0)),
    }}


def model(floors):
    return {"documents": [{"kind": "project", "surveyPoint": SURVEY, "floors": floors, "links": []}]}


FACES = yard_rule.face_shapes([{"kind": "plot_shape", "plot": label, "vertices": [[100 + x - 5, 200], [100 + x + 12, 200], [100 + x + 12, 210], [100 + x - 5, 210]]}
                               for label, x in (("1", 0), ("2", 50))], 0)[0]
PLAN = {"plots": [plan_plot("1", 0), plan_plot("2", 50)]}
REPORT = {"floors": {"mode": "real", "stopped": False, "rolledBack": False, "floorsBefore": 0, "floorsAfter": 6,
                     "placed": [{"plot": plot, "kind": kind} for plot in ("1", "2") for kind in ("generic", "yard", "ramp")], "skipped": [], "issues": [], "counts": {}}}


def answer_key():
    # The past model leaves the Plot parameter off its yards, so they join by the yard rule
    return model(plot_floors("1", 0, 10, with_yard_plot=False) + plot_floors("2", 50, 20, with_yard_plot=False))


def placed():
    return model(plot_floors("1", 0, 10) + plot_floors("2", 50, 20))


def check(test, report=REPORT, plan=PLAN, plots=None):
    return pc.compare_floors(plan, answer_key(), test, CONFIG, FACES, plots, report)


def test_r119_a_run_that_places_every_floor_as_the_answer_key_passes_with_every_check_equal():
    result = check(placed())
    assert pc.floors_pass(result)
    assert result["differsByPlacer"] == 0 and result["pastDataDifferences"] == 0
    assert result["floors"]["yard"] == {"answerKey": 2, "test": 2, "plan": 2, "inBoth": 2, "onlyAnswerKey": [], "onlyTest": [], "planNotInTest": [], "testNotInPlan": [], "twiceInTest": [], "notCompared": []}
    assert set(result["checks"]) == {kind + "." + name for kind, names in (("generic", ("type", "level", "top", "area", "outline", "plotValue")),
                                                                          ("yard", ("type", "level", "top", "area", "outline", "plotValue")),
                                                                          ("ramp", ("type", "level", "cornersZ", "slope", "topMax", "cornersSource", "area", "outline", "plotValue")))
                                    for name in names}
    assert all(state == {"equal": 2} for state in result["checks"].values())
    assert result["rampTypes"] == {"Ramp B": {"plots": 2, "allEqual": 2, "cornersZEqual": 2, "slopeEqual": 2}}


def test_r119_a_moved_outline_a_wrong_top_and_a_wrong_corner_z_are_each_caught_by_the_placer_count():
    for change, check_name in ((lambda floors: floors[0]["outline"][0].__setitem__(0, [100.002, 200.0, 10.0]), "generic.outline"),
                               (lambda floors: floors[0]["topElevationRange"].update({"min": 10.002, "max": 10.002}), "generic.top"),
                               (lambda floors: floors[2]["corners"][1].__setitem__(2, 9.812), "ramp.cornersZ")):
        test = placed()
        change(test["documents"][0]["floors"])
        result = check(test)
        assert result["checks"][check_name].get("differs") == 1, check_name
        assert not pc.floors_pass(result)


def test_r119_a_yard_at_the_plan_level_that_differs_from_the_past_one_is_past_data_and_not_a_placer_fault():
    # The past yard sits at the FFL, the plan and the test at FFL minus 0.1
    key = model(plot_floors("1", 0, 10, yard_top=10.0, with_yard_plot=False) + plot_floors("2", 50, 20, yard_top=10.0, with_yard_plot=False))
    result = pc.compare_floors(PLAN, key, placed(), CONFIG, FACES, None, REPORT)
    assert result["checks"]["yard.top"] == {"pastData": 2}
    assert result["pastDataByCheck"] == {"yard.top": 2}
    assert pc.floors_pass(result)


def test_r119_a_floor_with_no_plot_value_one_placed_twice_and_one_missing_fail():
    test = placed()
    test["documents"][0]["floors"][1]["parameters"] = {}
    result = check(test)
    assert result["unnumberedTestFloors"] == 1 and result["floors"]["yard"]["planNotInTest"] == ["1"]
    assert not pc.floors_pass(result)
    test = placed()
    test["documents"][0]["floors"].append(copy.deepcopy(test["documents"][0]["floors"][0]))
    assert check(test)["floors"]["generic"]["twiceInTest"] == ["1"]
    assert not pc.floors_pass(check(test))
    test = placed()
    del test["documents"][0]["floors"][5]
    assert check(test)["floors"]["ramp"]["planNotInTest"] == ["2"]


def test_r119_a_wrong_type_or_level_differs():
    test = placed()
    test["documents"][0]["floors"][2]["typeName"] = "Ramp A"
    test["documents"][0]["floors"][1]["level"] = "L1"
    result = check(test)
    assert result["checks"]["ramp.type"] == {"equal": 1, "differs": 1}
    assert result["checks"]["yard.level"] == {"equal": 1, "differs": 1}


def test_r119_floors_before_and_after_show_that_a_dry_run_stop_or_roll_back_changed_nothing():
    empty = model([])
    for end, flags in (("stop", {"stopped": True}), ("rollBack", {"rolledBack": True}), ("dryRun", {"mode": "dryRun"})):
        report = copy.deepcopy(REPORT)
        report["floors"].update(dict({"floorsAfter": 0, "placed": []}, **flags))
        result = check(empty, report)
        assert result["model"] == {"answerKeyFloors": 6, "testFloors": 0, "planFloors": 6}
        assert pc.floors_pass(result, expect_empty=True, expect_end=end)
        others = [other for other in ("stop", "rollBack", "dryRun") if other != end]
        assert not any(pc.floors_pass(result, expect_empty=True, expect_end=other) for other in others)
    report = copy.deepcopy(REPORT)
    report["floors"].update({"stopped": True, "floorsAfter": 3})
    assert not pc.floors_pass(check(empty, report), expect_empty=True, expect_end="stop")


def test_r119_a_partial_run_must_place_only_the_listed_plots():
    report = copy.deepcopy(REPORT)
    report["floors"]["placed"] = [{"plot": "2", "kind": kind} for kind in ("generic", "yard", "ramp")]
    result = check(placed(), report)
    assert pc.floors_pass(result, placed_plots=["2"])
    assert not pc.floors_pass(result, placed_plots=["1"])


def test_r119_the_second_run_must_skip_every_floor_as_placed_and_change_nothing():
    second = {"floors": {"mode": "real", "stopped": False, "rolledBack": False, "floorsBefore": 6, "floorsAfter": 6, "placed": [],
                         "skipped": [{"plot": "1", "kind": "yard", "reason": "already placed, a yard floor of the document has Plot VD2-1"}]}}
    assert pc.second_run(second)["pass"]
    second["floors"]["skipped"].append({"plot": "2", "kind": "ramp", "reason": "the plan lacks corners"})
    assert not pc.second_run(second)["pass"]


def test_r119_a_plot_filter_limits_the_join_and_the_plan_to_the_plots_listed():
    test = model(plot_floors("2", 50, 20))
    result = check(test, plots=["2"])
    assert result["floors"]["generic"]["answerKey"] == 1 and result["floors"]["generic"]["planNotInTest"] == []
    assert pc.floors_pass(result)


def test_r119_an_arc_of_the_plan_is_compared_as_short_chords():
    loop = {"points": [[0, 0], [10, 0], [10, 10], [0, 10]], "arcs": [{"edge": 1, "center": [10, 5], "radius": 5.0, "ccw": True}]}
    points = pc.loop_points(loop)
    assert len(points) > 60
    assert abs(max(point[0] for point in points) - 15.0) <= 0.0001 + 1e-9


def test_r119_the_second_method_works_the_area_out_by_hand():
    result = pc.floors_by_hand(answer_key(), placed(), CONFIG, FACES, PLAN, 5)
    assert result["of"] == 4 and result["match"] == 4
    assert result == pc.floors_by_hand(answer_key(), placed(), CONFIG, FACES, PLAN, 5)


def test_r119_an_outline_the_plan_moved_and_the_test_followed_is_a_fault_and_never_past_data():
    plan = copy.deepcopy(PLAN)
    for point in plan["plots"][0]["floors"]["generic"]["outline"]["value"]["loops"][0]["points"]:
        point[0] += 0.5
    test = placed()
    for point in test["documents"][0]["floors"][0]["outline"][0]:
        point[0] += 0.5
    result = check(test, plan=plan)
    assert result["checks"]["generic.outline"] == {"equal": 1, "differs": 1}
    assert not pc.floors_pass(result)


def test_r119_a_top_equal_to_the_past_but_off_the_plan_is_not_equal():
    plan = copy.deepcopy(PLAN)
    plan["plots"][0]["floors"]["generic"]["z"]["value"] = 9.996
    result = check(placed(), plan=plan)
    assert result["checks"]["generic.top"] == {"equal": 1, "differs": 1}
    assert not pc.floors_pass(result)


def test_r119_a_planned_floor_the_answer_key_lacks_is_not_compared_and_fails():
    key = answer_key()
    del key["documents"][0]["floors"][3]
    result = pc.compare_floors(PLAN, key, placed(), CONFIG, FACES, None, REPORT)
    assert result["floors"]["generic"]["notCompared"] == ["2"]
    assert not pc.floors_pass(result)


def test_r119_a_filtered_run_fails_when_the_model_holds_floors_of_other_plots():
    result = check(placed(), plots=["2"])
    assert result["model"]["testFloors"] == 6 and result["model"]["planFloors"] == 3
    assert not pc.floors_pass(result)


def test_r119_a_ramp_whose_top_faces_do_not_reach_its_highest_corner_differs():
    test = placed()
    test["documents"][0]["floors"][2]["topElevationRange"]["max"] = 9.8
    assert check(test)["checks"]["ramp.topMax"] == {"equal": 1, "differs": 1}


def test_r119_the_second_method_judges_the_top_by_the_plan_so_past_data_does_not_fail_it():
    # The past Generic top sits 4 mm above the plan, the test is on the plan
    key = answer_key()
    for floor in key["documents"][0]["floors"]:
        if floor["typeName"] == "Slab G":
            floor["topElevationRange"] = {"min": 10.004, "max": 10.004}
    result = pc.floors_by_hand(key, placed(), CONFIG, FACES, PLAN, 5)
    assert result["match"] == result["of"] == 4


def test_r119_a_large_arc_cut_by_the_sag_limit_stays_within_a_tenth_of_a_millimetre():
    import math
    loop = {"points": [[0, 0], [20, 0], [20, 20], [0, 20]], "arcs": [{"edge": 1, "center": [20, 10], "radius": 10.0, "ccw": True}]}
    points = pc.loop_points(loop)
    worst = max(10.0 - math.dist([(a[0] + b[0]) / 2, (a[1] + b[1]) / 2], [20, 10]) for a, b in zip(points[1:-2], points[2:-1]))
    assert 0 < worst <= 0.0001 + 1e-9


def test_r119_the_second_method_fails_a_floor_with_no_top_read_and_does_not_crash():
    test = placed()
    for floor in test["documents"][0]["floors"]:
        floor.pop("topElevationRange", None)
    result = pc.floors_by_hand(answer_key(), test, CONFIG, FACES, PLAN, 5)
    assert result["of"] == 4 and result["match"] == 0
