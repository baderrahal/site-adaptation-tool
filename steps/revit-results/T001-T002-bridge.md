# T001 and T002 of the Bridge, Revit runs of 01A, 13 Nov 2026

Written by Claude Code from the reports Bridge\done\T001\report.md and Bridge\done\T002\report.md under the work root, where the evidence lies. Counts, times and differences in mm only. No coordinate, no level, no plot number. Times are local.

## The runs

| Run | Bridge task | Revit started from | Model | Result |
|---|---|---|---|---|
| Builds 1 and 2 | T001 | the lead folder through python\window_lock.py and a start script the guard did not see, on Bader's order in the prompt | a copy of old-1b\OLD1-card1.rvt as old-1b\OLD1-site.rvt | both Builds to their end |
| Build 3 | T002 | the worktree revit-test, under python\window_lock.py | OLD1-site.rvt as T001 left it, copied first to OLD1-site-t001-end.rvt | Build to its end |
| Read of the built model | T002 | the worktree revit-test, Run-TestHost.ps1 -RoundSpec, kind read, job BT2 | a work copy of the saved OLD1-site.rvt | exit 0, 1.65 min |

Each Revit was one window under the lock, closed by its own window. The input folder: 837 files, unchanged by SHA-256 before and after each run, checked a second time by PowerShell on 60 random files.

## Build 1, T001, press 03:39

Scope neighbourhood 01A, villa mode Links, 278 plots.

| Step | Box counts line | Time |
|---|---|---|
| 2 Read data | the read of card 1 again, worker 2 min 14 s | 03:39 to 03:41 |
| Read for the build | once, worker 28 s | 03:41 to 03:42 |
| 4 Check | builds NBH 01A on the read of 30 inputs | 03:42 |
| 5 Floors and ramps | place 530, move 0, already placed 0, skip 0, listed 265 | 03:42 to 03:44 |
| 6 Levels | move 0, at the plan 530, listed 13 | 03:44 |
| 7 Place villas | place 265, swap 0, move 0, already placed 0, skip 0, link types new to the model 13 | 03:44 to 03:45 |
| 8 Walls | place 596, change 0, already placed 0, skip 0, listed 597 | 03:46 |

Card 2: "Build done: 8 steps." No data book line, no scan window, no What the tool read window. 7.5 min.

## Build 2, T001, press 03:47, same model, no change

Free memory before the press 1,786 MiB, under the read peak of 3,900. Card 2: "Unloaded 13 villa links to free memory for the reads. Place villas loads back the ones it needs." Free memory 1,739 MiB before Go on, 3,682 MiB at the next box.

| Step | Box counts line |
|---|---|
| 4 Check | builds NBH 01A on the read of 30 inputs, the read reused |
| 5 Floors and ramps | place 0, move 0, already placed 530, skip 0, listed 265 |
| 6 Levels | move 0, at the plan 530, listed 13 |
| 7 Place villas | place 0, swap 0, move 0, already placed 265, skip 0, link types to load 13, no box of link types, villaLoadTypes 74.8 s |
| 8 Walls | place 0, change 0, already placed 586, skip 10, listed 597 |

Card 2: "Build done: 5 steps." 3.5 min. Revit's Manage Links: 13 of 13 link types Loaded.

## Build 3, T002, press 13:12, the model of Build 2, no change

Built with the R885 change. Free memory 720 MiB before the press, 646 MiB before Go on, 3,322 MiB at the 4 Check box after the release.

| Step | Box counts line |
|---|---|
| 4 Check | builds NBH 01A on the read of 30 inputs, the read reused |
| 5 Floors and ramps | place 0, move 0, already placed 530, skip 0, listed 265 |
| 6 Levels | move 0, at the plan 530, listed 13 |
| 7 Place villas | place 0, swap 0, move 0, already placed 265, skip 0, link types to load 13, no box of link types, villaLoadTypes 70.9 s |
| 8 Walls | place 0, change 0, already placed 596, skip 0, listed 597 |

Card 2: "Unloaded 13 villa links to free memory for the reads. Place villas loads back the ones it needs. Build done: 5 steps." 3.8 min. walls_01A.json: keep 596, skip 0, skipped empty. So R885 is the cause of the 10 walls Build 2 skipped.

## The read of the built model, T002

Revit's own read of the saved model, job BT2: links 265, with a plot number 265, distinct 265, link files 13, loaded 265, mirrored 25, floors 530 (Generic 150mm 265, Ramps 0.5% 249, Ramps 2% 16), walls 596, levels 2, errors 0. Open 53.3 s, read 14.3 s, peak private 4,510 MiB. key_reads.py --built wrote built\01A\key_reads.json, one read under keys.

## The compare of 01A against IMG, T002

As compare_built.py wrote it in steps/compare.md: plots 265 of 297, position 0 of 265 within 5 mm, largest 1,905,114.1 mm, FFL 2 of 265 within 5 mm, largest 9,658.0 mm, rotation 90 of 265, mirror 144 of 265, floors 530 of 594, yards not counted, walls 596 of 859, levels 2 of 2, result fail. compare\rows.csv holds 856 rows of 01A. Cause measured by Cowork in docs/truth.md, Where the build stands, 13 Nov.

## Not seen in Revit

- The R639 words "the next press of 2 Read data reads again", no read ran in Build 3.
- The tool window shows no build stamp. The SHA-256 of the installed DLLs equal to the Release build is the proof of the build seen.
