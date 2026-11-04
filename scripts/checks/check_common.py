"""Shared by the checks of .github/workflows/tests.yml. A check says PASS, FAIL or NOT RUN, and only FAIL fails the job.

NOT RUN means there was nothing to score, it is never a pass. Each one prints and goes to the step summary, so a green job
with nothing scored can be seen on the PR.
"""
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def say(name, state, text):
    line = "%s %s: %s" % (state, name, text)
    print(line)
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as handle:
            handle.write("- " + line + "\n")


def finish(name, failures, passed_text):
    for failure in failures:
        say(name, "FAIL", failure)
    if not failures:
        say(name, "PASS", passed_text)
    return 1 if failures else 0


def pr_event(environ=None):
    """The body, labels and the two ends of the pull request of this run, or None off a pull request."""
    environ = os.environ if environ is None else environ
    path = environ.get("GITHUB_EVENT_PATH")
    if not path or not Path(path).is_file():
        return None
    pull = json.loads(Path(path).read_text(encoding="utf-8")).get("pull_request")
    if not pull:
        return None
    return {"body": pull.get("body") or "", "labels": [label["name"] for label in pull.get("labels", [])], "base": pull["base"]["sha"], "head": pull["head"]["sha"]}


def git(*args, root=ROOT):
    return subprocess.run(["git", *args], cwd=root, check=True, capture_output=True, text=True, encoding="utf-8").stdout


def changed_files(base, head, root=ROOT, status="ACDMRT"):
    return [line for line in git("diff", "--name-only", "--no-renames", "--diff-filter=" + status, base + "..." + head, root=root).splitlines() if line]


def task_of(body):
    """The id of the Task line of a PR body, or None."""
    found = re.search(r"^Task:\s*(\S+)\s*$", body, flags=re.MULTILINE)
    return found.group(1) if found else None


def read_task(text):
    """The head of a task file: id, lane, status, stage and the allowed list."""
    task = {"allowed": []}
    lines = text.splitlines()
    if not lines or lines[0].strip() != "---":
        return task
    in_allowed = False
    for line in lines[1:]:
        if line.strip() == "---":
            break
        item = re.match(r"^\s+-\s+(.+?)\s*$", line)
        if in_allowed and item:
            task["allowed"].append(item.group(1).strip("\"'").replace("\\", "/"))
            continue
        in_allowed = False
        pair = re.match(r"^(\w+):\s*(.*?)\s*$", line)
        if pair and pair.group(1) == "allowed":
            in_allowed = True
        elif pair:
            task[pair.group(1)] = pair.group(2)
    return task


def inside(path, allowed):
    item = allowed.rstrip("/")
    return path.lower() == item.lower() or path.lower().startswith(item.lower() + "/")
