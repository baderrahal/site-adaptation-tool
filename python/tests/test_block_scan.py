import json

import pytest

import block_scan
import project_profile

PROFILE_VALUES = {
    "units_mm": True,
    "typologies": {"DP2": {"kind": "duplex"}, "TH1": {"kind": "townhouse"}, "VL3": {"kind": "villa"}},
    "clusters": {"D02": ["DP2", "DP2"], "Q02": ["DP2", "TH1", "TH1", "DP2"]},
    "cluster_aliases": {"D02": "M02"},
}
SETTINGS_VALUES = {"analysis": {"unitPlotTypes": ["DP2", "TH1", "VL2", "VL3"], "noRev6Match": ["Q02"]}}
OLD = {"D02", "Q02", "VL2"}


def profile_in(tmp_path):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    return project_profile.load_profile(path)


def face(label, typology, x, y, width=10, depth=25):
    corners = [(x, y), (x + width, y), (x + width, y + depth), (x, y + depth)]
    return {"kind": "plot_shape", "plot": label, "type": typology, "vertices": [list(corner) for corner in corners]}


def block(name, x0, y0, x1, y1, mirrored=False, insert=None):
    return {"name": name, "outline": [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], "mirrored": mirrored, "insertPoint": insert or [x0, y0, 0]}


def faces_row(count):
    return [face(str(number + 1), "DP2", 10 * number, 0) for number in range(count)]


def test_r73_a_name_is_classed_by_the_code_it_holds_and_whether_it_ends_in_an_underscore_and_the_code():
    units = {"DP2", "TH1", "VL3"}
    assert block_scan.classify("Villa_VL3", units, OLD) == ("unit", "VL3", True)
    assert block_scan.classify("X$0$Duplex_DP2", units, OLD) == ("unit", "DP2", True)
    assert block_scan.classify("Villa_VL2", units, OLD) == ("old", "VL2", True)
    assert block_scan.classify("DP2 type A", units, OLD) == ("unit", "DP2", False)
    assert block_scan.classify("Q02 copy", units, OLD) == ("old", "Q02", False)
    assert block_scan.classify("MP-Parking_6mx2.5m", units, OLD) == ("none", None, False)
    assert block_scan.classify("Drop-off", units, OLD) == ("none", None, False)
    assert block_scan.classify(None, units, OLD) == ("none", None, False)
    # A code of the profile wins over an old code when a name holds both
    assert block_scan.classify("Q02_DP2", units, OLD)[:2] == ("unit", "DP2")


def test_r73_inserts_are_counted_by_how_many_plots_hold_a_fifth_of_the_outline_with_a_bucket_for_more(tmp_path):
    profile = profile_in(tmp_path)
    faces = faces_row(6)
    blocks = [block("A_DP2", 20, 1, 29, 24), block("B_DP2", 5, 1, 15, 24), block("C_DP2", 0, 1, 30, 24), block("D_DP2", 0, 1, 40, 24), block("E_DP2", 0, 1, 50, 24), block("F_DP2", 500, 500, 510, 510)]
    scan = block_scan.scan(blocks, faces, profile, OLD)
    by_name = {row["name"]: row["covering"] for row in scan["names"]}
    assert by_name == {"A_DP2": {"1": 1}, "B_DP2": {"2": 1}, "C_DP2": {"3": 1}, "D_DP2": {"4": 1}, "E_DP2": {"more": 1}, "F_DP2": {"0": 1}}
    assert scan["summary"]["covering"] == {"0": 1, "1": 1, "2": 1, "3": 1, "4": 1, "more": 1}


def test_r73_a_plot_holding_exactly_a_fifth_of_the_outline_counts_and_one_just_under_does_not(tmp_path):
    profile = profile_in(tmp_path)
    faces = faces_row(2)
    exact = block_scan.scan([block("A_DP2", 8, 1, 18, 24)], faces, profile, OLD)
    assert exact["names"][0]["covering"] == {"2": 1}
    under = block_scan.scan([block("A_DP2", 8.5, 1, 18.5, 24)], faces, profile, OLD)
    assert under["names"][0]["covering"] == {"1": 1}


def test_r73_the_insert_point_is_told_apart_as_in_the_best_plot_in_another_plot_in_no_plot_or_no_plot_holds_the_outline(tmp_path):
    profile = profile_in(tmp_path)
    faces = faces_row(2)
    blocks = [block("A_DP2", 2, 1, 9, 24, insert=[3, 3, 0]), block("B_DP2", 2, 1, 9, 24, insert=[15, 3, 0]), block("C_DP2", 2, 1, 9, 24, insert=[500, 3, 0]),
              block("D_DP2", 500, 500, 510, 510, insert=[3, 3, 0]), block("E_DP2", 2, 1, 9, 24, insert=None)]
    blocks[4]["insertPoint"] = None
    scan = block_scan.scan(blocks, faces, profile, OLD)
    points = {row["name"]: row["insertPoint"] for row in scan["names"]}
    assert points == {"A_DP2": {"inBestPlot": 1}, "B_DP2": {"inOtherPlot": 1}, "C_DP2": {"inNoPlot": 1}, "D_DP2": {"noPlotHoldsPart": 1}, "E_DP2": {"noInsertPoint": 1}}
    assert scan["summary"]["insertPoint"] == {"inBestPlot": 1, "inOtherPlot": 1, "inNoPlot": 1, "noPlotHoldsPart": 1, "noInsertPoint": 1}


def test_r73_mirrored_and_not_mirrored_are_counted_for_each_name_from_the_scale_when_the_flag_is_missing(tmp_path):
    profile = profile_in(tmp_path)
    faces = faces_row(1)
    blocks = [block("A_DP2", 1, 1, 9, 24, mirrored=True), block("A_DP2", 1, 1, 9, 24), {**block("A_DP2", 1, 1, 9, 24), "mirrored": None, "scale": [-1, 1, 1]},
              {**block("A_DP2", 1, 1, 9, 24), "mirrored": None, "scale": None}, block("B_DP2", 1, 1, 9, 24)]
    scan = block_scan.scan(blocks, faces, profile, OLD)
    rows = {row["name"]: (row["inserts"], row["mirrored"], row["notMirrored"], row["noFlag"]) for row in scan["names"]}
    assert rows == {"A_DP2": (4, 2, 1, 1), "B_DP2": (1, 0, 1, 0)}
    assert (scan["summary"]["mirrored"], scan["summary"]["notMirrored"], scan["summary"]["noFlag"]) == (2, 2, 1)


def test_r73_an_insert_with_no_outline_is_counted_apart_and_the_names_come_most_inserts_first(tmp_path):
    profile = profile_in(tmp_path)
    blocks = [{"name": "Tower", "outline": None}, {"name": "Tower", "outline": [[0, 0], [1, 1]]}, block("Z_DP2", 1, 1, 9, 24), block("Z_DP2", 1, 1, 9, 24), block("Z_DP2", 1, 1, 9, 24), block("A_TH1", 1, 1, 9, 24)]
    scan = block_scan.scan(blocks, faces_row(1), profile, OLD)
    assert [(row["name"], row["inserts"]) for row in scan["names"]] == [("Z_DP2", 3), ("Tower", 2), ("A_TH1", 1)]
    tower = scan["names"][1]
    assert (tower["covering"], tower["insertPoint"], tower["kind"]) == ({"noOutline": 2}, {"noOutline": 2}, "none")
    assert scan["summary"]["covering"]["noOutline"] == 2


def test_r73_the_summary_splits_the_names_and_inserts_by_kind_and_counts_the_ones_that_end_in_the_code(tmp_path):
    profile = profile_in(tmp_path)
    blocks = [block("Duplex_DP2", 1, 1, 9, 24)] * 3 + [block("Villa_VL2", 1, 1, 9, 24)] * 2 + [block("Parking", 1, 1, 9, 24)] + [block("DP2 note", 1, 1, 9, 24)]
    summary = block_scan.scan(blocks, faces_row(1), profile, OLD)["summary"]
    assert (summary["inserts"], summary["names"]) == (7, 4)
    assert summary["namesByKind"] == {"unit": 2, "old": 1, "none": 1} and summary["insertsByKind"] == {"unit": 4, "old": 2, "none": 1}
    assert summary["namesEndingInCode"] == 2
    assert sum(summary["coveringOfCodedNames"].values()) == 6


def test_r73_a_code_that_is_also_a_label_type_of_the_baseplan_shows_how_many_plots_carry_it(tmp_path):
    profile = profile_in(tmp_path)
    faces = faces_row(3) + [face("9", "VL2", 100, 0), face("10", ["VL2"], 120, 0)]
    scan = block_scan.scan([block("Villa_VL2", 101, 1, 109, 24), block("Duplex_DP2", 1, 1, 9, 24), block("Parking", 1, 1, 9, 24)], faces, profile, OLD)
    assert {row["name"]: row["labelTypePlots"] for row in scan["names"]} == {"Villa_VL2": 1, "Duplex_DP2": 3, "Parking": 0}


def test_r73_the_old_codes_are_the_aliases_the_no_match_codes_and_the_label_types_that_are_not_unit_codes(tmp_path):
    profile = profile_in(tmp_path)
    settings_path = tmp_path / "project.json"
    settings_path.write_text(json.dumps(SETTINGS_VALUES), encoding="utf-8")
    settings = project_profile.load_settings(settings_path)
    assert block_scan.old_codes_of(profile, settings) == {"D02", "Q02", "VL2"}


def write_inputs(tmp_path, faces, blocks, settings_values=None):
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"source": "base.dwg", "plots": faces}), encoding="utf-8")
    drawing = tmp_path / "blocks"
    drawing.mkdir()
    (drawing / "blocks.json").write_text(json.dumps({"source": "footprint.dwg", "blocks": blocks}), encoding="utf-8")
    profile = profile_in(tmp_path)
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile.path), **(settings_values or SETTINGS_VALUES)}), encoding="utf-8")
    return ["--cad", str(cad), "--blocks", str(drawing), "--project", str(settings)]


def test_r73_main_writes_block_scan_json_with_counts_and_no_coordinates_and_prints_one_line_for_each_name(tmp_path, capsys):
    args = write_inputs(tmp_path, faces_row(2), [block("Duplex_DP2", 1, 1, 19, 24), block("Villa_VL2", 500, 500, 510, 510, mirrored=True)])
    assert block_scan.main([str(tmp_path / "out")] + args) == 0
    lines = capsys.readouterr().out.strip().splitlines()
    assert lines[0].startswith("inserts 2, names 2, names by kind {'unit': 1, 'old': 1, 'none': 0}")
    assert lines[1].strip().startswith("1  Duplex_DP2  kind unit DP2, ends in the code, mirrored 0 not 1")
    assert lines[2].strip().startswith("1  Villa_VL2  kind old VL2, ends in the code, mirrored 1 not 0")
    result = json.loads((tmp_path / "out" / "block_scan.json").read_text(encoding="utf-8"))
    assert list(result) == ["source", "summary", "names"] and result["source"] == "footprint.dwg"
    text = json.dumps(result)
    assert "outline" not in text and "insertPoint\": [" not in text and "vertices" not in text


def test_r73_main_refuses_a_missing_folder_and_a_blocks_file_with_no_blocks_list_with_one_line(tmp_path, capsys):
    args = write_inputs(tmp_path, faces_row(1), [])
    (tmp_path / "blocks" / "blocks.json").write_text('{"blocks": null}', encoding="utf-8")
    assert block_scan.main([str(tmp_path / "out")] + args) == 2
    assert "blocks.json has no blocks list" in capsys.readouterr().err
    args[3] = str(tmp_path / "nowhere")
    assert block_scan.main([str(tmp_path / "out")] + args) == 2
    assert "block_scan:" in capsys.readouterr().err


@pytest.mark.parametrize("count,expected", [(0, "0"), (1, "1"), (4, "4"), (5, "more"), (40, "more")])
def test_r73_the_bucket_of_a_covering_count_stops_at_four(count, expected):
    assert block_scan.bucket(count) == expected
