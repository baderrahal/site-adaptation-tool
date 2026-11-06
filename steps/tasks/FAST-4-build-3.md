---
id: FAST-4-build-3
lane: build-3
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/CadReader/CadExtractor.cs
  - dotnet/CadReader/CadModels.cs
  - dotnet/CadReader/Exploder.cs
  - dotnet/CadReader/DataCheckCommand.cs
  - dotnet/SiteAdaptation.Tests/FAST4/
  - scripts/scenarios/
  - steps/scenarios.md
  - steps/heads-up/build-3.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The plot read and the mirror, for any project. Bader's order of 6 Nov 16:25, items 4 and 5 moved from FAST-2-build-2 to a third builder. In this order:

1. Plots read: S1 reads 2964 plots where the answer holds 1891. The plot table holds the plots a villa goes on, every other plot is listed with its reason, never dropped in silence. Numbers right stays at 1873 or more.
2. Mirror from what CAD gives: block Scale X, the footprint when it exists, the pair trace when it does not. Mirror right before and after on S1 and S2 in steps/scenarios.md, one run each.

The files are the plot read and the mirror: dotnet/SiteAdaptation.Common/DataCheck/, CadExtractor.cs, CadModels.cs, Exploder.cs and DataCheckCommand.cs of dotnet/CadReader/, the scenario scripts and steps/scenarios.md. Tests in dotnet/SiteAdaptation.Tests/FAST4/. A scenario run is one at a time, and the scenario folders are shared with the other lanes: say in steps/seam.md before a run and after it.

Rule ids: R730 to R739. Question ids: Q561 to Q569, as Q560 was taken on 6 Nov by Bader's answer on Plot on floors. Take no other id.

# Done when

S1 holds 1891 villa plots or lists the difference with reasons, numbers right 1873 or more, and mirror right before and after on S1 and S2 in steps/scenarios.md.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 2 hours. Open the PR when one card or one function works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
