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
    return {
        "ran": str(worker.get("status", "UNKNOWN")), "needs": check["needs"], "areas": tools.areas_read(table["plots"]), "sheetAreas": second.get("neighbourhoodsInSheet", "UNKNOWN"), "notRead": sum(1 for row in check.get("files", []) if row.get("status") != "read"), "score": tools.score(answer, plots["plots"]),
        "faults": tools.manifest_faults(root / name, manifest), "levels": profile["levels"], "built": str((read_json(out / "built_from.json") or {}).get("commit", "UNKNOWN"))[:7],
    }


def main(argv=None):
    root = tools.work_root() / "scenarios"
    results = {name: result_of(name, root) for name in tools.SCENARIOS}
    confirmed = {name: result_of(name, root, name + tools.CONFIRMED_RUN) for name in tools.SCENARIOS}
    target = ROOT / "steps" / "scenarios.md"
    target.write_text(tools.table(results, confirmed), encoding="utf-8", newline="\n")
    report = tools.lines_of(results) + "\n# With a confirmed profile\n" + tools.lines_of(confirmed)
    (root / "report.md").write_text(report, encoding="utf-8", newline="\n")
    print("wrote %s, %d and %d of %d scenarios scored" % (target.name, sum(1 for result in results.values() if result), sum(1 for result in confirmed.values() if result), len(results)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
