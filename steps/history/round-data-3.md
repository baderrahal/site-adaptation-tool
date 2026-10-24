# Round DATA-3, full record

The full top entry of steps/log.md as written at the end of the round.

## Round DATA-3, 2026-10-24, the Master Plan Rev 9 folder, El @ Entry by terms, the units on no plot and the location plan DWG

Goal:
- R the Rev 9 workbooks, drawing and PDF, E El @ Entry from the cross sections, U the units on no plot, V the VL6 hand, L2 the location plan DWG. Summary: steps/investigations/data-book.md, steps for Bader: steps/bader-data-3.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-data3 on data/data-book-3 from main at a203d0a, rebased on 54dbcfb after Z4 merged PRs 30 and 31. Plan written before the first edit. 10 sources copied into data_book\work, all 10 sources and copies hash the same before and after. The 01_Output PDF was read once in place before its copy was made
- Windows (R197): one Civil 3D run under the lock (5.26 GB free at the lock, lowest 2.82 GB, 0 waits, no lock of Z4 met). The own-only watcher left "Annotation Scale - Large Number of Scales" as rule 3 says, I stopped my own acad.exe after 100 s and removed its .dwl files, the copy unchanged. No Revit window: every Revit start shows the dialogs of two other add-ins first. Z4 merged its own lock (R200), the Civil 3D runner now uses it, my lock module is gone
- R1 (R185): TBD is newer, its Time Line says Rev 9 deleted plots 112, 84, 3165 and added the Amana numbers, its PH_1-DLUS lacks those 3 and has Amanah_PID on all 3,194 plots. Only the accommodation workbook holds the sales sheet. DLUS without formulas and the code table are equal in both
- R2 (R186): DLUS front 5.50 m on 1,521 unit plots, 7.00 m on 1,031 (TH), the drawn 5.00 m line equals it on 0 of 2,552, Rev 6 equal on 1,521
- R3 (R187): Land Width equals CAD on 2,493 of 2,555. C10 61 wide, 59 confirmed by the sales sheet, Rev 9 C10 47, VL6 14. VL3 43 between 12 and 16 m, 42 confirmed, Rev 9 VL4 16, VD4 15, DP4 9, DP2 3
- R4 (R188): typology_codes in rules/roshn-makkah.json, 18 rows, 18 equal the sheet. Location plan codes 9 exact and 11 by base, 0 outside, Typology_Rev9 15 codes all new
- R5 to R7 (R189 to R191): CadReader writes the DWG class list (3,480 Civil 3D parcels the DWG holds) and the header dates. The DWG has no plot number text and no setback layer. The PDF plot gives 3,171 labels, fit median 0.031 m, pointInside 746 of 747. Faces from the PDF strokes, refit median 0.010 m: 2,481 of 2,578 unit plots same, 58 shape, 39 no face, numbers differ 3, codes 5. Nothing names IME or tells NBH 02 from 03
- E (R192, R193): curbs 0.12 by 0.20 m, up-stand 0.070 m on most, sidewalk 2.0 percent, the profile holds no curb value. Curb terms within 5 mm: all 40, no up-stand 99, no sidewalk 39, no ramp 52, against 221 of the R170 corner variant
- U (R194): the 81 blocks, 120 units, all Zone 4 (01b 72 blocks 109 units, 01a 9 blocks 11 units), in one unlabelled baseplan face, a location plan and a Rev 9 label inside every block
- V: not run, see above. The CAD frame of R123 gives VL4 and VL6 their link origin at opposite front corners
- L2 (R196): the DWG and the Rev.09 PDF hold the same 3,181 numbers and codes, the DWG plot 17 too, positions within 0.051 pt. The Rev.09 PDF is the latest (modified 2024-03-26 15:44). Its title block holds "Plot Layout: 01101-PSN-IMG-ZZZ-PIM-TU-000001", the DATA-2 line that it does not is corrected
- New: python/rev9_workbooks.py, rev9_drawing.py, cross_sections.py, entry_terms.py, stray_units.py, location_dwg.py, data3_check.py, the class list and dates in CadReader, parcel outlines in the probe, own-only in the watcher, the R200 lock in c3d_window.py, data book README lines. R195 unused. Gate: Python 852 to 892 passed and 1 skipped, .NET 405 to 408 passed

Checked:
- Second method (R198), seeds 14 and 7, 20 plots each by separate code: DLUS setbacks 20 of 20, land widths 20 of 20, Rev 9 faces 20 of 20 (grid overlap, class, number), best El @ Entry variant 20 of 20
- The baseline corner variant gives 221, 193, 330, 3 again, the same as DATA-2

Remains:
- The Rev 9 parcels in Civil 3D and the VL6 hand in Revit wait for Your turn 40

Known bugs:
- The strict reading of rule 3 blocks every window whose own dialog is not the probe dialog
- plots.csv was not rebuilt, the Rev 9 columns live in the data book rev9 folder only

Next:
- The ramp type and El @ Entry rules once answered, the Rev 9 parcels and VL6 once 40 is answered

Your turn:
1. El @ Entry rule: not coded, the curb terms fit 99 of 747 at best, the road at the plot line by typology 221: ask the architect for the ramp slope and the civil engineer if the road surface holds the sidewalk
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest, and what makes a Flat Ramp. 3. Villa mirror: one hand per row set by the open end, VL6 the other hand, and the villa alone between cluster units
4. Setback: take the DLUS front (5.50 m villa and duplex, 7.00 m TH), the drawn 5.00 m line equals it on 0 of 2,552, ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face. 8. Round 16 test host run: yes, on a work file, once 2 is settled
9. Units: 2,698 from the footprint blocks, the 120 on no plot are all Zone 4 (01b 109, 01a 11): count them, ask for a baseplan with 01b. 10. Unit codes with no schedule: Typology_Rev9 by the location plan number
11. Widths: take the sales Land Width (equals CAD on 59 of 61 wide C10, 42 of 43 VL3) and Typology_Rev9 (C10 47, VL6 14, the VL3 ones VL4, VD4, DP4, DP2). 12. IME: Rev 9 names none, send its schedule or boundary
13. FFL split: ask why M07 and M08 split and M11 never. 14. CASE: the second letter is the hand as built
15. VL6 hand: look in Revit by hand, steps/bader-data-3.md, the CAD frame puts the VL4 and VL6 link origins at opposite front corners. 16. Yard depth: FFL minus 0.100 until answered
17. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart. 18. Plots 2301 and 2302: take VD4 from Rev 9
19. The 58 past ramps a median 97 mm above the surface: a past design choice, listed apart. 20. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 21. Angle to true north: keep the stop, CAD gives grid north only
22. Parallel windows: more than 8 GB free, Z4 had 3.5 to 4.9 GB. 23. TEST copies in Zone 02\ARC: delete when done
24. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md
25. Curb and ROW distance from the R94 edge (312 and 336 of 336), the R122 front stays for width, links, levels
26. The 13 plots the PDF check refuses: keep label plus shift, listed. 27. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current
28. Two DP4 labels in a face with many labels: ask for that face to be split. 29. Zone 4 IMG past job: the location plan names 2.1 Site Adapatation\Zone 4\4.1.10 BIM Models\01101-PSN-IMG-ZZZ-PIM-TU-000001, send it
30. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
31. 113 plan plots with no sales row: the sales sheet has no plot of NBH 01b, ask for its 01b rows
32. Rev 9 plot faces: 115 unit plots differ (58 shape, 39 no face, 3 numbers, 5 codes, 18 TH1 for TH1-IM): keep the baseplan faces, list them, ask which plot layout is current
33. The 1.4 model named IMD holds NBH 012 (336) and 022 (294), no 01A: Z4 built 022, send a 01A past model if one exists
34. The 1.4 key levels sit a constant datum above its FFL parameter (643 of 643): treat the parameter as the design level
35. Estimated FFL a median 349 mm under the 1.4 key, its ramp fronts 210 mm off Top Surface-02: ask which road it used
36. The 1.4 key links 8 old prototypes (D2, D4 and others) not on this machine: send 20221206 - Links, until then floors only
37. Three pairs with the key ramp on the other edge (19.5 m): look at them in the two images. 38. The DT3 plot alone on a VL3 face: list it apart until 3 is answered
39. Reader fix of Z4 (rotation from the transform when Revit gives none): keep it, the reader changes only for a round
40. Rule 3: Do Not Load and the safe button inside our own window only, never another process, for both sessions. DATA-3 read it strictly and could not read the Rev 9 parcels or VL6
41. round_z4\work keeps the 2024 key copy (74 MB) and the new file (7 MB): keep until the next round, then delete
42. Rev 9 workbooks: take TBD for the DLUS and the Amana numbers, the accommodation workbook for the sales sheet it alone holds, ask for one workbook with both
