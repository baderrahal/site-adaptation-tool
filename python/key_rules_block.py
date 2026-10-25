"""R338. keyRules of a level profile of the rules profile, written from the majority.json of a run of python/majority.py.

Each decision named gets the candidate the run coded for the group of majority.check.group, its share over the plots of that
group and per key, strong, the share of the other group beside it (native, this run), the share of Round 17 where the config
names one (nativeRound17) and the number of plots it misses, each an override of the list. The hand decisions hold the table
learned per link file, the yard its table per zone, keyed by the zone as plots.csv names it, past_all.csv spelling a zone as a
number, with the prefix of the config, and the zones the config takes from others, held equal or refused, marked notMeasured
with why. Everything the block takes besides majority.json comes from majority.keyRulesBlock of config/project.json. The block
is put in place by text, so the rest of the rules profile keeps its form byte for byte, and a rerun on the same majority.json
gives the same bytes.

python python/key_rules_block.py <majority.json> [DECISION ...]  writes the decisions named, every decision the block holds
when none is named.
"""
import argparse
import json
import sys
from pathlib import Path

import project_profile

INDENT = "        "
CLOSE = "\n      }"
# R339. The level decisions of keyRules, held at majority.levelPassMm with their shares at levelBandsMm
LEVELS = ("entry", "rampFront", "levelSplit", "baseHeight", "ffl", "steps", "parameter RRE_ZZ_SP_Prototype-FFL")
# R339. The decision of the other group a level decision is shown beside, the Native keys hold the FFL less El @ Entry of R275
NATIVE_WORD = {"ffl": "height"}
SSL = "ssl"


def share(counts):
    total = sum(counts.values())
    return round(counts.get("holds", 0) / total, 4) if total else None


def entry(report, word, block):
    """The coded candidate of a decision for the group, its share, strength, plots, misses and share per key, with the share of the
    other group beside it and the share of Round 17 the config names."""
    config = block["keyRulesBlock"]
    group = block["check"]["group"]
    found = {}
    for decision in report["decisions"]:
        owner, _, name = decision["decision"].partition(" ")
        if name != word and not (owner != group and name == NATIVE_WORD.get(word)):
            continue
        coded = decision["tests"][decision["coded"]]
        found[owner] = {"rule": decision["coded"], "share": decision["share"], "strong": decision["strong"], "plots": decision["plots"],
                        "misses": decision["misses"], "byKey": {key: share(counts) for key, counts in coded["byKey"].items()}}
    if group not in found:
        return None
    result = dict(found[group])
    others = [name for name in block["groups"] if name != group and name in found]
    if others:
        result["native"] = found[others[0]]["share"]
    if word in config["nativeRound17"]:
        result["nativeRound17"] = config["nativeRound17"][word]
    return result


def site_block(report, block):
    """R331. The coordinates of the keys of the group as one set, what button 1 needs and no coordinate, and how far the reference
    key of the other group lies, in km."""
    config = block["keyRulesBlock"]
    group = block["check"]["group"]
    facts = report["siteFacts"]
    other = next(name for name in block["siteReference"] if name != group)
    gap = facts["acrossGroups"]["keys"][block["siteReference"][other]]["differ"]["surveyPoint.sharedPosition"] / 1e6
    return {"rule": "oneSet", "keys": len(facts[group]["keys"]), "oneSet": facts[group]["oneSet"], "equal": config["siteEqual"], **facts[group]["shared"],
            "lengthAccuracy": {key: value["lengthAccuracy"] for key, value in facts[group]["keys"].items()}, "position": config["sitePosition"],
            "native": config["siteNative"] % gap}


def zone_table(table, config):
    """R338. The yard table keyed by the zone as plots.csv names it, and the zones taken from others with why. A zone taken from
    zones that are missing or give two values is refused, never given one of them."""
    keyed = {config["zonePrefix"] + zone: value for zone, value in table.items()}
    taken = {}
    for zone, rule in config["zoneFrom"].items():
        if zone in keyed:
            continue
        values = [keyed.get(other) for other in rule["from"]]
        if any(value is None for value in values) or len(set(values)) != 1:
            raise project_profile.ProfileError("keyRulesBlock.zoneFrom takes %s from %s, which give %s, a zone is taken only from zones that give one value (R338)"
                                               % (zone, ", ".join(rule["from"]), ", ".join(str(value) for value in values)))
        keyed[zone] = values[0]
        taken[zone] = rule["why"]
    return dict(sorted(keyed.items())), taken


def block_of(report, words, existing, block):
    """R338. keyRules with the note of the config first, the decisions it holds in their order and those of words written again
    from the report, new ones after them."""
    config = block["keyRulesBlock"]
    group = block["check"]["group"]
    found = {"note": config["keyRulesNote"], **{word: value for word, value in existing.items() if word != "note"}}
    for word in words:
        if word == "site":
            found["site"] = site_block(report, block)
            continue
        if word == SSL:
            # R339. No key holds SSL, a build gives the FFL plus levels.ground_ssl, majority.levelNotMeasured says why
            found[SSL] = {"rule": "fflPlusGroundSsl", "notMeasured": block["levelNotMeasured"][SSL]}
            continue
        made = entry(report, word, block)
        if made is None:
            raise project_profile.ProfileError("majority.json holds no %s decision %s (R338)" % (group, word))
        decision = next(item for item in report["decisions"] if item["decision"] == group + " " + word)
        coded = decision["tests"][decision["coded"]]
        if word == "linkOrigin":
            made["holdMm"] = block["linkHoldMm"]
            made["byCode"] = {code: {"anchor": fit["anchor"], "offset": fit["offset"]} for code, fit in sorted(report["linkFits"][group].items())}
        if word in ("linkRotation", "frontCorner"):
            made["holdDeg"] = block["rotationHoldDeg"]
        if word.startswith("hand "):
            # The table learned per link file, the scope written as the group and the file, then the factor value and its mirror flag
            table = {}
            for key, value in coded["rule"].items():
                scope, _, factor = key.rpartition(" | ")
                table.setdefault(scope.split("'")[3], {})[factor] = value
            made["byFile"] = dict(sorted(table.items()))
            made["candidates"] = {name: test["share"] for name, test in decision["tests"].items() if not test.get("checkOnly")}
            if "byBetweenClusters" in decision:
                made["byBetweenClusters"] = decision["byBetweenClusters"]
            if "byBuild" in decision:
                # R337. The coded hand judged by the table of the file a build links, the share a build reproduces
                made["byBuild"] = decision["byBuild"]
            made["byCode"] = {code: share(counts) for code, counts in coded["byCode"].items()}
        if word in ("rampSlope", "yard", "generic"):
            made["table"] = {key.split(" | ", 1)[1]: value for key, value in coded["rule"].items()}
            made["checks"] = {name: test["share"] for name, test in decision["tests"].items() if test.get("checkOnly")}
        if word == "rampSlope":
            flat = report["flatRamps"][group]
            made["flatRamps"] = {"plots": flat["plots"], "withElAtEntryRefused": flat["byEntryRefused"].get("yes", 0), "refusedPlots": flat["refusedPlots"],
                                 "byKey": flat["byKey"], "rule": config["flatRampRule"]}
        if word == "yard":
            made["perArea"] = "zone"
            made["table"], taken = zone_table(made["table"], config)
            if taken:
                made["notMeasured"] = taken
        if word in ("rampOutline", "yardOutline"):
            made["holdMm"] = block["outlineHoldMm"]
            made["candidates"] = {name: test["share"] for name, test in decision["tests"].items()}
        if word == "yardOutline":
            made["groups"] = [{name: fitted[name] for name in ("unit", "mirrored", "width", "type", "plots", "loops")} for fitted in report["yardOutlineGroups"][group]["refitLoops"]]
        if word in ("rampFront", "rampFrontFall"):
            ranked = sorted(decision["tests"].items(), key=lambda pair: -(pair[1]["share"] or 0))[:3]
            made["candidates"] = {name: test["share"] for name, test in ranked}
        if word.startswith("wall") or word == "frontOpen":
            made["candidates"] = {name: test["share"] for name, test in decision["tests"].items()}
            if word.startswith("wall"):
                made["listedIn"] = config["wallListedIn"]
                made["holdMm"] = block["outlineHoldMm"] if word == "wallLine" else block["levelHoldMm"]
            if word == "wallBase":
                made["walls"] = {key: counts for key, counts in report["walls"][group]["byKey"].items()}
            if word == "frontOpen":
                made["walledFronts"] = len(report["frontWalled"][group])
        if word in LEVELS:
            # R339. The pass limit, the shares at 1 and 5 mm, the share a build reproduces and the candidates beside the coded one
            for name in ("passMm", "bands", "byBuild"):
                if name in decision:
                    made[name] = decision[name]
            made["candidates"] = {name: test["share"] for name, test in sorted(decision["tests"].items(), key=lambda pair: -(pair[1]["share"] or 0))[:4]}
            if word in ("levelSplit", "baseHeight") and "rule" in coded:
                made["table"] = {key.split(" | ", 1)[1]: value for key, value in coded["rule"].items()}
        if word.startswith("parameter "):
            if "rule" in coded:
                made["table"] = {key.split(" | ", 1)[1]: value for key, value in coded["rule"].items()}
            made["candidates"] = {name: test["share"] for name, test in sorted(decision["tests"].items(), key=lambda pair: -(pair[1]["share"] or 0))[:3]}
            made["byKind"] = decision["byKind"]
        found[word] = made
    return found


def object_span(text, start):
    """The end of the JSON object that opens at start, the index after its closing brace, strings skipped."""
    depth, inside, escaped = 0, False, False
    for index in range(start, len(text)):
        char = text[index]
        if inside:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                inside = False
        elif char == '"':
            inside = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return index + 1
    raise project_profile.ProfileError("the rules profile has an object that does not close (R338)")


def put_block(text, name, found):
    """R338. The rules profile text with keyRules of the level profile name replaced, or added after its last value, one decision per
    line, the rest of the text byte for byte."""
    head = text.find('"levelProfiles": {')
    start = text.find(json.dumps(name) + ": {", head) if head >= 0 else -1
    if start < 0:
        raise project_profile.ProfileError("the rules profile has no level profile " + name + " (R338)")
    opening = text.index("{", start)
    end = object_span(text, opening)
    inner = text[opening:end]
    lines = ",\n".join(INDENT + json.dumps(word) + ": " + json.dumps(value, ensure_ascii=False) for word, value in found.items())
    made = '"keyRules": {\n' + lines + CLOSE
    at = inner.find('"keyRules": {')
    if at >= 0:
        stop = inner.index(CLOSE, at) + len(CLOSE)
        inner = inner[:at] + made + inner[stop:]
    else:
        body = inner[:-1].rstrip()
        inner = body + ",\n      " + made + inner[len(body):]
    new = text[:opening] + inner + text[end:]
    json.loads(new)
    return new


def write(path, report, block, words=None):
    """R338. Writes keyRules of majority.keyRulesBlock.levelProfile into the rules profile at path, the decisions of words, or every
    decision it holds when words is None."""
    text = Path(path).read_bytes().decode("utf-8")
    name = block["keyRulesBlock"]["levelProfile"]
    existing = (json.loads(text).get("levelProfiles") or {}).get(name, {}).get("keyRules") or {}
    if words is None:
        if not existing:
            raise project_profile.ProfileError("the level profile %s holds no keyRules, name the decisions to write (R338)" % name)
        words = [word for word in existing if word != "note"]
    new = put_block(text, name, block_of(report, words, existing, block))
    Path(path).write_bytes(new.encode("utf-8"))
    return words


def main(argv=None):
    parser = argparse.ArgumentParser(description="keyRules of the level profile from a majority.json (R338).")
    parser.add_argument("majority", help="majority.json of the write run of python/majority.py")
    parser.add_argument("decisions", nargs="*", help="the decisions to write, every decision the block holds when none is named")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        block = settings.get("majority")
        report = json.loads(Path(args.majority).read_text(encoding="utf-8"))
        words = write(project_profile.profile_path(settings), report, block, args.decisions or None)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("key_rules_block: " + str(error), file=sys.stderr)
        return 2
    print("keyRules of %s: %s" % (block["keyRulesBlock"]["levelProfile"], ", ".join(words)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
