# The plan from 12 Nov 2026

The one live plan, written again with Bader's answers of 12 Nov 03:25. The truth is docs/truth.md: the goal, the standing rules, the pass level and the decisions are there and are not repeated here. Older plans and forms are in steps/history.

Where it starts from, steps/audit-12nov.md and steps/old-project-pc.md: one neighbourhood, 01A, was built from an empty model on the laptop on 8 Nov, 263 of 278 plots, with 0 yard floors and the mirror value missing on 211 plots. Nothing built was ever held against a past model in Revit. The input folder holds no LandXML file, so no plot gets a road level on this PC until step a of round 1.

Words: the answer key is what the 7 past models hold, read into answer-key under the work root by task PC-1-revit-test. A compare number is a count of steps/compare.md, never a coordinate or a level.

## Round 0, done by this PR

Storage (steps/storage-12nov.md), the villa files of the input folder and the upgrade script, docs/truth.md, the write places check.

## Round 1, OLD-1

From 12 Nov 16:55 the way of working is docs/truth.md, Who does what: Cowork writes all tool code in the Cowork folder, Claude Code builds, tests, reports, commits and merges through the Bridge under the work root. Every code task from part 4 of step d on is a brief for Cowork, not a lane task. The revit-test run is held, and step e waits for Bader's order. A Revit run builds a small area, 01A, not all neighbourhoods, Bader 16:45.

Five steps in this order, each its own PR, merged on green, never two waiting. One open task per lane, each task a file in steps/tasks.

| Step | Task | Lane | What | Done when |
|---|---|---|---|---|
| a | OLD-1a-revit-test | revit-test | The surfaces of Roads\Makkah Phase 1_Road Surface C3D.dwg exported to LandXML with Civil 3D 2026, from a copy of the drawing under the work root, into landxml of the work root. Read back with python/landxml_reader.py. The copy deleted after | The record holds the count of surfaces and of points and faces read back per surface, every name of siteBuild.roadSurfaces of config is there or named as missing, the drawing unchanged by SHA-256, the copy gone |
| b | OLD-1b-revit-test | revit-test | The baseline run. The 3 cards on the whole old project from an empty model copy, reading the input folder in place with the past output folders skipped by settings. Card 1 once. Card 2 per neighbourhood, one per part under 110 minutes. Card 3 Report at the end. A neighbourhood that stops: its exact words, then the next | steps/revit-results/OLD-1-revit-test.md holds one row per neighbourhood with times and counts. The input folder unchanged by SHA-256 |
| c | OLD-1c-build-1 | build-1 | python/compare_built.py with its test: the built model against answer-key, joined by plot number, the columns and the pass level of docs/truth.md. It first counts each past model against itself as 100 percent | steps/compare.md written by the script, counts only, the same bytes on a second run |
| d | OLD-1d-build-2 | build-2 | Code cleanup by steps/audit-12nov.md, the list below | Build and all tests green, every function left has a caller, docs/truth.md labels the folders |
| e | OLD-1e-revit-test | revit-test | Step b again on the same neighbourhoods and step c again | The compare table equal to the baseline, or better with the reason. A worse number is a fault of step d, fixed before the round ends |

Step b needs one small code change first, in its own PR by build-1: the three other past output places skipped by settings. Today only 01_Output is skipped (dataCheck.skipFolders of config/project.json). Revit All Zones and Native Files join that list. The site models of 00_Revit Models are single files beside the folder of the villa files, and no setting skips a file today, so the task adds the smallest one, CHOSEN by Code, with its rule id and test.

Step d is split into 4 parts by Lead on 12 Nov, each its own PR, since the baseline run built 0 of 9 neighbourhoods on one cause, the read for the build asks for the data book of the past job (steps/revit-results/OLD-1-revit-test.md). Part 1 goes first so step e has a model to compare.

| Part | Task | Lane | What |
|---|---|---|---|
| 1 | OLD-1d1-build-2 | build-2 | Merged, PR 305. The build takes plots and labels from the read of card 1, R840, R843. The data book files of the old read of step 2 stay until part 4 |
| 2 | OLD-1d2-build-1 | build-1 | Merged, PR 306. 40 of the 42 dead parts went, 2 stay with their reason. R689 to R691 written. Q657 filed |
| 3 | OLD-1d3-build-2 | build-2 | Merged, PR 309. Advanced shows no step row, the ribbon holds Site Adaptation Rules and Show pane only, the 3 cards are the only way in. R845, R846. Q658 filed. Not seen in Revit |
| 4 | OLD-1d4, with Cowork | Cowork | The old read of step 2 and its data book files, past job mode and its override list, light mode, the old past job values of config, the Python copies of logic. The brief is Bridge\cowork-brief-OLD-1d4.md under the work root. build-1 was stopped part way on 12 Nov 16:55, its branch task/OLD-1d4-build-1 is not merged. R847 to R849, Q659 |
| first run of e | OLD-1e1-revit-test | revit-test | HELD by Bader, 12 Nov 16:55. 01A built 0 of 278 plots: the data book stop is gone, the Build stopped at its read twice on free memory, steps/revit-results/OLD-1e1-revit-test.md. No Revit run starts until Bader's next order |

The compare of step c is merged, PR 303: the 7 past models give 100 percent against themselves, the built side 0 of 9. Q650 to Q654 are filed.

Step d, what goes and what stays:

- Advanced no longer shows the 9 old step buttons. The 3 cards are the only way in. The step code stays only as the engine behind them.
- Removed: the 42 dead parts of the audit, past job mode and its override list, light mode, the data book reads, the old past job values of config, and the Python copies of logic the add-in holds in C#.
- Python stays only for dev tools: the compare, the readers, the checks. Each file kept has a line why.
- The rule ids R689 to R691 are written.
- Tests change with the code, never deleted to go green.
- Rule ids R830 to R849. Question ids Q650 to Q659. R830 to R839 and Q650 to Q654 are taken.

Not known yet: how many neighbourhoods card 1 names, and whether one neighbourhood fits in 110 minutes on this PC. The first part of step b measures both.

## Round 2, the fixes, worst first by the compare

The order below is by the numbers of 8 Nov and is set again by the first compare table. Each fix is rerun and compared, and is done only at the pass level on its neighbourhood and on one it was not tuned on.

| Task | What | Rule ids |
|---|---|---|
| Yard floor rule | The yard floor at FFL less 0.10 m, a project rule in the Rules window the team can change, source "Team decision 24 Oct, the manual gives no level". Q225 closes with this task, the rules profile cites it until then | R850 to R854 |
| Join option | A tick "Join surfaces for the road toposolid" under Road levels in Settings, on by default. The first picked surface wins where two overlap. Off keeps one surface and lists the plots with no toposolid on card 3. Saved per project. Card 3 names the surfaces used | R855 to R859 |
| Mirror direction | The plan lacks the mirror value on 211 of 278 plots of 01A | R860 to R864 |
| CASE and foundation type | They follow an assumed hand on about 201 plots | R865 to R869 |
| Plots with no road level | 13 of 278 plots of 01A had none on 8 Nov | R870 to R874 |
| F18 to F23 | Advanced run 2 reads the past job folder, a read has no Stop, Cancel loses the read, step 1 Done over reopen, a scope change undoes step 2 with no line | R875 to R879 |
| F35 to F39 | The pane faults that stop or mislead a Build | R880 to R884 |

Also, with the task of its lane: a manual rule a person changed shows "Changed from manual Rev 06" and the old value (docs/truth.md), and the i of the card heads.

## Round 3, UI-1, the identity, Bader 12 Nov 16:50

The windows and the pane take the look of docs/design/identity.md, with wpf/Theme.xaml and the Cairo fonts of docs/design/identity-kit. It starts after round 2, and only after Bader approves the boards: each window is drawn on a board first, a change asked on one board goes on every board of that window. The code is Cowork's, the brief is written by Lead when the boards are approved. Not started. The Parsons P mark waits outside the repo, docs/licences.md.

## Dropped or waiting, Bader 12 Nov

- NEW-1 the new project path and ROAD-1 roads from 3D solids: dropped.
- INST-1 the installer: waits until Bader asks.
- Whether the repo goes private: waits.
