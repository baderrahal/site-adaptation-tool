"""R369. The second method of Phase 4, with json and math only and none of the build code.

For seeds 14 and 7 it draws up to 20 plots of the plan of a whole project build and works out again, from the step files of that build,
the rules profile, the override list and config, for each drawn plot:
- clusters (R354): the cluster the step hands names against the step clusters, its code, count, order and the index of the plot
- fronts (R355): the front edge length, the normal and the right hand, the four ramp corners at the depth, the ends on the face of the
  step faces, and the ramp outline of the plan against the corners
- hands (R357): the factor the hand rule names, the table of the unit class, the mirror flag of the table of keyRules for the file, and
  the link file and mirror flag of the step linkFrame and the plan
- levels (R359): the road at front, El @ Entry, the FFL of the piece, SSL, height, steps and Prototype-FFL by the rules profile, and the
  levelRule of the plan against the step
- overrides (R360): every entry of the list of the default sources against the outcome of the step, every applied field against the
  list, the values each applied field gives the plan, and the level chain of the plan after the overrides
- parameters (R361): the parameters of the plan against the step, linkName, and each parameter keyRules gives by its own rule
Each value is held with the tolerance of its kind in addin.parity.kinds and half a unit of the decimals addin.parity.steps gives it.
The report holds counts, plot numbers, field names and override row numbers only, never a coordinate or a level.
"""
import argparse
import json
import math
import os
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHECKS = ("clusters", "fronts", "hands", "levels", "overrides", "parameters")
STEPS = ("clusters", "faces", "fronts", "hands", "levels", "linkFrame", "overrides", "parameters")
STEP_FORMAT = ("siteAdaptationStep", 2)
PARAMETER = "parameter "
PROTOTYPE_FFL = "RRE_ZZ_SP_Prototype-FFL"
RVT_NAME = "RRE_ZZ_SP_RVT-Name"


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def load_steps(folder, side):
    """The step files of one side, each refused when it is of another format, step or side."""
    found = {}
    for step in STEPS:
        payload = load(Path(folder) / ("step_%s_%s.json" % (step, side)))
        if (payload.get("format"), payload.get("formatVersion")) != STEP_FORMAT:
            raise ValueError("step_%s_%s.json is not %s formatVersion %d (R369)" % (step, side, *STEP_FORMAT))
        if payload.get("step") != step or payload.get("side") != side:
            raise ValueError("step_%s_%s.json holds the step %s of the side %s (R369)" % (step, side, payload.get("step"), payload.get("side")))
        found[step] = payload["plots"]
    return found


def number_key(plot):
    text = str(plot)
    return (0, int(text), text) if text.isdigit() else (1, 0, text)


def draw(plots, seed, size):
    """Up to size plots by random.Random(seed).sample over the plot numbers sorted by number, given back in that order."""
    population = sorted(plots, key=number_key)
    return sorted(random.Random(seed).sample(population, min(size, len(population))), key=number_key)


def git_tree_above(path):
    """The first folder at or above path with a .git entry, as given and as resolved, else None."""
    for start in (Path(os.path.abspath(path)), Path(path).resolve()):
        for folder in (start, *start.parents):
            if os.path.lexists(folder / ".git"):
                return folder
    return None


def fixed(value, decimals):
    """Half up at the decimals, the rounding this route takes on its own."""
    return math.floor(value * 10 ** decimals + 0.5) / 10 ** decimals


def value(node):
    return node.get("value") if isinstance(node, dict) else None


def close(first, second, tolerance):
    if isinstance(first, (list, tuple)) or isinstance(second, (list, tuple)):
        return (isinstance(first, (list, tuple)) and isinstance(second, (list, tuple)) and len(first) == len(second)
                and all(close(a, b, tolerance) for a, b in zip(first, second)))
    if first is None or second is None or isinstance(first, bool) or isinstance(second, bool):
        return False
    return abs(first - second) <= tolerance


class Context:
    def __init__(self, plan, steps, settings, rule_file, override_list, profile_name):
        self.plan = {entry["plot"]: entry for entry in plan["plots"]}
        self.steps = steps
        parity = settings["addin"]["parity"]
        self.kinds, self.decimals = parity["kinds"], parity["steps"]
        majority = settings["majority"]
        self.sources = set(majority["overrideDefaultSources"])
        self.facade = majority["linkNameFacade"]
        profiles = rule_file["levelProfiles"]
        self.profile = profiles[profile_name or profiles["default"]]
        self.key = self.profile["keyRules"]
        self.ground = rule_file["levels"]["ground_ssl"] / 1000.0
        self.base_table = rule_file["steps"]["base_height"]
        self.riser = rule_file["steps"]["riser_added"] / 1000.0
        self.rows = {}
        for number, entry in enumerate(override_list["overrides"], start=1):
            if entry.get("source") in self.sources:
                self.rows.setdefault(str(entry["plot"]), []).append((number, entry))
        self.cluster_of = {}
        for key, entry in steps["clusters"].items():
            for plot in entry["written"]["plots"]:
                self.cluster_of.setdefault(plot, []).append((key, entry["written"]))

    def tol(self, step, field):
        spec = self.decimals[step][field]
        slack = 0.5 * 10.0 ** -spec["decimals"] if "decimals" in spec else 0.0
        return self.kinds[spec["kind"]] + slack

    def raw(self, step, plot):
        return (self.steps[step].get(plot) or {}).get("raw")

    def written(self, step, plot):
        return (self.steps[step].get(plot) or {}).get("written")

    def applied(self, plot):
        return set((self.raw("overrides", plot) or {}).get("applied") or [])

    def base(self, unit):
        rule = self.key.get("baseHeight") or {}
        table = rule.get("table") if rule.get("rule") == "typology" else None
        if table and unit in table:
            return float(table[unit])
        return self.base_table.get(unit, self.base_table["default"]) / 1000.0

    def slope(self, unit):
        table = self.profile["rampSlopePercent"]
        return table.get(unit, table["default"])

    def entry(self, plot):
        """El @ Entry unrounded from the road levels of the two front corners, the offset and the slope of the unit over the depth."""
        front, levels = self.raw("fronts", plot), self.written("levels", plot)
        if not front or not levels or front.get("roadLevelLeft") is None or front.get("roadLevelRight") is None or front.get("depth") is None:
            return None
        road = (front["roadLevelLeft"] + front["roadLevelRight"]) / 2.0
        return road + self.profile["frontRoad"]["offsetM"] + self.slope(levels["unit"]) / 100.0 * front["depth"]

    def steps_for(self, height, unit):
        rule = self.key["steps"]["rule"]
        if height is None:
            return None
        if rule == "stepsFromHeight":
            over = fixed((fixed(height, 4) - self.base(unit)) / self.riser, 4)
            return 1 + max(0, int(math.ceil(over - 0.5)))
        if rule == "stepBands":
            return next((count for limit, count in self.profile["stepBands"] if fixed(height, 4) < limit), None)
        return None

    def prototype_ffl(self, ffl):
        rule = self.key[PARAMETER + PROTOTYPE_FFL]["rule"]
        if ffl is None:
            return None
        millimetres = int(fixed(ffl * 1000, 0))
        if rule == "fflDownToTheCm":
            return "%.3f" % ((millimetres // 10) / 100.0)
        if rule == "ffl":
            return "%.3f" % (millimetres / 1000.0)
        return None


def check_clusters(plot, ctx):
    hand = ctx.raw("hands", plot)
    if hand is None or hand.get("unit") is None:
        return []
    found = ctx.cluster_of.get(plot, [])
    members = hand.get("cluster")
    # A villa keeps the class villa when a footprint cluster holds it (R357)
    unit_class = "villa" if hand.get("kind") == "villa" else "cluster" if found else "alone"
    rows = [("membership", (not members and not found) or (bool(members) and len(found) == 1)),
            ("unitClass", hand.get("unitClass") == unit_class)]
    if members and len(found) == 1:
        key, entry = found[0]
        # The step hands lists a cluster in row order and the step clusters in number order, so the two are held as sets
        rows += [("plots", sorted(members, key=number_key) == entry["plots"] and key == "|".join(entry["plots"])),
                 ("order", entry["plots"] == sorted(entry["plots"], key=number_key)),
                 ("code", entry["code"] == hand.get("clusterCode")),
                 ("count", hand.get("clusterUnits") == len(entry["plots"])),
                 ("index", plot in members and hand.get("clusterIndex") == members.index(plot) + 1)]
    return rows


def check_fronts(plot, ctx):
    front, face = ctx.raw("fronts", plot), ctx.raw("faces", plot)
    if not front or not front.get("left") or not front.get("right") or not front.get("normal"):
        return []
    left, right, normal, hand = front["left"], front["right"], front["normal"], front["rightHand"]
    along = [right[0] - left[0], right[1] - left[1]]
    length = math.hypot(*along)
    unit_tol = ctx.tol("fronts", "normal")
    rows = [("length", close(length, front["length"], ctx.tol("fronts", "length"))),
            ("normalUnit", close(math.hypot(*normal), 1.0, unit_tol)),
            ("rightHand", length > 0 and close(hand, [along[0] / length, along[1] / length], ctx.tol("fronts", "rightHand"))),
            ("square", close(normal[0] * hand[0] + normal[1] * hand[1], 0.0, unit_tol))]
    if face and face.get("centroid") and face.get("boundsMin"):
        middle = [(left[i] + right[i]) / 2.0 for i in range(2)]
        inward = (face["centroid"][0] - middle[0]) * normal[0] + (face["centroid"][1] - middle[1]) * normal[1]
        slack = ctx.tol("fronts", "left")
        on_face = all(face["boundsMin"][i] - slack <= point[i] <= face["boundsMax"][i] + slack for point in (left, right) for i in range(2))
        rows += [("inward", inward > 0), ("onFace", on_face)]
    corners = front.get("corners")
    if corners and front.get("depth") is not None:
        depth = front["depth"]
        aim = [left, right, [right[0] + depth * normal[0], right[1] + depth * normal[1]], [left[0] + depth * normal[0], left[1] + depth * normal[1]]]
        rows.append(("corners", close([c[:2] for c in corners], aim, ctx.tol("fronts", "corners"))))
        outline = value(((ctx.plan.get(plot) or {}).get("floors") or {}).get("ramp", {}).get("outline"))
        if outline:
            points = [p[:2] for loop in outline["loops"] for p in loop["points"]]
            written = [c[:2] for c in ctx.written("fronts", plot)["corners"]]
            rows.append(("planRampOutline", close(points, written, ctx.tol("fronts", "corners"))))
    return rows


def hand_table(hand, ctx):
    """The table the unit class names, a cluster unit whose file holds a table of hand alone and none of hand cluster taking hand alone."""
    unit_class, linked = hand.get("unitClass"), hand.get("linkFile")
    if unit_class is None:
        return None
    files = lambda name: (ctx.key.get("hand " + name) or {}).get("byFile") or {}
    if unit_class == "cluster" and linked not in files("cluster") and linked in files("alone"):
        return "alone"
    if unit_class == "cluster" or linked in files(unit_class):
        return unit_class
    return None


def check_hands(plot, ctx):
    hand = ctx.raw("hands", plot)
    if hand is None or hand.get("unit") is None:
        return []
    table = hand_table(hand, ctx)
    rows = [("handTable", table == hand.get("handTable"))]
    rule = hand.get("handRule")
    if rule is not None:
        rows.append(("factor", hand.get("factor") == hand.get(rule)))
    if table is not None:
        section = ctx.key["hand " + table]
        rows.append(("handRule", section["rule"] == rule))
        aim = (section.get("byFile", {}).get(hand.get("linkFile")) or {}).get(hand.get("factor"))
        rows.append(("table", aim == hand.get("mirrored")))
    frame = ctx.raw("linkFrame", plot)
    if frame and frame.get("prototypeFile") is not None:
        rows += [("linkFrameFile", frame["prototypeFile"] == hand.get("linkFile")), ("linkFrameMirrored", frame.get("mirrored") == hand.get("mirrored"))]
    entry = ctx.plan.get(plot)
    if entry:
        rows.append(("planFile", value(entry.get("prototypeFile")) == hand.get("linkFile")))
        if "mirrored" not in ctx.applied(plot):
            rows.append(("planMirrored", value(entry.get("mirrored")) == hand.get("mirrored")))
    return rows


def check_levels(plot, ctx):
    levels = ctx.written("levels", plot)
    if not levels or levels.get("ffl") is None:
        return []
    rows = []
    entry = ctx.entry(plot)
    front = ctx.raw("fronts", plot)
    if entry is not None:
        road = (front["roadLevelLeft"] + front["roadLevelRight"]) / 2.0
        rows += [("roadAtFront", close(road, levels["roadAtFront"], ctx.tol("levels", "roadAtFront"))),
                 ("entry", close(entry, levels["entry"], ctx.tol("levels", "entry")))]
    rule = ctx.key["ffl"]["rule"]
    members = (levels.get("piece") or [plot]) if rule == "oneFflPerPieceAtTheHighestEntry" else [plot] if rule == "ownEntryPlusBase" else None
    if members is not None:
        tops = [(ctx.entry(member), (ctx.written("levels", member) or {}).get("unit")) for member in members]
        if all(top is not None and unit is not None for top, unit in tops):
            # The FFL is rounded to the mm from El @ Entry as written at its decimals, never from the unrounded entry
            places = ctx.decimals["levels"]["entry"]["decimals"]
            top = max(fixed(found, places) + ctx.base(unit) for found, unit in tops)
            rows.append(("ffl", close(top, levels["ffl"], ctx.tol("levels", "ffl"))))
    rows += [("ssl", close(levels["ffl"] + ctx.ground, levels["ssl"], ctx.tol("levels", "ssl"))),
             ("height", close(levels["ffl"] - levels["entry"], levels["height"], ctx.tol("levels", "height"))),
             ("steps", ctx.steps_for(levels["height"], levels["unit"]) == levels["steps"])]
    text = ctx.prototype_ffl(levels["ffl"])
    if text is not None:
        rows.append(("prototypeFfl", (levels.get("parameters") or {}).get(PARAMETER + PROTOTYPE_FFL) == text))
    plan_rule = (ctx.plan.get(plot) or {}).get("levelRule")
    if plan_rule:
        rows += [("planEntry", close(plan_rule.get("entry"), levels["entry"], ctx.tol("levels", "entry"))),
                 ("planFfl", close(plan_rule.get("ffl"), levels["ffl"], ctx.tol("levels", "ffl"))),
                 ("planHeight", close(plan_rule.get("height"), levels["height"], ctx.tol("levels", "height"))),
                 ("planSteps", plan_rule.get("steps") == levels["steps"]),
                 ("planPrototypeFfl", plan_rule.get(PARAMETER + PROTOTYPE_FFL) == (levels.get("parameters") or {}).get(PARAMETER + PROTOTYPE_FFL))]
    return rows


def gap(entry):
    found = entry.get("value")
    return found.get("gapToRuleM") if isinstance(found, dict) else None


def applied_value(field, entry, plot, ctx):
    """Whether the plan holds the value the applied override gives, None for a field this route does not work out."""
    plan, frame = ctx.plan.get(plot) or {}, ctx.written("linkFrame", plot) or {}
    rule, levels = plan.get("levelRule") or {}, plan.get("levels") or {}
    move = gap(entry)
    if field == "entryM" and move is not None and rule.get("entry") is not None:
        return close(value(levels.get("entry")), rule["entry"] + move, ctx.tol("overrides", "entry"))
    if field == "heightM" and move is not None and rule.get("ffl") is not None:
        return close(value(levels.get("ffl")), rule["ffl"] + move, ctx.tol("overrides", "ffl"))
    if field == "mirrored":
        return value(plan.get("mirrored")) == entry.get("value")
    if field == "steps":
        return str(value(levels.get("steps"))) == str(entry.get("value"))
    if field == "rampType":
        types = ctx.profile["rampTypes"]
        return value(((plan.get("floors") or {}).get("ramp") or {}).get("type")) == types.get(str(entry.get("value")), entry.get("value"))
    if "mirrored" in ctx.applied(plot) or frame.get("rotationDeg") is None:
        return None
    if field == "rotationDeg" and move is not None:
        turned = (frame["rotationDeg"] + move) % 360.0
        held = value(plan.get("rotationDeg"))
        return held is not None and min(abs(turned - held), 360.0 - abs(turned - held)) <= ctx.tol("overrides", "rotationDeg")
    if field == "insert" and isinstance(move, list) and frame.get("insert"):
        # The gap goes on the insert as the rule writes it, and a mirrored link has its Y axis turned the other way
        angle = math.radians(frame["rotationDeg"])
        turn = -1.0 if frame.get("mirrored") else 1.0
        x_axis, y_axis = (math.cos(angle), math.sin(angle)), (-turn * math.sin(angle), turn * math.cos(angle))
        aim = [frame["insert"][i] + move[0] * x_axis[i] + move[1] * y_axis[i] for i in range(2)]
        return close((value(plan.get("insert")) or [])[:2], aim, ctx.tol("overrides", "insert"))
    return None


def parameter_value(name, plot, ctx):
    """The text keyRules gives the parameter by its own rule on the plan values after the overrides, None for a rule this route does
    not work out."""
    plan = ctx.plan.get(plot) or {}
    spec = ctx.key.get(PARAMETER + name) or {}
    rule, table = spec.get("rule"), spec.get("table") or {}
    unit, mirrored = value(plan.get("unit")), value(plan.get("mirrored"))
    steps = value((plan.get("levels") or {}).get("steps"))
    if rule == "plot":
        return plot
    if rule == "typology":
        return table.get(unit)
    if rule == "typologyMirrored":
        return table.get("%s %s" % (unit, mirrored))
    if rule == "all":
        return table.get("all")
    if rule == "steps":
        return None if steps is None else table.get(str(steps))
    if rule in ("fflDownToTheCm", "ffl"):
        return ctx.prototype_ffl(value((plan.get("levels") or {}).get("ffl")))
    if rule == "plotCodeStepsAndFacade":
        parameters = plan.get("parameters") or {}
        facade = [value(parameters.get(other)) for other in ctx.facade]
        return " | ".join([plot, str(unit), "None" if steps is None else str(steps)] + [str(text) for text in facade])
    return None


def check_overrides(plot, ctx):
    outcome = ctx.raw("overrides", plot)
    plan = ctx.plan.get(plot)
    if outcome is None or plan is None:
        return []
    # A written plot with no override of its own has applied null in the step, and the list must then hold none for it
    applied, left = set(outcome.get("applied") or []), outcome.get("notApplied") or {}
    entries = ctx.rows.get(plot, [])
    rows = [("outcome", entry["field"] in applied or entry["field"] in left, number) for number, entry in entries]
    listed = {entry["field"]: entry for number, entry in entries}
    rows += [("applied", field in listed) for field in sorted(applied)]
    for field in sorted(applied & set(listed)):
        entry = listed[field]
        if field.startswith(PARAMETER):
            name = field[len(PARAMETER):]
            held = value((plan.get("parameters") or {}).get(name))
            if isinstance(entry.get("value"), dict):
                base = parameter_value(name, plot, ctx)
                move = gap(entry)
                found = None if base is None or move is None else held == "%.3f" % (float(base) + move)
            else:
                found = held == entry.get("value")
            if found is not None:
                rows.append((field, found))
            continue
        found = applied_value(field, entry, plot, ctx)
        if found is not None:
            rows.append((field, found))
    levels, rule = plan.get("levels") or {}, plan.get("levelRule") or {}
    ffl, entry_level, height = (value(levels.get(name)) for name in ("ffl", "entry", "height"))
    if ffl is not None and entry_level is not None:
        rows += [("planSsl", close(value(levels.get("ssl")), ffl + ctx.ground, ctx.tol("overrides", "ssl"))),
                 ("planHeight", close(height, ffl - entry_level, ctx.tol("overrides", "height"))),
                 ("insertZ", close((value(plan.get("insert")) or [None, None, None])[2:3], [ffl], ctx.tol("overrides", "ffl")))]
        if "steps" not in applied:
            rows.append(("planSteps", ctx.steps_for(height, value(plan.get("unit"))) == value(levels.get("steps"))))
        if "entryM" not in applied and rule.get("entry") is not None:
            rows.append(("planEntry", close(entry_level, rule["entry"], ctx.tol("overrides", "entry"))))
        if "heightM" not in applied and rule.get("ffl") is not None:
            rows.append(("planFfl", close(ffl, rule["ffl"], ctx.tol("overrides", "ffl"))))
    frame = ctx.written("linkFrame", plot)
    if frame and frame.get("insert") and not ({"insert", "rotationDeg", "mirrored"} & applied):
        rows += [("planInsert", close((value(plan.get("insert")) or [])[:2], frame["insert"][:2], ctx.tol("linkFrame", "insert"))),
                 ("planRotation", close(value(plan.get("rotationDeg")), frame["rotationDeg"], ctx.tol("linkFrame", "rotationDeg")))]
    return rows


def check_parameters(plot, ctx):
    step, plan = ctx.written("parameters", plot), ctx.plan.get(plot)
    if step is None or plan is None:
        return []
    held = {name: value(node) for name, node in (plan.get("parameters") or {}).items()}
    sources = {name: node.get("source") for name, node in (plan.get("parameters") or {}).items()}
    rows = [("step", held == (step.get("parameters") or {})), ("sources", sources == (step.get("sources") or {})),
            ("linkName", value(plan.get("linkName")) == held.get(RVT_NAME) == step.get("linkName"))]
    for name in sorted(held):
        if sources.get(name) != "keyRules":
            continue
        aim = parameter_value(name, plot, ctx)
        if aim is not None:
            rows.append((name, held[name] == aim))
    return rows


RUNS = {"clusters": check_clusters, "fronts": check_fronts, "hands": check_hands, "levels": check_levels,
        "overrides": check_overrides, "parameters": check_parameters}


def tally(plots, ctx):
    checks = {}
    for name in CHECKS:
        found = {"plotsChecked": 0, "plotsNotChecked": 0, "match": 0, "mismatch": 0, "mismatches": []}
        for plot in plots:
            rows = RUNS[name](plot, ctx)
            found["plotsChecked" if rows else "plotsNotChecked"] += 1
            for row in rows:
                if row[1]:
                    found["match"] += 1
                    continue
                found["mismatch"] += 1
                miss = {"plot": plot, "field": row[0]}
                if len(row) > 2:
                    miss["row"] = row[2]
                found["mismatches"].append(miss)
        checks[name] = found
    return checks


def main(argv=None):
    parser = argparse.ArgumentParser(description="The second method of Phase 4 (R369).")
    parser.add_argument("plan", help="plan.json of a whole project build")
    parser.add_argument("--steps", required=True, help="the folder of its step_<step>_<side>.json files")
    parser.add_argument("--side", default="python")
    parser.add_argument("--settings", default=str(ROOT / "config" / "project.json"))
    parser.add_argument("--rules", default=str(ROOT / "rules" / "roshn-makkah.json"))
    parser.add_argument("--overrides", default=str(ROOT / "config" / "overrides" / "roshn-makkah.json"))
    parser.add_argument("--profile", help="the level profile, else levelProfiles.default of the rules profile")
    parser.add_argument("--seeds", default="14,7")
    parser.add_argument("--size", type=int, default=20)
    parser.add_argument("--out", required=True, help="the report file, outside any git work tree")
    args = parser.parse_args(argv)
    out = Path(args.out)
    tree = git_tree_above(out.parent)
    if tree is not None:
        print("phase4_check: the report goes outside any git work tree, %s lies in %s (R369)" % (out.resolve(), tree), file=sys.stderr)
        return 2
    plan = load(args.plan)
    ctx = Context(plan, load_steps(args.steps, args.side), load(args.settings), load(args.rules), load(args.overrides), args.profile)
    plots = [entry["plot"] for entry in plan["plots"]]
    result = {"rule": "R369", "planPlots": len(plots), "seeds": {}}
    failed = False
    for seed in [int(text) for text in args.seeds.split(",")]:
        drawn = draw(plots, seed, args.size)
        checks = tally(drawn, ctx)
        result["seeds"][str(seed)] = {"plots": drawn, "checks": checks}
        failed = failed or any(found["mismatch"] for found in checks.values())
        print("seed %d: %d plots drawn, %s" % (seed, len(drawn), ", ".join(
            "%s %d match %d mismatch" % (name, checks[name]["match"], checks[name]["mismatch"]) for name in CHECKS)))
    out.write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(out)
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
