# build-2, FAST-2-build-2

The task is open, it ships in several PRs. Updated 6 Nov 2026.

## Merged

- PR 229: item 1, R700, sharedParameters strong from the parameter sheet the dataCheck request names. Item 6 Python side, R701.
- PR 233: item 2, R702 no past share on a new project, R703 a Source column on Plot schedule, Floors and Walls. Item 6 step 1 in the tests, item 12 the Plot note.

## This PR, PR 3

- Item 3, R704: UpdateChange.NewEntry. UpdateFromReport.Compare takes newProject, the worker UpdateFromReportWorker.Command tells it by ReportSheets.NewProject. Apply writes the new entry. python/update_check.py the same with --new-project.
- Item 8, R705: PlanPlots.Make adds one issue per plot with no ffl, PlanTexts.NoRoadLevel.
- Item 7, R706: RuleNumbers reads M14, PlanPlots adds boundaryWallThicknessM, PlanSteps counts it for step 8.
- Item 10: AddinRunNames.DockedPage in Common/AddinRun.cs.

## What calls what

- The add-in Update from report window calls the worker command updateFromReport, which calls Compare with newProject. The add-in calls Apply with the list read back, NewEntry rides in the JSON of UpdateListJson.
- siteBuild calls PlanPlots.Make, which calls RuleNumbers and adds the issues.

## Next

- PR 4: item 6, the worker and the listener hold no floor, siteBuild.minFreeGB out of config, on branch task/FAST-2-build-2-mem.
- Item 9, the R310 CIM limit.
