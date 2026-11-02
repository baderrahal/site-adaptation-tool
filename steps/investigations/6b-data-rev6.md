# Round 6B-DATA-REV6, what a new project would get on the 5 TU zones

Lane D, 2 Nov 2026, after steps/investigations/6b-data.md. Mode rev6 with no override list, by config copies outside git, the repo config unchanged. No Revit and no change to the tool. Counts, gaps and plot numbers only.

## In plain words

- **Coverage comes first.** With the config as it stands, rev6 writes plots on IMC only, 324 of 1,791 plots. The one named road surface, Top Surface-02, covers no plot of IMD, IME, IMF or IMG, so those zones get no plan. Naming the largest surface of each zone in a copy of the config (CHOSEN) writes 1,657 of 1,791. IMF still leaves out 38 and IMG 89, since each sits on two surfaces and rev6 takes one (READ, the road surfaces of the front corners in the summary of the past job plans).
- **On the plots it writes, rev6 gives the same levels as the past job rules.** FFL, El @ Entry and steps are equal on 1,657 of 1,657 plots, and the ramp corners on 1,657. The modes differ in which road surface they may use, the yard floors, the cluster proposals and some wall ends, not in the level rule.
- **FFL:** per zone 29 to 55 percent of plots lie within 5 mm of the past model, 46 to 79 percent within 30 mm and 92 to 99 percent within 100 mm. So not close enough to stand without a check, but rarely far off.
- **El @ Entry:** 47 to 60 percent within 5 mm, 81 to 92 percent within 30 mm, 92 to 100 percent within 100 mm. Closer than FFL. On IMC and IMD the key parameter is itself off the schedule on many plots (Q070), so these two zones may read worse than the real gap.
- **Steps:** the same on 1,595 of 1,657 plots, 1 off on 53, more than 1 off on 9, those all on IMF.
- **Ramp corners:** the front corners follow the road and sit close, 80 to 97 percent within 30 mm. The back corners hang on the door level and sit further off, 62 to 81 percent within 30 mm.
- **The gaps are scatter, not an offset.** The median signed FFL gap is -4.0 to 0.0 mm on IMD to IMG and -20.0 mm on IMC, so no one correction would close it. The past levels likely follow something the road and a fixed slope do not hold, why is UNKNOWN.

Against the 0.90 of majority.strongShare, read at the 5 mm limit, neither FFL nor El @ Entry reaches it on any zone. Steps and the villa type do, on every zone.

## 1. Coverage and the door level

No civil entry file: the three civil.csv of the data book are entity tables and no file holds Plot No. and El @ Entry (SEARCHED, the data book, Q227), and doorLevel.civilFile.path is null. So every plot takes road level plus slope, as round 4i set it (R408, READ, config). Because the door level is worked out from the slope, the slope cannot leave the manual range: 0 plots are listed outside it, which says nothing about the real door levels.

| Zone | Plots in key | Written, config as is | Surface named | Written, surface named | Left out, no road level | Past job rules write |
|---|---|---|---|---|---|---|
| IMC | 324 | 324 | Top Surface-02, the config | 324 | 0 | 324 |
| IMD | 403 | 0 | Top Surface-01 | 400 | 3 | 400 |
| IME | 320 | 0 | Part 4 Top Surfaces | 316 | 4 | 316 |
| IMF | 476 | 0 | Compiled West_MM | 438 | 38 | 462 |
| IMG | 268 | 0 | Compiled West_MM | 179 | 89 | 257 |
| All | 1,791 | 324 | | 1,657 | 134 | 1,759 |

The surface named is the one that holds the most plots of the zone in the past job build, CHOSEN by Code. The prompt asked for rev6, and with the config as is it gives no plan on 4 zones, so this second run is what answers how close a new project gets, since a new project names its own one finished surface (Q256, Q364). Each config copy differs from the repo config in designBasis.mode and, off IMC, roadLevel.surface, proven by diff.

## 2. The zone tables

Each cell is plots at or within the limit / plots over the limit, with the limits of 6B-DATA, on the plots rev6 writes, so the two tables hold the same plots. The past job plots outside them are in section 5.

Rev6, no override list:

| Zone | Plots | Villa | Mirror | Rot. | FFL | Entry | Steps | Ramp level | Ramp plan | Walls tool on key | Walls key on tool |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IMC | 324 | 322 / 2 | 312 / 12 | 312 / 12 | 94 / 230 | 152 / 162 | 324 / 0 | 184 / 140 | 320 / 4 | 315 / 9 | 288 / 36 |
| IMD | 400 | 396 / 4 | 398 / 2 | 398 / 2 | 162 / 238 | 198 / 197 | 400 / 0 | 227 / 173 | 395 / 5 | 389 / 11 | 356 / 44 |
| IME | 316 | 314 / 2 | 308 / 8 | 314 / 2 | 145 / 171 | 190 / 126 | 316 / 0 | 206 / 110 | 309 / 7 | 312 / 4 | 304 / 12 |
| IMF | 438 | 436 / 2 | 436 / 2 | 436 / 2 | 166 / 272 | 221 / 211 | 429 / 9 | 310 / 128 | 435 / 3 | 429 / 9 | 419 / 19 |
| IMG | 179 | 177 / 2 | 178 / 1 | 178 / 1 | 98 / 81 | 106 / 73 | 179 / 0 | 121 / 58 | 170 / 9 | 176 / 3 | 167 / 12 |

Past job rules, no override list, the same plots:

| Zone | Plots | Villa | Mirror | Rot. | FFL | Entry | Steps | Ramp level | Ramp plan | Walls tool on key | Walls key on tool |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IMC | 324 | 322 / 2 | 312 / 12 | 312 / 12 | 94 / 230 | 152 / 162 | 324 / 0 | 184 / 140 | 320 / 4 | 315 / 9 | 288 / 36 |
| IMD | 400 | 396 / 4 | 398 / 2 | 398 / 2 | 162 / 238 | 198 / 197 | 400 / 0 | 227 / 173 | 395 / 5 | 389 / 11 | 356 / 44 |
| IME | 316 | 314 / 2 | 308 / 8 | 314 / 2 | 145 / 171 | 190 / 126 | 316 / 0 | 206 / 110 | 309 / 7 | 312 / 4 | 304 / 12 |
| IMF | 438 | 436 / 2 | 436 / 2 | 436 / 2 | 166 / 272 | 221 / 211 | 429 / 9 | 310 / 128 | 435 / 3 | 429 / 9 | 419 / 19 |
| IMG | 179 | 177 / 2 | 178 / 1 | 178 / 1 | 98 / 81 | 106 / 73 | 179 / 0 | 121 / 58 | 170 / 9 | 176 / 3 | 167 / 12 |

## 3. How close, signed gap to the past model

Tool less key, on the plots of the tables. Median and the 10th and 90th percentile in mm, and the share within 5, 30 and 100 mm.

| Zone | Field | Median | P10 | P90 | Within 5 | Within 30 | Within 100 |
|---|---|---|---|---|---|---|---|
| IMC | FFL | -20.0 | -92.0 | 13.0 | 29% | 46% | 92% |
| IMC | El @ Entry | -0.1 | -63.5 | 14.9 | 47% | 82% | 93% |
| IMC | Ramp front | 0.5 | -47.0 | 2.0 | 68% | 80% | 94% |
| IMC | Ramp back | -2.0 | -84.0 | -0.5 | 53% | 64% | 92% |
| IMD | FFL | -1.0 | -82.0 | 8.0 | 40% | 67% | 92% |
| IMD | El @ Entry | 0.4 | -70.3 | 9.9 | 50% | 81% | 92% |
| IMD | Ramp front | 0.5 | -40.0 | 2.5 | 69% | 86% | 94% |
| IMD | Ramp back | -2.0 | -80.0 | 0.5 | 50% | 62% | 91% |
| IME | FFL | -1.0 | -61.0 | 19.0 | 46% | 69% | 98% |
| IME | El @ Entry | 1.1 | -14.2 | 19.1 | 60% | 88% | 98% |
| IME | Ramp front | 0.5 | -16.0 | 2.0 | 79% | 96% | 100% |
| IME | Ramp back | -1.5 | -60.0 | 0.5 | 58% | 75% | 98% |
| IMF | FFL | -4.0 | -81.0 | 23.0 | 38% | 65% | 92% |
| IMF | El @ Entry | 1.9 | -15.9 | 23.9 | 51% | 89% | 99% |
| IMF | Ramp front | 1.0 | -21.5 | 3.5 | 71% | 95% | 100% |
| IMF | Ramp back | -2.5 | -61.0 | 0.0 | 57% | 81% | 100% |
| IMG | FFL | 0.0 | -36.0 | 24.0 | 55% | 79% | 99% |
| IMG | El @ Entry | 1.4 | -15.6 | 11.4 | 59% | 92% | 100% |
| IMG | Ramp front | 0.5 | -0.5 | 1.5 | 90% | 97% | 100% |
| IMG | Ramp back | -1.0 | -60.0 | 0.5 | 57% | 68% | 100% |

Ramp front and back are means of the two corners, the front being those nearest the two low corners of the key. Steps, tool against key:

| Zone | Same | 1 off | More | Tool higher | Tool lower |
|---|---|---|---|---|---|
| IMC | 307 | 17 | 0 | 6 | 11 |
| IMD | 392 | 8 | 0 | 4 | 4 |
| IME | 316 | 0 | 0 | 0 | 0 |
| IMF | 403 | 26 | 9 | 10 | 25 |
| IMG | 177 | 2 | 0 | 1 | 1 |

## 4. What differs between the modes

- Yard floors on the plots written, rev6: 0 (R410). Each plot also carries the cluster proposal field of R411, which sets no value, the proposals themselves not compared here.
- Wall ends differ on part of the walls, on IMC 23 walls and 92 coordinates. The Walls columns of section 2 show the effect on the counts.

## 5. Second check

By a script outside git, plain json and math, with no function of zone_compare.py (RAN): every status of the rev6 plans worked out again, 16,570 the same, 0 differ. The 50 random plots read again from the raw plans, 250 cells the same, 0 differ. The plans of the past job and the keys are those of steps/investigations/6b-data.md, checked there.

## 6. Zone by zone

### IMC (TU)

Rev6, no override list: plots compared 324, only in the tool plan 0, only in the key 0.
Without overrides: plots compared 324, only in the tool plan 0, only in the key 0.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 322 0 2 0 0 | 322 0 2 0 0 |
| mirror | 312 0 12 0 0 | 312 0 12 0 0 |
| rotation | 312 0 12 0 0 | 312 0 12 0 0 |
| ffl | 22 72 230 0 0 | 22 72 230 0 0 |
| entry | 17 135 162 10 0 | 17 135 162 10 0 |
| steps | 307 17 0 0 0 | 307 17 0 0 0 |
| rampCornersLevel | 156 28 140 0 0 | 156 28 140 0 0 |
| rampCornersPlan | 223 97 4 0 0 | 223 97 4 0 0 |
| wallsToolOnKey | 72 243 9 0 0 | 71 244 9 0 0 |
| wallsKeyOnTool | 112 176 36 0 0 | 115 173 36 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: 2513 differs, 2514 differs
- mirror: 2291 differs, 2292 differs, 2293 differs, 2297 differs, 2298 differs, 2299 differs, 2302 differs, 2340 differs, 2534 differs, 2535 differs
- rotation: 2291 180.00 deg, 2292 180.00 deg, 2293 180.00 deg, 2297 180.00 deg, 2298 180.00 deg, 2299 180.00 deg, 2302 180.00 deg, 2340 180.00 deg, 2546 180.00 deg, 2534 180.00 deg
- ffl: 2608 511.0 mm, 2609 360.0 mm, 2601 328.0 mm, 2606 328.0 mm, 2607 328.0 mm, 2610 307.0 mm, 2319 233.0 mm, 2320 233.0 mm, 2301 214.0 mm, 2326 197.0 mm
- entry: 2608 511.0 mm, 2607 407.8 mm, 2601 328.4 mm, 2610 307.3 mm, 2606 292.8 mm, 2609 249.3 mm, 2605 183.8 mm, 2611 165.7 mm, 2349 164.0 mm, 2320 152.7 mm
- steps: 2298 1, 2301 1, 2319 1, 2327 1, 2329 1, 2331 1, 2335 1, 2339 1, 2342 1, 2344 1
- rampCornersLevel: 2608 599.0 mm, 2609 598.0 mm, 2607 464.0 mm, 2610 407.0 mm, 2606 382.0 mm, 2601 369.0 mm, 2605 239.0 mm, 2600 237.0 mm, 2611 235.0 mm, 2349 229.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 4000.0 mm, 2546 3900.8 mm, 2548 3899.1 mm, 2555 2212.7 mm, 2459 2212.5 mm, 2569 2211.8 mm, 2447 2211.6 mm, 2374 2099.7 mm, 2391 2099.7 mm, 2611 199.8 mm
- wallsKeyOnTool: 2491 25000.6 mm, 2476 24999.5 mm, 2549 11899.4 mm, 2528 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4404.2 mm, 2546 4100.6 mm, 2547 4099.8 mm, 2543 4099.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 2513 differs, 2514 differs
- mirror: 2291 differs, 2292 differs, 2293 differs, 2297 differs, 2298 differs, 2299 differs, 2302 differs, 2340 differs, 2534 differs, 2535 differs
- rotation: 2291 180.00 deg, 2292 180.00 deg, 2293 180.00 deg, 2297 180.00 deg, 2298 180.00 deg, 2299 180.00 deg, 2302 180.00 deg, 2340 180.00 deg, 2546 180.00 deg, 2534 180.00 deg
- ffl: 2608 511.0 mm, 2609 360.0 mm, 2601 328.0 mm, 2606 328.0 mm, 2607 328.0 mm, 2610 307.0 mm, 2319 233.0 mm, 2320 233.0 mm, 2301 214.0 mm, 2326 197.0 mm
- entry: 2608 511.0 mm, 2607 407.8 mm, 2601 328.4 mm, 2610 307.3 mm, 2606 292.8 mm, 2609 249.3 mm, 2605 183.8 mm, 2611 165.7 mm, 2349 164.0 mm, 2320 152.7 mm
- steps: 2298 1, 2301 1, 2319 1, 2327 1, 2329 1, 2331 1, 2335 1, 2339 1, 2342 1, 2344 1
- rampCornersLevel: 2608 599.0 mm, 2609 598.0 mm, 2607 464.0 mm, 2610 407.0 mm, 2606 382.0 mm, 2601 369.0 mm, 2605 239.0 mm, 2600 237.0 mm, 2611 235.0 mm, 2349 229.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 4000.0 mm, 2546 3900.8 mm, 2548 3899.1 mm, 2555 2212.7 mm, 2459 2212.5 mm, 2569 2211.8 mm, 2447 2211.6 mm, 2374 2099.7 mm, 2391 2099.7 mm, 2409 144.4 mm
- wallsKeyOnTool: 2491 25000.6 mm, 2476 24999.5 mm, 2549 11899.4 mm, 2528 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4545.8 mm, 2546 4100.6 mm, 2547 4099.8 mm, 2543 4099.2 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imc.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2328 | same / same | same / same | 0.00 deg / 0.00 deg | 149.0 mm / 149.0 mm | 79.2 mm / 79.2 mm | 0 / 0 | 164.0 mm / 164.0 mm | 1.0 mm / 1.0 mm | 99.0 mm / 99.0 mm | 1.0 mm / 1.0 mm |
| 2345 | same / same | same / same | 0.00 deg / 0.00 deg | 13.0 mm / 13.0 mm | 12.5 mm / 12.5 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 140.7 mm / 140.7 mm | 100.5 mm / 100.5 mm |
| 2425 | same / same | same / same | 0.00 deg / 0.00 deg | 42.0 mm / 42.0 mm | 1.8 mm / 1.8 mm | 0 / 0 | 42.0 mm / 42.0 mm | 0.0 mm / 0.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2429 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2437 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 5.0 mm / 5.0 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 2449 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 1.6 mm / 1.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 101.2 mm / 101.2 mm |
| 2455 | same / same | same / same | 0.00 deg / 0.00 deg | 32.0 mm / 32.0 mm | 1.7 mm / 1.7 mm | 0 / 0 | 32.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 2530 | same / same | same / same | 0.00 deg / 0.00 deg | 71.0 mm / 71.0 mm | 1.3 mm / 1.3 mm | 0 / 0 | 71.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 10099.5 mm / 10099.5 mm |
| 2571 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 2.6 mm / 2.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 101.0 mm / 101.0 mm |
| 2617 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 1.7 mm / 1.7 mm | 0 / 0 | 41.0 mm / 41.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 1.0 mm / 1.0 mm |

### IMD (TU)

Rev6, no override list: plots compared 400, only in the tool plan 0, only in the key 3.
Without overrides: plots compared 400, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 396 0 4 0 0 | 396 0 4 0 0 |
| mirror | 398 0 2 0 0 | 398 0 2 0 0 |
| rotation | 398 0 2 0 0 | 398 0 2 0 0 |
| ffl | 40 122 238 0 0 | 40 122 238 0 0 |
| entry | 37 161 197 5 0 | 37 161 197 5 0 |
| steps | 392 8 0 0 0 | 392 8 0 0 0 |
| rampCornersLevel | 173 54 173 0 0 | 173 54 173 0 0 |
| rampCornersPlan | 247 148 5 0 0 | 247 148 5 0 0 |
| wallsToolOnKey | 136 253 11 0 0 | 132 257 11 0 0 |
| wallsKeyOnTool | 173 183 44 0 0 | 175 181 44 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: 1297 differs, 1298 differs, 1392 differs, 1393 differs
- mirror: 1268 differs, 1403 differs
- rotation: 1403 180.00 deg, 1268 180.00 deg
- ffl: 1272 226.0 mm, 1273 226.0 mm, 1170 223.0 mm, 1171 223.0 mm, 1304 198.0 mm, 1274 193.0 mm, 1298 185.0 mm, 1299 185.0 mm, 1338 177.0 mm, 1173 174.0 mm
- entry: 1272 233.8 mm, 1273 225.6 mm, 1171 223.1 mm, 1304 198.4 mm, 1274 192.7 mm, 1173 184.8 mm, 1174 174.0 mm, 1277 173.3 mm, 1300 172.6 mm, 1302 172.5 mm
- steps: 1156 1, 1250 1, 1311 1, 1312 1, 1322 1, 1338 1, 1371 1, 1436 1
- rampCornersLevel: 1272 313.0 mm, 1173 283.0 mm, 1273 272.0 mm, 1171 257.0 mm, 1299 214.0 mm, 1303 214.0 mm, 1170 208.0 mm, 1301 204.0 mm, 1276 203.0 mm, 1271 201.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1197 2213.0 mm, 1510 2213.0 mm, 1378 2212.5 mm, 1366 2211.3 mm, 1280 2002.3 mm, 1485 2001.1 mm, 1403 2000.2 mm, 1227 600.8 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 3910.7 mm, 1246 3910.3 mm, 1287 2400.7 mm, 1437 2010.0 mm, 1452 2009.6 mm, 1469 2005.3 mm, 1482 2004.7 mm, 1485 2001.3 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 1297 differs, 1298 differs, 1392 differs, 1393 differs
- mirror: 1268 differs, 1403 differs
- rotation: 1403 180.00 deg, 1268 180.00 deg
- ffl: 1272 226.0 mm, 1273 226.0 mm, 1170 223.0 mm, 1171 223.0 mm, 1304 198.0 mm, 1274 193.0 mm, 1298 185.0 mm, 1299 185.0 mm, 1338 177.0 mm, 1173 174.0 mm
- entry: 1272 233.8 mm, 1273 225.6 mm, 1171 223.1 mm, 1304 198.4 mm, 1274 192.7 mm, 1173 184.8 mm, 1174 174.0 mm, 1277 173.3 mm, 1300 172.6 mm, 1302 172.5 mm
- steps: 1156 1, 1250 1, 1311 1, 1312 1, 1322 1, 1338 1, 1371 1, 1436 1
- rampCornersLevel: 1272 313.0 mm, 1173 283.0 mm, 1273 272.0 mm, 1171 257.0 mm, 1299 214.0 mm, 1303 214.0 mm, 1170 208.0 mm, 1301 204.0 mm, 1276 203.0 mm, 1271 201.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1197 2213.0 mm, 1510 2213.0 mm, 1378 2212.5 mm, 1366 2211.3 mm, 1280 2002.3 mm, 1485 2001.1 mm, 1403 2000.2 mm, 1227 600.8 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 4011.0 mm, 1246 4010.4 mm, 1287 2501.0 mm, 1437 2010.0 mm, 1452 2009.6 mm, 1469 2005.3 mm, 1482 2004.7 mm, 1485 2001.3 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imd.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1198 | same / same | same / same | 0.00 deg / 0.00 deg | 28.0 mm / 28.0 mm | 28.4 mm / 28.4 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 141.7 mm / 141.7 mm | 101.1 mm / 101.1 mm |
| 1270 | same / same | same / same | 0.00 deg / 0.00 deg | 69.0 mm / 69.0 mm | 98.8 mm / 98.8 mm | 0 / 0 | 129.0 mm / 129.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.7 mm / 99.7 mm |
| 1274 | same / same | same / same | 0.00 deg / 0.00 deg | 193.0 mm / 193.0 mm | 192.7 mm / 192.7 mm | 0 / 0 | 174.0 mm / 174.0 mm | 1.0 mm / 1.0 mm | 140.9 mm / 140.9 mm | 100.7 mm / 100.7 mm |
| 1284 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.7 mm / 0.7 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 101.1 mm / 101.1 mm |
| 1418 | same / same | same / same | 0.00 deg / 0.00 deg | 10.0 mm / 10.0 mm | 9.5 mm / 9.5 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm |
| 1464 | same / same | same / same | 0.00 deg / 0.00 deg | 7.0 mm / 7.0 mm | 8.0 mm / 8.0 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 1482 | same / same | same / same | 0.00 deg / 0.00 deg | 6.0 mm / 6.0 mm | 6.0 mm / 6.0 mm | 0 / 0 | 12.0 mm / 12.0 mm | 1.4 mm / 1.4 mm | 140.7 mm / 140.7 mm | 2004.7 mm / 2004.7 mm |
| 1512 | same / same | same / same | 0.00 deg / 0.00 deg | 4.0 mm / 4.0 mm | 4.4 mm / 4.4 mm | 0 / 0 | 6.0 mm / 6.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm | 99.6 mm / 99.6 mm |
| 1529 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 2.3 mm / 2.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1539 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 2.5 mm / 2.5 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |

### IME (TU)

Rev6, no override list: plots compared 316, only in the tool plan 0, only in the key 4.
Without overrides: plots compared 316, only in the tool plan 0, only in the key 4.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 314 0 2 0 0 | 314 0 2 0 0 |
| mirror | 308 0 8 0 0 | 308 0 8 0 0 |
| rotation | 314 0 2 0 0 | 314 0 2 0 0 |
| ffl | 51 94 171 0 0 | 51 94 171 0 0 |
| entry | 31 159 126 0 0 | 31 159 126 0 0 |
| steps | 316 0 0 0 0 | 316 0 0 0 0 |
| rampCornersLevel | 161 45 110 0 0 | 161 45 110 0 0 |
| rampCornersPlan | 217 92 7 0 0 | 217 92 7 0 0 |
| wallsToolOnKey | 85 227 4 0 0 | 82 230 4 0 0 |
| wallsKeyOnTool | 110 194 12 0 0 | 111 193 12 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: 2771 differs, 2772 differs
- mirror: 2786 differs, 2787 differs, 2892 differs, 2893 differs, 2894 differs, 2895 differs, 2896 differs, 2897 differs
- rotation: 2787 180.00 deg, 2786 180.00 deg
- ffl: 2893 241.0 mm, 2892 227.0 mm, 2894 214.0 mm, 2895 214.0 mm, 2896 162.0 mm, 2897 109.0 mm, 2913 103.0 mm, 2714 91.0 mm, 2767 86.0 mm, 2772 84.0 mm
- entry: 2893 241.2 mm, 2892 227.2 mm, 2894 213.5 mm, 2895 196.6 mm, 2896 162.2 mm, 2897 109.4 mm, 2913 103.4 mm, 2714 91.3 mm, 2759 90.7 mm, 2767 85.7 mm
- steps: none
- rampCornersLevel: 2892 225.0 mm, 2893 224.0 mm, 2894 221.0 mm, 2896 184.0 mm, 2895 174.0 mm, 2897 156.0 mm, 2666 138.0 mm, 2854 130.0 mm, 2759 126.0 mm, 2949 93.0 mm
- rampCornersPlan: 2892 19500.4 mm, 2897 19500.4 mm, 2893 19480.7 mm, 2894 19480.7 mm, 2895 19480.7 mm, 2896 19480.7 mm, 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2698 2212.5 mm, 2829 2211.6 mm, 2894 159.5 mm, 2895 159.5 mm, 2891 159.0 mm, 2703 141.9 mm, 2897 141.9 mm, 2705 141.9 mm
- wallsKeyOnTool: 2646 3655.2 mm, 2645 3654.5 mm, 2879 2404.1 mm, 2766 2005.3 mm, 2741 2004.7 mm, 2752 2004.7 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2719 1505.6 mm, 2730 1505.4 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 2771 differs, 2772 differs
- mirror: 2786 differs, 2787 differs, 2892 differs, 2893 differs, 2894 differs, 2895 differs, 2896 differs, 2897 differs
- rotation: 2787 180.00 deg, 2786 180.00 deg
- ffl: 2893 241.0 mm, 2892 227.0 mm, 2894 214.0 mm, 2895 214.0 mm, 2896 162.0 mm, 2897 109.0 mm, 2913 103.0 mm, 2714 91.0 mm, 2767 86.0 mm, 2772 84.0 mm
- entry: 2893 241.2 mm, 2892 227.2 mm, 2894 213.5 mm, 2895 196.6 mm, 2896 162.2 mm, 2897 109.4 mm, 2913 103.4 mm, 2714 91.3 mm, 2759 90.7 mm, 2767 85.7 mm
- steps: none
- rampCornersLevel: 2892 225.0 mm, 2893 224.0 mm, 2894 221.0 mm, 2896 184.0 mm, 2895 174.0 mm, 2897 156.0 mm, 2666 138.0 mm, 2854 130.0 mm, 2759 126.0 mm, 2949 93.0 mm
- rampCornersPlan: 2892 19500.4 mm, 2897 19500.4 mm, 2893 19480.7 mm, 2894 19480.7 mm, 2895 19480.7 mm, 2896 19480.7 mm, 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2698 2212.5 mm, 2829 2211.6 mm, 2894 159.5 mm, 2895 159.5 mm, 2891 159.0 mm, 2703 141.9 mm, 2897 141.9 mm, 2705 141.9 mm
- wallsKeyOnTool: 2646 3755.3 mm, 2645 3754.8 mm, 2879 2504.2 mm, 2766 2005.3 mm, 2741 2004.7 mm, 2752 2004.7 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2719 1505.6 mm, 2730 1505.4 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_ime.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2669 | same / same | same / same | 0.00 deg / 0.00 deg | 4.0 mm / 4.0 mm | 3.7 mm / 3.7 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.9 mm / 100.9 mm |
| 2686 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 2.3 mm / 2.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 100.0 mm / 100.0 mm | 100.1 mm / 100.1 mm |
| 2760 | same / same | same / same | 0.00 deg / 0.00 deg | 45.0 mm / 45.0 mm | 15.4 mm / 15.4 mm | 0 / 0 | 32.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2764 | same / same | same / same | 0.00 deg / 0.00 deg | 52.0 mm / 52.0 mm | 2.2 mm / 2.2 mm | 0 / 0 | 51.0 mm / 51.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm |
| 2772 | differs / differs | same / same | 0.00 deg / 0.00 deg | 84.0 mm / 84.0 mm | 12.6 mm / 12.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2783 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 2.8 mm / 2.8 mm | 0 / 0 | 71.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2789 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 2.7 mm / 2.7 mm | 0 / 0 | 71.0 mm / 71.0 mm | 0.0 mm / 0.0 mm | 0.9 mm / 0.9 mm | 0.9 mm / 0.9 mm |
| 2864 | same / same | same / same | 0.00 deg / 0.00 deg | 16.0 mm / 16.0 mm | 13.6 mm / 13.6 mm | 0 / 0 | 22.0 mm / 22.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2903 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 2949 | same / same | same / same | 0.00 deg / 0.00 deg | 79.0 mm / 79.0 mm | 78.6 mm / 78.6 mm | 0 / 0 | 93.0 mm / 93.0 mm | 1.4 mm / 1.4 mm | 100.3 mm / 100.3 mm | 100.7 mm / 100.7 mm |

### IMF (TU)

Rev6, no override list: plots compared 438, only in the tool plan 0, only in the key 38.
Without overrides: plots compared 462, only in the tool plan 0, only in the key 14.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 436 0 2 0 0 | 460 0 2 0 0 |
| mirror | 436 0 2 0 0 | 460 0 2 0 0 |
| rotation | 436 0 2 0 0 | 460 0 2 0 0 |
| ffl | 28 138 272 0 0 | 32 148 282 0 0 |
| entry | 23 198 211 6 0 | 25 209 222 6 0 |
| steps | 403 26 9 0 0 | 425 27 10 0 0 |
| rampCornersLevel | 227 83 128 0 0 | 240 83 139 0 0 |
| rampCornersPlan | 286 149 3 0 0 | 303 156 3 0 0 |
| wallsToolOnKey | 128 301 9 0 0 | 130 323 9 0 0 |
| wallsKeyOnTool | 178 241 19 0 0 | 196 245 21 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: 1062 differs, 1063 differs
- mirror: 739 differs, 755 differs
- rotation: 755 180.00 deg, 739 180.00 deg
- ffl: 1008 400.0 mm, 1009 400.0 mm, 990 379.0 mm, 998 379.0 mm, 1094 360.0 mm, 1095 360.0 mm, 1061 322.0 mm, 1062 322.0 mm, 910 321.0 mm, 918 320.0 mm
- entry: 986 138.1 mm, 975 134.7 mm, 934 131.9 mm, 1046 116.8 mm, 958 114.6 mm, 1075 98.9 mm, 1052 95.7 mm, 1068 95.3 mm, 946 90.0 mm, 1069 88.5 mm
- steps: 1008 3, 910 2, 918 2, 990 2, 998 2, 1009 2, 1058 2, 1062 2, 1094 2, 685 1
- rampCornersLevel: 974 180.0 mm, 673 175.0 mm, 654 147.0 mm, 741 138.0 mm, 887 97.0 mm, 826 82.0 mm, 829 82.0 mm, 886 82.0 mm, 912 82.0 mm, 920 82.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 1052 2212.2 mm, 836 2211.9 mm, 849 2211.8 mm, 1069 2211.6 mm, 865 1999.2 mm, 817 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 654 3641.3 mm, 640 3615.8 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 1062 differs, 1063 differs
- mirror: 739 differs, 755 differs
- rotation: 755 180.00 deg, 739 180.00 deg
- ffl: 1008 400.0 mm, 1009 400.0 mm, 990 379.0 mm, 998 379.0 mm, 704 363.0 mm, 1094 360.0 mm, 1095 360.0 mm, 1061 322.0 mm, 1062 322.0 mm, 910 321.0 mm
- entry: 986 138.1 mm, 975 134.7 mm, 934 131.9 mm, 1046 116.8 mm, 958 114.6 mm, 1075 98.9 mm, 1052 95.7 mm, 1068 95.3 mm, 946 90.0 mm, 1069 88.5 mm
- steps: 1008 3, 704 2, 910 2, 918 2, 990 2, 998 2, 1009 2, 1058 2, 1062 2, 1094 2
- rampCornersLevel: 974 180.0 mm, 673 175.0 mm, 694 161.0 mm, 713 149.0 mm, 654 147.0 mm, 741 138.0 mm, 887 97.0 mm, 707 84.0 mm, 1010 83.0 mm, 826 82.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 1052 2212.2 mm, 836 2211.9 mm, 849 2211.8 mm, 1069 2211.6 mm, 865 1999.2 mm, 817 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 713 3755.1 mm, 640 3754.0 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imf.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 716 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.2 mm / 0.2 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.8 mm / 99.8 mm | 1.2 mm / 1.2 mm |
| 791 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 1.3 mm / 1.3 mm | 0 / 0 | 4.0 mm / 4.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 504.9 mm / 504.9 mm |
| 796 | same / same | same / same | 0.00 deg / 0.00 deg | 19.0 mm / 19.0 mm | 9.1 mm / 9.1 mm | 0 / 0 | 81.0 mm / 81.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 804 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 2.8 mm / 2.8 mm | 0 / 0 | 81.0 mm / 81.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm | 99.5 mm / 99.5 mm |
| 952 | same / same | same / same | 0.00 deg / 0.00 deg | 16.0 mm / 16.0 mm | 16.2 mm / 16.2 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.2 mm / 100.2 mm |
| 998 | same / same | same / same | 0.00 deg / 0.00 deg | 379.0 mm / 379.0 mm | 26.9 mm / 26.9 mm | 2 / 2 | 72.0 mm / 72.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 1022 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 3.3 mm / 3.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.8 mm / 99.8 mm |
| 1048 | same / same | same / same | 0.00 deg / 0.00 deg | 12.0 mm / 12.0 mm | 5.4 mm / 5.4 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 99.0 mm / 99.0 mm | 100.1 mm / 100.1 mm |
| 1065 | same / same | same / same | 0.00 deg / 0.00 deg | 13.0 mm / 13.0 mm | 5.0 mm / 5.0 mm | 0 / 0 | 2.0 mm / 2.0 mm | 0.0 mm / 0.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 1075 | same / same | same / same | 0.00 deg / 0.00 deg | 99.0 mm / 99.0 mm | 98.9 mm / 98.9 mm | 0 / 0 | 20.0 mm / 20.0 mm | 0.0 mm / 0.0 mm | 100.8 mm / 100.8 mm | 100.3 mm / 100.3 mm |

### IMG (TU)

Rev6, no override list: plots compared 179, only in the tool plan 0, only in the key 89.
Without overrides: plots compared 257, only in the tool plan 0, only in the key 11.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 177 0 2 0 0 | 255 0 2 0 0 |
| mirror | 178 0 1 0 0 | 236 0 21 0 0 |
| rotation | 178 0 1 0 0 | 245 2 10 0 0 |
| ffl | 51 47 81 0 0 | 75 60 122 0 0 |
| entry | 32 74 73 0 0 | 46 99 112 0 0 |
| steps | 177 2 0 0 0 | 249 8 0 0 0 |
| rampCornersLevel | 99 22 58 0 0 | 141 29 87 0 0 |
| rampCornersPlan | 107 63 9 0 0 | 149 82 26 0 0 |
| wallsToolOnKey | 53 123 3 0 0 | 56 196 5 0 0 |
| wallsKeyOnTool | 59 108 12 0 0 | 73 164 20 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: 566 differs, 567 differs
- mirror: 457 differs
- rotation: 457 180.00 deg
- ffl: 485 188.0 mm, 515 77.0 mm, 519 77.0 mm, 567 77.0 mm, 579 77.0 mm, 580 77.0 mm, 571 74.0 mm, 543 68.0 mm, 530 64.0 mm, 531 64.0 mm
- entry: 633 56.4 mm, 453 53.8 mm, 534 48.4 mm, 524 47.4 mm, 497 45.7 mm, 489 43.7 mm, 632 41.7 mm, 613 38.4 mm, 455 37.7 mm, 623 34.9 mm
- steps: 485 1, 580 1
- rampCornersLevel: 585 163.0 mm, 455 97.0 mm, 623 82.0 mm, 579 81.0 mm, 451 81.0 mm, 567 80.0 mm, 571 80.0 mm, 515 80.0 mm, 519 80.0 mm, 629 79.0 mm
- rampCornersPlan: 552 2000.2 mm, 585 95.4 mm, 441 56.1 mm, 442 56.1 mm, 446 56.1 mm, 447 55.6 mm, 444 55.6 mm, 443 55.6 mm, 445 54.7 mm, 510 5.8 mm
- wallsToolOnKey: 546 2212.2 mm, 592 2211.8 mm, 552 2002.3 mm, 446 184.2 mm, 441 161.5 mm, 445 155.6 mm, 447 155.0 mm, 618 141.7 mm, 448 141.7 mm, 461 141.7 mm
- wallsKeyOnTool: 576 17790.5 mm, 592 17789.3 mm, 568 3173.5 mm, 455 2405.8 mm, 454 2399.4 mm, 566 2099.2 mm, 552 2000.2 mm, 602 1999.7 mm, 488 1000.2 mm, 510 920.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 566 differs, 567 differs
- mirror: 457 differs, 2998 differs, 2999 differs, 3000 differs, 3001 differs, 3002 differs, 3003 differs, 3005 differs, 3006 differs, 3007 differs
- rotation: 457 180.00 deg, 2998 180.00 deg, 3000 180.00 deg, 3001 180.00 deg, 3031 180.00 deg, 3003 180.00 deg, 3030 180.00 deg, 3002 180.00 deg, 2999 180.00 deg, 2950 1.00 deg
- ffl: 3011 248.0 mm, 3012 248.0 mm, 3013 248.0 mm, 3014 248.0 mm, 3005 244.0 mm, 3006 244.0 mm, 3015 194.0 mm, 3016 194.0 mm, 485 188.0 mm, 3025 163.0 mm
- entry: 3006 234.4 mm, 3015 220.8 mm, 3014 196.9 mm, 3016 184.6 mm, 3007 183.0 mm, 3005 162.8 mm, 3013 147.3 mm, 3008 133.4 mm, 3012 104.8 mm, 3009 97.7 mm
- steps: 485 1, 580 1, 2953 1, 3007 1, 3008 1, 3011 1, 3025 1, 3026 1
- rampCornersLevel: 3014 317.0 mm, 3007 292.0 mm, 3008 279.0 mm, 3016 271.0 mm, 3006 267.0 mm, 3015 262.0 mm, 3005 259.0 mm, 3013 227.0 mm, 3012 208.0 mm, 585 163.0 mm
- rampCornersPlan: 3005 19481.2 mm, 3006 19481.2 mm, 3007 19481.2 mm, 3008 19481.2 mm, 3013 19481.2 mm, 3014 19481.2 mm, 3015 19481.2 mm, 3016 19481.2 mm, 3009 19481.2 mm, 3011 19480.7 mm
- wallsToolOnKey: 546 2212.2 mm, 592 2211.8 mm, 2953 2002.5 mm, 552 2002.3 mm, 3004 531.4 mm, 3009 279.6 mm, 3010 279.6 mm, 3011 279.6 mm, 3012 279.6 mm, 3007 278.7 mm
- wallsKeyOnTool: 576 17790.5 mm, 592 17789.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 568 3173.5 mm, 455 2505.9 mm, 454 2499.7 mm, 566 2099.2 mm, 3003 2099.2 mm, 2953 2000.2 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_img.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 459 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.4 mm / 0.4 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 101.0 mm / 101.0 mm |
| 470 | same / same | same / same | 0.00 deg / 0.00 deg | 51.0 mm / 51.0 mm | 1.8 mm / 1.8 mm | 0 / 0 | 51.0 mm / 51.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm | 0.9 mm / 0.9 mm |
| 518 | same / same | same / same | 0.00 deg / 0.00 deg | 46.0 mm / 46.0 mm | 7.1 mm / 7.1 mm | 0 / 0 | 1.0 mm / 1.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 520 | same / same | same / same | 0.00 deg / 0.00 deg | 5.0 mm / 5.0 mm | 4.4 mm / 4.4 mm | 0 / 0 | 40.0 mm / 40.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 524 | same / same | same / same | 0.00 deg / 0.00 deg | 47.0 mm / 47.0 mm | 47.4 mm / 47.4 mm | 0 / 0 | 46.0 mm / 46.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 529 | same / same | same / same | 0.00 deg / 0.00 deg | 36.0 mm / 36.0 mm | 6.5 mm / 6.5 mm | 0 / 0 | 30.0 mm / 30.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.5 mm / 99.5 mm |
| 592 | same / same | same / same | 0.00 deg / 0.00 deg | 19.0 mm / 19.0 mm | 19.0 mm / 19.0 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 2211.8 mm / 2211.8 mm | 17789.3 mm / 17789.3 mm |
| 615 | same / same | same / same | 0.00 deg / 0.00 deg | 8.0 mm / 8.0 mm | 8.3 mm / 8.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 625 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 1.5 mm / 1.5 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 100.2 mm / 100.2 mm | 100.0 mm / 100.0 mm |
| 628 | same / same | same / same | 0.00 deg / 0.00 deg | 16.0 mm / 16.0 mm | 15.8 mm / 15.8 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm | 100.3 mm / 100.3 mm |
