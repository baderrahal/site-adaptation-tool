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


# R322. The counts of each key read

def key_settings(tmp_path):
    return settings_with(tmp_path, dict(FLOORS, keyTypes={"IMX": {"generic": ["Slab F"]}}))


def made_up_read():
    """One project document of the reader, made up: 3 links, 5 floors, 3 walls, 4 spots and 2 levels."""
    links = [{"file": "A.rvt", "elementId": 1, "plotNumber": "101", "loaded": False},
             {"file": "A.rvt", "elementId": 2, "plotNumber": "102", "loaded": False},
             {"file": "B.rvt", "elementId": 3, "plotNumber": None, "loaded": True}]
    for index, link in enumerate(links):
        link["transform"] = {"origin": [10.0 * index, 0.0, 0.0], "rotationDeg": 90.0, "mirrored": False}
    floors = [{"typeName": "Slab F", "elementId": 10}, {"typeName": "Slab F", "elementId": 11}, {"typeName": "Yard Y", "elementId": 12},
              {"typeName": "Ramp B", "elementId": 13}, {"typeName": "Footing Z", "elementId": 14}]
    walls = [{"typeName": "Wall 1", "elementId": 20}, {"typeName": "Wall 1", "elementId": 21}, {"typeName": "Wall 2", "elementId": 22}]
    spots = [{"elementId": 30, "value": 1.0, "host": {"elementId": 10, "category": "Floors", "typeName": "Slab F"}},
             {"elementId": 31, "value": 1.5, "host": {"elementId": 13, "category": "Floors", "typeName": "Ramp B"}},
             {"elementId": 32, "host": {"elementId": 3, "linkedElementId": 99}},
             {"elementId": 33}]
    levels = [{"name": "L1"}, {"name": "L2"}]
    return {"kind": "project", "links": links, "floors": floors, "walls": walls, "spotElevations": spots, "site": {"levels": levels}}


def test_r322_the_counts_of_a_read_hold_links_floors_by_type_and_kind_walls_by_type_spots_by_host_and_levels(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    own = key_reads.counts(made_up_read(), pf.load_floors(settings, "IMX"))
    other = key_reads.counts(made_up_read(), pf.load_floors(settings, "IMY"))

    assert own["links"] == {"instances": 3, "withPlotNumber": 2, "files": 2, "loaded": 1}
    assert own["floors"]["byType"] == {"Footing Z": 1, "Ramp B": 1, "Slab F": 2, "Yard Y": 1}
    assert own["floors"]["byKind"] == {"generic": 2, "yard": 1, "ramp": 1, "none": 1}
    assert other["floors"]["byKind"] == {"generic": 0, "yard": 1, "ramp": 1, "none": 3}
    assert own["walls"] == {"count": 3, "byType": {"Wall 1": 2, "Wall 2": 1}}
    assert own["spots"]["count"] == 4 and own["spots"]["withValue"] == 2
    assert own["spots"]["byHost"] == {"Floors: Ramp B": 1, "Floors: Slab F": 1, "in a link": 1, "no host": 1}
    assert own["levels"] == {"count": 2, "names": ["L1", "L2"]}


def test_r322_a_section_the_read_lacks_is_null_and_never_counted_as_none(tmp_path):
    import key_reads
    document = made_up_read()
    del document["walls"]
    document["site"] = {"levels": None, "notRead": {"levels": "boom"}}
    result = key_reads.counts(document, pf.load_floors(key_settings(tmp_path)))

    assert result["walls"] is None
    assert result["levels"] is None
    assert result["links"]["instances"] == 3


def test_r322_the_window_facts_come_from_the_run_and_the_log_of_the_job():
    import key_reads
    run = {"minutes": 1.22, "exitCode": 0, "logResult": "ok",
           "memory": {"peakPrivateMib": 1200, "peakWorkingSetMib": 1355, "minFreeGib": 0.53, "samplesUnderFloor": 9, "notResponding": 6, "throughExit": True}}
    log = {"result": "ok", "steps": [{"name": "openModel", "seconds": 3.3}, {"name": "read", "seconds": 10.3}, {"name": "close", "seconds": 0.5}]}
    facts = key_reads.window_facts(run, log)

    assert facts == {"minutes": 1.22, "openSeconds": 3.3, "readSeconds": 10.3, "peakPrivateMib": 1200, "peakWorkingSetMib": 1355, "throughExit": True,
                     "minFreeGib": 0.53, "samplesUnderFloor": 9, "notResponding": 6, "exitCode": 0, "logResult": "ok"}


def test_r322_main_writes_the_counts_of_each_key_into_the_reads_folder_and_never_into_the_repo(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    folder = reads / "QX"
    folder.mkdir(parents=True)
    (folder / "testhost_QX_prototypes.json").write_text(json.dumps({"documents": [made_up_read()]}), encoding="utf-8")
    (folder / "testhost_QX_run.json").write_text(json.dumps({"minutes": 1.0, "exitCode": 0, "logResult": "ok", "memory": {"peakPrivateMib": 900}}), encoding="utf-8")
    (folder / "testhost_QX_log.json").write_text(json.dumps({"result": "ok", "steps": []}), encoding="utf-8")

    assert key_reads.main(["--settings", str(settings.path), "--reads", str(reads), "--key", "IMX=QX"]) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert written["keys"]["IMX"]["job"] == "QX"
    assert written["keys"]["IMX"]["counts"]["floors"]["byKind"]["generic"] == 2
    assert written["keys"]["IMX"]["window"]["peakPrivateMib"] == 900
    with pytest.raises(ValueError):
        key_reads.main(["--settings", str(settings.path), "--reads", str(project_profile.ROOT / "python"), "--key", "IMX=QX"])
    with pytest.raises(project_profile.ProfileError):
        key_reads.main(["--settings", str(settings.path), "--reads", str(reads), "--key", "IMZ=QX"])

