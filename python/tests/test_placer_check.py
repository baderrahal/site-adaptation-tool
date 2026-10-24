import copy
import math
from pathlib import Path

import pytest

import placer_check

FILE = "RRE-WP-NSP-KEO-ARC-C10-ZZ-MOD-1500.rvt"
OTHER = "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt"
PARAMETERS = ["RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "CASE"]
SURVEY = {"position": [0.0, 0.0, 0.0], "sharedPosition": [100.0, 200.0, 0.0]}


def axes(rotation, mirrored):
    turn = math.radians(rotation)
    sign = -1 if mirrored else 1
    return [math.cos(turn), math.sin(turn), 0.0], [-sign * math.sin(turn), sign * math.cos(turn), 0.0]


def link(plot, file=FILE, origin=(1.0, 2.0, 7.5), rotation=30.0, mirrored=False, extra=None):
    x, y = axes(rotation, mirrored)
    parameters = {"RRE_ZZ_SP_Plot-Number": plot, "RRE_ZZ_SP_Prototype": "C10", "CASE": "A", "RRE_ZZ_SP_Prototype-FFL": "7.5"}
    parameters.update(extra or {})
    return {
        "file": file,
        "rawName": file + " : " + plot + " | C10 : location <Not Shared>",
        "transform": {"origin": list(origin), "rotationDeg": rotation % 360.0, "mirrored": mirrored, "axisX": x, "axisY": y, "axisZ": [0.0, 0.0, 1.0]},
        "parameters": parameters,
        "loaded": True,
        "boundingBoxCorners": [[0, 0, 0]] * 8,
        "boxSource": "linkExtents",
    }


def model(links, spots=None):
    return {"documents": [{"kind": "project", "surveyPoint": SURVEY, "links": links, "spotElevations": spots or []}]}


def plan_entry(plot, file=FILE, ffl=7.5):
    return {"plot": plot, "prototypeFile": {"value": file}, "levels": {"ffl": {"value": ffl}}}


PLAN = {"plots": [plan_entry("1"), plan_entry("2"), plan_entry("3", file=OTHER)]}
REPORT = {"mode": "real", "stopped": False, "rolledBack": False, "counts": {"placed": 2}, "skipped": [], "issues": []}


def original():
    return model([link("1"), link("2", origin=(5.0, 6.0, 7.5), rotation=200.0, mirrored=True), link("3", file=OTHER)])


def check(test, report=REPORT, plan=PLAN):
    return placer_check.compare(plan, original(), test, FILE, PARAMETERS, report)


def test_r103_a_run_that_places_every_plot_as_the_answer_key_passes():
    test = copy.deepcopy(original())
    result = check(test)

    assert result["pass"] is True
    assert result["plots"] == {"expected": 2, "placed": 2, "missing": 0, "extra": 0, "placedTwice": 0, "unnumberedLinksOfThisFile": 0, "noSingleOriginal": 0}
    assert result["position"]["within1mm"] == 2
    assert (result["rotationEqual"], result["mirroredEqual"], result["zEqualFfl"], result["fileEqual"], result["nameEqual"]) == (2, 2, 2, 2, 2)
    assert result["differing"] == []


def test_r103_only_the_plots_of_the_prototype_file_count():
    test = model([link("1"), link("2", origin=(5.0, 6.0, 7.5), rotation=200.0, mirrored=True)])

    result = check(test)

    assert result["plots"]["expected"] == 2
    assert result["pass"] is True


def test_r103_a_missing_plot_an_extra_plot_and_a_plot_placed_twice_are_counted_and_fail():
    test = model([link("1"), link("1"), link("9")])

    result = check(test)

    assert result["plots"]["missing"] == 1
    assert result["missingPlots"] == ["2"]
    assert result["plots"]["extra"] == 1
    assert result["extraPlots"] == ["9"]
    assert result["plots"]["placedTwice"] == 1
    assert result["pass"] is False


def test_r103_an_extra_link_of_the_file_alone_fails_the_run():
    test = copy.deepcopy(original())
    test["documents"][0]["links"].append(link("9"))

    result = check(test)

    assert (result["plots"]["placed"], result["plots"]["extra"]) == (2, 1)
    assert result["pass"] is False


def test_r103_position_bands_and_the_worst_plots_in_mm():
    test = copy.deepcopy(original())
    test["documents"][0]["links"][0]["transform"]["origin"][0] += 0.004
    test["documents"][0]["links"][1]["transform"]["origin"][1] += 0.050

    result = check(test)

    assert (result["position"]["within1mm"], result["position"]["within10mm"], result["position"]["worse"]) == (0, 1, 1)
    assert result["position"]["worst"] == [{"plot": "2", "mm": 50.0}, {"plot": "1", "mm": 4.0}]
    assert result["pass"] is False


def test_r103_position_is_compared_in_shared_coordinates_so_a_moved_survey_point_cancels():
    test = copy.deepcopy(original())
    test["documents"][0]["surveyPoint"] = {"position": [10.0, 0.0, 0.0], "sharedPosition": [110.0, 200.0, 0.0]}

    assert check(test)["position"]["within1mm"] == 2
    test["documents"][0]["surveyPoint"] = {"position": [0.0, 0.0, 0.0], "sharedPosition": [100.0, 200.002, 0.0]}
    assert check(test)["position"]["within10mm"] == 2


def test_r103_rotation_over_001_degrees_a_flipped_mirror_and_z_off_the_ffl_fail():
    test = copy.deepcopy(original())
    first, second = test["documents"][0]["links"][0], test["documents"][0]["links"][1]
    first["transform"]["rotationDeg"] += 0.02
    second["transform"]["mirrored"] = False
    plan = copy.deepcopy(PLAN)
    plan["plots"][0]["levels"]["ffl"]["value"] = 7.502

    result = check(test, plan=plan)

    assert (result["rotationEqual"], result["mirroredEqual"], result["zEqualFfl"]) == (1, 1, 1)
    assert {row["plot"] for row in result["differing"]} == {"1", "2"}
    assert result["pass"] is False


def test_r103_z_off_the_plan_ffl_alone_fails_the_run():
    plan = copy.deepcopy(PLAN)
    plan["plots"][1]["levels"]["ffl"]["value"] = 7.5015

    result = check(copy.deepcopy(original()), plan=plan)

    assert (result["position"]["within1mm"], result["rotationEqual"], result["mirroredEqual"], result["zEqualFfl"]) == (2, 2, 2, 1)
    assert result["pass"] is False


@pytest.mark.parametrize("field, value", [("mirrored", False), ("rotationDeg", 200.02)])
def test_r103_a_mirror_or_rotation_that_differs_alone_fails_the_run(field, value):
    test = copy.deepcopy(original())
    test["documents"][0]["links"][1]["transform"][field] = value

    result = check(test)

    assert result["position"]["within1mm"] == 2
    assert result["rotationEqual"] + result["mirroredEqual"] == 3
    assert result["pass"] is False


def test_r103_rotation_near_360_and_0_is_equal():
    test = copy.deepcopy(original())
    test["documents"][0]["links"][0]["transform"]["rotationDeg"] = 359.996
    old = original()
    old["documents"][0]["links"][0]["transform"]["rotationDeg"] = 0.003

    assert placer_check.compare(PLAN, old, test, FILE, PARAMETERS, REPORT)["rotationEqual"] == 2


def test_r103_each_written_parameter_is_compared_and_prototype_ffl_is_reported_apart():
    test = copy.deepcopy(original())
    test["documents"][0]["links"][0]["parameters"]["CASE"] = "B"
    del test["documents"][0]["links"][1]["parameters"]["RRE_ZZ_SP_Prototype-FFL"]

    result = check(test)

    assert result["parameters"]["CASE"] == {"equal": 1, "differ": 1, "plots": ["1"]}
    assert result["parameters"]["RRE_ZZ_SP_Prototype"]["equal"] == 2
    assert result["prototypeFfl"] == {"equal": 1, "differs": 0, "emptyInTest": 1}
    assert result["pass"] is False

    only_ffl = copy.deepcopy(original())
    for item in only_ffl["documents"][0]["links"]:
        item["parameters"]["RRE_ZZ_SP_Prototype-FFL"] = ""
    assert check(only_ffl)["pass"] is True


def test_r103_a_link_name_that_differs_is_counted_and_does_not_fail_the_run():
    test = copy.deepcopy(original())
    for item in test["documents"][0]["links"]:
        item["rawName"] = item["file"] + " : 1 : location <Not Shared>"

    result = check(test)

    assert result["nameEqual"] == 0
    assert result["pass"] is True


def test_r103_a_placer_report_that_stopped_rolled_back_or_is_missing_fails():
    test = copy.deepcopy(original())

    assert check(test, report=dict(REPORT, rolledBack=True))["pass"] is False
    assert check(test, report=dict(REPORT, stopped=True))["pass"] is False
    assert check(test, report=None)["pass"] is False


def test_r103_the_placer_report_lists_skips_with_reasons_and_issues_by_kind():
    report = dict(REPORT, skipped=[{"plot": "2", "reason": "already placed"}], issues=["plot 1: CASE is read only", "plot 2: CASE is read only"])

    summary = check(copy.deepcopy(original()), report=report)["placerReport"]

    assert summary["skipped"] == [{"plot": "2", "reason": "already placed"}]
    assert summary["issues"] == {"CASE is read only": 2}


def test_r103_reader_fixes_count_loaded_links_with_a_box_and_spots_with_a_host():
    test = copy.deepcopy(original())
    test["documents"][0]["links"][1]["boundingBoxCorners"] = None
    test["documents"][0]["links"][2]["loaded"] = False
    test["documents"][0]["spotElevations"] = [{"hostSource": "dependents"}, {"hostSource": None}, {}]

    fixes = check(test)["readerFixes"]

    assert (fixes["links"], fixes["loaded"], fixes["loadedWithBox"]) == (3, 2, 1)
    assert (fixes["spots"], fixes["spotsWithHost"]) == (3, 1)
    assert fixes["hostSource"] == {"dependents": 1, "None": 2}


def test_r103_the_second_method_recomputes_from_the_raw_axes_and_finds_what_the_main_method_finds():
    test = copy.deepcopy(original())
    good = placer_check.second_method(PLAN, original(), test, FILE, seed=7)
    assert (good["seed"], good["match"], good["of"]) == (7, 2, 2)

    flipped = copy.deepcopy(original())
    item = flipped["documents"][0]["links"][1]
    item["transform"]["axisY"] = [-value for value in item["transform"]["axisY"]]
    bad = placer_check.second_method(PLAN, original(), flipped, FILE, seed=7)
    assert bad["match"] == 1
    assert [row["mirroredEqual"] for row in bad["plots"] if row["plot"] == "2"] == [False]


def test_r103_the_second_method_draws_3_plots_and_the_same_seed_draws_the_same_ones():
    plan = {"plots": [plan_entry(str(n)) for n in range(1, 9)]}
    data = model([link(str(n)) for n in range(1, 9)])

    first = placer_check.second_method(plan, data, data, FILE, seed=15)
    again = placer_check.second_method(plan, data, data, FILE, seed=15)

    assert first["of"] == 3
    assert [row["plot"] for row in first["plots"]] == [row["plot"] for row in again["plots"]]


def test_r103_by_hand_reads_rotation_and_mirror_from_the_axes_alone():
    origin, rotation, mirrored = placer_check.by_hand(link("1", rotation=200.0, mirrored=True), SURVEY)

    assert origin == pytest.approx([101.0, 202.0, 7.5])
    assert rotation == pytest.approx(200.0)
    assert mirrored is True


def test_r103_the_check_never_writes_inside_the_repo(tmp_path):
    repo = Path(placer_check.__file__).resolve().parents[1]
    inside = repo / "python" / "testhost_C10_prototypes.json"

    with pytest.raises(ValueError):
        placer_check.main(["C10", "--test", str(inside), "--plan", str(tmp_path / "plan.json")])
