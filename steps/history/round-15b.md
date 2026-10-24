# Round 15b, placer step 2, Generic, yard and ramp floors, 24 Oct

Full record of the round. The top entry of steps/log.md is the short form.

## Folders and gates

- The session started in C:\Users\bader\Documents\GitHub\site-adaptation-tool-15a while the Round 15a test session was still busy there. That folder was only read through git refs and never edited. The work was done in the main folder C:\Users\bader\Documents\GitHub\site-adaptation-tool on branch feat/placer-floors-15b.
- The main guard hook reads the branch of the folder the session started in. When the 15a session put its folder back on main, the hook blocked every git commit of this session, also in the main folder on the feature branch. Nothing was committed around it. The commit, the pull request and the merge wait for Bader.
- Gate A passed at 08:22, PR 18 merged, main at 6e0da3d. Gate B passed: no Revit.exe running, the 13 files of Prototypes_2024 read Format 2024 build 20250918_1515 from their BasicFileInfo stream (the 2021 C10 file reads Format 2021 the same way). Free memory was 3 to 3.6 GB, under the 8 GB of the parallel rule, so every Revit run was one window alone.

## Part 1

- R111 yard rule on the real files: 337 yards, assigned 337, tie 0, none 0. The shift of IMC is 10. Smallest top share 0.549, 12 under 0.9. Yard 945580 goes to label 326 (official 336, share 0.736, then 2311 with 0.264), which is no plot of the plan, so plot 2311 has no yard. The Generic floor of 2311 lies half on face 2301 (official 2311) and half on face 326, a baseplan label question.
- Past yard tops against FFL minus 0.100: 48 equal (TH1 16, TH3 16, VL6 16), 284 at the FFL (all other units), 3 at plus 0.025 and 1 at minus 0.021 from the rule. They are conflicts in the plan and past data in the check, never changed.
- 47 yards of the past model are two pieces. The reader keeps only the larger loop in sketchBoundary, so the plan takes the second piece from the top face outline (R112). No floor of the past model holds an arc.
- plan_imc_past.json regenerated: 337 plots, Generic 336 (2441 has none in the past model), yard 336 (2311 has none), ramp 337 (Ramps 0.5% 280, Ramps 2% 47, Flat Ramps 10). Plot value equal to unit-plot on all 336 Generic and 337 ramps of the past model. Conflicts: floors.yard.z 288, levels.ffl against Prototype-FFL 258, unit against file name 10, unit against schedule 2, linkName 2.
- API calls of the floor step: Floor.Create(document, IList<CurveLoop>, floorTypeId, levelId), Line.CreateBound and Arc.Create(end0, end1, pointOnArc) for the loops, FLOOR_HEIGHTABOVELEVEL_PARAM for the height, then for a ramp Floor.GetSlabShapeEditor, SlabShapeEditor.Enable and ModifySubElement(vertex, offset) per corner vertex, and Document.Regenerate twice per run. Why: the past model holds its ramps as shape edited floors (cornersSource shapeEditor on all 337), Floor.Create gives a flat floor at its level and height, and ModifySubElement lifts each corner by its own amount above the floor top, so a ramp made flat at its lowest corner gets every corner exact. Regenerating once per floor would cost a regeneration of the whole model per floor, the phases cost two.
- The check proved outside Revit: the answer key against itself cut to the plan floors with the yard plot values passes with 0 placer and 0 past data differences. Planted changes, each caught: outline moved 10 mm, top 5 mm off, corner z 4 mm off (also the slope), Plot missing on a yard, wrong ramp type, a floor placed twice, a floor missing, and an empty model with the wrong expected end.

## Part 2 files

Deleted by python/scenario_files.py clean-old, the TEST copies of the 15a test in Zone 02\ARC, 27 files, 1,413,824,512 bytes:

| File | Bytes |
|---|---|
| _TEST-C10.rvt, .0002.rvt | 52,379,648, 52,363,264 |
| _TEST-DP1.rvt, .0002.rvt | 52,379,648, 52,346,880 |
| _TEST-DP2.rvt, .0002.rvt | 52,379,648, 52,367,360 |
| _TEST-DP3.rvt, .0002.rvt | 52,383,744, 52,371,456 |
| _TEST-DP4.rvt, .0002.rvt | 52,383,744, 52,355,072 |
| _TEST-DT1.rvt, .0002.rvt | 52,387,840, 52,346,880 |
| _TEST-DT3.rvt, .0003.rvt, _run1.rvt | 52,359,168, 52,301,824, 52,301,824 |
| _TEST-TH1.rvt, .0002.rvt | 52,379,648, 52,363,264 |
| _TEST-TH3.rvt, .0002.rvt | 52,383,744, 52,367,360 |
| _TEST-VD2.rvt, .0002.rvt | 52,387,840, 52,359,168 |
| _TEST-VD4.rvt, .0002.rvt | 52,371,456, 52,342,784 |
| _TEST-VL4.rvt, .0002.rvt | 52,379,648, 52,363,264 |
| _TEST-VL6.rvt, .0002.rvt | 52,371,456, 52,346,880 |

Each name is 01101-PSN-IMC-ZZZ-PIM-TU-000001 plus the tail. Left alone: the site model, its _backup folder (not a TEST copy), the IMD model, Prototypes, Prototypes_2024 with its 13 _backup folders of the 15a upgrade, and Revit_temp.

Made and deleted this round: the work files S1, S2, S3, S4, S5, S6 and S8, each deleted after its check, at most 3 at once. Each save deleted the one backup it left. Left: 01101-PSN-IMC-ZZZ-PIM-TU-000001_TEST-MASTER.rvt, 48,742,400 bytes, every link unloaded, no floor of the five types. The site model was unchanged by hash after every run and the master after every work run.

## Part 2 runs

- Master: links 14 set to unloaded, 1010 floors deleted (Flat Ramps 10, Generic 150mm 336, Ramps 0.5% 280, Ramps 2% 47, yard 337), no floor was in a model group, 0 left, 1.1 min.
- S1: placed 1009 in 61 s, run 1.8 min. Every check equal on 336 Generic, 336 yards and 337 ramps, outline worst 0 mm, area and top equal, 288 yard tops past data. 767 Revit warnings, all Highlighted floors overlap, the outlines are the past ones to 0 mm. Second method seed 151, 6 of 6.
- S2 on a copy of the S1 result with runTwice: both real runs placed 0 and skipped 1011 (1009 already placed, 2 not in the plan), floors 1009 before and after. Seed 152, 6 of 6.
- S3 on a copy of the master, dry run only: would place 1009 floors on 337 plots, model 0 floors after the save.
- S4 on a copy of the S1 result: seed 15 drew plots 2294 2297 2307 2317 2364 2369 2395 2411 2477 2555. 30 floors deleted (Generic 10, yard 10, Ramps 0.5% 9, Ramps 2% 1), 30 placed, 981 skipped, the placed plots are those 10, all 337 match. Seed 154, 6 of 6.
- S5 with plan_S5.json, the ramp of plot 2458 on a type that floorSettings lists and the model lacks: the dry run and the real run stopped before any change, 0 floors before and after.
- S6 with plan_S6.json, the Generic loop of plot 2458 crossed: Floor.Create refused it inside the transaction (The input curve loops cannot compose a valid boundary), everything rolled back, 0 floors before and after.
- S7 from S1: Ramps 0.5% 280 of 280, Ramps 2% 47 of 47, Flat Ramps 10 of 10 match on every check. Plots with an arc: 0, the past model has none.
- S8 on a copy of the master, C10 from Prototypes_2024: 15 links within 1 mm with rotation, mirror, Name and parameters equal, 45 floors equal, 15 yard tops past data. Seed 158, 6 of 6.
