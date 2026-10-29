STATE RUNNING, 5b, 2026-10-29 11:50
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- 4h-2 done on 2026-10-29 in Lane A on the laptop, one PR (D20): R366 scope and step state, R369, Q017, Q055, Q119, Q120
- 4h-1 done on 2026-10-29 in Lane A on the laptop, PR 107: Q122, R367 settings, R368 tags, D13, the D19 input guard, Q075
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- Two lanes in one repo, each a live session, tools/run-lane.ps1 retired: Lane A here, Lane B in the worktree Site Adaptation Tool Lane B WT
- 4g3 done on 2026-10-29 in Lane A on the laptop: R365 the report Excel with D4, the C# checks of Q111 with 0 failures
- Lane B: L0 done on 2026-10-28, 5a done on 2026-10-29 on the unit tests and Revit run 3 (Q147). 5b next
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- 4h-2: one PR with code, tests and records (D20). 4h-1: PR 107
- FOLD: PR 101 fold in, 102 design, 103 D9 to D19, 104 runner and guard, 105 answer pass and manual rules, then the end records
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
- Plot cut rows are not built until 5c (Q117), so Plot cut reads 0 and 7 cut plots of the IMC join count as following every rule on the whole project
- Report Excel: 2 Read data counts plan plots not faces, the Counts box holds the plan against itself, until 5e (Q123, Q124)
- A guard failure at the end of plan_from_book.py leaves the plan and step files of that run (LATENT, steps/latent.md)
- 4 yards that stand only by a mirror override read a status stronger than their evidence (Q158)
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146)

## Bader's turn

Opened in 4h-2: Q154 Q055 reads 11 more 1.4 plots off the ramp. Q155 A plot list takes unit plots only.
Q156 A changed step reruns every higher step. Q157 Reads of entryM with heightM or rampFrontM. Q158 A yard that stands only by its mirror.
11 questions open, full text and the recommended answers: steps/questions-open.md. Asks of others: steps/waiting-on-others.md.

## Next three steps

1. 5e and 5f together on Lane A, 5b on Lane B, each a live session
2. 5c with R366 joined at the pane and the Revit run after 5c on Lane B
3. Part 2 after Bader's check of Part 1
