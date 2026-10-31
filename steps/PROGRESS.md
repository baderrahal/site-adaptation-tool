STATE RUNNING, 5d, 2026-10-31 13:03
# Progress
Live state of the tool, at most 60 lines and 6 KB with no line over 200 characters (R400). The state of 4e in full: steps/history/progress-addin-4e.md.

## State

- 5e and 5f done on 2026-10-30 in Lane A on the laptop, one PR (D20): R381 button 9 Report, R402 Update from report and its window
- Lane A is done for Phase 4 and 5: 4h-1 PR 107, 4h-2 PR 109, 4g3 on 2026-10-29
- Round FOLD done on 2026-10-29: the fold in of steps/lane-b.md, D9 to D19, the answer pass, M01 to M15. D20 on 29 Oct for Phase 4 and 5
- Lane C NEWPROJ-UI done on 2026-10-31, one PR: New project, How it works, pane text, R430 to R434, Q280 to Q287 (steps/HEADSUP.md)
- Lane B: 5c done on 2026-10-31, PR pending: buttons 2, 3, 4 and 7, Revit run on NBH 012 in Masses mode. Links not run (Q215), J10 measured, Q146 UNKNOWN, NBH 03 image not run (Q222). 5d next
- ADDIN Part 1 (R300 to R399): the add-in inside Revit 2024 in C#, net48, option B, parsons2024Rev6 for every build (Q050)
- Plan: steps/01_next.md. Design: docs/design/. Rules: docs/rules/index.md. Lane B: steps/lane-b.md. Part 2 waits for Bader's check of Part 1

## Done

- Lane C NEWPROJ-UI: one PR with code, tests and records. 5e and 5f: one PR with code, tests and records (D20). 4h-2: PR 109. 4h-1: PR 107
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
- The build reads pastJob, not the pane Prototypes row (Q214). 4 Check dry run has no worker start (Q195). Base boundary open (Q206)

## Bader's turn

Opened in 5c: Q180 designValues status in the R401 block. Q181 Timing of the labels J11 and J14. Q182 Side band of majority.side_neighbours. Q183 Time cap of a buttons job. Q184 Plain rule names of
the check table. Q185 A plot list over two NBHs. Q186 Close J2 as no work. Q187 Prefill of a new project, J16. Q188 Entity load on a session unseen. Q189 Check table modal or modeless. Q190 Where
buttons 5 to 8 find the plan. Q191 The plan name of a plot list. Q192 Solid height of light mode. Q193 Footprint limit of the base. Q194 RRE parameters on Generic Models. Q195 Dry run of button 4.
Q196 addin.overrideFile in config. Q197 key_join.json and key reads as inputs. Q198 Past gaps of the missing and question rows. Q199 What the tool does per manual rule. Q200 Export to Excel of the
two windows. Q201 Data book files with no Python. Q202 The schedule folder in config. Q203 A worker end on a document not active. Q204 Manual rule of a Review data row. Q205 Corner table coordinates
of Review data. Q206 Base boundary of a prototype. Q207 No foot and no Esc in the windows. Q208 How to use line of Rules. Q209 Manual column mixes section and revision. Q210 Q015 in a past job text.
Q211 7 after a plot list 4 Check. Q212 4 Check stops 7 on another NBH. Q213 Use of an override entry under Skip. Q214 The build reads pastJob, not the pane. Q215 Where the 2024 prototypes come from.
Q216 Rows of 4 Check with no past job. Q217 Follow every rule count, table against chip. Q218 Config files beside a user's config. Q219 First run of 7 against the step limit. Q220 Judge rows of J10
test the old claim. Q221 Judge rows for Q146 and Q147. Q222 The NBH 03 image of Q063. Q223 The test host gives up after a refused raise.
Opened in NEWPROJ-UI, Lane C: Q280 Save setup writes the model at once. Q281 Board rows against the nine pane inputs. Q282 Where the project name is kept. Q283 Picks of layers, sheets, columns and
coordinates. Q284 Villa mode row the boards drop. Q285 Name the Rules row shows. Q286 Approve the four drafts of NEWPROJ-UI. Q287 Button messages that name a work file.
Closed in 5c and NEWPROJ-UI: none.
84 questions open, full text and the recommended answers: steps/questions-open.md. Asks of others: steps/waiting-on-others.md.

## Next three steps

1. Merge the PR of 5c, Bader looks at the table of Q216 before 5d
2. 5d on Lane B, then Phase 6
3. Bader answers Q280 to Q287 of Lane C, then Part 2 after Bader's check of Part 1
