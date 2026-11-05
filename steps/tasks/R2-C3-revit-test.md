---
id: R2-C3-revit-test
lane: revit-test
stage: rules
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.Tests/R665_RulesTableTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
---

# Scope

The Revit side of the rules per project. Starts after R2-C3-build-1 is merged.

1. The rules table opens from the gear on the pane and from any issue caused by a rule.
2. The pane shows one amber line until the rules are confirmed.
3. The first Build with no rules chosen asks once to use the manual rules template.
4. The Before Build setting.
5. The templates reach an installed add-in. R486 ships rules/roshn-makkah.json alone today, docs/rules/10-addin-r407-up.md:66. If that takes a file outside this list, ask Lead.

Windows follow the approved sketches. Where none covers one, the smallest version, CHOSEN by Lead, with a screenshot for Bader.

Rule ids: R665 to R669. Question ids: Q517 to Q519. Take no other id.

The driver: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md). A wait for that is no failure, say how long.

# Acceptance tests

tests/acceptance/test_r2_build.py, test_the_rules_window_in_revit_is_recorded. Tests go in dotnet/SiteAdaptation.Tests/R665_RulesTableTests.cs.

# Done when

steps/revit-results/R2-C3-revit-test.md names what was pressed and seen, with screenshots under steps/ui-shots/ holding no real value. Nothing counts as seen without a screenshot or a person.

# Revit check needed

All of the above.
