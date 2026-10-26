"""R388. The units of a CadReader output the Python reference reads.

A CadReader read writes layers.json beside its outputs with the InsUnits of the drawing header (R5). Its plot, block and piece outputs
carry none of their own, so their units are those of the layers.json of their folder. The reference reads a drawing only
in Meters and never converts one: an output of another unit, or one with no layers.json beside it, such as a copy of plots.json alone,
is refused with the file and the unit named.

The measure (python/majority.py) and the build (python/plan_from_book.py) read one set of CadReader outputs, the read folders
cadReads.root of config/project.json names by the drawings of drawings, each through this check (read_input).
"""
import hashlib
import json
from pathlib import Path

import project_profile

METRES = "Meters"
LAYERS = "layers.json"
# R388. The CadReader outputs the measure and the build read, by role: the drawing of drawings in config and the file of its read, the
# folder for the road layout, whose pieces.json tells one set from another
ROLES = {"baseplan": ("baseplan", "plots.json"), "roads": ("roadLayout", None), "footprintBlocks": ("villaBlocks", "blocks.json")}
FOLDER_FILE = "pieces.json"


class UnitError(ValueError):
    """R388. A CadReader output whose drawing is not in Meters, or whose units are not known."""


def check(path):
    """R388. The units of the CadReader output at path, a file or its folder, Meters, else UnitError with the file and the unit."""
    path = Path(path)
    layers = (path if path.is_dir() else path.parent) / LAYERS
    if not layers.is_file():
        raise UnitError("%s has no %s of its read beside it, so the units of its drawing are not known, a drawing is read only in %s (R388)" % (path, LAYERS, METRES))
    units = json.loads(layers.read_text(encoding="utf-8")).get("units")
    if units != METRES:
        raise UnitError("%s is of a drawing in %s, a drawing is read only in %s and never converted (R388)" % (path, units, METRES))
    return units


def check_table(folder, settings=None):
    """R388. The units of a table folder of the data book, the data.json its reader wrote beside its csv tables: the units it records,
    as the CadReader stand-in records the header of its drawing, else the unit tableUnits of config names for its reader, C3DProbe
    when it names none, whose tables carry no units. Meters, else UnitError with the folder, the reader and the unit."""
    folder = Path(folder)
    data = json.loads((folder / "data.json").read_text(encoding="utf-8"))
    units = data.get("units")
    if units is None:
        reader = data.get("reader") or "C3DProbe"
        settings = settings if settings is not None else project_profile.load_settings()
        units = (settings.values.get("tableUnits") or {}).get(reader)
        if units is None:
            raise UnitError("%s is a table of %s, which records no units, and tableUnits of config names none for it, a drawing is read only in %s (R388)"
                            % (folder, reader, METRES))
    if units != METRES:
        raise UnitError("%s is a table of a drawing in %s, a drawing is read only in %s and never converted (R388)" % (folder, units, METRES))
    return units


def named_input(settings, role):
    """R388. The CadReader output of role config names: the read folder of its drawing of drawings under cadReads.root, relative to the
    repo when not absolute, and in it the file of the role, the folder itself for the road layout."""
    reads = settings.get("cadReads")
    if not isinstance(reads, dict) or not isinstance(reads.get("root"), str) or not reads["root"].strip():
        raise UnitError("config holds no cadReads.root, the folder of the CadReader reads the measure and the build read (R388)")
    root = Path(reads["root"])
    key, name = ROLES[role]
    folder = (root if root.is_absolute() else project_profile.ROOT / root) / settings.get("drawings")[key]
    return folder if name is None else folder / name


def digest(path, role):
    """The SHA-256 of a CadReader output of role, of its pieces.json for a folder."""
    return hashlib.sha256((Path(path) / FOLDER_FILE if ROLES[role][1] is None else Path(path)).read_bytes()).hexdigest()


def read_input(settings, role, given=None):
    """R388. The CadReader output of role the measure and the build read: the one config names (named_input), through check. A given
    file that is not it is taken only as a copy of its bytes by SHA-256, such as the copies of the Round 17 snapshot, and the file
    config names is read, else it is refused with both named, so the two read one set."""
    found = named_input(settings, role)
    if given is not None and Path(given).resolve() != found.resolve():
        try:
            same = digest(given, role) == digest(found, role)
        except OSError as error:
            raise UnitError("%s can not be held against the %s config names, %s, by SHA-256: %s (R388)" % (given, role, found, error)) from error
        if not same:
            raise UnitError("%s is another file than the %s config names, %s, by SHA-256, the measure and the build read one set (R388)" % (given, role, found))
    check(found)
    return found
