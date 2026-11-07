# FAST-12-build-3, Revit check

8 Nov 2026, build-3.

## What was opened

Nothing. Revit was not opened by this lane for FAST-12. The PR carries the label revit-later. The check is owed by run 2 of revit-test from 06:00, which runs step 7 on prototypes of Revit 2021.

Why not tonight:
- Step 7 runs only on the plan of a real run of 4 Check, held by its SHA-256 against the run record (PlanSource.Open, dotnet/SiteAdaptation.Common/PlaceVillas/IVillaDocument.cs). To link copies of the prototypes and not the files of the project folder, a copy would need steps 1 to 6 run again with Set up on the copies, about 1 hour 20 by the driver, or a plan and run record edited by hand, which proves nothing the tool does.
- Installing this build into the add-in folder of the machine would change the add-in build-1 and the 06:00 run of revit-test use.

## What was measured instead, read only, off Revit

Python over the logs of the work root, no repo code.
- The box id. addin_run/LB6E_20261101-184355/buttons_result.json, step button7_masses (button 7, 336 links placed): dialog TaskDialog_Views_Related_To_Analytical_Changed, message "Some annotations, schedules, view templates, filters, and views related to analytical elements might be modified or lost during the upgrade process.", answer 8 (Close), accepted true. So Revit 2024 raises DialogBoxShowing for this box while step 7 loads links, and takes Close from a handler.
- The same id with Close taken 66 times in the testhost logs of the prototype upgrade jobs (testhost_*_upgrade_log.json), the Revit 2021 prototypes opened in 2024.
- The box of F31. fast3-revit-test/shots/short2/driver.log lines 101 to 107: "clicked Close of Structural Analytical Model Upgrade" at 23:43:46, 23:46:14, 23:48:09 and 23:51:09, during step 7.

## Owed to Revit

- Step 7 on 2021 prototypes ends with no stop on "Structural Analytical Model Upgrade".
- Its issues hold "link <file> was a Revit 2021 file and was upgraded to 2024 as it loaded (R806)" for each file and "the tool closed the Revit box Structural Analytical Model Upgrade N times while the links loaded (R805)".
- Its box shows "link types new to the model N, of an older Revit N" and a line per file.
- BasicFileInfo.Format gives the year, "2021", as R806 reads it. UNKNOWN until seen.

Not seen in Revit: R805, R806, R807.
