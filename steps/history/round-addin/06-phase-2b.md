## Phase 2 part 2, PR 46 data/addin-key-plans

Branch data/addin-key-plans from main at 104c1f3, equal to origin/main after git fetch (RAN). Rules R325 to R328 are new, R329 is not used, the PR holds four changes. R325 changes how R206 and R264 map a link, R326 adds the source and kind columns to R269 and fills the parsons table of R301. The phase ran in a helper agent started by the main session, which runs the checkers and merges. No Revit or Civil 3D window was opened, the reads of 2a are the input, so the lock was not taken and the registry was not read or written. Values of coordinates and levels stay in Documents\SiteAdaptation, this file holds counts, plot numbers, ids and differences only.

The breaker and the claim checker read the first push (head 5ecd49b). Their 19 items were fixed in new commits bc5a743 to the record commit, each code fix with its test seen failing first, and every output of 2b was made again. The numbers below are those of the rerun, the first run is kept only where it says so.

### Originals and inputs (R201, R202)

- First run: the 60 files of 2a (the 40 originals, the 7 key copies, the 13 Revit 2021 STR files) and 61 inputs (the 51 files of the 7 reads, round_17\past_all.csv, plots.csv, labels.csv and the baseplan plots.json of the round_17 snapshot, plan_imc_past.json, prototypes_imc_past.json, the three Native plans and footprint_imc.json) were equal by hash before and after (RAN, round_addin\logs\originals_p2b_before.txt, originals_p2b_after.txt, inputs_p2b_before.txt, inputs_p2b_after.txt).
- Review run: the same 60 and 61 hashed before the first fix, equal to the after files of the first run, and again after the last run (RAN, originals_p2b_fix_before.txt and _after.txt, inputs_p2b_fix_before.txt and _after.txt, the counts are in the PR body). So no read of 2a, no key copy, round_17\past_all.csv and plan_imc_past.json were written. config/overrides/roshn-makkah.json was hashed before and after the majority run of the review, equal (RAN, p2b_fix_overrides_hash_before.txt).

### What the review changed (R325 to R328)

| Item | Change | Test, seen failing first in round_addin\logs |
|---|---|---|
| 1 | A generic floor of a key joins the one baseplan face that holds cadLinks.cutShareMin (0.9, R120) of its area, in the plan and in the join, never the face its representative point falls on. Below that the plot is cut and listed with every face, number and share. IMB 1995, IME 2634, IMF 702 and IMG 480 and 481 sat 0.50 on two faces and were filed by chance | test_r325_a_generic_floor_joins_a_face_only_by_an_area_share..., test_r325_main_joins_by_the_cut_share..., test_r326_a_generic_floor_joins_the_face_that_holds_cut_share_min..., p2b_fix_r325_first.txt, p2b_fix_r326_join_first.txt |
| 2 | Two key plots on one official number are twoPlots, listed by number, and neither counts as joined or in the area row | test_r326_two_key_plots_on_one_official_number..., p2b_fix_r326_join_first.txt |
| 3 | An ELl@Entry value that puts the flat floor top outside keyPlans.entryHeightM (0.0 to 1.5 m) over it is refused with the height it gives and never used, and the plan has no height for it. The spot counts leave it out too | test_r325_an_el_at_entry_parameter_whose_height_is_outside_the_band..., test_r327_a_key_value_whose_floor_top_is_outside..., p2b_fix_r325_first.txt, p2b_fix_r327_band_first.txt |
| 4, 14 | A spot counts for a plot only when its host floor names that plot, and the spots are grouped by that plot first | test_r327_a_spot_on_a_floor_of_another_plot..., p2b_fix_r327_first.txt |
| 5 | past_all.csv has a kind column from answerKeys (TU or SE), majority.kindLeaveOut keeps the SE rows out of the ramp slope, El @ Entry, ramp front and fall, height, steps and yard decisions | test_r326_the_majority_rule_leaves_the_rows_of_a_kind_out..., test_r326_a_majority_run_over_the_real_past_all..., p2b_fix_r326_past_all_first.txt |
| 6 | El @ Entry of a key plan has the source keyParameter, the plan of the past job keeps pastJob for its schedule | test_r325_a_key_plan_labels_el_at_entry_by_the_key_parameter..., p2b_fix_r325_first.txt |
| 7 | --schedule-entry gives El @ Entry of a schedule only to the key pastAll.scheduleKeys names for its pastSource, and heightSource says what the height was worked out from | test_r326_the_schedule_gives_el_at_entry_only_to_the_key_of_its_own_job..., p2b_fix_r326_past_all_first.txt |
| 8 | entry_spots.py finds the schedule by the official number of the join (--join), the second method by the official number of its own join | test_r327_el_at_entry_of_the_schedule_is_found_by_the_official_number..., p2b_fix_r327_first.txt |
| 9 | The second method draws its samples from every plot of the read, ties a link by the face its house centre stands on, and checks walls per plot, height, steps, modelPlot, source, kind, entrySource and heightSource, and the counts of join, area row, plan and past_all.csv | the R328 tests of test_key_plans_check.py, p2b_fix_r328_first.txt |
| 10 | number_routes takes the whole number after the last dash, so VD2-0481 and TH1-481 both end in 481 | test_r325_the_number_route_takes_the_whole_number..., p2b_fix_r325_first.txt |
| 11 | An answer key plan is never written over pastJob.output, with or without --key, and with --key never under the name or over the plan of another key | test_r325_an_answer_key_plan_with_no_out..., test_r325_a_key_plan_is_never_written_under_the_name_of_another_key..., p2b_fix_r325_first.txt |
| 12 | key_join.json and entry_spots.json keep the inputs and settings of each key, the second method reads them per key | test_r326_each_key_of_the_join_keeps_the_inputs..., test_r327_each_key_keeps_the_settings..., p2b_fix_r327_first.txt |
| 13 | The second method reads floors.plotParameter, the spot bands come from entrySpots.bandsM | test_r328_the_sample_is_drawn_from_every_plot..., the R327 settings tests |

- keyPlans.entryHeightM 0.0 to 1.5 m (CHOSEN, measured): on the 7 reads every generic floor top stands 0.05 to 0.8 m over its ELl@Entry but 21, IMC 10 and IMD 4 whose value is 0, IMF 6 whose value is 150.0 and IMD 1217, which stand 14.8 m to 174 m off it (RAN, scratchpad heights_probe.py).
- The R321 test of a key plan now names its plan plan_imx.json, as item 11 asks of a key plan. The R301 test pins the new parsons counts. The join on every plot of the second method was added after its tests and has no test of its own.

### R325, the plans of the answer keys

- Before 2b: plan_from_past.py --answer-key --names (R206) mapped a link by its Name when that is a floor Plot value, else by its frame (R264). The 1,858 plot links of the 5 TU keys have a Name with bars and no floor Plot value, so no TU link mapped (READ, the reads).
- Now a link maps first by its RRE_ZZ_SP_Plot-Number, then by its Name, then by its frame, keeps its own unit and loses its plot number when nothing maps it. With --key the plan holds El @ Entry of the ELl@Entry parameter within the band, the steps of the link, the height computed, modelPlot, the code of the key as neighbourhood and walls with their plots (CHOSEN, the brief).
- The 7 plans of the rerun, IMC first (RAN, round_addin\logs\p2b_fix_plan_<code>.txt), in Documents\SiteAdaptation: plan_imc_key_past.json, since plan_imc_past.json is kept, and plan_imd_past.json to plan_img_past.json, plan_ima_past.json and plan_imb_past.json, each over the plan the first run of 2b wrote.

| Key | Plots written | Links by parameter | Links not mapped | Generic floors cut | El @ Entry refused | Floors with no official number | Walls | Walls on no plot |
|---|---|---|---|---|---|---|---|---|
| IMC | 324 | 324 | 14 | 12 | 10 | 25 | 926 | 25 |
| IMD | 403 | 403 | 8 | 7 | 5 | 14 | 1,138 | 10 |
| IME | 320 | 320 | 5 | 4 | 0 | 8 | 894 | 10 |
| IMF | 476 | 476 | 15 | 14 | 6 | 28 | 1,317 | 30 |
| IMG | 268 | 268 | 30 | 19, and 10 on no face | 0 | 58 | 859 | 68 |
| IMA | 420 of 710 rows | 420 | 13 | 23 | 0 | 23 | 2,323 | 49 |
| IMB | 290 of 710 rows | 290 | 12 | 23 | 0 | 23 | 1,515 | 47 |

(RAN.) The rows of IMA and IMB are the 710 of the 733 shared Generic floors one face holds 0.9 of, a plot with no link of that key is listed as missing its link. No link was loaded in a read, so the position proof of R82 has 0 links. A wall goes to the plots whose generic floor holds the middle of its location line or has an edge within 0.3 m of it, a wall of a cut plot goes to none.

- The new IMC plan against plan_imc_past.json, leaf by leaf (RAN, round_addin\logs\p2b_plan_fields.py, p2b_fix_plan_fields_imc.txt and .json): 324 plots in both, 13 only in the old plan, the 12 cut plots 2311, 2312, 2375 to 2378, 2387 to 2390, 2544, 2545, which are the cut list of R120, and 2441 with no generic floor. Equal on 324 of 324: case, insert, rotation, mirror, unit, prototype file, link Name, the 9 parameters, steps, FFL and the generic, ramp and yard floors whole. Different: El @ Entry, the height that follows it, SSL only in the old plan (the key holds none) and modelPlot only in the new. The source of El @ Entry is keyParameter against pastJob and of the height computed against pastJob on 314 plots.
- El @ Entry of the key against the schedule (RAN, p2b_fix_imc_entry_gaps.txt): 10 plots hold 0 and are refused (2583 to 2588, 2595 to 2598). Of the 314 others 26 are equal, 175 within 1 mm (the parameter holds 3 decimals, the schedule 4), 13 over 1 mm up to 30 mm, of them 2623 at exactly 30 mm and 2561 11 mm over the schedule, and 100 over 30 mm, 0.0301 to 0.113 m under the schedule, 10th percentile 0.040, median 0.070, 90th percentile 0.080. Why is UNKNOWN (Q070).
- R129, plan_compare.py --against-plan on a copy in round_addin\plans_compare (RAN, p2b_fix_plan_compare_r129.txt): 324 plots, 320 compared, it leaves out 2374, 2391, 2543 and 2546, the plots of plotFrame.apart still in the plan. Its bands as they are: FFL, insert in plan and Z, mirror, prototype file, rotation, steps and unit equal on 320, the generic, ramp and yard outlines within 1 mm on 320 and their tops and corners within 5 mm, SSL missing on 320, El @ Entry equal 25, within 1 mm 172, worse than 10 mm 113, missing 10, the height equal 26, within 1 mm 171, worse 113, missing 10. In the first run R129 left out 15 plots of plotFrame.apart (2311, 2374 to 2378, 2387 to 2391, 2543 to 2546) and compared 320 of 335.
- What Part 2 needs for sheets, kept in each plan: the official number and the number of the model (modelPlot), the link origin in shared coordinates, the generic and yard outlines with arcs and the four ramp corners with their levels, the levels FFL, El @ Entry, height and steps (no SSL, the keys hold none), the link file, rotation, mirror, unit, CASE and the 9 parameters, and walls: id, type, length, height, base and top constraint and offset, location line, the ends of its location line and its plots (CHOSEN). The wall faces and width are not read (R17).

### R326, the join, the place check, the area codes and past_all.csv

- Join by area share (RAN, round_addin\join\key_join.json, logs\p2b_fix_key_join.txt, p2b_fix_join_lists.txt), IMC first:

| Key | Link plots | Joined, all on their own number | Cut | Other, by why | NBH, zone |
|---|---|---|---|---|---|
| IMC | 337 | 324 | 12, the cut list of R120 | noGenericFloor 2441 | 012, Zone 2 |
| IMD | 410 | 403 | 7 (1278, 1279, 1400 to 1402, 1486, 1487) | none | 013, Zone 2 |
| IME | 324 | 320 | 4 (2634, 2635, 2656, 2657) | none | 02, Zone 3 |
| IMF | 490 | 476 | 14 (702, 703, 859 to 864, 901 to 906) | none | 03, Zone 3 |
| IMG | 297 | 268 | 18 (476 to 484, 550, 551, 2954 to 2960) | noFace 431 to 440, 474 (440 since breaker 3) | 01A, Zone 4 |
| IMA | 432 | 420 | 12 (1588, 1589, 1611, 1612, 1764 to 1771) | none | 021, Zone 1 |
| IMB | 301 | 290 | 11 (1995 to 1997, 2006, 2007, 2265 to 2270) | none | 022, Zone 1 |

- 2,501 of the 2,591 link plots join, 55 fewer than the first run. Every plot the first run joined to another number (IME 2634, IMF 702, IMG 481, IMB 1995) and every plot it left on a face of no or two plan labels is cut now, and no joined plot has another number, no number holds two key plots (twoPlots 0) and no plot stands on two faces. IMG 440 touches face 206 by 0.000127 of its area, under keyPlans.minShare since breaker 3, so it is on no face, as the grid of R328 finds it. The zone and NBH of the location plan equal the area key of plots.csv on every joined plot.
- Place check (RAN, same file, p2b_fix_place_swaps.txt):

| Key | Links match, other | Generic floors match, cut | Ramps match, other |
|---|---|---|---|
| IMC | 335, 2 (2312 on 2311, one on a face of no label) | 324, 12 | 325, 12 cut |
| IMD | 410, 0 | 403, 7 | 402, 8 cut |
| IME | 322, 2 | 320, 4 | 320, 4 cut |
| IMF | 480, 10 | 476, 14 | 476, 8 cut and 6 noNumber |
| IMG | 281, 16 (11 noFace, 481 on 480) | 268, 18 and 11 noFace | 267, 19 cut and 11 noFace |
| IMA | 431, 1 (1769 on 1770) | 710 of the 733 shared, 23 | none |
| IMB | 296, 5 | the same 733 floors as IMA | none |

- No two plots of any key swap places (RAN). The 3 links of another number are all of cut plots.
- Area codes: the parsons table of config/area_codes.json written again from key_join.json by round_addin\logs\p2b_area_codes.py and checked by im_codes.load_table (RAN, p2b_fix_area_codes.txt): IMA 021 Zone 1 420, IMB 022 Zone 1 290, IMC 012 Zone 2 324 beside its schedule 337, IMD 013 Zone 2 403 beside its schedule 410, IME 02 Zone 3 320, IMF 03 Zone 3 476, IMG 01A Zone 4 268. Q011 stays closed, IME is NBH 02 of Zone 3 by its plots.
- past_all.csv in round_addin (RAN, p2b_fix_past_all.txt): 4,896 rows, native 2,395 (u11 394, u12 585, u13 783, u14 633), parsons 2,501 (imc 324, imd 403, ime 320, imf 476, img 268, ima 420, imb 290), by kind TU 1,791 and SE 710, the native rows of no kind. The native rows equal round_17\past_all.csv cell for cell on every column of that table, 2,395 of 2,395 in order, their kind and heightSource empty (RAN, p2b_fix_past_all_native_compare.txt). The 324 imc rows equal the Round 17 imc rows on every column of that table, the 13 others are the 12 cut plots and 2441 (RAN, p2b_fix_past_all_imc_compare.txt). El @ Entry comes from the schedule for imc 324 and imd 403, from the floor parameter for ime 320, imf 470, img 268, ima 420 and imb 290, and imf 6 are refused. heightSource is floorTop less floorParameter on every parsons row with a height of ime to imb, and schedule less schedule on imc and imd since breaker 3, which takes FFL, El @ Entry and the height as one schedule pair.
- The counts agree: on each key the join, the area row, the plan and past_all.csv hold the same plots (RAN, the second method below). In the first run IMG read 283 in the join and the area codes and 281 in its plan and past_all.csv.
- Majority run over the new past_all.csv (RAN, round_addin\majority\majority.json, logs\p2b_fix_majority.txt and p2b_fix_majority_kinds.txt, the override list to round_addin\majority, never config/overrides): the parsons decisions of ramp slope, El @ Entry, ramp front and fall, height, steps and yard take 1,785 to 1,791 TU rows and no SE row. Hand, generic floor and the foundation type and CASE tests keep the 710 SE rows, whose links hold those values. The rules themselves are for Phase 3a.

### R327, the spots at the plot entrance

- radiiM 0.5 m and 6.0 m and cornerM 0.05 m (CHOSEN in the first run, measured on IMC: the spots sit on the ramp corners and the corners of the entrance edge lie 5.0 m from its middle at the median, no spot lies at the middle). bandsM 0.001 and 0.005 m (CHOSEN, the rounding of R2 and majority.levelHoldMm).
- Per key (RAN, round_addin\spots\entry_spots.json, logs\p2b_fix_entry_spots.txt and p2b_fix_entry_spots_shares.txt), only the spots of the floors of the plot, the schedule by official number:

| Key, El @ Entry of | Plots | A spot within 0.5 m | A spot within 6 m | Within 1 mm, share | Within 5 mm, share | Corner pair: plots with both, mean within 1 mm, within 5 mm, share |
|---|---|---|---|---|---|---|
| IMC, key | 326 | 0 | 237 | 2, 0.006 | 12, 0.037 | 261, 29, 86, 0.330 |
| IMC, schedule | 324 | 0 | 236 | 2, 0.006 | 5, 0.015 | 260, 37, 153, 0.588 |
| IMD, key | 405 | 0 | 296 | 5, 0.012 | 15, 0.037 | 325, 56, 119, 0.366 |
| IMD, schedule | 403 | 0 | 292 | 2, 0.005 | 10, 0.025 | 323, 65, 216, 0.669 |
| IME, key | 324 | 0 | 213 | 7, 0.022 | 17, 0.052 | 249, 51, 90, 0.361 |
| IMF, key | 484 | 0 | 343 | 1, 0.002 | 9, 0.019 | 380, 37, 124, 0.326 |
| IMG, key | 296 | 0 | 215 | 5, 0.017 | 16, 0.054 | 237, 60, 84, 0.354 |

- Key values refused by the band: IMC 10, IMD 5, IMF 6. Spots on a host that is no floor of a plot: IMC 1, IMD 4, IME 4. IMG has one ramp of no entrance edge.
- IMA and IMB: no ramp and no spot value (822 and 587 spots on STR links), nothing to compare. So 5 keys were compared, not 7, and on none of the 5 does a spot at the plot entrance give El @ Entry: at most 0.054 of the plots have a spot of their own floors within 6 m and within 5 mm of it, and the mean of the two entrance corner spots holds within 5 mm on 0.326 to 0.669 of the plots with both. Q001 does not close.

### R328, the second method

- The first run sampled from the lists of the first method and tied links by the plot parameter, the review replaced both (item 9). The first run of the new check found one plot where the two joins differ that no sample held, so the join is now held on every plot as well.
- Rerun on all 7 keys (RAN, round_addin\check\key_plans_check.json, logs\p2b_fix_key_plans_check.txt), samples from every plot of the read:

Each cell gives the items compared at seed 14 and at seed 7, and every one of them matched.

| Key | Counts of join, area row, plan, past_all | Join on every plot, matched | Plans | Join | Place | Spots | past_all rows of the sampled plan plots | Spot counts on every plot |
|---|---|---|---|---|---|---|---|---|
| IMC | 324 each | 337 of 337 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 20 | equal |
| IMD | 403 each | 410 of 410 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 19 | equal |
| IME | 320 each | 324 of 324 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 20 | equal |
| IMF | 476 each | 490 of 490 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 20, 19 | equal |
| IMG | 268 each | 297 of 297 | 20, 20 | 20, 20 | 60, 60 | 20, 20 | 19, 15 | equal |
| IMA | 420 each | 432 of 432 | 20, 20 | 20, 20 | 32, 35 | 20, 20 | 12, 14 | equal |
| IMB | 290 each | 301 of 301 | 20, 20 | 20, 20 | 28, 25 | 20, 20 | 8, 4 | equal |

- Every compared item matches. Before breaker 3 IMG 440 was cut by the first method on its touch of face 206 and noFace by the grid, keyPlans.minShare made both noFace. The past_all rows sampled are the sampled plots that are in the plan. The area row of each key equals the join counted by the area key of the official number.

### Known limit, recorded only

- Since PR 45 a read is tied to its key by the exact name <copy stem>_TEST-<job>, but the zone2 master of Revit All Zones and the IMC 2024 copy share one stem, so a read of a work copy made from the master passes as IMC by its name. Only a hash tie would part them. Q065, whose recommended answer replaces the zone2 file by a copy of the Zone 02\ARC original, removes the cause.

### Not done in this PR

- R329 is not used, the PR holds four changes and the review fixes stay inside them.
- CLAUDE.md was not changed by the helper: R325 to R328 and python/key_join.py, python/entry_spots.py and python/key_plans_check.py are for the main session to name there.
- The wall faces and width are not read, a wall goes to its plots by the middle of its location line. The footprint residual of R229 is in the imc rows only. SSL is in no key.
- No Revit window was opened.

### Tests first

- First run: most tests were run first and seen failing for the right reason. The R326 and R327 files failed at collection only, ModuleNotFoundError for key_join and entry_spots, then their real settings tests failed until the config held the new blocks (RAN, p2b_r325_first.txt to p2b_r328_first.txt).
- Review: each fix had its tests run first and seen failing, the outputs in p2b_fix_r325_first.txt, p2b_fix_r326_join_first.txt, p2b_fix_r326_past_all_first.txt, p2b_fix_r327_first.txt, p2b_fix_r327_band_first.txt and p2b_fix_r328_first.txt. The test of the real past_all.csv failed until the rerun wrote the kind column.

### Gate

- See the PR body, with the head it ran on.

### Questions

- Q011 stays closed, its answer now says 320 of the 324 IME plots join NBH 02 of Zone 3 by area share, the 4 others are cut.
- Q001 stays open: no entrance spot gives El @ Entry on the 5 TU keys compared, IMA and IMB have no ramp and no spot value.
- Q070 (new in 2b): the El @ Entry of IME, IMF, IMG, IMA and IMB comes only from the ELl@Entry parameter of the generic floor, which is off the IMC schedule by over 30 mm on 100 of the 314 IMC plots both hold, and 0 on 10 more, refused.
- No older question was shortened in this PR, PR 45 shortened them.

### Breaker pass 3

A third breaker pass read the PR at ebd69d2 and found 8 items. Each is fixed in a new commit, 8b7c004 to a47cdba, no amend, each with its test run first and seen failing (RAN, round_addin\logs\p2b_b3_item1_first.txt to p2b_b3_item8_first.txt, the head in the first line). The outputs that change were made again at a47cdba as files of this round, never over plan_imc_past.json, a read of 2a, round_17\past_all.csv or config/overrides. No Revit window was opened.

| Item | Rule | Change | Test |
|---|---|---|---|
| 1 | R326, R328 | A key with a schedule row takes FFL, El @ Entry and the height as one pair of the schedule, pastFfl and pastEntry of plots.csv, with heightSource schedule less schedule, never the key floor top with the schedule El @ Entry. A schedule row with El @ Entry and no FFL is refused. Each plot where the floor top of the key plan or its El @ Entry is off the pair by more than majority.levelHoldMm goes to past_all_conflicts.csv beside the table with both values. The second method checks FFL and the height as well | test_r326_a_key_with_a_schedule_takes_ffl_el_at_entry_and_the_height_as_one_pair..., test_r326_the_conflicts_list_is_written_beside_the_table..., test_r328_source_kind_el_at_entry... |
| 2 | R326 | Every kind is held against answerKeys.kinds as written there, case folded only when majority.kindFoldCase is true, every row of a group of majority.kindGroups (parsons) must carry one, in past_all.py before it writes and in majority.py when it reads, and majority.kindLeaveOut may name only kinds of answerKeys.kinds | test_r326_a_row_of_a_kind_group_with_no_kind..., test_r326_the_kind_of_an_answer_key_must_be_one_of..., test_r326_the_table_refuses_kept_parsons_rows..., test_r326_majority_refuses_a_kind... |
| 3 | R270 | entry_gaps.py holds the kinds as R326 and leaves out a row whose kind has entry in majority.kindLeaveOut, counted in leftOut | test_r270_a_row_of_a_kind_majority_kind_leave_out_keeps_out... |
| 4 | R325, R327, R328 | One flat top tolerance, keyPlans.flatTopM, in the plan, the spots and the second method, and the spots refuse a key value of a floor with no flat top as the plan does | test_r327_one_flat_top_tolerance..., test_r325_the_flat_top_of_a_generic_floor..., test_r325_the_flat_top_tolerance_must_be..., test_r328_the_flat_top_is_held... |
| 5 | R120, R325 | A generic floor whose face of cutShareMin holds no plan label or two is listed in cutPlots as noNumber or manyNumbers with that face | test_r325_a_generic_floor_whose_face_has_no_plan_label_or_two... |
| 6 | R325, R326, R328 | A share under keyPlans.minShare is left out by the plan, the join and the grid, so a floor that only touches a face is noFace by both methods | test_r326_a_generic_floor_that_only_touches..., test_r325_a_generic_floor_that_only_touches..., test_r325_the_minimum_share_must..., test_r328_a_grid_share_under_the_minimum_share... |
| 7 | R328 | The grid is laid in the axes of the longest edge of the floor and a floor with a share within 0.05 of cutShareMin by the coarse grid or the join of R326 is measured again on 200 by 200, one row 0.005 of the floor | test_r328_a_floor_whose_share_lies_near_cut_share_min... (0.89, 0.894, 0.906, 0.91 at 0 and 30.1 degrees), test_r328_a_floor_far_from_cut_share_min... |
| 8 | R326, R327 | key_join.py and entry_spots.py keep a key of the earlier file only when it holds the settings of this run, else the run is refused and the file left as it is | test_r326_each_key_of_the_join_keeps_the_inputs_and_the_share..., test_r327_each_key_keeps_the_settings... |

- Item 7 before the fix (RAN, p2b_b3_item7_first.txt): the 50 grid over the box read 0.894 as 0.900 and joined it, at 0 degrees. At 30.1 degrees the box grid read the four shares right, it is the edge parallel strip that a box grid can not resolve.
- Values: majority.levelHoldMm, 5 mm, is the band of the conflicts list, the band a level rule holds within (CHOSEN). keyPlans.flatTopM 0.0005 m (CHOSEN): every generic floor top of the 7 reads is exactly flat, its max less its min 0 on all 3,323 (RAN, scratchpad probe_b3.py). keyPlans.minShare 0.001 (CHOSEN, measured): the largest share of a floor that only touches a face is 0.000557, the ramp of IMG 440, its generic floor 0.000127, and no share of the 7 reads lies between 0.000557 and 0.0011 (RAN, scratchpad probe_b3b.py). majority.kindGroups parsons and majority.kindFoldCase false (CHOSEN). The refine band 0.05 and the resolution 0.005 are constants of the second method (CHOSEN, the coarse grid is off by up to 0.02).
- Plans (RAN, p2b_b3_plan_<code>.txt, p2b_b3_plan_compare.txt): the 7 plans written again, equal to the review plans on every plot and on the missing list. Only the IMG summary moved, DP4-440 from cut to noFace, cut 18 and noFace 11. Item 5 lists no floor on the real keys, no generic floor stands on a face of no or two plan labels.
- Join (RAN, p2b_b3_key_join.txt): IMG cut 18, noFace 11 (431 to 440, 474), its generic floors 268 match, 18 cut, 11 noFace, its ramps 267, 19 cut, 11 noFace. Every other count as in the review, 2,501 of 2,591 joined.
- past_all.csv (RAN, p2b_b3_past_all.txt, p2b_b3_past_all_compare.txt): 4,896 rows, the same keys and plots in order. Cells that moved: ffl and height of imd 1382 and 1436, the height 0.2527 to 0.0997 and 0.1499 to 0.0999, and heightSource of the 324 imc and 403 imd rows to schedule less schedule. The imc rows equal the Round 17 imc rows on every column of that table, 324 of 324, the native rows 2,395 of 2,395.
- Conflicts list (RAN, round_addin\past_all_conflicts.csv): 260 rows, imc ffl 0, imc entry 113, imd ffl 2 (1382, the schedule FFL 0.153 m under the floor top, 1436 0.050 m), imd entry 145. The key El @ Entry stands under the IMD schedule on all 145, 0.0099 to 0.0805 m, median 0.0495, and over 30 mm on 115 of the 398 plots that hold both. Why is UNKNOWN (Q070).
- Second way of item 1 (RAN, p2b_b3_conflicts_check.py and .txt, json and csv only): the same 0, 113, 2 and 145 plots, and all 324 imc and 403 imd rows hold the schedule pair.
- Spots (RAN, p2b_b3_entry_spots.txt): every count as in the review, the settings of each key now hold flatTopM.
- Second method (RAN, p2b_b3_key_plans_check.txt): every compared item matches on all 7 keys at seeds 14 and 7, the join of every plot 2,591 of 2,591 with IMG 297 of 297, and the past_all rows now with FFL and height. No floor of the join had a share within 0.05 of 0.9 by either method, so every one was measured on the 50 grid. The run took 42 s, 23 s before.
- Majority (RAN, p2b_b3_majority.txt, p2b_b3_majority_compare.txt, to round_addin\majority): parsons height misses 420 to 419 and steps 23 to 22, 1382 left both, parsons yard 0 to 2 misses, 1382 and 1436, whose yard top follows the key floor top while the FFL is now the schedule, the share 0.999 still strong. Overrides 7,597 to 7,595 in round_addin\majority\overrides_check.json. No SE row in the ramp, level, steps or yard decisions.
- El @ Entry gaps (RAN, p2b_b3_entry_gaps.txt to round_addin\gaps, p2b_b3_entry_gaps_before_fix.txt): on the new table 710 SE rows are left out and no pair holds ima or imb. The entry_gaps.py of ebd69d2 on the same table paired ima with u13 on 390 plots and imb with u14 on 289.
- Item 8 on a copy of the real join (RAN, p2b_b3_item8_real.txt): IMC alone at cutShareMin 0.8 exits with code 2 and names the 6 other keys, the hash of the copy unchanged.
- Hashes (RAN): 60 of 60 originals and 61 of 61 inputs equal before and after by sha256sum (originals_p2b_b3_before.txt, _after.txt, inputs_p2b_b3_before.txt, _after.txt), 60 of 60 by Get-FileHash (originals_p2b_b3_gethash.txt), plan_imc_past.json, round_17\past_all.csv and config/overrides/roshn-makkah.json equal (p2b_b3_protected_before.txt, _after.txt).
- The commands of the review were rebuilt and run at ebd69d2 first. Plans, join, spots, check and past_all.csv came out byte for byte equal to the review outputs (RAN, scratchpad), so the rerun runs what the review ran.
- Not done: the yard decision keeps FFL less the yard top, it does not take the key floor top on a plot with a schedule pair. Q070 holds the IMD numbers now. No new question.

