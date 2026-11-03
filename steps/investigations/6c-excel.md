# 6C-3NOV-C, the Excel compare of NBH 012

The Level Excel and the Report Excel of NBH 012 against the past job Excel of the same neighbourhood. Counts, plots and gaps only, no level value (Real files of CLAUDE.md).

## What was compared

- Tool (RAN): 012_Levels_parsons2024Rev6.xlsx and 012_Report.xlsx of a real run through the Revit pane, buttons 1 to 9, 3 Nov 2026, on check/6c-3nov-c at 6380769, which is main 708e0fd plus the window changes of this round. No build logic changed on that branch. Config of the branch with the demo input paths, level profile parsons2024Rev6, scope NBH 012. Files outside git in Documents\SiteAdaptation\SA-part2_56ba52e4.
- Past job (READ): 00101-PSN-IMC-XXX-SCH-TU-090000.xlsx in 01_Output\1.11 References, sheet MP_Phase 1_Zone21, header on row 39. NBH 012 is IMC by config/area_codes.json. Read on a copy, SHA-256 the same before and after (RAN).
- SEARCHED: it is the only past Excel found for this neighbourhood, by file name under C:\02-Site Adaptation and C:\10- ROSHEN MECCA, depth 6. The other copy under C:\10- ROSHEN MECCA carries the same name, not compared byte for byte.
- No past Report Excel was found, so the Report has no past file to compare against. Its Placed sheet is compared with the past Unit column instead, below.

## Sheets, columns and order

| Item | Past job | Level Excel |
|---|---|---|
| Sheets | MP_Phase 1_Zone21 | Levels, Clusters, Overrides used, Settings |
| Columns in order | Plot & Unit, Plot No., Unit, FFL, SSL, El @ Entry, Height, Site Steps Needed, Steps Needed | Plot No, Unit, Cluster, Road at front, El @ Entry, FFL, SSL, Height, Steps, Source |
| Only here | Plot & Unit, Site Steps Needed | Cluster, Road at front, Source |
| Same data, other name | Plot No., Steps Needed | Plot No, Steps |
| Order | FFL, SSL, El @ Entry, Height | El @ Entry, FFL, SSL, Height |
| Steps text | 1 steps, 2 step, 3 steps | 1, 2, 3 |

The Report Excel has Summary, Placed, Skipped, Overrides, Assumed, Issues, Accepted warnings, Rules and Settings. None of them has a past twin.

## Values per plot

337 plots on both sides, none on one side only. A level counts as equal within 0.5 mm, the levelPassMm of config/project.json.

| Field | Equal | Differ | Missing | Median gap of those that differ | Largest gap |
|---|---|---|---|---|---|
| Unit | 332 | 4 | 1 | | |
| FFL | 324 | 12 | 1 | 21.5 mm | 187.0 mm |
| SSL | 324 | 12 | 1 | 21.5 mm | 187.0 mm |
| El @ Entry | 323 | 13 | 1 | 13.1 mm | 53.4 mm |
| Height | 330 | 6 | 1 | 61.1 mm | 169.3 mm |
| Steps | 334 | 2 | 1 | | |

- Second method (RAN): the counts of every row above from the raw sheet XML with no spreadsheet library, against openpyxl for the first. Both give the same counts. The first script parsed the Steps text wrong on its first run and was fixed before this count.
- Missing: plot 2312 on all six. The past job has values, the Level Excel row of 2312 is blank. Round 17 already records that 2312 has no row in plots.csv (READ steps/investigations/round-17.md:110).
- Placed of the Report against the past Unit: 334 equal, 2 differ (2301, 2302), 1 missing (2312). 1,339 Placed rows: link 336, generic 336, ramp 336, yard 331.

## The worst 10

Tool minus past, in mm.

| Plot | Field | Gap | Source column of the tool |
|---|---|---|---|
| 2388 | FFL | +187.0 | Rule |
| 2388 | SSL | +187.0 | Rule |
| 2377 | FFL | +175.0 | Rule |
| 2377 | SSL | +175.0 | Rule |
| 2377 | Height | +169.3 | Rule |
| 2388 | Height | +153.5 | Rule |
| 2375 | Height | +66.4 | Rule |
| 2390 | Height | +55.9 | Rule |
| 2375 | El @ Entry | -53.4 | Rule |
| 2389 | FFL | +47.0 | Rule |

2390 FFL and SSL and 2389 SSL tie at 47.0 with the last row. The whole list of FFL: 2388, 2377, 2389, 2390, 2544, 2545, 2387, 2375, 2376, 2378, 2441, 2311.

## What the differences are

- The 12 FFL rows all read Rule in the Source column, none holds an override. 8 of them lie in four two-unit clusters: 2375-2376, 2377-2378, 2387-2388, 2389-2390. The other 4 are 2544, 2545, 2441, 2311, the last two within 2 mm.
- Steps: 2377 and 2388 differ by one step, the same two plots with the largest FFL gaps.
- Unit: 2301 and 2302 are VD4 in the tool and DP4 in the past job. 2513 and 2514 are TH1-IM in the tool and TH1 in the past job, the same unit under a longer name in the tool, so UNKNOWN whether a person counts those as a difference.
- UNKNOWN: whether the schedule or the IMC key model is the right past value on these plots. The majority rule of CLAUDE.md puts a plot the rule does not reproduce into the override list from the key models, and these 12 hold none. The schedule against the key was not compared in this round, past_all.csv is not in this worktree or the main folder's sample_output (SEARCHED by file name).
- UNKNOWN: why 2301 and 2302 read VD4. Not traced in this round.

Q401 and Q402 of steps/questions-open.md carry these for Bader.

## Scripts

Outside git, Documents\SiteAdaptation\check_6c: compare_excel.py the first method, compare2.py the second, analyse.py the Source column and Placed. Each prints counts, plots and gaps only.
