"""R331 to R335. The rules of Round ADDIN 3a measured on the Parsons answer keys, the Native keys beside them, called by majority.py.

R331: the coordinates of each key read, survey point, project base point, project position, angle to true north, shared site,
levels and units, held against one reference key, so button 1 knows whether one set serves every zone. Only gaps and names
leave this module, never a coordinate or a level.
R332: the link rotation against the front frame of the plot (R122, the corner rule of R226) and the link origin against the
plotFrame fit of R121, as fitted on the IMC key and fitted again per code on every plot of a group.
"""
import math
import statistics

import cad_links

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
    leads = {references[group]: documents[references[group]] for group in found}
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


