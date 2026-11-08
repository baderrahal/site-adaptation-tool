---
id: FAST-15-build-2
lane: build-2
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileStep.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileSession.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileModel.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileText.cs
  - dotnet/SiteAdaptation.AddIn/Commands/ReadDataBody.cs
  - dotnet/SiteAdaptation.AddIn/Fast5/BuildRead.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneRunState.cs
  - dotnet/SiteAdaptation.Common/ReadData/
  - dotnet/SiteAdaptation.Common/Read/
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/SiteAdaptation.Common/Flow/ButtonFlow.cs
  - dotnet/SiteAdaptation.Common/Flow/RunTexts.cs
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/FAST15/
  - docs/rules/31-fast15-r820-up.md
  - steps/heads-up/build-2.md
  - docs/rules/index.md
  - steps/log.md
  - steps/seam.md
  - docs/design/14-ui-labels.md
  - dotnet/SiteAdaptation.Tests/UiLabelTests.cs
---

# Scope

Bader's order of Sun 8 Nov 06:45, item 3. The demo is today at 17:00 Riyadh. Run 2 of 01A made two full reads of 12 minutes each (734 s and 736 s), F22 still stands: Confirm could not apply in Revit, the gathered files are too large. Record: steps/revit-results/FAST-3-revit-test.md, run 2 section.

One read, not two, and a reused read in seconds.
1. F22. Confirm applies the confirmed columns through the worker outside Revit, not in Revit, so the 667 MiB gathered file never loads in Revit. The smallest way named in steps/seam.md on 7 Nov 23:55: the worker command dataCheck takes the gathered file as an input in place of the folder walk, started from ReadDataRun.ApplyConfirmed as the read is. After Confirm the Scope list names the confirmed areas with no second read.
2. A read whose folder, settings and confirmed values are unchanged is reused in seconds from card 1 too, not only inside the Build, with the line "Reuses the read of <time>, nothing changed". Where R639 refuses a read that is not whole, say which file kept it from being whole.
3. Measure the read by part on S1: each drawing, each surface file, each workbook, the pair trace. Write the table in the PR. Cut what can be cut for one neighbourhood with no change of result: the pair trace only for the drawings of the scope, LandXML surfaces only those picked in Road levels. Any cut that changes a count of steps/scenarios.md is not made.

Target: one read under 8 minutes, a reused read under 10 seconds.

Not here: the files of FAST-13-build-3 (PaneViewModel.cs, PaneText.cs, PaneHost.cs, CheckRun.cs, BuildSteps.cs, PlaceVillas) and FAST-14-build-1. A change there is a line in steps/seam.md.

Shared records, the one exception to no path in two lists, as every PR carries its own log entry and the folder rules check reads this list: docs/rules/index.md by python/rules_index.py, docs/design/14-ui-labels.md for new pane words UiLabelTests reads (Lead, 8 Nov 07:45, asked by build-3), and its count in dotnet/SiteAdaptation.Tests/UiLabelTests.cs, only that count and only by the lines this task adds (Lead, 8 Nov 08:55), steps/log.md with this PR's entry on top, steps/seam.md for lines to Lead. Merge main before every push.

You do not open Revit. Tests and the worker on S1 outside Revit prove it, the PR says "not seen in Revit", revit-test checks it in item 5 of the order. Real inputs are read in place and never written, S1 hashed before and after any run on it.

Rule ids: R820 to R829. Question ids: Q640 to Q649. Take no other id.

# Done when

One PR merged on green: on S1, one read, one Confirm through the worker, and the Scope list names the confirmed areas with no second read and nothing loaded in Revit, proven by a test. A second press with nothing changed reuses the read, proven by a test. The table of the read by part, before and after, is in the PR.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry, the time before and after on the step it speeds up.
- Push at least every 2 hours. Merge main before every push.
- Keep steps/heads-up/<lane>.md current. One page.

At the end, put your final statement in the PR body and in steps/heads-up/build-2.md, then push.
