"""R385 in the level builders and the rule paths, Round ADDIN 4b (Q080): the FFL of a piece, El @ Entry, the height, SSL, the level
overrides of a build and the gaps majority.py and key_rules.py write round a tie half away from zero on the 15 digit decimal, as
common.fixed does. Every number here is made up (R10)."""
import io
import json
import tokenize
from pathlib import Path

import pytest

import common
import key_rules
import levels
import majority
import plan_rules
import project_profile

ROOT = Path(__file__).resolve().parents[2]
SETTINGS = project_profile.load_settings()
PROFILE = project_profile.load_profile(project_profile.profile_path(SETTINGS))
LP = plan_rules.level_profile(PROFILE, "parsons2024Rev6")
GROUND = PROFILE.metres("levels.ground_ssl")


def test_r385_the_ffl_of_a_piece_rounds_a_tie_half_away_from_zero_and_its_height_and_ssl_follow():
    # 119.3075 plus the DP1 base of 0.1 is the tie 119.4075, round() took it down to 119.407 by its binary value
    assert round(119.3075 + 0.1, 3) == 119.407 and plan_rules.base_of(PROFILE, "DP1") == 0.1
    assert plan_rules.piece_ffl([(119.3075, "DP1"), (119.2, "TH1")], PROFILE) == 119.408
    entries = {"1": {"unit": "DP1", "entry": 119.3075}, "2": {"unit": "TH1", "entry": 119.2}}
    rows = plan_rules.key_level_rows(entries, {"1": ["1", "2"], "2": ["1", "2"]}, PROFILE, LP, {"steps": {"rule": "stepsFromHeight"}})
    assert rows["1"]["ffl"] == rows["2"]["ffl"] == 119.408 and rows["1"]["height"] == 0.1005 and rows["2"]["height"] == 0.208
    assert rows["1"]["ssl"] == common.fixed(119.408 + GROUND, 3)
    # R278, a level profile with no level rules: the FFL by the same rule, the height from the FFL and El @ Entry as written, so the
    # three agree to the 0.1 mm, where the height of the level before it was rounded gave 0.1 beside 119.408 and 119.3075 (4b review)
    lp = dict(LP, height="oneFflPerClusterAtTheHighestEntry")
    found = plan_rules.heights({"1": {"unit": "DP1", "entry": 119.3075}}, [], lp, PROFILE)["1"]
    assert (found["ffl"], found["height"], found["entry"]) == (119.408, 0.1005, 119.3075) and found["ssl"] == common.fixed(119.4075 + GROUND, 3)
    assert found["height"] == common.fixed(found["ffl"] - found["entry"], 4) == rows["1"]["height"]


def test_r385_the_el_at_entry_a_build_gives_rounds_a_tie_on_its_decimal_value():
    # The road 10.0 plus 0.5 percent of 5.51 m is the tie 10.02755, round() gave 10.0275
    raw = plan_rules.entry_of([10.0, 10.0], "DP1", LP, 5.51)[0]
    assert repr(raw) == "10.02755" and round(raw, 4) == 10.0275
    assert plan_rules.key_entry([10.0, 10.0], "DP1", LP, 5.51) == 10.0276


def test_r385_the_levels_of_r67_round_the_ffl_of_a_cluster_half_away_from_zero(tmp_path):
    from test_levels import by_plot, dummy_profile, entries_of
    profile = dummy_profile(tmp_path)
    rows = by_plot(levels.compute(profile, [{"plots": ["1", "2"]}], entries_of(("1", "VL4", 119.3075), ("2", "VL4", 119.2))))
    assert rows["1"]["ffl"] == rows["2"]["ffl"] == 119.408 and rows["1"]["height"] == 0.1005 and rows["2"]["height"] == 0.208


def test_r385_a_level_override_of_a_build_moves_its_level_by_the_rule(tmp_path, monkeypatch):
    from test_addin_levels import level_build, level_of
    from test_addin_review import entry
    # Plot 3 has the FFL 10.263 of its pair and plot 2 the Prototype-FFL 10.240 by the rule, each gap below makes a tie round() takes down
    assert round(10.263 + 0.0015, 3) == 10.264 and round(10.24 + 0.0025, 3) == 10.242
    overrides = [entry("3", "heightM", {"gapToRuleM": 0.0015}, "parsons", "IMC.rvt", "imc"),
                 entry("2", "parameter RRE_ZZ_SP_Prototype-FFL", {"gapToRuleM": 0.0025}, "parsons", "IMC.rvt", "imc")]
    found, _ = level_build(tmp_path, monkeypatch, overrides=overrides)
    assert level_of(found["3"], "ffl") == 10.265 and level_of(found["3"], "ssl") == common.fixed(10.265 + GROUND, 3)
    assert level_of(found["3"], "height") == common.fixed(10.265 - level_of(found["3"], "entry"), 4)
    assert found["2"]["parameters"]["RRE_ZZ_SP_Prototype-FFL"]["value"] == "10.243"


def test_r385_the_gaps_majority_and_key_rules_write_round_by_the_rule():
    # A level gap of the override list at 4 decimals, the key value less the rule
    assert round(0.30015 - 0.0, 4) == 0.3001
    misses = [{"decision": "parsons ffl", "field": "heightM", "plot": "2", "key": "imc", "code": "DP1", "value": 0.30015, "rule": 0.0, "state": "breaks",
               "reason": "breaks oneFflPerPieceAtTheHighestEntry"}]
    listed = majority.overrides(misses, {"overrideDecisions": ["ffl"], "groups": {"parsons": ["imc"]}}, {"imc": "IMC.rvt"})
    assert listed[0]["value"] == {"gapToRuleM": 0.3002}
    assert key_rules.level_gap(None, 0.30015, 0.0) == {"gapToRuleM": 0.3002}
    # A rotation gap in degrees and an origin gap in the link axes at 3 decimals
    assert round(12.3455, 3) == 12.345 and key_rules.rotation_gap(None, 12.3455, 0.0) == {"gapToRuleDeg": 12.346}
    item = {"frame": {"r": (1.0, 0.0), "n": (0.0, 1.0)}, "row": {"mirrored": False}}
    assert (round(1.0005, 3), round(2.0465, 3)) == (1.0, 2.046) and key_rules.insert_gap(item, (1.0005, 2.0465), (0.0, 0.0)) == {"gapToRuleM": [1.001, 2.047]}


def test_r385_the_second_method_of_the_levels_rounds_by_its_own_integer_route_as_the_rule_does():
    import key_rules_check
    assert key_rules_check.own_ffl([(119.3075, 0.1), (119.2, 0.2)]) == 119.408
    for value, decimals in ((10.02755, 4), (12.3455, 3), (0.7 + 0.0005, 3), (-2.0465, 3), (0.1 + 0.2 - 0.3, 3), (150.38 - 150.0, 4), (0.5005, 3), (-0.0004, 3)):
        assert key_rules_check.own_fixed(value, decimals) == common.fixed(value, decimals)
    assert key_rules_check.own_fixed(float("nan"), 3) is None


def round_calls(path):
    """Every call of the builtin round in a Python file, by line, comments and texts left out by the tokenizer."""
    tokens = list(tokenize.generate_tokens(io.StringIO(path.read_text(encoding="utf-8")).readline))
    lines = path.read_text(encoding="utf-8").splitlines()
    found = []
    for index, token in enumerate(tokens[:-1]):
        before = tokens[index - 1] if index else None
        if token.type == tokenize.NAME and token.string == "round" and tokens[index + 1].string == "(" and not (before and before.string in (".", "def")):
            found.append(lines[token.start[0] - 1].strip())
    return found


# R385. The round() calls left in the build and the rule paths: a share of counts and a count of the native measure of R275, no level,
# length, angle or gap. The C# never computes them, majority.py and key_rules_block.py are the measure and stay in Python. And the round()
# of Round 17 that a build of native2022 keeps by majority.overrideRounding, its native overrides measured on it (Q050)
LEFT = {"plan_rules.py": ("lambda value, decimals: round(value, decimals)",),
        # The key side of past_all.csv of 2b, its levels, gaps, areas and slopes, moved with a rebuild of past_all.csv in 4f (R359)
        "past_all.py": ("round(shape.area, 3)", "round(c[2] - datum, 4)", "round(geometry[\"slope_short\"], 3)", "round(geometry[\"slope\"], 3)",
                        "round(ends[0], 4), round(ends[1], 4), round(ends[2], 4)", "\"gapM\": round(schedule_value - key_value, 4)", "round(entry_level, 4)",
                        "round(ffl_design - entry_level, 4)"),
        # The width key of the yard outline lookup, the width in steps, a lookup and no level, moved with step 8 in 4f (R358)
        "floor_fit.py": ("round(round(width / step) * step, 3)",),
        "majority.py": ("round(counts.get(\"holds\", 0) / total, 4)", "covers.append(round(cover, 2))",
                        "str(1 + max(0, round((item[\"row\"][\"height\"] - base_height(profile, item[\"code\"])) / riser)))"),
        "key_rules_block.py": ("round(counts.get(\"holds\", 0) / total, 4)", "round(holds / clusters, 4)"),
        # The yard outline width key of floor_fit.width_key, a lookup the first method keeps with round() until step 8 of 4f (R358)
        "key_rules_check.py": ("\"width\": round(round(math.dist(frame[\"left\"], frame[\"right\"]) / step) * step, 3)",)}


@pytest.mark.parametrize("name", ["plan_rules.py", "plan_from_book.py", "levels.py", "key_levels.py", "key_rules.py", "majority.py", "key_rules_block.py",
                                  "key_rules_check.py", "build_proof.py", "past_all.py", "floor_fit.py"])
def test_r385_no_round_of_python_is_left_on_a_level_a_length_an_angle_or_a_gap_of_the_build_and_the_rules(name):
    left = [line for line in round_calls(ROOT / "python" / name) if not any(allowed in line for allowed in LEFT.get(name, ()))]
    assert left == []


# R385 the rounding each level profile's overrides were measured on, named in config
def test_r385_config_names_the_rounding_of_each_level_profile_native2022_that_of_round_17_and_parsons2024rev6_r385():
    block = SETTINGS.get("majority")["overrideRounding"]["levelProfiles"]
    assert block == {"native2022": "pythonRound", "parsons2024Rev6": "R385"}
    assert plan_rules.rounding_of(SETTINGS, "parsons2024Rev6") is common.fixed
    native = plan_rules.rounding_of(SETTINGS, "native2022")
    # Round 17 rounded the binary value, 10.263 plus a gap of 0.0015 went down, R385 takes it up
    assert native(10.263 + 0.0015, 3) == 10.264 and common.fixed(10.263 + 0.0015, 3) == 10.265
    values = json.loads(json.dumps(SETTINGS.values))
    for name, value, why in (("native2022", None, "names no rounding"), ("native2022", "halfEven", "names no rounding"), ("parsons2024Rev6", "pythonRound", "R385")):
        found = dict(values["majority"]["overrideRounding"]["levelProfiles"])
        if value is None:
            del found[name]
        else:
            found[name] = value
        values["majority"]["overrideRounding"]["levelProfiles"] = found
        settings = project_profile.ProjectProfile(SETTINGS.path, values)
        with pytest.raises(project_profile.ProfileError, match=why):
            plan_rules.rounding_of(settings, name, plan_rules.key_rules_of(plan_rules.level_profile(PROFILE, name)))


def test_r385_a_build_rounds_its_r278_levels_and_applies_a_level_override_by_the_rounding_of_its_level_profile(tmp_path, monkeypatch):
    from test_addin_levels import level_build, level_of
    from test_addin_review import entry
    # Plot 3 has the FFL 10.253 of R278 on its own El @ Entry 10.0526 plus 0.2, the gap -0.0075 makes the tie 10.2455, which round() takes
    # down by its binary value. The height and SSL follow the FFL as written, as after a gap of R340
    overrides = [entry("3", "heightM", {"gapToRuleM": -0.0075}, "parsons", "IMC.rvt", "imc")]
    later = {"parsons": ["entryM", "steps", "parameter RRE_ZZ_SP_Prototype-FFL"]}
    found, _ = level_build(tmp_path, monkeypatch, overrides=overrides, rules={}, later=later)
    assert level_of(found["3"], "ffl") == 10.246 and level_of(found["3"], "height") == 0.1934 and level_of(found["3"], "ssl") == common.fixed(10.246 + GROUND, 3)
    found, _ = level_build(tmp_path, monkeypatch, overrides=overrides, rules={}, later=later, rounding="pythonRound")
    assert round(10.253 - 0.0075, 3) == 10.245 and level_of(found["3"], "ffl") == 10.245 and level_of(found["3"], "height") == 0.1924
    assert level_of(found["3"], "ssl") == round(10.245 + GROUND, 3)
    # A profile whose keyRules hold level rules was measured on R385 and builds on nothing else
    with pytest.raises(project_profile.ProfileError, match="R385"):
        level_build(tmp_path, monkeypatch, overrides=overrides, rounding="pythonRound")


def test_r385_a_majority_run_never_writes_the_overrides_of_a_job_whose_level_profile_was_measured_on_another_rounding():
    block = SETTINGS.get("majority")
    majority.check_measured_rounding({"new": {"parsons": 3}}, block, PROFILE)
    majority.check_measured_rounding(None, block, PROFILE)
    with pytest.raises(project_profile.ProfileError, match="native2022"):
        majority.check_measured_rounding({"new": {"native": 1, "parsons": 3}}, block, PROFILE)


def test_r385_the_height_is_the_ffl_less_el_at_entry_as_written_and_ssl_the_ffl_as_written_plus_the_ground_on_every_rounding():
    lp = dict(LP, height="oneFflPerClusterAtTheHighestEntry")
    entries = {"1": {"unit": "DP1", "entry": 119.3075}}
    for fix, ffl, height in ((common.fixed, 119.408, 0.1005), (plan_rules.rounding_of(SETTINGS, "native2022"), 119.407, 0.0995)):
        found = plan_rules.heights(entries, [], lp, PROFILE, fix)["1"]
        assert (found["ffl"], found["height"], found["entry"]) == (ffl, height, 119.3075)
        assert found["height"] == fix(found["ffl"] - found["entry"], 4) and found["ssl"] == fix(found["ffl"] + GROUND, 3)


# R385 each value of majority.json is written at its own decimals once, so keyRules never holds a value rounded twice
def test_r385_no_value_is_rounded_twice_on_the_way_from_majority_json_to_key_rules(tmp_path):
    import argparse
    import key_levels
    import key_rules_block
    block = SETTINGS.get("majority")
    # 1,698 of 4,901 is 0.34646, a share of 0.3465 at its 4 decimals, which majority.json wrote at 3 as 0.347 where one rounding gives 0.346.
    # A base of 0.10049 is 0.1005 at the 4 decimals of a height, which it wrote as 0.101 where one rounding gives 0.100
    base = key_levels.piece_base(10.1, 9.99951)
    items = [{"key": "imc", "plot": str(index), "code": "DP1", "factors": {"all": "all", "typology": "DP1"},
              "base": base if index < 1698 else 0.2 + index / 10000.0} for index in range(4901)]
    candidates = {"profile": lambda item: base, "typology": "typology"}
    result, _ = majority.decide("parsons baseHeight", items, lambda item: item["base"], candidates, lambda item: "parsons", 0.9, bands={"1mm": lambda a, b: a == b})
    result["field"] = "baseHeight"
    report = {"decisions": [result], "keyDatumsM": {"parsons entry": {"Made Up": majority.key_datums({"imc": 0.10049, "imd": -0.00005})}}}
    args = argparse.Namespace(out=str(tmp_path / "run" / "majority.json"), overrides=str(tmp_path / "list.json"), keep_from=None)
    majority.finish(args, {"overrideNote": "n"}, {}, report, [], [])
    written = json.loads((tmp_path / "run" / "majority.json").read_text(encoding="utf-8"))
    decision = written["decisions"][0]
    assert decision["share"] == 0.3465 and decision["tests"]["typology"]["share"] == 0.3465 and decision["bands"]["1mm"]["share"] == 0.3465
    assert decision["tests"]["typology"]["rule"] == {"parsons | DP1": 0.1005} and decision["tests"]["profile"]["share"] == 0.3465
    assert written["keyDatumsM"]["parsons entry"]["Made Up"] == {"imc": 0.1005, "imd": -0.0001}
    # keyRules takes them as majority.json holds them, the table of the base at the 4 decimals it was measured at
    found = key_rules_block.block_of(written, ["baseHeight"], {}, block)["baseHeight"]
    assert found["share"] == 0.3465 and found["candidates"] == {"profile": 0.3465, "typology": 0.3465} and found["bands"]["1mm"]["share"] == 0.3465
    assert found["rule"] == "profile" and "table" not in found
    result["coded"] = "typology"
    majority.finish(args, {"overrideNote": "n"}, {}, {"decisions": [result]}, [], [])
    written = json.loads((tmp_path / "run" / "majority.json").read_text(encoding="utf-8"))
    assert key_rules_block.block_of(written, ["baseHeight"], {}, block)["baseHeight"]["table"] == {"DP1": 0.1005}


# R210, R340 the Z of the ramp corners by the chain of docs/addin-design.md section 5, Step 8, which the C# copies
def test_r340_the_back_corners_of_the_ramp_take_el_at_entry_as_written_at_4_decimals_then_3_when_written():
    import plan_from_book
    import plan_from_past
    builder = plan_from_past.PlotBuilder("1")
    builder.entry["unit"] = {"value": "DP1"}
    inputs = {"floors": {"levels": {"ramp": "L0"}, "types": {}}, "levels": {"L0": 0.0}, "bands": []}
    corners = [(0.0, 0.0), (4.0, 0.0), (4.0, 5.0), (0.0, 5.0)]
    # A raw El @ Entry of 10.02745 is 10.0275 as the plan writes it at 4, so the back corners are 10.028, one rounding of it gives 10.027
    entry = common.fixed(10.02745, 4)
    made = plan_from_book.ramp_floor(builder, None, corners, [10.0, 10.0], entry, 0.5, inputs, "Ramps 0.5%")
    assert entry == 10.0275 and [point[2] for point in made["corners"]["value"]] == [10.0, 10.0, 10.028, 10.028] and common.fixed(10.02745, 3) == 10.027
    # The back pair runs parallel to the front, each its front corner less the mean of the two plus El @ Entry, then 3 decimals
    made = plan_from_book.ramp_floor(builder, None, corners, [10.0004, 9.9996], entry, 0.5, inputs, "Ramps 0.5%")
    assert [point[2] for point in made["corners"]["value"]] == [10.0, 10.0, common.fixed(10.0275 - 0.0004, 3), common.fixed(10.0275 + 0.0004, 3)]
    assert made["heightOffset"]["value"] == common.fixed(9.9996, 3)
