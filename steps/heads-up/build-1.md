# build-1 heads-up, FAST-1-build-1

6 Nov 2026. Restarted from ebb346e as the one session of build-1. PR 1 holds items 1 and 2, label revit-later.

## What changed, where

- dotnet/SiteAdaptation.AddIn/Pane/StageText.cs: every word of the pane in 3 stages (R680, R681).
- dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs: StageCard, StageStatus (stage 1 = steps 1 to 3, stage 2 = 4 to 8, stage 3 = 9), StageRead (the latest read folder of the work folder and its data check rows), ProjectFolderPick (writes inputRoot into the config copy of a new project under the work root only), StagesViewModel.
- dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml and .cs: the docked page, 3 cards, Settings, Advanced closed over the pane of version 1.
- dotnet/SiteAdaptation.AddIn/Pane/PaneViewModel.cs: RequestRun(int), RefreshSteps raises Statuses.
- dotnet/SiteAdaptation.AddIn/App/PaneHost.cs: SetupDockablePane docks StagesPage, WorkFolderNow, ChooseProjectFolder, FixItems.
- dotnet/SiteAdaptation.UiShots/ShotsStages.cs: pane-stage-1, -2, -3 in steps/ui-shots/.
- dotnet/SiteAdaptation.Tests/FAST1/R680_StagePaneTests.cs, net48 only.
- docs/design/14-ui-labels.md: 26 Pane entries, CHOSEN. UiLabelTests count 440 to 466.
- dotnet/SiteAdaptation.TestHost/AddinProbe.cs: PageType finds StagesPage by DockedPage, the docked page since FAST-1.

## What calls what

PaneHost.SetupDockablePane makes StagesViewModel and StagesPage. Card 1 Choose project folder raises ChooseFolderRequested, PaneHost.ChooseProjectFolder writes the folder then model.RequestRun(2). Fix items raises FixItemsRequested, PaneHost.FixItems opens ConfirmProfileStep.ShowConfirm. Card 3 Report calls model.RequestRun(9). Card 2 Build is grey and says it is not wired yet.

## Fixed this session

- StagesPage has no accessor a test alone reads, the test reads the Frame after the dispatcher runs its navigation.
- StageRead.Read gives the reason of a stopped or unreadable data check, card 1 says it and Fix items stays off.
- A fresh pane with no read shows No data check yet. Each need shows its plain line under it.
- R680_StagePaneTests 7 of 7 on net48.

## Next, in order

Item 3 Settings and the rules per project (R2-C3 points), item 4 the one Build with Preview and coordinates, item 5 stage 3 issues count and source mix bar, Update from report, item 6 Q504 no road level, item 7 a shot of each window.

## Open for Lead

- Opening each window in Revit is FAST-3-revit-test's.
- The comment naming R680 and R681 in R640_WorkerSeamTests.cs can go once PR 1 merges.
- AddinRunNames.Page of dotnet/SiteAdaptation.Common/AddinRun.cs:140 still names SitePanePage, seam line.
- Item 3 work in progress on branch task/FAST-1-build-1-rules, the rules table of a project.
