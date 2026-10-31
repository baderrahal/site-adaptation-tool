"""R412. The read only check of the inputs of a new project, python/new_project_check.py, on small inputs built in code (R10). The C# side
(dotnet/SiteAdaptation.Tests/R412_NewProjectCheckTests.cs) builds the same inputs and holds the same lines, so the two sides give one report."""
import json

import openpyxl
import pytest

import input_guard
import new_project_check
import new_project_check as npc

GUID = "7a7813d7-ad61-4bf2-a75f-6234c92957bb"
PROTOTYPE = "KEO-ARC-C10-ZZ.rvt"

LINES = [
    ("position file", "ok", "pos.json holds the project position, the survey point, the project base point and 2 levels", None),
    ("parameter sheet", "does not fit",
     "3 of 4 parameter names have no GUID in addin.sharedParameters: AA_Two (no GUID), AA_Three (not in the sheet), AA_Four (not in the sheet)",
     "the names are held to the prefix AA_ of the %s parameters" % npc.NAME),
    ("villa offsets", "does not fit", "plotFrame.units of prototype_offsets.json holds no offset for the unit codes: VL3, TH1", "measured on the %s past job N1" % npc.NAME),
    ("hand tables", "does not fit", "the hand tables hold no entry for the prototype files of the unit codes: TH1",
     "the tables are keyed by the KEO prototype file names of the %s job" % npc.NAME),
    ("prototype files", "does not fit", "the prototype folder protos holds no single file for the unit codes: TH1", "files are found by the -ARC-<code>- part of the KEO file names"),
    ("Excel dlus dataBook.rev9.dlus", "does not fit", "a.xlsx sheet S1 row 2 lacks the columns: NBH", "held to the sheet S1 and the headers Plot, NBH"),
    ("Excel dlus dataBook.rev9.sales", "does not fit", "a.xlsx has no sheet Nope", "held to the sheet Nope and the headers Plot"),
    ("Excel dlus dataBook.data5.areaKey.dlus", "ok", "a.xlsx sheet S1 row 2 holds all 1 columns the readers use", "held to the sheet S1 and the headers Plot"),
    ("Excel dataBook.data5.areaKey.sales", "does not fit", "config names no workbook, sheet, headerRow and columns in dataBook.data5.areaKey.sales", None),
    ("Excel dataBook.locationPlan.sales", "does not fit", "config names no workbook, sheet, headerRow and columns in dataBook.locationPlan.sales", None),
    ("Excel dataBook.locationPlan.dlus", "does not fit", "config names no workbook, sheet, headerRow and columns in dataBook.locationPlan.dlus", None),
    ("Schedule N1", "ok", "s1.xlsx sheet Sched holds the Plot No. header row", "held to the sheet Sched and a header cell with Plot No."),
    ("location plan PDF", "ok", "plan.pdf is one page and holds text that fits the number and code patterns",
     "held to the %s label patterns of dataBook.locationPlan.patterns: number, clientCode" % npc.NAME),
]


def made_pdf(path, text="(C10 12) Tj"):
    """A PDF of one page of 600 by 400 pt with a text, the bytes built in code as test_location_plan.made_pdf builds them."""
    content = ("BT /F1 10 Tf 100 300 Td %s ET" % text).encode()
    objects = [b"<< /Type /Catalog /Pages 2 0 R >>", b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
               b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 400] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
               b"<< /Length %d >>\nstream\n" % len(content) + content + b"\nendstream", b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
    out, offsets = bytearray(b"%PDF-1.4\n"), []
    for number, body in enumerate(objects, 1):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % number + body + b"\nendobj\n"
    start = len(out)
    out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1) + b"".join(b"%010d 00000 n \n" % offset for offset in offsets)
    out += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objects) + 1, start)
    path.write_bytes(bytes(out))


def sheet_book(path, sheet, rows):
    book = openpyxl.Workbook()
    book.active.title = sheet
    for row in rows:
        book.active.append(row)
    book.save(path)


def build(tmp):
    """The made up project: a config, a rules profile, an offsets file, the position file, two workbooks, a schedule, a PDF and a prototype folder."""
    work, root, config = tmp / "w", tmp / "in", tmp / "cfg"
    for folder in (work / "data_book" / "work" / "Book", root / "refs", root / "protos", config):
        folder.mkdir(parents=True)
    point = {"internalM": [1.0, 2.0, 3.0], "sharedM": [4.0, 5.0, 6.0]}
    (work / "pos.json").write_text(json.dumps({
        "format": "site-adaptation-position", "formatVersion": 1, "projectPosition": {"eastM": 1.0, "northM": 2.0, "elevationM": 3.0, "angleDeg": 0.5},
        "surveyPoint": point, "projectBasePoint": point, "levels": [{"name": "L1", "elevationM": 0.0}, {"name": "L2", "elevationM": 3.0}], "levelBase": "Project Base Point"}))
    sheet_book(work / "data_book" / "work" / "Book" / "a.xlsx", "S1", [["title"], ["Plot", "Other"], ["1", "x"]])
    made_pdf(work / "data_book" / "work" / "Book" / "plan.pdf")
    sheet_book(root / "refs" / "s1.xlsx", "Sched", [["t"]] + [[None]] * 38 + [["Plot No.", "Size"]])
    (root / "protos" / PROTOTYPE).write_bytes(b"x")
    (config / "prototype_offsets.json").write_text(json.dumps({"plotFrame": {"units": {"C10": {}}}}))
    (config / "rules.json").write_text(json.dumps({
        "typologies": {"C10": {}, "VL3": {"keo_file": "C10"}, "TH1": {}},
        "levelProfiles": {"default": "p", "p": {"keyRules": {"parameter AA_Four": {}, "hand villa": {"byFile": {PROTOTYPE: {}}}}}}}))
    (config / "project.json").write_text(json.dumps({
        "inputRoot": str(root), "floors": {"plotParameter": "AA_One"}, "pastJob": {"neighbourhood": "N1", "prototypeFolder": "protos", "parameters": ["AA_Two", "AA_Three"]},
        "majority": {"parameterLevels": []}, "connect": {"neighbourhoods": {"N1": {"schedule": "s1.xlsx"}, "N2": None}, "scheduleSheet": "Sched"},
        "addin": {"workbookRoles": {"bk": "dlus"}, "position": {"file": "{workRoot}/pos.json"}, "readData": {"dataBookFolder": "data_book", "scheduleFolder": "refs"},
                  "sharedParameters": {"parameters": [{"name": "AA_One", "guid": GUID}, {"name": "AA_Two", "guid": None}]}},
        "dataBook": {"rev9": {"workbooks": {"bk": "Book/a.xlsx"}, "dlus": {"workbook": "bk", "sheet": "S1", "headerRow": 2, "columns": {"plot": "Plot", "nbh": "NBH"}},
                              "sales": {"workbook": "bk", "sheet": "Nope", "headerRow": 1, "columns": {"plot": "Plot"}}},
                     "data5": {"areaKey": {"dlus": {"workbook": "bk", "sheet": "S1", "headerRow": 2, "columns": {"plot": "Plot"}}}},
                     "locationPlan": {"pdf": "Book/plan.pdf", "patterns": {"number": "\\d{1,4}", "clientCode": "[A-Z]{1,3}\\d{1,2}"}, "codeKinds": ["clientCode"]}}}))
    return work, root, config


def run(tmp):
    work, root, config = build(tmp)
    return new_project_check.check(config / "project.json", config / "rules.json", config / "prototype_offsets.json", work, root), work, root, config


def shown(report):
    return [(each["input"], each["status"], each["text"], each[npc.KEY_NOTE]) for each in report["lines"]]


def test_r412_the_check_gives_one_line_per_input_with_its_status_and_job_only_note_on_made_up_inputs(tmp_path):
    (report, paths, root), work, _, _ = run(tmp_path)
    assert shown(report) == LINES
    assert [each[npc.KEY] for each in report["lines"]] == [note is not None for _, _, _, note in LINES]
    assert report["counts"] == {"ok": 4, "missing": 0, "doesNotFit": 9, npc.KEY: 9}
    assert [each["name"] for each in report["inputs"]] == sorted(each["name"] for each in report["inputs"])
    assert all(each["unchanged"] for each in report["inputs"]) and len(report["inputs"]) == 7
    text = new_project_check.text_of(report).split("\n")
    assert text[0] == "New project check, 13 lines: 4 ok, 0 missing, 9 do not fit, 9 " + npc.ONLY
    assert text[1] == "position file: ok. pos.json holds the project position, the survey point, the project base point and 2 levels"
    assert text[-2] == "Inputs opened: 7, none changed"


def test_r412_an_input_that_is_not_there_is_missing_and_one_that_cannot_be_read_does_not_fit(tmp_path):
    work, root, config = build(tmp_path)
    (work / "pos.json").unlink()
    (work / "data_book" / "work" / "Book" / "a.xlsx").write_bytes(b"not a workbook")
    (work / "data_book" / "work" / "Book" / "plan.pdf").write_bytes(b"not a pdf")
    (root / "refs" / "s1.xlsx").unlink()
    (config / "prototype_offsets.json").write_text("[]")
    (root / "protos" / PROTOTYPE).unlink()
    report = new_project_check.check(config / "project.json", config / "rules.json", config / "prototype_offsets.json", work, root)[0]
    got = {each["input"]: (each["status"], each["text"]) for each in report["lines"]}
    assert got["position file"] == ("missing", "pos.json is not there")
    assert got["villa offsets"] == ("does not fit", "prototype_offsets.json cannot be read as a JSON object")
    assert got["Excel dlus dataBook.rev9.dlus"] == ("does not fit", "a.xlsx cannot be read as a workbook")
    assert got["Schedule N1"] == ("missing", "s1.xlsx is not there")
    assert got["location plan PDF"] == ("does not fit", "plan.pdf cannot be read as a PDF")
    assert got["prototype files"][1] == "the prototype folder protos holds no single file for the unit codes: C10, VL3, TH1"
    assert new_project_check.text_of(report).count("missing.") == 2


def test_r412_a_position_file_with_a_missing_point_or_level_does_not_fit_and_names_what_is_wrong(tmp_path):
    work, root, config = build(tmp_path)
    found = json.loads((work / "pos.json").read_text())
    found["formatVersion"] = 2
    found["surveyPoint"]["sharedM"] = [1.0, 2.0]
    found["projectPosition"].pop("angleDeg")
    found["levels"][1].pop("name")
    found.pop("levelBase")
    (work / "pos.json").write_text(json.dumps(found))
    report = new_project_check.check(config / "project.json", config / "rules.json", config / "prototype_offsets.json", work, root)[0]
    assert (report["lines"][0]["status"], report["lines"][0]["text"]) == (
        "does not fit", "pos.json lacks or has wrong: formatVersion, projectPosition.angleDeg, surveyPoint.sharedM, levels[1].name, levelBase")
    assert report["lines"][0][npc.KEY] is False


def test_r412_an_input_that_changes_while_the_check_reads_it_fails_the_run(tmp_path, monkeypatch):
    work, root, config = build(tmp_path)
    real = new_project_check.open_book

    def touching(path):
        found = real(path)
        with open(path, "ab") as handle:
            handle.write(b"x")
        return found
    monkeypatch.setattr(new_project_check, "open_book", touching)
    with pytest.raises(input_guard.InputChanged) as refused:
        new_project_check.check(config / "project.json", config / "rules.json", config / "prototype_offsets.json", work, root)
    assert "its size" in str(refused.value) and "SHA-256" in str(refused.value)


def test_r412_the_report_is_written_to_the_output_folder_only_and_never_beside_an_input(tmp_path, capsys):
    work, root, config = build(tmp_path)
    before = sorted(str(path) for path in tmp_path.rglob("*"))
    args = ["--config", str(config / "project.json"), "--rules", str(config / "rules.json"), "--work-root", str(work)]
    assert new_project_check.main(args + ["--out", str(config)]) == 1
    assert "beside an input" in capsys.readouterr().err
    assert new_project_check.main(args + ["--out", str(root / "reports")]) == 1
    assert "under the input root" in capsys.readouterr().err
    assert sorted(str(path) for path in tmp_path.rglob("*")) == before
    out = tmp_path / "out"
    assert new_project_check.main(args + ["--out", str(out)]) == 0
    assert sorted(path.name for path in out.iterdir()) == ["new_project_check.json", "new_project_check.txt"]
    written = json.loads((out / "new_project_check.json").read_text(encoding="utf-8"))
    assert written["format"] == "site-adaptation-new-project-check" and [each["input"] for each in written["lines"]][0] == "position file"
    assert (out / "new_project_check.txt").read_text(encoding="utf-8").startswith("New project check, 13 lines")
    assert sorted(str(path) for path in tmp_path.rglob("*") if out not in path.parents and path != out) == before


def test_r412_a_rules_profile_with_no_typologies_stops_the_check_with_its_reason(tmp_path):
    work, root, config = build(tmp_path)
    (config / "rules.json").write_text("{}")
    with pytest.raises(new_project_check.CheckError) as refused:
        new_project_check.check(config / "project.json", config / "rules.json", config / "prototype_offsets.json", work, root)
    assert "holds no typologies" in str(refused.value)
