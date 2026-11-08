---
id: FAST-13-build-3
lane: build-3
stage: 2
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/PlaceVillas/LinkUpgrade.cs
  - dotnet/SiteAdaptation.AddIn/PlaceVillas/PlaceVillasBody.cs
  - dotnet/SiteAdaptation.AddIn/PlaceVillas/UpgradingVillaDocument.cs
  - dotnet/SiteAdaptation.AddIn/Revit/RevitVillaDocument.cs
  - dotnet/SiteAdaptation.Common/PlaceVillas/
  - dotnet/SiteAdaptation.AddIn/Pane/PaneViewModel.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneText.cs
  - dotnet/SiteAdaptation.AddIn/App/PaneHost.cs
  - dotnet/SiteAdaptation.Common/CheckTable/CheckRun.cs
  - dotnet/SiteAdaptation.Common/Plan/BuildSteps.cs
  - dotnet/SiteAdaptation.Tests/FAST13/
  - docs/rules/29-fast13-r810-up.md
  - steps/revit-results/FAST-13-build-3.md
  - steps/heads-up/build-3.md
  - docs/rules/index.md
  - steps/log.md
  - steps/seam.md
---

# Scope

Bader's order of Sun 8 Nov 06:45, item 1. The demo is today at 17:00 Riyadh. Run 2 of 01A took 28 minutes in step 7 Place villas for 263 links of 13 villa files of Revit 2021, upgraded on load, and the reopen of the built model took 27.5 minutes with 13 upgrade boxes, F33. Record: steps/revit-results/FAST-3-revit-test.md, run 2 section.

Villa files in Revit 2024. Step 7 and the reopen take the Revit 2024 copy of each villa file when one exists.
1. The Prototypes row of the pane looks for a 2024 folder beside the given one, or a folder the person picks, and says per code which year it found. CHOSEN by Lead: a folder beside the given one is a 2024 folder when its name holds the year of the running Revit and it holds files of the same names saved in that year, the R496 look reads the year.
2. Step 7 links the 2024 file of a code when one exists, so the saved model links the 2024 files and the reopen shows no upgrade box. A code with no 2024 file is said in the row and linked as before.
3. The demo folder is <work root>\demo-S1, made by Lead at 07:00: a copy of scenarios\S1 with the 13 Revit 2024 copies of run 1b in demo-S1\Prototypes_2024, beside demo-S1\Prototypes. Copied, never moved. S1 unchanged, 37 files by SHA-256 before and after (<work root>\demo-S1-s1-before.txt and -after.txt). Do not change demo-S1 or S1.

Target: step 7 under 5 minutes for 263 links, the reopen with no upgrade box.

Not here: the files of FAST-14-build-1 and FAST-15-build-2. A change there is a line in steps/seam.md.

Shared records, the one exception to no path in two lists, as every PR carries its own log entry and the folder rules check reads this list: docs/rules/index.md by python/rules_index.py, steps/log.md with this PR's entry on top, steps/seam.md for lines to Lead. Merge main before every push.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05, on a model copy under python/window_lock.py, never the real one, and only while revit-test runs nothing. Keep the driver rule of .claude/rules/revit.md.

Rule ids: R810 to R814. Question ids: Q630 to Q634. Take no other id.

# Done when

One PR merged on green: on demo-S1 the Prototypes row names the 2024 folder and the year per code, and step 7 takes the 2024 files, proven by a test. The time of step 7 before (28 min in run 2) and after is in the PR, after measured in Revit or UNKNOWN until revit-test runs item 5 of the order.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry, the time before and after on the step it speeds up.
- Push at least every 2 hours. Merge main before every push.
- Keep steps/heads-up/<lane>.md current. One page.

At the end, put your final statement in the PR body and in steps/heads-up/build-3.md, then push.
