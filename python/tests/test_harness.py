import json
import re
import shutil
import subprocess
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
HOOK = ROOT / ".claude" / "hooks" / "guard-main.sh"
BASH_PLACES = ("C:/Program Files/Git/bin/bash.exe", "C:/Program Files/Git/usr/bin/bash.exe")


def find_bash():
    for place in BASH_PLACES:
        if Path(place).is_file():
            return place
    return shutil.which("bash")


def hook(branch, command):
    bash = find_bash()
    if bash is None:
        pytest.skip("no bash on this machine")
    payload = json.dumps({"tool_name": "Bash", "tool_input": {"command": command}})
    done = subprocess.run([bash, HOOK.as_posix()], input=payload, text=True, capture_output=True, env={"GUARD_BRANCH": branch, "PATH": "/usr/bin:/bin"})
    return done.returncode, done.stderr


def test_r18_commit_on_main_is_blocked_and_the_message_says_why():
    code, message = hook("main", "git commit -m x")
    assert code == 2
    assert "Blocked" in message


def test_r18_commit_on_a_round_branch_is_allowed():
    assert hook("feat/round", "git commit -m x")[0] == 0


def test_r18_push_that_names_main_is_blocked_from_any_branch():
    assert hook("feat/round", "git push origin main")[0] == 2
    assert hook("feat/round", "git push origin HEAD:main")[0] == 2
    assert hook("feat/round", "git push -u origin feat/round")[0] == 0


def test_r18_chained_and_multi_line_commands_are_checked_too():
    assert hook("main", "cd repo && git commit -m x")[0] == 2
    assert hook("main", "echo hi\ngit commit -m x")[0] == 2
    assert hook("feat/round", "git commit -m x && git push origin main")[0] == 2


def test_r18_reading_deleting_remote_branches_and_message_text_are_allowed_on_main():
    assert hook("main", "git status && git log --oneline")[0] == 0
    assert hook("main", "git push origin --delete old-branch")[0] == 0
    assert hook("feat/round", 'git commit -m "do not git push origin main"')[0] == 0


def test_r18_settings_register_the_hook_for_bash_and_powershell():
    settings = json.loads((ROOT / ".claude" / "settings.json").read_text(encoding="utf-8"))
    entry = settings["hooks"]["PreToolUse"][0]
    assert entry["matcher"] == "Bash|PowerShell"
    assert "guard-main.sh" in entry["hooks"][0]["command"]


def test_r26_claude_md_is_under_200_lines_and_points_to_the_rules():
    text = (ROOT / "CLAUDE.md").read_text(encoding="utf-8")
    assert len(text.splitlines()) < 200
    assert "docs/rules.md" in text


def test_r26_progress_file_is_at_most_60_lines_with_the_five_parts():
    text = (ROOT / "steps" / "PROGRESS.md").read_text(encoding="utf-8")
    assert len(text.splitlines()) <= 60
    for heading in ("## State", "## Done", "## Open bugs", "## Bader's turn", "## Next three steps"):
        assert heading in text


def test_r26_top_log_entry_fits_one_page_and_has_the_five_headings_in_order():
    text = (ROOT / "steps" / "log.md").read_text(encoding="utf-8")
    top = re.split(r"^## ", text, flags=re.MULTILINE)[1]
    assert len(top.splitlines()) <= 70
    # One page is also a number of characters, the older entries are a few hundred and a stub, the detail goes to steps/history
    assert len(top) <= 9000
    positions = [top.find(heading + ":") for heading in ("Done", "Remains", "Known bugs", "Next", "Your turn")]
    assert all(position >= 0 for position in positions)
    assert positions == sorted(positions)


REGISTRY_HOOK = ROOT / ".claude" / "hooks" / "guard-registry.sh"


def registry_hook(command, tool="PowerShell"):
    bash = find_bash()
    if bash is None:
        pytest.skip("no bash on this machine")
    payload = json.dumps({"tool_name": tool, "tool_input": {"command": command}})
    done = subprocess.run([bash, REGISTRY_HOOK.as_posix()], input=payload, text=True, capture_output=True, env={"PATH": "/usr/bin:/bin"})
    return done.returncode, done.stderr


@pytest.mark.parametrize("command", [
    r"reg add HKCU\Software\X /v a /d 1",
    r"reg.exe query HKLM\SOFTWARE",
    r"cd x && REG delete HKCU\Software\X /f",
    r"New-ItemProperty -Path 'HKCU:\Software\Autodesk\Revit\Autodesk Revit 2024\CodeSigning' -Name a -Value 1",
    r"Set-ItemProperty HKCU:\Software\X -Name a -Value 1",
    r"Remove-ItemProperty -Path HKLM:\SOFTWARE\X -Name a",
    r"New-Item -Force -Path 'HKCU:\Software\Autodesk'",
    r"Remove-Item -Recurse 'Registry::HKEY_CURRENT_USER\Software\X'",
    r"ni HKCU:\Software\X",
    r"sp -Path HKCU:\Software\X -Name a -Value 1",
    r"[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\X')",
    "python -c \"import winreg, winreg.SetValueEx(k, 'a', 0, 4, 1)\"",
])
def test_r106_registry_changes_and_reg_exe_are_blocked(command):
    code, message = registry_hook(command)
    assert code == 2
    assert "guard-registry.sh" in message


@pytest.mark.parametrize("command", [
    r"Remove-Item C:\Temp\old.txt",
    r"New-Item -ItemType Directory -Force C:\Temp\x",
    "git commit -m \"Round 15a test\"",
    r"Get-ChildItem $env:APPDATA\Autodesk",
    r".venv\Scripts\python -m pytest python",
])
def test_r106_file_commands_and_ordinary_work_are_allowed(command):
    assert registry_hook(command)[0] == 0
    assert registry_hook(command, tool="Bash")[0] == 0


def test_r106_settings_register_the_registry_hook_for_bash_and_powershell():
    settings = json.loads((ROOT / ".claude" / "settings.json").read_text(encoding="utf-8"))
    entry = settings["hooks"]["PreToolUse"][0]
    assert entry["matcher"] == "Bash|PowerShell"
    assert any("guard-registry.sh" in hook["command"] for hook in entry["hooks"])


def run_git(*args, cwd=None):
    subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True)


@pytest.fixture()
def repos(tmp_path):
    """A repo on main, a worktree of it on a branch, and a clone on a branch, as git sees them."""
    if shutil.which("git") is None:
        pytest.skip("no git on this machine")
    main = tmp_path / "main"
    main.mkdir()
    run_git("init", "-q", "-b", "main", cwd=main)
    run_git("-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "--allow-empty", "-m", "start", cwd=main)
    run_git("worktree", "add", "-q", "-b", "feat/tree", str(tmp_path / "tree"), cwd=main)
    run_git("clone", "-q", str(main), str(tmp_path / "clone"))
    run_git("checkout", "-q", "-b", "feat/clone", cwd=tmp_path / "clone")
    return {name: (tmp_path / name).as_posix() for name in ("main", "tree", "clone")}


def real_hook(command, project, cwd=None, tool="Bash"):
    """The hook as Claude Code runs it, with no GUARD_BRANCH, so git itself gives the branch of each folder."""
    bash = find_bash()
    if bash is None:
        pytest.skip("no bash on this machine")
    payload = json.dumps({"tool_name": tool, "cwd": cwd or project, "tool_input": {"command": command}})
    env = {"PATH": "/mingw64/bin:/usr/bin:/bin", "CLAUDE_PROJECT_DIR": project, "HOME": project}
    done = subprocess.run([bash, HOOK.as_posix()], input=payload, text=True, capture_output=True, env=env)
    return done.returncode, done.stderr


def test_r18_a_session_started_on_main_may_commit_in_another_clone_on_a_branch(repos):
    assert real_hook("git -C " + repos["clone"] + " commit -m x", repos["main"])[0] == 0
    assert real_hook("cd " + repos["clone"] + " && git commit -m x", repos["main"])[0] == 0
    assert real_hook("git commit -m x", repos["main"])[0] == 2


def test_r18_a_session_started_on_a_branch_is_blocked_on_main_elsewhere(repos):
    code, message = real_hook("git -C " + repos["main"] + " commit -m x", repos["clone"])
    assert code == 2 and "git commit on main" in message
    assert real_hook("cd '" + repos["main"] + "' && git add . && git commit -m x", repos["clone"])[0] == 2
    assert real_hook('bash -c "git -C ' + repos["main"] + ' commit -m x"', repos["clone"])[0] == 2
    assert real_hook("git commit -m x", repos["clone"])[0] == 0


def test_r18_a_worktree_on_a_branch_passes_and_its_main_checkout_does_not(repos):
    assert real_hook("git -C " + repos["tree"] + " commit -m x", repos["main"])[0] == 0
    assert real_hook("git commit -m x", repos["main"], cwd=repos["tree"])[0] == 0
    assert real_hook("git -C " + repos["main"] + " commit -m x", repos["tree"])[0] == 2


def test_r18_powershell_set_location_and_windows_paths_are_followed(repos):
    assert real_hook("Set-Location '" + repos["clone"] + "'" + chr(59) + " git commit -m x", repos["main"], tool="PowerShell")[0] == 0
    assert real_hook("Set-Location -Path '" + repos["main"] + "'" + chr(59) + " git commit -m x", repos["clone"], tool="PowerShell")[0] == 2
    windows = repos["clone"].replace("/", "\\")
    assert real_hook('git -C "' + windows + '" commit -m x', repos["main"])[0] == 0


def test_r18_push_is_judged_by_the_repo_it_runs_in_and_never_to_main(repos):
    assert real_hook("git -C " + repos["clone"] + " push -u origin feat/clone", repos["main"])[0] == 0
    assert real_hook("git -C " + repos["main"] + " push origin feat/x", repos["clone"])[0] == 2
    assert real_hook("git -C " + repos["main"] + " push origin --delete old", repos["clone"])[0] == 0
    assert real_hook("git -C " + repos["clone"] + " push origin HEAD:main", repos["main"])[0] == 2


def test_r18_text_that_only_mentions_a_commit_is_not_a_commit(repos):
    # A heredoc or a message that holds the words is no git command
    assert real_hook("cat >> notes.txt <<'EOF'" + chr(10) + "then git commit on main" + chr(10) + "EOF", repos["main"])[0] == 0
    assert real_hook('git -C ' + repos["clone"] + ' commit -m "git commit on main later"', repos["main"])[0] == 0
