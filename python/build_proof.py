"""R337. The proof of a build by the rules of the keys: the plan plan_from_book.py builds for the plots of each answer key held field
by field against that key, every difference explained or listed.

covers writes one cover.json per key of majority.check.group from past_all.csv, the plots of that key in its neighbourhood, so
plan_from_book.py builds them by its own route, the lookups, the link file, the factors and the override placing of any build
(the run script of the round calls it once per key). compare holds each plan against the rows of its key in past_all.csv and
its key plan (R325), per plot and field: the prototype file, the mirror flag, the rotation within majority.rotationHoldDeg, the
insert within linkHoldMm, the ramp class by rampTypeClass, the ramp outline within outlineHoldMm, the generic floor on the
whole face by wholeFaceShare, the yard depth, the yard outline in link axes within outlineHoldMm and every link parameter of
parameterTests. R340: the levels as well, El @ Entry, the FFL, the height, the steps, the ramp front and the level parameters of
majority.parameterLevels, each held within majority.levelPassMm and its gap kept, so the unexplained levels are counted at the pass
limit and at each band of majority.levelBandsMm, SSL is in no key and not held. A field majority.kindLeaveOut leaves out for the kind
of the key is not held. Since the review of PR 48 compare needs the plan, key plan and key read of every key, a level it could not read
is notChecked and fails the run, one the key holds none of is keyHoldsNone, the front corners of the ramp are held apart from their
mean, and levelHolds says per key and level what holds by the rule alone and what by an override.

A difference is explained when majority.decidedByQuestion names the plot and field (question), the key links the file of the
code in the discipline majority.kindDiscipline names for its kind (kind), the list holds an override of that key, plot and field
the build did not place (override), or a parameter differs only by the steps the stepBands of the level profile give (laterPhase).
A plot the build does not write is explained only when it lacks its levels for want of a road level at a front corner (input,
R340), its levels of a later phase (laterPhase) or a link whose insert and rotation overrides hold no gap (override). Every other
difference is unexplained, counted per key and field and listed with its plot.
"""
import argparse
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely import wkt

import cad_links
import floor_fit
import key_rules
import link_summary
import majority
import override_source
import past_all
import plan_rules
import plot_relations
import project_profile
import ramp_levels
import ramp_outline

# The decision of majority.py that measures each field, so majority.kindLeaveOut leaves the same fields out
DECISION_OF = {"prototypeFile": None, "mirrored": "hand", "rotationDeg": "linkRotation", "insert": "linkOrigin", "rampType": "rampSlope",
               "rampOutline": "rampOutline", "genericFloor": "generic", "yard": "yard", "yardOutline": "yardOutline",
               "entry": "entry", "ffl": "ffl", "height": "ffl", "steps": "steps", "rampFront": "rampFront", "rampFrontCorner": "rampFront"}
# R340. A level the proof could not read, notChecked, fails the proof, one the key holds no value of, keyHoldsNone, is counted apart
NOT_CHECKED = "notChecked"
KEY_NONE = "keyHoldsNone"
# R386. A plot the skip list of the override list leaves out, by the key of plan_from_book.py, counted apart and never unexplained
SKIP = "skip"
SKIPPED = "skipped"
STATES = ("holds", "override", "kind", "question", "laterPhase", "input", "unexplained", NOT_CHECKED, KEY_NONE, SKIPPED)
# R340. The level fields held, and the override fields that move each, so a level an override the build did not place moves is explained
LEVEL_OVERRIDES = {"entry": ("entryM",), "ffl": ("heightM",), "height": ("heightM", "entryM"), "steps": ("steps",), "rampFront": ("rampFrontM",)}
# R340. The override fields of its own plot a level follows in a build, so a level that holds and the rule alone does not is held by one
LEVEL_MOVERS = {"entry": ("entryM",), "ffl": ("heightM",), "height": ("heightM", "entryM"), "steps": ("steps", "heightM", "entryM"), "rampFront": ("rampFrontM",),
                "rampFrontCorner": ("rampFrontM",)}
# The fields a build gives by the hand of the plot, besides the parameters whose rule reads the mirror flag
FOLLOW_HAND = ("rotationDeg", "insert", "yardOutline")
HAND_RULES = ("mirroredFactor", "typologyMirrored")
WRITTEN = "written"
EPSILON = 1e-9


def covers(rows, group):
    """R337. One cover per key of the group, the plots of its rows of past_all.csv in plot order with the neighbourhood they lie
    in. A key with plots in two neighbourhoods is refused, a cover names one."""
    found = defaultdict(lambda: {"nbh": set(), "plots": set()})
    for row in rows:
        if row.get("source") == group:
            found[row["key"]]["nbh"].add(row.get("nbh") or "")
            found[row["key"]]["plots"].add(str(row["plot"]))
    wrong = sorted(key for key, item in found.items() if len(item["nbh"]) != 1)
    if wrong:
        raise project_profile.ProfileError("keys %s hold plots of more than one neighbourhood, a cover names one (R337)" % ", ".join(wrong))
    return {key: {"neighbourhood": next(iter(item["nbh"])), "plots": sorted(item["plots"], key=link_summary.sort_key)} for key, item in sorted(found.items())}


def proof_context(block, names, key_rules, overrides, later):
    """R337. What compare needs: the majority block, prototypeNames of config, keyRules of the level profile, the overrides of the
    build by key, plot and field, and the fields majority.overrideKeep leaves to 3c. The plots and fields an open question
    decides come from majority.decidedByQuestion."""
    questions = {}
    for question, rule in (block.get("decidedByQuestion") or {}).items():
        if not isinstance(rule, dict):
            continue
        for key, plots in rule["plots"].items():
            for plot in plots:
                for field in rule["fields"]:
                    questions[(key, str(plot), field)] = question + ", " + rule["why"]
    later = set(later)
    # R340. Once keyRules codes the levels the steps are a rule of its own, no difference is left to the steps of a later phase
    stepped = set() if plan_rules.key_levels_of(key_rules) else plan_rules.step_parameters(key_rules or {}, later)
    return {"block": block, "names": names, "keyRules": key_rules or {}, "overrides": overrides, "later": later, "questions": questions,
            "disciplines": block.get("kindDiscipline") or {}, "stepped": stepped, "handed": hand_parameters(key_rules or {}, later),
            "levelParameters": ["parameter " + name for name in block.get("parameterLevels") or []]}


def hand_parameters(rules, later):
    """R337. The link parameters whose rule of keyRules reads the mirror flag, those of later left out."""
    return {word[len(plan_rules.PARAMETER):] for word, entry in rules.items()
            if word.startswith(plan_rules.PARAMETER) and word not in later and isinstance(entry, dict) and entry.get("rule") in HAND_RULES}


def value(found, name):
    found = (found or {}).get(name)
    return found.get("value") if isinstance(found, dict) else None


def loops_of(floor):
    return [loop["points"] for loop in (value(floor, "outline") or {}).get("loops") or []]


def in_link(floor, entry):
    """The outline of a floor in the link axes of the plot entry that holds it, with its own insert, rotation and mirror flag."""
    loops = loops_of(floor)
    insert, rotation, mirrored = value(entry, "insert"), value(entry, "rotationDeg"), value(entry, "mirrored")
    if not loops or insert is None or rotation is None or mirrored is None:
        return None
    return floor_fit.shape_of([floor_fit.to_link([point[:2] for point in loop], insert, rotation, mirrored) for loop in loops])


def key_class(row, block):
    """R273. The ramp class of the key: the class its type names, else the band of its measured slope, None with no ramp."""
    named = block["rampTypeClass"].get(row.get("rampType") or "")
    if named is not None:
        return named
    return None if row.get("rampSlopePercent") is None else majority.band(row["rampSlopePercent"], block["slopeBands"])


def built_depth(entry, lacks):
    """The yard depth the build gives, the FFL less the top of its yard floor at 3 decimals, noYard with none, None when the build
    lacks the yard it should give."""
    yard, ffl = (entry.get("floors") or {}).get("yard"), value(entry.get("levels"), "ffl")
    if yard is None:
        return None if "floors.yard" in lacks else "noYard"
    return None if ffl is None or value(yard, "z") is None else "%.3f" % (ffl - value(yard, "z"))


def held_fields(entry, row, keyed, lacks, block):
    """R337. Per field the key holds a value of: (holds, what differs). A field the key gives no value of is not held."""
    found = {}
    say = lambda built, key: "built %s, key %s" % (built, key)
    gap_text = lambda gap, unit: "no value built" if gap is None else "gap %.3f %s" % (gap, unit)
    if row.get("linkFile"):
        found["prototypeFile"] = (value(entry, "prototypeFile") == row["linkFile"], say(value(entry, "prototypeFile"), row["linkFile"]))
    if row.get("mirrored") is not None:
        found["mirrored"] = (value(entry, "mirrored") == row["mirrored"], say(value(entry, "mirrored"), row["mirrored"]))
    if row.get("rotationDeg") is not None:
        gap = None if value(entry, "rotationDeg") is None else cad_links.angle_gap(value(entry, "rotationDeg"), row["rotationDeg"])
        found["rotationDeg"] = (gap is not None and gap <= block["rotationHoldDeg"] + EPSILON, gap_text(gap, "deg"))
    if row.get("x") is not None:
        point = value(entry, "insert")
        gap = None if point is None else math.dist(point[:2], (row["x"], row["y"]))
        found["insert"] = (gap is not None and gap <= block["linkHoldMm"] / 1000.0 + EPSILON, gap_text(gap, "m"))
    floors = entry.get("floors") or {}
    aim = key_class(row, block) if (row.get("rampType") or row.get("rampSlopePercent") is not None) else None
    if aim is not None:
        kind = block["rampTypeClass"].get(value(floors.get("ramp"), "type") or "")
        found["rampType"] = (kind == aim, say(kind, aim))
    corners = json.loads(row["rampCorners"]) if isinstance(row.get("rampCorners"), str) and row["rampCorners"] else row.get("rampCorners")
    if corners and len(corners) == 4:
        built = value(floors.get("ramp"), "corners")
        gap = None if not built else ramp_outline.compare_outline([point[:2] for point in built], [point[:2] for point in corners])["corner"]
        found["rampOutline"] = (gap is not None and gap <= block["outlineHoldMm"] / 1000.0 + EPSILON, gap_text(gap, "m"))
    if row.get("genericOutline"):
        loops = loops_of(floors.get("generic"))
        shown = None
        if loops:
            built, key = floor_fit.shape_of([[point[:2] for point in loop] for loop in loops]), wkt.loads(row["genericOutline"])
            union = built.union(key).area
            shown = built.intersection(key).area / union if union else None
        found["genericFloor"] = (shown is not None and shown >= block["wholeFaceShare"], "no floor built" if shown is None else "share %.3f" % shown)
    aim = majority.yard_depth(row)
    if aim is not None:
        depth = built_depth(entry, lacks)
        found["yard"] = (depth == aim, say(depth, aim))
    key_yard = (keyed or {}).get("floors", {}).get("yard")
    if key_yard is not None and in_link(key_yard, keyed) is not None:
        built = floors.get("yard")
        gap = None if built is None or in_link(built, entry) is None else floor_fit.outline_gap(in_link(built, entry), in_link(key_yard, keyed))
        found["yardOutline"] = (gap is not None and gap <= block["outlineHoldMm"] / 1000.0 + EPSILON, gap_text(gap, "m"))
    parameters = json.loads(row.get("parameters") or "{}")
    for name in block["parameterTests"]:
        if parameters.get(name) in (None, ""):
            continue
        built = value(entry.get("parameters"), name)
        found["parameter " + name] = (built == str(parameters[name]), say(built, parameters[name]))
    return found


def front_corner_gap(built, key_corners):
    """R340. The largest gap in mm of the two front corners of a ramp to the nearest corner of the key ramp in plan, None with either
    missing. built holds the two front corners as (x, y, z)."""
    if not built or not key_corners or len(built) < 2:
        return None
    nearest = lambda point: min(key_corners, key=lambda corner: math.dist(point[:2], corner[:2]))
    return max(abs(point[2] - nearest(point)[2]) * 1000.0 for point in built[:2])


def held_levels(entry, row, read, block):
    """R340. Per level of the plot a verdict: holds within majority.levelPassMm, what differs, the gap in mm or None for a count or a
    level the build gives none of, and with levelRule of the plan whether the rule alone holds it. El @ Entry is the one R339 holds,
    the height the key FFL less it, the ramp front the mean of the two front corners of the ramp and rampFrontCorner the larger gap of
    the two corners to the nearest corner of the key ramp. A level with no aim is noAim: notChecked when the key read holds no link of
    the plot, so the proof could not read it, keyHoldsNone when the key holds no value of it."""
    found = {}
    limit = block["levelPassMm"]
    levels = entry.get("levels") or {}
    rule = entry.get("levelRule")
    raw_entry = row.get("entry")
    key_entry = majority.clean_entry(row, block["entryRampM"]) if raw_entry is not None else None
    no_entry = "the key holds no El @ Entry" if raw_entry is None else "the El @ Entry parameter of the key is off its own ramp (R270)"
    ramp = value((entry.get("floors") or {}).get("ramp"), "corners")
    front = None if not ramp or len(ramp) < 2 else (ramp[0][2] + ramp[1][2]) / 2.0
    key_corners = json.loads(row["rampCorners"]) if isinstance(row.get("rampCorners"), str) and row["rampCorners"] else row.get("rampCorners")
    rule_corners = None if rule is None or not ramp or rule.get("rampFrontCorners") is None else [list(point[:2]) + [z] for point, z in zip(ramp, rule["rampFrontCorners"])]
    # The rule front is the mean of the rule corners, taken the same way as the built front from the corners of the plan
    rule = None if rule is None else dict(rule, rampFront=None if rule_corners is None else (rule_corners[0][2] + rule_corners[1][2]) / 2.0)
    height_aim = None if key_entry is None or row.get("ffl") is None else round(row["ffl"] - key_entry, plan_rules.ENTRY_DECIMALS)
    pairs = {"entry": (value(levels, "entry"), key_entry, no_entry), "ffl": (value(levels, "ffl"), row.get("ffl"), "the key holds no FFL"),
             "height": (value(levels, "height"), height_aim, no_entry if key_entry is None else "the key holds no FFL"),
             "rampFront": (front, row.get("rampFront"), "the key holds no ramp front")}
    for name in block.get("parameterLevels") or []:
        pairs["parameter " + name] = (key_rules.number(value(entry.get("parameters"), name)), key_rules.number((read or {}).get(name)),
                                      "the key read holds no link of the plot" if read is None else "the link of the key holds no " + name)
    for field, (built, aim, why) in pairs.items():
        if aim is None:
            found[field] = {"noAim": NOT_CHECKED if field.startswith(plan_rules.PARAMETER) and read is None else KEY_NONE, "why": why}
            continue
        gap = None if built is None else abs(built - aim) * 1000.0
        by_rule = None if rule is None or key_rules.number(rule.get(field)) is None else abs(key_rules.number(rule[field]) - aim) * 1000.0 <= limit + 1e-6
        found[field] = {"holds": gap is not None and gap <= limit + 1e-6, "detail": "no value built" if gap is None else "gap %.2f mm" % gap, "gap": gap,
                        "byRule": by_rule, "atTheLimit": gap is not None and limit - 1e-6 < gap <= limit + 1e-6}
    if row.get("rampFront") is not None:
        gap = front_corner_gap(ramp, key_corners)
        rule_gap = front_corner_gap(rule_corners, key_corners)
        found["rampFrontCorner"] = ({"noAim": KEY_NONE, "why": "the key ramp holds no four corners"} if not key_corners else
                                    {"holds": gap is not None and gap <= limit + 1e-6, "detail": "no value built" if gap is None else "corner gap %.2f mm" % gap,
                                     "gap": gap, "byRule": None if rule_gap is None else rule_gap <= limit + 1e-6,
                                     "atTheLimit": gap is not None and limit - 1e-6 < gap <= limit + 1e-6})
    if row.get("steps") not in (None, ""):
        built, aim = value(levels, "steps"), int(float(row["steps"]))
        found["steps"] = {"holds": built is not None and int(built) == aim, "detail": "built %s, key %s" % (built, aim), "gap": None,
                          "byRule": None if rule is None or rule.get("steps") is None else int(rule["steps"]) == aim, "atTheLimit": False}
    else:
        found["steps"] = {"noAim": KEY_NONE, "why": "the key holds no steps"}
    return found


def left_out(field, row, block):
    """R326. A field majority.kindLeaveOut leaves out for the kind of the key, by the decision that measures it."""
    leave = (block.get("kindLeaveOut") or {}).get(row.get("kind") or "", ())
    if field.startswith(plan_rules.PARAMETER):
        return "parameter" in leave or field in leave
    return DECISION_OF.get(field) in leave


def other_discipline(built, key, kind, ctx):
    """True when the key links the file of the same code in the discipline its kind links, an SE key the STR file."""
    discipline = ctx["disciplines"].get(kind or "")
    names = ctx["names"]
    if not discipline or not built:
        return False
    return built.replace(names["prefix"] + names["discipline"] + "-", names["prefix"] + discipline + "-", 1) == key


def steps_only(entry, row, field, ctx):
    """R337. True when a parameter that reads the steps differs only by them: its rule gives the built value with the steps of the
    build and the key value with the steps of the key."""
    name = field[len(plan_rules.PARAMETER):]
    steps, own = row.get("steps"), value(entry.get("levels"), "steps")
    if name not in ctx["stepped"] or steps in (None, "") or own is None or str(own) == str(steps):
        return False
    given = lambda count: plan_rules.key_parameters(ctx["keyRules"], entry["plot"], value(entry, "unit"), value(entry, "mirrored"), count, None,
                                                    ctx["block"]["linkNameFacade"], ctx["later"])[0].get(name)
    return given(own) == value(entry.get("parameters"), name) and given(int(float(steps))) == str(json.loads(row.get("parameters") or "{}").get(name))


def explain(key, plot, field, entry, row, ctx, detail):
    """R337. The state of a field the build gives otherwise than the key, and why."""
    question = ctx["questions"].get((key, plot, field))
    if question:
        return "question", question
    if field == "prototypeFile" and other_discipline(value(entry, "prototypeFile"), row.get("linkFile"), row.get("kind"), ctx):
        return "kind", "the %s key links the %s file of the code, a build the %s one" % (row.get("kind"), ctx["disciplines"][row["kind"]], ctx["names"]["discipline"])
    if (key, plot, field) in ctx["overrides"]:
        record = entry.get("overrides") or {}
        if field in (record.get("applied") or []):
            return "unexplained", "the build placed the override and the value still differs, " + detail
        return "override", (record.get("notApplied") or {}).get(field) or "an override of the key, plot and field the build counts as not placed"
    # R340. A level moved by an override the build did not place
    record = entry.get("overrides") or {}
    for moved in LEVEL_OVERRIDES.get(field, ()):
        if (key, plot, moved) in ctx["overrides"] and moved not in (record.get("applied") or []):
            return "override", (record.get("notApplied") or {}).get(moved) or "an override of the key, plot and field the build counts as not placed"
    if field.startswith(plan_rules.PARAMETER) and steps_only(entry, row, field, ctx):
        return "laterPhase", "differs only by the steps the stepBands of the level profile give, keyRules codes no steps"
    return "unexplained", detail


def unwritten(key, plot, found, plan, ctx):
    """R337. The state of a key plot the build does not write: laterPhase when it lacks only its levels, override when it lacks a
    link whose insert and rotation overrides hold no gap, skipped when the skip list of the override list alone leaves it out (R386),
    else unexplained with what it lacks."""
    if found is None:
        return "unexplained", "the build neither writes the plot nor lists it as missing"
    lacks = {item["key"]: item.get("why") for item in found.get("missing") or []}
    if set(lacks) == {SKIP}:
        return SKIPPED, "the plot is not written, " + str(lacks[SKIP])
    levels = "levels.ffl" in lacks
    road = plot in ((plan.get("summary") or {}).get("plotsWithNoRoadLevel") or [])
    # R340. With the levels coded a plot with no FFL for want of a road level at a front corner is explained by that input
    coded = plan_rules.key_levels_of(ctx["keyRules"])
    state, reasons, open_ = ("input" if road else "unexplained") if coded and levels else "laterPhase", [], []
    level_parameters = {name[len(plan_rules.PARAMETER):] for name in ctx.get("levelParameters") or []}
    linkless = "transform" in lacks or "mirrored" in lacks
    for name, why in lacks.items():
        if linkless and name.startswith("parameters.") and name[len("parameters."):] in ctx["handed"]:
            # A parameter of the hand follows the link, explained or not with it
            continue
        if name == "levels.ffl":
            if coded:
                reasons.append("no FFL, " + ("no road surface covers a front corner, so no El @ Entry" if road else str(why)))
                if not road:
                    open_.append(name + ": " + str(why))
            else:
                reasons.append("no FFL, " + ("no road level at a front corner" if road else str(why)) + ", the levels are 3c")
        elif levels and name.startswith("parameters.") and (name[len("parameters."):] in ctx["stepped"] or name[len("parameters."):] in level_parameters
                                                            or name[len("parameters."):] in plan_rules.step_parameters(ctx["keyRules"], ctx["later"])):
            reasons.append(name + " with no level")
        elif name == "transform" and all((key, plot, field) in ctx["overrides"] for field in ("insert", "rotationDeg")):
            reasons.append("no link, " + str(why) + ", its insert and rotation overrides hold no gap")
            state = "override" if state != "unexplained" else state
        else:
            open_.append(name + ": " + str(why))
    if open_:
        return "unexplained", "the plot is not written, it lacks " + " | ".join(open_)
    return state, "the plot is not written: " + ", ".join(reasons)


def compare(key, plan, rows, key_plan, ctx, reads=None):
    """R337. One verdict per field of every plot of the key: holds, or the state of its difference and why, a plot the build does
    not write one verdict of the field written. R340: the levels as well, each verdict of a level with its gap in mm, reads giving
    the level parameters of the key read by key and plot."""
    block = ctx["block"]
    written = {entry["plot"]: entry for entry in plan.get("plots") or []}
    missing = {item["plot"]: item for item in plan.get("missing") or []}
    keyed = {entry["plot"]: entry for entry in (key_plan or {}).get("plots") or []}
    verdicts = []
    for row in rows:
        if row["key"] != key:
            continue
        plot = str(row["plot"])
        entry = written.get(plot)
        if entry is None:
            state, why = unwritten(key, plot, missing.get(plot), plan, ctx)
            verdicts.append({"key": key, "plot": plot, "field": WRITTEN, "state": state, "why": why})
            continue
        lacks = {item["key"] for item in (missing.get(plot) or {}).get("missing") or []}
        mine = []
        for field, (holds, detail) in held_fields(entry, row, keyed.get(plot), lacks, block).items():
            if left_out(field, row, block):
                continue
            state, why = ("holds", "") if holds else explain(key, plot, field, entry, row, ctx, detail)
            mine.append({"key": key, "plot": plot, "field": field, "state": state, "why": why})
        for field, found in held_levels(entry, row, (reads or {}).get((key, plot)), block).items():
            if left_out(field, row, block):
                continue
            if "noAim" in found:
                mine.append({"key": key, "plot": plot, "field": field, "state": found["noAim"], "why": found["why"], "gapMm": None, "level": True})
                continue
            if found["holds"]:
                state, why = "holds", ""
            elif field == "rampFrontCorner":
                # The claim of the levels covers the mean of the front, a corner off is the fall of the front a build does not apply
                state, why = "question", ctx["block"]["rampFrontCorners"]["question"] + ", " + ctx["block"]["rampFrontCorners"]["why"] + ", " + found["detail"]
            else:
                state, why = explain(key, plot, field, entry, row, ctx, found["detail"])
            movers = LEVEL_MOVERS.get(field, (field, "heightM") if field.startswith(plan_rules.PARAMETER) else ())
            moved = any(name in ((entry.get("overrides") or {}).get("applied") or []) for name in movers)
            mine.append({"key": key, "plot": plot, "field": field, "state": state, "why": why, "gapMm": None if found["gap"] is None else round(found["gap"], 3),
                         "level": True, "byRule": found["byRule"], "moved": moved, "atTheLimit": found["atTheLimit"]})
        hand = next((item for item in mine if item["field"] == "mirrored"), None)
        if hand is not None and hand["state"] == "question":
            # A hand an open question decides gives the rotation, the origin, the yard outline and the parameters of the hand with it
            follow = set(FOLLOW_HAND) | {plan_rules.PARAMETER + name for name in ctx["handed"]}
            for item in mine:
                if item["field"] in follow and item["state"] == "unexplained":
                    item["state"], item["why"] = "question", "follows from the hand, " + hand["why"] + ", " + item["why"]
        verdicts.extend(mine)
    return verdicts


def tally(verdicts, bands=(1, 5)):
    """R337. The verdicts counted per key, field and state, the unexplained per key, every key with none at 0, and each unexplained
    difference listed."""
    by_key = defaultdict(lambda: defaultdict(Counter))
    for item in verdicts:
        by_key[item["key"]][item["field"]][item["state"]] += 1
    # R340. The unexplained levels per key at the pass limit and at each band, a level whose gap lies within a band holding at it
    levels = defaultdict(Counter)
    for item in verdicts:
        if item.get("level") and item["state"] == "unexplained":
            levels[item["key"]]["pass"] += 1
            for band in bands or ():
                if item.get("gapMm") is None or item["gapMm"] > band + 1e-6:
                    levels[item["key"]]["%gmm" % band] += 1
    # R340. Per key and level the holds by the rule alone, by an override of its own plot, neither, the misses, the levels the proof could
    # not read and those the key holds none of, and the holds at the pass limit itself
    holds = defaultdict(lambda: defaultdict(Counter))
    for item in verdicts:
        if not item.get("level"):
            continue
        counts = holds[item["key"]][item["field"]]
        if item["state"] == "holds":
            counts["byRule" if item.get("byRule") else "byUnknown" if item.get("byRule") is None else "byOverride" if item.get("moved") else "byNeither"] += 1
            counts["atTheLimit"] += 1 if item.get("atTheLimit") else 0
        else:
            counts[item["state"] if item["state"] in (NOT_CHECKED, KEY_NONE) else "misses"] += 1
    names = ("byRule", "byOverride", "byNeither", "byUnknown", "misses", NOT_CHECKED, KEY_NONE, "atTheLimit")
    return {"byKey": {key: {field: dict(counts) for field, counts in sorted(fields.items())} for key, fields in sorted(by_key.items())},
            "levelHolds": {key: {field: {name: counts.get(name, 0) for name in names} for field, counts in sorted(fields.items())} for key, fields in sorted(holds.items())},
            "notCheckedByKey": {key: sum(counts.get(NOT_CHECKED, 0) for counts in fields.values()) for key, fields in sorted(by_key.items())},
            "skippedByKey": {key: sum(counts.get(SKIPPED, 0) for counts in fields.values()) for key, fields in sorted(by_key.items())},
            "unexplainedByKey": {key: sum(counts.get("unexplained", 0) for counts in fields.values()) for key, fields in sorted(by_key.items())},
            "unexplainedLevelsByKey": {key: {name: levels[key].get(name, 0) for name in ["pass"] + ["%gmm" % band for band in bands or ()]} for key in sorted(by_key)},
            "byState": dict(Counter(item["state"] for item in verdicts)),
            "unexplained": [item for item in verdicts if item["state"] == "unexplained"]}


def exit_code(found):
    """R340. 1 when the proof could not hold a level of a written plot, notChecked, so a proof with a level unread never passes as 0."""
    return 1 if any(found["notCheckedByKey"].values()) else 0


def check_inputs(keys, plans, key_plans, reads):
    """R340. The plan, the key plan and the key read of every key the proof covers, else it is refused with the flags and keys named,
    never held with a level or a yard outline left out in silence."""
    missing = []
    for flag, found in (("--plan", plans), ("--key-plan", key_plans), ("--key-read", reads)):
        gone = sorted(set(keys) - set(found))
        if gone:
            missing.append(flag + " of " + ", ".join(gone))
    if missing:
        raise project_profile.ProfileError("the proof covers the keys %s, give %s (R340)" % (", ".join(sorted(keys)), " | ".join(missing)))


def overrides_of(path, sources):
    """The entries of the override list a build applies, its sources, by key, plot and field, with the misses its cannot list holds
    with no value (R272), which a build can not place, so a field they name is explained as an override with no gap was."""
    data = override_source.read_list(path)
    found = {(item["key"], str(item["plot"]), item["field"]): dict(item, value=None) for item in data.get("cannot") or [] if item["source"] in sources}
    found.update({(item["key"], str(item["plot"]), item["field"]): item for item in data["overrides"] if item["source"] in sources})
    return found


def main(argv=None):
    parser = argparse.ArgumentParser(description="The proof of a build by the rules of the keys (R337).")
    parser.add_argument("command", choices=("covers", "compare"))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--past-all", required=True)
    parser.add_argument("--out", required=True, help="covers: the folder of the cover files, compare: the report, outside the repo")
    parser.add_argument("--plan", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="compare: the plan plan_from_book.py built for the key")
    parser.add_argument("--key-plan", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="compare: the plan of the key of R325")
    parser.add_argument("--overrides", help="compare: the override list the build applied")
    parser.add_argument("--level-profile", help="compare: the level profile of the build, levelProfiles.default when left out")
    parser.add_argument("--key-read", nargs=2, action="append", metavar=("KEY", "FILE"), default=[], help="R340. compare: the reader output of a key, its level parameters")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        block = majority.load_block(settings)
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve() if args.command == "covers" else out.resolve().parent)
        rows = past_all.read_table(args.past_all)
        group = block["check"]["group"]
        if args.command == "covers":
            found = covers(rows, group)
            out.mkdir(parents=True, exist_ok=True)
            for key, cover in found.items():
                (out / ("cover_%s.json" % key)).write_text(json.dumps(cover, indent=1), encoding="utf-8")
            print("covers of %d keys: %s" % (len(found), ", ".join("%s %d plots" % (key, len(cover["plots"])) for key, cover in found.items())))
            return 0
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        lp = plan_rules.level_profile(profile, args.level_profile)
        later = (block["overrideKeep"].get("laterFields") or {}).get(lp.get("job")) or []
        overrides = overrides_of(args.overrides, set(override_source.default_sources(settings))) if args.overrides else {}
        ctx = proof_context(block, settings.get("prototypeNames"), plan_rules.key_rules_of(lp), overrides, later)
        plans = {key: json.loads(Path(path).read_text(encoding="utf-8")) for key, path in args.plan}
        key_plans = {key: json.loads(Path(path).read_text(encoding="utf-8")) for key, path in args.key_plan}
        # R337, R340. The plan, the key plan and the key read of every key of the group
        check_inputs(list(covers(rows, group)), plans, key_plans, dict(args.key_read))
        # R340. The level parameters of each key read, found by the plot number parameter as majority.py finds them (R335)
        found = key_rules.read_parameters({key: ramp_levels.host_document(link_summary.load(path)) for key, path in args.key_read}, block["linkPlotParameter"])
        reads = {pair: {name: parameters[name] for name in block["parameterLevels"] if name in parameters} for pair, parameters in found.items()}
        verdicts = [item for key in sorted(plans) for item in compare(key, plans[key], rows, key_plans.get(key), ctx, reads)]
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("build_proof: " + str(error), file=sys.stderr)
        return 2
    found = tally(verdicts, block["levelBandsMm"])
    report = {"plans": {key: str(path) for key, path in args.plan}, "overrides": args.overrides, "levelProfile": args.level_profile or "default",
              "levelPassMm": block["levelPassMm"], "levelBandsMm": block["levelBandsMm"], "notHeld": (block.get("levelNotMeasured") or {}), **found,
              "explained": [item for item in verdicts if item["state"] not in ("holds", "unexplained")]}
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(report, indent=1, ensure_ascii=False), encoding="utf-8")
    for key, fields in found["byKey"].items():
        print("%s unexplained %d: %s" % (key, found["unexplainedByKey"][key], json.dumps({field: counts for field, counts in fields.items() if set(counts) != {"holds"}}, ensure_ascii=False)))
        print("%s unexplained levels %s, skipped %d (R386)" % (key, json.dumps(found["unexplainedLevelsByKey"][key]), found["skippedByKey"][key]))
        for field, counts in (found["levelHolds"].get(key) or {}).items():
            print("%s level %s: %s" % (key, field, ", ".join("%s %d" % (name, count) for name, count in counts.items() if count)))
    print("states %s" % found["byState"])
    for item in found["unexplained"][:60]:
        print("  unexplained %s %s %s: %s" % (item["key"], item["plot"], item["field"], item["why"]))
    if exit_code(found):
        print("levels not checked, a failure: %s" % found["notCheckedByKey"], file=sys.stderr)
    return exit_code(found)


if __name__ == "__main__":
    sys.exit(main())
