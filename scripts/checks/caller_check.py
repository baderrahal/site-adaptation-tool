"""Each new function of the tool has a caller, and the PR lists it under Callers. dotnet/ and python/ only, CHOSEN by Code:
scripts/ is the checks themselves. Test code is left out, a test alone is not a caller. A method the host or a framework calls,
such as an Execute of Revit, says the word framework on its Callers line in place of a call in the repo. Found by pattern, not by
a compiler, so an unusual declaration can be missed.
"""
import re
import sys

import check_common

NAME = "caller check"
CSHARP = re.compile(r"^\+\s*(?:\[[^\]]*\]\s*)*(?:(?:public|internal|private|protected|static|async|virtual|sealed|extern|unsafe|partial)\s+)+"
                    r"(?!class\b|struct\b|interface\b|enum\b|record\b|delegate\b|event\b|const\b|new\b)"
                    r"[\w<>\[\],\.\?\s\(\)]*?\s(\w+)\s*(?:<[^>()]*>)?\s*\(")
PYTHON = re.compile(r"^\+\s*(?:async\s+)?def\s+(\w+)\s*\(")
REMOVED = re.compile(r"^-\s*(?:.*\s)?(?:def\s+)?(\w+)\s*(?:<[^>()]*>)?\s*\(")
LEFT_OUT_NAMES = {"Main", "main", "__init__", "Dispose", "ToString", "Equals", "GetHashCode"}


def is_test_path(path):
    lowered = path.lower()
    return "/tests/" in lowered or lowered.startswith("dotnet/siteadaptation.tests/") or lowered.split("/")[-1].startswith("test_")


def new_functions(diff):
    """(path, name) of each function a unified diff adds in tool code, when no line of that file loses one of that name."""
    found = {}
    removed = {}
    path = None
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            path = line[6:].strip()
            continue
        if line.startswith(("+++", "---")) or path is None or is_test_path(path):
            continue
        pattern = CSHARP if path.endswith(".cs") else PYTHON if path.endswith(".py") else None
        if pattern is None:
            continue
        if " override " in line:
            continue
        added = pattern.match(line)
        if added and added.group(1) not in LEFT_OUT_NAMES and not added.group(1).startswith("__"):
            found.setdefault((path, added.group(1)), True)
        gone = REMOVED.match(line)
        if gone:
            removed.setdefault(path, set()).add(gone.group(1))
    return [key for key in found if key[1] not in removed.get(key[0], set())]


def callers_section(body):
    match = re.search(r"^#+\s*Callers\s*$(.*?)(?=^#+\s|\Z)", body, flags=re.MULTILINE | re.DOTALL)
    return match.group(1) if match else ""


def failures_of(functions, body, call_count):
    section = callers_section(body)
    failures = []
    for path, name in functions:
        lines = [line for line in section.splitlines() if re.search(r"\b%s\b" % re.escape(name), line)]
        if not lines:
            failures.append("%s in %s is new and not listed under Callers in the PR" % (name, path))
        elif not any("framework" in line.lower() for line in lines) and call_count(name) < 1:
            failures.append("%s in %s has no call in tool code, a test alone is not a caller" % (name, path))
    return failures


def calls_in_repo(name):
    """Lines of tool code that use the name other than to declare it."""
    try:
        text = check_common.git("grep", "-w", "-n", "-e", name, "--", "dotnet", "python")
    except Exception:
        return 0
    count = 0
    for line in text.splitlines():
        path = line.split(":", 1)[0]
        if is_test_path(path):
            continue
        body = line.split(":", 2)[-1]
        if re.search(r"\bdef\s+%s\b" % re.escape(name), body):
            continue
        if re.search(r"(?:public|internal|private|protected|static)\s[^=(]*\b%s\s*(?:<[^>()]*>)?\s*\(" % re.escape(name), body):
            continue
        count += 1
    return count


def main(argv=None):
    event = check_common.pr_event()
    if event is None:
        check_common.say(NAME, "NOT RUN", "not a pull request")
        return 0
    diff = check_common.git("diff", "-U0", event["base"] + "..." + event["head"], "--", "dotnet", "python")
    functions = new_functions(diff)
    return check_common.finish(NAME, failures_of(functions, event["body"], calls_in_repo), "%d new functions, each with a caller listed and found" % len(functions))


if __name__ == "__main__":
    sys.exit(main())
