"""Scores tests/holdout/predictions/*.json against the answers of the Actions secret HOLDOUT_ANSWERS. Prints counts, never a value.

The secret is one JSON object: {"passShare": 0.9, "tolerance": 0.005, "answers": {"<key>": <value>}}. A prediction file is one JSON object of the same
keys. NOT RUN while the secret or the predictions are missing.
"""
import json
import os
import sys
from pathlib import Path

import check_common

NAME = "hold-out"
PREDICTIONS = check_common.ROOT / "tests" / "holdout" / "predictions"


def score(answers, predictions, tolerance=0):
    """(matches, total). A missing prediction is a miss, numbers match within the tolerance of the secret."""
    matches = 0
    for key, answer in answers.items():
        got = predictions.get(key)
        if isinstance(answer, (int, float)) and isinstance(got, (int, float)):
            matches += abs(answer - got) <= tolerance
        else:
            matches += got == answer
    return matches, len(answers)


def main(argv=None, secret=None, folder=PREDICTIONS):
    secret = os.environ.get("HOLDOUT_ANSWERS") if secret is None else secret
    files = sorted(Path(folder).glob("*.json")) if Path(folder).is_dir() else []
    if not secret or not files:
        check_common.say(NAME, "NOT RUN", "no answers in the secret HOLDOUT_ANSWERS or no predictions yet")
        return 0
    block = json.loads(secret)
    predictions = {}
    for path in files:
        predictions.update(json.loads(path.read_text(encoding="utf-8")))
    matches, total = score(block["answers"], predictions, block.get("tolerance", 0))
    share = matches / total if total else 0
    line = "%d of %d match, share %.3f, needed %.3f" % (matches, total, share, block["passShare"])
    return check_common.finish(NAME, [] if share >= block["passShare"] else [line], line)


if __name__ == "__main__":
    sys.exit(main())
