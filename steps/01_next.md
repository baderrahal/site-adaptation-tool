# Next, the plan to finish Part 1

One phase per session (Q096). Bader pastes steps/START.md into a fresh session, which runs the first phase below marked open, merges, marks it done here, rewrites steps/PROGRESS.md and stops. The R ids are those of docs/design/10-rule-ids.md, the parity steps those of docs/design/05-plan-steps.md, the open items those of Open until a Revit run in docs/design/09-risks.md. Each done-when line is taken from the design. A phase reads its design parts by section, never a whole file over 30 KB.

## 4f, the outlines, levels, overrides and link parameters

- Status: done 2026-10-27, PR 61 to 66, findings in steps/investigations/round-addin.md under 4f
- Q089 first (Bader, 2026-10-27, steps/questions-closed.md): the build proof on the footprint clusters alone on the 7 keys with the overrides measured again. 0 unexplained on every key drops the analysis clusters from the build in Python and C# with tests, else the snapshot analysis.json is named in config as an input of button 3 and listed in 5c, with no build logic changed
- Rules: R358 ramp, generic and yard outlines and the ramp type. R359 levels, the row partners and the road at front, the road at front and El @ Entry from one unrounded mean. R360 overrides with the use and skip of R386. R361 link parameters and the link name
- Design: docs/design/05-plan-steps.md, parity steps 8, 10, 12 and 13 and Step 8, the Z of the ramp corners
- Done when: parity holds on every unit plot of the data book for steps 8, 10, 12 and 13, and each step has tests on small dummy data built in code that run on both runners (05-plan-steps.md, R345)

## 4g1, Q098, Q099 and the status of each plotted value

- Status: done 2026-10-27, PR 69 to 73, findings in steps/investigations/round-addin.md under 4g1
- Answers (Bader, 2026-10-27, steps/questions-closed.md): Q098 and Q099 yes, Q100 to Q103 yes, Q104 by Bader's own answer
- Q098 in the hand step (R357) and Q099 in the link parameters (R361, R107), in Python and C# with a test each
- The status block of rules/roshn-makkah.json, written by audit.py with DOCUMENT of Q100 and the shares of Q101, and steps/investigations/status-review.md, Bader's review list of Q104
- R401, the status of each plotted value, the next free rule id (CHOSEN): the status of each sourced value of a plot of the plan, as section 10 point 1 of steps/investigations/rule-status-audit.md gives it, and its count by field and status in the plan summary of step 15
- Design: docs/design/05-plan-steps.md steps 5 to 13 and 15, steps/investigations/rule-status-audit.md sections 1, 8 and 10
- Done when: parity holds on the 7 keys and the whole project for the hands, the parameters and the status of each plotted value, the summary of step 15 of Python counts plots by field and status, and steps/investigations/status-review.md holds Bader's review list of Q104. The parity of that count goes with step 15 in 4g3

## 4g2, Q105, Q107, Q109, Q110, the walls and the check rows

- Status: done on 2026-10-28, PR 74, 75, 77, 78, 79, 81 and the end records. Q111 the C# check waits for the laptop session
- Rules: Q105 in the hand step. Q107 in the status step and the build memory. Q109 the status block held by the gate. Q110 the status of prototypeFile. R362 walls and python/plan_walls.py. R363 issues and the check rows, python/check_rows.py, with the grouped chip Assumed of Q108
- Design: docs/design/05-plan-steps.md steps 7, 11, 14 and 15 (status), docs/design/03b-buttons-2-to-8.md (7 Walls), docs/design/07-mock.md 7.4 and 7.7
- Done when: parity holds for steps 7, 11, 14 and the status of step 15 on the Python side and in the C# unit tests, the C# parity and the build peak go to the C# check of Q111
- C# check (Q111): a laptop session runs the C# reads, the parity on the whole project and the 7 keys on net48 and net8.0, and the build peak, on main once 4g2 is marked done, and writes the result to steps/lane-b.md. Bader named PR 3, the whole project covers the walls PR and the check rows PR of 4g2 too (CHOSEN by Code)

## 4g3, Q112, Q113, the level and report Excel

- Status: open
- Rules: Q112 and Q113 first (Bader, 2026-10-28, steps/questions-closed.md). Q112: an override moved a value only if removing that one override changes it, the detail names the last applied override that moved it and the status takes the worst of every override that moved it and of the entries still read. Measure first the time and the peak of a build of the 7 keys with it, and if it adds more than 20 percent to either, do not code it, list all overrides that touched the inputs of the value instead and say so in the log. Q113: a value takes the worst status of its inputs and names the override, so the 512 values read RECOMMENDED. R364 the level Excel. R365 the report Excel, with the plot count on each line of Rules applied and the sheet Assumed, and the parity of step 15 with the count by field and status of R401
- Design: docs/design/05-plan-steps.md step 15, docs/design/03b-buttons-2-to-8.md (6 Levels, 8 Report), docs/design/07-mock.md 7.5 to 7.7
- Q114 and Q116 (Bader, 2026-10-28): Q114 aligns the field names of majority.decidedByQuestion and the build proof, then carries Q017 to the rotationDeg, insert, floors.yard.outline and parameters.CASE of 2302, in 4g3 if it fits four changes per PR, else a join item of 5c. Q116 lists each plot a rotationDeg override turned in the conflicts of the plan until its ramp moves to the open edge in 5d
- C# on real data (Bader, 2026-10-28, by Q111): this PC never has the memory a C# read needs, so 4g3 and 4h do not wait for it or retry it. C# by its unit tests on both runners, Python on the real data with its second method, the top log entry names the C# checks left for the laptop
- Lane B: when the C# check of Q111 is on main in steps/lane-b.md, fold it into the log. A failure there is a REAL finding of 4g3
- Note: Q113 would move 294 wall values from MEASURED weak to RECOMMENDED (READ, the breaker of PR 79 pass 2, no log), and the rows of the chip Assumed follow the status (R363)
- Done when: Q112 and Q113 hold in Python and C# with tests, parity holds for step 15, and each Excel holds the labels of 7.5 and 7.6 of the mock in the order of the mock, with the items of 7.7

## 4h, the scope, step state, settings and element tags

- Status: open
- Rules: R366 scope with the plot list syntax and the step state by hash. R367 the settings JSON kept in the project. R368 element tags, the match to the plan, the hand change and the schema versions. R369 the second method of Phase 4, seeds 14 and 7
- Design: docs/design/03a-buttons-shared-and-1.md (What every button shares, Steps and Scope), docs/design/06-settings.md
- Done when: each rule has its tests on both runners and the second method of Phase 4 runs at seeds 14 and 7 (R369, 10-rule-ids.md)

## Laptop check after 4h, before 5c

- Status: open
- What (Bader, 2026-10-28, by Q111): one laptop session runs the C# reads, the parity on the whole project and the 7 keys on net48 and net8.0, and the build peak, for all of 4g3 and 4h together, into steps/lane-b.md
- Done when: the counts are on main in steps/lane-b.md and every failure is a REAL finding or a question, before 5c starts

## 5a, the install, ribbon, pane and one transaction per button

- Status: open
- Rules: R370 install script, manifest, one Common. R371 the ribbon. R372 the pane bound to the active document, the work folder and the settings with the project. R382 one transaction per button, the confirm, the dry folder, the roll back and the stop
- Q103 (Bader, 2026-10-27): move the 19 design values a build or the add-in uses into config/project.json with a rule id, the rows of section 7 of steps/investigations/rule-status-audit.md that Q103 names, and leave the checkers of past rounds with a note
- Design: docs/design/02-folders.md, docs/design/07-mock.md 7.1 and 7.2, docs/design/08-install.md, docs/design/09-risks.md open items 1 and 2
- Done when: the add-in installs into Revit 2024 with the ribbon and pane of 7.1 and 7.2, open items 1 and 2 are measured in Revit, and the values of Q103 are in config/project.json with a rule id

## 5b, button 1 Set up site

- Status: open
- Rules: R373 button 1 on an empty project. R374 button 1 on an existing site file. R383 element tags in Revit, schema names, the library and memory line of each run
- Design: docs/design/03a-buttons-shared-and-1.md (1 Set up site), docs/design/07-mock.md 7.3, docs/design/09-risks.md open items 3 to 8
- Done when: button 1 runs on the four copies of 3.1, each by the signs it holds, and open items 3 to 8 are measured in Revit

## 5c, buttons 2 Read data, 3 Check and 4 Place villas

- Status: open
- Join item (Q117, Bader 2026-10-28): key_join.json and the past model prototype reads as inputs of button 3, both optional, as a new project has no past job. Present: the Plot cut rows and the key Unit code rows are built from them. Absent: button 3 runs as today, Plot cut shows no rows and the check table says in one line that no past job was given, so no plot is counted as following every rule on Plot cut. With the key reads present, Past of the 51 missing rows and 3 question rows that offer it with no value is filled too (CHOSEN by Code, PR 81 review 2)
- Rules: R375 button 2. R376 button 3, the check table, Use and Save overrides. R377 button 4, link types by full path, the load and reload step, the move of a tagged link
- Design: docs/design/03b-buttons-2-to-8.md (2 Read data, 3 Check, 4 Place villas), docs/design/07-mock.md 7.4 and item 8 of 7.7, the chip Assumed, docs/design/09-risks.md open item 9
- Done when: buttons 2 to 4 run on one NBH, and open item 9 is proven on one link

## Revit run after 5c, on Bader's machine by Code

- Status: open
- What: Code builds and installs the add-in and runs buttons 1 to 4 on one NBH from a new project through the test host, with the runner agent under the shared window lock and the 2 GB floor, on copies only, logs outside the repo
- Done when: the run log, the R310 samples and the hashes of every source before and after are filed, and every failure is a fix of 5c or a question, before 5d starts

## 5d, buttons 5 Floors and ramps, 6 Levels and 7 Walls

- Status: open
- Rules: R378 button 5. R379 button 6, RRE_ZZ_SP_Prototype-FFL written. R380 button 7
- Q115 and Q116 (Bader, 2026-10-28): before button 7 places walls, measure the wall extent per kind of edge against the villa footprints and code the best rule, weak under majority.strongShare. The ramp of a plot a rotationDeg override turned goes on the open edge the walls take, as the keys do on 18 of 18
- Design: docs/design/03b-buttons-2-to-8.md (5 Floors and ramps, 6 Levels, 7 Walls), docs/design/09-risks.md open items 10 and 11
- Done when: NBH 012 is placed with all 13 prototype files in one window with the peak logged by R310, and open items 10 and 11 are measured

## 5e, button 8 Report and the test host jobs

- Status: open
- Rules: R381 button 8. R384 the test host jobs of the buttons and the screenshots
- Design: docs/design/03b-buttons-2-to-8.md (8 Report), docs/design/07-mock.md 7.6 and item 9 of 7.7, the sheet Assumed, docs/design/08-install.md (Unattended runs for Phase 6), docs/design/09-risks.md open item 12
- Done when: the test host calls SiteAdaptation.AddIn.Buttons.Run for each button with no click, and open item 12 is measured

## 6, end to end on all zones

- Status: open
- Rules: R389 to R399
- Design: docs/design/08-install.md, docs/design/07-mock.md 7.7, steps/history/round-addin/01-plan-0a.md (Phase 6)
- 6a: one NBH from a new project through all 8 buttons in the test host, and the fixes
- 6b: each of the 7 models from DefaultMetric, one zone again from None, button 1 on the IMD 2024 copy changing nothing, compare tables per zone with the worst 10, 10 random plots per zone by id
- 6c: the mock check by screenshots, both Excel files compared item by item, an image of every new file, the clean up, then steps/bader-addin.md for Bader's check of Part 1
- Done when: 6a to 6c are done as the plan of the round gives them, and Bader has the check sheet of Part 1
