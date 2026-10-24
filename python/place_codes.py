"""R231. A new site file placed one prototype code at a time, each run a linksAndFloors job of the test host into the same file.

The codes are those of testHost.order the plan has plots of, in that order. Before each run free memory must reach
siteBuild.minFreeGB within siteBuild.placeSettleMinutes, looked at once a minute, as memory comes back slowly after a Revit
closes. When it does not, no more code is added and the codes left are listed. Every run goes through window_lock.py, the test
host saves the file after each run, and the time and free memory of each run go to logs/place_codes.jsonl of siteBuild.folder.
"""
import argparse
import json
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import link_summary
import project_profile
import window_lock

SCRIPT = Path(__file__).resolve().parent.parent / "dotnet" / "SiteAdaptation.TestHost" / "Run-TestHost.ps1"


def plan_codes(plan, order):
    """The codes of order that the plan holds plots of, each with its plot count."""
    files = [entry["prototypeFile"]["value"] for entry in plan["plots"]]
    found = []
    for code in order:
        count = sum(1 for name in files if "-" + code.lower() + "-zz-mod-" in name.lower())
        if count:
            found.append((code, count))
    return found


def settle(free, floor, minutes, sleep=time.sleep):
    """Free memory once it reaches floor within minutes, looked at once a minute, else None."""
    for look in range(int(minutes) + 1):
        value = free()
        if value >= floor:
            return value
        if look < minutes:
            sleep(60)
    return None


def run(codes, plan_path, block, first_code, log, free=window_lock.free_gb, launch=None, sleep=time.sleep):
    """R231. One run per code until free memory stays under the floor. Returns the runs and the codes left."""
    runs, left = [], []
    work = Path(block["folder"]) / "logs"
    for index, (code, count) in enumerate(codes):
        before = settle(free, block["minFreeGB"], block["placeSettleMinutes"], sleep)
        if before is None:
            left = [name for name, _ in codes[index:]]
            log({"event": "stopped", "code": code, "why": "free memory stayed under the floor", "left": left})
            break
        job_code = "S" + str(first_code + index)
        spec = work / ("spec_" + job_code + "_links.json")
        spec.write_text(json.dumps({"kind": "linksAndFloors", "code": job_code, "prototypeCode": code, "plan": str(plan_path)}), encoding="utf-8")
        started = time.monotonic()
        result = launch(job_code, code, spec)
        row = {"event": "run", "code": code, "jobCode": job_code, "plots": count, "freeGBBefore": round(before, 2),
               "minutes": round((time.monotonic() - started) / 60.0, 2), "exitCode": result, "freeGBAfter": round(free(), 2)}
        log(row)
        runs.append(row)
        if result != 0:
            left = [name for name, _ in codes[index + 1:]]
            log({"event": "stopped", "code": code, "why": "the run ended with exit code " + str(result), "left": left})
            break
    return runs, left


def launcher(round_name, out_folder):
    def launch(job_code, code, spec):
        command = [sys.executable, str(Path(__file__).resolve().parent / "window_lock.py"), "--round", round_name, "--label", "linksAndFloors " + code, "--",
                   "pwsh", "-NoProfile", "-File", str(SCRIPT), "-RoundSpec", str(spec), "-TimeoutMinutes", "30"]
        with open(Path(out_folder) / ("run_" + job_code + "_" + code + ".txt"), "w", encoding="utf-8") as handle:
            return subprocess.call(command, stdout=handle, stderr=subprocess.STDOUT)
    return launch


def main(argv=None):
    parser = argparse.ArgumentParser(description="Place a plan into the new site file one prototype code at a time (R231).")
    parser.add_argument("plan")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--round", required=True)
    parser.add_argument("--first-code", type=int, required=True, help="the number of the first scenario code, S<n>")
    parser.add_argument("--codes", help="only these codes, by comma")
    args = parser.parse_args(argv)
    settings = project_profile.load_settings(args.settings)
    block = settings.get("siteBuild")
    codes = plan_codes(link_summary.load(args.plan), list(settings.get("testHost.order")))
    if args.codes:
        wanted = args.codes.split(",")
        codes = [item for item in codes if item[0] in wanted]
    logs = Path(block["folder"]) / "logs"

    def log(row):
        with open(logs / "place_codes.jsonl", "a", encoding="utf-8") as handle:
            handle.write(json.dumps({"utc": datetime.now(timezone.utc).isoformat(timespec="seconds"), **row}) + "\n")
        print(json.dumps(row))

    runs, left = run(codes, Path(args.plan), block, args.first_code, log, launch=launcher(args.round, logs))
    print("runs %d, codes left %s" % (len(runs), left))
    return 0 if not left else 1


if __name__ == "__main__":
    sys.exit(main())
