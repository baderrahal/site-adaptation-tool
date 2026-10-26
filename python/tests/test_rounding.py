"""R385. One rounding rule in Python and C#: the 15 significant digit text of a value read as a decimal and rounded half away from zero."""
import json
import math
from pathlib import Path

import pytest

import common

ROOT = Path(__file__).resolve().parents[2]
# The ties of docs/addin-design.md section 5, which Python round() takes down and C# Math.Round away from zero
TIES = [(12.3455, 12.346), (15.1235, 15.124), (1.0005, 1.001), (2.0465, 2.047), (7.0625, 7.063)]


def test_r385_a_tie_rounds_half_away_from_zero_on_the_decimal_value():
    assert [common.fixed(value, 3) for value, _ in TIES] == [rounded for _, rounded in TIES]
    assert [round(value, 3) for value, _ in TIES] != [rounded for _, rounded in TIES]
    assert [common.fixed(-value, 3) for value, _ in TIES] == [-rounded for _, rounded in TIES]
    assert common.fixed(0.00005, 4) == 0.0001 and common.fixed(2.5, 0) == 3 and common.fixed(-2.5, 0) == -3


def test_r385_float_noise_does_not_move_a_value():
    # The 15 digit text drops the noise of the last bits, the text of a value under 1E-04 holds an exponent
    assert format(0.1 + 0.2 - 0.3, ".15g") == "5.55111512312578e-17"
    assert common.fixed(0.1 + 0.2 - 0.3, 3) == 0
    assert common.fixed(0.000004, 4) == 0 and common.fixed(0.00001234, 4) == 0 and common.fixed(0.00001234, 5) == 0.00001
    assert common.fixed(150.38 - 150.0, 4) == 0.38
    assert common.fixed(0.30000000000000004, 3) == 0.3
    # 0.7 + 0.0005 is 0.7004999999999999 as a double, its 15 digit text is the tie 0.7005, so it rounds as that decimal does
    assert repr(0.7 + 0.0005) == "0.7004999999999999" and round(0.7 + 0.0005, 3) == 0.7
    assert common.fixed(0.7 + 0.0005, 3) == 0.701
    # The tie 0.5005 lies under its decimal as a double, as 12.3455 does
    assert round(0.5005, 3) == 0.5 and common.fixed(0.5005, 3) == 0.501


def test_r385_minus_zero_is_written_as_0_and_a_value_that_is_not_finite_stays_none():
    assert common.fixed(-0.0004, 3) == 0 and str(common.fixed(-0.0004, 3)) == "0"
    assert math.copysign(1, common.fixed(-0.0, 3)) == 1
    assert common.fixed(float("nan"), 3) is None and common.fixed(float("inf"), 3) is None and common.fixed(float("-inf"), 4) is None
    assert common.fixed(3, 3) == 3 and isinstance(common.fixed(171.5, 3), float)


def test_r385_a_value_the_decimal_of_csharp_cannot_hold_is_refused_never_written():
    for value in (1e300, -1e300, 7.93e28):
        with pytest.raises(OverflowError):
            common.fixed(value, 3)
    assert common.fixed(7.9e27, 3) == 7.9e27
    # 2 to the 96 as a double has the 15 digit text 7.92281625142643e+28, under the largest decimal, so both sides write it
    assert common.fixed(float(2 ** 96), 3) == 7.92281625142643e28
    with pytest.raises(OverflowError):
        common.r3(1e300)


def test_r385_r3_round_tree_and_write_json_go_through_the_rule(tmp_path):
    assert common.r3(2.0465) == 2.047 and common.r3(0.7 + 0.0005) == 0.701 and common.r3(True) is True and common.r3("2.0465") == "2.0465"
    assert common.r3(-0.0001) == 0 and common.r3(float("nan")) is None
    assert common.round_tree({"a": 2.0465, "e": 1.00005, "list": [12.3455, {"e": 0.00015}]}, frozenset({"e"})) == \
        {"a": 2.047, "e": 1.0001, "list": [12.346, {"e": 0.0002}]}
    path = common.write_json(tmp_path / "out.json", {"ffl": 7.0625, "entry": 15.12345}, frozenset({"entry"}))
    assert json.loads(path.read_text(encoding="utf-8")) == {"ffl": 7.063, "entry": 15.1235}
