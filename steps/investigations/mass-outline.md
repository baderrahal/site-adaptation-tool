# Mass outline, facts on the 13 prototypes (MASS-2NOV-D, task 1)

Measured 2 Nov 2026 in Revit 2024.3 on copies of the 13 files of prototypes_2024, opened detached, closed with no save. Source files SHA-256 before and after: identical on 13 of 13 (RAN, cmp of hash_before.txt and hash_after.txt). Raw results and the probe source are outside git in round_addin\logs\p_mass\probe. Levels are relative to L0, the lowest level that holds a floor, named GROUND FLOOR SSL in all 13. Distances in mm, positive outside the slab outer loop, negative inside. A wall is on the slab when its exterior face is within 5 mm of the loop (CHOSEN).

## Method

- Probe add-in MassProbe, loaded by Run-TestHost.ps1 -DryStart -ProbeManifest under window_lock.py. It dumps per wall the type, function, kind, WallLocationLine, width, compound layers with core indices, Flipped, Orientation, curve, base and top, the end joins from LocationCurve.get_ElementsAtJoin and the exterior and interior side faces from HostObjectUtils.GetSideFaces. Per level it dumps the largest floor top face and sketch. Distances, angles and chains are computed afterwards by analyse.py from that dump.
- EXT: WallType.Function Exterior, not a stacked wall member, standing on or crossing the plane of the top of the L0 slab (base at or below it within 1 mm, top above it). BAND is the part of EXT whose exterior face lies from 500 mm inside to 1000 mm outside the slab.
- Only the functions Exterior and Interior occur in the 13 files (RAN). No Foundation or Retaining wall type exists, so the rule of any other function within 1 m of the slab edge found 0 such walls. Interior walls within 1 m of the slab edge are in the raw file.

## Per prototype

| Code | Slab m2 | Slab corners / arcs / holes | Walls Ext / Int | EXT | BAND | EXT deeper than 500 inside | EXT more than 1 m outside | BAND widths | BAND short under 100 / 300 / 600 | BAND outside slab, max | BAND ends joined / not joined | EXT closed by joins | BAND closed by joins |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| C10 | 153.9 | 10 / 0 / 0 | 299 / 473 | 104 | 15 | 3 | 84 | 120, 150, 180, 190, 210 | 0 / 0 / 0 | 825 | 23 / 7 | no, 36 pieces | no, 3 pieces |
| DP1 | 96.8 | 6 / 0 / 0 | 145 / 780 | 42 | 21 | 5 | 16 | 120, 140, 150, 180, 210 | 0 / 2 / 3 | 400 | 24 / 18 | no, 32 pieces | no, 11 pieces |
| DP2 | 111.8 | 4 / 0 / 1 | 181 / 784 | 32 | 20 | 5 | 7 | 120, 150, 180, 210 | 0 / 4 / 4 | 400 | 21 / 19 | no, 18 pieces | no, 10 pieces |
| DP3 | 112.5 | 4 / 0 / 1 | 111 / 806 | 42 | 15 | 10 | 17 | 150, 180, 210 | 0 / 4 / 4 | 400 | 15 / 15 | no, 18 pieces | no, 9 pieces |
| DP4 | 121.0 | 4 / 0 / 0 | 110 / 421 | 32 | 15 | 14 | 3 | 120, 150, 180, 210 | 0 / 2 / 2 | 400 | 17 / 13 | no, 18 pieces | no, 9 pieces |
| DT1 | 78.8 | 8 / 0 / 0 | 170 / 584 | 55 | 25 | 2 | 28 | 120, 130, 150, 180, 210 | 8 / 8 / 8 | 300 | 19 / 31 | no, 43 pieces | no, 13 pieces |
| DT3 | 78.6 | 8 / 0 / 0 | 136 / 706 | 50 | 26 | 0 | 24 | 120, 130, 150, 210 | 8 / 8 / 8 | 546 | 14 / 38 | no, 43 pieces | no, 19 pieces |
| TH1 | 78.8 | 8 / 0 / 0 | 120 / 584 | 35 | 28 | 2 | 5 | 120, 130, 150, 180, 210 | 10 / 10 / 10 | 80 | 14 / 42 | no, 26 pieces | no, 19 pieces |
| TH3 | 78.6 | 8 / 0 / 0 | 136 / 706 | 47 | 27 | 0 | 20 | 120, 130, 150, 210 | 10 / 10 / 10 | 568 | 7 / 47 | no, 44 pieces | no, 24 pieces |
| VD2 | 111.8 | 4 / 0 / 1 | 84 / 187 | 20 | 12 | 5 | 3 | 120, 150, 180, 210 | 0 / 0 / 0 | 122 | 15 / 9 | no, 9 pieces | no, 5 pieces |
| VD4 | 121.0 | 4 / 0 / 0 | 100 / 242 | 22 | 10 | 9 | 3 | 120, 150, 180, 210 | 0 / 0 / 0 | 102 | 14 / 6 | no, 11 pieces | no, 5 pieces |
| VL4 | 140.6 | 6 / 0 / 0 | 281 / 255 | 45 | 14 | 7 | 24 | 120, 150, 180, 210 | 0 / 0 / 0 | 0 | 22 / 6 | no, 32 pieces | no, 3 pieces |
| VL6 | 144.6 | 6 / 0 / 0 | 235 / 435 | 92 | 14 | 6 | 72 | 120, 150, 180, 210 | 0 / 0 / 0 | 30 | 22 / 6 | no, 28 pieces | no, 2 pieces |

Location lines in BAND, and the check of the centre lines. MEET is an end that reaches the crossing line of another BAND wall within 2 mm, GAP is short of it, OVER runs past it, NONE has no crossing line within 1 m.

| Code | Location lines in BAND | Centre line ends | MEET | GAP | OVER | NONE | Join angles in EXT 90 / 180 / other | Collinear touching pairs in EXT | Floors on L0 |
|---|---|---|---|---|---|---|---|---|---|
| C10 | CoreInterior 9, CoreExterior 5, FinishFaceInterior 1 | 30 | 13 | 1 | 12 | 4 | 141 / 6 / 0 | 186 | 31 |
| DP1 | CoreInterior 13, CoreExterior 8 | 42 | 16 | 7 | 8 | 11 | 11 / 5 / 0 | 21 | 1 |
| DP2 | CoreExterior 4, CoreInterior 16 | 40 | 11 | 5 | 10 | 14 | 16 / 8 / 0 | 25 | 1 |
| DP3 | CoreExterior 7, CoreInterior 8 | 30 | 10 | 3 | 5 | 12 | 39 / 3 / 0 | 19 | 5 |
| DP4 | CoreInterior 13, CoreExterior 2 | 30 | 9 | 2 | 5 | 14 | 21 / 2 / 0 | 12 | 1 |
| DT1 | CoreExterior 5, CoreInterior 19, FinishFaceInterior 1 | 50 | 14 | 12 | 20 | 4 | 14 / 3 / 0 | 49 | 1 |
| DT3 | CoreExterior 4, CoreInterior 19, FinishFaceInterior 2, WallCenterline 1 | 52 | 10 | 20 | 18 | 4 | 10 / 2 / 0 | 35 | 1 |
| TH1 | CoreExterior 5, CoreInterior 21, FinishFaceInterior 2 | 56 | 10 | 19 | 23 | 4 | 10 / 2 / 0 | 20 | 1 |
| TH3 | CoreExterior 4, CoreInterior 21, FinishFaceInterior 2 | 54 | 6 | 23 | 21 | 4 | 6 / 0 / 0 | 50 | 1 |
| VD2 | CoreExterior 4, CoreInterior 8 | 24 | 5 | 4 | 5 | 10 | 10 / 8 / 0 | 6 | 1 |
| VD4 | CoreInterior 9, CoreExterior 1 | 20 | 7 | 1 | 1 | 11 | 17 / 2 / 0 | 4 | 1 |
| VL4 | WallCenterline 2, CoreInterior 12 | 28 | 17 | 2 | 5 | 4 | 20 / 3 / 0 | 82 | 1 |
| VL6 | CoreInterior 10, FinishFaceInterior 1, CoreExterior 3 | 28 | 18 | 1 | 5 | 4 | 131 / 4 / 0 | 75 | 23 |

Levels: SSL at 0 and FFL at +75 for the ground floor, first floor SSL at +3200 (RAN, summary.json). Arc walls in EXT: 0 of 13 files. Curtain or stacked walls in EXT: 0 of 13 files, curtain walls exist (4 to 12 per file) but none spans the L0 slab plane.

## Findings

1. Closed chain by joins: 0 of 13 (RAN). No EXT set and no BAND set forms one closed chain by get_ElementsAtJoin, each falls into several pieces (columns above). 257 of 484 BAND ends carry no join at all, the others join a wall of the set, a wall outside the set, or a ModelLine. The ring found on 1 Nov for C10, DP1, DP4, VD2, VD4, VL4 and VL6 did not come from joins.
2. What breaks the chain (RAN): (a) an end with no join, the wall meets its neighbour at the corner with no join. (b) a wall cut into pieces on one line, see 6. (c) a join to a ModelLine and not a wall. (d) a join to an Exterior wall that is outside BAND. The centre line check shows corners cut short and corners overrun: GAP 100 ends and OVER 138 ends over 13 files, MEET 146 of 484 ends.
3. The Location Line setting does not move the LocationCurve (RAN, two ways). The exterior side face from GetSideFaces lies W/2 from the curve along Orientation on all 600 EXT walls with a planar face, largest miss 0.10 mm, with the settings CoreInterior, CoreExterior, FinishFaceExterior, FinishFaceInterior and WallCenterline all present. An offset computed from the setting and the layers misses on all of them. Orientation points to the exterior, Flipped is not needed. Measured on these 13 files only.
4. Function does not separate house walls from yard and garden walls (RAN). Every EXT wall has function Exterior, also the types Generic Wall-150mm_ Garden, Generic Wall-100mm and EW_Arch_Wall_g1 panels. Distance does: BAND walls lie from 500 mm inside to 825 mm outside the slab, the next wall outside lies 1150 mm out (C10, DP1 to DP4, VD2, VD4, VL4, VL6), 1450 mm (DT1, DT3) or 4100 mm (TH1, TH3). Height agrees: BAND walls are 2875 mm or taller, the outside group 1800 mm or lower (1530 mm in TH1 and TH3), with one exception, C10 and VL6 hold 48 and 32 walls of type EW_Insulated_Arch_Wall_210 more than 1150 mm outside and up to 2690 mm tall, what they are is UNKNOWN. Type names are a note only: Garden is in no BAND wall of the 13 and in the outside group of 11 of 13.
5. Join kinds (RAN): join angles between EXT walls are 90 and 180 degrees only, no other angle, no arc wall. Wall widths in BAND differ inside one file (100 to 230 mm, columns above) so face lines of two widths meet at one corner and must be intersected as face lines, not location lines. Short walls in BAND are in the table, the 80 mm pieces of DT1, DT3, TH1 and TH3 and the 115 and 205 mm pieces of DP1 to DP4 are the short ones. The end gap of a location line to the line of the joined wall is 15 mm at most.
6. What would make intersecting the face lines wrong (RAN): (a) walls cut into pieces on one line, 4 to 186 touching collinear pairs per file in EXT, the 80 mm pieces lie in the face line of a 210 mm wall at the slab edge, so collinear neighbours need merging before a corner is found. (b) walls that run past the corner, OVER 1 to 23 ends per file. (c) steps in the outline, BAND walls stand outside the slab by 20, 30, 75, 80, 90, 92, 93, 300, 310, 400, 546 and 568 mm, so the outline is not one offset of the slab. The 300 mm steps of DT1 and DT3 and the 546 and 568 mm of DT3 and TH3 are the likely entrance recess (CHOSEN reading, no recess object is named in the data). (d) Interior function walls stand within 1 m of the slab edge in every file, they must stay out of the set. (e) EXT walls up to 3.7 m inside the slab, 0 to 14 per file, are internal and the band must cut them off.
7. DP2, DP3, DT1, DT3 and TH3 on 1 Nov: the facts show no join difference, no file closes by joins. What they show is the size of the step, BAND walls outside the slab by up to 400 mm (DP2, DP3), 300 mm (DT1), 546 mm (DT3) and 568 mm (TH3), and the 80 mm pieces and gap ends in DT1, DT3 and TH3. DP1 and DP4 also have 400 mm steps and passed on 1 Nov, so the step alone is not the cause. The cause of the fallback is UNKNOWN from these facts.

## Second check and run record

- Face distance is computed from the Revit side face. The location line with W/2 and Orientation gave the same line on all 600 planar EXT walls. Walls that failed to read: 0 of 9071.
- Window 1 on TH1 failed: a Revit warning box of the file open blocked the open and the freeze guard ended it after 10 minutes. The probe then deleted warnings and answered that box. Window 2 on TH1 ok, window 3 on the other 12 ok, 31 minutes. An open took 120 to 150 s.
- Lock: one slot of R200 only. R449 and the listener of Lane C exist on origin/speed/2nov-c and speed/2nov-c-listener, not on main, so neither was used.

## UNKNOWN

- What the EW_Insulated_Arch_Wall_210 walls more than 1150 mm outside in C10 and VL6 are.
- The 649 mm of DT3 of 1 Nov against the 546 mm here, not reconciled, the 1 Nov figure may include walls above the lowest level.
- Whether the 80 mm pieces are one wall in the design intent.
- The reason of the 1 Nov fallback of DP2, DP3, DT1, DT3 and TH3.

## The proof in Revit, 3 Nov 2026

All RAN. Run folders round_addin\logs\p_mass\proof, addin_run\MASS_20261103-045802, MASS_20261103-065119 and LNKP_20261103-051103. Masses of NBH 012, 336 plots, the 13 prototypes made again by the code of the round. Each row from the check block of its sidecar.

| Prototype | Base | Ring m2 | Slab m2 | Share | Corners | Far face walls |
|---|---|---|---|---|---|---|
| C10 | ring | 154.34 | 153.91 | 1.003 | 12 | 0 |
| DP1 | ring | 98.16 | 96.82 | 1.014 | 12 | 0 |
| DP2 | ring | 103.60 | 111.78 | 0.927 | 6 | 1 |
| DP3 | slab, flagged | | 112.50 | | | 0 |
| DP4 | ring | 112.20 | 121.03 | 0.927 | 6 | 1 |
| DT1 | ring | 80.58 | 78.84 | 1.022 | 10 | 2 |
| DT3 | ring | 82.57 | 78.57 | 1.051 | 8 | 0 |
| TH1 | ring | 80.39 | 78.84 | 1.020 | 8 | 0 |
| TH3 | ring | 80.38 | 78.57 | 1.023 | 8 | 0 |
| VD2 | ring | 105.02 | 111.78 | 0.939 | 6 | 0 |
| VD4 | ring | 114.19 | 121.03 | 0.944 | 6 | 0 |
| VL4 | ring | 140.60 | 140.60 | 1.000 | 6 | 0 |
| VL6 | ring | 145.07 | 144.62 | 1.003 | 6 | 0 |

DP3 falls back: the ring stops at the end of wall 6273202, no face starts within wallRingCornerGapM of it. In DP3 the primary walls on that side stand 400 mm out of the slab, why they do not close is UNKNOWN.

### The 1 mm target

Second method, proof\faces_check2.py, plain code: each ring edge of the sidecars against the side faces of the first probe, a read that shares no code with the builder. Of 94 ring edges 89 lie on the line of a used wall's face, largest off the line 0.000 mm. 5 are connecting edges with no wall face along them, a step between two wall lines, C10 1 of 540 mm, DP1 3 of up to 430 mm, DT1 1 of 10 mm. Stretches of an edge no face covers: at corners up to 330 mm, the thickness of the neighbour, in the middle of an edge up to 35 mm.

What this does not show: it takes the walls the builder used, so it cannot catch a wrong wall. For that, breaker 2 held every main and primary wall of any function at the ground against the rings, not using that list: no exterior face stretch of 200 mm or more stands within 1 m outside a ring (RAN). A wall end can still run past a corner: DP2 and DP4 walls reach 90 and 120 mm past the ring, DT1 pieces 110 mm, C10 a pier 90 by 540 mm is cut off.

### Mass against link

D3 method, proof\compare_links.txt: the links of the Links run against those of the Masses run on 336 of 336 plots: largest distance 0.000 mm, largest turn 0.0000 degrees, mirror differs on 0. This holds the placement. Whether a link's shape equals its mass in Revit was not read, no read job gives a mass outline, UNKNOWN. By the rule both show the main model and the primary option.

### Footprint, R477, NBH 012

| Run | Same type | Over limit | Other type | No outline |
|---|---|---|---|---|
| Main, Lane B hand run of 3 Nov, steps/fix-2nov-b-handrun.md | 59 | 0 | 269 | 8 |
| This round, the middle a mean of corners, as main | 59 | 0 | 258 | 19 |
| This round, the middle the centre of the area | 58 | 0 | 271 | 7 |

The 19 was false: the mean of the corners moved when the count of corners changed, 14 VL6 plots lost their block with the house unchanged. Fixed, R477. C10 2447 goes from same type to no outline under the area centre, and DP2 2614 from other type to no outline.

Both Masses runs: 5.9 and 7.1 minutes, least free 27 GiB or more. The first made 13 masses, ended exit 1 with 275 inputs, 262 unchanged, 13 new .slog files in prototypes_2024 _backup (RAN, by time, the guard names none): Q338 again. The second made none and ended exit 0 with 275 of 275.
