import numpy as np
import openpyxl
import pytest

import c3d_run
import data_book
import location_plan
import location_plan_check

CODE = r"(?P<base>[A-Z]{1,3}\d{1,2}(?:-0|-IM)?)(?:\s?\(|-)?(?P<suffix>BB|SA|QP)?\)?"
PATTERNS = {"number": r"\d{1,4}", "clientCode": CODE, "openSpace": "OS", "landUse": r"[A-Z][A-Za-z0-9 .&/()-]*"}


def chars(text, x, top, size=1.48, step=0.7):
    return [{"text": ch, "x0": x + k * step, "x1": x + k * step + 0.65, "top": top, "bottom": top + 1.0, "size": size} for k, ch in enumerate(text)]


# R179 text lines and their kind

def test_r179_characters_join_into_lines_and_split_at_a_gap():
    found = location_plan.text_lines(chars("DP4 (SA)", 10, 5) + chars("2172", 11, 6.56) + chars("VL4", 30, 5) + chars("9", 10, 5, size=7), [1.4, 1.6], 0.3, 0.05)
    assert [line["text"] for line in found] == ["DP4 (SA)", "VL4", "2172"]
    assert found[0]["x"] == pytest.approx((10 + 10 + 7 * 0.7 + 0.65) / 2)


def test_r179_kinds_in_the_order_of_the_patterns():
    kinds = [location_plan.classify(t, PATTERNS) for t in ("2172", "TH3-0 (BB)", "DP1-SA", "VL3-0", "TH1-IM", "C10", "OS", "WADI PARK", "?")]
    assert kinds == ["number", "clientCode", "clientCode", "clientCode", "clientCode", "clientCode", "openSpace", "landUse", "other"]


# R180 pairs

def test_r180_a_code_pairs_with_the_number_just_under_it_one_to_one():
    codes = [{"x": 10, "top": 5}, {"x": 20, "top": 5}, {"x": 40, "top": 5}]
    numbers = [{"x": 10.2, "top": 6.56}, {"x": 20, "top": 6.6}, {"x": 20.5, "top": 6.5}, {"x": 60, "top": 6.56}]
    pairs, lone_codes, lone_numbers = location_plan.pair_labels(codes, numbers, [1.3, 1.8], 1.5)
    assert sorted(pairs) == [(0, 0), (1, 1)] and lone_codes == [2] and lone_numbers == [2, 3]


# R181 the fit and the control point

def test_r181_robust_affine_fit_drops_the_outlier():
    page = [(x, y) for x in range(0, 50, 10) for y in range(0, 40, 10)]
    truth = np.array([[0.0, -1.76], [-1.76, 0.0], [1000.0, 2000.0]])
    site = [tuple(v) for v in location_plan.apply(truth, page)]
    site[3] = (site[3][0] + 30, site[3][1])
    matrix, residual, keep = location_plan.robust_fit(page, site, 3, 3, 0.001)
    assert 3 not in keep and len(keep) == len(page) - 1
    assert np.allclose(matrix, truth, atol=1e-6) and residual[3] == pytest.approx(30, abs=1e-6)
    assert location_plan.stats([1, 2, 3])["median"] == 2


def test_r181_control_point_takes_the_mark_nearest_its_stated_point():
    matrix = np.array([[1.0, 0.0], [0.0, 1.0], [0.0, 0.0]])
    word = {"x0": 100, "top": 100}
    assert location_plan.control_check(matrix, word, [(90, 95), (300, 300), (98, 101)], (98.1, 101.0), 20) == pytest.approx(0.1)
    assert location_plan.control_check(matrix, word, [(300, 300)], (98, 101), 20) is None


# R182 old code to new code

TABLE = [{"form": "Duplex", "old": "DP1", "new": "DP1"}, {"form": "Villa", "old": "DP1", "new": "VD1"}, {"form": "Duplex", "old": "DP2-0 (BB)", "new": "DP2"},
         {"form": None, "old": "DP2", "new": None}, {"form": None, "old": "TH3-0 (BB)", "new": "DT3"}, {"form": None, "old": "VL3-0", "new": "VL6"}]


def test_r182_new_code_by_exact_row_by_form_and_by_base():
    form_of = {"BB": "Duplex", "SA": "Villa", "QP": "Duplex"}
    found = [location_plan.new_code(code, TABLE, form_of, CODE) for code in ("TH3-0 (BB)", "VL3-0", "DP1-SA", "DP1 (QP)", "DP2 (BB)", "XX9", "WADI")]
    assert found == ["DT3", "VL6", "VD1", "DP1", "DP2", None, None]


def test_r182_read_table_by_header_names(tmp_path):
    path = tmp_path / "t.xlsx"
    workbook = openpyxl.Workbook()
    sheet = workbook.active
    sheet.title = "S"
    sheet.append(["title"])
    sheet.append(["Plot No", "Typology_Rev9", "x"])
    sheet.append([428, "VD4", 1])
    sheet.append([None, None, None])
    workbook.save(path)
    assert location_plan.read_table(path, "S", 2, {"plot": "Plot No", "typology": "Typology_Rev9"}) == [{"plot": "428", "typology": "VD4"}]


# R183 zones and the location plan in plots.csv

def test_r183_zone_outlines_from_lines_of_one_style():
    square = [((0, 0), (10, 0)), ((10, 0), (10, 10)), ((10, 10), (0, 10)), ((0, 10), (0, 0))]
    lines = [{"pts": list(seg), "stroking_color": (1.0, 0.0, 1.0), "linewidth": 4.0} for seg in square]
    lines.append({"pts": [(50, 50), (60, 60)], "stroking_color": (0.0, 0.0, 0.0), "linewidth": 4.0})
    outlines = location_plan.zone_outlines(lines, "(1.0, 0.0, 1.0)", 4.0)
    assert len(outlines) == 1 and outlines[0].area == pytest.approx(100)
    assert location_plan.zone_outlines(lines, "(0.0, 1.0, 0.0)", 4.0) == []


def test_r183_plots_csv_carries_the_location_plan_columns():
    columns = data_book.BASE_COLUMNS
    at = columns.index("locationPlanNumber")
    assert columns[at:at + 5] == ["locationPlanNumber", "locationPlanNumberMethod", "locationPlanCode", "locationPlanNewCode", "typologyRev9"]
    book = c3d_run.load_config(c3d_run.CONFIG)["dataBook"]
    assert book["wiring"]["chosen"]["locationPlan"] in ("pointInside", "nearest")


# R184 second method

def test_r184_plain_fit_and_solve():
    page = [(0, 0), (10, 0), (0, 10), (10, 10), (5, 3)]
    site = [(100 + 2 * x - y, 50 + x + 3 * y) for x, y in page]
    transform = location_plan_check.fit(page, site)
    assert location_plan_check.to_site(transform, 4, 7) == pytest.approx((100 + 8 - 7, 50 + 4 + 21))
    assert location_plan_check.solve([[2, 1], [1, 3]], [3, 5]) == pytest.approx([0.8, 1.4])


def test_r184_spelled_reads_the_code_over_the_number():
    raw = chars("DP4 (SA)", 10, 5) + chars("2172", 11, 6.56) + chars("2173", 30, 6.56)
    assert location_plan_check.spelled(raw, 12, 5.8, "DP4 (SA)", "2172", 4)
    assert not location_plan_check.spelled(raw, 12, 5.8, "DP4 (SA)", "2173", 4)


def test_r385_labels_csv_writes_the_page_and_site_points_of_a_label_by_the_rule():
    # data_book.py wires the labels to the faces by these site points, the page points are compared by the parity of step 3 (R351)
    assert location_plan.page_cells({"x": 2.0465, "y": 0.5005}) == {"pageX": 2.047, "pageY": 0.501}
    assert location_plan.site_cells("512345.6785", 2361234.1235) == {"east": 512345.679, "north": 2361234.124}
    assert round(512345.6785, 3) == 512345.678 and round(2.0465, 3) == 2.046


def made_pdf(path, page_extra):
    """A PDF of one page of 600 by 400 pt with a text and a stroked path, page_extra added to its page dictionary, the bytes built in code
    as LocationPlanTests.MadePdf builds them."""
    content = b"BT /F1 10 Tf 100 300 Td (AB12) Tj ET 200 200 m 210 205 l 220 200 l S"
    objects = [b"<< /Type /Catalog /Pages 2 0 R >>", b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
               b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 600 400]" + page_extra + b" /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
               b"<< /Length %d >>\nstream\n" % len(content) + content + b"\nendstream", b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"]
    out, offsets = bytearray(b"%PDF-1.4\n"), []
    for number, body in enumerate(objects, 1):
        offsets.append(len(out))
        out += b"%d 0 obj\n" % number + body + b"\nendobj\n"
    start = len(out)
    out += b"xref\n0 %d\n0000000000 65535 f \n" % (len(objects) + 1) + b"".join(b"%010d 00000 n \n" % offset for offset in offsets)
    out += b"trailer\n<< /Size %d /Root 1 0 R >>\nstartxref\n%d\n%%%%EOF\n" % (len(objects) + 1, start)
    path.write_bytes(bytes(out))
    return path


def test_r351_a_page_whose_crop_box_is_not_its_media_box_is_refused_as_the_c_sharp_refuses_it(tmp_path):
    # pdfplumber gives the points of the media box whatever the CropBox, PdfPig shifts them by the crop origin, so neither reads it
    plan = {"controlShapeMaxPt": 10}
    with pytest.raises(ValueError, match="CropBox.*MediaBox.*R351"):
        location_plan.read_page(made_pdf(tmp_path / "crop.pdf", b" /CropBox [50 50 550 350]"), plan)
    with pytest.raises(ValueError, match="CropBox"):
        location_plan.read_page(made_pdf(tmp_path / "turned.pdf", b" /CropBox [50 50 550 350] /Rotate 90"), plan)
    for name, extra in (("plain.pdf", b""), ("same.pdf", b" /CropBox [0 0 600 400]")):
        page = location_plan.read_page(made_pdf(tmp_path / name, extra), plan)
        assert "".join(char["text"] for char in page["chars"]) == "AB12" and page["chars"][0]["x0"] == pytest.approx(100.0)


# R387 the control point fit as a check of the schedule fit (Q081): the marks by their shape, the similarity start, the refine

TRUTH = np.array([[1.7, 0.3], [0.3, -1.7], [1000.0, 2000.0]])
MARK = {"widthPt": 3.96, "heightPt": 3.96, "tolerancePt": 0.01, "points": 6}


def curve(x, y, size, points):
    return {"x0": x - size / 2, "x1": x + size / 2, "top": y - size / 2, "bottom": y + size / 2, "pts": [(x, y)] * points}


def control_words(texts):
    """The words of E = <east> N = <north> for each (start x, top, east, north)."""
    found = []
    for x, top, east, north in texts:
        found += [{"text": t, "x0": x + k * 5.0, "top": top} for k, t in enumerate(("E", "=", "%.3f" % east, "N", "=", "%.3f" % north))]
    return found


def test_r387_the_marks_are_the_curves_of_the_mark_shape_and_the_fit_starts_from_their_similarity():
    # Three marks almost on one line, as on the sheet, their stated points off by up to 0.02 m
    marks = [(100.0, 100.0), (500.0, 100.6), (900.0, 101.0)]
    noise = [(0.02, -0.01), (-0.015, 0.02), (0.01, 0.005)]
    stated = [tuple(location_plan.apply(TRUTH, [mark])[0] + np.asarray(shift)) for mark, shift in zip(marks, noise)]
    words = control_words([(mark[0] + 20.0, mark[1] - 8.0, east, north) for mark, (east, north) in zip(marks, stated)])
    # Beside each mark a small shape of another size nearer the start of its text, never taken
    curves = [curve(x, y, 3.96, 6) for x, y in marks] + [curve(x + 18.0, y - 7.0, 2.0, 4) for x, y in marks] + [curve(x + 19.0, y - 7.5, 3.96, 5) for x, y in marks]
    texts = location_plan.control_texts(words)
    assert [stated for _, stated in texts] == [pytest.approx(point, abs=1e-3) for point in stated]
    found = location_plan.control_marks(curves, texts, MARK, 120)
    assert [mark for mark, _ in found] == marks
    start = location_plan.similarity_fit([mark for mark, _ in found], [point for _, point in found])
    # The similarity holds the truth within 0.05 m 500 pt off the line of the marks, where the exact affine of the three is far off
    far = [(500.0, 600.0)]
    assert np.hypot(*(location_plan.apply(start, far)[0] - location_plan.apply(TRUTH, far)[0])) < 0.05
    exact = np.linalg.solve(np.hstack([np.asarray(marks), np.ones((3, 1))]), np.asarray(stated))
    assert np.hypot(*(location_plan.apply(exact, far)[0] - location_plan.apply(TRUTH, far)[0])) > 1.0
    # A text with no mark of the shape in its search, or two, is refused, never a mark by nearness
    with pytest.raises(ValueError, match="R387"):
        location_plan.control_marks(curves[3:], texts, MARK, 120)
    with pytest.raises(ValueError, match="R387"):
        location_plan.control_marks(curves + [curve(105.0, 95.0, 3.96, 6)], texts, MARK, 120)


def square_faces():
    from shapely.geometry import box
    # Six faces of 10 m in a row on the site, labelled 101 to 106
    return {str(101 + k): box(1000.0 + 10 * k, 1900.0, 1010.0 + 10 * k, 1910.0) for k in range(6)}


def page_of(site_point):
    # The page point TRUTH maps onto a site point, site = [x, y] times its first two rows plus its third
    return tuple((np.asarray(site_point) - TRUTH[2]) @ np.linalg.inv(TRUTH[:2]))


def test_r387_the_refine_takes_only_faces_with_one_label():
    faces = square_faces()
    centres = {name: (face.centroid.x, face.centroid.y) for name, face in faces.items()}
    labels = [{"number": str(1001 + k), "x": p[0], "y": p[1]} for k, p in enumerate(page_of(centres[name]) for name in ("101", "102", "103", "104", "105"))]
    # A second label inside face 103 and a label outside every face: face 103 holds two labels and is left out of the refine
    labels.append({"number": "9", "x": page_of((1022.0, 1904.0))[0], "y": page_of((1022.0, 1904.0))[1]})
    labels.append({"number": "8", "x": page_of((900.0, 1800.0))[0], "y": page_of((900.0, 1800.0))[1]})
    rough = TRUTH + np.array([[0.0, 0.0], [0.0, 0.0], [0.4, -0.3]])
    refined, found = location_plan.control_refine(rough, labels, faces, 3, 3, 0.001)
    assert found["faces"] == ["101", "102", "104", "105"] and found["kept"] == 4
    assert np.allclose(location_plan.apply(refined, [page_of(centres["104"])])[0], centres["104"], atol=1e-6)
    with pytest.raises(ValueError, match="R387"):
        location_plan.control_refine(rough, labels[:2], faces, 3, 3, 0.001)


def test_r387_a_face_numbered_otherwise_is_listed_apart_with_its_reason_and_the_check_numbers_nothing():
    faces = square_faces()
    centres = {name: (face.centroid.x, face.centroid.y) for name, face in faces.items()}
    sites = [centres[name] for name in ("101", "102", "103", "104", "105", "106")] + [(1029.97, 1905.0)]
    labels = [{"number": str(1001 + k), "x": page_of(site)[0], "y": page_of(site)[1]} for k, site in enumerate(sites)]
    # The schedule fit lies 0.05 m east of the truth and the control fit is the truth, so label 1007, 0.03 m inside face 103 by the
    # truth, goes on face 104 by the schedule fit, 0.02 m from its edge
    shifted = TRUTH + np.array([[0.0, 0.0], [0.0, 0.0], [0.05, 0.0]])
    schedule = location_plan.apply(shifted, [(label["x"], label["y"]) for label in labels])
    rows = [{"number": label["number"], "face": face, "kind": "clientCode"} for label, face in zip(labels, location_plan.face_of_points(schedule, faces))]
    before = [dict(row) for row in rows]
    control = location_plan.apply(TRUTH, [(label["x"], label["y"]) for label in labels])
    listed = location_plan.numbered_otherwise(rows, location_plan.face_of_points(control, faces), schedule, control, faces)
    assert [(item["face"], item["schedule"], item["control"]) for item in listed] == [("103", ["1003"], ["1003", "1007"]), ("104", ["1004", "1007"], ["1004"])]
    why = listed[0]["why"][0]
    assert why["number"] == "1007" and why["scheduleFace"] == "104" and why["controlFace"] == "103"
    assert why["edgeM"] == pytest.approx(0.02, abs=1e-6) and why["gapM"] == pytest.approx(0.05, abs=1e-6) and "1007" in why["text"]
    assert rows == before
    # The faces the two fits number alike are not listed
    assert location_plan.numbered_otherwise(rows[:1], ["101"], schedule[:1], control[:1], faces) == []
    # The whole check on the made up page: the marks by their shape, the similarity, the refine, the list, and no row moved
    marks = [page_of((500.0, 1880.0)), page_of((1030.0, 1880.5)), page_of((1560.0, 1881.0))]
    stated = [tuple(location_plan.apply(TRUTH, [mark])[0]) for mark in marks]
    words = control_words([(mark[0] + 20.0, mark[1] - 8.0, east, north) for mark, (east, north) in zip(marks, stated)])
    plan = {"controlSearchPt": 120, "controlMark": MARK, "fitDropFactor": 3, "fitRounds": 3, "fitFloorM": 0.001}
    check = location_plan.control_fit_check(labels, rows, words, [curve(x, y, 3.96, 6) for x, y in marks], faces, plan, schedule)
    assert check["marks"] == 3 and check["facesNumberedOtherwise"] == 2 and check["unitFacesNumberedOtherwise"] == 2
    assert [item["face"] for item in check["list"]] == ["103", "104"] and rows == before
