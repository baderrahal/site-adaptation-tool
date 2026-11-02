# Round 6B-DATA, the plan of each zone against its past model

Lane D, 2 Nov 2026, the first half of Phase 6b. No Revit, no Civil 3D, no new CAD or PDF read. This file holds counts, gaps and plot numbers only.
The values sit outside git in round_addin\logs\p6b_data\<zone>\compare. This does not close the 6b shares of steps/01_next.md, which need Revit runs.

## In short

- All 7 zones have past model reads, so none is left out. 2,466 plots are compared on both sides with the override list (RAN).
- With the override list the tool agrees on type, mirror, rotation, FFL and steps on almost every plot. That list was made from these same models, so this agreement is partly by construction.
- Without the override list, the real test of the rules, the placement rules mostly hold and the level rules do not. FFL is within 5 mm on 29% to 53% of the plots of the 5 TU zones.
- Walls do not change with the list, it holds no wall. On most plots the largest wall gap is about 100 mm from the key line, and a few plots hold walls the other side lacks.

## 1. Which zones have reads

| Zone | Kind | Read | Links | Plots in the key plan | With generic floor | With ramp | With yard | Walls | Copy against manifest | What it can test |
|---|---|---|---|---|---|---|---|---|---|---|
| IMA | SE | KA | 433 | 420 | 420 | 0 | 0 | 2323 | same | Type, mirror, rotation, walls. kindLeaveOut gives it no level, step or ramp reference |
| IMB | SE | KB | 302 | 290 | 290 | 0 | 0 | 1515 | same | Type, mirror, rotation, walls. kindLeaveOut gives it no level, step or ramp reference |
| IMC | TU | KC | 338 | 324 | 324 | 324 | 324 | 926 | same | Every field |
| IMD | TU | KD | 411 | 403 | 403 | 403 | 403 | 1138 | same | Every field |
| IME | TU | KE | 325 | 320 | 320 | 320 | 320 | 894 | same | Every field |
| IMF | TU | KF | 491 | 476 | 476 | 476 | 476 | 1317 | same | Every field |
| IMG | TU | KG | 298 | 268 | 268 | 268 | 0 | 859 | same | Every field |

Zones with no read: none. Counted from the K read and the key plan (RAN). IMA and IMB hold no ramp or yard plots, IMG holds no yard.

## 2. How it was compared

- Tool side: python/plan_from_book.py on main, designBasis.mode roshnPastJob (Q224), once with config/overrides/roshn-makkah.json and once with no override list. python/zone_compare.py refuses rev6.
- Key side: python/plan_from_past.py --answer-key on the K read of the zone (R325). The plots are all those the key plan holds.
- The tool read the data book, the saved baseplan and footprint reads and the road surfaces. 64 input files are the same by SHA-256 before and after (RAN).

| Field | Match | Within limit | Source of the limit |
|---|---|---|---|
| Villa type, mirror | equal | none | |
| Rotation | 0.01 degrees | 1 degree | R129, the limit CHOSEN |
| FFL, El @ Entry | 0.5 mm | 5 mm | majority.levelPassMm, levelHoldMm |
| Steps | equal | 1 step | CHOSEN |
| Ramp corners, level | 5 mm | 30 mm | R138 |
| Ramp corners, in plan | 1 mm | 10 mm | R138 |
| Walls, each end against the nearest wall line of the plot, both ways | 10 mm | 300 mm | the match CHOSEN, keyPlans.wallEdgeM |

No reference means the key does not hold the value or majority.kindLeaveOut leaves it out for that kind of key. It is not a gap.

## 3. The zone table

Each cell is plots at or within the limit / plots over the limit.

With the override list, the tool as it runs:

| Zone | Plots | Villa | Mirror | Rot. | FFL | Entry | Steps | Ramp level | Ramp plan | Walls tool on key | Walls key on tool |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IMA | 419 | 415 / 4 | 419 / 0 | 419 / 0 | no ref | no ref | no ref | no ref | no ref | 412 / 7 | 404 / 15 |
| IMB | 288 | 286 / 2 | 288 / 0 | 288 / 0 | no ref | no ref | no ref | no ref | no ref | 279 / 9 | 269 / 19 |
| IMC | 324 | 322 / 2 | 323 / 1 | 323 / 1 | 324 / 0 | 201 / 113 | 324 / 0 | 281 / 43 | 320 / 4 | 315 / 9 | 288 / 36 |
| IMD | 400 | 396 / 4 | 400 / 0 | 400 / 0 | 398 / 2 | 250 / 145 | 400 / 0 | 347 / 53 | 395 / 5 | 389 / 11 | 356 / 44 |
| IME | 316 | 314 / 2 | 316 / 0 | 316 / 0 | 316 / 0 | 316 / 0 | 316 / 0 | 186 / 130 | 315 / 1 | 312 / 4 | 304 / 12 |
| IMF | 462 | 460 / 2 | 462 / 0 | 462 / 0 | 462 / 0 | 456 / 0 | 462 / 0 | 279 / 183 | 459 / 3 | 453 / 9 | 441 / 21 |
| IMG | 257 | 255 / 2 | 257 / 0 | 257 / 0 | 257 / 0 | 257 / 0 | 257 / 0 | 157 / 100 | 243 / 14 | 252 / 5 | 237 / 20 |

Without the override list, the rules alone:

| Zone | Plots | Villa | Mirror | Rot. | FFL | Entry | Steps | Ramp level | Ramp plan | Walls tool on key | Walls key on tool |
|---|---|---|---|---|---|---|---|---|---|---|---|
| IMA | 417 | 413 / 4 | 409 / 8 | 409 / 8 | no ref | no ref | no ref | no ref | no ref | 412 / 5 | 404 / 13 |
| IMB | 284 | 282 / 2 | 281 / 3 | 281 / 3 | no ref | no ref | no ref | no ref | no ref | 277 / 7 | 267 / 17 |
| IMC | 324 | 322 / 2 | 312 / 12 | 312 / 12 | 94 / 230 | 152 / 162 | 324 / 0 | 184 / 140 | 320 / 4 | 315 / 9 | 288 / 36 |
| IMD | 400 | 396 / 4 | 398 / 2 | 398 / 2 | 162 / 238 | 198 / 197 | 400 / 0 | 227 / 173 | 395 / 5 | 389 / 11 | 356 / 44 |
| IME | 316 | 314 / 2 | 308 / 8 | 314 / 2 | 145 / 171 | 190 / 126 | 316 / 0 | 206 / 110 | 309 / 7 | 312 / 4 | 304 / 12 |
| IMF | 462 | 460 / 2 | 460 / 2 | 460 / 2 | 180 / 282 | 234 / 222 | 452 / 10 | 323 / 139 | 459 / 3 | 453 / 9 | 441 / 21 |
| IMG | 257 | 255 / 2 | 236 / 21 | 247 / 10 | 135 / 122 | 145 / 112 | 257 / 0 | 170 / 87 | 231 / 26 | 252 / 5 | 237 / 20 |

Plots of a key the tool does not write: IMA 1, IMB 2, IMC 0, IMD 3, IME 4, IMF 14, IMG 11. Each is listed in the plan as missing, with levels.ffl among what it lacks. The build with no list writes 2 fewer on IMA and 4 fewer on IMB.

## 4. The biggest gaps and their likely rule

| Gap | Size | Likely rule | Owner |
|---|---|---|---|
| FFL by the rules | 1043 of 1759 TU plots over 5 mm, most within 100 mm, a few up to 1 m | The level rules of R278 work FFL out from the road. Why the past levels differ is UNKNOWN, a designed level is likely. The override list carries it today | Lane A |
| El @ Entry by the rules | 819 of 1738 TU plots over 5 mm | The same level rules (R278) | Lane A |
| Ramp corner levels | With the list 509 of 1,759 TU plots over 30 mm, without it 649 | Q057, closed: no front fall is applied (389 overrides not applied). On IME, IMF and IMG the list makes it worse, its front goes to the key and the back corners run parallel to it (R210), likely, not proven | Lane A |
| Wall lines | 64 to 75 percent of the plots have their largest gap between 10 and 150 mm, the most at 100 to 110 mm, the same with or without the list | Likely the wall location line. The keys mix codes 0, 3, 4 and 5 and the tool writes one line per edge. Half of a 200 mm wall is 100 mm, the wall thickness is not read here, so this is UNKNOWN until measured (Q360) | Lane A |
| Walls one side only | 167 plots of the key have a wall over 300 mm from any tool wall, 54 the other way | keyRules.wallOpen, weak at 0.7409 (Q115, Q266), and walls of the unit C10 on the tool side | Lane A |
| Mirror and rotation by the rules | 38 of 2,466 plots turned exactly 180 degrees and mirrored, 18 more mirrored only (IME 6, IMG 12). On IMG also 3 plots at about 1 degree | The hand rules and the units with no drawn side (R122, R123). Small. The list corrects all but plot 2302 of IMC, which has no override | Lane B |
| Ramp in plan | 1 to 14 plots a zone with the list over 10 mm, up to about 4 m. Several of the same plots then show wall gaps | Likely a ramp of another depth or side than R127 gives. Small, not opened one by one | Lane A |
| Villa type | 2 to 4 plots in each zone | Not a real gap: the location plan says TH1-IM and the key TH1 (Q238) | none |

Two gaps look large and are not faults of the tool:

- El @ Entry on IMC and IMD, 258 plots over 5 mm with the list. The tool follows the schedule of the zone, 724 of 724 within 5 mm of it, and the ELl@Entry parameter of the key is off that schedule on the same 258 plots. Known as Q070.
- IMA and IMB hold no level reference, so their level columns say no ref. They test type, mirror, rotation and walls only.

## 5. Second check

By scripts outside git in round_addin\logs\p6b_data\scripts, plain json, csv and math, with no function of zone_compare.py or plan_compare.py (RAN).

- Every status worked out again from the plans: 49260 the same, 0 differ. The first pass found the report had rounded its 0.5 mm limit on writing. That is fixed with a test, the tables never used the rounded value.
- The 10 random plots of each zone against the raw K read, rotation and mirror from the link: 70 the same, 0 differ. The X axis of a link sits a steady 0.023 degrees off its rotation field on all 70, not looked into.
- The key plan against past_all.csv on all plots, rotation, mirror, FFL and steps: the same but for 2 FFL plots of IMD, 1382 at 153.0 mm and 1436 at 50.0 mm. El @ Entry differs where the table holds the schedule, the 258 plots above.
- Conflicts: 260 rows in round_addin\logs\p6b_data\conflicts.csv, 258 El @ Entry and 2 FFL.

## 6. Zone by zone

### IMA (SE)

With overrides: plots compared 419, only in the tool plan 0, only in the key 1.
Without overrides: plots compared 417, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 415 0 4 0 0 | 413 0 4 0 0 |
| mirror | 419 0 0 0 0 | 409 0 8 0 0 |
| rotation | 419 0 0 0 0 | 409 0 8 0 0 |
| ffl | 0 0 0 419 0 | 0 0 0 417 0 |
| entry | 0 0 0 419 0 | 0 0 0 417 0 |
| steps | 0 0 0 419 0 | 0 0 0 417 0 |
| rampCornersLevel | 0 0 0 419 0 | 0 0 0 417 0 |
| rampCornersPlan | 0 0 0 419 0 | 0 0 0 417 0 |
| wallsToolOnKey | 118 294 7 0 0 | 118 294 5 0 0 |
| wallsKeyOnTool | 84 320 15 0 0 | 84 320 13 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 1667 differs, 1668 differs, 1859 differs, 1860 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 1613 3900.0 mm, 1813 2211.9 mm, 1868 2211.6 mm, 1763 2003.5 mm, 1590 2002.8 mm, 1808 1971.6 mm, 1796 707.8 mm, 1931 230.3 mm, 1932 230.3 mm, 1929 229.5 mm
- wallsKeyOnTool: 1613 3899.4 mm, 1830 2895.5 mm, 1808 2895.2 mm, 1895 2895.0 mm, 1651 2894.8 mm, 1875 2894.8 mm, 1925 2894.8 mm, 1738 2894.6 mm, 1713 2894.6 mm, 1700 2894.3 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 1667 differs, 1668 differs, 1859 differs, 1860 differs
- mirror: 1613 differs, 1707 differs, 1708 differs, 1709 differs, 1743 differs, 1744 differs, 1745 differs, 1809 differs
- rotation: 1707 180.00 deg, 1708 180.00 deg, 1709 180.00 deg, 1743 180.00 deg, 1744 180.00 deg, 1745 180.00 deg, 1613 180.00 deg, 1809 180.00 deg
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 1613 3900.0 mm, 1813 2211.9 mm, 1868 2211.6 mm, 1808 1971.6 mm, 1796 707.8 mm, 1931 230.3 mm, 1932 230.3 mm, 1929 229.5 mm, 1930 229.5 mm, 1801 142.9 mm
- wallsKeyOnTool: 1613 3899.4 mm, 1830 2895.5 mm, 1808 2895.2 mm, 1895 2895.0 mm, 1651 2894.8 mm, 1875 2894.8 mm, 1925 2894.8 mm, 1738 2894.6 mm, 1713 2894.6 mm, 1700 2894.3 mm

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_ima.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1613 | same / same | same / differs | 0.00 deg / 180.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 3900.0 mm / 3900.0 mm | 3899.4 mm / 3899.4 mm |
| 1685 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1689 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1697 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1837 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.4 mm / 100.4 mm | 1.1 mm / 1.1 mm |
| 1883 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm |
| 1901 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.3 mm / 100.3 mm | 2594.8 mm / 2594.8 mm |
| 1927 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1944 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.4 mm / 1.4 mm | 30.4 mm / 30.4 mm |
| 1954 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.0 mm / 1.0 mm | 30.0 mm / 30.0 mm |

### IMB (SE)

With overrides: plots compared 288, only in the tool plan 0, only in the key 2.
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
| wallsToolOnKey | 64 215 9 0 0 | 64 213 7 0 0 |
| wallsKeyOnTool | 59 210 19 0 0 | 59 208 17 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 2176 differs, 2177 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 2240 2211.8 mm, 2008 2002.8 mm, 2271 2002.1 mm, 2005 2001.8 mm, 2041 1583.1 mm, 2222 1136.3 mm, 2223 1136.3 mm, 2262 349.5 mm, 2263 349.5 mm, 2254 144.3 mm
- wallsKeyOnTool: 2171 2896.0 mm, 2148 2895.8 mm, 2249 2895.6 mm, 2288 2895.3 mm, 2257 2895.0 mm, 2160 2894.8 mm, 2159 2894.7 mm, 2264 2894.6 mm, 2256 2894.6 mm, 2099 2894.5 mm

Worst 10 per field, without overrides (plot, gap):

- villaType: 2176 differs, 2177 differs
- mirror: 2100 differs, 2199 differs, 2200 differs
- rotation: 2100 180.00 deg, 2199 180.00 deg, 2200 180.00 deg
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: none
- rampCornersPlan: none
- wallsToolOnKey: 2240 2211.8 mm, 2005 2001.8 mm, 2041 1583.1 mm, 2222 1136.3 mm, 2223 1136.3 mm, 2262 349.5 mm, 2263 349.5 mm, 2254 144.3 mm, 2255 144.1 mm, 2256 144.1 mm
- wallsKeyOnTool: 2171 2896.0 mm, 2148 2895.8 mm, 2249 2895.6 mm, 2288 2895.3 mm, 2257 2895.0 mm, 2160 2894.8 mm, 2159 2894.7 mm, 2264 2894.6 mm, 2256 2894.6 mm, 2099 2894.5 mm

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_imb.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2031 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 1.0 mm / 1.0 mm |
| 2048 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 140.9 mm / 140.9 mm | 100.6 mm / 100.6 mm |
| 2120 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.3 mm / 100.3 mm | 100.9 mm / 100.9 mm |
| 2124 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 2132 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm |
| 2143 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 2149 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 100.0 mm / 100.0 mm | 100.3 mm / 100.3 mm |
| 2226 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 99.3 mm / 99.3 mm | 99.9 mm / 99.9 mm |
| 2234 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 2274 | same / same | same / same | 0.00 deg / 0.00 deg | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | no ref / no ref | 141.9 mm / 141.9 mm | 101.0 mm / 101.0 mm |

### IMC (TU)

With overrides: plots compared 324, only in the tool plan 0, only in the key 0.
Without overrides: plots compared 324, only in the tool plan 0, only in the key 0.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 322 0 2 0 0 | 322 0 2 0 0 |
| mirror | 323 0 1 0 0 | 312 0 12 0 0 |
| rotation | 323 0 1 0 0 | 312 0 12 0 0 |
| ffl | 324 0 0 0 0 | 22 72 230 0 0 |
| entry | 195 6 113 10 0 | 17 135 162 10 0 |
| steps | 324 0 0 0 0 | 307 17 0 0 0 |
| rampCornersLevel | 127 154 43 0 0 | 156 28 140 0 0 |
| rampCornersPlan | 223 97 4 0 0 | 223 97 4 0 0 |
| wallsToolOnKey | 71 244 9 0 0 | 71 244 9 0 0 |
| wallsKeyOnTool | 115 173 36 0 0 | 115 173 36 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 2513 differs, 2514 differs
- mirror: 2302 differs
- rotation: 2302 180.00 deg
- ffl: none
- entry: 2540 113.0 mm, 2325 111.0 mm, 2609 111.0 mm, 2348 109.0 mm, 2338 80.5 mm, 2320 80.4 mm, 2334 80.4 mm, 2461 80.4 mm, 2552 80.4 mm, 2386 80.3 mm
- steps: none
- rampCornersLevel: 2609 240.0 mm, 2601 138.0 mm, 2325 115.0 mm, 2539 102.0 mm, 2588 100.0 mm, 2610 99.0 mm, 2447 94.0 mm, 2320 88.0 mm, 2608 87.0 mm, 2443 84.0 mm
- rampCornersPlan: 2546 4000.3 mm, 2543 4000.0 mm, 2391 2000.2 mm, 2374 1999.7 mm, 2553 2.8 mm, 2459 2.8 mm, 2448 2.2 mm, 2487 2.2 mm, 2557 2.2 mm, 2583 2.2 mm
- wallsToolOnKey: 2543 4000.0 mm, 2546 3900.8 mm, 2548 3899.1 mm, 2555 2212.7 mm, 2459 2212.5 mm, 2569 2211.8 mm, 2447 2211.6 mm, 2374 2099.7 mm, 2391 2099.7 mm, 2409 144.4 mm
- wallsKeyOnTool: 2491 25000.6 mm, 2476 24999.5 mm, 2549 11899.4 mm, 2528 11899.4 mm, 2531 10100.3 mm, 2530 10099.5 mm, 2320 4545.8 mm, 2546 4100.6 mm, 2547 4099.8 mm, 2543 4099.2 mm

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

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_imc.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2328 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 149.0 mm | 70.4 mm / 79.2 mm | 0 / 0 | 17.0 mm / 164.0 mm | 1.0 mm / 1.0 mm | 99.0 mm / 99.0 mm | 1.0 mm / 1.0 mm |
| 2345 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 13.0 mm | 0.2 mm / 12.5 mm | 0 / 0 | 14.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 140.7 mm / 140.7 mm | 100.5 mm / 100.5 mm |
| 2425 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 42.0 mm | 40.3 mm / 1.8 mm | 0 / 0 | 4.0 mm / 42.0 mm | 0.0 mm / 0.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2429 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.1 mm / 0.8 mm | 0 / 0 | 2.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2437 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.4 mm / 5.0 mm | 0 / 0 | 8.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 2449 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.2 mm / 1.6 mm | 0 / 0 | 3.0 mm / 2.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 101.2 mm / 101.2 mm |
| 2455 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 32.0 mm | 30.4 mm / 1.7 mm | 0 / 0 | 4.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 2530 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 71.0 mm | 70.2 mm / 1.3 mm | 0 / 0 | 3.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 10099.5 mm / 10099.5 mm |
| 2571 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.3 mm / 2.6 mm | 0 / 0 | 4.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 101.0 mm / 101.0 mm |
| 2617 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 39.7 mm / 1.7 mm | 0 / 0 | 3.0 mm / 41.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 1.0 mm / 1.0 mm |

### IMD (TU)

With overrides: plots compared 400, only in the tool plan 0, only in the key 3.
Without overrides: plots compared 400, only in the tool plan 0, only in the key 3.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 396 0 4 0 0 | 396 0 4 0 0 |
| mirror | 400 0 0 0 0 | 398 0 2 0 0 |
| rotation | 400 0 0 0 0 | 398 0 2 0 0 |
| ffl | 398 0 2 0 0 | 40 122 238 0 0 |
| entry | 244 6 145 5 0 | 37 161 197 5 0 |
| steps | 400 0 0 0 0 | 392 8 0 0 0 |
| rampCornersLevel | 208 139 53 0 0 | 173 54 173 0 0 |
| rampCornersPlan | 247 148 5 0 0 | 247 148 5 0 0 |
| wallsToolOnKey | 132 257 11 0 0 | 132 257 11 0 0 |
| wallsKeyOnTool | 175 181 44 0 0 | 175 181 44 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 1297 differs, 1298 differs, 1392 differs, 1393 differs
- mirror: none
- rotation: none
- ffl: 1382 153.0 mm, 1436 50.0 mm
- entry: 1249 80.5 mm, 1480 80.5 mm, 1322 80.4 mm, 1204 80.4 mm, 1157 80.3 mm, 1159 80.3 mm, 1460 80.3 mm, 1473 80.1 mm, 1507 80.0 mm, 1531 80.0 mm
- steps: none
- rampCornersLevel: 1274 192.0 mm, 1534 148.0 mm, 1216 133.0 mm, 1536 120.0 mm, 1197 102.0 mm, 1217 101.0 mm, 1329 91.0 mm, 1191 88.0 mm, 1153 85.0 mm, 1288 82.0 mm
- rampCornersPlan: 1485 2001.1 mm, 1280 1999.7 mm, 1403 1999.7 mm, 1206 500.5 mm, 1227 500.5 mm, 1285 2.2 mm, 1320 2.2 mm, 1180 1.4 mm, 1184 1.4 mm, 1185 1.4 mm
- wallsToolOnKey: 1181 6480.3 mm, 1188 6480.2 mm, 1197 2213.0 mm, 1510 2213.0 mm, 1378 2212.5 mm, 1366 2211.3 mm, 1280 2002.3 mm, 1485 2001.1 mm, 1403 2000.2 mm, 1227 600.8 mm
- wallsKeyOnTool: 1399 8000.3 mm, 1398 8000.3 mm, 1273 4011.0 mm, 1246 4010.4 mm, 1287 2501.0 mm, 1437 2010.0 mm, 1452 2009.6 mm, 1469 2005.3 mm, 1482 2004.7 mm, 1485 2001.3 mm

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

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_imd.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 1198 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 28.0 mm | 0.4 mm / 28.4 mm | 0 / 0 | 26.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 141.7 mm / 141.7 mm | 101.1 mm / 101.1 mm |
| 1270 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 69.0 mm | 0.4 mm / 98.8 mm | 0 / 0 | 30.0 mm / 129.0 mm | 1.4 mm / 1.4 mm | 99.5 mm / 99.5 mm | 99.7 mm / 99.7 mm |
| 1274 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 193.0 mm | 0.4 mm / 192.7 mm | 0 / 0 | 192.0 mm / 174.0 mm | 1.0 mm / 1.0 mm | 140.9 mm / 140.9 mm | 100.7 mm / 100.7 mm |
| 1284 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.7 mm / 0.7 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 101.1 mm / 101.1 mm |
| 1418 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 10.0 mm | 0.0 mm / 9.5 mm | 0 / 0 | 10.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm |
| 1464 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 7.0 mm | 0.2 mm / 8.0 mm | 0 / 0 | 10.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 1482 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 6.0 mm | 0.1 mm / 6.0 mm | 0 / 0 | 8.0 mm / 12.0 mm | 1.4 mm / 1.4 mm | 140.7 mm / 140.7 mm | 2004.7 mm / 2004.7 mm |
| 1512 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 4.0 mm | 0.2 mm / 4.4 mm | 0 / 0 | 1.0 mm / 6.0 mm | 1.0 mm / 1.0 mm | 99.5 mm / 99.5 mm | 99.6 mm / 99.6 mm |
| 1529 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1539 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 3.0 mm | 0.1 mm / 2.5 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |

### IME (TU)

With overrides: plots compared 316, only in the tool plan 0, only in the key 4.
Without overrides: plots compared 316, only in the tool plan 0, only in the key 4.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 314 0 2 0 0 | 314 0 2 0 0 |
| mirror | 316 0 0 0 0 | 308 0 8 0 0 |
| rotation | 316 0 0 0 0 | 314 0 2 0 0 |
| ffl | 316 0 0 0 0 | 51 94 171 0 0 |
| entry | 316 0 0 0 0 | 31 159 126 0 0 |
| steps | 316 0 0 0 0 | 316 0 0 0 0 |
| rampCornersLevel | 102 84 130 0 0 | 161 45 110 0 0 |
| rampCornersPlan | 223 92 1 0 0 | 217 92 7 0 0 |
| wallsToolOnKey | 82 230 4 0 0 | 82 230 4 0 0 |
| wallsKeyOnTool | 111 193 12 0 0 | 111 193 12 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 2771 differs, 2772 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 2854 124.0 mm, 2759 121.0 mm, 2913 104.0 mm, 2643 91.0 mm, 2714 89.0 mm, 2736 87.0 mm, 2771 86.0 mm, 2775 86.0 mm, 2661 85.0 mm, 2663 84.0 mm
- rampCornersPlan: 2891 22.8 mm, 2743 2.2 mm, 2870 2.2 mm, 2673 1.4 mm, 2674 1.4 mm, 2675 1.4 mm, 2679 1.4 mm, 2682 1.4 mm, 2683 1.4 mm, 2684 1.4 mm
- wallsToolOnKey: 2758 8266.2 mm, 2757 8265.8 mm, 2698 2212.5 mm, 2829 2211.6 mm, 2894 159.5 mm, 2895 159.5 mm, 2891 159.0 mm, 2703 141.9 mm, 2897 141.9 mm, 2705 141.9 mm
- wallsKeyOnTool: 2646 3755.3 mm, 2645 3754.8 mm, 2879 2504.2 mm, 2766 2005.3 mm, 2741 2004.7 mm, 2752 2004.7 mm, 2750 2000.6 mm, 2743 1999.4 mm, 2719 1505.6 mm, 2730 1505.4 mm

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

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_ime.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 2669 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 4.0 mm | 0.0 mm / 3.7 mm | 0 / 0 | 6.0 mm / 3.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.9 mm / 100.9 mm |
| 2686 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 100.0 mm / 100.0 mm | 100.1 mm / 100.1 mm |
| 2760 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 45.0 mm | 0.0 mm / 15.4 mm | 0 / 0 | 17.0 mm / 32.0 mm | 1.0 mm / 1.0 mm | 100.0 mm / 100.0 mm | 100.0 mm / 100.0 mm |
| 2764 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 52.0 mm | 0.0 mm / 2.2 mm | 0 / 0 | 49.0 mm / 51.0 mm | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm |
| 2772 | differs / differs | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 84.0 mm | 0.0 mm / 12.6 mm | 0 / 0 | 14.0 mm / 2.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2783 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 2.8 mm | 0 / 0 | 74.0 mm / 71.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.5 mm / 0.5 mm |
| 2789 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 2.7 mm | 0 / 0 | 74.0 mm / 71.0 mm | 0.0 mm / 0.0 mm | 0.9 mm / 0.9 mm | 0.9 mm / 0.9 mm |
| 2864 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 16.0 mm | 0.0 mm / 13.6 mm | 0 / 0 | 35.0 mm / 22.0 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm | 1.4 mm / 1.4 mm |
| 2903 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 1.0 mm | 0.0 mm / 0.8 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.6 mm / 100.6 mm |
| 2949 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 79.0 mm | 0.0 mm / 78.6 mm | 0 / 0 | 77.0 mm / 93.0 mm | 1.4 mm / 1.4 mm | 100.3 mm / 100.3 mm | 100.7 mm / 100.7 mm |

### IMF (TU)

With overrides: plots compared 462, only in the tool plan 0, only in the key 14.
Without overrides: plots compared 462, only in the tool plan 0, only in the key 14.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 460 0 2 0 0 | 460 0 2 0 0 |
| mirror | 462 0 0 0 0 | 460 0 2 0 0 |
| rotation | 462 0 0 0 0 | 460 0 2 0 0 |
| ffl | 462 0 0 0 0 | 32 148 282 0 0 |
| entry | 456 0 0 6 0 | 25 209 222 6 0 |
| steps | 462 0 0 0 0 | 425 27 10 0 0 |
| rampCornersLevel | 114 165 183 0 0 | 240 83 139 0 0 |
| rampCornersPlan | 303 156 3 0 0 | 303 156 3 0 0 |
| wallsToolOnKey | 130 323 9 0 0 | 130 323 9 0 0 |
| wallsKeyOnTool | 196 245 21 0 0 | 196 245 21 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 1062 differs, 1063 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 673 140.0 mm, 975 135.0 mm, 974 134.0 mm, 986 134.0 mm, 934 132.0 mm, 741 121.0 mm, 958 115.0 mm, 1046 114.0 mm, 1008 109.0 mm, 907 104.0 mm
- rampCornersPlan: 865 1999.7 mm, 654 18.4 mm, 664 14.0 mm, 738 2.2 mm, 869 2.2 mm, 1061 2.2 mm, 1071 2.2 mm, 882 2.2 mm, 641 1.4 mm, 642 1.4 mm
- wallsToolOnKey: 916 4000.8 mm, 996 4000.0 mm, 913 3999.5 mm, 993 3999.1 mm, 1052 2212.2 mm, 836 2211.9 mm, 849 2211.8 mm, 1069 2211.6 mm, 865 1999.2 mm, 817 280.1 mm
- wallsKeyOnTool: 994 5900.2 mm, 915 5899.7 mm, 914 5899.7 mm, 995 5899.7 mm, 917 4100.3 mm, 997 4100.3 mm, 912 4100.3 mm, 992 4099.8 mm, 713 3755.1 mm, 640 3754.0 mm

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

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_imf.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 696 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 1.4 mm | 0 / 0 | 42.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 100.3 mm / 100.3 mm | 0.9 mm / 0.9 mm |
| 773 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 49.0 mm | 0.0 mm / 48.7 mm | 0 / 0 | 90.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 777 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 54.0 mm | 0.0 mm / 53.5 mm | 0 / 0 | 99.0 mm / 46.0 mm | 1.4 mm / 1.4 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 785 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 10.0 mm | 0.0 mm / 19.3 mm | 0 / 0 | 40.0 mm / 21.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 934 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 132.0 mm | 0.0 mm / 131.9 mm | 0 / 0 | 132.0 mm / 4.0 mm | 1.4 mm / 1.4 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 980 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 27.0 mm | 0.0 mm / 27.1 mm | 0 / 0 | 31.0 mm / 5.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.5 mm / 100.5 mm |
| 998 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 379.0 mm | 0.0 mm / 26.9 mm | 0 / 2 | 99.0 mm / 72.0 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 0.9 mm / 0.9 mm |
| 1024 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 83.0 mm | 0.0 mm / 83.0 mm | 0 / 0 | 86.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 1041 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 32.0 mm | 0.0 mm / 4.7 mm | 0 / 0 | 7.0 mm / 2.0 mm | 0.0 mm / 0.0 mm | 0.5 mm / 0.5 mm | 1.0 mm / 1.0 mm |
| 1051 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 75.0 mm | 0.0 mm / 75.3 mm | 0 / 0 | 78.0 mm / 3.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 101.2 mm / 101.2 mm |

### IMG (TU)

With overrides: plots compared 257, only in the tool plan 0, only in the key 11.
Without overrides: plots compared 257, only in the tool plan 0, only in the key 11.

Counts are match, within limit, over limit, no reference, tool none.

| Field | With overrides | Without overrides |
|---|---|---|
| villaType | 255 0 2 0 0 | 255 0 2 0 0 |
| mirror | 257 0 0 0 0 | 236 0 21 0 0 |
| rotation | 257 0 0 0 0 | 245 2 10 0 0 |
| ffl | 257 0 0 0 0 | 75 60 122 0 0 |
| entry | 257 0 0 0 0 | 46 99 112 0 0 |
| steps | 257 0 0 0 0 | 249 8 0 0 0 |
| rampCornersLevel | 80 77 100 0 0 | 141 29 87 0 0 |
| rampCornersPlan | 160 83 14 0 0 | 149 82 26 0 0 |
| wallsToolOnKey | 56 196 5 0 0 | 56 196 5 0 0 |
| wallsKeyOnTool | 73 164 20 0 0 | 73 164 20 0 0 |

Worst 10 per field, with overrides (plot, gap):

- villaType: 566 differs, 567 differs
- mirror: none
- rotation: none
- ffl: none
- entry: none
- steps: none
- rampCornersLevel: 585 166.0 mm, 623 117.0 mm, 629 100.0 mm, 3008 92.0 mm, 2953 90.0 mm, 567 87.0 mm, 571 87.0 mm, 579 87.0 mm, 515 87.0 mm, 519 87.0 mm
- rampCornersPlan: 2953 2000.2 mm, 552 2000.2 mm, 3003 1999.7 mm, 3004 542.2 mm, 2950 245.8 mm, 2951 221.0 mm, 585 95.4 mm, 441 56.1 mm, 442 56.1 mm, 446 56.1 mm
- wallsToolOnKey: 546 2212.2 mm, 592 2211.8 mm, 2953 2002.5 mm, 552 2002.3 mm, 3004 531.4 mm, 3009 279.6 mm, 3010 279.6 mm, 3011 279.6 mm, 3012 279.6 mm, 3007 278.7 mm
- wallsKeyOnTool: 576 17790.5 mm, 592 17789.3 mm, 3024 6100.0 mm, 3025 6000.5 mm, 568 3173.5 mm, 455 2505.9 mm, 454 2499.7 mm, 566 2099.2 mm, 3003 2099.2 mm, 2953 2000.2 mm

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

Random plots at seed 14, the gap with overrides then without. The values are in side_by_side_img.csv outside git.

| Plot | villaType | mirror | rotation | ffl | entry | steps | rampCornersLevel | rampCornersPlan | wallsToolOnKey | wallsKeyOnTool |
|---|---|---|---|---|---|---|---|---|---|---|
| 491 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 1.8 mm | 0 / 0 | 22.0 mm / 20.0 mm | 1.0 mm / 1.0 mm | 99.0 mm / 99.0 mm | 99.5 mm / 99.5 mm |
| 508 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.4 mm / 0.4 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 35.5 mm / 35.5 mm | 1.4 mm / 1.4 mm |
| 583 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 2.0 mm | 0.0 mm / 2.3 mm | 0 / 0 | 3.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 588 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.3 mm / 0.3 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm | 100.8 mm / 100.8 mm | 100.8 mm / 100.8 mm |
| 596 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.1 mm / 0.1 mm | 0 / 0 | 1.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 100.3 mm / 100.3 mm | 100.9 mm / 100.9 mm |
| 607 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 0.0 mm | 0.0 mm / 2.1 mm | 0 / 0 | 43.0 mm / 41.0 mm | 1.4 mm / 1.4 mm | 1.0 mm / 1.0 mm | 1.0 mm / 1.0 mm |
| 613 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 38.0 mm | 0.0 mm / 38.4 mm | 0 / 0 | 37.0 mm / 17.0 mm | 1.0 mm / 1.0 mm | 100.3 mm / 100.3 mm | 100.3 mm / 100.3 mm |
| 2981 | same / same | same / same | 0.00 deg / 0.00 deg | 0.0 mm / 24.0 mm | 0.0 mm / 23.9 mm | 0 / 0 | 25.0 mm / 1.0 mm | 1.4 mm / 1.4 mm | 141.7 mm / 141.7 mm | 100.6 mm / 100.6 mm |
| 3008 | same / same | same / differs | 0.00 deg / 0.00 deg | 0.0 mm / 77.0 mm | 0.0 mm / 133.4 mm | 0 / 1 | 92.0 mm / 279.0 mm | 1.0 mm / 19481.2 mm | 278.7 mm / 278.7 mm | 278.2 mm / 278.2 mm |
| 3016 | same / same | same / differs | 0.00 deg / 0.00 deg | 0.0 mm / 194.0 mm | 0.0 mm / 184.6 mm | 0 / 0 | 17.0 mm / 271.0 mm | 1.0 mm / 19481.2 mm | 141.4 mm / 141.4 mm | 99.5 mm / 99.0 mm |
