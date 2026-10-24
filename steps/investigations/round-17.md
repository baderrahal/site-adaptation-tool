# Round 17, 2026-10-25, the past site models of all four sub-phases and the majority rule

Counts and distances only. The files are in Documents\SiteAdaptation\round_17 outside the repo: snapshot, work, images, logs, part_0, part_1, part_g, part_2, part_3, part_k, past_all.csv and the plans named below.

## Inputs and their state

- Snapshot of 74 files (402 MB): plots.csv, units.csv, wiring.json, locationPlan.json, retest.json, analysis.json, the location plan tables, the 18 LandXML surfaces, the baseplan and footprint tables, the rev9 tables, the round 12 CAD outputs (baseplan plots, footprint blocks, road pieces, analysis), the two Rev 9 workbooks with the two PDFs and the IMC and IMD schedules. plots.csv has 119 columns, 4 more than round 16, written by the area key of round DATA-5 before the snapshot. Only the snapshot was read.
- 32 originals hashed before and after: the 16 Native Files models, the Parsons IMC key, the 13 files of Prototypes_2024 and the two schedules. 5 key inputs hashed too (the two IMC key JSONs, the two Z4 key JSONs, the template). All 16 Native models and the Parsons IMC key were copied into round_17\work and every copy equals its original. BasicFileInfo and the dates were read from the copies.
- 58 windows, one at a time under the shared lock, 0 waits, no lock of another session met, 4.78 to 9.3 GB free at the lock: 3 upgrades, 4 reads, 6 newSite, 39 linksAndFloors, 6 images. 0.35 to 3.45 min each but the 3 image tries of the NBH 03 file (12.0, 11.7, 11.7 min). Refused before any work: one newSite whose file name lacked -TEST-NEW, one read of a new file (a read takes only a _TEST- copy, the read-back of the last placement run stands for it), and two newSite whose shared parameter file was there already. Each refusal left its file unchanged by hash. The watcher answered 5 dialogs per start inside our own Revit only: Do Not Load for RCRC Green and RCRC Green Audit, Load Once for the three DLLs of this tool. The registry was not read or written.
- R260 is used by round DATA-5 outside its R240 to R259, so this round used R261 to R282 (conflict listed).

## Part Q, fixed question IDs

- Each open question kept its DATA-4 number as its ID, Q001 to Q043. Q014 (VL6 hand) and Q034 (rule 3 of the watcher) were closed in DATA-4, Q043 (the IFC in Downloads) in DATA-5, and are in steps/questions-closed.md with their answers. DATA-5 added 44 to 49, which keep those numbers as Q044 to Q049, so this round's new questions start at Q050.
- steps/questions-closed.md maps the old numbers of Z4, DATA-3 and round 16 to the IDs. Z4 and DATA-3 numbered differently from today (Z4 41 is Q035, Z4 33 is Q028), and bader-16.md named 46 for what is Q042. The three steps files now name IDs. CLAUDE.md holds the ID rule and the majority rule.

## Part K, placer_check split (R261 to R263)

| Check | Z4 file against the 1.4 key | Round 16 IMC file against the IMC key |
|---|---|---|
| Plots placed | 61 of 61 | 205 of 205 |
| Plan position | FAIL, 61 of another file than the key, worse | FAIL, equal 67, 1 mm 105, 10 mm 23, 30 mm 1, worse 9 |
| Rotation, mirror | 0 and 6 equal of 61 | 200 and 200 of 205 |
| Level against the key FFL | FAIL, worse 61 (datum 164.897 m taken off) | FAIL, equal 20, 1 mm 18, 10 mm 15, 30 mm 22, worse 130 |
| Level against the plan FFL | equal 61 | equal 205 |
| Footprint, edge only | FAIL, pass 55, fail 6 | PASS, 205 |

- The counts equal round 16 (position 67, 105, 23, 1, 9, level 20, 18, 15, 22, 130, footprint 55 and 6). So the old FAIL of round 16 was the level alone: the plan position fails on 9 cut plots and the 5 wrong hands, the level on the estimated FFL.

## Part 0, every past model

- BasicFileInfo of 16: all Format 2021. TU build 20200220, EL and PP 20201116, SE 20220517. TU, EL and PP workshared central, SE not workshared. TU central paths \\MAINSRV\...\Site Adaptation\Phase 1 to Phase 4, EL and PP the Engicon folders of Phase 1.1 to 1.4.
- Upgrades, detached with worksets discarded, saved as new files of Format 2024, not workshared: 1.1 2.38 min, 1.2 1.87 min, 1.3 2.0 min. The 2021 copies unchanged by hash.
- Reads: 1.1 407 links, 814 floors (FFL 407, Ramps 365, Ramps missed from AutoCAD 42). 1.2 696 links, 1,388 floors (FFL 694, Ramps 667, Ramps 2 19, Ramps 3 8). 1.3 832 links, 1,664 floors (FFL 832, Ramps 832). The 1.4 read of Z4 reused: 643 links, 1,286 floors. The one error per read is the spot host line, no reader fix needed.
- 1.1 writes a level (EL@-Entry +164.98) in 153 link Names and a wrong plot in others: by its Name 147 links mapped. R264 maps the rest by the house frame of their file: 252 mapped, 8 not. The frame finds the own face on 692 of 696 (1.2), 643 of 643 (1.4) and 150 of 150 (1.1) links mapped by Name. The digits of the wrong Names agree with the frame on 1 of 88, so they are never trusted.
- Answer key plans by --answer-key --names: 1.1 394 of 399 plots, 1.2 585 of 626, 1.3 783 of 812, 1.4 633 (Z4).
- rvt_info.py failed on the Parsons IMD model: its TransmissionData keeps an older tail after the closing tag. Fixed with a test (R265).

### Coverage (R267)

| Model | NBH (zone): key plots of location plan unit plots |
|---|---|
| 1.1 IMA-TU-000001 | 02 (3): 309 of 324, 01A (4): 84 of 297 |
| 1.2 IMB-TU-000002 | 03 (3): 431 of 490, 01A (4): 150 of 297 |
| 1.3 IMC-TU-000003 | 021 (1): 399 of 432, 013 (2): 382 of 410 |
| 1.4 IMD-TU-000004 | 012 (2): 336 of 337, 022 (1): 294 of 301 |
| Parsons IMC key | 012 (2): 337 of 337 |

- The four models together hold 2,386 of the 2,591 unit plots of Phase 1 on the location plan, every neighbourhood has key plots. Not held: 03 59, 01A 63, 021 33, 013 28, 02 15, 022 7. Only the 1.4 key and the Parsons key share plots (336 of 012). 01A, Zone 4, is in 1.1 and 1.2 (234 of 297).
- The Native models use the old client units, not Rev 9: in NBH 03 the 1.2 key has DP4 119, DP2 68, VL4 62, VL3 50, VL2 50, C10 36, TH1 24, TH3 22 where Rev 9 has DT3 77, DT1 56, VD4 43 and so on.

### Prototype files and dates (R266, R268)

- The four TU models link the same 8 files of 1. Villas Prototypes\20221206 - Links: C1, D2, D4, V2, V3 (CP), V4, T1, T3. The EL, PP and SE models link their own discipline prototypes, some from Riyadh and personal folders.
- Storage times: the TU models were written 2022-11-10 to 2022-11-20 and last saved 2022-12-11. EL and PP 2022-11-01 to 2022-11-23, SE 2022-11-13 to 2022-12-31. File system dates are the copy date of this machine (2026-10-24) and say nothing.
- The Parsons IMC key is Format 2024 and was saved on this machine 2026-10-22, so its bytes no longer date it. Its sibling, the Parsons IMD TU-000001, was written 2024-03-10 and last saved 2024-03-26 under REV 8.2\WORKSPACE\6. Zone 2 and links prototypes of 4. Villa Prototypes\20240209. The 144 PDFs of 01_Output are Parsons IMC, IMD and general sheets, created 2024-02-04 to 2024-03-26. No transmittal is on this machine.
- So the Parsons job (March 2024) is newer than the Native job (December 2022).

## Part 1, past_all.csv (R269)

- 2,732 rows: 1.1 394, 1.2 585, 1.3 783, 1.4 633, IMC 337. El @ Entry from the schedule (IMC 337), the ELl@Entry parameter of the FFL floor (1.3 780, 1.4 632) or the back of the ramp (1.1, 1.2 and 4 more). Levels less the datum, 164.897 m on all four Native models, 0 on IMC. Footprint residual on the IMC key, 336 measured (within 10 mm 244, 100 mm 23, 1 m 68, worse 1).
- The ramp front is the long edge nearer the plot face boundary. The first take, the two lowest corners, is wrong where a ramp falls to the house, and it was replaced before Part 2 counted.

## Part G, El @ Entry gaps over 0.5 m (R270)

- Only the 1.4 key and the IMC key share plots: 336, 6 over 0.5 m. No wrong join: on all 6 both Generic floors stand on the face of the plot number and overlap fully.
- 5 TH3 (2461, 2470, 2473, 2481, 2557): the 1.4 ELl@Entry parameter is 0.41 to 1.59 m off its own ramp back, the ramp backs of the two keys sit 0.13 to 0.19 m apart like their neighbours (0.06 to 0.27 m): a typed parameter fault of the 1.4 model. Part 2 reads El @ Entry off the ramp there.
- 2609 VL4: parameter and ramp agree, the 1.4 job sits 0.65 m lower and its row too (2606 to 2610 at -0.12 to -0.42 m): a real level difference.
- No join fault, so nothing was fixed before Part 2.

## Part 2, the majority rule (R271 to R277)

Native is the four 2022 models (2,389 to 2,395 plots), Parsons the IMC key (337). Strong is a share of 0.90. A level holds within 5 mm.

| Decision | Group | Coded rule | Share | Strong | Overrides |
|---|---|---|---|---|---|
| Ramp slope | Parsons | by Rev 9 code: 2% for C10, VL4, VL6, else 0.5% | 0.970 | strong | 10 (the Flat Ramps) |
| Ramp slope | Native | 2% on every unit | 0.977 | strong | 54 |
| Hand, villa | Parsons | block mirror flag with the open end, per file | 0.913 | strong | 10 |
| Hand, villa | Native | block mirror flag with the open end, per old file | 0.945 | strong | 63 |
| Hand, cluster | Parsons | cluster side, R123 | 0.981 | strong | 4 (2291, 2292, 2297, 2298) |
| Hand, cluster | Native | cluster side | 0.983 | strong | 21 |
| Hand, alone | Parsons | block mirror flag with the open end | 0.938 (16 plots) | strong | 1 |
| Hand, alone | Native | block mirror flag | 0.714 (7 plots) | weak | 2 |
| Front edge, corner plots | all | Orientation quadrant (R226) | 0.967 (909 of 940) | strong | 24 breaks, 7 cannot, listed in corner_front.json |
| El @ Entry | Parsons | snapped road plus 0.5 or 2% times the ramp depth | 0.285 | weak | 241 |
| El @ Entry | Native | Total Top of Roads Subgrade (-0.70) less the datum of each key | 0.257 | weak | 1,768 |
| Ramp front | Parsons | snapped road | 0.726 | weak | 92 |
| Ramp front | Native | Total Top of Roads Subgrade (-0.70) plus 0.698 to 0.700 m per key | 0.634 | weak | 875 |
| Ramp front fall | Parsons, Native | the road at each corner | 0.711, 0.623 | weak | 97, 900, listed, not applied |
| FFL less El @ Entry | Parsons | one FFL per cluster at the highest El @ Entry plus the base of that unit | 0.905 | strong | 32 |
| FFL less El @ Entry | Native | median per unit | 0.353 | weak | 1,539 |
| Steps | Parsons | the count bands on the height (Rev 6 count rule) | 1.000 | strong | 0 |
| Generic floor | Parsons, Native | the whole face | 0.955, 0.899 | strong, weak | not in the list |
| Yard | Parsons, Native | 0.100 below FFL, none | 0.997, 1.000 | strong | not in the list |
| Number-of-Steps parameter | Parsons | the steps | 1.000 | strong | 0 |
| Foundation type | Parsons | code with steps | 0.685 | weak | not in the list |
| HVAC, façade colour and option | Parsons | code, XX, XX | 1.000 | strong | 0 |
| CASE | Parsons, Native | code with hand | 0.424, 0.314 | weak | not in the list |

- a. Ramp: flat ramps are few (IMC 10 of 337, their geometry slopes like the others, only the type says flat), so sloped is the rule and the 10 are overrides. The IMC type follows the code, not the geometry: its measured slope band holds 0.831.
- b. Hand: the CAD block mirror flag is the common reference of both jobs. On it each old file of the Native models is drawn the same hand as the 2024 file of its codes (D2 DP2, D4 DP4, T1 TH1 and DT1, T3 TH3 and DT3, C1 C10, V4 VL4) or the opposite (V3 against VL6, V2 against VD2 and VD4). It is in hands.oldFiles of the rules profile (R282). The middle DT1 of 2290 to 2293 and 2296 to 2299 break the cluster side on the IMC key, 4 overrides. The open end, the row parity, the side road and Orientation alone stay at 0.52 to 0.75 for villas.
- c. Front edge: Orientation picks the past edge on 128 of 146 (1.1), 169 of 177 (1.2), 274 of 276 (1.3), 229 of 233 (1.4), 109 of 109 (IMC) corner plots. 12 breaks are one row of 1.1, 3005 to 3016.
- d. Levels: the Parsons key follows Rev 6. FFL less El @ Entry is the base of the unit (0.10, or 0.20 for DP2, DP3, DP4, VD2, VD4) plus 0.17 m a riser on 0.985 of plots given its steps, and steps follow the count rule on 1.000. The Native job is no step rule: its heights run 0.06 to 0.41 m, its ramps slope 2% on every unit and its ramp fronts sit 0.698 to 0.700 m above the surface Total Top of Roads Subgrade (-0.70) in all four models, so it levelled to the road top that surface was cut from, an older road than today's Top Surface-02. The Parsons ramp fronts sit 0.56 m over that subgrade, on today's road. Rev 01 values are not in the repo, only the Rev 01 date of the foundations. Two level profiles are coded with no default: parsons2024Rev6 and native2022 (Q050).
- e. Generic, yard and parameters as in the table. CASE follows no tested logic on either generation.
- The override list config/overrides/roshn-makkah.json holds 5,709 overrides, one per line, a level as its gap to the rule in metres: Parsons 487, Native 5,222.

## Part 3, the IMC rebuild (R278 to R280)

- plan_imc_r17b.json for NBH 012 by the parsons2024Rev6 profile, the hand tables and the overrides of the Parsons IMC key: 336 of 337 plots (2312 has no plots.csv row), villas 115, lone duplex units 15, VD1 8 and TH1-IM 2 now in. Applied: El @ Entry 240, ramp front 91, height 32, mirror 14, ramp type 10. Hands by the block and open end 120, cluster side 202, override 14.
- 01101-PSN-IMC-R17B-TEST-NEW.rvt from DefaultMetric.rte, 5 check points within 0 mm, 13 runs one per code, saved after each, 0.5 to 0.8 min each: 336 links and 999 floors (Generic 336, yard 327, ramp 336). A first file of the same plan with the ramp fronts of the lowest corners was placed before the fix and is not kept.
- The new file equals plan_imc_r17b.json on every count of the compare.

| Field against the IMC key | Round 16 (205 plots) | Round 17 (336 plots) |
|---|---|---|
| Plots placed | 205 | 336 |
| Link file | 205 | 334, 2 VD4 plots the key links to DP4 |
| Plan position within 1 mm | 172 | 285 |
| Plan position worse than 30 mm | 9 | 10: 9 cut plots (2311, 2375 to 2377, 2388 to 2390, 2543, 2546) and 2302, which the key links to DP4 |
| Rotation, mirror equal | 200, 200 | 335, 335, only 2302 differs |
| Link Z against the key FFL within 10 mm | 53 | 336, worst 5 mm |
| Link Z worse than 30 mm | 130 | 0 |
| Generic outline within 10 mm | 200 | 319 |
| Ramp type equal | 199 | 336 |
| Ramp corners worse than 30 mm | 107 | 45 |
| Ramp front corners worse than 30 mm | 101 | 40 |
| Steps parameter equal | 168 | 336 |

- Split placer_check of the new file: position FAIL (285 of 336 within 1 mm, 2 of another file, 9 cut plots worse), level FAIL by its 1 mm limit (every plot within 5 mm, the level hold of the rule), footprint 334 pass, 2 with no key group (the DP4 links of 2301 and 2302).

## Part 3, NBH 03 of sub-phase 1.2 against its Native model

- NBH 03 is the largest neighbourhood with no Parsons key (490 unit plots, the 1.2 model holds 431). plan_03_r17.json by the native2022 profile, the hand tables and the El @ Entry and ramp front overrides of the 1.2 model: 404 of 490 plots. Not written: 75 whose front corners lie off the surface Total Top of Roads Subgrade (-0.70), 10 with no unit, 1 DP3 alone with no hand table. The mirror and height overrides of the 1.2 model were not applied, they belong to its old files and old units.
- 01101-PSN-IMB-R17-TEST-NEW.rvt, 13 runs one per code: 404 links and 808 floors (Generic 404, ramp 404, no yard by the profile).
- Floors and hands only, 370 plots both hold: Generic outline within 10 mm 369, worse 1. Ramp outline within 10 mm 349, within 30 mm 19, worse 2. The floor type names differ by dialect (FFL against Generic 150mm, Ramps against Ramps 2%) on every plot.
- Hands as built by hands.oldFiles (R282): equal 301, differs 6, another unit 63 (DP2 and DP4 plots the 2022 job built with villa files, no common reference). The raw mirror flag is equal on 167 of 370, which is why the old file note is needed.
- Levels were not compared. For the record the Generic top is within 30 mm of the key on 127 of 370, worse on 243.

## Images (round_17\images, not in git)

- testhost_I18_plan, the new IMC file, 336 links and 999 floors in the view, image in 91.5 s. testhost_IMK_plan, the Parsons IMC key with its Prototypes_2024 links, same area of 613 by 608 m. testhost_U12_plan, the 1.2 Native model, floors only, its old prototype files are not on this machine, NBH 03 area of 735 by 733 m.
- The NBH 03 file has no image: the image step ran over its 10 minute limit three times, twice with the links set to load and once without, though the file opens in 78 s with its 404 links loaded (Q061).

## Clean up

- Deleted from round_17\work: the 16 Native copies in native (777,273,344 bytes), the three 2021 TU copies (28,856,320, 77,832,192, 74,371,072), the first IMC file built before the ramp front fix 01101-PSN-IMC-R17-TEST-NEW.rvt (9,887,744) and the Parsons IMC key copy of the image (52,495,116). Total 1,020,715,788 bytes in 6 entries. The LandXML reader cache round_17\scratch, 91,417,420 bytes.
- Kept: the 2024 key copies of 1.1, 1.2 and 1.3 (the 1.4 one is in round_z4\work), 01101-PSN-IMC-R17B-TEST-NEW.rvt, 01101-PSN-IMB-R17-TEST-NEW.rvt, the site specs and reports, the images.
- After the round: 32 of 32 originals, 5 of 5 key inputs and 74 of 74 snapshot files equal their hashes.

## Second method (R281)

- r17_check.py, json, csv and math only, seeds 14 and 7, 20 plots each: past_all.csv against the raw reader output 20 and 20, the join 25 and 26 (20 random and the 6 gap plots), every coded rule against its counts and the override list 48 and 48, the split check against round 16 2 and 2, the rebuilt file bands equal to the split check 20 and 20, mirror 20 and 20.
- The first run had the table at 10 and 7 of 20: two links can share one origin in a key, and the plan outline is rounded to the mm, so the raw area differs by up to 0.012 m2. Both were faults of the second method, fixed. The reports were not changed.
