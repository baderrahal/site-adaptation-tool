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
    # plan_from_book.py refuses the build before it reads the drawing, with exit 2 and the reason, the baseplan of the reads config names
    cover = tmp_path / "cover.json"
    cover.write_text(json.dumps({"neighbourhood": "N1", "plots": ["1"]}), encoding="utf-8")
    settings = tmp_path / "project.json"
    settings.write_text(json.dumps(read_set(tmp_path / "set", "Millimeters").values), encoding="utf-8")
    args = [str(cover), "--plots-csv", str(tmp_path / "plots.csv"), "--landxml", str(tmp_path), "--site", str(tmp_path / "site.json"),
            "--scratch", str(tmp_path / "scratch"), "--out", str(tmp_path / "plan.json"), "--settings", str(settings)]
    (tmp_path / "plots.csv").write_text("baseplanLabel,locationPlanNumber\n1,1\n", encoding="utf-8")
    assert plan_from_book.main(args) == 2 and not (tmp_path / "plan.json").exists()
    err = capsys.readouterr().err
    assert "Millimeters" in err and "R388" in err
    # data_book.py reads an earlier CadReader output folder only of a drawing in Meters
    book = {"root": str(tmp_path / "book"), "drawings": {"roads": "Roads/r.dwg"}}
    cad_output(tmp_path / "earlier" / "r.dwg", "Millimeters", "pieces.json", {"source": "r.dwg", "pieces": []})
    _, earlier = data_book.folder_of(book, "roads", [str(tmp_path / "earlier")])
    with pytest.raises(cad_units.UnitError, match="Millimeters"):
        data_book.lines_of(earlier, ["ROAD"])
    cad_output(tmp_path / "good" / "r.dwg", "Meters", "pieces.json", {"source": "r.dwg", "pieces": []})
    assert data_book.folder_of(book, "roads", [str(tmp_path / "good")])[1] == tmp_path / "good" / "r.dwg"


def baseplan_settings(folder):
    """A made up config file whose cadReads names folder as the read of the baseplan, so a build reads the plots.json made up in it (R388)."""
    import project_profile
    values = json.loads(json.dumps(project_profile.load_settings().values))
    values["cadReads"] = dict(values["cadReads"], root=str(folder.parent))
    values["drawings"] = dict(values["drawings"], baseplan=folder.name)
    path = folder.parent / (folder.name + "_project.json")
    path.write_text(json.dumps(values), encoding="utf-8")
    return path


def read_set(tmp_path, units="Meters"):
    """A made up config whose cadReads root holds the CadReader reads of the three drawings config names, each with its layers.json."""
    import project_profile
    values = json.loads(json.dumps(project_profile.load_settings().values))
    values["cadReads"] = dict(values["cadReads"], root=str(tmp_path / "reads"))
    drawings = values["drawings"]
    cad_output(tmp_path / "reads" / drawings["baseplan"], units, "plots.json", {"source": "b.dwg", "plots": [{"label": "1"}]})
    cad_output(tmp_path / "reads" / drawings["roadLayout"], units, "pieces.json", {"source": "r.dwg", "pieces": []})
    cad_output(tmp_path / "reads" / drawings["villaBlocks"], units, "blocks.json", {"source": "f.dwg", "blocks": []})
    return project_profile.ProjectProfile(project_profile.SETTINGS_PATH, values)


# R388 the measure and the build read the CadReader outputs config names, one set, through the units check
def test_r388_the_measure_and_the_build_read_the_one_set_of_cad_outputs_config_names(tmp_path):
    settings = read_set(tmp_path)
    drawings = settings.get("drawings")
    named = {role: cad_units.read_input(settings, role, None) for role in cad_units.ROLES}
    assert named == {"baseplan": tmp_path / "reads" / drawings["baseplan"] / "plots.json", "roads": tmp_path / "reads" / drawings["roadLayout"],
                     "footprintBlocks": tmp_path / "reads" / drawings["villaBlocks"] / "blocks.json"}
    # A copy of the same bytes, as the Round 17 snapshot copied the outputs alone, is one set with them, the file config names is read
    copy = tmp_path / "snapshot" / "plots.json"
    copy.parent.mkdir()
    copy.write_bytes(named["baseplan"].read_bytes())
    assert cad_units.read_input(settings, "baseplan", copy) == named["baseplan"]
    roads = tmp_path / "snapshot" / "roads"
    roads.mkdir()
    (roads / "pieces.json").write_bytes((named["roads"] / "pieces.json").read_bytes())
    assert cad_units.read_input(settings, "roads", roads) == named["roads"]
    # A file of other bytes is another set, refused with both named
    copy.write_text(json.dumps({"source": "b.dwg", "plots": [{"label": "2"}]}), encoding="utf-8")
    with pytest.raises(cad_units.UnitError, match="SHA-256") as error:
        cad_units.read_input(settings, "baseplan", copy)
    assert str(copy) in str(error.value) and drawings["baseplan"] in str(error.value)
    # The file config names goes through the units check of its read
    with pytest.raises(cad_units.UnitError, match="Feet"):
        cad_units.read_input(read_set(tmp_path / "feet", "Feet"), "footprintBlocks", None)


def test_r388_majority_and_plan_from_book_take_their_cad_outputs_from_the_one_place(tmp_path, monkeypatch):
    import majority
    import project_profile
    seen = []

    def read_input(settings, role, given):
        seen.append((role, given))
        raise cad_units.UnitError("stop at " + role)

    settings = read_set(tmp_path)
    monkeypatch.setattr(cad_units, "read_input", read_input)
    monkeypatch.setattr(majority.plan_from_book, "book_rows", lambda path: ({}, {}))
    monkeypatch.setattr(majority, "read_rows", lambda path, settings, block: [])
    monkeypatch.setattr(project_profile, "load_settings", lambda path=None: settings)
    assert majority.main(["t.csv", "--plots-csv", "p.csv", "--sales", "s.xlsx", "--out", str(tmp_path / "m.json"),
                          "--overrides", str(tmp_path / "o.json")]) == 2
    assert seen == [("baseplan", None)]
    seen.clear()
    from test_round_addin import run_book
    monkeypatch.undo()
    code, _ = run_book(tmp_path, monkeypatch, [], read_input)
    assert code == 2 and seen == [("baseplan", "b.json")]


def table(folder, data, blocks=True):
    """A made up data book table folder: data.json as given, one block and one text."""
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "data.json").write_text(json.dumps(data), encoding="utf-8")
    if blocks:
        (folder / "blocks.csv").write_text("name,x,y,rotationDeg,mirrored\nVilla_VL4,1.0,2.0,0,no\n", encoding="utf-8")
    (folder / "texts.csv").write_text("text,x,y,layer\n77,1.0,2.0,NUM\n", encoding="utf-8")
    return folder


# R388 a table of the data book is read only in Meters, by its data.json, or by the unit config names for a reader that writes none
def test_r388_a_data_book_table_is_read_in_meters_by_its_data_json_or_by_the_unit_config_names_for_its_reader(tmp_path):
    import project_profile
    settings = project_profile.load_settings()
    assert settings.get("tableUnits")["C3DProbe"] == "Meters"
    found, source = data_book.blocks_of(table(tmp_path / "stand_in", {"reader": "CadReader", "units": "Meters"}), None)
    assert [item["name"] for item in found] == ["Villa_VL4"] and source == "data book CadReader"
    with pytest.raises(cad_units.UnitError, match="Millimeters") as error:
        data_book.blocks_of(table(tmp_path / "mm", {"reader": "CadReader", "units": "Millimeters"}), None)
    assert "mm" in str(error.value)
    with pytest.raises(cad_units.UnitError, match="Millimeters"):
        data_book.texts_of(tmp_path / "mm", None)
    # The C3DProbe tables carry no units, config names Meters for them, a reader config names none of is refused
    probe = table(tmp_path / "probe", {"reader": "C3DProbe", "counts": {}})
    assert data_book.texts_of(probe, None)[0][0]["text"] == "77"
    assert data_book.blocks_of(table(tmp_path / "old", {"counts": {}}), None)[1] == "data book C3DProbe"
    with pytest.raises(cad_units.UnitError, match="OtherReader"):
        data_book.blocks_of(table(tmp_path / "other", {"reader": "OtherReader"}), None)
    values = json.loads(json.dumps(settings.values))
    del values["tableUnits"]["C3DProbe"]
    with pytest.raises(cad_units.UnitError, match="C3DProbe"):
        cad_units.check_table(probe, project_profile.ProjectProfile(settings.path, values))


# R388 data_book.py refuses only the folder it reads, never an earlier folder a table of the data book stands in for
def test_r388_folder_of_refuses_only_the_folder_that_is_read(tmp_path):
    book = {"root": str(tmp_path / "book"), "drawings": {"foot": "Base Plans/f.dwg"}}
    table(tmp_path / "book" / "f", {"reader": "CadReader", "units": "Meters"})
    cad_output(tmp_path / "earlier" / "f.dwg", "Millimeters", "pieces.json", {"source": "f.dwg", "pieces": []})
    cad_output(tmp_path / "earlier" / "f.dwg", "Millimeters", "blocks.json", {"source": "f.dwg", "blocks": []})
    cad_output(tmp_path / "earlier" / "f.dwg", "Millimeters", "texts.json", {"source": "f.dwg", "texts": []})
    data, earlier = data_book.folder_of(book, "foot", [str(tmp_path / "earlier")])
    assert data == tmp_path / "book" / "f" and earlier == tmp_path / "earlier" / "f.dwg"
    # The blocks come from the table of the data book, so the earlier folder in millimetres is not read and not refused
    assert data_book.blocks_of(data, earlier)[1] == "data book CadReader"
    # Each reader of the earlier folder refuses it
    for read in (lambda: data_book.blocks_of(None, earlier), lambda: data_book.texts_of(None, earlier), lambda: data_book.lines_of(earlier, ["A"]),
                 lambda: data_book.lines_matching(earlier, "A*")):
        with pytest.raises(cad_units.UnitError, match="Millimeters"):
            read()


# R388 the LandXML surfaces an earlier round read are read only of a file in meter, by the landxml_summary.json of that read
def test_r388_the_cached_surfaces_of_the_data_book_are_read_only_in_meter(tmp_path):
    path = tmp_path / "road.xml"
    path.write_text(landxml_text([("Road", ROAD, ROAD_FACES)]), encoding="utf-8")
    landxml_reader.read_landxml(path, tmp_path / "cached")
    connect, book = {"landxmlDir": str(tmp_path / "cached"), "surfaces": ["Road"]}, {"root": str(tmp_path / "book")}
    assert list(data_book.load_surfaces(connect, book, tmp_path / "scratch")) == ["Road"]
    summary = tmp_path / "cached" / "landxml_summary.json"
    text = summary.read_text(encoding="utf-8")
    for changed, why in ((text.replace('"meter"', '"foot"'), "foot"), (text.replace('"linearUnit": "meter"', '"linearUnit": null'), "None")):
        summary.write_text(changed, encoding="utf-8")
        with pytest.raises(landxml_reader.UnitError, match=why):
            data_book.load_surfaces(connect, book, tmp_path / "scratch")
    summary.write_text(text, encoding="utf-8")
    with pytest.raises(landxml_reader.UnitError, match="Other"):
        data_book.load_surfaces(dict(connect, surfaces=["Road", "Other"]), book, tmp_path / "scratch")
    summary.unlink()
    with pytest.raises(landxml_reader.UnitError, match="landxml_summary.json"):
        data_book.load_surfaces(connect, book, tmp_path / "scratch")
