"""Round ADDIN PR 3a: the source of every override (R330) and the rules on the Parsons keys (R331 to R336)."""
import json
from pathlib import Path

import pytest

import majority
import override_source
import plan_rules
import project_profile
from test_round_addin import run_book

ROOT = Path(__file__).resolve().parents[2]
GROUPS = {"native": ["u11", "u12"], "parsons": ["imc", "imd"]}


def real_groups():
    return project_profile.load_settings().get("majority.groups")


def write_list(path, entries):
    path.write_text(json.dumps({"note": "n", "overrides": entries}), encoding="utf-8")
    return path


def entry(plot, field, value, source, model, key):
    return {"plot": plot, "field": field, "value": value, "rule": None, "source": source, "model": model, "key": key, "decision": "d", "reason": "r"}


# R330 the source of every override
def test_r330_every_override_majority_writes_has_the_job_of_its_key_as_source_and_its_model_file_apart():
    misses = [{"decision": "parsons hand villa", "field": "mirrored", "plot": "2", "key": "imc", "code": "VD2", "value": True, "rule": False,
               "state": "breaks", "reason": "breaks blockMirror"},
              {"decision": "native rampSlope", "field": "rampType", "plot": "1", "key": "u12", "code": "DP2", "value": "flat", "rule": "2",
               "state": "breaks", "reason": "breaks all"}]
    listed = majority.overrides(misses, {"overrideDecisions": ["hand", "rampSlope"], "groups": GROUPS}, {"imc": "IMC.rvt"})
    by_plot = {item["plot"]: item for item in listed}
    assert by_plot["2"]["source"] == "parsons" and by_plot["2"]["model"] == "IMC.rvt"
    # A key with no model given keeps its key as the model, as R272 did
    assert by_plot["1"]["source"] == "native" and by_plot["1"]["model"] == "u12"
    assert list(by_plot["2"]) == ["plot", "field", "value", "rule", "source", "model", "key", "decision", "reason"]


def test_r330_an_entry_of_before_the_source_field_takes_the_job_of_its_key_and_its_model_file_moves_to_model():
    old = {"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.02}, "rule": None, "source": "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt", "key": "u12",
           "decision": "native entry", "reason": "r"}
    new = override_source.migrate_entry(old, GROUPS)
    assert new["source"] == "native" and new["model"] == "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"
    assert list(new) == ["plot", "field", "value", "rule", "source", "model", "key", "decision", "reason"]
    # An entry already in the new form passes as it is
    assert override_source.migrate_entry(new, GROUPS) == new


@pytest.mark.parametrize("bad", [
    {"plot": "1", "field": "f", "value": 1, "source": "a.rvt", "key": "zz"},
    {"plot": "1", "field": "f", "value": 1, "source": "native", "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "source": "other", "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "model": "a.rvt", "key": "imc"},
    {"plot": "1", "field": "f", "value": 1, "source": "a.rvt", "model": "b.rvt", "key": "imc"},
])
def test_r330_an_entry_whose_key_has_no_job_or_whose_source_is_not_the_job_of_its_key_is_refused(bad):
    with pytest.raises(project_profile.ProfileError):
        override_source.migrate_entry(bad, GROUPS)
    with pytest.raises(project_profile.ProfileError):
        override_source.check_entries([bad], GROUPS)


def test_r330_the_loader_refuses_a_list_with_an_entry_of_no_source(tmp_path):
    path = write_list(tmp_path / "o.json", [entry("1", "rampType", "flat", "parsons", "IMC.rvt", "imc"),
                                            {"plot": "2", "field": "rampType", "value": "flat", "source": "IMC.rvt", "key": "imc"}])
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.load_overrides(path, {"parsons"})
    assert "R330" in str(error.value) and "2" in str(error.value)


def test_r330_the_loader_takes_the_entries_of_the_sources_named_and_narrows_to_the_models_named(tmp_path):
    path = write_list(tmp_path / "o.json", [entry("1", "rampType", "flat", "parsons", "IMC.rvt", "imc"),
                                            entry("2", "mirrored", True, "parsons", "IMD.rvt", "imd"),
                                            entry("3", "entryM", {"gapToRuleM": 0.02}, "native", "U12.rvt", "u12")])
    assert plan_rules.load_overrides(path, {"parsons"}) == {"1": {"rampType": "flat"}, "2": {"mirrored": True}}
    assert plan_rules.load_overrides(path, {"parsons"}, models={"IMD.rvt"}) == {"2": {"mirrored": True}}
    assert plan_rules.load_overrides(path, {"native"}) == {"3": {"entryM": {"gapToRuleM": 0.02}}}
    # A model with no override of the sources named is refused, never applied as none
    with pytest.raises(project_profile.ProfileError):
        plan_rules.load_overrides(path, {"parsons"}, models={"U12.rvt"})


def new_list(tmp_path):
    """One override of each job in the form of R330, with the keys and models the real list uses."""
    return write_list(tmp_path / "overrides.json", [
        entry("1", "entryM", {"gapToRuleM": 0.02}, "native", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt", "u12"),
        entry("2", "rampType", "flat", "parsons", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt", "imc")])


def test_r330_a_build_with_overrides_and_no_source_named_applies_only_the_parsons_overrides_by_default(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path)])
    assert code == 0, capsys.readouterr().err
    assert seen["rules"]["overrides"] == {"2": {"rampType": "flat"}}
    assert seen["rules"]["sources"] == ["parsons"]


def test_r330_native_overrides_are_never_applied_by_default_a_native2022_build_must_name_their_source(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path)])
    err = capsys.readouterr().err
    assert code == 2 and "rules" not in seen
    assert "--override-source native" in err and "R330" in err
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path), "--override-source", "native"])
    assert code == 0 and seen["rules"]["overrides"] == {"1": {"entryM": {"gapToRuleM": 0.02}}}
    # Native overrides on the default parsons build are refused, gaps to the rule of another job
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path), "--override-source", "native"])
    assert code == 2 and "rules" not in seen


def test_r330_a_model_named_narrows_the_overrides_of_its_source_and_needs_the_list(tmp_path, monkeypatch, capsys):
    path = new_list(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path), "--override-source", "native",
                                                  "--override-model", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"])
    assert code == 0 and seen["rules"]["overrides"] == {"1": {"entryM": {"gapToRuleM": 0.02}}}
    code, seen = run_book(tmp_path, monkeypatch, ["--override-model", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"])
    assert code == 2 and "--overrides" in capsys.readouterr().err


def test_r330_the_default_sources_of_the_config_are_jobs_of_majority_groups():
    settings = project_profile.load_settings()
    assert override_source.default_sources(settings) == ["parsons"]
    for wrong in ([], ["other"], "parsons", ["parsons", "parsons"]):
        values = dict(settings.values, majority=dict(settings.values["majority"], overrideDefaultSources=wrong))
        with pytest.raises(project_profile.ProfileError):
            override_source.default_sources(project_profile.ProjectProfile(settings.path, values))


def test_r330_majority_keeps_the_entries_of_a_kept_source_or_field_cell_for_cell_and_drops_the_run_entries_of_them():
    earlier = [entry("1", "entryM", {"gapToRuleM": 0.02}, "native", "U12.rvt", "u12"),
               entry("2", "entryM", {"gapToRuleM": 0.03}, "parsons", "IMC.rvt", "imc"),
               entry("2", "mirrored", True, "parsons", "IMC.rvt", "imc")]
    run = [entry("1", "entryM", {"gapToRuleM": 0.5}, "native", "U12.rvt", "u12"),
           entry("2", "entryM", {"gapToRuleM": 0.9}, "parsons", "IMC.rvt", "imc"),
           entry("3", "mirrored", False, "parsons", "IMD.rvt", "imd")]
    keep = override_source.parse_keep(["native", "parsons:entryM,heightM"], GROUPS)
    merged, counts = override_source.merge(earlier, run, keep)
    assert merged[0] == earlier[0] and earlier[1] in merged and earlier[2] not in merged
    assert run[2] in merged and run[0] not in merged and run[1] not in merged
    assert counts == {"kept": {"native": 1, "parsons": 1}, "new": {"parsons": 1}, "droppedFromRun": {"native": 1, "parsons": 1}}
    for wrong in (["other"], ["parsons:"], ["parsons:entryM,"], ["native", "native"]):
        with pytest.raises(project_profile.ProfileError):
            override_source.parse_keep(wrong, GROUPS)


def test_r330_the_real_override_list_has_the_job_of_its_key_as_source_on_every_entry():
    data = json.loads(override_source.list_path(project_profile.load_settings()).read_text(encoding="utf-8"))
    entries = data["overrides"]
    override_source.check_entries(entries, real_groups())
    counts = override_source.counts(entries)
    assert set(counts["bySource"]) == {"parsons", "native"}
    assert sum(counts["bySource"].values()) == len(entries)
