"""R640 to R654. The build columns of the scenario table."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "scenarios"))

import scenario_tools as tools  # noqa: E402

PLAN = {
    "blocking": [], "issues": [{"plot": None, "line": "x"}],
    "plots": [
        {"number": "1", "values": {"villaType": {"value": "T1", "source": "computed"}, "ffl": {"value": None, "source": "missing"}, "steps": {"value": 3.0, "source": "rule", "rule": "M10"}}, "marked": ["ffl"]},
        {"number": "2", "values": {"villaType": {"value": "T2", "source": "person entered"}}, "marked": []},
    ],
}


def test_counts_split_built_and_missing_values():
    count = tools.build_counts(PLAN, 4.5)
    assert (count["plots"], count["built"], count["missing"], count["marked"], count["issues"], count["past"], count["seconds"]) == (2, 3, 1, 1, 1, 0, 4.5)


def test_table_holds_built_and_missing_only_when_a_build_ran():
    assert "Missing" not in tools.table({"S1": {"needs": [], "score": {"read": 0, "right": 0, "numbers": 0, "villaType": 0, "mirror": 0}, "ran": "done", "faults": []}})
    text = tools.table({"S1": {"needs": [], "score": {"read": 0, "right": 0, "numbers": 0, "villaType": 0, "mirror": 0}, "ran": "done", "faults": [], "build": tools.build_counts(PLAN)}})
    assert "Built" in text and "Missing" in text and "| S1 | 2 | 3 | 1 | 1 | 0 | 1 | 0 | UNKNOWN |" in text
