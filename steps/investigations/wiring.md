# Wiring between the drawings, round DATA-2 Part W, 2026-10-24

Counts only. The full table with every method is wiring.md and wiring.json in Documents\SiteAdaptation\data_book. The 15 drawings and the workbook, and the 12 coordinate PDFs copied into data_book\work for this part, hash the same before and after.

## Inputs

- Tables: reader C3DProbe for Road Layout, Road Surface and Rough Grading, CadReader stand-in for the other 11 drawings. Geometry (block outlines, curb, ROW, setback, frames) from the round 12 CadReader output, and the R06 outlines from a CadReader run on its copy this part (22 reader notes).
- PDF reader on the 12 coordinate PDF copies: 3,346 rows for 746 plots (IMC 337, IMD 409).
- Answer key: the 747 schedule plots, and 336 of the 337 plots of plan_imc_past.json that have a generic floor (one has none). All 336 generic floors hold their face by at least 0.5.

## Score per source

Score is wired, agree, ambiguous, wrong, unwired over the key plots. With no key, the count is plots with one hit, ambiguous and unwired over the 2,956 Phase 1 plots.

| Source | Methods tried | Score of each | Chosen | Still unwired or ambiguous |
|---|---|---|---|---|
| Schedules (747) | labelShift, labelShiftPdf | 747, 336, 0, 0, 0 (411 IMD with no key). 734, 327, 0, 0, 13 | labelShift (R166) | 0. The PDF check fails on 13 (IMC 9, 6 of them cut plots, IMD 4, one with no PDF points), no edge plot one label off |
| Footprint blocks (1,865) | centreInside, centreWidened, insertInside, bestShare, everyShare, nearest, typeNear | 226, 226, 0, 0, 110. 304, 304, 0, 0, 32. 27, 10, 12, 5, 309. 304, 304, 0, 0, 32. 336, 336, 0, 0, 0. 304, 304, 0, 0, 32. 226, 226, 0, 0, 110 | everyShare (was centreWidened) | 378 Phase 1 plots, all of them non-unit plots, 0 ambiguous |
| R06 blocks (1,868) | the same 7 | as the footprint blocks | everyShare (was centreInside) | 375, 2 ambiguous (mixed use) |
| R06_0000 blocks (248, VL2 only) | the same 7 | 35, 35, 0, 0, 301 on all but insertInside | centreInside (R166, tie) | 2,724 (the drawing holds VL2 only) |
| R06 numbers | none | 0 number texts inside Phase 1 | none | all |
| V9 numbers (2,780) | pointInside, nearest, labelNear | one 2,634, ambiguous 7, unwired 315 for the first two. labelNear 2 | pointInside (R166) | no key, V9 is reference only |
| Curb | frontFrame, anyEdge, faceNearest | 336, 308, 5, 23, 0. 336, 312, 0, 24, 0. 336, 275, 0, 61, 0 | anyEdge (was frontFrame) | 0, frontFrame left 275 Phase 1 plots with no front and 109 tied |
| ROW | the same 3 | 336, 331, 5, 0, 0. 336, 336, 0, 0, 0. 336, 336, 0, 0, 0 | anyEdge (was frontFrame) | 0 |
| Setback line (2,699) | intersects, bestShare, centroidInside, nearest | 336, 334, 0, 0, 0 for all (2 no key). Wrong owner on Phase 1: 26, 0, 0, 0 | bestShare (was intersects) | 378 non-unit plots, 1 ambiguous |
| Cutsheet frames (44) | centroidIn, bestShare, nearestFrame | one 2,956 for all | centroidIn (R166, tie) | 0, no key |
| Location plan (3,181 labels, Part L) | pointInside, nearest | 747, 746, 1, 0, 0. 747, 745, 2, 0, 0 | pointInside | 30 Phase 1 plots unwired, 29 with two labels |
| Road surfaces (672 ramp front corners) | snapOrder, plainOrder, snapAll, nearestVertex | 672, 522, 0, 150, 0. 390, 301, 0, 89, 282. 672, 522, 0, 150, 0. 0 wired | snapOrder (R166) | 150 corners off by more than 5 mm |

The pick takes the most agree, then the fewest wrong plus wrong owner, the fewest ambiguous and the most wired. On a tie the R166 method stays.

## Weak spots

- Several blocks on one plot: 0 plots under the widened join and under everyShare.
- The 0.5 m widening: 548 widened hits, 0 of them take a block the plot holds under 0.1 of. It is the only way the widened join reaches the second plot of a pair, and 246 plots (the DP ends of the townhouse row blocks) stayed unwired by it.
- Setback line of the neighbour: the R166 join took a line that another face owns on 26 Phase 1 plots, 9 of them with a setback value in plots.csv (all OS). 1,795 plots touch more than one setback line.
- Many labels: 17 faces hold 56 labels in Phase 1. 9 are rescued by the footprint blocks, 47 stay listed (43 wadi or natural, 2 DP4, 1 MFR, 1 OS).
- Labels outside every face in Phase 1: 16 (14 ROW, 2 buffer), none rescued, no unit label among them.

## Drawing to drawing

- Footprint against R06: 1,865 blocks matched one to one by outline overlap, all with the same name, 3 R06 blocks with no footprint block.
- R06 against R06_0000: 248 matched with the same name, the 1,620 other R06 blocks have no R06_0000 block (R06_0000 holds VL2 only).
- Curb of Roads Road Layout against Base Plans Road Layout, a point every 1 m inside Phase 1: 77,276 within 1 cm, 208 within 10 cm, 1,515 within 1 m, 13,335 worse. The other way 76,226, 176, 983, 14,859. About 15 percent of each curb has no partner within 1 m.

## What the new wiring changes

| Finding | Before | After |
|---|---|---|
| plots.csv columns | 98 | 110 (12 method columns) |
| Unit plots with a footprint block | 2,332 | 2,578 (the 246 DP ends of townhouse rows) |
| Unit plots with an R06 block | 1,774 | 2,578 (all Phase 1 plots 1,892 to 2,696) |
| Plots with a curb distance | 2,681 | 2,956 (the rest 88 to 100 percent) |
| Curb distance changed | | 117 plots, ROW 101, sidewalk 105, from the anyEdge front |
| Setback on unit plots, 5.00 m, 3.00 m, other, none | 2,530, 37, 7, 4 | the same, no value moved by more than 1 cm |
| Setback values removed | | 9 OS plots that held a neighbour's line |
| Width against Rev 6 | C10 58, VL3 41 wider | the same, the width keeps the R122 front |
| El @ Entry by typology, within 5 mm, 30 mm, worse | 221, 193, 330 | the same, the road level keeps snapOrder |
| Units | 2,698 | 2,698, R171 already used every share |
| Type check of the footprint block | 2,332 same, 246 no block | 2,332 same, 246 under a townhouse row block (TH1 122, TH3 124), which is the row block and no type conflict |

## Second method (R178)

wiring_check.py with its own ray cast and a point grid, seeds 14 and 7, 20 unit plots and 20 schedule plots each: footprint block, R06 block, setback owner, curb, ROW, cutsheet, V9 and label plus shift 20 of 20 at both seeds. R173 on the rebuilt plots.csv gives the same counts as before, R168 20 of 20 with the share wired blocks left to R178.
