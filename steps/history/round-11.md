## Round 11, 2026-10-24, project settings, cluster derivation, checks 1 and 2

Done:
- Branch round/clusters-checks from main at b0a174c. This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. The untracked folder Claude outputs/ is not mine and stays out of the commit
- The plan was written before the first edit. One change of order: the units helper (item 6) came before the cluster step (item 4), because the cluster step uses it
- The prompt named analysis data, analysis.json and a villa block with a scale per plot. None of them existed. I made python/cad_analysis.py, which reads plots.json of the baseplan from the CAD reader and writes analysis.json (R64)
- config/project.json (R63) holds profile, drawings (baseplan, second default, the two skipped drawings with their reasons) and an analysis block (toleranceM 0.01, partyWallShare 0.75, endShare 0.25). full_run.py reads it, has a --project option and a new analysis stage that takes the profile path from the config. No ROSHN or Makkah string is left in any .py file, a repo test scans for it. test_project_profile.py takes the profile path from the config too
- Profile: every key kept, cluster_split_meaning added next to cluster_split_positions, plot.vl4_width "derive" added. rules/README.md checks 3 and 8 say the same in one line each. Checks 1 and 2 left it with a pointer to R65 and R66
- project_profile.py rejects a duplicate key with ProfileError naming it, one test. profile.metres(key) is the one mm to metres helper, one test, used for plot.long_side
- cad_analysis.derive_clusters: a plot is in a cluster when its typology kind in the profile is duplex or townhouse, never by code letters. Villa kinds are never in one. Side by side means the shared edge is at least 0.75 of plot.long_side. Back to back plots share a shorter edge and stay apart, a plain shared segment test would merge rows. Plots back onto other plots and front onto a road, so the front of a row is away from its one end that touches plots outside the row. Rows run left to right facing the road from the plots. When the front is not known the row keeps its chain, starts at the lower plot number, orderSure is false and the sequence is also tried reversed. The unit typologies are matched to the profile clusters and the code is written or unknown. mirrored comes from the block scale and is null when a plot has none
- python/rules_checks.py: check 1 is R65, check 2 is R66, one test each. R64 has 17 tests. Steps for you: steps/round-11.md, 22 steps. The round 10 steps moved to steps/history/round-10-steps.md because their counts are stale
- Checked, RAN: baseline before any edit Python 202 passed and .NET 143 passed. After the last edit Python 229 passed, .NET 143 passed (no C# changed), build 0 errors
- Checked, RAN: 21 mutations of the new code each failed a test and the files were put back. Real baseplan through the built CAD reader: 2962 plot faces, 1342 duplex and townhouse plots in 330 clusters: D02 85, D04 98, Q01 4, Q02 2, unknown 141, 577 issues (141 R66, 436 R65). A second method with plain Python collinear edge overlap and no shapely gave the same 330 groups and the same 1460 shared pairs. The analysis stage ran from the exact command line full_run builds, on a copy of the baseplan output, status ok
- Checked, RAN: the front of a row against the Revit link axes of prototypes.json. 240 links joined to a baseplan face by position, 100 of them in duplex or townhouse rows. The neighbour rule decides 76 and all 76 point along the link axis Y in the minus direction, 24 are left open. The first rule I wrote, the nearest road centreline, split 54 to 46, which is chance
- Found on the real files, RAN, nothing real in git: the baseplan holds no villa block per plot. Makkah Building Footprint.dwg holds them but the reader writes no scale, the insert points sit on plot edges, duplex blocks are about one per pair and townhouse blocks do not touch their plots. Shared edges between duplex and townhouse plots run from 2 to 27.5 m, back to back edges end at 13.5 m and party walls start at 25.0 m with nothing between
- A fresh reviewer read the pull request, ran the suite and about 60 mutations in a scratch copy. Fixed from its report: the road centreline signal was wrong (the nearest MP_Road CL line is 39 m away at best and 370 m at the median, so back to back rows were both marked sure with fronts the same way), a row with no known front lost its chain and could turn a valid Q01 into unknown, the margin case ordered by a guess, odd input could crash (a face with under 3 vertices, no label, a bad plots.json, a missing setting, a null profile), check 1 treated non-rows as rows, and some behaviour had no test (the summary fields, the printed line, the --project option, a bool in metres, the degree clause). Claims corrected: the 436 R65 issues are 88 in four unit rows and 348 in longer rows, the shared edge gap is 13.5 to 25.0 m, the README stage wording and the run sheet step numbers. Not changed: the party wall rule stays, and a lone duplex plot passes check 1 and is caught by check 2

Remains:
- Villa blocks to plots and the real mirror flags, they are null today. Checks 3 to 10. The Revit run of round 9. riser_height is null
- A full run of all 26 files with the new stage was not repeated, UNKNOWN

Known bugs:
- 141 clusters are unknown: 44 of four units (35 DP4 x4, 9 DP2 x4) and 97 of 6 to 12 units. They may be listed clusters standing side by side. The 436 R65 issues sit in those unknown rows, 88 in the four unit rows and 348 in the longer rows, and are noise until the rows are explained
- The order along the road is not sure on 95 of 330 clusters, 55 listed and 40 unknown. For the other 235 the front rule is used, and on the 76 units I could join to Revit it agrees every time. A plot with a typology that is not in the profile is in no cluster and nobody is told
- Plot numbers in analysis.json are baseplan labels, not official numbers
- tolerance, party wall share and end share are my choices (CHOSEN). The profile values are still not checked against the source report

Next:
- Explain the unknown clusters with Bader, read the block scale in the CAD reader and match blocks to plots, then code check 3

Your turn:
1. Unknown clusters. 35 rows are DP4 DP4 DP4 DP4 and 9 are DP2 DP2 DP2 DP2. Recommended: a row may be several listed clusters side by side, so the tool splits a long row into listed clusters first and only the leftover is unknown. Say yes or no
2. Villa blocks. Recommended: one block per unit, matched by the block outline lying inside the plot, so the CAD reader must write the block scale and outline. Tell me if a duplex block really stands for a pair of plots
3. Facing the road means standing in the plot and looking at the road, so left to right runs one way from there. Recommended: keep. All four listed clusters read the same both ways
4. Front of a row. Recommended: keep the rule that plots back onto other plots and front onto a road. It matched the Revit link axes on 76 of 76. For 95 rows no end or both ends touch plots, say how you want those placed, for example by the Revit link rotation
5. My settings in config/project.json: tolerance 0.01 m (the CAD reader's own), party wall 0.75 of the 25 m long side (18.75 m, inside the empty gap between 13.5 m and 25.0 m), end share 0.25. Recommended: keep. Say if you want other values
6. Plot numbers. Recommended: keep the baseplan labels in analysis.json until the blocks are matched, then join the official numbers. Say if you need official numbers now
7. Run steps/round-11.md and tell me if a Look for line did not match
8. The older open questions, the yard rule and the ramp slope, are in steps/history/round-9.md
