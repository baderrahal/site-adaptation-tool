---
id: OLD-1d3-build-2
lane: build-2
stage: 2
status: done
allowed:
  - dotnet/SiteAdaptation.AddIn/
  - dotnet/SiteAdaptation.Common/
  - dotnet/SiteAdaptation.Tests/
  - dotnet/SiteAdaptation.UiShots/
  - docs/rules/
  - docs/truth.md
  - docs/addin-mock.md
  - steps/buttons-guide.md
  - steps/ui-shots/
  - steps/latent.md
---

# Scope

Round 1 step d of steps/plan.md, part 3 of 4: the 3 cards are the only way in. Read docs/truth.md first, its standing rules hold here. Bader 12 Nov: "Advanced no longer shows the 9 old step buttons. The 3 cards are the only way in. The step code stays only as the engine behind them."

1. Measure first, and write it into the PR: every place a person can start one of the steps 1 to 9 alone today, with file and line. The audit of 12 Nov found the step rows 1 to 9 of Advanced (Pane/SitePanePage.xaml, PaneViewModel) and the numbered buttons of the ribbon (App/RibbonLayout.cs, Commands/ButtonCommands.cs).
2. Advanced no longer shows the 9 step rows, nor the i of a step row. What else Advanced holds stays: Browse, Pick surfaces, Use a saved project, How it works, the settings and rules rows, New project, Export.
3. The ribbon. CHOSEN by Lead, the smallest reading of "the only way in": the numbered buttons 1 to 9 and Update from report leave the ribbon. Show pane and Site Adaptation Rules stay. If a card cannot reach what a ribbon button did, name it in the PR and keep that button.
4. The step code stays as the engine: BuildChain, RequestRun, Buttons.Run and the bodies of the steps, and every test host job that runs a step. A function that only a removed row or button called goes, with the proof by `git grep -n -w`.
5. The words of the pane must match what is built: no line of a card, a box, the help or How it works tells a person to press a step row or a numbered button that is gone. steps/buttons-guide.md is the source of Help/help.json, built by python/build_help.py, run it and commit the built file if the guide changes.
6. docs/truth.md: the line that marks Advanced without step buttons as not built yet is set right.
7. Tests change with the code, never deleted to go green. A test of a removed row or button goes with it and is named in the PR. A test that ran a step through a row is turned to run it through the engine.

Not in this task: past job mode, light mode, the data book reads of the old read, the test run support inside the product library. No new window, no new setting. You open no Revit, the renders of UiShots are your proof of the pane.

Rule ids R845 and R846. Question id Q658. Take no other id.

# Done when

- A test holds that Advanced shows no step row and the ribbon no numbered button, and that the three cards still reach steps 1 to 9 and Update from report.
- A render of the pane with Advanced open, by UiShots, is named in the PR, with no real value in it.
- Build and quick tests pass, with the test counts before and after and the reason for each test that went. Every function left has a caller.
- The PR body starts with `Task: OLD-1d3-build-2` and says Not seen in Revit.
