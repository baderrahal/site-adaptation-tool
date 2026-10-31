STATE RUNNING, 6a, 2026-10-31 20:29
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- Lane B 5d on 2026-10-31, code merged, phase open: buttons 5, 6 and 8, 7 at the FFL, Q115, Q116, Q214, Q215, Q156. In Revit 1 to 5 commit, 6 stops (Q274)
- Lane C NEWPROJ-UI done on 2026-10-31, PR 112: New project, How it works, pane text, R430 to R434, Q280 to Q287 (steps/HEADSUP.md)
- Lane B 5c done on 2026-10-31, PR 111: buttons 2, 3, 4 and 7, Revit run on NBH 012 in Masses mode
- 5e and 5f done on 2026-10-30 in Lane A, one PR (D20): R381 button 9 Report, R402 Update from report and its window
- Lane A is done for Phase 4 and 5: 4h-1 PR 107, 4h-2 PR 109, 4g3 on 2026-10-29
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- Lane B 5d: one PR with code, tests and records (D20). Lane C NEWPROJ-UI: PR 112. Lane B 5c: PR 111. 5e and 5f: PR 110
- 4h-2: PR 109. 4h-1: PR 107. FOLD: PR 101 to 105 and the end records
- Lane B 5b: one PR (D20). 5a: PR 84, 86, 87, 89, 90, 92, 97, 99. L0: branch docs/laptop-l0
- 4g3: PR 83, 85, 88, 91, 93, 95, 96 and the end records. 4g2: PR 74 to 81. 4g1: PR 69 to 73. AUDIT-RULES: PR 67, 68. 4f: PR 61 to 66
- REC: PR 56 to 60. 4e PR 55 to 4a PR 50, 3b to 0a PR 41 to 49. Round 17: PR 39, 40. DATA-5: PR 38. DATA-4: PR 36. 16: PR 34. Earlier: PR 1 to 32

## Open bugs

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

## Bader's turn

Opened in 5d, Lane B: Q260 Outline change of a placed floor. Q262 Level Excel and the read back values. Q263 Levels count still at its level. Q265 Ends status of walls after
Q115. Q266 Walls on edges other units meet. Q267 Base level of a boundary wall. Q268 Scope of a wall on two plots. Q269 Untagged BW wall on a plan wall. Q270 Where a prototype set
upgrades now. Q271 Python build takes the pane folder. Q273 6 Levels on a floor moved by hand. Q274 Run id of the levelExcel worker.
Closed in 5d: Q148, Q190, Q214, Q215, Q216, Q218, Q219, Q220, Q221 and Q156 on the recommended answers of 31 Oct 2026. Q261 Plots of 6 not written in plan, Q264 Should 6 rewrite the
floor tags and Q272 Front levels of a ramp on the open edge, closed by findings of 5d.
Opened in NEWPROJ-UI, Lane C: Q280 Save setup writes the model at once. Q281 Board rows against the nine pane inputs. Q282 Where the project name is kept. Q283 Picks of layers, sheets,
columns and coordinates. Q284 Villa mode row the boards drop. Q285 Name the Rules row shows. Q286 Approve the four drafts of NEWPROJ-UI. Q287 Button messages that name a work file.
86 questions open, full text and the recommended answers: steps/questions-open.md. Asks of others: steps/waiting-on-others.md.

## Next three steps

1. Bader answers Q274, then Lane B ends 5d: the run id fix, window G again for 6, 7 and 8 and the second run
2. 6a on Lane B, Links mode joined from 5d, then Phase 6
3. Bader answers Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
