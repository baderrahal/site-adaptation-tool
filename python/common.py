import json
import math
from pathlib import Path

DECIMALS = 3
WIDE_DECIMALS = 4


def r3(value, decimals=DECIMALS):
    # -0 in a coordinate file reads as a real offset, so it is written as 0
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return value
    if isinstance(value, float) and not math.isfinite(value):
        return None
    rounded = round(value, decimals)
    return 0 if rounded == 0 else rounded


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
