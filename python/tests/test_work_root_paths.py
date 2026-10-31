import json
from pathlib import Path, PureWindowsPath

import pytest

import project_profile

REPO = Path(project_profile.__file__).resolve().parent.parent


@pytest.mark.parametrize("name", ["project.json", "road_surfaces.json"])
def test_q127_j5_no_path_of_a_user_is_in_config(name):
    text = (REPO / "config" / name).read_text(encoding="utf-8").replace("\\\\", "/").lower()
    assert "c:/users/" not in text


def test_q127_j5_a_path_under_the_work_root_mark_takes_the_root_of_the_machine(tmp_path):
    root = tmp_path / "SiteAdaptation"
    values = {"a": "{workRoot}" + chr(92) + "round_addin", "b": ["{workRoot}/x/y.json"], "c": "C:/02-Site Adaptation/z.dwg", "d": 1}
    found = project_profile.with_work_root(values, root)
    assert found == {"a": str(root / "round_addin"), "b": [str(root / "x" / "y.json")], "c": "C:/02-Site Adaptation/z.dwg", "d": 1}
    assert project_profile.with_work_root("{workRoot}", root) == str(root)


def test_q127_j5_the_work_root_is_that_of_machine_json_else_documents(tmp_path):
    assert project_profile.work_root({}, tmp_path) == tmp_path / "Documents" / "SiteAdaptation"
    machine = tmp_path / "app" / "SiteAdaptation" / "machine.json"
    machine.parent.mkdir(parents=True)
    machine.write_text(json.dumps({"workRoot": str(PureWindowsPath("D:/", "work", "SiteAdaptation"))}), encoding="utf-8")
    assert str(project_profile.work_root({"APPDATA": str(tmp_path / "app")}, tmp_path)).replace("\\", "/").endswith("work/SiteAdaptation")
    machine.write_text(json.dumps({"workRoot": str(PureWindowsPath("D:/", "work", "Other"))}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="named SiteAdaptation"):
        project_profile.work_root({"APPDATA": str(tmp_path / "app")}, tmp_path)


def test_q127_j5_the_settings_loader_gives_no_mark():
    settings = project_profile.load_settings()
    assert not settings.get("siteBuild.folder").startswith(project_profile.WORK_ROOT_MARK)
    assert Path(settings.get("siteBuild.folder")).name == "round_addin"
