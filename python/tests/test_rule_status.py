"""R401. The block status of the rules profile: one entry per value of the rules profile and the settings, each with one of the six
statuses and the fields it needs, written twice to the same bytes, never read as a value, and kept by every reader of the profile."""
import json
from pathlib import Path

import pytest

import key_rules_block
import plan_rules
import project_profile
import rule_status

ROOT = Path(__file__).resolve().parents[2]
FIELDS = {"CHOSEN by Bader": ({"where"}, [{"question"}, {"line"}]), "RECOMMENDED": ({"question", "where"}, [set()]),
          "CHOSEN by Code": ({"where"}, [set()]), "MEASURED": ({"share", "strength", "measuredBy"}, [set(), {"tiedBy"}]),
          "DOCUMENT": ({"document", "pageOrSheet", "source"}, [set()]), "UNKNOWN": ({"why"}, [set(), {"nearestSource"}])}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def make_repo(root, open_text="Q005. Tolerance: is analysis.toleranceM right (recommended).\n"):
    """A small repo with one value of each status, built in code."""
    write_json(root / "config" / "project.json", {
        "profile": "rules/p.json", "majority": {"strongShare": 0.9},
        "addin": {"limitMm": 5, "limitMmNote": "Code set it (CHOSEN)", "gapMm": 2, "gapMmNote": "answered (Q002)"},
        "analysis": {"toleranceM": 0.01, "endShare": 0.25, "otherShare": 0.5}})
    write_json(root / "rules" / "p.json", {
        "source": "Doc A Rev 06, June 2024", "slopes": {"front_min": 0.5}, "ramp": {"front_setback": {"default": 5520}},
        "codes": {"source": "Book_(Rev9).xlsx, sheet S1, as the sheet writes it", "rows": [{"w": 10}, {"w": 12}]},
        "hands": {"source": "the key", "villa": {"rule": "side", "share": 0.97}},
        "levelProfiles": {"note": "two profiles", "default": "p1", "p1": {
            "job": "x", "source": "the key with its schedule", "rampSlopePercent": {"default": 0.5, "C10": 2.0}, "rampTypes": {"0.5": "Ramps"},
            "other": 1, "keyRules": {"rampSlope": {"rule": "typology", "share": 0.95, "strong": True, "byKey": {"a": 1}},
                                     "steps": {"rule": "fromHeight", "share": 0.8, "strong": False}}}},
        "nothing": None})
    (root / "rules" / "README.md").write_text("The setback (Rev 6 section 2.3.A) is ramp.front_setback.\n", encoding="utf-8")
    (root / "steps").mkdir(parents=True, exist_ok=True)
    (root / "steps" / "PROGRESS.md").write_text("# Progress\n\n## Bader's turn\n\nQ005 Tolerance.\n\n## Next\n", encoding="utf-8")
    (root / "steps" / "questions-open.md").write_text(open_text, encoding="utf-8")
    (root / "steps" / "questions-closed.md").write_text(
        "| Q002 | Gap of analysis.otherShare | Yes (Bader) | 2026-10-27 |\n| Q003 | End | Yes (Bader): analysis.endShare stays | 2026-10-27 |\n\n"
        "## Q002\n\nQ002. Gap: is analysis.otherShare right\n",
        encoding="utf-8")
    (root / "docs" / "rules").mkdir(parents=True)
    (root / "docs" / "rules" / "index.md").write_text("# Rules index\n\n- R1 Rule one.\n", encoding="utf-8")
    (root / "docs" / "rules" / "01-a.md").write_text("- **R1 Rule one.** The profile holds slopes.front_min.\n", encoding="utf-8")
    return root


def entries(root):
    return json.loads((root / "rules" / "p.json").read_text(encoding="utf-8"))["status"]


def test_r401_every_value_has_one_entry_and_no_entry_names_a_missing_value(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    block = entries(root)
    for name in ("rules/p.json", "config/project.json"):
        data = json.loads((root / name).read_text(encoding="utf-8"))
        data.pop("status", None)
        values = [p for f, p, _, _ in rule_status.Audit(root).values() if f == name]
        assert sorted(block[name]) == sorted(values) and len(values) == len(set(values))
        for path in block[name]:
            found = rule_status.value_at(data, path)
            assert not isinstance(found, dict)
    assert len(block["rules/p.json"]) == 13 and len(block["config/project.json"]) == 6


def test_r401_each_status_holds_the_fields_it_needs(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    block = entries(root)
    rules, config = block["rules/p.json"], block["config/project.json"]
    assert rules["slopes.front_min"] == {"status": "DOCUMENT", "document": "Doc A Rev 06, June 2024", "pageOrSheet": "UNKNOWN", "source": "source"}
    assert rules["ramp.front_setback.default"]["pageOrSheet"] == "section 2.3.A"
    assert rules["codes.rows.1.w"] == {"status": "DOCUMENT", "document": "Book_(Rev9).xlsx, sheet S1, as the sheet writes it",
                                       "pageOrSheet": "sheet S1", "source": "codes.source"}
    assert rules["levelProfiles.p1.rampSlopePercent.C10"] == {"status": "MEASURED", "share": 0.95, "strength": "strong",
                                                             "measuredBy": "levelProfiles.p1.keyRules.rampSlope", "tiedBy": "Q101"}
    assert rules["levelProfiles.p1.rampTypes.0.5"]["measuredBy"] == "levelProfiles.p1.keyRules.rampSlope"
    assert rules["levelProfiles.p1.keyRules.steps.rule"] == {"status": "MEASURED", "share": 0.8, "strength": "weak",
                                                            "measuredBy": "levelProfiles.p1.keyRules.steps"}
    assert rules["hands.villa.rule"]["measuredBy"] == "hands.villa"
    assert rules["levelProfiles.p1.other"] == {"status": "UNKNOWN", "nearestSource": "levelProfiles.p1.source", "why": rule_status.WHY_NONE}
    assert rules["nothing"] == {"status": "UNKNOWN", "why": "null or empty"}
    assert rules["levelProfiles.default"]["question"] == "Q050"
    assert config["majority.strongShare"]["line"] == "the Majority rule of CLAUDE.md"
    assert config["addin.limitMm"] == {"status": "CHOSEN by Code", "where": "addin.limitMmNote"}
    assert config["addin.gapMm"] == {"status": "CHOSEN by Bader", "question": "Q002", "where": "addin.gapMmNote"}
    # A closed answer of Bader that names the value in its row of the table is a choice of Bader
    assert config["analysis.endShare"] == {"status": "CHOSEN by Bader", "question": "Q003", "where": "Q003"}
    assert config["analysis.toleranceM"] == {"status": "RECOMMENDED", "question": "Q005", "where": "Q005"}
    assert config["analysis.otherShare"]["status"] == "UNKNOWN"
    for name in block:
        if name != "note":
            for entry in block[name].values():
                need, optional = FIELDS[entry["status"]]
                assert list(entry)[0] == "status" and any(set(entry) - {"status"} == need | extra for extra in optional), entry


def test_r401_a_second_write_changes_nothing_and_no_value_changes(tmp_path):
    root = make_repo(tmp_path)
    path = root / "rules" / "p.json"
    before = json.loads(path.read_text(encoding="utf-8"))
    rule_status.write(root)
    first = path.read_bytes()
    rule_status.write(root)
    assert path.read_bytes() == first
    after = json.loads(first)
    after.pop("status")
    assert after == before
    assert rule_status.check(root) == []


def test_r401_the_block_is_never_read_as_a_value_or_as_evidence(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    path = root / "rules" / "p.json"
    text = path.read_text(encoding="utf-8")
    marked = text.replace('"note": "R401.', '"note": "analysis.otherShare (CHOSEN, Q002). R401.')
    assert marked != text
    path.write_text(marked, encoding="utf-8")
    assert rule_status.check(root) == []
    assert rule_status.Audit(root).values() == rule_status.Audit(make_repo(tmp_path / "plain")).values()


def test_r401_check_lists_an_entry_whose_evidence_changed(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    (root / "steps" / "questions-open.md").write_text("Q005. Tolerance: nothing here.\n", encoding="utf-8")
    changes = rule_status.check(root)
    assert len(changes) == 1 and changes[0].startswith("config/project.json analysis.toleranceM: ")
    assert '"UNKNOWN"' in changes[0]
    data = json.loads((root / "config" / "project.json").read_text(encoding="utf-8"))
    data["analysis"]["newShare"] = 1
    write_json(root / "config" / "project.json", data)
    assert any(line.startswith("config/project.json analysis.newShare: no entry") for line in rule_status.check(root))


def test_r401_a_question_id_inside_a_sentence_starts_no_question(tmp_path):
    root = make_repo(tmp_path, open_text="Q005. Tolerance: asked since Q003. so is analysis.toleranceM right\n")
    rule_status.write(root)
    assert entries(root)["config/project.json"]["analysis.toleranceM"]["question"] == "Q005"


def test_r401_a_closed_question_counts_by_its_answer_never_by_its_moved_text(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    config = entries(root)["config/project.json"]
    # The question cell and the moved text of Q002 name analysis.otherShare, its answer does not, so the value takes no status from it
    assert config["analysis.otherShare"]["status"] == "UNKNOWN"
    assert config["analysis.endShare"]["question"] == "Q003"


def test_r401_a_question_of_the_new_range_is_open_and_the_audit_questions_are_no_evidence(tmp_path):
    root = make_repo(tmp_path, open_text="Q105. Other: is analysis.otherShare right.\nQ101. Ties: analysis.toleranceM.\n")
    (root / "steps" / "PROGRESS.md").write_text("# Progress\n\n## Bader's turn\n\nQ105 Other. Q101 Ties.\n\n## Next\n", encoding="utf-8")
    rule_status.write(root)
    config = entries(root)["config/project.json"]
    assert config["analysis.otherShare"] == {"status": "RECOMMENDED", "question": "Q105", "where": "Q105"}
    assert config["analysis.toleranceM"]["status"] == "UNKNOWN"


def test_r401_a_block_that_would_change_a_value_is_refused():
    assert rule_status.put_block('{\n  "a": 1\n}\n', {"note": "n"}) == '{\n  "a": 1,\n  "status": {\n    "note": "n"\n  }\n}\n'
    # A value named status inside another object, written at the indent of the top level, is never taken for the block
    with pytest.raises(ValueError):
        rule_status.put_block('{\n  "a": {\n  "status": {"x": 1}\n  }\n}\n', {"note": "n"})


def test_r401_value_at_takes_a_key_with_a_dot_whole():
    data = {"rampTypes": {"0.5": "Ramps", "2": "Steep"}, "bands": [[0.2, 1], [0.4, 2]]}
    assert rule_status.value_at(data, "rampTypes.0.5") == "Ramps"
    assert rule_status.value_at(data, "bands.1") == [0.4, 2]
    with pytest.raises(KeyError):
        rule_status.value_at(data, "rampTypes.0.7")


def test_r401_the_real_block_covers_every_value_of_the_two_files():
    """For completeness on the real files: every value one entry with the fields of its status, and no entry of no value. The statuses
    are not held equal to a fresh run, rule_status.py --check reports that."""
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    profile = json.loads((ROOT / settings["profile"]).read_text(encoding="utf-8"))
    block = profile.pop("status")
    files = {settings["profile"]: profile, "config/project.json": settings}
    assert set(block) == set(files) | {"note"}
    values = rule_status.Audit(ROOT).values()
    assert len(values) == sum(len(block[f]) for f in files)
    for f, path, _, _ in values:
        entry = block[f][path]
        need, optional = FIELDS[entry["status"]]
        assert any(set(entry) - {"status"} == need | extra for extra in optional), (f, path, entry)
        assert not isinstance(rule_status.value_at(files[f], path), dict)


def test_r401_every_reader_of_the_rules_profile_keeps_reading_it_with_the_block():
    settings = project_profile.load_settings()
    path = project_profile.profile_path(settings)
    profile = project_profile.load_profile(path)
    assert "status" in profile.values
    lp = plan_rules.level_profile(profile)
    assert plan_rules.key_rules_of(lp) is lp["keyRules"]
    # R338 rewrites keyRules by text, the block after it stays byte for byte
    text = path.read_bytes().decode("utf-8")
    name = plan_rules.profile_name(profile, None)
    found = {word: entry for word, entry in lp["keyRules"].items()}
    new = key_rules_block.put_block(text, name, found)
    start = text.index('\n  "status": {')
    assert new[new.index('\n  "status": {'):] == text[start:]
