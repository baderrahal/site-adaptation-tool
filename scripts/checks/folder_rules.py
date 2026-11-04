"""Folder rules of a PR. The Edit and Write hook does not see a write made by a shell command, this check does.

A PR body names its Task, whose allowed folders hold every changed file, or says Lead with a reason. --all checks the root
files of the repo as they stand. A Lead line is only a word in a PR, the lane hooks are what hold a lane to its role.
"""
import argparse
import re
import subprocess
import sys

import check_common

NAME = "folder rules"
ROOT_FILES = {".gitattributes", ".gitignore", "CLAUDE.md", "README.md", "requirements.txt"}
LEAD_ONLY = ("tests/acceptance/", "tests/golden/", "tests/holdout/")


def pr_failures(body, changed, added, read_file):
    failures = []
    for path in added:
        if "/" not in path and path != "CLAUDE.md":
            failures.append("new file at the repo root: " + path)
    for path in changed:
        if path.lower() == "steps/status.md":
            failures.append("steps/STATUS.md is written by scripts/status.ps1 and never committed")
    task_id = check_common.task_of(body)
    if task_id is None:
        if not re.search(r"^Lead:[ \t]*\S+", body, flags=re.MULTILINE):
            failures.append("the PR body has no Task: <id> line and no Lead: <reason> line")
        return failures
    own = "steps/tasks/%s.md" % task_id
    text = read_file(own)
    if text is None:
        failures.append("the PR names task %s and %s is not there" % (task_id, own))
        return failures
    allowed = check_common.read_task(text)["allowed"]
    for path in changed:
        if path.startswith(LEAD_ONLY):
            failures.append("%s is owned by the lead, task %s may not change it" % (path, task_id))
        elif path != own and not any(check_common.inside(path, item) for item in allowed):
            failures.append("%s is outside the allowed folders of task %s" % (path, task_id))
    return failures


def base_reader(base, root=check_common.ROOT):
    """Reads a file as it stands on the base of the PR, so a task cannot widen its own allowed list in the PR that uses it."""
    def read_file(path):
        try:
            return check_common.git("show", base + ":" + path, root=root)
        except subprocess.CalledProcessError:
            return None
    return read_file


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--all", action="store_true", help="check the root files of the repo, no PR needed")
    args = parser.parse_args(argv)
    tracked = check_common.git("ls-files").splitlines()
    failures = ["file at the repo root outside the list: " + path for path in tracked if "/" not in path and path not in ROOT_FILES]
    failures += ["steps/STATUS.md is tracked by git" for path in tracked if path.lower() == "steps/status.md"]
    event = check_common.pr_event()
    if args.all or event is None:
        return check_common.finish(NAME, failures, "%d tracked files, root files on the list" % len(tracked))
    changed = check_common.changed_files(event["base"], event["head"])
    added = check_common.changed_files(event["base"], event["head"], status="A")
    failures += pr_failures(event["body"], changed, added, base_reader(event["base"]))
    return check_common.finish(NAME, failures, "%d changed files inside the rules" % len(changed))


if __name__ == "__main__":
    sys.exit(main())
