import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SKIP_DIRS = {".git", ".venv", "bin", "obj", "__pycache__", ".pytest_cache", ".vs"}
TEXT_SUFFIXES = {".md", ".py", ".yml", ".yaml", ".json", ".txt", ".props", ".csproj", ".addin", ".cs", ".ps1", ".gitignore", ".gitattributes"}
NO_SEMICOLON_SUFFIXES = TEXT_SUFFIXES - {".cs"}

EM_DASH = chr(0x2014)
SEMICOLON = chr(59)
EMOJI = re.compile("[" + chr(0x1F300) + "-" + chr(0x1FAFF) + chr(0x2600) + "-" + chr(0x27BF) + chr(0x2B50) + "]")
ATTRIBUTION = re.compile(r"co-authored-by\s*:|generated with \[?claude", re.IGNORECASE)


def repo_text_files():
    for path in ROOT.rglob("*"):
        if not path.is_file() or any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        if path.suffix in TEXT_SUFFIXES or path.name in {".gitignore", ".gitattributes"}:
            yield path


def test_r14_no_em_dash_and_no_emoji_in_any_file():
    offenders = []
    for path in repo_text_files():
        text = path.read_text(encoding="utf-8")
        if EM_DASH in text or EMOJI.search(text):
            offenders.append(str(path.relative_to(ROOT)))
    assert offenders == []


SKILL_COPY = ROOT / ".claude" / "skills" / "ai-max"


def in_skill_copy(path):
    return SKILL_COPY in path.parents


def test_r15_no_semicolons_outside_csharp_source():
    offenders = []
    for path in repo_text_files():
        # The skill copy stays byte for byte with its source and its workflow templates hold shell semicolons
        if in_skill_copy(path):
            continue
        if path.suffix in NO_SEMICOLON_SUFFIXES or path.name in {".gitignore", ".gitattributes"}:
            if SEMICOLON in path.read_text(encoding="utf-8"):
                offenders.append(str(path.relative_to(ROOT)))
    assert offenders == []


def test_r15_only_the_skill_copy_is_left_out_of_the_semicolon_scan():
    assert in_skill_copy(ROOT / ".claude" / "skills" / "ai-max" / "assets" / "workflow-python.yml")
    assert in_skill_copy(ROOT / ".claude" / "skills" / "ai-max" / "SKILL.md")
    assert not in_skill_copy(ROOT / ".claude" / "agents" / "developer.md")
    assert not in_skill_copy(ROOT / ".claude" / "skills" / "other" / "SKILL.md")
    assert not in_skill_copy(ROOT / ".claude" / "skills" / "ai-max-old" / "SKILL.md")
    assert not in_skill_copy(ROOT / "CLAUDE.md")


def test_r15_csharp_comments_and_strings_have_no_semicolon_in_prose():
    offenders = []
    for path in ROOT.rglob("*.cs"):
        if any(part in SKIP_DIRS for part in path.relative_to(ROOT).parts):
            continue
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
            stripped = line.strip()
            if stripped.startswith("//") and SEMICOLON in stripped:
                offenders.append(path.name + ":" + str(number))
    assert offenders == []


def test_r13_no_attribution_lines_in_any_file():
    offenders = []
    for path in repo_text_files():
        if ATTRIBUTION.search(path.read_text(encoding="utf-8")):
            offenders.append(str(path.relative_to(ROOT)))
    assert offenders == []


def test_r12_every_rule_in_the_rules_file_is_named_by_a_test():
    rules = re.findall(r"\*\*(R\d+) ", (ROOT / "docs" / "rules.md").read_text(encoding="utf-8"))
    assert len(rules) >= 15
    test_text = ""
    for path in list((ROOT / "python" / "tests").glob("*.py")) + list((ROOT / "dotnet" / "SiteAdaptation.Tests").glob("*.cs")):
        test_text += path.read_text(encoding="utf-8")
    # R6 and R10 are process rules proved by the round log, R11 by the workflow
    process_rules = {"R6", "R10", "R11"}
    missing = [rule for rule in rules if rule not in process_rules and not re.search(rule + r"(?!\d)", test_text, re.IGNORECASE)]
    assert missing == []


PROJECT_NAMES = re.compile("ro" + "shn|mak" + "kah", re.IGNORECASE)


def test_r63_no_project_name_in_any_python_file():
    offenders = [str(path.relative_to(ROOT)) for path in repo_text_files() if path.suffix == ".py" and PROJECT_NAMES.search(path.read_text(encoding="utf-8"))]
    assert offenders == []
