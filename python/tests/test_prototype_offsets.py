import json
import re
from pathlib import Path

import pytest

import project_profile
import prototype_offsets

PYTHON_DIR = Path(__file__).resolve().parent.parent
# The counts, the seed and the positions in the file are not fitted values
NOT_VALUES = {"plots", "position", "splitSeed", "plotsWithIt", "differentRelations", "entrancePlots", "relationRightOnTest", "fitError", "testError", "entranceTestError"}


def fitted_numbers(node):
    """Every fitted number of the offsets file: the entry offset, the offsets, the turns and the entrances, not counts or error tables."""
    if isinstance(node, dict):
        for key, value in node.items():
            if key not in NOT_VALUES:
                yield from fitted_numbers(value)
    elif isinstance(node, list):
        for value in node:
            yield from fitted_numbers(value)
    elif isinstance(node, (int, float)) and not isinstance(node, bool):
        yield node


def tokens(path):
    return {("%.3f" % float(token)).rstrip("0").rstrip(".") for token in re.findall(r"(?<![\w.])-?\d+(?:\.\d+)?(?![\w.])", path.read_text(encoding="utf-8"))}


def write_offsets(path, **sections):
    data = {"note": "x", "entry": {"roadOffsetM": 0.14}, "groups": [], **sections}
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


def test_r100_the_shipped_file_is_read_with_both_sections_and_every_group_has_the_keys_a_rule_needs():
    data = prototype_offsets.load()
    assert isinstance(prototype_offsets.entry_offset(data), float)
    index = prototype_offsets.group_index(data)
    assert len(index) == len(data["groups"]) > 0
    for group in data["groups"]:
        assert set(prototype_offsets.GROUP_KEYS) <= set(group)
        assert len(group["offset"]) == 2 and set(group["relative"]) >= {"rotationDeg", "mirrored"}


def test_r100_the_shipped_file_says_it_is_measured_from_the_imc_past_job_and_gives_the_seed_and_the_error_of_each_group():
    data = prototype_offsets.load()
    assert "Measured from the IMC past job" in data["note"] and data["entry"]["source"] == "measured from past work"
    for group in data["groups"]:
        if group["tooFew"]:
            continue
        assert group["splitSeed"] == 13
        assert set(group["fitError"]) == {"within1mm", "within10mm", "worse", "n"} and set(group["testError"]) == {"within1mm", "within10mm", "worse", "n"}
        assert group["fitError"]["n"] + group["testError"]["n"] == group["plots"]


def test_r100_no_python_file_holds_a_fitted_value_of_the_offsets_file():
    data = prototype_offsets.load()
    values = sorted(set(fitted_numbers(data)))
    assert values
    sources = [path for path in PYTHON_DIR.rglob("*.py") if ".venv" not in path.parts]
    code = [path for path in sources if "tests" not in path.parts]
    assert code
    # Every value with a fraction is looked for in the code files, a test file may hold the same small number for a dummy
    for path in code:
        found = tokens(path)
        for value in values:
            if value != int(value):
                assert ("%.3f" % value).rstrip("0").rstrip(".") not in found, str(path.name) + " holds " + str(value)
    # A value with three decimals is a noisy fit and nothing else writes that, so no test file holds it either
    for path in sources:
        found = tokens(path)
        for value in values:
            if round(value, 2) != value:
                assert ("%.3f" % value).rstrip("0").rstrip(".") not in found, str(path.name) + " holds " + str(value)


def test_r100_the_shipped_file_has_no_semicolon_and_no_em_dash():
    text = prototype_offsets.DEFAULT_PATH.read_text(encoding="utf-8")
    assert chr(59) not in text and chr(0x2014) not in text


def test_r100_a_missing_file_bad_json_or_a_missing_section_raises_one_error_with_the_file_name(tmp_path):
    with pytest.raises(project_profile.ProfileError, match="none.json cannot be read"):
        prototype_offsets.load(tmp_path / "none.json")
    (tmp_path / "bad.json").write_text("{x", encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="bad.json is not valid JSON"):
        prototype_offsets.load(tmp_path / "bad.json")
    (tmp_path / "list.json").write_text("[]", encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="list.json is not an object"):
        prototype_offsets.load(tmp_path / "list.json")
    (tmp_path / "half.json").write_text('{"entry": {}}', encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="half.json has no section groups"):
        prototype_offsets.load(tmp_path / "half.json")


def test_r100_the_entry_offset_must_be_a_number_and_a_group_must_hold_its_keys_and_appear_once(tmp_path):
    for bad in ({}, {"roadOffsetM": "0.14"}, {"roadOffsetM": True}):
        with pytest.raises(project_profile.ProfileError, match="roadOffsetM number"):
            prototype_offsets.entry_offset({"entry": bad})
    group = {"block": "B", "position": 1, "plots": 4, "offset": [0, 0], "relative": {}, "entrance": None}
    assert prototype_offsets.group_index({"groups": [group]}) == {("B", 1): group}
    with pytest.raises(project_profile.ProfileError, match="group has no offset"):
        prototype_offsets.group_index({"groups": [{k: v for k, v in group.items() if k != "offset"}]})
    with pytest.raises(project_profile.ProfileError, match="lists the group"):
        prototype_offsets.group_index({"groups": [group, dict(group)]})


def test_r100_write_section_replaces_one_section_keeps_the_other_and_rounds_to_the_millimetre(tmp_path):
    path = write_offsets(tmp_path / "offsets.json", groups=[{"keep": 1}])
    prototype_offsets.write_section(path, "entry", {"roadOffsetM": 0.14049, "other": 1.23456}, "a note")
    data = json.loads(path.read_text(encoding="utf-8"))
    assert data["groups"] == [{"keep": 1}] and data["note"] == "a note"
    assert data["entry"] == {"roadOffsetM": 0.1405, "other": 1.235}
    prototype_offsets.write_section(path, "groups", [])
    again = json.loads(path.read_text(encoding="utf-8"))
    assert again["entry"] == data["entry"] and again["groups"] == [] and again["note"] == "a note"


def test_r100_write_section_starts_a_file_that_does_not_exist_with_empty_sections(tmp_path):
    path = tmp_path / "sub" / "offsets.json"
    prototype_offsets.write_section(path, "entry", {"roadOffsetM": 0.1}, prototype_offsets.note_for("ABC"))
    data = prototype_offsets.load(path)
    assert data["groups"] == [] and prototype_offsets.entry_offset(data) == 0.1
    assert prototype_offsets.note_for("ABC").startswith("Measured from the ABC past job") and chr(59) not in prototype_offsets.NOTE
