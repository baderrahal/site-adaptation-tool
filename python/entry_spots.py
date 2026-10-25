"""R327. Do the spot elevations at the plot entrance give El @ Entry? The schedules say the past levels came from plot entrance spots.

Per plot of an answer key read (R322) with one ramp of four corners: the entrance point is the middle of the higher long edge of the
ramp (R94). The spots whose value reads (R320) and whose elevation base is the survey point, so the value is a shared elevation, are
put in shared plan coordinates by the survey offset (R51). For each radius of entrySpots.radiiM the spots within it of the entrance point,
and the corner pair, the spots hosted by the ramp within entrySpots.cornerM of each corner of its higher long edge, whose mean is held
apart. Each is held against El @ Entry of the key, pastAll.entryParameter of the generic floor of the plot, and of the schedule by plot
number when one is given, within 1 mm and 5 mm, the shares over the plots compared. Written to entry_spots.json outside the repo,
counts, plot numbers and element ids only, no coordinate or level.
"""
import argparse
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

import entry_levels
import key_join
import key_reads
import link_summary
import plan_from_past
import plot_relations
import project_profile
import ramp_levels
from common import write_json

OUTPUT = "entry_spots.json"
SURVEY_BASE = "Survey Point"
# The bands in metres. Values are written to the mm by R2, so 1 mm is the rounding of two values
BANDS = (("1mm", 0.001), ("5mm", 0.005))
SLACK = 1e-7
SOURCES = ("key", "schedule")


def load_block(settings):
    """R327. entrySpots of the project settings: radiiM, the distances from the entrance point in metres, rising and above 0, cornerM, how
    near a spot must lie to a corner of the entrance edge, above 0, and the El @ Entry parameter of pastAll."""
    name = settings.path.name
    block = settings.values.get("entrySpots")
    radii = block.get("radiiM") if isinstance(block, dict) else None
    if not isinstance(radii, list) or not radii or not all(plan_from_past.is_number(value) and value > 0 for value in radii) or radii != sorted(set(radii)):
        raise project_profile.ProfileError(name + " entrySpots.radiiM must be distances in metres above 0, each larger than the one before")
    corner = block.get("cornerM")
    if not plan_from_past.is_number(corner) or corner <= 0:
        raise project_profile.ProfileError(name + " entrySpots.cornerM must be metres above 0")
    entry = (settings.values.get("pastAll") or {}).get("entryParameter")
    if not isinstance(entry, str) or not entry.strip():
        raise project_profile.ProfileError(name + " pastAll.entryParameter must name the El @ Entry parameter of a generic floor")
    return {"radiiM": radii, "cornerM": corner, "entryParameter": entry}


def usable_spots(document, offset):
    """R327. The spots whose value reads and whose base is the survey point, as (id, plan point, value, host id), and those left out by why."""
    found, left = [], Counter()
    for spot in document.get("spotElevations") or []:
        origin = spot.get("origin")
        if not plan_from_past.is_number(spot.get("value")):
            left["noValue"] += 1
        elif spot.get("elevationBase") != SURVEY_BASE:
            left["otherBase"] += 1
        elif not isinstance(origin, list) or len(origin) < 2:
            left["noOrigin"] += 1
        else:
            found.append((spot.get("elementId"), (origin[0] + offset[0], origin[1] + offset[1]), spot["value"], (spot.get("host") or {}).get("elementId")))
    return found, dict(sorted(left.items()))


def floors_by_plot(document, floors_config, kind):
    found = defaultdict(list)
    for floor in document.get("floors") or []:
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if plot is not None and plan_from_past.kind_of(floor, floors_config) == kind:
            found[plot].append(floor)
    return found


def within(value, target, limit):
    return abs(value - target) <= limit + SLACK


def key_entry(document, floors_config, block, schedule=None):
    """R327. Per plot the spots near its entrance and at the corners of its entrance edge, and per source of El @ Entry the counts."""
    offset = ramp_levels.offset_of(document)
    spots, left = usable_spots(document, offset)
    ramps = floors_by_plot(document, floors_config, "ramp")
    generics = floors_by_plot(document, floors_config, "generic")
    radii = [str(radius) for radius in block["radiiM"]]
    not_compared, plots, levels = defaultdict(list), {}, {}
    for plot in sorted(set(ramps) | set(generics), key=link_summary.sort_key):
        own = ramps.get(plot, [])
        if len(own) != 1:
            not_compared["noRamp" if not own else "twoRamps"].append(plot)
            continue
        corners, _ = ramp_levels.corner_points(own[0], offset)
        edge = entry_levels.high_long_edge(corners) if len(corners) == 4 else None
        if edge is None:
            not_compared["noEntrance"].append(plot)
            continue
        middle = edge["mid"]
        near = {name: sorted(spot_id for spot_id, point, _, _ in spots if math.dist(point, middle[:2]) <= radius + SLACK)
                for name, radius in zip(radii, block["radiiM"])}
        pair = []
        for corner in edge["high"]:
            close = sorted((math.dist(point, corner[:2]), spot_id) for spot_id, point, _, host in spots
                           if host == own[0].get("elementId") and math.dist(point, corner[:2]) <= block["cornerM"] + SLACK)
            pair.append(close[0][1] if close else None)
        generic = generics.get(plot, [])
        key_value = (generic[0].get("parameters") or {}).get(block["entryParameter"]) if len(generic) == 1 else None
        levels[plot] = {"key": key_value if plan_from_past.is_number(key_value) else None, "schedule": (schedule or {}).get(plot)}
        plots[plot] = {"ramp": own[0].get("elementId"), "near": near, "corners": pair}
    values = {spot_id: value for spot_id, _, value, _ in spots}
    counts, no_entry = {}, {}
    for source in SOURCES:
        compared = [plot for plot in plots if levels[plot][source] is not None]
        no_entry[source] = [plot for plot in plots if levels[plot][source] is None]
        total = len(compared)
        entry = {"plots": total}
        for name in radii:
            row = {"withSpot": sum(1 for plot in compared if plots[plot]["near"][name])}
            for band, limit in BANDS:
                row["within" + band] = sum(1 for plot in compared if any(within(values[spot_id], levels[plot][source], limit) for spot_id in plots[plot]["near"][name]))
            for band, _ in BANDS:
                row["share" + band] = round(row["within" + band] / total, 3) if total else None
            row["spots"] = sum(len(plots[plot]["near"][name]) for plot in compared)
            for band, limit in BANDS:
                row["spotsWithin" + band] = sum(1 for plot in compared for spot_id in plots[plot]["near"][name] if within(values[spot_id], levels[plot][source], limit))
            entry["radius " + name] = row
        paired = [plot for plot in compared if None not in plots[plot]["corners"]]
        pair = {"plots": len(paired)}
        for band, limit in BANDS:
            pair["within" + band] = sum(1 for plot in paired if within(sum(values[spot_id] for spot_id in plots[plot]["corners"]) / 2, levels[plot][source], limit))
        for band, _ in BANDS:
            pair["share" + band] = round(pair["within" + band] / len(paired), 3) if paired else None
        entry["cornerPair"] = pair
        counts[source] = entry
    order = ("noRamp", "twoRamps", "noEntrance")
    return {"counts": counts, "notCompared": {why: not_compared[why] for why in order if not_compared[why]}, "noEntry": no_entry,
            "spotsLeftOut": left, "spotsUsed": len(spots), "plots": plots}


def render(code, entry):
    parts = []
    for source, counts in entry["counts"].items():
        bands = {name: (row.get("withSpot", row.get("plots")), row["within1mm"], row["within5mm"]) for name, row in counts.items() if name != "plots"}
        parts.append("%s %d plots %s" % (source, counts["plots"], bands))
    return "%s (%s): spots used %d, %s, not compared %s" % (code, entry["job"], entry["spotsUsed"], "; ".join(parts),
                                                           {why: len(items) for why, items in entry["notCompared"].items()})


def main(argv=None):
    parser = argparse.ArgumentParser(description="R327. The spot elevations at the plot entrance of each answer key against El @ Entry.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=key_reads.parse_pair, required=True, metavar="CODE=JOB")
    parser.add_argument("--plots", help="plots.csv of the data book, El @ Entry of the schedule by official number")
    parser.add_argument("--out", required=True, help="the folder entry_spots.json is written to, outside the repo")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out.resolve())
    settings = project_profile.load_settings(args.settings)
    block = load_block(settings)
    schedule = key_join.read_plots_csv(args.plots)[1] if args.plots else None
    path = out / OUTPUT
    key_reads.earlier_keys(path)
    entries = {}
    for code, job in args.key:
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        entries[code] = dict(key_entry(document, floors, block, schedule), job=job)
    report = {"note": "R327. Per answer key the spots within each radius of the entrance point of each plot, the middle of the higher long edge of "
                      "its ramp (R94), and the spots at the two corners of that edge, against El @ Entry of the key and of the schedule. "
                      "A run writes the keys it is given and keeps the other keys of the file.",
              "settings": {"radiiM": block["radiiM"], "cornerM": block["cornerM"], "entryParameter": block["entryParameter"]},
              "keys": key_reads.merged(path, entries)}
    out.mkdir(parents=True, exist_ok=True)
    write_json(path, report)
    for code, entry in entries.items():
        print(render(code, entry))
    return 0


if __name__ == "__main__":
    sys.exit(main())
