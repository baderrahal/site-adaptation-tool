# Heads-up

## Live status

### Lane A

- Lane: A, round 4j-2, branch feat/addin-4j2-split-walls
- Time: 2026-11-01 02:25
- Now: R415 and R416, the walls of irregular plots, coded by a helper, then parity of both modes
- Done: 4i merged as PR 116, the plan, past splits and walls measured, R414 merged in off (8 of 82, Q257, Q258), R417 stopped (Q259), main merged after PR 117
- Next: the split test and the walls by the measurements, parity, the PR before 09:30 or a PAUSED status
- Blocked: none

### Lane B

- Lane: B, round 6a, branch feat/lane-b-6a, worktree Site Adaptation Tool Lane B WT
- Time: 2026-11-01 00:10, demo freeze at 09:30
- Now: the end records and the PR of 6a, then the queued DEMO round
- Done: 6a for Masses, NBH 012 through buttons 1 to 9 from a new project with no click, 6.7 min, 2444 MiB peak, 10 window shots
- Next: DEMO, the add-in installed, C:\SA-demo, the demo sheet. Links waits on Q275
- Blocked: none

### Lane C and D

- Lane C: round SCAN-UI, branch feat/lane-c-scan-ui, worktree Site Adaptation Tool Lane C WT, 2026-11-01 01:40, freeze 09:30
- Lane C now: the PR of tasks 1 to 4, then tasks 5 and 6 on 4i in a second PR, task 7 when the window lock is free. Blocked: none
- Lane C done: tasks 1 to 4 (R435 to R441), review and breaker fixes. Lane D: 4j-1 merged as PR 114, 4j-1b next (Q250)

Rewritten by each round, never appended, the other lanes' lines kept. Last: Lane C SCAN-UI, 1 Nov 2026.

## Repo map

- .claude, .github: agents, hooks, the gate. config, rules: project.json, overrides, profile. docs: rules by id, design, boards
- dotnet: CadReader the worker and the scan, AddIn the ribbon, pane and windows, Common the logic, Tests and UiShots the gate
- python and tools: the twin of the C# side with scan.py, the run scripts. steps: records, ui-shots, history

## What this round changed

- Lane C SCAN-UI: What the tool read, opened by button 2 after the scan, New project dropdowns, Current files in 1 Set up site
- Lane A 4i: the plan by Manual Rev 06 behind designBasis.mode, door level source, cluster proposals, new project check
- Lane B 6a: the test host runs every button with no click and draws each window in Revit, fixes found in Revit
- Lane D 4j-1: the scanner of any CAD, Excel and PDF, worker command scan, python/scan.py

## New rules

- R435 to R441 (Lane C SCAN-UI), R430 to R434 now written too. R407 to R412 (Lane A 4i). R384 (Lane B 6a). R420 to R427 (Lane D)

## Wiring

- Lane C: Buttons calls ReadDataBody.For, ScanStep.Wrap runs the scan, ScanGate.Ask opens ScanResultsDialog. A test host run reads with no scan
- Lane C: NewProjectDialog.Facts is LatestScanFacts, View opens ScanResultsDialog. SetUpSiteBody wraps CurrentFilesDialog, ReportBody adds Current files
- Lane B: ButtonsRunHost answers each Dialog seam, WindowShots draws each window. Lane A: BuildSteps takes designBasis.mode

## Open risks

- Lane C: the read of button 2 does not take the picks yet (Q292), the first press hashes the prototypes once. Lane D: Q254, Q250
- Lane B: all 336 masses flagged by the footprint check (Q276). Update from report stops on the plan of 4 Check (Q278)

## Next
- Lane C: tasks 5 and 6 on 4i, task 7. Lane B: DEMO for 1 Nov 11:00. Lane A: 4j-2. Lane D: 4j-1b scan speed
