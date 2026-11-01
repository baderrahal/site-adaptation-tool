# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## Lane D 4j-1b, 2026-11-01, scan speed, Lane D on the laptop

Goal:
- A scan of a large drawing under 10 minutes, the result of set A and the renamed set unchanged field by field (Q250)

Done:
- Measured per stage in the worker log: the layer pair trace of PlotShapes held the time, 2277 s for 220 of 3003 Road Layout pairs
- PlotShapes finds crossings and T junctions among the pieces and nodes in the cells along each line, in the old order
- R428 the pairs on scan.pairThreads threads (4, Q257). R429 scan.pairTimeCapS (360 s, Q258) and a notRead row for the pairs left
- Three attempts at the speed (a box index, a line index, threads), then the cap by the rule of three
- Reviewer and breaker, one pass each: a capped file now proposes no outline (REAL, the proposal changed with the clock), the
  full loop reference test (80 random drawings, indexed against every item, equal), 4 LATENT lines in steps/latent.md

Checked:
- Today's reader, RAN on the 3 drawing copies: 21 of 21 output files equal by SHA-256 before and after the PlotShapes change
- Set A and the renamed set against 4j-1, RAN, both runtimes: rows 1548, conflicts 2808, checks 268, plots 11856 fields equal, 0 differ.
  The 2 input fields that differ are the size and SHA-256 of config/project.json after PR 116. Python parity 0 differ, 20 inputs unchanged
- Times, RAN, whole scan before and after: set A net48 299 to 50 s, net8.0 184 to 34 s, renamed set 240 to 47 s and 207 to 38 s
- Stages: single layer faces of the Road Layout 87.5 to 7.5 s, the footprint pair stage 157.1 to 35.3 s, the baseplan pairs 10.1 to 6.4 s
- Road Layout, RAN: before over 40 minutes and stopped, after 489 s on net48 and 465 s on net8.0, the cap left 1851 and 746 of 3003
  pairs, each with the notRead row and no outline proposed. Python against net8.0 on a capped run 0 differ. With no cap 967.8 s, one thread
- An input changed while read: a merge of main during a run changed config, the net48 read refused its result (R1, D19), run again

Remains:
- On 4 threads net48 traced fewer pairs than net8.0 on one, the threads help net8.0 only (Q257). Q250 stays open

Known bugs:
- A capped scan traces other pairs each time, its outline is not proposed so the result does not change with them (R429, Q258)

Next:
- Lane D waits for Bader's answers to Q243 to Q258

Your turn:
- Opened Q257 Threads of the scan pair trace. Q258 The time cap of the scan pair trace
- 129 questions open, full text: steps/questions-open.md

## Lane C SCAN-UI, 2026-11-01, What the tool read and the step gates, Lane C on the laptop

Goal:
- Bader's round SCAN-UI: the window What the tool read from the board ScanResults, button 2 starts the scan (Q246), New project picks
  from the scan (Q283), Current files in 1 Set up site, cluster confirm, door level picker, Revit pictures. Freeze 09:30 for the demo

Done:
- PR 117 with tasks 1 to 4, PR 2 with tasks 5 and 6
- R435 Scan value status and R436 Save scan picks with setup: AddIn/ScanResults, Common/Scan/ScanView.cs, ScanPicks.cs
- R438 Button 2 scan gate and R439 Scan reuse by input hash: ScanStep.Wrap in ReadDataBody.For, ScanGate.Ask, no scan in a test host run
- R440 Drawing facts from the scan: LatestScanFacts, layer, sheet and column dropdowns, View opens the window
- R441 Current files gate: the section Current files and Rules in 1 Set up site, Apply waits for the site drawing, the line in button 9
- The settings JSON gains scanPicks, conflictPicks and currentFiles at formatVersion 1 (Q288), a saved project carries scanPicks
- Lane C rules in docs/rules/12-lane-c-r430-up.md, R430 to R434 of NEWPROJ-UI written there too
- R442 Cluster confirm marks: column Cluster with the proposal and mirror of 4i, Confirmed, Confirm all proposed, clusterUnconfirmed
  in review_<NBH>.json for buttons 5 to 8. R443 Door level source counts: Door level from in 4 Check, doorLevelSource kept
- Reviewer with the UI checklist and breaker, one pass each on PR 117: 12 REAL faults fixed with tests, 6 LATENT to steps/latent.md

Checked:
- Gate, RAN: PR 117 net48 1745, net8.0 1534, PR 2 net48 1769, net8.0 1551, pytest 2212 and 1 skipped each, 0 failed
- PR 2 reviewer and breaker: the skip list from the plan of 4 Check, a tick for the whole cluster, door level row under rev6 only
- Pictures: steps/ui-shots/scan-results.png, setup-site.png, new-project.png, differences in steps/ui-shots/README.md
- Second method: the review read the scan.json of the Lane D run (1218 conflicts, 332 keys shared), which led to the key with sourceB

Remains:
- Task 7 Revit pictures from the DEMO run of Lane B, which owns the install. The scan window shows only offscreen, a test host run skips the scan (Q295)

Known bugs:
- The read of button 2 does not take the picks yet (Q292). The first press hashes the prototypes, over 1 GB, on the Revit thread once

Next:
- Lane B DEMO with the add-in of main, Lane B joins the skip of clusterUnconfirmed (Q300)

Your turn:
- Opened: Q288 Scan picks in the settings at version 1. Q289 Board rows the scan does not read. Q290 Curbs and road surface left out
  of scan. Q291 Where the line Current files is shown. Q292 The read does not use the picks yet. Q293 Approve the three drafts of
  SCAN-UI. Q294 Rules chosen in Set up site, when used. Q295 Test host runs skip the scan. Q300 Plots with no cluster
  proposal. Q301 The build takes the door level pick. Q302 Door level row under the past job
- Closed: Q246 Button 2 starts the scan or the window, Q283 Picks of layers, sheets, columns and coordinates, by Bader's prompt
- 130 questions open, full text: steps/questions-open.md

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

## Lane B 5c, 2026-10-31, buttons 2, 3, 4 and 7 and the Revit run after 5c, Lane B on the laptop

Goal:
- 5c of steps/01_next.md with the Revit run after 5c on NBH 012 in Masses mode, one PR (D20)

Done:
- D15 (RAN): 7 read steps, 14 parity lines, 0 failures. Q126 manifest 528 of 837 equal, 0 differ. Border made, 747 of 747 plots
- Buttons 2, 3, 4 and 7, review data, read data, rules window, plan file, check table, pane. Rules R375 to R377, R403 to R406
- Fixed with a test: pane hashed wrong folders, plot list scope, scope on the IM letter, 2 plan yard faults, worker path
- D20 reviews: reviewer, breaker and a second pass of both. 4 REAL faults, all fixed. The fixes after the second pass carry tests and the Revit run, no third pass
- R405 fix (c66b512): button 2 alone in a fresh session committed, 42 of 42 unchanged
- Step shape fix of the check table: 1237 rows on NBH 012 in window 10. Host fixes: layer list, data book, config folder, prototype folder, base shapes, host loops on copies

Checked:
- Gate at 52a9b26, RAN: net48 1503, net8.0 1326, pytest 2110 passed, 1 skipped
- Plan parity, RAN: 754,451 values equal, 0 differ, 8 covers, both runtimes. 248 inputs unchanged by hash
- Windows, RAN: probes 1, buttons 10, judge 3, button 2 check 1. Window 10 (exit 0): 336 solids placed and read back, 201.6 s for 7
- Plain judge second run: held 18, not held 2 (J10), not measured 1, peak 2145 MiB private. Button peak private 3906 MiB, least free 31.57 GiB
- Base boundary (Q206): other elements read on 13 of 13 prototypes, 0 of 336 solids hold at 1 mm

Remains:
- J10 measured, not held as before: a detached document keeps PathName and Title '_detached' (Q220)
- Q146 UNKNOWN. Q147 save stop held, pane first show and 16 px icons not measured (Q221)
- NBH 03 image of Q063 not run (Q222). Links not run (Q215). 5d, 6a to 6c, J11 and J14

Known bugs:
- The build ignores the pane Prototypes row (Q214). Host raise not retried (Q223). Latent lines: steps/latent.md

Next:
- 5d on Lane B after the PR of 5c merges

Your turn:
- Opened Q180 to Q223:
  Q180 designValues status in the R401 block. Q181 Timing of the labels J11 and J14. Q182 Side band of majority.side_neighbours. Q183 Time cap of a buttons job. Q184 Plain rule names of
  the check table. Q185 A plot list over two NBHs. Q186 Close J2 as no work. Q187 Prefill of a new project, J16. Q188 Entity load on a session unseen. Q189 Check table modal or modeless.
  Q190 Where buttons 5 to 8 find the plan. Q191 The plan name of a plot list. Q192 Solid height of light mode. Q193 Footprint limit of the base. Q194 RRE parameters on Generic Models.
  Q195 Dry run of button 4. Q196 addin.overrideFile in config. Q197 key_join.json and key reads as inputs. Q198 Past gaps of the missing and question rows. Q199 What the tool does per
  manual rule. Q200 Export to Excel of the two windows. Q201 Data book files with no Python. Q202 The schedule folder in config. Q203 A worker end on a document not active. Q204 Manual
  rule of a Review data row. Q205 Corner table coordinates of Review data. Q206 Base boundary of a prototype. Q207 No foot and no Esc in the windows. Q208 How to use line of Rules. Q209
  Manual column mixes section and revision. Q210 Q015 in a past job text. Q211 7 after a plot list 4 Check. Q212 4 Check stops 7 on another NBH. Q213 Use of an override entry under Skip.
  Q214 The build reads pastJob, not the pane. Q215 Where the 2024 prototypes come from. Q216 Rows of 4 Check with no past job. Q217 Follow every rule count, table against chip. Q218
  Config files beside a user's config. Q219 First run of 7 against the step limit. Q220 Judge rows of J10 test the old claim. Q221 Judge rows for Q146 and Q147. Q222 The NBH 03 image of
  Q063. Q223 The test host gives up after a refused raise.
- 76 questions open, full text: steps/questions-open.md

## Round ADDIN 5e and 5f, 2026-10-30, button 9 Report and Update from report, Lane A on the laptop

Goal:
- 5e (R381) and 5f (R402) of steps/01_next.md in one PR (D20), the last Lane A phases of Phase 4 and 5

Done:
- R381: the report Excel in the 9 steps (J12), a Manual rule cell on each line (W7), Q123 faces, Q124 Revit counts, Q125
- R381: the body of button 9, it stops with needs step 4 until 4 Check writes a run log (Q175)
- R402: Update from report, read back, refusal of another build, change list, rule checks, overrides, python/update_check.py
- The window Update from report (W8), its worker command, its labels and steps/ui-shots/update-from-report.png, Draft
- Open item 12 measured: 2 of 4 shots in our own Revit (5a run 3), the dialog and the check table after 5c

Checked:
- Gate, RAN after a laptop restart: Python 2,056 passed, 3 skipped. C# net48 1,262, net8.0 1,137, 0 failed. One R310 process test failed once before the restart and passed after it, both runtimes
- Report, whole project, RAN: 461,749 matches, 0 mismatches per runtime, 431,245 cells equal across runtimes and inputs
- NBH 012 Summary, RAN: 2 Read data 337 and 43 as 7.6 (Q123). 5 random plots at seed 21, 15 of 15 equal to the plan
- Update, RAN: real copy and probes, 13 cases, 127,844 cells equal, 0 mismatches between the C# and Python routes
- Reviewer and breaker (D20): pass 1 REAL faults fixed with tests, breaker pass 2 found 3 more, fixed with tests
- Breaker passes ran Python only, the C# side of the fixes rests on the tests and the route compare (UNKNOWN in Revit)
- CI of PR 110: the NBH of a file name now parts at either separator and the held open test runs on Windows only. R347 timing tests flaked once (latent)

Remains:
- Manual rule IDs read no block until Lane B 5c writes manualRules (W3, Q165). No button writes a run log yet (R383)
- No Revit run of 9 or of Update from report. docs/rules/07 is near the 100 KB cap, the next rule text needs a new part

Known bugs:
- R402 LATENT findings in steps/latent.md under 5e and 5f, the stale list on a hand edit while the window is open
- The window lacks the guide line, side box and row mark of the UI spec (Q179)

Next:
- Lane A is done for Phase 4 and 5. Lane B runs 5c, 5d. Phase 6 after them

Your turn:
- Opened: Q164 Levels count and the link Z of 7. Q165 Shape of the block manualRules. Q166 Manual rule of a check row
- Q167 Manual rule on the other sheets. Q168 The villa type of Update from report. Q169 Key of an update entry with none of its job
- Q170 updateChecks of manualRules. Q171 Which breaks untick a change. Q172 heightM gap with an El @ Entry edit
- Q173 Level profile of Update from report. Q174 The plan Update from report reads. Q175 Run log format 9 Report reads
- Q176 Plain names of the update rule checks. Q177 Manual rule ids in Rule check. Q178 A computed mirror edit breaks the hand rule
- Q179 Guide line, side box and row mark of Update. Closed: none
- 32 questions open, full text and the recommended answers: steps/questions-open.md

## Round ADDIN 4h-2, 2026-10-29, R366, R369 and the join items, Lane A on the laptop

Goal:
- 4h-2 of steps/01_next.md in one PR (D20): R366, R369, Q017, Q055, Q119, Q120

Done:
- R366: ButtonScope (plot list syntax and stops) in Common/Scope, StepStates and StepHash in Common/Steps, for the pane by W9
- R369: python/phase4_check.py, the second method of Phase 4 at seeds 14 and 7
- Q017 values CHOSEN by Bader, its two R401 tests moved, HELD_OPEN out of rule_status.py
- Q055: a key El @ Entry off its own ramp back is read off the ramp back, in clean_entry and entry_of (Q159)
- Q119 reads by the override that set each input. Q120 mirrored the yard outline and type, rotationDeg turned walls only

Checked:
- Gate, RAN, after the merge of main with Lane B 5b: Python 2,038 passed, 3 skipped. C# net48 1,215, net8.0 1,104, 0 failed
- Whole project on 2a7ea18, RAN: parity 13 of 13 steps per runtime, 0 failures, status step 8,112 fields
- Level Excel 70,519 matches, 0 mismatches per runtime. Peak net48 1,031 MiB, net8.0 1,018 MiB (RAN)
- R369, RAN: seed 14 1,154 and seed 7 1,188 matches, 0 mismatches, on the Python and both C# step sets
- Against 4h-1, RAN: plots and walls equal outside status and reads. 136 Q119 and Q120 moves as aimed, 9 of Q017
- Q120 second method, RAN: a build with no rotationDeg differs on the 18 walls named and on none of the other 30
- 5 plots at seed 21, RAN: Python and C# steps equal on 5. 2 plots differ from the levels step by their overrides, as built
- Reviewer and breaker one pass each (D20). Review: Q055 second route and rule line fixed with a test. Breaker F1 is Q163

Remains:
- R366 has no caller until W9 of Lane B, which must feed locationPlanNumber and nbhId (steps/latent.md)
- medianHeightM of native2022 stale until a majority run with the Native reads (Q151)

Known bugs:
- 4 yards that stand only by a mirror override read a status stronger than their evidence (Q163)
- R369 passes a short plan or an empty step (LATENT, steps/latent.md)

Next:
- 5e and 5f together on Lane A. Lane B runs 5b, then 5c with R366 at the pane

Your turn:
- Opened: Q159 Q055 reads 11 more 1.4 plots off the ramp. Q160 A plot list takes unit plots only
- Q161 A changed step reruns every higher step. Q162 Reads of entryM with heightM or rampFrontM
- Q163 A yard that stands only by its mirror. Closed: none
- 16 questions open, full text and the recommended answers: steps/questions-open.md

## Lane B 5b, 2026-10-29, button 1 Set up site, the watcher before Revit and the freeze guard

Goal:
- Button 1 on the four copies of 3.1, open items 3 to 8 in Revit, its window by 7.3. First the frozen Revit of the stopped session

Done:
- The freeze: a probe started Revit outside the test host and its own watcher left the RCRC Green box open (READ watch_5b logs). No Revit was left, the IMD copy it read unchanged by SHA-256
- Test host: the watcher starts first and waits for the pid, the tool names come from the installed manifests, probes run by -DryStart -ProbeManifest, the freeze guard (5 min no first step, 10 min no heartbeat, 5 min not responding)
- Button 1 in Common and the add-in, its window with setup-site.png, the setUp mode of the addinRun job. Q154 to Q157 opened
- RCRC Green no longer loads in Revit 2024 here (Bader, 16:31). Its manifest moved from %APPDATA%\Autodesk\Revit\Addins\2024\RcrcGreen.addin into %APPDATA%\Autodesk\Revit\Addins\2024\_disabled\RcrcGreen.addin, SHA-256 271D5599 unchanged, its DLL folder RcrcGreen left. None in C:\ProgramData. Move it back to load it again
- Test host watcher: Do Not Load for any add-in not Site Adaptation was already the rule of SecurityDialogs.Decide. The test R108_any_add_in_that_is_not_site_adaptation_gets_do_not_load_and_each_answer_is_logged holds it, it passed at its first run

Checked:
- Hang probe, RAN: stopped at 7.4 min on not responding, the windows and 40 journal lines logged, that pid only, step failed, exit 1
- Revit runs of button 1, RAN, 3.3 min each, peak 2,333, 2,247 and 2,250 MiB private, least free 29.1 GiB. Run 1 failed on closing the active document, fixed, runs 2 and 3 8 of 8 steps ok
- Badge on the 4 copies: template and no template empty, IMD existing with 4 signs, R17B stand-in existing with the parameter sign and 10 stops
- Second method, RAN: the facts after Apply by the test host's own API reads held 30 of 30 values within 0.5 mm of the position file, largest gap 0 mm, in runs 2 and 3. Dry run Revit column equal to the value after Apply on 6 of 6 points
- Gate, RAN, on the last commit after the merge of main: dotnet net48 1,152 and net8.0 1,041 passed, pytest 2,000 passed, 3 skipped. Parity on the 7 keys not run: no plan or build step changed (CHOSEN). No plot in button 1, so no 5 plot sample (CHOSEN)
- Reviewer, 1 pass: 2 REAL fixed with tests (sheet count, failed Browse), 1 not a fault, 4 latent. Breaker pass 1: 2 REAL (an empty second run committed, 0 of 2 before a dry run), pass 2: 3 REAL (waiting items not shown, a slope picked after the view), all fixed with tests, 1 question Q158, 17 latent in steps/latent.md
- Revit run 3, RAN, after the fixes: second runs commit 0 on all 4 copies, the IMD copy first run commits 0 since nothing differs, 30 of 30 values at 0 mm again

Remains:
- The R17B file is not on the laptop. Its stand-in was made as R212 made it, the parameter names with GUIDs of their own (CHOSEN)
- In 5b the check table holds the survey point and the project base point. The extreme plots and the control points need the location plan of D15 in 5c
- J17 to J20 of steps/lane-b.md. The imperial no template project was not run

Known bugs:
- No Settings entity until R367, Browse stops until the worker reads xlsx, the position path of config is the PC one until J5 (J19)

Next:
- 5c with the Revit run after 5c on Lane B, 4h-1 is done (PR 107)

Your turn:
- Opened: Q154 Freeze guard limit for an image job. Q155 The parameter sheet of button 1. Q156 The GUID of Plot. Q157 The type button 1 copies new types from. Q158 Step 1 Done on a model already set up
- 11 questions open, full text: steps/questions-open.md

## Round ADDIN 4h-1, 2026-10-29, Q122, R367, R368 and the join items, Lane A on the laptop

Goal:
- 4h-1 of steps/01_next.md in one PR (D20): Q122, R367, R368, D13, the D19 input guard, Q075

Done:
- D20 into CLAUDE.md and steps/decisions-2026-10-28.md. The draft 00d371f was in C:\Dev\site-adaptation-tool-a, Q122 written again
- Q122: Clusters columns FFL by rule and FFL as written, items 36 and 37 of 7.7, read by the checker from 7.7
- R367 settings JSON in Common/Settings, R368 tags and the second run match in Common/Tags, Revit adapters in AddIn/Revit
- D13: rule_status.py reads open questions from questions-open.md, Q017 held open by name until 4h-2
- D19: python/input_guard.py in plan_from_book.py and read_steps.py, InputGuard of Common in the worker and CadReader
- Q075: 1,346 native entries with no value moved to cannot by override_source.py cannot, not by majority (Q151)

Checked:
- Gate, RAN: Python 1,998 passed, 3 skipped, then the one failure test_r272 fixed for Q075. C# net48 1,089, net8.0 1,003
- Whole project on 974a869, RAN: parity 13 of 13 steps per runtime, 0 failures. Level Excel 70,519 matches, 0 mismatches
- Runtimes, RAN: 102,862 cells equal, 0 differ. 355 of 616 piece rows hold an FFL as written
- D19, RAN: worker read 15 of 15 inputs unchanged per runtime, 3 drawings in place. Python build 13 of 13 unchanged
- Peak once per runtime (D8), RAN: net48 1,065 MiB in 25.7 s, net8.0 1,021 MiB in 19.2 s
- Q075 second method, RAN: 11,075 kept entries equal in order, parsons build 2,518 of 2,518 plots equal to p4g3_prB
- 5 random pieces by id, RAN: 12 plots, plan, Levels and Clusters agree on 12, differ on 0 (seed 14)
- Reviewer and breaker one pass each (D20). No REAL logic fault. Review test gaps fixed with tests, the LATENT ones to latent.md under 4h-1

Remains:
- The Revit adapters of R367 and R368 never ran in Revit, the run after 5c judges them (UNKNOWN)
- The majority run with --replace native needs the 4 Native reads, not on this laptop (Q151)

Known bugs:
- A guard failure at the end leaves the plan and step files of the failed run (LATENT, steps/latent.md)
- Prototypes_2024 is a copy under the work root with its write times, not the upgraded set

Next:
- 4h-2, then 5e and 5f together. Lane B runs 5b

Your turn:
- Opened: Q151 Q075 moved without the majority run. Q152 Mode of the last run of a button. Q153 A lone copy of a tagged element
- 6 questions open, full text and the recommended answers: steps/questions-open.md

## Round FOLD, 2026-10-29, the fold in, D9 to D19, the answer pass and the manual rules

Goal:
- Fold Lane B into the shared records, write D9 to D19 with the lane runner and the real folders guard, take Bader's answer pass, write M01 to M15

Done:
- PR 101 the fold in records, PR 102 the design (9 steps, Revit run 3), PR 103 D9 to D19 and the start files, PR 104 tools/run-lane.ps1 and guard-inputs.sh, PR 105 the answer pass and docs/design/15-manual-rules.md
- QL1 to QL22 are Q126 to Q147. 5a done by Q147. steps/lane-b.md 54 KB to under 15 KB, the rest in steps/history

Checked:
- Questions, RAN: before 70 open, 55 closed, 22 QL, after 3 open, 147 closed, 16 of them in steps/waiting-on-others.md, no id lost
- M01 to M15, RAN: 15 found, 15 equal to the prompt. M14 and M15 differ from the PDF in meaning (Q149, Q150)
- D19 hook, RAN: 25 cases pass. Runner, RAN: Lane A and Lane B each exited 0 on LANE DONE, Lane B made its worktree, STOP ends it
- Each PR one reviewer pass, CI green on all five. Local gate runs, RAN, outputs in the scratchpad of this session outside the repo:
- PR 102: one run 29 failed and 6 errors beside other Python runs, the 35 passed on rerun. One run 2 net48 failures, 1037 of 1037 on rerun, names not kept
- PR 103: 1 failed, passed on rerun, name UNKNOWN. PR 104: 1 failed, test_r343 on a control byte in steps/latent.md, fixed. PR 105: 2 per runtime on the status of Q017, held as below

Remains:
- 4h-1 the first PR takes the Q122 code. The old Lane A folder is not on this laptop (SEARCHED the C drive to depth 4 by Get-ChildItem, only the Lane B worktree found), so 4h-1 writes Q122 again unless it is found elsewhere
- The rule_status.py change of D13 and the input hashes of D19 are join items of 4h-1
- Answers that change a value of a built plan or its outputs, each measured by the phase that codes it: Q075 the native list (4h-1), Q055 El @ Entry of 1.4, Q119 and Q120 the status detail and sets (4h-2), Q086 the key check at 2 mm (5c), Q123 to Q125 the report counts (5e). The status block of rules/roshn-makkah.json moved 5 values off RECOMMENDED, 4 to CHOSEN by Bader, 1 to CHOSEN by Code. The 5 of Q017 stay RECOMMENDED until 4h-2 changes the two R401 tests that pin them (CHOSEN, no test logic changed in this round)

Known bugs:
- python/rule_status.py reads the open questions from Bader's turn, so D13 holds in full only after 4h-1
- The hook judges words: a command line that names the link command or an input root beside a write word is blocked (steps/latent.md)
- git cannot delete 4 stale .git/worktrees entries, Permission denied

Next:
- 4h-1 on Lane A, 5b on Lane B, both by tools/run-lane.ps1

Your turn:
- Opened: Q148 Place villas before Floors and Levels after 5c. Q149 M14 boundary wall of an irregular plot. Q150 M15 foundation depth, at least or equal
- Closed: 92, Q021 by D3, the 22 Lane B questions and 69 more in the answer pass, ids in steps/questions-closed.md
- 3 questions open, full text: steps/questions-open.md

## Lane B L0 and 5a, 2026-10-28 to 2026-10-29, folded in by Round FOLD

Goal:
- L0 the laptop check, light mode and the UI spec. 5a the install, ribbon, pane and one transaction per button

Done:
- L0: steps/laptop-check.md, docs/design/11-light-mode.md, 12-ui-spec.md, D1 to D3 and U1 to U7
- 5a: PR 84, 86, 87, 89, 90, 92 (the 9 steps, the ribbon of four panels), 97 the fix of Q145 and Revit run 3, 99 the detached document
- The full end reports: steps/history/lane-b-2026-10-29.md

Checked:
- Revit run 3, RAN: 3.4 min, exit 0, peak 2,146 MiB private, 18 of 18 steps ok, judge held 17, not held 2, not measured 2
- Open item 1 held in full. The raised ExternalEvent ran on the Home screen with 0 documents open (RAN)
- Open item 2: a detached document never saved has a bare file name as PathName (RAN), fixed in PR 99 by SavedPath.Is

Remains:
- 5a close waits for Q147. Not seen in Revit: the save stop on a detached document, the first show of a hidden pane, the 16 px icons
- Bader's final approval of revit-ribbon.png and revit-pane.png

Known bugs:
- The tab title sits behind the overflow mark at 1536 logical px (Q146). The latent findings: steps/latent.md, part Lane B

Next:
- 5b Button 1 Set up site on Lane B

Your turn:
- Opened: Q146 Tab title hidden behind the overflow mark. Q147 Close 5a with three items not seen in Revit
- Closed: Q126 to Q145, the Lane B QL1 to QL20, answered by Bader on 28 and 29 Oct 2026
- 71 questions open, full text: steps/questions-open.md

## Round ADDIN 4g3, 2026-10-29, 4g3 done on the laptop, R365 and the C# checks of Q111

Goal:
- R365 the report Excel with D4 (D7), the C# checks of Q111 (D6), the end of 4g3

Done:
- PR 95 start records, D5 to D7. PR 96 R365, the worker command reportExcel, report_excel_check.py
- C# check of Q111 on c59e854, nothing to fix. End records: Q123 to Q125, 11 LATENT lines

Checked:
- Q111, RAN: 228 parity lines, 0 failures, both runtimes, the 7 keys and the whole project
- Q111, RAN: level Excel 69,903 matches, 0 mismatches per runtime. Build peak 973 to 1,064 MiB, 11 to 13 s
- PR 96, RAN: gate green, 14 workbooks and 901,701 matches, 0 mismatches per runtime
- PR 96: 2 review passes. Rows 4 and 5 of NBH 012 equal 7.6 (RAN). Started in the repo

Remains:
- 7 read steps of the location plan, the tables and the position not run, inputs not assembled (UNKNOWN)
- Q123 to Q125 left after PR 96 (Q097). Q122 answered, its code moved to the first PR of 4h (D8)

Known bugs:
- Q111 used 3 stand-ins: CAD reads made again, the site file from plan_012.json, the 2022 prototypes (CHOSEN, logs p4g3_q111)
- The Q111 run wrote one request file into the real Zone 02 ARC Prototypes folder by a junction. Deleted, 13 .rvt left, all dated 2024 (RAN, no hash before), junction gone
- Plans hold no build id, so D4 rests on the plan SHA-256

Next:
- 4h in Lane A, R366 to R369 with Bader's 9 steps, then the fold-in of steps/lane-b.md

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.
Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters.
Q119 Reads of a value several overrides touched. Q120 Mirror and rotation sets too wide.
Q123 2 Read data counts faces or plan plots. Q124 Counts box before the Revit read. Q125 Walls Issues row in the Issues sheet.

## Round ADDIN 4g3, 2026-10-28, 4g3 paused, moved to the laptop

Goal:
- Q112, Q113, R364, R365, the parity of step 15. Bader moved Phase 4 to the laptop on 28 Oct

Done:
- PR 83 records. PR 85 Q112 by the fallback (the leave-one-out added 15.5 to 32.2 percent to the time) and Q113
- PR 88 step 15, roadAtFrontM, Q114, the 18 conflicts of Q116
- PR 91 R364 the level Excel, one per NBH, with the D4 round trip (Q121)
- These records: 4g3 in progress, continues on the laptop, Q122 opened

Checked:
- Gate and three checks green before each merge (RAN). PR 85: 103,131 of 103,131 values name every override that moved them (RAN)
- PR 88: roadAtFrontM 2,518 of 2,518 against a second script, the summary 8 of 8 checks per cover (RAN)
- PR 91: 14 workbooks per runtime, peak 628.8 MiB, 138,620 matches and 0 mismatches per runtime (RAN)

Remains:
- R365 the report Excel and its D4 round trip: Placed keyed by plot, Villa type and Mirror editable, a Settings sheet with the list, build id and plan SHA-256
- The C# checks on real data of PR 85 and 88, as steps/01_next.md lists them
- The end records of 4g3. The usage row of this session was not run, UNKNOWN

Known bugs:
- Python plans hold no build id, so their Excel reads "none, the plan holds no run stamp". 124 plots with no row in plots.csv are in no level Excel, named in the run issue

Next:
- 4g3 on the laptop, then 4h. steps/01_next.md lists what is left

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.
Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters.
Q119 Reads of a value several overrides touched. Q120 Mirror and rotation sets too wide. Q122 Clusters FFL by rule or as written.

## Round ADDIN 4g2, 2026-10-28, Q105, Q107, Q109, Q110, the walls and the check rows

Goal:
- Q105, Q107, Q109 and Q110 as Bader answered, R362 the walls, R363 the check rows with the chip Assumed of Q108

Done:
- PR 74 and 77 records: Q105 to Q113 closed, the 41 review rows CHOSEN by Bader, the state line of PROGRESS
- PR 75: Q105 hand of 5 DP2 plots, Q107 worst status, the build memory. PR 78: Q109 the block in the gate, Q110 prototypeFile, decidedByQuestion
- PR 79: R362 boundary walls, the open edge after a rotation override. PR 81: R363 check rows, the chip Assumed word for word by Q108
- These records: Q117 closed, Q118 opened, 4g2 done

Checked:
- Gate green and the three checks passed before each merge, two review passes on each code PR (RAN)
- Python on real inputs: 2,518 plots written, the build proof 0 unexplained on 7 keys, plans equal main outside each PR's change (RAN)
- Walls: 4,796 of 4,901 key walls along a build wall, base within 5 mm on 4,696. Check rows: 7,679 rows, 17 Assumed rows equal a second count (RAN)
- C#: unit twins only. The C# read never reached its 5,948 MiB floor on this PC (RAN, p4g2_fix1_wait*.txt), so real-input parity is Q111

Remains:
- The C# check of Q111 on the laptop. Plot cut rows in 5c (Q117)

Known bugs:
- The build peak after PR 75 is UNKNOWN until Q111. 7 cut plots of the IMC join follow every rule on the whole project until 5c

Next:
- 4g3: Q112 and Q113 first, then R364 and R365. Bader pastes steps/START.md

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.

Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters.
Q114 Fields following the Q017 hand of 2302. Q115 Wall extent along an edge. Q116 Ramp and wall of 18 turned plots. Q118 Follow every rule leaves out moved plots.

## Round ADDIN 4g1, 2026-10-27, Q098, Q099 and the status of each plotted value

Goal:
- Q098 to Q104 as Bader answered, the status of every rule value and of each plotted value

Done:
- PR 69: Q098 to Q104 closed, 4g split into 4g1 and 4g2, the chip and sheet Assumed in 7.7 of the mock
- PR 70: Q098 hand alone table for a cluster unit with no cluster table, Q099 linkName from RRE_ZZ_SP_RVT-Name, Q105
- PR 71: R401 the status block by python/rule_status.py, steps/investigations/status-review.md, Q106
- PR 72: R401 the status of each plotted value, the summary of step 15 and the parity step status, Q107. PR 73 these records

Checked:
- Gate green and the three checks passed before each merge (RAN)
- Parity, whole project and 7 keys, net48 and net8.0: PR 70 128 of 128 lines, PR 72 144 of 144, 0 failures. The proof of PR 70 0 unexplained (RAN)
- Block: 595 values, Bader 3, MEASURED 50, DOCUMENT 273, RECOMMENDED 11, Code 16, UNKNOWN 242, equal by second_check.ps1 (RAN)
- Plan: 105,861 values, 20,026 differ from a build with no override, all with the override status (RAN)
- One pass of the two checkers on PR 69 and 70, two on PR 71 and 72, REAL fixed with tests, LATENT in steps/latent.md

Remains:
- The 5 DP2 plots still get no hand (Q105). Every written plot holds an assumed value. Bader's review list: status-review.md, 41 rows

Known bugs:
- A whole build peaks at 3,262 MiB on net8.0, 1,012 before the status step, the plan 26.2 MB from 15.5 (Q107)

Next:
- 4g2: R362 to R365, Bader pastes steps/START.md

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.

Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters. Q105 Row pair of 5 DP2 cluster units.
Q106 Step values of the schedules as DOCUMENT. Q107 Status detail of values built from others.

## Round AUDIT-RULES, 2026-10-27, AI MAX version 4 and the status of every rule value

Goal:
- The skill refresh and record upkeep, then the status of every rule value, no logic change

Done:
- PR 67: .claude/skills/ai-max is version 4, the 33 log entries before REC moved byte for byte to steps/history/log-before-rec.md, STATE first in PROGRESS
- PR 68: steps/investigations/rule-status-audit.md, Q100 to Q103

Checked:
- Log move: SHA-256 108cc030... on the cut and the new file, the old log equal to the kept part plus it, Q, R of 2 or 3 digits and PR ids 303, 213, 40 both sides (RAN)
- Audit by steps/investigations/rule-status-scripts/audit.py: 316 rules, 128 process, 591 values, Bader 3, MEASURED 35, RECOMMENDED 9, Code 16, UNKNOWN 528 (RAN)
- second_check.ps1 equal. Reviews found statuses by text match, Code by absence and marks not read, fixed in the script. Code sweep 120 rows (RAN)

Remains:
- No output shows assumed values per plot (Q102). R366 not built. The statuses rest on text matching (Q104)

Known bugs:
- Two relative links of the moved log, history/round-2.md and round-3.md, do not resolve from steps/history/, kept byte for byte

Next:
- 4g: Bader pastes steps/START.md into a fresh session

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.

Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters. Q098 Hand of 5 DP2 plots in DP4 clusters.
Q099 Link Name of a build. Q100 Status of values from client documents. Q101 Level profile fields with no share. Q102 Show the assumed values per plot. Q103 Design values in code, not in config.
Q104 Audit statuses rest on text matching.

## Round ADDIN 4f, 2026-10-27, Q089 and steps 8, 10, 12 and 13 in C#

Goal:
- Q089 as Bader answered it, then R358 to R361 with parity on the real data, the records of Bader's brief and addition

Done:
- PR 61 and 62 records: CLAUDE.md, Q089, paths to docs/design/, START.md step 7, usage.md. PR 63 Q089: the build on the footprint clusters alone
- PR 64 R359 levels and R358 floors, PR 65 R360 overrides and R361 parameters, PR 66 these records

Checked:
- Q089: 0 unexplained on all 7 keys, levels 0 at pass, 1 mm and 5 mm, the control route equal to main by SHA-256 (RAN, q089_none.txt, q089_snap.txt)
- Parity net48 and net8.0, whole project and 7 keys: 96 and 128 runs, 0 one unit cases, 0 failures (RAN, p4f_ and p4fb_parity_build)
- Second methods 0 mismatches, proof 0 unexplained, 7 plans byte equal (RAN). Gate: pytest 1825 passed 1 skipped, dotnet 811 and 785 (RAN, p4f_end_gate_python, p4fb_gate_test)
- Reviews: REAL fixed with tests or records, LATENT in steps/latent.md, at most 2 passes per PR

Remains:
- Not in 4f: heightOffset and floor z need the site file of button 1, roadAtFrontM with R364, the past_all.csv rebuild of R385
- Skill refresh skipped, ai-max.skill came to Downloads at 16:35, after the check, PR 1 of AUDIT-RULES. Usage rules of version 4 not followed: round-addin.md, 4f

Known bugs:
- 5 DP2 plots get no hand since Q089 (Q098), a build writes no linkName (Q099), steps/PROGRESS.md open bugs

Next:
- 4g: Bader pastes steps/START.md into a fresh session

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.

Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters. Q098 Hand of 5 DP2 plots in DP4 clusters.
Q099 Link Name of a build.

## Round REC, 2026-10-27, the records diet and Q095 to Q097

Goal:
- Small start reads, no record over 100 KB, nothing lost, token use measured, Q095 to Q097 in the rules, the plan of Part 1. No logic change

Done:
- PR 56 rules in 7 parts with an index, R400 an 8th, usage.md. PR 57 design in 11 parts, round-addin.md 0a to 4e in 15 history files
- PR 58 questions-open.md, Q095 to Q097 closed, Q088 to Q094 in latent.md. PR 59 CLAUDE.md 29,428 to 9,462 bytes, repo-map.md, agents
- PR 60: R400 with test_r400_record_sizes, steps/01_next.md, steps/START.md, PROGRESS 21,479 to 5,419 bytes, CLAUDE.md 9,552 (RAN, wc)

Checked:
- Gate on main at 73b8c42: pytest 1,792 passed 1 skipped, build 0 errors 2 warnings, dotnet test 763 net8.0 and 789 net48 (RAN, gate_main.txt)
- Gate at the end: pytest 1,798 passed 1 skipped, 6 tests new in the round, build 0 errors 2 warnings, dotnet test 763 and 789 (RAN, gate_pr5.txt)
- Every split, copy, moved line and section equal, 0 of 346 R and 94 Q ids lost, two routes (RAN, split_proof.txt, second_route.txt)
- Reviews: REAL findings fixed or open bugs, LATENT in latent.md, wrong claims fixed (READ, steps/runs/rec/reviews.txt)

Remains:
- ADDIN 4f to 6 by steps/01_next.md, one session per phase

Known bugs:
- steps/PROGRESS.md open bugs and steps/latent.md (READ)

Next:
- 4f: Bader pastes steps/START.md into a fresh session

Your turn:
Full text and the recommended answers by ID: steps/questions-open.md.
Q001 El @ Entry rule. Q002 Ramp type by Rev 9 code. Q003 Villa mirror by block flag. Q004 Setback from the DLUS front. Q005 Front level snapped to the road. Q006 Back corners parallel.
Q007 Generic floor on the whole face. Q008 Units, a baseplan with 01b. Q009 Typology_Rev9 for codes with no schedule. Q010 Sales Land Width. Q012 FFL split by units. Q013 CASE second letter as built.
Q015 Yard depth under the floor top. Q016 Plots 2311 and 2312 by baseplan. Q017 IMC 2301, 2302 and IMG 544 as VD4. Q018 58 past ramps listed. Q019 Schedule FFL. Q020 Keep the north stop.
Q021 Parallel windows over 8 GB free. Q022 TEST copies in Zone 02 ARC. Q023 Delete the empty 15a folder. Q024 Curb and ROW from the R94 edge. Q025 13 PDF refusals listed.
Q026 Two curbs of one drawing. Q027 Two DP4 labels in one face. Q029 Location plan number per face. Q030 The 01b sales rows. Q031 Rev 9 faces, 97 differ. Q032 1.4 FFL parameter as design level.
Q033 Lone DT3 on a VL3 face. Q036 TBD for DLUS, accommodation for sales. Q037 Corner fronts by the Orientation side. Q038 Native ramp fronts over the subgrade. Q039 Footprint test as third check.
Q041 Rows 2290 to 2299 as overrides. Q042 Lone duplex hand by row pairs. Q051 Rev 01 values of steps and ramps. Q052 Weak rules held through their overrides. Q053 The override list, native and byHand.
Q054 R260 of DATA-5 and R261 to R282. Q055 1.4 ELl@Entry typed off its ramp. Q056 Native models for hands, fronts, ramps only. Q057 Ramp front fall with no direction.
Q058 Old 20221206 prototypes and their hands. Q059 NBH 03 build on native2022. Q062 Rev 9 attribute values of the probe. Q063 Whole NBH 03 image past 30 minutes.
Q064 Watcher through the job, withdrawn. Q065 zone2 IMC file, the floor scenario master. Q066 No Parsons TU model of Zone 1. Q068 Project position of button 1. Q069 A remade key copy by its hashes.
Q070 El @ Entry with no schedule. Q071 Walls, base, centre and top rule. Q072 Side road of a villa, three edges. Q074 Steps of the base and risers. Q075 Native entries with no value.
Q076 Python checker licences. Q077 The worker beside Revit. Q078 Rounding half away from zero. Q079 Microsoft.NETCore.Platforms pinned to 3.1.0. Q082 Road level of 33 key plots.
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters.

## Older rounds

One line per entry before Round REC, each moved byte for byte to the line named.

- Round ADDIN Part 1, 2026-10-25, the add-in: steps/history/log-before-rec.md line 1
- Round 17, 2026-10-25, the past models of all four sub-phases, the majority rule and the IMC rebuild: steps/history/log-before-rec.md line 53
- Round DATA-5, 2026-10-25, the audit of DATA-4, the missing xrefs, the reader counts, the area key, the IFC and the coverage table: steps/history/log-before-rec.md line 104
- Round DATA-4, 2026-10-25, the Rev 9 parcels in Civil 3D, the VL6 hand in Revit and where the past El @ Entry came from: steps/history/log-before-rec.md line 150
- Round 16, 2026-10-24, links against the CAD footprint, the corner front, two past jobs and a full IMC file from CAD: steps/history/log-before-rec.md line 156
- Round DATA-3, 2026-10-24, the Master Plan Rev 9 folder, El @ Entry by terms, the units on no plot and the location plan DWG: steps/history/log-before-rec.md line 162
- Round Z4, 2026-10-24, a new site file for one neighbourhood from CAD only, compared with the past site model: steps/history/log-before-rec.md line 168
- Round DATA-2, 2026-10-24, the full Civil 3D read, the analysis fixes, the wiring (Part W) and the location plan (Part L): steps/history/log-before-rec.md line 230
- Round DATA, 2026-10-24, the data book of Phase 1: steps/history/log-before-rec.md line 236
- Round 14c Part B, 2026-10-24, floors in the CAD plan: steps/history/log-before-rec.md line 277
- Round 14c Part A, 2026-10-24, villa mirror, CASE and FFL split tested on the data: steps/history/log-before-rec.md line 314
- Round C3D, 2026-10-24, the road drawings in Civil 3D 2026 tied to the plots: steps/history/log-before-rec.md line 351
- Round 14 Part B, 2026-10-24, clusters and levels for the computed plan: steps/history/log-before-rec.md line 394
- Round 14 Part A, 2026-10-24, links from CAD for IMC: steps/history/log-before-rec.md line 430
- Round 15b, 2026-10-24, placer step 2, Generic, yard and ramp floors, eight scenarios in Revit: steps/history/log-before-rec.md line 468
- Round 15a test, 2026-10-24, the test host drives Revit, one prototype per run: steps/history/log-before-rec.md line 514
- Round 13, 2026-10-24, unit codes, road levels, entry level per plot, block to link offset: steps/history/log-before-rec.md line 563
- Round 12b, 2026-10-24, villa blocks from Makkah Building Footprint, unit codes into the clusters: steps/history/log-before-rec.md line 569
- Round 15a, 2026-10-24, plan.json from the past IMC job and the placer step 1: steps/history/log-before-rec.md line 575
- Round 12, 2026-10-24, villa blocks, row split, road fronts, levels, master plan V9: steps/history/log-before-rec.md line 581
- Round 11, 2026-10-24, project settings, cluster derivation, checks 1 and 2: steps/history/log-before-rec.md line 587
- Round 10, 2026-10-24, ROSHN Makkah rules profile and loader: steps/history/log-before-rec.md line 593
- Round 9, 2026-10-23, floor, ramp and link relations: steps/history/log-before-rec.md line 599
- Round 8, 2026-10-23, ramp levels from the real corners, surface roles, floor count, one error line: steps/history/log-before-rec.md line 605
- Round 7 Revit, 2026-10-22, ramp corners, spot elevations, plot numbers, link names: steps/history/log-before-rec.md line 611
- Round 7 road, 2026-10-22, LandXML road surfaces and the ramp level test: steps/history/log-before-rec.md line 617
- Round 6, 2026-10-22, plot number shift, second source, no type check: steps/history/log-before-rec.md line 623
- Round 5, 2026-10-22, Revit link names, shared parameters and floors: steps/history/log-before-rec.md line 629
- Round 4, 2026-10-22, explode, label join, PDF rows, full run: steps/history/log-before-rec.md line 635
- Round 4, 2026-10-22, Revit links, walls and ramps: steps/history/log-before-rec.md line 641
- Round 3, 2026-10-22, fixes against the real files: steps/history/log-before-rec.md line 647
- Round 2, 2026-10-22, readers run on real ROSHN Makkah files: steps/history/log-before-rec.md line 652
- Round 1, 2026-10-22, read-only readers: steps/history/log-before-rec.md line 657
