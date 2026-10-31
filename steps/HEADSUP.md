# Heads-up

## Live status

- Lane: D, round 4j-1, the scanner, branch feat/addin-4j1-scanner, worktree Site Adaptation Tool Lane D WT
- Now: the ROSHN and renamed scans again after the centroid fix, then reviewer and breaker
- Done: items 1 to 8 of the plan, ROSHN set A and the renamed set scanned, parity, main merged after PR 112
- Next: rerun the parity, reviews, records, PR
- Blocked: none. The Road Layout scan passed 40 minutes in the pair trace and was stopped (Q250)
- Time: 2026-10-31 19:50

Lane C, done: NEWPROJ-UI merged as PR 112 on 2026-10-31 (its status at 18:57 read full gate, then PR and merge).

Rewritten by each round, never appended, each lane keeps the lines of the others. Last: Lane D 4j-1 and Lane C NEWPROJ-UI, 31 Oct 2026.

## Repo map

- .claude and .github: agents, hooks, the AI MAX skill, the gate workflow
- config and rules: project.json with the files beside it, the override list, the rules profile with its scan block (4j-1)
- docs: rules (Lane A, and R420 to R427 of Lane D in part 11), design parts and boards, the mock, the repo map
- dotnet/CadReader: the worker, reads CAD, PDF and xlsx, writes the Excel files and the scan
- dotnet/SiteAdaptation.AddIn: the Revit add-in, ribbon, pane and windows
- dotnet/SiteAdaptation.Common: the logic of the add-in and the worker, no Revit type, Scan/ the scan logic
- dotnet/SiteAdaptation.Tests and UiShots: the gate tests, the offscreen renders of the windows
- dotnet, others: the placer, the test host, the prototype reader, the Civil 3D probe and watchers
- python and tools: the readers of the past rounds, the test checker with scan.py, the laptop run scripts
- steps: progress, log, plan, questions, ui-shots, the history of the rounds

## What this round changed

- Lane D 4j-1: a scanner of any CAD, Excel and PDF the user gives, the worker command scan and its twin python/scan.py (R420 to R427)
- Lane D 4j-1: scan.json per run in the run folder, docs/design/17-scanner.md, the window needs in steps/waiting-on-others.md
- Lane C NEWPROJ-UI: the window New project, the page How it works, the pane rows Setup, Rules and Help, Save setup (R367)
- Lane C NEWPROJ-UI: no add-in text names a .json file, a saved project ROSHN Makkah, boards and design part 18

## New rules

- R420 to R427 of Lane D, the CAD layer inventory, the three layer proposals, the drawing link, the villa type match and score, the scan file
- R430 to R434 of Lane C, the files the tool needs, the check before saving, Save setup, the template workbook, saved projects

## Wiring

- Lane D: worker command scan, WorkerReads.Commands to CadScan.Command. python/scan.py main. Button 2 does not start it (Q246)
- Lane C: pane box Open New project to NewProjectDialog.Show, Help to HowItWorksDialog.Show, Save setup to SetupSave.Apply

## Open risks

- Lane D: the villa type proposal takes the CAD labels, which agree with the 7 keys on 495 of 2323 plots (Q247)
- Lane D: the pair trace of the scan on the Road Layout passed 40 minutes (Q250)
- Lane C: Save setup reaches the model only with the next real run (Q280). The scanner hook NewProjectDialog.Facts is empty until Lane B

## Next

- Lane D: the PR of 4j-1. Lane C: Bader answers Q280 to Q287. Lane B fills IDrawingFacts from scan.json
