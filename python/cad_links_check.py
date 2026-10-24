"""Second method for R120 to R123 on plots drawn with a seed (R124).

It reads the raw files again with json and math only, no geometry library and none of the helpers cad_links.py uses: the
Generic floor share by clipping the floor with its face (Sutherland and Hodgman) and the shoelace formula, the front edge
by a plain search over every road segment, the rotation and the side in the cluster by hand, and the origin from the
fitted corner offset of the report. Each drawn plot must give the same values as cad_links.json.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

SEED = 14
SAMPLE = 20
# The report rounds to the millimetre and to a thousandth, so a value within this of it is the same
REPORT_SLACK = 0.0006
ANGLE_SLACK = 0.05
# The sine of the turn under which three vertices count as one straight edge
STRAIGHT = 1e-3


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def shoelace(ring):
    return sum(ring[i][0] * ring[(i + 1) % len(ring)][1] - ring[(i + 1) % len(ring)][0] * ring[i][1] for i in range(len(ring))) / 2.0


def centroid(ring):
    area = shoelace(ring)
    cx = sum((ring[i][0] + ring[(i + 1) % len(ring)][0]) * (ring[i][0] * ring[(i + 1) % len(ring)][1] - ring[(i + 1) % len(ring)][0] * ring[i][1]) for i in range(len(ring)))
    cy = sum((ring[i][1] + ring[(i + 1) % len(ring)][1]) * (ring[i][0] * ring[(i + 1) % len(ring)][1] - ring[(i + 1) % len(ring)][0] * ring[i][1]) for i in range(len(ring)))
    return (cx / (6 * area), cy / (6 * area))


def counter_clockwise(ring):
    return ring if shoelace(ring) > 0 else ring[::-1]


def convex(ring):
    signs = set()
    for i in range(len(ring)):
        a, b, c = ring[i], ring[(i + 1) % len(ring)], ring[(i + 2) % len(ring)]
        cross = (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0])
        # A vertex on a straight edge, rounded to the mm, turns by a hair either way and is no bend
        if abs(cross) > STRAIGHT * math.dist(a, b) * math.dist(b, c):
            signs.add(cross > 0)
    return len(signs) <= 1


def clip(subject, window):
    """Sutherland and Hodgman: the part of subject inside the convex counter clockwise window."""
    output = list(subject)
    for i in range(len(window)):
        a, b = window[i], window[(i + 1) % len(window)]
        inside = lambda p: (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= 0
        source, output = output, []
        for j in range(len(source)):
            p, q = source[j], source[(j + 1) % len(source)]
            if inside(q):
                if not inside(p):
                    output.append(cross_point(p, q, a, b))
                output.append(q)
            elif inside(p):
                output.append(cross_point(p, q, a, b))
        if not output:
            return []
    return output


def cross_point(p, q, a, b):
    dx, dy = q[0] - p[0], q[1] - p[1]
    ex, ey = b[0] - a[0], b[1] - a[1]
    t = ((a[0] - p[0]) * ey - (a[1] - p[1]) * ex) / (dx * ey - dy * ex)
    return (p[0] + t * dx, p[1] + t * dy)


def segment_distance(point, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = dx * dx + dy * dy
    t = 0.0 if length == 0 else max(0.0, min(1.0, ((point[0] - a[0]) * dx + (point[1] - a[1]) * dy) / length))
    return math.hypot(point[0] - a[0] - t * dx, point[1] - a[1] - t * dy)


def road_segments(pieces, layers):
    wanted = [name.lower() for name in layers]
    found = []
    for piece in pieces:
        layer = str(piece.get("layer") or "").lower()
        if piece.get("kind") not in ("polyline", "line") or not any(layer == name or layer.endswith("$" + name) for name in wanted):
            continue
        points = [(point[0], point[1]) for point in piece.get("pts") or [] if len(point) >= 2]
        if piece.get("closed") and len(points) >= 3:
            points.append(points[0])
        found.extend(zip(points, points[1:]))
    return found


def front(ring, segments, low, high):
    """The front edge as (inward normal, right hand, left end, right end), by the nearest midpoint over every road segment."""
    best = None
    centre = centroid(ring)
    for i in range(len(ring)):
        a, b = ring[i], ring[(i + 1) % len(ring)]
        length = math.dist(a, b)
        if length < low or length >= high:
            continue
        middle = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
        distance = min(segment_distance(middle, p, q) for p, q in segments)
        if best is None or distance < best[0]:
            best = (distance, a, b, length)
    if best is None:
        return None
    _, a, b, length = best
    normal = (-(b[1] - a[1]) / length, (b[0] - a[0]) / length)
    if (centre[0] - a[0]) * normal[0] + (centre[1] - a[1]) * normal[1] < 0:
        normal = (-normal[0], -normal[1])
    right = (normal[1], -normal[0])
    left_end, right_end = (a, b) if a[0] * right[0] + a[1] * right[1] < b[0] * right[0] + b[1] * right[1] else (b, a)
    return normal, right, left_end, right_end


def check(rows, report, prototypes, plots, pieces, analysis, profile, settings, shift, seed, sample):
    """The drawn plots and for each the fields that differ from the report, an empty list when all agree."""
    document = next(item for item in prototypes["documents"] if item.get("kind") == "project")
    survey = document["surveyPoint"]
    offset = [survey["sharedPosition"][axis] - survey["position"][axis] for axis in range(2)]
    links = {}
    for link in document.get("links") or []:
        number = str((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number") or "").strip()
        if number.isdigit():
            links[str(int(number))] = link
    generic_types = set(settings["floors"]["types"]["generic"])
    generics = {}
    for floor in document.get("floors") or []:
        if floor.get("typeName") in generic_types and floor.get("plotNumber") is not None:
            loops = floor.get("outline")
            ring = [(vertex[0], vertex[1]) for vertex in loops[0]] if loops else [(vertex[0] + offset[0], vertex[1] + offset[1]) for vertex in floor["sketchBoundary"]]
            generics[str(floor["plotNumber"])] = ring
    faces = {}
    for entry in plots:
        label = str(entry.get("plot") or "")
        if entry.get("kind") == "plot_shape" and label.isdigit():
            faces.setdefault(str(int(label) + shift), []).append(counter_clockwise([(vertex[0], vertex[1]) for vertex in entry["vertices"]]))
    widths = [item["plot_width_m"] for item in profile["typologies"].values() if isinstance(item.get("plot_width_m"), (int, float))]
    low = min(widths) - settings["analysis"]["toleranceM"]
    high = settings["analysis"]["partyWallShare"] * profile["plot"]["long_side"] / 1000.0
    segments = road_segments(pieces, settings["analysis"]["roadEdgeLayers"])
    clusters = {}
    for cluster in analysis["clusters"]:
        members = [str(int(label) + shift) for label in cluster["plots"]]
        for member in members:
            clusters[member] = members
    fits = report["origin"]["fits"]
    by_plot = {row["plot"]: row for row in rows}
    drawn = sorted(by_plot, key=int)
    random.Random(seed).shuffle(drawn)
    results = []
    for plot in drawn[:sample]:
        row, link = by_plot[plot], links.get(plot)
        differs, not_checked = [], []
        own = faces.get(plot) or []
        if len(own) != 1 or link is None:
            results.append({"plot": plot, "skipped": "no single face or no link"})
            continue
        face = own[0]
        if plot in generics and row.get("cutShare") is not None:
            floor = counter_clockwise(generics[plot])
            area = shoelace(floor)
            best, unclipped = 0.0, False
            for others in faces.values():
                for window in others:
                    # The intersection is the same either way round, so a face that is not convex is clipped by a convex floor
                    if convex(window):
                        subject, cutter = floor, window
                    elif convex(floor):
                        subject, cutter = window, floor
                    else:
                        unclipped = True
                        continue
                    if max(p[0] for p in window) < min(p[0] for p in floor) or min(p[0] for p in window) > max(p[0] for p in floor):
                        continue
                    if max(p[1] for p in window) < min(p[1] for p in floor) or min(p[1] for p in window) > max(p[1] for p in floor):
                        continue
                    part = clip(subject, cutter)
                    if len(part) >= 3:
                        best = max(best, shoelace(part) / area)
            if unclipped and best < row["cutShare"] - REPORT_SLACK:
                not_checked.append("cutShare, a face and the floor are both not convex")
            elif abs(best - row["cutShare"]) > REPORT_SLACK:
                differs.append("cutShare " + str(round(best, 4)) + " against " + str(row["cutShare"]))
        origin = (link["transform"]["origin"][0] + offset[0], link["transform"]["origin"][1] + offset[1])
        corner = min(math.dist(origin, vertex) for vertex in face)
        if abs(corner - row.get("faceCorner", -1)) > REPORT_SLACK:
            differs.append("faceCorner " + str(round(corner, 4)) + " against " + str(row.get("faceCorner")))
        found = front(face, segments, low, high)
        mirrored = bool(link["transform"]["mirrored"])
        if found is None:
            differs.append("no front edge")
        else:
            normal, right, left_end, right_end = found
            axis_x = (-right[0], -right[1]) if mirrored else right
            rotation = math.degrees(math.atan2(axis_x[1], axis_x[0])) % 360.0
            gap = abs(link["transform"]["rotationDeg"] % 360.0 - rotation) % 360.0
            gap = min(gap, 360.0 - gap)
            if abs(gap - row.get("rotationGap", -1)) > ANGLE_SLACK:
                differs.append("rotationGap " + str(round(gap, 3)) + " against " + str(row.get("rotationGap")))
            fit = fits.get(str(row["unit"]))
            if fit is not None and row.get("originGap") is not None:
                start, end = (right_end, left_end) if mirrored else (left_end, right_end)
                base = start if fit["anchor"] == "start" else end
                guess = (base[0] + fit["offset"][0] * axis_x[0] + fit["offset"][1] * normal[0], base[1] + fit["offset"][0] * axis_x[1] + fit["offset"][1] * normal[1])
                if abs(math.dist(guess, origin) - row["originGap"]) > 0.001:
                    differs.append("originGap " + str(round(math.dist(guess, origin), 4)) + " against " + str(row["originGap"]))
            members = clusters.get(plot)
            if members and len(members) >= 2 and all(len(faces.get(member) or []) == 1 for member in members):
                ordered = sorted(members, key=lambda member: centroid(faces[member][0])[0] * right[0] + centroid(faces[member][0])[1] * right[1])
                index, count = ordered.index(plot), len(ordered)
                side = "left" if index < (count - 1) / 2 else "right" if index > (count - 1) / 2 else "middle"
                if side != row.get("side"):
                    differs.append("side " + side + " against " + str(row.get("side")))
        case = str((link.get("parameters") or {}).get("CASE") or "").strip() or None
        if case != row.get("case") or mirrored != row.get("mirrored"):
            differs.append("case or mirror flag differs")
        results.append({"plot": plot, "differs": differs, "notChecked": not_checked})
    return results


def main(argv=None):
    parser = argparse.ArgumentParser(description="Check cad_links.json a second way on plots drawn with a seed (R124).")
    parser.add_argument("out_dir")
    parser.add_argument("--report", required=True, help="cad_links.json")
    parser.add_argument("--prototypes", required=True)
    parser.add_argument("--baseplan", required=True, help="plots.json of the baseplan")
    parser.add_argument("--roads", required=True, help="pieces.json of the road layout drawing")
    parser.add_argument("--analysis", required=True)
    parser.add_argument("--profile", required=True)
    parser.add_argument("--settings", required=True)
    parser.add_argument("--shift", type=int, required=True, help="the plot number shift of the neighbourhood, R28")
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--sample", type=int, default=SAMPLE)
    args = parser.parse_args(argv)
    report = read(args.report)
    results = check(report["rows"], report, read(args.prototypes), read(args.baseplan)["plots"], read(args.roads)["pieces"], read(args.analysis), read(args.profile), read(args.settings),
                    args.shift, args.seed, args.sample)
    checked = [item for item in results if "differs" in item]
    agree = [item for item in checked if not item["differs"]]
    out = {"seed": args.seed, "drawn": len(results), "checked": len(checked), "agree": len(agree), "skipped": [item for item in results if "skipped" in item],
           "notChecked": [item for item in checked if item["notChecked"]],
           "disagree": [item for item in checked if item["differs"]]}
    path = Path(args.out_dir) / "cad_links_check.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=1), encoding="utf-8")
    print("seed %d, drawn %d, checked %d, agree %d, a field not checked %d" % (args.seed, len(results), len(checked), len(agree), len(out["notChecked"])))
    return 0 if len(agree) == len(checked) else 1


if __name__ == "__main__":
    sys.exit(main())
