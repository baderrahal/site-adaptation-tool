# Round 14 Part A, 2026-10-24, links from CAD for IMC

Full record of Part A. The top entry of steps/log.md holds the short form.

## Start

- The session started in C:\Users\bader\Documents\GitHub\site-adaptation-tool, so both hooks loaded. git fetch showed main equal to origin/main at 82a8f1e. R111 to R119 were in docs/rules.md, guard-main.sh and guard-registry.sh in .claude/hooks. .claude/rules does not exist, the prompt named it. Branch feat/cad-links-14a. The plan was written before the first edit and before any delete.

## Part 0

- C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a holds no file and no .git, 0 bytes, last changed 15:04. git status and git log cannot run there (not a git repository), so nothing in it can be unpushed. rmdir and Remove-Item both failed, the folder is in use by another process, most likely a Claude or VS Code session that started in it. I did not stop any process. Two attempts, then stopped. The folder is still there and empty.
- The 13 _backup folders under C:\02-Site Adaptation\00_Revit Models\Zone 02\ARC\Prototypes_2024 were already gone when this session looked, the folder changed at 14:55 and holds only the 13 .rvt files. Nothing was deleted by this session, so no sizes can be listed. Nothing else in C:\02-Site Adaptation was touched.
- CLAUDE.md has the merge rule: merge only after gh pr checks <number> --watch shows the python and the dotnet check both passed.

## Inputs

- Answer key prototypes_imc_past.json (338 links, 337 with a plot number, 1010 floors), the baseplan plots.json of round 12 (2962 plot faces), the road layout pieces.json of round 12 (2533 RD-CURB-LINE pieces), analysis.json of cad_analysis.py with the schedule units (225 clusters, the same as Round 13). The shift of IMC is 10.

## A1 Plot cut list (R120)

- 336 plots with a Generic floor (2441 has none). 12 under 0.9 on their best face:
  - 2311 0.5 on face 2311 (label 2301 VL4) and 0.5 on face 336 (label 326 OS). 2312 0.5 on 2312 (2302 VL4) and 0.5 on 2311 (2301 VL4).
  - 2375 0.8 and 0.2 on 2374 (VL3), 2376 0.8 and 0.2 on 2375, 2377 0.8 and 0.2 on 2376, 2378 0.833 and 0.167 on 2377.
  - 2387 0.833 and 0.167 on 2388, 2388 0.8 and 0.2 on 2389, 2389 0.8 and 0.2 on 2390, 2390 0.8 and 0.2 on 2391.
  - 2544 0.75 and 0.25 on 2543, 2545 0.75 and 0.25 on 2546.
- This is the list the prompt gave. It is listed apart in every count below.

## A2 Corner test (R121)

- Face corner: 0 of 337 origins within 10 mm of a corner of their face, every typology and both mirror flags. No corner rule.
- Cluster outline corner: DT1 and DT3 107 of 108 within 10 mm (front right when mirrored, front left when not). 2611 (DT1, 13 m face) is 3.0 m off. DP1 to DP4, TH1, TH3 and VD4 0 within 10 mm. Villas are in no cluster.
- Origin from a front corner in link axes, fitted outside the A1 list: C10 start (6.0, 1.593), DP1 end (-10.0, 0.15), DP2 end (-9.995, 0.155), DP3 end (-9.99, 0.15), DP4 end (-9.99, 0.15), DT1 DT3 TH1 TH3 start (-10.0, 0), VD1 start (0, 0.149), VD2 end (-10.0, 0.155), VD4 end (-9.995, 0.15), VL4 end (-10.0, 0.15), VL6 start (0, 0.15).
- Judged against the fit, outside the A1 list: 325 plots, within 1 mm 267, within 10 mm 322, worse 3: 2338 DP4 11 mm, 2543 VD4 3.999 m, 2546 VD4 4.006 m. 2543 and 2546 are the 16 m faces next to the A1 plots 2544 and 2545. On the A1 list: 12 plots, 2 within 1 mm, 4 within 10 mm, 8 worse.
- Not every plot holds, so it is written as a measured relation in plotFrame and the CAD plan marks its origin computed.

## A3 Rotation from the front edge (R122)

- Front edge of R94 (any edge of 1 m or more): 329 of 337 hold within 0.1 degrees, 8 break (2321, 2324, 2347, 2409, 2583, 2594, 2602, 2610), all corner plots where a 25 m side or a 3.54 m chamfer is nearer the road.
- Front edge from 7.99 to below 18.75 m: 337 of 337 hold (325 outside A1, 12 on it). 5 fronts are tied within 1 m and still right.
- The link Y axis is the inward normal of the front, the X axis points to the right seen from the road, to the left when mirrored.

## A4 Mirror and CASE (R123)

- Lead of Rev 6 (left as drawn, right mirrored), clusters from the schedule units, outside A1:
  - M01 pair holds 4, M02 pair holds 10 (2 apart), M04 pair holds 32, M07 end holds 16, M08 end holds 6, M11 end holds 10.
  - M05 pair breaks 40 (4 apart), M06 pair breaks 68, M07 middle breaks 16, M08 middle breaks 6, M11 middle breaks 10.
- Drawn side per typology: DP1 left (13 and 13), DP2 left (5 and 5), DP3 left (5 and 5), DP4 left (15 and 15), DT1 right (20 and 20), DT3 right (34 and 34), TH1 right (8 and 8), TH3 right (8 and 8), VD4 left (1 and 1, plots 2301 and 2302 that are VD4 on the link and DP4 in the schedule).
- Rule mirrored when on the other side than drawn: holds 218 of 218 cluster plots outside A1, breaks 0. On A1 6 hold, 6 have no cluster. 107 plots outside A1 are in no cluster and get no rule.
- Villas by side neighbours: one neighbour on the left gives mirrored and on the right not mirrored on all C10 (7, 8), VD2 (7, 7), VD4 (7, 6), VL4 (4, 4). VL6 breaks (left 2 and 2, right 4 and 1). With neighbours on both sides the flags are mixed (VD1 4 and 4, VD2 7 and 8, VD4 7 and 7, VL4 3 and 4, VL6 3 and 7). No rule.
- CASE: A (30.1 mirrored 27, 210.1 not 23), B (30.1 mirrored 16, 210.1 not 21), C (120.1 not 17, 300.1 mirrored 14), D (120.1 not 17, 300.1 mirrored 17), E (30.1 not 26, 210.1 mirrored 30), F (30.1 not 35, 210.1 mirrored 28), G (120.1 mirrored 17, 300.1 not 18), H (120.1 mirrored 18, 300.1 not 13). No letter maps to one pair. Facing of the link Y axis: A B 300.1, C D 210.1, E F 120.1, G H 30.1, one facing per letter on 337 of 337.
- analysis.pairRule stays false.

## Second method (R124)

- cad_links_check.py, json and math only. Seed 14, 20 drawn, 20 agree. Seed 7, 20 drawn, 20 agree. All 337, 337 agree, 1 share not checked (2320, face and floor both not convex). Two fixes of the second method on the way, a vertex on a straight edge 0.3 mm off (2568) and the clip order for a face that is not convex (2320), the third case is listed and not fixed.

## Gate

- Python 680 to 694 passed, 1 skipped. .NET 347 passed.
