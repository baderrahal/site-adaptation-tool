# Heads-up

## Live status

- Lane: C, round NEWPROJ-UI, branch feat/lane-c-new-project, worktree Site Adaptation Tool Lane C WT
- Now: full gate on both runners after the renumbering (R430 to R434, Q280 to Q287) and the line ending fix
- Done: plan items 1 to 8, reviewer and breaker passes, 7 REAL faults fixed with tests, records and log entry written
- Next: gate green, diff against the plan, PR, merge, messages to the other lanes
- Blocked: none
- Time: 2026-10-31 18:57

Rewritten by each round, never appended. Last: Lane C NEWPROJ-UI, 31 Oct 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow
- config and rules: project.json with the files beside it, the override list, the rules profile
- docs: rules (Lane A), design parts and boards, the mock, the repo map
- dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane and windows
- dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows
- dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers of the past rounds, the test checker, the laptop run scripts
- steps: progress, log, plan, questions, ui-shots, the history of the rounds

## What this round changed

- New window New project: five files with who gives each, Browse, Get template, Found or Missing, the stop box, Start from
- New page How it works: the 9 steps, two notes, Close and Open New project
- Pane: Setup and Rules rows with no file name, a Help row, Use a saved project, a new first open box
- No text of the add-in names a file ending in .json, a gate test holds it
- Save setup lays the picks over the settings JSON in memory, the next real run writes it (R367)
- A saved project ROSHN Makkah was written to the work root, Saved projects, from the repo config
- Boards in docs/design/boards, design part 18, labels in part 14

## New rules

- R430 Files the tool needs. Input none, output the five rows. Test R430_SetupTests
- R431 Check files before saving. Input settings and disk, output states and stops, read only. Test R430_SetupTests
- R432 Save setup in settings format. Input settings, start and picks, output ProjectSettings. Test R430_SetupTests
- R433 Template of an input workbook. Input an input name and config, output an empty xlsx. Test R433_TemplateBookTests
- R434 Saved projects to start from. Input the work root, output the pre-settings there. Test R430_SetupTests

## Wiring

- Pane box Open New project, Setup Open and Edit, Help New project: NewProjectRequested, PaneHost.OpenNewProject, NewProjectDialog.Show
- Pane Help How it works and the first open link: HowItWorksDialog.Show, its Open New project opens New project
- Save setup: SetupSave.Apply, then PaneViewModel.LoadSettings. Check files: SetupCheck.Run. Get template: TemplateBook.Write
- Saved project names: SavedStarts.List, PaneViewModel.SetSavedStarts and the Start from list

## Open risks

- Save setup reaches the model only with the next real run (Q280). Messages that add a work file name still name a .json file (Q287)
- The scanner hook NewProjectDialog.Facts gives no survey point or layers until the Lane B round. No Revit run, windows checked offscreen only
- The saved project names the repo folder site-adaptation-tool, which follows the branch checked out there

## Next

- Bader answers Q280 to Q287 and approves the four drafts (Q286). Lane B fills IDrawingFacts. A Revit run takes the screenshots
