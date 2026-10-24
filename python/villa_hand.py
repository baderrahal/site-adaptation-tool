"""The hand of a villa prototype from Revit, Part V of round DATA-4 (R246).

The rooms step of a read job (R245) gives the rooms and doors of a prototype in internal coordinates, which are the link axes.
The plot frame of R123 in config/prototype_offsets.json says from which front corner the link X axis runs and where the link
origin sits from it, and the profile gives the plot width, so each point has a share across the plot front from the start
corner: under 0.5 it is on the left seen from the road, over 0.5 on the right. The plot entrance gate, the main entrance doors
and the driver's room of each prototype are measured that way, each position once however many design options repeat it, and
two prototypes are drawn the other hand when their entrance gates sit on opposite sides. A point off the plot width, an element of
another typology kept in the file, is counted apart. Counts go to villa_hand.json.
"""
import argparse
import json
import re
import sys
from pathlib import Path

import c3d_run
import project_profile

REPO = Path(__file__).resolve().parent.parent


def start_corner_x(frame):
    """The link X of the start front corner, from the anchor and offset of the plot frame and the plot width."""
    offset_x = frame["offset"][0]
    if frame["anchor"] == "start":
        return -offset_x
    return -offset_x - frame["widthM"]


def share(x, frame):
    return round((x - start_corner_x(frame)) / frame["widthM"], 3)


def side(value):
    return "left" if value < 0.5 else "right" if value > 0.5 else "middle"


def unique_points(items, key="point", step=0.01):
    """The positions of the items, each once, rounded to step."""
    seen = {}
    for item in items:
        point = item.get(key)
        if point:
            seen.setdefault((round(point[0] / step), round(point[1] / step)), item)
    return list(seen.values())


def ground(item, level_pattern):
    return re.search(level_pattern, item.get("level") or "", re.I) is not None


def measure(rooms_file, frame, settings):
    """The gate, the main entrance doors and the driver's room of one prototype, with x, share and side."""
    doors = [d for d in rooms_file["doors"] if ground(d, settings["groundLevel"])]
    found = {"gates": [], "entranceDoors": [], "driverRooms": []}
    for name, pattern, items in (("gates", settings["gateFamily"], doors), ("entranceDoors", settings["entranceDoorFamily"], doors)):
        picked = [d for d in items if re.search(pattern, d.get("family") or "", re.I) and (name == "gates" or not d.get("fromRoom") or not d.get("toRoom"))]
        for item in unique_points(picked):
            x = item["point"][0]
            found[name].append({"x": round(x, 3), "y": round(item["point"][1], 3), "share": share(x, frame), "side": side(share(x, frame)),
                                "facing": item.get("facing"), "room": item.get("toRoom") or item.get("fromRoom")})
    rooms = [r for r in rooms_file["rooms"] if re.search(settings["driverRoom"], r.get("name") or "", re.I) and r.get("point")]
    for item in unique_points(rooms):
        x = item["point"][0]
        found["driverRooms"].append({"x": round(x, 3), "y": round(item["point"][1], 3), "share": share(x, frame), "side": side(share(x, frame)),
                                     "level": item.get("level"), "number": item.get("number")})
    for name in ("gates", "entranceDoors", "driverRooms"):
        found[name + "OffPlot"] = sum(1 for item in found[name] if not 0 <= item["share"] <= 1)
        found[name] = [item for item in found[name] if 0 <= item["share"] <= 1]
    found["gateSides"] = sorted({g["side"] for g in found["gates"]})
    return found


def frames(settings, profile):
    offsets = json.loads((REPO / settings["offsets"]).read_text(encoding="utf-8"))["plotFrame"]["units"]
    found = {}
    for unit in settings["prototypes"]:
        frame = dict(offsets[unit])
        frame["widthM"] = profile.get("typologies." + unit + ".plot_width_m")
        found[unit] = frame
    return found


def verdict(measures, first, second):
    """other hand when the gates of the two sit on opposite sides, same hand when on one side, else open."""
    a, b = measures[first]["gateSides"], measures[second]["gateSides"]
    if len(a) == 1 and len(b) == 1 and a != b and "middle" not in a + b:
        return "other hand"
    if len(a) == 1 and a == b:
        return "same hand"
    return "open"


def run(config, settings_path):
    settings = config["dataBook"]["villaHand"]
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings(settings_path)))
    plot_frames = frames(settings, profile)
    measures = {}
    for unit, path in settings["prototypes"].items():
        rooms_file = json.loads(Path(path).read_text(encoding="utf-8"))
        measures[unit] = {"frame": plot_frames[unit], "rooms": len(rooms_file["rooms"]), "doors": len(rooms_file["doors"]), **measure(rooms_file, plot_frames[unit], settings)}
    first, second = settings["compare"]
    report = {"measures": measures, "compare": [first, second], "verdict": verdict(measures, first, second)}
    out = Path(config["dataBook"]["root"]) / settings["outFolder"]
    out.mkdir(parents=True, exist_ok=True)
    (out / "villa_hand.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="The hand of two villa prototypes from their rooms and doors in Revit (R246)")
    parser.add_argument("--config", default=str(c3d_run.CONFIG))
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    args = parser.parse_args(argv)
    report = run(c3d_run.load_config(args.config), args.settings)
    print(json.dumps(report, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
