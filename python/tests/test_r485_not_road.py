"""R485. designBasis.roadLevel.notRoad, the grading files of the road folder that no build takes, read under rev6 alone. R485_NotRoadTests.cs holds the same
lines for the C#. Made up names in a config copy in memory, the 7 stems are read from the shipped config."""
import json
from pathlib import Path

import pytest

import design_basis
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings

GRADING = "Made Up Job_Grading__Lot 7 - (3)"
WHY = "made up: a grading of the lots, not finished road"


def not_road_settings(mode="rev6", **road):
    settings = basis_settings(mode=mode)
    settings.values["designBasis"]["roadLevel"].update(road)
    return settings


def test_r485_a_surface_of_config_that_not_road_lists_is_refused_with_its_file_and_why():
    settings = not_road_settings(surfaces=["Made Up Job__Road A", GRADING], notRoad={GRADING: WHY})
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(settings)
    assert str(error.value) == "%s is no road surface: %s (R485)" % (GRADING, WHY)


def test_r485_the_order_of_a_reader_refuses_it_the_same():
    settings = not_road_settings(surfaces=[GRADING], notRoad={GRADING: WHY})
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.road_order(settings.values)
    assert str(error.value) == "%s is no road surface: %s (R485)" % (GRADING, WHY)


def test_r485_not_road_is_ignored_under_the_past_job():
    settings = not_road_settings(mode=PAST_JOB, surfaces=[GRADING], notRoad={GRADING: WHY})
    assert design_basis.basis_of(settings) == {"mode": PAST_JOB}
    assert design_basis.road_order(settings.values) is None


def test_r485_a_surface_not_listed_stands():
    settings = not_road_settings(surfaces=["Made Up Job__Road A"], notRoad={GRADING: WHY})
    assert design_basis.basis_of(settings)["surfaces"] == ["Made Up Job__Road A"]


def test_r485_the_stem_is_held_whatever_its_case_as_a_windows_file_name_is():
    settings = not_road_settings(surfaces=[GRADING.upper()], notRoad={GRADING: WHY})
    with pytest.raises(project_profile.ProfileError):
        design_basis.basis_of(settings)


@pytest.mark.parametrize("bad", [["list"], "text", {GRADING: ""}, {GRADING: 7}])
def test_r485_a_not_road_that_is_no_object_of_stem_and_why_is_refused(bad):
    settings = not_road_settings(notRoad=bad)
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(settings)
    assert str(error.value) == "designBasis.roadLevel.notRoad must be an object of file stem to why (R485)"


def test_r485_the_shipped_config_lists_the_7_grading_files_and_names_none_of_them_a_road():
    values = project_profile.load_settings().values
    block = values["designBasis"]["roadLevel"]
    reasons = block["notRoad"]
    assert len(reasons) == 7
    assert all(isinstance(why, str) and "grading" in why.lower() and "not finished road" in why.lower() for why in reasons.values())
    assert not set(reasons) & set(block["surfaces"])
    assert not set(reasons) & set(values["siteBuild"]["roadSurfaces"])


def test_r485_a_plan_holds_no_key_of_that_block():
    source = Path(design_basis.__file__).with_name("plan_from_book.py").read_text(encoding="utf-8")
    assert "notRoad" not in source
    assert "notRoad" not in json.dumps(design_basis.basis_of(not_road_settings(mode=PAST_JOB)))
