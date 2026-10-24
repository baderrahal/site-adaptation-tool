"""Reads and writes config/prototype_offsets.json, the values measured on the past job (R100).

The file holds the entry offset of R94 and the offsets of a unit inside its footprint block of R97 to R99. The values live
in the file and in no Python file. A measuring script writes a section, a rule only reads it.
"""
import json
from pathlib import Path

import common
import project_profile

DEFAULT_PATH = Path(__file__).resolve().parent.parent / "config" / "prototype_offsets.json"
SECTIONS = ("entry", "groups")
NOTE = ("Measured from the {job} past job (the answer key and the schedule of that job in config/project.json), not a rule of the client. "
        "entry: roadOffsetM is the median of El @ Entry minus the road level at the curb point nearest the front edge of the plot, R94. "
        "groups: one per footprint block name and unit position, counted along the X axis of the block. offset is the link origin in the axes of the block "
        "from its insert point, relative is the turn of the link against the block and whether it is mirrored, entrance is the entry point of the ramp in the same axes, "
        "variants hold the relations a group has with their own offsets. Offset, relative and entrance are the medians over all plots of the group. "
        "Each group was also split in half with the seed in splitSeed, fitted on the first half and tested on the second, and fitError and testError count the plots by the distance "
        "between the predicted and the measured link origin. entranceTestError does the same for the entrance. A group with tooFew true was not tested. "
        "plotFrame: the link of each typology in the frame of its plot face, R121 to R123. front says which face edge is the front, linkAxes how the link axes follow it, "
        "and per typology anchor is the front corner the link X axis starts from or runs to, offset is the link origin from that corner along the link X and Y axes, "
        "and drawnSide is the side of its cluster, seen from the road, where the unit is not mirrored, null when no side holds on every plot. apart lists the plots of R120 left out of the fit. "
        "Lengths are in metres and angles in degrees. No Python file holds these values, R100.")


def note_for(job):
    """The note of the file with the name of the neighbourhood of the past job, which comes from pastJob in config/project.json."""
    return NOTE.format(job=job)
GROUP_KEYS = ("block", "position", "plots", "offset", "relative", "entrance")


def load(path=DEFAULT_PATH):
    """The offsets file as a dict. A missing file, bad JSON or a missing section raises ProfileError with the file name."""
    path = Path(path)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except OSError as error:
        raise project_profile.ProfileError(path.name + " cannot be read: " + str(error)) from error
    except json.JSONDecodeError as error:
        raise project_profile.ProfileError(path.name + " is not valid JSON: " + str(error)) from error
    if not isinstance(data, dict):
        raise project_profile.ProfileError(path.name + " is not an object")
    for section in SECTIONS:
        if section not in data:
            raise project_profile.ProfileError(path.name + " has no section " + section)
    return data


def entry_offset(data):
    """The offset in metres that R94 adds to the road level, from the entry section."""
    value = (data.get("entry") or {}).get("roadOffsetM")
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise project_profile.ProfileError("prototype_offsets.json entry has no roadOffsetM number")
    return float(value)


def group_index(data):
    """The groups by (block name, position). A group missing a key or listed twice raises ProfileError."""
    found = {}
    for group in data["groups"]:
        missing = [key for key in GROUP_KEYS if key not in group]
        if missing:
            raise project_profile.ProfileError("prototype_offsets.json group has no " + ", ".join(missing))
        key = (group["block"], group["position"])
        if key in found:
            raise project_profile.ProfileError("prototype_offsets.json lists the group " + str(key) + " twice")
        found[key] = group
    return found


def plot_frame(data):
    """R121 to R123. The plotFrame section, each typology with an anchor of start or end, an offset of two numbers and a drawnSide of left, right or null."""
    frame = data.get("plotFrame")
    if not isinstance(frame, dict) or not isinstance(frame.get("units"), dict):
        raise project_profile.ProfileError("prototype_offsets.json has no plotFrame section with units")
    for unit, entry in frame["units"].items():
        offset = entry.get("offset") if isinstance(entry, dict) else None
        if (entry.get("anchor") if isinstance(entry, dict) else None) not in ("start", "end") or not isinstance(offset, list) or len(offset) != 2                 or any(isinstance(value, bool) or not isinstance(value, (int, float)) for value in offset) or entry.get("drawnSide") not in ("left", "right", None):
            raise project_profile.ProfileError("prototype_offsets.json plotFrame unit " + str(unit) + " needs anchor start or end, an offset of two numbers and drawnSide left, right or null")
    return frame


def write_section(path, key, value, note=None):
    """Replaces one section and keeps the others. A file that does not exist yet starts with empty sections. Values follow R2."""
    path = Path(path)
    data = json.loads(path.read_text(encoding="utf-8")) if path.is_file() else {"entry": {}, "groups": []}
    data[key] = value
    if note is not None:
        data["note"] = note
    path.parent.mkdir(parents=True, exist_ok=True)
    common.write_json(path, {"note": data.get("note"), **{name: item for name, item in data.items() if name != "note"}}, wide_keys=frozenset({"roadOffsetM"}))
    return path
