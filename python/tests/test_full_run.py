import json
import sys

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


def test_r29_the_two_skipped_master_plan_drawings_are_listed_with_a_reason_and_not_read(tmp_path):
    for name in ("221229_MP-SK-01-03-001_R06.dwg", "221229_MP-SK-01-03-001_R06_0000.dwg", "221229_MP-SK-01-03-001_R06_0001.dwg"):
        touch(tmp_path / "Master Plan" / name)
    jobs, skipped = full_run.plan(tmp_path, ("dwg",))
    assert [path.name for _, path in jobs] == ["221229_MP-SK-01-03-001_R06_0000.dwg"]
    reasons = {entry["file"]: entry["why"] for entry in skipped}
    assert "xref" in reasons["221229_MP-SK-01-03-001_R06.dwg"]
    assert "Identical copy" in reasons["221229_MP-SK-01-03-001_R06_0001.dwg"]
    assert "221229_MP-SK-01-03-001_R06_0000.dwg" not in reasons


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
    path.write_text(json.dumps({"profile": profile, "drawings": {"baseplan": "base.dwg", "secondDefault": "second.dwg", "skip": [{"file": "Skip.dwg", "why": "Decision X. Test reason."}]}}), encoding="utf-8")
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
