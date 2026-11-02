# Round log, ADDIN 4g2 and 4g3

Moved from steps/log.md in FIX-2NOV-D to keep it under 100 KB (R400).

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

