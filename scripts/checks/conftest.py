import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent))

# test_folder.py is a check script, not a file of tests
collect_ignore = ["test_folder.py"]


@pytest.fixture(autouse=True)
def no_job_summary(monkeypatch):
    """A test that breaks a check on purpose must not write its FAIL line into the summary of the real job."""
    monkeypatch.delenv("GITHUB_STEP_SUMMARY", raising=False)
    monkeypatch.delenv("GITHUB_EVENT_PATH", raising=False)
