---
id: FAST-5-build-1
lane: build-1
stage: 2
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/App/PaneHost.cs
  - dotnet/SiteAdaptation.AddIn/Pane/BuildChain.cs
  - dotnet/SiteAdaptation.AddIn/Pane/SiteBuildFlow.cs
  - dotnet/SiteAdaptation.AddIn/Pane/SiteBuildPlan.cs
  - dotnet/SiteAdaptation.AddIn/Pane/SiteBuildText.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StepPlan.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneRunState.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneSessions.cs
  - dotnet/SiteAdaptation.AddIn/Pane/AnswerRelay.cs
  - dotnet/SiteAdaptation.AddIn/Commands/ReadDataBody.cs
  - dotnet/SiteAdaptation.AddIn/Commands/ButtonCommands.cs
  - dotnet/SiteAdaptation.AddIn/Commands/Buttons.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ScanGate.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ScanResultsSession.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileStep.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileSession.cs
  - dotnet/SiteAdaptation.AddIn/Fast5/
  - dotnet/SiteAdaptation.Common/ReadData/
  - dotnet/SiteAdaptation.Common/Steps/
  - dotnet/SiteAdaptation.Common/Read/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.Tests/FAST5/
  - docs/rules/21-fast5-r740-up.md
  - docs/rules/index.md
  - steps/revit-results/FAST-5-build-1.md
  - steps/heads-up/build-1.md
  - steps/seam.md
  - steps/log.md
---

# Scope

F16 only, then F4. Bader's order of 7 Nov 10:05. Nothing else in this task. The findings are F16 and F4 of steps/seam.md, from run 1 of FAST-3, steps/revit-results/FAST-3-revit-test.md.

1. F16. The Build chain from card 1 must reach the end. Today the read of card 1 (Choose project folder) writes the data check files only, and step 4 Check needs the outputs of the old step 2: read_baseplan.json, read_villaBlocks.json, read_curbs.json, read_workbooks.json and the road surfaces. Bader allows either way: card 1's read also writes what step 4 needs, or the chain runs the old step 2 itself from the folder the data check found, with no box and no second Confirm. CHOSEN by Lead from the file split: the second way, as the data check worker and DataCheckStep.cs are FAST-6's. If the first way proves the only one, write the line in steps/seam.md for Lead before touching a file off this list. F5, F5b and F5c are symptoms of F16 and close with it.
2. F4. The first Build of a fresh Revit session stops with R405, no config of the run gave the design values. The add-in side is SiteBuildPlan.cs, it must hand the config of the run to the plan. The Common side, DesignValues.cs:125-135 and its fall back to the repo config, is FAST-6-build-2's. Agree the call in steps/seam.md.

The polish items F1, F2, F3, F6, F7, F9, F12, F13 and F17 are FAST-7-build-3's. The box of F12 needs one call from PaneHost.cs, where chain.Ended reports a stop. Add that call when build-3 asks for it in steps/seam.md, it is the one line of another item this task takes.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05. The test host and the listener on a copy of the 01A model, never on the real one, under python/window_lock.py, one lane at a time. Keep the driver rule of .claude/rules/revit.md. Write what you opened in steps/revit-results/FAST-5-build-1.md and in the PR body. revit-test runs run 1b in Revit at the same time and stops at the end of its next part, saving its copy, while the file revit-wanted-build-1.txt stands in the lanes folder of the work root. Write it when you need Revit, delete it when you are done. The window lock decides who goes, wait for it, never end another lane's Revit.

Rule ids: R740 to R749. Question ids: Q571 to Q579. Take no other id.

# Done when

On the S1 folder, neighbourhood 01A, from an empty model copy: Choose project folder, then Build, places floors, levels, villa links and walls with no stop at Check, proven by the test host on a copy. The record says the counts placed. Then F4: the first Build of a fresh Revit session makes its plan.

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
