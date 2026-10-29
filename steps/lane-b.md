# Lane B, the laptop lane

Phase 5 and 6 on Bader's company laptop (D1 of steps/decisions-2026-10-28.md). This file holds the Lane B phase table, the Lane B rules, the join items not yet done and the open Lane B questions. Everything it held before the fold in of Round FOLD, 2026-10-29, is in steps/history/lane-b-2026-10-29.md, its latent findings in steps/latent.md, its closed questions in steps/questions-closed.md.

## Phases

| Phase | Status |
|---|---|
| L0 Laptop check, light mode design, UI spec | done, Lane B, laptop, 28 Oct 2026 |
| 5a Install, ribbon, pane, one transaction per button | Revit run 3 ran on 29 Oct 2026, PR 97 and PR 99 merged, its close waits for Q147 |
| 5b Button 1 Set up site | open |

Lane B runs in the worktree "Site Adaptation Tool Lane B WT" by tools/run-lane.ps1 -Lane B, from steps/START-B.md (D17, D18). Its phases after 5b, by D10: 5c with the Revit run after 5c in one session, 5d, 6a, its share of 6b (IMD, IME, IMF, IMG), then 6c. Each phase's lane and what it starts when are in steps/01_next.md.

## Lane B rules, Bader 28 Oct 2026

- Rule 1, PRs across lanes: at most one open PR per lane, and never two open PRs that touch the same file. Lane B PR titles start with "Lane B", its branch names hold "laptop" or "lane-b". In CLAUDE.md since the fold in.
- Rule 2, Civil 3D: Civil 3D 2025 is installed here, the CAD reads of earlier rounds ran on Civil 3D 2026 on the PC. First thing in 5a, list every place a button or its worker needs Civil 3D, AutoCAD or a DWG reader at run time. Anything a button needs that is built against the 2026 libraries becomes a question before anything is built on it. The list is in steps/history/lane-b-2026-10-29.md, part Rule 2.
- Rule 3, data on the laptop (changed by D15, Bader 29 Oct 2026): no new copy from the personal PC. 5c checks the laptop inputs by key_copies.json and the hashes of Q126, then puts together the inputs the Q111 run of 4g3 lacked from C:-Site Adaptation and runs the 7 read steps 4g3 could not run, before any button work.
- Rule 4, setup report: if SA_COPY\setup_report.md of the OneDrive folder 02_ROSHN MAKKAH exists and steps/laptop-check.md does not hold it, its facts go into steps/laptop-check.md in the first records commit. At the start of 5a the file does not exist (RAN ls of SA_COPY), so nothing was copied.
- Window rule D3 and Q021 as in steps/decisions-2026-10-28.md. Revit probe first (D11), the real folders guard (D19). Revit runs on copies only, logs outside the repo, the peak memory of every run recorded, Code runs Revit through the test host and never asks Bader to click.
- Lane A's files, Bader 28 Oct 2026: Lane B never edits the plan builder, config/project.json, rules/, docs/rules/ or docs/design/ parts 01 to 10. Shared records only by the fold in rule.
- Memory, Bader 28 Oct 2026: Lane A runs builds of up to about 3 GB at the same time, so the windows of D3 are (free RAM minus 8 GB minus the Lane A builds running) divided by the peak of one window, rounded down, at least 1 and at most 3.

## Join items of Bader's changes of 28 Oct 2026

From docs/design/13-workflow-changes.md, part Where each lands. 5a builds the order, the ribbon and the pane in Lane B PR 8.

- W1, 5b. Set up site: the pass limit read from config by the answer of Q142, the Floor types row, the Ramp slope row and its dropdown, the Shared parameters row from the parameter sheet, the parameters and GUIDs only from that sheet, its PNG. The new labels of 7.3 are in the label list since review 1 of PR 92, the pass line as "Pass limit <n> mm" until Q142 is answered. The default 0.5% of Ramp slope is in the rules profile already, levelProfiles.parsons2024Rev6.rampSlopePercent.default beside rampTypes of rules/roshn-makkah.json, which the build reads (5a PR 8 review 2). Only the list of choices, 0.5%, 1%, 2%, then 3% to 15%, is new, and it goes beside rampTypes by Lane A or at the fold in (CLAUDE.md, no project value in code).
- W2, 5c. The pass limit of 0.1 mm in config by the answer of Q142 (Bader, 29 Oct 2026): a key of its own for the check points of button 1 against Revit, the Revit column after Apply against the transform of the dry run. addin.position.checkPointLimitMm stays at 1.0 for the key check of R353 (R211, Q086). Config is Lane A's file, so Lane A or the join adds the key and Code names it then.
- W3, 5c. The rules profile gains the block manualRules, M01 to M15 as drafted on the canvas, with id, text, group, manual section and the steps that check it. Code reads the page numbers from the manual PDF. The texts of M01 to M15 are on the canvas, not in the repo, so 5c asks Bader for them if no file holds them.
- W4, 5c. The pre-setting: Import from past job and Export with R367, never a drawing path or a coordinate.
- W5, 5c. The windows 3 Review data and Site Adaptation Rules, their labels in docs/design/14-ui-labels.md, their PNGs, and the manual rule ID on every check row and Review data row, with "Open in Site Adaptation Rules" in the Check table side box.
- W6, 5d. Place villas places each link at its FFL and writes Prototype-FFL, Levels writes no link parameter, in the buttons of the new order.
- W7, 5e. The manual rule ID on every report line.
- W8, 5f. Update from report, the whole window and its run, its labels and PNG.
- W9, 5c. R366, the step state of 4h, takes the 9 steps and their needs of docs/design/13-workflow-changes.md when the pane joins it. 4h builds from 03a, which keeps the eight buttons until the fold in.
- W10, 5c. The settings key parameterSheet of the Shared parameters row, named by Code in 5a PR 8, goes into docs/design/06-settings.md and R367 of 4h at the join.
- W11, 5c. The Check table window under its new number. Its title "4 Check, NBH <n>, <n> plots" is in the label list since review 1 of PR 92.

## Join items for 5c

Changes Lane B needs in files it may not edit while Phase 4 is open.

- J1. R367 settings: 5a builds the pane against a small settings reader, 5c joins it to R367 of 4h.
- J2. R368 tags: button 1 of 5b calls one tag step, 5c fills it from R368 of 4h.
- J3. Q103 moves from 5a to 5c.
- J4. The keys of light mode in config/project.json: the Villa mode default, the height of the solid and the limit of the base against the footprint (D2, Q129, Q130).
- J5. The 10 paths under C:\Users\bader, 9 in config/project.json and 1 in config/road_surfaces.json (Q127, steps/laptop-check.md item 14).
- J6. The rule id of light mode (D2). Bader gives it "at fold in", and docs/rules/ is outside Lane B while Phase 4 is open, so 5c writes it in docs/rules/ with the code and a test that names it, and names it in docs/design/11-light-mode.md. An id from R400 up must be a written rule with its test, as R401 is, and the planned ids of section 10 are R345 to R388, so the id cannot come in a records commit alone.
- J8. The code paths that take Documents\SiteAdaptation from the Windows Documents folder: dotnet/SiteAdaptation.TestHost/Run-TestHost.ps1, Run-Parallel.ps1, JobSession.cs and its job guard, dotnet/SiteAdaptation.Placer/PlaceFromPlan.cs and PlaceFromPlanCommand.cs, and the default work folder of docs/design/06-settings.md. Each reads the root of Q127.
- J9. The rule text of the Phase 5 ids Lane B codes before the fold in, R370, R371, R372 and R382 in 5a, R373, R374 and R383 in 5b, goes into docs/rules/07-addin-r354-r388.md and docs/rules/index.md at the fold in, from their rows of docs/design/10-rule-ids.md and what the code does. docs/rules/ is outside Lane B while Phase 4 is open, and no test ties a rule id to the index (RAN grep of python/tests), so the tests name the ids before the text is there.
- J7. D3 keeps a window lock per copy, while R200 holds one lock siteBuild.lock for every window and, while a process of siteBuild.windowProcesses runs, logs a wait and looks again after waitMinutes. Parallel windows need R200 changed before 6b. The peak per window is measured on one NBH, 6b opens whole zones, so the first 6b window measures its own peak before a second opens.
- J10, the Revit run after 5c. The run judge of Revit run 3 checks that a detached document has no PathName and an empty Title, the old claim of docs/design/06-settings.md. The design PR of the fold in of Round FOLD wrote what run 3 measured into 06-settings.md and open item 2 of docs/design/09-risks.md, so the Revit run after 5c judges a detached document by that text (Fold in items from Revit run 3, steps/history/lane-b-2026-10-29.md).
- J11, 5c. docs/addin-mock.md items 1, 2 and 4 and the 7.1 and 7.4 sentences of docs/design/07-mock.md keep the old button names, since python/tests/test_addin_design.py holds them word for word (MOCK_LABELS needs "3 Check", "4 Place villas", "7 Walls", "8 Report", and the sentence test all 35 sentences in order). The fold in wrote the new text beside them with the precedence stated. The phase that changes those labels changes the test with them.
- J12, 5e. The report Excel keeps the old numbers: ReportExcel.Steps, REPORT_STEP "8 Report", NO_LEVEL_RUN "no run of button 6 yet", addin.report.stepOfField and defaultStep 4 of config, and python/report_excel_check.py ties the 7.6 rows to "1. Ribbon" of docs/addin-mock.md. The 7.6 rows, 7.7 items 13 and 15 and addin.report.note stay as they are until then, item 13 marked in 7.7 item 35.
- J13, 5c and 5f. The rule ids of the windows Review data and Site Adaptation Rules take the next free id at 5c, Update from report at 5f. test_r343 holds the planned table of section 10 to R345 to R388 and the written ids, so no row could be added at the fold in.
- J14, 5c. Section 3 of the design keeps the title "The eight buttons", since the SECTIONS list of python/tests/test_addin_design.py holds it. A head note in 03a gives the nine steps.
- J15, 5e. With links placed after Levels, whether the Levels count of the box Counts agree takes the link Z of a run of 7 is open. R365 counts by the run of 6 today.
- J16, 5c. Item 34 of 7.7: the first open Start here state of 7.2 and the new project prefilled from config of section 6 do not say how they meet. Settle it with R367 and the pre-setting.

## Open Lane B questions

Q146 Tab title hidden behind the overflow mark. Q147 Close 5a with three items not seen in Revit. Full text in steps/questions-open.md.
