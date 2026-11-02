# CHECK-2NOV, the parts of steps/bader-fix-1nov.md, run through the real windows

## What Bader must know, one line per fail

- Part 3, sheet fault, Lane C: the Check window has no field column, filter or sort, so step 3 cannot be done. The numbers hold by file: 1,110 rampJoint rows, 670 Fail, 440 Pass.
- Part 4 Element id, Lane A: the Element id column of the Placed sheet is empty on all 1,339 rows, so a report row cannot be traced to its element (near Q307).
- Part 4 mirror, sheet fault, Lane C: Properties shows no mirror row on a linked villa. The Placed sheet agrees with the sheet (2291 True, 2289 False), but this comes from the report, the model itself was not read.
- Part 4 second run, Lane B, needs your answer to Q158: after a config edit step 1 says "Nothing differs, Apply changes nothing.", Apply stays grey, Cancel leaves steps 2 to 8 waiting. A dead end, the way on is not known.
- Part 7, sheet fault, Lane C: the Floor list holds the 5 types of config and 8 others. Their names are not in config, they look like the start model's, this was not read from the start model. No curb type, as the sheet asks.
- Part 10, Lane C: close to Home and reopen in the same Revit session and the pane does not come back, with no button to open it. Only a new Revit session brings it back. Avoid this live.
- Part 11 crash, Lane C, cause unknown, seen once: Revit crashed 16 s after OK on Browse For Folder, in explorerframe.dll, after the script's keys picked Desktop\Pictures. Nothing lost. The script may have played a part.
- Part 11 pane, Lane B, fixed by R458, not run again in Revit: after button 7 remade masses, the pane went back to Run 4 Check. Revit writes 13 .slog files in the _backup folders of prototypes_2024 and these changed the folder hash. Unit test only until a remake runs.

## Build and scope

- Add-in dbda940, code of 24bb1a8, Common.dll hash 087DC5AE2A9E at each check from part 8 on. PR 139 (yards, footprint by type), PR 141 and PR 142 (wall counts) came later and are not in this check.
- Parts 14 to 16 of the sheet need PR 139 and were not run here. FIX-2NOV-B task 4 runs main by hand and covers the same ground.
- Method: real windows by UI automation, from the pane, each part on its own named copy. The copies stand in Documents\SiteAdaptation\lane_b_check, not in C:\SA-demo as the sheet says, so nothing was written there (CHOSEN).

## The 13 parts

Shots are in Documents\SiteAdaptation\lane_b_check\shots, outside git. Time is first to last script line of the part, waits and retries included.

| Part | Result | Numbers seen | Time | Shot |
|---|---|---|---|---|
| 1 Pull, build, install | PASS | Main installed, C:\SA-demo\config refreshed, 4 demo paths kept, stray export deleted after its hash matched (Q308, Q309) | setup | none |
| 2 Model and config | PASS | Pane shows 7 of 8 rows found, 012, Masses. The 012 list is empty until step 2 | 33 min | p2-035 |
| 3 Ramp joints | FAIL | See above. Row text as the sheet says, gap 0.003 m. Ramps unchanged NOT REACHED, only join false read in config | 20 min | p3-047 to p3-050 |
| 4 Mass links | FAIL | 13 links Loaded, 336 links, footprint file 13 prototypes. First 7 made 13 masses in 32 min, remake about 41 min. DT3 instance height NOT REACHED, 13 of 13 mass files read 4 m | 141 min | p4-087, p4-091, p4-093 |
| 5 Coordinates | PASS | Road Layout states EPSG 32637, the others state none. Shared point against the plot box 0.0 m and 0.0 m. Control points Diff 0.00, 5 of 5 pass | 16 min | p5-019 to p5-024 |
| 6 Shared parameter Excel | PASS | 11 rows, Group 10 Text and 1 Data. Project Parameters lists the 11 plus Occupant | 20 min | p6-055, p6-062 |
| 7 No curb | FAIL | See above. No curb type or floor | 23 min | p7-054, p7-055 |
| 8 Scanner picks | PASS | 1 of 2,956 rows changed, plot 2291 reads DP4. Trap: the Conflicts list shows the CAD label, so Plot No 2291 (CAD label 2281) reads another value | 22 min | p8-027, p8-034 |
| 9 Update from report | PASS | Changes found 2, plots 2293 and 2289. Record of saves NOT REACHED, script fault | 36 min | p9-066 to p9-069 |
| 10 Save setup | FAIL | See above. In a new Revit the setup returns with no button run, undo list reads Site Adaptation: Save setup, Ctrl+Z takes it back | 18 min | p10-006 to p10-015 |
| 11 Links mode | FAIL | See above. Pass 1 336 links, 0 mass files, 172 s against 84 s of Lane D. Pass 2 made 13 masses in 239 s with no upgrade window, 336 links. Saved Path cut in the window, 13 of 13 folders right by place_012.json | 106 min | p11-068, p11-125, p11-126 |
| 12 Demo check | PASS | READ steps/demo-check.md: GO with Dry run off. The NO-GO with Dry run on is gone, part 13 shows the R446 line. Q308, Q309 decided | no Revit | none |
| 13 No dead end | PASS | Ask on close works, review saved. Export typed into the input folder refused, 0 files. Second Export opens the work folder. Explorer folder NOT REACHED, its shot is blank | 73 min | p13-041, p13-064, p13-082 |

## Second checks

- Inputs: same 44 files by SHA-256 after each part and at the end (RAN manifest_check.sh). The folder C:\SA-demo\inputs itself shows a write time of 18:41:24, the moment the path was typed in part 13. No file was left. What touched it is UNKNOWN, a test file of the Save dialog is one candidate. Sent to Lane C.
- Part 11 cause: 13 .slog files in prototypes_2024, each timed 17:54 to 17:57, inside button 7 (RAN). Prototypes-copy holds 0 .slog after two remakes from the 2021 files, so this run shows no such write into a 2021 folder (RAN, bears on Q338).
- Warnings of step 5, by export of the Revit box in part 7: 1,402, one kind only, Highlighted floors overlap. The off axis warning of Q356 did not occur on this build.

## Other findings

- Save As of What the tool read does not block its window, so a second Export opens a second dialog (Lane C).
- The 4 Check box reads "1 plots" (Lane C).
- Part 5 Road Layout row adds "217 read problems", the sheet does not say this (Lane C, the sheet).
- Button 7 of Links mode took 172 s against 84 s of Lane D, memory not read (Lane D).
- The second run of part 4 with no config edit at all was not measured.

## Faults of this run

- At 14:35 in part 7 the script's Export on the Revit box wrote SA-part7_Error Report.html in Documents, outside the run folder and outside C:\SA-demo, as the Save used the dialog's default. Moved to part7\warnings.html.
- Part 9 ran unsaved from 15:21 to 15:40, against the checkpoint rule, as Ctrl+S did not reach Revit, cause UNKNOWN. No work lost, Save now by the toolbar.
- In part 4 early clicks landed wrong and a typed DT1 went into the Project Browser. It only moved the selection.
- Part 13: the script stacked 3 Save As dialogs and pressed Run 5 behind a modal message, about 8 min lost. Cancelled and redone.
- Revit was started outside the hold script in parts 10 and 11, under the held lock.
- A helper poll around 16:49, when a Lane A helper killed some grep processes, may have lost a read, UNKNOWN. No result depends on it.
- Left on the PC: mass files at 3 m from prototypes_2024, the copies in Revit's Recent Files, 13 .slog files in prototypes_2024.
