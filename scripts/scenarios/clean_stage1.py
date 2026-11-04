"""The build of samples/new-project for the clean folder test of scripts/checks/clean_folder.py. Usage: clean_stage1.py <folder>.

The folder is a copy of the sample, the only thing the test hands over. This file opens each file of it, so the watch of the test sees
them, then runs stage 1 on the folder through the worker command dataCheck, the way the add-in does. The worker is a child process the
watch cannot see, so its own list of the files it opened, answer.opened, is held against the same past job paths. It fails when the
worker stops, writes no plot, or opened a path of scripts/checks/past_paths.txt.
"""
import hashlib
import json
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "checks"))

import clean_folder  # noqa: E402

WORKER = ROOT / "dotnet" / "CadReader" / "bin" / "Release" / "net8.0" / "CadReader.exe"
CONFIG = ROOT / "config" / "project.json"


def request_for(folder, out):
    return {
        "formatVersion": 1, "runId": "clean", "document": {"path": str(out / "model.rvt"), "sessionId": "clean"}, "command": "dataCheck",
        "inputs": [{"role": "scanFolder", "path": str(folder)}, {"role": "config", "path": str(CONFIG)}],
        "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": 300.0,
    }


def main(argv=None):
    folder = Path((argv or sys.argv[1:])[0]).resolve()
    files = sorted(path for path in folder.rglob("*") if path.is_file())
    # Opened here so the watch of the test sees the sample read, and so a change by the worker shows
    before = {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in files}
    if not WORKER.is_file():
        print("the worker is not built: %s" % WORKER)
        return 2
    with tempfile.TemporaryDirectory() as temp:
        out = Path(temp) / "out"
        out.mkdir()
        request = out / "request_clean.json"
        request.write_text(json.dumps(request_for(folder, out)), encoding="utf-8")
        done = subprocess.run([str(WORKER), "--request", str(request)], capture_output=True, text=True, encoding="utf-8", errors="replace")
        answer_path = out / "answer_clean.json"
        answer = json.loads(answer_path.read_text(encoding="utf-8")) if answer_path.is_file() else {}
        if done.returncode != 0 or answer.get("status") != "done":
            print("the worker did not finish: exit %d, %s" % (done.returncode, answer.get("message", done.stderr.strip()[-300:])))
            return 1
        plots = json.loads((out / "plots.json").read_text(encoding="utf-8"))["plots"]
        opened = [row["path"] for row in answer.get("opened", [])]
    past = clean_folder.past_reads(opened, clean_folder.past_patterns())
    if past:
        print("the worker opened a past job path: %s" % past)
        return 1
    if not plots:
        print("the worker read no plot from the sample")
        return 1
    after = {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(folder.rglob("*")) if path.is_file()}
    if after != before:
        print("the sample changed during the run")
        return 1
    print("stage 1 read %d plots from the sample, %d files opened, no past job path" % (len(plots), len(opened)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
