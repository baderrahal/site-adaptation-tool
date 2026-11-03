# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## Lane C HELP-3NOV-C, 2026-11-03, i beside each step

Goal:
- Hover line and side box on steps 1 to 9 and New project rows, text from the guide

Done:
- R540: python/build_help.py, help file in the DLL, side box

Checked:
- Touched tests on net48 and net8.0, the pane and New project PNGs read. Not seen in Revit yet

Remains:
- RULES-3NOV-C. Second guide PR when PARAM merges, then rebuild the help file

Known bugs:
- None new

Next:
- RULES-3NOV-C

Your turn:
- Opened none. Closed none.
- 184 open, steps/questions-open.md
## Lane B FIX-3NOV-B end, 2026-11-03, undo in Revit

Goal:
- Finish task 4, the undo check of R499 in Revit

Done:
- steps/investigations/undo-3nov.md. steps/dryrun-3nov.md: the step 1 line (Q415) and the undo line

Checked:
- One undo and one redo on 9, one undo on 3, read in the pane and the journal. Records only, no code

Remains:
- 4 not proven, it stopped on picks.json of the copy. Save prompt after 3, 4, 9 alone

Known bugs:
- none new

Next:
- Known issues, REPORT-3NOV-B, 6b

Your turn:
- Opened none. Closed none. See steps/questions-open.md

## Lane C known issues, 2026-11-03, FIX-KNOWN-3NOV-C

Goal:
- The issues of the guide with a clear answer

Done:
- Pick surfaces under the row (Q403), plain band (Q404), CAD label (Q406), No changes line (Q408), Ramp joint name (Q410), step 8 Boundary walls (Q415)

Checked:
- Touched tests on net48 and net8.0, text and window only, no parity

Remains:
- Not seen in Revit. Step 1 text of Q415 from Lane A. Then HELP-3NOV-C, RULES-3NOV-C

Known bugs:
- Q400, Q416, Q417, Q418

Next:
- HELP-3NOV-C

Your turn:
- Opened Q416 Road row reads optional, step 2 needs it. Q417 Whole project stops at step 3. Q418 Empty project keeps old rules on a set up model. Closed Q403, Q404, Q406, Q408, Q410.
- 184 open, steps/questions-open.md

## Lane C GUIDE-3NOV-C, 2026-11-03, plain guide to the 9 buttons

Goal:
- A plain guide to the 9 buttons for Bader and the team, records only, no code change

Done:
- steps/buttons-guide.md: the table, one part per button and pane row, every file from start to end

Checked:
- Each line against its class or config key and the files of the rehearsal run, READ. The list is in the PR body

Remains:
- Shared parameters row and New project change with Lane D PARAM-3NOV-D, a second small PR when it merges

Known bugs:
- Pane lines say more than is built, Q415. Whole project times not known, Q414

Next:
- Bader's answers. The second PR after PARAM-3NOV-D

Your turn:
- Opened Q414 How long a whole project takes. Q415 Pane lines say more than is built. Closed none.
- 186 open, steps/questions-open.md

## Lane B DRY-RUN 3 Nov, 2026-11-03, the presenter check on frozen main b1a27d9, Lane B on the laptop

Goal:
- Bader: rehearse 1 to 9 on NBH 012 through the real windows, read the shared parameters, GO or NO-GO, steps/dryrun-3nov.md

Done:
- steps/dryrun-3nov.md. Lane B made the kit, none existed: SA-dryrun3nov.rvt from SA-demo-start. C:\SA-demo config and rules refreshed from main, backup in lane_b_check\p3nov_check\backup
- 1 to 9 run twice through the pane. Fallback SA-dryrun3nov-done.rvt with its work folder

Checked:
- Q384 in Revit: a new Revit shows all nine rows as saved (READ the pane). Parameters read in Properties on 3 villas and 3 floors, none wrong
- Inputs same 44 by SHA-256 at 15:12 (RAN), read again before the end

Remains:
- Undo in Revit not proven, keys and the Undo button changed no row. Task 4 checks left. Two plot overlaps merged, Lane A's question. 6b after the restart

Known bugs:
- Level 1 is empty after the build (Q412). Floors hold no RRE_ZZ_SP_ values (Q411). Report Summary gaps (Q307). Pane and report issue counts differ (Q307)

Next:
- Bader's dry run at 17:00. After the restart Bader resumes Lane B: Phase 6b

Your turn:
- Q411 Floor parameters bound but never filled. Q412 Level 1 is empty after the build. Q413 Input picks lost before a real run. 184 open, steps/questions-open.md

## Lane A FIX-3NOV-A, 2026-11-03, a new project runs rev6 and grading files are no roads

Goal:
- Bader: Q373 project mode in the settings, Q371 notRoad list, an Empty project with no JSON edit reaches 4 Check under rev6

Done:
- R484 mode in the settings, on the request of all 7 sites. R485 7 grading files are left out and refused under rev6
- Breaker 1 and my check: an Empty project had no config at all, so it told Rev 06 and could not build. Bader closed Q377, Q378
- R486 the install ships a set, Empty project copies it per model. R487 its override list is empty, R330 gives way under rev6 alone
- Fixed by the breakers: button 4 under rev6 always stopped on the role floorOutlines, a saved past job moved to rev6 on a plain Save, Save overrides refused on an empty list, two models of one name shared a list
- The window before Save: Pick surfaces grey and a false parameter sheet stop, found by a Revit shot, fixed, the line now under Start from

Checked:
- 5 TU zones with the set, RAN: 1,759 of 1,791, the same 32 left out as PR 145, 0 changed. Recount from the plan files equal
- ROSHN saved project, RAN: IMC and whole project byte-equal to main, with and with no mode
- Parity on f225518, RAN: 32 runs, 416 lines, 0 failures, past job plan byte-equal. No Python, plan, CadReader, config or rules change after it
- Gate on 183ef5f: dotnet net48 2,526 and net8.0 2,206 passed at 183ef5f, pytest 2,509 passed, 1 skipped at 59692e9, no Python change after
- Revit on 84e1e69, RAN, 36 minutes under the lock: Empty project, Save setup, 1 dry run, 2, 3, 4 on a copy of NBH 012. Requests hold rev6, plan 336 plots, 0 overrides, inputs 43 of 43 same. One line edited, inputRoot in the project copy, to keep inputs on copies
- Review 1 and window review, breaker 1 and 2

Remains:
- Not seen in Revit: the window fixes after 84e1e69 (Pick surfaces active, Found sheet row, the line's place), Road surfaces window, Apply of button 1. Lane B holds the lock to the freeze. Tests only so far

Known bugs:
- A new site still needs the inputs of this job (Q379). Update from report saves nothing on a new project (Q394)

Next:
- A short Revit pass of the items in Remains after the restart

Your turn:
- Closed: Q371 Seven non road files still offered. Q373 New project does not start under rev6. Q377 Where Empty project gets its config. Q378 Override list of a new project
- Opened: Q379 A new project on another PC. Q394 Update from report on a new project. Q395 A mode in memory lost on reopen. Q396 Past of a native key on new project. Q397 Save As of a new project keeps one list. Q398 The 443 hairline floor overlaps at step 5
- 181 open, steps/questions-open.md

## Lane C 6C-3NOV-C, 2026-11-03, window check, Excel compare, check sheet

Goal:
- Every window against its board in real Revit, the Excel compare of NBH 012, the Part 1 sheet, then Bader's row rename before the 14:00 freeze

Done:
- R505: theme, pane, New project, How it works, What the tool read, Review data, 4 Check differences fixed. steps/investigations/6c-windows.md per window and the rest by owner
- steps/investigations/6c-excel.md. steps/part1-check.md the short form for 16:00, steps/part1-check-long.md the long check for later
- Bader 3 Nov: row Curbs reads Plot fronts (curb lines) with its Read only line, the run input keeps the name Curbs, a key of the step hashes (R366)

Checked:
- Real windows, RAN: 3 runs through the pane on copies, buttons 1 to 9 on NBH 012 in the last two. 1,003 floors, 336 villas, 778 walls, 1,339 Placed, 688 accepted, box 485. 43 inputs the same by SHA-256
- Excel, RAN by openpyxl and by raw XML, the same counts: FFL 324 equal, 12 differ up to 187 mm, 1 missing. Steps 334, 2, 1. Placed against past Unit 334, 2, 1
- Dead buttons: 0 pressed dead. Two Change buttons of New project have no handler and never show
- Review 1 with the UI checklist: REAL fixed or a question, 3 LATENT

Remains:
- The rename and the separator are not seen in Revit, only in the rendered PNG and tests, the lock was Lane B's. Road surfaces never seen in Revit. Update from report filled state not seen, my driver failed twice

Known bugs:
- Q403 Road surface row cut. Q400 New project reopen. Q408 Update silent after a pick

Next:
- Bader's dry run at 17:00, steps/dryrun-3nov.md of Lane B

Your turn:
- Opened Q400 New project reopens on Empty project. Q401 12 plots differ from the past FFL. Q402 Plots 2301 and 2302 read VD4. Q403 Road surface row cut in the pane. Q404 Set up site band shows a code name. Q405 Export to Excel hidden in Rules. Q406 Conflicts Plot column shows the CAD label. Q407 Amber in the spec or the boards. Q408 Update from report shows nothing after a pick. Q409 Two greens and the step name weight. Q410 Rule column shows R471 on joint rows. Closed none.
- 177 open, steps/questions-open.md

## Lane B FIX-3NOV-B tasks 1 and 2, 2026-11-03, step rows survive a reopen and an undo, Lane B on the laptop

Goal:
- Bader: Q384, every button 2 to 9 records its run in the model. Q382, an undo takes the Done state back

Done:
- R498: buttons 3, 5, 6, 7, 8, 9 write in the transaction of the run, 2 and 4, which have none, in a small one of their own
- Warnings, issues after the commit and outputs go in by a second write, one undo entry by a transaction group
- R499: an undo or redo drops the runs of the session, each step reads the entity
- Breaker: the record keeps its outputs under the work folder, so a copy or a Save As of the model is not Done
- Task 3: steps/investigations/two-plot-overlaps.md, the 443 are hairline laps, deepest 1.47 mm. Ramps fit the 1 mm rounding, 71 yard corners do not, cause UNKNOWN. Not run in Revit, sent to lane-a, nothing fixed

Checked:
- Parity 32 runs, 416 lines, 0 failures. Gate dotnet 2,404 and 2,109 passed. Review 1, breaker 2 passes. Fakes only so far: the group and the undo in Revit are task 4

Remains:
- Tasks 3, 4, 5 of the round. Q382 narrowed to step 4 and site.json

Known bugs:
- Override list changes leave steps Done (Q386). An undo of 3 or 4 takes the record back, not the files

Next:
- PR of task 3, hand check of tasks 1 and 2 in Revit, Phase 6b

Your turn:
- Q384 Step rows lost on a reopen, closed. Q386 Override list changes leave steps Done. 166 open, steps/questions-open.md

## Lane D MASS-2NOV-D, 2026-11-03, the mass outline on the outer face of the walls (Q321, Q279)

Goal:
- The mass base is the outer face of the exterior walls, exact corners by line intersections, the slab only where the walls do not close, with a flag

Done:
- steps/investigations/mass-outline.md, two probes. By joins 0 of 13 close. Function does not part house and yard walls, distance and height do. A wall's Location Line does not move its curve, the face is width/2 along Orientation, so the old offset missed. 925 walls of DP1 hold 118 in the main model, the rest design options
- WallRingBuilder in Common: R500 ring walls by distance and height, main model and primary option. R501 face line, an edited profile gives the ground stretches. R502 corners by intersection. R503 short edges, arcs kept. R504 ring or slab flagged. WallStrips and the union gone. Mass files remade once, reader version 5
- 12 of 13 rings, DP3 on the slab with its reason. R477 takes the centre of the area, the mean of the corners gave 14 false rows

Checked:
- Revit, 336 plots, all 13 prototypes. Second method, no code of the builder: 89 of 94 edges on a wall face line, 0.000 mm off, 5 connecting. Mass against link 0.000 mm, 0 degrees, 0 mirror on 336. The shape of a link against its mass was not read
- Breaker 1 found 7 of 12 rings wrong on real data, offline and silent: FFL based walls dropped, up to 430 mm, a recess filled, a mixed option. All fixed. Breaker 2 found the footprint fault, fixed, and no exterior stretch outside a ring. Review 1: values moved to config
- Footprint: main 59 same type, 269 other, 8 none. Now 58, 271, 7, over the limit 0
- Parity: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 lines, 0 failures. Past job plans byte equal on the whole project and IMC. 62 inputs the same by SHA-256, 13 prototypes unchanged by time and size, the first Masses run stopped on 13 .slog (Q338), the second 275 of 275
- Gate on the head: pytest 2,464 passed, 1 skipped. dotnet 2,377 on net48 and 2,087 on net8.0 passed. Parity ran before the merge of PR 151, which adds timing lines. Past job byte equal on the head too

Remains:
- Q390 to Q393. No read job gives a mass outline. The listener of Lane C came after these runs, they went through window_lock with one slot

Known bugs:
- 52 twin pairs overlap (Q391). 13 plots are not the primary option (Q392). Small wall ends past a ring, in the investigation. No arc wall was ever run in Revit, steps/latent.md

Next:
- Nothing queued for Lane D. Masses take 8.7 s each to remake, left to me by Lane C, steps/investigations/speed-2nov.md

Your turn:
- Opened: Q390 A wall modelled inside out, far face kept. Q391 Twin masses overlap along one side. Q392 The primary option is not every plot's condition. Q393 The chosen limits of the wall ring
- Closed: Q279, Q321 by Bader's prompt of 2 Nov
- 166 open, steps/questions-open.md

## Lane C SPEED-2NOV-C, 2026-11-03, Revit runs faster for all lanes

Goal:
- Timing first, a Revit listener, two slots, fixes only where the time goes, the same model after

Done:
- R447 run_timing per button and step. R448 the listener in the test host, job folder, one line to send. R449 two Revit slots, 65,221 MB here
- R496 the pane warns on Revit 2021 prototypes. R497 a Revit .slog of a backup folder leaves the input check outside the input roots, a new file under one fails the run
- Button 7 split: the 57 s were 6 stale masses, 52.3 s, 8.7 s each, once per change, 0 s else. The file is Lane D's, untouched
- No change for warnings 0.1 s, the model refresh of button 5 (both calls needed), transactions, joins, links: steps/investigations/speed-2nov.md

Checked:
- Revit, RAN: whole run 6.8 minutes own run, 2.0 minutes second listener job. Timed buttons 232.6 and 305.5 s against 110.2 s, first job 184.2 s
- Model, RAN against SPB3: floors, place, walls, footprint 0 differences, levels and accepted differ only in element ids, plan body equal. Second method, the Levels workbook: 8,298 of 8,302 cells equal, 4 run stamps
- Real inputs 276, changed 0 on SPM1, SPM2, SPM4
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 416 reports, 0 failures, 16 of 16 plans byte-equal to main
- Review 1, breaker 1 on 3329b5a and on the delta, REAL fixed. Test-first slipped on the first review's R496 only, said in the PR

Remains:
- Listener not run in Revit: a throw inside Revit, past limit and back, killed, hung, findings exit 5, slot 2. R497 and the pane line of R496 in the real pane not seen in Revit

Known bugs:
- None new. Lines in steps/latent.md

Next:
- Lanes send jobs through the listener, one add-in build per machine

Your turn:
- Opened none. Closed none.
- 164 open, steps/questions-open.md

## Lane B FIX-2NOV-B task 4, 2026-11-03, the hand run of main on NBH 012, Lane B on the laptop

Goal:
- Bader's task 4: buttons 1 to 9 by hand on main with PR 139 and R459, read back, compare with PR 139, check Q158

Done:
- steps/fix-2nov-b-handrun.md. Through the real windows on a new model from a named copy, not the test host
- 1,003 floors, 0 refused, 336 masses, 778 walls. Step 5: 688 accepted, 443 overlap and 42 off axis shown, all 443 of two plots
- Q158 in Revit: the dead end of part 4 gone, also after a new Revit. No number worse than PR 139

Checked:
- Inputs same 44 by SHA-256 (RAN). Warnings, accepted file, sheet and Placed read by a second script, 0 mismatches (RAN)
- No code in this record. Gate of the code: PR 146 and PR 148

Remains:
- Q382, Q383, Q384, Q385. Button 6 moving floors with no list, UNKNOWN

Known bugs:
- Accepted warnings return by the first save (Q385). Step rows lost on a reopen (Q384). Chip press shows an empty table

Next:
- Lane D holds the lock since 03:03. Lane C reuses the handler

Your turn:
- Q384 Step rows lost on a reopen. Q385 Accepted warnings return after a save. 164 open, steps/questions-open.md

## FIX-2NOV-A3, 2026-11-03, the split test, the ends of parted walls and notched fronts

Goal:
- Q303, Q304 R414 by steps alone from the high road end. Q265 walls.ends weak measured for parted walls. Q299 the 6 notched plots against their key links

Done:
- R414 steps alone, walls from the high road end, slope reported per unit. It stays off: 43 of 82, 0.5244, under majority.strongShare 0.90
- R481 parted cluster walls read MEASURED weak, source wallOpen, new step field endsSource, the rest as before
- R482 override frontEdge names the front edge. 6 lines, plots 1553, 1172, 2626, 2665, 727, 469. Insert lines of the old front gone. Level gaps of 5 plots measured on the true front, 16 no value lines gone, heightM of pair 469 and 470 -0.004, overrideSets frontEdge

Checked:
- R414 on the 5 TU keys, RAN: clusters 8 to 43 of 82, key splits 0 to 3 of 41, key whole 40 of 41. On 37 of the 41 key splits the whole cluster already fits, no wall is tried
- Second route, RAN: 82 of 82 marks equal. Q304 holds on 13 of 13 uneven key splits
- Plan against main 69775f8, past job, RAN: 2,518 to 2,524 written, only the 6 new and 470 differ. Missing 236 to 233. Walls 5,999 to 6,029, ends MEASURED weak 0 to 1,528, UNKNOWN 5,999 to 4,501, ends values equal
- Notched plots against key, RAN: rotation and insert within holds on 6, FFL, entry and steps equal on 5, second way 48 match 0 mismatch. 1553 is SE, levels left out
- Parity on c91c1fa, RAN: 13 steps, net48 and net8.0, whole project and 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures
- Gate on aa15d7d, RAN: pytest 2,448 passed 1 skipped, net48 2,218, net8.0 1,933, 0 failed
- Review 1: no code fault. Breaker 1: 3 REAL on R482, fixed. Breaker 2: stale outline and steps lines, the hand in the set, fixed, slope of 1172 is Q375

Remains:
- UNKNOWN: why the keys split clusters that fit. No full majority.py run, its inputs not rebuilt, the lines are by hand and measured by its method, 3,646 other lines equal
- No Revit run, none needed. Latent lines in steps/latent.md

Known bugs:
- Plan ramp of 1172 falls 0.658 percent against 2 in the key (Q375)

Next:
- FIX-3NOV-A, Q373 and Q371

Your turn:
- Closed Q265, Q299, Q303, Q304. Opened Q375 Ramp type after a rampFrontM gap, Q376 Plain words for the front edge override. 162 open, steps/questions-open.md

## Lane B FIX-2NOV-B Q158, 2026-11-02, button 1 with nothing different marks the step, R374, Lane B on the laptop

Goal:
- Bader's answer of Q158: a run of button 1 where nothing differs counts as Done, and the dead end of part 4 of steps/check-2nov.md goes

Done:
- Apply is on when nothing differs, one plain line says so, a press sets no element and records the step in the Settings entity
- site.json is written when missing and also when it disagrees with the model, wider than the answer, CHOSEN: breaker found a stale file went green
- Callers: SetUpRun.StepHolds by Confirm, SiteFileAgrees and SameFacts by StepHolds and Apply, SameSetup by StepHolds, ReadingLastRun by SetUpSiteBody,
  DocumentSession.LastRun by PaneViewModel.LastRun and SetUpSiteBody, StepRun.FromSettings by DocumentSession.LastRun

Checked:
- Review 1: CHANGES NEEDED, 3 REAL fixed. Breaker 1: 3 REAL fixed. Second breaker pass: 0 faults of the fix, 1 REAL older gap added to Q382, 3 LATENT
- Gate: see the PR. Not run in Revit, the hand run of task 4 checks part 4 again

Remains:
- Q382 and Q383. The hand run of task 4

Known bugs:
- Ctrl+Z of a real press of button 1 leaves step 1 Done (Q382)

Next:
- Install main, refresh C:\SA-demo\config, the hand run of task 4

Your turn:
- Q382 Ctrl+Z of button 1 keeps step Done. Q383 A tick on a held step reruns steps. 165 open, steps/questions-open.md

## Lane C FIX-2NOV-C-CHECK, 2026-11-02, the Lane C items of steps/check-2nov.md

Goal:
- Bader's priority before the speed fixes: the pane, 4 Check, the Save As, the read problems line, the crash, the sheet faults

Done:
- R494: a project open brings the pane back, a ribbon button Show pane, a family or a link does not bring it back
- R495: 4 Check sorts by any column, a Field list and a Find box, a hidden row keeps its Use. Plural by count, 1 plot, 1 override, 1 issue
- Save As of What the tool read owned by its window. The read problems line says named colours only when all are, else counts entries
- Sheet: parts 3, 4, 5, 7 corrected, 17 to 20 new, Lane A's R483 steps, question list rebuilt

Checked:
- Revit, RAN: close to Home and reopen brings the pane back, X then Show pane brings it back. 217 on the Road Layout are 156 DIC and 61 RAL entries (RAN, layers.json)
- Review 1 with the UI checklist: CHANGES NEEDED, the REAL findings fixed. Tests of the review fixes were written with the fix, not seen failing first, except the ones named in the PR

Remains:
- Not run in Revit: 4 Check sort and filter on NBH 012, the Save As, Show pane on Home and in a family document. Parts 17 to 19 of the sheet are their first check
- The crash: not reproduced, no session can try by hand, part 20 for Bader (Q381)
- The first Revit pass pressed Always Load on 4 add-in prompts by Lane B's driver, whether Revit keeps that is UNKNOWN, the registry was not read

Known bugs:
- None new

Next:
- SPEED-2NOV-C

Your turn:
- Opened Q380 The save dialog and the input folder write time. Q381 The crash on Browse For Folder. Closed none.
- 163 open, steps/questions-open.md

## Lane B FIX-2NOV-B task 3, 2026-11-02, step 5 accepts the known overlap, R459, Lane B on the laptop

Goal:
- Bader's task 3: one shared handler of known warnings, step 5 first, each accepted one counted, in the report and the 4 Check counts

Done:
- R459: generic with yard or ramp and ramp with yard of one plot are taken without showing (Q367), all else shows as before
- Callers: FloorsRun.Apply gives the list by RunContext.AcceptKnownWarnings, WarningCollector.Decide calls KnownWarningList.Decide, ButtonFlow calls
  ButtonBody.AfterCommit, FloorsRun.RecordAccepted writes the file, CheckRun.AcceptedCount the chip, ReportRun.AcceptedFile the sheet Accepted warnings
- Q158 and Q367 closed by Bader's answers. The Q158 fix is the next PR

Checked:
- Review 1 approved, 0 REAL, 6 LATENT. Breaker 1 found 5 REAL, all fixed with a test, the rest in steps/latent.md
- part7\warnings.html read a second way (RAN): 1,402 warnings, 2 elements each, listed pairs at most 1,303, so at least 99 always show
- Gate: see the PR. No run in Revit yet, every Revit behaviour UNKNOWN until task 4

Remains:
- Q158 PR, then task 4 by hand: the count still showing on NBH 012, a warning coming back at step 6, the text padding

Known bugs:
- The Revit box stays at step 5 for overlaps of two plots, so Q324 stays open. Breaker's estimate on the export is about 611 showing, by id order, not a read

Next:
- Q158, install main, the hand run. Lane C reuses the handler by RunContext.AcceptKnownWarnings and KnownWarningList.Parse

Your turn:
- Q158 Step 1 Done on a model already set up, closed. Q367 Which overlap pairs step 5 accepts, closed. 160 open, steps/questions-open.md
## Lane D FIX-2NOV-D, 2026-11-02, several road surfaces for a new project, no Revit

Goal:
- A project names several finished road surfaces in order, with no JSON edit, and a run that loses a zone stops (Q364, Q256)

Done:
- R490 designBasis.roadLevel.surfaces in order, the old surface a list of one. R491 the existing first surface loop, no second copy. R492 the window Road surfaces in New project and the pane, the pick through the read and the build. R493 the stop, noRoadStopShare 0.10 with its why
- steps/investigations/2nov-d-road-surfaces.md. 1,759 of 1,791 key plots written, against 324 as the config stood. 32 left out, the same 32 as the past job rules: 30 with a corner on no surface, 2 with no front frame. 5 of them are notched plots of Q299
- The scan reads no road surface (Q290), so the list of the window is the LandXML files of the road folder (CHOSEN). Three old log entries moved to steps/history/log-addin-4g2-4g3.md (R400)

Checked:
- On all 1,759 FFL, El @ Entry, steps and ramp corners equal the past job rules, 0 differ, plain code. Past job plans byte equal 11 of 11, on main at 015a253
- One surface on the 5 zones: IMD, IME, IMF, IMG and the whole project exit 2 with the message and no plan, IMC exit 0, it holds all its plots. The C# worker on IMD the same line on net48 and net8.0
- Parity: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 2 more on the old key, 34 runs, 442 lines, 0 failures. 62 inputs the same by SHA-256
- Review 1 and breaker 1 found 5 REAL faults, fixed with tests: the pick outside the input hash, a surface not taken offered, the stop blaming a surface for a plot with no front, a surface left out stopping 4, a pick surviving Start from. Review 2 APPROVE on what it reached, 25 calls
- Gate: dotnet 2,077 on net48 and 1,810 on net8.0 passed. Pytest 2,371 passed, 1 skipped, 1 failed: test_r401_q109, the status block after Q256 closed. Written by rule_status.py, that file and 2 more then 75 passed, the full pytest not run again after that one line, CI ran it

Remains:
- 7 non road files still offered (Q371). A new project does not reach rev6 by any window (Q373), so the road pick is grey on the shipped config. No Revit run of the window

Known bugs:
- Q371 and Q373 above. The rest in steps/latent.md

Next:
- Round MASS-2NOV-D, queued by Bader

Your turn:
- Opened: Q370 Approve the road surface window draft. Q371 Seven non road files still offered. Q372 A road pick in the pane, when it is kept. Q373 New project does not start under rev6. Q374 The stop looks at the zone, not the neighbourhood
- Closed: Q256, Q364 by Bader's prompt of 2 Nov
- 161 open, steps/questions-open.md

## FIX-2NOV-A ids, 2026-11-02, the element of a row of Placed (CHECK-2NOV part 4)

Goal:
- Lane B's item: Element id of the sheet Placed stood empty on all 1,339 rows, so a row could not be traced to its element

Done:
- R483: button 9 reads the id of each tagged link and floor with its tags, revitCounts holds them by plot and kind, Placed writes them
- One id as a number, the ids joined where a person copied an element, empty where none stands
- Update from report and update_check.py no longer list a filled cell, R402 text and two design docs follow
- report_excel_check.py holds each cell against the ids of the counts file

Checked:
- Gate on 8e611bb, RAN: pytest 2,329 passed 1 skipped, net48 2,000, net8.0 1,755, 0 failed. R310 failed once on net8.0 under load in the first gate, then 3 of 3 alone and 0 failed in the second
- Second way, RAN: worker on the whole project plan with made up ids, 9,753 Placed rows, net48 and net8.0, 538,285 cells match, 0 mismatch. One id planted wrong gives 1 mismatch, that row. 8 of 8 inputs unchanged by hash
- The R402 test failed on the old Update code, listing each filled cell, RAN
- Review 1: two stale design doc lines, fixed. Breaker 1: the two Python routes above, fixed

Remains:
- UNKNOWN: real ids in Revit. Lane B holds Revit, the re-check is its call. Not installed by Lane A
- The Conflicts list shows the CAD label: READ, ScanView of Lane C, passed by Lane B. The pick lands on the right plot by that label

Known bugs:
- Two latent lines in steps/latent.md

Next:
- Back to FIX-2NOV-A3, its helpers are done

Your turn:
- Opened none, closed none. Open count as in steps/PROGRESS.md, steps/questions-open.md

## Lane B CHECK-2NOV, 2026-11-02, the parts of Bader's sheet through the real windows, Lane B on the laptop

Goal:
- Bader's round: run parts 2 to 13 of steps/bader-fix-1nov.md on copies, pass or fail against each Look for, close Revit at the end

Done:
- steps/check-2nov.md: 8 parts pass, 5 fail, on add-in dbda940. PR 139, PR 141 and PR 142 are not in this check, parts 14 to 16 not run
- Q308 file deleted after its hash matched, C:\SA-demo\config refreshed from main with 4 demo paths kept
- R458 Revit slog leaves step hash. Caller: Where in InputHash.OfFolder, reached by InputHash.Of and InputProbe.FolderSha256. Test R458_a_revit_slog...
- steps/demo-check.md: the wrong lines on hidden buttons taken out, the R446 change noted

Checked:
- Inputs: same 44 files by SHA-256 after each part (RAN). Folder C:\SA-demo\inputs write time moved at 18:41:24, no file left, cause UNKNOWN
- Part 11 cause: 13 .slog files timed inside button 7 (RAN). Gate: see the PR. R458 not run again in Revit

Remains:
- Fails sent to their lanes by message. Q158 decides the second run of part 4

Known bugs:
- Part 10: no pane after a reopen in the same Revit session (Lane C). Part 4: Element id empty in Placed (Lane A)
- Our fault: an Error Report written outside the run folder, part 9 unsaved for 19 min, see steps/check-2nov.md

Next:
- FIX-2NOV-B: the shared handler of known warnings at step 5, then the hand run of main

Your turn:
- No question opened or closed. 158 open, steps/questions-open.md

## Lane C parameters, 2026-11-02, the other 12 bound parameters on the sheet

Goal:
- Bader's task: list the other 12 bound shared parameters in config/shared_parameters.xlsx with Keep No and a Note

Done:
- 12 rows of Keep No with a Note, a new column Note, the 11 kept rows unchanged. Set up site now says 11 parameters, 12 left out
- R461 and part 6 of steps/bader-fix-1nov.md say so

Checked:
- 22 bound in the read KC2, less the 10 link parameters on the sheet, 12 (RAN, read by script from testhost_KC2_parameters.json). Readers: ParameterBook, XlsxSheet and parameter_sheet.py give the same 11 and 12 (RAN)

Remains:
- The 12 come from the IMC key alone, the other 6 keys were not read for group and spec (UNKNOWN)

Known bugs:
- None new

Next:
- SPEED-2NOV-C

Your turn:
- None opened or closed. 154 open, steps/questions-open.md
## FIX-2NOV-A2, 2026-11-02, the walls against the keys and TH1-IM, Lane A on the laptop

Goal:
- Bader's FIX-2NOV-A2 from section 4 of steps/investigations/6b-data.md: wall lines about 100 mm off the keys, walls on one side only, the villa type on 18 plots

Done:
- Q360: no tool line changes. Key walls on a centre code stand within 10 mm of the tool line on 1,791 of 1,865, on a face code half their thickness off. R478 Wall compare by wall centre in zone_compare, best side only, as the reads hold no side (Q365)
- Walls one side: R480 C10 side wall by hand, 229 of 250 C10 side edges now equal the key, the rest questions: 77 garden walls (Q259), 26 structural walls of the SE keys and 37 back edges (Q358), 12 beside a VL6 (Q359), the SE keys' near span (Q366)
- R479 One villa type by prototype file: 12 of 18 TH1-IM plots match. The 6 are ima 1667, 1668, 1859, 1860, imb 2176, 2177, ARC in the tool, STR in the key, one villa. The footprint check takes the unit of the linked file

Checked:
- Worst wall of a plot within 10 mm, RAN on the 7 zones, tool on key: TU 471 to 1,232 of 1,759, all 653 to 1,344 of 2,466. Key on tool: TU 670 to 1,129, all 813 to 1,269. With R480 by the same compare tool on key 1,339, over 300 mm 54 to 63, as the compare tests wall ends only and a whole wall over an open span ends on a corner
- C10 side edges against the key, RAN by shapely on the written ends: 229 equal of 250, the 21 the measure lists. Plot fields beside walls and yards equal to main
- Past job against main, RAN: the 7 keys and the whole project differ only in the step walls and the walls of the plan
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures, on 23779c7, the changes after it are the compare and the footprint check, in no step
- Gate, RAN on ae8369b: pytest 2,327 passed, 1 skipped. C# net48 1,998, net8.0 1,753, 0 failed. Review 1 CHANGES NEEDED, breaker 1: 3 REAL fixed (end overshoot, keo_file, R480 tests), the side of a wall and the SE keys are questions

Remains:
- Reading Wall.Flipped from a key settles Q365. Not run in Revit, R480 walls of two parts are placed by the same code as wallOpen parts

Known bugs:
- A breaker helper's kill loop may have ended 4 grep processes of another session at 16:49, told to the lanes

Next:
- Queued FIX-2NOV-A3: the split test, walls.ends, notched plot fronts. Bader answers Q357, Q358, Q359, Q365, Q366

Your turn:
- Opened Q357 Key walls with the face on the line. Q358 Walls only the keys hold. Q359 A C10 side wall beside a VL6. Q365 The side of a key wall. Q366 C10 side walls of the SE keys.
  Closed Q360 Wall ends about 100 mm off the keys
- 158 questions open, full text: steps/questions-open.md

## Lane C sheet, 2026-11-02, Lane A's checks on Bader's sheet

Goal:
- Put Lane A's steps of FIX-2NOV-A on steps/bader-fix-1nov.md

Done:
- Parts 14 to 16 written, the question list rebuilt with Q351, Q353 to Q356, Q360 to Q364
- Added to Lane A's steps: a named copy of the start model, button 6 before 7, and what to do if 7 says footprint not measured (CHOSEN, READ PlaceVillasRun.cs)

Checked:
- READ OutlineDistance.cs for the run line, steps/log.md for the counts. RAN the list script, its count against steps/questions-open.md

Remains:
- Parts 14 to 16 have not run by hand. Run at 7 again after Apply has not run

Known bugs:
- None new

Next:
- Bader's sheet

Your turn:
- None opened or closed. 154 open, steps/questions-open.md

## FIX-2NOV-A, 2026-11-02, the yards, the plots of Review data and the footprint check, Lane A on the laptop

Goal:
- Bader's FIX-2NOV-A, from Lane B's hand run: floors that overlap, 379 plots against 337, all masses flagged over the footprint limit

Done:
- R475 Yard stays inside its plot: each yard cut to its plot and its ramp by Clipper2 and pyclipr on the mm grid, a cut yard reads yardCut, RECOMMENDED. Bader chose plan faults only (Q350): generic floors keep the whole plot, as in the past models, so overlaps inside a plot stay. BSL-1.0 allowed by name (Q352)
- R476 Review lists the plots of Check: the 42 are land, open space, sports, mixed use, a masjid, schools, with no official number and no villa type
- R477 Footprint check by same type: a block of another type is listed apart (Q277), same type held to 1,200 mm (Q351). The check compared each mass with a house the master plan drew, of another type on 271 of 336 plots, and even the same type stands 147 to 1,121 mm off
- Cause of the 70 m2 yards: a link override moved the yard with the link (2543, 2546, 2008, 2005, 865), a width group fitted on one wider plot (1761, 1737)

Checked:
- Floor pairs by a 3D count at 150 mm, RAN: NBH 012 1,356 to 1,082, whole project 9,386 to 7,409. Yard area past its plot NBH 012 187.25 to 0.87 m2, whole project 589.46 to 5.31 m2, largest 70.01 to 0.02 m2
- Revit run A2NV of buttons 1 to 5 on a new NBH 012 model, RAN on e94077f: 1,003 floors placed, 0 refused, overlap warnings 1,402 to 1,131, 42 off axis (Q356). The source yardCut came after it, no outline changed since. No yard read back from the model
- R476 on the real table, RAN: NBH 012 379 to 337 rows, 013 466 to 410. R477 on the last hand run, RAN by C# and a second script: flagged 336 to 8, all 8 no outline, 57 same type 0 over, 271 other
- Past job against main, RAN: the 7 keys and the whole project differ only in yard fields (outline, status, and on 4 plots the rule step), Python and C# 2,199 yards equal
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures, on c57fd1b
- Gate, RAN on c57fd1b: pytest 2,307 passed, 1 skipped. C# net48 1,979, net8.0 1,734, 0 failed. Review 1 APPROVE, breaker 1 and 2: 2 REAL fixed, the rest questions and latent lines

Remains:
- Step 5 accepts the overlap warning: Lane B's item L2NOV-1. Other type plots are not held (Q353). A built model keeps its old yards (Q354), build again from a new model

Known bugs:
- A Wrong on 2312 of NBH 012 never reaches Check (Q355). Mass checks of TH1-IM in the next round (Q238)

Next:
- Queued FIX-2NOV-A2: wall lines (Q360), walls one side, TH1-IM. Bader answers Q351, Q353 to Q356

Your turn:
- Opened Q351 The limit of the footprint check. Q353 The footprint check on other type plots. Q354 Yards of a model already built. Q355 A Wrong on a plot of two numbers. Q356 The off axis warning of a cut yard.
  Closed Q350 Floors edge to edge. Q352 A Boost licence package
- 154 questions open, full text: steps/questions-open.md

## Lane D 6B-DATA-REV6, 2026-11-02, what a new project gets on the 5 TU zones, no Revit

Goal:
- Rerun the zone compare in rev6 with no override list, beside the past job, and say how close a new project gets

Done:
- zone_compare.py takes --rev6-plan, 3 tests (R469). steps/investigations/6b-data-rev6.md. No civil entry file in the data book, so road level plus slope on every plot
- As the config stands rev6 writes only IMC, 324 of 1,791 key plots. Beyond the prompt, a config copy per zone names its largest surface (CHOSEN): 1,657 written
- Over limit on the plots written, rev6 and past job rules equal:
- IMC, 324 plots written of 324: FFL 230, entry 162, ramp level 140, steps 0
- IMD, 400 of 403: FFL 238, entry 197, ramp level 173, steps 0
- IME, 316 of 320: FFL 171, entry 126, ramp level 110, steps 0
- IMF, 438 of 476: FFL 272, entry 211, ramp level 128, steps 9
- IMG, 179 of 268: FFL 81, entry 73, ramp level 58, steps 0
- In plain words: FFL within 5 mm on 29 to 55 percent per zone, within 100 mm on 92 to 99. El @ Entry 47 to 60 and 92 to 100. Steps the same on 1,595 of 1,657. The gaps are scatter, the median near zero except IMC at -20 mm, no steady offset

Checked:
- By a script outside git, plain json and math, with no function of zone_compare.py (RAN): every status of the rev6 plans worked out again, 16,570 the same, 0 differ. The 50 random plots read again from the raw plans, 250 cells the same, 0 differ. The plans of the past job and the keys are those of steps/investigations/6b-data.md, checked there.
- The IMC plan really is rev6: it holds the proposal field and summary.noDoorLevel, no yard floors, 23 walls differ in their ends, 92 coordinates. Levels equal on 1,657 of 1,657, ramp corners too
- 62 inputs the same by SHA-256, the repo config unchanged, each config copy differs by mode and surface only
- Reviewer APPROVE, 2 latent. Claim-checker: no claim wrong, 3 wordings fixed
- Gate: dotnet 1,939 and 1,694 passed. Pytest 2,289 passed, 1 failed: test_r62 did not list zone_compare.py among the files that name the mode, added, and its one line changed. Then test_project_profile, test_zone_compare and test_records: 38 passed

Remains:
- Which surface a new project of many phases names (Q364, Q256). The cluster proposals were not compared. Q070 clouds El @ Entry on IMC and IMD

Known bugs:
- None new in the tool. A rev6 run that writes no plot ends with exit 0 (Q364)

Next:
- The Revit half of 6b. Q339 as answered, to build

Your turn:
- Opened: Q364 One named road surface and several zones
- 149 open, steps/questions-open.md

## Lane D 6B-DATA, 2026-11-02, each zone against its past model, no Revit

Goal:
- For every zone compare the plan of the tool with the past site model, from the data book and the key reads only

Done:
- python/zone_compare.py and its 19 tests (R469). steps/investigations/6b-data.md with the tables, worst 10 and 10 random plots per zone
- Every zone has reads, none left out. Over limit per zone, with the list, then by the rules alone:
- IMA (SE), 419 plots: walls 7 and 15. By the rules mirror 8, rotation 8. No level reference
- IMB (SE), 288 plots: walls 9 and 19. By the rules mirror 3, rotation 3. No level reference
- IMC (TU), 324 plots: entry 113, ramp level 43, walls 9 and 36. By the rules FFL 230, entry 162, mirror 12
- IMD (TU), 400 plots: FFL 2, entry 145, ramp level 53, walls 11 and 44. By the rules FFL 238, entry 197, mirror 2
- IME (TU), 316 plots: ramp level 130, walls 4 and 12. By the rules FFL 171, entry 126, mirror 8
- IMF (TU), 462 plots: ramp level 183, walls 9 and 21. By the rules FFL 282, entry 222, mirror 2
- IMG (TU), 257 plots: ramp level 100, walls 5 and 20. By the rules FFL 122, entry 112, mirror 21

Checked:
- Second method, plain json and math: every status of all zones the same, 0 differ. 70 random plots against the raw K read, 70 the same. Key plan against past_all.csv: only the El @ Entry of the schedules and 2 IMD FFL plots differ, 260 rows in conflicts.csv
- 64 inputs the same by SHA-256 before and after, the 7 copies the same as key_copies.json
- Reviewer one pass, its findings fixed or latent. Claim-checker: 2 figures corrected, the wall share and the mirror total
- Gate on the merged tree: dotnet 1,939 and 1,694 passed. Pytest full run 31 failed and 3 errors, nearly all process start failures 0xC0000142 under the load of the lanes, and the log over its cap. The four files alone: 115 passed, 1 failed, the log size, fixed by moving four old entries to steps/history. Then test_records and test_zone_compare: 25 passed

Remains:
- The 6b shares of steps/01_next.md still need their Revit runs. Insert position was not asked for and not compared. Rev6 not compared (Q361)
- The Lane D worktree lost its git entry on 2 Nov: only its own entry restored by hand, no prune. Lane C helper entries show the same stripped form, cause UNKNOWN

Known bugs:
- None new in the tool. Faults of zone_compare.py itself, each fixed with a test: a rounded limit in its report, the plots of a build with no list, and a plan with no wall read as a match, which no real plot reaches. Two latent in steps/latent.md

Next:
- The Revit half of 6b. Q339 as answered, to build

Your turn:
- Opened: Q360 Wall ends about 100 mm off the keys. Q361 FFL rule far from keys without overrides. Q362 Chosen limits of the zone compare. Q363 Plot 2302 and two IMD FFL plots
- 148 open, steps/questions-open.md

## Lane C answers, 2026-11-02, Bader's answers of 09:20

Goal:
- Record Bader's answers, send each lane its own, show the drafts of Q286 and Q293, move the folders he accepted

Done:
- 20 questions moved to steps/questions-closed.md, each lane told by one message
- Q350 is not added here: Lane A closed it on fix/2nov-a, not yet on main, and adds the overlap clause of today to its row
- steps/ui-shots/drafts-for-bader.md: 7 drafts beside the Revit shots. The Revit shots hold real coordinates, so they stay in the work root, the page links them
- 4 folders moved to C:\_cleanup-1nov, 40 now, none deleted
- rule_status.py --write on the answers: 10 values of Q224, Q227, Q243, Q325, Q328 go from RECOMMENDED to CHOSEN by Bader, status lines only. The first gate failed on the stale block (RAN, test_r401_q109), three answer rows now name their key so the tool does not fall back to another source

Checked:
- 164 open before, 144 after, of them Q286 and Q293 waiting, as Bader's 142 others (RAN). Lane A and Lane B folders still read their branches after the moves (RAN). Every shot of the page is on disk (RAN)

Remains:
- No Revit shot of Door level from: ROSHN runs roshnPastJob and the row shows under rev6 only (R443). Revit shot of the first open pane is from 5a, 29 Oct. The Review data shot is older than PR 132 and PR 133
- The 20 answers are recorded, not built. The owning lanes build what they call for

Known bugs:
- None new

Next:
- Bader's look at the drafts

Your turn:
- Closed: Q187 Prefill of a new project from config, J16. Q201 Data book files on a PC with no Python. Q202 The schedule folder in config. Q224 The mode the ROSHN config runs. Q227 Civil file format and plot match. Q237 Folder of the Rev 9 workbooks. Q243 Layer pairs for plot outlines in the scan. Q254 The order and share of villa type sources. Q281 Board rows against the nine pane inputs. Q282 Where the project name is kept. Q296 A saved project holds no drawing path. Q308 The stray export in the demo inputs. Q309 Refresh the demo config and add-in. Q320 Keep the solids path of light mode. Q322 Mass files made inside button 7. Q324 The Revit warning box at step 5. Q325 A file that states no coordinate system. Q328 Default source of the site position. Q335 A parameter sheet picked in the pane. Q339 A 2021 file in the link folder
- 144 open, steps/questions-open.md

## Lane C part 11, 2026-11-02, Links mode on Bader's sheet

Goal:
- Fill part 11 of steps/bader-fix-1nov.md from Lane D's steps, after PR 134

Done:
- Part 11 written, question list rebuilt with Q337 to Q339, HEADSUP refreshed
- Corrected from Lane D's steps: the key is villaModeDefault, the row Prototypes of the pane prevails over pastJob.upgradedPrototypeFolder, each pass takes a fresh copy of SA-demo-start.rvt (CHOSEN, Lane D ran from the template)

Checked:
- READ CheckRun.cs and BuildSteps.PrototypeFolder for the folder. RAN rvt_info.py on the C10 file of both folders: format 2021 in C:\SA-demo\inputs\Prototypes, 2024 in prototypes_2024. RAN a count of 13 .rvt in prototypes_2024

Remains:
- Part 11 has not run through the pane, only Lane D's test host runs. Mass file remake at step 11 read from MassFiles.State, not run, its time through the pane not measured. Starting from a copy of SA-demo-start.rvt UNKNOWN, Lane D's runs started from the template

Known bugs:
- None new

Next:
- Bader's sheet

Your turn:
- None opened or closed. 164 open, steps/questions-open.md

## Lane D D3, 2026-11-02, Links mode on the real prototypes (Q275)

Goal:
- Link the 13 real KEO prototypes where the masses stood by config alone, prove it on NBH 012, prove the swap back

Done:
- 13 of 13 prototypes upgraded to 2024 under the work root, one window each, 286 to 368 s (RAN, reads D3 upgrades.txt)
- Q275: the cause was the R466 fault, no new limit. The upgrade step before the run is Run-TestHost -Upgrade
- R467 a buttons run names linkFolder for its config copy. R468 the copy is saved after a button that changed it. The R405 tests run alone (extra, Lane A and Lane C each saw one failure, cause likely, not proven)
- D3L, NBH 012, linkFolder prototypes: buttons 1, 2, 4 to 9 committed, 3 cancelled by the test host seam. Seconds 1.1, 32.9, 0.6, 18.4, 14.7, 3.6, 84.4, 2.9, 3.2. Private MiB 1909, 2050, 2105, 2120, 2328, 2385, 6164, 6248, 6251. Run 5.4 minutes, peak 6261 MiB, least free 28.39 GiB
- Links per prototype: C10 15, DP1 34, DP2 11, DP3 10, DP4 30, DT1 44, DT3 68, TH1 16, TH3 16, VD2 29, VD4 29, VL4 15, VL6 19, total 336

Checked:
- Saved D3L model read from its bytes by rvt_info.py: 13 links, all in prototypes_2024. D3S: 13, all in masses
- Read jobs D3A and D3B: real link against mass on 5 plots, 2316 DT3, 2360 VD2, 2459 C10, 2490 TH1, 2495 DP1, and on all 336: 0.000 mm, 0 degrees, 0 mirror differences. This holds the link placements, not the geometry inside the files
- 13 sources and SA-demo-start.rvt the same by SHA-256 after all runs. D3L 275 inputs, 275 unchanged. Saved in reads D3 final_check.txt
- Reviewer one pass: APPROVE, 6 latent in steps/latent.md, R468 text set to the code. Claim-checker: no WRONG, its UNBACKED outputs now in final_check.txt

Remains:
- D3L started from the template, not a copy of the demo model, and the swap back ran on a new project, both by the guard of Q323 (Q337)
- D3S ended exit 1 on 13 .slog files in the _backup folders of the 2024 copies (Q338)
- A first keyCopy attempt was refused in 1.6 minutes, a read pair stopped on my wrong -Code before Revit

Known bugs:
- A stop reason set after the run file is written is missing from that file (Q338). 1402 lines of Highlighted floors overlap in each run, not looked at in D3

Next:
- Part 11 of steps/bader-fix-1nov.md by Lane C

Your turn:
- Closed: Q275 First log step limit of an upgrade job
- Opened: Q337 Swap back in the same model unproven. Q338 Input guard counts Revit .slog files. Q339 A 2021 file in the link folder
- 164 open, steps/questions-open.md

## Lane C FIX-2NOV-C, 2026-11-02, no dead end in the windows (R446)

Goal:
- Bader's queued prompt, the window findings of steps/demo-check.md, UI only

Done:
- 3 Review data: Close in a foot, the ask Save your review? on the X and Close, the side box scrolls, Team check keeps Not checked whole
- No idle button: Change and View of New project hidden. Zoom and Export of Check and Review data were hidden already by R444 (READ shots pB-030, pA-012), the driver of Task 6 invoked them through UI automation, demo-check.md corrected
- 9 Report: Run complete, the file name and Open folder in the box
- Export of What the tool read refuses the input folder, its dialog opens in the work folder (Q308, the stray file stays Bader's)
- 4 Check: Buttons.Run unticks Dry run before it, one line says why (Q195 closed)

Checked:
- R446_WindowFixTests.cs. The sweep proved by putting an idle button back: it failed naming New project: Change, passed once restored (RAN). The gate of this PR: see the PR

Remains:
- Check has no Close button, only Review data was asked. Part 11 of the sheet waits for fix/1nov-d3

Known bugs:
- R446 has not run in Revit, part 13 of the sheet checks it. The tests sit on net48 with the other window tests, net8.0 runs the rest of the gate

Next:
- Part 11 when fix/1nov-d3 merges

Your turn:
- Closed: Q195 Dry run of button 4. 162 open, steps/questions-open.md

## Lane C FIX-1NOV-C, 2026-11-01 and 02, Save setup writes the model, window check, cleanup, Bader's sheet

Goal:
- Bader's prompts: Q280, a window check after the lanes' rounds, HEADSUP for all lanes, steps/bader-fix-1nov.md with every open question, the folder cleanup

Done:
- R445: Save setup writes the Settings entity at once by SetupWrite.Write and SettingsStore.Save in one transaction, New project opens through the external event NewProjectHandler, an undo or redo reloads the pane settings (READ docs/rules/12-lane-c-r430-up.md)
- Window check: 11 windows rendered by UiShots and held against steps/ui-shots. One issue, the head Confirmed of Review data ran into Area m2, fixed in Theme.xaml. Four shots refreshed after DEMO-FIX. The rounds A, B and D changed no XAML and no window text file (RAN git diff)
- Cleanup: 36 helper folders moved to C:\_cleanup-1nov, none deleted, 7 merged remote branches deleted, steps/cleanup-1nov.md

Checked:
- Gate on 4a6fb62 before the last rebase: net48 1920, net8.0 1692, pytest 2268 passed, 1 skipped (RAN). The gate of this PR: see the PR
- Reviewer and breaker, one pass each. REAL fixed: undo left the pane ahead of the model, a narrow catch, three stale texts. LATENT in steps/latent.md
- Second method of the cleanup: each file of 39 helper folders hashed as a git blob against the 12,417 objects on origin (RAN)

Remains:
- Part 11 of the sheet waits for fix/1nov-d3

Known bugs:
- R445 has not run in Revit, part 10 of the sheet checks it. Hooks: this session started in the Lane A folder of the repo, so the hooks of that folder loaded

Next:
- FIX-2NOV-C, then part 11 when fix/1nov-d3 merges

Your turn:
- Closed: Q280 Save setup writes the model at once. 163 open, steps/questions-open.md

## Lane B Task 6 of FIX-1NOV-B, 2026-11-02, the demo by hand through the real windows, R457, Lane B on the laptop

Goal:
- Bader's Task 6: pass A with Dry run on and pass B with it off, from the pane's Next step, fix the empty Neighbourhood list, go or no-go

Done:
- Go or no-go in steps/demo-check.md: GO with Dry run off once four points are done, NO-GO with Dry run on, step 4 refuses (Q195)
- Pass B, 1 to 9 on NBH 012 (RAN): floors 1003, levels at the plan 1003, masses 336, walls 752, 2 Excel files. Stopped on a Revit box at step 5 (Q324)
- R457: the Neighbourhood list from the latest read. Callers: RefreshNbhCodes by PaneRunState.Committed and PaneHost.Load, LatestNbhCodes by
  RefreshNbhCodes, NbhCodes by LatestNbhCodes. Setters with no add-in or test host caller: PaneViewModel PickConfig, PickInput, SetSteps, listed only

Checked:
- 012_Report.xlsx read apart from the pane (RAN): 336 generic, 331 yard, 336 ramp, 336 links, Revit and Excel equal on links and floors, 0 mismatches
- C:\SA-demo by write time (RAN): 1 file changed. No SHA-256 before and after on the inputs, the hash rule of the round was not met for this run
- Gate: see the PR. Review of R457 in steps/latent.md

Remains:
- Bader answers Q306 to Q309, Q324, Q195. The guard 2a02c6d passed its test, it was not in the installed build and no run reached its case in Revit

Known bugs:
- Our fault: the driver script wrote an export into C:\SA-demo\inputs, against the rule of the round, left for Bader's answer (Q308)
- Step 2 back to Ready at the pass A open before any pick, cause UNKNOWN. Revit quit at 22:53:48 by a normal exit, cause UNKNOWN, no repeat
- Report Summary placeholders (Q307)

Next:
- Lane D D3 takes Revit. Lane C takes the window findings of steps/demo-check.md

Your turn:
- Q306 A neighbourhood picked after the first read. Q307 What the report Summary shows after a run. Q308 The stray export in the demo inputs.
- Q309 Refresh the demo config and add-in. Q324 The Revit warning box at step 5. 164 open, steps/questions-open.md
## FIX-1NOV-A2, 2026-11-02, the picks in button 2 and the plan of Update from report, Lane A on the laptop

Goal:
- Bader's FIX-1NOV-A2: button 2 takes the saved picks (Q292), Update from report finds the plan of 4 Check (Q278)

Done:
- R472 Update finds the plan of 4 Check: it looked for plan_<NBH>.json in the work folder root, 4 Check writes into check_4_<run id>. ReportRun.CheckRun.PlanOf reads the last committed run log, the plan alone held
- R473 Read takes the saved picks, else the scan proposal, else config, picks.json an input of the read so a pick changes its hash. R474 sources in read.json. ReadPicks, ReadDataRun, WorkerReads, read_steps.py
- Breaker 1: the proposal MP_VL4 kept 491 of 3,780 footprint inserts with no line, so no villa block source is taken (Q346). Breaker 2: the window saves the first source of every conflict, which set VL4 on 250 plots with no line,
  so only a second source is taken (Q348). A failed location plan read names a taken source. Review 1: default picks read as no picks, a test. Q278, Q292 closed by Bader's prompt

Checked:
- Q278 on a copy of the NBH 012 run, RAN: plan found, FFL of 2289 and villa type of 2294 listed, checked (fflOverEntry, stepsAtMost, stepHeights, riser, villaType), 2 entries applied. update_check.py 12,657 matches, 0 mismatches. An Excel of the SADM build refused in one line
- Q292 on the NBH 012 inputs, RAN with the steps labels and blocks: default 3,134 plot outlines, 2,962 plots, a pick of MP_PLOT_LINE 912 and 390, 16 inputs unchanged. The full read with that pick stops at the location plan fit and names the pick
- Real scans, RAN: on the 4 no proposal narrows the read, 3,780 of 3,780 inserts, Use these picks with nothing changed sets 0 plots
- Past job against main 84e17fd, RAN: whole project and the 7 keys, every plan byte-equal, 0 values differ
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures, on 25c84c7
- Gate, RAN on 25c84c7: pytest 2,268 passed, 1 skipped. C# net48 1,910, net8.0 1,685, 0 failed. R466 of Lane D failed once on net48 in a full run, passed alone 3 of 3 and in a full rerun, likely a race on DesignValues.Current, Lane D's fix is on fix/1nov-d3

Remains:
- A conflict pick changes the plot table, the build reads plots.csv (Q349). The first source can not be forced (Q348). Update lists saved changes again until 4 Check runs (Q347)

Known bugs:
- Saved projects ROSHN Makkah and task6 hold villa block and villa type picks the read refuses, 2 issue lines on every read (Q346). The first read after install reads each NBH again, its hash takes picks.json. Not run in Revit, Lane B held the lock

Next:
- Bader answers Q345 to Q349. Lane C: the window saves only conflicts a person changed

Your turn:
- Opened Q345 The key of a conflict pick. Q346 A villa block pick of one layer. Q347 A second Update before 4 Check runs again. Q348 A conflict pick of the first source. Q349 A conflict pick and the build.
  Closed Q278 Where Update from report finds the plan. Q292 The read does not use the picks yet
- 159 questions open, full text: steps/questions-open.md

## Lane D D2, 2026-11-01, the Revit read of the key link parameter groups (Q330), Lane D on the laptop

Goal:
- Bader's round D2: find why the Revit read of FIX-1NOV-D stopped, fix the start, read group, data type and sample of the 11 sheet rows from the IMC key

Done:
- Cause of the 3 FIX-1NOV-D stops: Revit never started, no journal between 14:10 and 16:05 (READ, Revit journals 0189, 0190), a build mid edit, the
  Common guard, {workRoot} of Run-TestHost.ps1 (fixed by Lane B, PR 127). The unsigned add-in box is no cause: the R108 watcher clicks Load Once on the
  4 tool DLLs in every run (RAN, 2 windows, 4 boxes each), no machine setting changed, no step for Bader
- A second cause found once Revit opened: since R405 JsonOut rounds every number by the design values, inside Revit no config lies above the test host,
  so every save of a read job after its first note threw and TestHostRunner.Save swallowed it. The probe saw the log written twice, the heartbeat 4 times
- R466: every job names the repo config as designConfig, JobSession.Subscribe loads it before the first step and notes the source
- The read KC2 with R466: 7 steps ok, 338 links, 35 shared parameters, 22 bound (RAN, round_addin reads KC2 run4). The 10 link parameters: group Text,
  spec String, 337 of 338 links with a value, CASE with none. Plot is a project binding with no GUID. The sheet takes Text, TEXT and the samples, FFL blank

Checked:
- Copy SHA-256 equal to key_copies.json, unchanged in both windows, deleted after, source unchanged (RAN, copy_KC2.json)
- Held to the sheet by against_sheet of R463: 10 GUIDs, 0 faults, 0 conflicts, group equal 10 of 10. key_parameters.py main refuses the file, its model
  stem lacks -zone2, the copy stem split of Q065
- Gate: see the PR. Peak 2.6 GB, least free 25.6 GiB, floor 2 GiB never reached

Remains:
- Bader answers Q336. D3 Links mode after Lane B's Task 6 hand run ends

Known bugs:
- TestHostRunner.Save writes a failure to Trace only, a log that cannot be written leaves no mark on disk
- Run-TestHost names a read copy by siteBuild.answerKey.model, R353 by the copy of answerKeys, key_parameters.py refuses the KC2 file (Q065)

Next:
- D3: the 13 KEO prototypes in Links mode on NBH 012 (Q275)

Your turn:
- Opened Q336 Link parameter group is Text not Data. Closed Q330 Group of the key link parameters unread, by Bader's prompt
- 156 questions open, full text and the recommended answers: steps/questions-open.md

## FIX-1NOV-A, 2026-11-01, ramp joints and no curb build, Lane A on the laptop

Goal:
- Bader's FIX-1NOV-A: one level at a corner two side by side ramps share, a check row per shared corner, no curb as a built element

Done:
- Cause on NBH 012 (SAD2 run of 04:06, and the Python build of main the same): 555 joints within 10 mm, 335 over 1 mm, max 224 mm. Front joints with no
  override 0 mm, so no road sample, rounding or fallback cause. 88 of 110 front joints differ by a rampFrontM gap, 150 of 170 back joints by each El @ Entry
- R470 Shared ramp corner, one level, behind floors.rampJoint.join, false by Bader's answer of 1 Nov (Q340). R471 Ramp joint check row, Pass at passGapM 1 mm
- Task 5: no code builds a curb. The curbs read stays: the front of R355 (BuildSteps.Run, plan_from_book.run_build to corner_front.road_edges), the curb
  point of a CAD ramp front (plan_from_cad.ramp_floor to floor_fit.curb_level), the reads of WorkerReads.Run. The pane row Curbs stays, Lane C told
- Breaker 1: the mean at joints twisted ramps and took 1,025 off El @ Entry, fixed to front joints at the road. Breaker 2: the road drops a rampFrontM override at
  a joint, 324 ramps twist (worst 360 mm), so Bader chose join false. Review 1 and 2 APPROVE. Q342 closed by Bader's prompt

Checked:
- With join true, RAN on head: front joints over 1 mm NBH 012 88 to 0 of 285, IMC 82 to 0 of 272, whole project 352 to 0 of 2,142, back joints left apart (Q341)
- With join false, RAN: R471 lists every shared corner, NBH 012 1,110 rows, 670 Fail (the 335 joints over 1 mm the script counts) and 440 Pass
- Past job against main 7d78fbe, RAN: whole project, the 7 keys and NBH 012, every plan byte-equal, the only values that differ the joint rows of checkRows (5,014 on the whole project)
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6, 32 runs, 416 step lines, 0 failures, on df09841
- Status block, RAN: main 679 values, branch 682, Bader +2 (nearM, passGapM), RECOMMENDED +1 (join)
- Gate, RAN: pytest 2,261 passed, 1 skipped. C# net48 1,832, net8.0 1,607, 0 failed, on df09841

Remains:
- Joining waits on Q340 and Q341. A joint at the edge of a build scope (Q344). Pass rows count in the chips (Q343)

Known bugs:
- Configs saved before this round hold no floors.rampJoint, so a build on them stops with the R470 line, C:\SA-demo\config among them

Next:
- Bader answers Q340, Q341, Q343, Q344, then join true or a new rule for joints an override moves

Your turn:
- Opened Q340 The level of a joint an override moves. Q341 Back corners two ramps share. Q343 Passing joint rows in the check chips.
  Q344 Joints at the edge of a build. Closed Q342 Ramp joint distance and pass limit
- 156 questions open, full text: steps/questions-open.md

## Lane B FIX-1NOV-B, 2026-11-01, masses as linked files, footprint by house, coordinates from CAD, Lane B on the laptop

Goal:
- Bader's tasks 2 and 3: one mass file per prototype linked per plot, the base from the outer walls, Q276 fixed, coordinates from CAD

Done:
- R450 mass file per prototype, named as its prototype, a config folder swaps in the KEO files. R451 wall ring or slab with the Q279 flag
- R452 masses as links, rotation and mirror as before, a link of another file swapped, a hand moved one where it stands, a stale one unloaded and reloaded
- R453 footprint by the house outline of layer 0 in the block (Q276 closed). R454 coordinate system of each input. R455 site position, named
  position and site location from CAD. R456 three control points. Test host: {workRoot} of a round spec and config xlsx in the run folder (Lane D)

Checked:
- Revit NBH 012, RAN: 13 mass files, wall ring 7 (C10 DP1 DP4 VD2 VD4 VL4 VL6, 0.96 to 1.04 of the slab), slab 5 (DP2 DP3 DT1 DT3 TH3), 336 links in 141.9 s
- Mirror, RAN: 167 of 336 mirrored in the plan, 167 with determinant -1 in Revit, 336 agree. Reload, RAN: DT3 1 m taller, 68 of 68 instances changed
- Footprint, RAN: unit type equal to the block 45 plots median 650 mm (VL4 148), type different 291 plots median 3.1 m (Q277)
- Button 1 cad, RAN LB1H: position E N of the plot box, 0, 0. Read back: position ROSHN Makkah, survey and base point internal 0, site location set,
  3 control points 0.000 mm. Past job minus this: E 820.992 m, N 110.240 m, elevation the whole past level (Q327), angle 0. Shot of Location dialog outside git
- Review 1 and breaker 1 of each task, review 2 and breaker 2: REAL fixed with tests, the rest in steps/latent.md. Inputs 41 of 41 unchanged each run
- Gate, RAN: pytest 2,246 passed, 1 skipped. C# net48 1,877 and R347 timing failed under Revit load, passed 3 of 3 alone, net8.0 1,653

Remains:
- Task 6 hand run after this PR and Lane A. coordinates.source stays positionFile (Q328). Mass making about 2 min per prototype (Q322)

Known bugs:
- Wall ring corners exact only for centred walls of one width (Q321). A keyCopy test host run is refused (Q323)

Next:
- Task 6, the demo by hand through the real windows, Dry run on and off. Bader answers Q320 to Q329

Your turn:
- Opened Q320 Keep the solids path of light mode. Q321 Mass base from walls, third attempt stopped. Q322 Mass files made inside button 7.
  Q323 A keyCopy run refused by its guard. Q325 to Q329 of the coordinates. Closed: Q276
- 152 questions open, full text: steps/questions-open.md

## Lane D FIX-1NOV-D, 2026-11-01, the shared parameters from one Excel, Lane D on the laptop

Goal:
- Bader's task 4: only the shared parameters of the past links, by name and GUID, in config/shared_parameters.xlsx, bound by button 1, each button its own

Done:
- The 10 shared parameters on the 338 links of the IMC key are the 10 config held, none added, dropped or renamed. Plot of Q156 kept as row 11
- config/shared_parameters.xlsx: Parameter, GUID, Data type, Group, Category, Filled by, Sample value, Keep. addin.sharedParameters and fixedParameters gone
- R460 the read job gives the spec, group and a value. R461 one sheet source. R462 ParameterBook reads it inside Revit. R463 key_parameters.py holds the keys
  to it and writes nothing. R464 bind by its group, a group change bound again. R465 button 7 writes only its own, FFL never, Plot Floors' alone
- Button 1 stops on a picked sheet other than config's (Q335). Get template writes the eight columns. Q155 closed by Bader's prompt
- The Revit read of the key groups stopped at 3 attempts before Revit (a build mid edit, the install guard, {workRoot} of Run-TestHost.ps1), Q330

Checked:
- Data type, RAN: String storage on every link of the 7 key reads RA to RG, 2,591 links for 6 names, 1,858 for 4
- Unique name, RAN on past_all.csv: RVT-Name = Plot-Number | Prototype | Number-of-Steps | Facade-Color | Facade-Design-Option on 321 of 324 IMC plots,
  3 with another fifth part. The rule plotCodeStepsAndFacade holds 0.9916 on 1,791 key plots (READ)
- ParameterBook against XlsxSheet, RAN: the sheet of the repo and inline strings read alike. C# and Python on the sheet of the repo and 22 variants, RAN by breaker 1
- Source copy of IMC hashed before and after each attempt, equal. Add-in and test host back on main 2cf5315 after the attempts
- Review 1: CHANGES NEEDED, 1 REAL fixed with a test (a picked sheet), 4 minor fixed. Breaker 1: S3 REAL fixed with a test (the group), L1 and L2 to Lane B
- Gate, RAN: pytest 2,245 passed, 1 skipped. C# net48 1,816, net8.0 1,591, 0 failed, 0 warnings

Remains:
- The read KC2 of the key groups once fix/1nov-b is on main (Q330). C:\SA-demo\config refreshed by Lane B at its next reinstall

Known bugs:
- 10 LATENT findings of review 1 and breaker 1 in steps/latent.md, the Plot-Number row given to another button first

Next:
- Bader answers Q330 to Q335. Lane B merges the xlsx copy of Run-TestHost.ps1

Your turn:
- Opened Q330 Group of the key link parameters unread. Q331 Plot is no link parameter of the keys. Q332 FFL bound but never written.
  Q333 Categories beyond the link instances. Q334 An xlsx read inside Revit. Q335 A parameter sheet picked in the pane. Closed: Q155
- 144 questions open, full text: steps/questions-open.md

## 4j-2, 2026-11-01, the floor split and the walls of irregular plots, Lane A on the laptop

Goal:
- Bader's queued round of 31 Oct: measure the past splits, the split test of 2.2.B, the walls of irregular plots of 2.8, behind designBasis

Done:
- Past splits measured on the 5 TU keys: 42 of 84 clusters of 4 units split, middle 27, walls 1 7, walls 3 7, walls 1,2 1. Street slope does not part them
- R414 Split by slope and steps, behind designBasis.split.test, off: it matches 8 of 82 key clusters (0.0976), a split never mends a slope (Q303, Q304)
- R415 Irregular plot by tolerance, 2 degrees: 41 of 2,501 key plots. R416 Boundary wall face on plot line under rev6, mitre ends at corners
- Lane B 10:34: steps/demo-check.md, 5115325 installed, hand run not done, NO-GO live, the security dialog defaults to Do Not Load
- Lane B 08:50: steps/demo-fill.md, each demo field and its path in C:\SA-demo, read only. Parameter sheet shows Missing, stops nothing
- R417 garden wall not built: no input holds the garden door, no key garden wall turns or stands at 5.52 m (Q259). R413 not used, the split measure is a record
- Review 1 APPROVE, breaker 1: 2 REAL fixed with tests (the status of a moved wall, the corners of moved walls), 12 LATENT. Review 2: a test gap closed
- Main merged after PR 117 to 121. Q257 and Q258 renumbered Q303 and Q304, Q288 renumbered Q299, for Lane C and D

Checked:
- Past job against main 705ec33, RAN: whole project and the 7 keys on 592c0f4, 0 of 673,868 values differ on the whole project, 0 on each key, every plan byte-equal, the new fields null
- Parity, RAN: 13 steps, net48 and net8.0, whole project and the 7 keys, past job, rev6 and rev6 with the split test, 48 runs, 624 step lines, 0 failures, 0 refused
- Status block, RAN: main 705ec33 678 values (Bader 55, MEASURED 51, DOCUMENT 297, RECOMMENDED 14, Code 3, UNKNOWN 258), branch 682: DOCUMENT +1, RECOMMENDED +1, UNKNOWN +2
- Moved walls on the rev6 all build, RAN: 37 of 1,415 walls on 42 irregular plots, 28 of 28 corners share one end, 0 gaps, 0 overlaps
- Gate, RAN: pytest 2,236 passed, 1 skipped. C# net48 1,796, net8.0 1,578, 0 failed, on 592c0f4

Remains:
- The split test stays off until Q303 and Q304. The garden wall of 2.8.B waits on a read of the prototype doors (Q259)

Known bugs:
- On 6 of 10 notched plots the front of R355 is the 3 m notch edge (Q299). How Revit joins the moved walls is UNKNOWN (L4j2-2)

Next:
- Lane B adds L4j2-1 and L4j2-2 of steps/waiting-on-others.md. Bader answers Q303, Q304, Q259, Q299

Your turn:
- Opened Q259 Garden door place of each unit. Q299 The front of a notched irregular plot. Q303 The slope limit in the split test.
  Q304 The side of an uneven split. Closed: none
- 139 questions open, full text: steps/questions-open.md

## Lane B DEMO, 2026-11-01, the add-in ready to show at 11:00, Lane B on the laptop

Goal:
- The add-in installed from main, C:\SA-demo with copies, models, a saved project and the demo sheet steps/demo.md, by 09:30

Done:
- Installed per user from main 691e1fc, version 1.0.0+691e1fc, with Lane C SCAN-UI, Lane A 4i and Lane D 4j-1b
- C:\SA-demo: 43 input copies of NBH 012 hashed (originals unchanged 43 of 43), config and rules of main pointed at them, SA-demo-start.rvt, SA-demo-done.rvt, the two Excel files
- Saved project ROSHN Makkah: the demo config, Masses and the scan picks of a scan of the copies (villa type Makkah Building Footprint.dwg|block|MP_VL4), read back by SavedStarts and PreSetting.Import
- Base boundary in Revit on the 13 prototypes (Q206): the mass base is the outer loop of the largest floor on the lowest level, 0.0 mm. Floor ids C10 872099, DP1 878667, DP2 849902, DP3 857818, DP4 886879, DT1 860942, DT3 825808, TH1 860942, TH3 825808, VD2 849902, VD4 886879, VL4 2220408, VL6 872099
- DT3 against its walls: outer wall face 21 corners 82.3 m2, base 8 corners 78.6 m2, 649.1 mm both ways (304.4 without the entrance recess). A DT3 link placed on plot 2294 gives the same 649.1 mm. Slides footprint-vs-mass.png, dt3-outline-vs-mass.png, dt3-link-vs-mass.png

Checked:
- Revit SAD2 at 691e1fc on the demo copies, RAN: 1, 2 and 4 to 9 commit, 3 closes by design, Rules closes, 5.2 min, peak 2479 MiB, least free 29.0 GiB, 41 of 41 inputs unchanged, 10 shots none blank
- Originals opened by the probe, RAN: 13 of 13 prototypes unchanged by SHA-256, Revit opened copies only. No code changed in DEMO

Remains:
- The walls of 9 prototypes do not close into a ring, their wall outline is UNKNOWN (Q279). A saved project holds no drawing path, the demo browses the files (Q296)

Known bugs:
- All 336 masses flagged by the footprint check (Q276). Update from report stops (Q278). Links waits on Q275

Next:
- The demo at 11:00 by steps/demo.md, then Bader answers Q275 to Q279 and Q296

Your turn:
- Opened Q279 Mass base, slab edge or outer wall face. Q296 A saved project holds no drawing path. Closed none
- 135 questions open, full text: steps/questions-open.md

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
- Task 7: the Revit shots of the DEMO run of Lane B (SAD2_20261101-040247) hold real values, so they stay outside git, the
  differences are in steps/ui-shots/README.md. What the tool read has no Revit shot, a test host run skips the scan (Q295) The scan window shows only offscreen, a test host run skips the scan (Q295)

Known bugs:
- The read of button 2 does not take the picks yet (Q292). The first press hashes the prototypes, over 1 GB, on the Revit thread once

Next:
- Lane B DEMO with the add-in of main, Lane B joins the skip of clusterUnconfirmed (Q300)

Your turn:
- Opened: Q288 Scan picks in the settings at version 1. Q289 Board rows the scan does not read. Q290 Curbs and road surface left out
  of scan. Q291 Where the line Current files is shown. Q292 The read does not use the picks yet. Q293 Approve the three drafts of
  SCAN-UI. Q294 Rules chosen in Set up site, when used. Q295 Test host runs skip the scan. Q300 Plots with no cluster
  proposal. Q301 The build takes the door level pick. Q302 Door level row under the past job. Q305 Hide Confirmed under the past job
- Closed: Q246 Button 2 starts the scan or the window, Q283 Picks of layers, sheets, columns and coordinates, by Bader's prompt
- 133 questions open, full text: steps/questions-open.md

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

## Older rounds

- Lane B 5c, ADDIN 5e and 5f, L0 and 5a: steps/history/log-addin-5c-l0.md

- Round ADDIN 4h-2, 2026-10-29, R366, R369 and the join items, Lane A on the laptop: steps/history/log-addin-4h-fold.md line 5
- Lane B 5b, 2026-10-29, button 1 Set up site, the watcher before Revit and the freeze guard: steps/history/log-addin-4h-fold.md line 44
- Round ADDIN 4h-1, 2026-10-29, Q122, R367, R368 and the join items, Lane A on the laptop: steps/history/log-addin-4h-fold.md line 80
- Round FOLD, 2026-10-29, the fold in, D9 to D19, the answer pass and the manual rules: steps/history/log-addin-4h-fold.md line 118
- Round ADDIN 4g3, 2026-10-29, 4g3 done on the laptop, R365 and the C# checks of Q111: steps/history/log-addin-4g2-4g3.md line 5
- Round ADDIN 4g3, 2026-10-28, 4g3 paused, moved to the laptop: steps/history/log-addin-4g2-4g3.md line 50
- Round ADDIN 4g2, 2026-10-28, Q105, Q107, Q109, Q110, the walls and the check rows: steps/history/log-addin-4g2-4g3.md line 94
- Round ADDIN 4g1, 2026-10-27, Q098, Q099 and the status of each plotted value: steps/history/log-addin-4f-4g1.md line 5
- Round AUDIT-RULES, 2026-10-27, AI MAX version 4 and the status of every rule value: steps/history/log-addin-4f-4g1.md line 50
- Round ADDIN 4f, 2026-10-27, Q089 and steps 8, 10, 12 and 13 in C#: steps/history/log-addin-4f-4g1.md line 92
- Round REC, 2026-10-27, the records diet and Q095 to Q097: steps/history/log-addin-4f-4g1.md line 135

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
