"""Copies only samples/new-project to an empty folder and runs its build. Fails if the build fails or reads a past job path.

samples/new-project/build.json names the build: {"command": ["python/some_build.py", "{folder}"]}, a Python file of the repo
with its arguments. The build runs under open_watch.py, which logs every file Python opens. A C# build or a child process of the
build is not watched, a build.json that names anything but a Python file fails until that changes. NOT RUN while the folder holds no build.json, the tool cannot
run from raw inputs today (steps/truth-audit.md section 5).
"""
import fnmatch
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import check_common

NAME = "clean folder"
SAMPLE = check_common.ROOT / "samples" / "new-project"
PAST_LIST = Path(__file__).with_name("past_paths.txt")
WATCH = Path(__file__).with_name("open_watch.py")


def past_patterns(path=PAST_LIST):
    return [line.strip() for line in path.read_text(encoding="utf-8").splitlines() if line.strip() and not line.startswith("#")]


def past_reads(opened, patterns):
    """The opened paths a past job pattern matches, with forward slashes and no regard for case."""
    found = []
    for path in opened:
        text = path.replace("\\", "/").lower()
        if any(fnmatch.fnmatch(text, "*" + pattern.lower()) for pattern in patterns):
            found.append(path)
    return sorted(set(found))


def run(sample=SAMPLE, root=check_common.ROOT, patterns=None):
    """A list of failures, or None when there is no build to run."""
    config = sample / "build.json"
    if not config.is_file():
        return None
    patterns = past_patterns() if patterns is None else patterns
    command = json.loads(config.read_text(encoding="utf-8"))["command"]
    if not command or not str(command[0]).endswith(".py"):
        return ["build.json must name a Python file first, a C# build is not watched yet: %s" % command[:1]]
    with tempfile.TemporaryDirectory() as temp:
        folder = Path(temp) / "new-project"
        shutil.copytree(sample, folder)
        log = Path(temp) / "opened.txt"
        target = str(root / command[0])
        args = [str(item).replace("{folder}", str(folder)) for item in command[1:]]
        done = subprocess.run([sys.executable, str(WATCH), str(log), target, *args], cwd=temp, capture_output=True, text=True, encoding="utf-8", errors="replace")
        opened = log.read_text(encoding="utf-8").splitlines() if log.is_file() else []
    failures = []
    if done.returncode != 0:
        failures.append("the build exited %d: %s" % (done.returncode, done.stderr.strip()[-400:]))
    sample_reads = [path for path in opened if str(folder).replace("\\", "/").lower() in path.replace("\\", "/").lower()]
    if not sample_reads:
        failures.append("the build read no file of the sample, so it proves nothing")
    failures += ["the build read a past job path: " + path for path in past_reads(opened, patterns)]
    return failures


def main(argv=None):
    failures = run()
    if failures is None:
        check_common.say(NAME, "NOT RUN", "samples/new-project holds no build.json, the tool cannot run from raw inputs yet (truth audit section 5)")
        return 0
    return check_common.finish(NAME, failures, "the build ran from samples/new-project alone and read no past job path")


if __name__ == "__main__":
    sys.exit(main())
