"""Runs stage 1 on each scenario folder through the worker, as the add-in does. Usage: run_stage1.py --worker <CadReader.exe> [--only S1 S2].
Output under the work root, scenarios\\runs\\<name>. A scenario that fails is recorded and the next one runs. With --confirmed each scenario runs
again into scenarios\\runs\\<name>_confirmed with what its own first run proposed, as a person pressing Confirm on all of it (confirmed_profile.py).
"""
import argparse
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path

import confirmed_profile
import scenario_tools as tools

ROOT = Path(__file__).resolve().parents[2]
TIME_LIMIT_S = 3600.0


def built_commit(worker):
    """The commit the worker was built from. The build writes it into the version text of SiteAdaptation.Common.dll beside the worker as
    1.0.0+ and 40 hex characters, in 16 bit characters. UNKNOWN when the text is not there, never a guess."""
    library = Path(worker).parent / "SiteAdaptation.Common.dll"
    if not library.is_file():
        return "UNKNOWN"
    found = re.search(rb"\+\x00((?:[0-9a-f]\x00){40})", library.read_bytes())
    return found.group(1).decode("utf-16-le") if found else "UNKNOWN"


def request_for(name, folder, out, config=None, profile=None):
    inputs = [{"role": "scanFolder", "path": str(folder)}, {"role": "config", "path": str(config or ROOT / "config" / "project.json")}]
    if profile is not None:
        inputs.append({"role": "projectProfile", "path": str(profile)})
    return {
        "formatVersion": 1, "runId": "stage1", "document": {"path": str(out / "model.rvt"), "sessionId": name}, "command": "dataCheck",
        "inputs": inputs, "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": TIME_LIMIT_S,
    }


def run_one(name, worker, root, config=None, confirm=False):
    folder = root / name
    out = root / "runs" / (name + tools.CONFIRMED_RUN if confirm else name)
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    profile = None
    if confirm:
        proposed = json.loads((root / "runs" / name / "project_profile.json").read_text(encoding="utf-8"))
        profile = out / "confirmed_profile.json"
        profile.write_text(json.dumps(confirmed_profile.confirmed(proposed), indent=1), encoding="utf-8")
    path = out / "request_stage1.json"
    path.write_text(json.dumps(request_for(name, folder, out, config, profile)), encoding="utf-8")
    done = subprocess.run([str(worker), "--request", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    (out / "built_from.json").write_text(json.dumps({"commit": built_commit(worker)}), encoding="utf-8")
    (out / "worker_output.txt").write_text(done.stdout[-20000:] + done.stderr[-20000:], encoding="utf-8")
    print("%s%s exit %d" % (name, " confirmed" if confirm else "", done.returncode))


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--worker", required=True)
    parser.add_argument("--only", nargs="*")
    parser.add_argument("--config", help="a copy of config/project.json kept outside the repo, so an edit of the repo file never changes an input in the middle of a run")
    parser.add_argument("--confirmed", action="store_true", help="run on what the first run of the scenario proposed, which must be there")
    args = parser.parse_args(argv)
    root = tools.work_root() / "scenarios"
    for name in args.only or sorted(tools.SCENARIOS):
        run_one(name, args.worker, root, args.config, args.confirmed)
    return 0


if __name__ == "__main__":
    sys.exit(main())
