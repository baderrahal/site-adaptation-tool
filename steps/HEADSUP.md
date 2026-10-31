# Heads-up

## Live status

- Lane: B, round 6a, branch feat/lane-b-6a, worktree Site Adaptation Tool Lane B WT
- Time: 2026-10-31 21:30
- Now: Q274 fix and R384 named jobs (button 9, Site Adaptation Rules, Update from report) by a developer, the 13 Revit 2024 prototype copies under the work root by a runner
- Done: 5d merged as PR 113, plan of 6a written
- Next: window shots in the host, the Masses run 1 to 9 on NBH 012, the Links run, shots against the boards, records, PR
- Blocked: none
- Lane C: NEWPROJ-UI merged as PR 112 on 31 Oct, changes the windows of buttons 1 to 4 tonight

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane B 5d, 31 Oct 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow
- config and rules: project.json with the files beside it, the override list, the rules profile with the R401 status block
- docs: rules (index.md finds a rule by id), design parts and boards, the mock, the repo map
- dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane, windows and the Revit side of each button
- dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows
- dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers and the plan build, the twin of the C# side, the run scripts
- steps: progress, log, plan, questions, latent findings, ui-shots, the history of the rounds

## What this round changed

- Lane B 5d: button 5 Floors and ramps, 6 Levels with the level Excel wait, 8 Walls of type BW
- Lane B 5d: button 7 places each villa at its FFL and writes Prototype-FFL (W6)
- Lane B 5d: plan walls leave the measured span of a cluster pair open, weak at 0.7409 (Q115)
- Lane B 5d: the ramp of the 18 turned plots on the open edge at its road level (Q116)
- Lane B 5d: the build takes the pane Prototypes folder (Q214), Plot takes a fixed GUID (Q156)
- Lane C NEWPROJ-UI: the New project window, the How it works page, the pane rows with no file name

## New rules

- R378 Button 5 Floors and ramps. R379 Button 6 Levels. R380 Button 8 Walls (Lane B 5d)
- R430 to R434 of New project and saved starts (Lane C NEWPROJ-UI)

## Wiring

- Buttons.cs calls FloorsBody.For, LevelsBody.For and WallsBody.For for 5, 6 and 8, ButtonsRun.Known lets the test host run 1 to 8
- FloorLoad and WallLoad find the plan of the last done 4 Check, FloorsRun, LevelsRun and WallsRun decide, RevitFloorDocument and RevitWallDocument act
- LevelsRun waits for the worker command levelExcel through ReportWorkerClient, which starts Worker\CadReader.exe
- Lane C: the pane opens NewProjectDialog and HowItWorksDialog, Save setup goes through SetupSave.Apply

## Open risks

- 6 Levels, 7 at the FFL and 8 Walls have not yet committed in Revit, button 6 stopped three times
- Links mode moves to 6a, a Links run needs the pane Prototypes row on the 2024 copies (Q214, Q215)
- The wall rule is weak, 888 of 3,427 walls differ from the keys (Q115, Q266)
- Lane C: Save setup reaches the model only with the next real run (Q280)

## Next

- Lane B: 6a, one NBH through all 9 steps from a new project, with Links mode
- Lane C: Bader answers Q280 to Q287, Lane B fills IDrawingFacts (join item C1)
