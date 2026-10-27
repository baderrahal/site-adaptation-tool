# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

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
