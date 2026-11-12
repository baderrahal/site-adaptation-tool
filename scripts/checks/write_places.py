"""Write places of a test run. Every file a test or a script writes goes under the work root, Bader's rule of 12 Nov.

--save lists what stands on the drive roots, in Documents and in the repo outside git before the tests run. --check lists them again
after and fails on every new name that is not the work root or a build folder. The list is kept under the work root.
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

import check_common

sys.path.insert(0, str(check_common.ROOT / "scripts" / "scenarios"))
import scenario_tools  # noqa: E402

NAME = "write places"
# Folders a build or a test runner makes inside the repo, none holds a result of the tool
BUILD_FOLDERS = {".venv", "__pycache__", ".pytest_cache", "bin", "obj", ".vs", "TestResults"}


def names_in(folder):
    try:
        return sorted(item.name for item in Path(folder).iterdir())
    except OSError:
        return []


def repo_files(root):
    """Every file of the repo folder git does not hold, ignored or not, but for the build folders."""
    text = subprocess.run(["git", "status", "--porcelain", "--ignored", "--untracked-files=all"], cwd=root, check=True, capture_output=True, text=True, encoding="utf-8").stdout
    found = []
    for line in text.splitlines():
        if line[:2] not in ("??", "!!"):
            continue
        path = line[3:].strip().strip('"')
        if not BUILD_FOLDERS.intersection(path.rstrip("/").split("/")):
            found.append(path)
    return sorted(found)


def snapshot(drives, documents, root):
    """What stands in each watched place, as place and name."""
    rows = []
    for drive in drives:
        rows += ["%s %s" % (drive, name) for name in names_in(drive)]
    rows += ["%s %s" % (documents, name) for name in names_in(documents)]
    rows += ["repo %s" % path for path in repo_files(root)]
    return rows


def new_rows(before, after, work, drives, documents):
    """The rows of after that were not there before, less the folder the work root stands in."""
    allowed = set()
    for place in list(drives) + [documents]:
        try:
            allowed.add("%s %s" % (place, Path(work).relative_to(place).parts[0]))
        except (ValueError, IndexError):
            pass
    return [row for row in after if row not in set(before) and row not in allowed]


def watched(environ=None, home=None, root=check_common.ROOT):
    environ = os.environ if environ is None else environ
    drives = sorted({Path(root).anchor, environ.get("SystemDrive", "C:") + "\\"} if os.name == "nt" else {Path(root).anchor})
    return drives, str(Path(home if home is not None else Path.home()) / "Documents")


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--save", action="store_true")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    work = scenario_tools.work_root()
    drives, documents = watched()
    listing = work / "checks" / "write_places.json"
    if args.save:
        rows = snapshot(drives, documents, check_common.ROOT)
        listing.parent.mkdir(parents=True, exist_ok=True)
        listing.write_text(json.dumps(rows, indent=1), encoding="utf-8")
        check_common.say(NAME, "PASS", "listed %d names before the tests" % len(rows))
        return 0
    if not listing.is_file():
        check_common.say(NAME, "NOT RUN", "no list of before the tests, run with --save first")
        return 0
    before = json.loads(listing.read_text(encoding="utf-8"))
    new = new_rows(before, snapshot(drives, documents, check_common.ROOT), work, drives, documents)
    return check_common.finish(NAME, ["written outside the work root: " + row for row in new], "nothing new on %s, in Documents or in the repo outside the build folders" % ", ".join(drives))


if __name__ == "__main__":
    sys.exit(main())
