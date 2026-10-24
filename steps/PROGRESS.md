# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 12 (villa blocks, row split, road fronts, levels, master plan V9) is on branch round/levels-blocks-fronts. The CAD reader writes blocks.json (R68). python/cad_analysis.py matches unit blocks to plots (R69), cuts long rows into listed clusters (R70) and takes the front of a row from the road edges (R71). python/levels.py gives FFL, SSL and steps (R67) and python/levels_proof.py compares it with the two past schedules. python/full_run.py looks up xrefs across the input folders (R72). config/project.json gives each drawing a role: masterplan is the V9 drawing, previousMasterplan is R06 (kept, not read in the run), villaBlocks is R06_0000, baseplan is unchanged.
- Real baseplan: 330 rows are 548 clusters, M02 143, M04 282, M09 61, Q02 62, no unknown. Fronts per row: neighbour 235, road edge 88, none 7. Proof on the past schedules: FFL 633 of 747, steps 744 of 747, the count rule alone 747 of 747.
- The profile follows the manual Rev 6: 16 typologies with keo_file and plot_width_m, clusters M01 to M11, old codes D02 D04 Q01 Q02 kept as aliases, a rev6 block that no code reads. The base heights are not confirmed by the architect, so every R67 row carries a note.
- Round 11 (project settings, clusters, checks 1 and 2) is merged as PR 13. Round 10 (profile and loader) is PR 12. Round 9 (floor, ramp and link relations) is PR 11 and its Revit code has NOT run in Revit yet.
- Top Surface-02 is the finished road for the ramps (Bader decided). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, surface roles, road check, cluster analysis, levels and full run script (Python, shapely, numpy).
- Gate: Python 288 passed, .NET 148 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 12: R67 to R72, the corrected Rev 6 profile, drawing roles in config/project.json, second_source.json on R06. Python went from 229 to 288 passed, .NET from 143 to 148.
- Round 11: R63 project settings, R64 cluster derivation, R65 and R66 for checks 1 and 2.
- Round 10: rules/roshn-makkah.json, the loader (R62), rules/README.md.
- Round 9: floor measurements, parameter details, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py (R57 to R61).
- Round 8: ramp corner levels (R47), role proposal (R48), floor count per plot (R49), one error line for floors with no Plot parameter (R50).
- Revit round 7 ran in Revit on 22 Oct. Round 7 road: LandXML reader, lookup, ramp test, gap bands, second check (R40 to R46). Round 6: plot number shift, skip list, second source check, face reasons. Round 1 to 5: readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook.

## Open bugs

- The only drawing with unit blocks is R06.dwg (1868) and the roles keep it out of the run. R06_0000 holds 248 inserts, all Villa_VL2, VL2 is not in the manual set, so the run finds no unit block. V9 has none.
- A duplex block covers 2 plots and a townhouse block 4, so one block has one mirror flag for a pair or a cluster. 793 plots have no block and 203 blocks tie.
- Q02 has DP4 ends and the manual M10 has DP2 ends, so 62 rows are written Q02. VL2 (232 plots) has no profile entry.
- The sheet FFL sits 1 to 5 mm below the rule in 53 of 473 clusters, and 100 mm below in plots 2560 to 2563. Plot 1526 differs because the FFL is not rounded to the mm.
- V9 holds plot faces and numbers but no typology, and its numbers are a new sequence (2342 of 2343 matched faces differ from the baseplan label). The baseplan role stays until Bader decides.
- Plot numbers in analysis.json are baseplan labels. 12 plots fail the location check. Sheet 009027 plot 2549 and sheet 009028 plot 2589 hold bad values in the source.
- 119 ramps have no covered corner on Top Surface-02. Yard floors have no Plot parameter. Unproven in Revit: the sketch plane at 150 m, the floor slope parameter, spots inside links, the round 9 code.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, first baseplan or V9, the unit block source, VL2 and Q02 against M10. Then run steps/round-12.md.
2. Restore the Generic 150mm floor of plot 2441, then run steps/for-bader-round-9.md and give me the File size line and plot_relations_summary.md.

## Next three steps

1. Settle the baseplan, the unit block source and VL2, then join the official plot numbers to analysis.json and feed levels.py from it.
2. Code check 3 (one FFL per cluster) with the split positions of the profile.
3. Read plot_relations_summary.md from the new Revit file and decide the yard rule and the ramp slope meaning.
