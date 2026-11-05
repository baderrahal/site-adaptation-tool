---
id: R1-revit-test
lane: revit-test
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.Tests/R620_DataCheckWindowTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
---

# Scope

Stage 1 seen in Revit, from an empty model with nothing pre-run. Starts after SETUP-sign is merged and R1-build-1 is merged, it needs the signed install and the worker command dataCheck.

1. Show the data check in the add-in. Button 2 runs dataCheck on the input folder. The rows go in a new section "Data check" at the top of the existing scan results window, one row per need: its name, its mark, its plain line. Nothing else changes in that window. This layout is CHOSEN by Lead, the smallest that shows it, Bader may change it after the screenshot.
2. Pane words must match what is built. Reword a pane line that says otherwise.
3. In Revit 2024, on a copy under the work root of an empty model, with folder S1 of scenarios: press 1 Set up site, 2 Read data, 3 Review data through the real window. Run on copies only, under the shared lock.

Out of scope: buttons 4 to 9. Any change to the worker or to Common, that is R1-build-1. If this task needs one, stop and say what.

Rule ids: R620 to R629. Question ids: Q490 to Q494. Take no other id.

# Acceptance tests

tests/acceptance/test_r1_stage1.py, test_the_revit_result_of_stage_1_is_recorded. New tests of the window go in dotnet/SiteAdaptation.Tests/R620_DataCheckWindowTests.cs.

# Done when

- steps/revit-results/R1-revit-test.md holds the build, the model copy by name with no client name, what was pressed, what was seen, the plot count against plots.json of the worker, and each screenshot's name in steps/ui-shots/. Counts only, no real coordinate, level or plot number, and blank any such value out of a screenshot before it enters git.
- A person sees the window, or say UNKNOWN: a driver press proves nothing about what a person sees, and a locked screen stops the check.
- Write not seen in Revit for anything else.

# Revit check needed

All of the above.
