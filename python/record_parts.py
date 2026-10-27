"""The records kept in parts since Round REC: the rules in docs/rules/, each part a run of whole lines of the old docs/rules.md in
its order. A reader that needs a whole record joins its parts in name order, which gives the old file byte for byte."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "docs" / "rules"
# The files of a parts folder that are no part of the record
NOT_PARTS = {"index.md", "README.md"}
RULE_TITLE = re.compile(r"^- \*\*(R\d+)( [^*]*)\*\*", re.MULTILINE)


def parts(folder):
    """The part files of a folder in the order they join."""
    return sorted(path for path in folder.glob("*.md") if path.name not in NOT_PARTS)


def text(folder):
    """The whole record of a parts folder."""
    return "".join(path.read_text(encoding="utf-8") for path in parts(folder))


def rule_titles(part_text):
    """The id and bold title of each rule of a part, in its order."""
    return [(match.group(1), match.group(1) + match.group(2)) for match in RULE_TITLE.finditer(part_text)]
