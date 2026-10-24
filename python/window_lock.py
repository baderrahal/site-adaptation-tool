"""R200. One CAD or Revit window at a time on this machine, shared with the other session by one lock file.

Before a window opens, the lock named by siteBuild.lock in config/project.json must be absent, no process of
siteBuild.windowProcesses may run and free memory must be at least siteBuild.minFreeGB. While one of them is not so, the
round waits siteBuild.waitMinutes and looks again, and every wait is logged. A lock older than siteBuild.staleHours with no
window process is logged and deleted. The lock holds the round name and the time, it is made only when nothing holds it, and
it is deleted as soon as the window command ends, also when that command fails.
"""
import argparse
import csv
import ctypes
import io
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

import project_profile


class LockError(Exception):
    """The lock could not be taken or given back. No window was opened."""


def settings_block(settings):
    block = settings.get("siteBuild")
    if not block:
        raise project_profile.ProfileError(settings.path.name + " has no siteBuild block")
    return block


def free_gb():
    """Free physical memory in GB, as GlobalMemoryStatusEx gives it."""

    class Status(ctypes.Structure):
        _fields_ = [("length", ctypes.c_ulong), ("load", ctypes.c_ulong), ("totalPhys", ctypes.c_ulonglong), ("availPhys", ctypes.c_ulonglong),
                    ("totalPage", ctypes.c_ulonglong), ("availPage", ctypes.c_ulonglong), ("totalVirtual", ctypes.c_ulonglong),
                    ("availVirtual", ctypes.c_ulonglong), ("availExtended", ctypes.c_ulonglong)]

    status = Status()
    status.length = ctypes.sizeof(Status)
    if not ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
        raise OSError("GlobalMemoryStatusEx failed")
    return status.availPhys / 1024 ** 3


def running(names):
    """The processes of the names given that run now, as name and process id, from tasklist."""
    text = subprocess.run(["tasklist", "/FO", "CSV", "/NH"], capture_output=True, text=True, check=True).stdout
    wanted = {name.lower() + ".exe" for name in names}
    return [{"name": row[0], "pid": int(row[1])} for row in csv.reader(io.StringIO(text)) if len(row) > 1 and row[0].lower() in wanted]


def now_utc():
    return datetime.now(timezone.utc)


class Lock:
    """The window lock of one round. now, sleep, free and processes are passed in so a test runs it with no window."""

    def __init__(self, block, round_name, log_path, now=now_utc, sleep=time.sleep, free=free_gb, processes=running):
        self.path = Path(block["lock"])
        self.round = round_name
        self.wait = float(block["waitMinutes"])
        self.stale = float(block["staleHours"])
        self.floor = float(block["minFreeGB"])
        self.names = list(block["windowProcesses"])
        self.log_path = Path(log_path)
        self.now, self.sleep, self.free, self.processes = now, sleep, free, processes
        self.token = None

    def log(self, event, **fields):
        self.log_path.parent.mkdir(parents=True, exist_ok=True)
        line = {"utc": self.now().isoformat(timespec="seconds"), "round": self.round, "event": event, **fields}
        with open(self.log_path, "a", encoding="utf-8") as handle:
            handle.write(json.dumps(line, ensure_ascii=False) + "\n")
        return line

    def holder(self):
        """The lock as written, or a stand-in with no time when it does not read."""
        try:
            return json.loads(self.path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {"round": None, "utc": None}

    def age_hours(self, holder):
        try:
            made = datetime.fromisoformat(holder["utc"])
        except (TypeError, ValueError, KeyError):
            made = datetime.fromtimestamp(self.path.stat().st_mtime, timezone.utc)
        return (self.now() - made).total_seconds() / 3600.0

    def look(self):
        """Why a window may not open now, or None, and what was seen."""
        seen = {"freeGB": round(self.free(), 2), "processes": self.processes(self.names), "lock": self.path.exists()}
        if seen["lock"]:
            holder = self.holder()
            seen["lockRound"], seen["lockAgeHours"] = holder.get("round"), round(self.age_hours(holder), 2)
            if seen["lockAgeHours"] > self.stale and not seen["processes"]:
                self.log("staleLockDeleted", **seen)
                self.path.unlink()
                return self.look()
            return "the lock is held by " + str(holder.get("round")), seen
        if seen["processes"]:
            return "a window process runs: " + ", ".join(sorted({item["name"] for item in seen["processes"]})), seen
        if seen["freeGB"] < self.floor:
            return "free memory %.2f GB is under the floor of %.2f GB" % (seen["freeGB"], self.floor), seen
        return None, seen

    def acquire(self, label, max_wait_minutes):
        """Waits until a window may open, then makes the lock. Raises LockError when the wait runs past max_wait_minutes."""
        start = self.now()
        while True:
            why, seen = self.look()
            if why is None:
                self.token = os.urandom(8).hex()
                try:
                    with open(self.path, "x", encoding="utf-8") as handle:
                        json.dump({"round": self.round, "utc": self.now().isoformat(timespec="seconds"), "label": label, "token": self.token, "pid": os.getpid()}, handle)
                except FileExistsError:
                    self.token = None
                    continue
                self.log("lockTaken", label=label, **seen)
                return seen
            if (self.now() - start) > timedelta(minutes=max_wait_minutes):
                self.log("gaveUp", label=label, why=why, **seen)
                raise LockError("waited more than %d minutes: %s" % (max_wait_minutes, why))
            self.log("wait", label=label, why=why, minutes=self.wait, **seen)
            self.sleep(self.wait * 60)

    def release(self, label):
        """Deletes the lock when it is this round's, logs it either way."""
        holder = self.holder()
        if self.path.exists() and holder.get("token") == self.token and self.token is not None:
            self.path.unlink()
            self.log("lockDeleted", label=label, freeGB=round(self.free(), 2))
        else:
            self.log("lockNotOurs", label=label, holder=holder.get("round"))
        self.token = None


def run(lock, label, command, max_wait_minutes):
    """R200. Takes the lock, runs the window command, gives the lock back however the command ends."""
    lock.acquire(label, max_wait_minutes)
    started = time.monotonic()
    code = None
    try:
        code = subprocess.call(command)
        return code
    finally:
        left = lock.processes(lock.names)
        lock.log("windowClosed", label=label, exitCode=code, minutes=round((time.monotonic() - started) / 60.0, 2), processesLeft=left)
        lock.release(label)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run one window command under the shared window lock (R200).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--round", required=True, help="round name written into the lock")
    parser.add_argument("--label", required=True, help="what the window is for, for the log")
    parser.add_argument("--max-wait-minutes", type=int, default=240)
    parser.add_argument("--status", action="store_true", help="only look and log, open nothing")
    parser.add_argument("command", nargs=argparse.REMAINDER, help="the window command after --")
    args = parser.parse_args(argv)
    block = settings_block(project_profile.load_settings(args.settings))
    lock = Lock(block, args.round, Path(block["folder"]) / "logs" / "windows.jsonl")
    if args.status:
        why, seen = lock.look()
        print(json.dumps({"why": why, **seen}))
        return 0
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("give the window command after --")
    try:
        return run(lock, args.label, command, args.max_wait_minutes)
    except LockError as error:
        print("window_lock: " + str(error), file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
