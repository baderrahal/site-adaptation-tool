"""Round R1, Stage 1 Project data. The worker command dataCheck runs on a folder as a team hands it over, with no past file and no name given.

The contract these tests set, for the lane to build:
- Request: command dataCheck, input role scanFolder with the path of the folder, input role config, an output folder.
- data_check.json: {"needs": [{"need", "mark", "line"}]} with one row per need. Mark is strong, usable, weak or missing.
- project_profile.json: {"levels": [{"level", "word", "source"}]}, the project's own word for each group level, level 1 the largest.
- plots.json: {"plots": [{"number", "villaType", "mirror", "groups", "source"}]}, groups the group of each level by level, source a tag per value.
"""
import json
import re
import subprocess
from pathlib import Path

import pytest

import clean_folder
import made_up_project

ROOT = Path(__file__).resolve().parents[2]
WORKER = ROOT / "dotnet" / "CadReader" / "bin" / "Release" / "net8.0" / "CadReader.exe"
NEEDS = ["plotOutlines", "plotNumbers", "groupLevels", "villaType", "villaBlocks", "roadEdges", "roadLevels", "coordinates", "prototypes", "rules", "sharedParameters"]
MARKS = {"strong", "usable", "weak", "missing"}
TAGS = {"computed", "rule", "user pick", "missing"}
BUILD = pytest.mark.task("R1-build-1")


def run_check(folder, out):
    """Runs the worker as the add-in does, and gives its exit code and answer."""
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    request = {
        "formatVersion": 1, "runId": "r1", "document": {"path": str(out / "model.rvt"), "sessionId": "s1"}, "command": "dataCheck",
        "inputs": [{"role": "scanFolder", "path": str(folder)}, {"role": "config", "path": str(ROOT / "config" / "project.json")}],
        "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": 300.0,
    }
    path = out / "request_r1.json"
    path.write_text(json.dumps(request), encoding="utf-8")
    done = subprocess.run([str(WORKER), "--request", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=320)
    answer = out / "answer_r1.json"
    return done.returncode, json.loads(answer.read_text(encoding="utf-8")) if answer.is_file() else {}


def read(out, name):
    return json.loads((Path(out) / name).read_text(encoding="utf-8"))


def needs_of(out):
    return {row["need"]: row for row in read(out, "data_check.json")["needs"]}


@pytest.fixture()
def project(tmp_path):
    folder = made_up_project.write_project(tmp_path / "project")
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    return tmp_path / "out", answer


@BUILD
def test_every_need_has_a_mark_and_one_plain_line(project):
    rows = needs_of(project[0])
    assert sorted(rows) == sorted(NEEDS)
    for need, row in rows.items():
        assert row["mark"] in MARKS, need
        line = row["line"]
        assert line and len(line) <= 160 and "\n" not in line, need
        # In CAD and Revit words, never a word of the code
        assert not re.search(r"[{}\[\]]|\bjson\b|\bnull\b|\bconfig\b|\bR\d{3}\b|[a-z][A-Z]", line), line


@BUILD
def test_the_check_tells_what_the_made_up_project_holds(project):
    rows = needs_of(project[0])
    for need in ("plotOutlines", "plotNumbers", "villaBlocks", "roadEdges", "prototypes", "villaType"):
        assert rows[need]["mark"] in {"strong", "usable"}, (need, rows[need])
    # No LandXML, no surface, no rule set and no parameter sheet are in this folder
    for need in ("roadLevels", "rules", "sharedParameters"):
        assert rows[need]["mark"] == "missing", (need, rows[need])


@BUILD
def test_an_empty_folder_says_all_is_missing_and_never_stops(tmp_path):
    folder = tmp_path / "empty"
    folder.mkdir()
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    rows = needs_of(tmp_path / "out")
    assert sorted(rows) == sorted(NEEDS)
    assert all(row["mark"] == "missing" and row["line"] for row in rows.values())


@BUILD
def test_a_broken_drawing_is_said_and_the_run_carries_on(tmp_path):
    folder = made_up_project.write_project(tmp_path / "project")
    (folder / "broken.dwg").write_bytes(b"not a drawing")
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    assert "broken.dwg" in json.dumps(read(tmp_path / "out", "data_check.json"))
    assert len(read(tmp_path / "out", "plots.json")["plots"]) == len(made_up_project.PLOTS)


@BUILD
def test_plots_are_read_with_no_past_file(project):
    out, answer = project
    plots = {row["number"]: row for row in read(out, "plots.json")["plots"]}
    assert sorted(plots) == sorted(plot[0] for plot in made_up_project.PLOTS)
    for number, _, _, villa, mirror, _, _ in made_up_project.PLOTS:
        assert plots[number]["villaType"] == villa, number
        assert plots[number]["mirror"] is mirror, number
    opened = [row["path"] for row in answer.get("opened", [])]
    assert opened
    assert clean_folder.past_reads(opened, clean_folder.past_patterns()) == []


@BUILD
def test_every_value_of_a_plot_carries_its_source(project):
    for row in read(project[0], "plots.json")["plots"]:
        for field in ("number", "villaType", "mirror", "groups"):
            assert row["source"][field] in TAGS, (row["number"], field)


def partition(out):
    """The plots of each group at each level as sets of numbers, with no regard for the group's own name."""
    plots = read(out, "plots.json")["plots"]
    levels = len(plots[0]["groups"])
    found = []
    for level in range(levels):
        groups = {}
        for row in plots:
            groups.setdefault(row["groups"][level], set()).add(row["number"])
        found.append(sorted(sorted(group) for group in groups.values()))
    return found


@BUILD
def test_groups_are_found_by_shape(project):
    assert partition(project[0]) == [
        [["101", "102", "103", "104", "105"], ["201", "202", "203"]],
        [["101", "102", "103"], ["104", "105"], ["201", "202", "203"]],
    ]


@BUILD
def test_groups_stay_the_same_when_every_name_changes(tmp_path):
    names = {"plots": "A-17", "numbers": "XX-TXT", "houses": "M9", "kerb": "QQ"}
    folder = made_up_project.write_project(tmp_path / "project", names=names, words=("Sector", "Cluster"))
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    assert partition(tmp_path / "out") == [
        [["101", "102", "103", "104", "105"], ["201", "202", "203"]],
        [["101", "102", "103"], ["104", "105"], ["201", "202", "203"]],
    ]


@BUILD
@pytest.mark.parametrize("words", [("Phase", "Block"), ("Zone", "Neighbourhood"), ("Sector", "Cluster")])
def test_the_profile_takes_the_projects_own_words(tmp_path, words):
    folder = made_up_project.write_project(tmp_path / "project", words=words)
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    levels = read(tmp_path / "out", "project_profile.json")["levels"]
    assert [row["word"] for row in levels] == list(words)
    assert all(row["source"] in TAGS for row in levels)


@BUILD
def test_a_project_with_no_group_words_says_so(tmp_path):
    folder = made_up_project.write_project(tmp_path / "project")
    (folder / "plots.xlsx").unlink()
    (folder / "site.dxf").write_text(made_up_project.dxf_text(words=("", "")), encoding="utf-8", newline="\n")
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    levels = read(tmp_path / "out", "project_profile.json")["levels"]
    assert levels and all(row["word"] is None and row["source"] == "missing" for row in levels)
    assert needs_of(tmp_path / "out")["groupLevels"]["mark"] in {"weak", "usable"}


@BUILD
def test_plot_numbers_in_another_style_are_still_read(tmp_path):
    folder = made_up_project.write_project(tmp_path / "project", number_style="LOT/%s-X")
    code, answer = run_check(folder, tmp_path / "out")
    assert code == 0, answer
    numbers = sorted(row["number"] for row in read(tmp_path / "out", "plots.json")["plots"])
    assert numbers == sorted("LOT/%s-X" % plot[0] for plot in made_up_project.PLOTS)


@BUILD
def test_the_clean_folder_check_runs_on_the_sample():
    failures = clean_folder.run()
    assert failures is not None, "samples/new-project holds no build.json"
    assert failures == []


@BUILD
def test_the_scenarios_table_holds_all_five():
    path = ROOT / "steps" / "scenarios.md"
    text = path.read_text(encoding="utf-8")
    for scenario in ("S1", "S2", "S3", "S4", "S5"):
        row = re.search(r"^\| *%s *\|(.+)\|\s*$" % scenario, text, flags=re.MULTILINE)
        assert row, scenario
        # Plots, numbers, villa type and mirror each score as a count of the right answer
        assert len(re.findall(r"\b\d[\d,]* of \d[\d,]*\b", row.group(1))) >= 4, scenario


@pytest.mark.task("R1-revit-test")
def test_the_revit_result_of_stage_1_is_recorded():
    text = (ROOT / "steps" / "revit-results" / "R1-revit-test.md").read_text(encoding="utf-8")
    for word in ("S1", "Set up site", "Read data", "Review data", "screenshot"):
        assert word in text, word
