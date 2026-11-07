# build-3, FAST-7-build-3

The polish of run 1 of FAST-3. Updated 7 Nov 2026 19:35, with PR 6 of F25, the last PR of FAST-7.

## What changed

- PR 252, F12, R760. StagesViewModel.ShowStop(RunEnd, line) in dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs shows a Build stop in the box StopBox of card 2, StagesPage.xaml. PressBuild clears it.
- PR 257, F1 and F3, R761 and R762. Card 1 holds 1 Set up site (SetUpCommand) before Choose project folder, which before step 1 runs step 1 (PR 260), and 3 Review data (ReviewCommand) beside Fix items.
- F9, R763. The gear SettingsGear in the head of StagesPage.xaml. Card 3 Report is grey while step 9 waits. DialogPicker.Folder in SitePanePage.xaml.cs opens dotnet/SiteAdaptation.AddIn/Fast7/FolderDialog.cs, the folder dialog of Windows with a path field, at DialogPicker.Start, which StagesViewModel.ChooseFolder sets from ProjectFolderPick.Chosen, the inputRoot of the config.
- F9, R767. DataCheckResult.OtherPlots, ScanResultsViewModel.PlotsFound and PlotsFoundLabel: "n of m, villa plots of all plots" in the check window.
- F17, R764. HowItWorksText.Line names the 3 cards, HowItWorksViewModel.CardOf heads the first step of each card.
- F7, R765. StageStatus.Of takes readThere: no setup and a read say StageText.SetupNotSaved.
- F2 part, R766. SetUpSiteViewModel.ApplyWaitLine beside a grey Apply.
- docs/design/14-ui-labels.md, 7 new CHOSEN words, UiLabelTests.cs count 524.
- PR 260, F2, R768. dotnet/SiteAdaptation.AddIn/Fast7/SetUpBaseplan.cs Fill takes the Baseplan of the folder by Fast5.BuildRead.FillRows, Baseplan row only, called by StagesViewModel.ChooseFolder after a person chose the folder, never by Set up site itself, so the inputRoot a config copy came with never fills it. StagesViewModel.ChooseFolder before step 1 writes the folder by ProjectFolderPick.Write and runs step 1, after it asks the Revit side for the read as before. PickFolder and WorkRootNow stand for the dialog and the work root.
- PR 4, F24, R769. ProjectFolderPick.Write leaves the config as it is for the same folder. PaneHost.ChooseProjectFolder(stages) keeps the folder before, writes, then calls StagesViewModel.FolderChosen, which card 1 calls too: drops a Baseplan of the old folder, fills the new one, runs StepAfterFolder(statuses, before, chosen).

- PR 5, F13, R770. ReviewDataSession.Closed, called by ReviewDataDialog.Show after the window: a Close with nothing unsaved runs Save once.
- PR 5, F6, R771. BuildChain.Current and putBack, a step goes back once per Build. dotnet/SiteAdaptation.AddIn/Fast7/QuietRead.cs, set in PaneHost.SetupDockablePane to chain.Current == 2. ScanGate.Ask and ConfirmProfileStep.Ended read QuietRead.On: no window, the picks kept, the Confirm skipped only when dotnet/SiteAdaptation.AddIn/Fast7/ConfirmedFor.cs Holds, the profile confirmed for the same scan folder. ConfirmedFor.Write runs after each Confirm that saved, in ConfirmProfileStep.Ended and PaneHost.FixItems. Line ScanResultsText.QuietKept.

- PR 6, item 10, F25, R772. BuildChain.committed: a step that committed in this Build is never put back. BuildChain.WaitsOnCommitted and PaneHost.RunWhenHashed run the next step once the pane hashed the committed one, at most 10 s later. CheckTableWindow CloseButton, OnClose, CheckTableText.Close.
- PR 5 review: ConfirmedFor.Write deletes the file of the Confirm before first.

## What calls what

- PaneHost.cs Buttons.ChainEnded calls stages.ShowStop, build-1's line. Card 2 calls PressBuild.
- Card 1: SetUpCommand and ReviewCommand call PaneViewModel.RequestRun(1 or 3). Card1.Command calls ChooseFolder, then PaneHost.ChooseProjectFolder, which calls DialogPicker.Folder, then FolderDialog.Pick.
- StagesViewModel.Refresh calls RefreshRead first, then StageStatus.Of with readFolder.

## Next

- Nothing on FAST-7. The Revit check of its 6 PRs is owed, each carries revit-later.

## Final statement

FAST-7 is done in 6 PRs, 252, 257, 260, 264, 267 and this one, R760 to R772. F12: a Build that stops says the step and the reason in a box of card 2. F1 and F2: card 1 holds 1 Set up site, Choose project folder runs it first on the folder chosen, which gives the Baseplan, and the same folder chosen again keeps step 1 done (F24). F3: card 1 holds 3 Review data. F7: the pane says plainly the setup was not saved with the model. F9: the gear, Report grey while card 3 waits, the folder dialog of Windows with a path field at the folder chosen before, villa plots of all plots in the check window. F17: How it works teaches the 3 cards. F13: a Review Close with nothing changed counts as saved. F6: a step 2 the Build puts back runs once, with no window, keeping the person's picks and the profile confirmed for the same folder. F25: after Check commits the Build goes on to step 5, and the Check window has a Close. Shots of the changed windows are in steps/ui-shots with made up values. Nothing of FAST-7 is seen in Revit yet. Open: F23, a scope change alone makes step 2 stale, Common's step states.
