# Round DATA-4, full record

The full top entry of steps/log.md as written at the end of the round.

## Round DATA-4, 2026-10-25, the Rev 9 parcels in Civil 3D, the VL6 hand in Revit and where the past El @ Entry came from

Goal:
- P read the Rev 9 parcels and redo the 115 differences, V check the VL6 hand in Revit, X find the source of the past El @ Entry. Summary: steps/investigations/data-book.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-data4 on data/data-book-4 from main at d67f2ef, rebased on ddac359 after Round 16 merged PRs 34 and 35, the read-of-a-copy of R227 and mine merged into one. Plan written before the first edit. 167 sources copied into data_book\work before opening (146 new), all 167 sources unchanged and all 167 copies equal after
- Windows, one at a time under the shared lock, 0 waits, no lock of another round met: Civil 3D twice (65 s, 45 s, lowest free 3.10 and 3.31 GB), Revit twice (1.0 and 0.55 min, 5.88 and 5.70 GB free at the lock). Dialogs by rule 3: Annotation Scale got No, the probe Load Once, in Revit RCRC Green and RCRC Green Audit Do Not Load and our three DLLs Load Once, only in our own processes. The registry was not read, the install ran with -SkipSigningReport
- P (R240, R243, R244): the first Civil 3D run found no site parcel, the 3,480 parcels sit in the bound block MP-Base. The probe now reads parcels inside blocks: 3,276 in Phase 1, 3,266 numbered, equal to the baseplan faces (median Hausdorff 0.000 m). Unit plots 2,481 same, 65 shape, 32 no face, numbers 2,554 same and 5 differ, codes 2,554 same and 6 differ. Of the 115: 97 shape, 2 number and 5 code differences are real, 1 number came from the PDF read, 21 DWG texts apart
- V (R245, R246): the read job writes rooms and doors and may read a test copy of handCheck.folders. VL6 gate at 0.631 of the front, right seen from the road, driver's room 0.941 right, VL4 gate at 0.325, left. VL6 is drawn the other hand from VL4. The four prototype files hash the same after
- X (R241, R242, R247 to R249): El @ Entry and FFL typed in all 747 schedule rows, Height a formula, no external link, the sheet note names the plot entrance spots of the Infrastructure IFC package. No IFC and no LandXML under the source root, one IFC in Downloads not opened. Only the three road drawings hold surfaces (saved 2023-02 and 2023-03). The snapped road level stays the best, 221, 193, 330 (IMC 96, IMD 125 within 5 mm), median 10 mm low, no datum improves it. The 1.4 key on 022: 3 within 5 mm, its ramps 0.21 m over Top Surface-02, 164 of 292 within 5 mm once that is off
- New: python/surface_versions.py, past_formulas.py, surface_entry.py, rev9_parcels.py, villa_hand.py, data4_check.py, CadReader --header-only, nested parcels in the probe, the never-purge watcher, HandSteps in the test host, handCheck in config/project.json. Gate: Python 912 to 925 passed and 1 skipped, .NET 418 to 421 passed

Checked:
- Second method (R250): parcels 20 of 20 at seeds 14 and 7 on parcel, plan number and class, the VL6 gate shares and verdict from the reader rows agree, the best surface version 20 of 20 at both seeds by brute force. The first parcel check took overlapping road and xx parcels, fixed to the smallest parcel holding the centroid

Remains:
- The El @ Entry rule waits for the Infrastructure IFC package (Your turn 43)

Known bugs:
- Windows PowerShell 5.1 fails in Install-TestHost.ps1 at the manifest, PowerShell 7 runs it. The probe exports no parcel a site holds in a drawing with none, by design

Next:
- The El @ Entry levels from the IFC package once answered, the villa and duplex hand rules of Round 16

Your turn:
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
