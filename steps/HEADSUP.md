# Heads-up

## Live status

### Lane A

- Lane: A, round FIX-1NOV-A2, branch fix/1nov-a2. Time: 2026-11-02 02:05. Blocked: none
- Done: R472, R473, R474, proofs on copies, review 1, breaker 1 and 2 fixed. Gate on 25c84c7 green, parity 32 runs 0 failures, past job plans byte-equal
- Now: the PR of FIX-1NOV-A2
- Next: merge when green, tell lane-c the steps of parts 8 and 9. Questions Q345 to Q349

### Lane B, C and D

- Lane B Task 6 (FIX-1NOV-B), 02:30, branch fix/1nov-b-task6: both passes done, Revit closed, lock free, main with PR 130 merged in, keeping both sides.
  Now: the gate on the merged tree, then merge PR 131. Then Lane D D3 on my word
- Lane C DEMO-FIX, 09:55, branch fix/lane-c-demo-fix: step rows of the pane run steps, a loader on every button, Confirm all in 3 Review
  data, idle buttons hidden (R444). Now: the gate and the PR, merge by 10:15, then lane-b reinstalls. Blocked: none
- Lane D D3, 04:27, branch fix/1nov-d3: 13 of 13 upgraded. Run D3L buttons 1 to 9 passed with 13 real files linked, 336 placed. Run D3S gave the masses back, 336 placed, and
  exited 1 on 13 .slog files in the backup folders of the 2024 copies, no real input changed. Holding the lock: two read jobs. Next: compare 5 plots, records, PR. Blocked: none

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane D D2, Lane D FIX-1NOV-D, Lane C DEMO-FIX, Lane B demo-fill, Lane A 4j-2, Lane D 4j-1b, Lane C SCAN-UI, Lane A 4i, Lane B 6a, 1 Nov 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow. config and rules: project.json with the files beside it, the override list, the rules profile with the R401 status block and the scan block
- docs: rules (index.md finds a rule by id), design parts and boards, the mock, the repo map. dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files and the scan
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane, windows and the Revit side of each button. dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type, Scan/ the scan logic
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows. dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers and the plan build, the twin of the C# side with scan.py, the run scripts. steps: progress, log, plan, questions, latent findings, ui-shots, the history of the rounds

## What this round changed

- Lane B 6a: the test host runs every button with no click (R384) and draws each window, fixes found in Revit (Q274, R108). 5d: buttons 5, 6, 8
- Lane D 4j-1: a scanner of any CAD, Excel and PDF, worker command scan, python/scan.py, design part 17. 4j-1b: speed and a time cap
- Lane A 4i: the design basis switch, the Rev 6 back yard slopes, the new project check (R407 to R412)
- Lane A 4j-2: split test off (R414), irregular plots (R415), wall face on the plot line (R416). FIX-1NOV-A: ramp joints behind join false, joint rows. A2: button 2 takes the picks, Update finds the plan of 4 Check
- Lane C SCAN-UI: What the tool read after the scan of button 2, New project dropdowns, Current files, cluster confirm, door level from

## New rules

- R384 Test host runs every button (Lane B 6a). R378 to R380 buttons 5, 6 and 8 (Lane B 5d)
- R420 to R429 of the scanner (Lane D 4j-1, 4j-1b). R407 to R412 (Lane A 4i). R414 to R416 (Lane A 4j-2). R470 to R474 (Lane A FIX-1NOV-A, A2). R430 to R443 of Lane C

## Wiring

- Lane B: ButtonsRunHost runs Buttons.Run(number) and Buttons.Run(name), answers each Dialog seam with no click, WindowShots draws each window. 4 Check writes run_4_<run id>.json, ReportRun.CheckRun.Find of 9 reads it. Buttons 5, 6, 8 by FloorsBody, LevelsBody, WallsBody
- Lane A: key_levels calls plan_rules.split_test, build plan_walls.inside. override_step and build call joint_levels, PlanOverrides.Of JointLevels, checks joint_rows, CheckRows.Step JointRows. ReadDataRun calls ReadPicks.Resolve, WorkerReads Take, UpdateFromReportRun.Read CheckRun.PlanOf
- Lane D: worker command scan, WorkerReads.Commands to CadScan.Command. Button 2 does not start it (Q246)
- Lane C: the pane opens NewProjectDialog and HowItWorksDialog. Buttons calls ReadDataBody.For, ScanStep.Wrap runs the scan, ScanGate.Ask opens ScanResultsDialog, NewProjectDialog.Facts is LatestScanFacts

## Open risks

- All 336 masses are flagged by the footprint check, the masses stand right, the drawing holds plot blocks (Q276). Update from report stops on the plan of 4 Check (Q278, Lane A). Links waits on the upgrade first step limit (Q275)
- New project shows Plots found and Villa types as not read until the scanner hook C1 is built. Lane D: no villa type source reaches the strong share on ROSHN (Q254), a large drawing scanned in part past the cap (Q258). Lane C: the read of button 2 does not take the picks yet (Q292)
- Lane A: R414 8 of 82 (Q303, Q304), no garden door (Q259). Joints wait on Q340, Q341, configs saved with no floors.rampJoint stop the build (C:\SA-demo\config)

## Next

- Lane B: DEMO for 1 Nov 11:00, then Links after Q275. Lane C: tasks 5 and 6 on 4i, task 7. Lane D: Bader's answers to Q243 to Q258. Lane A: Bader's answers to Q303, Q304 and Q259
