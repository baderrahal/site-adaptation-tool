import json
import math

import pytest

import plan_compare
import plan_from_past as pf
import project_profile
import yard_rule

OFFSET = [1000.0, 2000.0, 0.0]
FILES = {"keo-arc-vd2-zz.rvt", "keo-arc-dp4-zz.rvt"}
PARAMETERS = ["RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name", "CASE"]
JOB = {"neighbourhood": "AAA", "schedule": "book.xlsx", "sheet": "Sheet1", "prototypes": "C:\\x\\prototypes.json",
       "prototypeFolder": "Prototypes", "output": "C:\\x\\plan.json", "parameters": PARAMETERS}
# Made up floor type and level names, the real ones live in config/project.json only
FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]},
          "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}
LEVEL_ELEVATIONS = {"L1": 0.0, "L2": 4.0}


def shared(x, y, z=0.0):
    return [OFFSET[0] + x, OFFSET[1] + y, z]


def rect(x0, y0, x1, y1, z=0.0):
    return [shared(x0, y0, z), shared(x1, y0, z), shared(x1, y1, z), shared(x0, y1, z)]


def transform(x, y, z, rotation, mirrored):
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    return {"origin": [x, y, z], "rotationDeg": rotation, "mirrored": mirrored,
            "axisX": [round(math.cos(turn), 3), round(math.sin(turn), 3), 0], "axisY": [round(-sign * math.sin(turn), 3), round(sign * math.cos(turn), 3), 0], "axisZ": [0, 0, 1]}


def link(plot, x=0.0, y=0.0, z=10.0, rotation=30.0, mirrored=False, file="KEO-ARC-VD2-ZZ.rvt", unit="VD2", steps="1", ffl="10.000", shared_origin=None, name=None):
    parameters = {"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Prototype": unit, "RRE_ZZ_SP_Number-of-Steps": steps,
                  "RRE_ZZ_SP_RVT-Name": str(plot) + " | " + unit, "CASE": "A", "RRE_ZZ_SP_Prototype-FFL": ffl,
                  "Name": str(plot) + " | " + unit if name is None else name}
    if plot is None:
        parameters = {}
    entry = {"file": file, "elementId": 1000 + (int(plot) if plot and plot.isdigit() else 0), "transform": transform(x, y, z, rotation, mirrored), "parameters": parameters}
    if shared_origin is not None:
        entry["transformShared"] = {"origin": shared_origin}
    return entry


def internal(points):
    return [[point[0] - OFFSET[0], point[1] - OFFSET[1], 150.0] for point in points]


def flat(element_id, type_name, plot, loop, top, level="L1"):
    return {"typeName": type_name, "elementId": element_id, "topElevation": top, "topElevationRange": {"min": top, "max": top},
            "outline": [loop], "sketchBoundary": internal(loop), "level": level, "levelElevation": LEVEL_ELEVATIONS[level],
            "plotNumber": None if plot is None else str(plot)}


def ramp(element_id, plot, corners, type_name="Ramp B"):
    floor = flat(element_id, type_name, plot, corners, min(corner[2] for corner in corners))
    floor["corners"] = [[corner[0] - OFFSET[0], corner[1] - OFFSET[1], corner[2] - OFFSET[2]] for corner in corners]
    return floor


def plot_floors(plot, x0, first_id, yard=True, ffl=10.0):
    floors = [flat(first_id, "Slab G", plot, rect(x0, 0, x0 + 10, 10), ffl),
              ramp(first_id + 1, plot, [shared(x0 + 10, 0, 9.8), shared(x0 + 12, 0, 9.81), shared(x0 + 12, 10, 9.82), shared(x0 + 10, 10, 9.83)])]
    if yard:
        floors.append(flat(first_id + 2, "Yard Y", None, rect(x0 - 5, 0, x0, 10), ffl - 0.1, level="L2"))
    return floors


def face(label, x0, x1=None, y0=0, y1=10):
    """A baseplan plot face in the shape plots.json holds, from x0 - 5 to x0 + 12 so it holds the three floors of plot_floors."""
    x1 = x0 + 12 if x1 is None else x1
    return {"kind": "plot_shape", "plot": label, "vertices": [[OFFSET[0] + x, OFFSET[1] + y] for x, y in ((x0 - 5, y0), (x1, y0), (x1, y1), (x0 - 5, y1))]}


FACES = yard_rule.face_shapes([face("101", 0), face("102", 50), face("103", 100)], 0)[0]


def document(links, floors):
    return {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, 5.0], "sharedPosition": [OFFSET[0], OFFSET[1], 5.0 + OFFSET[2]]},
                           "angleToTrueNorthDeg": 0.0, "links": links, "floors": floors}]}


def row(plot, ffl=10.0, ssl=9.925, entry=9.81234, height=0.18766, steps="1 steps", unit="VD2"):
    return {"Plot No.": int(plot), "Unit": unit, "FFL": ffl, "SSL": ssl, "El @ Entry*": entry, "Height**": height, "Steps Needed": steps}


def build(links, floors, rows, files=FILES, faces=None, config=None):
    return pf.build_plan(document(links, floors), rows, JOB, files, "C:\\Prototypes", config or FLOORS, FACES if faces is None else faces)


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
    assert set(entry) == {"plot", "unit", "case", "linkName", "prototypeFile", "insert", "rotationDeg", "mirrored", "levels", "parameters", "floors"}
    every_leaf_is_sourced(entry)


def test_r80_insert_is_shared_and_its_z_is_the_ffl():
    entry = by_plot(build([link("101", x=3.0, y=4.0, z=10.0004)], plot_floors("101", 0, 10), [row("101", ffl=10.0)]))["101"]
    assert entry["insert"] == {"value": [1003.0, 2004.0, 10.0], "source": "pastJob"}


def test_r80_floors_generic_at_ffl_yard_at_ffl_minus_0_100_ramp_corners_with_their_own_z():
    floors = by_plot(one_plot_plan())["101"]["floors"]
    assert floors["generic"]["z"] == {"value": 10.0, "source": "computed"}
    assert floors["yard"]["z"] == {"value": 9.9, "source": "computed"}
    assert floors["generic"]["outline"]["value"] == {"loops": [{"points": [[1000.0, 2000.0], [1010.0, 2000.0], [1010.0, 2010.0], [1000.0, 2010.0]], "arcs": []}]}
    assert floors["yard"]["type"]["value"] == "Yard Y"
    assert floors["ramp"]["type"]["value"] == "Ramp B"
    assert [corner[2] for corner in floors["ramp"]["corners"]["value"]] == [9.8, 9.81, 9.82, 9.83]


def test_r112_a_yard_in_two_pieces_keeps_the_sketch_loop_first_and_the_second_piece_from_the_top_face():
    floors = plot_floors("101", 0, 10, yard=False)
    two = flat(12, "Yard Y", None, rect(-5, 0, 0, 10), 9.9, level="L2")
    two["outline"] = [rect(-5, 20, 0, 30), rect(-5, 0, 0, 10)]
    plan = build([link("101")], floors + [two], [row("101")])
    loops = by_plot(plan)["101"]["floors"]["yard"]["outline"]["value"]["loops"]
    assert [loop["points"][0] for loop in loops] == [[995.0, 2000.0], [995.0, 2020.0]]
    assert all(loop["arcs"] == [] for loop in loops)


def test_r112_each_floor_holds_type_level_level_elevation_height_offset_outline_and_plot_value():
    floors = by_plot(one_plot_plan())["101"]["floors"]
    assert set(floors["generic"]) == {"type", "level", "levelElevation", "heightOffset", "outline", "plotValue", "z"}
    assert set(floors["yard"]) == {"type", "level", "levelElevation", "heightOffset", "outline", "plotValue", "z"}
    assert set(floors["ramp"]) == {"type", "level", "levelElevation", "heightOffset", "outline", "plotValue", "corners"}
    assert floors["generic"]["level"]["value"] == "L1" and floors["yard"]["level"]["value"] == "L2"
    assert floors["generic"]["heightOffset"]["value"] == 10.0
    # The yard is 0.1 below the FFL on a level at 4 m
    assert floors["yard"]["heightOffset"]["value"] == 5.9
    # A ramp sits on its lowest corner, the slab shape editor lifts the others
    assert floors["ramp"]["heightOffset"]["value"] == 9.8
    assert {floor["plotValue"]["value"] for floor in floors.values()} == {"VD2-101"}


def test_r112_an_arc_edge_keeps_centre_radius_and_direction_in_shared_coordinates():
    floors = plot_floors("101", 0, 10)
    # Edge 1 from (10, 0) to (10, 10) bulges out as a half circle round (10, 5)
    floors[0]["arcs"] = [{"edge": 1, "center": [10.0, 5.0, 150.0], "radius": 5.0, "ccw": True}]
    floors[0]["sketchBoundary"] = [[0, 0, 150], [10, 0, 150], [10, 10, 150], [0, 10, 150]]
    plan = build([link("101")], floors, [row("101")])
    loop = by_plot(plan)["101"]["floors"]["generic"]["outline"]["value"]["loops"][0]
    assert loop["arcs"] == [{"edge": 1, "center": [1010.0, 2005.0], "radius": 5.0, "ccw": True}]


def test_r112_an_arc_whose_ends_are_off_its_circle_or_on_no_edge_keeps_the_floor_out():
    for arc in ({"edge": 1, "center": [10.0, 5.0], "radius": 4.0, "ccw": True}, {"edge": 9, "center": [10.0, 5.0], "radius": 5.0, "ccw": True},
                {"edge": 1, "center": [10.0, 5.0], "radius": 5.0}):
        floors = plot_floors("101", 0, 10)
        floors[0]["sketchBoundary"] = [[0, 0, 150], [10, 0, 150], [10, 10, 150], [0, 10, 150]]
        floors[0]["arcs"] = [arc]
        plan = build([link("101")], floors, [row("101")])
        assert "generic" not in by_plot(plan)["101"]["floors"]
        assert "arc" in missing_of(plan, "101")["floors.generic"]


def test_r112_a_floor_on_another_level_than_the_config_is_listed_and_not_written():
    floors = plot_floors("101", 0, 10)
    floors[0]["level"] = "L2"
    plan = build([link("101")], floors, [row("101")])
    assert "generic" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.generic"] == "floor 10 is on level L2, floors.levels.generic is L1"


def test_r112_a_floor_type_the_config_does_not_name_is_no_floor_of_the_plan():
    floors = plot_floors("101", 0, 10)
    floors[1]["typeName"] = "Ramp C"
    plan = build([link("101")], floors, [row("101")])
    assert "ramp" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.ramp"] == "no floor names the plot"


def test_r112_ramp_corners_that_are_not_its_outline_vertices_keep_the_ramp_out():
    floors = plot_floors("101", 0, 10)
    floors[1]["corners"][2][0] += 0.01
    plan = build([link("101")], floors, [row("101")])
    assert missing_of(plan, "101")["floors.ramp"] == "floor 11 has corners that are not the vertices of its outline"


def test_r112_a_past_plot_value_that_differs_is_a_conflict():
    floors = plot_floors("101", 0, 10)
    floors[0]["parameters"] = {"Plot": "DP4-101"}
    plan = build([link("101")], floors, [row("101")])
    assert {"plot": "101", "field": "floors.generic.plotValue", "kept": "VD2-101", "other": "DP4-101", "otherSource": "past floor Plot"} in plan["conflicts"]


def test_r112_the_summary_counts_the_floor_types_per_kind():
    plan = one_plot_plan()
    assert plan["summary"]["floorTypes"] == {"generic: Slab G": 1, "ramp: Ramp B": 1, "yard: Yard Y": 1}


def test_r112_the_plan_names_the_floor_types_levels_and_plot_parameter_for_the_placer():
    plan = one_plot_plan()
    assert plan["floorSettings"] == {"types": FLOORS["types"], "levels": FLOORS["levels"], "plotParameter": "Plot"}


def test_r112_settings_floors_block_is_checked_and_nothing_is_guessed(tmp_path):
    def settings_with(floors):
        path = tmp_path / "project.json"
        path.write_text(json.dumps({"floors": floors}), encoding="utf-8")
        return project_profile.load_settings(path)
    assert pf.load_floors(settings_with(FLOORS)) == FLOORS
    bad = [None, {k: v for k, v in FLOORS.items() if k != "levels"}, dict(FLOORS, types={"generic": ["A"], "yard": ["A"], "ramp": ["B"]}),
           dict(FLOORS, types={"generic": [], "yard": ["Y"], "ramp": ["B"]}), dict(FLOORS, levels={"generic": "L1", "yard": "", "ramp": "L1"}),
           dict(FLOORS, yardBelowFflM=-0.1), dict(FLOORS, yardShareTie=2), dict(FLOORS, plotParameter="")]
    for floors in bad:
        with pytest.raises(project_profile.ProfileError):
            pf.load_floors(settings_with(floors))


def test_r112_the_real_settings_name_the_five_floor_types_and_no_python_file_does():
    floors = pf.load_floors(project_profile.load_settings())
    names = [name for kind in pf.FLOOR_KINDS for name in floors["types"][kind]]
    assert len(names) == 5
    for path in (project_profile.ROOT / "python").glob("*.py"):
        if path.name == "link_summary.py":
            continue
        text = path.read_text(encoding="utf-8")
        # A type name that is also the name of a kind, such as yard, cannot be told from the kind in code
        assert not [name for name in names if name not in pf.FLOOR_KINDS and '"' + name + '"' in text], path.name


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
    floors = [floor for floor in plot_floors("101", 0, 10) if floor["typeName"] != "Slab G"]
    plan = build([link("101")], floors, [row("101")])
    assert "generic" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.generic"] == "no floor names the plot"
    assert plan["missing"][0]["written"] is True
    assert plan["summary"]["missingByKey"] == {"floors.generic": 1}


def test_r81_two_ramps_for_one_plot_are_listed_and_not_chosen():
    floors = plot_floors("101", 0, 10) + [ramp(99, "101", [shared(0, 20, 1), shared(1, 20, 1), shared(1, 21, 1), shared(0, 21, 1)])]
    plan = build([link("101")], floors, [row("101")])
    assert "ramp" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.ramp"].startswith("2 floors")


def test_r111_the_yard_goes_to_the_plot_whose_baseplan_face_holds_most_of_it():
    plan = one_plot_plan()
    assert plan["summary"]["yardStatus"] == {"assigned": 1}
    assert plan["summary"]["floorsFound"] == {"generic": 1, "yard": 1, "ramp": 1}
    assert plan["summary"]["yardRule"]["byStatus"] == {"assigned": 1}
    assert plan["summary"]["yardRule"]["withNoPlanPlot"] == 0


def test_r111_a_yard_that_touches_two_plots_goes_to_the_larger_share_and_r58_touching_does_not_decide():
    # The face of 101 runs to x 45 here, and its yard reaches 1 m into the face of 102, which starts at x 45
    faces = yard_rule.face_shapes([face("101", 0, x1=45), face("102", 50)], 0)[0]
    floors = plot_floors("101", 0, 10) + plot_floors("102", 50, 20)
    floors[2]["outline"] = [rect(-5, 0, 46, 10)]
    floors[2]["sketchBoundary"] = internal(rect(-5, 0, 46, 10))
    plan = build([link("101"), link("102", x=50)], floors, [row("101"), row("102")], faces=faces)
    assert "yard" in by_plot(plan)["101"]["floors"]
    assert by_plot(plan)["102"]["floors"]["yard"]["outline"]["value"]["loops"][0]["points"][0] == [1045.0, 2000.0]


def test_r111_a_tie_within_the_limit_is_listed_for_both_plots_and_never_guessed():
    floors = plot_floors("101", 0, 10, yard=False) + plot_floors("102", 50, 20, yard=False)
    # 5 m on the face of 101 (to x 12) and 5 m on the face of 102 (from x 45), the gap between holds no face
    floors.append(flat(70, "Yard Y", None, rect(7, 0, 50, 10), 9.9, level="L2"))
    plan = build([link("101"), link("102", x=50)], floors, [row("101"), row("102")])
    for plot in ("101", "102"):
        assert "yard" not in by_plot(plan)[plot]["floors"]
        assert missing_of(plan, plot)["floors.yard"] == "tie, yards 70 hold shares within 0.01 of another plot"
    assert plan["summary"]["yardRule"]["byStatus"] == {"tie": 1}


def test_r111_a_yard_mostly_off_every_usable_face_goes_to_no_plot_and_says_low_share():
    floors = plot_floors("101", 0, 10)
    # 2 m of the 20 m yard lie on the face of 101, the rest on no face with a usable label
    floors[2]["outline"] = [rect(-20, 0, 0, 10)]
    floors[2]["sketchBoundary"] = internal(rect(-20, 0, 0, 10))
    faces = yard_rule.face_shapes([face("101", 3)], 0)[0]
    plan = build([link("101")], floors, [row("101")], faces=faces)
    assert "yard" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.yard"] == "lowShare, yards 12 hold less than 0.5 of their area on the face of this plot"
    assert plan["summary"]["yardRule"]["byStatus"] == {"lowShare": 1}


def test_r112_an_arc_edge_is_not_taken_for_a_second_piece_when_the_top_face_cuts_it_into_chords():
    floors = plot_floors("101", 0, 10)
    floors[0]["sketchBoundary"] = [[0, 0, 150], [10, 0, 150], [10, 10, 150], [0, 10, 150]]
    floors[0]["arcs"] = [{"edge": 1, "center": [10.0, 5.0], "radius": 5.0, "ccw": True}]

    bulge = [[OFFSET[0] + 10 + 5 * math.sin(math.pi * step / 36), OFFSET[1] + 5 - 5 * math.cos(math.pi * step / 36), 10.0] for step in range(37)]
    floors[0]["outline"] = [[[OFFSET[0], OFFSET[1], 10.0]] + bulge + [[OFFSET[0], OFFSET[1] + 10, 10.0]]]
    plan = build([link("101")], floors, [row("101")])
    loops = by_plot(plan)["101"]["floors"]["generic"]["outline"]["value"]["loops"]
    assert len(loops) == 1 and len(loops[0]["arcs"]) == 1


def test_r111_a_plot_whose_face_holds_no_yard_is_listed_with_why():
    floors = plot_floors("101", 0, 10, yard=False)
    plan = build([link("101")], floors, [row("101")])
    assert missing_of(plan, "101")["floors.yard"] == "no yard has its largest share on the baseplan face of this plot"
    plan = build([link("101")], plot_floors("101", 0, 10), [row("101")], faces={})
    assert missing_of(plan, "101")["floors.yard"] == "no baseplan face carries the label of this plot"
    assert plan["summary"]["yardRule"]["left"] == [{"elementId": 12, "status": "none", "plot": None, "candidates": []}]


def test_r111_two_yards_on_one_plot_are_listed_and_neither_is_chosen():
    floors = plot_floors("101", 0, 10) + [flat(50, "Yard Y", None, rect(-5, 0, -1, 10), 9.9, level="L2")]
    floors[2]["outline"] = [rect(-1, 0, 0, 10)]
    floors[2]["sketchBoundary"] = internal(rect(-1, 0, 0, 10))
    plan = build([link("101")], floors, [row("101")])
    assert "yard" not in by_plot(plan)["101"]["floors"]
    assert missing_of(plan, "101")["floors.yard"] == "2 yards have their largest share on this plot: 12 50"
    assert plan["summary"]["yardStatus"] == {"several": 1}


def test_r111_a_yard_whose_plot_is_not_in_the_plan_is_counted():
    faces = yard_rule.face_shapes([face("101", 0), face("336", -20, x1=0)], 0)[0]
    floors = plot_floors("101", 0, 10)
    floors[2]["outline"] = [rect(-12, 0, -1, 10)]
    floors[2]["sketchBoundary"] = internal(rect(-12, 0, -1, 10))
    plan = build([link("101")], floors, [row("101")], faces=faces)
    rule = plan["summary"]["yardRule"]
    assert rule["withNoPlanPlot"] == 1
    assert rule["left"][0]["status"] == "plotNotInPlan" and rule["left"][0]["plot"] == "336"


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
    assert report["mismatches"] == {"link": 0, "file": 1, "position": 1, "rotation": 1, "mirrored": 1, "linkName": 0, "parameters": 1}
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


def test_r81_two_schedule_rows_for_one_plot_are_listed_and_never_picked():
    first, second = row("101", ffl=10.0), row("101", ffl=10.5)
    first["sheetRow"], second["sheetRow"] = 40, 41
    plan = build([link("101")], plot_floors("101", 0, 10), [first, second])
    assert plan["plots"] == []
    assert missing_of(plan, "101")["scheduleRow"] == "2 schedule rows hold the plot, sheet rows 40 41"


def test_r81_a_plot_number_that_is_not_whole_joins_nothing_and_is_listed():
    bad = row("101")
    bad["Plot No."] = 101.5
    bad["sheetRow"] = 40
    plan = build([link("101")], plot_floors("101", 0, 10), [bad])
    assert plan["plots"] == []
    assert plan["summary"]["scheduleRowsWithBadPlotNumber"] == [{"sheetRow": 40, "plotNo": "101.5"}]
    odd = build([link("10x")], [], [])
    assert odd["summary"]["linksWithBadPlotNumber"][0]["plotNumber"] == "10x"


def test_r81_levels_need_real_numbers_and_steps_a_whole_count():
    plan = build([link("101")], plot_floors("101", 0, 10), [row("101", ssl="1,234", steps="2.5")])
    missing = missing_of(plan, "101")
    assert "levels.ssl" in missing
    assert "levels.steps" in missing
    assert by_plot(build([link("101")], plot_floors("101", 0, 10), [row("101", steps="2 step")]))["101"]["levels"]["steps"]["value"] == 2


def test_r81_a_partial_transform_is_listed_under_transform_and_does_not_crash():
    for origin in ([1.0, 2.0], [1.0, 2.0, None]):
        broken = link("101")
        broken["transform"]["origin"] = origin
        plan = build([broken], plot_floors("101", 0, 10), [row("101")])
        assert plan["plots"] == []
        assert "transform" in missing_of(plan, "101")


def test_r80_a_rotation_that_rounds_to_360_is_written_as_0():
    assert pf.plan_rotation(359.9996) == 0
    assert pf.plan_rotation(-30.0) == 330.0
    entry = by_plot(one_plot_plan(rotation=359.9996))["101"]
    assert entry["rotationDeg"]["value"] == 0


def test_r80_an_angle_to_true_north_the_reader_did_not_give_is_left_out():
    data = document([link("101")], plot_floors("101", 0, 10))
    del data["documents"][0]["angleToTrueNorthDeg"]
    plan = pf.build_plan(data, [row("101")], JOB, FILES, "C:\\Prototypes", FLOORS, FACES)
    assert "angleToTrueNorthDeg" not in plan


def test_r81_sources_name_the_prototypes_file_that_was_read(tmp_path, monkeypatch):
    from openpyxl import Workbook
    root = tmp_path / "input"
    (root / "01_Output" / "1.11 References").mkdir(parents=True)
    (root / "Prototypes").mkdir()
    (root / "Prototypes" / "KEO-ARC-VD2-ZZ.rvt").write_text("x", encoding="utf-8")
    book = Workbook()
    sheet = book.active
    sheet.title = "Sheet1"
    sheet.append(["Plot No.", "Unit", "FFL", "SSL", "El @ Entry*", "Height**", "Steps Needed"])
    sheet.append([101, "VD2", 10.0, 9.925, 9.8123, 0.1877, "1 steps"])
    book.save(root / "01_Output" / "1.11 References" / "book.xlsx")
    prototypes = tmp_path / "other_prototypes.json"
    prototypes.write_text(json.dumps(document([link("101")], plot_floors("101", 0, 10))), encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    # Labels sit 10 below the official numbers, as the shift of AAA says
    baseplan.write_text(json.dumps({"plots": [dict(face("91", 0))]}), encoding="utf-8")
    shifts = tmp_path / "plot_numbers.json"
    shifts.write_text(json.dumps({"neighbourhoods": {"AAA": {"shift": 10}}, "sourceCodePattern": "-(AAA)-"}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"schedules": ["book.xlsx"], "inputRoot": str(root), "pastJob": JOB, "floors": FLOORS}), encoding="utf-8")
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    out = tmp_path / "plan.json"
    assert pf.main(["--settings", str(settings), "--prototypes", str(prototypes), "--baseplan", str(baseplan), "--shifts", str(shifts), "--out", str(out)]) == 0
    plan = json.loads(out.read_text(encoding="utf-8"))
    assert plan["sources"]["prototypes"] == "other_prototypes.json"
    assert plan["sources"]["baseplanPlots"] == "plots.json"
    assert plan["summary"]["written"] == 1
    assert set(plan["plots"][0]["floors"]) == {"generic", "yard", "ramp"}
    shifts.write_text(json.dumps({"neighbourhoods": {"BBB": {"shift": 10}}, "sourceCodePattern": "-(BBB)-"}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="no shift for AAA"):
        pf.main(["--settings", str(settings), "--prototypes", str(prototypes), "--baseplan", str(baseplan), "--shifts", str(shifts), "--out", str(out)])


def test_r81_a_floor_whose_plot_number_is_not_whole_joins_nothing():
    plan = build([link("101")], plot_floors("101.5", 0, 10), [row("101")])
    entry = by_plot(plan)["101"]
    assert "generic" not in entry["floors"]
    assert "ramp" not in entry["floors"]
    assert {item["plotNumber"] for item in plan["summary"]["floorsWithBadPlotNumber"]} == {"101.5"}


def test_r81_a_plot_number_with_leading_zeros_joins_its_plot():
    assert pf.plot_number("0101") == "101"
    assert pf.plot_number("\u00b2") is None
    plan = build([link("0101")], plot_floors("101", 0, 10), [row("101")])
    assert [entry["plot"] for entry in plan["plots"]] == ["101"]


def test_r89_a_placed_link_whose_plot_number_is_not_whole_is_listed():
    links = [link("101")]
    floors = plot_floors("101", 0, 10)
    plan = build(links, floors, [row("101")])
    odd = link("101")
    odd["parameters"]["RRE_ZZ_SP_Plot-Number"] = "101.5"
    odd["elementId"] = 77
    report = plan_compare.compare(plan, placed_model(links + [odd], floors))
    assert report["linksWithBadPlotNumber"] == [{"elementId": 77, "plotNumber": "101.5"}]
    assert report["fullMatch"] == 1


def test_r81_the_link_name_is_the_instance_name_of_the_past_link():
    entry = by_plot(one_plot_plan(name="101 | VD2 | 1 | XX | XX"))["101"]
    assert entry["linkName"] == {"value": "101 | VD2 | 1 | XX | XX", "source": "pastJob"}


def test_r81_a_link_name_that_differs_from_rvt_name_is_a_conflict_and_the_name_is_kept():
    plan = one_plot_plan(name="101 | VD2 | 2")
    assert by_plot(plan)["101"]["linkName"]["value"] == "101 | VD2 | 2"
    assert {"plot": "101", "field": "linkName", "kept": "101 | VD2 | 2", "other": "101 | VD2", "otherSource": "RRE_ZZ_SP_RVT-Name"} in plan["conflicts"]


def test_r81_a_link_with_no_instance_name_is_written_and_lists_link_name_as_missing():
    plan = one_plot_plan(name="")
    assert "linkName" not in by_plot(plan)["101"]
    assert "linkName" in missing_of(plan, "101")


def test_r89_a_link_name_that_differs_is_its_own_mismatch():
    links = [link("101")]
    floors = plot_floors("101", 0, 10)
    plan = build(links, floors, [row("101")])
    report = plan_compare.compare(plan, placed_model([link("101", name="1")], floors))
    assert report["mismatches"]["linkName"] == 1
    assert report["differing"] == [{"plot": "101", "differs": {"linkName": "link name differs"}}]


def test_r109_the_placer_folder_is_the_upgraded_one_and_the_past_folder_stays_the_source(tmp_path):
    job = dict(JOB, prototypeFolder="Prototypes", upgradedPrototypeFolder="Prototypes_2024")
    place, source = pf.prototype_folders(tmp_path, job)
    assert place == tmp_path / "Prototypes_2024"
    assert source == tmp_path / "Prototypes"
    place, source = pf.prototype_folders(tmp_path, dict(JOB, prototypeFolder="Prototypes"))
    assert place == source == tmp_path / "Prototypes"


def test_r109_the_real_settings_keep_the_2021_folder_as_source_and_name_the_2024_copies_beside_it():
    job = pf.load_job(project_profile.load_settings())
    assert job["prototypeFolder"].endswith("Prototypes")
    assert job["upgradedPrototypeFolder"] == job["prototypeFolder"] + "_2024"


def test_r81_the_prototypes_source_is_named_by_its_file_name_on_any_system():
    plan = pf.build_plan(document([link("101")], plot_floors("101", 0, 10)), [row("101")], dict(JOB, prototypes=r"C:\Docs\SiteAdaptation\prototypes.json"), FILES, r"C:\Prototypes", FLOORS, FACES)
    assert plan["sources"]["prototypes"] == "prototypes.json"
