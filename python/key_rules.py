"""R331 to R335. The rules of Round ADDIN 3a measured on the Parsons answer keys, the Native keys beside them, called by majority.py.

R331: the coordinates of each key read, survey point, project base point, project position, angle to true north, shared site,
levels and units, held against one reference key, so button 1 knows whether one set serves every zone. Only gaps and names
leave this module, never a coordinate or a level.
R332: the link rotation against the front frame of the plot (R122, the corner rule of R226) and the link origin against the
plotFrame fit of R121, as fitted on the IMC key and fitted again per code on every plot of a group.
R333: the hand of the units in no cluster by the pair of their row, the villas between cluster units apart.
R334: the ramp outline of R127 against the corners of the key ramp, the yard outline of R135 against the key yard, and what the
flat ramps share.
R335: the boundary walls of the key plans, their base, top and line against the plot, the front each plot leaves open, and the
shared parameters of the links against formulas a new build can give them.
"""
import json
import math
import re
from collections import Counter

from shapely.geometry import Point

import cad_links
import floor_fit
import ramp_outline

POINTS = ("surveyPoint.position", "surveyPoint.sharedPosition", "basePoint.position", "basePoint.sharedPosition")
# A point or a level is the same within half a mm, what the rounding of R2 to 3 decimals allows, an angle within a millionth of a degree (R311)
SAME_MM = 0.5
SAME_DEG = 1e-6
UNIT_FIELDS = ("system", "length", "lengthSystem")
EPSILON = 1e-9


def part(document, path):
    first, second = path.split(".")
    return (document.get(first) or {}).get(second)


def site_parts(document):
    """R331. The project position as a point in metres with its angle, the level elevations by name and the unit names of a read
    of R311, or None for a read of before R311."""
    site = document.get("site")
    if not isinstance(site, dict):
        return None
    position = site.get("projectPosition") or {}
    return {"position": [position.get("eastWest"), position.get("northSouth"), position.get("elevation")], "angle": position.get("angleDeg"),
            "levels": {level["name"]: level.get("elevation") for level in site.get("levels") or []},
            "names": {name: site.get(name) for name in ("activeSite", "sites", "placeName")},
            "units": {name: (site.get("units") or {}).get(name) for name in UNIT_FIELDS}, "accuracy": (site.get("units") or {}).get("lengthAccuracy")}


def site_facts(documents, reference):
    """R331. Each read held against the read of the reference key: the gap of every point in mm, of the angle in degrees, of the
    project position and of each level by name in mm, and the names and units that differ. oneSet when no key differs. shared
    holds what button 1 needs and no coordinate: the angle, the shared sites, the level names and their steps over the lowest."""
    base = documents[reference]
    base_site = site_parts(base)
    keys = {}
    for key, document in documents.items():
        compared, differ = [], {}
        for path in POINTS:
            mine, theirs = part(document, path), part(base, path)
            if mine is None or theirs is None:
                continue
            compared.append(path)
            gap = math.dist(mine, theirs) * 1000.0
            if gap > SAME_MM:
                differ[path] = round(gap, 1)
        if document.get("angleToTrueNorthDeg") is not None and base.get("angleToTrueNorthDeg") is not None:
            compared.append("angleToTrueNorthDeg")
            gap = cad_links.angle_gap(document["angleToTrueNorthDeg"], base["angleToTrueNorthDeg"])
            if gap > SAME_DEG:
                differ["angleToTrueNorthDeg"] = round(gap, 6)
        site = site_parts(document)
        if site is not None and base_site is not None:
            compared.append("projectPosition")
            gap = math.dist(site["position"], base_site["position"]) * 1000.0
            if gap > SAME_MM or cad_links.angle_gap(site["angle"], base_site["angle"]) > SAME_DEG:
                differ["projectPosition"] = round(gap, 1)
            for name in sorted(set(site["levels"]) | set(base_site["levels"])):
                compared.append("levels." + name)
                mine, theirs = site["levels"].get(name), base_site["levels"].get(name)
                if mine is None or theirs is None:
                    differ["levels." + name] = None
                elif abs(mine - theirs) * 1000.0 > SAME_MM:
                    differ["levels." + name] = round(abs(mine - theirs) * 1000.0, 1)
            for name in ("names", "units"):
                for field, value in site[name].items():
                    compared.append(field)
                    if value != base_site[name][field]:
                        differ[field] = value
        keys[key] = {"compared": compared, "differ": differ, "lengthAccuracy": None if site is None else site["accuracy"]}
    shared = None
    if base_site is not None:
        ordered = sorted(base_site["levels"].items(), key=lambda pair: (pair[1], pair[0]))
        lowest = ordered[0][1] if ordered else None
        shared = {"angleToTrueNorthDeg": base.get("angleToTrueNorthDeg"), "activeSite": base_site["names"]["activeSite"], "sites": base_site["names"]["sites"],
                  "levels": [name for name, _ in ordered], "levelStepsM": {name: round(value - lowest, 3) for name, value in ordered[1:]},
                  "units": dict(base_site["units"])}
    return {"reference": reference, "oneSet": all(not entry["differ"] for entry in keys.values()), "keys": keys, "shared": shared}


def group_site_facts(documents, groups, references):
    """R331. site_facts per group of majority.groups against the reference key of that group, then the reference keys of the
    groups against the first, so the Parsons keys and the Native keys are each held as one set and both sets apart."""
    found = {}
    for group, reference in references.items():
        mine = {key: document for key, document in documents.items() if key in groups.get(group, ())}
        if reference in mine:
            found[group] = site_facts(mine, reference)
        elif mine:
            # R331. The keys of a group read without its reference are listed, never left out in silence
            found[group] = {"reference": reference, "missingReference": True, "keys": sorted(mine)}
    leads = {references[group]: documents[references[group]] for group in found if not found[group].get("missingReference")}
    if len(leads) > 1:
        found["acrossGroups"] = site_facts(leads, next(iter(leads)))
    return found


def angle_hold(limit):
    """R332. A rotation holds when it is within limit degrees of the rule, the turn closed."""
    return lambda guess, aim: cad_links.angle_gap(guess, aim) <= limit + EPSILON


def point_hold(limit):
    """R332. An origin holds when it is within limit metres of the rule in plan."""
    return lambda guess, aim: math.dist(guess, aim) <= limit + EPSILON


def rotation_by(frame_key):
    """R332. The rotation the front frame of a plot gives its link with the mirror flag of the key (R122)."""
    return lambda item: None if item.get(frame_key) is None else cad_links.rotation_of(item[frame_key], item["row"]["mirrored"])


def rotation_candidates():
    """R332. frontRule takes the frame of the coded front, the corner rule of R226 on a plot with two road edges and the edge
    nearest a curb on the others, nearestCurb takes the edge nearest a curb on every plot, so a corner plot tells them apart."""
    return {"frontRule": rotation_by("frame"), "nearestCurb": rotation_by("curbFrame")}


def rotation_gap(item, value, rule):
    """R332. A rotation override is its gap to the rule in degrees, from minus 180 up to 180, never the rotation itself."""
    if rule is None:
        return None
    return {"gapToRuleDeg": round((value - rule + 180.0) % 360.0 - 180.0, 3)}


def fit_unit(code, fits, typologies):
    """R332. The fit of a code, else the fit of the code of the prototype file it links (keo_file), as plan_from_book.py takes it."""
    return fits.get(code) or fits.get((typologies.get(code) or {}).get("keo_file"))


def refit(items):
    """R332. The plotFrame fit of R121 again on every plot of the items with a frame and an origin, per code: the anchor corner
    whose offsets lie nearer their median, the most plots within 10 mm and then within 1 mm, and the median offset in link axes,
    at 3 decimals as R2 writes it."""
    links = {item["plot"]: {"origin": (item["row"]["x"], item["row"]["y"]), "mirrored": item["row"]["mirrored"], "unit": item["code"]}
             for item in items if item.get("frame") is not None and item["row"].get("x") is not None and item["code"]}
    frames = {plot: item["frame"] for plot, item in ((item["plot"], item) for item in items) if plot in links}
    fits = cad_links.fit_origins(links, frames, set())
    counts = {}
    for link in links.values():
        counts[link["unit"]] = counts.get(link["unit"], 0) + 1
    return {unit: {"anchor": fit["anchor"], "offset": [round(fit["offset"][0], 3), round(fit["offset"][1], 3)], "plots": counts[unit]} for unit, fit in fits.items()}


def origin_target(item):
    return None if item["row"].get("x") is None else (item["row"]["x"], item["row"]["y"])


def origin_by(fits, typologies):
    """R332. The link origin the front frame and a fit give a plot with the mirror flag of the key (R121)."""
    def guess(item):
        fit = fit_unit(item["code"], fits, typologies)
        if fit is None or item.get("frame") is None:
            return None
        return cad_links.predicted_origin(item["frame"], item["row"]["mirrored"], fit)
    return guess


def origin_candidates(as_fitted, fits, typologies):
    """R332. plotFrame is the fit of config/prototype_offsets.json, measured on the IMC key, plotFrameRefit the fit per code on
    every plot of the group."""
    return {"plotFrame": origin_by(as_fitted, typologies), "plotFrameRefit": origin_by(fits, typologies)}


def insert_gap(item, value, rule):
    """R332. An origin override is its gap to the rule in the link axes of the plot in metres, X along the front and Y into
    the plot, never a coordinate."""
    if rule is None or item.get("frame") is None:
        return None
    axis_x, axis_y = cad_links.link_axes(item["frame"], item["row"]["mirrored"])
    along, inward = cad_links.in_axes(value, rule, axis_x, axis_y)
    return {"gapToRuleM": [round(along, 3), round(inward, 3)]}




def row_pairs(units, neighbours, kind_of, in_cluster, kinds):
    """R333. The pairs of the rows of duplex and townhouse units in no cluster: per such unit the units of those kinds in no cluster
    beside it, walked left and right seen from the road while each step has one, and the side it takes in its pair, the rows
    paired from their right end, so the left unit of an odd row is the one alone. A unit with no such neighbour has no side."""
    def counted(plot):
        return kind_of(plot) in kinds and not in_cluster(plot)

    def walk(plot, side):
        seen, at, steps = {plot}, plot, 0
        while True:
            beside = [other for other in neighbours.get(at, {}).get(side, []) if other not in seen and counted(other)]
            if len(beside) != 1:
                return steps
            at = beside[0]
            seen.add(at)
            steps += 1
    found = {}
    for plot in units:
        if not counted(plot) or plot not in neighbours:
            continue
        left, right = walk(plot, "left"), walk(plot, "right")
        length = left + right + 1
        found[plot] = {"left": left, "right": right, "length": length, "side": None if length < 2 else ("right" if right % 2 == 0 else "left")}
    return found


def between_clusters(sides, in_cluster):
    """R333. How many sides of a plot hold a unit of a cluster: both, one or none, for the villas between cluster units."""
    held = sum(1 for side in ("left", "right") if any(in_cluster(other) for other in sides.get(side, [])))
    return "both" if held == 2 else "one" if held == 1 else "none"


def ramp_target(item):
    """R334. The four corners of the ramp of the key in plan, or None."""
    corners = item["row"].get("rampCorners")
    if isinstance(corners, str):
        corners = json.loads(corners) if corners else None
    return None if not corners or len(corners) != 4 else [(corner[0], corner[1]) for corner in corners]


def ramp_rule(profile):
    """R334. The ramp outline of R127 on the front frame of the plot with the front setback of its code."""
    def guess(item):
        if item.get("frame") is None:
            return None
        return ramp_outline.outline(item["frame"], ramp_outline.setback(profile, item.get("rev9") or item["code"]))
    return guess


def corner_hold(limit):
    """R334. An outline holds when each of its corners lies within limit metres of its own corner of the key, each used once."""
    return lambda guess, aim: ramp_outline.compare_outline(guess, aim)["corner"] <= limit + EPSILON


def corner_gap(item, value, rule):
    """R334. A ramp outline override is its largest corner gap to the rule in metres, never a corner."""
    return None if rule is None or value is None else {"gapToRuleM": round(ramp_outline.compare_outline(rule, value)["corner"], 3)}


def outline_rows(plans, frames, step):
    """R334. The yard outlines of the key plans in link axes by plot, with unit, mirror flag and plot width, as R135 reads them."""
    rows = {}
    for key, plan in plans.items():
        for row in floor_fit.outline_rows(plan, "yard", {}, {plot: frames.get(plot) for plot in (entry["plot"] for entry in plan["plots"])}, set(), step):
            rows[(key, row["plot"])] = row
    return rows


def outline_groups(groups):
    return {(group["unit"], group["mirrored"], group["width"]): floor_fit.shape_of(group["loops"]) for group in groups}


def outline_rule(groups, rows):
    """R334. The fitted outline of the unit, mirror flag and plot width of the plot of the key, in link axes, or None."""
    def guess(item):
        row = rows.get((item["key"], item["plot"]))
        return None if row is None else groups.get((row["unit"], row["mirrored"], row["width"]))
    return guess


def outline_target(rows):
    def target(item):
        row = rows.get((item["key"], item["plot"]))
        return None if row is None else row["shape"]
    return target


def shape_hold(limit):
    """R334. A floor outline holds when the boundaries of the two shapes lie within limit metres of each other (Hausdorff)."""
    return lambda guess, aim: floor_fit.outline_gap(guess, aim) <= limit + EPSILON


def shape_gap(item, value, rule):
    return None if rule is None or value is None else {"gapToRuleM": round(floor_fit.outline_gap(rule, value), 3)}


def entry_refused(plans):
    """R334. The plots of each key plan whose El @ Entry parameter R325 refused, a placeholder such as 0 or 150.0, by key and plot."""
    return {(key, entry["plot"]) for key, plan in plans.items() for entry in plan.get("missing") or []
            if any(item.get("key") == "levels.entry" and str(item.get("why", "")).startswith("refused") for item in entry.get("missing") or [])}


def flat_ramps(items, flat_class, class_of):
    """R334. What the ramps of the flat class share: their counts by code, key, measured slope band, steps, corner and whether the
    key refused their El @ Entry parameter, and the plots of the class against the plots refused."""
    flat = [item for item in items if class_of(item) == flat_class]
    by = lambda name: dict(sorted(Counter(str(name(item)) for item in flat).items()))
    refused = sum(1 for item in items if item["factors"].get("entryRefused") == "yes")
    return {"plots": len(flat), "refusedPlots": refused, "byEntryRefused": by(lambda item: item["factors"].get("entryRefused")),
            "byCode": by(lambda item: item["code"]), "byKey": by(lambda item: item["key"]),
            "byMeasuredBand": by(lambda item: item["factors"].get("measuredBand")), "bySteps": by(lambda item: item["factors"].get("steps")),
            "byCorner": by(lambda item: item["factors"].get("corner")), "plotsByKey": {key: sorted((item["plot"] for item in flat if item["key"] == key), key=int)
                                                                                     for key in sorted({item["key"] for item in flat})}}


def wall_kind(name, types):
    """R335. The kind of a wall type by the patterns of majority.wallTypes, the first kind whose pattern the name holds, or None."""
    return next((kind for kind, pattern in types.items() if re.search(pattern, name or "")), None)


def plan_levels(plan):
    """R335. The elevation of each level a floor of the plan names, so a wall base is its offset over its level."""
    return {floor["level"]["value"]: floor["levelElevation"]["value"] for entry in plan["plots"] for floor in (entry.get("floors") or {}).values()
            if "level" in floor and "levelElevation" in floor}


def wall_items(plans, keys, group, types):
    """R335. One item per boundary wall of the key plans of a group on at least one plot of its plan, with the FFL of those plots,
    filed under its first plot, and the walls counted per key by kind and by the number of plots they stand on."""
    items, counts = [], {}
    for key in keys:
        plan = plans.get(key)
        if plan is None:
            # R335. A key with no plan holds no counted wall, None and never an empty count
            counts[key] = None
            continue
        plots = {entry["plot"]: entry for entry in plan["plots"]}
        levels = plan_levels(plan)
        tally = Counter()
        for wall in plan.get("walls") or []:
            kind = wall_kind(wall.get("type"), types)
            count = len(wall.get("plots") or [])
            tally["%s on %d plot%s" % (kind, count, "" if count == 1 else "s")] += 1
            mine = [plots[plot] for plot in wall.get("plots") or [] if plot in plots and (plots[plot].get("levels") or {}).get("ffl")]
            if kind != "boundary" or not mine:
                continue
            first = mine[0]
            items.append({"plot": first["plot"], "key": key, "group": group, "code": first["unit"]["value"], "modelKind": None, "row": {},
                          "factors": {"all": "all", "locationLine": str(wall.get("locationLine")), "plots": str(count)},
                          "wall": wall, "ffls": [entry["levels"]["ffl"]["value"] for entry in mine], "baseLevel": levels.get(wall.get("baseConstraint"))})
        counts[key] = dict(tally)
    return items, counts


def no_base_level(items):
    """R335. The walls whose base constraint no floor of their plan names, so their base level is unknown and never 0."""
    return [{"key": item["key"], "elementId": item["wall"].get("elementId"), "baseConstraint": item["wall"].get("baseConstraint")}
            for item in items if item.get("baseLevel") is None]


def wall_base(item):
    """R335. The level of the base of a wall, its base offset over its base level, None when no floor names that level."""
    return None if item.get("baseLevel") is None else item["wall"]["baseOffset"] + item["baseLevel"]


def wall_top(item):
    base = wall_base(item)
    return None if base is None else base + item["wall"]["heightM"]


def wall_base_candidates(yard_depth):
    """R335. The base at the yard level, the FFL less the yard depth, of the lower or the higher plot of the wall."""
    return {"yardLevelOfTheLowerPlot": lambda item: min(item["ffls"]) - yard_depth, "yardLevelOfTheHigherPlot": lambda item: max(item["ffls"]) - yard_depth}


def wall_top_candidates(profile):
    """R335. The top at the height Rev 6 gives over the FFL (rev6.boundary_wall_top_above_ffl) of the higher or the lower plot."""
    rise = profile.metres("rev6.boundary_wall_top_above_ffl")
    return {"higherFflPlusRev6": lambda item: max(item["ffls"]) + rise, "lowerFflPlusRev6": lambda item: min(item["ffls"]) + rise}


def wall_line_target(faces):
    """R335. The distance from the middle of the location line of a wall to the nearest edge of the faces of its plots, in metres."""
    def target(item):
        wall = item["wall"]
        middle = Point((wall["start"][0] + wall["end"][0]) / 2.0, (wall["start"][1] + wall["end"][1]) / 2.0)
        found = [faces[plot].exterior.distance(middle) for plot in wall.get("plots") or [] if plot in faces]
        return min(found) if found else None
    return target


def wall_line_rule(centre_codes, thickness):
    """R335. The wall centre on the plot edge: a location line at the wall or core centre lies on the edge, one at a face half the
    boundary wall thickness of the profile off it."""
    return lambda item: 0.0 if item["wall"].get("locationLine") in centre_codes else thickness / 2.0


def number(text):
    """R335. A parameter that reads as a number, such as the text 175.400 a link holds, else None."""
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def level_hold(limit):
    return lambda guess, aim: abs(guess - aim) <= limit + EPSILON


def level_gap(item, value, rule):
    return None if rule is None or value is None else {"gapToRuleM": round(value - rule, 4)}


def front_cover(frame, walls, near):
    """R335. The share of the front edge of a plot that its boundary walls run along: a wall within 5 degrees of the edge with both
    ends within near of its line covers the part of the edge it spans, overlaps counted once. None with no front."""
    if frame is None:
        return None
    left, right = frame["left"], frame["right"]
    length = math.dist(left, right)
    along = ((right[0] - left[0]) / length, (right[1] - left[1]) / length)
    spans = []
    for wall in walls:
        first, second = wall["start"][:2], wall["end"][:2]
        size = math.dist(first, second)
        if size == 0 or abs((second[0] - first[0]) * along[1] - (second[1] - first[1]) * along[0]) / size > math.sin(math.radians(5)):
            continue
        offsets = [abs((point[0] - left[0]) * along[1] - (point[1] - left[1]) * along[0]) for point in (first, second)]
        if max(offsets) > near:
            continue
        ends = sorted((point[0] - left[0]) * along[0] + (point[1] - left[1]) * along[1] for point in (first, second))
        low, high = max(0.0, ends[0]), min(length, ends[1])
        if high > low:
            spans.append((low, high))
    covered, reach = 0.0, None
    for low, high in sorted(spans):
        if reach is None or low > reach:
            covered += high - low
            reach = high
        elif high > reach:
            covered += high - reach
            reach = high
    return covered / length


def read_parameters(reads, plot_parameter):
    """R335. The link parameters of each key read by key and plot, the plot of plot_parameter, majority.linkPlotParameter, a plot two
    links name left out."""
    found, twice = {}, set()
    for key, document in reads.items():
        for link in document.get("links") or []:
            parameters = link.get("parameters") or {}
            plot = str(parameters.get(plot_parameter) or "").strip()
            if not plot:
                continue
            if (key, plot) in found:
                twice.add((key, plot))
            found[(key, plot)] = parameters
    return {pair: parameters for pair, parameters in found.items() if pair not in twice}


def parameter_formulas(facade):
    """R335. Formulas a parameter may follow: the plot number, the Rev 9 code, the two joined, and the link Name made of the plot,
    the code, the steps and the facade parameters named, as plot | code | steps | colour | option."""
    def facade_part(item, name):
        return str(json.loads(item["row"].get("parameters") or "{}").get(name) or "")
    return {"plot": lambda item: item["plot"], "rev9Code": lambda item: item.get("rev9"), "plotAndCode": lambda item: item["plot"] + " | " + str(item.get("rev9")),
            "plotCodeStepsAndFacade": lambda item: " | ".join([item["plot"], str(item.get("rev9")), str(item["factors"].get("steps"))]
                                                               + [facade_part(item, name) for name in facade])}
