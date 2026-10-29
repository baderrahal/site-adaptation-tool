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

- Status: done on 2026-10-29 in Lane A on the laptop, PR 95 the start records, PR 96 R365 the report Excel with D4, the C# checks of Q111 with 0 failures, then the end records. Q123 to Q125 stay open. Q122 answered on 2026-10-29, its code moved to 4h (D8). D5 to D7 of steps/decisions-2026-10-28.md hold the speed rules, the C# checks and the report round trip
- Done on the laptop: the C# reads and parity of PR 75 to 91 on the 7 keys and the whole project, net48 and net8.0, 228 parity lines and 0 failures, three build peaks per runtime, 973 to 1,064 MiB
- Done on the personal PC: PR 83 records, PR 85 Q112 by the fallback and Q113, PR 88 step 15 with roadAtFrontM, Q114 and the Q116 conflicts, PR 91 R364 the level Excel with the D4 round trip of its sheet Levels, then the handover records
- Left of 4g3 at the handover, all done or moved since (PR 96, Q111, Q122 to 4h by D8):
  - R365 the report Excel: the top line, the Summary table of buttons 1 to 7, the box Counts agree across outputs, the box Rules applied with "[n] plots" on each line, the sheets Summary, Placed, Skipped, Overrides, Assumed, Issues, Rules, and a Python second route that opens it against 7.6 and item 9 of 7.7
  - The D4 round trip of the report, not done: Placed keyed by the plot number in its first column, one row per plot and kind, the columns Villa type and Mirror marked editable in the header and listed in a Settings sheet, which also holds the build id and the SHA-256 of the plan (items 10 to 12 of 7.7). The level Excel holds FFL, El @ Entry and Steps marked, its editable list, the build id and the plan SHA-256 since PR 91
  - The C# checks on real data that this PC could not run: the C# read and build on real inputs, the parity of the status, walls, checkRows, overrides and summary steps on the 7 keys and the whole project on net48 and net8.0, the worker counts against the summary, the C# build time and peak (commands.build stays 2,200 until then)
  - Q122, the Clusters FFL by rule or as written, once Bader answers
  - The end records: 4g3 done here, the top entry of steps/log.md, PROGRESS to STATE WAITING for 4h, the usage row
- Rules: Q112 and Q113 first (Bader, 2026-10-28, steps/questions-closed.md). Q112: an override moved a value only if removing that one override changes it, the detail names the last applied override that moved it and the status takes the worst of every override that moved it and of the entries still read. Measure first the time and the peak of a build of the 7 keys with it, and if it adds more than 20 percent to either, do not code it, list all overrides that touched the inputs of the value instead and say so in the log. Q113: a value takes the worst status of its inputs and names the override, so the 512 values read RECOMMENDED. R364 the level Excel. R365 the report Excel, with the plot count on each line of Rules applied and the sheet Assumed, and the parity of step 15 with the count by field and status of R401
- Design: docs/design/05-plan-steps.md step 15, docs/design/03b-buttons-2-to-8.md (6 Levels, 8 Report), docs/design/07-mock.md 7.5 to 7.7
- Q114 and Q116 (Bader, 2026-10-28): Q114 aligns the field names of majority.decidedByQuestion and the build proof, then carries Q017 to the rotationDeg, insert, floors.yard.outline and parameters.CASE of 2302, in 4g3 if it fits four changes per PR, else a join item of 5c. Q116 lists each plot a rotationDeg override turned in the conflicts of the plan until its ramp moves to the open edge in 5d
- C# on real data (Bader, 2026-10-29, D6): the laptop has the memory, so 4g3 runs the C# reads and the C# parity on the whole project and the 7 keys on net48 and net8.0 for everything since PR 75, and three build peaks per runtime. A failure is a REAL finding of 4g3. This replaces the rule of 2026-10-28 for the personal PC
- D4 (Bader, 2026-10-28, closes Q121): the sheets Levels and Placed key each row by the plot in the first column, FFL, El @ Entry, Steps, Villa type and Mirror are marked editable and listed in Settings, which also holds the build id and the SHA-256 of the plan, items 10 to 12 of 7.7. The level Excel in PR 4, the report Excel in PR 5
- Note: Q113 would move 294 wall values from MEASURED weak to RECOMMENDED (READ, the breaker of PR 79 pass 2, no log), and the rows of the chip Assumed follow the status (R363)
- Done when: Q112 and Q113 hold in Python and C# with tests, parity holds for step 15, and each Excel holds the labels of 7.5 and 7.6 of the mock in the order of the mock, with the items of 7.7

## 4h, the scope, step state, settings and element tags

- Status: open, starts on the laptop once 4g3 is done (Bader, 2026-10-28)
- First PR (Bader, 2026-10-29, D8): Q122, the sheet Clusters of the level Excel with the columns FFL by rule and FFL as written, new items of 7.7. A draft sits on the local branch feat/addin-4g3-q122-clusters-ffl at 00d371f, untested
- Peak once per runtime and one review pass for a records-only PR (D8)
- Rules: R366 scope with the plot list syntax and the step state by hash. R367 the settings JSON kept in the project. R368 element tags, the match to the plan, the hand change and the schema versions. R369 the second method of Phase 4, seeds 14 and 7
- Design: docs/design/03a-buttons-shared-and-1.md (What every button shares, Steps and Scope), docs/design/06-settings.md
- Done when: each rule has its tests on both runners and the second method of Phase 4 runs at seeds 14 and 7 (R369, 10-rule-ids.md)

## Laptop check after 4h, before 5c

- Status: folded into 4g3 and 4h by D6 (Bader, 2026-10-29). No separate session
- What (Bader, 2026-10-28, by Q111, moved by D6): the C# reads, the parity on the whole project and the 7 keys on net48 and net8.0, and the build peak, run inside 4g3 for everything since PR 75 and inside 4h for 4h, into the top entry of steps/log.md
- Done when: 4g3 and 4h each hold their counts in their log entry and every failure is a REAL finding or a question, before 5c starts

## L0, the laptop check, light mode design and UI spec

- Status: done, Lane B, laptop, 28 Oct 2026, on the merge of branch docs/laptop-l0 after its review
- What: steps/laptop-check.md, docs/design/11-light-mode.md, docs/design/12-ui-spec.md and steps/decisions-2026-10-28.md D1 to D3 and U1 to U7

## 5a, the install, ribbon, pane and one transaction per button

- Status: open, Lane B. Revit run 3 ran on 29 Oct 2026, PR 84, 86, 87, 89, 90, 92, 97 and 99 merged, the close waits for Q147
- Rules: R370 install script, manifest, one Common. R371 the ribbon. R372 the pane bound to the active document, the work folder and the settings with the project. R382 one transaction per button, the confirm, the dry folder, the roll back and the stop
- Phase changes (Bader, 2026-10-28): R372 settings part waits for R367 of 4h, build the pane against a small settings reader, join at 5c. Q103 moves to 5c. The UI spec and Theme.xaml are part of 5a's done-when. Install per user only, no admin, into %APPDATA%\Autodesk\Revit\Addins\2024. Rule 2 of steps/lane-b.md first. R371 ribbon with the icons of docs/design/12-ui-spec.md. Theme.xaml with the gate test that fails on any colour or font size outside it, Libre Franklin bundled from google/fonts
- Design: docs/design/02-folders.md, docs/design/07-mock.md 7.1 and 7.2, docs/design/08-install.md, docs/design/09-risks.md open items 1 and 2, docs/design/12-ui-spec.md, docs/design/13-workflow-changes.md
- Done when: the add-in installs into Revit 2024 on the laptop with the ribbon and pane of 7.1 and 7.2 and the UI spec, open items 1 and 2 of docs/design/09-risks.md are measured in Revit, a screenshot of the ribbon and the pane is in steps/ui-shots/ beside a list of every known difference from the canvas, the tests run on both runners, and the four UI controls of docs/design/12-ui-spec.md part UI controls hold: the theme test, the text test, the picture test and the UI checklist in review

## 5b, button 1 Set up site

- Status: open, Lane B
- Rules: R373 button 1 on an empty project. R374 button 1 on an existing site file. R383 element tags in Revit, schema names, the library and memory line of each run
- Phase changes (Bader, 2026-10-28): R383 element tags wait for R368 of 4h, button 1 calls one tag step that 5c fills. R374 on the R17B file of kept\round_17 among the four copies of 3.1. The Set up site window by 7.3 of docs/design/12-ui-spec.md with its screenshot in steps/ui-shots/
- Join items: W1 of steps/lane-b.md
- Design: docs/design/03a-buttons-shared-and-1.md (1 Set up site), docs/design/07-mock.md 7.3, docs/design/09-risks.md open items 3 to 8, docs/design/13-workflow-changes.md part Set up site
- Done when: button 1 runs on the four copies of 3.1, each by the signs it holds, open items 3 to 8 are measured in Revit, and the four UI controls hold for the Set up site window

## 5c, buttons 2 Read data, 3 Review data, 4 Check and 7 Place villas

- Status: open, Lane B
- Phase changes (Bader, 2026-10-28): Rule 3 of steps/lane-b.md first, then every join item of steps/lane-b.md (J1 R367 settings, J2 R368 tags and the rest), then Q117, the optional inputs of button 4 Check, then Q103, then the buttons 2 Read data, 3 Review data, 4 Check and 7 Place villas with light mode (Masses and Links) and the check table of the UI spec with the plain rule names
- Join item (Q117, Bader 2026-10-28): key_join.json and the past model prototype reads as inputs of button 4 Check, both optional, as a new project has no past job. Present: the Plot cut rows and the key Unit code rows are built from them. Absent: button 4 runs as today, Plot cut shows no rows and the check table says in one line that no past job was given, so no plot is counted as following every rule on Plot cut. With the key reads present, Past of the 51 missing rows and 3 question rows that offer it with no value is filled too (CHOSEN by Code, PR 81 review 2)
- Q103 (Bader, 2026-10-27, moved from 5a): move the 19 design values a build or the add-in uses into config/project.json with a rule id, the rows of section 7 of steps/investigations/rule-status-audit.md that Q103 names, and leave the checkers of past rounds with a note
- Join items: W2 to W5, W9 to W11, J1 to J11, J13, J14 and J16 of steps/lane-b.md
- Rules: R375 button 2. R376 button 4 Check, the check table, Use and Save overrides. R377 button 7 Place villas, link types by full path, the load and reload step, the move of a tagged link
- Design: docs/design/03b-buttons-2-to-8.md (2 Read data, 4 Check, 7 Place villas), docs/design/07-mock.md 7.4 and item 8 of 7.7, the chip Assumed, docs/design/09-risks.md open item 9, docs/design/11-light-mode.md, docs/design/13-workflow-changes.md parts Review data and Site Adaptation Rules
- Done when: buttons 2, 3, 4 and 7 run on one NBH, open item 9 is proven on one link, the values of Q103 are in config/project.json with a rule id, and the four UI controls hold for the Review data, Site Adaptation Rules and Check table windows

## Revit run after 5c, on Bader's machine by Code

- Status: open, Lane B
- What: Code builds and installs the add-in and runs buttons 1 to 4 and 7 on one NBH from a new project through the test host, with the runner agent under the shared window lock and the 2 GB floor, on copies only, logs outside the repo. The test host runs 7 Place villas by Buttons.Run on the plan before 5 and 6 are built (CHOSEN in Round FOLD, the new order has 7 wait for 6 in the pane)
- Phase changes (Bader, 2026-10-28): in Masses mode, plus one NBH in Links. Measure the open item of the base boundary. Record the peak memory of one window in each mode, and run the whole NBH 03 image again (Q063) now that 64 GB is free
- Done when: the run log, the R310 samples and the hashes of every source before and after are filed, and every failure is a fix of 5c or a question, before 5d starts

## 5d, buttons 5 Floors and ramps, 6 Levels and 8 Walls, and 7 Place villas at the FFL

- Status: open, Lane B
- Rules: R378 button 5. R379 button 6 Levels. R380 button 8 Walls. RRE_ZZ_SP_Prototype-FFL is written by 7 Place villas, Levels writes no link parameter (W6 of steps/lane-b.md)
- Q115 and Q116 (Bader, 2026-10-28): before button 8 places walls, measure the wall extent per kind of edge against the villa footprints and code the best rule, weak under majority.strongShare. The ramp of a plot a rotationDeg override turned goes on the open edge the walls take, as the keys do on 18 of 18
- Join items: W6 of steps/lane-b.md
- Design: docs/design/03b-buttons-2-to-8.md (5 Floors and ramps, 6 Levels, 8 Walls), docs/design/09-risks.md open items 10 and 11, docs/design/13-workflow-changes.md part Order
- Done when (Bader, 2026-10-28): NBH 012 is placed in Masses with all 13 prototype shapes, then in Links, the peaks logged by R310, and open items 10 and 11 are measured

## 5e, button 9 Report and the test host jobs

- Status: open
- Rules: R381 button 9 Report. R384 the test host jobs of the buttons and the screenshots
- Join items: W7, J12 and J15 of steps/lane-b.md
- Design: docs/design/03b-buttons-2-to-8.md (9 Report), docs/design/07-mock.md 7.6 and item 9 of 7.7, the sheet Assumed, docs/design/08-install.md (Unattended runs for Phase 6), docs/design/09-risks.md open item 12
- Done when: the test host calls SiteAdaptation.AddIn.Buttons.Run for each button with no click, by a number for the 9 steps and by a name for Site Adaptation Rules and Update from report, and open item 12 is measured

## 5f, the button Update from report

- Status: open
- What (Bader, D4 of steps/decisions-2026-10-28.md): a button reads the level Excel and the report Excel back after the team edits them, refuses an Excel whose build id or plan SHA-256 differs from the plan of the model, lists each change of an editable column against the model, checks it against the rules and applies it as an override of the list. Its rule id is given when the phase starts (CHOSEN)
- Join items: W8 of steps/lane-b.md
- Design: items 10 to 12 of docs/design/07-mock.md 7.7, the sheets Levels and Placed and the Settings sheet of both Excel files, docs/design/13-workflow-changes.md part Update from report
- Done when: an edited copy of each Excel of one NBH gives the list of its changes, each one checked against the rules, the overrides it applies match the edits cell by cell, an Excel of another build is refused with one line, and the four UI controls hold for the Update from report window

## 6, end to end on all zones

- Status: open
- Rules: R389 to R399
- Design: docs/design/08-install.md, docs/design/07-mock.md 7.7, steps/history/round-addin/01-plan-0a.md (Phase 6)
- 6a: one NBH from a new project through all 9 steps in the test host, and the fixes
- 6b: each of the 7 models from DefaultMetric, one zone again from None, button 1 on the IMD 2024 copy changing nothing, compare tables per zone with the worst 10, 10 random plots per zone by id. Phase changes (Bader, 2026-10-28): Masses for all 7 zones, Links for one NBH. Zones run in parallel windows by the window rule (D3)
- 6c: the mock check by screenshots, both Excel files compared item by item, an image of every new file, the clean up, then steps/bader-addin.md for Bader's check of Part 1
- Done when: 6a to 6c are done as the plan of the round gives them, and Bader has the check sheet of Part 1
