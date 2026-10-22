import json
import sys

from openpyxl import Workbook

import full_run
from minimal_pdf import build_pdf


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


def test_r23_plan_reads_the_three_dwg_folders_and_skips_output_bak_and_lock_files(tmp_path):
    real_tree(tmp_path)
    jobs, skipped = full_run.plan(tmp_path)
    assert [(kind, path.name) for kind, path in jobs] == [
        ("dwg", "a.dwg"), ("dwg", "b.DWG"), ("dwg", "m.dwg"), ("dwg", "r.dwg"),
        ("pdf", "s1.pdf"), ("pdf", "s2.pdf"), ("xlsx", "w.xlsx"),
    ]
    reasons = {entry["file"]: entry for entry in skipped}
    assert "c.bak" in reasons and "m.bak" in reasons and "plan.xlsx" in reasons
    assert reasons["~$w.xlsx"]["why"] == "Excel lock file"
    assert reasons["DWG files under 01_Output"]["count"] == 2


def test_r23_a_reader_that_runs_too_long_is_stopped_and_recorded(tmp_path):
    record = full_run.run_one([sys.executable, "-c", "import time\ntime.sleep(30)"], tmp_path / "out", timeout=1)
    assert record["status"] == "timeout"
    assert record["seconds"] < 20
    assert json.loads((tmp_path / "out" / "run.json").read_text(encoding="utf-8"))["status"] == "timeout"


def test_r23_a_failing_reader_keeps_its_exact_error_text_and_exit_code(tmp_path):
    record = full_run.run_one([sys.executable, "-c", "import sys\nsys.stderr.write('boom 42')\nsys.exit(3)"], tmp_path / "out")
    assert record["status"] == "failed"
    assert record["exit"] == 3
    assert record["stderr"] == "boom 42"


def test_r23_a_reader_that_cannot_start_is_recorded_not_raised(tmp_path):
    record = full_run.run_one(["no-such-reader-xyz", "file"], tmp_path / "out")
    assert record["status"] == "failed"
    assert "FileNotFoundError" in record["stderr"]


def test_r23_old_output_of_a_file_is_cleared_before_its_run(tmp_path):
    out = tmp_path / "out"
    touch(out / "stale.json")
    full_run.run_one([sys.executable, "-c", "pass"], out)
    assert not (out / "stale.json").exists()
    assert (out / "run.json").exists()


def test_r23_run_carries_on_after_a_failed_file_and_writes_one_folder_per_file(tmp_path):
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
