"""Scan of the block inserts of a drawing against the plot faces of the baseplan (R73).

Nothing is matched to a plot here and no rule reads the result. The scan counts what the blocks show,
so the rules that read them can be chosen on facts. Counts only, no coordinates.
"""
import argparse
import re
import sys
from collections import Counter
from pathlib import Path

from shapely.geometry import Point
from shapely.strtree import STRtree

import cad_analysis
import common
import project_profile

# The CAD reader names a block of a bound xref <xref>$<n>$<block>
XREF_PREFIX = re.compile(r"^.*\$\d+\$")
SPLIT = re.compile(r"[_\s]+")
MORE = "more"
# Buckets of the number of plots that hold at least COVER_SHARE of an insert outline
MOST_COUNTED = 4
POINT_CLASSES = ("inBestPlot", "inOtherPlot", "inNoPlot", "noPlotHoldsPart", "noOutline", "noInsertPoint")


def base_name(name):
    return XREF_PREFIX.sub("", name or "")


def classify(name, units, old):
    """(kind, code, ends_with_code) of a block name.

    kind is unit when a part of the name is a unit code of the profile typologies, else old when it is an old client
    code, else none. ends_with_code is true when the name ends in an underscore and that code, which is how R69 reads a name.
    """
    base = base_name(name)
    parts = [part for part in SPLIT.split(base) if part]
    last = base.rsplit("_", 1)[-1] if "_" in base else None
    for kind, codes in (("unit", units), ("old", old)):
        if last in codes:
            return kind, last, True
        for part in parts:
            if part in codes:
                return kind, part, False
    return "none", None, False


def bucket(count):
    return str(count) if count <= MOST_COUNTED else MORE


def point_class(point, best, polygons, tree):
    """Where an insert point lies: in the face that holds most of the outline, in another face, or in none."""
    if best is None:
        return "noPlotHoldsPart"
    if not point or len(point) < 2:
        return "noInsertPoint"
    spot = Point(point[0], point[1])
    inside = [int(index) for index in tree.query(spot) if polygons[int(index)].covers(spot)]
    if best in inside:
        return "inBestPlot"
    return "inOtherPlot" if inside else "inNoPlot"


def order(counter, keys):
    return {key: counter[key] for key in keys if counter[key]}


def scan(blocks, faces, profile, old_codes):
    """Per name and overall counts of the inserts, see R73."""
    units = set(profile.get("typologies"))
    polygons = cad_analysis.face_polygons(faces)
    tree = STRtree(polygons)
    label_types = Counter(face["type"] for face in faces if isinstance(face.get("type"), str))
    names = {}
    for block in blocks:
        name = block.get("name") or ""
        entry = names.setdefault(name, {"inserts": 0, "mirrored": 0, "notMirrored": 0, "noFlag": 0, "covering": Counter(), "insertPoint": Counter()})
        entry["inserts"] += 1
        flag = cad_analysis.mirrored_of(block)
        entry["mirrored" if flag is True else "notMirrored" if flag is False else "noFlag"] += 1
        outline = block.get("outline") or []
        if len(outline) < 3:
            entry["covering"]["noOutline"] += 1
            entry["insertPoint"]["noOutline"] += 1
            continue
        shares = cad_analysis.shares_of(cad_analysis.outline_of(block), polygons, tree)
        entry["covering"][bucket(sum(1 for share, _ in shares if share >= cad_analysis.COVER_SHARE))] += 1
        best = cad_analysis.owner_of(shares, faces)[0] if shares else None
        entry["insertPoint"][point_class(block.get("insertPoint"), best, polygons, tree)] += 1
    rows = []
    for name, entry in names.items():
        kind, code, ends = classify(name, units, old_codes)
        rows.append({
            "name": name, "inserts": entry["inserts"], "kind": kind, "code": code, "endsWithCode": ends,
            # A code that is also a client plot type of the baseplan labels, with the number of plots that carry it
            "labelTypePlots": label_types.get(code, 0) if code else 0,
            "mirrored": entry["mirrored"], "notMirrored": entry["notMirrored"], "noFlag": entry["noFlag"],
            "covering": dict(sorted(entry["covering"].items())), "insertPoint": order(entry["insertPoint"], POINT_CLASSES),
        })
    rows.sort(key=lambda row: (-row["inserts"], row["name"]))
    covering, covering_coded, point, point_coded = Counter(), Counter(), Counter(), Counter()
    for row in rows:
        coded = row["kind"] != "none"
        for key, number in row["covering"].items():
            covering[key] += number
            if coded:
                covering_coded[key] += number
        for key, number in row["insertPoint"].items():
            point[key] += number
            if coded:
                point_coded[key] += number
    keys = [str(number) for number in range(MOST_COUNTED + 1)] + [MORE, "noOutline"]
    summary = {
        "inserts": len(blocks),
        "names": len(rows),
        "namesByKind": {kind: sum(1 for row in rows if row["kind"] == kind) for kind in ("unit", "old", "none")},
        "insertsByKind": {kind: sum(row["inserts"] for row in rows if row["kind"] == kind) for kind in ("unit", "old", "none")},
        "namesEndingInCode": sum(1 for row in rows if row["endsWithCode"]),
        "covering": order(covering, keys),
        "coveringOfCodedNames": order(covering_coded, keys),
        "insertPoint": order(point, POINT_CLASSES),
        "insertPointOfCodedNames": order(point_coded, POINT_CLASSES),
        "mirrored": sum(row["mirrored"] for row in rows),
        "notMirrored": sum(row["notMirrored"] for row in rows),
        "noFlag": sum(row["noFlag"] for row in rows),
    }
    return {"summary": summary, "names": rows}


def old_codes_of(profile, settings):
    """The old client codes. The cluster codes that point at a Rev 6 cluster, the cluster codes with none, and the client plot types that are not Rev 6 unit codes."""
    return set(profile.get("cluster_aliases")) | set(settings.get("analysis.noRev6Match")) | (set(settings.get("analysis.unitPlotTypes")) - set(profile.get("typologies")))


def report_lines(result):
    summary = result["summary"]
    lines = ["inserts " + str(summary["inserts"]) + ", names " + str(summary["names"]) + ", names by kind " + str(summary["namesByKind"]) + ", inserts by kind " + str(summary["insertsByKind"])]
    for row in result["names"]:
        lines.append(str(row["inserts"]).rjust(5) + "  " + row["name"] + "  kind " + row["kind"] + (" " + str(row["code"]) if row["code"] else "") + (", ends in the code" if row["endsWithCode"] else "")
                     + ", mirrored " + str(row["mirrored"]) + " not " + str(row["notMirrored"]) + (", no flag " + str(row["noFlag"]) if row["noFlag"] else ""))
    lines.append("plots holding at least " + str(cad_analysis.COVER_SHARE) + " of the outline, all inserts " + str(summary["covering"]) + ", names with a code " + str(summary["coveringOfCodedNames"]))
    lines.append("insert point, all inserts " + str(summary["insertPoint"]) + ", names with a code " + str(summary["insertPointOfCodedNames"]))
    return lines


def main(argv=None):
    parser = argparse.ArgumentParser(description="Count the block inserts of a drawing against the baseplan plot faces. Nothing is matched.")
    parser.add_argument("out_dir", help="where block_scan.json is written")
    parser.add_argument("--cad", required=True, help="folder with plots.json from the CAD reader, run on the baseplan")
    parser.add_argument("--blocks", required=True, help="folder with blocks.json from the CAD reader, run on the block drawing")
    parser.add_argument("--project", default=str(project_profile.SETTINGS_PATH))
    parser.add_argument("--profile", help="rules profile, default is the one named in the project settings")
    args = parser.parse_args(argv)
    try:
        settings = project_profile.load_settings(args.project)
        profile = project_profile.load_profile(args.profile or project_profile.profile_path(settings))
        faces = cad_analysis.plot_faces(cad_analysis.read_plots(args.cad)["plots"])
        data = cad_analysis.read_blocks(args.blocks)
        result = scan(data["blocks"], faces, profile, old_codes_of(profile, settings))
    except project_profile.ProfileError as error:
        print("block_scan: " + str(error), file=sys.stderr)
        return 2
    common.write_json(Path(args.out_dir) / "block_scan.json", {"source": data.get("source"), **result})
    for line in report_lines(result):
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
