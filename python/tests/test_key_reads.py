"""Round ADDIN Phase 2a: the floor types of each answer key (R321).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import json

import pytest

import plan_from_past as pf
import project_profile

# Made up floor type and level names, the real ones live in config/project.json only
FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]},
          "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}
KEYS = {"keys": [{"code": "IMX"}, {"code": "IMY"}]}
JOB = {"neighbourhood": "AAA", "schedule": "book.xlsx", "sheet": "Sheet1", "prototypes": "C:\\x\\prototypes.json",
       "prototypeFolder": "Prototypes", "output": "C:\\x\\plan.json", "parameters": ["RRE_ZZ_SP_Plot-Number"]}


def settings_with(tmp_path, floors, keys=KEYS):
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"floors": floors, "answerKeys": keys, "pastJob": JOB, "schedules": ["book.xlsx"]}), encoding="utf-8")
    return project_profile.load_settings(path)


def test_r321_a_key_in_key_types_reads_its_floors_by_those_types_and_every_other_kind_by_floors_types(tmp_path):
    settings = settings_with(tmp_path, dict(FLOORS, keyTypes={"IMX": {"generic": ["Slab F"]}}))
    own = pf.load_floors(settings, "IMX")
    other = pf.load_floors(settings, "IMY")
    plain = pf.load_floors(settings)

    assert own["types"] == {"generic": ["Slab F"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]}
    assert pf.kind_of({"typeName": "Slab F"}, own) == "generic"
    assert pf.kind_of({"typeName": "Slab G"}, own) is None
    assert pf.kind_of({"typeName": "Ramp B"}, own) == "ramp"
    assert other["types"] == FLOORS["types"] and plain["types"] == FLOORS["types"]
    assert pf.kind_of({"typeName": "Slab F"}, other) is None
    # The block of the settings is left as it is, so a second key never sees the types of the first
    assert settings.values["floors"]["types"] == FLOORS["types"]


@pytest.mark.parametrize("key_types", [
    {"IMZ": {"generic": ["Slab F"]}},
    {"IMX": {"slab": ["Slab F"]}},
    {"IMX": {"generic": []}},
    {"IMX": {"generic": "Slab F"}},
    {"IMX": {"generic": [" Slab F"]}},
    {"IMX": {"generic": ["Yard Y"]}},
    {"IMX": []},
    ["IMX"],
])
def test_r321_key_types_are_refused_for_no_key_no_kind_no_list_or_a_name_under_two_kinds(tmp_path, key_types):
    settings = settings_with(tmp_path, dict(FLOORS, keyTypes=key_types))
    with pytest.raises(project_profile.ProfileError):
        pf.load_floors(settings)


def test_r321_a_key_the_answer_keys_do_not_name_is_refused_and_not_read_by_floors_types(tmp_path):
    settings = settings_with(tmp_path, dict(FLOORS, keyTypes={"IMX": {"generic": ["Slab F"]}}))
    with pytest.raises(project_profile.ProfileError, match="IMZ"):
        pf.load_floors(settings, "IMZ")


def test_r321_plan_from_past_reads_the_past_model_by_the_types_of_the_key_it_names(tmp_path):
    settings = settings_with(tmp_path, dict(FLOORS, keyTypes={"IMX": {"generic": ["Slab F"]}}))
    with pytest.raises(project_profile.ProfileError, match="IMZ"):
        pf.main(["--settings", str(settings.path), "--key", "IMZ"])


def test_r321_the_real_config_reads_ffl_as_the_generic_floor_of_ime_and_imf_only():
    settings = project_profile.load_settings()
    plain = pf.load_floors(settings)["types"]
    codes = [key["code"] for key in settings.values["answerKeys"]["keys"]]
    assert len(codes) == 7
    for code in codes:
        types = pf.load_floors(settings, code)["types"]
        assert types["yard"] == plain["yard"] and types["ramp"] == plain["ramp"], code
        assert types["generic"] == (["FFL"] if code in ("IME", "IMF") else plain["generic"]), code
