# The 443 overlaps of two plots at step 5

Round FIX-3NOV-B task 3, Lane B. Measured, nothing fixed. The plan belongs to Lane A (steps/fix-2nov-b-handrun.md gives the count). NBH 012, the hand run on main a4551dd: warnings_5.html for the pairs, the sheet Placed for the id of each floor, plan_012.json for the outlines and heights. Scripts and pairs.csv in Documents\SiteAdaptation\lane_b_check\two_plot, outside git. Distances and counts only.

## Cause

Rounding of the plan to 1 mm on plots that stand at an angle. The floors of two neighbours meet along one line, and a yard or ramp corner meant to lie on that line lands up to 0.7 mm to either side of it, so a hairline lap appears. Revit shows it when the two floor solids also meet in height.

- No pair is a deep lap: 0 cross, 0 sit inside the other, the deepest is 1.5 mm.
- This is the cause the numbers mostly fit. Rounding is READ and the sizes are RAN. Two rounded corners give at most 1.41 mm, and the deepest reads 1.49 mm by corners, so a small part is not explained by this limit. That a plan with no rounding would be quiet in Revit is UNKNOWN, no run tried it.

## Numbers

All RAN unless tagged.

| What | Count |
|---|---|
| Rows of overlap in the box, distinct pairs | 443, 443 |
| Plots in a pair | 302 of the 336 plots of the plan |
| Sliver along an edge, depth under 1 cm | 371 |
| Edge only, 1 cm2 of area or less | 72 |
| Lap with depth of 1 cm or more, crossing, one inside the other | 0, 0, 0 |
| Depth under 1 mm, from 1 mm to 1.5 mm | 409, 34 |

| Measure | Median | 90th percentile | Largest | Sum |
|---|---|---|---|---|
| Area, m2 | 0.0007 | 0.0037 | 0.0193 | 0.667 |
| Depth, mm | 0.4 | 0.9 | 1.5 | |
| Length of the edge the two share, m | 5.5 | 16.5 | 19.5 | |

Which floor stands past its own generic, the plot face:

| Whose | Pairs |
|---|---|
| One of the two | 341 |
| Both | 22 |
| Neither, both inside their faces and touching | 77 |
| Two generics | 3 |

By floor pair: Generic with yard 204, Generic with Ramps 0.5% 115, Ramps 0.5% with yard 58, yard with yard 27, Generic with Ramps 2% 13, Ramps 0.5% with Ramps 0.5% 10, Flat with Generic 6, Ramps 2% with yard 4, Generic with Generic 3, Flat with yard 2, Ramps 0.5% with Ramps 2% 1. Row by row: pairs.csv.

## How the cause was reached

| Fact | Result | Tag |
|---|---|---|
| Points of the plan | 6,125 with 3 decimals, 78 with 2, none finer | RAN |
| Clipping of a yard | on whole millimetres | READ dotnet/SiteAdaptation.Common/Plan/FloorOutlines.cs:206 |
| Plots square to the axes | 0 of 336 | RAN |
| Yard corners out of their own generic | 488, median 0.36 mm, largest 1.34 mm | RAN |
| Ramp corners out of their own generic | 336, median 0.38 mm, largest 0.99 mm | RAN |
| Limit of two rounded corners | 1.41 mm, each corner on a 1 mm grid moves by at most 0.71 mm. The largest measured, 1.49 mm, passes it | RAN for the measure, the limit worked by hand |
| Own plot faces of the pair | 434 touch with no area, 9 lap, 0 apart | RAN |

Height is the second condition. The gap is between the height ranges of the two floor tops, a ramp by its corners:

| Height gap at most | Shown | Touching, with area, not shown |
|---|---|---|
| 5 cm | 165 of 443 | 82 of 388 |
| 10 cm | 375 of 443 | 105 of 388 |
| 15 cm | 443 of 443 | 125 of 388 |
| 30 cm | 443 of 443 | 282 of 388 |

So every pair shown has a gap of at most 15 cm. That is the thickness the name Generic 150mm gives, the thickness of the yard and ramp types is UNKNOWN, not read. It does not decide alone: 125 pairs with area within 15 cm were not shown. What parts those from the shown ones is UNKNOWN, Revit does not state its test.

## What a fix could do, for Lane A

Tried on the plan's points only, not in Revit (RAN): yards and ramps pulled inside by a small distance, generics left alone.

| Pull | Pairs with area left |
|---|---|
| 1 mm | 18 of 443 |
| 2 mm | 3 of 443 |
| 5 mm | 3 of 443 |

The 3 left at any pull are the generics that lap, plots 2556 with 2569, 2557 with 2568 and 2557 with 2569, 6 to 30 cm2 at most 0.6 mm deep. Pulled-in floors no longer touch their neighbours, whether that shows in a level or a walkway is Lane A's question.

## Second check

- Depth by another method, plain Python with no geometry library, how far a corner of one floor stands inside the other: 418 pairs with a corner in, 25 with edges only, largest 1.49 mm, 0 over 1.5 mm. Agrees with the largest of 1.5 mm.
- Every row joined to the sheet Placed: 443 of 443. Placed and the plan each hold 1,003 floors.

## Corrections

- A first read took the first loop of a sketch as the outer one. 47 yards have two loops and the first is not always the outer, so it lost parts and 17 pairs read as apart, one by 12 m. Read even-odd, as one sketch, all 443 touch (RAN second.py). The numbers above are of that read.
- The same reading was in yards.py of the hand run. Yard past plot on the plan is 0.90 m2 (RAN rerun.py on 331 yards), not the 0.87 m2 of steps/fix-2nov-b-handrun.md and the compare with PR 139. Largest 0.019 m2, 254 yards over 1 cm2. Whether PR 139's 0.87 has the same error is UNKNOWN, its script is not read here.

## Not reached

- Whether a plan with no rounding or a pull gives no box in Revit: no run.
- Other neighbourhoods: NBH 012 only, steps/investigations/6b-revit.md counts the overlap warnings per zone.
