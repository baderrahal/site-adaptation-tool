# Next, the plan to finish Part 1

One phase per session (Q096). Bader pastes steps/START.md into a fresh session, which runs the first phase below marked open, merges, marks it done here, rewrites steps/PROGRESS.md and stops. The R ids are those of docs/design/10-rule-ids.md, the parity steps those of docs/design/05-plan-steps.md, the open items those of Open until a Revit run in docs/design/09-risks.md. Each done-when line is taken from the design. A phase reads its design parts by section, never a whole file over 30 KB.

## 4f, the outlines, levels, overrides and link parameters

- Status: done 2026-10-27, PR 61 to 66, findings in steps/investigations/round-addin.md under 4f
- Q089 first (Bader, 2026-10-27, steps/questions-closed.md): the build proof on the footprint clusters alone on the 7 keys with the overrides measured again. 0 unexplained on every key drops the analysis clusters from the build in Python and C# with tests, else the snapshot analysis.json is named in config as an input of button 3 and listed in 5c, with no build logic changed
- Rules: R358 ramp, generic and yard outlines and the ramp type. R359 levels, the row partners and the road at front, the road at front and El @ Entry from one unrounded mean. R360 overrides with the use and skip of R386. R361 link parameters and the link name
- Design: docs/design/05-plan-steps.md, parity steps 8, 10, 12 and 13 and Step 8, the Z of the ramp corners
- Done when: parity holds on every unit plot of the data book for steps 8, 10, 12 and 13, and each step has tests on small dummy data built in code that run on both runners (05-plan-steps.md, R345)

## 4g1, Q098, Q099 and the status of each plotted value

- Status: open
- Answers (Bader, 2026-10-27, steps/questions-closed.md): Q098 and Q099 yes, Q100 to Q103 yes, Q104 by Bader's own answer
- Q098 in the hand step (R357) and Q099 in the link parameters (R361, R107), in Python and C# with a test each
- The status block of rules/roshn-makkah.json, written by audit.py with DOCUMENT of Q100 and the shares of Q101, and steps/investigations/status-review.md, Bader's review list of Q104
- The status of each sourced value of a plot of the plan, as section 10 point 1 of steps/investigations/rule-status-audit.md gives it, and its count by field and status in the plan summary of step 15
- Design: docs/design/05-plan-steps.md steps 7, 13 and 15, steps/investigations/rule-status-audit.md sections 1, 8 and 10
- Done when: parity holds on the 7 keys and the whole project for the hands, the parameters and the status of each plotted value, and the summary of step 15 counts plots by field and status

## 4g2, the walls, issues and the two Excel files

- Status: open
- Rules: R362 walls and python/plan_walls.py. R363 issues and the check rows, python/check_rows.py, with the chip Assumed. R364 the level Excel. R365 the report Excel, with the plot count on each line of Rules applied and the sheet Assumed
- Design: docs/design/05-plan-steps.md steps 11, 14 and 15, docs/design/03b-buttons-2-to-8.md (6 Levels, 7 Walls, 8 Report), docs/design/07-mock.md 7.4 to 7.7
- Done when: parity holds for steps 11, 14 and 15, and each Excel holds the labels of 7.5 and 7.6 of the mock in the order of the mock, with the items of 7.7

## 4h, the scope, step state, settings and element tags

- Status: open
- Rules: R366 scope with the plot list syntax and the step state by hash. R367 the settings JSON kept in the project. R368 element tags, the match to the plan, the hand change and the schema versions. R369 the second method of Phase 4, seeds 14 and 7
- Design: docs/design/03a-buttons-shared-and-1.md (What every button shares, Steps and Scope), docs/design/06-settings.md
- Done when: each rule has its tests on both runners and the second method of Phase 4 runs at seeds 14 and 7 (R369, 10-rule-ids.md)

## 5a, the install, ribbon, pane and one transaction per button

- Status: open
- Rules: R370 install script, manifest, one Common. R371 the ribbon. R372 the pane bound to the active document, the work folder and the settings with the project. R382 one transaction per button, the confirm, the dry folder, the roll back and the stop
- Q103 (Bader, 2026-10-27): move the 19 design values a build or the add-in uses into config/project.json with a rule id, the rows of section 7 of steps/investigations/rule-status-audit.md that Q103 names, and leave the checkers of past rounds with a note
- Design: docs/design/02-folders.md, docs/design/07-mock.md 7.1 and 7.2, docs/design/08-install.md, docs/design/09-risks.md open items 1 and 2
- Done when: the add-in installs into Revit 2024 with the ribbon and pane of 7.1 and 7.2, and open items 1 and 2 are measured in Revit

## 5b, button 1 Set up site

- Status: open
- Rules: R373 button 1 on an empty project. R374 button 1 on an existing site file. R383 element tags in Revit, schema names, the library and memory line of each run
- Design: docs/design/03a-buttons-shared-and-1.md (1 Set up site), docs/design/07-mock.md 7.3, docs/design/09-risks.md open items 3 to 8
- Done when: button 1 runs on the four copies of 3.1, each by the signs it holds, and open items 3 to 8 are measured in Revit

## 5c, buttons 2 Read data, 3 Check and 4 Place villas

- Status: open
- Rules: R375 button 2. R376 button 3, the check table, Use and Save overrides. R377 button 4, link types by full path, the load and reload step, the move of a tagged link
- Design: docs/design/03b-buttons-2-to-8.md (2 Read data, 3 Check, 4 Place villas), docs/design/07-mock.md 7.4, docs/design/09-risks.md open item 9
- Done when: buttons 2 to 4 run on one NBH, and open item 9 is proven on one link

## Revit run after 5c, on Bader's machine by Code

- Status: open
- What: Code builds and installs the add-in and runs buttons 1 to 4 on one NBH from a new project through the test host, with the runner agent under the shared window lock and the 2 GB floor, on copies only, logs outside the repo
- Done when: the run log, the R310 samples and the hashes of every source before and after are filed, and every failure is a fix of 5c or a question, before 5d starts

## 5d, buttons 5 Floors and ramps, 6 Levels and 7 Walls

- Status: open
- Rules: R378 button 5. R379 button 6, RRE_ZZ_SP_Prototype-FFL written. R380 button 7
- Design: docs/design/03b-buttons-2-to-8.md (5 Floors and ramps, 6 Levels, 7 Walls), docs/design/09-risks.md open items 10 and 11
- Done when: NBH 012 is placed with all 13 prototype files in one window with the peak logged by R310, and open items 10 and 11 are measured

## 5e, button 8 Report and the test host jobs

- Status: open
- Rules: R381 button 8. R384 the test host jobs of the buttons and the screenshots
- Design: docs/design/03b-buttons-2-to-8.md (8 Report), docs/design/07-mock.md 7.6, docs/design/08-install.md (Unattended runs for Phase 6), docs/design/09-risks.md open item 12
- Done when: the test host calls SiteAdaptation.AddIn.Buttons.Run for each button with no click, and open item 12 is measured

## 6, end to end on all zones

- Status: open
- Rules: R389 to R399
- Design: docs/design/08-install.md, docs/design/07-mock.md 7.7, steps/history/round-addin/01-plan-0a.md (Phase 6)
- 6a: one NBH from a new project through all 8 buttons in the test host, and the fixes
- 6b: each of the 7 models from DefaultMetric, one zone again from None, button 1 on the IMD 2024 copy changing nothing, compare tables per zone with the worst 10, 10 random plots per zone by id
- 6c: the mock check by screenshots, both Excel files compared item by item, an image of every new file, the clean up, then steps/bader-addin.md for Bader's check of Part 1
- Done when: 6a to 6c are done as the plan of the round gives them, and Bader has the check sheet of Part 1
