"""R540. Builds the help file of the add-in from steps/buttons-guide.md, so the pane and the guide never differ.

Run python/build_help.py after any change of the guide. python/tests/test_r540_help.py fails when the file is behind the guide.
Help text is shown to the team, so a file name ending in .json takes a plain name here (NEWPROJ, no text names a json file) and
a rule id or a settings key stops the build.
"""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GUIDE = ROOT / "steps" / "buttons-guide.md"
OUTPUT = ROOT / "dotnet" / "SiteAdaptation.AddIn" / "Help" / "help.json"

SECTIONS = {
    "What it does": "what",
    "Files it needs": "files",
    "Before it": "before",
    "What the user clicks": "click",
    "Limits today": "limits",
}
ROWS = {
    "Site drawing (CAD)": "site",
    "Plot list (Excel)": "plotList",
    "Villa levels (Excel)": "villaLevels",
    "Road levels (civil file)": "roadLevels",
    "Parameter sheet (Excel)": "parameterSheet",
}
PLAIN_NAMES = [
    (r"plan_<NBH>\.json", "the plan"),
    (r"review_<NBH>\.json", "the review file"),
    (r"read_plotTable\.json", "the plot table"),
    (r"project\.json", "the project settings file"),
    (r"roshn-makkah\.json", "the rules file"),
    (r"position_parsons\.json", "the position file"),
    (r"cad_layers\.json", "the layer list"),
    (r"floor_outlines\.json", "the floor outlines file"),
    (r"prototype_offsets\.json", "the prototype offsets file"),
    (r"site\.json", "the site file of step 1"),
    (r"read_baseplan, read_villaBlocks, read_curbs, read_workbooks", "The reads of step 2"),
]
LEADS = {"Where it comes from": "From: ", "Folder": "Folder: "}
CODE_NAMES = re.compile(r"\bR\d{3}\b|\.json\b|\baddin\.|\bcoordinates\.|\bdesignBasis\b|\bpositionFile\b|\brev6\b|\bread_[a-zA-Z]+")


class HelpError(Exception):
    """The guide does not give what the help file needs."""


def plain(text):
    text = text.replace("**", "").strip()
    for pattern, name in PLAIN_NAMES:
        text = re.sub(pattern, name, text)
    found = CODE_NAMES.search(text)
    if found:
        raise HelpError("code name %r in the guide line: %s" % (found.group(0), text))
    return text[:1].upper() + text[1:]


def cells(line):
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def is_rule(line):
    return bool(re.fullmatch(r"\|[-| ]+\|", line.strip()))


def parts_of(text):
    parts = {}
    name = None
    for line in text.splitlines():
        if line.startswith("## "):
            name = line[3:].strip()
            parts[name] = []
        elif name is not None:
            parts[name].append(line)
    return parts


def sections_of(lines):
    found = {}
    key = None
    for line in lines:
        head = re.match(r"\*\*(.+?)\.\*\*\s*(.*)$", line)
        if head:
            key = head.group(1)
            found[key] = [head.group(2)] if head.group(2) else []
        elif key is not None and line.strip():
            found[key].append(line.strip())
    return found


def table(lines):
    """The rows of the first table of the lines, each a list of cells, with its head apart."""
    rows = [cells(line) for line in lines if line.startswith("|") and not is_rule(line)]
    return (rows[0], rows[1:]) if rows else ([], [])


def file_lines(lines):
    head, rows = table(lines)
    if rows:
        return [plain(". ".join(LEADS.get(head[at], "") + row[at] for at in range(len(row)) if row[at])) for row in rows]
    return [plain(line) for line in lines]


def list_lines(lines):
    return [plain(re.sub(r"^(?:\d+\.|-)\s+", "", line)) for line in lines if not line.startswith("|")]


def entry(key, title, line, sections):
    missing = [name for name in SECTIONS if name not in sections]
    if missing:
        raise HelpError("%s has no %s in the guide" % (title, ", ".join(missing)))
    return {
        "key": key,
        "title": plain(title),
        "line": plain(line),
        "what": plain(" ".join(sections["What it does"])),
        "files": file_lines(sections["Files it needs"]),
        "before": plain(" ".join(sections["Before it"])),
        "click": list_lines(sections["What the user clicks"]),
        "limits": list_lines(sections["Limits today"]),
    }


def build(text):
    parts = parts_of(text)
    _, glance = table(parts["All buttons at a glance"])
    lines = {row[0]: row[1] for row in glance}
    entries = []
    for name, body in parts.items():
        number = re.match(r"([1-9]) ", name)
        if number:
            if name not in lines:
                raise HelpError("no row of the table for " + name)
            entries.append(entry("step" + number.group(1), name, lines[name], sections_of(body)))
    project = sections_of(parts["New project"])
    head, rows = table(project["Files it needs"])
    seen = {row[0]: row for row in rows}
    for title, key in ROWS.items():
        if title not in seen:
            raise HelpError("no row of New project for " + title)
        row = seen[title]
        sections = dict(project)
        sections["What it does"] = [row[1]]
        sections["Files it needs"] = ["%s: %s" % (head[at], row[at]) for at in range(2, len(head))]
        entries.append(entry(key, title, row[1], sections))
    return {"note": "R540. Built by python/build_help.py from steps/buttons-guide.md, do not edit by hand", "entries": entries}


def main():
    book = build(GUIDE.read_text(encoding="utf-8"))
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(book, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print("%d entries, %s" % (len(book["entries"]), OUTPUT.relative_to(ROOT)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
