# Heads-up

## Live status

- Lane: C, round SCAN-UI, branch feat/lane-c-scan-ui, worktree Site Adaptation Tool Lane C WT
- Time: 2026-11-01 00:20, demo freeze at 09:30
- Now: task 3 New project picks from the scan
- Done: the board, the settings fields, task 4 Current files (R441), task 1 What the tool read (R435, R436), task 2 button 2 scan and gate (R438, R439)
- Next: task 3, tasks 5 and 6 after 4i merges, task 7 Revit pictures by WindowShots of Lane B 6a, records, PR
- Blocked: none. Tasks 5 and 6 wait for 4i on main
- Lane B: 6a merged as PR 115 (326422a), DEMO now. Lane D: 4j-1 merged as PR 114, 4j-1b next (Q250)

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane B 6a, 1 Nov 2026.

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

- Lane B 6a: the test host runs buttons 1 to 9 by number and Rules and Update from report by name with no click (R384)
- Lane B 6a: the test host draws every window in Revit to PNG, the shots against the boards in steps/ui-shots/README.md
- Lane B 6a: fixes found in Revit: the run id of levelExcel (Q274), the run log of 4 Check that 9 reads, the upgrade progress left open (R108)
- Lane B 5d: buttons 5, 6 and 8, 7 at the FFL, Q115 walls, Q116 ramps, all proven in Revit by 6a
- Lane D 4j-1: a scanner of any CAD, Excel and PDF, worker command scan, python/scan.py, design part 17
- Lane C NEWPROJ-UI: the New project window, the How it works page, the pane rows with no file name

## New rules

- R384 Test host runs every button (Lane B 6a). R378 to R380 buttons 5, 6 and 8 (Lane B 5d)
- R420 to R427 of the scanner (Lane D 4j-1). R430 to R434 of New project and saved starts (Lane C NEWPROJ-UI)

## Wiring

- Lane B: ButtonsRunHost runs Buttons.Run(number) and Buttons.Run(name), answers each Dialog seam with no click, WindowShots draws each window
- Lane B: 4 Check writes run_4_<run id>.json, ReportRun.CheckRun.Find of 9 reads it. Buttons 5, 6, 8 by FloorsBody, LevelsBody, WallsBody
- Lane D: worker command scan, WorkerReads.Commands to CadScan.Command. Button 2 does not start it (Q246)
- Lane C: the pane opens NewProjectDialog and HowItWorksDialog, Save setup goes through SetupSave.Apply

## Open risks

- All 336 masses are flagged by the footprint check, the masses stand right, the drawing holds plot blocks (Q276)
- Update from report stops on the plan of 4 Check (Q278, Lane A). Links waits on the upgrade first step limit (Q275)
- New project shows Plots found and Villa types as not read until the scanner hook C1 is built
- Lane D: no villa type source reaches the strong share on ROSHN (Q254), Road Layout scan time (Q250)

## Next

- Lane B: DEMO for 1 Nov 11:00, then Links after Q275. Lane C: SCAN-UI. Lane D: 4j-1b scan speed
