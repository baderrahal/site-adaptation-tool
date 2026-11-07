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

steps/ui-shots/: pane-stage-1.png, pane-stage-2.png, pane-stage-3.png, revit-new-project.png, revit-project-rules.png, revit-setup-site.png, revit-scan-results.png, revit-confirm-profile.png, revit-ask-rules.png, revit-build-preview.png, revit-review-data.png, revit-how-it-works.png, revit-update-from-report.png. Each cut from a shot of the real run, a real value blanked where one stood: the check point tables, the code map, the drawing of the plot outlines, the plot numbers, areas and folder path, the coordinates line. Not shot: Road surfaces, the check table of step 4 (never opened as step 4 stopped first), the Site Adaptation Rules window of the ribbon, Use a saved project.

## Not seen in Revit, UNKNOWN

- Floors and ramps, Levels, Place villas, Walls, Report: never ran.
- A person using the windows: every press came from the driver.
