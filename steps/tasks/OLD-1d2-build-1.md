---
id: OLD-1d2-build-1
lane: build-1
stage: rules
status: open
allowed:
  - dotnet/
  - python/
  - scripts/
  - tools/
  - docs/rules/
  - docs/repo-map.md
  - .claude/scheduled_tasks.lock
  - .gitignore
  - steps/latent.md
---

# Scope

Round 1 step d of steps/plan.md, part 2 of 4: the 42 dead parts of steps/audit-12nov.md section 1.1 go, and the rule ids R689 to R691 are written. Read docs/truth.md first, its standing rules hold here.

1. The 42 dead parts: 6 C# methods, 9 Python functions, 22 Python files, 5 scripts, rows 1 to 20 of the table of section 1.1 and the paragraph under it. The line numbers are of main 0c31771, find each by name. Before a part goes, prove again with `git grep -n -w` that nothing in product code calls it today, and put the output count in the PR. A part that has a caller today stays and is named.
2. Row 5, ReviewFile.ClusterUnconfirmed: first find whether another function makes buttons 5 to 8 skip the plots it names (R442). If none does, it is a missing wire, not dead code: leave it, and write the finding into steps/latent.md for Lead.
3. Rows 21 to 24 are not dead and stay: RevitTests, C3DProbe/Civil, RevitPrototypeReader, Placer.
4. The four Python files a C# comment names as the second route, new_project_check, report_excel_check, site_spec, update_check: stay in this task, part 4 judges the Python copies.
5. A test that only tests a removed part goes with it and is named in the PR. No other test is deleted. A test that imports a removed file for another reason is turned to what it needs.
6. R689, R690 and R691 get their rule lines in docs/rules/18-fast-pane-r680-up.md and docs/rules/index.md, each with id, plain name, input, output and test, written from what the code and dotnet/SiteAdaptation.Tests/FAST1/R689_StageResultsTests.cs do today. No code changes for them.
7. .claude/scheduled_tasks.lock is untracked and ignored. tools/run-lane.ps1 is row 15.
8. docs/repo-map.md loses the lines of what went.

Not in this task: the test run support inside the product library (the 31 parts), the data book reads, past job mode, light mode, the step buttons of Advanced. build-2 works on the data book reads in dotnet/SiteAdaptation.Common/ReadData at the same time: touch no file there.

Rule id R844 if one is needed. Question id Q657. Take no other id.

# Done when

- The PR lists each of the 42 parts as removed, or kept with its caller, by name.
- `python scripts/checks/rules_docs.py` passes with R689 to R691 in the index.
- Build and quick tests pass, with the test counts before and after and the reason for each test that went.
- The PR body starts with `Task: OLD-1d2-build-1`.
