# Heads up

## Live status

- Lane: B, phase 5d, branch feat/lane-b-5d, worktree Site Adaptation Tool Lane B WT
- Time: 2026-10-31 19:10
- Now: the last two fixes, breaker C and D in Levels and Floors, and the ramp front gap of Q116
- Done: buttons 5, 6 and 8 with tests, W6, Q115, Q116, Q214, Q215, Q156, Q220, the worker path, the fixes of review 1 and breaker pass 1 for Levels and the worker
- Next: merge the fixes, the gate, the Revit run of 1 to 8 on NBH 012 in Masses, the questions moved to Q260 up, records, the PR
- Blocked: none

## Repo map

- config/: project.json, the project settings, and the layer, area and plot number lists
- rules/: the rules profile of each project, roshn-makkah.json with the R401 status block
- docs/rules/: the rules by part, index.md finds a rule by id
- docs/design/: the add-in design by part, its README names each part
- python/: the readers, the plan build and the checkers, the twin of the C# side, tests in python/tests
- dotnet/: the C# solution, CadReader the worker, SiteAdaptation.Common the logic, AddIn the Revit add-in, TestHost the Revit runs, Tests
- steps/: the live state, the log, the plan, the questions, the latent findings and the history of old rounds
- tools/: the retired run script of the lanes

## What this round changed

- Button 5 Floors and ramps and 6 Levels with the level Excel wait (R378, R379)
- Button 8 Walls of type BW along the plan walls (R380)
- Button 7 places each villa at its FFL and writes Prototype-FFL (W6)
- Plan walls leave the measured span of a cluster pair open, keyRules.wallOpen, weak at 0.7409 (Q115)
- The ramp of the 18 turned plots on the open edge the walls take (Q116)
- The build takes the Prototypes folder of the pane (Q214), the 2024 copies go under the work root (Q215)
- Plot takes its fixed GUID from addin.fixedParameters (Q156)

## New rules

- R378 Button 5 Floors and ramps
- R379 Button 6 Levels
- R380 Button 8 Walls

## Wiring

- Buttons.cs calls FloorsBody.For, LevelsBody.For and WallsBody.For for buttons 5, 6 and 8
- FloorLoad and WallLoad find the plan of the last done 4 Check, FloorsRun, LevelsRun and WallsRun decide, RevitFloorDocument and RevitWallDocument carry it out
- LevelsRun waits for the worker command levelExcel through ReportWorkerClient
- ButtonsRun.Known lets the test host run buttons 1 to 8

## Open risks

- 6 Levels and 8 Walls have not yet committed in Revit, the Revit run of 5d stopped at 6 twice
- Links mode is not run in 5d, it moves to 6a. A Links run needs the pane Prototypes row on the 2024 copies (Q214, Q215)
- The wall rule is weak, 888 of 3,427 walls differ from the keys (Q115, Q230)

## Next

- 6a, one NBH through all 9 steps from a new project, with Links mode
