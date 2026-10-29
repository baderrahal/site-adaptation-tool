"""R401. The block status of the rules profile: one entry per value of the rules profile and the settings, each with one of the six
statuses and the fields it needs, written twice to the same bytes, never read as a value, and kept by every reader of the profile."""
import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

import key_rules_block
import plan_rules
import project_profile
import rule_status

ROOT = Path(__file__).resolve().parents[2]
FIELDS = {"CHOSEN by Bader": ({"where"}, [{"question"}, {"line"}]), "RECOMMENDED": ({"question", "where"}, [set()]),
          "CHOSEN by Code": ({"where"}, [set()]), "MEASURED": ({"share", "strength", "measuredBy"}, [set(), {"tiedBy"}, {"tiedBy", "candidate"}, {"tiedBy", "note"},
                                                                                      {"tiedBy", "candidate", "note"}]),
          "DOCUMENT": ({"document", "pageOrSheet", "source"}, [set()]), "UNKNOWN": ({"why"}, [set(), {"nearestSource"}])}


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def make_repo(root, open_text="Q005. Tolerance: is analysis.toleranceM right (recommended).\n"):
    """A small repo with one value of each status, built in code."""
    write_json(root / "config" / "project.json", {
        "profile": "rules/p.json", "majority": {"strongShare": 0.9, "check": {"group": "g", "file": "c.csv"}, "keep": {}},
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
            assert not (isinstance(found, dict) and found)
    assert len(block["rules/p.json"]) == 14 and len(block["config/project.json"]) == 8


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


REVIEW = ("# Status review\n\n## 1 Values CHOSEN by Code\n\n| # | What it sets | File and dotted path | Read by | Value |\n|---:|---|---|---|---|\n"
          "| 1 | a limit | config/project.json addin.limitMm | x.py:1 | 5 |\n\n"
          "## 2 Values still UNKNOWN that set the position or a level\n\n| # | What it sets | File and dotted path | Read by | Value |\n"
          "|---:|---|---|---|---|\n| 1 | a tolerance | config/project.json analysis.toleranceM | x.py:2 | 0.01 |\n"
          "| 2 | other | rules/p.json levelProfiles.p1.other | x.py:3 | 1 |\n\n"
          "## 2b Values for information\n\n| # | What it sets | File and dotted path | Read by | Value |\n|---:|---|---|---|---|\n"
          "| 1 | other share | config/project.json analysis.otherShare | x.py:4 | 0.5 |\n\n"
          "## 3 Values CHOSEN by Bader\n\n| # | File and dotted path | Value |\n|---:|---|---|\n| 1 | config/project.json analysis.endShare | 0.25 |\n")


def test_r401_the_review_list_of_q104_gives_chosen_by_bader_before_any_text_match(tmp_path):
    root = make_repo(tmp_path)
    review = root / "steps" / "investigations" / "status-review.md"
    review.parent.mkdir(parents=True)
    review.write_text(REVIEW, encoding="utf-8")
    rule_status.write(root)
    block = entries(root)
    config, rules = block["config/project.json"], block["rules/p.json"]
    where = "steps/investigations/status-review.md table %s row %s"
    # Q104 (Bader, 2026-10-27): tables 1 and 2 win over the CHOSEN mark of a note and over an open question that names the value
    assert config["addin.limitMm"] == {"status": "CHOSEN by Bader", "question": "Q104", "where": where % (1, 1)}
    assert config["analysis.toleranceM"] == {"status": "CHOSEN by Bader", "question": "Q104", "where": where % (2, 1)}
    assert rules["levelProfiles.p1.other"] == {"status": "CHOSEN by Bader", "question": "Q104", "where": where % (2, 2)}
    # Tables 2b and 3 are for information, they give nothing
    assert config["analysis.otherShare"]["status"] == "UNKNOWN"
    assert config["analysis.endShare"]["question"] == "Q003"
    assert review.read_text(encoding="utf-8") == REVIEW
    assert rule_status.check(root) == []


def test_r401_a_review_path_that_is_no_value_stops_the_write(tmp_path):
    root = make_repo(tmp_path)
    review = root / "steps" / "investigations" / "status-review.md"
    review.parent.mkdir(parents=True)
    review.write_text(REVIEW.replace("analysis.toleranceM", "analysis.noSuchKey"), encoding="utf-8")
    before = (root / "rules" / "p.json").read_bytes()
    with pytest.raises(ValueError, match=r"config/project.json analysis\.noSuchKey"):
        rule_status.write(root)
    assert (root / "rules" / "p.json").read_bytes() == before


def test_r401_a_source_that_starts_with_unknown_names_no_document(tmp_path):
    root = make_repo(tmp_path)
    path = root / "rules" / "p.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    data["steps"] = {"legend": [[0.1, 1], [0.2, 2]], "riser_added": 170, "front_min": 1,
                     "legend_source": "past job schedules, as the README ties them. DOCUMENT by Q106 (Bader, 2026-10-27)",
                     "riser_added_source": "UNKNOWN, the records name no document of this value"}
    write_json(path, data)
    rule_status.write(root)
    rules = entries(root)["rules/p.json"]
    # Q106 (Bader, 2026-10-27): a source that starts with UNKNOWN gives no DOCUMENT, a source that names the schedules does
    assert rules["steps.riser_added"] == {"status": "UNKNOWN", "nearestSource": "steps.riser_added_source",
                                          "why": "UNKNOWN, the records name no document of this value"}
    assert rules["steps.legend.1"] == {"status": "DOCUMENT", "document": "past job schedules, as the README ties them", "pageOrSheet": "UNKNOWN",
                                       "source": "steps.legend_source"}
    assert rules["steps.front_min"]["document"] == "Doc A Rev 06, June 2024"


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
    are held equal to a fresh run by test_r401_q109_the_real_block_equals_a_fresh_run_and_the_review_list_reads_its_41_rows."""
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
        found = rule_status.value_at(files[f], path)
        assert not (isinstance(found, dict) and found)


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


def test_r401_a_field_is_tied_by_its_own_key_to_the_entry_or_candidate_that_measures_it(tmp_path):
    root = make_repo(tmp_path)
    path = root / "rules" / "p.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    p1 = data["levelProfiles"]["p1"]
    p1.update(frontRoad={"surface": "roadLevel", "offsetM": 0.0}, height="perCluster", stepBands=[[0.2, 1], [99.0, 2]], steps="countRule")
    p1["keyRules"].update(rampFront={"rule": "roadLevel", "share": 0.7, "strong": False, "candidates": {"roadLevel": 0.7, "other": 0.1}},
                          entry={"rule": "roadLevel", "share": 0.088, "strong": False},
                          ffl={"rule": "perPiece", "share": 0.75, "strong": False, "candidates": {"perPiece": 0.75, "perCluster": 0.68}})
    p1["keyRules"]["steps"]["candidates"] = {"fromHeight": 0.8, "stepBands": 0.93}
    write_json(path, data)
    rule_status.write(root)
    rules = entries(root)["rules/p.json"]
    base = "levelProfiles.p1."
    assert rules[base + "frontRoad.offsetM"] == {"status": "MEASURED", "share": 0.088, "strength": "weak",
                                                 "measuredBy": base + "keyRules.entry", "tiedBy": "Q101"}
    assert rules[base + "frontRoad.surface"]["measuredBy"] == base + "keyRules.rampFront"
    assert rules[base + "frontRoad.surface"]["candidate"] == "roadLevel"
    assert rules[base + "height"] == {"status": "MEASURED", "share": 0.68, "strength": "weak", "measuredBy": base + "keyRules.ffl",
                                      "tiedBy": "Q101", "candidate": "perCluster",
                                      "note": "no code reads this field while keyRules holds ffl, a build takes its FFL from keyRules.ffl"}
    assert rules[base + "stepBands.1"] == {"status": "MEASURED", "share": 0.93, "strength": "strong", "measuredBy": base + "keyRules.steps",
                                           "tiedBy": "Q101", "candidate": "stepBands",
                                           "note": "no code reads this field while keyRules.steps codes another rule than stepBands"}
    assert rules[base + "steps"]["share"] == 0.8 and "no code reads" in rules[base + "steps"]["note"]


def test_r401_a_tied_field_no_build_reads_says_so_by_the_rule_keyrules_codes(tmp_path):
    root = make_repo(tmp_path)
    path = root / "rules" / "p.json"
    data = json.loads(path.read_text(encoding="utf-8"))
    p1 = data["levelProfiles"]["p1"]
    p1.update(height="perCluster", stepBands=[[0.2, 1], [99.0, 2]], steps="countRule")
    p1["keyRules"].update(ffl={"rule": "perPiece", "share": 0.75, "strong": False, "candidates": {"perPiece": 0.75, "perCluster": 0.68}})
    notes = {}
    for rule in ("stepsFromHeight", "stepBands"):
        p1["keyRules"]["steps"]["rule"] = rule
        write_json(path, data)
        rule_status.write(root)
        rules = entries(root)["rules/p.json"]
        notes[rule] = {field: rules["levelProfiles.p1." + field].get("note", "") for field in ("height", "stepBands.0", "steps")}
    # keyRules holds ffl, so a build takes its FFL from it and never reads height, and stepBands only under the rule stepBands
    assert "no code reads" in notes["stepsFromHeight"]["height"] and "no code reads" in notes["stepBands"]["height"]
    assert "no code reads" in notes["stepsFromHeight"]["stepBands.0"] and notes["stepBands"]["stepBands.0"] == ""
    assert "stepBands" not in notes["stepsFromHeight"]["steps"].split("from ", 1)[-1]


def test_r401_an_empty_object_and_the_choosers_of_a_bookkeeping_block_have_an_entry(tmp_path):
    root = make_repo(tmp_path)
    rule_status.write(root)
    block = entries(root)
    assert block["config/project.json"]["majority.keep"] == {"status": "UNKNOWN", "why": "null or empty"}
    assert block["config/project.json"]["majority.check.group"]["status"] == "UNKNOWN"
    assert "majority.check.file" not in block["config/project.json"]
    assert block["rules/p.json"]["levelProfiles.p1.job"]["status"] == "UNKNOWN"


def test_r401_cross_sections_leaves_the_status_block_out():
    import cross_sections
    profile = {"status": {"rules/p.json": {"curbs.curb_height": {"status": "UNKNOWN"}}}, "curbs": {"curb_height": 150}}
    assert cross_sections.profile_values(profile, ["curb"]) == {"curbs.curb_height": 150}


def test_r401_each_measured_entry_of_the_real_block_holds_the_share_of_the_profile():
    """A rewrite of keyRules with no --write fails here. Open and closed questions are left to rule_status.py --check."""
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    profile = json.loads((ROOT / settings["profile"]).read_text(encoding="utf-8"))
    strong = settings["majority"]["strongShare"]
    measured = [(f, path, e) for f, block in profile["status"].items() if f != "note" for path, e in block.items() if e["status"] == "MEASURED"]
    assert len(measured) >= 35
    for f, path, e in measured:
        entry = rule_status.value_at(profile, e["measuredBy"])
        share = entry["candidates"][e["candidate"]] if "candidate" in e else entry["share"]
        assert (e["share"], e["strength"]) == (share, "strong" if share >= strong else "weak"), (f, path)


# Q109 (Bader, 2026-10-28). The rows of tables 1 and 2 of the review list Bader accepted under Q104, the least it may read
REVIEW_ROWS = 41


def held(root):
    """Q109. rule_status.py --check run on root as the gate runs it, with its exit code and lines, the rows the review list of Q104 reads
    and the entries of the written block CHOSEN by Bader by Q104."""
    done = subprocess.run([sys.executable, str(ROOT / "python" / "rule_status.py"), str(root), "--check"], capture_output=True, text=True,
                          encoding="utf-8", env=dict(os.environ, PYTHONIOENCODING="utf-8"))
    settings = json.loads((Path(root) / "config" / "project.json").read_text(encoding="utf-8"))
    block = json.loads((Path(root) / settings["profile"]).read_text(encoding="utf-8")).get("status") or {}
    chosen = sum(1 for f, entries in block.items() if f != "note" for e in entries.values() if (e["status"], e.get("question")) == ("CHOSEN by Bader", "Q104"))
    return done.returncode, (done.stdout + done.stderr).splitlines(), len(rule_status.review_list(root)), chosen


def test_r401_q109_the_real_block_equals_a_fresh_run_and_the_review_list_reads_its_41_rows():
    """Q109. A records edit after --write, or a review list that reads short, fails the gate here, so the 41 values of Q104 never go back
    to their old status in silence."""
    code, lines, rows, chosen = held(ROOT)
    assert (code, lines[-1:]) == (0, ["entries whose evidence changed: 0"]), " | ".join(lines[:20])
    assert rows >= REVIEW_ROWS, "the review list of Q104 reads %d rows of %d" % (rows, REVIEW_ROWS)
    assert chosen >= REVIEW_ROWS, "the block holds %d values CHOSEN by Bader by Q104 of %d" % (chosen, REVIEW_ROWS)


def test_r401_q109_a_stale_block_or_a_review_list_that_reads_short_fails_the_hold(tmp_path):
    root = make_repo(tmp_path)
    review = root / "steps" / "investigations" / "status-review.md"
    review.parent.mkdir(parents=True)
    review.write_text(REVIEW, encoding="utf-8")
    rule_status.write(root)
    code, _, rows, chosen = held(root)
    assert (code, rows, chosen) == (0, 3, 3)
    # A records edit after --write leaves the block stale: Q003, which names analysis.endShare, leaves the closed questions
    closed = root / "steps" / "questions-closed.md"
    text = closed.read_text(encoding="utf-8")
    closed.write_text(text.replace("| Q003 | End | Yes (Bader): analysis.endShare stays | 2026-10-27 |\n", ""), encoding="utf-8")
    code, lines, _, _ = held(root)
    assert (code, lines[-1]) == (1, "entries whose evidence changed: 1") and lines[0].startswith("config/project.json analysis.endShare: ")
    closed.write_text(text, encoding="utf-8")
    # A heading of table 1 spelled otherwise drops its rows with no error: the list reads short and the block is stale
    review.write_text(REVIEW.replace("## 1 Values", "### 1 Values"), encoding="utf-8")
    code, lines, rows, chosen = held(root)
    assert (code, lines[-1], rows, chosen) == (1, "entries whose evidence changed: 1", 2, 3)


def test_r401_r363_the_block_check_rows_is_a_setting_with_no_entry_of_the_status_block():
    """Review 1 of PR 81: checkRows of the settings names the check type and the form of Past of the check table, settings of a check as
    valueStatus and addin.workbookRoles are, so the status block holds no entry of it."""
    assert "checkRows" in rule_status.BOOKKEEPING
    settings = json.loads((ROOT / "config" / "project.json").read_text(encoding="utf-8"))
    profile = json.loads((ROOT / settings["profile"]).read_text(encoding="utf-8"))
    assert "checkRows" in settings and [path for path in profile["status"]["config/project.json"] if path.split(".")[0] == "checkRows"] == []


def test_d13_the_open_questions_are_read_from_questions_open_not_from_baders_turn(tmp_path):
    # 4h-1 join item (D13): Bader's turn lists only the questions of the phase, so Q105 open in questions-open.md alone is open and Q005,
    # named in Bader's turn but with no text in questions-open.md, is not
    root = make_repo(tmp_path, open_text="Q105 Other.\n\nQ105. Other: is analysis.otherShare right.\n")
    (root / "steps" / "PROGRESS.md").write_text("# Progress\n\n## Bader's turn\n\nQ005 Tolerance.\n\n## Next\n", encoding="utf-8")
    rule_status.write(root)
    config = entries(root)["config/project.json"]
    assert config["analysis.otherShare"] == {"status": "RECOMMENDED", "question": "Q105", "where": "Q105"}
    assert rule_status.Audit(root).open == {"Q105"} | rule_status.HELD_OPEN and rule_status.HELD_OPEN == {"Q017"}
