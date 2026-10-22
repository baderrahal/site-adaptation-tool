import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
DWG_FOLDERS = ("Base Plans", "Master Plan", "Roads")
PDF_FOLDER = Path("01_Output") / "1.5 Coordinates Schedules" / "PDF"
XLSX_FOLDER = Path("01_Output") / "1.11 References"
DEFAULT_SHEET = "MP_Phase 1_Zone21"
TIMEOUT = 600
TEXT_LIMIT = 20000


def plan(root, kinds=("dwg", "pdf", "xlsx")):
    """Files to read and files left alone, each with the reason."""
    root = Path(root)
    jobs, skipped = [], []
    if "dwg" in kinds:
        for folder in DWG_FOLDERS:
            for path in sorted((root / folder).glob("*")):
                if path.suffix.lower() == ".dwg":
                    jobs.append(("dwg", path))
                elif path.is_file():
                    skipped.append({"file": path.name, "why": "not a DWG (" + path.suffix.lower() + ")"})
        output = root / "01_Output"
        left = len([path for path in output.rglob("*") if path.suffix.lower() == ".dwg"]) if output.is_dir() else 0
        skipped.append({"file": "DWG files under 01_Output", "why": "repeat the base plan and road drawings", "count": left})
    if "pdf" in kinds:
        jobs.extend(("pdf", path) for path in sorted((root / PDF_FOLDER).glob("*.pdf")))
    if "xlsx" in kinds:
        for path in sorted((root / XLSX_FOLDER).glob("*.xlsx")):
            if path.name.startswith("~$"):
                skipped.append({"file": path.name, "why": "Excel lock file"})
            else:
                jobs.append(("xlsx", path))
    return jobs, skipped


def command(kind, path, out_dir, cad_exe, layers, sheet):
    if kind == "dwg":
        return [str(cad_exe), str(path), str(out_dir), "--layers", str(layers)]
    if kind == "pdf":
        return [sys.executable, str(HERE / "pdf_reader.py"), str(path), str(out_dir)]
    return [sys.executable, str(HERE / "excel_reader.py"), str(path), str(out_dir), "--sheet", sheet]


def run_one(args, out_dir, timeout=TIMEOUT):
    """Runs one reader with a time limit. A failure or a timeout is recorded with its exact text and never raised."""
    out_dir = Path(out_dir)
    if out_dir.exists():
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True)
    started = time.monotonic()
    record = {"command": args, "timeoutSeconds": timeout}
    try:
        done = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=timeout)
        record.update({"status": "ok" if done.returncode == 0 else "failed", "exit": done.returncode, "stdout": done.stdout[-TEXT_LIMIT:], "stderr": done.stderr[-TEXT_LIMIT:]})
    except subprocess.TimeoutExpired as late:
        record.update({"status": "timeout", "exit": None, "stdout": text_of(late.stdout), "stderr": text_of(late.stderr)})
    except OSError as problem:
        record.update({"status": "failed", "exit": None, "stdout": "", "stderr": type(problem).__name__ + ": " + str(problem)})
    record["seconds"] = round(time.monotonic() - started, 1)
    (out_dir / "run.json").write_text(json.dumps(record, indent=1, ensure_ascii=False), encoding="utf-8")
    return record


def text_of(value):
    if value is None:
        return ""
    return (value.decode("utf-8", "replace") if isinstance(value, bytes) else value)[-TEXT_LIMIT:]


def cad_notes(out_dir):
    """Reader notes of a drawing, which name the objects the reader could not open."""
    layers = Path(out_dir) / "layers.json"
    if not layers.is_file():
        return None
    notes = json.loads(layers.read_text(encoding="utf-8")).get("notes", [])
    return {"total": sum(note["count"] for note in notes), "notImplemented": sum(note["count"] for note in notes if note["text"].startswith("NotImplemented")), "groups": len(notes)}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run the readers in place on the real files, one time limit per file.")
    parser.add_argument("root", help="folder that holds Base Plans, Master Plan, Roads and 01_Output")
    parser.add_argument("out_dir", help="where each file gets its own folder")
    parser.add_argument("--cad-exe", default=str(HERE.parent / "dotnet" / "CadReader" / "bin" / "Release" / "net48" / "CadReader.exe"))
    parser.add_argument("--layers", default=str(HERE.parent / "config" / "cad_layers.json"))
    parser.add_argument("--sheet", default=DEFAULT_SHEET)
    parser.add_argument("--timeout", type=int, default=TIMEOUT)
    parser.add_argument("--kinds", default="dwg,pdf,xlsx")
    args = parser.parse_args(argv)
    jobs, skipped = plan(args.root, tuple(args.kinds.split(",")))
    out_root = Path(args.out_dir)
    results = []
    for kind, path in jobs:
        out_dir = out_root / path.name
        record = run_one(command(kind, path, out_dir, args.cad_exe, args.layers, args.sheet), out_dir, args.timeout)
        entry = {"file": path.name, "kind": kind, "status": record["status"], "exit": record["exit"], "seconds": record["seconds"]}
        if kind == "dwg":
            entry["notes"] = cad_notes(out_dir)
        if record["status"] != "ok":
            entry["error"] = (record["stderr"].strip() or record["stdout"].strip())[-500:]
        results.append(entry)
        print(path.name + ": " + record["status"] + " in " + str(record["seconds"]) + " s", flush=True)
    out_root.mkdir(parents=True, exist_ok=True)
    summary = {"files": len(results), "ok": sum(1 for entry in results if entry["status"] == "ok"), "results": results, "skipped": skipped}
    (out_root / "full_run_summary.json").write_text(json.dumps(summary, indent=1, ensure_ascii=False), encoding="utf-8")
    print("files " + str(summary["files"]) + ", ok " + str(summary["ok"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
