"""Measures El @ Entry on the past job (R93) and proves the entry rule on the schedule plots (R95).

Everything is read in place and the reports go outside git with counts and distances only. measure compares the
El @ Entry of every plot of the past job that has a ramp with each candidate point of that ramp and with the road.
It also writes the entry section of config/prototype_offsets.json. proof works out the entry level of every plot of
the schedule workbooks from the road and the front edge of its plot face, and compares it with the sheet.
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import Point
from shapely.strtree import STRtree

import cad_analysis
import common
import entry_levels
import levels_proof
import link_table
import plan_from_past
import project_profile
import prototype_offsets
import ramp_levels
import road_lookup

SEED = 13
LISTED = 15
MEASURED = "measured from past work"


def centre_of(points):
    return Point(sum(point[0] for point in points) / len(points), sum(point[1] for point in points) / len(points))


def join_by_centre(point, polygons, tree):
    """The one face that contains the point, else None."""
    hit = [int(index) for index in tree.query(point) if polygons[int(index)].contains(point)]
    return hit[0] if len(hit) == 1 else None


def road_candidates(lookup, ramp_points, front):
    """The road levels that could stand for the entry level of a ramp plot, name to level, left out where no surface covers the point."""
    found = {}
    covered = [z for z, _ in lookup.levels([(point[0], point[1]) for point in ramp_points]) if z is not None]
    if covered:
        found["road at the covered ramp corners, mean"] = sum(covered) / len(covered)
        found["road at the covered ramp corners, highest"] = max(covered)
    if front is not None:
        for name, point in (("road at the curb point of the front edge", front["curb"]), ("road at the midpoint of the front edge", front["mid"]),
                            ("road half way from the curb to the front edge", ((front["curb"][0] + front["mid"][0]) / 2, (front["curb"][1] + front["mid"][1]) / 2))):
            z, _ = lookup.level_at(point[0], point[1])
            if z is not None:
                found[name] = z
    return found


def measure(ramps, links, schedule, faces, lookup, roads):
    """R93. For each ramp plot the difference El @ Entry minus each candidate, and the spread of each candidate over the plots.

    ramps are the rows of ramp_levels.ramp_rows, links maps a plot to its link origin (x, y), schedule maps a plot to its sheet
    row. A ramp whose centre lies in no plot face or in two is counted and gets no road candidate from the front edge.
    """
    polygons = cad_analysis.face_polygons(faces)
    tree = STRtree(polygons)
    road_tree = STRtree(roads)
    differences, rule, curb_differences = {}, [], []
    counts = Counter()
    for ramp in ramps:
        row = schedule.get(ramp["plot"])
        if row is None:
            counts["noScheduleRow"] += 1
            continue
        corners = [(point[0], point[1], level) for point, level in zip(ramp["corners"], ramp["levels"])]
        entry = row["entry"]
        counts["plots"] += 1
        found = {name: point[2] for name, point in entry_levels.ramp_candidates(corners, links.get(ramp["plot"])).items()}
        found["top elevation of the floor"] = ramp["topElevation"]
        face = join_by_centre(centre_of(corners), polygons, tree)
        front = entry_levels.front_edge(polygons[face], road_tree, roads) if face is not None else None
        counts["noFace" if face is None else "face"] += 1
        found.update(road_candidates(lookup, corners, front))
        for name, level in found.items():
            differences.setdefault(name, []).append(entry - level)
        edge = entry_levels.ramp_entry(corners)
        if edge is not None:
            rule.append(entry - edge["level"])
            counts["flatRamps"] += edge["flat"]
        curb = "road at the curb point of the front edge"
        if curb in found:
            curb_differences.append(entry - found[curb])
    offset = entry_levels.entry_offset(curb_differences)
    return {
        "counts": dict(counts),
        "candidates": {name: entry_levels.spread(values) for name, values in sorted(differences.items())},
        "rule": {"entryPoint": "midpoint of the higher long edge of the ramp", **entry_levels.spread(rule)},
        "roadOffsetM": None if offset is None else round(offset, 3),
        "roadRule": entry_levels.spread([value - offset for value in curb_differences]) if offset is not None else None,
        "note": MEASURED,
    }


def schedule_labels(book, shift):
    """The rows of one schedule with the baseplan label of each, the official number minus the shift of R28."""
    rows = []
    for row in book:
        if not str(row["plot"]).isdecimal():
            raise project_profile.ProfileError("sheet plot " + str(row["plot"]) + " is not a number")
        rows.append({**row, "label": str(int(row["plot"]) - shift)})
    return rows


def prove(books, faces, lookup, roads, offset):
    """R95. The entry level of each schedule plot from the road and its front edge, against the sheet.

    books maps a workbook name to (shift, rows). Per workbook it gives the bands, the worst plots, the plots with no
    road level or no face, the surfaces that answered and the plots whose front edge is a tie between two edges.
    """
    polygons = cad_analysis.face_polygons(faces)
    by_label = {}
    for index, face in enumerate(faces):
        by_label.setdefault(str(face["plot"]), []).append(index)
    road_tree = STRtree(roads)
    report = {}
    for name, (shift, book) in books.items():
        errors, worst, no_road, no_face, tied = [], [], [], [], []
        surfaces, entries, curbs = Counter(), {}, []
        for row in schedule_labels(book, shift):
            found = by_label.get(row["label"], [])
            if len(found) != 1:
                no_face.append({"plot": row["plot"], "faces": len(found)})
                continue
            front = entry_levels.front_edge(polygons[found[0]], road_tree, roads)
            answer = entry_levels.road_entry(lookup, front, offset)
            if front is not None and front["tied"]:
                tied.append(row["plot"])
            if answer is None:
                no_road.append({"plot": row["plot"], "unit": row["unit"], "frontFound": front is not None})
                continue
            difference = answer["level"] - row["entry"]
            errors.append(difference)
            entries[row["label"]] = round(answer["level"], 4)
            curbs.append([round(answer["point"][0], 3), round(answer["point"][1], 3)])
            surfaces[answer["surface"]] += 1
            worst.append({"plot": row["plot"], "unit": row["unit"], "difference": round(difference, 4), "surface": answer["surface"], "tied": bool(front["tied"])})
        worst.sort(key=lambda item: -abs(item["difference"]))
        report[name] = {
            "plots": len(book), "withRoadLevel": len(errors), "noRoadLevel": len(no_road), "noFace": len(no_face),
            "bands": entry_levels.band_counts(errors), "afterMedian": entry_levels.spread(errors),
            "surfaces": dict(surfaces), "tiedFront": len(tied), "worst": worst[:LISTED], "noRoadLevelPlots": no_road, "noFacePlots": no_face,
            "entries": entries, "curbPoints": curbs,
        }
    return report


def merge_totals(report):
    """Bands over every workbook together."""
    total = Counter()
    for entry in report.values():
        total.update(entry["bands"])
        total["plots"] += entry["plots"]
        total["noRoadLevel"] += entry["noRoadLevel"]
        total["noFace"] += entry["noFace"]
    return dict(total)


def load_past(settings):
    job = plan_from_past.load_job(settings)
    data = json.loads(Path(job["prototypes"]).read_text(encoding="utf-8"))
    document = ramp_levels.host_document(data)
    offset = ramp_levels.offset_of(document)
    links = {}
    for link in document.get("links") or []:
        plot = plan_from_past.plot_number((link.get("parameters") or {}).get("RRE_ZZ_SP_Plot-Number"))
        origin = plan_from_past.shared_origin(link, offset)
        if plot and origin:
            links.setdefault(plot, []).append((origin[0], origin[1]))
    unique = {plot: found[0] for plot, found in links.items() if len(found) == 1}
    return job, document, unique


def main(argv=None):
    parser = argparse.ArgumentParser(description="Measure El @ Entry on the past job and prove the entry rule on the schedule plots.")
    parser.add_argument("command", choices=("measure", "proof"))
    parser.add_argument("out_dir", help="where the report is written, keep it outside git")
    parser.add_argument("--cad", required=True, help="folder with plots.json of the baseplan")
    parser.add_argument("--roads", required=True, help="folder with pieces.json of the road layout")
    parser.add_argument("--landxml-dir", required=True, help="output folder of landxml_reader.py")
    parser.add_argument("--surfaces", default=str(ramp_levels.DEFAULT_CONFIG), help="road_surfaces.json")
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--root", default=None)
    parser.add_argument("--write-offsets", action="store_true", help="measure only: write the entry section of the offsets file")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        config = ramp_levels.load_config(args.surfaces)
        lookup = road_lookup.load(config, args.landxml_dir)
        faces = cad_analysis.plot_faces(cad_analysis.read_plots(args.cad)["plots"])
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis.roadEdgeLayers"))
        root = args.root or levels_proof.input_root(settings)
        if args.command == "measure":
            job, document, links = load_past(settings)
            rows, _ = ramp_levels.ramp_rows(document, set(config["rampTypes"]))
            sheet = {row["plot"]: row for row in levels_proof.read_schedule(Path(root) / levels_proof.full_run.XLSX_FOLDER / job["schedule"], job["sheet"])}
            result = measure(rows, links, sheet, faces, lookup, roads)
            result["surfaceOrder"] = {"order": lookup.names, "source": lookup.source}
            common.write_json(Path(args.out_dir) / "entry_measure.json", result)
            if args.write_offsets and result["roadOffsetM"] is not None:
                section = {"roadOffsetM": result["roadOffsetM"], "plots": result["roadRule"]["n"], "rule": "road level at the curb point nearest the front edge plus the offset", "rampRule": result["rule"]["entryPoint"],
                           "source": MEASURED, "surfaceOrder": lookup.names}
                prototype_offsets.write_section(args.offsets, "entry", section, prototype_offsets.NOTE)
            print("plots " + str(result["counts"].get("plots")) + ", ramp rule " + json.dumps({key: result["rule"][key] for key in ("n", "median", "within5mm", "within10mm", "within30mm", "maxAbs")}) + ", road offset " + str(result["roadOffsetM"]))
        else:
            shifts = link_table.load_shifts(args.shifts)
            books = {}
            for path in levels_proof.schedule_paths(root, settings):
                books[path.name] = (levels_proof.shift_of(path, shifts), levels_proof.read_schedule(path))
            offset = prototype_offsets.entry_offset(prototype_offsets.load(args.offsets))
            report = prove(books, faces, lookup, roads, offset)
            common.write_json(Path(args.out_dir) / "entry_proof.json", {"roadOffsetM": offset, "surfaceOrder": lookup.names, "workbooks": report, "total": merge_totals(report)})
            for name, entry in report.items():
                print(name + ": plots " + str(entry["plots"]) + ", road level " + str(entry["withRoadLevel"]) + ", none " + str(entry["noRoadLevel"]) + ", bands " + json.dumps(entry["bands"]))
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("entry_proof: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
