---
id: OLD-1d1-build-2
lane: build-2
stage: 2
status: done
allowed:
  - dotnet/
  - config/
  - rules/
  - docs/rules/
  - python/
  - samples/
  - steps/buttons-guide.md
  - steps/latent.md
---

# Scope

Round 1 step d of steps/plan.md, part 1 of 4: the data book reads go, and Build runs on the input folder alone. Read docs/truth.md first, its standing rules hold here.

What was measured, steps/revit-results/OLD-1-revit-test.md, Why it stops: on this PC card 2 built 0 of 9 neighbourhoods. The read for the build asks for labels and plotsCsv of addin.readData.dataBookFiles in the folder data_book beside the work folder (dotnet/SiteAdaptation.Common/ReadData/ReadInputs.cs, DataBook, called with forBuild from dotnet/SiteAdaptation.AddIn/Fast5/BuildRead.cs). data_book is an output of the past job. This PC does not hold it and no window sets it. On the laptop it stood under the work root, so the stop did not show.

1. Measure first, and write it into the PR: every place of product code that reads a file of the data book, with file and line, and what each read is used for. Start from `git grep -n -i "data_book\|dataBook" -- dotnet python config`. Tests are no product code.
2. The road levels of the build take the plots and the labels from what card 1 read from the CAD drawings, the files of the read in the work folder of the model. CHOSEN by Lead, the smallest version: the same plots the Review window and Check already show (F26, F28 of steps/audit-12nov.md were fixed that way). If the read of card 1 does not hold what the road levels need, say exactly what is missing in the PR and build nothing around it.
3. Remove the data book reads from the product: the read for the build, the plot table border, the fit neighbourhood of dataBook.locationPlan, and each other read step 1 finds. The keys of config that only those reads use go with them. A read that a kept test needs through the Python checker is named in the PR, not kept quietly.
4. The stop words of a Build hold only stops. The two notes of FolderRoles, the road surface and the prototype sentences, are not written as causes when a person has picked those rows (line 8 of Lines for Lead of the record).
5. Tests change with the code, never deleted to go green. A test of a removed read is removed with it and named in the PR with the reason. A test that fed a data book into a build is turned to feed the read of card 1.

Not in this task: the 9 step buttons of Advanced, past job mode and its override list, light mode, the dead parts of the audit. They are parts 2 to 4. No window, no new setting. You open no Revit. Whether the Build then builds in Revit is step e, by the revit-test lane.

Rule ids R840 to R843. Question ids Q655 and Q656. Take no other id. A changed config value needs python/rule_status.py --write.

# Done when

- A test builds the read for the build on a made up work folder that holds the read of card 1 and no data_book, and it gives no stop. It fails on main today.
- A test holds that a Build stop names no road surface or prototype note when both rows are picked.
- `git grep -n -i "data_book\|dataBook" -- dotnet ':!dotnet/SiteAdaptation.Tests'` is in the PR, each line left with its reason, or none.
- Build and quick tests pass. Every new function has its caller in the PR. Every rule has id, plain name, input, output and test.
- The PR body starts with `Task: OLD-1d1-build-2` and says Not seen in Revit.

# Closed by Lead, 12 Nov

PR 305 did steps 1, 2 and 4 and the build part of step 3. What remains of step 3, the old read of step 2 with its data book files, cannot go without past job mode and is step 1 of task OLD-1d4-build-1. The list is in the body of PR 305, What remains.
