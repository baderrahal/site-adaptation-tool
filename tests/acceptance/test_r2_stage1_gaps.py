"""Round R2 C1, the gaps of stage 1 that block a correct build. All made up, the names of the sheets differ test to test so nothing can hang on a name.

Contract set here, on top of test_r1_stage1.py:
- A team's workbook may hold a table of old code to new code, on a sheet of any name, and a typology column per plot. dataCheck finds them by what they hold.
- project_profile.json gains "codeMap": {"proposed": [{"old", "new", "source"}], "confirmed": [...]}, "typologyColumn": {"proposed": {"sheet", "column"} or null}
  and "hands": {"proposed": {code: "left" or "right" or null}, "confirmed": {code: hand}}.
- The optional input role projectProfile is a file the add-in window writes: {"codeMap": [{"old", "new"}], "hands": {code: hand}, "levels": [{"level", "word"}]}.
- A confirmed value carries source "person entered". A value worked out from it carries "computed".
"""
import json
import subprocess
from pathlib import Path

import pytest
from openpyxl import load_workbook

import made_up_project

ROOT = Path(__file__).resolve().parents[2]
WORKER = ROOT / "dotnet" / "CadReader" / "bin" / "Release" / "net8.0" / "CadReader.exe"
TASK = pytest.mark.task("R2-C1-build-1")
TAGS = {"computed", "rule", "person entered", "default", "missing", "user pick"}
NEW = {"VA1": "TA1", "VB2": "TB2"}


def run_check(folder, out, profile=None):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    inputs = [{"role": "scanFolder", "path": str(folder)}, {"role": "config", "path": str(ROOT / "config" / "project.json")}]
    if profile is not None:
        path = out / "confirmed_profile.json"
        path.write_text(json.dumps(profile), encoding="utf-8")
        inputs.append({"role": "projectProfile", "path": str(path)})
    request = {"formatVersion": 1, "runId": "r2", "document": {"path": str(out / "model.rvt"), "sessionId": "s1"}, "command": "dataCheck", "inputs": inputs, "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": 300.0}
    path = out / "request_r2.json"
    path.write_text(json.dumps(request), encoding="utf-8")
    done = subprocess.run([str(WORKER), "--request", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=320)
    return done.returncode


def read(out, name):
    return json.loads((Path(out) / name).read_text(encoding="utf-8"))


def add_code_map(folder, sheet="Renamed 2024", headers=("Former code", "Code now")):
    book = load_workbook(folder / "plots.xlsx")
    table = book.create_sheet(sheet)
    table.append(list(headers))
    for old, new in NEW.items():
        table.append([old, new])
    book.save(folder / "plots.xlsx")


def add_typology_column(folder, header="Typology_R9"):
    book = load_workbook(folder / "plots.xlsx")
    sheet = book["Plots"]
    sheet.cell(row=1, column=5, value=header)
    for row, plot in enumerate(made_up_project.PLOTS, start=2):
        sheet.cell(row=row, column=5, value=NEW[plot[3]])
    book.save(folder / "plots.xlsx")


def plots_of(out):
    return {row["number"]: row for row in read(out, "plots.json")["plots"]}


@TASK
@pytest.mark.parametrize("sheet, headers", [("Renamed 2024", ("Former code", "Code now")), ("2020-01-01_Typo", ("A", "B")), ("Sheet9", ("Old", "New"))])
def test_a_table_of_old_to_new_codes_is_found_by_what_it_holds(tmp_path, sheet, headers):
    folder = made_up_project.write_project(tmp_path / "p")
    add_code_map(folder, sheet, headers)
    assert run_check(folder, tmp_path / "o") == 0
    proposed = {row["old"]: row["new"] for row in read(tmp_path / "o", "project_profile.json")["codeMap"]["proposed"]}
    assert proposed == NEW


@TASK
def test_a_proposed_map_changes_no_plot_until_it_is_confirmed(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    add_code_map(folder)
    assert run_check(folder, tmp_path / "o") == 0
    assert plots_of(tmp_path / "o")["101"]["villaType"] == "VA1"
    assert read(tmp_path / "o", "project_profile.json")["codeMap"]["confirmed"] == []


@TASK
def test_a_confirmed_map_gives_the_new_code_with_its_source(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    profile = {"codeMap": [{"old": old, "new": new} for old, new in NEW.items()]}
    assert run_check(folder, tmp_path / "o", profile) == 0
    plots = plots_of(tmp_path / "o")
    for number, _, _, villa, _, _, _ in made_up_project.PLOTS:
        assert plots[number]["villaType"] == NEW[villa]
        assert plots[number]["source"]["villaType"] == "computed"
    assert read(tmp_path / "o", "project_profile.json")["codeMap"]["source"] == "person entered"


@TASK
def test_no_table_says_so_and_lets_the_user_type_it(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    assert run_check(folder, tmp_path / "o") == 0
    assert read(tmp_path / "o", "project_profile.json")["codeMap"]["proposed"] == []
    rows = {row["need"]: row for row in read(tmp_path / "o", "data_check.json")["needs"]}
    assert "code" in rows["villaType"]["line"].lower()


@TASK
def test_a_typology_column_per_plot_is_proposed(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    add_typology_column(folder)
    assert run_check(folder, tmp_path / "o") == 0
    proposed = read(tmp_path / "o", "project_profile.json")["typologyColumn"]["proposed"]
    assert proposed["column"] == "Typology_R9" and proposed["sheet"] == "Plots"


@TASK
def test_a_hand_is_proposed_per_prototype_and_shown_as_a_guess(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    assert run_check(folder, tmp_path / "o") == 0
    hands = read(tmp_path / "o", "project_profile.json")["hands"]
    assert sorted(hands["proposed"]) == ["VA1", "VB2"]
    assert all(value in ("left", "right", None) for value in hands["proposed"].values())
    assert hands["confirmed"] == {}


@TASK
def test_mirror_follows_the_block_scale_and_the_confirmed_hand(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    assert run_check(folder, tmp_path / "right", {"hands": {"VA1": "right", "VB2": "right"}}) == 0
    assert run_check(folder, tmp_path / "left", {"hands": {"VA1": "left", "VB2": "right"}}) == 0
    right = plots_of(tmp_path / "right")
    left = plots_of(tmp_path / "left")
    for number, _, _, villa, mirrored, _, _ in made_up_project.PLOTS:
        assert right[number]["source"]["mirror"] == "computed"
        assert "mirror" not in right[number].get("weak", {})
        # A prototype drawn in the other hand turns each of its plots over, the other type stays
        assert left[number]["mirror"] is (right[number]["mirror"] is not (villa == "VA1"))
        assert right[number]["mirror"] is mirrored


@TASK
def test_a_group_word_comes_only_from_a_text_that_names_groups(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p", words=("Sector", "Cluster"))
    (folder / "plots.xlsx").unlink()
    text = made_up_project.dxf_text(words=("Sector", "Cluster"))
    # Notes near each area that do not name it. A word they hold must never become a level
    note = "".join("0\nTEXT\n8\nNOTES\n10\n%s\n20\n-20.0\n30\n0.0\n40\n1.0\n1\nGeneral note\n" % x for x in (0.0, 300.0))
    (folder / "site.dxf").write_text(text.replace("0\nENDSEC\n0\nEOF\n", note + "0\nENDSEC\n0\nEOF\n"), encoding="utf-8", newline="\n")
    assert run_check(folder, tmp_path / "o") == 0
    words = [row["word"] for row in read(tmp_path / "o", "project_profile.json")["levels"]]
    assert "General" not in words and "note" not in words


@TASK
def test_a_level_the_user_names_is_taken_with_its_source(tmp_path):
    folder = made_up_project.write_project(tmp_path / "p")
    (folder / "plots.xlsx").unlink()
    (folder / "site.dxf").write_text(made_up_project.dxf_text(words=("", "")), encoding="utf-8", newline="\n")
    assert run_check(folder, tmp_path / "o", {"levels": [{"level": 1, "word": "Precinct"}, {"level": 2, "word": "Court"}]}) == 0
    levels = read(tmp_path / "o", "project_profile.json")["levels"]
    assert [row["word"] for row in levels] == ["Precinct", "Court"]
    assert all(row["source"] == "person entered" for row in levels)


@TASK
def test_the_scenarios_table_shows_c1_scored_on_all_five():
    text = (ROOT / "steps" / "scenarios.md").read_text(encoding="utf-8")
    for scenario in ("S1", "S2", "S3", "S4", "S5"):
        assert "| %s |" % scenario in text
    assert "not run" not in text


@pytest.mark.task("R2-C1-revit-test")
def test_the_confirm_window_is_recorded():
    text = (ROOT / "steps" / "revit-results" / "R2-C1-revit-test.md").read_text(encoding="utf-8")
    for word in ("code map", "hand", "group", "screenshot"):
        assert word in text.lower(), word
