"""R118. The model files of the floor scenarios: one master, short lived work files, and the clean up of the old TEST copies.

The site model is named by testHost.model in config/project.json under inputRoot. Every file this script makes or deletes
sits in the folder of the site model and has a name built from it:

- <name>_TEST-MASTER.rvt, the master, made once by a file copy of the site model and never deleted here
- <name>_TEST-WORK-S<n>.rvt, a work file, a file copy of the master or of another work file, at most 3 at once
- <name>_TEST-<code>.rvt, <name>_TEST-<code>_run<n>.rvt and their .0001.rvt style backups, the copies of the round 15a test

A delete only ever goes through delete_listed, which refuses the site model, the master, any other name, a link or junction,
and anything outside that folder, such as the prototype folders. The site model is only ever read.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import stat
import sys
from pathlib import Path

import plan_from_past
import project_profile

MAX_WORK = 3
MASTER_TAG = "_TEST-MASTER"
WORK_TAG = "_TEST-WORK-S"
SCENARIO = re.compile(r"^S[1-9][0-9]{0,2}$")


class FileRuleError(Exception):
    """A request this script refuses. Nothing was copied or deleted."""


def site_model(settings):
    return plan_from_past.input_root(settings) / settings.get("testHost.model")


def master_of(original):
    return original.with_name(original.stem + MASTER_TAG + ".rvt")


def work_of(original, scenario):
    if not SCENARIO.match(scenario or ""):
        raise FileRuleError("a scenario is S1 to S999, not " + str(scenario))
    return original.with_name(original.stem + WORK_TAG + scenario[1:] + ".rvt")


def patterns(original):
    stem = re.escape(original.stem)
    return {
        "work": re.compile(r"^" + stem + r"_TEST-WORK-S[1-9][0-9]{0,2}\.rvt$", re.IGNORECASE),
        "workBackup": re.compile(r"^" + stem + r"_TEST-WORK-S[1-9][0-9]{0,2}(\.[0-9]{4}\.rvt|_backup)$", re.IGNORECASE),
        # Round 15a copies: a prototype code of 2 to 4 letters and digits that is not MASTER or WORK, maybe _run<n>, maybe a backup
        "old": re.compile(r"^" + stem + r"_TEST-(?!MASTER|WORK)[A-Z][A-Z0-9]{1,3}(_run[0-9]+)?(\.[0-9]{4})?\.rvt$", re.IGNORECASE),
        "oldBackupFolder": re.compile(r"^" + stem + r"_TEST-(?!MASTER|WORK)[A-Z][A-Z0-9]{1,3}(_run[0-9]+)?_backup$", re.IGNORECASE),
    }


def is_link(path):
    """A symbolic link or a junction. A junction is a reparse point that os.path.islink does not always see."""
    if os.path.islink(path):
        return True
    attributes = getattr(os.lstat(path), "st_file_attributes", 0)
    return bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def size_of(path):
    if path.is_dir():
        return sum(item.stat().st_size for item in path.rglob("*") if item.is_file())
    return path.stat().st_size


def check_listed(original, path, kinds):
    """The path when it is a file or backup folder of the kinds named that may be deleted, None when it is not there, else refused."""
    path = Path(path)
    if path.parent.resolve() != original.parent.resolve():
        raise FileRuleError("only files next to the site model are deleted, not " + str(path))
    if path.name.lower() in (original.name.lower(), master_of(original).name.lower()):
        raise FileRuleError("the site model and the master are never deleted: " + path.name)
    found = patterns(original)
    if not any(found[kind].match(path.name) for kind in kinds):
        raise FileRuleError("not a file of the kinds " + ", ".join(kinds) + ": " + path.name)
    if not path.exists():
        return None
    if is_link(path):
        raise FileRuleError("a link or junction is never deleted: " + path.name)
    return path


def delete_listed(original, path, kinds):
    """Deletes one file or backup folder of the kinds named, and says what went with its size. The only place that deletes."""
    path = check_listed(original, path, kinds)
    if path is None:
        return None
    size = size_of(path)
    if path.is_dir():
        shutil.rmtree(path)
    else:
        path.unlink()
    return {"name": path.name, "bytes": size}


def listing(original):
    """The master and the work files next to the site model, with sizes."""
    folder = original.parent
    master = master_of(original)
    works = sorted(path for path in folder.iterdir() if patterns(original)["work"].match(path.name))
    return {
        "master": {"name": master.name, "bytes": master.stat().st_size} if master.exists() else None,
        "work": [{"name": path.name, "bytes": path.stat().st_size} for path in works],
    }


def clean_old(original):
    """Deletes the round 15a TEST copies, their backups and backup folders. Lists each with its size."""
    found = patterns(original)
    targets = []
    for path in sorted(original.parent.iterdir()):
        if found["old"].match(path.name) and path.is_file():
            targets.append((path, ("old",)))
        elif found["oldBackupFolder"].match(path.name) and path.is_dir():
            targets.append((path, ("oldBackupFolder",)))
    return delete_all(original, targets)


def delete_all(original, targets):
    """Every target is checked before the first is deleted, so a refusal leaves all of them in place."""
    for path, kinds in targets:
        check_listed(original, path, kinds)
    return [item for item in (delete_listed(original, path, kinds) for path, kinds in targets) if item]


def digest(path):
    hasher = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            hasher.update(block)
    return hasher.hexdigest()


def copy_new(source, target):
    """A plain file copy to a name that does not exist yet, checked by hash. The source is only read."""
    if target.exists():
        raise FileRuleError("the copy is there already and is never overwritten: " + target.name)
    if is_link(source):
        raise FileRuleError("the source is a link or junction: " + source.name)
    shutil.copyfile(source, target)
    if digest(source) != digest(target):
        target.unlink()
        raise FileRuleError("the copy does not have the hash of its source, it was removed: " + target.name)
    return {"name": target.name, "bytes": target.stat().st_size, "from": source.name}


def make_master(original):
    return copy_new(original, master_of(original))


def make_work(original, scenario, source="master"):
    """A work file for the scenario, a copy of the master or of the work file of another scenario."""
    works = listing(original)["work"]
    if len(works) >= MAX_WORK:
        raise FileRuleError("there are " + str(len(works)) + " work files, at most " + str(MAX_WORK) + " exist at once")
    origin = master_of(original) if source == "master" else work_of(original, source)
    if not origin.exists():
        raise FileRuleError("the source is not there: " + origin.name)
    target = work_of(original, scenario)
    if origin == target:
        raise FileRuleError("a work file is not copied onto itself")
    return copy_new(origin, target)


def remove_work(original, scenario):
    """Deletes the work file of the scenario and the backups Revit left of it."""
    work = work_of(original, scenario)
    targets = [(work, ("work",))]
    for path in sorted(original.parent.iterdir()):
        if path != work and (path.name.lower().startswith(work.stem.lower() + ".") or path.name.lower() == work.stem.lower() + "_backup"):
            targets.append((path, ("workBackup",)))
    return delete_all(original, targets)


MISSING_TYPE = "Floor type not in the model"


def middle_plot(plan):
    """The plot in the middle of the plan that has a Generic floor and a ramp, so a failure there comes after floors were made."""
    plots = [entry for entry in plan["plots"] if "generic" in entry.get("floors", {}) and "ramp" in entry.get("floors", {})]
    return plots[len(plots) // 2]


def plan_with_missing_type(plan):
    """S5. One ramp names a type that floorSettings lists and the model does not hold, so the step stops before any change."""
    changed = json.loads(json.dumps(plan))
    entry = middle_plot(changed)
    changed["floorSettings"]["types"]["ramp"] = changed["floorSettings"]["types"]["ramp"] + [MISSING_TYPE]
    entry["floors"]["ramp"]["type"]["value"] = MISSING_TYPE
    return changed, entry["plot"]


def plan_with_crossing_loop(plan):
    """S6. The Generic floor of the middle plot has its second and third points swapped, so its loop crosses itself and Revit refuses it
    inside the transaction, after the floors before it were made."""
    changed = json.loads(json.dumps(plan))
    entry = middle_plot(changed)
    points = entry["floors"]["generic"]["outline"]["value"]["loops"][0]["points"]
    if len(points) < 4:
        raise FileRuleError("the middle plot has no loop of four points to cross")
    points[1], points[2] = points[2], points[1]
    return changed, entry["plot"]


def write_plan_copy(settings, scenario):
    """Writes plan_<scenario>.json next to the plan, outside the repo, and says which plot was changed."""
    job = plan_from_past.load_job(settings)
    source = Path(job["output"])
    plan = json.loads(source.read_text(encoding="utf-8"))
    makers = {"S5": plan_with_missing_type, "S6": plan_with_crossing_loop}
    if scenario not in makers:
        raise FileRuleError("a plan copy is made for S5 or S6 only")
    changed, plot = makers[scenario](plan)
    target = source.with_name("plan_" + scenario + ".json")
    project_profile_root = project_profile.ROOT.resolve()
    if project_profile_root in target.resolve().parents:
        raise FileRuleError("a plan copy is never written inside the repo")
    plan_from_past.write_plan(target, changed)
    return {"plan": str(target), "plot": plot}


def main(argv=None):
    parser = argparse.ArgumentParser(description="The master and work files of the floor scenarios, and the clean up of the old TEST copies.")
    parser.add_argument("action", choices=("status", "clean-old", "master", "work", "remove", "plan"))
    parser.add_argument("scenario", nargs="?", help="S<n> for work and remove")
    parser.add_argument("--source", default="master", help="master, or S<n> to copy the work file of another scenario")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    if args.action == "plan":
        try:
            print(json.dumps(write_plan_copy(settings, args.scenario), indent=1))
        except FileRuleError as error:
            print("refused: " + str(error), file=sys.stderr)
            return 3
        return 0
    original = site_model(settings)
    if not original.is_file():
        print("the site model is not there: " + str(original), file=sys.stderr)
        return 2
    try:
        if args.action == "status":
            result = listing(original)
        elif args.action == "clean-old":
            result = {"deleted": clean_old(original)}
        elif args.action == "master":
            result = {"made": make_master(original)}
        elif args.action == "work":
            result = {"made": make_work(original, args.scenario, args.source)}
        else:
            result = {"deleted": remove_work(original, args.scenario)}
    except FileRuleError as error:
        print("refused: " + str(error), file=sys.stderr)
        return 3
    if args.action != "status":
        result["state"] = listing(original)
    print(json.dumps(result, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
