"""The records kept in parts since Round REC: the rules in docs/rules/, the design in docs/design/ and the round file of Round ADDIN
phases 0a to 4e in steps/history/round-addin/, each part a run of whole lines of the old file in its order. A reader that needs a
whole record joins its parts in name order, which gives the old file byte for byte."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "docs" / "rules"
DESIGN = ROOT / "docs" / "design"
ROUND_ADDIN = ROOT / "steps" / "history" / "round-addin"
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


KB = 1024
# R400. The caps of the start reads and of every record outside steps/history/
CLAUDE_MAX = 10 * KB
PROGRESS_MAX_LINES = 60
PROGRESS_MAX = 6 * KB
PROGRESS_MAX_LINE = 200
LOG_TOP_MAX = 4 * KB
RECORD_MAX = 100 * KB


def cap_breaches(root=ROOT):
    """R400. Each file of the repo at root over a cap, with its size and the cap, empty when every cap holds."""
    found = []
    claude = (root / "CLAUDE.md").read_bytes()
    if len(claude) > CLAUDE_MAX:
        found.append("CLAUDE.md %d bytes over %d" % (len(claude), CLAUDE_MAX))
    progress = (root / "steps" / "PROGRESS.md").read_bytes()
    lines = progress.decode("utf-8").splitlines()
    if len(lines) > PROGRESS_MAX_LINES:
        found.append("steps/PROGRESS.md %d lines over %d" % (len(lines), PROGRESS_MAX_LINES))
    if len(progress) > PROGRESS_MAX:
        found.append("steps/PROGRESS.md %d bytes over %d" % (len(progress), PROGRESS_MAX))
    found.extend("steps/PROGRESS.md line %d %d characters over %d" % (number, len(line), PROGRESS_MAX_LINE)
                 for number, line in enumerate(lines, start=1) if len(line) > PROGRESS_MAX_LINE)
    log = (root / "steps" / "log.md").read_text(encoding="utf-8")
    top = re.split(r"^(?=## )", log, flags=re.MULTILINE)[1].encode("utf-8")
    if len(top) > LOG_TOP_MAX:
        found.append("steps/log.md top entry %d bytes over %d" % (len(top), LOG_TOP_MAX))
    history = root / "steps" / "history"
    for folder in (root / "docs", root / "steps"):
        for path in sorted(folder.rglob("*")):
            if path.is_file() and history not in path.parents and path.stat().st_size > RECORD_MAX:
                found.append("%s %d bytes over %d" % (path.relative_to(root).as_posix(), path.stat().st_size, RECORD_MAX))
    return found
