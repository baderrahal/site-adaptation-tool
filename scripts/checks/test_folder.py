"""Runs pytest on an owned test folder. An empty folder reports NOT RUN, so it cannot read as a pass. Usage: test_folder.py <folder>."""
import subprocess
import sys

import check_common

NO_TESTS_COLLECTED = 5


def main(argv=None):
    folder = (argv or sys.argv[1:])[0]
    done = subprocess.run([sys.executable, "-m", "pytest", folder, "-q", "-p", "no:cacheprovider"], cwd=check_common.ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    print(done.stdout[-4000:])
    if done.returncode == NO_TESTS_COLLECTED:
        check_common.say(folder, "NOT RUN", "no tests in the folder yet")
        return 0
    last = done.stdout.strip().splitlines()[-1] if done.stdout.strip() else "no output"
    return check_common.finish(folder, [last] if done.returncode else [], last)


if __name__ == "__main__":
    sys.exit(main())
