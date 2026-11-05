"""Stands in for a person pressing Confirm in the window, for the scenario runs: takes what the tool itself proposed from the raw inputs of a run, its code map, its typology and area columns, its hand guesses and its level words, and writes them as the file the window would write. Nothing comes from a past file or an answer, so the score shows what a person gets who accepts every proposal. A proposal that is wrong stays wrong and scores so.

Usage: confirmed_profile.py --run <run folder> --out <file>.
"""
import argparse
import json
import sys
from pathlib import Path


def confirmed(profile):
    """The confirmed file from a project profile: every proposal taken as it stands, a hand or a word with no proposal left out."""
    code_map = [{"old": row["old"], "new": row["new"]} for row in (profile.get("codeMap") or {}).get("proposed", [])]
    hands = {code: hand for code, hand in ((profile.get("hands") or {}).get("proposed") or {}).items() if hand}
    levels = [{"level": row["level"], "word": row["word"]} for row in profile.get("levels", []) if row.get("word")]
    column = (profile.get("typologyColumn") or {}).get("proposed")
    data = {"codeMap": code_map, "hands": hands, "levels": levels}
    if column:
        data["typologyColumn"] = {"workbook": column.get("workbook"), "sheet": column["sheet"], "column": column["column"]}
    area = (profile.get("areaColumn") or {}).get("proposed")
    if area:
        data["areaColumn"] = {"workbook": area.get("workbook"), "sheet": area["sheet"], "column": area["column"]}
    return data


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--run", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    profile = json.loads((Path(args.run) / "project_profile.json").read_text(encoding="utf-8"))
    data = confirmed(profile)
    Path(args.out).write_text(json.dumps(data, indent=1), encoding="utf-8")
    print("confirmed %d code pairs, %d hands, %d words, %d typology columns, %d area columns" % (len(data["codeMap"]), len(data["hands"]), len(data["levels"]), 1 if "typologyColumn" in data else 0, 1 if "areaColumn" in data else 0))
    return 0


if __name__ == "__main__":
    sys.exit(main())
