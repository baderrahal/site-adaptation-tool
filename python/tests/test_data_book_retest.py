import csv

import pytest
from shapely.geometry import Polygon

import c3d_window
import data_book
import data_book_retest
import data_book_retest_check
import project_profile

BANDS = [0.005, 0.03]


def profile():
    return project_profile.ProjectProfile("dummy.json", {
        "units_mm": True,
        "ramp": {"front_setback": {"default": 5520, "C10": 5500}},
        "slopes_percent": {"front_min": 0.5, "front_max": 2.0},
        "typologies": {"C10": {"kind": "villa", "plot_width_m": 16}, "VL4": {"kind": "villa", "plot_width_m": 12}, "DP2": {"kind": "duplex", "plot_width_m": 12},
                       "VD1": {"kind": "villa", "plot_width_m": 10}, "DP1": {"kind": "duplex", "plot_width_m": 10}, "TH1": {"kind": "townhouse", "plot_width_m": 8}},
        "plot": {"vl3_width": 12000, "long_side": 25000},
    })


def row(**values):
    base = {k: "" for k in data_book.BASE_COLUMNS}
    base.update(values)
    return base


def square(x, y, w=10, d=20):
    return Polygon([(x, y), (x + w, y), (x + w, y + d), (x, y + d)])


# R169 export every surface

def test_r169_export_known_surfaces_empties_the_skip_list():
    book = {"knownSurfaces": {"roadSurface": ["Top Surface-02"]}}
    assert c3d_window.known_surfaces(book, "roadSurface") == ["Top Surface-02"]
    assert c3d_window.known_surfaces(book, "roughGrading") == []
    assert c3d_window.known_surfaces(dict(book, exportKnownSurfaces=True), "roadSurface") == []


def test_r169_runner_passes_an_empty_known_list(tmp_path):
    root = tmp_path / "book"
    (root / "work" / "Roads").mkdir(parents=True)
    (tmp_path / "source").mkdir()
    (root / "work" / "Roads" / "s.dwg").write_bytes(b"dwg")
    config = {"sourceRoot": str(tmp_path / "source"), "language": "en-US", "probeDll": str(tmp_path / "probe.dll"),
              "dataBook": {"root": str(root), "drawings": {"roadSurface": "Roads/s.dwg"}, "acad": "acad.exe", "watcher": "watch.exe", "minFreeGB": 2,
                           "timeoutMinutes": 1, "knownSurfaces": {"roadSurface": ["A", "B"]}, "exportKnownSurfaces": True}}
    seen = []

    class Done:
        pid, returncode = 1, 0

        def poll(self):
            return 0

    def launch(args, environment):
        seen.append(environment)
        return Done()

    c3d_window.run(config, "roadSurface", launcher=launch, memory=lambda: 8.0, sleep=lambda s: None)
    assert seen[0]["C3DPROBE_KNOWN"] == ""


# R170 El @ Entry from the road plus the ramp

def test_r170_depth_and_slopes_come_from_the_profile():
    p = profile()
    assert data_book_retest.ramp_depth(p, "C10") == pytest.approx(5.5) and data_book_retest.ramp_depth(p, "DP2") == pytest.approx(5.52)
    assert data_book_retest.slopes(p, {"C10"}, "C10") == {"slope0.5": 0.5, "slope2": 2.0, "byTypology": 2.0}
    assert data_book_retest.slopes(p, {"C10"}, "DP2")["byTypology"] == 0.5


def test_r170_entry_is_the_corner_mean_plus_slope_times_depth():
    rows = [row(officialNumber="11", unitRev6="DP2", unitRev6Source="schedule IMC", roadLevelLeft="100.0", roadLevelRight="100.02", pastEntry="100.0376"),
            row(officialNumber="12", unitRev6="C10", unitRev6Source="schedule IMD", roadLevelLeft="100.0", roadLevelRight="100.0", pastEntry="100.11"),
            row(officialNumber="13", unitRev6="DP2", unitRev6Source="schedule IMC", roadLevelLeft="", roadLevelRight="100.0", pastEntry="100.0"),
            row(officialNumber="14", unitRev6="", unitRev6Source="", roadLevelLeft="100.0", roadLevelRight="100.0", pastEntry="100.0")]
    items = data_book_retest.entry_rows(rows, profile(), {"C10"}, ["roadLevel"], BANDS)
    band = {(i["plot"], i["variant"]): i["band"] for i in items}
    assert band[("11", "slope0.5")] == "within5mm" and band[("11", "slope2")] == "worse" and band[("11", "byTypology")] == "within5mm"
    assert band[("12", "slope2")] == "within5mm" and band[("12", "byTypology")] == "within5mm" and band[("12", "slope0.5")] == "worse"
    assert band[("13", "slope0.5")] == "noLevel" and not any(i["plot"] == "14" for i in items)
    summary = data_book_retest.entry_summary(items)["roadLevel"]
    assert summary["byTypology"] == {"plots": 3, "within5mm": 2, "noLevel": 1, "medianDifference": pytest.approx(0, abs=1e-4)}


def test_r170_surfaces_are_the_road_first_then_every_level_column():
    rows = [dict(row(), **{"level_B_left": "", "level_B_right": "", "level_A_left": "", "level_A_right": ""})]
    assert data_book_retest.surface_names(rows) == ["roadLevel", "A", "B"]
    items = data_book_retest.entry_rows([dict(rows[0], officialNumber="1", unitRev6="DP2", unitRev6Source="schedule IMC", pastEntry="10.0",
                                              level_A_left="9.9724", level_A_right="9.9724")], profile(), set(), ["A"], BANDS)
    assert {i["variant"]: i["band"] for i in items}["slope0.5"] == "within5mm"


# R171 units per face

def blocks_on(faces):
    return [{"name": "Villa_VL4", "shape": square(0.5, 1, 9, 18)},
            {"name": "Duplex_DP2", "shape": square(10.5, 1, 19, 18)},
            {"name": "Townhouse_TH1", "shape": square(30.5, 1, 39, 18)},
            {"name": "Townhouse_TH1", "shape": square(200, 0, 40, 20)}]


def test_r171_each_face_a_block_covers_holds_one_of_its_units():
    faces = {str(k + 1): square(10 * k, 0) for k in range(7)}
    types = {"1": "VL4", "2": "DP2", "3": "DP2", "4": "DP2", "5": "TH1", "6": "TH1", "7": "DP2"}
    per_face, per_block = data_book_retest.units_on_faces(faces, blocks_on(faces), 0.1)
    assert {k: len(v) for k, v in per_face.items()} == {"1": 1, "2": 1, "3": 1, "4": 1, "5": 1, "6": 1, "7": 1}
    units = data_book_retest.unit_count(per_face, per_block, types, {"VL4", "DP2", "TH1"})
    assert units["blocksByName"]["Townhouse_TH1"]["noFace"] == 1 and units["blocksByName"]["Townhouse_TH1"]["usualCover"] == {"DP2": 2, "TH1": 2}
    assert units["unitsByClientType"] == {"VL4": 1, "DP2": 6, "TH1": 4} and units["unitTotal"] == 11
    assert units["facesByUnitCount"]["DP2"] == {1: 4}


def test_r171_a_sliver_under_the_share_is_no_unit_and_blocks_outside_are_left_out():
    faces = {"1": square(0, 0), "2": square(10, 0)}
    per_face, _ = data_book_retest.units_on_faces(faces, [{"name": "Villa_VL4", "shape": square(0.5, 1, 9.9, 18)}], 0.1)
    assert {k: len(v) for k, v in per_face.items()} == {"1": 1}
    raw = [{"name": "x$0$Villa_VL4", "outline": [[1, 1], [9, 1], [9, 19], [1, 19]]}, {"name": "MP-Parking", "outline": [[1, 1], [2, 1], [2, 2]]},
           {"name": "Villa_C10", "outline": [[500, 1], [509, 1], [509, 19]]}]
    kept = data_book_retest.unit_blocks(raw, {"Villa"}, lambda point: point.x < 100)
    assert [b["name"] for b in kept] == ["Villa_VL4"]


def test_r171_phase1_sheet_and_the_gap(tmp_path):
    path = tmp_path / "Phase1.csv"
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["A", "B", "C", "H"])
        writer.writerow(["", "", "Villa (C10)", "3"])
        writer.writerow(["", "", "Duplex (DP2,DP1)", "5"])
        writer.writerow(["", "Total Single family residential (Villas/Townhouses)", "", "8"])
    sheet = {"typologyColumn": "C", "countColumn": "H", "labels": ["Villa (C10)", "Duplex (DP2,DP1)"], "totalColumn": "B",
             "totalLabel": "Total Single family residential (Villas/Townhouses)"}
    groups, total = data_book_retest.phase1_sheet(path, sheet)
    assert groups == {"Villa (C10)": {"types": ["C10"], "count": 3}, "Duplex (DP2,DP1)": {"types": ["DP2", "DP1"], "count": 5}} and total == 8
    units = {"unitsByClientType": {"C10": 3, "DP2": 4}, "unitTotal": 7}
    gap = data_book_retest.unit_gap(units, groups, total, {"byCode": {"C10": 2, "DP2": 4, "DP1/VD1": 3}}, profile())
    assert gap["total"] == {"footprintUnits": 7, "unitsRev6": 9, "phase1Sheet": 8, "gapToUnitsRev6": -2, "gapToSheet": -1}
    assert gap["bySheetRow"]["Duplex (DP2,DP1)"]["gap"] == -1
    assert gap["byKind"]["C10"] == {"footprintUnits": 3, "unitsRev6": 2, "gap": 1} and gap["byKind"]["duplex or villa"]["unitsRev6"] == 3


def test_r171_schedule_cross_counts_client_type_against_unit_code():
    rows = [row(clientType="DP2", unitRev6="VD2"), row(clientType="DP2", unitRev6="VD2"), row(clientType="DP2", unitRev6="DP2"), row(clientType="VL3")]
    assert data_book_retest.schedule_cross(rows) == {"DP2>DP2": 1, "DP2>VD2": 2}


# R172 width meaning

def test_r172_width_keys_and_faces_by_units():
    found = data_book_retest.width_meaning(profile(), {"DP2": {1: 4}, "VL4": {0: 1, 1: 2, 2: 1}})
    assert found == {"typologyWidthKeys": ["plot_width_m"], "plotWidthKeys": ["plot.vl3_width"], "unitFacesByUnits": {"one": 6, "none": 1, "more": 1}}


# R173 second method

def test_r173_front_edge_is_the_edge_nearest_the_curb_of_at_least_the_least_length():
    ring = [(0, 0), (10, 0), (10, 20), (0, 20)]
    curbs = [(-5, -4, 15, -4), (14, -10, 14, 30)]
    distance, a, b, length = data_book_retest_check.front_edge(ring, curbs, 15)
    assert (a, b) == ((10, 0), (10, 20)) and length == 20 and distance == pytest.approx(4)
    _, a, b, _ = data_book_retest_check.front_edge(ring, curbs, 6)
    assert (a, b) == ((0, 0), (10, 0))
    assert data_book_retest_check.front_edge(ring, curbs, 50) is None


def test_r173_classes_for_setback_and_width():
    assert [data_book_retest_check.setback_class(v, [5.0, 3.0], 0.05) for v in (5.02, 2.96, 5.52, None)] == ["5.00", "3.00", "other", "none"]
    analysis = {"widthsByClientType": {"DP2": [10, 12]}, "widthFromCad": ["VL4"], "widthToleranceM": 0.1}
    assert [data_book_retest_check.width_class(k, w, analysis) for k, w in (("DP2", 12.05), ("DP2", 13), ("VL4", 30), ("VL2", 10))] == [
        "matches", "differs", "fromCad", "notChecked"]


def test_r173_fronts_and_export_compare_with_the_main_output():
    faces = {"1": [[(0, 0), (12, 0), (12, 25), (0, 25)]]}
    curbs = [(-5, -4, 20, -4)]
    setbacks = [(0, 5, 12, 5), (0, 5, 0, 20)]
    analysis = {"widthsByClientType": {"DP2": [12]}, "widthToleranceM": 0.1, "setbackToleranceM": 0.05}
    retest = {"setbackLinesM": [5.0, 3.0], "check": {"minEdgeM": 6, "lengthSlackM": 0.05}}
    good = row(baseplanLabel="1", clientType="DP2", widthM="12.0", setbackFromFrontM="5.0")
    bad = row(baseplanLabel="1", clientType="DP2", widthM="10.0", setbackFromFrontM="3.0")
    counts = data_book_retest_check.check_fronts([good, bad], faces, curbs, setbacks, analysis, retest)
    assert counts["widthValueMatch"] == 1 and counts["widthValueMismatch"] == 1 and counts["setbackClassMatch"] == 1 and counts["setbackClassMismatch"] == 1
    rows = [{"level_S_left": "1.0", "level_S_right": "1.0", "level_d__S_left": "1.0004", "level_d__S_right": ""},
            {"level_S_left": "1.0", "level_S_right": "", "level_d__S_left": "1.01", "level_d__S_right": ""}]
    assert data_book_retest_check.check_export(rows, [("S", "d__S")]) == {"S": {"equal": 1, "onlyOne": 1, "differ": 1, "neither": 1}}
