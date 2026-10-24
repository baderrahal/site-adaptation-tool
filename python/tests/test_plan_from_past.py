import json
import math

import pytest

import plan_compare
import plan_from_past as pf
import project_profile

OFFSET = [1000.0, 2000.0, 0.0]
FILES = {"keo-arc-vd2-zz.rvt", "keo-arc-dp4-zz.rvt"}
PARAMETERS = ["RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name", "CASE"]
JOB = {"neighbourhood": "AAA", "schedule": "book.xlsx", "sheet": "Sheet1", "prototypes": "C:\\x\\prototypes.json",
       "prototypeFolder": "Prototypes", "output": "C:\\x\\plan.json", "parameters": PARAMETERS}


def shared(x, y, z=0.0):
    return [OFFSET[0] + x, OFFSET[1] + y, z]


def rect(x0, y0, x1, y1, z=0.0):
    return [shared(x0, y0, z), shared(x1, y0, z), shared(x1, y1, z), shared(x0, y1, z)]


def transform(x, y, z, rotation, mirrored):
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    return {"origin": [x, y, z], "rotationDeg": rotation, "mirrored": mirrored,
            "axisX": [round(math.cos(turn), 3), round(math.sin(turn), 3), 0], "axisY": [round(-sign * math.sin(turn), 3), round(sign * math.cos(turn), 3), 0], "axisZ": [0, 0, 1]}


def link(plot, x=0.0, y=0.0, z=10.0, rotation=30.0, mirrored=False, file="KEO-ARC-VD2-ZZ.rvt", unit="VD2", steps="1", ffl="10.000", shared_origin=None):
    parameters = {"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Prototype": unit, "RRE_ZZ_SP_Number-of-Steps": steps,
                  "RRE_ZZ_SP_RVT-Name": str(plot) + " | " + unit, "CASE": "A", "RRE_ZZ_SP_Prototype-FFL": ffl}
    if plot is None:
        parameters = {}
    entry = {"file": file, "elementId": 1000 + (int(plot) if plot else 0), "transform": transform(x, y, z, rotation, mirrored), "parameters": parameters}
    if shared_origin is not None:
        entry["transformShared"] = {"origin": shared_origin}
    return entry


def flat(element_id, type_name, plot, loop, top):
    return {"typeName": type_name, "elementId": element_id, "topElevation": top, "topElevationRange": {"min": top, "max": top},
            "outline": [loop], "plotNumber": None if plot is None else str(plot)}


def ramp(element_id, plot, corners):
    floor = flat(element_id, "Ramps 2%", plot, corners, min(corner[2] for corner in corners))
    floor["corners"] = [[corner[0] - OFFSET[0], corner[1] - OFFSET[1], corner[2] - OFFSET[2]] for corner in corners]
    return floor


def plot_floors(plot, x0, first_id, yard=True, ffl=10.0):
    floors = [flat(first_id, "Generic 150mm", plot, rect(x0, 0, x0 + 10, 10), ffl),
              ramp(first_id + 1, plot, [shared(x0 + 10, 0, 9.8), shared(x0 + 12, 0, 9.81), shared(x0 + 12, 10, 9.82), shared(x0 + 10, 10, 9.83)])]
    if yard:
        floors.append(flat(first_id + 2, "yard", None, rect(x0 - 5, 0, x0, 10), ffl - 0.1))
    return floors


def document(links, floors):
    return {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, 5.0], "sharedPosition": [OFFSET[0], OFFSET[1], 5.0 + OFFSET[2]]},
                           "angleToTrueNorthDeg": 0.0, "links": links, "floors": floors}]}


def row(plot, ffl=10.0, ssl=9.925, entry=9.81234, height=0.18766, steps="1 steps", unit="VD2"):
    return {"Plot No.": int(plot), "Unit": unit, "FFL": ffl, "SSL": ssl, "El @ Entry*": entry, "Height**": height, "Steps Needed": steps}


def build(links, floors, rows, files=FILES):
    return pf.build_plan(document(links, floors), rows, JOB, files, "C:\\Prototypes")


def one_plot_plan(**link_options):
    return build([link("101", **link_options)], plot_floors("101", 0, 10), [row("101")])


def by_plot(plan):
    return {entry["plot"]: entry for entry in plan["plots"]}


def missing_of(plan, plot):
    return {item["key"]: item["why"] for entry in plan["missing"] if entry["plot"] == plot for item in entry["missing"]}


def every_leaf_is_sourced(node, path=""):
    for key, value in node.items():
        if key == "plot":
            continue
        if isinstance(value, dict) and "source" in value:
            assert set(value) == {"value", "source"}, path + key
            assert value["source"] in ("pastJob", "computed"), path + key
        else:
            assert isinstance(value, dict), path + key
            every_leaf_is_sourced(value, path + key + ".")


def test_r80_top_level_names_format_neighbourhood_coordinates_survey_point_angle_folder_and_plots():
    plan = one_plot_plan()
    assert plan["format"] == "site-adaptation-plan"
    assert plan["formatVersion"] == 1
    assert plan["neighbourhood"] == "AAA"
    assert plan["coordinateSystem"] == {"name": "shared", "lengthUnit": "m", "angleUnit": "deg"}
    assert plan["surveyPoint"] == {"position": [0, 0, 5.0], "sharedPosition": [1000.0, 2000.0, 5.0]}
    assert plan["angleToTrueNorthDeg"] == 0
    assert plan["prototypeFolder"] == "C:\\Prototypes"
    assert plan["sources"] == {"prototypes": "prototypes.json", "schedule": "book.xlsx", "sheet": "Sheet1"}
    assert [entry["plot"] for entry in plan["plots"]] == ["101"]


def test_r80_every_value_of_a_plot_has_a_value_and_a_source():
    entry = by_plot(one_plot_plan())["101"]
    assert set(entry) == {"plot", "unit", "case", "prototypeFile", "insert", "rotationDeg", "mirrored", "levels", "parameters", "floors"}
    every_leaf_is_sourced(entry)


def test_r80_insert_is_shared_and_its_z_is_the_ffl():
    entry = by_plot(build([link("101", x=3.0, y=4.0, z=10.0004)], plot_floors("101", 0, 10), [row("101", ffl=10.0)]))["101"]
    assert entry["insert"] == {"value": [1003.0, 2004.0, 10.0], "source": "pastJob"}


def test_r80_floors_generic_at_ffl_yard_at_ffl_minus_0_100_ramp_corners_with_their_own_z():
    floors = by_plot(one_plot_plan())["101"]["floors"]
    assert floors["generic"]["z"] == {"value": 10.0, "source": "computed"}
    assert floors["yard"]["z"] == {"value": 9.9, "source": "computed"}
    assert floors["generic"]["outline"]["value"] == [[[1000.0, 2000.0], [1010.0, 2000.0], [1010.0, 2010.0], [1000.0, 2010.0]]]
    assert floors["yard"]["type"]["value"] == "yard"
    assert floors["ramp"]["type"]["value"] == "Ramps 2%"
    assert [corner[2] for corner in floors["ramp"]["corners"]["value"]] == [9.8, 9.81, 9.82, 9.83]


def test_r80_a_yard_in_two_pieces_keeps_both_rings():
    floors = plot_floors("101", 0, 10, yard=False)
    two = flat(12, "yard", None, rect(-5, 0, 0, 10), 9.9)
    two["outline"].append(rect(-5, 20, 0, 30))
    plan = build([link("101")], floors + [two], [row("101")])
    assert len(by_plot(plan)["101"]["floors"]["yard"]["outline"]["value"]) == 2


def test_r80_prototype_ffl_is_never_a_parameter_to_write():
    entry = by_plot(one_plot_plan())["101"]
    assert "RRE_ZZ_SP_Prototype-FFL" not in entry["parameters"]
    assert list(entry["parameters"]) == PARAMETERS


def test_r80_written_file_keeps_4_decimals_for_entry_and_height_and_3_for_the_rest(tmp_path):
    plan = build([link("101", x=3.00049)], plot_floors("101", 0, 10), [row("101", ffl=10.00049, entry=9.81234, height=0.18766)])
    pf.write_plan(tmp_path / "plan.json", plan)
    entry = json.loads((tmp_path / "plan.json").read_text(encoding="utf-8"))["plots"][0]
    assert entry["levels"]["entry"]["value"] == 9.8123
    assert entry["levels"]["height"]["value"] == 0.1877
    assert entry["levels"]["ffl"]["value"] == 10.0
    assert entry["insert"]["value"][0] == 1003.0


def test_r81_unit_case_and_parameters_come_from_the_link_and_levels_from_the_schedule_by_plot_number():
    links = [link("102", x=50), link("101")]
    rows = [row("102", ffl=11.0, ssl=10.925, steps="2 steps"), row("101")]
    plans = by_plot(build(links, plot_floors("101", 0, 10) + plot_floors("102", 50, 20, ffl=11.0), rows))
    assert plans["102"]["levels"]["ffl"]["value"] == 11.0
    assert plans["102"]["levels"]["ssl"]["value"] == 10.925
    assert plans["102"]["levels"]["steps"]["value"] == 2
    assert plans["101"]["unit"]["value"] == "VD2"
    assert plans["101"]["case"]["value"] == "A"
    assert plans["101"]["parameters"]["RRE_ZZ_SP_RVT-Name"]["value"] == "101 | VD2"


def test_r81_rotation_and_mirror_come_from_the_transform():
    entry = by_plot(one_plot_plan(rotation=210.106, mirrored=True))["101"]
    assert entry["rotationDeg"]["value"] == 210.106
    assert entry["mirrored"]["value"] is True


def test_r81_the_schedule_ffl_is_kept_and_the_prototype_ffl_parameter_is_a_conflict():
    plan = build([link("101", ffl="10.020")], plot_floors("101", 0, 10), [row("101", ffl=10.0)])
    assert by_plot(plan)["101"]["levels"]["ffl"]["value"] == 10.0
    assert {(item["field"], item["kept"], item["other"], item["otherSource"]) for item in plan["conflicts"]} == {("levels.ffl", 10.0, 10.02, "RRE_ZZ_SP_Prototype-FFL")}


def test_r81_a_link_origin_z_off_the_ffl_by_more_than_1_5_mm_is_a_conflict():
    plan = build([link("101", z=10.002)], plot_floors("101", 0, 10), [row("101", ffl=10.0)])
    assert [item["otherSource"] for item in plan["conflicts"]] == ["past link origin Z"]
    assert build([link("101", z=10.0015)], plot_floors("101", 0, 10), [row("101", ffl=10.0)])["conflicts"] == []


def test_r81_unit_and_steps_that_differ_between_link_and_schedule_are_conflicts():
    plan = build([link("101", steps="2")], plot_floors("101", 0, 10), [row("101", unit="DP4")])
    found = {(item["field"], item["otherSource"]) for item in plan["conflicts"]}
    assert found == {("unit", "schedule Unit"), ("levels.steps", "RRE_ZZ_SP_Number-of-Steps")}
    assert by_plot(plan)["101"]["unit"]["value"] == "VD2"


def test_r81_a_plot_with_no_schedule_row_is_not_written_and_says_why():
    plan = build([link("101")], plot_floors("101", 0, 10), [])
    assert plan["plots"] == []
    assert "scheduleRow" in missing_of(plan, "101")
    assert plan["missing"][0]["written"] is False


def test_r81_a_schedule_plot_with_no_link_is_not_written():
    plan = build([], [], [row("101")])
    assert plan["plots"] == []
    assert "link" in missing_of(plan, "101")


def test_r81_a_plot_number_on_two_links_is_not_written():
    plan = build([link("101"), link("101", x=5)], plot_floors("101", 0, 10), [row("101")])
    assert plan["plots"] == []
    assert missing_of(plan, "101")["links"].startswith("2 links")


def test_r81_a_link_without_plot_number_is_counted_and_not_a_plot():
    plan = build([link(None), link("101")], plot_floors("101", 0, 10), [row("101")])
    assert plan["summary"]["linksWithoutPlot"] == 1
    assert plan["summary"]["plots"] == 1


def test_r81_a_prototype_file_not_in_the_folder_keeps_the_plot_out():
    plan = build([link("101", file="other.rvt")], plot_floors("101", 0, 10), [row("101")])
    assert plan["plots"] == []
    assert "not in the prototype folder" in missing_of(plan, "101")["prototypeFile"]


def test_r81_axes_that_are_not_a_turn_with_or_without_mirror_are_not_used():
    bad = link("101")
    bad["transform"]["axisY"] = [-bad["transform"]["axisY"][0], -bad["transform"]["axisY"][1], 0]
    plan = build([bad], plot_floors("101", 0, 10), [row("101")])
    assert plan["plots"] == []
    assert "transform" in missing_of(plan, "101")


def test_r81_a_plot_with_no_generic_floor_is_written_and_listed_in_missing():
    floors = [floor for floor in plot_floors("101", 0, 10) if floor["typeName"] != "Generic 150mm"]
    plan = build([link("101")], floors, [row("101")])
    assert "generic" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.generic"] == "no floor names the plot"
    assert plan["missing"][0]["written"] is True
    assert plan["summary"]["missingByKey"] == {"floors.generic": 1, "floors.yard": 1}


def test_r81_two_ramps_for_one_plot_are_listed_and_not_chosen():
    floors = plot_floors("101", 0, 10) + [ramp(99, "101", [shared(0, 20, 1), shared(1, 20, 1), shared(1, 21, 1), shared(0, 21, 1)])]
    plan = build([link("101")], floors, [row("101")])
    assert "ramp" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.ramp"].startswith("2 floors")


def test_r81_the_yard_is_found_by_geometry_with_the_r58_code():
    plan = one_plot_plan()
    assert plan["summary"]["yardStatus"] == {"assigned": 1}
    assert plan["summary"]["floorsFound"] == {"generic": 1, "yard": 1, "ramp": 1}


def test_r81_an_ambiguous_yard_is_missing_and_never_guessed():
    floors = plot_floors("101", 0, 10) + [flat(50, "yard", None, rect(0, -5, 10, 0), 9.9)]
    plan = build([link("101")], floors, [row("101")])
    assert "yard" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.yard"] == "ambiguous, candidates 12 50"


def test_r81_a_past_floor_top_off_its_computed_z_is_a_conflict():
    floors = plot_floors("101", 0, 10)
    floors[0]["topElevationRange"] = {"min": 9.8, "max": 9.8}
    plan = build([link("101")], floors, [row("101")])
    assert [(item["field"], item["other"]) for item in plan["conflicts"]] == [("floors.generic.z", [9.8, 9.8])]


def test_r81_settings_that_list_the_prototype_ffl_parameter_are_refused(tmp_path):
    settings = tmp_path / "project.json"
    job = dict(JOB, parameters=PARAMETERS + ["RRE_ZZ_SP_Prototype-FFL"])
    settings.write_text(json.dumps({"schedules": ["book.xlsx"], "pastJob": job}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="never overwritten"):
        pf.load_job(project_profile.load_settings(settings))


def test_r81_settings_without_a_past_job_key_are_refused(tmp_path):
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"schedules": ["book.xlsx"], "pastJob": dict(JOB, sheet="")}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="sheet"):
        pf.load_job(project_profile.load_settings(settings))


def test_r81_the_real_settings_name_the_past_job_with_no_ffl_parameter():
    job = pf.load_job(project_profile.load_settings())
    assert "RRE_ZZ_SP_Prototype-FFL" not in job["parameters"]
    assert len(job["parameters"]) == 9


def test_r81_the_plan_is_never_written_inside_the_repo():
    with pytest.raises(ValueError, match="inside"):
        pf.main(["--out", str(project_profile.ROOT / "plan.json")])


def test_r82_position_proof_reports_the_largest_difference_in_mm():
    links = [link("101", x=1.0, shared_origin=[1001.0004, 2000.0, 10.0]), link("102", x=50, shared_origin=[1050.0, 2000.0, 10.0])]
    plan = build(links, plot_floors("101", 0, 10) + plot_floors("102", 50, 20), [row("101"), row("102")])
    assert plan["summary"]["positionProof"] == {"links": 2, "largestMm": 0.4}
    assert plan["conflicts"] == []


def test_r82_a_difference_over_1_mm_is_a_conflict_and_links_without_transform_shared_use_the_same_math():
    links = [link("101", x=1.0, shared_origin=[1001.002, 2000.0, 10.0]), link("102", x=50)]
    plan = build(links, plot_floors("101", 0, 10) + plot_floors("102", 50, 20), [row("101"), row("102")])
    assert plan["summary"]["positionProof"]["links"] == 1
    assert [(item["plot"], item["field"]) for item in plan["conflicts"]] == [("101", "insert")]
    assert by_plot(plan)["102"]["insert"]["value"][:2] == [1050.0, 2000.0]


def placed_model(links, floors):
    """prototypes.json of a model the placer built from a plan of the same links."""
    return document(links, floors)


def test_r89_a_model_built_from_the_plan_matches_on_every_field():
    links = [link("101", x=3.0, rotation=210.106, mirrored=True), link("102", x=50)]
    floors = plot_floors("101", 0, 10) + plot_floors("102", 50, 20)
    plan = build(links, floors, [row("101"), row("102")])
    report = plan_compare.compare(plan, placed_model(links, floors))
    assert report["fullMatch"] == 2
    assert report["matches"] == dict.fromkeys(plan_compare.FIELDS, 2)
    assert report["differing"] == []


def test_r89_each_field_that_differs_is_counted_and_its_plot_listed():
    links = [link("101"), link("102", x=50), link("103", x=100)]
    floors = plot_floors("101", 0, 10) + plot_floors("102", 50, 20) + plot_floors("103", 100, 30)
    plan = build(links, floors, [row("101"), row("102"), row("103")])
    placed = [link("101", x=0.0012, rotation=30.02), link("102", x=50, mirrored=True, file="KEO-ARC-DP4-ZZ.rvt"), link("103", x=100, steps="2"), link("104", x=150)]
    report = plan_compare.compare(plan, placed_model(placed, floors))
    differs = {item["plot"]: item["differs"] for item in report["differing"]}
    assert set(differs["101"]) == {"position", "rotation"}
    assert differs["101"]["position"] == "1.2 mm off"
    assert set(differs["102"]) == {"mirrored", "file"}
    assert differs["103"] == {"parameters": "differ: RRE_ZZ_SP_Number-of-Steps"}
    assert report["mismatches"] == {"link": 0, "file": 1, "position": 1, "rotation": 1, "mirrored": 1, "parameters": 1}
    assert report["linksNotInPlan"] == ["104"]
    assert report["fullMatch"] == 0


def test_r89_a_plot_with_no_link_or_two_links_is_a_link_mismatch_only():
    links = [link("101"), link("102", x=50)]
    floors = plot_floors("101", 0, 10) + plot_floors("102", 50, 20)
    plan = build(links, floors, [row("101"), row("102")])
    report = plan_compare.compare(plan, placed_model([link("102", x=50), link("102", x=50)], floors))
    assert report["mismatches"]["link"] == 2
    assert sum(report["matches"].values()) == 0


def test_r89_the_report_is_never_written_inside_the_repo(tmp_path):
    plan = tmp_path / "plan.json"
    plan.write_text("{}", encoding="utf-8")
    with pytest.raises(ValueError, match="inside"):
        plan_compare.main([str(plan), str(project_profile.ROOT / "prototypes.json")])
