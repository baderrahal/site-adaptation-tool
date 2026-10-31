"""R407 to R410, round 4i. The design basis of a build, designBasis of config/project.json read once per run: rev6 the Site Adaptation
Manual Rev 06 of the client, the other mode of designBasis.modes the past job as the keys measured it. Under the past job nothing but the
mode is read, so a build is the build of before round 4i. Under rev6 El @ Entry is the input and the ramp slope the result (R408), the road level comes
from the one surface designBasis.roadLevel names (R409) and the back yard is a grade with no floor (R410). DesignBasis.cs of the C#
gives the same, each refusal with the same line. docs/design/16-design-basis.md"""
import csv
import math
import re
from pathlib import Path

import common
import project_profile

REV6 = "rev6"
# designBasis.modes holds rev6 and the one mode of the past job, whose name lives in config alone (R63)
MODE_COUNT = 2
CIVIL_FILE = "civilFile"
ROAD_PLUS_SLOPE = "roadPlusSlope"
# typologies of the rules profile, the attachment of a townhouse, whose back yard 2.3.C grades toward the back
BOTH_SIDES = "both_sides"
FRONT = "front"
BACK = "back"
# The decimals the plan writes slopePercent of a ramp at (R2), the slope the list outside the manual holds against front_min to front_max
SLOPE_DECIMALS = 3
# A number of the civil file as Python float and double.Parse of the C# both read it, no nan, inf, blank or underscore
NUMBER = re.compile(r"^[+-]?(\d+(\.\d*)?|\.\d+)([eE][+-]?\d+)?$")
NO_FLOOR = ("designBasis.backYard.floor is true, the manual gives no level for a yard floor under rev6, so none is placed until Q225 is "
            "answered (R410)")


def refuse(text):
    raise project_profile.ProfileError(text)


def common_text(value):
    return "none" if value is None else str(value)


def block_of(settings):
    """designBasis of the settings, refused when the settings hold none."""
    block = settings.get("designBasis")
    if not isinstance(block, dict):
        refuse("designBasis of the config is no object (R407)")
    return block


def mode_of(settings):
    """R407. designBasis.mode, one of designBasis.modes, any other value refused by its name. modes holds rev6 and the mode of the past job."""
    block = block_of(settings)
    modes = block.get("modes")
    mode = block.get("mode")
    listed = ", ".join(str(item) for item in modes) if isinstance(modes, list) else "none"
    if not isinstance(modes, list) or len(set(map(str, modes))) != MODE_COUNT or REV6 not in modes or not all(isinstance(item, str) for item in modes):
        refuse("designBasis.modes must hold rev6 and the mode of the past job, it holds %s (R407)" % listed)
    if not isinstance(mode, str) or mode not in modes:
        refuse("designBasis.mode %s is no mode of designBasis.modes %s (R407)" % (common_text(mode), listed))
    return mode


def finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def civil_levels(path, plot_column, level_column):
    """R408. El @ Entry of each plot of the civil file, the CSV read as plan_from_book.csv_rows reads one, by the text of plotColumn and the
    number of levelColumn. A file that is missing, a column the header lacks, a level that is no finite number and a plot twice are refused."""
    if not path or not Path(path).is_file():
        refuse("designBasis.doorLevel.civilFile.path %s is no file, the civil file of the door levels is missing (R408)" % common_text(path))
    try:
        with open(path, encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle, strict=True)
            rows = list(reader)
            header = reader.fieldnames or []
    except csv.Error as error:
        raise project_profile.ProfileError("%s is no CSV as Python reads it: %s (R408)" % (Path(path).name, error)) from error
    for column, name in ((plot_column, "plotColumn"), (level_column, "levelColumn")):
        if not isinstance(column, str) or column not in header:
            refuse("the civil file %s holds no column %s, designBasis.doorLevel.civilFile.%s (R408)" % (Path(path).name, common_text(column), name))
    found = {}
    for row in rows:
        plot, text = (row.get(plot_column) or "").strip(), (row.get(level_column) or "").strip()
        if not NUMBER.match(text) or not math.isfinite(float(text)):
            refuse("the civil file %s gives plot %s the level %r, which is no finite number (R408)" % (Path(path).name, plot, text))
        if plot in found:
            refuse("the civil file %s holds plot %s twice (R408)" % (Path(path).name, plot))
        found[plot] = float(text)
    return found


def slope_table(door):
    """R408. doorLevel.slopePercent, a finite number per unit code and the default, refused otherwise."""
    table = door.get("slopePercent")
    if not isinstance(table, dict) or not finite(table.get("default")) or not all(finite(value) for value in table.values()):
        refuse("designBasis.doorLevel.slopePercent must hold a finite number for default and for each unit code it names (R408)")
    return dict(table)


def door_of(block):
    """R408. The door level source of rev6 with what it reads: the levels of the civil file, or the slope of each unit."""
    door = block.get("doorLevel")
    if not isinstance(door, dict):
        refuse("designBasis.doorLevel of the config is no object (R408)")
    sources, source = door.get("sources"), door.get("source")
    if not isinstance(sources, list) or source not in sources or source not in (CIVIL_FILE, ROAD_PLUS_SLOPE):
        refuse("designBasis.doorLevel.source %s is no source of designBasis.doorLevel.sources, El @ Entry comes from civilFile or roadPlusSlope (R408)"
               % common_text(source))
    if source == CIVIL_FILE:
        civil = door.get("civilFile") or {}
        return {"source": source, "path": civil.get("path"), "levels": civil_levels(civil.get("path"), civil.get("plotColumn"), civil.get("levelColumn"))}
    return {"source": source, "slopes": slope_table(door)}


def surface_of(block):
    """R409. designBasis.roadLevel.surface, the one road surface of a build under rev6, a name of text."""
    surface = (block.get("roadLevel") or {}).get("surface")
    if not isinstance(surface, str) or not surface:
        refuse("designBasis.roadLevel.surface names no road surface, a build under rev6 takes the road level from the one surface it names (R409)")
    return surface


def check_yard(block):
    """R410. backYard.floor, false under rev6 until Q225 is answered, true refused by name."""
    floor = (block.get("backYard") or {}).get("floor")
    if floor is True:
        refuse(NO_FLOOR)
    if floor is not False:
        refuse("designBasis.backYard.floor must be true or false, it holds %r (R410)" % (floor,))


def basis_of(settings):
    """R407. The design basis of a build, read once: under the past job the mode alone, under rev6 also the door level source with what it
    reads (R408), the one road surface (R409) and the back yard floor held false (R410)."""
    block = block_of(settings)
    mode = mode_of(settings)
    if mode != REV6:
        return {"mode": mode}
    check_yard(block)
    return {"mode": mode, "door": door_of(block), "surface": surface_of(block)}


def is_rev6(basis):
    return basis is not None and basis["mode"] == REV6


def road_names(basis):
    """R409. The road surfaces of a build under rev6, the one surface of designBasis.roadLevel."""
    return [basis["surface"]]


def check_surfaces(basis, folder):
    """R409. The surface of designBasis.roadLevel is a LandXML file of the landxml folder, else refused by name."""
    if not (Path(folder) / (basis["surface"] + ".xml")).is_file():
        refuse("designBasis.roadLevel.surface %s is no surface of the inputs, the landxml folder holds no %s.xml (R409)" % (basis["surface"], basis["surface"]))


def door_entry(basis, number, unit, front, depth):
    """R408. El @ Entry of a plot under rev6, unrounded, and why it has none: the level of the civil file, or the road at front plus
    slopePercent of the unit, else its default, over the ramp depth. front holds the road levels at the two front corners."""
    door = basis["door"]
    if door["source"] == CIVIL_FILE:
        if number not in door["levels"]:
            return None, "the civil file %s holds no row of plot %s, so no El @ Entry (R408)" % (Path(door["path"]).name, number)
        return door["levels"][number], None
    slope = door["slopes"].get(unit, door["slopes"]["default"])
    return (front[0] + front[1]) / 2.0 + slope / 100.0 * depth, None


def slope_of(entry, front, depth):
    """R408. The ramp slope in percent, the result: El @ Entry less the road at front over the ramp depth."""
    return (entry - (front[0] + front[1]) / 2.0) / depth * 100.0


def outside_manual(slope, slopes):
    """R408. True for a slope outside slopes_percent front_min to front_max of the rules profile, manual 2.3.A, as the plan writes it."""
    written = common.fixed(slope, SLOPE_DECIMALS)
    return written < slopes["front_min"] or written > slopes["front_max"]


def yard_grade(unit, profile):
    """R410. The grade of the back yard of a unit plot by the manual: a unit of attachment both_sides, a townhouse, toward the back at
    townhouse_back_rev6 (2.3.C), any other toward the front within back_rev6_min to back_rev6_max (2.3.B), both in percent."""
    slopes = profile.get("slopes_percent")
    attachment = ((profile.get("typologies") or {}).get(unit) or {}).get("attachment")
    if attachment == BOTH_SIDES:
        return {"toward": BACK, "minPercent": slopes["townhouse_back_rev6"], "maxPercent": slopes["townhouse_back_rev6"]}
    return {"toward": FRONT, "minPercent": slopes["back_rev6_min"], "maxPercent": slopes["back_rev6_max"]}
