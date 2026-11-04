# Phase 6b in Revit, the buttons on all 7 zones

Lane B, 4 Nov 2026, branch check/lane-b-6b. The values sit outside git in lane_b_check\p6b. Counts, gaps and plot numbers only.

## In plain words

- All 7 zones build through buttons 1 to 9 from a new project in Masses, by the test host on the shared listener.
- 6b found the add-in could build 012 and 013 alone. Official numbers exist for two neighbourhoods, so the other 5 stopped at button 2. R557 fixes it, the first runs of those zones stopped safely with a plain message.
- The built models read back by the reader of the keys agree with the plan on every element by id.
- The levels and wall ends against the keys are the gaps of steps/investigations/6b-data.md, no new one. This is the past job build (Q224), so it does not test rev6.

## Runs

| Job | NBH, key | Start | Seconds of buttons | Plots | Floors | Walls | Inputs |
|---|---|---|---|---|---|---|---|
| ZA | 021, IMA | DefaultMetric | 168 | 431 | 1,277 | 1,008 | 262, 0 changed |
| ZB | 022, IMB | DefaultMetric | 165 | 294 | 858 | 702 | 262, 0 changed |
| ZC | 012, IMC | DefaultMetric | 167 | 336 | 1,003 | 778 | 262, 0 changed |
| ZD | 013, IMD | DefaultMetric | 237 | 408 | 1,219 | 986 | 262, 0 changed |
| ZE | 02, IME | DefaultMetric | 140 | 318 | 953 | 764 | 262, 0 changed |
| ZF | 03, IMF | DefaultMetric | 208 | 467 | 1,399 | 1,107 | 262, 0 changed |
| ZG | 01A, IMG | DefaultMetric | 149 | 270 | 540 | 684 | 262, 0 changed |
| LD | 013, IMD | DefaultMetric, Links | 312 run | 408 | 1,219 | 986 | 262, 0 changed |
| ND | 013, IMD | no template, imperial | 190 run | 408 | 1,219 | 986 | 262, 0 changed |

RAN, read from each buttons_result.json. Button 3 ends cancelled in every job, as a job presses no window, the same as earlier runs. ZC and ZD ran on the install before R557, the official number goes first, so their plots cannot change, not rerun. ZE to ZG, ZA, ZB ran after it.

## Second method

- Every built model read by the reader of the keys, kind read, no plan code: every link, floor and wall id of button 9 exists, its plot number is the plot, each generic floor top is 0.0 mm from the plan. 2,524 links, 7,249 floors, 6,029 walls, 0 faults (RAN, by_id.py).
- 70 random plots, 10 per zone at seed 14, by id: 70 the same.
- The report rows by openpyxl: Floors and Walls equal the model on all 7. Plot schedule also keeps the plots the plan could not write, as R552 says.
- ZD, LD and ND hold the same plots and walls, plan field by field (RAN). So no template and Links change no plan.
- 18 link names read TH1 where the plan says TH1-IM, the same prototype file on all 18 (RAN). The 6 villa type rows over the limit on IMA and IMB below are this name, no wrong villa.
- Inputs: 27 of the job's inputs found under C:\SA-demo\inputs by name hash the same after the window passes. The 13 others of that name are the older Prototypes folder, the jobs read prototypes_2024.

## Button 1 on the IMD 2024 copy

- First press: it committed and removed the Floors binding of 4 parameters and the Detail Items binding of CASE, as R507 and R508 say (Q411). Revit drops those values, the file changed. So the done line of 01_next.md, changed nothing, no longer holds, Q452.
- Second press on that file: no dialog, nothing to remove, 4 KB of save stamp, as R508 says.
- Q069, a remade key copy filed by the hashes: not done.

## No template

Plans equal. Step 5 warns Highlighted floors overlap 722 times from no template against 510, cause UNKNOWN, Q453.

## Window passes, real presses

HELP (R540): a hover line and the right text on each of 9 steps and 5 New project rows, a second i replaces the box. FAULT found: every box ran off the screen, bottom 1,292 to 1,814 against a work area of 1,140, Close out of reach, and stood on the pane or window. R558 fixes it, seen on the same 14 rows after an install: Close on screen on all, covers the host on none.

R541, Lane C's rules window, on a copy under the past job, first run to its end: ramp slope changes no field, as Q419 says. Wall top 2000 changes 778 wall tops and no other field, height 1.8 to 2.1 m, +300 mm. Both resets equal the base. Amber on the changed row, 4 Ready after Save. Rev 06 not run, UNKNOWN.

PR 172: the fresh step 1 line seen. Report file name not seen, the section is hidden as C:\SA-demo\config holds no block, Q454. Browse opens its dialog, its time not measured.

## Not proven

- Rev 6 build on the zones, parity skipped, see the PR.
- Ids opened by a person. The reader opened them by element id, no one looked at the plots.
- A press of the pane on buttons 1 to 9 on this build, the jobs run the same commands with no window.

## Compare tables

Past job build, with the override list and with none, against each key. The list was made from these keys, so with it agrees partly by construction. The build with none is the test of the rules, from round 6B-DATA. Limits and seed as there.

### IMA (SE)

With overrides: plots compared 420, only in the tool plan 11, only in the key 0.
Without overrides: plots compared 417, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 416 0 4 0 0 | 413 0 4 0 0 |
| mirror | 420 0 0 0 0 | 409 0 8 0 0 |
| rotation | 420 0 0 0 0 | 409 0 8 0 0 |
| ffl | 0 0 0 420 0 | 0 0 0 417 0 |
| entry | 0 0 0 420 0 | 0 0 0 417 0 |
| steps | 0 0 0 420 0 | 0 0 0 417 0 |
| rampCornersLevel | 0 0 0 420 0 | 0 0 0 417 0 |
| rampCornersPlan | 0 0 0 420 0 | 0 0 0 417 0 |
| wallsToolOnKey | 58 358 4 0 0 | 57 355 5 0 0 |
| wallsKeyOnTool | 55 348 17 0 0 | 71 331 15 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 1667 differs, 1668 differs, 1859 differs, 1860 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 1613 3805.0 mm, 1590 1910.9 mm, 1763 1910.5 mm, 1808 1807.2 mm, 1931 250.8 mm, 1932 250.8 mm, 1929 249.8 mm, 1930 249.8 mm, 1595 102.3 mm, 1596 102.3 mm
- wallsKeyOnTool: 1613 3799.4 mm, 1830 2735.9 mm, 1895 2735.5 mm, 1808 2735.3 mm, 1713 2735.1 mm, 1925 2734.7 mm, 1651 2734.7 mm, 1738 2734.7 mm, 1875 2734.7 mm, 1912 2734.6 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 1667 differs, 1668 differs, 1859 differs, 1860 differs
- mirror: 1613 differs, 1707 differs, 1708 differs, 1709 differs, 1743 differs, 1744 differs, 1745 differs, 1809 differs
- rotation: 1707 180.00 deg, 1708 180.00 deg, 1709 180.00 deg, 1743 180.00 deg, 1744 180.00 deg, 1745 180.00 deg, 1613 180.00 deg, 1809 180.00 deg
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 1613 3805.0 mm, 1813 2210.2 mm, 1868 2209.5 mm, 1808 1807.2 mm, 1796 700.8 mm, 1931 250.8 mm, 1932 250.8 mm, 1929 249.8 mm, 1930 249.8 mm, 1595 102.3 mm
- wallsKeyOnTool: 1613 3799.4 mm, 1830 2730.5 mm, 1808 2730.2 mm, 1895 2730.0 mm, 1651 2729.8 mm, 1875 2729.8 mm, 1925 2729.8 mm, 1738 2729.6 mm, 1713 2729.6 mm, 1700 2729.3 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_ima.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1613 | same / same | same / differs | 0.00 deg / 180.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 3805.0 mm / 3805.0 mm | 3799.4 mm / 3799.4 mm |
| 1685 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.3 mm / 1.3 mm | 99.7 mm / 99.7 mm |
| 1689 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.3 mm / 1.3 mm | 99.7 mm / 99.7 mm |
| 1697 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 1837 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 99.1 mm / 99.1 mm | 99.7 mm / 99.7 mm |
| 1883 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 99.9 mm / 99.9 mm | 99.9 mm / 99.9 mm |
| 1901 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 0.5 mm / 0.5 mm | 2519.8 mm / 2519.8 mm |
| 1927 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 1944 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 103.7 mm / 103.7 mm |
| 1954 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 104.3 mm / 104.3 mm |

### IMB (SE)

With overrides: plots compared 288, only in the tool plan 6, only in the key 2.
Without overrides: plots compared 284, only in the tool plan 0, only in the key 6.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 286 0 2 0 0 | 282 0 2 0 0 |
| mirror | 288 0 0 0 0 | 281 0 3 0 0 |
| rotation | 288 0 0 0 0 | 281 0 3 0 0 |
| ffl | 0 0 0 288 0 | 0 0 0 284 0 |
| entry | 0 0 0 288 0 | 0 0 0 284 0 |
| steps | 0 0 0 288 0 | 0 0 0 284 0 |
| rampCornersLevel | 0 0 0 288 0 | 0 0 0 284 0 |
| rampCornersPlan | 0 0 0 288 0 | 0 0 0 284 0 |
| wallsToolOnKey | 53 225 10 0 0 | 55 222 7 0 0 |
| wallsKeyOnTool | 52 215 21 0 0 | 69 198 17 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 2176 differs, 2177 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 2008 1910.9 mm, 2005 1910.2 mm, 2271 1902.2 mm, 2039 1689.8 mm, 2040 1689.8 mm, 2041 1580.0 mm, 2222 1140.7 mm, 2223 1140.7 mm, 2262 334.7 mm, 2263 334.7 mm
- wallsKeyOnTool: 2171 2736.1 mm, 2249 2735.5 mm, 2148 2735.5 mm, 2288 2735.3 mm, 2264 2735.1 mm, 2256 2735.1 mm, 2159 2735.0 mm, 2160 2734.7 mm, 2257 2734.6 mm, 2099 2734.5 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 2176 differs, 2177 differs
- mirror: 2100 differs, 2199 differs, 2200 differs
- rotation: 2100 180.00 deg, 2199 180.00 deg, 2200 180.00 deg
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 2240 2209.4 mm, 2005 1910.2 mm, 2041 1580.0 mm, 2222 1140.7 mm, 2223 1140.7 mm, 2262 334.7 mm, 2263 334.7 mm, 2220 101.3 mm, 2058 100.7 mm, 2254 100.6 mm
- wallsKeyOnTool: 2171 2731.0 mm, 2148 2730.8 mm, 2249 2730.6 mm, 2288 2730.3 mm, 2257 2730.0 mm, 2160 2729.8 mm, 2159 2729.7 mm, 2264 2729.6 mm, 2256 2729.6 mm, 2099 2729.5 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_imb.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2031 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2048 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 99.1 mm / 99.1 mm | 0.8 mm / 0.8 mm |
| 2120 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.3 mm / 1.3 mm | 0.9 mm / 0.9 mm |
| 2124 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 0.5 mm / 0.5 mm | 99.4 mm / 99.4 mm |
| 2132 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2143 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 0.5 mm / 0.5 mm | 0.6 mm / 0.6 mm |
| 2149 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 0.5 mm / 0.5 mm | 25.4 mm / 1.0 mm |
| 2226 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2234 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2274 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 4.1 mm / 1.0 mm |

### IMC (TU)

With overrides: plots compared 324, only in the tool plan 12, only in the key 0.
Without overrides: plots compared 324, only in the tool plan 0, only in the key 0.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 324 0 0 0 0 | 324 0 0 0 0 |
| mirror | 323 0 1 0 0 | 312 0 12 0 0 |
| rotation | 323 0 1 0 0 | 312 0 12 0 0 |
| ffl | 324 0 0 0 0 | 22 72 230 0 0 |
| entry | 195 6 113 10 0 | 17 135 162 10 0 |
| steps | 324 0 0 0 0 | 307 17 0 0 0 |
| rampCornersLevel | 127 154 43 0 0 | 156 28 140 0 0 |
| rampCornersPlan | 223 97 4 0 0 | 223 97 4 0 0 |
| wallsToolOnKey | 221 93 10 0 0 | 220 95 9 0 0 |
| wallsKeyOnTool | 181 105 38 0 0 | 182 106 36 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: none
- mirror: 2302 differs
- rotation: 2302 180.00 deg
- ffl: none
- entry: 2540 113.0 mm, 2325 111.0 mm, 2609 111.0 mm, 2348 109.0 mm, 2338 80.5 mm, 2320 80.4 mm, 2334 80.4 mm, 2461 80.4 mm, 2552 80.4 mm, 2386 80.3 mm
- steps: none
- rampCornersLevel: 2609 240.0 mm, 2601 138.0 mm, 2325 115.0 mm, 2539 102.0 mm, 2588 100.0 mm, 2610 99.0 mm, 2447 94.0 mm, 2320 88.0 mm, 2608 87.0 mm, 2443 84.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 3999.4 mm, 2546 3900.8 mm, 2548 3799.1 mm, 2346 3699.8 mm, 2347 3699.8 mm, 2549 3699.2 mm, 2540 3698.9 mm, 2541 3698.9 mm, 2374 1999.7 mm, 2391 1999.7 mm
- wallsKeyOnTool: 2491 24900.6 mm, 2476 24899.5 mm, 2528 11899.4 mm, 2549 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4492.4 mm, 2547 4099.8 mm, 2546 4000.6 mm, 2543 3999.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 2291 differs, 2292 differs, 2293 differs, 2297 differs, 2298 differs, 2299 differs, 2302 differs, 2340 differs, 2534 differs, 2535 differs
- rotation: 2291 180.00 deg, 2292 180.00 deg, 2293 180.00 deg, 2297 180.00 deg, 2298 180.00 deg, 2299 180.00 deg, 2302 180.00 deg, 2340 180.00 deg, 2546 180.00 deg, 2534 180.00 deg
- ffl: 2608 511.0 mm, 2609 360.0 mm, 2601 328.0 mm, 2606 328.0 mm, 2607 328.0 mm, 2610 307.0 mm, 2319 233.0 mm, 2320 233.0 mm, 2301 214.0 mm, 2326 197.0 mm
- entry: 2608 511.0 mm, 2607 407.8 mm, 2601 328.4 mm, 2610 307.3 mm, 2606 292.8 mm, 2609 249.3 mm, 2605 183.8 mm, 2611 165.7 mm, 2349 164.0 mm, 2320 152.7 mm
- steps: 2298 1, 2301 1, 2319 1, 2327 1, 2329 1, 2331 1, 2335 1, 2339 1, 2342 1, 2344 1
- rampCornersLevel: 2608 599.0 mm, 2609 598.0 mm, 2607 464.0 mm, 2610 407.0 mm, 2606 382.0 mm, 2601 369.0 mm, 2605 239.0 mm, 2600 237.0 mm, 2611 235.0 mm, 2349 229.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 3999.4 mm, 2546 3900.8 mm, 2548 3799.1 mm, 2555 2210.5 mm, 2459 2210.2 mm, 2569 2209.5 mm, 2447 2209.4 mm, 2374 1999.7 mm, 2391 1999.7 mm, 2299 101.3 mm
- wallsKeyOnTool: 2491 24900.6 mm, 2476 24899.5 mm, 2549 11899.4 mm, 2528 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4492.4 mm, 2547 4099.8 mm, 2546 4000.6 mm, 2543 3999.2 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_imc.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2328 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 149.0 mm | 70.4 mm / 79.2 mm | 0 / 0 | 17.0 mm / 164.0 mm | 1.0 mm / 1.0 mm | 1.3 mm / 1.3 mm | 1.3 mm / 1.3 mm |
| 2345 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 13.0 mm | 0.2 mm / 12.5 mm | 0 / 0 | 14.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 99.4 mm / 99.4 mm | 0.7 mm / 0.7 mm |
| 2425 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 42.0 mm | 40.3 mm / 1.8 mm | 0 / 0 | 4.0 mm / 42.0 mm | 0.0 mm / 0.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2429 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.1 mm / 0.8 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm |
| 2437 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.4 mm / 5.0 mm | 0 / 0 | 8.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.3 mm / 1.3 mm |
| 2449 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.2 mm / 1.6 mm | 0 / 0 | 3.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm |
| 2455 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 32.0 mm | 30.4 mm / 1.7 mm | 0 / 0 | 4.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 2530 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 71.0 mm | 70.2 mm / 1.3 mm | 0 / 0 | 3.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 10099.5 mm / 10099.5 mm |
| 2571 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.3 mm / 2.6 mm | 0 / 0 | 4.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 3.7 mm / 3.7 mm | 100.0 mm / 100.0 mm |
| 2617 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 39.7 mm / 1.7 mm | 0 / 0 | 3.0 mm / 41.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |

### IMD (TU)

With overrides: plots compared 401, only in the tool plan 7, only in the key 2.
Without overrides: plots compared 400, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 401 0 0 0 0 | 400 0 0 0 0 |
| mirror | 401 0 0 0 0 | 398 0 2 0 0 |
| rotation | 401 0 0 0 0 | 398 0 2 0 0 |
| ffl | 399 0 2 0 0 | 40 122 238 0 0 |
| entry | 245 6 145 5 0 | 37 161 197 5 0 |
| steps | 401 0 0 0 0 | 392 8 0 0 0 |
| rampCornersLevel | 208 139 54 0 0 | 173 54 173 0 0 |
| rampCornersPlan | 247 148 6 0 0 | 247 148 5 0 0 |
| wallsToolOnKey | 166 220 15 0 0 | 168 221 11 0 0 |
| wallsKeyOnTool | 178 178 45 0 0 | 182 174 44 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: none
- mirror: none
- rotation: none
- ffl: 1382 153.0 mm, 1436 50.0 mm
- entry: 1249 80.5 mm, 1480 80.5 mm, 1322 80.4 mm, 1204 80.4 mm, 1157 80.3 mm, 1159 80.3 mm, 1460 80.3 mm, 1473 80.1 mm, 1507 80.0 mm, 1531 80.0 mm
- steps: none
- rampCornersLevel: 1274 192.0 mm, 1534 148.0 mm, 1216 133.0 mm, 1536 120.0 mm, 1197 102.0 mm, 1217 101.0 mm, 1172 96.0 mm, 1329 91.0 mm, 1191 88.0 mm, 1153 85.0 mm
- rampCornersPlan: 1172 3000.7 mm, 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1190 5410.4 mm, 1191 5410.4 mm, 1189 3700.0 mm, 1387 3700.0 mm, 1388 3700.0 mm, 1379 3698.9 mm, 1380 3698.9 mm, 1172 2800.2 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 3936.0 mm, 1246 3935.4 mm, 1172 2799.9 mm, 1287 2426.0 mm, 1460 2000.1 mm, 1403 2000.0 mm, 1453 1999.7 mm, 1465 1999.7 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 1268 differs, 1403 differs
- rotation: 1403 180.00 deg, 1268 180.00 deg
- ffl: 1272 226.0 mm, 1273 226.0 mm, 1170 223.0 mm, 1171 223.0 mm, 1304 198.0 mm, 1274 193.0 mm, 1298 185.0 mm, 1299 185.0 mm, 1338 177.0 mm, 1173 174.0 mm
- entry: 1272 233.8 mm, 1273 225.6 mm, 1171 223.1 mm, 1304 198.4 mm, 1274 192.7 mm, 1173 184.8 mm, 1174 174.0 mm, 1277 173.3 mm, 1300 172.6 mm, 1302 172.5 mm
- steps: 1156 1, 1250 1, 1311 1, 1312 1, 1322 1, 1338 1, 1371 1, 1436 1
- rampCornersLevel: 1272 313.0 mm, 1173 283.0 mm, 1273 272.0 mm, 1171 257.0 mm, 1299 214.0 mm, 1303 214.0 mm, 1170 208.0 mm, 1301 204.0 mm, 1276 203.0 mm, 1271 201.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1510 2210.7 mm, 1197 2210.7 mm, 1378 2210.2 mm, 1366 2209.1 mm, 1403 2000.2 mm, 1280 1902.5 mm, 1485 1901.1 mm, 1227 500.8 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 3936.0 mm, 1246 3935.4 mm, 1287 2426.0 mm, 1460 2000.1 mm, 1403 2000.0 mm, 1453 1999.7 mm, 1465 1999.7 mm, 1461 1999.4 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_imd.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1198 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 28.0 mm | 0.4 mm / 28.4 mm | 0 / 0 | 26.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 99.9 mm / 99.9 mm | 1.0 mm / 1.1 mm |
| 1270 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 69.0 mm | 0.4 mm / 98.8 mm | 0 / 0 | 30.0 mm / 129.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.7 mm / 99.7 mm |
| 1274 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 193.0 mm | 0.4 mm / 192.7 mm | 0 / 0 | 192.0 mm / 174.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 0.7 mm / 0.7 mm |
| 1284 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.7 mm / 0.7 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 99.1 mm / 99.1 mm |
| 1418 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 10.0 mm | 0.0 mm / 9.5 mm | 0 / 0 | 10.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm |
| 1464 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 7.0 mm | 0.2 mm / 8.0 mm | 0 / 0 | 10.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 1482 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 6.0 mm | 0.1 mm / 6.0 mm | 0 / 0 | 8.0 mm / 12.0 mm | 1.4 mm / 1.4 mm | 99.4 mm / 99.4 mm | 1929.7 mm / 1929.7 mm |
| 1512 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 4.0 mm | 0.2 mm / 4.4 mm | 0 / 0 | 1.0 mm / 6.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 99.1 mm / 99.1 mm |
| 1529 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.3 mm / 1.2 mm | 1.3 mm / 0.8 mm |
| 1539 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.1 mm / 2.5 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |

### IME (TU)

With overrides: plots compared 318, only in the tool plan 0, only in the key 2.
Without overrides: plots compared 316, only in the tool plan 0, only in the key 4.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 318 0 0 0 0 | 316 0 0 0 0 |
| mirror | 318 0 0 0 0 | 308 0 8 0 0 |
| rotation | 318 0 0 0 0 | 314 0 2 0 0 |
| ffl | 318 0 0 0 0 | 51 94 171 0 0 |
| entry | 318 0 0 0 0 | 31 159 126 0 0 |
| steps | 318 0 0 0 0 | 316 0 0 0 0 |
| rampCornersLevel | 102 86 130 0 0 | 161 45 110 0 0 |
| rampCornersPlan | 224 93 1 0 0 | 217 92 7 0 0 |
| wallsToolOnKey | 260 52 6 0 0 | 260 52 4 0 0 |
| wallsKeyOnTool | 220 84 14 0 0 | 236 68 12 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: none
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 2854 124.0 mm, 2759 121.0 mm, 2913 104.0 mm, 2643 91.0 mm, 2714 89.0 mm, 2736 87.0 mm, 2771 86.0 mm, 2775 86.0 mm, 2661 85.0 mm, 2663 84.0 mm
- rampCornersPlan: 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm, 2674 1.4 mm, 2675 1.4 mm, 2679 1.4 mm, 2682 1.4 mm, 2683 1.4 mm, 2684 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2704 3700.3 mm, 2705 3700.3 mm, 2680 3699.6 mm, 2681 3699.6 mm, 2894 159.5 mm, 2895 159.5 mm, 2891 103.0 mm, 2904 100.3 mm
- wallsKeyOnTool: 2626 4490.1 mm, 2665 4489.3 mm, 2646 3680.3 mm, 2645 3679.8 mm, 2879 2429.2 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2766 1930.3 mm, 2741 1929.7 mm, 2752 1929.7 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 2786 differs, 2787 differs, 2892 differs, 2893 differs, 2894 differs, 2895 differs, 2896 differs, 2897 differs
- rotation: 2787 180.00 deg, 2786 180.00 deg
- ffl: 2893 241.0 mm, 2892 227.0 mm, 2894 214.0 mm, 2895 214.0 mm, 2896 162.0 mm, 2897 109.0 mm, 2913 103.0 mm, 2714 91.0 mm, 2767 86.0 mm, 2772 84.0 mm
- entry: 2893 241.2 mm, 2892 227.2 mm, 2894 213.5 mm, 2895 196.6 mm, 2896 162.2 mm, 2897 109.4 mm, 2913 103.4 mm, 2714 91.3 mm, 2759 90.7 mm, 2767 85.7 mm
- steps: none
- rampCornersLevel: 2892 225.0 mm, 2893 224.0 mm, 2894 221.0 mm, 2896 184.0 mm, 2895 174.0 mm, 2897 156.0 mm, 2666 138.0 mm, 2854 130.0 mm, 2759 126.0 mm, 2949 93.0 mm
- rampCornersPlan: 2892 19500.4 mm, 2897 19500.4 mm, 2893 19480.7 mm, 2894 19480.7 mm, 2895 19480.7 mm, 2896 19480.7 mm, 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2698 2210.2 mm, 2829 2209.4 mm, 2894 159.5 mm, 2895 159.5 mm, 2891 103.0 mm, 2904 100.3 mm, 2905 100.3 mm, 2903 100.3 mm
- wallsKeyOnTool: 2646 3680.3 mm, 2645 3679.8 mm, 2879 2429.2 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2766 1930.3 mm, 2741 1929.7 mm, 2752 1929.7 mm, 2719 1430.6 mm, 2730 1430.4 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_ime.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2669 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 4.0 mm | 0.0 mm / 3.7 mm | 0 / 0 | 6.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 2686 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2760 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 45.0 mm | 0.0 mm / 15.4 mm | 0 / 0 | 17.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm | 1.0 mm / 1.0 mm |
| 2764 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 52.0 mm | 0.0 mm / 2.2 mm | 0 / 0 | 49.0 mm / 51.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm |
| 2772 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 84.0 mm | 0.0 mm / 12.6 mm | 0 / 0 | 14.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2783 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 2.8 mm | 0 / 0 | 74.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2789 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 2.7 mm | 0 / 0 | 74.0 mm / 71.0 mm | 0.0 mm / 0.0 mm | 0.9 mm / 0.9 mm | 0.9 mm / 0.9 mm |
| 2864 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 16.0 mm | 0.0 mm / 13.6 mm | 0 / 0 | 35.0 mm / 22.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2903 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 0.8 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 2949 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 79.0 mm | 0.0 mm / 78.6 mm | 0 / 0 | 77.0 mm / 93.0 mm | 1.4 mm / 1.4 mm | 1.2 mm / 0.5 mm | 19.6 mm / 1.0 mm |

### IMF (TU)

With overrides: plots compared 463, only in the tool plan 4, only in the key 13.
Without overrides: plots compared 462, only in the tool plan 0, only in the key 14.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 463 0 0 0 0 | 462 0 0 0 0 |
| mirror | 463 0 0 0 0 | 460 0 2 0 0 |
| rotation | 463 0 0 0 0 | 460 0 2 0 0 |
| ffl | 463 0 0 0 0 | 32 148 282 0 0 |
| entry | 457 0 0 6 0 | 25 209 222 6 0 |
| steps | 463 0 0 0 0 | 425 27 10 0 0 |
| rampCornersLevel | 114 166 183 0 0 | 240 83 139 0 0 |
| rampCornersPlan | 304 156 3 0 0 | 303 156 3 0 0 |
| wallsToolOnKey | 392 66 5 0 0 | 388 65 9 0 0 |
| wallsKeyOnTool | 332 109 22 0 0 | 340 101 21 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: none
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 673 140.0 mm, 975 135.0 mm, 974 134.0 mm, 986 134.0 mm, 934 132.0 mm, 741 121.0 mm, 958 115.0 mm, 1046 114.0 mm, 1008 109.0 mm, 907 104.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 865 1999.2 mm, 823 280.1 mm, 824 280.1 mm, 817 280.1 mm, 818 280.1 mm, 819 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 640 3690.9 mm, 727 3689.6 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 739 differs, 755 differs
- rotation: 755 180.00 deg, 739 180.00 deg
- ffl: 1008 400.0 mm, 1009 400.0 mm, 990 379.0 mm, 998 379.0 mm, 704 363.0 mm, 1094 360.0 mm, 1095 360.0 mm, 1061 322.0 mm, 1062 322.0 mm, 910 321.0 mm
- entry: 986 138.1 mm, 975 134.7 mm, 934 131.9 mm, 1046 116.8 mm, 958 114.6 mm, 1075 98.9 mm, 1052 95.7 mm, 1068 95.3 mm, 946 90.0 mm, 1069 88.5 mm
- steps: 1008 3, 704 2, 910 2, 918 2, 990 2, 998 2, 1009 2, 1058 2, 1062 2, 1094 2
- rampCornersLevel: 974 180.0 mm, 673 175.0 mm, 694 161.0 mm, 713 149.0 mm, 654 147.0 mm, 741 138.0 mm, 887 97.0 mm, 707 84.0 mm, 1010 83.0 mm, 826 82.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 1052 2210.0 mm, 836 2209.7 mm, 849 2209.5 mm, 1069 2209.4 mm, 865 1999.2 mm, 823 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 640 3690.9 mm, 713 3680.1 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_imf.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 696 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 1.4 mm | 0 / 0 | 42.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 773 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 49.0 mm | 0.0 mm / 48.7 mm | 0 / 0 | 90.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 20.2 mm / 0.8 mm |
| 777 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 54.0 mm | 0.0 mm / 53.5 mm | 0 / 0 | 99.0 mm / 46.0 mm | 1.4 mm / 1.4 mm | 1.3 mm / 1.3 mm | 0.8 mm / 0.8 mm |
| 785 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 10.0 mm | 0.0 mm / 19.3 mm | 0 / 0 | 40.0 mm / 21.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 934 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 132.0 mm | 0.0 mm / 131.9 mm | 0 / 0 | 132.0 mm / 4.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.3 mm / 1.3 mm |
| 980 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 27.0 mm | 0.0 mm / 27.1 mm | 0 / 0 | 31.0 mm / 5.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 0.5 mm / 0.5 mm |
| 998 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 379.0 mm | 0.0 mm / 26.9 mm | 0 / 2 | 99.0 mm / 72.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 1024 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 83.0 mm | 0.0 mm / 83.0 mm | 0 / 0 | 86.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.0 mm | 1.5 mm / 0.9 mm |
| 1041 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 32.0 mm | 0.0 mm / 4.7 mm | 0 / 0 | 7.0 mm / 2.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 1051 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 75.0 mm | 0.0 mm / 75.3 mm | 0 / 0 | 78.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm |

### IMG (TU)

With overrides: plots compared 258, only in the tool plan 12, only in the key 10.
Without overrides: plots compared 257, only in the tool plan 0, only in the key 11.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 258 0 0 0 0 | 257 0 0 0 0 |
| mirror | 258 0 0 0 0 | 236 0 21 0 0 |
| rotation | 258 0 0 0 0 | 245 2 10 0 0 |
| ffl | 258 0 0 0 0 | 75 60 122 0 0 |
| entry | 258 0 0 0 0 | 46 99 112 0 0 |
| steps | 258 0 0 0 0 | 249 8 0 0 0 |
| rampCornersLevel | 81 77 100 0 0 | 141 29 87 0 0 |
| rampCornersPlan | 160 83 15 0 0 | 149 82 26 0 0 |
| wallsToolOnKey | 188 57 13 0 0 | 196 56 5 0 0 |
| wallsKeyOnTool | 174 63 21 0 0 | 181 57 19 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: none
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 585 166.0 mm, 623 117.0 mm, 629 100.0 mm, 3008 92.0 mm, 2953 90.0 mm, 567 87.0 mm, 571 87.0 mm, 579 87.0 mm, 515 87.0 mm, 519 87.0 mm
- rampCornersPlan: 469 3000.2 mm, 2953 2000.2 mm, 552 2000.2 mm, 3003 1999.7 mm, 3004 542.2 mm, 2950 245.8 mm, 2951 221.0 mm, 585 95.4 mm, 441 56.1 mm, 442 56.1 mm
- wallsToolOnKey: 612 3700.5 mm, 613 3700.5 mm, 614 3700.3 mm, 615 3700.3 mm, 546 3700.0 mm, 547 3700.0 mm, 555 3698.9 mm, 556 3698.9 mm, 469 2800.1 mm, 2950 2777.8 mm
- wallsKeyOnTool: 576 17690.5 mm, 592 17689.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 469 3712.9 mm, 568 3098.5 mm, 455 2430.9 mm, 454 2424.7 mm, 566 2099.2 mm, 2953 2000.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 457 differs, 2998 differs, 2999 differs, 3000 differs, 3001 differs, 3002 differs, 3003 differs, 3005 differs, 3006 differs, 3007 differs
- rotation: 457 180.00 deg, 2998 180.00 deg, 3000 180.00 deg, 3001 180.00 deg, 3031 180.00 deg, 3003 180.00 deg, 3030 180.00 deg, 3002 180.00 deg, 2999 180.00 deg, 2950 1.00 deg
- ffl: 3011 248.0 mm, 3012 248.0 mm, 3013 248.0 mm, 3014 248.0 mm, 3005 244.0 mm, 3006 244.0 mm, 3015 194.0 mm, 3016 194.0 mm, 485 188.0 mm, 3025 163.0 mm
- entry: 3006 234.4 mm, 3015 220.8 mm, 3014 196.9 mm, 3016 184.6 mm, 3007 183.0 mm, 3005 162.8 mm, 3013 147.3 mm, 3008 133.4 mm, 3012 104.8 mm, 3009 97.7 mm
- steps: 485 1, 580 1, 2953 1, 3007 1, 3008 1, 3011 1, 3025 1, 3026 1
- rampCornersLevel: 3014 317.0 mm, 3007 292.0 mm, 3008 279.0 mm, 3016 271.0 mm, 3006 267.0 mm, 3015 262.0 mm, 3005 259.0 mm, 3013 227.0 mm, 3012 208.0 mm, 585 163.0 mm
- rampCornersPlan: 3005 19481.2 mm, 3006 19481.2 mm, 3007 19481.2 mm, 3008 19481.2 mm, 3013 19481.2 mm, 3014 19481.2 mm, 3015 19481.2 mm, 3016 19481.2 mm, 3009 19481.2 mm, 3011 19480.7 mm
- wallsToolOnKey: 546 2210.0 mm, 592 2209.5 mm, 552 2000.2 mm, 2953 2000.2 mm, 3004 432.0 mm, 3011 279.6 mm, 3012 279.6 mm, 3009 279.6 mm, 3010 279.6 mm, 3007 278.7 mm
- wallsKeyOnTool: 576 17690.5 mm, 592 17689.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 568 3098.5 mm, 455 2430.9 mm, 454 2424.7 mm, 566 2099.2 mm, 2953 2000.2 mm, 552 2000.2 mm

Random plots at seed 14, the gap with overrides then without overrides. The values are in side_by_side_img.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 491 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 1.8 mm | 0 / 0 | 22.0 mm / 20.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm |
| 508 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.4 mm / 0.4 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 35.5 mm / 35.5 mm | 1.4 mm / 1.4 mm |
| 583 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm |
| 588 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.3 mm / 0.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm |
| 596 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.1 mm / 0.1 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 0.9 mm / 0.9 mm | 1.0 mm / 1.0 mm |
| 607 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 2.1 mm | 0 / 0 | 43.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 613 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 38.0 mm | 0.0 mm / 38.4 mm | 0 / 0 | 37.0 mm / 17.0 mm | 1.0 mm / 1.0 mm | 3700.5 mm / 0.9 mm | 1.5 mm / 1.3 mm |
| 2981 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 24.0 mm | 0.0 mm / 23.9 mm | 0 / 0 | 25.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.9 mm / 99.9 mm | 20.7 mm / 1.3 mm |
| 3008 | same / same | same / differs | 0.00 deg / 0.00 deg | 0.0 mm / 77.0 mm | 0.0 mm / 133.4 mm | 0 / 1 | 92.0 mm / 279.0 mm | 1.0 mm / 19481.2 mm | 278.7 mm / 278.7 mm | 278.2 mm / 278.2 mm |
| 3016 | same / same | same / differs | 0.00 deg / 0.00 deg | 0.0 mm / 194.0 mm | 0.0 mm / 184.6 mm | 0 / 0 | 17.0 mm / 271.0 mm | 1.0 mm / 19481.2 mm | 99.0 mm / 99.0 mm | 1.4 mm / 99.5 mm |

