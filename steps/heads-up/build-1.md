# build-1 heads-up, FAST-1-build-1

Stopped at a safe point on Bader's word, 6 Nov 2026. Work in progress on branch task/FAST-1-build-1, no PR opened, nothing merged. The build compiles, 4 tests fail, listed below.

## What changed, where

- dotnet/SiteAdaptation.AddIn/Pane/StageText.cs: every word of the pane in 3 stages (R680, R681).
- dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs: StageCard, StageStatus (stage 1 = steps 1 to 3, stage 2 = 4 to 8, stage 3 = 9), StageRead (the latest read folder of the work folder and its data check rows), ProjectFolderPick (Choose project folder writes inputRoot into the config copy of a new project under the work root only, never the shipped set or a past job), StagesViewModel.
- dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml and .cs: the docked page. Head with Settings, 3 cards each with status, i icon and one main button, card 1 shows the data check rows and Fix items, Advanced is an Expander closed by default holding the pane of version 1 whole in a Frame.
- dotnet/SiteAdaptation.AddIn/Pane/PaneViewModel.cs: RequestRun(int) added, RefreshSteps raises Statuses so the cards follow.
- dotnet/SiteAdaptation.AddIn/App/PaneHost.cs: SetupDockablePane docks StagesPage, new WorkFolderNow, ChooseProjectFolder (folder picker, ProjectFolderPick.Write, then step 2 Read data), FixItems (the Confirm window of R2-C1 over the latest read). teller is now a field.
- dotnet/SiteAdaptation.UiShots/Shots.cs and ShotsStages.cs: drafts pane-stage-1, -2, -3, one render per card with made up rows. Not yet written to steps/ui-shots/.
- dotnet/SiteAdaptation.Tests/FAST1/R680_StagePaneTests.cs: 5 tests, net48 only by #if NETFRAMEWORK.
- docs/design/14-ui-labels.md: 24 new Pane entries, all CHOSEN.

## What calls what

PaneHost.SetupDockablePane makes StagesViewModel and StagesPage. Card 1 Choose project folder raises ChooseFolderRequested, PaneHost.ChooseProjectFolder writes the folder then model.RequestRun(2). Fix items raises FixItemsRequested, PaneHost.FixItems opens ConfirmProfileStep.ShowConfirm. Card 3 Report calls model.RequestRun(9). Card 2 Build is grey and says it is not wired yet.

## Failing, to fix next

1. UiLabelTests R372_each_entry_not_marked_chosen: holds a fixed count of 440 CHOSEN entries (UiLabelTests.cs:199), now 464. That file is not on the list of the task, seam line below.
2. UiLabelTests R372_every_text: the word Build was missing under Pane, added just now, not rerun.
3. R680 cards test: StagesPage.OldPage reads Frame.Content, which is null until the Frame navigates. Fix: keep the old page in a field.
4. R681 latest read test: StageRead.Rows lets FormatException through on a data check with no needs. Fix: catch FormatException too.

## Next, in order

Fix the 4 above, render the 3 card shots into steps/ui-shots/, open PR 1 (label revit-later, this lane cannot open Revit by .claude/rules/revit.md). Then the rules table under Settings (R2-C3 points), the one Build button with Preview and coordinates, stage 3 issues count and source mix bar, Update from report.

## Open for Lead

- Rule lines R680 (the pane in 3 stages) and R681 (Choose project folder, data check rows, Fix items) for docs/rules.
- Opening each window in Revit is blocked for this lane by .claude/rules/revit.md, so FAST-3-revit-test or Lead does it.
