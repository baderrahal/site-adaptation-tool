"""Starts a real session in a lane's own folder, so its role comes from the folder and its hooks fire, with a log Lead can read. A message to a running session cannot clear it, a new session is the clear.

Usage: run_lane.py <lane> <prompt file> [--model <id>] [--resume <log stem>]. A log stem is the prompt file's stem, under lanes in the work root. With --resume the session of that log carries on and the log grows.
"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scenarios"))
import scenario_tools as tools  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]


def program():
    """The Claude Code of the VS Code extension, the newest, as no claude command is on the path of this PC."""
    found = sorted((Path(os.environ["USERPROFILE"]) / ".vscode" / "extensions").glob("anthropic.claude-code-*/resources/native-binary/claude.exe"))
    if not found:
        raise SystemExit("no Claude Code program found under the VS Code extensions")
    return found[-1]


def session_of(log):
    """The session id of a log, the last one it names."""
    session = None
    for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            session = json.loads(line).get("session_id") or session
        except ValueError:
            continue
    return session


def git_bash():
    """A session started from a script finds no Git Bash by itself, and a hook that cannot run blocks every tool. Git for Windows holds it two folders above its git."""
    found = subprocess.run(["git", "--exec-path"], capture_output=True, text=True, check=True).stdout.strip()
    for parent in Path(found).parents:
        if (parent / "bin" / "bash.exe").is_file():
            return str(parent / "bin" / "bash.exe")
    raise SystemExit("no Git Bash found above " + found)


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("lane")
    parser.add_argument("prompt")
    parser.add_argument("--model")
    parser.add_argument("--resume", help="the log stem of the session to carry on")
    args = parser.parse_args(argv)
    prompt = Path(args.prompt)
    folder = tools.work_root() / "lanes"
    folder.mkdir(parents=True, exist_ok=True)
    log = folder / ((args.resume or prompt.stem) + ".jsonl")
    command = [str(program()), "-p", prompt.read_text(encoding="utf-8"), "--permission-mode", "auto", "--output-format", "stream-json", "--verbose"]
    if args.model:
        command += ["--model", args.model]
    if args.resume:
        command += ["--resume", session_of(log)]
    env = dict(os.environ, CLAUDE_CODE_GIT_BASH_PATH=git_bash())
    env.pop("SITE_ADAPTATION_INPUT", None)
    with open(log, "a" if args.resume else "w", encoding="utf-8") as out:
        done = subprocess.run(command, cwd=ROOT / ".claude" / "worktrees" / args.lane, stdin=subprocess.DEVNULL, stdout=out, stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace", env=env)
    print("lane %s ended, exit %d, log %s" % (args.lane, done.returncode, log.name))
    return done.returncode


if __name__ == "__main__":
    sys.exit(main())
