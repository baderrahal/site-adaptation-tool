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
