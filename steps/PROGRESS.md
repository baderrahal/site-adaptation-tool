STATE WAITING, 2026-10-29 04:56, 4g3 done, 4h next in Lane A
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- 4g3 done on 2026-10-29 in Lane A on the laptop: R365 the report Excel with D4, the C# checks of Q111 with 0 failures
- The C# of 4g2 and 4g3 holds its parity on real inputs, the 7 keys and the whole project, net48 and net8.0 (D6)
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Phases 0a to 4g3 merged, PR 41 to 96: the reads of button 2, steps 4 to 15 of the plan builder, the status, the level and report Excel
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Findings: steps/investigations/round-addin.md. Part 2 waits for Bader's check of Part 1

## Done

- 4g3: PR 83, 85 Q112 and Q113, 88 step 15, 91 the level Excel, 93 handover, 95 laptop start, 96 R365 the report Excel, then the end records
- 4g2: PR 74 to 81, Q105 to Q110, the walls, the check rows. 4g1: PR 69 to 73, Q098, Q099, the status block and the status in the plan. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC, PR 56 to 60: rules, design and round file in parts, questions-open.md, latent.md, CLAUDE.md to 9 KB, R400, START.md
- 4e PR 55 R354 to R357, 4d PR 54, 4c PR 53, PR 52, 4b PR 51, 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

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
- Plot cut rows are not built until 5c (Q117), so Plot cut reads 0 and 7 cut plots of the IMC join count as following every rule on the whole project
- Report Excel: 2 Read data counts plan plots not faces (Q123), the Counts box holds the plan against itself until 5e (Q124)
- Not in 4f: heightOffset and floor z need the site file of button 1, the past_all.csv rebuild of R385

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
Q085 Plot as a shared parameter. Q086 Check point limits against the keys. Q087 Hand entries in addin.sharedParameters.
Q119 Reads of a value several overrides touched. Q120 Mirror and rotation sets too wide. Q123 2 Read data counts faces or plan plots.
Q124 Counts box before the Revit read. Q125 Walls Issues row in the Issues sheet.

## Next three steps

1. ADDIN 4h in Lane A on the laptop: Q122 first (D8), R366 to R369 with Bader's 9 steps, then the fold-in of steps/lane-b.md
2. Phase 4 done after 4h. The C# checks of 4h run inside 4h (D6)
3. Lane B phases by steps/lane-b.md, Part 2 after Bader's check of Part 1
