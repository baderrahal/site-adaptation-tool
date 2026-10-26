"""R353. The project position file of button 1 and its check points, the Python reference of the command position of the worker.

The position file is written from the read of an answer key (R311, R331), the reference key of majority.siteReference, outside git
(Q068): the project position of the internal origin in shared coordinates with its angle, the survey point and the project base point in
internal and shared coordinates, the levels with their elevations and the one Elevation Base they are measured from, and the units. It
is held against keyRules.site of the level profile, the angle, the level names, their steps and the level base, and refused when one
differs. The check points of button 1 are the three control points the location plan prints, the least and the greatest east and north
of the centroids of every face of the baseplan (plots min and plots max, as R211 takes them), the survey point and the project base
point, each a shared point taken to its internal point by SiteCoordinates of R211 from the position file, and by a second route, the
survey point pair of the key read, internal = Rz(-angle) times (shared less the shared survey point) plus the internal survey point. A
point passes when the gap of the two, in mm at addin.position.gapDecimals by R385, is at most addin.position.checkPointLimitMm. Values
are written by R385 at 3 decimals, the angle at 6, as R311 gives them. The C# of the worker gives the same (ProjectPositionFile of Common).
"""
import argparse
import json
import math
import sys
from pathlib import Path

import cad_units
import common
import face_rings
import link_summary
import location_plan
import parity
import plot_relations
import project_profile
import yard_rule

FORMAT = "site-adaptation-position"
FORMAT_VERSION = 1
STEPS = ("position", "checkPoints")
# A point or a level of the file is the one of the rules within half a mm, an angle within a millionth of a degree, as R331 holds a read
SAME_MM = 0.5
SAME_DEG = 1e-6


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
        "units": {name: site["units"].get(name) for name in ("system", "length", "lengthSystem")},
    }


def against_rules(position, site_rules):
    """R353. What of the position file differs from keyRules.site: the angle, the level names in order, the step of each level over the
    lowest within SAME_MM and the level base. [] when none does."""
    found = []
    if site_rules.get("angleToTrueNorthDeg") is None or abs(position["projectPosition"]["angleDeg"] - site_rules["angleToTrueNorthDeg"]) > SAME_DEG:
        found.append("angle %s, keyRules.site.angleToTrueNorthDeg %s" % (position["projectPosition"]["angleDeg"], site_rules.get("angleToTrueNorthDeg")))
    names = [level["name"] for level in position["levels"]]
    if names != site_rules.get("levels"):
        found.append("levels %s, keyRules.site.levels %s" % (names, site_rules.get("levels")))
    lowest = position["levels"][0]["elevationM"]
    for level in position["levels"][1:]:
        rule = (site_rules.get("levelStepsM") or {}).get(level["name"])
        if rule is None or abs(level["elevationM"] - lowest - rule) * 1000.0 > SAME_MM:
            found.append("the step of %s, keyRules.site.levelStepsM %s" % (level["name"], rule))
    if position["levelBase"] != site_rules.get("levelBase"):
        found.append("level base %s, keyRules.site.levelBase %s" % (position["levelBase"], site_rules.get("levelBase")))
    return found


def to_internal(position, shared):
    """R211. SiteCoordinates.ToInternal: the internal point of a shared point by the project position of the file."""
    at = position["projectPosition"]
    angle = math.radians(at["angleDeg"])
    east, north = shared[0] - at["eastM"], shared[1] - at["northM"]
    return [math.cos(angle) * east + math.sin(angle) * north, -math.sin(angle) * east + math.cos(angle) * north, shared[2] - at["elevationM"]]


def by_survey_pair(document, shared):
    """R353, the second route: the internal point of a shared point by the survey point pair of the key read and its angle to true
    north, never by its project position."""
    survey = document["surveyPoint"]
    angle = math.radians(document["angleToTrueNorthDeg"])
    east, north = shared[0] - survey["sharedPosition"][0], shared[1] - survey["sharedPosition"][1]
    return [math.cos(angle) * east + math.sin(angle) * north + survey["position"][0], -math.sin(angle) * east + math.cos(angle) * north + survey["position"][1],
            shared[2] - survey["sharedPosition"][2] + survey["position"][2]]


def cad_points(controls, centroids, position):
    """R353. The shared points of the check points of button 1 in their order: control 1 to 3 at elevation 0, plots min and plots max of
    the face centroids at elevation 0, the survey point and the project base point of the position file."""
    if not centroids:
        raise PositionError("the baseplan holds no face, the check points take plots min and plots max from its faces (R353)")
    points = [("control %d" % (k + 1), [east, north, 0.0]) for k, (east, north) in enumerate(controls)]
    xs, ys = [point[0] for point in centroids], [point[1] for point in centroids]
    points += [("plots min", [min(xs), min(ys), 0.0]), ("plots max", [max(xs), max(ys), 0.0])]
    points += [("survey point", list(position["surveyPoint"]["sharedM"])), ("project base point", list(position["projectBasePoint"]["sharedM"]))]
    return points


def check_points(position, controls, centroids, document, limit_mm, decimals):
    """R353. Each check point by name: its shared point, its internal point by the position file, by the survey pair of the key read
    when a read is given, the gap of the two in mm at decimals by R385 and whether it passes at limit_mm."""
    rows = {}
    for name, shared in cad_points(controls, centroids, position):
        internal = to_internal(position, shared)
        key = by_survey_pair(document, shared) if document is not None else None
        gap = math.dist(internal, key) * 1000.0 if key is not None else None
        rows[name] = {"sharedM": shared, "internalM": internal, "keyInternalM": key, "gapMm": gap,
                      "pass": None if gap is None else common.fixed(gap, decimals) <= limit_mm}
    return rows


def centroids_of(plots):
    """The centroid of every face of the baseplan read by its label, as yard_rule.face_shapes joins the rings of one label."""
    faces, _ = yard_rule.face_shapes(plots, 0)
    return [(face.centroid.x, face.centroid.y) for _, face in sorted(faces.items(), key=lambda item: int(item[0]))]


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


def position_step(position):
    """R353. The step position of the parity, one item."""
    return {"position": {
        "key": position["key"], "projectPositionM": [position["projectPosition"][name] for name in ("eastM", "northM", "elevationM")],
        "angleDeg": position["projectPosition"]["angleDeg"], "surveyInternalM": position["surveyPoint"]["internalM"],
        "surveySharedM": position["surveyPoint"]["sharedM"], "baseInternalM": position["projectBasePoint"]["internalM"],
        "baseSharedM": position["projectBasePoint"]["sharedM"], "levelNames": [level["name"] for level in position["levels"]],
        "levelElevationsM": [level["elevationM"] for level in position["levels"]],
        "levelProjectElevationsM": [level["projectElevationM"] for level in position["levels"]], "levelBase": position["levelBase"]}}


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


def level_profile_site(settings):
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    name = (profile.get("levelProfiles") or {}).get("default")
    return (((profile.get("levelProfiles") or {}).get(name) or {}).get("keyRules") or {}).get("site") or {}


def main(argv=None):
    parser = argparse.ArgumentParser(description="R353. The project position file of button 1 from a key read, and its check points.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--key-read", required=True, help="the reader output of the reference key of majority.siteReference")
    parser.add_argument("--out", help="the folder of the position file, outside any git work tree, the file name that of addin.position.file")
    parser.add_argument("--plot-numbers", help="the location plan PDF, its control points")
    parser.add_argument("--baseplan", help="R388. plots.json of the baseplan read, the one cadReads names when left out")
    parser.add_argument("--steps", help="a folder outside any git work tree for step_position_python.json and step_checkPoints_python.json")
    args = parser.parse_args(argv)
    try:
        if args.steps:
            for step in STEPS:
                parity.clear_step(args.steps, step, "python")
        settings = project_profile.load_settings(args.settings)
        block = settings_of(settings)
        path = args.key_read
        document = document_of(json.loads(Path(path).read_text(encoding="utf-8")))
        position = position_of(document, key_of(settings))
        differ = against_rules(position, level_profile_site(settings))
        if differ:
            raise PositionError("the key read differs from keyRules.site of the level profile: " + ", and ".join(differ))
        inputs = [parity.input_entry("config", args.settings), parity.input_entry("keyRead", path)]
        made = {"position": position_step(position)}
        rows = None
        if args.plot_numbers:
            plan = settings.get("dataBook.locationPlan")
            controls = [stated for _, stated in location_plan.control_texts(location_plan.read_page(args.plot_numbers, plan, "control")["words"])]
            baseplan = cad_units.read_input(settings, "baseplan", args.baseplan)
            plots = link_summary.load(baseplan)["plots"]
            # R345. The faces of the baseplan are refused as the build and the C# refuse them
            refused = face_rings.refusals(plots, settings.get("addin")["parity"]["kinds"]["areas"])
            if refused:
                raise PositionError("%d baseplan faces refused: %s" % (len(refused), ", ".join(refused)))
            rows = check_points(position, controls, centroids_of(plots), document, block["checkPointLimitMm"], block["gapDecimals"])
            made["checkPoints"] = rows
            inputs += [parity.input_entry("plotNumbers", args.plot_numbers), parity.input_entry("baseplanPlots", str(baseplan))]
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
        if rows is not None:
            print("check points %d, pass %d at %s mm, largest gap %s mm" % (
                len(rows), sum(1 for row in rows.values() if row["pass"]), block["checkPointLimitMm"], common.fixed(max(row["gapMm"] for row in rows.values()), block["gapDecimals"])))
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
