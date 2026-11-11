"""scripts/set-machine.ps1 writes a machine file the readers of the work root take, and refuses a root they would refuse."""
import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "scenarios"))
import scenario_tools as tools  # noqa: E402

pytestmark = pytest.mark.skipif(sys.platform != "win32" or shutil.which("powershell") is None, reason="the script is Windows PowerShell")


def run(app_data, work_root):
    return subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(ROOT / "scripts" / "set-machine.ps1"), "-WorkRoot", str(work_root), "-AppData", str(app_data)],
                          capture_output=True, text=True, encoding="utf-8", errors="replace")


def test_writes_the_root_the_readers_take(tmp_path):
    work = tmp_path / "work" / "SiteAdaptation"
    done = run(tmp_path / "appdata", work)
    assert done.returncode == 0, done.stdout + done.stderr
    assert work.is_dir()
    assert tools.work_root(environ={"APPDATA": str(tmp_path / "appdata")}) == work


def test_keeps_the_other_keys(tmp_path):
    machine = tmp_path / "appdata" / "SiteAdaptation" / "machine.json"
    machine.parent.mkdir(parents=True)
    machine.write_text(json.dumps({"workRoot": "C:\\old\\SiteAdaptation", "other": 3}), encoding="utf-8")
    assert run(tmp_path / "appdata", tmp_path / "SiteAdaptation").returncode == 0
    assert json.loads(machine.read_text(encoding="utf-8"))["other"] == 3


@pytest.mark.parametrize("root", [r"C:\SA-work", r"SiteAdaptation", r"C:\a\..\SiteAdaptation", r"C:\02-Site Adaptation\SiteAdaptation", "C:\\sa-test-none\\SiteAdaptation\\"])
def test_refuses_a_root_the_readers_refuse(tmp_path, root):
    done = run(tmp_path / "appdata", root)
    assert done.returncode == 1
    assert "REFUSED" in done.stdout
    # The root that ends in a backslash is a plain full path, so it is the name check that refuses it
    if root.endswith("\\"):
        assert "last folder" in done.stdout
    assert not (tmp_path / "appdata" / "SiteAdaptation" / "machine.json").exists()
