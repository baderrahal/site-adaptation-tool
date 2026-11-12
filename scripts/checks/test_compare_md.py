"""R839. The check of steps/compare.md, on the page of the repo and on made up lines. No real number is here."""
import pytest

import compare_md

GOOD = "\n".join([
    "# The compare",
    "",
    "| N1 | KA | 38 of 40, 95.0 percent, pass | 37 of 38, 97.3 percent, largest 300.0 mm, pass | not counted | 36 of 38, 94.7 percent, fail"
    " | no plot built, fail | 105 of 100, pass | UNKNOWN, the built read holds no count | 0 of 200, fail | 21 of 20, pass | fail |",
    "| N3 | none |" + " UNKNOWN, no past model |" * 9 + " UNKNOWN |",
    "- N1: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.",
    "0 of 3 neighbourhoods pass, 2 fail, 1 UNKNOWN.",
    ""])


def test_r839_the_page_of_the_repo_passes_the_check():
    assert compare_md.PAGE.is_file(), "steps/compare.md is written by python/compare_built.py"
    assert compare_md.faults(compare_md.PAGE.read_bytes().decode("utf-8")) == []
    assert compare_md.main() == 0


def test_r839_lines_of_the_script_pass():
    assert compare_md.faults(GOOD) == []


@pytest.mark.parametrize("line", [
    "The villa of plot 1234 stands at 123456.789, 7654321.123.",
    "| N1 | KA | 38 of 40, 95.0 percent, pass | 12.345 |",
    "- N1: the FFL is 12.345",
    "| N1 | KA | 38 of 40, 95.0 percent, largest 123456.789 mm, pass |" + " not counted |" * 8 + " pass |",
    "A note by hand.",
    "| Plots | 95 percent of the plots of the past model built | Survey 24345.678 E 5432.1 N |",
    "| Plots | 95 percent of the plots of the past model built | Point 2434567 |",
    "- A1234567: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.",
    "Yards are counted on 24345678, 5432100. Plots, position, rotation and mirror only are counted on KB. Floors are the floors that are no yard.",
    " # The compare",
    "0 of 3 neighbourhoods pass, 2 fail, 1 UNKNOWN. And more.",
])
def test_r839_a_line_of_no_form_fails_by_its_line_number(line):
    assert compare_md.faults(GOOD + line + "\n") == ["line 7 is of no form python/compare_built.py writes"]


def test_r839_a_carriage_return_fails():
    assert compare_md.faults(GOOD.replace("\n", "\r\n")) == ["the page holds a carriage return, the script writes none"]


def test_r839_the_fault_repeats_no_value_of_the_line():
    assert "123456" not in " ".join(compare_md.faults("at 123456.789\n"))
