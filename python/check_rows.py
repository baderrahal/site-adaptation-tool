"""R363. The check rows of button 3, the reference of the check table of docs/design/07-mock.md 7.4, which CheckRows.cs of Common gives the
same. One row per plot and check of the sources a build holds: an override of the list for the plot, by what the build did with it, a plot
and field an open question of majority.decidedByQuestion decides, an entry of the cannot list of R272 and a key the plan lists as missing for
the plot. Each row holds its check type, its field, the Use it opens on, Past when the build applies the override, Skip on a plot of the skip
list, else Rule, and what Past writes in the form of the list by check and field (docs/design/03b-buttons-2-to-8.md 3 Check), all from the
block checkRows of the settings. The chip Assumed of Q108 groups the assumed values of the plan by rule, one row per rule and status with its
plots, only a DOCUMENT or strong MEASURED value left out (Q108, 7.7 item 8). The step checkRows of the parity harness, step 14,
holds per plot its rows and the tables assumed and chips.

python python/check_rows.py <plan.json> --plots-csv <plots.csv> [--overrides <list>] --out <folder outside the repo>
"""
import argparse
import csv
import json
import sys
from pathlib import Path

import link_summary
import plan_walls
import plot_relations
import project_profile
import value_status

STEP = "checkRows"
STEP_FIELDS = ("unit", "nbh", "rows")
# The sources of a row in the order a plot lists them within one check and field
FROM_ORDER = ("override", "question", "cannot", "missing")
# The labels of the mock (docs/addin-mock.md item 4): the Use dropdown, the first chip and the chip of 7.7 item 8
RULE, PAST, SKIP = "Rule", "Past", "Skip"
ALL, ASSUMED, FOLLOW = "All", "Assumed", "followEveryRule"
NO_CHECK = "checkRows.%s names no check for %s (R363)"
# Q108. The rule of every field and source valueStatus maps to no entry, one cause, and the head of a decidedByQuestion entry, grouped by question
NO_STATUS_RULE = "valueStatus holds no status for the field and its source (R401)"
DECIDED = "majority.decidedByQuestion."


def list_entries(data, sources, models=None):
    """The overrides and the cannot entries of the list whose source is one of sources, and with models only those of the past model files
    named, as plan_rules.load_overrides chooses the overrides a build applies."""
    def chosen(items):
        return [item for item in items or () if item.get("source") in sources and (not models or item.get("model") in models)]
    return chosen(data.get("overrides")), chosen(data.get("cannot"))


def entry_of(block, part, name):
    """The entry of checkRows.<part> of a field or missing key: its own, else that of its head, parameter * or levels.*, refused when none."""
    table = block.get(part) or {}
    if name in table:
        return table[name]
    for mark in (".", " "):
        if mark in name and name.split(mark, 1)[0] + mark + "*" in table:
            return table[name.split(mark, 1)[0] + mark + "*"]
    raise project_profile.ProfileError(NO_CHECK % (part, name))


def plot_infos(numbers, plan, rows, skip):
    """Per plot of the scope what the plan holds of it: its unit and NBH of plots.csv, its skip reason, the overrides the build applied and
    left with why, from its plan entry or its missing entry, and the keys of the missing list, each once in the order of the list."""
    written = {entry["plot"]: entry for entry in plan.get("plots") or ()}
    missing = {entry["plot"]: entry for entry in plan.get("missing") or ()}
    found = {}
    for number in numbers:
        row = rows.get(number) or {}
        record = (written.get(number) or {}).get("overrides") or (missing.get(number) or {}).get("overrides") or {}
        found[number] = {"unit": row.get("typologyRev9") or None, "nbh": row.get("nbhId") or None, "skip": skip.get(number),
                         "applied": list(record.get("applied") or ()), "notApplied": dict(record.get("notApplied") or {}),
                         "lacks": list(dict.fromkeys(item["key"] for item in (missing.get(number) or {}).get("missing") or ()))}
    return found


def past_of(item, value=None):
    return {"writes": item.get("past"), "value": value, "why": None if item.get("past") is not None else item.get("why")}


def plot_rows(numbers, infos, overrides, cannot, decided, block):
    """R363. The rows of each plot of the scope, ordered by the check types of the mock, then any other type by name, then field and source."""
    by_pair = {(str(item["plot"]), item["field"]): item for item in overrides}
    questions = {}
    for question, rule in (decided or {}).items():
        if isinstance(rule, dict):
            for plots in (rule.get("plots") or {}).values():
                for plot in plots or ():
                    for field in rule.get("fields") or ():
                        questions.setdefault(str(plot), []).append((field, question, rule.get("why")))
    misses = {}
    for item in cannot:
        misses.setdefault(str(item["plot"]), []).append(item)
    types = list(block["types"])
    found = {}
    for number in numbers:
        info = infos[number]
        skip = info.get("skip") is not None
        made = []
        applied = info.get("applied") or []
        left = info.get("notApplied") or {}
        for field in list(applied) + [name for name in left if name not in applied]:
            item = entry_of(block, "fields", field)
            entry = by_pair.get((number, field)) or {}
            made.append({"check": item["check"], "field": field, "from": "override", "use": SKIP if skip else PAST if field in applied else RULE,
                         "rule": entry.get("decision"), "toolValue": entry.get("rule"), "past": past_of(item, entry.get("value")), "why": left.get(field)})
        for field, question, why in questions.get(number, ()):
            item = entry_of(block, "fields", field)
            made.append({"check": item["check"], "field": field, "from": "question", "use": SKIP if skip else RULE, "rule": question, "toolValue": None,
                         "past": past_of(item), "why": why})
        for entry in misses.get(number, ()):
            item = entry_of(block, "fields", entry["field"])
            made.append({"check": item["check"], "field": entry["field"], "from": "cannot", "use": SKIP if skip else RULE, "rule": entry.get("decision"),
                         "toolValue": None, "past": {"writes": None, "value": None, "why": block["cannotWhy"]}, "why": entry.get("reason")})
        for key in info.get("lacks") or ():
            item = entry_of(block, "missing", key)
            made.append({"check": item["check"], "field": key, "from": "missing", "use": SKIP if skip else RULE, "rule": None, "toolValue": None,
                         "past": past_of(item), "why": None})
        made.sort(key=lambda found_row: (types.index(found_row["check"]) if found_row["check"] in types else len(types), found_row["check"], found_row["field"],
                                         FROM_ORDER.index(found_row["from"])))
        found[number] = made
    return found


def type_order(rows_by_plot, block):
    """The check types of the mock in its order, then any other type the rows hold by name."""
    types = list(block["types"])
    return types + sorted({item["check"] for made in rows_by_plot.values() for item in made} - set(types))


def chips(numbers, rows_by_plot, assumed, block, moved=()):
    """The filter chips of 7.4 and 7.7 item 8: All, the count per check type in the order of the mock, Assumed, the rows of the chip, and the
    plots of the scope with no row and no value an override set or moved, moved, which follow every rule. An Assumed row is no issue and counts
    in no other chip."""
    found = {ALL: sum(len(rows_by_plot[number]) for number in numbers)}
    for name in type_order(rows_by_plot, block):
        found[name] = sum(1 for number in numbers for item in rows_by_plot[number] if item["check"] == name)
    found[ASSUMED] = len(assumed)
    found[FOLLOW] = sum(1 for number in numbers if not rows_by_plot[number] and number not in moved)
    return found


def rule_name(entry):
    """Q108. The rule an entry of a detail names: a decidedByQuestion entry by its question, a block entry by its path, else its question or
    document, and the why of a field and source valueStatus maps to no entry by that one cause (breaker F5 of PR 81), else the why."""
    path = entry.get("entry")
    if path:
        dotted = path.split(" ", 1)[-1]
        return dotted.split(".")[2] if dotted.startswith(DECIDED) else path
    for key in ("question", "document"):
        if entry.get(key):
            return entry[key]
    why = entry.get("why")
    head, tail = value_status.NO_STATUS.split("%s")[0], value_status.NO_STATUS.split("%s")[-1]
    return NO_STATUS_RULE if why and why.startswith(head) and why.endswith(tail) else why


def rules_of(detail, worst):
    """Q108. The rules a value takes its status from, those of the entries of its detail that tie for its worst status by worstFirst, else of its
    first entry, each once (breaker F2 of PR 81)."""
    entries = detail.get("entries") or []
    return list(dict.fromkeys(rule_name(entry) for entry in value_status.worst_entries(entries, worst))) if entries else [None]


def assumed_rows(values, details, assumed, worst):
    """Q108. The rows of the chip Assumed: per rule and status the plots that hold a value of it, the count of the values and their fields. values
    holds (field, plots, status, statusFrom, source) per value. A DOCUMENT or strong MEASURED value is not shown, every other value counts under
    each rule its worst status ties on, a value an override set or moved under the rule of the override (Bader, Q108)."""
    grouped = {}
    for field, plots, status, key, _ in values:
        detail = details.get(key) or {}
        label = value_status.status_label(status, detail)
        if label not in assumed:
            continue
        for rule in rules_of(detail, worst):
            item = grouped.setdefault("%s | %s" % (rule, label), {"rule": rule, "status": label, "plots": set(), "values": 0, "fields": set()})
            item["plots"].update(plots)
            item["values"] += 1
            item["fields"].add(field)
    return {key: {"rule": item["rule"], "status": item["status"], "plotCount": len(item["plots"]), "plots": sorted(item["plots"], key=link_summary.sort_key),
                  "values": item["values"], "fields": sorted(item["fields"])} for key, item in sorted(grouped.items())}


def moved_plots(values, details):
    """The plots that hold a value an override set or moved, its walls included, which follow no rule whole (review 1 of PR 81)."""
    return {plot for _, plots, _, key, source in values if source == value_status.OVERRIDE or (details.get(key) or {}).get("override") for plot in plots}


def plan_values(plan, numbers):
    """The status of each value of the plan as (field, plots, status, statusFrom, source): each sourced value of a written plot of the scope, and
    each value of a wall on the written plots of the scope it stands on (R362)."""
    scope = set(numbers)
    found, written = [], set()
    for entry in plan.get("plots") or ():
        if entry["plot"] in scope:
            written.add(entry["plot"])
            found += [(field, [entry["plot"]], item["status"], item["statusFrom"], item["source"]) for field, item in value_status.fields_of(entry).items()]
    for wall in plan.get("walls") or ():
        plots = [plot for plot in wall["plots"]["value"] if plot in written]
        if plots:
            found += [("walls." + field, plots, wall[field]["status"], wall[field]["statusFrom"], wall[field]["source"]) for field in plan_walls.FIELDS]
    return found


def checks(numbers, rows, plan, data, sources, models, settings):
    """R363, Q108. The rows of each plot of the scope, the rows of the chip Assumed and the chips, from the plan a build wrote, plots.csv by
    plot and the override list data as the build took it."""
    block = settings.get("checkRows")
    overrides, cannot = list_entries(data or {}, sources, models)
    skip = {str(item["plot"]): item.get("reason") for item in (data or {}).get("skip") or ()}
    infos = plot_infos(numbers, plan, rows, skip)
    rows_by_plot = plot_rows(numbers, infos, overrides, cannot, (settings.get("majority") or {}).get("decidedByQuestion"), block)
    status = settings.get("valueStatus") or {}
    values, details = plan_values(plan, numbers), plan.get("statusFrom") or {}
    assumed = assumed_rows(values, details, status.get("assumed") or [], status.get("worstFirst") or [])
    return infos, rows_by_plot, assumed, chips(numbers, rows_by_plot, assumed, block, moved_plots(values, details))


def step(numbers, rows, plan, data, sources, models, settings):
    """R345, R363. The step checkRows of the parity harness: per plot of the scope its unit, NBH and rows, and the tables assumed and chips."""
    infos, rows_by_plot, assumed, counted = checks(numbers, rows, plan, data, sources, models, settings)
    return ({number: {"unit": infos[number]["unit"], "nbh": infos[number]["nbh"], "rows": rows_by_plot[number]} for number in numbers},
            {"assumed": assumed, "chips": counted})


def by_check_and_field(rows_by_plot):
    """The rows counted by check, field and Use, for the lines of main."""
    found = {}
    for made in rows_by_plot.values():
        for item in made:
            counts = found.setdefault(item["check"], {}).setdefault(item["field"], {})
            counts[item["use"]] = counts.get(item["use"], 0) + 1
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="The check rows of button 3 from a plan of plan_from_book.py (R363).")
    parser.add_argument("plan", help="plan.json of plan_from_book.py")
    parser.add_argument("--plots-csv", required=True, help="plots.csv of the snapshot the plan was built from")
    parser.add_argument("--overrides", help="the override list the plan was built with")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--out", required=True, help="a folder outside the repo for checks_<plan name>.json")
    args = parser.parse_args(argv)
    try:
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out)
        settings = project_profile.load_settings(args.settings)
        plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
        with open(args.plots_csv, encoding="utf-8-sig", newline="") as handle:
            rows = {row["locationPlanNumber"]: row for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
        rules = (plan.get("summary") or {}).get("rules") or {}
        numbers = sorted({entry["plot"] for entry in plan.get("plots") or ()} | {entry["plot"] for entry in plan.get("missing") or ()}, key=link_summary.sort_key)
        data = json.loads(Path(args.overrides).read_text(encoding="utf-8")) if args.overrides else {}
        infos, rows_by_plot, assumed, counted = checks(numbers, rows, plan, data, rules.get("overrideSource") or [], rules.get("overrideModel") or [], settings)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("check_rows: " + str(error), file=sys.stderr)
        return 2
    out.mkdir(parents=True, exist_ok=True)
    path = out / ("checks_%s.json" % Path(args.plan).stem)
    path.write_text(json.dumps({"plan": str(Path(args.plan).resolve()), "chips": counted, "assumed": assumed,
                                "plots": {number: {"unit": infos[number]["unit"], "nbh": infos[number]["nbh"], "rows": rows_by_plot[number]}
                                          for number in numbers if rows_by_plot[number]}}, indent=1), encoding="utf-8")
    print("chips %s (R363)" % counted)
    for check, fields in by_check_and_field(rows_by_plot).items():
        print("%s: %s" % (check, fields))
    print(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
