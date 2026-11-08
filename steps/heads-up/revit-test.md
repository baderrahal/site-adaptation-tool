# revit-test heads-up, FAST-3-revit-test

8 Nov 2026, 06:45. Run 2 done and closed. Revit closed by the driver at 06:27:10 after the save of 06:25:53, no Revit process, no lock. Task status stays open for the 010 run, which Lead starts.

## Final statement

- Run 2 on NBH 01A (tuned) from a fresh empty copy, FAST3-run2-01A.rvt, on the add-in of main 1b4f287 Lead installed at 03:18. Part 1 03:32 to 05:23 (cards 1 and 2), part 2 05:30 to 06:27 (card 3). Always Load pressed at 03:34:12 and 05:31:24.
- Result: PASS of Lead's pass. The 3 cards ran to their end. Card 1 55 minutes, card 2 45 minutes, card 3 21 minutes (Report 23 s).
- The road pick of F8: Set up offers Pick surfaces on the row Road levels once its folder is chosen. Picked Compiled West_MM and Part 4 Top Surfaces, in that order, at 03:47. The plan took road levels from both, 378 and 154 front corners.
- Presses of 2 Read data: 734 s, 736 s, then a reuse of about 4 s inside the Build. F22 stands, Confirm says the confirmed values count from the next read.
- Counts placed: floors and ramps 526 on 263 plots (263 generic, 263 ramp, 0 yard), road toposolid 1 (Compiled West_MM), levels 526 at the plan, 0 moved, villa links 263 of 13 prototype files, walls 582. Plots built: 263 of 278, 15 left out (no floor level 11, no outline or front 2, no unit 2), named on card 2. Read three ways, the step boxes, the step files, and the counts of the model by 9 Report. They agree.
- FAST-11: F29 and F30 fixed and seen in Revit, card 3 says 13 plots have no road level, the count of the plan of 01A.
- FAST-12: no stop on the upgrade box in step 7 (R805), the box of step 7 says "link types new to the model 13, of an older Revit 13" (R807). Step 7 shows 14 issues, the words not seen (R806 UNKNOWN). Step 7 took 28 minutes.
- New faults in steps/seam.md: F32 the walls overlap box of Revit stops step 8, pressed OK at 05:19:28 from outside the driver by BM_CLICK. F33 the reopen of the built copy stops 13 times on the upgrade box, closed by the driver each time, 27.5 minutes. F34 card 3 says 2619 issues, the count of the whole read, card 2 says 881.
- Part 1 ran to the 110 minute limit of the driver: step 7 was 28 minutes, the copy was saved at 05:20:50 before the driver closed Revit at 05:23:23.
- S1 unchanged: 37 files, SHA-256, size and write time equal before and after.
- Card shots in git, cut to the pane: steps/ui-shots/pane-stage-1.png to pane-stage-3.png, the coordinate line of card 2 blanked. The full shots stay in fast3-revit-test/shots/run2 and run2p2 of the work root.

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the run 2 section, parts 1 and 2, step 7 for FAST-12, times, result.
- steps/seam.md: the run 2 line, F32 to F34, the R806 line for Lead.
- steps/demo-live.md: parts 2 to 4 from the rehearsal, the road pick, the folder flow, the walls box, no reopen in the room.
- steps/ui-shots/pane-stage-1.png to pane-stage-3.png, steps/log.md.
- No code. drive3.ps1 unchanged, the helpers of this run in the session scratch folder.

## Next

- Lead starts the 010 run of 12:00 after Bader names the new project, or at 10:00.
- The copy FAST3-run2-01A.rvt holds the built 01A. Opening it again costs 27 minutes of upgrade boxes, F33.
