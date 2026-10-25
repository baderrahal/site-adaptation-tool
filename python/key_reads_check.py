"""R323. The second method of the key reads of Round ADDIN Phase 2, json, math and shapely only, no code of the reader.

Four routes other than the reader sections the counts of R322 come from:
- the categories of R311 in the same read, the elements per category counted by Revit apart from the sections, for the links,
  walls, floors (by the Category parameter of each floor) and spot elevations
- the bytes of the key copy, the Revit links its TransmissionData names by rvt_info.py (R203), against the link files of the read
- the floor top under each spot: the value of a spot (R320) hosted on a floor of the read against the top of that floor at the spot,
  from the outline of R52 and the corners of R34 the same read holds
- the per plot rule of the TU keys: each unit plot holds one link, one generic floor (FFL on IME and IMF), one yard and one ramp,
  each element counted by its own plot parameter, which is also the second method of the floor kinds of R321
Besides them a repeat read: the Phase 1 read of the same key copy by an older reader in another window, by element id. It shows the
reader gives the same on the same bytes and is no second method. Written to key_reads_check.json in the reads folder, counts, ids
and gaps only.
"""
import argparse
import math
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.geometry import Point
from shapely.strtree import STRtree

import key_reads
import link_summary
import plan_from_past
import plot_relations
import project_profile
import rvt_info
from common import write_json

OUTPUT = "key_reads_check.json"
SEEDS = (14, 7)
SAMPLE = 20
# Revit names of the categories R311 counts the sections by, and of the spot elevation base that is the survey point
CATEGORY_OF = {"links": "RVT Links", "walls": "Walls", "floors": "Floors", "spots": "Spot Elevations"}
SECTION_OF = {"links": "links", "walls": "walls", "floors": "floors", "spots": "spotElevations"}
CATEGORY_PARAMETER = "Category"
SURVEY_BASE = "Survey Point"
# Origins are written to the mm by R2, so a gap up to this, in metres, is the rounding of two values
ORIGIN_TOLERANCE = 0.0005
LINK_FIELDS = ("file", "plotNumber", "prototype", "case")
# The bands of the floor top route in mm. Values and points are written to the mm by R2, so 1 mm is the rounding of two values
BANDS_MM = (1, 5)
BAND_SLACK = 1e-7
# A spot this close in plan to an outline edge of its floor takes the level of that edge, in metres
EDGE_IN_PLAN = 0.005
# Corners within this of their plane make a plane top, in metres. R2 rounds each corner to the mm
PLANE_FIT = 0.001
OUTSIDE = "outsideItsFloor"
WORST = 20
REPEAT_NOTE = ("A repeat read: the Phase 1 read of the same key copy, an older reader in another window, held by element id. "
               "It shows the reader gives the same on the same bytes, it is no second method.")
PARTS = ("link",) + plan_from_past.FLOOR_KINDS
PART_NAMES = {"link": ("link", "links"), "generic": ("generic floor", "generic floors"), "yard": ("yard", "yards"), "ramp": ("ramp", "ramps")}


def category_counts(document):
    counts = Counter()
    for item in ((document.get("site") or {}).get("categories") or []):
        counts[item.get("name")] += item.get("count") or 0
    return counts


def by_categories(document):
    """R323. Each section against the elements Revit counts in its category. A floor whose Category parameter names another
    category, as the structural slabs Revit files under Structural Foundations, is counted apart beside that category."""
    found = category_counts(document)
    result = {}
    floors = document.get("floors") or []
    of_floor = Counter((floor.get("parameters") or {}).get(CATEGORY_PARAMETER) for floor in floors)
    for key, category in CATEGORY_OF.items():
        read = of_floor[category] if key == "floors" else len(document.get(SECTION_OF[key]) or [])
        result[key] = {"read": read, "category": found[category], "equal": read == found[category]}
    result["floorsInOtherCategories"] = {str(name): {"read": count, "category": found[name]} for name, count in sorted(of_floor.items(), key=lambda item: str(item[0]))
                                        if name != CATEGORY_OF["floors"]}
    return result


def by_bytes(document, references):
    """R323. The link files of the read against the Revit links the TransmissionData of the copy names, by file name in any case."""
    read = {str(link.get("file")).lower(): str(link.get("file")) for link in document.get("links") or []}
    stored = {str(item.get("file")).lower(): str(item.get("file")) for item in references}
    return {"read": len(read), "bytes": len(stored), "both": len(set(read) & set(stored)),
            "onlyRead": sorted(read[name] for name in set(read) - set(stored)), "onlyBytes": sorted(stored[name] for name in set(stored) - set(read))}


def survey_offset(document):
    """The survey point offset of R51, shared position minus position on each axis, None when the read holds no survey point."""
    survey = document.get("surveyPoint") or {}
    position, shared = survey.get("position"), survey.get("sharedPosition")
    if not isinstance(position, list) or not isinstance(shared, list) or len(position) != 3 or len(shared) != 3:
        return None
    return [shared[axis] - position[axis] for axis in range(3)]


def along_edge(start, end, x, y):
    """The distance in plan from a point to an edge and the level of the edge at the foot of the point on it."""
    dx, dy = end[0] - start[0], end[1] - start[1]
    length = dx * dx + dy * dy
    share = 0.0 if length == 0 else max(0.0, min(1.0, ((x - start[0]) * dx + (y - start[1]) * dy) / length))
    return math.hypot(x - start[0] - share * dx, y - start[1] - share * dy), start[2] + share * (end[2] - start[2])


def plane_through(points):
    """The plane z = a x + b y + c through the points by least squares, as the level at x and y, and the largest gap of a point from
    it. None when the points span no plane in plan, fewer than three or all on one line."""
    if len(points) < 3:
        return None
    count = float(len(points))
    mx, my, mz = (sum(point[axis] for point in points) / count for axis in range(3))
    sxx = sum((point[0] - mx) ** 2 for point in points)
    syy = sum((point[1] - my) ** 2 for point in points)
    sxy = sum((point[0] - mx) * (point[1] - my) for point in points)
    sxz = sum((point[0] - mx) * (point[2] - mz) for point in points)
    syz = sum((point[1] - my) * (point[2] - mz) for point in points)
    det = sxx * syy - sxy * sxy
    if det <= 1e-12 * max(1.0, sxx * syy):
        return None
    a = (sxz * syy - syz * sxy) / det
    b = (syz * sxx - sxz * sxy) / det

    def level(x, y):
        return mz + a * (x - mx) + b * (y - my)
    return level, max(abs(point[2] - level(point[0], point[1])) for point in points)


def top_at(floor, x, y, offset):
    """R323. The top of a floor at a point in shared plan coordinates and the route it came by. Within EDGE_IN_PLAN of an edge of the
    outline of R52, the level along the nearest edge, which is straight between its ends whatever diagonal a warped top splits on.
    Elsewhere the point must lie in the floor, its outline loops by the even-odd rule so a hole is outside, else the sketch outline
    of R34, and a point outside is no top of it. Inside, the plane through the corners of R34 when they lie on it within PLANE_FIT.
    A warped floor gives no top inside it."""
    best = None
    for loop in floor.get("outline") or []:
        points = [point for point in loop if isinstance(point, list) and len(point) == 3]
        for start, end in zip(points, points[1:] + points[:1]):
            distance, level = along_edge(start, end, x, y)
            if distance <= EDGE_IN_PLAN and (best is None or distance < best[0]):
                best = (distance, level)
    if best is not None:
        return best[1], "edge"
    shape = shape_of(floor, offset)
    if shape is None:
        return None, "noGeometry"
    if not shape.covers(Point(x, y)):
        return None, OUTSIDE
    corners = [[corner[axis] + offset[axis] for axis in range(3)] for corner in floor.get("corners") or [] if isinstance(corner, list) and len(corner) == 3]
    fit = plane_through(corners)
    if fit is None:
        return None, "noGeometry"
    level, worst = fit
    if worst > PLANE_FIT:
        return None, "warped"
    return level(x, y), "plane"


def bands(gaps):
    """The count, the gaps within each band of BANDS_MM, their shares and the worst gap in mm."""
    result = {"count": len(gaps)}
    for band in BANDS_MM:
        result["within%dMm" % band] = sum(1 for gap in gaps if abs(gap) <= band / 1000.0 + BAND_SLACK)
    for band in BANDS_MM:
        result["share%dMm" % band] = round(result["within%dMm" % band] / float(len(gaps)), 4) if gaps else None
    result["worstMm"] = round(max((abs(gap) for gap in gaps), default=0.0) * 1000, 1)
    return result


def spot_tops(document):
    """R323, R320. The value of each spot whose elevation base is the survey point and whose host is a floor of the read against the
    top of that floor at the spot. The origin of a spot is internal, so the survey offset of R51 puts it in shared plan coordinates,
    and the value of a survey point base is a shared elevation. The shares within 1 mm and 5 mm per host kind and in all, the spots
    whose gap is minus the thickness of their floor, which show its bottom, and the worst gaps. A spot of another base, with no value,
    on no floor of the read or where the floor gives no top is counted by its reason. A read with no survey point is not read."""
    offset = survey_offset(document)
    if offset is None:
        return {"notRead": "the read holds no survey point, so no spot is put in shared coordinates"}
    floors = {floor.get("elementId"): floor for floor in document.get("floors") or [] if floor.get("elementId") is not None}
    files = key_reads.link_files(document.get("links"))
    skipped, routes, gaps, rows = Counter(), Counter(), defaultdict(list), []
    for spot in document.get("spotElevations") or []:
        host = spot.get("host") or {}
        floor = floors.get(host.get("elementId")) if host.get("linkedElementId") is None else None
        origin = spot.get("origin")
        if spot.get("value") is None:
            skipped["noValue"] += 1
        elif spot.get("elevationBase") != SURVEY_BASE:
            skipped["base " + str(spot.get("elevationBase"))] += 1
        elif floor is None:
            skipped["notOnAFloorOfTheRead"] += 1
        elif not isinstance(origin, list) or len(origin) != 3:
            skipped["noOrigin"] += 1
        else:
            top, route = top_at(floor, origin[0] + offset[0], origin[1] + offset[1], offset)
            if top is None:
                skipped[route] += 1
                continue
            routes[route] += 1
            kind = key_reads.host_kind(spot, files)[0]
            gap = spot["value"] - top
            gaps[kind].append(gap)
            thickness = floor.get("thickness")
            rows.append({"elementId": spot.get("elementId"), "host": kind, "floorId": floor.get("elementId"), "gapMm": round(gap * 1000, 1), "route": route,
                         "thicknessMm": None if thickness is None else round(thickness * 1000, 1), "atBottom": thickness is not None and abs(gap + thickness) <= BANDS_MM[0] / 1000.0 + BAND_SLACK})
    rows.sort(key=lambda row: (-abs(row["gapMm"]), row["elementId"] or 0))
    every = [gap for kind in gaps for gap in gaps[kind]]
    return {"compared": len(every), "byRoute": dict(sorted(routes.items())), "skipped": dict(sorted(skipped.items())), "all": bands(every),
            "byHost": {kind: bands(gaps[kind]) for kind in sorted(gaps)}, "atFloorBottom": sum(1 for row in rows if row["atBottom"]),
            "worst": [{key: value for key, value in row.items() if key != "atBottom"} for row in rows[:WORST]]}


def floor_plot(floor, floors_config):
    """The plot a floor names by the Plot parameter of floors.plotParameter, as the reader splits it into plotNumber (R37)."""
    if floor.get("plotNumber") is not None:
        return str(floor["plotNumber"])
    text = (floor.get("parameters") or {}).get(floors_config["plotParameter"])
    return str(text).rpartition("-")[2] if text and "-" in str(text) else None


def shape_of(floor, offset):
    """The floor in shared plan coordinates, None when it has no outline and no sketch can be put there."""
    if not floor.get("outline") and offset is None:
        return None
    shape = plot_relations.floor_shape(floor, offset)[0]
    return None if shape is None or shape.is_empty else shape


def yard_plots(yards, generic, offset):
    """R323. A yard that names no plot goes to the plot of the one generic floor whose shape holds a point inside the yard."""
    shapes = [(plot, shape_of(floor, offset)) for plot, floor in generic]
    shapes = [(plot, shape) for plot, shape in shapes if shape is not None]
    tree = STRtree([shape for _, shape in shapes]) if shapes else None
    placed, lost = [], Counter()
    for yard in yards:
        shape = shape_of(yard, offset)
        if shape is None:
            lost["the yard has no shape"] += 1
            continue
        point = shape.representative_point()
        plots = sorted({shapes[index][0] for index in tree.query(point) if shapes[index][1].contains(point)}) if tree is not None else []
        if len(plots) == 1:
            placed.append(plots[0])
        else:
            lost["in no generic floor" if not plots else "in %d generic floors" % len(plots)] += 1
    return placed, lost


def why(part, count):
    singular, plural = PART_NAMES[part]
    return "no " + singular if count == 0 else "%d %s" % (count, plural)


def plot_rule(document, floors_config):
    """R323, R321. The per plot rule of the TU keys: each unit plot, a plot some link or floor of a kind names, holds one link, one
    generic floor, one yard and one ramp. A link is counted by RRE_ZZ_SP_Plot-Number and a floor by its Plot parameter, its kind by
    the types of the key (R321). A yard that names no plot, as every yard of the TU keys, goes to the plot of the generic floor whose
    shape holds it. The plots that break the rule with why, the elements that name no plot and the floors of no kind by type."""
    counts = defaultdict(Counter)
    not_named, no_kind, bad = Counter({part: 0 for part in ("link", "generic", "ramp")}), Counter(), []
    for link in document.get("links") or []:
        value = (link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER)
        plot = plan_from_past.plot_number(value)
        if value is None or not str(value).strip():
            not_named["link"] += 1
        elif plot is None:
            bad.append({"elementId": link.get("elementId"), "value": str(value)})
        else:
            counts[plot]["link"] += 1
    generic, yards, by_parameter = [], [], 0
    for floor in document.get("floors") or []:
        kind = plan_from_past.kind_of(floor, floors_config)
        named = floor_plot(floor, floors_config)
        plot = None if named is None else plan_from_past.plot_number(named)
        if kind is None:
            no_kind[str(floor.get("typeName"))] += 1
        elif named is not None and plot is None:
            bad.append({"elementId": floor.get("elementId"), "value": str(named)})
        elif plot is None:
            if kind == "yard":
                yards.append(floor)
            else:
                not_named[kind] += 1
        else:
            counts[plot][kind] += 1
            by_parameter += kind == "yard"
            if kind == "generic":
                generic.append((plot, floor))
    placed, lost = yard_plots(yards, generic, survey_offset(document))
    for plot in placed:
        counts[plot]["yard"] += 1
    breaks = []
    for plot in sorted(counts, key=link_summary.sort_key):
        found = {part: counts[plot][part] for part in PARTS}
        reasons = [why(part, found[part]) for part in PARTS if found[part] != 1]
        if reasons:
            breaks.append({"plot": plot, "counts": found, "why": reasons})
    by_reason = Counter(reason for item in breaks for reason in item["why"])
    return {"plots": len(counts), "hold": len(counts) - len(breaks), "break": len(breaks), "byReason": dict(sorted(by_reason.items())), "breaks": breaks,
            "yards": {"byPlotParameter": by_parameter, "byGenericShape": len(placed), "noPlot": dict(sorted(lost.items()))},
            "notNamed": dict(not_named), "badPlotValues": bad, "floorsOfNoKind": dict(sorted(no_kind.items()))}


def pick(ids, seed, size):
    """The same ids for the same seed and list, at most size of them."""
    ordered = sorted(ids)
    return sorted(random.Random(seed).sample(ordered, min(size, len(ordered))))


def link_gaps(new, old):
    """The fields of one link that differ between two reads, an origin within the rounding of R2 being equal."""
    gaps = [field for field in LINK_FIELDS if new.get(field) != old.get(field)]
    one, two = new.get("transform") or {}, old.get("transform") or {}
    if one.get("mirrored") != two.get("mirrored"):
        gaps.append("mirrored")
    if one.get("rotationDeg") is None or two.get("rotationDeg") is None or abs(one["rotationDeg"] - two["rotationDeg"]) > ORIGIN_TOLERANCE:
        gaps.append("rotationDeg")
    first, second = one.get("origin"), two.get("origin")
    if not first or not second or max(abs(a - b) for a, b in zip(first, second)) > ORIGIN_TOLERANCE:
        gaps.append("origin")
    return gaps


def links_by_id(new, old, seed, size=SAMPLE):
    """R323, the repeat read. The link ids of both reads, and size links picked at the seed among the ids both hold, field by field."""
    one = {link.get("elementId"): link for link in new.get("links") or []}
    two = {link.get("elementId"): link for link in old.get("links") or []}
    ids = pick(set(one) & set(two), seed, size)
    mismatches = []
    for element in ids:
        gaps = link_gaps(one[element], two[element])
        if gaps:
            mismatches.append({"elementId": element, "fields": gaps})
    return {"seed": seed, "ids": ids, "idsEqual": set(one) == set(two), "onlyNew": sorted(set(one) - set(two)), "onlyOld": sorted(set(two) - set(one)),
            "match": len(ids) - len(mismatches), "mismatch": len(mismatches), "mismatches": mismatches}


def id_sets(rows):
    sets = {}
    for row in rows or []:
        sets.setdefault(str(row.get("typeName")), set()).add(row.get("elementId"))
    return sets


def ids_per_type(new, old):
    """R323, the repeat read. Per floor type and per wall type, whether both reads hold the same element ids."""
    result = {}
    for section in ("floors", "walls"):
        one, two = id_sets(new.get(section)), id_sets(old.get(section))
        types = sorted(set(one) | set(two))
        differ = [name for name in types if one.get(name) != two.get(name)]
        result[section] = {"types": len(types), "equal": len(types) - len(differ), "differ": differ}
    return result


def summary_of(rule):
    return {key: rule[key] for key in ("plots", "hold", "break", "byReason", "yards", "floorsOfNoKind")}


def check_key(document, old_document, references, floors, plain=None, old_name=None):
    """R323. The four routes of one key read and, when a Phase 1 read is given, the repeat read. With plain, the floors block of
    floors.types, a key that floors.keyTypes reads other types for gets the per plot rule by floors.types alone beside its own."""
    result = {"categories": by_categories(document), "bytes": by_bytes(document, references), "spotTops": spot_tops(document), "plotRule": plot_rule(document, floors)}
    if plain is not None and plain["types"] != floors["types"]:
        result["plotRuleByFloorsTypesAlone"] = summary_of(plot_rule(document, plain))
    if old_document is not None:
        result["repeatRead"] = {"note": REPEAT_NOTE, "file": old_name, "idsPerType": ids_per_type(document, old_document),
                                "links": {str(seed): links_by_id(document, old_document, seed) for seed in SEEDS}}
    return result


def parse_triple(text):
    parts = text.split("=")
    if len(parts) != 3 or not all(parts):
        raise argparse.ArgumentTypeError("give CODE=JOB=PHASE1_READ, as IMC=RC=C:\\...\\testhost_KC_prototypes.json")
    return parts


def render(code, entry):
    categories, tops, rule = entry["categories"], entry["spotTops"], entry["plotRule"]
    repeat = entry.get("repeatRead") or {}
    shares = "not read" if "notRead" in tops else "%s of %s within 1 mm, %s within 5 mm" % (tops["all"]["within1Mm"], tops["compared"], tops["all"]["within5Mm"])
    return "%s: categories equal %s, bytes both %d of read %d and bytes %d, spot tops %s, plot rule holds on %d of %d %s, repeat read links %s" % (
        code, {key: categories[key]["equal"] for key in CATEGORY_OF}, entry["bytes"]["both"], entry["bytes"]["read"], entry["bytes"]["bytes"], shares,
        rule["hold"], rule["plots"], rule["byReason"], {seed: (item["match"], item["mismatch"]) for seed, item in (repeat.get("links") or {}).items()})


def main(argv=None):
    parser = argparse.ArgumentParser(description="R323. Check the key reads a second way: categories, the bytes of the copy, the floor top under each spot, "
                                                 "the per plot rule, and the repeat read by id.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=parse_triple, required=True, metavar="CODE=JOB=PHASE1_READ")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    plot_relations.refuse_inside_repo(Path(args.reads))
    settings = project_profile.load_settings(args.settings)
    block = settings.values["answerKeys"]
    folder = Path(settings.values["inputRoot"]) / block["folder"]
    copies = {key["code"]: key["copy"] for key in block["keys"]}
    path = Path(args.reads) / OUTPUT
    key_reads.earlier_keys(path)
    report = {"note": "R323. The key reads held against the categories of R311, the bytes of each key copy, the floor top under each spot and the per plot "
                      "rule, and the repeat read by id at seeds " + ", ".join(str(seed) for seed in SEEDS) + ", which is no second method. "
                      "A run writes the keys it is given and keeps the other keys of the file.", "keys": {}}
    plain = plan_from_past.load_floors(settings)
    for code, job, old in args.key:
        if code not in copies:
            raise project_profile.ProfileError("answerKeys has no key " + code)
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        old_document = key_reads.project_document(key_reads.load(old)) if old != "none" else None
        if old_document is not None:
            plan_from_past.check_key_file(settings, code, old_document.get("file"), plan_from_past.read_job(old))
        references = rvt_info.read(folder / copies[code])["revitLinks"]
        report["keys"][code] = dict(check_key(document, old_document, references, floors, plain, Path(old).name if old_document is not None else None), job=job)
    printed = report["keys"]
    report["keys"] = key_reads.merged(path, printed)
    write_json(path, report)
    for code, entry in printed.items():
        print(render(code, entry))
    return 0


if __name__ == "__main__":
    sys.exit(main())
