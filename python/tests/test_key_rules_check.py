"""Round ADDIN PR 3a: the second method of the rules on the Parsons keys (R336), on made up plots."""
import csv
import json

import openpyxl
import pytest

import key_rules_check

SQUARE = [(0.0, 0.0), (16.0, 0.0), (16.0, 25.0), (0.0, 25.0)]
FRONT_CURB = [((-50.0, -3.0), (50.0, -3.0))]


def test_r336_the_own_front_is_the_edge_within_the_limits_whose_middle_is_nearest_a_curb_with_n_into_the_plot():
    frame, rule = key_rules_check.own_frame(SQUARE, FRONT_CURB, (7.99, 18.75), 8.0, None)
    assert rule == "nearestCurb"
    assert frame["n"] == pytest.approx((0.0, 1.0)) and frame["r"] == pytest.approx((1.0, 0.0))
    assert frame["left"] == (0.0, 0.0) and frame["right"] == (16.0, 0.0)


def test_r336_a_corner_face_takes_the_road_edge_its_orientation_quadrant_bears_to_and_none_with_no_orientation():
    curbs = FRONT_CURB + [((19.0, -50.0), (19.0, 50.0))]
    frame, rule = key_rules_check.own_frame(SQUARE, curbs, (7.99, 18.75), 8.0, "EN")
    # The east edge bears 90 degrees, the south edge 180, EN takes the east edge though the south edge is nearer
    assert rule == "orientation" and frame["n"] == pytest.approx((-1.0, 0.0))
    assert key_rules_check.own_frame(SQUARE, curbs, (7.99, 18.75), 8.0, None) == (None, "noOrientation")


def test_r336_the_own_origin_runs_from_the_anchor_corner_along_the_link_axes_and_turns_with_the_mirror():
    frame = {"n": (0.0, 1.0), "r": (1.0, 0.0), "left": (0.0, 0.0), "right": (16.0, 0.0)}
    assert key_rules_check.own_origin(frame, False, {"anchor": "end", "offset": [-10.0, 1.5]}) == pytest.approx((6.0, 1.5))
    assert key_rules_check.own_origin(frame, True, {"anchor": "end", "offset": [-10.0, 1.5]}) == pytest.approx((10.0, 1.5))


def made_up(tmp_path):
    """Two parsons plots on two square faces with a curb in front, one link where the fit puts it and one 2 m along."""
    past = tmp_path / "past_all.csv"
    fields = ["key", "source", "kind", "plot", "typologyRev9", "x", "y", "rotationDeg", "mirrored"]
    with open(past, "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerow({"key": "imx", "source": "parsons", "kind": "TU", "plot": "1", "typologyRev9": "C10", "x": 6.0, "y": 1.5, "rotationDeg": 0.0, "mirrored": "False"})
        writer.writerow({"key": "imx", "source": "parsons", "kind": "TU", "plot": "2", "typologyRev9": "C10", "x": 108.0, "y": 1.5, "rotationDeg": 0.0, "mirrored": "False"})
    plots = tmp_path / "plots.csv"
    plots.write_text("locationPlanNumber,baseplanLabel\n1,11\n2,12\n", encoding="utf-8")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": [{"kind": "plot_shape", "plot": "11", "vertices": [list(point) for point in SQUARE]},
                                              {"kind": "plot_shape", "plot": "12", "vertices": [[x + 100.0, y] for x, y in SQUARE]}]}), encoding="utf-8")
    roads = tmp_path / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_text(json.dumps({"pieces": [{"kind": "line", "layer": "RD-CURB-LINE", "pts": [[-50, -3, 0], [150, -3, 0]]}]}), encoding="utf-8")
    sales = tmp_path / "sales.xlsx"
    book = openpyxl.Workbook()
    sheet = book.active
    sheet.title = "S"
    sheet.append(["Plot No", "Orientation"])
    sheet.append([1, "EN"])
    sheet.append([2, "EN"])
    book.save(sales)
    majority_json = tmp_path / "majority.json"
    decision = {"decision": "parsons linkOrigin", "plots": 2, "coded": "plotFrameRefit", "tests": {"plotFrameRefit": {"holds": 1, "breaks": 1, "cannot": 0, "byKey": {"imx": {"holds": 1, "breaks": 1}}}}}
    majority_json.write_text(json.dumps({"linkFits": {"parsons": {"C10": {"anchor": "end", "offset": [-10.0, 1.5], "plots": 2}}}, "decisions": [decision], "siteFacts": {}}), encoding="utf-8")
    overrides = tmp_path / "overrides.json"
    overrides.write_text(json.dumps({"overrides": [{"plot": "2", "field": "insert", "decision": "parsons linkOrigin"}]}), encoding="utf-8")
    settings = tmp_path / "project.json"
    real = json.loads((key_rules_check.ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    real["majority"]["check"] = {"seeds": [14, 7], "sample": 2}
    settings.write_text(json.dumps(real), encoding="utf-8")
    return ["--settings", str(settings), "--past-all", str(past), "--majority", str(majority_json), "--overrides", str(overrides), "--plots-csv", str(plots),
            "--baseplan", str(baseplan), "--roads", str(roads), "--sales", str(sales), "--out", str(tmp_path / "out" / "check.json")], tmp_path / "out" / "check.json"


def test_r336_the_check_holds_each_sampled_plot_again_and_counts_its_verdict_against_the_first_method(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {"1": "EN", "2": "EN"})
    argv, out = made_up(tmp_path)
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    for seed in ("14", "7", "every"):
        assert found["seeds"][seed]["counts"] == {"origin match": 2, "rotation match": 2}
        assert found["seeds"][seed]["mismatches"] == []
    assert found["totals"]["parsons linkOrigin"]["match"] is True


def test_r336_a_first_method_that_missed_no_plot_where_the_route_finds_a_miss_is_a_mismatch(tmp_path, monkeypatch):
    monkeypatch.setattr(key_rules_check, "sales_orientation", lambda path, sheet, column: {})
    argv, out = made_up(tmp_path)
    (tmp_path / "overrides.json").write_text(json.dumps({"overrides": []}), encoding="utf-8")
    assert key_rules_check.main(argv) == 0
    found = json.loads(out.read_text(encoding="utf-8"))
    assert found["seeds"]["14"]["counts"]["origin mismatch"] == 1
    assert found["seeds"]["14"]["mismatches"][0]["plot"] == "2" and found["seeds"]["14"]["mismatches"][0]["gapMm"] == pytest.approx(2000.0)


def test_r336_the_check_writes_nothing_inside_the_repo(tmp_path):
    argv, _ = made_up(tmp_path)
    argv[-1] = str(key_rules_check.ROOT / "check.json")
    assert key_rules_check.main(argv) == 2
    assert not (key_rules_check.ROOT / "check.json").exists()
