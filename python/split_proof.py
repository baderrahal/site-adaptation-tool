"""Round REC. The proof that a record moved into parts lost nothing. For each split the SHA-256 of the original at the base commit
and of its parts joined in order, which must be equal, and the R and Q ids of every record file before and after, so an id that
left the records shows. Writes steps/runs/rec/split_proof.txt and exits 1 when a pair differs or an id is lost."""
import argparse
import hashlib
import re
import subprocess

import record_parts

ROOT = record_parts.ROOT
# The commit of main before Round REC moved anything
BASE = "73b8c42"
OUT = ROOT / "steps" / "runs" / "rec" / "split_proof.txt"
# Each original and the parts it was cut into, in the order they join. A part file added later, such as the rules of a later
# round, is no part of the split and is left out.
SPLITS = [
    ("docs/rules.md", ["docs/rules/01-reader-to-round-15a.md", "docs/rules/02-round-13-to-data-2.md", "docs/rules/03-round-z4-to-17.md",
                       "docs/rules/04-addin-r300-r327.md", "docs/rules/05-addin-r328-r344.md", "docs/rules/06-addin-r345-r353.md",
                       "docs/rules/07-addin-r354-r388.md"]),
]
# The record files whose ids are counted before and after: the docs, the steps, CLAUDE.md and the agents
RECORD_PREFIXES = ("docs/", "steps/", "CLAUDE.md", ".claude/agents/")
R_ID = re.compile(rb"\bR\d{1,3}\b")
Q_ID = re.compile(rb"\bQ\d{3}\b")


def git(*args):
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True).stdout


def base_records():
    """The bytes of each record file at the base commit."""
    names = git("ls-tree", "-r", "--name-only", BASE).decode("utf-8").splitlines()
    return {name: git("show", "%s:%s" % (BASE, name)) for name in names if name.startswith(RECORD_PREFIXES) and name.endswith(".md")}


def now_records():
    """The bytes of each record file in the working tree that git tracks or would add."""
    names = git("ls-files", "--cached", "--others", "--exclude-standard").decode("utf-8").splitlines()
    return {name: (ROOT / name).read_bytes() for name in names if name.startswith(RECORD_PREFIXES) and name.endswith(".md") and (ROOT / name).is_file()}


def ids(records, pattern):
    """Each id of the pattern and the number of times the records name it."""
    found = {}
    for data in records.values():
        for match in pattern.findall(data):
            found[match.decode()] = found.get(match.decode(), 0) + 1
    return found


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Prove that each record split of Round REC lost no byte and no id.")
    parser.parse_args(argv)
    lines = ["Round REC split proof, base %s. Each pair is the SHA-256 of the original and of its parts joined in order." % BASE, ""]
    ok = True
    for original, part_names in SPLITS:
        before = git("show", "%s:%s" % (BASE, original))
        after = b"".join((ROOT / name).read_bytes() for name in part_names)
        equal = before == after
        ok &= equal
        lines.append("%s: %d bytes, sha256 %s" % (original, len(before), sha(before)))
        lines.append("  %d parts joined: %d bytes, sha256 %s" % (len(part_names), len(after), sha(after)))
        for pattern, kind in ((R_ID, "R"), (Q_ID, "Q")):
            lines.append("  %s ids named: original %d, parts %d" % (kind, len(pattern.findall(before)), len(pattern.findall(after))))
        lines.append("  equal: %s" % ("yes" if equal else "NO"))
        lines.append("")
    before, after = base_records(), now_records()
    for pattern, kind in ((R_ID, "R"), (Q_ID, "Q")):
        old, new = ids(before, pattern), ids(after, pattern)
        lost = sorted(set(old) - set(new))
        ok &= not lost
        lines.append("%s ids of every record file (%s): before %d distinct, after %d distinct, lost %d %s, added %d %s" % (
            kind, ", ".join(RECORD_PREFIXES), len(old), len(new), len(lost), lost, len(set(new) - set(old)), sorted(set(new) - set(old))))
    lines.append("")
    lines.append("result: %s" % ("every pair equal, no id lost" if ok else "FAILED"))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print("\n".join(lines))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
