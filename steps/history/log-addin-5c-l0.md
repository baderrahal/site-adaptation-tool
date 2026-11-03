# Round log, Lane B 5c back to L0, moved from steps/log.md on 3 Nov 2026

## Lane B 5c, 2026-10-31, buttons 2, 3, 4 and 7 and the Revit run after 5c, Lane B on the laptop

Goal:
- 5c of steps/01_next.md with the Revit run after 5c on NBH 012 in Masses mode, one PR (D20)

Done:
- D15 (RAN): 7 read steps, 14 parity lines, 0 failures. Q126 manifest 528 of 837 equal, 0 differ. Border made, 747 of 747 plots
- Buttons 2, 3, 4 and 7, review data, read data, rules window, plan file, check table, pane. Rules R375 to R377, R403 to R406
- Fixed with a test: pane hashed wrong folders, plot list scope, scope on the IM letter, 2 plan yard faults, worker path
- D20 reviews: reviewer, breaker and a second pass of both. 4 REAL faults, all fixed. The fixes after the second pass carry tests and the Revit run, no third pass
- R405 fix (c66b512): button 2 alone in a fresh session committed, 42 of 42 unchanged
- Step shape fix of the check table: 1237 rows on NBH 012 in window 10. Host fixes: layer list, data book, config folder, prototype folder, base shapes, host loops on copies

Checked:
- Gate at 52a9b26, RAN: net48 1503, net8.0 1326, pytest 2110 passed, 1 skipped
- Plan parity, RAN: 754,451 values equal, 0 differ, 8 covers, both runtimes. 248 inputs unchanged by hash
- Windows, RAN: probes 1, buttons 10, judge 3, button 2 check 1. Window 10 (exit 0): 336 solids placed and read back, 201.6 s for 7
- Plain judge second run: held 18, not held 2 (J10), not measured 1, peak 2145 MiB private. Button peak private 3906 MiB, least free 31.57 GiB
- Base boundary (Q206): other elements read on 13 of 13 prototypes, 0 of 336 solids hold at 1 mm

Remains:
- J10 measured, not held as before: a detached document keeps PathName and Title '_detached' (Q220)
- Q146 UNKNOWN. Q147 save stop held, pane first show and 16 px icons not measured (Q221)
- NBH 03 image of Q063 not run (Q222). Links not run (Q215). 5d, 6a to 6c, J11 and J14

Known bugs:
- The build ignores the pane Prototypes row (Q214). Host raise not retried (Q223). Latent lines: steps/latent.md

Next:
- 5d on Lane B after the PR of 5c merges

Your turn:
- Opened Q180 to Q223:
  Q180 designValues status in the R401 block. Q181 Timing of the labels J11 and J14. Q182 Side band of majority.side_neighbours. Q183 Time cap of a buttons job. Q184 Plain rule names of
  the check table. Q185 A plot list over two NBHs. Q186 Close J2 as no work. Q187 Prefill of a new project, J16. Q188 Entity load on a session unseen. Q189 Check table modal or modeless.
  Q190 Where buttons 5 to 8 find the plan. Q191 The plan name of a plot list. Q192 Solid height of light mode. Q193 Footprint limit of the base. Q194 RRE parameters on Generic Models.
  Q195 Dry run of button 4. Q196 addin.overrideFile in config. Q197 key_join.json and key reads as inputs. Q198 Past gaps of the missing and question rows. Q199 What the tool does per
  manual rule. Q200 Export to Excel of the two windows. Q201 Data book files with no Python. Q202 The schedule folder in config. Q203 A worker end on a document not active. Q204 Manual
  rule of a Review data row. Q205 Corner table coordinates of Review data. Q206 Base boundary of a prototype. Q207 No foot and no Esc in the windows. Q208 How to use line of Rules. Q209
  Manual column mixes section and revision. Q210 Q015 in a past job text. Q211 7 after a plot list 4 Check. Q212 4 Check stops 7 on another NBH. Q213 Use of an override entry under Skip.
  Q214 The build reads pastJob, not the pane. Q215 Where the 2024 prototypes come from. Q216 Rows of 4 Check with no past job. Q217 Follow every rule count, table against chip. Q218
  Config files beside a user's config. Q219 First run of 7 against the step limit. Q220 Judge rows of J10 test the old claim. Q221 Judge rows for Q146 and Q147. Q222 The NBH 03 image of
  Q063. Q223 The test host gives up after a refused raise.
- 76 questions open, full text: steps/questions-open.md

## Round ADDIN 5e and 5f, 2026-10-30, button 9 Report and Update from report, Lane A on the laptop

Goal:
- 5e (R381) and 5f (R402) of steps/01_next.md in one PR (D20), the last Lane A phases of Phase 4 and 5

Done:
- R381: the report Excel in the 9 steps (J12), a Manual rule cell on each line (W7), Q123 faces, Q124 Revit counts, Q125
- R381: the body of button 9, it stops with needs step 4 until 4 Check writes a run log (Q175)
- R402: Update from report, read back, refusal of another build, change list, rule checks, overrides, python/update_check.py
- The window Update from report (W8), its worker command, its labels and steps/ui-shots/update-from-report.png, Draft
- Open item 12 measured: 2 of 4 shots in our own Revit (5a run 3), the dialog and the check table after 5c

Checked:
- Gate, RAN after a laptop restart: Python 2,056 passed, 3 skipped. C# net48 1,262, net8.0 1,137, 0 failed. One R310 process test failed once before the restart and passed after it, both runtimes
- Report, whole project, RAN: 461,749 matches, 0 mismatches per runtime, 431,245 cells equal across runtimes and inputs
- NBH 012 Summary, RAN: 2 Read data 337 and 43 as 7.6 (Q123). 5 random plots at seed 21, 15 of 15 equal to the plan
- Update, RAN: real copy and probes, 13 cases, 127,844 cells equal, 0 mismatches between the C# and Python routes
- Reviewer and breaker (D20): pass 1 REAL faults fixed with tests, breaker pass 2 found 3 more, fixed with tests
- Breaker passes ran Python only, the C# side of the fixes rests on the tests and the route compare (UNKNOWN in Revit)
- CI of PR 110: the NBH of a file name now parts at either separator and the held open test runs on Windows only. R347 timing tests flaked once (latent)

Remains:
- Manual rule IDs read no block until Lane B 5c writes manualRules (W3, Q165). No button writes a run log yet (R383)
- No Revit run of 9 or of Update from report. docs/rules/07 is near the 100 KB cap, the next rule text needs a new part

Known bugs:
- R402 LATENT findings in steps/latent.md under 5e and 5f, the stale list on a hand edit while the window is open
- The window lacks the guide line, side box and row mark of the UI spec (Q179)

Next:
- Lane A is done for Phase 4 and 5. Lane B runs 5c, 5d. Phase 6 after them

Your turn:
- Opened: Q164 Levels count and the link Z of 7. Q165 Shape of the block manualRules. Q166 Manual rule of a check row
- Q167 Manual rule on the other sheets. Q168 The villa type of Update from report. Q169 Key of an update entry with none of its job
- Q170 updateChecks of manualRules. Q171 Which breaks untick a change. Q172 heightM gap with an El @ Entry edit
- Q173 Level profile of Update from report. Q174 The plan Update from report reads. Q175 Run log format 9 Report reads
- Q176 Plain names of the update rule checks. Q177 Manual rule ids in Rule check. Q178 A computed mirror edit breaks the hand rule
- Q179 Guide line, side box and row mark of Update. Closed: none
- 32 questions open, full text and the recommended answers: steps/questions-open.md

## Lane B L0 and 5a, 2026-10-28 to 2026-10-29, folded in by Round FOLD

Goal:
- L0 the laptop check, light mode and the UI spec. 5a the install, ribbon, pane and one transaction per button

Done:
- L0: steps/laptop-check.md, docs/design/11-light-mode.md, 12-ui-spec.md, D1 to D3 and U1 to U7
- 5a: PR 84, 86, 87, 89, 90, 92 (the 9 steps, the ribbon of four panels), 97 the fix of Q145 and Revit run 3, 99 the detached document
- The full end reports: steps/history/lane-b-2026-10-29.md

Checked:
- Revit run 3, RAN: 3.4 min, exit 0, peak 2,146 MiB private, 18 of 18 steps ok, judge held 17, not held 2, not measured 2
- Open item 1 held in full. The raised ExternalEvent ran on the Home screen with 0 documents open (RAN)
- Open item 2: a detached document never saved has a bare file name as PathName (RAN), fixed in PR 99 by SavedPath.Is

Remains:
- 5a close waits for Q147. Not seen in Revit: the save stop on a detached document, the first show of a hidden pane, the 16 px icons
- Bader's final approval of revit-ribbon.png and revit-pane.png

Known bugs:
- The tab title sits behind the overflow mark at 1536 logical px (Q146). The latent findings: steps/latent.md, part Lane B

Next:
- 5b Button 1 Set up site on Lane B

Your turn:
- Opened: Q146 Tab title hidden behind the overflow mark. Q147 Close 5a with three items not seen in Revit
- Closed: Q126 to Q145, the Lane B QL1 to QL20, answered by Bader on 28 and 29 Oct 2026
- 71 questions open, full text: steps/questions-open.md

