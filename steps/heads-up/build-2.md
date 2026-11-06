# build-2, FAST-2-build-2

The task is open, it ships in several PRs. Updated 6 Nov 2026.

## What changed, PR 1

- Item 1, R700. dotnet/CadReader/DataCheckCommand.cs ReadParameterSheet reads the parameter sheet the request names as input role parameterSheet after Gather and puts it on DataCheckInput (SheetName, SheetFound, Sheet, SheetProblem in dotnet/SiteAdaptation.Common/DataCheck/DataCheckLogic.cs). DataCheckLogic.SharedParametersNeed in DataCheck/SharedParametersNeed.cs gives the mark and line, called from DataCheckMarks.cs. On S1: sharedParameters strong, 10 parameters for the villa links in shared_parameters.xlsx, each with a GUID.
- Item 6, Python side, R701. python/window_lock.py, c3d_window.py and place_codes.py hold no floor of free memory, c3d_probe.json names none. place_codes.settle is gone.

## What calls what

- DataCheckCommand.Run calls Gather, then ReadParameterSheet, then DataCheckLogic.Run, whose needs list calls SharedParametersNeed.
- The add-in, FAST-1, has to add the input role parameterSheet with the sheet of the project settings to its dataCheck request, a line in steps/seam.md. Until then the pane shows sharedParameters missing. scripts/scenarios/run_stage1.py with_sheet adds it for the scenario runs. WorkerRunner.NoShaGiven takes it with an empty SHA-256.

## Not done, and why

- Item 1, parameters never on floors: floors still carry the Plot parameter, floors.plotParameter, written by FloorsRun.cs. It is how a re-run tells an untagged placed floor (R114). Taking it out changes the floor write, the setup planner and tests outside FAST2. A line for Lead is in steps/seam.md.
- Item 6, the worker, the listener and siteBuild.minFreeGB of config wait on the test files listed in steps/seam.md.

## Next

Items 2, 3, 4, 5, 7, 8 and 9 in that order, one PR each.
