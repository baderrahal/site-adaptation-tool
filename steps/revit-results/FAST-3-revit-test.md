# FAST-3-revit-test, Revit check, run 1

Run 1 of the one test, Sat 7 Nov 2026 from 01:11. Lane revit-test, Revit 2024.3, one Revit under python/window_lock.py, round FAST-3-revit-test. The add-in is the one Lead built from main 14044dd and installed at 23:00 on 6 Nov.

Every press and every value set here came from a driver of this lane, not from a person. The driver is Lead's copy of drive3.ps1 at a fixed path of the work root, from the driver of R2-C1-revit-test. It sends a click, a key or a shot only with Revit in front and stops on any input that is not its own. Its own log is driver.log of the folder `fast3-revit-test/shots/run1` of the work root.

Screenshots of the real run show real coordinates, plot numbers, file names and the user name of this machine. They stay under the work root, folder `fast3-revit-test/shots/run1`, never in git.

## Times

- 23:33 Fri: lane started. 23:43 a Teams window "Meeting compact view" stood, so nothing was sent.
- 00:16 Sat, Bader's order by Lead: that one Teams window keeps the laptop awake and is no meeting window. Any other meeting window still stops the driver.
- 00:46 the launch of the driver was refused by the permission check of this session. 01:15 Bader allowed one command form.
- 01:11:09 Revit opened on the model copy, process 46028.
- 01:11:56 the box "Security - Unsigned Add-In" of Site Adaptation came and the driver pressed Always Load, Bader's order of 23:37. File always_load.txt of the run folder holds the time.
- 01:20 VS Code came in front with fresh input. The driver sent nothing and waited for 5 minutes idle, then went on at 01:29:55.
- 01:48 one click aimed at a button below the visible pane landed on the taskbar, which took the focus. Nothing opened there. The driver now refuses any point outside the visible Revit window, and brought Revit back in front only after checking that no other input had come.
- 03:10 the driver and Revit ended at the 2 hour limit of the background tool call, right after Build was pressed (s31-after-build.png). The model was not saved, window.lock stayed. Nothing was built.
- 03:19:13 relaunch, part 2, the same allowed command with -MaxMinutes 110. window_lock.py deleted the stale lock itself. 03:19:53 the Unsigned Add-In box came again, the driver pressed Always Load again (always_load.txt holds both times).

## Model and folder

- Model: `FAST3-empty-model.rvt` in the folder fast3-revit-test of the work root, a fresh copy of the empty model of R2-C1, a project made from the Revit metric template. Proof it is an empty model: the project browser holds only Level 1, Level 2 and Site plans, no Revit link, and Set up site opened with the badge "Empty project found" and "This model has no site coordinates" (shots s01-start.png, s10-setup-site.png).
- Folder: S1 of the scenarios of the work root, chosen in the pane by Choose project folder. SHA-256, size and write time of its 37 files were taken before the run into s1_before.json of the run folder.


## Stage 1 Project data, card 1

1. Settings, then Project setup Edit. The New project window opened on "Empty project" with the New project rules, Rev 06, 1 of 4 files ready (s03-after-edit.png). Save setup. The pane showed the amber line "No rules chosen for this project yet. Open Rules in Settings." and card 1 Ready (s04-after-save.png).
2. Rules Open from Settings: the window Rules of this project, 15 rules M01 to M15, each source missing and status not confirmed, and the button Use the manual template (s05-rules.png). Closed with no choice, to see the ask of the first Build.
3. Choose project folder, the Browse For Folder tree, S1 picked by keys (s07-folder-picked.png), OK. The box "2 Read data needs step 1" came (s08-after-ok.png), fault F1.
4. Under Advanced: Run 1 Set up site, "Empty project found", "The pane names no baseplan drawing" (s10-setup-site.png), fault F2. Cancel, the Baseplan row of Advanced set to the Baseplan of S1, Run 1 again, Use CAD: 5 check points from CAD. Dry run 5 of 5 pass, Apply stayed off until the site drawing was ticked Current, then Apply. Step 1 Done (s17-state.png, s19-after-apply.png).
5. Choose project folder again, S1, OK: the dry run box "Reads the folder and the settings file. Files in the folder: 37, 1317.4 MiB" (s23-after-ok2.png). Apply. The worker ran outside Revit with its own small window and an elapsed time (s24-reading.png), 02:07 to 02:21.
6. The window What the tool read: 7 files read, 2,618 plots found, 11 need rows (w-check-window.png). The Shared parameters row said Missing, fault F10. Close, then the window Confirm what was found with the villa type column and the area column Neighbourhood_ID proposed, a code map proposed from the DLUS schedule, 13 prototypes with no guess, the group level words (w-confirm-top.png). Confirm as proposed. "2 Read data done: 4 files written, 0 issues."
7. Card 1 then showed the rows of the read, each with Strong, Usable, Weak or Missing and its plain line, and Fix items (card1-a.png, card1-b.png). Choose project folder again read with the confirmed values, 02:32 to 02:45: 2,615 plots, areas from the confirmed column 13. Cancel on Confirm, the values stay.

Against the files, by a script that prints counts only: read 1 plots.json holds 2,618 plots and 346 otherPlots, 2,964 in all, the count of plot outlines with a number. So the header "plots found" counts the villa plots of R730. Read 2: plots per area, 010 holds 16 villa plots.

## Stage 2 Build site, card 2

The scope: Neighbourhood 010 under Advanced Scope. CHOSEN by Code: the task asks for a neighbourhood not used for tuning. steps/truth-audit.md section 6 lists the 7 tuned NBHs, 012, 013, 021, 022, 02, 03 and 01A. The scope list offered also 010, 01B and 023, with 16, 2 and 1 villa plots. 010 is the largest of those. Villa mode Links, Dry run first off (card 2 has its own Preview).

8. Build, 03:10 in part 1: the one ask "No rules are chosen for this project. Use the rules template of site adaptation manual ... Rev 06? ... They stay not confirmed until a person confirms them." Then the 2 hour end.
9. Part 2, after Save setup and step 1 again: Build gave "The plan could not be made: no config of the run gave the design values and no config/project.json lies above C:\Program Files\Autodesk\Revit 2024\ (R405)" (card-2-r405.png), fault F4. After step 1 ran in the session, Build made its plan.
10. The ask came once, Yes (w-ask-rules.png). The window Preview of the build: "2615 plots with an outline, 2619 issues. Nothing is written to the model yet.", Cancel and Go on (w-build-preview.png). Go on. Card 2: "Build plan: 2615 plots, 0 ready for every step, 2615 marked, 2619 issues."
11. The chain ran step 2 Read data twice, each reusing the read, with the check and Confirm windows each time, fault F6, then 3 Review data, NBH 010, 16 plots, 0 of 16 checked (w-review-data.png). Save review, Close. Steps 1 to 3 Done, card 1 Done.
12. The Build stopped at 4 Check: "4 Check needs Prototypes, the folder is not chosen" and card 3 "2615 plots have no road level" (card-2-after-review.png), fault F5. The Prototypes and Road surface rows of Advanced set to the folders Prototypes and landxml of S1, 4 of 8 found.
13. Build again stopped at Check: "2 Read data did not read the road surface Top Surface-02. Run 2 Read data again." one per surface (card-2-road-stop.png), fault F5b. 04:37 Run 2 Read data under Advanced.
14. The read again through Choose project folder said "Reuses the read of run 2_..., the folder, the settings file and the values a person confirmed are unchanged" (read3-preview.png): the Road surface row is no input of the read, so the stop line of step 13 cannot be met, fault F5c.
15. 04:52 and 05:07 the driver saved the model copy with Ctrl+S, Lead's word of 04:33. `FAST3-empty-model.rvt` is the built copy, `FAST3-empty-model-pristine.rvt` beside it is the empty model, byte equal to the empty model of R2-C1.
16. Build again 04:58 with the Prototypes row set: preview, Go on, then 3 Review data. Closed with no Save review, the Build said "Build stopped at Review data, cancelled." (d-end.png). In step 11 Save review came before Close and the chain went on. So each Build asks the review again, fault F13.

Seen on the pane at 05:07 (d-end.png): the amber line "15 of 15 rules are not confirmed. Open Rules in Settings." once the manual template was taken. Card 3: "2619 issues from the last build." with Open rules, the line "2615 plots have no road level, so no floor level, and steps 6 to 8 leave them out.", the source mix bar and its line "Values: computed 9879, rule 10907, person entered 2614, missing 10595, other 0.", Report and Update from report.

## Part 3, from 05:11

- 05:11:37 relaunch, part 3, on the saved model copy. No Unsigned Add-In box this time. The project, the input rows, the scope 010 and the rules stayed, the amber line said "15 of 15 rules are not confirmed. Open Rules in Settings." (p3-start.png).
17. 05:20 Build: "The plan could not be made ... (R405)" again on the first Build of the session, F4 seen twice.
18. 05:47 Fix items opened Confirm what was found. CHOSEN by Code: the word "Block" typed for group level 2, so the read cannot be reused, then Confirm: "Your values are saved. Press Read data again to read with them." Choose project folder, S1: "Reads the folder, the settings file and the values a person confirmed", Apply. The read ran 05:49 to 06:01, 721 s. Group levels "words Neighbourhood_ID, Block".
19. Its read.json, by a script that prints keys and counts: inputs config, scanFolder and projectProfile, no road surface, files data_check.json, plots.json, project_profile.json, read.json, read_plotTable.json.
20. 06:09 Build, preview, Go on, Review data with Save review then Close: stopped at Check, F16 (g-end.png).

The tool did not build the site in run 1. Floors and ramps, Levels, Place villas and Walls never ran. Villa links: none, NBH 010 holds no villa type (F15), and every NBH stops at Check (F16).
21. 06:21 to 06:36 the windows not yet shot: How it works and Update from report opened and closed by their own window close. The i of each card gives a tooltip, no window. Road surfaces (Pick surfaces) was not reached in time. 06:36:33 the driver saved the model copy, 06:37:53 Revit closed by the driver, "revit left open: False", the lock was gone, no Revit process.

## What the acceptance tests ask, seen or not

- The 3 cards, Project data, Build site and Review and deliver, seen in Revit from the empty model on S1, one screenshot each in steps/ui-shots, pane-stage-1.png to pane-stage-3.png, taken in Revit, the coordinates line of card 2 blanked.
- Choose project folder: seen, it started the read only after step 1 ran from Advanced (F1). Fix items: seen, it opened Confirm what was found (step 18).
- Preview: the tick Preview first on card 2, on by default, and the window Preview of the build with Cancel and Go on, seen (step 10).
- Build: seen, the one ask of the manual rules once, then the chain. The progress line of card 2 said "Step 3 of 8: Review data." while the review window stood (w-review-data.png, behind the window). The Build stopped at 4 Check each time (F5, F16).
- 4 Check: stopped, see F16. 5 Floors and ramps, 6 Levels, 7 Place villas and 8 Walls: not reached, not seen in Revit. No floor, level, villa link or wall was built.
- Q439, a model built before: not reached, as nothing was built. The saved copy holds coordinates and settings but no floor, so a Build on it again is no test of Q439. UNKNOWN.
- source mix: the bar of card 3 and its line, seen (pane-stage-3.png).
- Advanced: closed by default at the first open, it holds the old step rows, the input rows and the scope (s09-advanced.png, s12-advanced-rows.png). It closed again after a relaunch and after some builds.
- gear: no gear icon in Revit, the button at the top of the pane says Settings (F9). It opens Project setup and Rules.
- amber: the amber line "No rules chosen for this project yet. Open Rules in Settings." and after the ask "15 of 15 rules are not confirmed. Open Rules in Settings." (s04-after-save.png, d-end.png).
- not confirmed: the rules table showed 15 rules each "not confirmed" (revit-project-rules.png).
- empty model: the copy opened with only the template views and no link, Set up site said "Empty project found".
- S1: the folder of the run. 37 files, SHA-256, size and write time before and after the run equal, 0 changed, 0 new (s1_before.json, s1_after.json of the run folder).

## Window shots in git

steps/ui-shots/: pane-stage-1.png, pane-stage-2.png, pane-stage-3.png, revit-new-project.png, revit-project-rules.png, revit-setup-site.png, revit-scan-results.png, revit-confirm-profile.png, revit-ask-rules.png, revit-build-preview.png, revit-review-data.png, revit-how-it-works.png, revit-update-from-report.png. Each cut from a shot of the real run, a real value blanked where one stood: the check point tables, the code map and the line naming its sheet and columns, the drawing of the plot outlines, the plot numbers, areas and folder path, the coordinates line, the road edge line whose layer name is in no config. Not shot: Road surfaces, the check table of step 4 (never opened as step 4 stopped first), the Site Adaptation Rules window of the ribbon, Use a saved project.

## Not seen in Revit, UNKNOWN

- Floors and ramps, Levels, Place villas, Walls, Report: never ran.
- A person using the windows: every press came from the driver.

# Run 1b, Sat 7 Nov 2026, NBH 01A from Advanced

Bader's order of 10:05 by Lead: on an empty model copy, the old steps 1 to 9 from Advanced, neighbourhood 01A, marked tuned (steps/truth-audit.md section 6 lists it as tuned), Bader's answer to F15. Same add-in as run 1, the one installed 6 Nov 23:00, no new install, so no Unsigned Add-In box came. Same driver, drive3.ps1 of the work root, under python/window_lock.py, parts under 110 minutes, the copy saved at the end of each part. Shots under the work root, folder `fast3-revit-test/shots/run1b`, never in git.

## Model and folder, run 1b

- Model: `FAST3-run1b-01A.rvt` in fast3-revit-test of the work root, copied from `FAST3-empty-model-pristine.rvt` at 10:38. `cmp` against the empty model of R2-C1: equal bytes.
- Inputs: S1 of the scenarios, 37 files, SHA-256, size and write time taken before the run into s1_before_run1b.json of fast3-revit-test.
- revit-wanted-build-1.txt and revit-wanted-build-3.txt: none in the lanes folder at 10:39 and at 12:22.

## Part 1, 10:40 to 12:22

- 10:40:37 Revit opened on the copy, process 52488, the pane said "Set up this project first, in Settings." on the 3 cards.
1. Settings, Project setup Edit, the New project window, Save setup (a02-new-project.png, a03-after-save.png). Amber line "No rules chosen for this project yet."
2. Advanced opened. The 8 input rows set by Browse to the files of S1: Baseplan, Villa blocks, Plot fronts, DLUS and sales (two boxes, DLUS schedule then Sales schedule), Plot numbers, Road surface the landxml folder, and Shared parameters already set. "All 8 found" (a06-inputs.png). The Road surface row first took a wrong folder by the typing walk of the folder tree, picked again and checked by shot (folder-landxml-2.png).
3. Prototypes: the row warned "These files are Revit 2021 and upgrade on each use, use the 2024 copies. A remake from them takes 20 to 41 minutes." CHOSEN by Code: the row set to the folder prototypes_2024 of the work root, the 2024 copies of the same 13 prototype files, as the pane asks, to keep each part under 110 minutes.
4. Run 1 Set up site: this time the window took the Baseplan of the row, "Empty project found", "This model has no site coordinates", Use CAD already chosen, check points 5 of 5 pass (b01-setup-site.png). Each Current ticked, Dry run, Apply. Step 1 Done at 11:42, its line "Coordinates from the CAD baseplan, levels and units from the rules, types from config".
5. Run 2 Read data: the box "Dry run done, nothing was changed. Reads 19 files, 223.2 MiB." (b04-read-box.png), Apply 11:45:30. The card "Running outside Revit", no stop button (F19). It ran the data check on the input root of the past job, not on the input rows, F18. 12:17 What the tool read came: 18 files read, 2,964 plots found, 386 conflicts (b07-what-read.png). Cancel, as it read the wrong folder: "2 Read data: the read stopped at the scan, nothing was written." (F20). Step 2 stayed Ready.
6. 12:21:17 Ctrl+S saved the copy. 12:22:31 quit, "revit left open: False", no lock, no Revit process.

## Part 2, 12:27 to 14:06

- 12:26 idle 303 s, no wanted file, no Revit. 12:27 Revit opened on the saved copy, no Unsigned Add-In box.
7. Choose project folder, S1: the box "2 Read data needs step 1" (c03-needs-step1.png). Under Advanced step 1 showed Ready though it was Done and saved in part 1, F21. Run 1 Set up site again, the ticks Current kept, Dry run, Apply, Done.
8. 12:59 Choose project folder, S1, Apply: the read of card 1, 689 s. What the tool read: 7 files read, 2,618 plots found, Shared parameters Missing (F10 again). Close, Confirm what was found with the villa type column and the area column Neighbourhood_ID proposed, Confirm (c07-confirm.png). "2 Read data done: 4 files written, 0 issues."
9. The Scope list of Advanced offered one area, L1-1, F22. 13:20 Choose project folder again, the read with the confirmed values, 750 s. It ended on "the scope stops it: Neighbourhood: no neighbourhood is chosen", as the Neighbourhood option stood with no area picked. Scope Whole project, Run 2 Read data reused the read, then the list offered 010, 012, 013, 01A, 01B, 02, 021, 022, 023, 03. Scope 01A, Villa mode Links. Its plots.json, by a script that prints counts: 2,615 plots, 278 in 01A.
10. Run 3 Review data: "3 Review data, NBH 01A, 278 plots", "0 of 278 checked by the team" (c13-review.png). Save review, Close: step 2 back to Ready, F23. Run 2 again on the reused read, Run 3, Save review, Close: steps 1 to 3 Done, card 1 Done, card 2 Ready.
11. 14:03 Run 4 Check: "4 Check changed nothing, the model is as it was. The run failed: 2 Read data did not read the road surface ..." for each of the 6 surfaces, "2 Read data was done on other inputs, changed since: S1", and the read holds no read_baseplan.json, read_villaBlocks.json, read_curbs.json, read_workbooks.json (c17-check.png). F16, now from Advanced too. Step 4 stayed Ready, step 5 "Needs step 4".
12. 14:05:16 Ctrl+S saved the copy. 14:06:23 quit, "revit left open: False", no lock, no Revit process.

S1 after the two parts: 37 files, SHA-256, size and write time equal to s1_before_run1b.json, 0 changed, 0 new (s1_after_run1b.json, compared by a script).

Second check of the stop at Check, by another method: the read folder of the project work folder holds data_check.json, plots.json, project_profile.json, read.json and read_plotTable.json, no read_baseplan.json, read_villaBlocks.json, read_curbs.json or read_workbooks.json (ls).

# Short check, Sat 7 Nov 2026, NBH 01A from card 1

Lead's order of 14:40, Bader's: the short check after PR 256 (F16) and PR 254 (F4), not a full run. NBH 01A, marked tuned (steps/truth-audit.md section 6), Bader's answer to F15. The add-in Lead built from main 89d1c69 and installed with Install-SiteAdaptation.ps1, the .addin file written 14:59. Same driver, drive3.ps1 of the work root under python/window_lock.py, -MaxMinutes 110. Shots under the work root, folder `fast3-revit-test/shots/short`, never in git, they show real values.

## Model and folder, short check

- Model: `FAST3-short-01A.rvt` in fast3-revit-test of the work root, copied from `FAST3-empty-model-pristine.rvt` at 15:14, `cmp` equal bytes. A new name, not the run 1b copy.
- Inputs: S1 of the scenarios, 37 files, SHA-256, size and write time taken at 15:14 into s1_before_short.json of fast3-revit-test.
- revit-wanted-build-1.txt and revit-wanted-build-3.txt: none in the lanes folder at 15:13.

## Part 1, 15:15 to 17:05

- 15:15:24 Revit opened on the copy, process 29776. 15:16:17 the box "Security - Unsigned Add-In" of Site Adaptation came and the driver pressed Always Load, Bader's order of 6 Nov 23:37 (always_load.txt of the shots folder holds the time).
1. The pane said "Set up this project first, in Settings." on the 3 cards (s01-start.png). Settings, Project setup Edit, the New project window, Save setup (s02-new-project.png, s03-after-save.png). Card 1 then said "Ready, Run 1 Set up site first, it places the site. Then choose the project folder.", the card holds 1 Set up site, Choose project folder and 3 Review data (PR 257).
2. 1 Set up site of card 1: "Empty project found", "The pane names no baseplan drawing, so the tool has no coordinates from CAD", 0 of 0 check points (s04-setup-site.png), F2 seen again. Cancel. The Baseplan row of Advanced set to the Baseplan of S1, "2 of 8 found" (s05-baseplan-row.png).
3. 1 Set up site again: Use CAD, check points from CAD 5 of 5 pass, each Current ticked, Dry run, Apply enabled, Apply at 15:42 (s07-setup-dry.png). Step 1 Done, card 1 "No data check yet. Choose the project folder." (s08-after-setup.png). site.json of the project work folder written 15:42, positionSource cad.
4. Choose project folder opened the new Windows box Select Folder, not the old folder tree. S1 typed in its address bar (folder-S1.png), Select Folder at 15:50: the box "2 Read data needs step 1" (s10-after-select.png), and step 1 back to Ready in the Advanced list. F24 in steps/seam.md.
5. 15:56:10 the driver stopped on its guard: the Teams window had turned from "Meeting compact view | Meeting with ..." to "Meeting with ... | Microsoft Teams", which Bader's order of 00:16 did not name. Nothing was sent from 15:56. The gate cleared at 16:46:59 (idle 3332 s), and at 16:49 the meeting window stood again with input of a person (idle 1 s), so nothing was sent.
6. 17:00 Lead's message, Bader's words of 00:16 "ignore the teams": the same keep awake meeting counts as no meeting window in its full view too. The driver drive3.ps1 of the work root and the gate scripts now leave out both titles of that one meeting, the old driver kept as drive3.before-1700.ps1. Revit in front, 5 minutes idle and the stop on any other input stay.
7. 17:04:44 idle 842 s, Ctrl+S saved the copy (write time 17:04:46) with step 1 applied and S1 chosen. 17:06:00 the driver ended at its limit, "revit left open: False", no lock.

## Part 2, from 17:07

- 17:07:28 Revit opened on the saved copy, process 29020. 17:08:16 the Unsigned Add-In box came again and the driver pressed Always Load (always_load.txt).
1. Card 1 said Ready, "Run 1 Set up site first", step 1 Ready again after the save and reopen, F21 seen again (s20-p2-start.png).
2. Lead's work-around order of 17:00 for F24 was folder first, then 1 Set up site, then Build. On this add-in Choose project folder is grey until step 1 is Done (enabled False by UI Automation at 17:19, R761), so the folder could not come first. Build and 3 Review data were grey too.
3. 17:20 1 Set up site, 5 of 5 pass, Apply: card 1 "No data check yet. Choose the project folder.", Build still grey (s23-p2-after-setup.png).
4. 17:24 Choose project folder, S1 again: this time the box "2 Read data, Dry run done, nothing was changed. Reads the folder and the settings file. Files in the folder: 37, 1317.4 MiB" (s24-p2-folder-box.png). So the work-around of F24 that worked: Set up site, choose the folder (step 1 undone), save and reopen or not, Set up site again, choose the same folder again. Apply 17:25.
5. 17:37, after 694 s of reading, What the tool read: 7 files read, "2,618 of 2,964 villa plots of all plots" (F9 words fixed), Shared parameters Strong (F10 fixed), Group levels Weak "Areas found by distance only: 2" (s26-read-end.png). Close, Confirm what was found with Typology_Rev8.1 (2570 plots) and Neighbourhood_ID (2955 plots), both proposed, Confirm: "Your values are saved. Press Read data again to read with them." F22 stands.
6. 17:43 Choose project folder again, S1, Apply, 655 s: "Areas from the confirmed column: 13". Close, Cancel on Confirm (values stay). Scope under Advanced: Neighbourhood 01A of the list -, 010, 012, 013, 01A, 01B, 02, 021, 022, 023, 03. Card 2: "Scope: the neighbourhood 01A." Steps 1 and 2 Done, Build enabled (s31-scope.png).
7. 18:02 Build on card 2, Preview first ticked. The box "No rules are chosen for this project. Use the rules template of site adaptation manual ... Rev 06?" Yes. Preview of the build: "2615 plots with an outline, 2619 issues", Go on. 2 Read data reused the read, What the tool read Close, Confirm Cancel, "3 Review data, NBH 01A, 278 plots", Save review, Close. 4 Check box "builds NBH 01A on the read ... from 30 inputs", Apply.
8. 18:05 4 Check ran and opened "4 Check, NBH 01A, 297 plots": profile parsons2024Rev6, 0 overrides, All 911 rows, Floor 852, Unit code 16, Level 11, Parameter 28, "3 plots follow every rule" (s35-check.png). F16 is past, Check no longer stops on the missing reads. The window has no Close button. Closed by its frame, the Build asked "4 Check, Nothing changes until Apply" again, 5 times from 18:05 to 18:13, each Check committed (run_4_*.json, plan_01A.json). Save overrides kept the window open. 18:13 Cancel on the next Check box: "Build stopped at Check, cancelled." Step 4 then showed "1 issue", step 5 Ready. F25.
9. 18:25 as a check of the floor step only, Run 5 Floors and ramps of Advanced (Next step): "Floors: place 538, move 0, already placed 0, skip 0, listed 269", Apply. Revit's own box "Warning, Highlighted floors overlap", OK at 18:27 (s40-1-warning.png). The step ended with "271 issues", step 6 Ready.
10. 18:47:40 the copy saved by the Save button of the quick access bar (Ctrl+S with the pane in focus did not save at 18:45). 18:49:25 File, Exit Revit. 18:50:05 "Revit exited", "revit left open: False", no lock, no Revit process.

## Result of the short check

FAIL by the pass of Lead's order: the Build of card 2 did not run to the end, it loops on Check (F25) and was stopped there by Cancel at 18:13. Counts placed:

| Part | Placed | How |
|---|---|---|
| Floors and ramps | 538 floors on 269 plots of 01A: 269 generic, 269 ramp, 0 yard | Run 5 of Advanced, not the Build. floors_01A.json of the project work folder, "placed" 538, counts place 538, move 0, keep 0, skip 0 |
| Road toposolid | none, "made" false | toposolid_01A.json: the surface Top Surface-02 does not cover the scope, 807 of 807 ramp road edge points are off it |
| Levels | 0, step 6 Ready, never ran | |
| Villa links | 0, step 7 never ran | |
| Walls | 0, step 8 never ran | |

Where the Build stopped: step 7 of 11, Check, "Build stopped at Check, cancelled.", after 5 committed Check runs.

S1 after the two parts: 37 files, SHA-256, size and write time equal to s1_before_short.json, 0 changed, 0 new, 0 gone (s1_after_short.json, compared by a script).

Second check by another method: the count of floors is read twice, from the dry run box of Revit by UI Automation at 18:25 ("place 538, listed 269") and from floors_01A.json on disk after the run (538 placed, 269 distinct plots, by a script). Not seen in Revit: a count of the floor elements in the model itself, Revit was closed before it.

# Short check 2, Sat 7 Nov 2026, NBH 01A from card 1

Lead's order of 7 Nov, Bader's order of 14:40: the second short check, not a full run. NBH 01A, marked tuned (steps/truth-audit.md section 6), Bader's answer to F15. The add-in Lead built from main with F16, F4, F24, F6, F13, F25, F26 and F27 and installed with Install-SiteAdaptation.ps1, the .addin file written 22:01. Same driver, drive3.ps1 of the work root under python/window_lock.py, -MaxMinutes 110. Shots under the work root, folder `fast3-revit-test/shots/short2`, never in git, they show real values.

## Model and folder, short check 2

- Model: `FAST3-short2-01A.rvt` in fast3-revit-test of the work root, copied from `FAST3-empty-model-pristine.rvt` at 22:13, `cmp` equal bytes. A new name, not the run 1b copy and not the short check copy.
- Inputs: S1 of the scenarios, 37 files, SHA-256, size and write time taken at 22:13 into s1_before_short2.json of fast3-revit-test, equal to s1_after_short.json (0 changed, 0 new, 0 gone).
- revit-wanted-build-1.txt and revit-wanted-build-3.txt: none in the lanes folder at 22:13. Idle 3987 s, only the keep awake Teams window.

## Part 1, from 22:16

- 22:16:36 Revit opened on the copy, process 218024. 22:18:18 the box "Security - Unsigned Add-In" of Site Adaptation came and the driver pressed Always Load, Bader's order of 6 Nov 23:37 (always_load.txt of the shots folder holds the time).
1. The pane said "Set up this project first, in Settings." on the 3 cards (s01-start.png). Settings, Project setup Edit, the New project window, Save setup (s02-new-project.png). Card 1 then said "Ready, Choose the project folder: 1 Set up site runs first and places the site from its Baseplan. Then choose the folder again to read the data." with Choose project folder enabled (s03-after-save.png).
2. 22:27 Choose project folder, the Select Folder box, S1 typed in its address bar (folder-S1.png), Select Folder: the window 1 Set up site opened by itself with the Baseplan of S1, "Empty project found", Use CAD, 5 of 5 check points pass (s05-after-select.png). F2 and F24 not seen: no Baseplan row of Advanced was needed and no "needs step 1" box came. Each Current ticked, Dry run, Apply on, Apply at 22:31, the window closed in 7 s (s06-setup-dry.png, s07-after-setup.png). Card 1: "No data check yet. Choose the project folder."
3. 22:33 Choose project folder again, S1: the box "2 Read data, Dry run done, nothing was changed ... Files in the folder: 37, 1317.4 MiB" came at once (s08-folder2-box.png). Apply 22:33:57.
4. 22:47, after 806 s, What the tool read: 7 files read, 2,618 of 2,964 villa plots of all plots, Shared parameters Strong, Group levels Weak "Areas found by distance only: 2" (s09-read-end.png). Close, Confirm what was found with Typology_Rev8.1 (2570 plots) and Neighbourhood_ID (2955 plots), both proposed (s10-confirm.png), Confirm: "2 Read data done: 4 files written, 0 issues ... Your values are saved. Press Read data again to read with them." (s11-after-confirm.png). F22 stands.
5. 22:52:02 a driver fault: card 1 now lists the data check, so Choose project folder stood below the screen at y 1434. The driver clicked there, the cursor stopped on the taskbar edge and opened the GlobalProtect panel of the tray. Nothing was pressed in it. Revit came back to front by its window handle at 22:55, and from then each click scrolls the pane first and refuses a point outside the visible pane.
6. 23:00 Choose project folder a third time, S1: the box "Reads the folder, the settings file and the values a person confirmed" (s12-read2-box.png). Apply 23:01:31.
7. 23:14 What the tool read, after 710 s: 7 files read, 2,615 of 2,964 villa plots, Group levels Usable "Areas from the confirmed column: 13" (s13-read2-end.png). Close, Cancel on Confirm (values stay), the box "2 Read data done ... The read stays as it is." Close. Build enabled.
8. 23:19 Advanced, Scope Neighbourhood, 01A of the list -, 010, 012, 013, 01A, 01B, 02, 021, 022, 023, 03, Villa mode Links. Card 2: "Scope: the neighbourhood 01A." (s14-scope.png). Steps 1 and 2 Done, step 3 Ready.
9. 23:28 Build on card 2, Preview first ticked. The box "No rules are chosen for this project. Use the rules template of site adaptation manual ... Rev 06?" Yes (s15-rules-box.png). Preview of the build: "2615 plots with an outline, 2619 issues", Go on (s16-preview.png). 2 Read data "Reuses the read of run 2_20261107-230031-608", Apply (s17-read-reuse.png), with no What the tool read and no Confirm window this time. "3 Review data, NBH 01A, 278 plots", Save review, Close (s18-review.png).
10. 23:33 4 Check box "builds NBH 01A on the read ... from 30 inputs", Apply (s19-check-box.png). "4 Check, NBH 01A, 278 plots": profile parsons2024Rev6, 0 overrides, All 744, Floor 68, Hand 212, Unit code 3, Position 2, Level 11, Parameter 448, Assumed 15, "32 plots follow every rule", a Close button (s20-check.png). Close: the Build went on at once. F25 fixed. Check and Review now both say 278 plots, F28 fixed for those two.
11. 23:36 5 Floors and ramps "place 124, move 0, already placed 0, skip 0, listed 62", Apply (s21-floors-box.png). No Revit overlap box came, F27 box part fixed.
12. 23:37 6 Levels "move 0, at the plan 124, listed 216", Apply (s22-levels-box.png).
13. 23:38 7 Place villas "Links: place 62, swap 0, move 0, already placed 0, skip 0, listed 0, link types to load 0, to reload 0, over the footprint limit 0", Apply (s23-villas-box.png). Revit then upgraded each linked model, "Load Link, The linked model is being upgraded, From Autodesk Revit 2021 To Autodesk Revit 2024", and stopped 4 times on its own box "Structural Analytical Model Upgrade" with one button Close, which the driver pressed each time, Revit in front and no other input. 12 minutes, 23:39 to 23:51. F31.
14. 23:51 8 Walls "place 178, change 0, already placed 0, skip 0, listed 179", Apply (s24-walls-box.png). No window after it.
15. 23:58 Card 2 "Done, 462 issues", "Build done: 8 steps", the Build line ends "Check: 4 Check, NBH 01A, 278 plots, 744 rows, 32 plots follow every rule". Card 3 Ready, "2615 plots have no road level, so no floor level, and steps 6 to 8 leave them out." (s25-card-end.png), F30.
16. 00:00:08 the copy saved by the Save button of the quick access bar, 8,790,016 bytes. 00:01:32 File, Exit Revit. 00:02:01 "Revit exited", "revit left open: False", no lock, no Revit process.

## Result of short check 2

PASS by the pass of Lead's order: the Build of card 2 ran to its end, 8 steps, with no stop at Check, and placed floors and ramps, levels, villa links and walls. But it built 62 of the 278 plots of 01A, F29.

| Part | Placed | How |
|---|---|---|
| Floors and ramps | 124 floors on 62 plots: 62 generic, 62 ramp, 0 yard | floors_01A.json of the project work folder, counts place 124, move 0, keep 0, skip 0 |
| Road toposolid | made, 1 | toposolid_01A.json "made" true, surface Compiled West_MM of the 6 read, 87 warnings taken |
| Levels | 124 at the plan on 62 plots, 0 moved | run_6_*.json committed true, levelPlots 62, atPlan 124, moved 0 |
| Villa links | 62, of 4 prototype files of S1/Prototypes | place_01A.json, counts place 62, swap 0, move 0, keep 0, skip 0 |
| Walls | 178 | walls_01A.json, counts place 178, change 0, keep 0, skip 0 |

Where the Build stopped: it did not stop. The person still answers 1 rules box, Preview, the reuse box of step 2, Review, the boxes of steps 4 to 8 and 4 Revit upgrade boxes.

Why 62 plots: plan_01A.json of the check holds 278 plots, written 62, not written 216. Missing by key: mirrored 211, parameters CASE 213, Prototype Foundation Type 213, levels.ffl 11, Number of Steps 11, Prototype FFL 11, transform 2, unit 2. 11 plots have no road level. Read by a script from the plan file.

S1 after the part: 37 files, SHA-256, size and write time equal to s1_before_short2.json, 0 changed, 0 new, 0 gone (s1_after_short2.json, compared by a script).

Second check by another method: each count is read twice, from the dry run box of Revit by UI Automation (place 124 listed 62, at the plan 124, place 62, place 178) and from floors_01A.json, run_6_*.json, place_01A.json and walls_01A.json on disk after the run, by a script that shares no code with the add-in. The two agree. Not seen in Revit: a count of the elements in the model itself, Revit was closed before it. Not seen: F21 (save and reopen), the copy was not reopened.

# Run 2, Sun 8 Nov 2026, NBH 01A from card 1

Lead's order of 8 Nov 03:20: run 2, the full run of this task on NBH 01A, marked tuned (steps/truth-audit.md section 6), Bader's answer to F15, and the rehearsal of part 2 of steps/demo-live.md. The add-in Lead built from main 1b4f287 (F22, F8, F29, F30 and F31 fixes on top of short check 2) and installed with Install-SiteAdaptation.ps1, the .addin file written 03:18:22. Same driver, drive3.ps1 of the work root under python/window_lock.py, -MaxMinutes 110. Shots under the work root, folder `fast3-revit-test/shots/run2`, never in git, they show real values.

## Model and folder, run 2

- Model: `FAST3-run2-01A.rvt` in fast3-revit-test of the work root, copied from `FAST3-empty-model-pristine.rvt` at 03:31, SHA-256 equal. A new name, not a copy of an older run.
- Inputs: S1 of the scenarios, 37 files, SHA-256, size and write time taken at 03:31 into s1_before_run2.json of fast3-revit-test, equal to s1_after_short2.json (0 changed, 0 new, 0 gone).
- revit-wanted-build-1.txt and revit-wanted-build-3.txt: none in the lanes folder at 03:32. Idle 431 s at 03:37 before the first press, only the keep awake Teams window.

## Part 1, from 03:32

- 03:32:56 Revit opened on the copy, process 230996. 03:34:12 the box "Security - Unsigned Add-In" of Site Adaptation came and the driver pressed Always Load, Bader's order of 6 Nov 23:37 (always_load.txt of the shots folder holds the time).
1. 03:39 the pane said "Set up this project first, in Settings." on the 3 cards (s01-start.png), Advanced closed at the bottom. Settings (the gear), Project setup Edit, the New project window, Start from "New project rules, Rev 06", 1 of 4 files ready (s02-new-project.png).
2. The road pick of F8. The row Road levels (civil file) said "Not chosen" and "No road surface is picked.", with a button Pick surfaces. Pick surfaces before a folder: the window Road surfaces said "The road surface folder holds no road surface files. Pick the folder in the row Road levels, then open this window again." (s03-road-pick.png). Cancel. Browse of that row, the Select Folder box, S1/landxml typed in its address bar (folder-landxml.png), Select Folder: the row said landxml, Found. Pick surfaces again: 6 surfaces offered, each unticked, Compiled West_MM, Missed from Paste, Part 4 Top Surfaces, Top Surface Part 2 East Of East, Top Surface-01, Top Surface-02 (s04-road-list.png). The pick, 03:47: Compiled West_MM and Part 4 Top Surfaces ticked, in that order (s05-road-ticked.png), Use these picks. The row then said "Road surfaces in order (2): Compiled West_MM, Part 4 Top Surfaces" (s06-new-project-picked.png). Save setup 03:47:37 (s07-after-save.png). So Set up offers the pick, no fault, but only after the folder of the row is chosen.
3. The amber line "No rules chosen for this project yet. Open Rules in Settings." Rules Open: "Rules of this project", "No rules chosen for this project yet.", a button Use the manual template, rows M01 on, each source missing, status not confirmed (s08-rules.png). Close.
4. 03:52 Choose project folder, the Select Folder box, S1 typed in its address bar (folder-S1.png), Select Folder: the window 1 Set up site opened by itself with the Baseplan of S1, "Empty project found", 5 of 5 check points pass (s09-setup-site.png). Each Current ticked (Baseplan, landxml, shared_parameters.xlsx), Dry run, Apply on (s10-setup-dry.png), Apply 03:55:13, the window closed at 03:55:24. Card 1: "No data check yet. Choose the project folder." (s11-after-setup.png).
5. 03:56 Choose project folder again, S1: the box "2 Read data, Dry run done, nothing was changed. Reads the folder and the settings file. Files in the folder: 37, 1317.4 MiB ... The old read lacks a file: the project names no neighbourhood schedule (R798). The folder is read instead." (s12-read-box.png). Apply 03:57:14, read 1 of 2 Read data.
6. 04:09:28, after 734 s of reading (Apply 03:57:14), What the tool read: 7 files read, 2,618 of 2,964 villa plots of all plots, Plot outlines Strong, Plot numbers Strong, Group levels Weak "Areas found by distance only: 2", Villa types Weak, Villa blocks Usable, Road edges Weak, Road levels Usable "18 surface files of Land XML found", Coordinates Weak, Prototypes Strong 13 files, Rule sets Missing, Shared parameters Strong (s13-read-end.png). Close.
7. 04:10 Confirm what was found: Villa type column Typology_Rev8.1 of JN Sales Data_Base_Rev09, 2570 plots (proposed), Area column Neighbourhood_ID of PH_1-DLUS, 2955 plots (proposed), the code map proposed from the schedule, and a new part "Hand of each prototype", 13 rows C10 to VL6, each empty with "no guess" (s14-confirm.png). The two proposed columns kept, no hand picked, Confirm 04:10:59. The box: "2 Read data done: 5 files written, 0 issues ... Your values are saved. Press Read data again to read with them. The confirmed values count from the next read, the gathered files of the read are too large to take in Revit." (s15-after-confirm.png). So F22 stands on this add-in, now with its reason, and the second read is still needed. Close. Card 1 lists the data check rows with their marks (s16-card1-after-read1.png).
8. 04:13 Choose project folder a third time, the second read, S1, pressed by its Invoke pattern as it stood below the visible pane: the box "Reads the folder, the settings file and the values a person confirmed. Files in the folder: 37, 1317.4 MiB" (s17-read2-box.png). Apply 04:14:05, read 2 of 2 Read data.
9. 04:26:21, after 736 s of reading (Apply 04:14:05), What the tool read: 7 files read, 2,615 of 2,964 villa plots, Group levels Usable "Areas from the confirmed column: 13. Smaller groups: 138, words Neighbourhood_ID.", Villa types "read from the confirmed column for 2570 of 2964 plots ... 32 with no prototype file. Code map confirmed." (s18-read2-end.png). Close, Cancel on Confirm, the box "2 Read data done: 5 files written, 0 issues ... Nothing was saved. The read stays as it is." (s19-after-cancel.png). Close.
10. 04:29 3 Review data on card 1 before a scope: the box "3 Review data changed nothing, the model is as it was. The run failed: 3 Review data shows one NBH, the scope names none. Choose an NBH in Scope of the pane" (s20-review.png). Close. Advanced, its ExpandCollapse pattern, Scope Neighbourhood, 01A of the list -, 010, 012, 013, 01A, 01B, 02, 021, 022, 023, 03, Villa mode Links, 04:31. Card 2: "Scope: the neighbourhood 01A. The plan and its preview show every plot of stage 1."
11. 04:32:46 3 Review data: "3 Review data, NBH 01A, 278 plots", "0 of 278 checked by the team", rows such as "no type: the confirmed typology column has no code for this plot" and "no mirror: the plot holds no villa block insert" (s21-review.png). Save review, "Review saved.", Close. Card 1 ends with the data check rows, Fix items and 3 Review data (s22-card1-end.png). Card 1 took 03:39 to 04:34, 55 minutes, of which 1470 s in the two reads.

### Card 2, Build, from 04:35

12. 04:35:40 Build on card 2, Preview first ticked (s23-card2-before.png). The box "No rules are chosen for this project. Use the rules template of site adaptation manual 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06? ... They stay not confirmed until a person confirms them." Yes 04:36:20 (s24-rules-box.png).
13. 04:36:23 Preview of the build: "2615 plots with an outline, 2619 issues. Nothing is written to the model yet." (s25-preview.png). Go on 04:36:54.
14. 2 Read data, third press: "Reuses the read of run 2_20261108-041329-266, the folder, the settings file and the values a person confirmed are unchanged." (s26-read-reuse.png). Apply 04:37:36, 3 Review data came at 04:37:40, so about 4 s. "3 Review data, NBH 01A, 278 plots" (s27-review-build.png), Save review, Close 04:38:16.
15. 04:38:43 4 Check box: "builds NBH 01A on the read read_02a0dab15d0ec733 from 26 inputs" (s28-check-box.png), Apply 04:39:14. 04:39:26 "4 Check, NBH 01A, 278 plots": profile parsons2024Rev6, 0 overrides, "No past job was given, so Plot cut shows no rows", "No civil entry file in the setup, so the door level comes from road level plus slope.", All 1058, Plot cut 0, Floor 798, Hand 212, Unit code 3, Position 2, Level 13, Parameter 30, Assumed 15, "0 plots follow every rule" (s29-check.png). Close 04:40:27, the Build went on.
16. 04:40:40 5 Floors and ramps "Floors: place 526, move 0, already placed 0, skip 0, listed 263" (s30-floors-box.png), Apply 04:41:13. No Revit overlap box. The next box at 04:42:12.
17. 04:42:12 6 Levels "Levels: move 0, at the plan 526, listed 15" (s31-after-floors.png), Apply 04:42:46.
18. 04:42:58 7 Place villas "Links: place 263, swap 0, move 0, already placed 0, skip 0, listed 0, link types to load 0, to reload 0, over the footprint limit 0, link types new to the model 13, of an older Revit 13" (s32-villas-box.png), Apply 04:43:39. Revit's window Load Link came at 04:44:06.
19. Step 7 ran 04:43:39 to 05:11:58, 28 minutes for 263 links of 13 prototype files of Revit 2021 (run_timing_7: apply 1679 s, commit 13 s). The driver logged each window of Revit in that time: only Load Link. No stop on "Structural Analytical Model Upgrade", no box was pressed by the driver in step 7 (F31 stop part fixed, R805). The issue lines of R806 are read in part 2.
20. 05:11:58 8 Walls "Walls: place 582, change 0, already placed 0, skip 0, listed 583" (s33-walls-box.png), Apply. 05:12:05 Revit's own box "Warning, Highlighted walls overlap. One of them may be ignored when Revit finds room boundaries. Use Cut Geometry to embed one wall within the other." with OK and Cancel stopped the step (s34-after-walls-win.png). F32. The driver's command was waiting for the end of the Build, so at 05:19:28, Revit in front, idle 417 s (the driver's own last click), only the keep awake Teams window, OK was pressed from outside the driver by BM_CLICK on its handle, no mouse or key input, and written in driver.log. The walls committed at 05:19:29 (run_timing_8: commit 450 s, the wait on that box).
21. 05:20:16 card 2 "Done, 881 issues", its Build line: "Build plan: 2615 plots, 0 ready for every step, 2615 marked, 2619 issues ... Build done: 8 steps ... The Build kept the picks and values you confirmed before and showed no window ... Check: 4 Check, NBH 01A, 278 plots, 1058 rows, 0 plots follow every rule 15 of 278 plots are not built: no floor level 11, no outline or front 2, no unit 2. Built with a value missing: 201 plots, each value tagged missing in the plan." Card 3 Ready, "2619 issues from the last build.", "13 plots have no road level, so no floor level, and steps 6 to 8 leave them out." (s35-card2-end.png).
22. 05:20:47 the copy saved by the Save button of the quick access bar, 15,749,120 bytes, write time 05:20:50. 05:23:23 the driver ended at its limit and closed Revit, "revit left open: False", no Revit process. Part 1 took 03:32:56 to 05:23:23, 110 minutes. Card 2 took 04:35:40 to 05:20:16, 45 minutes, of which 28 in step 7 and 7 on the walls box.

## Result of card 2, run 2

PASS by the pass of Lead's order: the Build ran to its end, 8 steps, with no stop at Check and no stop on the upgrade box of step 7. It built 263 of the 278 plots of 01A (62 in short check 2), F29 fixed and seen in Revit. One new stop, the walls overlap box of Revit, F32.

| Part | Placed | How |
|---|---|---|
| Floors and ramps | 526 floors on 263 plots: 263 generic, 263 ramp, 0 yard | floors_01A.json of the project work folder, counts place 526, move 0, keep 0, skip 0 |
| Road toposolid | made, 1 | toposolid_01A.json "made" true, action make, surface Compiled West_MM of the pick, 433 warnings taken |
| Levels | 526 at the plan on 263 plots, 0 moved | run_6_*.json committed true, moved 0, atPlan 526 |
| Villa links | 263, of 13 prototype files of S1/Prototypes | place_01A.json, counts place 263, swap 0, move 0, keep 0, skip 0, 263 distinct plots |
| Walls | 582 on 263 plots | walls_01A.json, counts place 582, change 0, keep 0, skip 0 |

Plots built of 278: 263. plan_01A.json of the check: plots 278, written 263, not written 15. Missing by key: mirrored 211, levels.ffl 13, Number of Steps 13, Prototype FFL 13, parameters CASE 2, Prototype Foundation Type 2, transform 2, unit 2. roadSurfacesOfFrontCorners: Compiled West_MM 378, Part 4 Top Surfaces 154, none 16, so both picked surfaces gave levels. plotsWithNoRoadLevel 13, each tried on the two picked surfaces, 11 with "no road surface covers the corner within snapM 0.005 m" and 2 with "no front frame, so no front corners to take a road level at". Read by a script from the plan file.

The lines of FAST-11 on plots left out and no road level: card 2 names "15 of 278 plots are not built: no floor level 11, no outline or front 2, no unit 2" and "Built with a value missing: 201 plots", card 3 names "13 plots have no road level", the count of the plan of the scope, not of the whole read (F30 fixed and seen in Revit). The two cards agree: the 13 of card 3 are the 11 with no floor level and the 2 with no front of card 2.

## Part 2, from 05:30, card 3

- revit-wanted-build-1.txt and revit-wanted-build-3.txt: none in the lanes folder at 05:29. Idle 545 s.
- 05:30:29 Revit opened on the saved copy, process 121404. 05:31:24 the box "Security - Unsigned Add-In" of Site Adaptation came again and the driver pressed Always Load (always_load.txt of shots/run2p2).
1. 05:34 the main window had no title yet: Revit was upgrading the 13 linked prototype files of Revit 2021 again on open, and stood on its own box "Structural Analytical Model Upgrade". The driver closed it each time, Revit in front, idle 916 s at the first, only the keep awake Teams window: 05:36:06, 05:38:23, 05:39:46, 05:42:08, 05:43:39, 05:45:11, 05:46:46, 05:48:15, 05:50:11, 05:51:50, 05:53:21, 05:54:51, 05:56:24, 13 times (upgrade_boxes.txt). The model was open at 05:58:00, 27.5 minutes after the start. F33.
2. 05:58 the pane after the save and reopen (s41-p2-open.png): the amber line "15 of 15 rules are not confirmed. Open Rules in Settings.", card 1 Done with its data check rows, card 2 "Done, 881 issues" and "15 of 278 plots are not built: no floor level 11, no outline or front 2, no unit 2. Built with a value missing: 201 plots, each value tagged missing in the plan.", card 3 Ready, "2619 issues from the last build." with Open rules, "13 plots have no road level, so no floor level, and steps 6 to 8 leave them out.", the source mix bar "Values: computed 9879, rule 10907, person entered 2614, missing 10595, other 0." F21 not seen: the steps stayed Done after the reopen. The Advanced list: step 4 Check 1 issue, 5 Floors and ramps 267 issues, 6 Levels 15 issues, 7 Place villas 14 issues, 8 Walls 584 issues, so 881 on card 2. The Prototypes row of Advanced: "These files are Revit 2021 and upgrade on each use, use the 2024 copies. A remake from them takes 20 to 41 minutes."
3. 06:02:52 Report on card 3 (s42-card3-before.png): the box "9 Report, Nothing changes until Apply. Report Excel files: 1" (s43-report-box.png), Apply 06:03:28. The Excel 00101-PSN-01A-XXX-SCH-TU-090000.xlsx of the project work folder written 06:03:32, the run ended 06:03:51 (run_9 log), so 23 s. No window after it. Card 3 Done. Its sheets: Cover, Plot schedule (278 plots), Floors (526 rows), Walls (582 rows), Issues (1058 rows), Overrides, Rules used, Summary, Placed, Skipped, Assumed, Accepted warnings, Settings.
4. 06:21:29 Update from report: the window "Update from report", Browse, the Open box, the report Excel of step 3 typed by Alt+N, Enter. "No changes found in this Excel.", 0 changes found, 0 follow the rules, 0 break a manual rule, 0 not editable ignored, "15 manual rules of the block name no updateChecks, so no change is held against them: M01 ... M15 (R402)". Dry run and Apply 0 changes both off (s46-update-picked.png). Cancel 06:24:00. The model was not changed.
5. 06:24:44 the 3 cards at the end (s47-card3-end.png). 06:25:51 the copy saved by the Save button of the quick access bar, 15,769,600 bytes, write time 06:25:53. 06:26:44 the driver asked to quit, 06:27:10 "revit left open: False", no lock, no Revit process. Part 2 took 05:30:29 to 06:27:10, 57 minutes, of which 27.5 in the reopen.

## Step 7 for FAST-12, R805 to R807

- No stop on "Structural Analytical Model Upgrade" in step 7: 04:43:39 to 05:11:58, the only window of Revit was Load Link, the driver pressed nothing (R805, seen in Revit). The box does stop the reopen of the model, 13 times, F33.
- The box of step 7 said "link types new to the model 13, of an older Revit 13" beside "link types to load 0" (R807, seen in Revit, s32-villas-box.png).
- The issues of step 7: 14, on the Advanced list, which fits 13 upgraded files and one line of the boxes closed (R806). The words of the 14 lines: UNKNOWN, not seen. They are in no file of the work folder and not in the report Excel, the pane shows only the count. A line for Lead under F34 in steps/seam.md.

## Times of run 2

| What | From | To | Minutes |
|---|---|---|---|
| Card 1, setup, road pick, Rules, Set up site, two reads, Confirm, scope, Review | 03:39 | 04:34 | 55 |
| 2 Read data, press 1, a full read | 03:57:14 | 04:09:28 | 12.2 (734 s) |
| 2 Read data, press 2, a full read with the confirmed values | 04:14:05 | 04:26:21 | 12.3 (736 s) |
| 2 Read data, press 3, inside the Build, the read reused | 04:37:36 | 04:37:40 | about 4 s |
| Card 2, Build to its end | 04:35:40 | 05:20:16 | 45 |
| of which step 7 Place villas | 04:43:39 | 05:11:58 | 28 |
| of which the wait on the walls box, F32 | 05:12:05 | 05:19:28 | 7.4 |
| Reopen of the built copy, F33 | 05:30:29 | 05:58:00 | 27.5 |
| Card 3, Report and Update from report | 06:02:52 | 06:24:00 | 21, of which Report 23 s |

## Result of run 2

PASS by the pass of Lead's order: the 3 cards ran to their end on 01A from an empty model, card 1 with the road pick of F8, card 2 with the Build to its end, card 3 with Report and Update from report. The Build placed 526 floors and ramps on 263 plots, 1 road toposolid, 526 levels at the plan, 263 villa links of 13 prototype files, 582 walls, 263 of the 278 plots of 01A built. Three new faults, F32 (the walls overlap box stops step 8), F33 (the reopen stops 13 times on the upgrade box), F34 (card 3 counts the issues of the whole read).

For the demo: the person answers the rules box, Preview, the reuse box of step 2, Review, the boxes of steps 4 to 8, the walls overlap box of Revit, and on a reopen 13 upgrade boxes. The full run needs about 55 minutes for card 1 and 45 for card 2, more than the 30 of the demo script, the reads and step 7 take most of it.

S1 after the two parts: 37 files, SHA-256, size and write time equal to s1_before_run2.json, 0 changed, 0 new, 0 gone (s1_after_run2.json, compared by a script).

Second check by another method: each count is read three ways, from the dry run box of each step by UI Automation (place 526 listed 263, at the plan 526, place 263, place 582), from floors_01A.json, run_6_*.json, place_01A.json and walls_01A.json on disk after the run, and from the model itself, revit_counts_9_*.json that 9 Report wrote from the elements of the reopened model: 263 plots with 1 link each, 263 generic and 263 ramp floors, 0 yard, 263 plots with their levels, 582 walls on 263 plots. The three agree, read by a script that shares no code with the add-in. Not seen in Revit: the words of the issues of step 7 (R806).

The card shots in git: steps/ui-shots/pane-stage-1.png (card 1 at its end, 04:34), pane-stage-2.png (card 2 at its end, 05:20, the coordinate line of the Build blanked), pane-stage-3.png (the 3 cards at the end, 06:24), cut to the pane from the screenshot of the work root. No other window shot was added, the windows of the add-in were shot in run 1.
