import json
from datetime import datetime, timezone

import c3d_window
from test_data_book import Fake, book_config


# R197 the Civil 3D runner under the shared window lock of R200
def test_r197_runner_opens_civil_3d_only_under_the_lock_and_passes_own_only(tmp_path):
    config = book_config(tmp_path)
    (tmp_path / "book" / "work" / "Base Plans" / "cut.dwg").write_bytes(b"dwg")
    config["dataBook"]["windowLock"] = {"lock": str(tmp_path / "window.lock"), "round": "DATA-3", "windowProcesses": ["acad", "Revit"],
                                        "waitMinutes": 5, "staleHours": 2, "minFreeGB": 2, "maxWaitMinutes": 60, "log": "logs/window_lock.jsonl"}
    config["dataBook"]["watcherOwnOnly"] = True
    seen = []

    def launch(args, environment):
        seen.append((args, json.loads((tmp_path / "window.lock").read_text(encoding="utf-8"))["round"]))
        return Fake(300 + len(seen), polls=[0])

    record = c3d_window.run(config, "cutsheet", launcher=launch, memory=lambda: 4.0, sleep=lambda s: None, processes=lambda names: [])
    assert [held for _, held in seen] == ["DATA-3", "DATA-3"] and record["lockSeen"]["freeGB"] == 4.0
    assert seen[1][0][-2:] == ["--own-only", "true"]
    assert not (tmp_path / "window.lock").exists()
    events = [json.loads(line)["event"] for line in (tmp_path / "book" / "logs" / "window_lock.jsonl").read_text(encoding="utf-8").splitlines()]
    assert events == ["lockTaken", "lockDeleted"]


def test_r197_runner_gives_the_lock_back_when_the_run_fails(tmp_path):
    config = book_config(tmp_path)
    (tmp_path / "book" / "work" / "Base Plans" / "cut.dwg").write_bytes(b"dwg")
    config["dataBook"]["windowLock"] = {"lock": str(tmp_path / "window.lock"), "round": "DATA-3", "windowProcesses": ["acad"],
                                        "waitMinutes": 5, "staleHours": 2, "minFreeGB": 2, "maxWaitMinutes": 60, "log": "logs/window_lock.jsonl"}

    def launch(args, environment):
        raise OSError("acad.exe missing")

    try:
        c3d_window.run(config, "cutsheet", launcher=launch, memory=lambda: 4.0, sleep=lambda s: None, processes=lambda names: [])
    except OSError:
        pass
    assert not (tmp_path / "window.lock").exists()


def test_r197_runner_waits_while_another_round_holds_the_lock(tmp_path):
    config = book_config(tmp_path)
    (tmp_path / "book" / "work" / "Base Plans" / "cut.dwg").write_bytes(b"dwg")
    lock = tmp_path / "window.lock"
    lock.write_text(json.dumps({"round": "Z4", "utc": datetime.now(timezone.utc).isoformat(timespec="seconds")}), encoding="utf-8")
    config["dataBook"]["windowLock"] = {"lock": str(lock), "round": "DATA-3", "windowProcesses": ["acad"], "waitMinutes": 5, "staleHours": 2,
                                        "minFreeGB": 2, "maxWaitMinutes": 60, "log": "logs/window_lock.jsonl"}
    waits = []

    def sleep(seconds):
        waits.append(seconds)
        lock.unlink()

    record = c3d_window.run(config, "cutsheet", launcher=lambda a, e: Fake(1, polls=[0]), memory=lambda: 4.0, sleep=sleep, processes=lambda names: [])
    assert waits == [300] and "lockSeen" in record and not lock.exists()
