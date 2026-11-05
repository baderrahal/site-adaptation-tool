# R1-revit-test, Revit check

Date 2026-11-05. Lane revit-test, Revit 2024.3, one Revit window at a time under python/window_lock.py (round R1-revit-test), Revit closed after each run (tasklist showed 0 Revit processes after every run, the lock file was gone at the end).
Screenshots and result files are under the work root, folder `r1-revit-test`, never in git. They show client file names and real survey coordinates and I could not blank them.

## Build

Branch task/R1-revit-test from origin/main 5ad1e5d, installed with dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1 (one manifest SiteAdaptationAddIn_Revit2024.addin). One Load Once prompt at each cold start, answered Load Once by the watcher, never Always Load.

## Model and folder

- Model: `R1-model-copy.rvt` in the folder r1-revit-test of the work root. It is a copy of a model copy of the SETUP-sign run, so it is NOT an empty model. It already holds a saved setup and a position, and step 1 showed "Existing site file" with 2 of 2 check points passing. The task asks for an empty model, that part is not met.
- Folder S1: a saved project `R1-S1.json` in the folder Saved projects of the work root names a copy of the shipped set whose inputRoot is scenarios\S1 of the work root. It was picked with the button Use a saved project of the pane. The real input folder was not used.

## What was pressed and seen

Six runs. The first three found the way to the pane, the clicks and a dialog, the last three are the pressing. Run folders: run1 to run6 in r1-revit-test.

1. The pane Site Adaptation is open at the start of the model, so the ribbon tab was not needed. The earlier problem of the tab under the overflow arrow does not stop this.
2. Use a saved project, the file dialog took the path of R1-S1.json (a-dialog.png, b-after-pick.png).
3. Run 1 Set up site opened the window 1 Set up site (c-after-1.png). Apply was pressed (run3 to run6). A note window then said what Apply removed, closed with its Close button (note-*.png). The pane then showed the next step Run 2 Read data (c3-before-2.png). A dry run press in the same window (run2) showed no visible result in 40 s, UNKNOWN what it did.
4. Run 2 Read data, run6, after the preview ran the worker command dataCheck on the input folder: the window What the tool read opened 72 s after the press (07:17:21 to 07:18:33) with the section Data check at the top. d-window.png. 11 rows, one per need, in the order of R605: marks Strong 3, Weak 5, Usable 1, Missing 2 (counted from the UIA text of the window, result.json of run6, key window2). Cancel was pressed, the window closed (f-after-cancel.png) and button 2 stopped with nothing written.
5. Before the change of the preview (run4 and run5) button 2 stopped with "the schedule of IMC, the fit schedule, is not on disk", a stop of ReadInputs.cs line 189 in Common. That stop is the old read asking for past job schedules a new folder does not have. The data check now runs before it. The old read behind it still stops the same way on S1, that is R1-build-1 (Common) and not this task.
6. 3 Review data was NOT pressed. It follows 2 Read data, and 2 Read data cannot finish on S1 for the reason in 5. Review data is not seen in Revit.

## Plot count against plots.json

- Window text of the row Plot outlines and Plot numbers: 2992 outlines found, 2964 with a number of their own, 2964 numbers distinct.
- plots.json of this run (datacheck_2_20261105-071721-290 in the work folder of the model copy): 2964 plots, 2964 with a number.
- plots.json of the lead scenario run S1 (scenarios\runs\S1): 2964 plots, 2964 with a number.
- The three agree on 2964. The window reads them from data_check.json and I counted plots.json by a separate route (ConvertFrom-Json, not the code of the add-in).

## What a person still needs to see

- The window as a person sees it. The press, the screenshot and the UIA text are from a driver, a driver press proves nothing about layout or colour to a person. UNKNOWN.
- The window is wide and plain: the section Data check is on top, but under it the old sections Where each value comes from and Files read are empty, and the top counters read 0 files read, 0 plots found, 0 conflicts next to 2964 plots in the section. That contradicts the section. Not changed here, a question for Bader (the layout is CHOSEN by Lead).
- The check runs inside the preview on the Revit thread and took about 72 s with Revit not answering. A progress line is not built. A failed check is silent today (the read goes on), UNKNOWN how Bader wants it said.
- An empty model with nothing pre-run: not done, see Model.
- Button 3 Review data, buttons 4 to 9: not seen in Revit.
