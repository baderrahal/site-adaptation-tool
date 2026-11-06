"""A second check of a build plan that shares no code with siteBuild. It reads build_plan.json, plots.json and, when given, project_rules.json, and shows
that no value is copied: every value of the plan is the stage 1 value of its plot, a number taken from a rule appears in the text of that rule, and no value
has the source past. Usage: build_second_check.py <build folder> <stage 1 folder> [project_rules.json]. Counts only, exit 1 on a fault."""
import json
import re
import sys
from pathlib import Path

SOURCES = {"computed", "rule", "person entered", "default", "missing"}
STAGE1_FIELDS = {"villaType": "villaType", "mirror": "mirror", "area": "area", "areaM2": "areaM2"}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def check(plan, plots, rules=None):
    """The faults of a plan, each as a plain line. An empty list means no value is copied."""
    faults = []
    rows = {str(row.get("number")): row for row in plots.get("plots", [])}
    texts = {row.get("id"): str(row.get("value")) for row in (rules or {}).get("rules", [])}
    for plot in plan["plots"]:
        number = plot["number"]
        for name, value in plot["values"].items():
            if value["source"] not in SOURCES:
                faults.append("plot %s %s has the source %s" % (number, name, value["source"]))
            if value["source"] == "missing" and (value["value"] is not None or not plot["marked"]):
                faults.append("plot %s %s is missing but holds a value or the plot is not marked" % (number, name))
            if value["source"] == "rule":
                numbers = re.findall(r"\d+(?:\.\d+)?", texts.get(value.get("rule"), ""))
                if not any(abs(float(item) - value["value"]) < 1e-9 for item in numbers):
                    faults.append("plot %s %s takes %s from rule %s, whose text does not hold it" % (number, name, value["value"], value.get("rule")))
            if name in STAGE1_FIELDS and value["source"] != "missing" and number in rows and rows[number].get(STAGE1_FIELDS[name]) != value["value"]:
                faults.append("plot %s %s differs from the plot table" % (number, name))
    return faults


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    if len(args) not in (2, 3):
        print("usage: build_second_check.py <build folder> <stage 1 folder> [project_rules.json]")
        return 2
    faults = check(read(Path(args[0]) / "build_plan.json"), read(Path(args[1]) / "plots.json"), read(args[2]) if len(args) == 3 else None)
    print("%d faults" % len(faults))
    for line in faults[:20]:
        print(line)
    return 1 if faults else 0


if __name__ == "__main__":
    sys.exit(main())
