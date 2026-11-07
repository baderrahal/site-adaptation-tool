# build-2, FAST-9-build-2

F22, Bader's order of 7 Nov 21:06: a new project needs one read and one Confirm before Scope names its neighbourhoods. Updated 7 Nov 2026. FAST-6-build-2 notes are in the history of this file.

## This PR

- R790. dotnet/CadReader/DataCheckCommand.cs writes data_check_input.json, what it gathered before the check, into the read folder. dotnet/SiteAdaptation.Common/DataCheck/DataCheckApply.cs holds Save, Load and Check, the check the worker and the Confirm share.
- R791. dotnet/SiteAdaptation.Common/ReadData/ReadDataRun.cs, ApplyConfirmed: after a Confirm that saved, runs the check again in Revit on that file with confirmed_profile.json, writes the four outputs, moves the read folder to the folder of its new inputs and writes read.json last.
- R792. A read with no gathered file, or any fault, keeps the step done and the line says the values count from the next read.
- docs/rules/25-fast9-r790-up.md, tests in dotnet/SiteAdaptation.Tests/FAST9.

## What calls what

- DataCheckCommand.Run calls DataCheckApply.Save and DataCheckApply.Check.
- ReadDataBody.For builds the ReadDataRun and hands run.ApplyConfirmed to ConfirmProfileStep.Wrap. ConfirmProfileStep.Ended calls it through Applied after session.Result is Saved.
- ReadDataRun.ApplyConfirmed calls DataCheckApply.Load, DataCheckApply.Check, DataCheckLogic.PlotTable, Plan, FolderOf and Recorded.
- ReviewLoad.LatestNbhCodes, the Scope list, reads read_plotTable.json of the moved folder, which holds the confirmed areas.

## Next

- revit-test: on the new project of 8 Nov, after FAST-8 and short check 2, one Choose project folder, one Confirm, then Scope lists the neighbourhoods of the area column.
- Lead: the two seam lines of 7 Nov build-2, step 2 shows not done until one more press that reads nothing, and a read that is not whole is read again on every press by R639.

## Final statement

See the end of this file once the PR is merged.
