import argparse
import json
import re
import sys
from collections import Counter
from pathlib import Path

from common import write_json

FFL_TOLERANCE = 0.0015
NOT_LOADED = "Link is not loaded"
NONE_KEY = "(none)"
FLAT_NUMBER = re.compile(r"^\s*([-+]?\d+(?:[.,]\d+)?)\s*m?\s*$", re.IGNORECASE)
LEADING_INTEGER = re.compile(r"^\s*(\d+)")
# The villa file name carries the unit between ARC and ZZ, for example RRE-WP-NSP-KEO-ARC-DP4-ZZ-MOD-2400.rvt
FILE_UNIT = re.compile(r"-ARC-([A-Z0-9]+)-", re.IGNORECASE)
EXCEL_PLOT = "Plot No."
EXCEL_UNIT = "Unit"
EXCEL_FFL = "FFL"
EXCEL_STEPS = "Steps Needed"
LEVEL_TOLERANCE = 0.0005


def drop_instances(node):
    # An old file repeats the contents of every loaded link, so only the count is kept to save memory
    if isinstance(node.get("instances"), list):
        node["instanceCount"] = len(node.pop("instances"))
    return node


def load(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle, object_hook=drop_instances)


def links_of(data):
    links = []
    for document in data.get("documents") or []:
        links.extend(document.get("links") or [])
    return links


def text_key(value):
    if value is None or str(value).strip() == "":
        return NONE_KEY
    return str(value).strip()


def number_key(value):
    if value is None:
        return NONE_KEY
    rounded = round(float(value), 3)
    return str(int(rounded)) if rounded == int(rounded) else str(rounded)


def sort_key(text):
    return (0, float(text), text) if re.fullmatch(r"-?\d+(\.\d+)?", text) else (1, 0, text)


def ordered(counter):
    return {key: counter[key] for key in sorted(counter, key=sort_key)}


def parse_number(value):
    # Only a plain number counts. Text such as 12.5 m is read, anything with other words is left unread
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value)
    found = FLAT_NUMBER.match(str(value))
    return float(found.group(1).replace(",", ".")) if found else None


def has_field(links, field):
    return sum(1 for link in links if link.get(field) is not None)


def origin_z(link):
    origin = (link.get("transform") or {}).get("origin")
    return origin[2] if isinstance(origin, list) and len(origin) > 2 else None


def mirrored(link):
    return bool((link.get("transform") or {}).get("mirrored"))


def rotation(link):
    return (link.get("transform") or {}).get("rotationDeg")


def plot_key(value):
    # Excel holds 2289 as a number and the link holds "2289" as text, so both go through int when they can
    try:
        return str(int(float(value)))
    except (TypeError, ValueError):
        return text_key(value)


def file_unit(name):
    found = FILE_UNIT.search(name or "")
    return found.group(1).upper() if found else None


def load_excel(path):
    # coords.json of excel_reader.py, one row per plot keyed by the column header
    with open(path, encoding="utf-8") as handle:
        return json.load(handle).get("rows") or []


def leading_integer(value):
    found = LEADING_INTEGER.match(str(value)) if value is not None else None
    return int(found.group(1)) if found else None


def verify(data, rows):
    # A second method for the real run. It reads only prototypes.json and the Excel rows, with its own counting
    links = [link for link in links_of(data) if link.get("plotNumber") is not None]
    excel = {plot_key(row.get(EXCEL_PLOT)): row for row in rows if row.get(EXCEL_PLOT) is not None}
    plots = Counter(plot_key(link["plotNumber"]) for link in links)
    joined = [(link, excel[plot_key(link["plotNumber"])]) for link in links if plot_key(link["plotNumber"]) in excel]
    result = {
        "villaLinks": len(links),
        "excelPlots": len(excel),
        "plots": {
            "linkPlotsInExcel": sum(1 for plot in plots if plot in excel),
            "linkPlotsNotInExcel": sorted((plot for plot in plots if plot not in excel), key=sort_key),
            "excelPlotsWithoutLink": sorted((plot for plot in excel if plot not in plots), key=sort_key),
            "repeated": sorted((plot for plot in plots if plots[plot] > 1), key=sort_key),
        },
    }

    different = [
        {"plot": plot_key(link["plotNumber"]), "prototype": text_key(link.get("prototype")), "excelUnit": text_key(row.get(EXCEL_UNIT)), "fileUnit": file_unit(link.get("file"))}
        for link, row in joined
        if text_key(link.get("prototype")) != text_key(row.get(EXCEL_UNIT))
    ]
    result["prototypeVsExcelUnit"] = {"joined": len(joined), "equal": len(joined) - len(different), "different": different}

    pairs = Counter(
        text_key(link.get("prototype")) + " in file " + text_key(file_unit(link.get("file")))
        for link in links
        if text_key(link.get("prototype")) != text_key(file_unit(link.get("file")))
    )
    result["prototypeVsFile"] = {"links": len(links), "different": ordered(pairs)}

    groups = {}
    for link in links:
        groups.setdefault((text_key(link.get("case")), mirrored(link)), set()).add(number_key(rotation(link)))
    single = sum(1 for link in links if len(groups[(text_key(link.get("case")), mirrored(link))]) == 1)
    result["caseRotation"] = {
        "cases": sorted({key[0] for key in groups}, key=sort_key),
        "combinations": len(groups),
        "combinationsWithOneRotation": sum(1 for values in groups.values() if len(values) == 1),
        "linksInThoseCombinations": single,
    }

    steps = [(leading_integer(link.get("steps")), leading_integer(row.get(EXCEL_STEPS))) for link, row in joined]
    result["stepsVsExcel"] = {
        "joined": len(joined),
        "equal": sum(1 for found, expected in steps if found is not None and found == expected),
        "unreadable": sum(1 for found, expected in steps if found is None or expected is None),
    }

    origin_equal = parameter_equal = lower = higher = unreadable = 0
    largest = 0.0
    for link, row in joined:
        expected = parse_number(row.get(EXCEL_FFL))
        z = origin_z(link)
        parameter = parse_number(link.get("ffl"))
        if expected is None or z is None or parameter is None:
            unreadable += 1
            continue
        origin_equal += 1 if abs(z - expected) <= FFL_TOLERANCE + 1e-9 else 0
        gap = expected - parameter
        if gap > LEVEL_TOLERANCE:
            lower += 1
            largest = max(largest, gap)
        elif gap < -LEVEL_TOLERANCE:
            higher += 1
        else:
            parameter_equal += 1
    result["fflVsExcel"] = {
        "joined": len(joined),
        "originZEqualsExcelWithin1p5mm": origin_equal,
        "parameterEqualsExcel": parameter_equal,
        "parameterLowerThanExcel": lower,
        "parameterHigherThanExcel": higher,
        "largestLowerMm": round(largest * 1000, 1),
        "unreadable": unreadable,
    }
    return result


def floors_of(data):
    floors = []
    for document in data.get("documents") or []:
        floors.extend(document.get("floors") or [])
    return floors


def walls_of(data):
    walls = []
    for document in data.get("documents") or []:
        walls.extend(document.get("walls") or [])
    return walls


def is_ramp(floor):
    return "ramp" in str(floor.get("typeName") or "").lower()


def sketch_points(floor):
    # Files before the rename call the key boundary
    return floor.get("sketchBoundary") or floor.get("boundary") or []


def sketch_z(floors):
    # The z of the sketch outline against the floor top and the level, counted per floor.
    # It is the height of the sketch plane and not an elevation
    counts = {"floors": 0, "oneZ": 0, "equalsTop": 0, "equalsLevel": 0, "equalsNeither": 0}
    for floor in floors:
        zs = {round(point[2], 3) for point in sketch_points(floor) if len(point) > 2}
        if not zs:
            continue
        counts["floors"] += 1
        top = floor.get("topElevation")
        offset = floor.get("heightOffset")
        if len(zs) == 1:
            counts["oneZ"] += 1
            z = next(iter(zs))
            if top is not None and abs(z - top) <= 0.0015:
                counts["equalsTop"] += 1
            elif top is not None and offset is not None and abs(z - (top - offset)) <= 0.0015:
                counts["equalsLevel"] += 1
            else:
                counts["equalsNeither"] += 1
    return counts


def uses_slope(floor):
    info = floor.get("slope") or {}
    return info.get("parameter") is not None or info.get("arrow") is True


def floor_summary(data):
    floors = floors_of(data)
    walls = walls_of(data)
    ramps = [floor for floor in floors if is_ramp(floor)]
    spots = sum(len(document.get("spotElevations") or []) for document in data.get("documents") or [])
    summary = {
        "floors": len(floors),
        "floorTypes": ordered(Counter(text_key(floor.get("typeName")) for floor in floors)),
        "rampFloors": len(ramps),
        "rampTypes": ordered(Counter(text_key(floor.get("typeName")) for floor in ramps)),
        "walls": len(walls),
        "wallTypes": ordered(Counter(text_key(wall.get("typeName")) for wall in walls)),
        "sketchZ": sketch_z(ramps),
        "spotElevations": spots,
        "corners": None,
    }
    if any("cornersSource" in floor for floor in floors):
        shape = [floor for floor in ramps if floor.get("cornersSource") == "shapeEditor"]
        spreads = [max(c[2] for c in floor["corners"]) - min(c[2] for c in floor["corners"]) for floor in shape if floor.get("corners")]
        summary["corners"] = {
            "rampFloors": len(ramps),
            "shapeEditor": len(shape),
            "boundary": sum(1 for floor in ramps if floor.get("cornersSource") == "boundary"),
            "noCorners": sum(1 for floor in ramps if not floor.get("corners")),
            "reportSlope": sum(1 for floor in ramps if uses_slope(floor)),
            "shapeAndSlope": sum(1 for floor in shape if uses_slope(floor)),
            "neither": sum(1 for floor in ramps if floor.get("cornersSource") != "shapeEditor" and not uses_slope(floor)),
            "cornerHeightSpreadOver5mm": sum(1 for spread in spreads if spread > 0.005),
            "largestCornerHeightSpreadMm": round(max(spreads) * 1000, 1) if spreads else 0,
            "plotNumberNull": sum(1 for floor in floors if floor.get("plotNumber") is None),
        }
    return summary


def summarize(data):
    links = links_of(data)
    missing = []

    def need(part, field):
        if not links:
            missing.append({"part": part, "reason": "the file has no links"})
            return False
        if has_field(links, field) == 0:
            missing.append({"part": part, "reason": "no link has the field " + field + ", the file has the old shape"})
            return False
        return True

    summary = {
        "documents": len(data.get("documents") or []),
        "links": len(links),
        "notLoaded": sum(1 for link in links if NOT_LOADED in (link.get("errors") or [])),
        "perFile": ordered(Counter(text_key(link.get("file")) for link in links)),
        "mirroredTotal": sum(1 for link in links if mirrored(link)),
        "rotations": ordered(Counter(number_key(rotation(link)) for link in links)),
    }

    summary["perPrototype"] = None
    summary["mirroredByPrototype"] = None
    if need("perPrototype and mirroredByPrototype", "prototype"):
        counts = Counter(text_key(link.get("prototype")) for link in links)
        summary["perPrototype"] = ordered(counts)
        flipped = Counter(text_key(link.get("prototype")) for link in links if mirrored(link))
        summary["mirroredByPrototype"] = {key: {"links": counts[key], "mirrored": flipped[key]} for key in sorted(counts, key=sort_key)}

    summary["caseVsMirrored"] = None
    summary["caseVsRotation"] = None
    if need("caseVsMirrored and caseVsRotation", "case"):
        cases = sorted({text_key(link.get("case")) for link in links}, key=sort_key)
        summary["caseVsMirrored"] = {
            case: {
                "mirrored": sum(1 for link in links if text_key(link.get("case")) == case and mirrored(link)),
                "notMirrored": sum(1 for link in links if text_key(link.get("case")) == case and not mirrored(link)),
            }
            for case in cases
        }
        summary["caseVsRotation"] = {
            case: ordered(Counter(number_key(rotation(link)) for link in links if text_key(link.get("case")) == case))
            for case in cases
        }

    summary["fflEqualsOriginZ"] = None
    if need("fflEqualsOriginZ", "ffl"):
        match = differ = unreadable = 0
        for link in links:
            ffl = parse_number(link.get("ffl"))
            z = origin_z(link)
            if ffl is None or z is None:
                unreadable += 1
            elif abs(ffl - z) <= FFL_TOLERANCE + 1e-9:
                match += 1
            else:
                differ += 1
        summary["fflEqualsOriginZ"] = {"toleranceMm": 1.5, "unitAssumed": "m", "match": match, "differ": differ, "unreadable": unreadable}

    summary["repeatedPlots"] = None
    summary["plotNameMismatch"] = None
    if need("repeatedPlots and plotNameMismatch", "plotNumber"):
        plots = Counter(text_key(link.get("plotNumber")) for link in links if link.get("plotNumber") is not None)
        summary["repeatedPlots"] = [{"plot": plot, "links": plots[plot]} for plot in sorted(plots, key=sort_key) if plots[plot] > 1]
        summary["plotNameMismatch"] = sum(
            1
            for link in links
            if link.get("plotNumber") is not None
            and (link.get("nameParts") or {}).get("plot") is not None
            and text_key(link["plotNumber"]) != text_key(link["nameParts"]["plot"])
        )

    summary["notComputable"] = missing
    summary["floorSummary"] = floor_summary(data)
    return summary


def render(summary):
    lines = ["documents " + str(summary["documents"]), "links " + str(summary["links"]), "not loaded " + str(summary["notLoaded"])]
    lines.append("links per file (" + str(len(summary["perFile"])) + " files)")
    lines.extend("  " + key + ": " + str(count) for key, count in summary["perFile"].items())
    if summary["perPrototype"] is not None:
        lines.append("links per prototype parameter")
        lines.extend("  " + key + ": " + str(count) for key, count in summary["perPrototype"].items())
        lines.append("mirrored per prototype")
        lines.extend("  " + key + ": " + str(item["mirrored"]) + " of " + str(item["links"]) for key, item in summary["mirroredByPrototype"].items())
    lines.append("mirrored links " + str(summary["mirroredTotal"]))
    lines.append("rotation values")
    lines.extend("  " + key + ": " + str(count) for key, count in summary["rotations"].items())
    if summary["caseVsMirrored"] is not None:
        lines.append("CASE against mirrored")
        lines.extend("  " + key + ": " + str(item["mirrored"]) + " mirrored, " + str(item["notMirrored"]) + " not" for key, item in summary["caseVsMirrored"].items())
        lines.append("CASE against rotation")
        for case, counts in summary["caseVsRotation"].items():
            lines.append("  " + case + ": " + ", ".join(key + " x" + str(count) for key, count in counts.items()))
    if summary["fflEqualsOriginZ"] is not None:
        item = summary["fflEqualsOriginZ"]
        lines.append("Prototype-FFL equal to link origin Z within 1.5 mm, read as metres")
        lines.append("  match " + str(item["match"]) + ", differ " + str(item["differ"]) + ", unreadable " + str(item["unreadable"]))
    if summary["repeatedPlots"] is not None:
        lines.append("plot numbers on more than one link: " + str(len(summary["repeatedPlots"])))
        lines.extend("  " + item["plot"] + ": " + str(item["links"]) + " links" for item in summary["repeatedPlots"])
        lines.append("plot parameter differs from the plot in the name: " + str(summary["plotNameMismatch"]))
    lines.extend(render_floors(summary["floorSummary"]))
    if summary.get("verify") is not None:
        lines.extend(render_verify(summary["verify"]))
    for item in summary["notComputable"]:
        lines.append("NOT COMPUTED " + item["part"] + ": " + item["reason"])
    return "\n".join(lines)


def render_floors(info):
    lines = ["floors " + str(info["floors"]) + ", ramp floors " + str(info["rampFloors"]) + ", walls " + str(info["walls"])]
    lines.extend("  floor type " + key + ": " + str(count) for key, count in info["floorTypes"].items())
    lines.extend("  wall type " + key + ": " + str(count) for key, count in info["wallTypes"].items())
    z = info["sketchZ"]
    lines.append("ramp floor sketch z: " + str(z["floors"]) + " floors, one z on " + str(z["oneZ"]) + ", equals top " + str(z["equalsTop"]) + ", equals level " + str(z["equalsLevel"]) + ", equals neither " + str(z["equalsNeither"]))
    lines.append("spot elevations " + str(info["spotElevations"]))
    if info["corners"] is None:
        lines.append("NOT COMPUTED corners and slope: no floor has cornersSource, the file has the old shape")
    else:
        item = info["corners"]
        lines.append("ramp floor corners: shapeEditor " + str(item["shapeEditor"]) + ", boundary " + str(item["boundary"]) + ", no corners " + str(item["noCorners"]))
        lines.append("ramp floor slope: report a slope " + str(item["reportSlope"]) + ", shape editing and slope " + str(item["shapeAndSlope"]) + ", neither " + str(item["neither"]))
        lines.append("corner height spread: over 5 mm on " + str(item["cornerHeightSpreadOver5mm"]) + " floors, largest " + str(item["largestCornerHeightSpreadMm"]) + " mm")
        lines.append("floors with plotNumber null " + str(item["plotNumberNull"]))
    return lines


def render_verify(item):
    plots = item["plots"]
    lines = ["check against the Excel, " + str(item["villaLinks"]) + " villa links and " + str(item["excelPlots"]) + " Excel plots"]
    lines.append("  plots: " + str(plots["linkPlotsInExcel"]) + " link plots in Excel, not in Excel " + str(len(plots["linkPlotsNotInExcel"])) + ", Excel plots with no link " + str(len(plots["excelPlotsWithoutLink"])) + ", repeated " + str(len(plots["repeated"])))
    unit = item["prototypeVsExcelUnit"]
    lines.append("  prototype equals Excel unit: " + str(unit["equal"]) + " of " + str(unit["joined"]))
    lines.extend("    plot " + row["plot"] + ": prototype " + row["prototype"] + ", Excel " + row["excelUnit"] + ", file " + text_key(row["fileUnit"]) for row in unit["different"])
    lines.append("  prototype differs from the villa file unit on " + str(sum(item["prototypeVsFile"]["different"].values())) + " links")
    lines.extend("    " + key + ": " + str(count) for key, count in item["prototypeVsFile"]["different"].items())
    case = item["caseRotation"]
    lines.append("  CASE letters " + ",".join(case["cases"]) + ", " + str(case["combinations"]) + " combinations of CASE and mirror, " + str(case["combinationsWithOneRotation"]) + " with one rotation, " + str(case["linksInThoseCombinations"]) + " of " + str(item["villaLinks"]) + " links covered")
    steps = item["stepsVsExcel"]
    lines.append("  steps parameter equals Excel steps: " + str(steps["equal"]) + " of " + str(steps["joined"]) + ", unreadable " + str(steps["unreadable"]))
    ffl = item["fflVsExcel"]
    lines.append("  link origin Z equals Excel FFL within 1.5 mm: " + str(ffl["originZEqualsExcelWithin1p5mm"]) + " of " + str(ffl["joined"]))
    lines.append("  Prototype-FFL parameter: equal to Excel " + str(ffl["parameterEqualsExcel"]) + ", lower " + str(ffl["parameterLowerThanExcel"]) + " (up to " + str(ffl["largestLowerMm"]) + " mm), higher " + str(ffl["parameterHigherThanExcel"]) + ", unreadable " + str(ffl["unreadable"]))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(description="Count the Revit links in prototypes.json by file, prototype, mirror, rotation and CASE.")
    parser.add_argument("prototypes", help="prototypes.json from the Revit reader")
    parser.add_argument("out_dir", nargs="?", default=".", help="where link_summary.json is written")
    parser.add_argument("--excel", help="coords.json of the schedule workbook, adds the check of the links against the Excel")
    args = parser.parse_args(argv)
    data = load(args.prototypes)
    summary = summarize(data)
    summary["verify"] = verify(data, load_excel(args.excel)) if args.excel else None
    write_json(Path(args.out_dir) / "link_summary.json", summary)
    print(render(summary))
    return 0


if __name__ == "__main__":
    sys.exit(main())
