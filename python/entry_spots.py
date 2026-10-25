"""R327. Do the spot elevations at the plot entrance give El @ Entry? The schedules say the past levels came from plot entrance spots.

Per plot of an answer key read (R322) with one ramp of four corners: the entrance point is the middle of the higher long edge of the
ramp (R94). The spots whose value reads (R320) and whose elevation base is the survey point, so the value is a shared elevation, are
put in shared plan coordinates by the survey offset (R51). A spot counts for a plot only when its host is a floor of that plot, by the
plot its Plot value names, so a spot on the ramp of the next plot never does. For each radius of entrySpots.radiiM the spots of the plot
within it of the entrance point, and the corner pair, the spots hosted by the ramp within entrySpots.cornerM of each corner of its higher
long edge, whose mean is held apart. Each is held against El @ Entry of the key, pastAll.entryParameter of the generic floor of the plot,
and of the schedule of the job of the key by the official number the join of R326 gives the plot, within each band of entrySpots.bandsM,
the shares over the plots compared. Written to entry_spots.json outside the repo, counts, plot numbers and element ids only, no
coordinate or level, each key with the settings it was run with.
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
SLACK = 1e-7
SOURCES = ("key", "schedule")


def load_block(settings):
    """R327. entrySpots of the project settings: radiiM, the distances from the entrance point in metres, rising and above 0, cornerM, how
    near a spot must lie to a corner of the entrance edge, above 0, bandsM, how near a spot value must lie to El @ Entry in metres, rising
    and above 0, 0.001 and 0.005 since values are written to the mm by R2, and the El @ Entry parameter of pastAll."""
    name = settings.path.name
    block = settings.values.get("entrySpots")
    radii = block.get("radiiM") if isinstance(block, dict) else None
    if not rising(radii):
        raise project_profile.ProfileError(name + " entrySpots.radiiM must be distances in metres above 0, each larger than the one before")
    corner = block.get("cornerM")
    if not plan_from_past.is_number(corner) or corner <= 0:
        raise project_profile.ProfileError(name + " entrySpots.cornerM must be metres above 0")
    bands = block.get("bandsM")
    if not rising(bands):
        raise project_profile.ProfileError(name + " entrySpots.bandsM must be metres above 0, each larger than the one before")
    entry = (settings.values.get("pastAll") or {}).get("entryParameter")
    if not isinstance(entry, str) or not entry.strip():
        raise project_profile.ProfileError(name + " pastAll.entryParameter must name the El @ Entry parameter of a generic floor")
    return {"radiiM": radii, "cornerM": corner, "bandsM": bands, "entryParameter": entry}


def rising(values):
    return isinstance(values, list) and bool(values) and all(plan_from_past.is_number(value) and value > 0 for value in values) and values == sorted(set(values))


def band_names(bands):
    """R327. The name of each band by its millimetres, 0.001 is 1mm."""
    return ["%gmm" % round(band * 1000.0, 6) for band in bands]


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


def spots_by_plot(document, spots):
    """R327. The usable spots by the plot of their host floor, the plot its Plot value names, and the count of those on no such floor."""
    host_plot = {}
    for floor in document.get("floors") or []:
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if plot is not None:
            host_plot[str(floor.get("elementId"))] = plot
    found, alone = defaultdict(list), 0
    for spot in spots:
        plot = host_plot.get(str(spot[3]))
        if plot is None:
            alone += 1
        else:
            found[plot].append(spot)
    return found, alone


def key_entry(document, floors_config, block, schedule=None, numbers=None):
    """R327. Per plot the spots of its own floors near its entrance and at the corners of its entrance edge, and per source of El @ Entry
    the counts. schedule is El @ Entry of the schedule of the job of the key by official number and numbers the official number of each
    plot of the key by the join of R326, so a plot the join gives no number has no schedule value."""
    offset = ramp_levels.offset_of(document)
    spots, left = usable_spots(document, offset)
    own_spots, alone = spots_by_plot(document, spots)
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
        mine = own_spots.get(plot, [])
        near = {name: sorted(spot_id for spot_id, point, _, _ in mine if math.dist(point, middle[:2]) <= radius + SLACK)
                for name, radius in zip(radii, block["radiiM"])}
        pair = []
        for corner in edge["high"]:
            close = sorted((math.dist(point, corner[:2]), spot_id) for spot_id, point, _, host in mine
                           if str(host) == str(own[0].get("elementId")) and math.dist(point, corner[:2]) <= block["cornerM"] + SLACK)
            pair.append(close[0][1] if close else None)
        generic = generics.get(plot, [])
        key_value = (generic[0].get("parameters") or {}).get(block["entryParameter"]) if len(generic) == 1 else None
        official = (numbers or {}).get(plot)
        levels[plot] = {"key": key_value if plan_from_past.is_number(key_value) else None, "schedule": (schedule or {}).get(official) if official else None}
        plots[plot] = {"ramp": own[0].get("elementId"), "near": near, "corners": pair}
    values = {spot_id: value for spot_id, _, value, _ in spots}
    bands = list(zip(band_names(block["bandsM"]), block["bandsM"]))
    counts, no_entry = {}, {}
    for source in SOURCES:
        compared = [plot for plot in plots if levels[plot][source] is not None]
        no_entry[source] = [plot for plot in plots if levels[plot][source] is None]
        total = len(compared)
        entry = {"plots": total}
        for name in radii:
            row = {"withSpot": sum(1 for plot in compared if plots[plot]["near"][name])}
            for band, limit in bands:
                row["within" + band] = sum(1 for plot in compared if any(within(values[spot_id], levels[plot][source], limit) for spot_id in plots[plot]["near"][name]))
            for band, _ in bands:
                row["share" + band] = round(row["within" + band] / total, 3) if total else None
            row["spots"] = sum(len(plots[plot]["near"][name]) for plot in compared)
            for band, limit in bands:
                row["spotsWithin" + band] = sum(1 for plot in compared for spot_id in plots[plot]["near"][name] if within(values[spot_id], levels[plot][source], limit))
            entry["radius " + name] = row
        paired = [plot for plot in compared if None not in plots[plot]["corners"]]
        pair = {"plots": len(paired)}
        for band, limit in bands:
            pair["within" + band] = sum(1 for plot in paired if within(sum(values[spot_id] for spot_id in plots[plot]["corners"]) / 2, levels[plot][source], limit))
        for band, _ in bands:
            pair["share" + band] = round(pair["within" + band] / len(paired), 3) if paired else None
        entry["cornerPair"] = pair
        counts[source] = entry
    order = ("noRamp", "twoRamps", "noEntrance")
    return {"counts": counts, "notCompared": {why: not_compared[why] for why in order if not_compared[why]}, "noEntry": no_entry,
            "spotsLeftOut": left, "spotsUsed": len(spots), "spotsOnNoPlotFloor": alone, "plots": plots}


def render(code, entry):
    parts = []
    for source, counts in entry["counts"].items():
        bands = {name: tuple([row.get("withSpot", row.get("plots"))] + [value for field, value in row.items() if field.startswith("within")])
                 for name, row in counts.items() if name != "plots"}
        parts.append("%s %d plots %s" % (source, counts["plots"], bands))
    return "%s (%s): spots used %d, %s, not compared %s" % (code, entry["job"], entry["spotsUsed"], " and ".join(parts),
                                                           {why: len(items) for why, items in entry["notCompared"].items()})


def main(argv=None):
    parser = argparse.ArgumentParser(description="R327. The spot elevations at the plot entrance of each answer key against El @ Entry.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=key_reads.parse_pair, required=True, metavar="CODE=JOB")
    parser.add_argument("--plots", help="plots.csv of the data book, El @ Entry of the schedule by official number")
    parser.add_argument("--join", help="key_join.json of R326, the official number of each plot, needed with --plots")
    parser.add_argument("--out", required=True, help="the folder entry_spots.json is written to, outside the repo")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    if args.plots and not args.join:
        parser.error("--plots needs --join, the schedule is found by the official number the join gives each plot")
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out.resolve())
    settings = project_profile.load_settings(args.settings)
    block = load_block(settings)
    schedule = key_join.read_plots_csv(args.plots)[1] if args.plots else None
    joined = key_reads.load(args.join)["keys"] if args.join else {}
    path = out / OUTPUT
    key_reads.earlier_keys(path)
    entries = {}
    for code, job in args.key:
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        own = key_join.schedule_of(schedule, code, settings.get("pastAll.scheduleKeys") or {}) if schedule else None
        numbers = {plot: row["number"] for plot, row in ((joined.get(code) or {}).get("join") or {}).get("plots", {}).items() if row["status"] == key_join.JOINED}
        if schedule and code not in joined:
            parser.error("the join " + str(args.join) + " holds no key " + code)
        entries[code] = dict(key_entry(document, floors, block, own, numbers), job=job,
                             settings={"radiiM": block["radiiM"], "cornerM": block["cornerM"], "bandsM": block["bandsM"], "entryParameter": block["entryParameter"]},
                             inputs={"plots": Path(args.plots).name if args.plots else None, "join": Path(args.join).name if args.join else None})
    report = {"note": "R327. Per answer key the spots of its own floors within each radius of the entrance point of each plot, the middle of the "
                      "higher long edge of its ramp (R94), and the spots at the two corners of that edge, against El @ Entry of the key and of the "
                      "schedule of its job by official number. A run writes the keys it is given with the settings it ran with and keeps the other "
                      "keys of the file.",
              "keys": key_reads.merged(path, entries)}
    out.mkdir(parents=True, exist_ok=True)
    write_json(path, report)
    for code, entry in entries.items():
        print(render(code, entry))
    return 0


if __name__ == "__main__":
    sys.exit(main())
