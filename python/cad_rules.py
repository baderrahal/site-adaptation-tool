"""Rules tested on the data of the past job: the villa hand, the second CASE letter and the FFL split (R131 to R133).

Each test reports a count and the plots that break it, the cut plots of R120 and cadLinks.cutNeighbours apart. A test that
holds on every plot outside the cut plots may become a rule of the plan, one that does not is only reported. No value of
the project sits here, the typologies, parameter names and limits come from the profile and the settings.
"""
import argparse
import itertools
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import levels
import levels_proof
import link_summary
import link_table
import plan_from_past
import project_profile
import prototype_offsets
import ramp_levels
import yard_rule

SIDES = ("left", "right")
FFL_SAME = 0.0005


def unit_plots(plots, shift, units, unit_types):
    """The official numbers of the faces that carry a unit: a schedule unit, or a label type of analysis.unitPlotTypes."""
    found = set(units)
    for entry in plots:
        label = yard_rule.whole_label(entry.get("plot"))
        if entry.get("kind") == "plot_shape" and label is not None and entry.get("type") in unit_types:
            found.add(str(label + shift))
    return found


def side_neighbours(plots_wanted, faces, frames, carriers, wall):
    """R131. The side by side neighbours of each plot that carry a unit, left and right as seen from the road.

    A neighbour shares at least wall metres of boundary with the plot, the party wall of R64. A face with no unit, an open
    space or no face at all, leaves that side open.
    """
    numbers = sorted(faces, key=int)
    tree = STRtree([faces[number] for number in numbers]) if numbers else None
    found = {}
    for plot in plots_wanted:
        frame, face = frames.get(plot), faces.get(plot)
        if frame is None or face is None:
            continue
        sides = {side: [] for side in SIDES}
        for index in tree.query(face):
            other = numbers[int(index)]
            if other == plot or other not in carriers or face.boundary.intersection(faces[other].boundary).length < wall:
                continue
            centre = faces[other].centroid
            dx, dy = centre.x - face.centroid.x, centre.y - face.centroid.y
            sides["right" if dx * frame["r"][0] + dy * frame["r"][1] > 0 else "left"].append(other)
        found[plot] = {side: sorted(items, key=int) for side, items in sides.items()}
    return found


def villa_rows(sides):
    """R131. The rows of villas: villas joined through side neighbours that are villas too, each row in plot order."""
    parent = {plot: plot for plot in sides}

    def root(plot):
        while parent[plot] != plot:
            plot = parent[plot]
        return plot

    for plot, by_side in sides.items():
        for side in SIDES:
            for other in by_side[side]:
                if other in parent:
                    parent[root(other)] = root(plot)
    rows = defaultdict(list)
    for plot in sides:
        rows[root(plot)].append(plot)
    return sorted((sorted(row, key=int) for row in rows.values()), key=lambda row: int(row[0]))


def one_side(by_side):
    """left or right when exactly one side has a neighbour, else None."""
    held = [side for side in SIDES if by_side[side]]
    return held[0] if len(held) == 1 else None


def reversed_villas(links, units, sides, apart):
    """R131. The villa typologies drawn the other hand, measured: most of their villas with one neighbour side are mirrored
    when the neighbour is on the right, where the others are mirrored when it is on the left. Counts per typology go with it."""
    counts = defaultdict(Counter)
    for plot, by_side in sides.items():
        side = one_side(by_side)
        if plot in apart or side is None or plot not in links:
            continue
        counts[units[plot]]["same" if links[plot]["mirrored"] == (side == "left") else "other"] += 1
    flipped = sorted(unit for unit, table in counts.items() if table["other"] > table["same"])
    return flipped, {unit: dict(table) for unit, table in sorted(counts.items())}


def hand(unit, mirrored, flipped):
    """The hand of a unit as built: its mirror flag, turned for a typology drawn the other hand."""
    return mirrored != (unit in flipped)


def villa_tests(links, units, sides, apart, driver_house):
    """R131. Tests a to d of the villa mirror and the full rule they would make, each with its count and the plots that break it.

    a: all villas of a row share one hand. b: a villa with one neighbour side has its blind side to it, so its hand is the
    neighbour being on the left. c: test b on the typologies with a driver's house, per typology. d: the mirror flag alone
    is the neighbour being on the left, per typology. full: a villa with one neighbour side takes b and every villa of its
    row takes the hand of the row's b villas when they agree, a villa no b villa reaches is undecided.
    """
    flipped, flip_counts = reversed_villas(links, units, sides, apart)
    hands = {plot: hand(units[plot], links[plot]["mirrored"], flipped) for plot in sides if plot in links}
    rows = villa_rows(sides)
    report = {"reversed": flipped, "reversedCounts": flip_counts}
    table, breaks = Counter(), []
    for row in rows:
        kept = [plot for plot in row if plot not in apart and plot in hands]
        if len(row) < 2:
            table["single"] += 1
        elif len({hands[plot] for plot in kept}) <= 1:
            table["holds"] += 1
        else:
            table["breaks"] += 1
            breaks.append(row)
    report["a"] = {"rows": dict(table), "breaks": breaks}
    b_table, b_breaks, per_unit, d_table = Counter(), [], defaultdict(Counter), defaultdict(Counter)
    for plot in sorted(sides, key=int):
        side = one_side(sides[plot])
        if plot in apart or plot not in hands or side is None:
            continue
        holds = hands[plot] == (side == "left")
        b_table["holds" if holds else "breaks"] += 1
        per_unit[units[plot]]["holds" if holds else "breaks"] += 1
        d_table[units[plot]]["holds" if links[plot]["mirrored"] == (side == "left") else "breaks"] += 1
        if not holds:
            b_breaks.append(plot)
    report["b"] = {"counts": dict(b_table), "breaks": b_breaks}
    report["c"] = {unit: dict(per_unit[unit]) for unit in driver_house}
    report["d"] = {unit: dict(table) for unit, table in sorted(d_table.items())}
    full, wrong, undecided = Counter(), [], []
    for row in rows:
        decided = {plot: hands[plot] for plot in row if plot in hands and plot not in apart and one_side(sides[plot]) is not None}
        votes = {one_side(sides[plot]) == "left" for plot in decided}
        for plot in row:
            if plot in apart or plot not in hands:
                continue
            if one_side(sides[plot]) is not None:
                want = one_side(sides[plot]) == "left"
            elif len(votes) == 1:
                want = next(iter(votes))
            else:
                full["undecided"] += 1
                undecided.append(plot)
                continue
            full["holds" if hands[plot] == want else "breaks"] += 1
            if hands[plot] != want:
                wrong.append(plot)
    report["full"] = {"counts": dict(full), "breaks": wrong, "undecided": undecided}
    report["holdsOnEveryVilla"] = not wrong and not undecided
    return report


def facing_pairs(links):
    """R132. The CASE letters by facing, the direction of the link Y axis, and the first letter of each pair."""
    by_facing = defaultdict(set)
    for link in links.values():
        if link["case"]:
            by_facing[round((link["rotation"] + (-90.0 if link["mirrored"] else 90.0)) % 360.0, 1) % 360.0].add(link["case"])
    return {letter: sorted(letters)[0] for letters in by_facing.values() for letter in letters}


def case_tests(links, features, apart):
    """R132. For each feature, how many links its value explains the second letter on within each facing pair, the letter most
    links of that pair and value carry, the lower letter on a tie, all links and the links outside apart, and the links that break it."""
    first = facing_pairs(links)
    report = {}
    for name, read in features.items():
        groups = defaultdict(Counter)
        for plot, link in links.items():
            if link["case"]:
                groups[(first[link["case"]], read(plot, link))][link["case"]] += 1
        # A tie goes to the lower letter, so the order of the links never decides
        best = {key: min(letter for letter, count in table.items() if count == max(table.values())) for key, table in groups.items()}
        breaks = sorted((plot for plot, link in links.items() if link["case"] and best[(first[link["case"]], read(plot, link))] != link["case"]), key=int)
        counted = [plot for plot, link in links.items() if link["case"]]
        report[name] = {"values": len({key[1] for key in groups}), "explained": len(counted) - len(breaks), "links": len(counted),
                        "explainedOutside": sum(1 for plot in counted if plot not in apart and plot not in breaks), "outside": sum(1 for plot in counted if plot not in apart),
                        "breaksOutside": [plot for plot in breaks if plot not in apart], "holdsOnAll": not breaks}
    return report


def piece_ffl(profile, rows):
    base = max(levels.base_height(profile, rows[0]["unit"]), levels.base_height(profile, rows[-1]["unit"]))
    return round(max(row["entry"] for row in rows) + base, levels.FFL_DECIMALS)


def walls_of(values):
    return [index + 1 for index in range(len(values) - 1) if abs(values[index + 1] - values[index]) > FFL_SAME]


def split_rule(profile, rows):
    """R133. The walls the tested split rule picks: none when one FFL keeps every height at most the top of steps.legend,
    else the fewest allowed walls (R125) whose pieces, each with its own FFL, keep every height inside the legend span.
    Returns the walls, or None when no split brings them all inside, and how many splits of that size would do."""
    low, high = levels.height_span(profile)
    slack = levels.HALF_MM + levels.EPSILON
    whole = piece_ffl(profile, rows)
    if all(whole - row["entry"] <= high + slack for row in rows):
        return [], 0
    count, allowed = len(rows), set(profile.get("cluster_split_positions"))
    walls = [wall for wall in range(1, count) if count == profile.get("cluster_max_units") and {wall, count - wall} & allowed]
    for size in range(1, len(walls) + 1):
        found = []
        for chosen in itertools.combinations(walls, size):
            cuts = [0, *chosen, count]
            pieces = [rows[start:end] for start, end in zip(cuts, cuts[1:])]
            if all(low - slack <= piece_ffl(profile, piece) - row["entry"] <= high + slack for piece in pieces for row in piece):
                found.append(list(chosen))
        if found:
            return found[0], len(found)
    return None, 0


def split_tests(profile, clusters, books):
    """R133. The split rule against the schedule splits on every cluster whose plots all have a schedule row, per neighbourhood,
    and the FFL of each piece at the schedule walls against the schedule FFL."""
    counts, rows_out, pieces = Counter(), [], Counter()
    per_code = Counter()
    for hood, (shift, records) in sorted(books.items()):
        by_plot = {record["plot"]: record for record in records}
        for cluster in clusters:
            plots = [str(int(label) + shift) for label in cluster["plots"]]
            if not all(plot in by_plot for plot in plots):
                continue
            rows = [by_plot[plot] for plot in plots]
            sheet = walls_of([row["ffl"] for row in rows])
            rule, ties = split_rule(profile, rows)
            result = "no valid split" if rule is None else "same" if rule == sheet else "differs"
            counts[hood + " " + ("sheet split" if sheet else "sheet one FFL") + " " + result] += 1
            per_code[hood + " " + str(cluster.get("code")) + (" split" if sheet else " one FFL")] += 1
            rows_out.append({"neighbourhood": hood, "code": cluster.get("code"), "plots": plots, "sheetWalls": sheet, "ruleWalls": rule, "ties": ties})
            cuts = [0, *sheet, len(rows)]
            for start, end in zip(cuts, cuts[1:]):
                if sheet:
                    ffl = piece_ffl(profile, rows[start:end])
                    for row in rows[start:end]:
                        pieces["equal" if abs(ffl - row["ffl"]) <= FFL_SAME else "differs"] += 1
    return {"counts": dict(sorted(counts.items())), "perCode": dict(sorted(per_code.items())), "piecesAtSheetWalls": dict(pieces),
            "reproducesEverySplit": all(row["ruleWalls"] == row["sheetWalls"] for row in rows_out if row["sheetWalls"]) and any(row["sheetWalls"] for row in rows_out),
            "missed": [row for row in rows_out if row["ruleWalls"] != row["sheetWalls"]], "clusters": rows_out}


def schedule_books(settings, shifts_config, root):
    """Every schedule of settings.schedules with the shift of its neighbourhood, taken from the file name (R28)."""
    pattern = re.compile(shifts_config["pattern"])
    books = {}
    for path in levels_proof.schedule_paths(root, settings):
        found = pattern.search(path.name)
        if not found or found.group(1) not in shifts_config["shifts"]:
            raise project_profile.ProfileError(path.name + " names no neighbourhood with a shift")
        books[found.group(1)] = (shifts_config["shifts"][found.group(1)], levels_proof.read_schedule(path))
    return books


def main(argv=None):
    parser = argparse.ArgumentParser(description="Test the villa hand, the second CASE letter and the FFL split on the past job (R131 to R133).")
    parser.add_argument("out_dir", help="where cad_rules.json is written, keep it outside git")
    parser.add_argument("--roads", required=True)
    parser.add_argument("--analysis", required=True)
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        analysis = settings.get("analysis")
        job = plan_from_past.load_job(settings)
        block = cad_links.cad_links_settings(settings)
        shifts_config = link_table.load_shifts(args.shifts)
        shift = shifts_config["shifts"][job["neighbourhood"]]
        plots = link_summary.load(Path(args.baseplan or project_profile.ROOT / job["baseplanPlots"]))["plots"]
        faces, _ = yard_rule.face_shapes(plots, shift)
        document = ramp_levels.host_document(link_summary.load(job["prototypes"]))
        links, _, _ = cad_links.read_past(document, ramp_levels.offset_of(document), plan_from_past.load_floors(settings))
        roads = cad_analysis.read_road_edges(args.roads, analysis["roadEdgeLayers"])
        records = levels_proof.read_analysis(args.analysis)
        clusters = records[0]
        units = {str(int(record["plot"]) + shift): record["unit"] for record in link_summary.load(args.analysis)["plots"] if record.get("unit")}
        apart = set(prototype_offsets.plot_frame(prototype_offsets.load()).get("apart") or []) | set(block.get("cutNeighbours", []))
        limits = cad_links.front_limits(profile, analysis)
        road_tree = STRtree(roads)
        typologies = profile.get("typologies")
        villas = sorted((plot for plot in links if typologies.get(units.get(plot), {}).get("kind") == "villa"), key=int)
        frames = {plot: cad_links.front_frame(faces.get(plot), road_tree, roads, limits) for plot in links}
        carriers = unit_plots(plots, shift, units, set(analysis["unitPlotTypes"]))
        sides = side_neighbours(villas, faces, frames, carriers, limits[1])
        report = {"villas": villa_tests(links, units, sides, apart, profile.get("villa.driver_house"))}
        report["villas"]["sides"] = sides
        drawn = prototype_offsets.plot_frame(prototype_offsets.load())["units"]
        flipped = set(report["villas"]["reversed"]) | {unit for unit, entry in drawn.items() if entry.get("drawnSide") == "right"}
        options, colours = block["caseParameters"]
        features = {
            "facadeOption": lambda plot, link: link["parameters"].get(options),
            "facadeColor": lambda plot, link: link["parameters"].get(colours),
            "steps": lambda plot, link: link["parameters"].get(plan_from_past.STEPS_PARAMETER),
            "unit": lambda plot, link: link["unit"],
            "corner": lambda plot, link: bool(frames.get(plot) and frames[plot]["tied"]),
            "mirrorPerUnit": lambda plot, link: (link["unit"], link["mirrored"]),
            "hand": lambda plot, link: hand(link["unit"], link["mirrored"], flipped),
        }
        for link in document.get("links") or []:
            number = plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER))
            if number in links:
                links[number]["parameters"] = link.get("parameters") or {}
        report["case"] = {"flipped": sorted(flipped), "tests": case_tests(links, features, apart)}
        root = plan_from_past.input_root(settings)
        books = schedule_books(settings, {"pattern": shifts_config["pattern"], "shifts": shifts_config["shifts"]}, root)
        report["split"] = split_tests(profile, clusters, books)
        report["apart"] = sorted(apart, key=int)
        common.write_json(Path(args.out_dir) / "cad_rules.json", report)
        print("villas a %s, b %s, full %s" % (report["villas"]["a"]["rows"], report["villas"]["b"]["counts"], report["villas"]["full"]["counts"]))
        print("case %s" % {name: (test["explained"], test["links"]) for name, test in report["case"]["tests"].items()})
        print("split %s" % report["split"]["counts"])
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("cad_rules: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
