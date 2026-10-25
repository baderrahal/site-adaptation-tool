"""Round ADDIN Phase 1 part 2: the Revit 2024 copies of the answer keys checked or made by code from config/project.json (R312).

Every model here is a dummy compound file built in code with a BasicFileInfo stream (R10), so the format is read from the bytes as
on the real copies.
"""
import hashlib
import json
import os
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
    values = {"folder": "Revit All Zones\\2024", "manifest": "key_copies.json", "format": 2024, "jobPrefix": "K", "keys": keys}
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
    # The same source written in another case or with / is the same file on Windows
    entries[1] = entry(root, "IMC", MODEL_C, "00_REVIT MODELS/zone 02/arc/" + ORIGINAL_C)
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


def test_r312_check_import_and_record_write_nothing_to_a_source(tmp_path):
    root = tree(tmp_path)
    sources = [root / "Revit All Zones" / MODEL_A, root / "Revit All Zones" / MODEL_C, root / "00_Revit Models" / "Zone 02" / "ARC" / ORIGINAL_C]
    (root / "Revit All Zones" / "2024" / MODEL_A).unlink()
    old = tmp_path / "sources.txt"
    old.write_text("%s sha256 %s from 00_Revit Models\\Zone 02\\ARC\\%s sha256 %s by the siteUpgrade job KC of a dummy round\n"
                   % (MODEL_C, digest(root / "Revit All Zones" / "2024" / MODEL_C), ORIGINAL_C, digest(sources[2])), encoding="utf-8")
    work = tmp_path / "work"
    rvt(work / MODEL_A, 2021, "source A")
    made = rvt(work / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1_TEST-KA.rvt", 2024, "copy A made again")
    before = {path: (digest(path), path.stat().st_size, path.stat().st_mtime_ns) for path in sources}
    # A source that can not be written can still be read by every step, so no step tries to write it
    for path in sources:
        os.chmod(path, stat.S_IREAD)
    try:
        values = key_copies.load_keys(block())
        key_copies.import_sources(root, values, old)
        key_copies.check(root, values, work)
        key_copies.record(root, values, work, "IMA", made, "the siteUpgrade job KA of a dummy round")
        assert key_copies.check(root, values, work)[0]["verdict"] == "reuse"
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


def test_r312_record_moves_a_made_copy_into_the_copy_folder_never_over_a_file_and_writes_its_entry(tmp_path):
    root = tree(tmp_path)
    work = tmp_path / "work"
    values = key_copies.load_keys(block())
    rvt(work / MODEL_A, 2021, "source A")
    made = rvt(work / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1_TEST-KA.rvt", 2024, "copy A made again")
    made_hash = digest(made)
    # The old copy is still there, so nothing moves
    with pytest.raises(key_copies.KeyCopyError, match="never overwritten"):
        key_copies.record(root, values, work, "IMA", made, "run KA")
    assert made.exists() and digest(root / "Revit All Zones" / "2024" / MODEL_A) != made_hash

    (root / "Revit All Zones" / "2024" / MODEL_A).rename(tmp_path / "moved aside by hand.rvt")
    written = key_copies.record(root, values, work, "IMA", made, "run KA")

    assert not made.exists() and digest(root / "Revit All Zones" / "2024" / MODEL_A) == made_hash
    assert written == {"code": "IMA", "copy": MODEL_A, "copySha256": made_hash, "source": "Revit All Zones\\" + MODEL_A,
                       "sourceSha256": digest(root / "Revit All Zones" / MODEL_A), "format": 2024, "madeBy": "run KA"}
    assert key_copies.read_manifest(root / "Revit All Zones" / "2024" / "key_copies.json")[MODEL_A.casefold()] == written


def test_r312_record_refuses_a_made_copy_of_another_source_name_folder_or_format(tmp_path):
    root = tree(tmp_path)
    work = tmp_path / "work"
    values = key_copies.load_keys(block())
    (root / "Revit All Zones" / "2024" / MODEL_A).unlink()
    rvt(work / MODEL_A, 2021, "another file of that name")
    made = rvt(work / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1_TEST-KA.rvt", 2024, "copy A")
    with pytest.raises(key_copies.KeyCopyError, match="not the source"):
        key_copies.record(root, values, work, "IMA", made, "run KA")
    rvt(work / MODEL_A, 2021, "source A")
    with pytest.raises(key_copies.KeyCopyError, match="named"):
        key_copies.record(root, values, work, "IMA", rvt(work / "other_TEST-KA.rvt", 2024, "x"), "run KA")
    with pytest.raises(key_copies.KeyCopyError, match="work folder"):
        key_copies.record(root, values, work, "IMA", rvt(tmp_path / "01101-PSN-IMA-ZZZ-PIM-SE-000000-zone1_TEST-KA.rvt", 2024, "x"), "run KA")
    rvt(made, 2021, "copy A left in 2021")
    with pytest.raises(key_copies.KeyCopyError, match="format"):
        key_copies.record(root, values, work, "IMA", made, "run KA")
    assert not (root / "Revit All Zones" / "2024" / MODEL_A).exists()


def bad_key(field, value):
    keys = block()["keys"]
    keys[0] = dict(keys[0], **{field: value})
    return block(keys=keys)


@pytest.mark.parametrize("values", [
    bad_key("kind", "ARC"), bad_key("code", "IM1"), bad_key("zone", "zone 1"), bad_key("copy", "2024\\" + MODEL_A),
    bad_key("copy", "01101-PSN-IMA.txt"), bad_key("source", "C:\\Revit All Zones\\" + MODEL_A), bad_key("source", "..\\" + MODEL_A),
    bad_key("source", "Revit All Zones\\2024\\" + MODEL_A), bad_key("extra", 1), bad_key("copy", MODEL_C.upper()),
    bad_key("code", "IMC"), bad_key("source", "00_Revit Models\\zone 02\\arc\\" + ORIGINAL_C),
    block(format="2024"), block(jobPrefix="k"), block(keys=[]), block(manifest="2024\\key_copies.json"), block(folder="..\\2024")])
def test_r312_a_key_list_with_a_missing_or_extra_field_a_bad_kind_a_code_copy_or_source_twice_or_a_source_among_the_copies_is_refused(values):
    with pytest.raises(key_copies.KeyCopyError):
        key_copies.load_keys(values)


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


def test_r312_the_copy_folder_of_r301_is_the_folder_of_the_answer_keys_with_the_same_manifest():
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))["answerKeys"]
    probe = json.loads((ROOT / "python" / "c3d_probe.json").read_text(encoding="utf-8"))["dataBook"]["data5"]["imCodes"]["copyFolders"]
    assert [(entry["folder"].replace("/", "\\"), entry["manifest"]) for entry in probe] == [(settings["folder"], settings["manifest"])]
