"""R353. The shared parameter GUIDs of the answer keys into config, python/key_parameters.py, on made up parameter files of the read job
(R10): one GUID per name over every key that holds it, the GUID the elements give held as a second route, a conflict or a name no key
holds listed with why and never given a GUID."""
import json
import re

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


# The block addin.sharedParameters in the text of the config file, as write_block finds it
BLOCK = re.compile(r'    "sharedParameters": \{\n.*?\n    \},\n', re.S)


def config_without_block():
    """The text of config/project.json with no addin.sharedParameters, as before the first write, so a made up run of one or two keys is
    no shrink of the block of the 7 keys."""
    return BLOCK.sub("", project_profile.SETTINGS_PATH.read_text(encoding="utf-8"), count=1)


def edit_by_hand(path, change):
    """The block of the config file at path changed as Bader edits it, by its own lines, every other line kept."""
    text = path.read_text(encoding="utf-8")
    block = json.loads(text)["addin"]["sharedParameters"]
    change(block)
    path.write_text(BLOCK.sub(lambda match: key_parameters.block_text(block), text, count=1), encoding="utf-8")


def test_r353_the_block_is_written_into_the_addin_block_of_config_and_every_other_value_stays(tmp_path):
    text = config_without_block()
    assert "sharedParameters" not in json.loads(text)["addin"]
    path = tmp_path / "project.json"
    path.write_text(text, encoding="utf-8")
    found = key_parameters.collect({"imc": made_file()}, NAMES)
    block = key_parameters.block(found, NAMES, ["imc"])
    key_parameters.write_block(path, block)
    written = json.loads(path.read_text(encoding="utf-8"))
    before = json.loads(text)
    assert written["addin"]["sharedParameters"]["parameters"][1] == {"name": "CASE", "guid": CASE, "on": ["RVT Links"], "keys": 1, "routes": ["parameterElement", "elements"]}
    del written["addin"]["sharedParameters"]
    before["addin"].pop("sharedParameters", None)
    assert written == before
    key_parameters.write_block(path, block)
    assert path.read_text(encoding="utf-8").count('"sharedParameters"') == 1
    with pytest.raises(ValueError, match="no key"):
        key_parameters.block(key_parameters.collect({}, NAMES), NAMES, [])


def test_r353_config_holds_one_guid_of_the_keys_for_every_name_but_those_noguid_lists_with_why():
    settings = project_profile.load_settings()
    block = settings.get("addin")["sharedParameters"]
    names = key_parameters.names_of(settings)
    with_guid = [item["name"] for item in block["parameters"]]
    assert sorted(with_guid + [item["name"] for item in block["noGuid"]]) == sorted(names)
    assert all(re.fullmatch(r"[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}", item["guid"]) for item in block["parameters"])
    assert len({item["guid"] for item in block["parameters"]}) == len(block["parameters"])
    assert all(item["why"] for item in block["noGuid"]) and sorted(block["keys"]) == sorted(settings.get("majority")["groups"]["parsons"])


G1 = "11111111-1111-1111-1111-111111111111"
G2 = "22222222-2222-2222-2222-222222222222"


def test_r353_a_name_held_as_a_project_parameter_in_some_keys_and_shared_in_others_is_a_conflict_with_no_guid():
    # The probe of the breaker of PR 54: ima binds FFL as a shared parameter, imb as a project parameter, imc holds an unbound shared
    # element of the GUID and binds a project parameter of the name, so a read by GUID would miss it in imb and imc
    files = {"ima": {"parameters": [{"name": "FFL", "guid": G1, "bound": True, "categories": ["RVT Links"]}], "onElements": [], "otherBindings": []},
             "imb": {"parameters": [], "onElements": [], "otherBindings": ["FFL"]},
             "imc": {"parameters": [{"name": "FFL", "guid": G1, "bound": False, "categories": []}], "onElements": [], "otherBindings": ["FFL"]}}
    found = key_parameters.collect(files, ["FFL"])["FFL"]
    assert found["guid"] is None and found["projectParameterIn"] == ["imb", "imc"]
    assert "conflict" in found["why"] and "imb, imc" in found["why"] and "ima" in found["why"]
    block = key_parameters.block({"FFL": found}, ["FFL"], sorted(files))
    assert block["parameters"] == [] and block["noGuid"] == [{"name": "FFL", "why": found["why"], "projectParameterIn": ["imb", "imc"]}]
    # A key holding only an unbound element of another GUID beside a bound one is a conflict too, never two GUIDs to choose from
    other = key_parameters.collect({"ima": {"parameters": [{"name": "FFL", "guid": G1, "bound": True}], "onElements": [{"name": "FFL", "guids": [G1]}]},
                                    "imb": {"parameters": [{"name": "FFL", "guid": G2, "bound": False}], "onElements": []}}, ["FFL"])["FFL"]
    assert other["guid"] is None and "conflict" in other["why"] and "imb" in other["why"]


def test_r353_a_shared_name_also_bound_as_a_project_parameter_in_every_key_keeps_its_guid_and_lists_where():
    files = {key: made_file() for key in ("imc", "imd")}
    for payload in files.values():
        payload["otherBindings"] = ["Plot", "CASE"]
    found = key_parameters.collect(files, NAMES)
    assert found["CASE"]["guid"] == CASE and found["CASE"]["projectParameterIn"] == ["imc", "imd"]
    entry = next(item for item in key_parameters.block(found, NAMES, ["imc", "imd"])["parameters"] if item["name"] == "CASE")
    assert entry["projectParameterIn"] == ["imc", "imd"] and entry["routes"] == ["parameterElement", "elements"]


def test_r353_a_guid_no_element_gives_is_marked_as_held_on_one_route_only():
    payload = made_file()
    payload["onElements"] = [item for item in payload["onElements"] if item["name"] != "CASE"]
    found = key_parameters.collect({"imc": payload}, NAMES)
    assert found["CASE"]["guid"] == CASE and found["CASE"]["routes"] == ["parameterElement"]
    assert found["RRE_ZZ_SP_Prototype"]["routes"] == ["parameterElement", "elements"]
    entry = next(item for item in key_parameters.block(found, NAMES, ["imc"])["parameters"] if item["name"] == "CASE")
    assert entry["routes"] == ["parameterElement"]


def test_r353_a_write_keeps_an_entry_bader_marks_by_hand_and_refuses_a_run_of_fewer_keys(tmp_path):
    path = tmp_path / "project.json"
    path.write_text(config_without_block(), encoding="utf-8")
    found = key_parameters.collect({"imc": made_file(), "imd": made_file()}, NAMES)
    key_parameters.write_block(path, key_parameters.block(found, NAMES, ["imc", "imd"]))
    # Q085: Bader gives Plot a GUID by hand, the next run keeps it as he wrote it and Plot leaves noGuid
    def give_plot(block):
        block["parameters"].append({"name": "Plot", "guid": OTHER, "on": ["Floors"], "keys": 0, "byHand": True})
        block["noGuid"] = [item for item in block["noGuid"] if item["name"] != "Plot"]
    edit_by_hand(path, give_plot)
    key_parameters.write_block(path, key_parameters.block(found, NAMES, ["imc", "imd"]))
    block = json.loads(path.read_text(encoding="utf-8"))["addin"]["sharedParameters"]
    assert {"name": "Plot", "guid": OTHER, "on": ["Floors"], "keys": 0, "byHand": True} in block["parameters"]
    assert "Plot" not in [item["name"] for item in block["noGuid"]]
    # A run of fewer keys than the block holds would shrink it, refused with the keys named, the file as it was
    before = path.read_text(encoding="utf-8")
    with pytest.raises(ValueError, match="imd"):
        key_parameters.write_block(path, key_parameters.block(key_parameters.collect({"imc": made_file()}, NAMES), NAMES, ["imc"]))
    assert path.read_text(encoding="utf-8") == before
    # A hand GUID the keys give otherwise is refused with both named, never resolved in silence
    edit_by_hand(path, lambda block: next(item for item in block["parameters"] if item["name"] == "CASE").update(guid=OTHER, byHand=True))
    with pytest.raises(ValueError, match="CASE.*%s.*%s" % (OTHER, CASE)):
        key_parameters.write_block(path, key_parameters.block(found, NAMES, ["imc", "imd"]))
