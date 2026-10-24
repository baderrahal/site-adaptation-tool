import json

import pytest
from shapely.geometry import LineString

import entry_proof
import levels_proof
import project_profile
import road_lookup
import surface_lookup
from test_levels_proof import PROFILE, lone


def flat_surface(z, x0=-100, y0=-100, x1=100, y1=100):
    return surface_lookup.Surface([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [(0, 1, 2), (0, 2, 3)])


def plot_face(label, x0=0, y0=0, width=10, depth=25):
    corners = [(x0, y0), (x0 + width, y0), (x0 + width, y0 + depth), (x0, y0 + depth)]
    return {"kind": "plot_shape", "plot": label, "type": "VL4", "vertices": [list(corner) for corner in corners]}


def road_line(y):
    return [LineString([(-50, y), (50, y)])]


def a_ramp(plot, x0=0.0, y0=2.0, high=3.14):
    """A ramp inside the plot face with its higher long edge at y0+5, level `high`."""
    return {"plot": plot, "corners": [(x0, y0), (x0 + 10, y0), (x0 + 10, y0 + 5), (x0, y0 + 5)], "levels": [3.0, 3.0, high, high], "topElevation": 3.0}


def test_r93_measure_compares_the_entry_level_with_every_candidate_and_gives_the_ramp_rule_and_the_road_offset():
    faces = [plot_face("1")]
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0))])
    result = entry_proof.measure([a_ramp("11")], {"11": (5.0, 3.0)}, {"11": {"entry": 3.14}}, faces, lookup, road_line(-4))
    assert result["counts"] == {"plots": 1, "face": 1, "flatRamps": 0}
    assert result["rule"]["median"] == 0 and result["rule"]["within5mm"] == 1 and result["rule"]["entryPoint"] == "midpoint of the higher long edge of the ramp"
    assert result["roadOffsetM"] == pytest.approx(0.14) and result["note"] == "measured from past work"
    candidates = result["candidates"]
    assert candidates["higher long edge, midpoint"]["median"] == 0
    assert candidates["road at the curb point of the front edge"]["median"] == pytest.approx(0.14)
    assert candidates["corner highest"]["median"] == 0 and candidates["corner lowest"]["median"] == pytest.approx(0.14)
    assert "road at the covered ramp corners, mean" in candidates and "corner nearest the link" in candidates
    assert result["roadRule"]["n"] == 1 and result["roadRule"]["within5mm"] == 1


def test_r93_a_ramp_with_no_schedule_row_is_counted_and_a_ramp_in_no_face_gets_no_road_candidate_from_the_front_edge():
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0))])
    result = entry_proof.measure([a_ramp("11"), a_ramp("12", x0=500.0)], {}, {"11": {"entry": 3.14}}, [plot_face("1")], lookup, road_line(-4))
    assert result["counts"] == {"noScheduleRow": 1, "plots": 1, "face": 1, "flatRamps": 0}
    result = entry_proof.measure([a_ramp("12", x0=500.0)], {}, {"12": {"entry": 3.14}}, [plot_face("1")], lookup, road_line(-4))
    assert result["counts"]["noFace"] == 1 and "road at the curb point of the front edge" not in result["candidates"]
    assert result["roadOffsetM"] is None and result["roadRule"] is None


def books(rows, shift=10, name="a.xlsx"):
    return {name: (shift, rows)}


def test_r95_the_proof_works_out_the_entry_level_from_the_road_and_the_front_edge_and_counts_the_bands():
    faces = [plot_face("1"), plot_face("2", x0=20)]
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0))])
    rows = [lone(11, 3.24, entry=3.14), lone(12, 3.30, entry=3.20)]
    report = entry_proof.prove(books(rows), faces, lookup, road_line(-4), 0.14)["a.xlsx"]
    assert report["plots"] == 2 and report["withRoadLevel"] == 2 and report["noRoadLevel"] == 0 and report["noFace"] == 0
    assert report["bands"] == {"within5mm": 1, "within10mm": 1, "within30mm": 1, "worse": 1}
    assert report["surfaces"] == {"Road": 2} and report["entries"] == {"1": 3.14, "2": 3.14}
    assert [(w["plot"], w["difference"]) for w in report["worst"]] == [("12", -0.06), ("11", 0.0)]
    assert report["curbPoints"] == [[5.0, -4.0], [25.0, -4.0]]


def test_r95_a_plot_with_no_road_level_or_no_face_is_listed_and_never_filled_in():
    faces = [plot_face("1"), plot_face("2", x0=20), plot_face("2", x0=60)]
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0, 0, -10, 15, 30))])
    rows = [lone(11, 3.24, entry=3.14), lone(12, 3.24, entry=3.14), lone(13, 3.24, entry=3.14), lone(14, 3.24, entry=3.14)]
    report = entry_proof.prove(books(rows), faces, lookup, road_line(-4), 0.14)["a.xlsx"]
    assert report["withRoadLevel"] == 1 and report["noRoadLevel"] == 0
    assert {item["plot"]: item["faces"] for item in report["noFacePlots"]} == {"12": 2, "13": 0, "14": 0}
    only = entry_proof.prove(books([lone(12, 3.24, entry=3.14)]), [plot_face("2", x0=20)], lookup, road_line(-4), 0.14)["a.xlsx"]
    assert only["noRoadLevel"] == 1 and only["noRoadLevelPlots"] == [{"plot": "12", "unit": "VL4", "frontFound": True}] and only["entries"] == {}
    assert entry_proof.merge_totals({"a": only}) == {"within5mm": 0, "within10mm": 0, "within30mm": 0, "worse": 0, "plots": 1, "noRoadLevel": 1, "noFace": 0}


def test_r95_a_corner_plot_with_two_road_edges_is_counted_as_a_tied_front_and_the_proof_reports_each_workbook_apart():
    faces = [plot_face("1")]
    lookup = road_lookup.RoadLevels([("Road", flat_surface(3.0)), ("Other", flat_surface(9.0))])
    roads = [LineString([(-50, -4), (50, -4)]), LineString([(-4, -50), (-4, 50)])]
    report = entry_proof.prove({"a.xlsx": (10, [lone(11, 3.24, entry=3.14)]), "b.xlsx": (10, [lone(11, 3.24, entry=3.14)])}, faces, lookup, roads, 0.14)
    assert report["a.xlsx"]["tiedFront"] == 1 and report["b.xlsx"]["tiedFront"] == 1 and report["a.xlsx"]["worst"][0]["tied"] is True
    assert entry_proof.merge_totals(report)["within5mm"] == 2 and entry_proof.merge_totals(report)["plots"] == 2


def test_r95_a_sheet_plot_that_is_not_a_number_stops_the_proof():
    with pytest.raises(project_profile.ProfileError, match="sheet plot 11a is not a number"):
        entry_proof.prove(books([lone("11a", 3.24)]), [plot_face("1")], road_lookup.RoadLevels([]), road_line(-4), 0.14)


def test_r96_a_plot_that_misses_only_with_the_computed_entry_level_has_the_cause_entry_level_and_the_others_keep_their_cause():
    rows = [lone(11, 10.1, entry=10.0), lone(12, 12.1, entry=12.0), lone(13, 14.1, entry=14.0), lone(14, 13.25, entry=13.0, steps=2)]
    computed = {"1": 10.0, "2": 12.02, "4": 13.0}
    report = levels_proof.entry_cluster_report(PROFILE, [(10, rows)], [], computed)
    assert (report["plots"], report["withComputedEntry"], report["noComputedEntry"]) == (4, 3, 1)
    assert (report["fflMatches"], report["stepsMatches"], report["bothMatch"]) == (1, 2, 1)
    assert report["missCauses"] == {"ffl": {"entry level": 2, "base height": 1}, "steps": {"entry level": 1, "base height": 1}}
    assert report["entryErrorBands"] == {"within5mm": 2, "within10mm": 2, "within30mm": 3, "worse": 0}


def test_r96_the_causes_add_up_to_the_misses_and_a_computed_entry_that_equals_the_sheet_gives_the_run_of_r67():
    rows = [lone(11, 10.1, entry=10.0), lone(12, 12.1, entry=12.0)]
    report = levels_proof.entry_cluster_report(PROFILE, [(10, rows)], [], {"1": 10.0, "2": 12.0})
    assert report["fflMatches"] == 2 and report["missCauses"] == {"ffl": {}, "steps": {}}
    same = levels_proof.cad_cluster_report(PROFILE, [(10, rows)], [])
    assert same["fflMatches"] == report["fflMatches"] and same["stepsMatches"] == report["stepsMatches"]
    miss = levels_proof.entry_cluster_report(PROFILE, [(10, rows + [lone(13, 20.0, entry=14.0)])], [], {"1": 10.0, "2": 12.0, "3": 14.0})
    assert sum(miss["missCauses"]["ffl"].values()) == miss["plots"] - miss["fflMatches"] == 1


def test_r96_read_entries_joins_the_workbooks_by_baseplan_label_and_a_label_twice_stops(tmp_path):
    path = tmp_path / "entry_proof.json"
    path.write_text(json.dumps({"workbooks": {"a": {"entries": {"1": 3.14}}, "b": {"entries": {"2": 3.2}}}}), encoding="utf-8")
    assert levels_proof.read_entries(path) == {"1": 3.14, "2": 3.2}
    path.write_text(json.dumps({"workbooks": {"a": {"entries": {"1": 3.14}}, "b": {"entries": {"1": 3.2}}}}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="two workbooks give an entry level to the baseplan label 1"):
        levels_proof.read_entries(path)
    path.write_text("{not json", encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="the entry proof cannot be read"):
        levels_proof.read_entries(path)
    with pytest.raises(project_profile.ProfileError, match="the entry proof cannot be read"):
        levels_proof.read_entries(tmp_path / "missing.json")


def test_r96_the_levels_proof_causes_now_name_entry_level_first():
    assert levels_proof.CAUSES[0] == "entry level" and levels_proof.count_causes(["entry level", "other", "entry level"]) == {"entry level": 2, "other": 1}


def test_r95_main_refuses_a_missing_offsets_file_in_one_line(tmp_path, capsys):
    code = entry_proof.main(["proof", str(tmp_path / "out"), "--cad", str(tmp_path), "--roads", str(tmp_path), "--landxml-dir", str(tmp_path), "--offsets", str(tmp_path / "none.json")])
    assert code == 2 and capsys.readouterr().err.startswith("entry_proof: ")
