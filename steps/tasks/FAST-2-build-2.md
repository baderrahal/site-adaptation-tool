---
id: FAST-2-build-2
lane: build-2
stage: 2
status: open
allowed:
  - dotnet/SiteAdaptation.Common/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.Tests/FAST2/
  - dotnet/SiteAdaptation.Tests/ParsonsModelTests.cs
  - python/
  - scripts/scenarios/
  - config/
  - rules/
  - docs/rules/
  - steps/scenarios.md
  - steps/heads-up/build-2.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The logic behind the face, for any project. Bader's order of 6 Nov, fast build, 6 to 8 Nov. In this order:

1. Shared parameters for any project. config/shared_parameters.xlsx is the one source, a new project edits only that Excel. Parameters go on the villa links only, never on floors. Never write RRE_ZZ_SP_Prototype-FFL. The data check marks sharedParameters strong when the Excel is found and says in plain words what is missing when it is not.
2. The source mark per value: computed, rule, person entered, missing, carried through the plan, the check table and the report. The report on a new project prints no past shares, each sheet says the source of its values.
3. Update from report on a new project, Q394.
4. Plots read: S1 reads 2964 plots where the answer holds 1891. The plot table holds the plots a villa goes on, every other plot is listed with its reason, never dropped in silence. Numbers right stays at 1873 or more.
5. Mirror from what CAD gives: block Scale X, the footprint when it exists, the pair trace when it does not. Mirror right before and after on S1 and S2 in steps/scenarios.md, one run each.
6. No RAM floor: remove siteBuild.minFreeGB from config/project.json and every reader, python/window_lock.py, python/c3d_window.py and the worker checks.
7. Boundary wall thickness from the rules file, Q149. The link parameter group, Q336.
8. Q504, Bader's answer 5A of 6 Nov: a plot whose ffl is missing is marked, steps 6 to 8 skip it, and the plan lists every such plot in the issues list with the words no road level, so the card shows the count.
9. Bader's answer 4A of 6 Nov: the R310 tests of ParsonsModelTests.cs fail at random on this PC. Measure the time of a CIM call on this PC and take the limit of the tests from it. dotnet/SiteAdaptation.TestHost/WindowSamples.ps1 is FAST-1's, a change there is a line in steps/seam.md.

Tests in dotnet/SiteAdaptation.Tests/FAST2/ and python/tests/. tests/acceptance/ stays Lead's, the required check fails a lane that changes it, and scripts/ is held to scripts/scenarios/, as scripts/checks/ is the gate and scripts/lanes/ the lane starter. CHOSEN by Lead.

Rule ids: R700 to R719. Question ids: Q540 to Q549. Take no other id.

# Done when

The S1 folder scans with sharedParameters strong, the plan carries a source on every value, the report says it, and the villa plot count is 1891 or the difference is listed with reasons.

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

# Rules for Lead

The rules of this task are in docs/rules/19-fast2-r700-up.md, written by build-2 at Lead's answer of 6 Nov.
