"""R348, R350. The settings of the reads of button 2 live in config/project.json, dataBook there, where Phase 4c moved them from
python/c3d_probe.json: the curb, ROW and setback layers of dataBook.join, the two Rev 9 workbooks with the DLUS and sales sheets of
dataBook.rev9, the area key sheets of dataBook.data5.areaKey and the sales sheet of dataBook.locationPlan. c3d_run.load_config lays
them over dataBook of the probe settings, so every reader of the data book keeps its key path, and refuses a key both files hold."""
import json
from pathlib import Path

import pytest

import c3d_run
import project_profile

ROOT = Path(__file__).resolve().parents[2]
MOVED = {
    "join": ("curb", "row", "footprint"),
    "rev9": ("workbooks", "dlus", "sales"),
    "locationPlan": ("sales",),
}
MOVED_AREA_KEY = ("dlus", "sales")


def probe():
    return json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))


def test_r350_the_read_settings_live_in_config_and_no_longer_in_the_probe_settings():
    book = project_profile.load_settings().get("dataBook")
    old = probe()["dataBook"]
    for block, keys in MOVED.items():
        for key in keys:
            assert key in book[block], "config/project.json dataBook.%s.%s" % (block, key)
            assert key not in old.get(block, {}), "python/c3d_probe.json still holds dataBook.%s.%s" % (block, key)
    for key in MOVED_AREA_KEY:
        assert key in book["data5"]["areaKey"]
        assert key not in old["data5"]["areaKey"]
    # What the reads of 4c take: the layers of the curbs, the ROW and the setbacks, the sheets of the DLUS and sales workbooks
    assert book["join"]["curb"]["layers"] and book["join"]["row"]["layers"] and book["join"]["footprint"]["setbackLayer"]
    assert {entry["workbook"] for entry in (book["rev9"]["dlus"], book["rev9"]["sales"], book["data5"]["areaKey"]["dlus"],
                                            book["data5"]["areaKey"]["sales"])} <= set(book["rev9"]["workbooks"])


def test_r350_load_config_lays_the_moved_settings_over_the_probe_settings_and_keeps_the_rest():
    merged = c3d_run.load_config()["dataBook"]
    book = project_profile.load_settings().get("dataBook")
    old = probe()["dataBook"]
    for block, keys in MOVED.items():
        for key in keys:
            assert merged[block][key] == book[block][key]
    assert merged["data5"]["areaKey"]["dlus"] == book["data5"]["areaKey"]["dlus"]
    # The keys of the probe settings that did not move stay as they are
    assert merged["join"]["cutsheet"] == old["join"]["cutsheet"]
    assert merged["rev9"]["drawing"] == old["rev9"]["drawing"]
    assert merged["data5"]["areaKey"]["outFolder"] == old["data5"]["areaKey"]["outFolder"]
    # R351. The rest of the location plan settings moved with Round ADDIN 4d
    assert merged["locationPlan"]["pdf"] == book["locationPlan"]["pdf"] and "locationPlan" not in old
    assert "note" not in merged


def test_r350_a_key_both_files_hold_is_refused_with_its_path():
    with pytest.raises(ValueError, match="dataBook.rev9.sales"):
        c3d_run.lay_over({"rev9": {"sales": {"sheet": "a"}}}, {"rev9": {"sales": {"sheet": "b"}}}, "dataBook")
    with pytest.raises(ValueError, match="dataBook.join.row"):
        c3d_run.lay_over({"join": {"row": 1}}, {"join": {"row": {"layers": ["ROW"]}}}, "dataBook")
    book = {"rev9": {"outFolder": "rev9"}}
    c3d_run.lay_over(book, {"note": "why", "rev9": {"sales": {"sheet": "b"}}}, "dataBook")
    assert book == {"rev9": {"outFolder": "rev9", "sales": {"sheet": "b"}}}


def test_r350_a_probe_config_of_another_path_is_read_as_it_is(tmp_path):
    path = tmp_path / "probe.json"
    path.write_text(json.dumps({"dataBook": {"rev9": {"outFolder": "x"}}}), encoding="utf-8")
    assert c3d_run.load_config(path) == {"dataBook": {"rev9": {"outFolder": "x"}}}


def test_r350_the_build_the_measure_and_the_corner_rule_take_the_sales_sheet_from_config():
    for name in ("plan_from_book.py", "majority.py", "corner_front.py", "key_rules_check.py"):
        assert "c3d_probe.json" not in (ROOT / "python" / name).read_text(encoding="utf-8"), name
