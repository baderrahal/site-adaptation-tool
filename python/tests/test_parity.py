"""R345. The parity harness: the step files of plan_from_book.py --steps and of the worker, compared by python/parity.py field by
field with the tolerance of the kind of each field, addin.parity of config/project.json, the one unit cases listed."""
import csv
import json
from pathlib import Path

import pytest

import common
import parity
import plan_from_book
import project_profile

FIELDS = {"face": {"kind": "noGeometry"}, "area": {"kind": "areas", "decimals": 3}, "centroid": {"kind": "points", "decimals": 3},
          "boundsMin": {"kind": "points", "decimals": 3}, "boundsMax": {"kind": "points", "decimals": 3}}
SETTINGS = {"addin": {"parity": {"kinds": {"points": 0.000001, "levels": 0.000001, "areas": 0.000001, "angles": 0.000001, "noGeometry": None},
                                 "pagePointsPt": 0.01, "steps": {"faces": FIELDS}}}}


def entry(raw, fields=FIELDS):
    return {"raw": raw, "written": parity.written(raw, fields)}


def step(side, plots, name="faces"):
    return {"format": parity.FORMAT, "formatVersion": parity.FORMAT_VERSION, "step": name, "side": side, "plots": plots}


def face(area=100.0, x=10.0, y=20.0):
    return {"face": True, "area": area, "centroid": [x, y], "boundsMin": [x - 5, y - 10], "boundsMax": [x + 5, y + 10]}


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
    python = step("python", {"1": entry(face()), "2": entry({"face": False})})
    csharp = step("csharp", {"1": entry(face(area=100.0 + 4e-13, x=10.0 + 1e-9)), "2": entry({"face": False})})
    report = parity.compare(python, csharp, parity.tolerances(SETTINGS))
    assert report["failures"] == [] and report["oneUnit"] == []
    assert report["plots"] == 2 and report["equal"] == 6
    assert report["fields"]["area"] == {"compared": 1, "equal": 1, "oneUnit": 0, "failed": 0}


def test_r345_a_straddle_within_the_tolerance_is_one_unit_case_listed_with_both_unrounded_values():
    # 6.0005 less 0.4 micrometre and 6.0005 lie within 0.000001 and round to 6.0 and 6.001 under R385
    python, csharp = 6.0005 - 0.0000004, 6.0005
    assert common.fixed(python, 3) == 6.0 and common.fixed(csharp, 3) == 6.001
    report = parity.compare(step("python", {"7": entry(face(area=python))}), step("csharp", {"7": entry(face(area=csharp))}), parity.tolerances(SETTINGS))
    assert report["failures"] == []
    assert report["oneUnit"] == [{"step": "faces", "plot": "7", "field": "area", "index": None, "python": python, "csharp": csharp,
                                  "writtenPython": 6.0, "writtenCsharp": 6.001}]
    assert report["fields"]["area"] == {"compared": 1, "equal": 0, "oneUnit": 1, "failed": 0}


def test_r345_a_gap_past_the_tolerance_fails_even_when_both_round_to_one_value():
    report = parity.compare(step("python", {"1": entry(face(x=10.0))}), step("csharp", {"1": entry(face(x=10.0001))}), parity.tolerances(SETTINGS))
    assert [(item["plot"], item["field"], item["index"], item["why"]) for item in report["failures"]] == [("1", "centroid", 0, "before rounding"),
                                                                                                        ("1", "boundsMin", 0, "before rounding"),
                                                                                                        ("1", "boundsMax", 0, "before rounding")]
    assert report["oneUnit"] == []


def test_r345_a_written_value_off_by_more_than_the_straddle_allows_fails_after_rounding():
    python = entry(face())
    csharp = entry(face())
    csharp["written"]["area"] = 100.001
    report = parity.compare(step("python", {"1": python}), step("csharp", {"1": csharp}), parity.tolerances(SETTINGS))
    assert [(item["field"], item["why"]) for item in report["failures"]] == [("area", "after rounding")]


def test_r345_no_geometry_is_exact_and_a_plot_or_a_value_on_one_side_only_fails():
    python = step("python", {"1": entry(face()), "2": entry({"face": False}), "3": entry(face())})
    csharp = step("csharp", {"1": entry({"face": False}), "2": entry({"face": False}), "4": entry(face())})
    report = parity.compare(python, csharp, parity.tolerances(SETTINGS))
    whys = sorted((item["plot"], item["field"] or "", item["why"]) for item in report["failures"])
    assert ("1", "face", "not equal") in whys and ("1", "area", "one side has no value") in whys
    assert ("3", "", "only in python") in whys and ("4", "", "only in csharp") in whys
    assert report["plots"] == 4


def test_r345_steps_of_another_name_or_format_are_refused():
    with pytest.raises(ValueError, match="faces and levels"):
        parity.compare(step("python", {}), step("csharp", {}, name="levels"), parity.tolerances(SETTINGS))
    other = step("csharp", {})
    other["formatVersion"] = 2
    with pytest.raises(ValueError, match="formatVersion"):
        parity.compare(step("python", {}), other, parity.tolerances(SETTINGS))
    with pytest.raises(ValueError, match="no step walls"):
        parity.compare(step("python", {}, name="walls"), step("csharp", {}, name="walls"), parity.tolerances(SETTINGS))


def test_r345_the_written_values_of_a_step_are_rounded_by_r385_with_the_decimals_of_each_field():
    assert parity.written({"face": True, "area": 2.0465, "centroid": [0.7 + 0.0005, -0.0004]}, FIELDS) == {"face": True, "area": 2.047, "centroid": [0.701, 0]}
    assert parity.written({"face": False}, FIELDS) == {"face": False}


def write_csv(path, header, rows):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(header)
        writer.writerows(rows)
    return path


def book(tmp_path):
    """A made up baseplan of three faces, label 2 drawn as two squares, the data book rows and the location plan labels."""
    plots = {"plots": [
        {"kind": "plot_shape", "plot": "1", "vertices": [[0, 0], [10, 0], [10, 20], [0, 20]]},
        {"kind": "plot_shape", "plot": "02", "vertices": [[20, 0], [30, 0], [30, 10], [20, 10]]},
        {"kind": "plot_shape", "plot": "2", "vertices": [[40, 0], [50, 0], [50, 10], [40, 10]]},
        {"kind": "plot_shape", "plot": "3a", "vertices": [[0, 30], [10, 30], [10, 40]]},
        {"kind": "plot_shape", "plot": "4", "vertices": [[0, 50], [10, 50]]},
        {"kind": "polyline", "plot": "5", "vertices": [[0, 60], [10, 60], [10, 70]]}]}
    (tmp_path / "plots.json").write_text(json.dumps(plots), encoding="utf-8")
    write_csv(tmp_path / "plots.csv", ["baseplanLabel", "locationPlanNumber", "typologyRev9"], [["1", "101", "DP1"], ["2", "102", "DP1"], ["", "103", "TH1"]])
    write_csv(tmp_path / "labels.csv", ["number", "code", "kind", "nbh"], [["101", "DP1", "clientCode", "012"], ["102", "DP1", "clientCode", "012"],
                                                                           ["103", "TH1", "clientCode", ""], ["104", "x", "number", "012"]])
    return tmp_path


def run_steps(tmp_path, cover, extra=()):
    folder = book(tmp_path)
    argv = [cover, "--labels", str(folder / "labels.csv"), "--plots-csv", str(folder / "plots.csv"), "--baseplan", str(folder / "plots.json"),
            "--landxml", "x", "--site", "s.json", "--roads", "r", "--scratch", str(tmp_path), "--out", str(tmp_path / "plan.json"),
            "--steps", str(tmp_path / "steps"), "--steps-only"] + list(extra)
    return plan_from_book.main(argv)


def test_r345_plan_from_book_steps_writes_the_face_of_every_plot_of_the_cover_raw_and_written(tmp_path, capsys):
    assert run_steps(tmp_path, "all") == 0, capsys.readouterr().err
    found = json.loads((tmp_path / "steps" / "step_faces.json").read_text(encoding="utf-8"))
    assert (found["format"], found["formatVersion"], found["step"], found["side"]) == (parity.FORMAT, parity.FORMAT_VERSION, "faces", "python")
    assert sorted(found["plots"]) == ["101", "102", "103"]
    first = found["plots"]["101"]
    assert first["raw"] == {"face": True, "area": 200.0, "centroid": [5.0, 10.0], "boundsMin": [0.0, 0.0], "boundsMax": [10.0, 20.0]}
    assert first["written"] == first["raw"]
    # Label 2 is drawn twice, 02 and 2, two squares joined into one face as the build joins them
    assert found["plots"]["102"]["raw"]["area"] == 200.0 and found["plots"]["102"]["raw"]["centroid"] == [35.0, 5.0]
    assert found["plots"]["103"] == {"raw": {"face": False}, "written": {"face": False}}
    assert not (tmp_path / "plan.json").exists()


def test_r345_a_cover_of_one_neighbourhood_writes_its_plots_only(tmp_path):
    assert run_steps(tmp_path, "nbh=012") == 0
    assert sorted(json.loads((tmp_path / "steps" / "step_faces.json").read_text(encoding="utf-8"))["plots"]) == ["101", "102"]


def test_r345_the_python_step_and_a_made_up_csharp_step_of_the_same_faces_hold_parity(tmp_path):
    assert run_steps(tmp_path, "all") == 0
    python = json.loads((tmp_path / "steps" / "step_faces.json").read_text(encoding="utf-8"))
    csharp = json.loads(json.dumps(python))
    csharp["side"] = "csharp"
    csharp["plots"]["101"]["raw"]["area"] += 2e-10
    out = tmp_path / "parity"
    code = parity.main(["--python", str(tmp_path / "steps" / "step_faces.json"), "--csharp", str(write(tmp_path / "cs.json", csharp)), "--out", str(out)])
    report = json.loads((out / "parity_faces.json").read_text(encoding="utf-8"))
    assert code == 0 and report["failures"] == [] and report["plots"] == 3


def write(path, payload):
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def test_r345_parity_refuses_an_output_folder_inside_the_repo(tmp_path):
    payload = write(tmp_path / "a.json", step("python", {}))
    with pytest.raises(ValueError, match="inside the repo"):
        parity.main(["--python", str(payload), "--csharp", str(payload), "--out", str(Path(parity.__file__).resolve().parent / "parity_out")])
