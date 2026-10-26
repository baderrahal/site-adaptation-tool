"""R353. The project position file of button 1 and its check points, python/position_file.py, on made up key reads, key points and
baseplan faces built in code (R10). dotnet/SiteAdaptation.Tests/PositionFileTests.cs holds the C# side on the same made up reads, on
net48 and net8.0."""
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


def made_read(angle=0.0, base="Survey Point", survey_off=(0.0, 0.0, 0.0)):
    """A key read of R311: the internal origin at shared (1000, 2000, 5) turned by angle, the survey point and the project base point
    on the internal origin, two levels 4 m apart on one Elevation Base."""
    turn = math.radians(angle)
    shared = lambda x, y, z: [math.cos(turn) * x - math.sin(turn) * y + 1000.0, math.sin(turn) * x + math.cos(turn) * y + 2000.0, z + 5.0]
    point = {"position": [0.0, 0.0, 0.0], "sharedPosition": [a + b for a, b in zip(shared(0.0, 0.0, 0.0), survey_off)]}
    return {"tool": "RevitPrototypeReader", "lengthUnit": "m", "angleUnit": "deg", "documents": [{
        "file": "key_TEST-RC", "kind": "project", "surveyPoint": point, "basePoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)},
        "angleToTrueNorthDeg": angle,
        "site": {"activeSite": "Internal", "sites": ["Internal"], "placeName": "x", "projectPosition": {"eastWest": 1000.0, "northSouth": 2000.0, "elevation": 5.0, "angleDeg": angle},
                 "levels": [{"name": "Level 2", "elementId": 2, "elevation": 9.0, "projectElevation": 4.0, "elevationBase": "Survey Point"},
                            {"name": "Level 1", "elementId": 1, "elevation": 5.0, "projectElevation": 0.0, "elevationBase": base}],
                 "units": {"system": "METRIC", "length": "meters", "lengthSystem": "METRIC", "lengthAccuracy": 0.01}}}]}


# The Generic floors of a made up key, internal corners of squares of 10 m: plot 2001 at the internal origin, 2002 20 m east, 2003 20 m
# north, 2004 a yard floor, 2005 two Generic floors, so the extreme plots are 2001 (least east and north), 2002 and 2003
FLOORS = [("2001", "Generic 150mm", 0.0, 0.0), ("2002", "Generic 150mm", 20.0, 0.0), ("2003", "Generic 150mm", 0.0, 20.0), ("2004", "yard", 40.0, 40.0),
          ("2005", "Generic 150mm", -30.0, -30.0), ("2005", "Generic 150mm", -30.0, -30.0)]


def made_points(angle=0.0):
    """testhost_<code>_points.json of a made up key read at full precision, the corners in internal and shared coordinates by made_read."""
    turn = math.radians(angle)
    shared = lambda x, y, z: [math.cos(turn) * x - math.sin(turn) * y + 1000.0, math.sin(turn) * x + math.cos(turn) * y + 2000.0, z + 5.0]
    floors = []
    for index, (plot, kind, x0, y0) in enumerate(FLOORS):
        corners = [[x0, y0, 0.15], [x0 + 10.0, y0, 0.15], [x0 + 10.0, y0 + 10.0, 0.15], [x0, y0 + 10.0, 0.15]]
        floors.append({"elementId": 100 + index, "typeName": kind, "plot": plot, "corners": corners, "sharedCorners": [shared(*corner) for corner in corners]})
    return {"format": "site-adaptation-key-points", "formatVersion": 1, "model": "key_TEST-KC.rvt", "lengthUnit": "m", "angleUnit": "deg",
            "projectPosition": {"eastWest": 1000.0, "northSouth": 2000.0, "elevation": 5.0, "angleDeg": angle},
            "surveyPoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)}, "basePoint": {"position": [0.0, 0.0, 0.0], "sharedPosition": shared(0.0, 0.0, 0.0)},
            "floors": floors}


def made_plots(scale=1.0, east=0.0, angle=0.0):
    """The baseplan faces of the made up floors in site metres, label 101 to 103, each vertex scaled by scale about the origin and moved
    east, as a baseplan drawn in feet or offset would give them."""
    turn = math.radians(angle)
    found = []
    for label, (_, _, x0, y0) in zip(("101", "102", "103"), FLOORS[:3]):
        ring = [[x0, y0], [x0 + 10.0, y0], [x0 + 10.0, y0 + 10.0], [x0, y0 + 10.0]]
        site = [[(math.cos(turn) * x - math.sin(turn) * y + 1000.0) * scale + east, (math.sin(turn) * x + math.cos(turn) * y + 2000.0) * scale] for x, y in ring]
        found.append({"kind": "plot_shape", "plot": label, "vertices": site, "type": "DP1"})
    return found


TYPES = {"imc": ["Generic 150mm"]}


def made_check(position, plots, points=None):
    return position_file.check_points(position, {"imc": points or made_points()}, face_index.rings_of(plots), TYPES, 1.0, 2)


def test_r353_the_position_file_is_the_site_of_a_key_read_and_holds_every_field_of_key_rules_site():
    document = position_file.document_of(made_read())
    position = position_file.position_of(document, "imc")
    assert position["format"] == "site-adaptation-position" and position["key"] == "imc" and position["model"] == "key_TEST-RC"
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
    reference = position_file.document_of(made_read())
    others = {key: position_file.document_of(made_read()) for key in GROUP[1:]}
    assert position_file.set_differs(reference, others, "imc", GROUP) == []
    # The Native set: its survey point and project position 2432 km off the Parsons one in shared coordinates
    native = json.loads(json.dumps(made_read()))
    native["documents"][0]["surveyPoint"]["sharedPosition"][0] += 2432000.0
    native["documents"][0]["basePoint"]["sharedPosition"][0] += 2432000.0
    native["documents"][0]["site"]["projectPosition"]["eastWest"] += 2432000.0
    found = position_file.set_differs(reference, dict(others, imb=position_file.document_of(native)), "imc", GROUP)
    assert found and all(line.startswith("imb ") for line in found)
    assert any("surveyPoint.sharedPosition 2432000000" in line for line in found) and any("projectPosition" in line for line in found)
    # A level 1 mm off, another site name and a key of the group not given are each named
    level = json.loads(json.dumps(made_read()))
    level["documents"][0]["site"]["levels"][0]["elevation"] = 9.001
    level["documents"][0]["site"]["activeSite"] = "Site 2"
    found = position_file.set_differs(reference, dict(others, imd=position_file.document_of(level)), "imc", GROUP)
    assert sorted(found) == ["imd activeSite Site 2", "imd levels.Level 2 1.0 mm"]
    missing = dict(others)
    del missing["img"]
    assert position_file.set_differs(reference, missing, "imc", GROUP) == ["img no read, every key of the group is held against the reference"]


def test_r353_the_check_points_hold_the_baseplan_faces_against_the_floor_corners_of_the_keys_through_the_position():
    for angle in (0.0, 30.0):
        position = position_file.position_of(position_file.document_of(made_read(angle)), "imc")
        rows = made_check(position, made_plots(angle=angle), made_points(angle))
        # The extreme plots by the mean of their corners, 2001 least east and north, 2002 most east, 2003 most north, four corners each
        assert sorted({(row["key"], row["plot"], row["face"]) for row in rows.values()}) == [("imc", "2001", "101"), ("imc", "2002", "102"), ("imc", "2003", "103")]
        assert list(rows)[:4] == ["imc/2001/0", "imc/2001/1", "imc/2001/2", "imc/2001/3"] and len(rows) == 12
        assert all(row["gapMm"] < 1e-6 and row["revitGapMm"] < 1e-6 and row["pass"] for row in rows.values())
        assert rows["imc/2002/1"]["keyInternalM"] == [30.0, 0.0] and rows["imc/2002/1"]["internalM"] == pytest.approx([30.0, 0.0])
    position = position_file.position_of(position_file.document_of(made_read()), "imc")
    # A baseplan 0.6 mm and 1.2 mm east of the keys: every corner by that much, a pass and a fail at 1 mm
    for off, passes in ((0.0006, True), (0.0012, False)):
        rows = made_check(position, made_plots(east=off))
        assert all(row["gapMm"] == pytest.approx(off * 1000.0) and row["pass"] is passes for row in rows.values())
    # A baseplan drawn in feet and one 1000 km off: no face holds a floor of the keys, every check point fails
    for plots in (made_plots(scale=1 / 0.3048), made_plots(east=1000000.0)):
        rows = made_check(position, plots)
        assert len(rows) == 12 and all(row["face"] is None and row["gapMm"] is None and row["pass"] is False for row in rows.values())
    with pytest.raises(position_file.PositionError, match="no Generic floor"):
        position_file.check_points(position, {"imc": dict(made_points(), floors=[])}, face_index.rings_of(made_plots()), TYPES, 1.0, 2)


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
    assert block["checkPointLimitMm"] == 1.0 and block["gapDecimals"] == 2 and block["file"].endswith(".json")
    steps = settings.get("addin")["parity"]["steps"]
    assert set(steps["position"]) == {"key", "projectPositionM", "angleDeg", "surveyInternalM", "surveySharedM", "baseInternalM", "baseSharedM", "levelNames",
                                      "levelElevationsM", "levelProjectElevationsM", "levelBase", "activeSite", "sites", "heldAgainst"}
    assert set(steps["checkPoints"]) == {"key", "plot", "face", "sharedM", "internalM", "keyInternalM", "keySharedM", "gapMm", "revitGapMm", "pass"}
    assert position_file.level_profile_site(settings)["levelBase"] == "Survey Point" and position_file.key_of(settings) == "imc"
    assert position_file.group_of(settings) == GROUP


def test_r353_the_cli_writes_the_file_outside_git_the_position_step_and_the_check_points(tmp_path, capsys):
    path, values = made_settings(tmp_path)
    folder = tmp_path / "reads" / values["drawings"]["baseplan"]
    (folder / "plots.json").write_text(json.dumps({"source": "b.dwg", "plots": made_plots()}), encoding="utf-8")
    read = tmp_path / "read.json"
    read.write_text(json.dumps(made_read()), encoding="utf-8")
    reads = []
    for key in GROUP[1:]:
        other = tmp_path / ("read_%s.json" % key)
        other.write_text(json.dumps(made_read()), encoding="utf-8")
        reads += ["--key-reads", "%s=%s" % (key, other)]
    points = tmp_path / "points.json"
    points.write_text(json.dumps(made_points()), encoding="utf-8")
    base = ["--settings", str(path), "--key-read", str(read)] + reads
    code = position_file.main(base + ["--out", str(tmp_path / "out"), "--steps", str(tmp_path / "steps"), "--key-points", "imc=%s" % points])
    assert code == 0, capsys.readouterr().err
    written = json.loads((tmp_path / "out" / values["addin"]["position"]["file"].replace("\\", "/").split("/")[-1]).read_text(encoding="utf-8"))
    assert written["levelBase"] == "Survey Point" and written["projectPosition"]["northM"] == 2000.0 and written["key"] == "imc" and written["activeSite"] == "Internal"
    step = parity.load_step(tmp_path / "steps" / parity.step_name("position", "python"), "python")
    assert step["plots"]["position"]["raw"]["levelNames"] == ["Level 1", "Level 2"] and step["plots"]["position"]["raw"]["heldAgainst"] == GROUP[1:]
    assert {"config", "keyRead", "rules"} | {"keyRead:" + key for key in GROUP[1:]} <= {item["role"] for item in step["run"]["inputs"]}
    checked = parity.load_step(tmp_path / "steps" / parity.step_name("checkPoints", "python"), "python")
    assert len(checked["plots"]) == 12 and all(item["raw"]["pass"] for item in checked["plots"].values())
    assert {"keyPoints:imc", "baseplanPlots"} <= {item["role"] for item in checked["run"]["inputs"]}
    # A set of reads with a key left out is refused, nothing written
    assert position_file.main(["--settings", str(path), "--key-read", str(read)] + reads[:-2] + ["--steps", str(tmp_path / "steps")]) == 2
    assert "img no read" in capsys.readouterr().err
    assert not (tmp_path / "steps" / parity.step_name("position", "python")).exists()
    other = made_read()
    other["documents"][0]["site"]["levels"][0]["elevation"] = 9.5
    read.write_text(json.dumps(other), encoding="utf-8")
    assert position_file.main(base + ["--steps", str(tmp_path / "steps")]) == 2
    assert "levelStepsM" in capsys.readouterr().err
