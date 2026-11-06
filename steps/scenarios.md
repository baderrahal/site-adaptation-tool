# Scenarios

Written by scripts/scenarios/score_scenarios.py, never by hand. Stage 1 on each folder under the work root, scored against its right answer. Counts and marks only, the folders stay out of git and what each data check said is in scenarios\report.md under the work root. The right answers leave the hold-out neighbourhoods out.

## With no confirmed input

| Scenario | What it is | Ran | Built from | Plots read | Numbers right | Villa type right | Mirror right | Group words right | Marks: strong, usable, weak, missing | Copies unchanged | Areas read | Areas on the sheet, second check |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| S1 | raw inputs only, every past output removed | done | e0e5543 | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 448 of 1891 | not scored | 3, 1, 5, 2 | yes | 2 | 13 |
| S2 | as S1, footprint drawing removed | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 0 of 1891 | not scored | 3, 1, 5, 2 | yes | 2 | 13 |
| S3 | as S1, zone and neighbourhood words changed to Phase and Block | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 231 of 1891 | 448 of 1891 | 0 of 2 | 3, 1, 5, 2 | yes | 2 | not found |
| S4 | as S1, no LandXML and no Civil 3D surfaces, 2D CAD only | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 233 of 1891 | 448 of 1891 | not scored | 3, 0, 5, 3 | yes | 2 | 13 |
| S5 | as S1, plot numbers in another style | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 125 of 1891 | 448 of 1891 | not scored | 3, 1, 5, 2 | yes | 2 | 13 |

Files not read: S1 0, S2 0, S3 0, S4 0, S5 0.

## Mark of each need

| Need | S1 | S2 | S3 | S4 | S5 |
|---|---|---|---|---|---|
| plotOutlines | strong | strong | strong | strong | strong |
| plotNumbers | strong | strong | strong | strong | strong |
| groupLevels | weak | weak | weak | weak | weak |
| villaType | weak | weak | weak | weak | weak |
| villaBlocks | weak | weak | weak | weak | weak |
| roadEdges | weak | weak | weak | weak | weak |
| roadLevels | usable | usable | usable | missing | usable |
| coordinates | weak | weak | weak | weak | weak |
| prototypes | strong | strong | strong | strong | strong |
| rules | missing | missing | missing | missing | missing |
| sharedParameters | missing | missing | missing | missing | missing |

## Build plan, what was built and what was missing

siteBuild on the plot table of each run above (scripts/scenarios/run_build.py). No past file and no override list is read. A value with no rule stays missing and its plot is marked.

| Scenario | Plots planned | Built | Missing | Plots marked | Blocking | Issues | Copied from the past | Seconds |
|---|---|---|---|---|---|---|---|---|
| S1 | 2964 | 9474 | 26094 | 2964 | 0 | 3 | 0 | 1.6 |
| S2 | no build made | | | | | | | |
| S3 | no build made | | | | | | | |
| S4 | no build made | | | | | | | |
| S5 | no build made | | | | | | | |

## With a confirmed profile

Each scenario again on what its own first run proposed, as a person pressing Confirm on all of it (scripts/scenarios/confirmed_profile.py). Nothing comes from a past file or an answer, a proposal that is wrong stays wrong and scores so.

| Scenario | What it is | Ran | Built from | Plots read | Numbers right | Villa type right | Mirror right | Group words right | Marks: strong, usable, weak, missing | Copies unchanged | Areas read | Areas on the sheet, second check |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| S1 | raw inputs only, every past output removed | done | e0e5543 | 2964 of 1891 | 1873 of 1891 | 1859 of 1891 | 448 of 1891 | not scored | 3, 2, 4, 2 | yes | 13 | 13 |
| S2 | as S1, footprint drawing removed | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 1859 of 1891 | 0 of 1891 | not scored | 3, 2, 4, 2 | yes | 13 | 13 |
| S3 | as S1, zone and neighbourhood words changed to Phase and Block | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 1859 of 1891 | 448 of 1891 | 0 of 2 | 3, 2, 4, 2 | yes | 11 | not found |
| S4 | as S1, no LandXML and no Civil 3D surfaces, 2D CAD only | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 1859 of 1891 | 448 of 1891 | not scored | 3, 1, 4, 3 | yes | 13 | 13 |
| S5 | as S1, plot numbers in another style | done | 773c095 | 2964 of 1891 | 1873 of 1891 | 1859 of 1891 | 448 of 1891 | not scored | 3, 2, 4, 2 | yes | 13 | 13 |

Files not read: S1 0, S2 0, S3 0, S4 0, S5 0.

## Mark of each need with a confirmed profile

| Need | S1 | S2 | S3 | S4 | S5 |
|---|---|---|---|---|---|
| plotOutlines | strong | strong | strong | strong | strong |
| plotNumbers | strong | strong | strong | strong | strong |
| groupLevels | usable | usable | usable | usable | usable |
| villaType | weak | weak | weak | weak | weak |
| villaBlocks | weak | weak | weak | weak | weak |
| roadEdges | weak | weak | weak | weak | weak |
| roadLevels | usable | usable | usable | missing | usable |
| coordinates | weak | weak | weak | weak | weak |
| prototypes | strong | strong | strong | strong | strong |
| rules | missing | missing | missing | missing | missing |
| sharedParameters | missing | missing | missing | missing | missing |

## Build plan with a confirmed profile, what was built and what was missing

| Scenario | Plots planned | Built | Missing | Plots marked | Blocking | Issues | Copied from the past | Seconds |
|---|---|---|---|---|---|---|---|---|
| S1 | 2964 | 12738 | 22830 | 2964 | 0 | 3 | 0 | 1.7 |
| S2 | no build made | | | | | | | |
| S3 | no build made | | | | | | | |
| S4 | no build made | | | | | | | |
| S5 | no build made | | | | | | | |
