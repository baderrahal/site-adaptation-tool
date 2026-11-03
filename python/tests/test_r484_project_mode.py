"""R484. The project mode of the settings, laid once over designBasis.mode where a build loads its config. Made up names, the shipped config only for its
modes. R484_ProjectModeTests.cs holds the same lines for the C#."""
import copy
import json

import pytest

import design_basis
import level_excel_check
import plan_from_book
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings


def shipped():
    return copy.deepcopy(project_profile.load_settings().values)


def test_r484_no_project_mode_reads_the_mode_of_config():
    values = shipped()
    assert design_basis.take_mode(values, None) is values
    assert design_basis.mode_of(values) == values["designBasis"]["mode"]


def test_r484_the_shipped_config_is_the_past_job_and_a_new_project_starts_on_rev6():
    values = shipped()
    assert values["designBasis"]["mode"] == PAST_JOB
    assert values["designBasis"]["newProjectMode"] == design_basis.REV6


def test_r484_a_project_mode_replaces_the_mode_of_config_for_every_reader():
    values = design_basis.take_mode(shipped(), design_basis.REV6)
    surfaces = values["designBasis"]["roadLevel"]["surfaces"]
    assert design_basis.mode_of(values) == design_basis.REV6
    assert design_basis.road_order(values) == surfaces
    assert design_basis.basis_of(project_profile.ProjectProfile(project_profile.SETTINGS_PATH, values))["surfaces"] == surfaces


def test_r484_the_past_job_laid_over_a_rev6_config_reads_nothing_of_rev6():
    values = basis_settings(mode=design_basis.REV6).values
    design_basis.take_mode(values, PAST_JOB)
    assert design_basis.road_order(values) is None
    assert design_basis.basis_of(project_profile.ProjectProfile(project_profile.SETTINGS_PATH, values)) == {"mode": PAST_JOB}


@pytest.mark.parametrize("mode", ["manual7", "", 7])
def test_r484_a_mode_outside_design_basis_modes_is_refused_by_its_name(mode):
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.take_mode(shipped(), mode)
    assert str(error.value) == "the project mode %s is no mode of designBasis.modes rev6, %s (R484)" % (mode, PAST_JOB)


def test_r484_a_config_with_no_modes_refuses_a_project_mode():
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.take_mode({"designBasis": {"mode": design_basis.REV6}}, design_basis.REV6)
    assert str(error.value) == "the project mode rev6 is no mode of designBasis.modes none (R484)"


def build_args(tmp_path, extra):
    return ["all", "--plots-csv", str(tmp_path / "plots.csv"), "--landxml", str(tmp_path), "--site", str(tmp_path / "site.json"), "--scratch", str(tmp_path),
            "--settings", str(project_profile.SETTINGS_PATH), "--out", str(tmp_path / "plan.json")] + extra


def basis_taken(tmp_path, monkeypatch, extra):
    """The design basis run_build takes on the shipped config with this argument, the run stopped there."""
    taken = {}
    real = design_basis.basis_of

    def basis_of(settings):
        taken["basis"] = real(settings)
        raise project_profile.ProfileError("stopped at the design basis")

    monkeypatch.setattr(design_basis, "basis_of", basis_of)
    assert plan_from_book.main(build_args(tmp_path, extra)) != 0
    return taken["basis"]


def test_r484_the_mode_reaches_the_build_through_its_argument_on_a_shipped_past_job_config(tmp_path, monkeypatch):
    assert basis_taken(tmp_path, monkeypatch, ["--project-mode", design_basis.REV6])["mode"] == design_basis.REV6


def test_r484_a_build_with_no_project_mode_takes_the_past_job_of_the_shipped_config(tmp_path, monkeypatch):
    assert basis_taken(tmp_path, monkeypatch, []) == {"mode": PAST_JOB}


def test_r484_a_build_refuses_an_unknown_project_mode_before_any_step(tmp_path, capsys):
    assert plan_from_book.main(build_args(tmp_path, ["--project-mode", "manual7"])) != 0
    printed = capsys.readouterr()
    assert "the project mode manual7 is no mode of designBasis.modes" in printed.out + printed.err
    assert not (tmp_path / "plan.json").exists()


def test_r484_the_level_excel_check_reads_the_order_under_the_project_mode(tmp_path):
    values = shipped()
    path = tmp_path / "config.json"
    path.write_text(json.dumps(values), encoding="utf-8")
    assert design_basis.road_order(level_excel_check.read_config(path, None)) is None
    assert design_basis.road_order(level_excel_check.read_config(path, design_basis.REV6)) == values["designBasis"]["roadLevel"]["surfaces"]
