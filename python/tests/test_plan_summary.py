"""R365, Q116. The plan summary of step 15 and the conflicts of the plan on the made up row of test_level_floor_steps.py built in code (R10),
which the C# of the worker gives the same (PlanSummaryTests.cs holds the same made up data)."""
import copy

import plan_from_book
from test_level_floor_steps import made_build
from test_override_param_steps import MADE


def test_r362_q116_a_plot_a_rotation_override_turned_to_another_open_edge_is_listed_in_the_conflicts_with_its_ramp_on_the_front(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE), back_dry=True)
    conflicts = built["trace"]["conflicts"]
    # 2 is turned by about 180 degrees, so its walls leave open the back edge, where no road level is found, so its ramp stays on the front
    # of R355 (Q116), 6 turned by 1.5 degrees keeps the front open and is no conflict
    assert conflicts == [{"plot": "2", "field": "floors.ramp", "kept": plan_from_book.TURNED_KEPT, "other": plan_from_book.TURNED_OTHER,
                          "otherSource": "override rotationDeg", "question": "Q116"}]
    assert built["plan"]["2"]["overrides"]["applied"].count("rotationDeg") == 1 and "ramp" in built["plan"]["2"]["floors"] and built["conflicts"] == conflicts
    # With no rotation override no plot is listed
    assert made_build(monkeypatch)["trace"]["conflicts"] == []


def summary_of(built):
    plan = {"plots": built["plots"], "summary": built["summary"], "conflicts": built["conflicts"]}
    return plan_from_book.summary_step(built["scope"], built["rows"], plan)


def test_r365_r401_parity_summary_the_step_holds_the_counts_and_names_of_the_plan_summary_and_the_conflicts(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE), back_dry=True)
    plots, tables = summary_of(built)
    summary, rules = built["summary"], built["summary"]["rules"]
    # One source: the table is the summary the plan writes, the counts of step 15 and the status of R401 beside them
    assert tables == {"summary": {"frontRule": summary["frontRule"], "roadSurfacesOfFrontCorners": summary["roadSurfacesOfFrontCorners"], "floorsFound": summary["floorsFound"],
                                  "floorTypes": summary["floorTypes"], "handRules": rules["handRules"], "overridesNotApplied": rules["overridesNotApplied"],
                                  "steps": rules["steps"], "statusByField": rules["statusByField"], "assumedByField": rules["assumedByField"],
                                  "statusPlots": rules["statusPlots"], "conflicts": built["conflicts"], "noDoorLevel": None, "slopeOutsideManual": None}}
    # R407. The lists of rev6 are null in the table of a build of the past job, whose plan holds neither
    assert "noDoorLevel" not in summary and "slopeOutsideManual" not in summary
    assert list(tables["summary"]) == list(plan_from_book.SUMMARY_TABLE) + list(plan_from_book.SUMMARY_REV6)
    # By hand on the made row: six unit plots written, 7 no row, the front of each by its corner rule and the six with two road levels
    assert plots["7"] == {"unit": None, "written": False} and all(plots[number] == {"unit": built["rows"][number]["typologyRev9"], "written": True} for number in "123456")
    assert sum(tables["summary"]["roadSurfacesOfFrontCorners"].values()) == 12 and tables["summary"]["floorsFound"]["generic"] == 6
    assert tables["summary"]["overridesNotApplied"] == {"foo": 1, "genericFloor": 1, "insert": 1, "mirrored": 1, "rampFrontFallM": 1, "rampOutline": 1, "yardOutline": 1}
    # Review 1 of PR 88: every value PlanSummaryTests.cs asserts, so the made row holds both languages to one summary
    table = tables["summary"]
    assert table["frontRule"] == {"nearestCurb": 5, "orientation": 1} and table["roadSurfacesOfFrontCorners"] == {"road": 12}
    assert table["floorsFound"] == {"generic": 6, "yard": 3, "ramp": 6}
    assert table["floorTypes"] == {"generic: Generic 150mm": 6, "ramp: Flat Ramps": 1, "ramp: Ramps 0.5%": 5, "yard: Yard made up": 3}
    assert table["handRules"] == {"blockMirrorRoadSide": 1, "clusterSide": 2, "override": 1, "rowPairSideOrBlockMirrorOpenEnd": 2}
    assert table["steps"] == {"from": "keyRules.steps stepsFromHeight of parsons2024Rev6", "parameters": ["RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name"]}
    # Q120: the mirror flag of 4 touches only the outline and type of its yard, so its level and plotValue are no longer assumed
    assert (sum(sum(counts.values()) for counts in table["statusByField"].values()), len(table["statusByField"]), len(table["assumedByField"])) == (242, 44, 28)
    assert "floors.yard.level" not in table["assumedByField"] and "floors.yard.plotValue" not in table["assumedByField"]
    assert [table["assumedByField"][field] for field in ("floors.yard.outline", "mirrored", "parameters.RRE_ZZ_SP_Number-of-Steps", "rotationDeg")] == [
        ["2", "4", "5"], ["4"], ["1", "2", "3", "4", "6"], ["2", "4", "6"]]
    assert table["statusPlots"] == {"CHOSEN by Bader": 6, "DOCUMENT": 6, "MEASURED strong": 6, "MEASURED weak": 6, "RECOMMENDED": 5, "UNKNOWN": 6, "assumed": 6}
    assert table["statusByField"]["levels.roadAtFrontM"] and [item["plot"] for item in table["conflicts"]] == ["2"]


def test_r365_parity_summary_compares_every_count_and_name_of_the_table_exact(monkeypatch):
    import parity
    import project_profile
    settings = project_profile.load_settings().values
    plots, tables = summary_of(made_build(monkeypatch, copy.deepcopy(MADE)))
    entries = plan_from_book.step_entries(project_profile.load_settings(), "summary", plots)
    python = parity.step_file("summary", "python", "made", entries, settings, [], tables=tables)
    other = copy.deepcopy(tables)
    other["summary"]["handRules"]["override"] += 1
    other["summary"]["statusPlots"]["moved"] = 1
    report = parity.compare(python, parity.step_file("summary", "csharp", "made", entries, settings, [], tables=copy.deepcopy(tables)), parity.tolerances(settings))
    assert report["failures"] == [] and report["tables"]["summary"] == {"equal": 13, "differ": 0}
    report = parity.compare(python, parity.step_file("summary", "csharp", "made", entries, settings, [], tables=other), parity.tolerances(settings))
    assert [(item["field"], item["index"], item["why"]) for item in report["failures"]] == [("tables.summary", "handRules", "not equal"), ("tables.summary", "statusPlots", "not equal")]
    assert "summary" in plan_from_book.BUILD_STEPS and "summary" in parity.LISTED_STEPS
