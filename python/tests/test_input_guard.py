"""D19. The guard of a run that reads real inputs, held on a made up input root in the temporary folder."""

import json
import os

import pytest

import input_guard


def made(tmp_path):
    root = tmp_path / "input"
    root.mkdir()
    (root / "plots.csv").write_bytes(b"plot\n101\n")
    return root


def test_d19_a_file_read_under_an_input_root_is_recorded_before_and_after_and_one_outside_is_not(tmp_path):
    root = made(tmp_path)
    outside = tmp_path / "work.json"
    outside.write_text("{}", encoding="utf-8")
    with input_guard.Guard([root]) as guard:
        assert (root / "plots.csv").read_bytes() == b"plot\n101\n"
        outside.read_text(encoding="utf-8")
    [record] = guard.records
    assert record["name"] == "plots.csv" and record["unchanged"] is True
    assert record["before"] == record["after"]
    assert set(record["before"]) == {"size", "writeTimeUtc", "sha256"} and record["before"]["size"] == 9


def test_d19_a_write_time_changed_during_the_run_fails_it_at_the_end_naming_the_file_and_what_changed(tmp_path):
    root = made(tmp_path)
    path = root / "plots.csv"
    with pytest.raises(input_guard.InputChanged, match=r"plots\.csv.*write time"):
        with input_guard.Guard([root]):
            path.read_text(encoding="utf-8")
            os.utime(path, (1_600_000_000, 1_600_000_000))


def test_d19_an_open_for_writing_under_an_input_root_is_refused_before_it_writes(tmp_path):
    root = made(tmp_path)
    with pytest.raises(input_guard.InputChanged, match="request.json"):
        with input_guard.Guard([root]):
            (root / "request.json").write_text("{}", encoding="utf-8")
    assert not (root / "request.json").exists()


def test_d19_the_roots_are_the_two_of_d19_inputroot_of_config_and_the_input_variable(tmp_path, monkeypatch):
    config = tmp_path / "project.json"
    config.write_text(json.dumps({"inputRoot": str(tmp_path / "in")}), encoding="utf-8")
    monkeypatch.setenv("SITE_ADAPTATION_INPUT", str(tmp_path / "copy"))
    found = [str(item) for item in input_guard.roots(config)]
    assert found[:2] == [r"C:\02-Site Adaptation", r"C:\10- ROSHEN MECCA"]
    assert found[2:] == [str(tmp_path / "in"), str(tmp_path / "copy")]


def test_d19_the_record_file_lists_every_input_the_run_opened(tmp_path):
    root = made(tmp_path)
    out = tmp_path / "out" / "inputs_guard.json"
    with input_guard.Guard([root], record=out):
        (root / "plots.csv").read_bytes()
    assert [item["name"] for item in json.loads(out.read_text(encoding="utf-8"))["inputs"]] == ["plots.csv"]


def test_d19_a_change_of_the_content_alone_with_size_and_write_time_kept_fails_the_run(tmp_path):
    # Review 1 of 4h-1: a copy over a file keeps the write time of its source, so the content is held as well
    root = made(tmp_path)
    path = root / "plots.csv"
    with pytest.raises(input_guard.InputChanged, match=r"plots\.csv its SHA-256$"):
        with input_guard.Guard([root]):
            path.read_bytes()
            stamp = os.stat(path).st_mtime_ns
            input_guard.ACTIVE[-1].active = False
            path.write_bytes(b"plot\n102\n")
            input_guard.ACTIVE[-1].active = True
            os.utime(path, ns=(stamp, stamp))
