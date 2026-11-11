---
id: FAST-6-build-2
lane: build-2
stage: 1
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/ScanResults/DataCheckStep.cs
  - dotnet/CadReader/DataCheckCommand.cs
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/SiteAdaptation.Common/Worker/
  - dotnet/SiteAdaptation.Common/DesignValues.cs
  - dotnet/SiteAdaptation.Common/SetUp/ParameterSheet.cs
  - dotnet/SiteAdaptation.Common/Fast2/
  - dotnet/SiteAdaptation.Tests/FAST6/
  - docs/rules/22-fast6-r750-up.md
  - docs/rules/index.md
  - steps/heads-up/build-2.md
  - steps/seam.md
  - steps/log.md
---

# Scope

F10 and the Common side of F4, Bader's order of 7 Nov 10:05. The findings are in steps/seam.md, from run 1 of FAST-3.

1. F10. The data check says Shared parameters Missing, "No parameter sheet was given to the data check", while the pane shows the sheet found. The parameterSheet input of R700 and R685 does not reach the request of stage 1. Measure where it is lost first: DataCheckStep.cs:79-91 builds the input, WorkerRunner.cs:348 filters the roles of a dataCheck, DataCheckCommand.cs:278-293 reads it. Fix it there.
2. F4, the Common side. DesignValues.cs:125-135 falls back on the config of the repo, and in Revit no config/project.json lies above the Revit program folder, so the first Build of a fresh session stops with R405. It must take the design values of the run, the config of the project the pane holds. The add-in side, SiteBuildPlan.cs, is FAST-5-build-1's, agree the call in steps/seam.md.

You do not open Revit. A test proves it, the PR says "not seen in Revit", and revit-test checks it in the short check after the merge.

Rule ids: R750 to R759. Question ids: Q580 to Q589. Take no other id.

# Done when

The data check says Shared parameters strong from the pane on the S1 folder, proven by a test that runs the request the pane builds. The first Build of a fresh session makes its plan, proven by a test with no config/project.json above the process folder.

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

# Closed

Closed by Lead on 12 Nov 2026, not finished. Bader's order of 12 Nov ended the fast build and the demo. What was left of this task is in steps/audit-12nov.md and steps/plan.md.
