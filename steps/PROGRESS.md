STATE 2026-10-25, Round DATA-5 done and merged (PR 38): the DATA-4 audit, the missing xrefs, the reader counts, the area key per plot, the IFC in Downloads and the coverage table. Round DATA-4 done and merged (PR 36)

# Progress

Live state of the tool. At most 60 lines. The round detail is in steps/log.md, steps/history/ and steps/investigations/.

## State

- DATA-5 (R251 to R260): 97 Rev 9 plots differ for real (not 105) and 18 only by DWG text, the 4 xrefs are on no folder, the probe count of the road drawings is right (a DXF count agrees by type), every plot but 22 carries Zone and NBH from the TBD DLUS, the IFC in Downloads is another project, the coverage table names what is not read. Summary: steps/investigations/data-book.md
- DATA-4 (R240 to R250): the Rev 9 parcels equal the baseplan faces, VL6 is drawn the other hand from VL4, the past schedules type El @ Entry from an Infrastructure IFC package not on this machine
- 16 (R220 to R236): the footprint test, the corner front by the sales Orientation, the 1.4 job 194 mm over IMC, 205 IMC plots from CAD. Summary: steps/investigations/round-16.md
- DATA-3, Z4, DATA-2, DATA (R160 to R218): the data book of Phase 1, a new site file for DT3 of NBH 022
- 14c, C3D, 14, 15a, 15b (R120 to R159): links and floors from CAD and the placer. Rounds 1 to 13: R1 to R101
- Gate: Python 950 passed and 1 skipped, .NET 421 passed. Real files are read from copies, nothing real is in git

## Done

- DATA-5: R251 to R260, PR 38. DATA-4: R240 to R250, PR 36. 16: R220 to R236, PR 34. DATA-3: PR 32. Z4: PR 30. DATA-2: PR 28. DATA: PR 26. 14c: PR 24, 25. C3D: PR 22, 23. 14: PR 20, 21. 1 to 15b: PRs 1 to 19

## Open bugs

- Civil 3D ends with 0xC0000005 or 0xC0000409 after C3DQUIT. CadReader reads model space only and no Civil 3D object. The probe writes no attribute value
- Not read: 154 alignments of Road Surface, the parcels of CEC, R06, R06_0000, R06_0001 and Road Layout (the probe does not reach them)
- No villa mirror, CASE, linkName or El @ Entry rule. The estimated FFL is within 30 mm on 75 of 205 IMC plots
- The footprint test cannot see a wrong hand. placer_check.py reads only the IMC dialect

## Bader's turn

1. El @ Entry rule: the IFC in Downloads is another project, ask the client for the Infrastructure IFC package and the architect for the ramp slope
2. Ramp type: 2% for C10, VL4, VL6, 0.5% for the rest. 3. Villa mirror: one hand per row, VL6 the other. 4. Setback: DLUS front. 5. Front level: road snapped. 6. Back corners parallel. 7. Generic floor: whole face
8. Units: ask for a baseplan with 01b. 9. Typology_Rev9 for codes with no schedule. 10. Sales Land Width. 11. IME: nothing names it, ask which neighbourhood each IM letter is (see 44)
12. FFL split: ask why. 13. CASE second letter as built. 15. Yard FFL minus 0.100. 16. Plots 2311, 2312 by baseplan. 17. Plots 2301, 2302 VD4. 18. 58 past ramps listed. 19. Schedule FFL. 20. Keep the north stop
21. Parallel windows over 8 GB free. 22. TEST copies in Zone 02\ARC: delete when done. 23. Delete the empty 15a folder. 24. Curb and ROW from the R94 edge. 25. 13 PDF refusals listed
26. Two curbs from one 2021 drawing, Roads saved 2023-03-06, Base Plans 2023-01-31, no revision text, they differ at junction corners and curb breaks: keep the Roads curb, ask which is current
27. Two DP4 labels: ask for a split. 28. Send the Zone 4 IMG past job. 29. Location plan number per face. 30. Ask for the 01b sales rows. 31. Rev 9 faces: 97 differ, keep the baseplan, ask which is current
32. 1.4 FFL parameter as the design level. 33. The lone DT3 on a VL3 face listed. 35. round_z4\work and round_16\work: delete next round. 36. TBD for DLUS, accommodation for sales. 37. Confirm the Orientation side
38. Keep the IMC schedule as the level key, ask the road version of the 1.4 job. 39. Footprint test in every placer check. 40. Footprint clusters. 41. Rows 2290 to 2299 listed. 42. Lone duplex hand rule
44. config/area_codes.json: approve as names only, the area stays the Neighbourhood_ID. 45. R06_0000 and R06_0001 were saved here on 2026-10-22: confirm their source
46. A probe pass for the unread alignments and parcels. 47. Delete the IFC copy next round. 48. Delete the old Cutsheet lock files. 49. Leave attribute values out of the probe

## Next three steps

1. A probe pass that walks every block definition for alignments and parcels, once 46 is answered
2. The ramp type and the El @ Entry rule once 1, 2 and 38 are answered
3. The villa and lone duplex hand once 3 and 42 are answered, then the rest of the IMC file
