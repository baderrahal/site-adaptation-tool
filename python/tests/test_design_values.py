import json
import re
from pathlib import Path

import pytest

import common
import corner_front
import design_values
import entry_levels
import key_rules
import levels
import plan_from_past
import plan_rules
import position_file

# The literals the code held before Q103 moved them, so the move changes no value by one unit
BEFORE = {
    "placer.coordinateToleranceM": 0.001, "placer.angleToleranceDeg": 0.001, "placer.zToleranceM": 0.001,
    "placer.readBackOriginM": 0.001, "placer.readBackAngleDeg": 0.01,
    "floors.topToleranceM": 0.001, "floors.levelToleranceM": 0.001, "floors.cornerOnVertexM": 0.002, "floors.shortestEdgeM": 0.001,
    "fronts.frontTieM": 1.0, "fronts.outwardProbeM": 0.01,
    "fronts.quadrantsDeg": {"EN": [0.0, 90.0], "SE": [90.0, 180.0], "WS": [180.0, 270.0], "NW": [270.0, 360.0]},
    "hands.sideBandM": 0.05, "hands.bufferSegmentsPerQuarter": 16, "siteCoordinates.toleranceMm": 1.0,
    "samePoint.sameMm": 0.5, "samePoint.sameDeg": 1e-6, "levels.cmGrainMm": 10,
    "rounding.decimals": 3, "rounding.wideDecimals": 4, "rounding.angleDecimals": 6, "rounding.entryDecimals": 4,
    "rounding.heightDecimals": 4, "rounding.fflDecimals": 3, "rounding.bandDecimals": 6,
}


@pytest.mark.parametrize("dotted", sorted(BEFORE))
def test_r405_each_design_value_of_the_config_equals_the_literal_it_replaced(dotted):
    assert design_values.value(dotted) == BEFORE[dotted]


def test_r405_the_python_twins_read_the_config():
    assert entry_levels.FRONT_TIE == design_values.value("fronts.frontTieM")
    assert plan_from_past.CORNER_ON_VERTEX == design_values.value("floors.cornerOnVertexM")
    assert (key_rules.SAME_MM, key_rules.SAME_DEG) == (design_values.value("samePoint.sameMm"), design_values.value("samePoint.sameDeg"))
    assert (position_file.SAME_MM, position_file.SAME_DEG) == (key_rules.SAME_MM, key_rules.SAME_DEG)
    assert {k: list(v) for k, v in corner_front.QUADRANTS.items()} == design_values.value("fronts.quadrantsDeg")
    assert (common.DECIMALS, common.WIDE_DECIMALS) == (3, 4)
    assert (levels.ENTRY_DECIMALS, levels.HEIGHT_DECIMALS, levels.FFL_DECIMALS, levels.BAND_DECIMALS) == (4, 4, 3, 6)
    assert plan_rules.CM_GRAIN_MM == design_values.value("levels.cmGrainMm")


# Each twin assigns its constant from the config, so a literal put back fails here
READS = {"entry_levels.py": ["FRONT_TIE"], "plan_from_past.py": ["CORNER_ON_VERTEX"], "key_rules.py": ["SAME_MM", "SAME_DEG"],
         "position_file.py": ["SAME_MM", "SAME_DEG"], "corner_front.py": ["QUADRANTS"], "common.py": ["DECIMALS", "WIDE_DECIMALS"],
         "levels.py": ["ENTRY_DECIMALS", "HEIGHT_DECIMALS", "FFL_DECIMALS", "BAND_DECIMALS"], "plan_rules.py": ["CM_GRAIN_MM"]}


@pytest.mark.parametrize("name", sorted(READS))
def test_r405_each_twin_assigns_its_design_value_from_the_config(name):
    text = (Path(design_values.__file__).parent / name).read_text(encoding="utf-8")
    for constant in READS[name]:
        assert re.search(r"^%s = .*design_values\.value\(" % constant, text, re.M), (name, constant)


def test_r405_ffl_down_to_the_cm_gives_the_same_text_as_the_old_floor_division():
    for ffl in (150.389, 150.38, 150.0, 0.009, -0.001, -1.237, 2.5, 99.999, 100.0005):
        mm = int(common.fixed(ffl * 1000, 0))
        assert plan_rules.prototype_ffl("fflDownToTheCm", ffl) == "%.3f" % ((mm // 10) / 100.0)


def test_r405_a_missing_value_is_refused_by_its_name():
    config = json.loads(design_values.SETTINGS.read_text(encoding="utf-8"))
    del config["designValues"]["fronts"]["frontTieM"]
    with pytest.raises(design_values.DesignValueError, match="designValues.fronts.frontTieM"):
        design_values.value_of(config, "fronts.frontTieM")
    with pytest.raises(design_values.DesignValueError, match="no designValues block"):
        design_values.value_of({}, "fronts.frontTieM")
