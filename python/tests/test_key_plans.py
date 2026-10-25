"""Round ADDIN Phase 2b: the plans of the answer keys (R325).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import json
import math

import pytest
from shapely.geometry import box

import plan_from_past as pf
import project_profile

FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A"]},
          "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}
FILE = "KEO-ARC-DP4-ZZ.rvt"
OTHER = "KEO-ARC-TH3-ZZ.rvt"
# Baseplan faces by label, each 10 m wide and 25 m deep, and the location plan number of each
FACES = {"11": box(0, 0, 10, 25), "12": box(10, 0, 20, 25), "13": box(20, 0, 30, 25)}
NUMBERS = {"11": "501", "12": "502", "13": "503"}
ENTRY = "El at entry"


def key_link(plot, x0, file=FILE, unit="DP4", name=None, steps="2", element_id=None, rotation=0.0, mirrored=False):
    """A link of a Parsons key: the plot number parameter, a Name with bars that is no floor Plot value, and its house 5 m along X
    and 12.5 m along Y from the origin at the face corner."""
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    parameters = {pf.PLOT_PARAMETER: plot, pf.UNIT_PARAMETER: unit, pf.STEPS_PARAMETER: steps,
                  "Name": name if name is not None else str(plot) + " | " + str(unit) + " | 1 | XX | XX"}
    parameters = {key: value for key, value in parameters.items() if value is not None}
    return {"file": file, "elementId": element_id or 7000 + x0, "parameters": parameters,
            "transform": {"origin": [x0, 0.0, 7.0], "rotationDeg": rotation, "mirrored": mirrored,
                          "axisX": [math.cos(turn), math.sin(turn), 0.0], "axisY": [-sign * math.sin(turn), sign * math.cos(turn), 0.0], "axisZ": [0, 0, 1]}}


def generic(value, x0, element_id=None, top=7.0, entry=6.8123):
    return {"typeName": "Slab G", "elementId": element_id or 8000 + x0, "parameters": {"Plot": value, ENTRY: entry},
            "outline": [[[x0, 0, top], [x0 + 10, 0, top], [x0 + 10, 25, top], [x0, 25, top]]], "topElevationRange": {"min": top, "max": top},
            "level": "L1", "levelElevation": 0.0, "sketchBoundary": [[x0, 0, 0], [x0 + 10, 0, 0], [x0 + 10, 25, 0], [x0, 25, 0]]}


def model(links, floors, walls=None):
    return {"documents": [{"kind": "project", "file": "X-IMX-model_TEST-KX", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
                           "angleToTrueNorthDeg": 0.0, "links": links, "floors": floors, "walls": walls or []}]}


def mapped(data):
    return [link["parameters"].get(pf.PLOT_PARAMETER) for link in pf.host_of(data)["links"]]


def test_r325_a_link_takes_the_floor_whose_plot_value_ends_in_its_plot_number_before_its_name_or_frame():
    links = [key_link("501", 0), key_link("502", 10)]
    data, rows, report = pf.names_dialect(model(links, [generic("DP4-501", 0), generic("DP4-502", 10)]), FLOORS, FACES, NUMBERS)
    assert mapped(data) == ["501", "502"]
    assert report["link mapped by parameter"] == 2 and "link with no mapped name" not in report
    assert [row["Plot No."] for row in rows] == [501, 502]


def test_r325_a_plot_number_two_floor_values_end_in_is_no_route_and_the_link_falls_to_its_frame():
    # Two floor values end in 502, so the parameter can not pick one, the house centre of the link stands on face 12
    links = [key_link("501", 0), key_link("502", 10)]
    floors = [generic("DP4-501", 0), generic("DP4-502", 10), generic("TH3-502", 20)]
    data, _, report = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS)
    assert mapped(data) == ["501", "502"]
    assert report["link mapped by parameter"] == 1 and report["link mapped by frame"] == 1


def test_r325_a_link_whose_plot_number_names_no_floor_takes_its_frame_learned_on_the_links_mapped_by_parameter():
    # The frame of the file is learned on the link of plot 501, the link of the wrong plot 999 stands on face 13
    links = [key_link("501", 0), key_link("999", 20)]
    data, _, report = pf.names_dialect(model(links, [generic("DP4-501", 0), generic("DP4-503", 20)]), FLOORS, FACES, NUMBERS)
    assert mapped(data) == ["501", "503"]
    assert report["link mapped by parameter"] == 1 and report["link mapped by frame"] == 1


def test_r325_the_link_keeps_its_own_unit_and_a_link_with_none_takes_the_unit_of_its_floor():
    links = [key_link("501", 0, unit="VD4"), key_link("502", 10, unit=None)]
    data, _, _ = pf.names_dialect(model(links, [generic("DP4-501", 0), generic("DP4-502", 10)]), FLOORS, FACES, NUMBERS)
    units = [link["parameters"].get(pf.UNIT_PARAMETER) for link in pf.host_of(data)["links"]]
    assert units == ["VD4", "DP4"]


def test_r325_a_native_link_with_no_plot_number_still_maps_by_its_name_and_its_frame():
    # A Native key: the Name is the floor Plot value, no plot parameter, as R206 and R264 read it
    links = [key_link(None, 0, name="DP4-1"), key_link(None, 10, name="EL@-Entry +164.98")]
    data, _, report = pf.names_dialect(model(links, [generic("DP4-1", 0), generic("DP4-2", 10)]), FLOORS, FACES, NUMBERS)
    assert mapped(data) == ["501", "502"]
    assert report["link mapped"] == 1 and report["link mapped by frame"] == 1 and "link mapped by parameter" not in report


def test_r325_with_the_entry_parameter_a_row_holds_el_at_entry_of_its_generic_floor_and_the_steps_of_its_link():
    links = [key_link("501", 0, steps="2"), key_link("502", 10, steps="text")]
    floors = [generic("DP4-501", 0, entry=6.8123), generic("DP4-502", 10, entry="none")]
    _, rows, _ = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY)
    first, second = rows
    assert first["El @ Entry"] == 6.8123 and first["Steps Needed"] == "2" and first["modelPlot"] == "501"
    # A value that is no number stays out, so the plan lists it as missing and never guesses it
    assert "El @ Entry" not in second and second["Steps Needed"] == "text"
    _, plain, _ = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS)
    assert "El @ Entry" not in plain[0] and "Steps Needed" not in plain[0]


def test_r325_a_plan_of_a_key_holds_the_key_levels_its_height_computed_and_the_plot_number_of_the_key():
    links = [key_link("501", 0, steps="2")]
    data, rows, _ = pf.names_dialect(model(links, [generic("DP4-501", 0, top=7.0, entry=6.8123)]), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY)
    faces = {NUMBERS[label]: shape for label, shape in FACES.items()}
    plan = pf.build_plan(data, rows, dict(JOB), {FILE.lower()}, "C:\\Prototypes", FLOORS, faces)
    entry = plan["plots"][0]
    assert entry["levels"]["ffl"] == {"value": 7.0, "source": "pastJob"}
    assert entry["levels"]["entry"] == {"value": 6.8123, "source": pf.KEY_PARAMETER}
    assert entry["levels"]["steps"] == {"value": 2, "source": "pastJob"}
    assert entry["levels"]["height"] == {"value": 0.1877, "source": "computed"}
    assert entry["modelPlot"] == {"value": "501", "source": "pastJob"}
    missing = {item["key"] for row in plan["missing"] if row["plot"] == "501" for item in row["missing"]}
    # The key holds no SSL, so it is listed and never guessed, and the height is no longer missing
    assert "levels.ssl" in missing and "levels.height" not in missing


JOB = {"neighbourhood": "AAA", "schedule": "book.xlsx", "sheet": "Sheet1", "prototypes": "C:\\x\\prototypes.json",
       "prototypeFolder": "Prototypes", "output": "C:\\x\\plan.json", "parameters": [pf.PLOT_PARAMETER]}


def wall(element_id, start, end, type_name="BW"):
    return {"elementId": element_id, "typeName": type_name, "length": math.dist(start, end), "height": 1.8,
            "location": {"kind": "line", "start": [start[0], start[1], 0.0], "end": [end[0], end[1], 0.0]},
            "parameters": {"Base Constraint": "L1", "Base Offset": "0.000", "Top Constraint": "Unconnected", "Top Offset": "0.000", "Location Line": "3"}}


def test_r325_a_wall_goes_to_each_plot_whose_generic_floor_holds_its_middle_or_has_an_edge_within_the_distance():
    floors = [dict(generic("DP4-1", 0), plotNumber="501"), dict(generic("DP4-2", 10), plotNumber="502")]
    walls = [wall(1, (10.1, 2), (10.1, 20)),   # a party wall 0.1 m off the shared edge, both plots
             wall(2, (0.1, -0.5), (9.9, -0.5)),   # a front wall 0.5 m out, too far from every edge
             wall(3, (3, 5), (6, 5), "Garden"),     # a garden wall inside plot 501
             {"elementId": 4, "typeName": "BW", "location": {"kind": "arc"}, "parameters": {}}]
    found, summary = pf.key_walls(model([], floors, walls), FLOORS, 0.3)
    plots = {item["elementId"]: item["plots"] for item in found}
    assert plots == {1: ["501", "502"], 2: [], 3: ["501"], 4: []}
    assert found[0]["lengthM"] == 18.0 and found[0]["type"] == "BW" and found[0]["locationLine"] == "3"
    assert found[0]["start"] == [10.1, 2.0, 0.0] and found[3]["start"] is None
    assert summary == {"walls": 4, "withNoPlot": 2, "byPlotCount": {"0": 2, "1": 1, "2": 1}, "notALine": 1}


@pytest.mark.parametrize("value", [None, 0, -0.1, "0.3", True])
def test_r325_the_wall_edge_distance_must_be_metres_above_0(tmp_path, value):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"keyPlans": {"wallEdgeM": value}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="wallEdgeM"):
        pf.load_key_plans(project_profile.load_settings(path))


def test_r325_the_real_settings_name_the_wall_edge_distance_and_the_entry_parameter():
    settings = project_profile.load_settings()
    block = pf.load_key_plans(settings)
    assert 0 < block["wallEdgeM"] < 1
    assert block["entryParameter"] == settings.get("pastAll.entryParameter")
    assert block["flatTopM"] == settings.get("keyPlans.flatTopM")


def key_run(tmp_path, out_name="plan_imx_past.json"):
    """A made up key IMX with its work copy read, a baseplan of three faces and the location plan labels of them, through main."""
    root = tmp_path / "input"
    (root / "Prototypes").mkdir(parents=True)
    (root / "Prototypes" / FILE).write_bytes(b"x")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": label, "vertices": [list(xy) for xy in list(shape.exterior.coords)[:-1]]}
                                              for label, shape in FACES.items()]}), encoding="utf-8")
    labels = tmp_path / "labels.csv"
    labels.write_text("number,face\n" + "".join(number + "," + label + "\n" for label, number in NUMBERS.items()), encoding="utf-8")
    job = dict(JOB, output=str(tmp_path / "plan_past.json"), prototypeFolder="Prototypes")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"schedules": ["book.xlsx"], "inputRoot": str(root), "pastJob": job, "floors": FLOORS,
                                    "keyPlans": {"wallEdgeM": 0.3, "entryHeightM": {"min": 0.0, "max": 1.5}, "flatTopM": 0.0005, "minShare": 0.001},
                                    "cadLinks": {"cutShareMin": 0.9},
                                    "pastAll": {"entryParameter": ENTRY},
                                    "answerKeys": {"codeInName": "^X-{code}-", "keys": [{"code": "IMX", "copy": "X-IMX-model.rvt"}, {"code": "IMY", "copy": "X-IMY-model.rvt"}]}}),
                        encoding="utf-8")
    prototypes = tmp_path / "testhost_KX_prototypes.json"
    walls = [wall(1, (10.1, 2), (10.1, 20))]
    prototypes.write_text(json.dumps(model([key_link("501", 0), key_link("502", 10)], [generic("DP4-501", 0), generic("DP4-502", 10)], walls)), encoding="utf-8")
    out = tmp_path / out_name
    return ["--settings", str(settings), "--prototypes", str(prototypes), "--baseplan", str(baseplan), "--answer-key", "--names", str(labels),
            "--key", "IMX", "--out", str(out)], out, job


def test_r325_main_names_the_plan_by_its_key_and_keeps_the_key_levels_and_the_walls_with_their_plots(tmp_path, monkeypatch):
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, out, _ = key_run(tmp_path)
    assert pf.main(args) == 0
    plan = json.loads(out.read_text(encoding="utf-8"))
    assert plan["neighbourhood"] == "IMX" and plan["sources"]["answerKey"] == "IMX" and plan["answerKeyOnly"] is True
    assert [entry["plot"] for entry in plan["plots"]] == ["501", "502"]
    assert plan["plots"][0]["levels"]["entry"] == {"value": 6.8123, "source": pf.KEY_PARAMETER}
    assert plan["walls"][0]["plots"] == ["501", "502"] and plan["summary"]["walls"]["walls"] == 1
    assert plan["summary"]["namesDialect"]["link mapped by parameter"] == 2


def test_r325_main_never_writes_the_plan_of_a_key_over_the_plan_of_the_past_job(tmp_path, monkeypatch):
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, _, job = key_run(tmp_path)
    args[args.index("--out") + 1] = job["output"]
    with pytest.raises(SystemExit):
        pf.main(args)
    assert not (tmp_path / "plan_past.json").exists()


def test_r325_an_official_number_two_generic_floors_stand_on_takes_out_both_floors_and_their_links():
    # Faces 11 and 12 carry the same location plan number, so neither floor can say which plot is 501
    numbers = {"11": "501", "12": "501", "13": "503"}
    links = [key_link("501", 0), key_link("502", 10), key_link("503", 20)]
    floors = [generic("DP4-501", 0), generic("DP4-502", 10), generic("DP4-503", 20)]
    data, rows, report = pf.names_dialect(model(links, floors), FLOORS, FACES, numbers, entry_parameter=ENTRY)
    assert [row["Plot No."] for row in rows] == [503]
    assert mapped(data) == [None, None, "503"]
    assert report["plot value or official number given twice"] == 1 and report["link with no mapped name"] == 2
    assert [floor.get("plotNumber") for floor in pf.host_of(data)["floors"]] == [None, None, "503"]


def test_r325_a_floor_whose_plot_value_has_no_official_number_names_no_plot_by_its_own_number():
    # Face 11 carries no plan label, so plot 501 has no official number, and its ramp must not join a plot by its own 501
    import link_summary
    numbers = {"12": "502", "13": "503"}
    ramp = dict(generic("DP4-501", 0), typeName="Ramp A", plotNumber="501")
    data, rows, report = pf.names_dialect(model([key_link("502", 10)], [generic("DP4-501", 0), ramp, generic("DP4-502", 10)]), FLOORS, FACES, numbers)
    floors = pf.host_of(data)["floors"]
    assert [link_summary.floor_plot(floor) for floor in floors] == [None, None, "502"]
    assert floors[1]["modelPlot"] == "DP4-501" and report["floor with no official number"] == 2


def test_r325_the_number_route_takes_the_whole_number_of_a_floor_value_so_vd2_0481_and_th1_481_both_end_in_481():
    # Two floor values end in plot 481 once the leading zero is gone, so the parameter 481 picks neither
    both = {"VD2-0481": "1481", "TH1-481": "1482"}
    assert pf.number_routes(both) == {}
    link = {"parameters": {pf.PLOT_PARAMETER: "481"}}
    assert pf.direct_value(link, both, pf.number_routes(both)) == (None, None)
    # One value that ends in 0481 is found by the link plot number 481, as direct_value cleans it
    one = {"VD2-0481": "1481"}
    assert pf.number_routes(one) == {"481": "VD2-0481"}
    assert pf.direct_value(link, one, pf.number_routes(one)) == ("VD2-0481", "parameter")


def test_r325_an_answer_key_plan_with_no_out_is_never_written_over_the_plan_of_the_past_job(tmp_path, monkeypatch):
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, _, _ = key_run(tmp_path)
    for flag in ("--key", "--out"):
        at = args.index(flag)
        del args[at:at + 2]
    with pytest.raises(SystemExit):
        pf.main(args)
    assert not (tmp_path / "plan_past.json").exists()


def test_r325_a_key_plan_is_never_written_under_the_name_of_another_key_or_over_the_plan_of_another_key(tmp_path, monkeypatch):
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, _, _ = key_run(tmp_path / "first", out_name="plan_imy_key_past.json")
    with pytest.raises(SystemExit):
        pf.main(args)
    assert not (tmp_path / "first" / "plan_imy_key_past.json").exists()
    # A file of the right name that holds the plan of another key stays as it is
    args, out, _ = key_run(tmp_path / "second", out_name="plan_imx_key_past.json")
    out.write_text(json.dumps({"sources": {"answerKey": "IMY"}, "plots": []}), encoding="utf-8")
    before = out.read_bytes()
    with pytest.raises(SystemExit):
        pf.main(args)
    assert out.read_bytes() == before
    # The plan of the same key is written again
    out.write_text(json.dumps({"sources": {"answerKey": "IMX"}, "plots": []}), encoding="utf-8")
    assert pf.main(args) == 0 and json.loads(out.read_text(encoding="utf-8"))["plots"]


def test_r325_a_key_plan_labels_el_at_entry_by_the_key_parameter_and_a_plan_of_the_past_job_keeps_pastjob_for_its_schedule():
    links = [key_link("501", 0)]
    data, rows, _ = pf.names_dialect(model(links, [generic("DP4-501", 0, top=7.0, entry=6.8123)]), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY)
    faces = {NUMBERS[label]: shape for label, shape in FACES.items()}
    plan = pf.build_plan(data, rows, dict(JOB), {FILE.lower()}, "C:\\Prototypes", FLOORS, faces)
    assert plan["plots"][0]["levels"]["entry"] == {"value": 6.8123, "source": pf.KEY_PARAMETER}
    schedule = [{"Plot No.": 501, "Unit": "DP4", "FFL": 7.0, "SSL": 6.95, "El @ Entry": 6.8123, "Height": 0.1877, "Steps Needed": "2", "sheetRow": 5}]
    data, _, _ = pf.names_dialect(model(links, [generic("DP4-501", 0)]), FLOORS, FACES, NUMBERS)
    plan = pf.build_plan(data, schedule, dict(JOB), {FILE.lower()}, "C:\\Prototypes", FLOORS, faces)
    assert plan["plots"][0]["levels"]["entry"] == {"value": 6.8123, "source": "pastJob"}


def test_r325_an_el_at_entry_parameter_whose_height_is_outside_the_band_is_listed_with_its_reason_and_not_used():
    # 150.0 is a placeholder, it puts El @ Entry 143 m over the floor top, so it is refused
    links = [key_link("501", 0), key_link("502", 10)]
    floors = [generic("DP4-501", 0, top=7.0, entry=6.8123), generic("DP4-502", 10, top=7.0, entry=150.0)]
    data, rows, report = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY, entry_heights={"min": 0.0, "max": 1.5})
    assert rows[0]["El @ Entry"] == 6.8123 and "El @ Entry" not in rows[1]
    assert report["El @ Entry refused, its height is outside keyPlans.entryHeightM"] == 1
    faces = {NUMBERS[label]: shape for label, shape in FACES.items()}
    plan = pf.build_plan(data, rows, dict(JOB), {FILE.lower()}, "C:\\Prototypes", FLOORS, faces)
    second = plan["plots"][1]["levels"]
    assert "entry" not in second and "height" not in second and second["ffl"]["value"] == 7.0
    why = {item["key"]: item["why"] for row in plan["missing"] if row["plot"] == "502" for item in row["missing"]}
    assert why["levels.entry"].startswith(pf.REFUSED) and "outside keyPlans.entryHeightM" in why["levels.entry"]
    assert why["levels.height"] == "no El @ Entry to work the height out from"


@pytest.mark.parametrize("value", [None, {"min": 0.0}, {"min": 1.5, "max": 0.0}, {"min": "0", "max": 1.5}, [0.0, 1.5]])
def test_r325_the_entry_height_band_must_be_a_min_under_a_max_in_metres(tmp_path, value):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"keyPlans": {"wallEdgeM": 0.3, "entryHeightM": value}, "pastAll": {"entryParameter": ENTRY},
                                "cadLinks": {"cutShareMin": 0.9}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="entryHeightM"):
        pf.load_key_plans(project_profile.load_settings(path))


def shifted(floor, x0):
    """The floor moved to start at x0, outline and sketch alike."""
    floor["outline"] = [[[x0, 0, 7.0], [x0 + 10, 0, 7.0], [x0 + 10, 25, 7.0], [x0, 25, 7.0]]]
    floor["sketchBoundary"] = [[x0, 0, 0], [x0 + 10, 0, 0], [x0 + 10, 25, 0], [x0, 25, 0]]
    return floor


def test_r325_a_generic_floor_joins_a_face_only_by_an_area_share_of_cut_share_min_and_a_50_50_floor_is_listed_as_cut():
    # The floor of 503 lies half on face 12 and half on face 13, the floor of 502 holds 0.95 of its area on face 12
    links = [key_link("501", 0), key_link("503", 15), key_link("502", 10.5)]
    floors = [generic("DP4-501", 0), generic("DP4-503", 15), shifted(generic("DP4-502", 30), 10.5)]
    data, rows, report = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY, share_min=0.9)
    assert [row["Plot No."] for row in rows] == [501, 502]
    assert report["cutPlots"] == [{"modelPlot": "DP4-503", "why": "cut", "shares": [{"face": "12", "number": "502", "share": 0.5},
                                                                                     {"face": "13", "number": "503", "share": 0.5}]}]
    assert report["generic floor cut by the baseplan"] == 1
    assert mapped(data)[1] is None
    # A floor 0.6 on face 12 and 0.4 on face 13 is cut too. Without the share a point inside it joins face 12 by chance, as R206 does
    # for the Native keys
    leaning = [generic("DP4-501", 0), shifted(generic("DP4-503", 99), 14)]
    _, rows, report = pf.names_dialect(model(links, leaning), FLOORS, FACES, NUMBERS, share_min=0.9)
    assert [row["Plot No."] for row in rows] == [501] and report["cutPlots"][0]["shares"][0]["share"] == 0.6
    # names_dialect writes the official number into the floors it is given, so the second run takes fresh ones
    _, plain, _ = pf.names_dialect(model(links, [generic("DP4-501", 0), shifted(generic("DP4-503", 99), 14)]), FLOORS, FACES, NUMBERS)
    assert [row["Plot No."] for row in plain] == [501, 502]


def test_r325_main_joins_by_the_cut_share_of_the_settings_and_lists_the_cut_plots(tmp_path, monkeypatch):
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, out, _ = key_run(tmp_path)
    prototypes = tmp_path / "testhost_KX_prototypes.json"
    data = json.loads(prototypes.read_text(encoding="utf-8"))
    data["documents"][0]["links"].append(key_link("503", 25))
    data["documents"][0]["floors"].append(generic("DP4-503", 25))
    prototypes.write_text(json.dumps(data), encoding="utf-8")
    assert pf.main(args) == 0
    plan = json.loads(out.read_text(encoding="utf-8"))
    assert [entry["plot"] for entry in plan["plots"]] == ["501", "502"]
    assert [item["modelPlot"] for item in plan["summary"]["namesDialect"]["cutPlots"]] == ["DP4-503"]


def test_r325_the_flat_top_of_a_generic_floor_is_held_within_key_plans_flat_top_m_and_a_sloped_floor_gives_no_el_at_entry():
    links = [key_link("501", 0), key_link("502", 10)]
    nearly = dict(generic("DP4-501", 0, entry=6.8), topElevationRange={"min": 7.0, "max": 7.005})
    sloped = dict(generic("DP4-502", 10, entry=6.8), topElevationRange={"min": 7.0, "max": 7.05})
    _, rows, _ = pf.names_dialect(model(links, [nearly, sloped]), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY, entry_heights={"min": 0.0, "max": 1.5},
                                  flat_top=0.008)
    assert rows[0]["FFL"] == 7.005 and rows[0]["El @ Entry"] == 6.8
    assert rows[1]["FFL"] is None and "El @ Entry" not in rows[1] and "no flat floor top" in rows[1][pf.ENTRY_REFUSED]


@pytest.mark.parametrize("value", [None, -0.001, 0.01, "0", True])
def test_r325_the_flat_top_tolerance_must_be_metres_from_0_to_under_0_01(tmp_path, value):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"keyPlans": {"wallEdgeM": 0.3, "entryHeightM": {"min": 0.0, "max": 1.5}, "flatTopM": value}, "pastAll": {"entryParameter": ENTRY},
                                "cadLinks": {"cutShareMin": 0.9}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="flatTopM"):
        pf.load_key_plans(project_profile.load_settings(path))


def test_r325_a_generic_floor_whose_face_has_no_plan_label_or_two_is_in_the_cut_list_of_r120_with_its_reason(tmp_path, monkeypatch):
    # Face 11 carries one plan label, 12 two and 13 none, each floor stands whole on its face
    links = [key_link("501", 0), key_link("502", 10), key_link("503", 20)]
    floors = lambda: [generic("DP4-501", 0), generic("DP4-502", 10), generic("DP4-503", 20)]
    _, rows, report = pf.names_dialect(model(links, floors()), FLOORS, FACES, {"11": "501"}, entry_parameter=ENTRY, share_min=0.9)
    assert [row["Plot No."] for row in rows] == [501]
    assert [(item["modelPlot"], item["why"]) for item in report["cutPlots"]] == [("DP4-502", "noNumber"), ("DP4-503", "noNumber")]
    _, _, report = pf.names_dialect(model(links, floors()), FLOORS, FACES, {"11": "501"}, entry_parameter=ENTRY, share_min=0.9, many={"12": 2})
    assert report["cutPlots"] == [{"modelPlot": "DP4-502", "why": "manyNumbers", "face": "12", "shares": [{"face": "12", "number": None, "share": 1.0}]},
                                  {"modelPlot": "DP4-503", "why": "noNumber", "face": "13", "shares": [{"face": "13", "number": None, "share": 1.0}]}]
    assert report["generic floor on no face with one plan label"] == 2
    # main reads the faces of two labels from labels.csv
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    args, out, _ = key_run(tmp_path)
    (tmp_path / "labels.csv").write_text("number,face\n501,11\n502,12\n602,12\n", encoding="utf-8")
    assert pf.main(args) == 0
    plan = json.loads(out.read_text(encoding="utf-8"))
    assert [entry["plot"] for entry in plan["plots"]] == ["501"]
    assert [(item["modelPlot"], item["why"]) for item in plan["summary"]["namesDialect"]["cutPlots"]] == [("DP4-502", "manyNumbers")]


def test_r325_a_generic_floor_that_only_touches_a_face_by_less_than_key_plans_min_share_is_listed_on_no_face():
    # The floor of 504 lies 0.003 m of its 10 m width on face 13, a share of 0.0003, and on no other face
    links = [key_link("501", 0), key_link("504", 30)]
    floors = [generic("DP4-501", 0), shifted(generic("DP4-504", 40), 29.997)]
    _, rows, report = pf.names_dialect(model(links, floors), FLOORS, FACES, NUMBERS, entry_parameter=ENTRY, share_min=0.9, min_share=0.001)
    assert [row["Plot No."] for row in rows] == [501]
    assert report["cutPlots"] == [{"modelPlot": "DP4-504", "why": "noFace", "shares": []}]
    _, _, report = pf.names_dialect(model(links, [generic("DP4-501", 0), shifted(generic("DP4-504", 40), 29.997)]), FLOORS, FACES, NUMBERS,
                                    entry_parameter=ENTRY, share_min=0.9, min_share=0.0002)
    assert report["cutPlots"][0]["why"] == "cut" and report["cutPlots"][0]["shares"] == [{"face": "13", "number": "503", "share": 0.0}]


@pytest.mark.parametrize("value", [None, 0, -0.001, 0.9, "0.001", True])
def test_r325_the_minimum_share_must_be_above_0_and_under_cut_share_min(tmp_path, value):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"keyPlans": {"wallEdgeM": 0.3, "entryHeightM": {"min": 0.0, "max": 1.5}, "flatTopM": 0.0005, "minShare": value},
                                "pastAll": {"entryParameter": ENTRY}, "cadLinks": {"cutShareMin": 0.9}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="minShare"):
        pf.load_key_plans(project_profile.load_settings(path))


def test_r325_the_real_settings_put_the_minimum_share_between_the_slivers_and_the_shares_of_the_keys():
    block = pf.load_key_plans(project_profile.load_settings())
    assert pf.SLIVER_SHARE < block["minShare"] < block["cutShareMin"]
