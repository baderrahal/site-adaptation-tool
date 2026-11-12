# The compare

Written by python/compare_built.py, never by hand. Counts, shares and differences in mm only, no coordinate, no level, no plot number.

## The coordinate base of the past models

Measured first, on the past models alone: 1 distinct base of 7 reads, by the project position, the survey point and the base point of each read. The 7 past models share one coordinate base.

## The pass level

| Column | Limit | Source |
|---|---|---|
| Plots | 95 percent of the plots of the past model built | Bader 12 Nov 2026 |
| Position | 95 percent of the built plots within 5 mm, in plan | Bader 12 Nov 2026 |
| FFL | 95 percent of the built plots within 5 mm, by the origin Z of the villa link | Bader 12 Nov 2026 |
| Rotation | 95 percent of the built plots within 0.1 degree | Bader 12 Nov 2026 |
| Mirror | 95 percent of the built plots right | Bader 12 Nov 2026 |
| Floors, yards, walls, levels | each within 5 percent of the past count | Bader 12 Nov 2026 |

Yards are counted on IMC, IMD, IME, IMF. Plots, position, rotation and mirror only are counted on IMA, IMB. Floors are the floors that are no yard.

## The past models against themselves

| Neighbourhood | Past model | Plots | Position | FFL | Rotation | Mirror | Floors | Yards | Walls | Levels | Result |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 012 | IMC | 337 of 337, 100.0 percent, pass | 337 of 337, 100.0 percent, largest 0.0 mm, pass | 337 of 337, 100.0 percent, largest 0.0 mm, pass | 337 of 337, 100.0 percent, pass | 337 of 337, 100.0 percent, pass | 673 of 673, pass | 337 of 337, pass | 926 of 926, pass | 2 of 2, pass | pass |
| 013 | IMD | 410 of 410, 100.0 percent, pass | 410 of 410, 100.0 percent, largest 0.0 mm, pass | 410 of 410, 100.0 percent, largest 0.0 mm, pass | 410 of 410, 100.0 percent, pass | 410 of 410, 100.0 percent, pass | 820 of 820, pass | 410 of 410, pass | 1138 of 1138, pass | 2 of 2, pass | pass |
| 01A | IMG | 297 of 297, 100.0 percent, pass | 297 of 297, 100.0 percent, largest 0.0 mm, pass | 297 of 297, 100.0 percent, largest 0.0 mm, pass | 297 of 297, 100.0 percent, pass | 297 of 297, 100.0 percent, pass | 594 of 594, pass | not counted | 859 of 859, pass | 2 of 2, pass | pass |
| 02 | IME | 324 of 324, 100.0 percent, pass | 324 of 324, 100.0 percent, largest 0.0 mm, pass | 324 of 324, 100.0 percent, largest 0.0 mm, pass | 324 of 324, 100.0 percent, pass | 324 of 324, 100.0 percent, pass | 648 of 648, pass | 324 of 324, pass | 894 of 894, pass | 2 of 2, pass | pass |
| 021 | IMA | 432 of 432, 100.0 percent, pass | 432 of 432, 100.0 percent, largest 0.0 mm, pass | not counted | 432 of 432, 100.0 percent, pass | 432 of 432, 100.0 percent, pass | not counted | not counted | not counted | not counted | pass |
| 022 | IMB | 301 of 301, 100.0 percent, pass | 301 of 301, 100.0 percent, largest 0.0 mm, pass | not counted | 301 of 301, 100.0 percent, pass | 301 of 301, 100.0 percent, pass | not counted | not counted | not counted | not counted | pass |
| 03 | IMF | 490 of 490, 100.0 percent, pass | 490 of 490, 100.0 percent, largest 0.0 mm, pass | 490 of 490, 100.0 percent, largest 0.0 mm, pass | 490 of 490, 100.0 percent, pass | 490 of 490, 100.0 percent, pass | 980 of 980, pass | 490 of 490, pass | 1317 of 1317, pass | 2 of 2, pass | pass |

7 of 7 past models give 100 percent in every column that is counted.

## The built side against the past models

No built folder was given, every neighbourhood counts 0 built.

| Neighbourhood | Past model | Plots | Position | FFL | Rotation | Mirror | Floors | Yards | Walls | Levels | Result |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 010 | none | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN |
| 012 | IMC | 0 of 337, 0.0 percent, fail | no plot built, fail | no plot built, fail | no plot built, fail | no plot built, fail | 0 of 673, fail | 0 of 337, fail | 0 of 926, fail | 0 of 2, fail | fail |
| 013 | IMD | 0 of 410, 0.0 percent, fail | no plot built, fail | no plot built, fail | no plot built, fail | no plot built, fail | 0 of 820, fail | 0 of 410, fail | 0 of 1138, fail | 0 of 2, fail | fail |
| 01A | IMG | 0 of 297, 0.0 percent, fail | no plot built, fail | no plot built, fail | no plot built, fail | no plot built, fail | 0 of 594, fail | not counted | 0 of 859, fail | 0 of 2, fail | fail |
| 01B | none | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN, no past model | UNKNOWN |
| 02 | IME | 0 of 324, 0.0 percent, fail | no plot built, fail | no plot built, fail | no plot built, fail | no plot built, fail | 0 of 648, fail | 0 of 324, fail | 0 of 894, fail | 0 of 2, fail | fail |
| 021 | IMA | 0 of 432, 0.0 percent, fail | no plot built, fail | not counted | no plot built, fail | no plot built, fail | not counted | not counted | not counted | not counted | fail |
| 022 | IMB | 0 of 301, 0.0 percent, fail | no plot built, fail | not counted | no plot built, fail | no plot built, fail | not counted | not counted | not counted | not counted | fail |
| 03 | IMF | 0 of 490, 0.0 percent, fail | no plot built, fail | no plot built, fail | no plot built, fail | no plot built, fail | 0 of 980, fail | 0 of 490, fail | 0 of 1317, fail | 0 of 2, fail | fail |

0 of 9 neighbourhoods pass, 7 fail, 2 UNKNOWN.

## Notes

- 012: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 013: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 01A: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 02: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 021: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 022: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
- 03: past links with no plot number 1, built links with no plot number 0, built plots not in the past model 0, built plots with two links 0.
