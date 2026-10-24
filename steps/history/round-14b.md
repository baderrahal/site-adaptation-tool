# Round 14 Part B, 2026-10-24, clusters and levels for the computed plan

Full record of Part B. The top entry of steps/log.md holds the short form.

## Start

- Branch feat/cad-levels-14b from main at 497855c, after PR 20 was merged with the python and the dotnet check both passed. The first commit is PROGRESS.md after that merge.

## Profile

- ramp.front_setback (default 5520, C10 5500) and steps.one_step_only (C10, VL4, VL6) left the rev6 block, since a rule now reads them. The R62 test that pins rev6 says so. No value sits in a .py file.

## B1 Check 3 (R125)

- cluster_split_positions [2, 3] read as walls after unit n counted from either end. With four units that allows every inner wall: after 1 and after 3 (DP to TH, one case) and after 2 (middle TH to TH). A pair never splits.
- Schedule FFL of IMC on the 96 IMC clusters of analysis.json: one FFL 85, split allowed 11, not allowed 0. Walls: M07 after 2 7, M07 after 3 1, M08 after 2 3.
- Computed FFL: one FFL 96, levels.py gives one FFL per cluster.

## B2 Levels (R126)

- 337 schedule rows of IMC, 337 entries, none lacking. levels.py on the official clusters with the raw El @ Entry.
- Flags: height over 0.54 m on 2464, 2471, 2475, 2483. No C10, VL4 or VL6 with more than 1 step, the past schedule has 1 step on all 49 of them.

## B3 Ramp outline (R127)

- 337 past ramps, all with a front edge. Outside the A1 list 325: every corner, the front, the back and the depth within 10 mm on 321, worse 4: 2374 and 2391 (VD1, corners 2.0 m off, the past ramp is 2 m narrower than the face), 2543 and 2546 (VD4, 4.0 m off, 4 m narrower). All four are next to A1 plots. All 15 C10 fit at 5.50 m. On the A1 list 12, corners worse 12, depth within 10 mm 8.
- Top Surface-02: 414 of 1348 corners covered where they are, 1346 of 1348 at the curb point. 119 ramps with no corner covered, 0 with no curb point covered, all 119 have a covered curb point.

## B4 Plan from CAD and the compare (R128, R129)

- plan_imc_cad.json next to plan_imc_past.json: 224 of 337 plots written. 113 lack the mirror flag, 107 villas and 6 A1 plots in no cluster. Prototype file by unit, else by keo_file, VD1 takes the DP1 file.
- Against plan_imc_past.json, 218 plots outside A1:
  - rotation equal 218, mirrored equal 218, prototypeFile equal 218, unit equal 216 (2301, 2302)
  - insert in plan equal 73, within 1 mm 109, within 10 mm 217, worse 1 (2338, 10.3 mm between the two plans, both rounded to the mm)
  - FFL and insert Z equal 163, within 1 mm 165, within 10 mm 193, worse 25: 2460 2461 2478 2479 2484 2485 2488 2489 2492 2493 2496 2497 2503 2506 2507 2510 2511 2514 2515 (the split clusters) and 2556 2557 2560 to 2563
  - SSL and height equal 163, within 10 mm 193, worse 25. entry equal 218. steps equal 196, worse 22
- On the A1 list 6 plots compared: rotation, mirror, file, unit, steps equal 6, insert in plan worse 6, FFL equal 4, within 10 mm 2.

## Second method (R130)

- levels: seed 14 20 of 20, seed 7 20 of 20 (3 on a half millimetre), all 224 of 224 (24 on a half millimetre). The first run had 14 plots 1 mm off. The schedule holds El @ Entry with up to 12 decimals and levels.py uses them, the past plan holds 4 (R2), so an FFL on a half millimetre was decided by decimals the check did not have. On all 14 the plan FFL equals the schedule FFL. The check now accepts either millimetre on a half millimetre and lists the plot.
- ramps: seed 14 20 of 20, seed 7 20 of 20, all 337 of 337, corners within 1 mm and the same cover.
- compare: 4 of 4 fields give the same counts.

## Gate

- Python 694 to 708 passed, 1 skipped. .NET 347 passed.
