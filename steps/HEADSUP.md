# Heads-up

## Live status

- Lane: D, round 4j-1, the scanner, branch feat/addin-4j1-scanner, worktree Site Adaptation Tool Lane D WT
- Now: the full gate, the diff against the plan, then the PR of 4j-1
- Done: items 1 to 8, review fixes, villa type by prototype names (Bader 31 Oct), parity 0 differ, renamed set equal, records
- Next: PR 4j-1, then the queued round 4j-1b scan speed
- Blocked: none. The Road Layout scan passed 40 minutes in the pair trace and was stopped (Q250, round 4j-1b)
- Time: 2026-10-31 22:30
- Lane B: 5d merged as PR 113 on 31 Oct (its status at 20:40 read full gate, then the PR of 5d). Lane C: NEWPROJ-UI merged as PR 112

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane D 4j-1, Lane B 5d, 31 Oct 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow
- config and rules: project.json with the files beside it, the override list, the rules profile with the R401 status block and the scan block
- docs: rules (index.md finds a rule by id), design parts and boards, the mock, the repo map
- dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files and the scan
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane, windows and the Revit side of each button
- dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type, Scan/ the scan logic
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows
- dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers and the plan build, the twin of the C# side with scan.py, the run scripts
- steps: progress, log, plan, questions, latent findings, ui-shots, the history of the rounds

## What this round changed

- Lane D 4j-1: a scanner of any CAD, Excel and PDF, worker command scan, python/scan.py, scan.json per run, design part 17
- Lane B 5d: button 5 Floors and ramps, 6 Levels with the level Excel wait, 8 Walls of type BW
- Lane B 5d: button 7 places each villa at its FFL and writes Prototype-FFL (W6)
- Lane B 5d: plan walls leave the measured span of a cluster pair open, weak at 0.7409 (Q115), the ramp of the 18 turned plots (Q116)
- Lane B 5d: the build takes the pane Prototypes folder (Q214), Plot takes a fixed GUID (Q156)
- Lane C NEWPROJ-UI: the New project window, the How it works page, the pane rows with no file name

## New rules

- R420 to R427 of the scanner (Lane D 4j-1, docs/rules/11-scanner-r420-up.md)
- R378 Button 5 Floors and ramps. R379 Button 6 Levels. R380 Button 8 Walls (Lane B 5d)
- R430 to R434 of New project and saved starts (Lane C NEWPROJ-UI)

## Wiring

- Lane D: worker command scan, WorkerReads.Commands to CadScan.Command. python/scan.py main. Button 2 does not start it (Q246)
- Buttons.cs calls FloorsBody.For, LevelsBody.For and WallsBody.For for 5, 6 and 8, ButtonsRun.Known lets the test host run 1 to 8
- FloorLoad and WallLoad find the plan of the last done 4 Check, FloorsRun, LevelsRun and WallsRun decide, RevitFloorDocument and RevitWallDocument act. LevelsRun waits for the worker command levelExcel through ReportWorkerClient, which starts Worker\CadReader.exe
- Lane C: the pane opens NewProjectDialog and HowItWorksDialog, Save setup goes through SetupSave.Apply

## Open risks

- Lane D: no villa type source reaches the strong share on ROSHN, lead 0.116, the user chooses (Q254). Road Layout scan time (Q250)
- 6 Levels, 7 at the FFL and 8 Walls have not yet committed in Revit, button 6 stopped three times
- Links mode moves to 6a, a Links run needs the pane Prototypes row on the 2024 copies (Q214, Q215)
- The wall rule is weak, 888 of 3,427 walls differ from the keys (Q115, Q266). Lane C: Save setup waits for a real run (Q280)

## Next

- Lane D: 4j-1b scan speed. Lane B: 6a, one NBH through all 9 steps from a new project, with Links mode
- Lane C: Bader answers Q280 to Q287, Lane B fills IDrawingFacts (join item C1)
