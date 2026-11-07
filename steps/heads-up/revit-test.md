# revit-test heads-up, FAST-3-revit-test

8 Nov 2026, 00:20. Short check 2 done and closed. Revit closed by File, Exit at 00:01:32, exited 00:02:01, no Revit process, no lock. Task status stays open for run 2.

## Final statement

- Short check 2 on NBH 01A (tuned) from a fresh empty copy, FAST3-short2-01A.rvt, on the add-in Lead installed at 22:01, one part 22:16 to 00:02, under 110 minutes, the copy saved at 00:00:08. Always Load pressed at 22:18:18.
- Result: PASS of Lead's pass. The Build of card 2 ran its 8 steps to the end with no stop at Check, card 2 "Done, 462 issues".
- Counts placed: floors and ramps 124 on 62 plots (62 generic, 62 ramp, 0 yard), road toposolid 1 (Compiled West_MM), levels 124 at the plan, 0 moved, villa links 62 of 4 prototype files, walls 178.
- But only 62 of the 278 plots of 01A were built, F29: the plan leaves out 216, most with no hand and no CASE or Foundation Type. The short check of 7 Nov placed floors on 269.
- New faults F29 to F31 in steps/seam.md: F29 62 of 278 plots, F30 card 3 says 2615 plots have no road level, F31 the Revit 2021 links upgrade on load with 4 Revit boxes that wait for a person. Fixed and seen in Revit: F2, F24, F25, F28 for Review and Check, the overlap box of F27, F6 and F13. F22 seen again, on the add-in before PR 272.
- A driver fault of mine at 22:52: one click aimed below the screen opened the GlobalProtect tray panel. Nothing was pressed in it. From then each click scrolls the pane and refuses a point outside it.
- S1 unchanged: 37 files, SHA-256 equal before and after.
- No shot in git, they show real values. They stay in fast3-revit-test/shots/short2 of the work root, one per card: s03-after-save.png (card 1), s14-scope.png (card 2 before Build), s25-card-end.png (the 3 cards at the end).

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the short check 2 section, part 1 and the result table.
- steps/seam.md: F29 to F31 and the fixed list.
- steps/log.md: the short check 2 entry.
- No code. Outside the repo: the helpers of this check in the session scratch folder, drive3.ps1 unchanged.

## Next

- Lead decides run 2, moved to Sun 06:00 by Lead. The copy FAST3-short2-01A.rvt holds the built 01A.
- Main now holds PR 272 (F22) and PR 273 (FAST-10), merged after the install of 22:01. Not seen in Revit, run 2 needs Lead's new install.
