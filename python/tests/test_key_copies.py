"""Round ADDIN Phase 1 part 2: the Revit 2024 copies of the answer keys checked or made by code from config/project.json (R312).

Every model here is a dummy compound file built in code with a BasicFileInfo stream (R10), so the format is read from the bytes as
on the real copies.
"""
import hashlib
import json
import os
import re
import stat
from pathlib import Path

import pytest

import key_copies
from cfb_fixture import build

ROOT = Path(__file__).resolve().parents[2]
MODEL_A = "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1.rvt"
MODEL_C = "01101-PSN-IMC-ZZZ-PIM-TU-000001-zone2.rvt"
ORIGINAL_C = "01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"


def rvt(path, year, mark):
    """A dummy model of that format year, mark makes its bytes its own."""
    text = "\x00Worksharing: Not enabled\r\nFormat: %d\r\nBuild: 20250918_1515(x64)\r\nUnique Document GUID: %s\r\n" % (year, mark)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(build([("BasicFileInfo", b"\x0e\x00\x00\x00" + text.encode("utf-16-le"))]))
    return path


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def block(**change):
    """An answerKeys block of two keys: IMA made from its model in the folder above the copies, IMC from an original elsewhere."""
    keys = [{"code": "IMA", "zone": "Zone 1", "kind": "SE", "source": "Revit All Zones\\" + MODEL_A, "copy": MODEL_A},
            {"code": "IMC", "zone": "Zone 2", "kind": "TU", "source": "00_Revit Models\\Zone 02\\ARC\\" + ORIGINAL_C, "copy": MODEL_C}]
    values = {"folder": "Revit All Zones\\2024", "manifest": "key_copies.json", "format": 2024, "jobPrefix": "K", "codePrefix": "IM",
              "zonePattern": "Zone [1-9][0-9]*", "kinds": ["TU", "SE"], "codeInName": "^[0-9]{5}-PSN-{code}-", "keys": keys}
    values.update(change)
    return values


def tree(tmp_path):
    """A source root with both sources in Revit 2021 and Revit 2024 and a copy of each in the copy folder, as the site upgrade makes it."""
    root = tmp_path / "root"
    rvt(root / "Revit All Zones" / MODEL_A, 2021, "source A")
    rvt(root / "Revit All Zones" / MODEL_C, 2024, "the floor master")
    rvt(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C, 2024, "source C")
    rvt(root / "Revit All Zones" / "2024" / MODEL_A, 2024, "copy A")
    rvt(root / "Revit All Zones" / "2024" / MODEL_C, 2024, "copy C")
    return root


def entry(root, code, copy, source, made="the siteUpgrade job of a dummy round", copy_hash=None, source_hash=None, year=2024):
    return {"code": code, "copy": copy, "copySha256": copy_hash or digest(root / "Revit All Zones" / "2024" / copy), "source": source,
            "sourceSha256": source_hash or digest(root / Path(source.replace("\\", "/"))), "format": year, "madeBy": made}


def write_manifest(root, entries):
    path = root / "Revit All Zones" / "2024" / "key_copies.json"
    path.write_text(json.dumps({"note": "dummy", "copies": entries}), encoding="utf-8")
    return path


def both_entries(root):
    return [entry(root, "IMA", MODEL_A, "Revit All Zones\\" + MODEL_A), entry(root, "IMC", MODEL_C, "00_Revit Models\\Zone 02\\ARC\\" + ORIGINAL_C)]


def verdicts(root, tmp_path, values=None):
    return {row["code"]: row for row in key_copies.check(root, key_copies.load_keys(values or block()), tmp_path / "work")}


def state(root):
    """Bytes, size and time of every file under root, to see that nothing was written."""
    return {str(path.relative_to(root)): (digest(path), path.stat().st_size, path.stat().st_mtime_ns) for path in sorted(root.rglob("*")) if path.is_file()}


def test_r312_a_copy_whose_manifest_names_the_source_of_the_config_with_its_hash_of_today_is_reused(tmp_path):
    root = tree(tmp_path)
    write_manifest(root, both_entries(root))

    found = verdicts(root, tmp_path)

    assert {code: row["verdict"] for code, row in found.items()} == {"IMA": "reuse", "IMC": "reuse"}
    assert all(row["reasons"] == [] and row["request"] is None for row in found.values())
    assert found["IMC"]["format"] == 2024 and found["IMC"]["sourceSha256"] == digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C)


def test_r312_a_source_changed_since_the_copy_was_made_is_refused_and_asks_for_a_new_site_upgrade_job(tmp_path):
    root = tree(tmp_path)
    write_manifest(root, both_entries(root))
    rvt(root / "Revit All Zones" / MODEL_A, 2021, "source A saved again")
    before = state(root)

    found = verdicts(root, tmp_path)

    assert found["IMA"]["verdict"] == "remake" and found["IMA"]["reasons"] == ["sourceChanged"]
    assert found["IMC"]["verdict"] == "reuse"
    request = found["IMA"]["request"]
    assert request["spec"] == {"kind": "siteUpgrade", "code": "KA", "source": str(tmp_path / "work" / MODEL_A)}
    assert request["copyFrom"] == str(root / "Revit All Zones" / MODEL_A)
    # The copy in place is never overwritten: the new one goes in only once the old one is moved aside by hand
    assert "never overwritten" in request["note"]
    assert state(root) == before


def test_r312_a_copy_whose_source_is_not_the_one_of_the_config_is_refused(tmp_path):
    root = tree(tmp_path)
    # The IMC copy carries the name of the floor master of Revit All Zones, a round that pairs by name would make it from there
    entries = both_entries(root)
    entries[1] = entry(root, "IMC", MODEL_C, "Revit All Zones\\" + MODEL_C)
    write_manifest(root, entries)

    found = verdicts(root, tmp_path)

    assert found["IMC"]["verdict"] == "remake" and found["IMC"]["reasons"] == ["otherSource"]
    assert found["IMC"]["request"]["copyFrom"] == str(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C)
    assert found["IMC"]["request"]["spec"]["source"] == str(tmp_path / "work" / ORIGINAL_C)
    # The same source written in another case or with / is the same file on Windows. Its hash is read from the path as it is on
    # disk, since the runner of the gate matches names in their case
    entries[1] = entry(root, "IMC", MODEL_C, "00_REVIT MODELS/zone 02/arc/" + ORIGINAL_C, source_hash=digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C))
    write_manifest(root, entries)
    assert verdicts(root, tmp_path)["IMC"]["verdict"] == "reuse"


def test_r312_a_changed_copy_a_copy_the_manifest_does_not_name_a_copy_gone_and_another_format_are_refused(tmp_path):
    root = tree(tmp_path)
    entries = both_entries(root)
    entries[1]["copySha256"] = "0" * 64
    write_manifest(root, entries[1:])
    found = verdicts(root, tmp_path)
    assert found["IMA"]["reasons"] == ["notInManifest"] and found["IMC"]["reasons"] == ["copyChanged"]

    rvt(root / "Revit All Zones" / "2024" / MODEL_C, 2021, "copy C in 2021")
    write_manifest(root, [entry(root, "IMC", MODEL_C, "00_Revit Models\\Zone 02\\ARC\\" + ORIGINAL_C, year=2021)])
    assert verdicts(root, tmp_path)["IMC"]["reasons"] == ["notFormat"]

    write_manifest(root, both_entries(root))
    (root / "Revit All Zones" / "2024" / MODEL_A).unlink()
    found = verdicts(root, tmp_path)
    assert found["IMA"]["verdict"] == "remake" and found["IMA"]["reasons"] == ["copyGone"]


def test_r312_a_copy_that_is_no_revit_file_is_refused_as_another_format_and_the_check_goes_on(tmp_path):
    root = tree(tmp_path)
    (root / "Revit All Zones" / "2024" / MODEL_A).write_bytes(b"not a compound file")
    # The manifest names the copy as it is now, so only its format is wrong
    write_manifest(root, both_entries(root))

    found = verdicts(root, tmp_path)

    assert found["IMA"]["verdict"] == "remake" and found["IMA"]["reasons"] == ["notFormat"] and found["IMA"]["format"] is None
    assert found["IMC"]["verdict"] == "reuse"


def test_r312_an_entry_of_another_code_is_refused_and_an_entry_of_no_key_is_listed_never_dropped(tmp_path):
    root = tree(tmp_path)
    entries = both_entries(root)
    entries[0]["code"] = "IMB"
    stray = entry(root, "IMZ", MODEL_C, "Revit All Zones\\" + MODEL_C)
    stray["copy"] = "01101-PSN-IMZ-ZZZ-PIM-TU-000001-zone9.rvt"
    write_manifest(root, entries + [stray])

    rows = key_copies.check(root, key_copies.load_keys(block()), tmp_path / "work")

    found = {row["copy"]: row for row in rows}
    assert found[MODEL_A]["verdict"] == "remake" and found[MODEL_A]["reasons"] == ["otherCode"]
    assert found[MODEL_C]["verdict"] == "reuse"
    assert found[stray["copy"]]["verdict"] == "noKey" and found[stray["copy"]]["code"] == "IMZ" and found[stray["copy"]]["request"] is None
    assert len(rows) == 3


def test_r312_no_copy_and_no_entry_asks_for_a_site_upgrade_job_and_a_missing_source_for_nothing(tmp_path):
    root = tree(tmp_path)
    (root / "Revit All Zones" / "2024" / MODEL_A).unlink()

    found = verdicts(root, tmp_path)

    assert found["IMA"]["verdict"] == "make" and found["IMA"]["request"]["spec"]["code"] == "KA"
    assert found["IMA"]["request"]["made"] == str(tmp_path / "work" / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1_TEST-KA.rvt")
    (root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C).unlink()
    found = verdicts(root, tmp_path)
    assert found["IMC"]["verdict"] == "noSource" and found["IMC"]["request"] is None


def make_writable(root):
    for path in root.rglob("*"):
        if path.is_file():
            os.chmod(path, stat.S_IREAD | stat.S_IWRITE)


def test_r312_check_and_import_write_nothing_to_a_source(tmp_path):
    root = tree(tmp_path)
    sources = [root / "Revit All Zones" / MODEL_A, root / "Revit All Zones" / MODEL_C, root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C]
    (root / "Revit All Zones" / "2024" / MODEL_A).unlink()
    old = tmp_path / "sources.txt"
    old.write_text("%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by the siteUpgrade job KC of a dummy round\n"
                   % (MODEL_C, digest(root / "Revit All Zones" / "2024" / MODEL_C), ORIGINAL_C, digest(sources[2])), encoding="utf-8")
    work = tmp_path / "work"
    before = {path: (digest(path), path.stat().st_size, path.stat().st_mtime_ns) for path in sources}
    # A source that can not be written can still be read by every step, so no step tries to write it
    for path in sources:
        os.chmod(path, stat.S_IREAD)
    try:
        values = key_copies.load_keys(block())
        key_copies.import_sources(root, values, old)
        assert [row["verdict"] for row in key_copies.check(root, values, work)] == ["make", "reuse"]
    finally:
        make_writable(root)
    assert {path: (digest(path), path.stat().st_size, path.stat().st_mtime_ns) for path in sources} == before


def test_r312_import_writes_the_manifest_from_sources_txt_and_the_hashes_of_today_once(tmp_path):
    root = tree(tmp_path)
    old = tmp_path / "sources.txt"
    lines = ["%s sha256 %s from Revit All Zones\\%s sha256 %s by the siteUpgrade job KA of round X on 2026-10-25 07:43"
             % (MODEL_A, digest(root / "Revit All Zones" / "2024" / MODEL_A), MODEL_A, digest(root / "Revit All Zones" / MODEL_A)),
             "%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by the siteUpgrade job KC of round X on 2026-10-25 07:43"
             % (MODEL_C, digest(root / "Revit All Zones" / "2024" / MODEL_C), ORIGINAL_C, digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C))]
    old.write_text("\n".join(lines) + "\n", encoding="utf-8")
    values = key_copies.load_keys(block())

    written = key_copies.import_sources(root, values, old)

    manifest = json.loads((root / "Revit All Zones" / "2024" / "key_copies.json").read_text(encoding="utf-8"))
    assert manifest["copies"] == written and [row["code"] for row in written] == ["IMA", "IMC"]
    assert written[1] == {"code": "IMC", "copy": MODEL_C, "copySha256": digest(root / "Revit All Zones" / "2024" / MODEL_C),
                          "source": "00_Revit Models\\Zone 02\\ARC\\" + ORIGINAL_C,
                          "sourceSha256": digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C), "format": 2024,
                          "madeBy": "the siteUpgrade job KC of round X on 2026-10-25 07:43"}
    assert {row["verdict"] for row in key_copies.check(root, values, tmp_path / "work")} == {"reuse"}
    # The manifest is written once, an import never overwrites it
    with pytest.raises(key_copies.KeyCopyError, match="already"):
        key_copies.import_sources(root, values, old)


def test_r312_import_refuses_a_copy_changed_since_sources_txt_and_a_line_of_no_key(tmp_path):
    root = tree(tmp_path)
    old = tmp_path / "sources.txt"
    values = key_copies.load_keys(block())
    old.write_text("%s sha256 %s from Revit All Zones\\%s sha256 %s by a run\n" % (MODEL_A, "a" * 64, MODEL_A, digest(root / "Revit All Zones" / MODEL_A)), encoding="utf-8")
    with pytest.raises(key_copies.KeyCopyError, match="changed"):
        key_copies.import_sources(root, values, old)
    old.write_text("other.rvt sha256 %s from Revit All Zones\\other.rvt sha256 %s by a run\n" % ("a" * 64, "b" * 64), encoding="utf-8")
    with pytest.raises(key_copies.KeyCopyError, match="no key"):
        key_copies.import_sources(root, values, old)
    assert not (root / "Revit All Zones" / "2024" / "key_copies.json").exists()


def test_r312_import_checks_every_entry_as_the_manifest_reader_does_and_refuses_the_whole_import_with_every_reason(tmp_path):
    root = tree(tmp_path)
    values = key_copies.load_keys(block())
    copy_a, copy_c = root / "Revit All Zones" / "2024" / MODEL_A, root / "Revit All Zones" / "2024" / MODEL_C
    # The IMA copy is no compound file, so it has no format, the IMC line writes its source with a drive, and IMC is listed twice
    copy_a.write_bytes(b"not a compound file")
    lines = ["%s sha256 %s from Revit All Zones\\%s sha256 %s by run KA" % (MODEL_A, digest(copy_a), MODEL_A, "b" * 64),
             "%s sha256 %s from C:\\02-Site Adaptation\\00_Revit Models\\%s sha256 %s by run KC" % (MODEL_C, digest(copy_c), ORIGINAL_C, "b" * 64),
             "%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by run KC" % (MODEL_C.lower(), digest(copy_c), ORIGINAL_C, "b" * 64)]
    old = tmp_path / "sources.txt"
    old.write_text("\n".join(lines) + "\n", encoding="utf-8")
    before = state(root)

    with pytest.raises(key_copies.KeyCopyError) as refused:
        key_copies.import_sources(root, values, old)

    reasons = str(refused.value)
    assert "nothing is written" in reasons and "no format" in reasons and "no plain path" in reasons and "twice" in reasons
    assert state(root) == before


def test_r312_check_lists_every_revit_file_in_the_copy_folder_no_key_and_no_entry_names_and_ends_not_0(tmp_path, capsys):
    root = tree(tmp_path)
    write_manifest(root, both_entries(root))
    rvt(root / "Revit All Zones" / "2024" / "a copy left by hand.rvt", 2024, "stray")
    rvt(root / "Revit All Zones" / "2024" / "old" / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1.RVT", 2024, "older copy")
    (root / "Revit All Zones" / "2024" / "notes.txt").write_text("not a model", encoding="utf-8")

    rows = key_copies.check(root, key_copies.load_keys(block()), tmp_path / "work")

    unlisted = sorted(row["copy"] for row in rows if row["verdict"] == "notListed")
    assert unlisted == ["a copy left by hand.rvt", "old/01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1.RVT"]
    assert {row["code"]: row["verdict"] for row in rows if row["code"]} == {"IMA": "reuse", "IMC": "reuse"}
    assert key_copies.main(["--settings", str(settings_file(tmp_path, root)), "check"]) == 1
    assert "notListed" in capsys.readouterr().out


def test_r312_every_function_of_key_copies_names_its_rule_in_the_first_line_of_its_docstring():
    import inspect
    functions = [function for name, function in inspect.getmembers(key_copies, inspect.isfunction) if function.__module__ == "key_copies"]
    assert len(functions) > 15
    for function in functions:
        first = (function.__doc__ or "").strip().splitlines()[:1]
        assert first and re.match(r"R[0-9]+\. ", first[0]), function.__name__


def settings_file(tmp_path, root, values=None):
    """A project settings file with the input root, the answer keys and the round folder, whose work folder is tmp_path/work."""
    path = tmp_path / "project.json"
    path.write_text(json.dumps({"inputRoot": str(root), "answerKeys": values or block(), "siteBuild": {"folder": str(tmp_path)}}), encoding="utf-8")
    return path


def test_r312_import_writes_the_manifest_by_a_temporary_file_and_a_rename_so_a_failed_rename_leaves_no_file(tmp_path, monkeypatch):
    root = tree(tmp_path)
    old = tmp_path / "sources.txt"
    old.write_text("%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by the job KC\n"
                   % (MODEL_C, digest(root / "Revit All Zones" / "2024" / MODEL_C), ORIGINAL_C, digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C)), encoding="utf-8")
    before = state(root)

    def no_rename(*args):
        raise OSError("the rename failed")
    monkeypatch.setattr(key_copies.os, "replace", no_rename)
    with pytest.raises(OSError, match="the rename failed"):
        key_copies.import_sources(root, key_copies.load_keys(block()), old)

    assert state(root) == before


def test_r312_record_is_withdrawn_so_check_asks_for_a_remake_by_hand_and_no_step_files_a_made_copy(tmp_path, capsys):
    root = tree(tmp_path)
    write_manifest(root, both_entries(root))
    rvt(root / "Revit All Zones" / MODEL_A, 2021, "source A saved again")
    settings = settings_file(tmp_path, root)
    before = state(tmp_path)

    found = verdicts(root, tmp_path)
    assert found["IMA"]["verdict"] == "remake" and "Q069" in found["IMA"]["request"]["note"] and "withdrawn" in found["IMA"]["request"]["note"]
    assert key_copies.main(["--settings", str(settings), "check"]) == 1
    assert "no step of key_copies.py files a made copy, record is withdrawn (Q069)" in capsys.readouterr().out
    # The command is gone, so nothing can file a copy a siteUpgrade job made
    assert not hasattr(key_copies, "record")
    with pytest.raises(SystemExit) as stop:
        key_copies.main(["--settings", str(settings), "record", "--code", "IMA"])
    assert stop.value.code == 2 and "invalid choice" in capsys.readouterr().err
    assert state(tmp_path) == before


def test_r312_main_check_prints_one_line_per_row_and_ends_0_only_when_every_copy_is_reused(tmp_path, capsys):
    root = tree(tmp_path)
    write_manifest(root, both_entries(root))
    settings = settings_file(tmp_path, root)
    out = tmp_path / "rows.json"

    assert key_copies.main(["--settings", str(settings), "check", "--out", str(out)]) == 0
    printed = capsys.readouterr().out.splitlines()
    assert printed == ["IMA %s reuse" % MODEL_A, "IMC %s reuse" % MODEL_C, "reuse 2 of 2"]
    assert [row["verdict"] for row in json.loads(out.read_text(encoding="utf-8"))["rows"]] == ["reuse", "reuse"]

    rvt(root / "Revit All Zones" / MODEL_A, 2021, "source A saved again")
    assert key_copies.main(["--settings", str(settings), "check"]) == 1
    assert "IMA %s remake sourceChanged" % MODEL_A in capsys.readouterr().out


def test_r312_main_import_needs_its_sources_file_and_writes_the_manifest(tmp_path, capsys):
    root = tree(tmp_path)
    settings = settings_file(tmp_path, root)
    with pytest.raises(SystemExit) as stop:
        key_copies.main(["--settings", str(settings), "import"])
    assert stop.value.code == 2 and "--sources" in capsys.readouterr().err
    old = tmp_path / "sources.txt"
    old.write_text("%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by the job KC\n"
                   % (MODEL_C, digest(root / "Revit All Zones" / "2024" / MODEL_C), ORIGINAL_C, digest(root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C)), encoding="utf-8")

    assert key_copies.main(["--settings", str(settings), "import", "--sources", str(old)]) == 0

    assert "IMC %s written from 00_Revit Models\\Zone 02\\ARC\\%s" % (MODEL_C, ORIGINAL_C) in capsys.readouterr().out
    assert (root / "Revit All Zones" / "2024" / "key_copies.json").is_file()


def bad_key(field, value):
    keys = block()["keys"]
    keys[0] = dict(keys[0], **{field: value})
    return block(keys=keys)


@pytest.mark.parametrize("values", [
    bad_key("kind", "ARC"), bad_key("code", "IM1"), bad_key("zone", "zone 1"), bad_key("copy", "2024\\" + MODEL_A),
    bad_key("copy", "01101-PSN-IMA.txt"), bad_key("source", "C:\\Revit All Zones\\" + MODEL_A), bad_key("source", "..\\" + MODEL_A),
    bad_key("source", "Revit All Zones\\2024\\" + MODEL_A), bad_key("extra", 1), bad_key("copy", MODEL_C.upper()),
    bad_key("code", "IMC"), bad_key("source", "00_Revit Models\\zone 02\\arc\\" + ORIGINAL_C),
    block(format="2024"), block(jobPrefix="k"), block(keys=[]), block(manifest="2024\\key_copies.json"), block(folder="..\\2024"),
    block(codePrefix="im"), block(codePrefix=""), block(zonePattern="Zone ("), block(zonePattern=3), block(kinds=[]), block(kinds=["TU", "TU"]),
    block(kinds="TU"), block(codeInName="^[0-9]{5}-PSN-"), block(codeInName="{code}-{code}"), block(codeInName="({code}")])
def test_r312_a_key_list_with_a_missing_or_extra_field_a_bad_kind_a_code_copy_or_source_twice_or_a_source_among_the_copies_is_refused(values):
    with pytest.raises(key_copies.KeyCopyError):
        key_copies.load_keys(values)


@pytest.mark.parametrize("values", [
    bad_key("source", "Revit All Zones\\2024.\\x.rvt"), bad_key("source", "Revit All Zones\\2024.\\" + MODEL_A),
    bad_key("source", "revit all zones/2024./" + MODEL_A),
    dict(bad_key("source", "Revit All Zones\\2024\\" + MODEL_A), folder="Revit All Zones\\2024 ."),
    dict(bad_key("source", "Revit All Zones\\2024\\" + MODEL_A), folder="REVIT ALL ZONES/2024.")])
def test_r312_a_source_in_the_copy_folder_is_refused_by_the_place_windows_opens_not_by_the_text(values):
    with pytest.raises(key_copies.KeyCopyError, match="in the copy folder"):
        key_copies.load_keys(values)


def test_r312_a_path_is_opened_as_windows_opens_it_a_trailing_dot_or_blank_off_and_the_case_folded():
    assert key_copies.opened_parts("Revit All Zones\\2024.\\x.rvt", "a source") == ("Revit All Zones", "2024", "x.rvt")
    assert key_copies.opened_parts("Zone 02/ARC/Prototypes_2024.", "a folder") == ("Zone 02", "ARC", "Prototypes_2024")
    assert key_copies.opened_parts("a. ./b. . .", "a path") == ("a. ", "b")
    assert key_copies.opened_parts("a../b .", "a path") == ("a..", "b")
    assert key_copies.place("REVIT All Zones/2024./X.rvt", "a source") == key_copies.place("revit all zones\\2024\\x.rvt", "a source")
    assert key_copies.place("a ./b", "a path") != key_copies.place("a/b", "a path")
    # A last part Windows takes away whole names the folder above it, which is refused, as a drive and a parent step still are
    for text in ("a/...", "C:\\a", "a\\..\\b", "a\\.\\b", "a\\\\b", " a"):
        with pytest.raises(key_copies.KeyCopyError):
            key_copies.place(text, "a path")


# Texts plain_parts takes, a blank around a part being refused before, each part before the last and the last
WINDOWS_CASES = ["Revit All Zones/2024./x.rvt", "Prototypes_2024.", "a. ./b", "a ./b", "a../b", ".../b", "a/b. . .", "a/b .", "a/b./c",
                 "a/....", "x.rvt.", "a.b./c.d.", "a/.../b", "a. . ./b"]


@pytest.mark.skipif(os.name != "nt", reason="GetFullPathNameW is a Windows call, the runner of the gate is Linux")
@pytest.mark.parametrize("text", WINDOWS_CASES)
def test_r312_the_opened_parts_equal_what_windows_get_full_path_name_gives(text):
    from nt import _getfullpathname
    root = "C:\\place-root\\"
    try:
        mine = "\\".join(key_copies.opened_parts(text, "a path"))
    except key_copies.KeyCopyError:
        mine = None
    full = _getfullpathname(root + text.replace("/", "\\"))
    windows = full[len(root):].rstrip("\\") if full.startswith(root) else None
    # A path whose last part Windows takes away whole is refused by the code
    assert mine == (windows if windows and windows.count("\\") == text.count("/") else None)


def test_r312_an_entry_whose_source_opens_the_place_of_the_source_of_the_config_is_reused(tmp_path):
    root = tree(tmp_path)
    entries = both_entries(root)
    entries[1]["source"] = "00_REVIT MODELS/zone 02./arc/" + ORIGINAL_C + "."
    write_manifest(root, entries)
    assert verdicts(root, tmp_path)["IMC"]["verdict"] == "reuse"


def test_r312_a_copy_name_or_a_source_name_that_does_not_hold_the_code_of_its_own_key_is_refused():
    keys = block()["keys"]
    # Two copy names swapped between their keys
    keys[0]["copy"], keys[1]["copy"] = keys[1]["copy"], keys[0]["copy"]
    with pytest.raises(key_copies.KeyCopyError, match="does not hold the code IMA"):
        key_copies.load_keys(block(keys=keys))
    keys = block()["keys"]
    keys[1]["source"] = "00_Revit Models\\Zone 02\\ARC\\01101-PSN-IMA-ZZZ-PIM-TU-000001.rvt"
    with pytest.raises(key_copies.KeyCopyError, match="does not hold the code IMC"):
        key_copies.load_keys(block(keys=keys))
    # A pattern that is not anchored would take a name that holds two codes, so a name that holds the code of another key is refused too
    keys = block()["keys"]
    keys[0]["copy"] = "01101-PSN-IMA-IMC-PIM-SE-000000-zone1.rvt"
    with pytest.raises(key_copies.KeyCopyError, match="holds the code IMC of another key"):
        key_copies.load_keys(block(keys=keys, codeInName="-{code}-"))
    # The code is found in any case, as Windows matches names
    keys = block()["keys"]
    keys[0]["copy"] = MODEL_A.lower()
    assert key_copies.load_keys(block(keys=keys))["keys"][0]["copy"] == MODEL_A.lower()


def test_r312_the_job_code_and_the_code_come_from_the_prefixes_of_the_config():
    values = key_copies.load_keys(block())
    assert [key_copies.job_code(values, key) for key in values["keys"]] == ["KA", "KC"]
    keys = block()["keys"]
    for key, code in zip(keys, ("XYA", "XYC")):
        key["code"] = code
        key["copy"] = key["copy"].replace("-IM", "-XY")
        key["source"] = key["source"].replace("-IM", "-XY")
    values = key_copies.load_keys(block(keys=keys, codePrefix="XY", jobPrefix="Q", kinds=["SE", "TU"], zonePattern="Zone [0-9]"))
    assert [key_copies.job_code(values, key) for key in values["keys"]] == ["QA", "QC"]
    with pytest.raises(key_copies.KeyCopyError, match="code"):
        key_copies.load_keys(block(keys=keys))


@pytest.mark.parametrize("change", [{"copySha256": "A" * 64}, {"format": "2024"}, {"madeBy": ""}, {"copy": "2024/" + MODEL_A}, {"extra": 1}])
def test_r312_a_manifest_entry_that_is_not_whole_is_refused(tmp_path, change):
    root = tree(tmp_path)
    entries = both_entries(root)
    entries[0].update(change)
    write_manifest(root, entries)
    with pytest.raises(key_copies.KeyCopyError):
        verdicts(root, tmp_path)


def test_r312_the_real_config_lists_the_7_keys_with_imc_from_the_zone_02_arc_original_and_the_names_of_revit_all_zones():
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    values = key_copies.load_keys(settings["answerKeys"])
    keys = {key["code"]: key for key in values["keys"]}
    assert sorted(keys) == ["IMA", "IMB", "IMC", "IMD", "IME", "IMF", "IMG"]
    assert keys["IMC"]["source"] == "00_Revit Models\\Zone 02\\ARC\\01101-PSN-IMC-ZZZ-PIM-TU-000001.rvt"
    assert keys["IMC"]["copy"] == "01101-PSN-IMC-ZZZ-PIM-TU-000001-zone2.rvt"
    for code, key in keys.items():
        assert code in key["copy"] and key["kind"] in ("TU", "SE")
        if code != "IMC":
            assert key["source"] == "Revit All Zones\\" + key["copy"]
    assert [keys[code]["zone"] for code in sorted(keys)] == ["Zone 1", "Zone 1", "Zone 2", "Zone 2", "Zone 3", "Zone 3", "Zone 4"]
    assert [keys[code]["kind"] for code in sorted(keys)] == ["SE", "SE", "TU", "TU", "TU", "TU", "TU"]
    # The prefixes, the kinds and the zone shape moved from the code into the config give the same keys and job codes as before
    assert (values["codePrefix"], values["jobPrefix"], values["kinds"]) == ("IM", "K", ["TU", "SE"])
    assert [key_copies.job_code(values, keys[code]) for code in sorted(keys)] == ["KA", "KB", "KC", "KD", "KE", "KF", "KG"]


def test_r312_the_copy_folder_of_r301_is_the_folder_of_the_answer_keys_with_the_same_manifest():
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))["answerKeys"]
    probe = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["data5"]["imCodes"]["copyFolders"]
    assert [(entry["folder"].replace("/", "\\"), entry["manifest"]) for entry in probe] == [(settings["folder"], settings["manifest"])]
