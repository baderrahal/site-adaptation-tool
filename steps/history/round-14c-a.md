# Round 14c Part A, 2026-10-24, villa mirror, CASE and FFL split tested on the data

Full record of Part A. The top entry of steps/log.md holds the short form.

## Start

- The session started in the repo root, main equal to origin/main at 588d7a5. Worktree C:\Users\bader\Documents\GitHub\site-adaptation-tool-14c on feat/cad-rules-14c. The Civil 3D probe worktree site-adaptation-tool-c3d (probe/civil3d) was not touched, nor dotnet/SiteAdaptation.C3DProbe, python/c3d_*.py or steps/investigations. Plan written before the first edit. Real inputs read in place from the main folder: sample_output/round12 baseplan and roads, the analysis.json of Round 14 in the scratchpad, the answer key and both schedules.
- The 16 cut plots: cadLinks.cutNeighbours in config/project.json adds 2374, 2391, 2543, 2546 to the 12 of R120. plotFrame was measured again with all 16 apart: the fits did not change, measuredOn 325 to 321, apart 12 to 16. Outside the 16 the origin is within 10 mm on 320 of 321, 2338 at 11 mm.

## A1 Villa mirror (R131)

- 113 schedule villas on IMC with a link: VD2 29, VD4 27, VL6 19, C10 15, VL4 15, VD1 8. 2301 and 2302 are DP4 in the schedule, so they are cluster plots here.
- Villas drawn the other hand, measured on the villas with one neighbour side: VL6, 10 of its 11. C10 15, VD2 15, VD4 14, VL4 8 all the other way.
- a, one hand per row: 33 rows of two or more hold, 0 break, 40 villas alone.
- b, blind side to the neighbour: 62 hold, 1 breaks, 2534 (VL6 on a face labelled C10, a corner plot).
- c, driver's house typologies (villa.driver_house in the profile): C10 15 of 15, VL6 10 of 11 (2534). Which side of the file holds the driver's house is not in the data, so c is test b on those typologies.
- d, the mirror flag against the neighbour on the left: C10 15, VD2 15, VD4 14, VL4 8 hold, VL6 breaks 10 of 11, because VL6 runs the other hand. Its origin sits at the other front corner (anchor start, VL4 and VD end, R121). The KEO file is not read, so the cause in the file is not proven.
- Full rule (b, and the row hand for the others): holds 93, breaks 2 (2534, 2535), undecided 8 (2293, 2296, 2303, 2308, 2313, 2318, 2351, 2354, each a villa alone between two cluster units).
- No rule holds on every villa. Villas stay out of the plan.

## A2 CASE second letter (R132)

- Explained of 337, and of the 321 outside the cut plots: facade option 182 and 171 (XX on all 337), facade colour 182 and 171 (XX on all), steps 209 and 199, unit 182 and 171, corner 182 and 171, mirror per unit 331 and 316, hand 331 and 317.
- Hand breaks outside: 2547, 2548, 2589, 2594. On the cut plots: 2545, 2546.
- The second letter (B, D, F, H) is the hand as drawn on nearly every link. No test holds on all 337, no CASE rule.
- A vote tie goes to the lower letter, fixed after the first test run, where a tie depended on the link order. The real data has no tie in the hand test.

## A3 FFL split (R133)

- IMC 96 clusters: same as the schedule 81 (all one FFL), schedule splits missed 11 of 11, extra splits 4 (M11 2464 to 2467, 2468 to 2471, 2472 to 2475, 2480 to 2483, heights 0.541 to 0.545 m with one FFL).
- IMD 129 clusters: same 116 (all one FFL), schedule splits missed 13 of 13.
- Schedule splits per code: M07 18 of 19, M08 6 of 6, M11 0 of 12, every pair 0.
- With the schedule walls given, piece FFL equals the schedule on 68 of 96 split plots, 28 are 1 to 5 mm under the rule.
- The rule reproduces no split. levels.py keeps one FFL per cluster.

## Second method (R134)

- villas: seed 14 20 of 20, seed 7 20 of 20, all 113 of 113. split: seed 14 20 of 20, seed 7 20 of 20, all 225 of 225. case: the hand count and breaks agree.
