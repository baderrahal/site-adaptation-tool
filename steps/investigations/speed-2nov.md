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

## After

Not run yet.
