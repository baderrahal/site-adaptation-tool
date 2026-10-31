# Heads-up

## Live status

- Lane: D, round 4j-1, the scanner, branch feat/addin-4j1-scanner
- Now: the scan on the ROSHN copies, net8.0 first, then net48 and Python, then the parity and the renamed set
- Done: plan item 1 spec (R420 to R427, docs/design/17-scanner.md, scan block of the profile), items 2 to 8 code and tests, the .NET gate 1517 net48 and 1340 net8.0 green
- Next: parity numbers, the renamed set, reviewer and breaker, records, PR
- Blocked: none
- Time: 2026-10-31, after the code of items 2 to 8

## Repo map

- config/: project.json, the project settings, and the layer, area and plot number lists
- rules/: the rules profile of each project, roshn-makkah.json, its scan block new in 4j-1
- docs/rules/: the rules by part, index.md finds a rule by id
- docs/design/: the add-in design by part, its README names each part
- python/: the readers, the build and the checkers, the twin of the C# side, tests in python/tests
- dotnet/: the C# solution, CadReader the worker, SiteAdaptation.Common the logic, AddIn the Revit add-in, Tests
- steps/: the live state, the log, the plan, the questions and the history of old rounds
- tools/: the run scripts of the test host

## What this round changed

- A scanner of any CAD, Excel and PDF the user gives, the worker command scan and python/scan.py
- Written in progress, the final list comes with the PR

## New rules

- R420 to R427 in docs/rules/11-scanner-r420-up.md

## Wiring

- Worker command scan, WorkerReads.Commands to CadScan.Command. python/scan.py main. Button 2 does not start it yet (the scanner window of Lane B)

## Open risks

- Pair candidates of the face trace on large drawings, time not measured yet

## Next

- The PR of 4j-1
