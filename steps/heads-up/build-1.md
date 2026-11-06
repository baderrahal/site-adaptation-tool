# build-1 heads-up, FAST-1-build-1

7 Nov 2026, 02:00. Lead's order of 01:30 done: PR 248, the last line of item 8, open for Lead to merge on green. Task status stays open, its Done when needs a shot of each window in Revit, which FAST-3 owes.

## Final statement

- Merged: PR 228 (items 1, 2, the pane in 3 stages, stage 1 wired), PR 232 (item 3 and 2b, the rules table of a project, the parameter sheet), PR 234 (item 4 part 1, the plan behind Build, the 2D preview, the coordinates), PR 237 (item 8, no free memory floor in Run-TestHost.ps1 and Run-Parallel.ps1), PR 238 (item 4 part 2, the one Build runs the steps).
- Merged since: PR 243 (item 5, card 3), PR 244 (item 6, Q504).
- Open, for Lead to merge on green: PR 248 (item 8 last line, Start-RevitListener.ps1 gives no SITEADAPT_LISTENER_MIN_FREE_GB, Q570). The test FAST1/R701_ListenerScriptTests.cs keeps it out. R448_RevitListenerTests, TestHostTests, InstallScriptTests and ParsonsModelTests: 231 passed on net48 and on net8.0, quick tests passed. One review pass: PASS, no REAL fault.
- Left: no item of FAST-1-build-1 in code. Item 7, a shot of each window in Revit, moved to FAST-3. The draft renders of every new window are in steps/ui-shots/: pane-stage-1 to 3, project-rules, build-preview.
- Not seen in Revit: all of it. This lane may not open Revit, every PR carries revit-later.
- Done when, "a person goes from an empty model through the 3 cards on the S1 folder with no JSON edit": UNKNOWN until FAST-3 runs it in Revit.

## What changed, where

- dotnet/SiteAdaptation.AddIn/Pane/: the pane in 3 stages (R680, R681). StageCards.cs holds StageCard, StageStatus, StageRead, ProjectFolderPick, StagesViewModel. StagesPage.xaml docks it, Advanced holds the pane of version 1.
- RulesWindow/ProjectRulesTable.cs, ProjectRulesViewModel.cs, ProjectRulesWindow.xaml: the rules table of a project (R682, R683). Install-SiteAdaptation.ps1 ships manual.json (R684). ScanResults/DataCheckStep.cs SheetInput (R685).
- Pane/SiteBuildPlan.cs, SiteBuildFlow.cs, SiteBuildPreviewWindow.xaml: the plan of siteBuild behind Build, the scope, the coordinates, the one ask for rules, the 2D preview (R686, R687).
- Pane/BuildChain.cs and Commands/Buttons.cs ChainEnded: the steps 1 (only when not run), 4 to 8 as one run, a waited on step first (R688).
- Pane/StageResults.cs: card 3, the issues of the newest build_plan.json, the source mix of its values, Open rules from a rule issue, Update from report (R689, R690), the count of plots with no road level (R691, Q504).
- TestHost: AddinProbe finds StagesPage, two scripts lose the floor.
- Tests in dotnet/SiteAdaptation.Tests/FAST1/, net48 only: R680, R682, R685, R686, R688, R689.

## What calls what

PaneHost.SetupDockablePane makes StagesViewModel and StagesPage. Card 1: ChooseProjectFolder then RequestRun(2), FixItems opens the Confirm window. Settings, Rules, Open: OpenRules, RulesLineNow gives the amber line. Card 2 Build: SiteBuildFlow.Run over BuildPlace, then BuildChain.Start and RequestRun per step, each end back by Buttons.ChainEnded. Card 3: ResultsNow reads StageResults, Update from report runs the ribbon command through RunButtonHandler.Command.

## Open for Lead

- Rule lines R689 and R690 in the body of PR 243, R691 in the body of the Q504 PR. R688 in the body of PR 238.
- ParsonsModelTests R310 failed at random in quick tests on this PC 6 times today, FAST-2's R707 takes its CIM limit.
- docs/rules/12-lane-c-r430-up.md:23, the R448 text still names SITEADAPT_LISTENER_MIN_FREE_GB as an input, a docs line for Lead.
- Latent, not fixed: the run handler of the pane holds one step number, so a row press under Advanced in the instant between two steps of a Build replaces the next step and the build waits. Update from report is on while a build runs.
