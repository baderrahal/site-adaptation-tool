"""Runs pytest on an owned test folder. An empty folder reports NOT RUN, so it cannot read as a pass. Usage: test_folder.py <folder>."""
import subprocess
import sys
from pathlib import Path

import check_common

NO_TESTS_COLLECTED = 5


def main(argv=None):
    folder = (argv or sys.argv[1:])[0]
    # An absolute folder runs from itself, pytest stops when its folder lies on another drive than where it runs
    where = folder if Path(folder).is_absolute() else check_common.ROOT
    done = subprocess.run([sys.executable, "-m", "pytest", folder, "-q", "-p", "no:cacheprovider"], cwd=where, capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(done.stdout[-4000:])
    print(done.stderr[-2000:])
    if done.returncode == NO_TESTS_COLLECTED:
        check_common.say(folder, "NOT RUN", "no tests in the folder yet")
        return 0
    last = done.stdout.strip().splitlines()[-1] if done.stdout.strip() else "no output"
    if done.returncode == 0 and " passed" not in last:
        check_common.say(folder, "NOT RUN", "no test passed: " + last)
        return 0
    return check_common.finish(folder, [last] if done.returncode else [], last)


if __name__ == "__main__":
    sys.exit(main())
