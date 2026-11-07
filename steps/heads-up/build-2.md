# build-2, FAST-9-build-2

F22, Bader's order of 7 Nov 21:06: a new project needs one read and one Confirm before Scope names its neighbourhoods. Updated 7 Nov 2026. FAST-6-build-2 notes are in the history of this file.

## This PR

- R790. dotnet/CadReader/DataCheckCommand.cs writes data_check_input.json, what it gathered before the check, into the read folder. dotnet/SiteAdaptation.Common/DataCheck/DataCheckApply.cs holds Write, Load and Check, the check the worker and the Confirm share. The file is written with no indent, 667 MiB on S1.
- R791. dotnet/SiteAdaptation.Common/ReadData/ReadDataRun.cs, ApplyConfirmed: after a Confirm that saved, runs the check again in Revit on that file with confirmed_profile.json, writes the four outputs, moves the read folder to the folder of its new inputs and writes read.json last.
- R792. A read with no gathered file, a gathered file over 1,000,000,000 bytes, a settings file changed since the read, or any fault, keeps the step done and the line says the values count from the next read.
- Review 1: the apply takes the inputs read.json holds from the start of the read, with only the profile new, so a folder changed during the read is read again.
- docs/rules/25-fast9-r790-up.md, tests in dotnet/SiteAdaptation.Tests/FAST9.

## What calls what

- DataCheckCommand.Run calls DataCheckApply.Write, through WorkerContext.WriteJson, and DataCheckApply.Check.
- ReadDataBody.For builds the ReadDataRun and hands run.ApplyConfirmed to ConfirmProfileStep.Wrap. ConfirmProfileStep.Ended calls it through Applied after session.Result is Saved.
- ReadDataRun.ApplyConfirmed calls DataCheckApply.Load, DataCheckApply.Check, DataCheckLogic.PlotTable, FolderOf and Recorded, and builds its ReadPlan from read.json of the read.
- ReviewLoad.LatestNbhCodes, the Scope list, reads read_plotTable.json of the moved folder, which holds the confirmed areas.

## Next

- revit-test: on the new project of 8 Nov, after FAST-8 and short check 2, one Choose project folder, one Confirm, then Scope lists the neighbourhoods of the area column.
- Lead: the three seam lines of 7 Nov build-2. Step 2 shows not done until one more press that reads nothing. A read that is not whole is read again on every press by R639. On S1 the apply keeps Revit busy about 2 minutes.

## Final statement

FAST-9-build-2 is done in PR 272, merged on green once Lead's order on PR 273 lets it.
- F22 is fixed in code. One read, one Confirm, and the Scope list names the neighbourhoods of the confirmed area column, the plots carry the confirmed villa types, and no drawing is read twice. The next press of 2 reuses the read.
- Proven on S1, read only, outside the repo. The first read gives 1 Scope entry. The check on its gathered files with the proposed profile gives 11, the same as a full read with that profile, and the four output files are equal. Proven in the repo on the made up project by R791_ConfirmAppliesTests, which also shows F22 when the apply is off.
- The cost on S1: a gathered file of 667 MiB, and about 2 minutes and 11 GB for the check, measured outside Revit. Inside Revit it runs in the API context, so Revit is busy that long after Confirm. Lead holds the choice in steps/seam.md.
- Review 1 found no real fault in the logic. Its latent points are fixed with tests: the read keeps the inputs it recorded at its start, a changed settings file refuses the apply, the fallback reason. A gathered file over 1,000,000,000 bytes refuses the apply, and no test covers that case.
- Not seen in Revit. revit-test checks it on the new project of 8 Nov, after FAST-8 and short check 2.
