import json
import math
from decimal import Decimal, ROUND_HALF_UP, localcontext
from pathlib import Path

DECIMALS = 3
WIDE_DECIMALS = 4
# R385. The significant digits of the text a value is rounded from
SIGNIFICANT = 15


def fixed(value, decimals=DECIMALS):
    """R385. The one rounding rule of Python and C#: the value written as its text of 15 significant digits, read as a decimal,
    rounded half away from zero to decimals and turned back into a float. The 15 digits drop the noise of the last bits of a
    double, so 0.7 + 0.0005, 0.7004999999999999 as a double, rounds as the decimal 0.7005 does. Minus zero is written as 0 and a value that is not finite is None.
    A value whose digit after the asked decimals, the one that decides a tie, lies past the 15 digits of its text is refused: the
    text itself would round there, and Python and net8.0 break an exact tie of 16 digits half to even where G15 of net48 goes half up."""
    if isinstance(value, int) and not isinstance(value, bool):
        return value
    if not math.isfinite(value):
        return None
    text = Decimal(format(value, ".15g"))
    if text and text.adjusted() + decimals + 2 > SIGNIFICANT:
        raise OverflowError("%s needs %d significant digits to round at %d decimals, more than the %d of its text (R385)" % (
            format(value, ".15g"), text.adjusted() + decimals + 2, decimals, SIGNIFICANT))
    # Enough digits for every value under the limit above
    with localcontext() as context:
        context.prec = 60
        rounded = float(text.quantize(Decimal(1).scaleb(-decimals), rounding=ROUND_HALF_UP))
    return 0 if rounded == 0 else rounded


def r3(value, decimals=DECIMALS):
    # -0 in a coordinate file reads as a real offset, so it is written as 0
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return value
    return fixed(value, decimals)


# Keys named in wide_keys keep WIDE_DECIMALS, every other number keeps DECIMALS
def round_tree(node, wide_keys=frozenset()):
    if isinstance(node, dict):
        return {key: (r3(item, WIDE_DECIMALS) if key in wide_keys else round_tree(item, wide_keys)) for key, item in node.items()}
    if isinstance(node, (list, tuple)):
        return [round_tree(item, wide_keys) for item in node]
    return r3(node)


def write_json(path, payload, wide_keys=frozenset()):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(round_tree(payload, wide_keys), indent=1, ensure_ascii=False), encoding="utf-8")
    return path
