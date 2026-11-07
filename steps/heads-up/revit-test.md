# revit-test heads-up, FAST-3-revit-test

7 Nov 2026, 14:20. Run 1b done and closed by Lead at 14:10. Revit closed clean at 14:06:23, no Revit process, no lock. Task status stays open for the short check after PR 256 and for run 2.

## Final statement

- Run 1b ran the old steps from Advanced on NBH 01A (278 plots, tuned) on a fresh empty model copy, in two parts under 110 minutes, 10:40 to 12:22 and 12:27 to 14:06, on the add-in of 6 Nov 23:00. The copy was saved at the end of each part. No wanted file of build-1 or build-3 stood before either part.
- Steps 1 Set up site, 2 Read data and 3 Review data reached Done, card 1 Done. 4 Check stopped on F16, now seen from Advanced too: Run 2 Read data of Advanced is the data check of card 1 and never writes the files step 4 needs. Steps 5 to 9 did not run. No floor, level, villa link, wall or report was built in run 1b. Not seen in Revit: every step past 4.
- New faults F18 to F23 in steps/seam.md. The main one F18: on a new project, Run 2 Read data of Advanced read the input root of the past job that config names, not the 8 input rows set to S1, 32 minutes. Then F21 (step 1 Done lost over a save and reopen), F22 (two full reads before a neighbourhood scope), F19 (no stop for a running read), F20 (Cancel loses the read), F23 (a change of scope undoes step 2 with no line).
- S1 unchanged: 37 files, SHA-256 equal before and after.
- CHOSEN by Code: the Prototypes row on the 2024 copies of the work root, as the pane asks, not the Revit 2021 files of S1.
- No shot of run 1b in git, they show real values. They stay in fast3-revit-test/shots/run1b of the work root.

## What changed, where

- steps/revit-results/FAST-3-revit-test.md: the run 1b section, parts 1 and 2.
- steps/seam.md: F18 to F23, F10 and F16 seen again.
- steps/log.md: the run 1b entry.
- No code. The driver is drive3.ps1 of the work root. The helpers of this session sit in its scratch folder: Pick-Folder2 walks the folder box by type ahead and the numpad plus, Show-Fast brings a pane row into view, Open-Advanced and Step-States work by UI Automation with no click.

## Next

- Wait for Lead's message. Then the short check: once PR 256 is on main and Lead installed main, 01A from a fresh copy of FAST3-empty-model-pristine.rvt, card 1 then Build to the end. The new DLL brings the Unsigned Add-In box back, the driver presses Always Load.
- Run 2 tonight 23:30 if the short check passes.
