"""R540. The help file of the add-in is the guide, built by python/build_help.py."""

import json

import pytest

import build_help


def book():
    return build_help.build(build_help.GUIDE.read_text(encoding="utf-8"))


def test_r540_the_help_file_is_built_from_the_guide():
    shipped = json.loads(build_help.OUTPUT.read_text(encoding="utf-8"))
    assert shipped == book(), "the guide changed, run python/build_help.py"


def test_r540_each_step_and_new_project_row_has_all_its_parts():
    entries = {item["key"]: item for item in book()["entries"]}
    assert set(entries) == {"step%d" % number for number in range(1, 10)} | set(build_help.ROWS.values())
    for item in entries.values():
        assert item["line"] and item["what"] and item["files"] and item["before"] and item["click"], item["key"]


def test_r540_a_code_name_in_the_guide_stops_the_build():
    with pytest.raises(build_help.HelpError):
        build_help.plain("Set by coordinates.source (R362)")


def test_r540_a_file_name_takes_its_plain_name():
    assert build_help.plain("**project.json** beside plan_<NBH>.json") == "The project settings file beside the plan"
