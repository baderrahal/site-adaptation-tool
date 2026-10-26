"""R353. The project position file of button 1 and its check points, python/position_file.py, on made up key reads built in code
(R10). dotnet/SiteAdaptation.Tests/PositionFileTests.cs holds the C# side on the same made up reads, on net48 and net8.0."""
import json
import math

import pytest

import key_rules
import parity
import position_file
import project_profile

SITE_RULES = {"angleToTrueNorthDeg": 0, "levels": ["Level 1", "Level 2"], "levelStepsM": {"Level 2": 4.0}, "levelBase": "Survey Point"}


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


def test_r353_the_position_file_is_the_site_of_a_key_read_and_holds_the_site_rules_of_the_profile():
    document = position_file.document_of(made_read())
    position = position_file.position_of(document, "imc")
    assert position["format"] == "site-adaptation-position" and position["key"] == "imc" and position["model"] == "key_TEST-RC"
    assert position["projectPosition"] == {"eastM": 1000.0, "northM": 2000.0, "elevationM": 5.0, "angleDeg": 0.0}
    assert position["surveyPoint"] == {"internalM": [0.0, 0.0, 0.0], "sharedM": [1000.0, 2000.0, 5.0]}
    assert [level["name"] for level in position["levels"]] == ["Level 1", "Level 2"] and position["levelBase"] == "Survey Point"
    assert position_file.against_rules(position, SITE_RULES) == []
    other = dict(SITE_RULES, levelStepsM={"Level 2": 4.001}, levelBase="Project Base Point")
    assert len(position_file.against_rules(position, other)) == 2
    assert position_file.written(position)["projectPosition"]["eastM"] == 1000.0


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


def test_r353_the_check_points_go_to_their_internal_points_by_the_file_and_by_the_survey_pair_of_the_key_read():
    controls = [(1010.0, 2020.0), (990.0, 1985.0), (1500.0, 2500.0)]
    centroids = [(1005.0, 2003.0), (1040.0, 1990.0), (1020.0, 2050.0)]
    for angle in (0.0, 30.0):
        document = position_file.document_of(made_read(angle))
        position = position_file.position_of(document, "imc")
        rows = position_file.check_points(position, controls, centroids, document, 1.0, 2)
        assert list(rows) == ["control 1", "control 2", "control 3", "plots min", "plots max", "survey point", "project base point"]
        assert rows["plots min"]["sharedM"] == [1005.0, 1990.0, 0.0] and rows["plots max"]["sharedM"] == [1040.0, 2050.0, 0.0]
        turn = math.radians(angle)
        east, north = 1010.0 - 1000.0, 2020.0 - 2000.0
        assert rows["control 1"]["internalM"] == pytest.approx([math.cos(turn) * east + math.sin(turn) * north, -math.sin(turn) * east + math.cos(turn) * north, -5.0])
        assert all(row["gapMm"] < 1e-6 and row["pass"] for row in rows.values())
        assert rows["survey point"]["internalM"] == pytest.approx([0.0, 0.0, 0.0])
    # The survey point of the read 0.6 mm and 1.2 mm off the project position: the file and the read part by that much on every point
    for off, passes in ((0.0006, True), (0.0012, False)):
        document = position_file.document_of(made_read(survey_off=(off, 0.0, 0.0)))
        rows = position_file.check_points(position_file.position_of(document, "imc"), controls, centroids, document, 1.0, 2)
        assert rows["control 3"]["gapMm"] == pytest.approx(off * 1000.0) and rows["control 3"]["pass"] is passes
    # The gap is judged as written at the decimals of config, so 1.004 mm shows 1.0 and passes at 1 mm
    document = position_file.document_of(made_read(survey_off=(0.001004, 0.0, 0.0)))
    assert position_file.check_points(position_file.position_of(document, "imc"), controls, centroids, document, 1.0, 2)["control 1"]["pass"] is True
    rows = position_file.check_points(position_file.position_of(document, "imc"), controls, centroids, None, 1.0, 2)
    assert rows["control 1"]["keyInternalM"] is None and rows["control 1"]["pass"] is None
    with pytest.raises(position_file.PositionError, match="no face"):
        position_file.check_points(position_file.position_of(document, "imc"), controls, [], None, 1.0, 2)


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
                                      "levelElevationsM", "levelProjectElevationsM", "levelBase"}
    assert set(steps["checkPoints"]) == {"sharedM", "internalM", "keyInternalM", "gapMm", "pass"}
    assert position_file.level_profile_site(settings)["levelBase"] == "Survey Point" and position_file.key_of(settings) == "imc"


def test_r353_the_cli_writes_the_file_outside_git_and_the_position_step(tmp_path, capsys):
    read = tmp_path / "read.json"
    read.write_text(json.dumps(made_read()), encoding="utf-8")
    values = json.loads(json.dumps(project_profile.load_settings().values))
    config = tmp_path / "project.json"
    config.write_text(json.dumps(values), encoding="utf-8")
    code = position_file.main(["--settings", str(config), "--key-read", str(read), "--out", str(tmp_path / "out"), "--steps", str(tmp_path / "steps")])
    assert code == 0, capsys.readouterr().err
    written = json.loads((tmp_path / "out" / values["addin"]["position"]["file"].replace("\\", "/").split("/")[-1]).read_text(encoding="utf-8"))
    assert written["levelBase"] == "Survey Point" and written["projectPosition"]["northM"] == 2000.0 and written["key"] == "imc"
    step = parity.load_step(tmp_path / "steps" / parity.step_name("position", "python"), "python")
    assert step["plots"]["position"]["raw"]["levelNames"] == ["Level 1", "Level 2"]
    assert {item["role"] for item in step["run"]["inputs"]} == {"config", "keyRead"}
    other = made_read()
    other["documents"][0]["site"]["levels"][0]["elevation"] = 9.5
    read.write_text(json.dumps(other), encoding="utf-8")
    assert position_file.main(["--settings", str(config), "--key-read", str(read), "--steps", str(tmp_path / "steps")]) == 2
    assert "levelStepsM" in capsys.readouterr().err
    assert not (tmp_path / "steps" / parity.step_name("position", "python")).exists()
