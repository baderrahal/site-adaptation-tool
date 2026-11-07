# build-2, FAST-6-build-2

F10 and the Common side of F4, Bader's order of 7 Nov 10:05. Updated 7 Nov 2026. FAST-2-build-2 is done, its notes are in the history of this file.

## This PR

- R750, F10. dotnet/SiteAdaptation.Common/DataCheck/DataCheckSheet.cs, Input(ProjectSettings, configFile): the sheet the settings picked when on disk, else the one addin.parameterSheet of config names, else null. DataCheckStep.SheetInput now calls it.
- R751, F4. dotnet/SiteAdaptation.Common/Worker/WorkerContract.cs, WorkerRequest.ToJson writes unrounded while DesignValues.Known is false, as WorkerAnswer.ToJson does. SiteBuildPlan.Make wrote its request before any config gave values, in Revit no config/project.json lies above, so it stopped on R405.
- docs/rules/22-fast6-r750-up.md, tests in dotnet/SiteAdaptation.Tests/FAST6.

## What calls what

- DataCheckStep.SheetInput calls DataCheckSheet.Input. DataCheckStep.Start puts it in the request of the old read.
- The raw read of card 1, ReadDataBody.Choose to ConfirmProfileStep to ReadDataRun.DataCheckRequest, does not call it yet. That file is on FAST-5, the one line is in steps/seam.md, 7 Nov build-2 to build-1.
- WorkerRequest.Write calls ToJson, from SiteBuildPlan.Make:112, ProjectRulesTable:233, DataCheckStep:137, ReadDataRun:175, ScanStep:230, ReportWorkerClient:24 and UpdateWorkerClient:28. The two in process, Make and the rules table, gain most, their run takes the design values right after the write.

## Next

- build-1 adds the call to ReadDataRun.DataCheckRequest with a FAST5 test that captures the request of a raw read. Then FAST-6 is done, its status set by the PR that proves it.
- revit-test, short check: the first Build of a fresh Revit session makes its plan, card 1 says Shared parameters strong.

## Final statement

FAST-6-build-2 ships part 1 in this PR, the task stays open.
- F4 is fixed on the Common side. The first Build of a fresh session makes its plan with the design values of the project's config. Proven by R751: SiteBuildPlan.Make runs in Windows PowerShell 5.1 from a copy of the build, with no config/project.json above it. Reverting the fix gives the R405 text of F4 again. SiteBuildPlan.cs needs no change.
- F10 is measured and half fixed. The sheet is lost in the raw read of card 1, ReadDataRun.DataCheckRequest. That file is on FAST-5, outside this task's list, which folder_rules.py enforces. R750 gives the sheet from Common, and the request of DataCheckStep reads Shared parameters strong on the worker. Card 1 says strong only once build-1 adds the one line in steps/seam.md.
- Second check: PowerShell 7 with no config above it wrote the request, and CadReader.exe read the sheet strong.
- Not seen in Revit. Nothing of this task was opened in Revit.
