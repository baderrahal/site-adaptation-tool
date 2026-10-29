STATE RUNNING, 4h-1 and 5b, 2026-10-29 12:43
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- Round FOLD done on 2026-10-29 on the laptop: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15 in docs/design/15-manual-rules.md
- Two lanes in one repo by tools/run-lane.ps1 (D17): Lane A in this folder, Lane B in the worktree Site Adaptation Tool Lane B WT
- 4g3 done on 2026-10-29 in Lane A on the laptop: R365 the report Excel with D4, the C# checks of Q111 with 0 failures
- Lane B: L0 done on 2026-10-28, 5a done on 2026-10-29 on the unit tests and Revit run 3 (Q147). 5b next
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

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
- A native2022 build with --overrides is refused on 1,346 native entries with no value (Q075)
- Plot cut rows are not built until 5c (Q117), so Plot cut reads 0 and 7 cut plots of the IMC join count as following every rule on the whole project
- Report Excel: 2 Read data counts plan plots not faces, the Counts box holds the plan against itself, until 5e (Q123, Q124)
- rule_status.py reads the open questions from Bader's turn, so it holds every open ID until 4h-1 (D13)
- The tab title of the ribbon sits behind the overflow mark at 1536 logical px (Q146)

## Bader's turn

Opened in Round FOLD, open: Q148 Place villas before Floors and Levels after 5c. Q149 M14 boundary wall of an irregular plot.
Q150 M15 foundation depth, at least or equal.
Closed in Round FOLD: 92, Q021, the 22 Lane B questions and 69 more in the answer pass, ids in steps/questions-closed.md. Asks of others: steps/waiting-on-others.md.
3 questions open, full text and the recommended answers: steps/questions-open.md.
Q017 is closed, its status block held at RECOMMENDED until 4h-2 updates the two tests of R401 that pin it (join item).

## Next three steps

1. 4h-1 on Lane A, 5b on Lane B, both by tools/run-lane.ps1
2. 4h-2 on Lane A, then 5c with the Revit run after 5c on Lane B
3. Part 2 after Bader's check of Part 1
