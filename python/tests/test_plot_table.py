"""R352. The plot table of button 2, python/plot_table.py, on made up faces, labels, schedules, blocks and area keys built in code (R10).
dotnet/SiteAdaptation.Tests/PlotTableTests.cs holds the C# side on the same made up data, on net48 and net8.0."""
import json

import pytest
from shapely.geometry import Polygon

import parity
import plot_table
import project_profile
import read_steps
from test_read_steps import made_settings


def plot(label, x0, y0, x1, y1, kind="DP1"):
    return {"kind": "plot_shape", "plot": label, "vertices": [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], "type": kind}


def block(name, x0, y0, x1, y1, rotation=None, mirrored=None):
    found = {"name": name, "outline": [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], "insertPoint": [x0, y0, 0.0]}
    if rotation is not None:
        found["rotationDeg"] = rotation
    if mirrored is not None:
        found["mirrored"] = mirrored
    return found


# Face 102 is drawn as two rings of one label, 103 crosses the border and holds a label of a face with many labels, 104 lies outside it,
# 105 is a parking face whose first two bays lie 2E-11 m2 apart and are one overlap at 4 decimals, 107 touches the border from inside
PLOTS = [plot("101", 0, 0, 20, 20), plot("102", 20, 0, 30, 20, "TH1"), plot("102", 30, 0, 40, 20, "TH1"), plot("103", 40, 0, 60, 20, "VL3"),
         plot("104", 60, 0, 80, 20), plot("105", 0, 20, 20, 40, "C10"), plot("106", 20, 20, 40, 40, "DP2"), plot("107", 40, 20, 55, 40, "DP3"),
         {"kind": "text_only", "reason": "many_labels", "labels": ["103", "999"]}]
BORDER = [(-5.0, -5.0), (55.0, -5.0), (55.0, 45.0), (-5.0, 45.0)]
SCHEDULES = {("IMC", "111"), ("IMC", "112"), ("IMC", "113"), ("IMD", "114"), ("IMD", "116")}
SHIFTS = {"IMC": 10, "IMD": 11}
BAY = 14.9994505
BLOCKS = [block("A$DP1", 2, 2, 18, 18, 10.0, False), block("DT1", 15, 2, 25, 18, 30.0, False), block("B$TH1", 26, 2, 39, 18, 20.0, True),
          block("PARKA", 2, 22, 3, 22 + BAY - 1e-11, 90.0, False), block("PARKB", 5, 22, 6, 22 + BAY + 1e-11, 270.0, True),
          block("PARKC", 8, 22, 9, 22 + BAY - 5e-5, 0.0, False), block("SMALL", 39.5, 30, 49.5, 31)]


def label(number, code, east, north, new_code=None, rev9=None):
    return {"number": number, "code": code, "newCode": new_code, "rev9": rev9, "east": east, "north": north}


LABELS = [label("2003", "DP1 (QP)", 25.0, 10.0, rev9="DP1"), label("2001", "DP1", 10.0, 10.0, "DP1", "DP1"), label("2002", "TH1", 35.0, 10.0, "TH1"),
          label("2004", "DP1", 100.0, 100.0), label("2005", "VL3", 45.0, 10.0), label("2006", "DP2", 25.0, 30.0), label("2007", "DP2", 35.0, 30.0),
          label("2008", "DP3", 47.0, 30.0)]
DLUS = {"2001": ("Zone 2", "012"), "2002": ("Zone 2", "012"), "2003": ("Zone 2", "012"), "2006": ("Zone 2", "012"), "2007": ("Zone 3", "014"),
        "2008": ("Zone 2", "012")}
SALES = {"2001": ("Zone 2", "012"), "2002": ("Zone 2", "013")}


def made_table(blocks=None):
    rows = plot_table.table(PLOTS, BORDER, SCHEDULES, SHIFTS, LABELS, BLOCKS if blocks is None else blocks, 0.1, DLUS, SALES, 4)
    return {row["baseplanLabel"]: row for row in rows}


def test_r352_the_plot_table_of_made_up_faces_gives_every_column_as_data_book_wires_it():
    rows = made_table()
    assert list(rows) == ["101", "102", "103", "105", "106", "107"]
    assert all(list(row) == plot_table.COLUMNS for row in rows.values())
    first = rows["101"]
    assert first["centroid"] == [pytest.approx(10.0), pytest.approx(10.0)] and first["faceAreaM2"] == pytest.approx(400.0)
    assert (first["officialNumber"], first["neighbourhood"], first["twoNeighbourhoods"]) == ("111", "IMC", False)
    assert (first["locationPlanNumber"], first["locationPlanCode"], first["locationPlanNewCode"], first["typologyRev9"]) == ("2001", "DP1", "DP1", "DP1")
    assert (first["footprintBlock"], first["footprintRotationDeg"], first["footprintMirrored"], first["footprintShared"]) == ("DP1|DT1", 10.0, False, False)
    assert (first["zone"], first["nbhId"], first["areaKeyMethod"], first["areaKeyCheck"]) == ("Zone 2", "012", "locationPlanNumber", "same")
    two = rows["102"]
    assert two["duplicateLabel"] and two["clientType"] == "TH1" and two["faceAreaM2"] == pytest.approx(400.0)
    assert (two["locationPlanNumber"], two["locationPlanCode"], two["locationPlanNewCode"], two["typologyRev9"]) == ("2002|2003", "TH1|DP1 (QP)", "TH1", "DP1")
    assert (two["footprintBlock"], two["footprintRotationDeg"], two["footprintMirrored"], two["footprintShared"]) == ("TH1|DT1", 20.0, True, False)
    assert two["areaKeyCheck"] == "differs"
    crossing = rows["103"]
    assert crossing["crossesBorder"] and crossing["inManyLabelFace"] and crossing["twoNeighbourhoods"] and crossing["officialNumber"] is None
    assert crossing["areaKeyMethod"] == "noRow" and crossing["locationPlanNewCode"] is None and crossing["footprintBlock"] is None
    assert crossing["footprintShared"] is None and crossing["zone"] is None
    parking = rows["105"]
    assert (parking["officialNumber"], parking["neighbourhood"], parking["areaKeyMethod"]) == ("116", "IMD", "noNumber")
    assert rows["106"]["areaKeyMethod"] == "twoValues" and rows["106"]["footprintBlock"] is None
    touching = rows["107"]
    assert not touching["crossesBorder"] and touching["areaKeyCheck"] == "noSalesRow"
    assert (touching["footprintBlock"], touching["footprintRotationDeg"], touching["footprintMirrored"]) == ("SMALL", None, None)


def probe_bays():
    """The bays of the breaker's probe of PR 54 on a mm grid at site coordinates, made in code from whole mm so both languages hold the
    same doubles: A 1.001 by 0.999 m, 0.999999 m2, and B 1.000 by 1.000 m, 1E-6 m2 apart, the areas tolerance of 4d, on a face of 15 m."""
    found = []
    for k in range(200):
        x0, y0 = (500000000 + (k * 7919003) % 3000000) / 1000.0, (2400000000 + (k * 6271009) % 3000000) / 1000.0
        found.append(([(x0 - 5, y0 - 5), (x0 + 10, y0 - 5), (x0 + 10, y0 + 10), (x0 - 5, y0 + 10)],
                      [(x0, y0), (x0 + 1.001, y0), (x0 + 1.001, y0 + 0.999), (x0, y0 + 0.999)],
                      [(x0 + 3, y0), (x0 + 4.0, y0), (x0 + 4.0, y0 + 1.0), (x0 + 3, y0 + 1.0)]))
    return found


# Four turned bays of the probe, 2.5 by 5.0 m at mm, whose overlaps lie 1E-6 m2 apart within 2E-9: shapely put two by size and two by read
# order, the C# 2 of 8 otherwise
TURNED = [
    ([(500106.568, 2400080.837), (500124.46, 2400089.775), (500115.522, 2400107.667), (500097.63, 2400098.729)],
     [(500111.568, 2400085.837), (500113.804, 2400086.954), (500111.57, 2400091.427), (500109.333, 2400090.31)],
     [(500115.146, 2400087.625), (500117.383, 2400088.742), (500115.148, 2400093.215), (500112.912, 2400092.097)]),
    ([(501628.12, 2400954.942), (501648.12, 2400954.945), (501648.117, 2400974.945), (501628.117, 2400974.942)],
     [(501633.12, 2400959.942), (501635.62, 2400959.942), (501635.619, 2400964.942), (501633.119, 2400964.942)],
     [(501637.12, 2400959.942), (501639.62, 2400959.943), (501639.619, 2400964.943), (501637.119, 2400964.942)]),
    ([(502350.972, 2402695.553), (502370.972, 2402695.557), (502370.968, 2402715.557), (502350.968, 2402715.553)],
     [(502355.972, 2402700.553), (502358.472, 2402700.553), (502358.471, 2402705.553), (502355.971, 2402705.553)],
     [(502359.972, 2402700.553), (502362.472, 2402700.554), (502362.471, 2402705.554), (502359.971, 2402705.553)]),
    ([(500507.875, 2401453.471), (500525.766, 2401462.41), (500516.828, 2401480.301), (500498.936, 2401471.363)],
     [(500512.875, 2401458.471), (500515.111, 2401459.589), (500512.877, 2401464.061), (500510.64, 2401462.944)],
     [(500516.453, 2401460.259), (500518.69, 2401461.376), (500516.455, 2401465.849), (500514.219, 2401464.732)]),
]


def test_r352_blocks_of_one_overlap_as_r385_writes_it_keep_the_read_order_and_binary_noise_never_parts_them():
    # PARKB lies 2E-11 m2 over PARKA, one overlap at 4 decimals, so they keep the order of the read, then PARKC 5E-5 m2 smaller
    parking = made_table()["105"]
    assert (parking["footprintBlock"], parking["footprintRotationDeg"], parking["footprintMirrored"]) == ("PARKA|PARKB|PARKC", 90.0, False)
    assert plot_table.block_order({7: 5.0, 3: 5.0 + 9e-7, 4: 5.0 - 9e-7, 9: 7.0}, 4) == [9, 3, 4, 7]
    assert plot_table.block_order({1: 2.0, 5: 2.0 + 2e-6}, 4) == [1, 5]
    assert plot_table.block_order({1: 2.0, 5: 2.0 + 2e-4}, 4) == [5, 1]
    assert plot_table.block_order({0: 1.0, 1: 1.0 - 6e-7, 2: 1.0 - 1.2e-6, 3: 1.0 - 1.8e-6}, 4) == [0, 1, 2, 3]
    assert plot_table.block_order({}, 4) == []
    # The bays of the probe 1E-6 m2 apart went by size or by read order on the binary noise of shapely, 265 and 135 of 400, and the C#
    # 2 of 8 turned bays otherwise. At 4 decimals each pair is one overlap and keeps the read order
    for face, a, b in probe_bays() + TURNED:
        overlaps = {0: Polygon(face).intersection(Polygon(a)).area, 1: Polygon(face).intersection(Polygon(b)).area}
        assert 0 < abs(overlaps[1] - overlaps[0]) < 1.01e-6
        assert plot_table.block_order(overlaps, 4) == [0, 1]


def test_r352_the_decimals_of_the_block_order_are_addin_plot_table_and_never_a_parity_setting():
    values = project_profile.load_settings().values
    assert plot_table.overlap_decimals(values) == values["addin"]["plotTable"]["overlapDecimals"] == 4
    assert "overlapDecimals" not in json.dumps(values["addin"]["parity"])
    for bad in (None, "4", 4.0, True, -1, 11):
        changed = json.loads(json.dumps(values))
        if bad is None:
            del changed["addin"]["plotTable"]
        else:
            changed["addin"]["plotTable"]["overlapDecimals"] = bad
        with pytest.raises(ValueError, match="addin.plotTable.overlapDecimals"):
            plot_table.overlap_decimals(changed)


def test_r352_a_plot_table_step_with_no_addin_plot_table_is_refused_before_any_read(tmp_path, capsys):
    path, values = made_settings(tmp_path)
    values["addin"].pop("plotTable", None)
    path.write_text(json.dumps(values), encoding="utf-8")
    pdf, border, dlus = tmp_path / "plan.pdf", tmp_path / "phase1_border.csv", tmp_path / "dlus.xlsx"
    pdf.write_bytes(b"never read")
    border.write_text("x,y\n0,0\n10,0\n10,10\n", encoding="utf-8")
    code = read_steps.main(["--settings", str(path), "--steps", str(tmp_path / "steps"), "--only", "plotTable", "--plot-numbers", str(pdf),
                            "--sales", str(tmp_path / "sales.xlsx"), "--border", str(border), "--dlus", str(dlus)])
    assert code == 2
    assert "addin.plotTable.overlapDecimals" in capsys.readouterr().err


def test_r352_a_footprint_block_that_is_not_a_simple_ring_is_refused():
    bow_tie = {"name": "BOW", "outline": [[2, 2], [8, 8], [8, 2], [2, 8]], "insertPoint": [2, 2, 0.0], "rotationDeg": 0.0, "mirrored": False}
    with pytest.raises(ValueError, match="BOW.*not a simple ring"):
        made_table(BLOCKS + [bow_tie])


def test_r352_the_plot_table_step_needs_the_border_and_the_dlus_workbook_before_any_read(tmp_path, capsys):
    path, _ = made_settings(tmp_path)
    pdf = tmp_path / "plan.pdf"
    pdf.write_bytes(b"never read")
    code = read_steps.main(["--settings", str(path), "--steps", str(tmp_path / "steps"), "--only", "plotTable", "--plot-numbers", str(pdf),
                            "--sales", str(tmp_path / "sales.xlsx")])
    assert code == 2
    assert "plotTable needs --border and --dlus" in capsys.readouterr().err
    assert not (tmp_path / "steps" / parity.step_name("plotTable", "python")).exists()
