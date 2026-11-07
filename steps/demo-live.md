# Live demo, Mon 9 Nov 2026

The script for the managers, in plain words. Nothing is run before the room sits down. Nothing comes from the past job, no past model, no override list, no past Excel. Written by revit-test from run 1 of FAST-3 on 7 Nov, steps/revit-results/FAST-3-revit-test.md. Where run 1 hit a fault of the fix list in steps/seam.md, the step says what to do until it is fixed. Lead takes out those notes as the fixes land.

## Before the room, 10 minutes

1. Close every Revit. Copy the empty model to a new name in the work folder, never open the one of the last demo.
2. Start Revit once, open the copy, and if the box Security - Unsigned Add-In comes, press Always Load. Close Revit with File, Exit, not by ending the process, so Revit keeps that answer (F11).
3. Have the project folder ready, the copy of the project files the tool reads. It is read in place and never written.
4. Close mail, chat and meeting windows on the shared screen.

## 1. An empty model, 2 minutes

1. Open the model copy. Show the project browser: Level 1, Level 2, Site, no link, nothing built.
2. Show the pane on the right: three cards, Project data, Build site, Review and deliver, each saying what it waits for. Settings at the top. Advanced closed at the bottom.
3. Say: everything the tool builds comes from the files of this project and the rules we choose for it, and every value says where it came from.

## 2. Project data, 15 minutes

1. Settings, Project setup, Edit. The New project window lists the files the tool needs and who gives each. Start from Empty project. Save setup.
2. Point at the amber line: no rules chosen yet. Settings, Rules, Open shows the 15 rules of the manual, each not confirmed. Close, the first Build asks about them.
3. Until F1 and F2 are fixed: open Advanced, Baseplan row, Browse, pick the site drawing. Run 1 Set up site. Use CAD. Show the check points against the drawing. Dry run, then tick Current on the site drawing, then Apply.
4. Card 1, Choose project folder, pick the project folder, OK. The box shows the files and their size and that nothing changes until Apply. Apply.
5. The read runs outside Revit, about 13 minutes on a full site. Revit stays free, show it: pan the view, open a menu. Talk through the next part while it runs.
6. The window What the tool read: for each need, Strong, Usable, Weak or Missing, with one line why. Close.
7. The window Confirm what was found: the column that gives each plot its villa type, the column that gives its area, the code map from the schedule. Confirm. Choose project folder again so the read takes the confirmed values, another 13 minutes. Show card 1 with its rows and Fix items.

## 3. Build site, 15 minutes

1. Until F5 is fixed: in Advanced set the Prototypes and Road surface rows to the folders of the project before the read of step 2.4, else the Build stops at Check.
2. Advanced, Scope, Neighbourhood, pick one. Villa mode Links.
3. Card 2, Build, Preview first ticked. The one ask: use the rules of the manual for this project. Yes. The amber line now counts the rules not yet confirmed.
4. Until F4 is fixed: if card 2 says "The plan could not be made ... (R405)", run Set up site once from Advanced, Dry run, Cancel, then Build again.
5. The preview draws every plot outline north up, with the count of plots and issues. Nothing is written to the model yet. Go on.
6. The Build opens 3 Review data for the neighbourhood. Press Save review, then Close, else the Build stops there (F13).
7. The Build goes through Check, Floors and ramps, Levels, Place villas and Walls, one line on card 2 per step.

## 4. Review and deliver, 5 minutes

1. Card 3: the issues of the build with Open rules, the plots with no road level, and the bar of where each value came from, computed, rule, person entered, missing.
2. Report writes the Excel files. Update from report reads an edited Excel back and shows each change before it is applied.

## If something stops

Say what card 2 or the box says, word for word. It names the step and why. Do not run anything from the past job to cover it.
