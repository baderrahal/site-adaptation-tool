"""R839. Every line of steps/compare.md is of a form python/compare_built.py writes: counts, shares and differences in mm only.

The repo is public, so a line a hand or another script added fails. The forms are written here on their own and share no code
with the script, so a change of the page changes both. A form check only: a number that stands where the script writes a count
reads as a count, so the check holds a page against a line of another shape, not against a changed count.
"""
import re
import sys

import check_common

NAME = "compare page"
PAGE = check_common.ROOT / "steps" / "compare.md"
N = r"\d{1,6}"
TENTH = r"\d{1,7}\.\d"
SHARE = r"%s of %s, \d{1,3}\.\d percent(?:, largest %s mm)?, (?:pass|fail)" % (N, N, TENTH)
CELL = r"(?:%s|%s of %s, (?:pass|fail)|no plot built, fail|not counted|UNKNOWN, (?:no past model|the past read holds no count|the built read holds no count))" % (SHARE, N, N)
# A neighbourhood id or a model code, three characters at most, so no longer number fits
NAME_OF = r"[0-9A-Za-z]{1,3}"
NAMES = r"(?:no model|%s(?:, %s)*)" % (NAME_OF, NAME_OF)
LIMIT = r"\d{1,3}(?:\.\d{1,3})?"
# A source is words and a date: no number of five digits and no decimal
SOURCE = r"(?:(?!\d{5}|\d\.\d)[A-Za-z0-9 ,.]){1,80}"
FORMS = [re.compile("^%s$" % form) for form in (
    r"",
    r"# The compare",
    r"Written by python/compare_built\.py, never by hand\. Counts, shares and differences in mm only, no coordinate, no level, no plot number\.",
    r"## The coordinate base of the past models",
    r"Measured first, on the past models alone: %s distinct bases? of %s reads, by the project position, the survey point and the base point of each read\. The %s past models (?:share|do not share) one coordinate base\." % (N, N, N),
    r"Measured first, on the past models alone: UNKNOWN, %s of %s reads hold no points file, so whether the past models share one coordinate base is not known\." % (N, N),
    r"## The pass level",
    r"\| Column \| Limit \| Source \|",
    r"\|(?:---\|){3}",
    r"\| Plots \| %s percent of the plots of the past model built \| %s \|" % (LIMIT, SOURCE),
    r"\| Position \| %s percent of the built plots within %s mm, in plan \| %s \|" % (LIMIT, LIMIT, SOURCE),
    r"\| FFL \| %s percent of the built plots within %s mm, by the origin Z of the villa link \| %s \|" % (LIMIT, LIMIT, SOURCE),
    r"\| Rotation \| %s percent of the built plots within %s degree \| %s \|" % (LIMIT, LIMIT, SOURCE),
    r"\| Mirror \| %s percent of the built plots right \| %s \|" % (LIMIT, SOURCE),
    r"\| Floors, yards, walls, levels \| each within %s percent of the past count \| %s \|" % (LIMIT, SOURCE),
    r"Yards are counted on %s\. Plots, position, rotation and mirror only are counted on %s\. Floors are the floors that are no yard\." % (NAMES, NAMES),
    r"## The past models against themselves",
    r"\| Neighbourhood \| Past model \| Plots \| Position \| FFL \| Rotation \| Mirror \| Floors \| Yards \| Walls \| Levels \| Result \|",
    r"\|(?:---\|){12}",
    r"\| %s \| (?:%s|none) \|(?: %s \|){9} (?:pass|fail|UNKNOWN) \|" % (NAME_OF, NAME_OF, CELL),
    r"%s of %s past models give 100 percent in every column that is counted\." % (N, N),
    r"## The built side against the past models",
    r"A built folder was given, %s of its folders are named as a neighbourhood below\." % N,
    r"No built folder was given, every neighbourhood counts 0 built\.",
    r"%s of %s neighbourhoods pass, %s fail, %s UNKNOWN\." % (N, N, N, N),
    r"## Notes",
    r"- %s: past links with no plot number %s, built links with no plot number %s, built plots not in the past model %s, built plots with two links %s\." % (NAME_OF, N, N, N, N),
)]


def faults(text):
    """The lines that are of no form of the page, each by its line number alone, so the fault repeats no value."""
    if "\r" in text:
        return ["the page holds a carriage return, the script writes none"]
    return ["line %d is of no form python/compare_built.py writes" % number
            for number, line in enumerate(text.split("\n"), start=1) if not any(form.match(line) for form in FORMS)]


def main():
    if not PAGE.is_file():
        check_common.say(NAME, "NOT RUN", "steps/compare.md is not there")
        return 0
    return check_common.finish(NAME, faults(PAGE.read_bytes().decode("utf-8")), "every line of steps/compare.md is of a form the script writes")


if __name__ == "__main__":
    sys.exit(main())
