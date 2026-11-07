---
id: FAST-7-build-3
lane: build-3
stage: 1
status: open
allowed:
  - dotnet/SiteAdaptation.AddIn/App/PaneHost.cs
  - dotnet/SiteAdaptation.AddIn/Pane/BuildChain.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageCards.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageText.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml
  - dotnet/SiteAdaptation.AddIn/Pane/StagesPage.xaml.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageResults.cs
  - dotnet/SiteAdaptation.AddIn/Pane/StageResultsText.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneText.cs
  - dotnet/SiteAdaptation.AddIn/Pane/PaneSettings.cs
  - dotnet/SiteAdaptation.AddIn/Pane/WorkFolder.cs
  - dotnet/SiteAdaptation.AddIn/Pane/SitePanePage.xaml
  - dotnet/SiteAdaptation.AddIn/Pane/SitePanePage.xaml.cs
  - dotnet/SiteAdaptation.AddIn/Revit/SettingsEntity.cs
  - dotnet/SiteAdaptation.AddIn/SetUpSite/
  - dotnet/SiteAdaptation.AddIn/Commands/SetUpSiteBody.cs
  - dotnet/SiteAdaptation.AddIn/ReviewData/
  - dotnet/SiteAdaptation.AddIn/ScanResults/DataCheckRows.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/DataCheckText.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ScanResultsViewModel.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ScanResultsText.cs
  - dotnet/SiteAdaptation.AddIn/HowItWorks/
  - dotnet/SiteAdaptation.AddIn/Help/
  - dotnet/SiteAdaptation.AddIn/Fast7/
  - dotnet/SiteAdaptation.UiShots/
  - docs/design/14-ui-labels.md
  - dotnet/SiteAdaptation.Tests/UiLabelTests.cs
  - dotnet/SiteAdaptation.AddIn/ScanResults/ScanResultsWindow.xaml
  - dotnet/SiteAdaptation.Tests/FAST7/
  - dotnet/SiteAdaptation.Tests/FAST1/R688_BuildChainTests.cs
  - dotnet/SiteAdaptation.Tests/FAST5/R740_BuildReadTests.cs
  - docs/rules/23-fast7-r760-up.md
  - docs/rules/index.md
  - steps/revit-results/FAST-7-build-3.md
  - steps/ui-shots/
  - steps/heads-up/build-3.md
  - steps/seam.md
  - steps/log.md
---

# Scope

The polish of run 1, Bader's order of 7 Nov 10:05, in files FAST-5-build-1 does not touch, split by exact path above. The findings are in steps/seam.md. In this order:

1. F12 first. A Build that stops says why in a box, the step and the reason, not only at the end of the long line of card 2. Write the box and a method of StagesViewModel that shows it. The one call sits in PaneHost.cs, FAST-5's, where chain.Ended reports a stop: write the line in steps/seam.md for build-1 with the exact call.
2. F1. Card 1 holds step 1 Set up site, so an empty model never meets "2 Read data needs step 1".
3. F2. Set up site takes the baseplan of the chosen folder, the one the data check found, and a line says the dry run is needed before Apply.
4. F3. Card 1 holds Review data.
5. F7. The project choice, the input rows and the scope survive a model closed without save, or the pane says plainly the setup was not saved, never "Set up this project first" over the rows of a read.
6. F9. Settings has a gear. Report is grey while card 3 waits for stage 2. The folder picker has a path field and opens at the folder chosen before. The header of the check window says villa plots of all plots.
7. F17. The window How it works teaches the 3 cards, not the nine steps of version 1.
8. F24 of the short check, the part in PaneHost.cs: ChooseProjectFolder asks for the step after the folder by StagesViewModel.StepAfterFolder, so another folder, or step 1 run by its button first, never meets the box 2 Read data needs step 1. PaneHost.cs and BuildChain.cs are on this list since 7 Nov 16:40, a fault of the short check in them comes here.
9. F6 and F13 last, the smaller version CHOSEN by Lead on 7 Nov 17:15: F13, a Close of 3 Review data with nothing changed counts as saved and the Build goes on. F6, a step 2 the chain puts back runs as the read for the build of R741, no check window, no second Confirm, never twice in one Build. The read for the build before Check (R741) stays whole. No merge of it before the short check ends. They live in the Build chain, PaneHost.cs and BuildChain.cs, FAST-5's. Start them only when FAST-5 is done on main, then Lead moves the files to this task. If FAST-5 closed them, write that and skip.

Revit: you may open Revit for your own check, Bader's order of 7 Nov 10:05. The test host and the listener on a copy of the 01A model, never on the real one, under python/window_lock.py, one lane at a time. Keep the driver rule of .claude/rules/revit.md. Write what you opened in steps/revit-results/FAST-7-build-3.md and in the PR body. revit-test and build-1 also use Revit today. revit-test stops at the end of its next part, saving its copy, while the file revit-wanted-build-3.txt stands in the lanes folder of the work root. Write it when you need Revit, delete it when you are done. The window lock decides who goes, wait for it, never end another lane's Revit.

Rule ids: R760 to R769. Question ids: Q590 to Q599. Take no other id.

# Done when

Each item of the list closed in a PR or skipped with its reason, and a window shot of each changed window in steps/ui-shots/ with no real value in view.

# Build rules

- No vibe code. Add only what the task lists. A change not in the task goes in steps/seam.md as a line for Lead, not in the code.
- Wiring. Every new function has a caller named in the PR. A part nothing calls is a failure.
- Logic. Every rule has its id, input, output and test. Where the task is silent, take the smallest version, mark it CHOSEN in the PR, go on. Do not stop.
- Folder structure. New files go only in the folders of the allowed list. No new folder without a line in the PR saying why.
- Any project. No ROSHN value in code. Every project value comes from config, rules or the Excel. No hand editing of JSON by a user.
- Each PR body: plan items done, skipped and extra with the reason, callers, what was opened in Revit, the log entry.
- Push at least every 2 hours. Open the PR when one card or one function works, not at the end. Merge main before every push.
- Keep steps/heads-up/<lane>.md current: what changed, where, what calls what, what is next. One page.

At the end, put your final statement in the PR body and in steps/heads-up/<lane>.md, then push.
