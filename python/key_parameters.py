"""R353. The shared parameter GUIDs of the answer keys into addin.sharedParameters of config/project.json, from the parameter files the
read job of the test host writes, testhost_<code>_parameters.json (ParameterSteps and SharedParameterFile of Common).

The names are the floor parameter of floors.plotParameter, the link parameters of pastJob.parameters and the level parameters of
majority.parameterLevels, the 11 of docs/addin-design.md 3.1. A name takes the GUID of its SharedParameterElement when every key that
holds it gives one GUID and the parameters of its elements give that GUID too, the second route of the same window. A name with two
GUIDs, or whose elements give another, and a name no key holds as a shared parameter, a project parameter among them, get no GUID and are
listed in noGuid with why, never resolved silently, so button 1 stops naming them (docs/addin-design.md 3.1, Shared parameters).
"""
import argparse
import json
import re
import sys
from pathlib import Path

import project_profile

NOTE = ("R353. The shared parameters button 1 binds and every button reads and writes by GUID (docs/addin-design.md 3.1), read from the "
        "answer keys by the read job of the test host (testhost_<code>_parameters.json) and written by python/key_parameters.py: per name its "
        "GUID, the categories the keys bind it to and how many keys hold it. noGuid lists each name with no GUID and why, a project parameter "
        "holds none, and button 1 stops on a name of config with no GUID.")


def names_of(settings):
    """The 11 names: floors.plotParameter, pastJob.parameters and majority.parameterLevels, each once in that order."""
    found = [settings.get("floors")["plotParameter"]] + list(settings.get("pastJob")["parameters"]) + list(settings.get("majority")["parameterLevels"])
    return list(dict.fromkeys(found))


def collect(files, names):
    """Per name its GUID or why none, from the parameter files of the keys by key."""
    found = {}
    for name in names:
        guids, keys, on, seen, project = set(), [], set(), set(), []
        for key, payload in sorted(files.items()):
            rows = [row for row in payload.get("parameters") or [] if row.get("name") == name]
            if rows:
                keys.append(key)
                guids.update(row["guid"] for row in rows)
                for row in rows:
                    on.update(row.get("categories") or [])
            for item in payload.get("onElements") or []:
                if item.get("name") == name:
                    seen.update(item.get("guids") or [])
            if name in (payload.get("otherBindings") or []):
                project.append(key)
        entry = {"guid": None, "keys": keys, "on": sorted(on), "guids": sorted(guids), "onElements": sorted(seen)}
        if project:
            entry["projectParameterIn"] = project
        if not keys:
            entry["why"] = "no key holds a shared parameter of this name" + (", it is a project parameter in %s, which holds no GUID" % ", ".join(project) if project else "")
        elif len(guids) > 1:
            entry["why"] = "the keys give two GUIDs or more: %s" % ", ".join(sorted(guids))
        elif seen - guids:
            entry["why"] = "the parameters of the elements give another GUID: %s" % ", ".join(sorted(seen - guids))
        else:
            entry["guid"] = next(iter(guids))
        found[name] = entry
    return found


def block(found, names, keys):
    """The block addin.sharedParameters: the names with a GUID in the order of names, and noGuid with why."""
    if not keys:
        raise ValueError("no key parameter file was read, the GUIDs come from the answer keys (R353)")
    return {"note": NOTE, "keys": list(keys),
            "parameters": [{"name": name, "guid": found[name]["guid"], "on": found[name]["on"], "keys": len(found[name]["keys"])} for name in names if found[name]["guid"]],
            "noGuid": [{"name": name, "why": found[name]["why"]} for name in names if not found[name]["guid"]]}


def write_block(path, payload):
    """addin.sharedParameters written into the config file at path as its text, after addin.position, the block of an earlier run replaced,
    every other line kept as it is."""
    path = Path(path)
    text = path.read_text(encoding="utf-8")
    lines = ["    \"sharedParameters\": {", "      \"note\": %s," % json.dumps(payload["note"], ensure_ascii=False), "      \"keys\": %s," % json.dumps(payload["keys"], ensure_ascii=False),
             "      \"parameters\": ["]
    lines += ["        %s%s" % (json.dumps(item, ensure_ascii=False), "," if k + 1 < len(payload["parameters"]) else "") for k, item in enumerate(payload["parameters"])]
    lines += ["      ],", "      \"noGuid\": ["]
    lines += ["        %s%s" % (json.dumps(item, ensure_ascii=False), "," if k + 1 < len(payload["noGuid"]) else "") for k, item in enumerate(payload["noGuid"])]
    lines += ["      ]", "    },"]
    new = "\n".join(lines) + "\n"
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
            print("%s: %s, keys %d, on %s%s" % (name, "GUID" if entry["guid"] else "no GUID", len(entry["keys"]), ", ".join(entry["on"]) or "none",
                                                "" if entry["guid"] else ", " + entry["why"]))
        print("names %d, with a GUID %d, no GUID %d" % (len(names), len(payload["parameters"]), len(payload["noGuid"])))
        if args.write:
            print(write_block(args.settings, payload))
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("key_parameters: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
