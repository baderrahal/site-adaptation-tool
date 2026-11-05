# R2-C1-revit-test, Revit check

Date 2026-11-05. Lane revit-test, Revit 2024.3, one Revit window at a time under python/window_lock.py (round R2-C1-revit-test). Revit was closed after each pass, tasklist showed 0 Revit processes each time, and the lock file was gone at the end.
Screenshots and result files of the real run show client file names, the user name of this machine and plot data. They stay under the work root, folder `r2-c1-revit-test/shots`, never in git. The one picture in git is the draft `steps/ui-shots/confirm-profile.png`, made with invented names and codes.

The window Confirm what was found is CHOSEN by Lead: no sketch covers it, so this is the smallest version, and Bader sees the draft. Also CHOSEN by Lead after review 1: a press on Confirm is a person confirming both columns and the code map as the window shows them, a hand guess does not start selected, a level word is written only if typed or changed.

Every press and every value set here came from a driver of this lane, not from a person. Where a line says the person gave a value, read that the driver set it through the real window.

## Build

Branch task/R2-C1-revit-test, installed with dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1 (one manifest). One Load Once prompt at each cold start, answered Load Once by the driver, never Always Load. The data check runs in the worker by the launch the scan uses (IWorkerStart). Nothing in the add-in starts its own process or waits. Passes 1 to 4 ran builds before review 1, so the words and the starting state of the window differ from the final build, see Final build below.

## Model and folder

- Model: `R2-empty-model.rvt` in the folder r2-c1-revit-test of the work root, a copy of the project a test job made from the Revit metric template. Nothing was run on it before. Proof that it is empty: the window 1 Set up site opened with the badge "Empty project found" and the text "This model has no site coordinates" (`r2-c1-revit-test/shots/drv/d-setup-window.png`), the pane opened on "Start here, Set up this project" with no saved setup (`shots/drv/a-start.png`), and the model browser holds only the template views, Level 1 and Level 2. Every pass opened this same file, never saved, so each pass started from the empty model.
- Folder: S1 of the work root, named by inputRoot of a copy of the shipped config. The config copy is the shipped config/project.json with inputRoot changed to S1. A first copy of an older config lacked dataCheck.skipFolders and button 2 stopped in plain words, step 3.
- The saved project of the pane named the config, mode rev6 and no input file. All the file rows of the pane stayed "Not chosen" during the whole run.

## What was pressed and seen

Pass 1 (`shots/drv`), from the empty model:
1. Use a saved project, file dialog, the saved project of S1 (`b-dialog.png`, `c-after-pick.png`).
2. Run 1 Set up site. The window said Empty project found and kept Apply off until a source was given. The driver typed an invented position (Easting 500000, Northing 2000000, Angle 0), Use typed values, Dry run, then Apply (`e-setup-typed.png`, `h-after-apply.png`). The pane moved to Read data.
3. Run 2 Read data with no file in the pane rows. First it stopped with "dataCheck.skipFolders is no list of folder names, an empty list is allowed (R635)" because my copy of the config was old (`i-read-preview.png`). After the config was replaced by the shipped one, the dry run said "Dry run done, nothing was changed. Reads 2 files, 1317.5 MiB" (`l-after-read-press.png`). Stage 1 did not stop on a past file.
4. Apply. The worker ran 12:36:09 to about 12:49, 13 minutes. The worker monitor window said "Running outside Revit" with the elapsed time. Revit answered every probe, a UI Automation call took 17 to 46 ms in 8 looks (`shots/watch.log`, `n-during-read.png`). A second press at 12:37:06 said "2 Read data is running already, its worker has not ended" and started nothing (`m-read-running-10s.png`).
5. The window What the tool read opened with the check from the read folder (`p-check-window.png`): 7 files read, 2,964 plots found, 11 rows of marks (strong 3, weak 5, usable 1, missing 2). No second check ran.
6. Closed it, then the window Confirm what was found opened (`q-after-close.png`) with the villa type column and the area column proposed, a code map of 16 old and new pairs, 13 prototypes each with "no guess" for the hand, and the group levels. Confirm with a code map line of three words was refused in the window with "Line 6 of the code map needs two codes" and nothing was saved (`r-confirm-refused.png`). That was a real fault: a code with a note in brackets has spaces. Fixed with an arrow, old -> new.
7. Cancel gave "2 Read data done: 4 files written, 0 issues. Nothing was saved. The read stays as it is." and no confirmed_profile.json (`s-after-cancel.png`).

Pass 2 (`shots/drv2`), after the fix, Revit restarted, step 1 again because the config file had changed:
8. Run 2 Read data, dry run said "Reuses the read of run 2_20261105-123609-964, its 2 input files are unchanged" (`p2-e-read-preview.png`). Apply showed the check from that same folder, 7 files read, 2,964 plots (`p2-f-confirm-window.png`). The check was not run again.
9. The confirm window with arrows (`p2-g-confirm-window.png`). The driver set the hand of one prototype to right and the word of group level 1 by UI Automation (`p2-i-after-confirm.png`), then pressed Confirm. The note said the file was saved and to press Read data again (`p2-j-after-invoke.png`). Read back from disk, not from a screen: the file holds codeMap 16 rows, hands 1, levels 1, a typology column and an area column.
10. Run 3 Review data first stopped in plain words, "shows one NBH, the scope names none. Choose an NBH in Scope of the pane" (`p2-k-review-press.png`). With the scope on one of the two areas found by distance it opened "3 Review data, NBH L1-1, 2963 plots" (`p2-q-review-dry.png`).
11. Fault found here: after Confirm the pane still said Review data was next although the read was now stale, the same shot `p2-j-after-invoke.png`. Fixed: confirmed_profile.json is an input of step 2 in the work folder. The fix was seen as "Reads 3 files" in step 12. The pane line after a Confirm on the fixed build is step 21.

Pass 3 (`shots/drv4`), after the second fix. A first start of this pass, `shots/drv3`, lost the add-in because the driver was waiting and did not answer the Load Once prompt, so Revit was closed and started again, driver fixed. Not a fault of the add-in.
12. Use a saved project, 1 Set up site (the driver typed the invented position, Dry run, Apply), then Run 2 Read data. The dry run now said "Reads 3 files, 1317.5 MiB", the confirmed file counted as an input, a new read (`p3-d-read-preview.png`).
13. Apply. Worker 13:39:38 to 13:52, 13 minutes, 10 looks at Revit with a UI Automation call of 19 to 64 ms, no look failed (`shots/watch4.log`). Check window, 7 files read, 2,964 plots (`p3-f-check-window.png`).
14. The confirm window now starts on the confirmed values: both columns marked "(confirmed)", the code map line "Confirmed on an earlier read", the hand set earlier still set (`p3-g-confirm-window.png`).
15. Cancel gave the same "Nothing was saved. The read stays as it is." and the pane moved to Review data (`p3-h-after-cancel.png`).
16. Scope, Neighbourhood, one of the 14 areas of the confirmed area column, Run 3 Review data: "3 Review data, 386 plots" (`p3-k-review.png`).

Pass 4 (`shots/drv5`), the build before review 1, same model:
17. Use a saved project, 1 Set up site (invented position, Dry run, Apply), Run 2 Read data. The dry run said "Reuses the read of run 2_20261105-133841-555, its 3 input files are unchanged" (`p4-d-read-preview.png`), so the confirmed read of pass 3 was reused and no worker started.
18. Apply showed the check window (`p4-e-check-window.png`), then the confirm window on the confirmed values (`p4-f-confirm-top.png`). Scrolled down, the sections Hand of each prototype (13 rows, one set to right, the code is not named here) and Group levels (Level 1 word set earlier, Level 2 empty) show (`p4-g-confirm-levels.png`). Cancel gave the line that nothing was saved (`p4-h-after-cancel.png`). Revit closed, 0 processes, no lock file.

## Final build

Pass 5 (`shots/final`), the final build, head 99dee33 with the fixes of review 1, same empty model. All presses by the driver, which now refuses any click, key or picture unless Revit is in front and no input but its own came, see the end of this section.
19. Use a saved project, 1 Set up site (invented position, Dry run, Apply), Run 2 Read data with the pane rows not chosen. The dry run said "Reuses the read of run 2_20261105-142934-704, its 3 input files are unchanged" (`f2-read-preview.png`). No worker started.
20. Apply showed the check window, 7 files read, 2,964 plots (`f3-check-window.png`), then the window Confirm what was found (`f3-confirm-top.png`). Its first line says what was found and confirmed before, the second what a press on Confirm takes, CHOSEN by Lead. Both columns say "(confirmed)", the code map says "Confirmed on an earlier read". No guess words stand in the help, as S1 has no guess, and every row says "no guess". The level row says "confirmed on an earlier read" beside its word, Level 2 is empty (`f3-confirm-levels.png`).
21. The driver set a word for level 2 (`f4-word-set.png`) and pressed Confirm. The note said "Your values are saved. Press Read data again to read with them." and the pane moved to Next step Read data (`f4-after-confirm.png`). This is the fix of step 11 seen in Revit.
22. To spare a read of 13 minutes the driver put back a copy of the confirmed file it took before step 21, byte for byte (`cmp` said the same at the end), which is no press of the tool. Run 2 Read data then reused the read (`f5-read-preview.png`), the check again (`f6-check-again.png`), the confirm window and Cancel, "Nothing was saved. The read stays as it is." with the pane on Review data and the scope on one area (`f7-note-and-pane.png`, `f6-scope.png`).
23. Run 3 Review data opened on 386 plots (`f7-review.png`). Find plot took a plot that holds a confirmed type, and the row and the side show it with its type on screen (`f8-find.png`, `f8-picked.png`). The picture is under the work root as it shows a real plot number and code. Against the files, by a script, no value printed: the table and plots.json hold the same type as the screen, the code as read differs from it, and the read before any Confirm gave that plot another type, so the type on screen is the confirmed one. Plots with a type: 2261 before any Confirm, 2570 after. The villa type of a plot has source computed and the blocks of the profile person entered, as R632 says. The rows above it show not read yet as the table holds no type for them, R2-C1-fix-build-1 owns that line.
24. Close, Revit closed, 0 processes, no lock file (`f8-after-close.png`, `driver.log`).

A first try of this pass, 16:01 to 16:20, was stopped by me and is not counted. A person was in a live meeting on this machine, which was not expected. The driver sent its input while Revit was in front until 16:18:31, and that one click, at the place of the button Run 2, landed on the meeting window: the run folder holds no run of it, and the next picture showed the meeting in front. I took no further input, deleted that picture, stopped my Revit by its process id and kept no picture of that try. The click fell on the shared content of the meeting, so as far as I can see it did nothing there, but I cannot prove that from here. I then waited, reading only, until no meeting window stood and the machine had 5 minutes with no input, and pass 5 began at 16:51.

## Plot count against plots.json

- Pass 1 read, folder `read_125fd91d15620fe8` of the work folder of the model: plots.json 2964 plots, read_plotTable.json 2964 rows (area by distance: 2963 and 1), window counters 2,964 plots found, 3 Review data 2963 plots for the first area.
- Pass 3 read, folder `read_dd9076dfe813d0fa`: plots.json 2964, read_plotTable.json 2964, window counter 2,964, areas 14 values on 2955 plots, the area the review window opened holds 386 rows of the table, the window said 386.
- data_check.json of both: 7 files, 7 read, stopped empty. Counts of the read: data check drawings read 3, files not read 0.

## S1 unchanged

`scenario_tools.manifest_faults` of the S1 folder against `scenarios/S1_manifest.json`, by SHA-256 of each file: 37 files in the manifest, faults 0. The output of the command, run at 17:06 after the last pass, is `files in the manifest 37`, `faults 0`.

## Group, hand, code map and screenshot summary

- Code map: proposed 16 pairs, kept by the driver and saved, the read then tags them source person entered as CHOSEN by Lead, shown again as confirmed (steps 6, 9, 14, 20).
- Hand of each prototype: 13 prototypes, every one "no guess" on S1, the driver set one, saved and shown again (9, 14, 18, 20).
- Group levels: the word of level 1 set by the driver and saved (9), shown again (18, 20). The window lists two levels.
- Screenshots: draft in git `steps/ui-shots/confirm-profile.png`. Real run in the work root `r2-c1-revit-test/shots/drv`, `drv2`, `drv3` (the lost start), `drv4`, `drv5`, `final`.

## Not seen in Revit, UNKNOWN

- A person using the windows. The presses and the pictures come from a driver, several clicks were sent twice because the first only focused a window.
- A worker that runs out of time or fails in this run. Covered by tests and the existing words of the flow only.
- A check that stops, a code map whose direction is a guess, a hand guess, the removal of a cleared file and the discard prompt of a refused file. Covered by R636_ConfirmWindowTests.cs only. S1 has a sure code map and no hand guess, so its window shows none of these.
- The line "Read from" in the header of 3 Review data is empty on this read. Cause, no longer unknown: ReviewLoad.cs line 19 and line 77 name the band from the roles baseplan, villaBlocks and curbs, which a read of a folder does not hold. R2-C1-fix-build-1 owns it.
- Owned by R2-C1-fix-build-1, not touched here: ReadDataRun Reusable of a stopped check, the dry run count, the Read from line, "not read yet" on a finished plot, the raw read when one past file is missing, the IOException in RawConfig.
- A real input root of many thousand files at a pane refresh. Measured on S1 only, 37 files, in the PR body.
- Buttons 4 to 9.
