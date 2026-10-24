# Round 16, full log entry

Moved here from steps/log.md in Round 17.

## Round 16, 2026-10-24, links against the CAD footprint, the corner front, two past jobs and a full IMC file from CAD

Goal:
- Prove links without the old prototypes, find the ramp front of corner plots, test the levels of two past jobs, build a full IMC file from CAD. Summary: steps/investigations/round-16.md, steps for Bader: steps/bader-16.md

Done:
- Session started in the repo, hooks live. Worktree site-adaptation-tool-16 on test/round-16 from main at 54dbcfb. Plan written before the first edit. Snapshot of 45 files and 14 originals equal before and after. 36 windows one at a time under the shared lock
- E (R220): the reader rotation choice moved into PlacementRotation of Common with tests for both paths
- C (R221 to R223): on the 336 plots both keys hold, the 1.4 FFL sits a median 194 mm over IMC (30 within 30 mm), El @ Entry 143 mm, ramp corners 129 mm in the same plan place. DT, TH and DP1 about 300 mm, DP2 and DP4 about 0. The 1.4 ramp fronts of 022 fit no data book surface, Top Surface-02 is nearest at 128 mm on 415 of 590. No level rule coded
- B (R224 to R226): orientation of the sales sheet picks the past ramp edge on 109 of 109 IMC and 229 of 229 1.4 corner plots, 3 with no sales row. Today's nearest curb breaks on 19 (16 in 022). Coded, on the 6 Z4 plots it picks the past edge 6 of 6
- A (R227 to R229): 13 prototypes read once each. The footprint band of the IMC key is 7 mm or less for C10, DP1, DP3, DT3, TH1, TH3. Z4: 55 of 61 DT3 links pass, the 6 that fail are the 6 corner plots. It does not see a wrong hand
- D (R230, R231, R234, R236): 01101-PSN-IMC-TEST-NEW.rvt, plan_imc_new.json 205 of 337 (106 villas and 15 lone duplex units left out). First build with the 12b clusters had 36 wrong hands, rebuilt with footprint block clusters: 5. Nine codes placed one run each, 0.5 to 0.8 min, no code left, 205 links and 614 floors
- D compare, equal to its plan on 205 of 205. Against the IMC key: file 205, plan position within 30 mm 196, hand 200, Generic and ramp outline within 10 mm 200, yard 194, ramp type 199, FFL within 30 mm 75. placer_check: placed 205, missing 0, FAIL by its 3D position. Images of both files in round_16\images
- New: footprint_check.py, corner_front.py, level_cross.py, place_codes.py, z16_check.py, PlacementRotation, loadLinks of the image job, plots per code in Run-TestHost.ps1, nbh=, the corner rule and footprint clusters in plan_from_book.py. Gate after the rebase on DATA-3: Python 892 to 912 passed and 1 skipped, .NET 408 to 418 passed
- Clean up: 16 work entries deleted, 1,292,409,380 bytes. Kept the new IMC file and the images

Checked:
- Second method (R235): footprint, corner rule in both keys, levels and the new IMC file each 20 of 20 at seeds 14 and 7, after two faults of the check itself were fixed

Remains:
- The villa and lone duplex hand, the El @ Entry rule, the footprint test inside placer_check.py

Known bugs:
- The footprint test passes a wrong hand on near symmetric units (5 of 5). placer_check.py counts the FFL in its position and reads only the IMC dialect

Next:
- The rules of 1, 3, 36 and 40 once answered, then the rest of the IMC file

Your turn:
1. El @ Entry rule: not coded, the curb terms fit 99 of 747 at best, by typology 221: ask the architect for the ramp slope and the civil engineer if the road surface holds the sidewalk (see 38)
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest (199 of 205 IMC). 3. Villa mirror: one hand per row set by the open end, VL6 the other hand (106 IMC villas not built)
4. Setback: take the DLUS front (5.50 m villa and duplex, 7.00 m TH), the ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face
8. Units: 2,698 from the footprint blocks, the 120 on no plot are all Zone 4: ask for a baseplan with 01b. 9. Unit codes with no schedule: Typology_Rev9 by the location plan number
10. Widths: take the sales Land Width and Typology_Rev9 for the wide C10 and VL3. 11. IME: Rev 9 names none, send its schedule or boundary
12. FFL split: ask why M07 and M08 split and M11 never. 13. CASE: the second letter is the hand as built
14. VL6 hand: look in Revit by hand, steps/bader-data-3.md. 15. Yard depth: FFL minus 0.100 until answered
16. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart. 17. Plots 2301 and 2302: take VD4 from Rev 9
18. The 58 past ramps a median 97 mm above the surface: listed apart. 19. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 20. Angle to true north: keep the stop
21. Parallel windows: more than 8 GB free, round 16 had 2.6 to 8.5 GB. 22. TEST copies in Zone 02\ARC: delete when done
23. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md. 24. Curb and ROW distance from the R94 edge, the R122 front stays for width, links, levels. 25. The 13 plots the PDF check refuses: listed
26. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current. 27. Two DP4 labels in one face: ask for a split
28. Zone 4 IMG past job: the location plan names 01101-PSN-IMG-ZZZ-PIM-TU-000001, send it, the 1.4 model holds 012 and 022 only
29. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
30. 113 plan plots and 3 corner plots with no sales row: ask for the 01b rows and their Orientation
31. Rev 9 plot faces: 115 unit plots differ: keep the baseplan faces, list them, ask which plot layout is current
32. The 1.4 key levels sit a constant datum above its FFL parameter (643 of 643): treat the parameter as the design level
33. The DT3 plot alone on a VL3 face has no mirror rule: list it apart until 3 is answered
34. Rule 3: Do Not Load inside our own Revit only, never another process, as round 16 did: keep it for both sessions so DATA-3 can read the parcels and VL6
35. round_z4\work and round_16\work keep the key copy and the new files: keep until the next round, then delete
36. Rev 9 workbooks: take TBD for the DLUS and the Amana numbers, the accommodation workbook for the sales sheet, ask for one workbook with both
37. Corner front: the Orientation quadrant of the sales sheet is coded (338 of 338, today's rule breaks 19): confirm it is the side the ramp faces
38. The 1.4 job sits a median 194 mm over IMC on the same plots (DT, TH, DP1 about 300, DP2, DP4 about 0) and fits no road of the data book: keep the IMC schedule as the level key, ask which job is current
39. Footprint test: add it to every placer check, it caught the 6 Z4 links on the wrong edge, it cannot see a wrong hand
40. Clusters from the footprint blocks (R236): keep them for the mirror rule, wrong hands 36 down to 5 of 205
41. Rows 2290 to 2293 and 2296 to 2299: the key draws the middle DT1 the other hand from the drawn side, keep the rule and list them
42. Duplex plots in no cluster (DP4 12, DP2 3 in 012): need a rule for the hand of a duplex unit alone, left out until then
