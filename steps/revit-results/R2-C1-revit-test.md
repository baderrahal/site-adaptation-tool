# R2-C1-revit-test, Revit check

Date 2026-11-05. Lane revit-test, Revit 2024.3, one Revit window at a time under python/window_lock.py (round R2-C1-revit-test). Revit was closed after each of the three passes, tasklist showed 0 Revit processes each time, and the lock file was gone at the end.
Screenshots and result files of the real run show client file names, the user name of this machine and plot data. They stay under the work root, folder `r2-c1-revit-test/shots`, never in git. The one picture in git is the draft `steps/ui-shots/confirm-profile.png`, made with invented names and codes.

## Build

Branch task/R2-C1-revit-test, installed with dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1 (one manifest). One Load Once prompt at each cold start, answered Load Once by the driver, never Always Load. The data check runs in the worker by the launch the scan uses (IWorkerStart). Nothing in the add-in starts its own process or waits.

## Model and folder

- Model: `R2-empty-model.rvt` in the folder r2-c1-revit-test of the work root, a copy of the project a test job made from the Revit metric template. Nothing was run on it before. Proof that it is empty: the window 1 Set up site opened with the badge "Empty project found" and the text "This model has no site coordinates" (`r2-c1-revit-test/shots/drv/d-setup-window.png`), the pane opened on "Start here, Set up this project" with no saved setup (`shots/drv/a-start.png`), and the model browser holds only the template views, Level 1 and Level 2. All three passes opened this same file, never saved, so each pass started from the empty model.
- Folder: S1 of the work root, named by inputRoot of a copy of the shipped config. The config copy is the shipped config/project.json with inputRoot changed to S1. A first copy of an older config lacked dataCheck.skipFolders and button 2 stopped in plain words, see Seen below.
- S1 was only read. The name, size and write time of every file in it gave the same fingerprint before and after (`r2-c1-revit-test/s1_before.txt` against the same listing after the last pass).
- The saved project of the pane named the config, mode rev6 and no input file. All the file rows of the pane stayed "Not chosen" during the whole run.

## What was pressed and seen

Pass 1 (`shots/drv`), from the empty model:
1. Use a saved project, file dialog, the saved project of S1 (`b-dialog.png`, `c-after-pick.png`).
2. Run 1 Set up site. The window said Empty project found and kept Apply off until a source was given. Typed an invented position (Easting 500000, Northing 2000000, Angle 0), Use typed values, Dry run, then Apply (`e-setup-typed.png`, `h-after-apply.png`). The pane moved to Read data.
3. Run 2 Read data with no file in the pane rows. First it stopped with "dataCheck.skipFolders is no list of folder names, an empty list is allowed (R635)" because my copy of the config was old (`i-read-preview.png`). After the config was replaced by the shipped one, the dry run said "Dry run done, nothing was changed. Reads 2 files, 1317.5 MiB" (`l-after-read-press.png`). Stage 1 did not stop on a past file.
4. Apply. The worker ran 12:36:09 to about 12:49, 13 minutes. The worker monitor window said "Running outside Revit" with the elapsed time. Revit answered every probe, a UI Automation call took 17 to 46 ms in 8 looks (`watch.log` of the scratch folder, `n-during-read.png`). A second press at 12:37:06 said "2 Read data is running already, its worker has not ended" and started nothing (`m-read-running-10s.png`).
5. The window What the tool read opened with the check from the read folder (`p-check-window.png`): 7 files read, 2,964 plots found, 11 rows of marks (strong 3, weak 5, usable 1, missing 2). No second check ran.
6. Closed it, then the window Confirm what was found opened (`q-after-close.png`) with the villa type column and the area column proposed, a code map of 16 old and new pairs, 13 prototypes each with "no guess" for the hand, and the group levels. Confirm with a code map line of three words was refused in the window with "Line 6 of the code map needs two codes" and nothing was saved (`r-confirm-refused.png`). That was a real fault: a code with a note in brackets has spaces. Fixed with an arrow, old -> new.
7. Cancel gave "2 Read data done: 4 files written, 0 issues. Nothing was saved. The read stays as it is." and no confirmed_profile.json (`s-after-cancel.png`).

Pass 2 (`shots/drv2`), after the fix, Revit restarted, step 1 again because the config file had changed:
8. Run 2 Read data, dry run said "Reuses the read of run 2_20261105-123609-964, its 2 input files are unchanged" (`p2-e-read-preview.png`). Apply showed the check from that same folder, 7 files read, 2,964 plots (`p2-f-confirm-window.png`). The check was not run again.
9. The confirm window with arrows (`p2-g-confirm-window.png`). Set the hand of one prototype to right and the word of group level 1 by UI Automation, pressed Confirm. The note said "Saved confirmed_profile.json. Press Read data again to read with it." The file holds codeMap 16 rows, hands 1, levels 1, a typology column and an area column.
10. Run 3 Review data first stopped in plain words, "shows one NBH, the scope names none. Choose an NBH in Scope of the pane" (`p2-k-review-press.png`). With the scope on one of the two areas found by distance it opened "3 Review data, NBH L1-1, 2963 plots" (`p2-q-review-dry.png`).
11. Fault found here: after Confirm the pane still said Review data was next although the read was now stale. Fixed: confirmed_profile.json is an input of step 2 in the work folder.

Pass 3 (`shots/drv4`), after the second fix. A first start of this pass lost the add-in because the driver was waiting and did not answer the Load Once prompt, so Revit was closed and started again, driver fixed. Not a fault of the add-in.
12. Use a saved project, 1 Set up site (typed invented position, Dry run, Apply), then Run 2 Read data. The dry run now said "Reads 3 files, 1317.5 MiB", the confirmed file counted as an input, a new read (`p3-d-read-preview.png`).
13. Apply. Worker 13:39:38 to 13:52, 13 minutes, 10 looks at Revit with a UI Automation call of 19 to 64 ms, no look failed. Check window, 7 files read, 2,964 plots (`p3-f-check-window.png`).
14. The confirm window now starts on the confirmed values: both columns marked "(confirmed)", the code map line "Confirmed on an earlier read", the hand of the prototype set earlier still right (`p3-g-confirm-window.png`).
15. Cancel gave the same "Nothing was saved. The read stays as it is." and the pane moved to Review data (`p3-h-after-cancel.png`).
16. Scope, Neighbourhood, one of the 14 areas of the confirmed area column, Run 3 Review data: "3 Review data, 386 plots" (`p3-k-review.png`).

## Plot count against plots.json

- Pass 1 read, folder `read_125fd91d15620fe8` of the work folder of the model: plots.json 2964 plots, read_plotTable.json 2964 rows (area by distance: 2963 and 1), window counters 2,964 plots found, 3 Review data 2963 plots for the first area.
- Pass 3 read, folder `read_dd9076dfe813d0fa`: plots.json 2964, read_plotTable.json 2964, window counter 2,964, areas 14 values on 2955 plots, the area the review window opened holds 386 rows of the table, the window said 386.
- data_check.json of both: 7 files, 7 read, stopped empty. Counts of the read: data check drawings read 3, files not read 0.

## Group, hand, code map and screenshot summary

- Code map: proposed 16 pairs, typed by the person or kept, saved as source person entered, shown again as confirmed (steps 6, 9, 14).
- Hand of each prototype: 13 prototypes, every one "no guess" on S1, the person gave one, saved and shown again (9, 14).
- Group levels: the word of level 1 typed and saved, shown again (9). The window lists two levels.
- Screenshots: draft in git `steps/ui-shots/confirm-profile.png`. Real run in the work root `r2-c1-revit-test/shots/drv`, `drv2`, `drv3` (the lost start), `drv4`.

## Not seen in Revit, UNKNOWN

- A person using the windows. The presses and the pictures come from a driver, several clicks were sent twice because the first only focused a window.
- A worker that runs out of time or fails in this run. Covered by tests and the existing words of the flow only.
- A check that stops (data_check.json with a stopped note). Covered by the test A_check_that_stopped_shows_its_note_opens_no_confirm_window_and_fails_in_plain_words, not seen in Revit.
- Reusable at ReadDataRun.cs line 305 (Lead item 4). By reading the code, not seen in Revit: a stopped check still ends the worker as done, DataCheckCommand.cs lines 55 to 66 write data_check.json, project_profile.json and plots.json after an exception and the answer is done, so ReadDataRun.Answered writes read.json and Reusable gives that folder back for the same files. The same holds when files were not read for want of memory, the record keeps them. The folder is never retried until a file or the confirmed file changes. A raw read that failed, with no worker answer or an answer that is not done, writes no read.json and is not reused. The fix belongs in Common, build-1.
- Whether the villa type column of 3 Review data shows the confirmed typology. The plot table holds typologyRev9 for 2570 plots, the rows on screen showed "not read yet" for the ones I saw. UNKNOWN which rows hold a value.
- The line "Read from" in the header of 3 Review data is empty on this read. Not looked into.
- Buttons 4 to 9.
