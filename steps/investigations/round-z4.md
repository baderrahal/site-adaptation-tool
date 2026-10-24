# Round Z4, 2026-10-24, a new site file for one neighbourhood from CAD only, against the past site model

Counts and distances only. The files are in Documents\SiteAdaptation\round_z4 outside the repo: snapshot, work, images, logs and the JSON outputs named below.

## Inputs and their state

- Snapshot of the data book taken at the start: 209 files (plots.csv, locationPlan, location_plan, landxml, the three C3DProbe folders, the baseplan tables, the two Rev 9 workbooks and the location plan PDF), each equal to its source at copy time and equal to the manifest at the end. DATA-3 ran beside this round in its own worktree.
- The four models of Native Files\1.4 hashed before and after: equal. Only copies in round_z4\work were opened.
- BasicFileInfo from the bytes (R203): TU Format 2021, build 20200220, workshared Central. PP 2021 Central, SE 2021 not workshared, EL 2021 Central. Central and last save paths name Site Adaptation\Phase 4 (TU) and the Engicon 4. Phase 1.4 folders (PP, EL). TU links 8 Revit files and 5 CAD files, PP and EL 9 Revit links each with the TU model among them, SE 8 structural links and the TU model.

## Part 0, the answer key

- Site upgrade (R204): one window, 2.0 min, the TU copy opened detached with its worksets discarded and saved as 01101-PSN-IMD-ZZZ-PIM-TU-000004_TEST-K24.rvt, Format 2024, not workshared, 74,301,440 bytes. The 2021 copy unchanged by hash.
- Reader (R205): try 1 stopped in the read step, Revit could not give the rotation of one placed instance. The reader now takes the rotation of the transform there. Try 2: 643 links (0 loaded, their files are not on this machine), 1,286 floors, 4,795 spots, 0 errors.
- The key is another dialect than the IMC model: links hold no RRE_ZZ_SP parameter, only CASE, the instance Name is <unit>-<number>. Floors are 643 of type FFL (the whole face) and 643 of type Ramps, Plot <unit>-<number>, no yard floor. Its numbers are its own: number less location plan number runs from about -890 to -600 by row, never a fixed shift.
- plan_from_past.py --answer-key --names (R206): 634 plots by the face of their FFL floor, 633 written, links mapped 634, 8 floors on a face with no single plan label, 1 number given twice. Its levels sit a constant datum above the design levels the FFL parameter holds, proved on 643 of 643 floors, and the compare takes that datum off.
- Coverage (R206): NBH 012 (Zone 2, the IMC plots) 336 plots, NBH 022 (Zone 1) 294 of its 301 unit plots, 3 with no NBH. No plot of 01A (Zone 4) and none of 013 (IMD of the schedule). The file name IMD does not match what it holds.
- Choice: 022, the largest NBH the CAD fits were not made on (the plotFrame offsets and floor outlines were fitted on 012).

## Part 1, the prototype

- Typology_Rev9 in 022 held by the key: DT3 62, DT1 32, DP4 30, VD2 27, VD4 24, DP1 22, C10 21, VL4 19, TH3 14, TH1 12, VL6 11, DP3 10, DP2 8, VD1 2, TH1-IM 2.
- Chosen: DT3, RRE-WP-NSP-KEO-ARC-DT3-ZZ-MOD-3300.rvt of Prototypes_2024, placed in round 15a, 62 plots, all 62 in the key. The key links those plots to the old D2 (31) and D4 (30) prototype files.

## Part 2, the plan from CAD (R208 to R210)

- plan_z4_cad.json: 61 of 62 written. The one left out sits alone on a VL3 face, in no cluster, so no mirror rule gives its hand.
- Clusters from round12b analysis.json: 31 pairs hold 61 of the 62 plots.
- Road surface of the 124 front corners: Top Surface-02 124. Plots with no road level: none. Front levels against roadLevelLeft and roadLevelRight of plots.csv: 124 of 124 within 5 mm.
- Floors: Generic 61 (the whole face), yard 61 (fitted outline), ramp 61, all Ramps 0.5% (DT3 is not a steep unit).

## Part 3, the new site file (R211 to R213)

- site_z4.json: origin at the centre of the 62 plots rounded down to 100 m, elevation 0, angle 0 (no drawing gives a true north, the page fit turn is 0.005 deg), 3 control points of the location plan and 2 plot corners as check points.
- newSite try 1 stopped in the coordinates step before any save: the check found Revit 4,864 km off, GetTotalTransform takes shared to internal and the step applied it the other way. Try 2, 0.4 min: 5 check points within 0 mm, survey point within 0 mm, Level 1 and Level 2 kept from the template, 4 floor types made, Generic 150mm kept, 10 text parameters bound, saved as 01101-PSN-Z4-TEST-NEW.rvt, Format 2024. The template unchanged by hash.
- linksAndFloors S40, 0.7 min: dry run then one real run, links placed 61, floors placed 183 (Generic 61, yard 61, ramp 61), skipped 0, parameters written 183, then saved and read back. 237 Revit warnings, all Highlighted floors overlap, and 61 notes that the plan has no linkName.
- No fallback to a stripped key copy was needed.

## Part 4, the compare (R215, R216)

- The new file against the CAD plan: plan_compare.py 61 of 61 full match, z4_compare equal on every field of all 61 plots and all three floor kinds. So every gap to the key below is a plan fault or a key difference, none a Revit fault.
- The CAD plan against the key and the new file against the key give the same counts, 61 plots compared, 1 absent:

| Field | Equal | Within 1 mm | Within 10 mm | Within 30 mm | Worse or differs |
|---|---|---|---|---|---|
| Link file | 0 | | | | 61 |
| Position | 0 | 0 | 0 | 0 | 61 (median 20.0 m, max 31.9 m) |
| Rotation | 0 | | | | 61 (180 deg on 61) |
| Mirror | 6 | | | | 55 |
| Link Z against FFL | 0 | 0 | 0 | 0 | 61 (median 349 mm low, 90 to 370) |
| Generic outline | 0 | 26 | 33 | 2 | 0 |
| Generic top | 0 | 0 | 0 | 0 | 61 |
| Yard | | | | | 61 only in the new file, the key has no yard |
| Ramp outline | 2 | 24 | 27 | 2 | 6 (about 19.5 m) |
| Ramp corners | 0 | 0 | 0 | 1 | 60 (median 214 mm) |
| Ramp front corners | 0 | 0 | 0 | 2 | 59 (median 210 mm) |
| Ramp type | 0 | | | | 61 (Ramps against Ramps 0.5%) |

- Parameters: Plot-Number equal 61. Prototype differs 61 (the key holds the client type, the plan Typology_Rev9). The other 7 are missing in the key, which holds only CASE, and CASE, colour, option, foundation, HVAC and RVT-Name are not in the CAD plan.
- Reasons over the 61 plots: estimate FFL 61, road level 59, the key links another prototype file 61, rotation off 61, ramp on another edge 6, mirror rule 0 (the hand of another file is not compared), cut plot 0.
- Worst 10: 2252, 2253, 2021, 2022, 2031, 2032 (ramp of the key on the other edge, 19.5 m, with estimate FFL, and road level on four), then 2202, 2203, 2204, 2205 (link Z 370 mm under the key FFL, estimate FFL and road level). All 10 link another prototype file in the key.
- placer_check.py --floors reports FAIL by construction: it reads the key by the five config floor types and the plot number parameter, the 1.4 key has neither, so it found 0 key floors. Its own side: 61 links placed, 0 missing, 0 extra, 0 twice, 183 floors, not stopped.

## Images (R214)

- round_z4\images: testhost_Z4N_plan (the new file, 61 links and 183 floors in the view) and testhost_K24_plan (the key, 657 floors in the view, no house, its link files are not here). Same area, 509 by 527 m, plan cut 2000 m above Level 1.

## Second method (R217)

- z4_check.py, json and math only: seed 14 drew 20 plots, the CAD plan against the key 38 of 38 values agree in band (19 plots written), the placed links against the raw past links 19 of 19. Seed 7: 40 of 40 and 20 of 20.
- The first run disagreed on 16 values, all gaps of exactly 1.000 mm on a band edge where float noise of the shared coordinates (about 5e-7 mm) tipped the band. Both scripts now hold a band edge to 1e-6 mm.

## Windows (R200)

- 8 windows, one at a time, each under the lock: upgrade 2.0 min, read 0.5 and 0.6 min, newSite 0.4 and 0.4 min, linksAndFloors 0.7 min, images 0.9 and 0.6 min. Free memory at the lock 3.5 to 4.9 GB. One wait of 5 min, free memory 1.97 GB right after a Revit closed. No lock of the other session and no acad.exe met. The watcher answered 5 dialogs per start inside our own Revit only: Do Not Load for RCRC Green and RCRC Green Audit, Load Once for the three DLLs of this tool.

## Clean up (R218)

- Deleted from round_z4\work: 01101-PSN-C01-ZZZ-PIM-EL-000004.rvt 63,373,312 bytes, 01101-PSN-IMD-ZZZ-PIM-PP-000004.rvt 84,381,696, 01101-PSN-IMD-ZZZ-PIM-SE-000004.rvt 17,051,648, 01101-PSN-IMD-ZZZ-PIM-TU-000004.rvt (the 2021 copy) 74,448,896, landxml_read (reader cache) 54,732,159. Total 293,987,711 bytes.
- Kept: the 2024 key copy 74,301,440 bytes, the new file 6,594,560 bytes, the two images, the site spec, the site report, the shared parameter file and the round settings of the key.
