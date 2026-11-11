---
id: PC-1-revit-test
lane: revit-test
stage: 2
status: open
allowed:
  - steps/revit-results/
  - steps/heads-up/revit-test.md
---

# Scope

Bader's order of 12 Nov. The work machine is Bader's own PC, steps/pc-setup.md. The work root is C:\SA-work\SiteAdaptation by the machine file. This task makes two things under the work root that every later compare needs, and changes no code.

Part 1. The 13 villa files as Revit 2024 files. Source: prototypeFolder of pastJob in config/project.json under inputRoot, 13 Revit 2021 files, workshared. Target: prototypes_2024 under the work root. Use Run-TestHost.ps1 -Upgrade, -NewUpgradeFolder on the first, one code per run in the order of testHost.order. One Revit at a time. The originals are never opened for writing, the run script copies by file copy. The input folder already holds Prototypes_2024 with 13 files of format 2024 by their bytes. Do not use them as the result, but hold each new file against the one of the same name by size and say in the record how many are equal in size, counts only.

Part 2. The answer key. For each of the 7 keys of answerKeys in config/project.json, read its Revit 2024 copy of Revit All Zones\2024 with the existing read job of the test host (R204 to R214, R245, R312, R321, R325, python/key_copies.py check gives reuse 7 of 7 on this PC). The model is opened as a copy under the work root, never in the input folder. Then build the plan of each key with python/plan_from_past.py --key and the table with python/past_all.py, as Round ADDIN did. Write everything under answer-key in the work root, one folder per key code. Per key the record gives counts only: plots, villa links (with plot, prototype, position, rotation, mirror and origin Z held per link in the work root file), floors by type, walls, levels. If a step needs a file of the old work root that is gone on this PC (the data book, plots.csv, the location plan), stop that step, write exactly which file and which command asked for it, and carry on with the next key or the next count that does not need it.

Before and after every Revit run: size, write time and SHA-256 of each real input the run reads, the run fails on any change. Lead wrote the whole input folder before this task as old-project\inputs-before.json under the work root, 837 files. At the end of the task hash the folder again and give the count of files gone, added and changed.

Setup before the first run: build Release in this worktree, run dotnet\SiteAdaptation.TestHost\Install-TestHost.ps1, the install of 12 Nov by Lead took the three developer manifests away. Use the window lock through python/window_lock.py. Keep each part under 110 minutes. A file that fails or times out is recorded with its exact error and the run carries on.

The driver: a person may be at this PC. A click, a key or a shot only with Revit in front, after 5 minutes idle, no meeting window, and stop on other input. The box "Security - Unsigned Add-In" for Site Adaptation may be pressed Always Load once, with the time in the record. Every other box of another maker stays untouched.

The record is steps/revit-results/PC-1-revit-test.md: per run the code, the time it took, the result read from the result file and not the exit code. Counts, row numbers and distances only, no coordinate, no level, no plot data, the repo is public. Take no rule id and no question id. A fault goes into the record as a line for Lead.

# Done when

- prototypes_2024 under the work root holds 13 files of format 2024 by rvt_info.py, or the record names each that failed with its error.
- answer-key under the work root holds one folder per key that could be read, and the record holds the table of counts per key.
- The input folder is unchanged by SHA-256, 837 files.
- The PR body starts with `Task: PC-1-revit-test` and says what was opened in Revit.
