import json

import pytest

import cad_analysis
import link_proof
import project_profile

BOOK_C = "00101-PSN-IMC-XXX-SCH-TU-090000.xlsx"
BOOK_D = "00101-PSN-IMD-XXX-SCH-TU-090000.xlsx"


def shifts_in(tmp_path):
    path = tmp_path / "shifts.json"
    path.write_text(json.dumps({"neighbourhoods": {"IMC": {"shift": 10}, "IMD": {"shift": 11}}, "sourceCodePattern": "-(IMC|IMD)-", "searchRange": 1}), encoding="utf-8")
    return path


def book(source, *plots):
    return {"source": source, "rows": [{"sheetRow": number, "Plot No.": plot, "Unit": "DP2"} for number, plot in enumerate(plots, start=40)]}


def link(plot, rotation=30.1, mirrored=False):
    return {"nameParts": {"plot": plot}, "transform": {"rotationDeg": rotation, "mirrored": mirrored}}


def record(plot, unit="VL4", block="B", mirrored=None, rotation=30.1):
    return {"plot": plot, "unit": unit, "block": block, "mirrored": mirrored, "rotation": rotation}


def test_r77_the_neighbourhood_of_an_official_plot_number_is_the_one_of_the_workbook_that_lists_it(tmp_path):
    shifts = cad_analysis.read_shifts(shifts_in(tmp_path))
    found, twice = link_proof.shift_by_plot([book(BOOK_C, 1011, "1012", 1013.0, "²"), book(BOOK_D, 2021, 1013)], shifts)
    assert found == {1011: 10, 1012: 10, 1013: 11, 2021: 11} and twice == {1013}
    with pytest.raises(project_profile.ProfileError, match="no plot number shift for the workbook other.xlsx"):
        link_proof.shift_by_plot([book("other.xlsx", 1)], shifts)


def test_r77_the_turn_of_a_link_from_its_block_wraps_round_and_is_taken_to_a_tenth_of_a_degree():
    assert link_proof.turn(210.1, 30.1) == 180.0
    assert link_proof.turn(30.1, 210.1) == 180.0
    assert link_proof.turn(0.0, 30.1) == 329.9
    assert link_proof.turn(30.1, 30.1) == 0.0
    assert link_proof.turn(120.1, 300.1) == 180.0
    assert link_proof.turn(300.1, 30.1) == 270.0


def test_r77_links_are_counted_by_what_they_reach_and_the_mirror_flags_by_the_unit_of_the_plot():
    links = [link("1011", 30.1, True), link("1012", 210.1, False), link("1013", 120.1, True), link("1014"), link("1015"), link("x"), link("9999"), link("1020"), link("1021", 300.1, False), link("1030")]
    records = {"1001": record("1001", "VL4", mirrored=True, rotation=30.1), "1002": record("1002", "VL4", mirrored=True, rotation=30.1), "1003": record("1003", "C10", mirrored=False, rotation=30.1),
               "1004": record("1004", "DP2", mirrored=None, rotation=210.1), "1005": record("1005", block=None), "1011": record("1011", "VL4", mirrored=False, rotation=30.1)}
    shifts_of = {1011: 10, 1012: 10, 1013: 10, 1014: 10, 1015: 10, 1020: 10, 1021: 10, 1030: 10}
    result = link_proof.compare(links, records, shifts_of, {1020})
    assert (result["links"], result["noPlotNumber"], result["noScheduleRow"], result["inTwoWorkbooks"], result["noPlotRecord"], result["noBlock"], result["withBlock"]) == (10, 1, 1, 1, 1, 1, 5)
    assert (result["withMirrorFlag"], result["mirrorSame"], result["mirrorOpposite"]) == (4, 2, 2)
    assert result["mirrorCrosstab"] == {"block mirrored, link mirrored": 1, "block mirrored, link not mirrored": 1, "block not mirrored, link mirrored": 1, "block not mirrored, link not mirrored": 1}
    assert result["mirrorByUnit"] == {"C10": {"same": 0, "opposite": 1}, "VL4": {"same": 2, "opposite": 1}}
    assert result["linkTurnedFromBlock"] == {"0.0": 1, "90.0": 1, "180.0": 2, "270.0": 1}
    assert result["linkRotations"] == {"30.1": 7, "120.1": 1, "210.1": 1, "300.1": 1}


def test_r77_a_link_with_no_rotation_or_no_flag_is_counted_with_its_block_and_leaves_out_what_it_lacks():
    links = [{"nameParts": {"plot": "1011"}, "transform": {}}, {"nameParts": {"plot": "1012"}}, {"nameParts": {}}]
    records = {"1001": record("1001", mirrored=True), "1002": record("1002", mirrored=True)}
    result = link_proof.compare(links, records, {1011: 10, 1012: 10}, set())
    assert (result["links"], result["noPlotNumber"], result["withBlock"], result["withMirrorFlag"], result["linkTurnedFromBlock"]) == (3, 1, 2, 0, {})
    assert result["linkRotations"] == {"None": 3}


def write_inputs(tmp_path):
    (tmp_path / "analysis.json").write_text(json.dumps({"plots": [record("1001", mirrored=True, rotation=30.1), record("1002", block=None)]}), encoding="utf-8")
    (tmp_path / "prototypes.json").write_text(json.dumps({"documents": [{"links": [link("1011", 210.1, False), link("1012")]}]}), encoding="utf-8")
    (tmp_path / "coords.json").write_text(json.dumps(book(BOOK_C, 1011, 1012)), encoding="utf-8")
    return ["--analysis", str(tmp_path / "analysis.json"), "--prototypes", str(tmp_path / "prototypes.json"), "--coords", str(tmp_path / "coords.json"), "--shifts", str(shifts_in(tmp_path))]


def test_r77_main_writes_the_counts_and_prints_two_lines_and_no_plot_number(tmp_path, capsys):
    assert link_proof.main([str(tmp_path / "out")] + write_inputs(tmp_path)) == 0
    lines = capsys.readouterr().out.strip().splitlines()
    assert lines == ["links 2, with a block 1, with a mirror flag 1 (same 0, opposite 1)", 'link turned from the block by degrees: {"180.0": 1}']
    text = (tmp_path / "out" / "link_proof.json").read_text(encoding="utf-8")
    assert json.loads(text)["noBlock"] == 1 and "1011" not in text


@pytest.mark.parametrize("what", ["no prototypes", "bad prototypes", "no documents", "no analysis", "no plots", "no shifts", "no book"])
def test_r77_main_says_what_is_wrong_in_one_line(tmp_path, capsys, what):
    args = write_inputs(tmp_path)
    if what == "no prototypes":
        args[3] = str(tmp_path / "missing.json")
    elif what == "bad prototypes":
        (tmp_path / "prototypes.json").write_text("{not json", encoding="utf-8")
    elif what == "no documents":
        (tmp_path / "prototypes.json").write_text("{}", encoding="utf-8")
    elif what == "no analysis":
        args[1] = str(tmp_path / "missing.json")
    elif what == "no plots":
        (tmp_path / "analysis.json").write_text("{}", encoding="utf-8")
    elif what == "no shifts":
        args[-1] = str(tmp_path / "missing-shifts.json")
    elif what == "no book":
        (tmp_path / "coords.json").write_text(json.dumps(book("other.xlsx", 1011)), encoding="utf-8")
    assert link_proof.main([str(tmp_path / "out")] + args) == 2
    error = capsys.readouterr().err
    assert error.startswith("link_proof: ") and "Traceback" not in error and error.count("\n") == 1
    assert not (tmp_path / "out").exists()
