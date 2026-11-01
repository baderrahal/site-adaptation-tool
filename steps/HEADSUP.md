# Heads-up

## Live status

### Lane D

- Lane: D, round 4j-1b, scan speed, branch feat/addin-4j1b-speed. Time: 2026-11-01 02:30. Blocked: none
- Now: review fixes in, set A, the renamed set and the Road Layout run again, then the PR, before the 09:30 freeze
- Done: measured, PlotShapes by a cell index, threads (R428), cap (R429), set A and renamed equal to 4j-1, Road Layout 462 to 484 s, gates green

### Lane A

- Lane A 4i, 00:40: R407 to R412, review 1, breaker F1 fixed, parity 32 runs 0 failures, past job 0 of 636,012 values differ
- Merged as PR 116, then 4j-2 with no Revit until lane-b and lane-c are clear

### Lane B and C

- Lane B 6a, 00:10, demo freeze at 09:30: NBH 012 through buttons 1 to 9 from a new project with no click, 6.7 min, 2444 MiB peak,
  10 window shots. Merged as PR 115. Next DEMO, the add-in installed, C:\SA-demo, the demo sheet. Links waits on Q275
- Lane C SCAN-UI, 04:35, done: PR 117 tasks 1 to 4 (R435 to R441), PR 119 tasks 5 and 6 (R442, R443), task 7 the Revit shots of the DEMO
  run in steps/ui-shots/README.md, the shots outside git. Waits for Bader: Q288 to Q295, Q300 to Q302, Q305. Blocked: none

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane D 4j-1b, Lane C SCAN-UI, Lane A 4i, Lane B 6a, 1 Nov 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow. config and rules: project.json with the files beside it, the override list, the rules profile with the R401 status block and the scan block
- docs: rules (index.md finds a rule by id), design parts and boards, the mock, the repo map. dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files and the scan
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane, windows and the Revit side of each button. dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type, Scan/ the scan logic
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows. dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers and the plan build, the twin of the C# side with scan.py, the run scripts. steps: progress, log, plan, questions, latent findings, ui-shots, the history of the rounds

## What this round changed

- Lane B 6a: the test host runs buttons 1 to 9 by number and Rules and Update from report by name with no click (R384), and draws every window to PNG
- Lane B 6a: fixes found in Revit: the run id of levelExcel (Q274), the run log of 4 Check that 9 reads, the upgrade progress left open (R108)
- Lane B 5d: buttons 5, 6 and 8, 7 at the FFL, Q115 walls, Q116 ramps, all proven in Revit by 6a
- Lane D 4j-1: a scanner of any CAD, Excel and PDF, worker command scan, python/scan.py, design part 17. 4j-1b: speed and a time cap
- Lane A 4i: the design basis switch, the Rev 6 back yard slopes, the new project check (R407 to R412)
- Lane C SCAN-UI: What the tool read after the scan of button 2, New project dropdowns, Current files, cluster confirm, door level from

## New rules

- R384 Test host runs every button (Lane B 6a). R378 to R380 buttons 5, 6 and 8 (Lane B 5d)
- R420 to R429 of the scanner (Lane D 4j-1, 4j-1b). R407 to R412 (Lane A 4i). R430 to R443 of Lane C

## Wiring

- Lane B: ButtonsRunHost runs Buttons.Run(number) and Buttons.Run(name), answers each Dialog seam with no click, WindowShots draws each window. 4 Check writes run_4_<run id>.json, ReportRun.CheckRun.Find of 9 reads it. Buttons 5, 6, 8 by FloorsBody, LevelsBody, WallsBody
- Lane D: worker command scan, WorkerReads.Commands to CadScan.Command. Button 2 does not start it (Q246)
- Lane C: the pane opens NewProjectDialog and HowItWorksDialog. Buttons calls ReadDataBody.For, ScanStep.Wrap runs the scan, ScanGate.Ask opens ScanResultsDialog, NewProjectDialog.Facts is LatestScanFacts

## Open risks

- All 336 masses are flagged by the footprint check, the masses stand right, the drawing holds plot blocks (Q276). Update from report stops on the plan of 4 Check (Q278, Lane A). Links waits on the upgrade first step limit (Q275)
- New project shows Plots found and Villa types as not read until the scanner hook C1 is built. Lane D: no villa type source reaches the strong share on ROSHN (Q254), a large drawing scanned in part past the cap (Q258). Lane C: the read of button 2 does not take the picks yet (Q292)

## Next

- Lane B: DEMO for 1 Nov 11:00, then Links after Q275. Lane C: tasks 5 and 6 on 4i, task 7. Lane D: Bader's answers to Q243 to Q258. Lane A: 4j-2
