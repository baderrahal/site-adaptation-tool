"""Round ADDIN Phase 2b: the second method of the key plans, the join, the place check, the area rows and the entrance spots (R328).

Every model here is made up in code (R10).
"""
import copy

from shapely.geometry import box

import entry_spots
import key_join
import key_plans_check as kc
import test_entry_spots as te
import test_key_join as tj

SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]]
L_SHAPE = [[0, 0], [10, 0], [10, 4], [4, 4], [4, 10], [0, 10]]


def test_r328_ray_casting_and_the_shoelace_centroid_are_plain_math():
    assert kc.inside((5, 5), SQUARE) and not kc.inside((11, 5), SQUARE)
    assert kc.inside((2, 8), L_SHAPE) and not kc.inside((8, 8), L_SHAPE)
    assert kc.centroid(SQUARE) == (5.0, 5.0)
    # The L is a 10 by 4 strip with its centre at (5, 2) and a 4 by 6 strip at (2, 7), weighed by their areas 40 and 24
    x, y = kc.centroid(L_SHAPE)
    assert abs(x - 3.875) < 1e-9 and abs(y - 3.875) < 1e-9


def test_r328_the_sample_takes_20_of_the_sorted_items_by_the_seed_and_all_of_fewer():
    items = [str(number) for number in range(100, 160)]
    first = kc.sample(items, 14)
    assert len(first) == 20 and first == kc.sample(list(reversed(items)), 14) and first != kc.sample(items, 7)
    assert kc.sample(["3", "1"], 7) == ["1", "3"]


def faces():
    """The faces of test_key_join as the vertex lists of plots.json and the rows of plots.csv with their location plan number."""
    rings = {label: [[list(xy) for xy in list(shape.exterior.coords)[:-1]]] for label, shape in tj.FACES.items()}
    rows = {label: {"locationPlanNumber": tj.NUMBERS.get(label, ""), "nbhId": (tj.AREA.get(label) or {}).get("nbhId", ""),
                    "zone": (tj.AREA.get(label) or {}).get("zone", "")} for label in tj.FACES}
    return rings, rows


def joined_key():
    doc = tj.document([tj.key_link("501", 0), tj.key_link("502", 10), tj.key_link("503", 20), tj.key_link("601", 0, element_id=601)],
                      [tj.floor("DP4-501", 0), tj.floor("DP4-502", 10), tj.floor("DP4-503", 20), tj.floor("DP4-601", 60),
                       tj.ramp("DP4-502", 0), tj.ramp("DP4-501", 10), tj.ramp("DP4-503", 20)])
    return doc, {"join": key_join.join_plots(doc, tj.FLOORS, tj.places()), "place": key_join.place_check(doc, tj.FLOORS, tj.places())}


def test_r328_the_join_is_found_again_by_the_vertices_of_the_faces_and_plots_csv_and_a_wrong_record_is_a_mismatch():
    doc, entry = joined_key()
    rings, rows = faces()
    result = kc.check_join(doc, tj.FLOORS, entry["join"], rings, rows, ["501", "502", "503", "601"])
    assert result == {"compared": 4, "match": 4, "mismatch": []}
    wrong = copy.deepcopy(entry["join"])
    wrong["plots"]["502"]["number"] = "503"
    assert kc.check_join(doc, tj.FLOORS, wrong, rings, rows, ["502"])["mismatch"] == [{"plot": "502", "field": "number", "first": "503", "second": "502"}]


def test_r328_the_place_of_each_ramp_and_link_is_found_again_and_a_verdict_the_first_method_did_not_give_is_a_mismatch():
    doc, entry = joined_key()
    rings, rows = faces()
    result = kc.check_place(doc, tj.FLOORS, entry["place"], rings, rows, ["501", "502", "503"])
    # The two swapped ramps are otherNumber by both methods, the links stand right by the frame learned without them
    assert result["compared"] == 6 and result["match"] == 6 and result["mismatch"] == []
    clean = dict(entry["place"], mismatches=[])
    assert len(kc.check_place(doc, tj.FLOORS, clean, rings, rows, ["501", "502"])["mismatch"]) == 2


def test_r328_the_entrance_spots_are_found_again_from_the_two_highest_corners_of_the_ramp():
    floors = [te.ramp("501", 0, 1), te.generic("501", 0, 6.1)]
    spots = [te.spot(11, 0, 5, 6.1, host=1), te.spot(12, 10, 5, 6.1004, host=1), te.spot(13, 0, 0, 6.0, host=1)]
    doc = te.document(floors, spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK)
    assert kc.check_spots(doc, te.FLOORS, first, te.BLOCK, ["501"]) == {"compared": 1, "match": 1, "mismatch": []}
    first["plots"]["501"]["near"]["6.0"] = [11]
    assert kc.check_spots(doc, te.FLOORS, first, te.BLOCK, ["501"])["mismatch"] == [{"plot": "501", "field": "near 6.0", "first": [11], "second": [11, 12]}]


def test_r328_the_plan_values_are_read_again_from_the_transform_and_the_generic_floor():
    import test_key_plans as tp
    import plan_from_past as pf
    links = [tp.key_link("501", 0, steps="2"), tp.key_link("502", 10, rotation=90.0, mirrored=True)]
    floors = [tp.generic("DP4-501", 0, top=7.0, entry=6.8123), tp.generic("DP4-502", 10, top=7.2, entry=6.9)]
    doc = tp.model(links, floors)
    data, rows, _ = pf.names_dialect(copy.deepcopy(doc), tp.FLOORS, tp.FACES, tp.NUMBERS, entry_parameter=tp.ENTRY)
    plan = pf.build_plan(data, rows, tp.JOB, {tp.FILE.lower()}, "C:\\Prototypes", tp.FLOORS, {tp.NUMBERS[label]: shape for label, shape in tp.FACES.items()})
    result = kc.check_plan(plan, pf.host_of(doc), tp.FLOORS, tp.ENTRY, ["501", "502"])
    assert result == {"compared": 2, "match": 2, "mismatch": []}
    plan["plots"][1]["mirrored"]["value"] = False
    assert kc.check_plan(plan, pf.host_of(doc), tp.FLOORS, tp.ENTRY, ["502"])["mismatch"] == [{"plot": "502", "field": "mirrored", "first": False, "second": True}]


def test_r328_the_area_rows_are_counted_again_by_the_official_number_of_each_joined_plot_in_plots_csv():
    join = {"plots": {"501": {"status": "joined", "number": "501"}, "502": {"status": "joined", "number": "502"}, "503": {"status": "joined", "number": "502"},
                      "504": {"status": "noFace", "number": None}}}
    area_row = {"code": "IMX", "areas": [{"nbh": "01A", "zone": "Zone 4", "plots": 3}]}
    numbers = {"501": {"nbhId": "01A", "zone": "Zone 4"}, "502": {"nbhId": "01A", "zone": "Zone 4"}}
    assert kc.check_areas(join, area_row, numbers) == {"plots": 3, "byArea": {"01A Zone 4": 3}, "match": True}
    assert kc.check_areas(join, dict(area_row, areas=[dict(area_row["areas"][0], plots=2)]), numbers)["match"] is False


def test_r328_past_all_holds_one_row_per_plot_of_the_plan_of_each_key():
    rows = [{"key": "imx", "plot": "501"}, {"key": "imx", "plot": "502"}, {"key": "u11", "plot": "11"}]
    assert kc.check_rows(rows, "imx", {"plots": [{"plot": "501"}, {"plot": "502"}]}) == {"rows": 2, "planPlots": 2, "match": True}
    assert kc.check_rows(rows, "imx", {"plots": [{"plot": "501"}]})["match"] is False


def test_r328_the_entrance_is_the_higher_long_edge_also_where_the_ramp_falls_across_more_than_along():
    # The road falls 0.3 m along the front, so the two highest corners are a short edge, the entrance is still the back edge
    floor = te.ramp("501", 0, 1)
    floor["corners"] = [[0, 0, 6.0], [10, 0, 6.3], [10, 5, 6.33], [0, 5, 6.03]]
    spots = [te.spot(11, 0, 5, 6.03, host=1), te.spot(12, 10, 5, 6.33, host=1), te.spot(13, 10, 0, 6.3, host=1)]
    doc = te.document([floor, te.generic("501", 0, 6.18)], spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK)
    assert first["plots"]["501"]["corners"] == [12, 11] or first["plots"]["501"]["corners"] == [11, 12]
    assert kc.check_spots(doc, te.FLOORS, first, te.BLOCK, ["501"]) == {"compared": 1, "match": 1, "mismatch": []}


def test_r328_a_rotation_from_axes_written_to_3_decimals_is_held_within_what_that_rounding_allows():
    import math
    turn = math.radians(30.106)
    axes = [round(math.cos(turn), 3), round(math.sin(turn), 3)]
    assert abs(math.degrees(math.atan2(axes[1], axes[0])) - 30.106) > 0.01
    assert kc.axis_rotation_holds(30.106, axes)
    assert not kc.axis_rotation_holds(30.2, axes)


def test_r328_the_counts_of_the_entrance_spots_against_el_at_entry_are_counted_again_on_every_plot():
    floors = [te.ramp("501", 0, 1), te.generic("501", 0, 6.1), te.ramp("502", 20, 2), te.generic("502", 20, 6.2)]
    spots = [te.spot(11, 0, 5, 6.1, host=1), te.spot(12, 10, 5, 6.1004, host=1), te.spot(21, 20, 5, 6.1, host=2), te.spot(22, 30, 5, 6.1, host=2)]
    doc = te.document(floors, spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK, schedule={"502": 6.1})
    found = kc.recount_spots(doc, te.FLOORS, first, te.BLOCK, {"502": 6.1})
    assert found["match"] is True and found["counts"]["key"]["radius 6.0"] == {"within1mm": 1, "within5mm": 1}
    assert found["counts"]["schedule"]["cornerPair"] == {"within1mm": 1, "within5mm": 1}
    first["counts"]["key"]["radius 6.0"]["within5mm"] = 2
    assert kc.recount_spots(doc, te.FLOORS, first, te.BLOCK, {"502": 6.1})["match"] is False


def test_r328_a_face_plots_csv_gives_two_numbers_joined_by_a_bar_has_no_one_number():
    rings = {"14": [[[30, 0], [40, 0], [40, 25], [30, 25]]]}
    assert kc.where((35, 12), rings, {"14": {"locationPlanNumber": "481|482"}}) == {"status": "noNumber", "face": "14", "number": None}
    assert kc.where((35, 12), rings, {"14": {"locationPlanNumber": "481"}})["status"] == "joined"
