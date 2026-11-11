---
id: FAST-12-build-3
lane: build-3
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/PlaceVillas/
  - dotnet/SiteAdaptation.AddIn/Revit/WarningCollector.cs
  - dotnet/SiteAdaptation.Tests/FAST12/
  - docs/rules/28-fast12-r805-up.md
  - docs/rules/index.md
  - steps/revit-results/FAST-12-build-3.md
  - steps/heads-up/build-3.md
  - steps/seam.md
  - steps/log.md
---

# Scope

F31 of steps/seam.md, short check 2 of 01A, 7 Nov 23:38 to 23:51, record in steps/revit-results/FAST-3-revit-test.md. The demo is Sun 8 Nov 17:00 on a new project, whose prototypes may be of an older Revit too.

F31: 7 Place villas linked the 4 prototype files of the project folder, which are Revit 2021. Its box said "link types to load 0". Revit then upgraded each file on load and stopped 4 times on its own box "Structural Analytical Model Upgrade" with one button, Close. An unwatched Build waits there. The step took 12 minutes for 62 links of 4 types.

Needs, the smallest version:
1. The tool closes that upgrade box itself while step 7 loads links, as WarningCollector.cs takes known warnings, and lists each file it upgraded in the issues of the step. A box it does not know still stops as today.
2. The box of step 7 counts the link types it will load and says when a file is of an older Revit and will be upgraded, which takes time.

Not here: the plan, Common/Plan/, and the files of FAST-11-build-1 (Pane/StageCards.cs, StageResultsText.cs, StepPlan.cs). Naming 2024 copies of the prototypes in Set up is not here. A change there is a line in steps/seam.md.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05, on a copy of the 01A model of short check 2 under python/window_lock.py, never the real one, and with copies of the prototype files, never the files of the project folder. revit-test holds Revit from 06:00 for run 2, end your Revit use before then. build-1 may use Revit tonight too, the window lock takes turns. Keep the driver rule of .claude/rules/revit.md.

Rule ids: R805 to R809. Question ids: Q625 to Q629. Take no other id.

# Done when

One PR merged on green: step 7 on a copy of the 01A model loads the Revit 2021 prototype links with no stop on the upgrade box and lists the upgraded files, proven in Revit and written in steps/revit-results/FAST-12-build-3.md, and the box of step 7 counts the types and says they upgrade, proven by a test.

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

# Closed

Closed by Lead on 12 Nov 2026, not finished. Bader's order of 12 Nov ended the fast build and the demo. What was left of this task is in steps/audit-12nov.md and steps/plan.md.
