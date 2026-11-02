# Round FIX-2NOV-D, several road surfaces, the proof

Lane D, 2 Nov 2026. Under rev6 with no override list, the list of config in its order, the same six surfaces and order as siteBuild.roadSurfaces (CHOSEN). Run folder round_addin\logs\p2nov_d, config copies outside git differing by mode alone, or by mode and the old one surface key. Counts, gaps and plot numbers only.

## Result

| Zone | Key plots | Written | Left out | One surface named, 6B-DATA-REV6 | One surface as the config stood |
|---|---|---|---|---|---|
| IMC | 324 | 324 | 0 | 324 | 324 |
| IMD | 403 | 400 | 3 | 400 | 0 |
| IME | 320 | 316 | 4 | 316 | 0 |
| IMF | 476 | 462 | 14 | 438 | 0 |
| IMG | 268 | 257 | 11 | 179 | 0 |
| All | 1,791 | 1,759 | 32 | 1,657 | 324 |

- On all 1,759 written plots FFL, El @ Entry, steps and the ramp corners equal the past job rules with no list, 0 differ (RAN, analyse.py, plain code, no function of the tool, the ramp block compared whole).
- The past job rules write the same 1,759 and leave out the same 32 (RAN).
- Whole project under rev6: 2,512 of 2,704 written, 35 with no road level, no zone over the limit, no stop (RAN).
- Past job plans byte equal to main at 015a253, on the 5 zones with and without the list and the whole project with it: 11 of 11, 0 differ (RAN). PR 141, 143 and 144 merged after that base change no plan code (READ, the diff).
- Only 4 written plots take their two corners from two surfaces, all on IMF (RAN). Of the 38 of IMF that 6B-DATA-REV6 left out, 24 are now written and 14 stay out, of the 89 of IMG 78 and 11. So nearly all of them lay wholly on another surface, not across two.

## Plots still left out, 32

Each corner named has no level on any of the six surfaces within snapM, so it is listed in noRoadLevel with the surfaces tried. Two have no front frame, so no corner to ask. Five are notched plots of Q299: IMD 1172, IME 2626, IME 2665, IMF 727, IMG 469.

- IMD, 3: 1172, 1491, 1492
- IME, 4: 2626, 2665, 2690 (no front frame), 2699
- IMF, 14: 653, 693, 727, 769, 770, 792, 805, 813, 827, 828, 857, 1092, 1093, 1129
- IMG, 11: 428, 429, 430, 468, 469, 475, 511, 584, 622 (no front frame), 626, 639

## The stop on real inputs

A config with the old key, one surface, Top Surface-02:

| Run | Exit | Plan | Message |
|---|---|---|---|
| IMC, Python | 0 | written | none, 0 of 324 left out |
| IMD, Python | 2 | none | Zone 2: 403 of 403 plots have no road level on the road surfaces named (Top Surface-02). What to do: add the road surface of this zone in Set up. (R493) |
| IME, Python | 2 | none | Zone 3: 319 of 320, the same words |
| IMF, Python | 2 | none | Zone 3: 476 of 476, the same words |
| IMG, Python | 2 | none | Zone 4: 267 of 268, the same words |
| Whole project, Python | 2 | none | one sentence per zone, Zone 1: 433 of 727, Zone 2: 410 of 746, and on |
| IMD, C# worker with the site input, net48 and net8.0 | 2 | none | the same line as Python, 403 of 403 |
| IMD with the list of six, C# worker, net48 and net8.0 | 0 | written | none |

IMC stops on nothing because that one surface does cover it. Under Zone 2 taken whole, with IMD, it stops.

## Parity and inputs

13 steps, net48 and net8.0, whole project and the 7 keys, past job and rev6 with the list, 32 runs, plus IMC under the old one surface key, 2 runs: 34 runs, 442 step lines, 0 failures (RAN). The rev6 read ran fresh on both runtimes. 62 inputs the same by SHA-256 before and after the proof, 15 inputs of each rev6 read the same (RAN).

## Zone tables, limits of 6B-DATA, after PR 142

### IMC (TU)

Rev6, no override list: plots compared 324, only in the tool plan 0, only in the key 0.
Without overrides: plots compared 324, only in the tool plan 0, only in the key 0.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 324 0 0 0 0 | 324 0 0 0 0 |
| mirror | 312 0 12 0 0 | 312 0 12 0 0 |
| rotation | 312 0 12 0 0 | 312 0 12 0 0 |
| ffl | 22 72 230 0 0 | 22 72 230 0 0 |
| entry | 17 135 162 10 0 | 17 135 162 10 0 |
| steps | 307 17 0 0 0 | 307 17 0 0 0 |
| rampCornersLevel | 156 28 140 0 0 | 156 28 140 0 0 |
| rampCornersPlan | 223 97 4 0 0 | 223 97 4 0 0 |
| wallsToolOnKey | 216 98 10 0 0 | 221 93 10 0 0 |
| wallsKeyOnTool | 176 110 38 0 0 | 181 105 38 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: none
- mirror: 2291 differs, 2292 differs, 2293 differs, 2297 differs, 2298 differs, 2299 differs, 2302 differs, 2340 differs, 2534 differs, 2535 differs
- rotation: 2291 180.00 deg, 2292 180.00 deg, 2293 180.00 deg, 2297 180.00 deg, 2298 180.00 deg, 2299 180.00 deg, 2302 180.00 deg, 2340 180.00 deg, 2546 180.00 deg, 2534 180.00 deg
- ffl: 2608 511.0 mm, 2609 360.0 mm, 2601 328.0 mm, 2606 328.0 mm, 2607 328.0 mm, 2610 307.0 mm, 2319 233.0 mm, 2320 233.0 mm, 2301 214.0 mm, 2326 197.0 mm
- entry: 2608 511.0 mm, 2607 407.8 mm, 2601 328.4 mm, 2610 307.3 mm, 2606 292.8 mm, 2609 249.3 mm, 2605 183.8 mm, 2611 165.7 mm, 2349 164.0 mm, 2320 152.7 mm
- steps: 2298 1, 2301 1, 2319 1, 2327 1, 2329 1, 2331 1, 2335 1, 2339 1, 2342 1, 2344 1
- rampCornersLevel: 2608 599.0 mm, 2609 598.0 mm, 2607 464.0 mm, 2610 407.0 mm, 2606 382.0 mm, 2601 369.0 mm, 2605 239.0 mm, 2600 237.0 mm, 2611 235.0 mm, 2349 229.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 3999.4 mm, 2546 3900.8 mm, 2548 3799.1 mm, 2346 3699.8 mm, 2347 3699.8 mm, 2549 3699.2 mm, 2540 3698.9 mm, 2541 3698.9 mm, 2374 1999.7 mm, 2391 1999.7 mm
- wallsKeyOnTool: 2491 24900.6 mm, 2476 24899.5 mm, 2528 11899.4 mm, 2549 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4350.7 mm, 2547 4099.8 mm, 2546 4000.6 mm, 2543 3999.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 2291 differs, 2292 differs, 2293 differs, 2297 differs, 2298 differs, 2299 differs, 2302 differs, 2340 differs, 2534 differs, 2535 differs
- rotation: 2291 180.00 deg, 2292 180.00 deg, 2293 180.00 deg, 2297 180.00 deg, 2298 180.00 deg, 2299 180.00 deg, 2302 180.00 deg, 2340 180.00 deg, 2546 180.00 deg, 2534 180.00 deg
- ffl: 2608 511.0 mm, 2609 360.0 mm, 2601 328.0 mm, 2606 328.0 mm, 2607 328.0 mm, 2610 307.0 mm, 2319 233.0 mm, 2320 233.0 mm, 2301 214.0 mm, 2326 197.0 mm
- entry: 2608 511.0 mm, 2607 407.8 mm, 2601 328.4 mm, 2610 307.3 mm, 2606 292.8 mm, 2609 249.3 mm, 2605 183.8 mm, 2611 165.7 mm, 2349 164.0 mm, 2320 152.7 mm
- steps: 2298 1, 2301 1, 2319 1, 2327 1, 2329 1, 2331 1, 2335 1, 2339 1, 2342 1, 2344 1
- rampCornersLevel: 2608 599.0 mm, 2609 598.0 mm, 2607 464.0 mm, 2610 407.0 mm, 2606 382.0 mm, 2601 369.0 mm, 2605 239.0 mm, 2600 237.0 mm, 2611 235.0 mm, 2349 229.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 3999.4 mm, 2546 3900.8 mm, 2548 3799.1 mm, 2346 3699.8 mm, 2347 3699.8 mm, 2549 3699.2 mm, 2540 3698.9 mm, 2541 3698.9 mm, 2374 1999.7 mm, 2391 1999.7 mm
- wallsKeyOnTool: 2491 24900.6 mm, 2476 24899.5 mm, 2528 11899.4 mm, 2549 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4492.4 mm, 2547 4099.8 mm, 2546 4000.6 mm, 2543 3999.2 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imc.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2328 | same / same | same / same | 0.00 deg / 0.00 deg | 149.0 mm / 149.0 mm | 79.2 mm / 79.2 mm | 0 / 0 | 164.0 mm / 164.0 mm | 1.0 mm / 1.0 mm | 1.3 mm / 1.3 mm | 1.3 mm / 1.3 mm |
| 2345 | same / same | same / same | 0.00 deg / 0.00 deg | 13.0 mm / 13.0 mm | 12.5 mm / 12.5 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 99.4 mm / 99.4 mm | 0.7 mm / 0.7 mm |
| 2425 | same / same | same / same | 0.00 deg / 0.00 deg | 42.0 mm / 42.0 mm | 1.8 mm / 1.8 mm | 0 / 0 | 42.0 mm / 42.0 mm | 0.0 mm / 0.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2429 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm |
| 2437 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 5.0 mm / 5.0 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.3 mm / 1.3 mm |
| 2449 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 1.6 mm / 1.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm |
| 2455 | same / same | same / same | 0.00 deg / 0.00 deg | 32.0 mm / 32.0 mm | 1.7 mm / 1.7 mm | 0 / 0 | 32.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 2530 | same / same | same / same | 0.00 deg / 0.00 deg | 71.0 mm / 71.0 mm | 1.3 mm / 1.3 mm | 0 / 0 | 71.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 10099.5 mm / 10099.5 mm |
| 2571 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 2.6 mm / 2.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 3.7 mm / 3.7 mm | 100.0 mm / 100.0 mm |
| 2617 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 1.7 mm / 1.7 mm | 0 / 0 | 41.0 mm / 41.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |

### IMD (TU)

Rev6, no override list: plots compared 400, only in the tool plan 0, only in the key 3.
Without overrides: plots compared 400, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 400 0 0 0 0 | 400 0 0 0 0 |
| mirror | 398 0 2 0 0 | 398 0 2 0 0 |
| rotation | 398 0 2 0 0 | 398 0 2 0 0 |
| ffl | 40 122 238 0 0 | 40 122 238 0 0 |
| entry | 37 161 197 5 0 | 37 161 197 5 0 |
| steps | 392 8 0 0 0 | 392 8 0 0 0 |
| rampCornersLevel | 173 54 173 0 0 | 173 54 173 0 0 |
| rampCornersPlan | 247 148 5 0 0 | 247 148 5 0 0 |
| wallsToolOnKey | 163 223 14 0 0 | 166 220 14 0 0 |
| wallsKeyOnTool | 175 181 44 0 0 | 178 178 44 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: none
- mirror: 1268 differs, 1403 differs
- rotation: 1403 180.00 deg, 1268 180.00 deg
- ffl: 1272 226.0 mm, 1273 226.0 mm, 1170 223.0 mm, 1171 223.0 mm, 1304 198.0 mm, 1274 193.0 mm, 1298 185.0 mm, 1299 185.0 mm, 1338 177.0 mm, 1173 174.0 mm
- entry: 1272 233.8 mm, 1273 225.6 mm, 1171 223.1 mm, 1304 198.4 mm, 1274 192.7 mm, 1173 184.8 mm, 1174 174.0 mm, 1277 173.3 mm, 1300 172.6 mm, 1302 172.5 mm
- steps: 1156 1, 1250 1, 1311 1, 1312 1, 1322 1, 1338 1, 1371 1, 1436 1
- rampCornersLevel: 1272 313.0 mm, 1173 283.0 mm, 1273 272.0 mm, 1171 257.0 mm, 1299 214.0 mm, 1303 214.0 mm, 1170 208.0 mm, 1301 204.0 mm, 1276 203.0 mm, 1271 201.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1190 5410.4 mm, 1191 5410.4 mm, 1189 3700.0 mm, 1387 3700.0 mm, 1388 3700.0 mm, 1379 3698.9 mm, 1380 3698.9 mm, 1403 2000.2 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 3835.7 mm, 1246 3835.3 mm, 1287 2325.7 mm, 1460 2000.1 mm, 1403 2000.0 mm, 1453 1999.7 mm, 1465 1999.7 mm, 1461 1999.4 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 1268 differs, 1403 differs
- rotation: 1403 180.00 deg, 1268 180.00 deg
- ffl: 1272 226.0 mm, 1273 226.0 mm, 1170 223.0 mm, 1171 223.0 mm, 1304 198.0 mm, 1274 193.0 mm, 1298 185.0 mm, 1299 185.0 mm, 1338 177.0 mm, 1173 174.0 mm
- entry: 1272 233.8 mm, 1273 225.6 mm, 1171 223.1 mm, 1304 198.4 mm, 1274 192.7 mm, 1173 184.8 mm, 1174 174.0 mm, 1277 173.3 mm, 1300 172.6 mm, 1302 172.5 mm
- steps: 1156 1, 1250 1, 1311 1, 1312 1, 1322 1, 1338 1, 1371 1, 1436 1
- rampCornersLevel: 1272 313.0 mm, 1173 283.0 mm, 1273 272.0 mm, 1171 257.0 mm, 1299 214.0 mm, 1303 214.0 mm, 1170 208.0 mm, 1301 204.0 mm, 1276 203.0 mm, 1271 201.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1190 5410.4 mm, 1191 5410.4 mm, 1189 3700.0 mm, 1387 3700.0 mm, 1388 3700.0 mm, 1379 3698.9 mm, 1380 3698.9 mm, 1403 2000.2 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 3936.0 mm, 1246 3935.4 mm, 1287 2426.0 mm, 1460 2000.1 mm, 1403 2000.0 mm, 1453 1999.7 mm, 1465 1999.7 mm, 1461 1999.4 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imd.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1198 | same / same | same / same | 0.00 deg / 0.00 deg | 28.0 mm / 28.0 mm | 28.4 mm / 28.4 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 99.9 mm / 99.9 mm | 1.0 mm / 1.0 mm |
| 1270 | same / same | same / same | 0.00 deg / 0.00 deg | 69.0 mm / 69.0 mm | 98.8 mm / 98.8 mm | 0 / 0 | 129.0 mm / 129.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.7 mm / 99.7 mm |
| 1274 | same / same | same / same | 0.00 deg / 0.00 deg | 193.0 mm / 193.0 mm | 192.7 mm / 192.7 mm | 0 / 0 | 174.0 mm / 174.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 0.7 mm / 0.7 mm |
| 1284 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.7 mm / 0.7 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 99.1 mm / 99.1 mm |
| 1418 | same / same | same / same | 0.00 deg / 0.00 deg | 10.0 mm / 10.0 mm | 9.5 mm / 9.5 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm |
| 1464 | same / same | same / same | 0.00 deg / 0.00 deg | 7.0 mm / 7.0 mm | 8.0 mm / 8.0 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 1482 | same / same | same / same | 0.00 deg / 0.00 deg | 6.0 mm / 6.0 mm | 6.0 mm / 6.0 mm | 0 / 0 | 12.0 mm / 12.0 mm | 1.4 mm / 1.4 mm | 99.4 mm / 99.4 mm | 1929.7 mm / 1929.7 mm |
| 1512 | same / same | same / same | 0.00 deg / 0.00 deg | 4.0 mm / 4.0 mm | 4.4 mm / 4.4 mm | 0 / 0 | 6.0 mm / 6.0 mm | 1.0 mm / 1.0 mm | 99.1 mm / 99.1 mm | 99.1 mm / 99.1 mm |
| 1529 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 2.3 mm / 2.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.3 mm / 1.3 mm | 1.3 mm / 1.3 mm |
| 1539 | same / same | same / same | 0.00 deg / 0.00 deg | 3.0 mm / 3.0 mm | 2.5 mm / 2.5 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |

### IME (TU)

Rev6, no override list: plots compared 316, only in the tool plan 0, only in the key 4.
Without overrides: plots compared 316, only in the tool plan 0, only in the key 4.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 316 0 0 0 0 | 316 0 0 0 0 |
| mirror | 308 0 8 0 0 | 308 0 8 0 0 |
| rotation | 314 0 2 0 0 | 314 0 2 0 0 |
| ffl | 51 94 171 0 0 | 51 94 171 0 0 |
| entry | 31 159 126 0 0 | 31 159 126 0 0 |
| steps | 316 0 0 0 0 | 316 0 0 0 0 |
| rampCornersLevel | 161 45 110 0 0 | 161 45 110 0 0 |
| rampCornersPlan | 217 92 7 0 0 | 217 92 7 0 0 |
| wallsToolOnKey | 254 52 10 0 0 | 258 48 10 0 0 |
| wallsKeyOnTool | 213 87 16 0 0 | 218 82 16 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: none
- mirror: 2786 differs, 2787 differs, 2892 differs, 2893 differs, 2894 differs, 2895 differs, 2896 differs, 2897 differs
- rotation: 2787 180.00 deg, 2786 180.00 deg
- ffl: 2893 241.0 mm, 2892 227.0 mm, 2894 214.0 mm, 2895 214.0 mm, 2896 162.0 mm, 2897 109.0 mm, 2913 103.0 mm, 2714 91.0 mm, 2767 86.0 mm, 2772 84.0 mm
- entry: 2893 241.2 mm, 2892 227.2 mm, 2894 213.5 mm, 2895 196.6 mm, 2896 162.2 mm, 2897 109.4 mm, 2913 103.4 mm, 2714 91.3 mm, 2759 90.7 mm, 2767 85.7 mm
- steps: none
- rampCornersLevel: 2892 225.0 mm, 2893 224.0 mm, 2894 221.0 mm, 2896 184.0 mm, 2895 174.0 mm, 2897 156.0 mm, 2666 138.0 mm, 2854 130.0 mm, 2759 126.0 mm, 2949 93.0 mm
- rampCornersPlan: 2892 19500.4 mm, 2897 19500.4 mm, 2893 19480.7 mm, 2894 19480.7 mm, 2895 19480.7 mm, 2896 19480.7 mm, 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2704 3700.3 mm, 2705 3700.3 mm, 2680 3699.6 mm, 2681 3699.6 mm, 2892 2524.5 mm, 2893 2524.5 mm, 2896 2524.3 mm, 2897 2524.3 mm
- wallsKeyOnTool: 2646 3580.2 mm, 2645 3579.5 mm, 2892 2544.3 mm, 2893 2544.3 mm, 2896 2525.2 mm, 2897 2525.2 mm, 2879 2329.1 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2766 1930.3 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 2786 differs, 2787 differs, 2892 differs, 2893 differs, 2894 differs, 2895 differs, 2896 differs, 2897 differs
- rotation: 2787 180.00 deg, 2786 180.00 deg
- ffl: 2893 241.0 mm, 2892 227.0 mm, 2894 214.0 mm, 2895 214.0 mm, 2896 162.0 mm, 2897 109.0 mm, 2913 103.0 mm, 2714 91.0 mm, 2767 86.0 mm, 2772 84.0 mm
- entry: 2893 241.2 mm, 2892 227.2 mm, 2894 213.5 mm, 2895 196.6 mm, 2896 162.2 mm, 2897 109.4 mm, 2913 103.4 mm, 2714 91.3 mm, 2759 90.7 mm, 2767 85.7 mm
- steps: none
- rampCornersLevel: 2892 225.0 mm, 2893 224.0 mm, 2894 221.0 mm, 2896 184.0 mm, 2895 174.0 mm, 2897 156.0 mm, 2666 138.0 mm, 2854 130.0 mm, 2759 126.0 mm, 2949 93.0 mm
- rampCornersPlan: 2892 19500.4 mm, 2897 19500.4 mm, 2893 19480.7 mm, 2894 19480.7 mm, 2895 19480.7 mm, 2896 19480.7 mm, 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2704 3700.3 mm, 2705 3700.3 mm, 2680 3699.6 mm, 2681 3699.6 mm, 2892 2524.5 mm, 2893 2524.5 mm, 2896 2524.3 mm, 2897 2524.3 mm
- wallsKeyOnTool: 2646 3680.3 mm, 2645 3679.8 mm, 2892 2544.3 mm, 2893 2544.3 mm, 2896 2525.2 mm, 2897 2525.2 mm, 2879 2429.2 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2766 1930.3 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_ime.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2669 | same / same | same / same | 0.00 deg / 0.00 deg | 4.0 mm / 4.0 mm | 3.7 mm / 3.7 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 2686 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 2.3 mm / 2.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2760 | same / same | same / same | 0.00 deg / 0.00 deg | 45.0 mm / 45.0 mm | 15.4 mm / 15.4 mm | 0 / 0 | 32.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm | 1.0 mm / 1.0 mm |
| 2764 | same / same | same / same | 0.00 deg / 0.00 deg | 52.0 mm / 52.0 mm | 2.2 mm / 2.2 mm | 0 / 0 | 51.0 mm / 51.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm |
| 2772 | same / same | same / same | 0.00 deg / 0.00 deg | 84.0 mm / 84.0 mm | 12.6 mm / 12.6 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2783 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 2.8 mm / 2.8 mm | 0 / 0 | 71.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2789 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 2.7 mm / 2.7 mm | 0 / 0 | 71.0 mm / 71.0 mm | 0.0 mm / 0.0 mm | 0.9 mm / 0.9 mm | 0.9 mm / 0.9 mm |
| 2864 | same / same | same / same | 0.00 deg / 0.00 deg | 16.0 mm / 16.0 mm | 13.6 mm / 13.6 mm | 0 / 0 | 22.0 mm / 22.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2903 | same / same | same / same | 0.00 deg / 0.00 deg | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 2949 | same / same | same / same | 0.00 deg / 0.00 deg | 79.0 mm / 79.0 mm | 78.6 mm / 78.6 mm | 0 / 0 | 93.0 mm / 93.0 mm | 1.4 mm / 1.4 mm | 1.2 mm / 1.2 mm | 19.6 mm / 19.6 mm |

### IMF (TU)

Rev6, no override list: plots compared 462, only in the tool plan 0, only in the key 14.
Without overrides: plots compared 462, only in the tool plan 0, only in the key 14.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 462 0 0 0 0 | 462 0 0 0 0 |
| mirror | 460 0 2 0 0 | 460 0 2 0 0 |
| rotation | 460 0 2 0 0 | 460 0 2 0 0 |
| ffl | 32 148 282 0 0 | 32 148 282 0 0 |
| entry | 25 209 222 6 0 | 25 209 222 6 0 |
| steps | 425 27 10 0 0 | 425 27 10 0 0 |
| rampCornersLevel | 240 83 139 0 0 | 240 83 139 0 0 |
| rampCornersPlan | 303 156 3 0 0 | 303 156 3 0 0 |
| wallsToolOnKey | 386 71 5 0 0 | 392 65 5 0 0 |
| wallsKeyOnTool | 322 119 21 0 0 | 332 109 21 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: none
- mirror: 739 differs, 755 differs
- rotation: 755 180.00 deg, 739 180.00 deg
- ffl: 1008 400.0 mm, 1009 400.0 mm, 990 379.0 mm, 998 379.0 mm, 704 363.0 mm, 1094 360.0 mm, 1095 360.0 mm, 1061 322.0 mm, 1062 322.0 mm, 910 321.0 mm
- entry: 986 138.1 mm, 975 134.7 mm, 934 131.9 mm, 1046 116.8 mm, 958 114.6 mm, 1075 98.9 mm, 1052 95.7 mm, 1068 95.3 mm, 946 90.0 mm, 1069 88.5 mm
- steps: 1008 3, 704 2, 910 2, 918 2, 990 2, 998 2, 1009 2, 1058 2, 1062 2, 1094 2
- rampCornersLevel: 974 180.0 mm, 673 175.0 mm, 694 161.0 mm, 713 149.0 mm, 654 147.0 mm, 741 138.0 mm, 887 97.0 mm, 707 84.0 mm, 1010 83.0 mm, 826 82.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 865 1999.2 mm, 823 280.1 mm, 824 280.1 mm, 817 280.1 mm, 818 280.1 mm, 819 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 713 3579.8 mm, 654 3566.3 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 739 differs, 755 differs
- rotation: 755 180.00 deg, 739 180.00 deg
- ffl: 1008 400.0 mm, 1009 400.0 mm, 990 379.0 mm, 998 379.0 mm, 704 363.0 mm, 1094 360.0 mm, 1095 360.0 mm, 1061 322.0 mm, 1062 322.0 mm, 910 321.0 mm
- entry: 986 138.1 mm, 975 134.7 mm, 934 131.9 mm, 1046 116.8 mm, 958 114.6 mm, 1075 98.9 mm, 1052 95.7 mm, 1068 95.3 mm, 946 90.0 mm, 1069 88.5 mm
- steps: 1008 3, 704 2, 910 2, 918 2, 990 2, 998 2, 1009 2, 1058 2, 1062 2, 1094 2
- rampCornersLevel: 974 180.0 mm, 673 175.0 mm, 694 161.0 mm, 713 149.0 mm, 654 147.0 mm, 741 138.0 mm, 887 97.0 mm, 707 84.0 mm, 1010 83.0 mm, 826 82.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 865 1999.2 mm, 823 280.1 mm, 824 280.1 mm, 817 280.1 mm, 818 280.1 mm, 819 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 640 3690.9 mm, 713 3680.1 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_imf.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 696 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 1.4 mm / 1.4 mm | 0 / 0 | 41.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 773 | same / same | same / same | 0.00 deg / 0.00 deg | 49.0 mm / 49.0 mm | 48.7 mm / 48.7 mm | 0 / 0 | 41.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 20.2 mm / 20.2 mm |
| 777 | same / same | same / same | 0.00 deg / 0.00 deg | 54.0 mm / 54.0 mm | 53.5 mm / 53.5 mm | 0 / 0 | 46.0 mm / 46.0 mm | 1.4 mm / 1.4 mm | 1.3 mm / 1.3 mm | 0.8 mm / 0.8 mm |
| 785 | same / same | same / same | 0.00 deg / 0.00 deg | 10.0 mm / 10.0 mm | 19.3 mm / 19.3 mm | 0 / 0 | 21.0 mm / 21.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 934 | same / same | same / same | 0.00 deg / 0.00 deg | 132.0 mm / 132.0 mm | 131.9 mm / 131.9 mm | 0 / 0 | 4.0 mm / 4.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.3 mm / 1.3 mm |
| 980 | same / same | same / same | 0.00 deg / 0.00 deg | 27.0 mm / 27.0 mm | 27.1 mm / 27.1 mm | 0 / 0 | 5.0 mm / 5.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 0.5 mm / 0.5 mm |
| 998 | same / same | same / same | 0.00 deg / 0.00 deg | 379.0 mm / 379.0 mm | 26.9 mm / 26.9 mm | 2 / 2 | 72.0 mm / 72.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 1024 | same / same | same / same | 0.00 deg / 0.00 deg | 83.0 mm / 83.0 mm | 83.0 mm / 83.0 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.5 mm / 1.5 mm |
| 1041 | same / same | same / same | 0.00 deg / 0.00 deg | 32.0 mm / 32.0 mm | 4.7 mm / 4.7 mm | 0 / 0 | 2.0 mm / 2.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 1051 | same / same | same / same | 0.00 deg / 0.00 deg | 75.0 mm / 75.0 mm | 75.3 mm / 75.3 mm | 0 / 0 | 3.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm |

### IMG (TU)

Rev6, no override list: plots compared 257, only in the tool plan 0, only in the key 11.
Without overrides: plots compared 257, only in the tool plan 0, only in the key 11.

Counts are match, within limit, over limit, no reference, tool none.

| Field | Rev6, no override list | Without overrides |
|---|---|---|
| villaType | 257 0 0 0 0 | 257 0 0 0 0 |
| mirror | 236 0 21 0 0 | 236 0 21 0 0 |
| rotation | 245 2 10 0 0 | 245 2 10 0 0 |
| ffl | 75 60 122 0 0 | 75 60 122 0 0 |
| entry | 46 99 112 0 0 | 46 99 112 0 0 |
| steps | 249 8 0 0 0 | 249 8 0 0 0 |
| rampCornersLevel | 141 29 87 0 0 | 141 29 87 0 0 |
| rampCornersPlan | 149 82 26 0 0 | 149 82 26 0 0 |
| wallsToolOnKey | 186 59 12 0 0 | 188 57 12 0 0 |
| wallsKeyOnTool | 166 71 20 0 0 | 170 67 20 0 0 |

Worst 10 per field, rev6, no override list (plot, gap):

- villaType: none
- mirror: 457 differs, 2998 differs, 2999 differs, 3000 differs, 3001 differs, 3002 differs, 3003 differs, 3005 differs, 3006 differs, 3007 differs
- rotation: 457 180.00 deg, 2998 180.00 deg, 3000 180.00 deg, 3001 180.00 deg, 3031 180.00 deg, 3003 180.00 deg, 3030 180.00 deg, 3002 180.00 deg, 2999 180.00 deg, 2950 1.00 deg
- ffl: 3011 248.0 mm, 3012 248.0 mm, 3013 248.0 mm, 3014 248.0 mm, 3005 244.0 mm, 3006 244.0 mm, 3015 194.0 mm, 3016 194.0 mm, 485 188.0 mm, 3025 163.0 mm
- entry: 3006 234.4 mm, 3015 220.8 mm, 3014 196.9 mm, 3016 184.6 mm, 3007 183.0 mm, 3005 162.8 mm, 3013 147.3 mm, 3008 133.4 mm, 3012 104.8 mm, 3009 97.7 mm
- steps: 485 1, 580 1, 2953 1, 3007 1, 3008 1, 3011 1, 3025 1, 3026 1
- rampCornersLevel: 3014 317.0 mm, 3007 292.0 mm, 3008 279.0 mm, 3016 271.0 mm, 3006 267.0 mm, 3015 262.0 mm, 3005 259.0 mm, 3013 227.0 mm, 3012 208.0 mm, 585 163.0 mm
- rampCornersPlan: 3005 19481.2 mm, 3006 19481.2 mm, 3007 19481.2 mm, 3008 19481.2 mm, 3013 19481.2 mm, 3014 19481.2 mm, 3015 19481.2 mm, 3016 19481.2 mm, 3009 19481.2 mm, 3011 19480.7 mm
- wallsToolOnKey: 612 3700.5 mm, 613 3700.5 mm, 614 3700.3 mm, 615 3700.3 mm, 546 3700.0 mm, 547 3700.0 mm, 555 3698.9 mm, 556 3698.9 mm, 2950 2777.8 mm, 552 2000.2 mm
- wallsKeyOnTool: 576 17690.5 mm, 592 17689.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 568 3098.5 mm, 455 2330.8 mm, 454 2324.4 mm, 566 2099.2 mm, 2953 2000.2 mm, 552 2000.2 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: none
- mirror: 457 differs, 2998 differs, 2999 differs, 3000 differs, 3001 differs, 3002 differs, 3003 differs, 3005 differs, 3006 differs, 3007 differs
- rotation: 457 180.00 deg, 2998 180.00 deg, 3000 180.00 deg, 3001 180.00 deg, 3031 180.00 deg, 3003 180.00 deg, 3030 180.00 deg, 3002 180.00 deg, 2999 180.00 deg, 2950 1.00 deg
- ffl: 3011 248.0 mm, 3012 248.0 mm, 3013 248.0 mm, 3014 248.0 mm, 3005 244.0 mm, 3006 244.0 mm, 3015 194.0 mm, 3016 194.0 mm, 485 188.0 mm, 3025 163.0 mm
- entry: 3006 234.4 mm, 3015 220.8 mm, 3014 196.9 mm, 3016 184.6 mm, 3007 183.0 mm, 3005 162.8 mm, 3013 147.3 mm, 3008 133.4 mm, 3012 104.8 mm, 3009 97.7 mm
- steps: 485 1, 580 1, 2953 1, 3007 1, 3008 1, 3011 1, 3025 1, 3026 1
- rampCornersLevel: 3014 317.0 mm, 3007 292.0 mm, 3008 279.0 mm, 3016 271.0 mm, 3006 267.0 mm, 3015 262.0 mm, 3005 259.0 mm, 3013 227.0 mm, 3012 208.0 mm, 585 163.0 mm
- rampCornersPlan: 3005 19481.2 mm, 3006 19481.2 mm, 3007 19481.2 mm, 3008 19481.2 mm, 3013 19481.2 mm, 3014 19481.2 mm, 3015 19481.2 mm, 3016 19481.2 mm, 3009 19481.2 mm, 3011 19480.7 mm
- wallsToolOnKey: 612 3700.5 mm, 613 3700.5 mm, 614 3700.3 mm, 615 3700.3 mm, 546 3700.0 mm, 547 3700.0 mm, 555 3698.9 mm, 556 3698.9 mm, 2950 2777.8 mm, 552 2000.2 mm
- wallsKeyOnTool: 576 17690.5 mm, 592 17689.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 568 3098.5 mm, 455 2430.9 mm, 454 2424.7 mm, 566 2099.2 mm, 2953 2000.2 mm, 552 2000.2 mm

Random plots at seed 14, the gap rev6, no override list then without overrides. The values are in side_by_side_img.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 491 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 1.8 mm / 1.8 mm | 0 / 0 | 20.0 mm / 20.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm |
| 508 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.4 mm / 0.4 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 35.5 mm / 35.5 mm | 1.4 mm / 1.4 mm |
| 583 | same / same | same / same | 0.00 deg / 0.00 deg | 2.0 mm / 2.0 mm | 2.3 mm / 2.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm |
| 588 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.3 mm / 0.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.8 mm / 0.8 mm |
| 596 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.1 mm / 0.1 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 0.9 mm / 0.9 mm | 1.0 mm / 1.0 mm |
| 607 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 2.1 mm / 2.1 mm | 0 / 0 | 41.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 613 | same / same | same / same | 0.00 deg / 0.00 deg | 38.0 mm / 38.0 mm | 38.4 mm / 38.4 mm | 0 / 0 | 17.0 mm / 17.0 mm | 1.0 mm / 1.0 mm | 3700.5 mm / 3700.5 mm | 1.5 mm / 1.5 mm |
| 2981 | same / same | same / same | 0.00 deg / 0.00 deg | 24.0 mm / 24.0 mm | 23.9 mm / 23.9 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 99.9 mm / 99.9 mm | 20.7 mm / 20.7 mm |
| 3008 | same / same | differs / differs | 0.00 deg / 0.00 deg | 77.0 mm / 77.0 mm | 133.4 mm / 133.4 mm | 1 / 1 | 279.0 mm / 279.0 mm | 19481.2 mm / 19481.2 mm | 278.7 mm / 278.7 mm | 278.2 mm / 278.2 mm |
| 3016 | same / same | differs / differs | 0.00 deg / 0.00 deg | 194.0 mm / 194.0 mm | 184.6 mm / 184.6 mm | 0 / 0 | 271.0 mm / 271.0 mm | 19481.2 mm / 19481.2 mm | 99.0 mm / 99.0 mm | 99.5 mm / 99.5 mm |
