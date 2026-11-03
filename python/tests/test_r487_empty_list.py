"""R487. The override list of a new project holds no entries, and a build under rev6 takes it as a list with nothing to apply. Under the past job a list with none of its
source is refused as before. R486_ShippedSetTests.cs holds the same lines for the C#."""
import json

import pytest

import design_basis
import plan_rules
import project_profile

GROUPS = {"parsons": ["kx"], "native": ["n1"]}
NO_SOURCE = "the override list holds no override of parsons among the sources parsons (R280, R330)"
NATIVE = {"plot": "5", "field": "mirrored", "value": True, "source": "native", "model": "a.rvt", "key": "n1"}


def written(tmp_path, text):
    path = tmp_path / "list.json"
    path.write_text(text, encoding="utf-8")
    return path


def load(path, rev6, models=None):
    return plan_rules.load_overrides(path, {"parsons"}, groups=GROUPS, models=models, rev6=rev6)


def test_r487_an_empty_real_list_passes_under_rev6_and_under_the_past_job_is_refused_with_the_old_line(tmp_path):
    path = written(tmp_path, '{"note": "made up", "overrides": []}')
    loaded = load(path, True)
    assert not loaded and not loaded.skip and not loaded.ruled
    with pytest.raises(project_profile.ProfileError) as error:
        load(path, False)
    assert str(error.value) == NO_SOURCE


@pytest.mark.parametrize("text", ['{}', '{"note": "x"}', '{"overrides": null}', '{"overrides": {}}'])
@pytest.mark.parametrize("rev6", [True, False])
def test_r487_a_file_with_no_real_list_is_refused_under_both_modes_as_no_override(tmp_path, text, rev6):
    with pytest.raises(project_profile.ProfileError) as error:
        load(written(tmp_path, text), rev6)
    assert str(error.value) == NO_SOURCE


@pytest.mark.parametrize("text", ['{"overrides": ""}', '{"overrides": 0}', '{"overrides": false}'])
@pytest.mark.parametrize("rev6", [True, False])
def test_r487_overrides_that_is_no_list_is_refused_under_both_modes_by_the_line_of_the_csharp(tmp_path, text, rev6):
    path = written(tmp_path, text)
    with pytest.raises(project_profile.ProfileError) as error:
        load(path, rev6)
    assert str(error.value) == "%s: overrides of the override list is no list of entries (R330)" % path


@pytest.mark.parametrize("text", ['0', 'false'])
@pytest.mark.parametrize("rev6", [True, False])
def test_r487_a_file_that_is_no_object_is_refused_under_both_modes_by_the_line_of_the_csharp(tmp_path, text, rev6):
    path = written(tmp_path, text)
    with pytest.raises(project_profile.ProfileError) as error:
        load(path, rev6)
    assert str(error.value) == "the input overrides is no JSON object: %s" % path


def test_r487_entries_of_another_source_do_not_lock_a_rev6_project_and_apply_nothing_and_the_past_job_still_refuses(tmp_path):
    path = written(tmp_path, json.dumps({"overrides": [NATIVE]}))
    assert not load(path, True)
    with pytest.raises(project_profile.ProfileError) as error:
        load(path, False)
    assert str(error.value) == NO_SOURCE


def test_r487_a_model_named_with_no_override_is_still_refused_on_an_empty_list(tmp_path):
    with pytest.raises(project_profile.ProfileError) as error:
        load(written(tmp_path, '{"overrides": []}'), True, models=["a.rvt"])
    assert "holds no override of a.rvt" in str(error.value)


def test_r487_the_rev6_of_a_list_is_the_applied_mode_of_config():
    values = {"designBasis": {"mode": "pastMade", "modes": [design_basis.REV6, "pastMade"]}}
    assert design_basis.holds_rev6(values) is False
    assert design_basis.holds_rev6(design_basis.take_mode(values, design_basis.REV6)) is True
    assert design_basis.holds_rev6(project_profile.ProjectProfile("made.json", values)) is True
    assert design_basis.holds_rev6({}) is False
