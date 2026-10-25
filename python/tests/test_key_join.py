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
import majority
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


SHARE = 0.9


def places():
    return key_join.places(FACES, NUMBERS, MANY, AREA, PLAN, SHARE)


def test_r326_a_plot_joins_the_face_its_generic_floor_stands_on_with_the_number_zone_and_nbh_of_both_sources():
    doc = document([key_link("501", 0), key_link("502", 10)], [floor("DP4-501", 0), floor("DP4-502", 10)])
    found = key_join.join_plots(doc, FLOORS, places())
    row = found["plots"]["501"]
    assert row == {"status": "joined", "face": "11", "number": "501", "share": 1.0, "sameNumber": True, "nbh": "01A", "zone": "4", "areaNbh": "01A",
                   "areaZone": "Zone 4"}
    assert found["counts"] == {"keyPlots": 2, "joined": 2, "sameNumber": 2, "otherNumber": 0, "areaAgrees": 2}


def test_r326_a_plot_whose_location_plan_area_is_not_its_area_key_is_listed():
    plan = dict(PLAN, **{"501": {"nbh": "022", "zone": "1"}})
    where = key_join.places(FACES, NUMBERS, MANY, AREA, plan, SHARE)
    found = key_join.join_plots(document([key_link("501", 0), key_link("502", 10)], [floor("DP4-501", 0), floor("DP4-502", 10)]), FLOORS, where)
    assert found["counts"]["areaAgrees"] == 1 and found["areaDiffers"] == [{"plot": "501", "nbh": "022", "zone": "1", "areaNbh": "01A", "areaZone": "Zone 4"}]


def test_r326_a_plot_on_no_face_on_two_faces_on_a_face_of_no_or_two_labels_or_with_no_or_two_generic_floors_is_listed_by_why():
    links = [key_link(str(plot), 0, element_id=plot) for plot in (601, 602, 603, 604, 605, 606)]
    floors = [floor("DP4-601", 60), floor("DP4-602", 26, width=3, y0=11, y1=14),  # 602 lies where faces 13 and 16 overlap, whole on both
              floor("DP4-603", 40), floor("DP4-604", 30), floor("DP4-606", 0), floor("TH3-606", 10)]
    found = key_join.join_plots(document(links, floors), FLOORS, places())
    why = {plot: row["status"] for plot, row in found["plots"].items()}
    assert why == {"601": "noFace", "602": "twoFaces", "603": "noNumber", "604": "manyNumbers", "605": "noGenericFloor", "606": "twoGenericFloors"}
    assert found["counts"]["joined"] == 0 and found["byStatus"]["twoFaces"] == ["602"]


def test_r326_a_generic_floor_joins_the_face_that_holds_cut_share_min_of_it_and_a_50_50_floor_is_listed_as_cut_with_its_shares():
    # 701 lies half on face 11 and half on face 12, 502 holds 0.95 of its area on face 12 and 0.05 on face 13
    links = [key_link("701", 0, element_id=701), key_link("502", 10)]
    floors = [floor("DP4-701", 5), floor("DP4-502", 10.5)]
    found = key_join.join_plots(document(links, floors), FLOORS, places())
    assert found["plots"]["701"] == {"status": "cut", "face": None, "number": None,
                                     "shares": [{"face": "11", "number": "501", "share": 0.5}, {"face": "12", "number": "502", "share": 0.5}]}
    assert found["plots"]["502"]["status"] == "joined" and found["plots"]["502"]["number"] == "502" and found["plots"]["502"]["share"] == 0.95
    assert found["counts"]["joined"] == 1 and found["byStatus"]["cut"] == ["701"]
    # The place check holds the generic floor and a ramp across a face edge to the same share
    across = floor("DP4-502", 18, "Ramp A", width=4, y0=0, y1=5)
    place = key_join.place_check(document(links, floors + [across]), FLOORS, places())
    assert place["byKind"]["generic"] == {"cut": 1, "match": 1} and place["byKind"]["ramp"] == {"cut": 1}


def test_r326_two_key_plots_on_one_official_number_are_listed_as_two_plots_and_neither_counts_as_joined():
    # The floors of 701 and 702 both stand whole on face 11, so both name number 501
    links = [key_link("701", 0, element_id=701), key_link("702", 0, element_id=702), key_link("502", 10)]
    floors = [floor("DP4-701", 0, y0=0, y1=12), floor("DP4-702", 0, y0=13, y1=25), floor("DP4-502", 10)]
    found = key_join.join_plots(document(links, floors), FLOORS, places())
    assert {plot: row["status"] for plot, row in found["plots"].items()} == {"502": "joined", "701": "twoPlots", "702": "twoPlots"}
    assert found["counts"]["joined"] == 1 and found["byStatus"]["twoPlots"] == ["701", "702"]
    assert found["twoPlots"] == [{"number": "501", "plots": ["701", "702"]}]
    row = key_join.area_row("IMX", "x.rvt", found)
    assert [(area["nbh"], area["plots"]) for area in row["areas"]] == [("01A", 1)]


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
        writer = csv.DictWriter(handle, fieldnames=["baseplanLabel", "nbhId", "zone", "officialNumber", "pastFfl", "pastEntry", "pastSource"])
        writer.writeheader()
        writer.writerow({"baseplanLabel": "11", "nbhId": "01A", "zone": "Zone 4", "officialNumber": "501", "pastFfl": "7.0", "pastEntry": "6.9", "pastSource": "schedule IMX"})
        writer.writerow({"baseplanLabel": "12", "nbhId": "01A", "zone": "Zone 4", "officialNumber": "", "pastFfl": "", "pastEntry": "", "pastSource": ""})
    return path


def test_r326_the_area_key_and_the_schedule_entry_are_read_from_plots_csv_by_baseplan_label_and_official_number(tmp_path):
    area, schedule = key_join.read_plots_csv(plots_csv(tmp_path))
    assert area == {"11": {"nbhId": "01A", "zone": "Zone 4"}, "12": {"nbhId": "01A", "zone": "Zone 4"}}
    assert schedule == {"501": {"entry": 6.9, "ffl": 7.0, "source": "schedule IMX"}}


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


SCHEDULE_KEYS = {"schedule IMX": "IMX", "schedule IMY": "IMY"}
SCHEDULE = {"501": {"entry": 6.9, "ffl": 7.0, "source": "schedule IMX"}, "502": {"entry": 6.95, "ffl": 7.0, "source": "schedule IMY"}}
BAND = 0.005


def key_plan(answer="IMX", entries=(6.8, 6.85)):
    plan = {"sources": {"answerKey": answer} if answer else {}, "summary": {"namesDialect": {}}, "plots": [], "missing": []}
    for plot, entry in zip(("501", "502"), entries):
        levels = {"ffl": {"value": 7.0, "source": "pastJob"}}
        if entry is not None:
            levels["entry"] = {"value": entry, "source": pf.KEY_PARAMETER}
        plan["plots"].append({"plot": plot, "levels": levels})
    return plan


def test_r326_an_answer_key_plan_takes_el_at_entry_from_the_schedule_when_plots_csv_has_one_else_from_its_floor_parameter():
    data = {"documents": [document([], [])]}
    block = {"floorParameters": [], "entryParameter": "x", "linkParameters": [], "scheduleKeys": SCHEDULE_KEYS}
    rows = past_all.table_rows("imx", key_plan(), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block, schedule=SCHEDULE, code="IMX",
                               conflicts=[], level_band=BAND)
    assert [(row["entry"], row["entrySource"], row["height"]) for row in rows] == [(6.9, "schedule", 0.1), (6.85, "floorParameter", 0.15)]


def test_r326_the_schedule_gives_el_at_entry_only_to_the_key_of_its_own_job_on_its_own_plots_and_the_height_says_where_it_came_from():
    assert key_join.schedule_of(SCHEDULE, "IMX", SCHEDULE_KEYS) == {"501": 6.9}
    data = {"documents": [document([], [])]}
    block = {"floorParameters": ["x"], "entryParameter": "x", "linkParameters": [], "scheduleKeys": SCHEDULE_KEYS}
    # A Native key on the same plots takes nothing of the schedules, it has no El @ Entry of its own here
    native = past_all.table_rows("u11", key_plan(answer=None, entries=(None, None)), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block,
                                 schedule=SCHEDULE)
    assert [(row["entry"], row["entrySource"], row["heightSource"]) for row in native] == [(None, None, None), (None, None, None)]
    imy = past_all.table_rows("imy", key_plan(answer="IMY"), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block, schedule=SCHEDULE, code="IMY",
                              conflicts=[], level_band=BAND)
    assert [(row["entry"], row["entrySource"], row["heightSource"]) for row in imy] == [(6.8, "floorParameter", "floorTop less floorParameter"),
                                                                                      (6.95, "schedule", "schedule less schedule")]


def test_r326_a_key_with_a_schedule_takes_ffl_el_at_entry_and_the_height_as_one_pair_of_the_schedule_and_lists_every_key_value_off_it():
    # The floor top of 501 stands 0.050 m over its schedule FFL, as on IMD 1436, so the key FFL less the schedule El @ Entry would match
    # neither source. The key El @ Entry of 502 is 0.050 m over the schedule. Both are listed and the schedule pair is kept
    schedule = {"501": {"entry": 6.9, "ffl": 6.95, "source": "schedule IMX"}, "502": {"entry": 6.8, "ffl": 7.002, "source": "schedule IMX"}}
    data = {"documents": [document([], [])]}
    block = {"floorParameters": [], "entryParameter": "x", "linkParameters": [], "scheduleKeys": SCHEDULE_KEYS}
    conflicts = []
    rows = past_all.table_rows("imx", key_plan(entries=(6.9, 6.85)), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block, schedule=schedule,
                               code="IMX", conflicts=conflicts, level_band=BAND)
    assert [(row["ffl"], row["entry"], row["height"], row["entrySource"], row["heightSource"]) for row in rows] == [
        (6.95, 6.9, 0.05, "schedule", "schedule less schedule"), (7.002, 6.8, 0.202, "schedule", "schedule less schedule")]
    assert conflicts == [{"key": "imx", "plot": "501", "field": "ffl", "keyValue": 7.0, "scheduleValue": 6.95, "gapM": -0.05, "kept": "schedule"},
                         {"key": "imx", "plot": "502", "field": "entry", "keyValue": 6.85, "scheduleValue": 6.8, "gapM": -0.05, "kept": "schedule"}]
    # A schedule row of the key with El @ Entry and no FFL is no pair, it is refused and never mixed with the key floor top
    with pytest.raises(project_profile.ProfileError, match="501"):
        past_all.table_rows("imx", key_plan(), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block,
                            schedule={"501": {"entry": 6.9, "ffl": None, "source": "schedule IMX"}}, code="IMX", conflicts=[], level_band=BAND)
    # A pair is never used where the list of conflicts or the band is not given, so a key value off it can not go unlisted
    with pytest.raises(ValueError, match="conflicts"):
        past_all.table_rows("imx", key_plan(), data, {"plot": "parameter", "datum": "none"}, FLOORS, {}, {}, {}, block, schedule=schedule, code="IMX")


def test_r326_the_conflicts_list_is_written_beside_the_table_with_both_values(tmp_path):
    path = past_all.write_conflicts(tmp_path / "past_all.csv", [{"key": "imx", "plot": "501", "field": "ffl", "keyValue": 7.0, "scheduleValue": 6.95,
                                                                 "gapM": -0.05, "kept": "schedule"}])
    assert path.name == "past_all_conflicts.csv"
    with open(path, encoding="utf-8") as handle:
        assert list(csv.DictReader(handle)) == [{"key": "imx", "plot": "501", "field": "ffl", "keyValue": "7.0", "scheduleValue": "6.95", "gapM": "-0.05",
                                                 "kept": "schedule"}]


def test_r326_an_answer_key_row_whose_el_at_entry_the_plan_refused_takes_no_other_value_and_says_refused():
    plan = key_plan(entries=(6.8, None))
    plan["missing"] = [{"plot": "502", "written": True, "missing": [{"key": "levels.entry", "why": pf.REFUSED + "the value puts the floor top 143 m over it"}]}]
    # The floor parameter still holds the placeholder, it is never taken again
    data = {"documents": [document([], [dict(floor("DP4-502", 10), parameters={"Plot": "DP4-502", "x": 150.0})])]}
    block = {"floorParameters": ["x"], "entryParameter": "x", "linkParameters": [], "scheduleKeys": SCHEDULE_KEYS}
    rows = past_all.table_rows("imx", plan, data, {"plot": "name", "datum": "none"}, FLOORS, tj_faces(), NUMBERS, {}, block, code="IMX", share_min=SHARE)
    assert [(row["entry"], row["entrySource"], row["height"], row["heightSource"]) for row in rows] == [(6.8, "floorParameter", 0.2, "floorTop less floorParameter"),
                                                                                                       (None, "refused", None, None)]


def tj_faces():
    return {label: FACES[label] for label in ("11", "12", "13")}


def test_r326_each_answer_key_takes_its_kind_from_answer_keys_and_a_key_of_no_code_has_none():
    settings = project_profile.load_settings()
    assert past_all.key_kind(settings, "IMA") == "SE" and past_all.key_kind(settings, "IMC") == "TU" and past_all.key_kind(settings, None) == ""
    assert "kind" in past_all.COLUMNS and "heightSource" in past_all.COLUMNS


KIND_RULE = {"kinds": ["TU", "SE"], "fold": False, "groups": ["parsons"]}


def test_r326_a_row_of_a_kind_group_with_no_kind_or_a_kind_answer_keys_do_not_name_is_refused_and_case_folds_only_where_the_config_says():
    groups = {"native": ["u11"], "parsons": ["imx", "imy"]}
    good = [{"key": "u11", "kind": ""}, {"key": "imx", "kind": "TU"}, {"key": "imy", "kind": "SE"}]
    assert past_all.check_kinds(good, KIND_RULE, groups) == good
    # A parsons key given with no --key-code, or a row kept from a table of before the kind column, has no kind
    with pytest.raises(project_profile.ProfileError, match="imx"):
        past_all.check_kinds([{"key": "imx", "kind": ""}], KIND_RULE, groups)
    with pytest.raises(project_profile.ProfileError, match="imy"):
        past_all.check_kinds([{"key": "imy"}], KIND_RULE, groups)
    # se is no kind of answerKeys, it would pass every kindLeaveOut list
    with pytest.raises(project_profile.ProfileError, match="se"):
        past_all.check_kinds([{"key": "imy", "kind": "se"}], KIND_RULE, groups)
    with pytest.raises(project_profile.ProfileError, match="XX"):
        past_all.check_kinds([{"key": "u11", "kind": "XX"}], KIND_RULE, groups)
    assert past_all.check_kinds([{"key": "imy", "kind": "se"}], dict(KIND_RULE, fold=True), groups) == [{"key": "imy", "kind": "SE"}]


def test_r326_the_kind_of_an_answer_key_must_be_one_of_answer_keys_kinds(tmp_path):
    values = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    values["answerKeys"]["keys"][0]["kind"] = values["answerKeys"]["keys"][0]["kind"].lower()
    path = tmp_path / "project.json"
    path.write_text(json.dumps(values), encoding="utf-8")
    code = values["answerKeys"]["keys"][0]["code"]
    with pytest.raises(project_profile.ProfileError, match=code):
        past_all.key_kind(project_profile.load_settings(path), code)
    rule = past_all.kind_rule(project_profile.load_settings())
    assert rule["kinds"] == ["TU", "SE"] and rule["fold"] is False and rule["groups"] == ["parsons"]


def test_r326_the_table_refuses_kept_parsons_rows_of_a_table_written_before_the_kind_column(tmp_path):
    old = old_table(tmp_path)
    labels = tmp_path / "labels.csv"
    labels.write_text("number,face,nbh,zone,kind,rev9,code\n", encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": []}), encoding="utf-8")
    out = tmp_path / "out" / "past_all.csv"
    out.parent.mkdir()
    with pytest.raises(project_profile.ProfileError, match="imc"):
        past_all.main(["table", "--keep-from", str(old), "parsons", "--labels", str(labels), "--baseplan", str(baseplan), "--out", str(out)])
    assert not out.exists()


def test_r326_majority_refuses_a_kind_answer_keys_do_not_name_in_its_table_and_in_kind_leave_out(tmp_path):
    path = tmp_path / "past_all.csv"
    row = dict({column: "" for column in past_all.COLUMNS}, key="ima", source="parsons", kind="se", plot="601", parameters="{}")
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=past_all.COLUMNS)
        writer.writeheader()
        writer.writerow(row)
    settings = project_profile.load_settings()
    with pytest.raises(project_profile.ProfileError, match="se"):
        majority.read_rows(path, settings, majority.load_block(settings))
    values = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    values["majority"]["kindLeaveOut"] = {"se": values["majority"]["kindLeaveOut"]["SE"]}
    changed = tmp_path / "project.json"
    changed.write_text(json.dumps(values), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="kindLeaveOut"):
        majority.load_block(project_profile.load_settings(changed))


def table_row(key, kind, plot, entry=6.8):
    row = {column: None for column in past_all.COLUMNS}
    row.update(key=key, kind=kind, plot=plot, typologyRev9="DP4", keyUnit="DP4", linkFile="a.rvt", mirrored=True, entry=entry, entrySource="floorParameter",
               ffl=7.0, height=round(7.0 - entry, 4), steps="2", rampType="Ramps 0.5%", rampSlopePercent=0.5, rampFront=6.7, rampBack=entry, rampFrontFall=0.0,
               yardTop=6.9, parameters="{}")
    return row


LEFT_OUT = ("rampSlope", "entry", "rampFront", "rampFrontFall", "height", "steps", "yard")


def kinds_taken(rows):
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    block = majority.load_block(settings)
    items = majority.contexts(rows, {}, {}, {}, {}, {}, {}, profile, block)
    results, _, _ = majority.run(items, {}, profile, block, [majority.ROAD], [])
    return {result["decision"]: result["byKind"] for result in results}, block


def test_r326_the_majority_rule_leaves_the_rows_of_a_kind_out_of_every_decision_majority_kind_leave_out_names():
    rows = [table_row("imc", "TU", str(plot)) for plot in range(501, 506)] + [table_row("ima", "SE", str(plot), 9.9) for plot in range(601, 606)]
    taken, block = kinds_taken(rows)
    assert set(block["kindLeaveOut"]["SE"]) == set(LEFT_OUT)
    for decision, kinds in taken.items():
        word = decision.split(" ")[1]
        assert ("SE" in kinds) == (word not in LEFT_OUT), decision
    assert taken["parsons entry"] == {"TU": 5} and taken["parsons hand alone"] == {"TU": 5, "SE": 5}


def test_r326_a_majority_run_over_the_real_past_all_takes_no_se_row_into_the_fields_the_se_keys_do_not_hold():
    path = project_profile.Path(project_profile.load_settings().get("siteBuild.folder")) / "past_all.csv"
    if not path.is_file():
        pytest.skip("no past_all.csv of Round ADDIN on this machine")
    rows = past_all.read_table(path)
    assert sum(1 for row in rows if row.get("kind") == "SE") > 0
    taken, _ = kinds_taken(rows)
    for decision, kinds in taken.items():
        if decision.split(" ")[1] in LEFT_OUT:
            assert "SE" not in kinds, decision


def join_inputs(tmp_path, name, share):
    """A made up project of the faces above for key_join.main, keys IMX and IMY read by the jobs KX and KY."""
    floors = dict(FLOORS, levels={"generic": "L1", "yard": "L2", "ramp": "L1"}, yardBelowFflM=0.1, yardShareTie=0.01, yardMinShare=0.5)
    settings = tmp_path / (name + ".json")
    settings.write_text(json.dumps({"floors": floors, "keyPlans": {"wallEdgeM": 0.3, "entryHeightM": {"min": 0.0, "max": 1.5}}, "cadLinks": {"cutShareMin": share},
                                    "pastAll": {"entryParameter": "x"}, "answerKeys": {"keys": [{"code": "IMX", "copy": "X-IMX.rvt", "source": "a/X-IMX.rvt"},
                                                                                                {"code": "IMY", "copy": "X-IMY.rvt", "source": "a/X-IMY.rvt"}]}}),
                        encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": label, "vertices": [list(xy) for xy in list(shape.exterior.coords)[:-1]]}
                                              for label, shape in FACES.items()]}), encoding="utf-8")
    labels = tmp_path / "labels.csv"
    labels.write_text("number,face,nbh,zone\n" + "".join(number + "," + label + ",01A,4\n" for label, number in NUMBERS.items()), encoding="utf-8")
    plots = tmp_path / (name + "_plots.csv")
    plots.write_text("baseplanLabel,nbhId,zone\n11,01A,Zone 4\n12,01A,Zone 4\n", encoding="utf-8")
    reads = tmp_path / "reads"
    for code, job in (("IMX", "KX"), ("IMY", "KY")):
        (reads / job).mkdir(parents=True, exist_ok=True)
        doc = dict(document([key_link("501", 0), key_link("502", 10)], [floor("DP4-501", 0), floor("DP4-502", 10)]), file="X-" + code + "_TEST-" + job)
        (reads / job / ("testhost_" + job + "_prototypes.json")).write_text(json.dumps({"documents": [doc]}), encoding="utf-8")
    return ["--settings", str(settings), "--reads", str(reads), "--baseplan", str(baseplan), "--labels", str(labels), "--plots", str(plots)]


def test_r326_each_key_of_the_join_keeps_the_inputs_and_the_share_it_was_run_with(tmp_path):
    out = tmp_path / "join"
    assert key_join.main(join_inputs(tmp_path, "a", 0.9) + ["--key", "IMX=KX", "--out", str(out)]) == 0
    assert key_join.main(join_inputs(tmp_path, "b", 0.8) + ["--key", "IMY=KY", "--out", str(out)]) == 0
    report = json.loads((out / key_join.OUTPUT).read_text(encoding="utf-8"))
    assert "inputs" not in report
    assert report["keys"]["IMX"]["inputs"]["plots"] == "a_plots.csv" and report["keys"]["IMX"]["settings"] == {"cutShareMin": 0.9}
    assert report["keys"]["IMY"]["inputs"]["plots"] == "b_plots.csv" and report["keys"]["IMY"]["settings"] == {"cutShareMin": 0.8}
    assert report["keys"]["IMX"]["join"]["counts"]["joined"] == 2


def test_r326_the_real_settings_put_the_seven_answer_keys_in_the_parsons_job_with_a_parsons_dialect():
    settings = project_profile.load_settings()
    codes = [key["code"].lower() for key in settings.get("answerKeys.keys")]
    assert set(codes) <= set(settings.get("majority.groups.parsons"))
    assert not set(codes) & set(settings.get("majority.groups.native"))
    assert settings.get("placerCheck.dialects.parsons") == {"plot": "name", "datum": "none"}
