---
id: R2-C2-revit-test
lane: revit-test
stage: 2
status: waiting
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.Tests/R655_BuildButtonTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
---

# Scope

The Revit side of Stage 2. Starts after R2-C2-build-1 is merged.

1. One Build button runs set up site, check, floors and ramps, levels, villas and walls as one run, with one progress line. It stops only on a blocking issue and says why in plain words. Everything else goes to the issues list.
2. The 2D preview window before writing to Revit.
3. Coordinates shown from CAD and the position file with the difference in mm.
4. Q439: the floors step replaces the floors the tool placed when the outline changed. Test on a copy of a model built before the change, and on a new one.
5. Toposolid cut to the neighbourhood, masses, the road point report.
6. Re-check the old buttons 4 to 8 through the real windows.

Windows follow the approved sketches. Where none covers one, the smallest version, CHOSEN by Lead, with a screenshot for Bader. Do not design new screens.

Rule ids: R655 to R659. Question ids: Q509 to Q512. Take no other id.

The driver: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md). A wait for that is no failure, say how long.

# Acceptance tests

tests/acceptance/test_r2_build.py, test_the_build_run_in_revit_is_recorded. Tests go in dotnet/SiteAdaptation.Tests/R655_BuildButtonTests.cs.

# Done when

steps/revit-results/R2-C2-revit-test.md holds the build, the model copies with no client name, what was pressed, what was seen, counts against the plan by a second read, with screenshots under steps/ui-shots/ holding no real value. Nothing counts as seen without a screenshot or a person.

# Revit check needed

All of the above.
