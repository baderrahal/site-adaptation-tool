"""Scores the runs of run_stage1.py against each right answer, checks each manifest, and writes steps/scenarios.md. Counts and marks only."""
import json
import sys
from pathlib import Path

import scenario_tools as tools

ROOT = Path(__file__).resolve().parents[2]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8")) if path.is_file() else None


def result_of(name, root, run=None):
    out = root / "runs" / (run or name)
    answer = read_json(root / "answers" / (name + ".json"))
    check = read_json(out / "data_check.json")
    if answer is None or check is None:
        return None
    plots = read_json(out / "plots.json") or {"plots": []}
    profile = read_json(out / "project_profile.json") or {"levels": []}
    worker = read_json(out / "answer_stage1.json") or {}
    table = read_json(out / "read_plotTable.json") or {"plots": []}
    second = read_json(out / "second_check.json") or {}
    manifest = read_json(root / (name + "_manifest.json")) or {}
    # The build plan of a run is made from the plot table of that same run, so the confirmed run has its own
    built = root / "runs" / ((run or name) + tools.BUILD_RUN)
    plan = read_json(built / "build_plan.json")
    seconds = (read_json(built / "build_time.json") or {}).get("seconds")
    return {
        "build": tools.build_counts(plan, seconds) if plan else None, "listedApart": len(plots.get("otherPlots", [])),
        "ran": str(worker.get("status", "UNKNOWN")), "needs": check["needs"], "areas": tools.areas_read(table["plots"]), "sheetAreas": second.get("neighbourhoodsInSheet") or "not found", "notRead": sum(1 for row in check.get("files", []) if row.get("status") != "read"), "score": tools.score(answer, plots["plots"]),
        "faults": tools.manifest_faults(root / name, manifest), "levels": profile["levels"], "built": str((read_json(out / "built_from.json") or {}).get("commit", "UNKNOWN"))[:7],
    }


BEFORE_RUN = "_before"


def apart_line(results):
    """R730. How many plots each run listed apart as no villa goes on them, each with its reason in plots.json under otherPlots."""
    return "Plots listed apart, no villa goes on them: " + ", ".join("%s %s" % (name, (results.get(name) or {}).get("listedApart", "UNKNOWN")) for name in sorted(tools.SCENARIOS)) + "."


def before_section(before):
    """FAST-4. The runs of main before the change of a task, made by run_stage1.py --suffix _before, beside the runs above."""
    if not any(before.values()):
        return ""
    return "\n".join(["", "## Before the change, the same folders on main", "", "Each run named _before under the work root, made with a worker built from the commit in Built from. A scenario not run before shows not run.", ""]
                     + tools.score_rows(before) + ["", apart_line(before)]) + "\n"


def main(argv=None):
    root = tools.work_root() / "scenarios"
    results = {name: result_of(name, root) for name in tools.SCENARIOS}
    confirmed = {name: result_of(name, root, name + tools.CONFIRMED_RUN) for name in tools.SCENARIOS}
    target = ROOT / "steps" / "scenarios.md"
    before = {name: result_of(name, root, name + BEFORE_RUN) for name in tools.SCENARIOS}
    text = tools.table(results, confirmed).replace(tools.not_read_line(results) + "\n", tools.not_read_line(results) + "\n\n" + apart_line(results) + "\n", 1)
    target.write_text(text + before_section(before), encoding="utf-8", newline="\n")
    report = tools.lines_of(results) + "\n# With a confirmed profile\n" + tools.lines_of(confirmed)
    (root / "report.md").write_text(report, encoding="utf-8", newline="\n")
    print("wrote %s, %d and %d of %d scenarios scored" % (target.name, sum(1 for result in results.values() if result), sum(1 for result in confirmed.values() if result), len(results)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
