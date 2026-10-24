STATE 2026-10-25 00:20, Round DATA-4 done and merged (PR 36): the Rev 9 parcels read in Civil 3D, the VL6 hand checked in Revit, the source of the past El @ Entry traced. Round 16 done and merged (PR 34)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- DATA-4 (R240 to R250): 3,266 Rev 9 plot parcels equal the baseplan faces, 97 shape, 2 number and 5 code differences are real, VL6 is drawn the other hand from VL4, the past schedules type El @ Entry from the Infrastructure IFC package and fit today's road, the 1.4 ramps sit 0.21 m over it. Summary: steps/investigations/data-book.md
- 16 (R220 to R236): the footprint test finds the 6 Z4 links on the wrong edge, orientation of the sales sheet sets the front of 338 corner plots with no break, the 1.4 job sits a median 194 mm over IMC, 01101-PSN-IMC-TEST-NEW.rvt holds 205 IMC plots from CAD. Summary: steps/investigations/round-16.md
- DATA-3 (R185 to R198, R195 unused): TBD is the newer Rev 9 workbook, DLUS front 5.50 m villas and duplexes and 7.00 m TH, El @ Entry by curb terms no better than 221 of 747, the 120 units on no plot all in Zone 4. Summary: steps/investigations/data-book.md
- Z4 (R200 to R218): new site file from CAD for DT3 of NBH 022. DATA-2 and DATA (R160 to R184): the data book of Phase 1
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD and the placer. Rounds 1 to 13: R1 to R101
- Gate: Python 925 passed and 1 skipped, .NET 421 passed. Real files are read from copies, nothing real is in git

## Done

- DATA-4: R240 to R250, PR 36. 16: R220 to R236, PR 34. DATA-3: R185 to R198, PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 1 to 15b: PRs 1 to 19

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. Road Layout counts of stand-in and full read differ
- No villa mirror, CASE, linkName or El @ Entry rule. The estimated FFL is within 30 mm on 75 of 205 IMC plots
- The footprint test cannot see a wrong hand. placer_check.py reads only the IMC dialect. No road surface older than the three road drawings is on this machine

## Bader's turn

1. El @ Entry rule: not coded, the schedules type it (747 of 747) from the plot entrance spots of the Infrastructure IFC package and fit today's road by typology 221 of 747: ask for that IFC package and the architect for the ramp slope (see 38, 43)
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest (199 of 205 IMC). 3. Villa mirror: one hand per row set by the open end, VL6 the other hand, confirmed in Revit by DATA-4 (106 IMC villas not built)
4. Setback: take the DLUS front (5.50 m villa and duplex, 7.00 m TH), the ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face
8. Units: 2,698 from the footprint blocks, the 120 on no plot are all Zone 4: ask for a baseplan with 01b. 9. Unit codes with no schedule: Typology_Rev9 by the location plan number
10. Widths: take the sales Land Width and Typology_Rev9 for the wide C10 and VL3. 11. IME: Rev 9 names none, send its schedule or boundary
12. FFL split: ask why M07 and M08 split and M11 never. 13. CASE: the second letter is the hand as built. 15. Yard depth: FFL minus 0.100 until answered
16. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart. 17. Plots 2301 and 2302: take VD4 from Rev 9
18. The 58 past ramps a median 97 mm above the surface: listed apart. 19. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 20. Angle to true north: keep the stop
21. Parallel windows: more than 8 GB free, round 16 had 2.6 to 8.5 GB. 22. TEST copies in Zone 02\ARC: delete when done
23. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md. 24. Curb and ROW distance from the R94 edge, the R122 front stays for width, links, levels. 25. The 13 plots the PDF check refuses: listed
26. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current. 27. Two DP4 labels in one face: ask for a split
28. Zone 4 IMG past job: the location plan names 01101-PSN-IMG-ZZZ-PIM-TU-000001, send it, the 1.4 model holds 012 and 022 only
29. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
30. 113 plan plots and 3 corner plots with no sales row: ask for the 01b rows and their Orientation
31. Rev 9 plot faces: the parcels prove 97 shape, 2 number and 5 code differences real, 1 number came from the PDF read: keep the baseplan faces, list them, ask which plot layout is current
32. The 1.4 key levels sit a constant datum above its FFL parameter (643 of 643): treat the parameter as the design level
33. The DT3 plot alone on a VL3 face has no mirror rule: list it apart until 3 is answered
35. round_z4\work, round_16\work and the VL4 and VL6 _TEST copies in data_book\work: keep until the next round, then delete
36. Rev 9 workbooks: take TBD for the DLUS and the Amana numbers, the accommodation workbook for the sales sheet, ask for one workbook with both
37. Corner front: the Orientation quadrant of the sales sheet is coded (338 of 338, today's rule breaks 19): confirm it is the side the ramp faces
38. The 1.4 job sits a median 194 mm over IMC, its ramps follow today's Top Surface-02 a steady 0.21 m higher (164 of 292 within 5 mm once the 0.21 m is off), no older road is on this machine: keep the IMC schedule as the level key, ask which road version the 1.4 job used
39. Footprint test: add it to every placer check, it caught the 6 Z4 links on the wrong edge, it cannot see a wrong hand
40. Clusters from the footprint blocks (R236): keep them for the mirror rule, wrong hands 36 down to 5 of 205
41. Rows 2290 to 2293 and 2296 to 2299: the key draws the middle DT1 the other hand from the drawn side, keep the rule and list them
42. Duplex plots in no cluster (DP4 12, DP2 3 in 012): need a rule for the hand of a duplex unit alone, left out until then
43. The IFC in Downloads, 1TB02012-012D03-UNA-00-XX-MOD-UT-00010.ifc, not opened: say whether it is the Infrastructure IFC package of the schedule notes, if so copy it under C:\02-Site Adaptation

## Next three steps

1. The ramp type and the El @ Entry rule once Bader answers 1, 2 and 38
2. The villa and lone duplex hand once Bader answers 3 and 42, then the rest of the IMC file
3. The El @ Entry levels from the Infrastructure IFC package once Bader answers 43, the footprint test in placer_check.py once 39
