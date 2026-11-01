"""R362. The boundary walls of a build by the rules R335 measured on the answer keys, Walls of Common in C# gives the same (R345).

A wall runs along each edge of the face of a plot the plan writes but its front edge, which the keys leave open (keyRules.frontOpen all), its
centre on the edge (keyRules.wallLine centreOnTheEdge). An edge another face lies along within keyPlans.wallEdgeM is parted where the edges of
those faces end, so each part is one wall on the plots it divides, placed once from the first of its written plots in ordinal order
of the plot numbers. The base is the yard level, the FFL less yardBelowFflM of the level profile, of the higher or the lower plot of keyRules.wallBase,
and the top the FFL of the plot of keyRules.wallTop plus rev6.boundary_wall_top_above_ffl, weak until Q071 is answered. The garden walls have
no measured rule, so a build places none (conflict 8 of docs/design/10-rule-ids.md). Where a wall starts and ends along its edge no key rule
measures, so the ends of a wall carry UNKNOWN through valueStatus. Since Q115 keyRules.wallOpen clusterPairSpan leaves open the part of an edge
two written units of one footprint cluster share that the keys leave open for their pair of units, the party wall of the two villas, measured
from the end of the edge at the front of R355 of the plot, a weak rule on the keys.

R415, R416 since round 4j-2, Manual Rev 06 2.8.A. A plot is irregular when its face, its edges that run on within the tolerance of the rules
profile merged, is not 4 corners each within that tolerance of 90 degrees. Under the Rev 6 mode of designBasis each wall of an irregular
written plot runs with its outer face on the plot line, so its ends move half of plot.boundary_wall_thickness inside that plot. Under the past
job no plot is told irregular and every wall keeps its centre on the edge as the keys hold it on irregular plots as on regular ones.
"""
import math
from collections import Counter

from shapely.geometry import box
from shapely.strtree import STRtree

import common
import plan_rules
import project_profile
import value_status

FIELDS = ("plots", "ends", "base", "top")
STEP_FIELDS = ("unit", "written", "edges", "plots", "ends", "base", "top", "levelPlots", "status", "irregular", "lineInsideM")
SOURCE = "keyRules"
PLOTS_SOURCE = "locationPlan"
# R416. The source of the ends of a wall moved inside an irregular plot, its line by Manual Rev 06 2.8.A and no longer keyRules.wallLine
INSIDE_SOURCE = "manualInside"
# The override of the list that turns a link, plan_from_book.moved_link
ROTATION = "rotationDeg"
# The candidates of key_rules.wall_base_candidates and wall_top_candidates, True for the higher plot of the wall
BASE_RULES = {"yardLevelOfTheHigherPlot": True, "yardLevelOfTheLowerPlot": False}
TOP_RULES = {"higherFflPlusRev6": True, "lowerFflPlusRev6": False}
LINE_RULES = ("centreOnTheEdge",)
OPEN_RULES = ("all",)
# Q115. The rule of keyRules.wallOpen, which a profile may leave out to wall the whole edge
SPAN_RULES = ("clusterPairSpan",)
SPAN_LINE = "keyRules.wallOpen of %s holds no span table a build leaves an edge open by, it takes %s with spans of two numbers, the first under the second (R362, Q115)"
RULE_LINE = "keyRules.%s of %s holds no rule a build places boundary walls by, it takes %s (R362)"
NUMBER_LINE = "%s is no number of 0 or more, a build places no boundary wall without it (R362)"
# R415. A corner of 45 degrees or more off 90 is no rectangle corner at any tolerance, so the tolerance lies above 0 and under 45
TOLERANCE_LIMIT = 45.0
TOLERANCE_LINE = "rev6.irregular_tolerance_deg is no number over 0 and under 45, a build under the Rev 6 mode tells no irregular plot without it (R415)"
THICKNESS = "plot.boundary_wall_thickness"
RIGHT_ANGLE = 90.0
STRAIGHT_ANGLE = 180.0
RECTANGLE_CORNERS = 4


def is_number(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and value >= 0


def held(values, dotted):
    """The value of a dotted path of a profile, None where a part is missing."""
    for part in dotted.split("."):
        values = values.get(part) if isinstance(values, dict) else None
    return values


def wall_rules(key, level_profile, name, profile, near):
    """R362, R335. The rules of the walls of a build of the level profile name, refused with one line when keyRules codes a rule no build places
    walls by or a length is no number: higherBase and higherTop the plot of the base and the top, depth the yard depth, rise the top over the FFL
    and near keyPlans.wallEdgeM. None when keyRules holds none of the four wall rules, as a profile measured before R335 does, so it places no wall."""
    rules = (("wallBase", tuple(BASE_RULES)), ("wallTop", tuple(TOP_RULES)), ("wallLine", LINE_RULES), ("frontOpen", OPEN_RULES))
    if not any(entry in (key or {}) for entry, _ in rules):
        return None
    for entry, known in rules:
        if ((key or {}).get(entry) or {}).get("rule") not in known:
            raise project_profile.ProfileError(RULE_LINE % (entry, name, " or ".join(known)))
    depth, rise = level_profile.get("yardBelowFflM"), held(profile.values, "rev6.boundary_wall_top_above_ffl")
    for label, value in (("levelProfiles.%s.yardBelowFflM" % name, depth), ("rev6.boundary_wall_top_above_ffl", rise), ("keyPlans.wallEdgeM", near)):
        if not is_number(value):
            raise project_profile.ProfileError(NUMBER_LINE % label)
    return {"higherBase": BASE_RULES[key["wallBase"]["rule"]], "higherTop": TOP_RULES[key["wallTop"]["rule"]], "depth": float(depth),
            "rise": profile.metres("rev6.boundary_wall_top_above_ffl"), "near": float(near), "open": open_spans(key.get("wallOpen"), name)}


def open_spans(entry, name):
    """Q115. The spans of keyRules.wallOpen by unit pair, the two units in order joined by a bar, each the start and the end in metres from the
    front end of the edge, None when the profile holds no wallOpen."""
    if entry is None:
        return None
    spans = entry.get("spans") if isinstance(entry, dict) else None
    if not isinstance(entry, dict) or entry.get("rule") not in SPAN_RULES or not isinstance(spans, dict) or not all(
            isinstance(span, list) and len(span) == 2 and all(is_number(value) for value in span) and span[0] < span[1] for span in spans.values()):
        raise project_profile.ProfileError(SPAN_LINE % (name, " or ".join(SPAN_RULES)))
    return {pair: (float(span[0]), float(span[1])) for pair, span in spans.items()}


def edges_of(ring):
    return [(ring[index], ring[(index + 1) % len(ring)]) for index in range(len(ring))]


def same_edge(a, b, frame):
    """The edge a b is the front edge of the frame, its two ends those of the frame in either order."""
    if frame is None:
        return False
    left, right = (frame[0][0], frame[0][1]), (frame[1][0], frame[1][1])
    ends = ((a[0], a[1]), (b[0], b[1]))
    return ends == (left, right) or ends == (right, left)


def open_edge(ring, frame, rotation, mirrored, turned):
    """R362, breaker F2 of PR 79. The edge a wall build leaves open on a plot: the front of R355 given as frame, or on a plot a rotationDeg
    override turned, the edge of its ring whose outward normal runs most against the link Y axis that rotation and mirrored give after the
    overrides, the inward normal of the front as cad_links.link_axes makes it, the first such edge in ring order."""
    if not turned:
        return frame
    turn = math.radians(rotation)
    axis = (math.cos(turn), math.sin(turn))
    right = (-axis[0], -axis[1]) if mirrored else axis
    inward = (-right[1], right[0])
    twice = 0.0
    for a, b in edges_of(ring):
        twice += a[0] * b[1] - b[0] * a[1]
    best, found = None, frame
    for a, b in edges_of(ring):
        dx, dy = b[0] - a[0], b[1] - a[1]
        size = math.sqrt(dx * dx + dy * dy)
        if size == 0:
            continue
        # The outward normal lies right of the edge on an anticlockwise ring and left of it on a clockwise one
        outward = (dy / size, -dx / size) if twice > 0 else (-dy / size, dx / size)
        against = -(outward[0] * inward[0] + outward[1] * inward[1])
        if best is None or against > best:
            best, found = against, (a, b)
    return found


def bounds_of(ring):
    return min(point[0] for point in ring), min(point[1] for point in ring), max(point[0] for point in ring), max(point[1] for point in ring)


def pick(writers, ffls, higher):
    """The first plot in the order of writers with the highest FFL, or the lowest."""
    best = writers[0]
    for plot in writers[1:]:
        if (ffls[plot] > ffls[best]) if higher else (ffls[plot] < ffls[best]):
            best = plot
    return best


def walls(order, written, rings, frames, ffls, rules, fronts=None, kin=None):
    """R362. The boundary walls of the plots of order the plan writes, those of written, on the rings of every face of the site by plot, the open
    edges of the written plots as (left, right) and their FFL at 3 decimals. A wall in the order of its plot, its edge and its part. fronts
    holds the front of R355 of each written plot before a turn, so turned lists the plots whose front a wall lies on, which stands only as
    their turn opened another edge (Q120). kin holds the footprint cluster and the unit of each plot for keyRules.wallOpen (Q115)."""
    near = rules["near"]
    fronts = fronts or {}
    numbers = list(rings)
    bounds = [bounds_of(rings[number]) for number in numbers]
    tree = STRtree([box(*found) for found in bounds])
    found = []
    for plot in order:
        if plot not in written or plot not in rings:
            continue
        low_x, low_y, high_x, high_y = bounds_of(rings[plot])
        others = []
        for index in sorted(tree.query(box(low_x - near, low_y - near, high_x + near, high_y + near)).tolist()):
            other = numbers[index]
            o_low_x, o_low_y, o_high_x, o_high_y = bounds[index]
            if other == plot or o_low_x > high_x + near or o_high_x < low_x - near or o_low_y > high_y + near or o_high_y < low_y - near:
                continue
            frame, front = (frames.get(other), fronts.get(other)) if other in written else (None, None)
            others.extend((other, p, q, same_edge(p, q, frame), same_edge(p, q, front)) for p, q in edges_of(rings[other]))
        for edge, (a, b) in enumerate(edges_of(rings[plot])):
            if same_edge(a, b, frames.get(plot)):
                continue
            found.extend(edge_walls(plot, edge, a, b, others, written, ffls, rules, same_edge(a, b, fronts.get(plot)), kin, fronts.get(plot)))
    return found


def opening(plot, owners, written, a, b, length, rules, kin, front):
    """Q115. The part of the edge a b that keyRules.wallOpen leaves open for a part of owners, two written units of one footprint cluster whose
    pair the table holds, as start and end along the edge from a, measured from the end nearer the middle of front, else None."""
    spans = rules.get("open")
    if not spans or not kin or front is None or len(owners) != 2 or not all(other in written and other in kin for other in owners):
        return None
    (first, unit_a), (second, unit_b) = kin[owners[0]], kin[owners[1]]
    if first is None or first != second or not unit_a or not unit_b:
        return None
    span = spans.get("|".join(sorted((unit_a, unit_b))))
    if span is None:
        return None
    middle = ((front[0][0] + front[1][0]) / 2.0, (front[0][1] + front[1][1]) / 2.0)
    to_a = (a[0] - middle[0]) * (a[0] - middle[0]) + (a[1] - middle[1]) * (a[1] - middle[1])
    to_b = (b[0] - middle[0]) * (b[0] - middle[0]) + (b[1] - middle[1]) * (b[1] - middle[1])
    return span if to_a <= to_b else (length - span[1], length - span[0])


def edge_walls(plot, edge, a, b, others, written, ffls, rules, turned=False, kin=None, own_front=None):
    """R362. The walls of one edge: the edges of the other faces that lie along it within near, the parts between their ends, a part on the
    front of a written plot open and a part another written plot comes first on left to it. turned is the edge on the front of plot before
    its turn, and a part on the front of another written plot before its turn names that plot in turned (Q120)."""
    near = rules["near"]
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.sqrt(dx * dx + dy * dy)
    if length == 0:
        return []
    ux, uy = dx / length, dy / length
    spans = []
    for other, p, q, front, before in others:
        if abs((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) > near or abs((q[0] - a[0]) * uy - (q[1] - a[1]) * ux) > near:
            continue
        tp, tq = (p[0] - a[0]) * ux + (p[1] - a[1]) * uy, (q[0] - a[0]) * ux + (q[1] - a[1]) * uy
        low, high = max(0.0, min(tp, tq)), min(length, max(tp, tq))
        if high > low:
            spans.append((other, low, high, front, before))
    # An end of another edge within near of a kept cut or of the end of the edge parts nothing
    kept = [0.0]
    for cut in sorted(value for _, low, high, _, _ in spans for value in (low, high)):
        if cut - kept[-1] > near and length - cut > near:
            kept.append(cut)
    kept.append(length)
    parts = []
    for start, end in zip(kept, kept[1:]):
        middle = (start + end) / 2.0
        on = [(other, front, before) for other, low, high, front, before in spans if low <= middle <= high]
        owners = sorted({plot} | {other for other, _, _ in on})
        shut = any(front and other in written for other, front, _ in on)
        # A part that stands on the front a plot had before its turn stands as the turn opened another edge
        moved = ({plot} if turned else set()) | {other for other, _, before in on if before and other in written}
        if parts and parts[-1][2] == owners and parts[-1][3] == shut:
            parts[-1][1] = end
            parts[-1][4] |= moved
        else:
            parts.append([start, end, owners, shut, moved])
    found = []
    for start, end, owners, shut, moved in parts:
        writers = [other for other in owners if other in written and other in ffls]
        if shut or writers[0] != plot:
            continue
        base, top = pick(writers, ffls, rules["higherBase"]), pick(writers, ffls, rules["higherTop"])
        for low, high in pieces(start, end, opening(plot, owners, written, a, b, length, rules, kin, own_front), near):
            first = [a[0], a[1]] if low == 0.0 else [a[0] + ux * low, a[1] + uy * low]
            last = [b[0], b[1]] if high == length else [a[0] + ux * high, a[1] + uy * high]
            found.append({"plot": plot, "edge": edge, "plots": owners, "ends": [first, last], "base": ffls[base] - rules["depth"], "top": ffls[top] + rules["rise"],
                          "basePlot": base, "topPlot": top, "turned": [other for other in owners if other in moved]})
    return found


def pieces(start, end, gap, near):
    """Q115. The part start to end less the open gap, each piece left longer than near, the whole part when there is no gap."""
    if gap is None or gap[1] <= start or gap[0] >= end:
        return [(start, end)]
    found = []
    if gap[0] - start > near:
        found.append((start, gap[0]))
    if end - gap[1] > near:
        found.append((gap[1], end))
    return found


def line_rules(profile):
    """R415, R416. What a build under the Rev 6 mode reads for the walls of irregular plots: tolerance the degrees of
    rev6.irregular_tolerance_deg, refused when it is no number over 0 and under 45, and half of plot.boundary_wall_thickness in metres."""
    tolerance = held(profile.values, "rev6.irregular_tolerance_deg")
    if not is_number(tolerance) or not 0 < tolerance < TOLERANCE_LIMIT:
        raise project_profile.ProfileError(TOLERANCE_LINE)
    return {"tolerance": float(tolerance), "half": profile.metres(THICKNESS) / 2.0}


def turn_of(before, at, after):
    """The turn in degrees from the edge before to at onto the edge at to after, positive to the left, 0 for an edge that runs on."""
    ax, ay, bx, by = at[0] - before[0], at[1] - before[1], after[0] - at[0], after[1] - at[1]
    return math.degrees(math.atan2(ax * by - ay * bx, ax * bx + ay * by))


def corners(ring, tolerance):
    """R415. The turns of the corners of a ring after its repeated vertices are dropped and each vertex where the edges run on within
    tolerance degrees is merged, the first such vertex in ring order each time until none is left, so a collinear vertex parts no edge."""
    points = [point for index, point in enumerate(ring) if index == 0 or point != ring[index - 1]]
    while len(points) > 1 and points[-1] == points[0]:
        points.pop()
    while len(points) >= 3:
        turns = [turn_of(points[index - 1], points[index], points[(index + 1) % len(points)]) for index in range(len(points))]
        straight = next((index for index, turn in enumerate(turns) if abs(turn) <= tolerance), None)
        if straight is None:
            return turns
        del points[straight]
    return []


def irregular(ring, tolerance):
    """R415, Manual Rev 06 2.8. A face is irregular when its corners after the merge of corners are not 4, each within tolerance of 90."""
    turns = corners(ring, tolerance)
    return len(turns) != RECTANGLE_CORNERS or any(abs(abs(turn) - RIGHT_ANGLE) > tolerance for turn in turns)


def irregular_of(numbers, rings, tolerance):
    """R415. Each plot of numbers with a face, irregular or not."""
    return {number: irregular(rings[number], tolerance) for number in numbers if number in rings}


def inside(found, rings, written, irregulars, half, tolerance, near):
    """R416, Manual Rev 06 2.8.A. Under the Rev 6 mode a wall that stands on an irregular written plot runs with its outer face on the plot
    line, so its ends move half the thickness inside that plot: the plot it is filed under when that one is irregular, also when the other
    plot is irregular too, as the wall is written once, else the first irregular written plot of its plots. A wall on no irregular written
    plot keeps its centre on the edge. Each wall gets lineInsideM, the move, 0.0 or half. Then the moved walls that met at a face vertex
    meet again (mitre)."""
    before = {}
    for index, wall in enumerate(found):
        plot = wall["plot"]
        targets = [other for other in wall["plots"] if other in written and irregulars.get(other)]
        target = plot if plot in targets else (targets[0] if targets else None)
        if target is None:
            wall["lineInsideM"] = 0.0
            continue
        ring = rings[plot]
        a, b = ring[wall["edge"]], ring[(wall["edge"] + 1) % len(ring)]
        dx, dy = b[0] - a[0], b[1] - a[1]
        length = math.sqrt(dx * dx + dy * dy)
        ux, uy = dx / length, dy / length
        twice = 0.0
        for p, q in edges_of(ring):
            twice += p[0] * q[1] - q[0] * p[1]
        # The inside of the plot lies left of the edge on an anticlockwise ring and right of it on a clockwise one, the other plot of the edge
        # on the far side
        nx, ny = (-uy, ux) if twice > 0 else (uy, -ux)
        if target != plot:
            nx, ny = -nx, -ny
        before[index] = [[end[0], end[1]] for end in wall["ends"]]
        wall["ends"] = [[end[0] + nx * half, end[1] + ny * half] for end in wall["ends"]]
        wall["lineInsideM"] = half
    mitre(found, before, rings, tolerance, near)


def on_vertex(ring, edge, side, point):
    """R416. The index of the vertex of ring the end side, 0 the first and 1 the last, of a wall on edge sat on, None for an end mid edge."""
    index = (edge + side) % len(ring)
    return index if point[0] == ring[index][0] and point[1] == ring[index][1] else None


def next_point(ring, index, step):
    """R416. The first vertex of ring from index in the direction step, 1 on or -1 back, that is not that vertex, None where all are."""
    for count in range(1, len(ring)):
        found = ring[(index + step * count) % len(ring)]
        if found[0] != ring[index][0] or found[1] != ring[index][1]:
            return found
    return None


def partner(found, before, index, side, rings, tolerance, near, done):
    """R416. The moved wall and its end that meet the end side of the moved wall index at the face vertex that end sat on, before the move,
    as (other, end), else None. The other runs along the next edge of the face from that vertex, within near of it, and its end sat on a face
    vertex of its own plot within near of the first. An end mid edge, a corner whose edges run on within tolerance, as R415 merges them, or fold
    back, and an end whose next edge holds no moved wall have none."""
    wall = found[index]
    ring = rings[wall["plot"]]
    at = on_vertex(ring, wall["edge"], side, before[index][side])
    if at is None:
        return None
    vertex, far = ring[at], before[index][1 - side]
    following = next_point(ring, at, 1 if side == 1 else -1)
    if following is None:
        return None
    turn = abs(turn_of(far, vertex, following))
    if turn <= tolerance or turn >= STRAIGHT_ANGLE - tolerance:
        return None
    dx, dy = following[0] - vertex[0], following[1] - vertex[1]
    length = math.sqrt(dx * dx + dy * dy)
    ux, uy = dx / length, dy / length
    for other in before:
        if other == index:
            continue
        for end in (0, 1):
            point, tip = before[other][end], before[other][1 - end]
            px, py = point[0] - vertex[0], point[1] - vertex[1]
            if (other, end) in done or math.sqrt(px * px + py * py) > near:
                continue
            if on_vertex(rings[found[other]["plot"]], found[other]["edge"], end, point) is None:
                continue
            if abs((tip[0] - vertex[0]) * uy - (tip[1] - vertex[1]) * ux) <= near and (tip[0] - vertex[0]) * ux + (tip[1] - vertex[1]) * uy > 0:
                return other, end
    return None


def mitre(found, before, rings, tolerance, near):
    """R416, Manual Rev 06 2.8.A. The wall follows the exact plot shape with its outer face on the plot limit, so two moved walls whose ends
    met at a face vertex before the move meet again: both ends move to the crossing of their moved centre lines, the mitre point of the
    offset edges, which closes the square a notch corner left unbuilt and parts the overlap of a convex one. before holds the ends of each
    moved wall by its index before the move. In the order of the walls and their ends, each end once. Any other end stays square."""
    done = set()
    for index in before:
        for side in (0, 1):
            if (index, side) in done:
                continue
            pair = partner(found, before, index, side, rings, tolerance, near, done)
            if pair is None:
                continue
            other, end = pair
            first, second = found[index]["ends"][side], found[other]["ends"][end]
            ax, ay = before[index][1][0] - before[index][0][0], before[index][1][1] - before[index][0][1]
            bx, by = before[other][1][0] - before[other][0][0], before[other][1][1] - before[other][0][1]
            cross = ax * by - ay * bx
            if cross == 0:
                continue
            t = ((second[0] - first[0]) * by - (second[1] - first[1]) * bx) / cross
            found[index]["ends"][side] = [first[0] + t * ax, first[1] + t * ay]
            found[other]["ends"][end] = [first[0] + t * ax, first[1] + t * ay]
            done.update(((index, side), (other, end)))


def entry(wall):
    """The wall as the plan writes it, each value with its source, the plot and edge it runs along and the plots of its base and top."""
    sources = {"plots": PLOTS_SOURCE, "ends": INSIDE_SOURCE if wall.get("lineInsideM") else SOURCE, "base": SOURCE, "top": SOURCE}
    return {"plot": wall["plot"], "edge": wall["edge"], **{field: {"value": wall[field], "source": sources[field]} for field in FIELDS},
            "basePlot": wall["basePlot"], "topPlot": wall["topPlot"]}


def ffl_of(entry):
    """The FFL of a plan entry at the decimals of the plan, None for none."""
    value = (((entry or {}).get("levels") or {}).get("ffl") or {}).get("value")
    return None if value is None else common.fixed(value, plan_rules.FFL_DECIMALS)


def mark(item, wall, table, applied):
    """R401. The status of each value of a wall through valueStatus, applied the overrides of each plot in the order the build applied them.
    The base and the top take the FFL of the highest or lowest of the written plots of the wall, so every override of the plots of the wall,
    in their order, whose set holds the field touched it and is named, heightM the base and the top (Q112, Q113, breaker F3 of PR 79, R2 of the
    review of PR 85). A turn decides only whether a wall stands, so a rotation of a plot is named only on a wall of turned, and the plots of a
    wall come from the faces alone (Q120). The base and the top read what the overrides of the plot of their level still read (Q119)."""
    turned = set(wall.get("turned") or ())

    def kept(plot):
        return tuple(name for name in applied.get(plot) or () if name != ROTATION or plot in turned)
    done = tuple(name for plot in wall["plots"] for name in kept(plot))
    reading = {"base": kept(wall["basePlot"]), "top": kept(wall["topPlot"])}
    for field in FIELDS:
        item[field]["status"], detail, item[field]["statusFrom"] = table.of("walls." + field, item[field]["source"], None, done, reading=reading.get(field))
        table.used[item[field]["statusFrom"]] = detail


def summary(items, details):
    """R362, R401. The walls of the plan, by the number of plots they stand on, and by field and status."""
    by_field = {}
    for field in FIELDS:
        by_field["walls." + field] = dict(sorted(Counter(value_status.label(item[field], details) for item in items).items()))
    return {"walls": len(items), "byPlots": dict(sorted(Counter(str(len(item["plots"]["value"])) for item in items).items())), "statusByField": by_field}


def step(numbers, rows, trace):
    """R362. The step walls of the parity harness: per plot of the scope the walls the plan places from it, their edges, plots, ends, base and
    top, the plots of the base and top and the status of each value, and the table statusFrom of their details. R415, R416: under the Rev 6
    mode irregular for a plot with a face and lineInsideM the move of each wall inside a plot, both null under the past job."""
    built, items, details = trace.get("built") or {}, trace.get("walls") or [], trace.get("statusFrom") or {}
    irregulars, lines = trace.get("irregular"), trace.get("wallLines")
    by_plot, line_of, used, found = {}, {}, set(), {}
    for index, item in enumerate(items):
        by_plot.setdefault(item["plot"], []).append(item)
        if lines is not None:
            line_of.setdefault(item["plot"], []).append(lines[index])
    for number in numbers:
        mine = by_plot.get(number, [])
        used.update(item[field]["statusFrom"] for item in mine for field in FIELDS)
        found[number] = {"unit": (rows.get(number) or {}).get("typologyRev9") or None, "written": (built.get(number) or {}).get("written", False),
                         "edges": [item["edge"] for item in mine], "plots": [item["plots"]["value"] for item in mine], "ends": [item["ends"]["value"] for item in mine],
                         "base": [item["base"]["value"] for item in mine], "top": [item["top"]["value"] for item in mine],
                         "levelPlots": [[item["basePlot"], item["topPlot"]] for item in mine],
                         "status": [{field: {"status": item[field]["status"], "statusFrom": item[field]["statusFrom"]} for field in FIELDS} for item in mine],
                         "irregular": None if irregulars is None else irregulars.get(number),
                         "lineInsideM": None if lines is None else line_of.get(number, [])}
    return found, {"statusFrom": {key: details[key] for key in sorted(used)}}
