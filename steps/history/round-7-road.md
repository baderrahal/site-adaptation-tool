## Round 7, 2026-10-22, LandXML road surfaces and the ramp level test

Done:
- Branch feat/road-levels in the second clone (site-adaptation-tool v2). The first clone is on round/ramp-corners for the other session and was not touched, nor was dotnet/ (git diff of dotnet/ is empty). This session started in C:\Users\bader, outside the repo, so the main guard hook was NOT live. I never committed to main. Main did not move during the round. Rule ids R40 to R46 were free on main and on every remote branch
- LandXML reader (R40, R41): python/landxml_reader.py streams the 222 MB file with expat in 1 MB blocks and keeps numpy arrays. 7 surfaces, 1260324 points, 2511038 faces, 288402 invisible. It ran in 6 s. Point counts per surface equal the chat counts for all 7. Coordinate system code 32637 and metre units. No surface is missing from the config and none is extra. EXISTING has 47846 of 56528 faces invisible, the other six 2.2 to 11.4 percent
- Lookup (R42): python/surface_lookup.py, grid index, visible triangles only, None outside, lowest triangle index wins on an edge. All 337 ramps (1348 corners) on 7 surfaces take 3.3 s
- Config (R43): config/road_surfaces.json, EXISTING_11.5 Sqkm is ground, the other six are design, with the note that the finished road is not confirmed
- Ramp test (R44): 337 ramp floors, all with 4 corners. Ramps by covered corners over all design surfaces: 0 corners 119, 1 corner 39, 2 corners 162, 3 corners 17, 4 corners 0. Chat said 119, 39, 162, 17, so I agree on every count. The 162 ramps with 2 covered corners are all neighbouring, none opposite, which chat did not say. Only Top Surface-02 covers any corner (414 corners). The other 5 design surfaces cover 0, agree. EXISTING covers all 1348 corners and the floor top is above it at every corner by 0.156 to 2.131 m, chat said 0.16 to 2.13, agree. Against Top Surface-02 the floor top is above the road at 71 of 414 corners and below at 343
- Gaps (R45): corners with no cover are 934. Distance to the nearest visible triangle, up to 1 m, up to 5 m, up to 20 m, more. Top Surface-02: 327, 1, 606, 0. Top Surface-01: 0, 0, 63, 871. The other four: 0, 0, 0, 934. For the 119 ramps with no covered corner, Top Surface-02 is within 1 m for all 119, Top Surface-01 within 20 m for 4, the other five surfaces are more than 20 m for all 119. I did not compare against the surface bounding boxes
- Plot check against Bader's Revit tag: the chat match within 0.02 m was NOT reproduced. The result is in sample_output/full_run/road_levels/plot_check.json and nowhere else. I cannot tell which Revit value Bader tagged or which corner is the street corner, so the file lists every corner against the floor top, the link Prototype-FFL and the other floor of the plot
- Full run (R46): stages landxml (limit 600 s) and road_levels added to python/full_run.py. Run on the real file, ok in 6.2 s and 3.3 s. A broken file is recorded with its exact text and the run goes on, road_levels is then skipped, tested
- Second method (R46, python/road_check.py, seed 821895143): tag counts from the raw bytes equal the reader on 7 of 7 surfaces. Every point and every visible face read again with regular expressions equal the arrays, 1260324 points and 2222636 faces, 0 differ. shapely with a plane fit on 200 random corner points against 7 surfaces (1400 pairs): 264 both give a level, 1136 both give none, 0 where only one gives a level, largest difference 0.0000000000001 m. 6 random ramps (plots 2459, 2621, 2406, 2401, 2334, 2513), 168 corner levels recomputed from the raw prototypes.json, 0 mismatches, largest difference 0.0005 m (the file rounds to 3 decimals)
- Mutation check: swapping northing and easting made 14 tests fail, ignoring the invisible flag made 5 fail, both put back and the file is byte identical. numpy 2.5.4 added to requirements.txt, it was already installed with shapely
- Gate: Python 127 passed (96 before), .NET 77 passed, build 0 errors

Remains:
- Which design surface is the finished road. The 119 ramps and 934 corners with no cover. The plot check. Placement work

Known bugs:
- A corner on the exact edge of a visible triangle counts as covered (barycentric tolerance 1e-9), a library that treats the edge as outside would differ, the shapely check found none in 1400 pairs
- The gap bands do not say whether a far corner lies inside the surface bounding box
- Revit z of the boundary is not used, topElevation shifted by the survey point offset is the only floor level. The offset in z is 0 in this file
- Still open from earlier rounds: 12 failing plots, many label faces, Revit not run on the link round

Next:
- Decide the finished road surface and the rule for the 119 ramps with no covered corner. Then take the level per ramp edge, not before

Your turn:
1. Which design surface is the finished road? Recommended: Top Surface-02 for the ramps, it is the only one that covers any corner. The other five cover none of the 337 ramps, so nothing here tests them
2. 119 ramps have no corner on Top Surface-02 but all are within 1 m of it, and 606 corners are 5 to 20 m away. Should a ramp with no covered corner take its level from the nearest edge of Top Surface-02? Recommended: no. Look at 3 of them in CAD with me first, extending a surface is a guess
3. The 0.02 m match on one plot did not reproduce, see sample_output/full_run/road_levels/plot_check.json. Which Revit value did you tag and which corner is the street corner? Recommended: send me the parameter name and the corner, then I compare that one pair
4. 39 ramps have 1 covered corner and 17 have 3. Is one covered corner enough to set that ramp edge? Recommended: no, only the two neighbouring corners of the 162 give an edge, the rest wait for answer 2
