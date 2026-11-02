"""R469. The compare of a zone plan with its key plan, on made up plans."""
import csv
import json

import pytest

import zone_compare

SETTINGS = {"majority": {"levelPassMm": 0.5, "levelHoldMm": 5, "wallCentreCodes": [0, 1], "kindLeaveOut": {"SE": ["entry", "steps", "ffl", "rampFront"]}},
            "keyPlans": {"wallEdgeM": 0.3},
            "answerKeys": {"keys": [{"code": "IMC", "kind": "TU"}, {"code": "IMA", "kind": "SE"}]},
            "designBasis": {"modes": ["rev6", "pastJobMode"], "mode": "pastJobMode"}}
CHOSEN = {"rotationWithinDeg": 1.0, "stepsWithin": 1, "wallMatchM": 0.01}
RAMP = [[0.0, 0.0, 9.0], [5.0, 0.0, 9.0], [5.0, 3.0, 9.0], [0.0, 3.0, 9.0]]


def value(item):
    return {"value": item}


def plot(number, unit="VD4", mirrored=False, rotation=90.0, ffl=10.0, entry=9.0, steps=2, corners=None):
    levels = {"ffl": value(ffl)}
    if entry is not None:
        levels["entry"] = value(entry)
    if steps is not None:
        levels["steps"] = value(steps)
    floors = {} if corners is None else {"ramp": {"corners": value(corners)}}
    return {"plot": number, "unit": value(unit), "mirrored": value(mirrored), "rotationDeg": value(rotation), "levels": levels, "floors": floors}


def lifted(by, moved=0.0):
    return [[x + moved, y, z + by] for x, y, z in RAMP]


def tool_wall(plots, start, end):
    return {"plot": plots[0], "edge": 1, "plots": value(plots), "ends": value([start, end])}


def key_wall(plots, start, end):
    return {"elementId": 1, "plots": plots, "start": start + [0.0], "end": end + [0.0]}


def compare(tool, key, kind="TU", left_out=()):
    return zone_compare.compare_zone(tool, key, kind, zone_compare.limits_of(SETTINGS, CHOSEN), list(left_out))


def test_each_field_falls_in_its_band():
    tool = {"plots": [plot("1"), plot("2", ffl=10.0003), plot("3", ffl=10.003), plot("4", unit="DT3", mirrored=True, rotation=90.5, ffl=10.2, steps=3),
                      plot("5", steps=5, rotation=95.0)], "walls": []}
    key = {"plots": [plot(number) for number in "12345"], "walls": []}
    counts = compare(tool, key)["counts"]
    assert counts["ffl"] == {"match": 3, "withinLimit": 1, "overLimit": 1}
    assert counts["villaType"] == {"match": 4, "overLimit": 1}
    assert counts["mirror"] == {"match": 4, "overLimit": 1}
    assert counts["rotation"] == {"match": 3, "withinLimit": 1, "overLimit": 1}
    assert counts["steps"] == {"match": 3, "withinLimit": 1, "overLimit": 1}


def test_a_gap_on_the_limit_is_inside_it():
    limits = zone_compare.limits_of(SETTINGS, CHOSEN)
    assert zone_compare.status_of(limits["ffl"], 0.005) == "withinLimit"
    assert zone_compare.status_of(limits["ffl"], 0.0005) == "match"
    assert zone_compare.status_of(limits["ffl"], 0.00501) == "overLimit"


def test_rotation_gap_goes_round_the_circle():
    tool = {"plots": [plot("1", rotation=359.9)], "walls": []}
    key = {"plots": [plot("1", rotation=0.1)], "walls": []}
    assert compare(tool, key)["counts"]["rotation"] == {"withinLimit": 1}


def test_a_field_the_kind_leaves_out_has_no_reference():
    tool = {"plots": [plot("1", ffl=50.0, entry=49.0, steps=9)], "walls": []}
    key = {"plots": [plot("1", steps=1)], "walls": []}
    counts = compare(tool, key, "SE", ["entry", "steps", "ffl", "rampFront"])["counts"]
    for field in ("ffl", "entry", "steps"):
        assert counts[field] == {"noReference": 1}
    assert counts["mirror"] == {"match": 1}


def test_a_value_on_one_side_is_counted_by_side_and_never_as_a_gap():
    tool = {"plots": [plot("1", entry=None), plot("2")], "walls": []}
    key = {"plots": [plot("1"), plot("2", entry=None)], "walls": []}
    assert compare(tool, key)["counts"]["entry"] == {"toolNone": 1, "noReference": 1}


def test_ramp_corners_level_and_plan_apart():
    tool = {"plots": [plot("1", corners=lifted(0.01)), plot("2", corners=lifted(0.2)), plot("3", corners=lifted(0.0, moved=0.005))], "walls": []}
    key = {"plots": [plot(number, corners=lifted(0.0)) for number in "123"], "walls": []}
    counts = compare(tool, key)["counts"]
    assert counts["rampCornersLevel"] == {"match": 1, "withinLimit": 1, "overLimit": 1}
    assert counts["rampCornersPlan"] == {"match": 2, "withinLimit": 1}


def test_a_wall_in_two_key_pieces_matches_the_whole_tool_wall():
    tool = {"plots": [plot("1")], "walls": [tool_wall(["1"], [0.0, 0.0], [10.0, 0.0])]}
    key = {"plots": [plot("1")], "walls": [key_wall(["1"], [0.0, 0.0], [4.0, 0.0]), key_wall(["1"], [4.0, 0.0], [10.0, 0.0])]}
    counts = compare(tool, key)["counts"]
    assert counts["wallsToolOnKey"] == {"match": 1}
    assert counts["wallsKeyOnTool"] == {"match": 1}


def test_a_wall_on_one_side_only_is_over_the_limit_in_that_direction():
    tool = {"plots": [plot("1"), plot("2")], "walls": [tool_wall(["1"], [0.0, 0.0], [10.0, 0.0])]}
    key = {"plots": [plot("1"), plot("2")], "walls": [key_wall(["1"], [0.0, 0.1], [10.0, 0.1])]}
    assert compare(tool, key)["counts"]["wallsToolOnKey"] == {"withinLimit": 1, "noReference": 1}
    tool["walls"] = []
    counts = compare(tool, key)["counts"]
    assert counts["wallsKeyOnTool"] == {"overLimit": 1, "noReference": 1}
    assert counts["wallsToolOnKey"] == {"noReference": 2}


def test_a_wall_with_no_ends_is_left_out():
    tool = {"plots": [plot("1")], "walls": []}
    key = {"plots": [plot("1")], "walls": [{"elementId": 1, "plots": [], "start": None, "end": None}]}
    assert compare(tool, key)["counts"]["wallsKeyOnTool"] == {"noReference": 1}


def test_plots_of_one_side_are_listed_and_not_compared():
    tool = {"plots": [plot("1"), plot("9")], "walls": []}
    key = {"plots": [plot("1"), plot("8")], "walls": []}
    report = compare(tool, key)
    assert report["compared"] == 1
    assert report["onlyInTool"] == ["9"] and report["onlyInKey"] == ["8"]


def test_worst_lists_the_largest_gaps_first_and_stops_at_its_size():
    tool = {"plots": [plot(str(number), ffl=10.0 + number / 100.0) for number in range(1, 15)], "walls": []}
    key = {"plots": [plot(str(number)) for number in range(1, 15)], "walls": []}
    worst = compare(tool, key)["worst"]["ffl"]
    assert [item["plot"] for item in worst][:2] == ["14", "13"] and len(worst) == 10


def test_a_match_is_never_a_worst_plot():
    tool = {"plots": [plot("1"), plot("2", unit="DT3")], "walls": []}
    key = {"plots": [plot("1"), plot("2")], "walls": []}
    report = compare(tool, key)
    assert report["worst"]["ffl"] == []
    assert [item["plot"] for item in report["worst"]["villaType"]] == ["2"]


def test_the_random_plots_hold_for_a_seed_and_differ_by_seed():
    numbers = [str(number) for number in range(1, 200)]
    assert zone_compare.random_plots(numbers, 14) == zone_compare.random_plots(list(reversed(numbers)), 14)
    assert zone_compare.random_plots(numbers, 14) != zone_compare.random_plots(numbers, 7)
    assert len(zone_compare.random_plots(numbers, 14)) == 10
    assert zone_compare.random_plots(["1", "2"], 14) == ["1", "2"]


def test_only_the_past_job_mode_runs():
    zone_compare.check_mode(SETTINGS)
    with pytest.raises(ValueError, match="rev6"):
        zone_compare.check_mode({"designBasis": {"modes": ["rev6", "pastJobMode"], "mode": "rev6"}})


def test_kind_of_a_key_comes_from_the_config():
    assert zone_compare.kind_of(SETTINGS, "IMA") == "SE"
    with pytest.raises(ValueError, match="IMZ"):
        zone_compare.kind_of(SETTINGS, "IMZ")


def run_main(tmp_path, tool, key, settings=SETTINGS, without=None):
    settings_path = tmp_path / "project.json"
    settings_path.write_text(json.dumps(settings), encoding="utf-8")
    for name, plan in (("with.json", tool), ("without.json", without or tool), ("key.json", key)):
        (tmp_path / name).write_text(json.dumps(plan), encoding="utf-8")
    out = tmp_path / "out"
    code = zone_compare.main(["IMC", "--with-overrides", str(tmp_path / "with.json"), "--without-overrides", str(tmp_path / "without.json"),
                              "--key-plan", str(tmp_path / "key.json"), "--settings", str(settings_path), "--rotation-within-deg", "1", "--steps-within", "1",
                              "--wall-match-m", "0.01", "--seed", "14", "--out", str(out)])
    return code, out


def test_the_markdown_holds_no_level_and_the_csv_holds_the_values(tmp_path):
    tool = {"plots": [plot("1", ffl=123.456, entry=122.789)], "walls": []}
    key = {"plots": [plot("1", ffl=123.111, entry=122.222)], "walls": []}
    code, out = run_main(tmp_path, tool, key)
    assert code == 0
    text = (out / "zone_imc.md").read_text(encoding="utf-8")
    for level in ("123.456", "122.789", "123.111", "122.222"):
        assert level not in text
    assert "345.0" in text
    with (out / "side_by_side_imc.csv").open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    assert rows[0]["ffl_with"] == "123.456" and rows[0]["ffl_key"] == "123.111"
    report = json.loads((out / "zone_compare_imc.json").read_text(encoding="utf-8"))
    assert report["builds"]["with"]["compared"] == 1
    assert report["kind"] == "TU"


def test_the_report_keeps_the_limits_and_gaps_exact(tmp_path):
    tool = {"plots": [plot("1", ffl=10.00049)], "walls": []}
    key = {"plots": [plot("1")], "walls": []}
    _, out = run_main(tmp_path, tool, key)
    report = json.loads((out / "zone_compare_imc.json").read_text(encoding="utf-8"))
    assert report["limits"]["ffl"]["match"] == 0.0005
    row = report["builds"]["with"]["perPlot"]["1"]["ffl"]
    assert row["status"] == "match" and row["gap"] == pytest.approx(0.00049, abs=1e-9)


def test_a_plot_only_the_build_with_overrides_writes_is_never_a_random_plot(tmp_path):
    numbers = [str(number) for number in range(1, 40)]
    tool = {"plots": [plot(number) for number in numbers], "walls": []}
    fewer = {"plots": [plot(number) for number in numbers[:3]], "walls": []}
    key = {"plots": [plot(number) for number in numbers], "walls": []}
    code, out = run_main(tmp_path, tool, key, without=fewer)
    assert code == 0
    report = json.loads((out / "zone_compare_imc.json").read_text(encoding="utf-8"))
    assert sorted(report["randomPlots"]) == sorted(numbers[:3])
    text = (out / "zone_imc.md").read_text(encoding="utf-8")
    assert "With overrides: plots compared 39" in text and "Without overrides: plots compared 3," in text


def test_the_markdown_holds_no_coordinate_of_a_ramp_or_a_wall(tmp_path):
    far = [[501234.5, 2401234.5, 77.25], [501239.5, 2401234.5, 77.25], [501239.5, 2401237.5, 77.25], [501234.5, 2401237.5, 77.25]]
    tool = {"plots": [plot("1", corners=far)], "walls": [tool_wall(["1"], [501234.5, 2401234.5], [501239.5, 2401234.5])]}
    key = {"plots": [plot("1", corners=far)], "walls": [key_wall(["1"], [501234.5, 2401234.6], [501239.5, 2401234.6])]}
    _, out = run_main(tmp_path, tool, key)
    text = (out / "zone_imc.md").read_text(encoding="utf-8")
    for number in ("501234", "2401234", "501239", "2401237", "77.25"):
        assert number not in text


def run_rev6(tmp_path, rev6, past, key, extra=()):
    settings_path = tmp_path / "project.json"
    settings_path.write_text(json.dumps(SETTINGS), encoding="utf-8")
    for name, plan in (("rev6.json", rev6), ("past.json", past), ("key.json", key)):
        (tmp_path / name).write_text(json.dumps(plan), encoding="utf-8")
    out = tmp_path / "out"
    code = zone_compare.main(["IMC", "--rev6-plan", str(tmp_path / "rev6.json"), "--without-overrides", str(tmp_path / "past.json"), "--key-plan", str(tmp_path / "key.json"),
                              "--settings", str(settings_path), "--rotation-within-deg", "1", "--steps-within", "1", "--wall-match-m", "0.01", "--seed", "14", "--out", str(out), *extra])
    return code, out


def test_a_rev6_plan_is_compared_beside_the_past_job_rules(tmp_path):
    rev6 = {"plots": [plot("1", ffl=10.2), plot("2")], "walls": [], "summary": {"noDoorLevel": 0}}
    past = {"plots": [plot("1"), plot("2", ffl=10.003), plot("3")], "walls": [], "summary": {}}
    key = {"plots": [plot(number) for number in "123"], "walls": []}
    code, out = run_rev6(tmp_path, rev6, past, key)
    assert code == 0
    report = json.loads((out / "zone_compare_imc.json").read_text(encoding="utf-8"))
    assert list(report["builds"]) == ["rev6", "without"]
    assert report["builds"]["rev6"]["counts"]["ffl"] == {"overLimit": 1, "match": 1}
    assert report["builds"]["without"]["counts"]["ffl"] == {"match": 2, "withinLimit": 1}
    assert report["builds"]["rev6"]["onlyInKey"] == ["3"]
    assert sorted(report["randomPlots"]) == ["1", "2"]
    text = (out / "zone_imc.md").read_text(encoding="utf-8")
    assert "Rev6, no override list: plots compared 2" in text and "Without overrides: plots compared 3" in text
    with (out / "side_by_side_imc.csv").open(encoding="utf-8", newline="") as handle:
        rows = list(csv.DictReader(handle))
    assert rows[0]["ffl_rev6"] == "10.2" and rows[0]["ffl_without"] == "10.0" and rows[0]["ffl_key"] == "10.0"


def test_a_plan_of_the_past_job_is_refused_as_a_rev6_plan(tmp_path):
    past = {"plots": [plot("1")], "walls": [], "summary": {}}
    key = {"plots": [plot("1")], "walls": []}
    with pytest.raises(ValueError, match="noDoorLevel"):
        run_rev6(tmp_path, past, past, key)


def test_a_rev6_plan_and_a_plan_with_overrides_cannot_go_together(tmp_path):
    rev6 = {"plots": [plot("1")], "walls": [], "summary": {"noDoorLevel": 0}}
    key = {"plots": [plot("1")], "walls": []}
    (tmp_path / "with.json").write_text(json.dumps(rev6), encoding="utf-8")
    with pytest.raises(SystemExit):
        run_rev6(tmp_path, rev6, rev6, key, ("--with-overrides", str(tmp_path / "with.json")))


def walls_of(wall, line_code, type_name="BW"):
    wall.update(locationLine=line_code, type=type_name)
    return wall


def wall_gaps(key_wall_of):
    tool = {"plots": [plot("1")], "walls": [tool_wall(["1"], [0.0, 0.0], [10.0, 0.0])]}
    key = {"plots": [plot("1")], "walls": [key_wall_of]}
    limits = zone_compare.limits_of(SETTINGS, CHOSEN, 0.2)
    return zone_compare.compare_zone(tool, key, "TU", limits, [])["perPlot"]["1"]


def test_r478_a_key_wall_on_its_centre_line_is_held_by_its_line():
    rows = wall_gaps(walls_of(key_wall(["1"], [0.0, 0.0], [10.0, 0.0]), 0))
    assert rows["wallsToolOnKey"]["gap"] == pytest.approx(0.0) and rows["wallsKeyOnTool"]["gap"] == pytest.approx(0.0)


def test_r478_a_key_wall_drawn_on_a_face_half_its_thickness_off_has_its_centre_on_the_tool_line():
    rows = wall_gaps(walls_of(key_wall(["1"], [0.0, 0.1], [10.0, 0.1]), 5))
    assert rows["wallsToolOnKey"]["gap"] == pytest.approx(0.0) and rows["wallsKeyOnTool"]["gap"] == pytest.approx(0.0)
    # The thickness in the type name wins over the boundary wall of the profile
    rows = wall_gaps(walls_of(key_wall(["1"], [0.0, 0.165], [10.0, 0.165]), 3, "KEO_ST-CIP-RC Upsand - 330"))
    assert rows["wallsToolOnKey"]["gap"] == pytest.approx(0.0)


def test_r478_a_key_wall_with_its_face_on_the_tool_line_stands_half_a_thickness_off():
    rows = wall_gaps(walls_of(key_wall(["1"], [0.0, 0.0], [10.0, 0.0]), 5))
    assert rows["wallsToolOnKey"]["gap"] == pytest.approx(0.1)


def test_r479_two_villa_type_names_of_one_prototype_file_match_and_of_two_files_stay_over():
    limits = zone_compare.limits_of(SETTINGS, CHOSEN, 0.2)
    mine, theirs = plot("1"), plot("1")
    mine["unit"], theirs["unit"] = value("TH1-IM"), value("TH1")
    mine["prototypeFile"], theirs["prototypeFile"] = value("a.rvt"), "a.rvt"
    row = zone_compare.compare_zone({"plots": [mine], "walls": []}, {"plots": [theirs], "walls": []}, "TU", limits, [])["perPlot"]["1"]["villaType"]
    assert row["status"] == zone_compare.MATCH and row["sameFile"] is True
    theirs["prototypeFile"] = "b.rvt"
    row = zone_compare.compare_zone({"plots": [mine], "walls": []}, {"plots": [theirs], "walls": []}, "TU", limits, [])["perPlot"]["1"]["villaType"]
    assert row["status"] == zone_compare.OVER
