"""R640 to R654. The build columns of the scenario table and the second check of a plan, each broken on purpose as well."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "scenarios"))

import build_second_check as second  # noqa: E402
import scenario_tools as tools  # noqa: E402

PLAN = {
    "blocking": [], "issues": [{"plot": None, "line": "x"}],
    "plots": [
        {"number": "1", "values": {"villaType": {"value": "T1", "source": "computed"}, "ffl": {"value": None, "source": "missing"}, "steps": {"value": 3.0, "source": "rule", "rule": "M10"}}, "marked": ["ffl"]},
        {"number": "2", "values": {"villaType": {"value": "T2", "source": "person entered"}}, "marked": []},
    ],
}
PLOTS = {"plots": [{"number": "1", "villaType": "T1"}, {"number": "2", "villaType": "T2"}]}
RULES = {"rules": [{"id": "M10", "value": "T1 always have 3 steps"}]}


def test_counts_split_built_and_missing_values():
    count = tools.build_counts(PLAN, 4.5)
    assert (count["plots"], count["built"], count["missing"], count["marked"], count["issues"], count["past"], count["seconds"]) == (2, 3, 1, 1, 1, 0, 4.5)


def test_table_holds_built_and_missing_only_when_a_build_ran():
    assert "Missing" not in tools.table({"S1": {"needs": [], "score": {"read": 0, "right": 0, "numbers": 0, "villaType": 0, "mirror": 0}, "ran": "done", "faults": []}})
    text = tools.table({"S1": {"needs": [], "score": {"read": 0, "right": 0, "numbers": 0, "villaType": 0, "mirror": 0}, "ran": "done", "faults": [], "build": tools.build_counts(PLAN)}})
    assert "Built" in text and "Missing" in text and "| S1 | 2 | 3 | 1 | 1 | 0 | 1 | 0 | UNKNOWN |" in text


def test_second_check_finds_no_copied_value_in_a_good_plan():
    assert second.check(PLAN, PLOTS, RULES) == []


def test_second_check_names_a_value_that_is_not_the_plot_table():
    broken = {"plots": [dict(PLAN["plots"][0], values=dict(PLAN["plots"][0]["values"], villaType={"value": "T9", "source": "computed"}))]}
    assert any("differs from the plot table" in line for line in second.check(broken, PLOTS, RULES))


def test_second_check_names_a_rule_number_the_rule_text_does_not_hold():
    broken = {"plots": [dict(PLAN["plots"][0], values={"steps": {"value": 7.0, "source": "rule", "rule": "M10"}})]}
    assert any("whose text does not hold it" in line for line in second.check(broken, PLOTS, RULES))


def test_second_check_names_a_past_source_and_a_missing_value_with_no_mark():
    past = {"plots": [{"number": "1", "values": {"x": {"value": 1, "source": "past"}}, "marked": []}]}
    assert any("source past" in line for line in second.check(past, PLOTS))
    open_value = {"plots": [{"number": "1", "values": {"x": {"value": None, "source": "missing"}}, "marked": []}]}
    assert any("not marked" in line for line in second.check(open_value, PLOTS))
