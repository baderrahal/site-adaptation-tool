"""R211 and R212. The site file a newSite job of the test host sets up a new Revit file with, from CAD and config/project.json.

The CAD drawings are drawn in the site grid, the location plan control points say so: their stated coordinates land
0.27 to 0.31 m from their marks once the page is fitted to the baseplan (R181). So the shared coordinates of the new file
are the CAD coordinates. Its internal origin sits at the centre of the plots it is for, rounded down to
siteBuild.coordinateStepM, so the model stays near its origin, at shared elevation 0. No drawing gives a true north, so the
angle to true north is 0, the grid north of the drawings, and the page fit turn is recorded beside it. The check points are
the control points and the corners of the plots, each in shared metres, and the test host checks Revit puts them where
SiteCoordinates says. The level, floor type and parameter names come from config/project.json and nothing from a past model.
"""
import argparse
import csv
import json
import math
import sys
from pathlib import Path

import common
import plan_from_past
import plot_relations
import project_profile

FORMAT = "site-adaptation-site"


def control_points(words):
    """The stated E and N of each control point text, E = <east> N = <north>, in the order of the words of the page."""
    found = []
    texts = [word["text"] for word in words]
    for at in range(len(texts) - 5):
        if texts[at] == "E" and texts[at + 1] == "=" and texts[at + 3] == "N" and texts[at + 4] == "=":
            try:
                found.append((float(texts[at + 2]), float(texts[at + 5])))
            except ValueError:
                continue
    return found


def page_words(pdf_path):
    import pdfplumber
    with pdfplumber.open(pdf_path) as pdf:
        # The size attribute splits words as location_plan.py does, without it the control texts run into their neighbours
        return pdf.pages[0].extract_words(extra_attrs=["size"])


def fit_turn_deg(matrix):
    """The turn of the page up direction in the site, from the page to site fit of R181. The page y runs down."""
    up_east, up_north = -matrix[1][0], -matrix[1][1]
    return math.degrees(math.atan2(up_east, up_north))


def origin_of(points, step):
    """The centre of the bounding box of the points, each axis rounded down to step."""
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    return [math.floor((min(xs) + max(xs)) / 2.0 / step) * step, math.floor((min(ys) + max(ys)) / 2.0 / step) * step]


def build(settings, plots, controls, turn):
    """R211, R212. The site file of SiteSpec for the plot centroids given, as [[east, north], ...]."""
    block = settings.get("siteBuild")
    floors = plan_from_past.load_floors(settings)
    job = plan_from_past.load_job(settings)
    if not plots:
        raise project_profile.ProfileError("no plot to build the site for")
    east, north = origin_of(plots, block["coordinateStepM"])
    xs, ys = [p[0] for p in plots], [p[1] for p in plots]
    checks = [{"name": "control " + str(k + 1), "shared": [e, n, 0.0]} for k, (e, n) in enumerate(controls)]
    checks += [{"name": "plots " + name, "shared": [x, y, 0.0]} for name, x, y in (("min", min(xs), min(ys)), ("max", max(xs), max(ys)))]
    levels = []
    for kind in ("generic", "yard", "ramp"):
        if floors["levels"][kind] not in levels:
            levels.append(floors["levels"][kind])
    return {
        "format": FORMAT,
        "projectPosition": {"eastM": east, "northM": north, "elevationM": 0.0, "angleDeg": 0.0},
        "pageFitTurnDeg": common.r3(turn, 4) if turn is not None else None,
        "levels": levels,
        "floorTypes": {kind: list(floors["types"][kind]) for kind in ("generic", "yard", "ramp")},
        "floorParameter": floors["plotParameter"],
        "linkParameters": list(job["parameters"]),
        "sharedParameterFile": str(Path(block["folder"]) / "work" / "site_parameters.txt"),
        "parameterGroup": block["parameterGroup"],
        "checkPoints": checks,
    }


def plot_centroids(plots_csv, numbers):
    """The face centroid of each official number of the location plan in plots.csv of the snapshot, in the order given, and the
    numbers plots.csv holds no row for, such as a label that shares its face with another. Those only leave the box of the site."""
    with open(plots_csv, encoding="utf-8") as handle:
        rows = {row["locationPlanNumber"]: row for row in csv.DictReader(handle) if row.get("locationPlanNumber")}
    missing = [number for number in numbers if number not in rows]
    return [[float(rows[number]["centroidX"]), float(rows[number]["centroidY"])] for number in numbers if number in rows], missing


def main(argv=None):
    parser = argparse.ArgumentParser(description="The site file of the newSite job, from CAD and the config (R211, R212).")
    parser.add_argument("cover", help="cover.json of key_cover.py, its plots are the plots of the round")
    parser.add_argument("--settings", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--plots-csv", required=True, help="plots.csv of the snapshot")
    parser.add_argument("--pdf", required=True, help="the location plan PDF of the snapshot")
    parser.add_argument("--fit", required=True, help="location_plan/fit.json of the snapshot")
    parser.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.settings)
        numbers = json.loads(Path(args.cover).read_text(encoding="utf-8"))["plots"]
        controls = control_points(page_words(args.pdf))
        turn = fit_turn_deg(json.loads(Path(args.fit).read_text(encoding="utf-8"))["matrix"])
        centroids, missing = plot_centroids(args.plots_csv, numbers)
        spec = build(settings, centroids, controls, turn)
        spec["plotsWithNoRow"] = missing
        out = Path(args.out)
        plot_relations.refuse_inside_repo(out.resolve().parent)
        if out.exists():
            raise project_profile.ProfileError("the site file is there already, it is never overwritten: " + str(out))
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(spec, indent=1, ensure_ascii=False), encoding="utf-8")
    except (project_profile.ProfileError, KeyError, OSError, ValueError) as error:
        print("site_spec: " + str(error), file=sys.stderr)
        return 2
    print("plots %d, with no plots.csv row %d, control points %d, check points %d, angle %s, page fit turn %s deg" % (
        len(numbers), len(spec["plotsWithNoRow"]), len(controls), len(spec["checkPoints"]), spec["projectPosition"]["angleDeg"], spec["pageFitTurnDeg"]))
    print(out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
