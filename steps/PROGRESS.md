STATE RUNNING, 4i and 5d, 2026-10-31 13:55
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- 4i done on 2026-10-31 in Lane A, one PR (D20): designBasis rev6 or roshnPastJob, R407 to R412, Q224 to Q244. ROSHN keeps roshnPastJob (Q224). 5e and 5f done on 2026-10-30, R381, R402
- Lane A is done for Phase 4 and 5: 4h-1 PR 107, 4h-2 PR 109, 4g3 on 2026-10-29
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- Lane C NEWPROJ-UI done on 2026-10-31, one PR: New project, How it works, pane text, R430 to R434, Q280 to Q287 (steps/HEADSUP.md)
- Lane B: 5c done on 2026-10-31, PR pending: buttons 2, 3, 4 and 7, Revit run on NBH 012 in Masses mode. Links not run (Q215), J10 measured, Q146 UNKNOWN, NBH 03 image not run (Q222). 5d next
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- 4i: one PR with code, tests and records (D20). Lane C NEWPROJ-UI: one PR with code, tests and records. 5e and 5f: one PR with code, tests and records (D20). 4h-2: PR 109. 4h-1: PR 107
- FOLD: PR 101 fold in, 102 design, 103 D9 to D19, 104 runner and guard, 105 answer pass and manual rules, then the end records
- Lane B 5c: one PR with code, tests and records (D20), PR pending. Lane B 5b: one PR (D20). 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
- 4g3: PR 83, 85 Q112 and Q113, 88 step 15, 91 the level Excel, 93 handover, 95 laptop start, 96 R365 the report Excel, then the end records
- 4g2: PR 74 to 81. 4g1: PR 69 to 73. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC: PR 56 to 60
- 4e PR 55, 4d PR 54, 4c PR 53, PR 52, 4b PR 51, 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

## Open bugs

- Full text at 73b8c42: steps/history/progress-addin-4e.md. Bugs no real input reaches: steps/latent.md (Q095)
- Q063: the whole NBH 03 image runs past the 30 minute step, I25 did not respond at 10,732 MiB private memory
- R310 logs free memory in every window with no stop at the floor. In 2a it fell to 0.53 to 1.42 GiB per read window, cause UNKNOWN
- Revit starts 9 to 11 child processes in a read (RC 11). rvt_info.py (R203) does not see cloud links, the SE models list 1 of 14
- The 1,409 spots of the SE models give no value, their STR links are not loaded in a read
- A read is tied to its key by the copy stem, the zone2 master and the IMC 2024 copy share one, only a hash parts them (Q065)
- ELl@Entry is off its schedule over 30 mm on 100 IMC and 115 IMD plots (Q070), no entrance spot gives El @ Entry (R327)
- The reader angleToTrueNorthDeg has 3 decimals and is not brought into 0 to 360, the placer takes 0 only
- A guard failure at the end of plan_from_book.py leaves the plan and step files of that run (LATENT, steps/latent.md)
- 4 yards that stand only by a mirror override read a status stronger than their evidence (Q163)
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146)
- Under rev6 the one road surface covers 630 of 2,704 plots (Q244), the past override list still applies (Q243)
- The build reads pastJob, not the pane Prototypes row (Q214). 4 Check dry run has no worker start (Q195). Base boundary open (Q206)

## Bader's turn

Opened in 5c: Q180 to Q223, their titles in steps/questions-open.md.
Opened in 4i, Lane A: Q224 The mode the ROSHN config runs. Q225 The level of a yard floor under Rev 6. Q226 The hand of a detached unit. Q227 Civil file
format and plot match. Q228 Default slopes of road plus slope. Q229 A row with no single cluster split. Q230 Value status of the Rev 6 values. Q231 Civil door
level where no road level. Q232 yardGrade in plan.json. Q233 Slope limit at 3 or 4 decimals. Q234 Read step road levels under Rev 6. Q235 Hand table cover per
unit code. Q236 Label size in the PDF check. Q237 Folder of the Rev 9 workbooks. Q238 TH1-IM in the cluster sequences. Q239 Mirror of DP2 and DP4. Q240 The 121
IMC plots of the audit. Q241 doorLevel.source reads as a note in R401. Q242 The reason of an unreadable input. Q243 The override list under rev6. Q244 The one
road surface covers a quarter.
Opened in NEWPROJ-UI, Lane C: Q280 Save setup writes the model at once. Q281 Board rows against the nine pane inputs. Q282 Where the project name is kept. Q283 Picks of layers, sheets, columns and
coordinates. Q284 Villa mode row the boards drop. Q285 Name the Rules row shows. Q286 Approve the four drafts of NEWPROJ-UI. Q287 Button messages that name a work file.
Closed in 5c, NEWPROJ-UI and 4i: none.
105 questions open, full text and the recommended answers: steps/questions-open.md. Asks of others: steps/waiting-on-others.md.

## Next three steps

1. Lane A 4j-2, floor split and corner walls, queued after 4i. Lane B 5d
2. Bader answers Q224 to Q244 of 4i, Q243 and Q224 first, then Phase 6
3. Bader answers Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
