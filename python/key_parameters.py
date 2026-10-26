"""R353. The shared parameter GUIDs of the answer keys into addin.sharedParameters of config/project.json, from the parameter files the
read job of the test host writes, testhost_<code>_parameters.json (ParameterSteps and SharedParameterFile of Common).

The names are the floor parameter of floors.plotParameter, the link parameters of pastJob.parameters and the level parameters of
majority.parameterLevels, the 11 of docs/addin-design.md 3.1. A name takes the GUID of its bound SharedParameterElement when every key
that holds the name binds it as a shared parameter of one GUID and the parameters of its elements give that GUID too, the second route of
the same window. routes says which routes held it, parameterElement alone when no element of a key carries the name. A name with two
GUIDs or whose elements give another, a name some keys hold only as a project parameter or as an unbound shared element while others
bind it shared, a conflict, and a name no key binds as a shared parameter get no GUID and are listed in noGuid with why, never resolved
silently, so button 1 stops naming them (docs/addin-design.md 3.1, Shared parameters). projectParameterIn lists the keys that bind a
project parameter of the name, beside its shared one or in its place. conflicts lists every name the keys disagree on, two GUIDs, elements
of another GUID or project and shared keys, and every hand entry the keys give otherwise. A write keeps every entry Bader marks "byHand":
true as he wrote it, a GUID in parameters or none in noGuid, whatever the keys give, its disagreement with the keys in conflicts, and refuses
two hand entries of one name and a run of fewer keys than the block it writes over, which would shrink it (Q085). The totals printed are
those of the block written, or of the block a write would give.
"""
import argparse
import json
import re
import sys
from pathlib import Path

import project_profile

NOTE = ("R353. The shared parameters button 1 binds and every button reads and writes by GUID (docs/addin-design.md 3.1), read from the "
        "answer keys by the read job of the test host (testhost_<code>_parameters.json) and written by python/key_parameters.py: per name its "
        "GUID, the categories the keys bind it to, how many keys hold it and routes, the routes of the read that held the GUID, the "
        "SharedParameterElement and the parameters of the elements. projectParameterIn names the keys that bind a project parameter of the name. "
        "noGuid lists each name with no GUID and why, a project parameter holds none, and button 1 stops on a name of config with no GUID. "
        "conflicts lists each name the keys disagree on, two GUIDs, elements of another GUID, or keys that bind it shared and keys that hold it only as "
        "a project parameter, and each hand entry the keys give otherwise. An entry marked \"byHand\": true is kept by every run as written, in "
        "parameters or in noGuid, two hand entries of one name or a run that reads fewer keys than this block is refused (Q085).")


def names_of(settings):
    """The 11 names: floors.plotParameter, pastJob.parameters and majority.parameterLevels, each once in that order."""
    found = [settings.get("floors")["plotParameter"]] + list(settings.get("pastJob")["parameters"]) + list(settings.get("majority")["parameterLevels"])
    return list(dict.fromkeys(found))


def collect(files, names):
    """Per name its GUID or why none, from the parameter files of the keys by key."""
    found = {}
    for name in names:
        guids, keys, on, seen, project, held, elements = set(), [], set(), set(), [], [], []
        for key, payload in sorted(files.items()):
            rows = [row for row in payload.get("parameters") or [] if row.get("name") == name]
            bound = [row for row in rows if row.get("bound")]
            own = [item for item in payload.get("onElements") or [] if item.get("name") == name]
            is_project = name in (payload.get("otherBindings") or [])
            if rows or is_project:
                held.append(key)
            if bound:
                keys.append(key)
                guids.update(row["guid"] for row in bound)
                for row in bound:
                    on.update(row.get("categories") or [])
            for item in own:
                seen.update(item.get("guids") or [])
            if own:
                elements.append(key)
            if is_project:
                project.append(key)
        entry = {"guid": None, "keys": keys, "on": sorted(on), "guids": sorted(guids), "onElements": sorted(seen)}
        if project:
            entry["projectParameterIn"] = project
        apart = [key for key in held if key not in keys]
        entry["conflict"] = bool(keys) and (bool(apart) or len(guids) > 1 or bool(seen - guids))
        if not keys:
            entry["why"] = "no key binds a shared parameter of this name" + (", it is a project parameter in %s, which holds no GUID" % ", ".join(project) if project else "")
        elif apart:
            entry["why"] = "a conflict: %s bind it as a shared parameter, %s hold it only as a project parameter or an unbound shared element, so a read by GUID misses it there" % (
                ", ".join(keys), ", ".join(apart))
        elif len(guids) > 1:
            entry["why"] = "the keys give two GUIDs or more: %s" % ", ".join(sorted(guids))
        elif seen - guids:
            entry["why"] = "the parameters of the elements give another GUID: %s" % ", ".join(sorted(seen - guids))
        else:
            entry["guid"] = next(iter(guids))
            entry["routes"] = ["parameterElement", "elements"] if seen else ["parameterElement"]
        found[name] = entry
    return found


def block(found, names, keys):
    """The block addin.sharedParameters: the names with a GUID in the order of names, with the routes that held it, and noGuid with why,
    each with the keys that bind a project parameter of its name."""
    if not keys:
        raise ValueError("no key parameter file was read, the GUIDs come from the answer keys (R353)")
    with_project = lambda item, name: dict(item, projectParameterIn=found[name]["projectParameterIn"]) if found[name].get("projectParameterIn") else item
    return {"note": NOTE, "keys": list(keys),
            "parameters": [with_project({"name": name, "guid": found[name]["guid"], "on": found[name]["on"], "keys": len(found[name]["keys"]),
                                         "routes": found[name]["routes"]}, name) for name in names if found[name]["guid"]],
            "noGuid": [with_project({"name": name, "why": found[name]["why"]}, name) for name in names if not found[name]["guid"]],
            "conflicts": [{"name": name, "why": found[name]["why"]} for name in names if found[name].get("conflict")]}


def earlier_block(text):
    """addin.sharedParameters of the config text, None when it holds none."""
    return ((json.loads(text).get("addin") or {}).get("sharedParameters")) or None


def merged(payload, earlier):
    """Q085. The block of a run laid over the block it writes over: every entry Bader marks "byHand": true kept as he wrote it, a GUID in
    parameters or none in noGuid, its name out of the other list, and every disagreement of the keys with it or among themselves in
    conflicts, so a hand entry never hides what the keys give. Two hand entries of one name and a run of fewer keys than the earlier block
    are refused, so a rerun never shrinks the block or undoes a hand edit."""
    payload = dict(payload, conflicts=list(payload.get("conflicts") or []))
    if not earlier:
        return payload
    missing = [key for key in earlier.get("keys") or [] if key not in payload["keys"]]
    if missing:
        raise ValueError("the run reads the keys %s and the block it would write over holds %s too, a run of fewer keys would shrink it, refused (R353)" % (
            ", ".join(payload["keys"]), ", ".join(missing)))
    hand_guids = [item for item in earlier.get("parameters") or [] if item.get("byHand") is True]
    hand_none = [item for item in earlier.get("noGuid") or [] if item.get("byHand") is True]
    names = [item["name"] for item in hand_guids + hand_none]
    twice = sorted({name for name in names if names.count(name) > 1})
    if twice:
        raise ValueError("%s has two hand entries, one name holds one GUID or none, refused (R353, Q085)" % ", ".join(twice))
    run_guids = {item["name"]: item for item in payload["parameters"]}
    run_none = {item["name"]: item for item in payload["noGuid"]}
    conflicts = {item["name"]: dict(item) for item in payload["conflicts"]}
    for item in hand_guids:
        given = run_guids.get(item["name"])
        if given is not None and given["guid"] != item["guid"]:
            conflicts[item["name"]] = {"name": item["name"], "why": "the keys give %s" % given["guid"]}
        if item["name"] in conflicts:
            conflicts[item["name"]]["byHand"] = item["guid"]
    for item in hand_none:
        given = run_guids.get(item["name"])
        if given is not None:
            conflicts[item["name"]] = {"name": item["name"], "why": "the keys give %s" % given["guid"]}
        if item["name"] in conflicts:
            conflicts[item["name"]]["byHand"] = None
    held = set(names)
    order = [item["name"] for item in payload["parameters"] + payload["noGuid"]]
    return dict(payload, parameters=[item for item in payload["parameters"] if item["name"] not in held] + hand_guids,
                noGuid=[item for item in payload["noGuid"] if item["name"] not in held] + hand_none,
                conflicts=sorted(conflicts.values(), key=lambda item: order.index(item["name"]) if item["name"] in order else len(order)))


def totals(payload):
    """The counts of a block as it is written: names with a GUID, with none, conflicts and hand entries."""
    return "parameters %d, noGuid %d, conflicts %d, byHand %d" % (len(payload["parameters"]), len(payload["noGuid"]), len(payload["conflicts"]),
                                                                 sum(1 for part in ("parameters", "noGuid") for item in payload[part] if item.get("byHand") is True))


def block_text(payload):
    """The lines of the block addin.sharedParameters as the config file holds them, an entry per line."""
    lines = ["    \"sharedParameters\": {", "      \"note\": %s," % json.dumps(payload["note"], ensure_ascii=False), "      \"keys\": %s," % json.dumps(payload["keys"], ensure_ascii=False),
             "      \"parameters\": ["]
    lines += ["        %s%s" % (json.dumps(item, ensure_ascii=False), "," if k + 1 < len(payload["parameters"]) else "") for k, item in enumerate(payload["parameters"])]
    lines += ["      ],", "      \"noGuid\": ["]
    lines += ["        %s%s" % (json.dumps(item, ensure_ascii=False), "," if k + 1 < len(payload["noGuid"]) else "") for k, item in enumerate(payload["noGuid"])]
    lines += ["      ],", "      \"conflicts\": ["]
    lines += ["        %s%s" % (json.dumps(item, ensure_ascii=False), "," if k + 1 < len(payload["conflicts"]) else "") for k, item in enumerate(payload["conflicts"])]
    lines += ["      ]", "    },"]
    return "\n".join(lines) + "\n"


def write_block(path, payload):
    """addin.sharedParameters written into the config file at path as its text, after addin.position, the block of an earlier run replaced
    with its hand entries kept (merged), every other line kept as it is."""
    path = Path(path)
    text = path.read_text(encoding="utf-8")
    new = block_text(merged(payload, earlier_block(text)))
    earlier = re.compile(r"    \"sharedParameters\": \{\n.*?\n    \},\n", re.S)
    if earlier.search(text):
        text = earlier.sub(lambda match: new, text, count=1)
    else:
        anchor = "    \"parity\": {\n      \"note\": \"R345."
        if text.count(anchor) != 1:
            raise ValueError("the config file holds no one addin.parity block to write sharedParameters before (R353)")
        text = text.replace(anchor, new + anchor)
    json.loads(text)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(text)
    return path


def main(argv=None):
    parser = argparse.ArgumentParser(description="R353. The shared parameter GUIDs of the answer keys into config.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--parameters", action="append", default=[], metavar="KEY=FILE", help="testhost_<code>_parameters.json of the read of a key")
    parser.add_argument("--write", action="store_true", help="write addin.sharedParameters into the config file of --settings")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        files = {}
        for item in args.parameters:
            key, path = item.split("=", 1)
            payload = json.loads(Path(path).read_text(encoding="utf-8"))
            if payload.get("format") != "site-adaptation-shared-parameters":
                raise ValueError("%s is no parameter file of the read job (R353)" % path)
            files[key] = payload
        names = names_of(settings)
        found = collect(files, names)
        payload = block(found, names, sorted(files))
        for name in names:
            entry = found[name]
            print("%s: %s, keys %d, on %s, routes %s, a project parameter in %s%s" % (
                name, "GUID" if entry["guid"] else "no GUID", len(entry["keys"]), ", ".join(entry["on"]) or "none", ", ".join(entry.get("routes") or []) or "none",
                ", ".join(entry.get("projectParameterIn") or []) or "none", "" if entry["guid"] else ", " + entry["why"]))
        print("the keys: names %d, with a GUID %d, no GUID %d, a conflict %d, held on one route only %d" % (
            len(names), len(payload["parameters"]), len(payload["noGuid"]), len(payload["conflicts"]),
            sum(1 for name in names if found[name].get("routes") == ["parameterElement"])))
        # Q085. The block as it is written, the hand entries of the config kept over the keys, every conflict printed
        written = merged(payload, earlier_block(Path(args.settings).read_text(encoding="utf-8")))
        for item in written["conflicts"]:
            print("%s: a conflict of the keys, %s%s" % (item["name"], item["why"], "" if "byHand" not in item else ", held by hand with %s" % (item["byHand"] or "no GUID")))
        if args.write:
            print(write_block(args.settings, payload))
            print("the block written: " + totals(written))
        else:
            print("the block a write gives: " + totals(written))
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("key_parameters: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
