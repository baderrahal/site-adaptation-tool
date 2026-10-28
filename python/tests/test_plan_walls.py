"""R362. The boundary walls of a build, python/plan_walls.py, on faces made up in code (R10). WallTests.cs of the C# holds the same faces and
the same values, so the two sides give one wall list."""
import pytest

import common
import plan_walls
import project_profile
import value_status

RULES = {"higherBase": True, "higherTop": True, "depth": 0.1, "rise": 1.7, "near": 0.3}
# Plots 1, 2 and 5 written, 3 and 6 faces the plan does not write, as an open space and a plot of another build. 5 meets the side of 2 for 8 m
# and 6 for the other 12 m, 3 runs behind 1 and 2 on one edge
RINGS = {"1": [(0.0, 0.0), (10.0, 0.0), (10.0, 20.0), (0.0, 20.0)], "2": [(10.0, 0.0), (20.0, 0.0), (20.0, 20.0), (10.0, 20.0)],
         "3": [(0.0, 20.0), (20.0, 20.0), (20.0, 30.0), (0.0, 30.0)], "5": [(20.0, 0.0), (30.0, 0.0), (30.0, 8.0), (20.0, 8.0)],
         "6": [(20.0, 8.0), (30.0, 8.0), (30.0, 20.0), (20.0, 20.0)]}
FRAMES = {"1": ((0.0, 0.0), (10.0, 0.0)), "2": ((20.0, 0.0), (10.0, 0.0)), "5": ((20.0, 0.0), (30.0, 0.0))}
FFLS = {"1": 100.0, "2": 100.5, "5": 101.0}


def made(rings=RINGS, frames=FRAMES, ffls=FFLS, order=("1", "2", "5"), rules=RULES):
    return plan_walls.walls(list(order), set(ffls), rings, frames, ffls, rules)


def test_r362_walls_along_every_edge_but_the_front_one_wall_per_shared_edge_from_the_first_plot_that_writes_it():
    found = [(wall["plot"], wall["edge"], wall["plots"], wall["ends"], wall["base"], wall["top"], wall["basePlot"], wall["topPlot"]) for wall in made()]
    assert found == [
        ("1", 1, ["1", "2"], [[10.0, 0.0], [10.0, 20.0]], 100.5 - 0.1, 100.5 + 1.7, "2", "2"),
        ("1", 2, ["1", "3"], [[10.0, 20.0], [0.0, 20.0]], 100.0 - 0.1, 100.0 + 1.7, "1", "1"),
        ("1", 3, ["1"], [[0.0, 20.0], [0.0, 0.0]], 100.0 - 0.1, 100.0 + 1.7, "1", "1"),
        # The side of 2 parts at the corner of 5 and 6, the part on 5 at the yard of 5, the higher plot
        ("2", 1, ["2", "5"], [[20.0, 0.0], [20.0, 8.0]], 101.0 - 0.1, 101.0 + 1.7, "5", "5"),
        ("2", 1, ["2", "6"], [[20.0, 8.0], [20.0, 20.0]], 100.5 - 0.1, 100.5 + 1.7, "2", "2"),
        ("2", 2, ["2", "3"], [[20.0, 20.0], [10.0, 20.0]], 100.5 - 0.1, 100.5 + 1.7, "2", "2"),
        ("5", 1, ["5"], [[30.0, 0.0], [30.0, 8.0]], 101.0 - 0.1, 101.0 + 1.7, "5", "5"),
        ("5", 2, ["5", "6"], [[30.0, 8.0], [20.0, 8.0]], 101.0 - 0.1, 101.0 + 1.7, "5", "5")]


def test_r362_the_lower_plot_rules_take_the_yard_and_the_ffl_of_the_lower_plot():
    first = made(rules=dict(RULES, higherBase=False, higherTop=False))[0]
    assert (first["base"], first["top"], first["basePlot"], first["topPlot"]) == (100.0 - 0.1, 100.0 + 1.7, "1", "1")


def test_r362_a_neighbour_end_within_the_edge_hold_of_a_corner_parts_no_wall():
    # The face 8 starts 0.2 m up the side of 7, within keyPlans.wallEdgeM, so the side is one wall on both
    rings = {"7": [(0.0, 40.0), (10.0, 40.0), (10.0, 60.0), (0.0, 60.0)], "8": [(10.0, 40.2), (20.0, 40.2), (20.0, 60.0), (10.0, 60.0)]}
    found = made(rings, {"7": ((0.0, 40.0), (10.0, 40.0))}, {"7": 50.0}, ("7",))
    assert [(wall["edge"], wall["plots"], wall["ends"]) for wall in found] == [
        (1, ["7", "8"], [[10.0, 40.0], [10.0, 60.0]]), (2, ["7"], [[10.0, 60.0], [0.0, 60.0]]), (3, ["7"], [[0.0, 60.0], [0.0, 40.0]])]


def test_r362_an_edge_on_the_front_of_a_written_neighbour_is_open():
    # The front of 10 is the edge it shares with the back of 9, so neither places a wall there
    rings = {"9": [(0.0, 100.0), (10.0, 100.0), (10.0, 110.0), (0.0, 110.0)], "10": [(0.0, 110.0), (10.0, 110.0), (10.0, 120.0), (0.0, 120.0)]}
    frames = {"9": ((0.0, 100.0), (10.0, 100.0)), "10": ((10.0, 110.0), (0.0, 110.0))}
    found = made(rings, frames, {"9": 10.0, "10": 11.0}, ("9", "10"))
    assert [(wall["plot"], wall["edge"]) for wall in found] == [("9", 1), ("9", 3), ("10", 1), ("10", 2), ("10", 3)]


def test_r362_a_rule_the_build_does_not_place_walls_by_is_refused():
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    level = profile.get("levelProfiles.parsons2024Rev6")
    rules = plan_walls.wall_rules(level["keyRules"], level, "parsons2024Rev6", profile, 0.3)
    assert rules == {"higherBase": True, "higherTop": True, "depth": 0.1, "rise": 1.7, "near": 0.3}
    for entry, line in (("wallLine", "centreOnTheEdge"), ("frontOpen", "all"), ("wallBase", "yardLevelOfTheHigherPlot or yardLevelOfTheLowerPlot")):
        key = dict(level["keyRules"], **{entry: {"rule": "other"}})
        with pytest.raises(project_profile.ProfileError) as error:
            plan_walls.wall_rules(key, level, "parsons2024Rev6", profile, 0.3)
        assert str(error.value) == "keyRules.%s of parsons2024Rev6 holds no rule a build places boundary walls by, it takes %s (R362)" % (entry, line)
    with pytest.raises(project_profile.ProfileError) as error:
        plan_walls.wall_rules(level["keyRules"], level, "parsons2024Rev6", profile, "0.3")
    assert str(error.value) == "keyPlans.wallEdgeM is no number of 0 or more, a build places no boundary wall without it (R362)"
    # keyRules of none of the four wall rules places no wall, one of them alone is refused for the others
    bare = {name: rule for name, rule in level["keyRules"].items() if name not in ("wallBase", "wallTop", "wallLine", "frontOpen")}
    assert plan_walls.wall_rules(bare, level, "parsons2024Rev6", profile, 0.3) is None
    with pytest.raises(project_profile.ProfileError):
        plan_walls.wall_rules(dict(bare, frontOpen={"rule": "all"}), level, "parsons2024Rev6", profile, 0.3)


def test_r362_r401_each_wall_value_carries_the_status_of_its_entries_and_an_ffl_an_override_moved_names_it():
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    table = value_status.table_of(settings, profile, "parsons2024Rev6")
    wall = made()[0]
    entries = {plot: {"plot": plot, "levels": {"ffl": {"value": FFLS[plot], "source": "estimate"}}} for plot in FFLS}
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {}, entries, {})
    assert {field: (item[field]["source"], item[field]["status"], item[field]["statusFrom"]) for field in plan_walls.FIELDS} == {
        "plots": ("locationPlan", "DOCUMENT", "walls.plots from locationPlan"), "ends": ("keyRules", "UNKNOWN", "walls.ends from keyRules"),
        "base": ("keyRules", "MEASURED", "walls.base from keyRules"), "top": ("keyRules", "MEASURED", "walls.top from keyRules")}
    assert (table.used["walls.base from keyRules"]["strength"], table.used["walls.top from keyRules"]["strength"]) == ("weak", "weak")
    assert [entry["status"] for entry in table.used["walls.ends from keyRules"]["entries"]][-1] == "UNKNOWN"
    # A heightM override that moved the FFL of 2, the higher plot of the wall, gives its base and top the status of the override list
    rule = {"2": {"plot": "2", "levels": {"ffl": {"value": 100.4, "source": "estimate"}}}}
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)}, entries, rule)
    assert (item["base"]["status"], item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("RECOMMENDED", "walls.base from override by heightM",
                                                                                            "walls.top from override by heightM")
    assert item["ends"]["statusFrom"] == "walls.ends from keyRules"
    # An override that left the FFL at its value by the rule moved no wall
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)}, entries, {"2": entries["2"]})
    assert item["base"]["statusFrom"] == "walls.base from keyRules"


def test_r362_parity_walls_the_plan_places_on_the_made_up_row_after_the_overrides(monkeypatch):
    # The row of six 12 m faces of test_level_floor_steps.py with the overrides of test_override_param_steps.py, 3 its FFL 10 cm up by heightM.
    # WallTests.cs holds the same walls
    from test_level_floor_steps import made_build
    from test_override_param_steps import MADE
    built = made_build(monkeypatch, MADE)
    walls = built["trace"]["walls"]
    assert [(item["plot"], item["edge"], item["plots"]["value"], common.r3(item["base"]["value"]), common.r3(item["top"]["value"]), item["basePlot"]) for item in walls] == [
        ("1", 0, ["1"], 10.188, 11.988, "1"), ("1", 1, ["1"], 10.188, 11.988, "1"), ("1", 2, ["1", "2"], 10.428, 12.228, "2"),
        ("2", 1, ["2"], 10.428, 12.228, "2"), ("2", 2, ["2", "3"], 10.528, 12.328, "3"), ("3", 1, ["3"], 10.528, 12.328, "3"),
        ("3", 2, ["3", "4"], 10.548, 12.348, "4"), ("4", 1, ["4"], 10.548, 12.348, "4"), ("4", 2, ["4", "5"], 10.788, 12.588, "5"),
        ("5", 1, ["5"], 10.788, 12.588, "5"), ("5", 2, ["5", "6"], 10.788, 12.588, "5"), ("6", 1, ["6"], 10.788, 12.588, "6"),
        ("6", 2, ["6"], 10.788, 12.588, "6")]
    # The FFL of each written plot less the yard depth of 0.1 m and plus the 1.7 m of Rev 6, the front edge along the road at y = -3 open
    assert all(item["base"]["value"] == built["plan"][item["basePlot"]]["levels"]["ffl"]["value"] - 0.1 for item in walls)
    assert not any(item["ends"]["value"][0][1] == item["ends"]["value"][1][1] == 0.0 for item in walls)
    assert [item["base"]["statusFrom"] for item in walls if item["basePlot"] == "3"] == ["walls.base from override by heightM"] * 2
    assert built["summary"]["walls"] == {"walls": 13, "byPlots": {"1": 8, "2": 5}, "statusByField": {
        "walls.plots": {"DOCUMENT": 13}, "walls.ends": {"UNKNOWN": 13}, "walls.base": {"MEASURED weak": 11, "RECOMMENDED": 2},
        "walls.top": {"MEASURED weak": 11, "RECOMMENDED": 2}}}
    made, tables = plan_walls.step(built["scope"], built["rows"], built["trace"])
    assert made["7"] == {"unit": None, "written": False, "edges": [], "plots": [], "ends": [], "base": [], "top": [], "levelPlots": [], "status": []}
    assert (made["3"]["edges"], made["3"]["plots"], made["3"]["levelPlots"]) == ([1, 2], [["3"], ["3", "4"]], [["3", "3"], ["4", "4"]])
    assert made["3"]["status"][0]["base"] == {"status": "RECOMMENDED", "statusFrom": "walls.base from override by heightM"}
    assert sorted(tables["statusFrom"]) == ["walls.base from keyRules", "walls.base from override by heightM", "walls.ends from keyRules",
                                            "walls.plots from locationPlan", "walls.top from keyRules", "walls.top from override by heightM"]
    assert tables["statusFrom"]["walls.base from override by heightM"]["override"] == "heightM"
    assert tuple(project_profile.load_settings().get("addin")["parity"]["steps"]["walls"]) == plan_walls.STEP_FIELDS
    assert all(tuple(made[number]) == plan_walls.STEP_FIELDS for number in made)
