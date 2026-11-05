---
id: R2-C4-revit-test
lane: revit-test
stage: 1
status: waiting
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.UiShots/
  - dotnet/SiteAdaptation.TestHost/
  - dotnet/SiteAdaptation.Tests/R670_ThreeStagePaneTests.cs
  - docs/design/14-ui-labels.md
  - steps/ui-shots/
  - steps/revit-results/
---

# Scope

The pane in 3 stages, R2-C4. Bader asked for it several times and it was in no task, Bader's order of 6 Nov 2026. Lane revit-test runs one task at a time: R2-C3-revit-test, then this one, then R2-C2-revit-test, side by side with build-1 on R2-C2-build-1.

The design is approved by Bader: the three stages page and the sketch of 4 Nov, stage 1 Project data, stage 2 Build site, stage 3 Review and deliver, Settings and Advanced. That page and sketch are not in this repo, a search of docs and steps finds neither, so the seven points below are the design. Where they leave a detail open, the smallest version, CHOSEN by Lead, with a screenshot for Bader. Build exactly that:

1. The pane shows 3 stage cards in order, each with its status, an i icon, and one main button.
2. Stage 1 Project data: a Choose project folder button the user presses, which runs the data check on that folder. The data check rows show in the card: each need with strong, usable, weak or missing and one plain line. A Fix items button opens the confirm window of R2-C1.
3. Stage 2 Build site: the scope list in the project's own words, a Preview tick, and the one Build button of R2-C2.
4. Stage 3 Review and deliver: the issues count, the source mix bar (computed, rule, person entered, missing), and the Report and Update from report buttons.
5. Settings (gear): project profile and rules of R2-C3. Advanced: the old 9 step rows, closed by default.
6. Pane words must match what is built. A card whose work is not built yet says so plainly. Build of R2-C2 is not on main until R2-C2-build-1 merges, the card says so until then, and the rules table of R2-C3-revit-test the same.
7. A screenshot of each card in steps/ui-shots/ with no real value, named pane-stage-1.png, pane-stage-2.png and pane-stage-3.png, and a run in Revit on S1 from an empty model through the 3 cards.

Out of scope: any logic in Common or the worker, that is build-1's. docs/design/12-ui-spec.md is R2-C2-build-1's, put this pane's words in docs/design/14-ui-labels.md. If this task needs a file outside its list, stop and ask Lead.

The driver: a person may be at this PC. Send a click, a key or a shot only with Revit in front, after 5 minutes idle and with no meeting window, and stop when another input comes (CLAUDE.md). A wait for that is no failure, say how long.

Rule ids: R670 to R674. Question ids: Q521 to Q523. Take no other id.

# Acceptance tests

tests/acceptance/test_r2_pane.py, those marked R2-C4-revit-test. Tests of the pane go in dotnet/SiteAdaptation.Tests/R670_ThreeStagePaneTests.cs.

# Done when

- Those tests pass.
- The 3 cards, the gear and Advanced are seen in Revit on S1 from an empty model, each by a screenshot under the work root named in steps/revit-results/R2-C4-revit-test.md, and one screenshot per card with no real value in steps/ui-shots/.
- Every word on the pane is in docs/design/14-ui-labels.md, and a card that is not built yet says so.
- Advanced holds the old 9 step rows, closed by default, in every mode.
- Reported in steps/r2-check.md like the other tasks of R2, by Lead.

# Revit check needed

Yes, this task is the Revit check.
