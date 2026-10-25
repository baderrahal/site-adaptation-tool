"""R326. Every plot of each answer key joined to the baseplan face it stands on, and through that face to the official number, the zone
and the neighbourhood, the way Round 17 joined the past keys (R206, R267 to R269), and the place check of each link, generic floor and
ramp against the face its plot value names.

The join: the plots of a key are the plot numbers its links name. A plot joins the baseplan face that holds a point inside its one
generic floor (R206), the face gives the location plan number (labels.csv, one label on the face), the zone and neighbourhood of the
location plan by that number, as past_all.csv takes them, and the area key of plots.csv by the face label (R256), as the area codes
take them (R301). A plot whose floor stands on no face or two, on a face of no label or two, or that has no or two generic floors is
listed by why. The place check: each element stands on the face that holds a point inside it, a generic floor or a ramp, or the house
centre of its file in its own axes for a link (R264), and matches when the location plan number of that face is its own plot value.
The area rows: the joined plots of a key by the area key, for the parsons table of config/area_codes.json (R301).
Written to key_join.json outside the repo, counts, plot numbers, face labels and element ids only, no coordinate or level.
"""
import argparse
import csv
import sys
from collections import Counter, defaultdict
from pathlib import Path, PureWindowsPath

from shapely.geometry import Point
from shapely.strtree import STRtree

import key_cover
import key_reads
import link_summary
import plan_from_past
import plot_relations
import project_profile
import ramp_levels
import yard_rule
from common import write_json

OUTPUT = "key_join.json"
JOINED = "joined"
KINDS = ("link", "generic", "ramp")


def places(faces, numbers, many, area, plan):
    """R326. What a point on the baseplan can be joined to: the faces by label with a tree, the location plan number of a face with one
    label, the label count of a face with more, the area key of plots.csv by label and the location plan zone and NBH by number."""
    labels = sorted(faces, key=link_summary.sort_key)
    return {"labels": labels, "tree": STRtree([faces[label] for label in labels]), "numbers": numbers, "many": many, "area": area, "plan": plan}


def locate(point, where):
    """R326. The face that holds the point and its number, or why there is none: noFace, twoFaces, noNumber (a face of no plan label)
    or manyNumbers (a face of two or more)."""
    hits = [where["labels"][index] for index in where["tree"].query(point, predicate="within")]
    if not hits:
        return {"status": "noFace", "face": None, "number": None}
    if len(hits) > 1:
        return {"status": "twoFaces", "face": None, "number": None, "faces": sorted(hits, key=link_summary.sort_key)}
    face = hits[0]
    number = where["numbers"].get(face)
    if number is None:
        return {"status": "manyNumbers" if face in where["many"] else "noNumber", "face": face, "number": None}
    return {"status": JOINED, "face": face, "number": number}


def floor_point(floor, offset):
    """A point inside the floor in shared plan coordinates, as R206 takes it, or None for a floor with no shape."""
    shape, _ = plot_relations.floor_shape(floor, offset)
    return None if shape is None else Point(shape.representative_point())


def generic_floors(document, floors_config):
    """The generic floors of the read by the plot number their Plot value or plotNumber names (R49)."""
    found = defaultdict(list)
    for floor in document.get("floors") or []:
        plot = plan_from_past.plot_number(link_summary.floor_plot(floor))
        if plot is not None and plan_from_past.kind_of(floor, floors_config) == "generic":
            found[plot].append(floor)
    return found


def key_plots(document):
    """The plot numbers the links of the read name, in order."""
    found = {plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER)) for link in document.get("links") or []}
    return sorted((plot for plot in found if plot is not None), key=link_summary.sort_key)


def join_plots(document, floors_config, where):
    """R326. Each plot the links name joined by its one generic floor to a face, its number, the location plan zone and NBH and the
    area key, with sameNumber when the number is the plot of the key. The counts, the plots by status and those of another number."""
    offset = ramp_levels.offset_of(document)
    floors = generic_floors(document, floors_config)
    plots, by_status = {}, defaultdict(list)
    for plot in key_plots(document):
        own = floors.get(plot, [])
        if len(own) != 1:
            row = {"status": "noGenericFloor" if not own else "twoGenericFloors", "face": None, "number": None}
        else:
            point = floor_point(own[0], offset)
            row = locate(point, where) if point is not None else {"status": "noShape", "face": None, "number": None}
        row.pop("faces", None)
        if row["status"] == JOINED:
            plan, area = where["plan"].get(row["number"]) or {}, where["area"].get(row["face"]) or {}
            row.update(sameNumber=row["number"] == plot, nbh=plan.get("nbh"), zone=plan.get("zone"), areaNbh=area.get("nbhId"), areaZone=area.get("zone"))
        plots[plot] = row
        by_status[row["status"]].append(plot)
    joined = [plot for plot, row in plots.items() if row["status"] == JOINED]
    other = [{"plot": plot, "number": plots[plot]["number"]} for plot in joined if not plots[plot]["sameNumber"]]
    # The location plan writes the zone as its number, the area key as Zone and the number (R256)
    differs = [dict({key: plots[plot][key] for key in ("nbh", "zone", "areaNbh", "areaZone")}, plot=plot) for plot in joined
               if (plots[plot]["nbh"], "Zone " + str(plots[plot]["zone"])) != (plots[plot]["areaNbh"], plots[plot]["areaZone"])]
    floor_only = sorted(set(floors) - set(plots), key=link_summary.sort_key)
    return {"counts": {"keyPlots": len(plots), "joined": len(joined), "sameNumber": len(joined) - len(other), "otherNumber": len(other),
                       "areaAgrees": len(joined) - len(differs)},
            "byStatus": {status: items for status, items in sorted(by_status.items()) if status != JOINED}, "otherNumber": other,
            "areaDiffers": differs, "genericFloorsOfNoKeyPlot": len(floor_only), "plots": plots}


def link_places(document, floors_config):
    """R264, R326. The house centre of each link in shared plan coordinates, from the place of the generic floor centre in the axes of
    its file, the median over the links of that file whose plot value names one generic floor."""
    offset = ramp_levels.offset_of(document)
    shapes = {}
    for plot, own in generic_floors(document, floors_config).items():
        shape = plot_relations.floor_shape(own[0], offset)[0] if len(own) == 1 else None
        if shape is not None:
            shapes[plot] = shape
    links = document.get("links") or []
    learned = plan_from_past.house_offsets(links, shapes, shapes, offset, {plot: plot for plot in shapes})
    return {id(link): plan_from_past.house_centre(link, learned, offset) for link in links}


def place_check(document, floors_config, where):
    """R326. Each link, generic floor and ramp that names a plot against the face it stands on: match when the location plan number of
    that face is its plot value, else otherNumber with the number found, noFace, twoFaces, noNumber, manyNumbers or noPlace (a floor
    with no shape, a link whose file has no frame). Two neighbouring plots whose values are swapped give otherNumber on both."""
    offset = ramp_levels.offset_of(document)
    by_kind, mismatches = defaultdict(Counter), []
    centres = link_places(document, floors_config)
    elements = [("link", link, plan_from_past.plot_number((link.get("parameters") or {}).get(plan_from_past.PLOT_PARAMETER)), centres[id(link)])
                for link in document.get("links") or []]
    for floor in document.get("floors") or []:
        kind = plan_from_past.kind_of(floor, floors_config)
        if kind in ("generic", "ramp"):
            elements.append((kind, floor, plan_from_past.plot_number(link_summary.floor_plot(floor)), floor_point(floor, offset)))
    for kind, element, plot, point in elements:
        if plot is None:
            continue
        found = locate(point, where) if point is not None else {"status": "noPlace", "number": None}
        verdict = "match" if found["status"] == JOINED and found["number"] == plot else ("otherNumber" if found["status"] == JOINED else found["status"])
        by_kind[kind][verdict] += 1
        if verdict != "match":
            mismatches.append({"kind": kind, "elementId": element.get("elementId"), "plot": plot, "verdict": verdict, "found": found["number"], "face": found.get("face")})
    return {"byKind": {kind: dict(sorted(by_kind[kind].items())) for kind in KINDS if kind in by_kind}, "mismatches": mismatches}


def area_row(code, file, joined):
    """R301, R326. The row of a key for the parsons table of config/area_codes.json: its joined plots counted by the area key of
    plots.csv (R256), the largest area first, each with the file, its kind model and where it was measured."""
    counts = Counter((row["areaNbh"], row["areaZone"]) for row in joined["plots"].values() if row["status"] == JOINED and row.get("areaNbh"))
    total = joined["counts"]["keyPlots"]
    areas = [{"nbh": nbh, "zone": zone, "plots": count, "kind": "model", "file": file,
              "measured": "Round ADDIN 2b, key_join.py R326, steps/investigations/round-addin.md: %d of the %d plots the links of the key copy name "
                          "join a baseplan face of this neighbourhood by the area key (R256)" % (count, total)}
             for (nbh, zone), count in sorted(counts.items(), key=lambda item: (-item[1], item[0]))]
    return {"code": code, "areas": areas}


def read_plots_csv(path):
    """R326. The area key of plots.csv by baseplan label (R256), and El @ Entry of the schedule by official number where the row holds a
    schedule level, the IMC and IMD schedules of the data book."""
    area, schedule = {}, {}
    with open(path, encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            if row.get("baseplanLabel") and row.get("nbhId"):
                area[row["baseplanLabel"]] = {"nbhId": row["nbhId"], "zone": row.get("zone")}
            if row.get("officialNumber") and row.get("pastEntry") and row.get("pastSource"):
                schedule[row["officialNumber"]] = float(row["pastEntry"])
    return area, schedule


def key_source(settings, code):
    """The source of the key under inputRoot as answerKeys names it, with forward slashes as config/area_codes.json writes files."""
    keys = [key for key in settings.get("answerKeys.keys") if key.get("code") == code]
    if len(keys) != 1:
        raise project_profile.ProfileError("answerKeys has no one key " + code)
    return "/".join(PureWindowsPath(keys[0]["source"]).parts)


def render(code, entry):
    join, place = entry["join"], entry["place"]
    return "%s (%s): join %s, not joined %s, place %s, areas %s" % (
        code, entry["job"], join["counts"], {status: len(items) for status, items in join["byStatus"].items()}, place["byKind"],
        [(area["nbh"], area["zone"], area["plots"]) for area in entry["areaRow"]["areas"]])


def main(argv=None):
    parser = argparse.ArgumentParser(description="R326. Join every plot of each answer key to its baseplan face, number, zone and NBH, and check "
                                                 "where each link, generic floor and ramp stands.")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--reads", required=True, help="the reads folder of R322")
    parser.add_argument("--key", action="append", type=key_reads.parse_pair, required=True, metavar="CODE=JOB")
    parser.add_argument("--baseplan", required=True, help="plots.json of the baseplan")
    parser.add_argument("--labels", required=True, help="location_plan/labels.csv of the data book")
    parser.add_argument("--plots", required=True, help="plots.csv of the data book, for the area key")
    parser.add_argument("--out", required=True, help="the folder key_join.json is written to, outside the repo")
    args = parser.parse_args(argv)
    key_reads.refuse_repeats(parser, args.key)
    out = Path(args.out)
    plot_relations.refuse_inside_repo(out.resolve())
    settings = project_profile.load_settings(args.settings)
    faces, _ = yard_rule.face_shapes(link_summary.load(args.baseplan)["plots"], 0)
    numbers, many = plan_from_past.location_numbers(args.labels)
    area, _ = read_plots_csv(args.plots)
    where = places(faces, numbers, many, area, key_cover.labels_by_number(args.labels))
    path = out / OUTPUT
    key_reads.earlier_keys(path)
    entries = {}
    for code, job in args.key:
        floors = plan_from_past.load_floors(settings, code)
        document = key_reads.project_document(key_reads.load(Path(args.reads) / job / ("testhost_" + job + "_prototypes.json")))
        plan_from_past.check_key_file(settings, code, document.get("file"), job)
        joined = join_plots(document, floors, where)
        entries[code] = {"job": job, "join": joined, "place": place_check(document, floors, where), "areaRow": area_row(code, key_source(settings, code), joined)}
    report = {"note": "R326. Each plot the links of a key name joined by its generic floor to a baseplan face, the location plan number, zone and "
                      "NBH and the area key of plots.csv, and each link, generic floor and ramp held against the face it stands on. "
                      "A run writes the keys it is given and keeps the other keys of the file.",
              "inputs": {"baseplan": Path(args.baseplan).name, "labels": Path(args.labels).name, "plots": Path(args.plots).name},
              "keys": key_reads.merged(path, entries)}
    out.mkdir(parents=True, exist_ok=True)
    write_json(path, report)
    for code, entry in entries.items():
        print(render(code, entry))
    return 0


if __name__ == "__main__":
    sys.exit(main())
