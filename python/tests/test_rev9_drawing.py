import csv

import numpy as np
from shapely.geometry import Point, Polygon, box

import rev9_drawing as rd

BORDER = box(-1000, -1000, 1000, 1000)
PATTERN = r"(?P<base>[A-Z]{1,3}\d{1,2}(?:-0|-IM)?)(?:\s?\(|-)?(?P<suffix>BB|SA|QP)?\)?"
IDENTITY = np.array([[1.0, 0.0], [0.0, 1.0], [0.0, 0.0]])


def square(x, y, size=10):
    return box(x, y, x + size, y + size)


def write(path, columns, rows):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, columns)
        writer.writeheader()
        writer.writerows(rows)


# R189 the Rev 9 drawing read
def test_r189_classes_count_the_civil_3d_objects_apart():
    layers = {"classes": [{"dxfName": "AECC_PARCEL", "instanceCount": 5, "isAnEntity": True}, {"dxfName": "AECC_PARCEL_STYLE", "instanceCount": 2, "isAnEntity": False},
                          {"dxfName": "WIPEOUT", "instanceCount": 1, "isAnEntity": True}, {"dxfName": "AECC_TIN_SURFACE", "instanceCount": 0, "isAnEntity": True}]}
    found = rd.classes(layers)
    assert found["civilClasses"] == 3 and found["civilObjects"] == 7
    assert found["entityClassesWithInstances"] == {"AECC_PARCEL": 5, "WIPEOUT": 1}


def test_r189_survey_counts_numbers_unit_codes_cluster_codes_and_words(tmp_path):
    write(tmp_path / "texts.csv", ["layer", "text", "x", "y"], [
        {"layer": "X$0$MP_BLDG_ANNO_TYPENAME", "text": "VL3-0", "x": 1, "y": 1}, {"layer": "X$0$MP_BLDG_ANNO_TYPENAME", "text": "T02", "x": 2, "y": 2},
        {"layer": "Cabin$0$MP_BLDG_ANNO_TYPENAME", "text": "VL3-0", "x": 1, "y": 1}, {"layer": "STAIRS", "text": "12", "x": 3, "y": 3},
        {"layer": "NOTE", "text": "no word here", "x": 4, "y": 4}])
    write(tmp_path / "layers.csv", ["layer", "entitiesInside"], [{"layer": "MP_boundary_Phasing-01", "entitiesInside": 1}, {"layer": "A-RAMP LINE", "entitiesInside": 0}])
    settings = {"unitCodeLayers": ["X$0$MP_BLDG_ANNO_TYPENAME"], "unitCodeCopyLayers": ["Cabin$0$*"], "clusterCodePattern": "[DQT][0-9]{2}",
                "setbackLayers": ["*SETBACK*"], "boundaryLayers": ["*Phasing*"]}
    found, codes = rd.survey(tmp_path, settings, ["IME", "NBH"], PATTERN)
    assert found["unitCodes"] == {"VL3-0": 1} and found["clusterCodes"] == {"T02": 1} and found["unitCodeCopies"] == 1
    assert found["numberTexts"] == 1 and found["boundaryLayers"] == ["MP_boundary_Phasing-01"] and found["setbackLayers"] == []
    assert found["words"]["IME"] == {"texts": 0, "layers": 0} and len(codes) == 1


def test_r189_plot_lines_close_faces_kept_inside_the_border():
    pieces = [{"kind": "polyline", "layer": "B$0$MP_Plot", "pts": [[0, 0, 0], [10, 0, 0], [10, 10, 0], [0, 10, 0]], "closed": True},
              {"kind": "line", "layer": "B$0$MP_Plot", "pts": [[5, 0, 0], [5, 10, 0]]},
              {"kind": "polyline", "layer": "B$0$MP_Plot", "pts": [[2000, 0, 0], [2010, 0, 0], [2010, 10, 0]], "closed": True},
              {"kind": "line", "layer": "OTHER", "pts": [[0, 5, 0], [10, 5, 0]]}]
    faces, lines = rd.plot_faces(pieces, ["*MP_Plot"], BORDER, 20)
    assert lines == 3 and sorted(round(f.area) for f in faces) == [50, 50]


# R190 the PDF labels and the wiring
def test_r190_site_points_take_faces_with_one_location_plan_number():
    plots = [{"locationPlanNumber": "7", "baseplanLabel": "1"}, {"locationPlanNumber": "8|9", "baseplanLabel": "2"},
             {"locationPlanNumber": "10", "baseplanLabel": "3"}, {"locationPlanNumber": "10", "baseplanLabel": "4"}]
    faces = {str(k): square(20 * k, 0) for k in range(1, 5)}
    found = rd.site_points(plots, faces)
    assert list(found) == ["7"] and found["7"].x == 25


def test_r190_page_fit_on_numbers_gives_the_site():
    labels = [{"number": str(k), "x": k * 1.0, "y": (k % 3) * 1.0} for k in range(1, 7)]
    known = {str(k): Point(2 * k + 100, -2 * (k % 3) + 50) for k in range(1, 7)}
    settings = {"fitDropFactor": 3, "fitRounds": 2, "fitFloorM": 0.001}
    matrix, fit = rd.fit_page(labels, known, settings)
    assert fit["kept"] == 6 and fit["residualKeptM"]["max"] < 1e-6 and fit["metresPerPoint"] == 2.0


def test_r190_base_of_drops_the_suffix():
    assert rd.base_of("DP2-0 (BB)", PATTERN) == "DP2-0" and rd.base_of("DP2-0", PATTERN) == "DP2-0" and rd.base_of("VL4", PATTERN) == "VL4"


# R191 the faces against the baseplan
def test_r191_pdf_strokes_of_the_plot_style_close_the_faces():
    objects = {"line": [{"pts": [(0, 0), (10, 0)], "stroking_color": "(0.0, 0.0, 0.0)", "linewidth": 7.0},
                        {"pts": [(10, 0), (10, 10)], "stroking_color": "(0.0, 0.0, 0.0)", "linewidth": 7.0},
                        {"pts": [(10, 10), (0, 10)], "stroking_color": "(0.0, 0.0, 0.0)", "linewidth": 7.0},
                        {"pts": [(0, 10), (0, 0)], "stroking_color": "(0.0, 0.0, 0.0)", "linewidth": 7.0},
                        {"pts": [(5, 0), (5, 10)], "stroking_color": "(0.5, 0.5, 0.5)", "linewidth": 7.0}],
               "curve": [{"pts": [(0, 0), (5, 5), (0, 10)], "stroking_color": "(0.0, 0.0, 0.0)", "linewidth": 7.0, "fill": True}]}
    faces, strokes = rd.pdf_faces(objects, IDENTITY, {"colour": "(0.0, 0.0, 0.0)", "width": 7.0}, BORDER, 20)
    assert strokes == 4 and [round(f.area) for f in faces] == [100]


def test_r191_match_by_overlap_over_union_same_shape_or_no_face():
    base = {"1": square(0, 0), "2": square(20, 0), "3": square(40, 0)}
    other = [square(0.01, 0), box(20, 0, 27, 10)]
    found = rd.face_match(base, other, 0.98, 0.5)
    assert found["1"]["class"] == "same" and found["2"]["class"] == "shapeDiffers" and found["3"]["class"] == "noFace"
    assert found["2"]["areaDifferenceM2"] == -30.0


def test_r191_refine_takes_out_a_fixed_offset_of_the_line_work():
    base = {str(k): square(30 * k, 15 * (k % 2)) for k in range(6)}
    other = [square(30 * k - 0.26, 15 * (k % 2) + 0.08) for k in range(6)]
    first = rd.face_match(base, other, 0.98, 0.5)
    assert all(m["class"] == "shapeDiffers" or m["iou"] < 0.98 for m in first.values())
    correction, refit = rd.refine(other, base, first, 0.9, {"fitDropFactor": 3, "fitRounds": 2, "fitFloorM": 0.001})
    assert refit["medianMoveM"] == [0.26, -0.08]
    again = rd.face_match(base, rd.moved(other, correction), 0.98, 0.5)
    assert all(m["class"] == "same" for m in again.values())
