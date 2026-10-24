import json

import pytest

import rvt_info
from cfb_fixture import build

INFO = ("\x00\u0a0dWorksharing: Central\r\nUsername: \r\nCentral Model Path: \\\\srv\\Models\\site.rvt\r\nFormat: 2021\r\n"
        "Build: 20200220_1100(x64)\r\nLast Save Path: \\\\srv\\Models\\site.rvt\r\nUnique Document GUID: abc\r\n")
XML = ('<?xml version="1.0"?>\r\n<TransmissionData isTransmitted="false" userData="" version="5">'
       "<ExternalFileReference><ElementId>1</ElementId><ExternalFileReferenceType>Revit Link</ExternalFileReferenceType>"
       "<LastSavedPath>..\\Links\\RRE-WP-NSP-KEO-ARC-D4-ZZ-MOD-2400.rvt</LastSavedPath><LastSavedAbsolutePath>P:\\Links\\RRE-WP-NSP-KEO-ARC-D4-ZZ-MOD-2400.rvt</LastSavedAbsolutePath>"
       "<LastSavedPathType>Relative to Central Model</LastSavedPathType><LastSavedLoadState>Loaded</LastSavedLoadState>"
       "<DesiredLoadState>Loaded</DesiredLoadState></ExternalFileReference>"
       "<ExternalFileReference><ElementId>2</ElementId><ExternalFileReferenceType>CAD Link</ExternalFileReferenceType>"
       "<LastSavedPath>roads.dwg</LastSavedPath><LastSavedAbsolutePath>P:\\roads.dwg</LastSavedAbsolutePath>"
       "<LastSavedPathType>Absolute</LastSavedPathType><LastSavedLoadState>Not Found</LastSavedLoadState>"
       "<DesiredLoadState>Unloaded</DesiredLoadState></ExternalFileReference></TransmissionData>")


def utf16(text, head=b"\x0e\x00\x00\x00"):
    return head + text.encode("utf-16-le")


def model(tmp_path, streams):
    path = tmp_path / "site.rvt"
    path.write_bytes(build(streams))
    return path


def test_r203_basic_file_info_gives_format_build_worksharing_and_paths_past_a_stray_head(tmp_path):
    path = model(tmp_path, [("BasicFileInfo", utf16(INFO)), ("TransmissionData", utf16(XML, b"\x84-\x00\x00"))])

    row = rvt_info.read(path)

    info = row["info"]
    assert info["formatYear"] == 2021 and info["build"] == "20200220_1100(x64)"
    assert info["worksharing"] == "Central" and info["workshared"] is True
    assert info["centralModelPath"] == "\\\\srv\\Models\\site.rvt"
    assert "username" in info and info["username"] == ""


def test_r203_not_enabled_worksharing_is_not_workshared(tmp_path):
    path = model(tmp_path, [("BasicFileInfo", utf16(INFO.replace("Central\r\n", "Not enabled\r\n", 1)))])

    assert rvt_info.read(path)["info"]["workshared"] is False


def test_r203_transmission_data_lists_each_reference_with_type_paths_and_states(tmp_path):
    path = model(tmp_path, [("BasicFileInfo", utf16(INFO)), ("TransmissionData", utf16(XML, b"\x84-\x00\x00"))])

    row = rvt_info.read(path)

    assert [item["type"] for item in row["references"]] == ["Revit Link", "CAD Link"]
    link = row["revitLinks"][0]
    assert link["file"] == "RRE-WP-NSP-KEO-ARC-D4-ZZ-MOD-2400.rvt" and link["loadState"] == "Loaded" and link["pathType"] == "Relative to Central Model"
    assert row["references"][1]["loadState"] == "Not Found"


def test_r203_a_large_stream_outside_the_mini_stream_is_read_whole(tmp_path):
    big = XML.replace("</TransmissionData>", "<!--" + "x" * 5000 + "--></TransmissionData>")
    path = model(tmp_path, [("BasicFileInfo", utf16(INFO)), ("TransmissionData", utf16(big, b"\x84-\x00\x00"))])

    assert len(rvt_info.read(path)["revitLinks"]) == 1


def test_r203_a_file_with_no_signature_or_no_basic_file_info_is_refused(tmp_path):
    plain = tmp_path / "plain.rvt"
    plain.write_bytes(b"not a compound file" * 40)
    with pytest.raises(rvt_info.CfbError):
        rvt_info.read(plain)
    with pytest.raises(rvt_info.CfbError):
        rvt_info.read(model(tmp_path, [("Contents", b"x" * 100)]))


def test_r203_main_reads_only_and_records_an_error_per_file(tmp_path):
    good = model(tmp_path, [("BasicFileInfo", utf16(INFO))])
    before = good.read_bytes()
    bad = tmp_path / "bad.rvt"
    bad.write_bytes(b"x" * 600)
    out = tmp_path / "out" / "info.json"

    code = rvt_info.main([str(good), str(bad), "--out", str(out)])

    rows = json.loads(out.read_text(encoding="utf-8"))["files"]
    assert code == 1 and rows[0]["info"]["formatYear"] == 2021 and "error" in rows[1]
    assert good.read_bytes() == before
