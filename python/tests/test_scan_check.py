"""R420, R427, D19. The second checks of the scanner on small made up results, reads and records."""

import copy
import json

import input_guard
import scan_check

RESULT = {
    "inputs": [{"role": "config", "name": "project.json", "size": 2, "sha256": "a"}],
    "rows": [{"kind": "plotOutline", "source": "one.dwg|A + B", "found": "2 faces", "proposedUse": "plot outlines", "score": 2, "conflicts": 0},
             {"kind": "villaBlock", "source": "one.dwg|V", "found": "2 inserts", "proposedUse": "villa blocks", "score": 2, "conflicts": 0},
             {"kind": "villaType", "source": "one.dwg|block|V", "found": "X1, X2", "proposedUse": "villa type", "score": 2, "conflicts": 0}],
    "conflicts": [],
    "checks": [{"what": "tie", "source": "plotNumber", "plot": "", "value": "one.dwg|T or one.dwg|U"}],
    "plots": [{"plot": "101", "label": "101", "centroid": "5.000, 5.000", "villaType": {"one.dwg|block|V": "X1"}},
              {"plot": "102", "label": "102", "centroid": "25.000, 5.000", "villaType": {"one.dwg|block|V": "X2"}}],
}


def test_r427_two_equal_results_count_every_field_equal_and_none_differ():
    report = scan_check.parity(RESULT, copy.deepcopy(RESULT))
    assert {name: (counts["equal"], counts["differ"], counts["sameOrder"]) for name, counts in report.items()} == {
        "inputs": (4, 0, True), "rows": (18, 0, True), "conflicts": (0, 0, True), "checks": (4, 0, True), "plots": (8, 0, True)}


def test_r427_a_parity_differ_is_counted_and_fails_the_check(tmp_path):
    other = copy.deepcopy(RESULT)
    other["plots"][1]["villaType"] = {"one.dwg|block|V": "X3"}
    other["checks"] = []
    report = scan_check.parity(RESULT, other)
    assert (report["plots"]["equal"], report["plots"]["differ"]) == (7, 1)
    assert (report["checks"]["equal"], report["checks"]["differ"]) == (0, 1)
    assert report["plots"]["differs"] == [{"item": "102", "field": "villaType", "scan": {"one.dwg|block|V": "X2"}, "scanPython": {"one.dwg|block|V": "X3"}}]
    worker, python = tmp_path / "scan.json", tmp_path / "scan_python.json"
    worker.write_text(json.dumps(RESULT), encoding="utf-8")
    python.write_text(json.dumps(other), encoding="utf-8")
    assert scan_check.main(["--scan", str(worker), "--scan-python", str(python), "--out", str(tmp_path / "check")]) == 1
    python.write_text(json.dumps(RESULT), encoding="utf-8")
    assert scan_check.main(["--scan", str(worker), "--scan-python", str(python), "--out", str(tmp_path / "check")]) == 0
    assert json.loads((tmp_path / "check" / scan_check.OUTPUT).read_text(encoding="utf-8"))["parity"]["rows"]["differ"] == 0


def test_r427_the_same_items_in_another_order_fail_the_check():
    other = copy.deepcopy(RESULT)
    other["plots"].reverse()
    report = scan_check.parity(RESULT, other)
    assert report["plots"]["differ"] == 0 and report["plots"]["sameOrder"] is False


def test_r420_the_faces_of_a_candidate_are_counted_again_by_a_polygonize_of_its_lines_and_arcs():
    # Layer A the outer rectangle and a middle line, layer B a quarter arc and a line whose chord and line close a third face on the right
    lines = {"A": [[0, 0, 20, 0], [20, 0, 20, 10], [20, 10, 0, 10], [0, 10, 0, 0], [10, 0, 10, 10]], "B": [[25, 5, 20, 10]]}
    arcs = {"B": [[20, 5, 5, -90, 0]]}
    cad = {"source": "one.dwg", "lines": lines, "arcs": arcs,
           "candidates": [{"layers": ["A"], "faces": [{}, {}]}, {"layers": ["A", "B"], "faces": [{}, {}]}]}
    assert scan_check.polygon_count(cad, ["A"]) == 2 and scan_check.polygon_count(cad, ["A", "B"]) == 3
    report = scan_check.face_counts([cad])
    assert (report["equal"], report["differ"]) == (1, 1)
    assert report["candidates"][1] == {"source": "one.dwg|A + B", "faces": 2, "polygonize": 3}


def test_r420_the_polygonize_joins_an_end_that_misses_its_corner_by_less_than_the_node_tolerance_as_the_trace_does():
    lines = {"A": [[0, 0, 10, 0], [10, 0, 10, 10], [10, 10, 0, 10], [0, 10, 0, 0.005], [5, 0.004, 5, 10]]}
    assert scan_check.polygon_count({"lines": lines, "arcs": {}}, ["A"]) == 2


def test_r420_lines_held_as_a_list_of_layer_entries_are_read_the_same():
    cad = {"lines": [{"layer": "A", "lines": [[0, 0, 1, 0], [1, 0, 1, 1], [1, 1, 0, 1], [0, 1, 0, 0]]}], "arcs": []}
    assert scan_check.polygon_count(cad, ["A"]) == 1


def test_r427_the_plots_and_villa_types_against_a_plot_table_and_the_plots_of_each_key():
    table = {"plots": [{"baseplanLabel": "101", "typologyRev9": "X1", "footprintBlock": "X1"}, {"baseplanLabel": "102", "typologyRev9": "X1", "footprintBlock": "blk_X2"},
                       {"baseplanLabel": "103", "typologyRev9": "X2", "footprintBlock": None}]}
    report = scan_check.table_check(RESULT, table, {"X1", "X2"}, "[^A-Za-z0-9]+", {"K1": {"101", "103"}})
    assert (report["plotsBoth"], report["onlyScan"], report["onlyTable"]) == (2, 0, 1)
    assert report["villaTypeSource"] == "one.dwg|block|V" and report["villaBlockSource"] == "one.dwg|block|V"
    found = report["all"]
    assert (found["plots"], found["typeEqual"], found["typeDiffer"], found["blockEqual"], found["blockDiffer"]) == (2, 1, 1, 2, 0)
    assert found["differs"] == [{"plot": "102", "scan": "X2", "table": "X1"}]
    assert (report["keys"]["K1"]["keyPlots"], report["keys"]["K1"]["inScan"], report["keys"]["K1"]["typeEqual"]) == (2, 1, 1)
    assert scan_check.table_check(RESULT, table, {"X1"}, "[^A-Za-z0-9]+", None)["keys"].startswith("UNKNOWN")


def test_d19_an_input_changed_after_its_record_fails_the_check(tmp_path):
    path = tmp_path / "table.xlsx"
    path.write_bytes(b"one")
    mark = input_guard.mark(path)
    record = tmp_path / "record.json"
    record.write_text(json.dumps({"inputs": [{"name": "table.xlsx", "path": str(path), "before": mark, "after": mark, "unchanged": True}]}), encoding="utf-8")
    assert scan_check.input_check([record]) == {"unchanged": 1, "changed": 0, "changes": []}
    path.write_bytes(b"two")
    found = scan_check.input_check([record])
    assert found["changed"] == 1 and "SHA-256" in found["changes"][0]["change"]
    worker = tmp_path / "scan.json"
    worker.write_text(json.dumps(RESULT), encoding="utf-8")
    assert scan_check.main(["--scan", str(worker), "--scan-python", str(worker), "--record", str(record), "--out", str(tmp_path / "check")]) == 1
