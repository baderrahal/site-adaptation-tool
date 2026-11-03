# Round log, 4h-1, 4h-2, 5b and FOLD

Moved from steps/log.md in MASS-2NOV-D to keep it under 100 KB (R400).

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

