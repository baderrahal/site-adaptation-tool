"""Runs the lane hooks in made up lane folders: a main repo folder, and worktrees named revit-test, build-1 and agent-x."""
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
HOOKS = ROOT / ".claude" / "hooks"
pytestmark = pytest.mark.skipif(sys.platform != "win32" or shutil.which("powershell") is None, reason="the lane hooks are Windows PowerShell")

TASK = "---\nid: T1\nlane: build-1\nstage: 1\nstatus: open\nallowed:\n  - python/\n---\n"
DONE = "---\nid: T0\nlane: build-1\nstatus: done\nallowed:\n  - docs/\n---\n"


def run_hook(name, payload, **env):
    merged = dict(os.environ)
    merged.update(env)
    done = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(HOOKS / name)], input=json.dumps(payload), capture_output=True, text=True, encoding="utf-8", errors="replace", env=merged)
    return done.returncode, done.stdout, done.stderr


@pytest.fixture()
def lanes(tmp_path):
    main = tmp_path / "repo"
    (main / ".git" / "worktrees").mkdir(parents=True)
    (main / "config").mkdir()
    (main / "config" / "project.json").write_text(json.dumps({"inputRoot": str(tmp_path / "inputs"), "siteBuild": {"lock": str(tmp_path / "window.lock"), "maxWindows": 3, "windowProcesses": ["no-such-process"]}}), encoding="utf-8")
    (main / "steps" / "tasks").mkdir(parents=True)
    found = {"main": main, "tmp": tmp_path}
    for name in ("revit-test", "build-1", "build-2", "agent-x"):
        (main / ".git" / "worktrees" / name).mkdir()
        tree = main / ".claude" / "worktrees" / name
        (tree / "steps" / "tasks").mkdir(parents=True)
        (tree / "config").mkdir()
        shutil.copy(main / "config" / "project.json", tree / "config" / "project.json")
        (tree / ".git").write_text("gitdir: %s\n" % (main / ".git" / "worktrees" / name), encoding="utf-8")
        found[name] = tree
    (found["build-1"] / "steps" / "tasks" / "T1.md").write_text(TASK, encoding="utf-8")
    (found["build-1"] / "steps" / "tasks" / "T0.md").write_text(DONE, encoding="utf-8")
    return found


def edit(folder, path):
    return run_hook("guard-edit.ps1", {"cwd": str(folder), "tool_name": "Edit", "tool_input": {"file_path": str(path)}})


def test_lead_edits_anywhere_in_a_folder(lanes):
    (lanes["main"] / "python").mkdir()
    assert edit(lanes["main"], lanes["main"] / "python" / "a.py")[0] == 0
    assert edit(lanes["main"], lanes["main"] / "tests" / "golden" / "a.json")[0] == 0


def test_builder_edits_inside_its_task(lanes):
    tree = lanes["build-1"]
    assert edit(tree, tree / "python" / "a.py")[0] == 0
    assert edit(tree, tree / "steps" / "tasks" / "T1.md")[0] == 0


def test_a_relative_path_is_read_from_the_lane_folder(lanes):
    tree = lanes["build-1"]
    assert edit(tree, "python/a.py")[0] == 0
    assert edit(tree, "dotnet/a.cs")[0] == 2


def test_builder_is_blocked_outside_its_task(lanes):
    tree = lanes["build-1"]
    code, _, err = edit(tree, tree / "dotnet" / "a.cs")
    assert code == 2 and "allowed folders of task T1" in err
    # A folder whose name starts the same is not that folder, and a done task gives no folders
    assert edit(tree, tree / "pythonic" / "a.py")[0] == 2
    assert edit(tree, tree / "docs" / "a.md")[0] == 2


@pytest.mark.parametrize("name", ["acceptance", "golden", "holdout"])
def test_lead_tests_are_blocked_for_a_builder_even_if_allowed(lanes, name):
    tree = lanes["build-1"]
    (tree / "steps" / "tasks" / "T1.md").write_text(TASK.replace("  - python/\n", "  - tests/\n"), encoding="utf-8")
    code, _, err = edit(tree, tree / "tests" / name / "a.py")
    assert code == 2 and "owned by the lead" in err


def test_a_lane_with_no_open_task_edits_nothing(lanes):
    for name in ("build-2", "revit-test", "agent-x"):
        code, _, err = edit(lanes[name], lanes[name] / "python" / "a.py")
        assert code == 2 and "no open task" in err and name in err


def test_a_worktree_may_not_edit_the_main_folder(lanes):
    code, _, err = edit(lanes["build-1"], lanes["main"] / "python" / "a.py")
    assert code == 2 and "main repo folder" in err


def test_new_root_file_blocked_old_root_file_passes(lanes):
    main = lanes["main"]
    assert "repo root" in edit(main, main / "notes.txt")[2]
    assert edit(main, main / "CLAUDE.md")[0] == 0
    (main / "README.md").write_text("x", encoding="utf-8")
    assert edit(main, main / "README.md")[0] == 0


def test_input_folders_are_blocked_for_every_role(lanes):
    for folder in (lanes["main"], lanes["build-1"]):
        code, _, err = edit(folder, lanes["tmp"] / "inputs" / "a.xlsx")
        assert code == 2 and "input folder" in err
        assert edit(folder, r"C:\02-Site Adaptation\a.xlsx")[0] == 2
        assert edit(folder, r"c:\10- roshen mecca\a.xlsx")[0] == 2


def test_status_file_is_never_edited_by_hand(lanes):
    main = lanes["main"]
    assert "scripts/status.ps1" in edit(main, main / "steps" / "STATUS.md")[2]


def test_outside_the_repo_a_lane_writes_only_to_scratch_and_work_folders(lanes):
    tree = lanes["build-1"]
    assert edit(tree, Path(os.environ["TEMP"]) / "scratch" / "a.txt")[0] == 0
    assert edit(tree, Path.home() / "Documents" / "SiteAdaptation" / "logs" / "a.txt")[0] == 0
    assert edit(tree, Path.home() / ".claude" / "memory" / "a.md")[0] == 0
    code, _, err = edit(tree, r"C:\SA-demo\config\project.json")
    assert code == 2 and "outside this worktree" in err
    assert edit(lanes["main"], r"C:\SA-demo\config\project.json")[0] == 0


def test_an_input_folder_is_blocked_from_a_session_outside_any_repo(tmp_path):
    assert edit(tmp_path, r"C:\02-Site Adaptation\a.xlsx")[0] == 2
    assert edit(tmp_path, tmp_path / "a.txt")[0] == 0


def bash(folder, command):
    return run_hook("guard-revit.ps1", {"cwd": str(folder), "tool_name": "Bash", "tool_input": {"command": command}})


STARTS = [
    r'"C:\Program Files\Autodesk\Revit 2024\Revit.exe" a.rvt',
    "Revit.exe",
    r"& 'C:\Program Files\Autodesk\Revit 2024\Revit.exe'",
    "Start-Process Revit",
    r"Start-Process -FilePath 'C:\Program Files\Autodesk\Revit 2024\Revit.exe'",
    r"cd a && pwsh dotnet\SiteAdaptation.TestHost\Run-TestHost.ps1 -RoundSpec x",
    r".venv\Scripts\python python\window_lock.py --round r --label l -- pwsh dotnet\SiteAdaptation.TestHost\Run-TestHost.ps1 -RoundSpec x",
    r".venv\Scripts\python python\window_lock.py --round r --label l -- pwsh dotnet\SiteAdaptation.TestHost\Start-RevitListener.ps1",
    "dotnet test dotnet/SiteAdaptation.RevitTests",
    r"python python\place_codes.py plan.json --round r --first-code 1",
    r".\Run-TestHost -RoundSpec x",
    r"Start-Process C:\SA-demo\copy.rvt",
    r"Invoke-Item 'C:\SA-demo\copy.rvt'",
    r"C:\Autodesk\revit",
    r"""pwsh -c "& 'C:\Autodesk\Revit.exe' a.rvt" """,
    "cmd /c start Revit",
]
READS = [
    'tasklist | grep -i -E "python|Revit|acad"',
    "grep -rn Revit.exe dotnet",
    "cat dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1",
    "git log --grep Revit.exe",
    'git commit -m "start Revit.exe"',
    "Get-Process Revit",
    "ls Revit",
    "dotnet build dotnet/SiteAdaptation.AddIn/SiteAdaptation.AddIn.csproj",
    r".venv\Scripts\python python\window_lock.py --status --round r --label revit",
    'gh pr create --body "Line one\nStart-Process Revit\nRun-TestHost.ps1"',
    "Get-CimInstance Win32_Process -Filter \"Name='Revit.exe'\"",
    "Get-Process | Where-Object { $_.Path -like '*Revit.exe' }",
    "cp a.rvt b.rvt",
    r"C:\Git\usr\bin\grep.exe Revit.exe a.txt",
    "python -m pytest python/tests/test_round16.py -k place_codes",
    "python -m py_compile python/place_codes.py",
    'python -m pytest python/tests -k "start and revit"',
    "cmd /c dir *.rvt",
    'pwsh -NoProfile -File scripts/status.ps1 -Note "open each lane, start revit-test again"',
    'pwsh -NoProfile -Command "Get-Process Revit"',
]


@pytest.mark.parametrize("command", STARTS)
def test_starting_revit_is_blocked_for_other_lanes(lanes, command):
    for name in ("main", "build-1", "agent-x"):
        code, _, err = bash(lanes[name], command)
        assert code == 2 and "only the worktree named revit-test" in err


@pytest.mark.parametrize("command", STARTS)
def test_starting_revit_passes_for_the_tester_when_free(lanes, command):
    assert bash(lanes["revit-test"], command)[0] == 0


@pytest.mark.parametrize("command", READS)
def test_reading_about_revit_passes_for_every_lane(lanes, command):
    assert bash(lanes["build-1"], command)[0] == 0


def set_build_config(tree, **values):
    path = tree / "config" / "project.json"
    config = json.loads(path.read_text(encoding="utf-8"))
    config["siteBuild"].update(values)
    path.write_text(json.dumps(config), encoding="utf-8")


def test_the_tester_waits_on_the_lock(lanes):
    (lanes["tmp"] / "window.lock").write_text("held", encoding="utf-8")
    code, _, err = bash(lanes["revit-test"], "Revit.exe")
    assert code == 2 and "lock is held" in err


def test_the_lock_is_free_while_any_slot_is_free(lanes):
    tree = lanes["revit-test"]
    set_build_config(tree, revitSlots=2, revitSlotsMinTotalGB=0)
    (lanes["tmp"] / "window.lock").write_text("held", encoding="utf-8")
    assert bash(tree, "Revit.exe")[0] == 0
    (lanes["tmp"] / "window-2.lock").write_text("held", encoding="utf-8")
    code, _, err = bash(tree, "Revit.exe")
    assert code == 2 and "every slot is taken" in err
    # A machine under the size has the one slot, as python/window_lock.py gives it
    set_build_config(tree, revitSlots=2, revitSlotsMinTotalGB=100000)
    (lanes["tmp"] / "window-2.lock").unlink()
    assert bash(tree, "Revit.exe")[0] == 2


def test_the_tester_waits_at_the_window_limit(lanes):
    tree = lanes["revit-test"]
    # The hook itself runs in powershell, so one window of that name stands
    set_build_config(tree, maxWindows=1, windowProcesses=["powershell"])
    code, _, err = bash(tree, "Revit.exe")
    assert code == 2 and "Civil 3D windows are open" in err
    set_build_config(tree, maxWindows=100000, windowProcesses=["powershell"])
    assert bash(tree, "Revit.exe")[0] == 0


def test_a_job_to_the_listener_needs_the_role_and_not_the_lock(lanes):
    command = r"pwsh dotnet\SiteAdaptation.TestHost\Send-RevitJob.ps1 -RoundSpec x -Slot 1"
    (lanes["tmp"] / "window.lock").write_text("held by the listener", encoding="utf-8")
    assert bash(lanes["revit-test"], command)[0] == 0
    code, _, err = bash(lanes["build-1"], command)
    assert code == 2 and "only the worktree named revit-test" in err


def test_notify_writes_one_line_per_lane_to_the_main_folder(lanes):
    status = lanes["main"] / "steps" / "STATUS.md"
    payload = {"cwd": str(lanes["build-1"]), "message": "x"}
    for _ in range(2):
        assert run_hook("notify.ps1", payload, SITE_ADAPTATION_HOOK_NO_ALERT="1")[0] == 0
    assert run_hook("notify.ps1", {"cwd": str(lanes["main"])}, SITE_ADAPTATION_HOOK_NO_ALERT="1")[0] == 0
    text = status.read_text(encoding="utf-8")
    assert text.count("build-1 waiting for you, ") == 1 and text.count("lead waiting for you, ") == 1
    # R343 scans every file of steps on disk, a byte order mark or a carriage return here would fail it on any lane
    data = status.read_bytes()
    assert b"\r" not in data and not data.startswith(b"\xef\xbb\xbf")
    # An answer from Bader takes that lane's line away and leaves the others
    assert run_hook("notify.ps1", {"cwd": str(lanes["build-1"]), "hook_event_name": "UserPromptSubmit"}, SITE_ADAPTATION_HOOK_NO_ALERT="1")[0] == 0
    text = status.read_text(encoding="utf-8")
    assert "build-1 waiting" not in text and "lead waiting" in text
    assert not (lanes["build-1"] / "steps" / "STATUS.md").exists()


def test_session_start_prints_task_and_status(lanes):
    (lanes["main"] / "steps" / "STATUS.md").write_text("# Status\nmarker line\n", encoding="utf-8")
    code, out, _ = run_hook("session-start.ps1", {"cwd": str(lanes["build-1"])})
    assert code == 0 and "role: builder" in out and "T1.md" in out and "id: T1" in out and "marker line" in out and "id: T0" not in out
    code, out, _ = run_hook("session-start.ps1", {"cwd": str(lanes["revit-test"])})
    assert code == 0 and "role: revit tester" in out and "none open" in out
    assert "role: lead" in run_hook("session-start.ps1", {"cwd": str(lanes["main"])})[1]


def git_in(folder, *args):
    subprocess.run(["git", *args], cwd=folder, check=True, capture_output=True)


@pytest.fixture()
def real_worktree(tmp_path):
    """A real repo with a task merged on origin/main, and a real worktree of it named build-1."""
    repo = tmp_path / "repo"
    (repo / "steps" / "tasks").mkdir(parents=True)
    (repo / "config").mkdir()
    (repo / "config" / "project.json").write_text("{}", encoding="utf-8")
    (repo / "steps" / "tasks" / "T1.md").write_text(TASK, encoding="utf-8")
    git_in(repo, "init", "-q")
    git_in(repo, "config", "user.email", "t@example.com")
    git_in(repo, "config", "user.name", "t")
    git_in(repo, "add", ".")
    git_in(repo, "commit", "-q", "-m", "base")
    git_in(repo, "update-ref", "refs/remotes/origin/main", "HEAD")
    tree = tmp_path / "trees" / "build-1"
    git_in(repo, "worktree", "add", "-q", "--detach", str(tree), "HEAD")
    return tree


def test_a_lane_cannot_widen_its_own_task_and_status_done_does_not_lock_it_out(real_worktree):
    tree = real_worktree
    task = tree / "steps" / "tasks" / "T1.md"
    task.write_text(TASK.replace("  - python/\n", "  - python/\n  - dotnet/\n").replace("status: open", "status: done"), encoding="utf-8")
    assert edit(tree, tree / "python" / "a.py")[0] == 0
    code, _, err = edit(tree, tree / "dotnet" / "a.cs")
    assert code == 2 and "allowed folders of task T1" in err


def test_the_branch_names_the_task_when_a_lane_holds_two(tmp_path):
    repo = tmp_path / "repo"
    (repo / "steps" / "tasks").mkdir(parents=True)
    (repo / "config").mkdir()
    (repo / "config" / "project.json").write_text("{}", encoding="utf-8")
    other = TASK.replace("id: T1", "id: A0").replace("  - python/\n", "  - docs/\n")
    (repo / "steps" / "tasks" / "A0.md").write_text(other, encoding="utf-8")
    (repo / "steps" / "tasks" / "T1.md").write_text(TASK, encoding="utf-8")
    git_in(repo, "init", "-q")
    git_in(repo, "config", "user.email", "t@example.com")
    git_in(repo, "config", "user.name", "t")
    git_in(repo, "add", ".")
    git_in(repo, "commit", "-q", "-m", "base")
    git_in(repo, "update-ref", "refs/remotes/origin/main", "HEAD")
    tree = tmp_path / "trees" / "build-1"
    git_in(repo, "worktree", "add", "-q", "--detach", str(tree), "HEAD")
    # On no task branch the first by name stands
    assert edit(tree, tree / "docs" / "a.md")[0] == 0
    assert edit(tree, tree / "python" / "a.py")[0] == 2
    git_in(tree, "checkout", "-q", "-b", "task/T1")
    assert edit(tree, tree / "python" / "a.py")[0] == 0
    assert edit(tree, tree / "docs" / "a.md")[0] == 2


def test_a_waiting_task_gives_no_folders(real_worktree):
    tree = real_worktree
    (tree / "steps" / "tasks" / "T1.md").write_text(TASK, encoding="utf-8")
    repo = tree.parent.parent / "repo"
    (repo / "steps" / "tasks" / "T1.md").write_text(TASK.replace("status: open", "status: waiting"), encoding="utf-8")
    git_in(repo, "add", ".")
    git_in(repo, "commit", "-q", "-m", "waiting")
    git_in(repo, "update-ref", "refs/remotes/origin/main", "HEAD")
    assert edit(tree, tree / "python" / "a.py")[0] == 2


def test_a_task_not_merged_gives_no_folders(real_worktree):
    tree = real_worktree
    (tree / "steps" / "tasks" / "T2.md").write_text(TASK.replace("id: T1", "id: T2").replace("  - python/\n", "  - dotnet/\n"), encoding="utf-8")
    assert edit(tree, tree / "dotnet" / "a.cs")[0] == 2


@pytest.fixture()
def real_git_lane(tmp_path):
    """A real repo with a failing or passing quick test script, for the Stop hook."""
    repo = tmp_path / "repo"
    (repo / "scripts").mkdir(parents=True)
    (repo / ".claude").mkdir()
    shutil.copytree(HOOKS, repo / ".claude" / "hooks")
    for args in (["init", "-q"], ["config", "user.email", "t@example.com"], ["config", "user.name", "t"]):
        subprocess.run(["git", *args], cwd=repo, check=True, capture_output=True)
    return repo


def set_quick(repo, code):
    """The script logs each run outside the repo, so a test can see whether it ran."""
    log = repo.parent / "runs.txt"
    lines = ["Add-Content -LiteralPath '%s' -Value run" % log, "Write-Output 'marker of the quick tests'", "exit %d" % code]
    (repo / "scripts" / "quick-tests.ps1").write_text("\n".join(lines) + "\n", encoding="utf-8")


def runs(repo):
    log = repo.parent / "runs.txt"
    return len(log.read_text(encoding="utf-8").splitlines()) if log.exists() else 0


def test_stop_blocks_on_a_failure_and_shows_it_then_gives_up_after_two(real_git_lane):
    repo = real_git_lane
    set_quick(repo, 1)
    for block in (1, 2):
        code, _, err = run_hook("stop-tests.ps1", {"cwd": str(repo), "stop_hook_active": block > 1})
        assert code == 2 and "marker of the quick tests" in err and "block %d of 2" % block in err
    code, _, err = run_hook("stop-tests.ps1", {"cwd": str(repo), "stop_hook_active": True})
    assert code == 0 and "still fail" in err


def test_stop_skips_a_folder_with_nothing_changed_and_reruns_on_a_second_edit(real_git_lane):
    repo = real_git_lane
    set_quick(repo, 0)
    tracked = repo / "a.txt"
    tracked.write_text("one", encoding="utf-8")
    subprocess.run(["git", "add", "a.txt"], cwd=repo, check=True, capture_output=True)
    subprocess.run(["git", "commit", "-q", "-m", "one"], cwd=repo, check=True, capture_output=True)
    tracked.write_text("two", encoding="utf-8")
    assert run_hook("stop-tests.ps1", {"cwd": str(repo)})[0] == 0
    assert runs(repo) == 1
    assert run_hook("stop-tests.ps1", {"cwd": str(repo)})[0] == 0
    assert runs(repo) == 1
    # The same file changed again, git status reads the same and the tests must still run
    tracked.write_text("three, a longer text", encoding="utf-8")
    assert run_hook("stop-tests.ps1", {"cwd": str(repo)})[0] == 0
    assert runs(repo) == 2
