import json

import pytest

import pdf_reader
from minimal_pdf import build_pdf


@pytest.fixture
def dummy_pdf(tmp_path):
    path = tmp_path / "dummy.pdf"
    path.write_bytes(build_pdf(
        text_items=[(20, 150, "PLOT"), (20, 120, "A12")],
        lines=[(10, 10, 110.12345, 10)],
        rects=[(30, 40, 50.5, 20.25)],
    ))
    return path


def test_r8_labels_have_text_and_page_position(dummy_pdf, tmp_path):
    out = tmp_path / "out"
    pdf_reader.write_outputs(dummy_pdf, out)
    data = json.loads((out / "pdf_labels.json").read_text(encoding="utf-8"))
    texts = {label["text"]: label for label in data["labels"]}
    assert set(texts) == {"PLOT", "A12"}
    assert texts["PLOT"]["page"] == 1
    assert texts["PLOT"]["x0"] == pytest.approx(20, abs=0.5)
    assert data["pages"][0]["width"] == 200


def test_r8_shapes_list_lines_and_rects(dummy_pdf, tmp_path):
    out = tmp_path / "out"
    pdf_reader.write_outputs(dummy_pdf, out)
    shapes = json.loads((out / "pdf_shapes.json").read_text(encoding="utf-8"))["shapes"]
    kinds = sorted(shape["kind"] for shape in shapes)
    assert "line" in kinds
    assert "rect" in kinds
    line = next(shape for shape in shapes if shape["kind"] == "line")
    assert line["x0"] == 10
    assert line["x1"] == 110.123
    assert line["top"] == 190


def test_r2_coordinates_have_at_most_three_decimals(dummy_pdf, tmp_path):
    out = tmp_path / "out"
    pdf_reader.write_outputs(dummy_pdf, out)
    for name in ("pdf_labels.json", "pdf_shapes.json"):
        text = (out / name).read_text(encoding="utf-8")
        for token in text.replace(",", " ").replace("\n", " ").split(" "):
            if "." in token and token.replace(".", "", 1).replace("-", "", 1).isdigit():
                assert len(token.split(".")[1]) <= 3


def test_r1_pdf_is_not_modified(dummy_pdf, tmp_path):
    before = dummy_pdf.read_bytes()
    pdf_reader.write_outputs(dummy_pdf, tmp_path / "out")
    assert dummy_pdf.read_bytes() == before


def test_r7_pymupdf_is_not_used():
    import pathlib
    root = pathlib.Path(__file__).resolve().parents[2]
    sources = [p for p in (root / "python").glob("*.py")] + [root / "requirements.txt"]
    banned = ("fitz", "pymupdf")
    for source in sources:
        assert not any(word in source.read_text(encoding="utf-8").lower() for word in banned), source.name


def test_missing_pdf_returns_error_code(tmp_path, capsys):
    assert pdf_reader.main([str(tmp_path / "none.pdf"), str(tmp_path / "out")]) == 2
