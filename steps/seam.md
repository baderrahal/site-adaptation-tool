# Seam

A lane that needs a function or a value of the other lane, or a change not in its task, writes one line here: date, lane, what it needs and why. Lead gives it to the owning lane the same hour and marks the line given.

- 6 Nov 2026, build-1: FAST-1 needs dotnet/SiteAdaptation.Tests/UiLabelTests.cs by exact path, its fixed count of 440 CHOSEN labels (line 199) fails on every new pane word. Also rule lines R680 and R681, text in steps/heads-up/build-1.md. And the Revit opening of each window, which .claude/rules/revit.md blocks for this lane.
- 6 Nov 2026, build-1: AddinRunNames.Page of dotnet/SiteAdaptation.Common/AddinRun.cs:140 names SitePanePage, which now sits closed under Advanced. PR 228 points the test host at StagesPage by its own constant AddinProbe.DockedPage, Common is not on the list of FAST-1. Lead may set Page to SiteAdaptation.AddIn.Pane.StagesPage and AddinRunAddInTests.cs:103 with it.
