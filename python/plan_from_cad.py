"""Builds plan.json from CAD and the schedule, so the placer can serve plots with no past job (R126, R128).

The link of a plot comes from the frame of its baseplan face (R122) and the plotFrame section of the offsets file: the
rotation from the front edge, the mirror from the drawn side of its typology in its cluster (R123) and the origin from the
fitted front corner offset (R121). The levels come from levels.py with El @ Entry of the schedule (R67), and the link Z is
the FFL. A value no rule gives is left out and its plot is listed in missing with the reason, nothing is guessed. The plan
holds no floors yet. The model it is placed in is the site model of the past job, whose survey point and angle it takes.
"""
import argparse
import sys
from collections import Counter
from pathlib import Path, PurePath

from shapely.strtree import STRtree

import cad_analysis
import cad_links
import common
import excel_reader
import full_run
import levels
import levels_proof
import link_summary
import link_table
import plan_from_past
import plot_relations
import project_profile
import prototype_offsets
import rules_checks
import yard_rule

SCHEDULE = "schedule"
COMPUTED = plan_from_past.COMPUTED
LINK_KEYS = plan_from_past.LINK_KEYS


def sourced(value, source=COMPUTED):
    return {"value": value, "source": source}


def schedule_entries(rows):
    """R126. The unit and El @ Entry of each schedule row by official plot number, and the rows that give neither.

    A plot held by two rows is listed and gets no entry, so no row is picked.
    """
    by_plot, bad = plan_from_past.schedule_by_plot(rows)
    entries, lacking = {}, []
    for plot, found in by_plot.items():
        if len(found) != 1:
            lacking.append({"plot": plot, "why": str(len(found)) + " schedule rows hold the plot"})
            continue
        unit = plot_relations.excel_cell(found[0], "Unit")
        entry = plot_relations.excel_cell(found[0], "El @ Entry")
        unit = str(unit).strip() if unit is not None and str(unit).strip() else None
        if unit is None or not plan_from_past.is_number(entry):
            lacking.append({"plot": plot, "why": "the schedule row has no unit or no El @ Entry number"})
            continue
        entries[plot] = {"unit": unit, "entry": float(entry)}
        sheet_ffl = plot_relations.excel_cell(found[0], "FFL")
        if plan_from_past.is_number(sheet_ffl):
            entries[plot]["sheetFfl"] = float(sheet_ffl)
    return entries, lacking, bad


def official_clusters(clusters, shift):
    """The clusters of analysis.json with their plots as official numbers, the baseplan label plus the shift (R28)."""
    return [{**cluster, "plots": [str(int(label) + shift) for label in cluster["plots"]]} for cluster in clusters]


def one_step_flags(rows, profile):
    """R126. A plot of a typology of steps.one_step_only with more than 1 step gets the flag more than 1 step (Rev 6 Appendix A)."""
    only = set(profile.get("steps.one_step_only"))
    for row in rows:
        if row["unit"] in only and row["steps"] is not None and row["steps"] > 1:
            row["flags"].append("more than 1 step")
    return rows


def plot_levels(profile, clusters, entries):
    """R126. levels.py on the clusters with the schedule entries, by plot, with the one step flag."""
    return {row["plot"]: row for row in one_step_flags(levels.compute(profile, clusters, entries), profile)}


def prototype_files(listing, profile):
    """R128. The prototype file of each unit code: the file in the folder named with the code, else the file of the keo_file
    of its typology in the profile, so VD1 takes the DP1 file. A code with two files gets none and is listed."""
    found, twice = {}, set()
    for name in sorted(listing or []):
        unit = link_summary.file_unit(name)
        if unit is None:
            continue
        if unit in found:
            twice.add(unit)
        found[unit] = name
    files = {unit: name for unit, name in found.items() if unit not in twice}
    for unit, typology in profile.get("typologies").items():
        keo = typology.get("keo_file")
        if unit not in files and unit not in twice and keo in files:
            files[unit] = files[keo]
    return files, sorted(twice)


def link_values(builder, unit, face, frame, role, frame_section, files):
    """R128. Prototype file, insert in plan, rotation and mirror of one plot, each only when its rule gives it."""
    if unit in files:
        builder.entry["prototypeFile"] = sourced(files[unit])
    else:
        builder.lack("prototypeFile", "no single prototype file in the folder carries the unit " + unit)
    if face is None:
        builder.lack("transform", "no baseplan face carries the label of the plot")
        return
    if frame is None:
        builder.lack("transform", "the face has no front edge")
        return
    sides = {code: {"drawnSide": value.get("drawnSide")} for code, value in frame_section["units"].items()}
    mirrored = cad_links.mirror_of(unit, role, sides)
    if mirrored is None:
        builder.lack("mirrored", "no rule gives the mirror flag of " + unit + (" in no cluster" if role is None else " in this cluster"))
        return
    fit = frame_section["units"].get(unit)
    if fit is None:
        builder.lack("insert", "plotFrame holds no anchor and offset for " + unit)
        return
    origin = cad_links.predicted_origin(frame, mirrored, fit)
    builder.entry["insert"] = sourced(plan_from_past.point(origin) + [None])
    builder.entry["rotationDeg"] = sourced(plan_from_past.plan_rotation(cad_links.rotation_of(frame, mirrored)))
    builder.entry["mirrored"] = sourced(mirrored)


def level_values(builder, row):
    if row is None or row["ffl"] is None:
        builder.lack("levels.ffl", "levels.py gives no FFL" + ("" if row is None else ", " + ", ".join(row["flags"])))
        builder.entry["levels"] = {}
        return
    builder.entry["levels"] = {"ffl": sourced(row["ffl"]), "ssl": sourced(row["ssl"]), "entry": sourced(row["entry"], SCHEDULE),
                               "height": sourced(row["height"]), "steps": sourced(row["steps"])}
    if row["flags"]:
        builder.entry["levelFlags"] = list(row["flags"])
    if "insert" in builder.entry:
        builder.entry["insert"]["value"][2] = row["ffl"]


def build_plan(entries, faces, roads, clusters, profile, analysis, frame_section, listing, document, job, folder, floors_config):
    """R128. The plan of R80 from the schedule entries, the baseplan faces, the road edges and the clusters, links and levels only."""
    road_tree = STRtree(roads) if roads else None
    limits = cad_links.front_limits(profile, analysis)
    frames = {plot: cad_links.front_frame(faces.get(plot), road_tree, roads, limits) for plot in entries}
    roles = cad_links.cluster_roles(clusters, 0, faces, frames)
    rows = plot_levels(profile, clusters, entries)
    files, twice = prototype_files(listing, profile)
    plots, missing, lacking_keys, flags = [], [], Counter(), Counter()
    for plot in sorted(entries, key=link_summary.sort_key):
        unit = entries[plot]["unit"]
        builder = plan_from_past.PlotBuilder(plot)
        builder.entry["unit"] = sourced(unit, SCHEDULE)
        link_values(builder, unit, faces.get(plot), frames.get(plot), roles.get(plot), frame_section, files)
        level_values(builder, rows.get(plot))
        steps = builder.entry["levels"].get("steps", {}).get("value")
        builder.entry["parameters"] = {plan_from_past.PLOT_PARAMETER: sourced(plot, SCHEDULE), plan_from_past.UNIT_PARAMETER: sourced(unit, SCHEDULE)}
        if steps is not None:
            builder.entry["parameters"][plan_from_past.STEPS_PARAMETER] = sourced(str(steps))
        builder.entry["floors"] = {}
        flags.update(builder.entry.get("levelFlags", []))
        complete = all(builder.has(key) for key in LINK_KEYS)
        if complete:
            plots.append(builder.entry)
        if builder.missing:
            missing.append({"plot": plot, "written": complete, "missing": builder.missing})
            lacking_keys.update(item["key"] for item in builder.missing)
    ffls = {plot: row["ffl"] for plot, row in rows.items()}
    mine = [cluster for cluster in clusters if set(cluster["plots"]) & set(entries)]
    issues, records = rules_checks.check_one_ffl(mine, ffls, profile)
    sheet_issues, sheet_records = rules_checks.check_one_ffl(mine, {plot: entry.get("sheetFfl") for plot, entry in entries.items()}, profile)
    survey = document["surveyPoint"]
    plan = {
        "format": plan_from_past.FORMAT,
        "formatVersion": plan_from_past.FORMAT_VERSION,
        "neighbourhood": job["neighbourhood"],
        "coordinateSystem": {"name": "shared", "lengthUnit": "m", "angleUnit": "deg"},
        "surveyPoint": {"position": plan_from_past.point(survey["position"]), "sharedPosition": plan_from_past.point(survey["sharedPosition"])},
        "prototypeFolder": str(folder),
        "floorSettings": {"types": floors_config["types"], "levels": floors_config["levels"], "plotParameter": floors_config["plotParameter"]},
        "sources": {"schedule": job["schedule"], "sheet": job["sheet"], "model": PurePath(job["prototypes"]).name},
        "summary": {
            "plots": len(entries), "written": len(plots), "notWritten": len(entries) - len(plots),
            "missingByKey": dict(sorted(lacking_keys.items())), "levelFlags": dict(sorted(flags.items())),
            "frontTied": sum(1 for frame in frames.values() if frame and frame["tied"]),
            "clusterPlots": len(roles), "prototypeCodesWithTwoFiles": twice,
            "check3": {"byStatus": dict(sorted(Counter(record["status"] for record in records).items())), "issues": len(issues)},
            "check3ScheduleFfl": {"byStatus": dict(sorted(Counter(record["status"] for record in sheet_records).items())), "issues": len(sheet_issues),
                                  "splitWalls": dict(sorted(Counter(record["code"] + " after " + str(wall["after"]) for record in sheet_records for wall in record["walls"]).items()))},
        },
        "plots": plots,
        "missing": missing,
        "conflicts": [],
        "check3": {"issues": issues, "clusters": records, "scheduleFfl": {"issues": sheet_issues, "clusters": [record for record in sheet_records if record["status"] != "one FFL"]}},
    }
    angle = document.get("angleToTrueNorthDeg")
    if plan_from_past.is_number(angle):
        plan["angleToTrueNorthDeg"] = common.r3(angle)
    return plan



def main(argv=None):
    parser = argparse.ArgumentParser(description="Build plan.json from CAD and the schedule, links and levels (R126, R128).")
    parser.add_argument("--roads", required=True, help="folder with pieces.json of the road layout drawing")
    parser.add_argument("--analysis", required=True, help="analysis.json of cad_analysis.py with the schedule units")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--baseplan", help="plots.json of the baseplan, default pastJob.baseplanPlots")
    parser.add_argument("--shifts", default=str(cad_analysis.DEFAULT_SHIFTS))
    parser.add_argument("--offsets", default=str(prototype_offsets.DEFAULT_PATH))
    parser.add_argument("--out", help="plan file, default cadLinks.planOutput")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        profile = project_profile.load_profile(project_profile.profile_path(settings))
        job = plan_from_past.load_job(settings)
        floors_config = plan_from_past.load_floors(settings)
        block = cad_links.cad_links_settings(settings)
        out = Path(args.out or block.get("planOutput") or "")
        if not args.out and not block.get("planOutput"):
            raise project_profile.ProfileError(settings.path.name + " cadLinks has no planOutput")
        plot_relations.refuse_inside_repo(out.resolve().parent)
        shift = link_table.load_shifts(args.shifts)["shifts"][job["neighbourhood"]]
        faces, _ = yard_rule.face_shapes(link_summary.load(Path(args.baseplan or project_profile.ROOT / job["baseplanPlots"]))["plots"], shift)
        roads = cad_analysis.read_road_edges(args.roads, settings.get("analysis")["roadEdgeLayers"])
        clusters = official_clusters(levels_proof.read_clusters(args.analysis), shift)
        root = plan_from_past.input_root(settings)
        rows = excel_reader.read_sheet(root / full_run.XLSX_FOLDER / job["schedule"], job["sheet"])[3]
        entries, lacking, bad = schedule_entries(rows)
        folder, _ = plan_from_past.prototype_folders(root, job)
        document = plan_from_past.host_of(link_summary.load(job["prototypes"]))
        plan = build_plan(entries, faces, roads, clusters, profile, settings.get("analysis"), prototype_offsets.plot_frame(prototype_offsets.load(args.offsets)),
                          plan_from_past.folder_listing(folder), document, job, folder, floors_config)
        plan["summary"]["scheduleRowsLacking"] = lacking
        plan["summary"]["scheduleRowsWithBadPlotNumber"] = bad
        plan_from_past.write_plan(out, plan)
        summary = plan["summary"]
        print("plots written %d of %d, missing %s" % (summary["written"], summary["plots"], summary["missingByKey"]))
        print("level flags %s, check 3 %s" % (summary["levelFlags"], summary["check3"]))
        print("check 3 on the schedule FFL %s" % summary["check3ScheduleFfl"])
        print(out)
    except (project_profile.ProfileError, ValueError, KeyError, OSError) as error:
        print("plan_from_cad: " + str(error), file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
