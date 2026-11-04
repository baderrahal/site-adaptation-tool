"""Fails on any value tagged override or past with no entry in docs/signoff.md.

A value is tagged by a JSON key sourceTag or source. The tool does not write these tags on its plan yet (steps/truth-audit.md,
section 8 item 11), so this reads the JSON of the folders below and a plan outside the repo is not seen. Until the tags exist
this finds nothing, and the override list carries no tag at all, docs/signoff.md names it as not signed.
"""
import json
import re
import sys

import check_common

NAME = "source tags"
FOLDERS = ("samples", "tests/acceptance", "tests/golden", "steps/revit-results")
TAG_KEYS = ("sourceTag", "source")
NEEDS_SIGNOFF = {"override", "past", "past job", "past_job", "pastjob"}


def tagged_values(data, trail=""):
    """The JSON path of each value tagged override or past, inside any object or list."""
    if isinstance(data, dict):
        for key in TAG_KEYS:
            value = data.get(key)
            if isinstance(value, str) and value.strip().lower() in NEEDS_SIGNOFF:
                yield trail or "/"
        for key, value in data.items():
            yield from tagged_values(value, trail + "/" + str(key))
    elif isinstance(data, list):
        for index, value in enumerate(data):
            yield from tagged_values(value, trail + "/" + str(index))


def signed_entries(text):
    """The first cell of each table row of signoff.md that names who signed it and the date."""
    entries = set()
    for line in text.splitlines():
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if not line.lstrip().startswith("|") or len(cells) < 4 or cells[0] in {"Value", ""} or set(cells[0]) <= {"-"}:
            continue
        if cells[-2] and cells[-1] and re.match(r"^\d{4}-\d{2}-\d{2}$", cells[-1]):
            entries.add(cells[0].strip("`"))
    return entries


def unsigned(path_name, data, entries):
    found = []
    for trail in tagged_values(data):
        entry = path_name + "#" + trail
        if entry not in entries and path_name not in entries:
            found.append("%s is tagged override or past with no entry in docs/signoff.md" % entry)
    return found


def main(argv=None):
    entries = signed_entries((check_common.ROOT / "docs" / "signoff.md").read_text(encoding="utf-8"))
    failures = []
    scanned = 0
    tagged = 0
    for folder in FOLDERS:
        for path in sorted((check_common.ROOT / folder).rglob("*.json")):
            scanned += 1
            name = path.relative_to(check_common.ROOT).as_posix()
            try:
                data = json.loads(path.read_text(encoding="utf-8"))
            except ValueError as error:
                failures.append("%s does not read as JSON: %s" % (name, error))
                continue
            tagged += len(list(tagged_values(data)))
            failures += unsigned(name, data, entries)
    if tagged == 0 and not failures:
        check_common.say(NAME, "NOT RUN", "%d JSON files in %s, no value tagged override or past, and the plan carries no tags (truth audit item 11)" % (scanned, ", ".join(FOLDERS)))
        return 0
    return check_common.finish(NAME, failures, "%d JSON files scanned, %d tagged values, each signed" % (scanned, tagged))


if __name__ == "__main__":
    sys.exit(main())
