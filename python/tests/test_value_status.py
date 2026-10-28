"""R401, the plan part. The status beside each sourced value of a plot of the plan, taken once per build from the block status of the rules
profile through valueStatus of config/project.json, its detail written once in the table statusFrom at the top of the plan, its count by
field and status in the summary of step 15 and the step status of the parity harness, on a made up map and block and on the made up row of
test_level_floor_steps.py, built in code (R10). ValueStatusTests.cs holds the same made up data for the C#."""
import copy

import pytest

import plan_from_book
import project_profile
import test_level_floor_steps
import value_status
from test_override_param_steps import MADE

PROFILE_FILE = "rules/made.json"
OVERRIDE = {"status": "RECOMMENDED", "question": "Q900", "where": "a made up list"}
# A made up map: an override and a client input by their source, a field by one block entry or by a list of them, a parameter by its name
MAP = {"sources": {"override": OVERRIDE, "locationPlan": {"status": "DOCUMENT", "document": "a made up plan"}},
       "fields": {"levels.entry": {"estimate": [{"profile": "levelProfiles.{levelProfile}.keyRules.entry.rule"}]},
                  "levels.ffl": {"estimate": {"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}},
                  "levels.height": {"estimate": [{"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}, {"profile": "levelProfiles.{levelProfile}.keyRules.entry.rule"}]},
                  "insert": {"computed": [{"profile": "levelProfiles.{levelProfile}.keyRules.hand {hand}.rule"}, {"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}]},
                  "mirrored": {"computed": {"profile": "levelProfiles.{levelProfile}.keyRules.hand {hand}.rule"}},
                  "parameters.*": {"keyRules": {"profile": "levelProfiles.{levelProfile}.keyRules.parameter {parameter}.rule"}},
                  "parameters.CASE": {"keyRules": {"config": "made.caseRule"}},
                  "floors.generic.type": {"computed": {"config": "floors.types.generic"}},
                  "floors.generic.level": {"computed": {"profile": "levelProfiles.{levelProfile}.keyRules.notInTheBlock"}}},
       "overrideSets": {"entryM": ["levels.entry", "levels.height"], "heightM": ["levels.ffl", "insert"]},
       "overrideReads": {"entryM": {"levels.height": [{"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}]}}, "movedBy": {},
       "assumed": ["RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"], "worstFirst": ["UNKNOWN", "CHOSEN by Code", "RECOMMENDED", "MEASURED weak"]}
ENTRY = {"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.entry.rule", "status": "MEASURED", "share": 0.5, "strength": "weak", "measuredBy": "levelProfiles.p1.keyRules.entry"}
FFL = {"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.ffl.rule", "status": "MEASURED", "share": 0.95, "strength": "strong", "measuredBy": "levelProfiles.p1.keyRules.ffl"}
HAND = {"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.hand alone.rule", "status": "MEASURED", "share": 1.0, "strength": "strong",
        "measuredBy": "levelProfiles.p1.keyRules.hand alone"}
BLOCK = {"note": "a made up block",
         PROFILE_FILE: {"levelProfiles.p1.keyRules.entry.rule": {key: value for key, value in ENTRY.items() if key != "entry"},
                        "levelProfiles.p1.keyRules.ffl.rule": {key: value for key, value in FFL.items() if key != "entry"},
                        "levelProfiles.p1.keyRules.hand alone.rule": {key: value for key, value in HAND.items() if key != "entry"},
                        "levelProfiles.p1.keyRules.parameter X.rule": {"status": "CHOSEN by Bader", "question": "Q901", "where": "made up"}},
         "config/project.json": {"floors.types.generic": {"status": "UNKNOWN", "why": "no Q", "nearestSource": "floors.note"},
                                 "made.caseRule": {"status": "CHOSEN by Code", "where": "note"}}}


def made_table(level_profile="p1", shape=None):
    return value_status.Table(dict(MAP, **(shape or {})), BLOCK, PROFILE_FILE, level_profile)


def test_r401_a_value_takes_the_status_of_the_block_entry_its_field_and_source_name_with_the_entry_and_its_evidence():
    table = made_table()
    assert table.of("levels.entry", "estimate") == ("MEASURED", {"entries": [ENTRY], "strength": "weak"}, "levels.entry from estimate")
    assert table.of("levels.ffl", "estimate") == ("MEASURED", {"entries": [FFL], "strength": "strong"}, "levels.ffl from estimate")
    # The hand table of the plot names the keyRules entry that gave its mirror flag, and the key of the detail names the hand
    assert table.of("mirrored", "computed", hand="alone") == ("MEASURED", {"entries": [HAND], "strength": "strong"}, "mirrored from computed hand alone")
    assert table.of("parameters.X", "keyRules") == ("CHOSEN by Bader", {"entries": [{"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.parameter X.rule",
                                                                                     "status": "CHOSEN by Bader", "question": "Q901", "where": "made up"}]},
                                                    "parameters.X from keyRules")
    # A field of its own goes before parameters.*, and config names an entry of the settings
    assert table.of("parameters.CASE", "keyRules")[:2] == ("CHOSEN by Code", {"entries": [{"entry": "config/project.json made.caseRule", "status": "CHOSEN by Code", "where": "note"}]})
    assert table.of("floors.generic.type", "computed")[0] == "UNKNOWN"


def test_r401_a_field_of_several_block_entries_takes_the_worst_of_them_and_lists_each():
    table = made_table()
    # ffl strong, then El @ Entry weak: the weak one is the first assumed
    assert table.of("levels.height", "estimate") == ("MEASURED", {"entries": [FFL, ENTRY], "strength": "weak"}, "levels.height from estimate")
    # The hand and the FFL both strong: the first
    assert table.of("insert", "computed", hand="alone") == ("MEASURED", {"entries": [HAND, FFL], "strength": "strong"}, "insert from computed hand alone")
    # A plot with no hand table: its entry UNKNOWN, which is assumed, so the value is UNKNOWN with each entry listed
    status, detail, key = table.of("insert", "computed")
    assert (status, key) == ("UNKNOWN", "insert from computed hand None") and "strength" not in detail
    assert detail["entries"] == [{"entry": "levelProfiles.{levelProfile}.keyRules.hand {hand}.rule", "status": "UNKNOWN", "why": "the plot holds no hand (R401)"}, FFL]


def test_r401_q107_a_value_of_several_entries_takes_the_worst_status_in_the_order_of_valueStatus_worst_first():
    """Q107. The worst status of the list in the order of worstFirst, then the rest in the order the list holds them, where the first assumed
    entry gave MEASURED weak to a value that also reads an entry CHOSEN by Code."""
    case = {"config": "made.caseRule"}
    shape = {"fields": dict(MAP["fields"], **{"levels.ssl": {"estimate": [{"profile": "levelProfiles.{levelProfile}.keyRules.entry.rule"}, case]},
                                              "levels.steps": {"estimate": [{"profile": "levelProfiles.{levelProfile}.keyRules.parameter X.rule"},
                                                                            {"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}]}})}
    table = made_table(shape=shape)
    status, found, _ = table.of("levels.ssl", "estimate")
    assert (status, [entry["status"] for entry in found["entries"]], "strength" in found) == ("CHOSEN by Code", ["MEASURED", "CHOSEN by Code"], False)
    # No entry of the order: the first of the list, CHOSEN by Bader before MEASURED strong
    assert table.of("levels.steps", "estimate")[0] == "CHOSEN by Bader"
    assert table.of("levels.height", "estimate")[1] == {"entries": [FFL, ENTRY], "strength": "weak"}
    # UNKNOWN before all
    assert table.of("insert", "computed")[0] == "UNKNOWN"


def test_r401_q107_a_missing_status_order_or_one_holding_an_unknown_status_is_refused_with_one_line():
    line = ("valueStatus.worstFirst lists the statuses a value of several block entries takes first, the worst first, each of UNKNOWN, CHOSEN by Code, "
            "CHOSEN by Bader, RECOMMENDED, MEASURED weak, MEASURED strong, MEASURED or DOCUMENT, the settings hold %s (R401)")
    for order, held in ((None, "none"), (["UNKNOWN", "WRONG"], '["UNKNOWN", "WRONG"]'), ("UNKNOWN", '"UNKNOWN"'), ([], "[]")):
        with pytest.raises(project_profile.ProfileError) as refused:
            made_table(shape={"worstFirst": order})
        assert str(refused.value) == line % held
    no_order = {key: value for key, value in MAP.items() if key != "worstFirst"}
    with pytest.raises(project_profile.ProfileError) as refused:
        value_status.Table(no_order, BLOCK, PROFILE_FILE, "p1")
    assert str(refused.value) == line % "none"
    import rule_status
    assert set(value_status.LABELS) == set(rule_status.ORDER) | {"MEASURED weak", "MEASURED strong"}


def test_r401_q107_the_detail_names_the_override_applied_last_whose_set_holds_the_field():
    """Q107. applied in the order the build applied them: the last whose set holds the field moved the value, as heightM sets the height
    after entryM moved El @ Entry, where the first applied override was named."""
    table = made_table(shape={"overrideSets": {"entryM": ["levels.entry", "levels.height"], "heightM": ["levels.ffl", "levels.height", "insert"]}})
    assert table.of("levels.height", "estimate", applied=["entryM", "heightM"])[1:] == ({"entries": [OVERRIDE], "override": "heightM"}, "levels.height from override by heightM")
    assert table.of("levels.height", "estimate", applied=["heightM", "entryM"])[2] == "levels.height from override by entryM"
    assert table.of("levels.entry", "estimate", applied=["entryM", "heightM"])[2] == "levels.entry from override by entryM"


def test_r401_an_override_and_a_client_input_take_the_status_the_map_gives_their_source():
    table = made_table()
    assert table.of("parameters.X", "override") == ("RECOMMENDED", {"entries": [OVERRIDE]}, "parameters.X from override")
    assert table.of("unit", "locationPlan") == ("DOCUMENT", {"entries": [{"status": "DOCUMENT", "document": "a made up plan"}]}, "unit from locationPlan")


def test_r401_an_override_marks_every_field_it_moves_with_the_status_of_the_override_list():
    table = made_table()
    assert table.of("levels.entry", "estimate", applied=["entryM", "heightM"]) == ("RECOMMENDED", {"entries": [OVERRIDE], "override": "entryM"},
                                                                                  "levels.entry from override by entryM")
    assert table.of("levels.height", "estimate", applied=["entryM"])[0] == "RECOMMENDED"
    # heightM moves the Z of the insert through the FFL
    assert table.of("insert", "computed", hand="alone", applied=["heightM"])[1] == {"entries": [OVERRIDE], "override": "heightM"}
    # An override that moves other fields leaves the value its own status
    assert table.of("levels.ffl", "estimate", applied=["entryM"])[0] == "MEASURED"


def test_r401_an_override_set_of_another_shape_is_refused_with_one_line():
    line = "valueStatus.overrideSets maps each override of the list to the list of the fields it moves, the settings hold %s (R401)"
    for shape, held in (({"overrideSets": {"entryM": "levels.entry"}}, 'entryM: "levels.entry"'), ({"overrideSets": None}, "none"),
                        ({"overrideSets": {"entryM": ["levels.entry", 3]}}, 'entryM: ["levels.entry", 3]')):
        with pytest.raises(project_profile.ProfileError) as refused:
            made_table(shape=shape)
        assert str(refused.value) == line % held
    no_sets = {key: value for key, value in MAP.items() if key != "overrideSets"}
    with pytest.raises(project_profile.ProfileError):
        value_status.Table(no_sets, BLOCK, PROFILE_FILE, "p1")


def test_r401_a_pair_the_map_does_not_hold_or_an_entry_the_block_lacks_is_unknown_never_a_guess():
    table = made_table()
    unknown = {"status": "UNKNOWN", "why": "valueStatus holds no status for prototypeFile from computed (R401)"}
    assert table.of("prototypeFile", "computed") == ("UNKNOWN", {"entries": [unknown]}, "prototypeFile from computed")
    assert table.of("levels.entry", "keyRules")[0] == "UNKNOWN"
    assert table.of("floors.generic.level", "computed")[1] == {"entries": [{"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.notInTheBlock", "status": "UNKNOWN",
                                                                            "why": "the block status holds no entry (R401)"}]}
    assert table.of("mirrored", "computed") == ("UNKNOWN", {"entries": [{"entry": "levelProfiles.{levelProfile}.keyRules.hand {hand}.rule", "status": "UNKNOWN",
                                                                         "why": "the plot holds no hand (R401)"}]}, "mirrored from computed hand None")
    assert made_table("p2").of("levels.entry", "estimate")[0] == "UNKNOWN"
    # No map and no block give every value UNKNOWN
    assert value_status.Table(None, {}, PROFILE_FILE, "p1").of("unit", "locationPlan")[0] == "UNKNOWN"


def test_r401_a_value_holds_its_status_and_the_key_of_its_detail_written_once_in_the_table():
    entry = {"plot": "1", "unit": {"value": "VD2", "source": "locationPlan"}, "levels": {"entry": {"value": 10.1, "source": "estimate"}},
             "parameters": {"X": {"value": "a", "source": "keyRules"}}, "floors": {"generic": {"type": {"value": "G", "source": "computed"}}},
             "overrides": {"applied": [], "notApplied": {}}, "roadSurfaces": ["road"]}
    table = made_table()
    value_status.mark(entry, table, hand=None, applied=[])
    assert list(value_status.fields_of(entry)) == ["unit", "levels.entry", "parameters.X", "floors.generic.type"]
    assert entry["levels"]["entry"] == {"value": 10.1, "source": "estimate", "status": "MEASURED", "statusFrom": "levels.entry from estimate"}
    assert table.used["levels.entry from estimate"] == {"entries": [ENTRY], "strength": "weak"} and len(table.used) == 4
    assert [value_status.label(item, table.used) for item in value_status.fields_of(entry).values()] == ["DOCUMENT", "MEASURED weak", "CHOSEN by Bader", "UNKNOWN"]
    assert entry["overrides"] == {"applied": [], "notApplied": {}} and entry["roadSurfaces"] == ["road"]


def test_r401_the_summary_counts_plots_by_field_and_status_and_lists_the_assumed_plots_by_field():
    plots, table = [], made_table()
    for number in ("1", "2", "10"):
        entry = {"plot": number, "unit": {"value": "VD2", "source": "locationPlan"}, "levels": {"entry": {"value": 10.1, "source": "estimate"},
                                                                                             "ffl": {"value": 10.5, "source": "estimate"}}}
        value_status.mark(entry, table, applied=["entryM"] if number == "10" else [])
        plots.append(entry)
    found = value_status.summary(plots, MAP["assumed"], table.used)
    assert found["statusByField"] == {"levels.entry": {"MEASURED weak": 2, "RECOMMENDED": 1}, "levels.ffl": {"MEASURED strong": 3}, "unit": {"DOCUMENT": 3}}
    assert found["assumedByField"] == {"levels.entry": ["1", "2", "10"]}
    assert found["statusPlots"] == {"DOCUMENT": 3, "MEASURED strong": 3, "MEASURED weak": 2, "RECOMMENDED": 1, "assumed": 3}
    line = plan_from_book.status_line(found)
    assert line == "plots by the status of their values (R401): DOCUMENT 3, MEASURED strong 3, MEASURED weak 2, RECOMMENDED 1, an assumed value 3, in the fields levels.entry"


KEY = "levelProfiles.parsons2024Rev6.keyRules."
WEAK, STRONG = {"status": "MEASURED", "share": 0.5, "strength": "weak", "measuredBy": "made"}, {"status": "MEASURED", "share": 0.97, "strength": "strong", "measuredBy": "made"}
# Q110. The keo_file of the DP2 typology, so the DP2 plots of the made row take a status for prototypeFile and the VD2 plot, whose keo_file the
# made block lacks, is UNKNOWN
KEO = {"status": "DOCUMENT", "document": "a made up document", "pageOrSheet": "UNKNOWN", "source": "source"}
# The block of the made row in place of the real one, so no status of the made row follows a real share. Every other entry the map names is
# missing from it, so UNKNOWN
MADE_BLOCK = {KEY + "entry.rule": WEAK, KEY + "ffl.rule": STRONG, KEY + "steps.rule": STRONG, KEY + "hand villa.rule": STRONG, KEY + "hand cluster.rule": STRONG,
              KEY + "hand alone.rule": STRONG, KEY + "linkOrigin.rule": STRONG, KEY + "linkRotation.rule": STRONG, KEY + "parameter CASE.rule": WEAK,
              KEY + "parameter RRE_ZZ_SP_RVT-Name.rule": {"status": "CHOSEN by Bader", "question": "Q902", "where": "made up"},
              KEY + "rampSlope.rule": {"status": "CHOSEN by Code", "where": "a made up note"}, "typologies.DP2.keo_file": KEO}
MADE_CONFIG_BLOCK = {"floors.types.generic": {"status": "UNKNOWN", "why": "made up", "nearestSource": "floors.note"}}
# The plots of the made row by field and status with MADE, the made block and the map of config/project.json, which ValueStatusTests.cs holds the same.
# Since Q107 the steps and the fields of the steps read steps.riser_added and each heightOffset the elevation of its level, which the made
# block lacks or no entry maps, so they are UNKNOWN as the worst, and a field an override left at its value by the rule names no override
MADE_COUNTS = ("floors.generic.heightOffset:UNKNOWN=6 floors.generic.level:UNKNOWN=6 floors.generic.levelElevation:UNKNOWN=6 "
               "floors.generic.outline:UNKNOWN=6 floors.generic.plotValue:DOCUMENT=6 floors.generic.type:UNKNOWN=6 "
               "floors.generic.z:UNKNOWN=6 floors.ramp.corners:UNKNOWN=6 floors.ramp.heightOffset:UNKNOWN=6 floors.ramp.level:UNKNOWN=6 "
               "floors.ramp.levelElevation:UNKNOWN=6 floors.ramp.outline:UNKNOWN=6 floors.ramp.plotValue:DOCUMENT=6 "
               "floors.ramp.slopePercent:CHOSEN by Code=6 floors.ramp.type:CHOSEN by Code=5 floors.ramp.type:RECOMMENDED=1 "
               "floors.yard.heightOffset:UNKNOWN=3 floors.yard.level:UNKNOWN=3 floors.yard.levelElevation:UNKNOWN=3 "
               "floors.yard.outline:UNKNOWN=3 floors.yard.plotValue:DOCUMENT=3 floors.yard.type:UNKNOWN=3 floors.yard.z:UNKNOWN=3 "
               "insert:MEASURED strong=3 insert:RECOMMENDED=3 levels.entry:MEASURED weak=5 levels.entry:RECOMMENDED=1 "
               "levels.ffl:RECOMMENDED=1 levels.ffl:UNKNOWN=5 levels.height:MEASURED weak=4 levels.height:RECOMMENDED=2 "
               "levels.ssl:UNKNOWN=6 levels.steps:RECOMMENDED=1 levels.steps:UNKNOWN=5 linkName:RECOMMENDED=2 linkName:UNKNOWN=4 "
               "mirrored:MEASURED strong=5 mirrored:RECOMMENDED=1 parameters.CASE:MEASURED weak=4 parameters.CASE:RECOMMENDED=2 "
               "parameters.RRE_ZZ_SP_Façade-Color:UNKNOWN=6 parameters.RRE_ZZ_SP_Façade-Design-Option:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_Number-of-Steps:UNKNOWN=5 parameters.RRE_ZZ_SP_Plot-Number:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_Prototype:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-FFL:RECOMMENDED=1 "
               "parameters.RRE_ZZ_SP_Prototype-FFL:UNKNOWN=5 parameters.RRE_ZZ_SP_Prototype-Foundation-Type:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type:UNKNOWN=6 parameters.RRE_ZZ_SP_RVT-Name:RECOMMENDED=2 "
               "parameters.RRE_ZZ_SP_RVT-Name:UNKNOWN=4 prototypeFile:DOCUMENT=5 prototypeFile:UNKNOWN=1 rotationDeg:MEASURED strong=3 rotationDeg:RECOMMENDED=3 "
               "unit:DOCUMENT=6")


def made_block_rules(monkeypatch):
    """made_rules with the made block in place of the real one."""
    real = test_level_floor_steps.made_rules

    def made(split=None):
        profile = real(split)
        profile.values["status"] = {"note": "a made up block", project_profile.load_settings().get("profile"): copy.deepcopy(MADE_BLOCK),
                                    "config/project.json": copy.deepcopy(MADE_CONFIG_BLOCK)}
        return profile
    monkeypatch.setattr(test_level_floor_steps, "made_rules", made)


def count_text(by_field):
    return " ".join("%s:%s=%d" % (field, name, count) for field, counts in sorted(by_field.items()) for name, count in sorted(counts.items()))


def detail(built, item):
    return built["detail"][item["statusFrom"]]


def made_row(monkeypatch, overrides):
    made_block_rules(monkeypatch)
    built = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(overrides))
    built["detail"] = built["trace"]["statusFrom"]
    return built


def test_r401_each_sourced_value_of_a_written_plot_holds_its_status_by_its_source_and_field(monkeypatch):
    built = made_row(monkeypatch, MADE)
    plan, name = built["plan"], project_profile.load_settings().get("profile")
    entry = plan["1"]["levels"]["entry"]
    assert (entry["status"], detail(built, entry)) == ("MEASURED", {"entries": [{"entry": name + " " + KEY + "entry.rule", **WEAK}], "strength": "weak"})
    # 2 moved by its entryM override, which takes the status of the override list with its key
    moved = plan["2"]["levels"]["entry"]
    assert (moved["source"], moved["status"], moved["statusFrom"]) == ("estimate", "RECOMMENDED", "levels.entry from override by entryM")
    assert detail(built, moved) == {"entries": [{"status": "RECOMMENDED", "question": "Q053", "where": "steps/investigations/rule-status-audit.md section 6"}], "override": "entryM"}
    # The hand of 3 by the table of hand cluster, 4 by its mirror flag override with its CASE, the CASE of 2 by its override
    assert detail(built, plan["3"]["mirrored"])["entries"][0]["entry"] == name + " " + KEY + "hand cluster.rule"
    assert [(plan["4"][field]["status"], detail(built, plan["4"][field]).get("override")) for field in ("mirrored", "insert", "rotationDeg")] == [
        ("RECOMMENDED", None), ("RECOMMENDED", "mirrored"), ("RECOMMENDED", "mirrored")]
    assert plan["4"]["parameters"]["CASE"]["status"] == "RECOMMENDED" and plan["2"]["parameters"]["CASE"]["source"] == "override"
    # The FFL of 3 moved by heightM moves the Z of its insert and its Prototype-FFL
    assert detail(built, plan["3"]["insert"])["override"] == "heightM" and plan["3"]["parameters"]["RRE_ZZ_SP_Prototype-FFL"]["source"] == "override"
    assert plan["1"]["unit"]["status"] == "DOCUMENT" and plan["1"]["prototypeFile"]["status"] == "UNKNOWN"
    assert all(item["statusFrom"] in built["detail"] for entry in built["plots"] for item in value_status.fields_of(entry).values())


def real_table(block):
    """The map of config/project.json on a made block of the rules profile."""
    settings = project_profile.load_settings()
    return value_status.Table(settings.get("valueStatus"), {settings.get("profile"): block}, settings.get("profile"), "parsons2024Rev6")


def test_r401_q107_review1_a_height_offset_takes_the_unknown_elevation_of_its_level_in_the_new_site_file():
    """R1 of the review of PR 75: floors.levels names the level, CHOSEN by Bader by Q104, not its elevation in the new site file, which
    heightOffset is computed from and no block entry maps, so each heightOffset list holds that elevation UNKNOWN as the field levelElevation
    resolves to, and a heightOffset of entries MEASURED weak beside it is UNKNOWN."""
    weak = {"status": "MEASURED", "share": 0.5, "strength": "weak"}
    table = real_table({KEY + name: dict(weak) for name in ("generic.rule", "yard.rule", "ffl.rule", "rampFront.rule", "entry.rule")})
    for kind in ("generic", "yard", "ramp"):
        status, detail, _ = table.of("floors.%s.heightOffset" % kind, "estimate")
        assert status == "UNKNOWN" and [entry["status"] for entry in detail["entries"]] == ["MEASURED", "MEASURED", "UNKNOWN"]
        assert not any("floors.levels" in entry.get("entry", "") for entry in detail["entries"]) and "elevation" in detail["entries"][2]["why"]
        assert table.of("floors.%s.levelElevation" % kind, "computed")[0] == "UNKNOWN"


def test_r401_q107_review1_a_field_an_override_moved_but_the_code_still_computes_takes_the_worst_of_the_override_and_what_it_reads():
    """R2 of the review of PR 75: the steps after heightM are still made by steps_by_rule with steps.riser_added, so they take the worst of
    the override status and the entries overrideReads names, where the override status alone gave RECOMMENDED. A field the override sets
    outright, the FFL by heightM, takes the override alone."""
    strong = {"status": "MEASURED", "share": 0.97, "strength": "strong"}
    block = {KEY + name: dict(strong) for name in ("steps.rule", "entry.rule", "ffl.rule")}
    status, detail, key = real_table(block).of("levels.steps", "estimate", applied=["heightM"])
    assert (status, key, detail["override"]) == ("UNKNOWN", "levels.steps from override by heightM", "heightM")
    assert [entry["status"] for entry in detail["entries"]] == ["RECOMMENDED", "MEASURED", "UNKNOWN", "MEASURED"]
    assert real_table(dict(block, **{"steps.riser_added": dict(strong)})).of("levels.steps", "estimate", applied=["heightM"])[0] == "RECOMMENDED"
    status, detail, _ = real_table(block).of("levels.ffl", "estimate", applied=["heightM"])
    assert status == "RECOMMENDED" and len(detail["entries"]) == 1
    # The made map: entryM recomputes the height from the FFL
    assert made_table().of("levels.height", "estimate", applied=["entryM"])[:2] == ("RECOMMENDED", {"entries": [OVERRIDE, FFL], "override": "entryM"})


def test_r401_q107_review1_an_override_reads_list_of_another_shape_or_of_a_field_its_set_does_not_hold_is_refused_with_one_line():
    line = ("valueStatus.overrideReads maps an override of overrideSets to the fields of its set the code still computes from other entries, each to "
            "the list of them, the settings hold %s (R401)")
    for reads, held in (({"entryM": {"levels.ffl": [{"profile": "a"}]}}, 'entryM: {"levels.ffl": [{"profile": "a"}]}'), (None, "none"),
                        ({"entryM": {"levels.height": {"profile": "a"}}}, 'entryM: {"levels.height": {"profile": "a"}}'), ({"other": {}}, "other: {}")):
        with pytest.raises(project_profile.ProfileError) as refused:
            made_table(shape={"overrideReads": reads})
        assert str(refused.value) == line % held
    with pytest.raises(project_profile.ProfileError) as refused:
        made_table(shape={"movedBy": {"levels.height": 3}})
    assert str(refused.value) == "valueStatus.movedBy maps a field to the field whose value tells whether an override moved it, the settings hold {\"levels.height\": 3} (R401)"


def test_r401_q107_review1_an_override_that_leaves_a_field_at_its_value_by_the_rule_did_not_move_it():
    """R3 of the review of PR 75: a field an applied override holds in its set but leaves equal to the value the build gives with no override
    keeps its own list and names no override."""
    table = made_table()
    assert table.of("levels.height", "estimate", applied=["entryM"], unmoved=True) == table.of("levels.height", "estimate")


def test_r401_q107_review1_the_build_names_no_override_for_a_value_it_left_at_the_rule(monkeypatch):
    """R3 of the review of PR 75 on the made row: a heightM gap of 0 leaves the FFL of 2 at the rule, a gap of 0.1 moves that of 3."""
    plan = made_row(monkeypatch, {"2": {"heightM": {"gapToRuleM": 0.0}}, "3": {"heightM": {"gapToRuleM": 0.1}}})["plan"]
    assert plan["2"]["overrides"]["applied"] == ["heightM"] and plan["2"]["levels"]["ffl"]["statusFrom"] == "levels.ffl from estimate"
    assert plan["2"]["levels"]["steps"]["statusFrom"] == "levels.steps from estimate"
    assert plan["3"]["levels"]["ffl"]["statusFrom"] == "levels.ffl from override by heightM"


def test_r401_q107_review1_the_yard_outline_names_the_insert_override_applied_after_the_mirror_flag(monkeypatch):
    """R4 of the review of PR 75: the build applies the mirror flag before the insert, where the sorted list puts insert first, so the yard
    outline both move names insert, the last applied. The insert itself is of the source override."""
    plan = made_row(monkeypatch, {"4": {"mirrored": False, "insert": {"gapToRuleM": [0.3, -0.2]}}})["plan"]
    assert plan["4"]["overrides"]["applied"] == ["insert", "mirrored"]
    assert plan["4"]["floors"]["yard"]["outline"]["statusFrom"].endswith(" by insert")
    assert plan["4"]["insert"]["statusFrom"] == "insert from override"


def test_r401_q107_the_build_names_the_override_that_moved_the_value_by_the_order_it_applied_them(monkeypatch):
    """Q107. entryM moves El @ Entry first, then heightM sets the FFL and the height with the steps, a steps override the steps, and rampFrontM
    the front corners of the ramp, so each value names the override applied last whose set holds its field, never the first of the sorted list."""
    both = {"2": {"entryM": {"gapToRuleM": 0.05}, "heightM": {"gapToRuleM": 0.1}}, "5": {"entryM": {"gapToRuleM": 0.05}, "steps": "4"},
            "6": {"entryM": {"gapToRuleM": 0.05}, "rampFrontM": {"gapToRuleM": -0.05}}}
    plan = made_row(monkeypatch, both)["plan"]
    assert plan["2"]["overrides"]["applied"] == ["entryM", "heightM"] and plan["6"]["overrides"]["applied"] == ["entryM", "rampFrontM"]
    by = {(number, field): item["statusFrom"].rpartition(" by ")[2] for number in ("2", "5", "6") for field, item in value_status.fields_of(plan[number]).items()
          if " by " in item["statusFrom"]}
    assert by[("2", "levels.entry")] == "entryM" and by[("2", "levels.height")] == by[("2", "levels.ffl")] == "heightM"
    # The step count of 2 stays that of the rule under both, so neither moved it (R3 of the review of PR 75)
    assert ("2", "levels.steps") not in by and ("2", "linkName") not in by
    assert by[("5", "levels.steps")] == by[("5", "parameters.RRE_ZZ_SP_RVT-Name")] == "steps" and by[("5", "levels.height")] == "entryM"
    assert by[("6", "floors.ramp.heightOffset")] == by[("6", "floors.ramp.corners")] == "rampFrontM" and by[("6", "levels.entry")] == "entryM"


def test_r401_q107_the_outcomes_give_the_applied_overrides_of_a_plot_in_the_order_the_build_applied_them():
    """Q107. The order the detail names the override by: that of the calls, an override left or not written out of it."""
    outcomes = plan_from_book.Outcomes({"1": {"heightM": {}, "entryM": {}, "steps": "x"}}, ["1"], [], "made")
    for field in ("heightM", "entryM", "heightM", "steps"):
        outcomes.applied("1", field)
    outcomes.left("1", "steps", "made up")
    assert outcomes.plot("1", True, [])["applied"] == ["entryM", "heightM"] and outcomes.in_order("1", ["entryM", "heightM"]) == ["heightM", "entryM"]


def test_r401_q107_the_plan_writes_the_shares_of_the_table_status_from_unrounded(tmp_path):
    import json
    import plan_from_past
    path = plan_from_past.write_plan(tmp_path / "plan.json", {"statusFrom": {"a": {"entries": [{"status": "MEASURED", "share": 0.9883}], "strength": "strong"}},
                                                             "plots": [{"insert": {"value": [1.23456, 2.0, 3.0], "source": "computed", "statusFrom": "a"}}]})
    written = json.loads(path.read_text(encoding="utf-8"))
    assert written["statusFrom"]["a"]["entries"][0]["share"] == 0.9883 and written["plots"][0]["insert"]["value"][0] == 1.235


def test_r401_every_value_an_override_moves_carries_the_status_of_the_override_list(monkeypatch):
    """The second method: the made row built with no override, every value of a plot both write that differs carries the override status.
    The yard of 4 is there only by its mirror flag override, which picks the outline alone, so of its fields the outline carries it."""
    built, plain = made_row(monkeypatch, MADE), made_row(monkeypatch, {})
    differ, carry, only = [], [], []
    for number, entry in built["plan"].items():
        other = value_status.fields_of(plain["plan"][number])
        for field, item in value_status.fields_of(entry).items():
            carries = detail(built, item)["entries"][0].get("question") == "Q053"
            if field not in other:
                only.append((number, field, carries))
            elif other[field]["value"] != item["value"]:
                differ.append((number, field))
                if carries:
                    carry.append((number, field))
    assert differ == carry and len(differ) > 20
    assert only == [("4", "floors.yard." + name, name == "outline") for name in ("type", "level", "levelElevation", "outline", "plotValue", "z", "heightOffset")]


def strip(value):
    """A plan with the fields of R401 taken out."""
    if isinstance(value, dict):
        return {key: strip(item) for key, item in value.items() if key not in ("status", "statusFrom", "statusByField", "assumedByField", "statusPlots")}
    if isinstance(value, list):
        return [strip(item) for item in value]
    return value


def test_r401_a_status_changes_no_value_of_the_plan(monkeypatch):
    marked = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    monkeypatch.setattr(value_status, "mark", lambda entry, table, hand=None, applied=(), rule=None: None)
    monkeypatch.setattr(value_status, "summary", lambda plots, assumed, details: {})
    plain = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    assert strip(marked["plots"]) == plain["plots"] and strip(marked["summary"]) == strip(plain["summary"])
    assert marked["plots"] != plain["plots"]


def test_r401_the_summary_of_the_build_counts_the_statuses_of_the_plan_it_writes(monkeypatch):
    built = made_row(monkeypatch, MADE)
    rules = built["summary"]["rules"]
    assert rules["statusByField"] == value_status.summary(built["plots"], project_profile.load_settings().get("valueStatus")["assumed"], built["detail"])["statusByField"]
    assert rules["statusByField"]["levels.entry"] == {"MEASURED weak": 5, "RECOMMENDED": 1} and count_text(rules["statusByField"]) == MADE_COUNTS
    assert rules["assumedByField"]["levels.entry"] == ["1", "2", "3", "4", "5", "6"] and rules["assumedByField"]["mirrored"] == ["4"]
    assert sum(rules["statusByField"]["unit"].values()) == built["summary"]["written"] == 6
    assert any(line.startswith("plots by the status of their values (R401): ") for line in plan_from_book.summary_lines(built["summary"], "parsons2024Rev6", False))


def test_r401_parity_status_each_field_of_a_plot_with_its_source_status_and_detail(monkeypatch):
    """Since Q107 each field holds the key of its detail and the step file the details once in its table statusFrom, as the plan does, where
    the detail copied into every value of the step held the build at 2,913 MiB."""
    built = made_row(monkeypatch, MADE)
    made, tables = plan_from_book.status_step(built["scope"], built["rows"], built["trace"])
    for number in ("1", "2", "3", "4", "5", "6"):
        entry = built["plan"][number]
        assert made[number]["unit"] == entry["unit"]["value"] and made[number]["written"] is True
        assert made[number]["fields"] == {field: {"source": item["source"], "status": item["status"], "statusFrom": item["statusFrom"]}
                                          for field, item in value_status.fields_of(entry).items()}
    used = {item["statusFrom"] for found in made.values() for item in (found["fields"] or {}).values()}
    assert tables == {"statusFrom": {key: built["detail"][key] for key in sorted(used)}} and list(tables["statusFrom"]) == sorted(used)
    assert made["2"]["fields"]["levels.entry"]["status"] == "RECOMMENDED" and made["1"]["fields"]["floors.generic.plotValue"]["status"] == "DOCUMENT"
    # Plot 7 of the scope has no row, so no field
    assert made["7"] == {"unit": None, "written": False, "fields": None}


def test_r401_the_config_names_the_fields_of_the_step_status_and_the_step_takes_the_prototype_files():
    settings = project_profile.load_settings()
    steps = settings.get("addin")["parity"]["steps"]
    assert {name: field["kind"] for name, field in steps["status"].items()} == {"unit": "noGeometry", "written": "noGeometry", "fields": "noGeometry"}
    assert tuple(steps["status"]) == plan_from_book.STATUS_FIELDS
    assert plan_from_book.BUILD_STEPS[-1] == "status" and plan_from_book.PARITY_STEPS[-1] == "status" and "status" in plan_from_book.PROTOTYPE_STEPS
    # The map names its block entries by profile or config, or gives the status, one or a list, and every override names a list of fields
    block = settings.get("valueStatus")
    refs = list(block["sources"].values()) + [ref for sources in block["fields"].values() for found in sources.values() for ref in (found if isinstance(found, list) else [found])]
    assert all(set(ref) & {"profile", "config", "status"} for ref in refs)
    assert all(isinstance(fields, list) and fields for fields in block["overrideSets"].values())
    import rule_status
    assert {label.split(" ")[0] if label.startswith("MEASURED") else label for label in block["assumed"]} <= set(rule_status.ORDER)


def test_r401_q107_review1_a_ramp_height_offset_is_held_by_its_lowest_corner_which_an_entry_gap_may_leave_as_it_was():
    """R3 of the review of PR 75: the heightOffset of a ramp is its lowest corner less the elevation of its level, so an entryM gap that moves
    the back corners above the front leaves it at its value by the rule, held by movedBy at the lowest Z of the corners."""
    table = real_table({})
    assert table.moved_by["floors.ramp.heightOffset"] == "floors.ramp.corners lowest"
    front = [[0.0, 0.0, 10.0], [1.0, 0.0, 10.1]]
    moved = {"floors.ramp.corners": {"value": front + [[1.0, 5.0, 10.4], [0.0, 5.0, 10.3]]}}
    rule = {"floors.ramp.corners": {"value": front + [[1.0, 5.0, 10.35], [0.0, 5.0, 10.25]]}}
    assert value_status.unmoved("floors.ramp.heightOffset", moved, rule, table) and not value_status.unmoved("floors.ramp.corners", moved, rule, table)
    lower = {"floors.ramp.corners": {"value": [[0.0, 0.0, 9.95]] + moved["floors.ramp.corners"]["value"][1:]}}
    assert not value_status.unmoved("floors.ramp.heightOffset", lower, rule, table)


# Q110 (Bader, 2026-10-28). The entries valueStatus of config/project.json lists for prototypeFile: the unit of the location plan, the file of
# the prototype folder named with it and the keo_file of its typology, which plan_from_cad.prototype_files reads for every typology.
# ValueStatusTests.cs holds the same text
Q110_DETAIL = ('{"entries":[{"status":"DOCUMENT","document":"the location plan, labels.csv of the data book"},{"status":"DOCUMENT","document":'
               '"the prototype folder of the build, pastJob.upgradedPrototypeFolder, the file named with the unit code (R128)"},{"entry":"%s '
               'typologies.VD1.keo_file","status":"DOCUMENT","document":"a made up document","pageOrSheet":"UNKNOWN","source":"source"}]}')


def test_r401_q110_prototype_file_takes_the_worst_of_its_unit_its_prototype_folder_and_the_keo_file_of_its_typology():
    import json
    name = project_profile.load_settings().get("profile")
    table = real_table({"typologies.VD1.keo_file": KEO})
    status, found, key = table.of("prototypeFile", "computed", unit="VD1")
    assert (status, json.dumps(found, separators=(",", ":"), ensure_ascii=False), key) == ("DOCUMENT", Q110_DETAIL % name, "prototypeFile from computed unit VD1")
    # A unit whose typology the block holds no keo_file of, and a plot with no unit, are UNKNOWN with why, never a guess
    assert table.of("prototypeFile", "computed", unit="XX")[1]["entries"][-1] == {"entry": name + " typologies.XX.keo_file", "status": "UNKNOWN",
                                                                                   "why": "the block status holds no entry (R401)"}
    assert table.of("prototypeFile", "computed")[0::2] == ("UNKNOWN", "prototypeFile from computed unit None")


def test_r401_q110_each_written_plot_holds_a_status_for_prototype_file_with_its_unit(monkeypatch):
    built = made_row(monkeypatch, MADE)
    plan = built["plan"]
    assert [(number, plan[number]["prototypeFile"]["status"], plan[number]["prototypeFile"]["statusFrom"]) for number in ("1", "5")] == [
        ("1", "UNKNOWN", "prototypeFile from computed unit VD2"), ("5", "DOCUMENT", "prototypeFile from computed unit DP2")]
    assert [entry["status"] for entry in detail(built, plan["5"]["prototypeFile"])["entries"]] == ["DOCUMENT", "DOCUMENT", "DOCUMENT"]
    made, _ = plan_from_book.status_step(built["scope"], built["rows"], built["trace"])
    assert made["5"]["fields"]["prototypeFile"] == {"source": "computed", "status": "DOCUMENT", "statusFrom": "prototypeFile from computed unit DP2"}


def test_r401_q110_the_summary_of_step_15_counts_prototype_file_by_field_and_status(monkeypatch):
    # With the real block every plot of the made row takes the keo_file of its typology, so none is assumed for prototypeFile
    real = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))["summary"]["rules"]
    assert real["statusByField"]["prototypeFile"] == {"DOCUMENT": 6} and "prototypeFile" not in real["assumedByField"]
    rules = made_row(monkeypatch, MADE)["summary"]["rules"]
    assert rules["statusByField"]["prototypeFile"] == {"DOCUMENT": 5, "UNKNOWN": 1} and rules["assumedByField"]["prototypeFile"] == ["1"]
