"""Scores the plots read and the plan built against the hold-out answers under the work root, counts only, never a value. Lead only.

CHOSEN by Lead: an Actions secret cannot be read back, and the runs cannot reach GitHub with no real value in git, so the answers stand in
scenarios\\holdout_answers, which a hook blocks every lane from reading. make_answers.py writes them. Usage: score_holdout.py --run <S1 run folder>.
The level columns are the shares within 5, 20 and 50 mm, only where a plan and an answer both hold a level.
"""
import argparse
import json
import sys
from pathlib import Path

import scenario_tools as tools

LIMITS_MM = (5, 20, 50)


def level_value(field):
    value = tools.plain_value(field)
    return value if isinstance(value, (int, float)) and not isinstance(value, bool) else None


def scored_levels(answers, plan_plots):
    """For each level name: how many plots both sides hold and how many are within each limit."""
    by_number = {str(row.get("number")): row.get("values", {}) for row in plan_plots}
    out = {}
    for number, answer in answers.items():
        values = by_number.get(number)
        if values is None:
            continue
        for name, right in answer.get("levels", {}).items():
            if right is None:
                continue
            got = level_value((values.get(name) or {}).get("value"))
            row = out.setdefault(name, {"held": 0, "built": 0, **{str(limit): 0 for limit in LIMITS_MM}})
            row["held"] += 1
            if got is None:
                continue
            row["built"] += 1
            for limit in LIMITS_MM:
                if abs(got - right) * 1000.0 <= limit:
                    row[str(limit)] += 1
    return out


def score_plots(answers, plots):
    by_number = {str(row.get("number")): row for row in plots}
    found = [by_number[number] for number in answers if number in by_number]
    right = {"held": len(answers), "numbers": len(found)}
    right["villaType"] = sum(1 for number, answer in answers.items() if number in by_number and by_number[number].get("villaType") == answer["villaType"])
    right["mirror"] = sum(1 for number, answer in answers.items() if number in by_number and by_number[number].get("mirror") is answer["mirror"])
    return right


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    args = parser.parse_args(argv)
    run = Path(args.run)
    answers = json.loads((tools.work_root() / "scenarios" / "holdout_answers" / "answers.json").read_text(encoding="utf-8"))
    plots = json.loads((run / "plots.json").read_text(encoding="utf-8"))["plots"]
    result = {"plots": score_plots(answers, plots)}
    plan = run / "build_plan.json"
    if plan.is_file():
        result["levels"] = scored_levels(answers, json.loads(plan.read_text(encoding="utf-8"))["plots"])
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
