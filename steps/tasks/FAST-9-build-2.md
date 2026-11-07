---
id: FAST-9-build-2
lane: build-2
stage: 1
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileStep.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileSession.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ConfirmProfileModel.cs
  - dotnet/SiteAdaptation.AddIn/Commands/ReadDataBody.cs
  - dotnet/SiteAdaptation.Common/ReadData/ReadDataRun.cs
  - dotnet/SiteAdaptation.Common/ReadData/ReadPicks.cs
  - dotnet/SiteAdaptation.Common/ReadData/ScanStep.cs
  - dotnet/SiteAdaptation.Common/Read/
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/CadReader/DataCheckCommand.cs
  - dotnet/SiteAdaptation.Tests/FAST9/
  - docs/rules/25-fast9-r790-up.md
  - docs/rules/index.md
  - steps/heads-up/build-2.md
  - steps/seam.md
  - steps/log.md
---

# Scope

F22 of steps/seam.md, Bader's order of 7 Nov 21:06: Bader gets a real new project on Sun 8 Nov, files the tool has never seen, the demo is at 17:00. Make the new project path fast.

F22: after the first read of a new project and Confirm with the proposed area column, the Scope list offered one area only, the group by distance. The confirmed columns, villa type and area, count only from the next read, so a new project needs two full reads of about 12 minutes each before Scope names its neighbourhoods.

Needs, the smallest version: Confirm what was found applies the confirmed villa type and area columns to the read just made, without a second read of the drawings. The plot table, plots.json and the Scope list of that read take the confirmed columns at once. The read writes nothing in the project folder. A test proves it: one read, one Confirm, then the Scope list names the neighbourhoods of the confirmed area column and the plots carry the confirmed villa types, with no second worker run.

Not here: the Build chain, PaneHost.cs and BuildChain.cs, and the files of FAST-8-build-1 and FAST-10-build-3. A change there is a line in steps/seam.md. Common/ReadData/ReadInputs.cs is FAST-10's.

You do not open Revit. A test proves it, the PR says "not seen in Revit", revit-test checks it on the new project.

Rule ids: R790 to R794. Question ids: Q610 to Q614. Take no other id.

# Done when

One PR merged on green: on the S1 folder, one read, Confirm with the proposed columns, and the Scope list names the neighbourhoods, proven by a test that runs the confirmed apply on the outputs of a read.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 30 minutes. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
