"""R403: the block manualRules of rules/roshn-makkah.json is the table of docs/design/15-manual-rules.md, word for word."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DESIGN = ROOT / "docs" / "design" / "15-manual-rules.md"
PROFILE = ROOT / "rules" / "roshn-makkah.json"
HEADS = ["ID", "Rule", "Group", "Manual ref", "Checked in", "Past job", "Rev 06 section and page"]


def table_rows():
    rows = []
    for line in DESIGN.read_text(encoding="utf-8").replace("\r\n", "\n").split("\n"):
        if re.match(r"^\| M\d\d \|", line):
            rows.append([cell.strip() for cell in line.strip().strip("|").split("|")])
    return rows


def block():
    return json.loads(PROFILE.read_text(encoding="utf-8"))["manualRules"]


def test_r403_the_block_holds_m01_to_m15_in_the_order_and_words_of_the_design_table():
    rows = table_rows()
    rules = block()["rules"]
    assert [rule["id"] for rule in rules] == ["M%02d" % n for n in range(1, 16)]
    assert len(rows) == 15
    for row, rule in zip(rows, rules):
        assert rule["text"] == row[1], rule["id"]
        assert rule["group"] == row[2], rule["id"]
        assert rule["manualRef"] == row[3], rule["id"]
        assert ", ".join(rule["checkedIn"]) == row[4], rule["id"]
        assert rule["pastJob"] == row[5], rule["id"]
        assert rule["sectionPage"] == row[6], rule["id"]


def test_r403_the_pages_of_each_rule_are_those_the_design_read_from_the_pdf():
    # The page of every rule is carried from the table, which the Round FOLD read took from the PDF, so each has a printed page and a PDF page
    for rule in block()["rules"]:
        assert re.search(r"p\. \d+", rule["sectionPage"]) and "(PDF " in rule["sectionPage"], rule["id"]


def test_r403_each_value_of_a_rule_is_written_in_its_text():
    for rule in block()["rules"]:
        for value in rule["values"]:
            assert value in rule["text"], (rule["id"], value)


def test_r403_the_manual_is_named_with_its_revision_number_and_file_in_the_block_and_not_in_code():
    manual = block()["manual"]
    design = DESIGN.read_text(encoding="utf-8")
    assert manual["number"] in design and manual["name"] in design
    assert manual["revision"] == "Rev 06" and manual["olderRevision"] == "Rev 01"
    assert manual["file"].lower().endswith(".pdf")
    assert manual["folders"] and all(isinstance(item, str) for item in manual["folders"])
    assert manual["file"] in design


def test_r403_the_open_questions_keep_the_canvas_text_of_m14_and_m15():
    text = {rule["id"]: rule["text"] for rule in block()["rules"]}
    assert text["M14"].startswith("Irregular plots: boundary wall 200 mm inside the plot limit")
    assert text["M15"] == "Foundation at least 1.20 m below existing ground"


def test_r403_the_past_job_state_of_each_rule_follows_the_words_of_its_past_job_column():
    states = {"Not measured": "notMeasured", "Matches": "matches"}
    for rule in block()["rules"]:
        expected = states.get(rule["pastJob"], "differs" if rule["pastJob"].startswith("Differs") else None)
        assert rule["pastJobState"] == expected, rule["id"]
