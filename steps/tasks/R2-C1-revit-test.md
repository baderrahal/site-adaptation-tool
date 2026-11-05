---
id: R2-C1-revit-test
lane: revit-test
stage: 1
status: waiting
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.Tests/R636_ConfirmWindowTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
---

# Scope

The window where the user confirms what stage 1 proposed: the code map, or types it if none was found, the hand of each prototype with the scanner's guess shown, and the word of each group level. It writes the confirmed values to the project profile, source person entered. Windows follow the approved sketches, docs/design/12-ui-spec.md and docs/design/boards. No sketch covers this one, so build the smallest version, write CHOSEN by Lead in the record, and save a screenshot for Bader. Do not design new screens beyond it.

Then run in Revit on S1 through the real window, on a copy of an empty model.

Out of scope: any logic in Common or the worker, that is build-1. If this task needs a change there, stop and say what.

Rule ids: R636 to R638. Question ids: Q499, Q500. Take no other id.

# Acceptance tests

tests/acceptance/test_r2_stage1_gaps.py, test_the_confirm_window_is_recorded. Tests of the window go in dotnet/SiteAdaptation.Tests/R636_ConfirmWindowTests.cs.

# Done when

steps/revit-results/R2-C1-revit-test.md names the build, the model copy with no client name, what was pressed, what was seen, each screenshot under steps/ui-shots/ with no real value, user name or client word. Nothing counts as seen in Revit without a screenshot or a person. Say not seen in Revit for the rest.

# Revit check needed

All of the above.
