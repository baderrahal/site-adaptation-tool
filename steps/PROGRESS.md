STATE 2026-10-24 19:10, Round DATA-2 done, the full Civil 3D read ran on the three Civil 3D drawings and the data book analysis is fixed

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- Round DATA-2 (R169 to R173): Road Layout, Road Surface and Rough Grading have reader C3DProbe tables, 18 surfaces exported to LandXML. El @ Entry from road plus ramp, by typology: 221, 193, 330 of 747 within 5 mm, 30 mm, worse. Rough Grading gives no El @ Entry. Units 2,698 against Rev 6 2,704 and the Phase1 sheet 2,701, one unit per face. Summary: steps/investigations/data-book.md
- Round DATA (R160 to R168): the data book of Phase 1 in Documents\SiteAdaptation\data_book, border Phase 1 Bound with 747 of 747 schedule plots, plots.csv 2,956 plots, now 98 columns. The other 11 drawings keep CadReader stand-in tables
- Round 14c (R131 to R139): villa mirror, CASE and FFL split tested and not coded. plan_imc_cad.json holds Generic, yard and ramp floors for 224 plots. The 16 cut plots (R120) are counted apart everywhere
- Round C3D (R150 to R159): ROW is the plot line, Top Surface-02 snapped within 5 mm gives 672 of 674 IMC ramp front corners. Rounds 14, 15a, 15b: links from CAD, the placer with links and floors, proven in Revit on IMC
- Rounds 1 to 13: readers, road, LandXML, profile, clusters, levels, road lookup, entry rule, block offsets (R1 to R101)
- Gate: Python 782 passed and 1 skipped, .NET 371 passed
- Real files stay in C:\02-Site Adaptation and C:\Users\bader\Documents\SiteAdaptation and are read in place. Nothing real is in git

## Done

- Round DATA-2: R169 to R173. Round DATA: R160 to R168, PR 26. Round 14c: R131 to R139, PR 24 and 25. Round C3D: R150 to R159, PR 22 and 23. Round 14: R120 to R130, PR 20 and 21. Round 15b: R111 to R119, PR 19. Rounds 1 to 15a: PRs 1 to 18

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT, after the tables are written. Road Layout counts of the stand-in and the full read differ (texts 1,537 against 688)
- No villa mirror rule, no CASE or linkName rule, no El @ Entry rule. The ramp type from the slope bands differs on 78 of 218
- levels.py never splits, so 25 FFLs of split clusters differ from the schedule
- CadReader puts some nested Road Layout pieces at swapped coordinates. 2338 is 10 mm off the fitted origin
- The placer stops on an angle to true north other than 0. Unproven in Revit: R110, the round 9 code, plan_imc_cad.json

## Bader's turn

1. El @ Entry rule: not coded yet, by typology fits 221 of 747 within 5 mm, ask the architect what sets the ramp slope
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest, and what makes a Flat Ramp
3. Villa mirror: confirm one hand per row set by the open end, VL6 the other hand, and the villa alone between cluster units
4. Setback line: 5.00 m (2,530) or 3.00 m (40) is the building setback, the ramp stays 5.52 m
5. Plot front level: the road surface snapped within 5 mm, Top Surface-02 for IMC and Top Surface-01 for IMD
6. Back corners: parallel to the front with the mean at El @ Entry
7. Generic floor: the whole baseplan face
8. Round 16 test host run: yes, on a work file, once 2 is settled
9. Units: count from the footprint blocks, ask for the 3 villas of the sheet and the 6 of Rev 6
10. Unit codes for new zones: ask the client for the Rev 6 unit schedule of Phase 1 by plot
11. Widths over Rev 6, C10 58 and VL3 41: the architect confirms the unit fits
12. IME: send its schedule or its boundary
13. FFL split: ask why M07 and M08 split and M11 never
14. CASE: the second letter is the hand as built
15. VL6 hand: confirm in Revit it is drawn the other hand from VL4
16. Yard depth per unit: ask the architect, FFL minus 0.100 until then
17. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart
18. Plots 2301 and 2302: DP4, as the schedule and the file say
19. The 58 past ramps a median 97 mm above the surface: a past design choice, listed apart
20. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL, never write the parameter
21. Angle to true north: keep the stop
22. Parallel windows: more than 8 GB free before the combined test, this round had 6.2 GB
23. TEST copies in Zone 02\ARC: delete when no longer needed, keep Prototypes_2024
24. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md

## Next three steps

1. The ramp type and the El @ Entry rule once Bader answers 1 and 2
2. The villa mirror and the 5.00 m setback line in rules/ once Bader answers 3 and 4
3. Round 16, a test host run of plan_imc_cad.json on a work file
