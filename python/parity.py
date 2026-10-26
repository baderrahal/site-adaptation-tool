"""R345. The parity harness of Round ADDIN Phase 4: the output of one plan builder step per plot, written by plan_from_book.py --steps
and by the C# of the worker, compared field by field.

A step file holds per plot the raw values of the step, unrounded, and the written values, as that side writes them, rounded by
R385 to the decimals of each field. Each field has a kind in addin.parity.steps of config/project.json, and the kind its tolerance
in addin.parity.kinds (docs/addin-design.md section 5): a field of no geometry is compared exactly. A field of geometry holds when
its raw values lie within the tolerance and its written values are equal, or one unit of the last place apart only where the raw
values lie within the tolerance and straddle a rounding boundary of R385, each such case listed by step, plot, field and both raw
values. Any other difference fails the step. The report goes outside the repo, real values stay out of git.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import common
import plot_relations
import project_profile

FORMAT = "siteAdaptationStep"
FORMAT_VERSION = 1
SIDES = ("python", "csharp")


def tolerances(settings):
    """Per step, per field (kind, tolerance before rounding or None, decimals or None), from addin.parity of the settings."""
    block = ((settings or {}).get("addin") or {}).get("parity")
    if not isinstance(block, dict) or not isinstance(block.get("kinds"), dict) or not isinstance(block.get("steps"), dict):
        raise project_profile.ProfileError("config holds no addin.parity block with kinds and steps (R345)")
    found = {}
    for step, fields in block["steps"].items():
        found[step] = {}
        for name, field in fields.items():
            kind = field.get("kind")
            if kind not in block["kinds"]:
                raise project_profile.ProfileError("addin.parity.steps.%s.%s names the kind %s, which addin.parity.kinds does not hold (R345)" % (step, name, kind))
            tolerance = block["kinds"][kind]
            if (tolerance is None) != ("decimals" not in field):
                raise project_profile.ProfileError("addin.parity.steps.%s.%s: a kind with a tolerance has decimals and a kind compared exactly has none (R345)" % (step, name))
            found[step][name] = (kind, tolerance, field.get("decimals"))
    return found


def written(raw, fields):
    """The values a side writes, each number of a field with decimals rounded by R385, every other value as it is."""
    out = {}
    for name, value in raw.items():
        decimals = (fields.get(name) or {}).get("decimals")
        if decimals is None:
            out[name] = value
        elif isinstance(value, list):
            out[name] = [common.fixed(item, decimals) if isinstance(item, (int, float)) and not isinstance(item, bool) else item for item in value]
        elif isinstance(value, (int, float)) and not isinstance(value, bool):
            out[name] = common.fixed(value, decimals)
        else:
            out[name] = value
    return out


def step_file(step, side, plots, settings):
    """The step file of one side: per plot its raw values and the values written by R385 with the decimals of addin.parity."""
    fields = ((settings.get("addin") or {}).get("parity") or {}).get("steps", {}).get(step)
    if fields is None:
        raise project_profile.ProfileError("addin.parity.steps names no step %s (R345)" % step)
    return {"format": FORMAT, "formatVersion": FORMAT_VERSION, "step": step, "side": side, "fields": fields,
            "plots": {number: {"raw": raw, "written": written(raw, fields)} for number, raw in plots.items()}}


def write_step(folder, step, side, plots, settings):
    """step_<step>.json in folder, the raw numbers as Python writes a float, so no digit is lost (never through common.write_json)."""
    folder = Path(folder)
    plot_relations.refuse_inside_repo(folder)
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / ("step_%s.json" % step)
    path.write_text(json.dumps(step_file(step, side, plots, settings), indent=1), encoding="utf-8")
    return path


def numbers_of(value):
    """The numbers of a value as a list with the index of each, None for a single number, or None for no value."""
    if value is None:
        return None
    if isinstance(value, list):
        return list(enumerate(value))
    return [(None, value)]


def one_unit(a, b, decimals):
    return math.isclose(abs(a - b), 10.0 ** -decimals, rel_tol=1e-9, abs_tol=1e-12)


def compare_field(plot, name, rule, mine, theirs, step, report):
    """One field of one plot: equal, a one unit case or a failure, counted in report."""
    kind, tolerance, decimals = rule
    raw_a, raw_b = mine["raw"].get(name), theirs["raw"].get(name)
    if raw_a is None and raw_b is None:
        return
    counts = report["fields"].setdefault(name, {"compared": 0, "equal": 0, "oneUnit": 0, "failed": 0})
    counts["compared"] += 1

    def fail(why, index=None, a=None, b=None):
        counts["failed"] += 1
        report["failures"].append({"step": step, "plot": plot, "field": name, "index": index, "why": why, "python": a, "csharp": b})

    if tolerance is None:
        if raw_a == raw_b:
            counts["equal"] += 1
        else:
            fail("not equal", None, raw_a, raw_b)
        return
    one, other = numbers_of(raw_a), numbers_of(raw_b)
    if one is None or other is None:
        return fail("one side has no value", None, raw_a, raw_b)
    if len(one) != len(other):
        return fail("not the same count of numbers", None, raw_a, raw_b)
    written_a, written_b = numbers_of(mine["written"].get(name)), numbers_of(theirs["written"].get(name))
    if written_a is None or written_b is None or len(written_a) != len(one) or len(written_b) != len(one):
        return fail("written values missing", None, mine["written"].get(name), theirs["written"].get(name))
    cases = []
    for (index, a), (_, b), (_, wa), (_, wb) in zip(one, other, written_a, written_b):
        if None in (a, b, wa, wb) or abs(a - b) > tolerance:
            return fail("before rounding", index, a, b)
        if wa == wb:
            continue
        if one_unit(wa, wb, decimals) and common.fixed(a, decimals) != common.fixed(b, decimals):
            cases.append({"step": step, "plot": plot, "field": name, "index": index, "python": a, "csharp": b, "writtenPython": wa, "writtenCsharp": wb})
            continue
        return fail("after rounding", index, wa, wb)
    if cases:
        counts["oneUnit"] += 1
        report["oneUnit"].extend(cases)
    else:
        counts["equal"] += 1


def compare(python, csharp, rules):
    """R345. The report of two step files of one step, python and csharp: counts per field, the one unit cases and the failures."""
    for side, payload in (("python", python), ("csharp", csharp)):
        if payload.get("format") != FORMAT or payload.get("formatVersion") != FORMAT_VERSION:
            raise ValueError("the %s step file is not %s formatVersion %d" % (side, FORMAT, FORMAT_VERSION))
    if python.get("step") != csharp.get("step"):
        raise ValueError("the two step files hold the steps %s and %s" % (python.get("step"), csharp.get("step")))
    step = python["step"]
    if step not in rules:
        raise ValueError("addin.parity names no step %s (R345)" % step)
    report = {"step": step, "plots": 0, "pythonPlots": len(python["plots"]), "csharpPlots": len(csharp["plots"]), "equal": 0,
              "fields": {}, "oneUnit": [], "failures": []}
    numbers = sorted(set(python["plots"]) | set(csharp["plots"]), key=lambda text: (len(text), text))
    report["plots"] = len(numbers)
    for plot in numbers:
        mine, theirs = python["plots"].get(plot), csharp["plots"].get(plot)
        if mine is None or theirs is None:
            report["failures"].append({"step": step, "plot": plot, "field": None, "index": None, "why": "only in " + ("csharp" if mine is None else "python"),
                                       "python": None, "csharp": None})
            continue
        for name, rule in rules[step].items():
            compare_field(plot, name, rule, mine, theirs, step, report)
    report["equal"] = sum(counts["equal"] for counts in report["fields"].values())
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the step file of plan_from_book.py --steps with the one of the worker (R345).")
    parser.add_argument("--python", required=True, help="step_<step>.json of plan_from_book.py --steps")
    parser.add_argument("--csharp", required=True, help="step_<step>.json of the worker")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--out", required=True, help="a folder outside the repo for parity_<step>.json")
    args = parser.parse_args(argv)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out)
    rules = tolerances(project_profile.load_settings(args.settings).values)
    python = json.loads(Path(args.python).read_text(encoding="utf-8"))
    csharp = json.loads(Path(args.csharp).read_text(encoding="utf-8"))
    report = compare(python, csharp, rules)
    report["sources"] = {"python": str(Path(args.python).resolve()), "csharp": str(Path(args.csharp).resolve())}
    out.mkdir(parents=True, exist_ok=True)
    (out / ("parity_%s.json" % report["step"])).write_text(json.dumps(report, indent=1), encoding="utf-8")
    print("step %s: plots %d (python %d, csharp %d), fields equal %d, one unit cases %d, failures %d" % (
        report["step"], report["plots"], report["pythonPlots"], report["csharpPlots"], report["equal"], len(report["oneUnit"]), len(report["failures"])))
    for name, counts in report["fields"].items():
        print("  %s %s" % (name, counts))
    return 0 if not report["failures"] else 1


if __name__ == "__main__":
    sys.exit(main())
