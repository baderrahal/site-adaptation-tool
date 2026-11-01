STATE RUNNING, SCAN-UI, 2026-11-01 04:20
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- Lane D 4j-1 done on 2026-10-31 and 4j-1b on 2026-11-01: the scanner of any CAD, Excel and PDF, R420 to R429, the scan speed and its cap
- Lane A 4i done on 2026-10-31, one PR (D20): designBasis rev6 or roshnPastJob, R407 to R412, parity 0 failures, past job 0 differ (Q224)
- Lane B DEMO done on 2026-11-01: main 691e1fc installed, C:\SA-demo and steps/demo.md for 11:00, 1 to 9 in 5.2 min on the copies
- Lane B 6a done for Masses on 2026-10-31: NBH 012 through buttons 1 to 9 from a new project in 6.7 min with no click, Links waits on Q275
- Lane B 5d done on 2026-10-31, PR 113: buttons 5, 6 and 8, 7 at the FFL, Q115, Q116, proven in Revit by 6a. 5c, PR 111: buttons 2, 3, 4 and 7
- Lane C SCAN-UI on 2026-11-01: PR 117 What the tool read, button 2 scan, New project picks, Current files, PR 2 cluster confirm, door level
- 5e and 5f done on 2026-10-30 in Lane A, one PR (D20): R381 button 9 Report, R402 Update from report and its window
- Lane A is done for Phase 4 and 5: 4h-1 PR 107, 4h-2 PR 109, 4g3 on 2026-10-29
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- Lane D 4j-1: PR 114. 4j-1b: one PR, set A and the renamed set equal to 4j-1 field by field, today's reader 21 of 21 files equal
- Lane C SCAN-UI: PR 117 and PR 2, code, tests and records. 4i, Lane B 6a: one PR each (D20). Lane B 5d: PR 113. Lane C NEWPROJ-UI: PR 112. Lane B 5c: PR 111. 5e and 5f: PR 110
- 4h-2: PR 109. 4h-1: PR 107. FOLD: PR 101 to 105. Lane B 5b: one PR (D20). 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
- 4g3: PR 83, 85, 88, 91, 93, 95, 96 and the end records. 4g2: PR 74 to 81. 4g1: PR 69 to 73. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC: PR 56 to 60. 4e PR 55 to 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

## Open bugs

- Scan: a large drawing past scan.pairTimeCapS is scanned in part, a notRead row lists it (Q258). No villa type source at the strong share (Q254)
- Full text at 73b8c42: steps/history/progress-addin-4e.md. Bugs no real input reaches: steps/latent.md (Q095)
- All 336 masses of NBH 012 are flagged by the footprint check, the drawing holds plot blocks not houses (Q276). Update from report stops (Q278)
- The wall rule keyRules.wallOpen is weak, 2,539 of 3,427 walls hold on the keys, 0.7409 (Q115, Q266)
- Q063: the whole NBH 03 image runs past the 30 minute step, I25 did not respond at 10,732 MiB private memory
- R310 logs free memory in every window with no stop at the floor. In 2a it fell to 0.53 to 1.42 GiB per read window, cause UNKNOWN
- Revit starts 9 to 11 child processes in a read (RC 11). rvt_info.py (R203) does not see cloud links, the SE models list 1 of 14
- A read is tied to its key by the copy stem, the zone2 master and the IMC 2024 copy share one, only a hash parts them (Q065)
- ELl@Entry is off its schedule over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327)
- The reader angleToTrueNorthDeg has 3 decimals and is not brought into 0 to 360, the placer takes 0 only
- 4 yards that stand only by a mirror override read a status stronger than their evidence (Q163)
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146). 4 Check dry run has no worker start (Q195)
- Under rev6 the one road surface covers 630 of 2,704 plots (Q256), the past override list still applies (Q255)

## Bader's turn

Opened in 4i, Lane A: Q224 to Q242, Q255, Q256, titles in steps/questions-open.md and the top entry of steps/log.md. Closed in 4i: none.
Opened in DEMO, Lane B: Q279 Mass base, slab edge or outer wall face. Q296 A saved project holds no drawing path.
Opened in SCAN-UI, Lane C: Q288 Scan picks in the settings at version 1. Q289 Board rows the scan does not read. Q290 Curbs and road surface left out
of scan. Q291 Where the line Current files is shown. Q292 The read does not use the picks yet. Q293 Approve the three drafts of SCAN-UI. Q294 Rules
chosen in Set up site, when used. Q295 Test host runs skip the scan. Q300 Plots with no cluster proposal. Q301 The build takes the door level pick. Q302 Door level row under the past job.
Opened in 4j-1, Lane D: Q243 Layer pairs for plot outlines in the scan. Q244 A tie in a scan proposal. Q245 Old and new code of one row agree.
Q248 Villa blocks on one layer per type. Q249 A sheet whose numbers shift by area. Q250 The pair trace on the Road Layout.
Q251 The centroid of today's plots.json. Q252 PDF tables and turned text in the scan. Q253 The header row of a scanned sheet. Q254 The order and share of villa type sources.
Closed in 4j-1: Q247, by Bader's rule of 31 Oct 2026. Opened in 4j-1b: Q257 Threads of the scan pair trace. Q258 The time cap of the scan pair trace.
Closed in SCAN-UI: Q246, Q283, by Bader's prompt. 134 questions open, full text and the recommended answers: steps/questions-open.md. Asks: steps/waiting-on-others.md.

## Next three steps

1. Lane B DEMO for 1 Nov 11:00, Lane C SCAN-UI pictures from the DEMO run, then Lane A 4j-2. Lane D waits for Bader's answers to Q243 to Q258
2. Bader answers Q275 to Q278, then the Links run of 6a and Phase 6b
3. Bader answers Q224 and Q255 of 4i, Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
