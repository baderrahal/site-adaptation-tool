"""R345. The parity harness: the step files of plan_from_book.py --steps and of the worker, compared by python/parity.py field by
field with the tolerance of the kind of each field, addin.parity of config/project.json, the one unit cases listed."""
import copy
import csv
import hashlib
import json
import os
import re
from pathlib import Path

import pytest

import common
import parity
import plan_from_book
import plot_relations
import project_profile

FIELDS = {"face": {"kind": "noGeometry"}, "area": {"kind": "areas", "decimals": 3}, "centroid": {"kind": "points", "decimals": 3},
          "boundsMin": {"kind": "points", "decimals": 3}, "boundsMax": {"kind": "points", "decimals": 3}}
SETTINGS = {"addin": {"parity": {"kinds": {"points": 0.000001, "levels": 0.000001, "areas": 0.000001, "angles": 0.000001, "noGeometry": None},
                                 "pagePointsPt": 0.01, "steps": {"faces": FIELDS,
                                                                 "outlines": {"outline": {"kind": "points", "decimals": 3}, "count": {"kind": "noGeometry"}}}}}}
NO_FACE = {"face": False, "area": None, "centroid": None, "boundsMin": None, "boundsMax": None}


def entry(raw, written=None):
    """A plot of a step file, its written values as a side writes them by R385 at 3 decimals unless given."""
    return {"raw": raw, "written": common.round_tree(raw) if written is None else written}


def step(side, plots, name="faces", scope="project"):
    return {"format": parity.FORMAT, "formatVersion": parity.FORMAT_VERSION, "step": name, "side": side, "scope": scope, "plots": plots}


def face(area=100.0, x=10.0, y=20.0):
    return {"face": True, "area": area, "centroid": [x, y], "boundsMin": [x - 5, y - 10], "boundsMax": [x + 5, y + 10]}


def why(report):
    return sorted((item["plot"] or "", item["field"] or "", item["why"]) for item in report["failures"])


def test_r345_the_config_names_the_tolerances_of_the_design_by_kind_of_field():
    block = project_profile.load_settings().get("addin")["parity"]
    assert block["kinds"] == {"points": 0.000001, "levels": 0.000001, "areas": 0.000001, "angles": 0.000001, "noGeometry": None}
    assert block["pagePointsPt"] == 0.01
    assert set(block["steps"]["faces"]) == set(FIELDS)
    for name, field in block["steps"]["faces"].items():
        assert field["kind"] in block["kinds"], name
        assert ("decimals" in field) == (block["kinds"][field["kind"]] is not None), name
    assert parity.tolerances(project_profile.load_settings().values)["faces"]["area"] == ("areas", 0.000001, 3)


def test_r345_equal_steps_and_values_within_the_tolerance_on_one_side_of_a_boundary_are_equal():
    python = step("python", {"1": entry(face()), "2": entry(NO_FACE)})
    csharp = step("csharp", {"1": entry(face(area=100.0 + 4e-13, x=10.0 + 1e-9)), "2": entry(NO_FACE)})
    report = parity.compare(python, csharp, parity.tolerances(SETTINGS))
    assert report["failures"] == [] and report["oneUnit"] == []
    # Every field of both plots, the four of no face equal as no value on both sides
    assert report["plots"] == 2 and report["equal"] == 10
    assert report["fields"]["area"] == {"compared": 2, "equal": 2, "oneUnit": 0, "failed": 0}


def test_r345_a_straddle_within_the_tolerance_is_one_unit_case_listed_with_both_unrounded_values():
    # 6.0005 less 0.4 micrometre and 6.0005 lie within 0.000001 and round to 6.0 and 6.001 under R385
    python, csharp = 6.0005 - 0.0000004, 6.0005
    assert common.fixed(python, 3) == 6.0 and common.fixed(csharp, 3) == 6.001
    report = parity.compare(step("python", {"7": entry(face(area=python))}), step("csharp", {"7": entry(face(area=csharp))}), parity.tolerances(SETTINGS))
    assert report["failures"] == []
    assert report["oneUnit"] == [{"step": "faces", "plot": "7", "field": "area", "index": None, "python": python, "csharp": csharp,
                                  "writtenPython": 6.0, "writtenCsharp": 6.001}]
    assert report["fields"]["area"] == {"compared": 1, "equal": 0, "oneUnit": 1, "failed": 0}


def test_r345_one_unit_is_decided_on_the_decimals_of_the_written_texts_at_site_scale():
    # Two written site coordinates one unit apart differ by 0.0010000001639 as binary numbers, so the decision is on their decimals
    python, csharp = 2361234.1244999, 2361234.1245001
    assert abs(2361234.125 - 2361234.124) != 0.001

    def at(x):
        return {"face": True, "area": 100.0, "centroid": [x, 20.0], "boundsMin": [0.0, 0.0], "boundsMax": [1.0, 1.0]}

    report = parity.compare(step("python", {"7": entry(at(python))}), step("csharp", {"7": entry(at(csharp))}), parity.tolerances(SETTINGS))
    assert report["failures"] == []
    assert [(item["field"], item["index"], item["writtenPython"], item["writtenCsharp"]) for item in report["oneUnit"]] == [("centroid", [0], 2361234.124, 2361234.125)]
    # Two units apart is no straddle and fails after rounding
    far = entry(at(csharp), written=dict(common.round_tree(at(csharp)), centroid=[2361234.126, 20.0]))
    report = parity.compare(step("python", {"7": entry(at(python))}), step("csharp", {"7": far}), parity.tolerances(SETTINGS))
    assert why(report) == [("7", "centroid", "after rounding")]


def test_r345_a_gap_past_the_tolerance_fails_even_when_both_round_to_one_value():
    report = parity.compare(step("python", {"1": entry(face(x=10.0))}), step("csharp", {"1": entry(face(x=10.0001))}), parity.tolerances(SETTINGS))
    assert [(item["plot"], item["field"], item["index"], item["why"]) for item in report["failures"]] == [("1", "centroid", [0], "before rounding"),
                                                                                                        ("1", "boundsMin", [0], "before rounding"),
                                                                                                        ("1", "boundsMax", [0], "before rounding")]
    assert report["oneUnit"] == []


def test_r345_a_written_value_off_by_more_than_the_straddle_allows_fails_after_rounding():
    python = entry(face())
    csharp = entry(face())
    csharp["written"]["area"] = 100.002
    report = parity.compare(step("python", {"1": python}), step("csharp", {"1": csharp}), parity.tolerances(SETTINGS))
    assert [(item["field"], item["why"]) for item in report["failures"]] == [("area", "after rounding")]
    # A written value that is not at the decimals of its field is no value R385 wrote
    csharp["written"]["area"] = 100.0001
    report = parity.compare(step("python", {"1": python}), step("csharp", {"1": csharp}), parity.tolerances(SETTINGS))
    assert [(item["field"], item["why"]) for item in report["failures"]] == [("area", "written value not at 3 decimals")]


def test_r345_no_geometry_is_exact_and_a_plot_or_a_value_on_one_side_only_fails():
    python = step("python", {"1": entry(face()), "2": entry(NO_FACE), "3": entry(face())})
    csharp = step("csharp", {"1": entry(NO_FACE), "2": entry(NO_FACE), "4": entry(face())})
    report = parity.compare(python, csharp, parity.tolerances(SETTINGS))
    whys = why(report)
    assert ("1", "face", "not equal") in whys and ("1", "area", "one side has no value") in whys
    assert ("3", "", "only in python") in whys and ("4", "", "only in csharp") in whys
    assert report["plots"] == 4


def test_r345_every_field_of_either_side_is_compared_a_field_with_no_tolerance_or_a_configured_field_missing_fails():
    extra = dict(face(), units=2)
    python = step("python", {"1": entry(extra), "2": entry({"face": False})})
    csharp = step("csharp", {"1": entry(extra), "2": entry({"face": False, "area": None})})
    report = parity.compare(python, csharp, parity.tolerances(SETTINGS))
    whys = why(report)
    # units is in both files and in no addin.parity step, so it has no tolerance
    assert ("1", "units", "no tolerance in addin.parity") in whys
    # Plot 2 lacks the configured fields, on both sides or on one
    assert ("2", "area", "missing on python") in whys
    assert ("2", "centroid", "missing on both sides") in whys and ("2", "boundsMin", "missing on both sides") in whys
    assert report["fields"]["units"]["failed"] == 1


def test_r345_no_plot_on_either_side_fails_with_the_step_and_the_scopes():
    report = parity.compare(step("python", {}, scope="nbh=999"), step("csharp", {}, scope="nbh=999"), parity.tolerances(SETTINGS))
    assert [(item["step"], item["why"]) for item in report["failures"]] == [("faces", "no plot on either side, scope python nbh=999, csharp nbh=999")]


def test_r345_types_and_shapes_must_match_and_lists_of_points_are_compared_point_by_point():
    rules = parity.tolerances(SETTINGS)
    outline = [[2361234.1, 512345.2], [2361240.1, 512345.2], [2361240.1, 512350.2]]
    near = [[x + 1e-7, y] for x, y in outline]
    report = parity.compare(step("python", {"1": entry({"outline": outline, "count": 3})}, "outlines"),
                            step("csharp", {"1": entry({"outline": near, "count": 3})}, "outlines"), rules)
    assert report["failures"] == [] and report["fields"]["outline"]["equal"] == 1
    off = [list(point) for point in near]
    off[1][0] += 0.01
    report = parity.compare(step("python", {"1": entry({"outline": outline, "count": 3})}, "outlines"),
                            step("csharp", {"1": entry({"outline": off, "count": 3})}, "outlines"), rules)
    assert [(item["field"], item["index"], item["why"]) for item in report["failures"]] == [("outline", [1, 0], "before rounding")]
    # A number against a list of one number, a list of points against a flat list, True against 1 and 3 against 3.0
    cases = [({"outline": 5.0, "count": 3}, {"outline": [5.0], "count": 3}, "outline", "not the same shape"),
             ({"outline": outline, "count": 3}, {"outline": [x for point in outline for x in point], "count": 3}, "outline", "not the same shape"),
             ({"outline": outline, "count": True}, {"outline": outline, "count": 1}, "count", "not equal"),
             ({"outline": outline, "count": 3}, {"outline": outline, "count": 3.0}, "count", "not equal"),
             ({"outline": outline, "count": 3}, {"outline": [[True, 1.0]], "count": 3}, "outline", "not a number or a list of numbers")]
    for mine, theirs, field, reason in cases:
        report = parity.compare(step("python", {"1": entry(mine)}, "outlines"), step("csharp", {"1": entry(theirs)}, "outlines"), rules)
        assert [(item["field"], item["why"]) for item in report["failures"]] == [(field, reason)], (mine, theirs)


def test_r345_steps_of_another_name_format_or_side_are_refused():
    with pytest.raises(ValueError, match="faces and levels"):
        parity.compare(step("python", {}), step("csharp", {}, name="levels"), parity.tolerances(SETTINGS))
    other = step("csharp", {})
    other["formatVersion"] = 1
    with pytest.raises(ValueError, match="formatVersion"):
        parity.compare(step("python", {}), other, parity.tolerances(SETTINGS))
    with pytest.raises(ValueError, match="no step walls"):
        parity.compare(step("python", {}, name="walls"), step("csharp", {}, name="walls"), parity.tolerances(SETTINGS))
    # The same side twice, or the sides swapped, is no parity
    with pytest.raises(ValueError, match="the python step file holds the side csharp"):
        parity.compare(step("csharp", {}), step("csharp", {}), parity.tolerances(SETTINGS))
    with pytest.raises(ValueError, match="the csharp step file holds the side python"):
        parity.compare(step("python", {}), step("python", {}), parity.tolerances(SETTINGS))


def write_csv(path, header, rows, bom=False):
    with open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)
    return path


def book(tmp_path, bom=False, plots=None):
    """A made up baseplan of three faces, label 2 drawn as two squares, the data book rows and the location plan labels."""
    plots = plots or {"plots": [
        {"kind": "plot_shape", "plot": "1", "vertices": [[0, 0], [10, 0], [10, 20], [0, 20]]},
        {"kind": "plot_shape", "plot": "02", "vertices": [[20, 0], [30, 0], [30, 10], [20, 10]]},
        {"kind": "plot_shape", "plot": "2", "vertices": [[40, 0], [50, 0], [50, 10], [40, 10]]},
        {"kind": "plot_shape", "plot": "3a", "vertices": [[0, 30], [10, 30], [10, 40]]},
        {"kind": "plot_shape", "plot": "4", "vertices": [[0, 50], [10, 50]]},
        {"kind": "polyline", "plot": "5", "vertices": [[0, 60], [10, 60], [10, 70]]}]}
    (tmp_path / "plots.json").write_text(json.dumps(plots), encoding="utf-8")
    # R388. The layers.json of the read beside it gives the units of the drawing
    (tmp_path / "layers.json").write_text(json.dumps({"units": "Meters"}), encoding="utf-8")
    write_csv(tmp_path / "plots.csv", ["baseplanLabel", "locationPlanNumber", "typologyRev9"], [["1", "101", "DP1"], ["2", "102", "DP1"], ["", "103", "TH1"]], bom)
    write_csv(tmp_path / "labels.csv", ["number", "code", "kind", "nbh"], [["101", "DP1", "clientCode", "012"], ["102", "DP1", "clientCode", "012"],
                                                                           ["103", "TH1", "clientCode", ""], ["104", "x", "number", "012"]], bom)
    return tmp_path


def run_steps(tmp_path, cover, extra=(), bom=False, plots=None):
    folder = book(tmp_path, bom, plots)
    argv = [cover, "--labels", str(folder / "labels.csv"), "--plots-csv", str(folder / "plots.csv"), "--baseplan", str(folder / "plots.json"),
            "--landxml", "x", "--site", "s.json", "--roads", "r", "--scratch", str(tmp_path), "--out", str(tmp_path / "plan.json"),
            "--steps", str(tmp_path / "steps"), "--steps-only"] + list(extra)
    return plan_from_book.main(argv)


def python_step(tmp_path):
    return json.loads((tmp_path / "steps" / "step_faces_python.json").read_text(encoding="utf-8"))


def test_r345_plan_from_book_steps_writes_the_face_of_every_plot_of_the_cover_raw_and_written(tmp_path, capsys):
    assert run_steps(tmp_path, "all") == 0, capsys.readouterr().err
    found = python_step(tmp_path)
    assert (found["format"], found["formatVersion"], found["step"], found["side"], found["scope"]) == (parity.FORMAT, parity.FORMAT_VERSION, "faces", "python", "project")
    assert sorted(found["plots"]) == ["101", "102", "103"]
    first = found["plots"]["101"]
    assert first["raw"] == {"face": True, "area": 200.0, "centroid": [5.0, 10.0], "boundsMin": [0.0, 0.0], "boundsMax": [10.0, 20.0]}
    assert first["written"] == first["raw"]
    # Label 2 is drawn twice, 02 and 2, two squares joined into one face as the build joins them
    assert found["plots"]["102"]["raw"]["area"] == 200.0 and found["plots"]["102"]["raw"]["centroid"] == [35.0, 5.0]
    assert found["plots"]["103"] == {"raw": NO_FACE, "written": NO_FACE}
    assert not (tmp_path / "plan.json").exists()
    # The inputs the step was made from, with their SHA-256
    roles = {item["role"]: item for item in found["run"]["inputs"]}
    assert sorted(roles) == ["baseplanPlots", "config", "labels", "plotsCsv"]
    assert roles["plotsCsv"]["sha256"] == hashlib.sha256((tmp_path / "plots.csv").read_bytes()).hexdigest()


def test_r345_a_cover_of_one_neighbourhood_writes_its_plots_only(tmp_path):
    assert run_steps(tmp_path, "nbh=012") == 0
    found = python_step(tmp_path)
    assert sorted(found["plots"]) == ["101", "102"] and found["scope"] == "nbh=012"


def test_r345_the_python_side_writes_what_the_plan_writes_so_a_1_mm_tie_fails(tmp_path, monkeypatch):
    # A face of area 2.0465, a tie R385 writes 2.047 and round() 2.046. A plan whose writer rounded by round() would write 2.046,
    # and the step must hold what the plan writes, not a value recomputed from raw
    plots = {"plots": [{"kind": "plot_shape", "plot": "1", "vertices": [[0, 0], [2.0465, 0], [2.0465, 1], [0, 1]]}]}

    def round_tree(node, wide_keys=frozenset()):
        if isinstance(node, dict):
            return {key: round_tree(item) for key, item in node.items()}
        if isinstance(node, list):
            return [round_tree(item) for item in node]
        return round(node, 3) if isinstance(node, float) else node

    monkeypatch.setattr(common, "round_tree", round_tree)
    assert run_steps(tmp_path, "all", plots=plots) == 0
    python = python_step(tmp_path)
    assert python["plots"]["101"]["raw"]["area"] == 2.0465 and python["plots"]["101"]["written"]["area"] == 2.046
    monkeypatch.undo()
    csharp = json.loads(json.dumps(python))
    csharp["side"] = "csharp"
    csharp["plots"]["101"]["written"] = common.round_tree(csharp["plots"]["101"]["raw"])
    assert csharp["plots"]["101"]["written"]["area"] == 2.047
    report = parity.compare(python, csharp, parity.tolerances(project_profile.load_settings().values))
    # The area and the east bound, 2.0465 both
    assert [(item["plot"], item["field"], item["why"]) for item in report["failures"]] == [("101", "area", "after rounding"), ("101", "boundsMax", "after rounding")]


def test_r345_plan_from_book_reads_the_csv_files_of_the_data_book_with_a_byte_order_mark(tmp_path):
    # Excel writes UTF-8 with a byte order mark, csv.DictReader with utf-8 keeps it on the first header
    assert run_steps(tmp_path, "all", bom=True) == 0
    assert (tmp_path / "plots.csv").read_bytes()[:3] == b"\xef\xbb\xbf"
    found = python_step(tmp_path)
    assert sorted(found["plots"]) == ["101", "102", "103"] and found["plots"]["101"]["raw"]["face"] is True


def test_r345_the_python_step_and_a_made_up_csharp_step_of_the_same_faces_hold_parity(tmp_path):
    assert run_steps(tmp_path, "all") == 0
    python = python_step(tmp_path)
    csharp = json.loads(json.dumps(python))
    csharp["side"] = "csharp"
    csharp["plots"]["101"]["raw"]["area"] += 2e-10
    out = tmp_path / "parity"
    code = parity.main(["--python", str(tmp_path / "steps" / "step_faces_python.json"), "--csharp", str(write(tmp_path / "step_faces_csharp.json", csharp)), "--out", str(out)])
    report = json.loads((out / "parity_faces.json").read_text(encoding="utf-8"))
    assert code == 0 and report["failures"] == [] and report["plots"] == 3


def test_r345_parity_refuses_one_file_twice_a_file_of_the_other_side_and_inputs_that_changed_or_differ(tmp_path):
    assert run_steps(tmp_path, "all") == 0
    path = tmp_path / "steps" / "step_faces_python.json"
    python = python_step(tmp_path)
    out = str(tmp_path / "parity")
    with pytest.raises(ValueError, match="one file"):
        parity.main(["--python", str(path), "--csharp", str(path), "--out", out])
    with pytest.raises(ValueError, match="the csharp step file holds the side python"):
        parity.main(["--python", str(path), "--csharp", str(write(tmp_path / "copy.json", python)), "--out", out])
    csharp = dict(python, side="csharp")
    other = write(tmp_path / "step_faces_csharp.json", csharp)
    # An input the C# step names with another SHA-256 than the Python step is not the same input
    csharp["run"] = {"runId": "r1", "inputs": [dict(item, sha256="0" * 64) if item["role"] == "labels" else item for item in python["run"]["inputs"]]}
    write(other, csharp)
    with pytest.raises(ValueError, match="labels"):
        parity.main(["--python", str(path), "--csharp", str(other), "--out", out])
    # A step file with no run stamp names no input to check
    write(other, {key: value for key, value in csharp.items() if key != "run"})
    with pytest.raises(ValueError, match="no run stamp"):
        parity.main(["--python", str(path), "--csharp", str(other), "--out", out])
    # An input changed since the step files were written
    write(other, dict(python, side="csharp"))
    assert parity.main(["--python", str(path), "--csharp", str(other), "--out", out]) == 0
    with open(tmp_path / "plots.csv", "a", encoding="utf-8") as handle:
        handle.write("9,109,DP1\n")
    with pytest.raises(ValueError, match="plotsCsv .* changed since"):
        parity.main(["--python", str(path), "--csharp", str(other), "--out", out])


def write(path, payload):
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def test_r345_parity_refuses_an_output_folder_inside_the_repo(tmp_path):
    payload = write(tmp_path / "a.json", step("python", {}))
    with pytest.raises(ValueError, match="inside the repo"):
        parity.main(["--python", str(payload), "--csharp", str(payload), "--out", str(Path(parity.__file__).resolve().parent / "parity_out")])


def test_r345_a_one_unit_case_is_listed_only_when_each_written_value_is_the_r385_rounding_of_its_own_raw():
    # Raws 1E-09 apart across the tie 1.0005, which R385 rounds to 1.0 and 1.001
    a, b = 1.0004999995, 1.0005000005
    assert (common.fixed(a, 3), common.fixed(b, 3)) == (1.0, 1.001)
    rules = parity.tolerances(SETTINGS)

    def pair(written_a, written_b):
        return parity.compare(step("python", {"1": entry(face(area=a), dict(common.round_tree(face(area=a)), area=written_a))}),
                              step("csharp", {"1": entry(face(area=b), dict(common.round_tree(face(area=b)), area=written_b))}), rules)

    report = pair(1.0, 1.001)
    assert report["failures"] == [] and [(item["writtenPython"], item["writtenCsharp"]) for item in report["oneUnit"]] == [(1.0, 1.001)]
    # The C# rounded the wrong way at the boundary, 0.999 for 1.0005000005, or both sides wrote the rounding of the other side
    for written_a, written_b, side in ((1.0, 0.999, "csharp"), (1.001, 1.0, "python and csharp")):
        report = pair(written_a, written_b)
        assert report["oneUnit"] == [], (written_a, written_b)
        assert [(item["field"], item["why"]) for item in report["failures"]] == [("area", "one unit apart and not the R385 rounding of its raw on " + side)]


def test_r345_a_raw_or_written_value_that_is_not_finite_fails():
    rules = parity.tolerances(SETTINGS)
    nan, inf = float("nan"), float("inf")
    written = common.round_tree(face())
    cases = [(face(area=nan), face(), "area", None), (face(area=inf), face(area=inf), "area", None), (face(x=nan), face(), "centroid", [0])]
    for mine, theirs, field, index in cases:
        report = parity.compare(step("python", {"1": entry(mine, written)}), step("csharp", {"1": entry(theirs, written)}), rules)
        assert [(item["field"], item["index"], item["why"]) for item in report["failures"]][:1] == [(field, index, "not a finite number")], (mine, theirs)
        assert report["oneUnit"] == []
    report = parity.compare(step("python", {"1": entry(face(), dict(written, area=nan))}), step("csharp", {"1": entry(face(), dict(written, area=nan))}), rules)
    assert [(item["field"], item["why"]) for item in report["failures"]] == [("area", "not a finite number")]


def test_r345_a_key_held_only_under_written_or_beside_raw_and_written_is_compared_and_fails():
    rules = parity.tolerances(SETTINGS)
    csharp = entry(face())
    csharp["written"]["extra"] = 123.456
    report = parity.compare(step("python", {"1": entry(face())}), step("csharp", {"1": csharp}), rules)
    assert why(report) == [("1", "extra", "no tolerance in addin.parity")]
    csharp = entry(face())
    del csharp["raw"]["area"]
    report = parity.compare(step("python", {"1": entry(face())}), step("csharp", {"1": csharp}), rules)
    assert why(report) == [("1", "area", "missing on csharp")]
    csharp = dict(entry(face()), other={"x": 1})
    report = parity.compare(step("python", {"1": entry(face())}), step("csharp", {"1": csharp}), rules)
    assert why(report) == [("1", "other", "a key of the plot beside raw and written")]


def test_r345_a_run_stamp_that_names_no_input_is_refused():
    payload = dict(step("python", {}), run={"runId": "r1", "inputs": []})
    assert "names no input" in parity.stamp_refusal(payload, "python")


def test_r345_a_tolerance_is_a_finite_number_of_zero_or_more_or_null_for_an_exact_kind():
    for bad in (True, False, float("nan"), float("inf"), -0.000001, "0.000001", [0.1]):
        settings = copy.deepcopy(SETTINGS)
        settings["addin"]["parity"]["kinds"]["areas"] = bad
        with pytest.raises(project_profile.ProfileError, match="finite number of zero or more"):
            parity.tolerances(settings)
    settings = copy.deepcopy(SETTINGS)
    settings["addin"]["parity"]["kinds"]["areas"] = 0
    assert parity.tolerances(settings)["faces"]["area"] == ("areas", 0, 3)


def test_r345_a_python_run_that_fails_leaves_no_step_file_and_parity_fails_as_missing(tmp_path, monkeypatch):
    assert run_steps(tmp_path, "all") == 0
    path = tmp_path / "steps" / "step_faces_python.json"
    csharp = write(tmp_path / "step_faces_csharp.json", dict(python_step(tmp_path), side="csharp"))
    out = str(tmp_path / "parity")
    assert parity.main(["--python", str(path), "--csharp", str(csharp), "--out", out]) == 0

    # The same inputs, so the earlier step file would still hash the same, and a run that fails before it writes the step
    def broken(baseplan, buffer):
        raise ValueError("a made up failure of the build")

    monkeypatch.setattr(plan_from_book.yard_rule, "face_shapes", broken)
    assert run_steps(tmp_path, "all") == 2
    assert not path.exists()
    with pytest.raises(ValueError, match="python step file .* is not on disk"):
        parity.main(["--python", str(path), "--csharp", str(csharp), "--out", out])


def test_r345_the_run_stamp_names_its_run_id_the_code_that_wrote_it_and_its_config_and_parity_prints_both(tmp_path, capsys):
    assert run_steps(tmp_path, "all") == 0
    run = python_step(tmp_path)["run"]
    assert re.fullmatch(r"python_[0-9a-f]{12}", run["runId"])
    assert re.fullmatch(r"python ([0-9a-f]{40}( with changes)?|commit unknown)", run["code"])
    assert run["configSha256"] == hashlib.sha256(project_profile.SETTINGS_PATH.read_bytes()).hexdigest()
    first = run["runId"]
    assert run_steps(tmp_path, "all") == 0
    run = python_step(tmp_path)["run"]
    assert run["runId"] != first
    csharp = dict(python_step(tmp_path), side="csharp", run=dict(run, runId="r9", code="SiteAdaptation.Common 1.0.0+abc"))
    capsys.readouterr()
    assert parity.main(["--python", str(tmp_path / "steps" / "step_faces_python.json"), "--csharp", str(write(tmp_path / "c.json", csharp)),
                        "--out", str(tmp_path / "parity")]) == 0
    printed = capsys.readouterr().out
    assert "python run %s, code %s, config %s" % (run["runId"], run["code"], run["configSha256"]) in printed
    assert "csharp run r9, code SiteAdaptation.Common 1.0.0+abc, config %s" % run["configSha256"] in printed


def make_link(link, target):
    """A junction on Windows, which needs no right to make, and a symbolic link elsewhere."""
    if os.name == "nt":
        import _winapi
        _winapi.CreateJunction(str(target), str(link))
    else:
        os.symlink(target, link, target_is_directory=True)


def test_r345_a_folder_inside_any_git_tree_a_worktree_or_reached_through_a_junction_into_one_is_refused(tmp_path):
    repo = tmp_path / "repo"
    (repo / ".git").mkdir(parents=True)
    (repo / "sub").mkdir()
    worktree = tmp_path / "wt"
    worktree.mkdir()
    (worktree / ".git").write_text("gitdir: C:/elsewhere/.git/worktrees/wt\n", encoding="utf-8")
    make_link(tmp_path / "link", repo / "sub")
    for folder in (repo / "sub" / "out", worktree / "out", tmp_path / "link" / "out", tmp_path / "link"):
        with pytest.raises(ValueError, match="inside the repo or a git work tree"):
            plot_relations.refuse_inside_repo(folder)
    plot_relations.refuse_inside_repo(tmp_path / "plain" / "out")
    with pytest.raises(ValueError, match="inside the repo or a git work tree"):
        parity.write_step(tmp_path / "link" / "steps", "faces", "python", "project", {}, SETTINGS, [])
    assert not (repo / "sub" / "steps").exists()
