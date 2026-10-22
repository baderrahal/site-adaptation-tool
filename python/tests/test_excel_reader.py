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
    sheet.append(["Plot", "Easting", "Northing", "Easting", None])
    sheet.append(["A1", 1000.123456, 2000.5, 7, "x"])
    sheet.append([None, None, None, None, None])
    sheet.append(["A2", -0.0001, 3.14159, 8, "y"])
    path = tmp_path / "coords.xlsx"
    book.save(path)
    return path


def test_r9_rows_keyed_by_header_and_blank_rows_skipped(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    data = json.loads((out / "coords.json").read_text(encoding="utf-8"))
    assert data["sheet"] == "coordinates"
    assert len(data["rows"]) == 2
    assert data["rows"][0]["Plot"] == "A1"


def test_r9_headers_file_lists_headers_with_duplicates_and_blanks_named(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    headers = json.loads((out / "coords_headers.json").read_text(encoding="utf-8"))["headers"]
    assert headers == ["Plot", "Easting", "Northing", "Easting_2", "col_E"]


def test_r2_numbers_round_to_three_decimals_and_negative_zero_is_zero(dummy_book, tmp_path):
    out = tmp_path / "out"
    excel_reader.write_outputs(dummy_book, out, "Coordinates")
    rows = json.loads((out / "coords.json").read_text(encoding="utf-8"))["rows"]
    assert rows[0]["Easting"] == 1000.123
    assert rows[1]["Easting"] == 0
    assert rows[1]["Northing"] == 3.142


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
