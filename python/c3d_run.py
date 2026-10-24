"""Runs the Civil 3D probe in the core console on a working copy, read only (R150, R151).

The runner refuses any drawing that is not under the work folder, hashes the copy before and after the run,
starts accoreconsole with the probe commands and stops only the process it started when the run times out.
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
CONFIG = Path(__file__).resolve().parent / "c3d_probe.json"
COMMANDS = ("C3DDUMP", "C3DLEVELS", "C3DBORDER", "C3DLINES")
# A script line that could write a drawing. The probe never needs one, so a script holding one is refused
WRITING = ("SAVE", "QSAVE", "SAVEAS", "WBLOCK", "EXPORT", "ETRANSMIT", "PURGE", "AUDIT", "RECOVER")


def load_config(path=CONFIG):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def work_root(config):
    return Path(config["probeRoot"]) / "work"


def under(root, path):
    root = os.path.normcase(os.path.abspath(root)).rstrip("\\/") + os.sep
    return os.path.normcase(os.path.abspath(path)).startswith(root)


def refusal(config, drawing):
    """Why the drawing may not be opened, or None. Only a copy under work, never the source (R150)."""
    if under(config["sourceRoot"], drawing):
        return "the drawing lies in the source folder, open the working copy"
    if not under(work_root(config), drawing):
        return "the drawing is not under " + str(work_root(config))
    if not Path(drawing).is_file():
        return "the working copy is missing"
    return None


def script_text(dll, commands):
    for command in commands:
        if command not in COMMANDS:
            raise ValueError("not a probe command: " + command)
    return checked("\n".join(["NETLOAD", '"' + str(dll) + '"'] + list(commands)) + "\n")


def lisp_text(body):
    """A script of plain AutoLISP typed at the prompt. It needs no plug-in, so SECURELOAD does not stop it (R153).
    A command call is refused, the inventory only reads entity data and writes its own text file."""
    lowered = body.lower()
    for call in ("(command", "(vl-cmdf", "(command-s", "(vla-", "(vlax-"):
        if call in lowered:
            raise ValueError("the LISP holds " + call + ", the probe only reads")
    return checked(body.rstrip("\n") + "\n")


def checked(text):
    words = {word.strip('"_.()').upper() for word in text.split()}
    if words & set(WRITING):
        raise ValueError("the script holds a command that writes")
    return text


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for block in iter(lambda: handle.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def decode(raw):
    # The core console writes UTF-16 on some builds and the ANSI page on others
    if raw.count(b"\x00") > len(raw) // 4:
        return raw.decode("utf-16-le", errors="replace")
    return raw.decode("utf-8", errors="replace")


def stop_own(process):
    """Stops the process this runner started and its children, by its own id only (R151)."""
    if process.poll() is not None:
        return False
    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True)
    try:
        process.wait(30)
    except subprocess.TimeoutExpired:
        process.kill()
    return True


def run(config, drawing, commands, points=None, surfaces=None, label=None, launcher=None, lisp=None):
    drawing = Path(drawing)
    reason = refusal(config, drawing)
    if reason:
        raise ValueError(reason)
    root = Path(config["probeRoot"])
    logs = root / "logs"
    out = root / "out" / "c3d"
    logs.mkdir(parents=True, exist_ok=True)
    out.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    name = label or drawing.stem
    dll = Path(config["probeDll"])
    if not dll.is_absolute():
        dll = REPO / dll
    script = logs / f"{stamp}_{name}.scr"
    script.write_text(lisp_text(lisp) if lisp else script_text(dll, commands), encoding="ascii")
    env = dict(os.environ)
    env["C3DPROBE_WORK"] = str(work_root(config))
    env["C3DPROBE_OUT"] = str(out)
    if points:
        env["C3DPROBE_POINTS"] = str(points)
    if surfaces:
        env["C3DPROBE_SURFACES"] = "|".join(surfaces)
    folder_before = sorted(p.name for p in drawing.parent.iterdir())
    hash_before = sha256(drawing)
    args = [config["accoreconsole"], "/i", str(drawing), "/s", str(script), "/product", config["product"], "/l", config["language"]]
    start = time.time()
    # The core console writes its log into the folder it starts in, so it starts in logs
    launch = launcher or (lambda a, e: subprocess.Popen(a, env=e, cwd=str(logs), stdout=subprocess.PIPE, stderr=subprocess.STDOUT))
    process = launch(args, env)
    timed_out = False
    try:
        raw, _ = process.communicate(timeout=config["timeoutSeconds"])
    except subprocess.TimeoutExpired:
        timed_out = stop_own(process)
        raw, _ = process.communicate()
    seconds = round(time.time() - start, 1)
    text = decode(raw or b"")
    (logs / f"{stamp}_{name}.console.txt").write_text(text, encoding="utf-8")
    hash_after = sha256(drawing)
    folder_after = sorted(p.name for p in drawing.parent.iterdir())
    record = {
        "drawing": str(drawing),
        "commands": ["lisp"] if lisp else list(commands),
        "pid": process.pid,
        "exit": process.returncode,
        "timedOut": timed_out,
        "seconds": seconds,
        "unchanged": hash_before == hash_after,
        "newFiles": sorted(set(folder_after) - set(folder_before)),
        "probeLines": [line.strip() for line in text.splitlines() if "C3DPROBE" in line],
    }
    (logs / f"{stamp}_{name}.run.json").write_text(json.dumps(record, indent=1), encoding="utf-8")
    return record


def main(argv=None):
    parser = argparse.ArgumentParser(description="Run the Civil 3D probe on a working copy")
    parser.add_argument("role", help="a drawing role of c3d_probe.json")
    parser.add_argument("commands", nargs="+", choices=COMMANDS)
    parser.add_argument("--points", help="CSV of id,x,y for C3DLEVELS")
    parser.add_argument("--surfaces", nargs="*", help="surface names, all when left out")
    parser.add_argument("--config", default=str(CONFIG))
    args = parser.parse_args(argv)
    config = load_config(args.config)
    drawing = work_root(config) / config["drawings"][args.role]
    record = run(config, drawing, args.commands, args.points, args.surfaces, label=args.role + "_" + "_".join(c.lower() for c in args.commands))
    print(json.dumps(record, indent=1))
    return 0 if record["exit"] == 0 and record["unchanged"] and not record["timedOut"] else 1


if __name__ == "__main__":
    sys.exit(main())
