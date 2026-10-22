import json

from openpyxl import Workbook

import excel_reader


def book(path, sheets):
    workbook = Workbook()
    workbook.remove(workbook.active)
    for title, rows in sheets.items():
        sheet = workbook.create_sheet(title)
        for row in rows:
            sheet.append(row)
    workbook.save(path)
    return path


def test_r32_survey_lists_sheets_header_rows_row_counts_and_says_when_no_plot_number_column_exists(tmp_path):
    path = book(tmp_path / "m.xlsx", {
        "Summary": [["Title"], [], ["Land use", "Typology", "Plot Count", "Land Area"], ["Villa", "A", 3, 900], ["Town", "B", 2, 400]],
        "Empty": [],
    })
    report = excel_reader.survey(path)
    assert report["sheetCount"] == 2 and report["plotColumnSheets"] == 0
    summary = report["sheets"][0]
    assert summary["headerRow"] == 3 and summary["rows"] == 5 and summary["dataRows"] == 2
    assert summary["plotColumn"] is None
    assert summary["plotHeaders"] == ["Plot Count"]
    assert report["sheets"][1]["headerRow"] is None


def test_r32_survey_finds_the_plot_number_column_and_tests_the_numbering_with_area(tmp_path):
    path = book(tmp_path / "m.xlsx", {"Plots": [["Plot No.", "Area"], [21, 100.0], [22, 200.0], [23, 300.0]]})
    cad = {11: 100.0, 12: 200.5, 13: 300.0}
    report = excel_reader.survey(path, cad, [10, 11])
    sheet = report["sheets"][0]
    assert sheet["plotColumn"] == "Plot No." and sheet["plotNumbers"] == 3
    assert sheet["numbering"] == {"asFound": 0, "minus10": 3, "minus11": 0}


def test_r32_survey_without_an_area_column_gives_no_numbering_verdict(tmp_path):
    path = book(tmp_path / "m.xlsx", {"Plots": [["Plot No.", "Use"], [21, "x"]]})
    sheet = excel_reader.survey(path, {11: 100.0}, [10])["sheets"][0]
    assert sheet["plotColumn"] == "Plot No." and "numbering" not in sheet


def test_r32_main_survey_writes_survey_json_and_opens_the_workbook_read_only(tmp_path, capsys):
    path = book(tmp_path / "m.xlsx", {"Plots": [["Plot No.", "Area"], [21, 100.0]]})
    before = path.read_bytes()
    plots = tmp_path / "plots.json"
    plots.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": "11", "area": 100.0}]}), encoding="utf-8")
    shifts = tmp_path / "shifts.json"
    shifts.write_text(json.dumps({"neighbourhoods": {"IMC": {"shift": 10}}}), encoding="utf-8")
    assert excel_reader.main([str(path), str(tmp_path / "out"), "--survey", "--cad-plots", str(plots), "--shifts", str(shifts)]) == 0
    report = json.loads((tmp_path / "out" / "survey.json").read_text(encoding="utf-8"))
    assert report["sheets"][0]["numbering"] == {"asFound": 0, "minus10": 1}
    assert path.read_bytes() == before
    assert "sheets 1" in capsys.readouterr().out
