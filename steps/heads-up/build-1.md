# build-1 heads-up, FAST-11-build-1

8 Nov 2026. F29 and F30 of short check 2 of 01A. Task branch task/FAST-11-build-1.

## Final statement

F29 and F30 are fixed in code and proven outside Revit. Not seen in Revit, label revit-later.
- F29: under rev6 the plan of 4 Check now writes a plot no rule gives a hand. It is built not mirrored and its hand is tagged missing (R800). A plot that lacks the FFL, the unit, the transform or the link fit stays out.
- Measured on the real Check of 01A, rerun with the worker of this branch on the request of short check 2: 265 of 278 plots written, was 62. 13 left out: no floor level 9, no outline or front 2, no unit 2. 203 built with the hand missing. Counted by a Python script from plan_01A.json, sharing no code with the C#. The 30 inputs unchanged by SHA-256, size and write time.
- CASE and Prototype Foundation Type no longer lack on any plot of 01A, the rule tables give them for the hand the plot is built with. CHOSEN by Code.
- F29 line: card 2 adds after the Build line "13 of 278 plots are not built: no floor level 9, no outline or front 2, no unit 2. Built with a value missing: 203 plots, each value tagged missing in the plan." (R801).
- F30: card 3 counts plotsWithNoRoadLevel of the plan of 4 Check, 11 on 01A, not the 2615 of the whole read (R802).
- CHOSEN by Code: R800 under rev6 only, a build of the past job keeps the plan of plan_from_book, so the gate test of R360 stays as it is.

## What changed, where

- dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs: HandMissing and HandMissingKey. Of gives a plot with no hand the hand not mirrored under rev6, marks handMissing and lists mirrored in Lacks.
- dotnet/SiteAdaptation.Common/Plan/BuildPlan.cs: Lacks keeps the mirrored lack of a written plot with the words of HandMissingWhy and adds insert when the fit is missing. LeftOut adds notWrittenByFirstKey and writtenWithMissing to the summary.
- dotnet/SiteAdaptation.Common/Plan/ValueStatus.cs: MissingSource, the source of the mirrored value of such a plot.
- dotnet/SiteAdaptation.Common/CheckTable/CheckPlanCount.cs, new: LatestPlan, Read, Of, Line, Reason.
- dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs: RefreshPlan, Card2Text, NoRoadLevelLine. StageResultsText.cs: NoRoadLevelOfPlan.
- docs/rules/27-fast11-r800-up.md R800 to R802. Tests dotnet/SiteAdaptation.Tests/FAST11/.

## What calls what

4 Check: BuildSteps calls PlanOverrides.Of, which calls HandMissing. BuildPlan.Assemble calls Lacks and LeftOut. ValueStatus.Step calls Sources, which reads HandMissingKey. Pane: PaneHost sets WorkFolderNow, StagesViewModel.Refresh and ShowBuild call RefreshPlan, which calls CheckPlanCount.LatestPlan and Read, then Card2Text reads Line. StagesPage.xaml binds NoRoadLevelLine.

## Next

- Run 2 of revit-test at 06:00 on 01A should build about 265 plots and show both lines.
- The box of step 5 still says "listed" only, card 2 carries the line.
