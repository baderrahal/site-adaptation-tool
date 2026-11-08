# build-1 heads-up, FAST-14-build-1

8 Nov 2026. F32 and F34 of run 2 of 01A. Task branch task/FAST-14-build-1.

## Final statement

Written at the end of the task, see below.

## What changed, where, what calls what

- F32, R815. dotnet/SiteAdaptation.Common/BoundaryWalls/WallOverlapWarning.cs holds the words of Revit's walls overlap, BuiltInFailures.OverlapFailures.WallsOverlap of RevitAPI.xml of Revit 2024, the same words as the box of run 2. WallsRun.Apply calls context.AcceptKnownWarnings(WallOverlapWarning.List()). At the commit WarningCollector.Collect deletes that warning, as R782 does for floors, so Revit shows no box, and adds the line of WallOverlapWarning.Line, each wall by type and id. ButtonFlow adds the lines to the issues of step 8, one per warning. Other words still show.
- WarningCollector.Read reads no plot parameter when the list names none, as walls hold none.
- F34, R816. StageStatus.IssuesWhenDone gives the sum of the issues of a stage once all its steps ran. StageStatus.Of (card 2) and StagesViewModel.IssuesLine (card 3) both call it, so card 3 says the count of card 2. Before card 2 is done, card 3 keeps the plan count of R689.
- Tests: dotnet/SiteAdaptation.Tests/FAST14/.

## Next

- revit-test, item 5 of the order: step 8 with no box, the wait after, card 3 equal to card 2. steps/revit-results/FAST-14-build-1.md lists it.
