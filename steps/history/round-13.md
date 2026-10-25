# Round 13, full log entry

Moved here from steps/log.md in Round ADDIN.

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
