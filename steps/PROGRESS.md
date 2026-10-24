STATE 2026-10-24 22:30, Round DATA-3 done (PR 32): Rev 9 workbooks, drawing and PDF read and compared, El @ Entry by terms, the units on no plot, the location plan DWG. Round Z4 done and merged (PR 30)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- DATA-3 (R185 to R198, R195 unused): TBD is the newer Rev 9 workbook, DLUS front 5.50 m villas and duplexes and 7.00 m TH, code table in the profile, Rev 9 faces same on 2,481 of 2,578 unit plots, El @ Entry by curb terms no better than 221 of 747, the 120 units on no plot all in Zone 4. Summary: steps/investigations/data-book.md
- Z4 (R200 to R218): window lock, a new site file from CAD for DT3 of NBH 022 compared with the 1.4 past model. Summary: steps/investigations/round-z4.md
- DATA-2 (R169 to R184): data book wired, location plan numbers and Typology_Rev9. DATA (R160 to R168): plots.csv of Phase 1
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD, the placer with links and floors. Rounds 1 to 13: R1 to R101
- Gate: Python 892 passed and 1 skipped, .NET 408 passed. Real files are read in place or from copies, nothing real is in git

## Done

- DATA-3: R185 to R198, PR 32. Z4: R200 to R218, PR 30. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 15b: PR 19. 1 to 15a: PRs 1 to 18

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. Road Layout counts of stand-in and full read differ
- No villa mirror, CASE, linkName or El @ Entry rule. The estimated FFL is a median 349 mm under the 1.4 key on DT3 of 022
- The placer stops on an angle to true north other than 0. placer_check.py cannot read a key of the 1.4 dialect
- The Rev 9 parcels and the VL6 hand were not read, the strict reading of rule 3 left the dialogs of our own windows unanswered

## Bader's turn

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

## Next three steps

1. The ramp type and the El @ Entry rule once Bader answers 1, 2 and 35
2. The Rev 9 parcels in Civil 3D and the VL6 hand in Revit once Bader answers 40
3. A 01A past model as the answer key, then Round 16, a test host run of plan_imc_cad.json on a work file
