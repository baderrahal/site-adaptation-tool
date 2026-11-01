"""R408. The door level as the input under rev6 on the made up row of test_level_floor_steps.py built in code (R10): El @ Entry from a made
up civil file or from the road at front plus the slope of the unit, the slope the result with the ramp type of its band, the list of slopes
outside slopes_percent front_min to front_max and each refusal of the civil file. DesignBasisTests.cs holds the same made up data."""
import pytest

import common
import design_basis
import plan_from_book
import project_profile
from test_r407_design_basis import PAST_JOB, basis_settings, civil_csv, rev6_build

DEPTH = 5.52
# The road levels of the made up row at the two front corners of plots 5 and 6, 10 m plus a hundredth of x
ROAD_5 = (10.48 + 10.6) / 2.0
ROAD_6 = (10.6 + 10.72) / 2.0


def civil_build(monkeypatch, tmp_path, rows, **options):
    path = civil_csv(tmp_path, rows)
    return rev6_build(monkeypatch, basis_settings(mode="rev6", door={"source": "civilFile"}, civil={"path": str(path)}), **options), path


def test_r408_civil_file_el_at_entry_is_the_level_of_the_file_and_the_slope_the_result(monkeypatch, tmp_path):
    rows = [("1", "10.2"), ("2", "10.3"), ("3", "10.35"), ("5", "10.6"), ("6", "10.9")]
    built, _ = civil_build(monkeypatch, tmp_path, rows)
    five, six = built["levels"]["5"], built["levels"]["6"]
    assert (five["doorSource"], five["entry"]) == ("civilFile", 10.6)
    assert five["slopePercent"] == (10.6 - ROAD_5) / DEPTH * 100.0
    # 1.087 percent lies in the band under 1.25 of config/floor_outlines.json, 4.348 percent over it
    assert (built["floors"]["5"]["rampClass"], built["floors"]["5"]["rampType"]) == (None, "Ramps 0.5%")
    assert built["floors"]["6"]["rampType"] == "Ramps 2%" and built["plan"]["6"]["floors"]["ramp"]["type"]["value"] == "Ramps 2%"
    assert built["plan"]["5"]["floors"]["ramp"]["slopePercent"]["value"] == common.fixed(five["slopePercent"], 3)
    # FFL, height and steps by the level rules of keyRules from that El @ Entry: 5 and 6 a row pair at the highest entry plus the DP2 base
    assert five["ffl"] == six["ffl"] == 10.9 + 0.2 and built["plan"]["5"]["levels"]["ffl"]["value"] == 11.1
    assert built["plan"]["5"]["levels"]["height"]["value"] == common.fixed(11.1 - 10.6, 4)
    # The ramp type of the overrides step is the one the plan writes
    assert built["overrides"]["6"]["rampType"] == "Ramps 2%"


def test_r408_a_plot_the_civil_file_does_not_hold_gets_no_el_at_entry_and_is_listed_with_why(monkeypatch, tmp_path):
    built, path = civil_build(monkeypatch, tmp_path, [("1", "10.2"), ("2", "10.3"), ("3", "10.35"), ("5", "10.6"), ("6", "10.7")])
    four = built["levels"]["4"]
    assert (four["doorSource"], four["entry"], four["slopePercent"], four["ffl"]) == ("civilFile", None, None, None)
    assert built["floors"]["4"]["rampType"] is None and "4" not in built["plan"]
    why = "the civil file civil.csv holds no row of plot 4, so no El @ Entry (R408)"
    assert built["summary"]["noDoorLevel"] == [{"plot": "4", "reason": why}]
    assert built["summaryStep"][1]["summary"]["noDoorLevel"] == [{"plot": "4", "reason": why}]


def test_r408_road_plus_slope_el_at_entry_is_the_road_at_front_plus_the_slope_of_the_unit_over_the_ramp_depth(monkeypatch):
    settings = basis_settings(mode="rev6", door={"source": "roadPlusSlope", "slopePercent": {"default": 0.5, "VL4": 2.0}})
    built = rev6_build(monkeypatch, settings, units={"6": "VL4"})
    five, six = built["levels"]["5"], built["levels"]["6"]
    # No offsetM of the level profile is added under rev6
    assert five["doorSource"] == "roadPlusSlope" and five["entry"] == ROAD_5 + 0.5 / 100.0 * DEPTH
    assert five["slopePercent"] == pytest.approx(0.5, abs=1e-12) and built["floors"]["5"]["rampType"] == "Ramps 0.5%"
    assert six["entry"] == ROAD_6 + 2.0 / 100.0 * DEPTH and built["floors"]["6"]["rampType"] == "Ramps 2%"
    assert built["summary"]["noDoorLevel"] == [] and built["summary"]["slopeOutsideManual"] == []


def test_r408_a_slope_outside_front_min_to_front_max_is_listed_as_outside_the_manual(monkeypatch, tmp_path):
    # 5 at 0.3 percent under front_min 0.5, 6 at 4.348 percent over front_max 2, 2 and 3 at 1 percent inside, 4 with no door level
    road_2, road_3 = (10.12 + 10.24) / 2.0, (10.24 + 10.36) / 2.0
    rows = [("1", "10.2"), ("2", repr(round(road_2 + 0.01 * DEPTH, 6))), ("3", repr(round(road_3 + 0.01 * DEPTH, 6))), ("5", repr(round(ROAD_5 + 0.003 * DEPTH, 6))),
            ("6", "10.9")]
    built, _ = civil_build(monkeypatch, tmp_path, rows)
    listed = built["summary"]["slopeOutsideManual"]
    assert [item["plot"] for item in listed if item["plot"] != "1"] == ["5", "6"]
    assert {item["plot"]: item["slopePercent"] for item in listed}["5"] == 0.3
    assert {item["plot"]: item["slopePercent"] for item in listed}["6"] == common.fixed((10.9 - ROAD_6) / DEPTH * 100.0, 3)
    assert built["summaryStep"][1]["summary"]["slopeOutsideManual"] == listed
    # The slope as the plan writes it at 3 decimals is held against the limits, so 0.5 percent by roadPlusSlope is inside
    assert design_basis.outside_manual(0.49999999999, {"front_min": 0.5, "front_max": 2.0}) is False
    assert design_basis.outside_manual(2.0004, {"front_min": 0.5, "front_max": 2.0}) is False
    assert design_basis.outside_manual(2.0051, {"front_min": 0.5, "front_max": 2.0}) is True


@pytest.mark.parametrize("rows, header, why", [
    (None, ("Plot No.", "El @ Entry"), "designBasis.doorLevel.civilFile.path {path} is no file, the civil file of the door levels is missing (R408)"),
    ([("5", "10.6")], ("Plot", "El @ Entry"), "the civil file civil.csv holds no column Plot No., designBasis.doorLevel.civilFile.plotColumn (R408)"),
    ([("5", "10.6")], ("Plot No.", "Level"), "the civil file civil.csv holds no column El @ Entry, designBasis.doorLevel.civilFile.levelColumn (R408)"),
    ([("5", "nan")], ("Plot No.", "El @ Entry"), "the civil file civil.csv gives plot 5 the level 'nan', which is no finite number (R408)"),
    ([("5", "")], ("Plot No.", "El @ Entry"), "the civil file civil.csv gives plot 5 the level '', which is no finite number (R408)"),
    ([("5", "1_0.6")], ("Plot No.", "El @ Entry"), "the civil file civil.csv gives plot 5 the level '1_0.6', which is no finite number (R408)"),
    ([("5", "10.6"), ("5", "10.7")], ("Plot No.", "El @ Entry"), "the civil file civil.csv holds plot 5 twice (R408)"),
])
def test_r408_a_civil_file_missing_without_its_column_with_a_level_no_finite_number_or_a_plot_twice_is_refused(tmp_path, rows, header, why):
    path = tmp_path / "missing.csv" if rows is None else civil_csv(tmp_path, rows, header)
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", door={"source": "civilFile"}, civil={"path": str(path)}))
    assert str(error.value) == why.format(path=path)


def test_r408_a_civil_file_path_null_and_a_source_outside_sources_are_refused():
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", door={"source": "civilFile"}, civil={"path": None}))
    assert str(error.value) == "designBasis.doorLevel.civilFile.path none is no file, the civil file of the door levels is missing (R408)"
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", door={"source": "survey"}))
    assert str(error.value) == ("designBasis.doorLevel.source survey is no source of designBasis.doorLevel.sources, El @ Entry comes from civilFile or "
                                "roadPlusSlope (R408)")
    with pytest.raises(project_profile.ProfileError) as error:
        design_basis.basis_of(basis_settings(mode="rev6", door={"slopePercent": {"VL4": 2.0}}))
    assert "designBasis.doorLevel.slopePercent must hold a finite number for default" in str(error.value)


def test_r408_the_civil_file_is_an_input_of_the_build_steps_by_its_role_with_the_floor_file_of_the_bands(tmp_path):
    import argparse
    path = civil_csv(tmp_path, [("5", "10.6")])
    floors = tmp_path / "floor_outlines.json"
    floors.write_text("{}", encoding="utf-8")
    args = argparse.Namespace(cover="all", settings=str(project_profile.SETTINGS_PATH), baseplan=None, plots_csv=None, labels=None, footprint_blocks=None,
                              sales=None, roads=None, overrides=None, civil_file=str(path), floors=str(floors), design_basis="rev6")
    roles = {item["role"]: item for item in plan_from_book.build_inputs(args, project_profile.load_settings(), "levels")}
    assert roles[plan_from_book.CIVIL_ROLE]["path"] == str(path) and roles[plan_from_book.BANDS_ROLE]["path"] == str(floors)
    assert len(roles[plan_from_book.CIVIL_ROLE]["sha256"]) == 64


def slope_build(monkeypatch, overrides):
    """The made up row under rev6 with roadPlusSlope at 1.5 percent, in the band Ramps 2% and inside the manual, and the override list given."""
    settings = basis_settings(mode="rev6", door={"source": "roadPlusSlope", "slopePercent": {"default": 1.5}})
    return rev6_build(monkeypatch, settings, overrides=overrides)


def final_slope(road, entry_gap=0.0, front_gap=0.0):
    # El @ Entry of the rule at 4 decimals moved by its gap, less the road at front moved by its gap, over the ramp depth
    rule = common.fixed(road + 1.5 / 100.0 * DEPTH, 4)
    return (rule + entry_gap - (road + front_gap)) / DEPTH * 100.0


def test_r408_an_entry_override_gives_the_slope_ramp_type_and_outside_list_of_the_moved_el_at_entry(monkeypatch):
    built = slope_build(monkeypatch, {"5": {"entryM": {"gapToRuleM": -0.06}}})
    slope = final_slope(ROAD_5, entry_gap=-0.06)
    # By hand 1.5 less 6 cm over 5.52 m, 0.413 percent, under front_min 0.5 and in the band under 1.25
    assert common.fixed(slope, 3) == 0.413
    ramp = built["plan"]["5"]["floors"]["ramp"]
    assert ramp["slopePercent"]["value"] == 0.413 and ramp["type"]["value"] == "Ramps 0.5%"
    assert built["overrides"]["5"]["slopePercent"] == pytest.approx(slope, abs=1e-9) and built["overrides"]["5"]["rampType"] == "Ramps 0.5%"
    assert built["summary"]["slopeOutsideManual"] == [{"plot": "5", "slopePercent": 0.413}]
    assert built["summaryStep"][1]["summary"]["slopeOutsideManual"] == [{"plot": "5", "slopePercent": 0.413}]
    # The step levels keeps the slope by the rule, the step before the overrides
    assert built["levels"]["5"]["slopePercent"] == pytest.approx(1.5, abs=1e-12) and built["floors"]["5"]["rampType"] == "Ramps 2%"


def test_r408_a_ramp_front_override_gives_the_slope_ramp_type_and_outside_list_of_the_moved_front(monkeypatch):
    built = slope_build(monkeypatch, {"6": {"rampFrontM": {"gapToRuleM": 0.07}}})
    slope = final_slope(ROAD_6, front_gap=0.07)
    # By hand 1.5 less 7 cm over 5.52 m, 0.232 percent
    assert common.fixed(slope, 3) == 0.232
    ramp = built["plan"]["6"]["floors"]["ramp"]
    assert ramp["slopePercent"]["value"] == 0.232 and ramp["type"]["value"] == "Ramps 0.5%"
    # The slope the plan writes is that of its own corners, the back pair at El @ Entry and the front pair at the moved road
    corners = [corner[2] for corner in ramp["corners"]["value"]]
    assert abs((corners[2] + corners[3] - corners[0] - corners[1]) / 2.0 / DEPTH * 100.0 - ramp["slopePercent"]["value"]) < 0.05
    assert built["overrides"]["6"]["slopePercent"] == pytest.approx(slope, abs=1e-9) and built["overrides"]["6"]["rampType"] == "Ramps 0.5%"
    assert built["summary"]["slopeOutsideManual"] == [{"plot": "6", "slopePercent": 0.232}]
    assert built["summaryStep"][1]["summary"]["slopeOutsideManual"] == [{"plot": "6", "slopePercent": 0.232}]
    assert built["levels"]["6"]["slopePercent"] == pytest.approx(1.5, abs=1e-12)


def test_r408_a_plot_with_no_override_keeps_the_slope_of_the_rule(monkeypatch):
    built = slope_build(monkeypatch, {})
    for number in ("4", "5", "6"):
        ramp = built["plan"][number]["floors"]["ramp"]
        assert ramp["slopePercent"]["value"] == 1.5 and ramp["type"]["value"] == "Ramps 2%"
        assert built["overrides"][number]["slopePercent"] == built["levels"][number]["slopePercent"]
    assert built["summary"]["slopeOutsideManual"] == []
    # The step overrides of a build of the past job carries no slope
    past = rev6_build(monkeypatch, basis_settings(mode=PAST_JOB))
    assert {number: made["slopePercent"] for number, made in past["overrides"].items()} == dict.fromkeys(past["overrides"])
