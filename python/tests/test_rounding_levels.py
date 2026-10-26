"""R385 in the level builders and the rule paths, Round ADDIN 4b (Q080): the FFL of a piece, El @ Entry, the height, SSL, the level
overrides of a build and the gaps majority.py and key_rules.py write round a tie half away from zero on the 15 digit decimal, as
common.fixed does. Every number here is made up (R10)."""
import io
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
    # R278, a level profile with no level rules: the FFL by the same rule, the height there from the level before it is rounded
    lp = dict(LP, height="oneFflPerClusterAtTheHighestEntry")
    found = plan_rules.heights({"1": {"unit": "DP1", "entry": 119.3075}}, [], lp, PROFILE)["1"]
    assert (found["ffl"], found["height"], found["entry"]) == (119.408, 0.1, 119.3075) and found["ssl"] == common.fixed(119.4075 + GROUND, 3)


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
# length, angle or gap. The C# never computes them, majority.py and key_rules_block.py are the measure and stay in Python
LEFT = {"majority.py": ("round(counts.get(\"holds\", 0) / total, 4)", "covers.append(round(cover, 2))",
                        "str(1 + max(0, round((item[\"row\"][\"height\"] - base_height(profile, item[\"code\"])) / riser)))"),
        "key_rules_block.py": ("round(counts.get(\"holds\", 0) / total, 4)", "round(holds / clusters, 4)"),
        # The yard outline width key of floor_fit.width_key, a lookup the first method keeps with round() until step 8 of 4f (R358)
        "key_rules_check.py": ("\"width\": round(round(math.dist(frame[\"left\"], frame[\"right\"]) / step) * step, 3)",)}


@pytest.mark.parametrize("name", ["plan_rules.py", "plan_from_book.py", "levels.py", "key_levels.py", "key_rules.py", "majority.py", "key_rules_block.py",
                                  "key_rules_check.py"])
def test_r385_no_round_of_python_is_left_on_a_level_a_length_an_angle_or_a_gap_of_the_build_and_the_rules(name):
    left = [line for line in round_calls(ROOT / "python" / name) if not any(allowed in line for allowed in LEFT.get(name, ()))]
    assert left == []
