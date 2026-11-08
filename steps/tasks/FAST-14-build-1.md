---
id: FAST-14-build-1
lane: build-1
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/BoundaryWalls/WallsBody.cs
  - dotnet/SiteAdaptation.AddIn/Revit/RevitWallDocument.cs
  - dotnet/SiteAdaptation.AddIn/Revit/WarningCollector.cs
  - dotnet/SiteAdaptation.Common/BoundaryWalls/
  - dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageResults.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageResultsText.cs
  - dotnet/SiteAdaptation.Tests/FAST14/
  - docs/rules/30-fast14-r815-up.md
  - steps/revit-results/FAST-14-build-1.md
  - steps/heads-up/build-1.md
  - docs/rules/index.md
  - steps/log.md
  - steps/seam.md
---

# Scope

Bader's order of Sun 8 Nov 06:45, item 2. The demo is today at 17:00 Riyadh. Record: steps/revit-results/FAST-3-revit-test.md, run 2 section, faults F32 and F34 of steps/seam.md.

1. F32. Step 8 Walls waited 7.4 minutes in run 2 on the Revit box "Highlighted walls overlap". Step 8 takes the overlap warnings of its own walls as step 5 does for floors (R782, WarningCollector.cs), counts them in the issues of the step and lists the walls, so no Revit box waits. A warning of other words still stops as today.
2. F34. Card 3 counts the issues of the whole read. Card 3 counts the issues of the scope, as card 2 does.

Not here: the files of FAST-13-build-3 and FAST-15-build-2. A change there is a line in steps/seam.md.

Shared records, the one exception to no path in two lists, as every PR carries its own log entry and the folder rules check reads this list: docs/rules/index.md by python/rules_index.py, steps/log.md with this PR's entry on top, steps/seam.md for lines to Lead. Merge main before every push.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05, on a copy of the run 2 model of 01A under python/window_lock.py, never the real one, and only while revit-test runs nothing. Keep the driver rule of .claude/rules/revit.md.

Rule ids: R815 to R819. Question ids: Q635 to Q639. Take no other id.

# Done when

One PR merged on green: a test proves step 8 takes the wall overlap warning with no box and counts it in the issues, and card 3 counts the issues of the scope. The wait of step 8 before (7.4 min in run 2) and after is in the PR, after measured in Revit or UNKNOWN until revit-test runs item 5 of the order.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry, the time before and after on the step it speeds up.
- Push at least every 2 hours. Merge main before every push.
- Keep steps/heads-up/<lane>.md current. One page.

At the end, put your final statement in the PR body and in steps/heads-up/build-1.md, then push.
