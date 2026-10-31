"""R407. The design basis switch of round 4i on the made up row of test_level_floor_steps.py built in code (R10): designBasis.mode read once,
a mode outside designBasis.modes refused by its name, the past job reading none of the blocks of rev6 and building as before round 4i with the
new step fields null, and the mode in the run block of a build step. DesignBasisTests.cs holds the same made up data for the C#."""
import argparse
import copy
import json

import pytest

import common
import design_basis
import parity
import plan_from_book
import project_profile
import test_level_floor_steps as made

# The mode of the past job as config/project.json names it, read from config so no project name stands in code (R63)
PAST_JOB = next(mode for mode in project_profile.load_settings().get("designBasis")["modes"] if mode != design_basis.REV6)


def basis_settings(**changes):
    """config/project.json with designBasis changed: mode, door (the doorLevel keys), civil (the civilFile keys), surface and floor."""
    settings = project_profile.load_settings()
    values = copy.deepcopy(settings.values)
    block = values["designBasis"]
    if "mode" in changes:
        block["mode"] = changes["mode"]
    if "modes" in changes:
        block["modes"] = changes["modes"]
    block["doorLevel"].update(changes.get("door") or {})
    block["doorLevel"]["civilFile"].update(changes.get("civil") or {})
    if "surface" in changes:
        block["roadLevel"]["surface"] = changes["surface"]
    if "floor" in changes:
        block["backYard"]["floor"] = changes["floor"]
    return project_profile.ProjectProfile(settings.path, values)


def civil_csv(tmp_path, rows, header=("Plot No.", "El @ Entry")):
    """A made up civil file of door levels, one row per plot."""
    path = tmp_path / "civil.csv"
    path.write_text("\n".join([",".join(header)] + [",".join(str(cell) for cell in row) for row in rows]) + "\n", encoding="utf-8")
    return path


def rev6_build(monkeypatch, settings, **options):
    """The made up build of test_level_floor_steps.made_build on settings, the steps overrides and summary with it."""
    monkeypatch.setattr(project_profile, "load_settings", lambda path=None: settings)
    built = made.made_build(monkeypatch, **options)
    trace, rules = built["trace"], built["rules"]
    built["overrides"] = plan_from_book.override_step(built["scope"], built["rows"], trace, built["hands"], built["files"], rules, built["profile"], 0.5)
    plan = {"plots": built["plots"], "summary": built["summary"], "conflicts": built["conflicts"]}
    built["summaryStep"] = plan_from_book.summary_step(built["scope"], built["rows"], plan)
    return built


def test_r407_a_mode_outside_design_basis_modes_is_refused_by_its_name():
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="manual7"))
    assert str(error.value) == "designBasis.mode manual7 is no mode of designBasis.modes rev6, %s (R407)" % PAST_JOB
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode=None))
    assert "designBasis.mode none is no mode" in str(error.value)
    # modes must hold rev6 and the one mode of the past job
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(modes=[PAST_JOB, "other"]))
    assert str(error.value) == "designBasis.modes must hold rev6 and the mode of the past job, it holds %s, other (R407)" % PAST_JOB


def test_r407_the_past_job_reads_none_of_door_level_road_level_and_back_yard():
    # Every block of rev6 broken, the past job reads the mode alone
    settings = basis_settings(mode=PAST_JOB, door={"source": "nothing", "slopePercent": None}, civil={"path": None}, surface=None, floor=True)
    assert design_basis.basis_of(settings) == {"mode": PAST_JOB}
    assert design_basis.is_rev6(design_basis.basis_of(settings)) is False


def test_r407_the_config_of_today_runs_the_past_job():
    assert project_profile.load_settings().get("designBasis")["mode"] == PAST_JOB


def test_r407_the_switch_back_to_the_past_job_gives_the_values_of_before_round_4i_with_the_new_fields_null(monkeypatch, tmp_path):
    rev6 = rev6_build(monkeypatch, basis_settings(mode="rev6"))
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    levels, floors = past["levels"]["5"], past["floors"]["5"]
    # El @ Entry by keyRules, the road at front plus offsetM 0 plus the slope of keyRules along the ramp, the ramp class of keyRules
    assert levels["entry"] == pytest.approx(made.entry_of(10.54), abs=1e-12)
    assert (levels["doorSource"], levels["slopePercent"], floors["yardGrade"]) == (None, None, None)
    assert (floors["rampClass"], floors["rampType"], floors["yardDepth"]) == ("0.5", "Ramps 0.5%", 0.1)
    assert "yard" in past["plan"]["5"]["floors"] and "noDoorLevel" not in past["summary"] and "slopeOutsideManual" not in past["summary"]
    # The table summary holds the lists of rev6 as null on the past job
    assert past["summaryStep"][1]["summary"]["noDoorLevel"] is None and past["summaryStep"][1]["summary"]["slopeOutsideManual"] is None
    # rev6 between the two builds left no trace: the plan of the past job is the plan of a build of the past job alone
    again = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    assert json.dumps(again["plots"], sort_keys=True) == json.dumps(past["plots"], sort_keys=True)
    assert rev6["levels"]["5"]["doorSource"] == "roadPlusSlope"


def test_r407_the_mode_is_read_once_by_rule_inputs_when_the_run_gives_none(monkeypatch):
    calls = []
    real = design_basis.basis_of
    monkeypatch.setattr(design_basis, "basis_of", lambda settings: calls.append(1) or real(settings))
    built = made.made_build(monkeypatch)
    assert calls == [1] and built["rules"]["basis"] == {"mode": PAST_JOB}


def test_r407_the_run_block_of_a_build_step_names_the_design_basis(tmp_path):
    settings = project_profile.load_settings()
    args = argparse.Namespace(steps=str(tmp_path), cover="all", settings=str(project_profile.SETTINGS_PATH), baseplan=None, plots_csv=None, labels=None,
                              footprint_blocks=None, sales=None, roads=None, overrides=None, design_basis=PAST_JOB)
    plan_from_book.write_build_step(args, settings, "levels", {})
    payload = json.loads((tmp_path / parity.step_name("levels", "python")).read_text(encoding="utf-8"))
    assert payload["run"]["designBasis"] == PAST_JOB and list(payload["run"])[-1] == "designBasis"


def test_r407_the_entry_of_rev6_rounds_to_the_4_decimals_of_r2(monkeypatch):
    built = rev6_build(monkeypatch, basis_settings(mode="rev6"))
    assert built["plan"]["5"]["levels"]["entry"]["value"] == common.fixed(10.54 + 0.005 * 5.52, 4)
