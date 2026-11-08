# Live demo, Mon 9 Nov 2026

The script for the managers, in plain words. Nothing is run before the room sits down. Nothing comes from the past job, no past model, no override list, no past Excel. Written by revit-test from run 1 of FAST-3 on 7 Nov, steps/revit-results/FAST-3-revit-test.md. Where run 1 hit a fault of the fix list in steps/seam.md, the step says what to do until it is fixed. Lead takes out those notes as the fixes land.

## The neighbourhood, 01A, used for tuning

The demo and run 2 of FAST-3 use neighbourhood 01A, Bader's order of 7 Nov 10:05. 01A was used for tuning the rules of the past job, so this demo shows the tool on a neighbourhood it was tuned on, not on new ground. Say so in the room. The untuned neighbourhoods of this folder hold no villa type, so no villa link is placed there (F15 of steps/seam.md).

## Before the room, 10 minutes

1. Close every Revit. Copy the empty model to a new name in the work folder, never open the one of the last demo.
2. Start Revit once, open the copy, and if the box Security - Unsigned Add-In comes, press Always Load. Close Revit with File, Exit, not by ending the process, so Revit keeps that answer (F11).
3. Have the project folder ready, the copy of the project files the tool reads. It is read in place and never written.
4. Close mail, chat and meeting windows on the shared screen.

## 1. An empty model, 2 minutes

1. Open the model copy. Show the project browser: Level 1, Level 2, Site, no link, nothing built.
2. Show the pane on the right: three cards, Project data, Build site, Review and deliver, each saying what it waits for. Settings at the top. Advanced closed at the bottom.
3. Say: everything the tool builds comes from the files of this project and the rules we choose for it, and every value says where it came from.

## 2. Project data, 15 minutes, 55 in the rehearsal

Rehearsed in run 2 of FAST-3 on 8 Nov from 03:32, steps/revit-results/FAST-3-revit-test.md. Card 1 took 55 minutes there, most of it in the two reads.

1. Settings, Project setup, Edit. The New project window lists the files the tool needs and who gives each. Start from New project rules, Rev 06.
2. The row Road levels: Browse, pick the landxml folder of the project. Then Pick surfaces lists the road surfaces of that folder, each unticked. Tick the ones that give the floor levels, for 01A Compiled West_MM and Part 4 Top Surfaces, in that order. Use these picks. The row then says "Road surfaces in order (2): ...". Pick surfaces before the folder says the folder holds no road surface files. Save setup.
3. Point at the amber line: no rules chosen yet. Settings, Rules, Open shows the rules of the manual, each missing and not confirmed. Close, the first Build asks about them.
4. Card 1, Choose project folder, pick the project folder in the Select Folder box. The window 1 Set up site opens by itself with the site drawing of the folder. Show the 5 check points against the drawing. Tick Current on each file, Dry run, Apply.
5. Choose project folder again, the same folder. The box shows the files and their size and that nothing changes until Apply. Apply.
6. The read runs outside Revit, about 12 minutes on a full site (734 s in the rehearsal). Revit stays free, show it: pan the view, open a menu. Talk through the next part while it runs.
7. The window What the tool read: for each need, Strong, Usable, Weak or Missing, with one line why. Close.
8. The window Confirm what was found: the column that gives each plot its villa type, the column that gives its area, the code map from the schedule, and a hand per prototype, left empty. Confirm. The box says the confirmed values count from the next read (F22), so Choose project folder a third time, Apply, another 12 minutes. Close What the tool read, Cancel on Confirm, the values stay.

## 3. Build site, 15 minutes, 45 in the rehearsal

1. Advanced, Scope, Neighbourhood, pick 01A. Say it was used for tuning. Villa mode Links. Do this before 3 Review data, which needs one neighbourhood.
2. 3 Review data: 278 plots of 01A. Save review, Close.
3. Card 2, Build, Preview first ticked. The one ask: use the rules of the manual for this project. Yes. The amber line then counts the rules not yet confirmed.
4. The preview counts the plots and issues, nothing is written to the model yet. Go on.
5. 2 Read data says it reuses the last read, Apply, a few seconds. 3 Review data opens again, Save review, Close.
6. The Build goes through Check, Floors and ramps, Levels, Place villas and Walls, each with a box that counts what it will do, Apply on each. Check opens its table, Close goes on.
7. Place villas links the prototype files. On files of an older Revit it took 28 minutes for 263 links of 13 files in the rehearsal, with no box to answer. Plan the talk for it, or use prototypes saved in Revit 2024.
8. Until F32 is fixed: after Walls, Revit shows its own box "Highlighted walls overlap". Press OK, the walls are kept. Cancel would undo them.
9. Card 2 then says Done with its issues, the plots built of the scope and the plots left out with why.
10. Do not close and open the built model in the room. Until F33 is fixed, the open upgrades the old prototype files again and stops 13 times on "Structural Analytical Model Upgrade", 27 minutes.
## 4. Review and deliver, 5 minutes

1. Card 3: the issues of the build with Open rules, the plots with no road level, and the bar of where each value came from, computed, rule, person entered, missing.
2. Report writes the Excel file, 23 s in the rehearsal: the plot schedule, floors, walls, issues, rules used, placed, skipped and assumed values. Update from report reads an edited Excel back and shows each change before it is applied. The same Excel unedited says "No changes found in this Excel." and keeps Apply off.

## If something stops

Say what card 2 or the box says, word for word. It names the step and why. Do not run anything from the past job to cover it.
