STATE 2026-10-24 18:10, Round DATA done and merged (PR 26), the data book holds stand-in tables until the full Civil 3D read has the memory to run

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round DATA (R160 to R168, PR 26): the data book of Phase 1 in Documents\SiteAdaptation\data_book. Border Phase 1 Bound holds 747 of 747 schedule plots. The full Civil 3D window runner and watcher are built, but three tries were stopped by the 2 GB memory floor before NETLOAD, so the 14 drawings have CadReader stand-in tables. plots.csv 2,956 plots, 62 columns. No source gives a Rev 6 unit code for plots with no schedule, no source names IME, the drawn setback line sits at 5.00 m. Summary: steps/investigations/data-book.md
- Round 14c Part A (R131 to R134): cad_rules.py tests the villa mirror, the CASE second letter and the FFL split. None holds on every plot outside the 16 cut plots, so none is coded
- Round 14c Part B (R135 to R139): plan_imc_cad.json holds Generic, yard and ramp floors for its 224 plots. On 218 plots: Generic outline within 10 mm 217, yard 215, ramp 218, ramp corners within 30 mm 164, ramp type 140
- 16 cut plots (R120 and cadLinks.cutNeighbours) are counted apart everywhere, plotFrame measured with all 16 apart
- Round C3D (R150 to R159, PR 22): ROW is the plot line, sidewalk median 4.15 m. Top Surface-02 snapped within 5 mm gives 672 of 674 IMC ramp front corners, its edge on the plot line within 2 mm conflicts with the curb reading of R127
- Round 14 (R120 to R130, PR 20 and 21): links from CAD, rotation 337 of 337, mirror by drawn side 218 of 218. plan_imc_cad.json with links and levels for 224 of 337 plots
- Round 15b (R111 to R119): floors in plan.json and the Floors step of the placer, proven in Revit on IMC
- Round 15a (R80 to R89, R102 to R110): plan_from_past.py, the placer, the test host and the watcher, 337 of 337 links within 1 mm in Revit
- Rounds 1 to 13: readers, road, LandXML, profile, clusters, levels, road lookup, entry rule, block offsets (R1 to R101)
- Gate: Python 768 passed and 1 skipped, .NET 369 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round DATA: R160 to R168, PR 26. Round 14c: Part A R131 to R134 (PR 24), Part B R135 to R139 (PR 25). Round C3D: R150 to R159, PR 22 and 23
- Round 14: Part A R120 to R124 (PR 20), Part B R125 to R130 (PR 21). Round 15b: R111 to R119, PR 19
- Rounds 1 to 15a: PRs 1 to 18

## Open bugs

- The full Civil 3D read has not run, so the Civil 3D objects inside Phase 1 (Rough Grading and Road Layout surfaces, alignment, assembly, parcel) are UNKNOWN and the watcher has not met the security dialog yet
- No villa mirror rule, the CAD plan leaves the 107 villas out. No CASE or linkName rule
- The ramp type from the slope bands differs on 78 of 218
- levels.py never splits, so 25 FFLs of split clusters differ from the schedule. 24 FFLs lie on a half millimetre
- CadReader puts some nested Road Layout pieces at swapped coordinates
- The site-adaptation-tool-15a folder is empty but held open by another process
- 2338 is 10 mm off the fitted origin. Plots 2301 and 2302 are VD4 on the link and DP4 in the schedule
- The placer stops on an angle to true north other than 0
- Unproven in Revit: the parallel windows of R110, the round 9 code, plan_imc_cad.json

## Bader's turn

1. Answer the 6 questions of round DATA, first the memory for the full Civil 3D window
2. Answer the questions of Round 14c, first the ramp type and the villa mirror
3. Answer the 7 questions of round C3D, first where Top Surface-02 ends
4. Close the session held in site-adaptation-tool-15a so the empty folder can go

## Next three steps

1. The full Civil 3D read of the 14 copies once memory allows, then Rough Grading against El @ Entry
2. The ramp type, the villa mirror and the front level from the snapped road surface once Bader answers
3. Round 16, a test host run of plan_imc_cad.json on a work file
