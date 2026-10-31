"""R358 and R359. The build steps floors and levels of the parity harness on a made up row built in code (R10): the levels by the level rules
of keyRules and the ramp, generic and yard outlines with the ramp type of plan_from_book.py, both by the rule before an override of the
list, which the C# of the worker gives the same (LevelFloorStepTests.cs holds the same made up data)."""
import argparse
import copy
import json
from pathlib import Path

import pytest
from shapely.geometry import LineString, Polygon

import common
import plan_from_book
import plan_from_past
import plan_rules
import project_profile

DP2_FILE = "RRE-WP-NSP-KEO-ARC-DP2-ZZ-MOD-2200.rvt"
VD2_FILE = "RRE-WP-NSP-KEO-ARC-VD2-ZZ-MOD-2200.rvt"
TH1_FILE = "RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-2200.rvt"
# Every factor of the hand alone a unit in no cluster of the row can take, not mirrored for the file of TH1
TH1_HANDS = {factor: False for factor in ("pair left", "pair right", "alone no left", "alone no right", "alone no none", "alone no both")}
UNITS = {"1": "VD2", "2": "DP2", "3": "DP2", "4": "DP2", "5": "DP2", "6": "DP2"}
# The lines of LevelFloorStepTests.cs, the C# refuses with the same
NO_FFL = ("the steps levels and floors of the parity harness are a build by the level rules of keyRules, keyRules of parsons2024Rev6 codes no ffl, the C# builds the "
          "levels by keyRules alone (R359)")
YARD_LINE = ("the step floors of the parity harness takes the yard outlines of keyRules.yardOutline groups, keyRules of parsons2024Rev6 codes fittedOutline, whose "
             "outlines lie in config/floor_outlines.json, which the C# does not read (R358)")
# A made up yard outline of DP2 not mirrored on a 12 m plot, in the link frame
YARD = {"unit": "DP2", "mirrored": False, "width": 12.0, "type": "Yard made up", "loops": [[[1.0, 2.0], [5.0, 2.0], [5.0, 6.0], [1.0, 6.0]]]}
# A made up yard outline of TH1, which TH1-IM takes as its keo_file, holding none of its own
YARD_TH1 = {"unit": "TH1", "mirrored": False, "width": 12.0, "type": "Yard TH1 made up", "loops": [[[2.0, 3.0], [6.0, 3.0], [6.0, 7.0]]]}


def made_rules(split=None):
    """keyRules as the rules profile holds them, the hands as test_build_steps.py makes them up, the yard groups the made up one."""
    real = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    values = copy.deepcopy(real.values)
    key = values["levelProfiles"]["parsons2024Rev6"]["keyRules"]
    key["hand villa"] = {"rule": "blockMirrorRoadSide", "byFile": {VD2_FILE: {"no left": True, "no none": False}}}
    key["hand cluster"] = {"rule": "clusterSide", "byFile": {DP2_FILE: {"left": False, "right": True}}}
    key["hand alone"] = {"rule": "rowPairSideOrBlockMirrorOpenEnd", "byFile": {DP2_FILE: {"pair left": False, "pair right": True}, TH1_FILE: TH1_HANDS}}
    key["yardOutline"] = {"rule": "fittedOutlineRefit", "groups": [YARD, YARD_TH1]}
    key["levelSplit"] = {"rule": "units", "table": split or {}}
    return project_profile.ProjectProfile(real.path, values)


def made_build(monkeypatch, overrides=None, split=None, units=None, zones=None, road=0.0, back_dry=False, clusters=None):
    """A row of six 12 m faces along a road at y = -3, plot 1 a VD2 villa, 2 and 3 a DP2 pair of the footprint, 4 to 6 DP2 units in no
    cluster, each in Zone 2, units and zones another code or zone of a plot, the road level 10 m plus road plus a hundredth of x, none past
    y = 20 with back_dry, clusters other clusters of the footprint, built with a trace, and the steps linkFrame, levels and floors of it."""
    settings = project_profile.load_settings()
    profile = made_rules(split)
    numbers = [str(index) for index in range(1, 7)]
    rows = {number: {"locationPlanNumber": number, "typologyRev9": dict(UNITS, **(units or {}))[number], "footprintMirrored": "no",
                     "zone": (zones or {}).get(number, "Zone 2")} for number in numbers}
    # Each face drawn anticlockwise from its low left corner, as the C# test draws it, so shapely walks it from there the other way round
    faces = {number: Polygon([(index * 12.0, 0.0), (index * 12.0 + 12.0, 0.0), (index * 12.0 + 12.0, 25.0), (index * 12.0, 25.0)]).buffer(0)
             for index, number in enumerate(numbers)}
    roads = [LineString([(-50.0, -3.0), (100.0, -3.0)]), LineString([(-3.0, -10.0), (-3.0, 40.0)])]
    sales = {"1": {"Orientation": "WS"}}
    clusters = clusters or [{"code": "Duplex_DP2", "plots": ["2", "3"], "source": "footprint"}]
    monkeypatch.setattr(plan_from_book, "road_level", lambda surfaces, point, snap: (None, None) if back_dry and point[1] > 20.0 else (10.0 + road + point[0] / 100.0, "road"))
    args = argparse.Namespace(level_profile=None, overrides=None, override_source=None, override_model=[], override_fields=None)
    rules = plan_from_book.rule_inputs(args, settings, profile, numbers, rows, faces, roads, sales, clusters)
    rules["overrides"] = {} if overrides is None else overrides
    floors = plan_from_past.load_floors(settings)
    site = {"levels": {floors["levels"][kind]: 0.0 for kind in ("generic", "yard", "ramp")}, "surveyPoint": {"position": [0, 0, 0], "sharedPosition": [0, 0, 0]},
            "angleToTrueNorthDeg": 0.0}
    outlines = json.loads((Path(__file__).resolve().parents[2] / "config" / "floor_outlines.json").read_text(encoding="utf-8"))
    files = {"DP2": DP2_FILE, "VD2": VD2_FILE, "TH1-IM": TH1_FILE}
    trace = {}
    plan = plan_from_book.build(numbers, rows, {}, faces, roads, clusters, profile, settings, {"units": {}}, files, "folder", site, [("road", None)], outlines, "N1",
                                sales, True, rules, trace=trace)
    scope = numbers + ["7"]
    hands = plan_from_book.hand_step(scope, rows, rules, trace, files, profile, settings.get("majority"))
    links = plan_from_book.link_step(scope, rows, trace, hands, rules["keyRules"], files, profile)
    levels = plan_from_book.level_step(scope, rows, trace, rules, profile)
    made = plan_from_book.floor_step(scope, rows, trace, links, rules, profile, 0.5)
    return {"plan": {entry["plot"]: entry for entry in plan["plots"]}, "plots": plan["plots"], "missing": {entry["plot"]: entry for entry in plan["missing"]},
            "summary": plan["summary"], "levels": levels, "floors": made, "links": links, "profile": profile, "rules": rules, "trace": trace, "hands": hands,
            "rows": rows, "scope": scope, "files": files, "conflicts": plan["conflicts"]}


def entry_of(mean):
    # El @ Entry of a DP2 or VD2 unit by hand: the mean road at front plus 0.5 percent along its 5.52 m ramp, offsetM 0 of frontRoad
    return mean + 0.005 * 5.52


def test_r359_parity_levels_the_road_at_front_el_at_entry_and_the_fl_of_a_piece_by_the_level_rules_before_an_override(monkeypatch):
    built = made_build(monkeypatch)
    levels = built["levels"]
    # The road at front of plot 5 is the mean of 10.48 and 10.60, El @ Entry that mean plus the slope along the ramp, both unrounded
    assert levels["5"]["roadAtFront"] == (10.48 + 10.6) / 2.0 and levels["5"]["entry"] == pytest.approx(entry_of(10.54), abs=1e-12)
    # 5 and 6 are the row pair of units in no cluster, one FFL at the highest El @ Entry plus the DP2 base of 0.2 m, 4 alone
    assert (levels["5"]["partner"], levels["6"]["partner"], levels["4"]["partner"]) == ("6", "5", None)
    assert levels["5"]["piece"] == ["5", "6"] and levels["4"]["piece"] == ["4"] and levels["2"]["piece"] == ["2", "3"]
    top = common.fixed(entry_of(10.66), 4) + 0.2
    assert levels["5"]["ffl"] == top and levels["6"]["ffl"] == top
    ffl = common.fixed(top, 3)
    assert levels["5"]["ssl"] == ffl - 0.075 and levels["5"]["height"] == ffl - common.fixed(entry_of(10.54), 4)
    # By hand: El @ Entry 10.5676, the FFL 10.8876 to 10.888, the height 0.3204, one step for the base of 0.2 m and one for the 0.708 riser over it
    assert (common.fixed(entry_of(10.54), 4), ffl, common.fixed(levels["5"]["height"], 4), levels["5"]["steps"]) == (10.5676, 10.888, 0.3204, 2)
    assert levels["5"]["parameters"] == {"parameter RRE_ZZ_SP_Prototype-FFL": "%.3f" % ((int(common.fixed(ffl * 1000, 0)) // 10) / 100.0)}
    # Plot 7 has no row and no face: every level null
    assert levels["7"] == {"unit": None, "partner": None, "piece": None, "roadAtFront": None, "entry": None, "ffl": None, "ssl": None, "height": None, "steps": None,
                           "parameters": None, "doorSource": None, "slopePercent": None, "splitTest": None}
    # R407. The fields of round 4i are null on a build of the past job
    assert (levels["5"]["doorSource"], levels["5"]["slopePercent"]) == (None, None)
    # The written values are those the plan writes in levelRule and levels for a plot with no override
    entries = plan_from_book.step_entries(project_profile.load_settings(), "levels", levels)
    plan = built["plan"]["5"]
    assert entries["5"]["written"]["entry"] == plan["levelRule"]["entry"] == plan["levels"]["entry"]["value"]
    assert entries["5"]["written"]["ffl"] == plan["levelRule"]["ffl"] == plan["levels"]["ffl"]["value"]
    assert entries["5"]["written"]["height"] == plan["levelRule"]["height"] and entries["5"]["written"]["ssl"] == plan["levels"]["ssl"]["value"]
    assert entries["5"]["written"]["steps"] == plan["levelRule"]["steps"] and entries["5"]["written"]["roadAtFront"] == common.fixed((10.48 + 10.6) / 2.0, 4)


def test_r359_r364_the_plan_writes_road_at_front_m_the_unrounded_mean_of_the_two_front_corners_rounded_once_at_4_decimals(monkeypatch, tmp_path):
    # A road 0.4 mm over the plain one: the corners of plot 5 lie at 10.4804 and 10.6004, which levelRule.rampFrontCorners writes at
    # 3 decimals, 10.48 and 10.6, whose mean 10.54 is 0.4 mm off the unrounded mean 10.5404 entry_of takes before the offset and the slope
    built = made_build(monkeypatch, {"5": {"entryM": {"gapToRuleM": 0.5}}}, road=0.0004)
    plan, levels = built["plan"]["5"], built["levels"]["5"]
    assert plan["levelRule"]["rampFrontCorners"] == [10.48, 10.6]
    assert plan["levels"]["roadAtFrontM"]["value"] == 10.5404 == common.fixed(levels["roadAtFront"], 4)
    assert plan["levels"]["roadAtFrontM"]["source"] == plan_from_book.ESTIMATE
    # El @ Entry by the rule from the same unrounded mean, each rounded once, and an entryM override moves El @ Entry, never the road at front
    assert plan["levelRule"]["entry"] == common.fixed(entry_of(10.5404), 4) and plan["levels"]["entry"]["value"] == common.fixed(common.fixed(entry_of(10.5404), 4) + 0.5, 4)
    assert list(plan["levels"]) == ["ffl", "ssl", "entry", "height", "steps", "roadAtFrontM"]
    # Every written plot holds it, each the rounded mean of its own two front corners
    assert all(entry["levels"]["roadAtFrontM"]["value"] == common.fixed(built["levels"][number]["roadAtFront"], 4) for number, entry in built["plan"].items())
    # The status of R401 by its own entries of valueStatus, never the UNKNOWN of a field the map does not name
    assert plan["levels"]["roadAtFrontM"]["statusFrom"] == "levels.roadAtFrontM from estimate" and plan["levels"]["roadAtFrontM"]["status"] != "UNKNOWN"
    # The plan file keeps its 4 decimals, as it keeps those of El @ Entry, where the 3 decimals of the file would round it a second time
    written = json.loads(plan_from_past.write_plan(tmp_path / "plan.json", {"plots": built["plots"]}).read_text(encoding="utf-8"))
    assert next(entry for entry in written["plots"] if entry["plot"] == "5")["levels"]["roadAtFrontM"]["value"] == 10.5404


def test_r359_the_levels_of_the_step_are_those_of_the_rule_an_entry_or_height_override_moves_the_plan_only(monkeypatch):
    plain = made_build(monkeypatch)["levels"]
    moved = made_build(monkeypatch, {"6": {"entryM": {"gapToRuleM": 0.5}, "heightM": {"gapToRuleM": 0.2}}, "5": {"steps": 4}})
    assert moved["levels"] == plain
    plan = moved["plan"]
    assert plan["6"]["levels"]["entry"]["value"] == common.fixed(common.fixed(entry_of(10.66), 4) + 0.5, 4) and plan["6"]["levelRule"]["entry"] == common.fixed(entry_of(10.66), 4)
    assert plan["5"]["levels"]["steps"]["value"] == 4 and plan["5"]["levelRule"]["steps"] == plain["5"]["steps"]


def test_r359_a_cluster_is_cut_into_pieces_by_the_split_of_its_units_and_a_row_partner_pairs_both_ways():
    roles = {"2": {"plots": ["2", "3", "8", "9"]}, "3": {"plots": ["2", "3", "8", "9"]}, "8": {"plots": ["2", "3", "8", "9"]}, "9": {"plots": ["2", "3", "8", "9"]}}
    units = {"2": "DP1", "3": "TH1", "8": "TH1", "9": None}
    pieces = plan_rules.level_pieces(["2", "8", "9", "5", "6", "4"], roles, {"5": "6", "6": "5", "4": "5"}, units, {"DP1 TH1 TH1 None": "walls 1,3"})
    assert pieces == {"2": ["2"], "8": ["3", "8"], "9": ["9"], "5": ["5", "6"], "6": ["5", "6"], "4": ["4"]}
    assert plan_rules.level_pieces(["2"], roles, {}, units, {"DP1 TH1 TH1 None": "middle"})["2"] == ["2", "3"]
    with pytest.raises(project_profile.ProfileError) as error:
        plan_rules.level_pieces(["2"], roles, {}, units, {"DP1 TH1 TH1 None": "every 2"})
    assert str(error.value) == "keyRules.levelSplit names the pattern every 2, a build gives none, middle or walls n,m (R339)"
    sides = {"4": "right", "5": "left", "6": "right", "7": "left"}
    neighbours = {"5": {"left": ["4"], "right": ["6"]}, "6": {"left": ["5"], "right": []}, "4": {"left": [], "right": ["5"]}, "7": {"left": [], "right": ["8", "9"]}}
    assert plan_rules.row_partners(sides, neighbours) == {"5": "6", "6": "5"}


def test_r359_the_text_of_the_prototype_ffl_is_the_ffl_down_to_the_cm_with_3_decimals_and_a_level_profile_with_no_ffl_rule_is_refused(monkeypatch):
    assert plan_rules.prototype_ffl("fflDownToTheCm", 10.528) == "10.520" and plan_rules.prototype_ffl("ffl", 10.528) == "10.528"
    assert plan_rules.prototype_ffl("fflDownToTheCm", -0.015) == "-0.020"
    built = made_build(monkeypatch)
    rules = dict(built["rules"], keyRules={word: rule for word, rule in built["rules"]["keyRules"].items() if word != "ffl"})
    for step in (lambda: plan_from_book.level_step(["5"], {}, {}, rules, built["profile"]), lambda: plan_from_book.floor_step(["5"], {}, {}, {}, rules, built["profile"], 0.5)):
        with pytest.raises(project_profile.ProfileError) as error:
            step()
        assert str(error.value) == NO_FFL


def test_r358_parity_floors_the_ramp_generic_and_yard_outlines_and_the_ramp_type_by_the_rule(monkeypatch):
    built = made_build(monkeypatch)
    floors, links = built["floors"], built["links"]
    # The generic floor is the whole face as shapely gives its ring, the ramp the outline of R127 on the front, 5.52 m deep
    assert floors["5"]["generic"] == [[[48.0, 0.0], [48.0, 25.0], [60.0, 25.0], [60.0, 0.0]]]
    assert floors["5"]["rampOutline"] == [[48.0, 0.0], [60.0, 0.0], [60.0, 5.52], [48.0, 5.52]]
    assert (floors["5"]["rampClass"], floors["5"]["rampType"]) == ("0.5", "Ramps 0.5%")
    # The front corners at the road, the back pair parallel with its mean at El @ Entry as the plan writes it, at 4 decimals
    entry = common.fixed(entry_of(10.54), 4)
    assert floors["5"]["rampZ"] == [10.48, 10.6, entry + 10.6 - (10.48 + 10.6) / 2.0, entry + 10.48 - (10.48 + 10.6) / 2.0]
    # 5 is not mirrored: the yard outline of DP2 on a 12 m plot in the link frame, placed by the link as the plan writes it
    assert links["5"]["mirrored"] is False and floors["5"]["yardDepth"] == 0.1 and floors["5"]["yardUnit"] == "DP2" and floors["5"]["yardWidth"] == 12.0
    insert = plan_from_past.point(links["5"]["insert"])
    assert floors["5"]["yardType"] == "Yard made up" and floors["5"]["yardOutline"] == [[[insert[0] + x, insert[1] + y] for x, y in YARD["loops"][0]]]
    # 4 is mirrored and no outline is fitted for DP2 mirrored, so it lacks the yard with why
    assert links["4"]["mirrored"] is True and floors["4"]["yardOutline"] is None
    assert floors["4"]["lacks"] == [{"key": "floors.yard", "why": "no fitted outline for DP2 mirrored on a plot 12.0 m wide"}]
    assert floors["7"] == {"unit": None, "generic": None, "rampOutline": None, "rampZ": None, "rampClass": None, "rampType": None, "yardDepth": None, "yardUnit": None,
                           "yardWidth": None, "yardType": None, "yardOutline": None, "lacks": [], "yardGrade": None}
    # R407. yardGrade of round 4i is null on a build of the past job
    assert floors["5"]["yardGrade"] is None
    # The written values are those the plan writes for a plot with no override
    entries = plan_from_book.step_entries(project_profile.load_settings(), "floors", floors)
    plan = built["plan"]["5"]["floors"]
    assert entries["5"]["written"]["generic"] == [loop["points"] for loop in plan["generic"]["outline"]["value"]["loops"]]
    assert entries["5"]["written"]["yardOutline"] == [loop["points"] for loop in plan["yard"]["outline"]["value"]["loops"]]
    assert [corner[:2] for corner in plan["ramp"]["corners"]["value"]] == entries["5"]["written"]["rampOutline"]
    assert [corner[2] for corner in plan["ramp"]["corners"]["value"]] == entries["5"]["written"]["rampZ"] and plan["ramp"]["type"]["value"] == "Ramps 0.5%"


def test_r358_a_zone_with_no_yard_gives_the_plot_no_yard_and_no_lack(monkeypatch):
    # keyRules.yard gives Zone 4, the zone of IMG, noYard
    built = made_build(monkeypatch, zones={"5": "Zone 4"})
    five = built["floors"]["5"]
    assert (five["yardDepth"], five["yardUnit"], five["yardWidth"], five["yardType"], five["yardOutline"], five["lacks"]) == (None, None, None, None, None, [])
    assert five["rampType"] == "Ramps 0.5%" and "yard" not in built["plan"]["5"]["floors"]


def test_r358_a_code_with_no_yard_outline_of_its_own_takes_the_outline_of_its_keo_file(monkeypatch):
    built = made_build(monkeypatch, units={"5": "TH1-IM"})
    five, link = built["floors"]["5"], built["links"]["5"]
    assert link["mirrored"] is False and (five["yardUnit"], five["yardType"], five["yardWidth"]) == ("TH1", "Yard TH1 made up", 12.0)
    insert = plan_from_past.point(link["insert"])
    assert plan_from_past.plan_rotation(link["rotationDeg"]) == 0.0
    assert five["yardOutline"] == [[[insert[0] + x, insert[1] + y] for x, y in YARD_TH1["loops"][0]]]


def test_r358_a_code_keyrules_ramp_slope_holds_no_class_for_lacks_its_ramp_type(monkeypatch):
    # VL3 has no entry in keyRules.rampSlope of the rules profile
    assert "VL3" not in made_rules().get("levelProfiles")["parsons2024Rev6"]["keyRules"]["rampSlope"]["table"]
    five = made_build(monkeypatch, units={"5": "VL3"})["floors"]["5"]
    assert (five["rampClass"], five["rampType"]) == (None, None) and five["rampOutline"] is not None
    assert five["lacks"][-1] == {"key": "floors.ramp", "why": "keyRules.rampSlope and rampTypes give no ramp type for VL3"}


def test_r359_a_split_pattern_none_keeps_the_cluster_whole():
    # The rules profile holds none for a DP3 TH3 TH3 DP3 cluster
    assert project_profile.load_profile(project_profile.profile_path(project_profile.load_settings())).get(
        "levelProfiles")["parsons2024Rev6"]["keyRules"]["levelSplit"]["table"]["DP3 TH3 TH3 DP3"] == "none"
    roles = {plot: {"plots": ["2", "3", "8", "9"]} for plot in ("2", "3", "8", "9")}
    units = {"2": "DP3", "3": "TH3", "8": "TH3", "9": "DP3"}
    pieces = plan_rules.level_pieces(["2", "8"], roles, {}, units, {"DP3 TH3 TH3 DP3": "none"})
    assert pieces == {"2": ["2", "3", "8", "9"], "8": ["2", "3", "8", "9"]} and plan_rules.split_walls("none", 4) == []


def test_r358_the_back_ramp_corners_take_el_at_entry_as_written_at_4_decimals_never_its_raw_value():
    # Step 8 of docs/design/05-plan-steps.md: a road at 10.0 and a raw El @ Entry of 10.02745 give the back corners 10.028, one rounding 10.027
    heights = plan_from_book.ramp_heights([10.0, 10.0], common.fixed(10.02745, 4))
    assert [common.r3(z) for z in heights] == [10.0, 10.0, 10.028, 10.028] and common.r3(10.02745) == 10.027


def test_r358_the_floors_of_the_step_are_those_of_the_rule_a_ramp_override_moves_the_plan_only(monkeypatch):
    plain = made_build(monkeypatch)["floors"]
    moved = made_build(monkeypatch, {"5": {"rampFrontM": {"gapToRuleM": 0.3}, "rampType": "flat", "entryM": {"gapToRuleM": 0.1}}})
    assert moved["floors"] == plain
    ramp = moved["plan"]["5"]["floors"]["ramp"]
    assert ramp["type"]["value"] == "Flat Ramps" and ramp["corners"]["value"][0][2] == common.r3(10.48 + 0.3)


def test_r358_a_yard_outline_of_the_floor_file_or_a_width_step_of_another_kind_is_refused_with_the_line_of_csharp(monkeypatch):
    built = made_build(monkeypatch)
    key = dict(built["rules"]["keyRules"], yardOutline={"rule": "fittedOutline"})
    with pytest.raises(project_profile.ProfileError) as error:
        plan_from_book.floor_step(["5"], {}, {}, {}, dict(built["rules"], keyRules=key), built["profile"], 0.5)
    assert str(error.value) == YARD_LINE


def test_r358_the_width_key_of_the_yard_lookup_rounds_half_to_even_as_python_round_does():
    import floor_fit
    # 12.25 m is 24.5 steps of 0.5 m, half to even gives 24 steps, 12.0 m, and 12.75 m gives 26 steps, 13.0 m
    assert floor_fit.width_key(12.25, 0.5) == 12.0 and floor_fit.width_key(12.75, 0.5) == 13.0 and floor_fit.width_key(12.2500001, 0.5) == 12.5


def test_r358_r359_the_config_names_the_fields_of_the_steps_levels_and_floors_by_their_kind():
    steps = project_profile.load_settings().get("addin")["parity"]["steps"]
    assert {name: (field["kind"], field.get("decimals")) for name, field in steps["levels"].items()} == {
        "unit": ("noGeometry", None), "partner": ("noGeometry", None), "piece": ("noGeometry", None), "roadAtFront": ("levels", 4), "entry": ("levels", 4),
        "ffl": ("levels", 3), "ssl": ("levels", 3), "height": ("levels", 4), "steps": ("noGeometry", None), "parameters": ("noGeometry", None),
        "doorSource": ("noGeometry", None), "slopePercent": ("levels", 3), "splitTest": ("noGeometry", None)}
    assert {name: (field["kind"], field.get("decimals")) for name, field in steps["floors"].items()} == {
        "unit": ("noGeometry", None), "generic": ("points", 3), "rampOutline": ("points", 3), "rampZ": ("levels", 3), "rampClass": ("noGeometry", None),
        "rampType": ("noGeometry", None), "yardDepth": ("levels", 3), "yardUnit": ("noGeometry", None), "yardWidth": ("points", 3), "yardType": ("noGeometry", None),
        "yardOutline": ("points", 3), "lacks": ("noGeometry", None), "yardGrade": ("noGeometry", None)}
    assert plan_from_book.BUILD_STEPS == ("clusters", "fronts", "hands", "linkFrame", "levels", "floors", "walls", "overrides", "parameters", "status", "checkRows", "summary")
    assert "floors" in plan_from_book.PROTOTYPE_STEPS and "levels" not in plan_from_book.PROTOTYPE_STEPS
