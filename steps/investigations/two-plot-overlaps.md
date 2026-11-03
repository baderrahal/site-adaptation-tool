# The 443 overlaps of two plots at step 5

Round FIX-3NOV-B task 3, Lane B. Measured, nothing fixed, the plan belongs to Lane A. NBH 012, the hand run on main a4551dd: warnings_5.html for the pairs, the sheet Placed for the id of each floor, plan_012.json for the outlines and heights. The model was not read. Scripts and pairs.csv in Documents\SiteAdaptation\lane_b_check\two_plot, outside git, the breaker's scripts in its folder breaker. Distances, counts and plot numbers only.

## Cause

Hairline laps between neighbours, from points on a 1 mm grid, that Revit shows when the two floors also meet in height.

- No pair is a deep lap: 0 cross, 0 sit inside the other, the deepest is 1.47 mm.
- Ramps: rounding explains them. Both back corners of a ramp move by one same shift, the rounding of one 5.52 m vector set by the plot angle, so one corner stands out and the other in, on 329 of the 329 ramps on 4 sided plots (RAN by the breaker). It repeats, it is no noise: the 10 Ramps 0.5% with Ramps 0.5% rows have a width of 1.365 mm at the median.
- Yards: rounding of one point does not explain all of them. 71 of 488 yard corners past their own face stand over 0.71 mm out, 24 over 1 mm, largest 1.34 mm. The generic and the yard clip use the same ring on whole mm (READ dotnet/SiteAdaptation.Common/Plan/FloorOutlines.cs:395 and 439), so one clip should leave at most 0.71 mm. So the yard clip adds an error beyond rounding. Why is UNKNOWN, the clipper was not run.
- That a plan with no rounding, or a finer grid, gives no box in Revit is UNKNOWN, no run tried it.

## Numbers

RAN unless tagged, recheck.py and measure.py.

| What | Count |
|---|---|
| Rows of overlap in the box, distinct pairs | 443, 443 |
| Plots in a pair | 302 of the 336 plots of the plan |
| Area of 1 cm2 or less | 72 |
| Area over 1 cm2, all of depth under 1 cm | 371 |
| Crossing, one inside the other | 0, 0 |
| Depth under 1 mm, from 1 to 1.47 mm | 415, 28 |
| Smallest depth shown | 0.16 mm |

The row of 1 cm2 or less is small, not shallow: it holds the deepest pair of all, 2542 ramp with 2543 yard.

| Measure | Median | 90th percentile | Largest | Sum |
|---|---|---|---|---|
| Area, m2 | 0.0007 | 0.0037 | 0.0193 | 0.667 |
| Depth, mm | 0.4 | 0.9 | 1.47 | |
| Length of the edge the two share, m | 5.5 | 16.5 | 19.5 | |

Which floor stands past its own generic, the plot face, with no lower limit on the area:

| Whose | Pairs |
|---|---|
| One of the two | 393 |
| Both | 43 |
| Neither | 4 |
| Two generics | 3 |

The breaker gets 42 for both and 5 for neither, one pair at the limit of the float. The first version of this file said neither 77, a limit of 1 cm2 in measure.py put the small laps there.

By floor pair: Generic with yard 204, Generic with Ramps 0.5% 115, Ramps 0.5% with yard 58, yard with yard 27, Generic with Ramps 2% 13, Ramps 0.5% with Ramps 0.5% 10, Flat with Generic 6, Ramps 2% with yard 4, Generic with Generic 3, Flat with yard 2, Ramps 0.5% with Ramps 2% 1. 148 of the 443 hold no yard. Row by row: pairs.csv.

| Fact | Result | Tag |
|---|---|---|
| Points of the plan | 3 decimals, none finer | RAN |
| Clipping of a yard | on whole millimetres | READ FloorOutlines.cs:206 |
| Ramp corners past their own face | 336, over 0.71 mm 11, over 1 mm 0 | RAN |
| Yard corners past their own face | 488, over 0.71 mm 71, over 1 mm 24 | RAN |
| Rows with a yard corner over the limit inside the other floor | 48 | RAN by the breaker |
| Own plot faces of the shown pairs | 434 touch with no area, 9 lap, 0 apart | RAN |
| Generic pairs that lap, shown or not | 20, 3 shown, largest 40.2 cm2, 2442 with 2467 | RAN |
| Plots square to the axes | 0 of 336, to a whole degree, so no plot to compare with: the angle is not tested | RAN |

## Height

Gap taken where the two floors lap, a ramp's height from its corners there. RAN by the breaker, not rerun by me.

| Gap at most | Shown | Touching with area, not shown |
|---|---|---|
| 5 cm | 110 of 443 | 54 of 388 |
| 10 cm | 319 of 443 | 79 of 388 |
| 15 cm | 443 of 443 | 88 of 388 |

The first version took the whole height range of a ramp, 165, 375, 443 shown and 82, 105, 125 not, which overstates how many meet.

Of the 88 not shown, 60 are under 0.15 mm deep and 28 sit at the same height. No shown pair of the 1,131 is under 0.16 mm deep. Flat floors at the same height: 3 shown, 30 not, what parts them is UNKNOWN. Leaving those out, a gap under 15 cm with a depth of at least 0.15 mm parts shown from not shown on 497 of 498. Revit's own limit is UNKNOWN, not read. 15 cm fits the name Generic 150mm, and the data bound it for a generic and a yard on top. For a ramp on top the largest shown is 9.3 cm, so 15 cm is an assumption there.

That the plan's sizes predict the warnings is the best evidence that the floors stand as the plan, and it is indirect.

## What a fix could do, for Lane A

Tried on the plan's points only, not in Revit: yards and ramps pulled inside, generics left alone. Counts any area over 0, which is stricter than Revit behaves.

| Pull | Left, points as buffered | Left, rounded back to 1 mm |
|---|---|---|
| 1 mm | 18 of 443 | 22 by snapping (RAN recheck.py), 34 by rounding each point (RAN by the breaker) |
| 2 mm | 3 of 443 | 3 |
| 5 mm | 3 of 443 | 3 |

- The 3 left are the shown generics, 2556 with 2569, 2557 with 2568 and 2557 with 2569. 17 more generic pairs lap and are not shown. A level move or an override can bring them into height, and no pull of yards and ramps touches them.
- A 2 mm pull moves a ramp corner about 2.8 mm. config/project.json holds cornerOnVertexM at 0.002 and RevitFloorDocument.cs:147 refuses a corner off its outline by more, so a fix must move the corners with the outline (READ by the breaker).
- A finer grid was not tried. Pulled floors no longer touch their neighbours, whether that shows is Lane A's question.

## Second check

- Against another file, not the plan: floors_012_accepted.json names plot and types by id, 688 of 688 agree with the sheet Placed. Placed type against plan type, 0 of 1,003 differ. warnings_reopen.html equals shown plus accepted, 1,131 (RAN by the breaker).
- Depth by corners, how far a corner of one floor stands inside the other, all loops: 439 pairs with a corner in, 4 with edges only, largest 1.489 mm against 1.466 mm by circle. Same plan, so a check of the arithmetic, not of the model.
- A rerun of the breaker's main numbers by a script of mine, recheck.py: depth, corners past the face, corners in and generic laps agree. Differences as named above.

## Corrections

- A first read took the first loop of a sketch as the outer one. 47 yards have two loops, both are parts apart with no hole, so it lost parts and 17 pairs read as apart, one by 12 m. Read even-odd, as one sketch, all 443 touch (RAN).
- The first second.py still read the first loop only, the first version of this file gave 418 and 25 from it. Now 439 and 4.
- The same reading was in yards.py of the hand run. Yard past plot on the plan is 0.90 m2, not 0.87 m2 (RAN, 2 ways), largest 0.019 m2, 254 yards over 1 cm2. All from the 47 two loop yards. The breaker gets 0.865 old and 0.901 new on every plan with these yards, so no plan change made this, and if PR 139's plan is of that kind its true value is also 0.90. Which plan was PR 139's is UNKNOWN.

## Not reached

- The cause of the yard corners over the limit.
- What a plan with no rounding, a finer grid or a pull gives in Revit. Any read of the model.
- Other neighbourhoods: NBH 012 only, steps/investigations/6b-revit.md counts the overlap warnings per zone.
