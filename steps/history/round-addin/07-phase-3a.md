## Phase 3 part 1, PR 3a rules/addin-parsons-rules

Branch rules/addin-parsons-rules from main at 4894b4a, equal to origin/main after git fetch (RAN). Rules R330 to R337 are new, R337 in the review, R271, R272, R277, R279, R280 and R281 changed. Four changes: R330 (2d56f89), R331 and R332 (6df83e0), R333 and R334 (a7b8172), R335 (1649624 and bf9941d, which takes only the level parameters from the key reads, since a read found by its plot number holds it by construction), with R336, the second method, grown in each. The breaker and the claim checker read the PR at 351c2ac, their 21 items are fixed in new commits 251c74d to the record commit, no amend, see Review of PR 47 below, where every number of the rerun is. The numbers above that subsection are those of the first push where it does not say otherwise. The phase ran in a helper agent started by the main session, which runs the checkers and merges. No Revit or Civil 3D window was opened, the reads, plans, join and past_all.csv of Phase 2 are the input, so the lock was not taken and the registry was not read or written. Values of coordinates and levels stay in Documents\SiteAdaptation\round_addin\rules3a, this file holds counts, shares, gaps and plot numbers only.

### Originals and inputs (R201, R202)

- 60 originals and 80 inputs hashed with sha256sum before the first edit and after the last run, 60 of 60 and 80 of 80 equal (RAN, round_addin\logs\originals_p3a_before.txt, _after.txt, inputs_p3a_before.txt, _after.txt, p3a_hash.sh). The 80 are the 61 inputs of 2b, the 7 key plans, round_addin\past_all.csv and past_all_conflicts.csv, the join, the spots, the 2b check, the gaps, the 2b majority run, the sales workbook and the four CAD tables of the snapshot. The 61 equal inputs_p2b_b3_after.txt 61 of 61 and the 60 originals equal originals_p2b_b3_after.txt (RAN). So no original, key copy, 2a read, 2b output, plan_imc_past.json or round_17\past_all.csv was written. config/overrides/roshn-makkah.json and rules/roshn-makkah.json were hashed before (protected_p3a_before.txt) and are changed by this PR only.
- Baseline: majority.py of 4894b4a run again on round_addin\past_all.csv gave majority.json and the override list of the 2b dry run equal as JSON (RAN, rules3a\baseline). Every later run is that command with the new options (p3a_run_majority.sh).

### R330, the source of an override (change 1)

- Before: every entry held the past model file in source, 5,709 entries, 5 files: IMC TU-000001 487 (key imc), IMA TU-000001 947 (u11), IMB TU-000002 1,321 (u12), IMC TU-000003 1,582 (u13), IMD TU-000004 1,372 (u14) (READ, the list). The field name source was taken, so the brief's source field (parsons or native) takes that name and the file moves to a new field model (CHOSEN, conflict 1 below). --override-source of plan_from_book.py names jobs since, --override-model narrows to model files.
- override_source.py migrate rewrote the list in place: source parsons 487, native 5,222, the file in model (RAN, p3a_r330_migrate.txt). Second way, json only: the list of main against the new one entry by entry, same order and fields, model the old source, source the group of the key, 5,709 of 5,709 match (RAN, p3a_r330_second.py and .txt).
- majority.overrideDefaultSources is parsons (CHOSEN, the brief): a build with --overrides and no source applies parsons only, a native2022 build must name --override-source native, else it is refused. majority.py --keep SOURCE or SOURCE:FIELD keeps the entries of the earlier list a run does not measure, cell for cell, and sorts the list by plot.
- Tests first (RAN, p3a_r330_first.txt): the new file stopped at collection with ModuleNotFoundError for override_source, then on an empty module 15 failed, AttributeError and the old refusal text of R280. The R272 and R280 tests that held the old field were changed with it.

### R331, coordinates (change 2)

- The 7 Parsons reads against the IMC read, 14 fields each (both points in internal and shared coordinates, angle, project position, Level 1 and Level 2, active site, sites, place, units system, length and length system): 0 differ on 7 of 7, oneSet. Length accuracy 0.01 on the TU keys and 0.001 on the SE keys, listed apart (RAN, rules3a\c4\majority.json siteFacts). The 4 Native reads against u14 on their points and angle: 0 differ, one set of their own. Across the sets the survey point stands 2,432 km apart in shared coordinates and the base point 2,203 m, 2.2 km (RAN, the first push said 2,203 km, a slip, p3a_fix_records_numbers.txt). keyRules.site of the profile holds angle 0, site Internal, Level 1 and Level 2 with Level 2 4.0 m over Level 1, metric meters, and no coordinate, which stays in the reads (Q068 open).

### R332, link origin, rotation and the corner front (change 2)

- IMC first (RAN, p3a_c2_imc.txt): rotation 324 of 324, corner front 108 of 108, origin 321 of 324 within 10 mm.
- All keys (RAN, p3a_c2_all.txt, later runs equal): rotation by the coded front 2,478 of 2,501, 0.991, the edge nearest a curb 0.987. The corner plots of R226 (Q037): 916 of 937, 0.978, IME 113 of 119 and IMG 87 of 102, every other key 1.0. Origin: plotFrame as fitted on IMC 2,374 (0.949), fitted again per code on the 7 keys 2,429 (0.971). The refit takes the end anchor for C10 at -10.0 m: on the 125 C10 plots 16 m wide both anchors hold, on the 49 wider ones only the end anchor does, by 0.5 to 3.2 m of extra width (RAN, p3a_probe_frame.py, its output was not saved in the first push and is saved by its rerun in the review, p3a_fix_probe_frame.txt, the same 125 and 49). The anchors of the refit are in keyRules.linkOrigin.byCode, prototype_offsets.json keeps the IMC fit (conflict 2).
- 10 mm is the hold of the origin (CHOSEN, majority.linkHoldMm): the plan and the key write a point to the mm, two roundings put up to 1.4 mm between equal points, and R121 fits by 10 mm.

### R333, hands (change 3)

- Villa: block mirror flag with the side road 765 of 797 (0.960), with the open end 764, alone 763 (RAN). The measure codes the side road by one plot (Q003). VL6 74 of 76, VL4 181 of 192. Villas between cluster units: both sides 7 of 10, one side 408 of 418, none 350 of 369.
- Cluster: the side in the footprint and analysis clusters (R236) 1,297 of 1,322 (0.981), the analysis clusters alone 1,173 (0.887), so the footprint clusters stay (Q040).
- Units in no cluster: 382 on the 7 keys against 16 on IMC in Round 17. Their DP2 and DP4 plots carry villa blocks in the Building Footprint drawing, so no footprint cluster joins them (READ, plots.csv). Paired along their row of such units from its right end, then the side learned per file, else the block mirror flag with the open end: 380 of 382 (0.995), the row pairs alone 367, the Round 17 rule 283 (0.741) (RAN). Q042 takes the new rule.
- Two fixes the second method found: R271 gave a plot with no front frame the open end both, it is unknown now (C10 2690 and 622, 19.25 and 19.0 m wide fronts over the R122 length limit), and a face of two plan numbers, such as 2007|2006 coded TH3|TH3, had the kind townhouse by its prefix and paired with a unit, it has no kind now (RAN, p3a_probe_hands.py, the tests test_r333_a_plot_with_no_front_frame... and test_r333_a_face_of_two_plan_numbers...).

### R334, ramps and floors (change 3)

- Ramp type by Rev 9 code 1,770 of 1,791 (0.988), 2% for C10, VL4, VL6. The 21 misses are the 21 flat ramps, IMC 10 (2583 to 2588, 2595 to 2598), IMD 5 (1217, 1375 to 1378), IMF 6 (888 to 893), and they are the 21 plots whose ELl@Entry parameter the plans of R325 refused as a placeholder, such as 0 or 150.0, 21 of 21 both ways (RAN). A new build has no such parameter, so no flat ramp is coded (Q002).
- Ramp outline of R127, each corner within 10 mm, 1,735 of 1,791 (0.969), IMG 0.881. Ramp front on the snapped road 0.808 and its fall 0.764 on the 5 TU keys, weak, 343 and 423 overrides, the fall not applied (Q057).
- Generic floor on the whole face 2,478 of 2,501 (0.991). Yard depth against the generic floor top of the key itself, not the schedule FFL: 1,791 of 1,791, IMD 1382 and 1436 no longer miss. Zone 2 and 3 0.100, Zone 4 (IMG) no yard, coded as a setting per zone, the key a check only. Since the review the table is keyed by the zone as plots.csv names it and Zone 1, which no TU key holds since the SE rows are left out of the yard, takes the Zone 2 and 3 value marked notMeasured pending Q066, the build says so on every Zone 1 build and refuses a zone the table does not hold (R337). Yard outline of R135 fitted again on the 4 keys with yards, 102 groups against the 52 of IMC: 1,466 of 1,522 (0.963), as fitted on IMC 1,378 (0.905).

### R335, walls and shared parameters (change 4)

- Walls of the key plans (RAN, p3a_c4_all.txt): the TU keys hold BW boundary walls, 4,901 on at least one plot of their plan, and 93 garden walls, the SE keys SBW (3,630 on a plot) and KEO_ST walls, counted only. Base at the yard level (FFL less 0.100) of the higher plot of the wall 4,815 of 4,901 (0.983), of the lower plot 0.558. Top at the FFL of the higher plot plus the 1.7 m of Rev 6 1,698 (0.346), weak. Wall centre on the plot edge (0 for a location line at the wall or core centre, 0.1 m for a face) 4,513 (0.921), IMD 0.798 and IMC 0.891. Front edge open, at most 0.2 of it under a boundary wall, 1,768 of 1,789 (0.988), the front is the gate. The 21 walled fronts (IME 2626, 2665, 2892 to 2897, IMF 727, IMG 3005 to 3016) are plots whose coded front is not the key front. Wall misses, 86 base, 3,203 top and 388 line, are in wallMisses of the report, not in the override list (CHOSEN, a wall has no id in a new build and no step places walls, Q071).
- Parameters: Plot-Number the plot 2,501 of 2,501, Prototype the Rev 9 code 2,501, Number-of-Steps the steps, both façade parameters XX and HVAC by code 1,791 of 1,791 each, RVT-Name the link Name plot | code | steps | colour | option 1,776 of 1,791 (0.992). The SE links hold RVT-Name as plot | code, so kindLeaveOut leaves the SE rows out of that one decision (CHOSEN, Zone 1 follows the TU rule). Foundation type by code and hand 1,550 of 2,501 (0.620). The first push said no height over any of the 18 road and ground surfaces separates T1 to T4, from a probe whose script and output were not saved, so that claim stands as READ only. The probe run again in the review (RAN, p3a_fix_probe_foundation.py and .txt): 26 surfaces of plots.csv, on 24 the heights of the FFL over the road of the four types overlap, on the two Missed from Paste surfaces they lie apart but those hold 9 plots of 2,501, CASE 687 of 2,501 (0.275), both weak with 951 and 1,814 overrides as built (Q013). Prototype-FFL, a level, is to the cm and within 5 mm of the FFL on 1,078 of 1,791, within 20 mm on 1,788 and within 50 mm on 1,790 (1,078 plus 712), 1 over, weak, left to 3c (RAN, p3a_fix_records_numbers.txt, the first push wrote 1,788 for 50 mm).

### R336, the second method

- First push: key_rules_check.py, json, csv, openpyxl and plain math, seeds 14 and 7, 20 plots and 20 walls each, and every plot and wall as well. Final run (RAN, p3a_c4_check.txt): seeds 14 and 7, 0 mismatches each, every plot 6 mismatches, walls 0 at each. The review run is in Review of PR 47. Every plot: rotation and origin 2,501 of 2,501, ramp type, ramp outline and yard 1,791 each, yard outline 1,522, front open 1,789, every parameter on its plots, walls 4,901 of 4,901 on base, top and line. Hands 2,495 of 2,501 agree, 6 do not: IMB 1993 and 1994, the first method gives cut plot 1995 no side neighbours and the own route does, IMD 1483, 1518 lies right of it in the first and left in the own route, and the villas IMC 2301, IME 2710 and IMG 441, corner plots where the side road differs. The review found what each of the 6 is (Review of PR 47, item 17). The coordinates by json 7 of 7 and 4 of 4, the counts of 30 of 30 decisions sum per key.
- The first run of the hand routes had 10 mismatches (RAN, p3a_c3_check_first.txt), which found the two fixes of R333 above, and the own route then took a cluster only when R271 gives its plots a side (every plot of past_all.csv with a face and one with a front).
- Each merged override list against the one before by JSON text (RAN, p3a_c2_list_compare.txt to p3a_c4_list_compare.txt): every kept entry cell for cell, no new gap over 1,000, plots in order.

### The override list (R330)

| After | Total | Native | Parsons | Parsons by field |
|---|---|---|---|---|
| main, by key group | 5,709 | 5,222 | 487 | entryM 241, heightM 32, mirrored 15, rampFrontFallM 97, rampFrontM 92, rampType 10 |
| change 1 | 5,709 | 5,222 | 487 | the same, with source and model |
| change 2 | 5,804 | 5,222 | 582 | plus insert 72, rotationDeg 23 |
| change 3 | 6,571 | 5,222 | 1,349 | entryM 241, heightM 32, insert 72, rotationDeg 23, mirrored 59, rampType 21, rampFrontM 343, rampFrontFallM 423, rampOutline 56, yardOutline 56, genericFloor 23 |
| change 4 | 9,351 | 5,222 | 4,129 | plus parameter CASE 1,814, Foundation-Type 951, RVT-Name 15 |
| review | 9,336 | 5,222 | 4,114 | entryM 230, heightM 30, mirrored 57, the rest as change 4 |

(RAN, p3a_c*_overrides_after.txt.) The native entries are those of Round 17, kept cell for cell. Parsons entryM 241 and heightM 32 are the IMC ones of Round 17, kept for 3c, which measures the levels. Change 3 left 30 IMC entries of Round 17 changed or gone, mirrored 11, rampFrontM 12, rampFrontFallM 7, the rules now measured on 7 keys.

### Every decision of 3a

Share over the Parsons plots the rule covers, per key, the Native share of this run and of Round 17, strong at 0.90, overrides written.

| Decision | Coded rule | Parsons | Per key (a b c d e f g) | Native, Round 17 | | Overrides |
|---|---|---|---|---|---|---|
| Coordinates | one set | 7 of 7 keys | all equal | 4 of 4 own set | strong | none |
| Link rotation | front frame, R226 on corners | 0.991 | 1.0 1.0 1.0 1.0 0.978 1.0 0.940 | 0.983 | strong | 23 |
| Corner front | Orientation quadrant | 0.978 | 1.0 1.0 1.0 1.0 0.950 1.0 0.853 | 0.972, 0.967 | strong | in rotation |
| Link origin | plotFrame refit per code | 0.971 | 0.981 0.993 0.988 0.995 0.972 0.954 0.907 | 0.912 | strong | 72 |
| Hand villa | block mirror with side road | 0.960 | 0.977 0.981 0.935 0.991 0.938 0.986 0.896 | 0.944, 0.945 | strong | 32 |
| Hand cluster | cluster side (footprint) | 0.981 | 0.981 0.986 0.980 0.996 0.986 1.0 0.911 | 0.984, 0.983 | strong | 25 |
| Hand alone | row pair, else block mirror and open end | 0.995, 1.0 in the review (site view of R271) | 1.0 0.949 1.0 1.0 1.0 1.0 1.0, imb 1.0 in the review | 0.833, 0.714 | strong | 2, 0 in the review |
| Ramp type | by Rev 9 code | 0.988 | TU: 0.969 0.988 1.0 0.987 1.0 | 0.977, 0.977 | strong | 21 |
| Ramp outline | R127 | 0.969 | TU: 0.988 0.985 0.969 0.992 0.881 | 0.832 | strong | 56 |
| Ramp front | snapped road | 0.808 | TU: 0.753 0.725 0.881 0.838 0.862 | 0.634, 0.634 | weak | 343 |
| Ramp front fall | road at each corner | 0.764 | TU: 0.722 0.752 0.828 0.710 0.851 | 0.623, 0.623 | weak | 423, not applied |
| Generic floor | whole face | 0.991 | 0.993 0.990 0.988 0.985 1.0 0.998 0.978 | 0.899, 0.899 | strong | 23 |
| Yard | 0.100, none in Zone 4, per zone | 1.0 | TU: 1.0 each | 1.0, 1.0 | strong | 0 |
| Yard outline | R135 refit | 0.963 | c d e f: 0.957 0.945 0.956 0.987 | none | strong | 56 |
| Wall base | yard level of the higher plot | 0.983 | TU: 0.985 0.983 0.978 0.989 0.973 | none | strong | report only |
| Wall top | FFL of the higher plot + 1.7 | 0.346 | TU: 0.340 0.309 0.313 0.451 0.274 | none | weak | report only |
| Wall line | centre on the edge | 0.921 | TU: 0.891 0.798 0.981 0.982 0.960 | none | strong | report only |
| Front open | no wall along the front | 0.988 | TU: 1.0 1.0 0.975 0.998 0.955 | none | strong | none |
| Plot-Number, Prototype | plot, Rev 9 code | 1.0 | 1.0 each | 1.0 | strong | 0 |
| Steps, façade, HVAC | steps, XX, by code | 1.0 | TU: 1.0 each | none | strong | 0 |
| RVT-Name | plot, code, steps, façade | 0.992 | TU: 0.985 0.990 0.994 0.996 0.993 | none | strong | 15 |
| Foundation type | code and hand | 0.620 | 0.636 0.517 0.537 0.727 0.697 0.498 0.769 | none (Round 17 IMC 0.685) | weak | 951 |
| CASE | code and hand | 0.275 | 0.181 0.134 0.364 0.266 0.366 0.305 0.317 | 0.314, 0.314 | weak | 1,814 |
| Prototype-FFL | FFL within 5 mm | 0.602 | TU: 0.574 0.640 0.600 0.609 0.567 | none | weak | for 3c |

(RAN, rules3a\c4\majority.json, the coded rules in keyRules of levelProfiles.parsons2024Rev6.) The keys a and b are IMA and IMB, the SE keys, which hold no ramp, yard, level or BW wall. A Zone 1 field they lack takes the TU rule with no override (the brief). The one per area setting is the yard per zone.

### Conflicts list

1. The override field source held the model file, the brief's source field holds the job now and the file is in model (R330).
2. config/prototype_offsets.json plotFrame, fitted on IMC, against keyRules.linkOrigin.byCode, fitted on 7 keys: C10, DT1, DT3 and TH1 take the end anchor, VD1 an offset 1 mm deeper and TH1-IM a fit of its own, the other 9 codes are equal (RAN). Since the review a parsons2024Rev6 build takes the second (R337), native2022 the first. Neither file was changed silently.
3. The villa hand: the hands table of Round 17 (R279) by block mirror and open end, the measure by block mirror and side road, one plot apart (Q003). Since the review a parsons2024Rev6 build takes the measure (R337).
4. The hand of the units in no cluster: hands.alone of Round 17 by block mirror and open end (0.741 on 7 keys) against the row pairs (0.995, 1.0 since the review) (Q042). Since the review a parsons2024Rev6 build takes the row pairs (R337).
8. Review: the context of R271 fronted only the plots of past_all.csv, a build fronts every unit plot. The cut face IMB 1995, 6 m wide and held by no key, had no front, so the row of IMB 1993 and 1994 stopped there. The measure takes the site view now (R271), which moves 3 row pairs (IMD 1280, IMB 1993, 1994) and 13 row parities, and the lone duplex hand to 382 of 382 (RAN, p3a_fix_site_view.py, rules3a\fix_six\site_view.json).
9. Review: a villa with three road edges, front, one side and back, has two other road edges, and the side road of R271 is the first of them in the order each route walks the edges, a side edge in the first method and the back in R336 (Q072).
5. R271 gave a plot with no front frame the open end both, now none, one Native villa more is a miss (native hand villa 64 against 63).
6. Q002 said the 10 IMC flat ramps slope like the others, the 21 flat ramps of 3 keys are the 21 plots whose El @ Entry parameter is a placeholder.
7. RVT-Name holds plot | code on the SE links and the five part Name on the TU links, the SE rows leave that decision.

### Tests first

- Each change ran its new tests first and saw them fail for the right reason, the output saved with the head in round_addin\logs: p3a_r330_first.txt, p3a_r331_r332_first.txt, p3a_r333_r334_first.txt, p3a_r335_first.txt, p3a_r336_first.txt, p3a_r336_c3_first.txt, p3a_r336_c4_first.txt and p3a_r336_road_side_first.txt (RAN). The tests of a later change were run first on the code of the commit before it. Added with their code and not seen failing first: the group test of R331, the run and profile tests of R332 to R335, the R333 tests of the two fixes, the straight face test of R336 and the R335 test of the kind leave out of one decision. test_r333_the_hand_of_a_unit_alone_is_learned_per_file_by_the_side_of_its_row_pair passed when first run, it holds majority.decide, which was there before (RAN in the review: the R333 tests of a7b8172 on the code of 6df83e0, 5 failed and 1 passed, p3a_fix_r333_first_replay.txt). p3a_r330_first.txt holds the collection error and the first reasons and no count line: replayed in the review, the R330 tests of 2d56f89 on the code of 4894b4a with an empty override_source.py, 15 failed (RAN, p3a_fix_r330_first_replay.txt).

### Not done in this PR

- The levels are 3c: the misses of El @ Entry (992), the height (419), the steps (22) and the Prototype-FFL parameter (713) are not written, majority.overrideKeep.laterFields of config/project.json names the four fields, and SSL. The parsons entryM and heightM entries kept are those of Round 17 on IMC plots of the join, 230 and 30.
- Since the review plan_from_book.py builds a parsons2024Rev6 plot by keyRules (R337), the Round 17 tables and the IMC plotFrame stay for native2022. The refit yard outlines are in keyRules.yardOutline.groups of the rules profile, config/floor_outlines.json keeps the IMC fit.
- No wall override and no wall top rule (Q071). The wall faces and width are not read.
- CLAUDE.md was not changed by the helper.

### Review of PR 47

The breaker and the claim checker read the PR at 351c2ac and gave 21 items. Each is fixed in a new commit, no amend, each code fix with its test run first and seen failing for the right reason, the output with the head in round_addin\logs: 251c74d R330 (p3a_fix_r330_first.txt, 18 failed), 4bc4670 R272, R331, R335 (p3a_fix_r272_r335_first.txt, 5 failed), c0da01a R280, R281 (p3a_fix_r280_r281_first.txt, 2 failed), e9fc1e0 R337 (p3a_fix_r337_first.txt, 4 failed on the missing interface, then the dummy row built by the code of the head: plot 2 a Round 17 hand, a yard in Zone 4, 3 parameters), 6d476ae R336 and R271 (p3a_fix_r336_first.txt, the new tests on key_rules_check.py of the head, 10 failed, and the head check stopped with KeyError linkFits on a report with none and passed an unknown candidate with exit 0, p3a_fix_r271_first.txt, 1 failed), b929b22 the print of the merged list (p3a_fix_r336_merged_first.txt, TypeError), 2d27705 the list and keyRules of the rerun. The tests of the R336 change were written after its code and run first on the code of the head. No Revit or Civil 3D window was opened, the lock was not taken, the registry was not read or written.

| Item | Rule | Change | Test |
|---|---|---|---|
| 1 | R337, R277, R279 | A level profile with keyRules builds by them: the hand by the factor its coded rule names in the table of the file the plot links, the link origin by keyRules.linkOrigin.byCode, the ramp class per code, the yard per zone, the yard outline of keyRules.yardOutline.groups, every link parameter, and places the insert, rotation and parameter overrides, those it can not place counted in overridesNotApplied. A rule a build can not give is refused. native2022 keeps the Round 17 tables | test_r337_a_build_of_a_profile_with_key_rules_takes_the_hands_origin_ramp_yard_and_parameters_from_them, test_r337_the_overrides_of_a_key_rules_build_..., test_r337_a_profile_with_no_key_rules_builds_by_the_round_17_tables_... |
| 2 | R337, R334 | The yard per zone name, a zone the table does not hold refused, Zone 1 marked notMeasured and named on every Zone 1 build | test_r337_a_zone_the_yard_table_does_not_hold_is_refused_and_a_zone_taken_from_another_is_said_on_every_build |
| 3 | R330, R336 | A kept entry of a plot past_all.csv gives no row of its key is dropped and listed, R336 holds the merged list on the join and against the dry list | test_r330_a_kept_entry_on_a_plot_the_join_gives_no_row_of_its_key_is_dropped_and_counted, test_r336_the_merged_list_is_held_..., test_r336_main_holds_the_merged_list_... |
| 4 | R330 | majority.overrideKeep in config: native whole, the parsons laterFields entryM, heightM, steps and parameter RRE_ZZ_SP_Prototype-FFL left to 3c, so the plain command gives the committed list | test_r330_the_config_keeps_every_native_entry_and_the_level_fields_left_to_3c_..., test_r330_a_run_merges_with_the_earlier_list_by_the_config_keep_... |
| 5 | R336 | Every decision held under its coded candidate, a candidate or decision with no route a failure, exit 1, the levels of laterFields listed | test_r336_any_coded_candidate_is_held_..., test_r336_a_decision_whose_coded_candidate_has_no_route_fails_the_run_... |
| 6 | R336 | The link fits, yard outlines and clusters made again from the reads, plans, blocks.json and analysis.json | test_r336_the_link_fit_per_code_is_made_again_..., test_r336_the_yard_outline_of_a_group_is_made_again_..., test_r336_the_clusters_are_made_again_..., test_r336_the_area_two_rings_share_is_exact_... |
| 7 | R280 | Two overrides of one plot and field refused with both keys and models, --override-model narrows | test_r280_two_overrides_of_one_plot_and_field_from_two_keys_or_one_are_refused_... |
| 8 | R330 | A key with misses and no --model refused | test_r330_a_key_with_no_model_file_is_refused_... |
| 9 | R330 | --keep of a source or field the earlier list has no entry of refused | test_r330_keep_with_a_field_or_a_source_no_entry_of_the_earlier_list_has_is_refused |
| 10 | R281 | A decision with misses and no entry in the list is no match | test_r281_a_decision_with_misses_and_no_entry_in_the_override_list_is_not_a_match |
| 11 | R330 | Native entries stay unless --replace native, an entry marked "byHand": true never dropped, a run that would drop one refused, --dry outside the repo only | test_r330_native_entries_stay_unless_replace_names_them_..., test_r330_an_entry_marked_by_hand_is_never_dropped_..., test_r330_by_hand_is_true_or_left_out, test_r330_a_dry_run_writes_... |
| 12 | R272, R331, R335 | coverage of every decision by key and plots, a decision with no plot listed, a key of answerKeys with no rows, read or plan refused unless --allow-missing-key, a group read without its reference listed, a key with no plan counted as none | test_r272_every_decision_reports_its_coverage_..., test_r272_a_key_of_answer_keys_missing_..., test_r331_a_group_whose_reference_read_is_missing_... |
| 13 | R335 | A wall whose base constraint no floor names has no base and is listed in wallsNoBaseLevel, 0 walls on the keys | test_r335_a_wall_whose_base_level_no_floor_of_its_plan_names_... |
| 14 | R335, R336 | majority.check.group, majority.linkPlotParameter and frontOpen in kindLeaveOut.SE in config, no SE, parsons or plot parameter literal left in the code | test_r335_the_link_plot_parameter_and_the_kinds_with_no_front_wall_come_from_config |

Found in the review and fixed: R271 fronted only the plots of past_all.csv (conflict 8), a test first (test_r271_a_plot_no_key_holds_keeps_its_front_...). The second method summed areas and centres on world coordinates and put the centre of IMD 1483 0.575 m off, it sums from the first vertex now.

How Bader marks a hand edit: add "byHand": true at the end of the line of the entry in config/overrides/roshn-makkah.json. A rerun keeps such an entry cell for cell when its field is kept and refuses to run when it would replace or drop it, and the loader refuses byHand of any other value.

The write command, the record of the list (RAN, round_addin\logs\p3a_fix_run_majority.sh with --overrides config/overrides/roshn-makkah.json and no other flag, log p3a_fix_write.txt): python/majority.py round_addin\past_all.csv with --plots-csv, --baseplan, --roads, --analysis, --footprint-blocks and --sales of the round_17 snapshot, --key-read of the 7 Parsons reads and the 4 Native reads, --key-plan of the 7 key plans, --model of the 11 keys and --out rules3a\fix_write\majority.json. No --keep: majority.overrideKeep keeps native whole and the four level fields left to 3c. The dry run is the same with --dry and --overrides rules3a\fix\overrides_dry.json (p3a_fix_dry.txt), its majority.json equal to the write run on every decision and extra (RAN, p3a_fix_dry_write_compare.txt).

| List | Total | Native | Parsons | Parsons fields that moved |
|---|---|---|---|---|
| Head of the first push, 351c2ac | 9,351 | 5,222 | 4,129 | entryM 241, heightM 32, mirrored 59 |
| Review rerun | 9,336 | 5,222 | 4,114 | entryM 230, heightM 30, mirrored 57 |

(RAN, p3a_fix_list_compare.txt.) Gone: the 13 Round 17 IMC entries of the cut plots 2312, 2375 to 2378, 2387 to 2390, 2544 and 2545 (11 entryM, 2 heightM, 2377 and 2388), listed in droppedNoJoinEntries, and the lone duplex hands IMB 1993 and 1994, which the site view holds. New: none. The native entries are equal cell for cell, the note now says how to mark a hand edit.

Item 1, the build against the keys by the probe route (RAN, p3a_fix_build_probe.py and .txt, rules3a\fix\build_probe.json): majority.py run with every input and its run step stubbed, each decision of a build made again on its context, before by the code of 351c2ac with the list of 351c2ac, after by the functions plan_from_book.py calls with the new list. Unexplained differences, a key value the build does not give with no override of that key, plot and field:

| Key | ima | imb | imc | imd | ime | imf | img |
|---|---|---|---|---|---|---|---|
| Hands before | 20 | 13 | 1 | 25 | 28 | 32 | 21 |
| Every decision before | 417 | 240 | 1,593 | 2,126 | 1,753 | 2,412 | 1,692 |
| Every decision after | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

Before, besides the 140 hands: 268 IMG yards, 183 origins and 139 rotations no override names, 18 TH1-IM Prototype values, and the parameters the head did not write. After, every hand, origin, rotation, ramp class, yard and parameter holds, the outline and generic floor misses are the 56, 56 and 23 overrides the build counts and can not place, and 2 origins and 2 rotations hold overrides with no gap. The SE keys by the ARC file of the code a build links, not the STR file they link: 6 hands differ (IMA 3, IMB 3). The dummy row of the R337 tests builds the same way in the repo.

Second method (RAN, p3a_fix_check.txt, rules3a\fix\key_rules_check.json), 24 decisions under their coded candidates, the link fits (15 codes), yard outlines (102 groups) and clusters (568) made again by the own route:

| Run | Seed 14 | Seed 7 | Every plot | Walls 14, 7, every |
|---|---|---|---|---|
| First push, c4 | 0 mismatches | 0 | 6 mismatches, 6 hands | 0, 0, 0 |
| Review | 389 items, 0 mismatches | 303 items, 0 | 43,957 items, 3 mismatches | 60, 60, 14,703 items, 0 |

New decisions held by the review: frontCorner 937, generic 2,501, rampFront 1,791, rampFrontFall 1,791. Left for 3c: entry, height, steps. cannotCheck none. Coordinates 7 of 7 and 4 of 4, the counts of 30 of 30 decisions sum per key. The merged list: 9,336 entries, 0 off the join, the run entries equal the dry list.

Item 17, what each of the 6 hands of the first push is (RAN, p3a_fix_six_hands.py, p3a_fix_six_hands_b.py and .txt):
- IMC 2301 (VD4), IME 2710 (VL6) and IMG 441 (VD4): villas with three road edges, 3 streets in the sales sheet, the front, one side and the back. The side road of R271 is the first other road edge in the order each route walks the edges, a side edge in the first method (left, right, left), the back in R336, so blockMirrorRoadSide reads another value. They are the 3 mismatches of the review (Q072).
- IMB 1993 and 1994 (DP4 in no cluster): plots with a road at the front and the back, 2 streets, the front by the Orientation rule. Their row runs into the cut face IMB 1995, 6 m wide, which no key holds, so the first method gave it no front and the row pairs stopped there, the own route fronted it. Since the review both front it (R271), both hold them and their 2 overrides are gone.
- IMD 1483 (DP2 in no cluster): a road at the front and along its right side, 2 streets. Its back neighbour 1518 shares its whole back edge, 13.249 m, square behind it, 0.000 m along its front, so left or right is the sign of a zero. The own route summed the centre on world coordinates and put it 0.575 m off, so 1518 fell left. Fixed, both put 1518 right and agree.

Item 18: the foundation probe and the C10 counts are saved now (R335 and R332 above). Item 20: the hashes after the last run of this pass are in the Gate of the PR body.

### Last pass on PR 47

The proof that a build reproduces the keys had two tries in the review, both by the probe route: majority.py run with its run step stubbed and each decision of a build made again on its context, the hand looked up in the table of the file the key links. The breaker ran the build's own lookups (hunt2 build_factors.py) and found 6 SE hands that route never saw. This pass does not patch the probe: python/build_proof.py gives plan_from_book.py the plots of each key and holds what the build writes. Every commit is new, no amend, each code fix with its test run first and seen failing for the right reason, the output with the head in round_addin\logs. No Revit or Civil 3D window, the lock not taken, the registry not read or written. CLAUDE.md was not changed, it names neither python/build_proof.py, python/key_rules_block.py nor R338 yet.

| Item | Commit | Rules | Change | Test, first run |
|---|---|---|---|---|
| B | 4c724e2 | R337 | Number-of-Steps and the steps of RVT-Name come from the stepBands of the level profile, labelled levelProfileStepBands, not keyRules, steps of the summary and the print name them | test_r337_the_steps_parameters_are_labelled_..., p3a_last_b_first.txt, 1 failed |
| C | a495202 | R337, R280 | Each plot holds overrides.applied or overrides.notApplied with why, in the missing entry for a plot not written, applied counts only written plots, appliedOutsideBuild and overridesNotAppliedWhy, the print names entryM and heightM applied apart from the fields kept for 3c | test_r337_a_build_says_plot_by_plot_..., p3a_last_c_first.txt, KeyError overrides |
| E | a60f068 | R337 | keyRules.linkRotation nearestCurb refused, El @ Entry slope and ramp class compared per code of keyRules.rampSlope and refused when they differ | test_r337_a_link_rotation_by_the_edge_nearest_a_curb_..., test_r337_the_slope_of_el_at_entry_..., p3a_last_e_first.txt, 2 failed |
| F | 1303592 | R330, R336 | majority.py --keep-from reads the byHand entries of the list it writes over, a conflict refused, override_source.by_hand refuses any other value or spelling, key_rules_check.py --merged needs --earlier and holds every kept and byHand entry, a failed check ends the run with 1 | test_r330_a_by_hand_entry_of_the_list_..., test_r330_a_by_hand_mark_of_another_spelling_..., test_r336_the_merged_list_is_held_against_the_earlier_list_..., the main test of test_key_rules_check.py, p3a_last_f_first.txt, 4 failed |
| D | 517beec | R338 new | python/key_rules_block.py writes keyRules, the note, the zone prefix, the zone taken from others and the Round 17 shares from majority.keyRulesBlock, a zone taken from zones that give two values refused | test_r338_the_key_rules_block_takes_..., test_r338_a_zone_taken_from_zones_that_differ_..., p3a_last_d_first.txt, ModuleNotFoundError |
| A | 44923c8 | R337 | python/build_proof.py covers and compare, majority.kindDiscipline (SE links STR) and majority.decidedByQuestion (Q017) in config | python/tests/test_build_proof.py, 8 tests, p3a_last_r337_proof_first.txt, 7 failed on 0589a6e, p3a_last_r337_follow_first.txt, 1 failed |
| A | 8e3cf0d | R333, R337, R272, R336 | The coded hand, learned per file the key links, judged by the table of the file a build links (byBuild), a plot and field an open question decides listed apart, the second method holds hands by the file a build links found by its own route, keyRules writes byBuild | test_r337_a_hand_is_coded_by_the_tables_..., test_r272_a_miss_of_a_plot_and_field_an_open_question_decides_..., test_r336_the_own_route_finds_the_file_..., p3a_last_a2_first.txt, 3 failed, then 1 for byBuild |
| | 06e41bd | | The override list and keyRules of the rerun | |

D, the writer: the script of the review, round_addin\logs\p3a_fix_key_rules_block.py, run with --all on a copy of the profile and the review's majority.json (rules3a\fix_write) gave the committed profile byte for byte, and so did python/key_rules_block.py on the repo profile (RAN, p3a_last_d_rerun.txt, sha256 235dc5fd... before and after). On the majority.json of this pass the writer changed 3 lines, the three hand decisions, and a rerun gave the same bytes (RAN, p3a_last_key_rules_write.txt, sha256 e2b08e05...).

A, how a build was proved. The review's probe route held every plot by the link file of its key, so an SE plot was looked up in the table of the STR file its key links, while a build links the ARC file of the code (READ, plan_from_book.folder_files of Prototypes_2024). The new route: build_proof.py covers writes one cover per key from past_all.csv, plan_from_book.py builds each key with --overrides, --sales, --footprint-blocks and --analysis of the snapshot, the LandXML surfaces of the snapshot and the site report testhost_I17_site.json, and build_proof.py compare holds the plan field by field against past_all.csv and the key plan (R337), logs p3a_last_build_route.sh. Before is plan_from_book.py at 0589a6e with the list of 0589a6e (rules3a\last\before, p3a_last_proof_before.txt, p3a_last_proof_before_note.txt), after is 8e3cf0d with the rerun list (rules3a\last\after, p3a_last_proof_after.txt). Summary: p3a_last_proof_summary.txt.

| Unexplained differences | ima | imb | imc | imd | ime | imf | img |
|---|---|---|---|---|---|---|---|
| Before, fields | 5 | 3 | 0 | 0 | 0 | 0 | 0 |
| Before, plots | 3 | 3 | 0 | 0 | 0 | 0 | 0 |
| After | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

Before: IMA 1590 (DT1) and 1763 (DT3) and IMB 2008 (DP3), 2198 (VD1) and 2271 (DT3) not written, the table of the ARC file holds no value of their factor, and IMA 1809 (VL4) built True against False, its rotation and insert with it. The breaker's IMC 2302 and the three VD4 plots are Q017 below. After, per field over the 7 keys (RAN): prototype file 1,756 equal, 707 the STR file of the code on the SE keys (majority.kindDiscipline), 3 Q017, hand, rotation, insert and CASE 2,465 hold and 1 Q017 (IMC 2302), yard outline 1,445, 56 overrides the build can not place, 1 Q017, ramp class 1,759 and yard 1,759 hold, ramp outline 1,714 and 45 overrides, generic floor 2,446 and 20 overrides, the other parameters hold, Number-of-Steps 1,595 and RVT-Name 1,599 hold with the steps the build computes, 164 and 160 differ by those steps alone, left to 3c (B). The earlier proof held them with the key's own steps. 35 plots are not written: 33 with no road level at a front corner, so no FFL (3c), IMA 1553, IMB 2241, 2272, IMD 1172, 1491, 1492, IME 2626, 2665, 2699, IMF 14 and IMG 10, and IME 2690 and IMG 622, C10 faces with no front frame whose insert and rotation overrides hold no gap. The build route does not hold the link fields of these 35.

Second way for the route (RAN, p3a_last_proof_second.py and .txt, json, csv and math only): every written plot against past_all.csv by file name and code, mirror flag, rotation within 0.1 degree and insert within 10 mm. All agree but IMC 2301, 2302 and IMG 544 by file (VD4 against DP4) and IMC 2302 by hand, rotation and insert, the Q017 plots. 61 of the 63 parsons hand overrides are applied, IME 2690 and IMG 622 are not written.

The majority rule on what the route shows: majority.py codes each hand as before, learned per file the key links, and judges the coded hand again by the table of the file a build links (R333, byBuild): hand villa 0.960 measured, 0.956 by the build, 34 misses, cluster 0.981 both, alone 1.0 measured, 0.990 by the build, 4 misses. Judging by the file a build links in the choice of the candidate as well was tried first (p3a_last_hand_scope_probe.py and .txt): it coded blockMirror for the villas instead of blockMirrorRoadSide and moved other plots, so the choice stays on the measure. Its misses are the plots a build does not reproduce, each an override from the key: IMA 1590, 1763, 1809, IMB 2008, 2198, 2271. The 11 SE hand overrides of the review stay with the file a build links in their reason. The prototype file is a decision of the route tally only, majority.py codes none (CHOSEN): the file of the Rev 9 code on 1,756 of 1,759 written TU plots.

Q017: IMC 2301, 2302 and IMG 544 are VD4 by Rev 9, their keys link the DP4 file, and their RRE_ZZ_SP_Prototype parameter says VD4 (READ, the override list). A build links VD4, the open question decides it, so majority.decidedByQuestion lists the three for the file and the hand and no override is written: IMC 2302, whose hand by the VD4 table differs from its key, is in decidedByQuestion of the report, IMC 2301 and IMG 544 hold their hands. The route counts their file, and on IMC 2302 the hand, rotation, insert, yard outline and CASE that follow from it, as question.

Override list (RAN, p3a_last_write.txt, p3a_last_list_compare.txt): the write command of the review at 8e3cf0d, 9,336 to 9,342, parsons 4,114 to 4,120 (mirrored 57 to 63), native 5,222 cell for cell, 0 byHand. The dry run at the same head equals the write run on every decision (p3a_last_dry2.txt, p3a_last_dry_write_compare.txt).

| List | Total | Native | Parsons |
|---|---|---|---|
| 0589a6e | 9,336 | 5,222 | 4,114 |
| 06e41bd | 9,342 | 5,222 | 4,120 |

Second method (RAN, p3a_last_check.txt, rules3a\last\write\key_rules_check.json, --merged config/overrides and --earlier the list before the run): seed 14 389 items and seed 7 303, 0 mismatches each, every plot 43,957 items, 2 mismatches, IME 2710 and IMG 441, the villas of Q072, walls 60, 60 and 14,703 items, 0. IMC 2301 now agrees: by the VD4 table both side roads give its key hand. The merged list: 9,342 entries, 0 off the join, run entries equal the dry list, 5,482 kept entries of the earlier list cell for cell, 0 byHand lost, ok.

C, on the IMC build (RAN, p3a_last_proof_after.txt): applied on written plots entryM 230, heightM 30, insert 4, mirrored 11, CASE 206, foundation type 150, RVT-Name 5, rampFrontM 80, rampType 10, left with why rampFrontFallM 90 (Q057), yard outline 14, generic floor and ramp outline 4 each. The print says entryM and heightM, kept in the list for 3c, are applied, the Round 17 gaps of R280.

Hashes (RAN, p3a_last_hash.sh): 60 of 60 originals and 99 of 99 inputs equal before the first edit and after the last run, the 80 of the review and the 18 LandXML surfaces of the snapshot and the site report a build reads.

Not done: the link fields of the 35 plots a build does not write are not held by the route. majority.py has no prototype file decision. The levels stay 3c.

### Questions

- Q040 closed: keep the footprint clusters for the cluster side, 1,297 of 1,322 on the 7 keys against 1,173 with the analysis clusters alone.
- Q071 new: walls, see Bader's turn.
- Q002, Q003, Q015, Q037, Q042 and Q057 take the counts of the 7 keys, Q003 and Q042 a changed rule.
- Review: Q015, Q042, Q053 and Q066 take the counts of the rerun, Q072 is new (the side road of a villa with three road edges).
- Last pass: Q017 takes IMG 544 and the counts of the route, Q053 the 9,342 entries, Q066 the 6 SE hands as overrides.

