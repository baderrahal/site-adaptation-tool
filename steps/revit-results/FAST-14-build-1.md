# FAST-14-build-1, Revit check

Not seen in Revit. build-1 did not open Revit for this task: the order of 8 Nov leaves Revit to revit-test for item 5 once FAST-13, FAST-14 and FAST-15 merge, and a check of step 8 here would need a model copy with the walls of run 2 taken out first. No revit-wanted-build-1.txt was written.

Owed to revit-test, item 5 of Bader's order of 8 Nov 06:45, the PR carries the label revit-later:

1. Step 8 Walls of the Build on 01A ends with no Revit box "Highlighted walls overlap" (R815).
2. The issues of step 8 on the Advanced list hold one line per warning, "Walls overlap, taken without a box (R815): wall type ... id ...".
3. The commit of step 8 in run_timing_8 of the work folder, before 449.7 s of which the wait on the box was 7.4 minutes (05:12:05 to 05:19:28 of run 2), after UNKNOWN until that run.
4. Card 3 says the same issue count as card 2 after the Build (R816), run 2 said 2619 against 881.

What the tests prove instead: dotnet/SiteAdaptation.Tests/FAST14/R815_WallOverlapTests.cs, a fake transaction that decides as WarningCollector.Collect does by the same calls of Common, and dotnet/SiteAdaptation.Tests/FAST14/R816_CardThreeIssuesOfTheScopeTests.cs on the laid out pane.
