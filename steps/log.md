# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## FAST-11-build-1, the plan builds the plots of a new project, 8 Nov 2026

Goal:
- F29 and F30 of short check 2: the plan of 4 Check on 01A wrote 62 of 278 plots, and card 3 counted 2615 plots with no road level.

Done:
- R800: under rev6 a plot no rule gives a hand is built not mirrored, its hand tagged missing, listed in the missing list of the plan. A parameter with no value is left empty and listed. PlanOverrides.cs, BuildPlan.cs, ValueStatus.cs.
- R801: the plan summary counts the plots left out by the key that stops them, and card 2 says it after the Build line. CheckPlanCount.cs, StageCards.cs.
- R802: card 3 counts plotsWithNoRoadLevel of the plan of 4 Check, of the scope.

Checked:
- The worker of this branch on the request of the real Check of 01A: 265 of 278 plots written, 13 left out, no floor level 9, transform 2, unit 2, 203 built with the hand missing. Counted by a Python script from plan_01A.json. Inputs unchanged by SHA-256.
- Tests: dotnet/SiteAdaptation.Tests/FAST11/, 9 on net48, 8 on net8.0. Review 1 found 1 REAL fault, fixed with a test.

Remains:
- Not seen in Revit, label revit-later, run 2 of revit-test at 06:00.

Known bugs:
- None found. The box of step 5 still says only "listed".

Next:
- Run 2 on 01A builds about 265 plots.

Your turn:
- None.

## build-3, FAST-12, step 7 and prototypes of an older Revit, 8 Nov 2026

Goal:
- F31 of short check 2: an unwatched Build must not wait on the box "Structural Analytical Model Upgrade" of step 7, and the box of step 7 counts the link types it loads and says they upgrade.

Done:
- R805. dotnet/SiteAdaptation.AddIn/PlaceVillas/UpgradingVillaDocument.cs wraps the document of button 7. Around each load, reload and make of a link type it closes the box of id TaskDialog_Views_Related_To_Analytical_Changed with Close and counts it. Any other box stays as before.
- R806. The issues of step 7 name each file of an older Revit, read with BasicFileInfo before the load, and the number of boxes closed.
- R807. The box of step 7 adds "link types new to the model N, of an older Revit M" and a line per older file.
- Rules docs/rules/28-fast12-r805-up.md, tests dotnet/SiteAdaptation.Tests/FAST12/LinkUpgradeTests.cs, 8 tests. The task stays open until the Revit check.

Checked:
- quick tests PASS, net48 3307 passed, acceptance 45 passed 5 xpassed. A mutation of Close and of the year compare failed 3 tests.
- Second check in Python: the buttons run LB6E logged the id with Close 8 taken in step 7, the driver of short check 2 pressed Close on the title 4 times, a Revit file header gives Format as a bare year.

Remains:
- The Revit check, label revit-later, owed by run 2 of revit-test (steps/revit-results/FAST-12-build-3.md).

Known bugs:
- "link types to load" of PlaceVillasRun still counts only unloaded types, the new count stands beside it (steps/seam.md).

Next:
- Run 2 checks step 7 on 2021 prototypes.

Your turn:
- None.

## Lead, FAST-11 and FAST-12 for F29 to F31 of short check 2, 8 Nov 2026

Goal:
- Send the faults of short check 2 to a lane the same hour, Bader's order of 7 Nov 14:40.

Done:
- steps/tasks/FAST-11-build-1.md: F29, the plan builds a plot with no hand or no parameter source, tagged missing, and F30, card 3 counts the plots of the scope.
- steps/tasks/FAST-12-build-3.md: F31, step 7 closes the upgrade box of an older Revit prototype.
- steps/plan-fast-3days.md: section Sun 8 Nov 00:45.

Checked:
- The faults come from PR 275 of revit-test, plan_01A.json written 62, not written 216.
- Every allowed path exists on main, R800 and Q620 are free.

Remains:
- Run 2 at 06:00 on 01A.

Known bugs:
- F29, F30, F31 until FAST-11 and FAST-12 merge.

Next:
- Install main before 06:00.

Your turn:
- Sun 8 Nov: the folder path of the new project by 10:00.

## FAST-3-revit-test, short check 2 of 01A, 7 and 8 Nov 2026

Goal:
- Lead's order, Bader's of 14:40: the second short check on 01A from card 1, on the add-in of main installed at 22:01. Pass is floors and ramps, levels, villa links and walls placed with no stop at Check.

Done:
- One part in Revit, 22:16 to 00:02, on FAST3-short2-01A.rvt, a fresh copy of the empty model. Always Load pressed 22:18:18. Choose project folder S1 ran step 1 itself, two reads of S1 (806 s and 710 s, F22), Scope 01A, then Build to its end: 8 steps, card 2 "Done, 462 issues".
- Placed: 124 floors and ramps on 62 plots, the road toposolid, 124 levels at the plan, 62 villa links, 178 walls. Copy saved 00:00:08.

Checked:
- Each count twice, from the dry run box of each step by UI Automation and from the step files on disk by a script. S1 37 files unchanged by SHA-256, size and write time.

Remains:
- F29 the plan builds 62 of 278 plots, F30 card 3 counts 2615 plots with no road level, F31 Revit 2021 links upgrade with 4 boxes that wait for a person. In steps/seam.md.

Known bugs:
- A driver click at 22:52 aimed at a button below the screen and opened the GlobalProtect tray panel. Nothing was pressed in it, Revit was brought back by its window handle, and from then each click scrolls the pane first and refuses a point outside it.

Next:
- Lead decides run 2, now Sun 06:00 by Lead's message during the check.

Your turn:
- Nothing for Bader from this check.

## FAST-9-build-2, Confirm applies to the read just made, F22, 7 Nov 2026

Goal:
- F22, Bader's order of 21:06: on a new project, one read and one Confirm, then the Scope list names the neighbourhoods of the confirmed area column and the plots carry the confirmed villa types, with no second read of about 12 minutes.

Done:
- R790: the worker command dataCheck keeps what it gathered, data_check_input.json in the read folder, and the check is DataCheckApply.Check, shared by the worker and the Confirm.
- R791: after a Confirm that saved, ReadDataRun.ApplyConfirmed runs the check again in Revit on that file with the confirmed profile, writes data_check.json, project_profile.json, plots.json and read_plotTable.json, moves the read folder to the folder its inputs now name and writes read.json last. The next press of 2 reuses it.
- R792: a read with no gathered file, or any fault, keeps the step done and says the values count from the next read.

Checked:
- R791_ConfirmAppliesTests on the made up project of samples/new-project with its sheet areas parted unlike the distance groups: one worker run, the Scope list N1 and N2, plot 103 VA1 from the column, a second press reused. The same press with no apply keeps the old areas and VB2, so the test sees F22. The second check by another way: a full worker read with the same confirmed profile gives the same four files but for the run stamp.
- R790_GatheredFilesTests on net48 and net8.0. Not seen in Revit.
- S1, read only, outside the repo under the work root: the first read gave 1 Scope entry, the check on its gathered files with the proposed profile gave 11, as a full read with that profile, and data_check.json, plots.json, project_profile.json and read_plotTable.json equal that read but for the run stamp, 2615 plots. Each read took 922 to 964 s.

Remains:
- Step 2 shows not done after the Confirm until one more press, which reads nothing. A read that is not whole is still read again on every press by R639. Both in steps/seam.md for Lead.

Known bugs:
- On S1 the gathered file is 667 MiB, 1.7 GB before it was written with no indent, and the check on it took 106 to 126 s at 11 GB peak working set, outside Revit on net8.0. In Revit that is about 2 minutes busy after Confirm, in Revit on net48 UNKNOWN. In steps/seam.md for Lead.

Next:
- revit-test checks the Confirm on the new project of 8 Nov after FAST-8 and short check 2.

Your turn:
- Answer the seam line on reads that are not whole, before the demo.
## The intake of a new project folder, 7 Nov 2026

Goal:
- Bader's order of 21:06 item 3: a script that reads a new project folder outside Revit and writes what the form of 8 Nov needs.

Done:
- scripts/new-project-intake.ps1: the stage 1 read by the worker of the installed add-in, the raw read then the read with the proposed columns confirmed, <work root>\intake\intake.md with the needs, files by kind, proposed columns, neighbourhoods with villa types, LandXML files, run time, and the folder check.

Checked:
- Run on S1: 23 min, 0 of 37 files changed. The first run was refused on a level with no word and wrote under the Documents folder of OneDrive, both fixed, that output removed.

Remains:
- The cover of each road surface per neighbourhood, UNKNOWN until a plan of 4 Check.

Known bugs:
- None in the script.

Next:
- Short check 2, then the run on the new project of Sunday.

Your turn:
- Sun 8 Nov: name the folder of the new project.

## Lead, FAST-10 allowed list widened, run 2 moves to Sun 06:00, 7 Nov 2026

Goal:
- Unblock PR 273 of FAST-10 and keep the plan true.

Done:
- steps/tasks/FAST-10-build-3.md allows the 7 paths PR 273 changed outside its list, with the reason under Scope.
- steps/plan-fast-3days.md: run 2 moves to Sun 06:00, the state of PRs 272 and 273, the open risk of a new project with no schedule.

Checked:
- The tests job of PR 273, run 42411552782: the only FAIL lines are the folder rules check on those 7 paths. dotnet, dotnet-linux and python pass.
- Lead read the diff of DesignBasis.cs and BuildSteps.cs: an empty road surface list is no refusal only when Set up picked surfaces, no pick refuses as before.

Remains:
- Merge main into PR 273 and rerun, merge PR 272 after its Callers fix.

Known bugs:
- A new project with no schedule takes its plot table from the folder, not seen on a real folder yet.

Next:
- Short check 2 result, then install main before run 2 at 06:00.

Your turn:
- Nothing tonight. Sun 8 Nov: the folder path of the new project by 10:00.

## FAST-8-build-1, Check reads no past file, the toposolid surface, no overlap box, 7 Nov 2026

Goal:
- F26 and F27 of the short check of 01A, Bader's order of 14:40.

Done:
- R780: under rev6 a plots.csv or labels.csv of the data book of the work root the read named is never a Check input, the plot table of the read stands in for it, written to plots_<read> of the work folder. No plot table stops Check as missing.
- R781: Floors and ramps takes, of the road surfaces the build read, the one with the most ramp road edge points, and says which.
- R782: a warning of known words on floors the list does not pair, Highlighted floors overlap of two plots, is taken with no box and listed with its floors in the issues.

Checked:
- FAST8 tests 16 on net48 and net8.0, the full dotnet suite 0 failed, quick tests PASS, tests/acceptance 45 passed 5 xpassed.
- Review 1 FAIL with 4 REAL, all fixed: a stand-in for a named surface not read is said, the record lists each surface read, InDataBook takes the folder text as ReadInputs does, status back to open.
- A script that shares no code: Check took both data book files before, the plot table gives 278 plots of 01A. No road surface of S1 covers 01A, 6 ground and grading surfaces do.

Remains:
- Q600 for Bader, the toposolid of an NBH no road surface covers. F28 line. Not seen in Revit, label revit-later. Task status stays open.

Known bugs:
- Done when of FAST-8 on 01A: toposolid UNKNOWN, cannot pass on S1 without Q600. Yard floors 0 by R410.

Next:
- The second short check by revit-test, on 012 if Bader answers Q600 as recommended.

Your turn:
- Answer Q600 in steps/seam.md.

## FAST-10, a new project shows no past job value, F8, 7 Nov 2026

Goal:
- F8: a project made with Empty project held the rules profile, rules name, road surfaces, schedules and position file of the past job.

Done:
- R795 to R799. dotnet/SiteAdaptation.Common/Settings/NewProjectValues.cs empties each past value of the copy Empty project makes, ShippedSet.MakeNew writes it, ShippedSet.Seen shows the window the copy to be before Save. The rules profile is profile.json, named by the folder of the model, with no past job measure. The read says once that the project names no schedule. Step 4 says "Checks each plot against the rules". The position file is the project's own and on no disk.
- DesignBasis.Of takes the pick of Set up over an empty surface list, so a new project builds on the surfaces the person picked.

Checked:
- NewProjectNoPastJobTests, from the repo config and rules as the install ships them: no rules name, road surface, schedule or profile of the past job in the copy, the rules window, the window before and after Save and the read, the past job files unchanged. InstallScriptTests: a new project from the installed set still reaches a plan through the real worker, with a pick. Full suite green on net48 and net8.0 but the timing test R347 once, green alone. Not seen in Revit.

Remains:
- Window shots and the Revit check, after short check 2 of revit-test.

Known bugs:
- Past values outside the four kinds stay in the copy: drawings, siteBuild.answerKey, majority keys, designBasis.roadLevel.notRoad and the notes, steps/seam.md.

Next:
- Lead's intake and the demo of Sun 8 Nov.

Your turn:
- Nothing.

## The new project path, 7 Nov 2026

Goal:
- Bader's order of 21:06: a real new project on Sun 8 Nov, the path from its folder to a built neighbourhood made fast.

Done:
- FAST-9-build-2 for F22 and FAST-10-build-3 for F8, files split by exact path from FAST-8-build-1.
- steps/bader-form-8nov.md, the template.
- steps/plan-fast-3days.md, the order of tonight and Sunday.

Checked:
- grep of steps/tasks: the allowed lists of FAST-8, FAST-9 and FAST-10 share only steps/seam.md, steps/log.md and docs/rules/index.md. R790 to R799 and Q610 to Q619 are free.

Remains:
- The intake script and its run on S1, the two lanes, short check 2.

Known bugs:
- Q600, no road surface of S1 covers 01A.

Next:
- Lead starts build-2 and build-3, writes the intake script.

Your turn:
- Sun 8 Nov: name the folder of the new project.

## FAST-7 PR 6, the Build goes on after Check, F25, 7 Nov 2026

Goal:
- Item 10, F25 of the short check: the Build looped on 4 Check, 5 runs, and the Check window had no Close.

Done:
- R772: BuildChain keeps the steps that committed in this Build and never puts one back, so after Check commits the Build goes on to step 5, which PaneHost runs once the pane hashed Check again, at most 10 seconds later, as the gate of the flow reads the same statuses. The Check window has a Close beside Save overrides.
- Shot steps/ui-shots/check-table.png with the Close, made up values. steps/tasks/FAST-7-build-3.md status done, the last PR of FAST-7.

Checked:
- R772_CheckGoesOnTests: Check committed while step 5 still waits on it goes on to 5, a step not committed is still put back once, the window shows its Close. Not seen in Revit.

Remains:
- The Revit check of FAST-7, every PR carries revit-later.

Known bugs:
- F23, a scope change alone makes step 2 stale, in Common.

Next:
- Lead's run 2 and the demo of Sun 8 Nov.

Your turn:
- Nothing.

## FAST-7 PR 5, the Build reads and reviews without stopping, F6 and F13, 7 Nov 2026

Goal:
- F6 and F13 of run 1 in Lead's version of 17:15 and 17:38: a step 2 the Build puts back runs once and quietly, a Review Close with nothing changed counts as saved.

Done:
- R770: ReviewDataSession.Closed saves once when nothing is left unsaved, called by ReviewDataDialog after the window closes.
- R771: BuildChain puts a step back once per Build and names the step running (Current). PaneHost sets QuietRead from it. ScanGate opens no window then, ConfirmProfileStep shows no check window and skips the Confirm only when the profile was confirmed for the same scan folder, by confirmed_for.json that each saving Confirm writes (Lead's point of 18:20, the demo reads two folders), else opens it.
- steps/tasks/FAST-7-build-3.md stays open, Lead's word of 19:15: item 10, F25 of the short check, comes first.

Checked:
- R770_ReviewCloseTests, R771_QuietReadTests, R771_QuietWindowsTests, each with a broken case. The chain tests R688 and R741 unchanged and green. Not seen in Revit.

Remains:
- Item 10, F25 of the short check, the Build loops on Check. The Revit check of FAST-7 is owed, every PR carries revit-later.

Known bugs:
- F23: a scope change alone makes step 2 stale, so a Build after a scope change runs step 2 once more, now quietly.

Next:
- Item 10, F25, once Lead's PR 266 puts CheckTable on FAST-7.

Your turn:
- Nothing.

## FAST-3-revit-test short check, 01A from card 1, 7 Nov 2026

Goal:
- Lead's order of 14:40, Bader's: on a fresh empty copy, NBH 01A (tuned), Choose project folder on card 1 with S1, then Build on card 2 to the end, Preview first ticked. Pass: floors and ramps, levels, villa links and walls placed with no stop at Check.

Done:
- Two parts in Revit on FAST3-short-01A.rvt, 15:15 to 17:06 and 17:07 to 18:50, the add-in of main 89d1c69 installed 14:59, Always Load pressed at 15:16:17 and 17:08:16. The copy saved at the end of each part, closed by File, Exit, no lock left.
- Card 1: Settings, 1 Set up site (5 of 5 check points), the read of S1 twice (694 s and 655 s), Confirm, Scope 01A. Build: rules template Yes, Preview Go on, review of 278 plots saved, 4 Check ran, F16 is past.
- Faults F24 to F28 in steps/seam.md, the record in steps/revit-results/FAST-3-revit-test.md.

Checked:
- Floors: 538 by the dry run box in Revit and 538 in floors_01A.json on disk, 269 plots, by a script. S1 unchanged, 37 files by SHA-256.

Remains:
- FAIL of the pass: the Build loops on Check (F25) and never reached Floors. Floors and ramps placed from Advanced Run 5 only, 269 generic and 269 ramp, no yard, no toposolid (F27). Levels, villa links, walls: 0, never ran.

Known bugs:
- F24 choosing the folder undoes step 1, F25 the Build loops on Check, F26 Check reads the data book of the past job, F27 no toposolid and a Revit overlap box, F28 278, 297 and 269 plots for one area.

Next:
- Lead decides run 2 from this record. The copy holds steps 1 to 5 on 01A and can go on from step 6 in Advanced.

Your turn:
- Nothing.

## The short check failed, the faults given, 7 Nov 2026

Goal:
- Bader's order of 14:40: a fault of the short check to steps/seam.md and to a lane the same hour.

Done:
- FAST-8-build-1 for F26 and F27. FAST-7 item 10 for F25, with AddIn/CheckTable/ on its list.
- steps/plan-fast-3days.md: the short check result and when run 2 goes.

Checked:
- PR 265 of revit-test, the record of the short check: Check passed and looped, 538 floors on 269 plots by Run 5, levels, villas and walls 0.

Remains:
- F25, F26, F27, then the second short check, then run 2.

Known bugs:
- F24 to F28 in steps/seam.md.

Next:
- Lead starts build-1 and resumes build-3.

Your turn:
- Nothing.

## FAST-7 PR 4, the folder keeps step 1, F24, 7 Nov 2026

Goal:
- F24 of the short check: after step 1, Choose project folder made step 1 Ready again and the box "2 Read data needs step 1" came.

Done:
- R769: ProjectFolderPick.Write leaves the config byte for byte when the folder is the same, so step 1 stays done. PaneHost.ChooseProjectFolder, on FAST-7 since PR 262, hands the folder to StagesViewModel.FolderChosen as card 1 does before step 1: a Baseplan of the folder before goes, the new folder fills it, and the read runs only when step 1 still holds, else step 1.
- The heads-up of build-3 with the final statement so far.

Checked:
- R769_FolderKeepsStepOneTests and R768_SetUpBaseplanTests: the same folder with another case and a trailing separator, another folder with its own Baseplan, no folder before, and PaneHost calls FolderChosen. Not seen in Revit.

Remains:
- F6 and F13, item 9, next PR.

Known bugs:
- None new.

Next:
- F6 and F13.

Your turn:
- Nothing.

## Ready for the maintenance of 19:30, 7 Nov 2026

Goal:
- Bader, 16:50: IT maintenance at 19:30 may shut the PC down. Every lane safe by 19:15, and a way to start again.

Done:
- steps/plan-fast-3days.md: where each lane stands at 17:05 and the order to start again after a shut down.
- FAST-7 takes ScanGate.cs and ConfirmProfileStep.cs from FAST-5 for F6, the Confirm reuses the profile the person confirmed last, and rule ids R770 to R779.
- The plan held a carriage return from a script, R343 failed, taken out.
- FAST-7 takes R688_BuildChainTests.cs and R740_BuildReadTests.cs for F6 and F13, in their smaller version: no stop added to the Build, the read for the build of R741 kept.
- The driver exemption for Teams widened to the full view of the same keep awake meeting, by Bader's words of 00:16, in .claude/rules/revit.md and FAST-3-revit-test.md. The driver paused from 15:56 on that view.

Checked:
- revit-test answered: part 2 ends by 19:10 saved, no part after 19:00. build-3 told to push by 19:15.

Remains:
- The short check, the F24 PR, run 2.

Known bugs:
- F24 in steps/seam.md.

Next:
- Lead merges on green and reads the short check.

Your turn:
- Nothing.

## The Build chain files move to FAST-7, 7 Nov 2026

Goal:
- F24 of the short check needs PaneHost.cs, F6 and F13 need PaneHost.cs and BuildChain.cs, build-1 stopped after PR 256.

Done:
- PaneHost.cs and BuildChain.cs taken off FAST-5-build-1 and put on FAST-7-build-3, with item 8, the part of F24 in PaneHost.cs. CHOSEN by Lead, a fresh build-1 session for one line costs more than build-3, which holds the cards.

Checked:
- grep of steps/tasks: no other open task lists the two files.

Remains:
- The short check, FAST-5 Done when.

Known bugs:
- F24 in steps/seam.md.

Next:
- build-3 does F24, F6, F13.

Your turn:
- Nothing.

## S1-new, the villa types of 010, 7 Nov 2026

Goal:
- Bader's order of 14:40 item 3: a demo folder where a neighbourhood not used for tuning gets villa types.

Done:
- S1-new in the work root, a copy of S1 with one workbook changed: 11 rows added to the sales sheet by an edit of the sheet XML, every other zip entry byte equal. The 11 plots of 010 whose footprint block holds one prototype code by the rule of R731.
- steps/bader-form-7nov.md, one item, the 5 plots left. FAST-3-revit-test.md holds the Sunday run of 010 on S1-new.

Checked:
- The worker of the installed add-in on S1-new with the confirmed profile of run 1: 11 of the 16 plots of 010 with a villa type, the 2,599 plots outside 010 the same as the S1 read, status done, 0 issues. S1 37 files hashed before and after, 0 changed.

Remains:
- The short check of 01A, run 2 at 23:30, the Sunday run of 010.

Known bugs:
- PH_1-DLUS calls the 11 plots Multi Family, the drawing and the schedule disagree on 010.

Next:
- Lead reads the short check.

Your turn:
- Item 1 of steps/bader-form-7nov.md, by Sun 8 Nov 11:00, A by default.

## FAST-7 PR 3, Set up site takes the Baseplan of the folder, 7 Nov 2026

Goal:
- F2 of run 1: Set up site takes the Baseplan of the chosen folder, with FolderRoles.Of of FAST-5 now on main.

Done:
- R768: SetUpBaseplan.Fill in dotnet/SiteAdaptation.AddIn/Fast7, called by StagesViewModel.ChooseFolder right after a person chose the folder, fills an empty Baseplan row only. A Baseplan of the folder chosen before goes with it. Before step 1, Choose project folder writes the folder and runs 1 Set up site, the read follows at the next press, so card 1 never asks for a folder it cannot take.
- R761 changed with it: Choose project folder is on from the start, its line says step 1 runs first.

Checked:
- R768_SetUpBaseplanTests and R761_CardOneStepsTests, with a refused folder, a cancelled dialog and a row a person set. Not seen in Revit.

Remains:
- F6 and F13, once Lead moves PaneHost.cs and BuildChain.cs after the short check in Revit.

Known bugs:
- None new.

Next:
- F6 and F13.

Your turn:
- Nothing.

## The demo moves to Sunday, 7 Nov 2026

Goal:
- Bader's orders of 14:40 and 14:50: the demo on Sun 8 Nov 17:00 on a new neighbourhood, F16 merged, the short check in Revit, F15.

Done:
- PR 256 merged at 14:57 after Lead merged main into it, the gate failure of 10:43 was the caller check of Take, Most and Text of FolderRoles.cs. PRs 257 and 258 merged.
- Main 89d1c69 built and installed with Install-SiteAdaptation.ps1. revit-test started on the short check of 01A.
- steps/plan-fast-3days.md holds the Sunday order.
- steps/investigations/roshen-mecca-scan.md: the second input folder read, read only, 560 files hashed before and after, 0 changed. 01B holds 105 plots with a villa type in PH_1-DLUS, but the S1 baseplan draws 2 of them, so no untuned neighbourhood of S1 can be built with villa links from real data. C:\02-Site Adaptation 528 of 837 rows, the 309 of 28 Oct still missing.

Checked:
- gh pr checks 256: tests pass on 2ac352c. The install printed one SiteAdaptation.Common.dll in 5 folders of the tool.

Remains:
- The short check, the scan of the second input folder, S1-new, the Sunday run and demo script.

Known bugs:
- F18 to F23 of run 1b in steps/seam.md, F15.

Next:
- Lead reads the short check and the scan.

Your turn:
- Nothing yet.

## FAST-5-build-1 PR 1, F16, the Build reads for Check itself, 7 Nov 2026

Goal:
- Bader's order of 10:05, F16: the Build from card 1 must pass 4 Check.

Done:
- R740: each Build press fills the empty input rows from the chosen folder by the names config gives (FolderRoles), so Check finds the Prototypes row, F5.
- R741: right before 4 Check the Build runs the old read with no schedule, plot number file or border and none of the location plan steps (ReadInputs.ForBuild, Fast5.BuildRead), no box and no Confirm. Its folder holds the plot table of stage 1, so it is the latest read for Check and every later step.
- F4 left to FAST-6 by Lead's word of 12:00, PR 254 fixes it in Common.
- F10: the raw read of card 1 hands the parameter sheet of the pane to the check, in its request only (ReadDataRun.DataCheckRequest, DataCheckSheet.Input of R750). F12: the stop box call after each Build step.

Checked:
- The real worker on S1 with a request a Python script made by its own file search: first refused on the data book border, fixed, then done with 0 issues and the four reads Check takes. quick-tests PASS, net8.0 2736 passed, net48 217 of the read and chain passed, tests/acceptance 45 passed 5 xpassed.

Remains:
- Task status: done once the Build to the end is seen in Revit on 01A.
- Review 1: the rules index merge, the reasons of a row not found, three copies of code and three new pane words, fixed. The Build to the end on 01A in Revit.

Known bugs:
- A project with no data book stops at the read for the build, as every build step of version 1 takes plots.csv and labels.csv.

Next:
- revit-test's short check on 01A from an empty model once FAST-5 and FAST-6 are on main.

Your turn:
- Nothing.

## FAST-3-revit-test run 1b, 01A from Advanced, 7 Nov 2026

Goal:
- Bader's order of 10:05: on an empty model copy, the old steps 1 to 9 from Advanced on NBH 01A, marked tuned, to prove stages 2 and 3 still build there, faults to steps/seam.md as F18 and up.

Done:
- Two parts in Revit, 10:40 to 12:22 and 12:27 to 14:06, the copy saved at the end of each, Revit closed by the driver, no lock left. Steps 1 to 3 Done on 01A, 278 plots. Faults F18 to F23 in steps/seam.md, the run 1b section of steps/revit-results/FAST-3-revit-test.md.

Checked:
- S1 by SHA-256, size and write time before and after: 37 files, 0 changed, 0 new. The stop at Check against the read folder on disk, which holds no read_baseplan.json, read_villaBlocks.json, read_curbs.json or read_workbooks.json.

Remains:
- Steps 4 to 9 did not run. 4 Check stopped on F16 from Advanced too, Run 2 Read data of Advanced is the data check of card 1. No floor, level, villa link, wall or report built. Lead closed run 1b at 14:10.

Known bugs:
- F16 on the 6 Nov 23:00 add-in. F18, step 2 of Advanced read the input root of the past job, not the input rows. F21, step 1 Done lost over a save and reopen. F22, two reads of about 12 minutes before a neighbourhood scope.

Next:
- Lead's short check once PR 256 (F16) is on main and installed: 01A from a fresh empty copy, card 1 then Build to the end.

Your turn:
- None.

## FAST-7 PR 2, card 1, the gear, How it works, 7 Nov 2026

Goal:
- The polish of run 1 in build-3's files: F1, F3, F7, F9, F17 and the line of F2.

Done:
- R761 and R762: card 1 holds 1 Set up site and 3 Review data, Choose project folder waits for step 1.
- R763: the gear of Settings, Report grey while card 3 waits, the folder dialog of Windows with a path field at the folder chosen before.
- R764: How it works heads its steps with the 3 cards. R765: no setup and a read in the work folder say the setup was not saved. R766: the line beside a grey Apply of Set up site. R767: the check window counts villa plots of all plots.
- Shots in steps/ui-shots, made up values: pane-stage-1, pane-stages-head, how-it-works, setup-site-dry-run-first, scan-results-check.

Checked:
- dotnet/SiteAdaptation.Tests/FAST7, 15 new tests, most with a broken case, the gear and the How it works heads by the good case only. Not seen in Revit.

Remains:
- F2, the Baseplan of the chosen folder for Set up site, by FolderRoles.Of of FAST-5 once on main. F6 and F13 once Lead moves PaneHost.cs and BuildChain.cs.

Known bugs:
- How it works still says 9 steps and 9 buttons in its head, held by NewProjectTests.cs, line in steps/seam.md.

Next:
- F2 Baseplan, then F6 and F13.

Your turn:
- Nothing.

## FAST-7 takes UiLabelTests.cs, 7 Nov 2026

Goal:
- build-3 asked for dotnet/SiteAdaptation.Tests/UiLabelTests.cs, its line 203 holds the count of CHOSEN labels, 517, which each new word of FAST-7 moves.

Done:
- UiLabelTests.cs added to the list of FAST-7-build-3 by exact path.
- ScanResultsWindow.xaml too, for F9, its header of the check window is a fixed text shared with the old scan view of step 2.

Checked:
- grep of steps/tasks: FAST-5 and FAST-6 hold neither file. A new label of build-1 goes to build-3 by steps/seam.md.

Remains:
- Nothing for this change.

Known bugs:
- None.

Next:
- build-3 goes on with F1.

Your turn:
- Nothing.

## FAST-6-build-2 part 1, F10 and F4, 7 Nov 2026

Goal:
- Bader's order of 10:05: F10, the parameter sheet reaches the data check of stage 1, and the Common side of F4, the first Build of a fresh session makes its plan.

Done:
- F10 measured: card 1 on a project with no past file takes the raw read, which skips DataCheckStep, and ReadDataRun.DataCheckRequest builds the request with config, folder and profile only. R750 DataCheckSheet.Input in Common gives the sheet, DataCheckStep.SheetInput calls it, the one call in ReadDataRun is build-1's by steps/seam.md, as Common/ReadData is on FAST-5.
- F4 measured: WorkerRequest.ToJson rounds by DesignValues.Current before WorkerRunner takes the config of the run. R751 writes a request unrounded while no value is known, as the answer does. SiteBuildPlan.Make needs no change.

Checked:
- FAST6 tests, R751 runs Make in Windows PowerShell 5.1 from a copy of the build outside the repo, a revert of the fix gives the R405 text of F4. Second check: PowerShell 7 with CadReader.exe, sharedParameters strong. Quick tests PASS, acceptance 45 passed 5 xpassed, net48 3215 of 3216, the one a launch timing test that passes 3 of 3 alone.

Remains:
- build-1 adds the call of R750 to ReadDataRun.DataCheckRequest with a FAST5 test, then the task is done. Not seen in Revit.

Known bugs:
- WorkerRunTests.R347_an_answer_that_does_not_read_or_a_hand_off_that_throws_never_ends_the_host failed once in a full net48 run, "the worker never ended".

Next:
- revit-test's short check after the merge: the first Build of a fresh session plans, and the data check of card 1 says Shared parameters strong once build-1's call is on main.

Your turn:
- Nothing.

## FAST-7 item 1, the stop box of Build, 7 Nov 2026

Goal:
- F12 of run 1: a Build that stops says the step and the reason in a box, not only at the end of the long line of card 2.

Done:
- R760: StagesViewModel.ShowStop and the box StopBox at the top of card 2, cleared by the next press of Build. The words of SiteBuildText, no new word. Shot steps/ui-shots/pane-stage-2-stopped.png, made up values.

Checked:
- dotnet/SiteAdaptation.Tests/FAST7/R760_StopBoxTests.cs, a stop, a roll back and a cancel show the box, a step that ended well does not, Build clears it. Not seen in Revit.

Remains:
- The one call in PaneHost.cs is FAST-5's, its exact text in steps/seam.md for build-1. Items F1, F2, F3, F7, F9, F17, then F6 and F13.

Known bugs:
- Until build-1 adds the call, the box never shows in Revit.

Next:
- F1, card 1 holds Set up site, now that the label list is on FAST-7.

Your turn:
- Nothing.

## FAST-7 takes the label list, 7 Nov 2026

Goal:
- build-3 asked in steps/seam.md for docs/design/14-ui-labels.md, the new words of F1, F2, F3, F7, F9 and F17.

Done:
- docs/design/14-ui-labels.md added to the list of FAST-7-build-3. FAST-5-build-1 does not hold it, so no lane shares it.

Checked:
- grep of steps/tasks: of the open tasks only FAST-1-build-1 also names it, and build-1 works on FAST-5 today.

Remains:
- Nothing for this change.

Known bugs:
- None.

Next:
- build-3 goes on with F1.

Your turn:
- Nothing.

## Saturday lanes, 7 Nov 2026

Goal:
- Bader's order of 10:05: three build lanes and run 1b of revit-test on 01A, builders test in Revit before the PR.

Done:
- Tasks FAST-5-build-1, FAST-6-build-2 and FAST-7-build-3, files split by exact path. Run 1b and run 2 on 01A added to FAST-3-revit-test.
- guard-revit.ps1 lets the lanes of .claude/revit-lanes.txt open Revit, build-1 and build-3, a test in scripts/checks/test_hooks.py. The line in the fast section of CLAUDE.md and in .claude/rules/revit.md.
- steps/demo-live.md says 01A was used for tuning.

Checked:
- scripts/checks/test_hooks.py, the Revit tests, 56 passed. CLAUDE.md at 10,228 bytes.

Remains:
- The lanes, then the short check and run 2 at 23:30.

Known bugs:
- F16, F4, F10 and the polish list of steps/seam.md, now in the three tasks.

Next:
- Lead starts the four sessions and reviews each PR.

Your turn:
- Nothing.

## Run 1 of FAST-3 and the Saturday plan, 7 Nov 2026

Goal:
- Bader's order of 21:10: by 08:00, what the run showed, the fix list by lane, the Saturday order of work.

Done:
- Run 1 from 01:11 to 06:37 in three parts, PR 249 of revit-test. Stage 1 works from the pane, stage 2 built nothing, F16 the main fault. steps/plan-fast-3days.md holds the findings, the fix list by lane and the order of Saturday.

Checked:
- Every shot of PR 249 looked at by eye: the coordinates line, plot outlines, plot numbers and areas greyed, the names shown are in config.

Remains:
- FAST-5 and FAST-6 tasks at 08:30, then the lanes.

Known bugs:
- F16, F4, F10, F8 of steps/seam.md.

Next:
- Lead writes the tasks and starts build-1 and build-2.

Your turn:
- F15: the neighbourhood for the villa links of run 2, else 01A at 10:00.

## FAST-3-revit-test run 1, the one test in Revit, 7 Nov 2026

Goal:
- Bader's order of 6 Nov 21:10: one run from an empty model through the 3 cards on S1, a neighbourhood not used for tuning, real villa links, the fix list by lane, a shot per card and per window, the demo script.

Done:
- Revit opened 01:11, three parts of at most 2 hours, the model copy saved at the end of each. Stage 1 read S1 four times. Card 2 Build ran the one rules ask, the preview and the chain to 4 Check on NBH 010. The fix list F1 to F17 in steps/seam.md. 13 shots in steps/ui-shots with real values blanked. steps/demo-live.md.

Checked:
- S1 by SHA-256, size and write time before and after: 37 files, 0 changed. The plot counts of the windows against plots.json by a script. The read inputs by its read.json.

Remains:
- The site was not built: every Build stopped at 4 Check, F16. Floors, levels, villas, walls and Report not seen. The run of Sun 8 Nov.

Known bugs:
- F16, step 4 needs the outputs of the old step 2 that the read of card 1 never writes. F4, R405 on the first Build of a session. F15, the untuned NBHs hold no villa type.

Next:
- Lead's plan of 08:00 on F16, F4, F1 and F5. The second run on Sun 8 Nov.

Your turn:
- None.

## The listener script sets no floor, 7 Nov 2026

Goal:
- Lead's order of 01:30, FAST-1-build-1 item 8 last line: take SITEADAPT_LISTENER_MIN_FREE_GB out of Start-RevitListener.ps1 now that FAST-2 PR 242 is on main (Q570).

Done:
- The line was out on branch task/FAST-1-build-1-listener since 1411919, origin/main merged in with no conflict. A test, FAST1/R701_ListenerScriptTests.cs, keeps it out.

Checked:
- dotnet test of R448_RevitListenerTests, TestHostTests, InstallScriptTests, ParsonsModelTests and the new test: 231 passed on net48 and 231 on net8.0. pwsh scripts/quick-tests.ps1: 154 pytest and 2733 dotnet passed. git grep: the script at 1411919^ holds the floor string once, the TestHost scripts at HEAD hold none.

Remains:
- docs/rules R448 text still names SITEADAPT_LISTENER_MIN_FREE_GB as an input, a docs line for Lead.

Known bugs:
- None new.

Next:
- Lead merges on green. Not seen in Revit, this lane opens no Revit.

Your turn:
- None.

## The Teams window is no meeting, 7 Nov 2026

Goal:
- Bader's order of 00:16: the Teams window that keeps the laptop awake does not stop the Revit driver.

Done:
- The line in FAST-3-revit-test and .claude/rules/revit.md. The plan holds the wait from 23:43 to 00:16, revit-test told at 00:16, and the refused driver launch of 00:44.

Checked:
- pytest python/tests -k "r26 or r400 or log": 50 passed.

Remains:
- The first run of FAST-3: blocked since 00:44 on the permission check of the revit-test session, Bader to allow the driver launch.

Known bugs:
- None new.

Next:
- Lead reads steps/seam.md every 15 minutes.

Your turn:
- None.

## Always Load on our own box, 6 Nov 2026

Goal:
- Bader's order of 23:37: the Revit driver presses Always Load on the Unsigned Add-In box of Site Adaptation.

Done:
- The rule in FAST-3-revit-test, .claude/rules/revit.md and CLAUDE.md, one press, the time to steps/revit-results, every other box keeps the driver rule, a box of another maker untouched. SETUP-sign-2 item 1: a new build of the DLL brings the Load Once box back. Sent to the running revit-test session as a message, not restarted.

Checked:
- CLAUDE.md 10,217 bytes, under 10,240. pytest python/tests scripts/checks -k "r26 or r400 or log or claude or task or harness": 161 passed.

Remains:
- The first run of FAST-3: blocked since 00:44 on the permission check of the revit-test session, Bader to allow the driver launch.

Known bugs:
- None new.

Next:
- Lead reads steps/seam.md every 15 minutes.

Your turn:
- None.

## The night of 6 Nov: lanes stopped, add-in installed, 6 Nov 2026

Goal:
- Bader's order of 21:10: the one test runs tonight. Merge what is open, stop the builders, install, start revit-test.

Done:
- Merged PR 237, 238, 240, 241, 242, 243, 244. FAST-2 and FAST-4 done, build-1 stopped with FAST-1 open. Add-in built from main 14044dd and installed at 23:00. revit-test folder on task/FAST-3-revit-test.

Checked:
- Every PR green on the tests job before its merge. The installed SiteAdaptation.AddIn.dll is the 23:00:40 build of 14044dd. No process of the hidden builder sessions left.

Remains:
- The first run of FAST-3 from 23:30, and its fix list by 08:00 Sat.

Known bugs:
- R310 and R347 failed at random under load until PR 242. R444 failed once on CI.

Next:
- revit-test starts 23:30.

Your turn:
- Open Revit 2024 once, press Always Load, close it.

## FAST-1-build-1 PR 7, Q504 on card 3, 6 Nov 2026

Goal:
- Item 6 of FAST-1, Q504, Bader's answer 5A of 6 Nov: the card shows the count of the plots with no road level that the plan lists.

Done:
- StageResults.NoRoadLevelCount counts the issues of the plan with the words no road level, those of R705 of FAST-2. Card 3 says "<n> plots have no road level, so no floor level, and steps 6 to 8 leave them out." R691.

Checked:
- R691 with R689, R688 and the labels 16 of 16 on net48.

Remains:
- The listener floor line, once FAST-2 drops its refusal, Q570.

Known bugs:
- Not seen in Revit.

Next:
- Stopped by Bader's order of 21:10. The listener line on Saturday, item 7 moved to FAST-3. Final statement in steps/heads-up/build-1.md.

Your turn:
- None.

## FAST-2-build-2 PR 4, 6 Nov 2026, no RAM floor in the worker and the listener, the CIM limit of the R310 tests

Goal:
- Items 6 and 9 of FAST-2.

Done:
- R701: the worker, AddinConfig and the Revit listener hold no floor of free memory, Q570 CHOSEN. siteBuild.minFreeGB and placeSettleMinutes are out of config, the status block written again.
- R707: the R310 tests take testHost.cimTestTimeoutSeconds, 50 s CHOSEN, from the CIM times this PC measured. A run keeps cimTimeoutSeconds 2.

Checked:
- The CIM calls timed on this PC, two runs of 30, the raw numbers in the work root. The tests of the head in the work root file of the PR.

Remains:
- The variable of Start-RevitListener.ps1 is build-1's, the listener no longer reads it. The tests of Run-TestHost.ps1 and Run-Parallel.ps1 assert the lines without the floor since PR 237.

Known bugs:
- Listing the processes takes 11 to 36 s on this PC, so a run's sample of the children of Revit is often written as not read.

Next:
- The last PR sets the task done.

Your turn:
- build-1: the floor variable of Start-RevitListener.ps1.

## FAST-1-build-1 PR 6, card 3 Review and deliver, 6 Nov 2026

Goal:
- Item 5 of FAST-1: the issues count, the source mix bar, Report and Update from report, and point 1 of R2-C3, the rules table from a rule issue.

Done:
- dotnet/SiteAdaptation.AddIn/Pane/StageResults.cs reads build_plan.json of the newest build folder of the work folder: its issues, and the source of each plot value, the tags of R703, counted as computed, rule, person entered, missing and other. The closing messages of the steps are no issues, card 2 says them. R689.
- Card 3 shows the count, Open rules when an issue names a rule, the bar and its line, Report and Update from report, which runs the command of its ribbon button. R690.

Checked:
- R689 3 of 3, with R680, R686, R688, the labels and the shots 39 of 39 on net48 after review 1. pane-stage-3.png rendered.

Remains:
- Item 6 Q504, item 7 a shot of each window.

Known bugs:
- Not seen in Revit.

Next:
- Item 6, Q504.

Your turn:
- FAST-3-revit-test opens card 3 in Revit after a build, label revit-later.

## Rules R730 to R732 and R688, FAST-3 opens tonight, 6 Nov 2026

Goal:
- Enter the rule lines of FAST-4, which is done, and keep the plan true.

Done:
- docs/rules/20-fast4-r730-up.md with R730 to R732 from the seam line of build-3, index.md. Seam line given. Plan: FAST-4 done by PR 240.
- R688 of PR 238 in docs/rules/18-fast-pane-r680-up.md. FAST-3-revit-test open, Bader's order of 21:10: a first run tonight, a second on Sun 8 Nov, the window shots of FAST-1 item 7 moved to it. The plan holds the night.

Checked:
- pytest scripts/checks -k "r12 or rules or index or r26": 22 passed. rules_docs.py PASS.

Remains:
- R689 to R691 of FAST-1 once their PRs are on main. The install at 23:00 and the start of revit-test at 23:30.

Known bugs:
- R310, R347 and R444 fail at random under load, FAST-2 item 9 measures it.

Next:
- PR 237, 238, 239 merge on green.

Your turn:
- None.

## FAST-1-build-1 PR 5, the one Build runs the steps, 6 Nov 2026

Goal:
- Item 4 of FAST-1, second part: one Build runs set up site, check, floors and ramps, levels, villas and walls as one run with one progress line.

Done:
- dotnet/SiteAdaptation.AddIn/Pane/BuildChain.cs runs the steps 1, 4, 5, 6, 7 and 8 one after another, each by RequestRun as a press of its row, through the step code of version 1. R688.
- Buttons.ChainEnded hands the end of each step to the build, card 2 says Step <n> of <n>, the build stops only on a step that did not end well and says where and why, the messages of the steps that ended well are said at the end.
- Review 1: set up site runs only when it has not run, and a step that waits on an earlier one runs that one first, so a build after set up site reads and reviews again before check.

Checked:
- R688 6 of 6, with R686, R680 and the labels 26 of 26 on net48.

Remains:
- Item 5 stage 3, item 6 Q504.

Known bugs:
- The windows and confirm boxes each step of version 1 opens still open during the build. Not seen in Revit.

Next:
- Item 5, card 3.

Your turn:
- FAST-3-revit-test runs Build in Revit on a copy, label revit-later.

## FAST-1-build-1 PR 4, no RAM floor in two test host scripts, 6 Nov 2026

Goal:
- Item 8 of FAST-1, Bader's order of 6 Nov, FAST-2 item 6: the free memory floor goes from the test host scripts.

Done:
- Run-TestHost.ps1 calls Get-WindowPeaks with no -FloorGB, its memory line drops the floor words and keeps the least free memory.
- Run-Parallel.ps1 has no -MinFreeGB and no start gate on free memory. StopBelowGB stays, a stop and not a start floor, Q570.

Checked:
- Both scripts parse with no error. net8.0 TestHostTests, ParsonsModelTests and R448: 213 of 213.

Remains:
- The SITEADAPT_LISTENER_MIN_FREE_GB line of Start-RevitListener.ps1 goes once FAST-2 drops RevitListenerSetting.Refusal, Q570.

Known bugs:
- None seen. Not run in Revit, this lane may not open it.

Next:
- Item 4 part 2, the steps as one run.

Your turn:
- None.

## ReadEdgeTests.cs to FAST-2, rules R686 and R687, 6 Nov 2026

Goal:
- Let build-2 finish item 6, no RAM floor, and enter the rule lines of FAST-1 PR 3.

Done:
- dotnet/SiteAdaptation.Tests/ReadEdgeTests.cs on FAST-2 by exact path, for its two R347 floor tests. Seam line given.
- R686 and R687 in docs/rules/18-fast-pane-r680-up.md and index.md, from the body of PR 234.

Checked:
- No path of FAST-1, FAST-2 and FAST-4 lies in or holds another but steps/seam.md and steps/log.md, by check_common.inside.

Remains:
- R688 once PR 238 is on main.

Known bugs:
- R310, R347 and R444 fail at random under load, FAST-2 item 9 measures it.

Next:
- PR 235, 237, 238 merge on green.

Your turn:
- None.

## FAST-4-build-3, the plot read and the mirror, 6 Nov 2026

Goal:
- Items 1 and 2 of FAST-4: the plot table holds the villa plots and lists the rest with the reason, the mirror from the block scale, its footprint and the pair trace.

Done:
- R730 plots.json plots holds the plots a villa goes on, the rest in otherPlots with the reason. R731 a block goes to every plot its outline covers, the hand turns at each house along it, CadScan writes the outline and axes of each insert. R732 a pair with no block is traced and said, its hand left missing.

Checked:
- S1 on main 4c11b72 then on f153f14: plots read 2964 to 2618, 346 listed apart, numbers right 1873 of 1891 kept, mirror right 448 to 735 of 1891. S2: 2964 to 2632, 332 apart, mirror 0 to 0, 802 plots traced into pairs. A shapely route on the plain blocks.json gives the same hand on 2079 of 2079 plots. FAST4 tests 7 of 7, quick tests and tests/acceptance pass.

Remains:
- 2618 is not 1891: 745 plots read with a villa sign are not in the answer, which leaves the hold-out areas out, and 18 plots of the answer are not read, as before. The tool cannot tell a hold-out area. 499 plots under blocks of two layers the scan does not pick as villa block layers get no block hand.

Known bugs:
- Not seen in Revit, no Revit file touched. ParsonsModelTests R310 failed once under the 24 GB scenario run and passed alone on this branch and on main.

Next:
- Lead takes the rule lines R730 to R732 from steps/seam.md into docs/rules.

Your turn:
- None.

## FAST-2-build-2 PR 3, 6 Nov 2026, Update from report on a new project, no road level, wall thickness, the docked page

Goal:
- Items 3, 7, 8 and 10 of FAST-2.

Done:
- R704, Q394 CHOSEN: on a new project Update from report saves a plot with no override entry as a new entry, the Excel its model, no key.
- R705, Q504 5A: each plot with no floor level is one issue with the words no road level, steps 6 to 8 skip it.
- R706, Q149 CHOSEN: the plan gives each plot the boundary wall thickness from rule M14. Q336 held by a test, the link group stays Text.
- Item 10: AddinRunNames.DockedPage names the page of the stages.

Checked:
- FAST2 tests, the SiteBuild tests, R402 and the Python update check, output in the work root file of the PR.

Remains:
- Item 6 worker and config part, item 9.

Known bugs:
- None found. Not seen in Revit, the window of Update from report was not opened.

Next:
- Item 6, then item 9.

Your turn:
- build-1: AddinProbe.DockedPage may take AddinRunNames.DockedPage, steps/seam.md.

## FAST-1-build-1 PR 3, the plan behind Build and the 2D preview, 6 Nov 2026

Goal:
- Item 4 of FAST-1, first part: Build makes the plan of siteBuild, says the scope, the coordinates and a stop, and shows the 2D preview.

Done:
- dotnet/SiteAdaptation.AddIn/Pane/SiteBuildPlan.cs runs siteBuild of Common in process over the latest read of stage 1 and the rules of the project. SiteBuildFlow asks once for the manual template when no rules are chosen, stops on a blocking line in plain words, opens SiteBuildPreviewWindow when Preview first is ticked. R686, R687.
- Card 2 shows the scope in the words of the project and Build is on once stage 1 has a read. An empty card line folds.

Checked:
- R686 6 of 6 on net48, 85 of 85 with the tests of the pane, rules, labels, shots, install and siteBuild. build-preview.png and pane-stage-2.png rendered.

Remains:
- Item 4 second part: Build runs set up site, check, floors and ramps, levels, villas and walls as one run with one progress line, through the step code of version 1.

Known bugs:
- Until then card 2 says the steps 4 to 8 run from Advanced. Not seen in Revit.

Next:
- The chain of steps, then item 8 once FAST-2 PR 233 is on main.

Your turn:
- FAST-3-revit-test opens Build and the preview in Revit, label revit-later.

## Rules R682 to R685 and three CHOSEN answers, 6 Nov 2026

Goal:
- Enter the rule lines of FAST-1 PR 2 and record the answers the lanes took by Bader's order of 6 Nov.

Done:
- R682 to R685 in docs/rules/18-fast-pane-r680-up.md and index.md, from the body of PR 232. The R680 and R681 bridge line of R640_WorkerSeamTests.cs removed, R680_StagePaneTests.cs names both on main.
- Q394 and Q149 CHOSEN by build-2 in PR 235, Q570 CHOSEN by Lead for the listener floor, in questions-closed.md and the plan. Q394 out of questions-open.md. The seam line to build-1 and build-2 marked given.

Checked:
- pytest scripts/checks -k "r12 or rules or index": 22 passed.

Remains:
- PR 234 of build-1 and PR 235 of build-2, review and merge on green.

Known bugs:
- R310 and R347 fail at random, FAST-2 item 9.

Next:
- FAST-2 drops RevitListenerSetting.Refusal, then FAST-1 item 8 on Start-RevitListener.ps1.

Your turn:
- None.

## FAST-2-build-2 PR 2, 6 Nov 2026, the source of each value in the report, no past share on a new project

Goal:
- Item 2 of FAST-2, and the small items 6 step 1 and 12.

Done:
- R702: a report of a new project prints no past share or strength, and lists no weak rule.
- R703: Plot schedule, Floors and Walls each have a column Source with the sources of the row.
- The Python second check report_excel_check.py does the same. ParsonsModelTests.cs takes the script line with or without the floor. The Plot row of the parameter sheet has its note.

Checked:
- ReportExcelTests, R402_UpdateFromReportTests, FAST2 and ParsonsModelTests on net8.0: 93 of 94 passed, the one failure was the new regex of ParsonsModelTests.cs, fixed, then it and FAST2 passed 10 of 10. The Python report check tests pass.

Remains:
- Items 3, 6, 7, 8, 9, 10.

Known bugs:
- The check table keeps from and use as its source words, not the words computed, rule, person entered.

Next:
- Item 3, Q394.

Your turn:
- build-1: the floor lines of Run-TestHost.ps1, Start-RevitListener.ps1 and Run-Parallel.ps1 can go, the tests take both.

## FAST-1-build-1 PR 2, the rules of a project and the parameter sheet, 6 Nov 2026

Goal:
- Item 3 of FAST-1, Settings and the rules per project, and item 2b, stage 1 sends the parameter sheet.

Done:
- dotnet/SiteAdaptation.AddIn/RulesWindow/ProjectRulesWindow.xaml, the rules table of a project from Settings, Rules, Open. Confirm or Save runs rulesChange, Use the manual template runs rulesSetup, Before Build warn or block, all by WorkerRunner.Run in process. R682.
- One amber line on the pane until every rule is confirmed, R683. The install ships rules/templates/manual.json, R684. DataCheckStep.SheetInput sends the parameter sheet, R685.

Checked:
- R682 6 of 6, R685 1 of 1 on net48. project-rules.png rendered by SiteAdaptation.UiShots.

Remains:
- Point 1 open from a rule issue goes with the issues list of item 5. Point 3 the first Build asks once goes with the Build of item 4. Item 8 waits for FAST-2.

Known bugs:
- Not seen in Revit.

Next:
- Item 4, the one Build button.

Your turn:
- FAST-3-revit-test opens the rules table in Revit, label revit-later.

## A third builder and the seam of 16:50, 6 Nov 2026

Goal:
- Bader's order of 16:25: go faster, a third builder, the seam answered, main merged into open branches.

Done:
- FAST-4-build-3 open on lane build-3: plots read and mirror, items 4 and 5 of FAST-2, with Common/DataCheck/, four CadReader files, the scenario scripts. FAST-2 lists its Common and CadReader files by name without them, plus three test files for item 6, and item 12, the Plot note. FAST-1 gains item 2b parameterSheet, item 8 the floor lines of the TestHost scripts, InstallScriptTests.cs. Seam lines marked given.

Checked:
- No path of FAST-1, FAST-2 and FAST-4 lies in or holds another but steps/seam.md and steps/log.md, by check_common.inside.

Remains:
- build-3 starts hidden once this merges.

Known bugs:
- R310 at random until FAST-2 item 9.

Next:
- START build-3 FAST-4-build-3.

Your turn:
- None.

## FAST-1-build-1 PR 1, the pane in 3 stages and stage 1 wired, 6 Nov 2026

Goal:
- Items 1 and 2 of FAST-1: the pane in 3 stages, Choose project folder, the data check rows, Fix items.

Done:
- dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml docks 3 cards, each with status, i icon and one button, a Settings gear and Advanced closed over the pane of version 1. Choose project folder writes inputRoot of a new project's config copy then runs 2 Read data, Fix items opens the Confirm window of R2-C1. R680, R681.
- The 4 failing tests of the handover fixed. pane-stage-1, -2 and -3 rendered by SiteAdaptation.UiShots into steps/ui-shots/.

Checked:
- pwsh scripts/quick-tests.ps1: pytest 154 passed, dotnet 2706 of 2706 on net8.0 before review 1. R680 7 of 7 on net48 after the review of Lead. UiLabelTests count 466.
- Review 1 and the review of Lead fixed: a stopped read says why, card 3 words, Fix items says a failure, the fresh pane line, the plain line of each need, the Revit test host finds StagesPage.

Remains:
- Items 3 to 7: Settings and the rules, the one Build, stage 3, Q504, the shots of each window.

Known bugs:
- Card 2 Build is grey and says so until item 4. Not seen in Revit.

Next:
- Item 3, Settings and the rules per project.

Your turn:
- FAST-3-revit-test opens the pane in Revit, label revit-later.

## Plot on floors and the seam of 16:20, 6 Nov 2026

Goal:
- Record Bader's answer on Plot on floors and give the seam lines of the hour.

Done:
- Q560 closed, Bader's answer A: Plot stays on floors as the tool's own tag, every other shared parameter on villa links only. FAST-2 gains item 10, AddinRunNames.Page of Common names StagesPage, the seam of build-1, and item 11, Q560. steps/plan-fast-3days.md holds the list of questions Lead closes with its own answer.

Checked:
- Q560 free by git grep. PR 229 merged, PR 228 fixed and in CI.

Remains:
- FAST-1 items 3 to 7, FAST-2 items 2 to 11.

Known bugs:
- R310 at random until FAST-2 item 9.

Next:
- The 17:00 slip line for Sat 7 Nov.

Your turn:
- None, Bader's order: Lead does not wait on him.

## FAST-2-build-2 PR 1, 6 Nov 2026, shared parameters and the Python side of no RAM floor

Goal:
- Items 1 and 6 of FAST-2: the data check marks shared parameters from the Excel sheet, no free memory floor.

Done:
- R700: the worker reads the parameter sheet the dataCheck request names as input parameterSheet, the need sharedParameters is strong when every villa link row has a GUID and says in plain words what is missing when not.
- R701: python/window_lock.py, c3d_window.py and place_codes.py no longer wait, skip or stop on free memory.

Checked:
- S1 by this build, run outside git: sharedParameters strong, 10 parameters for the villa links, the sheet unchanged before and after. R700 and R600 tests 41 of 41 on net8.0.

Remains:
- Items 2, 3, 4, 5, 7, 8, 9. Item 6 worker and config part. Item 1 floors part.

Known bugs:
- Floors still carry the Plot parameter, steps/seam.md.

Next:
- Item 2, the source mark per value.

Your turn:
- Lead: the two seam lines of build-2, the item 6 test files and the floor Plot parameter.

## FAST-1 seam given, 6 Nov 2026

Goal:
- Give build-1 its seam line and restart it as one session.

Done:
- dotnet/SiteAdaptation.Tests/UiLabelTests.cs on the list of FAST-1. R680 and R681 in docs/rules/18-fast-pane-r680-up.md. Opening the windows in Revit moves to FAST-3, the Revit guard holds. A comment names R680 and R681 for test_r12 until their tests merge.

Checked:
- No other open task holds UiLabelTests.cs. rules_docs passes.

Remains:
- FAST-1 fixes its 4 failing tests and opens PR 1, label revit-later.

Known bugs:
- Two sessions ran in build-1 at once from 14:15, no work lost, the hidden one made no edit.

Next:
- build-1 restarted from ebb346e.

Your turn:
- Open no session in a lane folder Lead runs hidden, say so to Lead instead.

## Lanes started hidden, 6 Nov 2026

Goal:
- Bader's order: Lead starts the builders hidden, he opens no terminal.

Done:
- scripts/lanes/run_lane.py takes --effort, and gives the lane CLAUDE_PROJECT_DIR of its folder and the bin of Git Bash first on PATH. build-1 on FAST-1-build-1 and build-2 on FAST-2-build-2 run hidden, opus, effort high.

Checked:
- Both logs show the hook found the lane and its task, model claude-opus-5-5, no tool error. Both task branches on origin.

Remains:
- FAST-1 and FAST-2 build.

Known bugs:
- R310 at random until FAST-2 item 9.

Next:
- The lanes' first PRs.

Your turn:
- None.

## Bader's form answered, 6 Nov 2026

Goal:
- Do what Bader's answers to steps/bader-form-6nov.md ask.

Done:
- 1A: the Confirm window approved, D-46 closed. 2A: the account name of the PC gone from 10 files under steps/, and a check by its hash. 3A: Bader's own. 4A: ParsonsModelTests.cs on the list of FAST-2 by exact path, with a scope item. 5A: FAST-1 and FAST-2 skip steps 6 to 8 for a plot with no ffl and list each as no road level. Q520 and Q504 closed.
- Every open or waiting task, the task skill and the task shape of the round skill end with the line of the final statement.

Checked:
- git grep finds the account name in 0 files. The new check passes, its stored hash equals the hash of the account of this PC.

Remains:
- FAST-1 and FAST-2 build. FAST-3 on Sun 8 Nov.

Known bugs:
- R310 at random until FAST-2 item 9.

Next:
- The lanes' first PRs.

Your turn:
- 3A: press Always Load once by hand and ask IT for a company certificate.

## Fast build set up, 6 Nov 2026

Goal:
- Bader's order of 6 Nov 12:30: three days build the product first, test at the end, the managers see it on Mon 9 Nov.

Done:
- CLAUDE.md has the section Fast build, 6 to 8 Nov. R2-C3, R2-C4 and R2-C2 revit-test folded into FAST-1-build-1. Tasks FAST-1-build-1, FAST-2-build-2 open, FAST-3-revit-test waiting. steps/plan-fast-3days.md, steps/seam.md, steps/heads-up/, steps/bader-form-6nov.md.

Checked:
- No path of FAST-1 lies in or holds a path of FAST-2 but steps/seam.md and steps/log.md, which both lanes write. Rule and question ids free on main by git grep.

Remains:
- FAST-1 and FAST-2 build. FAST-3 on Sun 8 Nov.

Known bugs:
- R310 tests fail at random on this PC, the stop hook blocks, form item 4.

Next:
- START build-1 FAST-1-build-1, START build-2 FAST-2-build-2.

Your turn:
- steps/bader-form-6nov.md, five items.

## Recovery, 5 Nov 2026, after the PC shut down by mistake that evening

Goal:
- Check the damage of the shutdown, save what was not pushed, carry on the queue. No new tool code.

Done:
- Bader gave about 19:15, the last commit made on the PC is build-1 608d219 at 19:24 by its reflog, so it shut down after 19:24. Restarted about 21:00. Lost: the lane sessions of build-1 and build-2 and Lead's loops.
- 608d219 pushed from build-1. PR 213 the R639 rule line, one clause made to say what the code does. PR 211 merged after S1 and the hold-out held. R2-C3-revit-test opens.

Checked:
- No git lock, no OneDrive conflict copy, no window lock or listener job held, no Revit or worker left running, no run cut. Quick tests on main pass, 154 pytest and 2524 dotnet.
- S1 on PR 211: numbers 1873, villa type 233 and 1859 confirmed. Hold-out 610, 606, 146.

Remains:
- R2-C1-fix-build-1 opens its PR, then R2-C2-build-1. R2-C3-revit-test.

Known bugs:
- A build-1 session Bader opened ran S1 at the same time as Lead's, both wrote runs\S1 at 21:58, both void and redone one at a time.
- R310 fails under load, steps/latent.md.

Next:
- The Friday plan of 6 Nov.

Your turn:
- Q520, the account name in public files. Nine old agent folders under .claude/worktrees from 28 Oct to 4 Nov are not registered worktrees, keep or remove.

## setup-lanes, 2026-11-04, standing rules into files, hooks and checks

Goal:
- A round prompt is one line. No tool feature work

Done:
- CLAUDE.md, docs/lessons.md, .claude rules, agents, skills, hooks, tests.yml, scripts/status.ps1, steps/backlog.md, SETUP-sign task

Checked:
- Self tests in scripts/checks, hooks run in made up lane folders. The PR holds the counts

Remains:
- Clean folder, golden and hold-out checks say NOT RUN, nothing to score yet. Required check on main not turned on

Known bugs:
- Bash writes pass the Edit hook, the folder rules check catches them on the PR. A helper worktree needs its own task

Next:
- /questions, then /round R1

Your turn:
- Opened: none. Closed: none
- Open count and titles: steps/questions-open.md

## Lane C TRUTH-4NOV-C, 2026-11-04, what was computed and what was copied

Goal:
- Answer the managers with facts: what the tool computes and what it copies from the past job. Read only

Done:
- steps/truth-audit.md and steps/questions-for-bader-4nov.md, 197 questions, 9 changed, 20 affected, none closed

Checked:
- 13 read helpers. Worker steps 2 and 4 on copies in truth_4nov, 43 originals, 0 changed. Plan counts by script, a sample opened by hand. Revit not used. Most line numbers not rechecked

Remains:
- Tool does not run from raw inputs: step 2 stops with no schedule, step 4 with no plots.csv or keyRules. 322 of 336 plots took past values in the dry run

Known bugs:
- BW thickness not set. Report prints past shares on a new project. Q439 fallback model stops at step 5. None fixed, this round changes no code

Next:
- Bader answers Q441

Your turn:
- Opened: Q441 Order of the raw input changes. Closed: none
- Open count and titles: steps/questions-open.md

## Lane C ROAD-TICK, 2026-11-04, the tick of Lane D in the pane

Goal:
- Add the Road topography tick Lane D asked for under 5 Floors and ramps, R574

Done:
- The tick on the real step 5 row only, outside the row so a press does not run the step. Guide and help line

Checked:
- Tests R574 in PaneTests and UiShotTests, seen in the pane image of UiShots. Not seen in Revit

Remains:
- Revit look of the tick. Browse not measured

Known bugs:
- None found. R310 process sample failed once under the full net48 run, passed 3 of 3 alone

Next:
- TRUTH-4NOV-C

Your turn:
- Opened and closed: none
- Open count and titles: steps/questions-open.md

## Lane D TOPO-3NOV-D, 2026-11-04, road toposolid of button 5

Goal:
- Bader: a toposolid from Top Surface-02 cut to the NBH, ramps and road edges meet it, difference reported per ramp road edge, target under 5 mm, type named in settings, tick on by default

Done:
- R570 to R575. RoadTopography.cs, FloorsRun after the floors commit, toposolid_<NBH>.json. R575 takes the toposolid and floor overlap with no box, found by the Revit check. Tick text goes to Lane C

Checked:
- Revit, RAN, NBH 012, copy of SA-dryrun3nov-done.rvt, before RAMP: toposolid made, floors unchanged, second press keeps it, plot list leaves it, undo and redo
- Revit, RAN, with RAMP and R575, copy of SA-dryrun3nov.rvt with no floors: no box, 1,003 floors and 9,728 points, run 117.2 s, commit 100.4 s, 5,713,920 to 16,592,896 bytes. One undo takes floors and toposolid back
- Second method RAN: own walk over the triangles Revit holds, 1,008 points, 0 misses, gap to the report at most 0.05 mm. Ray 0.06 mm in the first run. Ramp tops read from Revit against the plan at most 0.03 mm
- Parity skipped: no Python, no plan or Excel number changed. Breaker 1 before R575, not run again

Remains:
- Road edges: 240 corners with no override at most 0.51 mm in the first run. Over 5 mm: 80 plots at corners, all with rampFrontM, Q472. 15 no override plots at the middle, Q475

Known bugs:
- The toposolid lines of a real press reach no window, Q476. SA-dryrun3nov-done.rvt on the config of main stops at step 5, 1,003 outlines changed, R378, Q439 of Lane A

Next:
- Lane C adds the tick. Bader's answers

Your turn:
- Opened Q472 Ramp front overrides stand off the road. Q473 The chosen values of the road toposolid. Q474 The road toposolid stands as a tall block. Q475 The road bends between ramp front corners. Q476 What a real press shows of the toposolid. Q477 A plot list press sends the pane to step 2.
- 196 open, steps/questions-open.md

## Lane A RAMP-3NOV-A, 2026-11-04, ramp corners and hairline overlaps

Goal:
- Bader: shared ramp corners within 5 mm take one level, the 443 overlaps gone, count on NBH 012, target 0 for both

Done:
- R470 joint on. R515 back corner, road at front plus 0.5%, CHOSEN by Code. R516 joint only with no override. R517 every floor 2 mm inside. R518 check reach

Checked:
- Revit, RAN, NBH 012, buttons 1 to 5: new project 0 two plot overlaps, 0 of 555 shared corners over 1 mm, 672 floors. Past job 2 overlaps from 443, 360 corners unchanged, joint off, 1,003 floors. Inputs 262 same
- Parity 624 step runs, 0 failures. Gate, CI. TU zones 1,759 of 1,791. Review 1, breaker 1

Remains:
- Target met on a new project. Past job misses it, Q430, Q432. C10 steps of up to 83 mm beside a neighbour are outside 5 mm, Q438

Known bugs:
- Same plot warnings are in the saved model, 481 and 176, accepted warnings return (Q385)

Next:
- Bader's answers

Your turn:
- Closed Q340 The level of a joint an override moves. Q341 Back corners two ramps share. Q398 The 443 hairline floor overlaps at step 5. Opened Q430 Past job keeps its shared corner gaps. Q432 Two plot overlaps left after the pull. Q433 2% units in mixed rows drop to 0.5%. Q436 Generic floor no longer the whole face. Q437 One hand entry turns the joint off. Q438 C10 ramp beside a neighbour. Q439 A model built before the pull.
- 190 open, steps/questions-open.md

## Lane B Phase 6b, 2026-11-04, the buttons on all 7 zones in Revit

Goal:
- 7 zones in Masses, one NBH in Links, one from None, button 1 on the IMD copy, compare tables, 10 random plots by id. Bader: the Revit pass of HELP

Done:
- All 7 zones through 1 to 9, Links and None on IMD, steps/investigations/6b-revit.md
- R557: 5 of 7 NBHs stopped at button 2 with no official number. R558: the help box ran off the screen on all 9 steps. R556: a buttons job from no template
- R541 of Lane C first run to its end. Step 1 help line after COORD

Checked:
- Built models read back by the reader of the keys: 2,524 links, 7,249 floors, 6,029 walls by id, 0 faults, 70 random plots the same. 0 inputs changed in 11 runs
- Parity skipped: R557 feeds scope only, no plan builder step reads it, CHOSEN. R558 seen in Revit before the last review fixes, the cap key after them not seen

Remains:
- Q069. Report file name section not seen. Browse time not measured. No person looked at the plots. Rev 6 on the zones not run

Known bugs:
- Plot 1995 of NBH 022 has a villa on an open space strip (Q455). A plot list drops a plot not in the plan, 34 units (Q456). A Revit job of mine had to be ended by Bader, my stop of a wrong script took its helpers
- Totals per NBH differ by what they count: 2,569 faces with a type, 2,558 scope units, 2,591 plan plots, 2,524 written

Next:
- Lane A RAMP, Lane D TOPO, whoever ends last installs main

Your turn:
- Opened: Q452 Button 1 on a past model. Q453 More overlap warnings from no template. Q454 Report name section on the demo config. Q455 A villa on an open space strip. Q456 Plot list with a plot not in the plan. Closed: none
- Open count and text: steps/questions-open.md

## Lane C R541 Revit pass, 2026-11-04, the rules table seen in Revit

Goal:
- Record the pass Lane B ran on the Lane C drivers, real presses on a copy, past job

Done:
- Q440 closed. Wall top 778 walls.top differ and no other field, reset 0 differ apart from 3 run and hash fields. Amber seen, step 4 back to Ready, 5 to 8 Needs step

Checked:
- Lane B result lines, build of main at 57b548e. Ramp limit 0 fields differ on 336 plots, so the asked proof by ramp limit did not show a change, wall top is the proof, Q419

Remains:
- Rev 06 not run in Revit. Report file name section not seen, C:\SA-demo\config has no report name block. Browse not measured. Road topography tick

Known bugs:
- None open from this pass. The i box off the screen is R558 on check/lane-b-6b, not seen in Revit

Next:
- Road topography tick after topo/3nov-d

Your turn:
- Closed: Q440 Rules table not yet proven in Revit. Opened: none
- Open count and titles: steps/questions-open.md

## Lane A COORD-3NOV-A, 2026-11-04, a model with no site coordinates

Goal:
- Bader: button 1 works on a model with no shared coordinates, position from the picked CAD, file only as a check, never set silently

Done:
- R510 detects it, says This model has no site coordinates. R511 CAD is the default of a new project. R512 CAD and file side by side in mm. R513 no usable CAD asks for a file or typed values, Apply off. R514 site.json positionSource, pane text with Lane C. R523 no move with placed content, Q420
- Review 1 and breaker 1: a silent fall back to the file, a typed slip passing every check, a source held before the commit, no retry of the CAD read, each fixed with a test

Checked:
- Gate on both runners and pytest, CI. Parity skipped, no plan or Excel number. Revit, RAN by the test host under a locked screen, 3.6 minutes: a blank copy shows the line and with no source Apply stays off

Remains:
- Revit on main, RAN: a blank model by the test host took CAD by default and with a pick, typed values too, all committed, positionSource right, button 1 itself 20 to 45 s. The real window: the line, 5 of 5 check points at 0.00 mm, Apply. It showed Existing site file on a blank model and the side by side below the fold, both fixed after it, PR of the window
- Not run: the move stop R523 on a real model, a buttons job with a key copy is refused at AddinRun.cs:83

Known bugs:
- The new project check has no sender in the add-in, so nothing asks it today (steps/latent.md)

Next:
- The runs on main, RAMP-3NOV-A

Your turn:
- Opened Q420 Moving a model with placed content. Closed none.
- 180 open, steps/questions-open.md

## Lane C TEXTS-3NOV-C, 2026-11-04, the pane texts of the other lanes

Goal:
- Apply the text changes Lane A and Lane B sent, the owner of the pane file. Lane D waits on topo/3nov-d

Done:
- R542: section Report file name in New project, each edit to ReportFields. R543: step 1 line from positionSource of site.json

Checked:
- Tests R542, R543, labels, Pane, New project on net48 and net8.0, full gate, see the PR. Not seen in Revit, Q440

Remains:
- Road topography tick of Lane D. Revit pass. Browse not measured

Known bugs:
- None found

Next:
- Lane D tick after its merge, then Lane done

Your turn:
- Opened and closed: none. Q415 Pane lines say more than is built stays open, narrowed to the config keys Lane A cites
- Open count and titles: steps/questions-open.md

## Lane C RULES-3NOV-C, 2026-11-04, the table of rule values

Goal:
- The Site Adaptation Rules button opens a table of values. Save writes a per-project copy, Reset, Export, Import

Done:
- R541: config/rule_values.json 9 rows, Common/Settings/RuleValues.cs, AddIn/RuleValues window, called by PaneHost.OpenRuleValues from Buttons.Rules
- changed_rules.json beside the copy, read by R555 of Lane B. Update from report reads the copy too

Checked:
- Tests of R541 and UiLabels, full gate, see the PR. Review 1 and breaker 1, all REAL findings fixed with a test
- Worker build, RAN on NBH 012 with a changed copy: past job, ramp limit changes no plan field, wall top changes walls. Rev 06, ramp limit changes the summary
- Revit, 4 attempts on a copy: NOT DONE. Base plan kept, 336 plots, the table never opened. Last stop: locked screen, 30 of 30 tries. Inputs 21, changed 0

Remains:
- The Revit pass, Q440. Browse not measured. The pane texts of Lanes A, B and D

Known bugs:
- None found. The window is unproven in Revit

Next:
- Pane texts round, then Lane done

Your turn:
- Opened: Q419 Ramp slope limit does nothing under the past job. Q440 Rules table not yet proven in Revit. Closed: none
- Open count and titles: steps/questions-open.md

## Lane D MASS-3NOV-D and the Revit check of PARAM-3NOV-D, 2026-11-04

Goal:
- Q391, masses inside their plots, no overlap above 1 mm. The check of PR 165 through the real windows

Done:
- R530: the base cut at the party lines of its unit, config massPartyLines. Reader version 6, masses made once

Checked:
- MASS, RAN in Revit on a copy, Boolean of link solids, NBH 012: pairs over 1 mm 45 before, 0 after, largest left 0.55 mm. Mirror and rotation hold on 336. 1 pair Revit refused, 0.21 mm by a second method. Footprint 59 same, 0 over, 271 other, 6 none
- PARAM, RAN in Revit on a copy, old sheet in C:\SA-demo\config: row green, New project Found, no stop, the line before and after Apply, rows 2 to 9 unchanged. Parameters on links only, Plot on Floors, by a read job, the dialog shows no categories. 336 villas: 0 values lost. 1,003 floors hold Plot only
- Offline whole project: pairs over 1 mm 385 before, 2 after, plots past their plot 1,199 before, 11 after, Q470. Past plans byte equal. Parity not run, no step reads a mass

Remains:
- The PC stood locked, so presses went by UIA, not by mouse, and a press by step row is UNKNOWN. Button 7 on a copied model in another folder failed on the full path in check.json until edited (R377), the fallback SA-dryrun3nov-done should fail the same way, not run
- C:\SA-demo\config was refreshed with massPartyLines and the new sheet, backup in p_revit3nov. Its config hash changed, so step rows of models on it read Needs step until they run again

Known bugs:
- Mass remake 13.9 s each with Revit open, 181 s for 13, once per reader version

Next:
- TOPO-3NOV-D

Your turn:
- Opened: Q470 Plots a mass of its unit does not fit. Q471 Free ends that lose a wall they had room for. Closed: Q391, Q331, Q333, Q338. 179 open, steps/questions-open.md

## Lane B REPORT-3NOV-B, 2026-11-03, the report in BIM layout

Goal:
- Why the report failed in the dry run, Q307, Q408, ISO name and BIM sheets

Done:
- R550 to R555. Cover, Plot schedule, Floors, Walls first, walls in Placed, one count for pane and report (R554, ButtonRun.IssueCount)
- Dry run report: no failure in the logs, UNKNOWN what Bader saw, see the PR

Checked:
- python/report_excel_check.py on the real book: 105,411 matches, 0 mismatches. 336 villas, 1,003 floors, 778 walls from the model read
- Update from report by the worker: 0 changes on the plain book, 1 on an edited one

Remains:
- New project fields, Lane C after this merges. The window of Update from report not seen in Revit
- Element ids not opened by id in Revit, the screen was in use

Known bugs:
- C:\SA-demo\config holds no fileName block, so its models keep 012_Report.xlsx until it is refreshed

Next:
- 6b

Your turn:
- Opened Q450 Report defaults of old number, revision, status. Q451 Report Issues column against its sheets. Closed Q307 What the report Summary shows after a run. See steps/questions-open.md

## Lane D PARAM-3NOV-D, 2026-11-03, shared parameters shown right, on links only

Goal:
- Bader's dry run: the pane row read Not chosen, New project said the sheet is missing, Q411 floors bound but empty

Done:
- R506: the pane row shows the sheet of config, green, 11 parameters, 12 left out, New project reads Found. R507: config/shared_parameters.xlsx, the 9 RRE_ZZ_SP_ rows and CASE on RVT Links alone, Plot on Floors. An old sheet stops nothing, a button 7 row binds on links alone whatever it names, so C:\SA-demo needs no edit
- R508: button 1 takes other categories off a links row and says so before Apply and after. A removal alone keeps the last run, so steps 2 to 9 stay done
- CHOSEN: CASE also leaves Detail Items, Bader's check asks links only

Checked:
- RAN on a15d58c: pytest 2,509 passed, 1 skipped. dotnet 2,563 net48, 2,238 net8.0. Past plans byte equal whole project and IMC. 62 inputs the same. Parity: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 lines, 0 failures
- Review 1: chosen sheet green though button 1 stops, line gone after the run, code names in the note. Breaker 1: the rebind took Done off 2 to 9. The stop on an old sheet taken out at Bader's fix step 1. All fixed with tests. The rest in steps/latent.md

Remains:
- The Revit check, waits for Bader to say the dry run is over: no Revit, no lock, no install, no C:\SA-demo until then. So UNKNOWN: BindParameters in Revit, whether link values survive ReInsert on fewer categories, the windows in Revit

Known bugs:
- A model keeps the old binding until a press of 1

Next:
- Bader says the dry run is over, then the Revit check on a copy of SA-dryrun3nov-done.rvt and a records PR

Your turn:
- Closed: Q411 Floor parameters bound but never filled. 180 open, steps/questions-open.md

## Lane A KNOWN-3NOV-A, 2026-11-03, the known issues with a clear answer

Goal:
- Bader: fix the known issues of Lane A with a clear answer, write a recommended answer on the rest

Done:
- R520 Start from opens on the saved project, a re-pick imports. R521 Review takes the official number on 2312, saved marks move with it. Front edge in plain words, counted from 1 as Review. The override line ends with its folder
- Breaker: built R522 no Past on another job's key, removed, the add-in gives no key reads, Q396 to steps/latent.md

Checked:
- Touched tests on both runners, full gate before the PR, review 1 and breaker 1. Q400 and the check line not seen in Revit
- Q401, Q402 compared with the IMC key, RAN: no override wanted

Remains:
- The Revit pass of FIX-3NOV-A runs with COORD-3NOV-A: a first pass stopped twice in New project, nothing conclusive

Known bugs:
- R347 of WorkerRunTests failed once on net48 in a gate under load, passed 3 of 3 alone

Next:
- COORD-3NOV-A, RAMP-3NOV-A

Your turn:
- Closed Q376 Plain words for the front edge override. Q396 Past of a native key on new project. Q400 New project reopens on Empty project. Narrowed Q355, Q397. Recommended written Q401, Q402. Opened none.
- 181 open, steps/questions-open.md

## Lane C HELP-3NOV-C, 2026-11-03, i beside each step

Goal:
- Hover line and side box on steps 1 to 9 and New project rows, text from the guide

Done:
- R540: python/build_help.py, help file in the DLL, side box

Checked:
- Touched tests on net48 and net8.0, the pane and New project PNGs read. Not seen in Revit yet

Remains:
- RULES-3NOV-C. Second guide PR when PARAM merges, then rebuild the help file

Known bugs:
- None new

Next:
- RULES-3NOV-C

Your turn:
- Opened none. Closed none.
- 184 open, steps/questions-open.md

## Lane B FIX-3NOV-B end, 2026-11-03, undo in Revit

Goal:
- Finish task 4, the undo check of R499 in Revit

Done:
- steps/investigations/undo-3nov.md. steps/dryrun-3nov.md: the step 1 line (Q415) and the undo line

Checked:
- One undo and one redo on 9, one undo on 3, read in the pane and the journal. Records only, no code

Remains:
- 4 not proven, it stopped on picks.json of the copy. Save prompt after 3, 4, 9 alone

Known bugs:
- none new

Next:
- Known issues, REPORT-3NOV-B, 6b

Your turn:
- Opened none. Closed none. See steps/questions-open.md

## Lane C known issues, 2026-11-03, FIX-KNOWN-3NOV-C

Goal:
- The issues of the guide with a clear answer

Done:
- Pick surfaces under the row (Q403), plain band (Q404), CAD label (Q406), No changes line (Q408), Ramp joint name (Q410), step 8 Boundary walls (Q415)

Checked:
- Touched tests on net48 and net8.0, text and window only, no parity

Remains:
- Not seen in Revit. Step 1 text of Q415 from Lane A. Then HELP-3NOV-C, RULES-3NOV-C

Known bugs:
- Q400, Q416, Q417, Q418

Next:
- HELP-3NOV-C

Your turn:
- Opened Q416 Road row reads optional, step 2 needs it. Q417 Whole project stops at step 3. Q418 Empty project keeps old rules on a set up model. Closed Q403, Q404, Q406, Q408, Q410.
- 184 open, steps/questions-open.md

## Lane C GUIDE-3NOV-C, 2026-11-03, plain guide to the 9 buttons

Goal:
- A plain guide to the 9 buttons for Bader and the team, records only, no code change

Done:
- steps/buttons-guide.md: the table, one part per button and pane row, every file from start to end

Checked:
- Each line against its class or config key and the files of the rehearsal run, READ. The list is in the PR body

Remains:
- Shared parameters row and New project change with Lane D PARAM-3NOV-D, a second small PR when it merges

Known bugs:
- Pane lines say more than is built, Q415. Whole project times not known, Q414

Next:
- Bader's answers. The second PR after PARAM-3NOV-D

Your turn:
- Opened Q414 How long a whole project takes. Q415 Pane lines say more than is built. Closed none.
- 186 open, steps/questions-open.md

## Lane B DRY-RUN 3 Nov, 2026-11-03, the presenter check on frozen main b1a27d9, Lane B on the laptop

Goal:
- Bader: rehearse 1 to 9 on NBH 012 through the real windows, read the shared parameters, GO or NO-GO, steps/dryrun-3nov.md

Done:
- steps/dryrun-3nov.md. Lane B made the kit, none existed: SA-dryrun3nov.rvt from SA-demo-start. C:\SA-demo config and rules refreshed from main, backup in lane_b_check\p3nov_check\backup
- 1 to 9 run twice through the pane. Fallback SA-dryrun3nov-done.rvt with its work folder

Checked:
- Q384 in Revit: a new Revit shows all nine rows as saved (READ the pane). Parameters read in Properties on 3 villas and 3 floors, none wrong
- Inputs same 44 by SHA-256 at 15:12 (RAN), read again before the end

Remains:
- Undo in Revit not proven, keys and the Undo button changed no row. Task 4 checks left. Two plot overlaps merged, Lane A's question. 6b after the restart

Known bugs:
- Level 1 is empty after the build (Q412). Floors hold no RRE_ZZ_SP_ values (Q411). Report Summary gaps (Q307). Pane and report issue counts differ (Q307)

Next:
- Bader's dry run at 17:00. After the restart Bader resumes Lane B: Phase 6b

Your turn:
- Q411 Floor parameters bound but never filled. Q412 Level 1 is empty after the build. Q413 Input picks lost before a real run. 184 open, steps/questions-open.md

## Lane A FIX-3NOV-A, 2026-11-03, a new project runs rev6 and grading files are no roads

Goal:
- Bader: Q373 project mode in the settings, Q371 notRoad list, an Empty project with no JSON edit reaches 4 Check under rev6

Done:
- R484 mode in the settings, on the request of all 7 sites. R485 7 grading files are left out and refused under rev6
- Breaker 1 and my check: an Empty project had no config at all, so it told Rev 06 and could not build. Bader closed Q377, Q378
- R486 the install ships a set, Empty project copies it per model. R487 its override list is empty, R330 gives way under rev6 alone
- Fixed by the breakers: button 4 under rev6 always stopped on the role floorOutlines, a saved past job moved to rev6 on a plain Save, Save overrides refused on an empty list, two models of one name shared a list
- The window before Save: Pick surfaces grey and a false parameter sheet stop, found by a Revit shot, fixed, the line now under Start from

Checked:
- 5 TU zones with the set, RAN: 1,759 of 1,791, the same 32 left out as PR 145, 0 changed. Recount from the plan files equal
- ROSHN saved project, RAN: IMC and whole project byte-equal to main, with and with no mode
- Parity on f225518, RAN: 32 runs, 416 lines, 0 failures, past job plan byte-equal. No Python, plan, CadReader, config or rules change after it
- Gate on 183ef5f: dotnet net48 2,526 and net8.0 2,206 passed at 183ef5f, pytest 2,509 passed, 1 skipped at 59692e9, no Python change after
- Revit on 84e1e69, RAN, 36 minutes under the lock: Empty project, Save setup, 1 dry run, 2, 3, 4 on a copy of NBH 012. Requests hold rev6, plan 336 plots, 0 overrides, inputs 43 of 43 same. One line edited, inputRoot in the project copy, to keep inputs on copies
- Review 1 and window review, breaker 1 and 2

Remains:
- Not seen in Revit: the window fixes after 84e1e69 (Pick surfaces active, Found sheet row, the line's place), Road surfaces window, Apply of button 1. Lane B holds the lock to the freeze. Tests only so far

Known bugs:
- A new site still needs the inputs of this job (Q379). Update from report saves nothing on a new project (Q394)

Next:
- A short Revit pass of the items in Remains after the restart

Your turn:
- Closed: Q371 Seven non road files still offered. Q373 New project does not start under rev6. Q377 Where Empty project gets its config. Q378 Override list of a new project
- Opened: Q379 A new project on another PC. Q394 Update from report on a new project. Q395 A mode in memory lost on reopen. Q396 Past of a native key on new project. Q397 Save As of a new project keeps one list. Q398 The 443 hairline floor overlaps at step 5
- 181 open, steps/questions-open.md

## Lane C 6C-3NOV-C, 2026-11-03, window check, Excel compare, check sheet

Goal:
- Every window against its board in real Revit, the Excel compare of NBH 012, the Part 1 sheet, then Bader's row rename before the 14:00 freeze

Done:
- R505: theme, pane, New project, How it works, What the tool read, Review data, 4 Check differences fixed. steps/investigations/6c-windows.md per window and the rest by owner
- steps/investigations/6c-excel.md. steps/part1-check.md the short form for 16:00, steps/part1-check-long.md the long check for later
- Bader 3 Nov: row Curbs reads Plot fronts (curb lines) with its Read only line, the run input keeps the name Curbs, a key of the step hashes (R366)

Checked:
- Real windows, RAN: 3 runs through the pane on copies, buttons 1 to 9 on NBH 012 in the last two. 1,003 floors, 336 villas, 778 walls, 1,339 Placed, 688 accepted, box 485. 43 inputs the same by SHA-256
- Excel, RAN by openpyxl and by raw XML, the same counts: FFL 324 equal, 12 differ up to 187 mm, 1 missing. Steps 334, 2, 1. Placed against past Unit 334, 2, 1
- Dead buttons: 0 pressed dead. Two Change buttons of New project have no handler and never show
- Review 1 with the UI checklist: REAL fixed or a question, 3 LATENT

Remains:
- The rename and the separator are not seen in Revit, only in the rendered PNG and tests, the lock was Lane B's. Road surfaces never seen in Revit. Update from report filled state not seen, my driver failed twice

Known bugs:
- Q403 Road surface row cut. Q400 New project reopen. Q408 Update silent after a pick

Next:
- Bader's dry run at 17:00, steps/dryrun-3nov.md of Lane B

Your turn:
- Opened Q400 New project reopens on Empty project. Q401 12 plots differ from the past FFL. Q402 Plots 2301 and 2302 read VD4. Q403 Road surface row cut in the pane. Q404 Set up site band shows a code name. Q405 Export to Excel hidden in Rules. Q406 Conflicts Plot column shows the CAD label. Q407 Amber in the spec or the boards. Q408 Update from report shows nothing after a pick. Q409 Two greens and the step name weight. Q410 Rule column shows R471 on joint rows. Closed none.
- 177 open, steps/questions-open.md

## Lane B FIX-3NOV-B tasks 1 and 2, 2026-11-03, step rows survive a reopen and an undo, Lane B on the laptop

Goal:
- Bader: Q384, every button 2 to 9 records its run in the model. Q382, an undo takes the Done state back

Done:
- R498: buttons 3, 5, 6, 7, 8, 9 write in the transaction of the run, 2 and 4, which have none, in a small one of their own
- Warnings, issues after the commit and outputs go in by a second write, one undo entry by a transaction group
- R499: an undo or redo drops the runs of the session, each step reads the entity
- Breaker: the record keeps its outputs under the work folder, so a copy or a Save As of the model is not Done
- Task 3: steps/investigations/two-plot-overlaps.md, the 443 are hairline laps, deepest 1.47 mm. Ramps fit the 1 mm rounding, 71 yard corners do not, cause UNKNOWN. Not run in Revit, sent to lane-a, nothing fixed

Checked:
- Parity 32 runs, 416 lines, 0 failures. Gate dotnet 2,404 and 2,109 passed. Review 1, breaker 2 passes. Fakes only so far: the group and the undo in Revit are task 4

Remains:
- Tasks 3, 4, 5 of the round. Q382 narrowed to step 4 and site.json

Known bugs:
- Override list changes leave steps Done (Q386). An undo of 3 or 4 takes the record back, not the files

Next:
- PR of task 3, hand check of tasks 1 and 2 in Revit, Phase 6b

Your turn:
- Q384 Step rows lost on a reopen, closed. Q386 Override list changes leave steps Done. 166 open, steps/questions-open.md

## Lane D MASS-2NOV-D, 2026-11-03, the mass outline on the outer face of the walls (Q321, Q279)

Goal:
- The mass base is the outer face of the exterior walls, exact corners by line intersections, the slab only where the walls do not close, with a flag

Done:
- steps/investigations/mass-outline.md, two probes. By joins 0 of 13 close. Function does not part house and yard walls, distance and height do. A wall's Location Line does not move its curve, the face is width/2 along Orientation, so the old offset missed. 925 walls of DP1 hold 118 in the main model, the rest design options
- WallRingBuilder in Common: R500 ring walls by distance and height, main model and primary option. R501 face line, an edited profile gives the ground stretches. R502 corners by intersection. R503 short edges, arcs kept. R504 ring or slab flagged. WallStrips and the union gone. Mass files remade once, reader version 5
- 12 of 13 rings, DP3 on the slab with its reason. R477 takes the centre of the area, the mean of the corners gave 14 false rows

Checked:
- Revit, 336 plots, all 13 prototypes. Second method, no code of the builder: 89 of 94 edges on a wall face line, 0.000 mm off, 5 connecting. Mass against link 0.000 mm, 0 degrees, 0 mirror on 336. The shape of a link against its mass was not read
- Breaker 1 found 7 of 12 rings wrong on real data, offline and silent: FFL based walls dropped, up to 430 mm, a recess filled, a mixed option. All fixed. Breaker 2 found the footprint fault, fixed, and no exterior stretch outside a ring. Review 1: values moved to config
- Footprint: main 59 same type, 269 other, 8 none. Now 58, 271, 7, over the limit 0
- Parity: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 lines, 0 failures. Past job plans byte equal on the whole project and IMC. 62 inputs the same by SHA-256, 13 prototypes unchanged by time and size, the first Masses run stopped on 13 .slog (Q338), the second 275 of 275
- Gate on the head: pytest 2,464 passed, 1 skipped. dotnet 2,377 on net48 and 2,087 on net8.0 passed. Parity ran before the merge of PR 151, which adds timing lines. Past job byte equal on the head too

Remains:
- Q390 to Q393. No read job gives a mass outline. The listener of Lane C came after these runs, they went through window_lock with one slot

Known bugs:
- 52 twin pairs overlap (Q391). 13 plots are not the primary option (Q392). Small wall ends past a ring, in the investigation. No arc wall was ever run in Revit, steps/latent.md

Next:
- Nothing queued for Lane D. Masses take 8.7 s each to remake, left to me by Lane C, steps/investigations/speed-2nov.md

Your turn:
- Opened: Q390 A wall modelled inside out, far face kept. Q391 Twin masses overlap along one side. Q392 The primary option is not every plot's condition. Q393 The chosen limits of the wall ring
- Closed: Q279, Q321 by Bader's prompt of 2 Nov
- 166 open, steps/questions-open.md

## Lane C SPEED-2NOV-C, 2026-11-03, Revit runs faster for all lanes

Goal:
- Timing first, a Revit listener, two slots, fixes only where the time goes, the same model after

Done:
- R447 run_timing per button and step. R448 the listener in the test host, job folder, one line to send. R449 two Revit slots, 65,221 MB here
- R496 the pane warns on Revit 2021 prototypes. R497 a Revit .slog of a backup folder leaves the input check outside the input roots, a new file under one fails the run
- Button 7 split: the 57 s were 6 stale masses, 52.3 s, 8.7 s each, once per change, 0 s else. The file is Lane D's, untouched
- No change for warnings 0.1 s, the model refresh of button 5 (both calls needed), transactions, joins, links: steps/investigations/speed-2nov.md

Checked:
- Revit, RAN: whole run 6.8 minutes own run, 2.0 minutes second listener job. Timed buttons 232.6 and 305.5 s against 110.2 s, first job 184.2 s
- Model, RAN against SPB3: floors, place, walls, footprint 0 differences, levels and accepted differ only in element ids, plan body equal. Second method, the Levels workbook: 8,298 of 8,302 cells equal, 4 run stamps
- Real inputs 276, changed 0 on SPM1, SPM2, SPM4
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 416 reports, 0 failures, 16 of 16 plans byte-equal to main
- Review 1, breaker 1 on 3329b5a and on the delta, REAL fixed. Test-first slipped on the first review's R496 only, said in the PR

Remains:
- Listener not run in Revit: a throw inside Revit, past limit and back, killed, hung, findings exit 5, slot 2. R497 and the pane line of R496 in the real pane not seen in Revit

Known bugs:
- None new. Lines in steps/latent.md

Next:
- Lanes send jobs through the listener, one add-in build per machine

Your turn:
- Opened none. Closed none.
- 164 open, steps/questions-open.md

## Lane B FIX-2NOV-B task 4, 2026-11-03, the hand run of main on NBH 012, Lane B on the laptop

Goal:
- Bader's task 4: buttons 1 to 9 by hand on main with PR 139 and R459, read back, compare with PR 139, check Q158

Done:
- steps/fix-2nov-b-handrun.md. Through the real windows on a new model from a named copy, not the test host
- 1,003 floors, 0 refused, 336 masses, 778 walls. Step 5: 688 accepted, 443 overlap and 42 off axis shown, all 443 of two plots
- Q158 in Revit: the dead end of part 4 gone, also after a new Revit. No number worse than PR 139

Checked:
- Inputs same 44 by SHA-256 (RAN). Warnings, accepted file, sheet and Placed read by a second script, 0 mismatches (RAN)
- No code in this record. Gate of the code: PR 146 and PR 148

Remains:
- Q382, Q383, Q384, Q385. Button 6 moving floors with no list, UNKNOWN

Known bugs:
- Accepted warnings return by the first save (Q385). Step rows lost on a reopen (Q384). Chip press shows an empty table

Next:
- Lane D holds the lock since 03:03. Lane C reuses the handler

Your turn:
- Q384 Step rows lost on a reopen. Q385 Accepted warnings return after a save. 164 open, steps/questions-open.md

## FIX-2NOV-A3, 2026-11-03, the split test, the ends of parted walls and notched fronts

Goal:
- Q303, Q304 R414 by steps alone from the high road end. Q265 walls.ends weak measured for parted walls. Q299 the 6 notched plots against their key links

Done:
- R414 steps alone, walls from the high road end, slope reported per unit. It stays off: 43 of 82, 0.5244, under majority.strongShare 0.90
- R481 parted cluster walls read MEASURED weak, source wallOpen, new step field endsSource, the rest as before
- R482 override frontEdge names the front edge. 6 lines, plots 1553, 1172, 2626, 2665, 727, 469. Insert lines of the old front gone. Level gaps of 5 plots measured on the true front, 16 no value lines gone, heightM of pair 469 and 470 -0.004, overrideSets frontEdge

Checked:
- R414 on the 5 TU keys, RAN: clusters 8 to 43 of 82, key splits 0 to 3 of 41, key whole 40 of 41. On 37 of the 41 key splits the whole cluster already fits, no wall is tried
- Second route, RAN: 82 of 82 marks equal. Q304 holds on 13 of 13 uneven key splits
- Plan against main 69775f8, past job, RAN: 2,518 to 2,524 written, only the 6 new and 470 differ. Missing 236 to 233. Walls 5,999 to 6,029, ends MEASURED weak 0 to 1,528, UNKNOWN 5,999 to 4,501, ends values equal
- Notched plots against key, RAN: rotation and insert within holds on 6, FFL, entry and steps equal on 5, second way 48 match 0 mismatch. 1553 is SE, levels left out
- Parity on c91c1fa, RAN: 13 steps, net48 and net8.0, whole project and 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures
- Gate on aa15d7d, RAN: pytest 2,448 passed 1 skipped, net48 2,218, net8.0 1,933, 0 failed
- Review 1: no code fault. Breaker 1: 3 REAL on R482, fixed. Breaker 2: stale outline and steps lines, the hand in the set, fixed, slope of 1172 is Q375

Remains:
- UNKNOWN: why the keys split clusters that fit. No full majority.py run, its inputs not rebuilt, the lines are by hand and measured by its method, 3,646 other lines equal
- No Revit run, none needed. Latent lines in steps/latent.md

Known bugs:
- Plan ramp of 1172 falls 0.658 percent against 2 in the key (Q375)

Next:
- FIX-3NOV-A, Q373 and Q371

Your turn:
- Closed Q265, Q299, Q303, Q304. Opened Q375 Ramp type after a rampFrontM gap, Q376 Plain words for the front edge override. 162 open, steps/questions-open.md

Older rounds stand in steps/history/log-older-2026-11-02.md, cut byte for byte to keep this file under the cap of the records.
