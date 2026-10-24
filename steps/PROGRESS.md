# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 12b (unit codes from blocks and schedule rows, building footprint drawing) is on branch round/footprint-unit-codes, pull request 16. python/cad_analysis.py gives each baseplan plot a unit code from its block, else its schedule row, else none (R69, R74), and builds the clusters from that code only. The baseplan label type stays on the plot as cadType. drawings.villaBlocks in config/project.json is Makkah Building Footprint.dwg. Q02 has no alias and each Q02 cluster is an issue (R75). New scripts: block_scan.py (R73), block_proof.py (R76), link_proof.py (R77). levels.py rounds the FFL to the mm and flags a height outside 0.10 to 0.54 m.
- Real baseplan: 535 clusters, M02 153, M04 292, unknown 90, Q02 0 (round 12 had 548: M02 143, M04 282, M09 61, Q02 62). Unit code source: block 2347, excel 73, none 159. The block names are client plot types and a block names a whole cluster, so 123 old M09 and Q02 clusters become unknown. The schedule units alone give 225 real Rev 6 clusters and no unknown.
- Level proof on the past schedules: FFL 633 of 747, steps 745, count rule alone 747. With the CAD clusters: FFL 548, steps 649. The sheet FFL is not the computed FFL rounded down to the cm or to 5 mm.
- The profile follows the manual Rev 6: 16 typologies, clusters M01 to M11, D02 D04 Q01 as aliases, Q02 an old client code with no Rev 6 cluster, a rev6 block that no code reads. The base heights are not confirmed by the architect, so every R67 row carries a note.
- Round 12 (blocks, row split, road fronts, levels, V9) is merged as PR 14. Round 11 is PR 13, round 10 is PR 12. Round 9 (floor, ramp and link relations) is PR 11 and its Revit code has NOT run in Revit yet.
- Top Surface-02 is the finished road for the ramps (Bader decided). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF (pdfplumber), Excel (openpyxl), link table, second source check, face reasons, LandXML reader, surface lookup, ramp levels, surface roles, road check, cluster analysis, block scan, levels and the proofs (Python, shapely, numpy).
- Gate: Python 405 passed, .NET 155 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 12b: R73 block scan, R74 Excel unit by baseplan label, R75 clusters with no Rev 6 match, R76 second block match, R77 block against link, R62 R64 R67 R69 updated. Python went from 323 to 405 passed, .NET stayed at 155.
- Round 12: R67 to R72, the corrected Rev 6 profile, drawing roles in config/project.json.
- Round 11: R63 project settings, R64 cluster derivation, R65 and R66 for checks 1 and 2.
- Round 10: rules/roshn-makkah.json, the loader (R62), rules/README.md.
- Round 9: floor measurements, parameter details, link state and spot hosts in the Revit reader (R51 to R56) and plot_relations.py (R57 to R61).
- Rounds 1 to 8: readers, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook, plot number shift, second source check, LandXML road levels, ramp levels, role proposal, floor count.

## Open bugs

- The unit code of a block is the client plot type of the cluster, not a Rev 6 unit. Townhouse blocks give TH1 or TH3 to their two end duplex plots and 123 quads read as unknown (question 1 in the log).
- 159 VL2 plots have no unit code. The 73 VL2 plots with a schedule unit take DP4, DP2, VD4, VD2, VL4 or VL6, so VL2 is not one unit.
- The Excel unit uses the config shift only, so an edge plot one label off (R28) takes another plot's unit.
- The sheet FFL sits 1 to 5 mm below the rule in 110 plots and 100 mm below in plots 2560 to 2563. The profile base height stays as decided.
- The mirror flag of a block is not the mirrored flag of its Revit link, 28 same and 86 opposite. The pair rule on blocks is not coded.
- Plot numbers in analysis.json are baseplan labels. 12 plots fail the location check. Sheet 009027 plot 2549 and sheet 009028 plot 2589 hold bad values in the source.
- 119 ramps have no covered corner on Top Surface-02. Yard floors have no Plot parameter. Unproven in Revit: the sketch plane at 150 m, the floor slope parameter, spots inside links, the round 9 code.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, first the unit code source. Then run steps/round-12b.md.
2. Restore the Generic 150mm floor of plot 2441, then run steps/for-bader-round-9.md and give me the File size line and plot_relations_summary.md.

## Next three steps

1. Settle the unit code source, then join the official plot numbers to analysis.json and feed levels.py from it.
2. Code check 3 (one FFL per cluster) with the split positions of the profile, since the sheet gives two FFLs to 24 of 37 four unit clusters.
3. Read plot_relations_summary.md from the new Revit file and decide the yard rule and the ramp slope meaning.
