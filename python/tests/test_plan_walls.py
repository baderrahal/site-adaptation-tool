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
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {})
    assert {field: (item[field]["source"], item[field]["status"], item[field]["statusFrom"]) for field in plan_walls.FIELDS} == {
        "plots": ("locationPlan", "DOCUMENT", "walls.plots from locationPlan"), "ends": ("keyRules", "UNKNOWN", "walls.ends from keyRules"),
        "base": ("keyRules", "MEASURED", "walls.base from keyRules"), "top": ("keyRules", "MEASURED", "walls.top from keyRules")}
    assert (table.used["walls.base from keyRules"]["strength"], table.used["walls.top from keyRules"]["strength"]) == ("weak", "weak")
    assert [entry["status"] for entry in table.used["walls.ends from keyRules"]["entries"]][-1] == "UNKNOWN"
    # A heightM override that moved the FFL of 2, the higher plot of the wall, gives its base and top the status of the override list
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)})
    assert (item["base"]["status"], item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("RECOMMENDED", "walls.base from override by heightM",
                                                                                            "walls.top from override by heightM")
    assert item["ends"]["statusFrom"] == "walls.ends from keyRules"
    # Q113. An override that left the FFL at its value by the rule still touched the wall
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)})
    assert item["base"]["statusFrom"] == "walls.base from override by heightM"


def test_r362_parity_walls_the_plan_places_on_the_made_up_row_after_the_overrides(monkeypatch):
    # The row of six 12 m faces of test_level_floor_steps.py with the overrides of test_override_param_steps.py, 3 its FFL 10 cm up by heightM.
    # WallTests.cs holds the same walls
    from test_level_floor_steps import made_build
    from test_override_param_steps import MADE
    built = made_build(monkeypatch, MADE)
    walls = built["trace"]["walls"]
    assert [(item["plot"], item["edge"], item["plots"]["value"], common.r3(item["base"]["value"]), common.r3(item["top"]["value"]), item["basePlot"]) for item in walls] == [
        ("1", 0, ["1"], 10.188, 11.988, "1"), ("1", 1, ["1"], 10.188, 11.988, "1"), ("1", 2, ["1", "2"], 10.428, 12.228, "2"),
        ("2", 2, ["2", "3"], 10.528, 12.328, "3"), ("2", 3, ["2"], 10.428, 12.228, "2"), ("3", 1, ["3"], 10.528, 12.328, "3"),
        ("3", 2, ["3", "4"], 10.548, 12.348, "4"), ("4", 1, ["4"], 10.548, 12.348, "4"), ("4", 2, ["4", "5"], 10.788, 12.588, "5"),
        ("5", 1, ["5"], 10.788, 12.588, "5"), ("5", 2, ["5", "6"], 10.788, 12.588, "5"), ("6", 1, ["6"], 10.788, 12.588, "6"),
        ("6", 2, ["6"], 10.788, 12.588, "6")]
    # The FFL of each written plot less the yard depth of 0.1 m and plus the 1.7 m of Rev 6, the front edge along the road at y = -3 open
    assert all(item["base"]["value"] == built["plan"][item["basePlot"]]["levels"]["ffl"]["value"] - 0.1 for item in walls)
    # The rotation override of -179.999 turns the link of 2 to face its back edge, so that edge is open and the front of the rule along the road
    # walled, the only wall on the road (breaker F2 of PR 79). The 1.5 degrees of 6 leave its front facing the road
    assert [(item["plot"], item["edge"]) for item in walls if item["ends"]["value"][0][1] == item["ends"]["value"][1][1] == 0.0] == [("2", 3)]
    assert not any(item["ends"]["value"][0][1] == item["ends"]["value"][1][1] == 25.0 and item["plots"]["value"] == ["2"] for item in walls)
    # Q112, R2 and R3 of the review of PR 85: a wall names every override of its plots whose set holds the field, in the order of its plots,
    # the rotations of 2 and 6 every value of their walls
    assert [item["base"]["statusFrom"] for item in walls if item["basePlot"] == "3"] == ["walls.base from override by rotationDeg heightM", "walls.base from override by heightM"]
    assert built["summary"]["walls"] == {"walls": 13, "byPlots": {"1": 8, "2": 5}, "statusByField": {
        "walls.plots": {"DOCUMENT": 7, "RECOMMENDED": 6}, "walls.ends": {"UNKNOWN": 13}, "walls.base": {"MEASURED weak": 5, "RECOMMENDED": 8},
        "walls.top": {"MEASURED weak": 5, "RECOMMENDED": 8}}}
    made, tables = plan_walls.step(built["scope"], built["rows"], built["trace"])
    assert made["7"] == {"unit": None, "written": False, "edges": [], "plots": [], "ends": [], "base": [], "top": [], "levelPlots": [], "status": []}
    assert (made["3"]["edges"], made["3"]["plots"], made["3"]["levelPlots"]) == ([1, 2], [["3"], ["3", "4"]], [["3", "3"], ["4", "4"]])
    assert made["3"]["status"][0]["base"] == {"status": "RECOMMENDED", "statusFrom": "walls.base from override by heightM"}
    assert sorted(tables["statusFrom"]) == ["walls.base from keyRules", "walls.base from override by heightM", "walls.base from override by rotationDeg",
                                            "walls.base from override by rotationDeg heightM", "walls.ends from keyRules", "walls.ends from override by rotationDeg",
                                            "walls.plots from locationPlan", "walls.plots from override by rotationDeg", "walls.top from keyRules",
                                            "walls.top from override by heightM", "walls.top from override by rotationDeg", "walls.top from override by rotationDeg heightM"]
    assert tables["statusFrom"]["walls.base from override by heightM"]["overrides"] == ["heightM"]
    assert tuple(project_profile.load_settings().get("addin")["parity"]["steps"]["walls"]) == plan_walls.STEP_FIELDS
    assert all(tuple(made[number]) == plan_walls.STEP_FIELDS for number in made)


def test_r362_a_plot_a_rotation_override_turned_leaves_open_the_edge_its_link_faces():
    # Breaker F2 of PR 79: the link Y axis of rotationDeg and mirrored after the overrides, the inward normal of the front (cad_links.link_axes),
    # names the edge whose outward normal runs most against it, the first in ring order. A plot no rotation override turned keeps the front of R355
    ring, frame = [(0.0, 0.0), (10.0, 0.0), (10.0, 20.0), (0.0, 20.0)], ((0.0, 0.0), (10.0, 0.0))
    assert plan_walls.open_edge(ring, frame, 0.0, False, True) == ((0.0, 0.0), (10.0, 0.0))
    assert plan_walls.open_edge(ring, frame, 90.0, False, True) == ((10.0, 0.0), (10.0, 20.0))
    # Review 2 of PR 79: the turned plots of the keys are mirrored at about 180 degrees, whose link faces the road again
    assert plan_walls.open_edge(ring, frame, 180.0, True, True) == ((0.0, 0.0), (10.0, 0.0))
    assert plan_walls.open_edge(ring, frame, 270.0, True, True) == ((10.0, 0.0), (10.0, 20.0))
    assert plan_walls.open_edge(ring[::-1], frame, 180.0, False, True) == ((0.0, 20.0), (10.0, 20.0))
    assert plan_walls.open_edge(ring, frame, 90.0, False, False) == frame
    # The walls of 1, 2 and 5 with the link of 2 turned to face its side on 5: that side open from both, its front on the road walled
    rings = {plot: RINGS[plot] for plot in ("1", "2", "5")}
    opened = {"1": FRAMES["1"], "2": plan_walls.open_edge(RINGS["2"], FRAMES["2"], 90.0, False, True), "5": FRAMES["5"]}
    found = made(rings=rings, frames=opened)
    assert [(wall["plot"], wall["edge"]) for wall in found] == [("1", 1), ("1", 2), ("1", 3), ("2", 0), ("2", 2), ("5", 1), ("5", 2)]


def test_r362_r401_q113_a_base_at_its_ffl_with_no_override_names_the_override_that_touched_it():
    # Q113 (Bader, 2026-10-28): heightM on 2, the plot of the base and the pick of the rule, left at the FFL of the rule, still touched the
    # base and the top, so they take the status of the override list and name it, where the equality rule of PR 75 left them their own list.
    # Q112: an override on each of the two plots of the wall is named once in the order of the plots
    settings = project_profile.load_settings()
    table = value_status.table_of(settings, project_profile.load_profile(project_profile.profile_path(settings)), "parsons2024Rev6")
    wall = made()[0]
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)})
    assert (item["base"]["status"], item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("RECOMMENDED", "walls.base from override by heightM",
                                                                                            "walls.top from override by heightM")
    ffls = dict(FFLS, **{"2": 99.98})
    wall = made(ffls=ffls)[0]
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"1": ("entryM", "heightM"), "2": ("heightM",)})
    assert item["base"]["statusFrom"] == "walls.base from override by heightM" and table.used[item["base"]["statusFrom"]]["overrides"] == ["heightM"]


def test_r362_r401_q112_review1_a_wall_names_the_overrides_of_every_plot_it_stands_on():
    # R2 of the review of PR 85: the base and the top of a wall take the FFL of the highest or lowest of its written plots, so a heightM of
    # either plot touched them, where the pick and the plot of the level alone left 290 values of the real list naming nothing (Q112)
    settings = project_profile.load_settings()
    table = value_status.table_of(settings, project_profile.load_profile(project_profile.profile_path(settings)), "parsons2024Rev6")
    ffls = dict(FFLS, **{"1": 100.6})
    wall = made(ffls=ffls)[0]
    assert (wall["plots"], wall["basePlot"]) == (["1", "2"], "1")
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)})
    assert (item["base"]["status"], item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("RECOMMENDED", "walls.base from override by heightM",
                                                                                            "walls.top from override by heightM")
    assert item["plots"]["statusFrom"] == "walls.plots from locationPlan"


def test_r362_r401_q112_review1_a_rotation_override_that_moves_the_open_edge_touches_every_value_of_the_walls():
    # R3 of the review of PR 85: a rotationDeg override turns the open edge (breaker F2 of PR 79), so it decides which walls stand, and every
    # value of a wall on its plot names it and takes the worst of the override status and its own entries (Q112, Q113)
    settings = project_profile.load_settings()
    table = value_status.table_of(settings, project_profile.load_profile(project_profile.profile_path(settings)), "parsons2024Rev6")
    wall = made()[0]
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"1": ("heightM", "rotationDeg")})
    assert [item[field]["statusFrom"] for field in plan_walls.FIELDS] == ["walls.plots from override by rotationDeg", "walls.ends from override by rotationDeg",
                                                                          "walls.base from override by heightM rotationDeg", "walls.top from override by heightM rotationDeg"]
    assert [item[field]["status"] for field in plan_walls.FIELDS] == ["RECOMMENDED", "UNKNOWN", "RECOMMENDED", "RECOMMENDED"]


def test_r362_r401_a_base_whose_pick_an_override_changed_names_the_override():
    # Breaker F3 of PR 79: by the rule 2 is the higher plot of the wall of 1 and 2, its heightM override drops it under 1, so the base and the top
    # are those of 1, which holds no override, and name heightM, as the FFL of 2 is compared to pick them (R2 of the review of PR 85)
    settings = project_profile.load_settings()
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    table = value_status.table_of(settings, profile, "parsons2024Rev6")
    ffls = dict(FFLS, **{"2": 99.98})
    wall = made(ffls=ffls)[0]
    assert (wall["plots"], wall["basePlot"], wall["topPlot"]) == (["1", "2"], "1", "1")
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"2": ("heightM",)})
    assert (item["base"]["status"], item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("RECOMMENDED", "walls.base from override by heightM",
                                                                                            "walls.top from override by heightM")
    # Review 2 of PR 79, the wall of 543 and 544: heightM lifts 1, the plot of the base, to the FFL of 2, the pick of the rule, so the base
    # equals the base with no override, and since Q113 it names heightM, which touched the FFL of its own plot
    ffls = dict(FFLS, **{"1": 100.5})
    wall = made(ffls=ffls)[0]
    assert (wall["plots"], wall["basePlot"]) == (["1", "2"], "1")
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, table, {"1": ("heightM",)})
    assert (item["base"]["statusFrom"], item["top"]["statusFrom"]) == ("walls.base from override by heightM", "walls.top from override by heightM")
