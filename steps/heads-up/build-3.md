# build-3, FAST-7-build-3

The polish of run 1 of FAST-3. Updated 7 Nov 2026, after PR 260.

## What changed

- PR 252, F12, R760. StagesViewModel.ShowStop(RunEnd, line) in dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs shows a Build stop in the box StopBox of card 2, StagesPage.xaml. PressBuild clears it.
- PR 257, F1 and F3, R761 and R762. Card 1 holds 1 Set up site (SetUpCommand) before Choose project folder, which before step 1 runs step 1 (PR 3), and 3 Review data (ReviewCommand) beside Fix items.
- F9, R763. The gear SettingsGear in the head of StagesPage.xaml. Card 3 Report is grey while step 9 waits. DialogPicker.Folder in SitePanePage.xaml.cs opens dotnet/SiteAdaptation.AddIn/Fast7/FolderDialog.cs, the folder dialog of Windows with a path field, at DialogPicker.Start, which StagesViewModel.ChooseFolder sets from ProjectFolderPick.Chosen, the inputRoot of the config.
- F9, R767. DataCheckResult.OtherPlots, ScanResultsViewModel.PlotsFound and PlotsFoundLabel: "n of m, villa plots of all plots" in the check window.
- F17, R764. HowItWorksText.Line names the 3 cards, HowItWorksViewModel.CardOf heads the first step of each card.
- F7, R765. StageStatus.Of takes readThere: no setup and a read say StageText.SetupNotSaved.
- F2 part, R766. SetUpSiteViewModel.ApplyWaitLine beside a grey Apply.
- docs/design/14-ui-labels.md, 7 new CHOSEN words, UiLabelTests.cs count 524.
- PR 260, F2, R768. dotnet/SiteAdaptation.AddIn/Fast7/SetUpBaseplan.cs Fill takes the Baseplan of the folder by Fast5.BuildRead.FillRows, Baseplan row only, called by StagesViewModel.ChooseFolder after a person chose the folder, never by Set up site itself, so the inputRoot a config copy came with never fills it. StagesViewModel.ChooseFolder before step 1 writes the folder by ProjectFolderPick.Write and runs step 1, after it asks the Revit side for the read as before. PickFolder and WorkRootNow stand for the dialog and the work root.
- PR 4, F24, R769. ProjectFolderPick.Write leaves the config as it is for the same folder. StagesViewModel.StepAfterFolder, called by ChooseFolder and, once build-1 adds the line of steps/seam.md, by PaneHost.ChooseProjectFolder.

## What calls what

- PaneHost.cs Buttons.ChainEnded calls stages.ShowStop, build-1's line. Card 2 calls PressBuild.
- Card 1: SetUpCommand and ReviewCommand call PaneViewModel.RequestRun(1 or 3). Card1.Command calls ChooseFolder, then PaneHost.ChooseProjectFolder, which calls DialogPicker.Folder, then FolderDialog.Pick.
- StagesViewModel.Refresh calls RefreshRead first, then StageStatus.Of with readFolder.

## Next

- F6 and F13 in PaneHost.cs and BuildChain.cs, once FAST-5 is done and Lead moves the files here.

## Final statement

FAST-7 so far: 3 PRs merged, 252, 257 and 260, R760 to R768, and PR 4 with R769 for F24 of the short check, whose call in PaneHost.cs waits for build-1. Done: F12 the stop box, F1 card 1 holds Set up site and Choose project folder runs it first, F2 Set up site takes the Baseplan of the chosen folder and says the dry run comes before Apply, F3 card 1 holds Review data, F7 the pane says plainly the setup was not saved, F9 the gear, Report grey while card 3 waits, the folder dialog with a path field at the folder chosen before, and villa plots of all plots in the check window, F17 How it works teaches the 3 cards. Shots of each changed window are in steps/ui-shots, made up values only. Nothing of it is seen in Revit yet, each PR carries revit-later. Left: F6 and F13 in PaneHost.cs and BuildChain.cs, which Lead moves to FAST-7 after the short check in Revit passes. FAST-5 has not closed them: BuildChain on main still puts an out of date step 2 or 3 back into the Build. The task stays open until then.
