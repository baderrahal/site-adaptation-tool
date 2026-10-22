import argparse
import json
import re
import sys
from pathlib import Path

from shapely.geometry import Point, Polygon
from shapely.strtree import STRtree

import link_table
from common import write_json

DEFAULT_LAYERS = Path(__file__).resolve().parent.parent / "config" / "cad_layers.json"
SLIVER_AREA = 50.0
SMALL_AREA = 1000.0
NEAR = 10.0
OWNER_NEAR = 30.0
NON_PLOT_TYPE = re.compile(r"^\s*(ROW|Buffer)\b", re.IGNORECASE)


def label_of(text):
    """Plot number and type words of a label text, or None when the last line is not a number."""
    lines = [line.strip() for line in re.split(r"[\r\n]|\\P", text) if line.strip()]
    if lines and re.fullmatch(r"\d+", lines[-1]):
        return int(lines[-1]), " ".join(lines[:-1])
    return None


def longest_run(numbers):
    ordered = sorted(set(numbers))
    best = run = 1
    for before, after in zip(ordered, ordered[1:]):
        run = run + 1 if after == before + 1 else 1
        best = max(best, run)
    return best


def face_group(face):
    if face["reason"] == "no_label":
        area = face["area"]
        return "no_label_sliver" if area < SLIVER_AREA else "no_label_small" if area < SMALL_AREA else "no_label_large"
    numbers = [int(label) for label in face.get("labels", [])]
    run = longest_run(numbers)
    if run == len(set(numbers)):
        return "many_labels_consecutive_run"
    return "many_labels_run_plus_outlier" if run >= 2 and run == len(set(numbers)) - 1 else "many_labels_not_consecutive"


def summarize(groups):
    return {name: {"count": len(items)} for name, items in sorted(groups.items())}


def reasons(plots, other, texts, label_layers):
    """The reason for each other face and each label outside every face, as groups with counts, plot numbers and distances."""
    shapes = [shape for shape in plots["plots"] if shape["kind"] == "plot_shape"]
    faces = [("shape", None, Polygon(link_table.ring(shape))) for shape in shapes]
    faces += [(face["reason"], face, Polygon(link_table.ring(face))) for face in other.get("faces", [])]
    polygons = [polygon if polygon.is_valid else polygon.buffer(0) for _, _, polygon in faces]
    tree = STRtree(polygons)

    wanted = {name.lower() for name in label_layers}
    labels = []
    for text in texts:
        if text["layer"].lower() in wanted:
            found = label_of(text["text"])
            if found:
                labels.append((found[0], found[1], Point(text["x"], text["y"])))
    stray = []
    for number, kind, point in labels:
        if len(tree.query(point, predicate="within")):
            continue
        nearest = int(tree.nearest(point))
        distance = polygons[nearest].exterior.distance(point)
        if NON_PLOT_TYPE.match(kind):
            group = "road_or_buffer_label"
        elif distance > NEAR:
            group = "far_from_every_face"
        else:
            group = "near_unlabelled_face" if faces[nearest][0] == "no_label" else "near_labelled_face"
        stray.append({"plot": number, "type": kind, "distance": round(distance, 2), "nearest": faces[nearest][0], "group": group, "point": point})

    face_groups, face_rows = {}, []
    for index, (reason, face, _) in enumerate(faces):
        if face is None:
            continue
        group = face_group(face)
        owners = [item["plot"] for item in stray if polygons[index].distance(item["point"]) <= OWNER_NEAR] if reason == "no_label" else []
        row = {"group": group, "reason": reason, "area": face["area"], "labels": face.get("labels", []), "strayLabelsWithin30m": owners}
        face_rows.append(row)
        face_groups.setdefault(group, []).append(row)

    label_groups = {}
    for item in stray:
        label_groups.setdefault(item["group"], []).append(item)
    for item in stray:
        del item["point"]
    return {
        "faces": {"total": len(face_rows), "groups": summarize(face_groups), "list": face_rows},
        "labels": {"total": len(stray), "groups": summarize(label_groups), "list": stray},
        "labelsRead": len(labels),
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Group the other faces and the labels outside every face of the baseplan by reason.")
    parser.add_argument("out_dir")
    parser.add_argument("--cad", required=True, help="folder of the baseplan with plots.json, other_faces.json and texts.json")
    parser.add_argument("--layers", default=str(DEFAULT_LAYERS))
    args = parser.parse_args(argv)
    cad = Path(args.cad)
    config = json.loads(Path(args.layers).read_text(encoding="utf-8"))
    report = reasons(link_table.load(cad / "plots.json"), link_table.load(cad / "other_faces.json"), link_table.load(cad / "texts.json")["texts"], config["plotLabelLayers"])
    write_json(Path(args.out_dir) / "other_reasons.json", report)
    print("other faces " + str(report["faces"]["total"]) + " " + json.dumps({name: item["count"] for name, item in report["faces"]["groups"].items()}))
    print("labels outside every face " + str(report["labels"]["total"]) + " " + json.dumps({name: item["count"] for name, item in report["labels"]["groups"].items()}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
