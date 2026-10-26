"""R353. The project position file of button 1 and its check points, the Python reference of the command position of the worker.

The position file is written from the read of an answer key (R311, R331), the reference key of majority.siteReference, outside git
(Q068): the project position of the internal origin in shared coordinates with its angle, the survey point and the project base point in
internal and shared coordinates, the levels with their elevations and the one Elevation Base they are measured from, the units, the length
accuracy and the active site with the sites of the read, its name recorded. It is held against every value keyRules.site of the level
profile gives, the angle, the active site, the sites, the level names, their steps, the units, the level base, the length accuracy of the
key and oneSet, and refused when one differs. At the write the reference key is held against every other key of its group of
majority.groups as R331 holds a read, the survey point and the project base point in internal and shared coordinates, the angle, the
project position, each level by name, the active site, the sites, the place, the units and the level base, so a set that disagrees, such
as the Native keys 2432 km off, is refused, and a key of the group with no read too.

The check points of button 1 hold the CAD against the keys: per answer key the corners of the Generic floors of its extreme plots (the
plots of one Generic floor whose corners have the least and the greatest mean east and north), read at full precision by the step
keyPoints of the read job of the test host, where the reader writes 3 decimals. Each corner is taken to shared coordinates by the position
file, the baseplan face that holds the middle of its floor so taken is the face of the plot, and the vertex of that face nearest the corner
is its CAD point, in site metres. The CAD point is taken to its internal point by SiteCoordinates of R211 from the file and the gap to the
corner in plan is the check, in mm by R385 at addin.position.gapDecimals and passing at addin.position.checkPointLimitMm. revitGapMm is
the gap of the CAD point to the shared corner Revit gives, apart from the file. A floor of no face, such as one a baseplan in feet or
offset leaves on no face, gives no CAD point and fails. Values are written by R385 at 3 decimals, the angle at 6, as R311 gives them. The
C# of the worker gives the same (ProjectPositionFile of Common).
"""
import argparse
import json
import math
import sys
from pathlib import Path

import cad_units
import common
import face_index
import face_rings
import key_rules
import link_summary
import parity
import plot_relations
import project_profile

FORMAT = "site-adaptation-position"
FORMAT_VERSION = 1
POINTS_FORMAT = "site-adaptation-key-points"
STEPS = ("position", "checkPoints")
# A point or a level of the file is the one of the rules within half a mm, an angle within a millionth of a degree, as R331 holds a read
SAME_MM = 0.5
SAME_DEG = 1e-6
UNIT_FIELDS = ("system", "length", "lengthSystem")


class PositionError(ValueError):
    """A key read or a setting the position file cannot be written from, with why."""


def finite_point(value, what):
    if not isinstance(value, list) or len(value) != 3 or not all(isinstance(item, (int, float)) and not isinstance(item, bool) and math.isfinite(item) for item in value):
        raise PositionError("%s of the key read is no point of three finite numbers (R353)" % what)
    return [float(item) for item in value]


def finite(value, what):
    if not isinstance(value, (int, float)) or isinstance(value, bool) or not math.isfinite(value):
        raise PositionError("%s of the key read is no finite number (R353)" % what)
    return float(value)


def document_of(read):
    """The one project document of a reader output."""
    documents = [document for document in read.get("documents") or [] if document.get("kind") == "project"]
    if len(documents) != 1:
        raise PositionError("the key read holds %d project documents, the position file is written from one (R353)" % len(documents))
    if read.get("lengthUnit") != "m" or read.get("angleUnit") != "deg":
        raise PositionError("the key read gives lengths in %s and angles in %s, the position file takes m and deg (R353)" % (read.get("lengthUnit"), read.get("angleUnit")))
    return documents[0]


def position_of(document, key):
    """R353. The position file of the site facts of one key read (R311): refused when the read holds no site, a part of it was not read,
    a point or a level is no finite number, or its levels stand on more than one Elevation Base."""
    site = document.get("site")
    if not isinstance(site, dict):
        raise PositionError("the key read holds no site facts, a read of R311 is needed (R353)")
    missing = [name for name in ("projectPosition", "levels", "units") if name in (site.get("notRead") or {}) or site.get(name) is None]
    if missing:
        raise PositionError("the site facts of the key read hold no %s (R353)" % ", ".join(missing))
    at = site["projectPosition"]
    levels = sorted(site["levels"], key=lambda level: (finite(level.get("projectElevation"), "projectElevation of " + str(level.get("name"))), level.get("name")))
    if not levels:
        raise PositionError("the key read holds no level (R353)")
    bases = sorted({level.get("elevationBase") for level in levels}, key=str)
    if len(bases) != 1 or not isinstance(bases[0], str):
        raise PositionError("the levels of the key read stand on the Elevation Bases %s, the position file takes one (R353)" % bases)
    points = {}
    for name, field in (("surveyPoint", "surveyPoint"), ("projectBasePoint", "basePoint")):
        point = document.get(field)
        if not isinstance(point, dict):
            raise PositionError("the key read holds no %s (R353)" % field)
        points[name] = {"internalM": finite_point(point.get("position"), field + ".position"), "sharedM": finite_point(point.get("sharedPosition"), field + ".sharedPosition")}
    return {
        "format": FORMAT, "formatVersion": FORMAT_VERSION, "key": key, "model": document.get("file"),
        "projectPosition": {"eastM": finite(at.get("eastWest"), "eastWest"), "northM": finite(at.get("northSouth"), "northSouth"),
                            "elevationM": finite(at.get("elevation"), "elevation"), "angleDeg": finite(at.get("angleDeg"), "angleDeg")},
        "angleToTrueNorthDeg": finite(document.get("angleToTrueNorthDeg"), "angleToTrueNorthDeg"),
        **points,
        "levels": [{"name": level["name"], "elevationM": finite(level.get("elevation"), "elevation of " + level["name"]),
                    "projectElevationM": float(level["projectElevation"])} for level in levels],
        "levelBase": bases[0],
        "units": {name: site["units"].get(name) for name in UNIT_FIELDS},
        "lengthAccuracy": site["units"].get("lengthAccuracy"),
        "activeSite": site.get("activeSite"), "sites": site.get("sites"), "placeName": site.get("placeName"),
    }


def against_rules(position, site_rules):
    """R353. What of the position file differs from keyRules.site, every value it gives: oneSet, the angle, the active site, the sites,
    the level names in order, the step of each level over the lowest within SAME_MM, the units, the level base and the length accuracy of
    the key of the file. [] when none does."""
    found = []
    if site_rules.get("oneSet") is not True:
        found.append("keyRules.site.oneSet %s, the keys hold no one set" % site_rules.get("oneSet"))
    if site_rules.get("angleToTrueNorthDeg") is None or abs(position["projectPosition"]["angleDeg"] - site_rules["angleToTrueNorthDeg"]) > SAME_DEG:
        found.append("angle %s, keyRules.site.angleToTrueNorthDeg %s" % (position["projectPosition"]["angleDeg"], site_rules.get("angleToTrueNorthDeg")))
    for name in ("activeSite", "sites"):
        if position[name] != site_rules.get(name):
            found.append("%s %s, keyRules.site.%s %s" % (name, position[name], name, site_rules.get(name)))
    names = [level["name"] for level in position["levels"]]
    if names != site_rules.get("levels"):
        found.append("levels %s, keyRules.site.levels %s" % (names, site_rules.get("levels")))
    lowest = position["levels"][0]["elevationM"]
    for level in position["levels"][1:]:
        rule = (site_rules.get("levelStepsM") or {}).get(level["name"])
        if rule is None or abs(level["elevationM"] - lowest - rule) * 1000.0 > SAME_MM:
            found.append("the step of %s, keyRules.site.levelStepsM %s" % (level["name"], rule))
    if position["units"] != {name: (site_rules.get("units") or {}).get(name) for name in UNIT_FIELDS}:
        found.append("units %s, keyRules.site.units %s" % (position["units"], site_rules.get("units")))
    if position["levelBase"] != site_rules.get("levelBase"):
        found.append("level base %s, keyRules.site.levelBase %s" % (position["levelBase"], site_rules.get("levelBase")))
    accuracy = (site_rules.get("lengthAccuracy") or {}).get(position["key"])
    if accuracy is None or position["lengthAccuracy"] != accuracy:
        found.append("lengthAccuracy %s of %s, keyRules.site.lengthAccuracy %s" % (position["lengthAccuracy"], position["key"], accuracy))
    return found


def set_differs(reference, others, reference_key, group):
    """R353, R331. The reference key read held against the read of every other key of its group as key_rules.site_facts holds a read:
    one line per key and field that differs, with its gap in mm or degrees or its value, and a key of the group with no read. []
    when the set is one."""
    found = ["%s no read, every key of the group is held against the reference" % key for key in group if key != reference_key and key not in others]
    facts = key_rules.site_facts(dict({reference_key: reference}, **{key: document for key, document in others.items() if key != reference_key}), reference_key)
    for key in sorted(facts["keys"]):
        for field, value in sorted(facts["keys"][key]["differ"].items()):
            unit = "" if value is None or field in ("activeSite", "sites", "placeName", "levelBase") or field in UNIT_FIELDS else " deg" if field == "angleToTrueNorthDeg" else " mm"
            found.append("%s %s %s%s" % (key, field, value, unit))
    return found


def to_internal(position, shared):
    """R211. SiteCoordinates.ToInternal: the internal point of a shared point by the project position of the file."""
    at = position["projectPosition"]
    angle = math.radians(at["angleDeg"])
    east, north = shared[0] - at["eastM"], shared[1] - at["northM"]
    return [math.cos(angle) * east + math.sin(angle) * north, -math.sin(angle) * east + math.cos(angle) * north, shared[2] - at["elevationM"]]


def to_shared(position, internal):
    """R211. SiteCoordinates.ToShared: the shared point of an internal point by the project position of the file."""
    at = position["projectPosition"]
    angle = math.radians(at["angleDeg"])
    return [math.cos(angle) * internal[0] - math.sin(angle) * internal[1] + at["eastM"], math.sin(angle) * internal[0] + math.cos(angle) * internal[1] + at["northM"],
            internal[2] + at["elevationM"]]


def mean_of(corners):
    """The mean east and north of corners, each a plain sum from the first, as the C# adds them, where sum() of Python 3.12 compensates."""
    east = north = 0.0
    for corner in corners:
        east += corner[0]
    for corner in corners:
        north += corner[1]
    return east / len(corners), north / len(corners)


def plot_order(plot):
    """A plot by its number, a plot of digits as the number it writes, every other after them as its text."""
    return (0, int(plot), "") if plot.isascii() and plot.isdigit() else (1, 0, plot)


def chosen_floors(points, types):
    """R353. The Generic floors of the extreme plots of a key: the plots with one floor of the types, whose corners have the least mean
    east, the greatest mean east, the least mean north and the greatest mean north, a tie to the lower plot, each plot once in that order."""
    by_plot = {}
    for floor in points.get("floors") or []:
        if floor.get("typeName") in types and floor.get("plot") and len(floor.get("corners") or []) >= 3:
            by_plot.setdefault(str(floor["plot"]), []).append(floor)
    single = {plot: found[0] for plot, found in by_plot.items() if len(found) == 1}
    if not single:
        raise PositionError("the key points of %s hold no Generic floor of %s on a plot of one such floor, the check points need one (R353)" % (points.get("model"), ", ".join(types)))
    mean = {plot: mean_of(floor["corners"]) for plot, floor in single.items()}
    picked = []
    for axis, sign in ((0, 1), (0, -1), (1, 1), (1, -1)):
        plot = min(single, key=lambda item: (sign * mean[item][axis], plot_order(item)))
        if plot not in picked:
            picked.append(plot)
    return [(plot, single[plot]) for plot in picked]


def check_points(position, points_by_key, rings, types_by_key, limit_mm, decimals):
    """R353. The check points of button 1 by <key>/<plot>/<corner>: per key of points_by_key the corners of the Generic floors of its
    extreme plots, each with its CAD point, the nearest vertex of the baseplan face that holds the middle of its floor taken to shared
    coordinates by the file, rings the rings of the baseplan faces by label, the gap through the file in mm and whether it passes."""
    index = face_index.FaceIndex(rings)
    rows = {}
    for key, points in points_by_key.items():
        for plot, floor in chosen_floors(points, types_by_key[key]):
            corners = floor["corners"]
            middle = to_shared(position, list(mean_of(corners)) + [0.0])
            face = index.face_of(middle[0], middle[1])
            vertices = [vertex for ring in rings[face] for vertex in ring] if face is not None else []
            for k, corner in enumerate(corners):
                shared_corner = floor["sharedCorners"][k]
                cad, internal, gap, revit = None, None, None, None
                if vertices:
                    at = to_shared(position, corner)
                    cad = list(min(vertices, key=lambda vertex: math.hypot(vertex[0] - at[0], vertex[1] - at[1])))
                    internal = to_internal(position, [cad[0], cad[1], 0.0])[:2]
                    gap = math.hypot(internal[0] - corner[0], internal[1] - corner[1]) * 1000.0
                    revit = math.hypot(cad[0] - shared_corner[0], cad[1] - shared_corner[1]) * 1000.0
                rows["%s/%s/%d" % (key, plot, k)] = {"key": key, "plot": plot, "face": face, "sharedM": cad, "internalM": internal, "keyInternalM": [corner[0], corner[1]],
                                                     "keySharedM": [shared_corner[0], shared_corner[1]], "gapMm": gap, "revitGapMm": revit,
                                                     "pass": gap is not None and common.fixed(gap, decimals) <= limit_mm}
    return rows


def points_of(path):
    """testhost_<code>_points.json of the step keyPoints, in metres and degrees."""
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    if payload.get("format") != POINTS_FORMAT or payload.get("lengthUnit") != "m" or payload.get("angleUnit") != "deg":
        raise PositionError("%s is no key points file of the read job in m and deg (R353)" % path)
    return payload


def written(position):
    """The position file as written, every length by R385 at 3 decimals and the angles at 6."""
    found = json.loads(json.dumps(position))
    found["projectPosition"] = {name: common.fixed(value, 6 if name == "angleDeg" else 3) for name, value in position["projectPosition"].items()}
    found["angleToTrueNorthDeg"] = common.fixed(position["angleToTrueNorthDeg"], 6)
    for name in ("surveyPoint", "projectBasePoint"):
        found[name] = {part: [common.fixed(value, 3) for value in values] for part, values in position[name].items()}
    found["levels"] = [{"name": level["name"], "elevationM": common.fixed(level["elevationM"], 3), "projectElevationM": common.fixed(level["projectElevationM"], 3)}
                       for level in position["levels"]]
    return found


def position_step(position, held):
    """R353. The step position of the parity, one item."""
    return {"position": {
        "key": position["key"], "projectPositionM": [position["projectPosition"][name] for name in ("eastM", "northM", "elevationM")],
        "angleDeg": position["projectPosition"]["angleDeg"], "surveyInternalM": position["surveyPoint"]["internalM"],
        "surveySharedM": position["surveyPoint"]["sharedM"], "baseInternalM": position["projectBasePoint"]["internalM"],
        "baseSharedM": position["projectBasePoint"]["sharedM"], "levelNames": [level["name"] for level in position["levels"]],
        "levelElevationsM": [level["elevationM"] for level in position["levels"]],
        "levelProjectElevationsM": [level["projectElevationM"] for level in position["levels"]], "levelBase": position["levelBase"],
        "activeSite": position["activeSite"], "sites": position["sites"], "heldAgainst": held}}


def settings_of(settings):
    block = (settings.get("addin") or {}).get("position")
    if not isinstance(block, dict):
        raise PositionError("config holds no addin.position, the position file and the check point limit (R353)")
    limit, decimals = block.get("checkPointLimitMm"), block.get("gapDecimals")
    if isinstance(limit, bool) or not isinstance(limit, (int, float)) or not math.isfinite(limit) or limit <= 0:
        raise PositionError("addin.position.checkPointLimitMm is no length in mm above 0 (R353)")
    if isinstance(decimals, bool) or not isinstance(decimals, int) or not 0 <= decimals <= 6:
        raise PositionError("addin.position.gapDecimals is no whole number of decimals from 0 to 6 (R353)")
    if not isinstance(block.get("file"), str) or not block["file"].lower().endswith(".json"):
        raise PositionError("addin.position.file names no .json file (R353)")
    return block


def key_of(settings):
    """R353. The key whose read the position file is written from, the reference key majority.siteReference names for the group of
    majority.check.group, the group whose rules keyRules holds."""
    block = settings.get("majority") or {}
    group = (block.get("check") or {}).get("group")
    key = (block.get("siteReference") or {}).get(group)
    if not isinstance(key, str) or not key:
        raise PositionError("majority.siteReference names no reference key for the group %s of majority.check.group (R353)" % group)
    return key


def group_of(settings):
    """R353. The keys of the group of majority.check.group in majority.groups, every one held against the reference key at the write."""
    block = settings.get("majority") or {}
    group = (block.get("check") or {}).get("group")
    keys = (block.get("groups") or {}).get(group)
    if not isinstance(keys, list) or not keys or key_of(settings) not in keys:
        raise PositionError("majority.groups names no keys of the group %s with its reference key (R353)" % group)
    return list(keys)


def generic_types(settings, key):
    """R353, R321. The type names of the Generic floors of a key, floors.keyTypes of its code when it names them, else floors.types."""
    floors = settings.get("floors") or {}
    own = ((floors.get("keyTypes") or {}).get(key.upper()) or {}).get("generic")
    return list(own or floors["types"]["generic"])


def level_profile_site(settings):
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    name = (profile.get("levelProfiles") or {}).get("default")
    return (((profile.get("levelProfiles") or {}).get(name) or {}).get("keyRules") or {}).get("site") or {}


def main(argv=None):
    parser = argparse.ArgumentParser(description="R353. The project position file of button 1 from a key read, and its check points.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--key-read", required=True, help="the reader output of the reference key of majority.siteReference")
    parser.add_argument("--key-reads", action="append", default=[], metavar="KEY=FILE", help="the reader output of every other key of the group of majority.check.group")
    parser.add_argument("--key-points", action="append", default=[], metavar="KEY=FILE", help="testhost_<code>_points.json of a key, its check points")
    parser.add_argument("--out", help="the folder of the position file, outside any git work tree, the file name that of addin.position.file")
    parser.add_argument("--baseplan", help="R388. plots.json of the baseplan read, the one cadReads names when left out")
    parser.add_argument("--steps", help="a folder outside any git work tree for step_position_python.json and step_checkPoints_python.json")
    args = parser.parse_args(argv)
    try:
        if args.steps:
            for step in STEPS:
                parity.clear_step(args.steps, step, "python")
        settings = project_profile.load_settings(args.settings)
        block = settings_of(settings)
        reference = key_of(settings)
        group = group_of(settings)
        document = document_of(json.loads(Path(args.key_read).read_text(encoding="utf-8")))
        position = position_of(document, reference)
        differ = against_rules(position, level_profile_site(settings))
        if differ:
            raise PositionError("the key read differs from keyRules.site of the level profile: " + ", and ".join(differ))
        others = dict(item.split("=", 1) for item in args.key_reads)
        stray = sorted(key for key in others if key not in group or key == reference)
        if stray:
            raise PositionError("the key reads %s are of no other key of the group %s (R353)" % (", ".join(stray), ", ".join(group)))
        documents = {key: document_of(json.loads(Path(path).read_text(encoding="utf-8"))) for key, path in others.items()}
        differ = set_differs(document, documents, reference, group)
        if differ:
            raise PositionError("the reference key %s differs from the keys of its group: %s (R353)" % (reference, ", and ".join(differ)))
        held = [key for key in group if key != reference]
        inputs = [parity.input_entry("config", args.settings), parity.input_entry("rules", str(project_profile.profile_path(settings))), parity.input_entry("keyRead", args.key_read)]
        inputs += [parity.input_entry("keyRead:" + key, path) for key, path in sorted(others.items())]
        made = {"position": position_step(position, held)}
        rows = None
        if args.key_points:
            given = dict(item.split("=", 1) for item in args.key_points)
            stray = sorted(key for key in given if key not in group)
            if stray:
                raise PositionError("the key points %s are of no key of the group %s (R353)" % (", ".join(stray), ", ".join(group)))
            baseplan = cad_units.read_input(settings, "baseplan", args.baseplan)
            plots = link_summary.load(baseplan)["plots"]
            # R345. The faces of the baseplan are refused as the build and the C# refuse them
            refused = face_rings.refusals(plots, settings.get("addin")["parity"]["kinds"]["areas"])
            if refused:
                raise PositionError("%d baseplan faces refused: %s" % (len(refused), ", ".join(refused)))
            points = {key: points_of(given[key]) for key in group if key in given}
            rows = check_points(position, points, face_index.rings_of(plots), {key: generic_types(settings, key) for key in points}, block["checkPointLimitMm"], block["gapDecimals"])
            made["checkPoints"] = rows
            inputs += [parity.input_entry("keyPoints:" + key, path) for key, path in sorted(given.items())] + [parity.input_entry("baseplanPlots", str(baseplan))]
        if args.out:
            out = Path(args.out)
            plot_relations.refuse_inside_repo(out.resolve())
            out.mkdir(parents=True, exist_ok=True)
            target = out / Path(block["file"]).name
            target.write_text(json.dumps(written(position), indent=1, ensure_ascii=False), encoding="utf-8")
            print(target)
        if args.steps:
            values = settings.values
            for step, items in made.items():
                fields = values["addin"]["parity"]["steps"][step]
                parity.write_step(args.steps, step, "python", "project", {name: entry_of(item, fields) for name, item in items.items()}, values, inputs)
                print("step %s: %d items" % (step, len(items)))
        print("the reference key %s held against %d keys of its group, the set is one" % (reference, len(held)))
        if rows is not None:
            gaps = [row["gapMm"] for row in rows.values() if row["gapMm"] is not None]
            print("check points %d, pass %d at %s mm, no face %d, largest gap %s mm" % (
                len(rows), sum(1 for row in rows.values() if row["pass"]), block["checkPointLimitMm"], sum(1 for row in rows.values() if row["face"] is None),
                common.fixed(max(gaps), block["gapDecimals"]) if gaps else None))
    except (project_profile.ProfileError, cad_units.UnitError, PositionError, KeyError, OSError, ValueError) as error:
        print("position_file: " + str(error), file=sys.stderr)
        return 2
    return 0


def entry_of(raw, fields):
    """A plot of a step file, its values of geometry by R385 at the decimals of their field, as read_steps.entry writes them."""
    import read_steps
    return read_steps.entry(raw, fields)


if __name__ == "__main__":
    sys.exit(main())
