"""R353, R461. The shared parameter GUIDs of the answer keys held to the parameter sheet of config, addin.parameterSheet, from the parameter
files the read job of the test host writes, testhost_<code>_parameters.json (ParameterSteps and SharedParameterFile of Common).

The names are the floor parameter of floors.plotParameter, the link parameters of pastJob.parameters and the level parameters of
majority.parameterLevels, the 11 of docs/design/03a-buttons-shared-and-1.md 3.1. A name takes the GUID of its bound SharedParameterElement when every key
that holds the name binds it as a shared parameter of one GUID and the parameters of its elements give that GUID too, the second route of
the same window. routes says which routes held it, parameterElement alone when no element of a key carries the name. A name with two
GUIDs or whose elements give another, a name some keys hold only as a project parameter or as an unbound shared element while others
bind it shared, a conflict, and a name no key binds as a shared parameter get no GUID and are listed in noGuid with why, never resolved
silently. projectParameterIn lists the keys that bind a project parameter of the name, beside its shared one or in its place. Each
parameter file is held to its key by the model it names, a work copy of the copy answerKeys names for the key, as
position_file.model_faults holds a key read. The run writes nothing: the sheet is the one source of the GUIDs (R461) and Bader edits it,
so every name the keys give a GUID the sheet lacks or gives another is printed and the run ends 1.
"""
import argparse
import json
import sys
from pathlib import Path

import parameter_sheet
import position_file
import project_profile


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
    """The measure of the keys: the names with a GUID in the order of names, with the routes that held it, and noGuid with why,
    each with the keys that bind a project parameter of its name."""
    if not keys:
        raise ValueError("no key parameter file was read, the GUIDs come from the answer keys (R353)")
    with_project = lambda item, name: dict(item, projectParameterIn=found[name]["projectParameterIn"]) if found[name].get("projectParameterIn") else item
    return {"keys": list(keys),
            "parameters": [with_project({"name": name, "guid": found[name]["guid"], "on": found[name]["on"], "keys": len(found[name]["keys"]),
                                         "routes": found[name]["routes"]}, name) for name in names if found[name]["guid"]],
            "noGuid": [with_project({"name": name, "why": found[name]["why"]}, name) for name in names if not found[name]["guid"]],
            "conflicts": [{"name": name, "why": found[name]["why"]} for name in names if found[name].get("conflict")]}


def against_sheet(payload, kept):
    """R461. The measure of the keys held to the parameter sheet, one line per name the keys give a GUID that the sheet lacks, leaves out
    or gives another GUID."""
    sheet = {item["name"]: item["guid"] for item in kept}
    faults = []
    for item in payload["parameters"]:
        if item["name"] not in sheet:
            faults.append("%s: the keys give the GUID %s, the parameter sheet has no row of Keep Yes" % (item["name"], item["guid"]))
        elif sheet[item["name"]] != item["guid"]:
            faults.append("%s: the keys give the GUID %s, the parameter sheet %s" % (item["name"], item["guid"], sheet[item["name"]]))
    return faults


def file_faults(files, copies):
    """R353, R312. Each parameter file held to its key by the model it names, one line per file of another model, as
    position_file.model_faults holds a key read."""
    return [line for key, payload in sorted(files.items()) for line in position_file.model_faults(payload.get("model"), key, copies, "parameters model")]


def totals(payload):
    """The counts of a measure: names with a GUID, with none and conflicts."""
    return "parameters %d, noGuid %d, conflicts %d" % (len(payload["parameters"]), len(payload["noGuid"]), len(payload["conflicts"]))


def main(argv=None):
    parser = argparse.ArgumentParser(description="R353, R461. The shared parameter GUIDs of the answer keys held to the parameter sheet of config.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--parameters", action="append", default=[], metavar="KEY=FILE", help="testhost_<code>_parameters.json of the read of a key")
    parser.add_argument("--key-copies", help="key_copies.json beside the copies of the answer keys, the one of answerKeys when left out")
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
        answer_keys = settings.get("answerKeys")
        manifest = args.key_copies or str(Path(settings.get("inputRoot")) / answer_keys["folder"] / answer_keys["manifest"])
        faults = file_faults(files, position_file.answer_copies(answer_keys, json.loads(Path(manifest).read_text(encoding="utf-8"))))
        if faults:
            raise ValueError("the parameter files are not those of their keys: %s (R353)" % ", and ".join(faults))
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
        for item in payload["conflicts"]:
            print("%s: a conflict of the keys, %s" % (item["name"], item["why"]))
        sheet_path = parameter_sheet.path_of(args.settings, {"addin": settings.get("addin")})
        kept, _ = parameter_sheet.from_file(sheet_path)
        differ = against_sheet(payload, kept)
        for line in differ:
            print(line)
        print("the keys against %s: %s, %d differ" % (sheet_path.name, totals(payload), len(differ)))
        if differ:
            return 1
    except (project_profile.ProfileError, position_file.PositionError, parameter_sheet.SheetError, KeyError, OSError, ValueError) as error:
        print("key_parameters: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
