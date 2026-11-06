# build-2, FAST-2-build-2

The task is open, it ships in several PRs. Updated 6 Nov 2026.

## Merged

- PR 229, item 1, R700: dataCheck reads the parameter sheet the request names as input role parameterSheet, need sharedParameters strong on S1. Item 6 Python side, R701.

## This PR, PR 2 of FAST-2

- Item 2, R702: ReportSheets.NewProject(config) in dotnet/SiteAdaptation.Common/Fast2/ReportSources.cs. Build in Plan/ReportExcel.cs sets Made.NewProject, then RulesBox, Basis and the weak rule rows print no past share.
- Item 2, R703: ReportSheets.SourcesOf(values) in Fast2/ReportSources.cs, called by ScheduleTable, FloorsTable and WallsTable in Plan/ReportSchedule.cs. A column Source sits before the element ids.
- python/report_excel_check.py, the second check, holds both: new_project, sources_of, --mode.
- Item 6 step 1: ParsonsModelTests.cs line 361 and TestHostTests.cs R110 take the script lines with or without the floor. Next FAST-1 takes the floor lines out, then build-2 tightens the tests.
- Item 12: the Plot row of config/shared_parameters.xlsx has the note tool tag, floors, Q560.

## What calls what

- ReportWorkbook.Command, the worker, calls ProjectModes.Apply, then ReportSheets.Build. Build calls NewProject and the three tables call SourcesOf.
- The check table already names the source of each row in from and use. Not changed, a CHOSEN line in the PR.

## Next

Item 3, Update from report on a new project, Q394, with the recommended answer, CHOSEN. Then items 6, 7, 8, 9, 10. Items 4 and 5 moved to build-3.
