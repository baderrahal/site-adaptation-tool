# build-3, FAST-7-build-3

The polish of run 1 of FAST-3. Updated 7 Nov 2026.

## What changed

- Item 1, F12, R760. dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs StagesViewModel.ShowStop(RunEnd, line) shows the line of a Build that stopped, rolled back or was cancelled in the box StopBox at the top of card 2, dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml. PressBuild, the command of card 2, clears it before the Revit side runs. No new word, the line is the one BuildChain.Ended gives.
- dotnet/SiteAdaptation.UiShots: the draft shot pane-stage-2-stopped, LaidOutStages takes an optional action on the view model before the page is drawn.

## What calls what

- PaneHost.cs Buttons.ChainEnded, after chain.Ended, calls stages.ShowStop(outcome.End, line). That line is build-1's to add, PaneHost.cs is on FAST-5, the exact text is in steps/seam.md.
- The Build button of card 2 calls StagesViewModel.PressBuild, which clears the box, then BuildRequested.

## Waits on

- docs/design/14-ui-labels.md on the FAST-7 list, for the new words of F1, F2, F3, F7, F9 and F17, line in steps/seam.md.

## Next

- F1, card 1 holds Set up site. Then F2, F3, F7, F9, F17. F6 and F13 once FAST-5 is on main and Lead moves PaneHost.cs and BuildChain.cs here.
