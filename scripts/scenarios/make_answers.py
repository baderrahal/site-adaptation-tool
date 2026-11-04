"""Lead only. Writes the right answers of the scenarios from the plans of the past models, which are for compare, never for a build, and sets the
two Actions secrets. Usage: make_answers.py --plans <folder> --book <plots.csv> --holdout <area> <area> --golden-from <area> <area>.

The areas are arguments, so this file names none. Answers of the held out areas and the golden levels leave this machine in the secrets alone,
nothing is kept on disk and no value is printed, only counts.
"""
import argparse
import csv
import json
import random
import subprocess
import sys
import tempfile
from pathlib import Path

import scenario_tools as tools

GOLDEN_COUNT = 30
GOLDEN_SEED = 20261104
LEVEL_FIELDS = ("pastFfl", "pastEntry", "pastSteps")


def set_secret(name, data):
    with tempfile.TemporaryDirectory() as temp:
        path = Path(temp) / "secret.json"
        path.write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
        size = path.stat().st_size
        if size > 48000:
            raise SystemExit("%s is %d bytes, an Actions secret holds at most 48 KB" % (name, size))
        with open(path, encoding="utf-8") as handle:
            subprocess.run(["gh", "secret", "set", name], stdin=handle, check=True, capture_output=True)
    print("secret %s set, %d bytes" % (name, size))


def golden_plots(book_rows, areas, count=GOLDEN_COUNT, seed=GOLDEN_SEED):
    """Plots whose level came from a schedule and not from a past plan, spread over villa types, drawn by a fixed seed so a rerun picks the same ones."""
    usable = [row for row in book_rows if row.get("neighbourhood") in areas and row.get("pastFfl") and str(row.get("pastSource", "")).startswith("schedule")]
    by_type = {}
    for row in sorted(usable, key=lambda row: row["officialNumber"]):
        by_type.setdefault(row.get("unitRev6"), []).append(row)
    draw = random.Random(seed)
    for rows in by_type.values():
        draw.shuffle(rows)
    picked = []
    while len(picked) < count and any(by_type.values()):
        for code in sorted(by_type, key=str):
            if by_type[code] and len(picked) < count:
                picked.append(by_type[code].pop())
    return picked


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--plans", required=True)
    parser.add_argument("--book", required=True)
    parser.add_argument("--holdout", nargs=2, required=True)
    parser.add_argument("--golden-from", nargs=2, required=True)
    args = parser.parse_args(argv)
    plans = [json.loads(path.read_text(encoding="utf-8")) for path in sorted(Path(args.plans).glob("*/plan_*_past.json"))]
    rows, twice = tools.answer_rows(plans)
    keep, held = tools.split_holdout(rows, set(args.holdout))
    if not held or not keep:
        raise SystemExit("no plot in the held out areas or none left, check the names")
    with open(args.book, encoding="utf-8-sig", newline="") as handle:
        book = list(csv.DictReader(handle))
    labels = sorted({row["baseplanLabel"] for row in book if row.get("baseplanLabel") and row.get("neighbourhood") not in args.holdout})
    folder = tools.work_root() / "scenarios" / "answers"
    folder.mkdir(parents=True, exist_ok=True)
    for name in tools.SCENARIOS:
        (folder / (name + ".json")).write_text(json.dumps(tools.scenario_answer(keep, name), indent=0), encoding="utf-8")
    (folder / "labels.json").write_text(json.dumps(labels), encoding="utf-8")
    set_secret("HOLDOUT_ANSWERS", {"passShare": 0.9, "answers": {number + "/" + field: row[field] for number, row in held.items() for field in ("villaType", "mirror")}})
    golden = golden_plots(book, set(args.golden_from))
    if len(golden) < GOLDEN_COUNT:
        raise SystemExit("only %d golden plots" % len(golden))
    set_secret("GOLDEN_ANSWERS", {"plots": {row["officialNumber"]: {field: row[field] for field in LEVEL_FIELDS} for row in golden}})
    print("plans %d, plots %d, twice %d, to tune on %d, held out %d, golden %d, villa types in golden %d" % (len(plans), len(rows), len(twice), len(keep), len(held), len(golden), len({row.get("unitRev6") for row in golden})))
    return 0


if __name__ == "__main__":
    sys.exit(main())
