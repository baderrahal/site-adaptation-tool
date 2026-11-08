# revit-test heads-up, FAST-3-revit-test

8 Nov 2026, 14:30. The demo run on demo-S1 is done, Revit closed at 14:23:08, lock free. Task status stays open for the Phase 3 run.

## Final statement

- The demo model of part 2 is built and saved: FAST3-demo-01A.rvt of fast3-revit-test, 15,785,984 bytes, write time 14:04:26. It is not to be changed.
- On main 57cde47, Lead's install of 10:09. Always Load pressed once, 10:21:54, the box did not come on the later starts.
- Pass of the order. The Build 4.2 min, target 20, met. The reopen 70 s with no box, target 5 min, met. Card 1 by Lead's count, Set up site to Scope set, 16.9 min, target 15, NOT met. The tool's own work in it is about 8 min, the rest is the driver's pace and about 4 min of its mistakes in the Scope row.
- Times: the one read 454 s, Confirm 27 s in the worker with no second read, the reuse Apply 1.1 s and its box 3.1 s. Steps 2 to 8: 4.1, 7.7, 20.5, 62.3, 8.8, 95.9, 6.5 s.
- Placed: 526 floors and ramps on 263 plots (263 generic, 263 ramp), 1 road toposolid, 526 levels at the plan, 263 villa links of 13 files all from Prototypes_2024, 582 walls. Plots built 263 of 278, 15 left out, the same as run 2.
- FAST-13, FAST-14 and FAST-15 seen in Revit, detail in the record. F32, F33 and F34 fixed.
- The run needed two presses the order does not list: F35, Advanced row Prototypes Browse, or the Build stops at Check, and F36, one more Choose project folder and Apply after Confirm, or 3 Review data stays grey. The demo needs both.
- New faults in steps/seam.md: F35 to F39. F35's cause is UNKNOWN.
- demo-S1 unchanged: 50 files, 0 changed, 0 gone, 0 new.
- Not seen in Revit: the sentence of R811 on the box of step 7 (F38), File, Exit (the driver's window close closed Revit, as a person was on the laptop), the counts of the model by 9 Report (not run, it would change the demo model).
- Slips of this session, each in the record: one shot at 11:27:30 caught a person's Explorer window and was deleted, two clicks landed harmlessly on a folder box, and one hour, 12:08 to 13:08, was lost to a wait of mine that needed a box that never came.

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the demo run section.
- steps/seam.md: the demo run line, F35 to F39.
- steps/log.md: the top entry.
- No code. drive3.ps1 unchanged, the helpers of this run in the session scratch folder.

## Next

- The Phase 3 run, Lead's order of 14:24, under the driver rule of 5 minutes idle.
