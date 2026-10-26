"""R388. The units of the inputs of the Python reference: a LandXML is read only in meter and a CadReader output only of a drawing in
Meters, each refused otherwise with the file and the unit named, never converted. Every file here is made up (R10)."""
import json

import pytest

import cad_units
import data_book
import landxml_reader
import plan_from_book
from landxml_fixture import ROAD, ROAD_FACES, landxml_text


def with_units(units):
    """The made up LandXML of the fixture with its Units element replaced by units."""
    return landxml_text([("Road", ROAD, ROAD_FACES)]).replace('<Units><Metric linearUnit="meter"></Metric></Units>', units)


def test_r388_a_landxml_in_feet_is_refused(tmp_path):
    for units, unit in (('<Units><Imperial linearUnit="foot"></Imperial></Units>', "foot"), ('<Units><Imperial linearUnit="USSurveyFoot"/></Units>', "USSurveyFoot"),
                        ('<Units><Metric linearUnit="millimeter"/></Units>', "millimeter")):
        path = tmp_path / "feet.xml"
        path.write_text(with_units(units), encoding="utf-8")
        with pytest.raises(landxml_reader.UnitError, match="R388") as error:
            landxml_reader.read_landxml(path, tmp_path / "out")
        assert "feet.xml" in str(error.value) and unit in str(error.value)
        # Refused before any surface is written, so no reader takes a surface of another unit
        assert not (tmp_path / "out").exists() or not any((tmp_path / "out").iterdir())
    # The command line ends with 2 and the reason, as a broken file ends with its error
    assert landxml_reader.main([str(path), str(tmp_path / "cli")]) == 2


def test_r388_a_landxml_with_no_unit_is_refused(tmp_path):
    for units, why in (("", "no Units"), ("<Units></Units>", "no linearUnit"), ('<Units><Metric areaUnit="squareMeter"/></Units>', "no linearUnit")):
        path = tmp_path / "none.xml"
        path.write_text(with_units(units), encoding="utf-8")
        with pytest.raises(landxml_reader.UnitError, match=why):
            landxml_reader.read_landxml(path, tmp_path / "out")
    # A file with no surface is refused as well, a file in meter reads as before
    path = tmp_path / "empty.xml"
    path.write_text('<?xml version="1.0"?><LandXML><Surfaces></Surfaces></LandXML>', encoding="utf-8")
    with pytest.raises(landxml_reader.UnitError, match="no Units"):
        landxml_reader.read_landxml(path, tmp_path / "out")
    path.write_text(with_units('<Units><Metric linearUnit="meter"/></Units>'), encoding="utf-8")
    assert landxml_reader.read_landxml(path, tmp_path / "meter")["linearUnit"] == "meter"


def cad_output(folder, units, name="plots.json", body=None):
    folder.mkdir(parents=True, exist_ok=True)
    if units is not None:
        (folder / "layers.json").write_text(json.dumps({"source": "a.dwg", "units": units, "layers": []}), encoding="utf-8")
    path = folder / name
    path.write_text(json.dumps(body or {"source": "a.dwg", "plots": []}), encoding="utf-8")
    return path


def test_r388_a_drawing_in_millimetres_is_refused(tmp_path, capsys):
    # The units of a CadReader output are the InsUnits of the header its read wrote in layers.json beside it
    assert cad_units.check(cad_output(tmp_path / "metres", "Meters")) == "Meters"
    with pytest.raises(cad_units.UnitError, match="Millimeters") as error:
        cad_units.check(cad_output(tmp_path / "mm", "Millimeters"))
    assert "plots.json" in str(error.value) and "R388" in str(error.value)
    with pytest.raises(cad_units.UnitError, match="no layers.json"):
        cad_units.check(cad_output(tmp_path / "copied", None))
    # A folder of pieces.json is held by its own layers.json
    with pytest.raises(cad_units.UnitError, match="Unitless"):
        cad_units.check((cad_output(tmp_path / "roads", "Unitless", "pieces.json", {"source": "r.dwg", "pieces": []})).parent)
    # plan_from_book.py refuses the build before it reads the drawing, with exit 2 and the reason
    cover = tmp_path / "cover.json"
    cover.write_text(json.dumps({"neighbourhood": "N1", "plots": ["1"]}), encoding="utf-8")
    baseplan = cad_output(tmp_path / "mm", "Millimeters")
    roads = cad_output(tmp_path / "metres_roads", "Meters", "pieces.json", {"source": "r.dwg", "pieces": []}).parent
    args = [str(cover), "--plots-csv", str(tmp_path / "plots.csv"), "--landxml", str(tmp_path), "--site", str(tmp_path / "site.json"), "--baseplan", str(baseplan),
            "--roads", str(roads), "--scratch", str(tmp_path / "scratch"), "--out", str(tmp_path / "plan.json")]
    (tmp_path / "plots.csv").write_text("baseplanLabel,locationPlanNumber\n1,1\n", encoding="utf-8")
    assert plan_from_book.main(args) == 2 and not (tmp_path / "plan.json").exists()
    err = capsys.readouterr().err
    assert "Millimeters" in err and "R388" in err
    # data_book.py takes an earlier CadReader output folder only of a drawing in Meters
    book = {"root": str(tmp_path / "book"), "drawings": {"roads": "Roads/r.dwg"}}
    cad_output(tmp_path / "earlier" / "r.dwg", "Millimeters", "pieces.json", {"source": "r.dwg", "pieces": []})
    with pytest.raises(cad_units.UnitError, match="Millimeters"):
        data_book.folder_of(book, "roads", [str(tmp_path / "earlier")])
    cad_output(tmp_path / "good" / "r.dwg", "Meters", "pieces.json", {"source": "r.dwg", "pieces": []})
    assert data_book.folder_of(book, "roads", [str(tmp_path / "good")])[1] == tmp_path / "good" / "r.dwg"
