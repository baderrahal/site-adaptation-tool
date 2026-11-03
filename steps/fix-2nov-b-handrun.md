# FIX-2NOV-B task 4, the hand run of main on NBH 012

Run 3 Nov 00:42 to 03:01, on main a4551dd, through the real windows from the pane, not the test host. New model handrun\SA-fix2nov.rvt from a named copy of SA-demo-start.rvt. Files and shots in Documents\SiteAdaptation\lane_b_check\handrun, outside git.

## What Bader must know

- No number is worse than PR 139. The handler works in Revit: 688 overlaps are taken without showing, Q158's dead end is gone.
- The Revit box still stops step 5, with 485 warnings where it held 1,173. All 443 overlaps left are of two plots, as Q367 asks, plus 42 off axis (Q356).
- FAIL, R459: the 688 accepted warnings come back in Review Warnings by the first save at the latest. Only the box is quieter, the floors still overlap. Q324 stays open, Q385.
- FAIL, step rows: all nine done and saved, then a reopen shows step 1 Done, step 2 Ready, 3 to 9 waiting. Only button 1 records its run in the model. A person must run 2 to 9 again, Q384.
- Ctrl+Z of a real press of button 1 leaves step 1 Done and site.json off the model, by 821 m and 110 m in this run, until the next press (Q382).

## Per button

Time is from the press to the end, by the driver.

| Button | Result | Time | Warnings shown | Accepted and counted |
|---|---|---|---|---|
| 1 Set up site | Done, window says 11 parameters, 12 left out | about 8 s | none | none |
| 2 Read data | 18 files, 2,964 plots, 386 conflicts, picks, 2 issues | about 155 s | none | none |
| 3 Review data | Confirm all 379 of 379, Save review, box back to 2 after the 012 pick (Q306) | 22 s, then 25 s and 19 s | none | none |
| 4 Check | 337 plots, 2,347 rows, 1 follows every rule | 55 s | none | none |
| 5 Floors and ramps | 1,003 placed, 0 refused | 22 s to the box | 485: 443 overlap, 42 off axis | 688 |
| 6 Levels | move 0, at the plan 1,003, listed 1 | 11 s | none | none |
| 7 Place villas | 336 placed, 0 mass files to make | 21 s | none | none |
| 8 Walls | 778 placed, listed 779 | 7 s | none | none |
| 9 Report | 012_Report.xlsx, Run complete | 6 s | none | none |

## Against PR 139

| Item | PR 139 | This run | How read |
|---|---|---|---|
| Floors placed, refused | 1,003, 0 | 1,003, 0 | RAN floors_012.json |
| Revit overlap warnings | 1,131 | 443 shown plus 688 accepted, 1,131 | RAN warnings_5.html, floors_012_accepted.json |
| Off axis | 42 | 42 shown | RAN warnings_5.html |
| Yard past plot, plan | 0.87 m2 | 0.87 m2, corrected to 0.90 m2 below | RAN yards.py on plan_012 |
| Yard past plot, model | none | UNKNOWN, no window or tool file holds placed outlines | READ floors_012.json |

Correction, FIX-3NOV-B: yards.py took the first loop of a sketch as the outer one, and 47 yards have two loops apart. Read as one sketch the plan gives 0.90 m2 (steps/investigations/two-plot-overlaps.md). This is no worsening: the old reading gives 0.865 on every plan with these yards, whether PR 139's plan is of that kind is UNKNOWN.

Bader asked for a read back from the model. The yard area is not one: PR 139's 0.87 m2 is a plan figure too, so the plan compare is like for like and the model read stays open. Floors refused and the overlap count are read from the model's own run.

Overlap of two plots still shown, by floor pair: Generic with yard 204, Generic with Ramps 0.5% 115, Ramps 0.5% with yard 58, yard with yard 27, Generic with Ramps 2% 13, Ramps 0.5% with Ramps 0.5% 10, Flat with Generic 6, Ramps 2% with yard 4, Generic with Generic 3, Flat with yard 2, Ramps 0.5% with Ramps 2% 1. All 443 two plots by joining the ids with Placed, 0 one plot (RAN).

Accepted, by floor pair: Generic with yard 331, Generic with Ramps 0.5% 153, Ramps 0.5% with yard 128, Generic with Ramps 2% 47, Ramps 2% with yard 13, Flat with Generic 9, Flat with yard 7. 688, 0 listing two plots (RAN by a second script).

## Checks of other rounds on main

| Check | Result | Numbers |
|---|---|---|
| Accepted, three places | PASS | Chip, sheet, file all 688 |
| Second press of step 5 | PASS | place 0, already 1,003, no box |
| Step 5 moving 2 floors | PASS | no box |
| Placed Element id, R483 | PASS | 1,339 rows, 1,339 distinct ids, 5 of 5 by Select by ID match kind and plot |
| Walls, R480 | PASS | 778, as Lane A's Python build |
| Footprint, R477 | line read | over the limit 0 of 59 same type, 269 another type, 8 no outline. Sheet part 16 said 57, 271 and 8 on an earlier main |
| Check filter, R495 | PASS | field rampJoint shows 1,110 of 2,347, column head sorts |
| Pane, R494 | PASS | back after close to Home and reopen, Show pane works |

## Q158, on handrun\q158\SA-q158.rvt

| Check | Result |
|---|---|
| Second press, nothing changed | PASS. No box, undo list unchanged, site.json time unchanged. The IsModified flag could not be read, UNKNOWN by flag |
| Config edit, then step 1 | PASS. New line fits, Apply on, step 1 Done, step 2 Ready, no box, undo list gains the step |
| Save, close, new Revit | PASS. Step 1 still Done, the old dead end gone |
| Stale site.json | PASS. A press after an undone move wrote the file, equal to the model at 0.000 |
| Ctrl+Z of a press | RECORD. Pane stays step 1 Done (Q382) |

## Not reached

- Whether a box comes when button 6 itself moves floors: it moved 0 in both runs. So whether step 6 needs a list is UNKNOWN, Lane C and a later run may meet it.
- Yard past plot read from the model.
- Before the first save, whether the accepted warnings are still gone: not read.

## Findings

- The chip Accepted at step 5, pressed, shows Showing 0 of 2,347 rows, an empty table. The count is right, the rows are on the report sheet (Lane B).
- Road Layout row now reads "the reader skipped 217 named colours of this drawing, the position does not use them", the sheet of part 5 is older (Lane C).
- Saved projects SA-fix2nov and SA-q158 stay under Start from, and the copies stay in Revit's Recent list.

## Faults of this run

- The Revit box at step 5 stayed open about 6 min while the driver was fixed, untouched. The same fault cost about 5 min on the Q158 copy.
- On a copy, not the main run, a press of Run 5 sat behind the 4 Check box and ran when it closed.
- The lock was held 2 h 19 min, over the 1 to 2 hours told to the lanes, about 12 min of it an extra build to time when warnings return.
- Earlier Always Load presses: one security box came, for Site Adaptation, Load Once pressed. None for the placer, reader or test host, why is UNKNOWN, the registry is not read.

## Second checks

- Inputs: same 44 files by SHA-256 at the start, after the main run and at the end (RAN manifest_check.sh).
- Warnings, file, sheet and Placed read by a second script apart from the helper's (RAN handrun_check.py): 443 and 42, 688, 688 rows, 1,339 distinct.
- Only one caller writes a run into the entity, SetUpRun (SEARCHED), which is the cause of the step rows.
