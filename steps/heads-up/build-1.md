# build-1 heads-up, FAST-1-build-1

6 Nov 2026. PR 228 (items 1 and 2) merged as 389af4c, PR 232 (item 3 and 2b) as 4c11b72. PR 3 holds item 4 part 1, the plan behind Build and the 2D preview, label revit-later.

## What changed, where

- dotnet/SiteAdaptation.AddIn/Pane/: the pane in 3 stages (R680, R681). StageCards.cs holds StageCard, StageStatus, StageRead, ProjectFolderPick, StagesViewModel. StagesPage.xaml docks it, Advanced holds the pane of version 1.
- dotnet/SiteAdaptation.AddIn/RulesWindow/ProjectRulesTable.cs: RulesFileNames (the names of the internal ProjectRulesFile of Common, a test holds them equal), ProjectRulesPlace (config, rules profile, work root, work folder, mode, templates, who), ProjectRulesTable (Refusal, Read, AmberLine, Parse, UseManual, Change, Run by WorkerRunner.Run in process). R682, R683.
- dotnet/SiteAdaptation.AddIn/RulesWindow/ProjectRulesViewModel.cs, ProjectRulesWindow.xaml: the rules table window. ProjectRulesText.cs its words.
- dotnet/SiteAdaptation.AddIn/Install-SiteAdaptation.ps1: ships rules/templates/manual.json in ShippedSet, R684.
- dotnet/SiteAdaptation.AddIn/ScanResults/DataCheckStep.cs: SheetInput sends the parameter sheet with dataCheck, R685.
- dotnet/SiteAdaptation.AddIn/Pane/SiteBuildPlan.cs: SiteBuildPlace, SiteBuildResult, SiteBuildPlan (ScopeLine, Make runs siteBuild in process, Read, CoordinatesLine). SiteBuildFlow.cs: SiteBuildFlow (the press, one ask for rules, the stop, the preview), SiteBuildPreview (Outlines, Fit, Line). SiteBuildPreviewWindow.xaml the 2D preview. SiteBuildText.cs its words. R686, R687.
- dotnet/SiteAdaptation.TestHost/AddinProbe.cs: PageType finds StagesPage.
- Tests in dotnet/SiteAdaptation.Tests/FAST1/: R680_StagePaneTests, R682_ProjectRulesTests, R685_SheetInputTests, R686_SiteBuildFlowTests, net48 only.

## What calls what

PaneHost.SetupDockablePane makes StagesViewModel and StagesPage. Card 1 Choose project folder, PaneHost.ChooseProjectFolder, then RequestRun(2). Fix items, PaneHost.FixItems, the Confirm window. Settings, Rules, Open raises RulesRequested, PaneHost.OpenRules opens ProjectRulesWindow over PaneHost.RulesPlace. StagesViewModel.Refresh asks RulesLineNow, PaneHost.RulesLineNow reads ProjectRulesTable.AmberLine when the config, rules or log changed. DataCheckStep.Start adds SheetInput. Card 2 Build raises BuildRequested, PaneHost runs SiteBuildFlow over BuildPlace with AskRules, UseManual and ShowPreview, then StagesViewModel.ShowBuild.

## Next, in order

Item 4 part 2, Build runs the steps 1 and 4 to 8 of version 1 as one run with one progress line. Item 5 stage 3, issues count, source mix, Update from report, and open the rules table from a rule issue (point 1). Item 6 Q504. Item 7 a shot of each window. Item 8 the RAM floor lines of three TestHost scripts, after FAST-2 makes its tests take either.

## Open for Lead

- R682 to R685 for docs/rules, lines in the body of PR 232. R686 and R687 in the body of PR 3.
- Opening each window in Revit is FAST-3-revit-test's.
