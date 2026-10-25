"""Round ADDIN PR 0b: the default level profile (R277), the villa block drawing (R300), the area codes by job (R301) and
the image step limit of the test host (R302)."""
import argparse
import json
from pathlib import Path

import pytest

import cad_analysis
import data_book
import footprint_check
import full_run
import im_codes
import plan_from_book
import plan_rules
import project_profile

ROOT = Path(__file__).resolve().parents[2]
R06_0000 = "221229_MP-SK-01-03-001_R06_0000.dwg"
R06_0001 = "221229_MP-SK-01-03-001_R06_0001.dwg"

LEVELS = {"frontRoad": {"surface": "roadLevel", "offsetM": 0.0}, "rampSlopePercent": {"default": 0.5}, "rampTypes": {"0.5": "Ramps 0.5%"}}


def profile_with(default):
    profiles = {"note": "dummy", "rev6": dict(LEVELS, height="oneFflPerClusterAtTheHighestEntry"), "old": dict(LEVELS, height="medianPerUnit")}
    if default is not None:
        profiles["default"] = default
    return project_profile.ProjectProfile("dummy.json", {"levelProfiles": profiles})


def real_profile():
    return project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


# R277 the level profile of every new build
def test_r277_a_build_with_no_level_profile_named_takes_the_default_of_the_rules_profile():
    profile = profile_with("rev6")
    assert plan_rules.profile_name(profile, None) == "rev6"
    assert plan_rules.level_profile(profile, None)["height"] == "oneFflPerClusterAtTheHighestEntry"
    assert plan_rules.profile_name(profile, "old") == "old"
    assert plan_rules.level_profile(profile, "old")["height"] == "medianPerUnit"


def test_r277_the_real_rules_profile_builds_by_parsons2024rev6_and_by_native2022_only_when_named():
    real = real_profile()
    assert plan_rules.profile_name(real, None) == "parsons2024Rev6"
    assert plan_rules.profile_name(real, "") == "parsons2024Rev6"
    assert plan_rules.profile_name(real, "native2022") == "native2022"
    assert plan_rules.level_profile(real, None) is plan_rules.level_profile(real, "parsons2024Rev6")


@pytest.mark.parametrize("default", [None, "", "note", "default", "missing"])
def test_r277_a_default_that_is_missing_or_names_no_level_profile_is_refused(default):
    with pytest.raises(project_profile.ProfileError):
        plan_rules.level_profile(profile_with(default), None)


def test_r277_the_default_key_is_never_taken_as_a_profile_name():
    with pytest.raises(project_profile.ProfileError):
        plan_rules.level_profile(profile_with("rev6"), "default")
    with pytest.raises(project_profile.ProfileError):
        plan_rules.level_profile(profile_with("rev6"), "note")


def test_r277_plan_from_book_takes_the_default_profile_when_no_level_profile_is_given():
    args = argparse.Namespace(level_profile=None, overrides=None, override_source=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, project_profile.load_settings(), real_profile(), [], {}, {}, [], None)
    assert rules["name"] == "parsons2024Rev6"
    assert rules["levelProfile"]["height"] == "oneFflPerClusterAtTheHighestEntry"
    named = plan_from_book.rule_inputs(argparse.Namespace(level_profile="native2022", overrides=None, override_source=[], override_fields=None),
                                       project_profile.load_settings(), real_profile(), [], {}, {}, [], None)
    assert named["name"] == "native2022"


SUMMARY = {"written": 0, "plots": 0, "missingByKey": {}, "inACluster": 0, "clusters": 0, "clusterPlots": 0, "roadSurfacesOfFrontCorners": {},
           "plotsWithNoRoadLevel": [], "frontLevelAgainstPlotsCsv": {}, "frontRule": {}, "floorsFound": {}, "floorTypes": {}}


def run_book(tmp_path, monkeypatch, extra):
    """plan_from_book.main on the real config and rules profile with the readers of the drawings, the LandXML and the site
    stubbed, so the rules a build gets and its refusals are seen through main itself."""
    seen = {}
    monkeypatch.setattr(plan_from_book, "book_rows", lambda path: ({}, {}))
    monkeypatch.setattr(plan_from_book.link_summary, "load", lambda path: {"plots": []})
    monkeypatch.setattr(plan_from_book.cad_analysis, "read_road_edges", lambda folder, layers: [])
    monkeypatch.setattr(plan_from_book.plan_from_past, "prototype_folders", lambda root, job: ("folder", "upgraded"))
    monkeypatch.setattr(plan_from_book.plan_from_past, "write_plan", lambda out, plan: None)
    monkeypatch.setattr(plan_from_book, "load_roads", lambda paths, scratch: {})
    monkeypatch.setattr(plan_from_book, "folder_files", lambda folder, profile: {})
    monkeypatch.setattr(plan_from_book, "site_report", lambda path: {})
    monkeypatch.setattr(plan_from_book.floor_fit, "load", lambda path: {})
    monkeypatch.setattr(plan_from_book.prototype_offsets, "load", lambda path: {})
    monkeypatch.setattr(plan_from_book.prototype_offsets, "plot_frame", lambda offsets: {})

    def build(*args):
        seen["rules"] = args[-1]
        return {"summary": SUMMARY}

    monkeypatch.setattr(plan_from_book, "build", build)
    cover = tmp_path / "cover.json"
    cover.write_text(json.dumps({"neighbourhood": "N1", "plots": []}), encoding="utf-8")
    analysis = tmp_path / "analysis.json"
    analysis.write_text(json.dumps({"clusters": []}), encoding="utf-8")
    argv = [str(cover), "--plots-csv", "p.csv", "--landxml", "x", "--site", "s.json", "--baseplan", "b.json", "--roads", "r", "--analysis", str(analysis),
            "--scratch", str(tmp_path), "--out", str(tmp_path / "plan.json")] + extra
    return plan_from_book.main(argv), seen


def overrides_file(tmp_path):
    """Two overrides of each job, with the source models and keys the real override list uses."""
    path = tmp_path / "overrides.json"
    path.write_text(json.dumps({"note": "n", "overrides": [
        {"plot": "1", "field": "entryM", "value": {"gapToRuleM": 0.02}, "source": "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt", "key": "u12"},
        {"plot": "2", "field": "rampType", "value": "flat", "source": "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt", "key": "imc"}]}), encoding="utf-8")
    return path


def test_r277_r279_a_build_that_names_no_profile_gets_the_default_profile_and_the_hand_tables_through_main(tmp_path, monkeypatch, capsys):
    code, seen = run_book(tmp_path, monkeypatch, [])
    assert code == 0, capsys.readouterr().err
    rules = seen["rules"]
    assert rules is not None and rules["name"] == "parsons2024Rev6"
    assert rules["hands"] == real_profile().get("hands") and rules["hands"]
    assert rules["overrides"] == {}
    assert "level profile parsons2024Rev6 (R277), the default of the rules profile" in capsys.readouterr().out


def test_r280_overrides_of_a_native_model_are_refused_on_the_default_parsons_build_through_main(tmp_path, monkeypatch, capsys):
    path = overrides_file(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path), "--override-source", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"])
    err = capsys.readouterr().err
    assert code == 2 and "rules" not in seen
    assert "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt" in err and "native" in err and "parsons2024Rev6" in err and "parsons" in err


def test_r280_overrides_apply_when_the_source_has_the_job_of_the_level_profile_through_main(tmp_path, monkeypatch, capsys):
    path = overrides_file(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path),
                                                  "--override-source", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"])
    assert code == 0, capsys.readouterr().err
    assert seen["rules"]["overrides"] == {"1": {"entryM": {"gapToRuleM": 0.02}}}
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path), "--override-source", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"])
    assert code == 0 and seen["rules"]["overrides"] == {"2": {"rampType": "flat"}}
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022", "--overrides", str(path),
                                                  "--override-source", "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"])
    assert code == 2 and "rules" not in seen


def test_r280_the_job_of_a_source_comes_from_the_keys_of_its_overrides_and_majority_groups():
    groups = project_profile.load_settings().get("majority.groups")
    rows = [{"source": "a.rvt", "key": "u12"}, {"source": "b.rvt", "key": "imc"}, {"source": "c.rvt", "key": "u11"}, {"source": "c.rvt", "key": "imc"},
            {"source": "d.rvt", "key": "zz"}]
    assert plan_rules.source_jobs(rows, {"a.rvt", "b.rvt"}, groups) == {"a.rvt": "native", "b.rvt": "parsons"}
    for source in ("c.rvt", "d.rvt", "e.rvt"):
        with pytest.raises(project_profile.ProfileError):
            plan_rules.source_jobs(rows, {source}, groups)
    real = real_profile()
    assert {name: plan_rules.level_profile(real, name)["job"] for name in ("parsons2024Rev6", "native2022")} == {"parsons2024Rev6": "parsons", "native2022": "native"}


# R300 the villa blocks come from the Building Footprint, R06_0000 and R06_0001 are reference only
def test_r300_the_villa_block_drawings_of_the_real_config_are_the_building_footprint_and_never_r06():
    settings = project_profile.load_settings()
    reference = project_profile.reference_only(settings)
    assert {project_profile.drawing_stem(R06_0000), project_profile.drawing_stem(R06_0001)} <= reference
    villa = settings.get("drawings.villaBlocks")
    assert "Building Footprint" in villa and not project_profile.is_reference_only(villa, settings)
    book = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]
    role = book["join"]["footprint"]["drawing"]
    assert "Building Footprint" in book["drawings"][role] and not project_profile.is_reference_only(book["drawings"][role], settings)
    assert data_book.footprint_drawing(book, settings) == book["drawings"][role]
    # R06_0000 stays a reference column of the data book and a drawing to drawing compare of R176, never the footprint role
    assert role not in book["join"]["masterBlocks"]


def test_r300_a_villa_block_reader_refuses_the_blocks_of_r06_0000_and_r06_0001(tmp_path):
    for name in (R06_0000, R06_0001, "C:\\02\\Master Plan\\" + R06_0000):
        folder = tmp_path / Path(name).name
        folder.mkdir(exist_ok=True)
        (folder / "blocks.json").write_text(json.dumps({"source": name, "blocks": []}), encoding="utf-8")
        with pytest.raises(project_profile.ProfileError):
            cad_analysis.read_blocks(folder)
        with pytest.raises(project_profile.ProfileError):
            footprint_check.footprint_blocks(folder / "blocks.json")
    good = tmp_path / "footprint"
    good.mkdir()
    (good / "blocks.json").write_text(json.dumps({"source": "Site Building Footprint.dwg", "blocks": [{"name": "Villa_VD2"}]}), encoding="utf-8")
    assert cad_analysis.read_blocks(good)["blocks"] == [{"name": "Villa_VD2"}]
    assert footprint_check.footprint_blocks(good / "blocks.json") == [{"name": "Villa_VD2"}]


def test_r300_the_full_run_and_the_data_book_refuse_r06_0000_as_the_villa_block_drawing(tmp_path):
    drawings = {"baseplan": "base.dwg", "masterplan": "second.dwg", "previousMasterplan": "old.dwg", "villaBlocks": R06_0000, "roadLayout": "roads.dwg",
                "xrefRoles": {}, "skip": [], "referenceOnly": [R06_0000, R06_0001]}
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"profile": "rules/other.json", "drawings": drawings}), encoding="utf-8")
    settings = project_profile.load_settings(path)
    (tmp_path / "out" / "base.dwg").mkdir(parents=True)
    (tmp_path / "out" / "base.dwg" / "plots.json").write_text("{}", encoding="utf-8")
    with pytest.raises(project_profile.ProfileError):
        full_run.stage_commands(tmp_path / "out", ["base.dwg"], "second.dwg", "shifts.json", settings)
    book = {"drawings": {"buildingFootprint": "Master Plan/" + R06_0000}, "join": {"footprint": {"drawing": "buildingFootprint"}}}
    with pytest.raises(project_profile.ProfileError):
        data_book.footprint_drawing(book, settings)
    drawings["villaBlocks"] = "villas.dwg"
    path.write_text(json.dumps({"profile": "rules/other.json", "drawings": drawings}), encoding="utf-8")
    assert full_run.stage_commands(tmp_path / "out", ["base.dwg"], "second.dwg", "shifts.json", project_profile.load_settings(path))


@pytest.mark.parametrize("name", [R06_0000, "221229_MP-SK-01-03-001_R06_0000.dxf", "221229_MP-SK-01-03-001_R06_0000 - Copy.dwg",
                                  "221229_mp-sk-01-03-001_r06_0001 (2).DWG", "C:\\x\\221229_MP-SK-01-03-001_R06_0000_copy.dwg",
                                  "Copy of 221229_MP-SK-01-03-001_R06_0001.dwg", "221229_MP-SK-01-03-001_R06_0000.dwg.bak", "work/221229_MP-SK-01-03-001_R06_0000.dwg"])
def test_r300_a_reference_drawing_is_matched_by_its_stem_whatever_the_case_extension_or_copy_suffix(name):
    settings = project_profile.load_settings()
    assert project_profile.is_reference_only(name, settings)
    with pytest.raises(project_profile.ProfileError):
        project_profile.refuse_reference_only(name, settings)


@pytest.mark.parametrize("name", ["221229_MP-SK-01-03-001_R06.dwg", "221229_MP-SK-01-03-001_R06_00001.dwg", "Site Building Footprint.dwg", "", None])
def test_r300_other_drawings_are_not_reference_only(name):
    settings = project_profile.load_settings()
    assert not project_profile.is_reference_only(name, settings)
    assert project_profile.refuse_reference_only(name, settings) == name


def other_settings(tmp_path):
    """A project settings file whose reference only drawing is old_villas.dwg, which the repo config does not name."""
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"profile": "rules/other.json", "drawings": {"villaBlocks": "villas.dwg", "referenceOnly": ["old_villas.dwg"]}}), encoding="utf-8")
    return path


def test_r300_the_villa_block_readers_use_the_settings_their_caller_loaded(tmp_path):
    path = other_settings(tmp_path)
    folder = tmp_path / "blocks"
    folder.mkdir()
    (folder / "blocks.json").write_text(json.dumps({"source": "old_villas.dwg", "blocks": []}), encoding="utf-8")
    assert cad_analysis.read_blocks(folder)["blocks"] == []
    with pytest.raises(project_profile.ProfileError):
        cad_analysis.read_blocks(folder, project_profile.load_settings(path))
    with pytest.raises(project_profile.ProfileError):
        footprint_check.footprint_blocks(folder / "blocks.json", project_profile.load_settings(path))
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"source": "base.dwg", "plots": []}), encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out"), "--cad", str(cad), "--blocks", str(folder), "--project", str(path),
                              "--profile", str(project_profile.profile_path(project_profile.load_settings()))]) == 2
    with pytest.raises(project_profile.ProfileError):
        footprint_check.load_and_run(project_profile.load_settings(path), [], "k", "kp", "t", "tp", "p.csv", str(folder / "blocks.json"), "b.json", "l.csv")


def test_r300_a_blocks_json_in_a_folder_named_after_a_reference_drawing_is_refused(tmp_path):
    folder = tmp_path / "221229_MP-SK-01-03-001_R06_0000.dwg"
    folder.mkdir()
    (folder / "blocks.json").write_text(json.dumps({"blocks": []}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError):
        project_profile.read_villa_blocks(folder / "blocks.json")
    with pytest.raises(project_profile.ProfileError):
        cad_analysis.read_blocks(folder)


# Python files that read a blocks.json or blocks.csv and are no villa block reader, with the reason
NOT_VILLA_READERS = {
    "data_book_cad.py": "writes the stand-in tables of every drawing, R06_0000 among them, as a reference table",
    "data_book_check.py": "counts the texts and blocks of a fresh CadReader run of each drawing against the stand-in tables",
    "xref_search.py": "reads the xref inserts of every drawing to find the xref files",
    "dxf_count.py": "counts the BLOCK records of a DXF",
    "road_layouts.py": "reads the paper space blocks of a road layout DXF",
    "data_book_readme.py": "describes the columns of blocks.csv",
    "data5_check.py": "counts the BLOCK records of a DXF a second way",
    "c3d_drawings.py": "surveys the block names of every drawing of the Roads and Base Plans folders (R154)",
}


def test_r300_every_python_reader_of_unit_blocks_goes_through_the_guard():
    guards = ("read_villa_blocks(", "refuse_reference_only(", "footprint_blocks(", "read_blocks(", "footprint_drawing(")
    unguarded = []
    for path in sorted((ROOT / "python").glob("*.py")):
        text = path.read_text(encoding="utf-8")
        if ("blocks.json" in text or "blocks.csv" in text) and path.name not in NOT_VILLA_READERS and not any(guard in text for guard in guards):
            unguarded.append(path.name)
    assert unguarded == []
    for name in ("data_book_retest.py", "stray_units.py", "c3d_connect.py", "wiring_check.py", "z16_check.py"):
        assert "read_villa_blocks(" in (ROOT / "python" / name).read_text(encoding="utf-8"), name


# R301 the area codes by job
def area(nbh, zone, plots, kind="model", file="m.rvt"):
    return {"nbh": nbh, "zone": zone, "plots": plots, "kind": kind, "file": file, "measured": "a count"}


def test_r301_the_real_table_holds_one_table_per_job_and_every_row_is_measured_with_its_count_and_source():
    table = im_codes.load_table(ROOT / "config" / "area_codes.json")
    assert set(table) == {"parsons", "native"}
    native = {row["code"]: {(a["nbh"], a["zone"], a["plots"]) for a in row["areas"]} for row in table["native"]}
    assert native == {"IMA": {("02", "Zone 3", 309), ("01A", "Zone 4", 84)}, "IMB": {("03", "Zone 3", 431), ("01A", "Zone 4", 150)},
                      "IMC": {("021", "Zone 1", 399), ("013", "Zone 2", 382)}, "IMD": {("012", "Zone 2", 336), ("022", "Zone 1", 294)}}
    assert {row["code"]: row["subPhase"] for row in table["native"]} == {"IMA": "1.1", "IMB": "1.2", "IMC": "1.3", "IMD": "1.4"}
    parsons = {row["code"]: row for row in table["parsons"]}
    # The rows guessed by the letter order in DATA-5 (IMA 021, IMB 022, IME 02, IMF 03) are gone
    assert set(parsons) == {"IMC", "IMD"}
    assert {(a["nbh"], a["plots"], a["kind"]) for a in parsons["IMC"]["areas"]} == {("012", 337, "model"), ("012", 337, "schedule")}
    assert {(a["nbh"], a["plots"], a["kind"]) for a in parsons["IMD"]["areas"]} == {("013", 410, "schedule")}
    text = (ROOT / "config" / "area_codes.json").read_text(encoding="utf-8")
    assert '"guessed"' not in text and '"status"' not in text


@pytest.mark.parametrize("change", [
    lambda t: t["jobs"]["native"][0].update(status="guessed"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(plots=0),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(plots=True),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(kind="letterOrder"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(file=""),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(measured=""),
    lambda t: t["jobs"]["native"][0]["areas"][0].pop("zone"),
    lambda t: t["jobs"]["native"][0].update(areas=[]),
    lambda t: t["jobs"]["native"][0].update(code="IMXY"),
    lambda t: t["jobs"]["native"].append({"code": "IMA", "areas": [area("03", "Zone 3", 4)]}),
    lambda t: t["jobs"].pop("parsons"),
    lambda t: t.pop("jobs"),
])
def test_r301_a_guessed_row_a_row_with_no_count_or_source_a_code_twice_or_a_missing_job_is_refused(tmp_path, change):
    table = {"note": "n", "jobs": {"parsons": [{"code": "IMC", "areas": [area("012", "Zone 2", 337)]}],
                                   "native": [{"code": "IMA", "subPhase": "1.1", "areas": [area("02", "Zone 3", 309), area("01A", "Zone 4", 84)]}]}}
    path = tmp_path / "t.json"
    path.write_text(json.dumps(table), encoding="utf-8")
    assert set(im_codes.load_table(path)) == {"parsons", "native"}
    change(table)
    path.write_text(json.dumps(table), encoding="utf-8")
    with pytest.raises(ValueError):
        im_codes.load_table(path)


def test_r301_a_neighbourhood_two_files_of_one_job_hold_is_allowed():
    rows = [{"code": "IMA", "areas": [area("01A", "Zone 4", 84)]}, {"code": "IMB", "areas": [area("01A", "Zone 4", 150)]}]
    assert im_codes.check_job("native", rows) == rows


def test_r301_a_source_is_held_against_the_row_of_its_code_in_its_own_job_only():
    table = {"parsons": [{"code": "IMA", "areas": [area("021", "Zone 1", 9)]}], "native": [{"code": "IMA", "areas": [area("02", "Zone 3", 9), area("01A", "Zone 4", 3)]}]}
    sources = [{"name": "a", "file": "x-IMA-1.rvt", "job": "native", "codes": ["IMA"], "byZoneAndNbh": {"Zone 3 02": 3, "Zone 4 01A": 1, "noKey": 1}},
               {"name": "b", "file": "x-IMA-1.rvt", "job": "parsons", "codes": ["IMA"], "byZoneAndNbh": {"Zone 3 02": 3}},
               {"name": "c", "file": "x-IMQ-1.rvt", "job": "native", "codes": ["IMQ"], "byZoneAndNbh": {}},
               {"name": "d", "file": "x-IMA-1.rvt", "job": "other", "codes": ["IMA"], "byZoneAndNbh": {}}]
    assert [r["verdict"] for r in im_codes.table_against_sources(table, sources)] == ["agrees", "differs", "noRow", "noJob"]


def test_r301_every_source_of_the_im_code_check_names_a_job_of_the_table():
    sources = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["data5"]["imCodes"]["sources"]
    table = im_codes.load_table(ROOT / "config" / "area_codes.json")
    assert sources and all(source["job"] in table for source in sources)


# R302 the image step limit of the test host
def test_r302_the_image_step_limit_is_read_from_the_test_host_settings_and_is_30_minutes():
    settings = project_profile.load_settings()
    assert settings.get("testHost.imageStepMinutes") == 30
    script = (ROOT / "dotnet" / "SiteAdaptation.TestHost" / "Run-TestHost.ps1").read_text(encoding="utf-8")
    assert "$config.testHost.imageStepMinutes" in script and "$roundJob.imageMinutes" in script
