# Heads-up

## Live status

### Lane A

- Lane: A, round 4i, branch feat/addin-4i-rev6
- Time: 2026-11-01 00:40
- Now: the gate green after PR 115, the PR of 4i and its checks
- Done: R407 to R412, review 1, breaker F1 fixed, parity 32 runs 0 failures, past job 0 of 636,012 values differ, the log entry, PROGRESS to WAITING
- Next: the PR and its merge, a message to lane-d, then 4j-2 with no Revit until lane-b and lane-c are clear
- Blocked: none

### Lane B

- Lane: B, round DEMO, branch feat/lane-b-demo, worktree Site Adaptation Tool Lane B WT
- Time: 2026-11-01 02:55, demo freeze at 09:30
- Now: update from main after PR 116 and 117, then the install, the saved project ROSHN Makkah and a check run
- Done: 6a PR 115. C:/SA-demo: inputs and config copies, start and done models, outputs, the base boundary probe, 3 slides, scan of the copies
- Next: install, the saved project with the scan picks, check run, the window to lane-c, demo sheet, PR
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
