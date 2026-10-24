import json
import os

import pytest

import scenario_files as sf

STEM = "01101-PSN-AAA-ZZZ-PIM-TU-000001"


@pytest.fixture()
def folder(tmp_path):
    arc = tmp_path / "ARC"
    arc.mkdir()
    (arc / (STEM + ".rvt")).write_bytes(b"site model")
    (arc / (STEM + "_backup")).mkdir()
    (arc / (STEM + "_backup") / "x.dat").write_bytes(b"backup of the site model")
    (arc / "Prototypes").mkdir()
    (arc / "Prototypes" / (STEM + "_TEST-C10.rvt")).write_bytes(b"not next to the model")
    (arc / "Prototypes_2024").mkdir()
    (arc / "Other.rvt").write_bytes(b"another model")
    return arc


def original(folder):
    return folder / (STEM + ".rvt")


def names(folder):
    return sorted(path.name for path in folder.iterdir())


def test_r118_clean_old_deletes_the_15a_copies_their_runs_and_backups_and_nothing_else(folder):
    old = ["_TEST-C10.rvt", "_TEST-C10.0002.rvt", "_TEST-DT3_run1.rvt", "_TEST-DT3.0003.rvt", "_TEST-VL6.rvt"]
    for tail in old:
        (folder / (STEM + tail)).write_bytes(b"12345")
    (folder / (STEM + "_TEST-DT3_backup")).mkdir()
    (folder / (STEM + "_TEST-DT3_backup") / "a.dat").write_bytes(b"123")
    keep = [name for name in names(folder) if "_TEST-" not in name]
    sf.make_master(original(folder))
    sf.make_work(original(folder), "S1")
    deleted = sf.clean_old(original(folder))
    assert sorted(item["name"] for item in deleted) == sorted([STEM + tail for tail in old] + [STEM + "_TEST-DT3_backup"])
    assert {item["bytes"] for item in deleted} == {5, 3}
    left = names(folder)
    assert STEM + "_TEST-MASTER.rvt" in left and STEM + "_TEST-WORK-S1.rvt" in left
    assert [name for name in left if any(code in name for code in ("_TEST-C10", "_TEST-DT3", "_TEST-VL6"))] == []
    # The site model, its own backup folder, the prototype folders and other models stay
    assert set(keep) <= set(left)
    assert (folder / "Prototypes" / (STEM + "_TEST-C10.rvt")).exists()


def test_r118_the_master_is_a_file_copy_that_is_never_overwritten(folder):
    made = sf.make_master(original(folder))
    assert made == {"name": STEM + "_TEST-MASTER.rvt", "bytes": 10, "from": STEM + ".rvt"}
    assert (folder / made["name"]).read_bytes() == b"site model"
    with pytest.raises(sf.FileRuleError, match="never overwritten"):
        sf.make_master(original(folder))


def test_r118_a_work_file_copies_the_master_or_another_work_file_and_at_most_three_exist(folder):
    sf.make_master(original(folder))
    sf.make_work(original(folder), "S1")
    (folder / (STEM + "_TEST-WORK-S1.rvt")).write_bytes(b"result of S1")
    sf.make_work(original(folder), "S4", source="S1")
    assert (folder / (STEM + "_TEST-WORK-S4.rvt")).read_bytes() == b"result of S1"
    sf.make_work(original(folder), "S3")
    with pytest.raises(sf.FileRuleError, match="at most 3"):
        sf.make_work(original(folder), "S5")
    sf.remove_work(original(folder), "S3")
    with pytest.raises(sf.FileRuleError, match="never overwritten"):
        sf.make_work(original(folder), "S1")
    with pytest.raises(sf.FileRuleError, match="S1 to S999"):
        sf.make_work(original(folder), "X1")
    with pytest.raises(sf.FileRuleError, match="not there"):
        sf.make_work(original(folder), "S6", source="S9")


def test_r118_remove_deletes_the_work_file_and_its_backups_and_never_the_master_or_the_site_model(folder):
    sf.make_master(original(folder))
    sf.make_work(original(folder), "S1")
    sf.make_work(original(folder), "S11")
    for tail in ("_TEST-WORK-S1.0001.rvt", "_TEST-WORK-S11.0001.rvt", "_TEST-MASTER.0001.rvt"):
        (folder / (STEM + tail)).write_bytes(b"b")
    (folder / (STEM + "_TEST-WORK-S1_backup")).mkdir()
    deleted = sf.remove_work(original(folder), "S1")
    assert sorted(item["name"] for item in deleted) == sorted(STEM + tail for tail in ("_TEST-WORK-S1.rvt", "_TEST-WORK-S1.0001.rvt", "_TEST-WORK-S1_backup"))
    left = names(folder)
    assert STEM + "_TEST-WORK-S11.rvt" in left and STEM + "_TEST-WORK-S11.0001.rvt" in left
    assert STEM + "_TEST-MASTER.rvt" in left and STEM + ".rvt" in left


def test_r118_the_one_delete_refuses_the_site_model_the_master_other_names_other_folders_and_links(folder):
    sf.make_master(original(folder))
    model = original(folder)
    cases = (
        (model, ("old", "work")),
        (sf.master_of(model), ("old", "work", "workBackup")),
        (folder / "Other.rvt", ("old",)),
        (folder / "Prototypes" / (STEM + "_TEST-C10.rvt"), ("old",)),
        (folder / (STEM + "_backup"), ("oldBackupFolder", "workBackup")),
    )
    for path, kinds in cases:
        with pytest.raises(sf.FileRuleError):
            sf.delete_listed(model, path, kinds)
        assert path.exists()
    target = folder / (STEM + "_TEST-C10.rvt")
    try:
        os.symlink(model, target)
    except OSError:
        pytest.skip("symbolic links need rights this session does not have")
    with pytest.raises(sf.FileRuleError, match="link or junction"):
        sf.delete_listed(model, target, ("old",))
    assert model.read_bytes() == b"site model"


def test_r118_a_name_that_fits_but_is_a_link_or_junction_is_left_alone(folder, monkeypatch):
    target = folder / (STEM + "_TEST-C10.rvt")
    target.write_bytes(b"x")
    monkeypatch.setattr(sf, "is_link", lambda path: True)
    with pytest.raises(sf.FileRuleError, match="link or junction"):
        sf.delete_listed(original(folder), target, ("old",))
    assert target.exists()


def test_r118_the_master_name_is_not_an_old_copy_and_work_names_need_a_scenario_number(folder):
    found = sf.patterns(original(folder))
    assert not found["old"].match(STEM + "_TEST-MASTER.rvt")
    assert not found["old"].match(STEM + "_TEST-WORK-S1.rvt")
    assert not found["old"].match(STEM + "_TEST-WORK.rvt")
    assert found["old"].match(STEM + "_TEST-C10.rvt")
    assert found["work"].match(STEM + "_TEST-WORK-S12.rvt")
    assert not found["work"].match(STEM + "_TEST-WORK-S0.rvt")
    assert not found["work"].match(STEM + "_TEST-WORK-S1.rvt.bak")


def small_plan():
    def floors(plot):
        loop = {"points": [[0, 0], [10, 0], [10, 10], [0, 10]], "arcs": []}
        return {"generic": {"type": {"value": "Slab G"}, "outline": {"value": {"loops": [loop]}}}, "ramp": {"type": {"value": "Ramp B"}}}
    return {"floorSettings": {"types": {"generic": ["Slab G"], "yard": ["Yard Y"], "ramp": ["Ramp B"]}},
            "plots": [{"plot": str(number), "floors": floors(str(number))} for number in (101, 102, 103, 104, 105)]}


def test_r118_the_s5_plan_names_a_listed_ramp_type_the_model_lacks_on_the_middle_plot_only():
    plan = small_plan()
    changed, plot = sf.plan_with_missing_type(plan)
    assert plot == "103"
    assert changed["floorSettings"]["types"]["ramp"] == ["Ramp B", sf.MISSING_TYPE]
    assert [entry["floors"]["ramp"]["type"]["value"] for entry in changed["plots"]] == ["Ramp B", "Ramp B", sf.MISSING_TYPE, "Ramp B", "Ramp B"]
    assert plan["floorSettings"]["types"]["ramp"] == ["Ramp B"]


def test_r118_the_s6_plan_crosses_the_loop_of_the_middle_plot_and_leaves_the_rest():
    changed, plot = sf.plan_with_crossing_loop(small_plan())
    assert plot == "103"
    loops = [entry["floors"]["generic"]["outline"]["value"]["loops"][0]["points"] for entry in changed["plots"]]
    assert loops[2] == [[0, 0], [10, 10], [10, 0], [0, 10]]
    assert all(loop == [[0, 0], [10, 0], [10, 10], [0, 10]] for index, loop in enumerate(loops) if index != 2)


def test_r118_the_command_line_refuses_with_code_3_and_reports_the_state(folder, tmp_path, capsys):
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"inputRoot": str(tmp_path), "testHost": {"model": os.path.join("ARC", STEM + ".rvt")}}), encoding="utf-8")
    assert sf.main(["master", "--settings", str(settings)]) == 0
    assert sf.main(["work", "S2", "--settings", str(settings)]) == 0
    assert sf.main(["remove", "S2", "--settings", str(settings)]) == 0
    assert sf.main(["remove", "MASTER", "--settings", str(settings)]) == 3
    capsys.readouterr()
    assert sf.main(["status", "--settings", str(settings)]) == 0
    assert json.loads(capsys.readouterr().out)["master"] == {"name": STEM + "_TEST-MASTER.rvt", "bytes": 10}
    assert sf.listing(original(folder))["work"] == []


def test_r118_a_refusal_comes_before_any_delete_so_nothing_goes(folder):
    sf.make_master(original(folder))
    sf.make_work(original(folder), "S1")
    # A file named like the work file but no backup of it stops the remove, and the work file stays
    (folder / (STEM + "_TEST-WORK-S1.pdf")).write_bytes(b"x")
    with pytest.raises(sf.FileRuleError):
        sf.remove_work(original(folder), "S1")
    assert (folder / (STEM + "_TEST-WORK-S1.rvt")).exists()
