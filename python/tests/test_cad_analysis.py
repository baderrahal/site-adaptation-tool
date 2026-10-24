import json
import math

import pytest
from shapely.geometry import LineString, Polygon
from shapely.ops import unary_union
from shapely.strtree import STRtree

import block_proof
import cad_analysis
import project_profile

PROFILE_VALUES = {
    "units_mm": True,
    "typologies": {
        "DP2": {"kind": "duplex", "attachment": "one_side"},
        "TH1": {"kind": "townhouse", "attachment": "both_sides"},
        "ZZ1": {"kind": "duplex", "attachment": "one_side"},
        "VL3": {"kind": "villa", "attachment": "detached"},
        "DX9": {"kind": "villa", "attachment": "detached"},
    },
    "clusters": {"D02": ["DP2", "DP2"], "Q01": ["DP2", "TH1", "TH1", "DP2"], "E02": ["DP2", "TH1"]},
    "cluster_aliases": {},
    "cluster_block_max": 4,
    "plot": {"long_side": 25000},
}
ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "endShare": 0.25, "unitPlotTypes": ["DP2", "TH1", "ZZ1", "VL3", "DX9"], "noRev6Match": []}
SHIFT = 10
BOOK = "00101-PSN-IMC-XXX-SCH-TU-090000.xlsx"


def profile_in(tmp_path):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    return project_profile.load_profile(path)


def face(label, typology, x, y, width=10, depth=25, **extra):
    corners = [(x, y), (x + width, y), (x + width, y + depth), (x, y + depth)]
    return {"kind": "plot_shape", "plot": label, "type": typology, "unit": typology, "vertices": [list(corner) for corner in corners], **extra}


def row(types, labels, x0=0, y=0):
    return [face(label, typology, x0 + 10 * index, y) for index, (typology, label) in enumerate(zip(types, labels))]


def excel_of(entries, typologies=None):
    """What read_excel_units gives when every plot of a listed typology has an Excel row that says its label type."""
    listed = PROFILE_VALUES["typologies"] if typologies is None else typologies
    units = {str(entry["plot"]): entry["type"] for entry in entries if entry.get("plot") not in (None, "") and isinstance(entry.get("type"), str) and entry["type"] in listed}
    return {"units": units, "stats": {"rows": len(units), "used": len(units), "ambiguous": 0}, "issues": []}


def behind(count, y):
    """One open space face of the full width of a row, lying against the north side (y=25) or the south side (y=-25) of it."""
    return face("99", "OS", 0, y, width=10 * count)


def test_r64_a_row_of_four_matches_q01_and_is_ordered_left_to_right_facing_the_road(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "TH1", "TH1", "DP2"], ["7", "5", "9", "6"])
    north_back = cad_analysis.derive_clusters(plots + [behind(4, 25)], profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["typologies"], c["orderSure"], c["row"]) for c in north_back] == [("Q01", ["6", "9", "5", "7"], ["DP2", "TH1", "TH1", "DP2"], True, True)]
    south_back = cad_analysis.derive_clusters(plots + [behind(4, -25)], profile, ANALYSIS)
    assert [(c["plots"], c["orderSure"]) for c in south_back] == [(["7", "5", "9", "6"], True)]


def test_r64_the_front_is_away_from_the_end_that_touches_other_plots_and_a_side_neighbour_does_not_count(tmp_path):
    profile = profile_in(tmp_path)
    pair = row(["DP2", "DP2"], ["8", "3"])
    beside = face("50", "VL3", 20, 0)
    assert cad_analysis.derive_clusters(pair + [beside], profile, ANALYSIS)[0]["orderSure"] is False
    both = cad_analysis.derive_clusters(pair + [behind(2, 25), behind(2, -25)], profile, ANALYSIS)[0]
    assert both["orderSure"] is False
    one = cad_analysis.derive_clusters(pair + [beside, behind(2, 25)], profile, ANALYSIS)[0]
    assert one["orderSure"] is True and one["plots"] == ["3", "8"]
    one_south = cad_analysis.derive_clusters(pair + [beside, behind(2, -25)], profile, ANALYSIS)[0]
    assert one_south["plots"] == ["8", "3"]


def test_r64_when_the_front_is_not_known_the_row_keeps_its_chain_and_matches_in_either_direction(tmp_path):
    profile = profile_in(tmp_path)
    q01 = cad_analysis.derive_clusters(row(["DP2", "TH1", "TH1", "DP2"], ["7", "5", "9", "6"]), profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["orderSure"]) for c in q01] == [("Q01", ["6", "9", "5", "7"], False)]
    # TH1 then DP2 from the lower plot number is not listed, DP2 then TH1 is
    e02 = cad_analysis.derive_clusters(row(["TH1", "DP2"], ["1", "2"]), profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["typologies"], c["orderSure"]) for c in e02] == [("E02", ["2", "1"], ["DP2", "TH1"], False)]


def test_r64_plots_that_touch_at_a_corner_or_back_to_back_are_not_side_by_side(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("1", "DP2", 0, 0), face("2", "DP2", 10, -25), face("3", "DP2", 0, 25)]
    pairs = cad_analysis.adjacent_pairs([Polygon([tuple(v) for v in plot["vertices"]]) for plot in plots], 0.01)
    assert sorted(pairs) == [(0, 2)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert sorted(len(cluster["plots"]) for cluster in clusters) == [1, 1, 1]
    assert {cluster["code"] for cluster in clusters} == {"unknown"}


def test_r64_villa_kinds_are_left_out_and_the_kind_comes_from_the_profile_not_the_code_letters(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2", "DX9", "ZZ1", "ZZ1", "VL3"], ["1", "2", "3", "4", "5", "6"])
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert sorted((c["code"], sorted(c["plots"])) for c in clusters) == [("D02", ["1", "2"]), ("unknown", ["4", "5"])]


def test_r64_a_sequence_that_is_not_in_the_profile_is_written_as_unknown_and_a_type_not_in_the_profile_is_not_clustered(tmp_path):
    profile = profile_in(tmp_path)
    clusters = cad_analysis.derive_clusters(row(["ZZ1", "DP2", "ZZ1"], ["1", "2", "3"]), profile, ANALYSIS)
    assert [(c["code"], c["typologies"], c["plots"]) for c in clusters] == [("unknown", ["ZZ1", "DP2", "ZZ1"], ["1", "2", "3"])]
    odd = cad_analysis.derive_clusters(row(["DP2", "DP3", "DP2"], ["1", "2", "3"]), profile, ANALYSIS)
    assert sorted(c["plots"] for c in odd) == [["1"], ["3"]]


def test_r64_a_group_that_is_not_one_row_is_unknown_and_listed_by_plot_number(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("20", "DP2", 0, 0, width=25), face("30", "DP2", -10, 0), face("10", "DP2", 25, 0), face("40", "DP2", 0, 25, width=25)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["orderSure"], c["row"]) for c in clusters] == [("unknown", ["10", "20", "30", "40"], False, False)]
    summary = cad_analysis.analyse(plots, profile, ANALYSIS, excel=excel_of(plots))["summary"]
    assert (summary["notRows"], summary["orderNotSure"], summary["clusters"]) == (1, 1, 1)


def test_r64_a_group_with_two_ends_and_a_plot_touching_three_others_is_not_a_chain():
    links = {0: [1], 1: [0, 2, 3], 2: [1, 3], 3: [1, 2, 4], 4: [3]}
    assert cad_analysis.chain_of([0, 1, 2, 3, 4], links) is None
    assert cad_analysis.chain_of([0, 1], {0: [1], 1: [0]}) == [0, 1]
    assert cad_analysis.chain_of([7], {7: []}) == [7]


def test_r64_the_mirror_flag_comes_from_the_block_scale(tmp_path):
    assert cad_analysis.mirrored_of({"scale": [-1, 1, 1]}) is True
    assert cad_analysis.mirrored_of({"scale": [1, -1, 1]}) is True
    assert cad_analysis.mirrored_of({"scale": [-1, -1, 1]}) is False
    assert cad_analysis.mirrored_of({"scale": [1, 1, 1]}) is False
    assert cad_analysis.mirrored_of({"name": "no scale read"}) is None
    assert cad_analysis.mirrored_of(None) is None
    # The reader's own flag wins, since a scale that rounds to zero loses its sign
    assert cad_analysis.mirrored_of({"scale": [0, 0, 0], "mirrored": True}) is True
    assert cad_analysis.mirrored_of({"scale": [-1, 1, 1], "mirrored": False}) is False
    # An insert inside a flipped insert is flipped once more
    assert cad_analysis.mirrored_of({"mirrored": False, "chainMirrored": True}) is True
    assert cad_analysis.mirrored_of({"mirrored": True, "chainMirrored": True}) is False
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"])
    plots[1]["block"] = {"scale": [-1, 1, 1], "name": "Duplex_DP2", "rotation": 30.0}
    cluster = cad_analysis.derive_clusters(plots, profile, ANALYSIS)[0]
    assert cluster["plots"] == ["1", "2"] and cluster["mirrored"] == [None, True]
    assert cluster["block"] == [None, "Duplex_DP2"] and cluster["rotation"] == [None, 30.0]


def test_r64_faces_without_an_outline_a_label_or_a_numeric_label_do_not_stop_the_analysis(tmp_path):
    profile = profile_in(tmp_path)
    entries = row(["DP2", "DP2"], ["1", "2"]) + [
        {"kind": "plot_shape", "plot": "3", "type": "DP2", "vertices": [[0, 0], [1, 1]]},
        {"kind": "plot_shape", "plot": None, "type": "DP2", "vertices": [[0, 0], [1, 0], [1, 1]]},
        {"kind": "plot_shape", "type": "DP2", "vertices": [[0, 0], [1, 0], [1, 1]]},
        face("²", "OS", 100, 0),
        face("X", ["not", "a", "string"], 200, 0),
    ]
    result = cad_analysis.analyse(entries, profile, ANALYSIS, excel=excel_of(entries))
    assert result["summary"]["plotFaces"] == 4
    assert [(c["code"], c["plots"]) for c in result["clusters"]] == [("D02", ["1", "2"])]
    assert cad_analysis.plot_key("²") == (1, 0, "²") and cad_analysis.plot_key("12") == (0, 12, "12")


def write_inputs(tmp_path, plots, analysis=None, plots_text=None, units=True):
    """The command line for main. With units, a workbook gives every plot of a listed typology its label type as Excel unit."""
    profile = profile_in(tmp_path)
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(plots_text if plots_text is not None else json.dumps({"source": "base.dwg", "plots": plots}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile.path), "analysis": ANALYSIS if analysis is None else analysis}), encoding="utf-8")
    args = ["--cad", str(cad), "--project", str(settings)]
    if units:
        rows = [{"sheetRow": number, "Plot No.": int(entry["plot"]) + SHIFT, "Unit": entry["type"]} for number, entry in enumerate(plots, start=1)
                if str(entry.get("plot")).isdecimal() and entry.get("type") in PROFILE_VALUES["typologies"]]
        (tmp_path / "coords.json").write_text(json.dumps({"source": BOOK, "rows": rows}), encoding="utf-8")
        (tmp_path / "shifts.json").write_text(json.dumps({"neighbourhoods": {"IMC": {"shift": SHIFT}}, "sourceCodePattern": "-(IMC|IMD)-", "searchRange": 1}), encoding="utf-8")
        args += ["--coords", str(tmp_path / "coords.json"), "--shifts", str(tmp_path / "shifts.json")]
    return args


def test_r64_main_writes_the_clusters_the_issues_and_no_coordinates_to_analysis_json(tmp_path, capsys):
    plots = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, 25)] + row(["DP2", "TH1", "DP2"], ["3", "4", "5"], y=100)
    plots.append({"kind": "polyline", "layer": "ROAD", "vertices": [[-50, 200], [150, 200]]})
    args = write_inputs(tmp_path, plots)
    assert cad_analysis.main([str(tmp_path / "out")] + args) == 0
    lines = capsys.readouterr().out.strip().splitlines()
    units = {"plots": 5, "bySource": {"block": 0, "excel": 5, "none": 0}, "byCode": {"DP2": 4, "TH1": 1}, "byCodeFromBlock": {}, "notInProfile": 0, "plotsWithBoth": 0,
             "blockUnitAgrees": 0, "blockUnitDiffers": 0, "excel": {"rows": 5, "used": 5, "ambiguous": 0, "noFace": 0}}
    assert lines == ['clusters 3 {"D02": 1, "E02": 1, "unknown": 1}, issues 2', "units " + json.dumps(units), 'fronts {"neighbour": 1, "none": 1}']
    result = json.loads((tmp_path / "out" / "analysis.json").read_text(encoding="utf-8"))
    assert list(result) == ["source", "summary", "clusters", "plots", "issues", "conflicts"] and result["source"] == "base.dwg" and result["conflicts"] == []
    assert result["summary"] == {"plotFaces": 6, "clusters": 3, "clusterPlots": 5, "byCode": {"D02": 1, "E02": 1, "unknown": 1}, "notRows": 0,
                                 "orderNotSure": 2, "unitsWithMirrorFlag": 0, "rows": 2, "cutFromLongerRow": 2, "frontRules": {"neighbour": 1, "none": 1},
                                 "roadEdges": None, "blocks": None, "units": {**units, "differing": {}}, "issues": 2, "numbering": "baseplan labels"}
    assert [(c["code"], c["plots"], c["orderSure"]) for c in result["clusters"]] == [("D02", ["2", "1"], True), ("E02", ["3", "4"], False), ("unknown", ["5"], False)]
    assert set(result["clusters"][0]) == {"code", "plots", "typologies", "sources", "mirrored", "rotation", "block", "orderSure", "row", "frontRule", "rowId", "rowUnits"}
    assert [c["sources"] for c in result["clusters"]] == [["excel", "excel"], ["excel", "excel"], ["excel"]]
    assert [(c["rowId"], c["rowUnits"]) for c in result["clusters"]] == [("1", 2), ("3", 3), ("3", 3)]
    assert [(i["rule"], i["plots"]) for i in result["issues"]] == [("R65", ["4"]), ("R66", ["5"])]
    assert [(p["plot"], p["cadType"], p["unit"], p["unitSource"]) for p in result["plots"]] == [("1", "DP2", "DP2", "excel"), ("2", "DP2", "DP2", "excel"), ("3", "DP2", "DP2", "excel"), ("4", "TH1", "TH1", "excel"), ("5", "DP2", "DP2", "excel")]
    assert "vertices" not in json.dumps(result)


@pytest.mark.parametrize("what", ["no folder", "bad json", "no list", "top level list", "null list", "missing setting", "no profile"])
def test_r64_main_says_what_is_wrong_in_one_line_instead_of_raising(tmp_path, capsys, what):
    if what == "no folder":
        args = ["--cad", str(tmp_path / "nothing")]
    elif what == "bad json":
        args = write_inputs(tmp_path, [], plots_text="{not json")
    elif what == "no list":
        args = write_inputs(tmp_path, [], plots_text='{"source": "x"}')
    elif what == "top level list":
        args = write_inputs(tmp_path, [], plots_text="[]")
    elif what == "null list":
        args = write_inputs(tmp_path, [], plots_text='{"plots": null}')
    elif what == "missing setting":
        args = write_inputs(tmp_path, [], analysis={"toleranceM": 0.01})
    else:
        args = write_inputs(tmp_path, [])
        (tmp_path / "project.json").write_text('{"profile": null}', encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out")] + args) == 2
    error = capsys.readouterr().err
    assert error.startswith("cad_analysis: ") and "Traceback" not in error and error.count("\n") == 1
    assert not (tmp_path / "out").exists()


def with_flags(plots, flags):
    for plot, flag in zip(plots, flags):
        if flag is not None:
            plot["block"] = {"name": "B", "mirrored": flag, "rotation": 0.0}
    return plots


def cut(clusters):
    return [(c["code"], c["plots"], c["orderSure"], c["rowUnits"]) for c in clusters]


def test_r70_a_long_row_is_cut_into_the_listed_clusters_from_the_start(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "TH1", "TH1", "DP2", "DP2", "DP2"], ["1", "2", "3", "4", "5", "6"]) + [behind(6, -25)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert cut(clusters) == [("Q01", ["1", "2", "3", "4"], True, 6), ("D02", ["5", "6"], True, 6)]
    assert {c["frontRule"] for c in clusters} == {"neighbour"} and {c["rowId"] for c in clusters} == {"1"}
    summary = cad_analysis.analyse(plots, profile, ANALYSIS, excel=excel_of(plots))["summary"]
    assert (summary["clusters"], summary["rows"], summary["cutFromLongerRow"], summary["byCode"]) == (2, 1, 2, {"D02": 1, "Q01": 1})
    assert summary["frontRules"] == {"neighbour": 1}


def test_r70_units_no_listed_cluster_covers_are_one_unknown_piece_and_the_order_is_not_sure_when_the_cut_is_not_the_only_one(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2"] * 5, ["1", "2", "3", "4", "5"]) + [behind(5, -25)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert cut(clusters) == [("D02", ["1", "2"], False, 5), ("D02", ["3", "4"], False, 5), ("unknown", ["5"], False, 5)]
    nothing = cad_analysis.derive_clusters(row(["ZZ1", "ZZ1", "ZZ1"], ["1", "2", "3"]) + [behind(3, -25)], profile, ANALYSIS)
    assert cut(nothing) == [("unknown", ["1", "2", "3"], True, 3)]


def test_r70_known_mirror_flags_decide_between_cuts_and_unknown_flags_cannot(tmp_path):
    profile = profile_in(tmp_path)
    back = [behind(3, -25)]
    flagged = with_flags(row(["DP2"] * 3, ["1", "2", "3"]), [False, True, True]) + back
    assert cut(cad_analysis.derive_clusters(flagged, profile, ANALYSIS)) == [("D02", ["1", "2"], True, 3), ("unknown", ["3"], True, 3)]
    flipped = with_flags(row(["DP2"] * 3, ["1", "2", "3"]), [True, True, False]) + back
    assert cut(cad_analysis.derive_clusters(flipped, profile, ANALYSIS)) == [("unknown", ["1"], True, 3), ("D02", ["2", "3"], True, 3)]
    both_fit = with_flags(row(["DP2"] * 3, ["1", "2", "3"]), [False, True, False]) + back
    assert cut(cad_analysis.derive_clusters(both_fit, profile, ANALYSIS))[0][2] is False
    unflagged = row(["DP2"] * 3, ["1", "2", "3"]) + back
    assert cut(cad_analysis.derive_clusters(unflagged, profile, ANALYSIS))[0][2] is False


def test_r70_a_duplex_pair_is_one_original_and_one_mirror_and_other_clusters_have_no_such_rule(tmp_path):
    profile = profile_in(tmp_path)
    equal = with_flags(row(["DP2", "DP2"], ["1", "2"]), [True, True]) + [behind(2, -25)]
    assert [c["code"] for c in cad_analysis.derive_clusters(equal, profile, ANALYSIS)] == ["unknown"]
    differ = with_flags(row(["DP2", "DP2"], ["1", "2"]), [True, False]) + [behind(2, -25)]
    assert [c["code"] for c in cad_analysis.derive_clusters(differ, profile, ANALYSIS)] == ["D02"]
    one_known = with_flags(row(["DP2", "DP2"], ["1", "2"]), [True, None]) + [behind(2, -25)]
    assert [c["code"] for c in cad_analysis.derive_clusters(one_known, profile, ANALYSIS)] == ["D02"]
    quad = with_flags(row(["DP2", "TH1", "TH1", "DP2"], ["1", "2", "3", "4"]), [True] * 4) + [behind(4, -25)]
    assert [c["code"] for c in cad_analysis.derive_clusters(quad, profile, ANALYSIS)] == ["Q01"]


def test_r70_without_a_known_front_each_piece_may_read_either_way_round(tmp_path):
    profile = profile_in(tmp_path)
    clusters = cad_analysis.derive_clusters(row(["TH1", "DP2", "DP2", "TH1"], ["1", "2", "3", "4"]), profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["typologies"], c["orderSure"]) for c in clusters] == [("E02", ["2", "1"], ["DP2", "TH1"], False), ("E02", ["3", "4"], ["DP2", "TH1"], False)]
    assert {c["frontRule"] for c in clusters} == {"none"}


def test_r70_best_split_covers_the_most_units_then_uses_the_fewest_clusters(tmp_path):
    profile = profile_in(tmp_path)
    known = {"D02": ("DP2", "DP2"), "Q01": ("DP2", "TH1", "TH1", "DP2"), "H": ("DP2", "TH1")}
    pieces, ways = cad_analysis.best_split(["DP2", "TH1", "TH1", "DP2"], [None] * 4, known, profile, reverse_ok=True)
    assert pieces == [(0, 4, "Q01", False)] and ways == 2
    pieces, ways = cad_analysis.best_split(["DP2", "TH1", "TH1", "DP2"], [None] * 4, known, profile, reverse_ok=False)
    assert pieces == [(0, 4, "Q01", False)] and ways == 1
    assert cad_analysis.best_split([], [], known, profile, reverse_ok=False) == ([], 1)
    assert cad_analysis.split_pieces(4, [(1, 3, "D02", False)]) == [(0, 1, "unknown", False), (1, 3, "D02", False), (3, 4, "unknown", False)]


def road(y0):
    return LineString([(-10, y0), (30, y0)])


def test_r71_a_row_the_neighbours_leave_open_gets_its_front_from_the_nearer_road_edge(tmp_path):
    profile = profile_in(tmp_path)
    pair = row(["DP2", "DP2"], ["1", "2"])
    south = cad_analysis.derive_clusters(pair, profile, ANALYSIS, [road(-5)])[0]
    assert (south["plots"], south["orderSure"], south["frontRule"]) == (["2", "1"], True, "road edge")
    north = cad_analysis.derive_clusters(pair, profile, ANALYSIS, [road(40)])[0]
    assert (north["plots"], north["orderSure"], north["frontRule"]) == (["1", "2"], True, "road edge")
    both = cad_analysis.derive_clusters(pair, profile, ANALYSIS, [road(-5), road(40)])[0]
    assert (both["plots"], both["frontRule"]) == (["2", "1"], "road edge")


def test_r71_only_hull_corners_beyond_the_end_distance_count_as_the_ends_of_the_row(tmp_path):
    profile = profile_in(tmp_path)
    # The second plot is shorter, so its north corner lies near the middle of the row and is not an end
    plots = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0, depth=19)]
    analysis = {**ANALYSIS, "endShare": 0.4}
    beside_the_short_plot = LineString([(25, 12), (25, 24)])
    found = cad_analysis.derive_clusters(plots, profile, analysis, [beside_the_short_plot])[0]
    assert (found["plots"], found["frontRule"], found["orderSure"]) == (["2", "1"], "road edge", True)


def test_r71_a_row_with_plots_at_both_ends_takes_the_nearer_road_edge_and_equal_distances_decide_nothing(tmp_path):
    profile = profile_in(tmp_path)
    boxed = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, 25), behind(2, -25)]
    found = cad_analysis.derive_clusters(boxed, profile, ANALYSIS, [road(-30)])[0]
    assert (found["plots"], found["orderSure"], found["frontRule"]) == (["2", "1"], True, "road edge")
    pair = row(["DP2", "DP2"], ["1", "2"])
    level = cad_analysis.derive_clusters(pair, profile, ANALYSIS, [road(-10), road(35)])[0]
    assert (level["plots"], level["orderSure"], level["frontRule"]) == (["1", "2"], False, "none")
    without = cad_analysis.derive_clusters(pair, profile, ANALYSIS)[0]
    assert (without["orderSure"], without["frontRule"]) == (False, "none")


def test_r71_the_neighbour_rule_comes_first_and_a_row_of_one_or_a_tangle_names_its_own_rule(tmp_path):
    profile = profile_in(tmp_path)
    pair = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, 25)]
    near_the_back = cad_analysis.derive_clusters(pair, profile, ANALYSIS, [road(26)])[0]
    assert (near_the_back["plots"], near_the_back["frontRule"]) == (["2", "1"], "neighbour")
    lone = cad_analysis.derive_clusters([face("1", "DP2", 0, 0)], profile, ANALYSIS, [road(-5)])[0]
    assert (lone["frontRule"], lone["orderSure"]) == ("single plot", True)
    tangle = [face("20", "DP2", 0, 0, width=25), face("30", "DP2", -10, 0), face("10", "DP2", 25, 0), face("40", "DP2", 0, 25, width=25)]
    assert cad_analysis.derive_clusters(tangle, profile, ANALYSIS, [road(-5)])[0]["frontRule"] == "not a row"


def test_r71_road_edges_come_from_the_named_layers_also_inside_a_bound_xref(tmp_path):
    pieces = [
        {"kind": "polyline", "layer": "RD-CURB-LINE", "pts": [[0, 0, 0], [10, 0, 0.5], [10, 5, 0]]},
        {"kind": "polyline", "layer": "Ext$1$rd-curb-line", "closed": True, "pts": [[0, 0], [4, 0], [4, 4]]},
        {"kind": "line", "layer": "RD-CURB-LINE", "pts": [[0, 0], [0, 9]]},
        {"kind": "polyline", "layer": "RD-CURB-LINE-X", "pts": [[0, 0], [1, 1]]},
        {"kind": "arc", "layer": "RD-CURB-LINE", "center": [0, 0], "radius": 1},
        {"kind": "polyline", "layer": "RD-CURB-LINE", "pts": [[3, 3]]},
    ]
    (tmp_path / "pieces.json").write_text(json.dumps({"source": "roads.dwg", "pieces": pieces}), encoding="utf-8")
    lines = cad_analysis.read_road_edges(tmp_path, ["RD-CURB-LINE"])
    assert [len(line.coords) for line in lines] == [3, 4, 2]
    assert lines[1].coords[0] == lines[1].coords[-1]
    with pytest.raises(project_profile.ProfileError, match="names no layer"):
        cad_analysis.read_road_edges(tmp_path, [])
    with pytest.raises(project_profile.ProfileError, match="no pieces.json"):
        cad_analysis.read_road_edges(tmp_path / "nothing", ["RD-CURB-LINE"])


def test_r71_main_reads_the_road_folder_and_reports_the_rule_that_decided_each_row(tmp_path, capsys):
    plots = row(["DP2", "DP2"], ["1", "2"])
    args = write_inputs(tmp_path, plots, analysis={**ANALYSIS, "roadEdgeLayers": ["RD"]})
    roads = tmp_path / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_text(json.dumps({"pieces": [{"kind": "polyline", "layer": "RD", "pts": [[-10, -5], [30, -5]]}]}), encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out")] + args + ["--roads", str(roads)]) == 0
    assert capsys.readouterr().out.strip().splitlines()[-1] == 'fronts {"road edge": 1}'
    result = json.loads((tmp_path / "out" / "analysis.json").read_text(encoding="utf-8"))
    assert result["summary"]["roadEdges"] == 1
    assert [(c["plots"], c["frontRule"]) for c in result["clusters"]] == [(["2", "1"], "road edge")]


def test_r71_a_road_folder_with_no_edge_layer_in_the_settings_is_refused_with_one_line(tmp_path, capsys):
    args = write_inputs(tmp_path, row(["DP2", "DP2"], ["1", "2"]))
    roads = tmp_path / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_text(json.dumps({"pieces": []}), encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out")] + args + ["--roads", str(roads)]) == 2
    error = capsys.readouterr().err
    assert "analysis.roadEdgeLayers" in error and error.count("\n") == 1


def box(x0, y0, x1, y1):
    return [[x0, y0], [x1, y0], [x1, y1], [x0, y1]]


def villa(name, outline, mirrored=False, rotation=0.0, **extra):
    return {"name": "X$0$" + name, "outline": outline, "mirrored": mirrored, "rotationDeg": rotation, **extra}


def test_r69_a_block_belongs_to_the_plot_with_the_largest_share_of_its_outline_and_a_plot_with_no_unit_code_is_an_issue(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "VL3", 10, 0)]
    # Plot 1 holds a tenth of the block, below the cover share, so only plot 2 gets the block
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Villa_VL3", box(9, 5, 19, 20), True, 30.0)])
    assert [(p["plot"], p["block"], p["mirrored"], p["rotation"], p["blocks"], p["unit"], p["unitSource"]) for p in result["plots"]] == [
        ("1", None, None, None, 0, None, None), ("2", "X$0$Villa_VL3", True, 30.0, 1, "VL3", "block")]
    assert [(i["rule"], i["plots"], i["text"]) for i in result["issues"]] == [("R69", ["1"], "Plot 1 (VL3) has no unit code, no block and no Excel unit")]
    blocks = result["summary"]["blocks"]
    assert (blocks["villaBlocks"], blocks["plotsOneBlock"], blocks["plotsNoBlock"], blocks["tiedBlocks"], blocks["mirrored"], blocks["notMirrored"], blocks["mirrorNull"]) == (1, 1, 1, 0, 1, 0, 0)
    assert blocks["blocksCovering"] == {"1": 1} and result["summary"]["units"]["bySource"] == {"block": 1, "excel": 0, "none": 1}


def test_r69_a_block_that_covers_several_plots_gives_its_unit_code_to_each_with_mirrored_null_and_a_flag(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "VL3", 10, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Villa_VL3", box(6, 5, 16, 20), True, 30.0)])
    assert [(p["plot"], p["unit"], p["unitSource"], p["block"], p["mirrored"], p["rotation"], p["blocks"], p["blockPlots"]) for p in result["plots"]] == [
        ("1", "VL3", "block", "X$0$Villa_VL3", None, 30.0, 1, 2), ("2", "VL3", "block", "X$0$Villa_VL3", None, 30.0, 1, 2)]
    assert [p["flags"] for p in result["plots"]] == [["block X$0$Villa_VL3 covers 2 plots"]] * 2
    assert result["issues"] == []
    blocks = result["summary"]["blocks"]
    assert (blocks["plotsOneBlock"], blocks["plotsCoveredOnly"], blocks["plotsOfSeveralPlotBlocks"], blocks["mirrored"], blocks["mirrorNull"]) == (2, 1, 2, 0, 2)
    assert blocks["blocksCovering"] == {"2": 1}


def test_r69_equal_shares_go_to_the_lowest_plot_number_and_are_counted_as_tied(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("9", "VL3", 10, 0), face("4", "VL3", 0, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Duplex_DP2", box(5, 5, 15, 20))])
    assert [(p["plot"], p["unit"], p["blockPlots"]) for p in result["plots"]] == [("4", "DP2", 2), ("9", "DP2", 2)]
    assert [p["flags"] for p in result["plots"]] == [["block X$0$Duplex_DP2 covers 2 plots"]] * 2
    assert result["summary"]["blocks"]["tiedBlocks"] == 1


def test_r69_a_plot_with_two_blocks_is_an_issue_and_keeps_no_flag(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 9), True), villa("Villa_VL3", box(1, 10, 9, 20), False)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks)
    assert [(i["rule"], i["plots"]) for i in result["issues"]] == [("R69", ["1"])]
    assert result["issues"][0]["text"] == "Plot 1 (VL3) has 2 blocks: X$0$Villa_VL3, X$0$Villa_VL3"
    assert (result["plots"][0]["block"], result["plots"][0]["mirrored"], result["plots"][0]["blocks"]) == (None, None, 2)
    assert result["summary"]["blocks"]["plotsTwoBlocks"] == 1


def test_r69_only_listed_plot_types_need_a_block_and_only_unit_blocks_are_matched(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "OS", 10, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 20)), villa("MP-Parking_6mx2.5m", box(11, 1, 14, 5)), villa("Villa_ZZ9", box(11, 6, 14, 9)),
              {"name": "X$0$Villa_VL3", "outline": None}, villa("Villa_VL3", box(500, 500, 510, 510))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks)
    assert result["issues"] == []
    assert [p["plot"] for p in result["plots"]] == ["1"]
    assert result["summary"]["blocks"]["villaBlocks"] == 2 and result["summary"]["blocks"]["blocksOnNoPlot"] == 1


def test_r69_the_block_of_each_plot_reaches_the_cluster_and_without_blocks_nothing_is_matched(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, -25)]
    blocks = [villa("Duplex_DP2", box(1, 1, 9, 24), True, 30.0), villa("Duplex_DP2", box(11, 1, 19, 24), False, 210.0)]
    result = cad_analysis.analyse(plots, profile, ANALYSIS, blocks, blocks_source="villas.dwg")
    cluster = result["clusters"][0]
    assert (cluster["code"], cluster["plots"], cluster["mirrored"], cluster["rotation"], cluster["block"]) == ("D02", ["1", "2"], [True, False], [30.0, 210.0], ["X$0$Duplex_DP2"] * 2)
    assert result["summary"]["blocks"]["source"] == "villas.dwg" and result["summary"]["unitsWithMirrorFlag"] == 2
    assert cluster["typologies"] == ["DP2", "DP2"] and cluster["sources"] == ["block", "block"]
    none_given = cad_analysis.analyse(plots, profile, ANALYSIS)
    assert none_given["summary"]["blocks"] is None and none_given["clusters"] == []
    assert [i["text"] for i in none_given["issues"]] == ["No block drawing and no Excel unit was given, so no plot has a unit code and no cluster can be built"]


def test_r69_main_reads_blocks_json_and_prints_the_block_counts(tmp_path, capsys):
    plots = [face("1", "VL3", 0, 0)]
    args = write_inputs(tmp_path, plots)
    blocks = tmp_path / "blocks"
    blocks.mkdir()
    (blocks / "blocks.json").write_text(json.dumps({"source": "villas.dwg", "blocks": [villa("Villa_VL3", box(1, 1, 9, 20), True)]}), encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out")] + args + ["--blocks", str(blocks)]) == 0
    lines = capsys.readouterr().out.strip().splitlines()
    assert lines[0].startswith("clusters 0 {}") and lines[1].startswith('blocks {"source": "villas.dwg", "villaBlocks": 1')
    result = json.loads((tmp_path / "out" / "analysis.json").read_text(encoding="utf-8"))
    assert result["plots"][0]["mirrored"] is True
    (blocks / "blocks.json").write_text('{"blocks": null}', encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out2")] + args + ["--blocks", str(blocks)]) == 2
    assert "blocks.json has no blocks list" in capsys.readouterr().err


REAL_PROFILE = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))


def test_r70_on_the_real_profile_a_manual_code_is_written_where_one_fits_and_an_old_code_only_where_none_does():
    types = ["DP2", "DP2", "DP4", "DP4", "DP2", "TH1", "TH1", "DP2", "DP2", "TH3", "TH3", "DP2", "DP4", "TH3", "TH3", "DP4"]
    plots = row(types, [str(number) for number in range(1, 17)]) + [behind(16, -25)]
    clusters = cad_analysis.derive_clusters(plots, REAL_PROFILE, ANALYSIS)
    assert [(c["code"], c["typologies"]) for c in clusters] == [
        ("M02", ["DP2", "DP2"]), ("M04", ["DP4", "DP4"]), ("M09", ["DP2", "TH1", "TH1", "DP2"]),
        ("M10", ["DP2", "TH3", "TH3", "DP2"]), ("Q02", ["DP4", "TH3", "TH3", "DP4"])]
    assert {c["orderSure"] for c in clusters} == {True}


def test_r69_on_the_real_profile_a_villa_block_of_a_typology_that_is_not_listed_is_not_a_unit_block():
    outline = [[0, 0], [10, 0], [10, 25], [0, 25]]
    blocks = [{"name": "X$0$Villa_VL4", "outline": outline}, {"name": "X$0$Villa_VL2", "outline": outline}, {"name": "X$0$Townhouse_TH1", "outline": outline}]
    assert [block["code"] for block in cad_analysis.villa_blocks(blocks, REAL_PROFILE)] == ["VL4", "TH1"]


def outlines_of(plots):
    return [Polygon([(vertex[0], vertex[1]) for vertex in plot["vertices"]]) for plot in plots]


def test_r64_a_short_shared_wall_makes_plots_adjacent_but_not_side_by_side(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 24)]
    assert sorted(cad_analysis.adjacent_pairs(outlines_of(plots), 0.01)) == [(0, 1)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert sorted(len(cluster["plots"]) for cluster in clusters) == [1, 1]


def test_r64_a_shared_wall_exactly_as_long_as_the_party_wall_share_is_a_party_wall_and_a_hair_shorter_is_not(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"])
    wall = max(cad_analysis.adjacent_pairs(outlines_of(plots), 0.01).values(), key=lambda shared: shared.length).length
    share = wall / 25.0
    for candidate in (share, math.nextafter(share, 2.0), math.nextafter(share, 0.0)):
        if candidate * 25.0 == wall:
            share = candidate
            break
    else:
        pytest.fail("no share gives the wall length back")
    on_the_limit = cad_analysis.derive_clusters(plots, profile, {**ANALYSIS, "partyWallShare": share})
    assert [(c["code"], len(c["plots"])) for c in on_the_limit] == [("D02", 2)]
    over = cad_analysis.derive_clusters(plots, profile, {**ANALYSIS, "partyWallShare": share * 1.0001})
    assert sorted(len(c["plots"]) for c in over) == [1, 1]


def test_r64_a_shared_edge_lying_exactly_at_the_end_distance_does_not_count_as_an_end(tmp_path):
    plots = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, 25)]
    outlines = outlines_of(plots)
    union = unary_union(outlines[:2])
    wall = max(cad_analysis.adjacent_pairs(outlines[:2], 0.01).values(), key=lambda shared: shared.length)
    direction = cad_analysis.direction_of(wall)
    tree = STRtree(outlines)
    # The offset of the shared edge from the centre of the row, worked out the way end_neighbours works it out
    shared = union.boundary.buffer(0.01).intersection(outlines[2].boundary)
    offset = abs((shared.centroid.x - union.centroid.x) * direction[0] + (shared.centroid.y - union.centroid.y) * direction[1])
    assert 12.4 < offset < 12.6
    at_the_distance = cad_analysis.end_neighbours(union, {0, 1}, direction, tree, outlines, 0.01, offset)
    nearer = cad_analysis.end_neighbours(union, {0, 1}, direction, tree, outlines, 0.01, math.nextafter(offset, 0.0))
    assert at_the_distance == [0.0, 0.0] and sum(nearer) > 0


def test_r71_two_ends_whose_road_distances_differ_by_exactly_the_tolerance_decide_nothing(tmp_path):
    profile = profile_in(tmp_path)
    analysis = {**ANALYSIS, "toleranceM": 0.5}
    pair = row(["DP2", "DP2"], ["1", "2"])
    tied = cad_analysis.derive_clusters(pair, profile, analysis, [road(-5), road(30.5)])[0]
    assert (tied["frontRule"], tied["orderSure"]) == ("none", False)
    apart = cad_analysis.derive_clusters(pair, profile, analysis, [road(-5), road(31)])[0]
    assert (apart["frontRule"], apart["plots"]) == ("road edge", ["2", "1"])


def test_r70_with_two_listed_sizes_that_tie_the_longest_cluster_comes_first(tmp_path):
    profile = profile_in(tmp_path)
    known = {"A": ("X", "X"), "B": ("X", "X", "X")}
    pieces, ways = cad_analysis.best_split(["X"] * 5, [None] * 5, known, profile, reverse_ok=False)
    assert pieces == [(0, 3, "B", False), (3, 5, "A", False)] and ways == 2


def test_r69_a_plot_holding_exactly_the_cover_share_of_a_block_counts_in_blocks_covering(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0, width=2, depth=10), face("2", "VL3", 2, 0, width=8, depth=10)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Villa_VL3", box(0, 0, 10, 10))])
    assert result["summary"]["blocks"]["blocksCovering"] == {"2": 1}
    assert [record["blocks"] for record in result["plots"]] == [1, 1]
    assert [record["blockPlots"] for record in result["plots"]] == [2, 2] and result["summary"]["blocks"]["plotsCoveredOnly"] == 1


def test_r69_an_outline_needs_three_points_to_make_a_unit_block(tmp_path):
    profile = profile_in(tmp_path)
    two = {"name": "X$0$Villa_VL3", "outline": [[0, 0], [5, 5]]}
    three = {"name": "X$0$Villa_VL3", "outline": [[0, 0], [5, 0], [5, 5]]}
    assert cad_analysis.villa_blocks([two, three], profile) == [{**three, "code": "VL3"}]


def test_r69_a_block_that_only_touches_a_plot_belongs_to_no_plot(tmp_path):
    profile = profile_in(tmp_path)
    result = cad_analysis.analyse([face("1", "VL3", 0, 0)], profile, ANALYSIS, [villa("Villa_VL3", box(10, 0, 20, 25))])
    assert result["summary"]["blocks"]["blocksOnNoPlot"] == 1 and result["plots"][0]["blocks"] == 0


def test_r69_shares_that_differ_by_less_than_a_millionth_are_a_tie_and_go_to_the_lowest_plot_number(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("9", "VL3", 0, 0), face("4", "VL3", 10, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Villa_VL3", box(4.9999999, 5, 14.9999999, 20))])
    assert [(record["plot"], record["blocks"], record["blockPlots"]) for record in result["plots"]] == [("4", 1, 2), ("9", 1, 2)]
    assert result["summary"]["blocks"]["tiedBlocks"] == 1


def test_r69_the_block_counts_are_over_the_plot_records_and_other_plots_with_blocks_are_counted_apart(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "OS", 10, 0), face("3", "OS", 20, 0), face("4", "VL3", 30, 0), face("5", "VL3", 40, 0)]
    blocks = [villa("Villa_VL3", box(31, 1, 39, 20)), villa("Villa_VL3", box(21, 1, 29, 20)), villa("Villa_VL3", box(41, 1, 49, 9)), villa("Villa_VL3", box(41, 11, 49, 24))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks)
    summary = result["summary"]["blocks"]
    assert (summary["plotsOneBlock"], summary["plotsNoBlock"], summary["plotsTwoBlocks"], summary["otherPlotsWithBlocks"]) == (2, 1, 1, 1)
    assert (summary["plotsOneBlock"] + summary["plotsNoBlock"] + summary["plotsTwoBlocks"], len(result["plots"])) == (4, 4)
    assert [(issue["plots"], issue["text"].split(" has ")[1]) for issue in result["issues"]] == [(["1"], "no unit code, no block and no Excel unit"), (["5"], "2 blocks: X$0$Villa_VL3, X$0$Villa_VL3")]


def test_r69_when_the_block_drawing_holds_no_unit_block_one_issue_says_so_instead_of_one_for_every_plot(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face(str(number), "VL3", 10 * number, 0) for number in range(1, 6)]
    parking = {"name": "X$0$MP-Parking_6mx2.5m", "outline": box(0, 0, 3, 3)}
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [parking, {"name": "Villa_ZZ9", "outline": box(0, 0, 3, 3)}])
    assert result["issues"] == [{"rule": "R69", "plots": [], "text": "The block drawing holds no unit block, none of its 2 inserts is named after a typology of the profile"}]
    assert result["summary"]["blocks"]["villaBlocks"] == 0 and result["summary"]["blocks"]["plotsNoBlock"] == 5


def test_r69_a_plot_face_that_crosses_itself_is_repaired_before_blocks_are_matched_and_rows_are_built(tmp_path):
    profile = profile_in(tmp_path)
    bowtie = {"kind": "plot_shape", "plot": "1", "type": "VL3", "vertices": [[0, 0], [10, 10], [10, 0], [0, 10]]}
    result = cad_analysis.analyse([bowtie], profile, ANALYSIS, [villa("Villa_VL3", box(0, 0, 10, 10))])
    assert result["plots"][0]["blocks"] == 1
    twisted = {"kind": "plot_shape", "plot": "2", "type": "DP2", "unit": "DP2", "vertices": [[10, 25], [20, 0], [20, 25], [10, 0]]}
    pair = cad_analysis.derive_clusters([face("1", "DP2", 0, 0), twisted], profile, ANALYSIS)
    assert [(cluster["code"], cluster["plots"]) for cluster in pair] == [("D02", ["1", "2"])]


def test_r64_two_rows_that_share_their_lowest_plot_number_get_different_row_ids_and_count_as_two_rows(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"]) + row(["DP2", "DP2"], ["1", "3"], y=200)
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert sorted(cluster["rowId"] for cluster in clusters) == ["1", "1#2"]
    assert cad_analysis.analyse(plots, profile, ANALYSIS, excel=excel_of(plots))["summary"]["rows"] == 2


def shifts_in(tmp_path):
    path = tmp_path / "shifts.json"
    path.write_text(json.dumps({"neighbourhoods": {"IMC": {"shift": SHIFT}}, "sourceCodePattern": "-(IMC|IMD)-", "searchRange": 1}), encoding="utf-8")
    return cad_analysis.read_shifts(path)


def sheet(*rows, source=BOOK):
    return {"source": source, "rows": [{"sheetRow": number, "Plot No.": plot, "Unit": unit} for number, (plot, unit) in enumerate(rows, start=40)]}


def given(units):
    return {"units": units, "stats": {"rows": len(units), "used": len(units), "ambiguous": 0}, "issues": []}


def test_r74_the_excel_unit_of_a_plot_is_keyed_by_the_baseplan_label_the_shift_below_the_official_number(tmp_path):
    shifts = shifts_in(tmp_path)
    read = cad_analysis.read_excel_units([sheet((11, " DP2 "), (12.0, "TH1"), ("13", "VL3"))], shifts)
    assert read["units"] == {"1": "DP2", "2": "TH1", "3": "VL3"}
    assert read["stats"] == {"rows": 3, "used": 3, "ambiguous": 0} and read["issues"] == []


def test_r74_a_row_with_no_numeric_plot_number_or_no_unit_is_left_out_with_an_issue(tmp_path):
    shifts = shifts_in(tmp_path)
    read = cad_analysis.read_excel_units([sheet(("x", "DP2"), (None, "DP2"), (15, None), (16, "  "), (17, 5), (18, "DP2"))], shifts)
    assert read["units"] == {"8": "DP2"} and read["stats"]["rows"] == 6 and read["stats"]["used"] == 1
    assert [issue["text"] for issue in read["issues"]] == [
        BOOK + " row 40 has no numeric plot number", BOOK + " row 41 has no numeric plot number", BOOK + " row 42 (plot 15) has no unit",
        BOOK + " row 43 (plot 16) has no unit", BOOK + " row 44 (plot 17) has no unit"]
    assert {issue["rule"] for issue in read["issues"]} == {"R69"}


def test_r74_two_rows_that_end_on_one_baseplan_label_give_neither_unit_and_one_issue_each(tmp_path):
    shifts = shifts_in(tmp_path)
    read = cad_analysis.read_excel_units([sheet((11, "DP2"), (11, "TH1"), (12, "VL3"), (11, "DP2"))], shifts)
    assert read["units"] == {"2": "VL3"} and read["stats"] == {"rows": 4, "used": 1, "ambiguous": 1}
    assert [issue["plots"] for issue in read["issues"]] == [["1"], ["1"]]


def test_r74_a_workbook_whose_neighbourhood_has_no_shift_is_refused_and_the_shift_is_never_guessed(tmp_path):
    shifts = shifts_in(tmp_path)
    with pytest.raises(project_profile.ProfileError, match="no plot number shift for the workbook 00101-PSN-IMD-XXX-SCH-TU-090000.xlsx"):
        cad_analysis.read_excel_units([sheet((11, "DP2"), source="00101-PSN-IMD-XXX-SCH-TU-090000.xlsx")], shifts)
    with pytest.raises(project_profile.ProfileError, match="no plot number shift for the workbook book.xlsx"):
        cad_analysis.read_excel_units([sheet((11, "DP2"), source="book.xlsx")], shifts)


def test_r69_a_plot_with_no_block_takes_the_excel_unit_and_the_source_says_so(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "ZZ", 10, 0), face("3", "VL3", 20, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, excel=given({"1": "DP2", "2": "TH1"}))
    assert [(p["plot"], p["unit"], p["unitSource"], p["excelUnit"], p["blocks"]) for p in result["plots"]] == [
        ("1", "DP2", "excel", "DP2", 0), ("2", "TH1", "excel", "TH1", 0), ("3", None, None, None, 0)]
    assert [(i["rule"], i["plots"]) for i in result["issues"] if i["rule"] == "R69"] == [("R69", ["3"])]
    assert result["summary"]["units"]["bySource"] == {"block": 0, "excel": 2, "none": 1}
    assert result["summary"]["units"]["excel"]["noFace"] == 0


def test_r69_the_baseplan_label_type_never_gives_the_unit_code_and_a_block_unit_beats_the_excel_unit(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 24)), villa("Villa_ZZ1", box(11, 1, 19, 24))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks, excel=given({"1": "TH1", "2": "ZZ1"}))
    assert [(p["plot"], p["cadType"], p["unit"], p["unitSource"], p["excelUnit"]) for p in result["plots"]] == [("1", "DP2", "VL3", "block", "TH1"), ("2", "DP2", "ZZ1", "block", "ZZ1")]
    units = result["summary"]["units"]
    assert (units["plotsWithBoth"], units["blockUnitAgrees"], units["blockUnitDiffers"], units["differing"]) == (2, 1, 1, {"VL3>TH1": 1})
    assert "typeAgrees" not in result["summary"]["blocks"] and "typeDiffers" not in result["summary"]["blocks"]


def test_r64_clusters_are_built_from_the_unit_code_and_not_from_the_baseplan_label_type(tmp_path):
    profile = profile_in(tmp_path)
    labelled_pair = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0)]
    # The label says duplex on both plots, the unit code says villa on one, so no pair forms
    villas = cad_analysis.analyse(labelled_pair, profile, ANALYSIS, [], excel=given({"1": "DP2", "2": "VL3"}))
    assert [(c["code"], c["plots"]) for c in villas["clusters"]] == [("unknown", ["1"])]
    other_pair = [face("1", "VL3", 0, 0), face("2", "VL3", 10, 0)]
    duplex = cad_analysis.analyse(other_pair, profile, ANALYSIS, [], excel=given({"1": "DP2", "2": "DP2"}))
    assert [(c["code"], c["plots"], c["typologies"], c["sources"]) for c in duplex["clusters"]] == [("D02", ["1", "2"], ["DP2", "DP2"], ["excel", "excel"])]
    no_unit = cad_analysis.analyse(labelled_pair, profile, ANALYSIS, [villa("Villa_DX9", box(500, 500, 510, 510))])
    assert no_unit["clusters"] == [] and [i["plots"] for i in no_unit["issues"]] == [["1"], ["2"]]


def test_r69_a_plot_with_two_blocks_takes_no_excel_unit_and_stays_out_of_every_cluster(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 9)), villa("Villa_VL3", box(1, 12, 9, 24)), villa("Duplex_DP2", box(11, 1, 19, 24))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks, excel=given({"1": "DP2", "2": "DP2"}))
    first = result["plots"][0]
    assert (first["unit"], first["unitSource"], first["excelUnit"], first["blocks"]) == (None, None, "DP2", 2)
    assert [(c["code"], c["plots"]) for c in result["clusters"]] == [("unknown", ["2"])]
    assert [(i["rule"], i["plots"]) for i in result["issues"] if i["rule"] == "R69"] == [("R69", ["1"])]


def test_r69_a_block_that_also_covers_a_plot_that_owns_another_block_leaves_the_owned_block_and_flags_it(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0), face("2", "VL3", 10, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 24)), villa("Duplex_DP2", box(7, 1, 19, 24))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks)
    first = result["plots"][0]
    assert (first["unit"], first["block"], first["blockPlots"]) == ("VL3", "X$0$Villa_VL3", 1)
    assert first["flags"] == ["also covered by X$0$Duplex_DP2"]
    assert result["plots"][1]["unit"] == "DP2" and result["plots"][1]["flags"] == ["block X$0$Duplex_DP2 covers 2 plots"]


def test_r75_a_cluster_with_a_no_match_code_is_an_issue_in_the_analysis_and_a_code_that_is_not_a_cluster_is_refused(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "TH1"], ["1", "2"])
    result = cad_analysis.analyse(plots, profile, {**ANALYSIS, "noRev6Match": ["E02"]}, excel=excel_of(plots))
    assert [(c["code"], c["plots"]) for c in result["clusters"]] == [("E02", ["1", "2"])]
    assert [(i["rule"], i["plots"]) for i in result["issues"] if i["rule"] == "R75"] == [("R75", ["1", "2"])]
    for bad, text in (({"noRev6Match": ["ZZ"]}, "which is not a cluster of the profile"), ({"noRev6Match": "Q02"}, "must be a list of codes"), ({"unitPlotTypes": [1]}, "must be a list of codes")):
        with pytest.raises(project_profile.ProfileError, match=text):
            cad_analysis.analyse(plots, profile, {**ANALYSIS, **bad})


def test_r75_a_code_that_cluster_aliases_points_at_a_rev_6_cluster_cannot_be_a_no_match_code(tmp_path):
    values = {**PROFILE_VALUES, "cluster_aliases": {"D02": "M02"}}
    path = tmp_path / "aliased.json"
    path.write_text(json.dumps(values), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="which cluster_aliases gives a Rev 6 cluster"):
        cad_analysis.analyse(row(["DP2", "DP2"], ["1", "2"]), project_profile.load_profile(path), {**ANALYSIS, "noRev6Match": ["D02"]})


def test_r69_a_block_whose_largest_share_is_under_the_cover_share_belongs_to_no_plot_and_gives_no_unit(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0)]
    # A hundredth of the block lies in plot 1, so it only touches the edge of the plot
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Villa_VL3", box(-9.9, 5, 0.1, 20))], excel=given({"1": "TH1"}))
    assert result["summary"]["blocks"]["blocksOnNoPlot"] == 1 and result["summary"]["blocks"]["blocksCovering"] == {}
    assert [(p["unit"], p["unitSource"], p["blocks"]) for p in result["plots"]] == [("TH1", "excel", 0)]


def test_r69_a_share_a_tie_width_under_the_cover_share_still_covers_and_one_two_tie_widths_under_does_not():
    assert cad_analysis.holds_cover(0.2) and cad_analysis.holds_cover(0.2 - 5e-7)
    assert not cad_analysis.holds_cover(0.2 - 2e-6) and not cad_analysis.holds_cover(0.0)


def test_r69_a_fifth_of_a_block_that_floats_a_hair_under_a_fifth_at_site_coordinates_still_covers_in_both_methods(tmp_path):
    x, y = 570571.083, 2362999.179
    faces = cad_analysis.plot_faces([face("1", "VL3", x, y, width=1.6, depth=10), face("2", "VL3", x + 1.6, y, width=6.4, depth=10)])
    villas = [{**villa("Villa_VL3", box(x, y, x + 8, y + 10)), "code": "VL3"}]
    polygons = cad_analysis.face_polygons(faces)
    share = cad_analysis.shares_of(cad_analysis.outline_of(villas[0]), polygons, STRtree(polygons))
    # The share of plot 1 is a fifth, and the clipping gives it as 0.19999999999709, which is under COVER_SHARE
    assert max(found for found, index in share if index == 0) < cad_analysis.COVER_SHARE
    assert cad_analysis.claim_blocks(faces, villas)[2] == [[0, 1]]
    assert block_proof.compare(faces, villas)["blocksAgree"] == 1


def test_r69_an_excel_unit_that_is_not_a_unit_code_of_the_profile_gives_no_unit_code_and_one_issue_and_is_never_normalised(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0), face("3", "TH1", 20, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, excel=given({"1": "dp2", "2": "XX9", "3": "TH1"}))
    assert [(p["plot"], p["unit"], p["unitSource"]) for p in result["plots"]] == [("1", None, None), ("2", None, None), ("3", "TH1", "excel")]
    texts = [i["text"] for i in result["issues"] if i["rule"] == "R69" and i["plots"] == ["1"]]
    assert "Plot 1 has the Excel unit 'dp2', which is not a unit code of the profile, so it gives no unit code" in texts
    assert result["summary"]["units"]["notInProfile"] == 2 and result["summary"]["units"]["bySource"] == {"block": 0, "excel": 1, "none": 2}


def test_r69_a_schedule_with_no_unit_says_so_and_a_unit_block_with_no_outline_is_counted(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "VL3", 0, 0)]
    empty = cad_analysis.analyse(faces, profile, ANALYSIS, excel={"units": {}, "stats": {"rows": 0, "used": 0, "ambiguous": 0}, "issues": []})
    assert [i["text"] for i in empty["issues"]] == ["The schedule workbooks gave no unit code of the profile, so they give no plot a unit code"]
    two_points = {"name": "X$0$Villa_VL3", "outline": [[0, 0], [5, 5]]}
    only = cad_analysis.analyse(faces, profile, ANALYSIS, [two_points])
    assert [i["text"] for i in only["issues"]] == ["The block drawing holds no unit block with an outline, 1 of its 1 inserts are named after a typology of the profile but have no outline of three points"]
    some = cad_analysis.analyse(faces, profile, ANALYSIS, [two_points, villa("Villa_VL3", box(1, 1, 9, 24))])
    assert some["summary"]["blocks"]["unitBlocksNoOutline"] == 1 and some["issues"] == []


def test_r74_a_plot_number_cell_is_a_whole_number_or_it_is_not_and_a_superscript_digit_is_not(tmp_path):
    assert [cad_analysis.whole_number(value) for value in (11, "11", " 12.0 ", 13.0, 13.5, True, "²", "١٢", None, "", "x1")] == [11, 11, 12, 13, None, None, None, None, None, None, None]
    shifts = shifts_in(tmp_path)
    read = cad_analysis.read_excel_units([sheet(("²", "DP2"), (11, "TH1"))], shifts)
    assert read["units"] == {"1": "TH1"} and [i["text"] for i in read["issues"]] == [BOOK + " row 40 has no numeric plot number"]


def test_r69_where_the_block_and_the_schedule_give_different_units_the_block_wins_and_the_pair_is_listed_as_a_conflict(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "DP2", 0, 0), face("2", "DP2", 10, 0)]
    blocks = [villa("Villa_VL3", box(1, 1, 9, 24)), villa("Villa_DX9", box(11, 1, 19, 24))]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, blocks, excel=given({"1": "TH1", "2": "DX9"}))
    assert result["conflicts"] == [{"plot": "1", "block": "X$0$Villa_VL3", "blockUnit": "VL3", "excelUnit": "TH1"}]
    assert cad_analysis.analyse(faces, profile, ANALYSIS, blocks)["conflicts"] == []


def test_r69_a_block_that_stands_for_more_plots_than_the_biggest_cluster_is_an_issue_naming_the_block_and_the_plots(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face(str(number), "VL3", 10 * (number - 1), 0) for number in range(1, 6)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, [villa("Duplex_DP2", box(0, 5, 50, 20))])
    assert [(i["rule"], i["plots"], i["text"]) for i in result["issues"] if i["rule"] == "R69"] == [("R69", ["1", "2", "3", "4", "5"], "Block X$0$Duplex_DP2 stands for 5 plots, more than the 4 of the biggest cluster")]
    assert [p["unit"] for p in result["plots"]] == ["DP2"] * 5


def test_r69_a_schedule_whose_units_are_all_dropped_as_not_profile_codes_says_so_once_and_leaves_no_plot_unissued(tmp_path):
    profile = profile_in(tmp_path)
    faces = [face("1", "DP2", 0, 0), face("5", "DP2", 10, 0), face("7", "OS", 20, 0)]
    result = cad_analysis.analyse(faces, profile, ANALYSIS, excel=given({"1": "XX9", "5": "XX9"}))
    texts = [i["text"] for i in result["issues"] if i["rule"] == "R69"]
    assert "The schedule workbooks gave no unit code of the profile, so they give no plot a unit code" in texts
    assert sum(1 for text in texts if "is not a unit code of the profile" in text) == 2
    assert result["summary"]["units"]["bySource"] == {"block": 0, "excel": 0, "none": 2}


@pytest.mark.parametrize("value", [None, "4", 4.0, True, 0, -1, 2.5])
def test_r69_a_profile_cluster_block_max_that_is_not_a_whole_number_of_plots_is_refused_in_one_error(tmp_path, value):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps({**PROFILE_VALUES, "cluster_block_max": value}), encoding="utf-8")
    with pytest.raises(project_profile.ProfileError, match="cluster_block_max must be a whole number of plots"):
        cad_analysis.analyse(row(["DP2", "DP2"], ["1", "2"]), project_profile.load_profile(path), ANALYSIS)


def test_r74_a_negative_number_a_huge_text_and_a_float_with_a_fraction_are_not_plot_numbers():
    assert [cad_analysis.whole_number(value) for value in (-5, -3.0, "-5", float("nan"), float("inf"), "9" * 5000, 0, "0.0")] == [None, None, None, None, None, None, 0, 0]
