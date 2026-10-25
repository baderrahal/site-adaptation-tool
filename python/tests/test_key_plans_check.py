"""Round ADDIN Phase 2b: the second method of the key plans, the join, the place check, the area rows and the entrance spots (R328).

Every model here is made up in code (R10).
"""
import copy
import math

import pytest

import entry_spots
import key_join
import key_plans_check as kc
import plan_from_past as pf
import test_entry_spots as te
import test_key_join as tj
import test_key_plans as tp

SQUARE = [[0, 0], [10, 0], [10, 10], [0, 10]]
L_SHAPE = [[0, 0], [10, 0], [10, 4], [4, 4], [4, 10], [0, 10]]
KEY = {"entryParameter": tp.ENTRY, "entryHeightM": {"min": 0.0, "max": 1.5}, "wallEdgeM": 0.3, "cutShareMin": 0.9, "flatTopM": 0.0005}


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
    return kc.face_index(rings), rows


def named(link, name):
    """A link of test_key_join with an instance Name, as the Parsons keys write it."""
    link["parameters"]["Name"] = name + " | DP4 | 1 | XX | XX"
    return link


def test_r328_the_sample_is_drawn_from_every_plot_the_read_names_not_from_the_lists_of_the_first_method():
    doc = tj.document([tj.key_link("501", 0)], [tj.floor("DP4-501", 0), tj.floor("DP4-502", 10), tj.ramp("DP4-503", 20)])
    assert kc.population(doc, tj.FLOORS) == ["501", "502", "503"]
    # The plot parameter comes from floors.plotParameter, so a model that writes it as Lot is read by Lot
    lot = {"types": tj.FLOORS["types"], "plotParameter": "Lot"}
    renamed = tj.document([], [dict(tj.floor("DP4-501", 0), parameters={"Lot": "DP4-601"})])
    assert kc.population(renamed, lot) == ["601"] and kc.population(renamed, tj.FLOORS) == []


def test_r328_a_grid_of_points_gives_the_share_of_a_floor_on_each_face_so_a_50_50_floor_is_cut_and_two_plots_on_one_number_are_found():
    index, rows = faces()
    shares = kc.grid_shares([[5, 0], [15, 0], [15, 25], [5, 25]], index)
    assert [(label, round(share, 3)) for label, share in shares] == [("11", 0.5), ("12", 0.5)]
    # 701 lies half on face 12 and half on face 13, 702 and 703 both stand whole on face 11
    links = [tj.key_link(str(plot), 0, element_id=plot) for plot in (701, 702, 703)]
    floors = [tj.floor("DP4-701", 15), tj.floor("DP4-702", 0, y0=0, y1=12), tj.floor("DP4-703", 0, y0=13, y1=25)]
    doc = tj.document(links, floors)
    second = kc.second_join(doc, tj.FLOORS, index, rows, 0.9)
    assert {plot: row["status"] for plot, row in second.items()} == {"701": "cut", "702": "twoPlots", "703": "twoPlots"}
    first = key_join.join_plots(doc, tj.FLOORS, tj.places())
    assert kc.check_join(first, second, ["701", "702", "703"], rows) == {"compared": 3, "match": 3, "mismatch": []}
    wrong = copy.deepcopy(first)
    wrong["plots"]["701"]["shares"][0]["share"] = 0.8
    assert kc.check_join(wrong, second, ["701"], rows)["mismatch"] == [{"plot": "701", "field": "share", "first": 0.8, "second": 0.5}]


def test_r328_the_join_is_found_again_and_a_wrong_record_is_a_mismatch():
    index, rows = faces()
    doc = tj.document([tj.key_link("501", 0), tj.key_link("502", 10), tj.key_link("503", 20), tj.key_link("601", 0, element_id=601)],
                      [tj.floor("DP4-501", 0), tj.floor("DP4-502", 10), tj.floor("DP4-503", 20), tj.floor("DP4-601", 60)])
    first = key_join.join_plots(doc, tj.FLOORS, tj.places())
    second = kc.second_join(doc, tj.FLOORS, index, rows, 0.9)
    assert kc.check_join(first, second, ["501", "502", "503", "601", "999"], rows) == {"compared": 5, "match": 5, "mismatch": []}
    wrong = copy.deepcopy(first)
    wrong["plots"]["502"]["number"] = "503"
    assert kc.check_join(wrong, second, ["502"], rows)["mismatch"] == [{"plot": "502", "field": "number", "first": "503", "second": "502"}]


def place_key():
    links = [named(tj.key_link("501", 0), "501"), named(tj.key_link("502", 10), "502"), named(tj.key_link("503", 20), "503"),
             named(tj.key_link("503", 10, element_id=9999), "503")]
    floors = [tj.floor("DP4-501", 0), tj.floor("DP4-502", 10), tj.floor("DP4-503", 20), tj.ramp("DP4-502", 0), tj.ramp("DP4-501", 10), tj.ramp("DP4-503", 20),
              tj.floor("DP4-502", 18, "Ramp A", element_id=8888, width=4, y0=5, y1=10)]
    return tj.document(links, floors)


def test_r328_the_place_of_each_floor_ramp_and_link_is_found_again_and_a_verdict_the_first_method_did_not_give_is_a_mismatch():
    index, rows = faces()
    doc = place_key()
    first = key_join.place_check(doc, tj.FLOORS, tj.places())
    places = kc.link_places(doc, tj.FLOORS, index, rows)
    result = kc.check_place(doc, tj.FLOORS, first, index, rows, places, 0.9, ["501", "502", "503"])
    # The swapped ramps are otherNumber, the ramp across faces 12 and 13 is cut and the second link of 503 stands on 502 by both methods
    assert result["mismatch"] == [] and result["compared"] == 11
    clean = dict(first, mismatches=[])
    assert len(kc.check_place(doc, tj.FLOORS, clean, index, rows, places, 0.9, ["501", "502"])["mismatch"]) == 3


def key_model(swap=False):
    """Three links of one file on faces 11 to 13 with their generic floors. With swap the plot numbers of the first two links are swapped,
    their Names stay right, so the first method takes each to the plot of the other."""
    links = [tp.key_link("501", 0), tp.key_link("502", 10), tp.key_link("503", 20)]
    if swap:
        links[0]["parameters"][pf.PLOT_PARAMETER], links[1]["parameters"][pf.PLOT_PARAMETER] = "502", "501"
    floors = [tp.generic("DP4-501", 0, top=7.0, entry=6.8123), tp.generic("DP4-502", 10, top=7.2, entry=150.0), tp.generic("DP4-503", 20, top=7.1, entry=6.9)]
    walls = [tp.wall(1, (10.1, 2), (10.1, 20)), tp.wall(2, (3, 5), (6, 5), "Garden")]
    return tp.model(links, floors, walls)


def first_plan(doc):
    data, rows, _ = pf.names_dialect(copy.deepcopy(doc), tp.FLOORS, tp.FACES, tp.NUMBERS, entry_parameter=tp.ENTRY, share_min=0.9,
                                     entry_heights=KEY["entryHeightM"])
    plan = pf.build_plan(data, rows, tp.JOB, {tp.FILE.lower()}, "C:\\Prototypes", tp.FLOORS, {tp.NUMBERS[label]: shape for label, shape in tp.FACES.items()})
    plan["walls"], _ = pf.key_walls(data, tp.FLOORS, KEY["wallEdgeM"])
    return plan


def plan_faces():
    rings = {label: [[list(xy) for xy in list(shape.exterior.coords)[:-1]]] for label, shape in tp.FACES.items()}
    return kc.face_index(rings), {label: {"locationPlanNumber": number} for label, number in tp.NUMBERS.items()}


def plan_check(plan, doc, plots):
    index, rows = plan_faces()
    host = pf.host_of(doc)
    second = kc.second_join(host, tp.FLOORS, index, rows, KEY["cutShareMin"])
    return kc.check_plan(plan, host, tp.FLOORS, KEY, second, kc.link_places(host, tp.FLOORS, index, rows), plots)


def test_r328_the_plan_values_are_read_again_from_the_link_that_stands_on_the_face_and_the_generic_floor():
    doc = key_model()
    plan = first_plan(doc)
    assert plan_check(plan, doc, ["501", "502", "503"]) == {"compared": 3, "match": 3, "mismatch": []}
    plan["plots"][0]["mirrored"]["value"] = True
    assert plan_check(plan, doc, ["501"])["mismatch"] == [{"plot": "501", "field": "mirrored", "first": True, "second": False}]


def test_r328_a_link_is_tied_to_its_plot_by_the_face_its_house_stands_on_so_two_swapped_plot_numbers_are_found():
    doc = key_model(swap=True)
    found = plan_check(first_plan(doc), doc, ["501", "502"])
    assert {(item["plot"], item["field"]) for item in found["mismatch"]} == {("501", "insert"), ("502", "insert")}


def test_r328_the_walls_the_height_the_steps_and_the_model_plot_are_worked_out_again_and_a_wrong_record_is_a_mismatch():
    doc = key_model()
    plan = first_plan(doc)
    entry = {item["plot"]: item for item in plan["plots"]}
    # The value 150.0 of 502 is off the band, the first method refused it, so its plan holds no El @ Entry and no height
    assert "entry" not in entry["502"]["levels"] and entry["503"]["levels"]["height"]["value"] == 0.2
    entry["501"]["levels"]["height"]["value"] = 0.3
    entry["501"]["levels"]["steps"]["value"] = 3
    entry["503"]["modelPlot"]["value"] = "501"
    plan["walls"][0]["plots"] = ["501"]
    found = {(item["plot"], item["field"]) for item in plan_check(plan, doc, ["501", "502", "503"])["mismatch"]}
    assert found == {("501", "height"), ("501", "steps"), ("503", "modelPlot"), ("502", "walls")}
    kept = first_plan(doc)
    kept["plots"][1]["levels"]["entry"] = {"value": 150.0, "source": pf.KEY_PARAMETER}
    assert {item["field"] for item in plan_check(kept, doc, ["502"])["mismatch"]} == {"entry"}


def test_r328_a_plot_the_second_method_does_not_join_must_not_be_in_the_plan():
    doc = key_model()
    plan = first_plan(doc)
    host = pf.host_of(doc)
    host["floors"][2] = tp.shifted(host["floors"][2], 15)
    found = plan_check(plan, doc, ["503"])["mismatch"]
    assert found == [{"plot": "503", "field": "presence", "first": "in the plan as 503", "second": "cut"}]


def test_r328_source_kind_el_at_entry_and_its_source_and_the_height_source_of_past_all_are_worked_out_again():
    plan = {"plots": [{"plot": "501", "levels": {"ffl": {"value": 7.0}, "entry": {"value": 6.8, "source": pf.KEY_PARAMETER}}},
                      {"plot": "502", "levels": {"ffl": {"value": 7.0}}}, {"plot": "503", "levels": {"ffl": {"value": 7.0}}}],
            "missing": [{"plot": "502", "written": True, "missing": [{"key": "levels.entry", "why": pf.REFUSED + "off the band"}]}]}
    # The schedule of 503 is a pair, its FFL 6.95 stands under the floor top 7.0, so FFL, El @ Entry and the height all come from the schedule
    by_number = {"503": {"pastFfl": "6.95", "pastEntry": "6.9", "pastSource": "schedule IMX"}, "501": {"pastFfl": "7.0", "pastEntry": "6.7", "pastSource": "schedule IMY"}}
    config = {"groups": {"parsons": ["imx"]}, "kinds": {"IMX": "TU"}, "scheduleKeys": {"schedule IMX": "IMX", "schedule IMY": "IMY"}}
    rows = [{"key": "imx", "plot": "501", "source": "parsons", "kind": "TU", "ffl": "7.0", "entry": "6.8", "height": "0.2", "entrySource": "floorParameter",
             "heightSource": "floorTop less floorParameter"},
            {"key": "imx", "plot": "502", "source": "parsons", "kind": "TU", "ffl": "7.0", "entry": "", "height": "", "entrySource": "refused", "heightSource": ""},
            {"key": "imx", "plot": "503", "source": "parsons", "kind": "TU", "ffl": "6.95", "entry": "6.9", "height": "0.05", "entrySource": "schedule",
             "heightSource": "schedule less schedule"}]
    assert kc.check_sources(rows, "IMX", plan, by_number, config, ["501", "502", "503"]) == {"compared": 3, "match": 3, "mismatch": []}
    rows[0] = dict(rows[0], source="native", entrySource="schedule")
    found = kc.check_sources(rows, "IMX", plan, by_number, config, ["501"])["mismatch"]
    assert {item["field"] for item in found} == {"source", "entrySource"}
    # The key floor top with the schedule El @ Entry, the mix the pair rule forbids, is a mismatch of the FFL, the height and its source
    rows[2] = dict(rows[2], ffl="7.0", height="0.1", heightSource="floorTop less schedule")
    found = kc.check_sources(rows, "IMX", plan, by_number, config, ["503"])["mismatch"]
    assert {item["field"] for item in found} == {"ffl", "height", "heightSource"}


def test_r328_the_counts_of_the_join_the_area_rows_the_plan_and_past_all_must_agree():
    join = {"join": {"plots": {"501": {"status": "joined", "number": "501"}, "502": {"status": "twoPlots", "number": "502"}}},
            "areaRow": {"areas": [{"plots": 1}]}}
    plan = {"plots": [{"plot": "501"}]}
    assert kc.check_counts(join, plan, [{"key": "imx", "plot": "501"}], "imx") == {"joined": 1, "areaRows": 1, "planPlots": 1, "pastAllRows": 1, "agree": True,
                                                                                   "onlyJoined": [], "onlyPlan": [], "onlyPastAll": []}
    found = kc.check_counts(join, {"plots": [{"plot": "501"}, {"plot": "502"}]}, [{"key": "imx", "plot": "501"}], "imx")
    assert found["agree"] is False and found["onlyPlan"] == ["502"]


def test_r328_the_entrance_spots_are_found_again_from_the_spots_of_the_plot_and_the_settings_of_its_key():
    floors = [te.ramp("501", 0, 1), te.generic("501", 0, 6.1), te.ramp("502", 10, 2), te.generic("502", 10, 6.1)]
    spots = [te.spot(11, 0, 5, 6.1, host=1), te.spot(12, 10, 5, 6.1004, host=1), te.spot(21, 10.2, 5, 6.1, host=2)]
    doc = te.document(floors, spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK)
    settings = dict(te.BLOCK, radiiM=[0.5, 6.0])
    assert kc.check_spots(doc, te.FLOORS, first, settings, ["501", "502"]) == {"compared": 2, "match": 2, "mismatch": []}
    first["plots"]["501"]["near"]["6.0"] = [11, 12, 21]
    assert kc.check_spots(doc, te.FLOORS, first, settings, ["501"])["mismatch"] == [{"plot": "501", "field": "near 6.0", "first": [11, 12, 21], "second": [11, 12]}]


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


def test_r328_the_counts_of_the_entrance_spots_are_counted_again_on_every_plot_with_the_schedule_by_the_official_number_of_the_second_join():
    floors = [te.ramp("501", 0, 1), te.generic("501", 0, 6.1), te.ramp("502", 20, 2), te.generic("502", 20, 6.2)]
    spots = [te.spot(11, 0, 5, 6.1, host=1), te.spot(12, 10, 5, 6.1004, host=1), te.spot(21, 20, 5, 6.1, host=2), te.spot(22, 30, 5, 6.1, host=2),
             te.spot(23, 8, 5, 6.2, host=2)]
    doc = te.document(floors, spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK, schedule={"1502": 6.1}, numbers={"502": "1502"})
    second = {"501": {"status": "joined", "number": "1501"}, "502": {"status": "joined", "number": "1502"}}
    found = kc.recount_spots(doc, te.FLOORS, first, te.BLOCK, {"1502": 6.1}, second)
    assert found["match"] is True and found["counts"]["key"]["radius 6.0"] == {"within1mm": 1, "within5mm": 1}
    assert found["counts"]["schedule"]["cornerPair"] == {"within1mm": 1, "within5mm": 1}
    first["counts"]["key"]["radius 6.0"]["within5mm"] = 2
    assert kc.recount_spots(doc, te.FLOORS, first, te.BLOCK, {"1502": 6.1}, second)["match"] is False


def test_r328_a_key_value_off_the_height_band_is_left_out_of_the_recount_as_the_first_method_leaves_it_out():
    floors = [te.ramp("501", 0, 1), dict(te.generic("501", 0, 6.1), topElevationRange={"min": 6.25, "max": 6.25}), te.ramp("502", 20, 2),
              dict(te.generic("502", 20, 150.0), topElevationRange={"min": 6.25, "max": 6.25})]
    spots = [te.spot(11, 0, 5, 6.1, host=1), te.spot(21, 20, 5, 150.0, host=2)]
    doc = te.document(floors, spots)
    first = entry_spots.key_entry(doc, te.FLOORS, te.BLOCK)
    found = kc.recount_spots(doc, te.FLOORS, first, te.BLOCK, {}, {})
    assert found["match"] is True and found["counts"]["key"]["radius 6.0"] == {"within1mm": 1, "within5mm": 1}
    assert kc.band_value(floors[3], te.BLOCK) is None and kc.band_value(floors[1], te.BLOCK) == 6.1


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


def test_r328_a_face_plots_csv_gives_two_numbers_joined_by_a_bar_has_no_one_number():
    index = kc.face_index({"14": [[[30, 0], [40, 0], [40, 25], [30, 25]]]})
    assert kc.where((35, 12), index, {"14": {"locationPlanNumber": "481|482"}}) == {"status": "noNumber", "face": "14", "number": None}
    assert kc.where((35, 12), index, {"14": {"locationPlanNumber": "481"}})["status"] == "joined"


def test_r328_the_flat_top_is_held_within_the_flat_top_m_the_key_ran_with_and_a_sloped_floor_has_no_ffl_and_no_el_at_entry():
    doc = key_model()
    host = pf.host_of(doc)
    host["floors"][0]["topElevationRange"] = {"min": 7.0, "max": 7.005}
    key = dict(KEY, flatTopM=0.008)
    data, rows, _ = pf.names_dialect(copy.deepcopy(doc), tp.FLOORS, tp.FACES, tp.NUMBERS, entry_parameter=tp.ENTRY, share_min=0.9,
                                     entry_heights=key["entryHeightM"], flat_top=key["flatTopM"])
    plan = pf.build_plan(data, rows, tp.JOB, {tp.FILE.lower()}, "C:\\Prototypes", tp.FLOORS, {tp.NUMBERS[label]: shape for label, shape in tp.FACES.items()})
    plan["walls"], _ = pf.key_walls(data, tp.FLOORS, key["wallEdgeM"])
    index, rows_csv = plan_faces()
    second = kc.second_join(host, tp.FLOORS, index, rows_csv, key["cutShareMin"])
    assert kc.check_plan(plan, host, tp.FLOORS, key, second, kc.link_places(host, tp.FLOORS, index, rows_csv), ["501"])["mismatch"] == []
    sloped = dict(host["floors"][0], topElevationRange={"min": 7.0, "max": 7.05})
    assert kc.band_value(sloped, dict(te.BLOCK, flatTopM=0.008)) is None
    assert kc.band_value(host["floors"][0], dict(te.BLOCK, entryParameter=tp.ENTRY, flatTopM=0.008)) == 6.8123


def test_r328_a_grid_share_under_the_minimum_share_is_left_out_so_a_touch_is_on_no_face_by_both_methods():
    index, rows = faces()
    # The floor lies 0.25 m of its 10 m width on face 15, a share of 0.025, the grid finds one column of 50 on it
    ring = [[49.75, 0], [59.75, 0], [59.75, 25], [49.75, 25]]
    assert kc.floor_status(ring, index, rows, 0.9, 0.05) == {"status": "noFace", "face": None, "number": None, "shares": [], "grid": kc.GRID}
    assert kc.floor_status(ring, index, rows, 0.9, 0.001)["status"] == "cut"
    where = tj.places()
    where["minShare"] = 0.05
    doc = tj.document([tj.key_link("801", 50, element_id=801)], [tj.floor("DP4-801", 49.75)])
    assert key_join.join_plots(doc, tj.FLOORS, where)["plots"]["801"]["status"] == "noFace"
    assert kc.second_join(doc, tj.FLOORS, index, rows, 0.9, 0.05)["801"]["status"] == "noFace"


def turned(points, degrees):
    turn = math.radians(degrees)
    return [[x * math.cos(turn) - y * math.sin(turn), x * math.sin(turn) + y * math.cos(turn)] for x, y in points]


@pytest.mark.parametrize("degrees", [0.0, 30.1])
@pytest.mark.parametrize("share, status", [(0.89, "cut"), (0.894, "cut"), (0.906, "joined"), (0.91, "joined")])
def test_r328_a_floor_whose_share_lies_near_cut_share_min_is_measured_again_on_a_grid_that_resolves_0_005(share, status, degrees):
    # Faces 11 and 12 side by side, the floor holds share of its area on 11 and the rest on 12, all turned as the plots of a key are
    index = kc.face_index({"11": [turned([[0, 0], [10, 0], [10, 25], [0, 25]], degrees)], "12": [turned([[10, 0], [20, 0], [20, 25], [10, 25]], degrees)]})
    rows = {"11": {"locationPlanNumber": "501"}, "12": {"locationPlanNumber": "502"}}
    x0 = 10 * (1 - share)
    found = kc.floor_status(turned([[x0, 0], [x0 + 10, 0], [x0 + 10, 25], [x0, 25]], degrees), index, rows, 0.9)
    assert found["status"] == status and found["grid"] == kc.FINE_GRID
    top = found["share"] if "share" in found else found["shares"][0][1]
    assert abs(top - share) <= kc.RESOLVE


def test_r328_a_floor_far_from_cut_share_min_stays_on_the_coarse_grid_unless_the_first_method_puts_it_near():
    index = kc.face_index({"11": [[[0, 0], [10, 0], [10, 25], [0, 25]]], "12": [[[10, 0], [20, 0], [20, 25], [10, 25]]]})
    rows = {"11": {"locationPlanNumber": "501"}, "12": {"locationPlanNumber": "502"}}
    whole = [[0, 0], [10, 0], [10, 25], [0, 25]]
    assert kc.floor_status(whole, index, rows, 0.9)["grid"] == kc.GRID
    assert kc.floor_status(whole, index, rows, 0.9, first_share=0.88)["grid"] == kc.FINE_GRID
    assert kc.FINE_GRID * kc.RESOLVE >= 1
