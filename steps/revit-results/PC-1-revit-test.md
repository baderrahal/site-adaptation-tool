# PC-1-revit-test, 12 Nov 2026

Lane revit-test on Bader's PC, branch task/PC-1-revit-test from origin/main 56d9dd69. In work, this file is filled as the runs end.

## Setup

- Input folder against old-project\inputs-before.json at 02:08: before 837, after 837, gone 0, added 0, changed 0.
- Build Release: first try 1 error RG1000, second try 0 errors.
- Install-TestHost.ps1 -NoBuild: first try refused, the Common DLL of the installed add-in was of another build. After Install-SiteAdaptation.ps1 -NoBuild from this build: the same in every folder, True.
- python\key_copies.py check: reuse 7 of 7.

## Part 1, in work

- C10: log result ok, 3.95 minutes.
