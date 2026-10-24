# Round log

Newest entry first. The top entry is one page at most. Older rounds keep a short stub here and the detail lives in steps/history/.

## Round 15b, 2026-10-24, placer step 2, Generic, yard and ramp floors, eight scenarios in Revit

Done:
- Session started in the 15a folder while the 15a session was busy there, so that folder was only read. Work in the main folder on feat/placer-floors-15b. The main guard hook read the branch of the start folder, which went back to main, so it blocked every commit of this session. At Bader's answer the first commit went through a script that refuses main, PR 19. Plan written before the first edit. Gate A passed 08:22 (PR 18, main 6e0da3d). Gate B passed, 13 prototypes Format 2024 by BasicFileInfo, no Revit running. Free memory was 3 to 3.6 GB the whole round, under the 8 GB of R110, so every Revit run was one window alone. Full record: steps/history/round-15b.md
- Gate: Python 620 to 680 passed and 1 skipped (no symlink rights), .NET 319 to 347 passed, locally
- Hook fix (Bader answer 2): guard-main.sh now takes the branch of the repo each git command acts on (cd, Set-Location, git -C, the hook cwd), not of the start folder. It splits on separators outside quotes, so a message or a heredoc that holds the words is no command, and walks bash -c and powershell -Command. Tests on real repos: a session started on main elsewhere, a worktree, git -C, cd, PowerShell paths, push, main blocked. A 27 KB command with 280 git segments takes 0.6 s. The second commit went through the fixed hook: it was copied into the 15a folder this session loads its hook from, and a commit on main there was blocked by it
- link_summary.py takes the Generic and yard type names from config/project.json floors (answer 6), no Python file holds a floor type name now
- Plot 2311 (answer 4), read in the baseplan output: face 326 holds one text, OS 326, an open space of 150 m2, and face 2301 holds VL4 2301, 300 m2. The labels are right and no face holds two plots. The past model sets its DP2 pair 2311 and 2312 half a plot over: each Generic floor lies 0.5 on face 2301 and 0.5 on face 326 or 2302, and the link of 2311 stands in face 326. So in that block the baseplan cut (two VL4 plots and an open space) is not the plot cut of the past model. 10 more Generic floors lie 0.75 to 0.83 on one face (2375 to 2378, 2387 to 2390, 2544, 2545), their yards still resolve. The yard rule is not changed
- Rules R111 to R119: yard rule, floors in the plan, floor step and its stops, plot value and skip with ramp corners, geometry, run and report, test host floor jobs, work file life cycle, floor check
- Plan: 337 plots, Generic 336, yard 336, ramp 337 (Ramps 0.5% 280, Ramps 2% 47, Flat Ramps 10). Yard rule: 337 yards assigned, 0 ties, 0 low share, 1 goes to official 336, which is no plan plot. Listed: 2441 no Generic floor, 2311 no yard. No arc in the past model. 47 yards are two pieces, both kept
- API: Floor.Create with CurveLoops of Line.CreateBound and Arc.Create through 3 points, FLOOR_HEIGHTABOVELEVEL_PARAM, and for ramps SlabShapeEditor.Enable and ModifySubElement per corner from the lowest corner, because the past ramps are shape edited and this sets each corner exactly. Two regenerations per run
- Review: 9 points, all fixed with tests (arc loop taken for a second piece, past data hid outline faults, equal ignored the plan, uncompared and filtered floors passed, sliver yards, second method, one plot list in the run script, delete before refuse, ramp read back). A second short review found 6 small points (arc chords too coarse, two rule texts, a WORK name in clean-old, an empty plot list, a crash in the second method), all fixed with tests. S1 and S8 rerun on fresh copies after the fixes, both pass. The last small fixes were checked by the suite and the S1 and S8 checks, not by a third review

| Scenario | Runs | Run min | Result | Against the answer key |
|---|---|---|---|---|
| Master | 1 | 1.1 | ok | 14 links unloaded, 1010 floors deleted, 0 left |
| S1 full | 2 | 1.8 | pass | 1009 placed, all checks equal, outline worst 0 mm, 288 yard tops past data |
| S2 run twice | 1 | 0.7 | pass | both runs placed 0, 1009 skipped as placed, 2 not in plan, 1009 before and after |
| S3 dry run | 1 | 0.5 | pass | would place 1009 on 337 plots, model 0 floors |
| S4 partial | 1 | 0.7 | pass | seed 15, 10 plots, 30 deleted, 30 placed, 981 skipped, all 337 match |
| S5 missing type | 1 | 0.5 | pass | stopped before any change, 0 before and after |
| S6 rollback | 1 | 0.6 | pass | crossed loop of plot 2458 refused by Revit, all rolled back, 0 after |
| S7 ramp types | 0 | 0 | pass | 0.5% 280 of 280, 2% 47 of 47, Flat 10 of 10, arc plots 0 |
| S8 links and floors | 2 | 0.7 | pass | C10 15 links within 1 mm, 45 floors equal, 15 yard tops past data |

- Deleted: the 27 TEST files of the 15a test, 1,413,824,512 bytes, each listed in the history file, then the work files S1 to S8 after their checks. Left: 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt, 48,742,400 bytes, links unloaded, no floor of the five types. The site model was unchanged by hash after every run

Remains:
- After the merge: delete the 15a folder and the 13 _backup folders of Prototypes_2024 (answer 7), listed in the reply of the round
- Not run: parallel windows (memory under 8 GB), a plan with arcs in Revit (the past model has none, proven on fakes only)

Known bugs:
- Yard depth, a question for the architect: the plan keeps FFL minus 0.100 on every yard (Bader). The past model has FFL minus 0.100 on TH1, TH3 and VL6 only and the FFL on every other unit, so 288 yard tops differ from it and are counted as past data
- Past model data: each full run gives 767 Revit warnings Highlighted floors overlap, since the floors are placed on the outlines of the past model to 0 mm and those overlap there too
- The past plots 2311 and 2312 do not follow the baseplan cut, so plot 2311 has no yard (see Done)
- A second piece of a floor comes from the top face outline, so an arc on it would be chords

Next:
- Merge, then the combined Revit test with parallel windows once more than 8 GB is free
- Fill plan.json from CAD and LandXML in the same format

Your turn:
1. Ask the architect for the yard depth per unit. Recommended: ask with the list above, the plan keeps FFL minus 0.100 until the answer
2. Plots 2311 and 2312. Recommended: ask which plot cut is current in that block, the baseplan (VL4 2301, VL4 2302, OS 326) or the past model (DP2 pair), before the CAD plan is built for it
3. Free memory for the parallel windows. Recommended: close what you can so more than 8 GB is free before the combined test

## Round 15a test, 2026-10-24, the test host drives Revit, one prototype per run

Done:
- Folder C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a, a fresh clone of main at e73da18 (the session folder was empty, the main folder ran 12b), branch test/revit-testhost-15a. The main guard hook loaded at session start, the new registry hook is not live in this session. Main moved to 907bb0c (Round 13, PR 17), merged into the branch, my rules renumbered to R102 to R110 and their tests with them, no Round 13 file changed. Plan written before the first edit. Full record: steps/history/round-15a-test.md
- Gate: Python 620 passed, .NET 319 passed (main had 573 and 205), locally. On GitHub the Linux Python job fails one Round 13 test, as on PR 17, see Known bugs
- Rules: R102 placer filter, R103 test host guard, R104 steps and log, R105 placer_check.py, R106 registry hook, R107 link instance Name, R108 security box watcher, R109 upgrade job, R110 parallel windows. R80, R81, R89 amended for linkName and the 2024 folder
- Answer key copied once to prototypes_original_imc.json, 10,355,725 bytes, 2026-10-24 01:51:15 kept, hash equal. Plan: 337 of 337 plots, linkName on 337, 2 differ from RVT-Name and are conflicts
- The watcher answered 5 boxes per Revit start: Do Not Load for RCRC Green and RCRC Green Audit, Load Once for the reader, the placer and the test host (Bader). The boxes show no GUID, the publisher is logged as vendor
- Prototype runs, each on a fresh TEST copy, first run each, original model unchanged every time:

| Code | Folder | Runs | Run min | Plots placed | Result |
|---|---|---|---|---|---|
| C10 | 2021 | 1 | 3.7 | 15 of 15 | pass |
| TH1 | 2021 | 1 | 4.0 | 16 of 16 | pass |
| VD2 | 2021 | 1 | 2.4 | 29 of 29 | pass |
| DP1 | 2021 | 1 | 4.2 | 34 of 34 | pass |
| DP2 | 2021 | 1 | 2.3 | 12 of 12 | pass |
| DP3 | 2021 | 1 | 2.4 | 10 of 10 | pass |
| DP4 | 2021 | 1 | 2.3 | 32 of 32 | pass |
| DT1 | 2021 | 1 | 3.2 | 44 of 44 | pass |
| DT3 | 2024 | 1 | 1.1 | 68 of 68 | pass |
| TH3 | 2024 | 1 | 0.9 | 16 of 16 | pass |
| VD4 | 2024 | 1 | 0.9 | 27 of 27 | pass |
| VL4 | 2024 | 1 | 0.8 | 15 of 15 | pass |
| VL6 | 2024 | 1 | 0.9 | 19 of 19 | pass |

- Every check: position within 1 mm (worst 0 mm), rotation, mirror, Z equal FFL, instance Name and the 9 parameters equal on every plot, missing, extra and skipped 0. RRE_ZZ_SP_Prototype-FFL empty on every new link. Reader fixes: every loaded link has a box, spots with a host 986 of 987. Second method 3 of 3 on every run, seeds in the history file. PowerShell spot check of 5 random plots, seed 24: 5 of 5
- Upgrade: the 13 prototypes saved as Revit 2024 centrals in Zone 02\ARC\Prototypes_2024, one per run, one after another, 07:24 to 07:57. Sources unchanged. Each new file Format 2024 by BasicFileInfo and again from its bytes, same names. Upgrade time 86 to 142 s, sizes in the history file. plan.json gives the 2024 folder to the placer, the 2021 folder stays in the config as the source
- Review: a fresh reviewer found no way to make a normal session act and 9 edge points, all fixed with tests

Remains:
- Not proven in Revit: the parallel windows of R110 (one job file and token per window, per job outputs) and Run-Parallel.ps1. They wait for the combined test after Round 15b. The parallel upgrade was not run: the 13 targets already exist and are never overwritten, and free memory was 5.2 of 15.8 GB, under the 8 GB rule
- The R110 guard had no fresh review

Known bugs:
- My first install recreated the CodeSigning key in the registry and removed four Always Load answers of other add-ins. Restoring them was blocked and you said to leave them. Since then nothing in the registry was read or written
- On 9 of 13 runs TransmissionData did not get the prototype loaded at open. The placer loads it anyway, so no result changed
- 1 spot of 987 has no host, element 996508
- On the Linux CI runner test_r96_entries_without_analysis_are_refused_in_one_line of Round 13 fails, it looks for the real schedules first. Not changed, it is a Round 13 file. test_r80 failed there since PR 15 and is fixed here (the file name of a Windows path on Linux)

Next:
- Round 15b, then the combined Revit test with parallel windows on the 2024 copies

Your turn:
1. Free memory for the parallel windows. Recommended: close what you can so more than 8 GB is free before the combined test. The peak memory of one window was not measured in this round, Run-TestHost.ps1 logs it from now on
2. You can delete C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a. Next rounds work in the main folder or a git worktree. Recommended: yes, after this merge
3. The TEST copies in Zone 02\ARC (14 models and their backups) and the 13 _backup folders in Prototypes_2024 stay. Recommended: delete the TEST copies when you no longer need them, keep Prototypes_2024
4. The yard rule of Round 15a is still open. Recommended: as in the Round 15a entry

## Round 13, 2026-10-24, unit codes, road levels, entry level per plot, block to link offset

Done:
- pwd was the main folder, the worktree is C:\Users\bader\Documents\GitHub\site-adaptation-tool-13 on branch round/road-levels-offsets from main at 0b7c9ad, pull request 17. The plan was written before the first edit. Main did not move during the round, so no rebase. The main guard hook checks the branch of the folder the session started in (main) and blocked every git commit, also in the worktree. At your answer I committed and pushed through a script file that refuses main. No commit went to main. The round 15a session was not touched
- Gate: Python 468 to 573 passed, .NET 205 passed. Rules R90 to R101, R69 R70 R74 R64 R63 amended. Reviewer in its own worktree found 13 points, 8 fixed with tests (role check so the ground cannot answer, entry levels kept to 4 decimals, pairRule must be a bool, odd scale, unit position ties, --entries without --analysis, project name out of Python, p95). Rest under Known bugs. A second short review of the fixes found 5 points, 4 fixed with tests (p95 nearest rank, a config that asks no surface, a surface that is no object, a scale that is not finite) and the unit position now counts the local x to the metre, so centimetre jitter cannot swap positions. Those last fixes were checked by the suite, not by a third review
- Part 0. Unit code is the Excel unit only (R69, R74). Clusters before to after: 535 (M02 153, M04 293, unknown 89, Q02 0) to 225 (M01 5, M02 30, M04 32, M05 50, M06 71, M07 19, M08 6, M11 12, unknown 0, Q02 0). Unit codes: excel 747, none 1831. The pair rule is off (analysis.pairRule false), with it on 38 clusters turn unknown. Block hints (R90): 548 blocks, 157 equal one schedule cluster, 10 differ, 381 have no schedule plot. The client type of the block gives the Rev 6 cluster code on 0 of the 157. 157 of the 225 clusters have a block group, of the other 68 41 sit under two single villa blocks, 21 under no block. Scan for a source for the other plots: the master plan workbook has 9 sheets of totals per client type and no plot row, baseplan, V9, R06 and footprint texts hold no Rev 6 code, V9 has 2781 plot numbers and no type. Block attributes are not read by the CAD reader. Nothing found, nothing used
- Part 1. Order is the file order with Top Surface-02 first, you set none. The 337 ramps: 414 of 1348 corners answered, all by Top Surface-02. Of the 119 ramps with no covered corner 0 gain a corner from a later surface. The road covers the lower long edge of the ramp on all 150 ramps with two neighbouring covered corners on a long edge, 12 on a short edge
- Part 2. Entry point of a ramp: midpoint of the higher long edge. 337 plots, median -2 mm, within 5 mm 205, 10 mm 238, 30 mm 319, worst 101 mm. Other candidates: highest edge midpoint 87, 114, 223, corner mean 15, 49, 209. Road at the curb point of the front edge plus 0.140 m (median, 336 plots): 24, 40, 160. 747 schedule plots by the road rule: within 5 mm 43, 10 mm 79, 30 mm 361, worse 385, no road level 1. IMC (fit) 23, 41, 160 of 336, IMD (not in the fit, all Top Surface-01) 20, 36, 201 of 410. Worst IMC 2609, 2610, 2608, IMD 1259, 1322, 1232. Through levels.py: FFL 6 of 747, steps 638. Misses FFL: entry level 594, wrong cluster 61, base height 4, rounding 82. With the sheet entry levels and the new clusters FFL 600, steps 705
- Part 3. 302 of 337 plots measured, 35 have no block or two. 15 groups, none too few. Test halves 149 plots: link origin within 1 mm 46, within 10 mm 117, worse 32, relation right 126. Entrance within 10 mm 118, 100 mm 126, worse 23. Offsets in block axes are round (Duplex pos 1 at 20, 25 and pos 2 at 0, 25). Villa_VL4, VL3 and C10 hold a second relation (a mirrored unit with its origin on the other side), and 6 of 43 Duplex_DP4 plots sit in the other half of their block, so one offset per group misses them. Written to config/prototype_offsets.json with seed 13
- Part 4. Round 9 proof on the answer key: 337 plots, 1010 floors, 338 links, Generic top equals FFL on 336 of 336 and El @ Entry on 0, ramp shares an edge with Generic on 336, yard ambiguous 337, ramp depth 5.52 on 322. Run through the functions of plot_relations because its main writes beside the input. Zone 3 IME: no label or layer in the baseplan, V9, R06 or footprint says IME, so 0 plots. For the 1831 unit plots outside the schedules the curb point is covered on 1828 (Compiled West_MM 656, Top Surface-01 434, Part 4 410, Top Surface-02 301, Missed from Paste 27)
- Second methods, seed 13, 10 each: road plain triangle search 10 of 10, offsets hand trigonometry 10 of 10 (seed 7 also 10)

Remains:
- The Revit run of Round 9 is done, the file is from 24 Oct 01:51. Check 3, checks 4 and 6 to 10, the pair rule on blocks, the entry level to the millimetre, unit codes for the plots outside the schedules, the yard rule of round 15a

Known bugs:
- The road rule gets the entry level within 30 mm on about half of the plots, so the computed FFL matches 6 of 747
- One offset per group misses mirrored units. Group by relation too
- A ramp that slopes along its long edges and has equal long edge means is called flat. A front made of chords under 1 m is dropped. Rotation keys round at 0.1 degree, so 359.94 and 0.06 are two keys. A plot with a dropped Excel unit has two R69 issues. Block attributes are not read. The no value in code test skips whole numbers such as 25 and 180

Next:
- Settle questions 1 and 2, then group the offsets by relation and find what picks the original or the mirror

Your turn:
1. The 10 hints that name other plots than the schedule cluster. Recommended: take the schedule, they are listed under conflicts in analysis.json
2. Entry rule for new plots. Recommended: keep road level at the curb plus 0.140 m as an estimate only, and ask the civil team for the rule that sets El @ Entry, since 49 percent within 30 mm cannot place a floor
3. Surface order. Recommended: keep the file order, only Top Surface-02 and Top Surface-01 answer on the 747 plots. Tell me if another surface is a road
4. Original or mirror of a unit. Recommended: tell me what decides it, the block flag does not
5. Pair rule on blocks. Recommended: keep it off
6. Run steps/round-13.md and tell me if a Look for line did not match

## Round 12b, 2026-10-24, villa blocks from Makkah Building Footprint, unit codes into the clusters

Done:
- Branch round/footprint-unit-codes from main at 5874ae5, pull request 16. The session started inside the repo, so the main guard hook was live and I never committed to main. The branch held unfinished edits of an earlier pass with one failing test. I checked them against your ten decisions and fixed them. The plan was written before the first edit. Full record and every count: steps/history/round-12b.md
- Gate: Python 323 to 425 passed on the branch, .NET 155 unchanged. Main moved during the round (round 15a, pull request 15). I merged main into the branch and kept both rounds in CLAUDE.md, config/project.json (the note and pastJob), docs/rules.md (R80 to R89 next to R73 to R77), PROGRESS.md and this log. One line in the 15a questions that pointed at the Round 12 questions now points here. After the merge: Python 468 passed, .NET 205 passed
- 1 Drawing: Makkah Building Footprint.dwg (Base Plans, 3780 inserts, 19 names) is in the site space, 3696 of 3780 inserts hold at least 0.2 of their outline inside a baseplan plot. CEC Building Footprint has 29 inserts and one outline. drawings.villaBlocks is the footprint file and no .py holds a file name
- 2 Scan (R73): 7 names hold a unit code of the profile (1617 inserts), Villa_VL2 an old client code (248), 11 names none (1915, of which 1806 parking). Inserts by plots that hold 0.2 of the outline: 0 84, 1 3148, 2 425, 3 0, 4 123, more 0. Insert point in the best plot 3115, in another plot 413, in no plot 168, no plot holds a fifth of the outline 84. Mirrored 2081, not mirrored 1699
- 3 All 8 names with a code end in an underscore and the code, so there is no config/block_codes.json. Unmapped: the 11 names (1915 inserts) and Villa_VL2 (248)
- 4 and 5 Unit code per plot (R69, R74): block 2346, excel 73, none 159. Plots per code from blocks, Rev 6 in brackets: C10 260 (204), VL3 267 (8), VL4 477 (201), DP2 286 (24), DP4 564 (18), TH1 244 (114), TH3 248 (132), the other 9 codes 0. Block unit against Excel unit: 674 plots have both, 138 agree, 536 differ. The block unit equals the baseplan label type on 2100 of 2346
- 6 Q02 is out of cluster_aliases and each Q02 cluster is one issue (R75). The real run writes 0 Q02 clusters
- 7 Levels (R67): FFL to the mm, steps 745 of 747 (was 744). The height flag gives 0 under 0.10 and 4 over 0.54 (plots 2464, 2471, 2475, 2483). The sheet FFL is not the computed FFL rounded down: to the cm 90 of 747 (fixes 10, breaks 553), to 5 mm 168 (fixes 24, breaks 489). The 110 rounding misses are all 1 to 5 mm with the computed FFL above the sheet
- 8 Level proof, sheet FFL grouping: FFL 633, steps 745, misses base height 4 and rounding 110. With the CAD clusters: FFL 548, steps 649 (round 12 gave 561 and 663), misses wrong cluster 145 and rounding 54, no unit code 0
- Clusters, before to after: 548 (M02 143, M04 282, M09 61, Q02 62) to 535 (M02 153, M04 293, unknown 89, Q02 0)
- Mirror flag against the 338 links, 114 with a flag on both sides: same 28, opposite 86. Link turned from the block: 0 degrees 134, 180 degrees 134, 90 degrees 15, 270 degrees 19, other 1. The rotation sets are equal (30.1, 120.1, 210.1, 300.1)
- Second block match (R76, no shapely): 1617 blocks, 1595 agree, 22 owner flips on a near tie, 0 differ. 2962 of 2962 plots agree
- Review: a fresh reviewer ran the suite and tried to break the unit code source and the block match. 7 defects, all closed but the first, which is your decision. Block over Excel now lists every conflict in analysis.json (536), a block with its largest share under 0.2 belongs to no plot, an Excel unit that is not a profile code is an issue, a schedule with no unit and a unit block with no outline are said, a superscript plot number no longer crashes, the cover share has a tolerance. Detail in steps/history/round-12b.md. A second reviewer read the fixes: 7 findings, none blocking. Fixed: a half millimetre tie in the height flag that float noise decided, a cover test that did not prove the slack, a schedule whose units are all dropped, a bad cluster_block_max, negative plot numbers, two doc wordings. Those last fixes were checked by reverting each one and seeing its test fail, not by a third review

Remains:
- Check 3, checks 4 and 6 to 10, the pair rule on blocks, the Revit run of round 9, levels.py wired to analysis.json

Known bugs:
- The block names are client plot types and a block names a whole cluster. As decision 4 reads, a townhouse block gives TH1 or TH3 to its two end duplex plots, so 123 old M09 and Q02 clusters read as four equal units and 89 unknown clusters appear. The 62 Townhouse_TH3 and 61 Townhouse_TH1 blocks match the 62 Q02 and 61 M09 clusters of the baseline one to one
- 159 VL2 plots have no unit code. 73 other VL2 plots take the Excel unit and it is DP4 22, DP2 20, VD4 11, VD2 10, VL4 8, VL6 2, so VL2 is not one unit
- The Excel unit uses the config shift only, so an edge plot that sits one label off (R28) takes another plot's unit
- 1342 plots sit under a block for several plots and have mirrored null, so the mirror flags of the R70 pair rule never fire on this data

Next:
- Settle question 1, then code check 3 (one FFL per cluster), then wire levels.py to analysis.json

Your turn:
1. Unit code source. The blocks name the client plot type and a whole cluster, not a unit. Recommended: keep the Excel unit as the unit code for the 747 schedule plots, which gives 225 real Rev 6 clusters and no unknown, and read a block that covers 2 or 4 plots as a cluster hint (Duplex_DP2 is D02, Duplex_DP4 is D04, Townhouse_TH1 is Q01, Townhouse_TH3 is Q02) and not as a unit code for each plot. Tell me the Rev 6 unit codes of the other plots, or how a client code maps to them
2. Pair rule on blocks. Blocks with one plot are 503 mirrored and 503 not, and the two plots of a duplex block share one block. Recommended: do not code it until the original and mirror convention is known from the master plan
3. Mirror and rotation of the Revit link against the block. Recommended: take the link as the truth for the placed unit and tell me if the block flag is still needed
4. VL2 plots (232). Recommended: send the Rev 6 unit of the 159 that have none
5. The 1 to 5 mm FFL misses. Recommended: ask the architect how the sheet FFL is rounded, it is neither the cm step nor the 5 mm step
6. The list analysis.unitPlotTypes in config/project.json (C10, DP2, DP4, TH1, TH3, VL2, VL3, VL4) is my choice and only says which plots get an issue when no unit code is found. Recommended: keep
7. Run steps/round-12b.md and tell me if a Look for line did not match

## Round 15a, 2026-10-24, plan.json from the past IMC job and the placer step 1

Done:
- pwd /c/Users/bader/Documents/GitHub/site-adaptation-tool-15a, a git worktree, branch round/addin-15a from main at 5874ae5, pull request 15. The session started in the worktree, so the main guard hook was live. Round 12b ran at the same time in the main folder, which I did not touch, and none of its five files changed. The Round 12 entry below is kept in full for it. Before the merge main was still at 5874ae5, 12b had not merged, so there was no rebase. The plan was written before the first edit. Full record: steps/history/round-15a.md
- Gate: Python 323 to 366 passed, .NET 155 to 205 passed
- Rules: R80 plan.json, R81 python/plan_from_past.py with names from pastJob in config/project.json, R82 position proof, R83 to R88 the placer dotnet/SiteAdaptation.Placer (Place from plan, logic in Common behind IPlacerTarget), R89 python/plan_compare.py. R1, R54 and R55 amended
- plan_imc_past.json: plots written 337 of 337. Missing: floors.yard 337, floors.generic 1 (plot 2441). Floors found: Generic 336, yard 0, ramp 337
- Position math against transformShared: 94 links, largest difference 0 mm. The reader used the same offset, so this proves the builder and not Revit. FFL against the past link origin Z: 337 of 337 within 1.5 mm, largest 0.0 mm
- Second method, seed 15, 10 random plots, PowerShell on the raw prototypes.json and zipfile on the raw sheet XML: 150 of 150 checks match
- Placer on fakes: position, rotation, mirror, Z, parameters, skip, rollback, coordinates stop. Three planted bugs were each caught. Mirror call: ElementTransformUtils.MirrorElements(document, ids, plane, false), which mirrors in place with no copy. MirrorElement is not used, it always makes a copy
- Reader fixes: the link box comes from the link document extents when get_BoundingBox gives none. The spot host comes from GetDependentElements when AreReferencesAvailable is false, which the API says happens while the view of a spot is closed. Both are unproven until you run Revit
- plan_compare.py on the round 9 file against its own plan gives 337 of 337 full match, the baseline for step 21
- I copied prototypes.json to prototypes_imc_past.json in Documents\SiteAdaptation, byte for byte, and pastJob reads the copy, because step 20 writes prototypes.json again
- Review: a fresh reviewer in its own worktree found 15 points and I fixed 13. The yard and the mm rounding chain are under Known bugs. A second short review of the fixes found 4 more, all fixed with tests and checked by the gate and the real run, not by a third review. The harness put that worktree inside the 12b folder under .claude\worktrees, I removed it with git worktree remove

Remains:
- The Revit run of steps/round-15a.md. Floors are in plan.json but nothing places them yet. A plan from CAD and LandXML comes in later rounds

Known bugs:
- R58 gives an ambiguous yard on all 337 plots, 2 to 6 candidates each, so floors.yard is missing on every plot. The largest shared edge would decide 239 and give 28 yards to two plots, so it is no fix
- RRE_ZZ_SP_Prototype-FFL differs from the schedule FFL beyond 1.5 mm on 258 plots, up to 32 mm. Plots 2301 and 2302 have link unit VD4, schedule and file DP4. 10 VD1 plots use the DP1 file
- The reader rounds the link origin and the survey points to 1 mm, so an insert can sit up to about 1.5 mm from the true past position. Not seen, the largest is 0 mm
- Revit loads one SiteAdaptation.Common.dll for both add-ins, so both must come from one build. Step 8 checks it
- An angle to true north other than 0 stops the placer, the rotation is not coded

Next:
- Fix what your Revit run shows. Then place the floors from plan.json, once the yard rule is decided

Your turn:
1. Run steps/round-15a.md on the copy only, step 26 says what to send. Recommended: yes, as written
2. Yard rule. Recommended: a yard floor belongs to the plot whose CAD plot face holds most of its area, through the official plot numbers. Until then yards stay missing. The touch rule of R58 cannot decide
3. RRE_ZZ_SP_Prototype-FFL. Recommended: keep the schedule FFL as you chose and never write the parameter. Tell me if the 258 plots that differ mean the parameter is stale
4. Plots 2301 and 2302. Recommended: DP4, as the schedule and the file say. The plan keeps the link value VD4 until you say
5. Angle to true north. Recommended: keep the stop until a project has an angle
6. The questions of Round 12 are answered in Round 12b above. Still open from them: check 3 and the pair rule on blocks

## Round 12, 2026-10-24, villa blocks, row split, road fronts, levels, master plan V9

Stub. The full entry is in steps/history/round-12.md.
- Block inserts (R68), block to plot (R69), row split (R70), road fronts (R71), xref lookup (R72), the level rule and its proof (R67), the Rev 6 profile and the master plan V9 role were added and merged as PR 14. Gate then: Python 323 passed, .NET 155 passed
- Its questions are answered in the round 12b entry: the baseplan stays, villaBlocks is the building footprint drawing, VL2 stays out of the profile, Q02 is flagged, the FFL is rounded to the mm, the height flag is the legend span, the road edge layer and the V9 layers stay. Still open from it: check 3 and the pair rule on blocks

## Round 11, 2026-10-24, project settings, cluster derivation, checks 1 and 2

Stub. The full entry is in steps/history/round-11.md.
- config/project.json (R63), python/cad_analysis.py (R64) and checks 1 and 2 (R65, R66) were added and merged as PR 13. Gate then: Python 229 passed, .NET 143 passed
- Its questions are answered in the round 12 entry: split long rows into listed clusters, the villa blocks, the front of a row, my settings stay. Still open from it: the official plot numbers in analysis.json

## Round 10, 2026-10-24, ROSHN Makkah rules profile and loader

Stub. The full entry is in steps/history/round-10.md.
- rules/roshn-makkah.json, the loader python/project_profile.py (R62) and rules/README.md were added and merged as PR 12. Gate then: Python 202 passed, .NET 143 passed
- Its questions are answered in the round 11 entry: split positions, VL4 width, project settings, where the ten checks live, cluster membership. Still open from it: the yard rule and the ramp slope, in steps/history/round-9.md

## Round 9, 2026-10-23, floor, ramp and link relations

Stub. The full entry is in steps/history/round-9.md.
- Floor measurements, parameter details, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py (R57 to R61) were added and merged as PR 11. Gate then: Python 201 passed, .NET 143 passed
- Open for Bader: the Revit run of steps/for-bader-round-9.md, the yard rule and the meaning of the ramp slope in the type name. Both questions are in steps/history/round-9.md

## Round 8, 2026-10-23, ramp levels from the real corners, surface roles, floor count, one error line

Stub. The full entry is in steps/history/round-8.md.
- Ramp corner levels with the survey offset (R47), role proposal (R48), floor count per plot (R49) and one error line for floors with no Plot parameter (R50) were added and merged as PR 10. Gate then: Python 166 passed, .NET 109 passed
- Open for Bader: the Generic floor of plot 2441, the 119 ramps with no corner on the road, the stale names of plots 2589 and 2594, the numbered questions in steps/history/round-8.md. The yard plot question is answered by the round 9 measurement

## Round 7 Revit, 2026-10-22, ramp corners, spot elevations, plot numbers, link names

Stub. The full entry is in steps/history/round-7-revit.md.
- Floor corners and cornersSource, slope, spot elevations, plot numbers, the link name parse and the link contents default were added and merged as PR 9. The add-in ran in Revit on 22 Oct 23:12 UTC and the round 8 entry says what it showed. Gate then: Python 135 passed, .NET 105 passed
- The questions of that round are answered or moved: Prototype-FFL 298 lower (round 8), the 337 yard lines (R50). Still open: the stale names of plots 2589 and 2594, the sketch plane at 150 m

## Round 7 road, 2026-10-22, LandXML road surfaces and the ramp level test

Stub. The full entry is in steps/history/round-7-road.md.
- LandXML reader, surface lookup, road config, ramp road levels, gap bands and the road check were added and merged as PR 8. 7 surfaces, 337 ramps tested, only Top Surface-02 covers any corner (119 ramps with no covered corner). Gate then: Python 127 passed, .NET 77 passed
- Open for Bader: the finished road surface, the 119 ramps with no covered corner, the plot check, the numbered questions that are in steps/history/round-7-road.md

## Round 6, 2026-10-22, plot number shift, second source, no type check

Stub. The full entry is in steps/history/round-6.md.
- Shift config IMC 10 and IMD 11, Master Plan skip list, second source check, workbook survey and face reasons were added and merged as PR 7. Location check 734 of 746 joined plots pass, 12 fail. Gate then: Python 96 passed, .NET 77 passed
- Open for Bader: the 12 failing plots, the 17 many label faces, the numbered questions that are in steps/history/round-6.md

## Round 5, 2026-10-22, Revit link names, shared parameters and floors

Stub. The full entry is in steps/history/round-5-revit.md.
- Link names, shared parameters, floors and link_summary.py were added and merged as PR 6. The Revit API code has never run in Revit, so nothing new is confirmed until Bader runs it. Gate then: Python 70 passed, .NET 77 passed
- Open for Bader: rebuild and run the add-in, send prototypes.json, check three link names, Prototype-FFL units, foundation slabs, nested links

## Round 4, 2026-10-22, explode, label join, PDF rows, full run

Done:
- Branch feat/explode-join-rows. Revit code untouched (diff against main is empty). Main moved during the round: another session merged PR 4 (Revit links, walls, ramps, also called round 4) and took R16 and R17, so my rules are R18 to R26. Nothing real is in git. This log holds counts only
- Housekeeping: CLAUDE.md, steps/PROGRESS.md, steps/history/ (rounds 2 and 3), 3 merged remote branches deleted, main guard hook. The hook script passed 17 offline cases and pytest, but it is NOT live in this session (it started outside the repo, a dry run of git push to main went through)
- Full run on the real files, in place, outputs in sample_output/full_run: 27 of 27 ran, 1 min 47 s, slowest 26 s, no timeout, no failure. 13 DWG, 12 PDF, 2 workbooks. Not read: 145 DWG under 01_Output, 2 Excel lock files, the Master Plan folder has no .bak but holds a masterplan .xlsx I was not asked to read
- CAD explode: pieces now carry layer, handle, insert path, world coordinates, attributes and extended data. Entities before and after exploding: Building Footprint 4271 to 20458, Master Plan R06 45 to 140064, Phase 1 Road Layout 7049 to 211479, Typical Cross Sections 1548 to 129100, baseplan 15791 to 15771. Second method, AutoCAD DXF export read by ezdxf: before cells match 527 of 527, after cells 1022 of 1026, totals equal in 12 of 13 files. The 4 cells: 3 are ARC and CIRCLE that ezdxf names ELLIPSE (Road Layout), 1 is 2 POLYLINE in R06 that ACadSharp does not read (2 of 140066)
- Layer filter: config/cad_layers.json (keep, drop, plot line layers, plot label layers). Dropped and counted: 2705 pieces on Defpoints in Building Footprint, 61 in Road Layout, 26 in R06. pieces.json is up to 46 MB per drawing
- Xrefs: baseplan 3 of 3 found next to the file. 4 other xrefs are not found (one in each of the 3 Master Plan files and one in Typical Cross Sections), the run went on without them. Reader notes are grouped with counts. MULTILEADER: 18 exist in 2 drawings and none holds text, ezdxf agrees for the 15 on the baseplan
- Plot join, baseplan: MP_PLOT_LINE alone gave 777 faces, 390 plots, 375 faces with many labels. Adding MP_PLOT_SUBD gives 2991 faces, 2962 plots with one label each, 29 other faces (12 no label, 17 many labels), 18 labels outside every face, 0 duplicate numbers. Second method, shapely on the DXF: 2960 of my 2962 plots match by number and area, 2 not in the reference, 1 reference face not in mine
- PDF rows: 3346 rows from 12 sheets (round 3 counted 3306, it missed 3 rows per full block). PDFium text agrees on 3345, 0 missed. The 1 extra is a source typo, plot 2589 code 2 on sheet 009028 has N with no decimal point, kept as found and listed in odd. shapes file 2 to 3 MB to 74 KB on sheet 009003
- Excel: sheetRow on every row, coords_extra.json (notes, legend, formulas). Both workbooks read with MP_Phase 1_Zone21, 410 rows (sheet rows 40 to 449) and 337 rows. Raw sheet XML second method: 8290 of 8290 cells match, the row after each table has an empty plot cell
- Link table, join by plot number: 2962 CAD plots, 746 PDF plots, 747 Excel plots, 746 with all three. PDF unit type equals Excel unit on all 746. Location check fails on 746 of 746, see Known bugs 1. Coordinate space: PDF sheets overlap the CAD plots yes, 3319 of 3346 PDF points lie inside some CAD plot. 213 of 259 drawing pairs overlap
- Six random plots opened across CAD, PDF and Excel (1192, 1319, 1432, 1505, 2473, 2576). Each PDF row is in the PDFium text, each Excel plot cell is in the raw sheet, PDF type equals Excel unit, and each plot sits in the CAD plot numbered 10 or 11 lower
- Civil 3D roads: Road Surface C3D (180 MB) holds 105 model space entities and Rough Grading (216 MB) holds 5, one reader error each. ezdxf on AutoCAD's DXF export finds the same 105 and 5, so the surfaces and grading are Civil 3D objects outside model space, not read
- Gate: Python 60 passed, .NET 63 passed. New tests built on dummy data in code

Remains:
- Revit run on the real file, by Bader. Plot join only for the baseplan layer names. Everything under Known bugs

Known bugs:
- 1. NUMBER SHIFT: every PDF plot sits in the CAD plot whose label is lower by 11 (IMD sheets, 404 plots) or 10 (IMC sheets, 333 plots), a few edge plots differ by one more or less. 0 of 805 sheet plots sit in the plot of the same number, independent shapely faces agree. Nothing is corrected, all 746 are in conflicts.json
- CAD label type (DP4, VL4) is a plot type and PDF and Excel type (DT3, VD2) is a unit type, so they cannot be compared
- 4 PDF plots repeat a point code, 58 plots appear on 2 or more sheets, 1 Excel plot has no PDF rows
- Typical Cross Sections, CEC Building Footprint and Rough Grading do not share the site coordinate space with the PDF sheets
- Master Plan layers use other names (X_MP_Plots$0$MP_Plot), so no plot faces come from them
- Block that contains itself: the guard is in code and untested, ACadSharp overflows the stack when a test builds one
- Still open: one unclosed face and 28 plot_open pieces, arc crossings not noded, Revit not run

Next:
- Decide the numbering with Bader, then put the shift in a small config the link step reads
- Look at the 29 other faces and 18 stray labels, then placement only after the conflicts are understood

Your turn:
1. The baseplan labels and the schedules differ by 11 (IMD) or 10 (IMC). Which numbering is right, and where does the shift come from? Recommended: the PDF and Excel numbers are official, tell me the source and I add the shift as a config map
2. Keep MP_PLOT_SUBD as a plot line layer? Recommended: yes, without it 375 faces hold many plots
3. Is it right that the PDF points are unit corners that must lie inside the plot? Recommended: yes, the check stays at inside or within 1 m
4. Is there a table from plot type to unit type? Recommended: send it, until then no type check against CAD
5. Review config/cad_layers.json drop list (Defpoints, logo, title, border, viewport, legend). Recommended: keep
6. Civil 3D road files hold no readable entities. Recommended: export from Civil 3D by hand, the same as Revit
7. Sheet 009028 plot 2589 code 2 has N 2363719462 in the source. Recommended: tell the drawing owner, I keep it as found
8. Two rounds are called round 4 in this log. Recommended: leave it, say Round 4 CAD or Round 4 Revit
9. Revit still waits for you, steps are in the Revit round entry below

## Round 4, 2026-10-22, Revit links, walls and ramps

Done:
- Branch feat/revit-links-walls-ramps. Changed: dotnet/RevitPrototypeReader, dotnet/SiteAdaptation.Common, the tests, docs/rules.md and README.md. Not touched: dotnet/CadReader, python/excel_reader.py, python/pdf_reader.py
- THIS CANNOT BE PROVEN AGAINST THE REAL FILE. There was no Revit here and I did not open 01101-PSN-IMC-ZZZ-PIM-TU-000001. What the tests prove is the reader logic and the JSON shape, against test doubles I built by hand. The code that talks to the Revit API (new RevitModelSource.cs and the edited RevitExtractor.cs) only compiles and has never run. Bader must run it in Revit to confirm anything about the real model
- Reported by Bader, not seen by me: the earlier add-in run on 01101-PSN-IMC-ZZZ-PIM-TU-000001 (Revit 2024, Zone 02 Neighborhood C) returned only Coordinate Point markers and title blocks. It missed the linked models, the walls and the ramps. No prototypes.json from that run is in the repo
- New in prototypes.json, project documents only. On the document entry, after prototypes: links, walls, ramps and errors. Every existing key keeps its name and place. A family document gets none of the new keys. Host prototype rows serialize exactly as before because mirrored is only set on link instances
- links, one entry per Revit link instance: file, transform (origin in metres, rotationDeg, mirrored, axisX, axisY, axisZ), instances, links, deeperLinks, errors. Instances are the family instances placed in the linked model, same shape as a prototypes row plus mirrored (the instance flip from FamilyInstance.Mirrored). They carry every instance parameter and every type parameter, shared ones included, and the instance value wins on a name clash. Instance points stay in the linked model's own coordinates
- Nested links: a link inside a link is read and sits under links of its parent entry. A link inside that is not read, it is only counted in deeperLinks. A nested transform is relative to the link that holds it
- walls: type name, location (line or arc ends, plus centre and radius for an arc), length, height, every instance parameter. Length is the location curve length. Height is the Unconnected Height parameter and is left out when the wall has none
- ramps: type name, location when Revit gives one, bounding box, every instance parameter. Ramps have no class in the Revit API, so they are found by category Ramps
- Design: Common now holds IModelSource, ModelReader, LinkMath and ParameterBag. RevitModelSource implements IModelSource over a real Document, and the tests implement it with a hand built fake. RevitExtractor.PlaceRow is the old InstanceRow location code moved out unchanged so host rows and link rows share it
- Added beyond the brief, say if you want any removed: axisX, axisY and axisZ (a mirrored transform cannot be rebuilt from rotation alone), deeperLinks and errors (so a skipped link or a bad element is visible and not silent), a bounding box on ramps (a ramp may have no location), parameters sorted by name, and link, wall, ramp and problem counts in the dialog that ends the command
- My reading of mirrored: the transform has the link mirror flag and each link instance has its own flip flag. Say if you meant only one of them
- Tests: 12 new, named R16 or R17. A link with translation and rotation, a mirrored link, the flip of one axis alone, rotation wrap, nesting one level, an unloaded and a failing link, a failure in the links section that keeps walls and ramps, the old keys kept, a wall with 8 documented parameters (and an arc wall), a ramp with 7 documented parameters, and the parameter merge. Mutation check: flipping the mirror test and raising the depth limit each made the matching tests fail, then both were put back
- Gate run locally before the PR: build 0 warnings and 0 errors, .NET 40 passed (28 before), Python 24 passed
- docs/rules.md has R16 and R17. README.md has one line on the new output. C# statement semicolons remain because the language needs them, as R15 allows

Remains:
- The Revit run on the real file, which only Bader can do. Until then none of the new output is confirmed
- Everything under Known bugs

Known bugs:
- UNPROVEN, Revit API behaviour I could not check without Revit: GetTotalTransform on a link (the pages I found suggest it matches GetTransform for a plain link, which I did not confirm), GetLinkDocument on a link inside a link, the file name from the link type file reference, collecting ramps by category, and whether a ramp has a location at all
- If the villa prototypes in the link are groups or in-place families and not loaded family instances, they will not appear in instances. That is the same limit round 2 predicted for the host
- Inside a link only family instances are read. Walls and ramps inside a link are not read. Type only rows (a family type with nothing placed) are not written for links
- Wall height is the Unconnected Height parameter. A wall whose top is attached to a level may show a value that is not its real height
- Instance points in links are in the link's coordinates and nothing converts them to host coordinates. A nested transform is not composed with its parent, so a consumer has to chain them
- Several instances of one link type each get their own entry and each walks the same linked model again, so the file can repeat content
- Output size is unmeasured. All parameters on every instance of a large link can make prototypes.json big, and R3 asks for small output
- A parameter that Revit cannot read throws, and the whole element is skipped and listed under errors. The rest of the parameters of that element are lost with it
- Still open from round 3 and untouched: CAD (one face not closed, 28 plot_open pieces, plot_shape holds every face, arc crossings not noded, 3 xrefs missing, anonymous *U0 blocks, 15 MULTILEADERs not in texts.json), Excel (default sheet name Coordinates, blank headers col_A col_K col_L, thin 4 decimal margin on the 0.205 threshold), PDF (single word labels, rows not rebuilt, reversed margin stamp, garbled key plan, large shapes file)
- RRE-WP-NSP-KEO-ARC-TH1-ZZ-MOD-3100.rvt is still NOT RUN, as in round 2

Next:
- Bader runs the rebuilt add-in on 01101-PSN-IMC-ZZZ-PIM-TU-000001 and sends back prototypes.json, then I check it against the model, link by link
- Fix whatever that run shows, most likely in RevitModelSource
- Decide whether to convert link instance points to host coordinates in the reader, and whether to read walls and ramps inside links
- The open CAD, Excel and PDF items from round 3

Your turn:
1. Rebuild the add-in, reinstall it, and run it again on the real file. This round can only be checked against that run. Build with dotnet build dotnet\SiteAdaptation.sln -c Release, copy dotnet\RevitPrototypeReader\bin\Release\net48\ and SiteAdaptation.addin to %APPDATA%\Autodesk\Revit\Addins\2024\ over the old copy, keep the Assembly line pointing at the full path of RevitPrototypeReader.dll, open Revit 2024, run External Tools, Read Prototypes, choose The open document, and send back Documents\SiteAdaptation\prototypes.json
2. Read the dialog at the end of the run. It now gives the link, wall and ramp counts and says if problems were listed. Tell me the counts you expected from the model, and whether the villa link shows up with the right file name
3. If errors is not empty in the file, send it as it is
4. Do you want link instance points converted to host coordinates in the reader, or left in the link's coordinates with the transform beside them? Recommended: keep both once the transform is confirmed on the real file
5. Are the villa prototypes in the link loaded family instances, or groups or in-place families? Recommended: check in Revit by selecting one and reading the type selector
6. Keep or drop the extras listed in Done (axes, deeperLinks, errors, ramp bounding box, sorted parameters, dialog counts)? Recommended: keep

## Round 3, 2026-10-22, fixes against the real files

Detail: [steps/history/round-3.md](history/round-3.md).
Excel header row and stop rules fixed, El @ Entry and Height kept at 4 decimals. CAD plot geometry read from MP_PLOT_LINE (777 closed shapes, 28 open pieces). One more PDF sheet read and the layout held on all 12. Gate at the end of the round was Python 24 passed and .NET 28 passed.

## Round 2, 2026-10-22, readers run on real ROSHN Makkah files

Detail: [steps/history/round-2.md](history/round-2.md).
First run of the four readers on the real files. Excel, PDF and CAD ran, Revit did not. The list of mismatches found there drove round 3. Gate at the end of the round was Python 18 passed and .NET 22 passed.

## Round 1, 2026-10-22, read-only readers

Done:
- RAN: Python tests, 18 passed, locally and on CI (run 39768241302, Ubuntu, Python 3.12)
- RAN: .NET tests, 22 passed, locally and on CI (windows-latest, .NET Framework 4.8)
- CAD path used: C#, ACadSharp 3.8.0. The ezdxf fallback was not needed
- RAN: ACadSharp opened three real AC1032 DWGs from the AutoCAD 2026 install with no reader errors (Civil Best Practices Guide current.dwg and source.dwg, Civil Tutorials Align-1.dwg). Smoke check only. They are not site data and not in the repo
- Found on that run: ACadSharp PlainText turned a label into LLOT 17l. MTextCleaner now strips the codes, with a test
- READ: pdfplumber 0.11.7, pdfminer.six and openpyxl 3.1.5 are MIT, from the installed package metadata. PyMuPDF is not installed or referenced
- Revit add-in compiles against the Nice3point Revit 2024.3.60 API package. It was not run in Revit
- docs/rules.md holds R1 to R15. Each rule is named by a test, except R6, R10 and R11 which are process rules
- Gate in .github/workflows/test.yml. Zero tests fails the job (pytest exit 5, trx count on .NET)
- R15 reading: C# statement semicolons are syntax and are the one exception. Prose and comments follow the rule

Remains:
- Any run on a real PDF, workbook or Revit file. No sample of those exists in the repo, so each of those readers stops at its dummy test
- The harness for this repo (CLAUDE.md, hooks, steps README and PROGRESS, checker agents). Not built this round
- The week one stress test round, before any placement work

Known bugs:
- UNKNOWN: the Revit command has never run inside Revit. Parameter values, level names and the family bounding box are unproven
- CHOSEN: the default key parameters are Width, Height, Depth, Length, Area, Mark, Type Mark, Description. They are my pick, not a stated list
- Revit: parameters match by English name. Project files list loadable family types only, not system families or groups
- Revit family files: insertion point is the family origin, level is empty, bounding box covers solid forms and nested instances only
- CAD: model space only. Lines, arcs and splines are not listed. Polyline bulges are ignored
- CAD: a dense drawing makes a big plots.json. Align-1.dwg gave 552 KB for 579 polylines
- PDF: labels are single words, so PLOT 12 is two labels. Curves are not read
- Excel: the first non-empty row is taken as the header row. The sheet defaults to Coordinates
- This session started outside the repo, so no hooks were live

Next:
- Run the Revit command on one prototype family in Revit 2024 and read prototypes.json against the family
- Then drop one real file per reader into a sample folder and confirm each reader against it

Your turn:
1. Which parameters count as key? Recommended: send the 8 to 12 names, they go in key-parameters.txt, no code change
2. May a small real DWG, PDF and coordinates workbook go into this private repo for tests? Recommended: yes, one each, added with git add -f. Say no if the client files must stay off GitHub
3. Should PDF labels stay as single words? Recommended: yes until a real PDF shows how plot labels are laid out
4. Build the harness next round, before any new reader work? Recommended: yes
5. Run the add-in. Open Revit 2024, run External Tools, Read Prototypes, pick a prototype .rfa, send back prototypes.json. Steps are in README.md
