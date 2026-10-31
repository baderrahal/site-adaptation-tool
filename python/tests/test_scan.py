"""R420 to R427. The scanner on small made up reads: layers A, B and C, codes X1 to X3 of a made up profile, a workbook written into the
temporary folder and character lists of a PDF page."""

import json

from openpyxl import Workbook

import scan
from minimal_pdf import build_pdf

BLOCK = {"labelPattern": "^(?:(.*?)\\s+)?(\\d{1,6})$", "tokenSplit": "[^A-Za-z0-9]+", "plotNumberShifts": [0, 10, 11], "pairMinSegments": 4,
         "pdfJoinGapPt": 0.3, "pdfLineTolerancePt": 0.05}
CODES = {"X1", "X2", "X3"}
PAIRS = {frozenset(("X2", "X3"))}
NAMES = {"prefix": "P-", "discipline": "D", "suffix": "-S-"}


def face(x, y, size=10):
    ring = [[x, y], [x + size, y], [x + size, y + size], [x, y + size]]
    return {"ring": ring, "centroid": [x + size / 2, y + size / 2], "area": size * size}


def text(layer, value, x, y):
    return {"layer": layer, "text": value, "x": x, "y": y}


def insert(layer, block, x, y):
    return {"layer": layer, "block": block, "x": x, "y": y}


def cad(source, candidates, texts=(), inserts=(), layers=None):
    found = sorted({layer for candidate in candidates for layer in candidate["layers"]} | {item["layer"] for item in list(texts) + list(inserts)})
    return {"source": source, "path": source, "size": 1, "sha256": "0" * 64, "units": "m",
            "layers": layers or [{"layer": layer, "closedOutlines": 0, "texts": 0, "blocks": 0, "segments": 0} for layer in found],
            "candidates": list(candidates), "texts": list(texts), "inserts": list(inserts), "lines": {}, "arcs": {}}


def two_plots():
    """A drawing whose pair A + B makes two plots and whose layer C makes one, the labels on layer T."""
    return cad("one.dwg", [{"layers": ["C"], "faces": [face(0, 0, 30)]}, {"layers": ["A", "B"], "faces": [face(0, 0), face(20, 0)]}],
               [text("T", "101", 5, 5), text("T", "102", 25, 5)])


def result(cads, excels=(), pdfs=(), inputs=()):
    return scan.scan_result(BLOCK, CODES, PAIRS, list(cads), list(excels), list(pdfs), list(inputs))


def kind_rows(found, kind):
    return [item for item in found["rows"] if item["kind"] == kind]


def test_r421_the_inside_test_is_the_even_odd_ray_test_of_the_cad_reader():
    ring = [(0, 0), (10, 0), (10, 10), (5, 5), (0, 10)]
    assert scan.contains(ring, 2, 2) and not scan.contains(ring, 5, 8) and not scan.contains(ring, 12, 2)
    # A point on the left edge counts as inside and one on the right edge as outside, as the C# test gives them
    assert scan.contains([(0, 0), (10, 0), (10, 10), (0, 10)], 0, 5) and not scan.contains([(0, 0), (10, 0), (10, 10), (0, 10)], 10, 5)


def test_r421_a_plot_outline_made_by_the_pair_of_two_layers_wins():
    found = result([two_plots()])
    outlines = {item["source"]: item for item in kind_rows(found, "plotOutline")}
    assert outlines["one.dwg|A + B"]["score"] == 2 and outlines["one.dwg|A + B"]["proposedUse"] == "plot outlines"
    # Face C holds two labels, so it covers no plot
    assert outlines["one.dwg|C"]["score"] == 0 and outlines["one.dwg|C"]["proposedUse"] == "check"
    assert [item["plot"] for item in found["plots"]] == ["101", "102"]


def test_r421_a_tie_proposes_none_and_writes_a_check_row():
    tied = cad("one.dwg", [{"layers": ["A"], "faces": [face(0, 0)]}, {"layers": ["B"], "faces": [face(0, 0)]}], [text("T", "101", 5, 5)])
    found = result([tied])
    assert all(item["proposedUse"] == "check" for item in kind_rows(found, "plotOutline"))
    assert {"what": "tie", "source": "plotOutline", "plot": "", "value": "one.dwg|A or one.dwg|B"} in found["checks"]
    assert {"what": "noPlotDrawing", "source": "", "plot": "", "value": ""} in found["checks"] and found["plots"] == []


def test_r421_a_tie_of_two_drawings_makes_no_plot_drawing():
    found = result([two_plots(), dict(two_plots(), source="two.dwg")])
    assert {"what": "tie", "source": "plotDrawing", "plot": "", "value": "one.dwg or two.dwg"} in found["checks"]
    assert found["plots"] == []


def test_r421_a_number_two_plots_of_the_plot_drawing_hold_is_a_check_and_joins_nothing():
    drawing = cad("one.dwg", [{"layers": ["A"], "faces": [face(0, 0), face(20, 0), face(40, 0)]}],
                  [text("T", "101", 5, 5), text("T", "101", 25, 5), text("T", "102", 45, 5)])
    found = result([drawing])
    assert {"what": "numberTwice", "source": "one.dwg|A", "plot": "101", "value": "2"} in found["checks"]
    assert [item["plot"] for item in found["plots"]] == ["102"]


def test_r420_one_cad_layer_row_per_layer_with_its_counts():
    layers = [{"layer": "A", "closedOutlines": 2, "texts": 0, "blocks": 1, "segments": 8}, {"layer": "T", "closedOutlines": 0, "texts": 2, "blocks": 0, "segments": 0}]
    found = result([dict(two_plots(), layers=layers)])
    assert kind_rows(found, "cadLayer") == [
        {"kind": "cadLayer", "source": "one.dwg|A", "found": "2 closed outlines, 0 texts, 1 blocks", "proposedUse": "", "score": 0, "conflicts": 0},
        {"kind": "cadLayer", "source": "one.dwg|T", "found": "0 closed outlines, 2 texts, 0 blocks", "proposedUse": "", "score": 0, "conflicts": 0}]


def test_r422_the_text_layer_of_the_labels_in_plots_is_proposed_as_the_plot_numbers():
    drawing = two_plots()
    drawing["texts"].append(text("N", "note", 5, 6))
    rows = {item["source"]: item for item in kind_rows(result([drawing]), "plotNumber")}
    assert rows["one.dwg|T"] == {"kind": "plotNumber", "source": "one.dwg|T", "found": "2 labels in plots", "proposedUse": "plot numbers", "score": 2, "conflicts": 0}
    assert rows["one.dwg|N"]["score"] == 0 and rows["one.dwg|N"]["proposedUse"] == "check"


def test_r423_the_insert_layer_inside_the_most_plots_is_proposed_as_the_villa_blocks():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1", 5, 5), insert("V", "X2", 25, 5), insert("W", "X1", 5, 5), insert("W", "X1", 6, 6), insert("W", "X1", 99, 99)]
    rows = {item["source"]: item for item in kind_rows(result([drawing]), "villaBlock")}
    assert rows["one.dwg|V"]["score"] == 2 and rows["one.dwg|V"]["proposedUse"] == "villa blocks"
    assert rows["one.dwg|W"] == {"kind": "villaBlock", "source": "one.dwg|W", "found": "3 inserts", "proposedUse": "check", "score": 1, "conflicts": 0}


def test_r424_two_plots_of_one_number_label_and_place_link_with_no_conflict():
    other = cad("two.dwg", [{"layers": ["A"], "faces": [face(0, 0)]}], [text("T", "101", 5, 5)])
    found = result([two_plots(), other])
    assert found["conflicts"] == []


def test_r424_the_conflict_rows_of_location_label_and_number():
    drawing = two_plots()
    drawing["candidates"][1]["faces"].append(face(40, 0))
    drawing["texts"].append(text("T", "105", 45, 5))
    other = cad("two.dwg", [{"layers": ["A"], "faces": [face(100, 0), face(20, 0)]}], [text("T", "X 101", 105, 5), text("T", "104", 25, 5)])
    found = result([drawing, other])
    assert found["conflicts"] == [
        {"plot": "101", "field": "label", "valueA": "101", "sourceA": "one.dwg|A + B", "valueB": "X 101", "sourceB": "two.dwg|A"},
        {"plot": "101", "field": "location", "valueA": "5.000, 5.000", "sourceA": "one.dwg|A + B", "valueB": "105.000, 5.000", "sourceB": "two.dwg|A"},
        {"plot": "102", "field": "number", "valueA": "102", "sourceA": "one.dwg|A + B", "valueB": "104", "sourceB": "two.dwg|A"}]
    outline = {item["source"]: item for item in kind_rows(found, "plotOutline")}
    assert outline["one.dwg|A + B"]["conflicts"] == 3 and outline["two.dwg|A"]["conflicts"] == 3


def test_r425_a_value_is_a_villa_type_by_its_whole_or_one_token_with_case():
    assert scan.value_codes("X1", CODES, BLOCK["tokenSplit"]) == ["X1"]
    assert scan.value_codes("Unit_X2-left", CODES, BLOCK["tokenSplit"]) == ["X2"]
    assert scan.value_codes("x1", CODES, BLOCK["tokenSplit"]) == [] and scan.value_codes("X10", CODES, BLOCK["tokenSplit"]) == []
    assert scan.value_codes("X1 / X2", CODES, BLOCK["tokenSplit"]) == ["X1", "X2"]


def test_r425_the_codes_are_the_profile_codes_and_the_prototype_names_read_by_prototype_names():
    class Profile:
        values = {"typologies": {"X1": {"keo_file": "X1"}, "X4": {"keo_file": "X5"}}, "typology_codes.rows": [{"old": "X2", "new": "X3"}, {"old": "X6", "new": None}]}

        def get(self, key):
            return self.values[key]

    assert scan.prototype_code("P-D-X7-S-0001.rvt", NAMES) == "X7" and scan.prototype_code("Q-D-X7-S-0001.rvt", NAMES) is None
    assert scan.known_codes(Profile(), NAMES, ["P-D-X7-S-0001.rvt", "other.rvt"]) == {"X1", "X2", "X3", "X4", "X5", "X6", "X7"}
    assert scan.old_new_pairs(Profile()) == {frozenset(("X2", "X3"))}


def test_r425_two_codes_in_one_value_is_a_check_and_gives_no_villa_type():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1_X2", 5, 5), insert("V", "X1", 25, 5)]
    found = result([drawing])
    assert {"what": "twoCodes", "source": "one.dwg|block|V", "plot": "101", "value": "X1_X2"} in found["checks"]
    assert [item["villaType"] for item in found["plots"]] == [{}, {"one.dwg|block|V": "X1"}]


def test_r425_a_plot_two_values_give_two_codes_is_a_check():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1", 5, 5), insert("V", "X2", 6, 6)]
    found = result([drawing])
    assert {"what": "twoCodes", "source": "one.dwg|block|V", "plot": "101", "value": "X1, X2"} in found["checks"]


def test_r426_another_code_of_another_source_is_a_conflict_with_both_values():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1", 5, 5), insert("V", "X1", 25, 5)]
    drawing["texts"] += [text("K", "X2", 6, 6)]
    found = result([drawing])
    rows = {item["source"]: item for item in kind_rows(found, "villaType")}
    assert rows["one.dwg|block|V"]["proposedUse"] == "villa type" and rows["one.dwg|block|V"]["score"] == 2
    assert rows["one.dwg|text|K"] == {"kind": "villaType", "source": "one.dwg|text|K", "found": "X2", "proposedUse": "check", "score": 1, "conflicts": 1}
    assert found["conflicts"] == [{"plot": "101", "field": "villa type", "valueA": "X1", "sourceA": "one.dwg|block|V", "valueB": "X2", "sourceB": "one.dwg|text|K"}]


def test_r426_the_old_and_new_code_of_one_row_is_a_check_not_a_conflict():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X2", 5, 5), insert("V", "X1", 25, 5)]
    drawing["texts"] += [text("K", "X3", 6, 6)]
    found = result([drawing])
    assert found["conflicts"] == []
    assert {"what": "oldNewPair", "source": "one.dwg|text|K", "plot": "101", "value": "X2, X3"} in found["checks"]
    assert found["plots"][0]["villaType"] == {"one.dwg|block|V": "X2", "one.dwg|text|K": "X3"}


def test_r426_a_tie_of_two_sources_proposes_no_villa_type():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1", 5, 5)]
    drawing["texts"] += [text("K", "X1", 6, 6)]
    found = result([drawing])
    assert all(item["proposedUse"] == "check" for item in kind_rows(found, "villaType"))
    assert {"what": "tie", "source": "villaType", "plot": "", "value": "one.dwg|block|V or one.dwg|text|K"} in found["checks"]


def workbook(tmp_path, rows, name="table.xlsx"):
    book = Workbook()
    sheet = book.active
    sheet.title = "S1"
    for found in rows:
        sheet.append(found)
    path = tmp_path / name
    book.save(path)
    return path


def test_r426_the_excel_plot_column_and_shift_that_match_the_most_plots_join_the_rows(tmp_path):
    # Column B under shift 10 gives 101 and 102, under shift 11 only 101, and 114 joins no plot
    path = workbook(tmp_path, [[None], ["Ref", None, "Kind"], [5, 111, "X1"], [6, 112, "type X2"], [7, 114, "X1"]])
    excel = scan.read_excel(path)
    assert excel["sheets"][0]["headers"] == ["Ref", "B", "Kind"]
    found = result([two_plots()], excels=[excel])
    rows = {item["source"]: item for item in kind_rows(found, "villaType")}
    assert rows["table.xlsx|S1|Kind|shift 10"] == {"kind": "villaType", "source": "table.xlsx|S1|Kind|shift 10", "found": "X1, X2", "proposedUse": "villa type",
                                                   "score": 2, "conflicts": 0}
    assert [item["villaType"] for item in found["plots"]] == [{"table.xlsx|S1|Kind|shift 10": "X1"}, {"table.xlsx|S1|Kind|shift 10": "X2"}]


def test_r426_a_tie_of_two_columns_joins_no_rows_and_writes_a_check(tmp_path):
    path = workbook(tmp_path, [["P", "Q", "Kind"], [101, 101, "X1"], [102, 102, "X1"]])
    found = result([two_plots()], excels=[scan.read_excel(path)])
    assert kind_rows(found, "villaType") == []
    assert {"what": "tie", "source": "excelJoin", "plot": "", "value": "table.xlsx|S1|P|shift 0 or table.xlsx|S1|Q|shift 0"} in found["checks"]


def test_r426_a_cell_is_its_text_and_a_whole_number_has_no_decimals():
    assert [scan.cell_text(value) for value in (None, 101, 101.0, 2.5, "X1")] == ["", "101", "101", "2.5", "X1"]


def char(value, x0, x1, top):
    return {"text": value, "x0": x0, "x1": x1, "top": top}


def test_r426_pdf_characters_join_into_words_by_the_gap_and_lines_by_the_tolerance():
    chars = [char("X", 10, 15, 100), char("1", 15.2, 20, 100.04), char("1", 30, 35, 100), char("0", 35, 40, 100), char("1", 40, 45, 100),
             char(" ", 45, 47, 100), char("Z", 10, 15, 120)]
    # A gap of 0.2 joins X1, the gap of 10 parts the number, the row at top 120 lies lower on the page and comes after
    assert scan.pdf_lines(chars, 800, 0.3, 0.05) == ["X1 101", "Z"]
    # A tolerance under the 0.04 between the two rows parts the 1 at top 100.04 from its row
    assert scan.pdf_lines(chars, 800, 0.3, 0.01) == ["X 101", "1", "Z"]


def test_r426_a_pdf_line_joins_by_its_one_number_token_under_the_shift_of_the_most_plots():
    # Shift 11 gives 101 and 102, shift 10 gives 102 alone. The line of page 2 holds two numbers of plots under shift 11 and joins none
    pdf = {"name": "list.pdf", "pages": [["X1 112", "X2 113", "note"], ["X2 112 113"]]}
    found = result([two_plots()], pdfs=[pdf])
    assert [item["source"] for item in kind_rows(found, "villaType")] == ["list.pdf|page 1|shift 11"]
    assert [item["villaType"] for item in found["plots"]] == [{"list.pdf|page 1|shift 11": "X1"}, {"list.pdf|page 1|shift 11": "X2"}]


def test_r427_the_lists_keep_the_order_and_field_texts_of_the_design():
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "X1", 5, 5)]
    found = result([drawing], inputs=[{"role": "config", "name": "project.json", "size": 2, "sha256": "a"}])
    assert list(found) == ["inputs", "rows", "conflicts", "checks", "plots"]
    assert [item["kind"] for item in found["rows"]] == ["cadLayer"] * 4 + ["plotOutline"] * 2 + ["plotNumber", "villaBlock", "villaType"]
    assert [item["source"] for item in found["rows"]][:6] == ["one.dwg|A", "one.dwg|B", "one.dwg|C", "one.dwg|T", "one.dwg|A + B", "one.dwg|C"]
    assert all(list(item) == ["kind", "source", "found", "proposedUse", "score", "conflicts"] for item in found["rows"])
    assert found["plots"][0] == {"plot": "101", "label": "101", "centroid": "5.000, 5.000", "villaType": {"one.dwg|block|V": "X1"}}


def settings_files(tmp_path):
    profile = tmp_path / "profile.json"
    profile.write_text(json.dumps({"typologies": {"X1": {"keo_file": "X1"}, "X2": {"keo_file": "X1"}}, "typology_codes": {"rows": [{"old": "X2", "new": "X3"}]},
                                   "scan": BLOCK}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile), "prototypeNames": NAMES}), encoding="utf-8")
    return settings


def test_r427_main_writes_scan_python_with_the_inputs_in_role_order_and_a_guard_record(tmp_path):
    settings = settings_files(tmp_path)
    drawing = two_plots()
    drawing["inserts"] += [insert("V", "P-D-X4-S-1", 5, 5)]
    read = tmp_path / "scan_cad_1.json"
    read.write_text(json.dumps(drawing), encoding="utf-8")
    book = workbook(tmp_path, [["Plot", "Kind"], [101, "X1"]])
    prototypes = tmp_path / "protos"
    prototypes.mkdir()
    (prototypes / "P-D-X4-S-1.rvt").write_bytes(b"x")
    out = tmp_path / "run"
    assert scan.main(["--settings", str(settings), "--cad-read", str(read), "--excel", str(book), "--prototypes", str(prototypes), "--out", str(out)]) == 0
    written = json.loads((out / scan.OUTPUT).read_text(encoding="utf-8"))
    assert [item["role"] for item in written["inputs"]] == ["config", "scanCad:1", "scanExcel:1", "prototype:P-D-X4-S-1.rvt"]
    assert written["inputs"][1] == {"role": "scanCad:1", "name": "one.dwg", "size": 1, "sha256": "0" * 64}
    # X4 is a code only by the prototype file name, the join column Plot gives no source
    assert written["plots"][0]["villaType"] == {"one.dwg|block|V": "X4", "table.xlsx|S1|Kind|shift 0": "X1"}
    record = json.loads((out / scan.RECORD).read_text(encoding="utf-8"))
    assert {item["name"] for item in record["inputs"]} == {"project.json", "scan_cad_1.json", "table.xlsx", "P-D-X4-S-1.rvt"}
    assert all(item["unchanged"] for item in record["inputs"])


def test_r426_a_pdf_file_is_read_by_pdfplumber_into_the_lines_of_each_page(tmp_path):
    path = tmp_path / "list.pdf"
    path.write_bytes(build_pdf([(20, 700, "X1 112"), (20, 650, "X2 113")], [], [], width=300, height=800))
    assert scan.read_pdf(path, BLOCK["pdfJoinGapPt"], BLOCK["pdfLineTolerancePt"]) == {"name": "list.pdf", "pages": [["X1 112", "X2 113"]]}
