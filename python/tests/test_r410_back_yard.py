"""R410. The back yard by Rev 6 on the made up row of test_level_floor_steps.py built in code (R10): the step floors gives each unit plot its
yardGrade, toward the front within back_rev6_min to back_rev6_max or for a unit of attachment both_sides toward the back at townhouse_back_rev6,
no yard floor is placed, backYard.floor true is refused naming Q225 and the past job keeps the flat yard of keyRules.yard. DesignBasisTests.cs
holds the same made up data."""
import pytest

import design_basis
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build

FRONT_GRADE = {"toward": "front", "minPercent": 0.3, "maxPercent": 0.5}
BACK_GRADE = {"toward": "back", "minPercent": 2.0, "maxPercent": 2.0}


def test_r410_the_step_floors_gives_each_unit_plot_its_yard_grade_by_the_manual_and_no_yard_floor(monkeypatch):
    built = rev6_build(monkeypatch, basis_settings(mode="rev6"), units={"5": "TH1-IM"})
    floors = built["floors"]
    # 4 a DP2, one side attached, toward the front, 5 a TH1-IM of attachment both_sides toward the back, 7 no unit plot
    assert floors["4"]["yardGrade"] == FRONT_GRADE and floors["5"]["yardGrade"] == BACK_GRADE and floors["7"]["yardGrade"] is None
    assert all((floors[plot]["yardDepth"], floors[plot]["yardUnit"], floors[plot]["yardWidth"], floors[plot]["yardType"], floors[plot]["yardOutline"]) ==
               (None, None, None, None, None) for plot in floors)
    assert not any("yard" in entry["floors"] for entry in built["plots"]) and built["plots"]
    assert all(built["overrides"][plot]["yardOutline"] is None and built["overrides"][plot]["yardZ"] is None for plot in built["overrides"])
    assert built["summary"]["floorsFound"]["yard"] == 0


def test_r410_the_grade_values_come_from_slopes_percent_of_the_rules_profile():
    profile = project_profile.load_profile(project_profile.profile_path(project_profile.load_settings()))
    slopes = profile.get("slopes_percent")
    assert design_basis.yard_grade("DP2", profile) == {"toward": "front", "minPercent": slopes["back_rev6_min"], "maxPercent": slopes["back_rev6_max"]}
    assert design_basis.yard_grade("TH1", profile) == design_basis.yard_grade("TH3", profile) == {
        "toward": "back", "minPercent": slopes["townhouse_back_rev6"], "maxPercent": slopes["townhouse_back_rev6"]}


def test_r410_back_yard_floor_true_under_rev6_is_refused_by_name_naming_q225():
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", floor=True))
    assert str(error.value) == ("designBasis.backYard.floor is true, the manual gives no level for a yard floor under rev6, so none is placed until Q225 is "
                                "answered (R410)")
    with pytest.raises(project_profile.ProfileError):
        design_basis.basis_of(basis_settings(mode="rev6", floor="no"))


def test_r410_the_past_job_keeps_the_flat_yard_floor_of_key_rules_yard(monkeypatch):
    built = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB, floor=True))
    assert built["floors"]["5"]["yardDepth"] == 0.1 and built["floors"]["5"]["yardGrade"] is None and "yard" in built["plan"]["5"]["floors"]
