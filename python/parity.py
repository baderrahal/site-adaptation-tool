"""R345. The parity harness of Round ADDIN Phase 4: the output of one plan builder step per plot, written by plan_from_book.py --steps
and by the C# of the worker, compared field by field.

A step file, step_<step>_<side>.json, holds its side, its scope, the run stamp of the inputs it was made from with their SHA-256,
and per plot the raw values of the step, unrounded, and the written values, as that side writes them, never recomputed here. Each
field has a kind in addin.parity.steps of config/project.json, and the kind its tolerance in addin.parity.kinds (docs/addin-design.md
section 5). Every field of either side is compared: a field no step of addin.parity names has no tolerance and fails, a configured
field missing on a side fails, a field of no geometry is equal by value and type or fails, and a field of geometry holds when its
raw values lie within the tolerance number by number, of the same shape on both sides, and its written values are equal, or one
unit of the last place apart, decided on the decimals of the written texts, only where the raw values lie within the tolerance and
straddle a rounding boundary of R385, each such case listed by step, plot, field and index. Any other difference fails the step,
and so do no plot on either side. The report goes outside the repo, real values stay out of git.
"""
import argparse
import hashlib
import json
import sys
from decimal import Decimal
from pathlib import Path

import common
import plot_relations
import project_profile

FORMAT = "siteAdaptationStep"
# 2 since the review of PR 50: files named by side, the scope, the run stamp and every configured field on every plot
FORMAT_VERSION = 2
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


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def input_entry(role, path):
    """One input of the run stamp of a step file: its role, full path and SHA-256, as RunStamp of Common writes it."""
    return {"role": role, "path": str(Path(path).resolve()), "sha256": sha256(path)}


def step_name(step, side):
    return "step_%s_%s.json" % (step, side)


def step_file(step, side, scope, plots, settings, inputs, run_id=None):
    """The step file of one side. plots holds per plot raw and written as the side writes them, they are kept as given."""
    fields = ((settings.get("addin") or {}).get("parity") or {}).get("steps", {}).get(step)
    if fields is None:
        raise project_profile.ProfileError("addin.parity.steps names no step %s (R345)" % step)
    if side not in SIDES:
        raise ValueError("a step file is of the side python or csharp, not %s" % side)
    return {"format": FORMAT, "formatVersion": FORMAT_VERSION, "step": step, "side": side, "scope": scope,
            "run": {"runId": run_id, "inputs": inputs}, "fields": fields, "plots": plots}


def write_step(folder, step, side, scope, plots, settings, inputs):
    """step_<step>_<side>.json in folder, the raw numbers as Python writes a float, so no digit is lost (never through common.write_json)."""
    folder = Path(folder)
    plot_relations.refuse_inside_repo(folder)
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / step_name(step, side)
    path.write_text(json.dumps(step_file(step, side, scope, plots, settings, inputs), indent=1), encoding="utf-8")
    return path


def stamp_refusal(payload, side):
    """None when every input of the run stamp is on disk with its SHA-256, else why not (R347)."""
    inputs = (payload.get("run") or {}).get("inputs")
    if not isinstance(inputs, list):
        return "the %s step file holds no run stamp with the inputs it was made from (R347)" % side
    for item in inputs:
        path = Path(item.get("path") or "")
        if not item.get("path") or not path.is_file():
            return "the %s step file was written from the input %s %s, which is not on disk" % (side, item.get("role"), item.get("path"))
        if sha256(path) != str(item.get("sha256")).lower():
            return "the %s step file was written from the input %s %s, which changed since, its SHA-256 is not the one of the step file" % (side, item.get("role"), path)
    return None


def load_step(path, side):
    """A step file read back, refused when it is of another format or side, or an input it was made from changed or is gone."""
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    check_header(payload, side)
    refusal = stamp_refusal(payload, side)
    if refusal is not None:
        raise ValueError(refusal)
    return payload


def check_header(payload, side):
    if payload.get("format") != FORMAT or payload.get("formatVersion") != FORMAT_VERSION:
        raise ValueError("the %s step file is not %s formatVersion %d" % (side, FORMAT, FORMAT_VERSION))
    if payload.get("side") != side:
        raise ValueError("the %s step file holds the side %s, a parity compares the step files of the two sides" % (side, payload.get("side")))


def same_inputs(python, csharp):
    """An input role both step files name was the same file by content on both sides."""
    mine = {item.get("role"): item.get("sha256") for item in (python.get("run") or {}).get("inputs") or []}
    for item in (csharp.get("run") or {}).get("inputs") or []:
        role = item.get("role")
        if role in mine and str(mine[role]).lower() != str(item.get("sha256")).lower():
            raise ValueError("the two step files were written from other %s files, SHA-256 %s and %s" % (role, mine[role], item.get("sha256")))


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def shape(value):
    """The shape of a value of geometry: a number, or a list of the shapes of its items, None for anything else."""
    if is_number(value):
        return "number"
    if isinstance(value, list):
        inner = [shape(item) for item in value]
        return None if any(item is None for item in inner) else inner
    return None


def numbers(value, index=()):
    """Every number of a value of geometry with its index path, [] for a single number and [i, j] for a point of a list of points."""
    if is_number(value):
        return [(list(index) if index else None, value)]
    found = []
    for position, item in enumerate(value):
        found.extend(numbers(item, index + (position,)))
    return found


def same(a, b):
    """Equal by value and by type, so True is not 1 and 3 is not 3.0, through lists and objects."""
    if type(a) is not type(b):
        return False
    if isinstance(a, dict):
        return a.keys() == b.keys() and all(same(a[key], b[key]) for key in a)
    if isinstance(a, list):
        return len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    return a == b


def decimal_of(number):
    """The decimal of a written number as its shortest text reads, the text a side wrote at the decimals of R385."""
    return Decimal(repr(float(number))) if isinstance(number, float) else Decimal(number)


def one_unit(a, b, decimals):
    """Two written values one unit of the last place apart, on their decimals, so 2361234.124 and 2361234.125 are, though their
    binary difference is 0.0010000001639."""
    return abs(decimal_of(a) - decimal_of(b)) == Decimal(1).scaleb(-decimals)


def at_decimals(number, decimals):
    value = decimal_of(number)
    return value == value.quantize(Decimal(1).scaleb(-decimals)) if value.is_finite() else False


def compare_field(plot, name, rule, mine, theirs, step, report):
    """One field of one plot: equal, a one unit case or a failure, counted in report."""
    counts = report["fields"].setdefault(name, {"compared": 0, "equal": 0, "oneUnit": 0, "failed": 0})
    counts["compared"] += 1

    def fail(why, index=None, a=None, b=None):
        counts["failed"] += 1
        report["failures"].append({"step": step, "plot": plot, "field": name, "index": index, "why": why, "python": a, "csharp": b})

    if rule is None:
        return fail("no tolerance in addin.parity", None, mine["raw"].get(name), theirs["raw"].get(name))
    held = (name in mine["raw"], name in theirs["raw"])
    if held != (True, True):
        return fail({(False, False): "missing on both sides", (False, True): "missing on python", (True, False): "missing on csharp"}[held])
    if name not in mine["written"] or name not in theirs["written"]:
        return fail("written values missing", None, mine["written"].get(name), theirs["written"].get(name))
    kind, tolerance, decimals = rule
    raw_a, raw_b = mine["raw"][name], theirs["raw"][name]
    written_a, written_b = mine["written"][name], theirs["written"][name]
    if tolerance is None:
        if not same(raw_a, raw_b):
            return fail("not equal", None, raw_a, raw_b)
        if not same(written_a, written_b):
            return fail("written values not equal", None, written_a, written_b)
        counts["equal"] += 1
        return
    if raw_a is None or raw_b is None:
        if raw_a is None and raw_b is None and written_a is None and written_b is None:
            counts["equal"] += 1
            return
        return fail("one side has no value", None, raw_a, raw_b)
    shapes = [shape(value) for value in (raw_a, raw_b, written_a, written_b)]
    if None in shapes:
        return fail("not a number or a list of numbers", None, raw_a, raw_b)
    if shapes[0] != shapes[1]:
        return fail("not the same shape", None, raw_a, raw_b)
    if shapes[2] != shapes[0] or shapes[3] != shapes[1]:
        return fail("written values not of the shape of the raw values", None, written_a, written_b)
    cases = []
    for (index, a), (_, b), (_, wa), (_, wb) in zip(numbers(raw_a), numbers(raw_b), numbers(written_a), numbers(written_b)):
        if abs(a - b) > tolerance:
            return fail("before rounding", index, a, b)
        if not at_decimals(wa, decimals) or not at_decimals(wb, decimals):
            return fail("written value not at %d decimals" % decimals, index, wa, wb)
        if decimal_of(wa) == decimal_of(wb):
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
    check_header(python, "python")
    check_header(csharp, "csharp")
    if python.get("step") != csharp.get("step"):
        raise ValueError("the two step files hold the steps %s and %s" % (python.get("step"), csharp.get("step")))
    step = python["step"]
    if step not in rules:
        raise ValueError("addin.parity names no step %s (R345)" % step)
    report = {"step": step, "plots": 0, "pythonPlots": len(python["plots"]), "csharpPlots": len(csharp["plots"]), "equal": 0,
              "scopes": {"python": python.get("scope"), "csharp": csharp.get("scope")}, "fields": {}, "oneUnit": [], "failures": []}
    numbers_of_plots = sorted(set(python["plots"]) | set(csharp["plots"]), key=lambda text: (len(text), text))
    report["plots"] = len(numbers_of_plots)
    if not numbers_of_plots:
        report["failures"].append({"step": step, "plot": None, "field": None, "index": None, "python": None, "csharp": None,
                                   "why": "no plot on either side, scope python %s, csharp %s" % (python.get("scope"), csharp.get("scope"))})
    configured = list(rules[step])
    for plot in numbers_of_plots:
        mine, theirs = python["plots"].get(plot), csharp["plots"].get(plot)
        if mine is None or theirs is None:
            report["failures"].append({"step": step, "plot": plot, "field": None, "index": None, "why": "only in " + ("csharp" if mine is None else "python"),
                                       "python": None, "csharp": None})
            continue
        others = sorted((set(mine["raw"]) | set(theirs["raw"])) - set(configured))
        for name in configured + others:
            compare_field(plot, name, rules[step].get(name), mine, theirs, step, report)
    report["equal"] = sum(counts["equal"] for counts in report["fields"].values())
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Compare the step file of plan_from_book.py --steps with the one of the worker (R345).")
    parser.add_argument("--python", required=True, help="step_<step>_python.json of plan_from_book.py --steps")
    parser.add_argument("--csharp", required=True, help="step_<step>_csharp.json of the worker")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--out", required=True, help="a folder outside the repo for parity_<step>.json")
    args = parser.parse_args(argv)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out)
    if Path(args.python).resolve() == Path(args.csharp).resolve():
        raise ValueError("--python and --csharp name one file, a parity compares the step files of the two sides: %s" % Path(args.python).resolve())
    rules = tolerances(project_profile.load_settings(args.settings).values)
    python = load_step(args.python, "python")
    csharp = load_step(args.csharp, "csharp")
    same_inputs(python, csharp)
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
