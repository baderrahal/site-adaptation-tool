"""Q075 (Bader, 2026-10-29). The entries of one source with no value move to the cannot list of the override list, every other entry kept
as it was, held against the list before."""

import json

import majority
import override_source

ENTRIES = [
    {"plot": "148", "field": "entryM", "value": None, "rule": None, "source": "native", "model": "m3.rvt", "key": "u13", "decision": "native entry",
     "reason": "no value of the subgrade"},
    {"plot": "20", "field": "rampFrontFallM", "value": None, "rule": None, "source": "native", "model": "m1.rvt", "key": "u11",
     "decision": "native rampFrontFall", "reason": "no value of roadAtEachCorner"},
    {"plot": "21", "field": "entryM", "value": 0.012, "rule": 0.01, "source": "native", "model": "m1.rvt", "key": "u11", "decision": "native entry",
     "reason": "past"},
    {"plot": "22", "field": "entryM", "value": None, "rule": None, "source": "native", "model": "m1.rvt", "key": "u11", "decision": "native entry",
     "reason": "kept by Bader", "byHand": True},
    {"plot": "5", "field": "heightM", "value": 0.2, "rule": 0.15, "source": "parsons", "model": "p.rvt", "key": "ima", "decision": "parsons height",
     "reason": "past"},
]
CANNOT = [{"plot": "100", "field": "entryM", "source": "parsons", "model": "p.rvt", "key": "img", "decision": "parsons entry", "reason": "no value of roadLevel"}]


def made(tmp_path):
    path = tmp_path / "overrides.json"
    majority.write_overrides(path, "note", ENTRIES, CANNOT)
    return path


def test_q075_the_entries_of_the_source_with_no_value_move_to_cannot_and_every_other_entry_stays_as_it_was(tmp_path, capsys):
    path = made(tmp_path)
    assert override_source.main(["cannot", str(path), "--source", "native"]) == 0
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["overrides"] == [item for item in ENTRIES if item["plot"] in ("21", "22", "5")]
    # A cannot entry holds no value and no rule, sorted by plot, key and field as majority.py writes the list
    assert [item["plot"] for item in data["cannot"]] == ["20", "100", "148"]
    assert data["cannot"][0] == {"plot": "20", "field": "rampFrontFallM", "source": "native", "model": "m1.rvt", "key": "u11",
                                 "decision": "native rampFrontFall", "reason": "no value of roadAtEachCorner (Q075)"}
    assert data["cannot"][1] == CANNOT[0]
    report = json.loads(capsys.readouterr().out)
    assert report["moved"] == {"total": 2, "bySourceField": {"native entryM": 1, "native rampFrontFallM": 1}}
    assert report["byHandLeft"] == 1
    assert report["before"]["overrides"]["total"] == 5 and report["after"]["overrides"]["total"] == 3
    assert report["before"]["cannot"] == 1 and report["after"]["cannot"] == 3


def test_q075_a_second_run_moves_nothing_and_leaves_the_file_byte_for_byte(tmp_path, capsys):
    path = made(tmp_path)
    override_source.main(["cannot", str(path), "--source", "native"])
    first = path.read_bytes()
    capsys.readouterr()
    assert override_source.main(["cannot", str(path), "--source", "native"]) == 0
    assert path.read_bytes() == first
    assert json.loads(capsys.readouterr().out)["moved"]["total"] == 0


def test_q075_cannot_needs_a_source(tmp_path, capsys):
    path = made(tmp_path)
    assert override_source.main(["cannot", str(path)]) == 2
    assert "cannot needs --source" in capsys.readouterr().err
