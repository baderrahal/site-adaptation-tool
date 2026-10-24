# Round DATA-2, full record

The full top entry of steps/log.md as written at the end of the round.

## Round DATA-2, 2026-10-24, the full Civil 3D read, the analysis fixes, the wiring (Part W) and the location plan (Part L)

Goal:
- Part A the full Civil 3D read of Road Surface, Roads Road Layout and Rough Grading. Part B El @ Entry from the road and the ramp, units per face, the second method, the width meaning, two conflicts out. Summary: steps/investigations/data-book.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-data2 on data/data-book-2 from main at a94bf1e. Plan written before the first edit. All 15 sources and 15 copies hash the same before and after
- A: free memory 5.62 GB at the start, 6.16 GB after dotnet build-server shutdown, floor kept at 2 GB. Road Layout try 1: the watcher clicked Do Not Load on the probe dialog, because Civil 3D 2026 shows the DLL as a Name and a Location line. Fixed in R161, try 2 and the other two drawings ran once each (40, 25, 25 s, lowest free 3.11 GB). The three folders now hold reader C3DProbe tables, 18 surfaces exported to LandXML (R169), plots.csv 98 columns with no old cell changed
- A found: Road Layout 2 surfaces, its alignment and assembly lie outside Phase 1, site CEC holds 0 parcels. Road Surface 7 surfaces, 1 empty point group. Rough Grading 9 surfaces with triangles inside and 1 volume surface
- B1 (R170), 747 schedule plots, within 5 mm, 30 mm, worse: slope 0.5 percent 217, 171, 356. 2 percent 35, 135, 574. By typology 221, 193, 330 (3 no level). Rough Grading surfaces sit 0.6 to 1.3 m below, none gives El @ Entry
- B2 (R171): every unit face holds one unit. Units 2,698 from the footprint blocks (2,578 on faces, 120 in 81 blocks on no face), Rev 6 2,704 (gap 6), Phase1 sheet 2,701 (gap 3, all villas VL2 to VL4). C10, duplex and townhouse rows match the sheet
- B4 (R172): the profile key is plot_width_m, a plot width, and each plot holds one unit
- B5: V9 numbering moved to referenceV9 and VL4 width to widthFromCad in R167, C10 and VL3 widths stay as conflicts
- W (R174 to R178): the 12 coordinate PDFs copied and read (3,346 rows, 746 plots). python/wiring.py scores 2 to 7 methods per source against the past plan (336 plots) and the 747 schedule plots. Chosen: footprint and R06 blocks everyShare (336 of 336, was 304), curb and ROW anyEdge (312 and 336, was 308 and 331), setback bestShare (no neighbour line, was 26), the rest stay. plots.csv 110 columns with a method column per wired column. Summary: steps/investigations/wiring.md
- W findings: setback, widths, El @ Entry bands and units unchanged. 246 DP ends of townhouse rows now carry their row block, 9 OS plots lost a neighbour's setback, curb on all 2,956 plots. The PDF check refuses 13 schedule plots (IMC 9, 6 of them cut plots), no edge plot one label off. Footprint against R06 1,865 same, about 15 percent of each curb has no partner in the other road layout within 1 m
- L (R179 to R184): the Zone 4 location plan read, 3,181 labels, 2,704 client codes. Fit on IMC 0.082 m median (316 of 337), IMD 0.095 m, control marks 0.27 to 0.31 m. IMC shift 10 337 of 337, IMD 11 410 of 410, a fixed shift holds for NBH 012, 013, 021 and 02 only. Codes equal Typology_Rev9 on 2,591 of 2,591 and Landuse_Level_4 on 16 of 16 codes, 113 plan plots have no sales row. 1,819 of 1,831 unit plots with no schedule unit now get Typology_Rev9. IMC is NBH 012 and IMD 013, Zone 4 is 01A, nothing names IME
- New: python/data_book_retest.py, python/data_book_retest_check.py, python/wiring.py, python/wiring_check.py, python/location_plan.py, python/location_plan_check.py, the retest settings in c3d_probe.json, exportKnownSurfaces, README lines for the new files. Gate: Python 768 to 808 passed and 1 skipped, .NET 369 to 371 passed

Checked:
- Second method (R173): fronts seed 14 20 of 20 on width, width class and setback class, seed 7 19, 20, 19. Per line at 3.00 m seed 14 20, 20, 20, seed 7 19, 19, 20. The misses are corner chamfers (plot 735). El @ Entry bands 20 of 20 at both seeds for all three variants. Export against the earlier LandXML: 0 corners differ over 1 mm
- R168 again: rows match data.json on 14 of 14 drawings, plots.csv seed 14 and 7 20 of 20, the share wired blocks left to R178
- L second method (R184): fit on IMD, label spelled, face and code 20 of 20 at seeds 14 and 7 (3 with no sales row). The PDF and both Rev 9 workbooks hash the same before and after
- W second method (R178): footprint block, R06 block, setback owner, curb, ROW, cutsheet, V9 and label plus shift 20 of 20 at seeds 14 and 7. All 27 sources and copies hash the same after Part W

Remains:
- A later round should change, not this one: the El @ Entry rule (not coded, see Your turn 1), the 5.00 m setback line in rules/

Known bugs:
- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT, after data.json is written, the copies stay unchanged
- Road Layout counts differ between the stand-in and the full read (texts 1,537 against 688), listed in the conflicts, not resolved

Next:
- The ramp type and the El @ Entry rule once Bader answers, then Round 16, a test host run of plan_imc_cad.json on a work file

Your turn:
1. El @ Entry rule. Recommended: do not code it yet, by typology fits 221 of 747 within 5 mm. Ask the architect what sets the ramp slope of a plot
2. Ramp type. Recommended: Ramps 2% for C10, VL4, VL6 and Ramps 0.5% for the rest (311 of 321), and say what makes a Flat Ramp
3. Villa mirror. Recommended: confirm one hand per row set by the open end, VL6 the other hand, and say what decides a villa alone between two cluster units
4. Setback line. Recommended: the drawn line sits at 5.00 m (2,530) or 3.00 m (40), keep the ramp at 5.52 m and treat the line as the building setback
5. Plot front level. Recommended: the road surface snapped within 5 mm at the plot line, Top Surface-02 for IMC and Top Surface-01 for IMD
6. Back corners. Recommended: the back edge parallel to the front with its mean at El @ Entry
7. Generic floor. Recommended: the whole baseplan face
8. Round 16 test host run. Recommended: yes, on a work file, once 2 is settled
9. Units. Recommended: count from the footprint blocks (2,698) and ask the master planner for the 3 villas the sheet holds more and the 6 of Rev 6
10. Unit codes for new zones. Recommended: ask the client for the Rev 6 unit schedule of Phase 1 by plot
11. Widths over Rev 6, C10 58 and VL3 41. Recommended: list them for the architect to confirm the unit fits
12. IME. Recommended: send its schedule or its boundary
13. FFL split. Recommended: ask the architect why M07 and M08 split at the middle wall and M11 never
14. CASE. Recommended: confirm the second letter is the hand as built
15. VL6 hand. Recommended: confirm in Revit that the VL6 file is drawn the other hand from VL4
16. Yard depth per unit. Recommended: ask the architect, the plan keeps FFL minus 0.100 until then
17. Plots 2311 and 2312. Recommended: follow the baseplan and the footprint drawing (VL4, VL4, OS) and keep them listed apart
18. Plots 2301 and 2302. Recommended: DP4, as the schedule and the file say
19. The 58 plots whose past ramp sits a median 97 mm above the surface. Recommended: a design choice of the past job, list them apart
20. RRE_ZZ_SP_Prototype-FFL. Recommended: keep the schedule FFL and never write the parameter
21. Angle to true north. Recommended: keep the stop until a project has one
22. Free memory for the parallel windows. Recommended: more than 8 GB free before the combined test, this round had 6.2 GB
23. TEST copies in Zone 02\ARC. Recommended: delete them when you no longer need them, keep Prototypes_2024
24. The empty site-adaptation-tool-15a folder. Recommended: close what holds it and delete it, steps/bader-data-2.md
25. Curb and ROW distance. Recommended: keep anyEdge (312 and 336 of 336) for the distances and the R122 front for width, links and levels
26. The 13 schedule plots the PDF location check refuses. Recommended: keep label plus shift, it agrees on all 336 IMC plots, and keep the 13 listed
27. The two curbs differ by more than 1 m on about 15 percent of their length. Recommended: keep the Roads Road Layout curb, it scores against the past plan, and ask which road layout is current
28. Two DP4 labels in a face with many labels. Recommended: ask for that baseplan face to be split, until then they stay unwired
29. The Zone 4 IMG past job (Revit model, coordinate sheets, Excel) as a second answer key. Recommended: send it if it exists, no IMG file is on this machine and 00_Revit Models holds Zone 02 only
30. Unit codes for plots with no schedule. Recommended: take Typology_Rev9 by the location plan number (1,819 of 1,831), replaces question 10
31. Official numbers. Recommended: take the location plan number per face for every NBH, it agrees on 746 of 747, a fixed shift fails in 01A, 03 and 022
32. 113 plan plots with no Rev 9 sales row (81 in Zone 4). Recommended: ask whether the sales sheet is complete for Zone 4
33. Location plan versions. Recommended: keep the Rev.09 PDF and read the DWG of 01_Output next, the other PDF is a later export

