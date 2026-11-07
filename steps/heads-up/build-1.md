# build-1 heads-up, FAST-5-build-1

7 Nov 2026. F16, the Build from card 1 to the end. F4 left to FAST-6 by Lead's word of 12:00.

## Final statement

- PR 1 of FAST-5, task/FAST-5-build-1: F16 in code. Each Build press fills the empty input rows from the chosen folder (R740), and right before 4 Check the Build runs the old read itself with no schedule, plot number file or border and none of the location plan steps, with no box and no Confirm (R741). Task status stays open.
- Proven by tests: dotnet/SiteAdaptation.Tests/FAST5/R740_BuildReadTests.cs, 8 tests, a worker of the test. Proven by the real worker outside Revit: the read for the build on S1, done, 0 issues, the four reads Check takes written (steps/revit-results/FAST-5-build-1.md). That check found one fault, the data book border starts the plot table in the worker, fixed.
- Not seen in Revit: all of it. Done when, "Choose project folder then Build places floors, levels, villa links and walls on 01A": UNKNOWN until the short check of revit-test or PR 2.
- F4: no add-in change. My SiteBuildPlan call was taken out after Lead's word, PR 254 fixes the rounding in Common.

## What changed, where

- dotnet/SiteAdaptation.Common/ReadData/FolderRoles.cs: FolderRoles.Of, the files of the rows found in the folder by the names of config.
- dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs: ReadInputs.ForBuild and PlotTableSteps, the old read with no schedule, plot numbers, border or location plan steps.
- dotnet/SiteAdaptation.AddIn/Fast5/BuildRead.cs: Needed, FillRows, Start, Answered. The read folder holds a copy of read_plotTable.json of stage 1 and a record of a read whose run id ends -build.
- dotnet/SiteAdaptation.AddIn/Pane/BuildChain.cs: ReadFirst and the step ReadForCheck, 10, put in right before 4 once per Build, after the steps 4 waits on.
- dotnet/SiteAdaptation.AddIn/App/PaneHost.cs: FillRows at each Build press, runStep runs the read for the build or RequestRun, endOf is the one end of every step of the Build, Buttons.ChainEnded calls it. endOf calls stages.ShowStop(end, line) after ShowBuild, F12 of FAST-7, PR 252.
- docs/rules/21-fast5-r740-up.md, R740 and R741.

## What calls what

Build press: PaneHost BuildRequested, BuildRead.FillRows(model.Settings, model.PickInput), SiteBuildFlow.Run, BuildChain.Start, runStep. Before Check: BuildChain.Before asks ReadFirst, BuildRead.Needed(WorkFolderNow()), and puts ReadForCheck in. runStep(ReadForCheck): BuildRead.Start with ReadDataBody.Worker, its end through LaunchedWorker.Hand to endOf, which runs 4. FolderRoles.Of is also what build-3 calls for F2.

## Next

- PR 2: in ReadDataRun.DataCheckRequest the parameter sheet input of DataCheckSheet.Input once PR 254 is on main, with a FAST5 test. Then status done once the Build to the end is seen in Revit.
- Open for Lead in steps/seam.md: the three words of the read for the build in docs/design/14-ui-labels.md, and the data book debt.
