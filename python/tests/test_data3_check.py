import openpyxl
import pytest

import data3_check


def workbook(path):
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "Other"
    sales = book.create_sheet("JN Sales")
    sales.append(["Plot No", "Typology_Rev9", "Land Width (m)", "Land Length (m)"])
    sales.append([428, "VD4", 12.5, 25])
    sales.append([429, "DT3", 10, 25])
    book.save(path)
    return path


# R198 second method
def test_r198_column_index_from_a_cell_reference():
    assert data3_check.column_index("A1") == 0 and data3_check.column_index("AB12") == 27


def test_r198_xlsx_rows_read_the_sheet_xml_without_openpyxl(tmp_path):
    rows = data3_check.xlsx_rows(workbook(tmp_path / "s.xlsx"), "JN Sales")
    found = data3_check.by_header(rows, 1, {"plot": "Plot No", "typology": "Typology_Rev9", "width": "Land Width (m)"})
    assert found == [{"plot": 428.0, "typology": "VD4", "width": 12.5}, {"plot": 429.0, "typology": "DT3", "width": 10.0}]
    assert data3_check.number_key(found[0]["plot"]) == "428"


def test_r198_grid_iou_of_two_squares():
    a = [(0, 0), (10, 0), (10, 10), (0, 10)]
    b = [(5, 0), (15, 0), (15, 10), (5, 10)]
    assert data3_check.grid_iou(a, b, 0.5) == pytest.approx(1 / 3, abs=0.01)
    assert data3_check.grid_iou(a, a, 0.5) == 1.0


def test_r198_setbacks_and_widths_against_the_rows_of_the_main_code():
    plots = {"10": {"setbackFromFrontM": "5.0", "widthM": "16.0"}}
    setbacks = [{"plot": "10", "front": "5.5", "drawnFront": "5.0", "drawnClass": "5.00"}]
    found = data3_check.check_setbacks(setbacks, {"10": {"front": 5.5}}, plots, {"setbackLinesM": [5.0, 3.0]}, 0.05)
    assert found == {"front match": 1, "drawn match": 1, "drawnClass match": 1}
    widths = [{"plot": "10", "landWidth": "17.5", "landLength": "25.0", "typologyRev9": "C10", "widthAgainstCad": "differs"}]
    found = data3_check.check_widths(widths, {"10": {"landWidth": 17.5, "landLength": 25.0, "typology": "C10"}}, plots, 0.1)
    assert found == {"landWidth match": 1, "landLength match": 1, "typology match": 1, "widthAgainstCad match": 1}


def test_r198_one_number_only_for_a_face_with_one():
    assert data3_check.one_number("12") == "12" and data3_check.one_number("12|13") is None and data3_check.one_number("") is None
