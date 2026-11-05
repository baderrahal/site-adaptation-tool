---
id: R2-C1-fix-build-1
lane: build-1
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.Common/ReadData/
  - dotnet/SiteAdaptation.Common/Review/
  - dotnet/SiteAdaptation.Common/DataCheck/
  - dotnet/CadReader/
  - dotnet/SiteAdaptation.AddIn/ReviewData/
  - dotnet/SiteAdaptation.Tests/R639_StageOneFixTests.cs
  - dotnet/SiteAdaptation.Tests/UiLabelTests.cs
  - docs/design/14-ui-labels.md
  - docs/rules/15-data-check-r600-up.md
  - docs/rules/index.md
  - scripts/scenarios/
  - steps/scenarios.md
  - steps/latent.md
---

# Scope

Faults the review and the breaker of PR 209 found in existing files of Common, outside the revit-test lane. Each is reached by a person now that button 2 is wired. Comes before R2-C2-build-1. Keep it short, Bader wants a faster pace with the same proof.

1. ReadDataRun.cs:171-175, 189-197, 305-330. A check that stopped, or that left a file not read, ends Done, writes read.json and is reused until a file changes. So a press after the cause is fixed gives the same stop. A read like that is never reused, and the line says why it reads again. A read with every file read stays reused as now.
2. ReadDataRun.cs:99. The dry run of a raw read says Reads 2 files or Reads 3 files, counting Settings, the folder and the confirmed file, while the run reads 7. Say what it reads, in words that count what they name. Never a number nobody measured.
3. ReviewLoad.cs:19, 77. The line Read from of 3 Review data takes baseplan, villaBlocks and curbs, so on a raw read it is empty. It names the folder read, in plain words.
4. ReviewDataViewModel.cs:313 with ReviewTable.cs:54. Any null prints not read yet. A plot the read finished with no type says a false thing and hides the reason plots.json holds under missing. On S1 that is 394 of 2964 plots. A finished read says no type and why. Same for mirror and cluster where the read holds a reason. Not read yet stays only for what was truly not read.
5. ReadInputs.cs:244-262. A rev6 project missing any one past file silently takes the raw read. One file not downloaded gives a different plot table with no word. Decided by Lead: raw only when the project holds none of the past files, and otherwise the old stop that names the missing file. A person who wants raw on a project that holds some gets a line saying which files to take out of the settings. Past job mode stays as it is.
6. ReadInputs.cs:255. RawConfig catches only InvalidDataException and JsonException, and the pane refresh now reaches it, so a locked config raises IOException there. PaneRunState.ConfigOf guards the same refresh. Treat it the same, not read and said, never a crash.
7. DataCheckAreas.cs:106, DataCheckCodes.cs:232. A column passed over since two columns carry its name gets no line. S3 reads 11 areas where the others read 13 and a person is not told. Say it in the areaColumn and typologyColumn lines, in steps/latent.md it stands as PR 207. Take the line out of latent when fixed. Do not carry the column by its index unless the line alone cannot be true.

Out of scope: the confirm window and pane wiring, which are revit-test's in PR 209. Any Build site work.

Rule ids: R639. Question ids: none are free, a question goes to Lead in the last reply. Take no other id.

# Acceptance tests

dotnet/SiteAdaptation.Tests/R639_StageOneFixTests.cs. Each item has a test that breaks with the fix taken out. A made up project only.

# Done when

- Those tests pass on both targets.
- S1 and the hold-out are run on the merged code, by run_stage1.py and score_scenarios.py, Lead runs score_holdout.py: numbers 1873, villa type confirmed 1859, S3 stays for the round end. Any drop stops and tells Lead. Bader's order of 5 Nov 2026 is S1 and the hold-out per PR. All five run once at the end of the round.
- Past job mode equals main.
- A scenario run is one at a time. A run refused for memory while Revit is open is run again alone and said in the PR. Read files not exit codes.

# Revit check needed

Items 3 and 4 show in 3 Review data. R2-C1-fix adds no window, so revit-test sees them in R2-C2-revit-test. Until then they are not seen in Revit, and the PR says so.
