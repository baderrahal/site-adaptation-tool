# build-2, FAST-15-build-2

F22 and the read time, Bader's order of 8 Nov 06:45 item 3: one read, a Confirm outside Revit, a reused read in seconds. Updated 8 Nov 2026. FAST-9 notes are in the history of this file.

## This PR

- R820. dotnet/SiteAdaptation.Common/ReadData/ReadDataRun.cs, ApplyConfirmedLater: after a Confirm that saved, the read folder moves to the folder of its new inputs with no read.json, the worker command dataCheck runs with the role gathered on data_check_input.json, config and the confirmed profile, and read.json comes last on its answer. dotnet/CadReader/DataCheckCommand.cs takes the role gathered in place of the folder walk.
- R821. RunTexts.ReadFolderReuses with ReadDataRun.RunTime, the words in docs/design/14-ui-labels.md: "Reuses the read of 04:13 on 8 Nov, nothing changed: ...".
- R822. DataCheckCommand: a part line per file and stage in the worker log, the memory release only after a drawing, workbook or PDF.
- R823. dotnet/SiteAdaptation.Common/DataCheck/DataCheckApply.cs, TopCandidates and Prune: each drawing keeps the outline candidates of its top score, at once after its read.
- docs/rules/31-fast15-r820-up.md, tests in dotnet/SiteAdaptation.Tests/FAST15/R820_ConfirmInWorkerTests.cs.

## What calls what

- ReadDataBody.For hands run.ApplyConfirmedLater to ConfirmProfileStep.WrapLater. ConfirmProfileStep.Ended calls it through Applied after a Confirm that saved, and calls done on the worker's answer.
- ApplyConfirmedLater calls HoldsProfile, Prepare, Moved, WorkerRequest.Write, IWorkerStart.Start, then AppliedLater and Recorded. ApplyConfirmed of R791 calls Prepare and Moved too.
- DataCheckCommand.Run calls DataCheckApply.Load for the role gathered, else Gather, where ReadDrawing calls DataCheckApply.Prune, which calls TopCandidates and CandidateKey. WriteOutputs and PartLine for both.
- ReadDataRun.Preview calls RunTime and RunTexts.ReadFolderReuses.

## S1, outside Revit, read in place

| Part | Before | After |
|---|---|---|
| One read, the worker | 782 s | 513 s |
| Road drawing, its pairs to the cap | 410 s | 424 s |
| 19 surface files, a peek each | 237 s | 0.3 s |
| Gathered file | 969 MB | 57 MB |
| Confirm, the check again | 300 s in the worker on the whole file, before that not possible in Revit | 38 s in the worker |

## Next

- revit-test, item 5: one Choose project folder, one Confirm, then Scope lists the neighbourhoods with no second read, and a second press reuses it.
- Lead: the cap of the pair trace and the two other lines of 8 Nov 08:35 in steps/seam.md.

## Final statement

Written at the end of the PR.
