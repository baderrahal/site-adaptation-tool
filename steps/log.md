# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

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
