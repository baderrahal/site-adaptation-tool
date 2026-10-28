"""R365, Q116. The plan summary of step 15 and the conflicts of the plan on the made up row of test_level_floor_steps.py built in code (R10),
which the C# of the worker gives the same (PlanSummaryTests.cs holds the same made up data)."""
import copy

import plan_from_book
from test_level_floor_steps import made_build
from test_override_param_steps import MADE


def test_r362_q116_a_plot_a_rotation_override_turned_to_another_open_edge_is_listed_in_the_conflicts_with_its_ramp_on_the_front(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE))
    conflicts = built["trace"]["conflicts"]
    # 2 is turned by about 180 degrees, so its walls leave open the back edge while its ramp stays on the front of R355, 6 turned by 1.5
    # degrees keeps the front open and is no conflict
    assert conflicts == [{"plot": "2", "field": "floors.ramp", "kept": plan_from_book.TURNED_KEPT, "other": plan_from_book.TURNED_OTHER,
                          "otherSource": "override rotationDeg", "question": "Q116"}]
    assert built["plan"]["2"]["overrides"]["applied"].count("rotationDeg") == 1 and "ramp" in built["plan"]["2"]["floors"] and built["conflicts"] == conflicts
    # With no rotation override no plot is listed
    assert made_build(monkeypatch)["trace"]["conflicts"] == []


def summary_of(built):
    plan = {"plots": built["plots"], "summary": built["summary"], "conflicts": built["conflicts"]}
    return plan_from_book.summary_step(built["scope"], built["rows"], plan)


def test_r365_r401_parity_summary_the_step_holds_the_counts_and_names_of_the_plan_summary_and_the_conflicts(monkeypatch):
    built = made_build(monkeypatch, copy.deepcopy(MADE))
    plots, tables = summary_of(built)
    summary, rules = built["summary"], built["summary"]["rules"]
    # One source: the table is the summary the plan writes, the counts of step 15 and the status of R401 beside them
    assert tables == {"summary": {"frontRule": summary["frontRule"], "roadSurfacesOfFrontCorners": summary["roadSurfacesOfFrontCorners"], "floorsFound": summary["floorsFound"],
                                  "floorTypes": summary["floorTypes"], "handRules": rules["handRules"], "overridesNotApplied": rules["overridesNotApplied"],
                                  "steps": rules["steps"], "statusByField": rules["statusByField"], "assumedByField": rules["assumedByField"],
                                  "statusPlots": rules["statusPlots"], "conflicts": built["conflicts"]}}
    assert list(tables["summary"]) == list(plan_from_book.SUMMARY_TABLE)
    # By hand on the made row: six unit plots written, 7 no row, the front of each by its corner rule and the six with two road levels
    assert plots["7"] == {"unit": None, "written": False} and all(plots[number] == {"unit": built["rows"][number]["typologyRev9"], "written": True} for number in "123456")
    assert sum(tables["summary"]["roadSurfacesOfFrontCorners"].values()) == 12 and tables["summary"]["floorsFound"]["generic"] == 6
    assert tables["summary"]["overridesNotApplied"] == {"foo": 1, "genericFloor": 1, "insert": 1, "mirrored": 1, "rampFrontFallM": 1, "rampOutline": 1, "yardOutline": 1}
    assert sum(tables["summary"]["handRules"].values()) == 6 and tables["summary"]["handRules"]["override"] == 1
    assert tables["summary"]["statusByField"]["levels.roadAtFrontM"] and tables["summary"]["conflicts"][0]["plot"] == "2"


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
    assert report["failures"] == [] and report["tables"]["summary"] == {"equal": 11, "differ": 0}
    report = parity.compare(python, parity.step_file("summary", "csharp", "made", entries, settings, [], tables=other), parity.tolerances(settings))
    assert [(item["field"], item["index"], item["why"]) for item in report["failures"]] == [("tables.summary", "handRules", "not equal"), ("tables.summary", "statusPlots", "not equal")]
    assert "summary" in plan_from_book.BUILD_STEPS and "summary" in parity.LISTED_STEPS
