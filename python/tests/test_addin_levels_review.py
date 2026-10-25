"""Round ADDIN PR 3c, review of PR 48: each level of a plot stands alone (R339 to R341), a miss with no value is listed and never
written as an override (R272, R280, R330), the proof needs every input and says what holds by the rule alone (R340), the second
method counts an item one side holds as a mismatch (R341) and the split per unit sequence (R338, R339). Every name and number here
is made up (R10)."""
from test_addin_levels import LEVEL_RULES, level_build, level_of, level_run


# R340 an override moves the level of its own plot only
def test_r340_an_override_moves_the_level_of_its_own_plot_only_so_deleting_one_el_at_entry_line_of_a_piece_keeps_every_other_level(tmp_path, monkeypatch):
    from test_addin_review import entry
    # Plots 3 and 4 are a row pair and share one FFL, 4 sets it. The key holds El @ Entry of 4 at 1.3 mm over the rule and the FFL of
    # the pair at 1 mm over the rule, so the list holds the entryM of 4 and the heightM of 3 and of 4
    full = [entry("4", "entryM", {"gapToRuleM": 0.0013}, "parsons", "IMC.rvt", "imc"), entry("3", "heightM", {"gapToRuleM": 0.001}, "parsons", "IMC.rvt", "imc"),
            entry("4", "heightM", {"gapToRuleM": 0.001}, "parsons", "IMC.rvt", "imc")]
    found, _ = level_build(tmp_path, monkeypatch, overrides=full)
    assert level_of(found["3"], "ffl") == level_of(found["4"], "ffl") == 10.264 and level_of(found["4"], "entry") == 10.0639
    # The entryM line of 4 deleted by hand: El @ Entry of 4 goes back to the rule, every FFL stays on its key
    cut, _ = level_build(tmp_path, monkeypatch, overrides=full[1:])
    assert level_of(cut["3"], "ffl") == level_of(cut["4"], "ffl") == 10.264 and level_of(cut["4"], "entry") == 10.0626
    assert level_of(cut["3"], "entry") == level_of(found["3"], "entry") == 10.0526


# R339 the FFL is judged on the El @ Entry of the rule, so a plot whose FFL another plot's El @ Entry sets gets its own override
def test_r339_the_ffl_is_judged_on_the_el_at_entry_of_the_rule_so_each_plot_of_a_piece_off_its_key_gets_its_own_height_override():
    results, misses, _, _ = level_run()
    ffl = next(result for result in results if result["decision"] == "parsons ffl")
    # Plot 3 sets the FFL of the piece 3 and 4 by its El @ Entry 2.4 mm over the rule, so on the El @ Entry of the rule both are 2 mm under
    # the key, and plots 1 and 2 round to the next mm, four misses with a gap of their own
    found = sorted((miss["plot"], round(miss["value"] - miss["rule"], 4)) for miss in misses if miss["field"] == "heightM")
    assert found == [("1", -0.001), ("2", -0.001), ("3", 0.002), ("4", 0.002)]
    assert ffl["byBuild"] == {"holds": 3, "breaks": 4, "cannot": 0, "share": round(3 / 7, 4)}
