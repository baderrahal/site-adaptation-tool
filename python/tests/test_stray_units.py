from shapely.geometry import Point, box

import stray_units


def block(x, y, name="Villa_VL4"):
    return {"name": name, "shape": box(x, y, x + 12, y + 25)}


# R194 the unit blocks on no baseplan plot
def test_r194_other_shapes_keep_faces_with_many_or_no_label_and_other_faces():
    plots = [{"kind": "plot_shape", "plot": "5", "vertices": [[0, 0], [1, 0], [1, 1]]},
             {"kind": "plot_shape", "reason": "many_labels", "vertices": [[0, 0], [10, 0], [10, 10]]},
             {"kind": "other_face", "vertices": [[0, 0], [5, 0], [5, 5]]}, {"kind": "polyline", "vertices": [[0, 0], [5, 0], [5, 5]]}]
    assert [kind for kind, _ in stray_units.other_shapes(plots)] == ["many_labels", "other_face"]


def test_r194_a_block_on_no_face_gets_its_distances_labels_and_dlus_zone():
    blocks = [block(0, 0), block(200, 0, "Duplex_DP4")]
    per_block = [("Villa_VL4", ["1"]), ("Duplex_DP4", [])]
    faces = {"1": box(0, 0, 12, 25), "2": box(150, 0, 162, 25)}
    unit_faces = {"1": faces["1"]}
    others = [("other_face", box(190, -10, 230, 40))]
    location = [(Point(206, 12), {"number": "900", "code": "DP4 (BB)"}), (Point(500, 500), {"number": "901", "code": "OS", "nbh": "01A"})]
    rev9 = [(Point(206, 12.5), {"number": "900", "code": "DP4"})]
    rows = stray_units.describe(blocks, per_block, {"Duplex_DP4": 2}, faces, unit_faces, others, location, rev9, 0.5, {"900": ("Zone 4", "01b")})
    assert len(rows) == 1
    row = rows[0]
    assert row["units"] == 2 and row["holder"] == "other_face" and row["nearestFaceM"] == 38.0 and row["nearestUnitFaceM"] == 188.0
    assert row["locationNumbers"] == "900" and row["rev9Codes"] == "DP4" and (row["zone"], row["nbh"]) == ("Zone 4", "01b")
    found = stray_units.summary(rows)
    assert found["withLocationPlot"] == {"blocks": 1, "units": 2} and found["withNeither"] == {"blocks": 0, "units": 0}
    assert found["byZoneNbh"] == {"Zone 4 01b": {"blocks": 1, "units": 2}}


def test_r194_no_label_inside_takes_the_nearest_label_for_the_zone():
    rows = stray_units.describe([block(0, 0)], [("Villa_VL4", [])], {"Villa_VL4": 1}, {"9": box(100, 0, 110, 10)}, {}, [],
                                [(Point(50, 50), {"number": "7", "code": "VL4"})], [], 0.5, {"7": ("Zone 4", "01a")})
    assert rows[0]["locationLabelsInside"] == 0 and rows[0]["zone"] == "Zone 4" and rows[0]["nearestUnitFaceM"] is None
