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


@pytest.mark.parametrize("extra", [[], ["--override-source"]])
def test_r280_overrides_with_no_override_source_are_refused_through_main_and_never_applied_as_none(tmp_path, monkeypatch, capsys, extra):
    path = overrides_file(tmp_path)
    code, seen = run_book(tmp_path, monkeypatch, ["--overrides", str(path)] + extra)
    err = capsys.readouterr().err
    assert code == 2 and "rules" not in seen
    assert "--override-source" in err and "R280" in err


@pytest.mark.parametrize("extra", [["--override-source", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"], ["--overrides", ""], ["--overrides", "  "],
                                   ["--overrides", "", "--override-source", "01101-PSN-IMB-ZZZ-PIM-TU-000002.rvt"]])
def test_r280_an_override_source_with_no_override_list_or_an_empty_list_name_is_refused_through_main(tmp_path, monkeypatch, capsys, extra):
    code, seen = run_book(tmp_path, monkeypatch, ["--level-profile", "native2022"] + extra)
    err = capsys.readouterr().err
    assert code == 2 and "rules" not in seen
    assert "--overrides" in err and "R280" in err


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


@pytest.mark.parametrize("name", ["Copy (2) of 221229_MP-SK-01-03-001_R06_0000.dwg", "Copy of Copy of 221229_MP-SK-01-03-001_R06_0001.dwg",
                                  "221229_MP-SK-01-03-001_R06_0000 - Copy.dwg", "221229_MP-SK-01-03-001_R06_0001 - Copy.dxf",
                                  "copy (3) of 221229_mp-sk-01-03-001_r06_0000 - copy (2).DXF", "C:\\x\\Copy of 221229_MP-SK-01-03-001_R06_0000.dwg",
                                  "old_221229_MP-SK-01-03-001_R06_0001.dwg"])
def test_r300_a_reference_drawing_is_matched_by_its_full_stem_as_a_whole_token_whatever_comes_before_or_after(name):
    settings = project_profile.load_settings()
    assert project_profile.is_reference_only(name, settings)
    with pytest.raises(project_profile.ProfileError):
        project_profile.refuse_reference_only(name, settings)


@pytest.mark.parametrize("name", ["221229_MP-SK-01-03-001_R06_00001.dwg", "Copy of 221229_MP-SK-01-03-001_R06_00001.dwg", "221229_MP-SK-01-03-001_R06_00000 - Copy.dwg",
                                  "1221229_MP-SK-01-03-001_R06_0000.dwg", "x221229_MP-SK-01-03-001_R06_0001.dxf", "221229_MP-SK-01-03-001_R06_0000a.dwg",
                                  "C:\\221229_MP-SK-01-03-001_R06_0000\\other.dwg"])
def test_r300_a_different_stem_that_holds_the_reference_stem_is_not_matched(name):
    assert not project_profile.is_reference_only(name, project_profile.load_settings())


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


def test_r301_a_guessed_status_on_an_area_is_refused(tmp_path):
    table = {"note": "n", "jobs": {"parsons": [{"code": "IMC", "areas": [dict(area("012", "Zone 2", 337), status="guessed")]}], "native": []}}
    path = tmp_path / "t.json"
    path.write_text(json.dumps(table), encoding="utf-8")
    with pytest.raises(ValueError):
        im_codes.load_table(path)


def test_r301_a_source_with_no_keyed_plot_gets_its_own_verdict_and_the_zone_counts_with_the_neighbourhood():
    table = {"parsons": [{"code": "IMC", "areas": [area("012", "Zone 2", 9)]}], "native": []}
    sources = [{"name": "a", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"noKey": 4}},
               {"name": "b", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {}},
               {"name": "c", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 4 012": 3}},
               {"name": "d", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 012": 3, "noKey": 1}}]
    assert [r["verdict"] for r in im_codes.table_against_sources(table, sources)] == ["noKeyedPlot", "noKeyedPlot", "differs", "agrees"]


def test_r301_a_verdict_carries_the_keyed_and_unkeyed_plot_counts_and_a_mostly_unkeyed_source_never_agrees():
    table = {"parsons": [{"code": "IMC", "areas": [area("012", "Zone 2", 9)]}], "native": []}
    sources = [{"name": "a", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 012": 2, "noKey": 3}},
               {"name": "b", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 4 012": 1, "noKey": 5}},
               {"name": "c", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 012": 2, "noKey": 2}},
               {"name": "d", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 012": 7}},
               {"name": "e", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 012": 1, "Zone 4 012": 1, "noKey": 9}}]
    found = im_codes.table_against_sources(table, sources)
    assert [r["verdict"] for r in found] == ["mostlyUnkeyed", "differs", "agrees", "agrees", "differs"]
    assert [(r["keyedPlots"], r["unkeyedPlots"]) for r in found] == [(2, 3), (1, 5), (2, 2), (7, 0), (2, 9)]


def test_r301_a_keyed_pair_the_row_does_not_hold_differs_whatever_the_unkeyed_count():
    table = {"parsons": [{"code": "IMC", "areas": [area("012", "Zone 2", 9)]}], "native": []}
    for unkeyed in (0, 1, 50):
        source = {"name": "a", "file": "x-IMC-1.rvt", "job": "parsons", "codes": ["IMC"], "byZoneAndNbh": {"Zone 2 013": 1, "noKey": unkeyed}}
        assert im_codes.table_against_sources(table, [source])[0]["verdict"] == "differs"


@pytest.mark.parametrize("change", [
    lambda t: t["jobs"]["native"][0]["areas"][0].update(guess="by letter order"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(note="any other field"),
    lambda t: t["jobs"]["native"][0].update(guessed=True),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh=12),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(measured=["a list"]),
])
def test_r301_an_area_carries_its_measured_fields_and_nothing_else(tmp_path, change):
    table = {"note": "n", "jobs": {"parsons": [], "native": [{"code": "IMA", "subPhase": "1.1", "areas": [area("02", "Zone 3", 309)]}]}}
    path = tmp_path / "t.json"
    path.write_text(json.dumps(table), encoding="utf-8")
    assert set(im_codes.load_table(path)) == {"parsons", "native"}
    change(table)
    path.write_text(json.dumps(table), encoding="utf-8")
    with pytest.raises(ValueError):
        im_codes.load_table(path)
    assert '"status"' not in (ROOT / "python" / "im_codes.py").read_text(encoding="utf-8")


@pytest.mark.parametrize("change", [
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh=" 02"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh="2"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh="0212"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh="01AB"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(nbh="02 Zone 3"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(zone="Zone 3 "),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(zone="zone 3"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(zone="Zone3"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(zone="3"),
    lambda t: t["jobs"]["native"][0]["areas"][0].update(plots=309.0),
    lambda t: t["jobs"]["native"][0].update(subPhase=1.1),
    lambda t: t["jobs"]["native"][0].update(subPhase=" 1.1"),
    lambda t: t["jobs"]["native"][0].update(subPhase="one"),
    lambda t: t["jobs"]["native"][0].update(subPhase=["1.1"]),
    lambda t: t["jobs"]["native"][0].update(code=["IMA"]),
    lambda t: t["jobs"]["native"][0].update(code={"IMA": 1}),
    lambda t: t["jobs"]["native"][0].update(code=" IMA"),
])
def test_r301_names_counts_and_sub_phases_have_their_type_and_form_and_a_bad_code_is_a_value_error(tmp_path, change):
    table = {"note": "n", "jobs": {"parsons": [{"code": "IMC", "areas": [area("012", "Zone 2", 337)]}],
                                   "native": [{"code": "IMA", "subPhase": "1.1", "areas": [area("02", "Zone 3", 309), area("01A", "Zone 4", 84)]}]}}
    path = tmp_path / "t.json"
    path.write_text(json.dumps(table), encoding="utf-8")
    assert set(im_codes.load_table(path)) == {"parsons", "native"}
    change(table)
    path.write_text(json.dumps(table), encoding="utf-8")
    with pytest.raises(ValueError):
        im_codes.load_table(path)


def test_r301_every_neighbourhood_id_of_the_real_table_has_the_dlus_form_and_a_job_with_no_sub_phase_needs_none():
    table = im_codes.load_table(ROOT / "config" / "area_codes.json")
    assert all(im_codes.NBH_FORM.fullmatch(a["nbh"]) and im_codes.ZONE_FORM.fullmatch(a["zone"]) for rows in table.values() for row in rows for a in row["areas"])
    assert all("subPhase" not in row for row in table["parsons"]) and all(im_codes.SUB_PHASE_FORM.fullmatch(row["subPhase"]) for row in table["native"])
    for value in ("02", "03", "010", "011", "012", "013", "021", "022", "01A"):
        assert im_codes.NBH_FORM.fullmatch(value), value


def test_r301_the_job_names_come_from_majority_groups_of_the_project_settings(tmp_path):
    assert set(im_codes.job_names(project_profile.load_settings())) == set(project_profile.load_settings().get("majority.groups"))
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"majority": {"groups": {"jobA": ["k1"], "jobB": ["k2"]}}}), encoding="utf-8")
    table = tmp_path / "t.json"
    table.write_text(json.dumps({"note": "n", "jobs": {"jobA": [{"code": "IMC", "areas": [area("012", "Zone 2", 1)]}], "jobB": []}}), encoding="utf-8")
    assert set(im_codes.load_table(table, project_profile.load_settings(settings))) == {"jobA", "jobB"}
    with pytest.raises(ValueError):
        im_codes.load_table(table)
    assert "parsons" not in (ROOT / "python" / "im_codes.py").read_text(encoding="utf-8")


def test_r301_a_count_is_a_positive_whole_number_and_never_a_flag():
    assert im_codes.measured_count(1) and im_codes.measured_count(337)
    assert not any(im_codes.measured_count(value) for value in (0, -3, True, 1.5, "337", None))


def test_r301_every_source_of_the_im_code_check_names_a_job_of_the_table():
    sources = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["data5"]["imCodes"]["sources"]
    table = im_codes.load_table(ROOT / "config" / "area_codes.json")
    assert sources and all(source["job"] in table for source in sources)


MODEL_A = "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1.rvt"


def manifest_entry(copy, source):
    """One entry of the manifest of a copy folder as key_copies.py writes it (R312): the copy, its hash, its source and its hash."""
    return {"code": "IMA", "copy": copy, "copySha256": "a" * 64, "source": source, "sourceSha256": "b" * 64, "format": 2024,
            "madeBy": "the siteUpgrade job KA of a dummy round"}


def write_manifest(folder, *entries, name="key_copies.json"):
    (folder / name).write_text(json.dumps({"note": "dummy", "copies": list(entries)}), encoding="utf-8")


def copy_tree(tmp_path, folder="Revit All Zones/2024", manifest="key_copies.json"):
    """A source root with one model, its copy in a copy folder with a manifest that names it, and a file in a folder whose name only
    starts like the copy folder."""
    for where, name in (("Revit All Zones", MODEL_A), ("Revit All Zones/2024", MODEL_A), ("Revit All Zones/2024 old", "01101-PSN-IMB-ZZZ-PIM-SE-000000-zone1.rvt")):
        (tmp_path / where).mkdir(parents=True, exist_ok=True)
        (tmp_path / where / name).write_bytes(b"")
    write_manifest(tmp_path / "Revit All Zones" / "2024", manifest_entry(MODEL_A, "Revit All Zones\\" + MODEL_A))
    return [{"folder": folder, "manifest": manifest, "reason": "the Revit 2024 copies of a dummy round"}]


def both_counts(root, copies):
    """The counts of im_codes.py and the check of its second method in data5_check.py against them."""
    import data5_check
    counts = im_codes.name_counts(im_codes.file_names(root, im_codes.copy_folders(copies)))
    return counts, data5_check.check_names(root, {"codes": counts}, copies)


def test_r301_the_names_of_a_copy_folder_of_the_config_are_counted_apart_so_the_name_counts_of_the_sources_keep(tmp_path):
    copies = copy_tree(tmp_path)
    names = im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    counts = im_codes.name_counts(names)
    assert counts["IMA"]["fileNames"] == 1 and counts["IMA"]["copyFileNames"] == 1 and counts["IMA"]["notInManifest"] == 0
    assert counts["IMB"]["fileNames"] == 1 and counts["IMB"]["copyFileNames"] == 0
    assert counts["IMA"]["copyFolders"] == {"Revit All Zones/2024": 1}
    assert counts["IMA"]["folders"] == {"Revit All Zones": 1}
    # With no copy folder the copy counts as a source file, as it did before
    assert im_codes.name_counts(im_codes.file_names(tmp_path))["IMA"]["fileNames"] == 2
    assert im_codes.file_names(tmp_path)[0].get("copyFolder") is None


def test_r301_the_second_method_counts_the_names_of_a_copy_folder_apart_too(tmp_path):
    import data5_check
    copies = copy_tree(tmp_path)
    report = {"codes": {"IMA": {"fileNames": 1, "copyFileNames": 1}, "IMB": {"fileNames": 1}}}
    assert data5_check.check_names(tmp_path, report, copies) == {"sources": {"IMA": [1, 1], "IMB": [1, 1]}, "copies": {"IMA": [1, 1]}, "notInManifest": {}}
    # An im_codes.json written before copy folders counted the copy with the sources, the check shows the copy against 0
    older = {"codes": {"IMA": {"fileNames": 1}, "IMB": {"fileNames": 1}}}
    assert data5_check.check_names(tmp_path, older, copies)["copies"] == {"IMA": [1, 0]}


@pytest.mark.parametrize("folder", ["revit all zones/2024", "REVIT ALL ZONES\\2024", "Revit all Zones/2024"])
def test_r301_a_copy_folder_and_its_manifest_match_in_any_case_in_the_check_and_its_second_method(tmp_path, folder):
    # Windows matches names in any case, so a folder, a manifest and a copy name written in another case are the same
    copies = copy_tree(tmp_path, folder=folder, manifest="KEY_COPIES.JSON")
    write_manifest(tmp_path / "Revit All Zones" / "2024", manifest_entry(MODEL_A.upper(), "REVIT ALL ZONES\\" + MODEL_A))
    counts, check = both_counts(tmp_path, copies)
    assert counts["IMA"]["fileNames"] == 1 and counts["IMA"]["copyFileNames"] == 1
    assert check["sources"]["IMA"] == [1, 1] and check["copies"]["IMA"] == [1, 1]


@pytest.mark.parametrize("folder", [" Revit All Zones/2024", "Revit All Zones/2024 ", "Revit All Zones /2024", "Revit All Zones/ 2024", "Revit All Zones/2024/"])
def test_r301_a_copy_folder_with_a_blank_around_it_or_a_part_is_refused_in_the_check_and_its_second_method(tmp_path, folder):
    import data5_check
    copies = copy_tree(tmp_path, folder=folder)
    with pytest.raises(ValueError):
        im_codes.copy_folders(copies)
    with pytest.raises(ValueError):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)


def test_r301_a_file_in_a_copy_folder_its_manifest_does_not_name_stays_a_source_and_is_flagged_in_the_check_and_its_second_method(tmp_path):
    copies = copy_tree(tmp_path)
    # A stray source right in the copy folder and a file in a folder inside it, neither named by the manifest
    (tmp_path / "Revit All Zones" / "2024" / "01101-PSN-IMH-ZZZ-PIM-TU-000001-zone9.rvt").write_bytes(b"")
    (tmp_path / "Revit All Zones" / "2024" / "old").mkdir()
    (tmp_path / "Revit All Zones" / "2024" / "old" / MODEL_A).write_bytes(b"")
    counts, check = both_counts(tmp_path, copies)
    assert counts["IMH"] == {"fileNames": 1, "folders": {str(Path("Revit All Zones") / "2024"): 1}, "copyFileNames": 0, "copyFolders": {}, "notInManifest": 1}
    assert counts["IMA"]["fileNames"] == 2 and counts["IMA"]["copyFileNames"] == 1 and counts["IMA"]["notInManifest"] == 1
    flagged = [entry for entry in im_codes.file_names(tmp_path, im_codes.copy_folders(copies)) if entry.get("notInManifest")]
    assert sorted(entry["file"] for entry in flagged) == sorted([MODEL_A, "01101-PSN-IMH-ZZZ-PIM-TU-000001-zone9.rvt"])
    assert check == {"sources": {"IMA": [2, 2], "IMB": [1, 1], "IMH": [1, 1]}, "copies": {"IMA": [1, 1]}, "notInManifest": {"IMA": [1, 1], "IMH": [1, 1]}}


def test_r301_a_copy_folder_listed_too_high_is_refused_in_the_check_and_its_second_method(tmp_path):
    import data5_check
    copies = copy_tree(tmp_path, folder="Revit All Zones")
    # Revit All Zones holds the sources and no manifest of its own
    with pytest.raises(ValueError, match="manifest"):
        im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    with pytest.raises(ValueError, match="manifest"):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)
    # With a manifest there, the sources it names sit inside the folder, which no copy folder may hold
    write_manifest(tmp_path / "Revit All Zones", manifest_entry(MODEL_A, "Revit All Zones\\" + MODEL_A))
    with pytest.raises(ValueError, match="too high"):
        im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    with pytest.raises(ValueError, match="too high"):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)


@pytest.mark.parametrize("change", [{"copySha256": None}, {"copy": "2024/" + MODEL_A}, {"source": "..\\x.rvt"}, {"source": "C:\\x.rvt"}, {"copy": None}])
def test_r301_a_manifest_entry_that_names_no_copy_with_its_source_under_the_root_is_refused_in_both_methods(tmp_path, change):
    import data5_check
    copies = copy_tree(tmp_path)
    write_manifest(tmp_path / "Revit All Zones" / "2024", {**manifest_entry(MODEL_A, "Revit All Zones\\" + MODEL_A), **change})
    with pytest.raises(ValueError):
        im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    with pytest.raises(ValueError):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)


@pytest.mark.parametrize("text", ["sources.txt line", "[]", "{\"copies\": {}}"])
def test_r301_a_manifest_that_is_no_json_list_of_copies_is_refused_in_both_methods(tmp_path, text):
    import data5_check
    copies = copy_tree(tmp_path)
    (tmp_path / "Revit All Zones" / "2024" / "key_copies.json").write_text(text, encoding="utf-8")
    with pytest.raises(ValueError):
        im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    with pytest.raises(ValueError):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)


@pytest.mark.parametrize("folder", ["C:/02-Site Adaptation/Revit All Zones/2024", "../2024", "Revit All Zones/../2024", "Revit All Zones/./2024", "", None])
def test_r301_a_copy_folder_is_a_folder_under_the_source_root_with_its_manifest_and_reason(folder):
    with pytest.raises(ValueError):
        im_codes.copy_folders([{"folder": folder, "manifest": "key_copies.json", "reason": "x"}])
    with pytest.raises(ValueError):
        im_codes.copy_folders([{"folder": "Revit All Zones/2024", "manifest": "key_copies.json", "reason": ""}])
    with pytest.raises(ValueError):
        im_codes.copy_folders([{"folder": "Revit All Zones/2024", "manifest": "key_copies.json", "reason": "x", "extra": 1}])
    for manifest in ("", " key_copies.json", "2024/key_copies.json", None):
        with pytest.raises(ValueError):
            im_codes.copy_folders([{"folder": "Revit All Zones/2024", "manifest": manifest, "reason": "x"}])
    nested = [{"folder": "Revit All Zones/2024", "manifest": "key_copies.json", "reason": "x"}, {"folder": "revit all zones/2024/STR", "manifest": "key_copies.json", "reason": "x"}]
    with pytest.raises(ValueError):
        im_codes.copy_folders(nested)
    import data5_check
    with pytest.raises(ValueError, match="inside"):
        data5_check.check_names(".", {"codes": {}}, nested)


def test_r301_a_copy_folder_that_is_not_under_the_source_root_is_refused_in_both_methods(tmp_path):
    import data5_check
    copies = copy_tree(tmp_path, folder="Revit All Zones/2025")
    with pytest.raises(ValueError, match="not under"):
        im_codes.file_names(tmp_path, im_codes.copy_folders(copies))
    with pytest.raises(ValueError, match="not under"):
        data5_check.check_names(tmp_path, {"codes": {}}, copies)


def test_r301_the_real_config_names_the_2024_copies_of_the_parsons_models_as_a_copy_folder_with_its_manifest_and_reason():
    settings = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["data5"]["imCodes"]
    folders = im_codes.copy_folders(settings["copyFolders"])
    assert [(folder.folder, folder.manifest) for folder in folders] == [("Revit All Zones/2024", "key_copies.json")]
    assert "Round ADDIN" in settings["copyFolders"][0]["reason"]


# Stale inputs after the clean up of Q047 and Q060
def test_r258_r260_the_retired_ifc_input_is_skipped_with_its_reason_and_nothing_is_read(tmp_path):
    import data5_check
    import ifc_header
    book = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]
    assert "Q047" in book["data5"]["ifc"]["retired"]
    config = {"dataBook": {"root": str(tmp_path), "data5": {"outFolder": "data5", "ifc": {"copy": "Downloads/none.ifc", "outFolder": "ifc", "retired": "Q047 deleted"}}}}
    assert ifc_header.run(config) == {"retired": "Q047 deleted"}
    assert not (tmp_path / "data5").exists()
    assert data5_check.ifc_part(config["dataBook"]["data5"], tmp_path, tmp_path / "data5") == {"skipped": "Q047 deleted"}


def test_r218_the_clean_up_lists_and_deletes_nothing_when_the_work_folder_is_gone(tmp_path, capsys):
    import round_files
    assert round_files.clean_list(tmp_path / "work", []) == []
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"siteBuild": {"folder": str(tmp_path)}}), encoding="utf-8")
    assert round_files.main(["--settings", str(settings), "clean", "--delete"]) == 0
    assert "no work folder" in capsys.readouterr().out


def test_r213_the_new_site_file_is_cleared_and_the_jobs_that_need_it_refuse_by_the_setting_name():
    assert project_profile.load_settings().get("siteBuild.newFile") is None
    script = (ROOT / "dotnet" / "SiteAdaptation.TestHost" / "Run-TestHost.ps1").read_text(encoding="utf-8")
    assert "siteBuild.newFile of config/project.json is empty" in script
    assert "Join-Path $work $build.newFile" not in script and script.count("Join-Path $work (Get-SiteNewFile $build)") == 2


# R302 the image step limit of the test host
def test_r302_the_image_step_limit_is_read_from_the_test_host_settings_and_is_30_minutes():
    settings = project_profile.load_settings()
    assert settings.get("testHost.imageStepMinutes") == 30
    script = (ROOT / "dotnet" / "SiteAdaptation.TestHost" / "Run-TestHost.ps1").read_text(encoding="utf-8")
    assert "$config.testHost.imageStepMinutes" in script and "$roundJob.imageMinutes" in script


def test_r302_a_plain_image_run_takes_its_window_limit_from_the_image_step_and_the_margin():
    settings = project_profile.load_settings()
    step, margin = settings.get("testHost.imageStepMinutes"), settings.get("testHost.imageMarginMinutes")
    assert margin > 0 and step + margin + 3 > 30
    script = (ROOT / "dotnet" / "SiteAdaptation.TestHost" / "Run-TestHost.ps1").read_text(encoding="utf-8")
    assert "$imageTotal = $imageMinutes + $imageMargin" in script
    assert "$TimeoutMinutes = [int][Math]::Ceiling($imageTotal + 3)" in script
    assert "$job.totalMinutes = [Math]::Min($imageTotal, $TimeoutMinutes - 2)" in script
