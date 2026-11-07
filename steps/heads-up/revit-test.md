# revit-test heads-up, FAST-3-revit-test

7 Nov 2026, 19:00. Short check done and closed. Revit closed by File, Exit at 18:49:25, exited 18:50:05, no Revit process, no lock. Task status stays open for run 2.

## Final statement

- Short check on NBH 01A (tuned) from a fresh empty copy, FAST3-short-01A.rvt, on the add-in of main 89d1c69 installed 14:59, two parts under 110 minutes, 15:15 to 17:06 and 17:07 to 18:50, the copy saved at the end of each. Always Load pressed at 15:16:17 and 17:08:16.
- Result: FAIL of the pass. The Build of card 2 passed Check for the first time (F16 is fixed) but loops on Check: each Check commits and opens its window, and on close the Build asks for Check again, 5 times, until a Cancel stopped it, "Build stopped at Check, cancelled." (F25). The Build placed nothing.
- Counts placed: floors and ramps 538 on 269 plots (269 generic, 269 ramp, 0 yard) by Run 5 of Advanced, not by the Build. Toposolid none. Levels 0, villa links 0, walls 0, steps 6 to 8 never ran.
- New faults F24 to F28 in steps/seam.md: F24 choosing the folder undoes step 1 (work around: Set up site, folder, Set up site, the same folder again), F25 the Check loop, F26 Check reads the data book of the past job, F27 no yard floors and no toposolid on 01A plus a Revit overlap box that waits for a person, F28 278, 297 and 269 plots for one area. F9 and F10 are fixed, F2, F21 and F22 seen again.
- From 15:56 to 17:04 nothing was sent: the keep awake Teams meeting showed its full view. Lead's message of 17:00, Bader's words "ignore the teams": drive3.ps1 and the gate scripts now leave out both titles of that one meeting, the old driver kept as drive3.before-1700.ps1.
- S1 unchanged: 37 files, SHA-256 equal before and after.
- No shot in git, they show real values. They stay in fast3-revit-test/shots/short of the work root, one per card: s01-start.png and s08-after-setup.png (card 1), s31-scope.png (card 2 before Build), s41-card-end.png (end).

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the short check section, parts 1 and 2, the result table.
- steps/seam.md: F24 to F28 and F2 seen again.
- steps/log.md: the short check entry.
- No code. Outside the repo: drive3.ps1 of the work root, the Teams exemption widened by Lead's message.

## Next

- Lead decides run 2. The copy FAST3-short-01A.rvt holds steps 1 to 5 on 01A, step 6 Levels Ready, so it can go on from step 6 in Advanced.
- Main now holds PR 264, R769 of build-3, the fix of F24, and PR 260, R768, merged after this check began. Not seen in Revit, run 2 needs Lead's new install.
- IT maintenance at 19:30: no Revit part starts after 19:00.
