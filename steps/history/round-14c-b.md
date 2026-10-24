# Round 14c Part B, 2026-10-24, floors in the CAD plan

Full record of Part B. The top entry of steps/log.md holds the short form.

## Start

- Branch feat/cad-floors-14c from main at 2119fe9 after PR 24 was merged with both checks passed. The first worktree site-adaptation-tool-14c was gone when Part B began, removed outside this session together with the c3d worktree, while a new worktree site-adaptation-tool-data on data/data-book had appeared. Part A was pushed and merged before, nothing was lost. A fresh worktree with the same name was made from origin/main. The data worktree was not touched.

## B1 Generic and yard outlines (R135)

- Past outlines of pastJob.output in link axes, the 16 cut plots apart, split of R98 with seed 13.
- By unit and mirror, as asked: Generic fit 164 plots, within 1 mm 121, within 10 mm 134, worse 30, test 152, within 1 mm 93, within 10 mm 116, worse 36, 4 plots in small groups. Yard fit 165: 92, 127, 38, test 152: 71, 115, 37, 4 small.
- By unit, mirror and plot width (0.5 m step), used by the plan: Generic fit 148: 132, 147, 1, test 139: 112, 138, 1, 33 small. Yard fit 148: 107, 144, 4, test 139: 77, 135, 4 (VD2 yards 80 mm off), 34 small.
- The whole baseplan face against the Generic floor: 320 plots, within 1 mm 210, within 10 mm 319, worse 1 (2409, 44 mm).
- config/floor_outlines.json holds the groups with their loops in link axes, written by floor_fit.py --write.

## B2 Ramps (R136, R137)

- Slope along the depth per type: Ramps 0.5% 266 ramps, median 0.507 percent (0.489 to 1.957), Ramps 2% 45, median 1.993 (1.993 to 2.0), Flat Ramps 10, median 0.507 (0.489 to 1.993). Bands: Ramps 0.5% under 1.25 percent, Ramps 2% from there. Flat Ramps is not separable.
- The bands pick the past type: Ramps 0.5% 188 of 266, Ramps 2% 45 of 45, Flat Ramps 0 of 10, so 233 of 321.
- Typology, counted and not coded: Ramps 2% on C10 15 of 15, VL6 17 of 17, VL4 13 of 15, Ramps 0.5% on every other unit but the 8 Flat Ramps among them, 311 of 321.
- Front corner level: road level at the curb point plus 0.098 m, 640 corners, within 5 mm 234, within 30 mm 496.
- Back corners: both at El @ Entry put 6 of 436 past back corners within 5 mm and 36 of 218 ramps within 30 mm. Parallel to the front pair with the mean at El @ Entry: 192 of 436 back corners within 5 mm, and the ramps below. The plan uses the parallel edge, still from the level of R126.
- Past back edge: mean minus El @ Entry median 2 mm.

## B3 Floors in the plan and the compare (R137, R138)

- plan_imc_cad.json: 224 plots written, Generic 223, yard 223, ramp 224 (Ramps 0.5% 151, Ramps 2% 73). One DP4 on a 16 m face has no Generic or yard group.
- Against plan_imc_past.json, 218 plots outside the cut plots:
  - Generic: both 217, only in the past plan 1. Outline within 1 mm 125, within 10 mm 217, worse 0. Top within 5 mm 192, worse 25 (the clusters the schedule splits). Type equal 217
  - Yard: both 217, only in the past plan 1. Outline within 1 mm 71, within 10 mm 215, worse 2. Top within 5 mm 192, worse 25. Type equal 217
  - Ramp: both 218. Outline within 1 mm 162, within 10 mm 218. Corners within 5 mm 44, within 30 mm 164, worse 54. Type equal 140, worse 78
- Cut plots, 6 compared: Generic and ramp outline worse 6, ramp corners worse 6, ramp type equal 5, one yard only in the CAD plan.

## Second method (R139)

- fit: seed 14 40 of 40, seed 7 40 of 40, all 641 of 641. outlines: seed 14 20 of 20, seed 7 20 of 20, all 224 of 224. ramps: seed 14 20 of 20, seed 7 20 of 20, all 224 of 224. compare: 6 of 6 field counts.

## Gate

- Python 740 to 750 passed and 1 skipped, .NET 358 passed.
