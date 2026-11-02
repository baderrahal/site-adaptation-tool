"""R481, Q265. A wall a build parts by keyRules.wallOpen carries the source wallOpen on its ends, so walls.ends reads MEASURED weak by that rule, on faces made up in
code (R10). R481_WallEndsTests.cs of the C# holds the same faces and the same values. Every other wall reads as it did."""
import plan_walls
import project_profile
import value_status
from test_plan_walls import FFLS, FRAMES, KIN, OPEN, RINGS, RULES
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build
from test_r415_r416_irregular_walls import irregular_four
from test_r480_c10_side import PAIR, PAIR_FRAMES, built

UNKNOWN_KEY = "walls.ends from keyRules"
OPEN_KEY = "walls.ends from wallOpen"


def table():
    settings = project_profile.load_settings()
    return value_status.table_of(settings, project_profile.load_profile(project_profile.profile_path(settings)), "parsons2024Rev6")


def marked(wall, applied=None, within=None):
    item = plan_walls.entry(wall)
    plan_walls.mark(item, wall, within or table(), applied or {})
    return item


def found(rules=OPEN, kin=KIN, frames=FRAMES):
    return plan_walls.walls(["1", "2", "5"], set(FFLS), RINGS, frames, FFLS, rules, frames, kin)


def pair(walls):
    return [wall for wall in walls if wall["plots"] == ["1", "2"]]


def test_r481_both_parts_of_a_wall_the_wallopen_table_parts_read_measured_weak_by_that_rule():
    within = table()
    parts = pair(found())
    assert len(parts) == 2
    for wall in parts:
        item = marked(wall, within=within)
        assert (item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("wallOpen", "MEASURED", OPEN_KEY)
        assert value_status.label(item["ends"], within.used) == "MEASURED weak"
    entries = within.used[OPEN_KEY]["entries"]
    assert within.used[OPEN_KEY]["strength"] == "weak"
    assert "R481" in entries[0]["why"] and "Q265" in entries[0]["why"] and "keyRules.wallOpen" in entries[0]["why"] and "0.7409" in entries[0]["why"]
    named = {entry["entry"].split(" ", 1)[1]: entry for entry in entries if "entry" in entry}
    # The share of the block stands beside the why, so the text and the block can be held against each other
    assert named["levelProfiles.parsons2024Rev6.keyRules.wallOpen.rule"]["share"] == 0.7409
    assert named["levelProfiles.parsons2024Rev6.keyRules.wallOpen.rule"]["strength"] == "weak"
    assert not any(entry["status"] == "UNKNOWN" for entry in entries)


def test_r481_a_whole_wall_stays_unknown_and_every_other_field_of_a_parted_wall_reads_as_before():
    within = table()
    walls = found()
    whole = [wall for wall in walls if wall["plots"] != ["1", "2"]]
    assert whole
    for wall in whole:
        item = marked(wall, within=within)
        assert (item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("keyRules", "UNKNOWN", UNKNOWN_KEY)
    parted, plain = marked(pair(walls)[0], within=within), marked(pair(found(RULES))[0], within=within)
    assert all(parted[field] == plain[field] for field in ("plots", "base", "top"))
    assert plain["ends"]["statusFrom"] == UNKNOWN_KEY


def test_r481_a_pair_the_table_lacks_and_a_profile_with_no_wallopen_leave_the_wall_whole_and_unknown():
    other = dict(KIN, **{"2": (0, "TH1")})
    for walls in (found(kin=other), found(RULES), found(dict(RULES, open=None))):
        assert len(pair(walls)) == 1 and pair(walls)[0]["parted"] is False
        assert marked(pair(walls)[0])["ends"]["source"] == "keyRules"


def test_r481_a_gap_clear_of_the_part_parts_nothing_so_the_part_stays_keyrules():
    # The edge of 1 and 2 runs 20 m, a span from 25 m to 30 m lies past its end, the table holds the pair and the wall stays whole
    walls = found(dict(RULES, open={"DT3|DT3": (25.0, 30.0), "DT3|VD4": (6.0, 21.0)}))
    assert [wall["ends"] for wall in pair(walls)] == [[[10.0, 0.0], [10.0, 20.0]]]
    assert pair(walls)[0]["parted"] is False and marked(pair(walls)[0])["ends"]["source"] == "keyRules"


def test_r481_a_part_of_the_r480_side_span_keeps_the_old_source():
    # The side span of R480 parts this edge and the wallOpen table holds no pair of it
    walls = built(PAIR, PAIR_FRAMES, {"1": (0, "U1"), "0": (0, "U2")}, {"1": False, "0": False})
    parts = [wall for wall in walls if wall["plots"] == ["0", "1"]]
    assert len(parts) == 2
    for wall in parts:
        item = marked(wall)
        assert wall["parted"] is False and (item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("keyRules", "UNKNOWN", UNKNOWN_KEY)


def test_r481_a_moved_wall_of_r416_keeps_its_source_also_where_wallopen_parted_it():
    wall = dict(pair(found())[0], lineInsideM=0.1)
    item = marked(wall)
    assert (item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("manualInside", "UNKNOWN", "walls.ends from manualInside")
    still = dict(pair(found())[0], lineInsideM=0.0)
    assert marked(still)["ends"]["source"] == "wallOpen"


def test_r481_a_turned_plot_on_a_parted_wall_reads_the_override_as_before():
    # Line 1011 of the settings, overrideReads rotationDeg walls.ends, holds no source, so the status of a turned wall stays that of before and only the source names wallOpen
    wall = dict(pair(found())[0], turned=["1"])
    plain = dict(pair(found(RULES))[0], turned=["1"])
    item, before = marked(wall, {"1": ("rotationDeg",)}), marked(plain, {"1": ("rotationDeg",)})
    assert (item["ends"]["status"], item["ends"]["statusFrom"]) == (before["ends"]["status"], before["ends"]["statusFrom"])
    assert (item["ends"]["status"], item["ends"]["statusFrom"]) == ("UNKNOWN", "walls.ends from override by rotationDeg")
    assert item["ends"]["source"] == "wallOpen"


def test_r481_the_settings_hold_walls_ends_from_wallopen_at_measured_weak_and_the_other_sources_as_they_were():
    ends = project_profile.load_settings().get("valueStatus")["fields"]["walls.ends"]
    assert list(ends) == ["keyRules", "manualInside", "wallOpen"]
    assert ends["wallOpen"][0]["status"] == "MEASURED" and ends["wallOpen"][0]["strength"] == "weak" and ends["wallOpen"][0]["question"] == "Q265"
    assert ends["keyRules"][-1]["status"] == "UNKNOWN" and ends["manualInside"][-1]["status"] == "UNKNOWN"


def test_r481_the_step_walls_names_the_source_of_each_ends_and_the_settings_compare_it(monkeypatch):
    irregular_four(monkeypatch)
    made_build = rev6_build(monkeypatch, basis_settings(mode="rev6"))
    made, _ = plan_walls.step(made_build["scope"], made_build["rows"], made_build["trace"])
    assert {number: made[number]["endsSource"] for number in ("3", "4", "7")} == {"3": ["keyRules", "manualInside"], "4": ["manualInside", "manualInside"], "7": []}
    assert project_profile.load_settings().get("addin")["parity"]["steps"]["walls"]["endsSource"] == {"kind": "noGeometry"}


def test_r481_the_build_gives_a_parted_wall_the_source_under_rev6_and_the_past_job_and_every_other_wall_stays_unknown(monkeypatch):
    # Plots 2 and 3 a pair of TH1-IM, which keyRules.wallOpen of the rules profile holds, so their shared edge of 25 m has two parts
    for mode in ("rev6", PAST_JOB):
        made_build = rev6_build(monkeypatch, basis_settings(mode=mode), units={"2": "TH1-IM", "3": "TH1-IM"})
        walls = made_build["trace"]["walls"]
        parted = [item for item in walls if item["plots"]["value"] == ["2", "3"]]
        assert len(parted) == 2
        assert all((item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("wallOpen", "MEASURED", OPEN_KEY) for item in parted)
        rest = [item for item in walls if item["plots"]["value"] != ["2", "3"]]
        assert rest and all((item["ends"]["source"], item["ends"]["status"], item["ends"]["statusFrom"]) == ("keyRules", "UNKNOWN", UNKNOWN_KEY) for item in rest)
        made, tables = plan_walls.step(made_build["scope"], made_build["rows"], made_build["trace"])
        assert made["2"]["endsSource"].count("wallOpen") == 2 and OPEN_KEY in tables["statusFrom"]
