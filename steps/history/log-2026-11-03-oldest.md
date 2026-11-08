# Round log, oldest entries of 3 Nov 2026

Moved from steps/log.md on 8 Nov by Lead for the 100 KB cap of R400, word for word.

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

Older rounds stand in steps/history/log-older-2026-11-02.md, cut byte for byte to keep this file under the cap of the records.
