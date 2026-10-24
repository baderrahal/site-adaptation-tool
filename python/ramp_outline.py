"""The ramp outline of Rev 6 section 2.3.A tested against the ramps of the past job, no placement (R127).

The ramp is the full plot width by the front setback deep, from the plot front line, the front edge of the baseplan face
of R122, which sits behind the sidewalk and not on the curb. The setback is ramp.front_setback of the profile, a default and
a value per typology. Each past ramp is compared with that outline by its corners and its area. The road surface is also
asked at every past ramp corner and at the curb point nearest to it, to see whether the ramps no road answers lie on the
sidewalk. Lengths are in metres.
"""
import argparse
import math
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import Point
from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import link_summary
import link_table
import plan_from_past
import project_profile
import prototype_offsets
import ramp_levels
import surface_lookup
import yard_rule

BANDS = (("within10mm", 0.010), ("within100mm", 0.100))
EPSILON = 1e-9


def band_of(distance):
    for label, limit in BANDS:
        if distance <= limit + EPSILON:
            return label
    return "worse"


def setback(profile, unit):
    """R127. The front setback of a typology in metres, ramp.front_setback.default when the profile lists none for it."""
    table = profile.get("ramp.front_setback")
    key = unit if isinstance(unit, str) and unit in table else "default"
    return profile.metres("ramp.front_setback." + key)


def outline(frame, depth):
    """R127. The ramp outline from the front edge: front left, front right, back right, back left."""
    left, right, normal = frame["left"], frame["right"], frame["n"]
    return [left, right, (right[0] + depth * normal[0], right[1] + depth * normal[1]), (left[0] + depth * normal[0], left[1] + depth * normal[1])]


def shoelace(points):
    return abs(sum(points[i][0] * points[(i + 1) % len(points)][1] - points[(i + 1) % len(points)][0] * points[i][1] for i in range(len(points)))) / 2.0


def ring_order(points):
    """The points in order around their centre, so a shoelace area holds for corners listed in any order."""
    cx, cy = sum(point[0] for point in points) / len(points), sum(point[1] for point in points) / len(points)
    return sorted(points, key=lambda point: math.atan2(point[1] - cy, point[0] - cx))


def compare_outline(planned, corners):
    """R127. The largest distance from a planned corner to its own nearest past corner, each past corner used once, the
    front and back gaps apart, and the area difference over the front length, which is the gap in depth."""
    left = list(range(len(corners)))
    gaps = []
    for point in planned:
        best = min(left, key=lambda index: math.dist(point, corners[index]))
        gaps.append(math.dist(point, corners[best]))
        left.remove(best)
    width = math.dist(planned[0], planned[1])
    area = shoelace(ring_order(corners))
    return {"corner": max(gaps), "front": max(gaps[:2]), "back": max(gaps[2:]), "depthGap": abs(shoelace(planned) - area) / width if width else None,
            "pastDepth": area / width if width else None}


def test_ramps(ramps, faces, roads, profile, analysis, apart):
    """R127. One row per past ramp with its plot, unit and gaps, and the band counts, the plots of apart on their own."""
    road_tree = STRtree(roads) if roads else None
    limits = cad_links.front_limits(profile, analysis)
    rows, counts = [], {}
    for ramp in ramps:
        plot = plan_from_past.plot_number(ramp["plot"])
        row = {"plot": plot, "unit": ramp["unit"], "cutListed": plot in apart}
        frame = cad_links.front_frame(faces.get(plot), road_tree, roads, limits) if plot in faces else None
        if frame is None:
            row["why"] = "no face or no front edge"
        else:
            row["setback"] = setback(profile, ramp["unit"])
            row.update(compare_outline(outline(frame, row["setback"]), ramp["corners"]))
        rows.append(row)
        group = "apart" if row["cutListed"] else "outside"
        table = counts.setdefault(group, {"ramps": 0, "noFrame": 0, "corners": Counter(), "front": Counter(), "back": Counter(), "depth": Counter()})
        table["ramps"] += 1
        if "corner" not in row:
            table["noFrame"] += 1
            continue
        for key, value in (("corners", row["corner"]), ("front", row["front"]), ("back", row["back"]), ("depth", row["depthGap"])):
            table[key][band_of(value)] += 1
    return rows, {group: {key: dict(value) if isinstance(value, Counter) else value for key, value in table.items()} for group, table in counts.items()}


def curb_cover(ramps, roads, surface):
    """R127. How many past ramp corners the road surface covers where they are, at the plot line, and at the curb point, the
    nearest point of a road edge line, and how many ramps have no corner covered either way."""
    road_tree = STRtree(roads) if roads else None
    counts, per_plot = Counter(), {}
    for ramp in ramps:
        here = there = 0
        for corner in ramp["corners"]:
            counts["corners"] += 1
            if surface.z_at(corner[0], corner[1]) is not None:
                here += 1
            if road_tree is not None:
                line = roads[int(road_tree.query_nearest(Point(corner))[0])]
                curb = line.interpolate(line.project(Point(corner)))
                if surface.z_at(curb.x, curb.y) is not None:
                    there += 1
        counts["ramps"] += 1
        counts["coveredAtCorner"] += here
        counts["coveredAtCurb"] += there
        counts["rampsNoneAtCorner"] += here == 0
        counts["rampsNoneAtCurb"] += there == 0
        counts["rampsNoneAtCornerSomeAtCurb"] += here == 0 and there > 0
        per_plot[plan_from_past.plot_number(ramp["plot"])] = {"atCorner": here, "atCurb": there}
    return dict(counts), per_plot


def main(argv=None):
    parser = argparse.ArgumentParser(description="Test the ramp outline of Rev 6 against the past ramps, no placement (R127).")
    parser.add_argument("out_dir", help="where ramp_outline.json is written, keep it outside git")
    parser.add_argument("--roads", required=True, help="folder with pieces.json of the road layout drawing")
    parser.add_argument("--landxml-dir", required=True, help="folder of the LandXML surfaces of landxml_reader.py")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--surfaces", default=str(ramp_levels.DEFAULT_CONFIG))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        analysis = settings.get("analysis")
        job = plan_from_past.load_job(settings)
        config = ramp_levels.load_config(args.surfaces)
        if not config["road"]:
            raise project_profile.ProfileError("no surface has the role road in " + Path(args.surfaces).name)
        shift = link_table.load_shifts(args.shifts)["shifts"][job["neighbourhood"]]
        faces, _ = yard_rule.face_shapes(link_summary.load(project_profile.ROOT / job["baseplanPlots"])["plots"], shift)
        document = ramp_levels.host_document(link_summary.load(job["prototypes"]))
        ramps, left_out = ramp_levels.ramp_rows(document, set(config["rampTypes"]))
        roads = cad_analysis.read_road_edges(args.roads, analysis["roadEdgeLayers"])
        apart = set(cad_links_apart())
        rows, counts = test_ramps(ramps, faces, roads, profile, analysis, apart)
        cover, per_plot = curb_cover(ramps, roads, surface_lookup.load_surface(args.landxml_dir, config["road"]))
        for row in rows:
            row["cover"] = per_plot.get(row["plot"])
        report = {"ramps": len(ramps), "leftOut": dict(left_out), "road": config["road"], "counts": counts, "cover": cover, "rows": rows}
        common.write_json(Path(args.out_dir) / "ramp_outline.json", report)
        print("ramps %d, %s" % (len(ramps), counts))
        print("road %s cover %s" % (config["road"], cover))
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("ramp_outline: " + str(error), file=sys.stderr)
        return 2
    return 0


def cad_links_apart():
    """The plots of R120 the plotFrame section lists apart."""
    return prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or []


if __name__ == "__main__":
    sys.exit(main())
