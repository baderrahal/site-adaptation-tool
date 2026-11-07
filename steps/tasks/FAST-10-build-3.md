---
id: FAST-10-build-3
lane: build-3
stage: 1
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/NewProject/
  - dotnet/SiteAdaptation.AddIn/RulesWindow/
  - dotnet/SiteAdaptation.AddIn/SetUpSite/
  - dotnet/SiteAdaptation.AddIn/Pane/PaneText.cs
  - dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1
  - dotnet/SiteAdaptation.Common/Settings/
  - dotnet/SiteAdaptation.Common/ProjectRules/
  - dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.Tests/FAST10/
  - config/
  - rules/
  - docs/rules/26-fast10-r795-up.md
  - docs/rules/index.md
  - steps/ui-shots/
  - steps/heads-up/build-3.md
  - steps/seam.md
  - steps/log.md
  - dotnet/SiteAdaptation.Common/Plan/DesignBasis.cs
  - dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs
  - docs/design/12-ui-spec.md
  - docs/design/14-ui-labels.md
  - dotnet/SiteAdaptation.Tests/InstallScriptTests.cs
  - dotnet/SiteAdaptation.Tests/R484_ProjectModeTests.cs
  - dotnet/SiteAdaptation.Tests/R486_ShippedSetTests.cs
---

# Scope

F8 of steps/seam.md, Bader's order of 7 Nov 21:06: Bader gets a real new project on Sun 8 Nov, files the tool has never seen, the demo is at 17:00.

F8: a project started as Empty project still holds the rules profile of the past job, the Rules row names the past project, the 6 road surface names of the past job are proposed, step 4 says "Compares the rules with the past job", the read names the fit schedule of a past key as missing, and Set up site fills a Position file column though none was picked.

Needs: a project made with Empty project shows no past job value in the New project window, the rules window, the pane and the read. Neutral defaults: every project value comes from the folder the person chooses, the rules template of the manual (rules/templates/manual.json) or a person. Where the past job value has no neutral one, the row says not chosen yet. The past job mode keeps its values as today. Look at the shipped set the install copies (Install-SiteAdaptation.ps1 line 74) and at how Empty project fills a new config.

Not here: Common/CheckTable/ and Common/Floors/ (FAST-8-build-1), Common/ReadData/ other than ReadInputs.cs and the Confirm files (FAST-9-build-2), the Build chain. A past job word in those goes in steps/seam.md.

Revit: you may open Revit for your own check on a model copy under python/window_lock.py, Bader's order of 7 Nov 10:05, after short check 2 of revit-test, which comes first. Window shots in steps/ui-shots/ with no real value in view.

Rule ids: R795 to R799. Question ids: Q615 to Q619. Take no other id.

Lead, 7 Nov 23:50: the last 7 paths of allowed were added after the review of PR 273. A new project names no road surface in config, and DesignBasis.Of refused that empty list before TakePick took the pick of Set up, so the installed set reached no plan. The docs carry the new step 4 line, the 3 tests follow the new profile name and the pick.

# Done when

One PR merged on green: a test makes a project with Empty project and finds no past job rules name, road surface name, schedule name or profile in its config, its rules window or the lines of its read, and the past job mode still holds its values.

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
