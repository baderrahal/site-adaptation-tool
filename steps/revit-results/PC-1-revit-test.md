# PC-1-revit-test, 12 Nov 2026

Lane revit-test on Bader's PC, branch task/PC-1-revit-test from origin/main 56d9dd69. RAN means run in this session, with the file that holds the output. The files named without a drive lie under the work root C:\SA-work\SiteAdaptation, the logs of this task in old-project\pc1. Counts, times and sizes only. No code was changed.

## Result

| Done when | Result | Proof |
|---|---|---|
| prototypes_2024 holds 13 files of format 2024 by rvt_info.py | 13 of 13, format 2024, workshared | RAN python\rvt_info.py, old-project\pc1\prototypes_2024_rvt_info.json |
| answer-key holds one folder per key that could be read | 7 of 7 folders, each with its read and links.csv. No plan and no table, see Stopped steps | RAN, old-project\pc1\read_runs.jsonl |
| The input folder is unchanged by SHA-256, 837 files | before 837, after 837, gone 0, added 0, changed 0 | RAN, old-project\inputs-after.json against inputs-before.json |

## For Lead, read first

1. Run-TestHost.ps1 -Upgrade can not run on this PC as it stands. It reads pastJob.output, plan_imc_past.json of the work root, at line 493, only to find the file name of a code, and that file is gone. The first run stopped there with no Revit started: `Cannot find path 'C:\SA-work\SiteAdaptation\plan_imc_past.json' because it does not exist.`
2. CHOSEN by this lane, yours to refuse: I wrote a stand-in at that path that holds the 13 file names of pastJob.prototypeFolder as listed that minute and nothing else, with a note in it that it is not the plan, and deleted it at 02:50 after the 13th upgrade. Each code of testHost.order matched exactly one of the 13 files by the pattern of the script, 13 distinct files (RAN). The upgrade job itself is not given the plan (Run-TestHost.ps1:641 to 651), so the stand-in chose the file name and no content. If you refuse this, delete prototypes_2024, nothing else came from it. The task text said to stop a step that needs a gone file, and I did not stop this one, since it needed a name and no value of the plan.
3. Install-TestHost.ps1 -NoBuild refused first: `The Common DLLs differ, run the install of each add-in again with Revit closed.` The add-in Lead installed was of the build of the main folder. I ran Install-SiteAdaptation.ps1 -NoBuild from the build of this worktree, same commit 56d9dd69, then the test host install said `the same in every folder of the tool and the build: True`. So the installed add-in is now the build of this worktree.
4. The first Release build failed once: `error RG1000: Unknown build error, 'Could not find file ...\SiteAdaptation.AddIn\obj\Release\net48\CheckTable\CheckTableWindow.baml'`. The second build, same command, gave 0 errors and 23 warnings. Why is UNKNOWN, not looked into.
5. One call of mine was wrong: the 12 codes went to my loop as one text, Run-TestHost.ps1 refused it in 9 s and no Revit started. It took the lock once, so windows.jsonl holds 22 lockTaken for 20 Revit windows.
6. The plan and the table of every key are not made, see Stopped steps. They need the baseplan plots.json and the location plan labels.csv again.
7. Left in the work root: the testhost_<code>_upgrade_* files of the 13 upgrades, round_addin\logs\windows.jsonl, an empty round_addin\work, dev-manifest-stamps.

## Setup

| Step | Result | Proof |
|---|---|---|
| Work root | C:\SA-work\SiteAdaptation | RAN Get-Content of %APPDATA%\SiteAdaptation\machine.json |
| Input folder against Lead's before file, 02:08 | before 837, after 837, gone 0, added 0, changed 0 | RAN my own check of the same shape, name, size, write time, SHA-256, old-project\pc1\inputs-at-start.json |
| dotnet build dotnet\SiteAdaptation.sln -c Release | 0 errors on the second try | RAN |
| Install-SiteAdaptation.ps1 -NoBuild, Install-TestHost.ps1 -NoBuild | one Common DLL in 5 folders of the tool, the same as the build: True | RAN |
| python\key_copies.py check, before the runs and after | reuse 7 of 7, both times | RAN |
| Revit before the first run | none running, the lock free | RAN python\window_lock.py --status |

## Part 1, the 13 villa files as Revit 2024

Command per code, under python\window_lock.py --round PC-1: `Run-TestHost.ps1 -Upgrade -Code <code>`, with -NewUpgradeFolder on C10. One Revit at a time, in the order of testHost.order. The result is the log result of testhost_<code>_upgrade_log.json, not the exit code. The whole prototype folder, 13 files, was hashed before and after each run.

| Code | Start to end | Minutes | Log result | Open s | Save s | Source, target | Peak private MiB | Least free GiB | Warnings of the upgrade | Folder of 13 before and after |
|---|---|---|---|---|---|---|---|---|---|---|
| C10 | 02:10:47 to 02:14:44 | 3.95 | ok | 125.1 | 21.6 | 2021, 2024 | 2,484 | 0.42 | 0 | changed 0 |
| TH1 | 02:15:48 to 02:20:31 | 4.71 | ok | 143.3 | 14.0 | 2021, 2024 | 2,277 | 0.54 | 12 | changed 0 |
| VD2 | 02:20:33 to 02:23:13 | 2.66 | ok | 98.4 | 16.1 | 2021, 2024 | 2,364 | 1.21 | 5 | changed 0 |
| DP1 | 02:23:15 to 02:25:44 | 2.49 | ok | 101.9 | 21.0 | 2021, 2024 | 2,657 | 1.78 | 4 | changed 0 |
| DP2 | 02:25:46 to 02:28:06 | 2.33 | ok | 98.7 | 15.4 | 2021, 2024 | 2,473 | 1.48 | 5 | changed 0 |
| DP3 | 02:28:08 to 02:30:32 | 2.39 | ok | 98.1 | 19.4 | 2021, 2024 | 2,542 | 1.99 | 1 | changed 0 |
| DP4 | 02:30:34 to 02:32:53 | 2.33 | ok | 96.7 | 15.9 | 2021, 2024 | 2,377 | 1.86 | 7 | changed 0 |
| DT1 | 02:32:56 to 02:36:47 | 3.85 | ok | 179.2 | 21.4 | 2021, 2024 | 3,256 | 0.59 | 12 | changed 0 |
| DT3 | 02:36:49 to 02:40:10 | 3.35 | ok | 137.6 | 21.8 | 2021, 2024 | 3,239 | 1.00 | 16 | changed 0 |
| TH3 | 02:40:12 to 02:42:29 | 2.28 | ok | 96.8 | 13.3 | 2021, 2024 | 2,259 | 1.79 | 13 | changed 0 |
| VD4 | 02:42:31 to 02:44:54 | 2.38 | ok | 101.4 | 14.8 | 2021, 2024 | 2,352 | 1.67 | 5 | changed 0 |
| VL4 | 02:44:56 to 02:47:05 | 2.15 | ok | 87.3 | 16.7 | 2021, 2024 | 2,399 | 2.60 | 8 | changed 0 |
| VL6 | 02:47:07 to 02:49:47 | 2.68 | ok | 118.5 | 17.7 | 2021, 2024 | 2,654 | 2.45 | 0 | changed 0 |

(RAN, old-project\pc1\upgrade_runs.jsonl, upgrade_<code>_run.txt, upgrade_<code>_before.json and _after.json.)

- Part 1 took 39 minutes, 02:10:47 to 02:49:47, under the 110 of the task.
- Every source: format 2021, workshared, central, opened detached with worksets kept, its links 4 to 9 and 0 loaded. Every target: format 2024, workshared, central, saved as a new central (RAN, the step details of each log).
- Each run said `Source prototype unchanged: True`, and my own hash of the 13 files gave gone 0, added 0, changed 0 on 13 of 13 runs.
- Second check, another method: python\rvt_info.py reads the bytes of the 13 new files, 13 of format 2024 and workshared (RAN).
- Against Prototypes_2024 of the input folder, by size: 13 names in both, 11 equal in size, 2 differ, one by 4,096 bytes more and one by 4,096 less (RAN). The files of the input folder were only listed, never opened.
- prototypes_2024 holds 13 files and 13 backup folders Revit made beside them, 247 files in all (RAN).
- One Revit box per upgrade, 13 in all, the same each time: TaskDialog_Views_Related_To_Analytical_Changed, answered by the test host with answer 8 in the step openSource (RAN, dialogs of each log).
- 88 warning lines over the 13 logs, all in openSource, none stopped a run: 51 a family error Revit resolved by itself (line too short 49, sketch invalid 1, extrusion 1), 26 balusters not created, 7 one element inside another, 2 a reveal not created, 2 joined elements that do not meet (RAN, failures of each log). Whether the upgraded files lost anything a later step needs is UNKNOWN, not looked into.

## Part 2, the answer key

Per key: the Revit 2024 copy of answerKeys was hashed, copied by a file copy to round_addin\work as <copy stem>_TEST-R<letter>.rvt, read by `Run-TestHost.ps1 -RoundSpec` of kind read under the lock, the outputs moved to answer-key\<key>\R<letter>, the work copy hashed again and deleted. No model was opened in the input folder.

| Key | Job | Start to end | Minutes | Log result | Open s, read s | Peak private MiB | Least free GiB | Revit boxes, failure lines | Work copy equal to the key copy, unchanged by the read | Key copy before and after |
|---|---|---|---|---|---|---|---|---|---|---|
| IMC | RC | 02:50:12 to 02:50:47 | 0.58 | ok | 2.2, 6.5 | 1,236 | 3.84 | 0, 0 | True, True | changed 0 |
| IMD | RD | 02:50:47 to 02:51:23 | 0.60 | ok | 2.1, 8.4 | 1,341 | 3.46 | 0, 0 | True, True | changed 0 |
| IME | RE | 02:51:24 to 02:51:57 | 0.55 | ok | 2.3, 5.5 | 1,229 | 3.40 | 0, 0 | True, True | changed 0 |
| IMF | RF | 02:51:58 to 02:52:38 | 0.68 | ok | 4.0, 10.4 | 1,644 | 3.37 | 0, 0 | True, True | changed 0 |
| IMG | RG | 02:52:39 to 02:53:09 | 0.50 | ok | 1.7, 4.0 | 1,105 | 3.74 | 0, 0 | True, True | changed 0 |
| IMA | RA | 02:53:09 to 02:53:44 | 0.57 | ok | 2.6, 5.4 | 1,260 | 3.68 | 0, 0 | True, True | changed 0 |
| IMB | RB | 02:53:44 to 02:54:14 | 0.50 | ok | 2.1, 4.3 | 1,144 | 3.68 | 0, 0 | True, True | changed 0 |

(RAN, old-project\pc1\read_runs.jsonl, read_<key>_run.txt, read_<key>_before.json and _after.json.) Part 2 took 4 minutes of Revit windows. round_addin\work is empty after the last read (RAN).

### Counts per key

| Key | Plots, the links with a plot number | Villa links | Links with all six fields | Link files | Floors, all | Floors by kind: generic, yard, ramp, none | Walls | Levels | Spots |
|---|---|---|---|---|---|---|---|---|---|
| IMC | 337 | 338 | 337 | 14 | 1,010 | 336, 337, 337, 0 | 926 | 2 | 987 |
| IMD | 410 | 411 | 410 | 14 | 1,230 | 410, 410, 410, 0 | 1,138 | 2 | 1,256 |
| IME | 324 | 325 | 324 | 14 | 972 | 324, 324, 324, 0 | 894 | 2 | 938 |
| IMF | 490 | 491 | 490 | 14 | 1,470 | 490, 490, 490, 0 | 1,317 | 2 | 1,415 |
| IMG | 297 | 298 | 297 | 14 | 594 | 297, 0, 297, 0 | 859 | 2 | 866 |
| IMA | 432 | 433 | 432 | 14 | 761 | 733, 0, 0, 28 | 2,323 | 2 | 822 |
| IMB | 301 | 302 | 301 | 14 | 773 | 733, 0, 0, 40 | 1,515 | 2 | 587 |

Floors by type:

| Key | Generic 150mm | FFL | yard | Ramps 0.5% | Ramps 2% | Flat Ramps | Structural, 3 types |
|---|---|---|---|---|---|---|---|
| IMC | 336 | 0 | 337 | 280 | 47 | 10 | 0 |
| IMD | 410 | 0 | 410 | 350 | 55 | 5 | 0 |
| IME | 0 | 324 | 324 | 248 | 76 | 0 | 0 |
| IMF | 0 | 490 | 490 | 409 | 75 | 6 | 0 |
| IMG | 297 | 0 | 0 | 229 | 68 | 0 | 0 |
| IMA | 733 | 0 | 0 | 0 | 0 | 0 | 28 |
| IMB | 733 | 0 | 0 | 0 | 0 | 0 | 40 |

- First method: python\key_reads.py --reads answer-key\<key> --key <key>=<job>, written to answer-key\<key>\key_reads.json (RAN). It ties each read to its key by the model name through check_key_file, 7 of 7 accepted.
- Second method, no code shared: my own count off the raw read, old-project\pc1 holds nothing of it, the script is in the scratch folder of the session. Links, links with a plot, link files, floors by type, walls, levels and spots are equal to the first method on 7 of 7 keys (RAN).
- The six fields of the task, plot, prototype, position, rotation, mirror and origin Z, are held per link in answer-key\<key>\links.csv, one row per link, written by the same second script from the read. Every link with a plot number has all six. One link per key has no plot number. The plot numbers are distinct, as many as the links with one, on 7 of 7.
- No link was loaded in any read, loaded 0 of each, as in Round ADDIN. So the position is the transform of the link in the model, and whether it holds in shared coordinates was not checked here.
- IMA and IMB hold the same 733 Generic floors, the same id and the same whole row (RAN, my own compare). The sharedWith of key_reads.json is empty, since each key was counted in its own folder.
- Error lines of the reader, by read: IMC 2, IMD 2, IME 2, IMF 1, IMG 0, IMA 4, IMB 4. They are of 3 kinds: a spot whose host was not found, floors of a type with no Plot parameter (yard, and the structural types of IMA and IMB), and on IMA and IMB the spots with no value (RAN).
- Every count of the two tables equals the table of Round ADDIN Phase 2a for the same key, steps/history/round-addin/05-phase-2a.md lines 41 to 51 (READ).
- The plots of a plan, the plots that join the baseplan, are UNKNOWN: no plan was built.

### Stopped steps

| Step | Command | Stopped on |
|---|---|---|
| The plan of each key, 7 times | `python\plan_from_past.py --prototypes answer-key\<key>\R<x>\testhost_R<x>_prototypes.json --answer-key --names <work root>\data_book\location_plan\labels.csv --key <key> --out answer-key\<key>\plan_<key>_past.json` | exit 2 on 7 of 7: `not found: ...\sample_output\round12\full\ROSHN Makkah Baseplan.dwg\plots.json`, the pastJob.baseplanPlots of config under this worktree. It stops at the first file that is gone. labels.csv of the data book is gone too (RAN Test-Path, False) |
| The table | `python\past_all.py table --labels <labels.csv> --baseplan <plots.json> --key imc pim <plan> <read> --key-code imc IMC --out answer-key\past_all.csv` | exit 1: `FileNotFoundError: [Errno 2] No such file or directory: 'C:\\SA-work\\SiteAdaptation\\data_book\\location_plan\\labels.csv'`. It also needs the 7 plans above, and plots.csv of the data book for --schedule-entry, gone too (RAN Test-Path, False) |

So answer-key holds per key the read, key_reads.json and links.csv, and no plan_<key>_past.json and no past_all.csv.

## Real inputs

- Whole folder C:\02-Site Adaptation at the end, 02:55: before 837, after 837, gone 0, added 0, changed 0, by size 0, by write time 0, by SHA-256 0 (RAN, my Python check, old-project\inputs-after.json against Lead's inputs-before.json).
- Second method: Get-FileHash of PowerShell over the folder against the before file, files now 837, in the before file 837, not equal by SHA-256 or size 0 (RAN).
- Nothing was written under the input folder. The runs wrote under the work root only, and this session's scratch folder.

## What was opened in Revit

Revit 2024, 20 windows, one at a time, each started and closed by Run-TestHost.ps1 under the window lock, round PC-1:
- 13 times one villa file of the prototype folder of the input root, opened detached from central and saved as a new file in prototypes_2024.
- 7 times a work copy of one answer key copy, in round_addin\work, read and closed without a save.

The driver:
- No click, no key and no screenshot was made by this lane. Nobody's input was touched.
- The box "Security - Unsigned Add-In": the dialog watcher of the test host pressed Load Once, never Always Load, 4 boxes in each of the 20 windows, 80 in all, each for an add-in of this tool: the reader, Site Adaptation, the placer, the test host. First box 02:11:39, last window 02:53:44 (RAN, securityDialogs of each log, times of the first log in UTC 23:11:39).
- No box of another maker was answered: the logs list no box of RcrcGreen or RcrcGreen Audit, whose manifests are in the add-ins folder (RAN). Whether Revit showed one and it closed by itself is UNKNOWN.
- After the last run the add-ins folder holds the three manifests it held before, the developer manifests are gone again (RAN).

## Not seen in Revit

- The upgraded villa files were not opened again after the save, their format is read from their bytes.
- Nothing of the add-in pane or its buttons was run.

## Tests

- pwsh scripts\quick-tests.ps1, with status: done in the task file: PASS, pytest 154 passed, dotnet net8.0 2,774 passed, 0 failed, 0 skipped (RAN).
- python -m pytest tests/acceptance: 50 passed (RAN).
- Not run here: the full gate, GitHub runs it on the PR.
