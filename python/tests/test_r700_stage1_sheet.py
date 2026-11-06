"""R700. The scenario run gives the data check the parameter sheet the config names, as the add-in does from the project settings."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts" / "scenarios"))

import run_stage1  # noqa: E402


def request():
    return {"inputs": [{"role": "scanFolder", "path": "f"}, {"role": "config", "path": "c"}]}


def test_r700_a_sheet_the_config_names_beside_it_joins_the_inputs(tmp_path):
    (tmp_path / "config").mkdir()
    config = tmp_path / "config" / "project.json"
    config.write_text(json.dumps({"addin": {"parameterSheet": "config/sheet.xlsx"}}), encoding="utf-8")
    (tmp_path / "config" / "sheet.xlsx").write_bytes(b"x")
    roles = run_stage1.with_sheet(request(), config)["inputs"]
    assert roles[-1] == {"role": "parameterSheet", "path": str((tmp_path / "config" / "sheet.xlsx").resolve())}


def test_r700_no_sheet_named_or_none_on_disk_adds_nothing(tmp_path):
    config = tmp_path / "project.json"
    config.write_text(json.dumps({"addin": {}}), encoding="utf-8")
    assert len(run_stage1.with_sheet(request(), config)["inputs"]) == 2
    config.write_text(json.dumps({"addin": {"parameterSheet": "config/gone.xlsx"}}), encoding="utf-8")
    assert len(run_stage1.with_sheet(request(), config)["inputs"]) == 2
