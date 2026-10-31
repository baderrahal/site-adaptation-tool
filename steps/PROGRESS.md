STATE RUNNING, 4i and 5d, 2026-10-31 13:55
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- 4i on 2026-10-31 in Lane A, one PR (D20): designBasis rev6 or roshnPastJob, R407 to R412, Q224 to Q242, Q255, Q256. ROSHN keeps roshnPastJob (Q224)
- Lane D 4j-1 done on 2026-10-31, one PR: the scanner of any CAD, Excel and PDF, R420 to R427, worker command scan, python/scan.py
- Lane B 5d on 2026-10-31, code merged, phase open: buttons 5, 6 and 8, 7 at the FFL, Q115, Q116, Q214, Q215, Q156. In Revit 1 to 5 commit, 6 stops (Q274)
- Lane C NEWPROJ-UI done on 2026-10-31, PR 112: New project, How it works, pane text, R430 to R434. Lane B 5c done, PR 111: buttons 2, 3, 4 and 7
- Lane A: 5e and 5f on 2026-10-30 (R381, R402), 4h-1 PR 107, 4h-2 PR 109, 4g3 on 2026-10-29, done for Phase 4 and 5 until 4i
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- 4i, Lane D 4j-1 (parity 0 differ on set A and the renamed set) and Lane B 5d: one PR each with code, tests and records (D20). Lane C NEWPROJ-UI: PR 112. Lane B 5c: PR 111. 5e and 5f: PR 110
- 4h-2: PR 109. 4h-1: PR 107. FOLD: PR 101 to 105 and the end records
- Lane B 5b: one PR (D20). 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
- 4g3: PR 83, 85, 88, 91, 93, 95, 96 and the end records. 4g2: PR 74 to 81. 4g1: PR 69 to 73. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC: PR 56 to 60. 4e PR 55 to 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

## Open bugs

- Scan: the Road Layout passed 40 minutes in the pair trace (Q250). No villa type source reaches the strong share on ROSHN (Q254)
- Full text at 73b8c42: steps/history/progress-addin-4e.md. Bugs no real input reaches: steps/latent.md (Q095)
- Button 6 stops in Revit: its levelExcel worker clears the temporary levels file of its run id (Q274). 7 and 8 never ran in Revit
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

Opened in 5d, Lane B: Q260 to Q274, closed in 5d: Q148, Q156, Q190, Q214 to Q216, Q218 to Q221, Q261, Q264, Q272, titles in steps/questions-open.md.
Opened in 4i, Lane A: Q224 The mode the ROSHN config runs. Q225 The level of a yard floor under Rev 6. Q226 The hand of a detached unit. Q227 Civil file
format and plot match. Q228 Default slopes of road plus slope. Q229 A row with no single cluster split. Q230 Value status of the Rev 6 values. Q231 Civil door
level where no road level. Q232 yardGrade in plan.json. Q233 Slope limit at 3 or 4 decimals. Q234 Read step road levels under Rev 6. Q235 Hand table cover per
unit code. Q236 Label size in the PDF check. Q237 Folder of the Rev 9 workbooks. Q238 TH1-IM in the cluster sequences. Q239 Mirror of DP2 and DP4. Q240 The 121
IMC plots of the audit. Q241 doorLevel.source reads as a note in R401. Q242 The reason of an unreadable input. Q255 The override list under rev6. Q256 The one
road surface covers a quarter. Closed in 4i: none.
Opened in NEWPROJ-UI, Lane C: Q280 to Q287, titles in steps/questions-open.md.
Opened in 4j-1, Lane D: Q243 Layer pairs for plot outlines in the scan. Q244 A tie in a scan proposal. Q245 Old and new code of one row agree.
Q246 Button 2 starts the scan or the window. Q248 Villa blocks on one layer per type. Q249 A sheet whose numbers shift by area. Q250 The pair trace on the Road Layout.
Q251 The centroid of today's plots.json. Q252 PDF tables and turned text in the scan. Q253 The header row of a scanned sheet. Q254 The order and share of villa type sources.
Closed in 4j-1: Q247 Scan villa type takes the old CAD labels, by Bader's rule of 31 Oct 2026.
118 questions open, full text and the recommended answers: steps/questions-open.md. Asks of others: steps/waiting-on-others.md.

## Next three steps

1. Bader answers Q274, then Lane B ends 5d: the run id fix, window G again for 6, 7 and 8 and the second run
2. 6a on Lane B, Links mode joined from 5d, then Phase 6. Lane A 4j-2, floor split and corner walls, queued after 4i
3. Bader answers Q224 to Q242, Q255, Q256 of 4i (Q224 and Q255 first) and Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
