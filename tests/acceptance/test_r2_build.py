"""Round R2 C2 and C3, the build and the rules per project, in the worker with no Revit. All made up.

Contract set here:
- Command siteBuild. Inputs: plotTable (plots.json of dataCheck), projectProfile, rules (project_rules.json, optional), config, and optional positionFile.
- build_plan.json: {"blocking": [{"line"}], "issues": [{"plot", "line"}], "rulesChoice": null or {"line"}, "coordinates": {...}, "plots": [{"number", "values": {name: {"value", "source", "rule"}}, "marked": [reason]}]}.
- preview.json, the 2D preview: {"plots": [{"number", "outline", "villaType"}], "issues": n}.
- A source is one of computed, rule, person entered, default, missing. A rule source names its rule id. A missing value is null and its plot is marked.
- Command rulesSetup. Inputs: template, projectFolder, who. Writes project_rules.json beside the profile, each rule status "not confirmed". Command rulesChange changes one rule and logs it.
- Rules fixed by the tool stay on: the Saudi checks, a missing value is never guessed, every change is logged.
"""
import json
import subprocess
from pathlib import Path

import pytest

import clean_folder
import made_up_project

ROOT = Path(__file__).resolve().parents[2]
WORKER = ROOT / "dotnet" / "CadReader" / "bin" / "Release" / "net8.0" / "CadReader.exe"
BUILD = pytest.mark.task("R2-C2-build-1")
RULES = pytest.mark.task("R2-C3-build-1")
SOURCES = {"computed", "rule", "person entered", "default", "missing"}
MANUAL = ROOT / "rules" / "templates" / "manual.json"


def worker(command, inputs, out, **extra):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    request = {"formatVersion": 1, "runId": "r2b", "document": {"path": str(out / "model.rvt"), "sessionId": "s1"}, "command": command, "inputs": [{"role": role, "path": str(path)} for role, path in inputs], "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": 300.0}
    request.update(extra)
    path = out / ("request_%s.json" % command)
    path.write_text(json.dumps(request), encoding="utf-8")
    done = subprocess.run([str(WORKER), "--request", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=320)
    answer = out / "answer_r2b.json"
    return done.returncode, json.loads(answer.read_text(encoding="utf-8")) if answer.is_file() else {}


def config():
    return ("config", ROOT / "config" / "project.json")


@pytest.fixture()
def stage1(tmp_path):
    folder = made_up_project.write_project(tmp_path / "project")
    out = tmp_path / "stage1"
    code, answer = worker("dataCheck", [("scanFolder", folder), config()], out)
    assert code == 0, answer
    return out


def build(stage1, out, rules=None, position=None):
    inputs = [("plotTable", stage1 / "plots.json"), ("projectProfile", stage1 / "project_profile.json"), config()]
    if rules:
        inputs.append(("rules", rules))
    if position:
        inputs.append(("positionFile", position))
    code, answer = worker("siteBuild", inputs, out)
    assert code == 0, answer
    return json.loads((Path(out) / "build_plan.json").read_text(encoding="utf-8")), answer


def setup_rules(tmp_path, template=MANUAL):
    code, answer = worker("rulesSetup", [("template", template), ("projectFolder", tmp_path / "project"), config()], tmp_path / "rules_out", who="made up person")
    assert code == 0, answer
    return tmp_path / "project" / "project_rules.json"


@BUILD
def test_every_value_of_the_plan_carries_its_source(stage1, tmp_path):
    plan, _ = build(stage1, tmp_path / "b")
    assert len(plan["plots"]) == len(made_up_project.PLOTS)
    for plot in plan["plots"]:
        assert plot["values"]
        for name, value in plot["values"].items():
            assert value["source"] in SOURCES, (plot["number"], name)
            if value["source"] == "rule":
                assert value["rule"].startswith("R") or value["rule"].startswith("M"), (plot["number"], name)
            if value["source"] == "missing":
                assert value["value"] is None and plot["marked"], (plot["number"], name)


@BUILD
def test_a_value_with_no_rule_is_never_guessed(stage1, tmp_path):
    plan, _ = build(stage1, tmp_path / "b")
    # No road surface and no rules: a level has nothing to stand on
    for plot in plan["plots"]:
        assert plot["values"]["ffl"]["value"] is None and plot["values"]["ffl"]["source"] == "missing"
        assert plot["marked"]


@BUILD
def test_the_build_reads_no_past_file(stage1, tmp_path):
    _, answer = build(stage1, tmp_path / "b")
    opened = [row["path"] for row in answer.get("opened", [])]
    assert opened
    assert clean_folder.past_reads(opened, clean_folder.past_patterns()) == []


@BUILD
def test_a_preview_comes_before_anything_is_written(stage1, tmp_path):
    build(stage1, tmp_path / "b")
    preview = json.loads((tmp_path / "b" / "preview.json").read_text(encoding="utf-8"))
    assert len(preview["plots"]) == len(made_up_project.PLOTS)
    assert all(len(row["outline"]) >= 4 for row in preview["plots"])


@BUILD
def test_only_a_blocking_issue_stops_and_it_says_why_in_plain_words(stage1, tmp_path):
    plan, _ = build(stage1, tmp_path / "b")
    assert plan["blocking"] == []
    (stage1 / "plots.json").write_text(json.dumps({"plots": []}), encoding="utf-8")
    plan, _ = build(stage1, tmp_path / "b2")
    assert plan["blocking"]
    line = plan["blocking"][0]["line"]
    assert line and "plot" in line.lower() and "{" not in line and "null" not in line


@BUILD
def test_coordinates_from_cad_and_the_position_file_shown_with_the_difference(stage1, tmp_path):
    position = tmp_path / "position.json"
    position.write_text(json.dumps({"eastM": 10.0, "northM": 20.0, "elevationM": 0.0, "angleDeg": 0.0}), encoding="utf-8")
    plan, _ = build(stage1, tmp_path / "b", position=position)
    coordinates = plan["coordinates"]
    assert coordinates["source"] == "cad"
    assert "positionFile" in coordinates and isinstance(coordinates["differenceMm"], (int, float))


@BUILD
def test_scenarios_table_holds_what_was_built_and_what_was_missing():
    text = (ROOT / "steps" / "scenarios.md").read_text(encoding="utf-8")
    assert "Built" in text and "Missing" in text


@RULES
def test_manual_template_holds_no_past_job_measure():
    text = MANUAL.read_text(encoding="utf-8")
    template = json.loads(text)
    assert template["rules"]
    assert all(rule.get("source") != "past" for rule in template["rules"])
    assert "keyRules" not in text


@RULES
def test_the_past_measures_are_a_template_of_their_own_and_never_the_default(tmp_path):
    assert (ROOT / "rules" / "templates" / "past-job.json").is_file()
    project = made_up_project.write_project(tmp_path / "project")
    assert not (project / "project_rules.json").exists()
    rules = setup_rules(tmp_path)
    assert all(rule.get("source") != "past" for rule in json.loads(rules.read_text(encoding="utf-8"))["rules"])


@BUILD
def test_a_first_build_with_no_rules_asks_once(stage1, tmp_path):
    plan, _ = build(stage1, tmp_path / "b")
    assert plan["rulesChoice"] and plan["rulesChoice"]["line"]


@RULES
def test_rules_from_a_template_stay_not_confirmed(tmp_path):
    made_up_project.write_project(tmp_path / "project")
    rules = setup_rules(tmp_path)
    data = json.loads(rules.read_text(encoding="utf-8"))
    assert data["rules"] and all(rule["status"] == "not confirmed" for rule in data["rules"] if not rule.get("fixed"))


@BUILD
@pytest.mark.parametrize("setting, blocks", [("warn", False), ("block", True)])
def test_before_build_warns_or_blocks_on_rules_not_confirmed(stage1, tmp_path, setting, blocks):
    made_up_project.write_project(tmp_path / "project")
    rules = setup_rules(tmp_path)
    data = json.loads(rules.read_text(encoding="utf-8"))
    data["beforeBuild"] = setting
    rules.write_text(json.dumps(data), encoding="utf-8")
    plan, _ = build(stage1, tmp_path / "b", rules=rules)
    assert bool(plan["blocking"]) is blocks
    assert plan["issues"] or blocks


@RULES
def test_warn_is_the_default(tmp_path):
    made_up_project.write_project(tmp_path / "project")
    assert json.loads(setup_rules(tmp_path).read_text(encoding="utf-8"))["beforeBuild"] == "warn"


@RULES
def test_every_rule_change_is_logged_with_who_when_old_and_new(tmp_path):
    made_up_project.write_project(tmp_path / "project")
    rules = setup_rules(tmp_path)
    first = [rule for rule in json.loads(rules.read_text(encoding="utf-8"))["rules"] if not rule.get("fixed")][0]
    code, answer = worker("rulesChange", [("rules", rules), config()], tmp_path / "c", who="made up person", ruleId=first["id"], newValue=123.0)
    assert code == 0, answer
    log = [json.loads(line) for line in (tmp_path / "project" / "rules_log.jsonl").read_text(encoding="utf-8").splitlines()]
    entry = log[-1]
    assert entry["who"] == "made up person" and entry["when"] and entry["rule"] == first["id"] and entry["new"] == 123.0 and "old" in entry


@RULES
def test_fixed_rules_cannot_be_changed(tmp_path):
    made_up_project.write_project(tmp_path / "project")
    rules = setup_rules(tmp_path)
    fixed = [rule for rule in json.loads(rules.read_text(encoding="utf-8"))["rules"] if rule.get("fixed")]
    assert fixed, "the Saudi checks, no guessed value and the change log are fixed rules"
    code, answer = worker("rulesChange", [("rules", rules), config()], tmp_path / "c", who="made up person", ruleId=fixed[0]["id"], newValue=False)
    assert code != 0 or "fixed" in json.dumps(answer).lower()


@pytest.mark.task("FAST-3-revit-test")
def test_the_build_run_in_revit_is_recorded():
    text = (ROOT / "steps" / "revit-results" / "FAST-3-revit-test.md").read_text(encoding="utf-8")
    for word in ("Build", "preview", "progress", "screenshot", "4 Check", "5 Floors", "6 Levels", "7 Place", "8 Walls", "Q439"):
        assert word in text, word


@pytest.mark.task("FAST-3-revit-test")
def test_the_rules_window_in_revit_is_recorded():
    text = (ROOT / "steps" / "revit-results" / "FAST-3-revit-test.md").read_text(encoding="utf-8")
    for word in ("gear", "amber", "not confirmed", "screenshot"):
        assert word in text, word
