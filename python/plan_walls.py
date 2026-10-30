"""R362. The boundary walls of a build by the rules R335 measured on the answer keys, Walls of Common in C# gives the same (R345).

A wall runs along each edge of the face of a plot the plan writes but its front edge, which the keys leave open (keyRules.frontOpen all), its
centre on the edge (keyRules.wallLine centreOnTheEdge). An edge another face lies along within keyPlans.wallEdgeM is parted where the edges of
those faces end, so each part is one wall on the plots it divides, placed once from the first of its written plots in ordinal order
of the plot numbers. The base is the yard level, the FFL less yardBelowFflM of the level profile, of the higher or the lower plot of keyRules.wallBase,
and the top the FFL of the plot of keyRules.wallTop plus rev6.boundary_wall_top_above_ffl, weak until Q071 is answered. The garden walls have
no measured rule, so a build places none (conflict 8 of docs/design/10-rule-ids.md). Where a wall starts and ends along its edge no key rule
measures, so the ends of a wall carry UNKNOWN through valueStatus.
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
STEP_FIELDS = ("unit", "written", "edges", "plots", "ends", "base", "top", "levelPlots", "status")
SOURCE = "keyRules"
PLOTS_SOURCE = "locationPlan"
# The override of the list that turns a link, plan_from_book.moved_link
ROTATION = "rotationDeg"
# The candidates of key_rules.wall_base_candidates and wall_top_candidates, True for the higher plot of the wall
BASE_RULES = {"yardLevelOfTheHigherPlot": True, "yardLevelOfTheLowerPlot": False}
TOP_RULES = {"higherFflPlusRev6": True, "lowerFflPlusRev6": False}
LINE_RULES = ("centreOnTheEdge",)
OPEN_RULES = ("all",)
RULE_LINE = "keyRules.%s of %s holds no rule a build places boundary walls by, it takes %s (R362)"
NUMBER_LINE = "%s is no number of 0 or more, a build places no boundary wall without it (R362)"


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
            "rise": profile.metres("rev6.boundary_wall_top_above_ffl"), "near": float(near)}


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


def walls(order, written, rings, frames, ffls, rules, fronts=None):
    """R362. The boundary walls of the plots of order the plan writes, those of written, on the rings of every face of the site by plot, the open
    edges of the written plots as (left, right) and their FFL at 3 decimals. A wall in the order of its plot, its edge and its part. fronts
    holds the front of R355 of each written plot before a turn, so turned lists the plots whose front a wall lies on, which stands only as
    their turn opened another edge (Q120)."""
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
            found.extend(edge_walls(plot, edge, a, b, others, written, ffls, rules, same_edge(a, b, fronts.get(plot))))
    return found


def edge_walls(plot, edge, a, b, others, written, ffls, rules, turned=False):
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
        first = [a[0], a[1]] if start == 0.0 else [a[0] + ux * start, a[1] + uy * start]
        last = [b[0], b[1]] if end == length else [a[0] + ux * end, a[1] + uy * end]
        base, top = pick(writers, ffls, rules["higherBase"]), pick(writers, ffls, rules["higherTop"])
        found.append({"plot": plot, "edge": edge, "plots": owners, "ends": [first, last], "base": ffls[base] - rules["depth"], "top": ffls[top] + rules["rise"],
                      "basePlot": base, "topPlot": top, "turned": [other for other in owners if other in moved]})
    return found


def entry(wall):
    """The wall as the plan writes it, each value with its source, the plot and edge it runs along and the plots of its base and top."""
    sources = {"plots": PLOTS_SOURCE, "ends": SOURCE, "base": SOURCE, "top": SOURCE}
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
    top, the plots of the base and top and the status of each value, and the table statusFrom of their details."""
    built, items, details = trace.get("built") or {}, trace.get("walls") or [], trace.get("statusFrom") or {}
    by_plot, used, found = {}, set(), {}
    for item in items:
        by_plot.setdefault(item["plot"], []).append(item)
    for number in numbers:
        mine = by_plot.get(number, [])
        used.update(item[field]["statusFrom"] for item in mine for field in FIELDS)
        found[number] = {"unit": (rows.get(number) or {}).get("typologyRev9") or None, "written": (built.get(number) or {}).get("written", False),
                         "edges": [item["edge"] for item in mine], "plots": [item["plots"]["value"] for item in mine], "ends": [item["ends"]["value"] for item in mine],
                         "base": [item["base"]["value"] for item in mine], "top": [item["top"]["value"] for item in mine],
                         "levelPlots": [[item["basePlot"], item["topPlot"]] for item in mine],
                         "status": [{field: {"status": item[field]["status"], "statusFrom": item[field]["statusFrom"]} for field in FIELDS} for item in mine]}
    return found, {"statusFrom": {key: details[key] for key in sorted(used)}}
