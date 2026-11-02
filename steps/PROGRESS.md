STATE WAITING, 2026-11-01 04:40
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- Lane D D3 2 Nov: Links mode on NBH 012, 13 prototypes in 2024, 336 real links, R467, R468. D2: R466, Text (Q336). FIX-1NOV-D: R460 to R465. 4j-1, 4j-1b: R420 to R429
- Lane A 4i, 4j-2, FIX-1NOV-A and A2, one PR each (D20): R407 to R416, R470 joints behind join false, R471 rows, R472 Update plan, R473, R474 picks
- Lane B FIX-1NOV-B on 2026-11-01: masses as linked files (R450 to R453), coordinates from CAD (R454 to R456), proven in Revit on NBH 012
- Lane B Task 6 on 2026-11-02: the demo by hand, Dry run off 1 to 9 on NBH 012, Dry run on stops at 4 (Q195), R457, steps/demo-check.md
- Lane B 6a on 31 Oct: NBH 012 through 1 to 9 from a new project, no click. 5d, PR 113: 5, 6, 8, 7 at the FFL. 5c, PR 111: 2, 3, 4, 7
- Lane C SCAN-UI PR 117, 119. DEMO-FIX R444. FIX-1NOV-C PR 132 (R445). FIX-2NOV-C: no dead end in the windows (R446)
- Lane A: 5e and 5f on 2026-10-30, one PR (D20), R381 button 9 Report, R402 Update from report. Phase 4 and 5: 4h-1 PR 107, 4h-2 PR 109, 4g3
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- Lane D D3, D2 and FIX-1NOV-D: one PR each. 4j-1: PR 114. 4j-1b: one PR, set A and the renamed set equal to 4j-1 field by field, today's reader 21 of 21 files equal
- Lane C SCAN-UI: PR 117, PR 119 and the Revit shot records. 4i, 4j-2, Lane B 6a: one PR each (D20). Lane B 5d: PR 113. Lane C NEWPROJ-UI: PR 112. Lane B 5c: PR 111. 5e and 5f: PR 110
- 4h-2: PR 109. 4h-1: PR 107. FOLD: PR 101 to 105. Lane B 5b: one PR (D20). 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
- 4g3: PR 83, 85, 88, 91, 93, 95, 96 and the end records. 4g2: PR 74 to 81. 4g1: PR 69 to 73. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC: PR 56 to 60. 4e PR 55 to 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

## Open bugs

- Scan: a large drawing past scan.pairTimeCapS is scanned in part, a notRead row lists it (Q258). No villa type source at the strong share (Q254)
- Full text at 73b8c42: steps/history/progress-addin-4e.md. Bugs no real input reaches: steps/latent.md (Q095)
- Footprint by house outline: 291 of 336 plots of NBH 012 sit in a block of another unit type (Q277)
- The wall rule keyRules.wallOpen is weak, 2,539 of 3,427 walls hold on the keys, 0.7409 (Q115, Q266)
- Q063: the whole NBH 03 image runs past the 30 minute step, I25 did not respond at 10,732 MiB private memory
- R310 logs free memory in every window with no stop at the floor. In 2a it fell to 0.53 to 1.42 GiB per read window, cause UNKNOWN
- Revit starts 9 to 11 child processes in a read (RC 11). rvt_info.py (R203) does not see cloud links, the SE models list 1 of 14
- A read is tied to its key by the copy stem, the zone2 master and the IMC 2024 copy share one, only a hash parts them (Q065)
- ELl@Entry is off its schedule over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327)
- The reader angleToTrueNorthDeg has 3 decimals and is not brought into 0 to 360, the placer takes 0 only
- 4 yards that stand only by a mirror override read a status stronger than their evidence (Q163)
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146). 4 Check dry run has no worker start (Q195)
- Under rev6 one road surface covers 630 of 2,704 plots (Q256), the past overrides apply (Q255). R414 off, 8 of 82 (Q303, Q304). No R417 (Q259)

## Bader's turn

Opened in 4i, Lane A: Q224 to Q242, Q255, Q256. In 4j-2: Q259, Q299, Q303, Q304, titles in steps/questions-open.md. Closed in both: none.
Opened in Task 6, Lane B: Q306, Q307, Q308, Q309, Q324, titles in steps/questions-open.md and the top entry of steps/log.md. Closed: none.
Opened in FIX-1NOV-A, Lane A: Q340 The level of a joint an override moves. Q341 Back corners two ramps share. Q343 Passing joint rows in the check chips.
Q344 Joints at the edge of a build. Closed: Q342 by Bader's prompt. A2: opened Q345 to Q349, titles in the log, closed Q278, Q292.
Opened in FIX-1NOV-B, Lane B: Q320 to Q323, Q325 to Q329, titles in steps/questions-open.md and the top entry of steps/log.md. Closed: Q276.
Opened in SCAN-UI, Lane C: Q288 Scan picks in the settings at version 1. Q289 Board rows the scan does not read. Q290 Curbs and road surface left out of scan. Q291 Where the line Current files is
shown. Q293 Approve the three drafts of SCAN-UI. Q294 Rules chosen in Set up site, when used. Q295 Test host runs skip the scan. Q300 Plots with no cluster
proposal. Q301 The build takes the door level pick. Q302 Door level row under the past job. Q305 Hide Confirmed under the past job.
Opened in FIX-1NOV-D, Lane D: Q330 Group of the key link parameters unread. Q331 Plot is no link parameter of the keys. Q332 FFL bound but never written.
Q333 to Q335. Closed: Q155. Opened in D2: Q336 Link parameter group is Text not Data. Closed in D2: Q330, by Bader's prompt.
Opened in D3: Q337 to Q339, titles in the log. Closed in D3: Q275, by Bader's prompt.
Closed in FIX-1NOV-C and FIX-2NOV-C: Q280, Q195, by Bader's prompt. 164 questions open, full text and the recommended answers: steps/questions-open.md. Asks: steps/waiting-on-others.md.

## Next three steps

1. Bader's answers. Lane D: Q243 to Q258, Q331 to Q339. Lane A: Q340, Q341, Q345 to Q349, Q303, Q304, Q259
2. Bader answers Q277, then Phase 6b
3. Bader answers Q224 and Q255 of 4i, Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
