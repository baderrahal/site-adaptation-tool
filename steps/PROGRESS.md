STATE 2026-10-24 21:40, Round Z4 done: a new Revit site file built from CAD only for DT3 of NBH 022 and compared with the 1.4 past model

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- Z4 (R200 to R218): window lock, snapshot, file info from bytes, site upgrade, read, new site file, image jobs, plan from the data book, compare in bands. The 1.4 key covers NBH 012 and 022, not 01A. 61 DT3 plots of 022 placed in 01101-PSN-Z4-TEST-NEW.rvt exactly as the CAD plan says. Summary: steps/investigations/round-z4.md
- DATA-2 (R169 to R184): data book wired, location plan numbers and Typology_Rev9. DATA (R160 to R168): plots.csv of Phase 1
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD, the placer with links and floors. Rounds 1 to 13: R1 to R101
- Gate: Python 852 passed and 1 skipped, .NET 405 passed. Real files are read in place, nothing real is in git

## Done

- Z4: R200 to R218. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 15b: PR 19. 1 to 15a: PRs 1 to 18

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. Road Layout counts of stand-in and full read differ
- No villa mirror, CASE, linkName or El @ Entry rule. The estimated FFL is a median 349 mm under the 1.4 key on DT3 of 022
- The placer stops on an angle to true north other than 0. placer_check.py cannot read a key of the 1.4 dialect

## Bader's turn

1. El @ Entry rule: not coded, by typology fits 221 of 747 within 5 mm, ask the architect what sets the ramp slope
2. Ramp type: Ramps 2% for C10, VL4, VL6 and 0.5% for the rest, and what makes a Flat Ramp
3. Villa mirror: one hand per row set by the open end, VL6 the other hand, and the villa alone between cluster units
4. Setback line: 5.00 m (2,530) or 3.00 m (37) is the building setback, the ramp stays 5.52 m. 5. Plot front level: the road surface snapped within 5 mm, Top Surface-02 for IMC, Top Surface-01 for IMD
6. Back corners parallel to the front, mean at El @ Entry. 7. Generic floor: the whole baseplan face
8. Round 16 test host run: yes, on a work file, once 2 is settled
9. Units: count from the footprint blocks (2,698), ask for the 3 villas of the sheet and the 6 of Rev 6. 10. Unit codes with no schedule: take Typology_Rev9 by the location plan number (1,819 of 1,831)
11. Widths over Rev 6, C10 58 and VL3 41: the architect confirms the unit fits. 12. IME: send its schedule or boundary
13. FFL split: ask why M07 and M08 split and M11 never. 14. CASE: the second letter is the hand as built
15. VL6 hand: confirm in Revit it is drawn the other hand from VL4. 16. Yard depth: FFL minus 0.100 until answered
17. Plots 2311 and 2312: follow the baseplan and footprint (VL4, VL4, OS), listed apart. 18. Plots 2301 and 2302: Rev 9 and the link say VD4, the schedule DP4, take VD4 from Rev 9
19. The 58 past ramps a median 97 mm above the surface: a past design choice, listed apart
20. RRE_ZZ_SP_Prototype-FFL: keep the schedule FFL. 21. Angle to true north: keep the stop, CAD gives grid north only
22. Parallel windows: more than 8 GB free, Z4 had 3.5 to 4.9 GB. 23. TEST copies in Zone 02\ARC: delete when done
24. The empty site-adaptation-tool-15a folder: close what holds it and delete it, steps/bader-data-2.md
25. Curb and ROW distance from the R94 edge (312 and 336 of 336), the R122 front stays for width, links, levels
26. The 13 plots the PDF check refuses: keep label plus shift (336 of 336 IMC agree), listed. 27. The two curbs differ by over 1 m on about 15 percent: keep the Roads curb, ask which is current
28. Two DP4 labels in a face with many labels: ask for that face to be split. 29. The Zone 4 IMG past job as a second answer key: send it if it exists, none is on this machine
30. Official numbers: take the location plan number per face (746 of 747), a fixed shift fails in 01A, 03, 022
31. 113 plan plots with no Rev 9 sales row (81 in Zone 4): ask whether the sales sheet is complete
32. Location plan versions: keep the Rev.09 PDF, read the DWG of 01_Output next
33. The 1.4 model named IMD holds NBH 012 (336) and 022 (294), no 01A: Z4 built 022, send a 01A past model if one exists
34. The 1.4 key levels sit a constant datum above its FFL parameter (643 of 643): treat the parameter as the design level
35. Estimated FFL a median 349 mm under the 1.4 key, its ramp fronts 210 mm off Top Surface-02: ask which road it used
36. The 1.4 key links 8 old prototypes (D2, D4 and others) not on this machine: send 20221206 - Links, until then floors only
37. Three pairs with the key ramp on the other edge (19.5 m): look at them in the two images, the R122 front stays. 38. The DT3 plot alone on a VL3 face has no mirror rule: list it apart until 3 is answered
39. Reader fix of Z4 (rotation from the transform when Revit gives none): keep it, the reader changes only for a round
40. Watcher: Do Not Load for other add-ins inside our own Revit only, never another process: keep this reading of rule 3
41. round_z4\work keeps the 2024 key copy (74 MB) and the new file (7 MB): keep until the next round, then delete

## Next three steps

1. The ramp type and the El @ Entry rule once Bader answers 1, 2 and 35
2. A 01A past model as the answer key, or the 1.4 prototypes to compare links, once Bader answers 33 and 36
3. Round 16, a test host run of plan_imc_cad.json on a work file
