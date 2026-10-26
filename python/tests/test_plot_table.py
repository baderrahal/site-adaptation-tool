"""R352. The plot table of button 2, python/plot_table.py, on made up faces, labels, schedules, blocks and area keys built in code (R10).
dotnet/SiteAdaptation.Tests/PlotTableTests.cs holds the C# side on the same made up data, on net48 and net8.0."""
import pytest

import parity
import plot_table
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
# 105 is a parking face whose first two bays lie 2E-11 m2 apart about a bucket edge of 1E-6 m2, 107 touches the border from inside
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
    rows = plot_table.table(PLOTS, BORDER, SCHEDULES, SHIFTS, LABELS, BLOCKS if blocks is None else blocks, 0.1, DLUS, SALES, 1e-6)
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


def test_r352_blocks_whose_overlaps_lie_within_the_areas_tolerance_of_the_largest_keep_the_read_order():
    # PARKB lies 2E-11 m2 over PARKA, both about the middle of a step of 1E-6 m2, so a rounding of the overlap in steps of the tolerance
    # would put PARKB first by binary noise. Within the tolerance of the largest they are one group in the order of the read
    parking = made_table()["105"]
    assert (parking["footprintBlock"], parking["footprintRotationDeg"], parking["footprintMirrored"]) == ("PARKA|PARKB|PARKC", 90.0, False)
    assert plot_table.block_order({7: 5.0, 3: 5.0 + 9e-7, 4: 5.0 - 9e-7, 9: 7.0}, 1e-6) == [9, 3, 7, 4]
    # A group is taken from the largest left, so a chain of small steps does not run on into one group
    assert plot_table.block_order({0: 1.0, 1: 1.0 - 6e-7, 2: 1.0 - 1.2e-6, 3: 1.0 - 1.8e-6}, 1e-6) == [0, 1, 2, 3]
    assert plot_table.block_order({2: 1.0 - 1.2e-6, 0: 1.0, 3: 1.0 - 1.8e-6, 1: 1.0 - 6e-7}, 1e-6) == [0, 1, 2, 3]
    assert plot_table.block_order({5: 2.0, 1: 2.0 + 2e-6}, 1e-6) == [1, 5]
    assert plot_table.block_order({}, 1e-6) == []


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
