"""Round ADDIN PR 3a, last pass on PR 47: the steps of the level profile (R337), the overrides a build applies and leaves plot by
plot (R337, R280), the rules a build carries out (R337), the hand edits of Bader (R330, R336) and the keyRules block (R338).
Every name and number here is made up (R10)."""
import plan_rules
from test_addin_review import by_plot, key_build, made_key_rules


# R337 the steps come from the step bands of the level profile until 3c codes them, and say so
def test_r337_the_steps_parameters_are_labelled_by_the_step_bands_of_the_level_profile_until_3c_codes_the_steps(tmp_path, monkeypatch):
    plan, rules, _ = key_build(tmp_path, monkeypatch)
    found = by_plot(plan)["1"]["parameters"]
    assert found["RRE_ZZ_SP_Number-of-Steps"]["source"] == "levelProfileStepBands"
    assert found["RRE_ZZ_SP_RVT-Name"]["source"] == "levelProfileStepBands"
    assert found["CASE"]["source"] == "keyRules" and found["RRE_ZZ_SP_Plot-Number"]["source"] == "keyRules"
    assert plan_rules.step_parameters(made_key_rules(), set()) == {"RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name"}
    assert plan_rules.step_parameters(made_key_rules(), {"parameter RRE_ZZ_SP_RVT-Name"}) == {"RRE_ZZ_SP_Number-of-Steps"}
    steps = plan["summary"]["rules"]["steps"]
    assert steps == {"from": "stepBands of the level profile parsons2024Rev6", "parameters": ["RRE_ZZ_SP_Number-of-Steps", "RRE_ZZ_SP_RVT-Name"]}
