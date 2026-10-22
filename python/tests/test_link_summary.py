import json

import link_summary


def link(file, plot=None, prototype=None, case=None, ffl=None, z=0, rotation=0, flipped=False, errors=None, instances=None):
    entry = {
        "file": file,
        "transform": {"origin": [100, 200, z], "rotationDeg": rotation, "mirrored": flipped},
    }
    if plot is not None or prototype is not None or case is not None or ffl is not None:
        entry.update({"plotNumber": plot, "prototype": prototype, "ffl": ffl, "case": case})
        entry["nameParts"] = {"plot": plot}
    if errors:
        entry["errors"] = errors
    if instances is not None:
        entry["instances"] = instances
    return entry


def project(links):
    return {"documents": [{"file": "site.rvt", "kind": "project", "links": links}]}


def new_shape():
    return project([
        link("Type A.rvt", "9001", "VX1", "C1", "10.500", 10.5, 0, False),
        link("Type A.rvt", "9002", "VX1", "C1", "10.5015", 10.5, 90, True),
        link("Type A.rvt", "9003", "VX2", "C2", "10.503", 10.5, 90, True),
        link("Type B.rvt", "9001", "VX2", "C2", "not a number", 10.5, 180, False, ["Link is not loaded"]),
        link("Type B.rvt", "9004", "VX2", None, None, 0, 0, False),
    ])


def test_r27_links_are_counted_per_file_prototype_and_mirror():
    summary = link_summary.summarize(new_shape())
    assert summary["links"] == 5
    assert summary["notLoaded"] == 1
    assert summary["perFile"] == {"Type B.rvt": 2, "Type A.rvt": 3}
    assert summary["perPrototype"] == {"VX1": 2, "VX2": 3}
    assert summary["mirroredTotal"] == 2
    assert summary["mirroredByPrototype"] == {"VX1": {"links": 2, "mirrored": 1}, "VX2": {"links": 3, "mirrored": 1}}
    assert summary["notComputable"] == []


def test_r27_rotation_values_are_counted_in_numeric_order():
    summary = link_summary.summarize(new_shape())
    assert list(summary["rotations"].items()) == [("0", 2), ("90", 2), ("180", 1)]


def test_r27_case_is_crossed_with_mirrored_and_with_rotation():
    summary = link_summary.summarize(new_shape())
    assert summary["caseVsMirrored"] == {
        "(none)": {"mirrored": 0, "notMirrored": 1},
        "C1": {"mirrored": 1, "notMirrored": 1},
        "C2": {"mirrored": 1, "notMirrored": 1},
    }
    assert summary["caseVsRotation"]["C1"] == {"0": 1, "90": 1}
    assert summary["caseVsRotation"]["C2"] == {"90": 1, "180": 1}
    assert summary["caseVsRotation"]["(none)"] == {"0": 1}


def test_r27_ffl_is_compared_with_the_origin_z_within_one_and_a_half_millimetres():
    summary = link_summary.summarize(new_shape())
    # 10.500 and 10.5015 are within 1.5 mm of 10.5, 10.503 is not, the text and the missing value cannot be read
    assert summary["fflEqualsOriginZ"] == {"toleranceMm": 1.5, "unitAssumed": "m", "match": 2, "differ": 1, "unreadable": 2}


def test_r27_ffl_text_is_read_only_when_it_is_a_plain_number():
    assert link_summary.parse_number("12.5") == 12.5
    assert link_summary.parse_number(" +12,5 m ") == 12.5
    assert link_summary.parse_number(7) == 7
    for text in ("12.5 to 13", "FFL 12.5", "", None, True):
        assert link_summary.parse_number(text) is None


def test_r27_plot_numbers_on_more_than_one_link_are_listed():
    data = project([
        link("A.rvt", "9001", "VX1", "C1", "1", 1),
        link("B.rvt", "9001", "VX1", "C1", "1", 1),
        link("B.rvt", "12", "VX1", "C1", "1", 1),
        link("C.rvt", "9001", "VX2", "C1", "1", 1),
        link("C.rvt", "12", "VX2", "C1", "1", 1),
        link("C.rvt", "7", "VX2", "C1", "1", 1),
    ])
    summary = link_summary.summarize(data)
    assert summary["repeatedPlots"] == [{"plot": "12", "links": 2}, {"plot": "9001", "links": 3}]
    assert summary["plotNameMismatch"] == 0
    data["documents"][0]["links"][5]["nameParts"]["plot"] = "8"
    assert link_summary.summarize(data)["plotNameMismatch"] == 1


def test_r27_old_shape_computes_what_it_can_and_names_what_it_cannot():
    old = project([link("Type A.rvt", z=1, rotation=0), link("Type A.rvt", z=1, rotation=90, flipped=True, errors=["Link is not loaded"]), link("Type B.rvt", rotation=90)])
    summary = link_summary.summarize(old)
    assert summary["perFile"] == {"Type B.rvt": 1, "Type A.rvt": 2}
    assert summary["rotations"] == {"0": 1, "90": 2}
    assert summary["mirroredTotal"] == 1
    assert summary["notLoaded"] == 1
    for part in ("perPrototype", "mirroredByPrototype", "caseVsMirrored", "caseVsRotation", "fflEqualsOriginZ", "repeatedPlots"):
        assert summary[part] is None
    parts = [item["part"] for item in summary["notComputable"]]
    assert parts == ["perPrototype and mirroredByPrototype", "caseVsMirrored and caseVsRotation", "fflEqualsOriginZ", "repeatedPlots and plotNameMismatch"]
    assert all("old shape" in item["reason"] for item in summary["notComputable"])


def test_r27_a_file_with_no_links_says_so():
    summary = link_summary.summarize({"documents": [{"file": "villa.rfa", "kind": "family"}]})
    assert summary["links"] == 0
    assert summary["perFile"] == {}
    assert all(item["reason"] == "the file has no links" for item in summary["notComputable"])


def test_r27_loading_drops_the_repeated_contents_and_keeps_their_count(tmp_path):
    instances = [{"familyName": "Villa", "parameters": {"Mark": str(number)}} for number in range(50)]
    path = tmp_path / "prototypes.json"
    path.write_text(json.dumps(project([link("Type A.rvt", instances=instances)])), encoding="utf-8")
    loaded = link_summary.load(path)
    entry = loaded["documents"][0]["links"][0]
    assert "instances" not in entry
    assert entry["instanceCount"] == 50


def test_r27_main_prints_text_and_writes_link_summary_json(tmp_path, capsys):
    path = tmp_path / "prototypes.json"
    path.write_text(json.dumps(new_shape()), encoding="utf-8")
    out = tmp_path / "out"
    assert link_summary.main([str(path), str(out)]) == 0
    text = capsys.readouterr().out
    assert "links 5" in text
    assert "VX2: 1 of 3" in text
    assert "match 2, differ 1, unreadable 2" in text
    assert "9001: 2 links" in text
    written = json.loads((out / "link_summary.json").read_text(encoding="utf-8"))
    assert written["perFile"] == {"Type B.rvt": 2, "Type A.rvt": 3}
    assert written["repeatedPlots"] == [{"plot": "9001", "links": 2}]
    assert written["notComputable"] == []


def villa(file_unit, plot, prototype, case, flipped, rotation, steps, ffl, z):
    entry = link("RRE-WP-NSP-KEO-ARC-" + file_unit + "-ZZ-MOD-1000.rvt", plot, prototype, case, ffl, z, rotation, flipped)
    entry["steps"] = steps
    return entry


def excel_row(plot, unit, ffl, steps):
    return {"Plot No.": plot, "Unit": unit, "FFL": ffl, "Steps Needed": str(steps) + " steps"}


def checked_data():
    return project([
        villa("VD2", "2001", "VD2", "A", False, 30.1, "1", "10.500", 10.5),
        villa("VD2", "2002", "VD2", "A", True, 210.1, "2", "10.498", 10.5),
        villa("DP4", "2003", "VD4", "B", False, 30.1, "1", "10.4", 10.5),
        villa("DP1", "2004", "VD1", "B", False, 30.1, "3", "10.5", 10.5),
        villa("DP1", "2005", "DP1", "B", False, 120.1, "1", "10.5", 10.5),
        link("Other.rvt", z=1),
    ])


def checked_rows():
    return [
        excel_row(2001, "VD2", 10.5, 1),
        excel_row(2002, "VD2", 10.5, 2),
        excel_row(2003, "DP4", 10.5, 1),
        excel_row(2004, "VD1", 10.5, 2),
        excel_row(2006, "VD2", 10.5, 1),
    ]


def test_r27_links_are_checked_against_the_excel_plots_and_units():
    result = link_summary.verify(checked_data(), checked_rows())
    assert result["villaLinks"] == 5
    assert result["plots"] == {"linkPlotsInExcel": 4, "linkPlotsNotInExcel": ["2005"], "excelPlotsWithoutLink": ["2006"], "repeated": []}
    unit = result["prototypeVsExcelUnit"]
    assert (unit["joined"], unit["equal"]) == (4, 3)
    assert unit["different"] == [{"plot": "2003", "prototype": "VD4", "excelUnit": "DP4", "fileUnit": "DP4"}]


def test_r27_a_prototype_that_differs_from_the_villa_file_unit_is_counted_by_pair():
    result = link_summary.verify(checked_data(), checked_rows())
    assert result["prototypeVsFile"]["different"] == {"VD1 in file DP1": 1, "VD4 in file DP4": 1}
    assert link_summary.file_unit("RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt") == "DP4"
    assert link_summary.file_unit("site.rvt") is None


def test_r27_case_with_the_mirror_flag_must_fix_one_rotation_per_combination():
    result = link_summary.verify(checked_data(), checked_rows())
    assert result["caseRotation"]["cases"] == ["A", "B"]
    assert result["caseRotation"]["combinations"] == 3
    assert result["caseRotation"]["combinationsWithOneRotation"] == 2
    # B not mirrored holds 30.1 and 120.1, so its three links are not covered
    assert result["caseRotation"]["linksInThoseCombinations"] == 2


def test_r27_steps_and_levels_are_compared_with_the_excel_rows():
    result = link_summary.verify(checked_data(), checked_rows())
    assert result["stepsVsExcel"] == {"joined": 4, "equal": 3, "unreadable": 0}
    ffl = result["fflVsExcel"]
    assert ffl["originZEqualsExcelWithin1p5mm"] == 4
    assert ffl["parameterEqualsExcel"] == 2
    assert ffl["parameterLowerThanExcel"] == 2
    assert ffl["parameterHigherThanExcel"] == 0
    assert ffl["largestLowerMm"] == 100.0


def test_r27_excel_plot_numbers_match_whether_they_are_numbers_or_text():
    assert link_summary.plot_key(2289) == "2289"
    assert link_summary.plot_key(2289.0) == "2289"
    assert link_summary.plot_key("2289") == "2289"
    assert link_summary.plot_key("A") == "A"
    assert link_summary.leading_integer("12 steps") == 12
    assert link_summary.leading_integer("none") is None


def floor(type_name, top, offset, sketch_z, source=None, corners=None, slope=None, plot="2001"):
    entry = {"typeName": type_name, "topElevation": top, "heightOffset": offset, "plotNumber": plot}
    key = "sketchBoundary" if source else "boundary"
    entry[key] = [[0, 0, sketch_z], [1, 0, sketch_z], [1, 1, sketch_z]]
    if source:
        entry["cornersSource"] = source
        entry["corners"] = corners
    if slope:
        entry["slope"] = slope
    return entry


def test_r27_old_floor_shape_counts_types_and_the_sketch_z_and_says_corners_are_not_computed():
    data = project([])
    data["documents"][0]["floors"] = [
        floor("Ramps 2%", 85.0, 85.0, 150),
        floor("Ramps 2%", 85.0, 85.0, 0),
        floor("Flat Ramps", 85.0, 85.0, 85.0),
        floor("yard", 4.2, 0.2, 4.2),
    ]
    data["documents"][0]["walls"] = [{"typeName": "BW"}, {"typeName": "BW"}, {"typeName": "Garden"}]
    info = link_summary.floor_summary(data)
    assert info["floors"] == 4
    assert info["rampFloors"] == 3
    assert info["rampTypes"] == {"Flat Ramps": 1, "Ramps 2%": 2}
    assert info["wallTypes"] == {"BW": 2, "Garden": 1}
    assert info["sketchZ"] == {"floors": 3, "oneZ": 3, "equalsTop": 1, "equalsLevel": 1, "equalsNeither": 1}
    assert info["corners"] is None
    assert "NOT COMPUTED corners and slope" in "\n".join(link_summary.render_floors(info))


def test_r27_new_floor_shape_counts_corner_sources_slope_and_height_spread():
    data = project([])
    spread = [[0, 0, 83.1], [1, 0, 83.3], [1, 1, 83.2]]
    flat = [[0, 0, 83.2], [1, 0, 83.2], [1, 1, 83.2]]
    data["documents"][0]["floors"] = [
        floor("Ramps 0.5%", 83.2, 83.2, 150, "shapeEditor", spread, {"parameter": None, "arrow": False}),
        floor("Ramps 0.5%", 83.2, 83.2, 150, "shapeEditor", flat, {"parameter": 0.005, "arrow": False}),
        floor("Ramps 0.5%", 83.2, 83.2, 150, "boundary", flat, {"parameter": None, "arrow": True}),
        floor("Flat Ramps", 83.2, 83.2, 150, "boundary", flat, {"parameter": None, "arrow": False}),
        floor("Ramps 2%", 83.2, 83.2, 150, None, None, None, None),
    ]
    data["documents"][0]["floors"][4]["boundary"] = []
    data["documents"][0]["spotElevations"] = [{"elementId": 1}, {"elementId": 2}]
    info = link_summary.floor_summary(data)
    assert info["spotElevations"] == 2
    assert info["corners"] == {
        "rampFloors": 5,
        "shapeEditor": 2,
        "boundary": 2,
        "noCorners": 1,
        "reportSlope": 2,
        "shapeAndSlope": 1,
        "neither": 2,
        "cornerHeightSpreadOver5mm": 1,
        "largestCornerHeightSpreadMm": 200.0,
        "plotNumberNull": 1,
    }


def test_r27_main_with_the_excel_option_writes_and_prints_the_check(tmp_path, capsys):
    path = tmp_path / "prototypes.json"
    path.write_text(json.dumps(checked_data()), encoding="utf-8")
    coords = tmp_path / "coords.json"
    coords.write_text(json.dumps({"rows": checked_rows()}), encoding="utf-8")
    out = tmp_path / "out"
    assert link_summary.main([str(path), str(out), "--excel", str(coords)]) == 0
    text = capsys.readouterr().out
    assert "prototype equals Excel unit: 3 of 4" in text
    assert "plot 2003: prototype VD4, Excel DP4, file DP4" in text
    assert "VD1 in file DP1: 1" in text
    written = json.loads((out / "link_summary.json").read_text(encoding="utf-8"))
    assert written["verify"]["fflVsExcel"]["parameterLowerThanExcel"] == 2
    assert written["floorSummary"]["floors"] == 0
    link_summary.main([str(path), str(out)])
    assert json.loads((out / "link_summary.json").read_text(encoding="utf-8"))["verify"] is None
