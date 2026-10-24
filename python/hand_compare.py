"""R282. The hands of a file built with the Prototypes_2024 files against a Native key that links the old prototype files.

The hand of a key link as built is its mirror flag, turned when hands.oldFiles of the rules profile lists the plot's code as drawn
the opposite hand from its old file. A plot is compared when the old file of the key lists that code as same or opposite,
else it is counted apart as another unit, since the two files have no common reference. Counts only are printed.
"""
import argparse
import re
import sys
from collections import Counter, defaultdict

import common
import link_summary
import past_all
import plan_from_past
import plot_relations
import project_profile

OLD_CODE = re.compile(r"-ARC-([A-Z]\d)-ZZ-MOD-")


def old_code(file_name):
    found = OLD_CODE.search(file_name or "")
    return found.group(1) if found else None


def as_built(key_mirrored, old, code, old_files):
    """The hand of a key link in the terms of the 2024 file of code, or None when its old file does not stand for that code."""
    entry = old_files.get(old) if old else None
    if entry is None or key_mirrored is None:
        return None
    if code in entry["same"]:
        return key_mirrored
    return not key_mirrored if code in entry["opposite"] else None


def compare(new_links, key_rows, old_files):
    """R282. Per plot of the built file with a key row: equal, differs or another unit, by code."""
    found, by_code = Counter(), defaultdict(Counter)
    rows = []
    for row in key_rows:
        plot = row["plot"]
        mine = new_links.get(plot)
        if mine is None or len(mine) != 1:
            continue
        code = (mine[0].get("parameters") or {}).get(plan_from_past.UNIT_PARAMETER)
        hand = as_built(row["mirrored"], old_code(row["linkFile"]), code, old_files)
        state = "anotherUnit" if hand is None else "equal" if hand == bool(mine[0]["transform"]["mirrored"]) else "differs"
        found[state] += 1
        by_code[code][state] += 1
        if state == "differs":
            rows.append({"plot": plot, "code": code, "oldFile": old_code(row["linkFile"])})
    return {"counts": dict(found), "byCode": {code: dict(value) for code, value in sorted(by_code.items(), key=lambda pair: str(pair[0]))}, "differs": rows}


def main(argv=None):
    parser = argparse.ArgumentParser(description="Hands of a built file against a Native key, as built (R282).")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--test", required=True, help="the reader output of the built file")
    parser.add_argument("--table", required=True, help="past_all.csv")
    parser.add_argument("--key", required=True, help="the key name of past_all.csv")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings(args.settings)))
    plot_relations.refuse_inside_repo(__import__("pathlib").Path(args.out).resolve().parent)
    old_files = {name: value for name, value in profile.get("hands.oldFiles").items() if name != "note"}
    document = plan_from_past.host_of(link_summary.load(args.test))
    links = defaultdict(list)
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
        if plot is not None:
            links[plot].append(link)
    rows = [row for row in past_all.read_table(args.table) if row["key"] == args.key]
    result = compare(links, rows, old_files)
    common.write_json(args.out, result)
    print("hands as built %s" % result["counts"])
    for code, counts in result["byCode"].items():
        print("  %s %s" % (code, counts))
    return 0


if __name__ == "__main__":
    sys.exit(main())
