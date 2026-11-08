---
id: FAST-11-build-1
lane: build-1
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.Common/Plan/PlanOverrides.cs
  - dotnet/SiteAdaptation.Common/Plan/BuildPlan.cs
  - dotnet/SiteAdaptation.Common/Plan/PlanSummary.cs
  - dotnet/SiteAdaptation.Common/Plan/ValueStatus.cs
  - dotnet/SiteAdaptation.Common/Plan/LinkParameters.cs
  - dotnet/SiteAdaptation.Common/Plan/CheckRows.cs
  - dotnet/SiteAdaptation.Common/CheckTable/
  - dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageResultsText.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StepPlan.cs
  - dotnet/SiteAdaptation.Tests/FAST11/
  - docs/rules/27-fast11-r800-up.md
  - docs/rules/index.md
  - steps/revit-results/FAST-11-build-1.md
  - steps/heads-up/build-1.md
  - steps/seam.md
  - steps/log.md
---

# Scope

F29 and F30 of steps/seam.md, short check 2 of 01A, 7 Nov 23:36 to 23:58, record in steps/revit-results/FAST-3-revit-test.md. The demo is Sun 8 Nov 17:00 on a new project, which will have the same gap, so this comes first tonight.

1. F29. The plan of 4 Check on 01A writes 62 of 278 plots, so steps 5 to 8 build 62. plan_01A.json: not written 216, missing by key mirrored 211, parameters CASE 213, Prototype Foundation Type 213, levels.ffl 11, Number of Steps 11, Prototype FFL 11, transform 2, unit 2. Since R780 the plot table of the read stands in for the data book of the past job, and it gives no hand and no CASE or Foundation Type to most plots.
   Needs, CHOSEN by Lead, the smallest version: a plot that lacks only the hand or a parameter the project has no source for is still written. Hand missing: the plot is built not mirrored, the hand tagged missing, listed in the issues. A parameter with no source: left empty in Revit, tagged missing, listed. A plot that lacks levels.ffl, the unit or the transform stays not written as today, a floor needs them. No value is guessed from the past job.
2. F29, the line. Card 2, or the box of step 5, names the count of plots left out of the plan and the reason by key, for example "13 of 278 plots are not built: no floor level 11, no unit 2", and the count built with a value missing.
3. F30. After the Build, card 3 says "2615 plots have no road level" where 2615 is the plot count of the whole read and the plan of 01A lists 11. The line counts the plots of the plan of the scope, from plotsWithNoRoadLevel of that plan.

Not here: DesignBasis.cs and the files of FAST-12-build-3 (AddIn/PlaceVillas/, AddIn/Revit/). A change there is a line in steps/seam.md.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05, on a copy of the 01A model of short check 2 under python/window_lock.py, never the real one. revit-test holds Revit from 06:00 for run 2, end your Revit use before then. build-3 may use Revit tonight too, the window lock takes turns. Keep the driver rule of .claude/rules/revit.md.

Rule ids: R800 to R804. Question ids: Q620 to Q624. Take no other id.

# Done when

One PR merged on green: on the S1 folder, 01A, the plan writes every plot that has a floor level, a unit and a transform, the ones with no hand or no parameter source tagged missing, proven by a test on the outputs of a read, and card 2 and card 3 count the plots of the scope with the reason.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 30 minutes. Open the PR when item 1 works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
