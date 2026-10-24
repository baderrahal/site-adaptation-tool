import csv
import json
import zipfile
from pathlib import Path

import pytest
from shapely.geometry import box

import data4_check
import past_formulas
import project_profile
import rev9_parcels
import surface_entry
import surface_versions
import villa_hand

LIMITS = [0.005, 0.03]


def write_civil(path, parcels):
    with open(path, "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle, lineterminator="\n")
        writer.writerow(["kind", "name", "layer", "parent", "property", "value"])
        writer.writerow(["drawing", "d.dwg", "", "", "insunits", "Meters"])
        for number, style, ring in parcels:
            values = {"Area": "100", "Name": number, "Number": number, "StyleName": style, "outline": "|".join("%g %g" % p for p in ring)}
            for prop in sorted(values):
                writer.writerow(["parcel", number, "C-PROP", "block:MP-Base", prop, values[prop]])


def ring(x, y, size=10):
    return [(x, y), (x + size, y), (x + size, y + size), (x, y + size)]


# R242 the surface survey
def test_r242_copy_path_renames_the_first_folder_and_counts_surface_classes(tmp_path):
    copy = surface_versions.copy_path(tmp_path / "src" / "Plan Rev9" / "a.dwg", tmp_path / "src", tmp_path / "work", {"Plan Rev9": "Rev9"})
    assert copy == tmp_path / "work" / "Rev9" / "a.dwg"
    header = {"classes": [{"dxfName": "AECC_TIN_SURFACE", "instanceCount": 2}, {"dxfName": "AECC_PARCEL", "instanceCount": 9}]}
    assert surface_versions.surface_count(header, {"AECC_TIN_SURFACE"}) == (2, {"AECC_TIN_SURFACE": 2})


def test_r242_landxml_facts_read_the_date_and_surfaces(tmp_path):
    path = tmp_path / "s.xml"
    path.write_text('<?xml version="1.0"?><LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" date="2023-01-05"><Application name="C3D"/>'
                    '<Surfaces><Surface name="Top"/><Surface name="Old"/></Surfaces></LandXML>', encoding="utf-8")
    assert surface_versions.landxml_facts(path) == {"root": "LandXML", "date": "2023-01-05", "application": "C3D", "surfaces": ["Top", "Old"]}


def test_r242_survey_reads_every_dwg_by_its_copy_and_never_the_source(tmp_path):
    source, work = tmp_path / "src", tmp_path / "book" / "work"
    (source / "Roads").mkdir(parents=True)
    (source / "Roads" / "r.dwg").write_bytes(b"x")
    (source / "Roads" / "n.dwg").write_bytes(b"y")
    (work / "Roads").mkdir(parents=True)
    (work / "Roads" / "r.dwg").write_bytes(b"x")
    opened = []

    def reader(copy):
        opened.append(copy)
        return {"created": "2022-10-10 23:40", "updated": "2023-02-12 21:09", "classes": [{"dxfName": "AECC_TIN_SURFACE", "instanceCount": 3}]}
    config = {"sourceRoot": str(source), "dataBook": {"root": str(tmp_path / "book"), "surfaceVersions": {"suffixes": [".dwg"], "renames": {}, "surfaceClasses": ["AECC_TIN_SURFACE"]}}}
    report, rows = surface_versions.survey(config, reader)
    assert opened == [work / "Roads" / "r.dwg"] and report["withSurfaces"] == 1 and report["noCopy"] == 1
    assert [r["error"] for r in rows if r["file"].endswith("n.dwg")] == ["no working copy, not opened"]


# R243 and R244 the parcels
def test_r243_parcels_end_where_a_property_comes_again(tmp_path):
    write_civil(tmp_path / "civil.csv", [("12", "VL4", ring(0, 0)), ("13", "DT3", ring(20, 0))])
    parcels = rev9_parcels.read_parcels(tmp_path / "civil.csv")
    assert [p["name"] for p in parcels] == ["12", "13"] and parcels[1]["outline"][1] == (30.0, 0.0)
    plots = rev9_parcels.plot_parcels(parcels)
    assert [(p["number"], p["style"], round(p["shape"].area)) for p in plots] == [("12", "VL4", 100), ("13", "DT3", 100)]


def test_r244_the_plan_compare_and_the_redo_tell_real_from_pdf_read():
    plots = [{"number": "12", "style": "VL4", "shape": box(0, 0, 10, 10)}, {"number": "14", "style": "DT3", "shape": box(20, 0, 27, 10)}]
    matches = {"1": {"face": 0, "class": "same", "iou": 1.0, "areaDifferenceM2": 0.0}, "2": {"face": 1, "class": "shapeDiffers", "iou": 0.7, "areaDifferenceM2": -30.0}}
    row_of = {"1": {"locationPlanNumber": "12", "typologyRev9": "VL4"}, "2": {"locationPlanNumber": "13", "typologyRev9": "DT3"}}
    by_label = rev9_parcels.against_plan(matches, plots, row_of, None)
    assert by_label["1"]["numberClass"] == "same" and by_label["2"]["numberClass"] == "differs" and by_label["2"]["codeClass"] == "same"
    differing = [{"baseplanLabel": "1", "clientType": "VL4", "faceClass": "shapeDiffers", "numberClass": "same", "codeClass": "same", "unitTextClass": "same"},
                 {"baseplanLabel": "2", "clientType": "DP4", "faceClass": "shapeDiffers", "numberClass": "differs", "codeClass": "same", "unitTextClass": "differs"}]
    rows = rev9_parcels.redo(differing, by_label, {"2": [[20, 0], [27, 0], [27, 10], [20, 10]]}, {p["number"]: p for p in plots})
    assert rows[0]["verdicts"] == "shape pdfRead" and rows[1]["verdicts"] == "shape real|number real|dwgText"
    assert rows[1]["pdfAgainstParcelIou"] == 1.0


# R246 the villa hand
def frame(anchor, offset_x, width):
    return {"anchor": anchor, "offset": [offset_x, 0.15], "widthM": width}


def test_r246_share_from_the_start_corner_by_the_plot_frame():
    assert villa_hand.share(1.9, frame("end", -10.0, 12)) == 0.325 and villa_hand.share(10.1, frame("start", 0, 16)) == 0.631
    assert villa_hand.side(0.325) == "left" and villa_hand.side(0.631) == "right"


def test_r246_measure_takes_each_position_once_and_counts_points_off_the_plot():
    rooms_file = {"doors": [{"family": "KEO_GATE-SINGLE", "level": "GROUND FLOOR FFL", "point": [10.1, 8.8, 0]},
                            {"family": "KEO_GATE-SINGLE", "level": "GROUND FLOOR FFL", "point": [10.1, 8.8, 0]},
                            {"family": "KEO_DOOR_Main Door", "level": "GROUND FLOOR FFL", "point": [9.9, 14.2, 0], "toRoom": "Hall"},
                            {"family": "KEO_DOOR_Main Door", "level": "GROUND FLOOR FFL", "point": [4.0, 9.0, 0], "toRoom": "Hall", "fromRoom": "Majlis"}],
                  "rooms": [{"name": "Driver Room", "point": [15.0, 3.6, 0]}, {"name": "Driver Room", "point": [42.0, 2.1, 0]}]}
    settings = {"groundLevel": "GROUND", "gateFamily": "GATE", "entranceDoorFamily": "Main Door", "driverRoom": "Driver Room"}
    found = villa_hand.measure(rooms_file, frame("start", 0, 16), settings)
    assert len(found["gates"]) == 1 and found["gateSides"] == ["right"] and len(found["entranceDoors"]) == 1
    assert [r["share"] for r in found["driverRooms"]] == [0.938] and found["driverRoomsOffPlot"] == 1


def test_r246_verdict_other_hand_only_for_gates_on_opposite_sides():
    assert villa_hand.verdict({"A": {"gateSides": ["right"]}, "B": {"gateSides": ["left"]}}, "A", "B") == "other hand"
    assert villa_hand.verdict({"A": {"gateSides": ["left"]}, "B": {"gateSides": ["left"]}}, "A", "B") == "same hand"
    assert villa_hand.verdict({"A": {"gateSides": ["left", "right"]}, "B": {"gateSides": ["left"]}}, "A", "B") == "open"


# R247 the past formulas
def xlsx(path):
    sheet = ('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>'
             '<row r="4"><c r="B4" t="s"><v>3</v></c></row>'
             '<row r="39"><c r="B39" t="s"><v>0</v></c><c r="C39" t="s"><v>1</v></c><c r="D39" t="s"><v>2</v></c><c r="E39" t="s"><v>4</v></c></row>'
             '<row r="40"><c r="B40"><v>7</v></c><c r="C40"><v>180.3</v></c><c r="D40"><v>179.9</v></c><c r="E40"><f>Table137[[#This Row],[FFL]]-D40</f><v>0.4</v></c></row>'
             '<row r="41"><c r="B41"><v>8</v></c><c r="C41"><f>D41+0.3</f><v>180.1</v></c><c r="D41"><v>179.8</v></c><c r="E41"><f>Table137[[#This Row],[FFL]]-D41</f><v>0.3</v></c></row>'
             '</sheetData></worksheet>')
    strings = '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' + "".join("<si><t>%s</t></si>" % t for t in ("Plot No.", "FFL", "El @ Entry*", "FFL from the IFC", "Height**")) + "</sst>"
    table = ('<table xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" displayName="Table137" ref="B39:E41"><tableColumns>'
             '<tableColumn name="Plot No."/><tableColumn name="FFL"/><tableColumn name="El @ Entry*"/>'
             '<tableColumn name="Height**"><calculatedColumnFormula>Table137[[#This Row],[FFL]]-Table137[[#This Row],[El @ Entry*]]</calculatedColumnFormula></tableColumn>'
             '</tableColumns></table>')
    with zipfile.ZipFile(path, "w") as book:
        book.writestr("xl/worksheets/sheet1.xml", sheet)
        book.writestr("xl/sharedStrings.xml", strings)
        book.writestr("xl/tables/table1.xml", table)
    return path


def test_r247_typed_values_and_formulas_by_pattern_with_their_references(tmp_path):
    settings = {"table": "Table137", "plotColumn": "Plot No.", "columns": ["El @ Entry*", "FFL", "Height**"], "noteCells": ["B4"]}
    found = past_formulas.read_book(xlsx(tmp_path / "s.xlsx"), settings)
    assert found["rows"] == 2 and found["notes"] == ["FFL from the IFC"] and found["externalLinks"] == []
    assert found["columns"]["El @ Entry*"]["typed"] == 2 and found["columns"]["FFL"] == {**found["columns"]["FFL"], "typed": 1, "formula": 1}
    assert found["columns"]["FFL"]["patterns"] == {"R[0]C[1]+0.3": 1} and found["columns"]["FFL"]["references"] == {"El @ Entry* row +0": 1}
    assert found["columns"]["Height**"]["formula"] == 2 and found["calculatedColumns"]["Height**"].startswith("Table137")


def test_r247_a_shared_formula_moves_with_its_cell():
    assert past_formulas.shared_text("D40+$A$1", "C40", "C42") == "D42+$A$1"
    assert past_formulas.pattern("D42+$A$1", "C42") == "R[0]C[1]+R1C1"


# R248 and R249 El @ Entry by surface version
def test_r248_summary_bands_and_shape_fit_after_the_median():
    items = [{"surface": "s", "neighbourhood": "IMC", "difference": d} for d in (0.21, 0.212, 0.213, 0.5)] + [{"surface": "s", "neighbourhood": "IMD", "difference": None}]
    found = surface_entry.summarise(items, LIMITS)["s"]
    assert found["worse"] == 4 and found["noLevel"] == 1 and found["shapeFit"]["within5mm"] == 3
    assert found["byNeighbourhood"]["IMD"]["noLevel"] == 1 and surface_entry.ranked({"a": {"within5mm": 1, "within30mm": 9}, "b": {"within5mm": 3, "within30mm": 0}}) == ["b", "a"]


def test_r249_key_entry_from_the_ramp_corners_far_from_the_curb_less_the_datum():
    class Curbs:
        tree = True

        def nearest(self, x, y):
            return y, None
    ramp = [[0, 0, 165.0], [5, 0, 165.0], [5, 5.5, 165.1], [0, 5.5, 165.1]]
    assert surface_entry.key_entry(ramp, Curbs(), 164.9) == pytest.approx(0.2)
    profile = project_profile.ProjectProfile("dummy.json", {"units_mm": True, "ramp": {"front_setback": {"default": 5520, "C10": 5500}},
                                                            "slopes_percent": {"front_min": 0.5, "front_max": 2.0}})
    rows = {"7": {"typologyRev9": "DT3", "roadLevelLeft": "100.0", "roadLevelRight": "100.0"}}
    items = surface_entry.key_items({"7": 100.2276, "8": 100.0}, rows, profile, {"C10"}, ["roadLevel"], {"7": "022", "8": "02"}, "022", LIMITS)
    assert len(items) == 1 and items[0]["difference"] == pytest.approx(-0.2)


# R250 second method
def test_r250_raw_parcels_and_the_smallest_parcel_holds_the_centroid(tmp_path):
    write_civil(tmp_path / "civil.csv", [("12", "VL4", ring(0, 0)), ("3150", "ROW", ring(-50, -50, 200))])
    parcels = data4_check.raw_parcels(tmp_path / "civil.csv")
    assert [n for n, _ in parcels] == ["12", "3150"] and data4_check.shoelace(parcels[0][1]) == 100.0
    faces = {"1": [ring(0, 0)]}
    rows = [{"baseplanLabel": "1", "locationPlanNumber": "12"}]
    found = data4_check.check_parcels(rows, faces, parcels, {"1": {"number": "12", "faceClass": "same"}}, {"gridM": 0.5, "sameShareMin": 0.98, "matchShareMin": 0.5})
    assert found == {"parcel match": 1, "planNumber same": 1, "class match": 1}
