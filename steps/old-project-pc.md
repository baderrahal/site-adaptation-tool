# The old project on this PC, 12 Nov 2026

Lead, on Bader's PC. C:\02-Site Adaptation was only read. Counts, sizes and file names of config only, no coordinate, level or plot data. RAN means run in this session. Files written by this work lie under the work root C:\SA-work\SiteAdaptation.

## 1. What the folder holds

RAN: every file hashed once before any run, name, size, write time and SHA-256, into old-project\inputs-before.json of the work root. 837 files, 8,874,086,637 bytes.

Against the manifest: SA_COPY\manifest.csv is not on this PC, and the repo holds its counts only, not its rows (steps/laptop-check.md:58, 837 inputs rows, written by this PC from its own folder on 28 Oct). The folder holds 837 files today, the same count. Whether each file is the same as on 28 Oct cannot be proven file by file, UNKNOWN. What can be proven is in the rows below.

| Folder | Files | Holds |
|---|---|---|
| 00_Revit Models\Zone 02 | 364 | the IMC site models of four disciplines, the villa files, their backups |
| 01_Output | 422 | the drawings and schedules the team delivered, 12 folders, past outputs |
| Base Plans | 6 | 6 drawings |
| Master Plan | 5 | drawings |
| Native Files | 16 | the 4 older site model sets 1.1 to 1.4, 4 models each |
| Revit All Zones | 15 | 7 zone models, 7 Revit 2024 copies and their manifest |
| Roads | 4 | 4 drawings |
| Roshn Makkah - Master Plan Rev.09 | 5 | the Rev 9 workbooks and sheet |

By type: 236 dat, 160 dwg, 146 pdf, 116 rvt, 64 pcp, 34 png, 29 jpg, 27 slog, 15 rws, 7 xlsx, 2 bak, 1 json.

Missing against what the tool needs, found by search of the folder:
- No LandXML file, 0 files of type xml. The road levels of every run of 6 to 8 Nov came from 18 LandXML files, exported from the road drawings by Civil 3D into the data book of the old work root (steps/seam.md:78). That work root is gone. The road drawings are here, 4 files in Roads, so the export can be made again with Civil 3D 2026. Until then no plot gets a road level on this PC.
- The data book, the past plans and the scenario answers of the old work root (steps/pc-setup.md row 21).

The villa files, by python/rvt_info.py from their bytes:

| Folder | Files | Revit format |
|---|---|---|
| 00_Revit Models\Zone 02\ARC\Prototypes | 13 | 2021, workshared |
| 00_Revit Models\Zone 02\ARC\Prototypes_2024 | 13 | 2024, workshared |
| 00_Revit Models\Zone 02\ARC\Prototypes_2024\STR | 13 | 2024 |
| ELE, MEC and STR Prototypes | 13 each | 2021 |

So the 13 villa files are there, and 13 Revit 2024 copies of them are there.

The zone models of Revit All Zones:

| Zone model | In Revit All Zones | Its copy in Revit All Zones\2024 |
|---|---|---|
| IMA, zone 1, SE | there | there, format 2024 |
| IMB, zone 1, SE | there | there, format 2024 |
| IMC, zone 2, TU | there | there, format 2024 |
| IMD, zone 2, TU | there | there, format 2024 |
| IME, zone 3, TU | there | there, format 2024 |
| IMF, zone 3, TU | there | there, format 2024 |
| IMG, zone 4, TU | there | there, format 2024 |

Of the 7 originals 6 are Revit 2021 and workshared and 1 is Revit 2024 and not workshared, which one is in old-project\rvt-info.json of the work root. RAN `python\key_copies.py check`: `reuse 7 of 7`, each copy equals its manifest entry by the SHA-256 of copy and source and by format. On the laptop it gave 6 of 7 (steps/laptop-check.md item 12), the IMC source is here.

## 2. The scenarios

RAN, by make() of scripts/scenarios/make_scenarios.py, called from a small script outside the repo, since main() of the script first reads the answer files of the old work root, which are gone.

CHOSEN by Code: the script leaves out only the folders 01_Output, data_book and position (scripts/scenarios/scenario_tools.py:19). That keeps the past site models in a scenario: Revit All Zones, Native Files and the site models of 00_Revit Models. The order of 12 Nov says every past output out, so those were left out too, the villa file folders kept. Of the 837 files the script keeps 415, 70 more were left out as past models, 345 are in S1.

| Scenario | What it is | Files | Bytes | Manifest |
|---|---|---|---|---|
| S1 | raw inputs only | 345 | 6,194,464,711 | scenarios\S1_manifest.json |
| S2 | as S1, footprint drawings out | 343 | 6,184,929,941 | scenarios\S2_manifest.json |
| S3 | as S1, zone and neighbourhood words changed | 345 | 6,813,475,626 | scenarios\S3_manifest.json |
| S4 | as S1, no LandXML | 345 | 6,194,464,711 | scenarios\S4_manifest.json |
| S5 | plot numbers in another style | not made | | It needs the plot numbers of the answers, which need the answer key first |

Each manifest holds the SHA-256 and size of every file of its folder. S4 equals S1 here file for file, since the folder holds no LandXML to drop. S1 holds 78 Revit files, all villa files, and no site model.

Not done: the right answers of the scenarios and the two Actions secrets (scripts/scenarios/make_answers.py). They are built from the plans of the past models and the data book, both gone, and writing a secret is a change on GitHub. It waits for the answer key and a task of its own.

## 3 and 4. The villa files as Revit 2024 files, and the answer key

Both need Revit, which only the revit-test lane opens. Task steps/tasks/PC-1-revit-test.md. Its record is steps/revit-results/PC-1-revit-test.md.

Done by the revit-test lane on 12 Nov, 02:08 to 02:55, PR 294. Lead checked the results a second way: its own run of python/rvt_info.py on the 13 new files, a count of the rows of each links.csv, and its own hash of the input folder.

### 3. The villa files

- 13 of 13 villa files saved as Revit 2024 files into prototypes_2024 of the work root, one Revit at a time, 39 minutes in all, 2.2 to 4.7 minutes each. Each log result is ok. Lead's check: 13 files, 13 of format 2024 by their bytes.
- The originals were opened detached and never written: the 13 source files were hashed before and after each run, changed 0 on 13 of 13 runs.
- Against the 13 Revit 2024 copies the input folder already holds, by size: 11 equal, 2 differ by 4,096 bytes.
- The memory of this PC is tight for it: the least free memory in a run was 0.42 GB, the peak of Revit 3,256 MiB.
- 88 warning lines of Revit over the 13 upgrades, none stopped a run. Whether an upgraded file lost anything a later step needs was not checked, the files were not opened again.
- Accepted by Lead, CHOSEN by the lane: Run-TestHost.ps1 -Upgrade reads plan_imc_past.json of the work root at line 493 only for the file name of a code, and that file is gone. The lane wrote a stand-in holding the 13 file names and nothing else, and deleted it after the 13th upgrade. Lead's check: the file is not there. The script should take the names from the folder, a line of steps/plan.md.

### 4. The answer key

answer-key of the work root holds one folder per zone model, 7 of 7, each read from a work copy of its Revit 2024 copy with the read job of the test host, 4 minutes of Revit in all. Each folder holds the raw read, key_reads.json and links.csv. links.csv has one row per villa link with plot, prototype, position, rotation, mirror and origin Z. No coordinate or level is in git.

| Zone model | Villa links | Links with a plot number | Floors | Generic, yard, ramp, other | Walls | Levels |
|---|---|---|---|---|---|---|
| IMA | 433 | 432 | 761 | 733, 0, 0, 28 | 2,323 | 2 |
| IMB | 302 | 301 | 773 | 733, 0, 0, 40 | 1,515 | 2 |
| IMC | 338 | 337 | 1,010 | 336, 337, 337, 0 | 926 | 2 |
| IMD | 411 | 410 | 1,230 | 410, 410, 410, 0 | 1,138 | 2 |
| IME | 325 | 324 | 972 | 324, 324, 324, 0 | 894 | 2 |
| IMF | 491 | 490 | 1,470 | 490, 490, 490, 0 | 1,317 | 2 |
| IMG | 298 | 297 | 594 | 297, 0, 297, 0 | 859 | 2 |
| All 7 | 2,598 | 2,591 | 6,810 | | 8,972 | |

- Lead's check: the rows of each links.csv equal the villa links of the table, 7 of 7. The lane counted each number by two methods that share no code, equal on 7 of 7, and each equals the table of Round ADDIN (steps/history/round-addin/05-phase-2a.md lines 41 to 51).
- What the past models hold, for the compare: IMG has no yard floor. IMA and IMB, the structural models, have no yard and no ramp floor and hold the same 733 generic floors, so their floors and walls are no measure of what the tool builds. The pass level of yard floors can only be counted on IMC, IMD, IME and IMF.
- No link was loaded in any read, so a position is the transform of the link in its own model. Whether the 7 models share one coordinate base was not checked here.
- Not made: the plan of each key and the one table of all plots (python/plan_from_past.py --key, python/past_all.py). Both stopped on files of the old work root that are gone, the baseplan plots.json of sample_output and labels.csv of the location plan. The compare of steps/plan.md joins by the plot number of links.csv and does not need them. The plots of a key that join the baseplan are UNKNOWN until those two files are made again.

## 5. The input folder after the work

RAN by Lead after the lane ended, the same script as before the work, into old-project\inputs-after.json: 837 files, 8,874,086,637 bytes, against inputs-before.json gone 0, added 0, changed 0, by size, write time and SHA-256. The lane's own two checks gave the same. Revit was opened 20 times, never on a file in the input folder for writing.
