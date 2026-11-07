---
id: FAST-8-build-1
lane: build-1
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.Common/CheckTable/
  - dotnet/SiteAdaptation.Common/Floors/
  - dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StepPlan.cs
  - dotnet/SiteAdaptation.AddIn/Revit/WarningCollector.cs
  - dotnet/SiteAdaptation.AddIn/Floors/
  - dotnet/SiteAdaptation.Tests/FAST8/
  - docs/rules/24-fast8-r780-up.md
  - docs/rules/index.md
  - steps/revit-results/FAST-8-build-1.md
  - steps/heads-up/build-1.md
  - steps/seam.md
  - steps/log.md
---

# Scope

Two faults of the short check of 7 Nov, Bader's order of 14:40: a fault of the short check goes to a lane the same hour. The demo is Sun 8 Nov 17:00 on a new project. The record is steps/revit-results/FAST-3-revit-test.md, short check section, the faults are F26 and F27 of steps/seam.md.

1. F26. Each Check of the short check read plotsCsv from data_book\plots.csv of the work root, the data book of the past job, not a file of the project (inputs of run_4_*.json). A new project must never read the past job. Check takes its plots from the read of the project, the plot table of card 1 and the read for the build. Where it has no such file it says so as missing, it never falls back on the data book. Find where the input is set, StepPlan.cs, BuildSteps.cs or CheckRun.cs, and fix it there.
2. F27. Run 5 on 01A placed 269 generic and 269 ramp floors, no yard floor, and no road toposolid: "the surface Top Surface-02 does not cover the scope, 807 of 807 ramp road edge points are off it". The first surface of the order is taken though it misses the area, while S1 holds 18 surfaces. Floors and ramps takes, of the surfaces the read found, the one that covers the scope, the one with the most ramp road edge points on it, and says which. Also: Revit stopped on its own box "Highlighted floors overlap" with OK and Cancel after the known warnings were taken, so an unwatched run waits there. Floors and ramps takes that warning as known, by WarningCollector.cs, and lists the overlapping floors in its issues.
3. If time is left: F28, one line on card 2 or the Check window says why Review data, Check and Floors count different plots of one neighbourhood.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05, the test host and the listener on a copy of the 01A model of the short check, never the real one, under python/window_lock.py. Keep the driver rule of .claude/rules/revit.md. Write what you opened in steps/revit-results/FAST-8-build-1.md and the PR body.

Rule ids: R780 to R789. Question ids: Q600 to Q609. Take no other id.

# Done when

On the S1 folder, 01A: Check reads no file of the work root data book, a test proves it, and Floors and ramps places the road toposolid and the yard floors with no stop on a Revit box, proven by the test host on a copy.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 30 minutes, IT maintenance may shut the PC down. Open the PR when one item works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
