import json
import sys
from pathlib import Path

from openpyxl import Workbook

import full_run
import project_profile
from minimal_pdf import build_pdf

SETTINGS = project_profile.load_settings()
BASEPLAN = SETTINGS.get("drawings.baseplan")


def touch(path, text="x"):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def real_tree(root):
    for name in ("Base Plans/a.dwg", "Base Plans/b.DWG", "Base Plans/c.bak", "Master Plan/m.dwg", "Master Plan/m.bak", "Master Plan/plan.xlsx", "Roads/r.dwg"):
        touch(root / name)
    touch(root / "01_Output/1.0 General/DWG/x.dwg")
    touch(root / "01_Output/1.1 Setting Out Plans/DWG/y.dwg")
    touch(root / "01_Output/1.5 Coordinates Schedules/PDF/s2.pdf")
    touch(root / "01_Output/1.5 Coordinates Schedules/PDF/s1.pdf")
    touch(root / "01_Output/1.11 References/w.xlsx")
    touch(root / "01_Output/1.11 References/~$w.xlsx")


def test_r25_plan_reads_the_three_dwg_folders_and_skips_output_bak_and_lock_files(tmp_path):
    real_tree(tmp_path)
    jobs, skipped = full_run.plan(tmp_path)
    assert [(kind, path.name) for kind, path in jobs] == [
        ("dwg", "a.dwg"), ("dwg", "b.DWG"), ("dwg", "m.dwg"), ("dwg", "r.dwg"),
        ("pdf", "s1.pdf"), ("pdf", "s2.pdf"), ("xlsx", "w.xlsx"), ("survey", "plan.xlsx"),
    ]
    reasons = {entry["file"]: entry for entry in skipped}
    assert "c.bak" in reasons and "m.bak" in reasons and "plan.xlsx" not in reasons
    assert reasons["~$w.xlsx"]["why"] == "Excel lock file"
    assert reasons["DWG files under 01_Output"]["count"] == 2


def test_r25_a_reader_that_runs_too_long_is_stopped_and_recorded(tmp_path):
    record = full_run.run_one([sys.executable, "-c", "import time\ntime.sleep(30)"], tmp_path / "out", timeout=1)
    assert record["status"] == "timeout"
    assert record["seconds"] < 20
    assert json.loads((tmp_path / "out" / "run.json").read_text(encoding="utf-8"))["status"] == "timeout"


def test_r25_a_failing_reader_keeps_its_exact_error_text_and_exit_code(tmp_path):
    record = full_run.run_one([sys.executable, "-c", "import sys\nsys.stderr.write('boom 42')\nsys.exit(3)"], tmp_path / "out")
    assert record["status"] == "failed"
    assert record["exit"] == 3
    assert record["stderr"] == "boom 42"


def test_r25_a_reader_that_cannot_start_is_recorded_not_raised(tmp_path):
    record = full_run.run_one(["no-such-reader-xyz", "file"], tmp_path / "out")
    assert record["status"] == "failed"
    assert "FileNotFoundError" in record["stderr"]


def test_r25_old_output_of_a_file_is_cleared_before_its_run(tmp_path):
    out = tmp_path / "out"
    touch(out / "stale.json")
    full_run.run_one([sys.executable, "-c", "pass"], out)
    assert not (out / "stale.json").exists()
    assert (out / "run.json").exists()


def test_r25_run_carries_on_after_a_failed_file_and_writes_one_folder_per_file(tmp_path):
    root = tmp_path / "real"
    pdf = root / "01_Output/1.5 Coordinates Schedules/PDF/a.pdf"
    pdf.parent.mkdir(parents=True)
    pdf.write_bytes(build_pdf([(20, 150, "PLOT")], [], []))
    book = Workbook()
    book.active.title = full_run.DEFAULT_SHEET
    book.active.append(["Plot No.", "FFL"])
    book.active.append([1, 10.5])
    good = root / "01_Output/1.11 References/good.xlsx"
    good.parent.mkdir(parents=True)
    book.save(good)
    book.active.title = "Other"
    book.save(root / "01_Output/1.11 References/odd.xlsx")

    out = tmp_path / "out"
    assert full_run.main([str(root), str(out), "--kinds", "pdf,xlsx"]) == 0
    summary = json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))
    status = {entry["file"]: entry["status"] for entry in summary["results"]}
    assert status == {"a.pdf": "ok", "good.xlsx": "ok", "odd.xlsx": "failed"}
    assert summary["ok"] == 2
    failed = next(entry for entry in summary["results"] if entry["file"] == "odd.xlsx")
    assert "Sheet 'MP_Phase 1_Zone21' not found" in failed["error"]
    assert (out / "a.pdf" / "pdf_rows.json").exists()
    assert (out / "good.xlsx" / "coords.json").exists()


def test_r29_the_current_master_plan_and_the_villa_block_drawing_are_read_and_the_previous_one_and_the_copy_are_not(tmp_path):
    drawings = SETTINGS.get("drawings")
    names = [drawings["masterplan"], drawings["previousMasterplan"], drawings["villaBlocks"], drawings["skip"][0]["file"]]
    assert len(set(names)) == 4
    for name in names:
        touch(tmp_path / "Master Plan" / name)
    jobs, skipped = full_run.plan(tmp_path, ("dwg",))
    assert sorted(path.name for _, path in jobs) == sorted([drawings["masterplan"], drawings["villaBlocks"]])
    reasons = {entry["file"]: entry["why"] for entry in skipped if entry["file"] != "DWG files under 01_Output"}
    assert sorted(reasons) == sorted([drawings["previousMasterplan"], drawings["skip"][0]["file"]])
    assert reasons[drawings["previousMasterplan"]] == "Previous master plan, kept for reference and not read in the run"
    assert "Identical copy" in reasons[drawings["skip"][0]["file"]]


def test_r29_skip_reasons_reach_full_run_summary_json(tmp_path):
    root = tmp_path / "real"
    touch(root / "Master Plan/221229_MP-SK-01-03-001_R06_0001.dwg")
    out = tmp_path / "out"
    assert full_run.main([str(root), str(out), "--kinds", "dwg"]) == 0
    summary = json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))
    assert summary["files"] == 0
    skipped = {entry["file"]: entry["why"] for entry in summary["skipped"]}
    assert skipped["221229_MP-SK-01-03-001_R06_0001.dwg"].startswith("Decision C")


def test_r25_stages_run_only_when_their_inputs_exist_and_say_why_when_they_do_not(tmp_path):
    none = full_run.stage_commands(tmp_path, [], "second.dwg", "shifts.json")
    assert [stage["why"] for stage in none] == ["no baseplan output"] * 4
    touch(tmp_path / BASEPLAN / "plots.json")
    touch(tmp_path / "s.pdf" / "pdf_rows.json")
    touch(tmp_path / "w.xlsx" / "coords.json")
    touch(tmp_path / "a.dwg" / "layers.json")
    touch(tmp_path / "second.dwg" / "pieces.json")
    ran = ["s.pdf", "w.xlsx", "a.dwg", BASEPLAN, "second.dwg"]
    stages = {stage["name"]: stage for stage in full_run.stage_commands(tmp_path, ran, "second.dwg", "shifts.json")}
    link = stages["link"]["args"]
    assert "shifts.json" in link and str(tmp_path / "s.pdf" / "pdf_rows.json") in link
    assert str(tmp_path / "a.dwg" / "layers.json") in link
    assert str(tmp_path / BASEPLAN / "layers.json") not in link
    assert "args" in stages["second_source"] and "args" in stages["other_faces"] and "args" in stages["analysis"]
    no_second = {stage["name"]: stage for stage in full_run.stage_commands(tmp_path, ["s.pdf", "w.xlsx"], "missing.dwg", "shifts.json")}
    assert no_second["second_source"]["why"] == "no output for missing.dwg"


def test_r32_survey_job_gets_the_baseplan_plots_and_the_shift_config(tmp_path):
    args = full_run.command("survey", tmp_path / "m.xlsx", tmp_path / "out", "cad.exe", "layers.json", "sheet", tmp_path / "plots.json", "shifts.json")
    assert "--survey" in args and str(tmp_path / "plots.json") in args and "shifts.json" in args


def write_settings(path, profile):
    drawings = {"baseplan": "base.dwg", "masterplan": "second.dwg", "previousMasterplan": "old.dwg", "villaBlocks": "villas.dwg", "roadLayout": "roads.dwg",
                "xrefRoles": {"Roads_new.dwg": "roadLayout"}, "skip": [{"file": "Skip.dwg", "why": "Decision X. Test reason."}]}
    path.write_text(json.dumps({"profile": profile, "drawings": drawings}), encoding="utf-8")
    return project_profile.load_settings(path)


def test_r63_the_analysis_stage_gets_the_profile_path_from_the_project_config(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    touch(tmp_path / "out" / "base.dwg" / "plots.json")
    stages = {stage["name"]: stage for stage in full_run.stage_commands(tmp_path / "out", ["base.dwg"], "second.dwg", "shifts.json", settings)}
    args = stages["analysis"]["args"]
    assert args[args.index("--profile") + 1] == str(project_profile.ROOT / "rules" / "other-profile.json")
    assert args[args.index("--project") + 1] == str(tmp_path / "project.json")


def test_r63_the_baseplan_and_the_skip_list_come_from_the_project_config(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    touch(tmp_path / "real" / "Master Plan" / "Skip.dwg")
    touch(tmp_path / "real" / "Master Plan" / "Read.dwg")
    jobs, skipped = full_run.plan(tmp_path / "real", ("dwg",), settings)
    assert [path.name for _, path in jobs] == ["Read.dwg"]
    assert {entry["file"]: entry["why"] for entry in skipped if entry["file"] == "Skip.dwg"} == {"Skip.dwg": "Decision X. Test reason."}
    touch(tmp_path / "out" / "base.dwg" / "plots.json")
    assert "args" in {stage["name"]: stage for stage in full_run.stage_commands(tmp_path / "out", ["base.dwg"], "second.dwg", "shifts.json", settings)}["other_faces"]


def test_r63_main_reads_the_skip_list_and_the_drawing_names_from_the_project_option(tmp_path):
    write_settings(tmp_path / "project.json", "rules/other-profile.json")
    root = tmp_path / "real"
    touch(root / "Master Plan/Skip.dwg")
    touch(root / "Master Plan/221229_MP-SK-01-03-001_R06_0001.dwg", "x")
    out = tmp_path / "out"
    assert full_run.main([str(root), str(out), "--kinds", "dwg", "--project", str(tmp_path / "project.json"), "--cad-exe", "no-such-reader-xyz"]) == 0
    summary = json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))
    assert {entry["file"]: entry["why"] for entry in summary["skipped"] if entry["file"] == "Skip.dwg"} == {"Skip.dwg": "Decision X. Test reason."}
    assert [entry["file"] for entry in summary["results"]] == ["221229_MP-SK-01-03-001_R06_0001.dwg"]
    assert {stage["stage"]: stage["why"] for stage in summary["stages"] if stage["stage"] == "analysis"} == {"analysis": "no baseplan output"}
    assert summary["xrefs"] == [] and summary["issues"] == []


def test_r29_the_second_source_drawing_is_the_master_plan_role_of_the_project_config(tmp_path, monkeypatch):
    write_settings(tmp_path / "project.json", "rules/other-profile.json")
    seen = []
    monkeypatch.setattr(full_run, "stage_commands", lambda out_root, ran, second, shifts, settings=None: seen.append(second) or [])
    monkeypatch.setattr(full_run, "road_stages", lambda *args, **more: [])
    assert full_run.main([str(tmp_path / "real"), str(tmp_path / "out"), "--kinds", "dwg", "--project", str(tmp_path / "project.json")]) == 0
    assert seen == ["second.dwg"]


def test_r72_the_analysis_stage_gets_the_block_and_road_folders_when_the_reader_wrote_them(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    out = tmp_path / "out"
    touch(out / "base.dwg" / "plots.json")

    def analysis_args():
        return {stage["name"]: stage for stage in full_run.stage_commands(out, ["base.dwg"], "second.dwg", "shifts.json", settings)}["analysis"]["args"]

    plain = analysis_args()
    assert "--blocks" not in plain and "--roads" not in plain
    touch(out / "villas.dwg" / "blocks.json")
    touch(out / "roads.dwg" / "pieces.json")
    full = analysis_args()
    assert full[full.index("--blocks") + 1] == str(out / "villas.dwg")
    assert full[full.index("--roads") + 1] == str(out / "roads.dwg")


def xref_layers(out, drawing, *xrefs):
    touch(out / drawing / "layers.json", json.dumps({"xrefs": [{"name": name, "path": path, "found": found, "inserts": 1} for name, path, found in xrefs]}))


def test_r72_an_xref_is_found_next_to_its_drawing_by_name_in_the_input_folders_or_by_the_role_that_stands_in_for_it(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    root, out = tmp_path / "real", tmp_path / "out"
    touch(root / "Roads" / "roads.dwg")
    touch(root / "Base Plans" / "Shared.DWG")
    xref_layers(out, "plan.dwg", ("Near", "./Near.dwg", True), ("Shared", "./sub/shared.dwg", False), ("Roads_new", "./Roads_new.dwg", False))
    found, issues = full_run.resolve_xrefs(root, out, ["plan.dwg"], settings)
    assert [(entry["xref"], entry["status"], entry.get("file")) for entry in found] == [
        ("Near.dwg", "found next to the drawing", None), ("shared.dwg", "found by name", "Shared.DWG"), ("Roads_new.dwg", "found by role roadLayout", "roads.dwg")]
    assert issues == []


def test_r72_an_xref_that_is_nowhere_is_an_issue_and_the_drawing_is_still_listed(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    root, out = tmp_path / "real", tmp_path / "out"
    touch(root / "Master Plan" / "a.dwg")
    xref_layers(out, "a.dwg", ("Lost", "./Lost.dwg", False), ("NoPath", None, False))
    xref_layers(out, "b.dwg", ("Roads_new", "./Roads_new.dwg", False))
    found, issues = full_run.resolve_xrefs(root, out, ["a.dwg", "b.dwg", "never-read.dwg"], settings)
    assert [(entry["drawing"], entry["xref"], entry["status"]) for entry in found] == [("a.dwg", "Lost.dwg", "not found"), ("a.dwg", "NoPath.dwg", "not found"), ("b.dwg", "Roads_new.dwg", "not found")]
    assert [issue["xref"] for issue in issues] == ["Lost.dwg", "NoPath.dwg", "Roads_new.dwg"]
    assert issues[0]["text"] == "The xref Lost.dwg of a.dwg is not in the input folders and no role stands in for it"
    # The stand in drawing has to exist too, a role alone is not enough
    touch(root / "Roads" / "other.dwg")
    assert full_run.resolve_xrefs(root, out, ["b.dwg"], settings)[0][0]["status"] == "not found"


def test_r29_a_reason_in_the_skip_list_wins_over_the_previous_master_plan_reason(tmp_path):
    write_settings(tmp_path / "project.json", "rules/other-profile.json")
    values = json.loads((tmp_path / "project.json").read_text(encoding="utf-8"))
    values["drawings"]["skip"].append({"file": "old.dwg", "why": "Decision Y. Its own reason."})
    (tmp_path / "project.json").write_text(json.dumps(values), encoding="utf-8")
    settings = project_profile.load_settings(tmp_path / "project.json")
    touch(tmp_path / "real" / "Master Plan" / "old.dwg")
    jobs, skipped = full_run.plan(tmp_path / "real", ("dwg",), settings)
    assert jobs == []
    assert {entry["file"]: entry["why"] for entry in skipped if entry["file"] == "old.dwg"} == {"old.dwg": "Decision Y. Its own reason."}


def test_r72_an_xref_found_by_name_is_not_replaced_by_the_drawing_of_its_role(tmp_path):
    settings = write_settings(tmp_path / "project.json", "rules/other-profile.json")
    root, out = tmp_path / "real", tmp_path / "out"
    touch(root / "Roads" / "Roads_new.dwg")
    touch(root / "Roads" / "roads.dwg")
    xref_layers(out, "plan.dwg", ("Roads_new", "./Roads_new.dwg", False))
    found, issues = full_run.resolve_xrefs(root, out, ["plan.dwg"], settings)
    assert [(entry["status"], entry["file"]) for entry in found] == [("found by name", "Roads_new.dwg")] and issues == []


def fake_reader(failing):
    """Stands in for the CAD reader. It writes a layers.json with one xref that is nowhere, and fails for the names given."""

    def run_one(args, out_dir, timeout=600):
        out_dir = Path(out_dir)
        out_dir.mkdir(parents=True, exist_ok=True)
        (out_dir / "layers.json").write_text(json.dumps({"xrefs": [{"name": "Lost", "path": "./Lost.dwg", "found": False, "inserts": 1}]}), encoding="utf-8")
        bad = out_dir.name in failing
        return {"status": "failed" if bad else "ok", "exit": 3 if bad else 0, "seconds": 0.0, "stdout": "", "stderr": "boom" if bad else ""}

    return run_one


def test_r72_only_the_drawings_that_were_read_ok_are_looked_up_and_the_issues_reach_the_main_summary(tmp_path, monkeypatch):
    write_settings(tmp_path / "project.json", "rules/other-profile.json")
    root = tmp_path / "real"
    touch(root / "Master Plan" / "good.dwg")
    touch(root / "Master Plan" / "bad.dwg")
    monkeypatch.setattr(full_run, "run_one", fake_reader({"bad.dwg"}))
    monkeypatch.setattr(full_run, "stage_commands", lambda out_root, ran, second, shifts, settings=None: [])
    monkeypatch.setattr(full_run, "road_stages", lambda *args, **more: [])
    out = tmp_path / "out"
    assert full_run.main([str(root), str(out), "--kinds", "dwg", "--project", str(tmp_path / "project.json")]) == 0
    summary = json.loads((out / "full_run_summary.json").read_text(encoding="utf-8"))
    assert {entry["file"]: entry["status"] for entry in summary["results"]} == {"bad.dwg": "failed", "good.dwg": "ok"}
    assert [(item["drawing"], item["xref"]) for item in summary["xrefs"]] == [("good.dwg", "Lost.dwg")]
    assert [(item["drawing"], item["xref"]) for item in summary["issues"]] == [("good.dwg", "Lost.dwg")]
