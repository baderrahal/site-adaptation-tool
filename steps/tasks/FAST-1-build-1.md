---
id: FAST-1-build-1
lane: build-1
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.Tests/FAST1/
  - dotnet/SiteAdaptation.Tests/UiLabelTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
  - steps/heads-up/build-1.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The face of the tool in Revit. Bader's order of 6 Nov, fast build, 6 to 8 Nov. In this order:

1. The pane in 3 stages, the 7 points of steps/tasks/R2-C4-revit-test.md: 3 cards each with status, an i icon and one main button. Settings gear. Advanced holds the old 9 rows closed by default. Pane words match what is built.
2. Stage 1 Project data wired: Choose project folder runs the scan on that folder, the data check rows show in the card in plain CAD and Revit words (strong, usable, weak, missing), Fix items opens the Confirm window of R2-C1.
3. Settings: project profile and the rules per project, the 5 points of steps/tasks/R2-C3-revit-test.md: the rules table from the gear and from any rule issue, one amber line until rules are confirmed, the first Build asks once to use the manual template, the Before Build setting, the templates reach an installed add-in.
4. Stage 2 Build site wired: the scope list in the project's words, a Preview tick that opens the 2D preview, one Build button that runs set up site, check, floors and ramps, levels, villas and walls as one run with one progress line, through the existing step code of version 1. It stops only on a blocking issue and says why in plain words. Everything else goes to the issues list. Coordinates from CAD shown with the file difference in mm.
5. Stage 3 Review and deliver: the issues count, the source mix bar (computed, rule, person entered, missing) read from what build-2 writes, Report and Update from report on the existing version 1 code.
6. Q504, Bader's answer 5A of 6 Nov: the Build skips steps 6 to 8 for a plot with no ffl, and the card shows the count of the issues no road level that the plan of FAST-2 lists.
7. Each window rendered once by SiteAdaptation.UiShots, one screenshot each in steps/ui-shots/ with no real value, and the PR labelled revit-later. Opening the windows in Revit is FAST-3-revit-test's, as .claude/rules/revit.md lets the revit-test lane alone open Revit. CHOSEN by Lead on the seam line of 6 Nov. No scoring, no second method, no counts against the plan.

Tests in dotnet/SiteAdaptation.Tests/FAST1/, a folder as the lane guard and the folder check take a path, not a file name pattern. CHOSEN by Lead.

The driver in Revit: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md).

Rule ids: R680 to R699. Question ids: Q530 to Q539. Take no other id.

# Done when

A person goes from an empty model through the 3 cards on the S1 folder with no JSON edit, and every window has a screenshot.

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
