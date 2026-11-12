# OLD-1-revit-test, the baseline run, 12 Nov 2026

Lane revit-test on Bader's PC, task OLD-1b-revit-test, branch task/OLD-1b-revit-test from origin/main a6f91989. RAN means run in this session, with the file that holds the output. A file named without a drive lies under the work root C:\SA-work\SiteAdaptation, the files of this task in old-1b. Counts, names, times and the words of the tool only, no coordinate, no level, no plot data. No code was changed and no JSON was edited by hand. Times are local.

This record is written part by part and pushed after each part. A part not yet run says so.

## Setup

| Step | Result | Proof |
|---|---|---|
| Work root | C:\SA-work\SiteAdaptation | RAN Get-Content of %APPDATA%\SiteAdaptation\machine.json |
| dotnet build dotnet\SiteAdaptation.sln -c Release, in this worktree at a6f91989 | 0 errors, 40 warnings, 1 min 41 s | RAN, 11:41 |
| Install-SiteAdaptation.ps1 -NoBuild | one manifest of the tool in the Revit 2024 add-ins folder, SiteAdaptationAddIn_Revit2024.addin | RAN |
| Install-TestHost.ps1 -NoBuild -SkipSigningReport | "SiteAdaptation.Common.dll the same in every folder of the tool and the build: True" | RAN |
| scripts\checks\write_places.py --save | "PASS write places: listed 79 names before the tests" | RAN, 11:42 |
| The input folder before, 11:42 to 11:44 | 837 files, 8,874,086,637 bytes, name, size, write time and SHA-256. Against Lead's list of 09:29: gone 0, added 0, changed 0 | RAN old-1b\scripts\hash_inputs.py, old-1b\inputs-before.json against old-project\inputs-round0-after.json |
| Revit and the lock before the first window | no Revit, no lock, slot 1, 1.24 GB free | RAN python\window_lock.py --status |

## The driver

The driver of the laptop, drive3.ps1, is not on this PC. This lane wrote a new one under the work root, old-1b\scripts\uia.ps1, with two helpers for the file and folder boxes. It is no code of the repo.

- It sends no mouse click, no key and takes no screenshot. It reads the windows of the Revit it started by UI Automation and presses a button by its Invoke, Toggle or Select pattern. On a Win32 box of Revit, which offers no such pattern, it posts BM_CLICK to the button and WM_SETTEXT to the text field, a window message to that one control.
- So nobody's input is touched and nothing is sent to another window. Idle time of the PC at the first press, 11:47: 3,633 s.
- Every press is in old-1b\logs\driver.log with its time. The texts of the windows, which hold real values, are in old-1b\logs and stay out of git.
- Not seen: a person using the windows, every press came from the driver. No shot was taken, so no picture of a window exists for this run.

## Part 0, the empty model, 11:46 to 11:52

One Revit 2024.3 under python\window_lock.py, round OLD-1b, process 32872, started with no model at 11:46:46.

1. 11:47:18 the box "Security - Unsigned Add-In" came, Name "Site Adaptation", Publisher "Unknown Publisher", the DLL of the installed add-in. The driver pressed Always Load at 11:48:18, Bader's order of 6 Nov 23:37. No other box came at the start.
2. File, New: the box New Project offered the template "Imperial Multi-discipline". Browse, C:\ProgramData\Autodesk\RVT 2024\Templates\English\DefaultMetric.rte, the template of siteBuild.template of config. Create new Project, OK. The first try of the Browse took the imperial template of the folder the box stood in, it was not created, the second try showed DefaultMetric.rte before OK.
3. Save on the quick access bar, Save As `old-1b\OLD1-site.rvt`, written 11:52:26, 5,677,056 bytes. `old-1b\OLD1-empty-pristine.rvt` is a file copy of it, SHA-256 equal.

Proof it is an empty model: the pane named the project OLD1-site with "Set up this project first, in Settings." on the 3 cards, and Set up site opened with "Empty project found" and "This model has no site coordinates." (old-1b\logs\w_setup_1.txt).

## Part 1, card 1 Project data, 11:53 to 12:10

The same Revit as part 0, on `old-1b\OLD1-site.rvt`. Card 1 ran once.

### The road surfaces, measured first

1. 11:53 Settings, Project setup, Edit: the window New project, Start from "New project rules, Rev 06", "1 of 4 files ready". The row Road levels (civil file) said "Not chosen" and "No road surface is picked."
2. Pick surfaces before any folder: the window Road surfaces said "The road surface folder holds no road surface files. Pick the folder in the row Road levels, then open this window again." Cancel. So the tool does not find landxml of the work root by itself.
3. Browse of the row Road levels, the Select Folder box, landxml of the work root: the row said "landxml", "Found". So a person reaches it in a window, and this part went on.
4. Pick surfaces again: 6 surfaces offered, each unticked: Compiled West_MM, Missed from Paste, Part 4 Top Surfaces, Top Surface Part 2 East Of East, Top Surface-01, Top Surface-02. The 7th file of landxml, EXISTING_11.5 Sqkm, was not offered.
5. CHOSEN by this lane: all 6 ticked, in the order of siteBuild.roadSurfaces of config. Use these picks. The row: "Road surfaces in order (6): Top Surface-02, Top Surface-01, Part 4 Top Surfaces, Compiled West_MM, Top Surface Part 2 East Of East, Missed from Paste". Save setup 11:55:24.
6. The data check of card 1 still says, after both reads: Road levels, Missing, "No Land XML surface file is in the folder, so there are no road levels." It looks in the project folder only, the picked folder is no input of it. A line for Lead: the row of card 1 and the row of Settings disagree. Whether the Build takes the road levels of the pick is under card 2 below.

### The folder, Set up site and the reads

7. 11:55:58 Choose project folder, the Select Folder box, C:\02-Site Adaptation. The window 1 Set up site opened by itself with the Baseplan "ROSHN Makkah Baseplan.dwg" of the folder, "Empty project found", "This model has no site coordinates.", "The place Boston, MA comes from the Revit template, it is not the site.", position from "CAD, the plot outlines of the baseplan", "5 of 5 check points pass", the coordinate system line "no input states a system, EPSG 32637 of config". The three Current ticked (Site drawing, Road surface, Parameter sheet), Dry run, Apply 11:57:06, the window gone by 11:57:21. Step 1 by its run_timing: Committed, 54.0 s.
8. 11:57:49 Choose project folder again, the same folder. The box: "2 Read data, Dry run done, nothing was changed. Reads the folder and the settings file. Files in the folder: 376, 5963.6 MiB, those of a kind it knows are read. The old read lacks a file: the project names no neighbourhood schedule (R798), and 3 more. The folder is read instead." Apply 11:58:27.
9. Read 1 in the worker, 11:58:28 to 11:59:29, 61 s. What the tool read: "10 files read", "2,618 of 2,964 villa plots of all plots". Under Files not read, 10 drawings, each with a line of this shape: "free memory 1.53 GiB, the CEC Building Footprint.dwg read needs 1.96 GiB". The 10: Base Plans/CEC Building Footprint.dwg and the 9 of step 13.
10. Close, then Confirm what was found: Villa type column "Typology_Rev8.1 in JN Sales Data_Base_Rev09 of PSN-ZZZ-XXX-SCH-TM-000201- DLUS Schedule_(Rev9)_Accomodation_Schedule.xlsx, 2570 plots (proposed)", note "2 columns tie and hold the same code on every plot, the first is proposed". Area column "Neighbourhood_ID in PH_1-DLUS of" the same workbook, "2955 plots (proposed)". Code map, 16 pairs, "Proposed from" the Reference sheet of that workbook. Hand of each prototype, 13 rows C10 to VL6, each "no guess". Group levels, 2 fields, empty. Both proposed columns kept, no hand picked, no word typed. Confirm 12:00:28, the box at 12:00:45, 17 s: "2 Read data done: 5 files written, 0 issues. The old read lacks a file: the project names no neighbourhood schedule (R798), and 3 more. The folder is read instead. This read is not whole, the next press reads again: files or folders not read 10, the first is Base Plans/CEC Building Footprint.dwg, free memory 1.53 GiB, the CEC Building Footprint.dwg read needs 1.96 GiB. Your values are saved. Press Read data again to read with them. They are applied to this read already, so that press reads no drawing again."
11. `dotnet build-server shutdown` by this lane, to give back the memory of its own build. Free memory 3.23 GB before the next press.
12. 12:01:39 Choose project folder a third time. The box: "Reads the folder, the settings file and the values a person confirmed. Files in the folder: 376, 5963.6 MiB ... Reads again, the last read of these files is not whole: files or folders not read 10 ...". Apply 12:02:03.
13. Read 2 in the worker, to 12:03:32, about 90 s. What the tool read: "12 files read", "2,615 of 2,964 villa plots of all plots". The footprint drawing was read this time. Files not read, 9, each for free memory, the free memory 1.86 to 2.19 GiB at its turn:

| File not read | The read needs, GiB |
|---|---|
| Master Plan/221229_MP-SK-01-03-001_R06.dwg | 9.72 |
| Master Plan/221229_MP-SK-01-03-001_R06_0000.dwg | 10.26 |
| Master Plan/221229_MP-SK-01-03-001_R06_0001.dwg | 10.26 |
| Master Plan/25.06.2025 Phase 1 Amana_V9.dwg | 6.97 |
| Roads/Makkah Phase 1_Road Layout.dwg | 19.26 |
| Roads/Makkah Phase 1_Road Surface C3D.dwg | 44.39 |
| Roads/Makkah Phase 1_Rough Grading.dwg | 53.15 |
| Roads/Makkah Phase 1_Typical Cross Sections.dwg | 2.73 |
| Roshn Makkah - Master Plan Rev.09/MP-SK-01-03-006_R9.0.dwg | 5.26 |

14. Close, Confirm what was found again: the same values, the code map "Confirmed on an earlier read.", level 1 word "Neighbourhood_ID", "the header of the area column". Cancel 12:04. The box: "2 Read data done: 5 files written, 0 issues. ... This read is not whole, the next press reads again: files or folders not read 9, the first is Master Plan/221229_MP-SK-01-03-001_R06.dwg, free memory 1.97 GiB, the 221229_MP-SK-01-03-001_R06.dwg read needs 9.72 GiB. Nothing was saved. The read stays as it is."

A line for Lead: on this PC, 16 GB, the read can never be whole. 7 of the 9 drawings need more memory than the PC has, the road layout among them, so every press of Choose project folder reads the folder again, about 90 s. Which values of the build these 9 drawings would have given is UNKNOWN.

### What the tool read, card 1 after read 2

| Need | Mark | The line of the card |
|---|---|---|
| Plot outlines | Strong | 2992 outlines found on layer MP_PLOT_LINE + MP_PLOT_SUBD, 2964 with a number of their own, 28 without one, 33 numbers outside every outline. 349 plots with n... (the card cuts the line) |
| Plot numbers | Strong | Plot numbers are text on layer MP_PLOT_TEXT, 2964 outlines hold one and 2964 numbers are distinct. |
| Group levels | Usable | Areas from the confirmed column: 13. Smaller groups: 138, words Neighbourhood_ID. |
| Villa types | Weak | Villa type is read from the confirmed column for 2570 of 2964 plots, the drawing gives another code on 2172, 32 with no prototype file. Code map confirmed. |
| Villa blocks | Usable | Villa blocks are inserts on 6 layers, 2079 of 2964 plots hold one. |
| Road edges | Weak | Road edges are lines on layer MP_WADI_CHAN, within 20 m of 279 of 2964 plots, not scored. |
| Road levels | Missing | No Land XML surface file is in the folder, so there are no road levels. |
| Coordinates | Weak | The drawing is in meters and names no coordinate system, the plots sit in drawing space. |
| Prototypes | Usable | 78 prototype files found, 26 with a villa type code in the file name. The files are opened at Read data. |
| Rule sets | Missing | No rule set file is in the folder, the rules are chosen in the add-in. |
| Shared parameters | Strong | 10 parameters for the villa links in shared_parameters.xlsx, each with a GUID. |

The villa block line is the same after read 1, which did not read the footprint drawing, and after read 2, which did: 2079 of 2964.

By a script on the read on disk, counts only (old-1b\scripts\read_counts.py on read_4b1a4a1eef460af2 of the work folder of the model): 2,615 villa plots and 349 other plots, 2,964 in all. 21 files listed, 12 read (7 drawings, one of them as notes, 3 workbooks, 2 PDF), 9 not read. "Left out as past outputs by the settings: 3 folders by dataCheck.skipFolders and 8 files by dataCheck.skipFiles." So the skip settings of PR 299 work in Revit.

### The neighbourhoods

The Scope list of Advanced names 9: 010, 012, 013, 01A, 01B, 02, 021, 022, 03. The card says "Areas from the confirmed column: 13". The list of 8 Nov on the laptop also held 023, it is not in this list.

| Neighbourhood | Villa plots read | With a villa type | With a mirror value |
|---|---|---|---|
| 010 | 16 | 0 | 11 |
| 012 | 342 | 337 | 282 |
| 013 | 416 | 410 | 331 |
| 01A | 278 | 276 | 210 |
| 01B | 2 | 0 | 0 |
| 02 | 325 | 324 | 253 |
| 021 | 435 | 432 | 362 |
| 022 | 305 | 301 | 247 |
| 03 | 493 | 490 | 383 |
| area "-" | 2 | 0 | 0 |
| no area | 1 | 0 | 0 |
| Total | 2,615 | 2,570 | 2,079 |

Villa types of the read: 15 codes on 2,570 plots, 45 plots with none. 010 and 01B hold no villa type on any plot.

### Fix items

12:04:15 Fix items opened the window Confirm what was found. Its rows, every one:

| Row | Value shown |
|---|---|
| Villa type column | Typology_Rev8.1 in JN Sales Data_Base_Rev09, 2570 plots (proposed), 19 other choices and none |
| Area column | Neighbourhood_ID in PH_1-DLUS, 2955 plots (proposed), 17 other choices and none |
| Code map | 16 pairs, "Confirmed on an earlier read." |
| Hand of each prototype | 13 rows, C10, DP1, DP2, DP3, DP4, DT1, DT3, TH1, TH3, VD2, VD4, VL4, VL6, each "no guess" and no hand picked |
| Group levels, level 1 | Neighbourhood_ID, "the header of the area column" |
| Group levels, level 2 | empty |

Cancel. The box "Nothing was saved. The read stays as it is."

### The villa files

- The tool before the pick: card 1 "78 prototype files found, 26 with a villa type code in the file name.", and the row Prototypes of Advanced "Not chosen", "3 of 8 found".
- 12:05 Browse of that row, 00_Revit Models\Zone 02\ARC\Prototypes_2024 of the input folder. The row then: "Prototypes_2024", "4 of 8 found". No other line under the row.

### The end of card 1

Scope Neighbourhood 01A and Villa mode Links set in Advanced, the Level profile parsons2024Rev6 as it stood. Card 2: "Scope: the neighbourhood 01A. The plan and its preview show every plot of stage 1." Save on the quick access bar, 12:10:09, 5,693,440 bytes. `old-1b\OLD1-card1.rvt` is a file copy of that save.

A driver fault of this lane, not of the tool: at 12:04 the box "Nothing was saved." stayed open while the driver pressed Browse of the Prototypes row behind it. The folder was taken. The box then closed only by its frame at 12:07, not by its Close button. From then the driver closes every box before the next press.

### Times of card 1

| What | From | To | Time |
|---|---|---|---|
| Settings, the road pick, Save setup | 11:53 | 11:55:24 | 2 min |
| 1 Set up site, Apply | 11:57:06 | 11:57:21 | under 15 s, 54.0 s for the whole step by run_timing |
| Read 1, the worker | 11:58:28 | 11:59:29 | 61 s, 10 drawings not read |
| Confirm, in the worker | 12:00:28 | 12:00:45 | 17 s |
| Read 2, the worker | 12:02:03 | 12:03:32 | about 90 s, 9 drawings not read |
| Card 1 in all, to the save | 11:53 | 12:10 | 17 min, the pace of the driver in it |

## Part 1, card 2 Build site, 12:12 to 12:30

The same Revit, the same model. Preview first ticked, Dry run first as it stood. Nothing was built: every Build stopped at its own read, before 4 Check. One row per neighbourhood:

| Neighbourhood | Plots read | Build pressed | Stopped | Time, press to stop | Read 2 again | Review | Plots built | Floors, by type | Levels | Links | Walls | Issues of the preview |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 01A | 278 | 12:12:05 | at Read data, about 12:15:30 | 3.4 min | yes, 131.6 s | "3 Review data, NBH 01A, 278 plots", "0 of 278 checked by the team", Save review, Close, 13.2 s | 0 | 0 | 0 | 0 | 0 | 2619 |
| 010 | 16 | 12:17:53 | at Read data, by 12:18:42 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 012 | 342 | 12:19:31 | at Read data, by 12:20:54 | under 85 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 013 | 416 | 12:21:46 | at Read data, by 12:22:35 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 01B | 2 | 12:23:19 | at Read data, by 12:24:09 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 02 | 325 | 12:24:52 | at Read data, by 12:25:41 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 021 | 435 | 12:26:25 | at Read data, by 12:27:13 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 022 | 305 | 12:27:57 | at Read data, by 12:28:47 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |
| 03 | 493 | 12:29:31 | at Read data, by 12:30:20 | under 50 s | no | none | 0 | 0 | 0 | 0 | 0 | 2619 |

The exact words of card 2, the same on all 9: "Build stopped at Read data: labels of the data book is not on disk: C:\SA-work\SiteAdaptation\data_book\location_plan\labels.csv. plotsCsv of the data book is not on disk: C:\SA-work\SiteAdaptation\data_book\plots.csv. roadSurface: no folder holds a road surface named in config or picked. prototypes: two folders hold 13 each, C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes and C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes_2024"

What was seen on 01A, in order:

1. The box "No rules are chosen for this project. Use the rules template of site adaptation manual 01101-PSN-ZZZ-XXX-RPT-ZZ-001000 Rev 06? Its rules name the unit types of that one manual, so check they fit this project. They stay not confirmed until a person confirms them. Use the manual template for this project now?" Yes. It came once, not on the 8 later Builds.
2. Preview of the build: "2615 plots with an outline, 2619 issues. Nothing is written to the model yet." Go on. The same line on all 9, the preview counts the whole read, not the scope.
3. The box 2 Read data, "Reads again, the last read of these files is not whole: files or folders not read 9 ...". Apply. The read ran again, 131.6 s by run_timing, with no What the tool read and no Confirm window after it.
4. 3 Review data, Save review, Close.
5. Card 2: "Build plan: 2615 plots, 0 ready for every step, 2615 marked, 2619 issues. ... 2619 issues go to the issues list." then the stop words above.

The 8 later Builds: the preview, Go on, then no box, no worker and no window. Each press wrote a new build plan (answer_build of the work folder, status done, plots 2615, marked 2615, blocking 0, issues 2619) and nothing after it. The driver read the stop line on the card 30 to 60 s after Go on. That line is word for word the line of the Build before, so the card alone does not prove it was written again. What proves the stop: Revit stood with no window and no worker, and the work folder holds no file of step 4 to 8 for any neighbourhood. A line for Lead: after the stop of 01A the later Builds asked no read again though the read is not whole, why is UNKNOWN.

### Why it stops, for Lead

- The read for the build always asks for labels and plotsCsv of addin.readData.dataBookFiles in the folder data_book beside the work folder of the model (dotnet\SiteAdaptation.Common\ReadData\ReadInputs.cs lines 201 and 365 to 386, called by dotnet\SiteAdaptation.AddIn\Fast5\BuildRead.cs line 74). data_book is not on this PC (RAN ls, "No such file or directory", and steps\revit-results\PC-1-revit-test.md said the same). No window sets these two files. On the laptop the work root held the data book of the past job, so this stop did not show on 8 Nov.
- The two last sentences of the stop are not stops. They are the notes of FolderRoles why the project folder gave no row (BuildRead.cs line 77, FolderRoles.cs lines 55 and 90): landxml is not inside the project folder, and two folders of the project folder hold 13 villa files each. Both rows were set by a person before the Build, Road surface "landxml" with 6 surfaces in order and Prototypes "Prototypes_2024", and stayed so after it ("7 of 8 found"). Whether the Build would take the road levels of the pick is UNKNOWN, the Build did not get that far.
- So the baseline of card 2 on this PC is 0 of 9 neighbourhoods built, by one cause that is not of a neighbourhood. Step c has no built model to compare.
- Card 3 after the Builds: "2615 plots have no road level, so no floor level, and steps 6 to 8 leave them out." and "Values: computed 9879, rule 10907, person entered 2614, missing 10595, other 0." The head line: "15 of 15 rules are not confirmed. Open Rules in Settings."

## Card 3, Report

Not run. The button Report of card 3 was off after the 9 Builds, the card said "Waits for stage 2" and "2619 issues from the last build.", and step 9 in the list of Advanced said "Needs step 4". Step 4 Check stood Ready, steps 5 to 8 each waited for the one before. Update from report was on and was not pressed. No Excel was written.

## The end

- 12:31:25 Save on the quick access bar, `old-1b\OLD1-site.rvt`, 5,697,536 bytes. It holds card 1 and no built element. 12:31:35 the main window closed by its frame, 12:31:40 Revit gone, the lock deleted (round_addin\logs\windows.jsonl: lockTaken 11:46:46, windowClosed after 44.9 minutes, lockDeleted). One Revit window in the whole task, under the 110 minutes of a part.
- The input folder after, 12:32: 837 files, 8,874,086,637 bytes. Against old-1b\inputs-before.json: "before 837, after 837, gone 0, added 0, changed 0 (by size 0, by write time 0, by SHA-256 0)" (old-1b\inputs-after.json).
- scripts\checks\write_places.py --check: "PASS write places: nothing new on C:\, in Documents or in the repo outside the build folders".

## Second check, by a different method

| Claim | First method | Second method | Result |
|---|---|---|---|
| The input folder is unchanged | hash_inputs.py, Python, all 837 files before and after | PowerShell: 837 files and the same bytes now, 0 files written after 11:40 today, and Get-FileHash of a random 60 files against the before list | equal, 0 of 60 differ |
| Each of the 9 Builds stopped at Read data | the line of card 2 read by UI Automation, kept per neighbourhood in old-1b\logs, files b and the neighbourhood and _pane_end.txt | the work folder of the model on disk: 0 files of step 4 to 8 (no plan, floors, place, walls, toposolid, run_4 or run_5), 1 review file, 9 build plans | agree |
| The cause is the data book | the words of the card | the code lines named above and ls of data_book | agree |
| Plots per neighbourhood | the Review window of 01A, 278 | read_counts.py on plots.json of the read, 278 for 01A | equal for 01A, the other 8 by the script alone |
| Revit closed, the lock back | windows.jsonl | no Revit process, no window.lock | agree |

## Lines for Lead

1. Card 2 builds nothing on this PC: the read for the build needs labels.csv and plots.csv of data_book of the past job, which this PC does not hold and no window sets. Exact words above.
2. The read is never whole on 16 GB: 9 drawings need 2.73 to 53.15 GiB of free memory, so each press of Choose project folder reads again, and the first read also left out the footprint drawing at 1.53 GiB free.
3. Card 1 says Road levels Missing while Settings holds the road folder Found with 6 surfaces picked.
4. The tool does not find landxml of the work root by itself, a person browses the row Road levels. The 7th surface file, EXISTING_11.5 Sqkm, is not offered.
5. The Scope list holds 9 neighbourhoods and the card says 13 areas. 010 and 01B hold no villa type. 3 plots have the area "-" or none.
6. Card 1 says "78 prototype files found", six folders of 13, and the Prototypes row stays "Not chosen" until a person browses it.
7. After the first stop the later Builds do not ask the read again, though the read is not whole.
8. The stop words end with two notes that read as causes and are not, the road surface and prototype sentences.
9. The driver of the laptop is not on this PC. The new one is old-1b\scripts under the work root, patterns and window messages only.
10. status in steps\tasks\OLD-1b-revit-test.md is still open, the task file is not in the allowed list of the task.

## Not seen in Revit

- 4 Check, 5 Floors and ramps, 6 Levels, 7 Place villas, 8 Walls, 9 Report: never ran, for any neighbourhood.
- Whether one neighbourhood fits in 110 minutes on this PC: UNKNOWN, no Build got past its read. What is measured: card 1 takes 17 minutes, a read 61 to 132 s.
- A person using the windows, and any picture of a window.

## Left under the work root

- old-1b: OLD1-site.rvt with its two Revit backups, OLD1-card1.rvt, OLD1-empty-pristine.rvt, inputs-before.json, inputs-after.json, scripts (the driver, hash_inputs.py, read_counts.py), logs (driver.log, the text of every window, real values in them, never in git).
- OLD1-site_347b04be, the work folder of the model: the reads, the review of 01A, 9 build plans. New projects\OLD1-site_347b04be: the settings and rules of the project.
- round_addin\logs\windows.jsonl: 3 new lines of round OLD-1b.
