# build-1 heads-up, FAST-1-build-1

6 Nov 2026. Merged: PR 228 (items 1, 2), PR 232 (item 3, 2b), PR 234 (item 4 part 1). Open: PR 237 (item 8, two scripts), PR 238 (item 4 part 2, the chain). Ready on branches: task/FAST-1-build-1-stage3 (item 5, card 3), task/FAST-1-build-1-q504 (item 6, Q504), each stacked on the one before. All label revit-later.

## What changed, where

- dotnet/SiteAdaptation.AddIn/Pane/: the pane in 3 stages (R680, R681). StageCards.cs holds StageCard, StageStatus, StageRead, ProjectFolderPick, StagesViewModel. StagesPage.xaml docks it, Advanced holds the pane of version 1.
- dotnet/SiteAdaptation.AddIn/RulesWindow/ProjectRulesTable.cs, ProjectRulesViewModel.cs, ProjectRulesWindow.xaml: the rules table of a project (R682, R683). Install-SiteAdaptation.ps1 ships manual.json (R684). ScanResults/DataCheckStep.cs SheetInput (R685).
- Pane/SiteBuildPlan.cs, SiteBuildFlow.cs, SiteBuildPreviewWindow.xaml: the plan of siteBuild behind Build, the scope, the coordinates, the one ask for rules, the 2D preview (R686, R687).
- Pane/BuildChain.cs and Commands/Buttons.cs ChainEnded: the steps 1 (only when not run), 4 to 8 as one run, a waited on step first (R688).
- Pane/StageResults.cs: card 3, issues of the newest build_plan.json and of the steps, the source mix of its values, Open rules from a rule issue, Update from report (R689, R690), the count of plots with no road level (R691, Q504).
- TestHost: AddinProbe finds StagesPage. Run-TestHost.ps1 and Run-Parallel.ps1 lose the free memory floor (item 8, PR 237).
- Tests in dotnet/SiteAdaptation.Tests/FAST1/, net48 only: R680, R682, R685, R686, R688, R689. Shots in steps/ui-shots/: pane-stage-1 to 3, project-rules, build-preview.

## What calls what

PaneHost.SetupDockablePane makes StagesViewModel and StagesPage. Card 1: ChooseProjectFolder then RequestRun(2), FixItems opens the Confirm window. Settings, Rules, Open: OpenRules. RulesLineNow gives the amber line. Card 2 Build: SiteBuildFlow.Run over BuildPlace, then BuildChain.Start and RequestRun per step, each end back by Buttons.ChainEnded. Card 3: ResultsNow reads StageResults, Update from report runs the ribbon command through RunButtonHandler.Command.

## Next, in order

Lead merges 237 and 238, then the stage3 branch opens as PR 6 and q504 as PR 7. Then the SITEADAPT_LISTENER_MIN_FREE_GB line of Start-RevitListener.ps1 goes once FAST-2 drops RevitListenerSetting.Refusal (Q570).

## Open for Lead

- Rule lines: R686, R687 in PR 239, R688 in the body of PR 238, R689 to R691 in PR 6 and 7.
- Opening each window in Revit is FAST-3-revit-test's.
- ParsonsModelTests R310 fails at random on this PC in quick tests, seen 4 times today, FAST-2's.
