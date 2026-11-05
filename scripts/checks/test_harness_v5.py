"""The harness rules of AI MAX version 5, each broken on purpose where it can be."""
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

import pytest
import yaml

import no_attribution

ROOT = Path(__file__).resolve().parents[2]
SETTINGS = json.loads((ROOT / ".claude" / "settings.json").read_text(encoding="utf-8"))
HOOKS = ROOT / ".claude" / "hooks"
windows_only = pytest.mark.skipif(sys.platform != "win32" or shutil.which("powershell") is None, reason="the lane hooks are Windows PowerShell")


def run_hook(name, payload, *args):
    done = subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(HOOKS / name), *args], input=json.dumps(payload), capture_output=True, text=True, encoding="utf-8", errors="replace")
    return done.returncode, done.stdout, done.stderr


def test_the_required_job_leaves_out_the_r347_tests_and_the_other_job_keeps_them():
    required = (ROOT / ".github" / "workflows" / "tests.yml").read_text(encoding="utf-8")
    other = (ROOT / ".github" / "workflows" / "test.yml").read_text(encoding="utf-8")
    assert "FullyQualifiedName!~WorkerRunTests.R347" in required
    assert "R347" not in other.replace("# R11, R346", "").split("filter", 1)[-1] or "filter" not in other
    assert "R347 worker tests" in (ROOT / "steps" / "backlog.md").read_text(encoding="utf-8")


def test_attribution_is_off():
    assert SETTINGS["attribution"] is False


def test_every_bash_deny_rule_is_also_a_powershell_rule():
    deny = SETTINGS["permissions"]["deny"]
    bash = {rule[len("Bash("):] for rule in deny if rule.startswith("Bash(")}
    powershell = {rule[len("PowerShell("):] for rule in deny if rule.startswith("PowerShell(")}
    assert bash and bash == powershell


@pytest.mark.parametrize("word", ["--force", "--no-verify", "core.hooksPath", "reset --hard", "git clean", "--admin"])
def test_each_ordered_deny_rule_is_there(word):
    assert any(word in rule for rule in SETTINGS["permissions"]["deny"] if rule.startswith("Bash("))
    assert any(word in rule for rule in SETTINGS["permissions"]["deny"] if rule.startswith("PowerShell("))


def test_every_hook_is_called_the_one_way():
    for event, entries in SETTINGS["hooks"].items():
        for entry in entries:
            for hook in entry["hooks"]:
                assert hook["command"].startswith("powershell.exe -NoProfile -ExecutionPolicy Bypass -File "), (event, hook["command"])


def test_shell_guards_match_both_shells():
    guards = [entry for entry in SETTINGS["hooks"]["PreToolUse"] if any(name in json.dumps(entry) for name in ("guard-main", "guard-revit"))]
    assert guards and all(entry["matcher"] == "Bash|PowerShell" for entry in guards)


def test_answers_guard_covers_every_way_to_read():
    covered = " ".join(entry["matcher"] for entry in SETTINGS["hooks"]["PreToolUse"] if "guard-answers" in json.dumps(entry))
    for tool in ("Bash", "PowerShell", "Read", "Grep", "Glob", "Edit", "Write"):
        assert tool in covered


@pytest.mark.parametrize("path", sorted((ROOT / ".claude" / "rules").glob("*.md")), ids=lambda path: path.name)
def test_rules_frontmatter_parses(path):
    parts = path.read_text(encoding="utf-8").split("---\n")
    assert parts[0] == "" and len(parts) >= 3, "no frontmatter"
    head = yaml.safe_load(parts[1])
    assert isinstance(head.get("paths"), list) and head["paths"]


def test_yaml_error_is_seen():
    with pytest.raises(yaml.YAMLError):
        yaml.safe_load('paths:\n  - "a\n   b: [')


def test_no_attribution_finds_each_word():
    assert no_attribution.failures_of("title", "text", [("abc1234", "fix")]) == []
    for word in ("Co-Authored-By", "Claude-Session", "Generated with"):
        assert word in no_attribution.failures_of("title", "a line " + word + " here", [])[0]
        assert "commit abc1234" in no_attribution.failures_of("title", "", [("abc1234567", "fix\n\n" + word + " x")])[0]
        assert "title" in no_attribution.failures_of(word, "", [])[0]


def test_progress_is_written_by_the_script():
    lines = (ROOT / "steps" / "PROGRESS.md").read_text(encoding="utf-8").split("\n")
    found = re.fullmatch(r"Hash of the lines above, a hand edit fails a test: ([0-9a-f]{64})", lines[-2])
    assert found, "steps/PROGRESS.md has no hash line, write it with scripts/status.ps1 -Progress"
    text = "\n".join(lines[:-2]) + "\n"
    assert hashlib.sha256(text.encode("utf-8")).hexdigest() == found.group(1), "steps/PROGRESS.md differs from what scripts/status.ps1 wrote"


def test_claude_md_holds_the_new_ways_of_work():
    text = (ROOT / "CLAUDE.md").read_text(encoding="utf-8")
    assert len(text.splitlines()) < 150
    for words in ("every 15 minutes", "two PRs waiting", "numbered, one action each", "New Terminal"):
        assert words in text


@windows_only
@pytest.mark.parametrize("field, value", [
    ("file_path", r"C:\Users\x\Documents\SiteAdaptation\scenarios\answers\S1.json"),
    ("path", "C:/Users/x/Documents/SiteAdaptation/scenarios/answers"),
    ("path", r"C:\w\scenarios\holdout_answers"),
    ("glob", "**/scenarios/answers/*.json"),
    ("command", r'python -c "open(r\"C:\\w\\scenarios\\answers\\S1.json\")"'),
    ("command", "cat tests/holdout/answers/a.json"),
])
def test_the_answers_guard_blocks(field, value):
    code, _, err = run_hook("guard-answers.ps1", {"tool_input": {field: value}})
    assert code == 2 and "scoring scripts" in err


@windows_only
@pytest.mark.parametrize("field, value", [
    ("file_path", r"C:\w\scenarios\S1\site.dxf"),
    ("command", "python scripts/scenarios/score_scenarios.py"),
    ("command", "grep -n answers docs/rules/index.md"),
])
def test_the_answers_guard_passes_the_rest(field, value):
    assert run_hook("guard-answers.ps1", {"tool_input": {field: value}})[0] == 0


@windows_only
def test_sh_guard_passes_a_block_on_and_a_pass_on(tmp_path):
    blocked = {"cwd": str(tmp_path), "tool_input": {"command": "mklink /J a b"}}
    code, _, err = run_hook("sh-guard.ps1", blocked, "guard-inputs.sh")
    assert code == 2 and "mklink" in err
    assert run_hook("sh-guard.ps1", {"cwd": str(tmp_path), "tool_input": {"command": "git status"}}, "guard-inputs.sh")[0] == 0


@pytest.fixture()
def stop_lane(tmp_path):
    """A real repo, its quick tests failing, and its main folder status under steps."""
    repo = tmp_path / "repo"
    (repo / "scripts").mkdir(parents=True)
    (repo / "steps").mkdir()
    (repo / "scripts" / "quick-tests.ps1").write_text("Write-Output 'marker'\nexit 1\n", encoding="utf-8")
    for args in (["init", "-q"], ["config", "user.email", "t@example.com"], ["config", "user.name", "t"]):
        subprocess.run(["git", *args], cwd=repo, check=True, capture_output=True)
    return repo


@windows_only
def test_stop_lets_go_after_two_blocks_and_writes_a_blocker(stop_lane):
    first = run_hook("stop-tests.ps1", {"cwd": str(stop_lane), "stop_hook_active": False})
    assert first[0] == 2 and "block 1 of 2" in first[2]
    second = run_hook("stop-tests.ps1", {"cwd": str(stop_lane), "stop_hook_active": True})
    assert second[0] == 2 and "block 2 of 2" in second[2]
    third = run_hook("stop-tests.ps1", {"cwd": str(stop_lane), "stop_hook_active": True})
    assert third[0] == 0
    status = (stop_lane / "steps" / "STATUS.md").read_bytes()
    assert b"Blocker:" in status and b"\r" not in status


@windows_only
def test_a_new_stop_starts_the_count_again(stop_lane):
    assert "block 1 of 2" in run_hook("stop-tests.ps1", {"cwd": str(stop_lane), "stop_hook_active": False})[2]
    assert "block 1 of 2" in run_hook("stop-tests.ps1", {"cwd": str(stop_lane), "stop_hook_active": False})[2]
