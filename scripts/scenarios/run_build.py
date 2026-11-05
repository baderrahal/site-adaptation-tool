"""Runs the build plan (siteBuild) on the stage 1 run of each scenario through the worker, as the Build button does. Usage:
run_build.py --worker <CadReader.exe> [--only S1 S2]. Reads scenarios\\runs\\<name> and writes scenarios\\runs\\<name>_build under the work root.
It reads the plot table and the project profile of the stage 1 run and nothing else of the folders. The seconds of each run are kept in build_time.json.
"""
import argparse
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

import scenario_tools as tools

ROOT = Path(__file__).resolve().parents[2]
TIME_LIMIT_S = 600.0


def request_for(name, stage1, out, config=None):
    inputs = [
        {"role": "plotTable", "path": str(stage1 / "plots.json")},
        {"role": "projectProfile", "path": str(stage1 / "project_profile.json")},
        {"role": "config", "path": str(config or ROOT / "config" / "project.json")},
    ]
    return {
        "formatVersion": 1, "runId": "build", "document": {"path": str(out / "model.rvt"), "sessionId": name}, "command": "siteBuild",
        "inputs": inputs, "scope": {"kind": "project"}, "outputFolder": str(out), "timeLimitS": TIME_LIMIT_S,
    }


def run_one(name, worker, root, config=None):
    stage1 = root / "runs" / name
    out = root / "runs" / (name + tools.BUILD_RUN)
    if not (stage1 / "plots.json").is_file():
        print("%s has no stage 1 run, run run_stage1.py first" % name)
        return
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    path = out / "request_build.json"
    path.write_text(json.dumps(request_for(name, stage1, out, config)), encoding="utf-8")
    started = time.time()
    done = subprocess.run([str(worker), "--request", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    (out / "build_time.json").write_text(json.dumps({"seconds": round(time.time() - started, 1)}), encoding="utf-8")
    (out / "worker_output.txt").write_text(done.stdout[-20000:] + done.stderr[-20000:], encoding="utf-8")
    print("%s build exit %d" % (name, done.returncode))


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--worker", required=True)
    parser.add_argument("--only", nargs="*")
    parser.add_argument("--config", help="a copy of config/project.json kept outside the repo")
    args = parser.parse_args(argv)
    root = tools.work_root() / "scenarios"
    for name in args.only or sorted(tools.SCENARIOS):
        run_one(name, args.worker, root, args.config)
    return 0


if __name__ == "__main__":
    sys.exit(main())
