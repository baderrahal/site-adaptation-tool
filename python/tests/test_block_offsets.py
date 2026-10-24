import math

import pytest

import block_offsets

IDENTITY = {"axisX": [1, 0, 0], "axisY": [0, 1, 0], "axisZ": [0, 0, 1], "rotationDeg": 0.0, "mirrored": False}
FLIP_X = {"axisX": [-1, 0, 0], "axisY": [0, 1, 0], "axisZ": [0, 0, 1], "rotationDeg": 180.0, "mirrored": True}


def box(x0, y0, x1, y1):
    return [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]


def face(label, x0, y0=0, width=10, depth=25):
    corners = [(x0, y0), (x0 + width, y0), (x0 + width, y0 + depth), (x0, y0 + depth)]
    return {"kind": "plot_shape", "plot": label, "type": "VL4", "vertices": [list(corner) for corner in corners]}


def block(name, outline, insert=(0.0, 0.0), scale=(1, 1, 1), rotation=0.0):
    return {"name": "X$0$" + name, "outline": outline, "insertPoint": [insert[0], insert[1], 0.0], "scale": list(scale), "rotationDeg": rotation}


def link(origin, transform=IDENTITY):
    return {"origin": origin, "transform": transform}


def test_r97_a_point_goes_to_the_axes_of_the_block_and_back_for_a_turned_and_flipped_block():
    flipped = block("Villa_VL4", box(0, 0, 1, 1), insert=(100.0, 50.0), scale=(-1, 1, 1), rotation=90.0)
    matrix = block_offsets.block_matrix(flipped)
    assert [value for row in matrix for value in row] == pytest.approx([0.0, -1.0, -1.0, 0.0], abs=1e-12)
    world = block_offsets.to_world(flipped, matrix, (2.0, 3.0))
    assert world == pytest.approx((97.0, 48.0))
    assert block_offsets.to_local(flipped, block_offsets.invert(matrix), world) == pytest.approx((2.0, 3.0))
    assert block_offsets.block_matrix({"insertPoint": [0, 0], "scale": [0, 1, 1]}) is None
    assert block_offsets.block_matrix({"insertPoint": [0, 0]}) == ((1.0, -0.0), (0.0, 1.0))


def test_r97_the_relation_of_a_link_is_its_full_matrix_in_the_axes_of_the_block_and_not_a_pair_of_flags():
    plain = block("Villa_VL4", box(0, 0, 1, 1))
    back = block_offsets.invert(block_offsets.block_matrix(plain))
    rotation, mirrored, scaled = block_offsets.relation(block_offsets.multiply(back, block_offsets.link_matrix(IDENTITY)))
    assert (rotation, mirrored, scaled) == (0.0, False, False)
    rotation, mirrored, scaled = block_offsets.relation(block_offsets.multiply(back, block_offsets.link_matrix(FLIP_X)))
    assert (rotation, mirrored, scaled) == (180.0, True, False)
    # A block flipped in X holds the link as it is drawn, so the same world link is the other relation in the axes of the block
    flipped = block("Villa_VL4", box(0, 0, 1, 1), scale=(-1, 1, 1))
    back = block_offsets.invert(block_offsets.block_matrix(flipped))
    assert block_offsets.relation(block_offsets.multiply(back, block_offsets.link_matrix(IDENTITY)))[:2] == (180.0, True)
    assert block_offsets.relation(block_offsets.multiply(back, block_offsets.link_matrix(FLIP_X)))[:2] == (0.0, False)
    # Two flags can match while the transforms differ, a flipped link turned 180 is not the plain link
    assert block_offsets.relation_key(*block_offsets.relation(block_offsets.link_matrix(FLIP_X))[:2]) != block_offsets.relation_key(0.0, True)


def test_r97_a_link_axis_that_is_scaled_is_said_to_be_scaled_and_a_link_without_axes_has_no_matrix():
    stretched = {"axisX": [2, 0, 0], "axisY": [0, 1, 0]}
    assert block_offsets.relation(block_offsets.link_matrix(stretched))[2] is True
    assert block_offsets.link_matrix({"axisX": [1, 0, 0]}) is None and block_offsets.link_matrix({}) is None


def test_r97_the_position_of_a_unit_in_its_block_is_counted_along_the_x_axis_of_the_block_not_the_world():
    faces = [face("1", 0), face("2", 10)]
    plain = [block("Duplex_DP2", box(1, 1, 19, 24))]
    flipped = [block("Duplex_DP2", box(1, 1, 19, 24), insert=(20.0, 0.0), scale=(-1, 1, 1))]
    for villas, wanted in ((plain, {0: 1, 1: 2}), (flipped, {0: 2, 1: 1})):
        chosen, stands_for = block_offsets.deciding_blocks(faces, villas)
        found = block_offsets.positions(faces, villas, stands_for)
        assert {index: rank for (number, index), rank in found.items()} == wanted


def test_r97_a_block_of_three_plots_or_with_no_scale_has_no_positions():
    faces = [face("1", 0), face("2", 10), face("3", 20)]
    villas = [block("Townhouse_TH1", box(1, 1, 29, 24))]
    _, stands_for = block_offsets.deciding_blocks(faces, villas)
    assert block_offsets.positions(faces, villas, stands_for) == {}


def test_r97_the_offset_and_the_relation_are_measured_for_each_plot_with_a_block_and_a_link_and_the_rest_is_counted():
    faces = [face("1", 0), face("2", 10), face("3", 40)]
    villas = [block("Duplex_DP2", box(1, 1, 19, 24))]
    links = {"11": link((2.0, 3.0)), "12": link((18.0, 3.0), FLIP_X), "13": link((45.0, 5.0)), "99": link((0.0, 0.0)), "11x": link((0, 0))}
    links.pop("11x")
    rows, left_out = block_offsets.measure_plots(faces, villas, links, {"11": (4.0, 10.0)}, 10)
    assert left_out == {"noFace": 1, "noBlockOrTwo": 1}
    first, second = rows
    assert (first["plot"], first["block"], first["position"], first["size"]) == ("11", "Duplex_DP2", 1, 2)
    assert first["offset"] == pytest.approx((2.0, 3.0)) and (first["rotation"], first["mirrored"]) == (0.0, False) and first["entrance"] == pytest.approx((4.0, 10.0))
    assert (second["position"], second["rotation"], second["mirrored"]) == (2, 180.0, True) and second["entrance"] is None
    assert second["offset"] == pytest.approx((18.0, 3.0)) and first["blockMirrored"] is False


def test_r97_a_link_without_axes_a_plot_with_two_blocks_and_a_block_of_another_size_are_left_out_and_counted():
    faces = [face("1", 0), face("2", 40), face("3", 80), face("4", 120), face("5", 130), face("6", 140)]
    villas = [block("Villa_VL4", box(1, 1, 9, 24)), block("Villa_VL4", box(41, 1, 49, 9)), block("Villa_VL4", box(41, 11, 49, 24)), block("Townhouse_TH1", box(121, 1, 149, 24))]
    links = {"11": {"origin": (2.0, 3.0), "transform": {"axisX": [1, 0, 0]}}, "12": link((42.0, 3.0)), "14": link((122.0, 3.0)), "15": link((132.0, 3.0))}
    rows, left_out = block_offsets.measure_plots(faces, villas, links, {}, 10)
    assert rows == [] and left_out == {"linkWithoutAxes": 1, "noBlockOrTwo": 1, "blockSizeOrScale": 2}


def single_plot_group(count, noise=None, name="Villa_VL4"):
    """count plots each with its own block and a link two metres in from the insert point, noise moves one link in x by metres."""
    faces, villas, links = [], [], {}
    for index in range(1, count + 1):
        x = 40.0 * index
        faces.append(face(str(index), x))
        villas.append(block(name, box(x + 1, 1, x + 9, 24), insert=(x, 0.0)))
        moved = (noise or {}).get(index, 0.0)
        links[str(index + 10)] = link((x + 2.0 + moved, 3.0))
    return faces, villas, links


def test_r98_each_group_is_split_in_half_with_a_fixed_seed_fitted_on_the_first_half_and_tested_on_the_second():
    faces, villas, links = single_plot_group(8, noise={2: 0.0004, 5: 0.005})
    rows, left_out = block_offsets.measure_plots(faces, villas, links, {}, 10)
    assert left_out == {} and len(rows) == 8
    report = block_offsets.evaluate(rows, villas, seed=13)
    assert len(report) == 1
    group = report[0]
    assert (group["block"], group["position"], group["plots"], group["tooFew"]) == ("Villa_VL4", 1, 8, False)
    assert group["fit"]["plots"] == 4 and group["test"]["plots"] == 4 and group["fit"]["offset"] == pytest.approx((2.0, 3.0), abs=1e-3)
    fit_errors, test_errors = group["fit"]["offsetError"], group["test"]["offsetError"]
    assert fit_errors["n"] == 4 and test_errors["n"] == 4
    assert test_errors["within1mm"] + test_errors["worse"] <= 4 and test_errors["within10mm"] >= test_errors["within1mm"]
    assert group["test"]["relationRight"] == 4
    again = block_offsets.evaluate(rows, villas, seed=13)
    assert again == report
    assert len(block_offsets.evaluate(rows, villas, seed=14)) == 1


def test_r98_the_split_is_deterministic_for_a_seed_and_the_fit_half_is_the_larger_when_the_count_is_odd():
    rows = [{"plot": str(number)} for number in range(1, 8)]
    fit, test = block_offsets.split_group(rows, 13)
    assert len(fit) == 4 and len(test) == 3 and {row["plot"] for row in fit} | {row["plot"] for row in test} == {row["plot"] for row in rows}
    assert block_offsets.split_group(rows, 13) == (fit, test)


def test_r98_a_noisy_plot_lands_in_the_right_band_in_the_test_half():
    faces, villas, links = single_plot_group(4, noise={1: 0.005, 2: 0.005, 3: 0.0, 4: 0.0})
    rows, _ = block_offsets.measure_plots(faces, villas, links, {}, 10)
    group = block_offsets.evaluate(rows, villas, seed=13)[0]
    total = group["fit"]["offsetError"]["n"] + group["test"]["offsetError"]["n"]
    assert total == 4
    errors = group["fit"]["offsetError"], group["test"]["offsetError"]
    assert sum(part["within1mm"] for part in errors) + sum(part["within10mm"] - part["within1mm"] for part in errors) + sum(part["worse"] for part in errors) == 4


def test_r98_a_group_with_fewer_than_four_plots_is_reported_as_too_few_and_not_tested():
    faces, villas, links = single_plot_group(3)
    rows, _ = block_offsets.measure_plots(faces, villas, links, {}, 10)
    group = block_offsets.evaluate(rows, villas, seed=13)[0]
    assert group["tooFew"] is True and "test" not in group and "fit" not in group
    assert group["all"]["offset"] == pytest.approx((2.0, 3.0), abs=1e-3)
    assert block_offsets.summarise([group]) == {"groups": 1, "tooFew": 1, "plots": 3}


def test_r98_the_relation_is_the_most_common_pair_a_tie_goes_to_the_smaller_and_the_test_counts_it_right_only_when_both_match():
    rows = [{"rotation": 180.0, "mirrored": False, "offset": (1.0, 1.0), "entrance": None}] * 3 + [{"rotation": 0.0, "mirrored": True, "offset": (9.0, 1.0), "entrance": None}]
    fit = block_offsets.fit_group(rows)
    assert (fit["rotation"], fit["mirrored"], fit["share"], fit["relations"]) == (180.0, False, 3, 2) and fit["offset"] == pytest.approx((1.0, 1.0))
    tie = block_offsets.fit_group(rows[:1] + rows[3:])
    assert (tie["rotation"], tie["mirrored"]) == (0.0, True)
    assert block_offsets.angle_gap(359.99, 0.01) == pytest.approx(0.02) and block_offsets.angle_gap(180.0, 0.0) == 180.0


def test_r98_a_group_with_two_relations_lists_each_as_a_variant_with_its_own_offset():
    faces, villas, links = single_plot_group(6)
    for number in ("11", "12"):
        origin = links[number]["origin"]
        links[number] = link((origin[0] + 8.0, origin[1]), FLIP_X)
    rows, _ = block_offsets.measure_plots(faces, villas, links, {}, 10)
    variants = block_offsets.evaluate(rows, villas, seed=13)[0]["variants"]
    assert [(v["rotationDeg"], v["mirrored"], v["plots"]) for v in variants] == [(0.0, False, 4), (180.0, True, 2)]
    assert variants[0]["offset"] == pytest.approx([2.0, 3.0]) and variants[1]["offset"] == pytest.approx([10.0, 3.0])


def test_r99_the_entrance_point_is_measured_in_the_axes_of_the_block_fitted_and_tested_like_the_offset():
    faces, villas, links = single_plot_group(8)
    entries = {str(index + 10): (40.0 * index + 5.0, 12.0 + (0.2 if index == 3 else 0.0)) for index in range(1, 9)}
    rows, _ = block_offsets.measure_plots(faces, villas, links, entries, 10)
    assert rows[0]["entrance"] == pytest.approx((5.0, 12.0))
    group = block_offsets.evaluate(rows, villas, seed=13)[0]
    entrance = group["entrance"]
    assert entrance["fitPlots"] + entrance["testPlots"] == 8
    assert set(entrance["testError"]) == {"within10mm", "within100mm", "worse", "n"} and entrance["testError"]["n"] == entrance["testPlots"]
    assert group["all"]["entrance"] == pytest.approx((5.0, 12.0)) and group["all"]["entrances"] == 8


def test_r99_a_plot_with_no_ramp_has_no_entrance_and_a_group_with_none_has_no_entrance_test():
    faces, villas, links = single_plot_group(6)
    rows, _ = block_offsets.measure_plots(faces, villas, links, {}, 10)
    group = block_offsets.evaluate(rows, villas, seed=13)[0]
    assert "entrance" not in group and group["all"]["entrance"] is None


def test_r98_the_summary_adds_up_the_bands_over_the_groups():
    faces, villas, links = single_plot_group(8)
    entries = {str(index + 10): (40.0 * index + 5.0, 12.0) for index in range(1, 9)}
    rows, _ = block_offsets.measure_plots(faces, villas, links, entries, 10)
    summary = block_offsets.summarise(block_offsets.evaluate(rows, villas, seed=13))
    assert summary["groups"] == 1 and summary["testPlots"] == 4 and summary["offsetwithin1mm"] == 4 and summary["relationRight"] == 4
    assert summary["entranceTestPlots"] == 4 and summary["entrancewithin10mm"] == 4


def test_r100_the_groups_for_the_config_hold_the_fit_over_all_plots_and_the_errors_of_the_split():
    faces, villas, links = single_plot_group(8)
    entries = {str(index + 10): (40.0 * index + 5.0, 12.0) for index in range(1, 9)}
    rows, _ = block_offsets.measure_plots(faces, villas, links, entries, 10)
    groups = block_offsets.groups_for_config(block_offsets.evaluate(rows, villas, seed=13), 13)
    group = groups[0]
    assert (group["block"], group["position"], group["plots"], group["splitSeed"]) == ("Villa_VL4", 1, 8, 13)
    assert group["offset"] == pytest.approx([2.0, 3.0]) and group["relative"]["rotationDeg"] == 0.0 and group["relative"]["mirrored"] is False
    assert group["entrance"] == pytest.approx([5.0, 12.0]) and set(group["testError"]) == {"within1mm", "within10mm", "worse", "n"} and "entranceTestError" in group
    assert math.isclose(group["fitError"]["n"] + group["testError"]["n"], 8)


def test_r97_read_links_takes_the_shared_origin_of_the_villa_link_of_each_plot_and_leaves_out_a_plot_held_twice():
    document = {"links": [
        {"parameters": {"RRE_ZZ_SP_Plot-Number": "0101"}, "transform": {"origin": [1.0, 2.0, 3.0], **IDENTITY}},
        {"parameters": {"RRE_ZZ_SP_Plot-Number": "102"}, "transform": {"origin": [1.0, 2.0, 3.0], **IDENTITY}},
        {"parameters": {"RRE_ZZ_SP_Plot-Number": "102"}, "transform": {"origin": [1.0, 2.0, 3.0], **IDENTITY}},
        {"parameters": {}, "transform": {"origin": [0, 0, 0]}},
    ]}
    found = block_offsets.read_links(document, [1000.0, 5000.0, 100.0])
    assert list(found) == ["101"] and found["101"]["origin"] == [1001.0, 5002.0, 103.0]


@pytest.mark.parametrize("scale", [[1], "x", ["a", 1], [True, 1], [0, 1, 1]])
def test_r97_a_scale_that_is_not_two_numbers_or_is_zero_gives_no_matrix_and_no_crash(scale):
    assert block_offsets.block_matrix({"insertPoint": [0, 0], "scale": scale}) is None


def test_r97_four_plots_in_two_columns_get_a_position_that_does_not_depend_on_the_face_order():
    faces = [face("1", 0, 0), face("2", 10, 0), face("3", 0, 25), face("4", 10, 25)]
    villas = [block("Townhouse_TH1", box(0, 0, 20, 50))]
    first = block_offsets.positions(faces, villas, [[0, 1, 2, 3]])
    reversed_faces = [faces[3], faces[2], faces[1], faces[0]]
    second = block_offsets.positions(reversed_faces, villas, [[0, 1, 2, 3]])
    by_label = lambda found, source: {source[index]["plot"]: rank for (_, index), rank in found.items()}
    assert by_label(first, faces) == by_label(second, reversed_faces) == {"1": 1, "3": 2, "2": 3, "4": 4}
