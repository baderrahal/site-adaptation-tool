# build-2, FAST-2-build-2

The task is open, it ships in several PRs. Updated 6 Nov 2026.

## Merged

- PR 229: item 1, R700. Item 6 Python side, R701.
- PR 233: item 2, R702 and R703. Item 6 step 1 in the tests, item 12.
- PR 235: item 3 R704, item 7 R706, item 8 R705, item 10.

## This PR, PR 4

- Item 6, R701: WorkerMemory.Refusal and CommandRefusal take no floor, AddinConfig.MinFreeGiB is gone, WorkerContext and WorkerRunner call them so. RevitListenerSetting has no MinFreeGb and no floor refusal, RevitListenerLoop refuses no job on free memory, Q570. config/project.json holds no siteBuild.minFreeGB and no placeSettleMinutes.
- Item 9, R707: ParsonsModelTests.CimTestSeconds reads testHost.cimTestTimeoutSeconds 50 for the R310 tests, the raw times in the work root under runs, folder fast2, file item9_cim_times.txt.
- ReadEdgeTests.cs, two R347 tests of the floor, once PR 239 puts the file on the list.

## What calls what

- WorkerRunner.Execute and WorkerContext.Input and OpenFile call WorkerMemory.Refusal and CommandRefusal.
- Every R310 test that needs a CIM call to end calls CimTestSeconds. Pwsh waits 30 s plus two calls.

## Next

- The last PR sets the task done: status done in the task file, the final statement.
- Items 4 and 5 are build-3's.
