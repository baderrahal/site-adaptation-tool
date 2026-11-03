"""R407 to R410, round 4i. The design basis of a build, designBasis of config/project.json read once per run: rev6 the Site Adaptation
Manual Rev 06 of the client, the other mode of designBasis.modes the past job as the keys measured it. Under the past job nothing but the
mode is read, so a build is the build of before round 4i. Under rev6 El @ Entry is the input and the ramp slope the result (R408), the road level comes
from the first of the surfaces designBasis.roadLevel names in order that covers a corner (R409, R490, R491), a zone mostly off them stops the build
(R493), the back yard is a grade with no floor (R410) and designBasis.split.test turns on the split of a cluster by slope and steps (R414).
DesignBasis.cs of the C# gives the same, each refusal with the same line. docs/design/16-design-basis.md"""
import csv
import math
import re
from collections import Counter
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
BOTH_SURFACES = "designBasis.roadLevel holds both surface and surfaces, a build under rev6 takes its road surfaces from surfaces alone (R490)"
NO_SURFACES = "designBasis.roadLevel names no road surface, a build under rev6 takes the road level from designBasis.roadLevel.surfaces in their order (R490)"
BAD_SURFACES = "designBasis.roadLevel.surfaces must be a list in order, not empty, of non empty unique texts (R490)"
# R493. The name of a plot whose zone column is blank, and the sentence of a zone that stops a build
NO_ZONE = "no zone"
NO_ROAD_ZONE = "%s: %d of %d plots have no road level on the road surfaces named (%s). What to do: add the road surface of this zone in Set up."
NO_ROAD_END = " (R493)"
NOT_ROAD = "%s is no road surface: %s (R485)"
NOT_ROAD_SHAPE = "designBasis.roadLevel.notRoad must be an object of file stem to why (R485)"


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


def take_mode(values, mode):
    """R484. The project mode of the settings laid over designBasis.mode of the values of a config, once where a run loads it, so every reader of the mode
    takes it. None leaves the values as they are, a settings file written before the project mode reads the mode of config. A mode outside designBasis.modes
    is refused by its name, ProjectModes.Apply of the C# gives the same line."""
    if mode is None:
        return values
    modes = (values.get("designBasis") or {}).get("modes")
    if not isinstance(modes, list) or mode not in modes:
        refuse("the project mode %s is no mode of designBasis.modes %s (R484)" % (mode, ", ".join(str(item) for item in modes) if isinstance(modes, list) else "none"))
    values["designBasis"]["mode"] = mode
    return values


def holds_rev6(settings):
    """R487. Whether the applied mode of a config, its values or its profile, is rev6, RoadSurfaceChoices.IsRev6 of the C#."""
    block = (settings.values if isinstance(settings, project_profile.ProjectProfile) else settings).get("designBasis")
    return isinstance(block, dict) and block.get("mode") == REV6


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


def road_block(block):
    road = block.get("roadLevel")
    return road if isinstance(road, dict) else {}


def not_taken_why(site_build, name):
    """R490. The reason of siteBuild.roadFolder.notTaken that names this surface by its short name, as a whole name, None for none. Those reasons are the
    one list of R344 of what is no road, read in the Roads folder, so no list of names stands in code."""
    reasons = ((site_build or {}).get("roadFolder") or {}).get("notTaken")
    whole = re.compile(r"(?<!\w)" + re.escape(name.rpartition("__")[2] or name) + r"(?!\w)")
    return next((reason for reason in (reasons.values() if isinstance(reasons, dict) else ()) if isinstance(reason, str) and whole.search(reason)), None)


def surfaces_of(block, site_build):
    """R490. designBasis.roadLevel.surfaces, the road surfaces of a build under rev6 in order, non empty and unique texts, each no surface
    siteBuild.roadFolder.notTaken names. The one surface of an old config reads as a list of one, both keys together, no value or a bad value refused by name."""
    surfaces = named_surfaces(block)
    reasons = not_road_of(block)
    for name in surfaces:
        why = not_taken_why(site_build, name)
        if why is not None:
            refuse("designBasis.roadLevel.surfaces names %s, which is no road surface: %s (R490)" % (name, why))
        why = next((reason for stem, reason in reasons.items() if stem.lower() == name.lower()), None)
        if why is not None:
            refuse(NOT_ROAD % (name, why))
    return surfaces


def not_road_of(block):
    """R485. designBasis.roadLevel.notRoad, file stem to why, the grading files of the road folder that are no road, read under rev6 alone. None is an empty list."""
    reasons = road_block(block).get("notRoad", {})
    if not isinstance(reasons, dict) or not all(isinstance(why, str) and why and stem for stem, why in reasons.items()):
        refuse(NOT_ROAD_SHAPE)
    return reasons


def named_surfaces(block):
    road = road_block(block)
    if "surface" in road and "surfaces" in road:
        refuse(BOTH_SURFACES)
    if "surfaces" not in road:
        surface = road.get("surface")
        if not isinstance(surface, str) or not surface:
            refuse(NO_SURFACES)
        return [surface]
    surfaces = road["surfaces"]
    if not isinstance(surfaces, list) or not surfaces or not all(isinstance(name, str) and name for name in surfaces) or len(set(surfaces)) != len(surfaces):
        refuse(BAD_SURFACES)
    return list(surfaces)


def stop_share_of(block):
    """R493. designBasis.roadLevel.noRoadStopShare, a number from 0 to 1 and no bool, refused by name. A config with none (CHOSEN) reads 0, so it stops at
    any plot with no road level and never loses plots silently."""
    road = road_block(block)
    if "noRoadStopShare" not in road:
        return 0
    share = road["noRoadStopShare"]
    if not finite(share) or not 0 <= share <= 1:
        refuse("designBasis.roadLevel.noRoadStopShare must be a number from 0 to 1, it holds %r (R493)" % (share,))
    return share


def check_yard(block):
    """R410. backYard.floor, false under rev6 until Q225 is answered, true refused by name."""
    floor = (block.get("backYard") or {}).get("floor")
    if floor is True:
        refuse(NO_FLOOR)
    if floor is not False:
        refuse("designBasis.backYard.floor must be true or false, it holds %r (R410)" % (floor,))


def propose_of(block):
    """R411. designBasis.clusters.propose, true or false, any other value refused."""
    propose = (block.get("clusters") or {}).get("propose")
    if not isinstance(propose, bool):
        refuse("designBasis.clusters.propose must be true or false, it holds %r (R411)" % (propose,))
    return propose


def whole(value):
    return isinstance(value, int) and not isinstance(value, bool)


def split_of(block):
    """R414. designBasis.split: test true or false, units the size of a cluster it tests and walls the walls it tries in order, each a wall
    after a unit inside the cluster, any other value refused."""
    split = block.get("split") if isinstance(block.get("split"), dict) else {}
    test, units, walls = split.get("test"), split.get("units"), split.get("walls")
    if not isinstance(test, bool):
        refuse("designBasis.split.test must be true or false, it holds %r (R414)" % (test,))
    if not whole(units) or units < 2 or not isinstance(walls, list) or not walls or not all(whole(wall) and 0 < wall < units for wall in walls):
        refuse("designBasis.split.units must be a whole number of 2 or more and designBasis.split.walls a list of the walls it tries in order, each a "
               "wall after a unit inside the cluster (R414)")
    return {"test": test, "units": units, "walls": list(walls)}


def basis_of(settings):
    """R407. The design basis of a build, read once: under the past job the mode alone, under rev6 also the door level source with what it
    reads (R408), the road surfaces in order (R490) with the limit of plots with no road level (R493), the back yard floor held false (R410),
    whether the build proposes clusters (R411) and the split test of a cluster (R414)."""
    block = block_of(settings)
    mode = mode_of(settings)
    if mode != REV6:
        return {"mode": mode}
    check_yard(block)
    return {"mode": mode, "door": door_of(block), "surfaces": surfaces_of(block, settings.values.get("siteBuild")), "noRoadStopShare": stop_share_of(block), "propose": propose_of(block),
            "split": split_of(block)}


def is_rev6(basis):
    return basis is not None and basis["mode"] == REV6


def proposes(basis):
    """R411. True for a build under rev6 with designBasis.clusters.propose, the one that proposes a cluster and a mirror flag per unit plot."""
    return is_rev6(basis) and basis["propose"]


def splits(basis):
    """R414. True for a build under rev6 with designBasis.split.test, the one that tests each cluster for a split by slope and steps."""
    return is_rev6(basis) and basis["split"]["test"]


def road_names(basis):
    """R490. The road surfaces of a build under rev6, those of designBasis.roadLevel in their order."""
    return list(basis["surfaces"])


def road_order(settings):
    """R490. The road surfaces of the values of a config under rev6 in their order, None under the past job, for a reader that builds nothing."""
    block = settings.get("designBasis") or {}
    return surfaces_of(block, settings.get("siteBuild")) if block.get("mode") == REV6 else None


def check_surfaces(basis, folder):
    """R490. Each surface of designBasis.roadLevel is a LandXML file of the landxml folder, else refused by name."""
    for name in basis["surfaces"]:
        if not (Path(folder) / (name + ".xml")).is_file():
            refuse("designBasis.roadLevel.surfaces names %s, which is no surface of the inputs, the landxml folder holds no %s.xml (R490)" % (name, name))


def road_gaps(no_road):
    """R493. The plots of summary.noRoadLevel that have a front and lack a road level at a corner. A plot with no front has no corner to lack a road at,
    so it is no missing surface, it stays in the summary."""
    return [entry["plot"] for entry in no_road if entry["corners"]]


def no_road_stop(basis, reached, gaps, rows):
    """R493. Under rev6 the message that stops a build, one sentence per zone in zone order whose share of its reached unit plots that are gaps (road_gaps)
    lies above noRoadStopShare, else None. A zone mostly off the named surfaces means a missing surface, a few plots stay listed in the summary. The
    zone is the zone column of the plot table, blank reads as no zone. Exactly at the share does not stop."""
    if not is_rev6(basis):
        return None

    def zone_of(number):
        return (rows.get(number, {}).get("zone") or "").strip() or NO_ZONE

    plots, lacking = Counter(zone_of(number) for number in reached), Counter(zone_of(number) for number in gaps)
    names = ", ".join(name.rpartition("__")[2] or name for name in basis["surfaces"])
    lines = [NO_ROAD_ZONE % (zone, lacking[zone], plots[zone], names) for zone in sorted(plots) if lacking[zone] / plots[zone] > basis["noRoadStopShare"]]
    return " ".join(lines) + NO_ROAD_END if lines else None


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
