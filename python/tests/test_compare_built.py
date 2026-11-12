"""R835 to R839. The compare of a built model against the answer key, on made up files in the temporary folder of the test.
No number of a real model is here: the positions, levels and plot numbers below are invented."""
import csv
import json
import sys
from decimal import Decimal
from pathlib import Path

import pytest

import compare_built

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "checks"))
import compare_md  # noqa: E402

SOURCE = "a made up source"
LIMITS = {"plotsBuiltPercent": 95, "heldPercent": 95, "positionMm": 5, "fflMm": 5, "rotationDeg": 0.1, "countPercent": 5}
PLOTS = 40
HEADER = ["elementId", "plot", "prototype", "file", "originX", "originY", "originZ", "rotationDeg", "mirrored", "loaded"]


def settings(neighbourhoods=("N1", "N2")):
    block = {name: {"value": value, "source": SOURCE} for name, value in LIMITS.items()}
    block["yardModels"] = {"value": ["KA"], "source": SOURCE}
    block["linksOnlyModels"] = {"value": ["KB"], "source": SOURCE}
    block["job"] = {"value": "made", "source": SOURCE}
    block["neighbourhoods"] = {"value": list(neighbourhoods), "source": SOURCE}
    return {"compare": block}


AREAS = {"jobs": {"made": [{"code": "KA", "areas": [{"nbh": "N1", "kind": "model"}, {"nbh": "N9", "kind": "schedule"}]},
                           {"code": "KB", "areas": [{"nbh": "N2", "kind": "model"}]}]}}


def past_rows(count=PLOTS, first=700):
    """One row per made up plot, and one link that names no plot, as each past model holds one."""
    rows = [{"elementId": str(index), "plot": str(first + index), "prototype": "P1", "file": "P1.rvt",
             "originX": "%d.250" % (100 + 20 * index), "originY": "-%d.500" % (50 + index), "originZ": "%d.125" % (10 + index % 3),
             "rotationDeg": ["0.000", "90.000", "359.950", "180.000"][index % 4], "mirrored": "True" if index % 2 else "False",
             "loaded": "False"} for index in range(count)]
    return rows + [dict(rows[0], elementId="9999", plot="")]


def counts(floors=100, yards=40, walls=200, levels=20):
    return {"floors": {"count": floors + yards, "byKind": {"generic": floors, "yard": yards, "ramp": 0, "none": 0}},
            "walls": {"count": walls}, "levels": {"count": levels}}


def write_read(folder, code, rows, count_values, base=(1.5, 2.5, 0.0, 0.0)):
    folder.mkdir(parents=True)
    with open(folder / "links.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=HEADER)
        writer.writeheader()
        writer.writerows(rows)
    (folder / "key_reads.json").write_text(json.dumps({"keys": {code: {"counts": count_values}}}), encoding="utf-8")
    if base is not None:
        raw = folder / ("R" + code)
        raw.mkdir()
        point = {"position": [0.0, 0.0, 0.0], "sharedPosition": [base[0], base[1], base[2]]}
        (raw / ("testhost_R%s_points.json" % code)).write_text(json.dumps({
            "projectPosition": {"eastWest": base[0], "northSouth": base[1], "elevation": base[2], "angleDeg": base[3]},
            "surveyPoint": point, "basePoint": point}), encoding="utf-8")


def moved(row, dx="0", dy="0", dz="0", turn="0", flip=False):
    changed = dict(row)
    changed["originX"] = str(Decimal(row["originX"]) + Decimal(dx))
    changed["originY"] = str(Decimal(row["originY"]) + Decimal(dy))
    changed["originZ"] = str(Decimal(row["originZ"]) + Decimal(dz))
    changed["rotationDeg"] = str((Decimal(row["rotationDeg"]) + Decimal(turn)) % 360)
    if flip:
        changed["mirrored"] = "False" if row["mirrored"] == "True" else "True"
    return changed


def inside_rows():
    """38 of 40 plots built, 95 percent. Of the 38, one row is off in every measure, so 37 of 38 hold each, and every other row
    stands exactly on each limit: 3 and 4 mm in plan, 5 mm in level, 0.1 degree, one of them across 360."""
    rows = [moved(row, dx="0.003", dy="-0.004", dz="-0.005", turn="0.1") for row in past_rows()[:38]]
    rows[5] = moved(past_rows()[5], dx="0.300", dz="0.300", turn="30", flip=True)
    return rows


class Run:
    def __init__(self, tmp_path, built_rows=None, built_counts=None, past=None, neighbourhoods=("N1", "N2"), bases=None, built=True):
        self.out = tmp_path / "out" / "compare.md"
        self.rows = tmp_path / "out" / "rows.csv"
        self.out.parent.mkdir()
        config = tmp_path / "project.json"
        config.write_text(json.dumps(settings(neighbourhoods)), encoding="utf-8")
        areas = tmp_path / "area_codes.json"
        areas.write_text(json.dumps(AREAS), encoding="utf-8")
        bases = bases or {}
        key = tmp_path / "key"
        for code in ("KA", "KB"):
            write_read(key / code, code, past_rows(first=700 if code == "KA" else 900) if past is None else past, counts(),
                       bases.get(code, (1.5, 2.5, 0.0, 0.0)))
        self.argv = ["--config", str(config), "--areas", str(areas), "--key", str(key), "--out", str(self.out), "--rows", str(self.rows)]
        if built:
            write_read(tmp_path / "built" / "N1", "X", inside_rows() if built_rows is None else built_rows,
                       counts(105, 42, 190, 21) if built_counts is None else built_counts, None)
            self.argv += ["--built", str(tmp_path / "built")]
        self.code = compare_built.main(self.argv)
        self.text = self.out.read_text(encoding="utf-8") if self.out.is_file() else ""

    def row(self, heading, neighbourhood):
        part = self.text.split("## " + heading)[1].split("\n## ")[0]
        line = [line for line in part.splitlines() if line.startswith("| %s |" % neighbourhood)][0]
        cells = [cell.strip() for cell in line.strip("|").split("|")]
        return dict(zip(("nbh", "model", "plots", "position", "ffl", "rotation", "mirror", "floors", "yards", "walls", "levels", "result"), cells))

    def failed_rows(self, column, neighbourhood="N1"):
        with open(self.rows, encoding="utf-8", newline="") as handle:
            return [(row["side"], int(row["row"])) for row in csv.DictReader(handle) if row["column"] == column and row["neighbourhood"] == neighbourhood]


BUILT = "The built side against the past models"
SELF = "The past models against themselves"


def test_r838_each_past_model_against_itself_gives_100_percent(tmp_path):
    run = Run(tmp_path, built=False)
    assert run.code == 0
    first = run.row(SELF, "N1")
    assert first["plots"] == "40 of 40, 100.0 percent, pass"
    assert first["position"] == first["ffl"] == "40 of 40, 100.0 percent, largest 0.0 mm, pass"
    assert first["rotation"] == first["mirror"] == "40 of 40, 100.0 percent, pass"
    assert (first["floors"], first["yards"], first["walls"], first["levels"]) == ("100 of 100, pass", "40 of 40, pass", "200 of 200, pass", "20 of 20, pass")
    assert first["result"] == "pass"
    assert "2 of 2 past models give 100 percent in every column that is counted." in run.text


def test_r837_a_links_only_model_counts_four_columns_and_yards_count_on_the_yard_models(tmp_path):
    run = Run(tmp_path, built=False)
    second = run.row(SELF, "N2")
    assert [second[name] for name in ("ffl", "floors", "yards", "walls", "levels")] == ["not counted"] * 5
    assert second["position"] == "40 of 40, 100.0 percent, largest 0.0 mm, pass" and second["result"] == "pass"


def test_r838_the_base_of_the_past_models_is_measured_first(tmp_path):
    (tmp_path / "a").mkdir()
    same = Run(tmp_path / "a", built=False)
    assert "1 distinct base of 2 reads" in same.text and "The 2 past models share one coordinate base." in same.text
    assert same.text.index("## The coordinate base") < same.text.index("## " + SELF)
    (tmp_path / "b").mkdir()
    other = Run(tmp_path / "b", built=False, bases={"KB": (1.5, 2.6, 0.0, 0.0)})
    assert "2 distinct bases of 2 reads" in other.text and "do not share one coordinate base" in other.text
    (tmp_path / "c").mkdir()
    none = Run(tmp_path / "c", built=False, bases={"KB": None})
    assert "UNKNOWN, 1 of 2 reads hold no points file" in none.text


def test_r835_no_built_folder_counts_0_built_of_each_neighbourhood(tmp_path):
    run = Run(tmp_path, built=False, neighbourhoods=("N1", "N2", "N3"))
    first = run.row(BUILT, "N1")
    assert first["plots"] == "0 of 40, 0.0 percent, fail" and first["position"] == "no plot built, fail"
    assert first["floors"] == "0 of 100, fail" and first["result"] == "fail"
    assert run.row(BUILT, "N3")["plots"] == "UNKNOWN, no past model" and run.row(BUILT, "N3")["result"] == "UNKNOWN"
    assert "0 of 3 neighbourhoods pass, 2 fail, 1 UNKNOWN." in run.text
    assert run.failed_rows("plots") == run.failed_rows("plots", "N2") == [("past", line) for line in range(2, 42)]


def test_r836_a_built_side_just_inside_every_limit_passes(tmp_path):
    run = Run(tmp_path)
    row = run.row(BUILT, "N1")
    assert row["plots"] == "38 of 40, 95.0 percent, pass"
    assert row["position"] == "37 of 38, 97.3 percent, largest 300.0 mm, pass"
    assert row["ffl"] == "37 of 38, 97.3 percent, largest 300.0 mm, pass"
    assert row["rotation"] == row["mirror"] == "37 of 38, 97.3 percent, pass"
    assert (row["floors"], row["yards"], row["walls"], row["levels"]) == ("105 of 100, pass", "42 of 40, pass", "190 of 200, pass", "21 of 20, pass")
    assert row["result"] == "pass"
    assert run.failed_rows("plots") == [("past", 40), ("past", 41)]
    assert run.failed_rows("position") == run.failed_rows("ffl") == run.failed_rows("rotation") == run.failed_rows("mirror") == [("built", 7)]


def test_r835_a_missing_plot_fails_the_plots_column_by_its_row(tmp_path):
    run = Run(tmp_path, built_rows=inside_rows()[:37])
    assert run.row(BUILT, "N1")["plots"] == "37 of 40, 92.5 percent, fail" and run.row(BUILT, "N1")["result"] == "fail"
    assert run.failed_rows("plots") == [("past", 39), ("past", 40), ("past", 41)]


@pytest.mark.parametrize("column,change,cell", [
    ("position", {"dx": "0.004", "dy": "0.004"}, "36 of 38, 94.7 percent, largest 300.0 mm, fail"),
    ("ffl", {"dz": "0.006"}, "36 of 38, 94.7 percent, largest 300.0 mm, fail"),
    ("rotation", {"turn": "-0.2"}, "36 of 38, 94.7 percent, fail"),
    ("mirror", {"flip": True}, "36 of 38, 94.7 percent, fail")])
def test_r836_a_measure_off_on_a_second_row_fails_its_column_by_that_row(tmp_path, column, change, cell):
    rows = inside_rows()
    rows[11] = moved(past_rows()[11], **change)
    run = Run(tmp_path, built_rows=rows)
    row = run.row(BUILT, "N1")
    assert row[column] == cell and row["result"] == "fail"
    assert run.failed_rows(column) == [("built", 7), ("built", 13)]
    others = [name for name in ("position", "ffl", "rotation", "mirror") if name != column]
    assert all(row[name].endswith("pass") and run.failed_rows(name) == [("built", 7)] for name in others)


@pytest.mark.parametrize("column,values,cell", [
    ("floors", (106, 42, 190, 21), "106 of 100, fail"), ("yards", (105, 37, 190, 21), "37 of 40, fail"),
    ("walls", (105, 42, 189, 21), "189 of 200, fail"), ("levels", (105, 42, 190, 22), "22 of 20, fail")])
def test_r837_a_count_off_by_more_than_the_limit_fails_its_column(tmp_path, column, values, cell):
    run = Run(tmp_path, built_counts=counts(*values))
    row = run.row(BUILT, "N1")
    assert row[column] == cell and row["result"] == "fail"
    assert all(row[name].endswith("pass") for name in ("floors", "yards", "walls", "levels") if name != column)


def test_r837_a_count_the_read_lacks_is_unknown_never_zero(tmp_path):
    lacking = counts(105, 42, 190, 21)
    lacking["walls"] = None
    run = Run(tmp_path, built_counts=lacking)
    row = run.row(BUILT, "N1")
    assert row["walls"] == "UNKNOWN, the built read holds no count" and row["result"] == "UNKNOWN"


def test_r835_a_plot_two_built_links_name_holds_no_measure(tmp_path):
    rows = inside_rows()
    rows.append(dict(rows[3], elementId="5000"))
    run = Run(tmp_path, built_rows=rows)
    row = run.row(BUILT, "N1")
    assert row["plots"] == "38 of 40, 95.0 percent, pass" and row["position"] == "36 of 38, 94.7 percent, largest 300.0 mm, fail"
    assert run.failed_rows("position") == [("built", 5), ("built", 7), ("built", 40)]
    assert "built plots with two links 1" in run.text


def test_r835_a_cell_that_is_no_number_fails_its_row_and_a_plot_outside_the_past_model_is_only_counted(tmp_path):
    rows = inside_rows()
    rows[11] = dict(rows[11], originX="NaN", rotationDeg="")
    rows.append(dict(rows[0], plot="31999"))
    run = Run(tmp_path, built_rows=rows)
    assert run.failed_rows("position") == [("built", 7), ("built", 13)] and run.failed_rows("rotation") == [("built", 7), ("built", 13)]
    assert run.row(BUILT, "N1")["plots"] == "38 of 40, 95.0 percent, pass"
    assert "built plots not in the past model 1" in run.text


def test_r835_an_empty_line_above_a_row_does_not_shift_its_line(tmp_path):
    run = Run(tmp_path)
    links = tmp_path / "built" / "N1" / "links.csv"
    lines = links.read_text(encoding="utf-8").split("\n")
    links.write_text("\n".join(lines[:1] + ["", ""] + lines[1:]), encoding="utf-8")
    assert compare_built.main(run.argv) == 0
    assert run.failed_rows("position") == [("built", 9)]


@pytest.mark.parametrize("cell", ["1_0", "1E+999999", "1E+30", "Infinity", "0x10"])
def test_r836_a_cell_of_an_odd_number_form_fails_its_row_and_stops_nothing(tmp_path, cell):
    rows = inside_rows()
    rows[11] = dict(rows[11], originX=cell, originZ=cell)
    run = Run(tmp_path, built_rows=rows)
    assert run.code == 0
    assert run.failed_rows("position") == run.failed_rows("ffl") == [("built", 7), ("built", 13)]


@pytest.mark.parametrize("floors", [{"count": True, "byKind": {"yard": True}}, {"count": 10, "byKind": {"yard": 12}},
                                    {"count": 10.0, "byKind": {"yard": 2}}, {"count": -1, "byKind": {"yard": 0}}, "text"])
def test_r837_a_count_that_is_no_whole_number_is_unknown(tmp_path, floors):
    odd = counts(105, 42, 190, 21)
    odd["floors"] = floors
    run = Run(tmp_path, built_counts=odd)
    row = run.row(BUILT, "N1")
    assert row["floors"] == "UNKNOWN, the built read holds no count" and row["result"] != "pass"


@pytest.mark.parametrize("text", ["[]", "null", '{"keys": {"a": null, "b": null}}', '{"keys": []}', "{"])
def test_r837_a_counts_file_of_another_shape_stops_the_compare_in_one_line(tmp_path, capsys, text):
    run = Run(tmp_path)
    (tmp_path / "built" / "N1" / "key_reads.json").write_text(text, encoding="utf-8")
    assert compare_built.main(run.argv) == 2
    assert capsys.readouterr().err.startswith("compare stopped: ")


def test_r835_a_links_file_that_is_no_text_stops_the_compare_in_one_line(tmp_path, capsys):
    run = Run(tmp_path)
    (tmp_path / "built" / "N1" / "links.csv").write_bytes(b"plot,originX\n\xff\xfe,1\n")
    assert compare_built.main(run.argv) == 2
    assert capsys.readouterr().err.startswith("compare stopped: ")


def test_r836_a_share_over_100_and_a_model_in_both_lists_stop_the_compare(tmp_path, capsys):
    run = Run(tmp_path, built=False)
    config = Path(run.argv[1])
    values = settings()
    values["compare"]["heldPercent"]["value"] = 150
    config.write_text(json.dumps(values), encoding="utf-8")
    assert compare_built.main(run.argv) == 2 and "compare.heldPercent" in capsys.readouterr().err
    values = settings()
    values["compare"]["linksOnlyModels"]["value"] = ["KA", "KB"]
    config.write_text(json.dumps(values), encoding="utf-8")
    assert compare_built.main(run.argv) == 2 and "both name KA" in capsys.readouterr().err


def test_r835_a_past_model_that_names_a_plot_twice_stops_the_compare(tmp_path, capsys):
    rows = past_rows()
    rows.append(dict(rows[3], elementId="5000"))
    run = Run(tmp_path, past=rows, built=False)
    assert run.code == 2 and not run.out.exists()
    assert "names a plot twice" in capsys.readouterr().err


def test_r836_a_limit_with_no_value_or_no_source_stops_the_compare(tmp_path, capsys):
    run = Run(tmp_path, built=False)
    config = Path(run.argv[1])
    for broken in ({"value": 5}, {"source": SOURCE}, {"value": "5", "source": SOURCE}):
        values = settings()
        values["compare"]["positionMm"] = broken
        config.write_text(json.dumps(values), encoding="utf-8")
        assert compare_built.main(run.argv) == 2
        assert "compare.positionMm" in capsys.readouterr().err


def test_r836_the_limits_of_the_settings_are_baders_of_12_nov():
    block = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))["compare"]
    assert {name: block[name]["value"] for name in LIMITS} == LIMITS
    assert all(block[name]["source"] == "Bader 12 Nov 2026" for name in list(LIMITS) + ["yardModels", "linksOnlyModels"])
    assert block["yardModels"]["value"] == ["IMC", "IMD", "IME", "IMF"] and block["linksOnlyModels"]["value"] == ["IMA", "IMB"]


def test_r839_a_second_run_gives_the_same_bytes_and_every_line_is_of_the_checked_form(tmp_path):
    run = Run(tmp_path)
    first, first_rows = run.out.read_bytes(), run.rows.read_bytes()
    assert compare_built.main(run.argv) == 0
    assert run.out.read_bytes() == first and run.rows.read_bytes() == first_rows
    assert b"\r" not in first
    assert compare_md.faults(first.decode("utf-8")) == []


def test_r839_the_page_holds_no_position_no_level_and_no_plot_number(tmp_path):
    run = Run(tmp_path)
    held = set()
    for row in past_rows() + inside_rows():
        held.update([row["plot"], row["originX"], row["originY"], row["originZ"]])
    held.discard("")
    assert [value for value in sorted(held) if value in run.text or value in run.rows.read_text(encoding="utf-8")] == []
