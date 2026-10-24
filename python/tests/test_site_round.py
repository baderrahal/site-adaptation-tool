"""Round Z4 on small made up inputs: the round files, the cover and choice, the site spec, the plan from the data book,
the compare in bands and its second method. Every name and number here is invented (R10)."""
import hashlib
import json
import math

import pytest
from shapely.geometry import Polygon

import key_cover
import plan_from_book
import plan_from_past
import project_profile
import round_files
import site_spec
import z4_check
import z4_compare

BANDS = [0, 1, 10, 30]
FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]},
          "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}
RAMP_BANDS = [{"type": "Ramp A", "belowPercent": 1.25}, {"type": "Ramp B", "belowPercent": None}]


def settings_file(tmp_path):
    values = {"profile": "rules/none.json", "schedules": ["book.xlsx"], "inputRoot": str(tmp_path),
              "pastJob": {"neighbourhood": "AAA", "schedule": "book.xlsx", "sheet": "S", "prototypes": "p.json", "prototypeFolder": "P",
                          "output": "o.json", "parameters": ["P-Number", "P-Unit", "CASE"]},
              "floors": FLOORS,
              "siteBuild": {"folder": str(tmp_path / "round"), "coordinateStepM": 100, "parameterGroup": "Group", "bandsMm": BANDS}}
    path = tmp_path / "project.json"
    path.write_text(json.dumps(values), encoding="utf-8")
    return project_profile.load_settings(path)


# R201, R202, R218 round files

def digest(data):
    return hashlib.sha256(data).hexdigest()


def test_r201_the_snapshot_is_read_the_same_or_each_change_is_named(tmp_path):
    (tmp_path / "a").mkdir()
    (tmp_path / "a" / "x.csv").write_bytes(b"one")
    (tmp_path / "y.json").write_bytes(b"two")
    manifest = tmp_path / "manifest_snapshot.txt"
    manifest.write_text(digest(b"one") + " *./a/x.csv\n" + digest(b"two") + "  y.json\n", encoding="utf-8")

    assert round_files.check_snapshot(tmp_path, manifest) == {"same": 2, "changed": [], "gone": [], "notListed": []}

    (tmp_path / "y.json").write_bytes(b"rebuilt")
    (tmp_path / "z.txt").write_bytes(b"new")
    (tmp_path / "a" / "x.csv").unlink()
    result = round_files.check_snapshot(tmp_path, manifest)
    assert result["changed"] == ["y.json"] and result["gone"] == ["a/x.csv"] and result["notListed"] == ["z.txt"]


def test_r202_each_original_is_the_same_by_hash_or_named_changed(tmp_path):
    (tmp_path / "m.rvt").write_bytes(b"model")
    (tmp_path / "n.rvt").write_bytes(b"other")
    before = tmp_path / "before.txt"
    before.write_text(digest(b"model") + " *m.rvt\n" + digest(b"old") + " *n.rvt\n", encoding="utf-8")

    assert round_files.check_originals(tmp_path, before) == {"m.rvt": "same", "n.rvt": "changed or gone"}


def test_r218_the_clean_list_names_every_entry_but_the_kept_ones_with_its_size_and_deletes_only_those(tmp_path):
    work = tmp_path / "work"
    work.mkdir()
    (work / "key_TEST-K24.rvt").write_bytes(b"x" * 10)
    (work / "copy.rvt").write_bytes(b"x" * 7)
    (work / "scratch").mkdir()
    (work / "scratch" / "s.bin").write_bytes(b"x" * 5)
    (tmp_path / "outside.rvt").write_bytes(b"x")

    rows = round_files.clean_list(work, ["KEY_TEST-K24.rvt"])
    round_files.delete_listed(work, rows)

    assert rows == [{"name": "copy.rvt", "bytes": 7}, {"name": "scratch", "bytes": 5}]
    assert sorted(path.name for path in work.iterdir()) == ["key_TEST-K24.rvt"]
    assert (tmp_path / "outside.rvt").exists()
    with pytest.raises(project_profile.ProfileError):
        round_files.delete_listed(work, [{"name": "../outside.rvt"}])


# R206, R207 cover and choice

LABELS = {"1": {"nbh": "N1", "zone": "2", "rev9": "DT3", "kind": "clientCode"}, "2": {"nbh": "N1", "zone": "2", "rev9": "DT3", "kind": "clientCode"},
          "3": {"nbh": "N2", "zone": "1", "rev9": "DT3", "kind": "clientCode"}, "4": {"nbh": "N2", "zone": "1", "rev9": "VL4", "kind": "clientCode"},
          "5": {"nbh": "N2", "zone": "1", "rev9": "VL4", "kind": "clientCode"}, "6": {"nbh": "N3", "zone": "4", "rev9": "C10", "kind": "clientCode"}}
PLAN = {"plots": [{"plot": plot, "unit": {"value": "D4"}} for plot in ("1", "2", "3", "4", "5", "9")]}


def test_r206_the_cover_counts_the_plots_of_the_key_by_neighbourhood_zone_and_code():
    found = key_cover.cover(PLAN, LABELS)

    assert found["byNeighbourhood"]["N1"]["plots"] == 2 and found["byNeighbourhood"]["N2"]["zone"] == "1"
    assert found["byNeighbourhood"]["N2"]["typologyRev9"] == {"VL4": 2, "DT3": 1}
    assert found["withNoNeighbourhood"] == 1 and "N3" not in found["byNeighbourhood"]


def test_r206_the_zone_neighbourhood_wins_else_the_largest_one_the_fits_were_not_made_on():
    found = key_cover.cover(PLAN, LABELS)

    assert key_cover.choose_nbh(found, "N1", ["N1"])[0] == "N1"
    nbh, why = key_cover.choose_nbh(found, "N3", ["N1"])
    assert nbh == "N2" and "not made on" in why
    assert key_cover.choose_nbh(found, "N3", ["N1", "N2"])[0] == "N2"


def test_r207_the_code_with_most_plots_that_has_a_file_and_was_placed_is_chosen():
    found = key_cover.cover(PLAN, LABELS)

    choice = key_cover.choose_code(found, "N2", {"VL4": "v.rvt", "DT3": "d.rvt"}, ["DT3", "VL4"])
    assert choice["code"] == "VL4" and choice["plots"] == 2
    assert key_cover.choose_code(found, "N2", {"DT3": "d.rvt"}, ["DT3", "VL4"])["notUsable"] == {"VL4": 2}
    assert key_cover.chosen_plots(PLAN, LABELS, "N2", "VL4") == ["4", "5"]
    with pytest.raises(project_profile.ProfileError):
        key_cover.choose_code(found, "N2", {}, ["DT3"])


# R211, R212 site spec

def test_r211_control_points_are_read_from_their_e_and_n_texts():
    words = [{"text": text} for text in ["x", "E", "=", "1000.5", "N", "=", "2000.25", "CONTROL", "E", "=", "bad", "N", "=", "1"]]

    assert site_spec.control_points(words) == [(1000.5, 2000.25)]


def test_r211_the_origin_is_the_centre_of_the_plots_rounded_down_and_the_page_turn_is_measured():
    assert site_spec.origin_of([[1234.0, 5678.0], [1434.0, 5878.0]], 100) == [1300, 5700]
    assert site_spec.fit_turn_deg([[2.0, 0.0], [0.0, -2.0], [5.0, 5.0]]) == pytest.approx(0.0)
    turn = math.radians(1.0)
    assert site_spec.fit_turn_deg([[2.0, 0.0], [-2 * math.sin(turn), -2 * math.cos(turn)], [0, 0]]) == pytest.approx(1.0)


def test_r212_the_site_file_takes_names_from_the_config_the_angle_0_and_the_check_points(tmp_path):
    spec = site_spec.build(settings_file(tmp_path), [[1234.0, 5678.0], [1434.0, 5878.0]], [(1000.0, 2000.0)], 0.005)

    assert spec["format"] == "site-adaptation-site"
    assert spec["projectPosition"] == {"eastM": 1300, "northM": 5700, "elevationM": 0.0, "angleDeg": 0.0}
    assert spec["levels"] == ["L1", "L2"] and spec["floorTypes"]["ramp"] == ["Ramp A", "Ramp B"]
    assert spec["floorParameter"] == "Plot" and spec["linkParameters"] == ["P-Number", "P-Unit", "CASE"]
    assert [point["name"] for point in spec["checkPoints"]] == ["control 1", "plots min", "plots max"]
    with pytest.raises(project_profile.ProfileError):
        site_spec.build(settings_file(tmp_path), [], [], None)


# R208 to R210 plan from the data book

class Profile:
    def get(self, key):
        return {"slopes_percent.front_min": 0.5, "slopes_percent.front_max": 2.0}[key]


def builder(plot="7", unit="DT3"):
    made = plan_from_past.PlotBuilder(plot)
    made.entry["unit"] = {"value": unit, "source": plan_from_book.LOCATION}
    return made


def test_r209_el_at_entry_is_the_mean_front_road_level_plus_the_slope_of_the_unit_along_the_ramp():
    low, slope = plan_from_book.entry_estimate([10.0, 10.2], "DT3", Profile(), {"C10"}, 5.52)
    steep, steep_slope = plan_from_book.entry_estimate([10.0, 10.2], "C10", Profile(), {"C10"}, 5.5)

    assert slope == 0.5 and low == pytest.approx(10.1 + 0.005 * 5.52)
    assert steep_slope == 2.0 and steep == pytest.approx(10.1 + 0.02 * 5.5)


def test_r209_every_level_is_marked_estimate_and_the_link_z_is_the_ffl():
    made = builder()
    made.entry["insert"] = {"value": [1.0, 2.0, None], "source": "computed"}

    plan_from_book.level_values(made, {"ffl": 10.4, "ssl": 10.3, "entry": 10.1, "height": 0.3, "steps": 2, "flags": []})

    assert {value["source"] for value in made.entry["levels"].values()} == {"estimate"}
    assert made.entry["insert"]["value"][2] == 10.4
    empty = builder()
    plan_from_book.level_values(empty, None)
    assert empty.missing[0]["key"] == "levels.ffl" and empty.entry["levels"] == {}


def test_r208_the_generic_floor_is_the_whole_face_with_its_hole_at_the_ffl():
    face = Polygon([(0, 0), (20, 0), (20, 30), (0, 30)], [[(5, 5), (6, 5), (6, 6)]])
    inputs = {"floors": FLOORS, "levels": {"L1": 2.0}}

    floor = plan_from_book.generic_floor(builder(), face, inputs, 10.4)

    loops = floor["outline"]["value"]["loops"]
    assert [len(loop["points"]) for loop in loops] == [4, 3]
    assert floor["type"]["value"] == "Slab G" and floor["z"] == {"value": 10.4, "source": "estimate"} and floor["heightOffset"]["value"] == 8.4
    assert floor["plotValue"]["value"] == "DT3-7"
    assert plan_from_book.generic_floor(builder(), face, {"floors": FLOORS, "levels": {}}, 10.4) is None


def test_r210_the_ramp_front_sits_on_the_road_the_back_is_parallel_with_its_mean_at_entry_and_the_slope_picks_the_type():
    corners = [(0.0, 0.0), (10.0, 0.0), (10.0, 5.52), (0.0, 5.52)]
    inputs = {"floors": FLOORS, "levels": {"L1": 0.0}, "bands": RAMP_BANDS}

    floor = plan_from_book.ramp_floor(builder(), None, corners, [10.0, 10.2], 10.1276, 0.5, inputs)
    steep = plan_from_book.ramp_floor(builder(), None, corners, [10.0, 10.2], 10.21, 2.0, inputs)

    z = [corner[2] for corner in floor["corners"]["value"]]
    assert z[:2] == [10.0, 10.2] and (z[2] + z[3]) / 2 == pytest.approx(10.1276, abs=0.001) and z[2] - z[3] == pytest.approx(0.2, abs=0.001)
    assert floor["type"]["value"] == "Ramp A" and steep["type"]["value"] == "Ramp B"
    assert floor["corners"]["source"] == "estimate"


def test_r208_clusters_take_the_location_plan_numbers_and_drop_plots_with_none():
    clusters = [{"code": "M04", "plots": ["10", "11"]}, {"code": "M04", "plots": ["12"]}]

    assert plan_from_book.numbered_clusters(clusters, {"10": "1990", "11": "1991"}) == [{"code": "M04", "plots": ["1990", "1991"]}]


# R215, R216 compare

def view(x=0.0, y=0.0, z=10.0, rotation=30.0, mirrored=True, ffl=10.0, file="F.rvt", top=10.0, shift=0.0, ramp_shift=0.0):
    square = Polygon([(shift, 0), (20 + shift, 0), (20 + shift, 20), (shift, 20)])
    ramp = Polygon([(ramp_shift, 0), (10 + ramp_shift, 0), (10 + ramp_shift, 5), (ramp_shift, 5)])
    return {"file": file, "insert": [x, y, z], "rotation": rotation, "mirrored": mirrored, "ffl": ffl, "parameters": {"P-Number": "7"},
            "floors": {"generic": {"type": "Slab G", "shape": square, "z": top, "corners": None},
                       "ramp": {"type": "Ramp A", "shape": ramp, "z": 9.0, "corners": [[ramp_shift, 0, 9.0], [10 + ramp_shift, 0, 9.0], [10 + ramp_shift, 5, 9.1], [ramp_shift, 5, 9.1]]}}}


@pytest.mark.parametrize("gap, band", [(0.0, "equal"), (0.0008, "within1mm"), (0.001, "within1mm"), (0.0055, "within10mm"), (0.03, "within30mm"), (0.031, "worse"), (None, "missing")])
def test_r215_a_gap_falls_in_equal_the_first_band_that_holds_it_or_worse(gap, band):
    assert z4_compare.band(gap, BANDS) == band


def test_r215_each_field_of_a_plot_is_banded_against_the_key():
    rows = z4_compare.compare_plot(view(x=0.005, z=10.02), view(), ["P-Number"], BANDS)

    assert rows["position"][0] == "within10mm" and rows["zAgainstFfl"][0] == "within30mm"
    assert rows["rotation"][0] == "equal" and rows["mirrored"][0] == "equal" and rows["file"][0] == "equal"
    assert rows["parameter P-Number"][0] == "equal" and rows["generic outline"][0] == "equal" and rows["ramp corners"][0] == "equal"
    assert rows["yard presence"][0] == "both missing"


def test_r215_reasons_name_the_cut_plot_the_mirror_rule_the_estimate_the_road_the_edge_and_another_file():
    key = view()
    mine = view(z=10.2, mirrored=False, shift=0.05)
    mine["floors"]["ramp"]["corners"] = [[0, 0, 8.9], [10, 0, 8.9], [10, 5, 9.1], [0, 5, 9.1]]
    reasons = z4_compare.reasons_of("7", z4_compare.compare_plot(mine, key, [], BANDS), set(), 0.03)
    other = z4_compare.reasons_of("8", z4_compare.compare_plot(view(mirrored=False, file="G.rvt"), key, [], BANDS), {"8"}, 0.03)
    edge = z4_compare.reasons_of("9", z4_compare.compare_plot(view(ramp_shift=19.0), key, [], BANDS), set(), 0.03)

    assert reasons == ["cut plot", "mirror rule", "estimate FFL", "road level"]
    assert other == ["cut plot", "other: the key links another prototype file"]
    assert edge == ["other: the ramp of the key is on another edge of the plot"]


def test_r215_the_key_datum_is_its_survey_elevation_proved_on_its_ffl_parameter_and_taken_off_the_key():
    data = {"documents": [{"kind": "project", "surveyPoint": {"position": [0, 0, -100.0], "sharedPosition": [0, 0, 0]},
                           "floors": [{"parameters": {"FFL": 10.0}, "topElevationRange": {"min": 110.0, "max": 110.0}},
                                      {"parameters": {"FFL": 9.0}, "topElevationRange": {"min": 110.0, "max": 110.0}}]}]}

    datum, proof = z4_compare.key_datum(data, "FFL")
    lowered = z4_compare.lower(view(z=110.0, ffl=110.0, top=110.0), datum)

    assert datum == 100.0 and proof == {"equal within 1 mm": 1, "differs": 1}
    assert lowered["insert"][2] == 10.0 and lowered["ffl"] == 10.0 and lowered["floors"]["generic"]["z"] == 10.0
    assert lowered["floors"]["ramp"]["corners"][0][2] == pytest.approx(-91.0)


def test_r216_the_run_counts_bands_ranks_the_worst_without_the_origin_of_another_file_and_lists_the_absent():
    key = {"1": view(), "2": view(), "3": view()}
    mine = {"1": view(x=30.0, file="G.rvt"), "2": view(z=10.5)}

    report = z4_compare.run(mine, key, ["1", "2", "3"], ["P-Number"], BANDS, set())

    assert report["compared"] == 2 and report["absent"] == [{"plot": "3", "inMine": False, "inKey": True}]
    assert report["counts"]["position"] == {"worse": 1, "equal": 1}
    assert report["worst10"][0]["plot"] == "2" and report["worst10"][0]["worstField"] == "zAgainstFfl"
    assert report["byPlot"][1]["worstField"] != "position"


# R217 second method

def test_r217_the_vertex_gap_is_the_largest_nearest_vertex_distance_both_ways():
    first = [{"points": [[0, 0], [10, 0], [10, 10], [0, 10]]}]
    second = [{"points": [[0, 0], [10, 0], [10, 10.003], [0, 10]]}]

    assert z4_check.vertex_gap(first, first) == 0
    assert z4_check.vertex_gap(first, second) == pytest.approx(0.003)
    assert z4_check.bands_of(0.003, BANDS) == "within10mm" and z4_check.bands_of(0.0, BANDS) == "equal"


def test_r217_plan_checks_agree_with_the_compare_band_and_not_otherwise():
    loops = {"loops": [{"points": [[0, 0], [10, 0], [10, 10], [0, 10]]}]}
    cad = {"7": {"levels": {"ffl": {"value": 10.02}}, "floors": {"generic": {"outline": {"value": loops}}}}}
    key = {"7": {"levels": {"ffl": {"value": 110.0}}, "floors": {"generic": {"outline": {"value": loops}}}}}
    report = {"byPlot": [{"plot": "7", "gapsMm": {"zAgainstFfl": 20.0, "generic outline": 0.0}, "bands": {"zAgainstFfl": "within30mm", "generic outline": "equal"}}]}
    wrong = {"byPlot": [{"plot": "7", "gapsMm": {"zAgainstFfl": 20.0}, "bands": {"zAgainstFfl": "within10mm", "generic outline": "equal"}}]}

    assert [row["agree"] for row in z4_check.plan_checks(["7"], cad, key, 100.0, report, BANDS)] == [True, True]
    assert [row["agree"] for row in z4_check.plan_checks(["7"], cad, key, 100.0, wrong, BANDS)] == [False, True]
