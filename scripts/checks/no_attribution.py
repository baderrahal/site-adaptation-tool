"""Fails if a commit message or the text of the PR holds a line that credits a tool. The words are the three the harness can add."""
import json
import os
import re
import sys
from pathlib import Path

import check_common

NAME = "no attribution"
WORDS = re.compile(r"co-authored-by|claude-session|generated with", re.IGNORECASE)


def failures_of(title, body, messages):
    failures = []
    for label, text in [("the PR title", title), ("the PR body", body)] + [("commit " + sha[:7], message) for sha, message in messages]:
        found = WORDS.search(text or "")
        if found:
            failures.append("%s holds %s" % (label, found.group(0)))
    return failures


def commits_of(base, head):
    """Each commit of the PR with its whole message. Log fields part on a byte no message holds."""
    text = check_common.git("log", "--format=%H%x01%B%x02", base + ".." + head)
    return [tuple(item.strip().split("\x01", 1)) for item in text.split("\x02") if "\x01" in item]


def main(argv=None):
    path = os.environ.get("GITHUB_EVENT_PATH")
    if not path or not Path(path).is_file():
        check_common.say(NAME, "NOT RUN", "not a pull request")
        return 0
    pull = json.loads(Path(path).read_text(encoding="utf-8"))["pull_request"]
    messages = commits_of(pull["base"]["sha"], pull["head"]["sha"])
    return check_common.finish(NAME, failures_of(pull.get("title"), pull.get("body"), messages), "%d commits and the PR text hold none" % len(messages))


if __name__ == "__main__":
    sys.exit(main())
