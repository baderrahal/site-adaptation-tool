import json

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
    "clusters": {"D02": ["DP2", "DP2"], "Q01": ["DP2", "TH1", "TH1", "DP2"]},
    "plot": {"long_side": 25000},
}
ANALYSIS = {"toleranceM": 0.01, "partyWallShare": 0.75, "frontMarginM": 3, "roadLayers": ["ROAD"]}
NORTH = [[(-50, 60), (150, 60)]]
SOUTH = [[(-50, -35), (150, -35)]]


def profile_in(tmp_path):
    path = tmp_path / "profile.json"
    path.write_text(json.dumps(PROFILE_VALUES), encoding="utf-8")
    return project_profile.load_profile(path)


def face(label, typology, x, y, width=10, depth=25, **extra):
    corners = [(x, y), (x + width, y), (x + width, y + depth), (x, y + depth), (x, y)]
    return {"kind": "plot_shape", "plot": label, "type": typology, "vertices": [list(corner) for corner in corners], **extra}


def row(types, labels, x0=0, y=0):
    return [face(label, typology, x0 + 10 * index, y) for index, (typology, label) in enumerate(zip(types, labels))]


def test_r64_a_row_of_four_matches_q01_and_is_ordered_left_to_right_facing_the_road(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "TH1", "TH1", "DP2"], ["7", "5", "9", "6"])
    north = cad_analysis.derive_clusters(plots, NORTH, profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["typologies"], c["orderSure"]) for c in north] == [("Q01", ["7", "5", "9", "6"], ["DP2", "TH1", "TH1", "DP2"], True)]
    south = cad_analysis.derive_clusters(plots, SOUTH, profile, ANALYSIS)
    assert south[0]["plots"] == ["6", "9", "5", "7"]


def test_r64_the_order_is_not_sure_when_no_end_is_clearly_nearer_the_road_or_there_is_no_road(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["8", "3"])
    beside = cad_analysis.derive_clusters(plots, [[(1000, -50), (1000, 100)]], profile, ANALYSIS)
    assert beside[0]["code"] == "D02" and beside[0]["orderSure"] is False
    none = cad_analysis.derive_clusters(plots, None, profile, ANALYSIS)
    assert none[0]["plots"] == ["3", "8"] and none[0]["orderSure"] is False


def test_r64_plots_that_touch_at_a_corner_or_back_to_back_are_not_side_by_side(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("1", "DP2", 0, 0), face("2", "DP2", 10, -25), face("3", "DP2", 0, 25)]
    pairs = cad_analysis.adjacent_pairs([Polygon([tuple(v) for v in plot["vertices"]]) for plot in plots], 0.01)
    assert sorted(pairs) == [(0, 2)]
    clusters = cad_analysis.derive_clusters(plots, NORTH, profile, ANALYSIS)
    assert sorted(len(cluster["plots"]) for cluster in clusters) == [1, 1, 1]
    assert {cluster["code"] for cluster in clusters} == {"unknown"}


def test_r64_villa_kinds_are_left_out_and_the_kind_comes_from_the_profile_not_the_code_letters(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2", "DX9", "ZZ1", "ZZ1", "VL3"], ["1", "2", "3", "4", "5", "6"])
    clusters = cad_analysis.derive_clusters(plots, NORTH, profile, ANALYSIS)
    assert [(c["code"], c["plots"]) for c in clusters] == [("D02", ["1", "2"]), ("unknown", ["4", "5"])]


def test_r64_a_sequence_that_is_not_in_the_profile_is_written_as_unknown(tmp_path):
    profile = profile_in(tmp_path)
    clusters = cad_analysis.derive_clusters(row(["DP2", "TH1", "DP2"], ["1", "2", "3"]), NORTH, profile, ANALYSIS)
    assert [(c["code"], c["typologies"]) for c in clusters] == [("unknown", ["DP2", "TH1", "DP2"])]


def test_r64_a_group_that_is_not_one_row_is_unknown_and_listed_by_plot_number(tmp_path):
    profile = profile_in(tmp_path)
    plots = [face("20", "DP2", 0, 0, width=25), face("30", "DP2", -10, 0), face("10", "DP2", 25, 0), face("40", "DP2", 0, 25, width=25)]
    clusters = cad_analysis.derive_clusters(plots, NORTH, profile, ANALYSIS)
    assert [(c["code"], c["plots"], c["orderSure"]) for c in clusters] == [("unknown", ["10", "20", "30", "40"], False)]


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
    cluster = cad_analysis.derive_clusters(plots, NORTH, profile, ANALYSIS)[0]
    assert cluster["plots"] == ["1", "2"] and cluster["mirrored"] == [None, True]


def test_r64_main_writes_the_clusters_the_issues_and_no_coordinates_to_analysis_json(tmp_path):
    profile = profile_in(tmp_path)
    plots = row(["DP2", "DP2"], ["1", "2"]) + row(["DP2", "TH1", "DP2"], ["3", "4", "5"], y=100)
    plots.append({"kind": "polyline", "layer": "ROAD", "vertices": [[-50, 200], [150, 200]]})
    plots.append({"kind": "polyline", "layer": "OTHER", "vertices": [[-50, 0], [150, 0]]})
    cad = tmp_path / "cad"
    cad.mkdir()
    (cad / "plots.json").write_text(json.dumps({"source": "base.dwg", "plots": plots}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps({"profile": str(profile.path), "analysis": ANALYSIS}), encoding="utf-8")
    assert cad_analysis.main([str(tmp_path / "out"), "--cad", str(cad), "--project", str(settings)]) == 0
    result = json.loads((tmp_path / "out" / "analysis.json").read_text(encoding="utf-8"))
    assert [(c["code"], c["plots"]) for c in result["clusters"]] == [("D02", ["1", "2"]), ("unknown", ["3", "4", "5"])]
    assert set(result["clusters"][0]) == {"code", "plots", "typologies", "mirrored", "orderSure"}
    assert [(i["rule"], i["plots"]) for i in result["issues"]] == [("R66", ["3", "4", "5"])]
    assert result["summary"]["byCode"] == {"D02": 1, "unknown": 1}
    assert "vertices" not in json.dumps(result)


def test_r64_main_says_what_is_missing_instead_of_raising(tmp_path, capsys):
    assert cad_analysis.main([str(tmp_path / "out"), "--cad", str(tmp_path / "nothing")]) == 2
    assert "no plots.json" in capsys.readouterr().err
