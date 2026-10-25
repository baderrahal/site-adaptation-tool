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
# A made up codeInName, the real pattern lives in config/project.json only
KEYS = {"codeInName": "^X-{code}-", "keys": [{"code": "IMX"}, {"code": "IMY"}]}
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
    prototypes = tmp_path / "prototypes.json"
    args = ["--settings", str(settings), "--prototypes", str(prototypes), "--baseplan", str(baseplan), "--out", str(tmp_path / "plan.json"), "--key", "IMX"]
    for name, match in (("X-IMY-model_TEST-KY", "IMX"), ("model of no key", "IMX"), (None, "no model file")):
        data = tp.document([tp.link("101")], tp.plot_floors("101", 0, 10))
        data["documents"][0]["file"] = name
        prototypes.write_text(json.dumps(data), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError, match=match):
            pf.main(args)
    assert not (tmp_path / "plan.json").exists()


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


# R323. The second method of the key reads

def categories(**counts):
    names = {"links": "RVT Links", "walls": "Walls", "floors": "Floors", "spots": "Spot Elevations", "footings": "Structural Foundations"}
    return [{"type": "Model", "name": names[key], "count": value} for key, value in counts.items()]


def with_categories(document, **counts):
    document["site"]["categories"] = categories(**counts)
    for floor in document["floors"]:
        floor["parameters"] = {"Category": "Structural Foundations" if floor["typeName"] == "Footing Z" else "Floors"}
    return document


def test_r323_the_counts_match_the_categories_of_r311_and_a_floor_of_another_category_is_counted_apart():
    import key_reads_check as check
    document = with_categories(made_up_read(), links=3, walls=3, floors=4, spots=4, footings=7)
    result = check.by_categories(document)

    assert result["links"] == {"read": 3, "category": 3, "equal": True}
    assert result["walls"] == {"read": 3, "category": 3, "equal": True}
    assert result["spots"] == {"read": 4, "category": 4, "equal": True}
    assert result["floors"] == {"read": 4, "category": 4, "equal": True}
    assert result["floorsInOtherCategories"] == {"Structural Foundations": {"read": 1, "category": 7}}
    short = check.by_categories(with_categories(made_up_read(), links=4, walls=3, floors=5, spots=4, footings=7))
    assert short["links"]["equal"] is False and short["floors"]["equal"] is False


def test_r323_the_link_files_of_the_read_are_held_against_the_link_files_of_the_bytes():
    import key_reads_check as check
    same = check.by_bytes(made_up_read(), [{"file": "A.rvt"}, {"file": "b.RVT"}])
    short = check.by_bytes(made_up_read(), [{"file": "A.rvt"}, {"file": "C.rvt"}])

    assert same == {"read": 2, "bytes": 2, "both": 2, "onlyRead": [], "onlyBytes": []}
    assert short == {"read": 2, "bytes": 2, "both": 1, "onlyRead": ["B.rvt"], "onlyBytes": ["C.rvt"]}


def link_of(element, plot, origin, rotation=30.0, mirrored=False, file="A.rvt"):
    return {"elementId": element, "file": file, "plotNumber": plot, "prototype": "VD2", "case": "A",
            "transform": {"origin": origin, "rotationDeg": rotation, "mirrored": mirrored}}


def test_r323_links_are_held_against_a_second_read_by_id_at_a_seed_and_a_changed_field_is_named():
    import key_reads_check as check
    new = {"links": [link_of(index, str(100 + index), [index, 2.0, 3.0]) for index in range(30)]}
    old = {"links": [link_of(index, str(100 + index), [index, 2.0, 3.0]) for index in range(30)]}
    old["links"][4]["transform"]["origin"] = [4.0, 2.002, 3.0]
    old["links"][5]["transform"]["mirrored"] = True
    seeds = {seed: check.links_by_id(new, old, seed, 20) for seed in (14, 7)}

    for seed, result in seeds.items():
        picked = set(result["ids"])
        assert len(picked) == 20 and result["idsEqual"] is True
        wrong = {4, 5} & picked
        assert result["match"] == 20 - len(wrong) and result["mismatch"] == len(wrong)
        assert sorted(item["elementId"] for item in result["mismatches"]) == sorted(wrong)
    assert seeds[14]["ids"] != seeds[7]["ids"]
    assert check.links_by_id(new, old, 14, 20)["ids"] == seeds[14]["ids"]
    fewer = check.links_by_id({"links": new["links"][:3]}, {"links": new["links"][:2]}, 7, 20)
    assert fewer["idsEqual"] is False and fewer["onlyNew"] == [2] and len(fewer["ids"]) == 2


def test_r323_floors_and_walls_are_held_against_a_second_read_by_their_ids_per_type():
    import key_reads_check as check
    new = made_up_read()
    old = made_up_read()
    old["walls"][2]["typeName"] = "Wall 3"
    result = check.ids_per_type(new, old)

    assert result["floors"] == {"types": 4, "equal": 4, "differ": []}
    assert result["walls"] == {"types": 3, "equal": 1, "differ": ["Wall 2", "Wall 3"]}


OFFSET = [100.0, 200.0, 10.0]


def floor_of(element, type_name, corners, plot=None, thickness=0.15):
    """A made up floor: its corners in internal coordinates (R34) and its outline the same loop in shared ones (R52)."""
    outline = [[[x + OFFSET[0], y + OFFSET[1], z + OFFSET[2]] for x, y, z in corners]]
    return {"elementId": element, "typeName": type_name, "corners": corners, "outline": outline, "thickness": thickness, "plotNumber": plot}


def spot_on(element, host, origin, value, base="Survey Point"):
    return {"elementId": element, "origin": origin, "value": value, "elevationBase": base,
            "host": None if host is None else {"elementId": host[0], "category": host[1], "typeName": host[2]}}


def test_r323_the_spot_value_is_held_against_the_top_of_its_floor_by_the_outline_edge_or_the_corner_plane():
    import key_reads_check as check
    flat = floor_of(1, "Slab G", [[0.0, 0.0, 5.0], [10.0, 0.0, 5.0], [10.0, 10.0, 5.0], [0.0, 10.0, 5.0]])
    sloped = floor_of(2, "Ramp A", [[20.0, 0.0, 5.0], [30.0, 0.0, 5.0], [30.0, 4.0, 5.2], [20.0, 4.0, 5.2]], thickness=0.1)
    # The corners of a warped floor lie on no plane, Revit splits its top on a diagonal the read does not give
    warped = floor_of(3, "Ramp A", [[40.0, 0.0, 5.0], [50.0, 0.0, 5.0], [50.0, 4.0, 5.3], [40.0, 4.0, 5.0]], thickness=0.1)
    on_flat, on_ramp = (1, "Floors", "Slab G"), (2, "Floors", "Ramp A")
    spots = [spot_on(11, on_flat, [5.0, 5.0, 5.0], 15.0),
             spot_on(12, on_ramp, [25.0, 2.0, 5.1], 15.1),
             spot_on(13, on_ramp, [30.0, 4.0, 5.2], 15.2),
             # On the edge of the flat floor, the value of its bottom, a gap of minus its thickness
             spot_on(14, on_flat, [10.0, 5.0, 5.0], 14.85),
             spot_on(15, (3, "Floors", "Ramp A"), [50.0, 4.0, 5.3], 15.3),
             spot_on(16, (3, "Floors", "Ramp A"), [45.0, 2.0, 5.1], 15.1),
             spot_on(17, on_flat, [2.0, 3.0, 5.0], 15.003),
             spot_on(18, on_flat, [2.0, 3.0, 5.0], None),
             spot_on(19, on_flat, [2.0, 3.0, 5.0], 15.0, base="Project Base Point"),
             spot_on(20, (9, "RVT Links", "B.rvt"), [2.0, 3.0, 5.0], 15.0),
             spot_on(21, None, [2.0, 3.0, 5.0], 15.0)]
    document = {"surveyPoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": OFFSET}, "floors": [flat, sloped, warped],
                "links": [{"elementId": 9, "file": "B.rvt"}], "spotElevations": spots}
    result = check.spot_tops(document)

    assert result["compared"] == 6
    assert result["byRoute"] == {"edge": 3, "plane": 3}
    assert result["skipped"] == {"base Project Base Point": 1, "noValue": 1, "notOnAFloorOfTheRead": 2, "warped": 1}
    assert result["byHost"]["Floors: Slab G"] == {"count": 3, "within1Mm": 1, "within5Mm": 2, "share1Mm": 0.3333, "share5Mm": 0.6667, "worstMm": 150.0}
    assert result["byHost"]["Floors: Ramp A"] == {"count": 3, "within1Mm": 3, "within5Mm": 3, "share1Mm": 1.0, "share5Mm": 1.0, "worstMm": 0.0}
    assert result["all"]["count"] == 6 and result["all"]["within1Mm"] == 4 and result["all"]["within5Mm"] == 5
    assert result["atFloorBottom"] == 1
    assert result["worst"][0] == {"elementId": 14, "host": "Floors: Slab G", "floorId": 1, "gapMm": -150.0, "route": "edge", "thicknessMm": 150.0}
    assert result["worst"][1]["elementId"] == 17 and result["worst"][1]["gapMm"] == 3.0


def test_r323_a_plane_needs_three_corners_that_span_it_and_a_point_off_the_outline_is_not_on_its_edge():
    import key_reads_check as check
    assert check.plane_through([[0.0, 0.0, 1.0], [1.0, 1.0, 2.0]]) is None
    assert check.plane_through([[0.0, 0.0, 1.0], [1.0, 1.0, 2.0], [2.0, 2.0, 3.0]]) is None
    level, worst = check.plane_through([[0.0, 0.0, 1.0], [2.0, 0.0, 1.0], [0.0, 2.0, 3.0]])
    assert abs(level(1.0, 1.0) - 2.0) < 1e-9 and worst < 1e-9
    floor = floor_of(1, "Slab G", [[0.0, 0.0, 5.0], [10.0, 0.0, 5.0], [10.0, 10.0, 5.0], [0.0, 10.0, 5.0]])
    assert check.top_at(floor, 110.004, 205.0, OFFSET) == (15.0, "edge")
    assert check.top_at(floor, 105.0, 205.0, OFFSET)[1] == "plane"
    assert check.top_at({"elementId": 2}, 105.0, 205.0, OFFSET) == (None, "noGeometry")


def test_r323_a_read_with_no_survey_point_gives_its_spot_check_as_not_read_and_the_other_routes_run(tmp_path):
    import key_reads_check as check
    document = with_categories(made_up_read(), links=3, walls=3, floors=4, spots=4, footings=7)
    document["surveyPoint"] = None
    result = check.check_key(document, None, [], pf.load_floors(key_settings(tmp_path)))

    assert result["spotTops"] == {"notRead": "the read holds no survey point, so no spot is put in shared coordinates"}
    assert result["categories"]["links"]["equal"] is True
    del document["surveyPoint"]
    assert check.spot_tops(document)["notRead"].startswith("the read holds no survey point")


def square(x, y, size=10.0, z=5.0):
    return [[x, y, z], [x + size, y, z], [x + size, y + size, z], [x, y + size, z]]


def plot_link(element, plot):
    return {"elementId": element, "file": "A.rvt", "parameters": {"RRE_ZZ_SP_Plot-Number": plot}}


def test_r323_each_unit_plot_holds_one_link_generic_floor_yard_and_ramp_counted_by_the_plot_of_each_element(tmp_path):
    import key_reads_check as check
    settings = key_settings(tmp_path)
    links = [plot_link(1, "101"), plot_link(2, "102"), plot_link(3, "103"), plot_link(4, "103"), {"elementId": 5, "file": "L.rvt", "parameters": {}}]
    floors = [floor_of(10, "Slab F", square(0.0, 0.0), "101"), floor_of(11, "Slab F", square(20.0, 0.0), "102"), floor_of(12, "Slab F", square(40.0, 0.0), "103"),
              floor_of(13, "Ramp A", square(0.0, -5.0, 4.0), "101"), floor_of(14, "Ramp B", square(40.0, -5.0, 4.0), "103"),
              # Two yards with no Plot parameter, in the generic floors of 101 and 103, one in none, and one that names 102
              floor_of(20, "Yard Y", square(1.0, 1.0, 3.0)), floor_of(21, "Yard Y", square(41.0, 1.0, 3.0)), floor_of(22, "Yard Y", square(70.0, 0.0, 3.0)),
              floor_of(23, "Yard Y", square(21.0, 1.0, 3.0), "102"),
              # A floor of the generic type of floors.types, of no kind for IMX
              floor_of(30, "Slab G", square(60.0, 0.0), "104")]
    document = {"surveyPoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": OFFSET}, "links": links, "floors": floors}
    rule = check.plot_rule(document, pf.load_floors(settings, "IMX"))

    assert rule["plots"] == 3 and rule["hold"] == 1 and rule["break"] == 2
    assert rule["breaks"] == [{"plot": "102", "counts": {"link": 1, "generic": 1, "yard": 1, "ramp": 0}, "why": ["no ramp"]},
                              {"plot": "103", "counts": {"link": 2, "generic": 1, "yard": 1, "ramp": 1}, "why": ["2 links"]}]
    assert rule["byReason"] == {"2 links": 1, "no ramp": 1}
    assert rule["yards"] == {"byPlotParameter": 1, "byGenericShape": 2, "noPlot": {"in no generic floor": 1}}
    assert rule["notNamed"] == {"link": 1, "generic": 0, "ramp": 0}
    assert rule["floorsOfNoKind"] == {"Slab G": 1}

    # R321. Read by floors.types alone the Slab F floors are of no kind and no plot holds a generic floor
    alone = check.plot_rule(document, pf.load_floors(settings, "IMY"))
    assert alone["hold"] == 0
    assert alone["byReason"]["no generic floor"] == 3
    assert alone["floorsOfNoKind"] == {"Slab F": 3}


def test_r323_main_holds_a_read_against_the_bytes_of_its_key_copy_and_its_second_read_and_writes_outside_the_repo(tmp_path):
    import key_reads_check as check
    from cfb_fixture import build
    from test_rvt_info import INFO, XML, utf16
    root = tmp_path / "root"
    (root / "copies").mkdir(parents=True)
    (root / "copies" / "x-IMX.rvt").write_bytes(build([("BasicFileInfo", utf16(INFO)), ("TransmissionData", utf16(XML, b"\x84-\x00\x00"))]))
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"inputRoot": str(root), "answerKeys": {"folder": "copies", "codeInName": KEYS["codeInName"], "keys": [{"code": "IMX", "copy": "x-IMX.rvt"}]},
                                "floors": FLOORS}), encoding="utf-8")
    document = with_categories(made_up_read(), links=3, walls=3, floors=4, spots=4, footings=7)
    for link in document["links"][:2]:
        link["file"] = "RRE-WP-NSP-KEO-ARC-D4-ZZ-MOD-2400.rvt"
    document["surveyPoint"] = {"position": [0.0, 0.0, 0.0], "sharedPosition": [0.0, 0.0, 0.0]}
    reads = tmp_path / "reads"
    (reads / "QX").mkdir(parents=True)
    (reads / "QX" / "testhost_QX_prototypes.json").write_text(json.dumps({"documents": [document]}), encoding="utf-8")
    old = tmp_path / "old.json"
    old.write_text(json.dumps({"documents": [made_up_read()]}), encoding="utf-8")

    assert check.main(["--settings", str(path), "--reads", str(reads), "--key", "IMX=QX=" + str(old)]) == 0
    written = json.loads((reads / "key_reads_check.json").read_text(encoding="utf-8"))["keys"]["IMX"]
    assert written["bytes"] == {"read": 2, "bytes": 1, "both": 1, "onlyRead": ["B.rvt"], "onlyBytes": []}
    assert written["categories"]["walls"]["equal"] is True
    # The Phase 1 read of the same copy repeats the read, it is named so and is no second method
    repeat = written["repeatRead"]
    assert repeat["file"] == "old.json" and "no second method" in repeat["note"]
    assert repeat["links"]["14"]["idsEqual"] is True and repeat["links"]["14"]["match"] == 1
    assert repeat["links"]["14"]["mismatches"] == [{"elementId": 1, "fields": ["file"]}, {"elementId": 2, "fields": ["file"]}]
    assert repeat["idsPerType"]["walls"]["differ"] == []
    assert "links" not in written and "idsPerType" not in written and "secondRead" not in written
    assert written["plotRule"]["plots"] == 2 and written["spotTops"]["compared"] == 0
    with pytest.raises(ValueError):
        check.main(["--settings", str(path), "--reads", str(project_profile.ROOT / "python"), "--key", "IMX=QX=none"])
    with pytest.raises(project_profile.ProfileError):
        check.main(["--settings", str(path), "--reads", str(reads), "--key", "IMZ=QX=none"])


def check_settings(tmp_path):
    """Made up settings of the check: two keys with their copies, codeInName and the floors block."""
    from cfb_fixture import build
    from test_rvt_info import INFO, XML, utf16
    root = tmp_path / "root"
    (root / "copies").mkdir(parents=True)
    for code in ("IMX", "IMY"):
        (root / "copies" / ("x-" + code + ".rvt")).write_bytes(build([("BasicFileInfo", utf16(INFO)), ("TransmissionData", utf16(XML, b"\x84-\x00\x00"))]))
    path = tmp_path / "project.json"
    keys = {"folder": "copies", "codeInName": KEYS["codeInName"], "keys": [{"code": code, "copy": "x-" + code + ".rvt"} for code in ("IMX", "IMY")]}
    path.write_text(json.dumps({"inputRoot": str(root), "answerKeys": keys, "floors": dict(FLOORS, keyTypes={"IMX": {"generic": ["Slab F"]}})}), encoding="utf-8")
    return path


def test_r323_a_read_is_checked_only_under_the_key_its_model_file_names_and_a_key_given_twice_is_refused(tmp_path):
    import key_reads_check as check
    path = check_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", dict(made_up_read(), file="X-IMX-model_TEST-QX"))
    write_read(reads, "QY", dict(made_up_read(), file="X-IMY-model_TEST-QY"))
    old = tmp_path / "old.json"
    old.write_text(json.dumps({"documents": [dict(made_up_read(), file="X-IMY-model_TEST-KY")]}), encoding="utf-8")
    base = ["--settings", str(path), "--reads", str(reads)]

    with pytest.raises(project_profile.ProfileError, match="IMX"):
        check.main(base + key_args("IMX=QY=none", "IMY=QX=none"))
    # The repeat read of another key is refused too
    with pytest.raises(project_profile.ProfileError, match="IMX"):
        check.main(base + key_args("IMX=QX=" + str(old)))
    for pairs in (("IMX=QX=none", "IMX=QX=none"), ("IMX=QX=none", "IMY=QX=none")):
        with pytest.raises(SystemExit):
            check.main(base + key_args(*pairs))
    assert not (reads / "key_reads_check.json").exists()
    assert check.main(base + key_args("IMX=QX=none", "IMY=QY=" + str(old))) == 0


def test_r323_a_run_merges_its_keys_into_the_file_and_keeps_the_keys_of_earlier_runs(tmp_path):
    import key_reads_check as check
    path = check_settings(tmp_path)
    reads = tmp_path / "reads"
    write_read(reads, "QX", made_up_read())
    write_read(reads, "QY", dict(made_up_read(), file="X-IMY-model_TEST-QY"))
    base = ["--settings", str(path), "--reads", str(reads)]

    assert check.main(base + key_args("IMY=QY=none")) == 0
    assert check.main(base + key_args("IMX=QX=none")) == 0
    written = json.loads((reads / "key_reads_check.json").read_text(encoding="utf-8"))
    assert list(written["keys"]) == ["IMY", "IMX"]
    assert written["keys"]["IMY"]["job"] == "QY" and written["keys"]["IMX"]["job"] == "QX"
    (reads / "key_reads_check.json").write_text("{\"keys\": 3}", encoding="utf-8")
    with pytest.raises(ValueError, match="key_reads_check.json"):
        check.main(base + key_args("IMX=QX=none"))
