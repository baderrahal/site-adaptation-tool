# Round log, 4i to NEWPROJ-UI, moved with no word changed

## 4i, 2026-10-31, the plan by Manual Rev 06 with the past job as a switch, Lane A on the laptop

Goal:
- Bader's decisions of 31 Oct: door level source C, clusters B, back yard B. The plan follows the manual on a new project, the past job behind a switch

Done:
- R407 designBasis.mode rev6 or roshnPastJob, ROSHN keeps roshnPastJob (Q224). R408 El @ Entry the input (civilFile or roadPlusSlope), the slope the result
- R409 one named road surface, the 0.21 m of the 1.4 job in config. R410 back yard by 2.3.B and 2.3.C, no yard floor (Q225)
- R411 cluster and mirror proposals by 2.1.A to 2.1.E (Q226, Q229). R412 read only new project check, worker command newProjectCheck
- Review 1 and breaker 1: F1 fixed with a test (the rev6 slope after the overrides), the workbook closed, 10 LATENT lines, Q242, Q255, Q256
- Main merged after PR 112, 113, 114 and 115, the rev6 slope of a ramp on the open edge of Q116 added in the merge

Checked:
- Past job against main c145089, RAN: whole project and the 7 keys, 0 of 636,012 values differ on the whole project, 0 differ on each key, every plan byte-equal, the new fields null
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, both modes, 32 runs, 0 failures, 0 refused (336,934 fields per mode on the whole project)
- New project check on the real inputs, RAN: 14 lines, Python and C# equal on net48 and net8.0, 7 inputs unchanged
- Status block, RAN: main c145089 655 values (Bader 55, MEASURED 51, DOCUMENT 287, RECOMMENDED 4, Code 3, UNKNOWN 255), branch 674: DOCUMENT +8, RECOMMENDED +6, UNKNOWN +5
- Gate after the merge of PR 115, RAN: pytest 2,212 passed, 1 skipped. C# net48 1,691, net8.0 1,498, 0 failed

Remains:
- Value status of the rev6 values (Q230). The 121 IMC plots of the audit not reproduced, 116 by detached plus no proposal (Q240)

Known bugs:
- Under rev6 the one surface writes 630 of 2,704 plots (Q256) and the past override list still applies (Q255)
- doorLevel.source reads as a note in R401, the slopes beside it read DOCUMENT (Q241). oldJobOffsetM reads UNKNOWN in the block

Next:
- Lane B adds L4i-1 to L4i-7 of steps/waiting-on-others.md. Lane A 4j-2, floor split and corner walls

Your turn:
- Opened Q224 to Q242, Q255, Q256: Q224 The mode the ROSHN config runs. Q225 The level of a yard floor under Rev 6. Q226 The hand of a detached unit.
  Q227 Civil file format and plot match. Q228 Default slopes of road plus slope. Q229 A row with no single cluster split. Q230 Value status of the
  Rev 6 values. Q231 Civil door level where no road level. Q232 yardGrade in plan.json. Q233 Slope limit at 3 or 4 decimals. Q234 Read step road
  levels under Rev 6. Q235 Hand table cover per unit code. Q236 Label size in the PDF check. Q237 Folder of the Rev 9 workbooks. Q238 TH1-IM in the
  cluster sequences. Q239 Mirror of DP2 and DP4. Q240 The 121 IMC plots of the audit. Q241 doorLevel.source reads as a note in R401. Q242 The reason
  of an unreadable input. Q255 The override list under rev6. Q256 The one road surface covers a quarter. Closed: none
- 121 questions open, full text: steps/questions-open.md

## Lane B 6a, 2026-10-31, one NBH through all 9 steps and the test host jobs, Lane B on the laptop

Goal:
- 6a of steps/01_next.md: the test host runs every button with no click, one NBH from a new project through all 9, with Links joined from 5d

Done:
- R384: buttons 1 to 9 by number, Site Adaptation Rules and Update from report by name, shots of every window drawn in Revit
- Fixes with tests: Q274 the run id of levelExcel, the run log of 4 Check that 9 reads, the work root mark of the run script
- R108: the watcher leaves the upgrade progress of Revit 2024 open and stops on any other dialog
- The footprint flags of 336 of 336 masses measured: the masses stand right, 305 inside their block, the drawing holds plot blocks (Q276)

Checked:
- Gate at f007959, clean build, RAN: net48 1647, net8.0 1454, pytest 2162 passed, 1 skipped, 1 failed on the R401 block of Q277, fixed, its 25 pass
- Revit LB6A at f007959, RAN: 1 to 9 commit on NBH 012 in Masses from a new project, 6.7 min, peak 2444 MiB, least free 16.7 GiB, 42 of 42 inputs unchanged
- Times, s: open 19.3, 1 2.5, 2 57.8, 3 1.1, 4 25.7, 5 23.6, 6 4.7, 7 8.5, 8 4.1, 9 3.7, Rules 0.3. 336 masses, 1003 floors, the level and report Excel written
- Shots: 10 PNGs none blank, the differences from the boards in steps/ui-shots/README.md. Review 1 APPROVE, 6 latent lines

Remains:
- Links: the upgrades of the 13 prototypes stop on the first log step limit (Q275), 1 of 13 saved unchecked
- Update from report stops, it looks for the plan in the work folder root (Q278, Lane A told)

Known bugs:
- Revit hung once at start in a third party add-in before ours (try 1), not seen again. Latent lines: steps/latent.md under Lane B 6a

Next:
- DEMO on Lane B, then Links after Q275

Your turn:
- Opened Q275 First log step limit of an upgrade job. Q276 What the footprint check of R404 measures. Q277 Footprint block names unlike the unit.
  Q278 Where Update from report finds the plan. Closed Q274 by the fix of the round.
- 100 questions open, full text: steps/questions-open.md

## Lane D 4j-1, 2026-10-31, the scanner of any CAD, Excel and PDF, Lane D on the laptop

Goal:
- Step 1 Plot and step 2 Villa type for any project: a scanner of the CAD, Excel and PDF the user gives, no project name in code (Bader, 31 Oct)

Done:
- R420 to R427: worker command scan (CadScan, ScanSources, Common/Scan/ScanLogic) and its twin python/scan.py, scan.json per run
- Layer inventory after the explode, outline, number and villa block proposals by plots covered, drawing link, villa type match
- Villa type scored by prototype names, blocks first (Bader, 31 Oct, Q247 closed). The window needs in steps/waiting-on-others.md
- Reviewer and breaker, one pass each: REAL faults fixed with tests, PDF pairs by R180, codes kept whole, check rows, centroid

Checked:
- Gate, RAN: net48 1622, net8.0 1435, 0 failed. pytest after the records below
- ROSHN set A, RAN on copies, baseplan and footprint, 3 workbooks, the location plan, 13 prototypes: 2964 plots by the pair
  MP_PLOT_LINE + MP_PLOT_SUBD, best single layer 392. Today's reader 2962 plots, 2991 faces, the scan 2992
- Parity, RAN: net48, net8.0 and Python, rows 1548, conflicts 2808, checks 268, plots 11856 fields equal, 0 differ
- Renamed set, RAN: DXF copies with every layer renamed, every file renamed, 258 rows, 468 conflicts, 67 checks, 2964 plots
  equal to set A by the names mapped back, all three runtimes. 20 inputs unchanged by hash in every run
- Villa type against the 7 keys, 2557 plots: the plot table of button 2 0.993, the PDF 0.219, the labels 0.213, blocks 0.006 to 0.638
- Times, RAN: 184 to 299 s per C# runtime, Python 94 to 99 s

Remains:
- No source at the strong share, lead MP_VL4 blocks 0.116, the user chooses (Q254). Road Layout not scanned (Q250, 4j-1b)
- Button 2 does not start the scan, the scanner window of Lane B (Q246). The second face method parts on 143 of 162 candidates

Known bugs:
- PlotLabels.Centroid off 0.27 m at site coordinates in today's plots.json (Q251). Latent lines in steps/latent.md

Next:
- Lane D 4j-1b scan speed, queued

Your turn:
- Opened Q243 to Q246 and Q248 to Q254: Q243 Layer pairs for plot outlines in the scan. Q244 A tie in a scan proposal. Q245 Old
  and new code of one row agree. Q246 Button 2 starts the scan or the window. Q248 Villa blocks on one layer per type. Q249 A sheet
  whose numbers shift by area. Q250 The pair trace on the Road Layout. Q251 The centroid of today's plots.json. Q252 PDF tables and
  turned text in the scan. Q253 The header row of a scanned sheet. Q254 The order and share of villa type sources
- Closed Q247 Scan villa type takes the old CAD labels
- 97 questions open, full text: steps/questions-open.md

## Lane B 5d, 2026-10-31, buttons 5, 6 and 8 and 7 at the FFL, Lane B on the laptop

Goal:
- 5d of steps/01_next.md, Masses only on NBH 012 (Bader, 31 Oct 16:00), one PR with code, tests and records (D20)

Done:
- R378 button 5 Floors and ramps, R379 button 6 Levels with the levelExcel wait, R380 button 8 Walls, W6 button 7 at the FFL
- Q115 keyRules.wallOpen, weak at 0.7409. Q116 ramp of the 18 turned plots on the open edge, 18 of 18 on the key edge
- Q214 build takes the pane folder, Q215 upgrades under the work root, Q156 Plot by addin.fixedParameters, Q220, the worker path
- Review 1, breaker pass 1, Q116 breaker: 9 REAL faults fixed with tests, review 2 and the Q116 second pass approve
- Questions of 5d moved to Q260 up, Lane A holds Q224 to Q242

Checked:
- Gate at 6326583, clean build, RAN: net48 1603, net8.0 1416, pytest 2120 passed, 1 skipped. Records tests again at the PR head
- Q115 parity, RAN: 18 made-up cases, Python and C# equal on every wall. Q116 on the 18 turned plots, RAN: front pair 18 of 18 within 1.0005 mm of the keys
- Revit windows 8, 8e, 8f and G on NBH 012, RAN: 1 to 4 commit in all, 5 commits in 8e, 8f and G, 1003 floors. 6 stops in each
- Window G: peak private 2379 MiB, least free 23.34 GiB, inputs 42 of 42 unchanged by SHA-256 in every window

Remains:
- 6, 7 and 8 have not committed in Revit, the second run not run (Q274). Links mode joins 6a
- Open items 10 and 11 not measured, Q221 picture and Q218 not answered in the run

Known bugs:
- The levelExcel worker clears the temporary levels file of its run id (Q274). Latent lines: steps/latent.md under Lane B 5d

Next:
- Bader answers Q274, then 5d ends with the fix and window G again, then 6a

Your turn:
- Opened Q260, Q262, Q263, Q265 to Q271, Q273, Q274:
  Q260 Outline change of a placed floor. Q262 Level Excel and the read back values. Q263 Levels count still at its level. Q265 Ends status of walls after Q115.
  Q266 Walls on edges other units meet. Q267 Base level of a boundary wall. Q268 Scope of a wall on two plots. Q269 Untagged BW wall on a plan wall.
  Q270 Where a prototype set upgrades now. Q271 Python build takes the pane folder. Q273 6 Levels on a floor moved by hand. Q274 Run id of the levelExcel worker.
- Closed Q148, Q156, Q190, Q214, Q215, Q216, Q218, Q219, Q220, Q221 as recommended, Q261, Q264, Q272 by findings
- 86 questions open, full text: steps/questions-open.md

## Lane C NEWPROJ-UI, 2026-10-31, New project, How it works and the pane text, Lane C on the laptop

Goal:
- Build the window New project, the page How it works and the pane text from Bader's boards of 31 Oct 2026, one PR

Done:
- Boards copied from C:\SA-boards to docs/design/boards. Design part 18, labels in part 14, a note in 12-ui-spec 7.2
- Common/Settings: SetupRows, SetupCheck, SetupSave, SavedStarts, TemplateBook, rules R430 to R434 (ids past Lane D R420 to R427)
- AddIn/NewProject and AddIn/HowItWorks, pane rows Setup, Rules and Help, first open box, wired in PaneHost
- Saved project ROSHN Makkah written to the work root, Saved projects, by PreSetting.Export, read back by SavedStarts.List (RAN, 1)
- Reviewer and breaker, one pass each: 7 REAL faults fixed with tests, 10 LATENT to steps/latent.md

Checked:
- Gate at the PR head, RAN: net48 1525, net8.0 1341, pytest 2110 passed, 1 skipped. The four pictures in steps/ui-shots with their differences from the boards
- Template read back by the OpenXML reader of the worker, a second method (R433)

Remains:
- No Revit run, the windows are checked offscreen only. Change and View wait for the scanner (lane-b.md C1)

Known bugs:
- Save setup reaches the model with the next real run (Q280). Messages that name a work file (Q287)

Next:
- Bader answers Q280 to Q287, Lane B fills the scanner hook, Lane A writes R430 to R434 into docs/rules

Your turn:
- Opened Q280 to Q287: Q280 Save setup writes the model at once. Q281 Board rows against the nine pane inputs. Q282 Where the project name is kept.
  Q283 Picks of layers, sheets, columns and coordinates. Q284 Villa mode row the boards drop. Q285 Name the Rules row shows. Q286 Approve the four drafts of
  NEWPROJ-UI. Q287 Button messages that name a work file. Closed: none
- 84 questions open, full text: steps/questions-open.md
