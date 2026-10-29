STATE RUNNING, FOLD, 2026-10-29 05:56
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- Round FOLD running on the laptop: the fold in of steps/lane-b.md, D9 to D19, the answer pass, the manual rules M01 to M15
- Closed in Round FOLD so far: the D3 window rule question and the 20 answered Lane B questions, ids in steps/questions-closed.md
- 4g3 done on 2026-10-29 in Lane A on the laptop: R365 the report Excel with D4, the C# checks of Q111 with 0 failures
- Lane B: L0 done on 2026-10-28. 5a Revit run 3 ran on 2026-10-29, 18 of 18 steps ok, PR 97 and 99 merged, its close waits for Q147
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- Lane B 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
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
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146)

## Bader's turn

Opened in Round FOLD so far: Q146 Tab title hidden behind the overflow mark. Q147 Close 5a with three items not seen in Revit.
Open: Q001 Q002 Q003 Q004 Q005 Q006 Q007 Q008 Q009 Q010 Q012 Q013 Q015 Q016 Q017 Q018 Q019 Q020 Q022 Q023 Q024 Q025 Q026 Q027 Q029 Q030 Q031 Q032 Q033 Q036 Q037 Q038 Q039 Q041 Q042 Q051 Q052
Q053 Q054 Q055 Q056 Q057 Q058 Q059 Q062 Q063 Q064 Q065 Q066 Q068 Q069 Q070 Q071 Q072 Q074 Q075 Q076 Q077 Q078 Q079 Q082 Q085 Q086 Q087 Q119 Q120 Q123 Q124 Q125 Q146 Q147
Also open with no ID: Bader's final approval of revit-ribbon.png and revit-pane.png, steps/ui-shots/README.md.
71 questions open, full text and the recommended answers by ID: steps/questions-open.md.

## Next three steps

1. Round FOLD: D9 to D19, the answer pass of 29 Oct 2026, the manual rules M01 to M15, then the end records
2. 4h-1 on Lane A, 5b on Lane B, both by tools/run-lane.ps1, which Round FOLD writes
3. Part 2 after Bader's check of Part 1
