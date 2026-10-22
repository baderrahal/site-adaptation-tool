import json
from pathlib import Path

import pytest
from openpyxl import Workbook

import excel_reader


@pytest.fixture
def dummy_book(tmp_path):
    book = Workbook()
    other = book.active
    other.title = "Notes"
    other.append(["ignore me"])
    sheet = book.create_sheet("coordinates")
    sheet.append([None, "Title of the sheet"])
    sheet.append([None, "A note that mentions the plot entrance"])
    sheet.append([])
    sheet.append(["Plot No.", "Easting", "Northing", "Easting", None, "El @ Entry*", "Height**"])
    sheet.append(["A1", 1000.123456, 2000.5, 7, "x", 171.51132273, 0.09967726])
    sheet.append(["A2", -0.0001, 3.14159, 8, "y", 171.5, 0.27441398])
    sheet.append([None, None, None, None, None, None, None])
    sheet.append(["A3", 1, 1, 1, "after the empty row", 1, 1])
    path = tmp_path / "coords.xlsx"
    book.save(path)
    return path


def make_book(tmp_path, rows):
    book = Workbook()
    sheet = book.active
    sheet.title = "Plots"
    for row in rows:
        sheet.append(row)
    path = tmp_path / "plots.xlsx"
    book.save(path)
    return path


def test_r9_header_is_the_row_with_plot_no_and_rows_above_it_are_not_data(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    data = json.loads((out / "coords.json").read_text(encoding="utf-8"))
    assert data["sheet"] == "coordinates"
    assert data["headerRow"] == 4
    assert [row["Plot No."] for row in data["rows"]] == ["A1", "A2"]


def test_r9_reading_stops_at_the_first_fully_empty_row(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    rows = json.loads((out / "coords.json").read_text(encoding="utf-8"))["rows"]
    assert len(rows) == 2


def test_r9_reading_stops_at_a_total_row_with_a_label(tmp_path):
    path = make_book(tmp_path, [["Plot No.", "FFL"], [1, 10.5], [2, 11.5], ["Total Units", 2], [3, 12.5]])
    rows = excel_reader.read_sheet(path, "Plots")[3]
    assert [row["Plot No."] for row in rows] == [1, 2]


def test_r9_reading_stops_at_a_total_row_with_no_plot_number(tmp_path):
    path = make_book(tmp_path, [["Plot No.", "Unit", "FFL"], [1, "A", 10.5], [2, "B", 11.5], [None, 410, None], [3, "C", 12.5]])
    rows = excel_reader.read_sheet(path, "Plots")[3]
    assert [row["Plot No."] for row in rows] == [1, 2]


def test_r9_header_text_is_found_when_it_shares_a_cell_with_other_words(tmp_path):
    path = make_book(tmp_path, [["Plot & Unit", "Plot No.", "FFL"], ["1-A", 1, 10.5]])
    sheet, header_row, headers, rows = excel_reader.read_sheet(path, "Plots")
    assert header_row == 1
    assert headers == ["Plot & Unit", "Plot No.", "FFL"]
    assert rows[0]["Plot No."] == 1


def test_r9_sheet_without_plot_no_is_an_error_naming_the_sheet(tmp_path):
    path = make_book(tmp_path, [["Name", "FFL"], ["a", 1]])
    with pytest.raises(ValueError) as problem:
        excel_reader.read_sheet(path, "Plots")
    assert "Plot No." in str(problem.value)
    assert "Plots" in str(problem.value)


def test_r9_headers_file_lists_headers_with_duplicates_and_blanks_named(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    headers = json.loads((out / "coords_headers.json").read_text(encoding="utf-8"))["headers"]
    assert headers == ["Plot No.", "Easting", "Northing", "Easting_2", "col_E", "El @ Entry*", "Height**"]


def test_r2_numbers_round_to_three_decimals_and_negative_zero_is_zero(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    rows = json.loads((out / "coords.json").read_text(encoding="utf-8"))["rows"]
    assert rows[0]["Easting"] == 1000.123
    assert rows[1]["Easting"] == 0
    assert rows[1]["Northing"] == 3.142


def test_r2_el_at_entry_and_height_keep_four_decimals(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    rows = json.loads((out / "coords.json").read_text(encoding="utf-8"))["rows"]
    assert rows[0]["El @ Entry*"] == 171.5113
    assert rows[0]["Height**"] == 0.0997
    assert rows[1]["Height**"] == 0.2744


def test_r9_missing_sheet_lists_sheets_present(dummy_book, tmp_path):
    with pytest.raises(ValueError) as problem:
        excel_reader.write_outputs(dummy_book, tmp_path / "out", "Nope")
    assert "Notes" in str(problem.value)
    assert "coordinates" in str(problem.value)


def test_r1_workbook_is_not_modified(dummy_book, tmp_path):
    before = Path(dummy_book).read_bytes()
    excel_reader.write_outputs(dummy_book, tmp_path / "out", "Coordinates")
    assert Path(dummy_book).read_bytes() == before


def test_missing_workbook_returns_error_code(tmp_path):
    assert excel_reader.main([str(tmp_path / "none.xlsx"), str(tmp_path / "out")]) == 2


def test_missing_sheet_returns_error_code(dummy_book, tmp_path):
    assert excel_reader.main([str(dummy_book), str(tmp_path / "out"), "--sheet", "Nope"]) == 3
