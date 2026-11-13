"""Round ADDIN Phase 2a: the floor types of each answer key (R321).

Every model here is made up in code (R10). The real config is read only where a test says so.
"""
import json
from pathlib import PureWindowsPath

import pytest

import plan_from_past as pf
import project_profile

# Made up floor type and level names, the real ones live in config/project.json only
FLOORS = {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp A", "Ramp B"]},
          "levels": {"generic": "L1", "yard": "L2", "ramp": "L1"},
          "plotParameter": "Plot", "yardBelowFflM": 0.1, "yardShareTie": 0.01, "yardMinShare": 0.5}
# A made up codeInName and made up copy names, the real ones live in config/project.json only
KEYS = {"codeInName": "^X-{code}-", "keys": [{"code": "IMX", "copy": "X-IMX-model.rvt"}, {"code": "IMY", "copy": "X-IMY-model.rvt"}]}
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


def test_r321_a_floor_of_no_kind_on_a_plot_is_named_by_its_type_and_the_types_it_is_not_in():
    import test_plan_from_past as tp
    floors = tp.plot_floors("101", 0, 10)
    # The generic floor of the plot is of a type no kind names, as FFL of IME read by floors.types alone
    floors[0]["typeName"] = "FFL"
    data = tp.document([tp.link("101")], floors)
    plain = pf.build_plan(data, [tp.row("101")], tp.JOB, tp.FILES, "C:\\Prototypes", tp.FLOORS, tp.FACES)
    keyed = pf.build_plan(data, [tp.row("101")], tp.JOB, tp.FILES, "C:\\Prototypes", tp.FLOORS, tp.FACES, key="IMX")

    why = tp.missing_of(plain, "101")["floors.generic"]
    assert "no floor names the plot" not in why
    assert "of type FFL" in why and "not in floors.types" in why and str(floors[0]["elementId"]) in why
    assert "not in floors.keyTypes of IMX or floors.types" in tp.missing_of(keyed, "101")["floors.generic"]
    # A plot no floor names at all keeps the old reason
    bare = pf.build_plan(tp.document([tp.link("101")], []), [tp.row("101")], tp.JOB, tp.FILES, "C:\\Prototypes", tp.FLOORS, tp.FACES)
    assert tp.missing_of(bare, "101")["floors.generic"] == "no floor names the plot"


def test_r321_plan_from_past_refuses_a_key_whose_model_file_holds_no_code_of_it_or_the_code_of_another(tmp_path, monkeypatch):
    import test_plan_from_past as tp
    root = tmp_path / "input"
    (root / "01_Output" / "1.11 References").mkdir(parents=True)
    (root / "01_Output" / "1.11 References" / "book.xlsx").write_bytes(b"x")
    baseplan = tmp_path / "plots.json"
    baseplan.write_text(json.dumps({"plots": []}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"schedules": ["book.xlsx"], "inputRoot": str(root), "pastJob": tp.JOB, "floors": tp.FLOORS, "answerKeys": KEYS}), encoding="utf-8")
    monkeypatch.delenv(pf.ROOT_VARIABLE, raising=False)
    prototypes = tmp_path / "testhost_KX_prototypes.json"
    # R325. The plan of a key is named by its code, so the run reaches the tie of the read to the key
    args = ["--settings", str(settings), "--prototypes", str(prototypes), "--baseplan", str(baseplan), "--out", str(tmp_path / "plan_imx.json"), "--key", "IMX"]
    for name, match in (("X-IMY-model_TEST-KX", "IMX"), ("model of no key", "IMX"), (None, "no model file")) + tuple((name, "work copy") for name in NOT_THE_WORK_COPY):
        data = tp.document([tp.link("101")], tp.plot_floors("101", 0, 10))
        data["documents"][0]["file"] = name
        prototypes.write_text(json.dumps(data), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError, match=match):
            pf.main(args)
    assert not (tmp_path / "plan_imx.json").exists()


# R321. Names that start like the work copy X-IMX-model_TEST-KX of the key IMX under the job KX and are not it: a floor scenario
# file the placer changed and the master of the scenarios (R118), the source the copy was made from, the copy itself, a work copy
# of another job and of another file of the key
NOT_THE_WORK_COPY = ("X-IMX-model_TEST-WORK-S1", "X-IMX-model_TEST-MASTER", "X-IMX-source", "X-IMX-model", "X-IMX-model_TEST-KY",
                     "X-IMX-model_TEST-KX_run1", "X-IMX-model-old_TEST-KX")


@pytest.mark.parametrize("name", [name.replace("KX", "QX").replace("KY", "QY") for name in NOT_THE_WORK_COPY])
def test_r322_a_read_is_counted_only_when_its_model_is_the_work_copy_of_the_key_copy_under_its_job(tmp_path, name):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", dict(made_up_read(), file=name))

    with pytest.raises(project_profile.ProfileError, match="IMX"):
        key_reads.main(["--settings", str(settings.path), "--reads", str(reads)] + key_args("IMX=QX"))
    assert not (reads / "key_reads.json").exists()


def test_r322_a_job_is_a_job_code_of_the_test_host_so_a_scenario_name_is_never_a_job(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "MASTER", dict(made_up_read(), file="X-IMX-model_TEST-MASTER"))
    write_read(reads, "WORK-S1", dict(made_up_read(), file="X-IMX-model_TEST-WORK-S1"))
    base = ["--settings", str(settings.path), "--reads", str(reads)]

    for job in ("MASTER", "WORK-S1"):
        with pytest.raises(project_profile.ProfileError, match="job"):
            key_reads.main(base + key_args("IMX=" + job))
    assert not (reads / "key_reads.json").exists()


def test_r321_the_work_copy_of_the_key_copy_under_its_job_is_taken_in_any_case_with_or_without_rvt(tmp_path):
    settings = key_settings(tmp_path)
    for name in ("X-IMX-model_TEST-KX", "x-imx-MODEL_test-kx", "X-IMX-model_TEST-KX.rvt"):
        pf.check_key_file(settings, "IMX", name, "KX")
    for name in NOT_THE_WORK_COPY:
        with pytest.raises(project_profile.ProfileError, match="IMX"):
            pf.check_key_file(settings, "IMX", name, "KX")
    with pytest.raises(project_profile.ProfileError, match="no job"):
        pf.check_key_file(settings, "IMX", "X-IMX-model_TEST-KX", None)
    with pytest.raises(project_profile.ProfileError, match="IMZ"):
        pf.check_key_file(settings, "IMZ", "X-IMZ-model_TEST-KX", "KX")
    assert pf.read_job("C:\\reads\\RC\\testhost_RC_prototypes.json") == "RC"
    assert pf.read_job("C:\\reads\\prototypes.json") is None and pf.read_job("testhost_rc_prototypes.json") is None


def test_r321_the_real_keys_take_the_work_copy_of_their_copy_and_refuse_the_scenario_files_the_source_and_another_job():
    settings = project_profile.load_settings()
    keys = settings.values["answerKeys"]["keys"]
    assert len(keys) == 7
    for key in keys:
        stem = PureWindowsPath(key["copy"]).stem
        source = PureWindowsPath(key["source"]).stem
        job, other = "R" + key["code"][-1], "R" + ("A" if key["code"][-1] != "A" else "B")
        pf.check_key_file(settings, key["code"], stem + "_TEST-" + job, job)
        # The floor scenario files the placer changes and their master are made from the source, R118, and from the copy name too
        for name in (source, source + "_TEST-WORK-S1", source + "_TEST-MASTER", source + "_TEST-" + job, stem, stem + "_TEST-WORK-S1",
                     stem + "_TEST-MASTER", stem + "_TEST-" + other):
            if name == stem + "_TEST-" + job:
                continue
            with pytest.raises(project_profile.ProfileError, match=key["code"]):
                pf.check_key_file(settings, key["code"], name, job)


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
        link["parameters"] = {} if link["plotNumber"] is None else {"RRE_ZZ_SP_Plot-Number": link["plotNumber"]}
    floors = [{"typeName": "Slab F", "elementId": 10}, {"typeName": "Slab F", "elementId": 11}, {"typeName": "Yard Y", "elementId": 12},
              {"typeName": "Ramp B", "elementId": 13}, {"typeName": "Footing Z", "elementId": 14}]
    walls = [{"typeName": "Wall 1", "elementId": 20}, {"typeName": "Wall 1", "elementId": 21}, {"typeName": "Wall 2", "elementId": 22}]
    spots = [{"elementId": 30, "value": 1.0, "host": {"elementId": 10, "category": "Floors", "typeName": "Slab F"}},
             {"elementId": 31, "value": 1.5, "host": {"elementId": 13, "category": "Floors", "typeName": "Ramp B"}},
             {"elementId": 32, "host": {"elementId": 3, "linkedElementId": 99}},
             {"elementId": 33}]
    levels = [{"name": "L1"}, {"name": "L2"}]
    return {"kind": "project", "file": "X-IMX-model_TEST-QX", "surveyPoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": [0.0, 0.0, 0.0]}, "links": links, "floors": floors, "walls": walls, "spotElevations": spots, "site": {"levels": levels}}


def write_read(reads, job, document):
    """The three files a read job of the test host leaves, made up, in the folder of the job."""
    folder = reads / job
    folder.mkdir(parents=True, exist_ok=True)
    (folder / ("testhost_" + job + "_prototypes.json")).write_text(json.dumps({"documents": [document]}), encoding="utf-8")
    (folder / ("testhost_" + job + "_run.json")).write_text(json.dumps({"minutes": 1.0, "exitCode": 0, "memory": {"peakPrivateMib": 900}}), encoding="utf-8")
    (folder / ("testhost_" + job + "_log.json")).write_text(json.dumps({"result": "ok", "steps": []}), encoding="utf-8")
    return folder


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


def key_args(*pairs):
    return [part for pair in pairs for part in ("--key", pair)]


def test_r322_a_read_is_counted_only_under_the_key_its_model_file_names_by_code_in_name(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", dict(made_up_read(), file="X-IMX-model_TEST-QX"))
    write_read(reads, "QY", dict(made_up_read(), file="X-IMY-model_TEST-QY"))
    write_read(reads, "QN", dict(made_up_read(), file=None))
    base = ["--settings", str(settings.path), "--reads", str(reads)]

    # Two jobs swapped between their keys would file the counts of one model under the other
    with pytest.raises(project_profile.ProfileError, match="IMX"):
        key_reads.main(base + key_args("IMX=QY", "IMY=QX"))
    with pytest.raises(project_profile.ProfileError, match="no model file"):
        key_reads.main(base + key_args("IMX=QN"))
    assert not (reads / "key_reads.json").exists()
    assert key_reads.main(base + key_args("IMX=QX", "IMY=QY")) == 0


def test_r322_a_run_merges_its_keys_into_the_file_and_keeps_the_keys_of_earlier_runs(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", made_up_read())
    write_read(reads, "QY", dict(made_up_read(), file="X-IMY-model_TEST-QY"))
    base = ["--settings", str(settings.path), "--reads", str(reads)]

    assert key_reads.main(base + key_args("IMX=QX")) == 0
    assert key_reads.main(base + key_args("IMY=QY")) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert list(written["keys"]) == ["IMX", "IMY"]
    assert written["keys"]["IMX"]["counts"]["links"]["instances"] == 3
    # A rerun of one key replaces its entry in its place and leaves the other
    fewer = made_up_read()
    fewer["links"] = fewer["links"][:1]
    write_read(reads, "QX", fewer)
    assert key_reads.main(base + key_args("IMX=QX")) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert list(written["keys"]) == ["IMX", "IMY"]
    assert written["keys"]["IMX"]["counts"]["links"]["instances"] == 1
    assert written["keys"]["IMY"]["counts"]["links"]["instances"] == 3
    # The size of the reader output is in MiB and its field says so
    assert "fileMib" in written["keys"]["IMX"] and "fileMb" not in written["keys"]["IMX"]
    # A file that is no report of R322 is never written over
    (reads / "key_reads.json").write_text("[1, 2]", encoding="utf-8")
    with pytest.raises(ValueError, match="key_reads.json"):
        key_reads.main(base + key_args("IMX=QX"))
    assert (reads / "key_reads.json").read_text(encoding="utf-8") == "[1, 2]"


def test_r322_spots_on_a_link_instance_are_one_host_kind_with_the_link_file_beside_it(tmp_path):
    import key_reads
    document = made_up_read()
    # The SE spots come by the dependents route of R55, the link instance holds them and no linkedElementId is given
    document["spotElevations"] += [{"elementId": 34, "host": {"elementId": 3, "category": "RVT Links", "typeName": "B.rvt : 1 : location"}, "hostSource": "dependents"},
                                   {"elementId": 35, "host": {"elementId": 1, "category": "RVT Links", "typeName": "A.rvt"}, "hostSource": "dependents"}]
    spots = key_reads.counts(document, pf.load_floors(key_settings(tmp_path)))["spots"]

    assert spots["byHost"] == {"Floors: Ramp B": 1, "Floors: Slab F": 1, "in a link": 3, "no host": 1}
    assert spots["inLinkByFile"] == {"A.rvt": 1, "B.rvt": 2}


def test_r322_rows_two_keys_both_hold_are_named_shared_so_a_total_over_keys_counts_them_once(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    two = dict(made_up_read(), file="X-IMY-model_TEST-QY")
    two["links"] = [dict(link, elementId=link["elementId"] + 100) for link in two["links"]]
    # The same id with another row is no shared row, ids of two models meet by chance
    two["walls"][0] = dict(two["walls"][0], typeName="Wall 9")
    two["spotElevations"] = []
    write_read(reads, "QX", made_up_read())
    write_read(reads, "QY", two)
    base = ["--settings", str(settings.path), "--reads", str(reads)]

    assert key_reads.main(base + key_args("IMX=QX", "IMY=QY")) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert written["keys"]["IMX"]["sharedWith"] == {"IMY": {"floors": 5, "walls": 2}}
    assert written["keys"]["IMY"]["sharedWith"] == {"IMX": {"floors": 5, "walls": 2}}
    assert "sharedWith" in written["note"]
    # A later run of one key still names the rows it shares with a key the file keeps
    assert key_reads.main(base + key_args("IMY=QY")) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert written["keys"]["IMX"]["sharedWith"] == {"IMY": {"floors": 5, "walls": 2}}
    assert written["keys"]["IMY"]["sharedWith"] == {"IMX": {"floors": 5, "walls": 2}}
    assert written["sharedNotChecked"] == []
    # A key whose read is gone keeps its counts, and the run says the shares with it were not checked
    import shutil
    shutil.rmtree(reads / "QX")
    assert key_reads.main(base + key_args("IMY=QY")) == 0
    written = json.loads((reads / "key_reads.json").read_text(encoding="utf-8"))
    assert written["sharedNotChecked"] == ["IMX"]
    assert written["keys"]["IMX"]["sharedWith"] is None and written["keys"]["IMY"]["sharedWith"] == {}


def test_r322_a_link_with_no_file_is_no_file_and_two_names_in_another_case_are_one_file():
    import key_reads
    links = [{"file": "A.rvt", "plotNumber": "1"}, {"file": "a.RVT", "plotNumber": "2"}, {"file": None, "plotNumber": "3"}, {"plotNumber": None}]
    assert key_reads.link_counts(links)["files"] == 1
    assert key_reads.link_counts(links)["instances"] == 4


@pytest.mark.parametrize("pairs", [("IMX=QX", "IMX=QX"), ("IMX=QX", "IMX=QY"), ("IMX=QX", "IMY=QX")])
def test_r322_a_key_or_a_job_given_twice_is_refused(tmp_path, pairs):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", made_up_read())
    write_read(reads, "QY", dict(made_up_read(), file="X-IMY-model_TEST-QY"))
    with pytest.raises(SystemExit):
        key_reads.main(["--settings", str(settings.path), "--reads", str(reads)] + key_args(*pairs))
    assert not (reads / "key_reads.json").exists()


# R886. The counts of the read of a built model

def test_r886_a_built_read_is_counted_with_no_key_tie_by_floors_types_into_its_own_folder(tmp_path):
    import compare_built
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    document = made_up_read()
    # A built model is named by no key copy, which --key refuses
    document["file"] = "Site-built.rvt"
    write_read(reads, "QB", document)
    out = tmp_path / "built" / "AAA"

    assert key_reads.main(["--settings", str(settings.path), "--reads", str(reads), "--built", "QB", "--out", str(out)]) == 0

    written = json.loads((out / "key_reads.json").read_text(encoding="utf-8"))
    assert list(written["keys"]) == ["QB"]
    entry = written["keys"]["QB"]
    assert entry["sharedWith"] is None
    # floors.types alone: Slab F is the generic of the key IMX only, so it is of no kind here
    assert entry["counts"]["floors"]["byKind"] == {"generic": 0, "yard": 1, "ramp": 1, "none": 3}
    assert entry["counts"]["walls"]["count"] == 3
    assert not (reads / "key_reads.json").exists()
    assert compare_built.read_counts(out / "key_reads.json") == {"yards": 1, "walls": 3, "levels": 2, "floors": 4}
    with pytest.raises(project_profile.ProfileError):
        key_reads.main(["--settings", str(settings.path), "--reads", str(reads), "--key", "IMX=QB"])


def test_r886_a_built_read_needs_its_own_folder_outside_the_repo_and_a_job_code(tmp_path):
    import key_reads
    settings = key_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QB", made_up_read())
    base = ["--settings", str(settings.path), "--reads", str(reads)]

    with pytest.raises(SystemExit):
        key_reads.main(base + ["--built", "QB"])
    with pytest.raises(SystemExit):
        key_reads.main(base + ["--built", "QB", "--out", str(reads)])
    with pytest.raises(SystemExit):
        key_reads.main(base + ["--key", "IMX=QB", "--out", str(tmp_path / "x")])
    with pytest.raises(SystemExit):
        key_reads.main(base + ["--key", "IMX=QB", "--built", "QB", "--out", str(tmp_path / "x")])
    with pytest.raises(ValueError):
        key_reads.main(base + ["--built", "QB", "--out", str(project_profile.ROOT / "python" / "built")])
    with pytest.raises(project_profile.ProfileError):
        key_reads.main(base + ["--built", "built-1", "--out", str(tmp_path / "x")])
    assert not (project_profile.ROOT / "python" / "built").exists()
