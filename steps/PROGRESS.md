STATE WAITING, 2026-10-27 17:32, for the 4g session, Bader pastes steps/START.md into a fresh session
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- AUDIT-RULES done on 2026-10-27, PR 67 and 68, no logic change: steps/investigations/rule-status-audit.md, Q100 to Q103
- 4f: Q089 the footprint clusters alone, steps 8, 10, 12 and 13 in C# with parity on 2,704 plots, 0 failures
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Phases 0a to 4f merged, PR 41 to 66: the reads of button 2, then steps 4 to 8, 10, 12 and 13 of the plan builder in C# with parity
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Findings from 4f: steps/investigations/round-addin.md
- Part 2, the sheets, waits for Bader's check of Part 1

## Done

- AUDIT-RULES: PR 67 AI MAX v4 and the log split, PR 68 the audit. 4f: PR 61, 62 records, PR 63 Q089, PR 64 R358, R359, PR 65 R360, R361, PR 66 records
- REC, PR 56 to 60: rules, design and round file in parts, questions-open.md, latent.md, CLAUDE.md to 9 KB, R400, START.md
- ADDIN 4e, PR 55: R354 to R357, parity on net48 and net8.0 with 0 failures, the proof 0 unexplained on 7 keys
- ADDIN 4d, PR 54: R351 to R353. 4c, PR 53: R348 to R350. Q081 and Q073, PR 52. 4b, PR 51. 4a, PR 50. 3b to 0a, PR 41 to 49
- Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier rounds: PRs 1 to 32

## Open bugs

- Full text at 73b8c42: steps/history/progress-addin-4e.md. Bugs no real input reaches: steps/latent.md (Q095)
- Q063: the whole NBH 03 image runs past the 30 minute step, I25 did not respond at 10,732 MiB private memory
- R310 logs free memory in every window with no stop at the floor. In 2a it fell to 0.53 to 1.42 GiB per read window, cause UNKNOWN
- Revit starts 9 to 11 child processes in a read (RC 11). rvt_info.py (R203) does not see cloud links, the SE models list 1 of 14
- The 1,409 spots of the SE models give no value, their STR links are not loaded in a read
- A read is tied to its key by the copy stem, the zone2 master and the IMC 2024 copy share one, only a hash parts them (Q065)
- ELl@Entry is off its schedule over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327)
- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT
- The reader angleToTrueNorthDeg has 3 decimals and is not brought into 0 to 360, the placer takes 0 only
- A native2022 build with --overrides is refused on 1,346 native entries with no value (Q075)
- For the check rows of 4g: 44 unit plots in the steps the plan does not place, 5 DP2 plots with no hand (Q098), no linkName on 2,513 (Q099)
- Not in 4f: heightOffset and floor z need the site file of button 1, roadAtFrontM in the plan with R364, the past_all.csv rebuild of R385

## Bader's turn

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
Q099 Link Name of a build. Q100 Status of values from the Rev 06 report. Q101 Level profile fields with no share. Q102 Show the assumed values per plot. Q103 Design values in code, not in config.

## Next three steps

1. ADDIN 4g: R362 to R365, the walls, issues and the two Excel files. Bader pastes steps/START.md into a fresh session
2. ADDIN 4h: R366 to R369, one session by steps/START.md
3. ADDIN Phase 5 and the Revit run after 5c, then Phase 6, as steps/01_next.md gives them
