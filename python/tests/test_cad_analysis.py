import json

import pytest
from shapely.geometry import Polygon

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
    "plot": {"long_side": 25000},
}
ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "endShare": 0.25}


def profile_in(tmp_path):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    return project_profile.load_profile(path)


def face(label, typology, x, y, width=10, depth=25, **extra):
    corners = [(x, y), (x + width, y), (x + width, y + depth), (x, y + depth)]
    return {"kind": "plot_shape", "plot": label, "type": typology, "vertices": [list(corner) for corner in corners], **extra}


def row(types, labels, x0=0, y=0):
    return [face(label, typology, x0 + 10 * index, y) for index, (typology, label) in enumerate(zip(types, labels))]


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
    clusters = cad_analysis.derive_clusters(row(["DP2", "TH1", "DP2"], ["1", "2", "3"]), profile, ANALYSIS)
    assert [(c["code"], c["typologies"]) for c in clusters] == [("unknown", ["DP2", "TH1", "DP2"])]
    odd = cad_analysis.derive_clusters(row(["DP2", "DP3", "DP2"], ["1", "2", "3"]), profile, ANALYSIS)
    assert sorted(c["plots"] for c in odd) == [["1"], ["3"]]


def test_r64_a_group_that_is_not_one_row_is_unknown_and_listed_by_plot_number(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("20", "DP2", 0, 0, width=25), face("30", "DP2", -10, 0), face("10", "DP2", 25, 0), face("40", "DP2", 0, 25, width=25)]
    clusters = cad_analysis.derive_clusters(plots, profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["orderSure"], c["row"]) for c in clusters] == [("unknown", ["10", "20", "30", "40"], False, False)]
    summary = cad_analysis.analyse(plots, profile, ANALYSIS)["summary"]
    assert (summary["notRows"], summary["orderNotSure"], summary["clusters"]) == (1, 1, 1)


def test_r64_a_group_with_two_ends_and_a_plot_touching_three_others_is_not_a_chain():
    links = {0: [1], 1: [0, 2, 3], 2: [1, 3], 3: [1, 2, 4], 4: [3]}
    assert cad_analysis.chain_of([0, 1, 2, 3, 4], links) is None
    assert cad_analysis.chain_of([0, 1], {0: [1], 1: [0]}) == [0, 1]
    assert cad_analysis.chain_of([7], {7: []}) == [7]


def test_r64_the_mirror_flag_comes_from_the_block_scale(tmp_path):
    assert cad_analysis.mirrored_from_scale({"scale": [-1, 1, 1]}) is True
    assert cad_analysis.mirrored_from_scale({"scale": [1, -1, 1]}) is True
    assert cad_analysis.mirrored_from_scale({"scale": [-1, -1, 1]}) is False
    assert cad_analysis.mirrored_from_scale({"scale": [1, 1, 1]}) is False
    assert cad_analysis.mirrored_from_scale({"name": "no scale read"}) is None
    assert cad_analysis.mirrored_from_scale(None) is None
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"])
    plots[1]["block"] = {"scale": [-1, 1, 1]}
    cluster = cad_analysis.derive_clusters(plots, profile, ANALYSIS)[0]
    assert cluster["plots"] == ["1", "2"] and cluster["mirrored"] == [None, True]


def test_r64_faces_without_an_outline_a_label_or_a_numeric_label_do_not_stop_the_analysis(tmp_path):
    profile = profile_in(tmp_path)
    entries = row(["DP2", "DP2"], ["1", "2"]) + [
        {"kind": "plot_shape", "plot": "3", "type": "DP2", "vertices": [[0, 0], [1, 1]]},
        {"kind": "plot_shape", "plot": None, "type": "DP2", "vertices": [[0, 0], [1, 0], [1, 1]]},
        {"kind": "plot_shape", "type": "DP2", "vertices": [[0, 0], [1, 0], [1, 1]]},
        face("²", "OS", 100, 0),
        face("X", ["not", "a", "string"], 200, 0),
    ]
    result = cad_analysis.analyse(entries, profile, ANALYSIS)
    assert result["summary"]["plotFaces"] == 4
    assert [(c["code"], c["plots"]) for c in result["clusters"]] == [("D02", ["1", "2"])]
    assert cad_analysis.plot_key("²") == (1, 0, "²") and cad_analysis.plot_key("12") == (0, 12, "12")


def write_inputs(tmp_path, plots, analysis=None, plots_text=None):
    profile = profile_in(tmp_path)
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(plots_text if plots_text is not None else json.dumps({"source": "base.dwg", "plots": plots}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile.path), "analysis": ANALYSIS if analysis is None else analysis}), encoding="utf-8")
    return ["--cad", str(cad), "--project", str(settings)]


def test_r64_main_writes_the_clusters_the_issues_and_no_coordinates_to_analysis_json(tmp_path, capsys):
    plots = row(["DP2", "DP2"], ["1", "2"]) + [behind(2, 25)] + row(["DP2", "TH1", "DP2"], ["3", "4", "5"], y=100)
    plots.append({"kind": "polyline", "layer": "ROAD", "vertices": [[-50, 200], [150, 200]]})
    args = write_inputs(tmp_path, plots)
    assert cad_analysis.main([str(tmp_path / "out")] + args) == 0
    assert capsys.readouterr().out.strip() == 'clusters 2 {"D02": 1, "unknown": 1}, issues 1'
    result = json.loads((tmp_path / "out" / "analysis.json").read_text(encoding="utf-8"))
    assert list(result) == ["source", "summary", "clusters", "issues"] and result["source"] == "base.dwg"
    assert result["summary"] == {"plotFaces": 6, "clusters": 2, "clusterPlots": 5, "byCode": {"D02": 1, "unknown": 1}, "notRows": 0,
                                 "orderNotSure": 1, "unitsWithMirrorFlag": 0, "issues": 1, "numbering": "baseplan labels"}
    assert [(c["code"], c["plots"], c["orderSure"]) for c in result["clusters"]] == [("D02", ["2", "1"], True), ("unknown", ["3", "4", "5"], False)]
    assert set(result["clusters"][0]) == {"code", "plots", "typologies", "mirrored", "orderSure", "row"}
    assert [(i["rule"], i["plots"]) for i in result["issues"]] == [("R66", ["3", "4", "5"])]
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
