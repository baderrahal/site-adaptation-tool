import argparse
import json
import re
import sys
import time
from array import array
from pathlib import Path
from xml.parsers import expat

import numpy as np

from common import write_json

CHUNK = 1 << 20
# R388. The one linear unit a LandXML is read in, never converted
METRE = "meter"
# R388. The file an earlier read writes for the LandXML, with its linearUnit and every surface it wrote
SUMMARY = "landxml_summary.json"
INVISIBLE = "1"
BAD_NAME = re.compile(r'[<>:"/\\|?*]')


def folder_name(name):
    """Surface names go into folder names, so the characters Windows refuses become an underscore."""
    return BAD_NAME.sub("_", name).strip().rstrip(".") or "surface"


class UnitError(ValueError):
    """R388. A LandXML with no Units element, no linearUnit or a linearUnit other than meter."""


class Collector:
    """Receives the parser events and keeps points and faces in compact arrays, never in lists of strings."""

    def __init__(self, on_surface, name=None):
        self.on_surface, self.name = on_surface, name
        self.units = False
        self.surface = None
        self.mode = None
        self.attrs = None
        self.text = []
        self.crs = None
        self.unit = None

    def start(self, tag, attrs):
        if tag == "P" or tag == "F":
            self.mode, self.attrs, self.text = tag, attrs, []
        elif tag == "Surface":
            self.surface = {"name": attrs.get("name", ""), "ids": array("q"), "xyz": array("d"), "faces": array("q"), "invisible": 0, "bad_points": 0, "bad_faces": 0, "face_count": 0, "declared": None}
        elif tag == "Definition" and self.surface is not None:
            self.surface["declared"] = {"zMin": attrs.get("elevMin"), "zMax": attrs.get("elevMax")}
        elif tag == "CoordinateSystem":
            self.crs = attrs.get("epsgCode")
        elif tag == "Units":
            self.units = True
        elif tag in ("Metric", "Imperial") and self.units:
            self.unit = attrs.get("linearUnit")

    def check_unit(self):
        """R388. The file is read in meter only: no Units element, no linearUnit and any other unit are refused with the file named."""
        if not self.units:
            raise UnitError("%s holds no Units element, a LandXML is read only in %s (R388)" % (self.name, METRE))
        if self.unit is None:
            raise UnitError("%s holds Units with no linearUnit, a LandXML is read only in %s (R388)" % (self.name, METRE))
        if self.unit != METRE:
            raise UnitError("%s is in %s, a LandXML is read only in %s and never converted (R388)" % (self.name, self.unit, METRE))

    def chars(self, data):
        if self.mode:
            self.text.append(data)

    def end(self, tag):
        if tag == "P" and self.surface is not None:
            self.point()
        elif tag == "F" and self.surface is not None:
            self.face()
        elif tag == "Surface" and self.surface is not None:
            # R388. Refused before the first surface is written, so no reader takes a surface of another unit
            self.check_unit()
            self.on_surface(self.surface)
            self.surface = None
        if tag == "P" or tag == "F":
            self.mode = None

    def point(self):
        parts = "".join(self.text).split()
        try:
            # The order inside a P tag is northing, easting, elevation. The arrays hold easting, northing, elevation
            northing, easting, z = float(parts[0]), float(parts[1]), float(parts[2])
            if len(parts) != 3:
                raise ValueError
            point_id = int(self.attrs["id"])
        except (ValueError, IndexError, KeyError):
            self.surface["bad_points"] += 1
            return
        self.surface["ids"].append(point_id)
        self.surface["xyz"].extend((easting, northing, z))

    def face(self):
        self.surface["face_count"] += 1
        if self.attrs.get("i") == INVISIBLE:
            self.surface["invisible"] += 1
            return
        parts = "".join(self.text).split()
        try:
            corners = [int(part) for part in parts]
            if len(corners) != 3:
                raise ValueError
        except ValueError:
            self.surface["bad_faces"] += 1
            return
        self.surface["faces"].extend(corners)


def stream(path, on_surface):
    """Feeds the file to the parser one megabyte at a time, so memory holds one surface and not the file."""
    collector = Collector(on_surface, Path(path).name)
    parser = expat.ParserCreate()
    parser.buffer_text = True
    parser.StartElementHandler = collector.start
    parser.EndElementHandler = collector.end
    parser.CharacterDataHandler = collector.chars
    with open(path, "rb") as handle:
        while True:
            block = handle.read(CHUNK)
            parser.Parse(block, not block)
            if not block:
                break
    return collector


def build_arrays(raw):
    """Points as an n by 3 array and visible faces as indexes into it. A face that names a missing point id is counted and dropped."""
    ids = np.frombuffer(raw["ids"], dtype=np.int64)
    xyz = np.frombuffer(raw["xyz"], dtype=np.float64).reshape(-1, 3)
    if len(np.unique(ids)) != len(ids):
        raise ValueError("surface " + raw["name"] + " has repeated point ids")
    order = np.argsort(ids, kind="stable")
    face_ids = np.frombuffer(raw["faces"], dtype=np.int64).reshape(-1, 3)
    position = np.searchsorted(ids[order], face_ids)
    position[position >= len(ids)] = 0
    found = ids[order][position] == face_ids if len(ids) else np.zeros(face_ids.shape, dtype=bool)
    good = found.all(axis=1)
    faces = order[position[good]].astype(np.int32)
    return xyz, faces, int(len(good) - good.sum())


def summary_of(raw, xyz, faces, lost):
    bad_faces = raw["bad_faces"] + lost
    entry = {
        "name": raw["name"],
        "folder": folder_name(raw["name"]),
        "pointCount": int(len(xyz)),
        "faceCount": raw["face_count"],
        "visibleFaceCount": int(len(faces)),
        "invisibleFaceCount": raw["invisible"],
        "badFaceCount": bad_faces,
        "badPointCount": raw["bad_points"],
        "declaredZRange": raw["declared"],
    }
    if len(xyz):
        entry["bbox"] = {"minEasting": float(xyz[:, 0].min()), "minNorthing": float(xyz[:, 1].min()), "maxEasting": float(xyz[:, 0].max()), "maxNorthing": float(xyz[:, 1].max())}
        entry["zRange"] = {"min": float(xyz[:, 2].min()), "max": float(xyz[:, 2].max())}
    return entry


def check_cached(out_dir, names):
    """R388. The surfaces names of an earlier read in out_dir are read only of a LandXML in meter: the landxml_summary.json that read
    wrote holds linearUnit meter and lists each name, else UnitError with the folder and the unit, since a surface folder carries none."""
    path = Path(out_dir) / SUMMARY
    if not path.is_file():
        raise UnitError("%s holds no %s, so the unit of its surfaces is not known, a LandXML is read only in %s (R388)" % (out_dir, SUMMARY, METRE))
    summary = json.loads(path.read_text(encoding="utf-8"))
    if summary.get("linearUnit") != METRE:
        raise UnitError("%s is the read of a LandXML in %s, a LandXML is read only in %s and never converted (R388)" % (out_dir, summary.get("linearUnit"), METRE))
    listed = {item.get("name") for item in summary.get("surfaces") or []}
    missing = [name for name in names if name not in listed]
    if missing:
        raise UnitError("%s lists no surface %s in its %s, so their unit is not known (R388)" % (out_dir, ", ".join(missing), SUMMARY))
    return summary["linearUnit"]


def read_landxml(path, out_dir, listed=None):
    """Writes one folder per surface with points.npy, faces.npy and summary.json, and landxml_summary.json for the file.

    listed is the set of surface names from config/road_surfaces.json. A surface the file holds that the config does not name is reported
    under notInConfig, and a configured name the file lacks is reported under missingFromFile.
    """
    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    started = time.monotonic()
    surfaces = []
    used = set()

    def finish(raw):
        xyz, faces, lost = build_arrays(raw)
        entry = summary_of(raw, xyz, faces, lost)
        if entry["folder"].lower() in used:
            entry["folder"] += "_" + str(len(surfaces) + 1)
        used.add(entry["folder"].lower())
        folder = out_dir / entry["folder"]
        folder.mkdir(parents=True, exist_ok=True)
        np.save(folder / "points.npy", xyz)
        np.save(folder / "faces.npy", faces)
        write_json(folder / "summary.json", entry)
        surfaces.append(entry)

    collector = stream(path, finish)
    collector.check_unit()
    names = [entry["name"] for entry in surfaces]
    report = {"file": Path(path).name, "epsgCode": collector.crs, "linearUnit": collector.unit, "surfaceCount": len(surfaces), "surfaces": surfaces, "seconds": round(time.monotonic() - started, 1)}
    if listed is not None:
        report["notInConfig"] = [name for name in names if name not in listed]
        report["missingFromFile"] = sorted(name for name in listed if name not in names)
    write_json(out_dir / SUMMARY, report)
    return report


def main(argv=None):
    parser = argparse.ArgumentParser(description="Stream a Civil 3D LandXML file into one folder of numpy arrays per surface.")
    parser.add_argument("xml", help="the LandXML file, read in place")
    parser.add_argument("out_dir")
    parser.add_argument("--config", help="config/road_surfaces.json, to report surfaces it does not name")
    args = parser.parse_args(argv)
    listed = None
    if args.config:
        listed = set(json.loads(Path(args.config).read_text(encoding="utf-8"))["surfaces"])
    try:
        report = read_landxml(args.xml, args.out_dir, listed)
    except UnitError as error:
        print("landxml_reader: " + str(error), file=sys.stderr)
        return 2
    for entry in report["surfaces"]:
        print(entry["name"] + ": " + str(entry["pointCount"]) + " points, " + str(entry["faceCount"]) + " faces, " + str(entry["invisibleFaceCount"]) + " invisible")
    if report.get("notInConfig"):
        print("not in config: " + ", ".join(report["notInConfig"]))
    if report.get("missingFromFile"):
        print("missing from file: " + ", ".join(report["missingFromFile"]))
    print("surfaces " + str(report["surfaceCount"]) + " in " + str(report["seconds"]) + " s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
