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

FAST-15-build-2 is done in PR 284, which merges on green of job tests.
- F22 is fixed in code. One read of S1, then one Confirm whose check runs in the worker on the gathered file of that read, so Revit loads none of it and stays free. The Scope list names the areas of the confirmed column with no second read. On S1 outside Revit the Confirm check takes 38 s, where run 2 needed a second full read of 736 s. Its four outputs equal those of that second read.
- A second press with nothing changed reuses the read, starts no worker and says "Reuses the read of <time>, nothing changed". A read that is not whole names the first file not read. Both are proven by tests. The time in Revit is not seen.
- The read on S1 went from 782 s to 513 s, and the gathered file from 969 MB to 57 MB. The four outputs are equal before and after, and S1 was unchanged after every run. The 8 minute target is missed by 33 s. Of those 513 s, 360 s are the pair trace of the road drawing, which runs to the cap of the profile and gives no outline either way. That choice is in steps/seam.md for Lead and Bader.
- One review pass. Its one REAL point, the final statement, is fixed here. Its latent points are listed in the PR and not fixed, per the order of 6 Nov.
- Not seen in Revit. revit-test checks it in item 5 of the order, label revit-later.
