"""Every rule has an id, a plain name of at most 6 words, input, output and test. Rules from before setup-lanes are listed in
rules_baseline.txt with a hash of their line and pass while that line stands, a changed line or a new rule meets the format.
--write-baseline writes the list and is for the setup round alone. --all also prints how many old rules are still to rewrite.
"""
import argparse
import hashlib
import re
import sys
from pathlib import Path

import check_common

NAME = "rules docs"
BASELINE = Path(__file__).with_name("rules_baseline.txt")
RULE = re.compile(r"^- \*\*R(\d+)\s+([^*]+?)\.?\*\*(.*)$")


def rules_in(root):
    """(id, name, whole line, file) of each rule in the parts of docs/rules, index.md left out."""
    found = []
    for path in sorted((root / "docs" / "rules").glob("*.md")):
        if path.name == "index.md":
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            match = RULE.match(line)
            if match:
                found.append(("R" + match.group(1), match.group(2).strip(), line, path.name))
    return found


def digest(line):
    return hashlib.sha256(line.strip().encode("utf-8")).hexdigest()[:16]


def format_faults(rule_id, name, line):
    faults = []
    if len(name.split()) > 6:
        faults.append("%s name has %d words, at most 6: %s" % (rule_id, len(name.split()), name))
    for word in ("Input:", "Output:", "Test:"):
        if word not in line:
            faults.append("%s has no %s" % (rule_id, word))
    return faults


def read_baseline(path=BASELINE):
    entries = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        parts = line.split()
        if len(parts) == 2 and parts[0].startswith("R"):
            entries.setdefault(parts[0], set()).add(parts[1])
    return entries


def check(rules, baseline):
    """The faults of new and changed rules, and the count of old rules that still stand as they were."""
    faults = []
    old = 0
    for rule_id, name, line, _ in rules:
        if digest(line) in baseline.get(rule_id, set()):
            old += 1
        else:
            faults += format_faults(rule_id, name, line)
    return faults, old


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--write-baseline", action="store_true")
    args = parser.parse_args(argv)
    rules = rules_in(check_common.ROOT)
    if args.write_baseline:
        BASELINE.write_text("Rules from before setup-lanes, id then hash of the line. A line leaves when its rule is rewritten.\n" + "".join("%s %s\n" % (rule[0], digest(rule[2])) for rule in rules), encoding="utf-8", newline="\n")
        print("wrote %d rules" % len(rules))
        return 0
    faults, old = check(rules, read_baseline())
    return check_common.finish(NAME, faults, "%d rules, %d new or changed meet the format, %d old rules still to rewrite" % (len(rules), len(rules) - old, old))


if __name__ == "__main__":
    sys.exit(main())
