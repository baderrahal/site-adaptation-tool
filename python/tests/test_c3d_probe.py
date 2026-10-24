import json
import subprocess

import numpy as np
import pytest
from shapely.geometry import LineString, Polygon

import c3d_check
import c3d_connect
import c3d_drawings
import c3d_inventory
import c3d_run
import c3d_surfaces
import surface_lookup


def config_for(tmp_path):
    work = tmp_path / "probe" / "work"
    (work / "Roads").mkdir(parents=True)
    (tmp_path / "source" / "Roads").mkdir(parents=True)
    return {"probeRoot": str(tmp_path / "probe"), "sourceRoot": str(tmp_path / "source"), "accoreconsole": "accoreconsole.exe",
            "product": "C3D", "language": "en-US", "probeDll": str(tmp_path / "probe.dll"), "timeoutSeconds": 5}


class FakeProcess:
    def __init__(self, text=b"C3DPROBE dump ok in 1 s: classes 2", code=0, pid=4242):
        self.text, self.returncode, self.pid, self.waited, self.killed = text, code, pid, False, False

    def communicate(self, timeout=None):
        return self.text, None

    def poll(self):
        return None

    def wait(self, timeout=None):
        self.waited = True

    def kill(self):
        self.killed = True


def square_surface(size=10.0, slope=0.01):
    xyz = [(0, 0, 0), (size, 0, size * slope), (size, size, size * slope), (0, size, 0)]
    return surface_lookup.Surface(xyz, [(0, 1, 2), (0, 2, 3)])


# R151 runner

def test_r151_refuses_the_source_folder_and_a_path_outside_work(tmp_path):
    config = config_for(tmp_path)
    source = tmp_path / "source" / "Roads" / "a.dwg"
    source.write_bytes(b"x")
    assert "source folder" in c3d_run.refusal(config, source)
    elsewhere = tmp_path / "a.dwg"
    elsewhere.write_bytes(b"x")
    assert "not under" in c3d_run.refusal(config, elsewhere)
    assert "missing" in c3d_run.refusal(config, tmp_path / "probe" / "work" / "Roads" / "none.dwg")


def test_r151_script_refuses_unknown_commands_writes_and_command_calls():
    assert c3d_run.script_text("p.dll", ["C3DDUMP"]).splitlines() == ["NETLOAD", '"p.dll"', "C3DDUMP"]
    with pytest.raises(ValueError):
        c3d_run.script_text("p.dll", ["QSAVE"])
    with pytest.raises(ValueError):
        c3d_run.lisp_text('(command "_.QSAVE")')
    with pytest.raises(ValueError):
        c3d_run.lisp_text("(princ)\nSAVEAS")
    assert c3d_run.lisp_text("(princ)").endswith("\n")


def test_r151_run_records_pid_hash_and_probe_lines_without_touching_the_copy(tmp_path):
    config = config_for(tmp_path)
    drawing = tmp_path / "probe" / "work" / "Roads" / "a.dwg"
    drawing.write_bytes(b"drawing bytes")
    seen = {}

    def launcher(args, env):
        seen["args"], seen["env"] = args, env
        return FakeProcess()

    record = c3d_run.run(config, drawing, ["C3DDUMP"], launcher=launcher)
    assert record["pid"] == 4242 and record["exit"] == 0 and record["unchanged"] and not record["timedOut"]
    assert record["probeLines"] == ["C3DPROBE dump ok in 1 s: classes 2"]
    assert seen["args"][seen["args"].index("/product") + 1] == "C3D"
    assert seen["env"]["C3DPROBE_WORK"] == str(tmp_path / "probe" / "work")
    assert drawing.read_bytes() == b"drawing bytes"


def test_r151_run_never_launches_on_a_refused_drawing(tmp_path):
    config = config_for(tmp_path)
    source = tmp_path / "source" / "Roads" / "a.dwg"
    source.write_bytes(b"x")
    with pytest.raises(ValueError):
        c3d_run.run(config, source, ["C3DDUMP"], launcher=lambda args, env: pytest.fail("launched"))


def test_r151_stop_own_kills_only_the_process_id_it_started(monkeypatch):
    calls = []
    monkeypatch.setattr(subprocess, "run", lambda args, **kw: calls.append(args))
    process = FakeProcess(pid=777)
    assert c3d_run.stop_own(process)
    assert calls == [["taskkill", "/PID", "777", "/T", "/F"]]
    assert process.waited


def test_r151_decode_reads_utf16_and_plain_text():
    assert c3d_run.decode("Command:".encode("utf-16-le")) == "Command:"
    assert c3d_run.decode(b"Command:") == "Command:"


# R153 LISP inventory

def test_r153_lisp_writes_to_the_given_file_with_forward_slashes():
    text = c3d_inventory.lisp_for(r"C:\out\roads.tsv")
    assert '(c3dinv "C:/out/roads.tsv")' in text
    c3d_run.lisp_text(text)


def test_r153_summary_counts_classes_civil_layers_z_and_3d_polylines():
    lines = [
        "AECC_TIN_SURFACE\tC-TOPO\tModel\t",
        "AECC_TIN_SURFACE\tC-TOPO\tModel\t",
        "LINE\tRD\tModel\t0.0000 0.0000",
        "LWPOLYLINE\tRD\tModel\t5.5000",
        "POLYLINE\tBRK\tModel\t8 170.1000 171.2000",
        "POLYLINE\tFLAT\tModel\t0 0.0000 0.0000",
        "INSERT\t0\tModel\tVilla_VL4",
        "VIEWPORT\t0\tLayout1\t",
    ]
    summary = c3d_inventory.summarize(lines)
    assert summary["entities"] == 8 and summary["isCivil"]
    assert summary["civil"] == {"AECC_TIN_SURFACE": 2}
    assert summary["layers"]["C-TOPO"] == {"AECC_TIN_SURFACE": 2}
    assert summary["polylines3dWithZ"] == {"BRK": 1}
    assert summary["zRange"]["BRK"]["POLYLINE3D"] == [170.1, 171.2]
    assert summary["zRange"]["RD"] == {"LINE": [0.0, 0.0], "LWPOLYLINE": [5.5, 5.5]}
    assert "FLAT" not in summary["zRange"]
    assert summary["blocks"] == {"Villa_VL4": 1}
    assert summary["spaces"] == {"Model": 7, "Layout1": 1}


# R154 drawing survey

SURVEY = {"levelWords": ["FFL", "EL"], "levelRange": [150, 320], "levelDecimals": 2,
          "lineRoles": {"curb": ["*CURB*"], "rightOfWay": ["ROW"]}}


def test_r154_level_text_needs_a_level_word_or_a_number_in_range():
    assert c3d_drawings.is_level_text("FFL", SURVEY)
    assert c3d_drawings.is_level_text("TOP 172.45", SURVEY)
    assert not c3d_drawings.is_level_text("172.4", SURVEY)
    assert not c3d_drawings.is_level_text("PLOT 2311", SURVEY)
    assert not c3d_drawings.is_level_text("12.50", SURVEY)
    assert not c3d_drawings.is_level_text("ELEVATOR", SURVEY)


def test_r154_lengths_real_z_patterns_and_overlap():
    assert c3d_drawings.piece_length({"kind": "line", "pts": [[0, 0], [3, 4]]}) == 5
    assert c3d_drawings.piece_length({"kind": "polyline", "pts": [[0, 0], [1, 0], [1, 1]], "closed": True}) == pytest.approx(2 + 2 ** 0.5)
    arc = {"kind": "arc", "radius": 1, "center": [0, 0], "start": [1, 0], "end": [0, 1], "ccw": True}
    assert c3d_drawings.piece_length(arc) == pytest.approx(3.14159265 / 2)
    assert c3d_drawings.real_z({"kind": "polyline", "pts": [[0, 0, 170.5], [1, 0, 171]]}) == (170.5, 171)
    assert c3d_drawings.real_z({"kind": "polyline", "pts": [[0, 0, 0], [1, 0, 0]]}) is None
    assert c3d_drawings.matches("Road$0$RD-CURB-LINE", ["*curb*"])
    assert not c3d_drawings.matches("BROW", ["ROW"])
    assert c3d_drawings.overlap_share([0, 0, 5, 10], [0, 0, 10, 10]) == 0.5


def test_r154_survey_reads_the_reader_output_of_one_drawing(tmp_path):
    (tmp_path / "layers.json").write_text(json.dumps({"units": "Meters", "xrefs": [{"name": "X", "found": False}], "extents": {"core": [0, 0, 100, 100]},
                                                      "layers": [{"name": "RD-CURB", "entities": 1, "byType": {"LINE": 1}}, {"name": "EMPTY", "entities": 0, "byType": {}}]}))
    (tmp_path / "texts.json").write_text(json.dumps({"texts": [{"layer": "T", "text": "EL 171.25", "x": 1, "y": 1}, {"layer": "T", "text": "ROAD 12", "x": 2, "y": 2}]}))
    (tmp_path / "pieces.json").write_text(json.dumps({"pieces": [{"kind": "line", "layer": "RD-CURB", "pts": [[0, 0, 171], [10, 0, 171.5]]}]}))
    (tmp_path / "blocks.json").write_text(json.dumps({"blocks": [{"name": "Villa_VL4", "mirrored": True}]}))
    entry = c3d_drawings.survey_drawing(tmp_path, SURVEY, [0, 0, 50, 50])
    assert entry["layersUsed"] == 1 and entry["xrefs"] == [{"name": "X", "found": False}]
    assert entry["levelTexts"]["count"] == 1
    assert entry["lines3d"] == {"count": 1, "byLayer": {"RD-CURB": 1}, "zRange": {"RD-CURB": [171, 171.5]}}
    assert entry["roles"]["curb"]["pieces"] == 1 and entry["roles"]["curb"]["insideBaseplan"] == 1
    assert entry["blocks"]["mirrored"] == 1


# R155 surfaces from the LandXML

def test_r155_grid_overlap_border_and_seam():
    assert len(c3d_surfaces.grid_points([0, 0, 10, 10], [5, 5, 20, 20], 1)) == 25
    assert c3d_surfaces.grid_points([0, 0, 1, 1], [2, 2, 3, 3], 1) == []
    stats = c3d_surfaces.difference_stats([0.001, -0.004, 0.02, 0.1])
    assert stats["count"] == 4 and stats["worstAbs"] == 0.1 and stats["within5mm"] == 2 and stats["within30mm"] == 3
    assert c3d_surfaces.border_edges([(0, 1, 2), (0, 2, 3)]) == 4
    assert list(c3d_surfaces.border_vertices([(0, 1, 2), (0, 2, 3)])) == [0, 1, 2, 3]
    first = square_surface()
    result = c3d_surfaces.overlap(first, first, [0, 0, 10, 10], [0, 0, 10, 10], 5)
    assert result["count"] == 4 and result["worstAbs"] == 0
    neighbour = np.array([(10.001, 0, 0.1), (20, 0, 0.1), (10.001, 10, 0.1)])
    seam = c3d_surfaces.seam(neighbour, [(0, 1, 2)], first, 0.005, c3d_connect.snap_z)
    assert seam["count"] == 2 and seam["worstAbs"] == pytest.approx(0.0, abs=1e-6)


# R156 plot front against the lines and the surface edges

def test_r156_line_set_nearest_and_front_corners():
    lines = c3d_connect.LineSet([LineString([(0, -4), (10, -4)])])
    distance, foot = lines.nearest(5, 0)
    assert distance == pytest.approx(4) and foot == pytest.approx((5, -4))
    frame = {"left": (0, 0), "right": (10, 0), "r": (1, 0), "n": (0, 1)}
    ramp = {"corners": [(10, 5.5), (10, 0.001), (0, 5.5), (0, 0.001)], "levels": [171.0, 170.2, 171.1, 170.1]}
    assert c3d_connect.front_corners(ramp, frame) == [((0, 0.001), 170.1), ((10, 0.001), 170.2)]


def test_r156_reach_gives_sidewalk_width_and_where_the_surface_edge_lies():
    rows = [{"curb": 4.15, "lines": {"row": 0.0}, "surfaceEdge": {"S": 0.0002}, "frontCovered": {"S": 1}},
            {"curb": 4.15, "lines": {"row": 0.0}, "surfaceEdge": {"S": 0.0}, "frontCovered": {"S": 2}},
            {"curb": 4.0, "lines": {"row": 0.1}, "surfaceEdge": {"S": 4.0}, "frontCovered": {"S": 0}}]
    summary = c3d_connect.reach_summary(rows, {"row": None}, {"S": None}, "row")
    assert summary["sidewalk"]["medianWidth"] == pytest.approx(4.15)
    assert summary["lines"]["row"]["within0p5m"] == 3
    surface = summary["surfaces"]["S"]
    assert surface["bothFrontCornersCovered"] == 1 and surface["midpointCovered"] == 1
    assert surface["edgeInsideSidewalk"] == 1 and surface["edgeWithin0p5mOfCurb"] == 1


# R157 levels at the front corners

def test_r157_snap_takes_the_edge_level_only_within_the_tolerance():
    surface = square_surface()
    assert c3d_connect.snap_z(surface, 5, 5, 0.005) == pytest.approx(0.05)
    assert surface.z_at(10.002, 5) is None
    assert c3d_connect.snap_z(surface, 10.002, 5, 0.005) == pytest.approx(0.1)
    assert c3d_connect.snap_z(surface, 10.02, 5, 0.005) is None


def test_r157_bands_table_and_ranking():
    bands = [0.005, 0.030]
    assert c3d_connect.band_names(bands) == ["within5mm", "within30mm", "worse"]
    assert [c3d_connect.band_of(d, bands) for d in (0.004, -0.02, 0.05)] == ["within5mm", "within30mm", "worse"]
    rows = [{"corners": {"a": [0.001, 0.002], "b": [0.02, None]}, "entry": {"a": -0.05}},
            {"corners": {"a": [0.1, 0.0], "b": [0.01, 0.04]}, "entry": {}}]
    table = c3d_connect.level_table(rows, ["a", "b"], bands)
    assert table["a"]["corners"] == 4 and table["a"]["cornerBands"] == {"within5mm": 3, "worse": 1}
    assert table["a"]["plotsWithEntry"] == 1 and table["a"]["entryMedian"] == -0.05
    assert table["b"]["corners"] == 3
    assert [item["source"] for item in c3d_connect.ranking(table, bands)] == ["a", "b"]


def test_r157_level_text_and_3d_line_sources():
    assert c3d_connect.text_value("TOP 172.45 EL", SURVEY) == 172.45
    assert c3d_connect.text_value("FFL", SURVEY) is None
    texts = c3d_connect.PointLevels([(0, 0, 170.0), (100, 0, 180.0)])
    assert texts.z_at(3, 0, 10) == 170.0 and texts.z_at(50, 0, 10) is None
    lines = c3d_connect.Segments3d([(0, 0, 170, 10, 0, 171)])
    assert lines.z_at(5, 1, 5) == pytest.approx(170.5) and lines.z_at(5, 9, 5) is None
    assert c3d_connect.Segments3d([]).z_at(0, 0, 5) is None


# R158 coverage and the cut check

def test_r158_coverage_counts_fronts_each_source_gives_two_corners():
    face = Polygon([(0, 0), (10, 0), (10, 20), (0, 20)])
    curbs = c3d_connect.LineSet([LineString([(-5, -4), (15, -4)])])
    sources = {"all": lambda x, y: 170.0, "left": lambda x, y: 170.0 if x < 5 else None}
    result = c3d_connect.coverage({"A": (0, ["1", "2"])}, {0: {"1": face}}, curbs, sources, (5, 15))
    assert result["A"]["plots"] == 2 and result["A"]["fronts"] == 1 and result["A"]["noFront"] == 1
    assert result["A"]["share"] == {"all": 1.0, "left": 0.0}


def test_r158_cut_check_lists_the_blocks_on_the_cut_faces():
    def face(label, x0, kind):
        return {"kind": "plot_shape", "plot": label, "type": kind, "vertices": [[x0, 0], [x0 + 10, 0], [x0 + 10, 30], [x0, 30]]}
    plots = [face("2301", 0, "VL4"), face("2302", 10, "VL4"), face("326", 20, "OS")]
    blocks = [{"name": "Villa_VL4", "mirrored": True, "outline": [[1, 1], [9, 1], [9, 29], [1, 29]]},
              {"name": "Far", "outline": [[100, 100], [101, 100], [101, 101]]}]
    result = c3d_connect.cut_check(plots, {"2301": "VL4", "2302": "VL4", "326": "OS"}, blocks)
    assert all(entry["found"] for entry in result["labels"].values())
    assert [(hit["block"], hit["onLabels"]) for hit in result["blocks"]] == [("Villa_VL4", ["2301"])]


# R159 second method

def test_r159_brute_level_inside_and_on_the_edge():
    points = np.array([(0, 0, 0), (10, 0, 0.1), (10, 10, 0.1), (0, 10, 0)], dtype=float)
    faces = np.array([(0, 1, 2), (0, 2, 3)])
    assert c3d_check.brute_level(points, faces, 5, 5, 0.005) == pytest.approx(0.05)
    assert c3d_check.brute_level(points, faces, 10.003, 5, 0.005) == pytest.approx(0.1)
    assert c3d_check.brute_level(points, faces, 10.03, 5, 0.005) is None


def test_r159_segments_nearest_and_seeded_sample():
    pieces = [{"kind": "polyline", "layer": "X$0$RD-CURB-LINE", "pts": [[0, 0], [10, 0], [10, 10]]}, {"kind": "line", "layer": "OTHER", "pts": [[0, 5], [1, 5]]}]
    segments = c3d_check.segments_of(pieces, ["RD-CURB-LINE"])
    assert len(segments) == 2
    assert c3d_check.nearest_segment(segments, 5, 3) == pytest.approx(3)
    items = [{"plot": str(n)} for n in range(50)]
    assert c3d_check.sample(items, 14, 5) == c3d_check.sample(list(reversed(items)), 14, 5)
    assert len(c3d_check.sample(items, None, 5)) == 50


def test_r159_inventory_against_acadsharp_counts_by_class():
    lines = ["LINE\tA\tModel\t0 0", "LINE\tB\tModel\t0 0", "INSERT\tA\tModel\tX", "LINE\tA\tLayout1\t0 0"]
    layers = {"layers": [{"byType": {"LINE": 2}}, {"byType": {"INSERT": 2}}]}
    result = c3d_check.check_inventory(lines, layers)
    assert result["match"] == len(c3d_check.CLASS_NAMES) - 1
    assert result["mismatch"] == [{"class": "INSERT", "lisp": 1, "acadsharp": 2}]


def test_r159_level_and_curb_checks_count_matches():
    points = np.array([(0, 0, 0), (10, 0, 0.1), (10, 10, 0.1), (0, 10, 0)], dtype=float)
    faces = np.array([(0, 1, 2), (0, 2, 3)])
    rows = [{"plot": "1", "rampFront": [[5, 5, 0.04], [10.002, 5, 0.1]], "corners": {"snap:S": [0.01, 0.0]}, "curb": 4.0}]
    assert c3d_check.check_levels(rows, points, faces, "snap:S", 0.005) == {"match": 2}
    curbs = [(0, 1, 10, 1)]
    rows_of_way = [(0, 5, 20, 5)]
    assert c3d_check.check_curb(rows, curbs, rows_of_way) == {"curbMatch": 1, "rowOnFront": 1}
