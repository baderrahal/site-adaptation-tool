"""R206 and R207. Which plots a past site model holds, the neighbourhood the round builds, and its one prototype.

The plots of the answer key plan are tied to the location plan labels of the snapshot by their official number, which
plan_from_past.py --names took from the face of each generic floor. The neighbourhood is siteBuild.zoneNbh when the model
holds it. Else it is the neighbourhood with the most plots in the model among those the CAD fits were not made on, the
fits being made on the plots of pastJob.neighbourhood, so the test is out of sample. The prototype is the Typology_Rev9
code with the most plots in that neighbourhood that the model holds, whose file is in the upgraded prototype folder and
whose code the round 15a test placed (testHost.order). Counts only are printed, cover.json goes outside the repo.
"""
import argparse
import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

import common
import link_summary
import plan_from_cad
import plan_from_past
import project_profile


def labels_by_number(path):
    with open(path, encoding="utf-8") as handle:
        return {row["number"]: row for row in csv.DictReader(handle)}


def cover(plan, labels):
    """R206. The plots of the plan by neighbourhood and zone of the location plan, with the Rev 9 codes and the past units."""
    by_nbh, codes, units, unknown = Counter(), defaultdict(Counter), defaultdict(Counter), []
    for entry in plan["plots"]:
        label = labels.get(entry["plot"])
        if label is None or not label.get("nbh"):
            unknown.append(entry["plot"])
            continue
        key = label["nbh"]
        by_nbh[key] += 1
        codes[key][label.get("rev9") or "none"] += 1
        units[key][(entry.get("unit") or {}).get("value") or "none"] += 1
    zones = {}
    for label in labels.values():
        if label.get("nbh"):
            zones[label["nbh"]] = label.get("zone")
    plan_plots = Counter(label["nbh"] for label in labels.values() if label.get("nbh") and label.get("kind") == "clientCode")
    return {"plots": len(plan["plots"]), "byNeighbourhood": {nbh: {"zone": zones.get(nbh), "plots": count, "unitPlotsOnLocationPlan": plan_plots.get(nbh, 0),
                                                                     "typologyRev9": dict(codes[nbh].most_common()), "pastUnits": dict(units[nbh].most_common())}
                                                               for nbh, count in by_nbh.most_common()},
            "withNoNeighbourhood": len(unknown)}


def choose_nbh(found, zone_nbh, fit_nbhs):
    """R206. The neighbourhood of the zone when the model holds it, else the largest the fits were not made on, else the largest."""
    held = found["byNeighbourhood"]
    if zone_nbh in held:
        return zone_nbh, "the model holds " + zone_nbh
    fresh = [nbh for nbh in held if nbh not in fit_nbhs]
    pick = fresh or list(held)
    if not pick:
        raise project_profile.ProfileError("the answer key holds no plot of any neighbourhood")
    best = max(pick, key=lambda nbh: (held[nbh]["plots"], nbh))
    why = "the model holds no plot of " + zone_nbh + ", " + best + " is its largest neighbourhood"
    return best, why + (" the CAD fits were not made on" if fresh else ", every one was a fit neighbourhood")


def choose_code(found, nbh, files, placed):
    """R207. The Rev 9 code with the most plots of the neighbourhood with a file in the folder and placed in round 15a, ties by placed order."""
    counts = found["byNeighbourhood"][nbh]["typologyRev9"]
    usable = [code for code in counts if code in files and code in placed]
    if not usable:
        raise project_profile.ProfileError("no code of " + nbh + " has a prototype file that round 15a placed")
    best = max(usable, key=lambda code: (counts[code], -placed.index(code)))
    return {"code": best, "file": files[best], "plots": counts[best], "usable": {code: counts[code] for code in usable},
            "notUsable": {code: count for code, count in counts.items() if code not in usable}}


def chosen_plots(plan, labels, nbh, code):
    """The official numbers of the plots of the neighbourhood and code that the answer key holds, in order."""
    return sorted((entry["plot"] for entry in plan["plots"] if labels.get(entry["plot"], {}).get("nbh") == nbh
                   and labels[entry["plot"]].get("rev9") == code), key=link_summary.sort_key)


def main(argv=None):
    parser = argparse.ArgumentParser(description="Which plots the answer key holds, the neighbourhood and the prototype of the round (R206, R207).")
    parser.add_argument("plan", help="plan_z4_past.json of plan_from_past.py --answer-key --names")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--labels", required=True, help="location_plan/labels.csv of the snapshot")
    parser.add_argument("--location-plan", required=True, help="locationPlan.json of the snapshot, for the neighbourhood of the schedule plots")
    parser.add_argument("--out", required=True, help="cover.json, outside the repo")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        block = settings.get("siteBuild")
        job = plan_from_past.load_job(settings)
        plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
        labels = labels_by_number(args.labels)
        schedule_nbh = json.loads(Path(args.location_plan).read_text(encoding="utf-8"))["nbhOfSchedulePlots"]
        fit_nbhs = sorted(schedule_nbh.get(job["neighbourhood"], {}))
        found = cover(plan, labels)
        nbh, why = choose_nbh(found, block["zoneNbh"], fit_nbhs)
        root = plan_from_past.input_root(settings)
        folder, _ = plan_from_past.prototype_folders(root, job)
        files, _ = plan_from_cad.prototype_files(plan_from_past.folder_listing(folder), project_profile.load_profile(project_profile.profile_path(settings)))
        choice = choose_code(found, nbh, files, list(settings.get("testHost.order")))
        plots = chosen_plots(plan, labels, nbh, choice["code"])
        result = {"cover": found, "fitNeighbourhoods": fit_nbhs, "neighbourhood": nbh, "why": why, "prototype": choice, "plots": plots}
        plot_out = Path(args.out)
        plot_relations_refuse(plot_out)
        common.write_json(plot_out, result)
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("key_cover: " + str(error), file=sys.stderr)
        return 2
    for name, row in found["byNeighbourhood"].items():
        print("NBH %s zone %s: %d plots of the model, %d unit plots on the location plan" % (name, row["zone"], row["plots"], row["unitPlotsOnLocationPlan"]))
    print("with no neighbourhood %d, fits made on %s" % (found["withNoNeighbourhood"], fit_nbhs))
    print("chosen %s: %s" % (nbh, why))
    print("prototype %s, %s, %d plots, %d in the plan" % (choice["code"], choice["file"], choice["plots"], len(plots)))
    return 0


def plot_relations_refuse(path):
    import plot_relations
    plot_relations.refuse_inside_repo(path.resolve().parent)


if __name__ == "__main__":
    sys.exit(main())
