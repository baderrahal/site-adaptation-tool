"""R401, the plan part. The status beside each sourced value of a plot of the plan, taken once per build from the block status of the rules
profile through valueStatus of config/project.json, its count by field and status in the summary of step 15 and the step status of the
parity harness, on a made up map and block and on the made up row of test_level_floor_steps.py, built in code (R10). ValueStatusTests.cs
holds the same made up data for the C#."""
import copy

import plan_from_book
import project_profile
import test_level_floor_steps
import value_status
from test_override_param_steps import MADE

PROFILE_FILE = "rules/made.json"
# A made up map: an override and a client input by their source, a field by the block entry of its source, a parameter by its name
MAP = {"sources": {"override": {"status": "RECOMMENDED", "question": "Q900", "where": "a made up list"},
                   "locationPlan": {"status": "DOCUMENT", "document": "a made up plan"}},
       "fields": {"levels.entry": {"estimate": {"profile": "levelProfiles.{levelProfile}.keyRules.entry.rule"}},
                  "levels.ffl": {"estimate": {"profile": "levelProfiles.{levelProfile}.keyRules.ffl.rule"}},
                  "mirrored": {"computed": {"profile": "levelProfiles.{levelProfile}.keyRules.hand {hand}.rule"}},
                  "parameters.*": {"keyRules": {"profile": "levelProfiles.{levelProfile}.keyRules.parameter {parameter}.rule"}},
                  "parameters.CASE": {"keyRules": {"config": "made.caseRule"}},
                  "floors.generic.type": {"computed": {"config": "floors.types.generic"}},
                  "floors.generic.level": {"computed": {"profile": "levelProfiles.{levelProfile}.keyRules.notInTheBlock"}}},
       "overrideSets": {"entryM": "levels.entry"},
       "assumed": ["RECOMMENDED", "CHOSEN by Code", "UNKNOWN", "MEASURED weak"]}
BLOCK = {"note": "a made up block",
         PROFILE_FILE: {"levelProfiles.p1.keyRules.entry.rule": {"status": "MEASURED", "share": 0.5, "strength": "weak", "measuredBy": "levelProfiles.p1.keyRules.entry"},
                        "levelProfiles.p1.keyRules.ffl.rule": {"status": "MEASURED", "share": 0.95, "strength": "strong", "measuredBy": "levelProfiles.p1.keyRules.ffl"},
                        "levelProfiles.p1.keyRules.hand alone.rule": {"status": "MEASURED", "share": 1.0, "strength": "strong",
                                                                       "measuredBy": "levelProfiles.p1.keyRules.hand alone"},
                        "levelProfiles.p1.keyRules.parameter X.rule": {"status": "CHOSEN by Bader", "question": "Q901", "where": "made up"}},
         "config/project.json": {"floors.types.generic": {"status": "UNKNOWN", "why": "no Q", "nearestSource": "floors.note"},
                                 "made.caseRule": {"status": "CHOSEN by Code", "where": "note"}}}


def made_table(level_profile="p1"):
    return value_status.Table(MAP, BLOCK, PROFILE_FILE, level_profile)


def test_r401_a_value_takes_the_status_of_the_block_entry_its_field_and_source_name_with_the_entry_and_its_evidence():
    table = made_table()
    assert table.of("levels.entry", "estimate") == ("MEASURED", {"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.entry.rule", "share": 0.5, "strength": "weak",
                                                                 "measuredBy": "levelProfiles.p1.keyRules.entry"})
    # The hand table of the plot names the keyRules entry that gave its mirror flag, the name of a parameter its own entry
    assert table.of("mirrored", "computed", hand="alone")[1]["entry"] == PROFILE_FILE + " levelProfiles.p1.keyRules.hand alone.rule"
    assert table.of("parameters.X", "keyRules") == ("CHOSEN by Bader", {"entry": PROFILE_FILE + " levelProfiles.p1.keyRules.parameter X.rule", "question": "Q901",
                                                                         "where": "made up"})
    # A field of its own goes before parameters.*, and config names an entry of the settings
    assert table.of("parameters.CASE", "keyRules") == ("CHOSEN by Code", {"entry": "config/project.json made.caseRule", "where": "note"})
    assert table.of("floors.generic.type", "computed") == ("UNKNOWN", {"entry": "config/project.json floors.types.generic", "why": "no Q", "nearestSource": "floors.note"})


def test_r401_an_override_and_a_client_input_take_the_status_the_map_gives_their_source():
    table = made_table()
    assert table.of("parameters.X", "override") == ("RECOMMENDED", {"question": "Q900", "where": "a made up list"})
    assert table.of("insert", "override") == ("RECOMMENDED", {"question": "Q900", "where": "a made up list"})
    assert table.of("unit", "locationPlan") == ("DOCUMENT", {"document": "a made up plan"})


def test_r401_an_applied_override_whose_value_keeps_its_source_gives_the_value_the_status_of_the_override_list():
    table = made_table()
    assert table.of("levels.entry", "estimate", applied=["entryM", "heightM"]) == ("RECOMMENDED", {"question": "Q900", "where": "a made up list", "override": "entryM"})
    # An override that sets another field leaves the value its own status
    assert table.of("levels.ffl", "estimate", applied=["entryM"])[0] == "MEASURED"


def test_r401_a_pair_the_map_does_not_hold_or_an_entry_the_block_lacks_is_unknown_never_a_guess():
    table = made_table()
    assert table.of("prototypeFile", "computed") == ("UNKNOWN", {"why": "valueStatus holds no status for prototypeFile from computed (R401)"})
    assert table.of("levels.entry", "keyRules") == ("UNKNOWN", {"why": "valueStatus holds no status for levels.entry from keyRules (R401)"})
    assert table.of("floors.generic.level", "computed") == ("UNKNOWN", {"why": "the block status holds no entry %s levelProfiles.p1.keyRules.notInTheBlock (R401)" % PROFILE_FILE})
    # A plot with no hand table, and a level profile the block holds nothing of
    assert table.of("mirrored", "computed") == ("UNKNOWN", {"why": "the plot holds no hand for levelProfiles.{levelProfile}.keyRules.hand {hand}.rule (R401)"})
    assert made_table("p2").of("levels.entry", "estimate")[0] == "UNKNOWN"
    # No map and no block give every value UNKNOWN
    assert value_status.Table({}, {}, PROFILE_FILE, "p1").of("unit", "locationPlan")[0] == "UNKNOWN"


def test_r401_the_build_marks_each_sourced_value_once_and_the_label_takes_the_strength_of_a_measured_value():
    entry = {"plot": "1", "unit": {"value": "VD2", "source": "locationPlan"}, "levels": {"entry": {"value": 10.1, "source": "estimate"}},
             "parameters": {"X": {"value": "a", "source": "keyRules"}}, "floors": {"generic": {"type": {"value": "G", "source": "computed"}}},
             "overrides": {"applied": [], "notApplied": {}}, "roadSurfaces": ["road"]}
    value_status.mark(entry, made_table(), hand=None, applied=[])
    assert list(value_status.fields_of(entry)) == ["unit", "levels.entry", "parameters.X", "floors.generic.type"]
    assert list(entry["levels"]["entry"]) == ["value", "source", "status", "statusFrom"]
    assert entry["unit"]["status"] == "DOCUMENT" and entry["parameters"]["X"]["status"] == "CHOSEN by Bader"
    assert [value_status.label(item) for item in value_status.fields_of(entry).values()] == ["DOCUMENT", "MEASURED weak", "CHOSEN by Bader", "UNKNOWN"]
    assert entry["overrides"] == {"applied": [], "notApplied": {}} and entry["roadSurfaces"] == ["road"]


def test_r401_the_summary_counts_plots_by_field_and_status_and_lists_the_assumed_plots_by_field():
    plots = []
    for number, (entry_status, generic) in {"1": ("estimate", "computed"), "2": ("estimate", "computed"), "10": ("estimate", "computed")}.items():
        entry = {"plot": number, "unit": {"value": "VD2", "source": "locationPlan"}, "levels": {"entry": {"value": 10.1, "source": entry_status},
                                                                                             "ffl": {"value": 10.5, "source": "estimate"}}}
        value_status.mark(entry, made_table(), applied=["entryM"] if number == "10" else [])
        plots.append(entry)
    found = value_status.summary(plots, MAP["assumed"])
    assert found["statusByField"] == {"levels.entry": {"MEASURED weak": 2, "RECOMMENDED": 1}, "levels.ffl": {"MEASURED strong": 3}, "unit": {"DOCUMENT": 3}}
    assert found["assumedByField"] == {"levels.entry": ["1", "2", "10"]}
    assert found["statusPlots"] == {"DOCUMENT": 3, "MEASURED strong": 3, "MEASURED weak": 2, "RECOMMENDED": 1, "assumed": 3}
    line = plan_from_book.status_line(found)
    assert line == "plots by the status of their values (R401): DOCUMENT 3, MEASURED strong 3, MEASURED weak 2, RECOMMENDED 1, an assumed value 3, in the fields levels.entry"


KEY = "levelProfiles.parsons2024Rev6.keyRules."
WEAK, STRONG = {"status": "MEASURED", "share": 0.5, "strength": "weak", "measuredBy": "made"}, {"status": "MEASURED", "share": 0.97, "strength": "strong", "measuredBy": "made"}
# The block of the made row in place of the real one, so no status of the made row follows a real share. Every other entry the map names is
# missing from it, so UNKNOWN
MADE_BLOCK = {KEY + "entry.rule": WEAK, KEY + "ffl.rule": STRONG, KEY + "steps.rule": STRONG, KEY + "hand villa.rule": STRONG, KEY + "hand cluster.rule": STRONG,
              KEY + "hand alone.rule": STRONG, KEY + "linkOrigin.rule": STRONG, KEY + "parameter CASE.rule": WEAK,
              KEY + "parameter RRE_ZZ_SP_RVT-Name.rule": {"status": "CHOSEN by Bader", "question": "Q902", "where": "made up"},
              KEY + "rampSlope.rule": {"status": "CHOSEN by Code", "where": "a made up note"}}
MADE_CONFIG_BLOCK = {"floors.types.generic": {"status": "UNKNOWN", "why": "made up", "nearestSource": "floors.note"}}
# The plots of the made row by field and status with MADE and the made block, which ValueStatusTests.cs holds the same
MADE_COUNTS = ("floors.generic.heightOffset:MEASURED strong=6 floors.generic.level:UNKNOWN=6 floors.generic.levelElevation:UNKNOWN=6 "
               "floors.generic.outline:UNKNOWN=6 floors.generic.plotValue:DOCUMENT=6 floors.generic.type:UNKNOWN=6 floors.generic.z:MEASURED strong=6 "
               "floors.ramp.corners:RECOMMENDED=1 floors.ramp.corners:UNKNOWN=5 floors.ramp.heightOffset:UNKNOWN=6 floors.ramp.level:UNKNOWN=6 "
               "floors.ramp.levelElevation:UNKNOWN=6 floors.ramp.outline:UNKNOWN=6 floors.ramp.plotValue:DOCUMENT=6 floors.ramp.slopePercent:CHOSEN by Code=6 "
               "floors.ramp.type:CHOSEN by Code=5 floors.ramp.type:RECOMMENDED=1 floors.yard.heightOffset:UNKNOWN=3 floors.yard.level:UNKNOWN=3 "
               "floors.yard.levelElevation:UNKNOWN=3 floors.yard.outline:UNKNOWN=3 floors.yard.plotValue:DOCUMENT=3 floors.yard.type:UNKNOWN=3 "
               "floors.yard.z:UNKNOWN=3 insert:MEASURED strong=5 insert:RECOMMENDED=1 levels.entry:MEASURED weak=5 levels.entry:RECOMMENDED=1 "
               "levels.ffl:MEASURED strong=5 levels.ffl:RECOMMENDED=1 levels.height:MEASURED strong=6 levels.ssl:UNKNOWN=6 levels.steps:MEASURED strong=5 "
               "levels.steps:RECOMMENDED=1 linkName:CHOSEN by Bader=5 linkName:RECOMMENDED=1 mirrored:MEASURED strong=5 mirrored:RECOMMENDED=1 "
               "parameters.CASE:MEASURED weak=5 parameters.CASE:RECOMMENDED=1 parameters.RRE_ZZ_SP_Façade-Color:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_Façade-Design-Option:UNKNOWN=6 parameters.RRE_ZZ_SP_Number-of-Steps:UNKNOWN=5 parameters.RRE_ZZ_SP_Plot-Number:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_Prototype:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-FFL:RECOMMENDED=1 parameters.RRE_ZZ_SP_Prototype-FFL:UNKNOWN=5 "
               "parameters.RRE_ZZ_SP_Prototype-Foundation-Type:UNKNOWN=6 parameters.RRE_ZZ_SP_Prototype-HVAC-Schedule-Type:UNKNOWN=6 "
               "parameters.RRE_ZZ_SP_RVT-Name:CHOSEN by Bader=5 parameters.RRE_ZZ_SP_RVT-Name:RECOMMENDED=1 prototypeFile:UNKNOWN=6 "
               "rotationDeg:RECOMMENDED=2 rotationDeg:UNKNOWN=4 unit:DOCUMENT=6")


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


def test_r401_each_sourced_value_of_a_written_plot_holds_its_status_by_its_source_and_field(monkeypatch):
    made_block_rules(monkeypatch)
    built = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    plan, name = built["plan"], project_profile.load_settings().get("profile")
    # El @ Entry by the rule, and on 2 moved by its entryM override, which takes the status of the override list
    assert (plan["1"]["levels"]["entry"]["status"], plan["1"]["levels"]["entry"]["statusFrom"]) == (
        "MEASURED", {"entry": name + " levelProfiles.parsons2024Rev6.keyRules.entry.rule", "share": 0.5, "strength": "weak", "measuredBy": "made"})
    assert plan["2"]["levels"]["entry"]["source"] == "estimate" and plan["2"]["levels"]["entry"]["status"] == "RECOMMENDED"
    assert plan["2"]["levels"]["entry"]["statusFrom"] == {"question": "Q053", "where": "steps/investigations/rule-status-audit.md section 6", "override": "entryM"}
    # The hand of 3 by the table of hand cluster, 4 by its mirror flag override, the CASE of 2 by its override, the unit from the location plan
    assert plan["3"]["mirrored"]["statusFrom"]["entry"] == name + " levelProfiles.parsons2024Rev6.keyRules.hand cluster.rule"
    assert (plan["4"]["mirrored"]["source"], plan["4"]["mirrored"]["status"]) == ("override", "RECOMMENDED")
    assert (plan["2"]["parameters"]["CASE"]["source"], plan["2"]["parameters"]["CASE"]["status"]) == ("override", "RECOMMENDED")
    assert plan["1"]["unit"]["status"] == "DOCUMENT" and plan["1"]["prototypeFile"]["status"] == "UNKNOWN"
    assert all("status" in item and "statusFrom" in item for entry in built["plots"] for item in value_status.fields_of(entry).values())


def strip(value):
    """A plan with the fields of R401 taken out."""
    if isinstance(value, dict):
        return {key: strip(item) for key, item in value.items() if key not in ("status", "statusFrom", "statusByField", "assumedByField", "statusPlots")}
    if isinstance(value, list):
        return [strip(item) for item in value]
    return value


def test_r401_a_status_changes_no_value_of_the_plan(monkeypatch):
    marked = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    monkeypatch.setattr(value_status, "mark", lambda entry, table, hand=None, applied=(): None)
    monkeypatch.setattr(value_status, "summary", lambda plots, assumed: {})
    plain = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    assert strip(marked["plots"]) == plain["plots"] and strip(marked["summary"]) == strip(plain["summary"])
    assert marked["plots"] != plain["plots"]


def test_r401_the_summary_of_the_build_counts_the_statuses_of_the_plan_it_writes(monkeypatch):
    made_block_rules(monkeypatch)
    built = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    rules = built["summary"]["rules"]
    assert rules["statusByField"] == value_status.summary(built["plots"], project_profile.load_settings().get("valueStatus")["assumed"])["statusByField"]
    assert rules["statusByField"]["levels.entry"] == {"MEASURED weak": 5, "RECOMMENDED": 1} and count_text(rules["statusByField"]) == MADE_COUNTS
    assert rules["assumedByField"]["levels.entry"] == ["1", "2", "3", "4", "5", "6"] and rules["assumedByField"]["mirrored"] == ["4"]
    assert sum(rules["statusByField"]["unit"].values()) == built["summary"]["written"] == 6
    assert any(line.startswith("plots by the status of their values (R401): ") for line in plan_from_book.summary_lines(built["summary"], "parsons2024Rev6", False))


def test_r401_parity_status_each_field_of_a_plot_with_its_source_status_and_detail(monkeypatch):
    made_block_rules(monkeypatch)
    built = test_level_floor_steps.made_build(monkeypatch, copy.deepcopy(MADE))
    made = plan_from_book.status_step(built["scope"], built["rows"], built["trace"])
    for number in ("1", "2", "3", "4", "5", "6"):
        entry = built["plan"][number]
        assert made[number]["unit"] == entry["unit"]["value"] and made[number]["written"] is True
        assert made[number]["fields"] == {field: {"source": item["source"], "status": item["status"], "statusFrom": item["statusFrom"]}
                                          for field, item in value_status.fields_of(entry).items()}
    assert made["2"]["fields"]["levels.entry"]["status"] == "RECOMMENDED" and made["1"]["fields"]["floors.generic.plotValue"]["status"] == "DOCUMENT"
    # Plot 7 of the scope has no row, so no field
    assert made["7"] == {"unit": None, "written": False, "fields": None}


def test_r401_the_config_names_the_fields_of_the_step_status_and_the_step_takes_the_prototype_files():
    settings = project_profile.load_settings()
    steps = settings.get("addin")["parity"]["steps"]
    assert {name: field["kind"] for name, field in steps["status"].items()} == {"unit": "noGeometry", "written": "noGeometry", "fields": "noGeometry"}
    assert tuple(steps["status"]) == plan_from_book.STATUS_FIELDS
    assert plan_from_book.BUILD_STEPS[-1] == "status" and plan_from_book.PARITY_STEPS[-1] == "status" and "status" in plan_from_book.PROTOTYPE_STEPS
    # The map names its block entries by profile or config, or gives the status, and the assumed statuses are of the six of rule_status
    block = settings.get("valueStatus")
    refs = list(block["sources"].values()) + [ref for sources in block["fields"].values() for ref in sources.values()]
    assert all(set(ref) & {"profile", "config", "status"} for ref in refs)
    import rule_status
    assert {label.split(" ")[0] if label.startswith("MEASURED") else label for label in block["assumed"]} <= set(rule_status.ORDER)
