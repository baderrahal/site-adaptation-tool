# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 13 (pull request 17): the Excel unit is the only unit code and a footprint block is a cluster hint (R90), 225 clusters, none unknown. python/road_lookup.py asks the surfaces in file order, Top Surface-02 first (R91). Entry rule (R94): ramp entry at the midpoint of the higher long edge, 205 of 337 within 5 mm, no ramp the road at the curb plus 0.140 m, 43 of 747 within 5 mm and 361 within 30 mm. Block offsets and the entrance are in config/prototype_offsets.json (R97 to R100), 15 groups, a second relation in villas.
- Round 12b (unit codes from blocks and schedule rows, building footprint drawing) was pull request 16, its block unit source is replaced by round 13. python/cad_analysis.py gives each baseplan plot a unit code from its block, else its schedule row, else none (R69, R74), and builds the clusters from that code only. The baseplan label type stays on the plot as cadType. drawings.villaBlocks in config/project.json is Makkah Building Footprint.dwg. Q02 has no alias and each Q02 cluster is an issue (R75). New scripts: block_scan.py (R73), block_proof.py (R76), link_proof.py (R77). levels.py rounds the FFL to the mm and flags a height outside 0.10 to 0.54 m.
- Real baseplan: 535 clusters, M02 153, M04 293, unknown 89, Q02 0 (round 12 had 548: M02 143, M04 282, M09 61, Q02 62). Unit code source: block 2346, excel 73, none 159. The block names are client plot types and a block names a whole cluster, so 123 old M09 and Q02 clusters become unknown. The schedule units alone give 225 real Rev 6 clusters and no unknown.
- Level proof on the past schedules: FFL 633 of 747, steps 745, count rule alone 747. With the CAD clusters: FFL 548, steps 649. The sheet FFL is not the computed FFL rounded down to the cm or to 5 mm.
- Round 15a (plan.json from the past IMC job and the placer step 1) is pull request 15, merged. python/plan_from_past.py builds plan_imc_past.json outside the repo (R80 to R82). dotnet/SiteAdaptation.Placer is the Revit command Place from plan (R83 to R88), dry run by default, its logic in Common and tested on fakes. python/plan_compare.py compares a plan with the placed model (R89). Nothing of it has run in Revit yet.
- Past plan: 337 of 337 plots written, yard missing on all 337 (R58 gives ambiguous), Generic missing on 1. Position proof 0 mm on 94 links.
- The profile follows the manual Rev 6: 16 typologies, clusters M01 to M11, D02 D04 Q01 as aliases, Q02 an old client code with no Rev 6 cluster. The base heights are not confirmed by the architect, so every R67 row carries a note.
- Rounds 12, 11 and 10 are PRs 14, 13 and 12. Round 9 (floor, ramp and link relations) is PR 11 and its Revit code has NOT run in Revit yet.
- Top Surface-02 is the finished road for the ramps (Bader decided). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF, Excel, link table, second source check, face reasons, LandXML, surface lookup, ramp levels, surface roles, road check, cluster analysis, block scan, levels, the proofs, plan builder, plan compare and full run (Python).
- Gate: Python 565 passed, .NET 205 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 12b: R73 to R77, R62 R64 R67 R69 updated. Python 323 to 425 on its own. Round 15a: R80 to R89, the placer, the plan builder and compare. Python 323 to 366, .NET 155 to 205 on its own.
- Round 12: R67 to R72, the corrected Rev 6 profile, drawing roles in config/project.json. Round 11: R63 to R66. Round 10: profile and loader (R62). Round 9: R51 to R61.
- Round 8: R47 to R50. Rounds 1 to 7: readers, road, LandXML, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook. Revit round 7 ran in Revit on 22 Oct.

## Open bugs

- The unit code of a block is the client plot type of the cluster, not a Rev 6 unit. Townhouse blocks give TH1 or TH3 to their two end duplex plots and 123 quads read as unknown (question 1 of round 12b in the log).
- 159 VL2 plots have no unit code. The 73 VL2 plots with a schedule unit take DP4, DP2, VD4, VD2, VL4 or VL6, so VL2 is not one unit.
- The Excel unit uses the config shift only, so an edge plot one label off (R28) takes another plot's unit.
- The sheet FFL sits 1 to 5 mm below the rule in 110 plots and 100 mm below in plots 2560 to 2563. The profile base height stays as decided.
- The mirror flag of a block is not the mirrored flag of its Revit link, 28 same and 86 opposite. The pair rule on blocks is not coded.
- R58 cannot assign a yard on the past job, all 337 are ambiguous. Yard floors have no Plot parameter.
- RRE_ZZ_SP_Prototype-FFL differs from the schedule FFL on 258 plots, up to 32 mm. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule.
- The placer stops on an angle to true north other than 0. Both add-ins share one SiteAdaptation.Common.dll in Revit.
- Unproven in Revit: the whole placer, the link box and spot host fixes, the round 9 code.
- Plot numbers in analysis.json are baseplan labels. 12 plots fail the location check. Sheet 009027 plot 2549 and sheet 009028 plot 2589 hold bad values in the source.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the numbered questions in the top entry of steps/log.md, first the entry rule. Then run steps/round-13.md.
2. Run steps/round-15a.md on a copy of the IMC model and answer the questions of the Round 15a entry, first the yard rule.
3. Restore the Generic 150mm floor of plot 2441, then run steps/for-bader-round-9.md and give me the File size line and plot_relations_summary.md.

## Next three steps

1. Group the offsets by relation, settle the entry rule, then code check 3 (one FFL per cluster) and wire levels.py to the official plot numbers.
2. Fix what the Revit run of round 15a shows, then place the floors from plan.json once the yard rule is set.
3. Fill plan.json from CAD and LandXML with the same format, so the placer does not change.
