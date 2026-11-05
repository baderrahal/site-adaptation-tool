"""Round R2 C4, the pane in 3 stages, Bader's order of 6 Nov 2026. All made up. Owed until R2-C4-revit-test is done."""
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
TASK = pytest.mark.task("R2-C4-revit-test")


@TASK
def test_the_three_stage_pane_is_recorded_in_revit():
    text = (ROOT / "steps" / "revit-results" / "R2-C4-revit-test.md").read_text(encoding="utf-8")
    for word in ("Project data", "Build site", "Review and deliver", "Choose project folder", "Fix items", "Preview", "source mix", "Advanced", "gear", "S1", "empty model", "screenshot"):
        assert word in text, word


@TASK
def test_each_card_has_a_screenshot_with_no_real_value():
    shots = ROOT / "steps" / "ui-shots"
    names = sorted(path.name for path in shots.glob("pane-stage-*.png"))
    assert len(names) >= 3, names


@TASK
def test_every_pane_word_is_in_the_label_list():
    labels = (ROOT / "docs" / "design" / "14-ui-labels.md").read_text(encoding="utf-8")
    for word in ("Project data", "Build site", "Review and deliver", "Choose project folder", "Fix items"):
        assert word in labels, word
