STATE 2026-10-24 21:50, Round DATA-2 done and merged (PR 28) with Parts A, B, W and L, the data book is read in full, wired by the best method per source and tied to Rev 9 by the Zone 4 location plan

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md and steps/history/.

## State

- DATA-2 (R169 to R184): C3DProbe tables for the three Civil 3D drawings, El @ Entry by typology 221, 193, 330 of 747. Part W wires each source by its best scored method. Part L: the location plan gives the official number (746 of 747) and Typology_Rev9 for 2,566 unit plots. Summary: steps/investigations/data-book.md and wiring.md
- DATA (R160 to R168): the data book of Phase 1 in Documents\SiteAdaptation\data_book, plots.csv 2,956 plots, 115 columns
- 14c (R131 to R139): villa mirror, CASE, FFL split not coded. plan_imc_cad.json floors for 224 plots. 16 cut plots counted apart
- C3D (R150 to R159), 14, 15a, 15b: road surface at the plot line, links from CAD, the placer with links and floors proven on IMC
- Rounds 1 to 13: readers, road, LandXML, profile, clusters, levels, entry rule, block offsets (R1 to R101)
- Gate: Python 808 passed and 1 skipped, .NET 371 passed. Real files are read in place, nothing real is in git

## Done

- DATA-2: R169 to R184, PR 28. DATA: R160 to R168, PR 26. 14c: R131 to R139, PR 24, 25. C3D: R150 to R159, PR 22, 23. 14: R120 to R130, PR 20, 21. 15b: PR 19. 1 to 15a: PRs 1 to 18

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. Road Layout counts of stand-in and full read differ
- No villa mirror, CASE, linkName or El @ Entry rule. Ramp type differs on 78 of 218. levels.py never splits (25 FFLs)
- The placer stops on an angle to true north other than 0. Unproven in Revit: R110, round 9, plan_imc_cad.json

## Bader's turn

1. El @ Entry rule: not coded, by typology fits 221 of 747 within 5 mm, ask the architect what sets the ramp slope
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest, and what makes a Flat Ramp
3. Villa mirror: one hand per row set by the open end, VL6 the other hand, and the villa alone between cluster units
4. Setback line: 5.00 m (2,530) or 3.00 m (37) is the building setback, the ramp stays 5.52 m
5. Plot front level: the road surface snapped within 5 mm, Top Surface-02 for IMC, Top Surface-01 for IMD
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face
8. Round 16 test host run: yes, on a work file, once 2 is settled
9. Units: count from the footprint blocks (2,698), ask for the 3 villas of the sheet and the 6 of Rev 6
10. Unit codes with no schedule: take Typology_Rev9 by the location plan number (1,819 of 1,831)
11. Widths over Rev 6, C10 58 and VL3 41: the architect confirms the unit fits
12. IME: send its schedule or boundary, the plan names none, NBH 02 and 03 (Zone 3) are the candidates
13. FFL split: ask why M07 and M08 split and M11 never. 14. CASE: the second letter is the hand as built
15. VL6 hand: confirm in Revit it is drawn the other hand from VL4. 16. Yard depth: FFL minus 0.100 until answered
17. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart
18. Plots 2301 and 2302: Rev 9 and the link say VD4, the schedule DP4, take VD4 from Rev 9
19. The 58 past ramps a median 97 mm above the surface: a past design choice, listed apart
20. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 21. Angle to true north: keep the stop
22. Parallel windows: more than 8 GB free, this round had 6.4 GB. 23. TEST copies in Zone 02\ARC: delete when done
24. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md
25. Curb and ROW distance from the R94 edge (312 and 336 of 336), the R122 front stays for width, links, levels
26. The 13 plots the PDF check refuses: keep label plus shift (336 of 336 IMC agree), listed
27. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current
28. Two DP4 labels in a face with many labels: ask for that face to be split
29. The Zone 4 IMG past job as a second answer key: send it if it exists, none is on this machine
30. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
31. 113 plan plots with no Rev 9 sales row (81 in Zone 4): ask whether the sales sheet is complete
32. Location plan versions: keep the Rev.09 PDF, read the DWG of 01_Output next

## Next three steps

1. The ramp type and the El @ Entry rule once Bader answers 1 and 2
2. Official numbers and Typology_Rev9 from the location plan into the plan builder once Bader answers 10 and 30
3. Round 16, a test host run of plan_imc_cad.json on a work file
