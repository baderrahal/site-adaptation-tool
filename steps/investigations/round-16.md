# Round 16, 2026-10-24, links against the CAD footprint, the corner front, two past jobs, a full IMC file from CAD

Counts and distances only. The files are in Documents\SiteAdaptation\round_16 outside the repo: snapshot, work, images, logs, prototype_reads, first_build and the JSON reports named below.

## Inputs and their state

- Snapshot of 45 files: plots.csv (the same file as the Z4 snapshot), location plan, the 18 LandXML surfaces, the footprint and baseplan tables, the two Rev 9 workbooks and the location plan PDF, and the CAD reader outputs of round 12 (footprint blocks, baseplan plots, road pieces) and the analysis of round 12b. Equal to the sources at copy time and to the manifest at the end.
- The IMC site model and the 13 files of Prototypes_2024 hashed before and after: 14 of 14 equal. Only copies in round_16\work were opened.
- 36 windows, one at a time under the lock: 13 prototype reads (0.5 to 0.8 min), 2 newSite, 18 linksAndFloors, 3 images (2.0 to 2.8 min with the links loaded, peak 1.9 GB). Free memory at the lock 2.64 to 8.47 GB. 2 waits of 5 min, one for memory (1.71 GB right after a Revit closed) and one for the lock of this round, no lock of another round met.

## Part E, the reader fix (R220)

- The rotation choice of the reader moved into PlacementRotation of Common: the location rotation when Revit gives one, else the turn of the transform X axis. 6 tests, 3 for each path.

## Part C, two past jobs on the same plots (R221 to R223)

- The 1.4 key and the IMC key share 336 plots of NBH 012, joined by the official number of the location plan.
- 1.4 less IMC: FFL median +194 mm (-532 to +571), within 30 mm 30 of 336. El @ Entry median +143 mm (-1,402 to +1,769), within 30 mm 21. Generic top as FFL. Ramp corners median +129 mm, worse than 30 mm on 335, while their plan places agree to a median 1 mm.
- By unit, FFL median: VD1 +365, DP1 +333, DT1 +320, TH1 +299, DT3 +296, TH3 +133, C10 +116, VL6 +112, VL4 +100, VD2 +94, VD4 +88, DP3 +80, DP2 +32, DP4 -27.
- So the two jobs do not agree. The 349 mm the Z4 plan sat under the 1.4 key on 022 is not a Zone 1 road effect alone, the 1.4 job is higher on IMC too, most of all on DT, TH and DP1 units.
- 1.4 ramp fronts of 022 (295 ramps, 590 corners) against all 18 surfaces: Top Surface-02 covers 415 at a median +128 mm, the Road Layout New Existing covers 590 at +532 mm, every other surface lies 0.79 m or more away or covers none. No surface of the data book is the road the 1.4 job levelled to.

## Part B, the ramp front of corner plots (R224 to R226)

- Corner plots, two or more road edges: IMC key 110 of 337, 1.4 key 233 of 633. Against the sales sheet: IMC 110 corner plots all with No. of streets 2 or 3, 1 plot with one road edge has 2 streets. 1.4 key 230 of 233 with 2 or 3 streets, 3 with no sales row, 7 plots with one road edge have 2 streets.
- Holds and breaks outside the cut plots (1 apart in each key):

| Rule | IMC holds, breaks | 1.4 holds, breaks, cannot say |
|---|---|---|
| nearestCurb (R122, today) | 109, 0 | 213, 19, 0 |
| orientation | 109, 0 | 229, 0, 3 |
| orientationOrCurb | 109, 0 | 230, 2, 0 |
| longest road edge | 20, 89 | 51, 181, 0 |
| shortest road edge | 79, 30 | 157, 75, 0 |
| neighbourFront | 46, 0, 63 cannot | 83, 17, 132 |

- Kept and coded: orientation, the edge whose outward normal falls in the Orientation quadrant of the Rev 9 sales sheet. It never picks another edge than the past ramp. The 3 plots with no sales row get no front and are listed.
- Today's rule breaks on 19 corner plots of the 1.4 key, 16 of them in NBH 022. Orientation picks the past edge on all 6 Z4 plots (2252, 2253, 2021, 2022, 2031, 2032), today's rule on none.

## Part A, links against the CAD footprint (R227 to R229)

- 13 prototypes read once each, house outline 135 to 310 m2.
- IMC key, 336 of 337 links measured (1 plot has no footprint block): residual within 10 mm 244, within 100 mm 23, within 1 m 68, worse 1, no mirror flag, long axis turn at most 0.025 deg. The test is tight (7 mm or less) for C10, DP1, DP3, DT3, TH1 and TH3, wide for DP2 (2.1 m), VD1, VL6 (0.95 m), VD4 (0.79 m), DP4 (0.50 m), DT1 (0.38 m), VD2 (0.31 m) and VL4 (0.27 m).
- Z4 links, 61 DT3: 55 pass, 6 fail, residual within 10 mm 53, within 100 mm 2, worse 6. The 6 are the 6 corner plots of Part B, overlap 0.77 against 0.574 and the centre 2.97 m off the key median. So the footprint finds the links on the wrong edge without the old prototypes.
- New IMC file, 205 links: 205 pass, within 10 mm 170, 100 mm 12, 1 m 23. The 5 links of the wrong hand pass too: where a unit footprint is near symmetric across its front a mirrored house overlaps it as well, so the test does not see a hand.

## Part D, a full IMC file from CAD (R230, R231, R234, R236)

- 01101-PSN-IMC-TEST-NEW.rvt from DefaultMetric.rte: 5 check points within 0 mm, angle 0, 4 floor types made, 10 parameters bound.
- plan_imc_new.json for NBH 012 from CAD only: 205 of 337 written (DT3 68, DT1 44, DP1 26, DP4 18, TH3 16, TH1 14, DP3 10, DP2 8, VD4 1). Left out: 106 villa plots with no mirror rule (VD2 29, VD4 28, VL6 19, C10 15, VL4 15), 15 duplex plots in no cluster (DP4 12, DP2 3), VD1 8, TH1-IM 2, and 2312, which has no plots.csv row. Front by orientation on 110 corner plots. All 672 front corners on Top Surface-02, equal to plots.csv within 5 mm.
- First build with the clusters of the round 12b analysis: 36 links of the wrong hand. The analysis joins two townhouse rows into one cluster of 8. Rebuilt with the footprint block clusters joined with the analysis (R236): 5 of the wrong hand, all in 2 short rows (2290 to 2293, 2296 to 2299) and one VD4 (2378). The first build is kept in first_build.
- Placement, one run per code into the same file, saved after each (second build): TH1, DP1, DP2, DP3, DP4, DT1, DT3, TH3, VD4, 0.5 to 0.8 min each, free memory 8.3 to 8.4 GB before each, no code left. 205 links and 614 floors (the VD4 plot has no fitted yard). Revit warnings: Highlighted floors overlap only.
- The new file against its plan: plan_compare 205 of 205 full match, and the CAD plan and the new file give the same counts against the key, so every gap is a plan fault.
- Against the IMC key, 205 plots, same prototype files:

| Field | Equal | Within 1 mm | Within 10 mm | Within 30 mm | Worse or differs |
|---|---|---|---|---|---|
| Link file | 205 | | | | 0 |
| Position in plan | 67 | 105 | 23 | 1 | 9 |
| Rotation | 200 | | | | 5 |
| Mirror | 200 | | | | 5 |
| Link Z against FFL | 20 | 18 | 15 | 22 | 130 |
| Generic outline | 2 | 139 | 59 | 0 | 5 |
| Generic top | 20 | 18 | 15 | 22 | 130 |
| Yard outline | 0 | 67 | 127 | 2 | 8 |
| Ramp outline | 23 | 126 | 51 | 0 | 5 |
| Ramp corners | 0 | 26 | 53 | 19 | 107 |
| Ramp type | 199 | | | | 6 |

- Parameters: Plot-Number and Prototype equal 205, Number-of-Steps equal 168, differ 37. Colour, option, foundation, HVAC, RVT-Name and CASE are not in a CAD plan.
- Reasons: estimate FFL 130, road level 106, other 10, mirror rule 5, cut plot 5. Worst 10: 2291, 2297, 2292, 2298 (mirror rule, the key draws these DT1 the other hand, 30 m), 2378 (VD4, cut plot and mirror), 2375, 2376, 2389, 2390 (cut plots, the key ramp on another edge, 2.0 m), 2607 (ramp corners 464 mm, estimate FFL and road level).
- placer_check per code against the IMC key: placed equal to expected on 9 of 9 codes, missing 0, extra 0, twice 0, file 205, rotation and mirror 200, Z equal to the plan FFL 205. It reports FAIL because its position is 3D and the link Z carries the estimated FFL.
- Images in round_16\images: the new file (205 links, 614 floors) and the IMC key (260 links of 13 types loaded, 1 type not found, 1,010 floors), the same area of 613 by 608 m.

## Second method (R235)

- z16_check.py, json, csv and math only, seeds 14 and 7, 20 items each: footprint 20 and 20, corner rule in the 1.4 key 20 and 20, in the IMC key 20 and 20, levels 20 and 20, new IMC file 20 and 20.
- First run: footprint 0 of 20 (the grid kept every detached ground floor where the house is the largest part) and corner 14 to 19 of 20 (the two lowest ramp corners are not always the front pair). Both were faults of the second method, fixed, the reports were not changed.

## Clean up

- Deleted from round_16\work: the 13 prototype copies (69,414,912 to 115,576,832 bytes each), the IMC key copy 52,495,116, landxml_book 54,732,159 and landxml_read 108,036,505. Total 1,292,409,380 bytes in 16 entries. The first new IMC file (8,376,320 bytes) was deleted before the rebuild.
- Kept: 01101-PSN-IMC-TEST-NEW.rvt 8,380,416 bytes, the two images, the site spec, the site report and the shared parameter file.
