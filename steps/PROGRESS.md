# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 11 (project settings, cluster derivation, checks 1 and 2) is on branch round/clusters-checks as PR 13. config/project.json names the profile and the drawings (R63). python/cad_analysis.py derives the clusters of duplex and townhouse plots from CAD adjacency and writes analysis.json (R64). Checks 1 and 2 are coded as R65 and R66 in python/rules_checks.py. The other eight checks stay in rules/README.md.
- Real baseplan result: 1342 duplex and townhouse plots in 330 clusters, D02 85, D04 98, Q01 4, Q02 2, unknown 141, 577 issues. Every mirror flag is null because the villa blocks are not matched to plots yet.
- Round 10 (ROSHN Makkah rules profile and loader) is merged as PR 12. riser_height is null until the KEO value is supplied.
- Round 9 (floor, ramp and link relations) is merged as PR 11. The new Revit code has NOT run in Revit yet, Bader runs steps/for-bader-round-9.md.
- Top Surface-02 is the finished road for the ramps (Bader decided, role road in config/road_surfaces.json). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, surface roles, road check, cluster analysis and full run script (Python, shapely, numpy).
- Gate: Python 218 passed, .NET 143 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 11: R63 project settings, R64 cluster derivation, R65 and R66 for checks 1 and 2, duplicate profile keys rejected, profile.metres for mm to metres, cluster_split_meaning and plot.vl4_width in the profile. Python went from 202 to 218 passed, .NET stayed at 143.
- Round 10: rules/roshn-makkah.json, the loader (R62), rules/README.md.
- Round 9: floor measurements, parameter details, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py (R57 to R61).
- Round 8: ramp corner levels (R47), role proposal (R48), floor count per plot (R49), one error line for floors with no Plot parameter (R50).
- Revit round 7 ran in Revit on 22 Oct: 1010 floors, 337 ramps with 4 corners each, 987 spot elevations, 337 error lines.
- Round 7 road: LandXML reader, lookup, ramp test, gap bands, second check (R40 to R46). Round 6: plot number shift, skip list, second source check, face reasons.
- Round 1 to 4: four readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.

## Open bugs

- 141 clusters are unknown, 44 of four units and 97 of 6 to 12 units. 436 R65 issues sit inside those long rows.
- The villa blocks are in Makkah Building Footprint.dwg. The reader writes no block scale, insert points sit on plot edges, townhouse blocks do not touch their plots.
- The order along the road is not sure on 18 of 330 clusters. Plot numbers in analysis.json are baseplan labels.
- 12 plots fail the location check, unit corners 1.4 to 20 m outside the plot, no shift fits.
- Sheet 009027 plot 2549 code 5 and sheet 009028 plot 2589 code 2 hold bad northing values in the source.
- Baseplan: 17 faces with many labels and 12 faces with no label are not fixed. R06_0000 against the baseplan: 334 faces only in the baseplan, 1100 only in R06_0000.
- 119 ramps have no covered corner on Top Surface-02. Yard floors have no Plot parameter. Rough grading has no export yet.
- Unproven in Revit: the sketch plane at 150 m, the floor slope parameter, spots inside links, the round 9 code (link box, top face outlines, file size estimate 12.7 MB).
- Profile values are not checked against the source report. The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the seven numbered questions in the top entry of steps/log.md, then run steps/round-11.md (22 steps).
2. Restore the Generic 150mm floor of plot 2441, then run steps/for-bader-round-9.md and give me the File size line and plot_relations_summary.md. The yard rule and ramp slope questions are in steps/history/round-9.md.

## Next three steps

1. Explain the unknown clusters with Bader, then split long rows into listed clusters if he agrees.
2. Read the block scale in the CAD reader, match villa blocks to plots, then fill the mirror flags and code check 3.
3. Read plot_relations_summary.md from the new Revit file and decide the yard rule and the ramp slope meaning.
