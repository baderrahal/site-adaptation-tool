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


def walls(order, written, rings, frames, ffls, rules):
    """R362. The boundary walls of the plots of order the plan writes, those of written, on the rings of every face of the site by plot, the front
    frames of the written plots as (left, right) and their FFL at 3 decimals. A wall in the order of its plot, its edge and its part."""
    near = rules["near"]
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
            frame = frames.get(other) if other in written else None
            others.extend((other, p, q, same_edge(p, q, frame)) for p, q in edges_of(rings[other]))
        for edge, (a, b) in enumerate(edges_of(rings[plot])):
            if same_edge(a, b, frames.get(plot)):
                continue
            found.extend(edge_walls(plot, edge, a, b, others, written, ffls, rules))
    return found


def edge_walls(plot, edge, a, b, others, written, ffls, rules):
    """R362. The walls of one edge: the edges of the other faces that lie along it within near, the parts between their ends, a part on the
    front of a written plot open and a part another written plot comes first on left to it."""
    near = rules["near"]
    dx, dy = b[0] - a[0], b[1] - a[1]
    length = math.sqrt(dx * dx + dy * dy)
    if length == 0:
        return []
    ux, uy = dx / length, dy / length
    spans = []
    for other, p, q, front in others:
        if abs((p[0] - a[0]) * uy - (p[1] - a[1]) * ux) > near or abs((q[0] - a[0]) * uy - (q[1] - a[1]) * ux) > near:
            continue
        tp, tq = (p[0] - a[0]) * ux + (p[1] - a[1]) * uy, (q[0] - a[0]) * ux + (q[1] - a[1]) * uy
        low, high = max(0.0, min(tp, tq)), min(length, max(tp, tq))
        if high > low:
            spans.append((other, low, high, front))
    # An end of another edge within near of a kept cut or of the end of the edge parts nothing
    kept = [0.0]
    for cut in sorted(value for _, low, high, _ in spans for value in (low, high)):
        if cut - kept[-1] > near and length - cut > near:
            kept.append(cut)
    kept.append(length)
    parts = []
    for start, end in zip(kept, kept[1:]):
        middle = (start + end) / 2.0
        on = [(other, front) for other, low, high, front in spans if low <= middle <= high]
        owners = sorted({plot} | {other for other, _ in on})
        shut = any(front and other in written for other, front in on)
        if parts and parts[-1][2] == owners and parts[-1][3] == shut:
            parts[-1][1] = end
        else:
            parts.append([start, end, owners, shut])
    found = []
    for start, end, owners, shut in parts:
        writers = [other for other in owners if other in written and other in ffls]
        if shut or writers[0] != plot:
            continue
        first = [a[0], a[1]] if start == 0.0 else [a[0] + ux * start, a[1] + uy * start]
        last = [b[0], b[1]] if end == length else [a[0] + ux * end, a[1] + uy * end]
        base, top = pick(writers, ffls, rules["higherBase"]), pick(writers, ffls, rules["higherTop"])
        found.append({"plot": plot, "edge": edge, "plots": owners, "ends": [first, last], "base": ffls[base] - rules["depth"], "top": ffls[top] + rules["rise"],
                      "basePlot": base, "topPlot": top})
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


def mark(item, wall, table, applied, entries, rules, higher):
    """R401. The status of each value of a wall through valueStatus, applied the overrides of each plot in the order the build applied them,
    entries the plan entries, rules those with no override and higher the wall rules. The inputs of the base and the top are the FFL of the pick
    of the rule, the plot of the highest or lowest FFL of the plots of the wall with no override, and that of the plot of the level, so each
    override on those plots whose set holds the field touched it and is named, the pick first (Q112, Q113, breaker F3 of PR 79), also where the
    value equals its value with no override. With no plan of the rule for a plot of the wall the plot of the level alone."""
    for field in FIELDS:
        level = {"base": wall["basePlot"], "top": wall["topPlot"]}.get(field)
        done = ()
        if level is not None:
            writers = [plot for plot in wall["plots"] if ffl_of(entries.get(plot)) is not None]
            rule_ffls = {plot: ffl_of(rules.get(plot)) for plot in writers}
            if rules and all(value is not None for value in rule_ffls.values()):
                picked = pick(writers, rule_ffls, higher["higherBase" if field == "base" else "higherTop"])
                done = tuple(applied.get(picked) or ()) + (tuple(applied.get(level) or ()) if level != picked else ())
            else:
                done = tuple(applied.get(level) or ())
        item[field]["status"], detail, item[field]["statusFrom"] = table.of("walls." + field, item[field]["source"], None, done)
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
