# revit-test heads-up, FAST-3-revit-test

7 Nov 2026, 06:50. Run 1 of the one test done, Revit closed clean at 06:37:53, no Revit process, no lock. Task status stays open for the second run on Sun 8 Nov.

## Final statement

- Run 1 went from the empty model through the 3 cards on S1 in three parts of at most 2 hours, Revit opened by the driver at 01:11, 03:19 and 05:11, the model copy saved at the end of parts 2 and 3. Always Load pressed on our box at 01:11:56 and 03:19:53.
- Stage 1 works from the cards once step 1 has run: Choose project folder, the read outside Revit about 12 minutes, What the tool read, Confirm what was found, the rows on card 1, Fix items. Settings, the rules table, the amber line, the one ask of the first Build, the preview and the source mix of card 3 were seen.
- The site was not built. Every Build stopped at 4 Check, as the read of card 1 never writes the files step 4 needs, F16. No floor, level, villa link or wall, no Report. NBH 010, not used for tuning, holds no villa type, F15, and so do 01B and 023.
- The fix list F1 to F17 by lane is in steps/seam.md, the main ones F16, F4, F1, F5.
- S1 unchanged: 37 files, SHA-256 equal before and after.
- Shots in git: pane-stage-1.png to pane-stage-3.png and 10 window shots revit-*.png, real values blanked. The other shots stay in fast3-revit-test/shots/run1 of the work root.

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the record, times, every press, what was seen, the acceptance words seen or not.
- steps/seam.md: F1 to F17 by lane.
- steps/ui-shots/: the 3 card shots taken in Revit, 10 window shots, README rows.
- steps/demo-live.md: the script for Mon 9 Nov, with the steps around F1, F2, F4, F5 and F13 until they are fixed.
- No code. The driver and its helpers stay outside the repo, Lead's copy of drive3.ps1 in fast3-revit-test of the work root.

## Next

- Sun 8 Nov, run 2 on the fixed build, from FAST3-empty-model-pristine.rvt, copied to a new name.
