"""R353. The project position file of button 1 and its check points, python/position_file.py, on made up key reads, key points, baseplan
faces and location plan numbers built in code (R10). dotnet/SiteAdaptation.Tests/PositionFileTests.cs holds the C# side on the same made
up inputs, on net48 and net8.0."""
import csv
import json
import math

import pytest

import face_index
import key_rules
import parity
import position_file
import project_profile
from test_read_steps import made_settings

SITE_RULES = {"rule": "oneSet", "keys": 7, "oneSet": True, "angleToTrueNorthDeg": 0, "activeSite": "Internal", "sites": ["Internal"],
              "levels": ["Level 1", "Level 2"], "levelStepsM": {"Level 2": 4.0}, "units": {"system": "METRIC", "length": "meters", "lengthSystem": "METRIC"},
              "levelBase": "Survey Point", "lengthAccuracy": {"imc": 0.01, "imd": 0.01}}
GROUP = ["imc", "ima", "imb", "imd", "ime", "imf", "img"]
# The copy of each made up answer key, its reads named <copy stem>_TEST-<job>
COPIES = {key: "copy_" + key for key in GROUP}


def model(key, job=None):
    return COPIES[key] + "_TEST-" + (job or "R" + key[2].upper())


def made_read(angle=0.0, base="Survey Point", survey_off=(0.0, 0.0, 0.0), key="imc"):
    """A key read of R311 of the key: the internal origin at shared (1000, 2000, 5) turned by angle, the survey point and the project base
    point on the internal origin, two levels 4 m apart on one Elevation Base."""
    turn = math.radians(angle)
    shared = lambda x, y, z: [math.cos(turn) * x - math.sin(turn) * y + 1000.0, math.sin(turn) * x + math.cos(turn) * y + 2000.0, z + 5.0]
    point = {"position": [0.0, 0.0, 0.0], "sharedPosition": [a + b for a, b in zip(shared(0.0, 0.0, 0.0), survey_off)]}
    return {"tool": "RevitPrototypeReader", "lengthUnit": "m", "angleUnit": "deg", "documents": [{
        "file": model(key), "kind": "project", "surveyPoint": point, "basePoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)},
        "angleToTrueNorthDeg": angle,
        "site": {"activeSite": "Internal", "sites": ["Internal"], "placeName": "x", "projectPosition": {"eastWest": 1000.0, "northSouth": 2000.0, "elevation": 5.0, "angleDeg": angle},
                 "levels": [{"name": "Level 2", "elementId": 2, "elevation": 9.0, "projectElevation": 4.0, "elevationBase": "Survey Point"},
                            {"name": "Level 1", "elementId": 1, "elevation": 5.0, "projectElevation": 0.0, "elevationBase": base}],
                 "units": {"system": "METRIC", "length": "meters", "lengthSystem": "METRIC", "lengthAccuracy": 0.01}}}]}


def documents(change=None):
    """The made up reads of the 7 keys as documents, change(key, document) applied to each."""
    found = {}
    for key in GROUP:
        document = position_file.document_of(made_read(key=key))
        if change:
            change(key, document)
        found[key] = document
    return found


# The Generic floors of a made up key, internal corners of squares of 10 m: plot 2001 at the internal origin, 2002 20 m east, 2003 20 m
# north, 2004 a yard floor, 2005 two Generic floors, and a Generic floor of no plot 80 m east, which is never an extreme plot
FLOORS = [("2001", "Generic 150mm", 0.0, 0.0), ("2002", "Generic 150mm", 20.0, 0.0), ("2003", "Generic 150mm", 0.0, 20.0), ("2004", "yard", 40.0, 40.0),
          ("2005", "Generic 150mm", -30.0, -30.0), ("2005", "Generic 150mm", -30.0, -30.0), (None, "Generic 150mm", 80.0, 0.0)]
# The location plan numbers of the made up faces: 2001 to 2003 on the faces 101 to 103 (labels.csv)
NUMBERS = {"2001": "101", "2002": "102", "2003": "103"}


def made_points(angle=0.0, key="imc"):
    """testhost_<code>_points.json of a made up key read at full precision, the corners in internal and shared coordinates by made_read."""
    turn = math.radians(angle)
    shared = lambda x, y, z: [math.cos(turn) * x - math.sin(turn) * y + 1000.0, math.sin(turn) * x + math.cos(turn) * y + 2000.0, z + 5.0]
    floors = []
    for index, (plot, kind, x0, y0) in enumerate(FLOORS):
        corners = [[x0, y0, 0.15], [x0 + 10.0, y0, 0.15], [x0 + 10.0, y0 + 10.0, 0.15], [x0, y0 + 10.0, 0.15]]
        floors.append({"elementId": 100 + index, "typeName": kind, "plot": plot, "corners": corners, "sharedCorners": [shared(*corner) for corner in corners]})
    return {"format": "site-adaptation-key-points", "formatVersion": 1, "model": model(key, "K" + key[2].upper()) + ".rvt", "lengthUnit": "m", "angleUnit": "deg",
            "projectPosition": {"eastWest": 1000.0, "northSouth": 2000.0, "elevation": 5.0, "angleDeg": angle},
            "surveyPoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)}, "basePoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)},
            "floors": floors}


def made_plots(scale=1.0, east=0.0, angle=0.0, labels=("101", "102", "103"), merges=None):
    """The baseplan faces of the made up floors in site metres, labels 101 to 103, each vertex scaled by scale about the origin and moved
    east, as a baseplan drawn in feet or offset would give them, with the merge of each vertex of CadReader --exact, 0 unless merges names
    the merges of a label."""
    turn = math.radians(angle)
    found = []
    for label, (_, _, x0, y0) in zip(labels, FLOORS[:3]):
        ring = [[x0, y0], [x0 + 10.0, y0], [x0 + 10.0, y0 + 10.0], [x0, y0 + 10.0]]
        site = [[(math.cos(turn) * x - math.sin(turn) * y + 1000.0) * scale + east, (math.sin(turn) * x + math.cos(turn) * y + 2000.0) * scale] for x, y in ring]
        found.append({"kind": "plot_shape", "plot": label, "vertices": site, "type": "DP1", "vertexMergeM": list((merges or {}).get(label) or [0.0] * len(site))})
    return found


TYPES = {key: ["Generic 150mm"] for key in GROUP}


def numbered(numbers):
    """A numbering of the build by a table of the faces of each number, as position_file.numbering gives it: the faces of the number."""
    return lambda key, plot: [numbers[plot]] if isinstance(numbers.get(plot), str) else list(numbers.get(plot) or [])


def made_check(position, plots, points=None, numbering=None, keys=("imc",)):
    by_key = {key: (points or made_points(key=key)) for key in keys}
    return position_file.check_points(position, by_key, GROUP, face_index.rings_of(plots), TYPES, numbering if numbering is not None else numbered(NUMBERS), 1.0, 2,
                                      position_file.merges_of(plots))


def test_r353_the_position_file_is_the_site_of_a_key_read_and_holds_every_field_of_key_rules_site():
    document = position_file.document_of(made_read())
    position = position_file.position_of(document, "imc")
    assert position["format"] == "site-adaptation-position" and position["key"] == "imc" and position["model"] == model("imc")
    assert position["projectPosition"] == {"eastM": 1000.0, "northM": 2000.0, "elevationM": 5.0, "angleDeg": 0.0}
    assert position["surveyPoint"] == {"internalM": [0.0, 0.0, 0.0], "sharedM": [1000.0, 2000.0, 5.0]}
    assert [level["name"] for level in position["levels"]] == ["Level 1", "Level 2"] and position["levelBase"] == "Survey Point"
    assert (position["activeSite"], position["sites"], position["placeName"], position["lengthAccuracy"]) == ("Internal", ["Internal"], "x", 0.01)
    assert position_file.against_rules(position, SITE_RULES) == []
    for change, field in ((dict(levelStepsM={"Level 2": 4.001}), "levelStepsM"), (dict(levelBase="Project Base Point"), "levelBase"),
                          (dict(activeSite="Site 2"), "activeSite"), (dict(sites=["Internal", "Site 2"]), "sites"),
                          (dict(units=dict(SITE_RULES["units"], length="feet")), "units"), (dict(lengthAccuracy={"imc": 0.001}), "lengthAccuracy"),
                          (dict(oneSet=False), "oneSet"), (dict(angleToTrueNorthDeg=0.5), "angleToTrueNorthDeg")):
        found = position_file.against_rules(position, dict(SITE_RULES, **change))
        assert len(found) == 1 and field in found[0], (field, found)
    written = position_file.written(position)
    assert written["projectPosition"]["eastM"] == 1000.0 and written["activeSite"] == "Internal" and written["sites"] == ["Internal"]


def test_r353_a_key_read_with_no_site_a_part_not_read_or_two_level_bases_is_refused():
    read = made_read()
    with pytest.raises(position_file.PositionError, match="Elevation Bases"):
        position_file.position_of(position_file.document_of(made_read(base="Project Base Point")), "imc")
    no_site = json.loads(json.dumps(read))
    del no_site["documents"][0]["site"]
    with pytest.raises(position_file.PositionError, match="no site facts"):
        position_file.position_of(position_file.document_of(no_site), "imc")
    not_read = json.loads(json.dumps(read))
    not_read["documents"][0]["site"]["levels"] = None
    not_read["documents"][0]["site"]["notRead"] = {"levels": "an error"}
    with pytest.raises(position_file.PositionError, match="no levels"):
        position_file.position_of(position_file.document_of(not_read), "imc")
    feet = dict(read, lengthUnit="ft")
    with pytest.raises(position_file.PositionError, match="m and deg"):
        position_file.document_of(feet)
    nan = json.loads(json.dumps(read))
    nan["documents"][0]["surveyPoint"]["position"] = [0.0, float("nan"), 0.0]
    with pytest.raises(position_file.PositionError, match="surveyPoint.position"):
        position_file.position_of(position_file.document_of(nan), "imc")


def test_r353_the_reference_key_is_held_against_every_other_key_of_its_group_and_a_set_that_disagrees_is_refused():
    assert position_file.set_differs(documents(), "imc", GROUP, COPIES) == []
    # The Native set: the read of IMB with its survey point and project position 2432 km off the Parsons one in shared coordinates
    def native(key, document):
        if key == "imb":
            document["surveyPoint"]["sharedPosition"][0] += 2432000.0
            document["basePoint"]["sharedPosition"][0] += 2432000.0
            document["site"]["projectPosition"]["eastWest"] += 2432000.0
    found = position_file.set_differs(documents(native), "imc", GROUP, COPIES)
    assert found and all(line.startswith("imb ") for line in found)
    assert any("surveyPoint.sharedPosition 2432000000" in line for line in found) and any("projectPosition" in line for line in found)
    # A level 1 mm off and another site name are each named, a key of the group not given too
    def level(key, document):
        if key == "imd":
            document["site"]["levels"][0]["elevation"] = 9.001
            document["site"]["activeSite"] = "Site 2"
    assert sorted(position_file.set_differs(documents(level), "imc", GROUP, COPIES)) == ["imd activeSite Site 2", "imd levels.Level 2 1.0 mm"]
    missing = documents()
    del missing["img"]
    assert position_file.set_differs(missing, "imc", GROUP, COPIES) == ["img no read, every key of the group is held against the reference"]


def test_r353_every_value_of_the_set_missing_or_not_read_in_a_key_read_is_refused_naming_the_key_and_the_field():
    # Inputs: the probe of the breaker of PR 54, a read of no site, no survey point, no base point and no angle, a read whose project
    # position is not read, one with no eastWest, one with no levels and no units, and the IMC read given for IMA. Each is named by key
    # and field with the same lines as ProjectPositionFile.SetDiffers
    def bare(key, document):
        if key == "ima":
            for name in ("site", "surveyPoint", "basePoint", "angleToTrueNorthDeg"):
                del document[name]
    assert position_file.set_differs(documents(bare), "imc", GROUP, COPIES) == [
        "ima surveyPoint.position missing", "ima surveyPoint.sharedPosition missing", "ima basePoint.position missing", "ima basePoint.sharedPosition missing",
        "ima angleToTrueNorthDeg missing", "ima site missing"]
    def unread(key, document):
        if key == "ima":
            document["site"]["projectPosition"] = None
            document["site"]["notRead"] = {"projectPosition": "an error"}
        if key == "imb":
            del document["site"]["projectPosition"]["eastWest"]
        if key == "imd":
            document["site"]["levels"] = []
            document["site"]["units"] = {}
            del document["site"]["placeName"]
    assert position_file.set_differs(documents(unread), "imc", GROUP, COPIES) == [
        "ima site.projectPosition not read", "imb site.projectPosition.eastWest missing", "imd site.levels missing", "imd site.placeName missing",
        "imd site.units.system missing", "imd site.units.length missing", "imd site.units.lengthSystem missing", "imd site.units.lengthAccuracy missing"]
    def imc_for_ima(key, document):
        if key == "ima":
            document["file"] = model("imc")
    assert position_file.set_differs(documents(imc_for_ima), "imc", GROUP, COPIES) == [
        "ima model %s is not a work copy copy_ima_TEST-<job> of its answer key" % model("imc")]


def test_r353_the_check_points_hold_the_baseplan_faces_against_the_floor_corners_of_the_keys_through_the_position():
    for angle in (0.0, 30.0):
        position = position_file.position_of(position_file.document_of(made_read(angle)), "imc")
        rows = made_check(position, made_plots(angle=angle), made_points(angle))
        # The extreme plots by the mean of their corners, 2001 least east and north, 2002 most east, 2003 most north, the floor of no plot
        # never taken, four corners each, and the 6 other keys of the group with no key points listed apart
        taken = {name: row for name, row in rows.items() if row["plot"] is not None}
        assert sorted({(row["key"], row["plot"], row["face"], row["expectedFace"]) for row in taken.values()}) == [
            ("imc", "2001", "101", "101"), ("imc", "2002", "102", "102"), ("imc", "2003", "103", "103")]
        assert list(rows)[:4] == ["imc/2001/0", "imc/2001/1", "imc/2001/2", "imc/2001/3"] and len(taken) == 12
        assert all(row["gapMm"] < 1e-6 and row["revitGapMm"] < 1e-6 and row["pass"] and row["why"] is None for row in taken.values())
        assert [(name, row["why"], row["pass"]) for name, row in rows.items() if row["plot"] is None] == [(key, "no key points", False) for key in GROUP[1:]]
        assert taken["imc/2002/1"]["keyInternalM"] == [30.0, 0.0] and taken["imc/2002/1"]["internalM"] == pytest.approx([30.0, 0.0])
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    # A baseplan 0.6 mm and 1.2 mm east of the keys: every corner by that much, a pass and a fail at 1 mm
    for off, why in ((0.0006, None), (0.0012, "gap over the limit")):
        rows = made_check(position, made_plots(east=off), keys=GROUP)
        assert all(row["gapMm"] == pytest.approx(off * 1000.0) and row["why"] == why and row["pass"] is (why is None) for row in rows.values())
    # A baseplan drawn in feet, one 1000 km off and one in mm with a header in metres: no face holds a floor of the keys, every point fails
    for plots in (made_plots(scale=1 / 0.3048), made_plots(east=1000000.0), made_plots(scale=1000.0)):
        rows = made_check(position, plots, keys=GROUP)
        assert len(rows) == 84 and all(row["face"] is None and row["gapMm"] is None and row["why"] == "no face" and row["pass"] is False for row in rows.values())
    with pytest.raises(position_file.PositionError, match="no Generic floor"):
        made_check(position, made_plots(), dict(made_points(), floors=[]))


def test_r353_a_point_on_the_face_of_another_plot_fails_whatever_its_gap():
    # Input: the probe of the breaker of PR 54, a row of identical plots and a baseplan whose faces and labels lie one plot east of the keys:
    # every corner lands on a vertex at 0 mm, on the face of the plot east of its own by the location plan numbers
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    floors = []
    for index, x0 in enumerate([0.0, 10.0, 20.0, 30.0]):
        corners = [[x0, 0.0, 0.15], [x0 + 10.0, 0.0, 0.15], [x0 + 10.0, 10.0, 0.15], [x0, 10.0, 0.15]]
        floors.append({"elementId": index, "typeName": "Generic 150mm", "plot": str(2001 + index), "corners": corners,
                       "sharedCorners": [[x + 1000.0, y + 2000.0, z + 5.0] for x, y, z in corners]})
    plots = [{"kind": "plot_shape", "plot": str(100 + index), "vertices": [[x + 1010.0, y + 2000.0] for x, y in ((x0, 0.0), (x0 + 10.0, 0.0), (x0 + 10.0, 10.0), (x0, 10.0))]}
             for index, x0 in enumerate([-10.0, 0.0, 10.0, 20.0, 30.0, 40.0])]
    numbers = {"2001": "101", "2002": "102", "2003": "103", "2004": "104"}
    rows = made_check(position, plots, dict(made_points(), floors=floors), numbered(numbers))
    taken = [row for row in rows.values() if row["plot"] is not None]
    assert len(taken) == 8 and all(row["gapMm"] < 1e-6 and row["why"] == "face of another plot" and row["pass"] is False for row in taken)
    assert {(row["plot"], row["face"], row["expectedFace"]) for row in taken} == {("2001", "100", "101"), ("2004", "103", "104")}
    summary = position_file.summary_of(rows, 1.0)
    assert (summary["points"], summary["pass"], summary["otherFace"], summary["noPoints"], summary["failed"]) == (14, 0, 8, 6, True)
    # A plot the location plan numbers no face of is listed apart, the shift of config/plot_numbers.json numbers a key it names
    rows = made_check(position, made_plots(), numbering=lambda key, plot: [])
    assert {row["why"] for row in rows.values() if row["plot"] is not None} == {"no face numbered"}
    shifts = position_file.numbering([], {"IMC": 10})
    assert shifts("imc", "111") == ["101"] and shifts("ima", "111") == []
    assert position_file.shifts_of({"neighbourhoods": {"IMC": {"shift": 10}}}) == {"IMC": 10}
    with pytest.raises(position_file.PositionError, match="IMC a shift that is no whole number"):
        position_file.shifts_of({"neighbourhoods": {"IMC": {"shift": 10.0}}})


def test_r353_only_a_plot_label_numbers_a_face_and_a_number_on_two_faces_fails_its_point_never_the_shift():
    # Inputs: labels.csv rows of the client code kind and of the other kinds, open space, land use and substation, a number on two faces of
    # client codes, a number with an empty face, and the shift of IMC
    labels = [{"number": "2001", "kind": "clientCode", "face": "101"}, {"number": "2001", "kind": "clientCode", "face": "101"},
              {"number": "2002", "kind": "openSpace", "face": "102"}, {"number": "2004", "kind": "landUse", "face": "104"}, {"number": "2005", "kind": "substation", "face": "105"},
              {"number": "2006", "kind": "clientCode", "face": ""}, {"number": "2003", "kind": "clientCode", "face": "7"}, {"number": "2003", "kind": "clientCode", "face": "8"}]
    face_of = position_file.numbering(labels, {"IMC": 10})
    assert [face_of("ima", number) for number in ("2001", "2002", "2004", "2005", "2006", "2003")] == [["101"], [], [], [], [], ["7", "8"]]
    assert [face_of("imc", number) for number in ("2002", "2006", "2003")] == [["1992"], ["1996"], ["7", "8"]]
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    rows = made_check(position, made_plots(), numbering=numbered({"2001": ["101", "102"], "2002": "102", "2003": "103"}))
    two = {name: row for name, row in rows.items() if row["plot"] == "2001"}
    assert len(two) == 4 and all(row["why"] == "number on two faces" and row["numberFaces"] == ["101", "102"] and row["expectedFace"] is None and row["pass"] is False
                                 for row in two.values())
    assert all(row["numberFaces"] is None for row in rows.values() if row["plot"] != "2001")
    summary = position_file.summary_of(rows, 1.0)
    assert (summary["twoFaces"], summary["pass"], summary["failed"]) == (4, 8, True)
    assert "imc/2001/0 number on two faces 101, 102, gap 0.0 mm" in position_file.issues_of(rows, 2)


def test_r353_a_check_point_on_a_vertex_merged_more_than_the_limit_fails_with_its_merge():
    # Input: the faces of the made up baseplan with the vertices of face 101 merged by CadReader 0, 1.2 mm, 0.4 mm and 0: the corner on the
    # vertex moved 1.2 mm fails whatever its gap, the one moved 0.4 mm passes with its merge written
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    rows = made_check(position, made_plots(merges={"101": [0.0, 0.0012, 0.0004, 0.0]}))
    assert [(rows["imc/2001/%d" % k]["mergeMm"], rows["imc/2001/%d" % k]["why"]) for k in range(4)] == [
        (0.0, None), (pytest.approx(1.2), "vertex merged over the limit"), (pytest.approx(0.4), None), (0.0, None)]
    assert rows["imc/2001/1"]["gapMm"] < 1e-6 and position_file.summary_of(rows, 1.0)["merged"] == 1
    assert all(row["mergeMm"] is None for row in rows.values() if row["plot"] is None)
    assert position_file.merges_of(made_plots(merges={"101": [0.0, 0.0012, 0.0004, 0.0]}))["101"] == [[0.0, 0.0012, 0.0004, 0.0]]


def test_r353_the_exact_plots_are_of_the_baseplan_config_names_and_hold_the_merge_of_each_vertex(tmp_path):
    # Inputs: plots_exact.json of the baseplan of drawings.baseplan, of a reference only drawing, of another drawing, with no merges, and in feet
    settings = project_profile.load_settings()
    baseplan = settings.get("drawings")["baseplan"].replace("\\", "/").split("/")[-1]
    reference = settings.get("drawings")["referenceOnly"][0]
    folder = tmp_path / "exact"
    folder.mkdir()
    layers = folder / "layers.json"
    path = folder / "plots_exact.json"
    def write(source, plots, units="Meters"):
        layers.write_text(json.dumps({"units": units, "layers": []}), encoding="utf-8")
        path.write_text(json.dumps({"source": source, "units": units, "plots": plots}), encoding="utf-8")
        return path
    assert len(position_file.exact_plots(write(baseplan, made_plots()), settings)) == 3
    with pytest.raises(project_profile.ProfileError, match="reference only"):
        position_file.exact_plots(write("Copy of " + reference, made_plots()), settings)
    with pytest.raises(position_file.PositionError, match="drawings.baseplan"):
        position_file.exact_plots(write("other.dwg", made_plots()), settings)
    stale = made_plots()
    del stale[1]["vertexMergeM"]
    with pytest.raises(position_file.PositionError, match="vertexMergeM"):
        position_file.exact_plots(write(baseplan, stale), settings)
    with pytest.raises(Exception, match="Feet"):
        position_file.exact_plots(write(baseplan, made_plots(), units="Feet"), settings)


def test_r353_a_not_read_that_is_no_object_is_refused():
    # Inputs: the reference read with notRead written as a list naming a part and as a text, and another key read with notRead as a list
    for value, kind in ((["projectPosition"], "array"), ("levels could not be read", "string"), (3, "number")):
        read = made_read()
        read["documents"][0]["site"]["notRead"] = value
        with pytest.raises(position_file.PositionError, match="notRead as a JSON %s" % kind):
            position_file.position_of(position_file.document_of(read), "imc")
    def listed(key, document):
        if key == "ima":
            document["site"]["notRead"] = ["levels"]
    assert position_file.set_differs(documents(listed), "imc", GROUP, COPIES) == ["ima site.notRead is no object"]
    read = made_read()
    read["documents"][0]["site"]["notRead"] = None
    assert position_file.position_of(position_file.document_of(read), "imc")["key"] == "imc"


def test_r353_the_key_points_are_held_to_their_key_and_to_the_position_file():
    # Inputs: the points of another model given as imc, the points of imc 5 km off the position file, and the points of imc as they are
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    assert position_file.points_faults(made_points(), "imc", COPIES, position) == []
    other = dict(made_points(), model=model("img", "KG") + ".rvt")
    assert position_file.points_faults(other, "imc", COPIES, position) == ["imc keyPoints model %s.rvt is not a work copy copy_imc_TEST-<job> of its answer key" % model("img", "KG")]
    far = made_points()
    far["projectPosition"] = dict(far["projectPosition"], eastWest=6000.0)
    assert position_file.points_faults(far, "imc", COPIES, position) == ["imc keyPoints projectPosition 5000000.0 mm off the position file"]
    turned = made_points()
    turned["projectPosition"] = dict(turned["projectPosition"], angleDeg=0.5)
    assert position_file.points_faults(turned, "imc", COPIES, position) == ["imc keyPoints projectPosition angle 0.5 deg off the position file"]
    assert position_file.answer_copies({"keys": [{"code": "IMC", "copy": "a.rvt"}]}, {"copies": [{"code": "IMC", "copy": "a.rvt"}]}) == {"imc": "a"}
    with pytest.raises(position_file.PositionError, match="IMC.*a.rvt.*b.rvt"):
        position_file.answer_copies({"keys": [{"code": "IMC", "copy": "a.rvt"}]}, {"copies": [{"code": "IMC", "copy": "b.rvt"}]})
    with pytest.raises(position_file.PositionError, match="no code or no copy"):
        position_file.answer_copies({"keys": [{"code": "IMC"}]}, {"copies": [{"code": "IMC"}]})
    # Two codes that name one copy, in either case of its name, would let the read or parameter file of IMA pass as IMB, the breaker's
    # case 7 of the third review
    for second in ("x.rvt", "X.RVT"):
        both = {"keys": [{"code": "IMA", "copy": "x.rvt"}, {"code": "IMB", "copy": second}]}
        with pytest.raises(position_file.PositionError, match="IMA and IMB.*one copy"):
            position_file.answer_copies(both, {"copies": both["keys"]})
    real = project_profile.load_settings().get("answerKeys")
    assert len(position_file.answer_copies(real, {"copies": real["keys"]})) == 7


def test_r353_key_rules_site_names_the_one_level_base_of_the_keys():
    one = position_file.document_of(made_read())
    two = json.loads(json.dumps(one))
    for level in two["site"]["levels"]:
        level["elevationBase"] = "Project Base Point"
    found = key_rules.site_facts({"imc": one, "imd": one}, "imc")
    assert found["shared"]["levelBase"] == "Survey Point" and found["oneSet"]
    found = key_rules.site_facts({"imc": one, "imd": two}, "imc")
    assert found["keys"]["imd"]["differ"]["levelBase"] == "Project Base Point" and not found["oneSet"]


def test_r353_config_names_the_position_file_its_limit_and_the_steps_of_its_parity():
    settings = project_profile.load_settings()
    block = position_file.settings_of(settings)
    assert block["checkPointLimitMm"] == 2.0 and block["revitCheckPointLimitMm"] == 0.1 and block["gapDecimals"] == 2 and block["file"].endswith(".json")
    steps = settings.get("addin")["parity"]["steps"]
    assert set(steps["position"]) == {"key", "projectPositionM", "angleDeg", "surveyInternalM", "surveySharedM", "baseInternalM", "baseSharedM", "levelNames",
                                      "levelElevationsM", "levelProjectElevationsM", "levelBase", "activeSite", "sites", "heldAgainst"}
    assert set(steps["checkPoints"]) == {"key", "plot", "face", "expectedFace", "numberFaces", "sharedM", "internalM", "keyInternalM", "keySharedM", "gapMm", "revitGapMm",
                                         "mergeMm", "pass", "why"}
    assert position_file.level_profile_site(settings)["levelBase"] == "Survey Point" and position_file.key_of(settings) == "imc"
    assert position_file.group_of(settings) == GROUP


def made_cli(tmp_path, values):
    """The inputs of the command line on made up reads: the reads of the 7 keys named as the work copies of the answer keys of config, a
    key_copies.json that lists the same copies, the exact baseplan plots with its layers.json in Meters, labels.csv and plot_numbers.json."""
    copies = {entry["code"].lower(): entry["copy"][:-4] for entry in values["answerKeys"]["keys"]}
    manifest = tmp_path / "key_copies.json"
    manifest.write_text(json.dumps({"copies": [{"code": entry["code"], "copy": entry["copy"]} for entry in values["answerKeys"]["keys"]]}), encoding="utf-8")
    reads = {}
    for key in GROUP:
        read = made_read(key=key)
        read["documents"][0]["file"] = copies[key] + "_TEST-R" + key[2].upper()
        reads[key] = tmp_path / ("read_%s.json" % key)
        reads[key].write_text(json.dumps(read), encoding="utf-8")
    points = made_points()
    points["model"] = copies["imc"] + "_TEST-KC.rvt"
    (tmp_path / "points.json").write_text(json.dumps(points), encoding="utf-8")
    exact = tmp_path / "exact"
    exact.mkdir()
    (exact / "layers.json").write_text(json.dumps({"units": "Meters", "layers": []}), encoding="utf-8")
    source = values["drawings"]["baseplan"].replace("\\", "/").split("/")[-1]
    (exact / "plots_exact.json").write_text(json.dumps({"source": source, "units": "Meters", "plots": made_plots()}), encoding="utf-8")
    with open(tmp_path / "labels.csv", "w", encoding="utf-8", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["number", "kind", "face"])
        writer.writerows((number, "clientCode", face) for number, face in sorted(NUMBERS.items()))
    (tmp_path / "plot_numbers.json").write_text(json.dumps({"neighbourhoods": {"IMC": {"shift": 10}}}), encoding="utf-8")
    base = ["--key-read", str(reads["imc"]), "--key-copies", str(manifest)]
    for key in GROUP[1:]:
        base += ["--key-reads", "%s=%s" % (key, reads[key])]
    check = ["--key-points", "imc=%s" % (tmp_path / "points.json"), "--baseplan-exact", str(exact / "plots_exact.json"), "--labels", str(tmp_path / "labels.csv"),
             "--plot-shifts", str(tmp_path / "plot_numbers.json")]
    return reads, base, check


def test_r353_the_cli_writes_the_file_outside_git_the_position_step_and_the_check_points_and_a_failed_check_is_an_issue(tmp_path, capsys):
    path, values = made_settings(tmp_path)
    reads, base, check = made_cli(tmp_path, values)
    code = position_file.main(["--settings", str(path)] + base + check + ["--out", str(tmp_path / "out"), "--steps", str(tmp_path / "steps")])
    out = capsys.readouterr()
    assert code == 0, out.err
    written = json.loads((tmp_path / "out" / values["addin"]["position"]["file"].replace("\\", "/").split("/")[-1]).read_text(encoding="utf-8"))
    assert written["levelBase"] == "Survey Point" and written["projectPosition"]["northM"] == 2000.0 and written["key"] == "imc" and written["activeSite"] == "Internal"
    # The check of the 6 keys with no points failed: the file records it, every failed point is an issue line, the run ends 0 as R347 has it
    assert written["checkPoints"] == {"limitMm": 2.0, "points": 18, "pass": 12, "overLimit": 0, "noFace": 0, "otherFace": 0, "noNumber": 0, "twoFaces": 0, "merged": 0,
                                      "noPoints": 6, "failed": True}
    assert "the check points failed: 12 of 18 pass at 2.0 mm" in out.out and "issue ima no key points" in out.out
    step = parity.load_step(tmp_path / "steps" / parity.step_name("position", "python"), "python")
    assert step["plots"]["position"]["raw"]["levelNames"] == ["Level 1", "Level 2"] and step["plots"]["position"]["raw"]["heldAgainst"] == GROUP[1:]
    assert {"config", "keyRead", "rules", "keyCopies"} | {"keyRead:" + key for key in GROUP[1:]} <= {item["role"] for item in step["run"]["inputs"]}
    checked = parity.load_step(tmp_path / "steps" / parity.step_name("checkPoints", "python"), "python")
    assert len(checked["plots"]) == 18 and sum(1 for item in checked["plots"].values() if item["raw"]["pass"]) == 12
    assert {"keyPoints:imc", "baseplanExact", "labels", "plotShifts"} <= {item["role"] for item in checked["run"]["inputs"]}
    # A set with a key left out and the IMC read given for IMA are refused, nothing written
    assert position_file.main(["--settings", str(path)] + base[:-2] + ["--steps", str(tmp_path / "steps")]) == 2
    assert "img no read" in capsys.readouterr().err
    assert not (tmp_path / "steps" / parity.step_name("position", "python")).exists()
    swapped = [item.replace(str(reads["ima"]), str(reads["imc"])) for item in base]
    assert position_file.main(["--settings", str(path)] + swapped + ["--steps", str(tmp_path / "steps")]) == 2
    assert "ima model" in capsys.readouterr().err
    other = made_read()
    other["documents"][0]["site"]["levels"][0]["elevation"] = 9.5
    other["documents"][0]["file"] = json.loads(reads["imc"].read_text(encoding="utf-8"))["documents"][0]["file"]
    reads["imc"].write_text(json.dumps(other), encoding="utf-8")
    assert position_file.main(["--settings", str(path)] + base + ["--steps", str(tmp_path / "steps")]) == 2
    assert "levelStepsM" in capsys.readouterr().err


def test_r353_the_check_points_take_the_areas_tolerance_as_the_build_holds_it(tmp_path, capsys):
    # H2 of the fifth review of PR 55: the check points took addin.parity.kinds.areas raw, so a NaN refused no face of two overlapping
    # rings, where the build, the read and the C# position command refuse the tolerance itself with one line
    path, values = made_settings(tmp_path)
    values["addin"]["parity"]["kinds"]["areas"] = float("nan")
    path.write_text(json.dumps(values), encoding="utf-8")
    reads, base, check = made_cli(tmp_path, values)
    assert position_file.main(["--settings", str(path)] + base + check + ["--out", str(tmp_path / "out")]) == 2
    assert "addin.parity.kinds.areas is NaN, a tolerance is a finite number of zero or more, or null for a kind compared exactly (R345)" in capsys.readouterr().err
    assert not (tmp_path / "out").exists()


def test_r353_the_cli_refuses_a_baseplan_with_no_key_points_and_says_when_the_check_points_were_not_made(tmp_path, capsys):
    # Inputs: the baseplan and labels with no key points, as the worker refuses them, and a run that names neither, whose file is written
    # with an issue that the check points were not made, as R387 lists a check that cannot be made
    path, values = made_settings(tmp_path)
    reads, base, check = made_cli(tmp_path, values)
    assert position_file.main(["--settings", str(path)] + base + check[2:] + ["--out", str(tmp_path / "out")]) == 2
    assert "the request names one of them" in capsys.readouterr().err and not (tmp_path / "out").exists()
    assert position_file.main(["--settings", str(path)] + base + ["--out", str(tmp_path / "out")]) == 0
    out = capsys.readouterr().out
    assert "issue " + position_file.NOT_MADE in out and "checkPoints" not in json.loads(next((tmp_path / "out").iterdir()).read_text(encoding="utf-8"))
