# Part 1 check, the long form, for later

Your final check of Part 1, in Revit 2024, on NBH 012. It replaces the parts of steps/bader-fix-1nov.md, which stays as history.

One action per step. (PR n) names the pull request that built the step. "Hand run" numbers are of the PR 150 run through the real windows, 3 Nov 2026. "This round" numbers are of the 6C-3NOV-C run, also through the real windows, on main with PR 153. Where only one is given, the other did not record it.

## Known not to work yet

Do not chase these. Each has a question in steps/questions-open.md.

| What you will see | Where | Question |
|---|---|---|
| The 688 accepted overlap warnings come back in Review Warnings by the first save. Only the box is quieter | 5 Floors and ramps | Q385 |
| The Revit box still stops step 5, 485 warnings, 42 of them off axis | 5 Floors and ramps | Q356 |
| Picking the neighbourhood after the first read sends the pane back to 2 Read data once | 2 and 3 | Q306 |
| Neighbourhood ticked before the first read has an empty list and stops step 2. Tick Whole project | Pane, Scope | Q306 |
| Ctrl+Z of button 1 leaves step 4 building on a site.json off the model until the next press | 1 Set up site | Q382 |
| A Current tick changed on a held step 1 reruns the later steps | 1 Set up site | Q383 |
| Revit crashed once on Browse For Folder of the Prototypes row, in explorerframe.dll. Three tries by hand are still owed | Pane | Q381 |
| The rule column reads Link rotation by the rule for frontEdge and the past value reads {"edge": n} | 4 Check | Q376 |
| Report Summary reads not run yet for 1 Set up site and 3 Review data | 012_Report.xlsx | Q307 |
| What the tool read shows 4 value rows where the board shows 6 | 2 Read data | Q289 |
| New project shows Start from Empty project when reopened on a saved setup | New project | Q400 |
| On this PC a new project still takes its schedules from inputRoot, another PC needs the inputs of this job | New project | Q379 |
| Update from report on a new project: an empty override list holds no entry for a change | Update from report | Q394 |
| A project mode held in memory is lost on a reopen | New project | Q395 |
| A first Past on a plot of a native key on a new project | 4 Check | Q396 |
| Save As of a new project keeps one override list | New project | Q397 |
| 443 hairline floor overlaps of two neighbouring plots at step 5 | 5 Floors and ramps | Q398 |
| Road surface: Browse is cut at the pane edge and the name landxml never shows | Pane | Q403 |
| Road surfaces window could not be opened: Pick surfaces is grey under a past job, so no Revit run has seen it | Pane, New project | Q403 |
| The band of 1 Set up site reads the Settings entity SiteAdaptation_Settings | 1 Set up site | Q404 |
| Rules has no Export to Excel button, hidden on purpose since R444 | Rules | Q405 |
| The Plot column of Conflicts shows the CAD label, not Plot No. Plot No 2291 has CAD label 2281, and another plot has Plot No 2281 | 2 Read data | Q406 |
| 12 plots differ from the past schedule on FFL, up to 187 mm, and plot 2312 is blank | Level Excel | Q401, Q402 |
| Update from report showed nothing after a report was picked, in the one try of this round. Its filled state is not seen by this round. Lane B ran the same flow in CHECK-2NOV | Update from report | Q408 |
| A file dialog can take over a minute to appear or close, its inside black meanwhile. Wait, do not press again | Every Browse | none, seen once per dialog in this round, cause UNKNOWN |

The 4 masses questions Q390 to Q393 are about the wall ring, they change no step below.

## Page 0. Pull, build and install

1. Close Revit 2024. The install refuses while it runs.
2. Open VS Code.
3. File, Open Folder, pick C:\Users\p003653k\OneDrive - Parsons Corp\Documents\GitHub\site-adaptation-tool, then Select Folder.
4. Terminal, New Terminal.
5. Run each line below on its own and wait for the prompt after each.

```
git switch main
git pull
dotnet build dotnet\SiteAdaptation.sln -c Release
C:\Users\p003653k\Tools\pwsh\pwsh.exe -File dotnet\SiteAdaptation.AddIn\Install-SiteAdaptation.ps1 -NoBuild
.venv\Scripts\python C:\Users\p003653k\Documents\SiteAdaptation\check_6c\make_cfg.py .
```

The last line copies the config and rules of what you just pulled to Documents\SiteAdaptation\check_6c, keeps the demo input paths and writes the saved project SA-6c. It writes nothing under C:\SA-demo or the repo. Do not use ROSHN Makkah (saved) for this check: its config C:\SA-demo\config\project.json is older than PR 153 and button 7 stops on addin.lightMode.wallRingOutsideM (seen in this round).

Look for: the build ends with "0 Error(s)". The install shows no red line. make_cfg ends with config and rules copied, saved project SA-6c written. If it ends in a red error, stop and tell Lane C, button 4 cannot run without its copy.

## Page 1. New project (PR 112, 117, 132, 133)

1. In the terminal run:

```
copy C:\SA-demo\SA-demo-start.rvt C:\Users\p003653k\Documents\SiteAdaptation\check_6c\SA-part1-check.rvt
```

2. Start Revit 2024 and open that copy. If Revit asks about an unsigned add-in, press Always Load for each one.
3. In the pane on the right, press Open New project.
4. At Start from, pick SA-6c (saved).
5. At Site drawing (CAD), press Browse and pick C:\SA-demo\inputs\ROSHN Makkah Baseplan.dwg.
6. At Plot list (Excel), press Browse and pick C:\SA-demo\inputs\PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx.
7. At Road levels (civil file), press Browse and pick the folder C:\SA-demo\inputs\landxml.
8. Press Check files.
9. Press Save setup.

Look for (this round): Check files reads 3 of 4 files ready, Villa levels reads Comes later, Parameter sheet reads Missing with the box The tool stops here, and that stops nothing. The window closes on Save setup and the pane band reads Next step, Set up site.

### A new project with no past job (PR 156)

Lane A says the Empty project and Pick surfaces steps are not seen in Revit yet.

1. Save a copy of a model with no setup under a new name. Open it.
2. Press Open New project. At Start from pick Empty project.
3. Look for the line New project rules, Rev 06 under the combo, Pick surfaces active, Parameter sheet Found.
4. Press Pick surfaces. Look for: the 7 grading files are not listed.
5. Pick the surfaces and files. Look for 4 of 4 files ready, then press Save setup.
6. Run buttons 1 to 4. Look for: 4 Check reads 0 overrides and profile parsons2024Rev6.
7. On a model with a saved project instead, look for: the line reads ROSHN past job and Pick surfaces is grey.

### The rest of the input rows of the pane (PR 90, 92, 151)

10. At Villa blocks press Browse and pick C:\SA-demo\inputs\Makkah Building Footprint.dwg.
11. At Plot fronts (curb lines) press Browse and pick C:\SA-demo\inputs\Makkah Phase 1_Road Layout.dwg.
12. At DLUS and sales press Browse. In the dialog DLUS schedule pick C:\SA-demo\inputs\TBD-PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_Rev9.0.xlsx. A second dialog Sales schedule follows: pick the Plot list file of step 6.
13. At Plot numbers press Browse and pick C:\SA-demo\inputs\01101-PSN-ZZZ-XXX-DWG-TU-005000.pdf.
14. At Prototypes press Browse and pick the folder C:\Users\p003653k\Documents\SiteAdaptation\prototypes_2024. Never the Revit 2021 folder: those files upgrade on each use, 20 to 41 minutes.

Look for (this round): Input files reads 7 of 8 found, only Shared parameters Not chosen, leave it. No warning line under Prototypes (PR 151). Scope stays on Whole project. Leave Dry run first as it is.

## Page 2. 1 Set up site (PR 108, 126, 127, 148)

1. Press Run 1 Set up site.
2. Tick Current on the row Baseplan. Apply was grey until now.
3. Press Dry run.
4. Press Apply.

Look for: 2 of 2 check points pass on an existing site file with Diff 0.00 (this round). The window says 11 parameters, 12 left out (hand run). About 8 s to the end (hand run). No warning. The row reads Done and the band Next step, Read data.

## Page 3. 2 Read data (PR 111, 117, 119, 130)

1. Press Run 2 Read data.
2. On the box, press Apply. Its text says Nothing changes until Apply or Dry run done.
3. Wait for What the tool read, about 1 minute.
4. Press Use the proposed for all.
5. Press Use these picks.
6. Press Close on the box.
7. In the pane under Scope, pick Neighbourhood and then 012 in its list.

Look for: 18 files read, 2,964 plots found, 386 conflicts on What the tool read (hand run, this round). About 155 s in the hand run, 109 s in this round. Plot outline and Plot number read 2,964 plots, Clear. The Baseplan row reads Read and External references not read: 3 (R505, this round). The Neighbourhood list holds 012 and 013 after this step (PR 131). The row reads 2 issues (this round).

## Page 4. 3 Review data (PR 111, 119, 124, 133)

1. Press Run 3 Review data.
2. Press Confirm all.
3. Press Save review.
4. Press Close.
5. The band goes back to Read data (Q306). Press Run 2 Read data, Apply and Close on the box. The read is reused, about 8 s (this round).
6. Press Run 3 Review data again, Save review if asked, then Close.

Look for: the title 3 Review data, NBH 012, 337 plots. 337 of 337 checked by the team after Confirm all, OK 337 (this round). Team check reads the whole words Not checked before it (R505, this round). The hand run counted 379 of 379 under a wider scope, not a fault. The row reads Done, the band Next step, Check, and the line under the tick says the tool unticks Dry run for 4 Check (PR 133).

## Page 5. 4 Check (PR 111, 119, 147, 149)

1. Press Run 4 Check.
2. On the box, press Apply.
3. Read the chips.
4. At Field, pick rampJoint.
5. Set Field back to All fields.
6. Close the window with its X.
7. Press Close on the box.

Look for: the title 4 Check, NBH 012, 337 plots. All 2,347 rows: Plot cut 0, Floor 1,147, Hand 13, Unit code 3, Position 4, Level 814, Parameter 366, Assumed 20 (this round). 1 plot follows every rule. Field rampJoint shows 1,110 of 2,347 (hand run). About 55 s in the hand run, 30 s in this round. The band reads Next step, Floors and ramps, Dry run is off.

## Page 6. 5 Floors and ramps (PR 113, 128, 139, 146)

1. Press Run 5 Floors and ramps.
2. On the box, press Apply.
3. The Revit box stops the step. Press OK. Cancel undoes the whole step.

Look for: the Revit box reads 0 Errors, 485 Warnings (hand run, this round), 443 overlap and 42 off axis (hand run). 1,003 placed, 0 refused, 688 accepted without showing (hand run). About 22 s to the box. The row reads 490 issues (this round), the band Next step, Levels.

## Page 7. 6 Levels (PR 113)

1. Press Run 6 Levels.
2. On the box, press Apply.
3. Press Close when it ends.

Look for: move 0, at the plan 1,003, listed 1 (hand run). About 11 s. No box of warnings. The row reads 1 issue. 012_Levels_parsons2024Rev6.xlsx is in the work folder.

## Page 8. 7 Place villas (PR 111, 113, 127, 153)

1. Press Run 7 Place villas.
2. On the box, press Apply.
3. Press Close when it ends.

Look for: 336 placed, 0 mass files to make (hand run). About 20 s when the masses are current. The first run after PR 153 remakes each stale mass once, about 8.7 s each. The row reads 24 issues (this round), the band Next step, Walls. Villa mode stays Masses.

## Page 9. 8 Walls (PR 113, 142, 149)

1. Press Run 8 Walls.
2. On the box, press Apply.
3. Press Close when it ends.

Look for: 778 placed, listed 779 (hand run). About 7 s. The row reads 779 issues, that is the listed walls, not 779 faults. The band reads Next step, Report.

## Page 10. 9 Report (PR 110, 144, 154)

1. Press Run 9 Report.
2. On the box, press Apply.
3. Press Close when it ends.
4. Press Open folder in the band.
5. Open 012_Report.xlsx.

Look for: the band reads Run complete, 012_Report.xlsx, The report is in the work folder. About 6 s. Sheets Summary, Placed, Skipped, Overrides, Assumed, Issues, Accepted warnings, Rules, Settings (this round). Placed has 1,339 rows: link 336, generic 336, ramp 336, yard 331, with 1,339 distinct element ids (hand run, this round for the rows). Summary reads 1,003 for floors, 336 for levels and villas, 778 for walls (this round). Accepted warnings has 688 rows (hand run, this round).

6. Save the model.
7. Close it and open it again.

Look for: the rows keep Done and their issue counts, the band stays Run complete (PR 154, Lane B's run, not this round's).

## Page 11. The other windows, once

1. Press How it works in the pane. Look for: 9 steps, one row per button, then press Close (PR 112).
2. On the Site Adaptation tab press Site Adaptation Rules. Look for: 15 rules, M06 reads Differs, then close it with its X (PR 111).
3. On the tab press Update from report. Look for: Pick the edited report Excel and Apply 0 changes grey, then press Cancel (PR 110). The flow with an edited Excel is Q408.

## What to send back

The first step on a page where what you see differs from Look for, with the page and step. Any new fault goes to a new question ID, not into this sheet.
