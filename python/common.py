import json
import math
from pathlib import Path

DECIMALS = 3


def r3(value):
    # -0 in a coordinate file reads as a real offset, so it is written as 0
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return value
    if isinstance(value, float) and not math.isfinite(value):
        return None
    rounded = round(value, DECIMALS)
    return 0 if rounded == 0 else rounded


def round_tree(node):
    if isinstance(node, dict):
        return {key: round_tree(item) for key, item in node.items()}
    if isinstance(node, (list, tuple)):
        return [round_tree(item) for item in node]
    return r3(node)


def write_json(path, payload):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(round_tree(payload), indent=1, ensure_ascii=False), encoding="utf-8")
    return path
