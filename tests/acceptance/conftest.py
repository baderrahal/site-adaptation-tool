"""A test of tests/acceptance names its task with @pytest.mark.task("<id>"). While that task is open the test is owed: it may fail and the
job says xfailed, since the lead merges these before the lanes start and main requires job tests. The PR that sets status: done in the task
file makes the same test a hard one, so a task cannot close on a failing acceptance test.
"""
import re
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
sys.path.insert(0, str(ROOT / "scripts" / "checks"))


def task_status(task_id, root=ROOT):
    path = root / "steps" / "tasks" / (task_id + ".md")
    if not path.is_file():
        return "open"
    found = re.search(r"^status:\s*(\S+)\s*$", path.read_text(encoding="utf-8"), flags=re.MULTILINE)
    return found.group(1) if found else "open"


def pytest_configure(config):
    config.addinivalue_line("markers", "task(id): the task that owes this acceptance test")


def pytest_collection_modifyitems(config, items):
    for item in items:
        marker = item.get_closest_marker("task")
        if marker and task_status(marker.args[0]) != "done":
            item.add_marker(pytest.mark.xfail(reason="owed by the open task " + marker.args[0], strict=False))
