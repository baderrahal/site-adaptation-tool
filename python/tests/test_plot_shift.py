import json
import re

import link_table
from test_link_table import excel_file, kinds, pdf_row, rows_file, square

CONFIG = {
    "shifts": {"IMC": 10, "IMD": 11},
    "pattern": re.compile("-(IMC|IMD)-", re.IGNORECASE),
    "range": 1,
}


def official(plot, shift, x=0, kind="VL4"):
    """A CAD square labelled plot minus shift, placed at x."""
    return square(plot - shift, x, 0, kind=kind)


def build(plots, rows, coords):
    return link_table.build({"plots": plots}, rows, coords, 1.0, CONFIG)


def test_r28_official_number_joins_the_cad_label_lower_by_the_shift_of_its_neighbourhood():
    plots = [official(2289, 10, 0), official(2400, 11, 20)]
    rows = [rows_file("01-PSN-IMC-ZZZ.pdf", [pdf_row(2289, 1, 2, 3)]), rows_file("01-PSN-IMD-ZZZ.pdf", [pdf_row(2400, 1, 22, 3)])]
    coords = [excel_file("00-PSN-IMC-XXX.xlsx", [(2289, "DT3")]), excel_file("00-PSN-IMD-XXX.xlsx", [(2400, "DT3")])]
    summary, links, conflicts = build(plots, rows, coords)
    by_plot = {link["plot"]: link for link in links if link["plot"] is not None}
    assert by_plot[2289]["cadLabel"] == 2279 and by_plot[2289]["neighbourhood"] == "IMC"
    assert by_plot[2400]["cadLabel"] == 2389 and by_plot[2400]["neighbourhood"] == "IMD"
    assert summary["locationOk"] == 2 and summary["shiftSameAsConfig"] == 2
    assert conflicts == []


def test_r28_plot_that_fails_with_the_config_shift_is_found_one_off_and_written_as_shift_edge():
    plots = [official(2289, 10, 0), official(2301, 11, 20)]
    rows = [rows_file("a-IMC-.pdf", [pdf_row(2289, 1, 2, 3), pdf_row(2301, 1, 22, 3)])]
    coords = [excel_file("b-IMC-.xlsx", [(2289, "DT3"), (2301, "DT3")])]
    summary, links, conflicts = build(plots, rows, coords)
    edge = [conflict for conflict in conflicts if conflict["kind"] == "shift_edge"]
    assert edge == [{"kind": "shift_edge", "plot": 2301, "neighbourhood": "IMC", "configShift": 10, "shiftFound": 11}]
    fixed = next(link for link in links if link["plot"] == 2301)
    assert fixed["cadLabel"] == 2290
    assert fixed["location"]["ok"] and fixed["location"]["shiftFound"] == 11
    assert summary["shiftDifferent"] == 1 and summary["locationOk"] == 2 and summary["locationBad"] == 0


def test_r28_shift_two_away_is_not_searched_and_stays_a_location_conflict():
    plots = [official(2289, 10, 0), official(2289, 12, 20)]
    rows = [rows_file("a-IMC-.pdf", [pdf_row(2289, 1, 22, 3)])]
    summary, links, conflicts = build(plots, rows, [excel_file("b-IMC-.xlsx", [(2289, "DT3")])])
    assert "location" in kinds(conflicts)
    assert "shift_edge" not in kinds(conflicts)
    assert summary["shiftNotFound"] == 1
    assert links[0]["location"]["shiftFound"] is None


def test_r28_two_plots_that_fit_one_cad_plot_are_a_conflict_and_never_picked_silently():
    plots = [square(2279, 0, 0)]
    rows = [rows_file("a-IMC-.pdf", [pdf_row(2289, 1, 2, 3), pdf_row(2290, 1, 4, 3)])]
    coords = [excel_file("b-IMC-.xlsx", [(2289, "DT3"), (2290, "DT3")])]
    _, _, conflicts = build(plots, rows, coords)
    twice = next(conflict for conflict in conflicts if conflict["kind"] == "cad_claimed_twice")
    assert twice["cadLabel"] == 2279 and twice["plots"] == [2289, 2290]


def test_r28_source_name_without_a_neighbourhood_code_or_with_two_is_a_conflict_and_not_joined():
    plots = [official(2289, 10, 0)]
    rows = [rows_file("plain.pdf", [pdf_row(2289, 1, 2, 3)])]
    _, links, conflicts = build(plots, rows, [excel_file("plain.xlsx", [(2289, "DT3")])])
    assert "no_neighbourhood" in kinds(conflicts)
    assert "cad" not in next(link for link in links if link["plot"] == 2289)
    mixed = [rows_file("a-IMC-.pdf", [pdf_row(2289, 1, 2, 3)])]
    _, _, conflicts = build(plots, mixed, [excel_file("b-IMD-.xlsx", [(2289, "DT3")])])
    assert "neighbourhood_mixed" in kinds(conflicts)


def test_r28_config_file_is_read_and_a_missing_one_stops_the_link_step(tmp_path, capsys):
    path = tmp_path / "plot_numbers.json"
    path.write_text(json.dumps({"note": "x", "neighbourhoods": {"IMC": {"shift": 10}}, "sourceCodePattern": "-(IMC)-", "searchRange": 2}), encoding="utf-8")
    loaded = link_table.load_shifts(path)
    assert loaded["shifts"] == {"IMC": 10} and loaded["range"] == 2
    assert link_table.neighbourhood_of("01101-PSN-IMC-ZZZ-DWG.pdf", loaded) == "IMC"
    assert link_table.main([str(tmp_path / "out"), "--cad", str(tmp_path), "--rows", "r", "--coords", "c", "--shifts", str(tmp_path / "none.json")]) == 2
    assert "Shift config not found" in capsys.readouterr().err


def test_r28_repo_config_holds_imc_10_and_imd_11_and_a_plain_note():
    loaded = link_table.load_shifts(link_table.DEFAULT_SHIFTS)
    assert loaded["shifts"] == {"IMC": 10, "IMD": 11}
    assert link_table.neighbourhood_of("00101-PSN-IMD-XXX-SCH-TU-090000.xlsx", loaded) == "IMD"
    note = json.loads(link_table.DEFAULT_SHIFTS.read_text(encoding="utf-8"))["note"]
    assert "minus shift" in note


def test_r30_cad_plot_type_is_an_info_field_and_never_compared_with_the_unit_type():
    plots = [square(1, 0, 0, kind="DP4")]
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3, "DT3")])]
    summary, links, conflicts = link_table.build({"plots": plots}, rows, [excel_file("b.xlsx", [(1, "DT3")])])
    assert links[0]["cadType"] == "DP4"
    assert conflicts == []


def test_r30_pdf_unit_type_that_differs_from_the_excel_unit_is_still_a_conflict():
    plots = [square(1, 0, 0, kind="DP4")]
    rows = [rows_file("a.pdf", [pdf_row(1, 1, 2, 3, "DT3")])]
    _, _, conflicts = link_table.build({"plots": plots}, rows, [excel_file("b.xlsx", [(1, "VD2")])])
    assert kinds(conflicts) == ["type_mismatch"]
