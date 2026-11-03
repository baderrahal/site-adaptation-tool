"""R487. The override list of a new project holds no entries, and a build takes it as a list with nothing to apply. R486_ShippedSetTests.cs holds the C#."""
import json

import pytest

import plan_rules
import project_profile

GROUPS = {"parsons": ["kx"], "native": ["n1"]}


def listed(tmp_path, entries):
    path = tmp_path / "list.json"
    path.write_text(json.dumps({"note": "made up", "overrides": entries}), encoding="utf-8")
    return path


def test_r487_a_list_with_no_entries_is_taken_with_nothing_to_apply(tmp_path):
    loaded = plan_rules.load_overrides(listed(tmp_path, []), {"parsons"}, groups=GROUPS)
    assert not loaded and not loaded.skip and not loaded.ruled


def test_r487_a_list_with_entries_of_another_source_is_still_refused(tmp_path):
    entry = {"plot": "5", "field": "mirrored", "value": True, "source": "native", "model": "a.rvt", "key": "n1"}
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.load_overrides(listed(tmp_path, [entry]), {"parsons"}, groups=GROUPS)
    assert str(error.value) == "the override list holds no override of parsons among the sources parsons (R280, R330)"


def test_r487_a_model_named_with_no_override_is_still_refused_on_an_empty_list(tmp_path):
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.load_overrides(listed(tmp_path, []), {"parsons"}, groups=GROUPS, models=["a.rvt"])
    assert "holds no override of a.rvt" in str(error.value)
