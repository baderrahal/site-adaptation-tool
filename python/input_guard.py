"""D19. The guard of a run that reads real inputs. While it is on, every file the process opens under an input root is recorded at its
first open, by name, size, write time and SHA-256, an open for writing there is refused before it writes, and at the end every recorded
file is measured again and the run fails on any change. The opens are seen by the audit event open of Python, so a reader that opens a
file through openpyxl, zipfile or csv is held the same as one that calls open itself. A root is held by its real path, so a link or a
junction into a root counts as the root (the Q111 run of 4g3 wrote into the real Prototypes folder through a junction).
"""

import datetime
import hashlib
import json
import os
import sys
from pathlib import Path

# The two roots D19 names, beside inputRoot of config and the input variable a run may point at a copy
FIXED_ROOTS = (r"C:\02-Site Adaptation", r"C:\10- ROSHEN MECCA")
ROOT_VARIABLE = "SITE_ADAPTATION_INPUT"
WRITE_FLAGS = os.O_WRONLY | os.O_RDWR | os.O_APPEND | os.O_CREAT | os.O_TRUNC


# The guards on now. An audit hook cannot be taken off, so one hook is added once and hands each open to these
ACTIVE = []
HOOKED = []


class InputChanged(RuntimeError):
    pass


def dispatch(event, args):
    if event == "open":
        for guard in list(ACTIVE):
            guard.hook(args)


def roots(config_path):
    found = [Path(root) for root in FIXED_ROOTS]
    root = json.loads(Path(config_path).read_text(encoding="utf-8")).get("inputRoot")
    if root:
        found.append(Path(root))
    if os.environ.get(ROOT_VARIABLE):
        found.append(Path(os.environ[ROOT_VARIABLE]))
    return found


def key(path):
    return os.path.normcase(os.path.realpath(path))


def write_time(ns):
    seconds, rest = divmod(ns, 10 ** 9)
    # The form of DateTime.ToString("o") of the C# InputGuard, 7 decimals of the second
    return datetime.datetime.fromtimestamp(seconds, datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S") + ".%07dZ" % (rest // 100)


def mark(path):
    found = os.stat(path)
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    return {"size": found.st_size, "writeTimeUtc": write_time(found.st_mtime_ns), "sha256": digest.hexdigest()}


def change(before, after):
    return ", ".join(name for field, name in (("size", "size"), ("writeTimeUtc", "write time"), ("sha256", "SHA-256")) if before[field] != after[field])


def writing(mode, flags):
    return bool(isinstance(mode, str) and set(mode) & set("wax+")) or bool(isinstance(flags, int) and flags & WRITE_FLAGS)


class Guard:
    """with Guard(roots, record=path): the body of the run. records lists each input with before, after and unchanged."""

    def __init__(self, input_roots, record=None):
        self.roots = [key(root) for root in input_roots]
        self.record = Path(record) if record else None
        self.seen = {}
        self.records = []
        self.active = False
        self.busy = False

    def under(self, path):
        found = key(path)
        return any(found == root or found.startswith(root.rstrip(os.sep) + os.sep) for root in self.roots)

    def hook(self, args):
        if not self.active or self.busy:
            return
        path, mode, flags = args
        if path is None or isinstance(path, int):
            return
        path = os.fsdecode(path)
        if not self.under(path):
            return
        if writing(mode, flags):
            raise InputChanged("a write into an input root is refused (D19): " + path)
        found = key(path)
        if found not in self.seen and os.path.isfile(path):
            self.busy = True
            try:
                self.seen[found] = (path, mark(path))
            finally:
                self.busy = False

    def __enter__(self):
        if not HOOKED:
            sys.addaudithook(dispatch)
            HOOKED.append(True)
        self.active = True
        ACTIVE.append(self)
        return self

    def __exit__(self, kind, value, trace):
        self.active = False
        ACTIVE.remove(self)
        changed = []
        for path, before in self.seen.values():
            after = mark(path) if os.path.isfile(path) else {"size": None, "writeTimeUtc": None, "sha256": None}
            differs = change(before, after)
            self.records.append({"name": os.path.basename(path), "path": path, "before": before, "after": after, "unchanged": not differs})
            if differs:
                changed.append("%s its %s" % (path, differs))
        if self.record:
            self.record.parent.mkdir(parents=True, exist_ok=True)
            self.record.write_text(json.dumps({"inputs": self.records}, indent=2), encoding="utf-8")
        if changed and not isinstance(value, InputChanged):
            raise InputChanged("an input changed while the run read it, nothing of the run is used (D19): " + ", and ".join(changed)) from value
        return False
