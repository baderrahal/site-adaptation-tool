# build-1 heads-up, FAST-5-build-1

7 Nov 2026. F16, the Build from card 1 to the end. F4 left to FAST-6 by Lead's word of 12:00.

## Final statement

F16 is built, with the F10 line and the F12 call: the Build fills the input rows from the chosen folder and reads the drawings, road surfaces and workbooks for Check itself, with no box. The real worker read S1 that way with 0 issues. Not seen in Revit yet. F4 is FAST-6's. Next: task status done once the Build to the end is seen in Revit on 01A, by revit-test's short check or this lane.

- PR 256, task/FAST-5-build-1. Tests: FAST5/R740_BuildReadTests.cs 8, FAST5/F10_RawReadSheetTests.cs 1. Review 1 FAIL with 4 REAL, all fixed.
- Done when, "Choose project folder then Build places floors, levels, villa links and walls on 01A": UNKNOWN, not seen in Revit.

## What changed, where

- dotnet/SiteAdaptation.Common/ReadData/FolderRoles.cs: FolderRoles.Of, the files of the rows found in the folder by the names of config.
- dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs: ReadInputs.ForBuild and PlotTableSteps, the old read with no schedule, plot numbers, border or location plan steps.
- dotnet/SiteAdaptation.AddIn/Fast5/BuildRead.cs: Needed, FillRows, Start, Answered. The read folder holds a copy of read_plotTable.json of stage 1 and a record of a read whose run id ends -build.
- dotnet/SiteAdaptation.AddIn/Pane/BuildChain.cs: ReadFirst and the step ReadForCheck, 10, put in right before 4 once per Build, after the steps 4 waits on.
- dotnet/SiteAdaptation.AddIn/App/PaneHost.cs: FillRows at each Build press, runStep runs the read for the build or RequestRun, endOf is the one end of every step of the Build, Buttons.ChainEnded calls it. endOf calls stages.ShowStop(end, line) after ShowBuild, F12 of FAST-7, PR 252.
- docs/rules/21-fast5-r740-up.md, R740 and R741.
- dotnet/SiteAdaptation.Common/ReadData/ReadDataRun.cs DataCheckRequest: F10, the parameter sheet of DataCheckSheet.Input in the request of the raw read only. Test FAST5/F10_RawReadSheetTests.cs.

## What calls what

Build press: PaneHost BuildRequested, BuildRead.FillRows(model.Settings, model.PickInput), SiteBuildFlow.Run, BuildChain.Start, runStep. Before Check: BuildChain.Before asks ReadFirst, BuildRead.Needed(WorkFolderNow()), and puts ReadForCheck in. runStep(ReadForCheck): BuildRead.Start with ReadDataBody.Worker, its end through LaunchedWorker.Hand to endOf, which runs 4. FolderRoles.Of is also what build-3 calls for F2.

## Next

- Status done once the Build to the end is seen in Revit on 01A, by revit-test's short check or this lane.
- Open for Lead in steps/seam.md: the data book debt. The read for the build adds no pane word, it speaks as step 2.
