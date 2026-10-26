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
keyPoints of the read job of the test host, where the reader writes 3 decimals, against the baseplan at full precision, plots_exact.json of
CadReader --exact, where plots.json holds 3 decimals. Each corner is taken to shared coordinates by the position file, the baseplan face that
holds the middle of its floor so taken is the face of the plot and must be the face the numbering of the build gives the plot (the client
code rows of labels.csv of the schedule fit of R351, a number on two faces a conflict, else the shift of config/plot_numbers.json), and the
vertex of that face nearest the corner is its CAD point, in site metres, with the merge CadReader made of it, a line end within its
tolerance taken as the vertex. The CAD point is taken to its internal point by SiteCoordinates of R211 from the file and the gap to the corner in plan is the
check, in mm by R385 at addin.position.gapDecimals and passing at addin.position.checkPointLimitMm. revitGapMm is the gap of the CAD point to
the shared corner Revit gives, apart from the file. A point fails with why: no face, no face numbered, a number on two faces, the face of
another plot, a vertex merged by more than the limit or the gap over the limit, and a key of the group with no key points is listed as
missing. A run that names no key points and no baseplan writes the file with an issue that the check points were not made. The key points are held to their key and to the position
file, and every key read to its key by the model it names (answerKeys and key_copies.json). A check that fails is an issue of the run and
the position file records its summary. Values are written by R385 at 3 decimals, the angle at 6, as R311 gives them. The C# of the worker
gives the same (ProjectPositionFile of Common).
"""
import argparse
import csv
import json
import math
import re
import sys
from pathlib import Path

import cad_links
import cad_units
import common
import face_index
import face_rings
import key_rules
import link_summary
import parity
import plot_relations
import project_profile
import yard_rule

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


def json_kind(value):
    """The JSON type of a value, as ProjectPositionFile names the type of a token."""
    return ("boolean" if isinstance(value, bool) else "number" if isinstance(value, (int, float)) else "string" if isinstance(value, str)
            else "array" if isinstance(value, list) else "object" if isinstance(value, dict) else "null")


def position_of(document, key):
    """R353. The position file of the site facts of one key read (R311): refused when the read holds no site, a part of it was not read,
    a point or a level is no finite number, or its levels stand on more than one Elevation Base."""
    site = document.get("site")
    if not isinstance(site, dict):
        raise PositionError("the key read holds no site facts, a read of R311 is needed (R353)")
    if site.get("notRead") is not None and not isinstance(site.get("notRead"), dict):
        raise PositionError("the site facts of the key read hold notRead as a JSON %s, an object of the parts not read is needed (R353)" % json_kind(site.get("notRead")))
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


POINTS_OF_READ = ("surveyPoint.position", "surveyPoint.sharedPosition", "basePoint.position", "basePoint.sharedPosition")
POSITION_PARTS = ("eastWest", "northSouth", "elevation", "angleDeg")
WORK_JOB = re.compile(r"[A-Z][A-Z0-9]{1,3}")


def text_of(value):
    """A value of a read as a line names it, as ProjectPositionFile.PyText gives it: None, a text as it is, a number as Python prints it, a
    list or an object as compact JSON."""
    if isinstance(value, (list, dict)):
        return json.dumps(value, separators=(",", ":"), ensure_ascii=False)
    return str(value)


def is_finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def is_point(value):
    return isinstance(value, list) and len(value) == 3 and all(is_finite(item) for item in value)


def read_faults(document, key):
    """R353, R331. Every value the set of the keys is held on that a key read is missing or did not read, one line per field, <key>
    <field> missing or not read, as ProjectPositionFile.ReadFaults gives them: the survey point and the project base point in internal
    and shared coordinates, the angle to true north, the site, every part of it the read lists in notRead, the project position by its
    east, north, elevation and angle, the levels with their names, elevations and Elevation Bases, the active site, the place, the sites
    and the units with the length accuracy."""
    found = []
    for path in POINTS_OF_READ:
        first, second = path.split(".")
        if not is_point((document.get(first) or {}).get(second) if isinstance(document.get(first), dict) else None):
            found.append("%s %s missing" % (key, path))
    if not is_finite(document.get("angleToTrueNorthDeg")):
        found.append("%s angleToTrueNorthDeg missing" % key)
    site = document.get("site")
    if not isinstance(site, dict):
        return found + ["%s site missing" % key]
    unread = site.get("notRead") if isinstance(site.get("notRead"), dict) else {}
    if site.get("notRead") is not None and not isinstance(site.get("notRead"), dict):
        found.append("%s site.notRead is no object" % key)
    found += ["%s site.%s not read" % (key, name) for name in sorted(unread)]
    at = site.get("projectPosition")
    if "projectPosition" not in unread:
        if not isinstance(at, dict):
            found.append("%s site.projectPosition missing" % key)
        else:
            found += ["%s site.projectPosition.%s missing" % (key, part) for part in POSITION_PARTS if not is_finite(at.get(part))]
    levels = site.get("levels")
    if "levels" not in unread:
        if not isinstance(levels, list) or not levels:
            found.append("%s site.levels missing" % key)
        else:
            for index, level in enumerate(levels):
                if not isinstance(level, dict):
                    found.append("%s site.levels[%d] missing" % (key, index))
                    continue
                if not isinstance(level.get("name"), str):
                    found.append("%s site.levels[%d].name missing" % (key, index))
                if not is_finite(level.get("elevation")):
                    found.append("%s site.levels[%d].elevation missing" % (key, index))
                if not isinstance(level.get("elevationBase"), str):
                    found.append("%s site.levels[%d].elevationBase missing" % (key, index))
    for name in ("activeSite", "placeName"):
        if name not in unread and not isinstance(site.get(name), str):
            found.append("%s site.%s missing" % (key, name))
    sites = site.get("sites")
    if "sites" not in unread and (not isinstance(sites, list) or not sites or not all(isinstance(item, str) for item in sites)):
        found.append("%s site.sites missing" % key)
    units = site.get("units")
    if "units" not in unread:
        if not isinstance(units, dict):
            found.append("%s site.units missing" % key)
        else:
            found += ["%s site.units.%s missing" % (key, part) for part in UNIT_FIELDS if not isinstance(units.get(part), str)]
            if not is_finite(units.get("lengthAccuracy")):
                found.append("%s site.units.lengthAccuracy missing" % key)
    return found


def answer_copies(answer_keys, manifest):
    """R353, R312. The stem of the copy of each answer key by its key in lower case, from answerKeys of config held against key_copies.json
    beside the copies: a code the manifest lists with another copy, or does not list, is refused, and so are two codes that name one copy,
    in any case of its name, which would let the read or parameter file of one key pass as the other."""
    listed = {item["code"]: item.get("copy") for item in (manifest or {}).get("copies") or [] if isinstance(item, dict) and isinstance(item.get("code"), str)}
    found = {}
    for entry in (answer_keys or {}).get("keys") or []:
        code, copy = (entry.get("code"), entry.get("copy")) if isinstance(entry, dict) else (None, None)
        if not isinstance(code, str) or not isinstance(copy, str):
            raise PositionError("answerKeys names a key of no code or no copy, a key read is held to its key by its copy (R353, R312)")
        if listed.get(code) != copy:
            raise PositionError("answerKeys names for %s the copy %s and key_copies.json %s, a key read is held to its key by one copy (R353, R312)" % (code, copy, text_of(listed.get(code))))
        stem = copy[:-4] if copy.lower().endswith(".rvt") else copy
        twin = next((other for other, held in found.items() if held.lower() == stem.lower()), None)
        if twin is not None:
            raise PositionError("answerKeys names for %s and %s one copy %s, a key read is held to its key by its own copy (R353, R312)" % (twin.upper(), code, copy))
        found[str(code).lower()] = stem
    return found


def model_faults(name, key, copies, what="model"):
    """R353. A read or key points file is of its key when the model it names is a work copy of the copy of that key, <copy stem>_TEST-<job>,
    as the read job names it: else one line naming the key and the model, as ProjectPositionFile.ModelFaults gives it."""
    stem = copies.get(key)
    if stem is None:
        return ["%s %s %s of no answer key" % (key, what, text_of(name))]
    text = name if isinstance(name, str) else ""
    bare = text[:-4] if text.lower().endswith(".rvt") else text
    prefix = stem + "_TEST-"
    if bare[:len(prefix)].casefold() != prefix.casefold() or not WORK_JOB.fullmatch(bare[len(prefix):]):
        return ["%s %s %s is not a work copy %s<job> of its answer key" % (key, what, text_of(name), prefix)]
    return []


def set_differs(documents, reference_key, group, copies):
    """R353, R331. The reads of the keys of a group held as one set: per key of the group in its order a read given, of its key and holding
    every value the set is held on (read_faults), then the reference key held against every other key as key_rules.site_facts holds a
    read, one line per key and field that differs with its gap in mm or degrees or its value. [] when the set is one."""
    found = []
    for key in group:
        if key not in documents:
            found.append("%s no read, every key of the group is held against the reference" % key)
            continue
        found += model_faults(documents[key].get("file"), key, copies)
        found += read_faults(documents[key], key)
    if found:
        return found
    facts = key_rules.site_facts({key: documents[key] for key in group}, reference_key)
    for key in sorted(facts["keys"]):
        for field, value in sorted(facts["keys"][key]["differ"].items()):
            unit = "" if value is None or field in ("activeSite", "sites", "placeName", "levelBase") or field in UNIT_FIELDS else " deg" if field == "angleToTrueNorthDeg" else " mm"
            found.append("%s %s %s%s" % (key, field, text_of(value), unit))
    return found


def points_faults(points, key, copies, position):
    """R353. The key points of a key held to its key, by the model they name, and to the position file, by their full precision project
    position within SAME_MM and SAME_DEG, one line per fault as ProjectPositionFile.PointsFaults gives it."""
    found = model_faults(points.get("model"), key, copies, "keyPoints model")
    at = points.get("projectPosition") if isinstance(points.get("projectPosition"), dict) else {}
    if not all(is_finite(at.get(part)) for part in POSITION_PARTS):
        return found + ["%s keyPoints projectPosition missing" % key]
    mine = position["projectPosition"]
    gap = math.dist([at["eastWest"], at["northSouth"], at["elevation"]], [mine["eastM"], mine["northM"], mine["elevationM"]]) * 1000.0
    if gap > SAME_MM:
        found.append("%s keyPoints projectPosition %s mm off the position file" % (key, common.fixed(gap, 1)))
    turn = cad_links.angle_gap(at["angleDeg"], mine["angleDeg"])
    if turn > SAME_DEG:
        found.append("%s keyPoints projectPosition angle %s deg off the position file" % (key, common.fixed(turn, 6)))
    return found


def shifts_of(payload):
    """R353, R28. The shift of each neighbourhood config/plot_numbers.json names, a whole number, as ProjectPositionFile.Shifts reads it."""
    found = {}
    for name, entry in ((payload or {}).get("neighbourhoods") or {}).items():
        shift = entry.get("shift") if isinstance(entry, dict) else None
        if isinstance(shift, bool) or not isinstance(shift, int):
            raise PositionError("config/plot_numbers.json gives %s a shift that is no whole number (R353, R28)" % name)
        found[name] = shift
    return found


# The kind location_plan.py writes for the client code labels of the unit plots, the rows plan_from_book.py and the faces step number by
PLOT_KIND = "clientCode"


def numbering(labels, shifts):
    """R353, R351. The faces of a plot by the numbering the build uses: the faces the client code rows of labels.csv of the schedule fit of
    R351 give its number, as plots.csv and plan_from_book.py take them, one face, or two and more for a number on two faces, a conflict
    never resolved by the shift. A number no client code row gives a face takes, for a key whose code config/plot_numbers.json names, the
    number less its shift, else none. Rows of open space, land use and substation number no face. Gives a function of the key and the plot
    that gives the list of faces, in the order of their numbers."""
    faces = {}
    for row in labels:
        number, face = str(row.get("number") or "").strip(), str(row.get("face") or "").strip()
        if number and face and row.get("kind") == PLOT_KIND:
            faces.setdefault(number, set()).add(face)

    def face_of(key, plot):
        held = faces.get(plot)
        if held:
            return sorted(held, key=plot_order)
        shift = shifts.get(key.upper())
        return [str(int(plot) - shift)] if shift is not None and plot.isascii() and plot.isdigit() else []
    return face_of


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
    east, the greatest mean east, the least mean north and the greatest mean north, a tie to the lower plot, each plot once in that order.
    A floor of no plot is never taken."""
    by_plot = {}
    for floor in points.get("floors") or []:
        if floor.get("typeName") in types and isinstance(floor.get("plot"), str) and floor["plot"] and len(floor.get("corners") or []) >= 3:
            by_plot.setdefault(floor["plot"], []).append(floor)
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


NO_POINTS = "no key points"


def merges_of(plots):
    """R353. The merge of each vertex of the faces of plots_exact.json by label and ring, as rings_of takes the rings: what CadReader merged
    into the vertex, in m."""
    found = {}
    for entry in plots or []:
        if entry.get("kind") != "plot_shape":
            continue
        label = yard_rule.whole_label(entry.get("plot"))
        ring = [(float(vertex[0]), float(vertex[1])) for vertex in entry.get("vertices") or []]
        if label is None or len(ring) < 3 or face_index.ring_area(ring) == 0:
            continue
        found.setdefault(str(label), []).append([float(value) for value in entry.get("vertexMergeM") or [0.0] * len(ring)])
    return found


def check_points(position, points_by_key, group, rings, types_by_key, face_numbered, limit_mm, decimals, merges=None):
    """R353. The check points of button 1 by <key>/<plot>/<corner>: per key of the group the corners of the Generic floors of its extreme
    plots, each with its CAD point, the nearest vertex of the baseplan face that holds the middle of its floor taken to shared coordinates
    by the file, with the merge CadReader made of it (merges_of, none when merges is None), the faces the numbering gives its plot
    (face_numbered), the gap through the file in mm and whether it passes, and why not: no face, no face numbered, a number on two faces,
    the face of another plot, the vertex merged by more than the limit or the gap over the limit. A key of the group with no key points is
    one row of its own, why no key points."""
    index = face_index.FaceIndex(rings)
    rows = {}
    for key in group:
        if key not in points_by_key:
            rows[key] = {"key": key, "plot": None, "face": None, "expectedFace": None, "numberFaces": None, "sharedM": None, "internalM": None, "keyInternalM": None,
                         "keySharedM": None, "gapMm": None, "revitGapMm": None, "mergeMm": None, "pass": False, "why": NO_POINTS}
            continue
        for plot, floor in chosen_floors(points_by_key[key], types_by_key[key]):
            corners = floor["corners"]
            middle = to_shared(position, list(mean_of(corners)) + [0.0])
            face = index.face_of(middle[0], middle[1])
            numbered = list(face_numbered(key, plot))
            expected = numbered[0] if len(numbered) == 1 else None
            vertices = [vertex for ring in rings[face] for vertex in ring] if face is not None else []
            moved = [value for ring in (merges or {}).get(face) or [] for value in ring] if face is not None else []
            for k, corner in enumerate(corners):
                shared_corner = floor["sharedCorners"][k]
                cad, internal, gap, revit, merge = None, None, None, None, None
                if vertices:
                    at = to_shared(position, corner)
                    nearest = min(range(len(vertices)), key=lambda item: math.hypot(vertices[item][0] - at[0], vertices[item][1] - at[1]))
                    cad = list(vertices[nearest])
                    merge = moved[nearest] * 1000.0 if moved else 0.0
                    internal = to_internal(position, [cad[0], cad[1], 0.0])[:2]
                    gap = math.hypot(internal[0] - corner[0], internal[1] - corner[1]) * 1000.0
                    revit = math.hypot(cad[0] - shared_corner[0], cad[1] - shared_corner[1]) * 1000.0
                why = ("no face" if face is None else "no face numbered" if not numbered else "number on two faces" if len(numbered) > 1
                       else "face of another plot" if face != expected else "vertex merged over the limit" if common.fixed(merge, decimals) > limit_mm
                       else "gap over the limit" if common.fixed(gap, decimals) > limit_mm else None)
                rows["%s/%s/%d" % (key, plot, k)] = {"key": key, "plot": plot, "face": face, "expectedFace": expected, "numberFaces": numbered if len(numbered) > 1 else None,
                                                     "sharedM": cad, "internalM": internal, "keyInternalM": [corner[0], corner[1]],
                                                     "keySharedM": [shared_corner[0], shared_corner[1]], "gapMm": gap, "revitGapMm": revit, "mergeMm": merge,
                                                     "pass": why is None, "why": why}
    return rows


WHY_COUNTS = (("overLimit", "gap over the limit"), ("noFace", "no face"), ("otherFace", "face of another plot"), ("noNumber", "no face numbered"),
              ("twoFaces", "number on two faces"), ("merged", "vertex merged over the limit"), ("noPoints", NO_POINTS))
NOT_MADE = "the check points were not made, the request names no baseplan and no key points (R353)"


def summary_of(rows, limit_mm):
    """R353. The check points in counts as the position file records them: the points, those that pass, each why apart, and whether the
    check failed."""
    found = {"limitMm": float(limit_mm), "points": len(rows), "pass": sum(1 for row in rows.values() if row["pass"])}
    for name, why in WHY_COUNTS:
        found[name] = sum(1 for row in rows.values() if row["why"] == why)
    found["failed"] = found["pass"] < found["points"]
    return found


def summary_line(summary):
    """The line of a check, the same in the answer of the worker."""
    return ("the check points %s: %d of %d pass at %s mm, gap over the limit %d, no face %d, face of another plot %d, no face numbered %d, number on two faces %d, "
            "vertex merged over the limit %d, no key points %d") % (
        "failed" if summary["failed"] else "passed", summary["pass"], summary["points"], summary["limitMm"], summary["overLimit"], summary["noFace"],
        summary["otherFace"], summary["noNumber"], summary["twoFaces"], summary["merged"], summary["noPoints"])


def issues_of(rows, decimals):
    """One line per check point that fails, its name and why, the faces of a number on two faces, its merge when it was merged over the
    limit and its gap when it has one, each a float as C# writes it, 0.0 and never 0."""
    return ["%s %s%s%s%s" % (name, row["why"], "" if not row.get("numberFaces") else " " + ", ".join(row["numberFaces"]),
                             "" if row["why"] != "vertex merged over the limit" else ", merged %s mm" % float(common.fixed(row["mergeMm"], decimals)),
                             "" if row["gapMm"] is None else ", gap %s mm" % float(common.fixed(row["gapMm"], decimals))) for name, row in rows.items() if not row["pass"]]


def points_of(path):
    """testhost_<code>_points.json of the step keyPoints, in metres and degrees."""
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    if payload.get("format") != POINTS_FORMAT or payload.get("lengthUnit") != "m" or payload.get("angleUnit") != "deg":
        raise PositionError("%s is no key points file of the read job in m and deg (R353)" % path)
    return payload


def exact_plots(path, settings):
    """R353. plots_exact.json of CadReader --exact, the plot shapes of the baseplan at full precision with the merge of each vertex, its units
    Meters by the layers.json of its read (R388), its drawing the one drawings.baseplan names and never one of drawings.referenceOnly (R300),
    as the worker reads the baseplan, and every face with the merge of each of its vertices, so a file of before the merges is refused."""
    cad_units.check(path)
    payload = json.loads(Path(path).read_text(encoding="utf-8"))
    if payload.get("units") != cad_units.METRES:
        raise PositionError("%s holds the plots of a drawing in %s, a drawing is read only in %s (R388)" % (path, payload.get("units"), cad_units.METRES))
    source = str(payload.get("source") or "")
    if project_profile.is_reference_only(source, settings):
        raise project_profile.ProfileError("%s is kept for reference only (drawings.referenceOnly) and is never read for the baseplan of the check points (R300, R353)" % source)
    baseplan = project_profile.file_name(settings.get("drawings")["baseplan"])
    if source.casefold() != baseplan.casefold():
        raise PositionError("%s is a read of %s, the check points take the baseplan drawings.baseplan names, %s (R353)" % (path, source or "no drawing", baseplan))
    for entry in payload["plots"]:
        if entry.get("kind") == "plot_shape" and len(entry.get("vertexMergeM") or []) != len(entry.get("vertices") or []):
            raise PositionError("%s holds the face of label %s with no vertexMergeM of each vertex, a read of CadReader --exact of before the merges, read the baseplan again (R353)"
                                % (path, entry.get("plot")))
    return payload["plots"]


def written(position, summary=None):
    """The position file as written, every length by R385 at 3 decimals and the angles at 6, with the summary of its check points when the
    check ran."""
    found = json.loads(json.dumps(position))
    found["projectPosition"] = {name: common.fixed(value, 6 if name == "angleDeg" else 3) for name, value in position["projectPosition"].items()}
    found["angleToTrueNorthDeg"] = common.fixed(position["angleToTrueNorthDeg"], 6)
    for name in ("surveyPoint", "projectBasePoint"):
        found[name] = {part: [common.fixed(value, 3) for value in values] for part, values in position[name].items()}
    found["levels"] = [{"name": level["name"], "elevationM": common.fixed(level["elevationM"], 3), "projectElevationM": common.fixed(level["projectElevationM"], 3)}
                       for level in position["levels"]]
    if summary is not None:
        found["checkPoints"] = summary
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
    parser.add_argument("--key-copies", help="key_copies.json beside the copies of the answer keys, the one of answerKeys when left out")
    parser.add_argument("--key-points", action="append", default=[], metavar="KEY=FILE", help="testhost_<code>_points.json of a key, its check points")
    parser.add_argument("--baseplan-exact", help="plots_exact.json of CadReader --exact on the baseplan, the faces of the check points at full precision")
    parser.add_argument("--labels", help="labels.csv of the location plan, the numbers of the faces by the schedule fit of R351")
    parser.add_argument("--plot-shifts", default=str(project_profile.ROOT / "config" / "plot_numbers.json"), help="config/plot_numbers.json, the shift of a neighbourhood it names")
    parser.add_argument("--out", help="the folder of the position file, outside any git work tree, the file name that of addin.position.file")
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
        answer_keys = settings.get("answerKeys")
        manifest_path = args.key_copies or str(Path(settings.get("inputRoot")) / answer_keys["folder"] / answer_keys["manifest"])
        copies = answer_copies(answer_keys, json.loads(Path(manifest_path).read_text(encoding="utf-8")))
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
        documents[reference] = document
        differ = set_differs(documents, reference, group, copies)
        if differ:
            raise PositionError("the reference key %s and the keys of its group are no one set: %s (R353)" % (reference, ", and ".join(differ)))
        held = [key for key in group if key != reference]
        inputs = [parity.input_entry("config", args.settings), parity.input_entry("rules", str(project_profile.profile_path(settings))), parity.input_entry("keyRead", args.key_read),
                  parity.input_entry("keyCopies", manifest_path)]
        inputs += [parity.input_entry("keyRead:" + key, path) for key, path in sorted(others.items())]
        made = {"position": position_step(position, held)}
        rows = summary = None
        if not args.key_points and (args.baseplan_exact or args.labels):
            raise PositionError("the check points hold the baseplan against the key points of the keys, the inputs baseplan and keyPoints:<key>, the request names one of them (R353)")
        if args.key_points:
            given = dict(item.split("=", 1) for item in args.key_points)
            stray = sorted(key for key in given if key not in group)
            if stray:
                raise PositionError("the key points %s are of no key of the group %s (R353)" % (", ".join(stray), ", ".join(group)))
            if not args.baseplan_exact or not args.labels:
                raise PositionError("the check points need --baseplan-exact and --labels (R353)")
            points = {key: points_of(given[key]) for key in group if key in given}
            faults = [line for key, payload in points.items() for line in points_faults(payload, key, copies, position)]
            if faults:
                raise PositionError("the key points are not those of their keys and of the position file: %s (R353)" % ", and ".join(faults))
            plots = exact_plots(args.baseplan_exact, settings)
            # R345. The faces of the baseplan are refused as the build and the C# refuse them
            refused = face_rings.refusals(plots, settings.get("addin")["parity"]["kinds"]["areas"])
            if refused:
                raise PositionError("%d baseplan faces refused: %s" % (len(refused), ", ".join(refused)))
            with open(args.labels, encoding="utf-8-sig") as handle:
                labels = list(csv.DictReader(handle))
            shifts = shifts_of(json.loads(Path(args.plot_shifts).read_text(encoding="utf-8")))
            rows = check_points(position, points, group, face_index.rings_of(plots), {key: generic_types(settings, key) for key in points}, numbering(labels, shifts),
                                block["checkPointLimitMm"], block["gapDecimals"], merges_of(plots))
            summary = summary_of(rows, block["checkPointLimitMm"])
            made["checkPoints"] = rows
            inputs += [parity.input_entry("keyPoints:" + key, path) for key, path in sorted(given.items())]
            inputs += [parity.input_entry("baseplanExact", args.baseplan_exact), parity.input_entry("labels", args.labels), parity.input_entry("plotShifts", args.plot_shifts)]
        if args.out:
            out = Path(args.out)
            plot_relations.refuse_inside_repo(out.resolve())
            out.mkdir(parents=True, exist_ok=True)
            target = out / Path(block["file"]).name
            target.write_text(json.dumps(written(position, summary), indent=1, ensure_ascii=False), encoding="utf-8")
            print(target)
        if args.steps:
            values = settings.values
            for step, items in made.items():
                fields = values["addin"]["parity"]["steps"][step]
                parity.write_step(args.steps, step, "python", "project", {name: entry_of(item, fields) for name, item in items.items()}, values, inputs)
                print("step %s: %d items" % (step, len(items)))
        print("the reference key %s held against %d keys of its group, the set is one" % (reference, len(held)))
        if summary is not None:
            print(summary_line(summary))
            for line in issues_of(rows, block["gapDecimals"]):
                print("issue " + line)
        else:
            print("issue " + NOT_MADE)
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
