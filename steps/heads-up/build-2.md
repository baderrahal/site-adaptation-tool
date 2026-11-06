# build-2, FAST-2-build-2

The task is open, it ships in several PRs. Updated 6 Nov 2026.

## Merged

- PR 229: item 1, R700. Item 6 Python side, R701.
- PR 233: item 2, R702 and R703. Item 6 step 1 in the tests, item 12.
- PR 235: item 3 R704, item 7 R706, item 8 R705, item 10.

## This PR, PR 4

- Item 6, R701: WorkerMemory.Refusal and CommandRefusal take no floor, AddinConfig.MinFreeGiB is gone, WorkerContext and WorkerRunner call them so. RevitListenerSetting has no MinFreeGb and no floor refusal, RevitListenerLoop refuses no job on free memory, Q570. config/project.json holds no siteBuild.minFreeGB and no placeSettleMinutes.
- Item 9, R707: ParsonsModelTests.CimTestSeconds reads testHost.cimTestTimeoutSeconds 50 for the R310 tests, the raw times in the work root under runs, folder fast2, file item9_cim_times.txt.
- ReadEdgeTests.cs: the two R347 tests of the whole read and the whole build ask the peak alone.

## What calls what

- WorkerRunner.Execute and WorkerContext.Input and OpenFile call WorkerMemory.Refusal and CommandRefusal.
- Every R310 test that needs a CIM call to end calls CimTestSeconds. Pwsh waits 30 s plus two calls.

## Next

- The last PR sets the task done: status done in the task file, the final statement.
- Items 4 and 5 are build-3's.

## Final statement

FAST-2-build-2 is done with PR 4. Items 1, 2, 3, 6, 7, 8, 9, 10, 11 and 12 are built, in PR 229, 233, 235 and this PR. Items 4 and 5 went to build-3 on FAST-4. Results:
- Item 1: on S1, sharedParameters is strong from the parameter sheet.
- Item 2: the report of a new project prints no past share, and each value sheet names its sources.
- Item 3: a new project saves its report edits as entries the override list accepts.
- Item 8: a plot with no road level is named and skipped.
- Item 7: the wall thickness comes from rule M14.
- Item 6: no floor of free memory is left in the worker, the listener, the Python tools or config.
- Item 9: the R310 tests take a CIM limit from times measured on this PC.
Left for others:
- build-1: the floor lines of the three TestHost scripts. Then the tests can be held to the lines without the floor.
- build-1: AddinProbe can take AddinRunNames.DockedPage.
Not seen in Revit: nothing of this task was opened in Revit.
