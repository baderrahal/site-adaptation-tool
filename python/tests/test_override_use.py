"""R386. Use and skip in the override list: the Use of a check table row, Rule, Past or Skip, kept by a majority run and honoured by a
build, so the C# of step 12 compares the same applied, left and skipped. Every name and number here is made up (R10)."""
import json

import pytest

import majority
import override_source
import plan_rules
import project_profile
from test_addin_review import GROUPS, entry, keep_block

SKIP = [{"plot": "2", "reason": "the client builds this plot later", "byHand": True}]


def write_list(path, overrides, **parts):
    path.write_text(json.dumps({"note": "n", "overrides": overrides, **parts}), encoding="utf-8")
    return path


def test_r386_a_skip_list_survives_a_majority_run(tmp_path):
    path = write_list(tmp_path / "list.json", [entry("1", "mirrored", True, "native", "U11.rvt", "u11")], cannot=[], skip=SKIP)
    # The skip list of the list a run writes over is kept whole, after the cannot list, whatever the run measures
    skip = majority.final_skip(path, None)
    assert skip == SKIP
    run = [entry("3", "rampType", "flat", "parsons", "IMC.rvt", "imc")]
    listing, _ = majority.final_list(run, keep_block(), dry=False, path=path, keep=[], replace=[], keep_from=None, joined=None)
    majority.write_overrides(path, "n", listing, [], skip)
    data = json.loads(path.read_text(encoding="utf-8"))
    assert list(data) == ["note", "overrides", "cannot", "skip"] and data["skip"] == SKIP
    # One skip entry per line, as the overrides, so Bader finds a plot by hand
    assert '    {"plot": "2", "reason": "the client builds this plot later", "byHand": true}' in path.read_text(encoding="utf-8").splitlines()
    # --keep-from reads the skip of the list it writes over too, a plot the two lists skip otherwise is refused with both named
    older = write_list(tmp_path / "older.json", [], skip=[{"plot": "5", "reason": "r", "byHand": True}])
    assert majority.final_skip(path, older) == [{"plot": "2", "reason": "the client builds this plot later", "byHand": True}, {"plot": "5", "reason": "r", "byHand": True}]
    write_list(older, [], skip=[{"plot": "2", "reason": "another reason", "byHand": True}])
    with pytest.raises(project_profile.ProfileError, match="plot 2"):
        majority.final_skip(path, older)
    # The migrate of R330 keeps the cannot and the skip lists of the list it rewrites
    cannot = [{"plot": "4", "field": "entryM", "source": "parsons", "model": "IMC.rvt", "key": "imc", "decision": "parsons entry", "reason": "no value"}]
    moved = write_list(tmp_path / "moved.json", [entry("1", "mirrored", True, "native", "U11.rvt", "u11")], cannot=cannot, skip=SKIP)
    settings = tmp_path / "project.json"
    values = json.loads(json.dumps(project_profile.load_settings().values))
    values["majority"]["groups"] = GROUPS
    settings.write_text(json.dumps(values), encoding="utf-8")
    assert override_source.main(["migrate", str(moved), "--settings", str(settings)]) == 0
    data = json.loads(moved.read_text(encoding="utf-8"))
    assert data["cannot"] == cannot and data["skip"] == SKIP


@pytest.mark.parametrize("bad", [
    [{"plot": "2", "reason": "r"}],
    [{"plot": "2", "reason": "r", "byHand": "true"}],
    [{"plot": "2", "reason": "", "byHand": True}],
    [{"plot": 2, "reason": "r", "byHand": True}],
    [{"plot": "2", "reason": "r", "byHand": True, "field": "entryM"}],
    [{"plot": "2", "reason": "r", "byHand": True}, {"plot": "2", "reason": "s", "byHand": True}],
    {"plot": "2"},
])
def test_r386_a_skip_entry_holds_plot_reason_and_by_hand_true_once_per_plot_or_the_list_is_refused(tmp_path, bad):
    path = write_list(tmp_path / "list.json", [entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc")], skip=bad)
    with pytest.raises(project_profile.ProfileError, match="R386"):
        plan_rules.load_overrides(path, {"parsons"}, None, GROUPS)
    with pytest.raises(project_profile.ProfileError, match="R386"):
        majority.final_skip(path, None)


@pytest.mark.parametrize("item, why", [
    (entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc", use="Rule", byHand=True), "rule or past"),
    (entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc", use="skip", byHand=True), "rule or past"),
    (entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc", use="rule"), "byHand"),
    (entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc", Use="rule", byHand=True), "spell it"),
])
def test_r386_use_is_rule_or_past_on_an_entry_marked_by_hand_or_the_list_is_refused(item, why):
    with pytest.raises(project_profile.ProfileError, match=why):
        override_source.check_entries([item], GROUPS)
    good = entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc", use="past", byHand=True)
    assert override_source.check_entries([good], GROUPS) == [good] and override_source.use_of(good) == "past"
    assert override_source.use_of(entry("1", "mirrored", True, "parsons", "IMC.rvt", "imc")) is None


def test_r386_an_entry_marked_use_rule_is_left_by_the_build(tmp_path, monkeypatch):
    from test_addin_levels import level_build, level_of
    # Plot 3 has the FFL 10.263 by the rule, its heightM entry is marked use rule, the entryM of plot 4 is applied as before
    overrides = [entry("3", "heightM", {"gapToRuleM": 0.01}, "parsons", "IMC.rvt", "imc", use="rule", byHand=True),
                 entry("4", "entryM", {"gapToRuleM": 0.0013}, "parsons", "IMC.rvt", "imc", use="past", byHand=True)]
    loaded = plan_rules.load_overrides(write_list(tmp_path / "l.json", overrides), {"parsons"}, None, GROUPS)
    assert loaded == {"4": {"entryM": {"gapToRuleM": 0.0013}}} and loaded.ruled == {"3": {"heightM": plan_rules.USE_RULE}} and loaded.skip == {}
    found, plan = level_build(tmp_path, monkeypatch, overrides=overrides)
    assert level_of(found["3"], "ffl") == 10.263 and level_of(found["4"], "entry") == 10.0639
    assert found["3"]["overrides"] == {"applied": [], "notApplied": {"heightM": plan_rules.USE_RULE}}
    assert found["4"]["overrides"] == {"applied": ["entryM"], "notApplied": {}}
    assert plan["summary"]["rules"]["applied"] == {"entryM": 1} and plan["summary"]["rules"]["overridesNotApplied"] == {"heightM": 1}


def test_r386_a_skip_plot_is_not_written(tmp_path, monkeypatch):
    from test_addin_levels import level_build, level_of
    overrides = [entry("2", "steps", "2", "parsons", "IMC.rvt", "imc"), entry("3", "heightM", {"gapToRuleM": -0.001}, "parsons", "IMC.rvt", "imc")]
    found, plan = level_build(tmp_path, monkeypatch, overrides=overrides, skip=SKIP)
    assert "2" not in found and set(found) == {"1", "3", "4"}
    missing = {item["plot"]: item for item in plan["missing"]}
    assert missing["2"]["written"] is False and missing["2"]["missing"] == [{"key": "skip", "why": "the skip list of the override list: the client builds this plot later (R386)"}]
    assert missing["2"]["overrides"] == {"applied": [], "notApplied": {"steps": "the plot is not written, it lacks skip"}}
    assert plan["summary"]["skipped"] == ["2"] and plan["summary"]["missingByKey"]["skip"] == 1 and plan["summary"]["notWritten"] == 1
    # The other plots build as before, plot 3 takes its own override
    assert level_of(found["3"], "ffl") == 10.262
