"""Runs the probe in the full Civil 3D window on one working copy of the data book (R160).

Bader decided for round DATA that the full window runs the probe the way the Revit test host runs: acad.exe starts with
the Civil 3D product switch on a copy and a script that NETLOADs the probe, runs C3DDATA and C3DQUIT, and the watcher of
R161 answers the dialogs. The runner refuses a drawing outside the data book work folder, hashes the copy before and after,
stops the run when free memory falls under minFreeGB or the time runs out, and only ever stops the processes it started.
SECURELOAD, TRUSTEDPATHS and the registry are never touched.
"""
import argparse
import ctypes
import json
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

import c3d_run

REPO = Path(__file__).resolve().parent.parent
COMMANDS = ("C3DDATA", "C3DQUIT")


class MemoryStatus(ctypes.Structure):
    _fields_ = [("dwLength", ctypes.c_ulong), ("dwMemoryLoad", ctypes.c_ulong), ("ullTotalPhys", ctypes.c_ulonglong), ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong), ("ullAvailPageFile", ctypes.c_ulonglong), ("ullTotalVirtual", ctypes.c_ulonglong),
                ("ullAvailVirtual", ctypes.c_ulonglong), ("ullAvailExtendedVirtual", ctypes.c_ulonglong)]


def free_gb():
    status = MemoryStatus()
    status.dwLength = ctypes.sizeof(MemoryStatus)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status))
    return status.ullAvailPhys / (1 << 30)


def work_root(book):
    return Path(book["root"]) / "work"


def refusal(config, drawing):
    """Why the drawing may not be opened, or None. Only a copy under the data book work folder (R160)."""
    book = config["dataBook"]
    if c3d_run.under(config["sourceRoot"], drawing):
        return "the drawing lies in the source folder, open the working copy"
    if not c3d_run.under(work_root(book), drawing):
        return "the drawing is not under " + str(work_root(book))
    if not Path(drawing).is_file():
        return "the working copy is missing"
    return None


def script_text(dll):
    return c3d_run.checked("\n".join(["NETLOAD", '"' + str(dll) + '"'] + list(COMMANDS)) + "\n")


def window_args(acad, drawing, script, language):
    return [str(acad), str(drawing), "/product", "C3D", "/language", language, "/nologo", "/b", str(script)]


def watch_args(watcher, pid, dll, out, minutes):
    return [str(watcher), "--pid", str(pid), "--probe", str(dll), "--out", str(out), "--minutes", str(minutes)]


def probe_dll(config):
    dll = Path(config["probeDll"])
    return dll if dll.is_absolute() else (REPO / dll).resolve()


def known_surfaces(book, role):
    """The surfaces the probe skips on export. Round DATA-2 exports every surface, so exportKnownSurfaces empties it (R169)."""
    if book.get("exportKnownSurfaces"):
        return []
    return book.get("knownSurfaces", {}).get(role, [])


def probe_done(summary, started):
    """True only for a data.json the probe wrote during this run. The stand-in tables of R164 leave their own data.json."""
    if not summary.is_file() or summary.stat().st_mtime < started:
        return False
    found = json.loads(summary.read_text(encoding="utf-8"))
    return found.get("reader") == "C3DProbe" and found.get("done") is True


def run(config, role, launcher=None, memory=free_gb, sleep=time.sleep, clock=time.time):
    book = config["dataBook"]
    root = Path(book["root"])
    drawing = work_root(book) / book["drawings"][role]
    reason = refusal(config, drawing)
    if reason:
        raise ValueError(reason)
    logs = root / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    dll = probe_dll(config)
    script = logs / f"{stamp}_{role}.scr"
    script.write_text(script_text(dll), encoding="ascii")
    out = root / drawing.stem
    env = dict(os.environ)
    env.update({"C3DPROBE_WORK": str(work_root(book)), "C3DPROBE_OUT": str(out), "C3DPROBE_BORDER": str(root / "phase1_border.csv"),
                "C3DPROBE_LANDXML": str(root / "landxml"), "C3DPROBE_KNOWN": "|".join(known_surfaces(book, role))})
    start_free = memory()
    record = {"role": role, "drawing": str(drawing), "freeGbAtStart": round(start_free, 2), "pids": []}
    if start_free < book["minFreeGB"]:
        record["skipped"] = "free memory under %s GB" % book["minFreeGB"]
        return record
    hash_before = c3d_run.sha256(drawing)
    folder_before = sorted(p.name for p in drawing.parent.iterdir())
    launch = launcher or (lambda args, environment: subprocess.Popen(args, env=environment, cwd=str(logs)))
    window = launch(window_args(book["acad"], drawing, script, config["language"]), env)
    record["pids"].append(window.pid)
    watch_log = logs / f"{stamp}_{role}.watch.json"
    watcher = launch(watch_args(REPO / book["watcher"], window.pid, dll, watch_log, book["timeoutMinutes"]), env)
    record["pids"].append(watcher.pid)
    started = clock()
    lowest = start_free
    while window.poll() is None:
        free = memory()
        lowest = min(lowest, free)
        if free < book["minFreeGB"]:
            record["stopped"] = "free memory %.2f GB under %s GB" % (free, book["minFreeGB"])
            c3d_run.stop_own(window)
            break
        if clock() - started > book["timeoutMinutes"] * 60:
            record["stopped"] = "timeout after %s minutes" % book["timeoutMinutes"]
            c3d_run.stop_own(window)
            break
        sleep(5)
    if watcher.poll() is None:
        try:
            watcher.wait(60)
        except subprocess.TimeoutExpired:
            c3d_run.stop_own(watcher)
    record["exit"] = window.returncode
    record["seconds"] = round(clock() - started, 1)
    record["freeGbLowest"] = round(lowest, 2)
    record["unchanged"] = c3d_run.sha256(drawing) == hash_before
    record["newFiles"] = sorted(set(p.name for p in drawing.parent.iterdir()) - set(folder_before))
    record["done"] = probe_done(out / "data.json", started)
    if (out / "data.error.txt").is_file():
        record["error"] = (out / "data.error.txt").read_text(encoding="utf-8").splitlines()[0]
    if watch_log.is_file():
        watched = json.loads(watch_log.read_text(encoding="utf-8"))
        record["dialogs"] = [{k: d.get(k) for k in ("title", "action", "clicked", "why")} for d in watched.get("dialogs", [])]
    (logs / f"{stamp}_{role}.run.json").write_text(json.dumps(record, indent=1), encoding="utf-8")
    return record


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run the data book probe in the full Civil 3D window, one drawing at a time (R160)")
    parser.add_argument("roles", nargs="*", help="drawing roles of dataBook.drawings, all when left out")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    args = parser.parse_args(argv)
    config = c3d_run.load_config(args.config)
    failed = 0
    for role in args.roles or list(config["dataBook"]["drawings"]):
        record = run(config, role)
        print(json.dumps({k: record.get(k) for k in ("role", "pids", "exit", "seconds", "unchanged", "done", "stopped", "skipped", "error", "freeGbLowest", "dialogs")}))
        failed += not record.get("done")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
