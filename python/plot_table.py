"""R352. The plot table of button 2: per baseplan face inside the Phase 1 border the columns of plots.csv (R166) the add-in takes from the
reads of button 2 and the location plan of R351, as data_book.build wires them: the face, its label, type and flags, the official number
of the neighbourhood schedule that holds its label plus the shift (R28), the location plan labels whose site point it holds (pointInside),
the footprint blocks that hold at least dataBook.wiring.minShare of their outline in it (everyShare), largest overlap first, and the area
key of R256 by the location plan numbers of the face. The labels of a face are joined in the order of their numbers, so the table does not hang on the order
the location plan lists them in. The Rev 9 parcel numbers of R244, a Civil 3D read of the data book and no input of the add-in, are not
taken: a face with no location plan number has no area key here. The blocks of a face go largest overlap first by block_order, the blocks
within the areas tolerance of addin.parity of the largest left one group in the order of the read, such as the parking bays of a
commercial face, where data_book.build takes the order of the binary noise of shapely, which no other reader can give. A block outline
that is not a simple ring is refused, as face_rings refuses a face (R345), since buffer(0) mends it and the C# would not. The front frame
and the road level at the front corners come with the front edge of R355. The C# of the worker gives the same table (PlotTable of Common).
"""
import csv
from collections import Counter, defaultdict

from shapely.geometry import Polygon

import area_key
import face_rings
import wiring
import yard_rule

COLUMNS = ["baseplanLabel", "clientType", "centroid", "faceAreaM2", "crossesBorder", "duplicateLabel", "inManyLabelFace", "officialNumber",
           "neighbourhood", "twoNeighbourhoods", "locationPlanNumber", "locationPlanCode", "locationPlanNewCode", "typologyRev9", "footprintBlock",
           "footprintRotationDeg", "footprintMirrored", "footprintShared", "zone", "nbhId", "areaKeyMethod", "areaKeyCheck"]


def joined(values):
    """The values of the labels of a face joined by a bar, an empty value kept between two others and dropped at an end, None for none,
    as data_book.build joins the new codes and Typology_Rev9."""
    return "|".join(value or "" for value in values).strip("|") or None


def block_order(overlaps, tolerance):
    """R352. The blocks of a face in the order of the table: the block of the largest overlap left with every block whose overlap lies
    within tolerance of it, that group in the order of the read, then the next group. overlaps maps the place of each block in the read to
    its overlap with the face in m2. Taken from the largest left, so a chain of small steps never runs on into one group, and with no
    rounding to steps, so two overlaps of one group never part at the edge of a step by binary noise."""
    left, order = dict(overlaps), []
    while left:
        top = max(left.values())
        group = sorted(item for item, overlap in left.items() if overlap >= top - tolerance)
        order += group
        for item in group:
            del left[item]
    return order


def simple_blocks(blocks):
    """R352. The blocks of the footprint read, a block whose outline of three points or more is not a simple ring refused with its name."""
    for block in blocks:
        ring = face_rings.clean([point[:2] for point in block.get("outline") or []])
        if len(ring) >= 3 and not face_rings.simple(ring):
            raise ValueError("the footprint block %s at %s is not a simple ring, two of its edges meet, shapely would mend it by buffer(0) and the C# "
                             "would not, so the plot table refuses it (R352)" % (block.get("name"), block.get("insertPoint")))
    return blocks


def table(plots, border, schedules, shifts, labels, blocks, min_share, dlus, sales, area_tolerance):
    """The rows of the plot table by baseplan label, of every face whose centroid lies inside the border. plots is the plots list of the
    baseplan read, border its points, schedules the rows of data_book.schedule_rows, shifts the shift of each neighbourhood, labels the
    rows of the location plan with their site points as labels.csv writes them, blocks the blocks of the footprint read, dlus and sales the
    area key tables of area_key.by_plot, area_tolerance the overlap two blocks of a face may differ by and keep the order of the read."""
    border = Polygon(border)
    faces, _ = yard_rule.face_shapes(plots, 0)
    count = Counter(str(yard_rule.whole_label(e.get("plot"))) for e in plots if e.get("kind") == "plot_shape" and yard_rule.whole_label(e.get("plot")) is not None)
    many = {str(label) for e in plots if e.get("reason") == "many_labels" for label in e.get("labels") or []}
    types = {}
    for entry in plots:
        if entry.get("kind") == "plot_shape" and yard_rule.whole_label(entry.get("plot")) is not None:
            types[str(yard_rule.whole_label(entry.get("plot")))] = entry.get("type")
    all_faces = wiring.Faces(faces)
    plan_by_face = defaultdict(list)
    items = [label for label in labels if label["east"] is not None and label["north"] is not None]
    for item, hits in zip(items, wiring.point_inside(all_faces, [(item["east"], item["north"]) for item in items])):
        for hit in hits:
            plan_by_face[hit].append(item)
    shapes = wiring.unit_shapes(simple_blocks(blocks), None)
    per_block = wiring.every_share(all_faces, [shape["shape"] for shape in shapes], min_share)
    found = defaultdict(list)
    for item, hits in enumerate(per_block):
        for hit in hits:
            found[hit].append(item)
    for hit, held in found.items():
        found[hit] = block_order({item: faces[hit].intersection(shapes[item]["shape"]).area for item in held}, area_tolerance)
    rows = []
    for label, face in sorted(faces.items(), key=lambda item: int(item[0])):
        centre = face.centroid
        if not border.contains(centre):
            continue
        homes = [name for name, shift in shifts.items() if (name, str(int(label) + shift)) in schedules]
        home = homes[0] if len(homes) == 1 else None
        on_plan = sorted(plan_by_face.get(label) or [], key=lambda item: int(item["number"]))
        on = found.get(label) or []
        numbers = [item["number"] for item in on_plan]
        key, why = area_key.key_of(numbers, dlus)
        rows.append({
            "baseplanLabel": label, "clientType": types.get(label), "centroid": [centre.x, centre.y], "faceAreaM2": face.area,
            "crossesBorder": not border.contains(face), "duplicateLabel": count[label] > 1, "inManyLabelFace": label in many,
            "officialNumber": str(int(label) + shifts[home]) if home else None, "neighbourhood": home, "twoNeighbourhoods": len(homes) > 1,
            "locationPlanNumber": "|".join(numbers) or None, "locationPlanCode": "|".join(item["code"] for item in on_plan) or None,
            "locationPlanNewCode": joined(item["newCode"] for item in on_plan), "typologyRev9": joined(item["rev9"] for item in on_plan),
            "footprintBlock": "|".join(shapes[item]["name"] for item in on) or None,
            "footprintRotationDeg": shapes[on[0]]["rotation"] if on else None, "footprintMirrored": shapes[on[0]]["mirrored"] if on else None,
            "footprintShared": len(per_block[on[0]]) > 1 if on else None,
            "zone": key[0] if key else None, "nbhId": key[1] if key else None,
            "areaKeyMethod": "locationPlanNumber" if key else why,
            "areaKeyCheck": area_key.check(numbers, key, sales) if key else None})
    return rows


def border_points(path):
    """The points of the Phase 1 border of the data book, phase1_border.csv of R163, in its order."""
    with open(path, encoding="utf-8-sig") as handle:
        return [(float(row["x"]), float(row["y"])) for row in csv.DictReader(handle)]

