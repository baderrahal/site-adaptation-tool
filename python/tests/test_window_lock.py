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


def slots(tmp_path, total=64.0, processes=None, free=5.0, block=None):
    clock = Clock()
    settings = {"lock": str(tmp_path / "window.lock"), "waitMinutes": 5, "staleHours": 2, "minFreeGB": 2.0, "windowProcesses": ["acad", "Revit"],
                "revitSlots": 2, "revitSlotsMinTotalGB": 32, "maxWindows": 3, **(block or {})}
    state = {"free": free, "processes": list(processes or [])}
    lock = wl.Lock(settings, "S", tmp_path / "logs" / "windows.jsonl", now=clock.now, sleep=clock.sleep,
                   free=lambda: state["free"], processes=lambda names: list(state["processes"]), total=lambda: total)
    return lock, state


REVIT = {"name": "Revit.exe", "pid": 1}


def test_r449_the_first_holder_takes_the_old_lock_file_and_slot_one(tmp_path):
    lock, _ = slots(tmp_path)
    seen = lock.acquire("a", 1)
    assert seen["slot"] == 1 and lock.slot == 1
    assert lock.path == tmp_path / "window.lock"


def test_r449_a_second_holder_takes_slot_two_beside_a_running_revit(tmp_path):
    first, state = slots(tmp_path)
    first.acquire("a", 1)
    state["processes"] = [REVIT]
    second, _ = slots(tmp_path, processes=[REVIT])
    assert second.acquire("b", 1)["slot"] == 2
    assert second.path == tmp_path / "window-2.lock"
    assert (tmp_path / "window.lock").exists() and (tmp_path / "window-2.lock").exists()


def test_r449_both_slots_held_makes_a_third_wait_and_give_up(tmp_path):
    slots(tmp_path)[0].acquire("a", 1)
    slots(tmp_path)[0].acquire("b", 1)
    third, _ = slots(tmp_path)
    assert third.look()[0].startswith("the lock is held by")
    with pytest.raises(wl.LockError):
        third.acquire("c", 1)


def test_r449_windows_and_held_slots_at_the_limit_make_it_wait(tmp_path):
    slots(tmp_path)[0].acquire("a", 1)
    lock, _ = slots(tmp_path, processes=[REVIT, {"name": "acad.exe", "pid": 2}])
    assert lock.look()[0].startswith("a window process runs")


def test_r449_under_the_memory_limit_the_machine_keeps_one_slot(tmp_path):
    lock, _ = slots(tmp_path, total=31.9)
    assert lock.slots == 1 and lock.max_windows == 1
    lock.acquire("a", 1)
    assert slots(tmp_path, total=31.9)[0].look()[0].startswith("the lock is held by")


def test_r449_the_memory_floor_holds_for_slot_two(tmp_path):
    slots(tmp_path)[0].acquire("a", 1)
    lock, _ = slots(tmp_path, free=1.5)
    assert "under the floor" in lock.look()[0]


def test_r449_a_block_with_no_slot_keys_keeps_one_slot(tmp_path):
    lock, _, _ = make(tmp_path)
    assert lock.slots == 1 and lock.max_windows == 1


def test_r449_release_deletes_only_its_own_slot(tmp_path):
    first, _ = slots(tmp_path)
    first.acquire("a", 1)
    second, _ = slots(tmp_path)
    second.acquire("b", 1)
    second.release("b")
    assert (tmp_path / "window.lock").exists() and not (tmp_path / "window-2.lock").exists()


def test_r449_a_stale_slot_two_is_deleted_with_no_window(tmp_path):
    clock = Clock()
    settings = {"lock": str(tmp_path / "window.lock"), "waitMinutes": 5, "staleHours": 2, "minFreeGB": 2.0, "windowProcesses": ["Revit"],
                "revitSlots": 2, "revitSlotsMinTotalGB": 32, "maxWindows": 3}
    (tmp_path / "window-2.lock").write_text(json.dumps({"round": "old", "utc": (clock.at - timedelta(hours=3)).isoformat()}), encoding="utf-8")
    lock = wl.Lock(settings, "S", tmp_path / "logs" / "windows.jsonl", now=clock.now, sleep=clock.sleep, free=lambda: 5.0, processes=lambda names: [], total=lambda: 64.0)
    assert lock.look()[0] is None
    assert not (tmp_path / "window-2.lock").exists()


def test_r449_the_command_is_told_its_slot(tmp_path, monkeypatch):
    lock, _ = slots(tmp_path)
    told = {}
    monkeypatch.delenv(wl.SLOT_VARIABLE, raising=False)
    monkeypatch.setattr(wl.subprocess, "call", lambda command: told.setdefault("slot", wl.os.environ[wl.SLOT_VARIABLE]) and 0)
    wl.run(lock, "a", ["x"], 1)
    assert told["slot"] == "1"
