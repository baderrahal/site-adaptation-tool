## Phase 3 part 3, PR 3c rules/addin-parsons-levels

Branch rules/addin-parsons-levels from main at cbfa90a, equal to origin/main after git fetch (RAN). Rules R339 to R341 are new, R338 writes the level decisions too. Four changes: R339 the levels measured as a build gives them (ec208f1), R340 a build and its proof by them (08cb2b4), R341 their second method (2d9606c), and the data and records (the override list, keyRules by key_rules_block.py, majority.overrideKeep, this section). The phase ran in a helper agent started by the main session, which runs the checkers and merges. No Revit or Civil 3D window was opened, the lock was not taken, the registry was not read or written. Values of levels stay in Documents\SiteAdaptation\round_addin\rules3c, this file holds counts, shares, gaps and plot numbers only.

### Originals and inputs (R201, R202)

- 60 originals and 99 inputs hashed with sha256sum before the first edit and after the last run, 60 of 60 and 99 of 99 equal, and equal to the after lists of the last pass on PR 47 (RAN, round_addin\logs\p3c_hash.sh, originals_p3c_before.txt and _after.txt, inputs_p3c_before.txt and _after.txt). The 99 hold plan_imc_past.json, round_17\past_all.csv, the 2a reads, the 2b outputs and the LandXML surfaces. config/overrides/roshn-makkah.json, config/project.json and rules/roshn-makkah.json changed by this PR only (protected_p3c_before.txt, _after.txt).
- The plans of main, built again at cbfa90a by the build route of 3a (rules3c\before, p3c_proof_before_main.txt), equal the plans of the last pass on PR 47 byte for byte, 7 of 7 (RAN cmp).

### R339, the levels measured as a build gives them (change 1)

- The road: majority.py --landxml --scratch asks the road surfaces of siteBuild.roadSurfaces at the two front corners of R226 as plan_from_book.fronts does, so a gap is measured against the very value a build adds it to. plots.csv holds the road of the R157 front at 4 decimals, the build route differs from it by 0.05 to 0.5 mm at a corner on 541 of the 2,463 parsons plots both give, by up to 1 mm on 1 more, and by a whole other edge on 32 corner plots, the orientation front (RAN, round_addin\logs\p3c_probe_road.txt on the context of p3c_probe_harness.py).
- Pass limit majority.levelPassMm 0.5 mm (CHOSEN): a level within half a mm of the key rounds to the same mm, so an FFL, written to the mm, is equal, the "to the mm" of the brief. The shares at majority.levelBandsMm 1 and 5 mm are given beside, 5 mm being the levelHoldMm Round 17 held the levels by. placerCheck.level.passMm stays 1.0 mm.
- The candidates of every other road surface and of a datum are checks for El @ Entry and the ramp front, since a build takes the road of frontRoad of its level profile and no other.
- Chained on what a build holds: a held El @ Entry keeps the rule, a miss the key, and the FFL, steps and Prototype-FFL are chosen on the key and judged on the levels a build holds (byBuild), so the misses are the plots a build does not reproduce.
- The FFL of a piece is rounded to the mm first (R67), the plan of 3a rounded it after the height. A piece is a cluster cut by the split table, or a unit in no cluster with its row pair partner (plan_rules.row_partners): the DP2 and DP4 units with villa blocks pair in the keys as R333 pairs their hands.
- Steps: the keys count one step for the base and one per riser over it, half a riser no step: the 9 key plots half a riser over their base have 1 step, the first 2 steps over half a riser lie at 0.529 riser (RAN, p3c_probe_steps.txt). Round half up had 9 more misses, the round of 3a counted the same by its even rounding.
- Prototype-FFL is the FFL down to the cm written with 3 decimals, 1,780 of 1,791 on the key FFL (RAN, write run).
- IMC first (RAN, p3c_imc_dry.txt, the IMC rows alone): El @ Entry 0.062 at the pass limit, 0.096 at 1 mm, 0.290 at 5 mm, split 0.9375 (15 of 16 clusters) and base 0.9167 strong on IMC alone, both weak on the 5 TU keys (0.702 and 0.844), FFL 0.818, 0.886 and 0.978, byBuild 0.787, steps by the step bands 1.0 on IMC, Prototype-FFL 0.985.

### Every level decision of 3c

Write run (RAN, round_addin\logs\p3c_write.txt, rules3c\write\majority.json, the summary p3c_levels_summary.txt), the byBuild of the FFL and the overrides from the rerun of the review of PR 48 (p3c_fix_write.txt). Share over the 5 TU keys at the pass limit, per key (c d e f g), at 1 mm and 5 mm, the Native share of this run beside (the FFL beside the Native FFL less El @ Entry of R275), strong at 0.90, overrides written and misses with no value listed in cannot. The SE keys hold no level (kindLeaveOut).

| Decision | Coded rule | Pass 0.5 mm | Per key c d e f g | 1 mm | 5 mm | byBuild | Native | | Overrides |
|---|---|---|---|---|---|---|---|---|---|
| El @ Entry | road a build gives + slope x depth | 0.088 | 0.062 0.087 0.097 0.053 0.172 | 0.146 | 0.442 | | 0.257 | weak | 1,596 entryM, 32 cannot |
| Ramp front | road a build gives | 0.692 | 0.651 0.610 0.756 0.742 0.701 | 0.735 | 0.801 | | 0.634 | weak | 519 rampFrontM, 32 cannot |
| Split by units | the plurality per unit sequence, DP1 ends middle, DP3 ends none | 0.702 of 84 clusters | 0.938 0.900 0.917 0.391 0.462 | | | | none | weak | none, levelMisses 25 |
| Base height | steps.base_height of the unit | 0.844 of 1,122 pieces | 0.917 0.851 0.848 0.746 0.906 | 0.849 | 0.891 | | none | weak | none, levelMisses 175 |
| FFL per cluster | one FFL per piece at the highest El @ Entry + base, to the mm | 0.749 | 0.818 0.787 0.778 0.622 0.802 | 0.781 | 0.826 | 0.121 on the El @ Entry of the rule | 0.353 | weak | 1,547 heightM, 27 cannot |
| Steps | base and risers, half a riser no step | 0.988 | 0.985 0.985 0.994 0.981 1.0 | a count | a count | 0.973 | none | strong | 48 steps |
| SSL | FFL + levels.ground_ssl | no key holds SSL | | | | | | not measured | none |
| Prototype-FFL | FFL down to the cm | 0.994 | 0.985 0.990 0.994 1.0 1.0 | 0.994 | 0.995 | 0.979 | none | strong | 11 parameter, 27 cannot |

- Beside the coded rules: one FFL per cluster whole, R278 of 3a, 0.696, each plot alone 0.564, the step bands of R67 0.934 (1.0 on IMC and IMD, the schedules), the split none for every cluster 0.500. The misses of byBuild hold 27 to 32 plots with no road level at a front corner or no front frame, listed in cannot with no value since the review of PR 48. The steps: 48 overrides, 16 that break stepsFromHeight and 32 with no value of it, the 30 TU plots with no road level and IME 2690 and IMG 622 with no front frame, written with the steps of the key (Q074).
- The split by units: IMC, IMD and IME split DP1 TH1 TH1 DP1 and DP1 TH3 TH3 DP1 at the middle wall on 44 of 48 clusters, IMF and IMG split them at an end or not at all, 15 of 36 (RAN). Not by zone, IME and IMF are both Zone 3 (Q012). Per unit sequence, keyRules.levelSplit.bySequence (RAN, p3c_fix_key_rules_write.txt): DP1 TH1 TH1 DP1 (M07) middle on 17 of 37 clusters, none 7, other walls 13, weak, DP1 TH1-IM TH1-IM DP1 middle 3 of 6, weak, DP1 TH3 TH3 DP1 middle 6 of 6, strong, DP3 TH3 TH3 DP3 none 33 of 35, strong, the plurality coded for each.
- The FFL misses on IME, IMF and IMG: the plot that sets an FFL of the key stands more than 5 mm over its base above its ELl@Entry parameter on 113 FFL groups, IME 30, IMF 68, IMG 15, from 0.01 to 0.37 m and a whole number of cm on 107 of them, so the parameter is not the level the FFL was set on (Q070). On IMC and IMD every such plot stands within 5 mm of its base (RAN, p3c_probe_tops.txt). There the unrounded rule lies within half a mm of the key FFL, rounded to nearest, on 400 of their 459 pieces, 50 set by another plot than the highest, 6 more than 5 mm off and 3 typed (RAN, p3c_probe_setter.txt).
- keyRules written by python/key_rules_block.py through round_addin\logs\p3c_key_rules_write.py (RAN, p3c_key_rules_write.txt): 34 decisions, the 28 of 3a in their order and entry, levelSplit, baseHeight, ffl, steps and ssl after them. Of the 28 only rampFront, now on the road a build gives at 0.5 mm, and Prototype-FFL, now fflDownToTheCm, changed, the other 26 kept their bytes (RAN, git diff). The dry run equals the write run on every decision and extra (RAN, p3c_dry_write_compare.txt).

### The cause per plot of the Round 17 levels within 5 mm but not 0.5 mm

Round 17 built IMC (plan_imc_r17b.json) with every FFL within 5 mm of the key, 103 plots over 0.5 mm and up to 5 mm: 42 at 1.0 mm or less, 61 over 1 mm, 21 at 2, 19 at 3, 12 at 4, 9 at 5 (RAN, round_addin\logs\p3c_r17_causes.py and .txt, rules3c\r17_causes.json). The cause of each, the first that holds:

| Cause | Plots | Fixed in the rule or listed |
|---|---|---|
| El @ Entry of the plot or of the plot of its cluster that sets the FFL held within the 5 mm hold, so no override moved it | 61 | fixed: the pass limit 0.5 mm, the FFL judged on the El @ Entry a build holds |
| The road snap: the gap measured on the plots.csv road, 0.05 to 0.5 mm off the road the build asks | 26 | fixed: the gap on the road a build gives, El @ Entry at 4 decimals |
| Another plot of the cluster than the highest sets the FFL in the sheet | 10 | listed: heightM overrides, typed choice of the sheet |
| Half a mm: the key FFL rounds a decimal the 4 decimals of El @ Entry do not hold | 3 | listed: heightM overrides |
| Rounding order: the FFL of the piece rounded to the mm first gives the key | 2 | fixed: rounded to the mm first (R67) |
| Typed: none of these | 1 | listed |

No datum is a cause: the parsons keys hold their levels in shared elevation, no datum (R263, READ config placerCheck.dialects.parsons).

### R340, the build and the proof (change 2)

The build route of 3a with --key-read for the level parameters (RAN, round_addin\logs\p3c_build_route.sh): build_proof.py covers, plan_from_book.py per key with the override list, build_proof.py compare. Before is the plans of main judged by the new compare with the list of main (rules3c\before, p3c_proof_before.txt), after the new build with the new list and keyRules (rules3c\after, p3c_proof_after.txt). Unexplained level differences per key, at the pass limit, 1 mm and 5 mm:

| Unexplained levels | ima | imb | imc | imd | ime | imf | img |
|---|---|---|---|---|---|---|---|
| Before, pass | 0 | 0 | 604 | 1,389 | 1,071 | 1,686 | 813 |
| Before, 1 mm | 0 | 0 | 528 | 1,302 | 981 | 1,592 | 753 |
| Before, 5 mm | 0 | 0 | 325 | 1,122 | 772 | 1,269 | 592 |
| After, pass, 1 mm and 5 mm | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

- Before, IMC: El @ Entry 74, FFL 103, height 73, ramp front 30 and Prototype-FFL 324, no value built. The SE keys hold no level. Every other field of the 3a proof holds as before.
- After: 10,542 level verdicts hold on the 1,759 written TU plots, El @ Entry and the height on 1,753, the 6 IMF placeholders hold no key value. The review of PR 48 splits them by the rule alone and by an override (below). Number-of-Steps and RVT-Name, 164 and 160 left to 3c by the steps alone, hold on all 1,759. Not written: 33 plots for want of a road level at a front corner (IMA 1553, IMB 2241, 2272, IMD 1172, 1491, 1492, IME 2626, 2665, 2699, IMF 14, IMG 10), explained by that input (Q073), and IME 2690 and IMG 622 with no front frame, their insert and rotation overrides with no gap.
- The overrides placed: every entryM, heightM, steps and Prototype-FFL entry of a written plot moves its level onto the key within 0.05 mm, El @ Entry rounded to 4 decimals after its gap.

### R341, the second method (change 3)

key_rules_check.py with --landxml (RAN, round_addin\logs\p3c_run_check.sh, p3c_check.txt, rules3c\write\key_rules_check.json, --merged config/overrides and --earlier the list before the run): the road surfaces read again from the LandXML text into an own grid, the own front of R226, the own clusters and row pairs.

| Run | Seed 14 | Seed 7 | Every plot | Walls 14, 7, every |
|---|---|---|---|---|
| Items, of them levels | 450, 95 | 343, 62 | 50,529, 10,154 | 60, 60, 14,703 |
| Mismatches | 0 | 0 | 2, IME 2710 and IMG 441 (Q072), no level | 0 |

- Every plot, each level matched: El @ Entry 1,785, ramp front 1,791, FFL 1,791, steps 1,791, Prototype-FFL 1,791, split 84 clusters, base 1,121 pieces. The own road reached 2,543 plots, 33 with no road level. The merged list: 11,528 entries, 0 off the join, run entries equal the dry list, 5,222 kept native entries cell for cell, 0 byHand lost, ok. The list held no byHand entry, so 0 byHand lost holds by having none and the hand edit check was not exercised on real data.
- The first run found the misses of the split and the base read from the dry list only, 25 and 174 mismatches, fixed by reading levelMisses of the report, before the commit.
- Conflict listed, not resolved: the base is held on 1,122 pieces by the first method and 1,121 by the second. IMF 1119 and 1100 share 12.599 m of edge by the buffer of the first method and 12.499 m by the edges of the second, against the 12.5 m of sideShare, so the first finds no row pair for IMF 1118 and 1119 and the second pairs 1118 with 1119 (RAN, p3c_probe_partners.txt). Since the review of PR 48 the base item of IMF 1118, which the first method alone holds, is a mismatch of every plot.

### The override list (R330)

| List | Total | Native | Parsons | Parsons level fields |
|---|---|---|---|---|
| main, 06e41bd | 9,342 | 5,222 | 4,120 | entryM 230, heightM 30, rampFrontM 343, kept for 3c |
| 3c | 11,528 | 5,222 | 6,306 | entryM 1,628, heightM 524, steps 48, Prototype-FFL 38, rampFrontM 551 |

(RAN, p3c_overrides_before.txt, p3c_overrides_after.txt, p3c_write.txt.) majority.overrideKeep.laterFields is empty since 3c measured the levels, so the plain write command (p3c_run_majority.sh with --overrides config/overrides/roshn-makkah.json) writes them, native whole kept cell for cell, 0 byHand, on a list that holds no byHand entry. Per key: imc 838 to 994, imd 664 to 1,196, ime 444 to 866, imf 828 to 1,549, img 425 to 780, ima 518 and imb 403 unchanged. Every other parsons field equals the list of main. Of the 230 entryM of Round 17, 174 keep their value and 56 take a new gap to the road a build gives. Of its 30 heightM the list of e3c0456 dropped 21 and gave 9 a changed value, the rerun of the review holds all 30 with a changed value (RAN, p3c_fix_list_numbers.txt).

### Tests first

Each change ran its new tests first and saw them fail for the right reason, the output with the head in round_addin\logs: p3c_r339_first.txt (8 failed on the missing functions and config keys, then the keyRules block test, ProfileError of no decision ssl), p3c_r340_first.txt (5 failed, the build refused fflDownToTheCm, compare took no reads), p3c_r341_first.txt (3 failed, no OwnTin, level_verdicts or own_steps). The half riser test was changed from half up to half down after the measure on the keys, before the commit. The tests of the committed profile and list were added with the data.

### Not done in this PR

- El @ Entry stays weak (Q001), the split (Q012), the base and the FFL per piece stay weak on IMF and IMG, where the El @ Entry parameter is not the level the FFL was set on (Q070), each listed by its overrides.
- The 33 plots with no road level at a front corner are not written (Q073). SSL is in no key and not measured. The Native levels keep R275, their overrides are kept.
- CLAUDE.md was not changed: R339 to R341, python/key_levels.py and the --landxml of majority.py and key_rules_check.py are for the main session to name there.

### Review of PR 48

The breaker of PR 48 found 19 items. The fixes ran in this branch from e3c0456, each code fix with its test first, seen failing in round_addin\logs (p3c_fix_item1_first.txt to p3c_fix_items8to12_first.txt and p3c_fix_front_rule_first.txt), the tests in python/tests/test_addin_levels_review.py. No Revit or Civil 3D window, no registry. 60 of 60 originals and 99 of 99 inputs equal by hash before the first edit and after the last run (RAN, p3c_fix_hash.sh).

| Item | Change | Rule, commit |
|---|---|---|
| 1 | Each level of a plot stands alone: the FFL of a piece is taken on the El @ Entry the rule gives each of its plots, an entryM moves the El @ Entry of its own plot only, so a plot whose FFL another plot's El @ Entry sets gets its own heightM, the second method the same | R339 to R341, 8ec1bdd |
| 2 | A miss with no value is listed in the cannot list of the override file, never written with a null value, a build refuses an override with no value, the proof and the second method read the cannot list | R272, R280, R340, R341, 4123228 |
| 3 to 5 | The proof is refused without the plan, key plan and key read of every key, a level it could not read is notChecked and fails the run, keyHoldsNone counted apart, levelRule of a build gives the levels of the rule alone, levelHolds per key and level, the ramp front per corner left to Q057 | R340, ac4f90a, 1ded226 |
| 6 | A level item one method holds alone is a mismatch, levelItems of the report | R341, c05c177 |
| 7 | The split per unit sequence with its clusters, strong or weak | R338, R339, 24203ff |
| 8, 9, 10, 12 | One road setting for a build and the measure, one FFL per piece needs levelSplit and baseHeight, a steps override is a whole number, the print names keyRules.steps | R280, R339 to R341, c7c80ea |

- Item 1, design: the FFL rule is judged on the El @ Entry of the rule for every plot of the piece, never on the El @ Entry an override gives (CHOSEN). An own entryM moving the own FFL would leave two FFLs in one piece once one line is edited, so no entryM moves an FFL. The cost is the heightM of every plot whose own El @ Entry set its FFL before: heightM 524 to 1,547, 476 of the 497 with a value kept, 1,071 new, 1,070 of them on plots with an entryM of their own and 1 with none, 21 gone (RAN, p3c_fix_list_numbers.txt). On the real case, the entryM line of IMC 2291 deleted and IMC built again: 324 of 324 plots, only the El @ Entry and the height of 2291 differ (RAN, p3c_fix_probe_cut.txt).
- Item 12: the laterPhase reason of build_proof.py is not dead, a keyRules with no level rules builds the steps by the step bands and test_build_proof.py holds it, so the line stays and no longer names 3c.
- Rerun (RAN, p3c_fix_dry.txt, p3c_fix_write.txt at c7c80ea, p3c_fix_key_rules_write.txt): the dry run equals the write run on 49 of 49 decisions and every extra (p3c_fix_dry_write_compare.txt). keyRules changed in ffl, byBuild 0.121 on the El @ Entry of the rule and misses 1,574, and levelSplit, bySequence added.

The override list (RAN, p3c_fix_list_numbers.txt):

| List | Total | Native | Parsons | Cannot | Parsons level fields |
|---|---|---|---|---|---|
| 3c, e3c0456 | 11,528 | 5,222 | 6,306 | none | entryM 1,628, heightM 524, steps 48, Prototype-FFL 38, rampFrontM 551, of them 118 with no value |
| review | 12,424 | 5,222 | 7,202 | 154 parsons | entryM 1,596, heightM 1,547, steps 48, Prototype-FFL 11, rampFrontM 519 |

The 154 of cannot: entryM 32, heightM 27, Prototype-FFL 27, rampFrontM 32, rampFrontFallM 30, insert, rotationDeg and rampOutline 2 each. The native list keeps 1,346 entries with no value from Round 17 cell for cell (entryM 657, rampFrontM 657, rampFrontFallM 32), so a native2022 build that loads entryM or rampFrontM is refused (RAN, 1,314 refused, Q075).

Proof by the build itself (RAN, p3c_fix_proof_after.txt, p3c_fix_proof_levels.txt), the holds of the 1,759 written TU plots by the rule alone and by an override of their own plot:

| Level | imc | imd | ime | imf | img | All, rule share |
|---|---|---|---|---|---|---|
| El @ Entry, rule and override | 20, 304 | 35, 365 | 31, 285 | 25, 431 | 46, 211 | 157, 1,596, 0.090 |
| FFL | 24, 300 | 50, 350 | 49, 267 | 22, 440 | 72, 185 | 217, 1,542, 0.123 |
| Height | 164, 160 | 191, 209 | 184, 132 | 204, 252 | 152, 105 | 895, 858, 0.511 |
| Steps | 307, 17 | 393, 7 | 316, 0 | 425, 37 | 249, 8 | 1,690, 69, 0.961 |
| Ramp front, the mean | 214, 110 | 256, 144 | 250, 66 | 361, 101 | 202, 55 | 1,283, 476, 0.729 |
| Prototype-FFL | 78, 246 | 149, 251 | 143, 173 | 145, 317 | 128, 129 | 643, 1,116, 0.366 |
| Front corner, held and under Q057 | 18, 306 | 19, 381 | 19, 297 | 12, 450 | 26, 231 | 94, 1,665 |

- Misses 0, held by neither 0 and notChecked 0 on every key and level, the 0 unexplained of 3c stands. keyHoldsNone: IMF 6 plots, El @ Entry and height, and Prototype-FFL on the SE keys, IMA 419 and IMB 288, whose STR links hold none, all skipped in silence before.
- The weak rules hold on the keys through their overrides, as the majority rule says: El @ Entry holds by the rule alone on 157 of 1,753 plots, the FFL on 217 of 1,759, the ramp front on 1,283 of 1,759. The claim of every level covers the mean of the ramp front only.
- At the pass limit itself: El @ Entry and height 27 each, the ramp front 579. Every ramp front gap is 0.0 mm (1,180) or exactly 0.5 mm (579): the plan writes the front corners to the mm (R2) and the key front lies on a half mm as well, so the gap moves in half mm steps and a mean 0.5 mm off is one corner 1 mm off (RAN, p3c_fix_probe_front.txt).
- The front corners, each against the nearest key corner in plan, where the mean holds on all 1,759: within 0.5 mm 94, up to 1 mm 776, up to 5 mm 632, over 5 mm 257. Taken in the order of the two lowest key corners, not by place, 1,483 lie over 5 mm. The 1,185 of the breaker is reproduced by neither pairing, the pairing it used is UNKNOWN. The fall stays under Q057.

Second method (RAN, p3c_fix_check.txt): seed 14 450 items, 95 levels, 0 mismatches, seed 7 343, 62 levels, 0, every plot 50,530 items, 10,155 levels, 3 mismatches: IME 2710 and IMG 441 (Q072), no level, and the base of IMF 1118 that the first method alone holds (the conflict of IMF 1119 and 1100 above, 12.599 m against 12.499 m). Level items equal on both sides for El @ Entry 1,785, ramp front 1,791, split 84, FFL, steps and Prototype-FFL 1,791, base 1,122 against 1,121. Walls 0 mismatches. Merged list ok: 12,424 entries, run entries equal the dry list, cannot 154 equal the dry list, 5,222 native cell for cell, 0 byHand lost on a list that holds no byHand entry.

Not done: the full text of Q072 is not in steps/questions-closed.md, as before this PR. CLAUDE.md is not changed, the cannot list and test_addin_levels_review.py are for the main session to name there.

### Questions

- Q001, Q012, Q052, Q053, Q057, Q070 and Q074 take the counts of 3c and its review, the same text in steps/PROGRESS.md and the top entry of steps/log.md. Q073 (the plots with no road level) and Q074 (the steps of the base and risers against the count rule of the schedules) are new, Q075 (the native entries with no value) is new in the review.

