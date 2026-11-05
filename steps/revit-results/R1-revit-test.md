# R1-revit-test, Revit check

Date 2026-11-05. Lane revit-test, Revit 2024.3, one Revit window at a time under python/window_lock.py (round R1-revit-test), Revit closed after each run (tasklist showed 0 Revit processes after every run, the lock file was gone at the end).
Screenshots and result files are under the work root, folder `r1-revit-test`, never in git. They show client file names and real survey coordinates and I could not blank them.
This is the record after the second pass. The empty model and 3 Review data moved to R2-C1-revit-test (Lead, 5 Nov 2026).

## Build

Branch task/R1-revit-test merged with origin/main e437d04, installed with dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1 (one manifest SiteAdaptationAddIn_Revit2024.addin). One Load Once prompt at each cold start, answered Load Once by the watcher, never Always Load.
The check now runs by the launch of the worker that the scan uses (IWorkerStart, the limit addin.workerTimeLimitS.dataCheck of config, the answer of the worker), so Revit does not wait. The first pass started its own process and waited on it, run 8 of that pass hung Revit for 323 s.

## Model and folder

- Model: `R1-model-copy.rvt` in the folder r1-revit-test of the work root, a copy of a model copy of the SETUP-sign run. It is NOT an empty model, it already holds a saved setup. The empty model is in R2-C1-revit-test.
- Folder S1: a saved project `R1-S1.json` in the folder Saved projects of the work root names a copy of the shipped set whose inputRoot is scenarios\S1 of the work root, picked with Use a saved project of the pane. The real input folder was not used.

## What was pressed and seen, run10 of r1-revit-test (the build of the PR)

1. Use a saved project, the file dialog took the path of R1-S1.json (a-dialog.png, b-after-pick.png).
2. Run 1 Set up site opened the window 1 Set up site, Apply pressed, a note window said what Apply removed and was closed, the pane showed the next step Run 2 Read data (c-after-1.png, note-085507.png, c3-before-2.png).
3. Run 2 Read data, 08:55:48. The flow showed its dry run confirm "Dry run done, nothing was changed" (e-confirm-085557.png), Apply was pressed at 08:55:58.
4. The data check ran in the worker, Revit stayed answering (no UIA call of the driver timed out), and the window What the tool read opened at 08:57:19, about 80 s after the confirm (d-window.png). It shows only the check:
   - the band line "The tool read the folder you gave and says what it holds for each need. Nothing changes in the model."
   - counters 5 files read and 2,964 plots found, no conflict counter, no badge of step 3
   - the section Data check, 11 rows, marks Strong 3, Weak 5, Usable 1, Missing 2
   - the section Files not read, 1 file with the exact reason the worker wrote (the free memory is under what its read needs and the floor)
   - one button, Close, and the foot line "The check wrote nothing in your folder and nothing in the model."
   - no Where each value comes from, no Files read, no side column, no Cancel, Export or Use these picks.
   The first row is still cut by the worker at "1 drawing could ...", its line is limited to 160 characters by R605, the full reason of a file is the section Files not read.
5. The read behind the check cannot go on on S1 (it asks for past schedules, ReadInputs.cs line 189 in Common, R2-C1-build-1). With that the window is shown alone, then the stop of the read is told.
6. The driver closed Revit with the window open, the Close button was not pressed in this run.

## Plot count against plots.json

- Window counter plots found: 2,964, read from plots.json of the run.
- Window text of the row Plot numbers: 2964 outlines hold a number and 2964 numbers are distinct.
- plots.json of this run (datacheck folder of the work folder of the model copy), counted by ConvertFrom-Json: 2964 plots, all numbered. plots.json of the lead scenario run of S1: 2964.
- Files read 5 against data_check.json of the run: 6 files, 5 read, 1 not read.

## Not seen in Revit, UNKNOWN

- The window as a person sees it. The press, the screenshot and the UIA text come from a driver.
- The window with the values of the scan in it (the case where the read goes on). It needs a folder the read accepts, R2-C1-build-1.
- A check that fails (time limit, no start, no result), covered by tests only.
- An empty model, 3 Review data, buttons 4 to 9.
- The pass 1 hang of Revit is not seen again, the check ran 80 s with Revit answering. How the machine felt to a person during those 80 s is UNKNOWN.
- Cancel at the dry run confirm: the flow's own Cancel is RunEnd.Cancelled, it comes before the check. A Cancel inside the window of the scan after the check is told as a stop of the read by the scan, as before. A cancelled end after the worker answered needs a change in Common, not made.
