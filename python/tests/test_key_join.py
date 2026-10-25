"""Round ADDIN Phase 2b: the join of the answer keys to the baseplan, the place check, the area rows and past_all.csv by source (R326).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import csv
import json
import math

import pytest
from shapely.geometry import box

import im_codes
import key_join
import past_all
import plan_from_past as pf
import project_profile

FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A"]}, "plotParameter": "Plot"}
FILE = "KEO-ARC-DP4-ZZ.rvt"
# Faces 11 to 13 carry one plan label each, 14 two labels, 15 none. 16 lies over 13, so a point there is on two faces
FACES = {"11": box(0, 0, 10, 25), "12": box(10, 0, 20, 25), "13": box(20, 0, 30, 25), "14": box(30, 0, 40, 25), "15": box(40, 0, 50, 25),
         "16": box(25.5, 10, 34, 15)}
NUMBERS = {"11": "501", "12": "502", "13": "503", "16": "509"}
MANY = {"14": 2}
# The area key of plots.csv by baseplan label and the location plan labels by number, both made up
AREA = {"11": {"nbhId": "01A", "zone": "Zone 4"}, "12": {"nbhId": "01A", "zone": "Zone 4"}, "13": {"nbhId": "022", "zone": "Zone 1"}}
PLAN = {"501": {"nbh": "01A", "zone": "4"}, "502": {"nbh": "01A", "zone": "4"}, "503": {"nbh": "022", "zone": "1"}}


def key_link(plot, x0, element_id=None):
    return {"file": FILE, "elementId": element_id or 7000 + x0, "parameters": {pf.PLOT_PARAMETER: plot, pf.UNIT_PARAMETER: "DP4"},
            "transform": {"origin": [x0, 0.0, 7.0], "rotationDeg": 0.0, "mirrored": False, "axisX": [1, 0, 0], "axisY": [0, 1, 0], "axisZ": [0, 0, 1]}}


def floor(value, x0, type_name="Slab G", element_id=None, width=10, y0=0, y1=25):
    return {"typeName": type_name, "elementId": element_id or 8000 + x0 + (1 if type_name != "Slab G" else 0), "parameters": {"Plot": value},
            "outline": [[[x0, y0, 7], [x0 + width, y0, 7], [x0 + width, y1, 7], [x0, y1, 7]]]}


def ramp(value, x0, element_id=None):
    return floor(value, x0 + 2, "Ramp A", element_id, width=4, y0=0, y1=5)


def document(links, floors):
    return {"kind": "project", "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]}, "links": links, "floors": floors}


def places():
    return key_join.places(FACES, NUMBERS, MANY, AREA, PLAN)


def test_r326_a_plot_joins_the_face_its_generic_floor_stands_on_with_the_number_zone_and_nbh_of_both_sources():
    doc = document([key_link("501", 0), key_link("502", 10)], [floor("DP4-501", 0), floor("DP4-502", 10)])
    found = key_join.join_plots(doc, FLOORS, places())
    row = found["plots"]["501"]
    assert row == {"status": "joined", "face": "11", "number": "501", "sameNumber": True, "nbh": "01A", "zone": "4", "areaNbh": "01A", "areaZone": "Zone 4"}
    assert found["counts"] == {"keyPlots": 2, "joined": 2, "sameNumber": 2, "otherNumber": 0, "areaAgrees": 2}


def test_r326_a_plot_whose_location_plan_area_is_not_its_area_key_is_listed():
    plan = dict(PLAN, **{"501": {"nbh": "022", "zone": "1"}})
    where = key_join.places(FACES, NUMBERS, MANY, AREA, plan)
    found = key_join.join_plots(document([key_link("501", 0), key_link("502", 10)], [floor("DP4-501", 0), floor("DP4-502", 10)]), FLOORS, where)
    assert found["counts"]["areaAgrees"] == 1 and found["areaDiffers"] == [{"plot": "501", "nbh": "022", "zone": "1", "areaNbh": "01A", "areaZone": "Zone 4"}]


def test_r326_a_plot_on_no_face_on_two_faces_on_a_face_of_no_or_two_labels_or_with_no_or_two_generic_floors_is_listed_by_why():
    links = [key_link(str(plot), 0, element_id=plot) for plot in (601, 602, 603, 604, 605, 606)]
    floors = [floor("DP4-601", 60), floor("DP4-602", 26, width=6, y0=11, y1=14),  # 602 lies where faces 13 and 16 meet
              floor("DP4-603", 40), floor("DP4-604", 30), floor("DP4-606", 0), floor("TH3-606", 10)]
    found = key_join.join_plots(document(links, floors), FLOORS, places())
    why = {plot: row["status"] for plot, row in found["plots"].items()}
    assert why == {"601": "noFace", "602": "twoFaces", "603": "noNumber", "604": "manyNumbers", "605": "noGenericFloor", "606": "twoGenericFloors"}
    assert found["counts"]["joined"] == 0 and found["byStatus"]["twoFaces"] == ["602"]


def test_r326_a_number_the_face_gives_other_than_the_key_plot_is_joined_and_counted_as_another_number():
    found = key_join.join_plots(document([key_link("777", 0)], [floor("DP4-777", 0)]), FLOORS, places())
    assert found["plots"]["777"]["number"] == "501" and found["plots"]["777"]["sameNumber"] is False
    assert found["counts"]["otherNumber"] == 1 and found["otherNumber"] == [{"plot": "777", "number": "501"}]


def test_r326_the_place_check_finds_two_ramps_whose_plot_values_are_swapped_and_a_link_standing_on_another_plot():
    # The ramps of 501 and 502 carry each other's values, the per plot rule of R323 counts both as held
    links = [key_link("501", 0), key_link("502", 10), key_link("503", 20), key_link("503", 10, element_id=9999)]
    floors = [floor("DP4-501", 0), floor("DP4-502", 10), floor("DP4-503", 20), ramp("DP4-502", 0), ramp("DP4-501", 10), ramp("DP4-503", 20)]
    found = key_join.place_check(document(links, floors), FLOORS, places())
    assert found["byKind"]["generic"] == {"match": 3}
    assert found["byKind"]["ramp"] == {"match": 1, "otherNumber": 2}
    # The second link of 503 has its house on face 12, the frame of its file is learned on the three links that stand right
    assert found["byKind"]["link"] == {"match": 3, "otherNumber": 1}
    wrong = {(row["kind"], row["elementId"]): (row["plot"], row["found"]) for row in found["mismatches"]}
    assert wrong[("ramp", 8003)] == ("502", "501") and wrong[("ramp", 8013)] == ("501", "502") and wrong[("link", 9999)] == ("503", "502")


def test_r326_the_area_rows_count_the_joined_plots_of_each_area_key_with_the_file_and_where_it_was_measured():
    doc = document([key_link("501", 0), key_link("502", 10), key_link("503", 20)], [floor("DP4-501", 0), floor("DP4-502", 10), floor("DP4-503", 20)])
    row = key_join.area_row("IMX", "Revit All Zones/X-IMX.rvt", key_join.join_plots(doc, FLOORS, places()))
    assert [(area["nbh"], area["zone"], area["plots"]) for area in row["areas"]] == [("01A", "Zone 4", 2), ("022", "Zone 1", 1)]
    assert all(area["kind"] == "model" and area["file"] == "Revit All Zones/X-IMX.rvt" and "of the 3 plots" in area["measured"] for area in row["areas"])
    assert im_codes.check_job("parsons", [row]) == [row]


def plots_csv(tmp_path):
    path = tmp_path / "plots.csv"
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=["baseplanLabel", "nbhId", "zone", "officialNumber", "pastEntry", "pastSource"])
        writer.writeheader()
        writer.writerow({"baseplanLabel": "11", "nbhId": "01A", "zone": "Zone 4", "officialNumber": "501", "pastEntry": "6.9", "pastSource": "schedule IMX"})
        writer.writerow({"baseplanLabel": "12", "nbhId": "01A", "zone": "Zone 4", "officialNumber": "", "pastEntry": "", "pastSource": ""})
    return path


def test_r326_the_area_key_and_the_schedule_entry_are_read_from_plots_csv_by_baseplan_label_and_official_number(tmp_path):
    area, schedule = key_join.read_plots_csv(plots_csv(tmp_path))
    assert area == {"11": {"nbhId": "01A", "zone": "Zone 4"}, "12": {"nbhId": "01A", "zone": "Zone 4"}}
    assert schedule == {"501": 6.9}


ROW = {column: "" for column in past_all.COLUMNS if column != "source"}


def old_table(tmp_path):
    path = tmp_path / "past_all_r17.csv"
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=[column for column in past_all.COLUMNS if column != "source"])
        writer.writeheader()
        writer.writerow(dict(ROW, key="u11", plot="11", ffl="1.234", parameters='{"a": "b"}'))
        writer.writerow(dict(ROW, key="imc", plot="2441", ffl="9.9"))
    return path


GROUPS = {"native": ["u11"], "parsons": ["imc", "imx"]}


def test_r326_kept_rows_of_a_group_stay_cell_for_cell_and_every_row_takes_the_job_of_its_key_as_source(tmp_path):
    kept = past_all.kept_rows(old_table(tmp_path), "native", GROUPS)
    assert kept == [dict(ROW, key="u11", plot="11", ffl="1.234", parameters='{"a": "b"}', source="native")]
    assert past_all.source_of("imx", GROUPS) == "parsons"
    with pytest.raises(project_profile.ProfileError, match="u99"):
        past_all.source_of("u99", GROUPS)
    with pytest.raises(project_profile.ProfileError, match="nogroup"):
        past_all.kept_rows(old_table(tmp_path), "nogroup", GROUPS)


def test_r326_the_table_is_never_written_over_the_table_it_keeps_rows_of(tmp_path):
    old = old_table(tmp_path)
    before = old.read_bytes()
    with pytest.raises(SystemExit):
        past_all.main(["table", "--keep-from", str(old), "native", "--out", str(old)])
    assert old.read_bytes() == before


def test_r326_an_answer_key_plan_takes_el_at_entry_from_the_schedule_when_plots_csv_has_one_else_from_its_floor_parameter():
    plan = {"sources": {"answerKey": "IMX"}, "plots": [
        {"plot": "501", "levels": {"ffl": {"value": 7.0, "source": "pastJob"}, "entry": {"value": 6.8, "source": "pastJob"}}},
        {"plot": "502", "levels": {"ffl": {"value": 7.0, "source": "pastJob"}, "entry": {"value": 6.85, "source": "pastJob"}}}]}
    data = {"documents": [document([], [])]}
    block = {"floorParameters": [], "entryParameter": "x", "linkParameters": []}
    rows = past_all.table_rows("imx", plan, data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block, schedule={"501": 6.9})
    assert [(row["entry"], row["entrySource"], row["height"]) for row in rows] == [(6.9, "schedule", 0.1), (6.85, "floorParameter", 0.15)]


def test_r326_the_real_settings_put_the_seven_answer_keys_in_the_parsons_job_with_a_parsons_dialect():
    settings = project_profile.load_settings()
    codes = [key["code"].lower() for key in settings.get("answerKeys.keys")]
    assert set(codes) <= set(settings.get("majority.groups.parsons"))
    assert not set(codes) & set(settings.get("majority.groups.native"))
    assert settings.get("placerCheck.dialects.parsons") == {"plot": "name", "datum": "none"}
