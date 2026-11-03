# Speed of a Revit run, SPEED-2NOV-C

Where a run of buttons 1 to 8 on Neighbourhood 012 spends its time (R447), before and after the speed work. Seconds and counts only, no real values.

## Before, run SPB2, 2 Nov 2026 20:33

RAN by Run-TestHost under the lock of R200, on b12e336 (main at 2cf029f plus R447, R448, R449, not yet used). A new model from the template, Villa mode Masses, Prototypes prototypes_2024, no cached base shapes. 0 mass files to make is UNKNOWN from the logs, the 13 files were last made from that folder.

| Button | Total | Revit start | Model open | Reading | Transactions | Model refresh | Warnings | Saving | Worker wait | Other |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 Set up site | 144.2 | 131.2 | 11.9 | 0.2 | 0.7 | 0.0 | 0.0 | absent | absent | 0.3 |
| 2 Read data | 37.2 | absent | absent | 0.7 | absent | absent | absent | absent | 35.6 | 1.0 |
| 3 Review data | 0.2 | absent | absent | 0.2 | absent | absent | absent | absent | absent | 0.0 |
| 4 Check | 20.8 | absent | absent | 3.0 | absent | absent | absent | absent | 15.0 | 2.8 |
| 5 Floors and ramps | 17.3 | absent | absent | 0.2 | 3.6 | 9.7 | 0.0 | absent | absent | 3.9 |
| 6 Levels | 3.8 | absent | absent | 0.7 | 0.0 | 0.0 | 0.0 | absent | 1.9 | 1.2 |
| 7 Place villas | 77.1 | absent | absent | 2.5 | 2.8 | 0.0 | 0.0 | 3.0 | absent | 68.8 |
| 8 Walls | 3.4 | absent | absent | 0.5 | 1.4 | 0.7 | 0.0 | absent | absent | 0.8 |
| Sum | 304.3 | 131.2 | 11.9 | 8.0 | 8.5 | 10.4 | 0.0 | 3.0 | 52.5 | 78.9 |

The save after each committed button stands apart, since the timing file is written first: button 1 0.6, 5 1.2, 7 1.0, 8 1.1 s, in buttons_result.json. Whole run 6.1 minutes by the run log.

Button 7 by step: outside the transaction 60.4 s, of which 3.0 s saving and 57.4 s not yet split, apply 11.4 s, commit 2.8 s.

## What the table says

- Revit start is 43 percent of the timed run, so the listener removes the largest single item.
- Button 7 is the largest button, and most of it is in Other, the 60 s outside its transaction. R447 does not split that yet, so it is not known what to fix there.
- The worker waits, 52.5 s, are the CAD reader and not Revit. Nothing of Task 4 touches them.
- Model refresh is 10.4 s, 9.7 of it in button 5, one candidate of Task 4.
- Warnings cost 0.0 s of machine time. The handler of FIX-2NOV-B saves a person a box, not machine time, on a test host run that answers its own boxes.

## Limits

- One run. CPU averaged 51 percent, Lane A worked on the machine (RAN, cpu_free_samples_before2.csv), so small differences mean little. Peak private 2,905 MiB, least free 27.46 GiB.
- Revit start here includes the first tick, the security boxes the watcher answers and machine load. A run before it, also loaded, read 151 s.
- The run ended with exit 1 though every button was ok: Revit wrote a .slog in 6 _backup folders of prototypes_2024 while button 7 ran, so the input check counted 276, unchanged 270. All 6 lie in that work copy, no file of an input root changed. Named files in the run log of SPB2.
- A first run, SPB1, named the Revit 2021 folder as Prototypes and timed out at button 7's limit of 20 minutes. Lane B measured about 41 minutes for that remake. Buttons 1 to 6 of SPB1 agree with this table within load, button 7 of SPB1 is no measure.

## New before runs, 3 Nov 2026

RAN by Run-TestHost under the lock, on 508ac69 (main at 5a1bd1f plus R447 to R449, R496), same spec as SPB2 with the split of button 7. SPB3 03:20, SPB4 03:27. Whole run 6.8 minutes by the folder times of SPB3 and SPB4.

In both, no mass file was stale, so the step villaMassFiles did not run. SPB2's 57.4 s are gone with it, button 7 is 19.3 and 26.0 s against 77.1 s. Same code and same spec differ by 31 percent in the sum between the two runs, so a gain under that is not to be read from one run.

## The 57 s of button 7, named

RAN, job SPMM, a private copy of the mass folder with 6 prototypes removed, in the listener:

| Step of button 7 | Seconds |
|---|---|
| villaDecide | 1.25 |
| villaMassFiles, 6 made | 52.33 |
| villaLoadTypes | not reached, nothing to load |
| apply | 10.12 |
| commit | 2.46 |

So the 57 s was the make of 6 stale mass files, 8.7 s each, once per change of a prototype or a reader version, and not a cost of each run. The largest part is in RevitMassFileMaker, which MASS-2NOV-D of Lane D changes, so nothing here touches it. It also proved that those runs ended as changed inputs only for the 6 .slog Revit writes, fixed by R497.

## After, through the listener

RAN, jobs SPM1 and SPM2 of one Revit, on 84773aa, same spec, same config, each a new model copy. Revit start is absent from every timing file of both.

| Button | SPB3 own run | SPB4 own run | SPM1 first job | SPM2 second job |
|---|---|---|---|---|
| 1 Set up site | 132.5 | 174.8 | 19.8 | 5.9 |
| 2 Read data | 36.9 | 44.0 | 49.0 | 47.7 |
| 3 Review data | 0.2 | 0.2 | 0.3 | 0.2 |
| 4 Check | 19.4 | 27.3 | 37.3 | 18.4 |
| 5 Floors and ramps | 17.3 | 23.7 | 38.0 | 16.3 |
| 6 Levels | 3.8 | 5.5 | 4.8 | 3.3 |
| 7 Place villas | 19.3 | 26.0 | 25.9 | 15.6 |
| 8 Walls | 3.3 | 4.0 | 9.1 | 2.9 |
| Sum | 232.6 | 305.5 | 184.2 | 110.2 |

| Category | SPB3 | SPB4 | SPM1 | SPM2 |
|---|---|---|---|---|
| Revit start | 116.7 | 154.2 | 0 | 0 |
| Model open | 14.6 | 19.0 | 18.2 | 5.3 |
| Reading | 8.0 | 10.9 | 11.9 | 5.8 |
| Transactions | 8.1 | 12.6 | 15.7 | 7.5 |
| Model refresh | 10.9 | 13.6 | 24.2 | 10.2 |
| Warnings | 0.1 | 0.1 | 0.2 | 0.1 |
| Worker wait | 51.1 | 66.3 | 77.6 | 61.2 |
| Other | 23.1 | 28.7 | 36.4 | 20.0 |

Whole job by the send: SPM1 211.7 s, SPM2 121.2 s, against 6.8 minutes for an own run. The start of the listener itself took about 2 minutes, once.

The first job is slower than the second, 184.2 against 110.2. Cause UNKNOWN: the first use of the code in that Revit, or load on the machine, was not separated.

## The model result

RAN against SPB3, for SPB4, SPM1 and SPM2 alike:

- floors_012, place_012, walls_012, footprint_012: 0 differences. 336 links placed in each.
- levels_6: only the 1,003 uniqueId values, which are per model. floors_012_accepted: only the 1,376 element ids of the 688 pairs, same.
- plan_012: the plan body has 0 differences. The run block differs in the build stamp of the code (SPM only) and the SHA-256 of 5 read files in the run folder, the same 5 between the two own runs.
- Second method, the 012_Levels workbook read cell by cell: 8,298 of 8,302 equal. The 4 others are the run id, run time, plan build id and plan SHA-256 of the Settings sheet.

Real inputs: 276, changed 0 on SPM1, SPM2, SPM4.

## Task 4, what the numbers say

| Listed fix | Measured | Done |
|---|---|---|
| Revit start | 116.7 to 154.2 s, the largest item | The listener |
| Button 7 outside its transaction | 52.3 s for 6 stale masses, 0 s else | Named. The file is Lane D's, nothing changed. R497 for the false changed inputs |
| Warnings | 0.1 s | No change. Lane B's handler quiets a box and costs no machine time, all accepted warnings return by the first save (RAN by Lane B) |
| One model refresh per step | 10 s in button 5, 2 calls | No change. The first gives the ramps their vertices, the second is what the read back reads. Dropping either changes the result or moves the same time |
| One transaction group per step | Commit 2.5 to 5.9 s | No change, each step already commits once |
| No auto join | Button 8 whole 2.9 to 4.0 s | No change, too small to read over the 31 percent of load |
| Links unloaded | Only button 8 runs after the links, 2.9 to 4.0 s | No change, same |
| Work copies outside OneDrive | The work root is outside it (READ) | Nothing moved, C:\SA-revit-jobs added to KEEP |

## Limits

- Worker wait is the slowest part left, 51 to 78 s, the CAD reader outside Revit. Nothing here touches it.
- The windows the watcher saw in the jobs are the expected ones. After 10 minutes the watcher stops, so later windows are UNKNOWN.
