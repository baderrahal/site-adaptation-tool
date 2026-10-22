import json

import pytest

import pdf_reader
from minimal_pdf import build_pdf

HEIGHT = 300
BLOCK_ONE = [20, 80, 140, 200, 260]
BLOCK_TWO = [340, 400, 460, 520, 580]
ROWS_ONE = [(101, 1, "DT3", "1000.123", "2000.456"), (101, 2, "DT3", "1001.5", "2001.5"), (102, 1, "VD2", "1010.25", "2010.75")]
ROWS_TWO = [(102, 2, "VD2", "1011.0", "2011.0"), (103, 1, "DT3", "1020.1234", "2020.5678")]


def header(anchors, y):
    items = [(anchors[0], y, "Point"), (anchors[0] + 20, y, "Plot"), (anchors[0] + 36, y, "No."),
             (anchors[1], y, "Point"), (anchors[1] + 20, y, "Code"),
             (anchors[2], y, "Point"), (anchors[2] + 20, y, "Unit"), (anchors[2] + 36, y, "Type"),
             (anchors[3], y, "N"), (anchors[4], y, "E")]
    return items


def data_rows(anchors, rows, first_y):
    items = []
    for index, row in enumerate(rows):
        for anchor, value in zip(anchors, row):
            items.append((anchor, first_y - 10 * index, str(value)))
    return items


def rules(anchors, rows, first_y):
    # a rule drawn cell by cell, five touching pieces per row line
    pieces = []
    for index in range(len(rows) + 1):
        y = first_y + 8 - 10 * index
        for column in range(4):
            pieces.append((anchors[column] - 2, y, anchors[column + 1] - 2, y))
    return pieces


@pytest.fixture
def table_pdf(tmp_path):
    items = header(BLOCK_ONE, 270) + header(BLOCK_TWO, 270)
    items += data_rows(BLOCK_ONE, ROWS_ONE, 250) + data_rows(BLOCK_TWO, ROWS_TWO, 250)
    items += [(10, 200, "Note"), (708, 150, "Key")]
    items += [(700 + 30 * (index % 2), 100 + 8 * index, "x", 1) for index in range(25)]
    lines = rules(BLOCK_ONE, ROWS_ONE, 250) + [(700 + (index % 5), 90 + index, 702 + (index % 5), 91 + index) for index in range(30)]
    lines.append((20, 290, 240, 290))
    stamp = ["BT /F1 6 Tf 0 1 -1 0 10 100 Tm (Ref) Tj ET"]
    path = tmp_path / "table.pdf"
    path.write_bytes(build_pdf(items, lines, [], width=800, height=HEIGHT, font_size=6, extra_ops=stamp))
    return path


def read(path, tmp_path):
    out = tmp_path / "out"
    pdf_reader.write_outputs(path, out)
    return json.loads((out / "pdf_rows.json").read_text(encoding="utf-8")), out


def test_r21_rows_are_rebuilt_as_five_values_and_blocks_are_stitched_in_order(table_pdf, tmp_path):
    data, _ = read(table_pdf, tmp_path)
    got = [(row["plot"], row["code"], row["type"], row["n"], row["e"]) for row in data["rows"]]
    assert got == [(101, 1, "DT3", 1000.123, 2000.456), (101, 2, "DT3", 1001.5, 2001.5), (102, 1, "VD2", 1010.25, 2010.75),
                   (102, 2, "VD2", 1011.0, 2011.0), (103, 1, "DT3", 1020.123, 2020.568)]
    assert [(row["block"], row["row"]) for row in data["rows"]] == [(1, 1), (1, 2), (1, 3), (2, 1), (2, 2)]
    assert [block["rows"] for block in data["blocks"]] == [3, 2]


def test_r21_plot_that_runs_from_the_foot_of_one_block_to_the_top_of_the_next_is_counted(table_pdf, tmp_path):
    data, _ = read(table_pdf, tmp_path)
    assert data["continued"] == 1
    assert [row["plot"] for row in data["rows"] if row["plot"] == 102] == [102, 102]


def test_r21_margin_stamp_and_key_plan_text_are_not_in_rows_or_labels(table_pdf, tmp_path):
    data, out = read(table_pdf, tmp_path)
    labels = {label["text"] for label in json.loads((out / "pdf_labels.json").read_text(encoding="utf-8"))["labels"]}
    assert "Ref" not in labels
    assert "Key" not in labels
    assert "x" not in labels
    assert "Note" in labels
    assert data["skipped"]["stamp"] == 1
    assert data["skipped"]["keyPlan"] == 26


def test_r21_short_strokes_outside_the_table_are_skipped_and_cell_rules_are_merged(table_pdf, tmp_path):
    data, out = read(table_pdf, tmp_path)
    shapes = json.loads((out / "pdf_shapes.json").read_text(encoding="utf-8"))["shapes"]
    horizontal = [shape for shape in shapes if shape["top"] == shape["bottom"]]
    assert len(horizontal) == 5
    assert sorted(round(shape["x1"] - shape["x0"]) for shape in horizontal) == [220, 240, 240, 240, 240]
    assert data["skipped"]["lines"] == 30


def test_r21_text_inside_a_block_that_is_not_a_row_is_rejected_and_listed(tmp_path):
    items = header(BLOCK_ONE, 270) + data_rows(BLOCK_ONE, ROWS_ONE, 250) + [(BLOCK_ONE[1], 200, "Sheet"), (BLOCK_ONE[0], 190, "5"), (BLOCK_ONE[1], 190, "6"), (BLOCK_ONE[2], 190, "7")]
    path = tmp_path / "bad.pdf"
    path.write_bytes(build_pdf(items, [], [], width=660, height=HEIGHT, font_size=6))
    data, _ = read(path, tmp_path)
    assert len(data["rows"]) == 3
    assert data["rejectedCount"] == 2
    assert data["rejected"][0]["why"].startswith("cells")


def test_r21_number_without_a_decimal_point_is_kept_as_found_and_flagged(tmp_path):
    rows = [(101, 1, "DT3", "1000123", "2000.456")]
    path = tmp_path / "odd.pdf"
    path.write_bytes(build_pdf(header(BLOCK_ONE, 270) + data_rows(BLOCK_ONE, rows, 250), [], [], width=660, height=HEIGHT, font_size=6))
    data, _ = read(path, tmp_path)
    assert data["rows"][0]["n"] == 1000123
    assert data["odd"] == [{"page": 1, "block": 1, "row": 1, "plot": 101, "why": "no decimal point in n"}]


def test_r2_row_coordinates_have_at_most_three_decimals(table_pdf, tmp_path):
    _, out = read(table_pdf, tmp_path)
    text = (out / "pdf_rows.json").read_text(encoding="utf-8")
    assert "1020.1234" not in text
    assert "1020.123" in text


def test_r21_page_without_a_table_gives_no_rows_and_keeps_every_shape(tmp_path):
    path = tmp_path / "plain.pdf"
    path.write_bytes(build_pdf([(20, 150, "PLOT")], [(10, 10, 12, 10)], [], font_size=6))
    data, out = read(path, tmp_path)
    assert data["rows"] == []
    assert len(json.loads((out / "pdf_shapes.json").read_text(encoding="utf-8"))["shapes"]) == 1
