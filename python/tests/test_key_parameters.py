"""R353. The shared parameter GUIDs of the answer keys into config, python/key_parameters.py, on made up parameter files of the read job
(R10): one GUID per name over every key that holds it, the GUID the elements give held as a second route, a conflict or a name no key
holds listed with why and never given a GUID."""
import json

import pytest

import key_parameters
import project_profile

CASE = "11111111-2222-3333-4444-555555555555"
PROTO = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
OTHER = "99999999-8888-7777-6666-555555555555"


def made_file(case=CASE, seen_case=CASE, plot_project=True):
    return {"format": "site-adaptation-shared-parameters", "model": "key_TEST-GC.rvt",
            "parameters": [{"name": "CASE", "guid": case, "elementId": 10, "bound": True, "binding": "instance", "categories": ["RVT Links"]},
                           {"name": "RRE_ZZ_SP_Prototype", "guid": PROTO, "elementId": 20, "bound": True, "binding": "instance", "categories": ["RVT Links"]}],
            "onElements": [{"category": "RVT Links", "name": "CASE", "guids": [seen_case], "count": 3},
                           {"category": "RVT Links", "name": "RRE_ZZ_SP_Prototype", "guids": [PROTO], "count": 3}],
            "otherBindings": ["Plot"] if plot_project else []}


NAMES = ["Plot", "RRE_ZZ_SP_Prototype", "CASE", "RRE_ZZ_SP_Prototype-FFL"]


def test_r353_a_name_takes_the_one_guid_every_key_and_its_elements_give_and_a_conflict_or_a_name_no_key_holds_is_listed():
    found = key_parameters.collect({"imc": made_file(), "imd": made_file()}, NAMES)
    assert found["CASE"]["guid"] == CASE and found["CASE"]["keys"] == ["imc", "imd"] and found["CASE"]["on"] == ["RVT Links"]
    assert found["RRE_ZZ_SP_Prototype"]["guid"] == PROTO
    assert found["Plot"]["guid"] is None and "project parameter" in found["Plot"]["why"] and found["Plot"]["projectParameterIn"] == ["imc", "imd"]
    assert found["RRE_ZZ_SP_Prototype-FFL"]["guid"] is None and "no key" in found["RRE_ZZ_SP_Prototype-FFL"]["why"]
    two = key_parameters.collect({"imc": made_file(), "imd": made_file(case=OTHER, seen_case=OTHER)}, NAMES)
    assert two["CASE"]["guid"] is None and "two GUIDs" in two["CASE"]["why"]
    seen = key_parameters.collect({"imc": made_file(seen_case=OTHER)}, NAMES)
    assert seen["CASE"]["guid"] is None and "elements" in seen["CASE"]["why"]
    block = key_parameters.block(found, NAMES, ["imc", "imd"])
    assert [item["name"] for item in block["parameters"]] == ["RRE_ZZ_SP_Prototype", "CASE"]
    assert [item["name"] for item in block["noGuid"]] == ["Plot", "RRE_ZZ_SP_Prototype-FFL"]


def test_r353_the_names_are_the_floor_parameter_the_link_parameters_and_the_level_parameters_of_config():
    settings = project_profile.load_settings()
    names = key_parameters.names_of(settings)
    assert names[0] == settings.get("floors")["plotParameter"] and len(names) == 11 and len(set(names)) == 11
    assert "RRE_ZZ_SP_Prototype-FFL" in names and "CASE" in names


def test_r353_the_block_is_written_into_the_addin_block_of_config_and_every_other_value_stays(tmp_path):
    text = (project_profile.SETTINGS_PATH).read_text(encoding="utf-8")
    path = tmp_path / "project.json"
    path.write_text(text, encoding="utf-8")
    found = key_parameters.collect({"imc": made_file()}, NAMES)
    block = key_parameters.block(found, NAMES, ["imc"])
    key_parameters.write_block(path, block)
    written = json.loads(path.read_text(encoding="utf-8"))
    before = json.loads(text)
    assert written["addin"]["sharedParameters"]["parameters"][1] == {"name": "CASE", "guid": CASE, "on": ["RVT Links"], "keys": 1}
    del written["addin"]["sharedParameters"]
    before["addin"].pop("sharedParameters", None)
    assert written == before
    key_parameters.write_block(path, block)
    assert path.read_text(encoding="utf-8").count('"sharedParameters"') == 1
    with pytest.raises(ValueError, match="no key"):
        key_parameters.block(key_parameters.collect({}, NAMES), NAMES, [])
