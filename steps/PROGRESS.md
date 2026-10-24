# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round 15b (R111 to R119): the yard of a plot is the yard with the largest share on its baseplan face. plan.json holds Generic, yard and ramp floors with type, level, height offset, loops with arcs, corners and the Plot value, and floorSettings from config/project.json floors. The placer has a Floors step (links, floors or both), the test host runs master and floor jobs, python/scenario_files.py keeps one master and short lived work files, placer_check.py --floors checks a scenario.
- Proven in Revit on the IMC model: 1009 floors placed in 61 s, all equal to the past model, outline worst 0 mm, 288 yard tops differ because the plan puts every yard at FFL minus 0.100. Run twice, dry run, partial, missing type, rollback and links with floors all pass. The test master stays in Zone 02\ARC, links unloaded, no floors.
- Round 13 (pull request 17): the Excel unit is the only unit code and a footprint block is a cluster hint (R90), 225 clusters, none unknown. python/road_lookup.py asks the surfaces in file order, Top Surface-02 first (R91). Entry rule (R94): ramp entry at the midpoint of the higher long edge, 205 of 337 within 5 mm, no ramp the road at the curb plus 0.140 m, 43 of 747 within 5 mm and 361 within 30 mm. Block offsets and the entrance are in config/prototype_offsets.json (R97 to R100), 15 groups, a second relation in villas.
- Round 12b (unit codes from blocks and schedule rows, building footprint drawing) was pull request 16, its block unit source is replaced by round 13. python/cad_analysis.py gives each baseplan plot a unit code from its block, else its schedule row, else none (R69, R74), and builds the clusters from that code only. The baseplan label type stays on the plot as cadType. drawings.villaBlocks in config/project.json is Makkah Building Footprint.dwg. Q02 has no alias and each Q02 cluster is an issue (R75). New scripts: block_scan.py (R73), block_proof.py (R76), link_proof.py (R77). levels.py rounds the FFL to the mm and flags a height outside 0.10 to 0.54 m.
- Real baseplan: 535 clusters, M02 153, M04 293, unknown 89, Q02 0 (round 12 had 548: M02 143, M04 282, M09 61, Q02 62). Unit code source: block 2346, excel 73, none 159. The block names are client plot types and a block names a whole cluster, so 123 old M09 and Q02 clusters become unknown. The schedule units alone give 225 real Rev 6 clusters and no unknown.
- Level proof on the past schedules: FFL 633 of 747, steps 745, count rule alone 747. With the CAD clusters: FFL 548, steps 649. The sheet FFL is not the computed FFL rounded down to the cm or to 5 mm.
- Round 15a test (R102 to R110): dotnet/SiteAdaptation.TestHost runs the placer and the reader in Revit with no clicks, only for a job of its run script, one job file and token per window. The dialog watcher answers the add-in security boxes, Do Not Load for other add-ins and Load Once for the three of this tool. The placer takes a prototype file filter and writes the link instance Name. python/placer_check.py checks a run against the answer key. The upgrade job saved the 13 KEO prototypes as Revit 2024 centrals in Zone 02\ARC\Prototypes_2024 and plan.json now points the placer there.
- Proven in Revit on the IMC copy: all 13 prototypes, 337 of 337 plots, within 1 mm with rotation, mirror, Z, Name and 9 parameters equal (8 on the 2021 files, 5 on the 2024 copies). Not proven in Revit: the parallel windows of R110 and Run-Parallel.ps1, they wait for the combined test after Round 15b.
- Round 15a (plan.json from the past IMC job and the placer step 1) is pull request 15. python/plan_from_past.py builds plan_imc_past.json outside the repo (R80 to R82). dotnet/SiteAdaptation.Placer is Place from plan (R83 to R88). python/plan_compare.py compares a plan with the placed model (R89).
- Past plan: 337 of 337 plots written, Generic 336 (2441 none), yard 336 (2311 none), ramp 337. Position proof 0 mm on 94 links.
- The profile follows the manual Rev 6: 16 typologies, clusters M01 to M11, D02 D04 Q01 as aliases, Q02 an old client code with no Rev 6 cluster. The base heights are not confirmed by the architect, so every R67 row carries a note.
- Rounds 12, 11 and 10 are PRs 14, 13 and 12. Round 9 (floor, ramp and link relations) is PR 11 and its Revit code has NOT run in Revit yet.
- Top Surface-02 is the finished road for the ramps (Bader decided). The other five design surfaces are not confirmed.
- Readers: CAD (C#, ACadSharp), PDF, Excel, link table, second source check, face reasons, LandXML, surface lookup, ramp levels, surface roles, road check, cluster analysis, block scan, levels, the proofs, plan builder, plan compare and full run (Python).
- Gate: Python 673 passed and 1 skipped, .NET 347 passed.
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git.

## Done

- Round 15b: R111 to R119, floors in the plan and the placer, eight scenarios in Revit. Python 620 to 673, .NET 319 to 347. Not committed yet, the hook blocked the session (log question 1).
- Round 15a test: R102 to R110, the test host, the watcher, the upgrade job, the registry hook. Python 573 to 620 and .NET 205 to 319 after the merge with Round 13.
- Round 12b: R73 to R77, R62 R64 R67 R69 updated. Python 323 to 425 on its own. Round 15a: R80 to R89, the placer, the plan builder and compare. Python 323 to 366, .NET 155 to 205 on its own.
- Round 12: R67 to R72, the corrected Rev 6 profile, drawing roles in config/project.json. Round 11: R63 to R66. Round 10: profile and loader (R62). Round 9: R51 to R61.
- Round 8: R47 to R50. Rounds 1 to 7: readers, road, LandXML, explode, layer filter, plot labels, PDF rows, link table, full run, main guard hook. Revit round 7 ran in Revit on 22 Oct.

## Open bugs

- The unit code of a block is the client plot type of the cluster, not a Rev 6 unit. Townhouse blocks give TH1 or TH3 to their two end duplex plots and 123 quads read as unknown (question 1 of round 12b in the log).
- 159 VL2 plots have no unit code. The 73 VL2 plots with a schedule unit take DP4, DP2, VD4, VD2, VL4 or VL6, so VL2 is not one unit.
- The Excel unit uses the config shift only, so an edge plot one label off (R28) takes another plot's unit.
- The sheet FFL sits 1 to 5 mm below the rule in 110 plots and 100 mm below in plots 2560 to 2563. The profile base height stays as decided.
- The mirror flag of a block is not the mirrored flag of its Revit link, 28 same and 86 opposite. The pair rule on blocks is not coded.
- The past yards sit at the FFL except TH1, TH3 and VL6 at FFL minus 0.100, so 288 yard tops differ from the plan. Plot 2311 has no yard, its yard lies on baseplan face 326. 767 floor overlap warnings per full run.
- RRE_ZZ_SP_Prototype-FFL differs from the schedule FFL on 258 plots, up to 32 mm. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule.
- The placer stops on an angle to true north other than 0. The reader, the placer, the test host and the watcher share one SiteAdaptation.Common.dll, so all come from one build.
- Unproven in Revit: the parallel windows of R110, the round 9 code. 1 of 987 spots has no host (element 996508). Four Always Load answers of other add-ins were removed from the registry by my install of 24 Oct, Bader keeps them off.
- Plot numbers in analysis.json are baseplan labels. 12 plots fail the location check. Sheet 009027 plot 2549 and sheet 009028 plot 2589 hold bad values in the source.
- The main guard hook only loads in a session that starts inside the repo.

## Bader's turn

1. Answer the questions of the Round 15b entry in steps/log.md, first the commit block, then the yard depth.
2. Answer the open questions of the Round 15a test and Round 13 entries, first the free memory for the parallel windows and the entry rule.
3. Restore the Generic 150mm floor of plot 2441, then run steps/for-bader-round-9.md and give me the File size line and plot_relations_summary.md.

## Next three steps

1. Group the offsets by relation, settle the entry rule, then code check 3 (one FFL per cluster) and wire levels.py to the official plot numbers.
2. Merge Round 15b, then the combined Revit test with the parallel windows on the 2024 copies and the test master.
3. Fill plan.json from CAD and LandXML with the same format, so the placer does not change.
