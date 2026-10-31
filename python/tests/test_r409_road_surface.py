"""R409. The one named road level surface under rev6, made up in code (R10): the road surfaces of a build are designBasis.roadLevel.surface
alone, a name the inputs do not hold or no name is refused, the past job keeps siteBuild.roadSurfaces in their order, and oldJobOffsetM is
added to no level. DesignBasisTests.cs holds the same made up names."""
import pytest

import design_basis
import plan_rules
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings, rev6_build

SURFACE = "Made Up Road Surface"


def test_r409_the_road_surfaces_of_a_build_under_rev6_are_the_one_surface_of_design_basis_road_level():
    settings = basis_settings(mode="rev6", surface=SURFACE)
    profile = project_profile.load_profile(project_profile.profile_path(settings))
    lp = plan_rules.level_profile(profile)
    basis = design_basis.basis_of(settings)
    assert plan_rules.road_names(settings.get("siteBuild"), lp, basis) == [SURFACE]
    # The past job keeps the surfaces of siteBuild.roadSurfaces in their order for frontRoad roadLevel
    past = design_basis.basis_of(basis_settings(mode=PAST_JOB, surface=SURFACE))
    assert plan_rules.road_names(settings.get("siteBuild"), lp, past) == plan_rules.road_names(settings.get("siteBuild"), lp) == list(settings.get("siteBuild")["roadSurfaces"])


def test_r409_a_surface_name_the_inputs_do_not_hold_is_refused(tmp_path):
    basis = design_basis.basis_of(basis_settings(mode="rev6", surface=SURFACE))
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.check_surfaces(basis, tmp_path)
    assert str(error.value) == "designBasis.roadLevel.surface %s is no surface of the inputs, the landxml folder holds no %s.xml (R409)" % (SURFACE, SURFACE)
    (tmp_path / (SURFACE + ".xml")).write_text("<LandXML/>", encoding="utf-8")
    design_basis.check_surfaces(basis, tmp_path)


def test_r409_no_surface_name_is_refused_under_rev6_and_not_read_by_the_past_job():
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", surface=""))
    assert str(error.value) == "designBasis.roadLevel.surface names no road surface, a build under rev6 takes the road level from the one surface it names (R409)"
    assert design_basis.basis_of(basis_settings(mode=PAST_JOB, surface="")) == {"mode": PAST_JOB}


def test_r409_old_job_offset_m_is_added_to_no_level(monkeypatch):
    plain = rev6_build(monkeypatch, basis_settings(mode="rev6"))["levels"]["5"]
    settings = basis_settings(mode="rev6")
    settings.values["designBasis"]["roadLevel"]["oldJobOffsetM"] = 5.0
    moved = rev6_build(monkeypatch, settings)["levels"]["5"]
    assert moved == plain and moved["roadAtFront"] == (10.48 + 10.6) / 2.0
