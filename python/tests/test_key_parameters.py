"""R353, R461. The shared parameter GUIDs of the answer keys held to the parameter sheet, python/key_parameters.py, on made up parameter files of the read job
(R10): one GUID per name over every key that holds it, the GUID the elements give held as a second route, a conflict or a name no key
holds listed with why and never given a GUID."""
import json
import re

import pytest

import key_parameters
import parameter_sheet
import project_profile

CASE = "11111111-2222-3333-4444-555555555555"
PROTO = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
OTHER = "99999999-8888-7777-6666-555555555555"


def stem(key):
    """The stem of the copy answerKeys of config names for a key, its work copies <stem>_TEST-<job> (R312)."""
    copy = next(item["copy"] for item in project_profile.load_settings().get("answerKeys")["keys"] if item["code"] == key.upper())
    return copy[:-4]


def made_file(case=CASE, seen_case=CASE, plot_project=True, key="imc"):
    return {"format": "site-adaptation-shared-parameters", "model": "%s_TEST-P%s.rvt" % (stem(key), key[2].upper()),
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


def two_keys(plot_imc, plot_imd):
    """The parameter files of two made up keys whose bound shared parameter Plot has the GUIDs given, None for a project parameter."""
    found = {}
    for key, guid in (("imc", plot_imc), ("imd", plot_imd)):
        payload = made_file(plot_project=guid is None, key=key)
        if guid is not None:
            payload["parameters"].append({"name": "Plot", "guid": guid, "elementId": 30, "bound": True, "binding": "instance", "categories": ["Floors"]})
        found[key] = payload
    return found


def manifest(tmp_path):
    """key_copies.json that names the copies answerKeys of config names, as R312 writes it beside the copies."""
    path = tmp_path / "key_copies.json"
    path.write_text(json.dumps({"copies": [{"code": item["code"], "copy": item["copy"]} for item in project_profile.load_settings().get("answerKeys")["keys"]]}),
                    encoding="utf-8")
    return path


def test_r353_a_parameter_file_is_held_to_its_key_by_its_model(tmp_path, capsys):
    # Inputs: one file given as imc and as imd, the file of imd given as imc, a file of no model, and the two files as they are
    files = two_keys(None, None)
    out = run_main({"imc": files["imc"], "imd": files["imc"]}, tmp_path, capsys, code=2)
    assert "imd parameters model %s is not a work copy %s_TEST-<job> of its answer key" % (files["imc"]["model"], stem("imd")) in out.err
    out = run_main({"imc": files["imd"], "imd": files["imd"]}, tmp_path, capsys, code=2)
    assert "imc parameters model" in out.err and "imd parameters model" not in out.err
    nameless = dict(files["imd"])
    del nameless["model"]
    out = run_main({"imc": files["imc"], "imd": nameless}, tmp_path, capsys, code=2)
    assert "imd parameters model None is not a work copy" in out.err


def run_main(files, tmp_path, capsys, code=0, settings=None):
    """key_parameters.py on made up parameter files with the config of the repo, or the one given, and its output."""
    args = ["--settings", str(settings or project_profile.SETTINGS_PATH), "--key-copies", str(manifest(tmp_path))]
    for key, payload in files.items():
        name = tmp_path / ("parameters_%s_%d.json" % (key, len(args)))
        name.write_text(json.dumps(payload), encoding="utf-8")
        args += ["--parameters", "%s=%s" % (key, name)]
    found = key_parameters.main(args)
    out = capsys.readouterr()
    assert found == code, out.err
    return out


def test_r461_the_parameter_sheet_of_config_gives_a_guid_to_every_name_of_the_tool_and_the_guids_config_held_before():
    settings = project_profile.load_settings()
    kept, left_out = parameter_sheet.from_file(parameter_sheet.path_of(project_profile.SETTINGS_PATH, {"addin": settings.get("addin")}))
    guids = {item["name"]: item["guid"] for item in kept}
    assert left_out == [] and sorted(guids) == sorted(key_parameters.names_of(settings))
    assert all(re.fullmatch(r"[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}", guid) for guid in guids.values()) and len(set(guids.values())) == 11
    # The GUIDs addin.sharedParameters and addin.fixedParameters held before R461, as the keys measured them and Q156 fixed Plot
    assert guids["RRE_ZZ_SP_Plot-Number"] == "7a7813d7-ad61-4bf2-a75f-6234c92957bb" and guids["CASE"] == "c0b2c4ee-e129-4a42-bcb6-225759b874eb"
    assert guids["Plot"] == "faecae98-f82c-46b5-bd80-489380545236" and guids["RRE_ZZ_SP_Prototype-FFL"] == "cd63d489-5fa8-46a8-a6bc-38ff3aae66bf"
    assert "sharedParameters" not in settings.get("addin") and "fixedParameters" not in settings.get("addin")


def test_r461_a_name_the_keys_give_another_guid_than_the_sheet_is_printed_and_the_run_ends_1(tmp_path, capsys):
    # Input: the keys give CASE a GUID the sheet does not hold, and Plot two GUIDs, a conflict of the keys
    out = run_main(two_keys(CASE, OTHER), tmp_path, capsys, code=1)
    assert "CASE: the keys give the GUID %s, the parameter sheet c0b2c4ee-e129-4a42-bcb6-225759b874eb" % CASE in out.out
    assert "Plot: a conflict of the keys" in out.out and "the keys against shared_parameters.xlsx: parameters 2, noGuid 9, conflicts 1, 2 differ" in out.out
    assert key_parameters.against_sheet({"parameters": [{"name": "X", "guid": G1}]}, []) == ["X: the keys give the GUID %s, the parameter sheet has no row of Keep Yes" % G1]
    assert key_parameters.against_sheet({"parameters": [{"name": "X", "guid": G1}]}, [{"name": "X", "guid": G1}]) == []


def test_r461_a_config_whose_sheet_is_not_there_ends_2_with_the_reason(tmp_path, capsys):
    settings = json.loads(project_profile.SETTINGS_PATH.read_text(encoding="utf-8"))
    settings["addin"]["parameterSheet"] = str(tmp_path / "none.xlsx")
    path = tmp_path / "project.json"
    path.write_text(json.dumps(settings), encoding="utf-8")
    out = run_main(two_keys(None, None), tmp_path, capsys, code=2, settings=path)
    assert "key_parameters:" in out.err and "none.xlsx" in out.err
