import json
from datetime import datetime, timedelta, timezone

import pytest

import window_lock as wl


class Clock:
    def __init__(self):
        self.at = datetime(2026, 10, 24, 12, 0, tzinfo=timezone.utc)

    def now(self):
        return self.at

    def sleep(self, seconds):
        self.at += timedelta(seconds=seconds)


def make(tmp_path, free=5.0, processes=None, clock=None):
    clock = clock or Clock()
    block = {"lock": str(tmp_path / "window.lock"), "waitMinutes": 5, "staleHours": 2, "minFreeGB": 2.0, "windowProcesses": ["acad", "Revit"]}
    state = {"free": free, "processes": list(processes or [])}
    lock = wl.Lock(block, "Z4", tmp_path / "logs" / "windows.jsonl", now=clock.now, sleep=clock.sleep,
                   free=lambda: state["free"], processes=lambda names: list(state["processes"]))
    return lock, state, clock


def events(lock):
    return [json.loads(line) for line in lock.log_path.read_text(encoding="utf-8").splitlines()]


def test_r200_a_clear_machine_gives_the_lock_with_the_round_and_time_and_logs_free_memory(tmp_path):
    lock, _, clock = make(tmp_path)

    seen = lock.acquire("upgrade", 60)

    held = json.loads(lock.path.read_text(encoding="utf-8"))
    assert held["round"] == "Z4" and held["utc"] == clock.at.isoformat(timespec="seconds")
    assert seen["freeGB"] == 5.0
    assert events(lock)[-1]["event"] == "lockTaken" and events(lock)[-1]["freeGB"] == 5.0


def test_r200_a_lock_of_the_other_round_makes_it_wait_five_minutes_and_log_each_wait(tmp_path):
    lock, _, clock = make(tmp_path)
    lock.path.write_text(json.dumps({"round": "DATA-3", "utc": clock.at.isoformat()}), encoding="utf-8")
    start = clock.at

    def free_after_two_waits(seconds, real=clock.sleep):
        real(seconds)
        if clock.at - start >= timedelta(minutes=10):
            lock.path.unlink()

    lock.sleep = free_after_two_waits
    lock.acquire("read", 60)

    waits = [event for event in events(lock) if event["event"] == "wait"]
    assert len(waits) == 2 and all(event["minutes"] == 5 for event in waits)
    assert "DATA-3" in waits[0]["why"]
    assert json.loads(lock.path.read_text(encoding="utf-8"))["round"] == "Z4"


@pytest.mark.parametrize("names", [[{"name": "acad.exe", "pid": 7}], [{"name": "Revit.exe", "pid": 8}]])
def test_r200_a_running_window_process_makes_it_wait_even_with_no_lock(tmp_path, names):
    lock, state, _ = make(tmp_path, processes=names)

    with pytest.raises(wl.LockError):
        lock.acquire("image", 12)

    assert not lock.path.exists()
    assert [event["event"] for event in events(lock)].count("wait") == 3


def test_r200_free_memory_under_the_floor_makes_it_wait(tmp_path):
    lock, state, _ = make(tmp_path, free=1.9)

    with pytest.raises(wl.LockError, match="under the floor"):
        lock.acquire("newSite", 6)

    assert not lock.path.exists()


def test_r200_a_lock_older_than_two_hours_with_no_window_process_is_logged_and_deleted(tmp_path):
    lock, _, clock = make(tmp_path)
    lock.path.write_text(json.dumps({"round": "DATA-3", "utc": (clock.at - timedelta(hours=2, minutes=1)).isoformat()}), encoding="utf-8")

    lock.acquire("read", 60)

    kinds = [event["event"] for event in events(lock)]
    assert kinds[:2] == ["staleLockDeleted", "lockTaken"]
    assert json.loads(lock.path.read_text(encoding="utf-8"))["round"] == "Z4"


def test_r200_an_old_lock_is_kept_while_a_window_process_runs(tmp_path):
    lock, _, clock = make(tmp_path, processes=[{"name": "acad.exe", "pid": 7}])
    lock.path.write_text(json.dumps({"round": "DATA-3", "utc": (clock.at - timedelta(hours=5)).isoformat()}), encoding="utf-8")

    with pytest.raises(wl.LockError):
        lock.acquire("read", 4)

    assert lock.path.exists()
    assert "staleLockDeleted" not in [event["event"] for event in events(lock)]


def test_r200_the_lock_goes_after_the_window_also_when_the_command_fails(tmp_path, monkeypatch):
    lock, _, _ = make(tmp_path)

    def boom(command):
        assert lock.path.exists()
        raise RuntimeError("window crashed")

    monkeypatch.setattr(wl.subprocess, "call", boom)
    with pytest.raises(RuntimeError):
        wl.run(lock, "upgrade", ["x"], 10)

    assert not lock.path.exists()
    assert [event["event"] for event in events(lock)][-2:] == ["windowClosed", "lockDeleted"]


def test_r200_a_lock_that_is_not_ours_is_never_deleted_on_release(tmp_path):
    lock, _, clock = make(tmp_path)
    lock.acquire("read", 10)
    lock.path.write_text(json.dumps({"round": "DATA-3", "utc": clock.at.isoformat(), "token": "other"}), encoding="utf-8")

    lock.release("read")

    assert lock.path.exists()
    assert events(lock)[-1]["event"] == "lockNotOurs"
