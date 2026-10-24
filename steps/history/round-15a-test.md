# Round 15a test, 2026-10-24, the test host drives Revit, one prototype per run

Full record of the round. The top entry of steps/log.md is the short form.

## Setup

- The session folder C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a was empty when the round began, the 15a work had merged as pull request 15. The main folder ran Round 12b on another branch, so I cloned main (e73da18) into the empty folder and worked there on branch test/revit-testhost-15a. The main guard hook loaded from the folder at session start, the registry hook of R106 is new and not live in this session
- Answer key: prototypes.json copied once to prototypes_original_imc.json, 10,355,725 bytes, time stamp 2026-10-24 01:51:15 kept, hash equal, also equal to prototypes_imc_past.json
- plan_from_past.py: 337 of 337 plots written, missing a value 337 (floors.yard 337, floors.generic 1). Plots per file: C10 15, TH1 16, VD2 29, DP1 34, DP2 12, DP3 10, DP4 32, DT1 44, DT3 68, TH3 16, VD4 27, VL4 15, VL6 19. linkName 337 of 337, 2 differ from RRE_ZZ_SP_RVT-Name and are in conflicts
- The IMC model is in Zone 02\ARC, not Zone 02 as the prompt said, Bader confirmed. Host format 2024 central, all 13 prototypes format 2021 central (read from the file bytes and later by BasicFileInfo)

## Registry incident

- My first Install-TestHost.ps1 ran New-Item -Force on HKCU\Software\Autodesk\Revit\Autodesk Revit 2024\CodeSigning. That recreated the key and removed four Always Load answers of other add-ins, 3f6d8a2a-5b0a-4a9f-9a1b-2e2c5d3f6b11, 5e1c8a20-9f4a-4d8b-b3ca-a1e0d3f2b102, a1b2c3d4-e5f6-7890-abcd-ef1234567890 and 51c9c81c-c2e2-4d7e-8301-1ccbb187a928, all value 1. The same run set 1 for the reader, the placer and the test host. Putting the four back was blocked by the permission check and Bader said not to restore them. Since then nothing in the registry was read or written by me, the install runs with -SkipSigningReport and R106 blocks registry commands

## Dialog watcher

- Dry start 1: the watcher looked at top windows only, the security box is a child window of the WPF main window, so it read the whole main window, answered RCRC Green and then skipped RCRC Green Audit. I stopped that Revit, no document open
- Dry start 2: the watcher found the boxes. Do Not Load for RCRC Green and RCRC Green Audit, then it stopped Revit on the box of this tool's reader as R108 said. The box shows Name, Publisher and Location and no GUID, its buttons are CCPushButton panes
- Bader chose Load Once for the three DLLs of this tool by exact Location. Dry start 3: Do Not Load 2, Load Once 3, start screen in 1.5 min, closed with exit code 0. Every later run answered the same 5 boxes

## Prototype runs

All 13 passed on the first run, 337 of 337 plots placed. Second method seeds per run in the table, 3 of 3 each. PowerShell spot check of 5 random plots across files, seed 24: origin 0 mm, axes equal, Name equal, 5 of 5.

| Code | Folder | Run min | Open s | Real run s | Plots | Within 1 mm | Name, Z, params | Seed | Result |
|---|---|---|---|---|---|---|---|---|---|
| C10 | 2021 | 3.7 | 100 | 86 | 15 | 15 | all equal | 144020 | pass |
| TH1 | 2021 | 4.0 | 106 | 92 | 16 | 16 | all equal | 888568 | pass |
| VD2 | 2021 | 2.4 | 15 | 91 | 29 | 29 | all equal | 176326 | pass |
| DP1 | 2021 | 4.2 | 113 | 96 | 34 | 34 | all equal | 578660 | pass |
| DP2 | 2021 | 2.3 | 11 | 90 | 12 | 12 | all equal | 202009 | pass |
| DP3 | 2021 | 2.4 | 10 | 98 | 10 | 10 | all equal | 428326 | pass |
| DP4 | 2021 | 2.3 | 11 | 90 | 32 | 32 | all equal | 830905 | pass |
| DT1 | 2021 | 3.2 | 11 | 143 | 44 | 44 | all equal | 816940 | pass |
| DT3 | 2024 | 1.1 | 10 | 13 | 68 | 68 | all equal | 553524 | pass |
| TH3 | 2024 | 0.9 | 10 | 4 | 16 | 16 | all equal | 31278 | pass |
| VD4 | 2024 | 0.9 | 10 | 4 | 27 | 27 | all equal | 151005 | pass |
| VL4 | 2024 | 0.8 | 10 | 4 | 15 | 15 | all equal | 286773 | pass |
| VL6 | 2024 | 0.9 | 10 | 5 | 19 | 19 | all equal | 634113 | pass |

- Every check: missing 0, extra 0, placed twice 0, rotation, mirror, Z equal FFL, instance Name and the 9 parameters equal on every plot. RRE_ZZ_SP_Prototype-FFL is empty on every new link, the placer never writes it. Placer report: stopped 0, rolled back 0, skipped 0, issues 0
- Reader fixes: every loaded link has a box (linkExtents), spots with a host 986 of 987 on every run, the one without is element 996508
- Dialogs inside Revit: TaskDialog_Views_Related_To_Analytical_Changed, the upgrade notice, answered Close, 0 to 2 per run. Failure lines are warnings from inside the 2021 links during their upgrade (balusters not created, an auto resolved door family error), deleted as R104 says
- The original IMC model unchanged after every run, by hash, size and time
- DT3 was started once on the 2021 file by the chain when Bader's change came in and I stopped it in the real run step. That copy was kept as 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-DT3_run1.rvt by the next run

## Upgrade to Revit 2024

One prototype per Revit run, one after another (07:24 to 07:57), before the parallel windows were asked for. Every source unchanged by hash, size and time. Every target checked by BasicFileInfo and again by reading the format text from its bytes: 13 of 13 Format 2024, Central, same names as the 2021 folder.

| Code | Source | MB | Target | MB | Upgrade s | Save s | Links in it | Result |
|---|---|---|---|---|---|---|---|---|
| C10 | 2021 | 77.8 | 2024 | 81.4 | 92 | 17 | 6, 0 loaded | ok |
| TH1 | 2021 | 63.5 | 2024 | 66.2 | 96 | 16 | 5, 0 loaded | ok |
| VD2 | 2021 | 64.4 | 2024 | 67.8 | 94 | 17 | 5, 0 loaded | ok |
| DP1 | 2021 | 79.2 | 2024 | 82.7 | 102 | 26 | 8, 0 loaded | ok |
| DP2 | 2021 | 72.3 | 2024 | 76.0 | 100 | 18 | 8, 0 loaded | ok |
| DP3 | 2021 | 75.6 | 2024 | 78.1 | 100 | 20 | 9, 0 loaded | ok |
| DP4 | 2021 | 68.7 | 2024 | 72.0 | 96 | 17 | 4, 0 loaded | ok |
| DT1 | 2021 | 99.3 | 2024 | 102.9 | 142 | 29 | 7, 0 loaded | ok |
| DT3 | 2021 | 107.2 | 2024 | 110.2 | 134 | 30 | 9, 0 loaded | ok |
| TH3 | 2021 | 64.3 | 2024 | 67.0 | 93 | 14 | 5, 0 loaded | ok |
| VD4 | 2021 | 66.4 | 2024 | 69.5 | 99 | 17 | 4, 0 loaded | ok |
| VL4 | 2021 | 70.2 | 2024 | 73.3 | 86 | 18 | 7, 0 loaded | ok |
| VL6 | 2021 | 76.5 | 2024 | 80.1 | 118 | 17 | 5, 0 loaded | ok |

- Revit made a _backup folder next to each new central. plan_imc_past.json now gives Prototypes_2024 to the placer and names Prototypes as sources.prototypeSourceFolder. The plan the first 8 runs used is kept as plan_imc_past_2021_folder.json, its plots are equal to the new plan

## Parallel windows

- Coded and tested on fakes (R110): one job file and token per window in SITEADAPT_JOB, per job outputs and refusal, the watcher logs the process id, Run-Parallel.ps1 keeps 8 GB free and runs at most 3
- Not run in Revit. Memory before: total 15.8 GB, free 5.2 GB (available 4.8 GB, commit 29.1 of 36.3 GB), under the 8 GB rule, so no window could start. The upgrade the parallel run was for was already done and its targets are never overwritten

## Review

- A fresh reviewer read the guard and found no way to make a normal session act and 9 edge points, all fixed with tests: stop at once on a refused job, the copy checked as a file of its own (no hard link, symlink or junction, not the original), stream and device paths, a catch-all in OnStartup with NaN and final line break fixed, claim by move before the check, no command link for an unlisted dialog and no link position saved on unload, errors rolled back only in the test host transactions, the token only on the Revit process, Newtonsoft handed out only to this tool. The per window guard of R110 came later and had no fresh review

## Files left on this machine

- Zone 02\ARC: 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-<code>.rvt for the 13 codes and their Revit backups .0001.rvt or .0002.rvt, and TEST-DT3_run1.rvt. Zone 02\ARC\Prototypes_2024: the 13 centrals and their _backup folders
- Documents\SiteAdaptation: prototypes_original_imc.json, plan_imc_past.json, plan_imc_past_2021_folder.json, testhost_<code>_*.json per run, testhost_old\ with outputs moved aside. No job file and no claimed job file is left
- %APPDATA%\Autodesk\Revit\Addins\2024: SiteAdaptation (reader), SiteAdaptationPlacer, SiteAdaptationTestHost with Watcher, and their three manifests
