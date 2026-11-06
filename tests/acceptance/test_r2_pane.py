"""Round R2 C4, the pane in 3 stages, Bader's order of 6 Nov 2026. All made up. Owed until FAST-3-revit-test is done, R2-C4 was folded into FAST-1-build-1 on 6 Nov."""
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
TASK = pytest.mark.task("FAST-3-revit-test")


@TASK
def test_the_three_stage_pane_is_recorded_in_revit():
    text = (ROOT / "steps" / "revit-results" / "FAST-3-revit-test.md").read_text(encoding="utf-8")
    for word in ("Project data", "Build site", "Review and deliver", "Choose project folder", "Fix items", "Preview", "source mix", "Advanced", "gear", "S1", "empty model", "screenshot"):
        assert word in text, word


@TASK
def test_each_card_has_a_screenshot_by_its_name():
    # The shots hold no real value, which the review of each PR checks by eye, a test cannot
    shots = ROOT / "steps" / "ui-shots"
    for name in ("pane-stage-1.png", "pane-stage-2.png", "pane-stage-3.png"):
        assert (shots / name).is_file(), name


@TASK
def test_every_pane_word_is_in_the_label_list():
    labels = (ROOT / "docs" / "design" / "14-ui-labels.md").read_text(encoding="utf-8")
    for word in ("Project data", "Build site", "Review and deliver", "Choose project folder", "Fix items"):
        assert word in labels, word
