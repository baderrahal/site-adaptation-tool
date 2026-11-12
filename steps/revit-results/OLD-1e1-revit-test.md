# OLD-1e1-revit-test, a first run of step e, 12 Nov 2026

Lane revit-test on Bader's PC, task OLD-1e1-revit-test, branch task/OLD-1e1-revit-test from origin/main 5531c677. RAN means run in this session, with the file that holds the output. A file named without a drive lies under the work root C:\SA-work\SiteAdaptation, the files of this task in old-1e1. Counts, times and the words of the tool only, no coordinate, no level, no plot number. No code was changed and no JSON was edited by hand. Times are local.

This record is written part by part and pushed after each part. A part not yet run says so.

## The plan

1. Build Release at 5531c677, install the add-in and the test host.
2. Hash the input folder before.
3. A copy of old-1b\OLD1-card1.rvt, opened in one Revit under python\window_lock.py.
4. Card 2 Build on 01A, the driver of this morning answers the windows. A stop: its words and the code line, and the Builds end there when the cause is not of a neighbourhood.
5. If 01A builds: save, close, the read of the built model by the test host, links.csv and key_reads.json into built\01A, python\compare_built.py.
6. The next neighbourhoods in the order of the task while the session lasts.
7. Card 3 Report on the last model, the input hash again, the second check.

## Setup

| Step | Result | Proof |
|---|---|---|
| Work root | C:\SA-work\SiteAdaptation | RAN Get-Content of %APPDATA%\SiteAdaptation\machine.json this morning, not changed |
| dotnet build dotnet\SiteAdaptation.sln -c Release, in this worktree at 5531c677 | 0 errors, 40 warnings, 5 min 28 s | RAN, 16:11 to 16:17. The build of this morning took 1 min 41 s, build-1 and build-2 built and tested at the same time |
| Install-SiteAdaptation.ps1 -NoBuild | one manifest of the tool in the Revit 2024 add-ins folder, "one SiteAdaptation.Common.dll in 5 folders of the tool" | RAN |
| Install-TestHost.ps1 -NoBuild -SkipSigningReport | "SiteAdaptation.Common.dll the same in every folder of the tool and the build: True" | RAN |
| scripts\checks\write_places.py --save | "PASS write places: listed 79 names before the tests" | RAN |
| The input folder before, 16:11 to 16:15 | 837 files, 8,874,086,637 bytes. Against old-1b\inputs-after.json of 12:32: "before 837, after 837, gone 0, added 0, changed 0 (by size 0, by write time 0, by SHA-256 0)" | RAN old-1e1\scripts\hash_inputs.py, old-1e1\inputs-before.json |
| Revit before the first window | no Revit process | RAN Get-Process |

The load of the PC, for Lead: at 16:13 the PC ran 89 dotnet processes of the other lanes, 1.03 GB free. `python\window_lock.py --status` did not end in 5 minutes, its tasklist call hung, and it ended with "Command '['tasklist', '/FO', 'CSV', '/NH']' returned non-zero exit status 4294967295" after this lane stopped that one tasklist, its own child. A tasklist alone took 84.6 s at 16:23. At 16:24: 28 dotnet processes, 3.05 GB free.

A fault of this lane: `dotnet build-server shutdown` at 16:24, to give back the memory of its own build as this morning. It ends the build servers of the user, so also those of build-1 and build-2. "VB/C# compiler server shut down successfully. MSBuild server shut down successfully." Whether a build of another lane failed by it is UNKNOWN.

## The driver

The driver of this morning, copied to old-1e1\scripts with its log in old-1e1\logs\driver.log. UI Automation patterns and window messages to one control of the Revit it started, no mouse click, no key, no screenshot. So nobody's input is touched.

A person was at this PC during the run: at 16:26 the window in front was a Navisworks window, idle time 111 s, at 16:31 idle 11 s with Revit in front, at 16:35 Navisworks in front again. The driver went on, since it sends no input. Not seen: whether a person pressed anything in a window of Revit.

## The model

The work folder of a model is named by the hash of its path (dotnet\SiteAdaptation.AddIn\Pane\WorkFolder.cs lines 28 to 53). So the copy of card 1 took the path of this morning: old-1b\OLD1-site.rvt of 12:31 was renamed OLD1-site-baseline-end.rvt, and old-1b\OLD1-card1.rvt was copied to old-1b\OLD1-site.rvt, SHA-256 equal (RAN). Its work folder is OLD1-site_347b04be, with the reads of this morning in it.

## Part 1, 01A, from 16:25

One Revit 2024.3 under python\window_lock.py, round OLD-1e1, process 43692, started 16:25:10 on old-1b\OLD1-site.rvt.

1. 16:27:00 the box "Security - Unsigned Add-In", Name "Site Adaptation", Publisher "Unknown Publisher", the DLL of the installed add-in. The driver pressed Always Load, Bader's order of 6 Nov 23:37. No other box came at the start. The model was open at 16:27:23.
2. The tool took the model and its work folder: the pane named the project OLD1-site, card 1 Ready with the 11 rows of this morning's read, Rules "OLD1-site_347b04be", Road surface "landxml" with "Road surfaces in order (6)", step 1 Done. Card 1 was not run again.
3. Three things set before the save of this morning were not in the saved model: the row Prototypes said "Not chosen" and "3 of 8 found", Scope said Whole project, Villa mode said Masses. This lane set them again as this morning: Browse of the row Prototypes to Prototypes_2024 of the input folder, then "Prototypes_2024" and "4 of 8 found", Neighbourhood 01A, Links. Card 2: "Scope: the neighbourhood 01A. The plan and its preview show every plot of stage 1." The Scope list holds 10 names now, the 9 of this morning and "-".

### The first press of Build, 16:37:52

| Time | What came | Answer of the driver |
|---|---|---|
| 16:38:17 | Preview of the build: "2615 plots with an outline, 2619 issues. Nothing is written to the model yet." | Go on |
| 16:38:28 | the box 2 Read data: "Dry run done, nothing was changed. ... Reads again, the last read of these files is not whole: files or folders not read 8, the first is Master Plan/221229_MP-SK-01-03-001_R06.dwg, free memory 3.10 GiB, the 221229_MP-SK-01-03-001_R06.dwg read needs 9.72 GiB." | Apply |
| 16:38:31 to 16:40:36 | the read of card 1 again in the worker, a window "Site Adaptation", "2 Read data", "Running outside Revit. The pane shows the step when it ends." with a clock. Worker 124.1 s, status done, the step 136.2 s by its run_timing. Free memory 0.51 GB by the driver at 16:38:44 | none, the window closes by itself |
| 16:41:06 | 3 Review data, NBH 01A, 278 plots, "0 of 278 checked by the team" | Save review, Close. 35.4 s by its run_timing |
| 16:41:14 | the read for the build, the worker refused in 0.2 s | none |

No rules box came, the rules of the project were chosen this morning. No What the tool read and no Confirm window came.

The exact words of card 2: "Build stopped at Read data: the worker ended refused, free memory 0.72 GiB, the Makkah Phase 1_Road Layout.dwg read needs 2.55 GiB"

### The second press of Build, 16:44:59

CHOSEN by this lane: one more press, with no change, once the PC showed 2.9 GB free, to see whether the stop is of that minute. 16:45:15 the preview, the same line, Go on. No read box and no review came. The worker refused in 0.15 s at 16:45:16.

The exact words of card 2: "Build stopped at Read data: the worker ended refused, free memory 2.64 GiB, the whole read needs 3.81 GiB (Q083)"

### What the two presses show

- The data book stop of this morning is gone. The Build wrote plots_read_4b1a4a1eef460af2 in the work folder with plots.csv and labels.csv, 2,615 rows each, and its request names them as plotsCsv and labels beside baseplan, cadLayers, curbs, dlus, sales, villaBlocks, config and the 6 road surfaces (RAN, request_2_20261112-164114-877-build.json). So R840 is seen in Revit as far as the request.
- The two notes of this morning, road surface and prototypes, are not in the stop words. R843 is seen for rows a person set.
- The next stop is memory, and it is not of a neighbourhood. The read for the build is refused before it reads any file, by two checks of the worker in turn: each large input against its measured peak, then the whole read against its peak of config.
- Code lines. The words "Build stopped at <step>: <reason>": dotnet\SiteAdaptation.AddIn\Pane\SiteBuildText.cs line 21, filled at BuildChain.cs line 109. "the worker ended refused, ": dotnet\SiteAdaptation.Common\Flow\RunTexts.cs line 171, called by dotnet\SiteAdaptation.AddIn\Fast5\BuildRead.cs line 126. "free memory 0.72 GiB, the ... read needs 2.55 GiB": dotnet\SiteAdaptation.Common\Worker\WorkerMemory.cs lines 20 and 29, called by WorkerRunner.cs lines 202 and 275. "free memory 2.64 GiB, the whole read needs 3.81 GiB (Q083)": WorkerMemory.cs lines 41 and 50, called by WorkerRunner.cs line 286.
- What the PC held then: 15.78 GB in all. At 16:43:53, 1.49 GB free, the working sets: dotnet 77 processes 4.5 GB, VS Code 2.1 GB, claude 1.5 GB, the test hosts of the other lanes 1.3 GB, Revit 0.5 GB, Navisworks 0.2 GB. The most free memory this lane saw today is 3.23 GB at 12:01 and 3.05 GB at 16:24, both under 3.81.

| Neighbourhood | Plots read | Build pressed | Stopped | Plots built | Floors, by type | Levels | Links | Walls | Issues of the preview |
|---|---|---|---|---|---|---|---|---|---|
| 01A | 278 | 16:37:52 | at Read data, 16:41:14, 3.4 min after the press | 0 | 0 | 0 | 0 | 0 | 2619 |
| 01A, second press | 278 | 16:44:59 | at Read data, 16:45:16, 17 s after the press | 0 | 0 | 0 | 0 | 0 | 2619 |
