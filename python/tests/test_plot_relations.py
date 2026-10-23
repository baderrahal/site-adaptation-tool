import csv
import json

import pytest
from openpyxl import Workbook
from shapely.geometry import Polygon, box

import plot_relations as pr

OFFSET = [1000.0, 2000.0, 0.0]


def shared(x, y, z=0.0):
    return [OFFSET[0] + x, OFFSET[1] + y, z]


def rect(x0, y0, x1, y1, z=0.0):
    return [shared(x0, y0, z), shared(x1, y0, z), shared(x1, y1, z), shared(x0, y1, z)]


def internal(points):
    return [[point[0] - OFFSET[0], point[1] - OFFSET[1], point[2] - OFFSET[2]] for point in points]


def floor_dict(element_id, type_name, plot, loop, low, high=None, level="Level 1", offset=0.0, unit="VD2"):
    return {
        "typeName": type_name, "elementId": element_id, "level": level, "heightOffset": offset, "levelElevation": 9.9,
        "topElevation": high if high is not None else low, "topElevationRange": {"min": low, "max": high if high is not None else low},
        "outline": [loop], "plotNumber": None if plot is None else str(plot), "unit": None if plot is None else unit,
    }


def ramp_dict(element_id, type_name, plot, corners):
    """corners are shared (x, y, z). The floor holds them in internal coordinates, as the Revit reader writes them."""
    levels = [corner[2] for corner in corners]
    floor = floor_dict(element_id, type_name, plot, corners, min(levels), max(levels))
    floor["corners"] = internal(corners)
    floor["cornersSource"] = "shapeEditor"
    return floor


def link_dict(element_id, plot, origin, loaded=True, box_corners=None):
    return {
        "file": "villa.rvt", "elementId": element_id, "plotNumber": str(plot), "prototype": "VD2", "case": "C1",
        "status": "Loaded" if loaded else "Unloaded", "loaded": loaded,
        "transform": {"origin": [origin[0] - OFFSET[0], origin[1] - OFFSET[1], origin[2]], "rotationDeg": 90.0, "mirrored": False},
        "transformShared": {"origin": origin, "rotationDeg": 90.0, "mirrored": False} if loaded else None,
        "boundingBoxCorners": box_corners,
    }


def box_corners(x0, y0, x1, y1, z0, z1):
    return rect(x0, y0, x1, y1, z0) + rect(x0, y0, x1, y1, z1)


def prototypes(floors, links):
    document = {"file": "site.rvt", "kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": OFFSET}, "floors": floors, "links": links}
    return {"documents": [document]}


def excel_row(plot, ffl, ssl, entry):
    return {"sheetRow": 40, "Plot No.": plot, "Unit": "VD2", "FFL": ffl, "SSL": ssl, "El @ Entry*": entry}


def normal_plot(number, dx, first_id):
    """The Generic floor, a ramp that shares its short end with it and a yard that wraps both. Levels are Generic 10.1 and yard 10.05."""
    generic = floor_dict(first_id, "Generic 150mm", number, rect(dx, 0, dx + 10, 6, 10.1), 10.1)
    ramp = ramp_dict(first_id + 1, "Ramps 0.5%", number, [shared(dx + 10, 0, 10.1), shared(dx + 16, 0, 10.07), shared(dx + 16, 3, 10.07), shared(dx + 10, 3, 10.1)])
    outline = [shared(dx, 6), shared(dx + 10, 6), shared(dx + 10, 3), shared(dx + 16, 3), shared(dx + 16, 9), shared(dx, 9)]
    yard = floor_dict(first_id + 2, "yard", None, [[point[0], point[1], 10.05] for point in outline], 10.05)
    link = link_dict(first_id + 3, number, shared(dx + 5, 3, 10.5), True, box_corners(dx + 1, 1, dx + 9, 5, 10.5, 17))
    return [generic, ramp, yard], link, excel_row(number, 10.5, 10.425, 10.1)


def plot_without_yard(number, dx, first_id):
    """The ramp sits 4 mm from the Generic floor and its two long edges rise 0.10 and 0.14 over 6 m. The link is not loaded."""
    generic = floor_dict(first_id, "Generic 150mm", number, rect(dx, 0, dx + 10, 6, 20.1), 20.1)
    edge = dx + 10.004
    ramp = ramp_dict(first_id + 1, "Ramps 2%", number, [shared(edge, 0, 20.10), shared(edge + 6, 0, 20.00), shared(edge + 6, 3, 19.96), shared(edge, 3, 20.10)])
    link = link_dict(first_id + 3, number, shared(dx + 5, 3, 20.5), False)
    return [generic, ramp], link, excel_row(number, 20.5, 20.4, 20.1)


def plot_with_overlap(number, dx, first_id):
    """The ramp runs 1 m into the Generic floor and the yard runs 1 m over its top edge."""
    generic = floor_dict(first_id, "Generic 150mm", number, rect(dx, 0, dx + 10, 6, 30.1), 30.1)
    ramp = ramp_dict(first_id + 1, "Ramps 0.5%", number, [shared(dx + 9, 0, 30.10), shared(dx + 15, 0, 30.07), shared(dx + 15, 3, 30.07), shared(dx + 9, 3, 30.10)])
    yard = floor_dict(first_id + 2, "yard", None, rect(dx, 5, dx + 15, 9, 30.05), 30.05)
    link = link_dict(first_id + 3, number, shared(dx + 8, 3, 30.5), True, box_corners(dx + 5, 1, dx + 15, 5, 30.5, 37))
    return [generic, ramp, yard], link, excel_row(number, 30.5, 30.4, 30.1)


def three_plots():
    floors, links, rows = [], [], []
    for builder, number, dx, first_id in ((normal_plot, 9001, 0, 100), (plot_without_yard, 9002, 30, 200), (plot_with_overlap, 9003, 60, 300)):
        part_floors, link, row = builder(number, dx, first_id)
        floors += part_floors
        links.append(link)
        rows.append(row)
    return prototypes(floors, links), rows


def by_plot(rows):
    return {row["plot"]: row for row in rows}


def test_r57_one_row_per_plot_with_the_link_the_excel_and_the_unit():
    data, excel = three_plots()
    rows, notes = pr.build_rows(data, excel)
    found = by_plot(rows)

    assert [row["plot"] for row in rows] == ["9001", "9002", "9003"]
    first = found["9001"]
    assert (first["unit"], first["link_id"], first["link_status"], first["case"]) == ("VD2", 103, "Loaded", "C1")
    assert (first["link_x"], first["link_y"], first["link_z"]) == (1005.0, 2003.0, 10.5)
    assert (first["link_rotation"], first["link_mirrored"]) == (90.0, False)
    assert (first["excel_ffl"], first["excel_ssl"], first["excel_el_entry"]) == (10.5, 10.425, 10.1)
    assert notes["floors"] == 8 and notes["yardFloors"] == 2 and notes["links"] == 3


def test_r57_the_generic_and_yard_tops_are_set_against_ffl_ssl_and_entry():
    data, excel = three_plots()
    first = by_plot(pr.build_rows(data, excel)[0])["9001"]

    assert first["generic_id"] == 100 and first["generic_top_source"] == "topFace"
    assert (first["generic_top_min"], first["generic_top_max"]) == (10.1, 10.1)
    assert (first["generic_min_vs_ffl"], first["generic_max_vs_ffl"]) == (-0.4, -0.4)
    assert (first["generic_min_vs_ssl"], first["generic_min_vs_entry"]) == (-0.325, 0.0)
    assert first["generic_top_is_entry"] is True and first["generic_top_is_ffl"] is False
    assert (first["yard_id"], first["yard_top_min"], first["yard_min_vs_entry"]) == (102, 10.05, -0.05)
    assert first["yard_top_is_entry"] is False


def test_r57_the_level_parameters_of_each_floor_are_written():
    data, excel = three_plots()
    data["documents"][0]["floors"][0].update({"level": "Level 2", "heightOffset": -0.15, "levelElevation": 12.5})
    first = by_plot(pr.build_rows(data, excel)[0])["9001"]

    assert (first["generic_level"], first["generic_offset"], first["generic_level_elevation"]) == ("Level 2", -0.15, 12.5)
    assert (first["yard_level"], first["ramp_level"], first["ramp_offset"]) == ("Level 1", "Level 1", 0.0)


def test_r57_an_old_file_without_the_new_fields_still_gives_a_row_from_the_sketch_and_the_top_elevation():
    data, excel = three_plots()
    for floor in data["documents"][0]["floors"]:
        floor.pop("outline")
        floor.pop("topElevationRange")
        floor["sketchBoundary"] = internal([[1000, 2000, 150], [1010, 2000, 150], [1010, 2006, 150], [1000, 2006, 150]])
    first = by_plot(pr.build_rows(data, excel)[0])["9001"]

    assert first["generic_top_source"] == "topElevation"
    assert first["generic_top_min"] == first["generic_top_max"] == 10.1
    assert "generic_outline_from_sketch" in first["flags"]


def test_r58_a_yard_that_touches_the_generic_or_the_ramp_is_assigned_by_geometry():
    data, excel = three_plots()
    found = by_plot(pr.build_rows(data, excel)[0])

    assert (found["9001"]["yard_status"], found["9001"]["yard_id"]) == ("assigned", 102)
    assert (found["9003"]["yard_status"], found["9003"]["yard_id"]) == ("assigned", 302)
    assert found["9003"]["yard_candidate_overlaps"] == "302:10/0"


def test_r58_a_plot_with_no_yard_near_it_is_marked_none():
    data, excel = three_plots()
    second = by_plot(pr.build_rows(data, excel)[0])["9002"]

    assert second["yard_status"] == "none"
    assert second["yard_id"] is None and second["yard_top_min"] is None
    assert second["generic_yard_edge"] == "no yard"
    assert "no_yard" in second["flags"]


def test_r58_two_yards_that_touch_the_plot_make_it_ambiguous_and_both_are_listed():
    data, excel = three_plots()
    data["documents"][0]["floors"].append(floor_dict(999, "yard", None, rect(-5, 0, 0, 6, 10.0), 10.0))
    first = by_plot(pr.build_rows(data, excel)[0])["9001"]

    assert first["yard_status"] == "ambiguous"
    assert first["yard_id"] is None
    assert first["yard_candidates"] == "102 999"
    assert first["yard_candidate_overlaps"] == "102:0/0 999:0/0"
    assert "yard_ambiguous" in first["flags"]


def test_r58_the_ramp_edge_on_the_generic_edge_and_the_edge_on_the_yard_are_found():
    data, excel = three_plots()
    first = by_plot(pr.build_rows(data, excel)[0])["9001"]

    assert [first["ramp_e" + str(index) + "_shared"] for index in range(4)] == ["none", "none", "yard", "generic"]
    assert (first["ramp_generic_roles"], first["ramp_yard_roles"]) == ("short", "long")
    assert first["generic_ramp_shared_len"] == pytest.approx(3.0, abs=0.02)
    assert first["generic_yard_shared_len"] == pytest.approx(13.0, abs=0.02)
    assert first["ramp_yard_shared_len"] == pytest.approx(6.0, abs=0.02)
    assert first["generic_yard_edge"] == "shared"


def test_r58_an_edge_4_mm_from_the_generic_edge_is_shared_and_one_8_mm_away_is_not():
    shape = box(0, 0, 10, 6)

    assert pr.shared_length([(10.004, 0), (10.004, 3)], shape) == pytest.approx(3.0, abs=0.01)
    assert pr.shared_length([(10.008, 0), (10.008, 3)], shape) == 0.0
    near = pr.share_state(pr.shared_length([(10.004, 0), (10.004, 3)], shape), 3.0)
    far = pr.share_state(pr.shared_length([(10.008, 0), (10.008, 3)], shape), 3.0)
    assert (near, far) == ("full", "none")


def test_r58_the_ramp_4_mm_from_the_generic_floor_counts_as_sharing_its_short_edge():
    data, excel = three_plots()
    second = by_plot(pr.build_rows(data, excel)[0])["9002"]

    assert second["ramp_e3_shared"] == "generic"
    assert second["ramp_generic_roles"] == "short"
    assert second["ramp_yard_roles"] == "no yard"


def test_r58_a_part_of_an_edge_on_the_other_outline_is_partly_shared():
    shape = box(0, 0, 10, 6)
    length = pr.shared_length([(8, 0), (14, 0)], shape)

    assert length == pytest.approx(2.0, abs=0.02)
    assert pr.share_state(length, 6.0) == "partial"
    assert pr.edge_label({"generic": "partial", "yard": "full"}) == "yard+part generic"
    assert pr.edge_label({"generic": "none"}) == "none"


def test_r59_the_ramp_depth_is_the_distance_between_the_long_edges():
    geometry = pr.ramp_geometry([(10, 0, 10.1), (16, 0, 10.07), (16, 3, 10.07), (10, 3, 10.1)])

    assert geometry["depth"] == pytest.approx(3.0)
    assert geometry["lengths"][0] == pytest.approx(6.0) and geometry["lengths"][1] == pytest.approx(3.0)
    assert geometry["roles"] == ["long", "short", "long", "short"]


def test_r59_the_ring_starts_so_that_edges_0_and_2_are_the_long_ones_whatever_order_the_corners_come_in():
    corners = [(5, 0, 1.0), (5, 6, 0.94), (2, 6, 0.94), (2, 0, 1.0)]
    geometry = pr.ramp_geometry(corners)

    assert geometry["depth"] == pytest.approx(3.0)
    assert geometry["lengths"][0] == pytest.approx(6.0)
    assert geometry["slope"] == pytest.approx(1.0)


def test_r59_the_slope_is_the_sum_of_the_rises_of_both_long_edges_over_the_sum_of_their_lengths():
    geometry = pr.ramp_geometry([(0, 0, 20.10), (6, 0, 20.00), (6, 3, 19.96), (0, 3, 20.10)])

    assert geometry["slope"] == pytest.approx((0.10 + 0.14) / 12 * 100)
    assert geometry["slope"] == pytest.approx(2.0)


def test_r59_the_slope_across_the_depth_is_worked_out_the_same_way_from_the_two_short_edges():
    geometry = pr.ramp_geometry([(0, 0, 20.10), (6, 0, 20.00), (6, 3, 19.96), (0, 3, 20.10)])

    assert geometry["slope_short"] == pytest.approx((0.04 + 0.0) / 6 * 100)
    assert pr.ramp_geometry([(0, 0, 5), (6, 0, 5), (6, 3, 5), (0, 3, 5)])["slope_short"] == 0


def test_r59_a_flat_ramp_has_slope_zero_and_the_depth_of_a_trapezoid_is_its_area_over_the_mean_long_edge():
    assert pr.ramp_geometry([(0, 0, 5), (6, 0, 5), (6, 3, 5), (0, 3, 5)])["slope"] == 0
    trapezoid = pr.ramp_geometry([(0, 0, 0), (8, 0, 0), (6, 3, 0), (2, 3, 0)])
    assert trapezoid["depth"] == pytest.approx(3.0)


def test_r59_a_ramp_that_does_not_have_four_corners_has_no_geometry():
    assert pr.ramp_geometry([(0, 0, 0), (1, 0, 0), (1, 1, 0)]) is None


def test_r59_the_slope_in_the_type_name_is_the_number_before_the_percent_sign():
    assert pr.type_slope("Ramps 0.5%") == 0.5
    assert pr.type_slope("Ramps 2%") == 2.0
    assert pr.type_slope("Ramps 1,5 %") == 1.5
    assert pr.type_slope("Flat Ramps") is None
    assert pr.type_slope(None) is None


def test_r59_the_row_holds_the_corner_levels_depth_and_both_slopes():
    data, excel = three_plots()
    found = by_plot(pr.build_rows(data, excel)[0])

    first = found["9001"]
    assert (first["ramp_id"], first["ramp_type"]) == (101, "Ramps 0.5%")
    assert [first["ramp_z" + str(index)] for index in (1, 2, 3, 4)] == [10.1, 10.07, 10.07, 10.1]
    assert (first["ramp_depth"], first["ramp_long_length"]) == (3.0, 6.0)
    assert (first["ramp_slope_corners"], first["ramp_slope_type"], first["ramp_slope_diff"]) == (0.5, 0.5, 0.0)
    assert (first["ramp_slope_short"], first["ramp_slope_short_diff"]) == (0.0, -0.5)
    second = found["9002"]
    assert (second["ramp_slope_corners"], second["ramp_slope_type"], second["ramp_slope_diff"]) == (2.0, 2.0, 0.0)


def test_r60_the_overlap_between_the_floors_is_measured_in_square_metres():
    data, excel = three_plots()
    third = by_plot(pr.build_rows(data, excel)[0])["9003"]

    assert third["overlap_generic_ramp"] == pytest.approx(3.0)
    assert third["overlap_generic_yard"] == pytest.approx(10.0)
    assert third["overlap_ramp_yard"] == 0.0


def test_r60_floors_that_touch_leave_no_gap_and_a_4_mm_slot_between_two_floors_is_measured():
    data, excel = three_plots()
    found = by_plot(pr.build_rows(data, excel)[0])

    assert found["9001"]["gap_generic_ramp"] == 0.0
    assert found["9001"]["gap_generic_yard"] == 0.0
    assert found["9002"]["gap_generic_ramp"] == pytest.approx(0.012, abs=0.003)


def test_r60_a_gap_wider_than_twice_the_closing_distance_is_not_counted():
    left, right = box(0, 0, 10, 6), box(10.2, 0, 16, 6)

    assert pr.gap_area(left, right) == 0.0
    assert pr.gap_area(left, box(10.05, 0, 16, 6)) == pytest.approx(0.3, abs=0.02)


def test_r60_the_link_box_is_inside_partly_inside_or_outside_the_generic_floor():
    data, excel = three_plots()
    found = by_plot(pr.build_rows(data, excel)[0])

    assert found["9001"]["link_box_vs_generic"] == "inside"
    assert found["9001"]["link_box_inside_share"] == 1.0
    assert found["9003"]["link_box_vs_generic"] == "partly inside"
    assert found["9003"]["link_box_inside_share"] == pytest.approx(0.5)
    shape = box(0, 0, 10, 6)
    outside = {"loaded": True, "boundingBoxCorners": box_corners(50, 0, 60, 6, 0, 3)}
    assert pr.link_box_state(outside, shape, None)[0] == "outside"


def test_r60_a_link_that_is_not_loaded_or_has_no_box_is_skipped():
    data, excel = three_plots()
    second = by_plot(pr.build_rows(data, excel)[0])["9002"]
    shape = box(0, 0, 10, 6)

    assert second["link_box_vs_generic"] == "not loaded"
    assert second["link_box_inside_share"] is None
    assert second["link_status"] == "Unloaded"
    assert pr.link_box_state({"loaded": True}, shape, None)[0] == "no box"
    assert pr.link_box_state(None, shape, None)[0] == "no link"


def test_r60_the_box_holds_the_link_origin_for_the_plausibility_count():
    data, excel = three_plots()
    found = by_plot(pr.build_rows(data, excel)[0])

    assert found["9001"]["link_box_holds_origin"] is True
    data["documents"][0]["links"][0]["boundingBoxCorners"] = box_corners(500, 0, 510, 6, 0, 3)
    assert by_plot(pr.build_rows(data, excel)[0])["9001"]["link_box_holds_origin"] is False


def test_r57_a_loop_inside_a_loop_is_a_hole_in_the_plan_shape():
    shape = pr.even_odd([[(0, 0), (10, 0), (10, 10), (0, 10)], [(3, 3), (6, 3), (6, 6), (3, 6)]])

    assert shape.area == pytest.approx(91.0)
    assert pr.even_odd([]) is None
    assert pr.even_odd([[(0, 0), (1, 1)]]) is None


def test_r57_a_floor_with_no_outline_uses_the_sketch_plus_the_survey_offset():
    shape, source = pr.floor_shape({"sketchBoundary": [[0, 0, 150], [4, 0, 150], [4, 3, 150], [0, 3, 150]]}, OFFSET)

    assert source == "sketchBoundary"
    assert shape.bounds == (1000.0, 2000.0, 1004.0, 2003.0)
    assert pr.floor_shape({}, OFFSET) == (None, None)


def test_r57_the_excel_columns_are_found_whatever_the_stars_and_spaces_in_the_header():
    row = {"Plot No.": 12, "El @ Entry*": 10.1, "FFL ": 10.5}

    assert pr.excel_cell(row, "El @ Entry") == 10.1
    assert pr.excel_cell(row, "FFL") == 10.5
    assert pr.excel_cell(row, "SSL") is None


def make_workbook(tmp_path, rows):
    book = Workbook()
    sheet = book.active
    sheet.title = pr.EXCEL_SHEET
    sheet.append(["Phase 1 Zone 21 schedule"])
    sheet.append(["Plot & Unit", "Plot No.", "Unit", "FFL", "SSL", "El @ Entry*", "Height**"])
    for row in rows:
        sheet.append(["x", row["Plot No."], row["Unit"], row["FFL"], row["SSL"], row["El @ Entry*"], 0.05])
    path = tmp_path / "schedule.xlsx"
    book.save(path)
    return path


def test_r57_the_schedule_is_read_from_the_workbook_on_the_default_sheet(tmp_path):
    data, excel = three_plots()
    rows = pr.load_excel(make_workbook(tmp_path, excel))

    assert [pr.excel_cell(row, "Plot No.") for row in rows] == [9001, 9002, 9003]
    assert pr.excel_cell(rows[0], "El @ Entry") == 10.1
    coords = tmp_path / "coords.json"
    coords.write_text(json.dumps({"rows": excel}), encoding="utf-8")
    assert pr.load_excel(coords) == excel


def test_r57_main_writes_the_csv_and_the_summary_next_to_the_input_and_nowhere_else(tmp_path):
    data, excel = three_plots()
    source = tmp_path / "prototypes.json"
    source.write_text(json.dumps(data), encoding="utf-8")
    workbook = make_workbook(tmp_path, excel)

    assert pr.main([str(source), "--excel", str(workbook)]) == 0

    with open(tmp_path / pr.CSV_NAME, encoding="utf-8") as handle:
        table = list(csv.DictReader(handle))
    assert list(table[0].keys()) == pr.COLUMNS
    assert [row["plot"] for row in table] == ["9001", "9002", "9003"]
    assert table[0]["generic_top_is_entry"] == "yes" and table[0]["link_mirrored"] == "no" and table[1]["yard_id"] == ""
    assert (tmp_path / pr.SUMMARY_NAME).read_text(encoding="utf-8").startswith("# Plot relations")
    assert not (pr.ROOT / pr.CSV_NAME).exists() and not (pr.ROOT / pr.SUMMARY_NAME).exists()


def test_r57_the_report_is_never_written_inside_the_repo(tmp_path):
    inside = pr.ROOT / "does_not_exist" / "prototypes.json"

    with pytest.raises(ValueError):
        pr.main([str(inside)])
    with pytest.raises(ValueError):
        pr.refuse_inside_repo(pr.ROOT)
    pr.refuse_inside_repo(tmp_path)


def test_r57_a_run_with_no_schedule_still_gives_every_row():
    data, _ = three_plots()
    rows, notes = pr.build_rows(data, [])

    assert len(rows) == 3
    assert rows[0]["excel_ffl"] is None and rows[0]["generic_min_vs_ffl"] is None
    assert "no_excel_row" in rows[0]["flags"]
    assert rows[0]["unit"] == "VD2"


def test_r61_the_summary_counts_the_ramp_edges_and_the_generic_levels():
    data, excel = three_plots()
    rows, notes = pr.build_rows(data, excel)
    summary = pr.summarize(rows, notes)
    text = pr.render_summary(summary, {"prototypes": "prototypes.json", "excel": "schedule.xlsx"})

    assert summary["plots"] == 3
    assert summary["genericTopEquals"]["entry"] == (3, 3)
    assert summary["genericTopEquals"]["ffl"] == (0, 3)
    assert summary["perFact"]["ramp shares an edge with Generic"] == {"yes": 2, "no": 1}
    assert summary["perFact"]["yard"] == {"assigned": 2, "none": 1}
    assert summary["perFact"]["link box against Generic"] == {"inside": 1, "not loaded": 1, "partly inside": 1}
    assert summary["slopeMatches"] == (3, 3)
    assert summary["slopeShortMatches"] == (0, 3)
    assert summary["yardCandidates"] == {1: 2, 0: 1}
    assert summary["overlaps"]["generic_ramp"] == (1, pytest.approx(3.0))
    assert "- ramp shares an edge with Generic: no 1, yes 2" in text
    assert "per plot: 0 x1, 1 x2" in text
    assert "- Generic top equals El @ Entry within 0.005 m: 3 of 3 plots with both values" in text


def test_r61_plots_that_break_the_most_common_pattern_are_listed_with_what_differs():
    floors, links, rows = [], [], []
    for index in range(5):
        part_floors, link, row = normal_plot(7000 + index, index * 40, 1000 + index * 10)
        floors += part_floors
        links.append(link)
        rows.append(row)
    part_floors, link, row = plot_without_yard(7100, 300, 2000)
    floors += part_floors
    links.append(link)
    rows.append(row)
    built, notes = pr.build_rows(prototypes(floors, links), rows)
    summary = pr.summarize(built, notes)

    assert summary["commonCount"] == 5
    assert [plot for plot, _ in summary["breaking"]] == ["7100"]
    differing = summary["breaking"][0][1]
    assert "yard: none" in differing
    assert "link box against Generic: not loaded" in differing
    assert "generic top equals El @ Entry: yes" not in differing
    text = pr.render_summary(summary, {"prototypes": "p.json", "excel": None})
    assert "Most plots (5 of 6) show:" in text
    assert "- plot 7100: " in text and "with no schedule" in text


def test_r61_an_empty_file_of_plots_gives_an_empty_summary_and_no_error():
    data = prototypes([floor_dict(1, "Generic 150mm", None, rect(0, 0, 1, 1), 1.0)], [])
    rows, notes = pr.build_rows(data, [])
    text = pr.render_summary(pr.summarize(rows, notes), {"prototypes": "p.json", "excel": None})

    assert rows == []
    assert "Plots that break the common pattern" in text and "None." in text
