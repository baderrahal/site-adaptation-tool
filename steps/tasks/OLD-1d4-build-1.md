---
id: OLD-1d4-build-1
lane: build-1
stage: rules
status: open
allowed:
  - dotnet/
  - python/
  - config/
  - rules/
  - samples/
  - scripts/
  - docs/rules/
  - docs/signoff.md
  - docs/repo-map.md
  - steps/buttons-guide.md
  - steps/latent.md
---

# Scope

Round 1 step d of steps/plan.md, part 4 of 4: past job mode goes. Read docs/truth.md first, its standing rules hold here. Bader 12 Nov, removed: "past job mode and its override list, light mode, the data book reads, the old past job values of config, and the Python copies of logic the add-in holds in C#. Python stays only for dev tools: the compare, the readers, the checks. Each file kept has a line why. Tests changed with the code, never deleted to go green."

This is the largest part. It is done in the order below, one commit per step, pushed after each. A step that cannot be finished in the session is left whole, not half, and the PR says exactly which steps are done and which remain.

0. Measure first, and write it into the PR, with file and line: where the mode is read (the profile names, pastJob of config, the source tag past), where config/overrides/roshn-makkah.json is read, where light mode is read, what the old read of step 2 is (ReadInputs.Make and RawConfig, PR 305 What remains), which keys of config/project.json only past job mode reads, and for each file of python/ whether it is a reader, a check, the compare, a dev tool, or a copy of logic the add-in holds in C#, with the C# file it copies.
1. The old read of step 2 and what is left of the data book: ReadInputs.DataBook, the plot table border, the fit neighbourhood of dataBook.locationPlan, addin.readData.dataBookFolder and dataBookFiles of config, the data book lines of Run-TestHost.ps1, of the help and of NewProjectCheck. The list is in the body of PR 305, What remains. A project reads its folder by the folder read alone.
2. Past job mode and the override list: the mode, config/overrides/roshn-makkah.json and every read of it, the majority rule code, the source tags override and past with their rows of docs/signoff.md. The 7 past models stay what they are since 12 Nov, the answer key of python/compare_built.py under the work root, read by no product code.
3. Light mode.
4. The keys of config/project.json that only a removed part read. Each removed key named in the PR. python/rule_status.py --write after.
5. The Python copies of logic the add-in holds in C#, with their tests, where step 0 proves the C# file holds the same logic and has its own test in dotnet/SiteAdaptation.Tests. A copy whose C# side has no test stays and is listed. The four files of the second route, new_project_check, report_excel_check, site_spec, update_check, are judged here.
6. Each Python file kept gets one comment line at its top that says why it stays: reader, check, compare or dev tool.

Rules of this task:
- A test of a removed part goes with it, named in the PR with the reason, by file and count. No other test is deleted. A test that used a removed part to reach a kept one is turned.
- A rule line whose whole subject went is marked Withdrawn as PR 306 did, never renumbered.
- A question a removed config value cited: list it in the PR for Lead, change no question file.
- The new mode must not get worse: the tests of the folder read, the Build read (R740, R780, R840), the build steps and the Report stay green and are named in the PR with their counts.
- You open no Revit. Not in this task: the step rows of Advanced and the ribbon, which build-2 changes at the same time in dotnet/SiteAdaptation.AddIn/Pane, App and Commands. Touch those three folders only where a removed part forces it, and say where.

Rule ids R847 to R849. Question id Q659. Take no other id.

# Done when

- The PR lists steps 0 to 6 as done or remaining, each done step with its proof by `git grep`.
- Build and quick tests pass, with the test counts before and after and the reason for each test that went.
- Every function left has a caller.
- The PR body starts with `Task: OLD-1d4-build-1` and says Not seen in Revit.
